/**
 * The Lab — a browser test bench for @maddr/genome-core.
 *
 * Everything runs client-side against the same compiled library the
 * Mutator service uses (vendored in ./lib). State lives in localStorage:
 * this is a test harness, not the product — the real lab is server-side
 * (packages/mutator-service) for persistence and anti-cheat.
 */

import {
  Rng, SLOT_NAMES, BODY_AXES, BRAIN_AXES,
  randomGenome, mutate, splice,
  harvestPart, harvestHeart, sewPart, sewHeart,
  upkeep, viability, heartCapacity, circulatoryLoad, partUpkeep,
  originOf, isVestigial, homologOf, brainSize, bodyAxis, brainAxis, heartVigor,
  capacity as controlCapacity, controlCost, controlRadius, berserkThreshold,
} from "./lib/index.js";
import { initRenderer, updateGenome, destroyRenderer } from "./creature-renderer.js";

const COSTS = { spawn: 0, mutate: 10, splice: 20, surgery: 5 };
const REFUND = 0.75;
const STORE_KEY = "maddr-lab-v1";

// ---- state -------------------------------------------------------------------

let state = load() ?? { creatures: [], tray: [], blood: 500, log: [], seq: 0, selected: null };

function load() {
  try { return JSON.parse(localStorage.getItem(STORE_KEY)); } catch { return null; }
}
function save() { localStorage.setItem(STORE_KEY, JSON.stringify(state)); }

function rng() {
  const s = new Uint32Array(1);
  crypto.getRandomValues(s);
  return new Rng(s[0]);
}
function nextName() {
  state.seq += 1;
  return `Specimen-${String(state.seq).padStart(2, "0")}`;
}
function byId(id) { return state.creatures.find((c) => c.id === id); }
function selected() { return state.selected ? byId(state.selected) : null; }

function log(msg) {
  const t = new Date().toLocaleTimeString();
  state.log.unshift({ t, msg });
  state.log = state.log.slice(0, 100);
}

function pay(cost, what) {
  if (state.blood < cost) { log(`🩸 Not enough blood for ${what} (need ${cost}).`); render(); return false; }
  state.blood -= cost;
  return true;
}
function refund(cost) { state.blood += Math.round(cost * REFUND); }

// ---- operations ----------------------------------------------------------------

function doSpawn() {
  const g = randomGenome(rng());
  const id = `cr-${Date.now()}-${state.seq}`;
  const name = nextName();
  state.creatures.unshift({ id, name, alive: true, genome: { ...g, creatureId: id } });
  state.selected = id;
  log(`⚡ ${name} crawls off the slab. It is ${describe(g)}.`);
  save(); render();
}

function doMutate(biasSlot) {
  const c = selected(); if (!c?.alive) return;
  if (!pay(COSTS.mutate, "Mutate")) return;
  const opts = biasSlot ? { biasSlot } : {};
  const g = mutate(c.genome, rng(), opts);
  const id = `cr-${Date.now()}-${state.seq}`;
  const name = nextName();
  state.creatures.unshift({ id, name, alive: true, genome: { ...g, creatureId: id, parentIds: [c.id] } });
  state.selected = id;
  log(`🧬 Mutated ${c.name} → ${name}${biasSlot ? ` (fed the ${biasSlot})` : ""}. It is ${describe(g)}.`);
  save(); render();
}

function doSplice(partnerId) {
  const a = selected(); const b = byId(partnerId);
  if (!a?.alive || !b?.alive) return;
  if (!pay(COSTS.splice, "Splice")) return;
  const g = splice(a.genome, b.genome, rng());
  const id = `cr-${Date.now()}-${state.seq}`;
  const name = nextName();
  state.creatures.unshift({ id, name, alive: true, genome: { ...g, creatureId: id, parentIds: [a.id, b.id] } });
  state.selected = id;
  log(`🧪 Spliced ${a.name} × ${b.name} → ${name}. It is ${describe(g)}.`);
  save(); render();
}

function doHarvestPart(slot) {
  const c = selected(); if (!c) return;
  if (isVestigial(c.genome.slots[slot].family)) { log(`✂️ ${c.name}'s ${slot} is already a stump.`); render(); return; }
  if (!pay(COSTS.surgery, "surgery")) return;
  const { donor, part } = harvestPart(c.genome, slot);
  c.genome = { ...donor, creatureId: c.id };
  state.tray.unshift({ itemId: `item-${Date.now()}`, item: part, from: c.name });
  log(`✂️ Cut the ${part.family.replace(/_/g, " ")} off ${c.name}. The ${slot} heals to a stump; the part goes in the tray.`);
  save(); render();
}

function doHarvestHeart() {
  const c = selected(); if (!c) return;
  if (!pay(COSTS.surgery, "surgery")) return;
  const { donor, heart } = harvestHeart(c.genome);
  c.genome = { ...donor, creatureId: c.id };
  state.tray.unshift({ itemId: `item-${Date.now()}`, item: heart, from: c.name });
  const v = viability(c.genome);
  if (v.state === "nonviable") {
    c.alive = false;
    log(`💔 Took the ${heart.tier} heart out of ${c.name}. The body cannot run without it — ${c.name} dies on the table. The heart is in the tray.`);
  } else {
    log(`💔 Took the ${heart.tier} heart out of ${c.name}. A faint vestige barely keeps the small body going (${v.state}).`);
  }
  save(); render();
}

function doSew(itemId) {
  const c = selected(); if (!c?.alive) return;
  const entry = state.tray.find((t) => t.itemId === itemId); if (!entry) return;
  if (!pay(COSTS.surgery, "surgery")) return;

  if (entry.item.kind === "heart") {
    const r = sewHeart(c.genome, entry.item);
    if (r.result === "survived") {
      state.tray = state.tray.filter((t) => t.itemId !== itemId);
      const old = r.explantedHeart;
      if (old) state.tray.unshift({ itemId: `item-${Date.now()}`, item: old, from: c.name });
      c.genome = { ...r.patient, creatureId: c.id };
      log(`🫀 Transplanted a ${entry.item.tier} heart into ${c.name} (${vword(r.viability)}). The old ${old?.tier ?? "?"} heart goes back in the tray.`);
    } else {
      refund(COSTS.surgery);
      c.genome = { ...r.patient, creatureId: c.id };
      c.alive = false;
      log(`☠️ The ${entry.item.tier} heart could not drive ${c.name}'s body — it never beat. ${c.name} dies on the table. The donor heart is still usable.`);
    }
  } else {
    const slot = homologOf(entry.item.family);
    const r = sewPart(c.genome, slot, entry.item);
    if (r.result === "survived") {
      state.tray = state.tray.filter((t) => t.itemId !== itemId);
      c.genome = { ...r.patient, creatureId: c.id };
      log(`🪡 Sewed the ${entry.item.family.replace(/_/g, " ")} onto ${c.name}'s ${slot} (${vword(r.viability)}). It takes.`);
    } else if (r.result === "limb_rejected") {
      refund(COSTS.surgery);
      log(`🦠 ${c.name}'s heart can't feed the new ${entry.item.family.replace(/_/g, " ")} — the limb necrotizes and is rejected (load ${r.viability.load.toFixed(1)} vs capacity ${r.viability.capacity.toFixed(1)}). The part is still usable. Try a bigger heart.`);
    } else {
      refund(COSTS.surgery);
      c.genome = { ...r.patient, creatureId: c.id };
      c.alive = false;
      log(`☠️ The graft overloaded ${c.name}'s heart far past shock — ${c.name} dies on the table, the ${entry.item.family.replace(/_/g, " ")} still attached to the corpse... and still recoverable.`);
    }
  }
  save(); render();
}

function doReset() {
  if (!confirm("Wipe the lab? All specimens, tray parts, and the notebook are lost.")) return;
  destroyRenderer(); _lastPortraitId = null;
  state = { creatures: [], tray: [], blood: 500, log: [], seq: 0, selected: null };
  log("🧹 The lab is scrubbed clean. Fresh slab, fresh blood.");
  save(); render();
}

// ---- describers ----------------------------------------------------------------

function describe(g) {
  const parts = SLOT_NAMES.filter((s) => !isVestigial(g.slots[s].family))
    .map((s) => g.slots[s].family.replace(/_/g, " "));
  return `a ${g.body.plan} (${g.brain.tier} brain, ${g.heart.tier} heart) with ${parts.join(", ") || "no parts at all"}`;
}
function vword(v) { return `load ${v.load.toFixed(1)} / capacity ${v.capacity.toFixed(1)}`; }
function pct(x) { return `${Math.round(x * 100)}%`; }
function esc(s) { return String(s).replace(/[&<>"]/g, (ch) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[ch])); }
function bar(x) { return `<span class="bar"><i style="width:${Math.round(x * 100)}%"></i></span>${pct(x)}`; }

// ---- render ----------------------------------------------------------------------

let _lastPortraitId = null;

function render() {
  document.getElementById("wallet").textContent = `🩸 ${state.blood}`;
  renderRoster(); renderActions(); renderScreen(); renderTray(); renderLog();
  renderPortrait();
}

function renderPortrait() {
  const wrap   = document.getElementById("portrait-wrap");
  const canvas = document.getElementById("portrait");
  const label  = document.getElementById("portrait-label");
  const c = selected();
  if (!c) {
    destroyRenderer();
    _lastPortraitId = null;
    wrap.style.display = "none";
    label.innerHTML = "";
    return;
  }
  wrap.style.display = "flex";
  const g = c.genome;
  const v = viability(g);
  const vClass = !c.alive ? "bad" : v.state === "viable" ? "ok" : v.state === "strained" ? "warn" : "bad";
  const parts = SLOT_NAMES
    .filter((s) => !isVestigial(g.slots[s].family))
    .map((s) => g.slots[s].family.replace(/_/g, " "))
    .join(" · ") || "—";

  label.innerHTML = `
    <div class="pl-name">${c.alive ? "" : "💀 "}${esc(c.name)}</div>
    <div class="pl-plan">${esc(g.body.plan)} · ${esc(g.brain.tier)} brain · ${esc(g.heart.tier)} heart</div>
    <div class="pl-stat ${vClass}">${c.alive ? v.state.toUpperCase() : "DEAD ON THE TABLE"}</div>
    <div class="pl-stat">load <span>${circulatoryLoad(g).toFixed(1)}</span> / cap <span>${heartCapacity(g).toFixed(1)}</span></div>
    <div class="pl-stat" style="max-width:200px;line-height:1.5">${esc(parts)}</div>`;

  if (c.id !== _lastPortraitId) {
    initRenderer(canvas, g);
    _lastPortraitId = c.id;
  } else {
    updateGenome(g);
  }
}

function renderRoster() {
  const el = document.getElementById("roster");
  if (state.creatures.length === 0) { el.innerHTML = `<div class="empty">The slab is empty.</div>`; return; }
  el.innerHTML = state.creatures.map((c) => {
    const v = viability(c.genome);
    const badge = c.alive ? `<span class="badge ${v.state}">${v.state}</span>` : `<span class="badge dead">DEAD</span>`;
    return `<div class="card ${c.id === state.selected ? "selected" : ""} ${c.alive ? "" : "dead"}" data-id="${c.id}">
      <div class="name">${c.alive ? "" : "💀 "}${esc(c.name)}${badge}</div>
      <div class="meta">${esc(c.genome.body.plan)} · ${esc(c.genome.brain.tier)} brain · ${esc(c.genome.heart.tier)} heart</div>
    </div>`;
  }).join("");
  el.querySelectorAll(".card").forEach((card) =>
    card.addEventListener("click", () => { state.selected = card.dataset.id; save(); render(); }));
}

function renderActions() {
  const el = document.getElementById("actions");
  const c = selected();
  if (!c) { el.innerHTML = ""; return; }

  const partners = state.creatures.filter((x) => x.alive && x.id !== c.id);
  const feedOpts = `<option value="">no feeding</option>` + SLOT_NAMES.map((s) => `<option value="${s}">feed the ${s}</option>`).join("");
  const partnerOpts = partners.map((p) => `<option value="${p.id}">${esc(p.name)}</option>`).join("");
  const slotOpts = SLOT_NAMES.map((s) => `<option value="${s}">${s}</option>`).join("");
  const dead = !c.alive;

  el.innerHTML = `
    <div class="group"><span class="lbl">Mutator</span>
      <select id="sel-feed" ${dead ? "disabled" : ""}>${feedOpts}</select>
      <button id="btn-mutate" ${dead ? "disabled" : ""}>🧬 Mutate (🩸${COSTS.mutate})</button>
      <select id="sel-partner" ${dead || !partners.length ? "disabled" : ""}>${partnerOpts || "<option>no partner</option>"}</select>
      <button id="btn-splice" ${dead || !partners.length ? "disabled" : ""}>🧪 Splice (🩸${COSTS.splice})</button>
    </div>
    <div class="group"><span class="lbl">Chop shop</span>
      <select id="sel-slot">${slotOpts}</select>
      <button id="btn-harvest">✂️ Harvest part (🩸${COSTS.surgery})</button>
      <button id="btn-harvest-heart">💔 Harvest heart (🩸${COSTS.surgery})</button>
    </div>`;

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
  const c = selected();
  if (!c) { el.innerHTML = `<div class="empty">Spawn a specimen, then select it.</div>`; return; }
  const g = c.genome;
  const v = viability(g);
  const u = upkeep(g);
  const vClass = !c.alive ? "bad" : v.state === "viable" ? "ok" : v.state === "strained" ? "warn" : "bad";

  const partRows = SLOT_NAMES.map((s) => {
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

  const brainRows = BRAIN_AXES.map((a) => `<div class="k">${a}</div><div>${bar(brainAxis(g.brain, a))}</div>`).join("");
  const bodyRows = BODY_AXES.map((a) => `<div class="k">${a}</div><div>${bar(bodyAxis(g.body, a))}</div>`).join("");
  const bt = berserkThreshold(g.brain);
  const parents = (g.parentIds ?? []).map((p) => esc(byId(p)?.name ?? p)).join(" × ") || "primordial (no parents)";

  el.innerHTML = `
    <h3>${c.alive ? "" : "💀 "}${esc(c.name)} — vital signs</h3>
    <div class="kv">
      <div class="k">status</div><div class="${vClass}">${c.alive ? v.state.toUpperCase() : "DEAD ON THE TABLE"}</div>
      <div class="k">heart</div><div>${esc(g.heart.tier)} (vigor ${pct(heartVigor(g.heart))}) — capacity <span class="num">${heartCapacity(g.heart).toFixed(1)}</span>/min</div>
      <div class="k">circulatory load</div><div><span class="num">${circulatoryLoad(g).toFixed(1)}</span>/min — margin <span class="${v.margin >= 0 ? "ok" : "bad"}">${v.margin >= 0 ? "+" : ""}${v.margin.toFixed(1)}</span></div>
      <div class="k">upkeep</div><div><span class="blood">${u.blood.toFixed(1)} blood</span> · <span class="fuel">${u.fuel.toFixed(1)} fuel</span> · <span class="ichor">${u.ichor.toFixed(1)} ichor</span> /min</div>
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
  if (state.tray.length === 0) { el.innerHTML = `<div class="empty">Harvest a part to fill the tray.</div>`; return; }
  const c = selected();
  const canSew = !!c?.alive;
  el.innerHTML = state.tray.map((t) => {
    const isHeart = t.item.kind === "heart";
    const label = isHeart
      ? `🫀 ${esc(t.item.tier)} heart`
      : `🦴 ${esc(t.item.family.replace(/_/g, " "))} <span class="badge ${originOf(t.item.family)}">${originOf(t.item.family)}</span>`;
    const target = isHeart ? "" : ` → ${homologOf(t.item.family)}`;
    return `<div class="item">
      <span class="what">${label}<br><small style="color:var(--dim)">from ${esc(t.from)}</small></span>
      <button class="small" data-item="${t.itemId}" ${canSew ? "" : "disabled"}>🪡 Sew${target} (🩸${COSTS.surgery})</button>
    </div>`;
  }).join("");
  el.querySelectorAll("button[data-item]").forEach((b) =>
    b.addEventListener("click", () => doSew(b.dataset.item)));
}

function renderLog() {
  document.getElementById("log").innerHTML =
    state.log.map((l) => `<p><span class="t">${l.t}</span>${esc(l.msg)}</p>`).join("") ||
    `<div class="empty">The notebook is blank.</div>`;
}

// ---- boot ------------------------------------------------------------------------

document.getElementById("btn-spawn").addEventListener("click", doSpawn);
document.getElementById("btn-reset").addEventListener("click", doReset);
if (state.log.length === 0) log("🔦 The lab lights flicker on.");
render();
