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

        // Example of extending TeamShare with custom data ("blackboard" style).
        // Add your own fields/methods here. In states, access it via GetTeamShare<ExampleTeamShare>().
        class ExampleTeamShare : TeamShare
        {
            // Example data: a label and a timestamp of when the share was created.
            public string TeamLabel { get; private set; }
            public float CreatedAtTime { get; private set; }

            public ExampleTeamShare(PrisonDodgeballManager.Team team, int teamSize, int numBalls)
                : base(team, teamSize, numBalls)
            {
                TeamLabel = $"Team {team} Share";
                CreatedAtTime = Time.timeSinceLevelLoad;
            }
        }

        // Override this method to provide your own TeamShare subtype with extra data.
        protected override TeamShare CreateTeamShare(PrisonDodgeballManager.Team team, int teamSize, int numBalls)
        {
            return new ExampleTeamShare(team, teamSize, numBalls);
        }

        // Example of extending the base state types for shared helpers.
        // Add common state utilities here and inherit from MinionStateCommon below.
        abstract class MinionStateCommon : MinionState
        {
            // Example helper for accessing custom TeamShare data in any state.
            protected ExampleTeamShare Shared => GetTeamShare<ExampleTeamShare>();
        }

        // Generic variants so parameterized states can share helpers too.
        abstract class MinionStateCommon<S0> : MinionState<S0>
        {
            protected ExampleTeamShare Shared => GetTeamShare<ExampleTeamShare>();
        }

        abstract class MinionStateCommon<S0, S1> : MinionState<S0, S1>
        {
            protected ExampleTeamShare Shared => GetTeamShare<ExampleTeamShare>();
        }

        // Go get a ball!
        class CollectBallState : MinionStateCommon
        {
            public override string Name => CollectBallStateName;

            bool hasDestBall = false;
            PrisonDodgeballManager.DodgeballInfo destBall;


            public override void Init(IFiniteStateMachine<MinionFSMData> parentFSM, MinionFSMData minFSMData)
            {
                base.Init(parentFSM, minFSMData);

            }

            public override void Enter()
            {
                base.Enter();

                if (FindClosestAvailableDodgeball(out destBall))
                {
                    hasDestBall = true;
                    Minion.GoTo(destBall.Pos);
                }

            }

            public override void Exit(bool globalTransition)
            {

            }

            public override StateTransitionBase<MinionFSMData> Update()
            {

                // could pick up a ball accidentally before getting to desired ball
                if (Minion.HasBall)
                    return ParentFSM.CreateStateTransition(GoToThrowSpotStateName);

                var dbInfo = TeamData.DBInfo;

                if (dbInfo == null)
                    return null;

                if (hasDestBall)
                {
                    destBall = dbInfo[destBall.Index];

                    if (destBall.IsHeld || destBall.State != PrisonDodgeballManager.DodgeballState.Neutral || !destBall.Reachable)
                    {
                        hasDestBall = false;
                    }

                }

                if (!hasDestBall)
                {
                    if (FindClosestAvailableDodgeball(out destBall))
                    {
                        hasDestBall = true;

                    }
                }

                if (hasDestBall)
                {
                    // The ball might be moving, so keep updating. GoTo() is smart enough
                    // to not keep performing full A* if it doesn't need to, so safe to call often.
                    Minion.GoTo(destBall.NavMeshPos);
                }
                else
                {
                    // No ball, so focus on defense
                    return ParentFSM.CreateStateTransition(DefensiveDemoStateName);
                }

                return null;
            }
        }


        // This state gets the minion close to the enemy for a throw (or a rescue of a buddy)
        class GoToThrowSpotState : MinionStateCommon
        {
            public override string Name => GoToThrowSpotStateName;


            public override void Init(IFiniteStateMachine<MinionFSMData> parentFSM, MinionFSMData minFSMData)
            {
                base.Init(parentFSM, minFSMData);
            }

            public override void Enter()
            {
                base.Enter();

                Minion.GoTo(Mgr.TeamAdvance(Team).position);
            }

            public override void Exit(bool globalTransition)
            {

            }

            public override StateTransitionBase<MinionFSMData> Update()
            {

                // just in case something bad happened
                if (!Minion.HasBall)
                {
                    return ParentFSM.CreateStateTransition(CollectBallStateName);
                }

                if (Minion.ReachedTarget())
                {
                    if (FindRescuableTeammate(out var m))
                    {
                        return ParentFSM.CreateStateTransition<MinionScript>(RescueStateName, m, true);
                    }
                    else
                        return ParentFSM.CreateStateTransition(ThrowBallStateName);
                }

                return null;
            }
        }


        // Rescue a buddy
        class RescueState : MinionStateCommon<MinionScript>
        {
            public override string Name => RescueStateName;

            MinionScript buddy;

            public override void Init(IFiniteStateMachine<MinionFSMData> parentFSM, MinionFSMData minFSMData)
            {
                base.Init(parentFSM, minFSMData);
            }

            public override void Enter(MinionScript m)
            {
                base.Enter(m);

                buddy = m;

                Minion.FaceTowards(buddy.transform.position);

            }

            public override void Exit(bool globalTransition)
            {

            }

            public override StateTransitionBase<MinionFSMData> Update()
            {

                // just in case something bad happened
                if (!Minion.HasBall)
                {
                    return ParentFSM.CreateStateTransition(CollectBallStateName);
                }

                if (buddy == null || !buddy.CanBeRescued)
                {

                    if (!FindRescuableTeammate(out buddy))
                    {
                        buddy = null;
                    }

                }

                // Nothing to do without buddy in prison...
                if (buddy == null)
                    // we should have a ball still...
                    return ParentFSM.CreateStateTransition(ThrowBallStateName);


                var canThrow = ThrowMethods.PredictThrow(Minion.HeldBallPosition, Minion.ThrowSpeed, Physics.gravity, buddy.transform.position,
                        buddy.Velocity, buddy.transform.forward, MaxAllowedThrowPositionError,
                        out var univVDir, out var speedScalar, out var interceptT, out var altT);


                var intercept = Minion.HeldBallPosition + univVDir * speedScalar * interceptT;
                Minion.FaceTowardsForThrow(intercept);

                if (canThrow)
                {
                    var speedNorm = speedScalar / Minion.ThrowSpeed;

                    if (Minion.ThrowBall(univVDir, speedNorm))
                        return ParentFSM.CreateStateTransition(CollectBallStateName);
                }

                return null;
            }
        }


        // Throw the ball at the enemy
        class ThrowBallState : MinionStateCommon
        {
            public override string Name => ThrowBallStateName;

            int opponentIndex = -1;
            PrisonDodgeballManager.OpponentInfo opponentInfo;
            bool hasOpponent = false;

            public override void Init(IFiniteStateMachine<MinionFSMData> parentFSM, MinionFSMData minFSMData)
            {
                base.Init(parentFSM, minFSMData);
            }


            public override void Enter()
            {
                base.Enter();

                if (Mgr.FindClosestNonPrisonerOpponentIndex(Minion.transform.position, Team, out opponentIndex))
                {
                    if (hasOpponent = Mgr.GetOpponentInfo(Team, opponentIndex, out opponentInfo))
                    {
                        Minion.FaceTowards(opponentInfo.Pos);
                    }
                }
            }

            public override void Exit(bool globalTransition)
            {

            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                // just in case something bad happened
                if (!Minion.HasBall)
                {
                    return ParentFSM.CreateStateTransition(CollectBallStateName);
                }

                // Check if opponent still valid
                if (
                    !(hasOpponent = Mgr.GetOpponentInfo(Team, opponentIndex, out opponentInfo)) ||
                    opponentInfo.IsPrisoner || opponentInfo.IsFreedPrisoner)
                {

                    if (Mgr.FindClosestNonPrisonerOpponentIndex(Minion.transform.position, Team, out opponentIndex))
                    {
                        hasOpponent = Mgr.GetOpponentInfo(Team, opponentIndex, out opponentInfo);
                    }
                }

                // Nothing to do without opponent...
                if (!hasOpponent)
                    return ParentFSM.CreateStateTransition(DefensiveDemoStateName);


                int navmask = NavMesh.AllAreas;


                if (!Mgr.ThrowTestEnabled || Mgr.ThrowTestRestrictTargetToSideEnabled)
                {
                    int oppTeamNavMask = 0;

                    if (Minion.Team == PrisonDodgeballManager.Team.TeamA)
                        oppTeamNavMask = Mgr.TeamBNavMeshAreaIndex;
                    else
                        oppTeamNavMask = Mgr.TeamANavMeshAreaIndex;

                    navmask = (1 << Mgr.NeutralNavMeshAreaIndex) | (1 << oppTeamNavMask) |
                                    (1 << Mgr.WalkableNavMeshAreaIndex);

                }


                var selection = ShotSelection.SelectThrow(Minion, opponentInfo, navmask, MaxAllowedThrowPositionError, Time.deltaTime, out var projectileDir, out var projectileSpeed, out var interceptT, out var interceptPos);

                if (selection == ShotSelection.SelectThrowReturn.DoThrow)
                {
                    var speedFactor = Mathf.Min(1f, projectileSpeed / Minion.ThrowSpeed);
                    var throwRes = Minion.ThrowBall(projectileDir, speedFactor);

                    if (throwRes)
                    {
                        Minion.FaceTowardsForThrow(interceptPos);

                        return ParentFSM.CreateStateTransition(CollectBallStateName);
                    }
                    else
                    {
                        //Debug.Log("COULDN'T THROW!");
                    }
                }

                Vector3 intercept;
                if (selection == ShotSelection.SelectThrowReturn.NoThrowTargettingFailed)
                    intercept = opponentInfo.Pos;
                else
                    intercept = interceptPos;

                Minion.FaceTowardsForThrow(intercept);


                return null;
            }
        }


        // A not very effective defensive strategy. Mainly a demonstration of calling
        // Minion.Evade()
        class DefensiveDemoState : MinionStateCommon
        {
            public override string Name => DefensiveDemoStateName;

            float lastEvade;
            float evadeWaitTimeSec;
            bool doPause = false;
            float pauseStart;
            float pauseDuration;

            public override void Init(IFiniteStateMachine<MinionFSMData> parentFSM, MinionFSMData minFSMData)
            {
                base.Init(parentFSM, minFSMData);
            }


            protected bool RandomGoTo()
            {
                var r = Minion.GoTo(Mgr.TeamHome(Team).position + 6f * (new Vector3(Random.value, 0f, Random.value)));

                if (!r)
                {
                    Debug.LogWarning("Could not GOTO in DefenseDemoState");
                }

                return r;
            }



            public override void Enter()
            {
                base.Enter();

                RandomGoTo();

                lastEvade = Time.timeSinceLevelLoad;

                evadeWaitTimeSec = 2f * Minion.EvadeCoolDownTimeSec + 0.1f;
            }

            public override void Exit(bool globalTransition)
            {

            }

            public override StateTransitionBase<MinionFSMData> Update()
            {

                if (Minion.HasBall)
                    return ParentFSM.CreateStateTransition(GoToThrowSpotStateName);

                PrisonDodgeballManager.DodgeballInfo ball;

                if (FindClosestAvailableDodgeball(out ball))
                {
                    return ParentFSM.CreateStateTransition(CollectBallStateName);
                }

                if (!doPause && Minion.ReachedTarget())
                {
                    pauseStart = Time.timeSinceLevelLoad;
                    doPause = true;
                    pauseDuration = Random.value * 3f;
                }

                if (doPause)
                {
                    Minion.FaceTowards(Mgr.TeamPrison(Team).position);

                    if (Time.timeSinceLevelLoad - pauseStart >= pauseDuration)
                    {
                        doPause = false;
                        RandomGoTo();
                    }
                }
                else if (Time.timeSinceLevelLoad - lastEvade >= evadeWaitTimeSec)
                {

                    lastEvade = Time.timeSinceLevelLoad;

                    var r = Random.Range(0, 3);

                    MinionScript.EvasionDirection ev;

                    switch (r)
                    {
                        case 0:
                            ev = MinionScript.EvasionDirection.Brake;
                            break;
                        case 1:
                            ev = MinionScript.EvasionDirection.Left;
                            break;
                        case 2:
                            ev = MinionScript.EvasionDirection.Right;
                            break;
                        default:
                            ev = MinionScript.EvasionDirection.Brake;
                            break;
                    }

                    Minion.Evade(ev, Random.Range(0.6f, 1.0f));
                }

                return null;
            }
        }


        // Go directly to jail. Do not pass go. Do not collect $200 
        class GoToPrisonState : MinionStateCommon
        {
            public override string Name => GoToPrisonStateName;

            int waypointIndex = 0;


            public override void Init(IFiniteStateMachine<MinionFSMData> parentFSM, MinionFSMData minFSMData)
            {
                base.Init(parentFSM, minFSMData);
            }


            public override void Enter()
            {
                base.Enter();

                waypointIndex = 0;

                Minion.GoTo(Mgr.TeamGutterEntranceLeft(Team).position);
            }

            public override void Exit(bool globalTransition)
            {

            }

            public override StateTransitionBase<MinionFSMData> Update()
            {

                if (!Minion.IsPrisoner)
                {
                    return ParentFSM.CreateStateTransition(LeavePrisonStateName);
                }

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
                        Minion.FaceTowards(Mgr.TeamHome(Team).position);
                    }
                }

                return null;
            }
        }

        // Free! 
        class LeavePrisonState : MinionStateCommon
        {
            public override string Name => LeavePrisonStateName;

            int waypointIndex = 0;

            public override void Init(IFiniteStateMachine<MinionFSMData> parentFSM, MinionFSMData minFSMData)
            {
                base.Init(parentFSM, minFSMData);
            }


            public override void Enter()
            {
                base.Enter();

                waypointIndex = 0;

                Minion.GoTo(Mgr.TeamGutterEndRight(Team).position);
            }

            public override void Exit(bool globalTransition)
            {

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


        // Going home. Maybe after a jailbreak
        class GoHomeState : MinionStateCommon
        {
            public override string Name => GoHomeStateName;

            public override void Init(IFiniteStateMachine<MinionFSMData> parentFSM, MinionFSMData minFSMData)
            {
                base.Init(parentFSM, minFSMData);
            }


            public override void Enter()
            {
                base.Enter();

                if (!Minion.GoTo(Mgr.TeamHome(Team).position))
                {
                    Debug.LogWarning($"Could not find a way home! NavMesh Mask: {Minion.NavMeshMaskToString()}");
                }
            }

            public override void Exit(bool globalTransition)
            {

            }

            public override StateTransitionBase<MinionFSMData> Update()
            {

                if (Minion.ReachedTarget())
                {
                    return ParentFSM.CreateStateTransition(CollectBallStateName);
                }

                return null;
            }
        }


        class RestState : MinionStateCommon
        {
            public override string Name => RestStateName;

            public override void Enter()
            {
                base.Enter();

                if (!Minion.GoTo(Mgr.TeamHome(Team).position))
                {
                    Debug.LogWarning($"Could not find a way home! NavMesh Mask: {Minion.NavMeshMaskToString()}");
                }
            }

            public override StateTransitionBase<MinionFSMData> Update()
            {
                return null;
            }
        }


        // This is a special state that never exits. It coexists with the current state.
        // It's always evaluated first. It's only job is supposed to identify global/wildcard
        // transitions (it shouldn't do anything that modifies anything externally other than
        // return a desired transition).
        class GlobalTransitionState : MinionStateCommon
        {
            public override string Name => GlobalTransitionStateName;

            bool wasPrisioner = false;

            public override void Init(IFiniteStateMachine<MinionFSMData> parentFSM, MinionFSMData minFSMData)
            {
                base.Init(parentFSM, minFSMData);
            }


            public override void Enter()
            {
                base.Enter();
            }

            // The global state never exits
            //public override void Exit(bool globalTransition)
            //{
            //}

            public override StateTransitionBase<MinionFSMData> Update()
            {

                if (Mgr.IsGameOver && !ParentFSM.CurrentState.Name.Equals(RestStateName))
                {
                    return ParentFSM.CreateStateTransition(RestStateName);
                }
                else if (Minion.IsPrisoner && !wasPrisioner)
                {
                    // Just switched to prisoner! Uh oh. Gotta head to prison. :-(

                    wasPrisioner = true;

                    return ParentFSM.CreateStateTransition(GoToPrisonStateName);
                }
                else if (!Minion.IsPrisoner && wasPrisioner)
                {
                    wasPrisioner = false;
                }

                return null;
            }
        }


        protected override void ConfigureFSM(FiniteStateMachine<MinionFSMData> fsm)
        {
            // Handles global/wildcard transitions. This state is a co-state that
            // never exits. Triggered transitions only change the current state.
            // The global state should only handle initiating transitions
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

            //MinionStateMachine, GameAIStudentWork, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
            //Debug.Log(this.GetType().AssemblyQualifiedName);
        }

    }
}
