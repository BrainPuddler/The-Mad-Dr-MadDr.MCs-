using MadDr.RosterClient;
using Xunit;

namespace MadDr.RosterClient.Tests;

/// <summary>
/// Golden values captured from the REAL site/creature-renderer.js
/// locomotionProfile running in node (same discipline as the Rng port):
///
///   tetrapod-mastermind {"mass":3.02,"power":33.8,"margin":9.3,"walkSpeed":3.91,"runSpeed":7.96,"walkHz":1.6,"runHz":2.72,"sprint":"strong"}
///   crab-titan          {"mass":5.07,"power":79.4,"margin":55.5,"walkSpeed":3.96,"runSpeed":8.71,"walkHz":1.6,"runHz":2.72,"sprint":"strong"}
///   serpentine-faint    {"mass":3.09,"power":11.5,"margin":-9.9,"walkSpeed":1.56,"runSpeed":1.71,"walkHz":1.04,"runHz":1.77,"sprint":"none"}
///
/// The first fixture is the same real captured spawn genome
/// GenomeDtoTests already uses; the other two exercise the crab
/// arm-assist path and the sprint=none starved-heart path.
/// </summary>
public class LocomotionTests
{
    private static GenomeDto Genome(string plan, double[] bodyParams, string brainTier,
        string heartTier, double[] heartParams, string handFam, string legFam, double[] legParams)
    {
        var half = new double[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 };
        return new GenomeDto(2, null, new string[0],
            new BodyGenesDto(plan, bodyParams),
            new BrainGenesDto(brainTier, new double[] { 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new HeartGenesDto(heartTier, heartParams),
            new SlotsDto(
                new PartAlleleDto(handFam, half, null),
                new PartAlleleDto("antenna", half, null),
                new PartAlleleDto("bug_eyes", half, null),
                new PartAlleleDto(legFam, legParams, null)));
    }

    [Fact]
    public void Tetrapod_mastermind_matches_the_JS_reference()
    {
        var g = Genome("tetrapod",
            new[] { 0.3776107709854841, 0.30856891931034625, 0.9898015218786895, 0.16002239705994725 },
            "mastermind", "steady",
            new[] { 1.0, 0.9416415798477829, 0.8976952487137169, 0.168225810630247, 0.3277691132389009, 0.9080030561890453 },
            "claw_hand", "talon_leg",
            new[] { 0.8613541850354522, 0.7053924158681184, 0.7143220356665552, 0.8728707591071725, 0.19388159876689315, 0.9697388194035739 });

        var p = Locomotion.Profile(g);
        Assert.Equal(3.02, p.Mass);
        Assert.Equal(33.8, p.Power);
        Assert.Equal(9.3, p.Margin);
        Assert.Equal(3.91, p.WalkSpeedHexPerMin);
        Assert.Equal(7.96, p.RunSpeedHexPerMin);
        Assert.Equal(1.6, p.WalkHz);
        Assert.Equal(2.72, p.RunHz);
        Assert.Equal("strong", p.Sprint);
    }

    [Fact]
    public void Crab_titan_with_working_hand_matches_the_JS_reference_arm_assist_included()
    {
        var g = Genome("crab",
            new[] { 0.2, 0.8, 0.4, 0.1 },
            "dim", "titan",
            new[] { 0.9, 0.5, 0.5, 0.5, 0.5, 0.5 },
            "plasma_lance", "insect_leg",
            new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 });

        var p = Locomotion.Profile(g);
        Assert.Equal(5.07, p.Mass);
        Assert.Equal(79.4, p.Power);
        Assert.Equal(55.5, p.Margin);
        Assert.Equal(3.96, p.WalkSpeedHexPerMin);
        Assert.Equal(8.71, p.RunSpeedHexPerMin);
        Assert.Equal("strong", p.Sprint);
    }

    [Fact]
    public void Starved_serpentine_matches_the_JS_reference_sprint_none()
    {
        var g = Genome("serpentine",
            new[] { 0.7, 0.3, 0.6, 0.9 },
            "average", "faint",
            new[] { 0.2, 0.5, 0.5, 0.5, 0.5, 0.5 },
            "hand_stump", "tendril_leg",
            new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 });

        var p = Locomotion.Profile(g);
        Assert.Equal(3.09, p.Mass);
        Assert.Equal(11.5, p.Power);
        Assert.Equal(-9.9, p.Margin);
        Assert.Equal(1.56, p.WalkSpeedHexPerMin);
        Assert.Equal(1.71, p.RunSpeedHexPerMin);
        Assert.Equal(1.04, p.WalkHz);
        Assert.Equal(1.77, p.RunHz);
        Assert.Equal("none", p.Sprint);
    }

    [Fact]
    public void Meters_per_second_conversion_uses_the_20m_hex()
    {
        var g = Genome("tetrapod", new[] { 0.5, 0.5, 0.5, 0.5 }, "average", "steady",
            new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }, "claw_hand", "hoofed_leg",
            new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 });
        var p = Locomotion.Profile(g);
        Assert.Equal(p.WalkSpeedHexPerMin * 20.0 / 60.0, p.WalkMetersPerSecond(), 10);
        Assert.Equal(p.WalkMetersPerSecond() * 3.0, p.WalkMetersPerSecond(3.0), 10);
    }

    [Theory]
    [InlineData("tetrapod", 4)]
    [InlineData("winged", 2)]
    [InlineData("avian", 2)]
    [InlineData("crab", 6)]
    [InlineData("arachnid", 8)]
    [InlineData("treant", 2)]
    [InlineData("serpentine", 0)]
    [InlineData("blob", 0)]
    [InlineData("floater", 0)]
    public void Leg_counts_match_the_plan_silhouettes(string plan, int legs)
    {
        Assert.Equal(legs, Locomotion.LegsFor(plan));
    }
}
