/**
 * "Cinemaware" voxel scene renderer for The Lab.
 * No deps. Pure Canvas 2D.
 *
 * Native canvas: 320 × 200 px (classic Amiga lo-res), displayed 2× by CSS
 * (image-rendering: pixelated).
 *
 * Creatures are authored on a coarse voxel grid, then refined 2× at load:
 * every coarse voxel splits into 2×2×2 fine voxels (3 native px each),
 * exposed corners of bulky masses are shaved for rounded forms, and the
 * shading is BAKED into the fine voxels' colours — neighbourhood ambient
 * occlusion, vertical light falloff, two octaves of mottled-hide noise,
 * pale belly / dusky spine. Runtime lighting rides on top of the bake.
 *
 * Art direction: Jim Sachs / Defender-of-the-Crown-era Amiga box art —
 *  - hand-dithered sky gradients (4×4 ordered Bayer),
 *  - a painted moonlit backdrop (full moon, clouds, castle, dead tree),
 *  - warm key light upper-left, cool moon rim-light from the right,
 *  - shadows that fall toward indigo, highlights toward warm cream,
 *  - the specimen slowly rotating on a stone display platform.
 *
 * Voxel coordinate system: Y=up, X=right, Z=toward viewer.
 * Origin = bottom-centre of creature (feet at Y=0).
 */

// ── canvas geometry ─────────────────────────────────────────────────────────
const NW = 320, NH = 200;
const CX = 160;            // creature centre X
const GY = 178;            // ground line (platform surface) in screen px
const SC = 3;              // native pixels per FINE voxel (coarse voxel = 2 fine)
const HORIZON = 150;

// ── colours [r,g,b] ─────────────────────────────────────────────────────────
const FLESH   = [195, 118,  78];
const PALLOR  = [192, 172, 152];   // sickly low-vigor flesh

// The b-movie skin swatch book. The body's posture gene doubles as the
// pigment gene (it has no other visual expression yet): a monster's hue
// breeds and drifts like any other trait.
const SKIN_ANCHORS = [
  [ 92, 138,  74],   // bog green
  [148, 152,  66],   // olive
  [195, 118,  78],   // classic flesh
  [124, 134, 152],   // cadaver grey-blue
  [142,  92, 168],   // mutant violet
  [172,  70,  58],   // rust red
];

function skinTone(t) {
  const s = Math.min(0.999, Math.max(0, t)) * (SKIN_ANCHORS.length - 1);
  const i = Math.floor(s);
  return lp(SKIN_ANCHORS[i], SKIN_ANCHORS[i + 1], s - i);
}
const BONE    = [212, 200, 170];
const BONDK   = [158, 148, 118];
const METAL   = [ 96, 110, 124];
const METDK   = [ 60,  72,  84];
const GLOW    = [255, 140,  20];
const CHITIN  = [ 40,  78,  52];
const EYEWH   = [230, 230, 210];
const PUPIL   = [ 12,   8,  18];
const GPUPIL  = [ 20, 210,  80];   // biotech glowing pupil
const HOOF    = [ 48,  40,  30];
const CLAW    = [170, 158, 128];
const BOLT    = [ 80,  92, 106];
const BLTGLO  = [255, 200,  40];
const ICHOR   = [135,  75, 215];
const STITCHD = [ 52,  30,  22];   // suture dark line

// materials that emit light (get a halo pass)
const GLOW_KEYS = new Set([GLOW, BLTGLO, ICHOR, GPUPIL].map(c => c.join(',')));

// colour helpers
const lp = (a, b, t) => a.map((v, i) => Math.round(v + (b[i] - v) * t));
const sh = (c, f)    => c.map(v => Math.min(255, Math.max(0, Math.round(v * f))));

// 4×4 ordered Bayer matrix — the signature Amiga dither
const B4 = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]];

// ── Sachs colour ramps ───────────────────────────────────────────────────────
// Shadows sink toward indigo, highlights climb toward warm cream.
const SHADOW_TINT = [ 26,  20,  56];
const WARM_HIGH   = [255, 242, 214];
const RAMP_N = 13;            // indices 0..12; lighting uses 0..10, deco +2
const _rampCache = new Map();

function rampFor(col) {
  const key = col.join(',');
  let r = _rampCache.get(key);
  if (r) return r;
  const dark = lp(sh(col, 0.30), SHADOW_TINT, 0.55);
  r = [];
  for (let i = 0; i < RAMP_N; i++) {
    const t = i / (RAMP_N - 1);
    r.push(t < 0.5 ? lp(dark, col, t * 2) : lp(col, WARM_HIGH, (t - 0.5) * 2 * 0.85));
  }
  _rampCache.set(key, r);
  return r;
}

// ── voxel primitives ────────────────────────────────────────────────────────
// Each voxel: [x, y, z, r, g, b]

function vox(x, y, z, col) { return [[x, y, z, ...col]]; }

function box(x, y, z, w, h, d, col) {
  const v = [];
  for (let dx = 0; dx < w; dx++)
    for (let dy = 0; dy < h; dy++)
      for (let dz = 0; dz < d; dz++)
        v.push([x+dx, y+dy, z+dz, ...col]);
  return v;
}

function sph(cx, cy, cz, r, col) {
  const v = [], r2 = r * r + r * 0.6;
  for (let dx = -r; dx <= r; dx++)
    for (let dy = -r; dy <= r; dy++)
      for (let dz = -r; dz <= r; dz++)
        if (dx*dx + dy*dy + dz*dz <= r2)
          v.push([cx+dx, cy+dy, cz+dz, ...col]);
  return v;
}

function mirrorX(voxels) {
  return voxels.map(([x,y,z,...col]) => [-x-1, y, z, ...col]);
}

// ── part shapes ─────────────────────────────────────────────────────────────
// Functions take params=[length,girth,taper,curl,count,ornament] and return
// voxels in local space (socket = [0,0,0]).
// All Y coordinates go upward from socket; a leg socket is at foot level.

function shapeClawHand(params, fc, right) {
  const [len, girth, , curl, count] = params;
  const v = [];
  const palmW = 1 + Math.round(girth * 1.5);
  v.push(...box(0, 0, -1, palmW, 2, 2, fc));
  const nClaws = 2 + Math.round(count * 2);
  const clawLen = 2 + Math.round(len * 2);
  for (let i = 0; i < Math.min(nClaws, 4); i++) {
    const cx = i % 2 === 0 ? 0 : 1;
    const cz = i < 2 ? 0 : 1;
    for (let c = 0; c < clawLen; c++) {
      const dropY = curl > 0.4 ? Math.max(0, c - 1) : 0;
      v.push([cx, 2 + c - dropY, cz, ...CLAW]);
    }
  }
  return right ? mirrorX(v) : v;
}

function shapePincer(params, fc, right) {
  const [len, girth] = params;
  const v = [];
  const jawLen = 2 + Math.round(len * 3);
  v.push(...box(0, 0, -1, 2, 1, 2, fc));
  for (let j = 0; j < jawLen; j++) {
    v.push([0, 1 + j, 0, ...CLAW]);
    v.push([1, 1 + j + (j === 0 ? 1 : 0), 0, ...CLAW]);
  }
  return right ? mirrorX(v) : v;
}

function shapeTentacle(params, fc, right) {
  const [len, girth, taper] = params;
  const v = [];
  const tentLen = 3 + Math.round(len * 4);
  for (let j = 0; j < tentLen; j++) {
    const w = Math.max(1, Math.round((1 - (j / tentLen) * taper) * (1 + girth)));
    v.push(...box(0, j, 0, w, 1, w, fc));
  }
  return right ? mirrorX(v) : v;
}

function shapeRifleArm(params, right) {
  const v = [
    ...box(0, 0, -1, 1, 1, 5, METDK),   // barrel
    ...box(-1, 0, -3, 3, 2, 2, METAL),  // stock body
    ...vox(0, 0, 2, GLOW),              // muzzle flash port
  ];
  return right ? mirrorX(v) : v;
}

function shapePlasmaLance(params, right) {
  const [len] = params;
  const v = [
    ...box(0, 0, -1, 2, 2, 2, CHITIN),
  ];
  const lanceLen = 2 + Math.round(len * 2);
  for (let j = 0; j < lanceLen; j++)
    v.push([0, 2 + j, 0, ...ICHOR]);
  v.push([0, 2 + lanceLen, 0, ...BLTGLO]);
  return right ? mirrorX(v) : v;
}

function shapeHandStump(right) {
  const v = box(0, 0, -1, 1, 1, 1, PALLOR);
  return right ? mirrorX(v) : v;
}

// sensor / eye

// Sensors mount as MIRRORED PAIRS on the head sides (except on the
// asymmetric serpentine): feelers, devil horns, twin masts — never a
// single flagpole in the middle of the skull.

function shapeAntenna(params, right) {
  const [len, girth] = params;
  const stalkH = 2 + Math.round(len * 3);
  const v = [];
  for (let j = 0; j < stalkH; j++)
    v.push([Math.floor(j / 2), j, 0, ...BONE]);          // feeler drifts outward
  v.push(...sph(Math.floor(stalkH / 2), stalkH, 0, girth > 0.3 ? 1 : 0, BONDK));
  return right ? mirrorX(v) : v;
}

function shapeHorn(params, fc, right) {
  const [, girth, , curl] = params;
  const v = [];
  const w = 1 + Math.round(girth * 1.5);
  v.push(...box(0, 0, 0, w, 1, w, fc));
  v.push(...box(0, 1, 0, Math.max(1, w-1), 2, Math.max(1, w-1), sh(fc, 0.85)));
  v.push(...box(Math.max(0, w - 2), 3, 0, 1, 2, 1, sh(fc, 0.7)));   // tip leans outward
  if (curl > 0.4) v.push(...vox(Math.max(1, w - 1), 4, 0, sh(fc, 0.7)));
  return right ? mirrorX(v) : v;
}

function shapeSensorMast(params, right) {
  const [len] = params;
  const h = 3 + Math.round(len * 2);
  const v = [];
  for (let j = 0; j < h; j++) v.push([0, j, 0, ...METAL]);
  v.push([0, h, 0, ...GLOW]);
  v.push([-1, h, 0, ...METDK]);
  v.push([1, h, 0, ...METDK]);
  return right ? mirrorX(v) : v;
}

function shapeSensorStub(right) {
  const v = box(0, 0, 0, 1, 1, 1, PALLOR);
  return right ? mirrorX(v) : v;
}

function shapeBugEyes(params) {
  const [, girth, , , count] = params;
  const n = 2 + Math.round(count * 3);
  const v = [];
  const positions = [
    [0,0,0],[2,0,0],[1,1,0],[0,1,0],[2,1,0]
  ];
  for (let i = 0; i < Math.min(n, 5); i++) {
    const [ex, ey, ez] = positions[i];
    v.push(...sph(ex, ey, ez, girth > 0.5 ? 1 : 0, EYEWH));
    v.push([ex, ey, ez + 1, ...PUPIL]);
  }
  return v;
}

function shapeCyclopsEye(params) {
  const [, girth] = params;
  const r = 1 + Math.round(girth * 1.5);
  const v = sph(0, 0, 0, r, EYEWH);
  v.push([0, 0, r, ...PUPIL]);
  return v;
}

function shapeStalkEyes(params) {
  const [len, , , , count] = params;
  const n = 1 + Math.round(count * 1);
  const stalkH = 2 + Math.round(len * 3);
  const v = [];
  const offsets = [[0,0],[2,0]];
  for (let i = 0; i < Math.min(n, 2); i++) {
    const [ex] = offsets[i];
    for (let j = 0; j < stalkH; j++) v.push([ex, j, 0, ...BONDK]);
    v.push(...sph(ex, stalkH, 0, 1, EYEWH));
    v.push([ex, stalkH, 1, ...PUPIL]);
  }
  return v;
}

function shapeOpticVisor(params) {
  const [, , , , count] = params;
  const nLenses = 1 + Math.round(count * 2);
  const v = [
    ...box(-1, -1, 0, 5, 3, 1, METDK),   // visor band
  ];
  for (let i = 0; i < Math.min(nLenses, 3); i++) {
    v.push([i * 2, 0, 1, ...GLOW]);
  }
  return v;
}

function shapeEyeSocket() {
  return box(0, 0, 0, 2, 2, 1, sh(FLESH, 0.65));
}

// legs

function shapeHoofedLeg(params, fc, right) {
  const [len, girth] = params;
  const w = 1 + Math.round(girth * 1.5);
  const h = 3 + Math.round(len * 2);
  const v = [
    ...box(0, 0, 0, w+1, 1, w+1, HOOF),       // hoof plate
    ...box(0, 1, 0, w, h, w, fc),              // leg column
  ];
  return right ? mirrorX(v) : v;
}

function shapeTalonLeg(params, fc, right) {
  const [len, , , , count] = params;
  const h = 3 + Math.round(len * 3);
  const nToes = 2 + Math.round(count * 2);
  const v = [];
  // thin shin
  v.push(...box(0, 0, 0, 1, h, 1, fc));
  // knee bump (backward)
  v.push([0, Math.round(h * 0.5), -1, ...sh(fc, 0.8)]);
  // splayed toes at bottom
  for (let t = 0; t < Math.min(nToes, 4); t++) {
    v.push([t - 1, 0, t % 2, ...CLAW]);
    v.push([t - 1, -1, t % 2, ...CLAW]);
  }
  return right ? mirrorX(v) : v;
}

function shapeInsectLeg(params, fc, right) {
  const [len, girth, , curl] = params;
  const seg = 2 + Math.round(len * 2);
  const v = [];
  for (let s = 0; s < 3; s++) {
    const angle = s * 1.2;
    for (let j = 0; j < seg; j++) {
      const y = s * seg + j;
      const z = Math.round(Math.sin(angle + j * 0.5) * 1.5);
      v.push([0, y, z, ...fc]);
    }
  }
  v.push([0, -1, 0, ...CLAW]);
  return right ? mirrorX(v) : v;
}

function shapePistonLeg(params, right) {
  const [len, girth] = params;
  const h = 3 + Math.round(len * 2);
  const w = 1 + Math.round(girth * 1);
  const v = [
    ...box(-1, 0, -1, w+2, 1, w+2, METDK),    // foot plate
    ...box(0, 1, 0, w, h, w, METAL),           // piston body
    ...box(0, h+1, 0, w, 1, w, METDK),         // cap
  ];
  return right ? mirrorX(v) : v;
}

function shapeLegStump(right) {
  const v = box(0, 0, 0, 2, 1, 2, PALLOR);
  return right ? mirrorX(v) : v;
}

// bolt decoration for heart tier on neck; xo = how far out the bolts sit
function boltRow(y, count, glowing, xo = 2) {
  const col = glowing ? BLTGLO : BOLT;
  const positions = [
    [[-xo, y, -1], [xo, y, -1]],
    [[-xo, y, -1], [xo, y, -1], [-xo, y, 1], [xo, y, 1]],
    [[-xo, y, -1], [xo, y, -1], [-xo, y, 0], [xo, y, 0], [-xo, y, 1], [xo, y, 1]],
  ];
  const n = Math.min(count, 3);
  if (n < 1) return [];
  return positions[n-1].map(([x,y,z]) => [x, y, z, ...col]);
}

// ── body plan assembly ───────────────────────────────────────────────────────

function buildTetrapod(g, fleshCol) {
  // The b-movie brute: a wide barrel torso on stubby legs, the head sunk
  // straight into hunched shoulders — no neck to speak of.
  const bulk = g.body.params[1] ?? 0.5;
  const tw = 5 + Math.round(bulk * 3);   // torso width 5–8
  const td = 4;                           // torso depth
  const th = 4 + Math.round(bulk);        // torso height 4–5
  const tx = -Math.floor(tw / 2);
  const ty = 2;                           // torso floats on stubby legs

  // brain tier affects head size
  const headScale = { dim: 1, average: 1, gifted: 1, mastermind: 2 }[g.brain.tier] ?? 1;
  const hw = 4 + headScale;
  const hh = 3 + headScale;
  const hx = -Math.floor(hw / 2);

  const headY = ty + th;                  // head sits right on the shoulders
  const topY  = headY + hh;

  // socket world positions (left-side; right gets mirrorX)
  const sockets = {
    hand:   [Math.ceil(tw / 2), headY - 3, 0],        // arm: just outside torso edge
    leg:    [Math.floor(tw / 4), 0, 0],               // leg: under torso (mirrored = other foot)
    sensor: [Math.max(1, Math.floor(hw / 2) - 1), topY, 0],  // paired, on the skull's corners
    eye:    [hx + Math.floor(hw/2) - 1, headY + Math.floor(hh/2) - 1, Math.floor(hw/2) + 1],
  };

  const v = [];

  // barrel torso
  v.push(...box(tx, ty, -2, tw, th, td, fleshCol));
  // shoulder hunch looming behind the head
  v.push(...box(tx, headY, -2, tw, 1, 2, sh(fleshCol, 0.94)));
  // head
  v.push(...box(hx, headY, -Math.floor(hw/2), hw, hh, hw, fleshCol));
  // cranial bump for mastermind
  if (g.brain.tier === 'mastermind')
    v.push(...box(hx+1, headY + hh, -Math.floor(hw/2)+1, hw-2, 1, hw-2, sh(fleshCol, 1.05)));

  // b-movie face: a dark mouth gash with an underbite tusk at each corner
  const faceZ = hw - Math.floor(hw / 2) - 1;
  for (let x = hx + 1; x <= hx + hw - 2; x++)
    v.push([x, headY, faceZ, 26, 14, 20]);
  v.push([hx + 1, headY, faceZ + 1, ...CLAW]);
  v.push([hx + hw - 2, headY, faceZ + 1, ...CLAW]);

  // Sachs monster trademark: suture seam zig-zagging across the chest.
  // Same coordinates as torso-front voxels — dedupe pass makes them decals.
  const seamY = ty + Math.round(th * 0.5);
  for (let x = tx; x < tx + tw; x++)
    v.push([x, seamY + (((x - tx) % 2) ? 1 : 0), 1, ...STITCHD]);

  // neck bolts from heart tier, jutting from the head base
  const heartLevel = ['faint','steady','strong','titan'].indexOf(g.heart.tier);
  if (heartLevel >= 1) {
    const glowing = heartLevel >= 3;
    v.push(...boltRow(headY, heartLevel, glowing, Math.floor(hw / 2) + 1));
  }

  return { voxels: v, sockets };
}

function buildBlob(g, fleshCol) {
  const bulk = g.body.params[1] ?? 0.5;
  const r = 3 + Math.round(bulk * 2);
  const v = sph(0, r+1, 0, r, fleshCol);
  // surface ripples
  for (let a = 0; a < 6; a++) {
    const ax = Math.round(Math.cos(a * 1.05) * (r + 1));
    const az = Math.round(Math.sin(a * 1.05) * (r + 1));
    v.push([ax, r + 1, az, ...sh(fleshCol, 0.75)]);
  }

  const sockets = {
    hand:   [r, r + 1, 0],
    leg:    [r * 0.4, 0, 0],
    sensor: [2, r * 2, 0],       // paired, on the dome's shoulders
    eye:    [0, r + 2, r + 1],
  };
  return { voxels: v, sockets };
}

function buildSerpentine(g, fleshCol) {
  const len = g.body.params[2] ?? 0.5;
  const bulk = g.body.params[1] ?? 0.5;
  const r = 1 + Math.round(bulk * 1.5);
  const segs = 5 + Math.round(len * 4);
  const v = [];

  // coiling body
  for (let s = 0; s < segs; s++) {
    const t = s / segs;
    const y = 1 + s * 1.2;
    const x = Math.round(Math.sin(t * Math.PI * 1.5) * 3);
    const segR = Math.max(1, Math.round(r * (1 - t * 0.6)));
    v.push(...sph(x, y, 0, segR, fleshCol));
  }

  // head, with a dark mouth line and a fang pair
  const headX = Math.round(Math.sin(1.5 * Math.PI * 0.9) * 3);
  const headY  = 1 + segs * 1.2;
  v.push(...box(headX - 2, headY, -1, 4, 3, 3, fleshCol));
  for (let x = headX - 1; x <= headX + 1; x++) v.push([x, headY, 1, 26, 14, 20]);
  v.push([headX - 1, headY, 2, ...CLAW]);
  v.push([headX + 1, headY, 2, ...CLAW]);

  const sockets = {
    hand:   [headX + 2, headY + 1, 0],
    sensor: [headX, headY + 3, 0],
    eye:    [headX, headY + 1, 2],
    leg:    [0, 0, 0],  // serpentine: leg slot is vestigial / ignored
  };
  return { voxels: v, sockets };
}

function buildWinged(g, fleshCol) {
  const limb = g.body.params[2] ?? 0.5;
  const wingspan = 5 + Math.round(limb * 5);   // 5–10 columns per side
  const v = [];

  // squat body, head sunk on the shoulders
  v.push(...box(-2, 3, -2, 4, 5, 4, fleshCol));
  v.push(...box(-2, 8, -2, 4, 4, 4, fleshCol));

  // bat wings: a bone leading edge over a solid membrane sheet
  const wingCol = sh(fleshCol, 0.72);
  for (let wx = 0; wx < wingspan; wx++) {
    const t = wx / Math.max(1, wingspan - 1);
    const wy = 7 + Math.round(4 * Math.sin(Math.PI * (0.15 + 0.85 * t))); // arc up, tips droop
    const depth = wx < wingspan * 0.5 ? 3 : 2;
    for (const side of [-1, 1]) {
      const X = side === 1 ? wx + 2 : -(wx + 3);
      v.push([X, wy, -1, ...BONDK]);                                     // wing bone
      for (let m = 1; m <= depth; m++) v.push([X, wy - m, -1, ...wingCol]); // membrane
    }
  }

  const sockets = {
    hand:   [3, 4, 0],
    leg:    [1, 0, 0],
    sensor: [1, 12, 0],          // paired, on the skull's corners
    eye:    [0, 9, 2],
  };
  return { voxels: v, sockets };
}

// ── full creature assembly ───────────────────────────────────────────────────

export function assembleCreature(genome) {
  // skin: the posture gene picks the pigment, heart vigor washes it toward
  // sickly pallor when the pump is weak
  const vigor = genome.heart?.params?.[0] ?? 0.5;
  const hue   = genome.body?.params?.[0] ?? 0.5;
  const fleshCol = lp(PALLOR, skinTone(hue), 0.40 + 0.60 * vigor);

  const plan = genome.body?.plan ?? 'tetrapod';
  const builders = { tetrapod: buildTetrapod, blob: buildBlob, serpentine: buildSerpentine, winged: buildWinged };
  const builder  = builders[plan] ?? buildTetrapod;
  const { voxels, sockets } = builder(genome, fleshCol);

  const allVoxels = [...voxels];

  // attach parts at sockets
  const slots = genome.slots ?? {};
  const SLOT_NAMES = ['hand', 'sensor', 'eye', 'leg'];

  for (const slotName of SLOT_NAMES) {
    const allele = slots[slotName];
    if (!allele) continue;
    const { family, params } = allele;
    const sock = sockets[slotName];
    if (!sock) continue;

    const [sx, sy, sz] = sock;
    const partVoxels = buildPart(slotName, family, params, fleshCol);
    for (const pv of partVoxels)
      allVoxels.push([pv[0] + sx, pv[1] + sy, pv[2] + sz, pv[3], pv[4], pv[5]]);

    // mirrored copy for bilateral parts (hands, legs, and paired sensors —
    // the serpentine keeps its single crest, its head is off-axis)
    if (slotName === 'hand' || slotName === 'leg' ||
        (slotName === 'sensor' && plan !== 'serpentine')) {
      const partVoxelsR = buildPart(slotName, family, params, fleshCol, true);
      const [rx, ry, rz] = [-sx - 1, sy, sz];  // X mirror of socket
      for (const pv of partVoxelsR)
        allVoxels.push([pv[0] + rx, pv[1] + ry, pv[2] + rz, pv[3], pv[4], pv[5]]);
    }
  }

  return allVoxels;
}

function buildPart(slotName, family, params, fc, right = false) {
  // origin → base colour (mirrors catalog.ts origins)
  const techFams    = new Set(['rifle_arm','sensor_mast','optic_visor','piston_leg']);
  const biotechFams = new Set(['plasma_lance']);
  const partCol = techFams.has(family) ? METAL : biotechFams.has(family) ? CHITIN : fc;

  switch (family) {
    // hand homologs
    case 'claw_hand':   return shapeClawHand(params, partCol, right);
    case 'pincer':      return shapePincer(params, partCol, right);
    case 'tentacle':    return shapeTentacle(params, partCol, right);
    case 'rifle_arm':   return shapeRifleArm(params, right);
    case 'plasma_lance':return shapePlasmaLance(params, right);
    case 'hand_stump':  return shapeHandStump(right);
    // sensor homologs
    case 'antenna':     return shapeAntenna(params, right);
    case 'horn':        return shapeHorn(params, partCol, right);
    case 'sensor_mast': return shapeSensorMast(params, right);
    case 'sensor_stub': return shapeSensorStub(right);
    // eye homologs
    case 'bug_eyes':    return shapeBugEyes(params);
    case 'cyclops_eye': return shapeCyclopsEye(params);
    case 'stalk_eyes':  return shapeStalkEyes(params);
    case 'optic_visor': return shapeOpticVisor(params);
    case 'eye_socket':  return shapeEyeSocket();
    // leg homologs
    case 'hoofed_leg':  return shapeHoofedLeg(params, partCol, right);
    case 'talon_leg':   return shapeTalonLeg(params, partCol, right);
    case 'insect_leg':  return shapeInsectLeg(params, partCol, right);
    case 'piston_leg':  return shapePistonLeg(params, right);
    case 'leg_stump':   return shapeLegStump(right);
    default:            return [];
  }
}

// ── model preparation: subdivide 2×, sculpt, bake shading ───────────────────
// The coarse authored model is refined into a fine grid (8 fine voxels per
// coarse), exposed corners of bulky masses are chamfered away for rounded
// forms (thin features — stalks, claws, membranes, bolts — are protected),
// interiors are culled, and shading is baked into each surviving voxel's
// colour. Normals are baked too; per-frame lighting rotates them.

const DIRS = [[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]];

function hash3(x, y, z) {
  let h = (x * 374761393 + y * 668265263 + z * 1274126177) | 0;
  h = ((h ^ (h >> 13)) * 1103515245) | 0;
  return h >>> 16;
}

function prepareModel(genome) {
  const raw = assembleCreature(genome);

  // dedupe coarse by coordinate — later voxels win, so decals (stitches,
  // mouths) overwrite the surface they sit on. Coordinates are snapped to
  // integers: some builders emit fractional lattices (serpentine coils),
  // and unsnapped they never see each other as neighbours.
  const coarse = new Map();
  for (const v of raw) {
    const x = Math.round(v[0]), y = Math.round(v[1]), z = Math.round(v[2]);
    coarse.set(`${x},${y},${z}`, [x, y, z, v[3], v[4], v[5]]);
  }

  // thin coarse features keep their full form through the chamfer
  const cOcc = new Set(coarse.keys());
  const thinKeys = new Set();
  for (const [k, [x, y, z]] of coarse) {
    let n = 0;
    for (const [dx, dy, dz] of DIRS) if (cOcc.has(`${x+dx},${y+dy},${z+dz}`)) n++;
    if (n <= 2) thinKeys.add(k);
  }

  // subdivide: each coarse voxel → 2×2×2 fine voxels
  const fine = new Map();
  for (const [k, [x, y, z, r, g, b]] of coarse) {
    const thin = thinKeys.has(k);
    for (let i = 0; i < 2; i++)
      for (let j = 0; j < 2; j++)
        for (let l = 0; l < 2; l++) {
          const fx = 2 * x + i, fy = 2 * y + j, fz = 2 * z + l;
          fine.set(`${fx},${fy},${fz}`, { x: fx, y: fy, z: fz, col: [r, g, b], thin });
        }
  }

  // wedge fill: fillet concave steps with averaged-colour fine voxels, so
  // stepped curves (blocky spheres, coils) smooth into 45° slopes instead
  // of reading as a pile of separate cubes
  {
    const cands = new Map();   // empty coarse cells adjacent to filled ones
    for (const [, [x, y, z]] of coarse)
      for (const [dx, dy, dz] of DIRS) {
        const k = `${x+dx},${y+dy},${z+dz}`;
        if (!coarse.has(k)) cands.set(k, [x+dx, y+dy, z+dz]);
      }
    for (const [, [x, y, z]] of cands) {
      for (let i = 0; i < 2; i++)
        for (let j = 0; j < 2; j++)
          for (let l = 0; l < 2; l++) {
            const nbs = [
              coarse.get(`${x + (i ? 1 : -1)},${y},${z}`),
              coarse.get(`${x},${y + (j ? 1 : -1)},${z}`),
              coarse.get(`${x},${y},${z + (l ? 1 : -1)}`),
            ].filter(Boolean);
            if (nbs.length < 2) continue;
            const fk = `${2*x+i},${2*y+j},${2*z+l}`;
            if (fine.has(fk)) continue;
            const col = [3, 4, 5].map(ci =>
              Math.round(nbs.reduce((s, n) => s + n[ci], 0) / nbs.length));
            fine.set(fk, { x: 2*x+i, y: 2*y+j, z: 2*z+l, col, thin: true });
          }
    }
  }

  // chamfer: shave exposed fine corners of bulky masses → rounded silhouettes
  {
    const occ = new Set(fine.keys());
    const doomed = [];
    for (const [k, f] of fine) {
      if (f.thin) continue;
      let n = 0;
      for (const [dx, dy, dz] of DIRS) if (occ.has(`${f.x+dx},${f.y+dy},${f.z+dz}`)) n++;
      if (n <= 3) doomed.push(k);
    }
    for (const k of doomed) fine.delete(k);
  }

  const occ = new Set(fine.keys());
  const voxels = [];
  let maxR = 1, maxY = 1;
  for (const f of fine.values()) maxY = Math.max(maxY, f.y + 1);

  for (const f of fine.values()) {
    const { x, y, z } = f;
    let nx = 0, ny = 0, nz = 0, exposed = false;
    for (const [dx, dy, dz] of DIRS) {
      if (!occ.has(`${x+dx},${y+dy},${z+dz}`)) {
        nx += dx; ny += dy; nz += dz; exposed = true;
      }
    }
    if (!exposed) continue;                    // fully enclosed: never visible
    const nl = Math.hypot(nx, ny, nz) || 1;
    nx /= nl; ny /= nl; nz /= nl;

    const glow = GLOW_KEYS.has(f.col.join(','));

    // ---- baked colour shading ----
    let col = f.col;
    if (!glow) {
      // ambient occlusion from the 26-neighbourhood:
      // crevices sink into shadow, edges and bumps catch the light
      let n26 = 0;
      for (let dx = -1; dx <= 1; dx++)
        for (let dy = -1; dy <= 1; dy++)
          for (let dz = -1; dz <= 1; dz++)
            if ((dx || dy || dz) && occ.has(`${x+dx},${y+dy},${z+dz}`)) n26++;
      const ao = Math.min(1.12, Math.max(0.76, 1 + (12 - n26) * 0.022));
      // vertical light falloff + two octaves of mottled-hide noise
      const vert = 0.90 + 0.20 * (y / maxY);
      const blotch = 1 + ((hash3(x >> 2, y >> 2, z >> 2) % 7) - 3) * 0.017;
      const fleck  = 1 + ((hash3(x, y, z) % 5) - 2) * 0.012;
      col = sh(col, ao * vert * blotch * fleck);
      // pale belly, dusky spine — baked in, so it spins with the model
      if (nz > 0.3)       col = lp(col, [232, 208, 178], 0.16 * nz);
      else if (nz < -0.3) col = lp(col, [ 58,  42,  84], 0.14 * -nz);
      // quantize so the ramp cache stays small
      col = col.map(c => Math.min(250, Math.round(c / 10) * 10));
    }

    voxels.push({
      x, y, z,
      ramp: rampFor(col),
      nx, ny, nz,
      topExposed: !occ.has(`${x},${y+1},${z}`),
      glow, glowCol: f.col,
      // one halo per coarse glow cell, not eight
      glowRep: glow && (x & 1) === 0 && (y & 1) === 0 && (z & 1) === 0,
    });
    maxR = Math.max(maxR, Math.abs(x) + 1, Math.abs(z) + 1);
  }
  return { voxels, maxR, maxY };
}

// ── the painted backdrop (built once) ────────────────────────────────────────

let _bg = null;

function skyCol(t) {
  // piecewise gradient: zenith navy → violet → dusty magenta horizon
  if (t < 0.45) return lp([8, 6, 26], [24, 14, 52], t / 0.45);
  if (t < 0.75) return lp([24, 14, 52], [56, 28, 74], (t - 0.45) / 0.3);
  return lp([56, 28, 74], [116, 58, 96], (t - 0.75) / 0.25);
}

function buildBackground() {
  const c = document.createElement('canvas');
  c.width = NW; c.height = NH;
  const ctx = c.getContext('2d');
  const img = ctx.createImageData(NW, NH);
  const px = img.data;

  // deterministic scene RNG (fixed seed — the backdrop is a painting)
  let seed = 987654321;
  const rnd = () => ((seed = (seed * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff);

  const MX = 252, MY = 44, MR = 26;      // the moon
  const CRATERS = [[-8,-6,5],[6,4,4],[-2,9,3],[10,-9,4],[-14,3,3],[3,-14,2]];
  const STARS = [];
  for (let i = 0; i < 110; i++) {
    const x = Math.floor(rnd() * NW), y = Math.floor(rnd() * (HORIZON - 20));
    if (Math.hypot(x - MX, y - MY) < MR + 12) continue;
    STARS.push([x, y, rnd()]);
  }

  const SKY_STEPS = 26, GND_STEPS = 12;

  for (let y = 0; y < NH; y++) {
    for (let x = 0; x < NW; x++) {
      const bay = (B4[y & 3][x & 3] + 0.5) / 16;   // 0..1 dither threshold
      let col;

      if (y < HORIZON) {
        // dithered sky gradient
        let t = y / HORIZON + (bay - 0.5) / SKY_STEPS;
        t = Math.min(1, Math.max(0, t));
        col = skyCol(Math.floor(t * SKY_STEPS) / SKY_STEPS);

        // moon + halo
        const d = Math.hypot(x - MX, y - MY);
        if (d <= MR) {
          const f = d / MR;
          col = lp([232, 230, 208], [172, 170, 160], f * f);
          for (const [cx, cy, cr] of CRATERS)
            if (Math.hypot(x - MX - cx, y - MY - cy) < cr) col = [203, 199, 178];
        } else if (d < MR + 34) {
          const g = (1 - (d - MR) / 34) ** 2;
          if (g * 0.55 > bay * 0.35) col = lp(col, [150, 140, 200], g * 0.55);
        }
      } else {
        // dithered ground gradient
        let t = (y - HORIZON) / (NH - HORIZON) + (bay - 0.5) / GND_STEPS;
        t = Math.min(1, Math.max(0, t));
        col = lp([38, 30, 58], [70, 56, 88], Math.floor(t * GND_STEPS) / GND_STEPS);
        // moonlight sheen streak below the moon
        const w = 8 + (y - HORIZON) * 1.1;
        const dx = Math.abs(x - MX);
        if (dx < w) {
          const s = (1 - dx / w) * 0.5;
          if (s > bay * 0.6) col = lp(col, [130, 105, 130], s);
        }
      }

      // vignette — pull the corners down toward the dark
      const vx = (x - 160) / 160, vy = (y - 100) / 100;
      const vq = vx * vx + vy * vy;
      const f = Math.max(0.62, 1 - 0.38 * Math.max(0, vq - 0.55));
      const o = (y * NW + x) * 4;
      px[o] = col[0] * f; px[o+1] = col[1] * f; px[o+2] = col[2] * f; px[o+3] = 255;
    }
  }

  // stars (into the image data, over the sky)
  for (const [sx, sy, b] of STARS) {
    const o = (sy * NW + sx) * 4;
    const v = 120 + Math.floor(b * 135);
    px[o] = v; px[o+1] = v; px[o+2] = Math.min(255, v + 25); px[o+3] = 255;
    if (b > 0.88 && sx > 0 && sx < NW - 1 && sy > 0 && sy < HORIZON - 1) {
      for (const [ox, oy] of [[1,0],[-1,0],[0,1],[0,-1]]) {
        const oo = ((sy + oy) * NW + sx + ox) * 4;
        px[oo] = 110; px[oo+1] = 110; px[oo+2] = 150; px[oo+3] = 255;
      }
    }
  }

  ctx.putImageData(img, 0, 0);

  // ---- vector overlays: hill, castle, tree, clouds, floor, platform ----

  // hill on the left with the old laboratory-castle
  ctx.fillStyle = '#100a24';
  for (let x = 0; x < 150; x++) {
    const h = Math.round(30 * Math.exp(-((x - 70) ** 2) / 2800));
    ctx.fillRect(x, HORIZON - h, 1, h);
  }
  ctx.fillStyle = '#0e0922';
  ctx.fillRect(52, 96, 24, 30);              // keep
  ctx.fillRect(46, 88, 9, 38);               // left tower
  ctx.fillRect(74, 92, 9, 34);               // right tower
  ctx.fillRect(44, 84, 13, 4);               // tower caps
  ctx.fillRect(72, 88, 13, 4);
  for (let i = 0; i < 6; i++) ctx.fillRect(52 + i * 4, 93, 2, 3);  // crenellations
  ctx.fillStyle = 'rgb(255,190,90)';         // lit windows — someone is working late
  ctx.fillRect(60, 106, 2, 3);
  ctx.fillRect(67, 112, 2, 3);
  ctx.fillRect(49, 97, 2, 2);
  ctx.fillStyle = 'rgb(140,220,140)';        // ...on something green
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

  // cloud bands — one crossing the moon's lower third
  const cloud = (cx, cy, rx, ry, aBody, aEdge) => {
    ctx.fillStyle = `rgba(22,16,44,${aBody})`;
    ctx.beginPath(); ctx.ellipse(cx, cy, rx, ry, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = `rgba(170,160,215,${aEdge})`;
    ctx.beginPath(); ctx.ellipse(cx, cy - ry + 1, rx * 0.9, 1.5, 0, 0, Math.PI * 2); ctx.fill();
  };
  cloud(130, 66, 62, 6, 0.65, 0.10);
  cloud(250, 58, 48, 5, 0.85, 0.30);   // moonlit edge
  cloud(60, 40, 44, 5, 0.55, 0.08);

  // flagstone perspective lines on the floor
  ctx.strokeStyle = 'rgba(14,9,28,0.4)';
  ctx.lineWidth = 1;
  for (let k = -5; k <= 5; k++) {
    ctx.beginPath();
    ctx.moveTo(160 + k * 9, HORIZON);
    ctx.lineTo(160 + k * 78, NH);
    ctx.stroke();
  }
  for (const dy of [3, 7, 13, 21, 32, 45]) {
    ctx.beginPath(); ctx.moveTo(0, HORIZON + dy); ctx.lineTo(NW, HORIZON + dy); ctx.stroke();
  }
  // mist hugging the horizon
  const mist = ctx.createLinearGradient(0, HORIZON - 8, 0, HORIZON + 8);
  mist.addColorStop(0, 'rgba(96,86,130,0)');
  mist.addColorStop(0.5, 'rgba(96,86,130,0.28)');
  mist.addColorStop(1, 'rgba(96,86,130,0)');
  ctx.fillStyle = mist;
  ctx.fillRect(0, HORIZON - 8, NW, 16);

  // the specimen platform — a stone display dais
  ctx.fillStyle = '#221839';
  ctx.beginPath(); ctx.ellipse(CX, GY + 5, 90, 24, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#241a3a';
  ctx.beginPath(); ctx.ellipse(CX, GY + 3, 88, 24, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#463862';
  ctx.beginPath(); ctx.ellipse(CX, GY - 2, 86, 22, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#544472';
  ctx.beginPath(); ctx.ellipse(CX + 6, GY - 3, 64, 15, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#5e4c82';
  ctx.beginPath(); ctx.ellipse(CX + 8, GY - 4, 42, 9, 0, 0, Math.PI * 2); ctx.fill();
  ctx.strokeStyle = '#2c2148';
  ctx.beginPath(); ctx.ellipse(CX, GY - 2, 74, 18, 0, 0, Math.PI * 2); ctx.stroke();
  for (let a = 0; a < 12; a++) {
    const th = (a / 12) * Math.PI * 2;
    ctx.fillStyle = '#2c2148';
    ctx.fillRect(Math.round(CX + Math.cos(th) * 80) - 1, Math.round(GY - 2 + Math.sin(th) * 20) - 1, 2, 2);
  }

  return c;
}

// ── lighting ─────────────────────────────────────────────────────────────────
// Warm key light upper-left-front; cool moon rim-light upper-right-back.

const KEY = (() => { const l = Math.hypot(-0.45, 0.6, 0.66); return [-0.45/l, 0.6/l, 0.66/l]; })();
const RIM = (() => { const l = Math.hypot(0.62, 0.3, -0.72); return [0.62/l, 0.3/l, -0.72/l]; })();
const RIM_COL = [70, 95, 170];

// ── canvas renderer ──────────────────────────────────────────────────────────

let _raf = null;
let _canvas = null;
let _model = null;
let _theta = 0;
let _frame = 0;
const ROT_SPEED = 0.014;      // radians per drawn frame (30 fps → ~15 s/rev)
const FLASH_CYCLE = 560;      // frames between lightning strikes

export function initRenderer(canvas, genome) {
  destroyRenderer();
  _canvas = canvas;
  canvas.width  = NW;
  canvas.height = NH;
  if (!_bg) _bg = buildBackground();

  try {
    _model = prepareModel(genome);
  } catch {
    _model = { voxels: [], maxR: 1, maxY: 1 };
  }

  function frame() {
    _frame++;
    if (_frame % 2 === 0) {      // draw at 30 fps — cinematic, and kind to CPUs
      _theta += ROT_SPEED;
      _drawFrame();
    }
    _raf = requestAnimationFrame(frame);
  }
  _raf = requestAnimationFrame(frame);
}

export function updateGenome(genome) {
  try {
    _model = prepareModel(genome);
  } catch {
    _model = { voxels: [], maxR: 1, maxY: 1 };
  }
}

export function destroyRenderer() {
  if (_raf !== null) { cancelAnimationFrame(_raf); _raf = null; }
  _canvas = null;
  _model = null;
}

function _drawFrame() {
  if (!_canvas || !_model) return;
  const ctx = _canvas.getContext('2d');

  // lightning state — two quick flickers, then a long dark
  const fc = _frame % FLASH_CYCLE;
  const flash = fc < 2 ? 0.28 : (fc >= 5 && fc < 7) ? 0.16 : 0;

  ctx.drawImage(_bg, 0, 0);

  // contact shadow on the dais
  const shR = Math.min(80, _model.maxR * SC * 0.95 + 10);
  ctx.fillStyle = 'rgba(6,4,20,0.55)';
  ctx.beginPath(); ctx.ellipse(CX, GY, shR, shR * 0.26, 0, 0, Math.PI * 2); ctx.fill();

  const cos = Math.cos(_theta), sin = Math.sin(_theta);
  const pulse = 0.55 + 0.25 * Math.sin(_frame * 0.09);

  // rotate, then painter's sort back-to-front
  const rot = _model.voxels.map(v => ({
    v,
    rx: v.x * cos - v.z * sin,
    rz: v.x * sin + v.z * cos,
    nrx: v.nx * cos - v.nz * sin,
    nrz: v.nx * sin + v.nz * cos,
  }));
  rot.sort((a, b) => a.rz - b.rz);

  const glows = [];

  for (const { v, rx, rz, nrx, nrz } of rot) {
    const sx = Math.round(CX + rx * SC - SC / 2);
    const sy = Math.round(GY - (v.y + 1) * SC + rz * SC * 0.20);
    if (sx < -SC || sx >= NW || sy < -SC || sy >= NH) continue;

    // diffuse key + ambient (+lightning), depth cue — the rest is baked in
    const diff = Math.max(0, nrx * KEY[0] + v.ny * KEY[1] + nrz * KEY[2]);
    let b = 0.34 + flash + 0.55 * diff - 0.05 * (rz / 20);
    if (v.glow) b = Math.max(b, 0.92);
    b = Math.min(1, Math.max(0, b));

    // cool rim-light from the moon side
    const rimDot = Math.max(0, nrx * RIM[0] + v.ny * RIM[1] + nrz * RIM[2]);
    const rim = rimDot * rimDot * (0.45 + flash);

    const idx = Math.min(11, Math.round(b * 10));
    const c0 = v.ramp[idx];
    ctx.fillStyle = `rgb(${Math.min(255, c0[0] + rim * RIM_COL[0]) | 0},${Math.min(255, c0[1] + rim * RIM_COL[1]) | 0},${Math.min(255, c0[2] + rim * RIM_COL[2]) | 0})`;
    ctx.fillRect(sx, sy, SC, SC);

    // lit top faces only — at 3px, per-voxel edge lines read as corduroy
    if (v.topExposed && v.ny > 0.25) {
      const ct = v.ramp[Math.min(idx + 2 + (flash > 0 ? 1 : 0), 12)];
      ctx.fillStyle = `rgb(${ct[0]},${ct[1]},${ct[2]})`;
      ctx.fillRect(sx, sy, SC, 1);
    }

    if (v.glowRep) glows.push({ sx, sy, col: v.glowCol });
  }

  // halo pass for glowing materials
  if (glows.length) {
    ctx.globalCompositeOperation = 'lighter';
    for (const { sx, sy, col } of glows) {
      const gx = sx + SC, gy = sy + SC, gr = SC * 4.5;   // halo spans the coarse cell
      const grad = ctx.createRadialGradient(gx, gy, 0, gx, gy, gr);
      grad.addColorStop(0, `rgba(${col[0]},${col[1]},${col[2]},${(0.5 * pulse).toFixed(3)})`);
      grad.addColorStop(1, 'rgba(0,0,0,0)');
      ctx.fillStyle = grad;
      ctx.fillRect(gx - gr, gy - gr, gr * 2, gr * 2);
    }
    ctx.globalCompositeOperation = 'source-over';
  }

  // lightning washes the whole scene
  if (flash > 0) {
    ctx.fillStyle = `rgba(225,230,255,${flash})`;
    ctx.fillRect(0, 0, NW, NH);
  }
}
