using MadDr.RosterClient;
using Xunit;

namespace MadDr.RosterClient.Tests;

/// <summary>
/// Fixtures below are VERBATIM captures from a real running
/// packages/mutator-service (POST /spawn, PUT+GET /menagerie,
/// GET /creature/:id against a live local instance) -- not hand-written
/// approximations. If mutator-service's response shape ever changes,
/// these tests should fail loudly against the real thing, not silently
/// pass against a shape that was only ever imagined.
/// </summary>
public class GenomeDtoTests
{
    private const string StoredGenomeFixture = """
    {
      "id": "cr_e85fdbb7a81e2c7c68",
      "accountId": "test-account-1783904806",
      "genome": {
        "genomeVersion": 2,
        "parentIds": [],
        "body": {
          "plan": "tetrapod",
          "params": [0.3776107709854841, 0.30856891931034625, 0.9898015218786895, 0.16002239705994725]
        },
        "brain": {
          "tier": "mastermind",
          "params": [0.856841839151457, 0.07018761313520372, 0.7532138007227331, 0.8283258976880461, 0.49623918696306646]
        },
        "heart": {
          "tier": "steady",
          "params": [1, 0.9416415798477829, 0.8976952487137169, 0.168225810630247, 0.3277691132389009, 0.9080030561890453]
        },
        "slots": {
          "hand": {"family": "claw_hand", "params": [0.8602430273313075, 0.3528051266912371, 0.12743810145184398, 0.10257222130894661, 0.9823367290664464, 0.6784482116345316]},
          "sensor": {"family": "horn", "params": [0.16267869505099952, 0.7607320595998317, 0.6388200763612986, 0.692019609734416, 0.7178837396204472, 0.6747803124599159]},
          "eye": {"family": "stalk_eyes", "params": [0.24463836988434196, 0.24862536368891597, 0.5587546087335795, 0.8438383494503796, 0.7956113221589476, 0.21233270806260407]},
          "leg": {"family": "talon_leg", "params": [0.8613541850354522, 0.7053924158681184, 0.7143220356665552, 0.8728707591071725, 0.19388159876689315, 0.9697388194035739]}
        },
        "creatureId": "cr_e85fdbb7a81e2c7c68"
      },
      "signature": "16b7a8e3d589da515c0ce131d0b75f1acb9a6f5e79fdc6729bf8ae4187d81923",
      "createdAt": "2026-07-13T01:06:46.331Z"
    }
    """;

    private const string MenagerieFixture =
        """{"accountId":"test-account-1783904806","creatureIds":["cr_e85fdbb7a81e2c7c68"],"updatedAt":"2026-07-13T01:06:53.165Z"}""";

    [Fact]
    public void Parses_a_real_captured_GET_creature_response()
    {
        var stored = StoredGenomeDto.FromJson(JsonValue.Parse(StoredGenomeFixture));

        Assert.Equal("cr_e85fdbb7a81e2c7c68", stored.Id);
        Assert.Equal("test-account-1783904806", stored.AccountId);
        Assert.Equal("16b7a8e3d589da515c0ce131d0b75f1acb9a6f5e79fdc6729bf8ae4187d81923", stored.Signature);
        Assert.Equal("2026-07-13T01:06:46.331Z", stored.CreatedAt);

        var g = stored.Genome;
        Assert.Equal(2, g.GenomeVersion);
        Assert.Equal("cr_e85fdbb7a81e2c7c68", g.CreatureId);
        Assert.Empty(g.ParentIds);

        Assert.Equal("tetrapod", g.Body.Plan);
        Assert.Equal(4, g.Body.Params.Length);
        Assert.Equal(0.3776107709854841, g.Body.Params[0]);

        Assert.Equal("mastermind", g.Brain.Tier);
        Assert.Equal(5, g.Brain.Params.Length);

        Assert.Equal("steady", g.Heart.Tier);
        Assert.Equal(6, g.Heart.Params.Length);
        Assert.Equal(1.0, g.Heart.Params[0]);

        Assert.Equal("claw_hand", g.Slots.Hand.Family);
        Assert.Equal("horn", g.Slots.Sensor.Family);
        Assert.Equal("stalk_eyes", g.Slots.Eye.Family);
        Assert.Equal("talon_leg", g.Slots.Leg.Family);
        Assert.Equal(6, g.Slots.Hand.Params.Length);
        Assert.Null(g.Slots.Hand.Hue); // native part, no graft -- hue absent, not zero
    }

    [Fact]
    public void Parses_a_real_captured_GET_menagerie_response()
    {
        var menagerie = MenagerieDto.FromJson(JsonValue.Parse(MenagerieFixture));

        Assert.Equal("test-account-1783904806", menagerie.AccountId);
        Assert.Equal(new[] { "cr_e85fdbb7a81e2c7c68" }, menagerie.CreatureIds);
        Assert.Equal("2026-07-13T01:06:53.165Z", menagerie.UpdatedAt);
    }

    [Fact]
    public void StoredGenome_round_trips_through_ToJson_then_FromJson_unchanged()
    {
        var original = StoredGenomeDto.FromJson(JsonValue.Parse(StoredGenomeFixture));
        var reparsed = StoredGenomeDto.FromJson(original.ToJson());

        Assert.Equal(original.Id, reparsed.Id);
        Assert.Equal(original.Signature, reparsed.Signature);
        Assert.Equal(original.Genome.Body.Plan, reparsed.Genome.Body.Plan);
        Assert.Equal(original.Genome.Body.Params, reparsed.Genome.Body.Params);
        Assert.Equal(original.Genome.Slots.Hand.Family, reparsed.Genome.Slots.Hand.Family);
        Assert.Equal(original.Genome.Slots.Hand.Params, reparsed.Genome.Slots.Hand.Params);
        Assert.Equal(original.Genome.CreatureId, reparsed.Genome.CreatureId);
    }

    [Fact]
    public void Menagerie_round_trips_through_ToJson_then_FromJson_unchanged()
    {
        var original = MenagerieDto.FromJson(JsonValue.Parse(MenagerieFixture));
        var reparsed = MenagerieDto.FromJson(original.ToJson());

        Assert.Equal(original.AccountId, reparsed.AccountId);
        Assert.Equal(original.CreatureIds, reparsed.CreatureIds);
        Assert.Equal(original.UpdatedAt, reparsed.UpdatedAt);
    }

    [Fact]
    public void A_part_with_hue_set_round_trips_the_hue()
    {
        var withHue = JsonValue.Parse("""{"family": "rifle_arm", "params": [0.1,0.2,0.3,0.4,0.5,0.6], "hue": 0.42}""");
        var part = PartAlleleDto.FromJson(withHue);
        Assert.Equal(0.42, part.Hue);

        var reparsed = PartAlleleDto.FromJson(part.ToJson());
        Assert.Equal(0.42, reparsed.Hue);
    }

    [Fact]
    public void RosterCache_bundles_menagerie_and_creatures_and_round_trips()
    {
        var menagerie = MenagerieDto.FromJson(JsonValue.Parse(MenagerieFixture));
        var creature = StoredGenomeDto.FromJson(JsonValue.Parse(StoredGenomeFixture));
        var cache = new RosterCache("test-account-1783904806", menagerie, new[] { creature }, "2026-07-13T01:07:00.000Z");

        var reparsed = RosterCache.FromJson(cache.ToJson());

        Assert.Equal(cache.AccountId, reparsed.AccountId);
        Assert.Equal(cache.FetchedAtUtc, reparsed.FetchedAtUtc);
        Assert.Single(reparsed.Creatures);
        Assert.Equal(creature.Id, reparsed.Creatures[0].Id);
        Assert.Equal(creature.Genome.Slots.Leg.Family, reparsed.Creatures[0].Genome.Slots.Leg.Family);
    }
}
