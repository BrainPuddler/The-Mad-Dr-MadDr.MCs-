using System;

namespace MadDr.MatchCore
{
    /// <summary>
    /// One player's mutable simulation state (docs/23 §1/§3/§13-E). ALL
    /// fields are integer/fixed-point so the whole thing hashes exactly:
    /// wallets are whole resource units, supply is a whole count, and the
    /// Chimera Track is a 3-bit "which origins have I salvaged" mask.
    ///
    /// Phase 1 is the skeleton: wallets and supply exist and hash. Phase 2
    /// (bases) adds construction cost debits. Phase 3 adds wallet CAPS
    /// (<see cref="RaiseWalletCap"/>, storage buildings) -- income ticks
    /// and upkeep drains (the other two things that MOVE wallets on their
    /// own, without a player action) are still pending, gated on real
    /// prerequisites (Citizens as sim entities, genome-linked per-unit
    /// cost data, the FuelNodes generator) that haven't landed yet; see
    /// docs/12's Phase 3 entry. Kept deliberately small and copyable so
    /// MatchState cloning (for rollback/serialization tests) stays cheap.
    /// </summary>
    public sealed class PlayerState
    {
        public int PlayerIndex { get; }
        public FactionId Faction { get; }

        private readonly int[] _wallet = new int[Resources.Count];

        /// <summary>docs/23 Phase 3: per-resource wallet ceiling, raised by
        /// completed storage buildings (<see cref="RaiseWalletCap"/>).
        /// Defaults to <see cref="int.MaxValue"/> -- effectively uncapped
        /// -- for every resource until the first storage building
        /// completes. This is a deliberate non-guess, not an oversight:
        /// docs/22 §6 itself says "base caps come from the Vat" but never
        /// gives that base a number (flagged there as **Q28**, still
        /// open), so match-core doesn't invent one either -- the status
        /// quo before any storage exists (uncapped) is exactly what it
        /// was before this phase, and only a REAL, docs-grounded number
        /// (a storage building's own <see cref="BuildingDef.StorageCapBonus"/>)
        /// ever changes it.</summary>
        private readonly int[] _walletCap = new int[Resources.Count];

        /// <summary>Supply currently consumed by this player's units and
        /// its cap (docs/23 §13-E: 60 base, raised by HQ + supply
        /// buildings; ~20-40 units). Phase 1 seeds the cap; units that
        /// consume it arrive with the sim port (Phase 1.5+).</summary>
        public int SupplyUsed { get; private set; }
        public int SupplyCap { get; private set; }

        /// <summary>Bitmask of origins this player has salvaged a part of
        /// (bit i = (Origin)i). The Chimera Track opens when all three are
        /// set -- docs/23 §13 amendment F, reachable even in a mono-faction
        /// 1v1 via off-origin neutral drops.</summary>
        public int SalvagedOrigins { get; private set; }

        /// <summary>docs/03 / docs/23 Phase 3.5: mana, a currency DISJOINT
        /// from the <see cref="ResourceKind"/> wallet above -- "Mana is
        /// energy... Components are material," never interchangeable
        /// (docs/03's own load-bearing framing) -- so it's its own field,
        /// not a seventh `ResourceKind`. Fixed cap (docs/03: "cap 100;
        /// overflow is lost"), unlike the wallet's per-resource caps,
        /// which start uncapped and only rise when a building completes --
        /// mana's cap is a real, given number from day one, nothing to
        /// leave open.</summary>
        public const int ManaCap = 100;
        public int Mana { get; private set; }

        /// <summary>docs/30 (selectable races + AI opponents): whether this
        /// player slot is driven by <see cref="AiMatchDriver"/> instead of
        /// external (human) input. Setup data, same category as
        /// <see cref="Faction"/> -- but unlike Faction, it has NO influence
        /// on <see cref="MatchState.Tick"/>'s own math (only on which
        /// external process decides what <see cref="Command"/>s to submit;
        /// once a command arrives, Tick processes it identically regardless
        /// of source) -- so, like <see cref="AiPersonality"/> just below,
        /// it is deliberately left OUT of <see cref="WriteTo"/>'s hash.</summary>
        public bool IsAiControlled { get; }

        /// <summary>2026-08 (docs/12 tech-wing epic, Phase 1 -- "fix real
        /// Worker gating first"): how many possessed-Worker units this
        /// player currently has, and how many of those are tied up on an
        /// in-progress construction site. Int counters, not per-entity
        /// tracking -- Worker.cs itself has no individual identity beyond
        /// existing in Unity's own `RuntimeCityBuilder.Workers` list, so
        /// this matches that same fidelity level rather than inventing
        /// entity IDs for something that doesn't have them yet.</summary>
        public int WorkerCount { get; private set; }

        /// <summary>Workers currently occupied building something --
        /// <see cref="TryOccupyWorker"/>/<see cref="ReleaseWorker"/> are
        /// the only ways this moves.</summary>
        public int BusyWorkers { get; private set; }

        /// <summary>How many Workers are free to start a NEW build right
        /// now. Never negative by construction (<see cref="RemoveWorker"/>
        /// clamps <see cref="BusyWorkers"/> down alongside <see
        /// cref="WorkerCount"/> if a Worker dies mid-build).</summary>
        public int AvailableWorkers => WorkerCount - BusyWorkers;

        /// <summary>A Worker was possessed/spawned for this player --
        /// called from <see cref="CommandKind.RegisterWorker"/>.</summary>
        public void AddWorker() => WorkerCount++;

        /// <summary>A Worker died -- called from <see
        /// cref="CommandKind.UnregisterWorker"/>. Clamps at 0 and pulls
        /// <see cref="BusyWorkers"/> down with it if needed (a Worker that
        /// dies mid-build can't leave more Workers "busy" than exist) --
        /// the building it was on keeps counting down on its own timer
        /// regardless (<see cref="SimBuilding.Tick"/> never re-checks
        /// worker availability once construction has started, only at the
        /// moment it begins), matching this system's own coarse "occupied
        /// at start, freed at the end" fidelity rather than a continuous
        /// per-tick requirement.</summary>
        public void RemoveWorker()
        {
            WorkerCount = Math.Max(0, WorkerCount - 1);
            if (BusyWorkers > WorkerCount) BusyWorkers = WorkerCount;
        }

        /// <summary>Claim one Worker for a new build if one's free; false
        /// (no state change) otherwise -- same validation-not-clamping
        /// contract as <see cref="TrySpend"/>.</summary>
        public bool TryOccupyWorker()
        {
            if (AvailableWorkers <= 0) return false;
            BusyWorkers++;
            return true;
        }

        /// <summary>Free one occupied Worker -- called when a building
        /// leaves <see cref="BuildingState.UnderConstruction"/>, whichever
        /// way (Complete OR Destroyed; see <see
        /// cref="MatchState.ApplyBuildingDamage(uint, int)"/>'s own doc
        /// comment for why destruction-mid-build needs this too). No-op,
        /// not an exception, if nothing was actually occupied -- matches
        /// this class's own "bad/redundant calls are silent no-ops"
        /// convention.</summary>
        public void ReleaseWorker()
        {
            if (BusyWorkers > 0) BusyWorkers--;
        }

        /// <summary>The AI's decision-weighting profile, or null for a
        /// human-controlled player. <see cref="CommanderPersonality"/>'s own
        /// header already documents it as "DATA, never simulation state...
        /// not part of the tick hash" -- <see cref="WriteTo"/> omits this
        /// field for the exact same reason.</summary>
        public CommanderPersonality? AiPersonality { get; }

        public PlayerState(int playerIndex, FactionId faction, int supplyCap,
            bool isAiControlled = false, CommanderPersonality? aiPersonality = null)
        {
            PlayerIndex = playerIndex;
            Faction = faction;
            SupplyCap = supplyCap;
            IsAiControlled = isAiControlled;
            AiPersonality = aiPersonality;
            for (var i = 0; i < _walletCap.Length; i++) _walletCap[i] = int.MaxValue;
        }

        public int Wallet(ResourceKind kind) => _wallet[(int)kind];

        public int WalletCap(ResourceKind kind) => _walletCap[(int)kind];

        /// <summary>Grant income, clamped at this resource's current cap
        /// (docs/23 Phase 3) -- never confiscates an existing over-cap
        /// balance retroactively (e.g. if a cap-raising building is later
        /// destroyed, or simply hasn't been built yet when the balance
        /// happened to exceed what a future cap will be); it only ever
        /// stops FUTURE income from pushing the total any higher. Docs/22
        /// §6's own Q28 leaves "does a cap apply retroactively" an open
        /// question -- this is the non-destructive reading, not a
        /// resolution of it.</summary>
        public void Grant(ResourceKind kind, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var i = (int)kind;
            // room-to-the-cap, not Math.Min(wallet+amount, cap) -- the
            // latter would DECREASE an already-over-cap wallet (e.g.
            // wallet 80, cap freshly raised to 50: Math.Min(80+amount, 50)
            // clamps down to 50, confiscating 30 that was never spent).
            // Clamping the ROOM instead means an over-cap wallet simply
            // has zero room and Grant becomes a true no-op, never a
            // decrease.
            var room = Math.Max(0, _walletCap[i] - _wallet[i]);
            _wallet[i] += Math.Min(amount, room);
        }

        /// <summary>Spend if affordable; returns false and changes nothing
        /// otherwise (never goes negative -- validation, not clamping,
        /// matching the mutator-service "validation = failed experiment"
        /// discipline).</summary>
        public bool TrySpend(ResourceKind kind, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (_wallet[(int)kind] < amount) return false;
            _wallet[(int)kind] -= amount;
            return true;
        }

        public void RaiseSupplyCap(int by) => SupplyCap += by;
        public void AddSupplyUsed(int by) => SupplyUsed += by;

        /// <summary>Raise a resource's wallet cap by `by` (docs/23 Phase
        /// 3: a storage building completing). Raise-only, matching
        /// <see cref="RaiseSupplyCap"/>'s own existing raise-only shape
        /// (no `LowerSupplyCap` exists either) -- whether destroying the
        /// building should lower it back down is docs/22 §6's own
        /// unresolved Q28, not decided here. The very first raise moves
        /// the cap from "uncapped" (<see cref="int.MaxValue"/>) to
        /// exactly `by`, not `int.MaxValue + by` (which would overflow
        /// and wrap negative) -- every raise after that accumulates
        /// normally.</summary>
        public void RaiseWalletCap(ResourceKind kind, int by)
        {
            if (by < 0) throw new ArgumentOutOfRangeException(nameof(by));
            var i = (int)kind;
            _walletCap[i] = _walletCap[i] == int.MaxValue ? by : _walletCap[i] + by;
        }

        /// <summary>Bank mana income, clamped at <see cref="ManaCap"/>
        /// (docs/03: "overflow is lost"). Unlike the wallet's
        /// <see cref="Grant"/>, a straight clamp is correct here with no
        /// "room" trick needed -- mana's cap is fixed at 100 from the
        /// start of the match, never raised later, so there's no
        /// "already-over-a-since-lowered-cap" edge case to protect
        /// against.</summary>
        public void GrantMana(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Mana = Math.Min(Mana + amount, ManaCap);
        }

        /// <summary>Spend mana if affordable; same validation-not-clamping
        /// contract as <see cref="TrySpend"/>.</summary>
        public bool TrySpendMana(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Mana < amount) return false;
            Mana -= amount;
            return true;
        }

        /// <summary>Record salvaging a part of the given origin -- sets its
        /// bit toward the Chimera unlock.</summary>
        public void RecordSalvage(Origin origin) => SalvagedOrigins |= 1 << (int)origin;

        /// <summary>The Chimera Track predicate (docs/23 §13-F): all three
        /// origins salvaged. Encoded here so Phase 1's test can pin it and
        /// later phases read one method, not a scattered bit check.</summary>
        public bool ChimeraTrackOpen => SalvagedOrigins == 0b111;

        public PlayerState Clone()
        {
            var c = new PlayerState(PlayerIndex, Faction, SupplyCap, IsAiControlled, AiPersonality)
            {
                SupplyUsed = SupplyUsed,
                SalvagedOrigins = SalvagedOrigins,
                Mana = Mana,
                WorkerCount = WorkerCount,
                BusyWorkers = BusyWorkers,
            };
            Array.Copy(_wallet, c._wallet, _wallet.Length);
            Array.Copy(_walletCap, c._walletCap, _walletCap.Length);
            return c;
        }

        /// <summary>Append this player's canonical bytes to a hasher, in a
        /// FIXED field order -- the serialization contract docs/23 §13-J
        /// requires (never reflection/JSON order, which can drift).</summary>
        public void WriteTo(FnvHash h)
        {
            h.Add(PlayerIndex);
            h.Add((int)Faction);
            for (var i = 0; i < _wallet.Length; i++) h.Add(_wallet[i]);
            for (var i = 0; i < _walletCap.Length; i++) h.Add(_walletCap[i]);
            h.Add(SupplyUsed);
            h.Add(SupplyCap);
            h.Add(SalvagedOrigins);
            h.Add(Mana);
            h.Add(WorkerCount);
            h.Add(BusyWorkers);
        }
    }
}
