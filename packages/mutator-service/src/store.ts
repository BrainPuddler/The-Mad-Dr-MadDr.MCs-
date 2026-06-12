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
}

export type OpType =
  | "spawn"
  | "mutate"
  | "splice"
  | "graft"
  | "harvestPart"
  | "harvestHeart"
  | "sewPart"
  | "sewHeart";

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

export interface Page<T> {
  readonly items: readonly T[];
  readonly nextCursor?: string;
}

export interface Store {
  // genomes (immutable)
  putGenome(g: StoredGenome): void;
  getGenome(id: string): StoredGenome | undefined;
  listGenomes(accountId: string, cursor: string | undefined, limit: number): Page<StoredGenome>;

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
  private catalogs = new Map<string, Set<string>>();

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
  getGenome(id: string): StoredGenome | undefined {
    return this.genomes.get(id);
  }
  listGenomes(accountId: string, cursor: string | undefined, limit: number): Page<StoredGenome> {
    const all = [...this.genomes.values()]
      .filter((g) => g.accountId === accountId)
      .sort((a, b) => (a.createdAt < b.createdAt ? 1 : a.createdAt > b.createdAt ? -1 : 0));
    const start = cursor ? all.findIndex((g) => g.id === cursor) + 1 : 0;
    const items = all.slice(start, start + limit);
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
}
