using MadDr.RosterClient;
using Xunit;

namespace MadDr.RosterClient.Tests;

public class JsonTests
{
    [Fact]
    public void Parses_primitives()
    {
        Assert.Equal(JsonKind.Null, JsonValue.Parse("null").Kind);
        Assert.True(JsonValue.Parse("true").AsBool());
        Assert.False(JsonValue.Parse("false").AsBool());
        Assert.Equal(42.5, JsonValue.Parse("42.5").AsNumber());
        Assert.Equal(-3.0, JsonValue.Parse("-3").AsNumber());
        Assert.Equal(1.5e3, JsonValue.Parse("1.5e3").AsNumber());
        Assert.Equal("hello", JsonValue.Parse("\"hello\"").AsString());
    }

    [Fact]
    public void Parses_string_escapes()
    {
        Assert.Equal("a\"b\\c\nd\te", JsonValue.Parse("\"a\\\"b\\\\c\\nd\\te\"").AsString());
        Assert.Equal("A", JsonValue.Parse("\"\\u0041\"").AsString());
    }

    [Fact]
    public void Parses_nested_objects_and_arrays()
    {
        var v = JsonValue.Parse("""{"a": [1, 2, {"b": true}], "c": null}""");
        Assert.Equal(3, v.Field("a").AsArray().Count);
        Assert.Equal(1.0, v.Field("a").AsArray()[0].AsNumber());
        Assert.True(v.Field("a").AsArray()[2].Field("b").AsBool());
        Assert.Null(v.FieldOrNull("c"));
    }

    [Fact]
    public void Empty_object_and_array_parse_correctly()
    {
        Assert.Empty(JsonValue.Parse("{}").AsObject());
        Assert.Empty(JsonValue.Parse("[]").AsArray());
    }

    [Fact]
    public void FieldOrNull_returns_null_for_missing_or_json_null_but_Field_throws_on_missing()
    {
        var v = JsonValue.Parse("""{"present": null}""");
        Assert.Null(v.FieldOrNull("present"));
        Assert.Null(v.FieldOrNull("absent"));
        Assert.Throws<FormatException>(() => v.Field("absent"));
    }

    [Fact]
    public void Rejects_malformed_json()
    {
        Assert.Throws<FormatException>(() => JsonValue.Parse("{"));
        Assert.Throws<FormatException>(() => JsonValue.Parse("[1, 2"));
        Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\": }"));
        Assert.Throws<FormatException>(() => JsonValue.Parse("not json"));
        Assert.Throws<FormatException>(() => JsonValue.Parse("{}trailing"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData(0.3776107709854841)] // an actual genome param value, full precision
    [InlineData(2.0)]
    public void Numbers_round_trip_exactly_through_serialize_then_parse(double n)
    {
        var serialized = JsonValue.Of(n).Serialize();
        var reparsed = JsonValue.Parse(serialized).AsNumber();
        Assert.Equal(n, reparsed);
    }

    [Fact]
    public void Strings_with_special_characters_round_trip_through_serialize_then_parse()
    {
        var original = "quote\"backslash\\newline\ntab\tunicodeend";
        var serialized = JsonValue.Of(original).Serialize();
        Assert.Equal(original, JsonValue.Parse(serialized).AsString());
    }
}
