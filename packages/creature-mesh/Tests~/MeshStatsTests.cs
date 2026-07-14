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
}
