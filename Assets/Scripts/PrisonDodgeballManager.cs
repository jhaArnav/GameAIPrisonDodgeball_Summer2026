using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.IO;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

[DefaultExecutionOrder(5)]
public class PrisonDodgeballManager : MonoBehaviour
{
    public enum DodgeballSimulationMode
    {
        FPS_60_1X_RealTime,
        FPS_60_1X_SimTime,
    };
    public DodgeballSimulationMode dodgeballSimulationMode = DodgeballSimulationMode.FPS_60_1X_RealTime;
    public static bool OverrideConfiguration = false;
    public static string Override_TeamAAssemblyQualifiedName = "";
    public static string Override_TeamBAssemblyQualifiedName = "";
    public static int Override_teamSize = 3;
    public static int Override_ballsPerTeam = 3;
    public static int Override_matchLengthSec = 180;
    public static DodgeballSimulationMode Override_dodgeballSimulationMode = DodgeballSimulationMode.FPS_60_1X_RealTime;
    public static Dictionary<string, int> WinsByTeamAssembly = new Dictionary<string, int>();
    public static int TotalMatches { get; private set; }
    public static bool TeamsReversed { get; private set; }
    // Tournament-wide static state
    static bool TournamentInitialized = false;
    static TournamentPhase CurrentPhase = TournamentPhase.None;
    // Current round (for rounds above semifinals)
    static List<string> CurrentRoundTeams;
    static List<string> NextRoundTeams;
    static int CurrentRoundMatchIndex;
    // Semifinal / final / 3rd place bookkeeping
    static string SemiWinner1, SemiWinner2;
    static string SemiLoser1, SemiLoser2;
    // Current best-of-3 series state
    static string SeriesTeam1, SeriesTeam2;
    static int SeriesWins1, SeriesWins2;
    static int SeriesMatchIndex; // counts matches in this series (including ties)
    // Tiebreak level (1–4) and tie tracking
    static int CurrentLevel = 1;
    static int ConsecutiveTiesAtCurrentLevel = 0;


    // Tracks whether we have already emitted the leading series-start marker
    static bool hasShownSeriesIntroMarker = false;


    public enum Team
    {
        TeamA,
        TeamB
    }

    public enum TournamentPhase
    {
        None,
        RoundsAboveSemis,
        Semifinal1,
        Semifinal2,
        ThirdPlace,
        Championship,
        Completed
    }

    public const string ArenaLayerName = "Arena";
    public const string MinionTeamALayerName = "MinionTeamA";
    public const string MinionTeamBLayerName = "MinionTeamB";
    public const string BallTeamALayerName = "BallTeamA";
    public const string BallTeamBLayerName = "BallTeamB";
    public const string BallNeutralLayerName = "BallNeutral";
    public const string PrisonerTeamALayerName = "PrisonerTeamA";
    public const string PrisonerTeamBLayerName = "PrisonerTeamB";
    public const string NavMeshCarverLayerName = "NavmeshCarver";

    public int ArenaLayerLayerIndex { get; private set; }
    public int MinionTeamALayerIndex { get; private set; }
    public int MinionTeamBLayerIndex { get; private set; }
    public int BallTeamALayerIndex { get; private set; }
    public int BallTeamBLayerIndex { get; private set; }
    public int BallNeutralLayerIndex { get; private set; }
    public int PrisonerTeamALayerIndex { get; private set; }
    public int PrisonerTeamBLayerIndex { get; private set; }
    public int NavMeshCarverLayerIndex { get; private set; }

    public const string TeamANavMeshAreaName = "TeamA";
    public const string TeamBNavMeshAreaName = "TeamB";
    public const string NeutralNavMeshAreaName = "Neutral";
    public const string WalkableNavMeshAreaName = "Walkable";
    public const string TeamAPrisonNavMeshAreaName = "TeamAPrison";
    public const string TeamBPrisonNavMeshAreaName = "TeamBPrison";

    public int TeamANavMeshAreaIndex { get; private set; }
    public int TeamBNavMeshAreaIndex { get; private set; }
    public int NeutralNavMeshAreaIndex { get; private set; }
    public int WalkableNavMeshAreaIndex { get; private set; }
    public int TeamAPrisonNavMeshAreaIndex { get; private set; }
    public int TeamBPrisonNavMeshAreaIndex { get; private set; }

    [SerializeField]
    Text TeamAUIText = null;

    [SerializeField]
    Text TeamBUIText = null;

    [SerializeField]
    Text TeamAUIWinsText = null;

    [SerializeField]
    Text TeamBUIWinsText = null;

    [SerializeField]
    Text MatchOutputText = null;

    [SerializeField]
    Color TeamATextColor = Color.green;

    [SerializeField]
    Color TeamBTextColor = Color.blue;

    [SerializeField]
    Color NeutralTextColor = Color.white;

    [SerializeField]
    string TeamAAssemblyQualifiedName = "";

    [SerializeField]
    string TeamBAssemblyQualifiedName = "";

    [SerializeField]
    bool tournamentMode = false;


    [SerializeField]
    GameObject TournamentCanvas;

    // Full-screen magenta overlay used as a deterministic sync marker in captures
    [SerializeField]
    GameObject TournamentSyncMarkerCanvas;

    [SerializeField]
    bool TournamentEnableSyncMarkers = true;

    [SerializeField]
    int TournamentSyncMarkerFrames = 2;


    [SerializeField]
    TMPro.TextMeshProUGUI TournamentMatchDetail;

    [SerializeField]
    TMPro.TextMeshProUGUI TournamentDetail2;

    [SerializeField]
    TMPro.TextMeshProUGUI TournamentTeamA;

    [SerializeField]
    TMPro.TextMeshProUGUI TournamentTeamB;

    private const float TournamentAnnouncementDuration = 5f;

    [SerializeField]
    float tournamentSettleMinSeconds = 5f;

    [SerializeField]
    float tournamentSettleMaxSeconds = 15f;

    // Internal timer for post-game settle phase in tournament mode
    float tournamentSettleStartTime = -1f;

    [SerializeField] private bool enableMatchLogging = false;
    private static string logFilePath;
    private static bool logFileInitialized = false;

    // Ordered list of competitors (assembly-qualified names), length must be a power of 2.
    [SerializeField]
    string[] tournamentAssemblies = new string[0];

    [SerializeField]
    bool ThrowTest = false;

    [SerializeField]
    bool ThrowTestResetMinionPos = false;

    [SerializeField]
    bool ThrowTestRestrictTargetToSide = false;

    [SerializeField]
    float ThrowTestBallRequestInterval = 0.2f;

    [SerializeField]
    Transform TeamAGutterEntranceRight = default;
    [SerializeField]
    Transform TeamAGutterEntranceLeft = default;

    [SerializeField]
    Transform TeamAGutterEndRight = default;
    [SerializeField]
    Transform TeamAGutterEndLeft = default;

    [SerializeField]
    Transform TeamAPrison = default;

    [SerializeField]
    Transform TeamAHome = default;

    [SerializeField]
    Transform TeamAAdvance = default;

    [SerializeField]
    Transform TeamACenter = default;

    [SerializeField]
    Transform TeamBGutterEntranceRight = default;
    [SerializeField]
    Transform TeamBGutterEntranceLeft = default;

    [SerializeField]
    Transform TeamBGutterEndRight = default;
    [SerializeField]
    Transform TeamBGutterEndLeft = default;

    [SerializeField]
    Transform TeamBPrison = default;

    [SerializeField]
    Transform TeamBHome = default;

    [SerializeField]
    Transform TeamBAdvance = default;

    [SerializeField]
    Transform TeamBCenter = default;

    public bool ThrowTestEnabled
    {
        get => ThrowTest;
    }

    public bool ThrowTestRestrictTargetToSideEnabled
    {
        get => ThrowTestRestrictTargetToSide;
    }

    public Transform TeamGutterEntranceRight(Team team)
    {
        if (team == Team.TeamA)
            return TeamAGutterEntranceRight;
        else
            return TeamBGutterEntranceRight;
    }


    public Transform TeamGutterEntranceLeft(Team team)
    {
        if (team == Team.TeamA)
            return TeamAGutterEntranceLeft;
        else
            return TeamBGutterEntranceLeft;
    }

    public Transform TeamGutterEndRight(Team team)
    {
        if (team == Team.TeamA)
            return TeamAGutterEndRight;
        else
            return TeamBGutterEndRight;
    }


    public Transform TeamGutterEndLeft(Team team)
    {
        if (team == Team.TeamA)
            return TeamAGutterEndLeft;
        else
            return TeamBGutterEndLeft;
    }

    public Transform TeamPrison(Team team)
    {
        if (team == Team.TeamA)
            return TeamAPrison;
        else
            return TeamBPrison;
    }

    public Transform TeamHome(Team team)
    {
        if (team == Team.TeamA)
            return TeamAHome;
        else
            return TeamBHome;
    }

    public Transform TeamAdvance(Team team)
    {
        if (team == Team.TeamA)
            return TeamAAdvance;
        else
            return TeamBAdvance;
    }

    public Transform TeamCenter(Team team)
    {
        if (team == Team.TeamA)
            return TeamACenter;
        else
            return TeamBCenter;
    }

    public Transform TeamSpawn(Team team, int i)
    {
        if (team == Team.TeamA)
            return TeamASpawnLocations[i];
        else
            return TeamBSpawnLocations[i];
    }

    public Transform TeamBallSpawn(Team team, int i)
    {
        if (team == Team.TeamA)
            return TeamABallSpawnLocations[i];
        else
            return TeamBBallSpawnLocations[i];
    }


    public void SetTeamText(Team team, string s)
    {
        if(team == Team.TeamA)
        {
            TeamAUIText.text = s;
        }
        else
        {
            TeamBUIText.text = s;
        }
    }

    [SerializeField]
    int MatchLengthSec = 180;

    public TimeSpan MatchTimeRem { get; private set; }
    public double MatchTimeRemSec { get => MatchTimeRem.TotalSeconds; }

    [SerializeField]
    int teamSize = 3;

    public int TeamSize { get => teamSize; private set { teamSize = value; } }

    [SerializeField]
    Transform[] teamASpawnLocations = new Transform[] { };

    public Transform[] TeamASpawnLocations { get => teamASpawnLocations; private set { teamASpawnLocations = value; } }

    [SerializeField]
    Transform[] teamBSpawnLocations = new Transform[] { };

    public Transform[] TeamBSpawnLocations { get => teamBSpawnLocations; private set { teamBSpawnLocations = value; } }


    [SerializeField]
    int ballsPerTeam = 2;

    public int BallsPerTeam { get => ballsPerTeam; private set { ballsPerTeam = value; } }

    public int TotalBalls { get => 2 * ballsPerTeam; }

    [SerializeField]
    Transform[] teamABallSpawnLocations = new Transform[] { };

    public Transform[] TeamABallSpawnLocations { get => teamABallSpawnLocations; private set { teamABallSpawnLocations = value; } }


    [SerializeField]
    Transform[] teamBBallSpawnLocations = new Transform[] { };

    public Transform[] TeamBBallSpawnLocations { get => teamBBallSpawnLocations; private set { teamBBallSpawnLocations = value; } }

    [SerializeField]
    float throwSpeed = 20f;

    public float ThrowSpeed { get => throwSpeed; private set { throwSpeed = value; } }

    [SerializeField]
    float runSpeed = 8f;

    public float RunSpeed { get => runSpeed; private set { runSpeed = value; } }

    [SerializeField]
    float dodgeSpeed = 10f;

    public float DodgeSpeed { get => dodgeSpeed; private set { dodgeSpeed = value; } }


    [SerializeField]
    float runAccel = 12f;

    public float RunAccel { get => runAccel; private set { runAccel = value; } }


    [SerializeField]
    MinionScript MinionPrefab = null;


    [SerializeField]
    DodgeBall DodgeBallPrefab = null;


    DodgeBall[] dodgeBalls = new DodgeBall[] { };

    protected DodgeBall[] DodgeBalls { get => dodgeBalls; set { dodgeBalls = value; } }


    [SerializeField]
    int throwCount = 0;

    [SerializeField]
    int hitCount = 0;

    [SerializeField]
    int missCount = 0;

    [SerializeField]
    int totalCount = 0;

    [SerializeField]
    float hitPercentage = 0f;

    [SerializeField]
    float shotsPerMinute = 0f;

    float statsTimeStart = 0f;

    public int ThrowCount { get => throwCount; private set => throwCount = value; }
    public int HitCount { get => hitCount; private set => hitCount = value; }
    public int MissCount { get => missCount; private set => missCount = value; }
    

    public void INTERNAL_AnnounceThrow()
    {
        ++ThrowCount;

    }

    void updateStats()
    {
        totalCount = HitCount + MissCount;

        hitPercentage = hitCount / (float)totalCount;

        float elapsedMin = (Time.time - statsTimeStart)/60f;

        shotsPerMinute = totalCount / elapsedMin;
    }

    public void INTERNAL_AnnounceHit()
    {
        ++HitCount;

        updateStats();
    }

    public void INTERNAL_AnnounceMiss()
    {
        ++MissCount;

        updateStats();
    }

    public void INTERNAL_ResetStats()
    {
        ThrowCount = 0;
        HitCount = 0;
        MissCount = 0;
        totalCount = 0;
        hitPercentage = 0f;
        shotsPerMinute = 0f;
        statsTimeStart = Time.time;
    }

bool gameOver = false;
bool matchResultApplied = false;

    public bool IsGameOver { get => gameOver; }

    public bool IsTie { get; private set; }

    Team WinningTeam = Team.TeamA;

    public bool IsWinner(Team team)
    {
        if (team == Team.TeamA)
        {
            return WinningTeam == Team.TeamA;
        }
        else
            return WinningTeam == Team.TeamB;
    }



    object TeamADataShare { get; set; }
    object TeamBDataShare { get; set; }

    public object GetTeamDataShare(Team team)
    {
        if (team == Team.TeamA)
            return TeamADataShare;
        else
            return TeamBDataShare;
    }

    public void SetTeamDataShare(Team team, object o)
    {
        if (team == Team.TeamA)
            TeamADataShare = o;
        else
            TeamBDataShare = o;
    }

    private static PrisonDodgeballManager Mgr;

    public static PrisonDodgeballManager Instance
    {
        get
        {
            if (!Mgr)
            {
                Debug.Log("Finding PrisonDodgeballManager singleton");

                Mgr = FindFirstObjectByType(typeof(PrisonDodgeballManager)) as PrisonDodgeballManager;

                if (!Mgr)
                {
                    Debug.LogError("There needs to be one active PrisonDodgeballManager script on a GameObject in your scene.");
                }
                else
                {
                    Mgr.Init();
                }
            }

            return Mgr;
        }
    }



    private UnityAction<Vector3, MinionScript> minionDeathEventListener;

    // Guard against double-advancing the tournament in one frame
    bool tournamentAdvanceInProgress = false;





    public bool IsInit { get; private set; }


    void Init()
    {
        if (!IsInit)
        {
            IsInit = true;

 

            // JBW QualitySettings.vSyncCount = 1;

            ArenaLayerLayerIndex = LayerMask.NameToLayer(ArenaLayerName);
            MinionTeamALayerIndex = LayerMask.NameToLayer(MinionTeamALayerName);
            MinionTeamBLayerIndex = LayerMask.NameToLayer(MinionTeamBLayerName);
            BallTeamALayerIndex = LayerMask.NameToLayer(BallTeamALayerName);
            BallTeamBLayerIndex = LayerMask.NameToLayer(BallTeamBLayerName);
            BallNeutralLayerIndex = LayerMask.NameToLayer(BallNeutralLayerName);
            PrisonerTeamALayerIndex = LayerMask.NameToLayer(PrisonerTeamALayerName);
            PrisonerTeamBLayerIndex = LayerMask.NameToLayer(PrisonerTeamBLayerName);

            NavMeshCarverLayerIndex = LayerMask.NameToLayer(NavMeshCarverLayerName);

            //Debug.Log($"{ArenaLayerLayerIndex} {MinionTeamALayerIndex} {MinionTeamBLayerIndex} {BallTeamALayerIndex} {BallTeamBLayerIndex} {BallNeutralLayerIndex} {PrisonerTeamALayerIndex} {PrisonerTeamBLayerIndex}");

            TeamANavMeshAreaIndex = NavMesh.GetAreaFromName(TeamANavMeshAreaName);
            TeamBNavMeshAreaIndex = NavMesh.GetAreaFromName(TeamBNavMeshAreaName);
            NeutralNavMeshAreaIndex = NavMesh.GetAreaFromName(NeutralNavMeshAreaName);
            WalkableNavMeshAreaIndex = NavMesh.GetAreaFromName(WalkableNavMeshAreaName);
            TeamAPrisonNavMeshAreaIndex = NavMesh.GetAreaFromName(TeamAPrisonNavMeshAreaName);
            TeamBPrisonNavMeshAreaIndex = NavMesh.GetAreaFromName(TeamBPrisonNavMeshAreaName);
        }
    }



    MinionScript[] TeamAMinions;
    MinionScript[] TeamBMinions;


    //System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    TimeSpan MatchTimeSpan;
    float matchStartTime = 0f;

    private void Awake()
    {
        minionDeathEventListener = new UnityAction<Vector3, MinionScript>(minionDeathEventHandler);

    }


    void OnEnable()
    {

        EventManager.StartListening<MinionDeathEvent, Vector3, MinionScript>(minionDeathEventListener);

    }

    void OnDisable()
    {
        EventManager.StopListening<MinionDeathEvent, Vector3, MinionScript>(minionDeathEventListener);

    }

    void minionDeathEventHandler(Vector3 worldPos, MinionScript ms)
    {
        if(ThrowTest && ThrowTestResetMinionPos)
        {

            if (ms.Team == Team.TeamA)
                return; //only team b is the target drone

            var nma = ms.GetComponent<NavMeshAgent>();

            if (nma != null)
            {

                float xAbsRange = 11f;
                float zAbsRange = 25f;

                float minx = -xAbsRange;
                float maxx = xAbsRange;

                float minz = -zAbsRange;
                float maxz = zAbsRange;

                if (ThrowTestRestrictTargetToSideEnabled)
                {
                    minz = -23.5f;
                    maxz = 5f;
                }

                var randDir = new Vector3(UnityEngine.Random.Range(minx, maxx), 0f, UnityEngine.Random.Range(minz, maxz));
                
                var newPos = TeamACenter.position + randDir;

                nma.Warp(newPos);
            }

        }

    }


    protected void SetSimulationMode()
    {
        if (this.dodgeballSimulationMode == DodgeballSimulationMode.FPS_60_1X_RealTime)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }
        else if (this.dodgeballSimulationMode == DodgeballSimulationMode.FPS_60_1X_SimTime)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureFramerate = 60;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }
    }

    private DodgeballSimulationMode lastSimMode = DodgeballSimulationMode.FPS_60_1X_RealTime;

    const string TEAM_SIZE = "TeamSize";
    const string BALLS_PER_TEAM = "BallsPerTeam";
    const string THROW_SPEED = "ThrowSpeed";
    const string DODGE_SPEED = "DodgeSpeed";
    const string RUN_SPEED = "RunSpeed";
    const string RUN_ACCEL = "RunAccel";
    private void Start()
    {
        //Debug.Log("Prison Dodgeball MGR Start");

        // Set target frame rate
        //JBW QualitySettings.vSyncCount = 0;
        //JBW Application.targetFrameRate = 60;

        SetSimulationMode();
        lastSimMode = dodgeballSimulationMode;

        // Ensure each new match/scene starts unpaused, even if a previous
        // results banner or editor pause left Time.timeScale at 0.
        Time.timeScale = 1f;

        Cursor.visible = false;

        Init();

        if (tournamentMode)
        {
            InitTournamentIfNeeded();
            ConfigureTeamsForCurrentMatch();
        }

        if (TeamAMinions != null)
        {
            foreach (var m in TeamAMinions)
            {
                Destroy(m.gameObject);
            }
        }

        if (TeamBMinions != null)
        {
            foreach (var m in TeamBMinions)
            {
                Destroy(m.gameObject);
            }
        }

        if(dodgeBalls != null)
        {
            foreach(var b in dodgeBalls)
            {
                Destroy(b.gameObject);
            }
        }

        gameOver = false;
        matchResultApplied = false;
        tournamentSettleStartTime = -1f;
        IsTie = false;

        bool reverseColor = false;

        if (!tournamentMode)
        {
            if (OverrideConfiguration)
            {
                //Debug.Log("OVERRIDING CONFIGURATION FROM INSPECTOR!");

                TeamAAssemblyQualifiedName = Override_TeamAAssemblyQualifiedName;
                TeamBAssemblyQualifiedName = Override_TeamBAssemblyQualifiedName;
                TeamSize = Override_teamSize;
                ballsPerTeam = Override_ballsPerTeam;
                MatchLengthSec = Override_matchLengthSec;
                dodgeballSimulationMode = Override_dodgeballSimulationMode;
            }
            else
            {
                //Debug.Log("Not using override config");

                if (TeamsReversed)
                {
                    var tmp = TeamAAssemblyQualifiedName;
                    TeamAAssemblyQualifiedName = TeamBAssemblyQualifiedName;
                    TeamBAssemblyQualifiedName = tmp;
                    reverseColor = true;
                }

                TeamsReversed = !TeamsReversed;
            }
        }


        if (PlayerPrefs.HasKey(TEAM_SIZE))
        {
            TeamSize = PlayerPrefs.GetInt(TEAM_SIZE);
        }

        if (PlayerPrefs.HasKey(BALLS_PER_TEAM))
        {
            BallsPerTeam = PlayerPrefs.GetInt(BALLS_PER_TEAM);
        }

        if (PlayerPrefs.HasKey(THROW_SPEED))
        {
            ThrowSpeed = PlayerPrefs.GetFloat(THROW_SPEED);
        }

        if (PlayerPrefs.HasKey(DODGE_SPEED))
        {
            DodgeSpeed = PlayerPrefs.GetFloat(DODGE_SPEED);
        }

        if (PlayerPrefs.HasKey(RUN_SPEED))
        {
            RunSpeed = PlayerPrefs.GetFloat(RUN_SPEED);
        }

        if (PlayerPrefs.HasKey(RUN_ACCEL))
        {
            RunAccel = PlayerPrefs.GetFloat(RUN_ACCEL);
        }

        PlayerPrefs.DeleteAll();


        if (!tournamentMode)
        {
            if (!WinsByTeamAssembly.ContainsKey(TeamAAssemblyQualifiedName))
            {
                WinsByTeamAssembly.Add(TeamAAssemblyQualifiedName, 0);
            }

            if (!WinsByTeamAssembly.ContainsKey(TeamBAssemblyQualifiedName))
            {
                WinsByTeamAssembly.Add(TeamBAssemblyQualifiedName, 0);
            }

            TeamAUIWinsText.text = WinsByTeamAssembly[TeamAAssemblyQualifiedName].ToString();
            TeamBUIWinsText.text = WinsByTeamAssembly[TeamBAssemblyQualifiedName].ToString();
        }
        else
        {
            // In tournament mode, the on-screen wins show the current best-of-3 series score (0–0 at series start).
            UpdateSeriesScoreUI();
        }


        // TODO Error check sizes 

        TeamAMinions = new MinionScript[teamSize];
        TeamBMinions = new MinionScript[teamSize];

        for(int i = 0; i < teamSize; ++i)
        {

            //Debug.Log("Spawning minions");

            var ta = TeamASpawnLocations[i];

            if(ThrowTest && teamSize == 1)
            {
                // start the thrower in the middle
                ta = TeamACenter;
            }

            var ma = Instantiate<MinionScript>(MinionPrefab, ta.position, ta.rotation);
            ma.Mgr = this;
            ma.gameObject.AddComponent(System.Type.GetType(TeamAAssemblyQualifiedName));
            ma.INTERNAL_Team = Team.TeamA;
            ma.INTERNAL_SpawnIndex = i;
            ma.INTERNAL_ReverseColor = reverseColor;
            ma.INTERNAL_SetThrowSpeed(ThrowSpeed);
            ma.INTERNAL_SetDodgeSpeed(DodgeSpeed);
            ma.INTERNAL_SetRunSpeed(RunSpeed);
            ma.INTERNAL_SetRunAccel(RunAccel);
            TeamAMinions[i] = ma;

            var tb = TeamBSpawnLocations[i];
            var mb = Instantiate<MinionScript>(MinionPrefab, tb.position, tb.rotation);
            mb.Mgr = this;
            mb.gameObject.AddComponent(System.Type.GetType(TeamBAssemblyQualifiedName));
            mb.INTERNAL_Team = Team.TeamB;
            mb.INTERNAL_SpawnIndex = i;
            mb.INTERNAL_ReverseColor = reverseColor;
            mb.INTERNAL_SetThrowSpeed(ThrowSpeed);
            mb.INTERNAL_SetDodgeSpeed(DodgeSpeed);
            mb.INTERNAL_SetRunSpeed(RunSpeed);
            mb.INTERNAL_SetRunAccel(RunAccel);
            TeamBMinions[i] = mb;

            if(ThrowTest)
            {
                // make target minion not get knocked around by dodgeballs
                var rb = mb.GetComponent<Rigidbody>();
                if(rb != null)
                {
                    rb.mass = 10000f;
                }

                if (!ThrowTestRestrictTargetToSide)
                {
                    // allow the target minion everywhere
                    // also constantly checked in MinionScript.Update()
                    var nma = mb.GetComponent<NavMeshAgent>();
                    if (nma != null)
                    {
                        nma.areaMask = NavMesh.AllAreas;
                    }
                    else
                    {
                        Debug.LogError($"No navmeshagent!");
                    }
                }

            }
        }

        int extraBallMult = 1;

        if (ThrowTestEnabled)
            extraBallMult = 2;


        int numBalls = ballsPerTeam * extraBallMult * 2;


        dodgeBalls = new DodgeBall[numBalls];

        int dbIndex = 0;

        for(int i = 0; i < ballsPerTeam; ++i)
        {
            for (int j = 0; j < extraBallMult; ++j)
            {
                Vector3 vertOffs = Vector3.one * 3f * (float)j;
                var ta = TeamABallSpawnLocations[i];
                var ma = Instantiate<DodgeBall>(DodgeBallPrefab, ta.position + vertOffs, ta.rotation);
                ma.ReverseColor = reverseColor;
                ma.Index = dbIndex;
                dodgeBalls[dbIndex++] = ma;
                var tb = TeamBBallSpawnLocations[i];
                var mb = Instantiate<DodgeBall>(DodgeBallPrefab, tb.position + vertOffs, tb.rotation);
                mb.ReverseColor = reverseColor;
                mb.Index = dbIndex;
                dodgeBalls[dbIndex++] = mb;
            }
        }


        MatchTimeSpan = new TimeSpan(0, 0, MatchLengthSec);
        //stopwatch.Start();
        matchStartTime = Time.timeSinceLevelLoad;
        MatchTimeRem = MatchTimeSpan;

        MatchOutputText.color = NeutralTextColor;
        MatchOutputText.text =  MatchTimeSpan.ToString();

        var teamAColor = reverseColor ? TeamBTextColor : TeamATextColor;
        var teamBColor = reverseColor ? TeamATextColor : TeamBTextColor;

        TeamAUIText.color = teamAColor;
        TeamBUIText.color = teamBColor;

        TeamAUIWinsText.color = teamAColor;
        TeamBUIWinsText.color = teamBColor;

        // At the very start of each best-of-3 series in tournament mode,
        // briefly show an announcement banner using the HUD team names.
        // SeriesMatchIndex is 0 for the first match in a series and is
        // reset to 0 in SetupNextSeriesPair() when a new series is created.
        if (tournamentMode && TournamentCanvas != null && SeriesMatchIndex == 0)
        {
            StartCoroutine(RunSeriesIntroSequence());
        }
    }



    private void InitTournamentIfNeeded()
    {
        if (!tournamentMode || TournamentInitialized)
            return;

        if (tournamentAssemblies == null || tournamentAssemblies.Length < 2)
        {
            Debug.LogError("Tournament mode enabled but no assemblies configured.");
            tournamentMode = false;
            return;
        }

        int n = tournamentAssemblies.Length;
        // Require power-of-two length
        if ((n & (n - 1)) != 0)
        {
            Debug.LogError("Tournament assemblies length must be a power of 2.");
            tournamentMode = false;
            return;
        }

        // Reset series-win counters for this tournament
        WinsByTeamAssembly.Clear();
        foreach (var asm in tournamentAssemblies)
        {
            if (!string.IsNullOrEmpty(asm) && !WinsByTeamAssembly.ContainsKey(asm))
            {
                WinsByTeamAssembly.Add(asm, 0);
            }
        }

        TournamentInitialized = true;
        CurrentPhase = (n == 2) ? TournamentPhase.Championship
                     : (n == 4) ? TournamentPhase.Semifinal1
                     : TournamentPhase.RoundsAboveSemis;

        CurrentRoundTeams = new List<string>(tournamentAssemblies);
        NextRoundTeams = new List<string>();

        CurrentRoundMatchIndex = 0;
        SeriesWins1 = SeriesWins2 = 0;
        SeriesMatchIndex = 0;
        CurrentLevel = 1;
        ConsecutiveTiesAtCurrentLevel = 0;

        // Ensure the very first match uses level 1 settings
        SetLevelPreset(CurrentLevel);

        // Log overall tournament field and phase first
        LogEvent($"TOURNAMENT_START: Assemblies=[{string.Join(",", tournamentAssemblies)}], Phase={CurrentPhase}");

        // Now schedule and log the first best-of-3 series
        SetupNextSeriesPair();
    }

    private void SetupNextSeriesPair()
    {
        if (!tournamentMode || CurrentPhase == TournamentPhase.Completed)
            return;

        if (CurrentPhase == TournamentPhase.RoundsAboveSemis)
        {
            int i = CurrentRoundMatchIndex;
            SeriesTeam1 = CurrentRoundTeams[2 * i];
            SeriesTeam2 = CurrentRoundTeams[2 * i + 1];
        }
        else if (CurrentPhase == TournamentPhase.Semifinal1)
        {
            SeriesTeam1 = CurrentRoundTeams[0];
            SeriesTeam2 = CurrentRoundTeams[1];
        }
        else if (CurrentPhase == TournamentPhase.Semifinal2)
        {
            SeriesTeam1 = CurrentRoundTeams[2];
            SeriesTeam2 = CurrentRoundTeams[3];
        }
        else if (CurrentPhase == TournamentPhase.ThirdPlace)
        {
            SeriesTeam1 = SemiLoser1;
            SeriesTeam2 = SemiLoser2;
        }
        else if (CurrentPhase == TournamentPhase.Championship)
        {
            if (CurrentRoundTeams != null && CurrentRoundTeams.Count == 2)
            {
                SeriesTeam1 = CurrentRoundTeams[0];
                SeriesTeam2 = CurrentRoundTeams[1];
            }
            else
            {
                SeriesTeam1 = SemiWinner1;
                SeriesTeam2 = SemiWinner2;
            }
        }

        SeriesWins1 = SeriesWins2 = 0;
        SeriesMatchIndex = 0;
        CurrentLevel = 1;
        ConsecutiveTiesAtCurrentLevel = 0;

        // At this point we only know the configured assemblies; HUD names are not yet available.
        LogEvent($"SERIES_START: Phase={CurrentPhase}, Team1={SeriesTeam1}, Team2={SeriesTeam2}, Level={CurrentLevel}");
    }

    private void ConfigureTeamsForCurrentMatch()
    {
        if (!tournamentMode || CurrentPhase == TournamentPhase.Completed)
            return;

        // Alternate sides each match in the series
        bool seriesTeam1OnA = (SeriesMatchIndex % 2 == 0);

        if (seriesTeam1OnA)
        {
            TeamAAssemblyQualifiedName = SeriesTeam1;
            TeamBAssemblyQualifiedName = SeriesTeam2;
        }
        else
        {
            TeamAAssemblyQualifiedName = SeriesTeam2;
            TeamBAssemblyQualifiedName = SeriesTeam1;
        }
    }

    public string NavMeshMaskToString(int mask)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (0 != (mask & (1 << NeutralNavMeshAreaIndex)))
        {
            sb.Append(NeutralNavMeshAreaName);
            sb.Append(":");
        }

        if (0 != (mask & (1 << TeamANavMeshAreaIndex)) )
        {
            sb.Append(TeamANavMeshAreaName);
            sb.Append(":");
        }

        if (0 != (mask & (1 << TeamBNavMeshAreaIndex)))
        {
            sb.Append(TeamBNavMeshAreaName);
            sb.Append(":");
        }

        if (0 != (mask & (1 << WalkableNavMeshAreaIndex)))
        {
            sb.Append(WalkableNavMeshAreaName);
            sb.Append(":");
        }

        if (0 != (mask & (1 << TeamAPrisonNavMeshAreaIndex)))
        {
            sb.Append(TeamAPrisonNavMeshAreaName);
            sb.Append(":");
        }

        if (0 != (mask & (1 << TeamBPrisonNavMeshAreaIndex)))
        {
            sb.Append(TeamBPrisonNavMeshAreaName);
            sb.Append(":");
        }


        return sb.ToString();
    }


    public bool FindClosestNonPrisonerOpponentIndex(Vector3 myPos, Team myTeam, out int foundIndex)
    {
        MinionScript[] mins;

        if (myTeam == Team.TeamA)
            mins = TeamBMinions;
        else
            mins = TeamAMinions;

        var foundDist = float.MaxValue;
        foundIndex = -1;
        for(int i =0; i<mins.Length; ++i)
        {
            var m = mins[i];

            if (m.IsPrisoner || m.IsFreedPrisoner)
                continue;

            var dist = Vector3.Distance(m.transform.position, myPos);
            if(dist < foundDist)
            {
                foundIndex = i;
                foundDist = dist;
            }
        }

        return foundIndex > -1;
    }

    public enum DodgeballState
    {
        Neutral,
        Opponent,
        Team
    }


    public struct DodgeballInfo
    {
        public int Index;
        public Vector3 Pos;
        public float Radius;
        public Vector3 NavMeshPos;
        public Vector3 Vel;
        public bool IsHeld;
        public DodgeballState State;
        public int NMMask;
        public bool Reachable;


        public DodgeballInfo(int index, Vector3 pos, float radius, Vector3 navMeshPos, Vector3 vel, bool isHeld, DodgeballState state,
            int nmMask, bool reachable)
        {
            Index = index;
            Pos = pos;
            Radius = radius;
            NavMeshPos = navMeshPos;
            Vel = vel;
            IsHeld = isHeld;
            State = state;
            NMMask = nmMask;
            Reachable = reachable;
        }
    }



    // Get info about dodgeball at index in dodgeball array of len TotalBalls
    // determineRegion param must be set to true in order to obtain NMMask and Reachable
    // properties in the DodgeballInfo
    public bool GetDodgeballInfo(Team myTeam, int ballIndex, out DodgeballInfo di, 
        bool determineRegion)
    {
        var opponentBallLayer = myTeam == Team.TeamA ?
                            Mgr.BallTeamBLayerIndex : Mgr.BallTeamALayerIndex;

        var myTeamBallLayer = myTeam == Team.TeamA ?
                    Mgr.BallTeamALayerIndex : Mgr.BallTeamBLayerIndex;

        var reachableMask = (1 << NeutralNavMeshAreaIndex) | (1 << WalkableNavMeshAreaIndex);

        if(myTeam == Team.TeamA)
            reachableMask = reachableMask | (1 << TeamANavMeshAreaIndex);
        else
            reachableMask = reachableMask | (1 << TeamBNavMeshAreaIndex);

        di = new DodgeballInfo();



        if (ballIndex >= 0 && ballIndex < Mgr.DodgeBalls.Length)
        {
            var b = Mgr.DodgeBalls[ballIndex];

            var pos = new Vector3( b.transform.position.x, 0f, b.transform.position.z);
            Vector3 navMeshPos = default;
            var vel = b.Velocity;
            var isHeld = b.IsHeld;

            var coll = b.GetComponent<SphereCollider>();

            float radius = 0.25f;

            if(coll != null)
            {
                radius = coll.radius;
            }
 

            DodgeballState state = DodgeballState.Neutral;

            if(b.Layer == opponentBallLayer)
            {
                state = DodgeballState.Opponent;
            }
            else if(b.Layer == myTeamBallLayer)
            {
                state = DodgeballState.Team;
            }
            else if(b.Layer == Mgr.BallNeutralLayerIndex)
            {
                state = DodgeballState.Neutral;
            }

            int nmMask = 0;
            bool reachable = false;

            if(determineRegion)
            {
                if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas))
                {
                    nmMask = hit.mask;

                    if((hit.mask & reachableMask) > 0)
                    {
                        reachable = true;
                    }

                    navMeshPos = hit.position;
                }

            }

            di = new DodgeballInfo(ballIndex, b.transform.position, radius, navMeshPos, vel, isHeld, state, nmMask, reachable);

            return true;
        }
        else
            return false;
    }


    public bool GetAllDodgeballInfo(Team myTeam, ref DodgeballInfo[] dodgeballInfo, bool determineRegion)
    {
        if (dodgeballInfo == null || dodgeballInfo.Length != Mgr.dodgeBalls.Length)
            return false;

        for(int i = 0; i < Mgr.dodgeBalls.Length; ++i)
        {
            DodgeballInfo dbi;
            if(GetDodgeballInfo(myTeam, i, out dbi, determineRegion))
            {
                dodgeballInfo[i] = dbi;
            }
        }

        return true;
    }



    public struct OpponentInfo
    {
        public int Index;
        public Vector3 Pos;
        public Vector3 Vel;
        public Vector3 Forward;
        public Vector3 PrevPos;
        public Vector3 PrevVel;
        public Vector3 PrevForward;
        public float Radius;
        public float Height;
        public bool HasBall;
        public bool IsPrisoner;
        public bool IsFreedPrisoner;

        public OpponentInfo( int index, Vector3 pos,  Vector3 vel, Vector3 forward,
            Vector3 prevPos, Vector3 prevVel, Vector3 prevForward,
            float radius, float height,
            bool hasBall,  
            bool isPrisoner,  bool isFreedPrisoner)
        {
            Index = index; 
            Pos = pos;
            Vel = vel;
            Forward = forward;
            PrevPos = prevPos;
            PrevForward = prevForward;
            PrevVel = prevVel;
            Radius = radius;
            Height = height;
            HasBall = hasBall;
            IsPrisoner = isPrisoner;
            IsFreedPrisoner = isFreedPrisoner;
        }
    }

    public bool GetOpponentInfo(Team myTeam, int index, out OpponentInfo oi)
    {

        oi = new OpponentInfo();

        MinionScript[] mins;

        if (myTeam == Team.TeamA)
            mins = TeamBMinions;
        else
            mins = TeamAMinions;

        if (index >= 0 && index < mins.Length)
        {
            var m = mins[index];

            var pos = m.transform.position;
            var vel = m.Velocity;
            var forward = m.transform.forward;
            var prevPos = m.prevPosition;
            var prevVel = m.prevVelocity;
            var prevForward = m.prevForward;

            var coll = m.GetComponent<CapsuleCollider>();

            float radius = 0.5f;
            float height = 1.5f;

            if(coll != null)
            {
                radius = coll.radius;
                height = coll.height;
            }

            var hasBall = m.HasBall;
            var isPrisoner = m.IsPrisoner;
            var isFreedPrisoner = m.IsFreedPrisoner;
            oi = new OpponentInfo(index, pos, vel, forward, prevPos, prevVel, prevForward, radius, height, hasBall, isPrisoner, isFreedPrisoner);

            return true;
        }
        else
            return false;
    }


    public bool GetAllOpponentInfo(Team myTeam, ref OpponentInfo[] oppInfo)
    {
        MinionScript[] mins;

        if (myTeam == Team.TeamA)
            mins = TeamBMinions;
        else
            mins = TeamAMinions;

        if (oppInfo == null || oppInfo.Length != mins.Length)
            return false;

        for (int i = 0; i < mins.Length; ++i)
        {
            OpponentInfo oi;
            if (GetOpponentInfo(myTeam, i, out oi))
            {
                oppInfo[i] = oi;
            }
        }

        return true;
    }


    public void INTERNAL_ThrowTestReportThrow()
    {
        if (!ThrowTest)
        {
            Debug.LogError("Attempt to call INTERNAL_ThrowTestReportThrow() during regular play!");
            return;
        }

        throwTestLastRequest = Time.time;
    }

    // make sure sim settles down before throwing begins
    private float throwTestInitPause = 1f;

    private float throwTestLastRequest = 0f;

    public void ThrowTestRequestBall(MinionScript minion)
    {

        if (!ThrowTest)
        { 
            Debug.LogError("Attempt to call ThrowTestRequestBall() during regular play!");
            return;
        }

        if (Time.time < throwTestInitPause)
            return;

        if ( (Time.time - throwTestLastRequest) < ThrowTestBallRequestInterval)
            return;

        foreach (var b in dodgeBalls)
        {
            //Debug.Log("considering ball...");

            //if (b.Layer == BallNeutralLayerIndex)
            if (!b.gameObject.activeSelf)
            {
                //Debug.Log("found a ball for minion!");

                b.gameObject.SetActive(true);

               

                var brb = b.gameObject.GetComponent<Rigidbody>();

                if (brb != null)
                {
                    brb.linearVelocity = Vector3.zero;
                    brb.angularVelocity = Vector3.zero;
                    brb.ResetInertiaTensor();

                    brb.MovePosition(minion.HeldBallPosition);
                    minion.INTERNAL_ReceiveBall(b);
                    b.INTERNAL_SetToTeam(Mgr.MinionTeamALayerIndex);
                    throwTestLastRequest = Time.time;
                    return;

                }
            }
        }


    }





    protected float maxDistSqr = Mathf.Pow(1f, 2f);




    private void SetLevelPreset(int preset)
    {
        if (preset == 1)
        {
            PlayerPrefs.SetInt(TEAM_SIZE, 4);
            PlayerPrefs.SetInt(BALLS_PER_TEAM, 3);
            PlayerPrefs.SetFloat(THROW_SPEED, 20.0f);
            PlayerPrefs.SetFloat(RUN_SPEED, 8f);
            PlayerPrefs.SetFloat(DODGE_SPEED, 10f);
            PlayerPrefs.SetFloat(RUN_ACCEL, 12f);
        }
        else if (preset == 2)
        {
            PlayerPrefs.SetInt(TEAM_SIZE, 3);
            PlayerPrefs.SetInt(BALLS_PER_TEAM, 3);
            PlayerPrefs.SetFloat(THROW_SPEED, 20.0f);
            PlayerPrefs.SetFloat(RUN_SPEED, 8f);
            PlayerPrefs.SetFloat(DODGE_SPEED, 10f);
            PlayerPrefs.SetFloat(RUN_ACCEL, 12f);
        }
        else if (preset == 3)
        {
            PlayerPrefs.SetInt(TEAM_SIZE, 2);
            PlayerPrefs.SetInt(BALLS_PER_TEAM, 3);
            PlayerPrefs.SetFloat(THROW_SPEED, 20.0f);
            PlayerPrefs.SetFloat(RUN_SPEED, 8f);
            PlayerPrefs.SetFloat(DODGE_SPEED, 10f);
            PlayerPrefs.SetFloat(RUN_ACCEL, 12f);
        }
        else if (preset == 4)
        {
            PlayerPrefs.SetInt(TEAM_SIZE, 1);
            PlayerPrefs.SetInt(BALLS_PER_TEAM, 3);
            PlayerPrefs.SetFloat(THROW_SPEED, 20.0f);
            PlayerPrefs.SetFloat(RUN_SPEED, 8f);
            PlayerPrefs.SetFloat(DODGE_SPEED, 10f);
            PlayerPrefs.SetFloat(RUN_ACCEL, 12f);
        }
    }

    private void Update()
    {

        if(lastSimMode != dodgeballSimulationMode)
        {
            SetSimulationMode();
        }

        Cursor.visible = false;

        if (Input.GetKeyUp(KeyCode.Alpha1))
        {
            SetLevelPreset(1);
        }
        else if (Input.GetKeyUp(KeyCode.Alpha2))
        {
            SetLevelPreset(2);
        }
        else if (Input.GetKeyUp(KeyCode.Alpha3))
        {
            SetLevelPreset(3);
        }
        else if (Input.GetKeyUp(KeyCode.Alpha4))
        {
            SetLevelPreset(4);
        }

        if (gameOver)
        {
            if (!tournamentMode)
            {
                if (Input.GetKeyUp(KeyCode.Space))
                {
                    //Debug.Log("Starting a new match");
                    //Start();
                    if (!PlayerPrefs.HasKey(TEAM_SIZE))
                    {
                        PlayerPrefs.SetInt(TEAM_SIZE, TeamSize);
                    }

                    if (!PlayerPrefs.HasKey(BALLS_PER_TEAM))
                    {
                        PlayerPrefs.SetInt(BALLS_PER_TEAM, BallsPerTeam);
                    }

                    if (!PlayerPrefs.HasKey(THROW_SPEED))
                    {
                        PlayerPrefs.SetFloat(THROW_SPEED, ThrowSpeed);
                    }

                    if (!PlayerPrefs.HasKey(RUN_SPEED))
                    {
                        PlayerPrefs.SetFloat(RUN_SPEED, RunSpeed);
                    }

                    if (!PlayerPrefs.HasKey(DODGE_SPEED))
                    {
                        PlayerPrefs.SetFloat(DODGE_SPEED, DodgeSpeed);
                    }

                    if (!PlayerPrefs.HasKey(RUN_ACCEL))
                    {
                        PlayerPrefs.SetFloat(RUN_ACCEL, RunAccel);
                    }

                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

                    INTERNAL_ResetStats();
                }
                else if (Input.GetKeyUp(KeyCode.Escape))
                {
                    PlayerPrefs.DeleteAll();
                    Application.Quit();
                }

                return;
            }
            else
            {
                if (Input.GetKeyUp(KeyCode.Escape))
                {
                    PlayerPrefs.DeleteAll();
                    Application.Quit();
                }
                // Do not return here; let tournament logic advance.
            }
        }

        // Once the tournament is fully completed, freeze the HUD result.
        // Non-tournament flow returns early above when gameOver is true,
        // so this special-case keeps tournament behavior consistent and
        // avoids overriding the final result (for example, with a late tie).
        if (tournamentMode && CurrentPhase == TournamentPhase.Completed)
        {
            return;
        }


        if(ThrowTest)
        {

            // Make the ball go away until requested by thrower
            foreach (var b in dodgeBalls)
            {
                if (b.Layer == BallNeutralLayerIndex)
                {
                    b.gameObject.SetActive(false);
                }
            }

                    //// balls migrate towards the minion that throws
                    //foreach(var m in TeamAMinions)
                    //{
                    //    if(m != null)// && !m.HasBall)
                    //    {
                    //        foreach(var b in dodgeBalls)
                    //        {
                    //            if(b.Layer == BallNeutralLayerIndex)
                    //            {
                    //                var brb = b.gameObject.GetComponent<Rigidbody>();

                    //                if (brb != null)
                    //                {
                    //                    var currPos = brb.transform.position;
                    //                    var currPos2D = new Vector2(currPos.x, currPos.z);
                    //                    var tpos = m.transform.position;
                    //                    var tpos2d = new Vector2(tpos.x, tpos.z);
                    //                    var sqrDist = Vector2.SqrMagnitude(currPos2D - tpos2d);
                    //                    if (sqrDist > maxDistSqr)
                    //                    {
                    //                        brb.velocity = Vector3.zero;
                    //                        //brb.AddForce((m.transform.position - brb.position).normalized * 8f, ForceMode.VelocityChange);
                    //                        brb.MovePosition(m.transform.position + Vector3.up * 5f);
                    //                    }
                    //                }
                    //            }
                    //        }
                    //    }
                    //}


            return;
        }


        if(!gameOver)
        {

            int teamAInPrison = 0;

            bool TeamALost = false;
            foreach(var m in TeamAMinions)
            {
                if(m != null && m.IsPrisoner)
                {
                    //TeamALost = false;
                    ++teamAInPrison;
                    //break;
                }
            }

            int teamBInPrison = 0;

            bool TeamBLost = false;
            foreach (var m in TeamBMinions)
            {
                if (m != null && m.IsPrisoner)
                {
                    //TeamBLost = false;
                    ++teamBInPrison;
                    //break;
                }
            }

            if (teamAInPrison >= TeamSize)
                TeamALost = true;

            if (teamBInPrison >= TeamSize)
                TeamBLost = true;

            string outputText = "";

            Color outputColor = NeutralTextColor;

            if (TeamALost && TeamBLost)
            {
                IsTie = true;
                gameOver = true;

                //Debug.Log("Teams tied");

                outputText = "Double knockout tie!";
    
            }
            else if (TeamALost)
            {
                IsTie = false;
                gameOver = true;
                //Debug.Log("Team B Won!");

                outputText = $"{TeamBUIText.text} WINS!";
                WinningTeam = Team.TeamB;
                outputColor = TeamBUIText.color;

                if (tournamentMode)
                {
                    if (!matchResultApplied)
                    {
                        // Immediately credit this win to the current best-of-3 series
                        string winnerAsm = TeamBAssemblyQualifiedName;

                        if (winnerAsm == SeriesTeam1)
                        {
                            SeriesWins1++;
                        }
                        else if (winnerAsm == SeriesTeam2)
                        {
                            SeriesWins2++;
                        }
                        else
                        {
                            Debug.LogWarning("Winner assembly not recognised in current series.");
                        }

                        // A decisive result resets the tie streak
                        ConsecutiveTiesAtCurrentLevel = 0;

                        // Update the visible series score on the HUD immediately
                        UpdateSeriesScoreUI();

                        matchResultApplied = true;
                    }
                }
                else
                {
                    if (!matchResultApplied)
                    {
                        if (WinsByTeamAssembly.ContainsKey(TeamBAssemblyQualifiedName))
                        {
                            WinsByTeamAssembly[TeamBAssemblyQualifiedName] += 1;
                            TeamBUIWinsText.text = WinsByTeamAssembly[TeamBAssemblyQualifiedName].ToString();
                        }
                        else
                        {
                            Debug.LogError("Could not store TeamB win!");
                        }

                        matchResultApplied = true;
                    }
                }
            }
            else if (TeamBLost)
            {
                IsTie = false;
                gameOver = true;

                //Debug.Log("Team A Won!");

                outputText = $"{TeamAUIText.text} WINS!";          
                WinningTeam = Team.TeamA;
                outputColor = TeamAUIText.color;

                if (tournamentMode)
                {
                    if (!matchResultApplied)
                    {
                        // Immediately credit this win to the current best-of-3 series
                        string winnerAsm = TeamAAssemblyQualifiedName;

                        if (winnerAsm == SeriesTeam1)
                        {
                            SeriesWins1++;
                        }
                        else if (winnerAsm == SeriesTeam2)
                        {
                            SeriesWins2++;
                        }
                        else
                        {
                            Debug.LogWarning("Winner assembly not recognised in current series.");
                        }

                        // A decisive result resets the tie streak
                        ConsecutiveTiesAtCurrentLevel = 0;

                        // Update the visible series score on the HUD immediately
                        UpdateSeriesScoreUI();

                        matchResultApplied = true;
                    }
                }
                else
                {
                    if (!matchResultApplied)
                    {
                        if (WinsByTeamAssembly.ContainsKey(TeamAAssemblyQualifiedName))
                        {
                            WinsByTeamAssembly[TeamAAssemblyQualifiedName] += 1;
                            TeamAUIWinsText.text = WinsByTeamAssembly[TeamAAssemblyQualifiedName].ToString();
                        }
                        else
                        {
                            Debug.LogError("Could not store TeamA win!");
                        }

                        matchResultApplied = true;
                    }
                }
            }
            else
            {
                //var timeRem = MatchTimeSpan - stopwatch.Elapsed;
                TimeSpan elapsed = new TimeSpan(0, 0, Mathf.RoundToInt(Time.timeSinceLevelLoad - matchStartTime));
                MatchTimeRem = MatchTimeSpan - elapsed;

                if (MatchTimeRem.TotalSeconds <= 0f)
                {

                    IsTie = true;
                    gameOver = true;

                    // discourage holding all the balls, just tie
                    //if (teamAInPrison > teamBInPrison)
                    //    outputText = "Team B WINS tiebreaker!";
                    //else if (teamBInPrison > teamAInPrison)
                    //    outputText = "Team A WINS tiebreaker!";
                    //else

                    outputText = "TIE!";

                }
                else
                {
                    if (tournamentMode)
                    {
                        // In tournament mode, restart the HUD match number at 1 for each best-of-3 series.
                        // SeriesMatchIndex is 0-based and is reset to 0 in SetupNextSeriesPair() at the start of each series.
                        outputText = $"Match {SeriesMatchIndex + 1}: {MatchTimeRem.ToString(@"mm\:ss")}";
                    }
                    else
                    {
                        // In non-tournament mode, preserve the original overall-match counter behavior.
                        outputText = $"Match {TotalMatches + 1}: {MatchTimeRem.ToString(@"mm\:ss")}";
                    }
                }
            }

            if(gameOver)
            {
                ++TotalMatches;
            }

            MatchOutputText.color = outputColor;
            MatchOutputText.text = $"{outputText}";


        }

        if (tournamentMode && gameOver && !tournamentAdvanceInProgress)
        {
            // In tournament mode, do not advance immediately when the match ends.
            // Allow a brief "cinematic" settle period so minions can finish moving
            // for a nicer video capture: wait at least tournamentSettleMinSeconds,
            // up to tournamentSettleMaxSeconds, and optionally stop early once all
            // minions have reached their targets.
            if (tournamentSettleStartTime < 0f)
            {
                tournamentSettleStartTime = Time.timeSinceLevelLoad;
            }

            float settleElapsed = Time.timeSinceLevelLoad - tournamentSettleStartTime;

            if (settleElapsed >= tournamentSettleMinSeconds)
            {
                bool allSettled = AllMinionsReachedTarget();

                if (allSettled || settleElapsed >= tournamentSettleMaxSeconds)
                {
                    AdvanceTournamentAfterMatch();
                    // The scene will usually reload after this; if not (e.g., final
                    // championship game), this prevents repeatedly re-evaluating.
                    tournamentSettleStartTime = -1f;
                }
            }
        }

        //if(gameOver)
        //{
        //    Time.timeScale = 0f;
        //}

    }

    private void AdvanceTournamentAfterMatch()
    {
        // Derive a human-readable winner name for logging
        string winnerName = IsTie
            ? "TIE"
            : (WinningTeam == Team.TeamA ? TeamAUIText.text : TeamBUIText.text);

        string matchLog =
            $"MATCH_RESULT: Phase={CurrentPhase}, MatchIndex={SeriesMatchIndex}, " +
            $"TeamA={TeamAUIText.text}, TeamB={TeamBUIText.text}, " +
            $"WinnerName={winnerName}, WinnerSide={(IsTie ? "None" : WinningTeam.ToString())}, " +
            $"Tie={IsTie}, Level={CurrentLevel}, Elapsed={Time.timeSinceLevelLoad:F2}";

        LogEvent(matchLog);
        tournamentAdvanceInProgress = true;

        // For decisive results, the current best-of-3 series wins and HUD were
        // already updated at the moment the match ended in Update(). Here we
        // only handle tie-based level escalation.
        if (IsTie)
        {
            // Tie: we keep the same series, same score
            ConsecutiveTiesAtCurrentLevel++;

            // If two ties at this level, move to the next level (up to 4)
            if (ConsecutiveTiesAtCurrentLevel >= 2 && CurrentLevel < 4)
            {
                int oldLevel = CurrentLevel;
                CurrentLevel++;
                ConsecutiveTiesAtCurrentLevel = 0;

                LogEvent(
                    $"LEVEL_CHANGE: Phase={CurrentPhase}, SeriesTeams=[{TeamAUIText.text} vs {TeamBUIText.text}], " +
                    $"OldLevel={oldLevel}, NewLevel={CurrentLevel}");
            }
        }

        bool seriesDone = (SeriesWins1 >= 2 || SeriesWins2 >= 2);

        if (!seriesDone)
        {
            // Same pair, new match in this series; alternate sides
            SeriesMatchIndex++;

            SetLevelPreset(CurrentLevel);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // Series winner determined
        string seriesWinner = (SeriesWins1 > SeriesWins2) ? SeriesTeam1 : SeriesTeam2;
        string seriesLoser  = (SeriesWins1 > SeriesWins2) ? SeriesTeam2 : SeriesTeam1;

        // Log a human-readable series result
        string seriesWinnerHUD = seriesWinner;
        string seriesLoserHUD = seriesLoser;

        if (TeamAAssemblyQualifiedName == seriesWinner)
        {
            seriesWinnerHUD = TeamAUIText.text;
        }
        else if (TeamBAssemblyQualifiedName == seriesWinner)
        {
            seriesWinnerHUD = TeamBUIText.text;
        }

        if (TeamAAssemblyQualifiedName == seriesLoser)
        {
            seriesLoserHUD = TeamAUIText.text;
        }
        else if (TeamBAssemblyQualifiedName == seriesLoser)
        {
            seriesLoserHUD = TeamBUIText.text;
        }

        LogEvent(
            $"SERIES_RESULT: Phase={CurrentPhase}, Winner={seriesWinnerHUD}, Loser={seriesLoserHUD}, " +
            $"SeriesScore={SeriesWins1}-{SeriesWins2}, FinalLevel={CurrentLevel}");

        // After a best-of-3 series is decided, show a short announcement
        // banner (if the tournament canvas is available) and only then
        // advance the tournament bracket.
        if (tournamentMode && TournamentCanvas != null)
        {
            // For all series (including championship), present the winner vs "WINS"
            // on the tournament results banner, without touching the in-game HUD.
            string bannerTeamA = seriesWinnerHUD;
            string bannerTeamB = "WINS";

            // This is a result screen; clear the "versus" line.
            StartCoroutine(ShowTournamentAnnouncementWithNames(
                GetSeriesResultHeader(),
                bannerTeamA,
                "",
                bannerTeamB,
                TournamentAnnouncementDuration,
                () => AdvanceTournamentAfterSeriesResult(seriesWinner, seriesLoser)));
        }
        else
        {
            AdvanceTournamentAfterSeriesResult(seriesWinner, seriesLoser);
        }
    }

    private void AdvanceTournamentAfterSeriesResult(string seriesWinner, string seriesLoser)
    {
        if (CurrentPhase == TournamentPhase.RoundsAboveSemis)
        {
            // Advance winner to the next round
            NextRoundTeams.Add(seriesWinner);
            CurrentRoundMatchIndex++;

            // If we've played all pairs in this round, move to the next round
            if (2 * CurrentRoundMatchIndex >= CurrentRoundTeams.Count)
            {
                // Collapse to the winners of this round
                CurrentRoundTeams = NextRoundTeams;
                NextRoundTeams = new List<string>();
                CurrentRoundMatchIndex = 0;

                // When we reach 4 remaining, move into the semifinal/placement structure
                if (CurrentRoundTeams.Count == 4)
                {
                    // Transition into Semifinal1 and immediately set up the first semifinal pairing
                    CurrentPhase = TournamentPhase.Semifinal1;
                    SetupNextSeriesPair();
                }
            }

            if (CurrentPhase == TournamentPhase.RoundsAboveSemis)
            {
                // Still in generic rounds-above-semifinals: prepare the next pair in this round
                SetupNextSeriesPair();
            }
        }
        else if (CurrentPhase == TournamentPhase.Semifinal1)
        {
            SemiWinner1 = seriesWinner;
            SemiLoser1 = seriesLoser;
            CurrentPhase = TournamentPhase.Semifinal2;
            SetupNextSeriesPair();
        }
        else if (CurrentPhase == TournamentPhase.Semifinal2)
        {
            SemiWinner2 = seriesWinner;
            SemiLoser2 = seriesLoser;

            // Schedule the 3rd-place match before the championship
            CurrentPhase = TournamentPhase.ThirdPlace;
            SetupNextSeriesPair();
        }
        else if (CurrentPhase == TournamentPhase.ThirdPlace)
        {
            // 3rd place is decided; now move to the championship
            CurrentPhase = TournamentPhase.Championship;
            SetupNextSeriesPair();
        }
        else if (CurrentPhase == TournamentPhase.Championship)
        {
            // Final championship series winner is the tournament champion.
            LogEvent($"Tournament complete. Champion: {seriesWinner}");
            CurrentPhase = TournamentPhase.Completed;

            // === FINAL TOURNAMENT END BEHAVIOR ===
            // Stub: this is where automated capture software would stop recording.
            // e.g. OBS or an external script listening for this signal.
            // STOP VIDEO RECORDING HERE.

            // Quit the application cleanly after the final results screen.
            QuitApp();
            return;
        }

        if (CurrentPhase != TournamentPhase.Completed)
        {
            // Start a new series with the next pair
            SeriesMatchIndex = 0;
            CurrentLevel = 1;
            ConsecutiveTiesAtCurrentLevel = 0;

            SetLevelPreset(CurrentLevel);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Human-readable labels for tournament announcements
    private string GetPhaseLabelForAnnouncement()
    {
        switch (CurrentPhase)
        {
            case TournamentPhase.Semifinal1:
                return "Semifinal 1";
            case TournamentPhase.Semifinal2:
                return "Semifinal 2";
            case TournamentPhase.ThirdPlace:
                return "Third Place Match";
            case TournamentPhase.Championship:
                return "Championship";
            case TournamentPhase.RoundsAboveSemis:
            default:
                if (CurrentRoundTeams != null && CurrentRoundTeams.Count > 0)
                {
                    int count = CurrentRoundTeams.Count;
                    if (count == 16) return "Round of 16";
                    if (count == 8) return "Quarterfinal";
                    if (count == 32) return "Round of 32";
                    return $"Round of {count}";
                }
                return "Elimination Round";
        }
    }

    private string GetSeriesStartHeader()
    {
        return GetPhaseLabelForAnnouncement() + " Matchup";
    }

    private string GetSeriesResultHeader()
    {
        return GetPhaseLabelForAnnouncement() + " Result";
    }

    // Series intro sequence: At the very start of the tournament, emit a magenta sync marker,
    // then show the series intro banner with current HUD team names.
    private IEnumerator RunSeriesIntroSequence()
    {
        // Only the very first series gets a leading magenta cap.
        // For all later series, the preceding series' end marker serves as the
        // boundary before this intro.
        if (TournamentEnableSyncMarkers && TournamentSyncMarkerCanvas != null && !hasShownSeriesIntroMarker)
        {
            yield return StartCoroutine(ShowSyncMarker());
            hasShownSeriesIntroMarker = true;
        }
        yield return StartCoroutine(
            ShowTournamentAnnouncementForCurrentHUD(
                GetSeriesStartHeader(),
                TournamentAnnouncementDuration,
                null));
    }

    // Show a tournament announcement using the current HUD team labels.
    // This is used at the start of each best-of-3 series. It waits one
    // frame so that MinionScript.ConfigureForTeam() has a chance to
    // populate TeamAUIText / TeamBUIText, then pauses time, displays
    // the banner for a real-time duration, and resumes.
    private IEnumerator ShowTournamentAnnouncementForCurrentHUD(
        string header,
        float durationSeconds,
        System.Action onComplete)
    {
        if (TournamentCanvas == null)
        {
            onComplete?.Invoke();
            yield break;
        }
        // Allow one frame for HUD labels to be configured.
        yield return null;
        string teamALabel = TeamAUIText != null ? TeamAUIText.text : string.Empty;
        string teamBLabel = TeamBUIText != null ? TeamBUIText.text : string.Empty;
        float previousTimeScale = Time.timeScale;
        TournamentMatchDetail.text = header;
        if (TournamentDetail2 != null)
        {
            // For matchup announcements, explicitly show "versus"
            TournamentDetail2.text = "versus";
        }
        TournamentTeamA.text = teamALabel;
        TournamentTeamB.text = teamBLabel;
        TournamentCanvas.SetActive(true);
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(durationSeconds);
        Time.timeScale = previousTimeScale;
        TournamentCanvas.SetActive(false);
        onComplete?.Invoke();
    }

    // Show a tournament announcement with explicit team labels (for
    // example, series winner vs. loser). Used after a series concludes,
    // before advancing the bracket.
    private IEnumerator ShowTournamentAnnouncementWithNames(
        string header,
        string header2,
        string teamALabel,
        string teamBLabel,
        float durationSeconds,
        System.Action onComplete)
    {
        if (TournamentCanvas == null)
        {
            onComplete?.Invoke();
            yield break;
        }
        float previousTimeScale = Time.timeScale;
        TournamentMatchDetail.text = header;
        TournamentDetail2.text = header2;
        TournamentTeamA.text = teamALabel;
        TournamentTeamB.text = teamBLabel;
        TournamentCanvas.SetActive(true);
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(durationSeconds);
        Time.timeScale = previousTimeScale;
        TournamentCanvas.SetActive(false);
        // SERIES-END SENTINEL:
        // After the result banner is fully shown and time restored,
        // emit the magenta boundary that closes this series.
        if (TournamentEnableSyncMarkers && TournamentSyncMarkerCanvas != null)
        {
            yield return StartCoroutine(ShowSyncMarker());
        }
        onComplete?.Invoke();
    }

    // Show a solid magenta full-screen overlay for a fixed number of rendered frames.
    // This acts as the deterministic sync marker for video segmentation.
    private IEnumerator ShowSyncMarker()
    {
        if (!TournamentEnableSyncMarkers || TournamentSyncMarkerCanvas == null)
        {
            yield break;
        }
        TournamentSyncMarkerCanvas.SetActive(true);
        int frames = Mathf.Max(1, TournamentSyncMarkerFrames);
        for (int i = 0; i < frames; ++i)
        {
            yield return new WaitForEndOfFrame();
        }
        TournamentSyncMarkerCanvas.SetActive(false);
    }

    private bool AllMinionsReachedTarget()
    {
        bool anyMinion = false;

        if (TeamAMinions != null)
        {
            foreach (var m in TeamAMinions)
            {
                if (m == null)
                    continue;

                anyMinion = true;

                // MinionScript exposes whether its NavMeshAgent has reached destination
                if (!m.ReachedTarget())
                    return false;
            }
        }

        if (TeamBMinions != null)
        {
            foreach (var m in TeamBMinions)
            {
                if (m == null)
                    continue;

                anyMinion = true;

                if (!m.ReachedTarget())
                    return false;
            }
        }

        // If there are no minions for some reason, treat as settled.
        return true;
    }

    private void UpdateSeriesScoreUI()
    {
        if (!tournamentMode)
            return;

        int teamASeriesWins = 0;
        int teamBSeriesWins = 0;

        if (TeamAAssemblyQualifiedName == SeriesTeam1)
        {
            teamASeriesWins = SeriesWins1;
        }
        else if (TeamAAssemblyQualifiedName == SeriesTeam2)
        {
            teamASeriesWins = SeriesWins2;
        }

        if (TeamBAssemblyQualifiedName == SeriesTeam1)
        {
            teamBSeriesWins = SeriesWins1;
        }
        else if (TeamBAssemblyQualifiedName == SeriesTeam2)
        {
            teamBSeriesWins = SeriesWins2;
        }

        TeamAUIWinsText.text = teamASeriesWins.ToString();
        TeamBUIWinsText.text = teamBSeriesWins.ToString();
    }


        // Logging helper
    private void LogEvent(string text)
    {
        Debug.Log(text);

        if (!enableMatchLogging)
            return;

        if (!logFileInitialized)
        {
            string dir = Path.Combine(Application.persistentDataPath, "PrisonDodgeballLogs");
            Directory.CreateDirectory(dir);
            logFilePath = Path.Combine(dir, "log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            Debug.Log($"Tournament dedicated log file: {logFilePath}");
            logFileInitialized = true;
        }

        try
        {
            string stamped = DateTime.UtcNow.ToString("[yyyy-MM-ddTHH:mm:ss.fffZ]") + " " + text;
            File.AppendAllText(logFilePath, stamped + "\n");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to write log file: " + e.Message);
        }
    }

    private void QuitApp()
    {

    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }


}


