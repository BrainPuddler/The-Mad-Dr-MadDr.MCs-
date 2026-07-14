using MadDr.CreatureMesh;
using MadDr.RosterClient;
using Xunit;

namespace MadDr.CreatureMesh.Tests;

public class CreatureBuilderTests
{
    private static double[] Mid() => new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 };

    private static GenomeDto Genome(
        string plan = "tetrapod",
        double[]? bodyParams = null,
        string brainTier = "average",
        string heartTier = "steady",
        string hand = "claw_hand", double[]? handParams = null, double? handHue = null,
        string sensor = "antenna", double[]? sensorParams = null,
        string eye = "bug_eyes", double[]? eyeParams = null,
        string leg = "hoofed_leg", double[]? legParams = null)
    {
        return new GenomeDto(2, "test-creature", new string[0],
            new BodyGenesDto(plan, bodyParams ?? new[] { 0.5, 0.5, 0.5, 0.5 }),
            new BrainGenesDto(brainTier, new[] { 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new HeartGenesDto(heartTier, new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new SlotsDto(
                new PartAlleleDto(hand, handParams ?? Mid(), handHue),
                new PartAlleleDto(sensor, sensorParams ?? Mid(), null),
                new PartAlleleDto(eye, eyeParams ?? Mid(), null),
                new PartAlleleDto(leg, legParams ?? Mid(), null)));
    }

    private static bool HasColor(CreatureMeshResult r, Col c)
    {
        foreach (var chunk in r.Chunks)
            if ((int)chunk.Color.R == (int)c.R && (int)chunk.Color.G == (int)c.G && (int)chunk.Color.B == (int)c.B)
                return true;
        return false;
    }

    private static int TotalTris(CreatureMeshResult r)
    {
        var n = 0;
        foreach (var chunk in r.Chunks) n += chunk.Triangles.Count / 3;
        return n;
    }

    [Fact]
    public void NonTetrapodPlansReturnNull()
    {
        Assert.Null(CreatureBuilder.Build(Genome(plan: "blob")));
        Assert.Null(CreatureBuilder.Build(Genome(plan: "crab")));
        Assert.NotNull(CreatureBuilder.Build(Genome()));
    }

    [Fact]
    public void SameGenomeBuildsIdenticalMesh()
    {
        var a = CreatureBuilder.Build(Genome())!;
        var b = CreatureBuilder.Build(Genome())!;
        Assert.Equal(a.Chunks.Count, b.Chunks.Count);
        for (var i = 0; i < a.Chunks.Count; i++)
        {
            Assert.Equal(a.Chunks[i].Positions, b.Chunks[i].Positions);
            Assert.Equal(a.Chunks[i].Normals, b.Chunks[i].Normals);
            Assert.Equal(a.Chunks[i].Triangles, b.Chunks[i].Triangles);
        }
    }

    [Fact]
    public void AllChunksHaveValidGeometry()
    {
        var r = CreatureBuilder.Build(Genome(brainTier: "mastermind", heartTier: "titan"))!;
        Assert.True(r.Chunks.Count > 0);
        foreach (var c in r.Chunks)
        {
            Assert.Equal(0, c.Positions.Count % 3);
            Assert.Equal(c.Positions.Count, c.Normals.Count);
            Assert.Equal(0, c.Triangles.Count % 3);
            foreach (var idx in c.Triangles)
                Assert.InRange(idx, 0, c.VertexCount - 1);
            foreach (var v in c.Positions) Assert.True(double.IsFinite(v));
            foreach (var v in c.Normals) Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void WindingAgreesWithNormalsEverywhere()
    {
        // Unity single-sides its materials: a triangle wound against its
        // own analytic normals would be culled to an invisible hole
        var r = CreatureBuilder.Build(Genome(brainTier: "mastermind", heartTier: "titan",
            hand: "laser_array", sensor: "horn", eye: "stalk_eyes"))!;
        foreach (var c in r.Chunks)
        {
            for (var t = 0; t < c.Triangles.Count; t += 3)
            {
                int i0 = c.Triangles[t] * 3, i1 = c.Triangles[t + 1] * 3, i2 = c.Triangles[t + 2] * 3;
                var e1 = new Vec3(c.Positions[i1] - c.Positions[i0], c.Positions[i1 + 1] - c.Positions[i0 + 1], c.Positions[i1 + 2] - c.Positions[i0 + 2]);
                var e2 = new Vec3(c.Positions[i2] - c.Positions[i0], c.Positions[i2 + 1] - c.Positions[i0 + 1], c.Positions[i2 + 2] - c.Positions[i0 + 2]);
                var g = Vec3.Cross(e1, e2);
                var n = new Vec3(
                    c.Normals[i0] + c.Normals[i1] + c.Normals[i2],
                    c.Normals[i0 + 1] + c.Normals[i1 + 1] + c.Normals[i2 + 1],
                    c.Normals[i0 + 2] + c.Normals[i1 + 2] + c.Normals[i2 + 2]);
                Assert.True(Vec3.Dot(g, n) >= 0, "triangle wound against its normals");
            }
        }
    }

    [Theory]
    [InlineData("claw_hand")]
    [InlineData("pincer")]
    [InlineData("tentacle")]
    [InlineData("rifle_arm")]
    [InlineData("plasma_lance")]
    [InlineData("chain_blade")]
    [InlineData("spore_launcher")]
    [InlineData("laser_array")]
    [InlineData("photon_blaster")]
    [InlineData("hand_stump")]
    public void EveryHandFamilyAddsGeometry(string family)
    {
        var without = CreatureBuilder.Build(Genome(hand: "hand_stump"))!;
        var with = CreatureBuilder.Build(Genome(hand: family))!;
        Assert.True(TotalTris(with) > 0);
        if (family != "hand_stump")
            Assert.NotEqual(TotalTris(without), TotalTris(with));
    }

    [Fact]
    public void WeaponFamiliesCarryTheirSignatureGlowColors()
    {
        Assert.True(HasColor(CreatureBuilder.Build(Genome(hand: "laser_array"))!, Palette.LASER_N));
        Assert.True(HasColor(CreatureBuilder.Build(Genome(hand: "photon_blaster"))!, Palette.PHOTON_N));
        Assert.True(HasColor(CreatureBuilder.Build(Genome(hand: "plasma_lance"))!, Palette.BLTGLO));
        Assert.True(HasColor(CreatureBuilder.Build(Genome(hand: "claw_hand"))!, Palette.CLAW));
    }

    [Theory]
    [InlineData("antenna")]
    [InlineData("horn")]
    [InlineData("sensor_mast")]
    [InlineData("sensor_stub")]
    public void EverySensorFamilyBuilds(string family)
    {
        var r = CreatureBuilder.Build(Genome(sensor: family))!;
        Assert.True(TotalTris(r) > 0);
    }

    [Theory]
    [InlineData("bug_eyes")]
    [InlineData("cyclops_eye")]
    [InlineData("stalk_eyes")]
    [InlineData("optic_visor")]
    [InlineData("eye_socket")]
    public void EveryEyeFamilyBuilds(string family)
    {
        var r = CreatureBuilder.Build(Genome(eye: family))!;
        Assert.True(TotalTris(r) > 0);
    }

    [Fact]
    public void OrganicEyesHaveEyeballs()
    {
        Assert.True(HasColor(CreatureBuilder.Build(Genome(eye: "bug_eyes"))!, Palette.EYEWH));
        Assert.True(HasColor(CreatureBuilder.Build(Genome(eye: "cyclops_eye"))!, Palette.EYEWH));
        Assert.False(HasColor(CreatureBuilder.Build(Genome(eye: "eye_socket", sensor: "antenna"))!, Palette.EYEWH));
    }

    [Fact]
    public void MastermindGrowsBrainUnderGlass()
    {
        var r = CreatureBuilder.Build(Genome(brainTier: "mastermind"))!;
        Assert.True(HasColor(r, Palette.BRAINC));
        var foundGlass = false;
        foreach (var c in r.Chunks)
            if (c.Alpha < 0.99) { foundGlass = true; Assert.Equal(0.24, c.Alpha, 3); }
        Assert.True(foundGlass, "mastermind must have a translucent glass dome chunk");

        var avg = CreatureBuilder.Build(Genome(brainTier: "average"))!;
        Assert.False(HasColor(avg, Palette.BRAINC));
    }

    [Fact]
    public void DormantSensorGeneSkipsAntennaAndHorn()
    {
        var lowOrn = Mid(); lowOrn[5] = 0.1;
        var bald = CreatureBuilder.Build(Genome(sensor: "antenna", sensorParams: lowOrn))!;
        Assert.False(HasColor(bald, Palette.BONE));   // antenna shaft color absent
        var active = CreatureBuilder.Build(Genome(sensor: "antenna"))!;
        Assert.True(HasColor(active, Palette.BONE));
    }

    [Fact]
    public void TailGeneGatesTail()
    {
        var noTail = CreatureBuilder.Build(Genome(bodyParams: new[] { 0.5, 0.5, 0.5, 0.2 }))!;
        var bigTail = CreatureBuilder.Build(Genome(bodyParams: new[] { 0.5, 0.5, 0.5, 0.9 }))!;
        Assert.True(TotalTris(bigTail) > TotalTris(noTail));
    }

    [Fact]
    public void HeadlessCreatureHasNoSkullOrFace()
    {
        var r = CreatureBuilder.Build(Genome(sensor: "sensor_stub", eye: "eye_socket"))!;
        Assert.False(HasColor(r, Palette.EYEWH));
        Assert.False(HasColor(r, Palette.MOUTHC));   // no jaw/mouth without a head
        var headed = CreatureBuilder.Build(Genome())!;
        Assert.True(HasColor(headed, Palette.MOUTHC));
        Assert.True(r.TopY < headed.TopY);
    }

    [Fact]
    public void TitanHeartGlowingNeckBolts()
    {
        Assert.True(HasColor(CreatureBuilder.Build(Genome(heartTier: "titan"))!, Palette.BLTGLO));
        var faint = CreatureBuilder.Build(Genome(heartTier: "faint", hand: "claw_hand"))!;
        Assert.False(HasColor(faint, Palette.BOLT));   // faint heart: no bolts at all
        var steady = CreatureBuilder.Build(Genome(heartTier: "steady"))!;
        Assert.True(HasColor(steady, Palette.BOLT));
    }

    [Fact]
    public void GraftedHandKeepsItsOwnHue()
    {
        var native = CreatureBuilder.Build(Genome(hand: "tentacle"))!;
        var grafted = CreatureBuilder.Build(Genome(hand: "tentacle", handHue: 0.95))!;
        // the grafted tentacle's skin chunk color differs from the body's
        var nativeCols = new System.Collections.Generic.HashSet<string>();
        foreach (var c in native.Chunks) nativeCols.Add($"{(int)c.Color.R},{(int)c.Color.G},{(int)c.Color.B}");
        var extra = 0;
        foreach (var c in grafted.Chunks)
            if (!nativeCols.Contains($"{(int)c.Color.R},{(int)c.Color.G},{(int)c.Color.B}")) extra++;
        Assert.True(extra > 0, "graft hue must mint a distinct skin color chunk");
    }

    [Fact]
    public void LegSocketMatchesLegGenes()
    {
        var r = CreatureBuilder.Build(Genome(leg: "hoofed_leg"))!;
        Assert.NotNull(r.Leg);
        Assert.Equal("hoofed_leg", r.Leg!.Family);
        Assert.Equal(3.0, r.Leg.Len, 6);            // clamp(2.4 + 1.2*0.5)
        Assert.Equal(r.Leg.Len, r.Leg.P.Y, 6);
        Assert.True(r.Leg.P.X > 0);

        var stump = CreatureBuilder.Build(Genome(leg: "leg_stump"))!;
        Assert.Equal(0.6, stump.Leg!.Len, 6);
        Assert.True(stump.WaistY < r.WaistY);       // stumps slump low
    }

    [Fact]
    public void BodySitsAboveGroundAndBelowTop()
    {
        var r = CreatureBuilder.Build(Genome())!;
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var c in r.Chunks)
            for (var i = 1; i < c.Positions.Count; i += 3)
            {
                if (c.Positions[i] < minY) minY = c.Positions[i];
                if (c.Positions[i] > maxY) maxY = c.Positions[i];
            }
        // the torso hangs on legs (rig-owned): nothing except a dangling
        // arm should come near the ground, nothing below it
        Assert.True(minY > -1.0, $"geometry dips implausibly low: {minY}");
        // TopY is the SKULL top (the Lab's semantics) -- antennae/masts
        // legitimately arc past it, bounded by their max length gene
        Assert.True(maxY <= r.TopY + 4.5, $"geometry pokes far above TopY: {maxY} vs {r.TopY}");
        Assert.True(r.TopY > r.WaistY);
    }
}
