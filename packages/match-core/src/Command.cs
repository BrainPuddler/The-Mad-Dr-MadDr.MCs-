namespace MadDr.MatchCore
{
    /// <summary>
    /// The command kinds a player can issue. Phase 1 defines the envelope
    /// and one no-op-ish kind (Ping, used to prove the command pipeline
    /// hashes deterministically); real orders (Move/Attack/Build/...) are
    /// added by the phases that implement them, each targeting ENTITY IDs,
    /// never object references (docs/23 §13-J).
    /// </summary>
    public enum CommandKind
    {
        None = 0,
        Ping = 1,   // Phase 1 placeholder: carries data through the pipeline, affects only the ack counter
    }

    /// <summary>
    /// One player command, scheduled for a specific tick (docs/23 §11:
    /// commands land N ticks ahead). A flat value type -- all fields
    /// integer, entity references are IDs -- so a command stream serializes
    /// and hashes byte-for-byte identically on every client. The generic
    /// arg slots (<see cref="TargetEntity"/>, <see cref="ArgA"/>,
    /// <see cref="ArgB"/>) are interpreted per <see cref="Kind"/>; later
    /// phases give them meaning without changing the envelope.
    /// </summary>
    public readonly struct Command
    {
        public readonly int PlayerIndex;
        public readonly CommandKind Kind;
        public readonly uint TargetEntity;   // an EntityId value, or 0 for none
        public readonly int ArgA;
        public readonly int ArgB;

        public Command(int playerIndex, CommandKind kind, uint targetEntity = 0, int argA = 0, int argB = 0)
        {
            PlayerIndex = playerIndex;
            Kind = kind;
            TargetEntity = targetEntity;
            ArgA = argA;
            ArgB = argB;
        }

        public void WriteTo(FnvHash h)
        {
            h.Add(PlayerIndex);
            h.Add((int)Kind);
            h.Add(TargetEntity);
            h.Add(ArgA);
            h.Add(ArgB);
        }
    }
}
