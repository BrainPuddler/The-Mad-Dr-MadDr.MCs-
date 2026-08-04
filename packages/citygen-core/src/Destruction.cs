using System;

namespace MadDr.CityGen
{
    /// <summary>Destruction staging, docs/18 SS3: "Intact -> Damaged (<=50%
    /// HP) -> Destroyed (0 HP)." Perf-bounded authored states, not physics
    /// simulation -- explicitly not modeled here beyond which of these
    /// three stages a structure is currently in; the collapse mesh/rubble
    /// hazard/visual side is a renderer concern.</summary>
    public enum DamageStage
    {
        Intact,
        Damaged,
        Destroyed,
    }

    public static class DamageStaging
    {
        /// <summary>docs/18 SS3's threshold table, exactly: 0 HP is
        /// Destroyed; at or below half max HP is Damaged; above half is
        /// Intact.</summary>
        public static DamageStage StageFor(int currentHp, int maxHp)
        {
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (currentHp <= 0) return DamageStage.Destroyed;
            if (currentHp * 2 <= maxHp) return DamageStage.Damaged; // <=50%
            return DamageStage.Intact;
        }
    }

    /// <summary>A building's runtime HP, separate from its static
    /// <see cref="Building"/> data (footprint/tier never change; HP does).
    /// Immutable -- <see cref="ApplyDamage"/> returns a new instance, the
    /// same "operators mint new state" discipline genome-core's
    /// surgery/mutation operators use. No new combat math: whatever
    /// computed the damage amount is docs/04's existing formula,
    /// unchanged -- this class only tracks the resulting HP and derives
    /// the damage stage from it.</summary>
    public sealed class BuildingRuntimeState
    {
        public Building Building { get; }
        public int MaxHp { get; }
        public int CurrentHp { get; }

        public DamageStage Stage
        {
            get { return DamageStaging.StageFor(CurrentHp, MaxHp); }
        }

        /// <summary>Whether this building's footprint currently blocks
        /// ground movement. Intact/Damaged buildings are solid; Destroyed
        /// is walkable rubble (docs/18 SS3: "a Destroyed building removes
        /// hexes from the pathing index... can open new flank routes").</summary>
        public bool BlocksMovement
        {
            get { return Stage != DamageStage.Destroyed; }
        }

        /// <summary>2026-08 (creator direction: "assign some salvage
        /// parts based on the building size... as the lot is cleaned of
        /// parts the lot debris is decreased until it is completely
        /// cleared"): this wreck's total usable-metal value, a fixed
        /// identity stat derived from <see cref="BuildingTier"/> (see
        /// <see cref="BuildingStats.ScavengeValue"/>) -- same "size"
        /// concept <see cref="MaxHp"/> already derives from Tier.</summary>
        public int ScavengeValue
        {
            get { return BuildingStats.ScavengeValue(Building.Tier); }
        }

        /// <summary>How much of <see cref="ScavengeValue"/> is still
        /// waiting to be cleared from this wreck -- 0 while standing,
        /// set to the full <see cref="ScavengeValue"/> the instant
        /// <see cref="ApplyDamage"/> newly crosses into
        /// <see cref="DamageStage.Destroyed"/>, decremented as
        /// <see cref="WithScavengeConsumed"/> clears the pile.</summary>
        public int ScavengeRemaining { get; }

        /// <summary>True once this wreck's own pile has been fully
        /// cleared -- the gate a caller uses to decide the lot is
        /// actually available to build on again (docs/12: "until it is
        /// completely cleared and is available to build on it").</summary>
        public bool IsFullyScavenged
        {
            get { return Stage == DamageStage.Destroyed && ScavengeRemaining <= 0; }
        }

        /// <summary>The frame this building first crossed into Destroyed,
        /// or null while still standing -- same "pass the caller's own
        /// frame in" idiom the separate match-core `SimBuilding.
        /// DestroyedAtFrame` already established (citygen-core stays
        /// match-core-agnostic; this is only a same-shaped convention,
        /// not a shared type), threaded through the optional <see
        /// cref="ApplyDamage(int, int)"/> overload so a caller can gate
        /// reclaim on elapsed time as a fallback when nothing ever
        /// scavenges the pile.</summary>
        public int? DestroyedAtFrame { get; }

        private BuildingRuntimeState(Building building, int maxHp, int currentHp, int scavengeRemaining, int? destroyedAtFrame)
        {
            Building = building;
            MaxHp = maxHp;
            CurrentHp = currentHp;
            ScavengeRemaining = scavengeRemaining;
            DestroyedAtFrame = destroyedAtFrame;
        }

        public static BuildingRuntimeState FullyIntact(Building building)
        {
            var hp = BuildingStats.StructureHp(building.Tier);
            return new BuildingRuntimeState(building, hp, hp, scavengeRemaining: 0, destroyedAtFrame: null);
        }

        /// <summary>Applies damage, clamped to [0, MaxHp]. Never goes
        /// negative; never heals past its own max -- there's no repair
        /// path for buildings in v0.1 (docs/20's Repair action is
        /// creature HP only). The instant this crosses into Destroyed for
        /// the first time, rolls <see cref="ScavengeRemaining"/> up to the
        /// full <see cref="ScavengeValue"/> -- idempotent against a
        /// repeated call on an already-destroyed instance (the pile is
        /// only ever set once, never re-topped-up).</summary>
        public BuildingRuntimeState ApplyDamage(int amount)
        {
            return ApplyDamage(amount, frame: null);
        }

        /// <summary>Same as <see cref="ApplyDamage(int)"/>, additionally
        /// stamping <see cref="DestroyedAtFrame"/> with the caller's own
        /// `frame` the instant this crosses into Destroyed -- optional
        /// overload so existing callers/tests using the single-arg form
        /// are unaffected (DestroyedAtFrame just stays null for them).</summary>
        public BuildingRuntimeState ApplyDamage(int amount, int frame)
        {
            return ApplyDamage(amount, frame: (int?)frame);
        }

        private BuildingRuntimeState ApplyDamage(int amount, int? frame)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var newHp = Math.Max(0, CurrentHp - amount);
            var wasDestroyed = Stage == DamageStage.Destroyed;
            var nowDestroyed = DamageStaging.StageFor(newHp, MaxHp) == DamageStage.Destroyed;
            var newScavengeRemaining = ScavengeRemaining;
            var newDestroyedAtFrame = DestroyedAtFrame;
            if (!wasDestroyed && nowDestroyed)
            {
                newScavengeRemaining = ScavengeValue;
                newDestroyedAtFrame = frame;
            }
            return new BuildingRuntimeState(Building, MaxHp, newHp, newScavengeRemaining, newDestroyedAtFrame);
        }

        /// <summary>Deplete this wreck's own remaining pile by `amount`
        /// (clamped at 0) -- "as the lot is cleaned of parts the lot
        /// debris is decreased." No-op (returns this unchanged) if not
        /// actually Destroyed, `amount` isn't positive, or the pile is
        /// already empty -- same bad-input contract <see
        /// cref="ApplyDamage(int)"/> already follows.</summary>
        public BuildingRuntimeState WithScavengeConsumed(int amount)
        {
            if (amount <= 0 || Stage != DamageStage.Destroyed || ScavengeRemaining <= 0) return this;
            var newRemaining = Math.Max(0, ScavengeRemaining - amount);
            return new BuildingRuntimeState(Building, MaxHp, CurrentHp, newRemaining, DestroyedAtFrame);
        }
    }

    /// <summary>A bridge's runtime HP -- the same staging as a building
    /// (bridges reuse the Large tier verbatim, docs/18 terrain), but the
    /// only transition that matters to pathing is the last one: once
    /// Destroyed, the deck is gone and its hexes revert to water
    /// (<see cref="BattlefieldState"/>), not walkable rubble the way a
    /// destroyed building is.</summary>
    public sealed class BridgeRuntimeState
    {
        public Bridge Bridge { get; }
        public int MaxHp { get; }
        public int CurrentHp { get; }

        public DamageStage Stage
        {
            get { return DamageStaging.StageFor(CurrentHp, MaxHp); }
        }

        public bool IsStanding
        {
            get { return Stage != DamageStage.Destroyed; }
        }

        private BridgeRuntimeState(Bridge bridge, int maxHp, int currentHp)
        {
            Bridge = bridge;
            MaxHp = maxHp;
            CurrentHp = currentHp;
        }

        public static BridgeRuntimeState FullyIntact(Bridge bridge)
        {
            var hp = BuildingStats.StructureHp(bridge.Tier);
            return new BridgeRuntimeState(bridge, hp, hp);
        }

        public BridgeRuntimeState ApplyDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var newHp = Math.Max(0, CurrentHp - amount);
            return new BridgeRuntimeState(Bridge, MaxHp, newHp);
        }
    }
}
