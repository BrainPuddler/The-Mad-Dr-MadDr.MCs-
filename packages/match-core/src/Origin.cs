namespace MadDr.MatchCore
{
    /// <summary>The three part origins (docs/17), the axis the Chimera
    /// Track (docs/23 §1 / §13 amendment F) unlocks along. Energy follows
    /// origin: Organic→Blood, Tech→Fuel, Biotech→Ichor.</summary>
    public enum Origin
    {
        Organic = 0,
        Tech = 1,
        Biotech = 2,
    }

    /// <summary>The six match resources (docs/05 + docs/23 §3). The first
    /// three are the per-faction energy currencies; the last three are the
    /// shared construction / RPG-upgrade currencies. Wallets are held as
    /// fixed-point integers in <see cref="PlayerState"/> so the economy is
    /// part of the deterministic hash.</summary>
    public enum ResourceKind
    {
        Blood = 0,
        Fuel = 1,
        Ichor = 2,
        Bones = 3,
        Parts = 4,
        Brains = 5,
    }

    public static class Resources
    {
        public const int Count = 6;

        /// <summary>The energy currency an origin produces / spends
        /// (docs/17 invariant).</summary>
        public static ResourceKind EnergyOf(Origin origin)
        {
            switch (origin)
            {
                case Origin.Tech: return ResourceKind.Fuel;
                case Origin.Biotech: return ResourceKind.Ichor;
                default: return ResourceKind.Blood;
            }
        }
    }
}
