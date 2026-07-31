using System;
using System.Collections.Generic;
using MadDr.CityGen;

namespace MadDr.MatchCore
{
    /// <summary>docs/23 §13 amendment A (Phase 1.5): a unit's order state.
    /// Deliberately just the two states the "movement + order state
    /// machine" acceptance bar needs -- Idle and MoveTo. Every later phase
    /// adds the order kinds its own slice needs (AttackUnit, Harvest,
    /// SpecialAttack, ...), the same way Unity's MonsterAgent.OrderKind
    /// grew one enum value per feature; this is that enum's sim-side
    /// twin, ported one state at a time rather than all at once.</summary>
    public enum UnitOrderKind
    {
        Idle = 0,
        MoveTo = 1,

        /// <summary>docs/23 Phase 4 (combat core, docs/04): channeling an
        /// attack against another unit (<see cref="SimUnit.AttackTargetId"/>).
        /// Set by <see cref="CommandKind.AttackUnit"/>; the attacker and
        /// target must already be within <see cref="CombatStats.Reach"/>
        /// hexes at the moment the command is applied -- there is no
        /// chase-to-range movement yet (a documented, deferred gap; see
        /// this enum's own file header for the others).</summary>
        AttackUnit = 2,

        /// <summary>docs/23 Phase 6a (docs/04): channeling a 3-second
        /// harvest of a corpse (<see cref="SimUnit.SalvageTargetId"/>). Set
        /// by <see cref="CommandKind.SalvageCorpse"/>; re-validated every
        /// tick (corpse still salvageable, still in range) the same way
        /// <see cref="AttackUnit"/>'s own Reach is -- an invalidated
        /// channel simply cancels back to Idle rather than erroring.</summary>
        Salvaging = 3,

        /// <summary>docs/23 §6: attacking a <see cref="SimAnomaly"/>
        /// (<see cref="SimUnit.AttackAnomalyTargetId"/>). Set by
        /// <see cref="CommandKind.AttackAnomaly"/>; resolved by
        /// <see cref="MatchState.TickAnomalyCombat"/>, a separate loop
        /// from <see cref="AttackUnit"/>'s own -- an anomaly isn't a
        /// <see cref="SimUnit"/> at all.</summary>
        AttackAnomaly = 4,

        /// <summary>2026-07 (creator direction: "Building need decent
        /// amount of HPs and should show damage and some low-poly fire
        /// when being attacked"): channeling an attack against a <see
        /// cref="SimBuilding"/> (<see cref="SimUnit.
        /// AttackBuildingTargetId"/>). Set by <see cref="CommandKind.
        /// AttackBuilding"/>; resolved by <see cref="MatchState.
        /// TickBuildingCombat"/>, a separate loop from <see
        /// cref="AttackUnit"/>'s own -- a building has no facing/arc/
        /// Level/XP of its own, the same reasoning <see cref="AttackAnomaly"/>
        /// already established for anomalies.</summary>
        AttackBuilding = 5,
    }

    /// <summary>
    /// One deterministic battlefield entity's movement + order state
    /// (docs/23 §13-A: the first slice of the sim-porting workstream).
    /// Position is a double (X, Z) world-space pair -- not fixed-point --
    /// but every operation on it is restricted to the §0 float-discipline
    /// allow-list (+, -, *, /, sqrt; no Sin/Cos/Atan2/Exp in the tick
    /// path), and it is hashed bitwise via <see cref="FnvHash.AddBits"/>,
    /// never through ToString/JSON.
    ///
    /// This is the sim-side analog of Unity's <c>MonsterAgent</c> order
    /// state machine (docs/23 CLAUDE.md: "the unit sim is being ported
    /// here out of the frame-driven Unity MonoBehaviours... do not add
    /// gameplay decisions to MonsterAgent.Update()") -- but it does NOT
    /// yet replace MonsterAgent in Unity. That view-layer rewrite
    /// ("MonsterAgent renders interpolated sim state only, no
    /// Time.deltaTime-driven gameplay left") is Phase 1.5's other half
    /// and is deliberately NOT attempted in this same pass: gutting a
    /// ~950-line file that ten already-shipped phases of combat/economy/
    /// special-attacks logic depend on, with no Unity Editor available to
    /// visually verify the result, is exactly the kind of large,
    /// hard-to-verify change this project's "never claim visual
    /// verification" discipline warns against doing blind. Flagged here
    /// and in docs/12/docs/23, not hidden.
    /// </summary>
    public sealed class SimUnit
    {
        public uint EntityId { get; }
        public int PlayerIndex { get; }
        public double Speed { get; }   // m/s, IEEE-exact ops only

        /// <summary>2026-07 Mixed-faction amendment (docs/23 §13 amendment
        /// G): this unit's OWN race for <see cref="FactionLumenTable"/>
        /// lookups, overriding its owner's <see cref="PlayerState.Faction"/>.
        /// Null for every unit spawned by a non-Mixed player (every
        /// existing call site, unaffected -- same backward-compatible-
        /// default pattern <see cref="Combat"/> already uses) AND for a
        /// Mixed player's own pure-movement test spawns that don't care.
        /// Set at spawn time only -- a unit's race never changes mid-life,
        /// same as <see cref="Radius"/>.</summary>
        public FactionId? RaceOverride { get; }

        /// <summary>docs/27 Phase C: body half-width for
        /// <see cref="Flocking.Separate"/> -- the sim-side twin of
        /// Unity's <c>UnitCombat.Radius</c>. Fixed for the unit's whole
        /// lifetime (no genome/growth system sim-side yet), so it's set
        /// once at spawn, not mutated by Tick.</summary>
        public double Radius { get; }

        public double X { get; private set; }
        public double Z { get; private set; }
        public UnitOrderKind Order { get; private set; } = UnitOrderKind.Idle;

        /// <summary>docs/23 Phase 4 (combat core): this unit's genome-derived
        /// combat stat block, or null for a pure-movement unit (every
        /// existing `SpawnUnit` call site, unaffected -- same
        /// backward-compatible-default pattern <see cref="Radius"/> used).</summary>
        public CombatStats? Combat { get; }

        /// <summary>Current hit points -- only meaningful when
        /// <see cref="Combat"/> is set. Starts at
        /// <see cref="CombatStats.MaxVitality"/>.</summary>
        public int Vitality { get; private set; }

        /// <summary>A unit with no <see cref="Combat"/> stats can never
        /// die (it isn't a combatant at all) -- vacuously true, matching
        /// how a pure-movement unit has no HP to track in the first
        /// place.</summary>
        public bool IsAlive => Combat == null || Vitality > 0;

        /// <summary>docs/23 §4 "RPG layer": career-cumulative XP, never
        /// decreasing. Only meaningful for a combat-capable unit -- "every
        /// unit is a character," but only fighting units earn XP at all.
        /// Carried here (`UnitRuntime`'s sim-side home, docs/23's own
        /// words: "not the genome -- the genome is nature, the level is
        /// nurture").</summary>
        public int XP { get; private set; }

        /// <summary>1-10 (docs/23 §4), derived from <see cref="XP"/> via
        /// <see cref="UnitLeveling.LevelForXp"/> -- never stored/set
        /// independently, so it can never drift from the XP total that
        /// produced it.</summary>
        public int Level => Combat == null ? 1 : UnitLeveling.LevelForXp(XP);

        /// <summary>docs/23 §4: base <see cref="CombatStats.MaxVitality"/>
        /// scaled by this unit's level bonus (+8%/level, linear -- see
        /// <see cref="UnitLeveling.StatMultiplier"/>'s own doc comment).
        /// The genome-derived base stat itself never changes; only the
        /// EFFECTIVE number combat/movement math actually uses does.</summary>
        public int EffectiveMaxVitality => Combat == null ? 0 :
            (int)System.Math.Round(Combat.Value.MaxVitality * UnitLeveling.StatMultiplier(Level, UnitLeveling.MaxVitalityPerLevel));

        /// <summary>docs/23 §4: base Power scaled by level (+4%/level),
        /// THEN docs/23 §6's Damage anomaly buff (+25%, a flagged v0.1
        /// placeholder -- docs/23 names the four buff kinds but gives no
        /// magnitude for any of them, unlike every other numbered table
        /// this project ships against) while <see cref="ActiveBuff"/> is
        /// <see cref="AnomalyBuffKind.Damage"/>.</summary>
        public int EffectivePower
        {
            get
            {
                if (Combat == null) return 0;
                var leveled = Combat.Value.Power * UnitLeveling.StatMultiplier(Level, UnitLeveling.PowerPerLevel);
                if (ActiveBuff == AnomalyBuffKind.Damage) leveled *= AnomalyBuffDamageMultiplier;
                return (int)System.Math.Round(leveled);
            }
        }

        /// <summary>docs/23 §4: base movement Speed scaled by level
        /// (+2%/level) -- this is what <see cref="Tick"/>'s movement math
        /// actually consumes, not the raw <see cref="Speed"/> field
        /// (kept as the genome-derived base, same "never mutate the
        /// identity stat" convention as <see cref="Combat"/> itself) --
        /// THEN docs/23 §6's Speed anomaly buff (+25%, same flagged
        /// placeholder magnitude as <see cref="EffectivePower"/>'s own
        /// Damage buff) while <see cref="ActiveBuff"/> is
        /// <see cref="AnomalyBuffKind.Speed"/>.</summary>
        public double EffectiveSpeed
        {
            get
            {
                var leveled = Combat == null ? Speed : Speed * UnitLeveling.StatMultiplier(Level, UnitLeveling.SpeedPerLevel);
                return ActiveBuff == AnomalyBuffKind.Speed ? leveled * AnomalyBuffSpeedMultiplier : leveled;
            }
        }

        /// <summary>The frame this unit died, or null if still alive.
        /// Drives <see cref="IsSalvageable"/>'s 15s window (docs/04:
        /// "lootable by either side for 15 seconds, then the remains sink
        /// into the ground"). docs/23 Phase 6a built the actual resource
        /// payout and harvest command on top of this state -- see
        /// <see cref="SalvageValue"/>/<see cref="SalvageRemaining"/>/
        /// <see cref="SalvageTargetId"/>.</summary>
        public int? DeathTick { get; private set; }

        /// <summary>docs/04: a corpse is lootable for 15s (150 ticks)
        /// after death, then it "sinks into the ground." False for a
        /// living unit.</summary>
        public bool IsSalvageable(int currentFrame) => DeathTick.HasValue && currentFrame - DeathTick.Value < SalvageWindowTicks;

        public const int SalvageWindowTicks = 15 * MatchState.TicksPerSecond;

        /// <summary>docs/23 Phase 6a / docs/04: this unit's total
        /// "construction components" value, supplied whole by the caller
        /// at spawn time -- match-core never derives it (same "accept the
        /// genome-derived NUMBER, stay genome-agnostic" pattern as
        /// <see cref="Speed"/>/<see cref="Radius"/>/<see cref="Combat"/>).
        /// 0 for every pre-Phase-6a spawn call (the existing default),
        /// meaning nothing drops on death -- not an error, just an empty
        /// corpse.</summary>
        public int SalvageValue { get; }

        /// <summary>docs/06's `regeneration` quirk: "a passive, free, 1%
        /// max-HP/s trickle, out-of-combat only, and gene-dependent (not
        /// every creature has it)." Supplied whole by the caller at spawn
        /// time (an optional bool, default false) -- same genome-agnostic
        /// pattern as every other stat this class accepts rather than
        /// derives. docs/23 §7's Doctor regen swing (-10% Day / +15%
        /// Night) MODIFIES this quirk's own rate; it invents no baseline
        /// of its own for a unit that lacks the quirk (0%/s x anything is
        /// still 0%/s) -- see <see cref="MatchState.
        /// ApplyRegenerationQuirk"/>.</summary>
        public bool HasRegenerationQuirk { get; }

        /// <summary>The frame this unit last dealt or received damage, or
        /// null if it never has. Drives docs/06's "out of combat only"
        /// gate for <see cref="HasRegenerationQuirk"/> -- stamped at the
        /// two existing points combat already resolves
        /// (<see cref="ApplyDamage"/> for the receiving side,
        /// <see cref="ResetAttackCooldown"/> for the dealing side), not a
        /// new call site MatchState has to remember to hit.</summary>
        public int? LastCombatFrame { get; private set; }

        /// <summary>How much of <see cref="SalvageValue"/> is still
        /// waiting to be looted from this corpse -- 0 while alive, rolled
        /// once at death (<see cref="SalvageMath.RollAmount"/>, 40-60%),
        /// decremented to 0 the instant a harvest channel completes
        /// (docs/04 pays out the whole remaining pile in one channel,
        /// not a partial-harvest system).</summary>
        public int SalvageRemaining { get; private set; }

        /// <summary>docs/04: "occasionally (10%) yield a genome fragment."
        /// Rolled once at death alongside <see cref="SalvageRemaining"/>
        /// (see <see cref="SalvageMath.RollGenomeFragment"/>'s own doc
        /// comment for why match-core stops at deciding THIS boolean and
        /// goes no further).</summary>
        public bool YieldsGenomeFragment { get; private set; }

        private bool _salvageRolled;

        /// <summary>Roll this corpse's loot, exactly once (idempotent
        /// against an accidental second call -- there is only one death
        /// per unit, but defensive no-op costs nothing, same style as
        /// <see cref="ApplyDamage"/>'s own guards). Called by
        /// <see cref="MatchState.TickCombat"/> the instant
        /// <see cref="IsAlive"/> goes false.</summary>
        internal void RollSalvage(SimRng rng)
        {
            if (_salvageRolled) return;
            _salvageRolled = true;
            SalvageRemaining = SalvageMath.RollAmount(SalvageValue, rng);
            YieldsGenomeFragment = SalvageMath.RollGenomeFragment(rng);
        }

        /// <summary>docs/04: "harvesting a corpse is a 3-second channel."</summary>
        public const double SalvageChannelSeconds = 3.0;

        /// <summary>Who this unit is channeling a harvest against, while
        /// <see cref="Order"/> is <see cref="UnitOrderKind.Salvaging"/>.</summary>
        public uint? SalvageTargetId { get; private set; }

        private double _salvageChannelRemainingSeconds;

        /// <summary>Start (or restart) harvesting a corpse -- always resets
        /// the channel to the full 3s, even if re-issued against the same
        /// target (a v0.1 simplification: re-clicking harvest restarts
        /// progress, matching how re-issuing most orders in this project
        /// restarts rather than resumes).</summary>
        internal void BeginSalvaging(uint corpseId)
        {
            SalvageTargetId = corpseId;
            _salvageChannelRemainingSeconds = SalvageChannelSeconds;
            Order = UnitOrderKind.Salvaging;
        }

        /// <summary>Abort an in-progress harvest (target died a second
        /// time -- impossible --, decayed past its window, or walked out
        /// of range). No-op if not currently salvaging.</summary>
        internal void CancelSalvaging()
        {
            if (Order != UnitOrderKind.Salvaging) return;
            Order = UnitOrderKind.Idle;
            SalvageTargetId = null;
            _salvageChannelRemainingSeconds = 0.0;
        }

        /// <summary>Advance this tick's worth of channel progress. Returns
        /// true the instant the channel completes (caller pays out and
        /// calls <see cref="CompleteSalvaging"/> that same tick) -- only
        /// ever called by <see cref="MatchState.TickSalvage"/>, and only
        /// while <see cref="Order"/> is already confirmed
        /// <see cref="UnitOrderKind.Salvaging"/> and in-range, the same
        /// division of labor <see cref="MatchState.TickCombat"/> uses for
        /// <see cref="ApplyDamage"/>.</summary>
        internal bool TickSalvageChannel(double dt)
        {
            _salvageChannelRemainingSeconds -= dt;
            return _salvageChannelRemainingSeconds <= 0.0;
        }

        /// <summary>Called on the HARVESTER once payout completes.</summary>
        internal void CompleteSalvaging()
        {
            Order = UnitOrderKind.Idle;
            SalvageTargetId = null;
            _salvageChannelRemainingSeconds = 0.0;
        }

        /// <summary>Called on the CORPSE once its pile has been paid out.</summary>
        internal void ConsumeSalvage() => SalvageRemaining = 0;

        /// <summary>Which of this unit's 6 hex edges it currently faces --
        /// feeds <see cref="MadDr.CityGen.Facing.ArcOf"/> when someone
        /// attacks it. Deliberately simplified for this pass: docs/04's
        /// real turn-time cost (`turnTime = 0.15s x sizeClass` per edge,
        /// "this is what makes flanking real") needs a sizeClass stat
        /// this unit doesn't carry yet, so facing here snaps INSTANTLY to
        /// face whatever this unit last attacked (see
        /// <see cref="FaceToward"/>) rather than turning over time or
        /// tracking movement direction. A unit that's busy fighting one
        /// attacker still reads as vulnerable to a flanker who didn't
        /// just get attacked back, which preserves the mechanic's main
        /// point (flanking a distracted defender) even with the time-cost
        /// simplified away. Flagged, not silently dropped.</summary>
        public HexEdge FacingEdge { get; private set; } = HexEdge.E;

        internal void FaceToward(HexEdge edge) => FacingEdge = edge;

        /// <summary>Who this unit is channeling an attack against, while
        /// <see cref="Order"/> is <see cref="UnitOrderKind.AttackUnit"/>.</summary>
        public uint? AttackTargetId { get; private set; }

        /// <summary>Which <see cref="SimAnomaly"/> this unit is attacking,
        /// while <see cref="Order"/> is
        /// <see cref="UnitOrderKind.AttackAnomaly"/>.</summary>
        public uint? AttackAnomalyTargetId { get; private set; }

        /// <summary>Which <see cref="SimBuilding"/> this unit is
        /// attacking, while <see cref="Order"/> is <see
        /// cref="UnitOrderKind.AttackBuilding"/>.</summary>
        public uint? AttackBuildingTargetId { get; private set; }

        /// <summary>Seconds until this unit's next attack may resolve
        /// (docs/04: Ferocity is attacks/second). Counts down every tick
        /// regardless of order, so switching targets doesn't grant a free
        /// instant attack. Shared by <see cref="UnitOrderKind.AttackUnit"/>
        /// and <see cref="UnitOrderKind.AttackAnomaly"/> -- one Ferocity
        /// stat gates a unit's attack rate regardless of what it's
        /// swinging at.</summary>
        private double _attackCooldownSeconds;

        /// <summary>docs/23 §6: which anomaly buff (if any) this unit
        /// currently carries, captured from a Loose Experiment's killing
        /// blow. Buffs don't stack -- capturing a second anomaly while one
        /// is already active simply replaces it, the same "no stacking"
        /// convention docs/03's emitter auras already established.</summary>
        public AnomalyBuffKind? ActiveBuff { get; private set; }

        private double _buffRemainingSeconds;

        /// <summary>docs/23 §6: "the buff attaches to the killing unit for
        /// 90s" -- the one duration number docs/23 actually gives (unlike
        /// the four buffs' own magnitudes, all flagged v0.1 placeholders --
        /// see <see cref="EffectivePower"/>/<see cref="EffectiveSpeed"/>/
        /// <see cref="AnomalyRegenPercentPerSecond"/>/
        /// <see cref="AnomalyXpGainMultiplier"/>'s own doc comments).</summary>
        public const double AnomalyBuffDurationSeconds = 90.0;

        public const double AnomalyBuffDamageMultiplier = 1.25;
        public const double AnomalyBuffSpeedMultiplier = 1.25;
        public const double AnomalyRegenPercentPerSecond = 5.0;
        public const double AnomalyXpGainMultiplier = 1.5;

        /// <summary>Grant this unit a fresh 90s anomaly buff, called by
        /// <see cref="MatchState.TickAnomalyCombat"/> the instant a Loose
        /// Experiment's Vitality reaches 0.</summary>
        internal void ApplyAnomalyBuff(AnomalyBuffKind kind)
        {
            ActiveBuff = kind;
            _buffRemainingSeconds = AnomalyBuffDurationSeconds;
        }

        /// <summary>docs/23 §6's Regen buff -- a flat heal-over-time,
        /// granted once per simulated second by
        /// <see cref="MatchState.ApplyAnomalyBuffRegen"/> (the same "once
        /// per simulated second, exact not fractional" idiom
        /// <see cref="MatchState.GrantEmitterManaIncome"/> already uses
        /// for mana), capped at <see cref="EffectiveMaxVitality"/>. A
        /// no-op for a dead or non-combat unit.</summary>
        internal void Heal(int amount)
        {
            if (Combat == null || !IsAlive || amount <= 0) return;
            Vitality = System.Math.Min(EffectiveMaxVitality, Vitality + amount);
        }

        private List<HexCoord>? _path;
        private int _pathIndex;

        /// <summary>docs/27 Phase B: waypoints queued behind the CURRENT
        /// path, walked one at a time in FIFO order once each leg
        /// completes -- the sim-side twin of Unity's `_waypoints` queue
        /// on `MonsterAgent`. `Queue&lt;T&gt;` enumerates in insertion
        /// order (a documented guarantee, unlike Dictionary/HashSet), so
        /// hashing it is safe and deterministic. `MatchState` (not
        /// `SimUnit`) computes the actual path for a dequeued waypoint --
        /// this class stays pathfinding-agnostic, matching how it never
        /// pathfound for `SetPath` either.</summary>
        private readonly Queue<HexCoord> _waypointQueue = new Queue<HexCoord>();

        internal SimUnit(uint entityId, int playerIndex, double x, double z, double speed, double radius, CombatStats? combat, int salvageValue = 0, bool hasRegenerationQuirk = false, FactionId? raceOverride = null)
        {
            EntityId = entityId;
            PlayerIndex = playerIndex;
            X = x;
            Z = z;
            Speed = speed;
            Radius = radius;
            Combat = combat;
            Vitality = combat?.MaxVitality ?? 0;
            SalvageValue = salvageValue;
            HasRegenerationQuirk = hasRegenerationQuirk;
            RaceOverride = raceOverride;
        }

        /// <summary>Begin walking a precomputed path (HexPathfinder output,
        /// already deterministic and blocked-set-aware). An empty/null
        /// path is treated as "arrived already" -- Idle, no-op.</summary>
        internal void SetPath(List<HexCoord>? path)
        {
            _path = path != null && path.Count > 0 ? path : null;
            _pathIndex = 0;
            Order = _path != null ? UnitOrderKind.MoveTo : UnitOrderKind.Idle;
        }

        /// <summary>docs/23 §9 hardening audit: undo this tick's movement
        /// outright and drop the now-suspect path. `HexPathfinder` only
        /// ever hands out OPEN path nodes, and straight-line interpolation
        /// between two ADJACENT open hex centers is geometrically
        /// confined to those two hexes' own Voronoi cells (it can never
        /// stray into a third) -- an invariant that quietly assumes this
        /// unit's position sits exactly on that line. It doesn't always:
        /// <see cref="MatchState.ApplySeparationPass"/> validates its OWN
        /// nudge's destination every tick, but a unit can still
        /// accumulate many individually-valid separation nudges that
        /// laterally drift it OFF its path's assumed centerline over
        /// time, until this method's own straight-line step toward the
        /// next path node -- taken from that drifted position, not the
        /// centerline -- clips a hex neither system ever explicitly
        /// checked. Found by docs/23 Phase 9's own 200-unit fuzz harness
        /// (a unit slowly nudged across a hex boundary this way, tick
        /// 2004 of a 100k-tick run) -- not a hypothetical. Caller
        /// (<see cref="MatchState.Tick"/>) re-validates the hex after
        /// every regular movement step and calls this the instant it's
        /// ever wrong: a unit stalled for one tick is a far smaller cost
        /// than tunneling through solid ground.</summary>
        internal void RevertToSafePosition(double x, double z)
        {
            X = x;
            Z = z;
            Order = UnitOrderKind.Idle;
            _path = null;
        }

        /// <summary>Advance one fixed tick's worth of movement. Consumes
        /// this tick's whole speed*dt budget across as many path nodes as
        /// it covers (never leaves a fractional-tick's motion on the
        /// table) so results don't depend on how finely a path happens to
        /// be subdivided relative to speed -- same idiom as Unity's
        /// FollowPath, ported to fixed dt instead of Time.deltaTime.
        /// <paramref name="speedMultiplier"/> (docs/23 §7's Lumen-cycle
        /// faction modifier) defaults to 1.0 -- every pre-Phase-7 caller
        /// keeps compiling and moving at the identical rate it always did.
        /// Computed by <see cref="MatchState"/> (which alone knows this
        /// unit's owning Faction and the current <see cref="LumenPhase"/>)
        /// and passed in fresh each tick, the same "SimUnit stays
        /// self-contained, MatchState supplies external context" division
        /// of labor <see cref="ApplySeparationOffset"/> already
        /// uses.</summary>
        internal void Tick(double dt, double speedMultiplier = 1.0)
        {
            // docs/23 Phase 4: cooldown counts down regardless of order,
            // so switching targets (or briefly moving) never grants a
            // free instant attack the moment AttackUnit resumes.
            if (_attackCooldownSeconds > 0.0) _attackCooldownSeconds -= dt;

            // docs/23 §6: an anomaly buff counts down regardless of order
            // too, same "a timer is a timer" reasoning as the attack
            // cooldown above.
            if (ActiveBuff.HasValue)
            {
                _buffRemainingSeconds -= dt;
                if (_buffRemainingSeconds <= 0.0)
                {
                    _buffRemainingSeconds = 0.0;
                    ActiveBuff = null;
                }
            }

            if (Order != UnitOrderKind.MoveTo || _path == null) return;

            // docs/23 §4: EffectiveSpeed (base Speed x the level bonus),
            // not the raw base -- a leveled-up unit actually moves
            // faster, not just hits harder. docs/23 §7: THEN the Lumen-
            // cycle faction speed multiplier on top.
            var budget = EffectiveSpeed * speedMultiplier * dt;
            while (budget > 0.0 && Order == UnitOrderKind.MoveTo)
            {
                if (_path == null || _pathIndex >= _path.Count)
                {
                    Order = UnitOrderKind.Idle;
                    _path = null;
                    break;
                }

                var (tx, tz) = _path[_pathIndex].ToWorld();
                var dx = tx - X;
                var dz = tz - Z;
                var dist = Math.Sqrt(dx * dx + dz * dz);

                if (dist <= budget)
                {
                    X = tx;
                    Z = tz;
                    budget -= dist;
                    _pathIndex++;
                    if (_pathIndex >= _path.Count)
                    {
                        Order = UnitOrderKind.Idle;
                        _path = null;
                    }
                }
                else
                {
                    if (dist > 1e-12)
                    {
                        X += dx / dist * budget;
                        Z += dz / dist * budget;
                    }
                    budget = 0.0;
                }
            }
        }

        /// <summary>Append a waypoint to walk once the current path (and
        /// every waypoint already queued ahead of it) is finished --
        /// never replaces anything already in flight. If this unit is
        /// currently Idle, it does NOT start moving on its own: the
        /// caller (`MatchState.ApplyMoveQueue`) is responsible for
        /// dequeuing immediately in that case, since starting a path
        /// requires pathfinding this class deliberately doesn't
        /// do.</summary>
        internal void EnqueueWaypoint(HexCoord hex) => _waypointQueue.Enqueue(hex);

        /// <summary>Drop every not-yet-started queued waypoint -- called
        /// when a REPLACE-style move (MoveTo) overrides whatever this
        /// unit was doing, same as Unity's `_waypoints.Clear()` on a
        /// non-queued OrderMove.</summary>
        internal void ClearWaypoints() => _waypointQueue.Clear();

        internal bool HasQueuedWaypoints => _waypointQueue.Count > 0;

        internal HexCoord DequeueWaypoint() => _waypointQueue.Dequeue();

        /// <summary>docs/27 Phase C: apply one tick's worth of separation
        /// nudge, computed by <see cref="MatchState"/> (which owns the
        /// blocked-hex clamp and the other units' current positions) via
        /// <see cref="Flocking.Separate"/>. A plain position add -- this
        /// class stays unaware of neighbours, radii of OTHER units, or the
        /// blocked-hex set, matching how it's never known about
        /// pathfinding either (`MatchState` computes, `SimUnit`
        /// applies).</summary>
        internal void ApplySeparationOffset(double dx, double dz)
        {
            X += dx;
            Z += dz;
        }

        /// <summary>docs/23 §4: award XP (a no-op for a non-combatant or a
        /// non-positive amount), scaled up by docs/23 §6's XpGain anomaly
        /// buff (+50%, rounded -- same flagged-placeholder-magnitude
        /// status as every other anomaly buff number) while
        /// <see cref="ActiveBuff"/> is <see cref="AnomalyBuffKind.
        /// XpGain"/>. If this pushes <see cref="Level"/> up, current
        /// <see cref="Vitality"/> rises by exactly the same amount
        /// <see cref="EffectiveMaxVitality"/> just did -- preserving how
        /// much damage this unit was already carrying rather than a full
        /// heal on level-up (a documented interpretation choice; docs/23
        /// doesn't specify either way).</summary>
        internal void GrantXp(int amount)
        {
            if (Combat == null || amount <= 0) return;
            if (ActiveBuff == AnomalyBuffKind.XpGain) amount = (int)System.Math.Round(amount * AnomalyXpGainMultiplier);
            var oldEffectiveMax = EffectiveMaxVitality;
            XP += amount;
            var newEffectiveMax = EffectiveMaxVitality;
            if (newEffectiveMax != oldEffectiveMax) Vitality += newEffectiveMax - oldEffectiveMax;
        }

        /// <summary>docs/23 §6: start (or retarget) attacking a Loose
        /// Experiment. Same "doesn't reset cooldown" contract as
        /// <see cref="BeginAttacking"/> -- Ferocity gates attack RATE
        /// regardless of what this unit last swung at.</summary>
        internal void BeginAttackingAnomaly(uint anomalyId)
        {
            AttackAnomalyTargetId = anomalyId;
            Order = UnitOrderKind.AttackAnomaly;
        }

        /// <summary>2026-07: start (or retarget) attacking a <see
        /// cref="SimBuilding"/>. Same "doesn't reset cooldown" contract as
        /// <see cref="BeginAttacking"/>/<see cref="BeginAttackingAnomaly"/>.</summary>
        internal void BeginAttackingBuilding(uint buildingId)
        {
            AttackBuildingTargetId = buildingId;
            Order = UnitOrderKind.AttackBuilding;
        }

        /// <summary>docs/23 Phase 4: start (or retarget) an attack channel.
        /// Does NOT reset the cooldown -- an attack already close to
        /// ready keeps its progress even when retargeting, matching
        /// docs/04's model where Ferocity gates attack RATE, not
        /// per-target state.</summary>
        internal void BeginAttacking(uint targetId)
        {
            AttackTargetId = targetId;
            Order = UnitOrderKind.AttackUnit;
        }

        internal bool CanAttackNow => _attackCooldownSeconds <= 0.0;

        /// <summary>Start the cooldown for this unit's NEXT attack --
        /// called by `MatchState` immediately after resolving one
        /// (docs/04: Ferocity is attacks/second, so the cooldown is
        /// exactly its reciprocal). Also stamps <see cref="LastCombatFrame"/>
        /// for the DEALING side of the exchange -- see that property's own
        /// doc comment for why this is the natural hook rather than a new
        /// call site.</summary>
        internal void ResetAttackCooldown(int currentFrame)
        {
            LastCombatFrame = currentFrame;
            if (Combat != null) _attackCooldownSeconds = 1.0 / Combat.Value.Ferocity;
        }

        /// <summary>Apply combat damage. A no-op if already dead or if
        /// this unit has no combat stats at all (shouldn't happen --
        /// `MatchState` only ever calls this on a unit it already
        /// confirmed has `Combat` -- but a defensive no-op costs nothing
        /// and matches every other "bad input never crashes the sim"
        /// contract in this class). Clamped at exactly 0, never negative;
        /// reaching 0 is death: records `DeathTick`, drops whatever this
        /// unit was doing (a corpse channels nothing, attacks nothing, and
        /// harvests nothing -- if it was mid-<see cref="UnitOrderKind.
        /// Salvaging"/> itself when killed, that channel is abandoned
        /// too).</summary>
        internal void ApplyDamage(int amount, int currentFrame)
        {
            if (Combat == null || !IsAlive || amount <= 0) return;
            LastCombatFrame = currentFrame;
            Vitality -= amount;
            if (Vitality <= 0)
            {
                Vitality = 0;
                DeathTick = currentFrame;
                Order = UnitOrderKind.Idle;
                AttackTargetId = null;
                AttackAnomalyTargetId = null;
                AttackBuildingTargetId = null;
                SalvageTargetId = null;
                _salvageChannelRemainingSeconds = 0.0;
                _path = null;
            }
        }

        /// <summary>Append this unit's canonical bytes, FIXED field order
        /// (docs/23 §13-J), floats bitwise. The waypoint queue is part of
        /// this unit's real state -- two clients that disagree on what's
        /// still queued (even before any of it has been walked) must
        /// hash differently, or a desync there would go undetected until
        /// it visibly manifested many ticks later.</summary>
        public void WriteTo(FnvHash h)
        {
            h.Add(EntityId);
            h.Add(PlayerIndex);
            h.AddBits(X);
            h.AddBits(Z);
            h.AddBits(Speed);
            h.AddBits(Radius);
            h.Add((int)Order);
            h.Add(_pathIndex);
            h.Add(_waypointQueue.Count);
            foreach (var w in _waypointQueue) { h.Add(w.Q); h.Add(w.R); }

            // docs/23 Phase 4 (combat core): fixed identity stats (never
            // mutated post-spawn, hashed anyway for the same "everything
            // gets hashed" consistency Speed/Radius already follow) plus
            // the actual mutable combat state.
            h.Add(Combat.HasValue ? 1 : 0);
            if (Combat.HasValue)
            {
                var c = Combat.Value;
                h.Add(c.MaxVitality);
                h.Add(c.Power);
                h.Add(c.Armor);
                h.Add(c.Reach);
                h.AddBits(c.Ferocity);
                h.Add(c.CunningPercent);
                h.Add((int)c.Affinity);
            }
            h.Add(Vitality);
            h.Add(DeathTick ?? -1);
            h.Add((int)FacingEdge);
            h.Add(AttackTargetId.HasValue ? (long)AttackTargetId.Value : -1L);
            h.AddBits(_attackCooldownSeconds);
            h.Add(XP);   // Level is a pure function of XP -- hashing XP alone covers it

            // docs/23 Phase 6a (salvage): SalvageValue is a fixed identity
            // stat (hashed for the same "everything gets hashed" reason as
            // Speed/Radius); the rest is the corpse's own mutable loot
            // state plus the harvester's channel progress.
            h.Add(SalvageValue);
            h.Add(SalvageRemaining);
            h.Add(YieldsGenomeFragment ? 1 : 0);
            h.Add(SalvageTargetId.HasValue ? (long)SalvageTargetId.Value : -1L);
            h.AddBits(_salvageChannelRemainingSeconds);

            // docs/23 §6 (anomalies): attack-target and buff state.
            h.Add(AttackAnomalyTargetId.HasValue ? (long)AttackAnomalyTargetId.Value : -1L);
            h.Add(ActiveBuff.HasValue ? (int)ActiveBuff.Value : -1);
            h.AddBits(_buffRemainingSeconds);

            // docs/23 Phase 7: the regeneration quirk is a fixed identity
            // stat (same "hashed anyway" consistency as SalvageValue);
            // LastCombatFrame is real mutable state two clients must agree
            // on, or a desync in the out-of-combat gate would go
            // undetected.
            h.Add(HasRegenerationQuirk ? 1 : 0);
            h.Add(LastCombatFrame ?? -1);

            // 2026-07 Mixed-faction amendment: another fixed identity
            // stat, hashed for the same reason as HasRegenerationQuirk --
            // it changes real gameplay behavior (FactionLumenTable
            // lookups), so two clients must agree on it.
            h.Add(RaceOverride.HasValue ? (int)RaceOverride.Value : -1);

            // 2026-07: building-attack target, same "real mutable state
            // two clients must agree on" reasoning as AttackAnomalyTargetId.
            h.Add(AttackBuildingTargetId.HasValue ? (long)AttackBuildingTargetId.Value : -1L);
        }
    }
}
