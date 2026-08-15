/**
 * Storage contract for the Mutator service (docs/07 data model). The
 * service depends only on this interface; the in-memory implementation
 * here is the test/dev backend, and a Postgres implementation slots in
 * behind the same methods with no service changes.
 *
 * The cardinal rule from docs/07: **genomes are immutable rows.** Every
 * operation inserts a NEW genome; nothing is ever edited. Lineage lives in
 * the genome's own parentIds.
 */

import type { Genome, HeartItem, PartItem } from "@maddr/genome-core";

export interface StoredGenome {
  readonly id: string;
  readonly accountId: string;
  /** The genome blob, with creatureId === id. */
  readonly genome: Genome;
  readonly signature: string;
  readonly createdAt: string;
  /**
   * 2026-08 (creator direction: "use the portrait created in the lab.
   * Export that with the monster" -- the battalion-build queue in-game
   * should show the SAME image the Lab already renders per specimen,
   * not a re-derived or separately-authored one). A base64 PNG data URL
   * (`data:image/png;base64,...`), baked client-side by the Lab's own
   * WebGL renderer (site/creature-renderer.js `renderThumbnail`) --
   * this service never renders one itself (no WebGL/DOM here, and
   * shouldn't need one: the Lab already produces the exact pixels the
   * player looks at). Deliberately NOT part of the immutable genome row
   * above (docs/07's own "genomes are immutable rows" rule is about the
   * GENETIC data an operation could mutate; a display portrait is
   * neither genetic nor written by a mutation op) -- stored and updated
   * through a SEPARATE mutable side-table (see `setPortrait`/
   * `getPortrait` below, same "separate mutable side-state next to an
   * immutable row" shape `retireGenome`/`isRetired` already established
   * for genome lifecycle) so re-baking/replacing a portrait later never
   * has to pretend it minted a new genome. Optional -- a genome saved
   * before this field existed, or whose thumbnail bake failed
   * client-side, simply has none; every reader treats it as "no
   * portrait available," never a hard error.
   */
  readonly portraitPng?: string;
}

export type OpType =
  | "spawn"
  | "mutate"
  | "splice"
  | "graft"
  | "harvestPart"
  | "harvestHeart"
  | "sewPart"
  | "sewHeart"
  | "cannibalize"
  | "restore";

export type OpStatus = "completed" | "failed_experiment";

export interface OperationRecord {
  readonly id: string;
  readonly accountId: string;
  readonly opType: OpType;
  readonly idempotencyKey: string;
  readonly status: OpStatus;
  readonly serverSeed: number;
  /** The new genome this op produced, if any. */
  readonly resultGenomeId?: string;
  /** Opaque result envelope returned to the caller (and replayed verbatim
   * on an idempotent resubmit). */
  readonly result: unknown;
  readonly createdAt: string;
}

/** Concrete harvested item held in a player's surgical tray. Fungible
 * components (blood/bones) live in the Wallet; harvested parts keep their
 * exact genes, so they are stored individually. */
export interface InventoryItem {
  readonly itemId: string;
  readonly accountId: string;
  readonly item: PartItem | HeartItem;
}

export interface Wallet {
  accountId: string;
  blood: number;
  bones: number;
}

export interface Menagerie {
  accountId: string;
  creatureIds: string[];
  updatedAt: string;
}

/** A named, reusable group of creature IDs (docs/12: "Lab stable" half of
 * the battalion-grouping feature -- the in-game half assigns LIVE fielded
 * monsters to a control-group slot; this is its Lab-side counterpart, a
 * template that survives between matches and drives what a Factory can be
 * told to build). Deliberately NOT a field on Menagerie or a sub-list of
 * it -- a template can reference creatures whether or not they're
 * currently in the active ≤12-slot Menagerie, and one creature can appear
 * in many templates (or repeated within the SAME template: "3 Tetrapods +
 * 2 Winged" is a real, intended shape, mirroring how the in-game battalion
 * feature already allows multiple live clones of one genome in one
 * group). */
export interface BattalionTemplate {
  readonly id: string;
  readonly accountId: string;
  name: string;
  creatureIds: string[];
  updatedAt: string;
}

export interface Page<T> {
  readonly items: readonly T[];
  readonly nextCursor?: string;
}

export interface Store {
  // genomes (immutable)
  putGenome(g: StoredGenome): void;
  getGenome(id: string): StoredGenome | undefined;
  listGenomes(accountId: string, cursor: string | undefined, limit: number): Page<StoredGenome>;

  /** Genome lifecycle marker (docs/06 Cannibalize, docs/07): a *separate*
   * retired-set, not a field on the genome row -- the genome blob itself
   * stays untouched and immutable; only its usability state changes.
   * Retired genomes are excluded from new Mutator/Menagerie use but stay
   * fully readable for lineage/pedigree views. */
  retireGenome(id: string): void;
  isRetired(id: string): boolean;

  /** 2026-08 (creator direction: "use the portrait created in the lab.
   * Export that with the monster") -- a separate mutable side-slot per
   * genome id, same "doesn't touch the immutable row" shape as
   * retireGenome/isRetired above. `setPortrait` overwrites freely (a
   * player re-baking/re-saving the same specimen replaces the old
   * image, it doesn't accumulate versions) -- unlike the genome row
   * itself, a display portrait has no lineage/audit reason to be
   * append-only. */
  setPortrait(genomeId: string, portraitPng: string): void;
  getPortrait(genomeId: string): string | undefined;

  // operations (idempotency + audit)
  getOpByKey(accountId: string, idempotencyKey: string): OperationRecord | undefined;
  putOp(op: OperationRecord): void;

  // wallet
  getWallet(accountId: string): Wallet;
  saveWallet(w: Wallet): void;

  // surgical inventory
  addItem(item: InventoryItem): void;
  getItem(accountId: string, itemId: string): InventoryItem | undefined;
  removeItem(accountId: string, itemId: string): void;
  listItems(accountId: string): readonly InventoryItem[];

  // menagerie
  getMenagerie(accountId: string): Menagerie;
  saveMenagerie(m: Menagerie): void;

  // battalion templates (named creature groups, docs/12)
  listBattalions(accountId: string): readonly BattalionTemplate[];
  getBattalion(accountId: string, id: string): BattalionTemplate | undefined;
  saveBattalion(t: BattalionTemplate): void;
  deleteBattalion(accountId: string, id: string): void;

  // catalog discovery
  getCatalog(accountId: string): ReadonlySet<string>;
  discover(accountId: string, families: readonly string[]): void;
}

const STARTING_BLOOD = 500; // docs/05 balance is Phase-2; enough to operate

export class InMemoryStore implements Store {
  private genomes = new Map<string, StoredGenome>();
  private opsByKey = new Map<string, OperationRecord>();
  private wallets = new Map<string, Wallet>();
  private items = new Map<string, InventoryItem>();
  private menageries = new Map<string, Menagerie>();
  private battalions = new Map<string, BattalionTemplate>();
  private catalogs = new Map<string, Set<string>>();
  private retired = new Set<string>();
  private portraits = new Map<string, string>();

  private opKey(accountId: string, key: string): string {
    return `${accountId}::${key}`;
  }
  private itemKey(accountId: string, itemId: string): string {
    return `${accountId}::${itemId}`;
  }

  putGenome(g: StoredGenome): void {
    if (this.genomes.has(g.id)) throw new Error(`genome ${g.id} already exists (immutable)`);
    this.genomes.set(g.id, g);
  }

  /** Merges in a stored portrait (see `setPortrait`/`getPortrait`) if
   * one exists -- the ONE place every read path (getGenome directly,
   * listGenomes below, and every service method that calls either)
   * picks it up, so a caller never has to remember to ask for it
   * separately. A genome with no saved portrait is returned completely
   * unchanged (`portraitPng` stays absent, not `undefined`-but-present
   * -- `{...g}` with an `undefined` value would still serialize as a
   * JSON `null` field on every genome, which is worse for callers than
   * the field just not existing at all). */
  private withPortrait(g: StoredGenome): StoredGenome {
    const portraitPng = this.portraits.get(g.id);
    return portraitPng === undefined ? g : { ...g, portraitPng };
  }

  getGenome(id: string): StoredGenome | undefined {
    const g = this.genomes.get(id);
    return g ? this.withPortrait(g) : undefined;
  }
  listGenomes(accountId: string, cursor: string | undefined, limit: number): Page<StoredGenome> {
    const all = [...this.genomes.values()]
      .filter((g) => g.accountId === accountId)
      .sort((a, b) => (a.createdAt < b.createdAt ? 1 : a.createdAt > b.createdAt ? -1 : 0));
    const start = cursor ? all.findIndex((g) => g.id === cursor) + 1 : 0;
    const items = all.slice(start, start + limit).map((g) => this.withPortrait(g));
    const nextCursor = start + limit < all.length ? items[items.length - 1]?.id : undefined;
    return { items, ...(nextCursor ? { nextCursor } : {}) };
  }

  getOpByKey(accountId: string, idempotencyKey: string): OperationRecord | undefined {
    return this.opsByKey.get(this.opKey(accountId, idempotencyKey));
  }
  putOp(op: OperationRecord): void {
    this.opsByKey.set(this.opKey(op.accountId, op.idempotencyKey), op);
  }

  getWallet(accountId: string): Wallet {
    let w = this.wallets.get(accountId);
    if (!w) {
      w = { accountId, blood: STARTING_BLOOD, bones: 0 };
      this.wallets.set(accountId, w);
    }
    return { ...w };
  }
  saveWallet(w: Wallet): void {
    this.wallets.set(w.accountId, { ...w });
  }

  addItem(item: InventoryItem): void {
    this.items.set(this.itemKey(item.accountId, item.itemId), item);
  }
  getItem(accountId: string, itemId: string): InventoryItem | undefined {
    return this.items.get(this.itemKey(accountId, itemId));
  }
  removeItem(accountId: string, itemId: string): void {
    this.items.delete(this.itemKey(accountId, itemId));
  }
  listItems(accountId: string): readonly InventoryItem[] {
    return [...this.items.values()].filter((i) => i.accountId === accountId);
  }

  getMenagerie(accountId: string): Menagerie {
    let m = this.menageries.get(accountId);
    if (!m) {
      m = { accountId, creatureIds: [], updatedAt: new Date(0).toISOString() };
      this.menageries.set(accountId, m);
    }
    return { ...m, creatureIds: [...m.creatureIds] };
  }
  saveMenagerie(m: Menagerie): void {
    this.menageries.set(m.accountId, { ...m, creatureIds: [...m.creatureIds] });
  }

  listBattalions(accountId: string): readonly BattalionTemplate[] {
    return [...this.battalions.values()]
      .filter((t) => t.accountId === accountId)
      .map((t) => ({ ...t, creatureIds: [...t.creatureIds] }))
      .sort((a, b) => (a.updatedAt < b.updatedAt ? 1 : a.updatedAt > b.updatedAt ? -1 : 0));
  }
  getBattalion(accountId: string, id: string): BattalionTemplate | undefined {
    const t = this.battalions.get(id);
    if (!t || t.accountId !== accountId) return undefined;
    return { ...t, creatureIds: [...t.creatureIds] };
  }
  saveBattalion(t: BattalionTemplate): void {
    this.battalions.set(t.id, { ...t, creatureIds: [...t.creatureIds] });
  }
  deleteBattalion(accountId: string, id: string): void {
    const t = this.battalions.get(id);
    if (t && t.accountId === accountId) this.battalions.delete(id);
  }

  getCatalog(accountId: string): ReadonlySet<string> {
    return this.catalogs.get(accountId) ?? new Set();
  }
  discover(accountId: string, families: readonly string[]): void {
    let c = this.catalogs.get(accountId);
    if (!c) {
      c = new Set();
      this.catalogs.set(accountId, c);
    }
    for (const f of families) c.add(f);
  }

  retireGenome(id: string): void {
    this.retired.add(id);
  }
  isRetired(id: string): boolean {
    return this.retired.has(id);
  }

  setPortrait(genomeId: string, portraitPng: string): void {
    this.portraits.set(genomeId, portraitPng);
  }
  getPortrait(genomeId: string): string | undefined {
    return this.portraits.get(genomeId);
  }
}
