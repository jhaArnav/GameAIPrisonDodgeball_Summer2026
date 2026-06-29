using UnityEngine;

using GameAI;

namespace GameAIStudent
{
    public abstract class BaseMinionStateMachine : MonoBehaviour
    {
        public MinionScript Minion { get; private set; }
        public TeamShare TeamData { get; private set; }
        protected PrisonDodgeballManager Mgr { get; private set; }

        protected FiniteStateMachine<MinionFSMData> fsm;

        protected abstract string StudentNameText { get; }

        public struct MinionFSMData
        {
            public BaseMinionStateMachine MinionFSM { get; private set; }
            public MinionScript Minion { get; private set; }
            public PrisonDodgeballManager Mgr { get; private set; }
            public PrisonDodgeballManager.Team Team { get; private set; }
            public TeamShare TeamData { get; private set; }

            public MinionFSMData(
                BaseMinionStateMachine minionFSM,
                MinionScript minion,
                PrisonDodgeballManager mgr,
                PrisonDodgeballManager.Team team,
                TeamShare teamData
                )
            {
                MinionFSM = minionFSM;
                Minion = minion;
                Mgr = mgr;
                Team = team;
                TeamData = teamData;
            }
        }

        // Create a base class for states to access the FSM and shared data.
        // Derived student FSMs can reuse this without copying boilerplate.
        protected abstract class MinionStateBase
        {
            public virtual string Name => throw new System.NotImplementedException();

            protected IFiniteStateMachine<MinionFSMData> ParentFSM;
            protected BaseMinionStateMachine MinionFSM;
            protected MinionScript Minion;
            protected PrisonDodgeballManager Mgr;
            protected PrisonDodgeballManager.Team Team;
            protected TeamShare TeamData;

            protected T GetTeamShare<T>() where T : TeamShare
            {
                return TeamData as T;
            }

            public virtual void Init(IFiniteStateMachine<MinionFSMData> parentFSM,
                MinionFSMData minFSMData)
            {
                ParentFSM = parentFSM;
                MinionFSM = minFSMData.MinionFSM;
                Minion = minFSMData.Minion;
                Mgr = minFSMData.Mgr;
                Team = minFSMData.Team;
                TeamData = minFSMData.TeamData;
            }


            protected bool FindClosestAvailableDodgeball(
                out PrisonDodgeballManager.DodgeballInfo dodgeballInfo)
            {

                var dist = float.MaxValue;
                bool found = false;

                dodgeballInfo = default;

                if (TeamData == null)
                    return false;

                var dbInfo = TeamData.DBInfo;

                if (dbInfo == null)
                    return false;

                foreach (var db in dbInfo)
                {
                    if (!db.IsHeld && db.State == PrisonDodgeballManager.DodgeballState.Neutral && db.Reachable)
                    {
                        var d = Vector3.Distance(db.Pos, Minion.transform.position);

                        if (d < dist)
                        {
                            found = true;
                            dist = d;
                            dodgeballInfo = db;
                        }

                    }
                }

                return found;
            }


            public bool FindRescuableTeammate(out MinionScript firstHelplessMinion)
            {

                firstHelplessMinion = null;

                if (TeamData == null)
                    return false;

                var teammates = TeamData.TeamMates;

                if (teammates == null)
                    return false;

                foreach (var m in teammates)
                {
                    if (m == null)
                        continue;

                    if (m.CanBeRescued)
                    {
                        firstHelplessMinion = m;
                        return true;
                    }
                }
                return false;
            }

            protected void InternalEnter()
            {
                MinionFSM.Minion.DisplayText(Name);
            }

            // globalTransition parameter is to notify if transition was triggered
            // by a global transition (wildcard)
            public virtual void Exit(bool globalTransition) { }
            public virtual void Exit() { Exit(false); }

            public virtual StateTransitionBase<MinionFSMData> Update()
            {
                return null;
            }
        }

        protected abstract class MinionState : MinionStateBase, IState<MinionFSMData>
        {
            public virtual void Enter() { InternalEnter(); }
        }

        protected abstract class MinionState<S0> : MinionStateBase, IState<MinionFSMData, S0>
        {
            public virtual void Enter(S0 s) { InternalEnter(); }
        }

        protected abstract class MinionState<S0, S1> : MinionStateBase, IState<MinionFSMData, S0, S1>
        {
            public virtual void Enter(S0 s0, S1 s1) { InternalEnter(); }
        }

        protected virtual void Awake()
        {
            Minion = GetComponent<MinionScript>();

            if (Minion == null)
            {
                Debug.LogWarning("No minion script");
            }
        }

        protected virtual void Start()
        {
            Mgr = PrisonDodgeballManager.Instance;

            InitTeamData();

            var minionFSMData = new MinionFSMData(this, Minion, Mgr, Minion.Team, TeamData);

            fsm = new FiniteStateMachine<MinionFSMData>(minionFSMData);

            ConfigureFSM(fsm);
        }

        protected virtual void Update()
        {
            // Don't start until all the team is ready to go
            if (TeamData == null || !TeamData.IsFullyInitialized)
                return;

            fsm.Update();
        }

        protected virtual void ConfigureFSM(FiniteStateMachine<MinionFSMData> fsm)
        {
        }

        protected virtual TeamShare CreateTeamShare(PrisonDodgeballManager.Team team, int teamSize, int numBalls)
        {
            return new TeamShare(team, teamSize, numBalls);
        }

        protected virtual void OnTeamShareInitialized(TeamShare teamShare)
        {
        }

        protected void InitTeamData()
        {
            Mgr.SetTeamText(Minion.Team, StudentNameText);

            var o = Mgr.GetTeamDataShare(Minion.Team);

            if (o == null)
            {
                TeamData = CreateTeamShare(Minion.Team, Mgr.TeamSize, Mgr.TotalBalls);
                Mgr.SetTeamDataShare(Minion.Team, TeamData);
            }
            else
            {
                TeamData = o as TeamShare;

                if (TeamData == null)
                {
                    Debug.LogWarning("TeamData is null!");
                }
            }

            TeamData?.AddTeamMember(Minion);
            OnTeamShareInitialized(TeamData);
        }
    }


    // Simple demo of shared info amongst the team
    // You can modify this as necessary for advanced team strategy
    // Tracking teammates is added to get you started.
    // Also, some expensive queries of opponent and dodgeballs are
    // shared across the team
    public class TeamShare
    {
        public PrisonDodgeballManager.Team Team { get; private set; }
        public MinionScript[] TeamMates { get; private set; }
        public int TeamSize { get; private set; }
        public int NumBalls { get; private set; }
        protected int currTeamMateRegSpot = 0;

        // These are used to track whether data is stale
        protected float timeOfDBQuery = float.MinValue;

        protected PrisonDodgeballManager.DodgeballInfo[] dbInfo;

        public PrisonDodgeballManager.DodgeballInfo[] DBInfo
        {
            get
            {
                var t = Time.timeSinceLevelLoad;

                if (t != timeOfDBQuery)
                {
                    timeOfDBQuery = t;
                    PrisonDodgeballManager.Instance.GetAllDodgeballInfo(Team, ref dbInfo, true);
                }

                return dbInfo;
            }

            private set { dbInfo = value; }
        }

        public TeamShare(PrisonDodgeballManager.Team team, int teamSize, int numBalls)
        {
            Team = team;
            TeamSize = teamSize;
            NumBalls = numBalls;
            TeamMates = new MinionScript[TeamSize];

            DBInfo = new PrisonDodgeballManager.DodgeballInfo[NumBalls];
        }

        public void AddTeamMember(MinionScript m)
        {
            TeamMates[currTeamMateRegSpot] = m;
            ++currTeamMateRegSpot;
        }

        public bool IsFullyInitialized
        {
            get => currTeamMateRegSpot >= TeamSize;
        }
    }
}
