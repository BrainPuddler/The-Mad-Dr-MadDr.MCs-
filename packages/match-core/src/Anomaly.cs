using MadDr.CityGen;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §6: "Roaming cycling power-ups... 2-4 Loose
    /// Experiments wander the neutral streets per match... cycling their
    /// aura every 20s through Damage &lt;-&gt; Speed &lt;-&gt; Regen &lt;-&gt;
    /// XP-gain." Which buff kind is showing, in that fixed order.</summary>
    public enum AnomalyBuffKind
    {
        Damage = 0,
        Speed = 1,
        Regen = 2,
        XpGain = 3,
    }

    /// <summary>docs/23 §6: a neutral, attackable map entity -- "the SC2
    /// xel'naga-tower/DotA-rune hybrid this game's streets deserve." Not a
    /// <see cref="SimUnit"/> (no owning player, no Combat stat block of its
    /// own, no facing/arc, no salvage/XP on death) -- a deliberately
    /// separate, lightweight entity kind, the same "map feature with its
    /// own runtime state, not shoehorned into the player-unit model"
    /// relationship <see cref="SimEmitter"/> already has to a Landmark.
    /// Killed via the new <see cref="CommandKind.AttackAnomaly"/> +
    /// <see cref="MatchState.TickAnomalyCombat"/>, which mirrors
    /// <see cref="MatchState.TickCombat"/>'s own resolution shape but
    /// with a flat (no-facing) posMod and no Armor.
    ///
    /// Wander movement ("drift along sidewalks, Citizen movement reuse")
    /// is explicitly NOT implemented this pass: match-core has no
    /// Citizen-as-sim-entity walker to reuse (the same missing-
    /// prerequisite gap docs/12's Phase 3 entry already logs for
    /// Citizens/upkeep) -- an anomaly sits still at its (re)spawn hex
    /// between captures. It is still fully functional as a contested,
    /// timed capture point; only the roaming is deferred.</summary>
    public sealed class SimAnomaly
    {
        public uint EntityId { get; }

        public double X { get; private set; }
        public double Z { get; private set; }

        /// <summary>docs/23 gives no HP number for an anomaly (unlike the
        /// combat-core/emitter/leveling tables, which all had explicit
        /// docs/03-04 figures) -- a flagged v0.1 placeholder, easy to
        /// retune later since nothing else derives from it.</summary>
        public const int MaxVitality = 50;

        public int Vitality { get; private set; } = MaxVitality;

        public bool IsAlive => Vitality > 0;

        /// <summary>docs/23: "cycling their aura every 20s" -- 200 ticks.</summary>
        public const int CycleTicks = 20 * MatchState.TicksPerSecond;

        /// <summary>The frame this anomaly last (re)spawned -- the aura
        /// cycle's own epoch. <see cref="CurrentBuff"/> is a PURE function
        /// of (currentFrame - SpawnFrame), the same "nothing to seed or
        /// drift out of sync" shape <see cref="LumenClock"/> already uses
        /// for the match-wide day/night cycle, just re-based per anomaly
        /// so a respawn restarts its own cycle at Damage rather than
        /// picking up wherever the global clock happens to be.</summary>
        public int SpawnFrame { get; private set; }

        internal SimAnomaly(uint entityId, double x, double z, int spawnFrame)
        {
            EntityId = entityId;
            X = x;
            Z = z;
            SpawnFrame = spawnFrame;
        }

        /// <summary>Which of the four buffs this anomaly is currently
        /// showing, at the given frame -- always well-defined (frame is
        /// never before <see cref="SpawnFrame"/>), in the fixed
        /// Damage-Speed-Regen-XpGain order docs/23 itself lists.</summary>
        public AnomalyBuffKind CurrentBuff(int currentFrame)
        {
            var elapsed = currentFrame - SpawnFrame;
            var cycle = (elapsed / CycleTicks) % 4;
            return (AnomalyBuffKind)cycle;
        }

        /// <summary>Apply combat damage (no Armor of its own -- see
        /// <see cref="MatchState.TickAnomalyCombat"/>'s own doc comment
        /// for why the caller always passes armor: 0). Clamped at exactly
        /// 0, never negative. A no-op once already at 0 -- the CALLER
        /// (<see cref="MatchState.TickAnomalyCombat"/>) is responsible for
        /// granting the capture buff and respawning the instant this
        /// reaches 0, exactly once, not on every subsequent no-op
        /// call.</summary>
        internal void ApplyDamage(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            Vitality = System.Math.Max(0, Vitality - amount);
        }

        /// <summary>docs/23: "the anomaly respawns at a random
        /// roundabout" -- full health, a new position, and its own aura
        /// cycle restarted from Damage (a documented interpretation
        /// choice; docs/23 doesn't specify whether a respawn keeps or
        /// resets cycle phase).</summary>
        internal void Respawn(double x, double z, int currentFrame)
        {
            X = x;
            Z = z;
            Vitality = MaxVitality;
            SpawnFrame = currentFrame;
        }

        public void WriteTo(FnvHash h)
        {
            h.Add(EntityId);
            h.AddBits(X);
            h.AddBits(Z);
            h.Add(Vitality);
            h.Add(SpawnFrame);
        }
    }
}
