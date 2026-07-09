/**
 * The Lab — wired to the live Mutator service at MUTATOR_URL.
 *
 * All genetics (spawn/mutate/splice/surgery) run server-side: genomes are
 * stored permanently there. Names, dead-creature tracking, and the notebook
 * are kept in localStorage for this browser only.
 *
 * Genome-core is still loaded locally for display math (viability, upkeep,
 * stat bars) and the portrait renderer.
 */

import {
  SLOT_NAMES, BODY_AXES, BRAIN_AXES,
  viability, upkeep, heartCapacity, circulatoryLoad, partUpkeep,
  originOf, isVestigial, homologOf, brainSize, bodyAxis, brainAxis, heartVigor,
  capacity as controlCapacity, controlCost, controlRadius, berserkThreshold,
} from "./lib/index.js";
import { initRenderer, updateGenome, destroyRenderer, locomotionProfile, setLabFaction, renderThumbnail, renderPartThumbnail, initBenchTurntable } from "./creature-renderer.js";

const MUTATOR_URL = "https://maddr-mutator.onrender.com";
const LOCAL_KEY   = "maddr-lab-v2";

// ── local-only state (localStorage) ──────────────────────────────────────────
// accountId     : stable UUID → identifies this browser to the server
// nameMap       : { [genomeId]: string }  — we assign human names, server assigns IDs
// deadSet       : string[]  — genomeIds that died on the table
// trayFrom      : { [itemId]: string }  — creature name a tray item came from
// log           : notebook entries
// seq           : auto-name counter
// selectedId    : currently selected genome ID
//
// A "specimen" is the physical animal; a "genome id" is one immutable
// snapshot of it (docs/07 lineage — every mutate/splice/harvest/sew mints
// a new row). These track the animal across that churn:
// specimenOf    : { [genomeId]: specimenId }  — which animal a row belongs to
// currentGenomeOf: { [specimenId]: genomeId }  — that animal's latest row
// locationOf    : { [specimenId]: "lab" | "chop" }  — which room it's in
// originItem    : { [specimenId]: { [slot|"heart"]: itemId } }  — the most
//                 recently harvested item for each of ITS OWN missing
//                 parts, so "Restore" knows what to graft back and can
//                 tell if that exact piece has since been used elsewhere
//
// The freezer is a hierarchy, not a flat list: items land in ONE of four
// labeled drawers (derived from the part's own homolog, never stored),
// and within a drawer, a SIMPLE thumbnail per (drawer, source specimen)
// pair -- "the head from Specimen-01" is one tile no matter how many
// separate cuts it took to remove it all, or how many genomes ago that
// was. Clicking it opens every piece that's ever landed in that group.
// originSpecimen: { [itemId]: specimenId }  — which animal a part came from
// chopMode      : "slab" | "tray"  — which the Chop Shop's center panel shows
// trayBench     : itemId[] — the "mini-slab": tray items currently pulled
//                 out onto the surgical tray, worked on as their own
//                 little assembly (add more, harvest pieces back off,
//                 graft the lot onto whatever's on the real slab). Purely
//                 a local staging list -- an item sitting here is still
//                 the same freezer item, unmoved and ungrouped, until it
//                 actually gets grafted (consumed) or just stays put.

let local = (() => {
  try { return JSON.parse(localStorage.getItem(LOCAL_KEY)); } catch { return null; }
})() ?? newLocal();

function newLocal() {
  return {
    accountId: crypto.randomUUID(), nameMap: {}, deadSet: [], trayFrom: {}, log: [], seq: 0,
    selectedId: null, faction: "maddr", stable: [], hidden: [], factionOf: {}, view: "lab",
    specimenOf: {}, currentGenomeOf: {}, locationOf: {}, originItem: {},
    originSpecimen: {}, chopMode: "slab", trayBench: [],
  };
}
function saveLocal() { localStorage.setItem(LOCAL_KEY, JSON.stringify(local)); }

// ── server state (in-memory, refreshed via sync()) ───────────────────────────
let creatures = [];   // [{ id, name, alive, genome }]
let tray      = [];   // [{ itemId, item, from }]
let blood     = 500;

// ── helpers ───────────────────────────────────────────────────────────────────
function nextName()     { local.seq += 1; return `Specimen-${String(local.seq).padStart(2, "0")}`; }
function byId(id)       { return creatures.find(c => c.id === id); }
function selected()     { return local.selectedId ? byId(local.selectedId) : null; }
function logEntry(msg)  { const t = new Date().toLocaleTimeString(); local.log.unshift({ t, msg }); local.log = local.log.slice(0, 100); }
function registerName(id, name)  { local.nameMap[id] = name; if (!local.factionOf[id]) local.factionOf[id] = local.faction; }
function registerFrom(itemId, name) { local.trayFrom[itemId] = name; }
function markDead(id)   { if (!local.deadSet.includes(id)) local.deadSet.push(id); }
function nameOf(id)     { return local.nameMap[id] ?? id.slice(-8); }
function vword(v)       { return `load ${v.load.toFixed(1)} / cap ${v.capacity.toFixed(1)}`; }
function pct(x)         { return `${Math.round(x * 100)}%`; }
function esc(s)         { return String(s).replace(/[&<>"]/g, ch => ({ "&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;" }[ch])); }
function bar(x)         { return `<span class="bar"><i style="width:${Math.round(x*100)}%"></i></span>${pct(x)}`; }
function ikey()         { return crypto.randomUUID(); }

// ── specimen identity (survives surgery's ever-changing genome ids) ─────────
function specimenIdOf(genomeId) { return local.specimenOf?.[genomeId] ?? genomeId; }
function setSpecimenGenome(sid, genomeId) {
  local.specimenOf[genomeId] = sid;
  local.currentGenomeOf[sid] = genomeId;
}

// ── freezer part display (icons, grouping, gene-axis stats) ─────────────────
// Which of the freezer's four labeled drawers an item belongs in --
// derived from its own homolog, never stored, so it can't drift out of
// sync with the part catalog.
function drawerKeyForItem(item) {
  if (item.kind === "heart") return "torso";
  const h = homologOf(item.family);
  if (h === "sensor" || h === "eye") return "head";
  if (h === "leg") return "lower";
  return "arms";
}
// Group key: drawer + which specimen it came from. Legacy items harvested
// before this existed have no recorded origin and just fall back to being
// their own one-item group.
function groupKeyOf(t) { return `${drawerKeyForItem(t.item)}|${local.originSpecimen?.[t.itemId] ?? t.itemId}`; }
function groupItems(groupKey) { return tray.filter(t => groupKeyOf(t) === groupKey); }
const PART_ICON = { hand: "✋", sensor: "📡", eye: "👁️", leg: "🦵" };
function partIcon(item) { return item.kind === "heart" ? "🫀" : (PART_ICON[homologOf(item.family)] ?? "🦴"); }
function partSlot(item) { return item.kind === "heart" ? "heart" : homologOf(item.family); }
// The tray-as-mini-slab: whatever's currently pulled onto it, resolved
// against the live tray (so a bench itemId that got consumed elsewhere,
// or a stale one from an old session shape, just quietly drops out).
function benchItems() { return tray.filter(t => (local.trayBench ?? []).includes(t.itemId)); }
// One item per slot for the preview mannequin -- last one added wins if
// the bench somehow holds two for the same slot (e.g. two eyes).
function benchParts() {
  const parts = {};
  for (const t of benchItems()) parts[partSlot(t.item)] = t.item;
  return parts;
}
function partName(item) { return item.kind === "heart" ? `${item.tier} heart` : item.family.replace(/_/g, " "); }
// Real renders of the harvested piece itself -- the freezer used to show
// a category emoji (a brain for "head", say) no matter what was actually
// inside. One cached still per item, worn by a neutral mannequin body
// (renderPartThumbnail) and zoomed to wherever that piece sockets in.
const partThumbCache = {};
function partThumbHtml(t) {
  if (!(t.itemId in partThumbCache)) {
    try { partThumbCache[t.itemId] = renderPartThumbnail(t.item, partSlot(t.item), local.faction); }
    catch { partThumbCache[t.itemId] = ""; }
  }
  const url = partThumbCache[t.itemId];
  return url ? `<img src="${url}" alt="${esc(partName(t.item))}">` : partIcon(t.item);
}
// The tray shows the whole assembled bench as ONE live turntable -- a
// real WebGL context + its own rAF loop (initBenchTurntable), so it must
// be stopped before the tray's DOM is rebuilt out from under it, or the
// loop (and GL context) leaks.
let activeTurntables = [];
function stopTurntables() { activeTurntables.forEach(h => h.stop()); activeTurntables = []; }
const PART_AXES = ["length", "girth", "taper", "curl", "count", "ornament"];
function partStatsHtml(item) {
  if (item.kind === "heart") {
    return `<div class="k">tier</div><div>${esc(item.tier)}</div><div class="k">vigor</div><div>${bar(heartVigor(item))}</div>`;
  }
  return item.params.map((p, i) => `<div class="k">${PART_AXES[i]}</div><div>${bar(p)}</div>`).join("");
}
// The exact signature harvestHeart() leaves behind (surgery.ts): a real
// "faint" tier is a normal, weak-but-working heart -- only an all-zero
// param vector marks the cavity as emptied.
function heartHarvested(heart) { return heart.tier === "faint" && heart.params.every(p => p === 0); }
function isWhole(g) {
  return SLOT_NAMES.every(s => !isVestigial(g.slots[s].family)) && !heartHarvested(g.heart);
}

// ── API ───────────────────────────────────────────────────────────────────────
async function api(method, path, body) {
  const r = await fetch(`${MUTATOR_URL}${path}`, {
    method,
    headers: { "content-type": "application/json", "x-account-id": local.accountId },
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });
  const json = await r.json().catch(() => ({}));
  if (!r.ok) throw new Error(json.message ?? `HTTP ${r.status} on ${path}`);
  return json;
}

// ── sync from server ──────────────────────────────────────────────────────────
async function sync() {
  const [crRes, walletRes, trayRes] = await Promise.all([
    api("GET", "/creatures?limit=200"),
    api("GET", "/wallet"),
    api("GET", "/tray"),
  ]);

  blood = walletRes.blood ?? 0;

  const deadSet = new Set(local.deadSet ?? []);
  creatures = (crRes.items ?? [])
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .map(sg => ({
      id: sg.id,
      name: nameOf(sg.id),
      alive: !deadSet.has(sg.id),
      genome: sg.genome,
    }));

  tray = (trayRes.items ?? []).map(inv => ({
    itemId: inv.itemId,
    item: inv.item,
    from: local.trayFrom[inv.itemId] ?? "tray",
  }));

  // selection must be a member of the current screen's bench; default to
  // the first one so the data screen is never blank when creatures exist
  ensureSelection();

  if (local.view === "stable") renderStable();
  else if (local.view === "chop") renderChop();
  else render();
}

// ── operations ────────────────────────────────────────────────────────────────

// Each faction has its OWN roster of body forms — creatures are not
// transferable between races. Mad Doctors field the full monster mix;
// the Human Army only builds bipedal/flying machines (robots don't
// slither or ooze — the tank comes from tech legs); the Alien Hive
// skews crawling and amorphous with few flyers.
const FACTION_PLANS = {
  // Mad Doctors: the full monster mix, classic archetypes at even odds
  maddr: [
    ["tetrapod", 0.30], ["winged", 0.12], ["serpentine", 0.10], ["blob", 0.08],
    ["crab", 0.12], ["arachnid", 0.10], ["avian", 0.10], ["treant", 0.05], ["floater", 0.03],
  ],
  // Human Army: only chassis that make sense as a machine -- runners,
  // walkers, hover units. No slither/ooze/plant (still no treant), same
  // restriction that already ruled out blob/serpentine.
  human: [
    ["tetrapod", 0.40], ["winged", 0.18], ["avian", 0.16],
    ["crab", 0.14], ["arachnid", 0.08], ["floater", 0.04],
  ],
  // Alien Hive: skews crawling, amorphous, and grown -- arachnid and
  // treant read strongest for a hive that grows its weapons.
  alien: [
    ["tetrapod", 0.18], ["serpentine", 0.18], ["blob", 0.14], ["winged", 0.08],
    ["arachnid", 0.18], ["treant", 0.14], ["floater", 0.08], ["crab", 0.02],
  ],
};
function pickPlan() {
  const table = FACTION_PLANS[local.faction] ?? FACTION_PLANS.maddr;
  let r = crypto.getRandomValues(new Uint32Array(1))[0] / 2 ** 32;
  for (const [p, w] of table) { r -= w; if (r < 0) return p; }
  return table[0][0];
}

// Faction spawn pools (docs/17): the Mad Doctors breed pure flesh and
// graft tech later; the Army issues hardware from birth; the Hive grows
// its weapons. The service keeps organic in every pool.
const FACTION_ORIGINS = {
  maddr: ["organic"],
  human: ["organic", "tech"],
  alien: ["organic", "biotech"],
};

async function doSpawn() {
  showBusy(true);
  try {
    const rec = await api("POST", "/spawn", {
      idempotencyKey: ikey(),
      plan: pickPlan(),
      origins: FACTION_ORIGINS[local.faction] ?? ["organic"],
    });
    if (rec.status === "failed_experiment") {
      logEntry("⚡ The tissue rejected animation. (failed experiment)");
    } else {
      const name = nextName();
      registerName(rec.genomeId, name);
      const sid = crypto.randomUUID();
      setSpecimenGenome(sid, rec.genomeId);
      local.locationOf[sid] = "lab";
      local.selectedId = rec.genomeId;
      logEntry(`⚡ ${name} crawls off the slab.`);
    }
  } catch (e) { logEntry(`⚠️ Spawn failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

async function doMutate(biasSlot) {
  const c = selected(); if (!c?.alive) return;
  showBusy(true);
  try {
    const rec = await api("POST", "/mutate", { idempotencyKey: ikey(), parentId: c.id, options: biasSlot ? { biasSlot } : {} });
    if (rec.status === "failed_experiment") {
      logEntry(`🧬 Mutation of ${c.name} produced a failed experiment.`);
    } else {
      const name = nextName();
      registerName(rec.genomeId, name);
      const sid = crypto.randomUUID();
      setSpecimenGenome(sid, rec.genomeId);
      local.locationOf[sid] = "lab";
      local.selectedId = rec.genomeId;
      logEntry(`🧬 Mutated ${c.name} → ${name}${biasSlot ? ` (fed the ${biasSlot})` : ""}.`);
    }
  } catch (e) { logEntry(`⚠️ Mutate failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

async function doSplice(partnerId) {
  const a = selected(); const b = byId(partnerId);
  if (!a?.alive || !b?.alive) return;
  showBusy(true);
  try {
    const rec = await api("POST", "/splice", { idempotencyKey: ikey(), parentAId: a.id, parentBId: b.id });
    if (rec.status === "failed_experiment") {
      logEntry(`🧪 Splice of ${a.name} × ${b.name} produced a failed experiment.`);
    } else {
      const name = nextName();
      registerName(rec.genomeId, name);
      const sid = crypto.randomUUID();
      setSpecimenGenome(sid, rec.genomeId);
      local.locationOf[sid] = "lab";
      local.selectedId = rec.genomeId;
      logEntry(`🧪 Spliced ${a.name} × ${b.name} → ${name}.`);
    }
  } catch (e) { logEntry(`⚠️ Splice failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

async function doHarvestHeart() {
  const c = selected(); if (!c) return;
  const sid = specimenIdOf(c.id);
  showBusy(true);
  try {
    const rec = await api("POST", "/harvest/heart", { idempotencyKey: ikey(), creatureId: c.id });
    // rec: { genomeId (corpse/survivor), genome, heart, itemId }
    registerName(rec.genomeId, c.name);
    registerFrom(rec.itemId, c.name);
    setSpecimenGenome(sid, rec.genomeId);
    local.originItem[sid] = local.originItem[sid] ?? {};
    local.originItem[sid].heart = rec.itemId;
    local.originSpecimen[rec.itemId] = sid;
    local.locationOf[sid] = "chop";
    local.selectedId = rec.genomeId;
    const v = viability(rec.genome);
    if (v.state === "nonviable") {
      markDead(rec.genomeId);
      logEntry(`💔 Took the ${rec.heart.tier} heart out of ${c.name}. The body cannot run without it — ${c.name} dies on the table.`);
    } else {
      logEntry(`💔 Took the ${rec.heart.tier} heart out of ${c.name}. A faint vestige barely keeps the small body going (${v.state}).`);
    }
  } catch (e) { logEntry(`⚠️ Harvest failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

// Chop Shop region cuts: several slots taken in one motion (e.g. "the
// whole head" = sensor + eye). Each slot is still its own /harvest/part
// call under the hood -- the genome's slot granularity hasn't changed,
// this just walks the chain of stumped genomes the calls produce and
// reports it as one cut. Slots already a stump are skipped quietly.
async function doHarvestRegion(slots, label) {
  const c = selected(); if (!c) return;
  const sid = specimenIdOf(c.id);
  showBusy(true);
  try {
    let curId = c.id, curGenome = c.genome;
    const cut = [];
    for (const slot of slots) {
      if (isVestigial(curGenome.slots[slot].family)) continue;
      const rec = await api("POST", "/harvest/part", { idempotencyKey: ikey(), creatureId: curId, slot });
      registerName(rec.genomeId, c.name);
      registerFrom(rec.itemId, c.name);
      setSpecimenGenome(sid, rec.genomeId);
      local.originItem[sid] = local.originItem[sid] ?? {};
      local.originItem[sid][slot] = rec.itemId;
      local.originSpecimen[rec.itemId] = sid;
      curId = rec.genomeId; curGenome = rec.genome;
      cut.push(rec.part.family.replace(/_/g, " "));
    }
    local.locationOf[sid] = "chop";
    local.selectedId = curId;
    if (cut.length) logEntry(`🔪 Took the ${label} (${cut.join(", ")}) off ${c.name}. Bagged and into the freezer.`);
    else logEntry(`✂️ ${c.name}'s ${label} is already bare — nothing left to take.`);
  } catch (e) { logEntry(`⚠️ Harvest failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

// Sewing works on dead specimens too -- a heart transplant is exactly how
// a corpse (per local.deadSet) comes back: the new genome id it produces
// was never added to deadSet, so `alive` just becomes true again on the
// next sync. No revival flag needed -- it falls out of the immutable-row
// design for free.
async function doSew(itemId) {
  const c = selected(); if (!c) return;
  const entry = tray.find(t => t.itemId === itemId); if (!entry) return;
  const sid = specimenIdOf(c.id);
  showBusy(true);
  try {
    let rec;
    if (entry.item.kind === "heart") {
      rec = await api("POST", "/sew/heart", { idempotencyKey: ikey(), creatureId: c.id, itemId });
    } else {
      const slot = homologOf(entry.item.family);
      rec = await api("POST", "/sew/part", { idempotencyKey: ikey(), creatureId: c.id, slot, itemId });
    }

    if (rec.result === "survived") {
      registerName(rec.genomeId, c.name);
      setSpecimenGenome(sid, rec.genomeId);
      local.selectedId = rec.genomeId;
      local.trayBench = (local.trayBench ?? []).filter(id => id !== itemId);   // consumed -- off the tray too
      if (entry.item.kind === "heart") {
        if (rec.explantedHeartItemId) registerFrom(rec.explantedHeartItemId, c.name);
        logEntry(`🫀 Transplanted a ${entry.item.tier} heart into ${c.name} (${vword(rec.viability)}). Old heart back in tray.`);
      } else {
        if (rec.explantedPartItemId) {
          registerFrom(rec.explantedPartItemId, c.name);
          logEntry(`🪡 Sewed the ${entry.item.family.replace(/_/g, " ")} onto ${c.name}'s ${homologOf(entry.item.family)}, swapping out what was there (${vword(rec.viability)}). Old part back in the freezer.`);
        } else {
          logEntry(`🪡 Sewed the ${entry.item.family.replace(/_/g, " ")} onto ${c.name}'s ${homologOf(entry.item.family)} (${vword(rec.viability)}).`);
        }
      }
    } else if (rec.result === "limb_rejected") {
      logEntry(`🦠 ${c.name}'s heart can't feed the new ${(entry.item.family ?? "part").replace(/_/g, " ")} — rejected. The part is still usable.`);
    } else if (rec.result === "patient_died") {
      if (rec.genomeId) { registerName(rec.genomeId, c.name); setSpecimenGenome(sid, rec.genomeId); markDead(rec.genomeId); local.selectedId = rec.genomeId; }
      if (entry.item.kind === "heart") {
        logEntry(`☠️ The ${entry.item.tier} heart could not drive ${c.name}'s body — ${c.name} dies on the table.`);
      } else {
        logEntry(`☠️ The graft overloaded ${c.name}'s heart far past shock — ${c.name} dies on the table.`);
      }
    }
  } catch (e) { logEntry(`⚠️ Sew failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

// ── moving specimens between the Lab and the Chop Shop ──────────────────────
// A specimen lives in exactly one room. Sending it under the knife pulls
// it off the Lab's bench entirely -- there is only ever one card for it,
// wherever it currently is -- and it only comes back once it's whole
// again: "only a whole functioning specimen can exist in the lab."
function doSendToChop() {
  const c = selected(); if (!c) return;
  const sid = specimenIdOf(c.id);
  local.locationOf[sid] = "chop";
  saveLocal();
  logEntry(`🔪 Wheeled ${c.name} into the chop shop.`);
  setView("chop");
}

function doSendToLab() {
  const c = selected(); if (!c) return;
  if (!c.alive || !isWhole(c.genome)) return;
  const sid = specimenIdOf(c.id);
  local.locationOf[sid] = "lab";
  saveLocal();
  logEntry(`🏠 ${c.name} is whole again — back to the lab.`);
  setView("lab");
}

// What a specimen is missing, and whether ITS OWN original parts (not
// some other creature's) are still sitting unclaimed in the freezer. If
// even one has already been grafted onto something else, a full restore
// is impossible -- the button greys out rather than doing a partial job
// silently.
function restoreCheck(sid, g) {
  const missing = SLOT_NAMES.filter(s => isVestigial(g.slots[s].family));
  if (heartHarvested(g.heart)) missing.push("heart");
  if (missing.length === 0) return { ok: false, missing, reason: "whole" };
  const origins = local.originItem[sid] ?? {};
  for (const m of missing) {
    const itemId = origins[m];
    if (!itemId || !tray.find(t => t.itemId === itemId)) return { ok: false, missing, reason: "unavailable" };
  }
  return { ok: true, missing };
}

async function doRestore() {
  const c = selected(); if (!c) return;
  const sid = specimenIdOf(c.id);
  const check = restoreCheck(sid, c.genome);
  if (!check.ok) return;
  showBusy(true);
  try {
    const origins = local.originItem[sid];
    // heart first: restoring pumping capacity before adding load back is
    // the one ordering that can never itself provoke a rejection that a
    // different order might
    const order = check.missing.includes("heart")
      ? ["heart", ...check.missing.filter(m => m !== "heart")]
      : [...check.missing];
    let curId = c.id;
    const restored = [];
    for (const m of order) {
      const itemId = origins[m];
      if (m === "heart") {
        const rec = await api("POST", "/sew/heart", { idempotencyKey: ikey(), creatureId: curId, itemId });
        if (rec.result !== "survived") {
          if (rec.genomeId) { registerName(rec.genomeId, c.name); setSpecimenGenome(sid, rec.genomeId); markDead(rec.genomeId); curId = rec.genomeId; }
          logEntry(`☠️ Restoring ${c.name}'s heart killed it on the table — stopped there.`);
          break;
        }
        registerName(rec.genomeId, c.name); setSpecimenGenome(sid, rec.genomeId); curId = rec.genomeId;
        restored.push("heart");
      } else {
        const rec = await api("POST", "/sew/part", { idempotencyKey: ikey(), creatureId: curId, slot: m, itemId });
        if (rec.result === "limb_rejected") {
          logEntry(`🦠 ${c.name}'s heart rejected the restored ${m} — stopped there.`);
          break;
        }
        if (rec.result === "patient_died") {
          registerName(rec.genomeId, c.name); setSpecimenGenome(sid, rec.genomeId); markDead(rec.genomeId); curId = rec.genomeId;
          logEntry(`☠️ Restoring ${c.name}'s ${m} killed it on the table — stopped there.`);
          break;
        }
        registerName(rec.genomeId, c.name); setSpecimenGenome(sid, rec.genomeId); curId = rec.genomeId;
        restored.push(m);
      }
    }
    local.selectedId = curId;
    if (restored.length === order.length) logEntry(`🧵 Restored ${c.name} to whole (${restored.join(", ")}).`);
    else if (restored.length) logEntry(`🧵 Partially restored ${c.name} (${restored.join(", ")}) before the surgery gave out.`);
  } catch (e) { logEntry(`⚠️ Restore failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

// Sews each item in order onto whatever's on the slab (c), stopping at
// the first rejection/death -- whatever's left in `items` just stays put
// (in the freezer, or still on the tray), nothing forces the run to
// finish. Shared by the freezer's one-click group graft and the tray's
// "graft the whole bench" -- same surgery, two different sources for
// the batch of items.
async function graftItems(c, items) {
  const sid = specimenIdOf(c.id);
  let curId = c.id;
  const grafted = [], consumed = [];
  let swapped = 0;
  for (const t of items) {
    if (t.item.kind === "heart") {
      const rec = await api("POST", "/sew/heart", { idempotencyKey: ikey(), creatureId: curId, itemId: t.itemId });
      if (rec.result !== "survived") {
        if (rec.genomeId) { registerName(rec.genomeId, c.name); setSpecimenGenome(sid, rec.genomeId); markDead(rec.genomeId); curId = rec.genomeId; }
        logEntry(`☠️ Grafting the ${t.item.tier} heart killed ${c.name} on the table — stopped there.`);
        break;
      }
      registerName(rec.genomeId, c.name); setSpecimenGenome(sid, rec.genomeId); curId = rec.genomeId;
      if (rec.explantedHeartItemId) registerFrom(rec.explantedHeartItemId, c.name);
      grafted.push(`${t.item.tier} heart`); consumed.push(t.itemId);
    } else {
      const slot = homologOf(t.item.family);
      const rec = await api("POST", "/sew/part", { idempotencyKey: ikey(), creatureId: curId, slot, itemId: t.itemId });
      if (rec.result === "limb_rejected") {
        logEntry(`🦠 ${c.name}'s heart rejected the ${t.item.family.replace(/_/g, " ")} — stopped there. Still usable.`);
        break;
      }
      if (rec.result === "patient_died") {
        registerName(rec.genomeId, c.name); setSpecimenGenome(sid, rec.genomeId); markDead(rec.genomeId); curId = rec.genomeId;
        logEntry(`☠️ Grafting the ${t.item.family.replace(/_/g, " ")} overloaded ${c.name}'s heart — dies on the table.`);
        break;
      }
      registerName(rec.genomeId, c.name); setSpecimenGenome(sid, rec.genomeId); curId = rec.genomeId;
      if (rec.explantedPartItemId) { registerFrom(rec.explantedPartItemId, c.name); swapped++; }
      grafted.push(t.item.family.replace(/_/g, " ")); consumed.push(t.itemId);
    }
  }
  local.selectedId = curId;
  return { grafted, consumed, swapped };
}

// Slab-mode shortcut: click a freezer thumbnail and its whole group grafts
// straight onto whatever's currently on the slab, no trip through the
// tray. (In Tray mode the same click adds the group to the tray's bench
// instead -- see the freezer's click handler.)
async function doGraftGroup(groupKey) {
  const c = selected(); if (!c) return;
  const items = groupItems(groupKey);
  if (!items.length) return;
  showBusy(true);
  try {
    const { grafted, swapped } = await graftItems(c, items);
    if (grafted.length) logEntry(`🪡 Grafted the ${grafted.join(", ")} onto ${c.name} straight off the slab.` +
      (swapped ? ` Swapped out ${swapped} old part${swapped === 1 ? "" : "s"}, back in the freezer.` : ""));
  } catch (e) { logEntry(`⚠️ Graft failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

// The tray's "graft the whole bench" -- whatever's currently pulled onto
// the mini-slab goes onto the real one, in one motion. Anything that
// stops the run early (rejection, death) simply stays on the tray for
// another attempt; only what actually took comes off it.
async function doGraftBench() {
  const c = selected(); if (!c) return;
  const items = benchItems();
  if (!items.length) return;
  showBusy(true);
  try {
    const { grafted, consumed, swapped } = await graftItems(c, items);
    local.trayBench = (local.trayBench ?? []).filter(id => !consumed.includes(id));
    if (grafted.length) logEntry(`🪡 Grafted the ${grafted.join(", ")} onto ${c.name} straight off the tray.` +
      (swapped ? ` Swapped out ${swapped} old part${swapped === 1 ? "" : "s"}, back in the freezer.` : ""));
  } catch (e) { logEntry(`⚠️ Graft failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

async function doReset() {
  if (!confirm("Reset this Lab session? Clears names, history, and your session ID. Server genomes are NOT deleted.")) return;
  destroyRenderer(); _lastPortraitId = null;
  local = newLocal();
  logEntry("🧹 New session started. Server genomes from prior sessions are still there under their old account ID.");
  saveLocal(); await sync();
  // newLocal() already resets chop/stable state (nothing on the slab,
  // freezer empty, tray closed) -- but if the Chop Shop or Stable screen
  // was on screen when Reset was clicked, sync() has no way to know to
  // repaint it: it dispatches by local.view, and only setView() actually
  // flips which <section> is visible. Without this, a reset mid-Chop-Shop
  // left that screen frozen on stale pre-reset content.
  setView("lab");
}

// ── factions ──────────────────────────────────────────────────────────────────
// Same Mutator service, three houses. Each gets its own lab skin so you
// always know whose bench you're standing at (docs/17 origins).
const FACTION_META = {
  maddr: { title: "⚗️ THE LAB",     sub: "MadDr.MCs test bench — mutator & chop shop" },
  human: { title: "📡 THE HANGAR",  sub: "Army R&D proving ground — requisition & retrofit" },
  alien: { title: "🛸 THE SANCTUM", sub: "Hive biotech vault — growth & communion" },
};

const FACTION_GLYPH = { maddr: "🧟", human: "🪖", alien: "🛸" };
const FACTION_LABEL = { maddr: "Mad Doctors", human: "Human Army", alien: "Alien Hive" };

// Which faction a creature belongs to: the stamped birth faction, else
// inferred from its part origins (biotech→alien, tech→human, else maddr).
function factionOfCreature(c) {
  const f = local.factionOf?.[c.id];
  if (f) return f;
  const fams = SLOT_NAMES.map(s => c.genome.slots[s].family);
  if (fams.some(fam => originOf(fam) === "biotech")) return "alien";
  if (fams.some(fam => originOf(fam) === "tech")) return "human";
  return "maddr";
}

// Where a specimen lives when nothing has recorded it explicitly yet
// (legacy local state from before rooms existed): infer it from its own
// structural state -- whole goes to the lab, anything short of that to
// the chop shop -- rather than defaulting everything to one room.
function locationOfSpecimen(sid, genome) {
  return local.locationOf[sid] ?? (isWhole(genome) ? "lab" : "chop");
}

// One card per PHYSICAL specimen, in the given room, on this faction's
// bench. Surgery mints a new immutable genome row on every cut or graft
// (docs/07 lineage) -- that's bookkeeping, not a new animal, so only the
// latest row for each specimen is ever shown.
function benchCreatures(loc) {
  const hidden = new Set(local.hidden ?? []);
  const out = [];
  for (const c of creatures) {
    if (hidden.has(c.id)) continue;
    if (factionOfCreature(c) !== local.faction) continue;
    const sid = specimenIdOf(c.id);
    const current = local.currentGenomeOf[sid] ?? c.id;
    if (c.id !== current) continue;                          // superseded row
    if (locationOfSpecimen(sid, c.genome) !== loc) continue;
    out.push(c);
  }
  return out;
}
function labCreatures()  { return benchCreatures("lab"); }
function chopCreatures() { return benchCreatures("chop"); }

// Selection must belong to whichever room is on screen; default to the
// first specimen there so the view is never blank when one exists.
function ensureSelection() {
  const list = local.view === "chop" ? chopCreatures() : labCreatures();
  if (!list.find(c => c.id === local.selectedId)) local.selectedId = list[0]?.id ?? null;
}

// ── specimen actions: name / delete / stable ──────────────────────────────────
function doRename() {
  const c = selected(); if (!c) return;
  const name = prompt("Name this specimen:", c.name);
  if (!name || !name.trim()) return;
  local.nameMap[c.id] = name.trim();
  c.name = name.trim();
  saveLocal(); logEntry(`🏷️ Renamed to ${c.name}.`); render();
}
function doDelete() {
  const c = selected(); if (!c) return;
  if (!confirm(`Clear ${c.name} from the bench? A saved stable copy is kept.`)) return;
  local.hidden = [...new Set([...(local.hidden ?? []), c.id])];
  local.selectedId = null;
  saveLocal(); logEntry(`🗑️ ${c.name} cleared from the bench.`); render();
}
function isSaved(id) { return (local.stable ?? []).includes(id); }
function doSaveStable() {
  const c = selected(); if (!c) return;
  if (isSaved(c.id)) return;
  local.stable = [...(local.stable ?? []), c.id];
  saveLocal(); logEntry(`⭐ ${c.name} saved to the stable.`); render();
}
function doUnsaveStable(id) {
  local.stable = (local.stable ?? []).filter(x => x !== id);
  saveLocal();
  if (local.view === "stable") renderStable(); else render();
}

// ── view switching: Lab ⇄ Stable ──────────────────────────────────────────────
function setView(v) {
  local.view = v; saveLocal();
  document.getElementById("view-lab").style.display = v === "lab" ? "" : "none";
  document.getElementById("view-chop").style.display = v === "chop" ? "flex" : "none";
  document.getElementById("view-stable").style.display = v === "stable" ? "flex" : "none";
  document.getElementById("nav-lab").classList.toggle("active", v === "lab");
  document.getElementById("nav-chop").classList.toggle("active", v === "chop");
  document.getElementById("nav-stable").classList.toggle("active", v === "stable");
  destroyRenderer(); _lastPortraitId = null;   // hand the single renderer over
  stopTurntables();   // leaving the Chop Shop mid-tray shouldn't leave rAF loops spinning
  if (v === "stable") renderStable();
  else if (v === "chop") { applyFaction(local.faction); renderChop(); }
  else { applyFaction(local.faction); render(); }
}

function applyFaction(f) {
  const meta = FACTION_META[f] ?? FACTION_META.maddr;
  document.body.dataset.faction = f;
  document.getElementById("lab-title").textContent = meta.title;
  document.getElementById("lab-sub").textContent = meta.sub;
  document.getElementById("sel-faction").value = f;
  setLabFaction(f);
  destroyRenderer();            // force the portrait to rebuild its scene
  _lastPortraitId = null;
  ensureSelection();
}

// ── busy indicator ────────────────────────────────────────────────────────────
function showBusy(on) {
  document.getElementById("btn-spawn").disabled = on;
  document.getElementById("btn-reset").disabled = on;
}

// ── render ────────────────────────────────────────────────────────────────────
function render() {
  document.getElementById("wallet").textContent = `🩸 ${blood}`;
  if (local.view !== "lab") return;      // the stable/chop shop draw themselves
  for (const fn of [renderRoster, renderActions, renderScreen, renderLog, renderPortrait]) {
    try { fn(); } catch (err) { console.error(`${fn.name} crashed:`, err); }
  }
}

function renderRoster() {
  const el = document.getElementById("roster");
  const list = labCreatures();
  if (list.length === 0) { el.innerHTML = `<div class="empty">This lab's slab is empty. Spawn a specimen.</div>`; return; }
  el.innerHTML = list.map(c => {
    const v = viability(c.genome);
    const badge = c.alive ? `<span class="badge ${v.state}">${v.state}</span>` : `<span class="badge dead">DEAD</span>`;
    return `<div class="card ${c.id === local.selectedId ? "selected" : ""} ${c.alive ? "" : "dead"}" data-id="${c.id}">
      <div class="name">${c.alive ? "" : "💀 "}${esc(c.name)}${badge}</div>
      <div class="meta">${esc(c.genome.body.plan)} · ${esc(c.genome.brain.tier)} brain · ${esc(c.genome.heart.tier)} heart</div>
    </div>`;
  }).join("");
  el.querySelectorAll(".card").forEach(card =>
    card.addEventListener("click", () => { local.selectedId = card.dataset.id; saveLocal(); render(); }));
}

function renderActions() {
  const el = document.getElementById("actions");
  const c = selected();
  if (!c) { el.innerHTML = ""; return; }
  const partners = labCreatures().filter(x => x.alive && x.id !== c.id);
  const feedOpts = `<option value="">no feeding</option>` + SLOT_NAMES.map(s => `<option value="${s}">feed the ${s}</option>`).join("");
  const partnerOpts = partners.map(p => `<option value="${p.id}">${esc(p.name)}</option>`).join("");
  const dead = !c.alive;
  el.innerHTML = `
    <div class="group"><span class="lbl">Mutator</span>
      <select id="sel-feed" ${dead ? "disabled" : ""}>${feedOpts}</select>
      <button id="btn-mutate" ${dead ? "disabled" : ""}>🧬 Mutate (🩸10)</button>
      <select id="sel-partner" ${dead || !partners.length ? "disabled" : ""}>${partnerOpts || "<option>no partner</option>"}</select>
      <button id="btn-splice" ${dead || !partners.length ? "disabled" : ""}>🧪 Splice (🩸20)</button>
    </div>
    <div class="group"><span class="lbl">Specimen</span>
      <button id="btn-rename">🏷️ Name</button>
      <button id="btn-save">${isSaved(c.id) ? "★ In stable" : "⭐ Save to stable"}</button>
      <button id="btn-chop">🔪 Send to chop shop</button>
      <button id="btn-delete" class="danger">🗑️ Delete</button>
    </div>`;
  document.getElementById("btn-rename")?.addEventListener("click", doRename);
  document.getElementById("btn-save")?.addEventListener("click", doSaveStable);
  document.getElementById("btn-chop")?.addEventListener("click", doSendToChop);
  document.getElementById("btn-delete")?.addEventListener("click", doDelete);
  document.getElementById("btn-mutate")?.addEventListener("click", () =>
    doMutate(document.getElementById("sel-feed").value || undefined));
  document.getElementById("btn-splice")?.addEventListener("click", () =>
    doSplice(document.getElementById("sel-partner").value));
}

function renderScreen() {
  const el = document.getElementById("screen");
  try {
    _renderScreenInner(el);
  } catch (err) {
    console.error("renderScreen error:", err);
    el.innerHTML = `<div class="empty" style="color:var(--blood);text-align:left;white-space:pre-wrap">⚠️ Render error — open browser console for details.\n\n${esc(String(err))}</div>`;
  }
}

function _renderScreenInner(el) {
  const c = selected();
  if (!c) { el.innerHTML = `<div class="empty">Spawn a specimen, then select it.</div>`; return; }
  const g = c.genome;
  const v = viability(g);
  const u = upkeep(g);
  const vClass = !c.alive ? "bad" : v.state === "viable" ? "ok" : v.state === "strained" ? "warn" : "bad";

  const partRows = SLOT_NAMES.map(s => {
    const a = g.slots[s];
    const stump = isVestigial(a.family);
    const o = originOf(a.family);
    const pu = partUpkeep(a);
    return `<tr>
      <td>${s}</td>
      <td>${esc(a.family.replace(/_/g, " "))}${stump ? ' <span class="badge stump">stump</span>' : ""}</td>
      <td><span class="badge ${o}">${o}</span></td>
      <td class="${pu.type}">${pu.perMin.toFixed(1)} ${pu.type}/min</td>
    </tr>`;
  }).join("");

  const brainRows = BRAIN_AXES.map(a => `<div class="k">${a}</div><div>${bar(brainAxis(g.brain, a))}</div>`).join("");
  const bodyRows  = BODY_AXES.map(a => `<div class="k">${a}</div><div>${bar(bodyAxis(g.body, a))}</div>`).join("");
  const bt = berserkThreshold(g.brain);
  const parents = (g.parentIds ?? []).map(p => esc(nameOf(p))).join(" × ") || "primordial (no parents)";

  el.innerHTML = `
    <h3>${c.alive ? "" : "💀 "}${esc(c.name)} — vital signs</h3>
    <div class="kv">
      <div class="k">status</div><div class="${vClass}">${c.alive ? v.state.toUpperCase() : "DEAD ON THE TABLE"}</div>
      <div class="k">heart</div><div>${esc(g.heart.tier)} (vigor ${pct(heartVigor(g.heart))}) — capacity <span class="num">${heartCapacity(g.heart).toFixed(1)}</span>/min</div>
      <div class="k">circulatory load</div><div><span class="num">${circulatoryLoad(g).toFixed(1)}</span>/min — margin <span class="${v.margin >= 0 ? "ok" : "bad"}">${v.margin >= 0 ? "+" : ""}${v.margin.toFixed(1)}</span></div>
      <div class="k">upkeep</div><div><span class="blood">${u.blood.toFixed(1)} blood</span> · <span class="fuel">${u.fuel.toFixed(1)} fuel</span> · <span class="ichor">${u.ichor.toFixed(1)} ichor</span> /min</div>
      <div class="k">server id</div><div style="color:var(--dim);font-size:11px">${esc(c.id)}</div>
      <div class="k">lineage</div><div>${parents}</div>
    </div>
    <h3>Body — ${esc(g.body.plan)}</h3>
    <div class="kv">${bodyRows}</div>
    <h3>Parts</h3>
    <table><tr><th>slot</th><th>family</th><th>origin</th><th>energy</th></tr>${partRows}</table>
    <h3>Brain — ${esc(g.brain.tier)} (size ${brainSize(g.brain)})</h3>
    <div class="kv">${brainRows}</div>
    <h3>Behavior (expressed)</h3>
    <div class="kv">
      <div class="k">control capacity</div><div class="num">${controlCapacity(g.brain).toFixed(2)} pts</div>
      <div class="k">control cost</div><div class="num">${controlCost(g.brain).toFixed(2)} pts to hold</div>
      <div class="k">control radius</div><div class="num">${controlRadius(g.brain).toFixed(1)} hexes</div>
      <div class="k">berserk threshold</div><div>${bt > 1 ? '<span class="ok">never berserks</span>' : `<span class="warn">rage ${pct(bt)}</span>`}</div>
    </div>`;
}

// ── the Chop Shop (separate screen) ────────────────────────────────────────────
// The current specimen sits "on the slab"; cuts move parts into "the
// freezer" (the same server-side tray/inventory the old inline harvest
// controls use — this screen is a friendlier front end on the same
// /harvest, /sew, and /tray calls, not a new backend). Region granularity
// is capped by what the genome schema actually models: hand (=arm+hand),
// sensor, eye, leg (=leg+foot), heart. "Head" bundles sensor+eye since
// those are the two head-mounted slots and eyes genuinely can be taken
// separately from the rest of the head. The brain isn't a separable part
// at all yet, so "heads and brains never separate" holds by construction
// — there's simply no cut that could split them.
const CHOP_REGIONS = [
  {
    key: "head", title: "🧠 Head", slots: ["sensor", "eye"],
    note: "The brain never leaves the skull — there is no cut for that.",
    actions: [
      { label: "Take the whole head (sensors + eyes)", slots: ["sensor", "eye"], cutLabel: "head" },
      { label: "Take the sensors, leave the eyes", slots: ["sensor"], cutLabel: "sensors" },
      { label: "Take the eyes only", slots: ["eye"], cutLabel: "eyes" },
    ],
  },
  {
    key: "torso", title: "🫀 Torso", slots: [], heartOnly: true,
    note: "Only the heart is a separable cut today — hide and ribs stay with whatever body remains.",
    actions: [{ label: "Take the heart", heart: true }],
  },
  {
    key: "lower", title: "🦵 Lower body", slots: ["leg"],
    note: "Both legs come off together as one cut — no separate foot yet.",
    actions: [{ label: "Take the legs", slots: ["leg"], cutLabel: "legs" }],
  },
  {
    key: "arms", title: "✋ Arms & hands", slots: ["hand"],
    note: "Arm and hand are one piece today — no separate wrist cut yet.",
    actions: [{ label: "Take the arms (hands included)", slots: ["hand"], cutLabel: "arms" }],
  },
];
// The freezer's four labeled drawers, in the same order as the regions
// above -- one physical place per body area, however many batches of
// parts have piled up in it over time.
const DRAWERS = [
  { key: "head",  title: "🧠 Head" },
  { key: "torso", title: "🫀 Torso" },
  { key: "lower", title: "🦵 Lower body" },
  { key: "arms",  title: "✋ Arms & hands" },
];

function renderChop() {
  document.getElementById("wallet").textContent = `🩸 ${blood}`;
  if (local.view !== "chop") return;
  renderChopRoster();
  renderChopModeButtons();
  renderChopCenter();
  renderChopRegions();
  renderChopFreezer();
  renderLog();
}

// The center panel is either "the slab" (the live specimen) or "the
// surgical tray" (one opened batch of harvested parts, worked on in
// isolation). Clicking a batch's thumbnail in the freezer switches here
// automatically; the two buttons let the user flip back manually without
// re-opening the drawer.
function renderChopModeButtons() {
  const slabBtn = document.getElementById("btn-mode-slab");
  const trayBtn = document.getElementById("btn-mode-tray");
  const title = document.getElementById("chop-center-title");
  if (!slabBtn || !trayBtn) return;
  const mode = local.chopMode ?? "slab";
  slabBtn.classList.toggle("active", mode === "slab");
  trayBtn.classList.toggle("active", mode === "tray");
  trayBtn.disabled = tray.length === 0;   // nothing in the freezer to inspect at all
  if (title) title.textContent = mode === "tray" ? "Surgical tray" : "The slab";
}

function renderChopCenter() {
  const mode = local.chopMode ?? "slab";
  if (mode === "tray") renderChopTray();   // handles "nothing open yet" itself
  else renderChopSlab();
}

function renderChopRoster() {
  const el = document.getElementById("chop-roster");
  const list = chopCreatures();
  if (list.length === 0) {
    el.innerHTML = `<div class="empty">Nobody's under the knife. Send a specimen over from the Lab.</div>`; return;
  }
  el.innerHTML = list.map(c => {
    const v = viability(c.genome);
    const badge = c.alive ? `<span class="badge ${v.state}">${v.state}</span>` : `<span class="badge dead">DEAD</span>`;
    return `<div class="card small ${c.id === local.selectedId ? "selected" : ""} ${c.alive ? "" : "dead"}" data-id="${c.id}">
      <div class="name">${c.alive ? "" : "💀 "}${esc(c.name)}${badge}</div>
      <div class="meta">${esc(c.genome.body.plan)}</div>
    </div>`;
  }).join("");
  el.querySelectorAll(".card").forEach(card =>
    card.addEventListener("click", () => { local.selectedId = card.dataset.id; saveLocal(); renderChop(); }));
}

function renderChopSlab() {
  const wrap = document.getElementById("chop-slab");
  const c = selected();
  destroyRenderer();
  stopTurntables();
  if (!c) { wrap.innerHTML = `<div class="empty">Nothing on the slab. Send a specimen over from the Lab.</div>`; return; }
  const g = c.genome;
  const v = viability(g);
  const vClass = !c.alive ? "bad" : v.state === "viable" ? "ok" : v.state === "strained" ? "warn" : "bad";
  const sid = specimenIdOf(c.id);
  const whole = isWhole(g);
  const check = restoreCheck(sid, g);
  const canSendBack = c.alive && whole;
  const restoreTitle = check.ok ? "Sew this specimen's own harvested parts back on"
    : check.reason === "whole" ? "Already whole -- nothing to restore"
    : "One or more of its original parts have already been grafted onto something else";
  const sendTitle = canSendBack ? "" : !c.alive ? "Dead specimens can't go back to the lab"
    : "Must be whole -- no stumps, working heart -- first";
  wrap.innerHTML = `
    <canvas id="chop-canvas"></canvas>
    <div class="chop-slab-label">
      <div class="pl-name">${c.alive ? "" : "💀 "}${esc(c.name)}</div>
      <div class="pl-plan">${esc(g.body.plan)} · ${esc(g.brain.tier)} brain · ${esc(g.heart.tier)} heart</div>
      <div class="pl-stat ${vClass}">${c.alive ? v.state.toUpperCase() : "DEAD ON THE TABLE"}</div>
    </div>
    <div class="chop-slab-actions">
      <button id="btn-restore" title="${restoreTitle}" ${check.ok ? "" : "disabled"}>🩹 Restore original parts</button>
      <button id="btn-tolab" class="primary" title="${sendTitle}" ${canSendBack ? "" : "disabled"}>🏠 Send back to lab</button>
    </div>`;
  try { initRenderer(document.getElementById("chop-canvas"), g); } catch (e) { console.error(e); }
  document.getElementById("btn-restore")?.addEventListener("click", doRestore);
  document.getElementById("btn-tolab")?.addEventListener("click", doSendToLab);
}

function renderChopRegions() {
  const el = document.getElementById("chop-regions");
  const c = selected();
  if (!c) { el.innerHTML = `<div class="empty">Nothing on the slab.</div>`; return; }
  const g = c.genome;
  el.innerHTML = CHOP_REGIONS.map(r => {
    const occ = r.heartOnly
      ? `🫀 ${esc(g.heart.tier)} heart`
      : r.slots.map(s => {
          const fam = g.slots[s].family;
          const stump = isVestigial(fam);
          return `${s}: ${esc(fam.replace(/_/g, " "))}${stump ? ' <span class="badge stump">stump</span>' : ""}`;
        }).join(" · ");
    const btns = r.actions.map((a, i) => {
      const already = a.heart ? false : a.slots.every(s => isVestigial(g.slots[s].family));
      return `<button class="small" data-region="${r.key}" data-action="${i}" ${already ? "disabled" : ""}>✂️ ${esc(a.label)}</button>`;
    }).join("");
    return `<div class="region-card">
      <div class="region-title">${r.title}</div>
      <div class="region-occ">${occ}</div>
      <div class="region-actions">${btns}</div>
      <div class="region-note">${esc(r.note)}</div>
    </div>`;
  }).join("");
  el.querySelectorAll("button[data-region]").forEach(b => {
    b.addEventListener("click", () => {
      const r = CHOP_REGIONS.find(x => x.key === b.dataset.region);
      const a = r.actions[Number(b.dataset.action)];
      if (a.heart) doHarvestHeart();
      else doHarvestRegion(a.slots, a.cutLabel);
    });
  });
}

// The freezer: four labeled drawers (native <details> -- click a label,
// it pops open right there, no extra state to track). Inside, one SIMPLE
// thumbnail per specimen that's contributed to that drawer -- "the head
// from Specimen-01" -- not a breakdown of what's inside.
//
// What clicking a thumbnail does depends on which mode the slab is in:
//   - Slab mode: grafts the whole group straight onto whatever's on the
//     slab right now -- no detour through the tray.
//   - Tray mode: adds that group onto the tray's own mini-slab (the
//     bench) instead of touching the real slab -- click another tile
//     and it piles on rather than replacing what's already there.
function renderChopFreezer() {
  const el = document.getElementById("chop-freezer");
  if (tray.length === 0) { el.innerHTML = `<div class="empty">The freezer is empty. Harvest something to fill a drawer.</div>`; return; }
  const inSlabMode = (local.chopMode ?? "slab") === "slab";
  const c = selected();
  const byDrawer = Object.fromEntries(DRAWERS.map(d => [d.key, []]));
  for (const t of tray) (byDrawer[drawerKeyForItem(t.item)] ??= []).push(t);

  el.innerHTML = DRAWERS.map(d => {
    const items = byDrawer[d.key] ?? [];
    const groups = new Map();
    for (const t of items) {
      const k = groupKeyOf(t);
      if (!groups.has(k)) groups.set(k, []);
      groups.get(k).push(t);
    }
    // the badge counts what's actually rendered below -- tiles (one per
    // contributing specimen), not raw harvested pieces, so "the whole
    // head" (sensor + eye, 2 pieces) off ONE specimen reads as 1, matching
    // the 1 tile you'll actually see, not a confusing 2
    return `<details class="drawer-unit" ${items.length ? "" : "data-empty"}>
      <summary class="drawer-label"><span>${d.title}</span><span class="drawer-count">${groups.size}</span></summary>
      <div class="drawer-contents">
        ${groups.size === 0 ? `<div class="empty">Empty.</div>` : [...groups.entries()].map(([k, its]) => groupTileHtml(d, k, its, { inSlabMode, canGraft: !!c })).join("")}
      </div>
    </details>`;
  }).join("");

  el.querySelectorAll("[data-open-group]").forEach(t =>
    t.addEventListener("click", () => {
      const key = t.dataset.openGroup;
      if (inSlabMode) {
        if (c) doGraftGroup(key);
      } else {
        const ids = groupItems(key).map(x => x.itemId);
        local.trayBench = [...new Set([...(local.trayBench ?? []), ...ids])];
        saveLocal(); renderChop();
      }
    }));
}

function groupTileHtml(drawer, groupKey, items, { inSlabMode, canGraft }) {
  const from = local.trayFrom[items[0].itemId] ?? "unknown";
  const label = drawer.title.replace(/^\S+\s/, "");
  const graftable = inSlabMode && canGraft;
  const onBench = !inSlabMode && items.every(t => (local.trayBench ?? []).includes(t.itemId));
  const title = inSlabMode
    ? (canGraft ? "Graft straight onto the specimen on the slab" : "Pick a specimen on the slab first")
    : (onBench ? "Already on the tray" : "Add to the tray");
  return `<div class="part-tile ${inSlabMode && !canGraft ? "disabled" : ""}" data-open-group="${esc(groupKey)}" title="${title}">
    <div class="part-thumb">${partThumbHtml(items[0])}</div>
    <div class="part-name">${esc(label)}${graftable ? ` <span class="badge">🪡 graft</span>` : ""}${onBench ? ` <span class="badge">🗄️ on tray</span>` : ""}</div>
    <div class="part-from">from ${esc(from)}</div>
  </div>`;
}

// One item's card on the tray: graft just this piece onto the slab, or
// harvest it back off the tray into the freezer (it was never anywhere
// else -- this just drops it out of the bench selection).
function benchChipHtml(t, { canSend }) {
  const item = t.item;
  const originBadge = item.kind === "heart" ? "" : `<span class="badge ${originOf(item.family)}">${originOf(item.family)}</span>`;
  return `<div class="part-tile">
    <div class="part-thumb">${partThumbHtml(t)}</div>
    <div class="part-body">
      <div class="part-name">${esc(partName(item))} ${originBadge}</div>
      <div class="part-stats kv">${partStatsHtml(item)}</div>
      <div class="bench-chip-actions">
        <button class="small" data-item="${t.itemId}" ${canSend ? "" : "disabled"} title="${canSend ? "" : "Pick a specimen on the slab first"}">🪡 Graft (🩸5)</button>
        <button class="small" data-harvest="${t.itemId}" title="Take it back off the tray -- stays in the freezer">🔪 Harvest</button>
      </div>
    </div>
  </div>`;
}

// The surgical tray, now a mini-slab: click freezer tiles (in Tray mode)
// to pull parts onto it -- they pile up rather than replace each other --
// see them assembled as one live turntable, harvest any single piece back
// off, or graft the whole bench onto whatever's on the real slab at once.
function renderChopTray() {
  const wrap = document.getElementById("chop-slab");
  destroyRenderer();
  stopTurntables();
  const items = benchItems();
  if (items.length === 0) {
    wrap.innerHTML = `<div class="tray-view"><div class="empty">Nothing on the tray. Click a freezer drawer tile to pull its parts onto it.</div></div>`;
    return;
  }
  const c = selected();
  const canGraft = !!c;
  wrap.innerHTML = `
    <div class="tray-view">
      <canvas id="bench-canvas"></canvas>
      <div class="tray-head">
        <div class="tray-title">The tray <span class="badge">${items.length} piece${items.length === 1 ? "" : "s"}</span></div>
      </div>
      <div class="tray-parts">${items.map(t => benchChipHtml(t, { canSend: canGraft })).join("")}</div>
      <div class="chop-slab-actions">
        <button id="btn-graft-bench" class="primary" ${canGraft ? "" : "disabled"} title="${canGraft ? "" : "Pick a specimen on the slab first"}">🪡 Graft all to slab (🩸${items.length * 5})</button>
        <button id="btn-clear-bench">↩️ Clear the tray</button>
      </div>
    </div>`;
  activeTurntables.push(initBenchTurntable(document.getElementById("bench-canvas"), benchParts(), local.faction));
  wrap.querySelectorAll("button[data-item]").forEach(b =>
    b.addEventListener("click", () => doSew(b.dataset.item)));
  wrap.querySelectorAll("button[data-harvest]").forEach(b =>
    b.addEventListener("click", () => {
      local.trayBench = (local.trayBench ?? []).filter(id => id !== b.dataset.harvest);
      saveLocal(); renderChop();
    }));
  document.getElementById("btn-graft-bench")?.addEventListener("click", doGraftBench);
  document.getElementById("btn-clear-bench")?.addEventListener("click", () => {
    local.trayBench = []; saveLocal(); renderChop();
  });
}

// Two copies of the notebook exist in the DOM -- the Lab's side panel and
// a compact one in the Chop Shop -- since surgery happens on a screen
// that has no other feedback: without this, a successful graft (or a
// rejected one) shows nothing at all unless you flip back to the Lab.
function renderLog() {
  const html = local.log.map(l => `<p><span class="t">${l.t}</span>${esc(l.msg)}</p>`).join("") ||
    `<div class="empty">The notebook is blank.</div>`;
  for (const id of ["log", "chop-log"]) {
    const el = document.getElementById(id);
    if (el) el.innerHTML = html;
  }
}

// ── build version indicator ──────────────────────────────────────────────────
// Two independently-deployed halves: GitHub Pages autodeploys the
// frontend on every push to main, but the mutator-service on Render does
// NOT autodeploy just because this repo moves -- it needs its own
// trigger and has, in practice, gone silently stale for weeks (docs/12
// decision log). Showing both commits plus how long the backend process
// has actually been running makes that drift visible without digging
// through git log to notice a feature "never" works.
function timeAgo(iso) {
  const ms = Date.now() - new Date(iso ?? NaN).getTime();
  if (!Number.isFinite(ms) || ms < 0) return "unknown";
  const mins = Math.floor(ms / 60000);
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 48) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}

async function loadBuildInfo() {
  const el = document.getElementById("build-info");
  if (!el) return;
  const [fe, be] = await Promise.all([
    fetch("./version.json").then(r => r.ok ? r.json() : null).catch(() => null),
    fetch(`${MUTATOR_URL}/version`).then(r => r.ok ? r.json() : null).catch(() => null),
  ]);
  const feBit = `frontend <code>${esc(fe?.commit ?? "dev")}</code>`;
  const beKnown = !!be?.commit && be.commit !== "unknown";
  const beBit = !be
    ? "backend unreachable"
    : beKnown
      ? `backend <code>${esc(be.commit)}</code> (up ${timeAgo(be.startedAt)})`
      : `backend commit unknown (up ${timeAgo(be.startedAt)}) -- redeploy needed to bake one in`;
  el.innerHTML = `Build: ${feBit} · <span class="${beKnown ? "" : "warn"}">${beBit}</span>`;
}

// ── portrait renderer ─────────────────────────────────────────────────────────
let _lastPortraitId = null;

function renderPortrait() {
  const wrap   = document.getElementById("portrait-wrap");
  const canvas = document.getElementById("portrait");
  const label  = document.getElementById("portrait-label");
  const c = selected();
  if (!c) {
    destroyRenderer(); _lastPortraitId = null;
    wrap.style.display = "none"; label.innerHTML = ""; return;
  }
  wrap.style.display = "flex";
  const g = c.genome;
  const v = viability(g);
  const vClass = !c.alive ? "bad" : v.state === "viable" ? "ok" : v.state === "strained" ? "warn" : "bad";
  const parts = SLOT_NAMES.filter(s => !isVestigial(g.slots[s].family))
    .map(s => g.slots[s].family.replace(/_/g, " ")).join(" · ") || "—";
  const loco = locomotionProfile(g);
  label.innerHTML = `
    <div class="pl-name">${c.alive ? "" : "💀 "}${esc(c.name)}</div>
    <div class="pl-plan">${esc(g.body.plan)} · ${esc(g.brain.tier)} brain · ${esc(g.heart.tier)} heart</div>
    <div class="pl-stat ${vClass}">${c.alive ? v.state.toUpperCase() : "DEAD ON THE TABLE"}</div>
    <div class="pl-stat">load <span>${circulatoryLoad(g).toFixed(1)}</span> / cap <span>${heartCapacity(g.heart).toFixed(1)}</span></div>
    <div class="pl-stat" style="line-height:1.5">${esc(parts)}</div>
    <div class="pl-stat">🏃 walk <span>${loco.walkSpeed}</span> · run <span>${loco.runSpeed}</span> hex/min · sprint <span>${loco.sprint}</span></div>`;
  if (c.id !== _lastPortraitId) { initRenderer(canvas, g); _lastPortraitId = c.id; }
  else updateGenome(g);
}

// ── the Stable (separate screen) ──────────────────────────────────────────────
// Saved specimens across all three labs, as rendered thumbnails; click one
// for the full living portrait and stats. Thumbs are cached for the session.
const thumbCache = {};

function renderStable() {
  const grid = document.getElementById("stable-grid");
  const detail = document.getElementById("stable-detail");
  const saved = (local.stable ?? []).map(id => byId(id)).filter(Boolean);
  if (saved.length === 0) {
    grid.innerHTML = `<div class="empty">The stable is empty. In a lab, select a specimen and press \u201c\u2b50 Save to stable\u201d.</div>`;
    detail.innerHTML = ""; return;
  }
  grid.innerHTML = saved.map(c => {
    const fac = factionOfCreature(c);
    if (!thumbCache[c.id]) { try { thumbCache[c.id] = renderThumbnail(c.genome, fac); } catch { thumbCache[c.id] = ""; } }
    return `<div class="stable-card ${c.id === local.selectedId ? "selected" : ""}" data-id="${c.id}">
      <div class="sc-thumb">${thumbCache[c.id] ? `<img src="${thumbCache[c.id]}" alt="">` : ""}<span class="sc-fac">${FACTION_GLYPH[fac]}</span></div>
      <div class="sc-name">${esc(c.name)}${c.alive ? "" : " 💀"}</div>
      <div class="sc-meta">${esc(c.genome.body.plan)} · ${esc(c.genome.heart.tier)} heart</div>
    </div>`;
  }).join("");
  grid.querySelectorAll(".stable-card").forEach(card =>
    card.addEventListener("click", () => { local.selectedId = card.dataset.id; saveLocal(); renderStable(); }));
  const cur = saved.find(c => c.id === local.selectedId) ?? saved[0];
  showStableDetail(cur.id);
}

function showStableDetail(id) {
  const c = byId(id); if (!c) return;
  const g = c.genome, fac = factionOfCreature(c);
  const v = viability(g), u = upkeep(g), loco = locomotionProfile(g);
  const vClass = !c.alive ? "bad" : v.state === "viable" ? "ok" : v.state === "strained" ? "warn" : "bad";
  const parts = SLOT_NAMES.filter(s => !isVestigial(g.slots[s].family))
    .map(s => g.slots[s].family.replace(/_/g, " ")).join(" · ") || "\u2014";
  const det = document.getElementById("stable-detail");
  det.innerHTML = `
    <canvas id="stable-portrait"></canvas>
    <div class="sd-info">
      <h3>${esc(c.name)}${c.alive ? "" : " 💀"}</h3>
      <div class="sd-fac">${FACTION_GLYPH[fac]} ${FACTION_LABEL[fac]}</div>
      <div class="kv">
        <div class="k">form</div><div>${esc(g.body.plan)}</div>
        <div class="k">brain</div><div>${esc(g.brain.tier)}</div>
        <div class="k">heart</div><div>${esc(g.heart.tier)} \u2014 cap <span class="num">${heartCapacity(g.heart).toFixed(1)}</span></div>
        <div class="k">viability</div><div class="${vClass}">${c.alive ? v.state.toUpperCase() : "DEAD"}</div>
        <div class="k">upkeep</div><div><span class="blood">${u.blood.toFixed(1)}b</span> \u00b7 <span class="fuel">${u.fuel.toFixed(1)}f</span> \u00b7 <span class="ichor">${u.ichor.toFixed(1)}i</span>/min</div>
        <div class="k">speed</div><div>walk <span class="num">${loco.walkSpeed}</span> \u00b7 run <span class="num">${loco.runSpeed}</span> \u00b7 sprint ${loco.sprint}</div>
        <div class="k">parts</div><div>${esc(parts)}</div>
      </div>
      <div class="sd-actions">
        <button id="sd-rename">🏷️ Rename</button>
        <button id="sd-remove" class="danger">\u2796 Remove from stable</button>
      </div>
    </div>`;
  setLabFaction(fac);
  destroyRenderer(); _lastPortraitId = null;
  try { initRenderer(document.getElementById("stable-portrait"), g); } catch (e) { console.error(e); }
  document.getElementById("sd-rename").addEventListener("click", () => {
    const n = prompt("Rename this specimen:", c.name);
    if (n && n.trim()) { local.nameMap[id] = n.trim(); c.name = n.trim(); saveLocal(); delete thumbCache[id]; renderStable(); }
  });
  document.getElementById("sd-remove").addEventListener("click", () => doUnsaveStable(id));
}

// \u2500\u2500 boot \u2500\u2500
document.getElementById("btn-spawn").addEventListener("click", doSpawn);
document.getElementById("btn-reset").addEventListener("click", doReset);
document.getElementById("nav-lab").addEventListener("click", () => setView("lab"));
document.getElementById("nav-chop").addEventListener("click", () => setView("chop"));
document.getElementById("nav-stable").addEventListener("click", () => setView("stable"));
document.getElementById("btn-mode-slab").addEventListener("click", () => {
  local.chopMode = "slab"; saveLocal(); renderChop();
});
document.getElementById("btn-mode-tray").addEventListener("click", () => {
  local.chopMode = "tray"; saveLocal(); renderChop();
});
local.faction ??= "maddr";
local.view ??= "lab";
applyFaction(local.faction);
document.getElementById("sel-faction").addEventListener("change", (e) => {
  local.faction = e.target.value;
  saveLocal();
  applyFaction(local.faction);
  logEntry(`🚪 Crossed over to ${e.target.selectedOptions[0].textContent.trim()}.`);
  if (local.view === "chop") renderChop(); else render();
});

// Show connecting state, then sync
document.getElementById("roster").innerHTML = `<div class="empty">Connecting to Mutator…</div>`;
logEntry("🔦 The lab lights flicker on. Connecting to Mutator service…");
setView(local.view);   // establish Lab/Stable layout, then load
sync().catch(e => {
  logEntry(`⚠️ Could not reach the Mutator service: ${e.message}`);
  render();
});
loadBuildInfo();
