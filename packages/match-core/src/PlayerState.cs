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

        public PlayerState(int playerIndex, FactionId faction, int supplyCap)
        {
            PlayerIndex = playerIndex;
            Faction = faction;
            SupplyCap = supplyCap;
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

        /// <summary>Record salvaging a part of the given origin -- sets its
        /// bit toward the Chimera unlock.</summary>
        public void RecordSalvage(Origin origin) => SalvagedOrigins |= 1 << (int)origin;

        /// <summary>The Chimera Track predicate (docs/23 §13-F): all three
        /// origins salvaged. Encoded here so Phase 1's test can pin it and
        /// later phases read one method, not a scattered bit check.</summary>
        public bool ChimeraTrackOpen => SalvagedOrigins == 0b111;

        public PlayerState Clone()
        {
            var c = new PlayerState(PlayerIndex, Faction, SupplyCap)
            {
                SupplyUsed = SupplyUsed,
                SalvagedOrigins = SalvagedOrigins,
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
        }
    }
}
