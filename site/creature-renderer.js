/**
 * Pixelated voxel portrait renderer for The Lab.
 * No deps. Pure Canvas 2D.
 *
 * Native canvas: 80 × 96 px, displayed 3× by CSS (image-rendering: pixelated).
 * Each voxel = 3 × 3 native pixels. Creature spins on Y axis.
 *
 * Voxel coordinate system: Y=up, X=right, Z=toward viewer.
 * Origin = bottom-centre of creature (feet at Y=0).
 */

// ── canvas geometry ─────────────────────────────────────────────────────────
const NW = 80, NH = 96;
const CX = 40, CY = 68;   // projection centre (pushed down so head has room)
const SC = 3;              // native pixels per voxel unit

// ── colours [r,g,b] ─────────────────────────────────────────────────────────
const FLESH   = [195, 118,  78];
const FLDK    = [132,  78,  48];
const PALLOR  = [192, 172, 152];   // sickly low-vigor flesh
const BONE    = [212, 200, 170];
const BONDK   = [158, 148, 118];
const METAL   = [ 96, 110, 124];
const METDK   = [ 60,  72,  84];
const GLOW    = [255, 140,  20];
const CHITIN  = [ 40,  78,  52];
const CHTLT   = [ 65, 115,  78];
const CHTDK   = [ 24,  50,  34];
const EYEWH   = [230, 230, 210];
const PUPIL   = [ 12,   8,  18];
const GPUPIL  = [ 20, 210,  80];   // biotech glowing pupil
const HOOF    = [ 48,  40,  30];
const CLAW    = [170, 158, 128];
const BOLT    = [ 80,  92, 106];
const BLTGLO  = [255, 200,  40];
const ICHOR   = [135,  75, 215];
const STITCHD = [ 52,  30,  22];   // suture dark line

// colour helpers
const lp = (a, b, t) => a.map((v, i) => Math.round(v + (b[i] - v) * t));
const sh = (c, f)    => c.map(v => Math.min(255, Math.max(0, Math.round(v * f))));
const cs = ([r,g,b]) => `rgb(${r},${g},${b})`;

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

function shapeAntenna(params) {
  const [len, girth] = params;
  const stalkH = 3 + Math.round(len * 4);
  const v = [];
  for (let j = 0; j < stalkH; j++)
    v.push([0, j, 0, ...BONE]);
  v.push(...sph(0, stalkH, 0, girth > 0.3 ? 1 : 0, BONDK));
  return v;
}

function shapeHorn(params, fc) {
  const [, girth, , curl] = params;
  const v = [];
  const w = 1 + Math.round(girth * 1.5);
  v.push(...box(0, 0, 0, w, 1, w, fc));
  v.push(...box(0, 1, 0, Math.max(1, w-1), 2, Math.max(1, w-1), sh(fc, 0.85)));
  v.push(...box(0, 3, 0, 1, 2, 1, sh(fc, 0.7)));
  if (curl > 0.4) v.push(...vox(-1, 4, 0, sh(fc, 0.7)));
  return v;
}

function shapeSensorMast(params) {
  const [len] = params;
  const h = 4 + Math.round(len * 3);
  const v = [];
  for (let j = 0; j < h; j++) v.push([0, j, 0, ...METAL]);
  v.push([0, h, 0, ...GLOW]);
  v.push([-1, h, 0, ...METDK]);
  v.push([1, h, 0, ...METDK]);
  return v;
}

function shapeSensorStub() {
  return box(0, 0, 0, 1, 1, 1, PALLOR);
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

// bolt decoration for heart tier on neck
function boltRow(y, count, glowing) {
  const col = glowing ? BLTGLO : BOLT;
  const positions = [
    [[-2, y, -1], [2, y, -1]],
    [[-2, y, -1], [2, y, -1], [-2, y, 1], [2, y, 1]],
    [[-2, y, -1], [2, y, -1], [-2, y, 0], [2, y, 0], [-2, y, 1], [2, y, 1]],
  ];
  const n = Math.min(count, 3);
  if (n < 1) return [];
  return positions[n-1].map(([x,y,z]) => [x, y, z, ...col]);
}

// ── body plan assembly ───────────────────────────────────────────────────────

function buildTetrapod(g, fleshCol) {
  // bulk gene (params[1]) affects torso width
  const bulk = g.body.params[1] ?? 0.5;
  const tw = 3 + Math.round(bulk * 2);   // torso width 3–5
  const th = 4 + Math.round(bulk * 1);   // torso height 4–5
  const tx = -Math.floor(tw / 2);

  // brain tier affects head size
  const headScale = { dim: 1, average: 1, gifted: 1, mastermind: 2 }[g.brain.tier] ?? 1;
  const hw = 3 + headScale;
  const hh = 3 + headScale;
  const hx = -Math.floor(hw / 2);

  const neckY = th + 1;
  const headY = neckY + 1;
  const topY  = headY + hh;

  // socket world positions (left-side; right gets mirrorX)
  const sockets = {
    hand:   [Math.ceil(tw / 2), th - 2, 0],          // arm: just outside torso edge
    leg:    [Math.floor(tw / 4) - 1, 0, 0],           // leg: under torso (mirrored = other foot)
    sensor: [0, topY, 0],                             // top of head
    eye:    [hx + Math.floor(hw/2) - 1, headY + Math.floor(hh/2) - 1, Math.floor(hw/2) + 1],
  };

  const v = [];

  // torso
  v.push(...box(tx, 1, -1, tw, th, 3, fleshCol));
  // neck
  v.push(...box(-1, neckY, -1, 2, 1, 2, fleshCol));
  // head
  v.push(...box(hx, headY, -Math.floor(hw/2), hw, hh, hw, fleshCol));
  // cranial bump for gifted/mastermind
  if (g.brain.tier === 'mastermind')
    v.push(...box(hx+1, headY + hh, hx+1, hw-2, 1, hw-2, sh(fleshCol, 1.05)));

  // neck bolts from heart tier
  const heartLevel = ['faint','steady','strong','titan'].indexOf(g.heart.tier);
  if (heartLevel >= 1) {
    const glowing = heartLevel >= 3;
    v.push(...boltRow(neckY, heartLevel, glowing));
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

  const top = r * 2 + 2;
  const sockets = {
    hand:   [r, r + 1, 0],
    leg:    [r * 0.4, 0, 0],
    sensor: [0, top + 1, 0],
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

  // head
  const headX = Math.round(Math.sin(1.5 * Math.PI * 0.9) * 3);
  const headY  = 1 + segs * 1.2;
  v.push(...box(headX - 2, headY, -1, 4, 3, 3, fleshCol));

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
  const bulk = g.body.params[1] ?? 0.5;
  const wingspan = 4 + Math.round(limb * 5);
  const v = [];

  // small body
  v.push(...box(-2, 4, -1, 4, 5, 3, fleshCol));
  // neck + head
  v.push(...box(-1, 9, -1, 2, 1, 2, fleshCol));
  v.push(...box(-2, 10, -2, 4, 4, 4, fleshCol));

  // wings (membrane as thin flat voxels)
  const wingCol = sh(fleshCol, 0.6);
  for (let wx = 0; wx < wingspan; wx++) {
    const wy = 6 + Math.round(Math.sin(wx * 0.4) * 2);
    v.push([-(wx + 3), wy, -1, ...wingCol]);
    v.push([ wx + 2,   wy, -1, ...wingCol]);
    if (wx < wingspan - 2) {
      v.push([-(wx + 3), wy - 1, 0, ...sh(wingCol, 0.8)]);
      v.push([ wx + 2,   wy - 1, 0, ...sh(wingCol, 0.8)]);
    }
  }

  const sockets = {
    hand:   [3, 5, 0],
    leg:    [1, 0, 0],
    sensor: [0, 14, 0],
    eye:    [0, 11, 2],
  };
  return { voxels: v, sockets };
}

// ── full creature assembly ───────────────────────────────────────────────────

export function assembleCreature(genome) {
  // flesh color: lerp from healthy to pallor based on heart vigor
  const vigor = genome.heart?.params?.[0] ?? 0.5;
  const fleshCol = lp(PALLOR, FLESH, vigor);

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

    // mirrored copy for bilateral parts (hands and legs)
    if (slotName === 'hand' || slotName === 'leg') {
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
    case 'antenna':     return shapeAntenna(params);
    case 'horn':        return shapeHorn(params, partCol);
    case 'sensor_mast': return shapeSensorMast(params);
    case 'sensor_stub': return shapeSensorStub();
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

// ── Y-axis rotation + projection ─────────────────────────────────────────────

function rotateY(vox, theta) {
  const cos = Math.cos(theta), sin = Math.sin(theta);
  return vox.map(([x, y, z, r, g, b]) => [
    x * cos - z * sin,
    y,
    x * sin + z * cos,
    r, g, b,
  ]);
}

function project([rx, ry, rz]) {
  // slight dimetric tilt: Z contributes a little to screen Y for depth feel
  return {
    sx: Math.round(CX + rx * SC),
    sy: Math.round(CY - ry * SC + rz * SC * 0.18),
  };
}

function brightness(vy, maxY, rz) {
  // top-lighting + slight depth cue
  const topLit = 0.72 + 0.28 * (vy / Math.max(1, maxY));
  const depth  = 1.0 - 0.12 * (rz / 8);   // farther = slightly darker
  return Math.min(1.4, Math.max(0.5, topLit * depth));
}

// ── canvas renderer ──────────────────────────────────────────────────────────

let _raf = null;
let _canvas = null;
let _voxels = null;
let _theta  = 0;
const ROT_SPEED = 0.012; // radians per frame

export function initRenderer(canvas, genome) {
  destroyRenderer();
  _canvas = canvas;
  canvas.width  = NW;
  canvas.height = NH;

  try {
    _voxels = assembleCreature(genome);
  } catch {
    _voxels = [];
  }

  function frame() {
    _theta += ROT_SPEED;
    _drawFrame();
    _raf = requestAnimationFrame(frame);
  }
  _raf = requestAnimationFrame(frame);
}

export function updateGenome(genome) {
  try {
    _voxels = assembleCreature(genome);
  } catch {
    _voxels = [];
  }
}

export function destroyRenderer() {
  if (_raf !== null) { cancelAnimationFrame(_raf); _raf = null; }
  _canvas = null;
  _voxels = null;
}

function _drawFrame() {
  if (!_canvas || !_voxels) return;
  const ctx = _canvas.getContext('2d');
  ctx.clearRect(0, 0, NW, NH);

  // scanline BG — dark green-black gradient
  const grad = ctx.createLinearGradient(0, 0, 0, NH);
  grad.addColorStop(0, '#0a1410');
  grad.addColorStop(1, '#060d0b');
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, NW, NH);

  // rotate + sort back-to-front by Z
  const rotated = rotateY(_voxels, _theta);
  const maxY = Math.max(..._voxels.map(v => v[1]), 1);

  rotated.sort((a, b) => a[2] - b[2]);

  for (const rv of rotated) {
    const [rx, ry, rz, r, g, b] = rv;
    const { sx, sy } = project(rv);
    if (sx < 0 || sx >= NW - SC || sy < 0 || sy >= NH - SC) continue;

    const bright = brightness(ry, maxY, rz);
    const cr = Math.min(255, Math.round(r * bright));
    const cg = Math.min(255, Math.round(g * bright));
    const cb = Math.min(255, Math.round(b * bright));

    ctx.fillStyle = `rgb(${cr},${cg},${cb})`;
    ctx.fillRect(sx, sy, SC, SC);

    // 1-px highlight on top-left for cube feel
    if (bright > 0.85) {
      ctx.fillStyle = `rgba(255,255,255,0.18)`;
      ctx.fillRect(sx, sy, SC, 1);
      ctx.fillRect(sx, sy, 1, SC);
    }
    // 1-px shadow on bottom-right
    ctx.fillStyle = `rgba(0,0,0,0.22)`;
    ctx.fillRect(sx, sy + SC - 1, SC, 1);
    ctx.fillRect(sx + SC - 1, sy, 1, SC);
  }
}
