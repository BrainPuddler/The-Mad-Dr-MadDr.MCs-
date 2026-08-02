// SteerVerify -- a headless reproduction of the ground-movement loop, so
// the "monsters circle each other" bug can be MEASURED instead of guessed
// at. Compiles the REAL `Assets/Scripts/MonsterSteeringController.cs`
// (see the csproj) against small shims; the parts of the loop that live
// inside MonoBehaviours (`MonsterAgent.FollowPath`'s waypoint walk,
// `RuntimeCityBuilder.ApplySeparation`'s hard positional correction) are
// mirrored here, faithfully including their ORDER within a frame:
//
//   for each unit, in roster order (Unity calls Update() per component,
//   sequentially -- so a later unit genuinely sees an earlier unit's
//   already-moved position this same frame):
//       1. seek current waypoint  -> desiredDir
//       2. Combine(...)           -> steering dir + speed scale
//       3. transform.position += dir * step
//       4. publish LastVelocity   (MonsterAgent.Update, after the tick)
//       5. ApplySeparation        (MonsterAgent.Update, after locomotion)
//
// Step 4 is the one the previous throwaway harness got wrong (it never
// published velocities at all), which silently disabled Alignment and
// made every earlier measurement of this bug worthless.
//
// Run:  dotnet run --project unity-client/Tools~/SteerVerify
// Flags: --legacy-arrive  (use the pre-fix waypoint arrive rule, so the
//                          before/after delta can be re-measured later)
using System;
using System.Collections.Generic;
using UnityEngine;

internal static class Program
{
    // ---- knobs mirrored from the real scene ---------------------------
    private const float Dt = 1f / 60f;
    private const float GroupSpacing = 1f;          // RuntimeCityBuilder.groupSpacing default
    private const float LegacyGroundArriveDist = 0.6f;   // MonsterAgent.GroundArriveDist
    private const int MaxTicks = 3600;              // 60s -- anything still going is "stuck"

    private static bool _legacyArrive;

    private static int Main(string[] args)
    {
        foreach (var a in args) if (a == "--legacy-arrive") _legacyArrive = true;

        Console.WriteLine("SteerVerify -- ground steering / circling harness");
        Console.WriteLine("arrive rule: " + (_legacyArrive ? "LEGACY (flat 0.6m)" : "current"));
        Console.WriteLine(new string('=', 68));

        var results = new List<Result>
        {
            HeadOnPair("S1 head-on pair (small, R=1.5)", 1.5f),
            HeadOnPair("S2 head-on pair (LARGE, R=5.0)", 5f),
            Corridor3v3("S3 3v3 corridor, opposing squads"),
            ConvergeOnPoint("S4 8 units -> one shared destination", 8, 1.5f),
            ConvergeOnPoint("S5 4 LARGE units -> one shared destination", 4, 5f),
            ChaseMovingTarget("S6 4 monsters chase a moving player"),
            Crush("S7 8v8 dense head-on crush, narrow lane"),
            ConvergeOnPoint("S7b 24 units -> one shared destination", 24, 1.5f),
            ThroughAWall("S8 chase a player standing behind idle bodies"),
        };

        Console.WriteLine();
        Console.WriteLine(new string('=', 68));
        var flips = 0;
        var stuck = 0;
        foreach (var r in results) { flips += r.Flips; if (!r.Resolved) stuck++; }
        Console.WriteLine("TOTAL side-reversals: " + flips + "   scenarios unresolved: " + stuck + "/" + results.Count);
        return 0;
    }

    // ---- scenarios ----------------------------------------------------

    /// <summary>Two units walking straight through each other's start
    /// position -- the simplest possible contest, and the one the creator
    /// reports as "spinning around each other trying to pass."</summary>
    private static Result HeadOnPair(string name, float radius)
    {
        var w = new World();
        w.Add(radius, new Vector3(-25f, 0f, 0f), new Vector3(25f, 0f, 0f));
        w.Add(radius, new Vector3(25f, 0f, 0f), new Vector3(-25f, 0f, 0f));
        return w.Run(name);
    }

    /// <summary>Two three-unit squads walking opposite ways down the same
    /// lane -- the scenario the previous fixes were tuned against.</summary>
    private static Result Corridor3v3(string name)
    {
        var w = new World();
        for (var i = 0; i < 3; i++)
        {
            var lane = (i - 1) * 2.5f;
            w.Add(1.5f, new Vector3(-30f, 0f, lane), new Vector3(30f, 0f, lane));
            w.Add(1.5f, new Vector3(30f, 0f, lane), new Vector3(-30f, 0f, lane));
        }
        return w.Run(name);
    }

    /// <summary>Everyone ordered to the SAME world point from all around
    /// it. Separation holds bodies (2*R + spacing) apart, so most of them
    /// physically cannot stand on the goal -- what they do while they
    /// can't is the whole question.</summary>
    private static Result ConvergeOnPoint(string name, int count, float radius)
    {
        var w = new World();
        for (var i = 0; i < count; i++)
        {
            var ang = i * (2f * Math.PI / count);
            var start = new Vector3((float)Math.Cos(ang) * 30f, 0f, (float)Math.Sin(ang) * 30f);
            w.Add(radius, start, Vector3.zero);
        }
        return w.Run(name);
    }

    /// <summary>The reported exploit: a player stands near a knot of
    /// monsters and they orbit forever instead of closing. The "player" is
    /// a slowly-strafing goal every monster re-seeks each tick.</summary>
    private static Result ChaseMovingTarget(string name)
    {
        var w = new World { MovingGoal = true };
        for (var i = 0; i < 4; i++)
        {
            var ang = i * (2f * Math.PI / 4);
            w.Add(1.5f, new Vector3((float)Math.Cos(ang) * 22f, 0f, (float)Math.Sin(ang) * 22f), Vector3.zero);
        }
        return w.Run(name);
    }

    /// <summary>Eight against eight jammed into a lane barely wider than
    /// their bodies -- the worst legitimate crowding this layer can face,
    /// and the case where an unbounded lateral avoidance sum stacks
    /// highest.</summary>
    private static Result Crush(string name)
    {
        var w = new World();
        for (var i = 0; i < 8; i++)
        {
            var lane = (i % 4 - 1.5f) * 3.5f;
            var rank = (i / 4) * 5f;
            w.Add(1.5f, new Vector3(-20f - rank, 0f, lane), new Vector3(20f, 0f, lane));
            w.Add(1.5f, new Vector3(20f + rank, 0f, lane), new Vector3(-20f, 0f, lane));
        }
        return w.Run(name);
    }

    /// <summary>The reported exploit in its purest form: the player stands
    /// just behind a clump of bodies the chasers have to get through. If
    /// avoidance can out-vote seek, this is where they orbit instead of
    /// arriving.</summary>
    private static Result ThroughAWall(string name)
    {
        var w = new World();
        for (var i = 0; i < 5; i++)   // the wall: idle bodies parked on the approach
            w.AddIdle(1.5f, new Vector3((i - 2) * 3.2f, 0f, 0f));
        for (var i = 0; i < 4; i++)
            w.Add(1.5f, new Vector3((i - 1.5f) * 3.2f, 0f, -20f), new Vector3(0f, 0f, 6f));
        return w.Run(name);
    }

    // ---- the simulated world ------------------------------------------

    private sealed class Mover
    {
        public UnitCombat Unit;
        public Vector3 Goal;
        public float Speed = 8f;
        public int Flips;               // left/right steering reversals
        public float LastSide;          // sign of the last non-trivial lateral deflection
        public bool Arrived;
        public int ArrivedTick = -1;
        public float PathLength;        // total distance actually walked
        public float StraightLine;      // start->goal distance, for the detour ratio
        public MonsterSteeringController.WaypointProgress Progress;
        public bool ProgressArmed;
        public bool Contested;
        public Vector3 LegDir;          // direction of the leg being walked, for the overshoot test
    }

    private sealed class Result
    {
        public string Name;
        public int Flips;
        public bool Resolved;
    }

    private sealed class World
    {
        public bool MovingGoal;
        private readonly List<Mover> _movers = new List<Mover>();
        private readonly List<UnitCombat> _neighbours = new List<UnitCombat>();

        public void Add(float radius, Vector3 start, Vector3 goal)
        {
            var u = new UnitCombat { Radius = radius };
            u.transform.position = start;
            var straight = goal - start;
            straight.y = 0f;
            _movers.Add(new Mover
            {
                Unit = u, Goal = goal,
                StraightLine = straight.magnitude,
                LegDir = straight.normalized,
            });
        }

        /// <summary>A body that just stands there -- still a full
        /// separation/avoidance participant, never a mover.</summary>
        public void AddIdle(float radius, Vector3 at)
        {
            var u = new UnitCombat { Radius = radius };
            u.transform.position = at;
            _idle.Add(u);
        }

        private readonly List<UnitCombat> _idle = new List<UnitCombat>();

        public Result Run(string name)
        {
            foreach (var m in _movers) _neighbours.Add(m.Unit);
            foreach (var u in _idle) _neighbours.Add(u);

            var tick = 0;
            for (; tick < MaxTicks; tick++)
            {
                if (MovingGoal)
                {
                    // a "player" drifting sideways at a walk -- the goal is
                    // re-read every tick, exactly like TickAttackUnit does
                    var t = tick * Dt;
                    var g = new Vector3((float)Math.Sin(t * 0.4f) * 12f, 0f, (float)Math.Cos(t * 0.4f) * 12f);
                    foreach (var m in _movers) m.Goal = g;
                }

                var allDone = true;
                foreach (var m in _movers)
                {
                    if (!Step(m, tick)) allDone = false;
                }
                TrackMinGap();   // running minimum over the WHOLE run, not an end-state sample
                if (allDone && !MovingGoal) break;
            }

            var resolved = true;
            var flips = 0;
            foreach (var m in _movers)
            {
                flips += m.Flips;
                if (!m.Arrived) resolved = false;
            }

            // "path efficiency": walked distance vs the straight line each
            // unit needed. A unit that orbits racks up huge distance for no
            // progress, which is precisely what the old stall detector (it
            // measured DISTANCE MOVED, not progress) could never see.
            var worstDetour = 1f;
            foreach (var m in _movers)
            {
                var straight = Mathf.Max(1f, m.StraightLine);
                var ratio = m.PathLength / straight;
                if (ratio > worstDetour) worstDetour = ratio;
            }

            Console.WriteLine();
            Console.WriteLine(name);
            Console.WriteLine("  side-reversals : " + flips);
            Console.WriteLine("  resolved       : " + (MovingGoal ? "n/a (moving goal)" : (resolved ? "yes @ tick " + tick : "NO -- STUCK")));
            Console.WriteLine("  worst detour   : " + worstDetour.ToString("0.00") + "x straight line");
            Console.WriteLine("  min gap        : " + _minGap.ToString("0.00") + "m (0 = bodies touching)");
            Console.WriteLine("  max deflection : " + _maxDeflect.ToString("0.0") + " deg off the seek direction");

            return new Result { Name = name, Flips = flips, Resolved = MovingGoal || resolved };
        }

        /// <summary>One unit's frame, in MonsterAgent.Update()'s own order.</summary>
        private bool Step(Mover m, int tick)
        {
            if (m.Arrived && !MovingGoal) { m.Unit.LastVelocity = Vector3.zero; Separate(m); return true; }

            var pos = m.Unit.transform.position;
            var to = m.Goal - pos;
            to.y = 0f;
            var dist = to.magnitude;

            var arrive = _legacyArrive
                ? LegacyGroundArriveDist
                : MonsterSteeringController.ArriveDistFor(m.Unit.Radius);

            if (!m.ProgressArmed)
            {
                MonsterSteeringController.ResetProgress(ref m.Progress);
                m.ProgressArmed = true;
            }

            // legacy = the flat distance test FollowPath used to do; current
            // = the shared three-rule test (arrived / overshot / contested
            // and not closing). The goal here IS the final node, so
            // "advance past it" means "this order is done."
            var done = _legacyArrive
                ? dist < arrive
                : MonsterSteeringController.ShouldAdvanceWaypoint(ref m.Progress, pos, m.Goal,
                    m.LegDir, arrive, m.Contested, Dt);

            if (done)
            {
                // sticky, mirroring the real game: consuming the FINAL path
                // node makes FollowPath return zero, which sends TickMove
                // into GoIdle(). The order is over -- the unit does not
                // re-seek the point it just settled at. (Separation keeps
                // running, so the pack still spreads out around it; that's
                // TickSettle's job in the real code.)
                if (!m.Arrived) { m.Arrived = true; m.ArrivedTick = tick; }
                m.Unit.LastVelocity = Vector3.zero;
                Separate(m);
                return true;
            }

            var dir = to / dist;
            var result = MonsterSteeringController.Combine(m.Unit, dir, m.Speed, _neighbours, GroupSpacing, tick * Dt);
            m.Contested = result.Contested;

            // count left/right reversals: the sign of the steering output's
            // lateral component relative to the pure seek direction
            // how far the blend actually deflected off the seek direction
            // this frame -- the number that says whether ClampToCone is
            // doing anything or is dead weight
            var deflect = (float)(Math.Acos(Mathf.Clamp(Vector3.Dot(result.Direction, dir), -1f, 1f)) * 180.0 / Math.PI);
            if (deflect > _maxDeflect) _maxDeflect = deflect;

            var right = new Vector3(dir.z, 0f, -dir.x);
            var lateral = Vector3.Dot(result.Direction, right);
            if (Mathf.Abs(lateral) > 0.05f)
            {
                var side = lateral > 0f ? 1f : -1f;
                if (m.LastSide != 0f && side != m.LastSide) m.Flips++;
                m.LastSide = side;
            }

            var scaled = m.Speed * result.SpeedScale;
            var step = Mathf.Min(scaled * Dt, dist);
            var before = m.Unit.transform.position;
            m.Unit.transform.position = before + result.Direction * step;
            m.Unit.LastVelocity = result.Direction * scaled;
            m.PathLength += (m.Unit.transform.position - before).magnitude;

            Separate(m);
            return false;
        }

        /// <summary>RuntimeCityBuilder.ApplySeparation, mirrored -- the hard
        /// positional correction that runs EVERY frame for every grounded
        /// unit, on top of whatever Combine already decided.</summary>
        private void Separate(Mover m)
        {
            var push = MonsterSteeringController.SeparationForce(m.Unit, _neighbours, GroupSpacing);
            if (push.sqrMagnitude > 1e-8f) m.Unit.transform.position = m.Unit.transform.position + push;
        }

        private float _minGap = float.MaxValue;
        private float _maxDeflect;

        /// <summary>Closest any two bodies' surfaces came, over the whole
        /// run -- the guard that a steering change didn't buy smoothness by
        /// quietly letting units interpenetrate.</summary>
        private void TrackMinGap()
        {
            var min = _minGap;
            for (var i = 0; i < _movers.Count; i++)
            for (var j = i + 1; j < _movers.Count; j++)
            {
                var d = _movers[i].Unit.transform.position - _movers[j].Unit.transform.position;
                d.y = 0f;
                var gap = d.magnitude - _movers[i].Unit.Radius - _movers[j].Unit.Radius;
                if (gap < min) min = gap;
            }
            _minGap = min;
        }
    }

    /// <summary>The waypoint arrive radius under test. Kept here (not in
    /// the steering file) for the baseline run; the fix moves the real
    /// rule into MonsterSteeringController so both this harness and
    /// MonsterAgent read ONE definition.</summary>
    private static float ArriveDistFor(float radius)
    {
        return LegacyGroundArriveDist;
    }
}
