using MadDr.CityGen;
using MadDr.MatchCore;

// docs/23 Phase 1 acceptance: a headless 8-player match runs 10,000 ticks
// deterministically. Print the final state hash TWICE from two independent
// runs -- the two lines must be identical.
static ulong RunEmptyMatch()
{
    var factions = new List<FactionId>
    {
        FactionId.MadDoctor, FactionId.HumanArmy, FactionId.AlienHive, FactionId.MadDoctor,
        FactionId.HumanArmy, FactionId.AlienHive, FactionId.MadDoctor, FactionId.HumanArmy,
    };
    var m = MatchState.Create(0xC0FFEEu, factions);
    for (var i = 0; i < 10_000; i++) m.Tick(null);
    return m.Hash();
}

// docs/23 §13-A Phase 1.5 acceptance: a headless harness ticks 100 units
// through scripted orders twice -> identical MatchState hash. Uses a real
// generated city (citygen-core, same determinism discipline) and the same
// HexPathfinder the live game paths with.
static ulong RunHundredUnits()
{
    var city = CityGenerator.Generate(0xB16u, CityPreset.Village());
    var factions = new List<FactionId> { FactionId.MadDoctor, FactionId.HumanArmy };
    var m = MatchState.Create(0xF00Du, factions, city);

    var blocked = BattlefieldState.FreshFrom(city).BlockedToGround();
    var spots = new List<HexCoord>();
    for (var r = 0; r <= 60 && spots.Count < 100; r++)
    {
        foreach (var h in city.CenterHex.Ring(r))
        {
            if (!city.Contains(h) || blocked.Contains(h)) continue;
            spots.Add(h);
            if (spots.Count >= 100) break;
        }
    }

    var ids = new List<uint>();
    for (var i = 0; i < spots.Count; i++)
        ids.Add(m.SpawnUnit(i % 2, spots[i], speed: 4.0 + (i % 5)));

    var commands = new List<Command>();
    for (var i = 0; i < ids.Count; i++)
    {
        var goal = spots[(i + 37) % spots.Count];
        commands.Add(new Command(i % 2, CommandKind.MoveTo, targetEntity: ids[i], argA: goal.Q, argB: goal.R));
    }
    m.Tick(commands);

    for (var i = 0; i < 3_000; i++) m.Tick(null);
    return m.Hash();
}

var okAll = true;

{
    var a = RunEmptyMatch();
    var b = RunEmptyMatch();
    Console.WriteLine("Phase 1 -- 10,000-tick 8-player empty match:");
    Console.WriteLine($"  run 1: {a:X16}");
    Console.WriteLine($"  run 2: {b:X16}");
    Console.WriteLine("  " + (a == b ? "DETERMINISTIC: identical" : "DESYNC: MISMATCH"));
    okAll &= a == b;
}

{
    var a = RunHundredUnits();
    var b = RunHundredUnits();
    Console.WriteLine();
    Console.WriteLine("Phase 1.5 -- 100 units, scripted MoveTo orders, 3,000 ticks:");
    Console.WriteLine($"  run 1: {a:X16}");
    Console.WriteLine($"  run 2: {b:X16}");
    Console.WriteLine("  " + (a == b ? "DETERMINISTIC: identical" : "DESYNC: MISMATCH"));
    okAll &= a == b;
}

return okAll ? 0 : 1;
