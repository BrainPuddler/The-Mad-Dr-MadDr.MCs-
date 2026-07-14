using System;
using System.Collections.Generic;

namespace MadDr.CreatureMesh
{
    /// <summary>A double-precision 3-vector -- matches the JS renderer's
    /// number type so ported geometry math stays value-identical.</summary>
    public readonly struct Vec3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator *(Vec3 a, double f) { return new Vec3(a.X * f, a.Y * f, a.Z * f); }

        public double Length() { return Math.Sqrt(X * X + Y * Y + Z * Z); }

        public Vec3 Norm()
        {
            var l = Length();
            return l > 1e-9 ? new Vec3(X / l, Y / l, Z / l) : new Vec3(0, 1, 0);
        }

        public static double Dot(Vec3 a, Vec3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }
    }

    /// <summary>RGB in 0-255 like the JS palette, plus material params.</summary>
    public readonly struct Col
    {
        public readonly double R;
        public readonly double G;
        public readonly double B;

        public Col(double r, double g, double b) { R = r; G = g; B = b; }

        /// <summary>lp(a, b, t) -- linear blend, straight from the JS.</summary>
        public static Col Lp(Col a, Col b, double t)
        {
            return new Col(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }

        /// <summary>sh(c, f) -- shade/brighten with clamp, straight from the JS.</summary>
        public static Col Sh(Col c, double f)
        {
            return new Col(Math.Min(255, Math.Max(0, c.R * f)),
                Math.Min(255, Math.Max(0, c.G * f)),
                Math.Min(255, Math.Max(0, c.B * f)));
        }
    }

    /// <summary>One material bucket of triangles: everything emitted with
    /// the same (color, gloss, emissive, alpha). The Unity side makes one
    /// submesh + material per chunk -- per-vertex color gradients
    /// (skinColorFn) are deliberately flattened to their base color in
    /// this pass, trading the Lab's belly/spine tinting for zero custom
    /// shaders (URP/Lit handles everything).</summary>
    public sealed class MeshChunk
    {
        public readonly List<double> Positions = new List<double>(); // xyz triples
        public readonly List<double> Normals = new List<double>();
        public readonly List<int> Triangles = new List<int>();
        public Col Color;
        public double Gloss;
        public double Emissive;
        public double Alpha = 1.0;

        public int VertexCount { get { return Positions.Count / 3; } }
    }

    /// <summary>Chunked mesh accumulator -- the port of the JS MeshB, with
    /// the anim/gait/texture vertex channels dropped (Unity animates via
    /// transforms and the gait rig; surface tiling is a later pass).</summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, MeshChunk> _chunks = new Dictionary<string, MeshChunk>();
        private readonly List<MeshChunk> _ordered = new List<MeshChunk>();
        private double _alpha = 1.0;

        public IReadOnlyList<MeshChunk> Chunks { get { return _ordered; } }

        public void SetAlpha(double a) { _alpha = a; }

        private MeshChunk ChunkFor(Col col, double gloss, double emis)
        {
            var key = ((int)col.R) + "," + ((int)col.G) + "," + ((int)col.B) + ","
                + Math.Round(gloss, 2) + "," + Math.Round(emis, 2) + "," + Math.Round(_alpha, 2);
            MeshChunk? c;
            if (!_chunks.TryGetValue(key, out c))
            {
                c = new MeshChunk { Color = col, Gloss = gloss, Emissive = emis, Alpha = _alpha };
                _chunks[key] = c;
                _ordered.Add(c);
            }
            return c;
        }

        /// <summary>Adds a vertex to the chunk for this material; returns
        /// its index within that chunk. Callers batch a whole primitive
        /// into one chunk (same material throughout).</summary>
        public int Vert(MeshChunk chunk, Vec3 p, Vec3 n)
        {
            chunk.Positions.Add(p.X); chunk.Positions.Add(p.Y); chunk.Positions.Add(p.Z);
            var nn = n.Norm();
            chunk.Normals.Add(nn.X); chunk.Normals.Add(nn.Y); chunk.Normals.Add(nn.Z);
            return chunk.VertexCount - 1;
        }

        public MeshChunk Begin(Col col, double gloss, double emis) { return ChunkFor(col, gloss, emis); }

        public void Tri(MeshChunk c, int a, int b, int d) { c.Triangles.Add(a); c.Triangles.Add(b); c.Triangles.Add(d); }
        public void Quad(MeshChunk c, int a, int b, int d, int e)
        {
            c.Triangles.Add(a); c.Triangles.Add(b); c.Triangles.Add(d);
            c.Triangles.Add(a); c.Triangles.Add(d); c.Triangles.Add(e);
        }

        /// <summary>Safety net for Unity's single-sided rendering: the Lab
        /// shader is two-sided, so a winding slip there shows as a lighting
        /// quirk at worst; in URP it culls the face entirely. Flip any
        /// triangle whose geometric winding disagrees with its (correct by
        /// construction) analytic vertex normals.</summary>
        public void FixWinding()
        {
            foreach (var c in _ordered)
            {
                var pos = c.Positions;
                var nrm = c.Normals;
                var tri = c.Triangles;
                for (var t = 0; t < tri.Count; t += 3)
                {
                    int i0 = tri[t] * 3, i1 = tri[t + 1] * 3, i2 = tri[t + 2] * 3;
                    var p0 = new Vec3(pos[i0], pos[i0 + 1], pos[i0 + 2]);
                    var e1 = new Vec3(pos[i1] - p0.X, pos[i1 + 1] - p0.Y, pos[i1 + 2] - p0.Z);
                    var e2 = new Vec3(pos[i2] - p0.X, pos[i2 + 1] - p0.Y, pos[i2 + 2] - p0.Z);
                    var g = Vec3.Cross(e1, e2);
                    var n = new Vec3(
                        nrm[i0] + nrm[i1] + nrm[i2],
                        nrm[i0 + 1] + nrm[i1 + 1] + nrm[i2 + 1],
                        nrm[i0 + 2] + nrm[i1 + 2] + nrm[i2 + 2]);
                    if (Vec3.Dot(g, n) < 0)
                    {
                        var tmp = tri[t + 1];
                        tri[t + 1] = tri[t + 2];
                        tri[t + 2] = tmp;
                    }
                }
            }
        }
    }

    /// <summary>The Lab's geometry primitives, ported 1:1 from
    /// site/creature-renderer.js (fixed full detail -- the _detail LOD
    /// dial can come later with the perf pass).</summary>
    public static class Prims
    {
        public static void Ellipsoid(Builder mb, Vec3 c, Vec3 r, Col col, double gloss = 0.25, double emis = 0, int seg = 14)
        {
            var chunk = mb.Begin(col, gloss, emis);
            var la = seg;
            var lo = (int)Math.Round(seg * 1.6);
            var rows = new int[la + 1][];
            for (var i = 0; i <= la; i++)
            {
                var th = (double)i / la * Math.PI;
                var sy = Math.Cos(th);
                var sr = Math.Sin(th);
                rows[i] = new int[lo + 1];
                for (var j = 0; j <= lo; j++)
                {
                    var ph = (double)j / lo * Math.PI * 2;
                    var u = new Vec3(sr * Math.Cos(ph), sy, sr * Math.Sin(ph));
                    var p = new Vec3(c.X + u.X * r.X, c.Y + u.Y * r.Y, c.Z + u.Z * r.Z);
                    var n = new Vec3(u.X / r.X, u.Y / r.Y, u.Z / r.Z);
                    rows[i][j] = mb.Vert(chunk, p, n);
                }
            }
            for (var i = 0; i < la; i++)
                for (var j = 0; j < lo; j++)
                    mb.Quad(chunk, rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]);
        }

        /// <summary>Swept tube along a path with per-point radii;
        /// parallel-transport frames, capped ends (caps bit 1 = start,
        /// bit 2 = end) -- the JS tube() verbatim minus anim channels.</summary>
        public static void Tube(Builder mb, IReadOnlyList<Vec3> path, IReadOnlyList<double> radii,
            Col col, double gloss = 0.25, double emis = 0, int sides = 10, int caps = 3)
        {
            var chunk = mb.Begin(col, gloss, emis);

            // sanitize path: drop zero-length steps, floor radii
            var pts = new List<Vec3> { path[0] };
            for (var i = 1; i < path.Count; i++)
                if ((path[i] - pts[pts.Count - 1]).Length() > 1e-4) pts.Add(path[i]);
            if (pts.Count < 2) return;
            var rr = new double[pts.Count];
            for (var i = 0; i < pts.Count; i++)
                rr[i] = Math.Max(0.03, radii[Math.Min(i, radii.Count - 1)]);

            var tangents = new Vec3[pts.Count];
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[Math.Max(0, i - 1)];
                var b = pts[Math.Min(pts.Count - 1, i + 1)];
                tangents[i] = (b - a).Norm();
            }
            var n0 = Math.Abs(tangents[0].Y) < 0.9
                ? Vec3.Cross(new Vec3(0, 1, 0), tangents[0]).Norm()
                : Vec3.Cross(new Vec3(1, 0, 0), tangents[0]).Norm();

            var rows = new int[pts.Count][];
            var normal = n0;
            var dirs = new Vec3[pts.Count][];
            for (var i = 0; i < pts.Count; i++)
            {
                normal = (normal - tangents[i] * Vec3.Dot(normal, tangents[i])).Norm(); // transport
                var b = Vec3.Cross(tangents[i], normal);
                rows[i] = new int[sides + 1];
                dirs[i] = new Vec3[sides + 1];
                for (var j = 0; j <= sides; j++)
                {
                    var ph = (double)j / sides * Math.PI * 2;
                    var dir = normal * Math.Cos(ph) + b * Math.Sin(ph);
                    dirs[i][j] = dir;
                    rows[i][j] = mb.Vert(chunk, pts[i] + dir * rr[i], dir);
                }
            }
            for (var i = 0; i < pts.Count - 1; i++)
                for (var j = 0; j < sides; j++)
                    mb.Quad(chunk, rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]);

            if ((caps & 1) != 0)
            {
                var cv = mb.Vert(chunk, pts[0], tangents[0] * -1);
                for (var j = 0; j < sides; j++) mb.Tri(chunk, cv, rows[0][j + 1], rows[0][j]);
            }
            if ((caps & 2) != 0)
            {
                var k = pts.Count - 1;
                var cv = mb.Vert(chunk, pts[k], tangents[k]);
                for (var j = 0; j < sides; j++) mb.Tri(chunk, cv, rows[k][j], rows[k][j + 1]);
            }
        }

        /// <summary>Torus (hoop) centred at `center`, hole along `axis` --
        /// the geometry of a brace clamped AROUND a limb.</summary>
        public static void Torus(Builder mb, Vec3 center, Vec3 axis, double majorR, double minorR,
            Col col, double gloss = 0.4, double emis = 0, int nMaj = 14, int nMin = 8)
        {
            var chunk = mb.Begin(col, gloss, emis);
            var a = axis.Norm();
            var n = Math.Abs(a.Y) < 0.9
                ? Vec3.Cross(new Vec3(0, 1, 0), a).Norm()
                : Vec3.Cross(new Vec3(1, 0, 0), a).Norm();
            var b = Vec3.Cross(a, n);
            var rows = new int[nMaj + 1][];
            for (var i = 0; i <= nMaj; i++)
            {
                var th = (double)i / nMaj * Math.PI * 2;
                var radial = n * Math.Cos(th) + b * Math.Sin(th);
                var ringC = center + radial * majorR;
                rows[i] = new int[nMin + 1];
                for (var j = 0; j <= nMin; j++)
                {
                    var ph = (double)j / nMin * Math.PI * 2;
                    var dir = radial * Math.Cos(ph) + a * Math.Sin(ph);
                    rows[i][j] = mb.Vert(chunk, ringC + dir * minorR, dir);
                }
            }
            for (var i = 0; i < nMaj; i++)
                for (var j = 0; j < nMin; j++)
                    mb.Quad(chunk, rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]);
        }

        public struct LatheLevel
        {
            public double Y;
            public double X;
            public double Z;
            public double Rx;
            public double Rz;

            public LatheLevel(double y, double x, double z, double rx, double rz)
            {
                Y = y; X = x; Z = z; Rx = rx; Rz = rz;
            }
        }

        /// <summary>Surface of revolution with elliptical cross-sections --
        /// the generic body builder. Levels run bottom-to-top; normals lean
        /// with the profile slope; ends capped. This is what buys
        /// silhouette variety (pear/barrel/gorilla are just radius
        /// profiles through the same machine).</summary>
        public static void Lathe(Builder mb, IReadOnlyList<LatheLevel> levels, Col col,
            double gloss = 0.28, double emis = 0, int seg = 16)
        {
            var chunk = mb.Begin(col, gloss, emis);
            var count = levels.Count;
            var rows = new int[count][];
            for (var i = 0; i < count; i++)
            {
                var lv = levels[i];
                var lo2 = levels[Math.Max(0, i - 1)];
                var hi = levels[Math.Min(count - 1, i + 1)];
                var slope = ((lo2.Rx + lo2.Rz) - (hi.Rx + hi.Rz)) / (2 * Math.Max(0.2, hi.Y - lo2.Y));
                rows[i] = new int[seg + 1];
                for (var j = 0; j <= seg; j++)
                {
                    var ph = (double)j / seg * Math.PI * 2;
                    var cx = Math.Cos(ph);
                    var sz = Math.Sin(ph);
                    var n = new Vec3(cx, slope * 0.6, sz).Norm();
                    rows[i][j] = mb.Vert(chunk,
                        new Vec3(lv.X + cx * lv.Rx, lv.Y, lv.Z + sz * lv.Rz), n);
                }
            }
            // rows run bottom-to-top (the reverse of Ellipsoid's
            // top-to-bottom), so quads are emitted in reverse order to keep
            // outward faces front-facing -- same fix as the JS lathe()
            for (var i = 0; i < count - 1; i++)
                for (var j = 0; j < seg; j++)
                    mb.Quad(chunk, rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1], rows[i][j]);

            // caps
            var bottom = levels[0];
            var bv = mb.Vert(chunk, new Vec3(bottom.X, bottom.Y, bottom.Z), new Vec3(0, -1, 0));
            for (var j = 0; j < seg; j++) mb.Tri(chunk, bv, rows[0][j], rows[0][j + 1]);
            var top = levels[count - 1];
            var tv = mb.Vert(chunk, new Vec3(top.X, top.Y, top.Z), new Vec3(0, 1, 0));
            for (var j = 0; j < seg; j++) mb.Tri(chunk, tv, rows[count - 1][j + 1], rows[count - 1][j]);
        }

        /// <summary>Curved horn/claw: a cone bending toward `bend`.</summary>
        public static void CurvedCone(Builder mb, Vec3 baseP, Vec3 dir, double length, double baseR,
            Vec3 bend, Col col, double gloss = 0.3, double emis = 0)
        {
            var d = dir.Norm();
            var path = new List<Vec3>();
            for (var i = 0; i <= 4; i++)
            {
                var t = (double)i / 4;
                path.Add(baseP + d * (length * t) + bend * (t * t));
            }
            Tube(mb, path, new[] { baseR, baseR * 0.82, baseR * 0.6, baseR * 0.34, 0.04 }, col, gloss, emis, 8, 3);
        }

        /// <summary>Mad-science joint hardware: iron ball seated at the
        /// mount, a rod out along the limb axis, and a brass torus brace
        /// wrapped around the limb's shaft studded with six bolts.</summary>
        public static void LimbJoint(Builder mb, Vec3 rootAt, Vec3 dir, double limbR)
        {
            var d = dir.Norm();
            var r = Math.Max(0.14, limbR);
            var braceC = rootAt + d * (r * 0.45);
            var ballAt = rootAt - d * (r * 0.18);
            Ellipsoid(mb, ballAt, new Vec3(r * 0.26, r * 0.26, r * 0.26), Palette.IRON, 0.85, 0, 8);
            Tube(mb, new[] { ballAt, rootAt + d * (r * 0.95) }, new[] { r * 0.11, r * 0.11 }, Palette.IRON, 0.85, 0, 7);
            var majorR = r * 1.0;
            var minorR = r * 0.38;
            Torus(mb, braceC, d, majorR, minorR, Palette.BRASS, 0.85, 0, 14, 8);
            var n = Math.Abs(d.Y) < 0.9
                ? Vec3.Cross(new Vec3(0, 1, 0), d).Norm()
                : Vec3.Cross(new Vec3(1, 0, 0), d).Norm();
            var b = Vec3.Cross(d, n);
            for (var i = 0; i < 6; i++)
            {
                var th = (double)i / 6 * Math.PI * 2;
                var radial = n * Math.Cos(th) + b * Math.Sin(th);
                Ellipsoid(mb, braceC + radial * (majorR + minorR * 0.6),
                    new Vec3(minorR * 0.55, minorR * 0.55, minorR * 0.55), Palette.IRON, 0.8, 0, 5);
            }
        }

        /// <summary>Outward surface normal of an ellipsoid at point q --
        /// the analytic gradient. Socket frames use this so parts grow
        /// along the skin.</summary>
        public static Vec3 EllipN(Vec3 q, Vec3 c, Vec3 r)
        {
            return new Vec3(
                (q.X - c.X) / (r.X * r.X),
                (q.Y - c.Y) / (r.Y * r.Y),
                (q.Z - c.Z) / (r.Z * r.Z)).Norm();
        }
    }

    /// <summary>The Lab's palette, verbatim (0-255 RGB).</summary>
    public static class Palette
    {
        public static readonly Col PALLOR = new Col(192, 172, 152);
        public static readonly Col BONE = new Col(212, 200, 170);
        public static readonly Col BONDK = new Col(158, 148, 118);
        public static readonly Col METAL = new Col(116, 130, 144);
        public static readonly Col METDK = new Col(62, 74, 86);
        public static readonly Col GLOW = new Col(255, 150, 30);
        public static readonly Col CHITIN = new Col(52, 96, 64);
        public static readonly Col EYEWH = new Col(235, 235, 220);
        public static readonly Col PUPIL = new Col(16, 10, 22);
        public static readonly Col HOOF = new Col(52, 44, 34);
        public static readonly Col CLAW = new Col(196, 184, 152);
        public static readonly Col BOLT = new Col(96, 108, 122);
        public static readonly Col BLTGLO = new Col(255, 205, 50);
        public static readonly Col ICHOR = new Col(150, 85, 230);
        public static readonly Col STITCH = new Col(46, 26, 20);
        public static readonly Col MOUTHC = new Col(34, 16, 26);
        public static readonly Col BRASS = new Col(186, 146, 70);
        public static readonly Col IRON = new Col(76, 80, 90);
        public static readonly Col BRAINC = new Col(214, 150, 160);
        public static readonly Col GLASS = new Col(200, 228, 224);
        public static readonly Col LASER_N = new Col(130, 220, 255);
        public static readonly Col PHOTON_N = new Col(255, 235, 175);
        public static readonly Col ICHOR_N = new Col(150, 235, 190);
        public static readonly Col TONGUE = new Col(198, 62, 92);
        // planBlob's visible organs
        public static readonly Col HEARTC_L = new Col(205, 35, 50);   // oxygenated left
        public static readonly Col HEARTC_R = new Col(150, 45, 62);   // darker deoxygenated right
        public static readonly Col STOMACHC = new Col(214, 172, 92);
        public static readonly Col GUTC = new Col(188, 116, 132);

        private static readonly Col[] SkinAnchors =
        {
            new Col(92, 138, 74),    // bog green
            new Col(148, 152, 66),   // olive
            new Col(195, 118, 78),   // classic flesh
            new Col(124, 134, 152),  // cadaver grey-blue
            new Col(142, 92, 168),   // mutant violet
            new Col(172, 70, 58),    // rust red
        };

        public static Col SkinTone(double t)
        {
            var s = Math.Min(Math.Max(t, 0), 0.999) * (SkinAnchors.Length - 1);
            var i = (int)Math.Floor(s);
            return Col.Lp(SkinAnchors[i], SkinAnchors[i + 1], s - i);
        }

        /// <summary>MadDr skin: lp(PALLOR, skinTone(hue), 0.40 + 0.60 * vigor).</summary>
        public static Col MadDrSkin(double hue, double vigor)
        {
            return Col.Lp(PALLOR, SkinTone(hue), 0.40 + 0.60 * vigor);
        }
    }
}
