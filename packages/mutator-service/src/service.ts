/**
 * MutatorService -- the server-side authority over genomes (docs/07).
 *
 * Invariants enforced here:
 *  - All mutation math runs server-side via @maddr/genome-core; clients
 *    submit requests, never genomes (anti-cheat, docs/07).
 *  - Every mutating op is idempotent by (account, idempotencyKey): a retry
 *    returns the SAME result and never double-spends (docs/07).
 *  - Every op's RNG seed is generated and logged server-side (auditable,
 *    replayable; resubmitting can't reroll).
 *  - Genomes are immutable rows; operators mint NEW genomes with parentIds
 *    lineage. Results are signed for match servers to trust.
 */

import {
  Rng,
  SLOT_NAMES,
  bonesCost,
  graft as graftOp,
  harvestHeart as harvestHeartOp,
  harvestPart as harvestPartOp,
  homologOf,
  isKnownFamily,
  mutate as mutateOp,
  randomGenome,
  sewHeart as sewHeartOp,
  sewPart as sewPartOp,
  splice as spliceOp,
  validateGenome,
  type Genome,
  type HeartItem,
  type MutateOptions,
  type Origin,
  type Params6,
  type PartItem,
  type SlotName,
} from "@maddr/genome-core";

import { genId, newServerSeed } from "./ids.js";
import { signGenome } from "./sign.js";
import { COSTS, canAfford, debit, refund, type Cost } from "./economy.js";
import {
  type InventoryItem,
  type OperationRecord,
  type OpStatus,
  type OpType,
  type Store,
  type StoredGenome,
} from "./store.js";
import { badRequest, conflict, forbidden, notFound, paymentRequired } from "./errors.js";

export interface ServiceConfig {
  /** HMAC key for genome signatures (docs/07). */
  readonly signingKey: string;
  /** Shared secret the match servers present to internal endpoints. */
  readonly internalKey: string;
  readonly menagerieLimit?: number;
}

/** What a unit of work produces, before persistence. */
interface PerformOutcome {
  readonly status: OpStatus;
  /** A genome to mint (immutably persisted), if the op produced one. */
  readonly mint?: Genome;
  /** Whether to refund part of the fee (failed experiment / failed op). */
  readonly refundOnFail?: boolean;
  /** Extra fields merged into the result envelope. */
  readonly extra?: Record<string, unknown>;
}

export class MutatorService {
  private readonly menagerieLimit: number;

  constructor(
    private readonly store: Store,
    private readonly cfg: ServiceConfig,
  ) {
    this.menagerieLimit = cfg.menagerieLimit ?? 12;
  }

  // ---- generic operation runner: idempotency + cost + audit ----------------

  private perform(
    accountId: string,
    idempotencyKey: string,
    opType: OpType,
    cost: Cost,
    body: (seed: number) => PerformOutcome,
  ): OperationRecord {
    if (!idempotencyKey) throw badRequest("idempotencyKey is required");

    const prior = this.store.getOpByKey(accountId, idempotencyKey);
    if (prior) {
      if (prior.opType !== opType) {
        throw conflict("idempotencyKey already used for a different operation");
      }
      return prior; // exactly-once: replay the stored result, no side effects
    }

    let wallet = this.store.getWallet(accountId);
    if (!canAfford(wallet, cost)) throw paymentRequired("not enough components for this operation");

    const seed = newServerSeed();
    const outcome = body(seed);

    wallet = debit(wallet, cost);
    if (outcome.status === "failed_experiment" || outcome.refundOnFail) {
      wallet = refund(wallet, cost);
    }
    this.store.saveWallet(wallet);

    let resultGenomeId: string | undefined;
    let mintedEnvelope: Record<string, unknown> = {};
    if (outcome.mint) {
      const stored = this.mint(accountId, outcome.mint);
      resultGenomeId = stored.id;
      mintedEnvelope = { genome: stored.genome, signature: stored.signature };
    }

    const record: OperationRecord = {
      id: genId("op"),
      accountId,
      opType,
      idempotencyKey,
      status: outcome.status,
      serverSeed: seed,
      ...(resultGenomeId ? { resultGenomeId } : {}),
      result: {
        opType,
        status: outcome.status,
        ...(resultGenomeId ? { genomeId: resultGenomeId } : {}),
        ...mintedEnvelope,
        ...(outcome.extra ?? {}),
      },
      createdAt: new Date().toISOString(),
    };
    this.store.putOp(record);
    return record;
  }

  /** Persist a brand-new immutable genome, stamping its id, signing it, and
   * recording any newly-seen part families in the catalog (docs/06
   * discovery). */
  private mint(accountId: string, genome: Genome): StoredGenome {
    const id = genId("cr");
    const withId: Genome = { ...genome, creatureId: id };
    const stored: StoredGenome = {
      id,
      accountId,
      genome: withId,
      signature: signGenome(withId, this.cfg.signingKey),
      createdAt: new Date().toISOString(),
    };
    this.store.putGenome(stored);
    this.store.discover(
      accountId,
      SLOT_NAMES.map((s) => withId.slots[s].family),
    );
    return stored;
  }

  private requireOwned(accountId: string, genomeId: string): StoredGenome {
    const g = this.store.getGenome(genomeId);
    if (!g) throw notFound(`creature ${genomeId} not found`);
    if (g.accountId !== accountId) throw forbidden("not your creature");
    return g;
  }

  private validatedOutcome(g: Genome): PerformOutcome {
    const v = validateGenome(g);
    if (!v.ok) {
      // a "failed experiment" (docs/06): no genome minted, fee mostly refunded
      return { status: "failed_experiment", refundOnFail: true, extra: { errors: v.errors } };
    }
    return { status: "completed", mint: g };
  }

  // ---- onboarding ----------------------------------------------------------

  /** Mint a primordial creature (no parents). Onboarding / dev seeding.
   * `origins` widens the part-family pool per faction (docs/17): the
   * Human Army spawns with tech in the mix, the Hive with biotech.
   * Unknown origin strings are dropped, and "organic" is always kept in
   * the pool — some homolog classes have no families in a lone non-organic
   * origin, and flesh is the substrate every faction builds on anyway. */
  spawn(
    accountId: string,
    idempotencyKey: string,
    opts: { plan?: string; origins?: readonly string[] } = {},
  ): OperationRecord {
    const KNOWN: readonly Origin[] = ["organic", "tech", "biotech"];
    const origins = Array.isArray(opts.origins)
      ? KNOWN.filter((o) => o === "organic" || opts.origins!.includes(o))
      : undefined;
    return this.perform(accountId, idempotencyKey, "spawn", { blood: 0 }, (seed) => {
      const g = randomGenome(new Rng(seed), {
        plan: opts.plan,
        ...(origins && origins.length > 1 ? { origins } : {}),
      });
      return { status: "completed", mint: g };
    });
  }

  // ---- the three classic operators (docs/06) -------------------------------

  mutate(
    accountId: string,
    idempotencyKey: string,
    args: { parentId: string; options?: MutateOptions },
  ): OperationRecord {
    const parent = this.requireOwned(accountId, args.parentId);
    return this.perform(accountId, idempotencyKey, "mutate", COSTS.mutate, (seed) =>
      this.validatedOutcome(mutateOp(parent.genome, new Rng(seed), args.options ?? {})),
    );
  }

  splice(
    accountId: string,
    idempotencyKey: string,
    args: { parentAId: string; parentBId: string; noise?: number },
  ): OperationRecord {
    const a = this.requireOwned(accountId, args.parentAId);
    const b = this.requireOwned(accountId, args.parentBId);
    return this.perform(accountId, idempotencyKey, "splice", COSTS.splice, (seed) =>
      this.validatedOutcome(spliceOp(a.genome, b.genome, new Rng(seed), args.noise)),
    );
  }

  graft(
    accountId: string,
    idempotencyKey: string,
    args: { parentId: string; slot: SlotName; family: string; params: Params6 },
  ): OperationRecord {
    const parent = this.requireOwned(accountId, args.parentId);
    if (!isKnownFamily(args.family)) throw badRequest(`unknown part family: ${args.family}`);
    if (homologOf(args.family) !== args.slot) {
      throw badRequest(`${args.family} does not fit the ${args.slot} slot`);
    }
    return this.perform(accountId, idempotencyKey, "graft", COSTS.graft, () =>
      this.validatedOutcome(graftOp(parent.genome, args.slot, args.family, args.params)),
    );
  }

  // ---- surgery: harvest & sew (docs/06 grafting-as-surgery) -----------------

  /** Cut a part off a creature: mints the stumped donor as a new genome and
   * drops the harvested part into the surgical tray (still usable). */
  harvestPart(
    accountId: string,
    idempotencyKey: string,
    args: { creatureId: string; slot: SlotName },
  ): OperationRecord {
    const donor = this.requireOwned(accountId, args.creatureId);
    return this.perform(accountId, idempotencyKey, "harvestPart", COSTS.surgery, () => {
      const { donor: stumped, part } = harvestPartOp(donor.genome, args.slot);
      const itemId = this.stashItem(accountId, part);
      return { status: "completed", mint: stumped, extra: { itemId, part } };
    });
  }

  harvestHeart(
    accountId: string,
    idempotencyKey: string,
    args: { creatureId: string },
  ): OperationRecord {
    const donor = this.requireOwned(accountId, args.creatureId);
    return this.perform(accountId, idempotencyKey, "harvestHeart", COSTS.surgery, () => {
      const { donor: corpse, heart } = harvestHeartOp(donor.genome);
      const itemId = this.stashItem(accountId, heart);
      return { status: "completed", mint: corpse, extra: { itemId, heart } };
    });
  }

  /** Sew a tray part onto a creature, gated by the heart. On "survived" the
   * item is consumed and a new genome is minted; on "limb_rejected"/
   * "patient_died" no genome is minted, the item stays in the tray (still
   * usable), and the fee is mostly refunded. */
  sewPart(
    accountId: string,
    idempotencyKey: string,
    args: { creatureId: string; slot: SlotName; itemId: string },
  ): OperationRecord {
    const patient = this.requireOwned(accountId, args.creatureId);
    const inv = this.store.getItem(accountId, args.itemId);
    if (!inv) throw notFound(`tray item ${args.itemId} not found`);
    if (inv.item.kind !== "part") throw badRequest("that item is not a part");
    const part = inv.item as PartItem;

    return this.perform(accountId, idempotencyKey, "sewPart", COSTS.surgery, () => {
      const r = sewPartOp(patient.genome, args.slot, part);
      if (r.result === "survived") {
        this.store.removeItem(accountId, args.itemId); // consumed into the body
        let explantedId: string | undefined;
        if (r.explantedPart) explantedId = this.stashItem(accountId, r.explantedPart);
        return {
          status: "completed",
          mint: r.patient,
          extra: {
            result: r.result,
            viability: r.viability,
            ...(explantedId ? { explantedPartItemId: explantedId } : {}),
          },
        };
      }
      // failure: the part is still in the tray; refund most of the fee
      return {
        status: "completed",
        refundOnFail: true,
        extra: { result: r.result, viability: r.viability, itemId: args.itemId },
      };
    });
  }

  sewHeart(
    accountId: string,
    idempotencyKey: string,
    args: { creatureId: string; itemId: string },
  ): OperationRecord {
    const patient = this.requireOwned(accountId, args.creatureId);
    const inv = this.store.getItem(accountId, args.itemId);
    if (!inv) throw notFound(`tray item ${args.itemId} not found`);
    if (inv.item.kind !== "heart") throw badRequest("that item is not a heart");
    const heart = inv.item as HeartItem;

    return this.perform(accountId, idempotencyKey, "sewHeart", COSTS.surgery, () => {
      const r = sewHeartOp(patient.genome, heart);
      if (r.result === "survived") {
        this.store.removeItem(accountId, args.itemId); // donor heart consumed
        let explantedId: string | undefined;
        if (r.explantedHeart) explantedId = this.stashItem(accountId, r.explantedHeart);
        return {
          status: "completed",
          mint: r.patient,
          extra: {
            result: r.result,
            viability: r.viability,
            ...(explantedId ? { explantedHeartItemId: explantedId } : {}),
          },
        };
      }
      // patient died: donor heart returns to the tray, fee mostly refunded
      return {
        status: "completed",
        refundOnFail: true,
        extra: { result: r.result, viability: r.viability, itemId: args.itemId },
      };
    });
  }

  private stashItem(accountId: string, item: PartItem | HeartItem): string {
    const itemId = genId("item");
    const stored: InventoryItem = { itemId, accountId, item };
    this.store.addItem(stored);
    return itemId;
  }

  // ---- cannibalize: scrap an owned genome for parts (docs/06 Workshop) -----

  /** Retire a genome you own and recycle it: every slot and the heart are
   * stripped into the surgical tray (100% recovery -- "nothing is wasted",
   * docs/01 -- reusing the exact harvest functions surgery already runs),
   * plus a Bones credit at 50% of the genome's own build cost (the same
   * recovery rate docs/05 already uses for corpse salvage). The genome
   * itself is never deleted (immutable rows, docs/07) -- it's marked
   * retired so it can never be Menagerie-loaded or bred from again, but
   * stays fully readable for any descendant's lineage view.
   *
   * This is the Workshop's meta-side Cannibalize. Its in-match twin --
   * recalling and dismantling a *living* fielded creature at the Vat --
   * is a real-time match-server command (docs/09, docs/20), not a
   * Mutator-service op; there is no match server in this repo yet. */
  cannibalize(
    accountId: string,
    idempotencyKey: string,
    args: { genomeId: string },
  ): OperationRecord {
    const source = this.requireOwned(accountId, args.genomeId);
    const bonesRecovered = Math.round(0.5 * bonesCost(source.genome));

    return this.perform(
      accountId,
      idempotencyKey,
      "cannibalize",
      { bones: -bonesRecovered },
      () => {
        // Checked here, inside the idempotency-gated body -- not before
        // `perform()` is called -- so a retry of the SAME key replays the
        // stored result (perform()'s own idempotency check short-circuits
        // before this ever runs) instead of tripping "already retired" on
        // a genome this exact call already retired.
        if (this.store.isRetired(source.id)) {
          throw conflict(`creature ${source.id} is already retired`);
        }
        let g = source.genome;
        const partItemIds: Record<SlotName, string> = {} as Record<SlotName, string>;
        for (const slot of SLOT_NAMES) {
          const { donor, part } = harvestPartOp(g, slot);
          partItemIds[slot] = this.stashItem(accountId, part);
          g = donor;
        }
        const { heart } = harvestHeartOp(g);
        const heartItemId = this.stashItem(accountId, heart);

        this.store.retireGenome(source.id);

        return {
          status: "completed",
          extra: { bonesRecovered, partItemIds, heartItemId },
        };
      },
    );
  }

  // ---- reads ---------------------------------------------------------------

  getCreature(accountId: string, id: string): StoredGenome {
    return this.requireOwned(accountId, id);
  }

  lineage(accountId: string, id: string): StoredGenome[] {
    const root = this.requireOwned(accountId, id);
    const out: StoredGenome[] = [];
    const seen = new Set<string>();
    const walk = (gid: string) => {
      if (seen.has(gid)) return;
      seen.add(gid);
      const node = this.store.getGenome(gid);
      if (!node || node.accountId !== accountId) return;
      out.push(node);
      for (const pid of node.genome.parentIds) walk(pid);
    };
    walk(root.id);
    return out;
  }

  listCreatures(accountId: string, cursor: string | undefined, limit = 50) {
    return this.store.listGenomes(accountId, cursor, Math.min(Math.max(limit, 1), 200));
  }

  getWallet(accountId: string) {
    return this.store.getWallet(accountId);
  }

  listTray(accountId: string) {
    return this.store.listItems(accountId);
  }

  getCatalog(accountId: string): string[] {
    return [...this.store.getCatalog(accountId)].sort();
  }

  // ---- menagerie -----------------------------------------------------------

  getMenagerie(accountId: string) {
    return this.store.getMenagerie(accountId);
  }

  setMenagerie(accountId: string, creatureIds: string[]) {
    if (creatureIds.length > this.menagerieLimit) {
      throw badRequest(`menagerie holds at most ${this.menagerieLimit} creatures`);
    }
    if (new Set(creatureIds).size !== creatureIds.length) {
      throw badRequest("menagerie has duplicate creatures");
    }
    for (const id of creatureIds) {
      this.requireOwned(accountId, id);
      if (this.store.isRetired(id)) throw badRequest(`creature ${id} is retired (cannibalized)`);
    }
    const m = { accountId, creatureIds, updatedAt: new Date().toISOString() };
    this.store.saveMenagerie(m);
    return m;
  }

  // ---- internal (match servers) --------------------------------------------

  /** Signed Menagerie genomes for the match handshake (docs/07, docs/09).
   * Gated by the shared internal key. */
  roster(internalKey: string, accountId: string) {
    if (internalKey !== this.cfg.internalKey) throw forbidden("internal endpoint");
    const m = this.store.getMenagerie(accountId);
    return m.creatureIds
      .map((id) => this.store.getGenome(id))
      .filter((g): g is StoredGenome => !!g)
      .map((g) => ({ genome: g.genome, signature: g.signature }));
  }
}
