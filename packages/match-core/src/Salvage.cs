namespace MadDr.MatchCore
{
    /// <summary>docs/23 Phase 6a / docs/04: "every unit death drops 40-60%
    /// (uniform roll) of its construction components... lootable by either
    /// side for 15 seconds." Pure integer math -- same reasoning as
    /// <see cref="CombatMath"/>'s own header: a gameplay roll belongs in a
    /// standalone, directly-testable static class, not buried inside
    /// <see cref="MatchState"/>'s tick method.
    ///
    /// A unit's total "construction components" value is NOT derived here
    /// (match-core has no genome/Bones-cost data at all) -- the same
    /// "accept the genome-derived NUMBER as a spawn parameter" pattern
    /// already used for <see cref="SimUnit.Speed"/>/<see cref="SimUnit.
    /// Radius"/>/<see cref="CombatStats"/>: the caller (Unity's
    /// `packages/roster-client`, which already parses genomes) supplies
    /// <see cref="SimUnit.SalvageValue"/> whole at spawn time.</summary>
    public static class SalvageMath
    {
        public const int MinPercent = 40;
        public const int MaxPercent = 60;

        /// <summary>Uniform roll in [40, 60], inclusive -- 21 possible
        /// values, one <see cref="SimRng.IntRange"/> draw.</summary>
        public static int RollPercent(SimRng rng) => rng.IntRange(MaxPercent - MinPercent + 1) + MinPercent;

        /// <summary>round(totalValue * percent / 100), round-half-up,
        /// all-integer (docs/23 §0 determinism discipline -- no double
        /// anywhere in a resource-payout computation).</summary>
        public static int RollAmount(int totalValue, SimRng rng)
        {
            if (totalValue <= 0) return 0;
            var percent = RollPercent(rng);
            return (totalValue * percent + 50) / 100;
        }

        /// <summary>docs/04: "Salvaged enemy corpses occasionally (10%)
        /// yield a genome fragment revealing one part family to your
        /// Mutator catalog." Draws exactly one RNG value regardless of
        /// <see cref="RollAmount"/>'s own draw, so a corpse's total RNG
        /// footprint at death is always the same shape (percent draw,
        /// then fragment draw) -- the same "draw COUNT determinism
        /// matters" reasoning <see cref="CombatMath.ResolveDamage"/>'s own
        /// doc comment gives for crit-vs-luck.
        ///
        /// This only decides WHETHER a fragment drops, deterministically
        /// and replayably -- match-core has zero reference to genome-core
        /// or the Mutator catalog (repo invariant), so it can't say WHICH
        /// part family, whether the player already owns it, or apply the
        /// +5% stat affinity/Lab-unlock docs/23 §6 also promises. That
        /// translation is a real, separate, NOT-YET-BUILT job for whatever
        /// system reads the match transcript afterward (mutator-service or
        /// a future match-summary consumer) -- <see cref="SimUnit.
        /// YieldsGenomeFragment"/> is sim STATE for that job to build on,
        /// the same "flag the state, defer the cross-system consumer"
        /// shape <see cref="SimUnit.DeathTick"/>'s own doc comment already
        /// used for salvage itself before this phase.</summary>
        public const int GenomeFragmentChancePercent = 10;

        public static bool RollGenomeFragment(SimRng rng) => rng.IntRange(100) < GenomeFragmentChancePercent;
    }
}
