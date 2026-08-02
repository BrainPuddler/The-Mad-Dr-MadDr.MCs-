# SteerVerify

Headless reproduction of the ground-movement loop, so the recurring
"monsters circle each other instead of chasing" bug can be **measured**
instead of guessed at.

```
dotnet run --project unity-client/Tools~/SteerVerify
dotnet run --project unity-client/Tools~/SteerVerify -- --legacy-arrive
```

It compiles the real `Assets/Scripts/MonsterSteeringController.cs` (not a
copy — see the csproj) against tiny `UnityEngine`/`UnitCombat` shims, and
mirrors the parts of the frame that live inside MonoBehaviours
(`MonsterAgent.FollowPath`'s waypoint walk, `RuntimeCityBuilder.ApplySeparation`)
in `Program.cs`, **in Unity's own within-frame order** — including
publishing `LastVelocity` after each unit moves.

That last detail is why this harness lives in the repo rather than being
re-invented per session: the throwaway harness used for the earlier 2026-08
passes never published velocities between frames, so `Alignment` was
silently dead in every measurement it produced and its conclusions were
worthless. `MonsterSteeringController`'s own header records that
correction.

## Metrics

| metric | meaning |
| --- | --- |
| **side-reversals** | how many times a unit flipped its left/right steering decision. High = visible jitter. |
| **resolved** | did every unit reach its destination inside 60 s? |
| **worst detour** | walked distance ÷ straight-line distance. **This is the circling number** — an orbiting unit racks up distance with zero progress. |
| **min gap** | closest two body surfaces came over the whole run. Guards against buying smoothness with interpenetration. Negative = units overlapped. |
| **max deflection** | furthest the blend steered off the seek direction. Pinning at exactly 60° means `ClampToCone` is load-bearing. |

## Results

Before = commit `b9c56a0`. After = the 2026-08 root-cause pass.

| scenario | flips before | flips after | detour before | detour after | resolved before → after |
| --- | ---: | ---: | ---: | ---: | --- |
| S1 head-on pair (R=1.5) | 0 | 0 | 1.00x | 1.00x | yes → yes |
| S2 head-on pair (R=5.0) | 0 | 0 | 1.04x | 1.00x | yes → yes |
| S3 3v3 corridor | 10347 | **23** | 6.82x | **1.21x** | **STUCK → yes** |
| S4 8 → one destination | 3414 | **246** | 13.69x | **1.88x** | **STUCK → yes** |
| S5 4 large → one dest. | 1412 | **21** | 15.75x | **1.51x** | **STUCK → yes** |
| S6 4 chase a moving player | 519 | **43** | 16.69x | **3.57x** | n/a |
| **total (S1–S6)** | **15692** | **333** | | | **3 stuck → 0** |

S7 (8v8 crush), S7b (24 → one destination) and S8 (chase a player standing
behind idle bodies) were added during this pass to stress the fixes; they
have no "before" number, and all three resolve.

The lone-pair cases (S1/S2) were already clean — which is exactly why the
previous passes, which only ever tested a pair, kept concluding the bug
was fixed. Every scenario with **three or more** units in contest orbited
forever.

## What each fix is worth (ablation, total flips across the suite)

| configuration | flips | unresolved |
| --- | ---: | ---: |
| all fixes | 427 | 0/8 |
| without the contested-waypoint escape | 642 | 1/8 |
| without side commitment | 593 | 0/8 |
| without the cone clamp | 423 | 0/8 |
| with the *legacy* flat 0.6 m arrive rule | 4465 | 2/8 |

Read honestly:

- The **arrive-rule fix dominates** — reverting just it undoes most of the
  cure even with everything else in place.
- The **cone clamp does not move the flip count** at these densities
  (423 vs 427 — noise). What it does do is bound the worst case: without
  it, peak deflection reaches 74.5°, and above 90° a closed orbit becomes
  possible again. It is a guarantee, not a tuning win, and is documented
  as such.
- Widening the give-way heading cutoff to 0.35 was tried and **rejected**:
  427 → 466 flips, nothing resolved faster.

(These ablation numbers predate S7b, so they total over 8 scenarios, not 9.)
