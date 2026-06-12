/**
 * Component costs (docs/05, docs/06). v0.1 placeholder numbers -- the real
 * balance pass is Phase-2 ([12] Q-economy). The shapes are what matter:
 * mutating costs blood; graft costs 3x the mutate fee (docs/06); surgery
 * costs a small operating fee; a failed experiment is refunded most of the
 * fee (docs/06 "small component refund").
 */

import type { Wallet } from "./store.js";

export interface Cost {
  readonly blood?: number;
  readonly bones?: number;
}

export const COSTS = {
  mutate: { blood: 10 },
  splice: { blood: 20 },
  graft: { blood: 30 }, // 3x mutate fee (docs/06)
  surgery: { blood: 5 }, // harvesting/sewing operating fee
} as const satisfies Record<string, Cost>;

/** Fraction of the fee refunded when an op fails (failed experiment, or a
 * rejected/dead surgery -- you keep most of your blood and the part). */
export const REFUND_FRACTION = 0.75;

export function canAfford(w: Wallet, cost: Cost): boolean {
  return w.blood >= (cost.blood ?? 0) && w.bones >= (cost.bones ?? 0);
}

export function debit(w: Wallet, cost: Cost): Wallet {
  return { ...w, blood: w.blood - (cost.blood ?? 0), bones: w.bones - (cost.bones ?? 0) };
}

export function refund(w: Wallet, cost: Cost, fraction = REFUND_FRACTION): Wallet {
  return {
    ...w,
    blood: w.blood + Math.round((cost.blood ?? 0) * fraction),
    bones: w.bones + Math.round((cost.bones ?? 0) * fraction),
  };
}

export class InsufficientComponents extends Error {
  constructor(readonly cost: Cost) {
    super("insufficient components");
    this.name = "InsufficientComponents";
  }
}
