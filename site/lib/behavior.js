/**
 * Behavior expression: pure functions from brain genes to the quantities
 * the match simulation consumes (docs/16). The loyalty/rage simulation
 * itself lives in the match server, not here -- this package only defines
 * what a brain IS, deterministically.
 *
 * v0.1 numbers, validated in the prototype; tuning tables in docs/16.
 */
import { brainAxis, brainSize } from "./genome.js";
/** Control points a commander can project. */
export function capacity(brain) {
    return brainSize(brain) * (0.4 + 0.8 * brainAxis(brain, "command"));
}
/** Control points a subordinate consumes to hold. */
export function controlCost(brain) {
    return brainSize(brain) * (0.3 + 0.7 * brainAxis(brain, "will"));
}
/** Control radius in hexes; behaves like an emitter aura (docs/03). */
export function controlRadius(brain) {
    return 4 + 8 * brainAxis(brain, "command");
}
/** Rage level at which the unit goes berserk. For low fury the threshold
 * sits above the rage cap (1.0): a placid brain can NEVER berserk. */
export function berserkThreshold(brain) {
    return 1.05 - 0.6 * brainAxis(brain, "fury");
}
export function berserkPowerMult(brain) {
    return 1.3 + 0.5 * brainAxis(brain, "fury");
}
export function berserkArmorBonus(brain) {
    return 2 + 4 * brainAxis(brain, "fury");
}
/** The normalized "how much mind" number that feeds the power budget
 * matchmaking reads (docs/06, 09): bigger and more capable brains cost
 * more budget; fury DISCOUNTS (berserkers are cheap power with a blast
 * radius, docs/16). */
export function brainPowerBudget(brain) {
    const capable = 0.5 * brainAxis(brain, "command") +
        0.3 * brainAxis(brain, "will") +
        0.2 * brainAxis(brain, "guile");
    const furyDiscount = 0.25 * brainAxis(brain, "fury");
    return brainSize(brain) * (0.5 + capable - furyDiscount);
}
