using System;
using System.Collections.Generic;
using MadDr.RosterClient;

namespace MadDr.CreatureMesh
{
    /// <summary>Where the gait rig should mount a leg pair: position +
    /// outward normal in creature space (feet on y=0), mirrored across x.
    /// Legs are the one part family NOT baked into the mesh -- Unity's rig
    /// animates them as transforms so the no-skating contract
    /// (MonsterBody's distance-phased gait) keeps working. LegKit supplies
    /// the family's real geometry for the rig's segments.</summary>
    public sealed class LegSocketInfo
    {
        public Vec3 P;
        public Vec3 Nrm;
        public double Len;
        public string Family = "";
        public double[] Params = new double[0];
    }

    /// <summary>Where each bat wing hinges -- the other part NOT baked
    /// into the main mesh (alongside legs), for the same reason: Unity
    /// needs to pose it as a live transform, this time to flap. Left/
    /// Right are each a full, independent chunk set, root-relative (the
    /// shoulder joint sits at local origin) so Unity can rotate the whole
    /// wing around that point without any per-frame vertex work.</summary>
    public sealed class WingSocketInfo
    {
        public Vec3 RootL;
        public Vec3 RootR;
        public IReadOnlyList<MeshChunk> Left = new List<MeshChunk>();
        public IReadOnlyList<MeshChunk> Right = new List<MeshChunk>();
    }

    /// <summary>Everything Unity needs to regenerate a Lab creature from
    /// its DNA: the material-bucketed mesh chunks (torso, head, face,
    /// arms, weapons, eyes -- the works) plus the leg sockets for the gait
    /// rig and framing heights. Leg is null on plans that ignore the leg
    /// slot (blob, serpentine, treant, floater -- silent genes). Wing is
    /// non-null only for "winged".</summary>
    public sealed class CreatureMeshResult
    {
        public IReadOnlyList<MeshChunk> Chunks = new List<MeshChunk>();
        public LegSocketInfo? Leg;
        public WingSocketInfo? Wing;
        public Col Skin;
        public double TopY;
        public double WaistY;
    }

    /// <summary>Port of the Lab's creature assembly
    /// (site/creature-renderer.js, docs/08) -- all nine body plans, Mad
    /// Doctor faction kit. Deliberately dropped from the JS: per-vertex
    /// color gradients (skinColorFn -> flat base color per chunk),
    /// texture tiling, anim/gait/blink vertex channels (Unity animates
    /// via transforms), and mb.glow() light halos.</summary>
    public static class CreatureBuilder
    {
        /// <summary>Unknown plans fall back to tetrapod, same as the JS
        /// (`builders[plan] ?? planTetrapod`), so this never returns null
        /// for a well-formed genome.</summary>
        public static CreatureMeshResult Build(GenomeDto genome)
        {
            var mb = new Builder();
            var o = MakeCtx(genome);
            Sockets s;
            switch (genome.Body.Plan)
            {
                case "blob": s = PlanBlob(mb, o); break;
                case "serpentine": s = PlanSerpentine(mb, o); break;
                case "winged": s = PlanWinged(mb, o); break;
                case "crab": s = PlanCrab(mb, o); break;
                case "arachnid": s = PlanArachnid(mb, o); break;
                case "avian": s = PlanAvian(mb, o); break;
                case "treant": s = PlanTreant(mb, o); break;
                case "floater": s = PlanFloater(mb, o); break;
                default: s = PlanTetrapod(mb, o); break;
            }

            if (s.Leg != null)
            {
                s.Leg.Family = genome.Slots.Leg.Family;
                s.Leg.Params = genome.Slots.Leg.Params;
            }

            if (s.Hand != null) BuildSlot(mb, "hand", genome.Slots.Hand, s.Hand, o);
            if (!o.Headless)
            {
                if (s.Sensor != null)
                {
                    // a STORAGE vessel is a tank on the creature's BACK/TOP,
                    // not a sense organ on its head -- each plan provides its
                    // own Back mount (docs/22, creator direction); the
                    // derived DorsalSock is only a fallback for a plan that
                    // forgot to. Everything else stays head-mounted.
                    var sensorSock = IsStorageVessel(genome.Slots.Sensor.Family)
                        ? (s.Back ?? DorsalSock(s)) : s.Sensor;
                    BuildSlot(mb, "sensor", genome.Slots.Sensor, sensorSock, o);
                }
                if (s.Eye != null) BuildSlot(mb, "eye", genome.Slots.Eye, s.Eye, o);
            }

            mb.FixWinding();

            return new CreatureMeshResult
            {
                Chunks = mb.Chunks,
                Skin = o.Skin,
                TopY = s.TopY,
                WaistY = s.WaistY,
                Leg = s.Leg,
                Wing = s.Wing,
            };
        }

        // gene context for one creature -- the slice of the JS `o` object
        // the maddr paths actually read
        private sealed class Ctx
        {
            public Col Skin;
            public double Vigor;
            public double Bulk;
            public double Limb;
            public double Tail;
            public double HeadScale;
            public int HeartLevel;
            public double LegLen;
            public string? LegFam;
            public string BrainTier = "average";
            public bool Headless;
            /// <summary>Storage-vessel contents read (docs/22): RED for a
            /// blood harvester, WHITE for a bone harvester -- set from the
            /// hand tool, since the tool decides what the tank fills with.</summary>
            public bool StoreIsBone;
        }

        private sealed class Sockets
        {
            public Sock? Hand;
            public Sock? Sensor;
            public Sock? Eye;
            /// <summary>Where a storage vessel straps on (docs/22): each plan
            /// sets this from its OWN real geometry -- top of the shell for
            /// horizontal bodies (crab/arachnid, normal up), mid-back for
            /// upright torsos (normal backward) -- always dead centre,
            /// always clear of the head, tail, and wing roots.</summary>
            public Sock? Back;
            public LegSocketInfo? Leg;
            public WingSocketInfo? Wing;
            public double TopY;
            public double WaistY;
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

        private static double HeadScaleOf(string tier)
        {
            switch (tier)
            {
                case "dim": return 0;
                case "gifted": return 0.3;
                case "mastermind": return 0.75;
                default: return 0.15;
            }
        }

        private static Ctx MakeCtx(GenomeDto g)
        {
            var vigor = P(g.Heart.Params, 0, 0.5);
            var hue = P(g.Body.Params, 0, 0.5);
            var o = new Ctx
            {
                Vigor = vigor,
                Skin = Palette.MadDrSkin(hue, vigor),
                Bulk = P(g.Body.Params, 1, 0.5),
                Limb = P(g.Body.Params, 2, 0.5),
                Tail = P(g.Body.Params, 3, 0.5),
                HeadScale = HeadScaleOf(g.Brain.Tier),
                HeartLevel = HeartLevelOf(g.Heart.Tier),
                BrainTier = g.Brain.Tier,
                Headless = g.Slots.Sensor.Family == "sensor_stub" && g.Slots.Eye.Family == "eye_socket",
                // bone-dominant tools fill the tank with bone (white); every
                // other tool (lamprey, siphon, claws...) fills it with blood
                // (red) -- matches harvest.ts's per-tool gather bias
                StoreIsBone = g.Slots.Hand.Family == "bone_saw"
                    || g.Slots.Hand.Family == "chain_blade" || g.Slots.Hand.Family == "pincer",
            };

            // leg genes set stance height (stumps slump low); plans that
            // ignore the leg slot never read this
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
            return o;
        }

        private static readonly double[] Sides = { 1, -1 };

        // ---- body plans ------------------------------------------------------

        private static Sockets PlanTetrapod(Builder mb, Ctx o)
        {
            var b = o.Limb;                       // the limb axis IS the build axis here
            var w = 1.9 + 1.0 * o.Bulk;           // human-ish width, not beach-ball
            var h = 3.1 + 0.7 * o.Bulk;
            var waistY = o.LegLen + 1.15;         // lower torso lives below the belt
            var y0 = waistY - 0.15;
            var levels = TorsoLevels(b, w, h, y0, 0.5);

            Prims.Lathe(mb, levels, o.Skin, 0.28, 0, 18);
            var shl = levels[3];
            if (b > 0.5)                          // brute deltoid caps
                foreach (var s in Sides)
                    Prims.Ellipsoid(mb, new Vec3(s * shl.Rx * 0.85, shl.Y + 0.15, shl.Z),
                        new Vec3(w * 0.48 * b, w * 0.42 * b, w * 0.44 * b), o.Skin, 0.28, 0, 10);
            var ch = levels[2];
            StitchSeam(mb, ch.Y - h * 0.12, ch.Rx, ch.Rz, ch.Z);
            BuildPelvis(mb, o, levels[0].Rx, waistY);

            // an actual neck between the shoulders and the skull
            var neckTop = y0 + h + 0.55;
            BuildNeck(mb, o, y0 + h - 0.3, neckTop, levels[4].Z * 0.7, levels[4].Z * 0.8, w * 0.32);

            var head = BuildHead(mb, o, neckTop - 0.2, levels[4].Z);
            if (!o.Headless) FrankenDetails(mb, head.HC, head.HR, o.HeartLevel, o);

            AddTail(mb, o, o.LegLen + 0.55, -levels[0].Rx * 0.75, false);

            var slope = (levels[2].Rx - levels[4].Rx) / Math.Max(0.4, levels[4].Y - levels[2].Y);
            var sensP = new Vec3(head.HR.X * 0.52, head.TopY, head.HC.Z - 0.1);
            var eyeP = new Vec3(0, head.HC.Y + head.HR.Y * 0.20, head.HC.Z + head.HR.Z * 0.62);
            return new Sockets
            {
                Hand = new Sock
                {
                    P = new Vec3(shl.Rx * 0.92 + (b > 0.5 ? w * 0.28 * b : 0), shl.Y, shl.Z + 0.15),
                    Nrm = new Vec3(1, slope * 0.5, 0.15).Norm(),
                    Mirror = true,
                },
                Sensor = HeadSock(sensP, head, true),
                Eye = EyeSock(eyeP, head),
                // upright torso: a vertical pack against the mid-back, at
                // the chest level's true rear face
                Back = new Sock
                {
                    P = new Vec3(0, (waistY + shl.Y) * 0.5, ch.Z - ch.Rz * 0.92),
                    Nrm = new Vec3(0, 0.15, -1).Norm(),
                    Mirror = false,
                },
                Leg = new LegSocketInfo
                {
                    P = new Vec3(Math.Max(0.7, levels[0].Rx * 0.58), o.LegLen, 0),
                    Nrm = new Vec3(0.3, -1, 0).Norm(),
                    Len = o.LegLen,
                },
                TopY = o.Headless ? neckTop : head.TopY,
                WaistY = waistY,
            };
        }

        private static Sockets PlanBlob(Builder mb, Ctx o)
        {
            // limb gene sets the pour: 0 = wide flat puddle, 1 = tall
            // gelatin tower. Blobs never get a leg socket at all.
            var tall = o.Limb;
            var dr = (3.0 + 1.3 * o.Bulk) * (1.15 - 0.40 * tall);
            var dR = new Vec3(dr, (2.5 + 1.0 * o.Bulk) * (0.62 + 1.05 * tall), dr);
            var dC = new Vec3(0, dR.Y * 0.9, 0);

            // Organs first: the outer mass draws translucent, so whatever
            // should show through must already be in the mesh (draw-order
            // rule, docs/08 -- Unity's transparent queue draws after
            // opaques, which lands the same way).
            var hs = Math.Min(dR.X, Math.Min(dR.Y, dR.Z)) * 0.20;
            var hb = new Vec3(dR.X * 0.12, dC.Y + dR.Y * 0.20, dR.Z * 0.16);

            // the large main chamber -- the base everything else sits on
            Prims.Lathe(mb, new[]
            {
                new Prims.LatheLevel(hb.Y - hs * 0.55, hb.X, hb.Z, hs * 0.16, hs * 0.14),
                new Prims.LatheLevel(hb.Y + hs * 0.25, hb.X, hb.Z, hs * 0.68, hs * 0.60),
                new Prims.LatheLevel(hb.Y + hs * 0.80, hb.X, hb.Z, hs * 0.60, hs * 0.52),
            }, Palette.HEARTC_L, 0.55, 0, 6);

            // left and right ventricles, side by side on top
            var topChY = hb.Y + hs * 0.80;
            var vSide = hs * 0.36;
            Prims.Lathe(mb, new[]
            {
                new Prims.LatheLevel(topChY + hs * 0.02, hb.X - vSide, hb.Z, hs * 0.30, hs * 0.27),
                new Prims.LatheLevel(topChY + hs * 0.50, hb.X - vSide, hb.Z, hs * 0.46, hs * 0.40),
            }, Palette.HEARTC_L, 0.55, 0, 6);
            Prims.Lathe(mb, new[]
            {
                new Prims.LatheLevel(topChY + hs * 0.02, hb.X + vSide, hb.Z, hs * 0.26, hs * 0.23),
                new Prims.LatheLevel(topChY + hs * 0.46, hb.X + vSide, hb.Z, hs * 0.40, hs * 0.34),
            }, Palette.HEARTC_R, 0.55, 0, 5);

            // the stomach: a fleshy sac slung mid-body
            Prims.Ellipsoid(mb, new Vec3(-dR.X * 0.10, dC.Y - dR.Y * 0.05, dR.Z * 0.08),
                new Vec3(dR.X * 0.30, dR.Y * 0.24, dR.Z * 0.28), Palette.STOMACHC, 0.4, 0, 10);

            // the digestive tract: coiled through the lower half
            var gutPath = new List<Vec3>();
            var gutR = new List<double>();
            const int nSeg = 12;
            for (var i = 0; i <= nSeg; i++)
            {
                var t = (double)i / nSeg;
                var ang = t * Math.PI * 3.4;
                var rad = dR.X * (0.42 - 0.20 * t);
                gutPath.Add(new Vec3(
                    Math.Cos(ang) * rad,
                    dC.Y - dR.Y * 0.4 + t * dR.Y * 0.45,
                    Math.Sin(ang) * rad * (dR.Z / dR.X)));
                gutR.Add(dR.X * 0.055 * (1 - 0.25 * Math.Sin(t * 8)));
            }
            Prims.Tube(mb, gutPath, gutR, Palette.GUTC, 0.35, 0, 8, 3);

            // the mass itself: translucent gelatin, organs showing through
            mb.SetAlpha(0.55);
            Prims.Ellipsoid(mb, dC, dR, o.Skin, 0.34, 0, 18);
            // the flattened base: full puddle at limb=0, gone by limb~0.87
            var skirtK = Math.Max(0, 1 - tall * 1.15);
            if (skirtK > 0.03)
                Prims.Ellipsoid(mb, new Vec3(0, 0.62, 0),
                    new Vec3(dr * 1.14 * skirtK, 0.85 * skirtK, dr * 1.14 * skirtK),
                    Col.Sh(o.Skin, 0.92), 0.3, 0, 12);
            mb.SetAlpha(1);

            // surface boils: opaque flecks on the translucent hide
            for (var a = 0; a < 6; a++)
            {
                var th = a * 1.047 + 0.4;
                Prims.Ellipsoid(mb, new Vec3(Math.Cos(th) * dr * 0.9, 1.1 + a % 3 * 0.5, Math.Sin(th) * dr * 0.9),
                    new Vec3(0.5, 0.42, 0.5), Col.Sh(o.Skin, 0.88), 0.4, 0, 6);
            }

            var handP = new Vec3(dr * 0.92, dC.Y + 0.4, 0);
            var sensP = new Vec3(dr * 0.5, dC.Y + dR.Y * 0.85, 0);
            var eyeP = new Vec3(0, dC.Y + dR.Y * 0.35, dR.Z * 0.9);
            return new Sockets
            {
                Hand = new Sock { P = handP, Nrm = Prims.EllipN(handP, dC, dR), Mirror = true },
                Sensor = new Sock { P = sensP, Nrm = Prims.EllipN(sensP, dC, dR), Mirror = true },
                Eye = new Sock { P = eyeP, Nrm = Prims.EllipN(eyeP, dC, dR), Mirror = false, FaceR = dr * 0.8 },
                // a dome has no back -- storage sits flat on TOP of the
                // mound, half-sunk in the gelatin
                Back = new Sock
                {
                    P = new Vec3(0, dC.Y + dR.Y * 0.8, 0),
                    Nrm = new Vec3(0, 1, 0),
                    Mirror = false,
                },
                TopY = dC.Y + dR.Y,
                WaistY = dC.Y,
            };
        }

        private static Sockets PlanSerpentine(Builder mb, Ctx o)
        {
            var girth = o.Bulk;
            var baseR = 0.95 + 0.7 * girth;
            var headY = 6.6 + 2.6 * o.Limb;
            var path = new List<Vec3>();
            var radii = new List<double>();
            const int n = 30;
            for (var k = 0; k <= n; k++)
            {                                            // ground coil
                var t = (double)k / n;
                var ang = t * Math.PI * 2 * 1.6 + 0.8;
                var r = 2.75 - 1.35 * t;
                path.Add(new Vec3(Math.Cos(ang) * r, 0.55 + t * 2.0, Math.Sin(ang) * r * 0.8));
                radii.Add(baseR * Clamp(t * 6, 0.16, 1));
            }
            var neckBase = path[path.Count - 1];
            for (var m = 1; m <= 10; m++)
            {                                            // rising S-neck
                var s = (double)m / 10;
                path.Add(new Vec3(
                    neckBase.X * (1 - s) + Math.Sin(s * Math.PI) * 0.7,
                    neckBase.Y + s * (headY - neckBase.Y),
                    neckBase.Z * (1 - s) + s * 1.1));
                radii.Add(baseR * (1 - 0.42 * s));
            }
            Prims.Tube(mb, path, radii, o.Skin, 0.3, 0, 12, 3);

            // the snake builds its OWN head (never buildHead) -- brain tier
            // only scales it, so even a mastermind serpent keeps a skull
            var hC = new Vec3(0.35, headY + 0.9, 1.25);
            var hR = new Vec3(1.5 + 0.4 * o.HeadScale, 1.3 + 0.4 * o.HeadScale, 1.7);
            Prims.Ellipsoid(mb, hC, hR, o.Skin, 0.3, 0, 14);
            if (girth > 0.55)                            // cobra hood
                Prims.Ellipsoid(mb, new Vec3(hC.X, hC.Y - 0.2, hC.Z - 0.7),
                    new Vec3(hR.X * 1.75, hR.Y * 1.5, 0.5), Col.Sh(o.Skin, 0.9), 0.28, 0, 12);
            // fangs point DOWN on a serpent
            var mz = hC.Z + hR.Z * 0.8;
            Prims.Ellipsoid(mb, new Vec3(hC.X, hC.Y - hR.Y * 0.4, mz),
                new Vec3(hR.X * 0.4, 0.16, 0.2), Palette.MOUTHC, 0.15, 0, 8);
            foreach (var s in Sides)
                Prims.CurvedCone(mb, new Vec3(hC.X + s * hR.X * 0.3, hC.Y - hR.Y * 0.42, mz),
                    new Vec3(0, -1, 0.1), 0.6, 0.13, new Vec3(0, 0, 0.04), Palette.CLAW, 0.6);
            // forked tongue, parked just inside the mouth
            foreach (var s in Sides)
                Prims.Tube(mb, new[]
                {
                    new Vec3(hC.X, hC.Y - hR.Y * 0.38, mz - 0.55),
                    new Vec3(hC.X + s * 0.05, hC.Y - hR.Y * 0.40, mz - 0.15),
                    new Vec3(hC.X + s * 0.22, hC.Y - hR.Y * 0.34, mz + 0.25),
                }, new[] { 0.09, 0.07, 0.02 }, Palette.TONGUE, 0.6, 0, 5);

            var sSensP = new Vec3(hC.X + 0.5, hC.Y + hR.Y * 0.8, hC.Z - 0.3);
            var sEyeP = new Vec3(hC.X, hC.Y + hR.Y * 0.25, hC.Z + hR.Z * 0.85);
            var fakeHead = new Head { HC = hC, HR = hR, TopY = hC.Y + hR.Y };
            return new Sockets
            {
                Hand = new Sock
                {
                    P = new Vec3(hC.X + 1.0, headY - 1.3, 0.9),
                    Nrm = new Vec3(1, 0.1, 0.35).Norm(),
                    Mirror = true,
                    Tiny = true,
                },
                Sensor = new Sock { P = sSensP, Nrm = Prims.EllipN(sSensP, hC, hR), Mirror = false },
                Eye = EyeSock(sEyeP, fakeHead),
                // cargo strapped flat on TOP of the thickest coil, where
                // the neck rises -- never floating behind the S-curve
                Back = new Sock
                {
                    P = new Vec3(neckBase.X * 0.75, neckBase.Y + baseR * 0.5, neckBase.Z * 0.75),
                    Nrm = new Vec3(0, 1, 0),
                    Mirror = false,
                },
                TopY = hC.Y + hR.Y,
                WaistY = headY * 0.4,
            };
        }

        private static Sockets PlanWinged(Builder mb, Ctx o)
        {
            var b = o.Bulk * 0.85;               // bulk sets the build: imp vs gargoyle
            var w = 1.5 + 0.8 * o.Bulk;
            var h = 2.8 + 0.6 * o.Bulk;
            var waistY = o.LegLen + 0.95;
            var y0 = waistY - 0.12;
            var levels = TorsoLevels(b, w, h, y0, 0.4);

            Prims.Lathe(mb, levels, o.Skin, 0.28, 0, 14);
            BuildPelvis(mb, o, levels[0].Rx, waistY);
            var neckTop = y0 + h + 0.45;
            BuildNeck(mb, o, y0 + h - 0.25, neckTop, levels[4].Z * 0.7, levels[4].Z * 0.8, w * 0.3);
            var head = BuildHead(mb, o, neckTop - 0.2, levels[4].Z);
            if (!o.Headless) FrankenDetails(mb, head.HC, head.HR, o.HeartLevel, o);

            AddTail(mb, o, o.LegLen + 0.5, -levels[0].Rx * 0.75, true);   // devil spade

            // bat wings, rooted at the BACK shoulders and sweeping out and
            // behind. NOT baked into the main mesh (like legs) -- each side
            // is its own root-relative chunk set so Unity can hinge it at
            // the shoulder and flap it, rather than a static pose.
            var span = 4.6 + 3.6 * o.Limb;
            var shY = levels[3].Y;
            var rootZ = levels[3].Z - levels[3].Rz * 0.8;
            var rootL = new Vec3(0.9, shY, rootZ);
            var rootR = new Vec3(-0.9, shY, rootZ);
            var wingL = new Builder();
            var wingR = new Builder();
            BuildWingInto(wingL, o, rootL, span, 1);
            BuildWingInto(wingR, o, rootR, span, -1);
            wingL.FixWinding();
            wingR.FixWinding();

            var sensP = new Vec3(head.HR.X * 0.5, head.TopY, head.HC.Z - 0.1);
            var eyeP = new Vec3(0, head.HC.Y + head.HR.Y * 0.2, head.HC.Z + head.HR.Z * 0.62);
            return new Sockets
            {
                Hand = new Sock
                {
                    P = new Vec3(levels[2].Rx * 0.95, levels[2].Y + 0.2, levels[2].Z + 0.3),
                    Nrm = new Vec3(1, 0.1, 0.3).Norm(),
                    Mirror = true,
                    Tiny = true,
                },
                Sensor = HeadSock(sensP, head, true),
                Eye = EyeSock(eyeP, head),
                Leg = new LegSocketInfo
                {
                    P = new Vec3(Math.Max(0.8, levels[0].Rx * 0.5), o.LegLen, 0),
                    Nrm = new Vec3(0.28, -1, 0).Norm(),
                    Len = o.LegLen,
                },
                Wing = new WingSocketInfo
                {
                    RootL = rootL,
                    RootR = rootR,
                    Left = wingL.Chunks,
                    Right = wingR.Chunks,
                },
                // vertical pack low on the back, BELOW the wing roots at
                // levels[3] so it never collides with the membranes
                Back = new Sock
                {
                    P = new Vec3(0, (waistY + levels[2].Y) * 0.5, levels[2].Z - levels[2].Rz * 0.92),
                    Nrm = new Vec3(0, 0.15, -1).Norm(),
                    Mirror = false,
                },
                TopY = o.Headless ? neckTop : head.TopY,
                WaistY = waistY,
            };
        }

        /// <summary>One bat wing (membrane + bone + fingers + the shoulder
        /// joint hoop), built ROOT-RELATIVE: every vertex is emitted as an
        /// offset from `root` rather than in absolute creature-space, so
        /// the resulting chunk set can be parented in Unity at `root`'s
        /// world position and rotated there as a rigid hinge to flap --
        /// the same NOT-baked-into-the-main-mesh treatment as legs, for
        /// the same reason (a live transform, not a static pose).
        /// Geometry is otherwise unchanged from the original single-mesh
        /// version: same span/droop/chord formulas, same double-sided
        /// membrane (Unity culls back faces where the Lab shader two-
        /// sided them), same bone-and-finger strut.</summary>
        private static void BuildWingInto(Builder wb, Ctx o, Vec3 root, double span, double side)
        {
            const int nU = 9, nV = 3;
            var lead = new Vec3[nU + 1];
            for (var iu = 0; iu <= nU; iu++)
            {
                var u = (double)iu / nU;
                var world = new Vec3(
                    side * (0.9 + u * span),
                    root.Y + Math.Sin(u * Math.PI * 0.85) * 2.5 - u * u * 1.6,
                    root.Z - u * 0.85);
                lead[iu] = world - root;
            }
            // hoop around the wing bone right at its root
            Prims.LimbJoint(wb, lead[0], lead[1] - lead[0], 0.34);

            var spine = Col.Lp(o.Skin, new Col(52, 40, 80), 0.45);
            var wingCol = Col.Sh(Col.Lp(o.Skin, spine, 0.25), 0.95);

            // membrane: emitted twice (front + back sheet) so it reads
            // from both sides under single-sided URP materials
            foreach (var flip in new[] { 1.0, -1.0 })
            {
                var chunk = wb.Begin(wingCol, 0.2, 0);
                var grid = new int[nU + 1][];
                for (var iu = 0; iu <= nU; iu++)
                {
                    var u = (double)iu / nU;
                    var l = lead[iu];
                    var chord = (2.5 * (1 - 0.5 * u) + 0.5) * (1 + 0.10 * Math.Sin(u * Math.PI * 3));
                    grid[iu] = new int[nV + 1];
                    for (var iv = 0; iv <= nV; iv++)
                    {
                        var v = (double)iv / nV;
                        grid[iu][iv] = wb.Vert(chunk,
                            new Vec3(l.X, l.Y - v * chord, l.Z + v * 0.3),
                            new Vec3(0, 0.25 * flip, 0.97 * flip));
                    }
                }
                for (var iu = 0; iu < nU; iu++)
                    for (var iv = 0; iv < nV; iv++)
                        wb.Quad(chunk, grid[iu][iv], grid[iu][iv + 1], grid[iu + 1][iv + 1], grid[iu + 1][iv]);
            }

            var boneR = new double[lead.Length];
            for (var i = 0; i < lead.Length; i++) boneR[i] = 0.24 * (1 - (double)i / lead.Length) + 0.08;
            Prims.Tube(wb, lead, boneR, Palette.BONDK, 0.35, 0, 7, 3);
            foreach (var fu in new[] { 0.45, 0.75 })
            {
                var k = (int)Math.Round(fu * nU);
                var a = lead[k];
                var chord = 2.5 * (1 - 0.5 * fu) + 0.5;
                Prims.Tube(wb, new[] { a, new Vec3(a.X, a.Y - chord, a.Z + 0.3) },
                    new[] { 0.12, 0.05 }, Palette.BONDK, 0.3, 0, 6);
            }
        }

        private static Sockets PlanCrab(Builder mb, Ctx o)
        {
            var w = 3.0 + 1.6 * o.Bulk;
            var d = 2.2 + 1.0 * o.Bulk;
            var h = 1.7 + 0.5 * o.Bulk;
            var y0 = Math.Max(0.5, o.LegLen * 0.7);
            // wide, low, flat-topped shell -- a carapace, not a torso
            var levels = new[]
            {
                new Prims.LatheLevel(y0, 0, 0, w * 0.72, d * 0.72),
                new Prims.LatheLevel(y0 + h * 0.35, 0, 0.05 * d, w * 0.96, d * 0.96),
                new Prims.LatheLevel(y0 + h * 0.62, 0, 0.02 * d, w * 1.00, d * 1.00),
                new Prims.LatheLevel(y0 + h * 0.88, 0, -0.05 * d, w * 0.70, d * 0.72),
                new Prims.LatheLevel(y0 + h, 0, -0.10 * d, w * 0.30, d * 0.42),
            };
            Prims.Lathe(mb, levels, o.Skin, 0.3, 0, 20);

            // no true neck: the head fuses low and forward onto the shell edge
            var head = BuildHead(mb, o, y0 + h * 0.55, levels[1].Rz * 0.85);
            if (!o.Headless) FrankenDetails(mb, head.HC, head.HR, o.HeartLevel, o);

            AddTail(mb, o, y0 + h * 0.3, -levels[0].Rz * 0.9, false);

            var shl = levels[2];
            var sensP = new Vec3(head.HR.X * 0.5, head.TopY, head.HC.Z - 0.1);
            var eyeP = new Vec3(0, head.HC.Y + head.HR.Y * 0.25, head.HC.Z + head.HR.Z * 0.7);
            return new Sockets
            {
                // chelipeds: carried out in FRONT of the carapace, by the
                // mouth -- short, slender, reach-capped so the claw never
                // out-lengths the legs or dips into the ground
                Hand = new Sock
                {
                    P = new Vec3(shl.Rx * 0.30, y0 + h * 0.42, head.HC.Z + head.HR.Z * 0.55),
                    Nrm = new Vec3(0.4, -0.15, 1).Norm(),
                    Mirror = true,
                    Tiny = true,
                    ArmCapLen = Math.Max(0.9, o.LegLen * 0.82),
                },
                Sensor = HeadSock(sensP, head, true),
                Eye = new Sock
                {
                    P = eyeP,
                    Nrm = Prims.EllipN(eyeP, head.HC, head.HR),
                    Mirror = false,
                    FaceR = head.HR.X,
                },
                // horizontal shell: the pack lies flat ON TOP of the
                // carapace, dead centre and biased slightly FORWARD --
                // never behind, never near the tail (creator direction)
                Back = new Sock
                {
                    P = new Vec3(0, y0 + h * 0.92, d * 0.12),
                    Nrm = new Vec3(0, 1, 0),
                    Mirror = false,
                },
                Leg = new LegSocketInfo
                {
                    P = new Vec3(shl.Rx * 0.85, o.LegLen, -shl.Rz * 0.15),
                    Nrm = new Vec3(1, -0.7, 0).Norm(),
                    Len = o.LegLen,
                },
                TopY = Math.Max(y0 + h, o.Headless ? 0 : head.TopY),
                WaistY = y0 + h * 0.5,
            };
        }

        private static Sockets PlanArachnid(Builder mb, Ctx o)
        {
            // two-part body: a smaller cephalothorax forward, a bulbous
            // abdomen behind
            var ar = 1.5 + 0.9 * o.Bulk;
            var cr = 0.9 + 0.4 * o.Bulk;
            var y0 = Math.Max(0.6, o.LegLen * 0.75);
            var aC = new Vec3(0, y0 + ar * 0.7, -ar * 0.5);
            var cC = new Vec3(0, y0 + cr * 0.85, cr * 0.9);
            Prims.Ellipsoid(mb, aC, new Vec3(ar, ar * 0.82, ar * 1.15), o.Skin, 0.32, 0, 18);
            Prims.Ellipsoid(mb, cC, new Vec3(cr, cr * 0.92, cr * 1.05), o.Skin, 0.32, 0, 16);
            Prims.Ellipsoid(mb, new Vec3(0, (aC.Y + cC.Y) * 0.5 - 0.1, (aC.Z + cC.Z) * 0.5),
                new Vec3(cr * 0.5, cr * 0.4, cr * 0.5), Col.Sh(o.Skin, 0.85), 0.3, 0, 10);   // waist pinch

            var head = BuildHead(mb, o, cC.Y + cr * 0.5, cC.Z + cr * 0.6);
            if (!o.Headless) FrankenDetails(mb, head.HC, head.HR, o.HeartLevel, o);

            var sensP = new Vec3(head.HR.X * 0.5, head.TopY, head.HC.Z - 0.1);
            var eyeP = new Vec3(0, head.HC.Y + head.HR.Y * 0.2, head.HC.Z + head.HR.Z * 0.65);
            return new Sockets
            {
                // pedipalps: stubby front appendages flanking the mouth,
                // never reaching past the legs
                Hand = new Sock
                {
                    P = new Vec3(cr * 0.35, cC.Y + 0.15, cC.Z + cr * 0.75),
                    Nrm = new Vec3(0.45, -0.1, 1).Norm(),
                    Mirror = true,
                    Tiny = true,
                    ArmCapLen = Math.Max(0.8, o.LegLen * 0.65),
                },
                Sensor = HeadSock(sensP, head, true),
                Eye = EyeSock(eyeP, head),
                // horizontal body: the pack rides flat ON TOP of the
                // abdomen's crown, biased toward the waist (forward) so
                // nothing hangs off the spinneret end
                Back = new Sock
                {
                    P = new Vec3(0, aC.Y + ar * 0.75, aC.Z + ar * 0.25),
                    Nrm = new Vec3(0, 1, 0),
                    Mirror = false,
                },
                Leg = new LegSocketInfo
                {
                    P = new Vec3(cr * 0.9, o.LegLen, cC.Z * 0.3),
                    Nrm = new Vec3(0.6, -1, 0.1).Norm(),
                    Len = o.LegLen,
                },
                TopY = Math.Max(aC.Y + ar * 0.82, o.Headless ? 0 : head.TopY),
                WaistY = y0,
            };
        }

        private static Sockets PlanAvian(Builder mb, Ctx o)
        {
            var w = 1.4 + 0.7 * o.Bulk;
            var h = 3.4 + 0.8 * o.Bulk;
            var waistY = o.LegLen + 0.9;
            var y0 = waistY - 0.1;
            // forward-leaning raptor-runner silhouette: narrow tail-end,
            // deep chest, leans forward as it rises
            var levels = new[]
            {
                new Prims.LatheLevel(y0, 0, -h * 0.15, w * 0.55, w * 0.7),
                new Prims.LatheLevel(y0 + h * 0.3, 0, -h * 0.05, w * 0.9, w * 1.0),
                new Prims.LatheLevel(y0 + h * 0.6, 0, h * 0.1, w * 1.05, w * 1.1),
                new Prims.LatheLevel(y0 + h * 0.85, 0, h * 0.28, w * 0.65, w * 0.75),
                new Prims.LatheLevel(y0 + h, 0, h * 0.4, w * 0.32, w * 0.42),
            };
            Prims.Lathe(mb, levels, o.Skin, 0.28, 0, 16);
            var ch = levels[2];
            BuildPelvis(mb, o, levels[0].Rx, waistY);

            var neckTop = y0 + h + 1.1;    // a long neck -- the archetype's signature
            BuildNeck(mb, o, y0 + h + 0.1, neckTop, levels[4].Z, levels[4].Z + 0.9, w * 0.22);

            var head = BuildHead(mb, o, neckTop - 0.15, levels[4].Z + 0.9);
            if (!o.Headless) FrankenDetails(mb, head.HC, head.HR, o.HeartLevel, o);

            AddTail(mb, o, y0 + h * 0.15, -levels[0].Rz * 0.8, false);

            var sensP = new Vec3(head.HR.X * 0.52, head.TopY, head.HC.Z - 0.1);
            var eyeP = new Vec3(0, head.HC.Y + head.HR.Y * 0.2, head.HC.Z + head.HR.Z * 0.62);
            return new Sockets
            {
                Hand = new Sock
                {
                    P = new Vec3(ch.Rx * 0.85, ch.Y + 0.2, ch.Z + 0.2),
                    Nrm = new Vec3(1, 0.1, 0.3).Norm(),
                    Mirror = true,
                    Tiny = true,
                },
                Sensor = HeadSock(sensP, head, true),
                Eye = EyeSock(eyeP, head),
                // the raptor's sloped upper back, between chest and
                // shoulders -- high and forward, well clear of the tail
                // counterbalance at the body's low rear
                Back = new Sock
                {
                    P = new Vec3(0, (levels[2].Y + levels[3].Y) * 0.5,
                        ((levels[2].Z - levels[2].Rz) + (levels[3].Z - levels[3].Rz)) * 0.5 * 0.95),
                    Nrm = new Vec3(0, 0.5, -0.87).Norm(),
                    Mirror = false,
                },
                Leg = new LegSocketInfo
                {
                    P = new Vec3(Math.Max(0.6, levels[0].Rx * 0.55), o.LegLen, levels[0].Z * 0.3),
                    Nrm = new Vec3(0.25, -1, 0).Norm(),
                    Len = o.LegLen,
                },
                TopY = o.Headless ? neckTop : head.TopY,
                WaistY = waistY,
            };
        }

        private static Sockets PlanTreant(Builder mb, Ctx o)
        {
            var w = 2.0 + 1.3 * o.Bulk;
            var h = 4.2 + 1.0 * o.Bulk;
            const double y0 = 0.3;   // rooted low; the plan ignores 'leg'
            // a thick columnar trunk, widest at the base, tapering gently up
            var levels = new[]
            {
                new Prims.LatheLevel(y0, 0, 0, w * 1.15, w * 1.15),
                new Prims.LatheLevel(y0 + h * 0.15, 0, 0, w * 0.85, w * 0.85),
                new Prims.LatheLevel(y0 + h * 0.55, 0, 0, w * 0.62, w * 0.62),
                new Prims.LatheLevel(y0 + h * 0.85, 0, 0, w * 0.5, w * 0.5),
                new Prims.LatheLevel(y0 + h, 0, 0, w * 0.36, w * 0.36),
            };
            Prims.Lathe(mb, levels, o.Skin, 0.22, 0, 16);

            // gnarled roots flaring from the base, standing in for legs/feet
            const int nRoots = 5;
            for (var i = 0; i < nRoots; i++)
            {
                var a = (double)i / nRoots * Math.PI * 2;
                var basePt = new Vec3(Math.Cos(a) * levels[0].Rx * 0.7, y0 + 0.5, Math.Sin(a) * levels[0].Rx * 0.7);
                var tip = new Vec3(Math.Cos(a) * levels[0].Rx * 1.7, 0, Math.Sin(a) * levels[0].Rx * 1.7);
                Prims.Tube(mb, new[] { basePt, tip }, new[] { 0.35 + 0.1 * o.Bulk, 0.08 },
                    Col.Sh(o.Skin, 0.8), 0.2, 0, 6, 3);
            }

            var head = BuildHead(mb, o, y0 + h - 0.3, 0);
            if (!o.Headless) FrankenDetails(mb, head.HC, head.HR, o.HeartLevel, o);

            var sensP = new Vec3(head.HR.X * 0.52, head.TopY, head.HC.Z - 0.1);
            var eyeP = new Vec3(0, head.HC.Y + head.HR.Y * 0.2, head.HC.Z + head.HR.Z * 0.62);
            return new Sockets
            {
                Hand = new Sock
                {
                    P = new Vec3(levels[3].Rx * 0.9, levels[3].Y, levels[3].Z + 0.15),
                    Nrm = new Vec3(1, 0.3, 0.15).Norm(),
                    Mirror = true,
                },
                Sensor = HeadSock(sensP, head, true),
                Eye = EyeSock(eyeP, head),
                // vertical trunk: a pack strapped flat to the bark, mid-height
                Back = new Sock
                {
                    P = new Vec3(0, levels[2].Y, -levels[2].Rz * 0.95),
                    Nrm = new Vec3(0, 0.1, -1).Norm(),
                    Mirror = false,
                },
                TopY = o.Headless ? y0 + h : head.TopY,
                WaistY = y0 + h * 0.5,
            };
        }

        private static Sockets PlanFloater(Builder mb, Ctx o)
        {
            // a lean drone-pod hull: spindled front-to-back for a fast,
            // agile silhouette. Hovers -- the plan ignores 'leg'.
            var w = 1.4 + 0.7 * o.Bulk;
            var h = 3.6 + 0.85 * o.Bulk;
            const double y0 = 0.9;
            var levels = new[]
            {
                new Prims.LatheLevel(y0, 0, 0, w * 0.20, w * 0.20),              // tapered nose cone
                new Prims.LatheLevel(y0 + h * 0.16, 0, 0.02 * w, w * 0.62, w * 0.62),   // thruster collar
                new Prims.LatheLevel(y0 + h * 0.5, 0, 0.05 * w, w * 0.80, w * 0.80),    // fuselage waist
                new Prims.LatheLevel(y0 + h * 0.82, 0, 0.02 * w, w * 0.42, w * 0.42),   // canopy neck
                new Prims.LatheLevel(y0 + h, 0, 0, w * 0.18, w * 0.18),
            };
            // hull tints toward gunmetal -- a chassis, not skin -- while
            // still taking a cut of the creature's own pigment
            Prims.Lathe(mb, levels, Col.Lp(Palette.METAL, o.Skin, 0.4), 0.55, 0, 18);

            // stabilizer fins ringing the thruster collar, and a glowing
            // thruster ring underneath
            var collar = levels[1];
            const int nFin = 5;
            for (var i = 0; i < nFin; i++)
            {
                var a = (double)i / nFin * Math.PI * 2;
                var basePt = new Vec3(Math.Cos(a) * collar.Rx * 0.95, collar.Y, Math.Sin(a) * collar.Rx * 0.95);
                var tip = new Vec3(Math.Cos(a) * collar.Rx * 2.15, collar.Y - 0.6 - 0.25 * o.Bulk, Math.Sin(a) * collar.Rx * 2.15);
                Prims.Tube(mb, new[] { basePt, tip }, new[] { 0.2, 0.05 }, Palette.METAL, 0.85, 0, 6, 1);
            }
            Prims.Ellipsoid(mb, new Vec3(0, y0 - 0.1, 0), new Vec3(w * 0.42, 0.1, w * 0.42),
                Palette.GLOW, 0.4, 0.9, 12);

            // the head pokes out through the canopy neck like a cockpit
            var head = BuildHead(mb, o, y0 + h - 0.2, 0);
            if (!o.Headless) FrankenDetails(mb, head.HC, head.HR, o.HeartLevel, o);

            var sensP = new Vec3(head.HR.X * 0.5, head.TopY, head.HC.Z - 0.1);
            var eyeP = new Vec3(0, head.HC.Y + head.HR.Y * 0.15, head.HC.Z + head.HR.Z * 0.6);
            return new Sockets
            {
                // short arms high on the hull -- a full-length armDrop
                // would hang past the fins and read as legs
                Hand = new Sock
                {
                    P = new Vec3(levels[3].Rx * 0.8, levels[3].Y, levels[3].Z + 0.1),
                    Nrm = new Vec3(1, 0, 0.3).Norm(),
                    Mirror = true,
                    Tiny = true,
                },
                Sensor = HeadSock(sensP, head, true),
                Eye = new Sock
                {
                    P = eyeP,
                    Nrm = Prims.EllipN(eyeP, head.HC, head.HR),
                    Mirror = false,
                    FaceR = head.HR.X,
                },
                // upright hull: a saddle pack on the fuselage waist's rear
                // face, at the hull's true surface radius
                Back = new Sock
                {
                    P = new Vec3(0, levels[2].Y, levels[2].Z - levels[2].Rz * 0.92),
                    Nrm = new Vec3(0, 0.1, -1).Norm(),
                    Mirror = false,
                },
                TopY = o.Headless ? y0 + h : head.TopY,
                WaistY = y0 + h * 0.5,
            };
        }

        private static Sock HeadSock(Vec3 p, Head head, bool mirror)
        {
            return new Sock { P = p, Nrm = Prims.EllipN(p, head.HC, head.HR), Mirror = mirror };
        }

        private static Sock EyeSock(Vec3 p, Head head)
        {
            return new Sock { P = p, Nrm = Prims.EllipN(p, head.HC, head.HR), Mirror = false, FaceR = head.HR.X };
        }

        // ---- torso -----------------------------------------------------------

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

        /// <summary>Mad-Doctor neck: a fleshy column (robot piston and
        /// alien stalk variants come with the faction-kit pass).</summary>
        private static void BuildNeck(Builder mb, Ctx o, double y0, double y1, double z0, double z1, double r)
        {
            Prims.Tube(mb, new[] { new Vec3(0, y0, z0), new Vec3(0, y1, z1) },
                new[] { r, r * 0.86 }, Col.Sh(o.Skin, 0.95), 0.28, 0, 10);
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

        // ---- head ------------------------------------------------------------

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
        /// tapered whip out the lower back, curling up. Winged plans cap
        /// it with a devil spade.</summary>
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

        // ---- parts -----------------------------------------------------------

        private sealed class Sock
        {
            public Vec3 P;
            public Vec3 Nrm;
            public bool Mirror;
            public bool Tiny;
            public double FaceR;
            public double ArmCapLen = double.PositiveInfinity;
        }

        private static bool IsStorageVessel(string family)
        {
            return family == "storage_bladder" || family == "steel_tank" || family == "amber_vesicle";
        }

        // ---- storage-pack frame -------------------------------------------------
        // Pack geometry is authored once in a local frame -- `across` the
        // body (X), `along` the spine, `out` of the body surface -- and
        // mapped to world by the mount's orientation: an upright torso's
        // back-mount lays the pack VERTICALLY (along = +Y, out = -Z); a
        // horizontal body's top-mount lays it HORIZONTALLY on the shell
        // (along = +Z, out = +Y), like an actual backpack. Positive `o`
        // is always OUT of the body; negative `o` sinks into the trunk.

        private static Vec3 PackP(Vec3 s, bool topMount, double a, double l, double o)
        {
            return topMount
                ? new Vec3(s.X + a, s.Y + o, s.Z + l)
                : new Vec3(s.X + a, s.Y + l, s.Z - o);
        }

        private static Vec3 PackR(bool topMount, double a, double l, double o)
        {
            return topMount ? new Vec3(a, o, l) : new Vec3(a, l, o);
        }

        // resource-contents colors (docs/22, creator direction): RED blood,
        // WHITE bone -- what the tank is FULL of, by the harvest tool
        private static readonly Col BloodRed = new Col(150, 30, 40);
        private static readonly Col BoneWhite = new Col(224, 216, 194);

        /// <summary>A dorsal (back) mount derived generically from the plan's
        /// own geometry, seated DEAD CENTRE of the back: the eye socket gives
        /// the torso's front half-depth, so its negation is the back face;
        /// the height is the vertical middle of the trunk (between the waist
        /// and the shoulder/hand mount), not up by the neck or down by the
        /// tail. The normal is the near-vertical back's outward normal
        /// (mostly backward, slightly up). Single-mount -- one seated pack,
        /// centered on the spine, not a head-flanking pair.</summary>
        private static Sock DorsalSock(Sockets s)
        {
            var depth = s.Eye != null ? Math.Abs(s.Eye.P.Z) : 0.8;
            var shoulderY = s.Hand != null ? s.Hand.P.Y : s.WaistY + 0.6 * (s.TopY - s.WaistY);
            var y = (s.WaistY + shoulderY) * 0.5;   // dead centre of the trunk
            return new Sock
            {
                P = new Vec3(0, y, -depth * 0.92),
                Nrm = new Vec3(0, 0.2, -1).Norm(),
                Mirror = false,
            };
        }

        private static void BuildSlot(Builder mb, string slot, PartAlleleDto al, Sock sock, Ctx o)
        {
            // dormant organic head sensors: low ornament gene -> bald head
            if (slot == "sensor" && (al.Family == "antenna" || al.Family == "horn") &&
                P(al.Params, 5, 0.5) < 0.35) return;

            // a grafted part remembers its OWN hue (surgery.ts, docs/06)
            // instead of blending into this body's skin
            var partSkin = al.Hue.HasValue ? Palette.MadDrSkin(al.Hue.Value, o.Vigor) : o.Skin;

            // storage-vessel contents color (docs/22): RED blood / WHITE bone
            var store = o.StoreIsBone ? BoneWhite : BloodRed;
            if (sock.Mirror)
            {
                BuildPart(mb, al.Family, al.Params, 1, sock, partSkin, store);
                BuildPart(mb, al.Family, al.Params, -1, sock, partSkin, store);
            }
            else
            {
                BuildPart(mb, al.Family, al.Params, 1, sock, partSkin, store);
            }
        }

        private static void BuildPart(Builder mb, string family, double[] pg, double side, Sock sock, Col skin, Col store)
        {
            var len = P(pg, 0, 0.5);
            var girth = P(pg, 1, 0.5);
            var taper = P(pg, 2, 0.5);
            var curl = P(pg, 3, 0.5);
            var count = P(pg, 4, 0.5);
            var orn = P(pg, 5, 0.5);
            var s = new Vec3(side * sock.P.X, sock.P.Y, sock.P.Z);
            var scale = sock.Tiny ? 0.62 : 1.0;
            var capLen = sock.ArmCapLen;
            // the rig: parts leave the body along the surface normal at the
            // socket, so nothing buries into a chest on extreme morphs
            var n = new Vec3(side * sock.Nrm.X, sock.Nrm.Y, sock.Nrm.Z).Norm();

            switch (family)
            {
                // ---- hands ----
                case "claw_hand":
                {
                    var armR = (0.42 + 0.4 * girth) * scale;
                    var wrist = ArmDrop(mb, s, side, armR, scale, skin, pg, n, capLen);
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
                    var wrist = ArmDrop(mb, s, side, armR, scale, skin, pg, n, capLen);
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
                    var wrist = ArmDrop(mb, s, side, 0.42 * scale, scale, skin, pg, n, capLen);
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
                    var wrist = ArmDrop(mb, s, side, 0.5 * scale, scale, Palette.CHITIN, pg, n, capLen);
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
                    var wrist = ArmDrop(mb, s, side, 0.4 * scale, scale, skin, pg, n, capLen);
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
                    var wrist = ArmDrop(mb, s, side, 0.46 * scale, scale, Palette.CHITIN, pg, n, capLen);
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
                    var wrist = ArmDrop(mb, s, side, 0.44 * scale, scale, Palette.CHITIN, pg, n, capLen);
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
                    var wrist = ArmDrop(mb, s, side, 0.5 * scale, scale, Palette.CHITIN, pg, n, capLen);
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
                case "lamprey_maw":
                {
                    // the harvest suction tool (docs/22): a fleshy hose-arm
                    // ending in a round rasping sucker mouth ringed with teeth
                    var baseR = (0.42 + 0.4 * girth) * scale;
                    var hoseL = (2.0 + 1.8 * len) * scale;
                    var path = new List<Vec3>();
                    for (var i = 0; i <= 8; i++)
                    {
                        var t = (double)i / 8;
                        var exit = baseR * 1.5 * Math.Pow(1 - t, 1.5);
                        path.Add(new Vec3(
                            s.X + n.X * exit + side * 0.35 * t,
                            s.Y + n.Y * exit - t * hoseL + Math.Sin(t * Math.PI) * 0.25,
                            s.Z + n.Z * exit + 0.7 * t + curl * Math.Sin(t * Math.PI * 1.3) * 0.6));
                    }
                    Prims.LimbJoint(mb, path[0], path[1] - path[0], baseR);
                    var hr = new double[9];
                    for (var i = 0; i <= 8; i++) hr[i] = baseR * (1 - (double)i / 8 * 0.25);
                    Prims.Tube(mb, path, hr, skin, 0.3, 0, 9, 3);
                    var mouth = path[8];
                    var mR = baseR * 1.55;
                    Prims.Ellipsoid(mb, mouth, new Vec3(mR, mR * 0.55, mR), skin, 0.3, 0, 9);
                    Prims.Ellipsoid(mb, new Vec3(mouth.X, mouth.Y - mR * 0.28, mouth.Z),
                        new Vec3(mR * 0.62, mR * 0.3, mR * 0.62), Palette.MOUTHC, 0.2, 0, 8);
                    var rows = (int)Clamp(1 + Math.Round(count * 2), 1, 3);
                    for (var r = 0; r < rows; r++)
                    {
                        var rr = mR * (0.85 - r * 0.22);
                        var nT = 8 - r * 2;
                        for (var i = 0; i < nT; i++)
                        {
                            var a = (double)i / nT * Math.PI * 2;
                            Prims.CurvedCone(mb,
                                new Vec3(mouth.X + Math.Cos(a) * rr, mouth.Y - mR * 0.18 - r * 0.08, mouth.Z + Math.Sin(a) * rr),
                                new Vec3(-Math.Cos(a) * 0.4, -0.9, -Math.Sin(a) * 0.4),
                                0.28 + 0.1 * girth, 0.08, new Vec3(0, 0, 0), Palette.CLAW, 0.4);
                        }
                    }
                    break;
                }
                case "bone_saw":
                {
                    // the harvest saw tool (docs/22): a motor housing driving
                    // a round surgical saw blade on an articulated boom
                    var wrist = ArmDrop(mb, s, side, 0.42 * scale, scale, skin, pg, n, capLen);
                    Prims.Ellipsoid(mb, wrist, new Vec3(0.45, 0.42, 0.55), Palette.METAL, 0.7, 0, 8);
                    var boomLen = (1.2 + 1.2 * len) * scale;
                    var hub = new Vec3(wrist.X, wrist.Y + boomLen * 0.1, wrist.Z + boomLen);
                    Prims.Tube(mb, new[] { wrist, hub }, new[] { 0.15, 0.12 }, Palette.METDK, 0.75, 0, 8);
                    var bladeR = (0.7 + 0.55 * girth) * scale;
                    Prims.Ellipsoid(mb, hub, new Vec3(0.06, bladeR, bladeR), Palette.METAL, 0.85, 0, 12);
                    Prims.Ellipsoid(mb, hub, new Vec3(0.09, bladeR * 0.3, bladeR * 0.3), Palette.METDK, 0.6, 0, 8);
                    for (var i = 0; i < 10; i++)
                    {
                        var a = (double)i / 10 * Math.PI * 2;
                        Prims.Ellipsoid(mb,
                            new Vec3(hub.X, hub.Y + Math.Cos(a) * bladeR, hub.Z + Math.Sin(a) * bladeR),
                            new Vec3(0.05, 0.14, 0.14), Palette.METDK, 0.7, 0, 4);
                    }
                    break;
                }
                case "ichor_siphon":
                {
                    // the biotech harvest siphon (docs/22): a fleshy arm
                    // splitting into siphon tubes that drink from wounds
                    var wrist = ArmDrop(mb, s, side, 0.44 * scale, scale, Palette.CHITIN, pg, n, capLen);
                    Prims.Ellipsoid(mb, wrist, new Vec3(0.5, 0.45, 0.5), Palette.CHITIN, 0.4, 0, 8);
                    var nTubes = (int)Clamp(2 + Math.Round(count * 2), 2, 4);
                    var tubeL = (1.5 + 1.4 * len) * scale;
                    for (var i = 0; i < nTubes; i++)
                    {
                        var a = ((double)i / Math.Max(nTubes - 1, 1) - 0.5) * 1.2;
                        var tip = new Vec3(wrist.X + Math.Sin(a) * 0.8 * scale, wrist.Y - tubeL * 0.8,
                            wrist.Z + tubeL * 0.55 + Math.Cos(a) * 0.25 * scale);
                        var mid = new Vec3(wrist.X + Math.Sin(a) * 0.5, wrist.Y - tubeL * 0.35, wrist.Z + tubeL * 0.4);
                        Prims.Tube(mb, new[] { wrist, mid, tip },
                            new[] { 0.14 * scale, 0.1 * scale, 0.07 * scale }, Palette.ICHOR, 0.3, 0.35, 7, 2);
                        Prims.Ellipsoid(mb, tip, new Vec3(0.12, 0.12, 0.12), Palette.ICHOR, 0.4, 0.7, 5);
                    }
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
                // storage vessels seat DEAD CENTRE of the back mount, pushed
                // INTO the body so they read as part of the creature, never
                // floating (creator direction). Geometry is authored in the
                // Pack frame (across/along/out): on an upright torso it lays
                // vertically against the back; on a horizontal body (crab,
                // arachnid) it lies flat ON TOP of the shell like a real
                // backpack -- never below or near the tail.
                case "storage_bladder":
                {
                    // organic: pus-filled sacs pushed INTO the body, bulging
                    // out THROUGH the skin -- each blob half-seated, its
                    // outer dome swollen through a taut skin cap
                    var top = n.Y > 0.6;
                    var contents = store;
                    var blobR = 0.55 + 0.55 * girth;
                    var nBlob = (int)Clamp(2 + Math.Round(len * 2), 2, 3);
                    for (var i = 0; i < nBlob; i++)
                    {
                        var t = (double)i / Math.Max(nBlob - 1, 1) - 0.5;
                        var r = blobR * (0.8 + 0.35 * Math.Abs(Math.Sin(i * 1.7)));
                        // centre sunk ~45% in, so ~55% bulges out of the hide
                        var c = PackP(s, top, t * blobR * 0.9, t * blobR * 0.5, -r * 0.45);
                        Prims.Ellipsoid(mb, c, PackR(top, r, r * 0.95, r * 1.05), contents, 0.3, 0.1, 10);
                        // a taut skin membrane over the outer (bulging) face
                        Prims.Ellipsoid(mb, PackP(s, top, t * blobR * 0.9, t * blobR * 0.5, -r * 0.15),
                            PackR(top, r * 0.82, r * 0.78, r * 0.5), skin, 0.35, 0, 9);
                    }
                    break;
                }
                case "steel_tank":
                {
                    // tech: a classic single-barrel CYLINDER tank strapped
                    // to the mount -- the scuba/oxygen-tank silhouette,
                    // not a rack of parts. Mostly proud of the hide (it is
                    // strapped ON, not embedded) with a saddle collar that
                    // seats it against the body. End caps + gauge show the
                    // contents (RED blood / WHITE bone).
                    var top = n.Y > 0.6;
                    var contents = store;
                    var tR = 0.62 + 0.34 * girth;   // barrel radius
                    var tHalf = 1.05 + 0.75 * len;  // barrel half-length along the spine
                    var sink = tR * 0.18;            // strapped-on, not embedded
                    // saddle collar seats the tank against the body
                    Prims.Ellipsoid(mb, PackP(s, top, 0, 0, -sink * 2.2),
                        PackR(top, tR * 1.15, tR * 0.4, tR * 0.5), Palette.METAL, 0.5, 0, 10);
                    // the barrel itself
                    Prims.Tube(mb,
                        new[] { PackP(s, top, 0, -tHalf, -sink), PackP(s, top, 0, tHalf, -sink) },
                        new[] { tR, tR }, Palette.METAL, 0.78, 0, 14, 2);
                    // contents-coloured end caps
                    Prims.Ellipsoid(mb, PackP(s, top, 0, tHalf, -sink), PackR(top, tR * 0.95, tR * 0.35, tR * 0.95), contents, 0.5, 0.15, 10);
                    Prims.Ellipsoid(mb, PackP(s, top, 0, -tHalf, -sink), PackR(top, tR * 0.95, tR * 0.35, tR * 0.95), contents, 0.5, 0.15, 10);
                    // filler / valve cap
                    Prims.Ellipsoid(mb, PackP(s, top, 0, tHalf + 0.16, -sink), new Vec3(0.15, 0.15, 0.15), Palette.METDK, 0.6, 0, 6);
                    // a sight gauge running along the barrel, contents-coloured
                    Prims.Tube(mb, new[] { PackP(s, top, tR * 0.55, -tHalf * 0.75, -sink), PackP(s, top, tR * 0.55, tHalf * 0.75, -sink) },
                        new[] { 0.08, 0.08 }, contents, 0.4, 0.4, 6);
                    // strap rivets -- the functional-hardware read
                    foreach (var sx in Sides)
                    {
                        Prims.Ellipsoid(mb, PackP(s, top, tR * 0.85 * sx, -tHalf * 0.5, -sink * 1.6),
                            new Vec3(0.07, 0.07, 0.07), Palette.METDK, 0.8, 0, 4);
                        Prims.Ellipsoid(mb, PackP(s, top, tR * 0.85 * sx, tHalf * 0.5, -sink * 1.6),
                            new Vec3(0.07, 0.07, 0.07), Palette.METDK, 0.8, 0, 4);
                    }
                    break;
                }
                case "amber_vesicle":
                {
                    // biotech: a cluster of vesicles fused INTO the body,
                    // half-sunk and swelling out through the hide, glowing
                    // with their contents (RED blood / WHITE bone) -- the
                    // mad-doctor/alien read
                    var top = n.Y > 0.6;
                    var contents = store;
                    var nV = (int)Clamp(3 + Math.Round(count * 3), 3, 6);
                    var vR = 0.4 + 0.4 * girth;
                    for (var i = 0; i < nV; i++)
                    {
                        var t = (double)i / Math.Max(nV - 1, 1) - 0.5;
                        var r = vR * (0.7 + 0.35 * Math.Abs(Math.Sin(i * 1.3)));
                        // sunk ~40% into the body, bulging out the rest
                        var p = PackP(s, top, Math.Sin(i * 2.4) * vR * 0.7, t * vR * 2.4, -r * 0.4);
                        Prims.Ellipsoid(mb, p, PackR(top, r, r * 0.95, r * 1.05), contents, 0.25, 0.45, 8);
                    }
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
        /// normal so the limb clears the torso before gravity takes it.
        /// capLen bounds the reach for plans where a long arm would be
        /// wrong (a crab dragging its claws).</summary>
        private static Vec3 ArmDrop(Builder mb, Vec3 s, double side, double armR, double scale,
            Col armCol, double[] pg, Vec3 n, double capLen)
        {
            var len = P(pg, 0, 0.5);
            var girth = P(pg, 1, 0.5);
            var taper = P(pg, 2, 0.5);
            var curl = P(pg, 3, 0.5);
            var armLen = Math.Min((2.5 + 2.0 * len) * scale, capLen);
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

        internal static void RingStitch(Builder mb, Vec3 c, double r)
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
