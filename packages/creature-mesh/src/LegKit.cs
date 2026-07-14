using System;
using System.Collections.Generic;

namespace MadDr.CreatureMesh
{
    /// <summary>Dressed geometry for one rig leg, split at the joints the
    /// gait rig animates. The Lab bakes whole legs into its static mesh;
    /// Unity's no-skate rig instead positions three transforms per leg
    /// (hip-anchored hardware, upper segment, lower segment, foot), so
    /// the kit authors each piece in its own local space:
    ///
    ///  - Hip: at the hip point, leg axis pointing down (-Y). Static.
    ///  - Upper/Lower: a tapered tube spanning y in [-1, +1] (two units
    ///    tall, matching Unity's built-in cylinder convention) with the
    ///    family's radii baked in lab units; the rig scales Y to the live
    ///    hip-knee / knee-foot distance each frame.
    ///  - Foot: origin at the ground-contact point, +z forward. `side`
    ///    mirrors claw splay properly (a negative GameObject scale would
    ///    flip winding, so mirroring happens here in the data).</summary>
    public sealed class LegKitResult
    {
        public IReadOnlyList<MeshChunk> Hip = new List<MeshChunk>();
        public IReadOnlyList<MeshChunk> Upper = new List<MeshChunk>();
        public IReadOnlyList<MeshChunk> Lower = new List<MeshChunk>();
        public IReadOnlyList<MeshChunk> Foot = new List<MeshChunk>();
    }

    /// <summary>Leg-family geometry for the gait rig -- the fix for
    /// "the legs are just sticks". Radii/colors come from the same JS
    /// leg builders the Lab uses (site/creature-renderer.js buildPart's
    /// leg families), reshaped from baked poses into rig segments.</summary>
    public static class LegKit
    {
        private static readonly Vec3 Top = new Vec3(0, 1, 0);
        private static readonly Vec3 Bottom = new Vec3(0, -1, 0);

        private static double P(double[]? arr, int i, double d)
        {
            return arr != null && i < arr.Length ? arr[i] : d;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return Math.Min(Math.Max(v, lo), hi);
        }

        public static LegKitResult Build(string family, double[] pg, Col skin, double side)
        {
            var girth = P(pg, 1, 0.5);
            var count = P(pg, 4, 0.5);
            switch (family)
            {
                case "talon_leg": return Talon(girth, count, skin, side);
                case "insect_leg": return Insect(girth, skin, side);
                case "piston_leg": return Piston(side);
                case "jet_leg": return Jet(girth);
                case "tendril_leg": return Tendril(girth, skin);
                case "leg_stump": return Stump();
                default: return Hoofed(girth, skin);   // hoofed_leg + anything unknown
            }
        }

        /// <summary>One rig segment: proximal (hip/knee-side) radius at
        /// local y=-1, distal at y=+1 -- the rig's FromToRotation maps
        /// local -Y onto the segment's anchor end, +Y onto its far end.</summary>
        private static void Segment(Builder mb, double rProximal, double rDistal, Col col, double gloss, double emis = 0)
        {
            Prims.Tube(mb, new[] { Bottom, Top }, new[] { rProximal, rDistal }, col, gloss, emis, 9, 3);
        }

        private static LegKitResult Collect(Builder hip, Builder upper, Builder lower, Builder foot)
        {
            hip.FixWinding(); upper.FixWinding(); lower.FixWinding(); foot.FixWinding();
            return new LegKitResult
            {
                Hip = hip.Chunks, Upper = upper.Chunks, Lower = lower.Chunks, Foot = foot.Chunks,
            };
        }

        /// <summary>Biped flesh leg with a hoofed foot -- thigh brace,
        /// hip-joint mass, tapered thigh/calf, hoof stepping forward.</summary>
        private static LegKitResult Hoofed(double girth, Col skin)
        {
            var r = 0.42 + 0.3 * girth;
            var hip = new Builder();
            Prims.LimbJoint(hip, new Vec3(0, 0, 0), new Vec3(0.25, -1, 0.15), r * 1.1);
            Prims.Ellipsoid(hip, new Vec3(0, 0.15, 0), new Vec3(r * 1.35, r * 1.25, r * 1.3), skin, 0.28, 0, 8);

            var upper = new Builder();
            Segment(upper, r * 1.18, r * 0.9, skin, 0.28);
            var lower = new Builder();
            Segment(lower, r * 0.9, r * 0.68, skin, 0.28);

            var foot = new Builder();
            Prims.Ellipsoid(foot, new Vec3(0, 0.4, 0.35), new Vec3(r * 0.8, 0.34, r * 1.2), skin, 0.28, 0, 8);
            Prims.Tube(foot, new[] { new Vec3(0, 0.5, 0.8), new Vec3(0, 0.0, 0.9) },
                new[] { r * 0.62, r * 0.75 }, Palette.HOOF, 0.5, 0, 9, 2);
            return Collect(hip, upper, lower, foot);
        }

        /// <summary>Bird-of-prey leg: skinny shin over a fan of ground
        /// claws; `count` sets 2-4 talons, splayed outward per side.</summary>
        private static LegKitResult Talon(double girth, double count, Col skin, double side)
        {
            var r = 0.24 + 0.12 * girth;
            var hip = new Builder();
            Prims.LimbJoint(hip, new Vec3(0, 0, 0), new Vec3(side * 0.15, -1, -0.55), r * 1.2);
            Prims.Ellipsoid(hip, new Vec3(0, 0.05, 0), new Vec3(r * 1.7, r * 1.55, r * 1.65), skin, 0.28, 0, 8);

            var upper = new Builder();
            Segment(upper, r * 1.3, r, skin, 0.3);
            var lower = new Builder();
            Segment(lower, r, r * 0.9, skin, 0.3);

            var foot = new Builder();
            var nt = (int)Clamp(2 + Math.Round(count * 2), 2, 4);
            for (var i = 0; i < nt; i++)
            {
                var a = ((double)i / Math.Max(nt - 1, 1) - 0.5) * 1.6;
                // toe splay mirrors with the foot -- `side` fans the claws
                // outward on both feet instead of the same absolute way
                Prims.CurvedCone(foot, new Vec3(0, 0.18, 0),
                    new Vec3(side * Math.Sin(a) * 0.7, -0.18, Math.Cos(a) * 0.8),
                    0.8, 0.14, new Vec3(0, -0.12, 0), Palette.CLAW, 0.5);
            }
            return Collect(hip, upper, lower, foot);
        }

        /// <summary>Chitin strut tapering to a needle point -- no foot
        /// mass at all, the point IS the foot.</summary>
        private static LegKitResult Insect(double girth, Col skin, double side)
        {
            var r = 0.2 + 0.1 * girth;
            var chit = Col.Lp(Palette.CHITIN, skin, 0.35);
            var hip = new Builder();
            Prims.LimbJoint(hip, new Vec3(0, 0, 0), new Vec3(side * 0.7, -0.7, 0), r * 1.15);

            var upper = new Builder();
            Segment(upper, r * 1.2, r, chit, 0.4);
            var lower = new Builder();
            Segment(lower, r * 0.85, 0.04, chit, 0.4);
            return Collect(hip, upper, lower, new Builder());
        }

        /// <summary>Machined spider strut (the piston family's walking
        /// mode; the tank-tread variant stays a future pass -- treads
        /// have no feet for a stepping rig to honor).</summary>
        private static LegKitResult Piston(double side)
        {
            var hip = new Builder();
            Prims.LimbJoint(hip, new Vec3(0, 0, 0), new Vec3(side * 0.6, -0.8, 0), 0.34);

            var upper = new Builder();
            Segment(upper, 0.34, 0.26, Palette.METAL, 0.8);
            var lower = new Builder();
            Segment(lower, 0.2, 0.05, Palette.METAL, 0.8);
            return Collect(hip, upper, lower, new Builder());
        }

        /// <summary>Strut ending in a gimbaled thruster. In the Lab a jet
        /// leg never touches down; the battlefield rig still steps it (the
        /// game needs ground locomotion), reading as hop-thrusts.</summary>
        private static LegKitResult Jet(double girth)
        {
            var r = 0.18 + 0.1 * girth;
            var hip = new Builder();
            Prims.LimbJoint(hip, new Vec3(0, 0, 0), new Vec3(0, -1, 0), r * 1.15);
            Prims.Ellipsoid(hip, new Vec3(0, 0.1, 0), new Vec3(r * 1.2, r * 1.1, r * 1.2), Palette.METAL, 0.75, 0, 7);

            var upper = new Builder();
            Segment(upper, r * 0.9, r * 1.1, Palette.METDK, 0.7);
            var lower = new Builder();
            Segment(lower, r * 1.1, r * 1.3, Palette.METDK, 0.7);

            var foot = new Builder();
            Prims.Ellipsoid(foot, new Vec3(0, 0.35, 0), new Vec3(r * 1.5, r * 1.1, r * 1.5), Palette.METAL, 0.75, 0, 8);
            Prims.Tube(foot, new[] { new Vec3(0, 0.35, 0), new Vec3(0, 0.05, 0) },
                new[] { r, r * 0.3 }, Col.Sh(Palette.METDK, 0.7), 0.4, 0.5, 8, 2);
            return Collect(hip, upper, lower, foot);
        }

        /// <summary>Boneless muscular pseudopod, hip-to-ground.</summary>
        private static LegKitResult Tendril(double girth, Col skin)
        {
            var r = 0.22 + 0.12 * girth;
            var hip = new Builder();
            Prims.LimbJoint(hip, new Vec3(0, 0, 0), new Vec3(0, -1, 0), r * 1.15);
            Prims.Ellipsoid(hip, new Vec3(0, 0.15, 0), new Vec3(r * 1.3, r * 1.2, r * 1.3), skin, 0.3, 0, 8);

            var upper = new Builder();
            Segment(upper, r, r * 0.8, skin, 0.32);
            var lower = new Builder();
            Segment(lower, r * 0.75, r * 0.35, skin, 0.32);

            var foot = new Builder();
            Prims.CurvedCone(foot, new Vec3(0, 0.3, 0), new Vec3(0, -0.6, 0.8), 0.5, r * 0.5,
                new Vec3(0, 0, 0.1), Col.Sh(skin, 0.95), 0.32);
            return Collect(hip, upper, lower, foot);
        }

        /// <summary>Healed-over stumps: pallid, ring-stitched where the
        /// old leg came off. "Nothing is wasted."</summary>
        private static LegKitResult Stump()
        {
            var hip = new Builder();
            Prims.Ellipsoid(hip, new Vec3(0, 0, 0), new Vec3(0.5, 0.45, 0.5), Palette.PALLOR, 0.25, 0, 8);

            var upper = new Builder();
            Segment(upper, 0.5, 0.45, Palette.PALLOR, 0.25);
            var lower = new Builder();
            Segment(lower, 0.45, 0.4, Palette.PALLOR, 0.25);

            var foot = new Builder();
            CreatureBuilder.RingStitch(foot, new Vec3(0, 0.15, 0), 0.55);
            return Collect(hip, upper, lower, foot);
        }
    }
}
