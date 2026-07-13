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
  panels:   [0.75, 0.25],   // riveted metal plate — the tin-toy robot hide
  veins:    [0.00, 0.50],   // veined membrane — the 1950s brain-alien hide
};
const TILE_SPEC = { warts: 28, scales: 90, slick: 215, feathers: 18, chitin: 110, ridge: 50, none: 0, panels: 190, veins: 120 };
const TEX_NONE = [TILE.none[0], TILE.none[1], 1, 0];

const GAIT0 = [0, 0, 0, 0];

class MeshB {
  constructor() {
    this.v = []; this.idx = []; this.glows = [];
    this.anim = ANIM0; this.fx = 0; this.tex = TEX_NONE; this.gait = GAIT0; this.alpha = 1;
    this.heart = 0; this.organ = 0;
  }
  setAnim(a) { this.anim = a; }
  setFx(f) { this.fx = f; }
  /** [tileU, tileV, tilingScale, amplitude] — which skin material, how
   * dense its grain, and how strongly it shows. */
  setTex(t) { this.tex = t; }
  /** Locomotion channel: [zSwing, yLift, phase, bob]. Legs swing fore-aft
   * and lift on alternating phases; bodies bob and roll; serpents put a
   * traveling phase here to slither harder when moving. */
  setGait(gt) { this.gait = gt; }
  /** Opacity, 1 = opaque (default). Below 1 the surface blends with
   * whatever was drawn before it at that pixel — glass jars, force
   * fields. Order matters: draw what should show THROUGH the glass
   * first, then the glass. */
  setAlpha(a) { this.alpha = a; }
  /** Heartbeat pulse amplitude (scale along the normal), 0 = doesn't
   * beat (default). Runs on its own speed-linked clock (uHeartBeat,
   * driven by gaitStep()) rather than the shared breathing/gait channels,
   * since a heart keeps beating at rest when neither of those animate. */
  setHeart(h) { this.heart = h; }
  /** Suspension weight, 0 = rigidly on the torso (default). >0 tags a
   * vertex as belonging to a loose internal organ (heart, stomach, gut)
   * that gets pushed by a damped spring (uOrganOffset) instead of
   * riding the torso exactly -- see organLevel(). */
  setOrgan(o) { this.organ = o; }
  vert(p, n, c, g, e) {
    const a = this.anim, t = this.tex, gt = this.gait;
    this.v.push(p[0], p[1], p[2], n[0], n[1], n[2], c[0]/255, c[1]/255, c[2]/255, g, e, this.fx,
      t[0], t[1], t[2], t[3],
      a[0], a[1], a[2], a[3],
      gt[0], gt[1], gt[2], gt[3],
      this.alpha, this.heart, this.organ);
    return this.v.length / 27 - 1;
  }
  tri(a, b, c) { this.idx.push(a, b, c); }
  quad(a, b, c, d) { this.idx.push(a, b, c, a, c, d); }
  glow(p, c, size) { this.glows.push([p[0], p[1], p[2], c[0]/255, c[1]/255, c[2]/255, size]); }
}

/** Ellipsoid at c with radii r. colorFn(unitPos) may vary the colour;
 * animFn(unitPos) may vary the anim channel across the surface. */
// ── level of detail ──────────────────────────────────────────────────────────
// A single tessellation dial, read by every geometry primitive below.
// buildCreature() measures the mesh it produces at full detail and, if a
// busy build (many legs, faction hardware, a mastermind brain) pushes past
// the triangle budget, lowers this and rebuilds — the docs/08 mobile
// perf budget (~8k tris) applies to the Lab too, not just the match sim,
// and it structurally prevents ever needing more than a 16-bit index
// buffer (the root cause of the "legs on one side" bug) instead of just
// tolerating an oversized mesh.
let _detail = 1;
function segFor(n, floor) { return Math.max(floor, Math.round(n * _detail)); }

function ellipsoid(mb, c, r, col, gloss = 0.25, emis = 0, seg = 14, colorFn = null, animFn = null) {
  seg = segFor(seg, 3);
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
function tube(mb, path, radii, col, gloss = 0.25, emis = 0, sides = 10, caps = 3, colorFn = null, animFn = null, gaitFn = null) {
  sides = segFor(sides, 4);
  const prevAnim = mb.anim, prevGait = mb.gait;
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
    if (gaitFn) mb.setGait(gaitFn(t));
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
    if (gaitFn) mb.setGait(gaitFn(k ? 1 : 0));
    const nrm = V.scale(T[k], dirSign);
    const cv = mb.vert(P[k], nrm, colorFn ? colorFn(k ? 1 : 0) : col, gloss, emis);
    for (let j = 0; j < sides; j++)
      dirSign > 0 ? mb.tri(cv, rows[k][j], rows[k][j+1]) : mb.tri(cv, rows[k][j+1], rows[k][j]);
  }
  mb.setAnim(prevAnim);
  mb.setGait(prevGait);   // was previously left dangling on the last gaitFn sample
}

/** A torus (hoop) centred at `center`, hole aligned to `axis` — the
 * actual geometry of a brace clamped AROUND a limb: the limb passes
 * through the hole, the band wraps the full 360° around it. This is
 * the 90°-rotated sweep from a plain tube: instead of translating a
 * ring along the axis (a solid capped puck sitting beside the limb),
 * the small cross-section revolves around a big circle perpendicular
 * to the axis, so the surface genuinely encircles the shaft. */
function torus(mb, center, axis, majorR, minorR, col, gloss = 0.4, emis = 0, nMaj = 14, nMin = 8) {
  nMaj = segFor(nMaj, 6); nMin = segFor(nMin, 3);
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

// ── locomotion profile ──────────────────────────────────────────────────────
// Physiology → movement numbers, exported for the engine and used by The
// Lab's gait preview. Speeds are v0.1 hex/min placeholders (docs/11): the
// heart is the engine (energy output), mass is the brake, and SPRINTING is
// gated by circulatory headroom — a strained heart barely runs.

const HEART_OUT = { faint: 14, steady: 26, strong: 42, titan: 64 };

export function locomotionProfile(genome) {
  const g = genome ?? {};
  const P = (arr, i, d) => (arr && typeof arr[i] === 'number' ? arr[i] : d);
  const plan = g.body?.plan ?? 'tetrapod';
  const bulk = P(g.body?.params, 1, 0.5);
  const build = P(g.body?.params, 2, 0.5);
  const legFam = g.slots?.leg?.family ?? 'hoofed_leg';
  const legCount = P(g.slots?.leg?.params, 4, 0.5);
  const handFam = g.slots?.hand?.family ?? null;
  const brainSize = { dim: 1, average: 2, gifted: 3, mastermind: 4 }[g.brain?.tier] ?? 2;

  // energy output: the heart's pumping capacity
  const vigor = P(g.heart?.params, 0, 0.5);
  const power = (HEART_OUT[g.heart?.tier] ?? 26) * (0.7 + 0.6 * vigor);

  // mass: plan volume, bulk, build, and metal lower bodies weigh in
  const planMass = {
    tetrapod: 1.0, winged: 0.8, serpentine: 1.15, blob: 1.55,
    crab: 1.3, arachnid: 1.1, avian: 0.85, treant: 1.7, floater: 0.6,
  }[plan] ?? 1;
  const mass = (1.5 + bulk * 2.4) * planMass * (1 + build * 0.35)
    + (legFam === 'piston_leg' || legFam === 'jet_leg' ? 0.9 : 0);

  // approximate upkeep load (display-side twin of energy.ts)
  const load = 6 + bulk * 8 + brainSize * 1.5 + 10;
  const margin = power - load;
  const p2w = power / (mass * 10);

  const legBase =
    plan === 'serpentine' ? 1.8 :
    plan === 'blob' ? 0.9 :
    plan === 'treant' ? 0.5 :          // rooted; barely shuffles
    plan === 'floater' ? 2.8 :         // thruster-driven, no gait friction -- built for speed
    plan === 'avian' ? 2.9 :           // built to run
    legFam === 'talon_leg' ? 2.6 :
    legFam === 'insect_leg' ? 2.0 :
    legFam === 'piston_leg' ? (legCount < 0.45 ? 2.8 : 2.3) :
    legFam === 'jet_leg' ? 2.7 :
    legFam === 'tendril_leg' ? 1.6 :
    legFam === 'leg_stump' ? 0.6 : 2.2;

  // crab/arachnid forelimbs brace and shove during the low scuttle -- a
  // working hand (not a healed-over stump) braces against the ground for
  // balance and adds a bit of push, the way a real crab's front legs do
  const armAssist = (plan === 'crab' || plan === 'arachnid') && handFam && handFam !== 'hand_stump'
    ? 1.1 : 1;
  const legBaseA = legBase * armAssist;

  const walkSpeed = legBaseA * clamp(0.55 + p2w * 0.85, 0.5, 1.8);
  // sprint gate: headroom decides whether "run" means anything
  const sprint = margin > power * 0.25 ? 'strong' : margin > 0 ? 'limited' : 'none';
  const sprintMult = sprint === 'strong' ? 1.9 + Math.min(margin / power, 0.6) * 0.5
                   : sprint === 'limited' ? 1.35 : 1.1;
  const runSpeed = walkSpeed * sprintMult;
  const walkHz = clamp(0.9 + p2w * 0.8 - mass * 0.05, 0.5, 1.6);
  return {
    mass: +mass.toFixed(2), power: +power.toFixed(1), margin: +margin.toFixed(1),
    walkSpeed: +walkSpeed.toFixed(2), runSpeed: +runSpeed.toFixed(2),
    walkHz: +walkHz.toFixed(2), runHz: +(walkHz * 1.7).toFixed(2),
    sprint,
  };
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

// LOD0 triangle budget for a single creature — the docs/08 mobile perf
// target (~8k tris), with a little headroom for the Lab's hero framing.
const TRI_BUDGET = 9000;

/** Build at full detail; if the busy build (many legs, faction hardware,
 * a mastermind's two-lobe brain) blows the triangle budget, estimate a
 * reduced tessellation scale and rebuild. Triangle count is roughly
 * linear in tube `sides` and quadratic in ellipsoid/torus segment count,
 * so sqrt(budget/actual) is a reasonable single-shot estimate; a short
 * bounded retry loop cleans up any remaining overshoot. This is the
 * primary defense against oversized meshes — uploadCreature's 32-bit
 * index fallback stays in place as a last-resort safety net only. */
function buildCreature(genome) {
  _detail = 1;
  let mb = buildCreatureAtDetail(genome);
  let tris = mb.idx.length / 3;
  let guard = 0;
  while (tris > TRI_BUDGET && guard++ < 3) {
    _detail *= Math.sqrt(TRI_BUDGET / tris) * 0.92;   // slight extra margin per pass
    mb = buildCreatureAtDetail(genome);
    tris = mb.idx.length / 3;
  }
  _detail = 1;   // never leak a reduced dial into anything built outside this pass
  return mb;
}

function buildCreatureAtDetail(genome) {
  const mb = new MeshB();
  const g = genome ?? {};
  const P = (arr, i, d) => (arr && typeof arr[i] === 'number' ? arr[i] : d);

  const vigor = P(g.heart?.params, 0, 0.5);
  const hue   = P(g.body?.params, 0, 0.5);
  const bulk  = P(g.body?.params, 1, 0.5);
  const limb  = P(g.body?.params, 2, 0.5);
  const tail  = P(g.body?.params, 3, 0.5);

  // Faction is the whole visual language, not just a backdrop. The genome
  // still drives silhouette (breeding matters); the faction re-skins the
  // flesh: Mad Doctors keep the stitched b-movie look, Humans become
  // 1950s tin-toy robots (Robby/Gort), Aliens become Metaluna-mutant
  // brain-invaders. Palette, body material, and head/chest detailing all
  // switch; MadDr's path is unchanged.
  const fk = factionKit(_faction, hue, vigor);
  const skin = fk.skin, skinFn = fk.skinFn;
  const headScale = { dim: 0, average: 0.15, gifted: 0.3, mastermind: 0.75 }[g.brain?.tier] ?? 0.15;
  const heartLevel = ['faint','steady','strong','titan'].indexOf(g.heart?.tier ?? 'steady');

  const slots = g.slots ?? {};
  const plan = g.body?.plan ?? 'tetrapod';

  // A decapitated creature: both head-mounted slots (sensor, eye) healed
  // to their stumps -- there's no genome field for "no head" (the skull
  // is pure renderer geometry, docs/08), so this is the derived signal.
  // buildHead() reads it and builds nothing at all, not just an empty
  // one; the sensor/eye slot loop below skips those sockets entirely so
  // nothing floats where a head used to be.
  const headless = slots.sensor?.family === 'sensor_stub' && slots.eye?.family === 'eye_socket';

  // leg genes set stance height (stumps slump low)
  // real leg length: bipeds stand on legs, not casters
  const legAl = slots.leg;
  const legLen = legAl && !plan.match(/blob|serpentine|treant|floater/)
    ? (legAl.family === 'leg_stump' ? 0.6
      : legAl.family === 'insect_leg' ? clamp(1.25 + 1.0 * P(legAl.params, 0, 0.5), 1.25, 2.25)
      : legAl.family === 'piston_leg' ? clamp(1.8 + 1.0 * P(legAl.params, 0, 0.5), 1.8, 2.8)
      : legAl.family === 'jet_leg' ? clamp(1.6 + 1.1 * P(legAl.params, 0, 0.5), 1.6, 2.7)
      : legAl.family === 'tendril_leg' ? clamp(1.1 + 0.9 * P(legAl.params, 0, 0.5), 1.1, 2.0)
      : clamp(2.4 + 1.2 * P(legAl.params, 0, 0.5), 2.4, 3.6))
    : 0;
  const legFam = legAl?.family ?? null;

  const builders = {
    tetrapod: planTetrapod, blob: planBlob, serpentine: planSerpentine, winged: planWinged,
    crab: planCrab, arachnid: planArachnid, avian: planAvian, treant: planTreant, floater: planFloater,
  };
  // hide grain: an unused heart axis picks how fine or coarse the skin
  // texture runs — a heritable detail-density gene
  const grain = P(g.heart?.params, 3, 0.5);

  const sockets = (builders[plan] ?? planTetrapod)(mb, {
    bulk, limb, tail, skin, skinFn, headScale, heartLevel, legLen, legFam,
    brainTier: g.brain?.tier ?? 'average',
    texScale: 0.35 + 0.5 * grain,
    faction: _faction,
    headless,
    details: headless ? () => {} : fk.details,   // no face to detail without a skull
    chestDeco: fk.chestDeco,      // torso front: sutures / rivets / ichor nodes
    // body material override: Humans force riveted panels, Aliens veined
    // membrane; MadDr passes the plan's own organic material through
    bodyTex: (mat, scale, amp) =>
      fk.bodyMat ? [...TILE[fk.bodyMat], scale, fk.bodyAmp] : [...TILE[mat], scale, amp],
  });

  for (const slot of SLOT_NAMES) {
    const al = slots[slot];
    if (!al || !sockets[slot]) continue;
    // plans that ignore a slot render nothing there (silent genes)
    if ((plan === 'blob' || plan === 'serpentine' || plan === 'treant' || plan === 'floater')
        && slot === 'leg') continue;
    // no head, no head-mounted parts -- nothing should float where the
    // skull used to be
    if (headless && (slot === 'sensor' || slot === 'eye')) continue;
    // dormant organic head sensors: low ornament gene → bald head
    if (slot === 'sensor' && (al.family === 'antenna' || al.family === 'horn') &&
        P(al.params, 5, 0.5) < 0.35) continue;

    // a grafted part remembers its OWN hue (surgery.ts, docs/06) instead
    // of blending into this body's skin -- a transplanted limb should
    // still look like it came from wherever it came from. Native
    // (never-grafted) parts have no hue of their own and just express
    // the body's, same as before.
    const partKit = al.hue !== undefined ? factionKit(_faction, al.hue, vigor) : null;
    const partSkin = partKit ? partKit.skin : skin, partSkinFn = partKit ? partKit.skinFn : skinFn;

    const sock = sockets[slot];
    const sides = sock.mirror ? [1, -1] : [1];
    for (const side of sides)
      buildPart(mb, slot, al.family, al.params ?? [], side, sock, { skin: partSkin, skinFn: partSkinFn, faction: _faction });
  }
  return mb;
}

// ---- body plans -------------------------------------------------------------

// ── faction visual kits ─────────────────────────────────────────────────────

// Necks: Mad Doctors get a fleshy column, robots a stacked-ring piston
// (Robby's segmented neck), aliens a ribbed biotech stalk.
function buildNeck(mb, x, y0, y1, z0, z1, r, o) {
  if (o.faction === 'human') {
    const prevTex = mb.tex; mb.setTex(TEX_NONE);
    const n = 4;
    for (let i = 0; i <= n; i++) {
      const t = i / n, y = y0 + (y1 - y0) * t, z = z0 + (z1 - z0) * t;
      const rr = r * (i % 2 ? 0.82 : 1.02);              // alternating discs
      tube(mb, [[x, y - 0.04, z], [x, y + 0.04, z]], [rr, rr],
        i % 2 ? sh(METAL, 1.15) : METAL, 0.85, 0, 10);
    }
    tube(mb, [[x, y0, z0], [x, y1, z1]], [r*0.5, r*0.5], METDK, 0.8, 0, 8);  // central rod
    mb.setTex(prevTex);
    return;
  }
  if (o.faction === 'alien') {
    tube(mb, [[x, y0, z0], [x, (y0+y1)/2, z0 - 0.15], [x, y1, z1]],
      [r*1.05, r*0.82, r*0.72], sh(o.skin, 0.92), 0.3, 0, 10, 3, null, null);
    return;
  }
  tube(mb, [[x, y0, z0], [x, y1, z1]], [r, r*0.86], sh(o.skin, 0.95), 0.28, 0, 10);
}

// 1950s tin-toy enamel over gunmetal (Robby/Gort palette)
const ROBOT_ENAMEL = [
  [200,  58,  50],   // fire-engine red
  [224, 202, 154],   // cream
  [ 66, 150, 158],   // teal
  [ 60,  92, 168],   // cobalt
  [206, 166,  58],   // mustard
  [176, 182, 190],   // bare aluminium
];
const CHROME = [214, 222, 230];
const ROBOT_LENS = [255, 150, 40];
const RIVET = [150, 158, 170];
// Metaluna-mutant invader palette: vivid bruised greens & violets, wet
const ALIEN_HIDE = [
  [ 82, 168,  86],   // metaluna green
  [128, 176,  70],   // sickly lime
  [148,  92, 186],   // mutant violet
  [ 58, 158, 146],   // deep teal
];
const BRAINP = [214, 150, 160];
const ICHOR_N = [150, 235, 190];   // bioluminescent node
const LASER_N  = [130, 220, 255];  // laser_array emitter glow -- cool cyan, reads distinct from plasma_lance's warm ICHOR/BLTGLO
const PHOTON_N = [255, 235, 175];  // photon_blaster maw glow -- warm near-white, "stored light" rather than plasma or laser

function metalColorFn(base) {
  // brushed-metal: catches a hard chrome highlight up top, sinks dark below
  return (u) => {
    let c = base;
    if (u[1] > 0.2) c = lp(c, CHROME, (u[1] - 0.2) * 0.5);
    else if (u[1] < -0.2) c = sh(c, 0.7);
    if (u[2] > 0.3) c = lp(c, CHROME, (u[2] - 0.3) * 0.25);   // frontal sheen
    return c;
  };
}

function factionKit(faction, hue, vigor) {
  if (faction === 'human') {
    const enamel = ROBOT_ENAMEL[Math.floor(clamp(hue, 0, 0.999) * ROBOT_ENAMEL.length)];
    return {
      skin: enamel, skinFn: metalColorFn(enamel),
      details: robotDetails, chestDeco: robotChest,
      bodyMat: 'panels', bodyAmp: 0.9,
    };
  }
  if (faction === 'alien') {
    const base = ALIEN_HIDE[Math.floor(clamp(hue, 0, 0.999) * ALIEN_HIDE.length)];
    const skin = lp(base, skinTone(hue), 0.10);   // a touch of heritable drift
    const belly = lp(skin, [170, 250, 200], 0.45); // bioluminescent underglow
    const spine = lp(skin, [96, 40, 140], 0.55);   // violet iridescent flank
    return {
      skin, skinFn: skinColorFn(skin, belly, spine),
      details: alienDetails, chestDeco: alienChest,
      bodyMat: 'veins', bodyAmp: 0.9,
    };
  }
  // Mad Doctors — unchanged
  const skin = lp(PALLOR, skinTone(hue), 0.40 + 0.60 * vigor);
  const belly = lp(skin, [236, 214, 184], 0.55);
  const spine = lp(skin, [52, 40, 80], 0.45);
  return {
    skin, skinFn: skinColorFn(skin, belly, spine),
    details: frankenDetails, chestDeco: null,   // null → stitchSeam (maddr default)
    bodyMat: null, bodyAmp: 0,
  };
}

// 1950s robot head: a recessed visor band with a glowing lens, a speaker
// grille "voice box", a riveted collar, and a beacon finial.
function robotDetails(mb, headC, headR, heartLevel, o) {
  const prevTex = mb.tex;
  // visor band across the brow (dark recess) with a hot scanning lens
  const vy = headC[1] + headR[1] * 0.18, vz = headC[2] + headR[2] * 0.72;
  mb.setTex(TEX_NONE);
  tube(mb, [
    [headC[0] - headR[0]*0.9, vy, vz - headR[2]*0.25],
    [headC[0], vy, vz + headR[2]*0.05],
    [headC[0] + headR[0]*0.9, vy, vz - headR[2]*0.25],
  ], [0.16, 0.2, 0.16], sh(METAL, 0.6), 0.8, 0, 8);
  ellipsoid(mb, [headC[0], vy, vz + 0.14], [headR[0]*0.28, 0.14, 0.12], ROBOT_LENS, 0.6, 0.9, 8);
  mb.glow([headC[0], vy, vz + 0.2], ROBOT_LENS, 22);
  // speaker-grille mouth: horizontal slats on the lower face
  const my = headC[1] - headR[1] * 0.5, mz = headC[2] + headR[2] * 0.72;
  ellipsoid(mb, [headC[0], my, mz], [headR[0]*0.5, headR[1]*0.24, 0.1], sh(METAL, 0.5), 0.7, 0, 10);
  for (let i = -1; i <= 1; i++)
    tube(mb, [[headC[0]-headR[0]*0.38, my + i*0.13, mz+0.08], [headC[0]+headR[0]*0.38, my + i*0.13, mz+0.08]],
      [0.03, 0.03], [20, 22, 26], 0.5, 0, 5);
  // beacon finial on the crown -- mastermind tier skips this: buildHead
  // already put a glass brain-dome up there (a robot smart enough to
  // need one got its cortex transplanted straight into the chassis),
  // and the finial's rod would sit inside the dome's silhouette and
  // get depth-occluded by its own near surface
  if (o.brainTier !== 'mastermind') {
    tube(mb, [[headC[0], headC[1]+headR[1]*0.9, headC[2]], [headC[0], headC[1]+headR[1]*1.35, headC[2]]],
      [0.07, 0.05], METAL, 0.8, 0, 6);
    ellipsoid(mb, [headC[0], headC[1]+headR[1]*1.4, headC[2]], [0.12,0.12,0.12], ROBOT_LENS, 0.5, 1, 6);
    mb.glow([headC[0], headC[1]+headR[1]*1.42, headC[2]], ROBOT_LENS, 16);
  }
  // riveted collar + heart-tier indicator lamps
  for (let i = 0; i < 8; i++) {
    const a = (i / 8) * Math.PI * 2;
    ellipsoid(mb, [headC[0] + Math.cos(a)*headR[0]*0.9, headC[1]-headR[1]*0.9, headC[2] + Math.sin(a)*headR[2]*0.9],
      [0.08,0.08,0.08], RIVET, 0.8, 0, 4);
  }
  if (heartLevel >= 1) {
    const lamp = heartLevel >= 3 ? BLTGLO : ROBOT_LENS;
    for (let i = 0; i < Math.min(heartLevel + 1, 4); i++) {
      const lx = headC[0] + (i - (Math.min(heartLevel + 1, 4) - 1) / 2) * 0.34;
      ellipsoid(mb, [lx, headC[1]-headR[1]*0.72, headC[2]+headR[2]*0.7], [0.07,0.07,0.07], lamp, 0.5, 1, 5);
      mb.glow([lx, headC[1]-headR[1]*0.72, headC[2]+headR[2]*0.76], lamp, 12);
    }
  }
  mb.setTex(prevTex);
}

// 1950s brain-alien head (This Island Earth's Metaluna Mutant): an
// oversized exposed veined brain sits atop the skull, a heavy compound-eye
// brow beneath, small mandible palps at the mouth, glowing cranial nodes.
function alienDetails(mb, headC, headR, heartLevel, o) {
  const prevTex = mb.tex;
  // the exposed brain — two great veined lobes
  mb.setTex([...TILE.veins, 0.8, 0.7]);
  const bc = [headC[0], headC[1] + headR[1]*0.72, headC[2] - 0.1];
  ellipsoid(mb, bc, [headR[0]*1.02, headR[1]*0.72, headR[2]*0.95], BRAINP, 0.55, 0, 16);
  for (const s of [-1, 1])
    ellipsoid(mb, [bc[0] + s*headR[0]*0.42, bc[1] + headR[1]*0.18, bc[2] + 0.1],
      [headR[0]*0.5, headR[1]*0.5, headR[2]*0.6], sh(BRAINP, 0.94), 0.55, 0, 10);
  // deep sulcus down the middle
  mb.setTex(TEX_NONE);
  tube(mb, [[bc[0], bc[1]+headR[1]*0.5, bc[2]-headR[2]*0.4], [bc[0], bc[1]-headR[1]*0.2, bc[2]+headR[2]*0.5]],
    [0.08, 0.08], sh(BRAINP, 0.7), 0.4, 0, 6);
  // heavy brow ridge over the compound eyes
  const by = headC[1] + headR[1]*0.12, bz = headC[2] + headR[2]*0.74;
  tube(mb, [
    [headC[0]-headR[0]*0.9, by-0.1, bz-headR[2]*0.24],
    [headC[0], by+0.14, bz+0.1],
    [headC[0]+headR[0]*0.9, by-0.1, bz-headR[2]*0.24],
  ], [0.16, 0.22, 0.16], sh(o.skin, 0.72), 0.3, 0, 8);
  // mandible palps flanking the mouth
  const my = headC[1] - headR[1]*0.55, mz = headC[2] + headR[2]*0.7;
  ellipsoid(mb, [headC[0], my, mz], [headR[0]*0.34, 0.12, 0.14], MOUTHC, 0.2, 0, 8);
  for (const s of [-1, 1])
    curvedCone(mb, [headC[0]+s*headR[0]*0.34, my+0.05, mz], [s*0.4, -0.7, 0.5],
      0.55, 0.11, [s*0.2, -0.1, 0.15], sh(o.skin, 0.8), 0.4);
  // glowing cranial nodes (ichor sacs), doubling as the heart-tier tell
  const nodes = Math.min(heartLevel + 1, 4);
  for (let i = 0; i < nodes; i++) {
    const a = Math.PI * (0.25 + 0.5 * (i / Math.max(1, nodes - 1)));
    const nx = bc[0] + Math.cos(a) * headR[0] * 0.9, nz = bc[2] + 0.2;
    ellipsoid(mb, [nx, bc[1] + Math.sin(a)*headR[1]*0.4, nz], [0.13,0.13,0.13], ICHOR_N, 0.5, 0.9, 6);
    mb.glow([nx, bc[1] + Math.sin(a)*headR[1]*0.4, nz + 0.1], ICHOR_N, 16);
  }
  // the glass dome goes LAST: everything it should show through (brain,
  // sulcus, ichor nodes) must already be in the mesh before it, since the
  // see-through blend depends on draw order, not just alpha
  buildGlassDome(mb, [bc[0], bc[1] + headR[1]*0.12, bc[2] + 0.05],
    [headR[0]*1.05, headR[1]*0.85, headR[2]*1.0], o);
  mb.setTex(prevTex);
}

// chest-front detailing by faction (torso already built).
// An anchor tagged `up: true` (planCrab) means the decoration lies flat
// on TOP of the shell facing the sky -- the identity disk rides on a
// crab's back, its only billboard, instead of at the front edge where it
// crowded under the fused head (creator direction, 2026-07).
function robotChest(mb, ch, h, o) {
  const prevTex = mb.tex;
  if (ch.up) {
    mb.setTex(TEX_NONE);
    const y = ch.y + 0.1, z = ch.z;
    // same recessed plate / dial / gauge needle / lamp row / rivet ring,
    // rotated to face +y; "up the panel" now runs along -z (toward the head)
    ellipsoid(mb, [0, y, z], [ch.rx*0.5, 0.12, ch.rx*0.42], sh(METAL, 0.55), 0.7, 0, 12);
    ellipsoid(mb, [0, y+0.12, z - ch.rx*0.12], [ch.rx*0.18, 0.08, ch.rx*0.18], [30,34,40], 0.6, 0, 10);
    ellipsoid(mb, [0, y+0.16, z - ch.rx*0.12], [ch.rx*0.04, 0.04, ch.rx*0.1], ROBOT_LENS, 0.6, 0.9, 6);  // needle
    for (let i = -1; i <= 1; i++)                                    // lamp row
      ellipsoid(mb, [i*ch.rx*0.2, y+0.12, z + ch.rx*0.2], [0.06,0.05,0.06],
        [ROBOT_LENS, [90,220,120], [90,150,255]][i+1], 0.5, 1, 5);
    for (let i = 0; i < 10; i++) {                                   // panel rivets
      const a = (i/10)*Math.PI*2;
      ellipsoid(mb, [Math.cos(a)*ch.rx*0.52, y+0.02, z + Math.sin(a)*ch.rx*0.44], [0.05,0.05,0.05], RIVET, 0.8, 0, 4);
    }
    mb.setTex(prevTex);
    return;
  }
  const y = ch.y - h*0.10, z = ch.z + ch.rz + 0.02;
  mb.setTex(TEX_NONE);
  // a control panel: recessed plate, central dial, gauge, blinking lamps
  ellipsoid(mb, [0, y, z], [ch.rx*0.5, ch.rx*0.42, 0.12], sh(METAL, 0.55), 0.7, 0, 12);
  ellipsoid(mb, [0, y+ch.rx*0.12, z+0.12], [ch.rx*0.18, ch.rx*0.18, 0.08], [30,34,40], 0.6, 0, 10);
  ellipsoid(mb, [0, y+ch.rx*0.12, z+0.16], [ch.rx*0.04, ch.rx*0.1, 0.04], ROBOT_LENS, 0.6, 0.9, 6);  // needle
  for (let i = -1; i <= 1; i++)                                    // lamp row
    ellipsoid(mb, [i*ch.rx*0.2, y-ch.rx*0.2, z+0.12], [0.06,0.06,0.05],
      [ROBOT_LENS, [90,220,120], [90,150,255]][i+1], 0.5, 1, 5);
  for (let i = 0; i < 10; i++) {                                   // panel rivets
    const a = (i/10)*Math.PI*2;
    ellipsoid(mb, [Math.cos(a)*ch.rx*0.52, y+Math.sin(a)*ch.rx*0.44, z+0.02], [0.05,0.05,0.05], RIVET, 0.8, 0, 4);
  }
  mb.setTex(prevTex);
}

function alienChest(mb, ch, h, o) {
  const prevTex = mb.tex;
  if (ch.up) {
    // the sac cluster + translucent plate lie on the carapace top
    const y = ch.y + 0.1;
    ellipsoid(mb, [0, y, ch.z], [ch.rx*0.46, 0.14, ch.rx*0.5], sh(o.skin, 1.05), 0.5, 0, 12, o.skinFn);
    mb.setTex(TEX_NONE);
    const sacsUp = [[0,0.15],[ -0.24,-0.1],[0.24,-0.1],[0,-0.32]];
    for (const [sx, sz] of sacsUp) {
      ellipsoid(mb, [sx*ch.rx, y + 0.14, ch.z + sz*ch.rx], [0.12,0.09,0.15], ICHOR_N, 0.5, 0.85, 7);
      mb.glow([sx*ch.rx, y + 0.2, ch.z + sz*ch.rx], ICHOR_N, 15);
    }
    mb.setTex(prevTex);
    return;
  }
  const y = ch.y - h*0.08, z = ch.z + ch.rz;
  // a cluster of glowing ichor sacs beneath a translucent belly-plate
  ellipsoid(mb, [0, y, z], [ch.rx*0.46, ch.rx*0.5, 0.14], sh(o.skin, 1.05), 0.5, 0, 12, o.skinFn);
  mb.setTex(TEX_NONE);
  const sacs = [[0,0.15],[ -0.24,-0.1],[0.24,-0.1],[0,-0.32]];
  for (const [sx, sy] of sacs) {
    ellipsoid(mb, [sx*ch.rx, y+sy*ch.rx, z+0.14], [0.12,0.15,0.09], ICHOR_N, 0.5, 0.85, 7);
    mb.glow([sx*ch.rx, y+sy*ch.rx, z+0.2], ICHOR_N, 15);
  }
  mb.setTex(prevTex);
}

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
        // Z relative to headC, not world-space 0 -- a hardcoded 0 happened
        // to sit near tetrapod/winged's small forward lean but stranded
        // the bolts far from the head on any plan with a bigger Z offset
        // (avian's forward-leaning neck, most visibly)
        tube(mb, [[bx, byy, headC[2]], [bx + s * 0.8, byy, headC[2]]], [0.22, 0.28],
          glow ? BLTGLO : BOLT, 0.7, glow ? 0.85 : 0, 8);
        if (glow) mb.glow([bx + s * 0.9, byy, headC[2]], BLTGLO, 24);
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
const GLASS  = [200, 228, 224];   // pale cyan-tinted glass

/** A riveted glass dome sealed over an exposed brain — pale, glossy,
 * translucent (mad-science specimen-jar reading, matching the game's
 * brass-and-iron joint language). The brain geometry MUST already be
 * in the mesh before this call: draw order is what makes the see-
 * through effect work (the dome blends with whatever is already in the
 * frame at that pixel, so the brain has to be there first). A brass
 * collar and three support ribs are fully opaque, so the dome reads as
 * a distinct structure at a glance even before the transparency
 * registers. */
function buildGlassDome(mb, center, radii, o) {
  const prevTex = mb.tex;
  const R = [radii[0] * 1.3, radii[1] * 1.3, radii[2] * 1.3];
  mb.setTex(TEX_NONE);
  mb.setAlpha(0.24);   // the ellipsoid draws both near and far shell, so
  ellipsoid(mb, center, R, GLASS, 0.92, 0, 14);   // the effective blend reads stronger than this alone
  mb.setAlpha(1);

  const collarY = center[1] - R[1] * 0.32;
  const collarR = (R[0] + R[2]) * 0.5 * 0.94;
  torus(mb, [center[0], collarY, center[2]], [0, 1, 0], collarR, 0.12, BRASS, 0.85, 0, 16, 6);
  for (let i = 0; i < 6; i++) {
    const a = (i / 6) * Math.PI * 2;
    ellipsoid(mb, [center[0] + Math.cos(a)*collarR, collarY, center[2] + Math.sin(a)*collarR],
      [0.09, 0.09, 0.09], IRON, 0.8, 0, 4);
  }
  // three thin ribs arching from the collar to the crown — guaranteed
  // visible (fully opaque) regardless of how the glass blend renders
  for (let i = 0; i < 3; i++) {
    const a = (i / 3) * Math.PI * 2 + 0.4;
    const base = [center[0] + Math.cos(a)*collarR, collarY, center[2] + Math.sin(a)*collarR];
    const top  = [center[0], center[1] + R[1] * 0.98, center[2]];
    const mid  = [(base[0]+top[0])*0.5 + Math.cos(a)*0.3, (base[1]+top[1])*0.5, (base[2]+top[2])*0.5 + Math.sin(a)*0.3];
    tube(mb, [base, mid, top], [0.05, 0.045, 0.03], sh(BRASS, 0.85), 0.7, 0, 5, 1);
  }
  mb.setTex(prevTex);
}

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
  // the pelvis wears the faction's hide too (robots metal, aliens veined),
  // unless the leg family already dictates a mechanical/chitin chassis
  mb.setTex(mech ? TEX_NONE : chit ? [...TILE.chitin, o.texScale, 0.6]
                                   : o.bodyTex('warts', o.texScale, 0.5));
  // The pelvis must stay INSIDE where the legs actually attach (every
  // plan that calls this sockets its leg at roughly 0.5-0.58 * waistR --
  // see the `leg:` return in each), or a wide pelvis visually swallows
  // the tops of the legs. The old radii (0.72/1.04/0.94) flared out
  // *past* that leg-attach offset, which barely showed at low bulk but
  // became a solid plate hiding the legs at high bulk (independent of
  // heart tier -- it just takes a titan heart to carry that much bulk
  // without going nonviable, which is why it reads as a "titan" bug).
  // Only the two lower levels sit near leg height, so only those shrink;
  // the top level keeps its width to blend into the torso above.
  const hipY = waistY - 1.15;
  const midR = waistR * 0.42, midRz = waistR * 0.35;
  lathe(mb, [
    { y: hipY - 0.45, x: 0, z: 0, rx: waistR*0.30, rz: waistR*0.25 },
    { y: hipY + 0.30, x: 0, z: 0, rx: midR,         rz: midRz },
    { y: waistY + 0.12, x: 0, z: 0, rx: waistR*0.85, rz: waistR*0.71 },
  ], col, mech ? 0.7 : 0.28, 0, 14, fn);
  if (mech) {
    for (let i = 0; i < 8; i++) {          // chassis rivets
      const a = (i / 8) * Math.PI * 2;
      ellipsoid(mb, [Math.cos(a)*midR, hipY + 0.3, Math.sin(a)*midRz],
        [0.09, 0.09, 0.09], IRON, 0.8, 0, 4);
    }
    ellipsoid(mb, [0, hipY + 0.25, midRz], [0.16, 0.16, 0.1], GLOW, 0.5, 1, 6);
    mb.glow([0, hipY + 0.25, midRz * 1.07], GLOW, 18);
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
  if (o.headless) {
    // no skull at all -- the neck just ends. Callers that read hC/hR/topY
    // for socket math (mostly moot now, since headless skips the sensor
    // and eye slots entirely) get a nominal point instead of a crash.
    return { hC: [0, neckY, zOff], hR: [0.01, 0.01, 0.01], topY: neckY };
  }
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
    // the brain, sealed under glass
    const bc = [hC[0], hC[1] + hR[1]*0.62, hC[2] - 0.15];
    const bTex = mb.tex;
    mb.setTex([...TILE.slick, 0.9, 0.55]);
    ellipsoid(mb, bc, [hR[0]*0.92, hR[1]*0.66, hR[2]*0.88], BRAINC, 0.55, 0, 14);
    for (const s of [-1, 1])
      ellipsoid(mb, [bc[0] + s*hR[0]*0.40, bc[1] + hR[1]*0.34, bc[2]],
        [hR[0]*0.44, hR[1]*0.30, hR[2]*0.55], sh(BRAINC, 0.92), 0.55, 0, 8);
    mb.setTex(bTex);
    buildGlassDome(mb, [bc[0], bc[1] + hR[1]*0.1, bc[2]], [hR[0]*0.95, hR[1]*0.72, hR[2]*0.92], o);
    topY = bc[1] + hR[1] * 1.05;
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
    null, (t) => [0, 0, 0.18 * t * t, 4 + t * 3.4],
    (t) => [0, 0, 3 + t * 2.5, 0.2 * t]);
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
  mb.setTex(o.bodyTex('warts', o.texScale, 0.55));
  mb.setGait([0, 0, 0, 0.12]);
  lathe(mb, levels, o.skin, 0.28, 0, 18, o.skinFn);
  const shl = levels[3];
  if (b > 0.5)                            // brute deltoid caps
    for (const s of [-1, 1])
      ellipsoid(mb, [s * shl.rx * 0.85, shl.y + 0.15, shl.z],
        [W*0.48*b, W*0.42*b, W*0.44*b], o.skin, 0.28, 0, 10, o.skinFn);
  const ch = levels[2];
  if (o.chestDeco) o.chestDeco(mb, ch, h, o);
  else stitchSeam(mb, ch.y - h * 0.12, ch.rx, ch.rz, ch.z);
  mb.setGait([0, 0, 0, 0.08]);
  buildPelvis(mb, o, levels[0].rx, waistY);

  // an actual neck between the shoulders and the skull
  const neckTop = y0 + h + 0.55;
  buildNeck(mb, 0, y0 + h - 0.3, neckTop, levels[4].z * 0.7, levels[4].z * 0.8, W*0.32, o);

  mb.setAnim(BREATH_H);
  const HEADBOB = [0, 0, 0, 0.07];
  mb.setGait(HEADBOB);
  const head = buildHead(mb, o, neckTop - 0.2, levels[4].z);
  o.details(mb, head.hC, head.hR, o.heartLevel, o);
  mb.setAnim(ANIM0);
  mb.setGait(GAIT0);

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
    sensor: { p: sensP, nrm: ellipN(sensP, head.hC, head.hR), mirror: true, out: 1,
              anim: BREATH_H, gait: HEADBOB },
    eye:    { p: eyeP, nrm: ellipN(eyeP, head.hC, head.hR),
              mirror: false, faceR: head.hR[0], anim: BREATH_H, gait: HEADBOB },
  };
}

function planBlob(mb, o) {
  // limb gene sets the pour: 0 = wide flat puddle (it slides), 1 = tall
  // gelatin tower (it rolls). A blob never gets a leg socket at all
  // (ignoresSlots, catalog.ts) -- it slides or rolls, full stop -- so a
  // roller has no business keeping a flattened base like it's missing
  // legs it never had; the puddle end of the axis keeps that base
  // because a puddle sliding along the ground plausibly has one.
  const tall = o.limb;
  const dr = (3.0 + 1.3*o.bulk) * (1.15 - 0.40*tall);
  const dR = [dr, (2.5 + 1.0*o.bulk) * (0.62 + 1.05*tall), dr];
  const dC = [0, dR[1]*0.9, 0];
  const JELLY = [0.13, 0, 0.10, 0.7];
  const SQUELCH = [0, 0, 0, 0.16];

  // Organs first: the outer mass draws translucent, so whatever should
  // show through has to already be in the mesh before that call (same
  // draw-order rule the glass dome runs on, docs/08).
  const prevTex = mb.tex, prevAnim = mb.anim;
  mb.setTex(TEX_NONE);
  mb.setGait(GAIT0);   // organs don't ride the locomotion cycle; gait is reset to SQUELCH below, for the body
  mb.setAnim(ANIM0);   // the heart's beat is its own channel (setHeart), not the shared breath/gait ones
  const HEARTC_L = [205, 35, 50], HEARTC_R = [150, 45, 62];   // oxygenated left, darker deoxygenated right
  const STOMACHC = [214, 172, 92], GUTC = [188, 116, 132];
  // three trapezoidal chambers (low-segment lathes -- faceted, not
  // smooth, to read as built rather than a round blob): one large main
  // chamber as the base, tapering to the anatomical apex point, with the
  // left and right ventricles sitting SIDE BY SIDE on top of it -- same
  // Y range, offset from each other in X, not stacked on each other --
  // left (bright, oxygenated) beside right (darker, deoxygenated). Each
  // beats on setHeart()'s own clock (uHeartBeat, gaitStep() in the JS),
  // which tracks the creature's current locomotion speed but never goes
  // fully silent at rest the way the shared gait channel does. All three
  // chambers are also tagged setOrgan() -- loose on the torso, pushed by
  // organLevel()'s spring (uOrganOffset) instead of sitting rigid.
  const hs = Math.min(dR[0], dR[1], dR[2]) * 0.20;
  const heartBase = [dR[0]*0.12, dC[1] + dR[1]*0.20, dR[2]*0.16];

  mb.setOrgan(1);
  // the large main chamber -- the base everything else sits on
  mb.setHeart(0.30);
  lathe(mb, [
    { y: heartBase[1] - hs*0.55, x: heartBase[0], z: heartBase[2], rx: hs*0.16, rz: hs*0.14 },
    { y: heartBase[1] + hs*0.25, x: heartBase[0], z: heartBase[2], rx: hs*0.68, rz: hs*0.60 },
    { y: heartBase[1] + hs*0.80, x: heartBase[0], z: heartBase[2], rx: hs*0.60, rz: hs*0.52 },
  ], HEARTC_L, 0.55, 0, 6);

  // left and right ventricles, side by side on top of the main chamber
  const topY = heartBase[1] + hs*0.80;
  const vSide = hs * 0.36;
  mb.setHeart(0.30);
  lathe(mb, [
    { y: topY + hs*0.02, x: heartBase[0] - vSide, z: heartBase[2], rx: hs*0.30, rz: hs*0.27 },
    { y: topY + hs*0.50, x: heartBase[0] - vSide, z: heartBase[2], rx: hs*0.46, rz: hs*0.40 },
  ], HEARTC_L, 0.55, 0, 6);
  mb.setHeart(0.22);
  lathe(mb, [
    { y: topY + hs*0.02, x: heartBase[0] + vSide, z: heartBase[2], rx: hs*0.26, rz: hs*0.23 },
    { y: topY + hs*0.46, x: heartBase[0] + vSide, z: heartBase[2], rx: hs*0.40, rz: hs*0.34 },
  ], HEARTC_R, 0.55, 0, 5);
  mb.setHeart(0);
  // the stomach: a fleshy sac slung mid-body
  mb.setAnim(ANIM0);
  ellipsoid(mb, [-dR[0]*0.10, dC[1] - dR[1]*0.05, dR[2]*0.08],
    [dR[0]*0.30, dR[1]*0.24, dR[2]*0.28], STOMACHC, 0.4, 0, 10);
  // the digestive tract: coiled through the lower half
  const gutPath = [], gutR = [];
  const nSeg = 12;
  for (let i = 0; i <= nSeg; i++) {
    const t = i / nSeg;
    const ang = t * Math.PI * 3.4;
    const rad = dR[0] * (0.42 - 0.20*t);
    gutPath.push([
      Math.cos(ang) * rad,
      dC[1] - dR[1]*0.4 + t * dR[1]*0.45,
      Math.sin(ang) * rad * (dR[2] / dR[0]),
    ]);
    gutR.push(dR[0] * 0.055 * (1 - 0.25 * Math.sin(t*8)));
  }
  tube(mb, gutPath, gutR, GUTC, 0.35, 0, 8, 3);
  mb.setOrgan(0);
  mb.setTex(prevTex);
  mb.setAnim(prevAnim);

  mb.setGait(SQUELCH);
  mb.setTex(o.bodyTex('slick', o.texScale, 0.85));
  // the mass itself: translucent gelatin now, organs showing through
  mb.setAnim(JELLY);
  mb.setAlpha(0.55);
  ellipsoid(mb, dC, dR, o.skin, 0.34, 0, 18, o.skinFn,
    (u) => [0.13, 0.30 * Math.max(0, u[1]), 0.10, 0.7]);
  // the flattened base: full puddle at limb=0, gone by limb~0.87 -- a
  // rolling ball keeps no flat foot to sit on
  const skirtK = Math.max(0, 1 - tall * 1.15);
  if (skirtK > 0.03) {
    mb.setAnim([0.05 * skirtK, 0, 0.05 * skirtK, 0.7]);
    ellipsoid(mb, [0, 0.62, 0], [dr*1.14*skirtK, 0.85*skirtK, dr*1.14*skirtK],
      sh(o.skin, 0.92), 0.3, 0, 12, o.skinFn);
  }
  mb.setAlpha(1);
  // surface boils: opaque flecks on the translucent hide
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
    hand:   { p: bHandP, nrm: ellipN(bHandP, dC, dR), mirror: true, anim: JELLY, gait: SQUELCH },
    sensor: { p: bSensP, nrm: ellipN(bSensP, dC, dR), mirror: true, out: 1, anim: JELLY, gait: SQUELCH },
    eye:    { p: bEyeP, nrm: ellipN(bEyeP, dC, dR), mirror: false, faceR: dr*0.8, anim: JELLY, gait: SQUELCH },
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
  mb.setTex(o.bodyTex('scales', o.texScale, 0.8));
  const SLITHER = (t) => [0, 0, t * 5.0, 0.30 * t * t];
  tube(mb, path, radii, o.skin, 0.3, 0, 12, 3,
    (t) => sh(o.skin, 0.84 + 0.16 * Math.sin(t * 40) * 0.5 + 0.16),   // belly-band shimmer
    swayFn, SLITHER);
  mb.setGait(SLITHER(1));

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
              mirror: true, tiny: true, anim: SWAY_H, gait: [0, 0, 5.0, 0.30] },
    sensor: { p: sSensP, nrm: ellipN(sSensP, hC, hR), mirror: false, out: 1,
              anim: SWAY_H, gait: [0, 0, 5.0, 0.30] },
    eye:    { p: sEyeP, nrm: ellipN(sEyeP, hC, hR), mirror: false, faceR: hR[0],
              anim: SWAY_H, gait: [0, 0, 5.0, 0.30] },
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
  mb.setTex(o.bodyTex('feathers', o.texScale, 0.5));
  mb.setGait([0, 0, 0, 0.1]);
  lathe(mb, levels, o.skin, 0.28, 0, 14, o.skinFn);
  if (o.chestDeco) o.chestDeco(mb, levels[2], h, o);
  buildPelvis(mb, o, levels[0].rx, waistY);
  const neckTop = y0 + h + 0.45;
  buildNeck(mb, 0, y0 + h - 0.25, neckTop, levels[4].z * 0.7, levels[4].z * 0.8, W*0.3, o);
  mb.setAnim(BREATH_H);
  const WHEADBOB = [0, 0, 0, 0.06];
  mb.setGait(WHEADBOB);
  const head = buildHead(mb, o, neckTop - 0.2, levels[4].z);
  o.details(mb, head.hC, head.hR, o.heartLevel, o);
  mb.setAnim(ANIM0);
  mb.setGait(GAIT0);

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
    sensor: { p: wSensP, nrm: ellipN(wSensP, head.hC, head.hR), mirror: true, out: 1,
              anim: BREATH_H, gait: WHEADBOB },
    eye:    { p: wEyeP, nrm: ellipN(wEyeP, head.hC, head.hR),
              mirror: false, faceR: head.hR[0], anim: BREATH_H, gait: WHEADBOB },
  };
}

// ---- five more body types ---------------------------------------------------

function planCrab(mb, o) {
  const BREATH_T = [0.08, 0, 0, 0], BREATH_H = [0.035, 0, 0, 0];
  const W = 3.0 + 1.6 * o.bulk, D = 2.2 + 1.0 * o.bulk, h = 1.7 + 0.5 * o.bulk;
  const y0 = Math.max(0.5, o.legLen * 0.7);
  // wide, low, flat-topped shell -- a carapace, not a torso
  const levels = [
    { y: y0,          x: 0, z: 0,          rx: W*0.72, rz: D*0.72 },
    { y: y0+h*0.35,   x: 0, z: 0.05*D,     rx: W*0.96, rz: D*0.96 },
    { y: y0+h*0.62,   x: 0, z: 0.02*D,     rx: W*1.00, rz: D*1.00 },
    { y: y0+h*0.88,   x: 0, z: -0.05*D,    rx: W*0.70, rz: D*0.72 },
    { y: y0+h,        x: 0, z: -0.10*D,    rx: W*0.30, rz: D*0.42 },
  ];
  mb.setAnim(BREATH_T);
  mb.setTex(o.bodyTex('chitin', o.texScale, 0.6));
  mb.setGait([0, 0, 0, 0.1]);
  lathe(mb, levels, o.skin, 0.3, 0, 20, o.skinFn);
  // identity disk rides ON the carapace (a crab's back is its only
  // billboard), not at the shell's front edge where it crowded in under
  // the fused head -- `up: true` flips the deco to face the sky.
  // Anchor Y must clear the shell's actual peak (the last `levels` entry,
  // y0+h) -- 0.90h sat BELOW that apex and read as sunk into the dome;
  // 1.05h sits clearly above it, mounted on top like a coin on a dome,
  // not embedded in it.
  if (o.chestDeco) o.chestDeco(mb, { y: y0 + h*1.05, x: 0, z: -0.08*D, rx: W*0.42, rz: D*0.42, up: true }, h, o);
  mb.setGait(GAIT0);

  // no true neck: the head fuses low and forward onto the shell edge
  mb.setAnim(BREATH_H);
  const HEADBOB = [0, 0, 0, 0.06];
  mb.setGait(HEADBOB);
  const head = buildHead(mb, o, y0 + h*0.55, levels[1].rz*0.85);
  o.details(mb, head.hC, head.hR, o.heartLevel, o);
  mb.setAnim(ANIM0);
  mb.setGait(GAIT0);

  addTail(mb, o, y0 + h*0.3, -levels[0].rz*0.9, false);

  const shl = levels[2];
  const sensP = [head.hR[0]*0.5, head.topY, head.hC[2] - 0.1];
  const eyeP  = [0, head.hC[1] + head.hR[1]*0.25, head.hC[2] + head.hR[2]*0.7];
  return {
    // chelipeds: real crabs carry their claws out in FRONT of the
    // carapace, by the mouth, not off its sides -- small lateral spread,
    // parked at the shell's leading edge, normal pointed forward. Short
    // and slender (tiny scale) with a hard cap on reach so the arm can
    // never out-length the legs or dip the claw into the ground.
    hand:   { p: [shl.rx * 0.30, y0 + h*0.42, head.hC[2] + head.hR[2]*0.55],
              nrm: V.norm([0.4, -0.15, 1]), mirror: true, tiny: true,
              armCapLen: Math.max(0.9, o.legLen * 0.82) },
    leg:    { p: [shl.rx * 0.85, o.legLen, -shl.rz*0.15],
              nrm: V.norm([1, -0.7, 0]), mirror: true, len: o.legLen },
    sensor: { p: sensP, nrm: ellipN(sensP, head.hC, head.hR), mirror: true, out: 1,
              anim: BREATH_H, gait: HEADBOB },
    eye:    { p: eyeP, nrm: ellipN(eyeP, head.hC, head.hR),
              mirror: false, faceR: head.hR[0], anim: BREATH_H, gait: HEADBOB },
  };
}

function planArachnid(mb, o) {
  const BREATH_T = [0.09, 0, 0, 0], BREATH_H = [0.035, 0, 0, 0];
  // two-part body: a smaller cephalothorax forward, a bulbous abdomen behind
  const ar = 1.5 + 0.9*o.bulk, cr = 0.9 + 0.4*o.bulk;
  const y0 = Math.max(0.6, o.legLen*0.75);
  const aC = [0, y0 + ar*0.7, -ar*0.5];
  const cC = [0, y0 + cr*0.85, cr*0.9];
  mb.setAnim(BREATH_T);
  mb.setTex(o.bodyTex('chitin', o.texScale, 0.65));
  mb.setGait([0, 0, 0, 0.09]);
  ellipsoid(mb, aC, [ar, ar*0.82, ar*1.15], o.skin, 0.32, 0, 18, o.skinFn);
  ellipsoid(mb, cC, [cr, cr*0.92, cr*1.05], o.skin, 0.32, 0, 16, o.skinFn);
  ellipsoid(mb, [0, (aC[1]+cC[1])*0.5 - 0.1, (aC[2]+cC[2])*0.5], [cr*0.5, cr*0.4, cr*0.5],
    sh(o.skin, 0.85), 0.3, 0, 10, o.skinFn);   // waist pinch between the segments
  if (o.chestDeco) o.chestDeco(mb, { y: cC[1], x: 0, z: cC[2], rx: cr, rz: cr }, cr*2, o);
  mb.setGait(GAIT0);

  // Leg count and style both come from the slot-driven family alone --
  // no hardcoded extra pairs. A monster never wears two different kinds
  // of leg (e.g. insect struts bolted onto hoofed legs); insect_leg and
  // piston_leg's spider mode already render several matching legs on
  // their own, so the "crowded with legs" read still happens when the
  // genome actually picks one of those families.

  mb.setAnim(BREATH_H);
  const HEADBOB = [0, 0, 0, 0.05];
  mb.setGait(HEADBOB);
  const head = buildHead(mb, o, cC[1] + cr*0.5, cC[2] + cr*0.6);
  o.details(mb, head.hC, head.hR, o.heartLevel, o);
  mb.setAnim(ANIM0);
  mb.setGait(GAIT0);

  const sensP = [head.hR[0]*0.5, head.topY, head.hC[2]-0.1];
  const eyeP  = [0, head.hC[1]+head.hR[1]*0.2, head.hC[2]+head.hR[2]*0.65];
  return {
    // pedipalps: short front appendages flanking the mouth, close to the
    // midline and out ahead of the cephalothorax -- not stuck out the side.
    // Real pedipalps are stubby, shorter even than a crab's claws, and
    // never reach past the legs.
    hand:   { p: [cr*0.35, cC[1]+0.15, cC[2]+cr*0.75], nrm: V.norm([0.45, -0.1, 1]), mirror: true, tiny: true,
              armCapLen: Math.max(0.8, o.legLen * 0.65) },
    leg:    { p: [cr*0.9, o.legLen, cC[2]*0.3], nrm: V.norm([0.6, -1, 0.1]), mirror: true, len: o.legLen },
    sensor: { p: sensP, nrm: ellipN(sensP, head.hC, head.hR), mirror: true, out: 1, anim: BREATH_H, gait: HEADBOB },
    eye:    { p: eyeP, nrm: ellipN(eyeP, head.hC, head.hR), mirror: false, faceR: head.hR[0], anim: BREATH_H, gait: HEADBOB },
  };
}

function planAvian(mb, o) {
  const BREATH_T = [0.09, 0, 0, 0], BREATH_H = [0.04, 0, 0, 0];
  const W = 1.4 + 0.7*o.bulk, h = 3.4 + 0.8*o.bulk;
  const waistY = o.legLen + 0.9;
  const y0 = waistY - 0.1;
  // forward-leaning profile: narrow tail-end, deep chest, leans forward as
  // it rises -- the raptor-runner silhouette, distinct from tetrapod's
  // upright posture
  const levels = [
    { y: y0,          x: 0, z: -h*0.15, rx: W*0.55, rz: W*0.7 },
    { y: y0+h*0.3,     x: 0, z: -h*0.05, rx: W*0.9,  rz: W*1.0 },
    { y: y0+h*0.6,     x: 0, z: h*0.1,   rx: W*1.05, rz: W*1.1 },
    { y: y0+h*0.85,    x: 0, z: h*0.28,  rx: W*0.65, rz: W*0.75 },
    { y: y0+h,         x: 0, z: h*0.4,   rx: W*0.32, rz: W*0.42 },
  ];
  mb.setAnim(BREATH_T);
  mb.setTex(o.bodyTex('feathers', o.texScale, 0.5));
  mb.setGait([0, 0, 0, 0.1]);
  lathe(mb, levels, o.skin, 0.28, 0, 16, o.skinFn);
  const ch = levels[2];
  if (o.chestDeco) o.chestDeco(mb, ch, h, o);
  mb.setGait([0, 0, 0, 0.07]);
  buildPelvis(mb, o, levels[0].rx, waistY);

  const neckTop = y0 + h + 1.1;    // a long neck -- the archetype's signature
  buildNeck(mb, 0, y0+h+0.1, neckTop, levels[4].z, levels[4].z+0.9, W*0.22, o);

  mb.setAnim(BREATH_H);
  const HEADBOB = [0, 0, 0, 0.06];
  mb.setGait(HEADBOB);
  const head = buildHead(mb, o, neckTop - 0.15, levels[4].z + 0.9);
  o.details(mb, head.hC, head.hR, o.heartLevel, o);
  mb.setAnim(ANIM0);
  mb.setGait(GAIT0);

  addTail(mb, o, y0 + h*0.15, -levels[0].rz*0.8, false);

  const sensP = [head.hR[0]*0.52, head.topY, head.hC[2]-0.1];
  const eyeP  = [0, head.hC[1]+head.hR[1]*0.2, head.hC[2]+head.hR[2]*0.62];
  return {
    hand:   { p: [ch.rx*0.85, ch.y+0.2, ch.z+0.2], nrm: V.norm([1, 0.1, 0.3]), mirror: true, tiny: true },
    leg:    { p: [Math.max(0.6, levels[0].rx*0.55), o.legLen, levels[0].z*0.3],
              nrm: V.norm([0.25, -1, 0]), mirror: true, len: o.legLen },
    sensor: { p: sensP, nrm: ellipN(sensP, head.hC, head.hR), mirror: true, out: 1, anim: BREATH_H, gait: HEADBOB },
    eye:    { p: eyeP, nrm: ellipN(eyeP, head.hC, head.hR), mirror: false, faceR: head.hR[0], anim: BREATH_H, gait: HEADBOB },
  };
}

function planTreant(mb, o) {
  const BREATH_T = [0.05, 0, 0, 0], BREATH_H = [0.025, 0, 0, 0];   // slow, barely breathing
  const W = 2.0 + 1.3*o.bulk, h = 4.2 + 1.0*o.bulk;
  const y0 = 0.3;   // rooted low; no leg-driven stance (the plan ignores 'leg')
  // a thick columnar trunk, widest at the base, tapering gently up
  const levels = [
    { y: y0,          x: 0, z: 0, rx: W*1.15, rz: W*1.15 },
    { y: y0+h*0.15,    x: 0, z: 0, rx: W*0.85, rz: W*0.85 },
    { y: y0+h*0.55,    x: 0, z: 0, rx: W*0.62, rz: W*0.62 },
    { y: y0+h*0.85,    x: 0, z: 0, rx: W*0.5,  rz: W*0.5 },
    { y: y0+h,         x: 0, z: 0, rx: W*0.36, rz: W*0.36 },
  ];
  mb.setAnim(BREATH_T);
  mb.setTex(o.bodyTex('ridge', o.texScale, 0.7));   // growth-ring texture reads as bark
  mb.setGait([0, 0, 0, 0.03]);
  lathe(mb, levels, o.skin, 0.22, 0, 16, o.skinFn);
  if (o.chestDeco) o.chestDeco(mb, levels[2], h, o);
  mb.setGait(GAIT0);

  // gnarled roots flaring from the base, standing in for legs/feet
  mb.setTex(TEX_NONE);
  const nRoots = 5;
  for (let i = 0; i < nRoots; i++) {
    const a = (i / nRoots) * Math.PI * 2;
    const base = [Math.cos(a)*levels[0].rx*0.7, y0+0.5, Math.sin(a)*levels[0].rx*0.7];
    const tip  = [Math.cos(a)*levels[0].rx*1.7, 0, Math.sin(a)*levels[0].rx*1.7];
    tube(mb, [base, tip], [0.35+0.1*o.bulk, 0.08], sh(o.skin, 0.8), 0.2, 0, 6, 3);
  }

  mb.setAnim(BREATH_H);
  const HEADBOB = [0, 0, 0, 0.03];
  mb.setGait(HEADBOB);
  const head = buildHead(mb, o, y0 + h - 0.3, 0);
  o.details(mb, head.hC, head.hR, o.heartLevel, o);
  mb.setAnim(ANIM0);
  mb.setGait(GAIT0);

  const sensP = [head.hR[0]*0.52, head.topY, head.hC[2]-0.1];
  const eyeP  = [0, head.hC[1]+head.hR[1]*0.2, head.hC[2]+head.hR[2]*0.62];
  return {
    hand:   { p: [levels[3].rx*0.9, levels[3].y, levels[3].z+0.15], nrm: V.norm([1, 0.3, 0.15]), mirror: true },
    sensor: { p: sensP, nrm: ellipN(sensP, head.hC, head.hR), mirror: true, out: 1, anim: BREATH_H, gait: HEADBOB },
    eye:    { p: eyeP, nrm: ellipN(eyeP, head.hC, head.hR), mirror: false, faceR: head.hR[0], anim: BREATH_H, gait: HEADBOB },
  };
}

function planFloater(mb, o) {
  const BREATH_T = [0.04, 0, 0, 0], BREATH_H = [0.03, 0, 0, 0];
  // a lean drone-pod hull, not a soft robed bell: narrower and taller than
  // the old cone, spindled front-to-back for a fast, agile silhouette
  const W = 1.4 + 0.7*o.bulk, h = 3.6 + 0.85*o.bulk;
  const y0 = 0.9;   // hovers -- feet never a factor (the plan ignores 'leg')
  const levels = [
    { y: y0,          x: 0, z: 0,       rx: W*0.20, rz: W*0.20 },   // tapered nose cone
    { y: y0+h*0.16,   x: 0, z: 0.02*W,  rx: W*0.62, rz: W*0.62 },   // thruster collar
    { y: y0+h*0.5,    x: 0, z: 0.05*W,  rx: W*0.80, rz: W*0.80 },   // fuselage waist
    { y: y0+h*0.82,   x: 0, z: 0.02*W,  rx: W*0.42, rz: W*0.42 },   // tapers to the canopy neck
    { y: y0+h,        x: 0, z: 0,       rx: W*0.18, rz: W*0.18 },
  ];
  mb.setAnim(BREATH_T);
  mb.setTex(o.bodyTex('panels', o.texScale, 0.7));
  const HOVER = [0, 0, 0, 0.14];   // a tight mechanical judder -- the engine, not a drift
  mb.setGait(HOVER);
  // hull tints toward gunmetal instead of bare flesh-tone -- a chassis, not
  // skin -- while still taking a cut of the creature's own pigment so it
  // stays a genome trait, not a fixed paint job. No skinFn here: the
  // organic belly/spine tint doesn't belong on a metal hull.
  lathe(mb, levels, lp(METAL, o.skin, 0.4), 0.55, 0, 18);
  if (o.chestDeco) o.chestDeco(mb, levels[2], h, o);

  // stabilizer fins ringing the thruster collar, and a glowing thruster
  // ring underneath -- straight, sharp, mechanical, where the old
  // tentacle-fringe used to hang
  mb.setTex(TEX_NONE);
  const collar = levels[1];
  const nFin = 5;
  for (let i = 0; i < nFin; i++) {
    const a = (i / nFin) * Math.PI * 2;
    const base = [Math.cos(a)*collar.rx*0.95, collar.y, Math.sin(a)*collar.rx*0.95];
    const tip  = [Math.cos(a)*collar.rx*2.15, collar.y - 0.6 - 0.25*o.bulk, Math.sin(a)*collar.rx*2.15];
    mb.setGait([0, 0, i*0.7, 0.04]);
    tube(mb, [base, tip], [0.2, 0.05], METAL, 0.85, 0, 6, 1);
  }
  ellipsoid(mb, [0, y0-0.1, 0], [W*0.42, 0.1, W*0.42], GLOW, 0.4, 0.9, 12);
  mb.glow([0, y0-0.15, 0], GLOW, 24);
  mb.setGait(HOVER);

  // the head pokes out through the canopy neck -- a small skull (or, at
  // mastermind, a brain in its glass dome) riding up top like a cockpit
  mb.setAnim(BREATH_H);
  const HEADBOB = [0, 0, 0, 0.1];
  mb.setGait(HEADBOB);
  const head = buildHead(mb, o, y0 + h - 0.2, 0);
  o.details(mb, head.hC, head.hR, o.heartLevel, o);
  mb.setAnim(ANIM0);
  mb.setGait(GAIT0);

  const sensP = [head.hR[0]*0.5, head.topY, head.hC[2]-0.1];
  const eyeP  = [0, head.hC[1]+head.hR[1]*0.15, head.hC[2]+head.hR[2]*0.6];
  return {
    // short arms high on the hull -- a full-length armDrop would hang past
    // the fins and read as legs, confusing itself with the stabilizers
    hand:   { p: [levels[3].rx*0.8, levels[3].y, levels[3].z+0.1], nrm: V.norm([1, 0, 0.3]), mirror: true, tiny: true },
    sensor: { p: sensP, nrm: ellipN(sensP, head.hC, head.hR), mirror: true, out: 1, anim: BREATH_H, gait: HEADBOB },
    eye:    { p: eyeP, nrm: ellipN(eyeP, head.hC, head.hR), mirror: false, faceR: head.hR[0], anim: BREATH_H, gait: HEADBOB },
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
  // low, hunched plans (crab, arachnid) cap how far an arm can reach off
  // the body: real chelipeds/pedipalps are short and held up in front,
  // never longer than the legs and never long enough to drag the ground
  const armCapLen = sock.armCapLen ?? Infinity;
  // the rig: parts leave the body along the surface normal at the socket,
  // so nothing buries into a chest or skewers a dome on extreme morphs
  const N = sock.nrm ? V.norm([side * sock.nrm[0], sock.nrm[1], sock.nrm[2]]) : [side, 0, 0];
  mb.setAnim(sock.anim ?? ANIM0);   // parts ride whatever their mount does
  mb.setGait(sock.gait ?? GAIT0);
  // joint hardware (limbJoint) is placed per family below, sized from the
  // same girth genes as the limb it clamps — collar always fits the limb

  // biological surface per family: tentacles read wet, bird legs feathered,
  // insect parts chitinous. The part's ornament gene sets detail density.
  const TEX_FAM = {
    claw_hand: ['warts', 0.45], pincer: ['chitin', 0.5], tentacle: ['slick', 0.8],
    rifle_arm: ['none', 0], plasma_lance: ['chitin', 0.6], hand_stump: ['warts', 0.3],
    chain_blade: ['none', 0], spore_launcher: ['chitin', 0.55],
    laser_array: ['chitin', 0.5], photon_blaster: ['chitin', 0.6],
    antenna: ['ridge', 0.35], horn: ['ridge', 0.55], sensor_mast: ['none', 0],
    sensor_stub: ['warts', 0.3],
    bug_eyes: ['none', 0], cyclops_eye: ['none', 0], stalk_eyes: ['none', 0],
    optic_visor: ['none', 0], eye_socket: ['warts', 0.3],
    hoofed_leg: ['warts', 0.5], talon_leg: ['feathers', 0.55], insect_leg: ['chitin', 0.65],
    piston_leg: ['none', 0], leg_stump: ['warts', 0.3],
    jet_leg: ['none', 0], tendril_leg: ['slick', 0.7],
  };
  const tf = TEX_FAM[family] ?? ['none', 0];
  mb.setTex([...TILE[tf[0]], 0.45 + 0.75 * orn, tf[1]]);

  switch (family) {
    // ---- hands ----
    case 'claw_hand': {
      const armR = (0.42 + 0.4*girth) * scale;
      const wrist = armDrop(mb, S, side, armR, scale, o, [len, girth, taper, curl], N, armCapLen);
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
      const wrist = armDrop(mb, S, side, armR, scale, o, [len, girth, taper, curl], N, armCapLen);
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
      const wrist = armDrop(mb, S, side, 0.42*scale, scale, o, [len, girth, taper, curl], N, armCapLen);
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
      const wrist = armDrop(mb, S, side, 0.5*scale, scale, { skin: CHITIN, skinFn: null }, [len, girth, taper, curl], N, armCapLen);
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
    case 'chain_blade': {
      // a motor housing at the wrist driving a guide bar with a looping
      // chain of blade links -- a killer-robot arm, rounded like everything
      // else here, not a brick
      const wrist = armDrop(mb, S, side, 0.4*scale, scale, o, [len, girth, taper, curl], N, armCapLen);
      ellipsoid(mb, wrist, [0.36, 0.4, 0.5], METAL, 0.7, 0, 8);
      const barLen = (1.6 + 1.8*len) * scale;
      const barTip = [wrist[0], wrist[1]+barLen*0.15, wrist[2]+barLen];
      tube(mb, [wrist, barTip], [0.16, 0.1], METDK, 0.75, 0, 8);
      const nLinks = clamp(6 + Math.round(count*6), 6, 12);
      for (let i = 0; i < nLinks; i++) {
        const t = i / nLinks;
        ellipsoid(mb, [
          wrist[0] + (barTip[0]-wrist[0])*t,
          wrist[1] + (barTip[1]-wrist[1])*t + Math.sin(t*Math.PI)*0.22,
          wrist[2] + (barTip[2]-wrist[2])*t,
        ], [0.08, 0.08, 0.08], sh(METAL, 1.1), 0.8, 0, 5);
      }
      ellipsoid(mb, [wrist[0], wrist[1]-0.1, wrist[2]-0.3], [0.1, 0.1, 0.1], GLOW, 0.5, 0.8, 5);
      mb.glow([wrist[0], wrist[1]-0.1, wrist[2]-0.35], GLOW, 12);
      break;
    }
    case 'spore_launcher': {
      // a fleshy arm ending in a bulbous veined pod that vents glowing
      // spore motes -- the biotech counterpart to plasma_lance
      const wrist = armDrop(mb, S, side, 0.46*scale, scale, { skin: CHITIN, skinFn: null }, [len, girth, taper, curl], N, armCapLen);
      const podR = (0.5 + 0.5*girth) * scale;
      ellipsoid(mb, wrist, [podR, podR*1.1, podR], lp(CHITIN, ICHOR, 0.3), 0.45, 0, 10);
      const tipC = [wrist[0], wrist[1]+podR*1.3, wrist[2]+podR*0.6];
      ellipsoid(mb, tipC, [podR*0.6, podR*0.5, podR*0.6], sh(CHITIN, 1.05), 0.5, 0, 8);
      const nSpore = clamp(3 + Math.round(count*4), 3, 7);
      for (let i = 0; i < nSpore; i++) {
        const a = (i/nSpore) * Math.PI * 2, r = podR*0.5;
        const mp = [tipC[0]+Math.cos(a)*r, tipC[1]+podR*0.4+Math.sin(a*1.7)*0.15, tipC[2]+Math.sin(a)*r];
        ellipsoid(mb, mp, [0.09, 0.09, 0.09], ICHOR, 0.4, 0.7, 4);
        mb.glow(mp, ICHOR, 10);
      }
      break;
    }
    case 'laser_array': {
      // a fleshy arm bearing a rigid cluster of narrow crystalline
      // emitters -- count sets how many spikes fan out (docs/17: the
      // family's own canalized bounds keep curl low, so the fan reads
      // rigid rather than tentacle-like)
      const wrist = armDrop(mb, S, side, 0.44*scale, scale, { skin: CHITIN, skinFn: null }, [len, girth, taper, curl], N, armCapLen);
      const mountR = (0.4 + 0.3*girth) * scale;
      ellipsoid(mb, wrist, [mountR, mountR*0.9, mountR], CHITIN, 0.4, 0, 8);
      const nEmit = clamp(3 + Math.round(count*4), 3, 7);
      const L = (1.3 + 1.3*len) * scale;
      for (let i = 0; i < nEmit; i++) {
        const a = (i/(nEmit-1||1) - 0.5) * 1.1;   // rigid fan, not a droop
        const tip = [wrist[0]+Math.sin(a)*0.35*scale, wrist[1]+L, wrist[2]+Math.cos(a)*0.35*scale+0.3*scale];
        tube(mb, [wrist, tip], [0.09*scale, 0.03*scale], lp(CHITIN, [210, 230, 238], 0.4), 0.6, 0.3, 6);
        ellipsoid(mb, tip, [0.06, 0.06, 0.06], LASER_N, 0.5, 1, 5);
        mb.glow(tip, LASER_N, 9);
      }
      break;
    }
    case 'photon_blaster': {
      // a fleshy arm ending in a broad bioluminescent maw -- girth sets
      // how wide the pod is, ornament how bright the stored charge glows
      const wrist = armDrop(mb, S, side, 0.5*scale, scale, { skin: CHITIN, skinFn: null }, [len, girth, taper, curl], N, armCapLen);
      const podR = (0.55 + 0.55*girth) * scale;
      ellipsoid(mb, wrist, [podR, podR*0.95, podR*1.1], lp(CHITIN, PHOTON_N, 0.15), 0.45, 0, 10);
      const mawC = [wrist[0], wrist[1]+podR*0.9, wrist[2]+podR*0.6];
      ellipsoid(mb, mawC, [podR*0.55, podR*0.5, podR*0.3], sh(CHITIN, 0.7), 0.3, 0, 10);   // dark maw rim
      const irisScale = 0.6 + 0.4*orn;
      ellipsoid(mb, mawC, [podR*0.38*irisScale, podR*0.34*irisScale, podR*0.18], PHOTON_N, 0.5, 0.85 + 0.15*orn, 8);
      mb.glow(mawC, PHOTON_N, 14 + 10*orn);
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
      const hPh = side > 0 ? 0 : Math.PI;
      const legGait = (t) => [0.85 * t, 0.5 * t, hPh, 0];
      mb.setGait(legGait(0.12));         // brace clamps the thigh — move WITH it
      limbJoint(mb, hip, V.sub(knee, hip), R * 1.1);
      mb.setGait([0, 0, 0, 0.08]);       // hip ball is body-side: bobs with pelvis
      ellipsoid(mb, [hip[0], hip[1] + 0.15, hip[2]], [R*1.35, R*1.25, R*1.3],
        o.skin, 0.28, 0, 8, o.skinFn);   // hip joint mass
      tube(mb, [hip, knee, ankle], [R*1.18, R*0.9, R*0.68], o.skin, 0.28, 0, 9, 3, null, null, legGait);
      mb.setGait(legGait(1));
      ellipsoid(mb, [S[0], 0.4, S[2] + 0.35], [R*0.8, 0.34, R*1.2], o.skin, 0.28, 0, 8, o.skinFn);
      tube(mb, [[S[0], 0.5, S[2] + 0.8], [S[0], 0.0, S[2] + 0.9]], [R*0.62, R*0.75], HOOF, 0.5, 0, 9, 2);
      mb.setGait(GAIT0);
      break;
    }
    case 'talon_leg': {
      const R = 0.24 + 0.12*girth;
      const tPh = side > 0 ? 0 : Math.PI;
      const talGait = (t) => [0.9 * t, 0.55 * t, tPh, 0];
      mb.setGait(talGait(0.12));         // brace clamps the shin — move WITH it
      // direction = the shin's actual first segment
      limbJoint(mb, [S[0], sock.len + 0.6, S[2]],
        [side * 0.15, sock.len * 0.55 - (sock.len + 0.6), -0.55], R * 1.2);
      mb.setGait([0, 0, 0, 0.08]);       // hip ball is body-side
      ellipsoid(mb, [S[0], sock.len + 0.65, S[2]], [R*1.7, R*1.55, R*1.65],
        o.skin, 0.28, 0, 8, o.skinFn);   // hip joint mass
      tube(mb, [
        [S[0], sock.len + 0.6, S[2]],
        [S[0] + side*0.15, sock.len*0.55, S[2] - 0.55],
        [S[0], 0.12, S[2] + 0.1],
      ], [R*1.3, R, R*0.9], o.skin, 0.3, 0, 7, 3, null, null, talGait);
      mb.setGait(talGait(1));
      const nt = clamp(2 + Math.round(count*2), 2, 4);
      for (let i = 0; i < nt; i++) {
        const a = (i/(nt-1||1) - 0.5) * 1.6;
        // toe splay must mirror with the foot — without `side` here the
        // claws on both feet fan the same absolute way instead of outward
        curvedCone(mb, [S[0], 0.18, S[2]], [side*Math.sin(a)*0.7, -0.18, Math.cos(a)*0.8],
          0.8, 0.14, [0, -0.12, 0], CLAW, 0.5);
      }
      mb.setGait(GAIT0);
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
        const iPh = ((p + (side > 0 ? 0 : 1)) % 2) * Math.PI;   // tripod gait
        mb.setGait([0.6 * 0.12, 0.45 * 0.12, iPh, 0]);           // brace rides its own leg
        limbJoint(mb, hip, V.sub(knee, hip), R * 1.15);
        tube(mb, [hip, knee, shin, foot], [R*1.2, R, R*0.85, 0.05], chit, 0.4, 0, 7, 3,
          null, null, (t) => [0.6 * t, 0.45 * t, iPh, 0]);
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
        mb.setGait([0, 0, side > 0 ? 0 : 1.6, 0.05]);   // tread rumble
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
          const sPh = (((f > 0 ? 1 : 0) + (side > 0 ? 0 : 1)) % 2) * Math.PI;
          mb.setGait([0.55 * 0.12, 0.4 * 0.12, sPh, 0]);         // brace rides its own leg
          limbJoint(mb, hip, V.sub(knee, hip), 0.34);
          tube(mb, [hip, knee, shin, foot], [0.34, 0.26, 0.2, 0.05], METAL, 0.8, 0, 8, 3,
            null, null, (t) => [0.55 * t, 0.4 * t, sPh, 0]);
          ellipsoid(mb, knee, [0.3, 0.3, 0.3], METDK, 0.7, 0, 6);   // knee actuator
        }
      }
      break;
    }
    case 'jet_leg': {
      // a strut ending in a gimbaled thruster -- no foot ever touches down
      const R = 0.18 + 0.1*girth;
      const hip = [S[0], sock.len+0.5, S[2]];
      const nozzle = [S[0]+N[0]*0.2, 0.35, S[2]+N[2]*0.2];
      const hPh = side > 0 ? 0 : Math.PI;
      const legGait = (t) => [0.7*t, 0.3*t, hPh, 0];
      mb.setGait(legGait(0.1));
      limbJoint(mb, hip, V.sub(nozzle, hip), R*1.15);
      mb.setGait([0, 0, 0, 0.08]);
      ellipsoid(mb, [hip[0], hip[1]+0.1, hip[2]], [R*1.2, R*1.1, R*1.2], METAL, 0.75, 0, 7);
      tube(mb, [hip, nozzle], [R*0.9, R*1.3], METDK, 0.7, 0, 8, 1, null, null, legGait);
      mb.setGait(legGait(1));
      ellipsoid(mb, nozzle, [R*1.5, R*1.1, R*1.5], METAL, 0.75, 0, 8);
      const exhaust = [nozzle[0], nozzle[1]-0.55, nozzle[2]];
      tube(mb, [nozzle, exhaust], [R*1.0, R*0.3], sh(METDK, 0.7), 0.4, 0.5, 8, 2);
      mb.glow(exhaust, GLOW, 16);
      mb.setGait(GAIT0);
      break;
    }
    case 'tendril_leg': {
      // a boneless muscular pseudopod -- the biotech counterpart to
      // tentacle, oriented hip-to-ground instead of shoulder-to-droop
      const R = 0.22 + 0.12*girth;
      const hPh = side > 0 ? 0 : Math.PI;
      const hip = [S[0], sock.len+0.5, S[2]];
      const path = [hip];
      for (let i = 1; i <= 8; i++) {
        const t = i / 8;
        path.push([
          hip[0] + N[0]*t*0.6 + Math.sin(t*Math.PI*(1+curl*1.4))*curl*0.6,
          hip[1] - t*(sock.len+0.4),
          hip[2] + N[2]*t*0.6 + 0.3*t,
        ]);
      }
      const legGait = (t) => [0.6*t, 0.35*t, hPh, 0];
      mb.setGait(legGait(0.1));
      limbJoint(mb, hip, V.sub(path[1], hip), R*1.15);
      mb.setGait([0, 0, 0, 0.08]);
      ellipsoid(mb, [hip[0], hip[1]+0.15, hip[2]], [R*1.3, R*1.2, R*1.3],
        o.skinFn ? o.skin : CHITIN, 0.3, 0, 8, o.skinFn);
      tube(mb, path, path.map((_, i) => R*(1-(i/8)*0.75)),
        o.skinFn ? o.skin : CHITIN, 0.32, 0, 8, 3, null, null, legGait);
      mb.setGait(GAIT0);
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
function armDrop(mb, S, side, armR, scale, o, pg = [], N = null, capLen = Infinity) {
  // The arm is shaped by the hand part's own genes, not a fixed tube:
  //   length -> arm length (high + short legs = knuckle-dragger),
  //   curl   -> elbow bend, taper -> forearm mass (low = popeye forearms),
  //   girth  -> bicep bulge.
  // The upper arm EXITS ALONG THE SOCKET NORMAL: a true shoulder joint, so
  // the limb clears the torso before gravity takes it.
  // capLen bounds the reach for plans where a long arm would be wrong
  // (a crab dragging its claws) even at a high length gene.
  const len = pg[0] ?? 0.5, girth = pg[1] ?? 0.5, taper = pg[2] ?? 0.5, curl = pg[3] ?? 0.5;
  const n = N ?? [side, 0, 0];
  const armLen = Math.min((2.5 + 2.0 * len) * scale, capLen);
  const bend = 0.35 + curl * 0.75;
  const ex = V.add(S, V.scale(n, (armR * 1.6 + 0.25) * scale));
  const elbow = [ex[0] + side * 0.2 * scale, ex[1] - armLen * 0.44, ex[2] + 0.08];
  const wrist = [elbow[0] + side * 0.2 * scale, elbow[1] - armLen * 0.52, elbow[2] + 0.25 + bend * 0.4];
  const foreR = Math.max(armR * 0.55, armR * (1.2 - 0.8 * taper + 0.3 * girth));
  const phase = side * 1.3 + 2.0;
  const swing = (t) => [0, 0, 0.04 + 0.11 * t, phase];
  const gPh = side > 0 ? Math.PI : 0;                 // opposite the same-side leg
  const armGait = (t) => [0.5 * t, 0, gPh, 0.05];
  mb.setGait(armGait(0));
  limbJoint(mb, S, n, armR * 1.15);   // brass retaining ring, on the normal
  const armCol = o.skinFn ? o.skin : CHITIN;
  if (o.faction === 'human') {
    // Robby-the-Robot accordion arm: a ball shoulder, a rubber-hose ladder
    // of alternating discs, and a metal fist stub. Segments carry the same
    // traveling gait so the concertina swings as one.
    const prevTex = mb.tex; mb.setTex(TEX_NONE);
    ellipsoid(mb, V.add(S, V.scale(n, armR*0.5*scale)), [armR*1.25, armR*1.2, armR*1.25], METAL, 0.85, 0, 10);
    const pts = [S, ex, elbow, wrist];
    const SEG = 9;
    for (let i = 0; i <= SEG; i++) {
      const u = i / SEG;
      // sample the polyline S→ex→elbow→wrist at parameter u
      const seg = u < 0.34 ? [pts[0], pts[1], u/0.34] : u < 0.67 ? [pts[1], pts[2], (u-0.34)/0.33] : [pts[2], pts[3], (u-0.67)/0.33];
      const q = [seg[0][0]+(seg[1][0]-seg[0][0])*seg[2], seg[0][1]+(seg[1][1]-seg[0][1])*seg[2], seg[0][2]+(seg[1][2]-seg[0][2])*seg[2]];
      const rr = (armR*1.05) * (1 - u*0.35) * (i % 2 ? 1.18 : 0.82);   // ribs
      mb.setGait(armGait(u));
      ellipsoid(mb, q, [rr, armR*0.45*(1-u*0.3), rr], i % 2 ? sh(METAL, 1.12) : METAL, 0.85, 0, 8);
    }
    mb.setGait(armGait(1));
    ellipsoid(mb, wrist, [foreR*1.1, foreR*1.1, foreR*1.1], METDK, 0.8, 0, 8);   // wrist coupling
    mb.setTex(prevTex);
    return wrist;
  }
  // the shoulder ball itself — flesh, seated in the ring
  ellipsoid(mb, V.add(S, V.scale(n, armR * 0.55 * scale)),
    [armR*1.3, armR*1.2, armR*1.25], armCol, 0.3, 0, 8, o.skinFn);
  tube(mb, [S, ex, elbow, wrist], [armR*1.25, armR*1.05, Math.max(armR*0.8, foreR*0.9), foreR],
    armCol, 0.3, 0, 9, 1, null, swing, armGait);
  mb.setGait(armGait(1));             // hands and weapons swing with the wrist
  if (girth > 0.45) {
    // An elongated tube ALONG the shoulder->elbow direction -- a real
    // bicep's long axis -- not a ball, and its own animFn/gaitFn sample
    // the SAME global t the main arm tube above used across this exact
    // stretch (t=1/3 at ex, t=2/3 at elbow), so every point on the bulge
    // moves with the tube surface underneath it. The previous version
    // left mb's gait at armGait(1) -- the WRIST's phase, set on the line
    // above and never overridden here -- while drawing at the elbow's
    // position: the bulge swung on the wrist's motion instead of its
    // own, which is exactly "floats to the front and back of the arm,
    // not locked in sync."
    const armT = (frac) => 1/3 + frac * (2/3 - 1/3);
    const armPt = (frac) => [
      ex[0] + (elbow[0]-ex[0])*frac, ex[1] + (elbow[1]-ex[1])*frac, ex[2] + (elbow[2]-ex[2])*frac,
    ];
    const bicepR = armR * (0.75 + 0.5*girth) * 1.18;   // creator: "too small, make them bigger by 18%"
    tube(mb, [armPt(0.20), armPt(0.45), armPt(0.70)], [bicepR*0.55, bicepR, bicepR*0.55],
      o.skinFn ? o.skin : CHITIN, 0.3, 0, 8, 3, null,
      (t) => swing(armT(0.20 + t*0.50)),
      (t) => armGait(armT(0.20 + t*0.50)));
    mb.setGait(armGait(1));           // restore: hands/weapons still swing with the wrist
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
  // riveted enamel plate — Robby/Gort tin-toy hide: seams divide plates,
  // rivets stud the corners, faint brushed-metal streaks
  tile('panels', (ox, oy) => {
    x.fillStyle = gr(150); x.fillRect(ox, oy, 256, 256);
    for (let i = 0; i < 60; i++) {                       // brushed streaks
      x.fillStyle = gr(140 + Math.floor(rnd() * 22));
      x.fillRect(ox, oy + rnd() * 256, 256, 1);
    }
    x.strokeStyle = gr(96); x.lineWidth = 3;             // recessed plate seams
    for (const gpos of [0, 128]) {
      x.beginPath(); x.moveTo(ox, oy + gpos + 64); x.lineTo(ox + 256, oy + gpos + 64); x.stroke();
      x.beginPath(); x.moveTo(ox + gpos + 64, oy); x.lineTo(ox + gpos + 64, oy + 256); x.stroke();
    }
    x.strokeStyle = gr(196); x.lineWidth = 1;            // seam highlight
    for (const gpos of [0, 128]) {
      x.beginPath(); x.moveTo(ox, oy + gpos + 62); x.lineTo(ox + 256, oy + gpos + 62); x.stroke();
    }
    for (let rx2 = 0; rx2 <= 256; rx2 += 64)             // rivets on the seam grid
      for (let ry2 = 0; ry2 <= 256; ry2 += 64) {
        const g2 = x.createRadialGradient(ox+rx2-2, oy+ry2-2, 1, ox+rx2, oy+ry2, 6);
        g2.addColorStop(0, gr(210)); g2.addColorStop(1, gr(120));
        x.fillStyle = g2;
        x.beginPath(); x.arc(ox + rx2, oy + ry2, 5, 0, 7); x.fill();
      }
  });

  // veined membrane — Metaluna-mutant brain hide: dark base, branching
  // raised veins that read as bioluminescent under the wet shader
  tile('veins', (ox, oy) => {
    x.fillStyle = gr(120); x.fillRect(ox, oy, 256, 256);
    x.lineCap = 'round';
    // px,py are ABSOLUTE canvas coords (already offset by ox,oy); wrapping
    // by ±256 lets a vein leaving one edge reappear so the tile is seamless
    const vein = (px, py, ang, len, wsz) => {
      if (len < 6 || wsz < 0.6) return;
      const nx = px + Math.cos(ang) * len, ny = py + Math.sin(ang) * len;
      wrap((dx, dy) => {
        x.strokeStyle = gr(96); x.lineWidth = wsz + 1.5;
        x.beginPath(); x.moveTo(px+dx, py+dy); x.lineTo(nx+dx, ny+dy); x.stroke();
        x.strokeStyle = gr(172); x.lineWidth = wsz;       // raised highlight
        x.beginPath(); x.moveTo(px+dx, py+dy); x.lineTo(nx+dx, ny+dy); x.stroke();
      });
      vein(nx, ny, ang + (rnd() - 0.5) * 1.1, len * 0.75, wsz * 0.7);
      if (rnd() > 0.4) vein(nx, ny, ang + (rnd() - 0.5) * 1.6, len * 0.6, wsz * 0.6);
    };
    for (let i = 0; i < 5; i++)
      vein(ox + rnd() * 256, oy + rnd() * 256, rnd() * 6.28, 26 + rnd() * 20, 3.5);
    for (let i = 0; i < 90; i++) {                         // pores between veins
      x.fillStyle = gr(104); x.beginPath();
      x.arc(ox + rnd() * 256, oy + rnd() * 256, 1.5, 0, 7); x.fill();
    }
  });

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

// ── the painted backdrop: one scene per faction ─────────────────────────────
// Built once per init. Each faction's lab has its own night: the Mad
// Doctors' castle moor, the Human Army's cold-war airbase, the Alien
// hive's crystal fields under twin moons.

let _faction = 'maddr';

/** Select the lab faction skin. Takes effect on the next initRenderer. */
export function setLabFaction(f) { _faction = SCENES[f] ? f : 'maddr'; }

const SCENES = {
  maddr: {
    sky: [[8, 6, 26], [24, 14, 52], [56, 28, 74], [116, 58, 96]],
    moon: [232, 230, 208], moonEdge: [172, 170, 160], halo: [150, 140, 200],
    ground0: [38, 30, 58], ground1: [70, 56, 88], sheen: [130, 105, 130],
    mist: '96,86,130', cloud: '22,16,44', cloudEdge: '170,160,215',
    dais: ['#221839', '#241a3a', '#463862', '#544472', '#5e4c82'], daisRing: '#2c2148',
  },
  human: {
    sky: [[5, 9, 18], [14, 24, 40], [28, 46, 62], [66, 92, 106]],
    moon: [238, 238, 228], moonEdge: [180, 184, 184], halo: [150, 170, 205],
    ground0: [28, 34, 42], ground1: [50, 60, 70], sheen: [105, 125, 138],
    mist: '90,110,130', cloud: '16,24,36', cloudEdge: '150,170,200',
    dais: ['#1e242c', '#20262f', '#39434e', '#454f5b', '#4f5a66'], daisRing: '#2a323c',
  },
  alien: {
    sky: [[10, 4, 24], [30, 10, 54], [62, 22, 88], [124, 52, 132]],
    moon: [205, 235, 218], moonEdge: [150, 185, 165], halo: [140, 200, 175],
    ground0: [40, 24, 62], ground1: [72, 46, 98], sheen: [150, 110, 180],
    mist: '140,100,180', cloud: '30,14,52', cloudEdge: '200,150,255',
    dais: ['#2a1848', '#2d1a4e', '#472c74', '#54357e', '#63408e'], daisRing: '#3a2560',
  },
};

function skyCol(cfg, t) {
  const s = cfg.sky;
  if (t < 0.45) return lp(s[0], s[1], t / 0.45);
  if (t < 0.75) return lp(s[1], s[2], (t - 0.45) / 0.3);
  return lp(s[2], s[3], (t - 0.75) / 0.25);
}

const B4 = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]];

// faction skyline silhouettes -------------------------------------------------

function silhouetteMaddr(ctx) {
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
  // dead tree, right foreground
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
}

function silhouetteHuman(ctx) {
  // quonset hangar with a lit door slit
  ctx.fillStyle = '#0c1420';
  ctx.beginPath(); ctx.arc(66, HORIZON, 36, Math.PI, 0); ctx.fill();
  ctx.fillStyle = 'rgb(255,196,90)';
  ctx.fillRect(62, HORIZON - 16, 8, 16);
  ctx.fillStyle = '#0c1420';
  ctx.fillRect(62, HORIZON - 16, 8, 3);
  // rocket on a gantry, far left
  ctx.fillStyle = '#0b121d';
  ctx.fillRect(14, 104, 6, 46);
  ctx.beginPath(); ctx.moveTo(14, 104); ctx.lineTo(17, 92); ctx.lineTo(20, 104); ctx.fill();
  ctx.fillRect(24, 100, 3, 50);
  for (let y = 104; y < 150; y += 9) ctx.fillRect(20, y, 5, 2);
  // radar dish on a lattice mast, right side
  ctx.strokeStyle = '#0b121d'; ctx.lineWidth = 2;
  ctx.beginPath(); ctx.moveTo(276, HORIZON); ctx.lineTo(284, 96); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(292, HORIZON); ctx.lineTo(285, 96); ctx.stroke();
  for (let y = 108; y < 148; y += 10) {
    ctx.beginPath(); ctx.moveTo(277 + (148 - y) * 0.1, y); ctx.lineTo(291 - (148 - y) * 0.1, y); ctx.stroke();
  }
  ctx.fillStyle = '#0b121d';
  ctx.beginPath(); ctx.ellipse(284, 88, 14, 7, -0.5, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = 'rgb(255,70,70)';
  ctx.fillRect(283, 78, 3, 3);                       // beacon
  // power poles marching along the horizon
  ctx.strokeStyle = '#0b121d'; ctx.lineWidth = 1;
  for (const px of [150, 190, 230]) {
    ctx.strokeRect(px, 126, 1, HORIZON - 126);
    ctx.beginPath(); ctx.moveTo(px - 6, 130); ctx.lineTo(px + 7, 130); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(px - 5, 135); ctx.lineTo(px + 6, 135); ctx.stroke();
  }
}

function silhouetteAlien(ctx) {
  // a second, smaller moon
  ctx.fillStyle = 'rgba(190,225,205,0.85)';
  ctx.beginPath(); ctx.arc(56, 44, 9, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = 'rgba(150,200,175,0.25)';
  ctx.beginPath(); ctx.arc(56, 44, 14, 0, Math.PI * 2); ctx.fill();
  // crystal spires, glowing at the heart
  const spire = (cx, w, top) => {
    ctx.fillStyle = '#170c2e';
    ctx.beginPath();
    ctx.moveTo(cx - w, HORIZON);
    ctx.lineTo(cx - w * 0.25, top);
    ctx.lineTo(cx + w * 0.35, top + 8);
    ctx.lineTo(cx + w, HORIZON);
    ctx.fill();
    ctx.strokeStyle = 'rgba(190,130,255,0.4)'; ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(cx - w * 0.25, top); ctx.lineTo(cx - w * 0.1, HORIZON); ctx.stroke();
    ctx.fillStyle = 'rgba(210,150,255,0.8)';
    ctx.fillRect(cx - 1, top + (HORIZON - top) * 0.55, 2, 3);
  };
  spire(38, 10, 96); spire(58, 14, 78); spire(80, 9, 104); spire(97, 6, 118);
  spire(286, 12, 86); spire(304, 8, 106);
  // floating shard with a faint glow
  ctx.fillStyle = '#1c1038';
  ctx.beginPath();
  ctx.moveTo(258, 96); ctx.lineTo(266, 104); ctx.lineTo(258, 114); ctx.lineTo(251, 104); ctx.fill();
  ctx.fillStyle = 'rgba(190,130,255,0.3)';
  ctx.beginPath(); ctx.arc(258, 105, 13, 0, Math.PI * 2); ctx.fill();
}

function buildBackground() {
  const cfg = SCENES[_faction] ?? SCENES.maddr;
  const c = document.createElement('canvas');
  c.width = BW; c.height = BH;
  const ctx = c.getContext('2d');
  const img = ctx.createImageData(BW, BH);
  const px = img.data;

  let seed = 987654321;
  const rnd = () => ((seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);

  const MX = 252, MY = 44, MR = 26;
  const CRATERS = [[-8,-6,5],[6,4,4],[-2,9,3],[10,-9,4],[-14,3,3],[3,-14,2]];

  const SKY_STEPS = 26, GND_STEPS = 12;
  for (let y = 0; y < BH; y++) {
    for (let x = 0; x < BW; x++) {
      const bay = (B4[y & 3][x & 3] + 0.5) / 16;
      let col;
      if (y < HORIZON) {
        let t = y / HORIZON + (bay - 0.5) / SKY_STEPS;
        t = clamp(t, 0, 1);
        col = skyCol(cfg, Math.floor(t * SKY_STEPS) / SKY_STEPS);
        const d = Math.hypot(x - MX, y - MY);
        if (d <= MR) {
          const f = d / MR;
          col = lp(cfg.moon, cfg.moonEdge, f * f);
          for (const [cx2, cy2, cr] of CRATERS)
            if (Math.hypot(x - MX - cx2, y - MY - cy2) < cr) col = sh(cfg.moonEdge, 1.12);
        } else if (d < MR + 34) {
          const gg = (1 - (d - MR) / 34) ** 2;
          if (gg * 0.55 > bay * 0.35) col = lp(col, cfg.halo, gg * 0.55);
        }
      } else {
        let t = (y - HORIZON) / (BH - HORIZON) + (bay - 0.5) / GND_STEPS;
        t = clamp(t, 0, 1);
        col = lp(cfg.ground0, cfg.ground1, Math.floor(t * GND_STEPS) / GND_STEPS);
        const w = 8 + (y - HORIZON) * 1.1;
        const dx = Math.abs(x - MX);
        if (dx < w) {
          const s = (1 - dx / w) * 0.5;
          if (s > bay * 0.6) col = lp(col, cfg.sheen, s);
        }
      }
      const vx = (x - 160) / 160, vy = (y - 100) / 100;
      const vq = vx * vx + vy * vy;
      const f = Math.max(0.62, 1 - 0.38 * Math.max(0, vq - 0.55));
      const o = (y * BW + x) * 4;
      px[o] = col[0] * f; px[o+1] = col[1] * f; px[o+2] = col[2] * f; px[o+3] = 255;
    }
  }
  ctx.putImageData(img, 0, 0);

  // skyline
  if (_faction === 'human') silhouetteHuman(ctx);
  else if (_faction === 'alien') silhouetteAlien(ctx);
  else silhouetteMaddr(ctx);

  // floor lines
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
  mist.addColorStop(0, `rgba(${cfg.mist},0)`);
  mist.addColorStop(0.5, `rgba(${cfg.mist},0.28)`);
  mist.addColorStop(1, `rgba(${cfg.mist},0)`);
  ctx.fillStyle = mist;
  ctx.fillRect(0, HORIZON - 8, BW, 16);

  // the dais
  const D = cfg.dais;
  ctx.fillStyle = D[0];
  ctx.beginPath(); ctx.ellipse(DAIS.x, DAIS.y + 5, 90, 24, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = D[1];
  ctx.beginPath(); ctx.ellipse(DAIS.x, DAIS.y + 3, 88, 24, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = D[2];
  ctx.beginPath(); ctx.ellipse(DAIS.x, DAIS.y - 2, 86, 22, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = D[3];
  ctx.beginPath(); ctx.ellipse(DAIS.x + 6, DAIS.y - 3, 64, 15, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = D[4];
  ctx.beginPath(); ctx.ellipse(DAIS.x + 8, DAIS.y - 4, 42, 9, 0, 0, Math.PI * 2); ctx.fill();
  ctx.strokeStyle = cfg.daisRing;
  ctx.beginPath(); ctx.ellipse(DAIS.x, DAIS.y - 2, 74, 18, 0, 0, Math.PI * 2); ctx.stroke();
  for (let a = 0; a < 12; a++) {
    const th = (a / 12) * Math.PI * 2;
    ctx.fillStyle = cfg.daisRing;
    ctx.fillRect(Math.round(DAIS.x + Math.cos(th) * 80) - 1, Math.round(DAIS.y - 2 + Math.sin(th) * 20) - 1, 2, 2);
  }
  return c;
}

function roundedRectPath(ctx, x, y, w, h, r) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

// A plain steel surgical tray -- no sky, moon, or castle skyline; those
// only read right under a whole creature standing on the lab's dais. A
// single harvested part (freezer thumbnails, the surgical tray view) gets
// this instead: a squared, riveted tray plate lit from the upper-left,
// matching the game's brass-and-iron joint language.
function buildTrayBackground() {
  const c = document.createElement('canvas');
  c.width = c.height = BW;   // square, unlike the wide lab backdrop
  const ctx = c.getContext('2d');
  const cx = BW / 2, cy = BW / 2;
  const bg = ctx.createRadialGradient(cx, cy * 0.8, 10, cx, cy, BW * 0.75);
  bg.addColorStop(0, '#2a3038');
  bg.addColorStop(1, '#0d1015');
  ctx.fillStyle = bg;
  ctx.fillRect(0, 0, BW, BW);

  const m = BW * 0.10, r = BW * 0.06;
  ctx.fillStyle = '#31363d';
  roundedRectPath(ctx, m, m, BW - m * 2, BW - m * 2, r);
  ctx.fill();

  const m2 = m + BW * 0.025;
  const inner = ctx.createLinearGradient(m2, m2, BW - m2, BW - m2);
  inner.addColorStop(0, '#565f69');
  inner.addColorStop(0.5, '#3c434b');
  inner.addColorStop(1, '#282d33');
  ctx.fillStyle = inner;
  roundedRectPath(ctx, m2, m2, BW - m2 * 2, BW - m2 * 2, r * 0.7);
  ctx.fill();
  ctx.strokeStyle = 'rgba(255,255,255,0.10)';
  ctx.lineWidth = 1.5;
  roundedRectPath(ctx, m2, m2, BW - m2 * 2, BW - m2 * 2, r * 0.7);
  ctx.stroke();

  ctx.fillStyle = '#8a8f96';
  for (const [rx, ry] of [[m * 1.6, m * 1.6], [BW - m * 1.6, m * 1.6],
                           [m * 1.6, BW - m * 1.6], [BW - m * 1.6, BW - m * 1.6]]) {
    ctx.beginPath(); ctx.arc(rx, ry, 2.4, 0, Math.PI * 2); ctx.fill();
  }
  return c;
}

// Twinkling stars: generated once as GL points [clipX, clipY, phase,
// baseBright]; the star shader pulses each by its own phase over uTime.
function buildStars() {
  let seed = 987654321;
  const rnd = () => ((seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);
  const MX = 252, MY = 44, MR = 26;
  const out = [];
  for (let i = 0; i < 130; i++) {
    const sx = Math.floor(rnd() * BW), sy = Math.floor(rnd() * (HORIZON - 16));
    const ph = rnd(), br = 0.4 + rnd() * 0.6;
    if (Math.hypot(sx - MX, sy - MY) < MR + 12) continue;
    out.push(sx / BW * 2 - 1, 1 - sy / BH * 2, ph, br);
  }
  return new Float32Array(out);
}

// Drifting clouds: their own transparent, horizontally-seamless texture
// (clouds sit clear of the left/right edges, so REPEAT wrap has no seam).
// Drawn as a scrolling quad in drawFrame.
function buildClouds() {
  const cfg = SCENES[_faction] ?? SCENES.maddr;
  const c = document.createElement('canvas');
  c.width = BW; c.height = BH;
  const ctx = c.getContext('2d');
  const cloud = (cx, cy, rx, ry, aBody, aEdge) => {
    ctx.fillStyle = `rgba(${cfg.cloud},${aBody})`;
    ctx.beginPath(); ctx.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = `rgba(${cfg.cloudEdge},${aEdge})`;
    ctx.beginPath(); ctx.ellipse(cx, cy - ry + 1, rx * 0.9, 1.5, 0, 0, Math.PI * 2); ctx.fill();
  };
  cloud(130, 66, 62, 6, 0.65, 0.10);
  cloud(250, 58, 48, 5, 0.85, 0.30);
  cloud(60, 40, 44, 5, 0.55, 0.08);
  cloud(198, 92, 40, 5, 0.5, 0.08);
  return c;
}

// ── shaders ─────────────────────────────────────────────────────────────────

const VS_CREATURE = `
attribute vec3 aPos, aNor, aCol, aMat;
attribute vec4 aTex, aAnim, aGait;
attribute float aAlpha, aHeart, aOrgan;
uniform mat4 uPV;
uniform float uCos, uSin;
uniform float uTime, uBreath, uBlink, uTongue;
uniform float uGait, uGaitAmp, uHeartBeat;
uniform vec3 uOrganOffset;
uniform vec2 uGaze;
varying vec3 vNor, vCol, vPos, vMat, vLoc, vLNor;
varying vec4 vTex;
varying float vAlpha;
void main() {
  vLoc = aPos; vLNor = aNor; vTex = aTex; vAlpha = aAlpha;
  vec3 lp = aPos;
  lp += aNor * (aAnim.x * uBreath);                          // breathing
  // heartbeat: an asymmetric "lub-DUB" (sharp systolic contraction, a
  // smaller second bump, then a long relaxed rest) on its OWN clock --
  // see gaitStep() -- so it keeps beating even when uGaitAmp is 0 (the
  // creature standing still), just slower than when it's running
  float lub = pow(max(sin(uHeartBeat), 0.0), 3.0);
  float dub = pow(max(sin(uHeartBeat - 1.1), 0.0), 6.0) * 0.4;
  lp += aNor * (aHeart * (lub + dub));
  // internal organs hang off a damped spring rather than sitting rigid
  // on the torso -- a straight translation (not normal-scaled, unlike
  // the heartbeat pulse above), so the whole organ swings as one lump
  // instead of breathing in and out -- see organLevel()
  lp += aOrgan * uOrganOffset;
  lp.y += max(aAnim.y, 0.0) * sin(uTime * 2.6 + aAnim.w);    // traveling sinusoidal flap
  lp.y -= max(-aAnim.y, 0.0) * uBlink;                       // eyelid blink
  float sway = aAnim.z * sin(uTime * 1.4 + aAnim.w);         // pendulum sway
  lp.x += sway * 0.6;
  lp.z += aAnim.z * cos(uTime * 1.1 + aAnim.w) * 0.35;
  float fx = aMat.z;
  lp.x += max(fx, 0.0) * uGaze.x;                            // pupils drift (saccades)
  lp.y += max(fx, 0.0) * uGaze.y * 0.6;
  lp.z += max(-fx, 0.0) * uTongue;                           // tongue darts
  // locomotion: legs swing fore-aft and lift alternately, bodies bob & roll
  float gp = uGait + aGait.z;
  lp.z += sin(gp) * aGait.x * uGaitAmp;
  lp.y += max(0.0, sin(gp + 1.57)) * aGait.y * uGaitAmp;
  lp.y += sin(uGait * 2.0 + aGait.z) * aGait.w * uGaitAmp * 0.5;
  lp.x += cos(gp) * aGait.w * uGaitAmp * 0.4;
  vec3 p = vec3(lp.x*uCos - lp.z*uSin, lp.y, lp.x*uSin + lp.z*uCos);
  vec3 n = vec3(aNor.x*uCos - aNor.z*uSin, aNor.y, aNor.x*uSin + aNor.z*uCos);
  vNor = n; vCol = aCol; vMat = aMat; vPos = p;
  gl_Position = uPV * vec4(p, 1.0);
}`;

const FS_CREATURE = `
precision mediump float;
varying vec3 vNor, vCol, vPos, vMat, vLoc, vLNor;
varying vec4 vTex;
varying float vAlpha;
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
  gl_FragColor = vec4(lit, vAlpha);
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
uniform float uUseTex, uUVX;
void main() {
  vec2 uv = vUV + vec2(uUVX, 0.0);
  vec4 t = uUseTex > 0.5 ? texture2D(uTex, uv) : vec4(1.0);
  gl_FragColor = vec4(t.rgb * uColor.rgb, t.a * uColor.a);
}`;

const VS_STAR = `
attribute vec4 aStar;               // xy = clip pos, z = phase, w = base brightness
uniform float uTime, uStarSize;
varying float vB;
void main() {
  gl_Position = vec4(aStar.xy, 0.0, 1.0);
  gl_PointSize = uStarSize;
  vB = aStar.w * (0.35 + 0.65 * (0.5 + 0.5 * sin(uTime * 2.3 + aStar.z * 6.2831)));
}`;

const FS_STAR = `
precision mediump float;
varying float vB;
void main() {
  float r = length(gl_PointCoord - 0.5) * 2.0;
  float a = max(0.0, 1.0 - r);
  a *= a;
  gl_FragColor = vec4(vec3(0.82, 0.86, 1.0) * vB, a * vB);
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

// ── personality: second-order dynamics ──────────────────────────────────────
// After "Giving Personality to Procedural Animations using Math"
// (SalvatoreScalia / t3ssel8r): drive TRANSITIONS through a spring-damper
// y + k1·y' + k2·y'' = x + k3·x', tuned per creature from its BRAIN genes.
// The gait waveform itself is untouched — only how motion arrives:
//   fury        → low damping  (twitchy, overshooting starts and stops)
//   will        → damping      (composed, settles without wobble)
//   temperament → frequency    (how fast it responds at all)
//   guile       → negative response (winds up before moving — sneaky)

class SOD {
  constructor(f, z, r, x0 = 0) {
    const w = 2 * Math.PI * f;
    this.k1 = z / (Math.PI * f);
    this.k2 = 1 / (w * w);
    this.k3 = r * z / w;
    this.xp = x0; this.y = x0; this.yd = 0;
  }
  update(dt, x) {
    const xd = (x - this.xp) / dt;
    this.xp = x;
    const k2s = Math.max(this.k2, dt * dt / 2 + dt * this.k1 / 2, dt * this.k1);
    this.y += dt * this.yd;
    this.yd += dt * (x + this.k3 * xd - this.y - this.k1 * this.yd) / k2s;
    return this.y;
  }
}

// Locomotion preview: cycle idle → walk → run on the dais treadmill.
// Cadence comes from the creature's locomotion profile, so heavy or
// weak-hearted specimens visibly lumber while sprinters blur.
const _gait = { phase: 0, amp: 0, mode: 0, t: 0, heartPhase: 0 };
const GAIT_MODES = [
  { name: 'idle', dur: 210, amp: 0,   hzKey: 'walkHz' },
  { name: 'walk', dur: 360, amp: 1,   hzKey: 'walkHz' },
  { name: 'run',  dur: 240, amp: 1.5, hzKey: 'runHz'  },
];

function gaitStep() {
  const G = _gait;
  const loco = R?.loco;
  if (!loco) return [0, 0, 0];
  const m = GAIT_MODES[G.mode];
  if (++G.t > m.dur) { G.mode = (G.mode + 1) % GAIT_MODES.length; G.t = 0; }
  const mode = GAIT_MODES[G.mode];
  // transitions carry the creature's temperament: a furious brute lunges
  // into its run with overshoot, a strong-willed one glides, a guileful
  // one visibly winds up first. The stride waveform itself is unchanged.
  G.amp = Math.max(0, R.sodAmp ? R.sodAmp.update(1 / 60, mode.amp)
                               : (G.amp + (mode.amp - G.amp) * 0.04));
  const hz = R.sodHz ? Math.max(0.15, R.sodHz.update(1 / 60, loco[mode.hzKey]))
                     : loco[mode.hzKey];
  G.phase += (2 * Math.PI * hz) / 60;
  // the heart runs on its own clock, driven by the SAME speed signal
  // (hz) as the legs, but scaled well above stride rate -- and unlike
  // aGait's motion (gated by uGaitAmp, which is 0 at idle), hz itself
  // never drops to 0 at rest -- idle still tracks walkHz, just without
  // visibly stepping -- so the beat never actually stops, only slows
  const heartHz = 1.0 + hz * 1.3;
  G.heartPhase += (2 * Math.PI * heartHz) / 60;
  return [G.phase, G.amp, G.heartPhase];
}

// Internal organs (aOrgan-tagged: heart, stomach, gut) aren't rigid with
// the torso -- they hang off their own SOD spring, chasing an impulse
// target built from footfalls and heartbeats, so they lag and jiggle like
// they're on bungee cords instead of moving in lockstep. Same k2s-clamped
// SOD math as the gait/gaze springs above, which is what keeps this from
// ever overshooting into runaway oscillation ("exploding") no matter how
// hard or fast it gets kicked.
function organLevel() {
  const G = _gait;
  if (!R?.sodOrganY) return [0, 0, 0];
  const scale = (R.maxR ?? 4) * 0.05;
  // a footfall lands twice per stride and jolts everything hanging inside
  // -- silent at idle (G.amp is 0 there, same gate the legs use), sharper
  // the faster the creature moves
  const footfall = Math.pow(Math.max(Math.sin(G.phase * 2), 0), 10) * G.amp;
  // each heartbeat gives the organs packed around it a small kick too,
  // off the same clock as the shader's lub-DUB
  const hbKick = Math.pow(Math.max(Math.sin(G.heartPhase), 0), 3);
  const targetY = -footfall * 1.0 - hbKick * 0.22;
  const targetX = Math.sin(G.phase) * 0.35 * G.amp;
  const targetZ = Math.cos(G.heartPhase * 0.5) * 0.10 * hbKick;
  const oy = R.sodOrganY.update(1 / 60, targetY) * scale;
  const ox = R.sodOrganX.update(1 / 60, targetX) * scale;
  const oz = R.sodOrganZ.update(1 / 60, targetZ) * scale;
  return [ox, oy, oz];
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
  if (R?.sodGX) {
    G.cur = [R.sodGX.update(1 / 60, G.to[0]), R.sodGY.update(1 / 60, G.to[1])];
  } else if (G.t < 7) {
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

function setupGL(canvas, opts = {}) {
  const gl = canvas.getContext('webgl', { antialias: true, alpha: false, preserveDrawingBuffer: !!opts.preserve });
  if (!gl) throw new Error('WebGL unavailable');
  // 32-bit element indices: near-universal on WebGL1 (desktop + mobile),
  // needed so heavily-detailed creatures never silently lose geometry
  // past the 65535-index Uint16 ceiling (see uploadCreature).
  const uintIdx = !!gl.getExtension('OES_element_index_uint');

  const progC = makeProgram(gl, VS_CREATURE, FS_CREATURE);
  const progQ = makeProgram(gl, VS_QUAD, FS_QUAD);
  const progG = makeProgram(gl, VS_GLOW, FS_GLOW);
  const progS = makeProgram(gl, VS_STAR, FS_STAR);

  // backdrop texture (nearest: keep the chunky pixels)
  const bgTex = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, bgTex);
  gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE,
    opts.background === 'tray' ? buildTrayBackground() : buildBackground());
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

  // drifting cloud layer. The texture is NPOT (320x200), so it MUST use
  // CLAMP wrap (WebGL1 renders NPOT+REPEAT as opaque black); the seamless
  // horizontal scroll is done by drawing two screen-shifted copies, and the
  // cloud art is transparent at its left/right edges so they meet cleanly.
  const cloudTex = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, cloudTex);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, buildClouds());
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

  // twinkling star field
  const starData = buildStars();
  const starBuf = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, starBuf);
  gl.bufferData(gl.ARRAY_BUFFER, starData, gl.STATIC_DRAW);

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
  const vw = opts.vw ?? CW, vh = opts.vh ?? CH;
  const eye = opts.eye ?? [0, 7.4, 30];
  const target = opts.target ?? [0, 5.7, 0];
  const proj = perspective(28 * Math.PI / 180, vw / vh, 1, 120);
  const view = lookAt(eye, target, [0, 1, 0]);
  const pv = mat4mul(proj, view);

  return {
    gl, progC, progQ, progG, progS, bgTex, shTex, skinTex, cloudTex, quadBuf,
    starBuf, starCount: starData.length / 4, uintIdx,
    pv: new Float32Array(pv), eye, vw, vh,
    meshBuf: gl.createBuffer(), idxBuf: gl.createBuffer(), glowBuf: gl.createBuffer(),
    idxCount: 0, idxType: gl.UNSIGNED_SHORT, glowCount: 0, maxR: 3,
    background: opts.background ?? 'sky',
  };
}

function uploadCreature(genome, X = R) {
  const { gl } = X;
  let mb;
  try { mb = buildCreature(genome); }
  catch (e) { console.error('creature build failed:', e); mb = new MeshB(); }

  gl.bindBuffer(gl.ARRAY_BUFFER, X.meshBuf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(mb.v), gl.STATIC_DRAW);

  // Heavily-detailed creatures (multi-leg insect/spider stances × faction
  // joint hardware × mastermind brains) routinely need MORE than 65535
  // vertex indices — a Uint16Array silently wraps/truncates past that,
  // which is exactly the "legs on one side only" bug: legs build last
  // among the slots and the right side (side=1) builds before the left
  // (side=-1), so a truncated buffer cuts off the tail — the left leg(s).
  // Use 32-bit indices via the near-universal OES_element_index_uint
  // extension whenever the mesh actually needs them; stay on 16-bit
  // (smaller, faster) otherwise.
  const need32 = mb.idx.length > 65000;
  if (need32 && !X.uintIdx) {
    console.warn(`creature mesh needs ${mb.idx.length} indices but this device lacks ` +
      `OES_element_index_uint — truncating to 64998 (geometry will be cut off).`);
  }
  const idx = need32 && X.uintIdx ? new Uint32Array(mb.idx)
    : mb.idx.length <= 65000 ? new Uint16Array(mb.idx)
    : new Uint16Array(mb.idx.slice(0, 64998));
  X.idxType = need32 && X.uintIdx ? gl.UNSIGNED_INT : gl.UNSIGNED_SHORT;
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, X.idxBuf);
  gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, idx, gl.STATIC_DRAW);
  X.idxCount = idx.length;

  const gf = [];
  for (const g of mb.glows) gf.push(...g);
  gl.bindBuffer(gl.ARRAY_BUFFER, X.glowBuf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(gf), gl.STATIC_DRAW);
  X.glowCount = mb.glows.length;

  X.loco = locomotionProfile(genome);
  // brain genes tune the springs: [command, will, temperament, guile, fury]
  const bp = genome?.brain?.params ?? [];
  const will = bp[1] ?? 0.5, temper = bp[2] ?? 0.5, guile = bp[3] ?? 0.5, fury = bp[4] ?? 0.5;
  const pf = 1.1 + temper * 2.6;
  const pz = clamp(1.15 - fury * 0.85 + will * 0.35, 0.28, 1.6);
  const pr = 0.6 - guile * 2.2;
  X.sodAmp = new SOD(pf, pz, pr, 0);
  X.sodHz  = new SOD(pf * 0.8, pz, pr * 0.5, X.loco.walkHz);
  X.sodGX  = new SOD(pf * 1.6, pz * 0.85, pr, 0);
  X.sodGY  = new SOD(pf * 1.6, pz * 0.85, pr, 0);
  // internal-organ suspension springs: fixed, not personality-linked --
  // damping ratio 0.55 is comfortably above critical enough that no
  // parameter choice here reads as unstable, and the SOD's own k2s clamp
  // (see class SOD) guarantees it can't diverge regardless
  X.sodOrganX = new SOD(3.2, 0.55, 0, 0);
  X.sodOrganY = new SOD(3.6, 0.55, 0, 0);
  X.sodOrganZ = new SOD(3.2, 0.55, 0, 0);
  if (X === R) { _gait.mode = 0; _gait.t = 0; _gait.amp = 0; }
  let mr = 2.5;
  for (let i = 0; i < mb.v.length; i += 27)
    mr = Math.max(mr, Math.hypot(mb.v[i], mb.v[i + 2]));
  X.maxR = Math.min(mr, 13);

  // per-creature rhythms (live renderer only)
  if (X === R) {
    _blink.seed = (mb.v.length * 2654435761) >>> 0 || 1;
    _blink.next = 60 + Math.floor(blinkRnd() * 180);
    _blink.phase = -1;
    _gaze.cur = [0, 0]; _gaze.t = 99;
    _gaze.next = 50 + Math.floor(blinkRnd() * 120);
    _tongue.t = 99;
    _tongue.next = 200 + Math.floor(blinkRnd() * 200);
  }
}

function drawQuad(X, x0, y0, x1, y1, u0, v0, u1, v1, tex, color, useTex, uvx = 0) {
  const { gl, progQ, quadBuf } = X;
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
  gl.uniform1f(gl.getUniformLocation(progQ, 'uUVX'), uvx);
  if (tex) {
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.uniform1i(gl.getUniformLocation(progQ, 'uTex'), 0);
  }
  gl.drawArrays(gl.TRIANGLES, 0, 6);
}

function drawFrame(X = R, still = false) {
  const { gl } = X;
  const onTray = X.background === 'tray';
  const fc = _frame % FLASH_CYCLE;
  const flash = still ? 0 : (fc < 3 ? 0.30 : (fc >= 8 && fc < 11) ? 0.17 : 0);
  const pulse = still ? 0.4 : Math.sin(_frame * 0.05);
  const theta = still ? 0.62 : _theta;    // fixed three-quarter view for stills
  const time = still ? 0 : _frame / 60;

  gl.viewport(0, 0, X.vw, X.vh);
  gl.disable(gl.DEPTH_TEST);
  gl.disable(gl.BLEND);
  gl.disable(gl.CULL_FACE);

  // backdrop: the lab's painted sky/moon/castle/dais for a whole creature,
  // or a plain riveted steel tray for a lone harvested part -- stars,
  // drifting clouds, and the dais contact shadow are all lab-scene
  // dressing and skipped on a tray backdrop
  drawQuad(X, -1, -1, 1, 1, 0, 0, 1, 1, X.bgTex, [1 + flash, 1 + flash, 1 + flash * 1.2, 1], true);

  // twinkling stars (each pulses on its own phase), additive over the sky
  if (!onTray && X.starCount) {
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE);
    const s = X.progS;
    gl.useProgram(s);
    gl.bindBuffer(gl.ARRAY_BUFFER, X.starBuf);
    const sa = gl.getAttribLocation(s, 'aStar');
    gl.enableVertexAttribArray(sa);
    gl.vertexAttribPointer(sa, 4, gl.FLOAT, false, 16, 0);
    gl.uniform1f(gl.getUniformLocation(s, 'uTime'), time);
    gl.uniform1f(gl.getUniformLocation(s, 'uStarSize'), Math.max(2, X.vh / 150));
    gl.drawArrays(gl.POINTS, 0, X.starCount);
    gl.disable(gl.BLEND);
  }

  // clouds drifting slowly across the sky: two screen-shifted copies wrap
  // seamlessly (the cloud art is clear at its horizontal edges)
  if (!onTray) {
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    const shift = (time * 0.035) % 2;
    for (const k of [-1, 0]) {
      const x0 = shift + 2 * k - 1;
      drawQuad(X, x0, -1, x0 + 2, 1, 0, 0, 1, 1, X.cloudTex, [1, 1, 1, 1], true);
    }
    gl.disable(gl.BLEND);
  }

  // contact shadow on the dais (screen-space, scaled by creature radius) --
  // its screen position is tuned to the lab's fixed wide framing, which a
  // tray's tight part-camera doesn't share, so it's skipped there too
  if (!onTray) {
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    const pxPerUnit = 15.4;                      // matches the camera framing
    const sw = (X.maxR * pxPerUnit + 14) / BW * 2;
    const shx = (DAIS.x / BW) * 2 - 1, shy = 1 - (DAIS.y / BH) * 2;
    drawQuad(X, shx - sw, shy - sw * 0.26, shx + sw, shy + sw * 0.26, 0, 0, 1, 1,
      X.shTex, [0.02, 0.01, 0.06, 0.55], true);
    gl.disable(gl.BLEND);
  }

  // creature. Blending stays on for the whole draw -- opaque geometry
  // (alpha=1, the overwhelming majority) blends as a no-op, so this only
  // costs anything where a part actually sets alpha<1 (glass domes).
  // Depth WRITES stay on too: a glass dome sits further from camera than
  // nothing, so it still needs to occlude whatever's behind it once drawn;
  // what makes the glass-over-brain effect work is draw ORDER (the brain
  // is built, and its triangles land in the index buffer, before the dome).
  gl.enable(gl.DEPTH_TEST);
  gl.enable(gl.BLEND);
  gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
  gl.clear(gl.DEPTH_BUFFER_BIT);
  const p = X.progC;
  gl.useProgram(p);
  gl.bindBuffer(gl.ARRAY_BUFFER, X.meshBuf);
  const stride = 108;
  const attr = (name, size, off) => {
    const a = gl.getAttribLocation(p, name);
    gl.enableVertexAttribArray(a);
    gl.vertexAttribPointer(a, size, gl.FLOAT, false, stride, off);
  };
  attr('aPos', 3, 0); attr('aNor', 3, 12); attr('aCol', 3, 24); attr('aMat', 3, 36);
  attr('aTex', 4, 48); attr('aAnim', 4, 64); attr('aGait', 4, 80); attr('aAlpha', 1, 96);
  attr('aHeart', 1, 100); attr('aOrgan', 1, 104);
  gl.activeTexture(gl.TEXTURE1);
  gl.bindTexture(gl.TEXTURE_2D, X.skinTex);
  gl.uniform1i(gl.getUniformLocation(p, 'uSkin'), 1);
  gl.activeTexture(gl.TEXTURE0);
  const t = still ? 0 : _frame / 60;
  const breathRaw = (Math.sin(t * 1.65) + 1) / 2;
  const breath = still ? 0 : breathRaw * breathRaw * (3 - 2 * breathRaw);   // eased in-out
  gl.uniformMatrix4fv(gl.getUniformLocation(p, 'uPV'), false, X.pv);
  gl.uniform1f(gl.getUniformLocation(p, 'uCos'), Math.cos(theta));
  gl.uniform1f(gl.getUniformLocation(p, 'uSin'), Math.sin(theta));
  gl.uniform3fv(gl.getUniformLocation(p, 'uEye'), X.eye);
  gl.uniform1f(gl.getUniformLocation(p, 'uFlash'), flash);
  gl.uniform1f(gl.getUniformLocation(p, 'uPulse'), pulse);
  gl.uniform1f(gl.getUniformLocation(p, 'uTime'), t);
  gl.uniform1f(gl.getUniformLocation(p, 'uBreath'), breath);
  gl.uniform1f(gl.getUniformLocation(p, 'uBlink'), still ? 0 : blinkLevel());
  const gz = still ? [0, 0] : gazeLevel();
  gl.uniform2f(gl.getUniformLocation(p, 'uGaze'), gz[0], gz[1]);
  gl.uniform1f(gl.getUniformLocation(p, 'uTongue'), still ? 0 : tongueLevel());
  const gs = still ? [0, 0, 0] : gaitStep();
  gl.uniform1f(gl.getUniformLocation(p, 'uGait'), gs[0]);
  gl.uniform1f(gl.getUniformLocation(p, 'uGaitAmp'), gs[1]);
  gl.uniform1f(gl.getUniformLocation(p, 'uHeartBeat'), gs[2]);
  const oo = still ? [0, 0, 0] : organLevel();
  gl.uniform3f(gl.getUniformLocation(p, 'uOrganOffset'), oo[0], oo[1], oo[2]);
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, X.idxBuf);
  gl.drawElements(gl.TRIANGLES, X.idxCount, X.idxType, 0);
  gl.disable(gl.BLEND);

  // glow sprites (additive, over everything but depth-tested)
  if (X.glowCount) {
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE);
    gl.depthMask(false);
    const q = X.progG;
    gl.useProgram(q);
    gl.bindBuffer(gl.ARRAY_BUFFER, X.glowBuf);
    const ga = (name, size, off) => {
      const a = gl.getAttribLocation(q, name);
      gl.enableVertexAttribArray(a);
      gl.vertexAttribPointer(a, size, gl.FLOAT, false, 28, off);
    };
    ga('aPos', 3, 0); ga('aCol', 3, 12); ga('aSize', 1, 24);
    gl.uniformMatrix4fv(gl.getUniformLocation(q, 'uPV'), false, X.pv);
    gl.uniform1f(gl.getUniformLocation(q, 'uCos'), Math.cos(theta));
    gl.uniform1f(gl.getUniformLocation(q, 'uSin'), Math.sin(theta));
    gl.uniform1f(gl.getUniformLocation(q, 'uPulse'), pulse);
    gl.drawArrays(gl.POINTS, 0, X.glowCount);
    gl.depthMask(true);
    gl.disable(gl.BLEND);
  }

  // lightning wash
  if (flash > 0) {
    gl.disable(gl.DEPTH_TEST);
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    drawQuad(X, -1, -1, 1, 1, 0, 0, 1, 1, null, [0.88, 0.9, 1, flash], false);
    gl.disable(gl.BLEND);
  }
}

/** One-shot square still of a creature in a faction's lab, as a PNG data
 * URL — the Stable's thumbnails. Uses its own throwaway GL context so the
 * live portrait is never disturbed. */
export function renderThumbnail(genome, faction, size = 168) {
  const prevFac = _faction;
  _faction = SCENES[faction] ? faction : 'maddr';
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = size;
  let url = '';
  try {
    const X = setupGL(canvas, {
      preserve: true, vw: size, vh: size,
      eye: [0, 6.6, 24], target: [0, 6.0, 0],
    });
    uploadCreature(genome, X);
    drawFrame(X, true);
    url = canvas.toDataURL('image/png');
    X.gl.getExtension('WEBGL_lose_context')?.loseContext();
  } catch (e) {
    console.error('thumbnail failed:', e);
  }
  _faction = prevFac;
  return url;
}

// Healed-over family per slot -- the same names surgery.ts's STUMP_OF
// uses server-side, kept as a local literal here rather than an import:
// creature-renderer.js is a standalone, zero-deps module (site/lib is
// genome-core's own vendored copy, wired into main.js, not this file).
const STUMP_FAMILY = { hand: 'hand_stump', sensor: 'sensor_stub', eye: 'eye_socket', leg: 'leg_stump' };
const PART_CAMERA = {
  sensor: { eye: [0, 9.0, 7],   target: [0, 9.0, 0] },
  eye:    { eye: [0, 9.0, 7],   target: [0, 9.0, 0] },
  hand:   { eye: [7.5, 6.0, 12.0], target: [2.2, 5.0, 0.5] },
  leg:    { eye: [0, 2.0, 7],   target: [0, 2.0, 0] },
  heart:  { eye: [0, 4.2, 6],   target: [0, 3.9, 0] },
};

// A mannequin genome wearing ONLY the one harvested part (or heart) --
// every other slot is a healed stump, not a generic filled-in part.
// Sensor and eye in particular share a camera angle (same head-region
// shot), so a generic "default eyes" sitting next to a real sensor would
// read as "the eyes are still there" -- which they aren't, that's the
// whole point of a per-part thumbnail. A heart has no external geometry
// on any OTHER plan, so a heart item swaps the mannequin to 'blob'
// instead, the one plan with a visible internal heart. The part keeps
// its OWN hue (surgery.ts) rather than the mannequin's, so the render
// matches exactly the color it'll keep once actually grafted on.
function buildMannequinGenome(item, slot) {
  const isHeart = slot === 'heart';
  const slotFor = (s) => (!isHeart && slot === s)
    ? { family: item.family, params: item.params, hue: item.hue }
    : { family: STUMP_FAMILY[s], params: [0, 0, 0, 0, 0, 0] };
  return {
    genomeVersion: 2, parentIds: [],
    body: { plan: isHeart ? 'blob' : 'tetrapod', params: [0.5, 0.5, 0.5, 0.5] },
    brain: { tier: 'average', params: [0.5, 0.5, 0.5, 0.5, 0.5] },
    heart: isHeart ? { tier: item.tier, params: item.params } : { tier: 'faint', params: [0, 0, 0, 0, 0, 0] },
    slots: { hand: slotFor('hand'), sensor: slotFor('sensor'), eye: slotFor('eye'), leg: slotFor('leg') },
  };
}

/** One-shot square still of a single harvested part (or heart) worn by
 * the neutral mannequin above, zoomed to the body region it sockets into
 * and set on a plain steel tray (not the lab dais) -- the freezer's real
 * per-part thumbnail. */
export function renderPartThumbnail(item, slot, faction, size = 96) {
  const prevFac = _faction;
  _faction = SCENES[faction] ? faction : 'maddr';
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = size;
  let url = '';
  try {
    const genome = buildMannequinGenome(item, slot);
    const cam = PART_CAMERA[slot] ?? PART_CAMERA.hand;
    const X = setupGL(canvas, { preserve: true, vw: size, vh: size, eye: cam.eye, target: cam.target, background: 'tray' });
    uploadCreature(genome, X);
    drawFrame(X, true);
    url = canvas.toDataURL('image/png');
    X.gl.getExtension('WEBGL_lose_context')?.loseContext();
  } catch (e) {
    console.error('part thumbnail failed:', e);
  }
  _faction = prevFac;
  return url;
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
