/**
 * MadDr.MCs creature renderer — parametric 3D mesh edition.
 * No deps. Raw WebGL 1.
 *
 * The docs/08 pipeline, in miniature: creatures are assembled from
 * INTERCHANGEABLE PARAMETRIC PART MESHES (capsules, swept tubes, curved
 * cones, membranes) attached at body-plan sockets. Genome genes act as
 * MORPH TARGETS — length/girth/taper/curl/count scale the control
 * skeletons smoothly, so breeding morphs bodies continuously instead of
 * swapping blocks.
 *
 * Art direction: designer-toy takes on classic b-movie monsters — big
 * heads, squat bodies, chunky limbs, soft 3-band toon shading with a
 * vinyl sheen, warm key light + cool moon rim. The painted 320×200
 * backdrop (dithered sky, moon, castle, dais) remains, rendered as a
 * chunky-pixel billboard behind the smooth model.
 *
 * Render-safety rules (no inside-out geometry, ever):
 *  - mirrored parts are built from mirrored control skeletons, never
 *    negative scale matrices;
 *  - face culling is DISABLED and the shader flips normals on back
 *    faces, so winding can never produce holes or inverted shells;
 *  - every gene-driven dimension passes through clamps; radii have
 *    floors; tube frames use parallel transport (no twist collapse).
 *
 * Budget: ~6–14k triangles per creature, one interleaved buffer, two
 * draw calls (backdrop, creature) plus glow sprites — comfortable for
 * mid-range Android.
 */

// ── canvas geometry ─────────────────────────────────────────────────────────
const BW = 320, BH = 200;          // backdrop native pixels
const CW = 640, CH = 400;          // render target
const HORIZON = 150;
const DAIS = { x: 160, y: 178 };   // dais centre in backdrop pixels

// ── palette ─────────────────────────────────────────────────────────────────
const PALLOR  = [192, 172, 152];
const BONE    = [212, 200, 170];
const BONDK   = [158, 148, 118];
const METAL   = [116, 130, 144];
const METDK   = [ 62,  74,  86];
const GLOW    = [255, 150,  30];
const CHITIN  = [ 52,  96,  64];
const EYEWH   = [235, 235, 220];
const PUPIL   = [ 16,  10,  22];
const HOOF    = [ 52,  44,  34];
const CLAW    = [196, 184, 152];
const BOLT    = [ 96, 108, 122];
const BLTGLO  = [255, 205,  50];
const ICHOR   = [150,  85, 230];
const STITCH  = [ 46,  26,  20];
const MOUTHC  = [ 34,  16,  26];
const BRASS   = [186, 146,  70];
const IRON    = [ 76,  80,  90];
const TONGUE  = [198,  62,  92];

const SKIN_ANCHORS = [
  [ 92, 138,  74],   // bog green
  [148, 152,  66],   // olive
  [195, 118,  78],   // classic flesh
  [124, 134, 152],   // cadaver grey-blue
  [142,  92, 168],   // mutant violet
  [172,  70,  58],   // rust red
];

const lp = (a, b, t) => a.map((v, i) => v + (b[i] - v) * t);
const sh = (c, f)    => c.map(v => Math.min(255, Math.max(0, v * f)));
const clamp = (x, a, b) => x < a ? a : x > b ? b : x;

function skinTone(t) {
  const s = clamp(t, 0, 0.999) * (SKIN_ANCHORS.length - 1);
  const i = Math.floor(s);
  return lp(SKIN_ANCHORS[i], SKIN_ANCHORS[i + 1], s - i);
}

// ── tiny vector / matrix kit ────────────────────────────────────────────────
const V = {
  add: (a, b) => [a[0]+b[0], a[1]+b[1], a[2]+b[2]],
  sub: (a, b) => [a[0]-b[0], a[1]-b[1], a[2]-b[2]],
  scale: (a, s) => [a[0]*s, a[1]*s, a[2]*s],
  dot: (a, b) => a[0]*b[0] + a[1]*b[1] + a[2]*b[2],
  cross: (a, b) => [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]],
  len: (a) => Math.hypot(a[0], a[1], a[2]),
  norm: (a) => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0]/l, a[1]/l, a[2]/l]; },
};

function perspective(fovY, aspect, near, far) {
  const f = 1 / Math.tan(fovY / 2), nf = 1 / (near - far);
  return [f/aspect,0,0,0, 0,f,0,0, 0,0,(far+near)*nf,-1, 0,0,2*far*near*nf,0];
}
function lookAt(eye, at, up) {
  const z = V.norm(V.sub(eye, at));
  const x = V.norm(V.cross(up, z));
  const y = V.cross(z, x);
  return [x[0],y[0],z[0],0, x[1],y[1],z[1],0, x[2],y[2],z[2],0,
          -V.dot(x,eye), -V.dot(y,eye), -V.dot(z,eye), 1];
}
function mat4mul(a, b) {
  const o = new Array(16);
  for (let c = 0; c < 4; c++)
    for (let r = 0; r < 4; r++)
      o[c*4+r] = a[r]*b[c*4] + a[4+r]*b[c*4+1] + a[8+r]*b[c*4+2] + a[12+r]*b[c*4+3];
  return o;
}

// ── mesh builder ────────────────────────────────────────────────────────────
// Interleaved: pos(3) normal(3) colour(3, 0..1) mat(3: gloss, emissive, fx)
// anim(4) = 16 floats.
//
// The anim channel drives the idle animation in the vertex shader:
//   x — breath weight (displace along the normal with the breath cycle)
//   y — >0: wing-flap weight (traveling sine, phased by w) / <0: blink drop
//   z — sway weight (gentle x/z pendulum, tentacles & tails)
//   w — phase offset (staggers limbs; makes flap & sway waves travel)
// The mat.z (fx) channel drives facial secondary motion:
//   >0 — gaze weight: pupils drift with the saccade uniform
//   <0 — tongue weight: darts forward with the flicker uniform

const ANIM0 = [0, 0, 0, 0];

// skin-atlas tile origins (1024² atlas, 4×4 grid of 256px tiles) and the
// per-material gloss boost stored in the tile's alpha channel
const TILE = {
  warts:    [0.00, 0.00],
  scales:   [0.25, 0.00],
  slick:    [0.50, 0.00],
  feathers: [0.75, 0.00],
  chitin:   [0.00, 0.25],
  ridge:    [0.25, 0.25],
  none:     [0.50, 0.25],
};
const TILE_SPEC = { warts: 28, scales: 90, slick: 215, feathers: 18, chitin: 110, ridge: 50, none: 0 };
const TEX_NONE = [TILE.none[0], TILE.none[1], 1, 0];

class MeshB {
  constructor() {
    this.v = []; this.idx = []; this.glows = [];
    this.anim = ANIM0; this.fx = 0; this.tex = TEX_NONE;
  }
  setAnim(a) { this.anim = a; }
  setFx(f) { this.fx = f; }
  /** [tileU, tileV, tilingScale, amplitude] — which skin material, how
   * dense its grain, and how strongly it shows. */
  setTex(t) { this.tex = t; }
  vert(p, n, c, g, e) {
    const a = this.anim, t = this.tex;
    this.v.push(p[0], p[1], p[2], n[0], n[1], n[2], c[0]/255, c[1]/255, c[2]/255, g, e, this.fx,
      t[0], t[1], t[2], t[3],
      a[0], a[1], a[2], a[3]);
    return this.v.length / 20 - 1;
  }
  tri(a, b, c) { this.idx.push(a, b, c); }
  quad(a, b, c, d) { this.idx.push(a, b, c, a, c, d); }
  glow(p, c, size) { this.glows.push([p[0], p[1], p[2], c[0]/255, c[1]/255, c[2]/255, size]); }
}

/** Ellipsoid at c with radii r. colorFn(unitPos) may vary the colour;
 * animFn(unitPos) may vary the anim channel across the surface. */
function ellipsoid(mb, c, r, col, gloss = 0.25, emis = 0, seg = 14, colorFn = null, animFn = null) {
  const prevAnim = mb.anim;
  const la = seg, lo = Math.round(seg * 1.6);
  const rows = [];
  for (let i = 0; i <= la; i++) {
    const th = (i / la) * Math.PI;
    const sy = Math.cos(th), sr = Math.sin(th);
    const row = [];
    for (let j = 0; j <= lo; j++) {
      const ph = (j / lo) * Math.PI * 2;
      const u = [sr * Math.cos(ph), sy, sr * Math.sin(ph)];
      const p = [c[0] + u[0]*r[0], c[1] + u[1]*r[1], c[2] + u[2]*r[2]];
      const n = V.norm([u[0]/r[0], u[1]/r[1], u[2]/r[2]]);
      if (animFn) mb.setAnim(animFn(u));
      row.push(mb.vert(p, n, colorFn ? colorFn(u) : col, gloss, emis));
    }
    rows.push(row);
  }
  for (let i = 0; i < la; i++)
    for (let j = 0; j < lo; j++)
      mb.quad(rows[i][j], rows[i][j+1], rows[i+1][j+1], rows[i+1][j]);
  mb.setAnim(prevAnim);
}

/** Swept tube along path with per-point radii; parallel-transport frames.
 * animFn(t) may vary the anim channel along the path (traveling sway). */
function tube(mb, path, radii, col, gloss = 0.25, emis = 0, sides = 10, caps = 3, colorFn = null, animFn = null) {
  const prevAnim = mb.anim;
  // sanitize path: drop zero-length steps, floor radii
  const P = [path[0]];
  for (let i = 1; i < path.length; i++)
    if (V.len(V.sub(path[i], P[P.length-1])) > 1e-4) P.push(path[i]);
  if (P.length < 2) return;
  const R = P.map((_, i) => Math.max(0.03, radii[Math.min(i, radii.length - 1)]));

  const T = P.map((p, i) => {
    const a = P[Math.max(0, i-1)], b = P[Math.min(P.length-1, i+1)];
    return V.norm(V.sub(b, a));
  });
  let n = Math.abs(T[0][1]) < 0.9 ? V.norm(V.cross([0,1,0], T[0])) : V.norm(V.cross([1,0,0], T[0]));

  const rows = [];
  for (let i = 0; i < P.length; i++) {
    const t = i / (P.length - 1);
    if (animFn) mb.setAnim(animFn(t));
    n = V.norm(V.sub(n, V.scale(T[i], V.dot(n, T[i]))));   // transport
    const b = V.cross(T[i], n);
    const row = [];
    for (let j = 0; j <= sides; j++) {
      const ph = (j / sides) * Math.PI * 2;
      const dir = V.add(V.scale(n, Math.cos(ph)), V.scale(b, Math.sin(ph)));
      const cc = colorFn ? colorFn(t) : col;
      row.push(mb.vert(V.add(P[i], V.scale(dir, R[i])), dir, cc, gloss, emis));
    }
    rows.push(row);
  }
  for (let i = 0; i < P.length - 1; i++)
    for (let j = 0; j < sides; j++)
      mb.quad(rows[i][j], rows[i][j+1], rows[i+1][j+1], rows[i+1][j]);

  // caps (bit 1 = start, bit 2 = end)
  for (const [on, k, dirSign] of [[caps & 1, 0, -1], [caps & 2, P.length - 1, 1]]) {
    if (!on) continue;
    if (animFn) mb.setAnim(animFn(k ? 1 : 0));
    const nrm = V.scale(T[k], dirSign);
    const cv = mb.vert(P[k], nrm, colorFn ? colorFn(k ? 1 : 0) : col, gloss, emis);
    for (let j = 0; j < sides; j++)
      dirSign > 0 ? mb.tri(cv, rows[k][j], rows[k][j+1]) : mb.tri(cv, rows[k][j+1], rows[k][j]);
  }
  mb.setAnim(prevAnim);
}

/** A torus (hoop) centred at `center`, hole aligned to `axis` — the
 * actual geometry of a brace clamped AROUND a limb: the limb passes
 * through the hole, the band wraps the full 360° around it. This is
 * the 90°-rotated sweep from a plain tube: instead of translating a
 * ring along the axis (a solid capped puck sitting beside the limb),
 * the small cross-section revolves around a big circle perpendicular
 * to the axis, so the surface genuinely encircles the shaft. */
function torus(mb, center, axis, majorR, minorR, col, gloss = 0.4, emis = 0, nMaj = 14, nMin = 8) {
  const a = V.norm(axis);
  let n = Math.abs(a[1]) < 0.9 ? V.norm(V.cross([0, 1, 0], a)) : V.norm(V.cross([1, 0, 0], a));
  const b = V.cross(a, n);
  const rows = [];
  for (let i = 0; i <= nMaj; i++) {
    const th = (i / nMaj) * Math.PI * 2;
    const radial = V.add(V.scale(n, Math.cos(th)), V.scale(b, Math.sin(th)));
    const ringC = V.add(center, V.scale(radial, majorR));
    const row = [];
    for (let j = 0; j <= nMin; j++) {
      const ph = (j / nMin) * Math.PI * 2;
      const dir = V.add(V.scale(radial, Math.cos(ph)), V.scale(a, Math.sin(ph)));
      row.push(mb.vert(V.add(ringC, V.scale(dir, minorR)), dir, col, gloss, emis));
    }
    rows.push(row);
  }
  for (let i = 0; i < nMaj; i++)
    for (let j = 0; j < nMin; j++)
      mb.quad(rows[i][j], rows[i][j+1], rows[i+1][j+1], rows[i+1][j]);
}

/** Mad-science joint hardware. The linkage, body outward:
 *  1. an IRON BALL seated in the body at the mount point,
 *  2. a rod running out along the limb's own axis,
 *  3. a BRASS BRACE — a true hoop wrapped around the limb's shaft,
 *     the limb passing through its hole — at the limb's angle, studded
 *     with six radial iron bolts.
 * `limbR` is the limb's actual radius at the joint, so the hardware
 * scales with the limb it holds. */
function limbJoint(mb, rootAt, dir, limbR) {
  // `rootAt` is the limb's exact first path point and `dir` its exact
  // first-segment direction — callers pass the real skeleton values, so
  // the hoop's angle always matches the limb and sits right at its end.
  const prevTex = mb.tex;
  mb.setTex(TEX_NONE);
  const d = V.norm(dir);
  const R = Math.max(0.14, limbR);
  const braceC = V.add(rootAt, V.scale(d, R * 0.45));
  const ballAt = V.sub(rootAt, V.scale(d, R * 0.18));
  // small ball just inside the mount — bigger than the rod, nothing more
  ellipsoid(mb, ballAt, [R*0.26, R*0.26, R*0.26], IRON, 0.85, 0, 8);
  // stub of a rod, narrow, bridging ball through the brace
  tube(mb, [ballAt, V.add(rootAt, V.scale(d, R * 0.95))], [R*0.11, R*0.11], IRON, 0.85, 0, 7);
  // brass brace: a hoop wrapped around the limb, hole on the limb's axis
  const majorR = R * 1.0, minorR = R * 0.38;
  torus(mb, braceC, d, majorR, minorR, BRASS, 0.85, 0, 14, 8);
  // radial bolts studding the band's outer rim
  let n = Math.abs(d[1]) < 0.9 ? V.norm(V.cross([0, 1, 0], d)) : V.norm(V.cross([1, 0, 0], d));
  const b = V.cross(d, n);
  for (let i = 0; i < 6; i++) {
    const th = (i / 6) * Math.PI * 2;
    const radial = V.add(V.scale(n, Math.cos(th)), V.scale(b, Math.sin(th)));
    ellipsoid(mb, V.add(braceC, V.scale(radial, majorR + minorR * 0.6)),
      [minorR*0.55, minorR*0.55, minorR*0.55], IRON, 0.8, 0, 5);
  }
  mb.setTex(prevTex);
}

/** Curved horn/claw: cone bending toward `bend` (a world-space offset). */
function curvedCone(mb, base, dir, length, baseR, bend, col, gloss = 0.3, emis = 0) {
  const d = V.norm(dir);
  const path = [];
  for (let i = 0; i <= 4; i++) {
    const t = i / 4;
    path.push(V.add(V.add(base, V.scale(d, length * t)), V.scale(bend, t * t)));
  }
  tube(mb, path, [baseR, baseR*0.82, baseR*0.6, baseR*0.34, 0.04], col, gloss, emis, 8, 3);
}

// ── creature assembly ───────────────────────────────────────────────────────
// Units: world; feet on y=0; camera frames roughly 0..13 units of height.

const SLOT_NAMES = ['hand', 'sensor', 'eye', 'leg'];

function skinColorFn(skin, belly, spine) {
  // pale belly toward +z, dusky spine toward -z, darker underside
  return (u) => {
    let c = skin;
    if (u[2] > 0.15) c = lp(c, belly, (u[2] - 0.15) * 0.45);
    if (u[2] < -0.2) c = lp(c, spine, (-u[2] - 0.2) * 0.35);
    return sh(c, 0.86 + 0.14 * clamp(u[1] * 0.5 + 0.5, 0, 1));
  };
}

function buildCreature(genome) {
  const mb = new MeshB();
  const g = genome ?? {};
  const P = (arr, i, d) => (arr && typeof arr[i] === 'number' ? arr[i] : d);

  const vigor = P(g.heart?.params, 0, 0.5);
  const hue   = P(g.body?.params, 0, 0.5);
  const bulk  = P(g.body?.params, 1, 0.5);
  const limb  = P(g.body?.params, 2, 0.5);
  const tail  = P(g.body?.params, 3, 0.5);
  const skin  = lp(PALLOR, skinTone(hue), 0.40 + 0.60 * vigor);
  const belly = lp(skin, [236, 214, 184], 0.55);
  const spine = lp(skin, [52, 40, 80], 0.45);
  const skinFn = skinColorFn(skin, belly, spine);
  const headScale = { dim: 0, average: 0.15, gifted: 0.3, mastermind: 0.75 }[g.brain?.tier] ?? 0.15;
  const heartLevel = ['faint','steady','strong','titan'].indexOf(g.heart?.tier ?? 'steady');

  const slots = g.slots ?? {};
  const plan = g.body?.plan ?? 'tetrapod';

  // leg genes set stance height (stumps slump low)
  // real leg length: bipeds stand on legs, not casters
  const legAl = slots.leg;
  const legLen = legAl && !plan.match(/blob|serpentine/)
    ? (legAl.family === 'leg_stump' ? 0.6
      : legAl.family === 'insect_leg' ? clamp(1.25 + 1.0 * P(legAl.params, 0, 0.5), 1.25, 2.25)
      : legAl.family === 'piston_leg' ? clamp(1.8 + 1.0 * P(legAl.params, 0, 0.5), 1.8, 2.8)
      : clamp(2.4 + 1.2 * P(legAl.params, 0, 0.5), 2.4, 3.6))
    : 0;
  const legFam = legAl?.family ?? null;

  const builders = { tetrapod: planTetrapod, blob: planBlob, serpentine: planSerpentine, winged: planWinged };
  // hide grain: an unused heart axis picks how fine or coarse the skin
  // texture runs — a heritable detail-density gene
  const grain = P(g.heart?.params, 3, 0.5);

  const sockets = (builders[plan] ?? planTetrapod)(mb, {
    bulk, limb, tail, skin, skinFn, headScale, heartLevel, legLen, legFam,
    brainTier: g.brain?.tier ?? 'average',
    texScale: 0.35 + 0.5 * grain,
  });

  for (const slot of SLOT_NAMES) {
    const al = slots[slot];
    if (!al || !sockets[slot]) continue;
    // plans that ignore a slot render nothing there (silent genes)
    if ((plan === 'blob' || plan === 'serpentine') && slot === 'leg') continue;
    // dormant organic head sensors: low ornament gene → bald head
    if (slot === 'sensor' && (al.family === 'antenna' || al.family === 'horn') &&
        P(al.params, 5, 0.5) < 0.35) continue;

    const sock = sockets[slot];
    const sides = sock.mirror ? [1, -1] : [1];
    for (const side of sides)
      buildPart(mb, slot, al.family, al.params ?? [], side, sock, { skin, skinFn });
  }
  return mb;
}

// ---- body plans -------------------------------------------------------------

function frankenDetails(mb, headC, headR, heartLevel, o) {
  // heavy brow ridge — the shelf that turns glued-on balls into deep-set
  // eyes and does most of the b-movie scowl on its own
  const by = headC[1] + headR[1] * 0.40, bz = headC[2] + headR[2] * 0.70;
  const bw = 0.14 + headR[0] * 0.07;
  tube(mb, [
    [headC[0] - headR[0]*0.85, by - 0.12, bz - headR[2]*0.22],
    [headC[0], by + 0.12, bz + headR[2]*0.18],
    [headC[0] + headR[0]*0.85, by - 0.12, bz - headR[2]*0.22],
  ], [bw, bw * 1.45, bw], sh(o.skin, 0.78), 0.25, 0, 8);

  // protruding lower jaw: mouth is a shadow line between jaw and skull,
  // underbite tusks rise from the jaw's corners
  const jC = [headC[0], headC[1] - headR[1]*0.68, headC[2] + headR[2]*0.30];
  const jR = [headR[0]*0.80, headR[1]*0.40, headR[2]*0.82];
  ellipsoid(mb, jC, jR, o.skin, 0.28, 0, 10, o.skinFn);
  const fdTex = mb.tex;
  mb.setTex(TEX_NONE);
  ellipsoid(mb, [jC[0], jC[1] + jR[1]*0.55, jC[2] + jR[2]*0.45],
    [jR[0]*0.82, 0.13, jR[2]*0.5], MOUTHC, 0.15, 0, 8);
  for (const s of [-1, 1])
    curvedCone(mb, [jC[0] + s*jR[0]*0.60, jC[1] + jR[1]*0.30, jC[2] + jR[2]*0.66],
      [0, 1, 0.15], 0.5 + headR[0]*0.12, 0.13, [0, 0, 0.06], CLAW, 0.6);

  // neck bolts by heart tier; titan bolts glow
  if (heartLevel >= 1) {
    const glow = heartLevel >= 3;
    const rows = heartLevel >= 2 ? [-0.52, -0.78] : [-0.62];
    for (const fy of rows.slice(0, heartLevel >= 2 ? 2 : 1))
      for (const s of [-1, 1]) {
        const bx = headC[0] + s * headR[0] * 0.88;
        const byy = headC[1] + headR[1] * fy;
        tube(mb, [[bx, byy, 0], [bx + s * 0.8, byy, 0]], [0.22, 0.28],
          glow ? BLTGLO : BOLT, 0.7, glow ? 0.85 : 0, 8);
        if (glow) mb.glow([bx + s * 0.9, byy, 0], BLTGLO, 24);
      }
  }
  mb.setTex(fdTex);
}

function stitchSeam(mb, y0, rx, rz, zc) {
  // zigzag suture across the chest front
  const pts = [];
  for (let i = 0; i <= 8; i++) {
    const t = i / 8;
    const x = (t - 0.5) * rx * 1.4;
    const y = y0 + ((i % 2) ? 0.32 : -0.32);
    const q = 1 - (x / rx) ** 2;
    if (q <= 0.05) continue;
    pts.push([x, y, zc + rz * Math.sqrt(q) + 0.04]);
  }
  if (pts.length > 2) tube(mb, pts, pts.map(() => 0.09), STITCH, 0.1, 0, 5, 0);
}

/** Surface of revolution with elliptical cross-sections — the generic
 * body builder. `levels` run bottom→top: {y, x, z, rx, rz}. Normals lean
 * with the profile slope, ends are capped. This is what buys silhouette
 * variety: pear, barrel, and triangular gorilla builds are just
 * different radius profiles through the same machine. */
function lathe(mb, levels, col, gloss = 0.28, emis = 0, seg = 16, colorFn = null) {
  const L = levels.length;
  const y0 = levels[0].y, y1 = levels[L-1].y;
  const rows = [];
  for (let i = 0; i < L; i++) {
    const lv = levels[i];
    const lo = levels[Math.max(0, i-1)], hi = levels[Math.min(L-1, i+1)];
    const slope = ((lo.rx + lo.rz) - (hi.rx + hi.rz)) / (2 * Math.max(0.2, hi.y - lo.y));
    const yn = 2 * (lv.y - y0) / Math.max(0.2, y1 - y0) - 1;
    const row = [];
    for (let j = 0; j <= seg; j++) {
      const ph = (j / seg) * Math.PI * 2;
      const cx = Math.cos(ph), sz = Math.sin(ph);
      const u = [cx * 0.92, yn, sz * 0.92];
      const n = V.norm([cx, slope * 0.6, sz]);
      row.push(mb.vert([lv.x + cx * lv.rx, lv.y, lv.z + sz * lv.rz], n,
        colorFn ? colorFn(u) : col, gloss, emis));
    }
    rows.push(row);
  }
  // rows run bottom→top (the reverse of ellipsoid's top→bottom), so the
  // quads are emitted in reverse order to keep outward faces front-facing —
  // otherwise the two-sided shader flips the torso's normals and the whole
  // front renders back-lit (slate-blue in the moon rim)
  for (let i = 0; i < L - 1; i++)
    for (let j = 0; j < seg; j++)
      mb.quad(rows[i+1][j], rows[i+1][j+1], rows[i][j+1], rows[i][j]);
  for (const [k, sgn] of [[0, -1], [L - 1, 1]]) {
    const lv = levels[k];
    const cv = mb.vert([lv.x, lv.y, lv.z], [0, sgn, 0],
      colorFn ? colorFn([0, sgn, 0]) : col, gloss, emis);
    for (let j = 0; j < seg; j++)
      sgn > 0 ? mb.tri(cv, rows[k][j+1], rows[k][j]) : mb.tri(cv, rows[k][j], rows[k][j+1]);
  }
}

/** Torso profiles: build 0 = pear (bottom-heavy egg), 1 = gorilla
 * (triangular — huge chest and shoulders over narrow hips, hunched
 * forward). Everything between breeds smoothly. */
const PROFILE_PEAR = [1.02, 1.22, 0.90, 0.62, 0.38];
const PROFILE_GOR  = [0.60, 0.80, 1.24, 1.40, 0.58];
const PROFILE_T    = [0, 0.30, 0.60, 0.86, 1];

function torsoLevels(build, W, h, y0, lean) {
  return PROFILE_T.map((t, i) => {
    const rx = W * (PROFILE_PEAR[i] + (PROFILE_GOR[i] - PROFILE_PEAR[i]) * build);
    return {
      y: y0 + t * h, x: 0,
      z: t > 0.45 ? lean * build * (t - 0.45) / 0.55 : 0,
      rx, rz: rx * (i === 2 ? 0.82 + 0.30 * build : 0.80),   // deep gorilla chest
    };
  });
}

const BRAINC = [214, 150, 160];

/** The modular LOWER TORSO. Material follows the leg family — flesh
 * pelvis for organic legs, chitin pod for insect, and a machined metal
 * chassis when the legs are tech (the whole lower body pops off and a
 * mechanical unit bolts on). The waist junction is a brass collar worn
 * as a bolted belt. */
function buildPelvis(mb, o, waistR, waistY) {
  const mech = o.legFam === 'piston_leg';
  const chit = o.legFam === 'insect_leg';
  const col = mech ? METAL : chit ? lp(CHITIN, o.skin, 0.35) : o.skin;
  const fn = (mech || chit) ? null : o.skinFn;
  const prevTex = mb.tex, prevAnim = mb.anim;
  mb.setAnim(ANIM0);
  mb.setTex(mech ? TEX_NONE : chit ? [...TILE.chitin, o.texScale, 0.6]
                                   : [...TILE.warts, o.texScale, 0.5]);
  const hipY = waistY - 1.15;
  lathe(mb, [
    { y: hipY - 0.45, x: 0, z: 0, rx: waistR*0.72, rz: waistR*0.60 },
    { y: hipY + 0.30, x: 0, z: 0, rx: waistR*1.04, rz: waistR*0.86 },
    { y: waistY + 0.12, x: 0, z: 0, rx: waistR*0.94, rz: waistR*0.78 },
  ], col, mech ? 0.7 : 0.28, 0, 14, fn);
  if (mech) {
    for (let i = 0; i < 8; i++) {          // chassis rivets
      const a = (i / 8) * Math.PI * 2;
      ellipsoid(mb, [Math.cos(a)*waistR, hipY + 0.3, Math.sin(a)*waistR*0.83],
        [0.09, 0.09, 0.09], IRON, 0.8, 0, 4);
    }
    ellipsoid(mb, [0, hipY + 0.25, waistR*0.82], [0.16, 0.16, 0.1], GLOW, 0.5, 1, 6);
    mb.glow([0, hipY + 0.25, waistR*0.88], GLOW, 18);
  }
  // THE BELT: brass collar around the waist junction, iron-bolted
  torus(mb, [0, waistY + 0.05, 0], [0, 1, 0], waistR*0.98, 0.24, BRASS, 0.85, 0, 18, 8);
  for (let i = 0; i < 8; i++) {
    const a = (i / 8) * Math.PI * 2 + 0.39;
    ellipsoid(mb, [Math.cos(a)*(waistR*0.98 + 0.16), waistY + 0.05, Math.sin(a)*(waistR*0.98 + 0.16)],
      [0.13, 0.13, 0.13], IRON, 0.8, 0, 4);
  }
  mb.setTex(prevTex);
  mb.setAnim(prevAnim);
}

/** The head ladder: dim = pinhead sunk in the shoulders, average =
 * standard, gifted = tall egghead dome, mastermind = exposed pulsing
 * brain with two lobes. Returns geometry the face and sockets hang on. */
function buildHead(mb, o, neckY, zOff) {
  const t = o.brainTier;
  let hR, sunk = 0.72;
  if (t === 'dim')             { hR = [1.05, 0.98, 1.10]; sunk = 0.52; }
  else if (t === 'gifted')     { hR = [1.28, 1.75, 1.30]; sunk = 0.78; }
  else if (t === 'mastermind') { hR = [1.45, 1.38, 1.45]; }
  else                         { hR = [1.32, 1.26, 1.35]; }
  const hC = [0, neckY + hR[1] * sunk, zOff + 0.15];
  ellipsoid(mb, hC, hR, o.skin, 0.3, 0, 16, o.skinFn);
  let topY = hC[1] + hR[1];
  if (t === 'gifted') {
    // egghead crown
    ellipsoid(mb, [hC[0], hC[1] + hR[1]*0.52, hC[2] - 0.1],
      [hR[0]*0.72, hR[1]*0.52, hR[2]*0.70], o.skin, 0.3, 0, 12, o.skinFn);
    topY = hC[1] + hR[1] * 1.04;
  } else if (t === 'mastermind') {
    // the brain, proudly exposed
    const bc = [hC[0], hC[1] + hR[1]*0.62, hC[2] - 0.15];
    const bTex = mb.tex;
    mb.setTex([...TILE.slick, 0.9, 0.55]);
    ellipsoid(mb, bc, [hR[0]*0.92, hR[1]*0.66, hR[2]*0.88], BRAINC, 0.55, 0, 14);
    for (const s of [-1, 1])
      ellipsoid(mb, [bc[0] + s*hR[0]*0.40, bc[1] + hR[1]*0.34, bc[2]],
        [hR[0]*0.44, hR[1]*0.30, hR[2]*0.55], sh(BRAINC, 0.92), 0.55, 0, 8);
    mb.setTex(bTex);
    topY = bc[1] + hR[1] * 0.88;
  }
  return { hC, hR, topY };
}

/** Tail from the tail gene (below 0.35 there is none): a swaying tapered
 * whip out the lower back, curling up. Winged plans cap it with a devil
 * spade. */
function addTail(mb, o, baseY, baseZ, spade) {
  if (o.tail < 0.35) return;
  const k = (o.tail - 0.35) / 0.65;
  const L = 2.4 + 3.4 * k;
  const path = [];
  for (let i = 0; i <= 8; i++) {
    const t = i / 8;
    path.push([
      Math.sin(t * 2.6) * 0.5 * k,
      baseY - Math.sin(t * Math.PI) * 0.6 + t * t * (1.6 + 2.2 * k),
      baseZ - t * L,
    ]);
  }
  const r0 = 0.5 + 0.25 * o.bulk;
  tube(mb, path, path.map((_, i) => r0 * (1 - (i / 8) * 0.85)), o.skin, 0.3, 0, 8, 3,
    null, (t) => [0, 0, 0.18 * t * t, 4 + t * 3.4]);
  if (spade)
    curvedCone(mb, path[8], [0, 0.4, -1], 0.9, 0.3, [0, 0.2, 0], sh(o.skin, 0.7), 0.4);
}

function planTetrapod(mb, o) {
  const BREATH_T = [0.09, 0, 0, 0], BREATH_H = [0.04, 0, 0, 0];
  const b = o.limb;                       // the limb axis IS the build axis here
  const W = 1.9 + 1.0 * o.bulk;           // human-ish width, not beach-ball
  const h = 3.1 + 0.7 * o.bulk;
  const waistY = o.legLen + 1.15;         // lower torso lives below the belt
  const y0 = waistY - 0.15;
  const levels = torsoLevels(b, W, h, y0, 0.5);

  mb.setAnim(BREATH_T);
  mb.setTex([...TILE.warts, o.texScale, 0.55]);
  lathe(mb, levels, o.skin, 0.28, 0, 18, o.skinFn);
  const shl = levels[3];
  if (b > 0.5)                            // brute deltoid caps
    for (const s of [-1, 1])
      ellipsoid(mb, [s * shl.rx * 0.85, shl.y + 0.15, shl.z],
        [W*0.48*b, W*0.42*b, W*0.44*b], o.skin, 0.28, 0, 10, o.skinFn);
  const ch = levels[2];
  stitchSeam(mb, ch.y - h * 0.12, ch.rx, ch.rz, ch.z);
  buildPelvis(mb, o, levels[0].rx, waistY);

  // an actual neck between the shoulders and the skull
  const neckTop = y0 + h + 0.55;
  tube(mb, [[0, y0 + h - 0.3, levels[4].z * 0.7], [0, neckTop, levels[4].z * 0.8]],
    [W*0.34, W*0.29], sh(o.skin, 0.95), 0.28, 0, 10);

  mb.setAnim(BREATH_H);
  const head = buildHead(mb, o, neckTop - 0.2, levels[4].z);
  frankenDetails(mb, head.hC, head.hR, o.heartLevel, o);
  mb.setAnim(ANIM0);

  addTail(mb, o, o.legLen + 0.55, -levels[0].rx * 0.75, false);

  // socket frames: position + the body's outward surface normal there
  const slope = (levels[2].rx - levels[4].rx) / Math.max(0.4, levels[4].y - levels[2].y);
  const sensP = [head.hR[0]*0.52, head.topY, head.hC[2] - 0.1];
  const eyeP  = [0, head.hC[1] + head.hR[1]*0.20, head.hC[2] + head.hR[2]*0.62];
  return {
    hand:   { p: [shl.rx * 0.92 + (b > 0.5 ? W * 0.28 * b : 0), shl.y, shl.z + 0.15],
              nrm: V.norm([1, slope * 0.5, 0.15]), mirror: true },
    leg:    { p: [Math.max(0.7, levels[0].rx * 0.58), o.legLen, 0],
              nrm: V.norm([0.3, -1, 0]), mirror: true, len: o.legLen },
    sensor: { p: sensP, nrm: ellipN(sensP, head.hC, head.hR), mirror: true, out: 1, anim: BREATH_H },
    eye:    { p: eyeP, nrm: ellipN(eyeP, head.hC, head.hR),
              mirror: false, faceR: head.hR[0], anim: BREATH_H },
  };
}

function planBlob(mb, o) {
  // limb gene sets the pour: 0 = wide flat puddle, 1 = tall gelatin tower
  const tall = o.limb;
  const dr = (3.0 + 1.3*o.bulk) * (1.15 - 0.40*tall);
  const dR = [dr, (2.5 + 1.0*o.bulk) * (0.62 + 1.05*tall), dr];
  const dC = [0, dR[1]*0.9, 0];
  const JELLY = [0.13, 0, 0.10, 0.7];
  mb.setAnim(JELLY);
  mb.setTex([...TILE.slick, o.texScale, 0.85]);
  // squash-and-stretch: the crown bobs on the flap channel, the base stays
  ellipsoid(mb, dC, dR, o.skin, 0.34, 0, 18, o.skinFn,
    (u) => [0.13, 0.30 * Math.max(0, u[1]), 0.10, 0.7]);
  // drooping skirt
  mb.setAnim([0.05, 0, 0.05, 0.7]);
  ellipsoid(mb, [0, 0.62, 0], [dr*1.14, 0.85, dr*1.14], sh(o.skin, 0.92), 0.3, 0, 12, o.skinFn);
  // surface boils
  mb.setAnim(JELLY);
  for (let a = 0; a < 6; a++) {
    const th = a * 1.047 + 0.4;
    ellipsoid(mb, [Math.cos(th)*dr*0.9, 1.1 + (a%3)*0.5, Math.sin(th)*dr*0.9],
      [0.5, 0.42, 0.5], sh(o.skin, 0.88), 0.4, 0, 6);
  }
  mb.setAnim(ANIM0);
  const bHandP = [dr*0.92, dC[1] + 0.4, 0];
  const bSensP = [dr*0.5, dC[1] + dR[1]*0.85, 0];
  const bEyeP  = [0, dC[1] + dR[1]*0.35, dR[2]*0.9];
  return {
    hand:   { p: bHandP, nrm: ellipN(bHandP, dC, dR), mirror: true, anim: JELLY },
    sensor: { p: bSensP, nrm: ellipN(bSensP, dC, dR), mirror: true, out: 1, anim: JELLY },
    eye:    { p: bEyeP, nrm: ellipN(bEyeP, dC, dR), mirror: false, faceR: dr*0.8, anim: JELLY },
  };
}

function planSerpentine(mb, o) {
  const girth = o.bulk;
  const baseR = 0.95 + 0.7 * girth;
  const headY = 6.6 + 2.6 * o.limb;
  const path = [], radii = [];
  const N = 30;
  for (let k = 0; k <= N; k++) {                       // ground coil
    const t = k / N;
    const ang = t * Math.PI * 2 * 1.6 + 0.8;
    const R = 2.75 - 1.35 * t;
    path.push([Math.cos(ang) * R, 0.55 + t * 2.0, Math.sin(ang) * R * 0.8]);
    radii.push(baseR * clamp(t * 6, 0.16, 1));
  }
  const neckBase = path[path.length - 1];
  for (let m = 1; m <= 10; m++) {                      // rising S-neck
    const s = m / 10;
    path.push([
      neckBase[0] * (1 - s) + Math.sin(s * Math.PI) * 0.7,
      neckBase[1] + s * (headY - neckBase[1]),
      neckBase[2] * (1 - s) + s * 1.1,
    ]);
    radii.push(baseR * (1 - 0.42 * s));
  }
  // sway travels up the coil and grows toward the raised neck
  const swayFn = (t) => [0.02 + 0.02*t, 0, 0.5*t*t, t*5.2];
  const SWAY_H = [0.03, 0, 0.5, 5.2];   // the head rides the neck tip
  mb.setTex([...TILE.scales, o.texScale, 0.8]);
  tube(mb, path, radii, o.skin, 0.3, 0, 12, 3,
    (t) => sh(o.skin, 0.84 + 0.16 * Math.sin(t * 40) * 0.5 + 0.16),   // belly-band shimmer
    swayFn);

  mb.setAnim(SWAY_H);
  const hC = [0.35, headY + 0.9, 1.25];
  const hR = [1.5 + 0.4*o.headScale, 1.3 + 0.4*o.headScale, 1.7];
  ellipsoid(mb, hC, hR, o.skin, 0.3, 0, 14, o.skinFn);
  if (girth > 0.55)                                     // cobra hood
    ellipsoid(mb, [hC[0], hC[1] - 0.2, hC[2] - 0.7], [hR[0]*1.75, hR[1]*1.5, 0.5],
      sh(o.skin, 0.9), 0.28, 0, 12, o.skinFn);
  // fangs point DOWN on a serpent
  const mz = hC[2] + hR[2] * 0.8;
  mb.setTex(TEX_NONE);
  ellipsoid(mb, [hC[0], hC[1] - hR[1]*0.4, mz], [hR[0]*0.4, 0.16, 0.2], MOUTHC, 0.15, 0, 8);
  for (const s of [-1, 1])
    curvedCone(mb, [hC[0] + s*hR[0]*0.3, hC[1] - hR[1]*0.42, mz], [0, -1, 0.1],
      0.6, 0.13, [0, 0, 0.04], CLAW, 0.6);

  // forked tongue, parked just inside the mouth; the flicker uniform darts
  // it forward (fx < 0 marks tongue geometry)
  mb.setFx(-1.0);
  for (const s of [-1, 1])
    tube(mb, [
      [hC[0], hC[1] - hR[1]*0.38, mz - 0.55],
      [hC[0] + s*0.05, hC[1] - hR[1]*0.40, mz - 0.15],
      [hC[0] + s*0.22, hC[1] - hR[1]*0.34, mz + 0.25],
    ], [0.09, 0.07, 0.02], TONGUE, 0.6, 0, 5);
  mb.setFx(0);
  mb.setAnim(ANIM0);

  const sSensP = [hC[0] + 0.5, hC[1] + hR[1]*0.8, hC[2] - 0.3];
  const sEyeP  = [hC[0], hC[1] + hR[1]*0.25, hC[2] + hR[2]*0.85];
  return {
    hand:   { p: [hC[0] + 1.0, headY - 1.3, 0.9], nrm: V.norm([1, 0.1, 0.35]),
              mirror: true, tiny: true, anim: SWAY_H },
    sensor: { p: sSensP, nrm: ellipN(sSensP, hC, hR), mirror: false, out: 1, anim: SWAY_H },
    eye:    { p: sEyeP, nrm: ellipN(sEyeP, hC, hR), mirror: false, faceR: hR[0], anim: SWAY_H },
  };
}

function planWinged(mb, o) {
  const BREATH_B = [0.07, 0, 0, 0], BREATH_H = [0.035, 0, 0, 0];
  const b = o.bulk * 0.85;               // bulk sets the build: imp vs gargoyle
  const W = 1.5 + 0.8 * o.bulk;
  const h = 2.8 + 0.6 * o.bulk;
  const waistY = o.legLen + 0.95;
  const y0 = waistY - 0.12;
  const levels = torsoLevels(b, W, h, y0, 0.4);

  mb.setAnim(BREATH_B);
  mb.setTex([...TILE.feathers, o.texScale, 0.5]);
  lathe(mb, levels, o.skin, 0.28, 0, 14, o.skinFn);
  buildPelvis(mb, o, levels[0].rx, waistY);
  const neckTop = y0 + h + 0.45;
  tube(mb, [[0, y0 + h - 0.25, levels[4].z * 0.7], [0, neckTop, levels[4].z * 0.8]],
    [W*0.33, W*0.28], sh(o.skin, 0.95), 0.28, 0, 9);
  mb.setAnim(BREATH_H);
  const head = buildHead(mb, o, neckTop - 0.2, levels[4].z);
  frankenDetails(mb, head.hC, head.hR, o.heartLevel, o);
  mb.setAnim(ANIM0);

  addTail(mb, o, o.legLen + 0.5, -levels[0].rx * 0.75, true);   // devil spade

  // bat wings, rooted at the BACK shoulders (grafted on, not grown from the
  // sides) and sweeping out and behind. The flap is a traveling sine: phase
  // advances along the span, so the wing rolls in a wave — shoulder barely
  // stirs, tips ride the crest.
  const span = 4.6 + 3.6 * o.limb;
  const shY = levels[3].y;
  const rootZ = levels[3].z - levels[3].rz * 0.8;
  const wingCol = sh(lp(o.skin, spineOf(o.skin), 0.25), 0.95);
  for (const s of [-1, 1]) {
    const wingAnim = (t) => [0, 0.55 * t * t + 0.04, 0.05 * t, t * 2.2 + (s > 0 ? 0 : 0.4)];
    const nU = 9, nV = 3, grid = [];
    const lead = [];
    for (let iu = 0; iu <= nU; iu++) {
      const u = iu / nU;
      lead.push([
        s * (0.9 + u * span),
        shY + Math.sin(u * Math.PI * 0.85) * 2.5 - u * u * 1.6,
        rootZ - u * 0.85,
      ]);
    }
    // hoop around the wing bone right at its root, angled with the bone
    limbJoint(mb, lead[0], V.sub(lead[1], lead[0]), 0.34);
    mb.setTex(TEX_NONE);
    for (let iu = 0; iu <= nU; iu++) {
      const u = iu / nU;
      const [lx, ly, lz] = lead[iu];
      const chord = (2.5 * (1 - 0.5 * u) + 0.5) * (1 + 0.10 * Math.sin(u * Math.PI * 3));
      const row = [];
      for (let iv = 0; iv <= nV; iv++) {
        const v = iv / nV;
        mb.setAnim(wingAnim(u));
        row.push(mb.vert(
          [lx, ly - v * chord, lz + v * 0.3],
          [0, 0.25, 0.97],   // soft fake normal; shader two-sides it
          lp(wingCol, o.skin, v * 0.35), 0.2, 0));
      }
      grid.push(row);
    }
    mb.setAnim(ANIM0);
    for (let iu = 0; iu < nU; iu++)
      for (let iv = 0; iv < nV; iv++)
        mb.quad(grid[iu][iv], grid[iu][iv+1], grid[iu+1][iv+1], grid[iu+1][iv]);
    tube(mb, lead, lead.map((_, i) => 0.24 * (1 - i / lead.length) + 0.08), BONDK, 0.35, 0, 7, 3, null, wingAnim);
    for (const fu of [0.45, 0.75]) {
      const k = Math.round(fu * nU);
      const a = lead[k];
      const chord = (2.5 * (1 - 0.5 * fu) + 0.5);
      mb.setAnim(wingAnim(fu));
      tube(mb, [a, [a[0], a[1] - chord, a[2] + 0.3]], [0.12, 0.05], BONDK, 0.3, 0, 6);
    }
    mb.setAnim(ANIM0);
  }

  const wSensP = [head.hR[0]*0.5, head.topY, head.hC[2] - 0.1];
  const wEyeP  = [0, head.hC[1] + head.hR[1]*0.2, head.hC[2] + head.hR[2]*0.62];
  return {
    hand:   { p: [levels[2].rx * 0.95, levels[2].y + 0.2, levels[2].z + 0.3],
              nrm: V.norm([1, 0.1, 0.3]), mirror: true, tiny: true },
    leg:    { p: [Math.max(0.8, levels[0].rx * 0.5), o.legLen, 0],
              nrm: V.norm([0.28, -1, 0]), mirror: true, len: o.legLen },
    sensor: { p: wSensP, nrm: ellipN(wSensP, head.hC, head.hR), mirror: true, out: 1, anim: BREATH_H },
    eye:    { p: wEyeP, nrm: ellipN(wEyeP, head.hC, head.hR),
              mirror: false, faceR: head.hR[0], anim: BREATH_H },
  };
}

function spineOf(skin) { return lp(skin, [52, 40, 80], 0.45); }

/** Outward surface normal of an ellipsoid at point q — the analytic
 * gradient. Socket frames use this so parts grow along the skin. */
function ellipN(q, c, r) {
  return V.norm([
    (q[0] - c[0]) / (r[0] * r[0]),
    (q[1] - c[1]) / (r[1] * r[1]),
    (q[2] - c[2]) / (r[2] * r[2]),
  ]);
}

// ---- parts ------------------------------------------------------------------
// Each part reads its 6 genes [length,girth,taper,curl,count,ornament],
// clamps them into safe morph ranges, and builds from control skeletons.
// `side` is +1 (right) / −1 (left): control points mirror, geometry never
// gets a negative scale.

function buildPart(mb, slot, family, params, side, sock, o) {
  const [len=0.5, girth=0.5, taper=0.5, curl=0.5, count=0.5, orn=0.5] = params;
  const S = [side * sock.p[0], sock.p[1], sock.p[2]];
  const scale = sock.tiny ? 0.62 : 1;
  // the rig: parts leave the body along the surface normal at the socket,
  // so nothing buries into a chest or skewers a dome on extreme morphs
  const N = sock.nrm ? V.norm([side * sock.nrm[0], sock.nrm[1], sock.nrm[2]]) : [side, 0, 0];
  mb.setAnim(sock.anim ?? ANIM0);   // parts ride whatever their mount does
  // joint hardware (limbJoint) is placed per family below, sized from the
  // same girth genes as the limb it clamps — collar always fits the limb

  // biological surface per family: tentacles read wet, bird legs feathered,
  // insect parts chitinous. The part's ornament gene sets detail density.
  const TEX_FAM = {
    claw_hand: ['warts', 0.45], pincer: ['chitin', 0.5], tentacle: ['slick', 0.8],
    rifle_arm: ['none', 0], plasma_lance: ['chitin', 0.6], hand_stump: ['warts', 0.3],
    antenna: ['ridge', 0.35], horn: ['ridge', 0.55], sensor_mast: ['none', 0],
    sensor_stub: ['warts', 0.3],
    bug_eyes: ['none', 0], cyclops_eye: ['none', 0], stalk_eyes: ['none', 0],
    optic_visor: ['none', 0], eye_socket: ['warts', 0.3],
    hoofed_leg: ['warts', 0.5], talon_leg: ['feathers', 0.55], insect_leg: ['chitin', 0.65],
    piston_leg: ['none', 0], leg_stump: ['warts', 0.3],
  };
  const tf = TEX_FAM[family] ?? ['none', 0];
  mb.setTex([...TILE[tf[0]], 0.45 + 0.75 * orn, tf[1]]);

  switch (family) {
    // ---- hands ----
    case 'claw_hand': {
      const armR = (0.42 + 0.4*girth) * scale;
      const wrist = armDrop(mb, S, side, armR, scale, o, [len, girth, taper, curl], N);
      ellipsoid(mb, wrist, [armR*1.35, armR*1.15, armR*1.35], o.skin, 0.3, 0, 8, o.skinFn);
      const n = clamp(2 + Math.round(count * 3), 2, 5);
      for (let i = 0; i < n; i++) {
        const a = (i / (n - 1 || 1) - 0.5) * 1.5;
        curvedCone(mb, wrist,
          [Math.sin(a) * 0.5, -0.85, 0.45 + Math.cos(a) * 0.2],
          (0.7 + 1.0*len) * scale, (0.24 + 0.16*girth) * scale,
          [0, -(0.45 + curl*0.7), 0.3], CLAW, 0.55);
      }
      break;
    }
    case 'pincer': {
      const armR = (0.5 + 0.4*girth) * scale;
      const wrist = armDrop(mb, S, side, armR, scale, o, [len, girth, taper, curl], N);
      const jl = (1.1 + 1.5*len) * scale;
      curvedCone(mb, wrist, [side*0.15, -0.25, 0.9], jl, armR*0.75, [0, -(0.4+curl*0.8), 0.3], CLAW, 0.5);
      curvedCone(mb, wrist, [side*0.15, -0.9, 0.35], jl*0.9, armR*0.65, [0, 0.45+curl*0.6, 0.45], CLAW, 0.5);
      break;
    }
    case 'tentacle': {
      const baseR = (0.5 + 0.42*girth) * scale;
      const L = (2.6 + 2.6*len) * scale;
      const path = [];
      for (let i = 0; i <= 10; i++) {
        const t = i / 10;
        const exit = baseR * 1.6 * Math.pow(1 - t, 1.5);   // leave along the normal first
        path.push([
          S[0] + N[0]*exit + side * (0.5*t + Math.sin(t*Math.PI*1.2) * 0.4),
          S[1] + N[1]*exit - t*L + Math.sin(t*Math.PI) * 0.2,
          S[2] + N[2]*exit + 0.4*t + Math.sin(t * Math.PI * (1 + curl*1.6)) * curl * 1.1,
        ]);
      }
      limbJoint(mb, path[0], V.sub(path[1], path[0]), baseR);
      ellipsoid(mb, V.add(S, V.scale(N, baseR*0.5)), [baseR*1.25, baseR*1.15, baseR*1.2],
        o.skin, 0.3, 0, 8, o.skinFn);   // shoulder mass at the root
      tube(mb, path, path.map((_, i) =>
        baseR * (1 - (i/10) * clamp(0.35 + 0.6*taper, 0.35, 0.92))), o.skin, 0.3, 0, 9, 3,
        null, (t) => [0, 0, 0.1 + 0.45*t*t, side*2 + t*3.2]);   // wave travels to the tip
      break;
    }
    case 'rifle_arm': {
      const wrist = armDrop(mb, S, side, 0.42*scale, scale, o, [len, girth, taper, curl], N);
      // rounded receiver, no boxes — a toy gun, not a brick
      ellipsoid(mb, [wrist[0], wrist[1]+0.05, wrist[2]+0.3], [0.5, 0.42, 1.0], METAL, 0.7, 0, 10);
      // barrel with a chunky muzzle brake and a little front sight
      tube(mb, [[wrist[0], wrist[1]+0.12, wrist[2]+1.0], [wrist[0], wrist[1]+0.12, wrist[2]+3.05]],
        [0.17, 0.15], METDK, 0.85, 0, 10);
      tube(mb, [[wrist[0], wrist[1]+0.12, wrist[2]+3.0], [wrist[0], wrist[1]+0.12, wrist[2]+3.4]],
        [0.28, 0.28], METDK, 0.85, 0, 10, 2);
      ellipsoid(mb, [wrist[0], wrist[1]+0.4, wrist[2]+2.55], [0.07, 0.15, 0.07], METDK, 0.6, 0, 6);
      // grip under the receiver, curved stock with a butt pad behind
      tube(mb, [[wrist[0], wrist[1]-0.3, wrist[2]+0.5], [wrist[0], wrist[1]-0.88, wrist[2]+0.32]],
        [0.17, 0.14], METDK, 0.5, 0, 8, 2);
      tube(mb, [
        [wrist[0], wrist[1], wrist[2]-0.6],
        [wrist[0], wrist[1]-0.25, wrist[2]-1.25],
        [wrist[0], wrist[1]-0.45, wrist[2]-1.6],
      ], [0.3, 0.26, 0.34], METDK, 0.6, 0, 8);
      ellipsoid(mb, [wrist[0], wrist[1]-0.5, wrist[2]-1.72], [0.3, 0.44, 0.16], sh(METDK, 0.8), 0.4, 0, 8);
      ellipsoid(mb, [wrist[0], wrist[1]+0.12, wrist[2]+3.42], [0.13,0.13,0.13], GLOW, 0.5, 0.9, 6);
      mb.glow([wrist[0], wrist[1]+0.12, wrist[2]+3.45], GLOW, 18);
      break;
    }
    case 'plasma_lance': {
      const wrist = armDrop(mb, S, side, 0.5*scale, scale, { skin: CHITIN, skinFn: null }, [len, girth, taper, curl], N);
      const L = (1.6 + 1.6*len) * scale;
      ellipsoid(mb, wrist, [0.55, 0.5, 0.55], CHITIN, 0.4, 0, 8);
      tube(mb, [wrist, [wrist[0], wrist[1]+L*0.9, wrist[2]+0.5]],
        [0.3, 0.05], ICHOR, 0.5, 0.8, 8);
      ellipsoid(mb, [wrist[0], wrist[1]+L*0.92, wrist[2]+0.52], [0.2,0.2,0.2], BLTGLO, 0.5, 1, 6);
      mb.glow([wrist[0], wrist[1]+L*0.92, wrist[2]+0.52], ICHOR, 30);
      break;
    }
    case 'hand_stump': {
      ellipsoid(mb, [S[0], S[1], S[2]], [0.62, 0.5, 0.62], PALLOR, 0.25, 0, 8);
      ringStitch(mb, [S[0], S[1]-0.1, S[2]], 0.58);
      break;
    }

    // ---- sensors (paired via sock.mirror) ----
    case 'antenna': {
      const L = (1.5 + 1.7*len);
      const aR = 0.11 + 0.09*girth;
      const gDir = V.norm(V.add(N, [0, 0.55, 0]));
      const path = [];
      for (let i = 0; i <= 6; i++) {
        const t = i / 6;
        path.push([
          S[0] + gDir[0]*t*L + side*t*t*0.5,
          S[1] + gDir[1]*t*L,
          S[2] + gDir[2]*t*L + Math.sin(t*2.2)*0.25,
        ]);
      }
      limbJoint(mb, path[0], V.sub(path[1], path[0]), aR);
      tube(mb, path, path.map(() => aR), BONE, 0.35, 0, 6);
      if (girth > 0.3)
        ellipsoid(mb, path[6], [0.3, 0.3, 0.3], BONDK, 0.5, 0, 6);
      break;
    }
    case 'horn': {
      const hornR = 0.3 + 0.4*girth;
      const hDir = V.norm(V.add(N, [0, 0.55, 0]));
      limbJoint(mb, S, hDir, hornR * 0.85);
      curvedCone(mb, S, hDir, 1.2 + 1.5*girth, hornR,
        [side*(0.3 + curl*0.9), curl*0.4, -0.2], lp(BONE, o.skin, 0.25), 0.4);
      break;
    }
    case 'sensor_mast': {
      const L = 1.5 + 1.0*len;
      const mDir = V.norm(V.add(N, [0, 1.1, 0]));
      const mTop = V.add(S, V.scale(mDir, L));
      limbJoint(mb, S, mDir, 0.2);
      tube(mb, [S, mTop], [0.22, 0.16], METAL, 0.8, 0, 8);
      ellipsoid(mb, [mTop[0], mTop[1], mTop[2]+0.15], [0.68, 0.68, 0.18], METDK, 0.7, 0, 10);
      ellipsoid(mb, [mTop[0], mTop[1], mTop[2]+0.34], [0.16,0.16,0.16], GLOW, 0.5, 1, 6);
      mb.glow([mTop[0], mTop[1], mTop[2]+0.36], GLOW, 20);
      break;
    }
    case 'sensor_stub': {
      ellipsoid(mb, S, [0.3, 0.22, 0.3], PALLOR, 0.25, 0, 6);
      break;
    }

    // ---- eyes (single socket, patterns handle multiplicity) ----
    case 'bug_eyes': {
      // deep-set under the brow ridge: always a MAIN PAIR (real anatomy),
      // extra count genes add smaller mutant eyes above and beside them
      const n = clamp(2 + Math.round(count * 3), 2, 5);
      const spots = [[-0.46, 0, 1], [0.46, 0, 1], [0, 0.5, 0.6], [-0.74, 0.4, 0.55], [0.74, 0.4, 0.55]];
      const R = 0.30 + 0.20*girth;
      for (let i = 0; i < n; i++) {
        const [ex, ey, sc] = spots[i];
        eyeball(mb, [S[0] + ex*sock.faceR, S[1] + ey*1.1, S[2] - Math.abs(ex)*0.2],
          R * sc, o.skin, 0.5, N);
      }
      break;
    }
    case 'cyclops_eye': {
      const R = 0.55 + 0.4*girth;
      eyeball(mb, S, R, o.skin, 0.7, N);
      // one heavy scowling unibrow; knits down on each blink
      const uBase = mb.anim;
      mb.setAnim([uBase[0], -R * 0.3, uBase[2], uBase[3]]);
      tube(mb, [
        [S[0] - R*1.05, S[1] + R*0.72, S[2] - 0.15],
        [S[0],          S[1] + R*0.98, S[2] + 0.05],
        [S[0] + R*1.05, S[1] + R*0.72, S[2] - 0.15],
      ], [0.2, 0.26, 0.2], sh(o.skin, 0.5), 0.25, 0, 7);
      mb.setAnim(uBase);
      break;
    }
    case 'stalk_eyes': {
      const L = 1.1 + 1.4*len;
      for (const s of [-1, 1]) {
        const top = [S[0] + s*0.75, S[1] + L, S[2] + 0.25];
        tube(mb, [[S[0] + s*0.45, S[1] - 0.3, S[2] - 0.3], [S[0]+s*0.7, S[1]+L*0.6, S[2]], top],
          [0.16, 0.13, 0.11], BONDK, 0.3, 0, 6);
        eyeball(mb, top, 0.4 + 0.2*girth, o.skin, 0.25, V.norm(V.add(N, [0, 0, 0.8])));
      }
      break;
    }
    case 'optic_visor': {
      ellipsoid(mb, [S[0], S[1], S[2] - 0.2], [sock.faceR*0.84, 0.5, 0.36], METDK, 0.75, 0, 12);
      const n = clamp(1 + Math.round(count * 2), 1, 3);
      for (let i = 0; i < n; i++) {
        const ex = (i - (n-1)/2) * sock.faceR * 0.55;
        ellipsoid(mb, [S[0]+ex, S[1], S[2]+0.2], [0.26, 0.26, 0.1], GLOW, 0.6, 1, 8);
        mb.glow([S[0]+ex, S[1], S[2]+0.3], GLOW, 16);
      }
      break;
    }
    case 'eye_socket': {
      ellipsoid(mb, [S[0], S[1], S[2] - 0.05], [0.55, 0.4, 0.18], sh(o.skin, 0.5), 0.15, 0, 8);
      break;
    }

    // ---- legs (paired) ----
    case 'hoofed_leg': {
      // biped leg: thigh, knee, calf, and a hoofed foot stepping forward
      const R = (0.42 + 0.3*girth);
      const hip   = [S[0], sock.len + 0.5, S[2]];
      const knee  = [S[0] + N[0]*0.45, sock.len * 0.52, S[2] + 0.24 + N[2]*0.2];
      const ankle = [S[0] + N[0]*0.2, 0.6, S[2] - 0.08];
      limbJoint(mb, hip, V.sub(knee, hip), R * 1.1);
      ellipsoid(mb, [hip[0], hip[1] + 0.15, hip[2]], [R*1.35, R*1.25, R*1.3],
        o.skin, 0.28, 0, 8, o.skinFn);   // hip joint mass
      tube(mb, [hip, knee, ankle], [R*1.18, R*0.9, R*0.68], o.skin, 0.28, 0, 9);
      ellipsoid(mb, [S[0], 0.4, S[2] + 0.35], [R*0.8, 0.34, R*1.2], o.skin, 0.28, 0, 8, o.skinFn);
      tube(mb, [[S[0], 0.5, S[2] + 0.8], [S[0], 0.0, S[2] + 0.9]], [R*0.62, R*0.75], HOOF, 0.5, 0, 9, 2);
      break;
    }
    case 'talon_leg': {
      const R = 0.24 + 0.12*girth;
      // direction = the shin's actual first segment
      limbJoint(mb, [S[0], sock.len + 0.6, S[2]],
        [side * 0.15, sock.len * 0.55 - (sock.len + 0.6), -0.55], R * 1.2);
      ellipsoid(mb, [S[0], sock.len + 0.65, S[2]], [R*1.7, R*1.55, R*1.65],
        o.skin, 0.28, 0, 8, o.skinFn);   // hip joint mass
      tube(mb, [
        [S[0], sock.len + 0.6, S[2]],
        [S[0] + side*0.15, sock.len*0.55, S[2] - 0.55],
        [S[0], 0.12, S[2] + 0.1],
      ], [R*1.3, R, R*0.9], o.skin, 0.3, 0, 7);
      const nt = clamp(2 + Math.round(count*2), 2, 4);
      for (let i = 0; i < nt; i++) {
        const a = (i/(nt-1||1) - 0.5) * 1.6;
        curvedCone(mb, [S[0], 0.18, S[2]], [Math.sin(a)*0.7, -0.18, Math.cos(a)*0.8],
          0.8, 0.14, [0, -0.12, 0], CLAW, 0.5);
      }
      break;
    }
    case 'insect_leg': {
      // insects need numbers: the count gene sets 2-3 pairs per side
      // (4-6 legs total) staggered along the belly, front pairs reaching
      // forward and rear pairs raking back — enough struts for the weight
      const R = 0.2 + 0.1*girth;
      const chit = lp(CHITIN, o.skin, 0.35);
      const nP = 2 + Math.round(clamp(count, 0, 1));
      for (let p = 0; p < nP; p++) {
        const f = p / (nP - 1) - 0.5;                 // -0.5 front … +0.5 rear
        const z0 = S[2] + f * 2.6;
        const rake = f * 1.6;
        const hip  = [S[0], sock.len + 0.5, z0];
        const knee = [S[0] + side*1.5, sock.len + 1.15 + curl*0.8, z0 + rake*0.4];
        const shin = [S[0] + side*1.95, 0.6, z0 + rake*0.8];
        const foot = [S[0] + side*1.5, 0.0, z0 + rake];
        limbJoint(mb, hip, V.sub(knee, hip), R * 1.15);
        tube(mb, [hip, knee, shin, foot], [R*1.2, R, R*0.85, 0.05], chit, 0.4, 0, 7);
      }
      break;
    }
    case 'piston_leg': {
      // the mechanical lower body: the chassis pelvis is built by
      // buildPelvis; the count gene picks the drive unit —
      // low = TANK TREADS, high = ROBOT SPIDER LEGS
      if (count < 0.45) {
        const cy = sock.len * 0.42, hh = sock.len * 0.4;
        const TREAD = [40, 42, 48];
        ellipsoid(mb, [S[0], cy, 0.15], [0.72, hh, 2.4], TREAD, 0.35, 0, 12);
        for (let wI = -1; wI <= 1; wI++)               // road wheels
          tube(mb, [[S[0], cy * 0.6, 0.15 + wI * 1.3], [S[0] + side * 0.8, cy * 0.6, 0.15 + wI * 1.3]],
            [0.4, 0.4], METDK, 0.7, 0, 10, 2);
        ellipsoid(mb, [S[0], cy + hh * 0.8, 0.15], [0.8, 0.26, 2.55], METAL, 0.7, 0, 10);  // fender
        ellipsoid(mb, [S[0], cy, 2.5], [0.16, 0.16, 0.16], GLOW, 0.5, 1, 6);
        mb.glow([S[0], cy, 2.55], GLOW, 16);
      } else {
        for (const f of [-0.45, 0.45]) {               // two struts per side = 4 legs
          const z0 = S[2] + f * 2.2;
          const hip  = [S[0], sock.len + 0.25, z0];
          const knee = [S[0] + side * 1.9, sock.len + 0.95, z0 + f * 0.9];
          const shin = [S[0] + side * 2.35, 0.5, z0 + f * 1.5];
          const foot = [S[0] + side * 2.6, 0.0, z0 + f * 1.8];
          limbJoint(mb, hip, V.sub(knee, hip), 0.34);
          tube(mb, [hip, knee, shin, foot], [0.34, 0.26, 0.2, 0.05], METAL, 0.8, 0, 8);
          ellipsoid(mb, knee, [0.3, 0.3, 0.3], METDK, 0.7, 0, 6);   // knee actuator
        }
      }
      break;
    }
    case 'leg_stump': {
      ellipsoid(mb, [S[0], sock.len * 0.6, S[2]], [0.6, 0.55, 0.6], PALLOR, 0.25, 0, 8);
      ringStitch(mb, [S[0], sock.len * 0.35, S[2]], 0.55);
      break;
    }
  }
}

/** Chunky little arm from shoulder to a hanging wrist; returns wrist pos.
 * The arm dangles with a slight pendulum sway that grows toward the hand;
 * the builder's anim state is left at the wrist value so whatever the
 * caller attaches next (claws, gun, lance) swings along with it. */
function armDrop(mb, S, side, armR, scale, o, pg = [], N = null) {
  // The arm is shaped by the hand part's own genes, not a fixed tube:
  //   length -> arm length (high + short legs = knuckle-dragger),
  //   curl   -> elbow bend, taper -> forearm mass (low = popeye forearms),
  //   girth  -> bicep bulge.
  // The upper arm EXITS ALONG THE SOCKET NORMAL: a true shoulder joint, so
  // the limb clears the torso before gravity takes it.
  const len = pg[0] ?? 0.5, girth = pg[1] ?? 0.5, taper = pg[2] ?? 0.5, curl = pg[3] ?? 0.5;
  const n = N ?? [side, 0, 0];
  const armLen = (2.5 + 2.0 * len) * scale;
  const bend = 0.35 + curl * 0.75;
  const ex = V.add(S, V.scale(n, (armR * 1.6 + 0.25) * scale));
  const elbow = [ex[0] + side * 0.2 * scale, ex[1] - armLen * 0.44, ex[2] + 0.08];
  const wrist = [elbow[0] + side * 0.2 * scale, elbow[1] - armLen * 0.52, elbow[2] + 0.25 + bend * 0.4];
  const foreR = Math.max(armR * 0.55, armR * (1.2 - 0.8 * taper + 0.3 * girth));
  const phase = side * 1.3 + 2.0;
  const swing = (t) => [0, 0, 0.04 + 0.11 * t, phase];
  limbJoint(mb, S, n, armR * 1.15);   // brass retaining ring, on the normal
  // the shoulder ball itself — flesh, seated in the ring
  ellipsoid(mb, V.add(S, V.scale(n, armR * 0.55 * scale)),
    [armR*1.3, armR*1.2, armR*1.25], o.skinFn ? o.skin : CHITIN, 0.3, 0, 8, o.skinFn);
  tube(mb, [S, ex, elbow, wrist], [armR*1.25, armR*1.05, Math.max(armR*0.8, foreR*0.9), foreR],
    o.skinFn ? o.skin : CHITIN, 0.3, 0, 9, 1, null, swing);
  if (girth > 0.45) {                                // bicep bulge
    mb.setAnim(swing(0.35));
    const bi = [ex[0] + (elbow[0]-ex[0])*0.45, ex[1] + (elbow[1]-ex[1])*0.45, ex[2] + (elbow[2]-ex[2])*0.45];
    ellipsoid(mb, bi, [armR*(0.9+0.55*girth), armR*(1.0+0.5*girth), armR*(0.9+0.55*girth)],
      o.skinFn ? o.skin : CHITIN, 0.3, 0, 8, o.skinFn);
  }
  mb.setAnim(swing(1));
  return wrist;
}

/** Glossy toy eye with a hooded, skin-coloured upper lid. `hood` 0..1 sets
 * how heavily the lid droops — the b-movie menace dial. The lid carries a
 * blink weight: the shader slides it down over the eyeball on uBlink. */
function eyeball(mb, c, r, skin, hood = 0.4, N = [0, 0, 1]) {
  const base = mb.anim;
  const prevTex = mb.tex;
  mb.setTex(TEX_NONE);
  ellipsoid(mb, c, [r, r, r], EYEWH, 0.85, 0, 10);
  mb.setFx(r * 0.35);   // pupils drift with the gaze saccades
  ellipsoid(mb, [c[0] + N[0]*r*0.72, c[1] + N[1]*r*0.72, c[2] + N[2]*r*0.72],
    [r*0.38, r*0.38, r*0.38], PUPIL, 0.95, 0, 8);
  mb.setFx(0);
  if (hood > 0) {
    mb.setAnim([base[0], -r * 1.05, base[2], base[3]]);
    ellipsoid(mb, [c[0], c[1] + r*(0.62 - 0.28*hood), c[2] - r*0.10],
      [r*1.07, r*(0.42 + 0.34*hood), r*1.03], skin, 0.3, 0, 8);
    mb.setAnim(base);
  }
  mb.setTex(prevTex);
}

function ringStitch(mb, c, r) {
  const prevTex = mb.tex;
  mb.setTex(TEX_NONE);
  const pts = [];
  for (let i = 0; i <= 12; i++) {
    const a = (i / 12) * Math.PI * 2;
    pts.push([c[0] + Math.cos(a)*r, c[1], c[2] + Math.sin(a)*r]);
  }
  tube(mb, pts, pts.map(() => 0.07), STITCH, 0.1, 0, 4, 0);
  mb.setTex(prevTex);
}

// ── the skin atlas (painted once; detail costs 3 texture fetches) ───────────
// Seven tileable biological materials with shading BAKED into the tile:
// applying them as an albedo multiply gives relief without normal maps.
// Alpha carries the material's gloss boost (wet things shine).

function buildSkinAtlas() {
  const c = document.createElement('canvas');
  c.width = c.height = 1024;
  const x = c.getContext('2d');
  let seed = 424242;
  const rnd = () => ((seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);
  const gr = (v) => `rgb(${v},${v},${v})`;
  // draw wrapped 9× so random features tile seamlessly at 256
  const wrap = (fn) => { for (const dx of [-256, 0, 256]) for (const dy of [-256, 0, 256]) fn(dx, dy); };

  const tile = (name, paint) => {
    const [u, v] = TILE[name];
    const ox = u * 1024, oy = v * 1024;
    x.save();
    x.beginPath(); x.rect(ox, oy, 256, 256); x.clip();
    x.fillStyle = gr(135); x.fillRect(ox, oy, 256, 256);
    paint(ox, oy);
    x.restore();
  };

  tile('warts', (ox, oy) => {
    for (let i = 0; i < 70; i++) {
      const px = rnd() * 256, py = rnd() * 256, r = 5 + rnd() * 15;
      wrap((dx, dy) => {
        const g2 = x.createRadialGradient(ox+px+dx - r*0.3, oy+py+dy - r*0.3, r*0.15, ox+px+dx, oy+py+dy, r);
        g2.addColorStop(0, gr(178)); g2.addColorStop(0.7, gr(128)); g2.addColorStop(1, gr(97));
        x.fillStyle = g2;
        x.beginPath(); x.arc(ox+px+dx, oy+py+dy, r, 0, 7); x.fill();
      });
    }
    x.fillStyle = gr(106);
    for (let i = 0; i < 220; i++) x.fillRect(ox + rnd()*256, oy + rnd()*256, 2, 2);
  });

  tile('scales', (ox, oy) => {
    // overlapping rows drawn top-down so each row overlaps the one above
    for (let row = -1; row <= 8; row++)
      for (let col = -1; col <= 8; col++) {
        const px = ox + col*32 + (row & 1 ? 16 : 0), py = oy + row*32;
        const g2 = x.createLinearGradient(0, py - 21, 0, py + 21);
        g2.addColorStop(0, gr(172)); g2.addColorStop(0.6, gr(122)); g2.addColorStop(1, gr(76));
        x.fillStyle = g2;
        x.beginPath(); x.arc(px, py, 21, 0, 7); x.fill();
        x.strokeStyle = gr(66); x.lineWidth = 1.5;
        x.beginPath(); x.arc(px, py, 21, 0.15*Math.PI, 0.85*Math.PI); x.stroke();
      }
  });

  tile('slick', (ox, oy) => {
    x.fillStyle = gr(142); x.fillRect(ox, oy, 256, 256);
    for (let i = 0; i < 14; i++) {
      const px = rnd()*256, py = rnd()*256, r = 30 + rnd()*55, lite = rnd() > 0.5;
      wrap((dx, dy) => {
        const g2 = x.createRadialGradient(ox+px+dx, oy+py+dy, r*0.1, ox+px+dx, oy+py+dy, r);
        g2.addColorStop(0, `rgba(${lite ? 185 : 98},${lite ? 185 : 98},${lite ? 185 : 98},0.32)`);
        g2.addColorStop(1, 'rgba(0,0,0,0)');
        x.fillStyle = g2;
        x.beginPath(); x.arc(ox+px+dx, oy+py+dy, r, 0, 7); x.fill();
      });
    }
  });

  tile('feathers', (ox, oy) => {
    x.lineCap = 'round';
    for (let row = -1; row <= 16; row++)
      for (let col = -1; col <= 16; col++) {
        const px = ox + col*16 + (row & 1 ? 8 : 0), py = oy + row*16;
        const tilt = ((col & 3) - 1.5) * 2.2;             // periodic, so it tiles
        x.strokeStyle = gr(((row + col) & 1) ? 158 : 96);
        x.lineWidth = 5;
        x.beginPath();
        x.moveTo(px, py);
        x.quadraticCurveTo(px + tilt, py + 10, px + tilt*1.6, py + 18);
        x.stroke();
      }
  });

  tile('chitin', (ox, oy) => {
    for (let row = -1; row <= 4; row++)
      for (let col = -1; col <= 4; col++) {
        const px = ox + col*64 + (row & 1 ? 32 : 0), py = oy + row*64;
        const g2 = x.createLinearGradient(0, py, 0, py + 60);
        g2.addColorStop(0, gr(158)); g2.addColorStop(1, gr(96));
        x.fillStyle = g2;
        x.beginPath();
        x.roundRect(px + 3, py + 3, 58, 58, 12);
        x.fill();
        x.strokeStyle = gr(58); x.lineWidth = 3; x.stroke();
      }
  });

  tile('ridge', (ox, oy) => {
    for (let yy = 0; yy < 256; yy += 16) {
      x.fillStyle = gr(154); x.fillRect(ox, oy + yy, 256, 7);
      x.fillStyle = gr(112); x.fillRect(ox, oy + yy + 7, 256, 7);
      x.fillStyle = gr(86);  x.fillRect(ox, oy + yy + 14, 256, 2);
    }
  });

  // 'none' stays neutral grey (amp 0 makes it moot anyway)
  tile('none', () => {});

  // bake each material's gloss boost into the alpha channel; return raw
  // ImageData so the canvas's premultiplied alpha never touches the rgb
  const img = x.getImageData(0, 0, 1024, 1024);
  const d = img.data;
  for (const [k, [u, v]] of Object.entries(TILE)) {
    const a = TILE_SPEC[k];
    for (let py = v * 1024; py < v * 1024 + 256; py++) {
      let o4 = (py * 1024 + u * 1024) * 4 + 3;
      for (let px = 0; px < 256; px++, o4 += 4) d[o4] = a;
    }
  }
  return img;
}

// ── the painted backdrop (Canvas 2D, built once, uploaded as texture) ───────

function skyCol(t) {
  if (t < 0.45) return lp([8, 6, 26], [24, 14, 52], t / 0.45);
  if (t < 0.75) return lp([24, 14, 52], [56, 28, 74], (t - 0.45) / 0.3);
  return lp([56, 28, 74], [116, 58, 96], (t - 0.75) / 0.25);
}

const B4 = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]];

function buildBackground() {
  const c = document.createElement('canvas');
  c.width = BW; c.height = BH;
  const ctx = c.getContext('2d');
  const img = ctx.createImageData(BW, BH);
  const px = img.data;

  let seed = 987654321;
  const rnd = () => ((seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);

  const MX = 252, MY = 44, MR = 26;
  const CRATERS = [[-8,-6,5],[6,4,4],[-2,9,3],[10,-9,4],[-14,3,3],[3,-14,2]];
  const STARS = [];
  for (let i = 0; i < 110; i++) {
    const x = Math.floor(rnd() * BW), y = Math.floor(rnd() * (HORIZON - 20));
    if (Math.hypot(x - MX, y - MY) < MR + 12) continue;
    STARS.push([x, y, rnd()]);
  }

  const SKY_STEPS = 26, GND_STEPS = 12;
  for (let y = 0; y < BH; y++) {
    for (let x = 0; x < BW; x++) {
      const bay = (B4[y & 3][x & 3] + 0.5) / 16;
      let col;
      if (y < HORIZON) {
        let t = y / HORIZON + (bay - 0.5) / SKY_STEPS;
        t = clamp(t, 0, 1);
        col = skyCol(Math.floor(t * SKY_STEPS) / SKY_STEPS);
        const d = Math.hypot(x - MX, y - MY);
        if (d <= MR) {
          const f = d / MR;
          col = lp([232, 230, 208], [172, 170, 160], f * f);
          for (const [cx, cy, cr] of CRATERS)
            if (Math.hypot(x - MX - cx, y - MY - cy) < cr) col = [203, 199, 178];
        } else if (d < MR + 34) {
          const gg = (1 - (d - MR) / 34) ** 2;
          if (gg * 0.55 > bay * 0.35) col = lp(col, [150, 140, 200], gg * 0.55);
        }
      } else {
        let t = (y - HORIZON) / (BH - HORIZON) + (bay - 0.5) / GND_STEPS;
        t = clamp(t, 0, 1);
        col = lp([38, 30, 58], [70, 56, 88], Math.floor(t * GND_STEPS) / GND_STEPS);
        const w = 8 + (y - HORIZON) * 1.1;
        const dx = Math.abs(x - MX);
        if (dx < w) {
          const s = (1 - dx / w) * 0.5;
          if (s > bay * 0.6) col = lp(col, [130, 105, 130], s);
        }
      }
      const vx = (x - 160) / 160, vy = (y - 100) / 100;
      const vq = vx * vx + vy * vy;
      const f = Math.max(0.62, 1 - 0.38 * Math.max(0, vq - 0.55));
      const o = (y * BW + x) * 4;
      px[o] = col[0] * f; px[o+1] = col[1] * f; px[o+2] = col[2] * f; px[o+3] = 255;
    }
  }
  for (const [sx, sy, b] of STARS) {
    const o = (sy * BW + sx) * 4;
    const v = 120 + Math.floor(b * 135);
    px[o] = v; px[o+1] = v; px[o+2] = Math.min(255, v + 25); px[o+3] = 255;
    if (b > 0.88 && sx > 0 && sx < BW - 1 && sy > 0 && sy < HORIZON - 1) {
      for (const [ox, oy] of [[1,0],[-1,0],[0,1],[0,-1]]) {
        const oo = ((sy + oy) * BW + sx + ox) * 4;
        px[oo] = 110; px[oo+1] = 110; px[oo+2] = 150; px[oo+3] = 255;
      }
    }
  }
  ctx.putImageData(img, 0, 0);

  ctx.fillStyle = '#100a24';
  for (let x = 0; x < 150; x++) {
    const h = Math.round(30 * Math.exp(-((x - 70) ** 2) / 2800));
    ctx.fillRect(x, HORIZON - h, 1, h);
  }
  ctx.fillStyle = '#0e0922';
  ctx.fillRect(52, 96, 24, 30);
  ctx.fillRect(46, 88, 9, 38);
  ctx.fillRect(74, 92, 9, 34);
  ctx.fillRect(44, 84, 13, 4);
  ctx.fillRect(72, 88, 13, 4);
  for (let i = 0; i < 6; i++) ctx.fillRect(52 + i * 4, 93, 2, 3);
  ctx.fillStyle = 'rgb(255,190,90)';
  ctx.fillRect(60, 106, 2, 3);
  ctx.fillRect(67, 112, 2, 3);
  ctx.fillRect(49, 97, 2, 2);
  ctx.fillStyle = 'rgb(140,220,140)';
  ctx.fillRect(78, 101, 2, 3);

  ctx.strokeStyle = '#0c0820';
  ctx.lineWidth = 3;
  ctx.beginPath(); ctx.moveTo(297, 200); ctx.quadraticCurveTo(292, 150, 288, 118); ctx.stroke();
  ctx.lineWidth = 2;
  ctx.beginPath(); ctx.moveTo(290, 132); ctx.quadraticCurveTo(276, 116, 268, 108); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(289, 124); ctx.quadraticCurveTo(302, 106, 310, 100); ctx.stroke();
  ctx.lineWidth = 1;
  ctx.beginPath(); ctx.moveTo(288, 118); ctx.quadraticCurveTo(284, 100, 286, 92); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(272, 112); ctx.quadraticCurveTo(266, 104, 264, 98); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(306, 103); ctx.quadraticCurveTo(314, 96, 318, 94); ctx.stroke();

  const cloud = (cx, cy, rx, ry, aBody, aEdge) => {
    ctx.fillStyle = `rgba(22,16,44,${aBody})`;
    ctx.beginPath(); ctx.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = `rgba(170,160,215,${aEdge})`;
    ctx.beginPath(); ctx.ellipse(cx, cy - ry + 1, rx * 0.9, 1.5, 0, 0, Math.PI * 2); ctx.fill();
  };
  cloud(130, 66, 62, 6, 0.65, 0.10);
  cloud(250, 58, 48, 5, 0.85, 0.30);
  cloud(60, 40, 44, 5, 0.55, 0.08);

  ctx.strokeStyle = 'rgba(14,9,28,0.4)';
  ctx.lineWidth = 1;
  for (let k = -5; k <= 5; k++) {
    ctx.beginPath();
    ctx.moveTo(160 + k * 9, HORIZON);
    ctx.lineTo(160 + k * 78, BH);
    ctx.stroke();
  }
  for (const dy of [3, 7, 13, 21, 32, 45]) {
    ctx.beginPath(); ctx.moveTo(0, HORIZON + dy); ctx.lineTo(BW, HORIZON + dy); ctx.stroke();
  }
  const mist = ctx.createLinearGradient(0, HORIZON - 8, 0, HORIZON + 8);
  mist.addColorStop(0, 'rgba(96,86,130,0)');
  mist.addColorStop(0.5, 'rgba(96,86,130,0.28)');
  mist.addColorStop(1, 'rgba(96,86,130,0)');
  ctx.fillStyle = mist;
  ctx.fillRect(0, HORIZON - 8, BW, 16);

  ctx.fillStyle = '#221839';
  ctx.beginPath(); ctx.ellipse(DAIS.x, DAIS.y + 5, 90, 24, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#241a3a';
  ctx.beginPath(); ctx.ellipse(DAIS.x, DAIS.y + 3, 88, 24, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#463862';
  ctx.beginPath(); ctx.ellipse(DAIS.x, DAIS.y - 2, 86, 22, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#544472';
  ctx.beginPath(); ctx.ellipse(DAIS.x + 6, DAIS.y - 3, 64, 15, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#5e4c82';
  ctx.beginPath(); ctx.ellipse(DAIS.x + 8, DAIS.y - 4, 42, 9, 0, 0, Math.PI * 2); ctx.fill();
  ctx.strokeStyle = '#2c2148';
  ctx.beginPath(); ctx.ellipse(DAIS.x, DAIS.y - 2, 74, 18, 0, 0, Math.PI * 2); ctx.stroke();
  for (let a = 0; a < 12; a++) {
    const th = (a / 12) * Math.PI * 2;
    ctx.fillStyle = '#2c2148';
    ctx.fillRect(Math.round(DAIS.x + Math.cos(th) * 80) - 1, Math.round(DAIS.y - 2 + Math.sin(th) * 20) - 1, 2, 2);
  }
  return c;
}

// ── shaders ─────────────────────────────────────────────────────────────────

const VS_CREATURE = `
attribute vec3 aPos, aNor, aCol, aMat;
attribute vec4 aTex, aAnim;
uniform mat4 uPV;
uniform float uCos, uSin;
uniform float uTime, uBreath, uBlink, uTongue;
uniform vec2 uGaze;
varying vec3 vNor, vCol, vPos, vMat, vLoc, vLNor;
varying vec4 vTex;
void main() {
  vLoc = aPos; vLNor = aNor; vTex = aTex;
  vec3 lp = aPos;
  lp += aNor * (aAnim.x * uBreath);                          // breathing
  lp.y += max(aAnim.y, 0.0) * sin(uTime * 2.6 + aAnim.w);    // traveling sinusoidal flap
  lp.y -= max(-aAnim.y, 0.0) * uBlink;                       // eyelid blink
  float sway = aAnim.z * sin(uTime * 1.4 + aAnim.w);         // pendulum sway
  lp.x += sway * 0.6;
  lp.z += aAnim.z * cos(uTime * 1.1 + aAnim.w) * 0.35;
  float fx = aMat.z;
  lp.x += max(fx, 0.0) * uGaze.x;                            // pupils drift (saccades)
  lp.y += max(fx, 0.0) * uGaze.y * 0.6;
  lp.z += max(-fx, 0.0) * uTongue;                           // tongue darts
  vec3 p = vec3(lp.x*uCos - lp.z*uSin, lp.y, lp.x*uSin + lp.z*uCos);
  vec3 n = vec3(aNor.x*uCos - aNor.z*uSin, aNor.y, aNor.x*uSin + aNor.z*uCos);
  vNor = n; vCol = aCol; vMat = aMat; vPos = p;
  gl_Position = uPV * vec4(p, 1.0);
}`;

const FS_CREATURE = `
precision mediump float;
varying vec3 vNor, vCol, vPos, vMat, vLoc, vLNor;
varying vec4 vTex;
uniform vec3 uEye;
uniform float uFlash, uPulse;
uniform sampler2D uSkin;
void main() {
  vec3 n = normalize(vNor);
  if (!gl_FrontFacing) n = -n;                      // two-sided: nothing inverts

  // triplanar skin sample in LOCAL space — the detail sticks to the body
  // as it turns. Tile shading is pre-baked, so this is pure albedo cost.
  vec3 w = abs(vLNor) + 1e-3;
  w /= (w.x + w.y + w.z);
  vec2 t0 = vTex.xy + 0.004;
  float s = vTex.z;
  vec4 tx = texture2D(uSkin, t0 + fract(vLoc.zy * s) * 0.242) * w.x
          + texture2D(uSkin, t0 + fract(vLoc.xz * s) * 0.242) * w.y
          + texture2D(uSkin, t0 + fract(vLoc.xy * s) * 0.242) * w.z;
  vec3 col = vCol * mix(1.0, tx.r * 1.9, vTex.w);
  float gls = clamp(vMat.x + tx.a * vTex.w, 0.0, 1.0);

  vec3 view = normalize(uEye - vPos);
  vec3 key  = normalize(vec3(-0.5, 0.75, 0.65));
  vec3 moon = normalize(vec3(0.65, 0.30, -0.55));
  float d = max(dot(n, key), 0.0);
  float band = smoothstep(0.02, 0.12, d) * 0.42
             + smoothstep(0.30, 0.42, d) * 0.36
             + smoothstep(0.62, 0.74, d) * 0.22;    // 3-step toon ramp
  vec3 hemi = mix(vec3(0.17, 0.13, 0.25), vec3(0.34, 0.30, 0.44), n.y * 0.5 + 0.5);
  vec3 lit = col * (hemi + vec3(1.0, 0.93, 0.80) * (band * 1.15 + uFlash));
  vec3 h = normalize(view + key);                   // sheen (wet skin shines)
  lit += vec3(1.0, 0.97, 0.9) * pow(max(dot(n, h), 0.0), mix(14.0, 90.0, gls))
       * (0.18 + 0.5 * gls);
  float rim = pow(1.0 - max(dot(n, view), 0.0), 2.4) * max(dot(n, moon), 0.0);
  lit += vec3(0.45, 0.55, 0.95) * rim * (0.55 + uFlash);
  lit = mix(lit, vCol * (1.15 + 0.25 * uPulse), vMat.y);   // emissive
  gl_FragColor = vec4(lit, 1.0);
}`;

const VS_QUAD = `
attribute vec2 aPos, aUV;
varying vec2 vUV;
void main() { vUV = aUV; gl_Position = vec4(aPos, 0.0, 1.0); }`;

const FS_QUAD = `
precision mediump float;
varying vec2 vUV;
uniform sampler2D uTex;
uniform vec4 uColor;
uniform float uUseTex;
void main() {
  vec4 t = uUseTex > 0.5 ? texture2D(uTex, vUV) : vec4(1.0);
  gl_FragColor = vec4(t.rgb * uColor.rgb, t.a * uColor.a);
}`;

const VS_GLOW = `
attribute vec3 aPos, aCol;
attribute float aSize;
uniform mat4 uPV;
uniform float uCos, uSin;
varying vec3 vCol;
void main() {
  vec3 p = vec3(aPos.x*uCos - aPos.z*uSin, aPos.y, aPos.x*uSin + aPos.z*uCos);
  vCol = aCol;
  gl_Position = uPV * vec4(p, 1.0);
  gl_PointSize = aSize;
}`;

const FS_GLOW = `
precision mediump float;
varying vec3 vCol;
uniform float uPulse;
void main() {
  float d = length(gl_PointCoord - 0.5) * 2.0;
  float a = max(0.0, 1.0 - d);
  gl_FragColor = vec4(vCol * a * a * (0.5 + 0.3 * uPulse), 0.0);
}`;

// ── renderer ────────────────────────────────────────────────────────────────

let R = null;   // { gl, programs, buffers, ... }
let _raf = null;
let _frame = 0;
let _theta = 0;
const ROT_SPEED = 0.008;
const FLASH_CYCLE = 900;

// Natural blink cadence: snap shut (~4 frames), brief hold, ease open
// (~10 frames), at randomized 2–6 s intervals with occasional double
// blinks. Deterministic per creature (seeded from its mesh).
const _blink = { next: 130, phase: -1, t: 0, seed: 1 };

function blinkRnd() {
  _blink.seed = (_blink.seed * 1103515245 + 12345) >>> 0;
  return _blink.seed / 4294967296;
}

function blinkLevel() {
  const B = _blink;
  if (B.phase < 0) {
    if (--B.next <= 0) { B.phase = 0; B.t = 0; }
    return 0;
  }
  const t = B.t++;
  if (t < 4) {                     // snap shut
    const x = t / 4;
    return x * x;
  }
  if (t < 7) return 1;             // hold
  if (t < 17) {                    // ease open
    const x = 1 - (t - 7) / 10;
    return x * x * (3 - 2 * x);
  }
  B.phase = -1;
  B.next = blinkRnd() < 0.25 ? 20 : 120 + Math.floor(blinkRnd() * 240);
  return 0;
}

// Gaze: pupils saccade — a quick eased hop to a new target, then a long
// hold. All eyes move together (conjugate gaze).
const _gaze = { cur: [0, 0], from: [0, 0], to: [0, 0], t: 99, next: 80 };

function gazeLevel() {
  const G = _gaze;
  if (--G.next <= 0) {
    G.from = [...G.cur];
    G.to = [(blinkRnd() * 2 - 1), (blinkRnd() * 2 - 1) * 0.5];
    G.t = 0;
    G.next = 70 + Math.floor(blinkRnd() * 160);
  }
  if (G.t < 7) {
    const x = ++G.t / 7;
    const s = x * x * (3 - 2 * x);
    G.cur = [G.from[0] + (G.to[0] - G.from[0]) * s, G.from[1] + (G.to[1] - G.from[1]) * s];
  }
  return G.cur;
}

// Tongue flicker: out–dip–out–in over ~24 frames, every 4–8 s. Only the
// serpentine carries tongue geometry; elsewhere the uniform drives nothing.
const _tongue = { t: 99, next: 300 };

function tongueLevel() {
  const T = _tongue;
  if (T.t > 24) {
    if (--T.next <= 0) { T.t = 0; T.next = 240 + Math.floor(blinkRnd() * 240); }
    return 0;
  }
  const t = T.t++;
  if (t < 5)  { const x = t / 5; return x * x * (3 - 2 * x); }
  if (t < 9)  return 0.45;
  if (t < 15) return 1;
  const x = 1 - (t - 15) / 9;
  return x * x;
}

function makeProgram(gl, vsSrc, fsSrc) {
  const mk = (type, src) => {
    const s = gl.createShader(type);
    gl.shaderSource(s, src);
    gl.compileShader(s);
    if (!gl.getShaderParameter(s, gl.COMPILE_STATUS))
      throw new Error(gl.getShaderInfoLog(s) || 'shader error');
    return s;
  };
  const p = gl.createProgram();
  gl.attachShader(p, mk(gl.VERTEX_SHADER, vsSrc));
  gl.attachShader(p, mk(gl.FRAGMENT_SHADER, fsSrc));
  gl.linkProgram(p);
  if (!gl.getProgramParameter(p, gl.LINK_STATUS))
    throw new Error(gl.getProgramInfoLog(p) || 'link error');
  return p;
}

function setupGL(canvas) {
  const gl = canvas.getContext('webgl', { antialias: true, alpha: false });
  if (!gl) throw new Error('WebGL unavailable');

  const progC = makeProgram(gl, VS_CREATURE, FS_CREATURE);
  const progQ = makeProgram(gl, VS_QUAD, FS_QUAD);
  const progG = makeProgram(gl, VS_GLOW, FS_GLOW);

  // backdrop texture (nearest: keep the chunky pixels)
  const bgTex = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, bgTex);
  gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, buildBackground());
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

  // skin atlas (unit 1): tileable biological materials, LINEAR, no mips
  const skinTex = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, skinTex);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, buildSkinAtlas());
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

  // soft radial texture for the contact shadow
  const sc = document.createElement('canvas');
  sc.width = sc.height = 64;
  const sctx = sc.getContext('2d');
  const grad = sctx.createRadialGradient(32, 32, 2, 32, 32, 32);
  grad.addColorStop(0, 'rgba(255,255,255,1)');
  grad.addColorStop(1, 'rgba(255,255,255,0)');
  sctx.fillStyle = grad;
  sctx.fillRect(0, 0, 64, 64);
  const shTex = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, shTex);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, sc);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

  // fullscreen-quad buffer (pos2 uv2), reused for bg / shadow / flash
  const quadBuf = gl.createBuffer();

  // camera: frames feet-on-dais against the painted backdrop
  const proj = perspective(28 * Math.PI / 180, CW / CH, 1, 120);
  const eye = [0, 7.4, 30];
  const view = lookAt(eye, [0, 5.7, 0], [0, 1, 0]);
  const pv = mat4mul(proj, view);

  return {
    gl, progC, progQ, progG, bgTex, shTex, skinTex, quadBuf,
    pv: new Float32Array(pv), eye,
    meshBuf: gl.createBuffer(), idxBuf: gl.createBuffer(), glowBuf: gl.createBuffer(),
    idxCount: 0, glowCount: 0, maxR: 3,
  };
}

function uploadCreature(genome) {
  const { gl } = R;
  let mb;
  try { mb = buildCreature(genome); }
  catch (e) { console.error('creature build failed:', e); mb = new MeshB(); }

  gl.bindBuffer(gl.ARRAY_BUFFER, R.meshBuf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(mb.v), gl.STATIC_DRAW);
  const idx = mb.idx.length < 65000 ? new Uint16Array(mb.idx) : new Uint16Array(mb.idx.slice(0, 64998));
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, R.idxBuf);
  gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, idx, gl.STATIC_DRAW);
  R.idxCount = idx.length;

  const gf = [];
  for (const g of mb.glows) gf.push(...g);
  gl.bindBuffer(gl.ARRAY_BUFFER, R.glowBuf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(gf), gl.STATIC_DRAW);
  R.glowCount = mb.glows.length;

  let mr = 2.5;
  for (let i = 0; i < mb.v.length; i += 20)
    mr = Math.max(mr, Math.hypot(mb.v[i], mb.v[i + 2]));
  R.maxR = Math.min(mr, 13);

  // per-creature rhythms
  _blink.seed = (mb.v.length * 2654435761) >>> 0 || 1;
  _blink.next = 60 + Math.floor(blinkRnd() * 180);
  _blink.phase = -1;
  _gaze.cur = [0, 0]; _gaze.t = 99;
  _gaze.next = 50 + Math.floor(blinkRnd() * 120);
  _tongue.t = 99;
  _tongue.next = 200 + Math.floor(blinkRnd() * 200);
}

function drawQuad(x0, y0, x1, y1, u0, v0, u1, v1, tex, color, useTex) {
  const { gl, progQ, quadBuf } = R;
  gl.useProgram(progQ);
  gl.bindBuffer(gl.ARRAY_BUFFER, quadBuf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([
    x0, y0, u0, v1,  x1, y0, u1, v1,  x1, y1, u1, v0,
    x0, y0, u0, v1,  x1, y1, u1, v0,  x0, y1, u0, v0,
  ]), gl.DYNAMIC_DRAW);
  const aP = gl.getAttribLocation(progQ, 'aPos');
  const aU = gl.getAttribLocation(progQ, 'aUV');
  gl.enableVertexAttribArray(aP); gl.vertexAttribPointer(aP, 2, gl.FLOAT, false, 16, 0);
  gl.enableVertexAttribArray(aU); gl.vertexAttribPointer(aU, 2, gl.FLOAT, false, 16, 8);
  gl.uniform4fv(gl.getUniformLocation(progQ, 'uColor'), color);
  gl.uniform1f(gl.getUniformLocation(progQ, 'uUseTex'), useTex ? 1 : 0);
  if (tex) {
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.uniform1i(gl.getUniformLocation(progQ, 'uTex'), 0);
  }
  gl.drawArrays(gl.TRIANGLES, 0, 6);
}

function drawFrame() {
  const { gl } = R;
  const fc = _frame % FLASH_CYCLE;
  const flash = fc < 3 ? 0.30 : (fc >= 8 && fc < 11) ? 0.17 : 0;
  const pulse = Math.sin(_frame * 0.05);

  gl.viewport(0, 0, CW, CH);
  gl.disable(gl.DEPTH_TEST);
  gl.disable(gl.BLEND);
  gl.disable(gl.CULL_FACE);

  // backdrop
  drawQuad(-1, -1, 1, 1, 0, 0, 1, 1, R.bgTex, [1 + flash, 1 + flash, 1 + flash * 1.2, 1], true);

  // contact shadow on the dais (screen-space, scaled by creature radius)
  gl.enable(gl.BLEND);
  gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
  const pxPerUnit = 15.4;                      // matches the camera framing
  const sw = (R.maxR * pxPerUnit + 14) / BW * 2;
  const shx = (DAIS.x / BW) * 2 - 1, shy = 1 - (DAIS.y / BH) * 2;
  drawQuad(shx - sw, shy - sw * 0.26, shx + sw, shy + sw * 0.26, 0, 0, 1, 1,
    R.shTex, [0.02, 0.01, 0.06, 0.55], true);
  gl.disable(gl.BLEND);

  // creature
  gl.enable(gl.DEPTH_TEST);
  gl.clear(gl.DEPTH_BUFFER_BIT);
  const p = R.progC;
  gl.useProgram(p);
  gl.bindBuffer(gl.ARRAY_BUFFER, R.meshBuf);
  const stride = 80;
  const attr = (name, size, off) => {
    const a = gl.getAttribLocation(p, name);
    gl.enableVertexAttribArray(a);
    gl.vertexAttribPointer(a, size, gl.FLOAT, false, stride, off);
  };
  attr('aPos', 3, 0); attr('aNor', 3, 12); attr('aCol', 3, 24); attr('aMat', 3, 36);
  attr('aTex', 4, 48); attr('aAnim', 4, 64);
  gl.activeTexture(gl.TEXTURE1);
  gl.bindTexture(gl.TEXTURE_2D, R.skinTex);
  gl.uniform1i(gl.getUniformLocation(p, 'uSkin'), 1);
  gl.activeTexture(gl.TEXTURE0);
  const t = _frame / 60;
  const breathRaw = (Math.sin(t * 1.65) + 1) / 2;
  const breath = breathRaw * breathRaw * (3 - 2 * breathRaw);   // eased in-out
  gl.uniformMatrix4fv(gl.getUniformLocation(p, 'uPV'), false, R.pv);
  gl.uniform1f(gl.getUniformLocation(p, 'uCos'), Math.cos(_theta));
  gl.uniform1f(gl.getUniformLocation(p, 'uSin'), Math.sin(_theta));
  gl.uniform3fv(gl.getUniformLocation(p, 'uEye'), R.eye);
  gl.uniform1f(gl.getUniformLocation(p, 'uFlash'), flash);
  gl.uniform1f(gl.getUniformLocation(p, 'uPulse'), pulse);
  gl.uniform1f(gl.getUniformLocation(p, 'uTime'), t);
  gl.uniform1f(gl.getUniformLocation(p, 'uBreath'), breath);
  gl.uniform1f(gl.getUniformLocation(p, 'uBlink'), blinkLevel());
  const gz = gazeLevel();
  gl.uniform2f(gl.getUniformLocation(p, 'uGaze'), gz[0], gz[1]);
  gl.uniform1f(gl.getUniformLocation(p, 'uTongue'), tongueLevel());
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, R.idxBuf);
  gl.drawElements(gl.TRIANGLES, R.idxCount, gl.UNSIGNED_SHORT, 0);

  // glow sprites (additive, over everything but depth-tested)
  if (R.glowCount) {
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE);
    gl.depthMask(false);
    const q = R.progG;
    gl.useProgram(q);
    gl.bindBuffer(gl.ARRAY_BUFFER, R.glowBuf);
    const ga = (name, size, off) => {
      const a = gl.getAttribLocation(q, name);
      gl.enableVertexAttribArray(a);
      gl.vertexAttribPointer(a, size, gl.FLOAT, false, 28, off);
    };
    ga('aPos', 3, 0); ga('aCol', 3, 12); ga('aSize', 1, 24);
    gl.uniformMatrix4fv(gl.getUniformLocation(q, 'uPV'), false, R.pv);
    gl.uniform1f(gl.getUniformLocation(q, 'uCos'), Math.cos(_theta));
    gl.uniform1f(gl.getUniformLocation(q, 'uSin'), Math.sin(_theta));
    gl.uniform1f(gl.getUniformLocation(q, 'uPulse'), pulse);
    gl.drawArrays(gl.POINTS, 0, R.glowCount);
    gl.depthMask(true);
    gl.disable(gl.BLEND);
  }

  // lightning wash
  if (flash > 0) {
    gl.disable(gl.DEPTH_TEST);
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    drawQuad(-1, -1, 1, 1, 0, 0, 1, 1, null, [0.88, 0.9, 1, flash], false);
    gl.disable(gl.BLEND);
  }
}

// ── public API ──────────────────────────────────────────────────────────────

export function initRenderer(canvas, genome) {
  destroyRenderer();
  canvas.width = CW;
  canvas.height = CH;
  try {
    R = setupGL(canvas);
    uploadCreature(genome);
  } catch (e) {
    console.error('renderer init failed:', e);
    R = null;
    return;
  }
  const loop = () => {
    _frame++;
    _theta += ROT_SPEED;
    try { drawFrame(); } catch (e) { console.error(e); destroyRenderer(); return; }
    _raf = requestAnimationFrame(loop);
  };
  _raf = requestAnimationFrame(loop);
}

export function updateGenome(genome) {
  if (!R) return;
  uploadCreature(genome);
}

export function destroyRenderer() {
  if (_raf !== null) { cancelAnimationFrame(_raf); _raf = null; }
  R = null;
}
