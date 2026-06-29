# HW6 — Root-Cause Analysis & Fix Plan (for independent review)

> This doc is for review only. It is **not** a submitted source file (only `MinionStateMachine.cs`,
> `ThrowMethods.cs`, `ShotSelection.cs` are submitted). Current code on `main` is **v6 / commit at
> push time**; the Step-1 fix below is **not yet implemented** — it is pending review.

## Problem

The HW6 team FSM must beat the demo opponent **Glass Joe ≥ 2/3 of matches across team sizes 1–5 and
balls 1–4** (85%) + undisclosed opponents (15%). I was tuning blind (no Unity locally) against
10-match head-to-head runs and circling on the **1-ball-per-team** configs.

## Results so far (10 matches each, vs Glass Joe; `NvN_Xb` = N players, X balls/team)

| Config | v1 | v2 | v3 | v4 | v5 | v6 (HEAD) |
|---|---|---|---|---|---|---|
| 1v1_1b | 50 | 0 | 40 | 50 | 50 | 50 ❌ |
| 2v2_1b | 30 | 70 | 70 | 50 | 90 | 60 ❌ |
| 2v2_2b | 60 | 70 | 30 | 100 | 100 | 100 ✅ |
| 3v3_3b | 90 | 100 | 100 | 100 | 100 | 100 ✅ |
| 4v4_3b | 60 | 90 | 90 | 100 | 80 | 90 ✅ |
| 5v5_1b | 20 | 50 | 20 | 20 | 20 | 10 ❌ |
| 5v5_4b | 100| 80 | 100| 90 | 90 | 100 ✅ |

Consistent failures are **exactly the 1-ball configs**. ≥2-ball configs are reliably 90–100%.

## Investigation findings (3 parallel code reads, with citations)

### Win/Tie mechanics (`PrisonDodgeballManager.cs`, `DodgeballTests.cs`)
- A match is **WON only if ALL `TeamSize` of one team are imprisoned in the SAME frame**
  (`teamXInPrison >= TeamSize`, mgr ~:1703–1722).
- **Timeout = flat TIE; the prisoner-count tiebreaker is commented out** (~:1844–1857). Being ahead
  on eliminations at the buzzer earns nothing.
- **Rescues reverse eliminations**: a teammate throws a same-team ball to an imprisoned minion
  touching the prison wall (`CanBeRescued = IsPrisoner && TouchingPrison`, `DodgeBall.cs:171–197`).
- Match length **120 s**. Grading is **strictly per-config**: each runs 10 matches, asserts
  `winRatio >= 2/3`; **ties count as non-wins** ⇒ effective bar **≥ 7 wins / 10**. No aggregation.

### Ball lifecycle (`DodgeBall.cs`, `PrisonDodgeballManager.cs`)
- A ball is "owned" only while held/in-flight; the instant it hits floor/wall it becomes **Neutral**
  and collectible (no timers).
- Collectible also requires **Reachable** = navmesh area is Neutral / Walkable / **your own half**
  (never the opponent's half).
- ⇒ A ball you throw to their side lands Neutral but **Reachable only for them**; an opponent's
  *miss onto your side / the neutral center* is Reachable for **you**. **Possession is the whole game
  in 1-ball play.**

### Glass Joe — two fatal flaws (`MinionAI/*.cs`)
1. **Cannot lead a moving target** — its `PredictThrow` is the placeholder, aims at the target's
   *current* position. A continuously-moving minion is nearly un-hittable by it.
2. **No reactive dodge; defenseless after every throw** — blind timer-evade (~1/0.9 s, 1/3 just
   brakes); after throwing its lone ball it wanders ball-less in `DefensiveDemo`. Only collects
   Neutral+Reachable balls; clusters at fixed `TeamAdvance`/`TeamHome`. (No fixed RNG seed, but
   structure is deterministic.)

## Root cause (primary hypothesis — credit to 2nd review agent)

**Offense starvation from one constant: `ThrowMaxInterceptT = 1.1f`.** In 1-ball configs the enemies
sit on their back line (no scattered balls pulling them to midfield), so they never enter my 1.1 s
shot window → I hold the ball and never fire → **ties**. The 1.1 s conservatism exists to beat
*dodgers*, but Glass Joe **doesn't dodge** (85% of the grade) — so every long accurate shot I refuse
is a free elimination. This single cause predicts the whole table: win where enemies come to me,
stall where they don't. (v6's "bait/hold" branch made it strictly worse by standing the carrier
still in 1-ball games.)

Secondary contributor: my minions `Stop()` to aim → the one thing Glass Joe *can* punish. Held in
reserve as Step 2.

## Plan — one variable at a time, falsifiable

### STEP 1 (offense fix) — edit `MinionStateMachine.cs`
1. **Delete the v6 bait/hold branch** in `ThrowBallState`; always keep advancing (defenseless target
   preferred).
2. **Short-shot cap becomes a fallback, not a gate.** Add `FireAnywayDelay = 0.7f`; give
   `FindBestThrowTarget` an optional `maxT`:
   - Prefer `maxT = 1.1` shots and fire whenever one exists (ball-rich play unchanged — resets the
     hold timer every frame, so the fallback ~never triggers there).
   - If no short shot for `> 0.7 s`, call with `maxT = ∞` and **fire the best reachable shot anyway**
     (`PredictThrow` reachability still bounds range). Free kill vs a non-dodger; still beats holding.
3. Keep v5's accurate leading `PredictThrow`, defenseless-target preference, projectile dodge,
   twin-ray occlusion.

**Falsification (one 10-match run):** if `5v5_1b` ties collapse 6 → ~0 and it + `1v1_1b`/`2v2_1b`
clear 2/3 → confirmed. If ties stay ~6 → hypothesis wrong → Step 2.

### STEP 2 (defense fix — only if Step 1 leaves losses)
Exploit flaw #1: **throw on the move** (drop `Stop()`, strafe perpendicular to nearest armed
opponent while aiming) + **defenders never idle** (continuous unpredictable motion; robust vs the
15% undisclosed opponents).

## Verification
- Falsification run at `numMatches = 10` (signal is the `5v5_1b` tie count).
- Confirmation run at `numMatches = 30` on marginal configs (a true ≥80% is needed to reliably clear
  a single 10-match 7/10 autograder run — binomial: a true 70% fails ~35% of runs).
- Disallowed-access audit on the 3 submitted files; strip `// compile_check` only at submission.

## Specific questions for the reviewer
1. Does the `maxT = ∞` fallback risk anything in the **ball-rich** configs (it shouldn't — short
   shots reset the 0.7 s timer every frame there)?
2. For `5v5_1b`, beyond firing more: is **rescue suppression** (deny Glass Joe its ball so jails
   stick) worth building, or does pure elimination throughput suffice?
3. Is there a court-geometry risk that even `maxT = ∞` can't produce a reachable shot on a back-line
   enemy from our half (i.e., max `ThrowSpeed` range < half-court-to-backline distance)?
