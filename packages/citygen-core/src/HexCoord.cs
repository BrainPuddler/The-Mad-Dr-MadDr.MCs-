using System;
using System.Collections.Generic;

namespace MadDr.CityGen
{
    /// <summary>
    /// A hex cell in axial coordinates (pointy-top layout), the pathing/
    /// positioning index docs/18 "Hex grid, preserved underneath" describes:
    /// the abstract 24x24 grid of docs/02/03/04 realized at city-battlefield
    /// scale, up to 250x250 hexes on a 5 km Big City map (docs/18 SS1).
    ///
    /// Pure value type, pure functions -- no engine dependency, matching the
    /// genome-core convention (docs/06/07) for anything that must be
    /// deterministic and independently testable off the render layer.
    ///
    /// Plain struct, not a C# 10 `record struct`, and a braced namespace,
    /// not file-scoped: Unity's asmdef compiler caps at C# 9 regardless of
    /// this package's own .csproj TargetFramework (that only governs the
    /// standalone dotnet build/test) -- equality and the constructor are
    /// hand-written here rather than relying on syntax C# 9 doesn't have.
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        /// <summary>Meters between the centers of two axially-adjacent hexes
        /// (docs/18 SS1: "1 hex = 20 m, chosen so the existing Speed range of
        /// 0.5-2.0 hex/s reads as 10-40 m/s").</summary>
        public const double HexMeters = 20.0;

        public int Q { get; }
        public int R { get; }

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        /// <summary>Axial coordinate of the hex at (col, row) of an odd-r
        /// offset rectangle -- the map-region shape docs/18 SS1 implies
        /// ("a Big City map at 5 km/side is therefore a 250x250 hex
        /// index"): rows stack vertically, odd rows shove right half a
        /// hex, so a WxH offset rectangle reads as a roughly square field
        /// in world space rather than the sheared parallelogram raw axial
        /// ranges produce.</summary>
        public static HexCoord FromOffset(int col, int row)
        {
            return new HexCoord(col - (row - (row & 1)) / 2, row);
        }

        // Pointy-top axial neighbor directions, index = HexEdge (see Facing.cs),
        // in the standard cube/axial convention (Red Blob Games' reference
        // algorithms -- the well-established approach, not invented here).
        private static readonly (int dq, int dr)[] Directions =
        {
            (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1),
        };

        public HexCoord Neighbor(HexEdge edge)
        {
            var (dq, dr) = Directions[(int)edge];
            return new HexCoord(Q + dq, R + dr);
        }

        public IEnumerable<HexCoord> Neighbors()
        {
            for (var e = 0; e < 6; e++) yield return Neighbor((HexEdge)e);
        }

        /// <summary>Hex (Manhattan-on-cube) distance -- exact, no approximation.</summary>
        public int DistanceTo(HexCoord other)
        {
            var dq = Q - other.Q;
            var dr = R - other.R;
            var ds = -dq - dr; // cube coordinate s = -q - r
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;
        }

        /// <summary>Every hex at exactly <paramref name="radius"/> hexes away.
        /// Used for ring-shaped queries; <see cref="Range"/> is the filled disc.</summary>
        public IEnumerable<HexCoord> Ring(int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (radius == 0) { yield return this; yield break; }

            var hex = this;
            for (var i = 0; i < radius; i++) hex = hex.Neighbor(HexEdge.SW);

            for (var side = 0; side < 6; side++)
            {
                for (var step = 0; step < radius; step++)
                {
                    yield return hex;
                    hex = hex.Neighbor((HexEdge)side);
                }
            }
        }

        /// <summary>Every hex within <paramref name="radius"/> hexes, inclusive
        /// (a filled disc) -- e.g. an emitter's 3-hex aura (docs/03), a
        /// Collection Station's 5-hex radius (docs/18/20).</summary>
        public IEnumerable<HexCoord> Range(int radius)
        {
            for (var d = 0; d <= radius; d++)
                foreach (var h in Ring(d)) yield return h;
        }

        /// <summary>World-space (x, z) center of this hex, in meters, at
        /// <see cref="HexMeters"/> center-to-center spacing (docs/18 SS1). Ground
        /// plane only -- height is a rendering/terrain concern, out of scope
        /// for this engine-agnostic core.</summary>
        public (double X, double Z) ToWorld()
        {
            // size = circumradius of one hex, chosen so adjacent centers land
            // exactly HexMeters apart (pointy-top: center spacing = size * sqrt(3)).
            var size = HexMeters / Math.Sqrt(3);
            var x = size * (Math.Sqrt(3) * Q + Math.Sqrt(3) / 2 * R);
            var z = size * (1.5 * R);
            return (x, z);
        }

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object? obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => (Q, R).GetHashCode();
        public override string ToString() => $"({Q}, {R})";
        public static bool operator ==(HexCoord left, HexCoord right) => left.Equals(right);
        public static bool operator !=(HexCoord left, HexCoord right) => !left.Equals(right);
    }

    public enum HexEdge
    {
        E = 0,
        NE = 1,
        NW = 2,
        W = 3,
        SW = 4,
        SE = 5,
    }
}
