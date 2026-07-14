using System;
using System.Collections.Generic;
using MadDr.RosterClient;

namespace MadDr.CreatureMesh
{
    /// <summary>Where the gait rig should mount a leg pair: position +
    /// outward normal in creature space (feet on y=0), mirrored across x.
    /// Legs are the one part family NOT baked into the mesh -- Unity's rig
    /// animates them as transforms so the no-skating contract
    /// (MonsterBody's distance-phased gait) keeps working.</summary>
    public sealed class LegSocketInfo
    {
        public Vec3 P;
        public Vec3 Nrm;
        public double Len;
        public string Family = "";
        public double[] Params = new double[0];
    }

    /// <summary>Everything Unity needs to regenerate a Lab creature from
    /// its DNA: the material-bucketed mesh chunks (torso, head, face,
    /// arms, weapons, eyes -- the works) plus the leg sockets for the gait
    /// rig and framing heights.</summary>
    public sealed class CreatureMeshResult
    {
        public IReadOnlyList<MeshChunk> Chunks = new List<MeshChunk>();
        public LegSocketInfo? Leg;
        public Col Skin;
        public double TopY;
        public double WaistY;
    }

    /// <summary>Port of the Lab's creature assembly
    /// (site/creature-renderer.js, docs/08) -- pass 1: the tetrapod plan
    /// at full fidelity with the Mad-Doctor faction kit. The other eight
    /// plans return null and keep their placeholder bodies until their
    /// own pass. Deliberately dropped from the JS: per-vertex color
    /// gradients (skinColorFn -> flat base color per chunk), texture
    /// tiling, anim/gait/blink vertex channels (Unity animates via
    /// transforms), and mb.glow() light halos.</summary>
    public static class CreatureBuilder
    {
        public static CreatureMeshResult? Build(GenomeDto genome)
        {
            if (genome.Body.Plan != "tetrapod") return null;
            return BuildTetrapod(genome);
        }

        // gene context for one creature -- the slice of the JS `o` object
        // the maddr tetrapod path actually reads
        private sealed class Ctx
        {
            public Col Skin;
            public double Vigor;
            public double Bulk;
            public double Limb;
            public double Tail;
            public int HeartLevel;
            public double LegLen;
            public string? LegFam;
            public string BrainTier = "average";
            public bool Headless;
        }

        private struct Head
        {
            public Vec3 HC;
            public Vec3 HR;
            public double TopY;
        }

        private static double P(double[]? arr, int i, double d)
        {
            return arr != null && i < arr.Length ? arr[i] : d;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return Math.Min(Math.Max(v, lo), hi);
        }

        private static int HeartLevelOf(string tier)
        {
            switch (tier)
            {
                case "faint": return 0;
                case "steady": return 1;
                case "strong": return 2;
                case "titan": return 3;
                default: return -1; // matches JS indexOf on an unknown tier
            }
        }

        public static CreatureMeshResult BuildTetrapod(GenomeDto g)
        {
            var mb = new Builder();

            var vigor = P(g.Heart.Params, 0, 0.5);
            var hue = P(g.Body.Params, 0, 0.5);
            var o = new Ctx
            {
                Vigor = vigor,
                Skin = Palette.MadDrSkin(hue, vigor),
                Bulk = P(g.Body.Params, 1, 0.5),
                Limb = P(g.Body.Params, 2, 0.5),
                Tail = P(g.Body.Params, 3, 0.5),
                HeartLevel = HeartLevelOf(g.Heart.Tier),
                BrainTier = g.Brain.Tier,
                Headless = g.Slots.Sensor.Family == "sensor_stub" && g.Slots.Eye.Family == "eye_socket",
            };

            // leg genes set stance height (stumps slump low)
            var legAl = g.Slots.Leg;
            o.LegFam = legAl.Family;
            var legLenGene = P(legAl.Params, 0, 0.5);
            switch (legAl.Family)
            {
                case "leg_stump": o.LegLen = 0.6; break;
                case "insect_leg": o.LegLen = Clamp(1.25 + 1.0 * legLenGene, 1.25, 2.25); break;
                case "piston_leg": o.LegLen = Clamp(1.8 + 1.0 * legLenGene, 1.8, 2.8); break;
                case "jet_leg": o.LegLen = Clamp(1.6 + 1.1 * legLenGene, 1.6, 2.7); break;
                case "tendril_leg": o.LegLen = Clamp(1.1 + 0.9 * legLenGene, 1.1, 2.0); break;
                default: o.LegLen = Clamp(2.4 + 1.2 * legLenGene, 2.4, 3.6); break;
            }

            // ---- planTetrapod, minus the leg slot (gait rig owns legs) ----
            var b = o.Limb;                       // the limb axis IS the build axis here
            var W = 1.9 + 1.0 * o.Bulk;           // human-ish width, not beach-ball
            var h = 3.1 + 0.7 * o.Bulk;
            var waistY = o.LegLen + 1.15;         // lower torso lives below the belt
            var y0 = waistY - 0.15;
            var levels = TorsoLevels(b, W, h, y0, 0.5);

            Prims.Lathe(mb, levels, o.Skin, 0.28, 0, 18);
            var shl = levels[3];
            if (b > 0.5)                          // brute deltoid caps
                foreach (var s in Sides)
                    Prims.Ellipsoid(mb, new Vec3(s * shl.Rx * 0.85, shl.Y + 0.15, shl.Z),
                        new Vec3(W * 0.48 * b, W * 0.42 * b, W * 0.44 * b), o.Skin, 0.28, 0, 10);
            var ch = levels[2];
            StitchSeam(mb, ch.Y - h * 0.12, ch.Rx, ch.Rz, ch.Z);
            BuildPelvis(mb, o, levels[0].Rx, waistY);

            // an actual neck between the shoulders and the skull (maddr:
            // fleshy column)
            var neckTop = y0 + h + 0.55;
            var neckR = W * 0.32;
            Prims.Tube(mb,
                new[] { new Vec3(0, y0 + h - 0.3, levels[4].Z * 0.7), new Vec3(0, neckTop, levels[4].Z * 0.8) },
                new[] { neckR, neckR * 0.86 }, Col.Sh(o.Skin, 0.95), 0.28, 0, 10);

            var head = BuildHead(mb, o, neckTop - 0.2, levels[4].Z);
            if (!o.Headless) FrankenDetails(mb, head.HC, head.HR, o.HeartLevel, o);

            AddTail(mb, o, o.LegLen + 0.55, -levels[0].Rx * 0.75, false);

            // socket frames: position + the body's outward surface normal there
            var slope = (levels[2].Rx - levels[4].Rx) / Math.Max(0.4, levels[4].Y - levels[2].Y);
            var sensP = new Vec3(head.HR.X * 0.52, head.TopY, head.HC.Z - 0.1);
            var eyeP = new Vec3(0, head.HC.Y + head.HR.Y * 0.20, head.HC.Z + head.HR.Z * 0.62);

            var handSock = new Sock
            {
                P = new Vec3(shl.Rx * 0.92 + (b > 0.5 ? W * 0.28 * b : 0), shl.Y, shl.Z + 0.15),
                Nrm = new Vec3(1, slope * 0.5, 0.15).Norm(),
                Mirror = true,
            };
            var sensSock = new Sock
            {
                P = sensP,
                Nrm = Prims.EllipN(sensP, head.HC, head.HR),
                Mirror = true,
            };
            var eyeSock = new Sock
            {
                P = eyeP,
                Nrm = Prims.EllipN(eyeP, head.HC, head.HR),
                Mirror = false,
                FaceR = head.HR.X,
            };

            BuildSlot(mb, "hand", g.Slots.Hand, handSock, o);
            if (!o.Headless)
            {
                BuildSlot(mb, "sensor", g.Slots.Sensor, sensSock, o);
                BuildSlot(mb, "eye", g.Slots.Eye, eyeSock, o);
            }

            mb.FixWinding();

            return new CreatureMeshResult
            {
                Chunks = mb.Chunks,
                Skin = o.Skin,
                TopY = o.Headless ? neckTop : head.TopY,
                WaistY = waistY,
                Leg = new LegSocketInfo
                {
                    P = new Vec3(Math.Max(0.7, levels[0].Rx * 0.58), o.LegLen, 0),
                    Nrm = new Vec3(0.3, -1, 0).Norm(),
                    Len = o.LegLen,
                    Family = legAl.Family,
                    Params = legAl.Params,
                },
            };
        }

        private static readonly double[] Sides = { 1, -1 };

        // ---- torso ---------------------------------------------------------

        /// <summary>Torso profiles: build 0 = pear (bottom-heavy egg),
        /// 1 = gorilla (triangular -- huge chest and shoulders over narrow
        /// hips, hunched forward). Everything between breeds smoothly.</summary>
        private static readonly double[] ProfilePear = { 1.02, 1.22, 0.90, 0.62, 0.38 };
        private static readonly double[] ProfileGor = { 0.60, 0.80, 1.24, 1.40, 0.58 };
        private static readonly double[] ProfileT = { 0, 0.30, 0.60, 0.86, 1 };

        private static Prims.LatheLevel[] TorsoLevels(double build, double w, double h, double y0, double lean)
        {
            var lv = new Prims.LatheLevel[5];
            for (var i = 0; i < 5; i++)
            {
                var t = ProfileT[i];
                var rx = w * (ProfilePear[i] + (ProfileGor[i] - ProfilePear[i]) * build);
                lv[i] = new Prims.LatheLevel(
                    y0 + t * h,
                    0,
                    t > 0.45 ? lean * build * (t - 0.45) / 0.55 : 0,
                    rx,
                    rx * (i == 2 ? 0.82 + 0.30 * build : 0.80));   // deep gorilla chest
            }
            return lv;
        }

        private static void StitchSeam(Builder mb, double y0, double rx, double rz, double zc)
        {
            // zigzag suture across the chest front
            var pts = new List<Vec3>();
            for (var i = 0; i <= 8; i++)
            {
                var t = (double)i / 8;
                var x = (t - 0.5) * rx * 1.4;
                var y = y0 + (i % 2 != 0 ? 0.32 : -0.32);
                var q = 1 - (x / rx) * (x / rx);
                if (q <= 0.05) continue;
                pts.Add(new Vec3(x, y, zc + rz * Math.Sqrt(q) + 0.04));
            }
            if (pts.Count > 2)
            {
                var radii = new double[pts.Count];
                for (var i = 0; i < radii.Length; i++) radii[i] = 0.09;
                Prims.Tube(mb, pts, radii, Palette.STITCH, 0.1, 0, 5, 0);
            }
        }

        /// <summary>The modular LOWER TORSO. Material follows the leg
        /// family -- flesh pelvis for organic legs, chitin pod for insect,
        /// machined metal chassis for tech legs. The waist junction is a
        /// brass collar worn as a bolted belt.</summary>
        private static void BuildPelvis(Builder mb, Ctx o, double waistR, double waistY)
        {
            var mech = o.LegFam == "piston_leg";
            var chit = o.LegFam == "insect_leg";
            var col = mech ? Palette.METAL : chit ? Col.Lp(Palette.CHITIN, o.Skin, 0.35) : o.Skin;
            var hipY = waistY - 1.15;
            var midR = waistR * 0.42;
            var midRz = waistR * 0.35;
            Prims.Lathe(mb, new[]
            {
                new Prims.LatheLevel(hipY - 0.45, 0, 0, waistR * 0.30, waistR * 0.25),
                new Prims.LatheLevel(hipY + 0.30, 0, 0, midR, midRz),
                new Prims.LatheLevel(waistY + 0.12, 0, 0, waistR * 0.85, waistR * 0.71),
            }, col, mech ? 0.7 : 0.28, 0, 14);
            if (mech)
            {
                for (var i = 0; i < 8; i++)
                {          // chassis rivets
                    var a = (double)i / 8 * Math.PI * 2;
                    Prims.Ellipsoid(mb, new Vec3(Math.Cos(a) * midR, hipY + 0.3, Math.Sin(a) * midRz),
                        new Vec3(0.09, 0.09, 0.09), Palette.IRON, 0.8, 0, 4);
                }
                Prims.Ellipsoid(mb, new Vec3(0, hipY + 0.25, midRz), new Vec3(0.16, 0.16, 0.1), Palette.GLOW, 0.5, 1, 6);
            }
            // THE BELT: brass collar around the waist junction, iron-bolted
            Prims.Torus(mb, new Vec3(0, waistY + 0.05, 0), new Vec3(0, 1, 0), waistR * 0.98, 0.24,
                Palette.BRASS, 0.85, 0, 18, 8);
            for (var i = 0; i < 8; i++)
            {
                var a = (double)i / 8 * Math.PI * 2 + 0.39;
                Prims.Ellipsoid(mb,
                    new Vec3(Math.Cos(a) * (waistR * 0.98 + 0.16), waistY + 0.05, Math.Sin(a) * (waistR * 0.98 + 0.16)),
                    new Vec3(0.13, 0.13, 0.13), Palette.IRON, 0.8, 0, 4);
            }
        }

        // ---- head ----------------------------------------------------------

        /// <summary>The head ladder: dim = pinhead sunk in the shoulders,
        /// average = standard, gifted = tall egghead dome, mastermind =
        /// exposed pulsing brain under a riveted glass dome.</summary>
        private static Head BuildHead(Builder mb, Ctx o, double neckY, double zOff)
        {
            if (o.Headless)
            {
                // no skull at all -- the neck just ends
                return new Head { HC = new Vec3(0, neckY, zOff), HR = new Vec3(0.01, 0.01, 0.01), TopY = neckY };
            }
            var t = o.BrainTier;
            Vec3 hR;
            var sunk = 0.72;
            if (t == "dim") { hR = new Vec3(1.05, 0.98, 1.10); sunk = 0.52; }
            else if (t == "gifted") { hR = new Vec3(1.28, 1.75, 1.30); sunk = 0.78; }
            else if (t == "mastermind") { hR = new Vec3(1.45, 1.38, 1.45); }
            else { hR = new Vec3(1.32, 1.26, 1.35); }
            var hC = new Vec3(0, neckY + hR.Y * sunk, zOff + 0.15);
            Prims.Ellipsoid(mb, hC, hR, o.Skin, 0.3, 0, 16);
            var topY = hC.Y + hR.Y;
            if (t == "gifted")
            {
                // egghead crown
                Prims.Ellipsoid(mb, new Vec3(hC.X, hC.Y + hR.Y * 0.52, hC.Z - 0.1),
                    new Vec3(hR.X * 0.72, hR.Y * 0.52, hR.Z * 0.70), o.Skin, 0.3, 0, 12);
                topY = hC.Y + hR.Y * 1.04;
            }
            else if (t == "mastermind")
            {
                // the brain, sealed under glass
                var bc = new Vec3(hC.X, hC.Y + hR.Y * 0.62, hC.Z - 0.15);
                Prims.Ellipsoid(mb, bc, new Vec3(hR.X * 0.92, hR.Y * 0.66, hR.Z * 0.88), Palette.BRAINC, 0.55, 0, 14);
                foreach (var s in Sides)
                    Prims.Ellipsoid(mb, new Vec3(bc.X + s * hR.X * 0.40, bc.Y + hR.Y * 0.34, bc.Z),
                        new Vec3(hR.X * 0.44, hR.Y * 0.30, hR.Z * 0.55), Col.Sh(Palette.BRAINC, 0.92), 0.55, 0, 8);
                BuildGlassDome(mb, new Vec3(bc.X, bc.Y + hR.Y * 0.1, bc.Z),
                    new Vec3(hR.X * 0.95, hR.Y * 0.72, hR.Z * 0.92));
                topY = bc.Y + hR.Y * 1.05;
            }
            return new Head { HC = hC, HR = hR, TopY = topY };
        }

        /// <summary>A riveted glass dome sealed over an exposed brain --
        /// pale, glossy, translucent. Brass collar + three opaque support
        /// ribs keep it reading as a structure even before the
        /// transparency registers.</summary>
        private static void BuildGlassDome(Builder mb, Vec3 center, Vec3 radii)
        {
            var r = new Vec3(radii.X * 1.3, radii.Y * 1.3, radii.Z * 1.3);
            mb.SetAlpha(0.24);
            Prims.Ellipsoid(mb, center, r, Palette.GLASS, 0.92, 0, 14);
            mb.SetAlpha(1);

            var collarY = center.Y - r.Y * 0.32;
            var collarR = (r.X + r.Z) * 0.5 * 0.94;
            Prims.Torus(mb, new Vec3(center.X, collarY, center.Z), new Vec3(0, 1, 0), collarR, 0.12,
                Palette.BRASS, 0.85, 0, 16, 6);
            for (var i = 0; i < 6; i++)
            {
                var a = (double)i / 6 * Math.PI * 2;
                Prims.Ellipsoid(mb,
                    new Vec3(center.X + Math.Cos(a) * collarR, collarY, center.Z + Math.Sin(a) * collarR),
                    new Vec3(0.09, 0.09, 0.09), Palette.IRON, 0.8, 0, 4);
            }
            // three thin ribs arching from the collar to the crown
            for (var i = 0; i < 3; i++)
            {
                var a = (double)i / 3 * Math.PI * 2 + 0.4;
                var basePt = new Vec3(center.X + Math.Cos(a) * collarR, collarY, center.Z + Math.Sin(a) * collarR);
                var top = new Vec3(center.X, center.Y + r.Y * 0.98, center.Z);
                var mid = new Vec3((basePt.X + top.X) * 0.5 + Math.Cos(a) * 0.3, (basePt.Y + top.Y) * 0.5,
                    (basePt.Z + top.Z) * 0.5 + Math.Sin(a) * 0.3);
                Prims.Tube(mb, new[] { basePt, mid, top }, new[] { 0.05, 0.045, 0.03 },
                    Col.Sh(Palette.BRASS, 0.85), 0.7, 0, 5, 1);
            }
        }

        /// <summary>The b-movie face: heavy brow ridge, protruding jaw with
        /// underbite tusks, and heart-tier neck bolts (titan bolts glow).</summary>
        private static void FrankenDetails(Builder mb, Vec3 headC, Vec3 headR, int heartLevel, Ctx o)
        {
            var by = headC.Y + headR.Y * 0.40;
            var bz = headC.Z + headR.Z * 0.70;
            var bw = 0.14 + headR.X * 0.07;
            Prims.Tube(mb, new[]
            {
                new Vec3(headC.X - headR.X * 0.85, by - 0.12, bz - headR.Z * 0.22),
                new Vec3(headC.X, by + 0.12, bz + headR.Z * 0.18),
                new Vec3(headC.X + headR.X * 0.85, by - 0.12, bz - headR.Z * 0.22),
            }, new[] { bw, bw * 1.45, bw }, Col.Sh(o.Skin, 0.78), 0.25, 0, 8);

            // protruding lower jaw: mouth is a shadow line between jaw and
            // skull, underbite tusks rise from the jaw's corners
            var jC = new Vec3(headC.X, headC.Y - headR.Y * 0.68, headC.Z + headR.Z * 0.30);
            var jR = new Vec3(headR.X * 0.80, headR.Y * 0.40, headR.Z * 0.82);
            Prims.Ellipsoid(mb, jC, jR, o.Skin, 0.28, 0, 10);
            Prims.Ellipsoid(mb, new Vec3(jC.X, jC.Y + jR.Y * 0.55, jC.Z + jR.Z * 0.45),
                new Vec3(jR.X * 0.82, 0.13, jR.Z * 0.5), Palette.MOUTHC, 0.15, 0, 8);
            foreach (var s in Sides)
                Prims.CurvedCone(mb, new Vec3(jC.X + s * jR.X * 0.60, jC.Y + jR.Y * 0.30, jC.Z + jR.Z * 0.66),
                    new Vec3(0, 1, 0.15), 0.5 + headR.X * 0.12, 0.13, new Vec3(0, 0, 0.06), Palette.CLAW, 0.6);

            // neck bolts by heart tier; titan bolts glow
            if (heartLevel >= 1)
            {
                var glow = heartLevel >= 3;
                var rows = heartLevel >= 2 ? new[] { -0.52, -0.78 } : new[] { -0.62 };
                foreach (var fy in rows)
                    foreach (var s in Sides)
                    {
                        var bx = headC.X + s * headR.X * 0.88;
                        var byy = headC.Y + headR.Y * fy;
                        Prims.Tube(mb,
                            new[] { new Vec3(bx, byy, headC.Z), new Vec3(bx + s * 0.8, byy, headC.Z) },
                            new[] { 0.22, 0.28 },
                            glow ? Palette.BLTGLO : Palette.BOLT, 0.7, glow ? 0.85 : 0, 8);
                    }
            }
        }

        /// <summary>Tail from the tail gene (below 0.35 there is none): a
        /// tapered whip out the lower back, curling up.</summary>
        private static void AddTail(Builder mb, Ctx o, double baseY, double baseZ, bool spade)
        {
            if (o.Tail < 0.35) return;
            var k = (o.Tail - 0.35) / 0.65;
            var len = 2.4 + 3.4 * k;
            var path = new List<Vec3>();
            for (var i = 0; i <= 8; i++)
            {
                var t = (double)i / 8;
                path.Add(new Vec3(
                    Math.Sin(t * 2.6) * 0.5 * k,
                    baseY - Math.Sin(t * Math.PI) * 0.6 + t * t * (1.6 + 2.2 * k),
                    baseZ - t * len));
            }
            var r0 = 0.5 + 0.25 * o.Bulk;
            var radii = new double[9];
            for (var i = 0; i < 9; i++) radii[i] = r0 * (1 - (double)i / 8 * 0.85);
            Prims.Tube(mb, path, radii, o.Skin, 0.3, 0, 8, 3);
            if (spade)
                Prims.CurvedCone(mb, path[8], new Vec3(0, 0.4, -1), 0.9, 0.3, new Vec3(0, 0.2, 0),
                    Col.Sh(o.Skin, 0.7), 0.4);
        }

        // ---- parts ---------------------------------------------------------

        private sealed class Sock
        {
            public Vec3 P;
            public Vec3 Nrm;
            public bool Mirror;
            public double FaceR;
        }

        private static void BuildSlot(Builder mb, string slot, PartAlleleDto al, Sock sock, Ctx o)
        {
            // dormant organic head sensors: low ornament gene -> bald head
            if (slot == "sensor" && (al.Family == "antenna" || al.Family == "horn") &&
                P(al.Params, 5, 0.5) < 0.35) return;

            // a grafted part remembers its OWN hue (surgery.ts, docs/06)
            // instead of blending into this body's skin
            var partSkin = al.Hue.HasValue ? Palette.MadDrSkin(al.Hue.Value, o.Vigor) : o.Skin;

            if (sock.Mirror)
            {
                BuildPart(mb, al.Family, al.Params, 1, sock, partSkin);
                BuildPart(mb, al.Family, al.Params, -1, sock, partSkin);
            }
            else
            {
                BuildPart(mb, al.Family, al.Params, 1, sock, partSkin);
            }
        }

        private static void BuildPart(Builder mb, string family, double[] pg, double side, Sock sock, Col skin)
        {
            var len = P(pg, 0, 0.5);
            var girth = P(pg, 1, 0.5);
            var taper = P(pg, 2, 0.5);
            var curl = P(pg, 3, 0.5);
            var count = P(pg, 4, 0.5);
            var orn = P(pg, 5, 0.5);
            var s = new Vec3(side * sock.P.X, sock.P.Y, sock.P.Z);
            const double scale = 1;   // tetrapod sockets are never `tiny`
            // the rig: parts leave the body along the surface normal at the
            // socket, so nothing buries into a chest on extreme morphs
            var n = new Vec3(side * sock.Nrm.X, sock.Nrm.Y, sock.Nrm.Z).Norm();

            switch (family)
            {
                // ---- hands ----
                case "claw_hand":
                {
                    var armR = (0.42 + 0.4 * girth) * scale;
                    var wrist = ArmDrop(mb, s, side, armR, scale, skin, pg, n);
                    Prims.Ellipsoid(mb, wrist, new Vec3(armR * 1.35, armR * 1.15, armR * 1.35), skin, 0.3, 0, 8);
                    var nClaw = (int)Clamp(2 + Math.Round(count * 3), 2, 5);
                    for (var i = 0; i < nClaw; i++)
                    {
                        var a = ((double)i / Math.Max(nClaw - 1, 1) - 0.5) * 1.5;
                        Prims.CurvedCone(mb, wrist,
                            new Vec3(Math.Sin(a) * 0.5, -0.85, 0.45 + Math.Cos(a) * 0.2),
                            (0.7 + 1.0 * len) * scale, (0.24 + 0.16 * girth) * scale,
                            new Vec3(0, -(0.45 + curl * 0.7), 0.3), Palette.CLAW, 0.55);
                    }
                    break;
                }
                case "pincer":
                {
                    var armR = (0.5 + 0.4 * girth) * scale;
                    var wrist = ArmDrop(mb, s, side, armR, scale, skin, pg, n);
                    var jl = (1.1 + 1.5 * len) * scale;
                    Prims.CurvedCone(mb, wrist, new Vec3(side * 0.15, -0.25, 0.9), jl, armR * 0.75,
                        new Vec3(0, -(0.4 + curl * 0.8), 0.3), Palette.CLAW, 0.5);
                    Prims.CurvedCone(mb, wrist, new Vec3(side * 0.15, -0.9, 0.35), jl * 0.9, armR * 0.65,
                        new Vec3(0, 0.45 + curl * 0.6, 0.45), Palette.CLAW, 0.5);
                    break;
                }
                case "tentacle":
                {
                    var baseR = (0.5 + 0.42 * girth) * scale;
                    var tenL = (2.6 + 2.6 * len) * scale;
                    var path = new List<Vec3>();
                    for (var i = 0; i <= 10; i++)
                    {
                        var t = (double)i / 10;
                        var exit = baseR * 1.6 * Math.Pow(1 - t, 1.5);   // leave along the normal first
                        path.Add(new Vec3(
                            s.X + n.X * exit + side * (0.5 * t + Math.Sin(t * Math.PI * 1.2) * 0.4),
                            s.Y + n.Y * exit - t * tenL + Math.Sin(t * Math.PI) * 0.2,
                            s.Z + n.Z * exit + 0.4 * t + Math.Sin(t * Math.PI * (1 + curl * 1.6)) * curl * 1.1));
                    }
                    Prims.LimbJoint(mb, path[0], path[1] - path[0], baseR);
                    Prims.Ellipsoid(mb, s + n * (baseR * 0.5), new Vec3(baseR * 1.25, baseR * 1.15, baseR * 1.2),
                        skin, 0.3, 0, 8);   // shoulder mass at the root
                    var tr = new double[11];
                    for (var i = 0; i <= 10; i++)
                        tr[i] = baseR * (1 - (double)i / 10 * Clamp(0.35 + 0.6 * taper, 0.35, 0.92));
                    Prims.Tube(mb, path, tr, skin, 0.3, 0, 9, 3);
                    break;
                }
                case "rifle_arm":
                {
                    var wrist = ArmDrop(mb, s, side, 0.42 * scale, scale, skin, pg, n);
                    // rounded receiver, no boxes -- a toy gun, not a brick
                    Prims.Ellipsoid(mb, new Vec3(wrist.X, wrist.Y + 0.05, wrist.Z + 0.3),
                        new Vec3(0.5, 0.42, 1.0), Palette.METAL, 0.7, 0, 10);
                    // barrel with a chunky muzzle brake and a little front sight
                    Prims.Tube(mb,
                        new[] { new Vec3(wrist.X, wrist.Y + 0.12, wrist.Z + 1.0), new Vec3(wrist.X, wrist.Y + 0.12, wrist.Z + 3.05) },
                        new[] { 0.17, 0.15 }, Palette.METDK, 0.85, 0, 10);
                    Prims.Tube(mb,
                        new[] { new Vec3(wrist.X, wrist.Y + 0.12, wrist.Z + 3.0), new Vec3(wrist.X, wrist.Y + 0.12, wrist.Z + 3.4) },
                        new[] { 0.28, 0.28 }, Palette.METDK, 0.85, 0, 10, 2);
                    Prims.Ellipsoid(mb, new Vec3(wrist.X, wrist.Y + 0.4, wrist.Z + 2.55),
                        new Vec3(0.07, 0.15, 0.07), Palette.METDK, 0.6, 0, 6);
                    // grip under the receiver, curved stock with a butt pad behind
                    Prims.Tube(mb,
                        new[] { new Vec3(wrist.X, wrist.Y - 0.3, wrist.Z + 0.5), new Vec3(wrist.X, wrist.Y - 0.88, wrist.Z + 0.32) },
                        new[] { 0.17, 0.14 }, Palette.METDK, 0.5, 0, 8, 2);
                    Prims.Tube(mb, new[]
                    {
                        new Vec3(wrist.X, wrist.Y, wrist.Z - 0.6),
                        new Vec3(wrist.X, wrist.Y - 0.25, wrist.Z - 1.25),
                        new Vec3(wrist.X, wrist.Y - 0.45, wrist.Z - 1.6),
                    }, new[] { 0.3, 0.26, 0.34 }, Palette.METDK, 0.6, 0, 8);
                    Prims.Ellipsoid(mb, new Vec3(wrist.X, wrist.Y - 0.5, wrist.Z - 1.72),
                        new Vec3(0.3, 0.44, 0.16), Col.Sh(Palette.METDK, 0.8), 0.4, 0, 8);
                    Prims.Ellipsoid(mb, new Vec3(wrist.X, wrist.Y + 0.12, wrist.Z + 3.42),
                        new Vec3(0.13, 0.13, 0.13), Palette.GLOW, 0.5, 0.9, 6);
                    break;
                }
                case "plasma_lance":
                {
                    var wrist = ArmDrop(mb, s, side, 0.5 * scale, scale, Palette.CHITIN, pg, n);
                    var lanceL = (1.6 + 1.6 * len) * scale;
                    Prims.Ellipsoid(mb, wrist, new Vec3(0.55, 0.5, 0.55), Palette.CHITIN, 0.4, 0, 8);
                    Prims.Tube(mb,
                        new[] { wrist, new Vec3(wrist.X, wrist.Y + lanceL * 0.9, wrist.Z + 0.5) },
                        new[] { 0.3, 0.05 }, Palette.ICHOR, 0.5, 0.8, 8);
                    Prims.Ellipsoid(mb, new Vec3(wrist.X, wrist.Y + lanceL * 0.92, wrist.Z + 0.52),
                        new Vec3(0.2, 0.2, 0.2), Palette.BLTGLO, 0.5, 1, 6);
                    break;
                }
                case "hand_stump":
                {
                    Prims.Ellipsoid(mb, s, new Vec3(0.62, 0.5, 0.62), Palette.PALLOR, 0.25, 0, 8);
                    RingStitch(mb, new Vec3(s.X, s.Y - 0.1, s.Z), 0.58);
                    break;
                }
                case "chain_blade":
                {
                    // a motor housing at the wrist driving a guide bar with a
                    // looping chain of blade links
                    var wrist = ArmDrop(mb, s, side, 0.4 * scale, scale, skin, pg, n);
                    Prims.Ellipsoid(mb, wrist, new Vec3(0.36, 0.4, 0.5), Palette.METAL, 0.7, 0, 8);
                    var barLen = (1.6 + 1.8 * len) * scale;
                    var barTip = new Vec3(wrist.X, wrist.Y + barLen * 0.15, wrist.Z + barLen);
                    Prims.Tube(mb, new[] { wrist, barTip }, new[] { 0.16, 0.1 }, Palette.METDK, 0.75, 0, 8);
                    var nLinks = (int)Clamp(6 + Math.Round(count * 6), 6, 12);
                    for (var i = 0; i < nLinks; i++)
                    {
                        var t = (double)i / nLinks;
                        Prims.Ellipsoid(mb, new Vec3(
                            wrist.X + (barTip.X - wrist.X) * t,
                            wrist.Y + (barTip.Y - wrist.Y) * t + Math.Sin(t * Math.PI) * 0.22,
                            wrist.Z + (barTip.Z - wrist.Z) * t),
                            new Vec3(0.08, 0.08, 0.08), Col.Sh(Palette.METAL, 1.1), 0.8, 0, 5);
                    }
                    Prims.Ellipsoid(mb, new Vec3(wrist.X, wrist.Y - 0.1, wrist.Z - 0.3),
                        new Vec3(0.1, 0.1, 0.1), Palette.GLOW, 0.5, 0.8, 5);
                    break;
                }
                case "spore_launcher":
                {
                    // a fleshy arm ending in a bulbous pod venting glowing
                    // spore motes -- the biotech counterpart to plasma_lance
                    var wrist = ArmDrop(mb, s, side, 0.46 * scale, scale, Palette.CHITIN, pg, n);
                    var podR = (0.5 + 0.5 * girth) * scale;
                    Prims.Ellipsoid(mb, wrist, new Vec3(podR, podR * 1.1, podR),
                        Col.Lp(Palette.CHITIN, Palette.ICHOR, 0.3), 0.45, 0, 10);
                    var tipC = new Vec3(wrist.X, wrist.Y + podR * 1.3, wrist.Z + podR * 0.6);
                    Prims.Ellipsoid(mb, tipC, new Vec3(podR * 0.6, podR * 0.5, podR * 0.6),
                        Col.Sh(Palette.CHITIN, 1.05), 0.5, 0, 8);
                    var nSpore = (int)Clamp(3 + Math.Round(count * 4), 3, 7);
                    for (var i = 0; i < nSpore; i++)
                    {
                        var a = (double)i / nSpore * Math.PI * 2;
                        var r = podR * 0.5;
                        var mp = new Vec3(tipC.X + Math.Cos(a) * r,
                            tipC.Y + podR * 0.4 + Math.Sin(a * 1.7) * 0.15,
                            tipC.Z + Math.Sin(a) * r);
                        Prims.Ellipsoid(mb, mp, new Vec3(0.09, 0.09, 0.09), Palette.ICHOR, 0.4, 0.7, 4);
                    }
                    break;
                }
                case "laser_array":
                {
                    // a fleshy arm bearing a rigid fan of crystalline emitters
                    var wrist = ArmDrop(mb, s, side, 0.44 * scale, scale, Palette.CHITIN, pg, n);
                    var mountR = (0.4 + 0.3 * girth) * scale;
                    Prims.Ellipsoid(mb, wrist, new Vec3(mountR, mountR * 0.9, mountR), Palette.CHITIN, 0.4, 0, 8);
                    var nEmit = (int)Clamp(3 + Math.Round(count * 4), 3, 7);
                    var emitL = (1.3 + 1.3 * len) * scale;
                    for (var i = 0; i < nEmit; i++)
                    {
                        var a = ((double)i / Math.Max(nEmit - 1, 1) - 0.5) * 1.1;   // rigid fan, not a droop
                        var tip = new Vec3(wrist.X + Math.Sin(a) * 0.35 * scale, wrist.Y + emitL,
                            wrist.Z + Math.Cos(a) * 0.35 * scale + 0.3 * scale);
                        Prims.Tube(mb, new[] { wrist, tip }, new[] { 0.09 * scale, 0.03 * scale },
                            Col.Lp(Palette.CHITIN, new Col(210, 230, 238), 0.4), 0.6, 0.3, 6);
                        Prims.Ellipsoid(mb, tip, new Vec3(0.06, 0.06, 0.06), Palette.LASER_N, 0.5, 1, 5);
                    }
                    break;
                }
                case "photon_blaster":
                {
                    // a fleshy arm ending in a broad bioluminescent maw
                    var wrist = ArmDrop(mb, s, side, 0.5 * scale, scale, Palette.CHITIN, pg, n);
                    var podR = (0.55 + 0.55 * girth) * scale;
                    Prims.Ellipsoid(mb, wrist, new Vec3(podR, podR * 0.95, podR * 1.1),
                        Col.Lp(Palette.CHITIN, Palette.PHOTON_N, 0.15), 0.45, 0, 10);
                    var mawC = new Vec3(wrist.X, wrist.Y + podR * 0.9, wrist.Z + podR * 0.6);
                    Prims.Ellipsoid(mb, mawC, new Vec3(podR * 0.55, podR * 0.5, podR * 0.3),
                        Col.Sh(Palette.CHITIN, 0.7), 0.3, 0, 10);   // dark maw rim
                    var irisScale = 0.6 + 0.4 * orn;
                    Prims.Ellipsoid(mb, mawC,
                        new Vec3(podR * 0.38 * irisScale, podR * 0.34 * irisScale, podR * 0.18),
                        Palette.PHOTON_N, 0.5, 0.85 + 0.15 * orn, 8);
                    break;
                }

                // ---- sensors (paired via sock.Mirror) ----
                case "antenna":
                {
                    var antL = 1.5 + 1.7 * len;
                    var aR = 0.11 + 0.09 * girth;
                    var gDir = (n + new Vec3(0, 0.55, 0)).Norm();
                    var path = new List<Vec3>();
                    for (var i = 0; i <= 6; i++)
                    {
                        var t = (double)i / 6;
                        path.Add(new Vec3(
                            s.X + gDir.X * t * antL + side * t * t * 0.5,
                            s.Y + gDir.Y * t * antL,
                            s.Z + gDir.Z * t * antL + Math.Sin(t * 2.2) * 0.25));
                    }
                    Prims.LimbJoint(mb, path[0], path[1] - path[0], aR);
                    var ar = new double[7];
                    for (var i = 0; i < 7; i++) ar[i] = aR;
                    Prims.Tube(mb, path, ar, Palette.BONE, 0.35, 0, 6);
                    if (girth > 0.3)
                        Prims.Ellipsoid(mb, path[6], new Vec3(0.3, 0.3, 0.3), Palette.BONDK, 0.5, 0, 6);
                    break;
                }
                case "horn":
                {
                    var hornR = 0.3 + 0.4 * girth;
                    var hDir = (n + new Vec3(0, 0.55, 0)).Norm();
                    Prims.LimbJoint(mb, s, hDir, hornR * 0.85);
                    Prims.CurvedCone(mb, s, hDir, 1.2 + 1.5 * girth, hornR,
                        new Vec3(side * (0.3 + curl * 0.9), curl * 0.4, -0.2),
                        Col.Lp(Palette.BONE, skin, 0.25), 0.4);
                    break;
                }
                case "sensor_mast":
                {
                    var mastL = 1.5 + 1.0 * len;
                    var mDir = (n + new Vec3(0, 1.1, 0)).Norm();
                    var mTop = s + mDir * mastL;
                    Prims.LimbJoint(mb, s, mDir, 0.2);
                    Prims.Tube(mb, new[] { s, mTop }, new[] { 0.22, 0.16 }, Palette.METAL, 0.8, 0, 8);
                    Prims.Ellipsoid(mb, new Vec3(mTop.X, mTop.Y, mTop.Z + 0.15),
                        new Vec3(0.68, 0.68, 0.18), Palette.METDK, 0.7, 0, 10);
                    Prims.Ellipsoid(mb, new Vec3(mTop.X, mTop.Y, mTop.Z + 0.34),
                        new Vec3(0.16, 0.16, 0.16), Palette.GLOW, 0.5, 1, 6);
                    break;
                }
                case "sensor_stub":
                {
                    Prims.Ellipsoid(mb, s, new Vec3(0.3, 0.22, 0.3), Palette.PALLOR, 0.25, 0, 6);
                    break;
                }

                // ---- eyes (single socket, patterns handle multiplicity) ----
                case "bug_eyes":
                {
                    // always a MAIN PAIR; extra count genes add smaller
                    // mutant eyes above and beside them
                    var nEyes = (int)Clamp(2 + Math.Round(count * 3), 2, 5);
                    var spots = new[]
                    {
                        new[] { -0.46, 0.0, 1.0 }, new[] { 0.46, 0.0, 1.0 }, new[] { 0.0, 0.5, 0.6 },
                        new[] { -0.74, 0.4, 0.55 }, new[] { 0.74, 0.4, 0.55 },
                    };
                    var eyeR = 0.30 + 0.20 * girth;
                    for (var i = 0; i < nEyes; i++)
                    {
                        var ex = spots[i][0];
                        var ey = spots[i][1];
                        var sc = spots[i][2];
                        Eyeball(mb, new Vec3(s.X + ex * sock.FaceR, s.Y + ey * 1.1, s.Z - Math.Abs(ex) * 0.2),
                            eyeR * sc, skin, 0.5, n);
                    }
                    break;
                }
                case "cyclops_eye":
                {
                    var eyeR = 0.55 + 0.4 * girth;
                    Eyeball(mb, s, eyeR, skin, 0.7, n);
                    // one heavy scowling unibrow
                    Prims.Tube(mb, new[]
                    {
                        new Vec3(s.X - eyeR * 1.05, s.Y + eyeR * 0.72, s.Z - 0.15),
                        new Vec3(s.X, s.Y + eyeR * 0.98, s.Z + 0.05),
                        new Vec3(s.X + eyeR * 1.05, s.Y + eyeR * 0.72, s.Z - 0.15),
                    }, new[] { 0.2, 0.26, 0.2 }, Col.Sh(skin, 0.5), 0.25, 0, 7);
                    break;
                }
                case "stalk_eyes":
                {
                    var stalkL = 1.1 + 1.4 * len;
                    foreach (var ss in Sides)
                    {
                        var top = new Vec3(s.X + ss * 0.75, s.Y + stalkL, s.Z + 0.25);
                        Prims.Tube(mb, new[]
                        {
                            new Vec3(s.X + ss * 0.45, s.Y - 0.3, s.Z - 0.3),
                            new Vec3(s.X + ss * 0.7, s.Y + stalkL * 0.6, s.Z),
                            top,
                        }, new[] { 0.16, 0.13, 0.11 }, Palette.BONDK, 0.3, 0, 6);
                        Eyeball(mb, top, 0.4 + 0.2 * girth, skin, 0.25, (n + new Vec3(0, 0, 0.8)).Norm());
                    }
                    break;
                }
                case "optic_visor":
                {
                    Prims.Ellipsoid(mb, new Vec3(s.X, s.Y, s.Z - 0.2),
                        new Vec3(sock.FaceR * 0.84, 0.5, 0.36), Palette.METDK, 0.75, 0, 12);
                    var nLens = (int)Clamp(1 + Math.Round(count * 2), 1, 3);
                    for (var i = 0; i < nLens; i++)
                    {
                        var ex = (i - (nLens - 1) / 2.0) * sock.FaceR * 0.55;
                        Prims.Ellipsoid(mb, new Vec3(s.X + ex, s.Y, s.Z + 0.2),
                            new Vec3(0.26, 0.26, 0.1), Palette.GLOW, 0.6, 1, 8);
                    }
                    break;
                }
                case "eye_socket":
                {
                    Prims.Ellipsoid(mb, new Vec3(s.X, s.Y, s.Z - 0.05),
                        new Vec3(0.55, 0.4, 0.18), Col.Sh(skin, 0.5), 0.15, 0, 8);
                    break;
                }
            }
        }

        /// <summary>Chunky little arm from shoulder to a hanging wrist;
        /// returns the wrist position. Shaped by the hand part's own genes:
        /// length -> arm length, curl -> elbow bend, taper -> forearm mass,
        /// girth -> bicep bulge. The upper arm exits along the socket
        /// normal so the limb clears the torso before gravity takes it.</summary>
        private static Vec3 ArmDrop(Builder mb, Vec3 s, double side, double armR, double scale,
            Col armCol, double[] pg, Vec3 n)
        {
            var len = P(pg, 0, 0.5);
            var girth = P(pg, 1, 0.5);
            var taper = P(pg, 2, 0.5);
            var curl = P(pg, 3, 0.5);
            var armLen = (2.5 + 2.0 * len) * scale;
            var bend = 0.35 + curl * 0.75;
            var ex = s + n * ((armR * 1.6 + 0.25) * scale);
            var elbow = new Vec3(ex.X + side * 0.2 * scale, ex.Y - armLen * 0.44, ex.Z + 0.08);
            var wrist = new Vec3(elbow.X + side * 0.2 * scale, elbow.Y - armLen * 0.52,
                elbow.Z + 0.25 + bend * 0.4);
            var foreR = Math.Max(armR * 0.55, armR * (1.2 - 0.8 * taper + 0.3 * girth));
            Prims.LimbJoint(mb, s, n, armR * 1.15);   // brass retaining ring, on the normal
            // the shoulder ball itself -- flesh, seated in the ring
            Prims.Ellipsoid(mb, s + n * (armR * 0.55 * scale),
                new Vec3(armR * 1.3, armR * 1.2, armR * 1.25), armCol, 0.3, 0, 8);
            Prims.Tube(mb, new[] { s, ex, elbow, wrist },
                new[] { armR * 1.25, armR * 1.05, Math.Max(armR * 0.8, foreR * 0.9), foreR },
                armCol, 0.3, 0, 9, 1);
            return wrist;
        }

        /// <summary>Glossy toy eye with a hooded, skin-coloured upper lid.
        /// `hood` 0..1 sets how heavily the lid droops -- the b-movie
        /// menace dial.</summary>
        private static void Eyeball(Builder mb, Vec3 c, double r, Col skin, double hood, Vec3 n)
        {
            Prims.Ellipsoid(mb, c, new Vec3(r, r, r), Palette.EYEWH, 0.85, 0, 10);
            Prims.Ellipsoid(mb, new Vec3(c.X + n.X * r * 0.72, c.Y + n.Y * r * 0.72, c.Z + n.Z * r * 0.72),
                new Vec3(r * 0.38, r * 0.38, r * 0.38), Palette.PUPIL, 0.95, 0, 8);
            if (hood > 0)
                Prims.Ellipsoid(mb, new Vec3(c.X, c.Y + r * (0.62 - 0.28 * hood), c.Z - r * 0.10),
                    new Vec3(r * 1.07, r * (0.42 + 0.34 * hood), r * 1.03), skin, 0.3, 0, 8);
        }

        private static void RingStitch(Builder mb, Vec3 c, double r)
        {
            var pts = new List<Vec3>();
            for (var i = 0; i <= 12; i++)
            {
                var a = (double)i / 12 * Math.PI * 2;
                pts.Add(new Vec3(c.X + Math.Cos(a) * r, c.Y, c.Z + Math.Sin(a) * r));
            }
            var radii = new double[pts.Count];
            for (var i = 0; i < radii.Length; i++) radii[i] = 0.07;
            Prims.Tube(mb, pts, radii, Palette.STITCH, 0.1, 0, 4, 0);
        }
    }
}
