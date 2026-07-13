using System.Collections.Generic;

namespace MadDr.RosterClient
{
    internal static class JsonHelpers
    {
        public static List<JsonValue> NumbersToJson(double[] numbers)
        {
            var list = new List<JsonValue>(numbers.Length);
            foreach (var n in numbers) list.Add(JsonValue.Of(n));
            return list;
        }

        public static List<JsonValue> StringsToJson(string[] strings)
        {
            var list = new List<JsonValue>(strings.Length);
            foreach (var s in strings) list.Add(JsonValue.Of(s));
            return list;
        }
    }

    /// <summary>One part allele: a family name plus its 6 canalized-axis
    /// params (docs/15 PART_AXES: length, girth, taper, curl, count,
    /// ornament). Mirrors genome-core's PartAllele exactly.</summary>
    public sealed class PartAlleleDto
    {
        public string Family { get; }
        public double[] Params { get; } // length 6
        public double? Hue { get; } // present only on a grafted/transplanted part

        public PartAlleleDto(string family, double[] parameters, double? hue)
        {
            Family = family;
            Params = parameters;
            Hue = hue;
        }

        public static PartAlleleDto FromJson(JsonValue v)
        {
            var family = v.Field("family").AsString();
            var paramsArray = v.Field("params").AsArray();
            var parameters = new double[paramsArray.Count];
            for (var i = 0; i < paramsArray.Count; i++) parameters[i] = paramsArray[i].AsNumber();

            double? hue = null;
            var hueField = v.FieldOrNull("hue");
            if (hueField != null) hue = hueField.AsNumber();

            return new PartAlleleDto(family, parameters, hue);
        }

        public JsonValue ToJson()
        {
            var obj = new Dictionary<string, JsonValue>();
            obj["family"] = JsonValue.Of(Family);
            obj["params"] = JsonValue.Of(JsonHelpers.NumbersToJson(Params));
            if (Hue.HasValue) obj["hue"] = JsonValue.Of(Hue.Value);
            return JsonValue.Of(obj);
        }
    }

    /// <summary>The 4 body-plan slots (docs/15 SLOT_NAMES), each an
    /// independent part allele.</summary>
    public sealed class SlotsDto
    {
        public PartAlleleDto Hand { get; }
        public PartAlleleDto Sensor { get; }
        public PartAlleleDto Eye { get; }
        public PartAlleleDto Leg { get; }

        public SlotsDto(PartAlleleDto hand, PartAlleleDto sensor, PartAlleleDto eye, PartAlleleDto leg)
        {
            Hand = hand;
            Sensor = sensor;
            Eye = eye;
            Leg = leg;
        }

        public static SlotsDto FromJson(JsonValue v)
        {
            return new SlotsDto(
                PartAlleleDto.FromJson(v.Field("hand")),
                PartAlleleDto.FromJson(v.Field("sensor")),
                PartAlleleDto.FromJson(v.Field("eye")),
                PartAlleleDto.FromJson(v.Field("leg")));
        }

        public JsonValue ToJson()
        {
            var obj = new Dictionary<string, JsonValue>();
            obj["hand"] = Hand.ToJson();
            obj["sensor"] = Sensor.ToJson();
            obj["eye"] = Eye.ToJson();
            obj["leg"] = Leg.ToJson();
            return JsonValue.Of(obj);
        }
    }

    /// <summary>docs/15 BODY_AXES: posture, bulk, limb, tail (4 params).</summary>
    public sealed class BodyGenesDto
    {
        public string Plan { get; }
        public double[] Params { get; } // length 4

        public BodyGenesDto(string plan, double[] parameters)
        {
            Plan = plan;
            Params = parameters;
        }

        public static BodyGenesDto FromJson(JsonValue v)
        {
            var arr = v.Field("params").AsArray();
            var parameters = new double[arr.Count];
            for (var i = 0; i < arr.Count; i++) parameters[i] = arr[i].AsNumber();
            return new BodyGenesDto(v.Field("plan").AsString(), parameters);
        }

        public JsonValue ToJson()
        {
            var obj = new Dictionary<string, JsonValue>();
            obj["plan"] = JsonValue.Of(Plan);
            obj["params"] = JsonValue.Of(JsonHelpers.NumbersToJson(Params));
            return JsonValue.Of(obj);
        }
    }

    /// <summary>docs/16 BRAIN_AXES: command, will, temperament, guile, fury
    /// (5 params); tier is dim/average/gifted/mastermind.</summary>
    public sealed class BrainGenesDto
    {
        public string Tier { get; }
        public double[] Params { get; } // length 5

        public BrainGenesDto(string tier, double[] parameters)
        {
            Tier = tier;
            Params = parameters;
        }

        public static BrainGenesDto FromJson(JsonValue v)
        {
            var arr = v.Field("params").AsArray();
            var parameters = new double[arr.Count];
            for (var i = 0; i < arr.Count; i++) parameters[i] = arr[i].AsNumber();
            return new BrainGenesDto(v.Field("tier").AsString(), parameters);
        }

        public JsonValue ToJson()
        {
            var obj = new Dictionary<string, JsonValue>();
            obj["tier"] = JsonValue.Of(Tier);
            obj["params"] = JsonValue.Of(JsonHelpers.NumbersToJson(Params));
            return JsonValue.Of(obj);
        }
    }

    public sealed class HeartGenesDto
    {
        public string Tier { get; }
        public double[] Params { get; } // vigor + 5 more, tier-dependent length

        public HeartGenesDto(string tier, double[] parameters)
        {
            Tier = tier;
            Params = parameters;
        }

        public static HeartGenesDto FromJson(JsonValue v)
        {
            var arr = v.Field("params").AsArray();
            var parameters = new double[arr.Count];
            for (var i = 0; i < arr.Count; i++) parameters[i] = arr[i].AsNumber();
            return new HeartGenesDto(v.Field("tier").AsString(), parameters);
        }

        public JsonValue ToJson()
        {
            var obj = new Dictionary<string, JsonValue>();
            obj["tier"] = JsonValue.Of(Tier);
            obj["params"] = JsonValue.Of(JsonHelpers.NumbersToJson(Params));
            return JsonValue.Of(obj);
        }
    }

    /// <summary>The full genome, docs/06 v2 schema. Mirrors genome-core's
    /// Genome interface field-for-field, parsed from the exact JSON
    /// packages/mutator-service returns (verified against real captured
    /// server responses -- see Tests~).</summary>
    public sealed class GenomeDto
    {
        public double GenomeVersion { get; }
        public string? CreatureId { get; }
        public string[] ParentIds { get; }
        public BodyGenesDto Body { get; }
        public BrainGenesDto Brain { get; }
        public HeartGenesDto Heart { get; }
        public SlotsDto Slots { get; }

        public GenomeDto(double genomeVersion, string? creatureId, string[] parentIds,
            BodyGenesDto body, BrainGenesDto brain, HeartGenesDto heart, SlotsDto slots)
        {
            GenomeVersion = genomeVersion;
            CreatureId = creatureId;
            ParentIds = parentIds;
            Body = body;
            Brain = brain;
            Heart = heart;
            Slots = slots;
        }

        public static GenomeDto FromJson(JsonValue v)
        {
            var parentIdsArr = v.Field("parentIds").AsArray();
            var parentIds = new string[parentIdsArr.Count];
            for (var i = 0; i < parentIdsArr.Count; i++) parentIds[i] = parentIdsArr[i].AsString();

            var creatureIdField = v.FieldOrNull("creatureId");

            return new GenomeDto(
                v.Field("genomeVersion").AsNumber(),
                creatureIdField != null ? creatureIdField.AsString() : null,
                parentIds,
                BodyGenesDto.FromJson(v.Field("body")),
                BrainGenesDto.FromJson(v.Field("brain")),
                HeartGenesDto.FromJson(v.Field("heart")),
                SlotsDto.FromJson(v.Field("slots")));
        }

        public JsonValue ToJson()
        {
            var obj = new Dictionary<string, JsonValue>();
            obj["genomeVersion"] = JsonValue.Of(GenomeVersion);
            if (CreatureId != null) obj["creatureId"] = JsonValue.Of(CreatureId);
            obj["parentIds"] = JsonValue.Of(JsonHelpers.StringsToJson(ParentIds));
            obj["body"] = Body.ToJson();
            obj["brain"] = Brain.ToJson();
            obj["heart"] = Heart.ToJson();
            obj["slots"] = Slots.ToJson();
            return JsonValue.Of(obj);
        }
    }

    /// <summary>docs/07 StoredGenome: the row a creature actually lives
    /// in -- the genome plus its id/owner/signature/timestamp.</summary>
    public sealed class StoredGenomeDto
    {
        public string Id { get; }
        public string AccountId { get; }
        public GenomeDto Genome { get; }
        public string Signature { get; }
        public string CreatedAt { get; }

        public StoredGenomeDto(string id, string accountId, GenomeDto genome, string signature, string createdAt)
        {
            Id = id;
            AccountId = accountId;
            Genome = genome;
            Signature = signature;
            CreatedAt = createdAt;
        }

        public static StoredGenomeDto FromJson(JsonValue v)
        {
            return new StoredGenomeDto(
                v.Field("id").AsString(),
                v.Field("accountId").AsString(),
                GenomeDto.FromJson(v.Field("genome")),
                v.Field("signature").AsString(),
                v.Field("createdAt").AsString());
        }

        public JsonValue ToJson()
        {
            var obj = new Dictionary<string, JsonValue>();
            obj["id"] = JsonValue.Of(Id);
            obj["accountId"] = JsonValue.Of(AccountId);
            obj["genome"] = Genome.ToJson();
            obj["signature"] = JsonValue.Of(Signature);
            obj["createdAt"] = JsonValue.Of(CreatedAt);
            return JsonValue.Of(obj);
        }
    }

    /// <summary>docs/07 Menagerie: an account's active loadout (up to 12
    /// creature ids, ordered).</summary>
    public sealed class MenagerieDto
    {
        public string AccountId { get; }
        public string[] CreatureIds { get; }
        public string UpdatedAt { get; }

        public MenagerieDto(string accountId, string[] creatureIds, string updatedAt)
        {
            AccountId = accountId;
            CreatureIds = creatureIds;
            UpdatedAt = updatedAt;
        }

        public static MenagerieDto FromJson(JsonValue v)
        {
            var arr = v.Field("creatureIds").AsArray();
            var ids = new string[arr.Count];
            for (var i = 0; i < arr.Count; i++) ids[i] = arr[i].AsString();
            return new MenagerieDto(v.Field("accountId").AsString(), ids, v.Field("updatedAt").AsString());
        }

        public JsonValue ToJson()
        {
            var obj = new Dictionary<string, JsonValue>();
            obj["accountId"] = JsonValue.Of(AccountId);
            obj["creatureIds"] = JsonValue.Of(JsonHelpers.StringsToJson(CreatureIds));
            obj["updatedAt"] = JsonValue.Of(UpdatedAt);
            return JsonValue.Of(obj);
        }
    }
}
