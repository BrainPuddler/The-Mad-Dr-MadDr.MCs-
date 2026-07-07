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
import { initRenderer, updateGenome, destroyRenderer, locomotionProfile, setLabFaction, renderThumbnail } from "./creature-renderer.js";

const MUTATOR_URL = "https://maddr-mutator.onrender.com";
const LOCAL_KEY   = "maddr-lab-v2";

// ── local-only state (localStorage) ──────────────────────────────────────────
// accountId : stable UUID → identifies this browser to the server
// nameMap   : { [genomeId]: string }  — we assign human names, server assigns IDs
// deadSet   : string[]  — genomeIds that died on the table
// trayFrom  : { [itemId]: string }  — creature name a tray item came from
// log       : notebook entries
// seq       : auto-name counter
// selectedId: currently selected genome ID

let local = (() => {
  try { return JSON.parse(localStorage.getItem(LOCAL_KEY)); } catch { return null; }
})() ?? newLocal();

function newLocal() {
  return { accountId: crypto.randomUUID(), nameMap: {}, deadSet: [], trayFrom: {}, log: [], seq: 0, selectedId: null, faction: "maddr", stable: [], hidden: [], factionOf: {}, view: "lab" };
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

  // selection must be a member of the current faction's bench; default to
  // the first one so the data screen is never blank when creatures exist
  const lab = labCreatures();
  if (!lab.find(c => c.id === local.selectedId)) {
    local.selectedId = lab[0]?.id ?? null;
  }

  if (local.view === "stable") renderStable(); else render();
}

// ── operations ────────────────────────────────────────────────────────────────

// Each faction has its OWN roster of body forms — creatures are not
// transferable between races. Mad Doctors field the full monster mix;
// the Human Army only builds bipedal/flying machines (robots don't
// slither or ooze — the tank comes from tech legs); the Alien Hive
// skews crawling and amorphous with few flyers.
const FACTION_PLANS = {
  maddr: [["tetrapod", 0.55], ["winged", 0.2], ["serpentine", 0.15], ["blob", 0.1]],
  human: [["tetrapod", 0.7], ["winged", 0.3]],
  alien: [["tetrapod", 0.35], ["serpentine", 0.3], ["blob", 0.25], ["winged", 0.1]],
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
      local.selectedId = rec.genomeId;
      logEntry(`🧪 Spliced ${a.name} × ${b.name} → ${name}.`);
    }
  } catch (e) { logEntry(`⚠️ Splice failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

async function doHarvestPart(slot) {
  const c = selected(); if (!c) return;
  if (isVestigial(c.genome.slots[slot].family)) {
    logEntry(`✂️ ${c.name}'s ${slot} is already a stump.`); render(); return;
  }
  showBusy(true);
  try {
    const rec = await api("POST", "/harvest/part", { idempotencyKey: ikey(), creatureId: c.id, slot });
    // rec: { genomeId (stumped), genome, part, itemId }
    registerName(rec.genomeId, c.name);
    registerFrom(rec.itemId, c.name);
    local.selectedId = rec.genomeId;
    logEntry(`✂️ Cut the ${rec.part.family.replace(/_/g, " ")} off ${c.name}. The ${slot} heals to a stump; part is in the tray.`);
  } catch (e) { logEntry(`⚠️ Harvest failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

async function doHarvestHeart() {
  const c = selected(); if (!c) return;
  showBusy(true);
  try {
    const rec = await api("POST", "/harvest/heart", { idempotencyKey: ikey(), creatureId: c.id });
    // rec: { genomeId (corpse/survivor), genome, heart, itemId }
    registerName(rec.genomeId, c.name);
    registerFrom(rec.itemId, c.name);
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

async function doSew(itemId) {
  const c = selected(); if (!c?.alive) return;
  const entry = tray.find(t => t.itemId === itemId); if (!entry) return;
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
      local.selectedId = rec.genomeId;
      if (entry.item.kind === "heart") {
        if (rec.explantedHeartItemId) registerFrom(rec.explantedHeartItemId, c.name);
        logEntry(`🫀 Transplanted a ${entry.item.tier} heart into ${c.name} (${vword(rec.viability)}). Old heart back in tray.`);
      } else {
        logEntry(`🪡 Sewed the ${entry.item.family.replace(/_/g, " ")} onto ${c.name}'s ${homologOf(entry.item.family)} (${vword(rec.viability)}).`);
      }
    } else if (rec.result === "limb_rejected") {
      logEntry(`🦠 ${c.name}'s heart can't feed the new ${(entry.item.family ?? "part").replace(/_/g, " ")} — rejected. The part is still usable.`);
    } else if (rec.result === "patient_died") {
      if (rec.genomeId) { registerName(rec.genomeId, c.name); markDead(rec.genomeId); local.selectedId = rec.genomeId; }
      if (entry.item.kind === "heart") {
        logEntry(`☠️ The ${entry.item.tier} heart could not drive ${c.name}'s body — ${c.name} dies on the table.`);
      } else {
        logEntry(`☠️ The graft overloaded ${c.name}'s heart far past shock — ${c.name} dies on the table.`);
      }
    }
  } catch (e) { logEntry(`⚠️ Sew failed: ${e.message}`); }
  saveLocal(); showBusy(false); await sync();
}

async function doReset() {
  if (!confirm("Reset this Lab session? Clears names, history, and your session ID. Server genomes are NOT deleted.")) return;
  destroyRenderer(); _lastPortraitId = null;
  local = newLocal();
  logEntry("🧹 New session started. Server genomes from prior sessions are still there under their old account ID.");
  saveLocal(); await sync();
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

// The current lab's bench: this faction's creatures, minus ones cleared.
function labCreatures() {
  const hidden = new Set(local.hidden ?? []);
  return creatures.filter(c => !hidden.has(c.id) && factionOfCreature(c) === local.faction);
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
  document.getElementById("view-stable").style.display = v === "stable" ? "flex" : "none";
  document.getElementById("nav-lab").classList.toggle("active", v === "lab");
  document.getElementById("nav-stable").classList.toggle("active", v === "stable");
  destroyRenderer(); _lastPortraitId = null;   // hand the single renderer over
  if (v === "stable") renderStable(); else { applyFaction(local.faction); render(); }
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
  const lab = labCreatures();
  if (!lab.find(c => c.id === local.selectedId)) local.selectedId = lab[0]?.id ?? null;
}

// ── busy indicator ────────────────────────────────────────────────────────────
function showBusy(on) {
  document.getElementById("btn-spawn").disabled = on;
  document.getElementById("btn-reset").disabled = on;
}

// ── render ────────────────────────────────────────────────────────────────────
function render() {
  document.getElementById("wallet").textContent = `🩸 ${blood}`;
  if (local.view === "stable") return;      // the stable draws itself
  for (const fn of [renderRoster, renderActions, renderScreen, renderTray, renderLog, renderPortrait]) {
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
  const slotOpts = SLOT_NAMES.map(s => `<option value="${s}">${s}</option>`).join("");
  const dead = !c.alive;
  el.innerHTML = `
    <div class="group"><span class="lbl">Mutator</span>
      <select id="sel-feed" ${dead ? "disabled" : ""}>${feedOpts}</select>
      <button id="btn-mutate" ${dead ? "disabled" : ""}>🧬 Mutate (🩸10)</button>
      <select id="sel-partner" ${dead || !partners.length ? "disabled" : ""}>${partnerOpts || "<option>no partner</option>"}</select>
      <button id="btn-splice" ${dead || !partners.length ? "disabled" : ""}>🧪 Splice (🩸20)</button>
    </div>
    <div class="group"><span class="lbl">Chop shop</span>
      <select id="sel-slot">${slotOpts}</select>
      <button id="btn-harvest">✂️ Harvest part (🩸5)</button>
      <button id="btn-harvest-heart">💔 Harvest heart (🩸5)</button>
    </div>
    <div class="group"><span class="lbl">Specimen</span>
      <button id="btn-rename">🏷️ Name</button>
      <button id="btn-save">${isSaved(c.id) ? "★ In stable" : "⭐ Save to stable"}</button>
      <button id="btn-delete" class="danger">🗑️ Delete</button>
    </div>`;
  document.getElementById("btn-rename")?.addEventListener("click", doRename);
  document.getElementById("btn-save")?.addEventListener("click", doSaveStable);
  document.getElementById("btn-delete")?.addEventListener("click", doDelete);
  document.getElementById("btn-mutate")?.addEventListener("click", () =>
    doMutate(document.getElementById("sel-feed").value || undefined));
  document.getElementById("btn-splice")?.addEventListener("click", () =>
    doSplice(document.getElementById("sel-partner").value));
  document.getElementById("btn-harvest")?.addEventListener("click", () =>
    doHarvestPart(document.getElementById("sel-slot").value));
  document.getElementById("btn-harvest-heart")?.addEventListener("click", doHarvestHeart);
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

function renderTray() {
  const el = document.getElementById("tray");
  if (tray.length === 0) { el.innerHTML = `<div class="empty">Harvest a part to fill the tray.</div>`; return; }
  const c = selected();
  const canSew = !!c?.alive;
  el.innerHTML = tray.map(t => {
    const isHeart = t.item.kind === "heart";
    const label = isHeart
      ? `🫀 ${esc(t.item.tier)} heart`
      : `🦴 ${esc(t.item.family.replace(/_/g, " "))} <span class="badge ${originOf(t.item.family)}">${originOf(t.item.family)}</span>`;
    const target = isHeart ? "" : ` → ${homologOf(t.item.family)}`;
    return `<div class="item">
      <span class="what">${label}<br><small style="color:var(--dim)">from ${esc(t.from)}</small></span>
      <button class="small" data-item="${t.itemId}" ${canSew ? "" : "disabled"}>🪡 Sew${target} (🩸5)</button>
    </div>`;
  }).join("");
  el.querySelectorAll("button[data-item]").forEach(b =>
    b.addEventListener("click", () => doSew(b.dataset.item)));
}

function renderLog() {
  document.getElementById("log").innerHTML =
    local.log.map(l => `<p><span class="t">${l.t}</span>${esc(l.msg)}</p>`).join("") ||
    `<div class="empty">The notebook is blank.</div>`;
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
document.getElementById("nav-stable").addEventListener("click", () => setView("stable"));
local.faction ??= "maddr";
local.view ??= "lab";
applyFaction(local.faction);
document.getElementById("sel-faction").addEventListener("change", (e) => {
  local.faction = e.target.value;
  saveLocal();
  applyFaction(local.faction);
  logEntry(`🚪 Crossed over to ${e.target.selectedOptions[0].textContent.trim()}.`);
  render();
});

// Show connecting state, then sync
document.getElementById("roster").innerHTML = `<div class="empty">Connecting to Mutator…</div>`;
logEntry("🔦 The lab lights flicker on. Connecting to Mutator service…");
setView(local.view);   // establish Lab/Stable layout, then load
sync().catch(e => {
  logEntry(`⚠️ Could not reach the Mutator service: ${e.message}`);
  render();
});
