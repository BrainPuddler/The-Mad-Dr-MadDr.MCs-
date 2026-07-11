using System;

namespace MadDr.CityGen
{
    /// <summary>Attack-arc classification, docs/04 "posMod -- positioning
    /// (the centerpiece)": front x1.00, flank x1.25, rear x1.50.</summary>
    public enum Arc
    {
        Front,
        Flank,
        Rear,
    }

    public static class Facing
    {
        /// <summary>
        /// Classifies which arc <paramref name="attacker"/> is in relative to
        /// a defender at <paramref name="defender"/> facing <paramref
        /// name="defenderFacing"/> (docs/04): "front = the faced hex-edge +/-1;
        /// rear = opposite edge +/-1; the remaining two edges are flanks."
        ///
        /// Implementation note (found building this): taken completely
        /// literally, docs/04's two "+/-1" spans overlap and tile all six
        /// edges between front and rear, leaving zero edges for "the
        /// remaining two" it also promises. The only reading that satisfies
        /// BOTH constraints at once -- front spans exactly 3 edges (the faced
        /// edge and its two neighbors) AND exactly two edges remain as flank
        /// -- is rear as the single exact-opposite edge, not its own +/-1
        /// span. That's what's implemented here (Front=3 edges, Flank=2,
        /// Rear=1). Doesn't change any tuned number (posMod's x1.00/1.25/1.50
        /// are untouched) -- only which of the 6 approach directions maps to
        /// which multiplier. Worth a docs/04 wording fix, not a new open
        /// question: the ambiguity is textual, not a design decision.
        /// </summary>
        public static Arc ArcOf(HexCoord attacker, HexCoord defender, HexEdge defenderFacing)
        {
            var approachEdge = ApproachEdge(defender, attacker);
            var offset = AngularOffset(approachEdge, defenderFacing);

            switch (offset)
            {
                case 0:
                case 1:
                    return Arc.Front;
                case 3:
                    return Arc.Rear;
                default: // offset == 2
                    return Arc.Flank;
            }
        }

        /// <summary>Which of the defender's 6 edges the attacker is beyond,
        /// i.e. the direction from defender to attacker. Requires the
        /// attacker to be exactly one of the defender's 6 neighbors -- the
        /// arc model is a melee/adjacency concept (docs/04); reach-2+
        /// attackers "still *have* arcs as defenders" but this method
        /// classifies incoming attacks, not ranged geometry.</summary>
        private static HexEdge ApproachEdge(HexCoord defender, HexCoord attacker)
        {
            for (var e = 0; e < 6; e++)
            {
                if (defender.Neighbor((HexEdge)e).Equals(attacker)) return (HexEdge)e;
            }
            throw new ArgumentException("attacker is not adjacent to defender; arcs are a melee/adjacency concept (docs/04)");
        }

        /// <summary>Shortest distance around the hexagon's 6 edges between two
        /// edge indices, in [0, 3].</summary>
        private static int AngularOffset(HexEdge a, HexEdge b)
        {
            var diff = Math.Abs((int)a - (int)b) % 6;
            return Math.Min(diff, 6 - diff);
        }
    }
}
