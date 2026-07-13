using System.Collections.Generic;

namespace MadDr.CityGen
{
    /// <summary>
    /// The live battlefield: a static <see cref="CityModel"/> plus every
    /// building's and bridge's current runtime HP (docs/18 SS3). Derives
    /// the two things combat/movement actually need each tick:
    /// what's currently passable, and what currently counts as high
    /// ground -- neither of which is fixed at generation time once
    /// buildings start taking damage.
    ///
    /// Immutable: <see cref="WithBuildingDamage"/>/<see
    /// cref="WithBridgeDamage"/> return a new BattlefieldState, matching
    /// the runtime-state discipline in Destruction.cs.
    /// </summary>
    public sealed class BattlefieldState
    {
        public CityModel City { get; }
        public IReadOnlyList<BuildingRuntimeState> Buildings { get; }
        public IReadOnlyList<BridgeRuntimeState> Bridges { get; }

        public BattlefieldState(
            CityModel city,
            IReadOnlyList<BuildingRuntimeState> buildings,
            IReadOnlyList<BridgeRuntimeState> bridges)
        {
            City = city;
            Buildings = buildings;
            Bridges = bridges;
        }

        /// <summary>A freshly generated city: every building and bridge
        /// starts fully Intact.</summary>
        public static BattlefieldState FreshFrom(CityModel city)
        {
            var buildings = new List<BuildingRuntimeState>();
            foreach (var b in city.Buildings) buildings.Add(BuildingRuntimeState.FullyIntact(b));

            var bridges = new List<BridgeRuntimeState>();
            foreach (var br in city.Bridges) bridges.Add(BridgeRuntimeState.FullyIntact(br));

            return new BattlefieldState(city, buildings, bridges);
        }

        public BattlefieldState WithBuildingDamage(BuildingRuntimeState updated)
        {
            var buildings = new List<BuildingRuntimeState>(Buildings);
            for (var i = 0; i < buildings.Count; i++)
            {
                if (ReferenceEquals(buildings[i].Building, updated.Building)) { buildings[i] = updated; break; }
            }
            return new BattlefieldState(City, buildings, Bridges);
        }

        public BattlefieldState WithBridgeDamage(BridgeRuntimeState updated)
        {
            var bridges = new List<BridgeRuntimeState>(Bridges);
            for (var i = 0; i < bridges.Count; i++)
            {
                if (ReferenceEquals(bridges[i].Bridge, updated.Bridge)) { bridges[i] = updated; break; }
            }
            return new BattlefieldState(City, Buildings, bridges);
        }

        /// <summary>Ground-impassable hexes right now: the static river/
        /// ponds, any DESTROYED bridge's footprint (reverted to water --
        /// docs/18 terrain: "destroy it and its hexes revert to water"),
        /// and any standing (Intact/Damaged) building's footprint.
        /// Destroyed buildings are rubble, not water -- absent from this
        /// set (docs/18 SS3: "can open new flank routes").</summary>
        public HashSet<HexCoord> BlockedToGround()
        {
            var blocked = new HashSet<HexCoord>(City.Water);
            foreach (var br in Bridges)
            {
                if (br.IsStanding) continue;
                foreach (var h in br.Bridge.Footprint) blocked.Add(h);
            }
            foreach (var b in Buildings)
            {
                if (!b.BlocksMovement) continue;
                foreach (var h in b.Building.Footprint) blocked.Add(h);
            }
            return blocked;
        }

        /// <summary>Amphibious-impassable hexes: standing buildings only.
        /// Water -- river, ponds, or a destroyed bridge's reverted
        /// footprint -- never blocks an amphibious plan (docs/04 water
        /// rule: crab and serpentine cross freely).</summary>
        public HashSet<HexCoord> BlockedToAmphibious()
        {
            var blocked = new HashSet<HexCoord>();
            foreach (var b in Buildings)
            {
                if (!b.BlocksMovement) continue;
                foreach (var h in b.Building.Footprint) blocked.Add(h);
            }
            return blocked;
        }

        /// <summary>High-ground hexes (+0.10 posMod, docs/04): generated
        /// ridges plus every building's footprint at ANY damage stage --
        /// "a destroyed building's remaining structure grants the same
        /// +0.10 posMod term" (docs/04), so this set does NOT shrink as
        /// buildings take damage. Bridges are excluded: docs never call a
        /// road deck out as elevated structure the way a building is.</summary>
        public HashSet<HexCoord> HighGround()
        {
            var hg = new HashSet<HexCoord>(City.Ridges);
            foreach (var b in Buildings)
                foreach (var h in b.Building.Footprint) hg.Add(h);
            return hg;
        }
    }
}
