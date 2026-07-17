using MadDr.RosterClient;
using Xunit;

namespace MadDr.RosterClient.Tests;

/// <summary>
/// Golden values captured from the REAL genome-core harvest.ts running in
/// node (same discipline as LocomotionTests/WeaponTests). If Harvest.cs
/// and harvest.ts ever disagree -- a bounds table drift, a tuning-knob
/// copy error -- the same harvester would gather/carry/slow differently in
/// the Lab preview and the Unity battlefield. These pin the two together.
/// </summary>
public class HarvestTests
{
    private static double[] P6(double x) => new[] { x, x, x, x, x, x };

    private static GenomeDto Genome(string plan, string handFam, double handP,
        string sensorFam, double sensorP, double bulk)
    {
        return new GenomeDto(2, null, new string[0],
            new BodyGenesDto(plan, new[] { 0.5, bulk, 0.5, 0.5 }),
            new BrainGenesDto("average", new[] { 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new HeartGenesDto("steady", P6(0.5)),
            new SlotsDto(
                new PartAlleleDto(handFam, P6(handP), null),
                new PartAlleleDto(sensorFam, P6(sensorP), null),
                new PartAlleleDto("bug_eyes", P6(0.5), null),
                new PartAlleleDto("hoofed_leg", P6(0.5), null)));
    }

    private static void AssertClose(double expected, double actual)
    {
        Assert.True(System.Math.Abs(expected - actual) < 1e-3,
            $"expected {expected}, got {actual}");
    }

    [Fact]
    public void Lamprey_maw_drinks_blood_fast_and_from_the_living()
    {
        var p = Harvest.Profile(Genome("tetrapod", "lamprey_maw", 0.5, "antenna", 0.5, 0.5));
        AssertClose(3.075, p.GatherBlood);
        AssertClose(0.3075, p.GatherBone);
        AssertClose(0.41, p.GatherBrain);
        Assert.True(p.DrainsLiving);
        AssertClose(17.5, p.Capacity);
        Assert.False(p.HasVessel);
        Assert.False(p.Flies);
    }

    [Fact]
    public void Bone_saw_cuts_bone_from_corpses_and_a_steel_tank_carries()
    {
        var p = Harvest.Profile(Genome("tetrapod", "bone_saw", 0.5, "steel_tank", 0.7, 0.5));
        AssertClose(0.525, p.GatherBlood);
        AssertClose(3.15, p.GatherBone);
        AssertClose(0.63, p.GatherBrain);
        Assert.False(p.DrainsLiving);
        AssertClose(68.425, p.Capacity);
        Assert.True(p.HasVessel);
    }

    [Fact]
    public void Ichor_siphon_with_amber_vesicle_matches_the_JS_reference()
    {
        var p = Harvest.Profile(Genome("tetrapod", "ichor_siphon", 0.6, "amber_vesicle", 0.6, 0.3));
        AssertClose(2.52, p.GatherBlood);
        AssertClose(0.315, p.GatherBone);
        AssertClose(0.84, p.GatherBrain);
        Assert.True(p.DrainsLiving);
        AssertClose(49.15, p.Capacity);
        Assert.True(p.HasVessel);
    }

    [Fact]
    public void Generic_claws_gather_but_a_gun_barely_does()
    {
        var claws = Harvest.Profile(Genome("tetrapod", "claw_hand", 0.5, "storage_bladder", 0.6, 0.5));
        AssertClose(1.0, claws.GatherBlood);
        AssertClose(1.0, claws.GatherBone);
        AssertClose(57.1, claws.Capacity);

        var rifle = Harvest.Profile(Genome("tetrapod", "rifle_arm", 0.5, "antenna", 0.5, 0.5));
        AssertClose(0.4, rifle.GatherBlood);
        Assert.True(rifle.GatherBlood < claws.GatherBlood);
    }

    [Fact]
    public void Blob_body_is_its_own_bag()
    {
        var p = Harvest.Profile(Genome("blob", "lamprey_maw", 0.9, "storage_bladder", 0.8, 0.7));
        AssertClose(4.035, p.GatherBlood);
        AssertClose(100.95, p.Capacity); // (20.5 + 46.8) * 1.5 blob bonus
    }

    [Fact]
    public void Winged_carrier_flies_and_a_stump_gathers_nothing()
    {
        var winged = Harvest.Profile(Genome("winged", "ichor_siphon", 0.4, "amber_vesicle", 0.5, 0.4));
        AssertClose(2.16, winged.GatherBlood);
        AssertClose(46.25, winged.Capacity);
        Assert.True(winged.Flies);

        var stump = Harvest.Profile(Genome("tetrapod", "hand_stump", 0.1, "antenna", 0.5, 0.5));
        AssertClose(0, stump.GatherBlood);
        AssertClose(0, stump.GatherBone);
    }

    [Fact]
    public void Weight_factors_match_the_JS_reference_and_flight_pays_double()
    {
        double[] fills = { 0, 0.25, 0.5, 0.75, 1, 2, -1 };
        double[] gsf = { 1, 0.9375, 0.875, 0.8125, 0.75, 0.75, 1 };
        double[] fsf = { 1, 0.875, 0.75, 0.625, 0.5, 0.5, 1 };
        for (var i = 0; i < fills.Length; i++)
        {
            AssertClose(gsf[i], Harvest.GroundSpeedFactor(fills[i]));
            AssertClose(fsf[i], Harvest.FlightSpeedFactor(fills[i]));
        }
        // flight hurts more than ground at the same fill, both floored
        Assert.True(Harvest.FlightSpeedFactor(0.5) < Harvest.GroundSpeedFactor(0.5));
        Assert.True(Harvest.FlightSpeedFactor(1) >= 0.4);
        Assert.True(Harvest.GroundSpeedFactor(1) >= 0.6);
    }
}
