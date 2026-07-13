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

        private BuildingRuntimeState(Building building, int maxHp, int currentHp)
        {
            Building = building;
            MaxHp = maxHp;
            CurrentHp = currentHp;
        }

        public static BuildingRuntimeState FullyIntact(Building building)
        {
            var hp = BuildingStats.StructureHp(building.Tier);
            return new BuildingRuntimeState(building, hp, hp);
        }

        /// <summary>Applies damage, clamped to [0, MaxHp]. Never goes
        /// negative; never heals past its own max -- there's no repair
        /// path for buildings in v0.1 (docs/20's Repair action is
        /// creature HP only).</summary>
        public BuildingRuntimeState ApplyDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var newHp = Math.Max(0, CurrentHp - amount);
            return new BuildingRuntimeState(Building, MaxHp, newHp);
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
