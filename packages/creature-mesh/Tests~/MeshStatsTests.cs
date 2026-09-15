using MadDr.CreatureMesh;
using MadDr.RosterClient;
using Xunit;
using Xunit.Abstractions;

namespace MadDr.CreatureMesh.Tests;

public class MeshStatsTests
{
    private readonly ITestOutputHelper _out;
    public MeshStatsTests(ITestOutputHelper output) { _out = output; }

    [Fact]
    public void TriangleCountStaysNearTheLabsMobileBudget()
    {
        // the Lab's LOD0 budget is ~9k tris (TRI_BUDGET, docs/08); the
        // busiest tetrapod build (mastermind + titan + busiest families)
        // should land in the same order of magnitude -- an order-of-
        // magnitude overshoot means a port bug (runaway segment counts)
        var g = new GenomeDto(2, "stats", new string[0],
            new BodyGenesDto("tetrapod", new[] { 0.5, 0.9, 0.9, 0.9 }),
            new BrainGenesDto("mastermind", new[] { 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new HeartGenesDto("titan", new[] { 0.9, 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new SlotsDto(
                new PartAlleleDto("laser_array", new[] { 0.9, 0.9, 0.5, 0.5, 0.9, 0.9 }, null),
                new PartAlleleDto("antenna", new[] { 0.9, 0.9, 0.5, 0.5, 0.9, 0.9 }, null),
                new PartAlleleDto("bug_eyes", new[] { 0.9, 0.9, 0.5, 0.5, 0.9, 0.9 }, null),
                new PartAlleleDto("hoofed_leg", new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }, null)));
        var r = CreatureBuilder.Build(g)!;
        int tris = 0, verts = 0;
        foreach (var c in r.Chunks) { tris += c.Triangles.Count / 3; verts += c.VertexCount; }
        _out.WriteLine($"chunks={r.Chunks.Count} verts={verts} tris={tris} topY={r.TopY:F2} waistY={r.WaistY:F2} legLen={r.Leg!.Len:F2}");
        Assert.InRange(tris, 2000, 40000);
        Assert.InRange(r.Chunks.Count, 5, 64);
    }

    // docs/39 SS4.1's busiest-build genome (mastermind + titan + busiest
    // families) -- the same worst case TriangleCountStaysNearTheLabsMobileBudget
    // uses, reused here so the LOD dial is proven against the worst
    // realistic build, not a cheap average one.
    private static GenomeDto BusiestTetrapod()
    {
        return new GenomeDto(2, "stats-lod", new string[0],
            new BodyGenesDto("tetrapod", new[] { 0.5, 0.9, 0.9, 0.9 }),
            new BrainGenesDto("mastermind", new[] { 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new HeartGenesDto("titan", new[] { 0.9, 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new SlotsDto(
                new PartAlleleDto("laser_array", new[] { 0.9, 0.9, 0.5, 0.5, 0.9, 0.9 }, null),
                new PartAlleleDto("antenna", new[] { 0.9, 0.9, 0.5, 0.5, 0.9, 0.9 }, null),
                new PartAlleleDto("bug_eyes", new[] { 0.9, 0.9, 0.5, 0.5, 0.9, 0.9 }, null),
                new PartAlleleDto("hoofed_leg", new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }, null)));
    }

    private static int TrisOf(CreatureMeshResult r)
    {
        var tris = 0;
        foreach (var c in r.Chunks) tris += c.Triangles.Count / 3;
        return tris;
    }

    [Fact]
    public void DetailDialReducesTriangleCountMonotonically()
    {
        // docs/39 SS11 item 1: LOD0/1/2 at Detail = 1 / 0.55 / 0.3. Real
        // measurement (2026-09-14) on the busiest genome came in well
        // above the doc's own hand-estimated "near 3k / near 800" (that
        // estimate only modeled Ellipsoid+Tube scaling; Lathe -- the
        // torso, the single biggest contributor -- was never gated by the
        // JS at all, and many hardware-detail primitives already sit at
        // or near their floor so have little room left to shrink):
        // LOD0=16001, LOD1(0.55)=5652, LOD2(0.3)=3814 for this genome.
        // See docs/39 SS11 item 1's status note and docs/12 for the full
        // writeup -- closing the remaining gap to the SS4.2 hero ceiling
        // (LOD1 <= 3,500, LOD2 <= 900) needs more than this dial (a
        // steeper curve and/or per-primitive simplification), tracked as
        // a follow-up, not asserted here as if already achieved. This
        // test asserts the dial's real, measured behavior instead:
        // monotonic and a genuine cut at each step.
        var g = BusiestTetrapod();
        var t0 = TrisOf(CreatureBuilder.Build(g, 1));
        var t1 = TrisOf(CreatureBuilder.Build(g, 0.55));
        var t2 = TrisOf(CreatureBuilder.Build(g, 0.3));
        _out.WriteLine($"LOD0={t0} LOD1={t1} LOD2={t2}");
        Assert.True(t1 < t0 * 0.5, $"LOD1 ({t1}) should be well under half of LOD0 ({t0})");
        Assert.True(t2 < t1 * 0.8, $"LOD2 ({t2}) should cut further below LOD1 ({t1})");
        Assert.True(t2 > 0);
    }

    [Fact]
    public void DefaultDetailIsAStrictNoOp()
    {
        // Detail=1 must reproduce the pre-LOD-dial mesh exactly -- every
        // seg/sides/nMaj/nMin/lathe-seg value already in use across the
        // codebase sits at or above its primitive's SegFor floor, so
        // round(n*1) == n always and nothing should change for any
        // existing caller that never passes `detail`.
        var g = BusiestTetrapod();
        var implicitDetail = CreatureBuilder.Build(g);
        var explicitDetail = CreatureBuilder.Build(g, 1);
        Assert.Equal(TrisOf(implicitDetail), TrisOf(explicitDetail));
    }

    [Fact]
    public void DetailDialNeverLeaksIntoTheNextBuild()
    {
        // Mirrors the JS's own "_detail = 1; // never leak a reduced dial
        // into anything built outside this pass" contract.
        var g = BusiestTetrapod();
        var reduced = TrisOf(CreatureBuilder.Build(g, 0.3));
        Assert.Equal(1, Prims.Detail);
        var full = TrisOf(CreatureBuilder.Build(g));
        Assert.True(full > reduced, $"a plain Build() after a reduced-detail Build() should be back at full detail ({full} vs {reduced})");
    }

    [Theory]
    [InlineData("tetrapod")]
    [InlineData("blob")]
    [InlineData("serpentine")]
    [InlineData("winged")]
    [InlineData("crab")]
    [InlineData("arachnid")]
    [InlineData("avian")]
    [InlineData("treant")]
    [InlineData("floater")]
    public void EveryPlanStaysNearTheLabsBudget(string plan)
    {
        var g = new GenomeDto(2, "stats-" + plan, new string[0],
            new BodyGenesDto(plan, new[] { 0.5, 0.5, 0.5, 0.5 }),
            new BrainGenesDto("average", new[] { 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new HeartGenesDto("steady", new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }),
            new SlotsDto(
                new PartAlleleDto("claw_hand", new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }, null),
                new PartAlleleDto("antenna", new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }, null),
                new PartAlleleDto("bug_eyes", new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }, null),
                new PartAlleleDto("hoofed_leg", new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 }, null)));
        var r = CreatureBuilder.Build(g);
        var tris = 0;
        foreach (var c in r.Chunks) tris += c.Triangles.Count / 3;
        _out.WriteLine($"{plan}: chunks={r.Chunks.Count} tris={tris} topY={r.TopY:F2} leg={(r.Leg == null ? "none" : r.Leg.Len.ToString("F2"))}");
        Assert.InRange(tris, 1000, 40000);
    }
}
