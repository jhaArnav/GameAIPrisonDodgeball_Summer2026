// compile_check
// Remove the line above if you are submitting to GradeScope for a grade. But leave it if you only want to check
// that your code compiles and the autograder can access your public methods.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

using GameAI;
using MinionFSMData = GameAIStudent.BaseMinionStateMachine.MinionFSMData;


namespace GameAIStudent
{

    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(MinionScript))]
    public class MinionStateMachine : BaseMinionStateMachine
    {
        public const string StudentName = "Arnav Jha";

        protected override string StudentNameText => StudentName;

        public const string GlobalTransitionStateName = "GlobalTransition";
        public const string CollectBallStateName = "CollectBall";
        public const string GoToThrowSpotStateName = "GoToThrowBall";
        public const string ThrowBallStateName = "ThrowBall";
        public const string DefensiveDemoStateName = "DefensiveDemo";
        public const string GoToPrisonStateName = "GoToPrison";
        public const string LeavePrisonStateName = "LeavePrison";
        public const string GoHomeStateName = "GoHome";
        public const string RescueStateName = "Rescue";
        public const string RestStateName = "Rest";


        // For throws...
        public static float MaxAllowedThrowPositionError = (0.25f + 0.5f) * 0.99f;

        // ---- Tunable strategy parameters (adjust after head-to-head test runs) ----
        // Only take high-confidence shots: short flight time => opponent can't dodge in time =>
        // high hit rate. We CLOSE THE DISTANCE (advance toward the target) to create these shots
        // rather than chucking long, easily-dodged cross-field throws.
        const float ThrowMaxInterceptT = 1.1f;
        // Fallback cap: if no SHORT shot has been available for a while, take the best REACHABLE
        // shot regardless of flight time. Effectively unbounded -> "any shot the ballistics can
        // solve." Against a non-dodging opponent (Glass Joe = 85% of the grade) every accurate
        // shot is a free elimination, so holding fire for a perfect short shot just stalemates.
        const float ThrowMaxInterceptTRelaxed = 99f;
        // How long (sec) we hold the ball without a short shot before firing the relaxed shot.
        // Short shots reset this every frame, so in ball-rich play the fallback ~never triggers
        // (winning configs stay unchanged); it only kicks in during scarce-ball stalemates.
        const float FireAnywayDelay = 0.7f;
        // Penalty (sec) added to an armed opponent's shot score so we prefer defenseless targets
        // (an opponent without a ball can't counter-throw -> a free elimination). Larger than the
        // max intercept time so any ball-less target outranks any armed one.
        const float ArmedTargetPenalty = 10f;
        // If we hold a ball but can't find a shot for this long, reposition / re-plan.
        const float MaxThrowWaitSec = 2.5f;
        // How often a ball-less defender re-picks a position (keeps it mobile, harder to hit).
        // Short so defenders stay in continuous motion — a moving minion is ~un-hittable by
        // Glass Joe's no-lead throw.
        const float DefenseRepositionSec = 0.8f;
        // Lateral spacing between defenders so they spread out instead of clustering.
        const float DefenseLateralSpacing = 2.5f;
        // Approximate dodgeball radius, used to space the twin occlusion rays.
        const float BallRadius = 0.25f;
        // ---- Incoming-projectile dodging ----
        // Minimum speed for a loose enemy ball to count as a thrown projectile (vs. carried).
        const float MinThrownBallSpeed = 6f;
        // How far ahead (sec) we look for a ball's closest approach to us.
        const float BallDodgeHorizon = 0.9f;
        // If a ball's closest approach is within this radius, dodge it.
        const float BallDodgeRadius = 1.1f;


        // TeamShare ("blackboard") extended with a per-frame cache of opponent info so the
        // whole team shares a single GetAllOpponentInfo() query each frame.
        class MyTeamShare : TeamShare
        {
            PrisonDodgeballManager.OpponentInfo[] oppInfo;
            float timeOfOppQuery = float.MinValue;

            public MyTeamShare(PrisonDodgeballManager.Team team, int teamSize, int numBalls)
                : base(team, teamSize, numBalls)
            {
                // Opponent count equals our team size (teams are always equal).
                oppInfo = new PrisonDodgeballManager.OpponentInfo[teamSize];
            }

            public PrisonDodgeballManager.OpponentInfo[] OppInfo
            {
                get
                {
                    var t = Time.timeSinceLevelLoad;
                    if (t != timeOfOppQuery)
                    {
                        timeOfOppQuery = t;
                        PrisonDodgeballManager.Instance.GetAllOpponentInfo(Team, ref oppInfo);
                    }
                    return oppInfo;
                }
            }
        }

        protected override TeamShare CreateTeamShare(PrisonDodgeballManager.Team team, int teamSize, int numBalls)
        {
            return new MyTeamShare(team, teamSize, numBalls);
        }


        // Common base for all states: shared targeting / defense helpers.
        abstract class MinionStateCommon : MinionState
        {
            protected MyTeamShare Shared => GetTeamShare<MyTeamShare>();

            // Layer mask that ignores minions, dodgeballs, and the navmesh carver so an
            // occlusion raycast only trips on real geometry (walls, obstacles).
            protected int ThrowLineMask()
            {
                int carverMask = ~(1 << Mgr.NavMeshCarverLayerIndex);
                int minionMask = ~(1 << Mgr.MinionTeamBLayerIndex) & ~(1 << Mgr.MinionTeamALayerIndex);
                int ballMask = ~(1 << Mgr.BallTeamALayerIndex) & ~(1 << Mgr.BallTeamBLayerIndex);
                return Physics.AllLayers & carverMask & ballMask & minionMask;
            }

            // True if real geometry blocks the straight line from -> to (twin rays a ball-width apart).
            protected bool Occluded(Vector3 from, Vector3 to)
            {
                Vector3 delta = to - from;
                float dist = delta.magnitude;
                if (dist < 1e-4f)
                    return false;

                Vector3 dir = delta / dist;
                Vector3 perp = Vector3.Cross(dir, Vector3.up);
                if (perp.sqrMagnitude < 1e-6f)
                    perp = Vector3.Cross(dir, Vector3.forward);
                perp = perp.normalized * BallRadius;

                int mask = ThrowLineMask();
                return Physics.Raycast(from + perp, dir, dist, mask) ||
                       Physics.Raycast(from - perp, dir, dist, mask);
            }

            // Compute a ballistic solution to hit a single opponent. Returns false if unreachable
            // or the line of fire is blocked.
            protected bool TryAim(PrisonDodgeballManager.OpponentInfo opp,
                out Vector3 dir, out float speedNorm, out Vector3 interceptPos, out float interceptT)
            {
                dir = Vector3.forward;
                speedNorm = 1f;
                interceptPos = opp.Pos;
                interceptT = 0f;

                bool ok = ThrowMethods.PredictThrow(
                    Minion.HeldBallPosition, Minion.ThrowSpeed, Physics.gravity,
                    opp.Pos, opp.Vel, opp.Forward, MaxAllowedThrowPositionError,
                    out var pDir, out var pSpeed, out var pT, out var altT);

                if (!ok || pT <= 0f)
                    return false;

                interceptT = pT;
                interceptPos = opp.Pos + opp.Vel * pT;
                dir = pDir;
                speedNorm = Mathf.Min(1f, pSpeed / Minion.ThrowSpeed);

                if (Occluded(Minion.HeldBallPosition, interceptPos))
                    return false;

                return true;
            }

            // Pick the best (quickest, hardest-to-dodge) valid shot among all live opponents.
            // maxT caps the allowed ball flight time; pass ThrowMaxInterceptTRelaxed for the
            // fire-anyway fallback (take any reachable shot, not just short ones).
            protected bool FindBestThrowTarget(
                out Vector3 dir, out float speedNorm, out Vector3 interceptPos, out float interceptT,
                float maxT = ThrowMaxInterceptT)
            {
                dir = Vector3.forward;
                speedNorm = 1f;
                interceptPos = Minion.transform.position;
                interceptT = 0f;

                var opps = Shared?.OppInfo;
                if (opps == null)
                    return false;

                bool found = false;
                float bestScore = float.MaxValue;

                foreach (var o in opps)
                {
                    if (o.IsPrisoner || o.IsFreedPrisoner)
                        continue;

                    if (!TryAim(o, out var d, out var sn, out var ip, out var t))
                        continue;

                    if (t > maxT)
                        continue;

                    // Prefer DEFENSELESS targets: an opponent without a ball can't counter-throw,
                    // so it's a free, risk-free elimination. Armed opponents get a penalty so we only
                    // pick them when no ball-less target is available. Among equals, quickest shot wins.
                    float score = t + (o.HasBall ? ArmedTargetPenalty : 0f);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        dir = d;
                        speedNorm = sn;
                        interceptPos = ip;
                        interceptT = t;
                        found = true;
                    }
                }

                return found;
            }

            // Find the nearest live opponent's position (for closing distance / facing). Returns
            // false if there is no valid target.
            protected bool NearestOpponentPos(out Vector3 pos)
            {
                pos = Minion.transform.position;
                if (Mgr.FindClosestNonPrisonerOpponentIndex(Minion.transform.position, Team, out var idx) &&
                    Mgr.GetOpponentInfo(Team, idx, out var info))
                {
                    pos = info.Pos;
                    return true;
                }
                return false;
            }

            // Nearest live opponent that is NOT holding a ball (a safe, free target to advance on).
            protected bool NearestDefenselessOpponentPos(out Vector3 pos)
            {
                pos = Minion.transform.position;
                var opps = Shared?.OppInfo;
                if (opps == null)
                    return false;

                Vector3 myPos = Minion.transform.position;
                float best = float.MaxValue;
                bool found = false;
                foreach (var o in opps)
                {
                    if (o.HasBall || o.IsPrisoner || o.IsFreedPrisoner)
                        continue;
                    float d = Vector3.Distance(o.Pos, myPos);
                    if (d < best)
                    {
                        best = d;
                        pos = o.Pos;
                        found = true;
                    }
                }
                return found;
            }

            // Dodge an incoming projectile if there is one. Returns true if an evade was performed.
            protected bool DodgeIfThreatened()
            {
                return TryDodgeProjectile(Minion, TeamData);
            }
        }

        abstract class MinionStateCommon<S0> : MinionState<S0>
        {
            protected MyTeamShare Shared => GetTeamShare<MyTeamShare>();

            // Generic states share the same projectile dodge via the static helper.
            protected bool DodgeIfThreatened()
            {
                return TryDodgeProjectile(Minion, TeamData);
            }
        }


        // Detect an incoming THROWN enemy ball on a collision course and dodge it.
        // Static so both the generic and non-generic state bases can use it. We react to the
        // actual projectile (not an opponent merely holding a ball) so we don't waste the
        // evade cooldown early. Returns true if an evade was performed.
        static bool TryDodgeProjectile(MinionScript minion, TeamShare teamData)
        {
            if (minion == null || teamData == null)
                return false;

            var balls = teamData.DBInfo;
            if (balls == null)
                return false;

            Vector3 myPos = minion.transform.position;

            foreach (var b in balls)
            {
                if (b.IsHeld)
                    continue;
                // Only enemy balls threaten us; ignore our own team's and loose neutral balls.
                if (b.State != PrisonDodgeballManager.DodgeballState.Opponent)
                    continue;

                Vector3 vel = b.Vel; vel.y = 0f;
                float speed = vel.magnitude;
                if (speed < MinThrownBallSpeed)
                    continue; // carried or barely moving, not a throw

                Vector3 rel = b.Pos - myPos; rel.y = 0f; // me -> ball
                // Time of the ball line's closest approach to us.
                float tStar = -Vector3.Dot(rel, vel) / (speed * speed);
                if (tStar < 0f || tStar > BallDodgeHorizon)
                    continue; // moving away, or too far in the future

                Vector3 closest = rel + vel * tStar; // me -> ball at closest approach
                if (closest.magnitude < BallDodgeRadius)
                {
                    // Step away from the ball's path. Map that world direction to the
                    // minion's Left (-right) / Right (+right).
                    Vector3 away = -closest;
                    if (away.sqrMagnitude < 1e-4f)
                        away = Vector3.Cross(Vector3.up, vel.normalized);

                    float d = Vector3.Dot(away.normalized, minion.transform.right);
                    var evadeDir = (d >= 0f)
                        ? MinionScript.EvasionDirection.Right
                        : MinionScript.EvasionDirection.Left;
                    return minion.Evade(evadeDir, 1f);
                }
            }

            return false;
        }


        // Go get a ball!
        class CollectBallState : MinionStateCommon
        {
            bool hasDestBall = false;
            PrisonDodgeballManager.DodgeballInfo destBall;

            public override string Name => CollectBallStateName;

            public override void Enter()
            {
                base.Enter();

                if (FindClosestAvailableDodgeball(out destBall))
                {
                    hasDestBall = true;
                    Minion.GoTo(destBall.NavMeshPos);
                }
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                DodgeIfThreatened();

                // could pick up a ball accidentally before getting to desired ball
                if (Minion.HasBall)
                    return ParentFSM.CreateStateTransition(GoToThrowSpotStateName);

                var dbInfo = TeamData?.DBInfo;
                if (dbInfo == null)
                    return null;

                if (hasDestBall)
                {
                    destBall = dbInfo[destBall.Index];

                    if (destBall.IsHeld || destBall.State != PrisonDodgeballManager.DodgeballState.Neutral || !destBall.Reachable)
                        hasDestBall = false;
                }

                if (!hasDestBall)
                {
                    if (FindClosestAvailableDodgeball(out destBall))
                        hasDestBall = true;
                }

                if (hasDestBall)
                {
                    // The ball might be moving, so keep updating. GoTo() avoids redundant A*.
                    Minion.GoTo(destBall.NavMeshPos);
                }
                else
                {
                    // No ball to grab, so focus on defense.
                    return ParentFSM.CreateStateTransition(DefensiveDemoStateName);
                }

                return null;
            }
        }


        // Get into a forward position to throw (or rescue a jailed teammate).
        class GoToThrowSpotState : MinionStateCommon
        {
            public override string Name => GoToThrowSpotStateName;

            public override void Enter()
            {
                base.Enter();
                Minion.GoTo(Mgr.TeamAdvance(Team).position);
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                DodgeIfThreatened();

                if (!Minion.HasBall)
                    return ParentFSM.CreateStateTransition(CollectBallStateName);

                // If we already have a clean shot on the way, take it immediately.
                if (FindBestThrowTarget(out _, out _, out _, out _))
                    return ParentFSM.CreateStateTransition(ThrowBallStateName);

                if (Minion.ReachedTarget())
                {
                    if (FindRescuableTeammate(out var m))
                        return ParentFSM.CreateStateTransition<MinionScript>(RescueStateName, m, true);

                    return ParentFSM.CreateStateTransition(ThrowBallStateName);
                }

                Minion.GoTo(Mgr.TeamAdvance(Team).position);
                return null;
            }
        }


        // Rescue a jailed buddy by throwing them a ball.
        class RescueState : MinionStateCommon<MinionScript>
        {
            MinionScript buddy;

            public override string Name => RescueStateName;

            public override void Enter(MinionScript m)
            {
                base.Enter(m);
                buddy = m;
                if (buddy != null)
                    Minion.FaceTowards(buddy.transform.position);
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                if (!Minion.HasBall)
                    return ParentFSM.CreateStateTransition(CollectBallStateName);

                if (buddy == null || !buddy.CanBeRescued)
                {
                    if (!FindRescuableTeammate(out buddy))
                        buddy = null;
                }

                if (buddy == null)
                    return ParentFSM.CreateStateTransition(ThrowBallStateName);

                var canThrow = ThrowMethods.PredictThrow(
                    Minion.HeldBallPosition, Minion.ThrowSpeed, Physics.gravity,
                    buddy.transform.position, buddy.Velocity, buddy.transform.forward,
                    MaxAllowedThrowPositionError,
                    out var dir, out var speed, out var interceptT, out var altT);

                var intercept = Minion.HeldBallPosition + dir * speed * interceptT;
                Minion.FaceTowardsForThrow(intercept);

                if (canThrow)
                {
                    var speedNorm = Mathf.Min(1f, speed / Minion.ThrowSpeed);
                    if (Minion.ThrowBall(dir, speedNorm))
                        return ParentFSM.CreateStateTransition(CollectBallStateName);
                }

                // Couldn't release the rescue throw yet (still aiming): dodge while we turn.
                DodgeIfThreatened();
                return null;
            }
        }


        // Throw the ball at the best available enemy. Aggressive but accurate.
        class ThrowBallState : MinionStateCommon
        {
            float enterTime;

            public override string Name => ThrowBallStateName;

            public override void Enter()
            {
                base.Enter();
                enterTime = Time.timeSinceLevelLoad;
                Minion.Stop();
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                if (!Minion.HasBall)
                    return ParentFSM.CreateStateTransition(CollectBallStateName);

                if (FindBestThrowTarget(out var dir, out var speedNorm, out var interceptPos, out var interceptT))
                {
                    enterTime = Time.timeSinceLevelLoad; // making progress; keep trying to land this shot
                    Minion.Stop();                       // settle to aim a high-confidence shot
                    Minion.FaceTowardsForThrow(interceptPos);

                    // Throwing is our edge: fire the instant we're aligned (don't dodge it away).
                    if (Minion.ThrowBall(dir, speedNorm))
                    {
                        Debug.Log($"[THROW t={Time.timeSinceLevelLoad:0.0} m={Minion.SpawnIndex}] iT={interceptT:0.00} fb=0"); // DIAG
                        return ParentFSM.CreateStateTransition(CollectBallStateName);
                    }

                    // Not aligned yet: dodge an incoming ball while we keep turning to aim.
                    DodgeIfThreatened();
                    return null;
                }

                // No clean shot yet. Survival first.
                DodgeIfThreatened();

                // Rescue a teammate if one needs help and we can't shoot anyone.
                if (FindRescuableTeammate(out var buddy))
                    return ParentFSM.CreateStateTransition<MinionScript>(RescueStateName, buddy, true);

                // FIRE-ANYWAY FALLBACK: if we've been holding the ball without a short shot for a
                // while, the opponents aren't coming into short range (they're sitting on their
                // back line, e.g. scarce-ball configs). Holding for a perfect short shot just
                // stalemates. Take the best REACHABLE shot (quickest intercept) instead -- against
                // a non-dodging opponent it's a free kill, and it converts stalls into action.
                float held = Time.timeSinceLevelLoad - enterTime;
                if (held > FireAnywayDelay &&
                    FindBestThrowTarget(out var ld, out var lsn, out var lip, out _,
                                        ThrowMaxInterceptTRelaxed))
                {
                    Minion.Stop();
                    Minion.FaceTowardsForThrow(lip);
                    if (Minion.ThrowBall(ld, lsn))
                    {
                        Debug.Log($"[THROW t={Time.timeSinceLevelLoad:0.0} m={Minion.SpawnIndex}] fb=1"); // DIAG
                        return ParentFSM.CreateStateTransition(CollectBallStateName);
                    }

                    DodgeIfThreatened(); // still turning to aim; survive meanwhile
                    return null;
                }

                // CLOSE THE DISTANCE to manufacture a short, high-hit-rate shot (preferred). The
                // navmesh clamps us to our legal area, so this advances as far forward as the rules
                // allow; we stop and fire the instant a shot opens up (handled above). Prefer
                // advancing on a DEFENSELESS opponent (no counter-throw risk), else press anyone.
                // Same rule for every config -- no per-ball-count special cases.
                if (NearestDefenselessOpponentPos(out var softPos))
                {
                    Minion.GoTo(softPos);
                }
                else if (NearestOpponentPos(out var anyPos))
                {
                    Minion.GoTo(anyPos);
                }
                else if (held > MaxThrowWaitSec)
                {
                    // No opponents to chase: fall back to defense.
                    return ParentFSM.CreateStateTransition(DefensiveDemoStateName);
                }

                return null;
            }
        }


        // Defensive behavior for ball-less minions: spread out, stay mobile, dodge throws.
        class DefensiveDemoState : MinionStateCommon
        {
            float lastReposition;

            public override string Name => DefensiveDemoStateName;

            // A spread-out position along the home line based on this minion's spawn index.
            Vector3 DefensiveSpot()
            {
                Vector3 home = Mgr.TeamHome(Team).position;
                Vector3 fwd = (Mgr.TeamCenter(Team).position - home);
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);

                int teamSize = Mathf.Max(1, TeamData != null ? TeamData.TeamSize : 1);
                float lateral = (Minion.SpawnIndex - (teamSize - 1) * 0.5f) * DefenseLateralSpacing;
                float jitter = (Random.value - 0.5f) * 2f;

                return home + right * lateral + fwd * (1.5f + jitter);
            }

            public override void Enter()
            {
                base.Enter();
                Minion.GoTo(DefensiveSpot());
                lastReposition = Time.timeSinceLevelLoad;
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                // Survive: dodge incoming throws first.
                DodgeIfThreatened();

                if (Minion.HasBall)
                    return ParentFSM.CreateStateTransition(GoToThrowSpotStateName);

                if (FindClosestAvailableDodgeball(out _))
                    return ParentFSM.CreateStateTransition(CollectBallStateName);

                // Stay mobile and re-spread periodically so we're not a sitting target.
                if (Minion.ReachedTarget() || Time.timeSinceLevelLoad - lastReposition > DefenseRepositionSec)
                {
                    Minion.GoTo(DefensiveSpot());
                    lastReposition = Time.timeSinceLevelLoad;
                }

                return null;
            }
        }


        // Go directly to jail. Do not pass go. Do not collect $200
        class GoToPrisonState : MinionStateCommon
        {
            int waypointIndex = 0;

            public override string Name => GoToPrisonStateName;

            public override void Enter()
            {
                base.Enter();
                waypointIndex = 0;
                Minion.GoTo(Mgr.TeamGutterEntranceLeft(Team).position);
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                if (!Minion.IsPrisoner)
                    return ParentFSM.CreateStateTransition(LeavePrisonStateName);

                if (Minion.ReachedTarget())
                {
                    if (waypointIndex == 0)
                    {
                        ++waypointIndex;
                        Minion.GoTo(Mgr.TeamGutterEndLeft(Team).position);
                    }
                    else if (waypointIndex == 1)
                    {
                        ++waypointIndex;
                        Minion.GoTo(Mgr.TeamPrison(Team).position);
                    }
                    else
                    {
                        // In prison: face the field so a rescue throw can reach us.
                        Minion.FaceTowards(Mgr.TeamHome(Team).position);
                    }
                }

                return null;
            }
        }


        // Freed! Exit via the gutter back to the field.
        class LeavePrisonState : MinionStateCommon
        {
            int waypointIndex = 0;

            public override string Name => LeavePrisonStateName;

            public override void Enter()
            {
                base.Enter();
                waypointIndex = 0;
                Minion.GoTo(Mgr.TeamGutterEndRight(Team).position);
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                if (Minion.ReachedTarget())
                {
                    if (waypointIndex == 0)
                    {
                        ++waypointIndex;
                        Minion.GoTo(Mgr.TeamGutterEntranceRight(Team).position);
                    }
                    else
                    {
                        if (Minion.HasBall)
                            return ParentFSM.CreateStateTransition(GoToThrowSpotStateName);
                        else
                            return ParentFSM.CreateStateTransition(GoHomeStateName);
                    }
                }

                return null;
            }
        }


        // Heading home (e.g. after a jailbreak).
        class GoHomeState : MinionStateCommon
        {
            public override string Name => GoHomeStateName;

            public override void Enter()
            {
                base.Enter();
                Minion.GoTo(Mgr.TeamHome(Team).position);
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                DodgeIfThreatened();

                if (Minion.ReachedTarget())
                    return ParentFSM.CreateStateTransition(CollectBallStateName);

                return null;
            }
        }


        // Game over: stand down.
        class RestState : MinionStateCommon
        {
            public override string Name => RestStateName;

            public override void Enter()
            {
                base.Enter();
                Minion.GoTo(Mgr.TeamHome(Team).position);
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                return null;
            }
        }


        // Co-state evaluated first every frame. Only initiates wildcard transitions.
        class GlobalTransitionState : MinionStateCommon
        {
            bool wasPrisoner = false;
            float lastDiag = -999f; // DIAG (remove before submission)

            public override string Name => GlobalTransitionStateName;

            public override StateTransitionBase<MinionFSMData> Update()
            {
                DiagSnapshot(); // DIAG (remove before submission)

                if (Mgr.IsGameOver && !ParentFSM.CurrentState.Name.Equals(RestStateName))
                {
                    return ParentFSM.CreateStateTransition(RestStateName);
                }
                else if (Minion.IsPrisoner && !wasPrisoner)
                {
                    wasPrisoner = true;
                    Debug.Log($"[JAIL t={Time.timeSinceLevelLoad:0.0} m={Minion.SpawnIndex} team={Team}]"); // DIAG
                    return ParentFSM.CreateStateTransition(GoToPrisonStateName);
                }
                else if (!Minion.IsPrisoner && wasPrisoner)
                {
                    wasPrisoner = false;
                }

                return null;
            }

            // DIAG (remove before submission): the SpawnIndex==0 minion logs the whole-match state
            // every ~3s so we can read the elimination race + ball control from the exported XML.
            void DiagSnapshot()
            {
                if (Minion.SpawnIndex != 0) return;
                float now = Time.timeSinceLevelLoad;
                if (now - lastDiag < 3f) return;
                lastDiag = now;

                int myFree = 0, myJail = 0, myBall = 0;
                if (TeamData?.TeamMates != null)
                    foreach (var m in TeamData.TeamMates)
                    {
                        if (m == null) continue;
                        if (m.IsPrisoner) myJail++; else myFree++;
                        if (m.HasBall) myBall++;
                    }

                int oppFree = 0, oppJail = 0, oppBall = 0;
                var opps = Shared?.OppInfo;
                if (opps != null)
                    foreach (var o in opps)
                    {
                        if (o.IsPrisoner) oppJail++; else oppFree++;
                        if (o.HasBall) oppBall++;
                    }

                string balls = "";
                var db = TeamData?.DBInfo;
                if (db != null)
                    foreach (var b in db)
                        balls += $"[{b.State} h{(b.IsHeld ? 1 : 0)} r{(b.Reachable ? 1 : 0)}]";

                Debug.Log($"[DIAG t={now:0.0} team={Team}] ME free={myFree} jail={myJail} ball={myBall} | OPP free={oppFree} jail={oppJail} ball={oppBall} | {balls}");
            }
        }


        protected override void ConfigureFSM(FiniteStateMachine<MinionFSMData> fsm)
        {
            fsm.SetGlobalTransitionState(new GlobalTransitionState());

            fsm.AddState(new CollectBallState(), true);
            fsm.AddState(new GoToThrowSpotState());
            fsm.AddState(new ThrowBallState());
            fsm.AddState(new DefensiveDemoState());
            fsm.AddState(new GoToPrisonState());
            fsm.AddState(new LeavePrisonState());
            fsm.AddState(new GoHomeState());
            fsm.AddState(new RescueState());
            fsm.AddState(new RestState());
        }

    }
}
