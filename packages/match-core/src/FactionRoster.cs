using System.Collections.Generic;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §6 / §13 amendment D's Phase 6b: "the two enemy
    /// faction rosters as genome data" -- Human Army and Alien Hive, the
    /// unit archetypes docs/23 §6 itself names ("Army: riflemen squads,
    /// the existing Tank, half-tracks, a zeppelin gunship; Hive: drones,
    /// spitters, a floater queen"). `FactionId.MadDoctor` gets no roster
    /// here at all -- the Doctor's whole identity is fielding CUSTOM bred
    /// creatures through the Mutator (docs/06), never a fixed unit list,
    /// so there is nothing for a roster table to enumerate for that
    /// faction.</summary>
    public enum RosterUnitKind
    {
        // -- Human Army (FactionId.HumanArmy) --
        Rifleman = 0,
        HalfTrack = 1,
        Tank = 2,
        ZeppelinGunship = 3,

        // -- Alien Hive (FactionId.AlienHive) --
        Drone = 4,
        Spitter = 5,
        FloaterQueen = 6,
    }

    /// <summary>Static per-roster-unit data (docs/23 §13 amendment D's own
    /// words for Phase 6b: "the two enemy faction rosters as genome
    /// data"). DATA read by the sim, never simulation state (same
    /// convention as <see cref="FactionDef"/>/<see cref="BuildingDef"/>)
    /// -- not part of the tick hash.
    ///
    /// **Every number below is an invented v0.1 placeholder, not a real
    /// figure sourced from anywhere.** docs/17-factions.md gives each
    /// faction's real BEHAVIORAL/economic design (control-snap identity,
    /// origin-tag energy/material flavors, morale math) in detail, but
    /// never a numeric <see cref="CombatStats"/>/Speed/Radius table for
    /// any individual unit archetype -- there is no docs/17 number to
    /// reuse the way <see cref="BuildingDef"/>'s <c>BloodStorage</c> reused
    /// docs/22's real Blood Bank figures. More fundamentally: docs/17 §
    /// "the bill of materials IS the genome" describes these units as
    /// PRODUCTS of genome-core's real expression math (bulk, canalized
    /// part bounds, brain tier) -- match-core has ZERO reference to
    /// genome-core (a repo invariant) and no bridge exists from
    /// TypeScript expression formulas to this C# table today. That
    /// translation (a real genome → these exact stats, so a Human
    /// Rifleman fielded in a match is actually the SAME genome docs/17
    /// describes, not a parallel hand-tuned stand-in) is a genuine,
    /// separate, not-yet-built integration job -- this table exists so
    /// Phase 6c's skirmish AI and Phase 7's balance smoke test have real
    /// units to field AT ALL, not a claim that these numbers are
    /// balanced or canon. Differentiated only by docs/17's own fictional
    /// intent (Army: durable/armored machines, ranged-leaning; Hive:
    /// cheap fast swarm + one precious mastermind, per "the Queen is the
    /// Vat").</summary>
    public sealed class UnitRosterDef
    {
        public RosterUnitKind Kind { get; }
        public FactionId Faction { get; }
        public string Name { get; }
        public double Speed { get; }
        public double Radius { get; }
        public CombatStats Combat { get; }
        public int SalvageValue { get; }

        /// <summary>2026-07 worker-economy epic, Phase 4: what a
        /// <see cref="CommandKind.TrainUnit"/> command debits from the
        /// training player's wallet, all-or-nothing (mirrors <see
        /// cref="BuildingDef.Cost"/>'s own contract exactly). v0.1
        /// placeholder numbers, same standing policy as every other cost
        /// table in this codebase -- shaped by docs/17's own fictional
        /// framing (creator direction, 2026-07): Human Army "recruiting
        /// volunteers" is cheaper per line but spans MORE resource kinds
        /// (Bones + Fuel, drilled infantry needs matériel, not genetics);
        /// Alien Hive "mind control... energy from captured" is a single,
        /// heavier Ichor cost (their one native energy currency) -- the
        /// captured-citizen-energy SOURCE itself is explicitly deferred
        /// (see docs/12's Phase 4 entry), only the Ichor SINK is real
        /// here.</summary>
        public IReadOnlyList<(ResourceKind Resource, int Amount)> Cost { get; }

        /// <summary>Ticks to train, v0.1 placeholder scaled roughly with
        /// the unit's own combat weight (a Rifleman trains fast, a
        /// Floater Queen slow) -- no real docs/17 number exists for
        /// this any more than for Cost above.</summary>
        public int TrainTimeTicks { get; }

        private UnitRosterDef(RosterUnitKind kind, FactionId faction, string name,
            double speed, double radius, CombatStats combat, int salvageValue,
            (ResourceKind, int)[] cost, int trainTimeTicks)
        {
            Kind = kind;
            Faction = faction;
            Name = name;
            Speed = speed;
            Radius = radius;
            Combat = combat;
            SalvageValue = salvageValue;
            Cost = cost;
            TrainTimeTicks = trainTimeTicks;
        }

        // Tech and biotech units have no Lumen-cycle coupling of their
        // own (docs/03's Solar/Lunar affinity is a MadDr/organic-monster
        // gene per docs/17's energy table) -- Neutral for every roster
        // unit here, same "no special aura synergy" default CombatStats
        // already uses elsewhere for non-organic test fixtures.
        private const UnitAffinity NoLumenCoupling = UnitAffinity.Neutral;

        private static readonly UnitRosterDef[] All =
        {
            // -- Human Army: durable, armored, ranged-leaning; drilled
            // morale rather than genetic surprise (docs/17: "reliable,
            // un-bred, un-counterable by genetic tricks"). --

            new UnitRosterDef(RosterUnitKind.Rifleman, FactionId.HumanArmy, "Rifleman Squad",
                speed: 3.0, radius: 1.0,
                combat: new CombatStats(maxVitality: 60, power: 8, armor: 1, reach: 3, ferocity: 1.0, cunningPercent: 5, affinity: NoLumenCoupling),
                salvageValue: 20,
                cost: new[] { (ResourceKind.Bones, 10), (ResourceKind.Fuel, 5) }, trainTimeTicks: 40),

            new UnitRosterDef(RosterUnitKind.HalfTrack, FactionId.HumanArmy, "Half-Track",
                speed: 5.0, radius: 2.0,
                combat: new CombatStats(maxVitality: 250, power: 15, armor: 4, reach: 2, ferocity: 0.8, cunningPercent: 0, affinity: NoLumenCoupling),
                salvageValue: 80,
                cost: new[] { (ResourceKind.Bones, 20), (ResourceKind.Fuel, 15) }, trainTimeTicks: 80),

            // "The existing Tank" (unity-client/Assets/Scripts/Tank.cs) --
            // this is only its match-core COMBAT stat block; the turret/
            // hull/wreck visuals it already has in Unity are untouched.
            new UnitRosterDef(RosterUnitKind.Tank, FactionId.HumanArmy, "Tank",
                speed: 2.5, radius: 2.5,
                combat: new CombatStats(maxVitality: 500, power: 30, armor: 8, reach: 3, ferocity: 0.6, cunningPercent: 0, affinity: NoLumenCoupling),
                salvageValue: 200,
                cost: new[] { (ResourceKind.Bones, 30), (ResourceKind.Fuel, 25) }, trainTimeTicks: 120),

            new UnitRosterDef(RosterUnitKind.ZeppelinGunship, FactionId.HumanArmy, "Zeppelin Gunship",
                speed: 4.0, radius: 3.0,
                combat: new CombatStats(maxVitality: 300, power: 20, armor: 3, reach: 3, ferocity: 0.8, cunningPercent: 0, affinity: NoLumenCoupling),
                salvageValue: 150,
                cost: new[] { (ResourceKind.Bones, 25), (ResourceKind.Fuel, 30) }, trainTimeTicks: 100),

            // -- Alien Hive: cheap, fast swarm plus one precious
            // mastermind (docs/17: "the Queen is the Vat" -- decapitation
            // shock -0.85, the steepest of any faction). --

            new UnitRosterDef(RosterUnitKind.Drone, FactionId.AlienHive, "Drone",
                speed: 4.0, radius: 0.8,
                combat: new CombatStats(maxVitality: 40, power: 6, armor: 0, reach: 1, ferocity: 1.5, cunningPercent: 5, affinity: NoLumenCoupling),
                salvageValue: 15,
                cost: new[] { (ResourceKind.Ichor, 15) }, trainTimeTicks: 30),

            new UnitRosterDef(RosterUnitKind.Spitter, FactionId.AlienHive, "Spitter",
                speed: 3.0, radius: 1.2,
                combat: new CombatStats(maxVitality: 80, power: 14, armor: 1, reach: 3, ferocity: 0.9, cunningPercent: 5, affinity: NoLumenCoupling),
                salvageValue: 40,
                cost: new[] { (ResourceKind.Ichor, 25) }, trainTimeTicks: 60),

            new UnitRosterDef(RosterUnitKind.FloaterQueen, FactionId.AlienHive, "Floater Queen",
                speed: 1.5, radius: 3.5,
                combat: new CombatStats(maxVitality: 800, power: 25, armor: 5, reach: 2, ferocity: 0.7, cunningPercent: 10, affinity: NoLumenCoupling),
                salvageValue: 300,
                cost: new[] { (ResourceKind.Ichor, 80) }, trainTimeTicks: 200),
        };

        public static UnitRosterDef Get(RosterUnitKind kind) => All[(int)kind];
        public static IReadOnlyList<UnitRosterDef> AllDefs => All;
    }
}
