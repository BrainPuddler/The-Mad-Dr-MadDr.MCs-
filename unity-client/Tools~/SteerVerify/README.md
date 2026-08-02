# SteerVerify

Headless reproduction of the ground-movement loop, so the recurring
"monsters circle each other instead of chasing" bug can be **measured**
instead of guessed at.

```
dotnet run --project unity-client/Tools~/SteerVerify
```

It compiles the real `Assets/Scripts/MonsterSteeringController.cs` (not a
copy — see the csproj) against tiny `UnityEngine`/`UnitCombat` shims, and
mirrors the parts of the frame that live inside MonoBehaviours
(`MonsterAgent.FollowPath`'s waypoint walk, `RuntimeCityBuilder.ApplySeparation`)
in `Program.cs`, **in Unity's own within-frame order** — including
publishing `LastVelocity` after each unit moves.

That last detail is why this harness exists in the repo rather than being
re-invented per session: the throwaway harness used for the 2026-08
passes never published velocities between frames, so `Alignment` was
silently dead in every measurement it produced and its conclusions were
worthless. `MonsterSteeringController`'s own header documents that
correction.

## Metrics

| metric | meaning |
| --- | --- |
| **side-reversals** | how many times a unit flipped its left/right steering decision. High = visible jitter. |
| **resolved** | did every unit reach its destination inside 60 s? |
| **worst detour** | walked distance ÷ straight-line distance. **This is the circling number** — an orbiting unit racks up distance with zero progress. |
| **min gap** | closest two body surfaces came over the whole run. Guards against buying smoothness with interpenetration. Negative = units overlapped. |

## Baseline (commit b9c56a0, before the 2026-08 root-cause pass)

```
S1 head-on pair (small, R=1.5)          0 flips   resolved     1.00x
S2 head-on pair (LARGE, R=5.0)          0 flips   resolved     1.04x
S3 3v3 corridor, opposing squads    10347 flips   STUCK        6.82x   gap -0.31m
S4 8 units -> one shared destination 3414 flips   STUCK       13.69x
S5 4 LARGE units -> shared dest       1412 flips  STUCK       15.75x
S6 4 monsters chase a moving player    519 flips   n/a        16.69x
                                    TOTAL 15692 flips, 3/6 scenarios never resolve
```

The lone-pair cases (S1/S2) were already clean — which is exactly why the
previous passes, which only ever tested a pair, kept concluding the bug
was fixed. Every scenario with **three or more** units in contest orbits
forever.
