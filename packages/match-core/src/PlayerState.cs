using System;

namespace MadDr.MatchCore
{
    /// <summary>
    /// One player's mutable simulation state (docs/23 §1/§3/§13-E). ALL
    /// fields are integer/fixed-point so the whole thing hashes exactly:
    /// wallets are whole resource units, supply is a whole count, and the
    /// Chimera Track is a 3-bit "which origins have I salvaged" mask.
    ///
    /// Phase 1 is the skeleton: wallets and supply exist and hash, but
    /// income/upkeep/production that MOVE them arrive in Phase 3. Kept
    /// deliberately small and copyable so MatchState cloning (for
    /// rollback/serialization tests) stays cheap.
    /// </summary>
    public sealed class PlayerState
    {
        public int PlayerIndex { get; }
        public FactionId Faction { get; }

        private readonly int[] _wallet = new int[Resources.Count];

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
        }

        public int Wallet(ResourceKind kind) => _wallet[(int)kind];

        public void Grant(ResourceKind kind, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            _wallet[(int)kind] += amount;
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
            h.Add(SupplyUsed);
            h.Add(SupplyCap);
            h.Add(SalvagedOrigins);
        }
    }
}
