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
// Interleaved: pos(3) normal(3) colour(3, 0..1) gloss(1) emissive(1) = 11

class MeshB {
  constructor() { this.v = []; this.idx = []; this.glows = []; }
  vert(p, n, c, g, e) {
    this.v.push(p[0], p[1], p[2], n[0], n[1], n[2], c[0]/255, c[1]/255, c[2]/255, g, e);
    return this.v.length / 11 - 1;
  }
  tri(a, b, c) { this.idx.push(a, b, c); }
  quad(a, b, c, d) { this.idx.push(a, b, c, a, c, d); }
  glow(p, c, size) { this.glows.push([p[0], p[1], p[2], c[0]/255, c[1]/255, c[2]/255, size]); }
}

/** Ellipsoid at c with radii r. colorFn(unitPos) may vary the colour. */
function ellipsoid(mb, c, r, col, gloss = 0.25, emis = 0, seg = 14, colorFn = null) {
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
      row.push(mb.vert(p, n, colorFn ? colorFn(u) : col, gloss, emis));
    }
    rows.push(row);
  }
  for (let i = 0; i < la; i++)
    for (let j = 0; j < lo; j++)
      mb.quad(rows[i][j], rows[i][j+1], rows[i+1][j+1], rows[i+1][j]);
}

/** Swept tube along path with per-point radii; parallel-transport frames. */
function tube(mb, path, radii, col, gloss = 0.25, emis = 0, sides = 10, caps = 3, colorFn = null) {
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
    n = V.norm(V.sub(n, V.scale(T[i], V.dot(n, T[i]))));   // transport
    const b = V.cross(T[i], n);
    const row = [];
    for (let j = 0; j <= sides; j++) {
      const ph = (j / sides) * Math.PI * 2;
      const dir = V.add(V.scale(n, Math.cos(ph)), V.scale(b, Math.sin(ph)));
      const cc = colorFn ? colorFn(i / (P.length - 1)) : col;
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
    const nrm = V.scale(T[k], dirSign);
    const cv = mb.vert(P[k], nrm, colorFn ? colorFn(k ? 1 : 0) : col, gloss, emis);
    for (let j = 0; j < sides; j++)
      dirSign > 0 ? mb.tri(cv, rows[k][j], rows[k][j+1]) : mb.tri(cv, rows[k][j+1], rows[k][j]);
  }
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
  const skin  = lp(PALLOR, skinTone(hue), 0.40 + 0.60 * vigor);
  const belly = lp(skin, [236, 214, 184], 0.55);
  const spine = lp(skin, [52, 40, 80], 0.45);
  const skinFn = skinColorFn(skin, belly, spine);
  const headScale = { dim: 0, average: 0.15, gifted: 0.3, mastermind: 0.75 }[g.brain?.tier] ?? 0.15;
  const heartLevel = ['faint','steady','strong','titan'].indexOf(g.heart?.tier ?? 'steady');

  const slots = g.slots ?? {};
  const plan = g.body?.plan ?? 'tetrapod';

  // leg genes set stance height (stumps slump low)
  const legAl = slots.leg;
  const legLen = legAl && !plan.match(/blob|serpentine/)
    ? (legAl.family === 'leg_stump' ? 0.45 : clamp(0.9 + 1.1 * P(legAl.params, 0, 0.5), 0.9, 2.0))
    : 0;

  const builders = { tetrapod: planTetrapod, blob: planBlob, serpentine: planSerpentine, winged: planWinged };
  const sockets = (builders[plan] ?? planTetrapod)(mb, {
    bulk, limb, skin, skinFn, headScale, heartLevel, legLen,
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

function frankenDetails(mb, headC, headR, heartLevel, skin) {
  // mouth gash + underbite fangs on the head front
  const mz = headC[2] + headR[2] * 0.86;
  const my = headC[1] - headR[1] * 0.42;
  ellipsoid(mb, [headC[0], my, mz], [headR[0]*0.44, headR[1]*0.16, 0.22], MOUTHC, 0.15, 0, 8);
  for (const s of [-1, 1])
    curvedCone(mb, [headC[0] + s*headR[0]*0.36, my - 0.08, mz + 0.05], [0, 1, 0.12],
      0.62, 0.16, [0, 0, 0.05], CLAW, 0.6);

  // neck bolts by heart tier; titan bolts glow
  if (heartLevel >= 1) {
    const glow = heartLevel >= 3;
    const rows = heartLevel >= 2 ? [-0.28, -0.55] : [-0.42];
    for (const fy of rows.slice(0, heartLevel >= 2 ? 2 : 1))
      for (const s of [-1, 1]) {
        const bx = headC[0] + s * headR[0] * 0.92;
        const by = headC[1] + headR[1] * fy;
        tube(mb, [[bx, by, 0], [bx + s * 0.85, by, 0]], [0.24, 0.3],
          glow ? BLTGLO : BOLT, 0.7, glow ? 0.85 : 0, 8);
        if (glow) mb.glow([bx + s * 0.95, by, 0], BLTGLO, 26);
      }
  }
}

function stitchSeam(mb, cx, y0, rx, rz, torsoC, torsoR) {
  // zigzag suture across the chest front
  const pts = [];
  for (let i = 0; i <= 8; i++) {
    const t = i / 8;
    const x = cx + (t - 0.5) * rx * 1.5;
    const y = y0 + ((i % 2) ? 0.32 : -0.32);
    const dx = (x - torsoC[0]) / torsoR[0], dy = (y - torsoC[1]) / torsoR[1];
    const zz = 1 - dx*dx - dy*dy;
    if (zz <= 0.02) continue;
    pts.push([x, y, torsoC[2] + torsoR[2] * Math.sqrt(zz) + 0.04]);
  }
  if (pts.length > 2) tube(mb, pts, pts.map(() => 0.09), STITCH, 0.1, 0, 5, 0);
}

function planTetrapod(mb, o) {
  const tR = [2.5 + 1.2*o.bulk, 2.3 + 0.6*o.bulk, 2.1 + 0.8*o.bulk];
  const tC = [0, o.legLen + tR[1] * 0.82, 0];
  const hR = [2.0 + 0.5*o.headScale, 1.85 + 0.6*o.headScale, 1.95 + 0.4*o.headScale];
  const hC = [0, tC[1] + tR[1] - 0.35 + hR[1] * 0.72, 0.25];

  ellipsoid(mb, tC, tR, o.skin, 0.28, 0, 16, o.skinFn);
  ellipsoid(mb, hC, hR, o.skin, 0.3, 0, 16, o.skinFn);
  frankenDetails(mb, hC, hR, o.heartLevel, o.skin);
  stitchSeam(mb, 0, tC[1] + 0.4, tR[0], tR[2], tC, tR);

  return {
    hand:   { p: [tR[0]*0.88, tC[1] + tR[1]*0.42, 0.2], mirror: true },
    leg:    { p: [tR[0]*0.42, o.legLen, 0], mirror: true, len: o.legLen },
    sensor: { p: [hR[0]*0.52, hC[1] + hR[1]*0.82, 0], mirror: true, out: 1 },
    eye:    { p: [0, hC[1] + hR[1]*0.18, hC[2] + hR[2]*0.92], mirror: false, faceR: hR[0] },
  };
}

function planBlob(mb, o) {
  const dr = 3.0 + 1.3*o.bulk;
  const dR = [dr, 2.5 + 1.0*o.bulk, dr];
  const dC = [0, dR[1]*0.9, 0];
  ellipsoid(mb, dC, dR, o.skin, 0.34, 0, 18, o.skinFn);
  // drooping skirt
  ellipsoid(mb, [0, 0.62, 0], [dr*1.14, 0.85, dr*1.14], sh(o.skin, 0.92), 0.3, 0, 12, o.skinFn);
  // surface boils
  for (let a = 0; a < 6; a++) {
    const th = a * 1.047 + 0.4;
    ellipsoid(mb, [Math.cos(th)*dr*0.9, 1.1 + (a%3)*0.5, Math.sin(th)*dr*0.9],
      [0.5, 0.42, 0.5], sh(o.skin, 0.88), 0.4, 0, 6);
  }
  return {
    hand:   { p: [dr*0.92, dC[1] + 0.4, 0], mirror: true },
    sensor: { p: [dr*0.5, dC[1] + dR[1]*0.85, 0], mirror: true, out: 1 },
    eye:    { p: [0, dC[1] + dR[1]*0.35, dR[2]*0.9], mirror: false, faceR: dr*0.8 },
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
  tube(mb, path, radii, o.skin, 0.3, 0, 12, 3,
    (t) => sh(o.skin, 0.84 + 0.16 * Math.sin(t * 40) * 0.5 + 0.16));  // belly-band shimmer

  const hC = [0.35, headY + 0.9, 1.25];
  const hR = [1.5 + 0.4*o.headScale, 1.3 + 0.4*o.headScale, 1.7];
  ellipsoid(mb, hC, hR, o.skin, 0.3, 0, 14, o.skinFn);
  if (girth > 0.55)                                     // cobra hood
    ellipsoid(mb, [hC[0], hC[1] - 0.2, hC[2] - 0.7], [hR[0]*1.75, hR[1]*1.5, 0.5],
      sh(o.skin, 0.9), 0.28, 0, 12, o.skinFn);
  // fangs point DOWN on a serpent
  const mz = hC[2] + hR[2] * 0.8;
  ellipsoid(mb, [hC[0], hC[1] - hR[1]*0.4, mz], [hR[0]*0.4, 0.16, 0.2], MOUTHC, 0.15, 0, 8);
  for (const s of [-1, 1])
    curvedCone(mb, [hC[0] + s*hR[0]*0.3, hC[1] - hR[1]*0.42, mz], [0, -1, 0.1],
      0.6, 0.13, [0, 0, 0.04], CLAW, 0.6);

  return {
    hand:   { p: [hC[0] + 1.0, headY - 1.3, 0.9], mirror: true, tiny: true },
    sensor: { p: [hC[0] + 0.5, hC[1] + hR[1]*0.8, hC[2] - 0.3], mirror: false, out: 1 },
    eye:    { p: [hC[0], hC[1] + hR[1]*0.25, hC[2] + hR[2]*0.85], mirror: false, faceR: hR[0] },
  };
}

function planWinged(mb, o) {
  const bR = [1.7, 2.2, 1.5];
  const bC = [0, o.legLen + bR[1]*0.85, 0];
  const hR = [2.05 + 0.4*o.headScale, 1.9 + 0.5*o.headScale, 1.95];
  const hC = [0, bC[1] + bR[1] - 0.3 + hR[1]*0.7, 0.2];
  ellipsoid(mb, bC, bR, o.skin, 0.28, 0, 14, o.skinFn);
  ellipsoid(mb, hC, hR, o.skin, 0.3, 0, 16, o.skinFn);
  frankenDetails(mb, hC, hR, o.heartLevel, o.skin);

  // bat wings: membrane grid + bone leading edge + finger struts
  const span = 4.6 + 3.6 * o.limb;
  const shY = bC[1] + bR[1] * 0.55;
  const wingCol = sh(lp(o.skin, spineOf(o.skin), 0.25), 0.95);
  for (const s of [-1, 1]) {
    const nU = 9, nV = 3, grid = [];
    const lead = [];
    for (let iu = 0; iu <= nU; iu++) {
      const u = iu / nU;
      const lx = s * (1.1 + u * span);
      const ly = shY + Math.sin(u * Math.PI * 0.85) * 2.5 - u * u * 1.6;
      const lz = -0.25 - u * 0.25;
      lead.push([lx, ly, lz]);
      const chord = (2.5 * (1 - 0.5 * u) + 0.5) * (1 + 0.10 * Math.sin(u * Math.PI * 3));
      const row = [];
      for (let iv = 0; iv <= nV; iv++) {
        const v = iv / nV;
        row.push(mb.vert(
          [lx, ly - v * chord, lz + v * 0.35],
          [0, 0.25, s > 0 ? 0.97 : 0.97],   // soft fake normal; shader two-sides it
          lp(wingCol, o.skin, v * 0.35), 0.2, 0));
      }
      grid.push(row);
    }
    for (let iu = 0; iu < nU; iu++)
      for (let iv = 0; iv < nV; iv++)
        mb.quad(grid[iu][iv], grid[iu][iv+1], grid[iu+1][iv+1], grid[iu+1][iv]);
    tube(mb, lead, lead.map((_, i) => 0.24 * (1 - i / lead.length) + 0.08), BONDK, 0.35, 0, 7);
    for (const fu of [0.45, 0.75]) {
      const k = Math.round(fu * nU);
      const a = lead[k];
      const chord = (2.5 * (1 - 0.5 * fu) + 0.5);
      tube(mb, [a, [a[0], a[1] - chord, a[2] + 0.35]], [0.12, 0.05], BONDK, 0.3, 0, 6);
    }
  }

  return {
    hand:   { p: [bR[0]*0.95, bC[1] + 0.3, 0.35], mirror: true, tiny: true },
    leg:    { p: [0.85, o.legLen, 0], mirror: true, len: o.legLen },
    sensor: { p: [hR[0]*0.5, hC[1] + hR[1]*0.8, 0], mirror: true, out: 1 },
    eye:    { p: [0, hC[1] + hR[1]*0.2, hC[2] + hR[2]*0.9], mirror: false, faceR: hR[0] },
  };
}

function spineOf(skin) { return lp(skin, [52, 40, 80], 0.45); }

// ---- parts ------------------------------------------------------------------
// Each part reads its 6 genes [length,girth,taper,curl,count,ornament],
// clamps them into safe morph ranges, and builds from control skeletons.
// `side` is +1 (right) / −1 (left): control points mirror, geometry never
// gets a negative scale.

function buildPart(mb, slot, family, params, side, sock, o) {
  const [len=0.5, girth=0.5, taper=0.5, curl=0.5, count=0.5, orn=0.5] = params;
  const S = [side * sock.p[0], sock.p[1], sock.p[2]];
  const scale = sock.tiny ? 0.62 : 1;

  switch (family) {
    // ---- hands ----
    case 'claw_hand': {
      const armR = (0.42 + 0.4*girth) * scale;
      const wrist = armDrop(mb, S, side, armR, scale, o);
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
      const wrist = armDrop(mb, S, side, armR, scale, o);
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
        path.push([
          S[0] + side * (0.5*t + Math.sin(t*Math.PI*1.2) * 0.4) ,
          S[1] - t*L + Math.sin(t*Math.PI) * 0.2,
          S[2] + 0.4*t + Math.sin(t * Math.PI * (1 + curl*1.6)) * curl * 1.1,
        ]);
      }
      tube(mb, path, path.map((_, i) =>
        baseR * (1 - (i/10) * clamp(0.35 + 0.6*taper, 0.35, 0.92))), o.skin, 0.3, 0, 9, 3);
      break;
    }
    case 'rifle_arm': {
      const wrist = armDrop(mb, S, side, 0.42*scale, scale, o);
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
      const wrist = armDrop(mb, S, side, 0.5*scale, scale, { skin: CHITIN, skinFn: null });
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
      const path = [];
      for (let i = 0; i <= 6; i++) {
        const t = i / 6;
        path.push([S[0] + side*(0.4 + t*1.1), S[1] + t*L, S[2] + Math.sin(t*2.2)*0.25]);
      }
      tube(mb, path, path.map(() => 0.11 + 0.09*girth), BONE, 0.35, 0, 6);
      if (girth > 0.3)
        ellipsoid(mb, path[6], [0.3, 0.3, 0.3], BONDK, 0.5, 0, 6);
      break;
    }
    case 'horn': {
      curvedCone(mb, S, [side*0.45, 1, -0.1], 1.2 + 1.5*girth, 0.3 + 0.4*girth,
        [side*(0.3 + curl*0.9), curl*0.4, -0.2], lp(BONE, o.skin, 0.25), 0.4);
      break;
    }
    case 'sensor_mast': {
      const L = 1.5 + 1.0*len;
      tube(mb, [S, [S[0]+side*0.2, S[1]+L, S[2]]], [0.22, 0.16], METAL, 0.8, 0, 8);
      ellipsoid(mb, [S[0]+side*0.2, S[1]+L, S[2]+0.15], [0.68, 0.68, 0.18], METDK, 0.7, 0, 10);
      ellipsoid(mb, [S[0]+side*0.2, S[1]+L, S[2]+0.34], [0.16,0.16,0.16], GLOW, 0.5, 1, 6);
      mb.glow([S[0]+side*0.2, S[1]+L, S[2]+0.36], GLOW, 20);
      break;
    }
    case 'sensor_stub': {
      ellipsoid(mb, S, [0.3, 0.22, 0.3], PALLOR, 0.25, 0, 6);
      break;
    }

    // ---- eyes (single socket, patterns handle multiplicity) ----
    case 'bug_eyes': {
      const n = clamp(3 + Math.round(count * 3), 3, 6);
      const spots = [[0,0],[ -0.42,0.1],[0.42,0.1],[-0.2,0.42],[0.24,0.44],[0,-0.34]];
      const R = 0.42 + 0.25*girth;
      for (let i = 0; i < n; i++) {
        const [ex, ey] = spots[i];
        eyeball(mb, [S[0] + ex*sock.faceR, S[1] + ey*1.4, S[2] - Math.abs(ex)*0.35],
          R * (i === 0 ? 1.15 : 0.9), o.skin, i === 0 ? 0.55 : 0.3);
      }
      // angry V-brows over the cluster
      for (const s of [-1, 1])
        tube(mb, [
          [S[0] + s*sock.faceR*0.62, S[1] + 1.05, S[2] - 0.05],
          [S[0] + s*0.12,            S[1] + 0.58, S[2] + 0.14],
        ], [0.17, 0.13], sh(o.skin, 0.55), 0.25, 0, 6);
      break;
    }
    case 'cyclops_eye': {
      const R = 0.85 + 0.55*girth;
      eyeball(mb, S, R, o.skin, 0.7);
      // one heavy scowling unibrow
      tube(mb, [
        [S[0] - R*1.05, S[1] + R*0.72, S[2] - 0.15],
        [S[0],          S[1] + R*0.98, S[2] + 0.05],
        [S[0] + R*1.05, S[1] + R*0.72, S[2] - 0.15],
      ], [0.2, 0.26, 0.2], sh(o.skin, 0.5), 0.25, 0, 7);
      break;
    }
    case 'stalk_eyes': {
      const L = 1.1 + 1.4*len;
      for (const s of [-1, 1]) {
        const top = [S[0] + s*0.75, S[1] + L, S[2] + 0.25];
        tube(mb, [[S[0] + s*0.45, S[1] - 0.3, S[2] - 0.3], [S[0]+s*0.7, S[1]+L*0.6, S[2]], top],
          [0.16, 0.13, 0.11], BONDK, 0.3, 0, 6);
        eyeball(mb, top, 0.4 + 0.2*girth, o.skin, 0.25);
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
      const R = (0.5 + 0.35*girth);
      tube(mb, [[S[0], sock.len + 0.6, S[2]], [S[0], 0.7, S[2]]], [R*1.15, R], o.skin, 0.28, 0, 9);
      tube(mb, [[S[0], 0.75, S[2]], [S[0], 0.0, S[2] + 0.1]], [R*1.02, R*1.15], HOOF, 0.5, 0, 9, 2);
      break;
    }
    case 'talon_leg': {
      const R = 0.24 + 0.12*girth;
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
      const R = 0.2 + 0.1*girth;
      tube(mb, [
        [S[0], sock.len + 0.5, S[2]],
        [S[0] + side*1.3, sock.len + 1.1 + curl*0.8, S[2] - 0.2],
        [S[0] + side*1.7, 0.6, S[2] + 0.1],
        [S[0] + side*1.2, 0.0, S[2] + 0.3],
      ], [R*1.2, R, R*0.85, 0.05], lp(CHITIN, o.skin, 0.35), 0.4, 0, 7);
      break;
    }
    case 'piston_leg': {
      tube(mb, [[S[0], sock.len + 0.7, S[2]], [S[0], 0.85, S[2]]], [0.5, 0.5], METAL, 0.8, 0, 10);
      tube(mb, [[S[0], 0.9, S[2]], [S[0], 0.25, S[2]]], [0.26, 0.26], sh(METAL, 1.25), 0.9, 0, 8);
      tube(mb, [[S[0], 0.3, S[2]], [S[0], 0.0, S[2]]], [0.72, 0.78], METDK, 0.6, 0, 10, 2);
      break;
    }
    case 'leg_stump': {
      ellipsoid(mb, [S[0], sock.len * 0.6, S[2]], [0.6, 0.55, 0.6], PALLOR, 0.25, 0, 8);
      ringStitch(mb, [S[0], sock.len * 0.35, S[2]], 0.55);
      break;
    }
  }
}

/** Chunky little arm from shoulder to a hanging wrist; returns wrist pos. */
function armDrop(mb, S, side, armR, scale, o) {
  const elbow = [S[0] + side*0.9*scale, S[1] - 1.1*scale, S[2] + 0.15];
  const wrist = [S[0] + side*1.15*scale, S[1] - 2.3*scale, S[2] + 0.45];
  tube(mb, [S, elbow, wrist], [armR*1.2, armR, armR*0.85],
    o.skinFn ? o.skin : CHITIN, 0.3, 0, 9, 1, o.skinFn ? null : undefined);
  return wrist;
}

/** Glossy toy eye with a hooded, skin-coloured upper lid. `hood` 0..1 sets
 * how heavily the lid droops — the b-movie menace dial. */
function eyeball(mb, c, r, skin, hood = 0.4) {
  ellipsoid(mb, c, [r, r, r], EYEWH, 0.85, 0, 10);
  ellipsoid(mb, [c[0], c[1], c[2] + r*0.72], [r*0.42, r*0.42, r*0.3], PUPIL, 0.95, 0, 8);
  if (hood > 0)
    ellipsoid(mb, [c[0], c[1] + r*(0.62 - 0.28*hood), c[2] - r*0.10],
      [r*1.07, r*(0.42 + 0.34*hood), r*1.03], skin, 0.3, 0, 8);
}

function ringStitch(mb, c, r) {
  const pts = [];
  for (let i = 0; i <= 12; i++) {
    const a = (i / 12) * Math.PI * 2;
    pts.push([c[0] + Math.cos(a)*r, c[1], c[2] + Math.sin(a)*r]);
  }
  tube(mb, pts, pts.map(() => 0.07), STITCH, 0.1, 0, 4, 0);
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
attribute vec3 aPos, aNor, aCol;
attribute vec2 aMat;
uniform mat4 uPV;
uniform float uCos, uSin;
varying vec3 vNor, vCol, vPos;
varying vec2 vMat;
void main() {
  vec3 p = vec3(aPos.x*uCos - aPos.z*uSin, aPos.y, aPos.x*uSin + aPos.z*uCos);
  vec3 n = vec3(aNor.x*uCos - aNor.z*uSin, aNor.y, aNor.x*uSin + aNor.z*uCos);
  vNor = n; vCol = aCol; vMat = aMat; vPos = p;
  gl_Position = uPV * vec4(p, 1.0);
}`;

const FS_CREATURE = `
precision mediump float;
varying vec3 vNor, vCol, vPos;
varying vec2 vMat;
uniform vec3 uEye;
uniform float uFlash, uPulse;
void main() {
  vec3 n = normalize(vNor);
  if (!gl_FrontFacing) n = -n;                      // two-sided: nothing inverts
  vec3 view = normalize(uEye - vPos);
  vec3 key  = normalize(vec3(-0.5, 0.75, 0.65));
  vec3 moon = normalize(vec3(0.65, 0.30, -0.55));
  float d = max(dot(n, key), 0.0);
  float band = smoothstep(0.02, 0.12, d) * 0.42
             + smoothstep(0.30, 0.42, d) * 0.36
             + smoothstep(0.62, 0.74, d) * 0.22;    // 3-step toon ramp
  vec3 hemi = mix(vec3(0.17, 0.13, 0.25), vec3(0.34, 0.30, 0.44), n.y * 0.5 + 0.5);
  vec3 lit = vCol * (hemi + vec3(1.0, 0.93, 0.80) * (band * 1.15 + uFlash));
  vec3 h = normalize(view + key);                   // vinyl sheen
  lit += vec3(1.0, 0.97, 0.9) * pow(max(dot(n, h), 0.0), mix(14.0, 90.0, vMat.x))
       * (0.18 + 0.5 * vMat.x);
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
    gl, progC, progQ, progG, bgTex, shTex, quadBuf,
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
  for (let i = 0; i < mb.v.length; i += 11)
    mr = Math.max(mr, Math.hypot(mb.v[i], mb.v[i + 2]));
  R.maxR = Math.min(mr, 13);
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
  const stride = 44;
  const attr = (name, size, off) => {
    const a = gl.getAttribLocation(p, name);
    gl.enableVertexAttribArray(a);
    gl.vertexAttribPointer(a, size, gl.FLOAT, false, stride, off);
  };
  attr('aPos', 3, 0); attr('aNor', 3, 12); attr('aCol', 3, 24); attr('aMat', 2, 36);
  gl.uniformMatrix4fv(gl.getUniformLocation(p, 'uPV'), false, R.pv);
  gl.uniform1f(gl.getUniformLocation(p, 'uCos'), Math.cos(_theta));
  gl.uniform1f(gl.getUniformLocation(p, 'uSin'), Math.sin(_theta));
  gl.uniform3fv(gl.getUniformLocation(p, 'uEye'), R.eye);
  gl.uniform1f(gl.getUniformLocation(p, 'uFlash'), flash);
  gl.uniform1f(gl.getUniformLocation(p, 'uPulse'), pulse);
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
