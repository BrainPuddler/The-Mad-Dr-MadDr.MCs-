using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MadDr.RosterClient
{
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object,
    }

    /// <summary>
    /// A minimal, dependency-free JSON value/parser/writer -- no external
    /// package (Newtonsoft, System.Text.Json availability varies by Unity
    /// scripting backend/API compatibility level, and this repo already
    /// hit one real Unity Package Manager resolution failure this
    /// session; a second external dependency is exactly the kind of risk
    /// worth avoiding when a ~200-line hand-rolled parser, fully unit
    /// tested against real captured server responses, does the job with
    /// nothing to resolve). Standard JSON grammar (RFC 8259): objects,
    /// arrays, strings with escapes, numbers, true/false/null.
    /// </summary>
    public sealed class JsonValue
    {
        public JsonKind Kind { get; }
        private readonly bool _bool;
        private readonly double _number;
        private readonly string? _string;
        private readonly List<JsonValue>? _array;
        private readonly Dictionary<string, JsonValue>? _object;

        private JsonValue(JsonKind kind, bool b = false, double n = 0, string? s = null,
            List<JsonValue>? a = null, Dictionary<string, JsonValue>? o = null)
        {
            Kind = kind;
            _bool = b;
            _number = n;
            _string = s;
            _array = a;
            _object = o;
        }

        public static readonly JsonValue Null = new JsonValue(JsonKind.Null);
        public static JsonValue Of(bool b) { return new JsonValue(JsonKind.Bool, b: b); }
        public static JsonValue Of(double n) { return new JsonValue(JsonKind.Number, n: n); }
        public static JsonValue Of(string s) { return new JsonValue(JsonKind.String, s: s); }
        public static JsonValue Of(List<JsonValue> a) { return new JsonValue(JsonKind.Array, a: a); }
        public static JsonValue Of(Dictionary<string, JsonValue> o) { return new JsonValue(JsonKind.Object, o: o); }

        public bool AsBool()
        {
            if (Kind != JsonKind.Bool) throw new InvalidOperationException("not a bool: " + Kind);
            return _bool;
        }

        public double AsNumber()
        {
            if (Kind != JsonKind.Number) throw new InvalidOperationException("not a number: " + Kind);
            return _number;
        }

        public string AsString()
        {
            if (Kind != JsonKind.String || _string == null) throw new InvalidOperationException("not a string: " + Kind);
            return _string;
        }

        public List<JsonValue> AsArray()
        {
            if (Kind != JsonKind.Array || _array == null) throw new InvalidOperationException("not an array: " + Kind);
            return _array;
        }

        public Dictionary<string, JsonValue> AsObject()
        {
            if (Kind != JsonKind.Object || _object == null) throw new InvalidOperationException("not an object: " + Kind);
            return _object;
        }

        public JsonValue Field(string key)
        {
            var obj = AsObject();
            JsonValue? value;
            if (!obj.TryGetValue(key, out value) || value == null) throw new FormatException("missing field: " + key);
            return value;
        }

        public JsonValue? FieldOrNull(string key)
        {
            var obj = AsObject();
            JsonValue? value;
            if (obj.TryGetValue(key, out value) && value != null && value.Kind != JsonKind.Null) return value;
            return null;
        }

        // ---- parsing ----------------------------------------------------

        public static JsonValue Parse(string json)
        {
            var pos = 0;
            var value = ParseValue(json, ref pos);
            SkipWhitespace(json, ref pos);
            if (pos != json.Length) throw new FormatException("trailing content at position " + pos);
            return value;
        }

        private static JsonValue ParseValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length) throw new FormatException("unexpected end of input");
            var c = s[pos];
            if (c == '{') return ParseObject(s, ref pos);
            if (c == '[') return ParseArray(s, ref pos);
            if (c == '"') return Of(ParseString(s, ref pos));
            if (c == 't') { Expect(s, ref pos, "true"); return Of(true); }
            if (c == 'f') { Expect(s, ref pos, "false"); return Of(false); }
            if (c == 'n') { Expect(s, ref pos, "null"); return Null; }
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref pos);
            throw new FormatException("unexpected character '" + c + "' at position " + pos);
        }

        private static JsonValue ParseObject(string s, ref int pos)
        {
            pos++; // '{'
            var obj = new Dictionary<string, JsonValue>();
            SkipWhitespace(s, ref pos);
            if (Peek(s, pos) == '}') { pos++; return Of(obj); }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                var key = ParseString(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (Peek(s, pos) != ':') throw new FormatException("expected ':' at position " + pos);
                pos++;
                var value = ParseValue(s, ref pos);
                obj[key] = value;
                SkipWhitespace(s, ref pos);
                var next = Peek(s, pos);
                if (next == ',') { pos++; continue; }
                if (next == '}') { pos++; break; }
                throw new FormatException("expected ',' or '}' at position " + pos);
            }
            return Of(obj);
        }

        private static JsonValue ParseArray(string s, ref int pos)
        {
            pos++; // '['
            var arr = new List<JsonValue>();
            SkipWhitespace(s, ref pos);
            if (Peek(s, pos) == ']') { pos++; return Of(arr); }
            while (true)
            {
                var value = ParseValue(s, ref pos);
                arr.Add(value);
                SkipWhitespace(s, ref pos);
                var next = Peek(s, pos);
                if (next == ',') { pos++; continue; }
                if (next == ']') { pos++; break; }
                throw new FormatException("expected ',' or ']' at position " + pos);
            }
            return Of(arr);
        }

        private static string ParseString(string s, ref int pos)
        {
            if (Peek(s, pos) != '"') throw new FormatException("expected '\"' at position " + pos);
            pos++;
            var sb = new StringBuilder();
            while (true)
            {
                if (pos >= s.Length) throw new FormatException("unterminated string");
                var c = s[pos];
                if (c == '"') { pos++; break; }
                if (c == '\\')
                {
                    pos++;
                    if (pos >= s.Length) throw new FormatException("unterminated escape");
                    var esc = s[pos];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 >= s.Length) throw new FormatException("truncated \\u escape");
                            var hex = s.Substring(pos + 1, 4);
                            var code = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            sb.Append((char)code);
                            pos += 4;
                            break;
                        default:
                            throw new FormatException("unknown escape '\\" + esc + "'");
                    }
                    pos++;
                }
                else
                {
                    sb.Append(c);
                    pos++;
                }
            }
            return sb.ToString();
        }

        private static JsonValue ParseNumber(string s, ref int pos)
        {
            var start = pos;
            if (Peek(s, pos) == '-') pos++;
            while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9') pos++;
            if (Peek(s, pos) == '.')
            {
                pos++;
                while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9') pos++;
            }
            if (Peek(s, pos) == 'e' || Peek(s, pos) == 'E')
            {
                pos++;
                if (Peek(s, pos) == '+' || Peek(s, pos) == '-') pos++;
                while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9') pos++;
            }
            var text = s.Substring(start, pos - start);
            return Of(double.Parse(text, CultureInfo.InvariantCulture));
        }

        private static void Expect(string s, ref int pos, string literal)
        {
            if (pos + literal.Length > s.Length || s.Substring(pos, literal.Length) != literal)
                throw new FormatException("expected '" + literal + "' at position " + pos);
            pos += literal.Length;
        }

        private static char Peek(string s, int pos)
        {
            return pos < s.Length ? s[pos] : '\0';
        }

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t' || s[pos] == '\n' || s[pos] == '\r')) pos++;
        }

        // ---- writing ------------------------------------------------------

        public string Serialize()
        {
            var sb = new StringBuilder();
            Write(sb);
            return sb.ToString();
        }

        private void Write(StringBuilder sb)
        {
            switch (Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Bool:
                    sb.Append(_bool ? "true" : "false");
                    break;
                case JsonKind.Number:
                    // .NET Core 3+ default double.ToString() already
                    // round-trips exactly; no format specifier needed
                    // (the older "R" specifier is deprecated).
                    sb.Append(_number.ToString(CultureInfo.InvariantCulture));
                    break;
                case JsonKind.String:
                    WriteString(sb, _string!);
                    break;
                case JsonKind.Array:
                    sb.Append('[');
                    for (var i = 0; i < _array!.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        _array[i].Write(sb);
                    }
                    sb.Append(']');
                    break;
                case JsonKind.Object:
                    sb.Append('{');
                    var first = true;
                    foreach (var kv in _object!)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        kv.Value.Write(sb);
                    }
                    sb.Append('}');
                    break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
