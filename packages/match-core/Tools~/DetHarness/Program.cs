using MadDr.MatchCore;

// docs/23 Phase 1 acceptance: a headless 8-player match runs 10,000 ticks
// deterministically. Print the final state hash TWICE from two independent
// runs -- the two lines must be identical.
static ulong Run()
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

var a = Run();
var b = Run();
Console.WriteLine($"run 1: {a:X16}");
Console.WriteLine($"run 2: {b:X16}");
Console.WriteLine(a == b ? "DETERMINISTIC: identical" : "DESYNC: MISMATCH");
return a == b ? 0 : 1;
