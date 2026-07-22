using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A uniform spatial hash over the XZ ground plane -- Phase A of docs/25's
/// monster-movement migration plan (the neighbour-lookup system Layer 1
/// steering and Layer 2 deadlock recovery will both eventually query).
/// This first pass does ONE job: let RuntimeCityBuilder's existing
/// ApplySeparation/AvoidanceDir scans find "everyone near me" without
/// walking the whole combatant list -- same results, less work as the
/// unit count grows. No steering behavior changes in this pass (docs/25
/// Phase A is explicitly parity-only).
///
/// Cell size matches the hex grid's own spatial unit (HexCoord.HexMeters,
/// 20m) -- not chosen for any steering reason, just a natural, already-
/// established "how big is a neighbourhood" scale in this project. Cells
/// are a Dictionary keyed by packed (cellX, cellZ), with per-cell Lists
/// pulled from a pool rather than freshly allocated every Clear() -- after
/// the first frame or two of a match, inserting units into the grid each
/// frame steady-states at zero GC allocation (docs/25's explicit
/// performance constraints).
///
/// Generic (not UnitCombat-specific) so the same grid can back Citizens/
/// TrafficCar neighbour queries later without a second implementation --
/// the class itself doesn't know or care what T is, only where it is.
/// </summary>
public sealed class SpatialGrid<T> where T : class
{
    private const float CellSize = 20f; // matches MadDr.CityGen.HexCoord.HexMeters

    private readonly Dictionary<long, List<T>> _cells = new Dictionary<long, List<T>>();
    private readonly Stack<List<T>> _pool = new Stack<List<T>>();

    private static long Key(int cx, int cz)
    {
        return ((long)cx << 32) | (uint)cz;
    }

    private static void CellOf(Vector3 position, out int cx, out int cz)
    {
        cx = Mathf.FloorToInt(position.x / CellSize);
        cz = Mathf.FloorToInt(position.z / CellSize);
    }

    public void Clear()
    {
        foreach (var cell in _cells)
        {
            cell.Value.Clear();
            _pool.Push(cell.Value);
        }
        _cells.Clear();
    }

    public void Insert(T item, Vector3 position)
    {
        int cx, cz;
        CellOf(position, out cx, out cz);
        var key = Key(cx, cz);
        List<T> list;
        if (!_cells.TryGetValue(key, out list))
        {
            list = _pool.Count > 0 ? _pool.Pop() : new List<T>();
            _cells[key] = list;
        }
        list.Add(item);
    }

    /// <summary>Everyone in the cells the query's bounding SQUARE touches --
    /// not exact-circle filtered. Both existing call sites already do their
    /// own distance check on the candidates, so nothing about their
    /// filtering logic needs to change.</summary>
    public void QueryRadius(Vector3 center, float radius, List<T> results)
    {
        var minCx = Mathf.FloorToInt((center.x - radius) / CellSize);
        var maxCx = Mathf.FloorToInt((center.x + radius) / CellSize);
        var minCz = Mathf.FloorToInt((center.z - radius) / CellSize);
        var maxCz = Mathf.FloorToInt((center.z + radius) / CellSize);
        for (var cx = minCx; cx <= maxCx; cx++)
        {
            for (var cz = minCz; cz <= maxCz; cz++)
            {
                List<T> list;
                if (!_cells.TryGetValue(Key(cx, cz), out list)) continue;
                for (var i = 0; i < list.Count; i++) results.Add(list[i]);
            }
        }
    }
}
