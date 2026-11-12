using System;
using System.Collections.Generic;
using BombBrick.Combat;
using BombBrick.Game.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BrickSurvivorBootstrap
{
    private const string PrototypeScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SavedAnnihilationScenePath = "Assets/Scenes/AnnihilationGreybox.unity";
    private const string ScaleTrialScenePath = "Assets/Scenes/ScaleTrialGreybox.unity";
    private const string SealExcavationScenePath = "Assets/Scenes/SealExcavationGreybox.unity";
    private const string TripleSealDefenseScenePath = "Assets/Scenes/TripleSealDefenseGreybox.unity";
    private const string TimedSurvivalScenePath = "Assets/Scenes/TimedSurvivalGreybox.unity";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string activeScenePath=SceneManager.GetActiveScene().path;
        if (activeScenePath != PrototypeScenePath && activeScenePath != SavedAnnihilationScenePath &&
            activeScenePath != ScaleTrialScenePath && activeScenePath != SealExcavationScenePath &&
            activeScenePath != TripleSealDefenseScenePath && activeScenePath != TimedSurvivalScenePath)
            return;

        if (UnityEngine.Object.FindObjectOfType<BrickSurvivorGame>() == null)
            new GameObject("Brick Survivor Prototype").AddComponent<BrickSurvivorGame>();
    }
}

public sealed class BrickSurvivorAnimationEvents : MonoBehaviour
{
    private BrickSurvivorGame owner;

    public void Initialize(BrickSurvivorGame game)
    {
        owner=game;
    }

    public void OnAttackEvent_HitImmediate()
    {
        if(owner!=null)owner.HandleBasicAttackHit();
    }
}

public sealed class BrickSurvivorGame : MonoBehaviour, IBattleRuntime
{
    private const int Grid = 48;
    private const float Cell = 0.1033333f;
    private const int VoxelMeshDepth = 2;
    private const int MaxEnemyRenderSlots = 256;
    private const float PlayerSpeed = 7f;
    private const float AttackDamage = 42f;
    private const float AttackCooldown = 0.42f;
    private const float AttackRange = 5.7f;
    private const float AttackHalfAngle = 42f;
    private const float ArenaRadius = 40.5f;
    private const float CameraDistance = 36.77f;
    private const float CameraDownwardAngle = 35f;
    private const float InputBufferDuration = 0.12f;
    private const float DashDistance = 3.8f;
    private const float DashDuration = 0.2f;
    private const float DashCooldown = 5f;
    private const float DashInvulnerabilityDuration = 0.12f;
    private const float DashSlowDuration = 0.75f;
    private const float DashSlowMultiplier = 0.42f;
    private const float PlayerHitStunDuration = 0.22f;
    private const int MaxCrackStacks = 5;
    private const int MaxCrackedEnemies = 10;
    private const int MaxDetachedIslandBricks = 12;
    private const float CrackMarkRange = 12f;
    private const float CrackDetonationRange = 8f;
    private const float CrackBaseRadius = 3.6f;
    private const float CrackRadiusPerExtraStack = 0.9f;
    private const int PlayerMaxHealth = 100;
    private const int EnemyContactDamage = 5;
    private const float StonehornRushTriggerMinDistance = 3.2f;
    private const float StonehornRushTriggerMaxDistance = 10.5f;
    private const float StonehornRushReadyFallbackDuration = 0.52f;
    private const float StonehornRushDuration = 0.62f;
    private const float StonehornRushRecoveryDuration = 0.38f;
    private const float StonehornRushCooldown = 3.4f;
    private const float StonehornRushSpeed = 10.5f;
    private const float TyrannosaurusTargetHeight = 2.65f;
    private const float EnemySeparationRadiusFactor = 0.82f;
    private const float EnemySeparationResponsiveness = 16f;
    // The authored dinosaur points along local +X; rotate it so its head follows world forward.
    private const float TyrannosaurusYawOffset = -90f;
    private const bool UseProceduralTyrannosaurusMotion = false;

    private enum ActionState { Normal, Casting, Recovery, Dashing, HitStunned, Dead }
    private enum EnemySpecialType { None, StonehornRush }
    private enum EnemyActionState { Moving, RushReady, Rushing, RushRecovery }
    private enum PrototypeMapMode { Annihilation, ScaleTrial, SealExcavation, TripleSealDefense, TimedSurvival }
    private enum ScaleTrialState { Fighting, Choosing, Completed, Failed }
    private enum SealExcavationState { Planning, Fighting, Completed, Failed }
    private enum TripleSealDefenseState { Preparing, Fighting, Completed, Failed }
    private enum TimedSurvivalState { Fighting, Cleanup, Completed, Failed }
    private enum DefenseEnemyRole { None, Raider, Hunter, Siege, Commander }

    private static readonly Color32[] Palette =
    {
        new Color32(24,18,35,255), new Color32(55,35,63,255),
        new Color32(95,50,76,255), new Color32(153,69,91,255),
        new Color32(217,100,112,255), new Color32(250,158,132,255),
        new Color32(255,211,169,255), new Color32(255,239,210,255),
        new Color32(41,55,84,255), new Color32(50,88,126,255),
        new Color32(54,136,173,255), new Color32(95,191,205,255),
        new Color32(113,76,132,255), new Color32(178,101,175,255),
        new Color32(226,155,213,255), new Color32(238,238,244,255),
        new Color32(52,24,4,255), new Color32(90,39,4,255),
        new Color32(143,22,0,255), new Color32(231,57,1,255),
        new Color32(245,89,0,255), new Color32(221,115,17,255),
        new Color32(241,151,27,255), new Color32(252,241,215,255),
        new Color32(38,84,4,255), new Color32(88,140,13,255),
        new Color32(153,197,21,255),
        new Color32(27,25,31,255), new Color32(48,45,50,255),
        new Color32(75,70,72,255), new Color32(108,101,96,255),
        new Color32(151,139,122,255), new Color32(197,174,135,255),
        new Color32(231,205,158,255), new Color32(57,25,77,255),
        new Color32(91,36,111,255), new Color32(135,49,145,255),
        new Color32(184,64,177,255), new Color32(230,91,221,255)
    };

    private sealed class EnemyAnimationClip
    {
        public string id;
        public byte[][] frames;
        public float fps;
        public bool loop;
        public Mesh[] meshes;
        public List<Matrix4x4>[] meshBatches;
        public List<float>[] damageLayers;
    }

    private sealed class Enemy
    {
        public int encounterGroup;
        public bool encounterActivated;
        public int archetypeIndex;
        public Vector3 position;
        public float speed;
        public float attackSpeed;
        public float damageMultiplier;
        public DefenseEnemyRole defenseRole;
        public int defenseSealTarget=-1;
        public float defenseSealDamageMultiplier=1f;
        public float attackCooldownRemaining;
        public float scale;
        public float collisionRadius;
        public float visualHeight;
        public float hp;
        public float maxHp;
        public CombatDefense defense;
        public float phase;
        public float animationTime;
        public Vector3 knockbackVelocity;
        public Vector3 rushDirection;
        public Vector3 facingDirection;
        public float idleAnimationSpeed;
        public float turnSpeedDegrees;
        public float animationStartPhase;
        public EnemyActionState actionState;
        public float actionTimer;
        public float specialCooldownRemaining;
        public bool flipX;
        public bool dead;
        public int alive;
        public int initialBricks;
        public int crackStacks;
        public long crackOrder;
        public int damageMaskLayer=-1;
        public GameObject modelVisual;
        public VoxelTyrannosaurusProceduralMotion proceduralMotion;
        public Animator modelAnimator;
        public VoxelEnemyReaction reaction;
        public Vector3 lastImpactDirection;
        public readonly bool[] visible = new bool[Grid * Grid];
        public readonly byte[] color = new byte[Grid * Grid];
    }

    private sealed class EnemyArchetype
    {
        public string id;
        public GameObject modelPrefab;
        public EnemyAnimationClip moveClip;
        public EnemyAnimationClip rushReadyClip;
        public EnemyAnimationClip rushAttackClip;
        public int[] occupiedIndices;
        public byte[] unionColors;
        public float visualScale;
        public float maxHp;
        public float moveSpeed;
        public float attackSpeed;
        public float animationFps;
        public float spawnWeight;
        public int contactDamage;
        public int rushDamage;
        public EnemySpecialType specialType;
        public int minX;
        public int maxX;
        public int minY;
        public int maxY;
        public float modelCollisionRadius;
        public float modelVisualHeight;
        public float modelYawOffset;
        public float CollisionRadius => modelPrefab!=null?modelCollisionRadius:Mathf.Max(0.45f,(maxX-minX+1)*Cell*visualScale*0.42f);
        public float VisualHeight => modelPrefab!=null?modelVisualHeight:(maxY-minY+1)*Cell*visualScale;
    }

    private sealed class ArenaObstacle
    {
        public Vector3 center;
        public float radius;
    }

    private struct Fragment
    {
        public Vector3 position;
        public Vector3 velocity;
        public float life;
        public float maxLife;
        public float spin;
        public byte color;
    }

    private struct PhaseBrick
    {
        public Vector3 origin;
        public float age;
        public float delay;
        public float duration;
        public float size;
        public float drift;
        public byte color;
    }

    private struct CrackBlast
    {
        public Enemy source;
        public Vector3 position;
        public float radius;
        public DamagePacket damage;
        public int stacks;
    }

    private struct CrackRequest
    {
        public Enemy source;
        public int stacks;
    }

    private struct CrackPulse
    {
        public Vector3 position;
        public float radius;
        public float age;
        public float delay;
        public float duration;
    }

    private sealed class SealStone
    {
        public GameObject root;
        public Renderer renderer;
        public Material material;
        public Vector3 position;
        public SealRiskType risk;
        public SealBlessingType blessing;
        public int selectionOrder;
        public bool cleared;
    }

    private sealed class DefenseSeal
    {
        public GameObject root;
        public Renderer renderer;
        public Material material;
        public Color baseColor;
        public Vector3 position;
        public string rewardName;
        public float hp;
        public float maxHp;
        public bool Destroyed => hp<=0f;
    }

    private readonly List<Enemy> enemies = new List<Enemy>(128);
    private readonly Stack<Enemy> enemyPool = new Stack<Enemy>(128);
    private readonly Dictionary<int,Stack<GameObject>> modelVisualPools = new Dictionary<int,Stack<GameObject>>();
    private readonly Dictionary<GameObject,int> modelVisualArchetypes = new Dictionary<GameObject,int>(128);
    private readonly Dictionary<GameObject,Vector3> modelBaseScales = new Dictionary<GameObject,Vector3>(128);
    private readonly List<GameObject> collapsingEnemyVisuals = new List<GameObject>(64);
    private readonly List<Fragment> fragments = new List<Fragment>(600);
    private readonly List<PhaseBrick> phaseBricks = new List<PhaseBrick>(1200);
    private readonly List<CrackBlast> crackBlasts = new List<CrackBlast>(MaxCrackedEnemies);
    private readonly List<CrackRequest> crackChainQueue = new List<CrackRequest>(MaxCrackedEnemies);
    private readonly List<CrackPulse> crackPulses = new List<CrackPulse>(MaxCrackedEnemies+4);
    private readonly List<Matrix4x4> crackBrickBatch = new List<Matrix4x4>(2048);
    private readonly List<int> exposedBricks = new List<int>(Grid*Grid);
    private readonly int[] brickComponentLabels = new int[Grid*Grid];
    private readonly int[] brickComponentSizes = new int[Grid*Grid];
    private readonly int[] brickConnectivityQueue = new int[Grid*Grid];
    private readonly List<Matrix4x4>[] brickBatches = new List<Matrix4x4>[Palette.Length];
    private readonly List<Matrix4x4>[] fragmentBatches = new List<Matrix4x4>[Palette.Length];
    private readonly List<Matrix4x4>[] phaseBatches = new List<Matrix4x4>[Palette.Length];
    [SerializeField] private AnnihilationMapPreset eliminationMap;
    [SerializeField] private ScaleTrialMapPreset scaleTrialMap;
    [SerializeField] private SealExcavationMapPreset sealExcavationMap;
    [SerializeField] private TripleSealDefenseMapPreset tripleSealDefenseMap;
    [SerializeField] private TimedSurvivalMapPreset timedSurvivalMap;
    private CombatMapPreset activeMap;
    private PrototypeMapMode mapMode;
    private readonly List<Renderer> eliminationGroupMarkers = new List<Renderer>(12);
    private readonly List<Material> eliminationMaterials = new List<Material>(16);
    private readonly List<ArenaObstacle> arenaObstacles = new List<ArenaObstacle>(16);
    private readonly Matrix4x4[] drawBuffer = new Matrix4x4[1023];
    private readonly List<Vector3> meshVertices = new List<Vector3>(12000);
    private readonly List<Vector3> meshNormals = new List<Vector3>(12000);
    private readonly List<Vector2> meshUvs = new List<Vector2>(12000);
    private readonly List<Vector2> meshDamageUvs = new List<Vector2>(12000);
    private readonly List<int> meshTriangles = new List<int>(18000);

    private Camera cam;
    private Mesh cubeMesh;
    private Material[] materials;
    private Material[] phaseMaterials;
    private Material voxelMeshMaterial;
    private Texture2D voxelPaletteTexture;
    private Texture2DArray voxelDamageMasks;
    private MaterialPropertyBlock voxelMeshProperties;
    private readonly float[] voxelDamageLayerBuffer = new float[1023];
    private readonly byte[] damageMaskUploadBuffer = new byte[Grid*Grid];
    private int allocatedDamageMaskLayers;
    private bool damageMasksNeedApply;
    private EnemyArchetype[] enemyArchetypes;
    private Transform enemyVisualRoot;
    private Transform player;
    private Transform playerArrow;
    private Transform playerVisual;
    private Animator playerAnimator;
    private Transform playerUpperBody;
    private Transform attackVisual;
    private Material attackMaterial;
    private Mesh crackRingMesh;
    private Material crackMaterial;
    private readonly Transform[] skillSignals = new Transform[2];
    private readonly Material[] skillSignalMaterials = new Material[2];
    private readonly float[] skillSignalLife = new float[2];
    private readonly Vector3[] skillSignalOrigin = new Vector3[2];
    private readonly int[] lastSkillHits = new int[2];
    private TrailRenderer dashTrail;
    private ParticleSystem impactParticles;
    private VoxelPlayerDamageReaction playerDamageReaction;
    private VoxelPlayerRagdoll playerRagdoll;
    private Vector3 playerKnockbackVelocity;
    private Vector3 aimDirection = Vector3.forward;
    private float attackTimer;
    private float attackAnimationRemaining;
    private float attackVisualLife;
    private float spawnTimer;
    private float elapsed;
    private float fps = 60f;
    private float hitStopRemaining;
    private float timeScaleBeforeHitStop=1f;
    private bool hitStopActive;
    private readonly float[] skillCooldown = {0.2f,1.5f};
    private readonly float[] skillCooldownRemaining = new float[2];
    private readonly float[] skillCastTime = {0.06f,0.14f};
    private readonly float[] skillRecoveryTime = {0.08f,0.2f};
    private ActionState actionState;
    private float actionTimer;
    private int activeSkill=-1;
    private int bufferedSkill=-1;
    private Enemy bufferedCrackTarget;
    private Enemy activeCrackTarget;
    private float inputBufferTimer;
    private float dashCooldownRemaining;
    private Vector3 dashStart;
    private Vector3 dashEnd;
    private float dashElapsed;
    private float dashSlowRemaining;
    private float movementAmount;
    private Vector3 movementDirection;
    private bool movingBackward;
    private bool stressMode;
    private bool useVoxelMeshRendering=true;
    private static readonly int DamageLayerProperty=Shader.PropertyToID("_DamageLayer");
    private int playerHp = PlayerMaxHealth;
    private int score;
    private int kills;
    private long crackOrderSequence;
    private bool playerDefeatRaised;
    private bool eliminationMapComplete;
    private float eliminationClearTime;
    private int currentEliminationGroupCount;
    private int currentSpawnCandidateCount;
    private Transform eliminationMarkerRoot;
    private Transform greyboxRoot;
    private GameObject eliminationReward;
    private GameObject eliminationExit;
    private GameObject eliminationEntrySeal;
    private ScaleTrialState scaleTrialState;
    private int scaleTrialStage;
    private float scaleChoiceDelay;
    private float scaleChallengeHold;
    private bool scaleWaitForMouseRelease;
    private readonly List<SealStone> sealStones=new List<SealStone>(8);
    private readonly List<GameObject> sealRewardDrops=new List<GameObject>(24);
    private SealExcavationState sealExcavationState;
    private int selectedSealCount;
    private int activeSealOrder;
    private float sealEnemyHealthMultiplier=1f;
    private float sealEnemyDamageMultiplier=1f;
    private float sealEnemyMoveMultiplier=1f;
    private float sealEnemyAttackSpeedMultiplier=1f;
    private int sealExtraEnemies;
    private float playerMoveSpeedMultiplier=1f;
    private float playerDamageMultiplier=1f;
    private float playerAttackSpeedMultiplier=1f;
    private float playerItemRarityBonus;
    private string sealRewardMessage=string.Empty;
    private float sealRewardMessageTime;
    private readonly List<DefenseSeal> defenseSeals=new List<DefenseSeal>(3);
    private TripleSealDefenseState tripleSealDefenseState;
    private int defenseWave;
    private float defenseWaveDelay;
    private int defenseFocusedSeal=-1;
    private string defenseAlert=string.Empty;
    private float defenseAlertTime;
    private TimedSurvivalState timedSurvivalState;
    private float timedSurvivalRemaining;
    private float timedSurvivalSpawnCooldown;
    private int timedSurvivalBatch;
    private int timedSurvivalTotalSpawned;
    private Vector3 cameraBase;
    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle centerStyle;
    private static readonly int LocomotionParameter=Animator.StringToHash("Locomotion");
    private static readonly int DashParameter=Animator.StringToHash("Dash");
    private static readonly int AttackParameter=Animator.StringToHash("Attack");
    private static readonly int EnemyMovingParameter=Animator.StringToHash("Moving");

    public event Action PlayerDefeated;
    public bool IsPlayerDefeated => playerHp <= 0;
    public int CurrentHealth => playerHp;
    public int MaximumHealth => PlayerMaxHealth;
    public int Score => score;
    public int Kills => kills;

    private void Awake()
    {
        Time.timeScale=1f;
        timeScaleBeforeHitStop=1f;
        Application.runInBackground = true;
        Application.targetFrameRate = 144;
        QualitySettings.vSyncCount = 0;
        for (int i = 0; i < Palette.Length; i++)
        {
            brickBatches[i] = new List<Matrix4x4>(2048);
            fragmentBatches[i] = new List<Matrix4x4>(128);
            phaseBatches[i] = new List<Matrix4x4>(256);
        }
        string activeScenePath=SceneManager.GetActiveScene().path;
        bool isScaleTrial=activeScenePath=="Assets/Scenes/ScaleTrialGreybox.unity";
        bool isSealExcavation=activeScenePath=="Assets/Scenes/SealExcavationGreybox.unity";
        bool isTripleSealDefense=activeScenePath=="Assets/Scenes/TripleSealDefenseGreybox.unity";
        bool isTimedSurvival=activeScenePath=="Assets/Scenes/TimedSurvivalGreybox.unity";
        mapMode=isScaleTrial?PrototypeMapMode.ScaleTrial:
            isSealExcavation?PrototypeMapMode.SealExcavation:
            isTripleSealDefense?PrototypeMapMode.TripleSealDefense:
            isTimedSurvival?PrototypeMapMode.TimedSurvival:PrototypeMapMode.Annihilation;
        if(isScaleTrial)
        {
            scaleTrialMap=Resources.Load<ScaleTrialMapPreset>("Maps/ScaleTrialGreybox");
            if(scaleTrialMap==null)throw new InvalidOperationException("Scale trial map preset was not found in Resources/Maps.");
            activeMap=scaleTrialMap;
        }
        else if(isSealExcavation)
        {
            sealExcavationMap=Resources.Load<SealExcavationMapPreset>("Maps/SealExcavationGreybox");
            if(sealExcavationMap==null)throw new InvalidOperationException("Seal excavation map preset was not found in Resources/Maps.");
            activeMap=sealExcavationMap;
        }
        else if(isTripleSealDefense)
        {
            tripleSealDefenseMap=Resources.Load<TripleSealDefenseMapPreset>("Maps/TripleSealDefenseGreybox");
            if(tripleSealDefenseMap==null)throw new InvalidOperationException("Triple seal defense map preset was not found in Resources/Maps.");
            activeMap=tripleSealDefenseMap;
        }
        else if(isTimedSurvival)
        {
            timedSurvivalMap=Resources.Load<TimedSurvivalMapPreset>("Maps/TimedSurvivalGreybox");
            if(timedSurvivalMap==null)throw new InvalidOperationException("Timed survival map preset was not found in Resources/Maps.");
            activeMap=timedSurvivalMap;
        }
        else
        {
            eliminationMap=Resources.Load<AnnihilationMapPreset>("Maps/AnnihilationGreybox");
            if(eliminationMap==null)throw new InvalidOperationException("Annihilation map preset was not found in Resources/Maps.");
            activeMap=eliminationMap;
        }
        LoadEnemyArchetypes();
        CreateRendering();
        CreateArena();
        enemyVisualRoot=new GameObject("Enemy Model Visuals").transform;
        enemyVisualRoot.SetParent(transform,false);
        ResetRun();
    }

    private void LoadEnemyArchetypes()
    {
        GameObject tyrannosaurus=Resources.Load<GameObject>("Enemies/VoxelTyrannosaurusEnemy");
        if(tyrannosaurus==null)
            throw new InvalidOperationException("Voxel Tyrannosaurus authoring prefab was not found in Resources/Enemies.");
        GameObject velociraptor=Resources.Load<GameObject>("Enemies/VelociraptorEnemy");
        if(velociraptor==null)
            throw new InvalidOperationException("Velociraptor authoring prefab was not found in Resources/Enemies.");
        GameObject mechanicalVelociraptor=Resources.Load<GameObject>("Enemies/MechanicalVelociraptorEnemy");
        if(mechanicalVelociraptor==null)
            throw new InvalidOperationException("Mechanical Velociraptor authoring prefab was not found in Resources/Enemies.");
        GameObject abyssalVelociraptor=Resources.Load<GameObject>("Enemies/AbyssalVelociraptorEnemy");
        if(abyssalVelociraptor==null)
            throw new InvalidOperationException("Abyssal Velociraptor authoring prefab was not found in Resources/Enemies.");
        GameObject infernalVelociraptor=Resources.Load<GameObject>("Enemies/InfernalVelociraptorEnemy");
        if(infernalVelociraptor==null)
            throw new InvalidOperationException("Infernal Velociraptor authoring prefab was not found in Resources/Enemies.");
        enemyArchetypes=new[]
        {
            new EnemyArchetype
            {
                id="VoxelTyrannosaurus",modelPrefab=tyrannosaurus,visualScale=1f,
                maxHp=180f,moveSpeed=1.65f,attackSpeed=0.85f,spawnWeight=0.72f,contactDamage=8,
                modelCollisionRadius=1.05f,modelVisualHeight=TyrannosaurusTargetHeight,
                modelYawOffset=TyrannosaurusYawOffset,
                specialType=EnemySpecialType.None
            },
            new EnemyArchetype
            {
                id="Velociraptor",modelPrefab=velociraptor,visualScale=1f,
                maxHp=95f,moveSpeed=2.45f,attackSpeed=1.2f,spawnWeight=0.32f,contactDamage=5,
                modelCollisionRadius=0.68f,modelVisualHeight=1.72f,modelYawOffset=0f,
                specialType=EnemySpecialType.None
            },
            new EnemyArchetype
            {
                id="MechanicalVelociraptor",modelPrefab=mechanicalVelociraptor,visualScale=1f,
                maxHp=95f,moveSpeed=2.45f,attackSpeed=1.2f,spawnWeight=0.32f,contactDamage=5,
                modelCollisionRadius=0.68f,modelVisualHeight=1.72f,modelYawOffset=0f,
                specialType=EnemySpecialType.None
            },
            new EnemyArchetype
            {
                id="AbyssalVelociraptor",modelPrefab=abyssalVelociraptor,visualScale=1f,
                maxHp=95f,moveSpeed=2.45f,attackSpeed=1.2f,spawnWeight=0.32f,contactDamage=5,
                modelCollisionRadius=0.68f,modelVisualHeight=1.72f,modelYawOffset=0f,
                specialType=EnemySpecialType.None
            },
            new EnemyArchetype
            {
                id="InfernalVelociraptor",modelPrefab=infernalVelociraptor,visualScale=1f,
                maxHp=95f,moveSpeed=2.45f,attackSpeed=1.2f,spawnWeight=0.32f,contactDamage=5,
                modelCollisionRadius=0.68f,modelVisualHeight=1.72f,modelYawOffset=0f,
                specialType=EnemySpecialType.None
            }
        };
        Debug.Log("[Brick Survivor] Loaded Tyrannosaurus and four Velociraptor enemy archetypes.");
    }

    private EnemyArchetype BuildEnemyArchetype(
        string id,string resourcePath,float visualScale,float maxHp,float moveSpeed,float attackSpeed,float animationFps,
        float spawnWeight,int contactDamage,EnemySpecialType specialType=EnemySpecialType.None,
        string rushReadyPath=null,string rushAttackPath=null,int rushDamage=0)
    {
        EnemyAnimationClip moveClip=LoadEnemyAnimationClip(id+"_Move",resourcePath,animationFps,true);
        EnemyAnimationClip rushReadyClip=string.IsNullOrEmpty(rushReadyPath)?null:LoadEnemyAnimationClip(id+"_RushReady",rushReadyPath,8f,false);
        EnemyAnimationClip rushAttackClip=string.IsNullOrEmpty(rushAttackPath)?null:LoadEnemyAnimationClip(id+"_RushAttack",rushAttackPath,10f,true);

        byte[] unionColors=new byte[Grid*Grid];
        bool[] occupied=new bool[Grid*Grid];
        List<int> occupiedIndices=new List<int>(Grid*Grid);
        int minX=Grid,maxX=0,minY=Grid,maxY=0;
        IncludeClipPixels(moveClip,occupied,occupiedIndices,unionColors,ref minX,ref maxX,ref minY,ref maxY);
        IncludeClipPixels(rushReadyClip,occupied,occupiedIndices,unionColors,ref minX,ref maxX,ref minY,ref maxY);
        IncludeClipPixels(rushAttackClip,occupied,occupiedIndices,unionColors,ref minX,ref maxX,ref minY,ref maxY);
        if(occupiedIndices.Count==0)throw new InvalidOperationException($"{id} sheets contain no opaque pixels.");

        EnemyArchetype archetype=new EnemyArchetype
        {
            id=id,moveClip=moveClip,rushReadyClip=rushReadyClip,rushAttackClip=rushAttackClip,
            occupiedIndices=occupiedIndices.ToArray(),unionColors=unionColors,
            visualScale=visualScale,maxHp=maxHp,moveSpeed=moveSpeed,attackSpeed=attackSpeed,animationFps=animationFps,
            spawnWeight=spawnWeight,contactDamage=contactDamage,rushDamage=rushDamage,specialType=specialType,
            minX=minX,maxX=maxX,minY=minY,maxY=maxY
        };
        BuildAnimationMeshes(archetype,moveClip);
        BuildAnimationMeshes(archetype,rushReadyClip);
        BuildAnimationMeshes(archetype,rushAttackClip);
        return archetype;
    }

    private EnemyAnimationClip LoadEnemyAnimationClip(string id,string resourcePath,float fps,bool loop)
    {
        Texture2D sheet=Resources.Load<Texture2D>(resourcePath);
        if(sheet==null)throw new InvalidOperationException($"Enemy sheet not found in Resources: {resourcePath}");
        if(sheet.height!=Grid||sheet.width%Grid!=0)
            throw new InvalidOperationException($"{id} sheet must contain horizontal {Grid}x{Grid} frames. Actual: {sheet.width}x{sheet.height}");

        int frameCount=sheet.width/Grid;
        Color32[] pixels=sheet.GetPixels32();
        byte[][] frames=new byte[frameCount][];
        for(int frame=0;frame<frameCount;frame++)
        {
            byte[] frameColors=frames[frame]=new byte[Grid*Grid];
            int frameOffset=frame*Grid;
            for(int y=0;y<Grid;y++)
            for(int x=0;x<Grid;x++)
            {
                Color32 c=pixels[y*sheet.width+frameOffset+x];
                if(c.a<32)continue;
                int index=y*Grid+x;
                byte paletteColor=(byte)(NearestPalette(c)+1);
                frameColors[index]=paletteColor;
            }
        }
        return new EnemyAnimationClip{id=id,frames=frames,fps=fps,loop=loop};
    }

    private void IncludeClipPixels(
        EnemyAnimationClip clip,bool[] occupied,List<int> occupiedIndices,byte[] unionColors,
        ref int minX,ref int maxX,ref int minY,ref int maxY)
    {
        if(clip==null)return;
        for(int frame=0;frame<clip.frames.Length;frame++)
        for(int index=0;index<clip.frames[frame].Length;index++)
        {
            byte color=clip.frames[frame][index];
            if(color==0)continue;
            if(!occupied[index])
            {
                occupied[index]=true;
                occupiedIndices.Add(index);
                unionColors[index]=color;
            }
            int x=index%Grid,y=index/Grid;
            minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);
            minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
        }
    }

    private void BuildAnimationMeshes(EnemyArchetype archetype,EnemyAnimationClip clip)
    {
        if(clip==null)return;
        clip.meshes=new Mesh[clip.frames.Length];
        clip.meshBatches=new List<Matrix4x4>[clip.frames.Length];
        clip.damageLayers=new List<float>[clip.frames.Length];
        for(int frame=0;frame<clip.frames.Length;frame++)
        {
            clip.meshes[frame]=new Mesh{name=$"{clip.id}_Voxel_Frame_{frame}"};
            clip.meshBatches[frame]=new List<Matrix4x4>(128);
            clip.damageLayers[frame]=new List<float>(128);
            BuildVoxelMesh(clip.meshes[frame],archetype,clip.frames[frame]);
            clip.meshes[frame].UploadMeshData(true);
        }
    }

    private void BuildVoxelMesh(Mesh target,EnemyArchetype archetype,byte[] frameColors)
    {
        meshVertices.Clear();meshNormals.Clear();meshUvs.Clear();meshDamageUvs.Clear();meshTriangles.Clear();
        float half=Cell*0.5f;
        float halfDepth=Cell*VoxelMeshDepth*0.5f;
        for(int y=0;y<Grid;y++)
        for(int x=0;x<Grid;x++)
        {
            int index=y*Grid+x;
            byte color=frameColors[index];
            if(color==0)continue;
            Vector3 center=new Vector3((x-(Grid-1)*0.5f)*Cell,(y-archetype.minY)*Cell,0f);
            Vector2 damageUv=new Vector2((x+0.5f)/Grid,(y+0.5f)/Grid);
            AddVoxelFace(center+Vector3.left*half,Vector3.left,Vector3.forward*halfDepth,Vector3.up*half,color,damageUv);
            AddVoxelFace(center+Vector3.right*half,Vector3.right,Vector3.back*halfDepth,Vector3.up*half,color,damageUv);
            AddVoxelFace(center+Vector3.down*half,Vector3.down,Vector3.right*half,Vector3.forward*halfDepth,color,damageUv);
            AddVoxelFace(center+Vector3.up*half,Vector3.up,Vector3.right*half,Vector3.back*halfDepth,color,damageUv);
            AddVoxelFace(center+Vector3.back*halfDepth,Vector3.back,Vector3.left*half,Vector3.up*half,color,damageUv);
            AddVoxelFace(center+Vector3.forward*halfDepth,Vector3.forward,Vector3.right*half,Vector3.up*half,color,damageUv);
        }
        target.Clear();
        target.indexFormat=meshVertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
        target.SetVertices(meshVertices);target.SetNormals(meshNormals);target.SetUVs(0,meshUvs);target.SetUVs(1,meshDamageUvs);target.SetTriangles(meshTriangles,0,true);
        target.RecalculateBounds();
    }

    private void AddVoxelFace(Vector3 faceCenter,Vector3 normal,Vector3 faceRight,Vector3 faceUp,byte color,Vector2 damageUv)
    {
        int start=meshVertices.Count;
        meshVertices.Add(faceCenter-faceRight-faceUp);meshVertices.Add(faceCenter+faceRight-faceUp);meshVertices.Add(faceCenter+faceRight+faceUp);meshVertices.Add(faceCenter-faceRight+faceUp);
        meshNormals.Add(normal);meshNormals.Add(normal);meshNormals.Add(normal);meshNormals.Add(normal);
        float paletteU=(color-0.5f)/Palette.Length;
        Vector2 uv=new Vector2(paletteU,0.5f);
        meshUvs.Add(uv);meshUvs.Add(uv);meshUvs.Add(uv);meshUvs.Add(uv);
        meshDamageUvs.Add(damageUv);meshDamageUvs.Add(damageUv);meshDamageUvs.Add(damageUv);meshDamageUvs.Add(damageUv);
        meshTriangles.Add(start);meshTriangles.Add(start+1);meshTriangles.Add(start+2);
        meshTriangles.Add(start);meshTriangles.Add(start+2);meshTriangles.Add(start+3);
    }

    private void CreateRendering()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");
        materials = new Material[Palette.Length];
        phaseMaterials = new Material[Palette.Length];
        for (int i = 0; i < Palette.Length; i++)
        {
            materials[i] = new Material(lit){color=Palette[i],enableInstancing=true};
            materials[i].SetFloat("_Smoothness",0.12f);
            phaseMaterials[i] = new Material(lit){color=Color.Lerp(Palette[i],Color.white,0.38f),enableInstancing=true};
            phaseMaterials[i].SetFloat("_Smoothness",0.48f);
            phaseMaterials[i].EnableKeyword("_EMISSION");
            phaseMaterials[i].SetColor("_EmissionColor",Color.Lerp(Palette[i],new Color(0.2f,0.9f,1f),0.62f)*1.7f);
        }
        voxelPaletteTexture=new Texture2D(Palette.Length,1,TextureFormat.RGBA32,false,false)
        {
            name="Enemy Voxel Palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp
        };
        voxelPaletteTexture.SetPixels32(Palette);voxelPaletteTexture.Apply(false,true);
        voxelDamageMasks=new Texture2DArray(Grid,Grid,MaxEnemyRenderSlots,TextureFormat.R8,false,true)
        {
            name="Enemy Damage Masks",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,anisoLevel=0
        };
        voxelDamageMasks.Apply(false,false);
        Shader voxelShader=Shader.Find("BombBrick/Voxel Damage Mask");
        if(voxelShader==null)throw new InvalidOperationException("BombBrick/Voxel Damage Mask shader not found.");
        voxelMeshMaterial=new Material(voxelShader){name="Enemy Voxel Mesh Material",enableInstancing=true};
        voxelMeshMaterial.SetTexture("_BaseMap",voxelPaletteTexture);
        voxelMeshMaterial.SetTexture("_DamageMasks",voxelDamageMasks);
        voxelMeshMaterial.SetColor("_BaseColor",Color.white);
        voxelMeshProperties=new MaterialPropertyBlock();
    }

    private void CreateArena()
    {
        cam = Camera.main;
        if (cam == null)
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
        }
        cam.orthographic = false;
        cam.fieldOfView = 25f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        cam.backgroundColor = new Color(0.035f,0.045f,0.07f);
        float cameraAngleRadians=CameraDownwardAngle*Mathf.Deg2Rad;
        cameraBase = new Vector3(
            0f,
            Mathf.Sin(cameraAngleRadians)*CameraDistance,
            -Mathf.Cos(cameraAngleRadians)*CameraDistance);
        cam.transform.position = cameraBase;
        cam.transform.LookAt(Vector3.zero);

        Light light = FindObjectOfType<Light>();
        if (light == null) light = new GameObject("Key Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(55f,-30f,0f);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Survivor Arena";
        floor.transform.position = new Vector3(0f,-0.45f,0f);
        floor.transform.localScale = new Vector3(ArenaRadius*2f,0.16f,ArenaRadius*2f);
        Destroy(floor.GetComponent<Collider>());
        Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floorMat.color = new Color(0.07f,0.1f,0.14f);
        floor.GetComponent<Renderer>().material = floorMat;

        GameObject physicsFloor=new GameObject("Arena Physics Floor");
        physicsFloor.transform.position=new Vector3(0f,-0.1f,0f);
        BoxCollider floorCollider=physicsFloor.AddComponent<BoxCollider>();
        floorCollider.size=new Vector3(ArenaRadius*2f+8f,0.2f,ArenaRadius*2f+8f);
        CreateGreyboxTerrain();
        CreateEliminationMapVisuals();
        if(mapMode==PrototypeMapMode.SealExcavation)CreateSealExcavationVisuals();
        if(mapMode==PrototypeMapMode.TripleSealDefense)CreateTripleSealDefenseVisuals();
        CreatePlayer();
        CreateAttackVisual();
        CreateSkillSignals();
        CreateCrackVisuals();
        CreateImpactParticles();
    }

    private void CreateGreyboxTerrain()
    {
        greyboxRoot=new GameObject(activeMap.displayName+" Greybox Terrain").transform;
        greyboxRoot.SetParent(transform,false);
        Material pathMaterial=CreateEliminationMaterial("Greybox Walkable Area",new Color(0.11f,0.15f,0.19f),Color.black);
        Material obstacleMaterial=CreateEliminationMaterial("Greybox Obstacles",new Color(0.24f,0.27f,0.31f),Color.black);

        CreateGreyboxDisk("Central Combat Clearing",Vector3.zero,activeMap.centralClearingRadius,pathMaterial);
        for(int i=0;i<activeMap.paths.Length;i++)
        {
            GreyboxPathPreset path=activeMap.paths[i];
            CreateGreyboxPath(path.name,path.position,path.scale,path.yaw,pathMaterial);
        }
        for(int i=0;i<activeMap.obstacles.Length;i++)
        {
            GreyboxObstaclePreset obstacle=activeMap.obstacles[i];
            AddGreyboxObstacle(obstacle.center,obstacle.radius,obstacle.height,obstacle.yaw,obstacleMaterial);
        }
    }

    private void CreateGreyboxDisk(string objectName,Vector3 position,float radius,Material material)
    {
        GameObject disk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disk.name=objectName;
        disk.transform.SetParent(greyboxRoot,false);
        disk.transform.position=position+Vector3.down*0.27f;
        disk.transform.localScale=new Vector3(radius*2f,0.025f,radius*2f);
        Destroy(disk.GetComponent<Collider>());
        disk.GetComponent<Renderer>().sharedMaterial=material;
    }

    private void CreateGreyboxPath(string objectName,Vector3 position,Vector3 scale,float yaw,Material material)
    {
        GameObject path=GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.name=objectName;
        path.transform.SetParent(greyboxRoot,false);
        path.transform.position=position;
        path.transform.rotation=Quaternion.Euler(0f,yaw,0f);
        path.transform.localScale=scale;
        Destroy(path.GetComponent<Collider>());
        path.GetComponent<Renderer>().sharedMaterial=material;
    }

    private void AddGreyboxObstacle(Vector3 center,float radius,float height,float yaw,Material material)
    {
        arenaObstacles.Add(new ArenaObstacle{center=center,radius=radius});
        Transform cluster=new GameObject($"Obstacle Cluster {arenaObstacles.Count}").transform;
        cluster.SetParent(greyboxRoot,false);
        cluster.position=center;
        cluster.rotation=Quaternion.Euler(0f,yaw,0f);
        CreateGreyboxBlock(cluster,new Vector3(0f,height*0.5f-0.18f,0f),new Vector3(radius*1.45f,height,radius*0.78f),0f,material);
        CreateGreyboxBlock(cluster,new Vector3(radius*0.34f,height*0.37f,-radius*0.42f),new Vector3(radius*0.72f,height*0.72f,radius*0.95f),37f,material);
        CreateGreyboxBlock(cluster,new Vector3(-radius*0.38f,height*0.28f,radius*0.31f),new Vector3(radius*0.66f,height*0.56f,radius*0.72f),-31f,material);
    }

    private void CreateGreyboxBlock(Transform parent,Vector3 localPosition,Vector3 localScale,float yaw,Material material)
    {
        GameObject block=GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.transform.SetParent(parent,false);
        block.transform.localPosition=localPosition;
        block.transform.localRotation=Quaternion.Euler(0f,yaw,0f);
        block.transform.localScale=localScale;
        Destroy(block.GetComponent<Collider>());
        block.GetComponent<Renderer>().sharedMaterial=material;
    }

    private void CreateEliminationMapVisuals()
    {
        eliminationMarkerRoot=new GameObject("Elimination Group Markers").transform;
        eliminationMarkerRoot.SetParent(transform,false);

        eliminationReward=GameObject.CreatePrimitive(PrimitiveType.Cube);
        eliminationReward.name="Elimination Clear Reward";
        eliminationReward.transform.SetParent(transform,false);
        eliminationReward.transform.localScale=new Vector3(1.35f,1.1f,1.35f);
        Destroy(eliminationReward.GetComponent<Collider>());
        eliminationReward.GetComponent<Renderer>().sharedMaterial=CreateEliminationMaterial(
            "Clear Reward",new Color(1f,0.66f,0.08f),new Color(1f,0.42f,0.02f)*2.5f);
        eliminationReward.SetActive(false);

        eliminationExit=new GameObject("Elimination Exit Beacon");
        eliminationExit.transform.SetParent(transform,false);
        eliminationExit.transform.position=new Vector3(0f,0f,ArenaRadius-4f);
        CreateGatePart(eliminationExit.transform,new Vector3(-1.7f,1.7f,0f),new Vector3(0.45f,3.4f,0.45f));
        CreateGatePart(eliminationExit.transform,new Vector3(1.7f,1.7f,0f),new Vector3(0.45f,3.4f,0.45f));
        CreateGatePart(eliminationExit.transform,new Vector3(0f,3.25f,0f),new Vector3(3.8f,0.38f,0.45f));
        eliminationExit.SetActive(false);

        eliminationEntrySeal=GameObject.CreatePrimitive(PrimitiveType.Cube);
        eliminationEntrySeal.name="Closed Entry Seal";
        eliminationEntrySeal.transform.SetParent(transform,false);
        eliminationEntrySeal.transform.position=new Vector3(0f,1.3f,-ArenaRadius+3f);
        eliminationEntrySeal.transform.localScale=new Vector3(5f,2.6f,0.4f);
        Destroy(eliminationEntrySeal.GetComponent<Collider>());
        eliminationEntrySeal.GetComponent<Renderer>().sharedMaterial=CreateEliminationMaterial(
            "Entry Seal",new Color(0.35f,0.035f,0.04f),new Color(0.8f,0.03f,0.02f)*1.5f);
    }

    private void CreateSealExcavationVisuals()
    {
        int count=Mathf.Max(3,sealExcavationMap.sealCount);
        float rotation=UnityEngine.Random.Range(0f,Mathf.PI*2f);
        float radius=Mathf.Min(activeMap.centralClearingRadius-2.4f,14.5f);
        for(int i=0;i<count;i++)
        {
            float angle=rotation+i*Mathf.PI*2f/count;
            Vector3 position=new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
            GameObject stone=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stone.name=$"Seal Stone {i+1}";
            stone.transform.SetParent(transform,false);
            stone.transform.position=position+Vector3.up*0.78f;
            stone.transform.localScale=new Vector3(1.15f,0.78f,1.15f);
            Material material=CreateEliminationMaterial(
                $"Seal Stone {i+1} Material",new Color(0.18f,0.08f,0.28f),new Color(0.35f,0.04f,0.7f)*1.8f);
            Renderer renderer=stone.GetComponent<Renderer>();
            renderer.sharedMaterial=material;
            sealStones.Add(new SealStone
            {
                root=stone,renderer=renderer,material=material,position=position
            });
        }
    }

    private void CreateTripleSealDefenseVisuals()
    {
        string[] rewards={"EQUIPMENT","CRAFT MATERIAL","ITEM RARITY","COMBAT EXP","SKILL CHOICE","ACCESSORY"};
        List<int> rewardPool=new List<int>();
        for(int i=0;i<rewards.Length;i++)rewardPool.Add(i);
        for(int i=rewardPool.Count-1;i>0;i--)
        {
            int swap=UnityEngine.Random.Range(0,i+1);
            int temp=rewardPool[i];rewardPool[i]=rewardPool[swap];rewardPool[swap]=temp;
        }
        Color[] colors={new Color(0.78f,0.12f,0.08f),new Color(0.07f,0.42f,0.9f),new Color(0.9f,0.55f,0.03f)};
        float radius=tripleSealDefenseMap.sealDistanceFromCenter;
        for(int i=0;i<3;i++)
        {
            float angle=(90f+i*120f)*Mathf.Deg2Rad;
            Vector3 position=new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
            GameObject root=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name=$"Defense Seal {i+1}";
            root.transform.SetParent(transform,false);
            root.transform.position=position+Vector3.up*0.85f;
            root.transform.localScale=new Vector3(1.65f,0.85f,1.65f);
            Destroy(root.GetComponent<Collider>());
            Material material=CreateEliminationMaterial(
                $"Defense Seal {i+1} Material",colors[i],colors[i]*2.3f);
            Renderer renderer=root.GetComponent<Renderer>();
            renderer.sharedMaterial=material;
            defenseSeals.Add(new DefenseSeal
            {
                root=root,renderer=renderer,material=material,position=position,
                baseColor=colors[i],
                rewardName=rewards[rewardPool[i]],hp=tripleSealDefenseMap.sealMaxHealth,
                maxHp=tripleSealDefenseMap.sealMaxHealth
            });
        }
    }

    private void CreateGatePart(Transform parent,Vector3 localPosition,Vector3 localScale)
    {
        GameObject part=GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.transform.SetParent(parent,false);
        part.transform.localPosition=localPosition;
        part.transform.localScale=localScale;
        Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial=CreateEliminationMaterial(
            "Exit Gate",new Color(0.08f,0.65f,0.34f),new Color(0.02f,1f,0.42f)*2f);
    }

    private Material CreateEliminationMaterial(string materialName,Color color,Color emission)
    {
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)shader=Shader.Find("Standard");
        Material material=new Material(shader){name=materialName,color=color};
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor",emission);
        eliminationMaterials.Add(material);
        return material;
    }

    private void CreatePlayer()
    {
        player = new GameObject("Player").transform;

        GameObject shadow=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadow.name="Player Shadow";
        shadow.transform.SetParent(player,false);
        shadow.transform.localPosition=new Vector3(0f,0.02f,0f);
        shadow.transform.localScale=new Vector3(0.72f,0.025f,0.5f);
        Destroy(shadow.GetComponent<Collider>());
        Material shadowMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        shadowMat.color=new Color(0.05f,0.07f,0.1f);
        shadow.GetComponent<Renderer>().material=shadowMat;

        GameObject visualPrefab=Resources.Load<GameObject>("Player");
        if(visualPrefab==null)throw new InvalidOperationException("Player prefab was not found in Resources.");
        GameObject visualInstance=Instantiate(visualPrefab,player);
        visualInstance.name="Voxel Player Visual";
        playerVisual=visualInstance.transform;
        playerVisual.localPosition=new Vector3(0f,0.03f,0f);
        playerVisual.localScale=visualPrefab.transform.localScale*4.5f;
        playerVisual.localRotation=Quaternion.identity;
        playerAnimator=visualInstance.GetComponent<Animator>();
        if(playerAnimator!=null)
        {
            playerAnimator.applyRootMotion=false;
            // Root_Parent is the lower-body boundary. Start aim twisting at
            // Spine1_M so the waist and the entire torso belong to the upper body.
            playerUpperBody=playerAnimator.GetBoneTransform(HumanBodyBones.Spine);
            if(playerUpperBody==null)playerUpperBody=playerAnimator.GetBoneTransform(HumanBodyBones.Chest);
        }
        visualInstance.AddComponent<BrickSurvivorAnimationEvents>().Initialize(this);
        playerDamageReaction=visualInstance.AddComponent<VoxelPlayerDamageReaction>();
        playerDamageReaction.Initialize();
        playerRagdoll=visualInstance.AddComponent<VoxelPlayerRagdoll>();
        playerRagdoll.Initialize();

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.16f,0.9f,1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor",new Color(0.02f,0.3f,0.45f));

        playerArrow = new GameObject("Aim Arrow").transform;
        playerArrow.SetParent(player,false);
        GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arrow.transform.SetParent(playerArrow,false);
        arrow.transform.localPosition = new Vector3(0f,0.08f,0.9f);
        arrow.transform.localScale = new Vector3(0.14f,0.06f,1.2f);
        Destroy(arrow.GetComponent<Collider>());
        arrow.GetComponent<Renderer>().material = mat;

        dashTrail=player.gameObject.AddComponent<TrailRenderer>();
        dashTrail.time=0.18f;
        dashTrail.startWidth=0.72f;
        dashTrail.endWidth=0f;
        dashTrail.material=new Material(Shader.Find("Sprites/Default"));
        dashTrail.startColor=new Color(0.2f,0.95f,1f,0.75f);
        dashTrail.endColor=new Color(0.2f,0.65f,1f,0f);
        dashTrail.emitting=false;
    }

    private void CreateAttackVisual()
    {
        GameObject root = new GameObject("Fan Attack Visual");
        attackVisual = root.transform;
        MeshFilter filter = root.AddComponent<MeshFilter>();
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        const int segments = 24;
        Vector3[] vertices = new Vector3[segments+2];
        int[] triangles = new int[segments*3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-AttackHalfAngle,AttackHalfAngle,i/(float)segments)*Mathf.Deg2Rad;
            vertices[i+1] = new Vector3(Mathf.Sin(angle)*AttackRange,0f,Mathf.Cos(angle)*AttackRange);
            if (i == segments) continue;
            triangles[i*3] = 0;
            triangles[i*3+1] = i+1;
            triangles[i*3+2] = i+2;
        }
        Mesh mesh = new Mesh{name="Fan Attack Mesh"};
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        filter.sharedMesh = mesh;
        attackMaterial = new Material(Shader.Find("Sprites/Default"));
        attackMaterial.color = new Color(0.15f,0.9f,1f,0.25f);
        renderer.material = attackMaterial;
        attackVisual.gameObject.SetActive(false);
    }

    private void CreateSkillSignals()
    {
        Color[] colors={new Color(0.25f,0.85f,1f),new Color(1f,0.25f,0.72f)};
        for(int i=0;i<2;i++)
        {
            GameObject signal=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            signal.name=i==0?"Left Skill Signal":"Right Skill Signal";
            Destroy(signal.GetComponent<Collider>());
            signal.transform.localScale=new Vector3(0.5f,0.025f,0.5f);
            skillSignalMaterials[i]=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            skillSignalMaterials[i].color=colors[i];
            skillSignalMaterials[i].EnableKeyword("_EMISSION");
            skillSignalMaterials[i].SetColor("_EmissionColor",colors[i]*1.3f);
            signal.GetComponent<Renderer>().material=skillSignalMaterials[i];
            signal.SetActive(false);
            skillSignals[i]=signal.transform;
        }
    }

    private void CreateCrackVisuals()
    {
        const int segments=32;
        Vector3[] vertices=new Vector3[(segments+1)*2];
        int[] triangles=new int[segments*6];
        for(int i=0;i<=segments;i++)
        {
            float angle=i/(float)segments*Mathf.PI*2f;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            vertices[i*2]=direction*0.78f;
            vertices[i*2+1]=direction;
            if(i==segments)continue;
            int vertex=i*2,triangle=i*6;
            triangles[triangle]=vertex;
            triangles[triangle+1]=vertex+3;
            triangles[triangle+2]=vertex+1;
            triangles[triangle+3]=vertex;
            triangles[triangle+4]=vertex+2;
            triangles[triangle+5]=vertex+3;
        }
        crackRingMesh=new Mesh{name="Crack Ring Mesh"};
        crackRingMesh.vertices=vertices;
        crackRingMesh.triangles=triangles;
        crackRingMesh.RecalculateNormals();
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)shader=Shader.Find("Standard");
        crackMaterial=new Material(shader){color=new Color(0.78f,0.16f,1f),enableInstancing=true};
        crackMaterial.EnableKeyword("_EMISSION");
        crackMaterial.SetColor("_EmissionColor",new Color(0.65f,0.04f,1f)*2.1f);
    }

    private void CreateImpactParticles()
    {
        GameObject root=new GameObject("Voxel Impact Particles");
        root.transform.SetParent(transform,false);
        impactParticles=root.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main=impactParticles.main;
        main.playOnAwake=false;
        main.loop=false;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.maxParticles=320;
        main.startLifetime=0.22f;
        main.startSize=0.08f;
        main.gravityModifier=1.15f;
        ParticleSystem.EmissionModule emission=impactParticles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=impactParticles.shape;
        shape.enabled=false;
        ParticleSystemRenderer renderer=root.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Mesh;
        renderer.mesh=cubeMesh;
        Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if(shader==null)shader=Shader.Find("Sprites/Default");
        renderer.material=new Material(shader){color=Color.white};
    }

    private void ResetRun()
    {
        for(int i=0;i<enemies.Count;i++)ReleaseEnemy(enemies[i]);
        enemies.Clear();
        for(int i=0;i<collapsingEnemyVisuals.Count;i++)
        {
            GameObject visual=collapsingEnemyVisuals[i];
            if(visual==null)continue;
            VoxelEnemyReaction reaction=visual.GetComponent<VoxelEnemyReaction>();
            if(reaction!=null)reaction.PrepareForReuse();
            visual.SetActive(false);
            ReturnModelVisual(visual);
        }
        collapsingEnemyVisuals.Clear();
        fragments.Clear();
        phaseBricks.Clear();
        crackPulses.Clear();
        crackBlasts.Clear();
        crackChainQueue.Clear();
        EndHitStop();
        Time.timeScale=1f;
        timeScaleBeforeHitStop=1f;
        player.position = Vector3.zero;
        if(playerRagdoll!=null)playerRagdoll.ResetRagdoll();
        playerHp = PlayerMaxHealth;
        playerDefeatRaised = false;
        eliminationMapComplete=false;
        eliminationClearTime=0f;
        stressMode=false;
        if(eliminationReward!=null)eliminationReward.SetActive(false);
        if(eliminationExit!=null)eliminationExit.SetActive(false);
        if(eliminationEntrySeal!=null)eliminationEntrySeal.SetActive(true);
        for(int i=0;i<sealRewardDrops.Count;i++)if(sealRewardDrops[i]!=null)Destroy(sealRewardDrops[i]);
        sealRewardDrops.Clear();
        playerMoveSpeedMultiplier=1f;
        playerDamageMultiplier=1f;
        playerAttackSpeedMultiplier=1f;
        playerItemRarityBonus=0f;
        sealRewardMessage=string.Empty;
        sealRewardMessageTime=0f;
        defenseAlert=string.Empty;
        defenseAlertTime=0f;
        score = kills = 0;
        elapsed = spawnTimer = attackTimer = attackAnimationRemaining = 0f;
        actionState=ActionState.Normal;
        actionTimer=inputBufferTimer=dashCooldownRemaining=dashElapsed=dashSlowRemaining=0f;
        activeSkill=bufferedSkill=-1;
        activeCrackTarget=bufferedCrackTarget=null;
        crackOrderSequence=0;
        movementAmount=0f;
        movementDirection=Vector3.zero;
        playerKnockbackVelocity=Vector3.zero;
        movingBackward=false;
        if(playerAnimator!=null)
        {
            playerAnimator.ResetTrigger(DashParameter);
            playerAnimator.ResetTrigger(AttackParameter);
            playerAnimator.SetFloat(LocomotionParameter,0f);
            if(playerAnimator.layerCount>1)playerAnimator.SetLayerWeight(1,1f);
        }
        skillCooldownRemaining[0]=skillCooldownRemaining[1]=0f;
        skillSignalLife[0]=skillSignalLife[1]=0f;
        lastSkillHits[0]=lastSkillHits[1]=0;
        if(dashTrail!=null){dashTrail.Clear();dashTrail.emitting=false;}
        for(int i=0;i<skillSignals.Length;i++)if(skillSignals[i]!=null)skillSignals[i].gameObject.SetActive(false);
        if(mapMode==PrototypeMapMode.ScaleTrial)ResetScaleTrial();
        else if(mapMode==PrototypeMapMode.SealExcavation)ResetSealExcavation();
        else if(mapMode==PrototypeMapMode.TripleSealDefense)ResetTripleSealDefense();
        else if(mapMode==PrototypeMapMode.TimedSurvival)ResetTimedSurvival();
        else SpawnEliminationGroups();
    }

    private void ResetTimedSurvival()
    {
        timedSurvivalState=TimedSurvivalState.Fighting;
        timedSurvivalRemaining=timedSurvivalMap.combatDuration;
        timedSurvivalSpawnCooldown=timedSurvivalMap.initialSpawnDelay;
        timedSurvivalBatch=0;
        timedSurvivalTotalSpawned=0;
    }

    private void UpdateTimedSurvival(float dt)
    {
        if(timedSurvivalState==TimedSurvivalState.Fighting)
        {
            timedSurvivalRemaining=Mathf.Max(0f,timedSurvivalRemaining-dt);
            timedSurvivalSpawnCooldown-=dt;
            if(timedSurvivalRemaining<=0f)
            {
                timedSurvivalState=timedSurvivalMap.clearRemainingEnemies
                    ?TimedSurvivalState.Cleanup:TimedSurvivalState.Completed;
                if(timedSurvivalState==TimedSurvivalState.Completed)CompleteTimedSurvival();
            }
            else if(timedSurvivalSpawnCooldown<=0f)
            {
                SpawnTimedSurvivalBatch();
                float progress=1f-timedSurvivalRemaining/Mathf.Max(0.01f,timedSurvivalMap.combatDuration);
                timedSurvivalSpawnCooldown=Mathf.Lerp(
                    timedSurvivalMap.startingSpawnInterval,timedSurvivalMap.endingSpawnInterval,progress);
            }
        }

        if(timedSurvivalState==TimedSurvivalState.Cleanup&&CountAliveEnemies()==0)
            CompleteTimedSurvival();
    }

    private void SpawnTimedSurvivalBatch()
    {
        int alive=CountAliveEnemies();
        int capacity=Mathf.Max(0,timedSurvivalMap.maximumAliveEnemies-alive);
        if(capacity<=0)return;

        float progress=1f-timedSurvivalRemaining/Mathf.Max(0.01f,timedSurvivalMap.combatDuration);
        int requested=timedSurvivalMap.startingBatchSize+
            Mathf.FloorToInt(timedSurvivalMap.additionalBatchSize*progress);
        int count=Mathf.Min(capacity,Mathf.Max(1,requested));
        int group=7000+timedSurvivalBatch++;
        float centerAngle=UnityEngine.Random.Range(0f,Mathf.PI*2f);
        for(int i=0;i<count;i++)
        {
            float angle=centerAngle+UnityEngine.Random.Range(-0.28f,0.28f);
            float radius=timedSurvivalMap.spawnDistance+
                UnityEngine.Random.Range(-timedSurvivalMap.spawnDistanceJitter,timedSurvivalMap.spawnDistanceJitter);
            Vector3 position=new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
            position=ResolveArenaPosition(position,1.1f);
            SpawnEnemyAt(position,group,true);
            Enemy spawned=enemies[enemies.Count-1];
            spawned.maxHp*=Mathf.Lerp(1f,timedSurvivalMap.endingHealthMultiplier,progress);
            spawned.hp=spawned.maxHp;
            spawned.speed*=Mathf.Lerp(1f,timedSurvivalMap.endingMoveSpeedMultiplier,progress);
            spawned.damageMultiplier*=Mathf.Lerp(1f,timedSurvivalMap.endingDamageMultiplier,progress);
            timedSurvivalTotalSpawned++;
        }
    }

    private int CountAliveEnemies()
    {
        int alive=0;
        for(int i=0;i<enemies.Count;i++)if(!enemies[i].dead)alive++;
        return alive;
    }

    private void CompleteTimedSurvival()
    {
        if(timedSurvivalState==TimedSurvivalState.Completed&&eliminationMapComplete)return;
        timedSurvivalState=TimedSurvivalState.Completed;
        eliminationMapComplete=true;
        eliminationClearTime=elapsed;
        score+=timedSurvivalMap.completionScore;
        if(eliminationReward!=null)
        {
            eliminationReward.transform.position=player.position+aimDirection*2.2f+Vector3.up*0.65f;
            eliminationReward.SetActive(true);
        }
        if(eliminationExit!=null)eliminationExit.SetActive(true);
        if(eliminationEntrySeal!=null)eliminationEntrySeal.SetActive(false);
        Debug.Log($"[Timed Survival] Cleared {timedSurvivalMap.combatDuration:0}s. Kills {kills}, spawned {timedSurvivalTotalSpawned}.");
    }

    private void SpawnEnemy(float radius=-1f,float angle=float.NaN,int forcedArchetypeIndex=-1)
    {
        if(enemies.Count>=MaxEnemyRenderSlots)return;
        if (radius < 0f) radius = UnityEngine.Random.Range(11f,16f);
        if (float.IsNaN(angle)) angle = UnityEngine.Random.Range(0f,Mathf.PI*2f);
        SpawnEnemyAt(player.position+new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius),-1,true,forcedArchetypeIndex);
    }

    private void SpawnEnemyAt(Vector3 spawnPosition,int encounterGroup,bool encounterActivated,int forcedArchetypeIndex=-1)
    {
        if(enemies.Count>=MaxEnemyRenderSlots)return;
        int archetypeIndex=forcedArchetypeIndex>=0&&forcedArchetypeIndex<enemyArchetypes.Length
            ?forcedArchetypeIndex
            :SelectEnemyArchetype();
        EnemyArchetype archetype=enemyArchetypes[archetypeIndex];
        Enemy e=AcquireEnemy();
        e.encounterGroup=encounterGroup;
        e.encounterActivated=encounterActivated;
        e.archetypeIndex=archetypeIndex;
        e.position=spawnPosition;
        e.speed=archetype.moveSpeed;
        e.attackSpeed=archetype.attackSpeed;
        e.damageMultiplier=1f;
        e.defenseRole=DefenseEnemyRole.None;
        e.defenseSealTarget=-1;
        e.defenseSealDamageMultiplier=1f;
        e.attackCooldownRemaining=0f;
        e.scale=archetype.visualScale;
        e.collisionRadius=archetype.CollisionRadius;
        e.visualHeight=archetype.VisualHeight;
        e.hp=e.maxHp=archetype.maxHp;
        e.defense=CombatDefense.None;
        e.phase=archetype.moveClip!=null?UnityEngine.Random.Range(0,archetype.moveClip.frames.Length):UnityEngine.Random.Range(0f,10f);
        e.animationTime=0f;
        e.actionState=EnemyActionState.Moving;
        e.actionTimer=0f;
        e.specialCooldownRemaining=archetype.specialType==EnemySpecialType.None?0f:UnityEngine.Random.Range(1.1f,2.4f);
        e.rushDirection=Vector3.zero;
        float spawnFacingAngle=encounterGroup>=0?UnityEngine.Random.Range(0f,Mathf.PI*2f):Mathf.Atan2(player.position.z-e.position.z,player.position.x-e.position.x);
        e.facingDirection=new Vector3(Mathf.Cos(spawnFacingAngle),0f,Mathf.Sin(spawnFacingAngle));
        e.idleAnimationSpeed=UnityEngine.Random.Range(0.88f,1.13f);
        e.turnSpeedDegrees=UnityEngine.Random.Range(150f,210f);
        e.animationStartPhase=UnityEngine.Random.value;
        e.lastImpactDirection=e.facingDirection;
        e.flipX=UnityEngine.Random.value>0.5f;
        if(archetype.modelPrefab!=null)
        {
            e.alive=e.initialBricks=1;
            e.modelVisual=AcquireModelVisual(archetypeIndex,archetype);
            e.proceduralMotion=e.modelVisual.GetComponent<VoxelTyrannosaurusProceduralMotion>();
            e.modelAnimator=e.modelVisual.GetComponentInChildren<Animator>(true);
            e.reaction=e.modelVisual.GetComponent<VoxelEnemyReaction>();
            if(e.reaction==null)e.reaction=e.modelVisual.AddComponent<VoxelEnemyReaction>();
            // Pooled visuals are inactive. Animator.Update is only valid after the visual
            // has re-entered the active hierarchy.
            e.modelVisual.SetActive(true);
            e.reaction.PrepareForReuse();
            if(e.modelAnimator!=null)
            {
                e.modelAnimator.enabled=!UseProceduralTyrannosaurusMotion;
                if(e.modelAnimator.enabled)
                {
                    e.modelAnimator.Rebind();
                    e.modelAnimator.SetBool(EnemyMovingParameter,encounterActivated);
                    e.modelAnimator.Play(encounterActivated?"Move":"Idle",0,e.animationStartPhase);
                    e.modelAnimator.speed=encounterActivated?1f:e.idleAnimationSpeed;
                    e.modelAnimator.Update(0f);
                }
            }
            SyncModelEnemyVisual(e,0f);
        }
        else
        {
            for(int i=0;i<archetype.occupiedIndices.Length;i++)
            {
                int index=archetype.occupiedIndices[i];
                e.visible[index]=true;
                e.color[index]=archetype.unionColors[index];
                e.alive++;
            }
            e.initialBricks=e.alive;
            UploadDamageMask(e);
        }
        enemies.Add(e);
    }

    private void SpawnEliminationGroups()
    {
        int groupCount=eliminationMap.RollGroupCount();
        currentEliminationGroupCount=groupCount;
        EnsureEliminationGroupMarkers(groupCount);
        List<Vector3> groupCenters=new List<Vector3>(groupCount);
        List<Vector3> spawnCandidates=BuildEliminationSpawnCandidates();
        currentSpawnCandidateCount=spawnCandidates.Count;
        for(int group=0;group<groupCount;group++)
        {
            Vector3 center=SelectEliminationSpawnCandidate(spawnCandidates,groupCenters,group,groupCount);
            groupCenters.Add(center);
            PositionEliminationGroupMarker(group,center);
            int memberCount=eliminationMap.RollEnemiesPerGroup();
            float formationRotation=UnityEngine.Random.Range(0f,Mathf.PI*2f);
            const float goldenAngle=2.399963f;
            for(int member=0;member<memberCount;member++)
            {
                float memberAngle=formationRotation+member*goldenAngle+UnityEngine.Random.Range(-0.22f,0.22f);
                float memberRadius=member==0?0f:eliminationMap.groupSpread*(0.52f+member*0.17f)*UnityEngine.Random.Range(0.85f,1.18f);
                Vector3 offset=new Vector3(Mathf.Cos(memberAngle)*memberRadius,0f,Mathf.Sin(memberAngle)*memberRadius);
                SpawnEnemyAt(center+offset,group,false);
            }
        }
        UpdateEliminationGroupMarkers();
    }

    private void ResetScaleTrial()
    {
        scaleTrialStage=1;
        scaleTrialState=ScaleTrialState.Fighting;
        scaleChoiceDelay=0f;
        scaleChallengeHold=0f;
        scaleWaitForMouseRelease=false;
        SpawnScaleTrialWave();
    }

    private void SpawnScaleTrialWave()
    {
        scaleTrialState=ScaleTrialState.Fighting;
        scaleChallengeHold=0f;
        int count=scaleTrialMap.GetEnemyCount(scaleTrialStage);
        float healthMultiplier=scaleTrialMap.GetHealthMultiplier(scaleTrialStage);
        float speedMultiplier=scaleTrialMap.GetSpeedMultiplier(scaleTrialStage);
        int group=3000+scaleTrialStage;
        float rotation=UnityEngine.Random.Range(0f,Mathf.PI*2f);
        for(int i=0;i<count;i++)
        {
            float angle=rotation+i*Mathf.PI*2f/count+UnityEngine.Random.Range(-0.15f,0.15f);
            float radius=UnityEngine.Random.Range(12.5f,17.5f);
            Vector3 position=new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
            SpawnEnemyAt(position,group,true);
            Enemy spawned=enemies[enemies.Count-1];
            spawned.maxHp*=healthMultiplier;
            spawned.hp=spawned.maxHp;
            spawned.speed*=speedMultiplier;
            spawned.attackSpeed*=Mathf.Lerp(1f,speedMultiplier,0.65f);
        }
        Debug.Log($"[Scale Trial] Stage {scaleTrialStage}/{scaleTrialMap.stageCount} started with {count} enemies.");
    }

    private void UpdateScaleTrial(float dt)
    {
        if(scaleTrialState==ScaleTrialState.Fighting)
        {
            if(HasAliveEnemyInEncounter(3000+scaleTrialStage))return;
            if(scaleTrialStage>=scaleTrialMap.stageCount)
            {
                CompleteScaleTrial();
                return;
            }
            scaleTrialState=ScaleTrialState.Choosing;
            scaleChoiceDelay=scaleTrialMap.choiceRevealDelay;
            scaleChallengeHold=0f;
            scaleWaitForMouseRelease=true;
            Debug.Log($"[Scale Trial] Stage {scaleTrialStage} cleared. Settle or challenge the next stage.");
            return;
        }
        if(scaleTrialState!=ScaleTrialState.Choosing)return;
        scaleChoiceDelay=Mathf.Max(0f,scaleChoiceDelay-dt);
        if(scaleWaitForMouseRelease)
        {
            if(!Input.GetMouseButton(0))scaleWaitForMouseRelease=false;
            return;
        }
        if(scaleChoiceDelay>0f)return;

        GetScaleChoiceRects(out Rect settleRect,out Rect challengeRect);
        Vector2 mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
        if(settleRect.Contains(mouse)&&Input.GetMouseButtonDown(0))
        {
            ApplyScaleChoice(false);
            return;
        }
        if(challengeRect.Contains(mouse)&&Input.GetMouseButton(0))
        {
            scaleChallengeHold+=dt;
            if(scaleChallengeHold>=scaleTrialMap.challengeHoldDuration)ApplyScaleChoice(true);
        }
        else scaleChallengeHold=0f;
    }

    private bool HasAliveEnemyInEncounter(int encounterGroup)
    {
        for(int i=0;i<enemies.Count;i++)
            if(!enemies[i].dead&&enemies[i].encounterGroup==encounterGroup)return true;
        return false;
    }

    private void ApplyScaleChoice(bool challenge)
    {
        scaleChallengeHold=0f;
        if(challenge)
        {
            Debug.Log("[Scale Trial] Challenge selected. Risking all secured room reward.");
            scaleTrialStage++;
            SpawnScaleTrialWave();
        }
        else
        {
            Debug.Log("[Scale Trial] Settle selected. Securing current reward.");
            CompleteScaleTrial();
        }
    }

    private static void GetScaleChoiceRects(out Rect settleRect,out Rect challengeRect)
    {
        float width=Mathf.Min(300f,(Screen.width-100f)*0.5f);
        float gap=24f;
        float total=width*2f+gap;
        float x=(Screen.width-total)*0.5f;
        float y=Screen.height*0.5f-45f;
        settleRect=new Rect(x,y,width,165f);
        challengeRect=new Rect(x+width+gap,y,width,165f);
    }

    private void CompleteScaleTrial()
    {
        scaleTrialState=ScaleTrialState.Completed;
        eliminationMapComplete=true;
        eliminationClearTime=elapsed;
        int rewardScore=Mathf.RoundToInt(1500f*scaleTrialMap.GetRewardMultiplier(scaleTrialStage));
        score+=rewardScore;
        if(eliminationReward!=null)
        {
            eliminationReward.transform.position=Vector3.up*0.65f;
            eliminationReward.SetActive(true);
        }
        if(eliminationExit!=null)eliminationExit.SetActive(true);
        if(eliminationEntrySeal!=null)eliminationEntrySeal.SetActive(false);
        Debug.Log($"[Scale Trial] Settled at stage {scaleTrialStage}. Reward score {rewardScore} secured.");
    }

    private void ResetSealExcavation()
    {
        sealExcavationState=SealExcavationState.Planning;
        selectedSealCount=0;
        activeSealOrder=0;
        sealEnemyHealthMultiplier=1f;
        sealEnemyDamageMultiplier=1f;
        sealEnemyMoveMultiplier=1f;
        sealEnemyAttackSpeedMultiplier=1f;
        sealExtraEnemies=0;
        RollSealContents();
        if(eliminationEntrySeal!=null)eliminationEntrySeal.SetActive(true);
    }

    private void RollSealContents()
    {
        List<int> shuffled=new List<int>(sealStones.Count);
        for(int i=0;i<sealStones.Count;i++)shuffled.Add(i);
        for(int i=shuffled.Count-1;i>0;i--)
        {
            int swap=UnityEngine.Random.Range(0,i+1);
            int temp=shuffled[i];shuffled[i]=shuffled[swap];shuffled[swap]=temp;
        }

        int blessed=0;
        for(int i=0;i<sealStones.Count;i++)if(UnityEngine.Random.value<sealExcavationMap.blessingChance)blessed++;
        blessed=Mathf.Clamp(blessed,sealExcavationMap.minimumBlessedSeals,
            Mathf.Min(sealExcavationMap.maximumBlessedSeals,sealStones.Count));

        for(int i=0;i<sealStones.Count;i++)
        {
            SealStone stone=sealStones[i];
            stone.risk=(SealRiskType)UnityEngine.Random.Range(0,5);
            stone.blessing=SealBlessingType.None;
            stone.selectionOrder=0;
            stone.cleared=false;
            stone.root.SetActive(true);
        }
        for(int i=0;i<blessed;i++)sealStones[shuffled[i]].blessing=RollSealBlessing();
        RefreshSealStoneMaterials();
    }

    private static SealBlessingType RollSealBlessing()
    {
        float roll=UnityEngine.Random.value;
        if(roll<0.34f)return SealBlessingType.MovementSpeed;
        if(roll<0.62f)return SealBlessingType.AttackDamage;
        if(roll<0.88f)return SealBlessingType.AttackSpeed;
        return SealBlessingType.ItemRarity;
    }

    private void UpdateSealPlanningInput()
    {
        if(sealExcavationState!=SealExcavationState.Planning||!Input.GetMouseButtonDown(0))return;
        Vector2 guiMouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
        if(GetSealConfirmRect().Contains(guiMouse))return;
        Ray ray=cam.ScreenPointToRay(Input.mousePosition);
        if(!Physics.Raycast(ray,out RaycastHit hit,200f))return;
        for(int i=0;i<sealStones.Count;i++)
        {
            SealStone stone=sealStones[i];
            if(hit.collider.gameObject!=stone.root)continue;
            ToggleSealSelection(stone);
            return;
        }
    }

    private void ToggleSealSelection(SealStone stone)
    {
        if(stone.selectionOrder>0)
        {
            int removedOrder=stone.selectionOrder;
            stone.selectionOrder=0;
            selectedSealCount--;
            for(int i=0;i<sealStones.Count;i++)
                if(sealStones[i].selectionOrder>removedOrder)sealStones[i].selectionOrder--;
        }
        else
        {
            int maximum=Mathf.Min(sealExcavationMap.maximumSelectedSeals,sealStones.Count);
            if(selectedSealCount>=maximum)return;
            stone.selectionOrder=++selectedSealCount;
        }
        RefreshSealStoneMaterials();
    }

    private void RefreshSealStoneMaterials()
    {
        for(int i=0;i<sealStones.Count;i++)
        {
            SealStone stone=sealStones[i];
            if(stone.cleared)continue;
            bool selected=stone.selectionOrder>0;
            Color baseColor=selected?new Color(0.92f,0.29f,0.05f):new Color(0.18f,0.08f,0.28f);
            Color emission=selected?new Color(1f,0.12f,0.015f)*2.4f:new Color(0.35f,0.04f,0.7f)*1.8f;
            stone.material.color=baseColor;
            stone.material.SetColor("_EmissionColor",emission);
        }
    }

    private void BeginSealExcavation()
    {
        if(sealExcavationState!=SealExcavationState.Planning||selectedSealCount<=0)return;
        for(int i=0;i<sealStones.Count;i++)
            if(sealStones[i].selectionOrder<=0)sealStones[i].root.SetActive(false);
        sealExcavationState=SealExcavationState.Fighting;
        activeSealOrder=1;
        SpawnActiveSealWave();
    }

    private SealStone GetSealByOrder(int order)
    {
        for(int i=0;i<sealStones.Count;i++)if(sealStones[i].selectionOrder==order)return sealStones[i];
        return null;
    }

    private void SpawnActiveSealWave()
    {
        SealStone stone=GetSealByOrder(activeSealOrder);
        if(stone==null){CompleteSealExcavation();return;}
        ApplySealRisk(stone.risk);
        stone.material.color=new Color(0.95f,0.04f,0.03f);
        stone.material.SetColor("_EmissionColor",new Color(1f,0.015f,0.01f)*3.4f);

        int count=Mathf.Max(1,sealExcavationMap.baseEnemiesPerSeal+
            (activeSealOrder-1)*sealExcavationMap.enemiesAddedPerOrder+sealExtraEnemies);
        int group=5000+activeSealOrder;
        float rotation=UnityEngine.Random.Range(0f,Mathf.PI*2f);
        for(int i=0;i<count;i++)
        {
            float angle=rotation+i*Mathf.PI*2f/count+UnityEngine.Random.Range(-0.18f,0.18f);
            float radius=sealExcavationMap.spawnRadius*UnityEngine.Random.Range(0.78f,1.12f);
            Vector3 position=stone.position+new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
            position=ResolveArenaPosition(position,1.1f);
            SpawnEnemyAt(position,group,true);
            Enemy spawned=enemies[enemies.Count-1];
            spawned.maxHp*=sealEnemyHealthMultiplier;
            spawned.hp=spawned.maxHp;
            spawned.speed*=sealEnemyMoveMultiplier;
            spawned.attackSpeed*=sealEnemyAttackSpeedMultiplier;
            spawned.damageMultiplier=sealEnemyDamageMultiplier;
        }
        Debug.Log($"[Seal Excavation] Seal {activeSealOrder}/{selectedSealCount} activated with {count} enemies.");
    }

    private void ApplySealRisk(SealRiskType risk)
    {
        switch(risk)
        {
            case SealRiskType.Vitality: sealEnemyHealthMultiplier+=sealExcavationMap.enemyHealthIncrease; break;
            case SealRiskType.Ferocity: sealEnemyDamageMultiplier+=sealExcavationMap.enemyDamageIncrease; break;
            case SealRiskType.Movement: sealEnemyMoveMultiplier+=sealExcavationMap.enemyMoveSpeedIncrease; break;
            case SealRiskType.AttackSpeed: sealEnemyAttackSpeedMultiplier+=sealExcavationMap.enemyAttackSpeedIncrease; break;
            case SealRiskType.Swarm: sealExtraEnemies+=sealExcavationMap.swarmEnemyIncrease; break;
        }
    }

    private void UpdateSealExcavation(float dt)
    {
        sealRewardMessageTime=Mathf.Max(0f,sealRewardMessageTime-dt);
        if(sealExcavationState!=SealExcavationState.Fighting)return;
        if(HasAliveEnemyInEncounter(5000+activeSealOrder))return;
        SealStone cleared=GetSealByOrder(activeSealOrder);
        if(cleared!=null)BreakSealAndDropReward(cleared);
        if(activeSealOrder>=selectedSealCount){CompleteSealExcavation();return;}
        activeSealOrder++;
        SpawnActiveSealWave();
    }

    private void BreakSealAndDropReward(SealStone stone)
    {
        stone.cleared=true;
        ApplySealBlessing(stone.blessing);
        float riskMultiplier=1f+(activeSealOrder-1)*0.32f;
        float rewardMultiplier=riskMultiplier*(1f+playerItemRarityBonus);
        int reward=Mathf.RoundToInt(sealExcavationMap.baseRewardScore*rewardMultiplier);
        score+=reward;
        SpawnSealRewardBurst(stone.position);
        stone.root.SetActive(false);
        sealRewardMessage=$"SEAL {activeSealOrder} BROKEN   REWARD {reward:N0}"+
            (stone.blessing==SealBlessingType.None?string.Empty:$"   BLESSING: {GetSealBlessingLabel(stone.blessing)}");
        sealRewardMessageTime=2.2f;
        Debug.Log($"[Seal Excavation] {sealRewardMessage}");
    }

    private void ApplySealBlessing(SealBlessingType blessing)
    {
        switch(blessing)
        {
            case SealBlessingType.MovementSpeed: playerMoveSpeedMultiplier+=sealExcavationMap.playerMoveSpeedIncrease; break;
            case SealBlessingType.AttackDamage: playerDamageMultiplier+=sealExcavationMap.playerDamageIncrease; break;
            case SealBlessingType.AttackSpeed: playerAttackSpeedMultiplier+=sealExcavationMap.playerAttackSpeedIncrease; break;
            case SealBlessingType.ItemRarity: playerItemRarityBonus+=sealExcavationMap.itemRarityIncrease; break;
        }
    }

    private void SpawnSealRewardBurst(Vector3 position)
    {
        if(impactParticles!=null)
        {
            for(int i=0;i<22;i++)
            {
                Vector2 circle=UnityEngine.Random.insideUnitCircle.normalized;
                ParticleSystem.EmitParams emit=new ParticleSystem.EmitParams
                {
                    position=position+Vector3.up*0.7f,
                    velocity=new Vector3(circle.x*UnityEngine.Random.Range(2.5f,6f),UnityEngine.Random.Range(4f,8f),circle.y*UnityEngine.Random.Range(2.5f,6f)),
                    startColor=Color.Lerp(new Color(1f,0.45f,0.02f),new Color(1f,0.95f,0.35f),UnityEngine.Random.value),
                    startLifetime=UnityEngine.Random.Range(0.5f,0.9f),
                    startSize=UnityEngine.Random.Range(0.16f,0.3f)
                };
                impactParticles.Emit(emit,1);
            }
        }
        for(int i=0;i<3;i++)
        {
            GameObject drop=GameObject.CreatePrimitive(PrimitiveType.Cube);
            drop.name="Seal Reward Drop";
            drop.transform.SetParent(transform,false);
            drop.transform.position=position+new Vector3((i-1)*0.55f,0.35f,UnityEngine.Random.Range(-0.35f,0.35f));
            drop.transform.localScale=Vector3.one*0.38f;
            Destroy(drop.GetComponent<Collider>());
            drop.GetComponent<Renderer>().sharedMaterial=CreateEliminationMaterial(
                "Seal Reward",new Color(1f,0.55f,0.04f),new Color(1f,0.32f,0.01f)*2.8f);
            sealRewardDrops.Add(drop);
        }
    }

    private void CompleteSealExcavation()
    {
        sealExcavationState=SealExcavationState.Completed;
        eliminationMapComplete=true;
        eliminationClearTime=elapsed;
        if(eliminationExit!=null)eliminationExit.SetActive(true);
        if(eliminationEntrySeal!=null)eliminationEntrySeal.SetActive(false);
        Debug.Log($"[Seal Excavation] Cleared {selectedSealCount} selected seals.");
    }

    private void ResetTripleSealDefense()
    {
        tripleSealDefenseState=TripleSealDefenseState.Preparing;
        defenseWave=0;
        defenseWaveDelay=0f;
        defenseFocusedSeal=-1;
        for(int i=0;i<defenseSeals.Count;i++)
        {
            DefenseSeal seal=defenseSeals[i];
            seal.hp=seal.maxHp=tripleSealDefenseMap.sealMaxHealth;
            seal.root.SetActive(true);
            seal.material.color=seal.baseColor;
            seal.material.SetColor("_EmissionColor",seal.baseColor*2.3f);
        }
    }

    private void BeginTripleSealDefense()
    {
        if(tripleSealDefenseState!=TripleSealDefenseState.Preparing)return;
        tripleSealDefenseState=TripleSealDefenseState.Fighting;
        defenseWave=0;
        defenseWaveDelay=tripleSealDefenseMap.firstWaveDelay;
        defenseAlert="DEFENSE STARTED";
        defenseAlertTime=1.5f;
    }

    private void UpdateTripleSealDefense(float dt)
    {
        defenseAlertTime=Mathf.Max(0f,defenseAlertTime-dt);
        if(tripleSealDefenseState!=TripleSealDefenseState.Fighting)return;
        RefreshDefenseSealVisuals();
        if(!HasAliveDefenseSeal())
        {
            tripleSealDefenseState=TripleSealDefenseState.Failed;
            defenseAlert="ALL SEALS DESTROYED";
            defenseAlertTime=999f;
            return;
        }
        int encounterGroup=6000+defenseWave;
        if(defenseWave>0&&HasAliveEnemyInEncounter(encounterGroup))return;
        if(defenseWave>=tripleSealDefenseMap.waveCount)
        {
            CompleteTripleSealDefense();
            return;
        }
        defenseWaveDelay-=dt;
        if(defenseWaveDelay>0f)return;
        defenseWave++;
        SpawnDefenseWave();
    }

    private void SpawnDefenseWave()
    {
        int index=Mathf.Clamp(defenseWave-1,0,tripleSealDefenseMap.enemiesPerWave.Length-1);
        int count=Mathf.Max(1,tripleSealDefenseMap.enemiesPerWave[index]);
        int group=6000+defenseWave;
        if(defenseWave==3)defenseFocusedSeal=RollAliveDefenseSeal();
        else defenseFocusedSeal=-1;

        bool commanderPresent=defenseWave>=3;
        for(int i=0;i<count;i++)
        {
            DefenseEnemyRole role=DefenseEnemyRole.Raider;
            if(defenseWave>=3&&i<2)role=DefenseEnemyRole.Siege;
            else if(defenseWave>=3&&i==2)role=DefenseEnemyRole.Commander;
            else if(defenseWave>=2&&UnityEngine.Random.value<tripleSealDefenseMap.hunterRatio)role=DefenseEnemyRole.Hunter;

            int targetSeal=defenseFocusedSeal>=0&&role!=DefenseEnemyRole.Hunter
                ?defenseFocusedSeal:RollWaveSealTarget(i);
            Vector3 laneDirection=defenseSeals[targetSeal].position.normalized;
            Vector3 tangent=new Vector3(-laneDirection.z,0f,laneDirection.x);
            Vector3 position=laneDirection*tripleSealDefenseMap.enemySpawnDistance+
                tangent*UnityEngine.Random.Range(-tripleSealDefenseMap.laneSpread,tripleSealDefenseMap.laneSpread);
            position=ResolveArenaPosition(position,1.1f);
            SpawnEnemyAt(position,group,true);
            Enemy spawned=enemies[enemies.Count-1];
            spawned.defenseRole=role;
            spawned.defenseSealTarget=targetSeal;
            if(commanderPresent&&role!=DefenseEnemyRole.Commander)
                spawned.speed*=tripleSealDefenseMap.commanderWaveSpeedMultiplier;
            if(role==DefenseEnemyRole.Siege)
            {
                spawned.maxHp*=tripleSealDefenseMap.siegeHealthMultiplier;
                spawned.hp=spawned.maxHp;
                spawned.speed*=tripleSealDefenseMap.siegeMoveMultiplier;
                spawned.defenseSealDamageMultiplier=tripleSealDefenseMap.siegeSealDamageMultiplier;
                spawned.scale*=1.3f;
                spawned.collisionRadius*=1.18f;
                if(spawned.modelVisual!=null)spawned.modelVisual.transform.localScale*=1.3f;
            }
            else if(role==DefenseEnemyRole.Commander)
            {
                spawned.maxHp*=tripleSealDefenseMap.commanderHealthMultiplier;
                spawned.hp=spawned.maxHp;
                spawned.scale*=1.16f;
                if(spawned.modelVisual!=null)spawned.modelVisual.transform.localScale*=1.16f;
            }
        }
        defenseAlert=defenseFocusedSeal>=0?$"WAVE {defenseWave}: FOCUSED ASSAULT":$"WAVE {defenseWave} INCOMING";
        defenseAlertTime=1.6f;
        defenseWaveDelay=tripleSealDefenseMap.nextWaveDelay;
        Debug.Log($"[Triple Seal Defense] Wave {defenseWave}/{tripleSealDefenseMap.waveCount}, enemies {count}.");
    }

    private int RollWaveSealTarget(int enemyIndex)
    {
        if(defenseWave==1)return enemyIndex%defenseSeals.Count;
        if(defenseWave==2)return (enemyIndex%2==0?0:1+UnityEngine.Random.Range(0,2))%defenseSeals.Count;
        return RollAliveDefenseSeal();
    }

    private int RollAliveDefenseSeal()
    {
        List<int> alive=new List<int>(3);
        for(int i=0;i<defenseSeals.Count;i++)if(!defenseSeals[i].Destroyed)alive.Add(i);
        return alive.Count>0?alive[UnityEngine.Random.Range(0,alive.Count)]:0;
    }

    private bool HasAliveDefenseSeal()
    {
        for(int i=0;i<defenseSeals.Count;i++)if(!defenseSeals[i].Destroyed)return true;
        return false;
    }

    private int GetNearestAliveDefenseSeal(Vector3 position)
    {
        int best=-1;
        float bestDistance=float.MaxValue;
        for(int i=0;i<defenseSeals.Count;i++)
        {
            if(defenseSeals[i].Destroyed)continue;
            float distance=(defenseSeals[i].position-position).sqrMagnitude;
            if(distance>=bestDistance)continue;
            bestDistance=distance;best=i;
        }
        return best;
    }

    private void DamageDefenseSeal(int index,float damage)
    {
        if(index<0||index>=defenseSeals.Count)return;
        DefenseSeal seal=defenseSeals[index];
        if(seal.Destroyed)return;
        seal.hp=Mathf.Max(0f,seal.hp-damage);
        seal.material.color=Color.Lerp(seal.baseColor,Color.white,0.65f);
        if(!seal.Destroyed)return;
        SpawnSealRewardBurst(seal.position);
        seal.root.SetActive(false);
        defenseAlert=$"{seal.rewardName} SEAL LOST";
        defenseAlertTime=2.2f;
        for(int i=0;i<enemies.Count;i++)
            if(!enemies[i].dead&&enemies[i].defenseSealTarget==index)
                enemies[i].defenseSealTarget=GetNearestAliveDefenseSeal(enemies[i].position);
    }

    private void RefreshDefenseSealVisuals()
    {
        for(int i=0;i<defenseSeals.Count;i++)
        {
            DefenseSeal seal=defenseSeals[i];
            if(seal.Destroyed)continue;
            float hp01=seal.hp/Mathf.Max(1f,seal.maxHp);
            seal.material.color=Color.Lerp(new Color(0.18f,0.015f,0.01f),seal.baseColor,hp01);
        }
    }

    private void CompleteTripleSealDefense()
    {
        tripleSealDefenseState=TripleSealDefenseState.Completed;
        eliminationMapComplete=true;
        eliminationClearTime=elapsed;
        int surviving=0,totalReward=0;
        for(int i=0;i<defenseSeals.Count;i++)
        {
            DefenseSeal seal=defenseSeals[i];
            if(seal.Destroyed)continue;
            surviving++;
            float hp01=seal.hp/Mathf.Max(1f,seal.maxHp);
            float quality=hp01>=tripleSealDefenseMap.highHealthThreshold?1.2f:
                hp01<tripleSealDefenseMap.damagedThreshold?0.75f:1f;
            int reward=Mathf.RoundToInt(tripleSealDefenseMap.rewardScorePerSurvivingSeal*quality);
            totalReward+=reward;
            SpawnSealRewardBurst(seal.position);
        }
        if(surviving==defenseSeals.Count)totalReward=Mathf.RoundToInt(totalReward*tripleSealDefenseMap.perfectDefenseMultiplier);
        score+=totalReward;
        if(eliminationExit!=null)eliminationExit.SetActive(true);
        if(eliminationEntrySeal!=null)eliminationEntrySeal.SetActive(false);
        defenseAlert=$"DEFENSE COMPLETE   {surviving}/3 SEALS   REWARD {totalReward:N0}";
        defenseAlertTime=999f;
    }

    private List<Vector3> BuildEliminationSpawnCandidates()
    {
        int candidatesPerRing=Mathf.Max(4,activeMap.candidatesPerRing);
        int ringCount=Mathf.Max(1,activeMap.candidateRingCount);
        List<Vector3> candidates=new List<Vector3>(candidatesPerRing*ringCount);
        float minimumRadius=Mathf.Max(4f,activeMap.minimumGroupDistance);
        float maximumRadius=Mathf.Max(minimumRadius+1f,Mathf.Min(ArenaRadius-5f,activeMap.maximumGroupDistance));
        float globalRotation=UnityEngine.Random.Range(0f,Mathf.PI*2f);
        for(int ring=0;ring<ringCount;ring++)
        {
            float ring01=ringCount>1?ring/(float)(ringCount-1):0.5f;
            float baseRadius=Mathf.Lerp(minimumRadius,maximumRadius,ring01);
            float ringRotation=globalRotation+ring*0.19f;
            for(int point=0;point<candidatesPerRing;point++)
            {
                float angleJitter=activeMap.candidateAngleJitter*Mathf.Deg2Rad;
                float angle=ringRotation+point*Mathf.PI*2f/candidatesPerRing+UnityEngine.Random.Range(-angleJitter,angleJitter);
                float radius=baseRadius+UnityEngine.Random.Range(-activeMap.candidateRadialJitter,activeMap.candidateRadialJitter);
                Vector3 radial=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
                Vector3 tangent=new Vector3(-radial.z,0f,radial.x);
                Vector3 candidate=radial*radius+tangent*UnityEngine.Random.Range(-activeMap.candidateTangentJitter,activeMap.candidateTangentJitter);
                if(candidate.magnitude>ArenaRadius-4.5f)continue;
                if(IsNearArenaObstacle(candidate,activeMap.groupSpread+2.5f))continue;
                candidates.Add(candidate);
            }
        }
        for(int i=candidates.Count-1;i>0;i--)
        {
            int swapIndex=UnityEngine.Random.Range(0,i+1);
            Vector3 temp=candidates[i];candidates[i]=candidates[swapIndex];candidates[swapIndex]=temp;
        }
        return candidates;
    }

    private Vector3 SelectEliminationSpawnCandidate(
        List<Vector3> candidates,List<Vector3> existingCenters,int groupIndex,int groupCount)
    {
        float minimumSeparation=Mathf.Max(2f,activeMap.minimumGroupSeparation);
        while(candidates.Count>0)
        {
            int last=candidates.Count-1;
            Vector3 candidate=candidates[last];
            candidates.RemoveAt(last);
            bool overlaps=false;
            for(int i=0;i<existingCenters.Count;i++)
            {
                if((existingCenters[i]-candidate).sqrMagnitude>=minimumSeparation*minimumSeparation)continue;
                overlaps=true;
                break;
            }
            if(!overlaps)return candidate;
        }
        return RollIrregularGroupCenter(existingCenters,groupIndex,groupCount);
    }

    private Vector3 RollIrregularGroupCenter(List<Vector3> existingCenters,int groupIndex,int groupCount)
    {
        float minimumRadius=Mathf.Max(4f,activeMap.minimumGroupDistance);
        float maximumRadius=Mathf.Max(minimumRadius+1f,Mathf.Min(ArenaRadius-5f,activeMap.maximumGroupDistance));
        float minimumSeparation=Mathf.Max(2f,activeMap.minimumGroupSeparation);
        for(int attempt=0;attempt<32;attempt++)
        {
            float angle=UnityEngine.Random.Range(0f,Mathf.PI*2f);
            // Square-root sampling avoids concentrating every group near the center.
            float radius=Mathf.Sqrt(UnityEngine.Random.Range(minimumRadius*minimumRadius,maximumRadius*maximumRadius));
            Vector3 candidate=new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
            bool overlaps=false;
            for(int i=0;i<existingCenters.Count;i++)
            {
                if((existingCenters[i]-candidate).sqrMagnitude>=minimumSeparation*minimumSeparation)continue;
                overlaps=true;
                break;
            }
            if(!overlaps&&!IsNearArenaObstacle(candidate,activeMap.groupSpread+2.5f))return candidate;
        }

        // Extremely crowded settings still receive a safe fallback position.
        float fallbackAngle=UnityEngine.Random.Range(0f,Mathf.PI*2f)+groupIndex*Mathf.PI*2f/Mathf.Max(1,groupCount);
        float fallbackRadius=UnityEngine.Random.Range(minimumRadius,maximumRadius);
        return new Vector3(Mathf.Cos(fallbackAngle)*fallbackRadius,0f,Mathf.Sin(fallbackAngle)*fallbackRadius);
    }

    private bool IsNearArenaObstacle(Vector3 position,float clearance)
    {
        for(int i=0;i<arenaObstacles.Count;i++)
        {
            ArenaObstacle obstacle=arenaObstacles[i];
            float minimumDistance=obstacle.radius+clearance;
            Vector3 offset=position-obstacle.center;offset.y=0f;
            if(offset.sqrMagnitude<minimumDistance*minimumDistance)return true;
        }
        return false;
    }

    private void EnsureEliminationGroupMarkers(int count)
    {
        while(eliminationGroupMarkers.Count<count)
        {
            int index=eliminationGroupMarkers.Count;
            GameObject marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name=$"Enemy Group Marker {index+1}";
            marker.transform.SetParent(eliminationMarkerRoot,false);
            marker.transform.localScale=new Vector3(3.1f,0.025f,3.1f);
            Destroy(marker.GetComponent<Collider>());
            Renderer renderer=marker.GetComponent<Renderer>();
            renderer.sharedMaterial=CreateEliminationMaterial($"Group Marker {index+1}",new Color(0.06f,0.22f,0.48f),new Color(0.02f,0.2f,0.8f));
            eliminationGroupMarkers.Add(renderer);
        }
        for(int i=0;i<eliminationGroupMarkers.Count;i++)
            eliminationGroupMarkers[i].gameObject.SetActive(i<count);
    }

    private void PositionEliminationGroupMarker(int group,Vector3 center)
    {
        if(group<0||group>=eliminationGroupMarkers.Count)return;
        eliminationGroupMarkers[group].transform.position=center+Vector3.up*0.025f;
    }

    private void ActivateEnemyGroup(int group)
    {
        if(group<0)return;
        bool changed=false;
        for(int i=0;i<enemies.Count;i++)
        {
            Enemy member=enemies[i];
            if(member.dead||member.encounterGroup!=group||member.encounterActivated)continue;
            member.encounterActivated=true;
            if(member.modelAnimator!=null)member.modelAnimator.SetBool(EnemyMovingParameter,true);
            changed=true;
        }
        if(changed)UpdateEliminationGroupMarkers();
    }

    private void UpdateEliminationGroupMarkers()
    {
        for(int group=0;group<eliminationGroupMarkers.Count;group++)
        {
            Renderer marker=eliminationGroupMarkers[group];
            if(!marker.gameObject.activeSelf)continue;
            bool alive=false,active=false;
            for(int i=0;i<enemies.Count;i++)
            {
                Enemy enemy=enemies[i];
                if(enemy.dead||enemy.encounterGroup!=group)continue;
                alive=true;active|=enemy.encounterActivated;
            }
            Color color=!alive?new Color(0.08f,0.55f,0.23f):active?new Color(0.82f,0.16f,0.04f):new Color(0.05f,0.22f,0.62f);
            marker.sharedMaterial.color=color;
            marker.sharedMaterial.SetColor("_EmissionColor",color*(active?1.8f:1.15f));
            marker.gameObject.SetActive(alive);
        }
    }

    private GameObject AcquireModelVisual(int archetypeIndex,EnemyArchetype archetype)
    {
        if(modelVisualPools.TryGetValue(archetypeIndex,out Stack<GameObject> pool)&&pool.Count>0)
        {
            GameObject pooled=pool.Pop();
            if(modelBaseScales.TryGetValue(pooled,out Vector3 baseScale))pooled.transform.localScale=baseScale;
            return pooled;
        }
        GameObject visual=Instantiate(archetype.modelPrefab,enemyVisualRoot);
        visual.name=archetype.id+" Enemy";
        modelVisualArchetypes[visual]=archetypeIndex;
        Animator importedAnimator=visual.GetComponentInChildren<Animator>(true);
        if(importedAnimator!=null)importedAnimator.enabled=!UseProceduralTyrannosaurusMotion;
        float groundOffset=NormalizeModelHeight(visual,archetype.modelVisualHeight);
        modelBaseScales[visual]=visual.transform.localScale;
        VoxelTyrannosaurusProceduralMotion motion=visual.GetComponent<VoxelTyrannosaurusProceduralMotion>();
        if(motion==null)motion=visual.AddComponent<VoxelTyrannosaurusProceduralMotion>();
        motion.Initialize();
        motion.SetGroundOffset(groundOffset);
        motion.enabled=UseProceduralTyrannosaurusMotion;
        VoxelEnemyReaction reaction=visual.GetComponent<VoxelEnemyReaction>();
        if(reaction==null)reaction=visual.AddComponent<VoxelEnemyReaction>();
        reaction.Initialize();
        if(archetype.id.IndexOf("Velociraptor",StringComparison.OrdinalIgnoreCase)>=0)
        {
            RaptorTurnAnticipation turnAnticipation=visual.GetComponent<RaptorTurnAnticipation>();
            if(turnAnticipation==null)turnAnticipation=visual.AddComponent<RaptorTurnAnticipation>();
            turnAnticipation.Initialize();
        }
        return visual;
    }

    private void ReturnModelVisual(GameObject visual)
    {
        if(visual==null)return;
        visual.SetActive(false);
        if(!modelVisualArchetypes.TryGetValue(visual,out int archetypeIndex))
        {
            Destroy(visual);
            return;
        }
        if(!modelVisualPools.TryGetValue(archetypeIndex,out Stack<GameObject> pool))
        {
            pool=new Stack<GameObject>(64);
            modelVisualPools.Add(archetypeIndex,pool);
        }
        if(pool.Count<128)pool.Push(visual);
        else
        {
            modelVisualArchetypes.Remove(visual);
            modelBaseScales.Remove(visual);
            Destroy(visual);
        }
    }

    private static float NormalizeModelHeight(GameObject visual,float targetHeight)
    {
        Renderer[] renderers=visual.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length==0)return 0f;
        Bounds bounds=renderers[0].bounds;
        for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
        if(bounds.size.y<=0.0001f)return 0f;
        visual.transform.localScale*=targetHeight/bounds.size.y;
        bounds=renderers[0].bounds;
        for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
        return visual.transform.position.y-bounds.min.y+0.03f;
    }

    private int SelectEnemyArchetype()
    {
        float totalWeight=0f;
        for(int i=0;i<enemyArchetypes.Length;i++)totalWeight+=Mathf.Max(0f,enemyArchetypes[i].spawnWeight);
        float roll=UnityEngine.Random.value*totalWeight;
        for(int i=0;i<enemyArchetypes.Length;i++)
        {
            roll-=Mathf.Max(0f,enemyArchetypes[i].spawnWeight);
            if(roll<=0f)return i;
        }
        return enemyArchetypes.Length-1;
    }

    private Enemy AcquireEnemy()
    {
        Enemy e=enemyPool.Count>0?enemyPool.Pop():new Enemy();
        if(e.damageMaskLayer<0)
        {
            if(allocatedDamageMaskLayers>=MaxEnemyRenderSlots)throw new InvalidOperationException("Enemy damage mask slots exhausted.");
            e.damageMaskLayer=allocatedDamageMaskLayers++;
        }
        Array.Clear(e.visible,0,e.visible.Length);
        Array.Clear(e.color,0,e.color.Length);
        e.position=Vector3.zero;
        e.encounterGroup=-1;
        e.encounterActivated=true;
        e.archetypeIndex=0;
        e.speed=e.attackSpeed=e.attackCooldownRemaining=e.scale=e.collisionRadius=e.visualHeight=e.hp=e.maxHp=e.phase=0f;
        e.damageMultiplier=1f;
        e.defenseRole=DefenseEnemyRole.None;
        e.defenseSealTarget=-1;
        e.defenseSealDamageMultiplier=1f;
        e.animationTime=e.actionTimer=e.specialCooldownRemaining=0f;
        e.defense=CombatDefense.None;
        e.knockbackVelocity=Vector3.zero;
        e.rushDirection=Vector3.zero;
        e.facingDirection=Vector3.forward;
        e.idleAnimationSpeed=1f;
        e.turnSpeedDegrees=180f;
        e.animationStartPhase=0f;
        e.actionState=EnemyActionState.Moving;
        e.flipX=e.dead=false;
        e.alive=e.initialBricks=0;
        e.crackStacks=0;
        e.crackOrder=0;
        e.modelVisual=null;
        e.proceduralMotion=null;
        e.modelAnimator=null;
        e.reaction=null;
        e.lastImpactDirection=Vector3.forward;
        return e;
    }

    private void UploadDamageMask(Enemy e)
    {
        if(e==null||e.damageMaskLayer<0||voxelDamageMasks==null)return;
        for(int i=0;i<damageMaskUploadBuffer.Length;i++)damageMaskUploadBuffer[i]=e.visible[i]?(byte)255:(byte)0;
        voxelDamageMasks.SetPixelData(damageMaskUploadBuffer,0,e.damageMaskLayer,0);
        damageMasksNeedApply=true;
    }

    private void FlushDamageMasks()
    {
        if(!damageMasksNeedApply||voxelDamageMasks==null)return;
        voxelDamageMasks.Apply(false,false);
        damageMasksNeedApply=false;
    }

    private void ReleaseEnemy(Enemy e)
    {
        if(e==null)return;
        if(e.modelVisual!=null)
        {
            ReturnModelVisual(e.modelVisual);
            e.modelVisual=null;
            e.proceduralMotion=null;
            e.modelAnimator=null;
            e.reaction=null;
        }
        e.dead=false;
        e.alive=0;
        if(enemyPool.Count<256)enemyPool.Push(e);
    }

    private int NearestPalette(Color32 c)
    {
        int best=0, bestDistance=int.MaxValue;
        for (int i=0;i<Palette.Length;i++)
        {
            int dr=c.r-Palette[i].r,dg=c.g-Palette[i].g,db=c.b-Palette[i].b;
            int d=dr*dr+dg*dg+db*db;
            if (d>=bestDistance) continue;
            bestDistance=d; best=i;
        }
        return best;
    }

    private void Update()
    {
        fps=Mathf.Lerp(fps,1f/Mathf.Max(0.0001f,Time.unscaledDeltaTime),0.05f);
        if(hitStopActive)
        {
            hitStopRemaining-=Time.unscaledDeltaTime;
            if(hitStopRemaining>0f)return;
            EndHitStop();
        }
        float dt=Time.deltaTime;
        elapsed+=dt;
        attackTimer-=dt;
        attackAnimationRemaining=Mathf.Max(0f,attackAnimationRemaining-dt);
        spawnTimer-=dt;
        dashCooldownRemaining=Mathf.Max(0f,dashCooldownRemaining-dt);
        dashSlowRemaining=Mathf.Max(0f,dashSlowRemaining-dt);
        skillCooldownRemaining[0]=Mathf.Max(0f,skillCooldownRemaining[0]-dt);
        skillCooldownRemaining[1]=Mathf.Max(0f,skillCooldownRemaining[1]-dt);
        attackVisualLife-=dt;
        attackVisual.gameObject.SetActive(attackVisualLife>0f);
        if (Input.GetKeyDown(KeyCode.R)) ResetRun();
        if (Input.GetKeyDown(KeyCode.F1)) for (int i=0;i<20;i++) SpawnEnemy();
        if (Input.GetKeyDown(KeyCode.F2)) stressMode=!stressMode;
        if (Input.GetKeyDown(KeyCode.F3)) useVoxelMeshRendering=!useVoxelMeshRendering;
        bool scaleInputLocked=mapMode==PrototypeMapMode.ScaleTrial&&scaleTrialState==ScaleTrialState.Choosing;
        bool sealPlanning=mapMode==PrototypeMapMode.SealExcavation&&sealExcavationState==SealExcavationState.Planning;
        bool defensePreparing=mapMode==PrototypeMapMode.TripleSealDefense&&
            tripleSealDefenseState==TripleSealDefenseState.Preparing;
        if(mapMode==PrototypeMapMode.SealExcavation)UpdateSealPlanningInput();
        if(!scaleInputLocked)UpdateAim();
        UpdateSkillSignals(dt);
        if (playerHp>0)
        {
            if(scaleInputLocked)
            {
                actionState=ActionState.Normal;
                activeSkill=bufferedSkill=-1;
                activeCrackTarget=bufferedCrackTarget=null;
                movementAmount=0f;
                movementDirection=Vector3.zero;
                playerKnockbackVelocity=Vector3.zero;
                if(dashTrail!=null)dashTrail.emitting=false;
                UpdatePlayerVisual();
            }
            else if(sealPlanning||defensePreparing)
            {
                // Seal selection is performed in-world. Keep traversal and mouse-facing
                // available while suppressing combat commands until the route is confirmed.
                actionState=ActionState.Normal;
                activeSkill=bufferedSkill=-1;
                activeCrackTarget=bufferedCrackTarget=null;
                playerKnockbackVelocity=Vector3.zero;
                if(dashTrail!=null)dashTrail.emitting=false;
                UpdateMovement(dt);
                UpdatePlayerVisual();
            }
            else
            {
                if(actionState!=ActionState.HitStunned)CaptureCommands();
                UpdatePlayerAction(dt);
                UpdatePlayerKnockback(dt);
                UpdatePlayerVisual();
                UpdateEnemies(dt);
            }
            if(mapMode==PrototypeMapMode.ScaleTrial)UpdateScaleTrial(dt);
            else if(mapMode==PrototypeMapMode.SealExcavation)UpdateSealExcavation(dt);
            else if(mapMode==PrototypeMapMode.TripleSealDefense)UpdateTripleSealDefense(dt);
            else if(mapMode==PrototypeMapMode.TimedSurvival)UpdateTimedSurvival(dt);
            else CheckEliminationMapCompletion();
            if(stressMode&&spawnTimer<=0f){spawnTimer=0.1f;SpawnEnemy();}
        }
        else
        {
            actionState=ActionState.Dead;
            if(mapMode==PrototypeMapMode.ScaleTrial&&scaleTrialState!=ScaleTrialState.Completed)
            {
                scaleTrialState=ScaleTrialState.Failed;
            }
            if(mapMode==PrototypeMapMode.SealExcavation&&sealExcavationState!=SealExcavationState.Completed)
                sealExcavationState=SealExcavationState.Failed;
            if(mapMode==PrototypeMapMode.TripleSealDefense&&tripleSealDefenseState!=TripleSealDefenseState.Completed)
                tripleSealDefenseState=TripleSealDefenseState.Failed;
            if(mapMode==PrototypeMapMode.TimedSurvival&&timedSurvivalState!=TimedSurvivalState.Completed)
                timedSurvivalState=TimedSurvivalState.Failed;
            RaisePlayerDefeated();
        }
        UpdateFragments(dt);
        UpdatePhaseBricks(dt);
        UpdateCrackPulses(dt);
        UpdateCollapsingVisualPool();
        UpdateCamera(dt);
    }

    private void UpdateMovement(float dt)
    {
        Vector3 input=new Vector3(Input.GetAxisRaw("Horizontal"),0f,Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude>1f) input.Normalize();
        movementAmount=Mathf.Clamp01(input.magnitude);
        movementDirection=movementAmount>0.01f?input.normalized:Vector3.zero;
        float slowProgress=DashSlowDuration>0f?dashSlowRemaining/DashSlowDuration:0f;
        float moveMultiplier=Mathf.Lerp(1f,DashSlowMultiplier,Mathf.Clamp01(slowProgress));
        Vector3 desiredPosition=player.position+input*(PlayerSpeed*playerMoveSpeedMultiplier*moveMultiplier)*dt;
        player.position=ResolveArenaPosition(desiredPosition,0.5f);
    }

    private void UpdatePlayerVisual()
    {
        bool dashing=actionState==ActionState.Dashing;
        Vector3 desiredLowerFacing=aimDirection;
        float locomotion=0f;

        if(dashing)
        {
            desiredLowerFacing=dashEnd-dashStart;
            desiredLowerFacing.y=0f;
        }
        else if(movementAmount>0.01f&&movementDirection.sqrMagnitude>0.001f)
        {
            float aimFromMovement=Mathf.Abs(Vector3.SignedAngle(movementDirection,aimDirection,Vector3.up));
            if(!movingBackward&&aimFromMovement>100f)movingBackward=true;
            else if(movingBackward&&aimFromMovement<80f)movingBackward=false;
            desiredLowerFacing=movingBackward?-movementDirection:movementDirection;
            locomotion=movingBackward?-1f:1f;
        }
        else movingBackward=false;

        if(desiredLowerFacing.sqrMagnitude>0.001f)
        {
            desiredLowerFacing.Normalize();
            float turnSpeed=dashing?30f:16f;
            playerVisual.rotation=Quaternion.Slerp(playerVisual.rotation,Quaternion.LookRotation(desiredLowerFacing,Vector3.up),1f-Mathf.Exp(-turnSpeed*Time.deltaTime));
        }

        if(playerAnimator!=null)
        {
            playerAnimator.SetFloat(LocomotionParameter,locomotion,0.065f,Time.deltaTime);
            bool stationaryFullBodyAttack=attackAnimationRemaining>0f&&movementAmount<=0.01f;
            if(playerAnimator.layerCount>1)playerAnimator.SetLayerWeight(1,dashing||stationaryFullBodyAttack?0f:1f);
        }
    }

    private void CaptureCommands()
    {
        if(Input.GetMouseButtonDown(0))
        {
            Enemy target=FindCrackTargetUnderCursor();
            if(target!=null)QueueSkill(0,target);
        }
        if(Input.GetMouseButtonDown(1)&&HasDetonatableCracks())QueueSkill(1,null);
        if(inputBufferTimer>0f&&actionState!=ActionState.Dashing)
        {
            inputBufferTimer-=Time.deltaTime;
            if(inputBufferTimer<=0f)
            {
                bufferedSkill=-1;
                bufferedCrackTarget=null;
            }
        }
    }

    private void QueueSkill(int slot,Enemy crackTarget)
    {
        if(skillCooldownRemaining[slot]>InputBufferDuration)return;
        if(bufferedSkill<0)
        {
            bufferedSkill=slot;
            bufferedCrackTarget=slot==0?crackTarget:null;
            inputBufferTimer=InputBufferDuration;
        }
    }

    private void UpdatePlayerAction(float dt)
    {
        movementAmount=0f;
        movementDirection=Vector3.zero;
        if(actionState==ActionState.HitStunned)
        {
            actionTimer-=dt;
            if(actionTimer<=0f)actionState=ActionState.Normal;
            return;
        }
        if(Input.GetKeyDown(KeyCode.Space)&&dashCooldownRemaining<=0f)
        {
            StartDash();
        }
        if(actionState==ActionState.Dashing)
        {
            UpdateDash(dt);
            return;
        }
        if(actionState==ActionState.Casting)
        {
            actionTimer-=dt;
            if(actionTimer<=0f)
            {
                TriggerSkillSignal(activeSkill);
                actionState=ActionState.Recovery;
                actionTimer=skillRecoveryTime[activeSkill];
            }
            return;
        }
        if(actionState==ActionState.Recovery)
        {
            UpdateMovement(dt);
            actionTimer-=dt;
            if(actionTimer>0f)return;
            actionState=ActionState.Normal;
            activeSkill=-1;
        }
        if(actionState!=ActionState.Normal)return;
        UpdateMovement(dt);
        if(bufferedSkill>=0&&skillCooldownRemaining[bufferedSkill]<=0f)
        {
            StartSkill(bufferedSkill);
            return;
        }
        if(attackTimer<=0f)TryStartBasicAttack();
    }

    private void StartSkill(int slot)
    {
        if(slot==0&&(bufferedCrackTarget==null||bufferedCrackTarget.dead||!IsInCrackMarkRange(bufferedCrackTarget)))
        {
            bufferedSkill=-1;
            bufferedCrackTarget=null;
            inputBufferTimer=0f;
            return;
        }
        if(slot==1&&!HasDetonatableCracks())
        {
            bufferedSkill=-1;
            inputBufferTimer=0f;
            return;
        }
        activeCrackTarget=slot==0?bufferedCrackTarget:null;
        bufferedSkill=-1;
        bufferedCrackTarget=null;
        inputBufferTimer=0f;
        activeSkill=slot;
        skillCooldownRemaining[slot]=skillCooldown[slot];
        actionState=ActionState.Casting;
        actionTimer=skillCastTime[slot];
    }

    private void TriggerSkillSignal(int slot)
    {
        if(slot==0)
        {
            Enemy target=activeCrackTarget;
            activeCrackTarget=null;
            if(target==null||target.dead||!IsInCrackMarkRange(target))
            {
                skillSignalLife[slot]=0f;
                lastSkillHits[slot]=0;
                return;
            }
            ApplyCrack(target,1);
            skillSignalLife[slot]=0f;
            skillSignals[slot].gameObject.SetActive(false);
            lastSkillHits[slot]=target.crackStacks;
            return;
        }

        skillSignalLife[slot]=0.24f;
        skillSignals[slot].gameObject.SetActive(true);
        skillSignalOrigin[slot]=player.position+Vector3.up*0.04f;
        skillSignals[slot].position=skillSignalOrigin[slot];
        lastSkillHits[slot]=DetonateCracks();
    }

    private void UpdateSkillSignals(float dt)
    {
        for(int i=0;i<skillSignals.Length;i++)
        {
            if(skillSignalLife[i]<=0f)continue;
            skillSignalLife[i]-=dt;
            if(skillSignalLife[i]<=0f){skillSignals[i].gameObject.SetActive(false);continue;}
            float t=1f-skillSignalLife[i]/0.24f;
            if(i==1)skillSignalOrigin[i]=player.position+Vector3.up*0.04f;
            skillSignals[i].position=skillSignalOrigin[i];
            float size=i==0
                ?Mathf.Lerp(0.65f,2.2f,Mathf.SmoothStep(0f,1f,t))
                :Mathf.Lerp(2.25f,CrackDetonationRange*2f,Mathf.SmoothStep(0f,1f,t));
            skillSignals[i].localScale=new Vector3(size,0.025f,size);
        }
    }

    private Enemy FindCrackTargetUnderCursor()
    {
        Enemy best=null;
        float bestScore=float.MaxValue;
        Vector2 mouse=Input.mousePosition;
        for(int i=0;i<enemies.Count;i++)
        {
            Enemy e=enemies[i];
            if(!IsInCrackMarkRange(e))continue;
            float height=e.visualHeight;
            Vector3 center=e.position+Vector3.up*(height*0.42f);
            Vector3 screen=cam.WorldToScreenPoint(center);
            if(screen.z<=0f)continue;
            Vector3 top=cam.WorldToScreenPoint(center+Vector3.up*(height*0.5f));
            float radiusPixels=Mathf.Max(24f,Mathf.Abs(top.y-screen.y)*1.15f);
            float score=Vector2.Distance(mouse,new Vector2(screen.x,screen.y))/radiusPixels;
            if(score>1.15f||score>=bestScore)continue;
            best=e;
            bestScore=score;
        }
        return best;
    }

    private bool IsInCrackMarkRange(Enemy e)
    {
        if(e==null||e.dead)return false;
        Vector3 offset=e.position-player.position;offset.y=0f;
        return offset.magnitude<=CrackMarkRange+e.collisionRadius;
    }

    private bool HasDetonatableCracks()
    {
        for(int i=0;i<enemies.Count;i++)
        {
            Enemy e=enemies[i];
            if(e.dead||e.crackStacks<=0)continue;
            Vector3 offset=e.position-player.position;offset.y=0f;
            if(offset.magnitude<=CrackDetonationRange+e.collisionRadius)return true;
        }
        return false;
    }

    private bool ApplyCrack(Enemy target,int amount)
    {
        if(target==null||target.dead||amount<=0)return false;
        if(target.crackStacks<=0&&CountCrackedEnemies()>=MaxCrackedEnemies)
        {
            Enemy oldest=null;
            for(int i=0;i<enemies.Count;i++)
            {
                Enemy candidate=enemies[i];
                if(candidate.dead||candidate.crackStacks<=0)continue;
                if(oldest==null||candidate.crackOrder<oldest.crackOrder)oldest=candidate;
            }
            if(oldest==null)return false;
            ClearCrack(oldest);
        }
        if(target.crackStacks<=0)target.crackOrder=++crackOrderSequence;
        target.crackStacks=Mathf.Clamp(target.crackStacks+amount,1,MaxCrackStacks);
        return true;
    }

    private int CountCrackedEnemies()
    {
        int count=0;
        for(int i=0;i<enemies.Count;i++)
            if(!enemies[i].dead&&enemies[i].crackStacks>0)count++;
        return count;
    }

    private static bool IsCrackTintBrick(Enemy e,int index)
    {
        uint seed=(uint)Mathf.FloorToInt(e.phase*1000f)+1u;
        uint hash=(uint)(index+1)*2654435761u^seed*2246822519u;
        hash^=hash>>15;
        hash*=3266489917u;
        hash^=hash>>16;
        return (hash&255u)<34u;
    }

    private static void ClearCrack(Enemy e)
    {
        e.crackStacks=0;
        e.crackOrder=0;
    }

    private void QueueCrackExplosion(Enemy source)
    {
        if(source==null||source.dead||source.crackStacks<=0||crackChainQueue.Count>=MaxCrackedEnemies)return;
        int stacks=source.crackStacks;
        ClearCrack(source);
        crackChainQueue.Add(new CrackRequest{source=source,stacks=stacks});
    }

    private int DetonateCracks()
    {
        crackBlasts.Clear();
        crackChainQueue.Clear();

        for(int i=0;i<enemies.Count;i++)
        {
            Enemy source=enemies[i];
            if(source.dead||source.crackStacks<=0)continue;
            Vector3 playerOffset=source.position-player.position;playerOffset.y=0f;
            if(playerOffset.magnitude>CrackDetonationRange+source.collisionRadius)continue;
            QueueCrackExplosion(source);
        }

        for(int queueIndex=0;queueIndex<crackChainQueue.Count;queueIndex++)
        {
            CrackRequest request=crackChainQueue[queueIndex];
            Enemy source=request.source;
            int stacks=request.stacks;
            Vector3 blastPosition=source.position;
            float radius=CrackBaseRadius+(stacks-1)*CrackRadiusPerExtraStack;
            float damage=AttackDamage*playerDamageMultiplier*(1f+(stacks-1)*0.75f);
            DamagePacket damagePacket=DamagePacket.Physical(
                damage,
                DamageSourceId.CrackExplosion,
                DamageTags.Attack|DamageTags.Area|DamageTags.Explosion|DamageTags.Crack|DamageTags.Knockback);
            crackBlasts.Add(new CrackBlast{source=source,position=blastPosition,radius=radius,damage=damagePacket,stacks=stacks});
            crackPulses.Add(new CrackPulse
            {
                position=blastPosition+Vector3.up*0.07f,
                radius=radius,
                age=0f,
                delay=(crackBlasts.Count-1)*0.035f,
                duration=0.22f
            });

            for(int i=0;i<enemies.Count;i++)
            {
                Enemy chained=enemies[i];
                if(chained.dead||chained.crackStacks<=0)continue;
                Vector3 offset=chained.position-blastPosition;offset.y=0f;
                if(offset.magnitude<=radius+chained.collisionRadius)QueueCrackExplosion(chained);
            }
        }

        for(int blastIndex=0;blastIndex<crackBlasts.Count;blastIndex++)
        {
            CrackBlast blast=crackBlasts[blastIndex];
            for(int i=0;i<enemies.Count;i++)
            {
                Enemy target=enemies[i];
                if(target.dead)continue;
                Vector3 offset=target.position-blast.position;offset.y=0f;
                if(offset.magnitude>blast.radius+target.collisionRadius)continue;
                Vector3 impactDirection=offset.sqrMagnitude>0.001f?offset.normalized:aimDirection;
                DamagePacket damage=target==blast.source?blast.damage:blast.damage.Scaled(0.5f);
                DamageEnemy(target,damage,impactDirection,5f+blast.stacks*0.7f);
            }
        }
        return crackBlasts.Count;
    }

    private void UpdateCrackPulses(float dt)
    {
        for(int i=crackPulses.Count-1;i>=0;i--)
        {
            CrackPulse pulse=crackPulses[i];
            pulse.age+=dt;
            if(pulse.age>=pulse.delay+pulse.duration){crackPulses.RemoveAt(i);continue;}
            crackPulses[i]=pulse;
        }
    }

    private void StartDash()
    {
        Vector3 input=new Vector3(Input.GetAxisRaw("Horizontal"),0f,Input.GetAxisRaw("Vertical"));
        Vector3 direction=input.sqrMagnitude>0.05f?input.normalized:aimDirection;
        dashStart=player.position;
        dashEnd=dashStart+direction*DashDistance;
        dashEnd=ClampMovementSegmentToObstacles(dashStart,dashEnd,0.5f);
        dashEnd=ResolveArenaPosition(dashEnd,0.5f);
        dashElapsed=0f;
        dashCooldownRemaining=DashCooldown;
        actionState=ActionState.Dashing;
        activeSkill=-1;
        attackAnimationRemaining=0f;
        movingBackward=false;
        if(playerAnimator!=null)playerAnimator.SetTrigger(DashParameter);
        if(dashTrail!=null){dashTrail.Clear();dashTrail.emitting=true;}
    }

    private void UpdateDash(float dt)
    {
        dashElapsed+=dt;
        float t=Mathf.Clamp01(dashElapsed/DashDuration);
        player.position=Vector3.Lerp(dashStart,dashEnd,Mathf.SmoothStep(0f,1f,t));
        if(t<1f)return;
        actionState=ActionState.Normal;
        dashSlowRemaining=DashSlowDuration;
        if(dashTrail!=null)dashTrail.emitting=false;
    }

    private void UpdateAim()
    {
        if(playerHp<=0||actionState==ActionState.Dead)return;
        Ray ray=cam.ScreenPointToRay(Input.mousePosition);
        Plane ground=new Plane(Vector3.up,Vector3.zero);
        if (ground.Raycast(ray,out float distance))
        {
            Vector3 direction=ray.GetPoint(distance)-player.position;
            direction.y=0f;
            if (direction.sqrMagnitude>0.05f) aimDirection=direction.normalized;
        }
        playerArrow.rotation=Quaternion.LookRotation(aimDirection,Vector3.up);
    }

    private void TryStartBasicAttack()
    {
        if(!HasBasicAttackTarget())return;
        attackTimer=AttackCooldown/Mathf.Max(0.01f,playerAttackSpeedMultiplier);
        attackAnimationRemaining=AttackCooldown/Mathf.Max(0.01f,playerAttackSpeedMultiplier);
        if(playerAnimator!=null)playerAnimator.SetTrigger(AttackParameter);
    }

    private bool HasBasicAttackTarget()
    {
        for(int i=0;i<enemies.Count;i++)
            if(IsInsideBasicAttackCone(enemies[i]))return true;
        return false;
    }

    private bool IsInsideBasicAttackCone(Enemy e)
    {
        if(e==null||e.dead)return false;
        Vector3 to=e.position-player.position;to.y=0f;
        float distance=to.magnitude;
        if(distance>AttackRange+e.collisionRadius||distance<0.01f)return false;
        return Vector3.Angle(aimDirection,to)<=AttackHalfAngle+Mathf.Clamp(e.collisionRadius*3f,1f,5f);
    }

    public void HandleBasicAttackHit()
    {
        if(playerHp<=0||actionState==ActionState.Dashing)return;
        attackVisual.position=player.position+Vector3.up*0.08f;
        attackVisual.rotation=Quaternion.LookRotation(aimDirection,Vector3.up);
        attackVisualLife=0.12f;
        for (int i=0;i<enemies.Count;i++)
        {
            Enemy e=enemies[i];
            if(!IsInsideBasicAttackCone(e))continue;
            DamagePacket damage=DamagePacket.Physical(
                AttackDamage*playerDamageMultiplier,
                DamageSourceId.BasicAttack,
                DamageTags.Attack|DamageTags.Melee|DamageTags.Basic);
            DamageEnemy(e,damage,aimDirection,4.5f);
        }
    }

    private void DamageEnemy(Enemy e,DamagePacket packet,Vector3 impactDirection,float knockbackSpeed)
    {
        if (e.dead) return;
        if(!e.encounterActivated)ActivateEnemyGroup(e.encounterGroup);
        DamageResult result=CombatDamageResolver.Resolve(packet,e.defense);
        float damage=result.total;
        if(damage<=0f)return;
        e.hp=Mathf.Max(0f,e.hp-damage);
        impactDirection.y=0f;
        if(impactDirection.sqrMagnitude>0.001f)e.lastImpactDirection=impactDirection.normalized;
        if (e.hp<=0f){ExplodeEnemy(e);return;}
        EnemyArchetype archetype=enemyArchetypes[e.archetypeIndex];
        if(archetype.modelPrefab!=null)
        {
            if(e.reaction!=null)e.reaction.PlayHit(e.lastImpactDirection);
            bool modelKnockbackAttack=(packet.tags&DamageTags.Knockback)!=0;
            if(modelKnockbackAttack&&impactDirection.sqrMagnitude>0.001f)
                e.knockbackVelocity+=impactDirection.normalized*knockbackSpeed;
            return;
        }
        // Erode slowly: the silhouette reaches 50% only once HP falls to 10%.
        // Below that point it stays intact until the killing blow releases everything.
        float hpRatio=e.hp/e.maxHp;
        float visibleRatio=0.5f+0.5f*Mathf.Clamp01((hpRatio-0.1f)/0.9f);
        int targetAlive=Mathf.CeilToInt(e.initialBricks*visibleRatio);
        int aliveBeforeDamage=e.alive;
        RemoveOuterBricks(e,Mathf.Max(0,e.alive-targetAlive));
        bool isKnockbackAttack=(packet.tags&DamageTags.Knockback)!=0;
        bool brokeVisibleBricks=e.alive<aliveBeforeDamage;
        impactDirection.y=0f;
        if((isKnockbackAttack||brokeVisibleBricks)&&impactDirection.sqrMagnitude>0.001f)
            e.knockbackVelocity+=impactDirection.normalized*knockbackSpeed;
    }

    private void RemoveOuterBricks(Enemy e,int count)
    {
        int aliveBefore=e.alive;
        int remaining=count;
        while(remaining>0&&e.alive>0)
        {
            exposedBricks.Clear();
            for(int index=0;index<e.visible.Length;index++)
            {
                if(e.visible[index]&&IsExposed(e,index))exposedBricks.Add(index);
            }
            if(exposedBricks.Count==0)break;
            for(int i=exposedBricks.Count-1;i>0;i--)
            {
                int swap=UnityEngine.Random.Range(0,i+1);
                int temp=exposedBricks[i];exposedBricks[i]=exposedBricks[swap];exposedBricks[swap]=temp;
            }
            int removeCount=Mathf.Min(remaining,exposedBricks.Count);
            for(int i=0;i<removeCount;i++)
            {
                int index=exposedBricks[i];
                AddPhaseBrick(e,index,false);
                if(fragments.Count<600&&UnityEngine.Random.value<0.65f)AddFragment(e,index,1.15f);
                e.visible[index]=false;e.alive--;remaining--;
            }
        }
        CullDetachedBrickIslands(e);
        if(e.alive<aliveBefore)UploadDamageMask(e);
    }

    private void CullDetachedBrickIslands(Enemy e)
    {
        for(int i=0;i<brickComponentLabels.Length;i++)brickComponentLabels[i]=-1;
        Array.Clear(brickComponentSizes,0,brickComponentSizes.Length);

        int componentCount=0;
        int largestComponent=-1;
        int largestSize=0;
        for(int start=0;start<e.visible.Length;start++)
        {
            if(!e.visible[start]||brickComponentLabels[start]>=0)continue;
            int head=0,tail=0,size=0;
            brickConnectivityQueue[tail++]=start;
            brickComponentLabels[start]=componentCount;
            while(head<tail)
            {
                int current=brickConnectivityQueue[head++];
                size++;
                int x=current%Grid,y=current/Grid;
                for(int oy=-1;oy<=1;oy++)
                for(int ox=-1;ox<=1;ox++)
                {
                    if(ox==0&&oy==0)continue;
                    int nx=x+ox,ny=y+oy;
                    if(nx<0||nx>=Grid||ny<0||ny>=Grid)continue;
                    int neighbor=ny*Grid+nx;
                    if(!e.visible[neighbor]||brickComponentLabels[neighbor]>=0)continue;
                    brickComponentLabels[neighbor]=componentCount;
                    brickConnectivityQueue[tail++]=neighbor;
                }
            }
            brickComponentSizes[componentCount]=size;
            if(size>largestSize){largestSize=size;largestComponent=componentCount;}
            componentCount++;
        }

        if(componentCount<=1)return;
        int minimumAlive=Mathf.CeilToInt(e.initialBricks*0.5f);
        for(int islandSize=1;islandSize<=MaxDetachedIslandBricks;islandSize++)
        for(int component=0;component<componentCount;component++)
        {
            if(component==largestComponent||brickComponentSizes[component]!=islandSize)continue;
            if(e.alive-islandSize<minimumAlive)continue;
            for(int index=0;index<e.visible.Length;index++)
            {
                if(!e.visible[index]||brickComponentLabels[index]!=component)continue;
                AddPhaseBrick(e,index,false);
                if(fragments.Count<600&&UnityEngine.Random.value<0.55f)AddFragment(e,index,1.05f);
                e.visible[index]=false;
                e.alive--;
            }
        }
    }

    private bool IsExposed(Enemy e,int index)
    {
        int x=index%Grid,y=index/Grid;
        return x==0||x==Grid-1||y==0||y==Grid-1||
            !e.visible[index-1]||!e.visible[index+1]||
            !e.visible[index-Grid]||!e.visible[index+Grid];
    }

    private void ExplodeEnemy(Enemy e)
    {
        if(enemyArchetypes[e.archetypeIndex].modelPrefab!=null)
        {
            BeginModelDeath(e);
            e.alive=0;e.dead=true;kills++;score+=Mathf.RoundToInt(e.maxHp*10f);
            UpdateEliminationGroupMarkers();
            return;
        }
        for (int i=0;i<e.visible.Length;i++)
        {
            if (!e.visible[i]) continue;
            AddPhaseBrick(e,i,true);
            if(fragments.Count<600&&UnityEngine.Random.value<0.48f)AddFragment(e,i,1.65f);
            e.visible[i]=false;
        }
        e.alive=0;e.dead=true;kills++;score+=Mathf.RoundToInt(e.maxHp*10f);
        UpdateEliminationGroupMarkers();
    }

    private void BeginModelDeath(Enemy e)
    {
        if(e.modelVisual==null)return;
        GameObject visual=e.modelVisual;
        VoxelEnemyReaction reaction=e.reaction!=null?e.reaction:visual.GetComponent<VoxelEnemyReaction>();
        if(reaction!=null)reaction.PlayDeath(e.lastImpactDirection);
        else visual.SetActive(false);
        collapsingEnemyVisuals.Add(visual);
        e.modelVisual=null;
        e.proceduralMotion=null;
        e.modelAnimator=null;
        e.reaction=null;
    }

    private void UpdateCollapsingVisualPool()
    {
        for(int i=collapsingEnemyVisuals.Count-1;i>=0;i--)
        {
            GameObject visual=collapsingEnemyVisuals[i];
            if(visual!=null&&visual.activeSelf)continue;
            collapsingEnemyVisuals.RemoveAt(i);
            if(visual==null)continue;
            ReturnModelVisual(visual);
        }
    }

    private void EmitImpactVoxels(Vector3 position,Vector3 direction,Color color,int count,float force,float sizeMultiplier=1f,float scatter=0.55f)
    {
        if(impactParticles==null)return;
        direction.y=Mathf.Max(0.08f,direction.y);
        if(direction.sqrMagnitude<0.001f)direction=Vector3.up;
        direction.Normalize();
        for(int i=0;i<count;i++)
        {
            ParticleSystem.EmitParams emit=new ParticleSystem.EmitParams
            {
                position=position+UnityEngine.Random.insideUnitSphere*0.08f,
                velocity=(direction*UnityEngine.Random.Range(0.65f,1.2f)+UnityEngine.Random.onUnitSphere*scatter)*force,
                startColor=color,
                startLifetime=UnityEngine.Random.Range(0.24f,0.38f),
                startSize=UnityEngine.Random.Range(0.065f,0.11f)*sizeMultiplier
            };
            impactParticles.Emit(emit,1);
        }
    }

    private void AddPhaseBrick(Enemy e,int index,bool deathSequence)
    {
        if(phaseBricks.Count>=1200)return;
        int x=index%Grid,y=index/Grid;
        float centerDistance=Vector2.Distance(new Vector2(x,y),new Vector2((Grid-1)*0.5f,(Grid-1)*0.5f));
        float outer01=Mathf.Clamp01(centerDistance/(Grid*0.55f));
        phaseBricks.Add(new PhaseBrick
        {
            origin=BrickWorld(e,x,y),
            age=0f,
            delay=deathSequence
                ?(1f-outer01)*0.16f+(y/(float)(Grid-1))*0.13f+UnityEngine.Random.Range(0f,0.085f)
                :UnityEngine.Random.Range(0f,0.055f),
            duration=deathSequence?UnityEngine.Random.Range(0.3f,0.42f):UnityEngine.Random.Range(0.24f,0.33f),
            size=Cell*e.scale,
            drift=UnityEngine.Random.Range(-0.16f,0.16f),
            color=(byte)(e.color[index]-1)
        });
    }

    private void AddFragment(Enemy e,int index,float force)
    {
        int x=index%Grid,y=index/Grid;
        Vector3 awayFromPlayer=e.position-player.position;awayFromPlayer.y=0f;
        if(awayFromPlayer.sqrMagnitude>0.001f)awayFromPlayer.Normalize();
        Vector3 scatter=new Vector3(UnityEngine.Random.Range(-1f,1f),UnityEngine.Random.Range(0.18f,1.35f),UnityEngine.Random.Range(-1f,1f)).normalized;
        Vector3 launchDirection=(scatter+awayFromPlayer*0.22f).normalized;
        float life=UnityEngine.Random.Range(0.28f,0.48f);
        fragments.Add(new Fragment
        {
            position=BrickWorld(e,x,y),
            velocity=launchDirection*UnityEngine.Random.Range(6.5f,10.5f)*force,
            life=life,maxLife=life,spin=UnityEngine.Random.Range(-18f,18f),color=(byte)(e.color[index]-1)
        });
    }

    private void UpdateEnemies(float dt)
    {
        for (int i=enemies.Count-1;i>=0;i--)
        {
            Enemy e=enemies[i];
            if (e.dead){enemies.RemoveAt(i);ReleaseEnemy(e);continue;}
            EnemyArchetype archetype=enemyArchetypes[e.archetypeIndex];
            e.attackCooldownRemaining=Mathf.Max(0f,e.attackCooldownRemaining-dt);
            e.specialCooldownRemaining=Mathf.Max(0f,e.specialCooldownRemaining-dt);
            e.animationTime+=dt;
            Vector3 toPlayer=player.position-e.position;toPlayer.y=0f;
            bool targetsDefenseSeal=mapMode==PrototypeMapMode.TripleSealDefense&&
                tripleSealDefenseState==TripleSealDefenseState.Fighting&&
                e.defenseRole!=DefenseEnemyRole.None&&e.defenseRole!=DefenseEnemyRole.Hunter;
            if(targetsDefenseSeal&&(e.defenseSealTarget<0||e.defenseSealTarget>=defenseSeals.Count||
                defenseSeals[e.defenseSealTarget].Destroyed))
                e.defenseSealTarget=GetNearestAliveDefenseSeal(e.position);
            if(targetsDefenseSeal&&e.defenseSealTarget<0)targetsDefenseSeal=false;
            Vector3 toTarget=targetsDefenseSeal?defenseSeals[e.defenseSealTarget].position-e.position:toPlayer;
            toTarget.y=0f;
            float distance=toTarget.magnitude;

            if(!e.encounterActivated)
            {
                if(distance<=activeMap.activationRadius)ActivateEnemyGroup(e.encounterGroup);
                if(!e.encounterActivated)
                {
                    e.knockbackVelocity=Vector3.zero;
                    continue;
                }
            }

            if(archetype.specialType==EnemySpecialType.StonehornRush&&
               e.actionState==EnemyActionState.Moving&&e.specialCooldownRemaining<=0f&&
               distance>=StonehornRushTriggerMinDistance&&distance<=StonehornRushTriggerMaxDistance)
            {
                e.rushDirection=toTarget.sqrMagnitude>0.001f?toTarget.normalized:Vector3.forward;
                float readyDuration=GetFullClipDuration(archetype.rushReadyClip,StonehornRushReadyFallbackDuration);
                SetEnemyAction(e,EnemyActionState.RushReady,readyDuration);
            }

            if(e.actionState==EnemyActionState.RushReady)
            {
                e.actionTimer-=dt;
                UpdateEnemyFacing(e,e.rushDirection,dt,2.2f);
                if(e.actionTimer>0f)
                {
                    e.position=ResolveArenaPosition(e.position+e.knockbackVelocity*dt,e.collisionRadius);
                    DecayEnemyKnockback(e,dt);
                    continue;
                }

                // Switch the visual and begin translation in the same update.
                SetEnemyAction(e,EnemyActionState.Rushing,StonehornRushDuration);
            }

            if(e.actionState==EnemyActionState.Rushing)
            {
                e.actionTimer-=dt;
                Vector3 start=e.position;
                Vector3 rushVelocity=e.rushDirection*StonehornRushSpeed;
                e.position=ResolveArenaPosition(e.position+(rushVelocity+e.knockbackVelocity)*dt,e.collisionRadius);
                UpdateEnemyFacing(e,rushVelocity,dt,2.2f);
                float hitRadius=e.collisionRadius+0.48f;
                if(SqrDistanceToSegment(player.position,start,e.position)<=hitRadius*hitRadius)
                {
                    bool dashInvulnerable=actionState==ActionState.Dashing&&dashElapsed<=DashInvulnerabilityDuration;
                    if(!dashInvulnerable)DamagePlayer(Mathf.RoundToInt(archetype.rushDamage*e.damageMultiplier),e.position);
                    e.attackCooldownRemaining=1f/Mathf.Max(0.01f,e.attackSpeed);
                    SetEnemyAction(e,EnemyActionState.RushRecovery,StonehornRushRecoveryDuration);
                }
                else if(e.actionTimer<=0f)
                {
                    SetEnemyAction(e,EnemyActionState.RushRecovery,StonehornRushRecoveryDuration);
                }
                DecayEnemyKnockback(e,dt);
                continue;
            }

            if(e.actionState==EnemyActionState.RushRecovery)
            {
                e.actionTimer-=dt;
                e.position=ResolveArenaPosition(e.position+e.knockbackVelocity*dt,e.collisionRadius);
                if(e.actionTimer<=0f)
                {
                    SetEnemyAction(e,EnemyActionState.Moving,0f);
                    e.specialCooldownRemaining=StonehornRushCooldown;
                }
                DecayEnemyKnockback(e,dt);
                continue;
            }

            if(targetsDefenseSeal&&distance<e.collisionRadius+tripleSealDefenseMap.sealHitRadius)
            {
                UpdateEnemyFacing(e,toTarget,dt);
                if(e.attackCooldownRemaining<=0f)
                {
                    EnemyArchetype defenseArchetype=enemyArchetypes[e.archetypeIndex];
                    int baseDamage=defenseArchetype.contactDamage>0?defenseArchetype.contactDamage:EnemyContactDamage;
                    DamageDefenseSeal(e.defenseSealTarget,baseDamage*e.damageMultiplier*e.defenseSealDamageMultiplier);
                    e.attackCooldownRemaining=1f/Mathf.Max(0.01f,e.attackSpeed);
                }
                DecayEnemyKnockback(e,dt);
                continue;
            }
            if (!targetsDefenseSeal&&distance<e.collisionRadius+0.45f)
            {
                UpdateEnemyFacing(e,toTarget,dt);
                bool dashInvulnerable=actionState==ActionState.Dashing&&dashElapsed<=DashInvulnerabilityDuration;
                if(!dashInvulnerable&&e.attackCooldownRemaining<=0f)
                {
                    int baseDamage=archetype.contactDamage>0?archetype.contactDamage:EnemyContactDamage;
                    DamagePlayer(Mathf.RoundToInt(baseDamage*e.damageMultiplier),e.position);
                    e.attackCooldownRemaining=1f/Mathf.Max(0.01f,e.attackSpeed);
                }
                DecayEnemyKnockback(e,dt);
                continue;
            }
            Vector3 travelDirection=GetObstacleAwareDirection(e.position,toTarget.normalized,e.collisionRadius);
            Vector3 velocity=travelDirection*e.speed;
            e.position=ResolveArenaPosition(e.position+(velocity+e.knockbackVelocity)*dt,e.collisionRadius);
            DecayEnemyKnockback(e,dt);
            UpdateEnemyFacing(e,velocity,dt);
        }

        SeparateEnemies(dt);
        for(int i=0;i<enemies.Count;i++)
            if(!enemies[i].dead)enemies[i].position=ResolveArenaPosition(enemies[i].position,enemies[i].collisionRadius);
    }

    private Vector3 GetObstacleAwareDirection(Vector3 position,Vector3 desiredDirection,float radius)
    {
        Vector3 steering=Vector3.zero;
        for(int i=0;i<arenaObstacles.Count;i++)
        {
            ArenaObstacle obstacle=arenaObstacles[i];
            Vector3 away=position-obstacle.center;away.y=0f;
            float distance=away.magnitude;
            float influence=obstacle.radius+radius+4f;
            if(distance>=influence)continue;
            if(distance<0.001f)away=Vector3.right;
            else away/=distance;
            Vector3 tangent=Vector3.Cross(Vector3.up,away).normalized;
            if(Vector3.Dot(-tangent,desiredDirection)>Vector3.Dot(tangent,desiredDirection))tangent=-tangent;
            float weight=1f-Mathf.Clamp01(distance/influence);
            steering+=(away*0.75f+tangent*1.45f)*weight;
        }
        Vector3 result=desiredDirection+steering;
        result.y=0f;
        return result.sqrMagnitude>0.001f?result.normalized:desiredDirection;
    }

    private Vector3 ResolveArenaPosition(Vector3 position,float radius)
    {
        position.y=0f;
        for(int pass=0;pass<2;pass++)
        for(int i=0;i<arenaObstacles.Count;i++)
        {
            ArenaObstacle obstacle=arenaObstacles[i];
            Vector3 offset=position-obstacle.center;offset.y=0f;
            float minimumDistance=obstacle.radius+radius;
            float distance=offset.magnitude;
            if(distance>=minimumDistance)continue;
            Vector3 direction=distance>0.001f?offset/distance:Vector3.right;
            position=obstacle.center+direction*minimumDistance;
        }
        Vector3 flat=position;flat.y=0f;
        float boundary=ArenaRadius-radius;
        if(flat.magnitude>boundary)position=flat.normalized*boundary;
        return position;
    }

    private Vector3 ClampMovementSegmentToObstacles(Vector3 start,Vector3 end,float radius)
    {
        Vector3 segment=end-start;segment.y=0f;
        float length=segment.magnitude;
        if(length<0.001f)return start;
        Vector3 direction=segment/length;
        float allowedLength=length;
        for(int i=0;i<arenaObstacles.Count;i++)
        {
            ArenaObstacle obstacle=arenaObstacles[i];
            Vector3 toCenter=obstacle.center-start;toCenter.y=0f;
            float projected=Vector3.Dot(toCenter,direction);
            if(projected<=0f||projected>=allowedLength)continue;
            float expandedRadius=obstacle.radius+radius;
            float perpendicularSq=toCenter.sqrMagnitude-projected*projected;
            if(perpendicularSq>=expandedRadius*expandedRadius)continue;
            float entry=projected-Mathf.Sqrt(expandedRadius*expandedRadius-perpendicularSq);
            allowedLength=Mathf.Min(allowedLength,Mathf.Max(0f,entry-0.05f));
        }
        return start+direction*allowedLength;
    }

    private void CheckEliminationMapCompletion()
    {
        if(eliminationMapComplete)return;
        for(int i=0;i<enemies.Count;i++)
        {
            Enemy enemy=enemies[i];
            if(!enemy.dead&&enemy.encounterGroup>=0)return;
        }
        eliminationMapComplete=true;
        eliminationClearTime=elapsed;
        score+=5000;
        if(eliminationReward!=null)
        {
            eliminationReward.transform.position=player.position+aimDirection*2.2f+Vector3.up*0.65f;
            eliminationReward.SetActive(true);
        }
        if(eliminationExit!=null)eliminationExit.SetActive(true);
        if(eliminationEntrySeal!=null)eliminationEntrySeal.SetActive(false);
        UpdateEliminationGroupMarkers();
        Debug.Log($"[Annihilation Map] Cleared in {eliminationClearTime:0.0}s. Reward and next-route exit opened.");
    }

    private void SeparateEnemies(float dt)
    {
        float response=1f-Mathf.Exp(-EnemySeparationResponsiveness*dt);
        for(int i=0;i<enemies.Count-1;i++)
        {
            Enemy a=enemies[i];
            if(a.dead)continue;
            for(int j=i+1;j<enemies.Count;j++)
            {
                Enemy b=enemies[j];
                if(b.dead)continue;

                Vector3 offset=b.position-a.position;
                offset.y=0f;
                float desiredDistance=(a.collisionRadius+b.collisionRadius)*EnemySeparationRadiusFactor;
                float sqrDistance=offset.sqrMagnitude;
                if(sqrDistance>=desiredDistance*desiredDistance)continue;

                float distance=Mathf.Sqrt(sqrDistance);
                Vector3 direction=distance>0.001f
                    ?offset/distance
                    :new Vector3(Mathf.Cos((i*17+j*31)*0.73f),0f,Mathf.Sin((i*17+j*31)*0.73f));
                float correction=(desiredDistance-distance)*response;
                bool aRushing=a.actionState==EnemyActionState.Rushing;
                bool bRushing=b.actionState==EnemyActionState.Rushing;

                if(aRushing&&!bRushing)b.position+=direction*correction;
                else if(!aRushing&&bRushing)a.position-=direction*correction;
                else
                {
                    Vector3 halfCorrection=direction*(correction*0.5f);
                    a.position-=halfCorrection;
                    b.position+=halfCorrection;
                }
            }
        }
    }

    private void SetEnemyAction(Enemy e,EnemyActionState state,float duration)
    {
        e.actionState=state;
        e.actionTimer=duration;
        e.animationTime=0f;
    }

    private static float GetFullClipDuration(EnemyAnimationClip clip,float fallback)
    {
        if(clip==null||clip.frames==null||clip.frames.Length==0||clip.fps<=0f)return fallback;
        // A small final-frame hold prevents a low frame-rate update from visually skipping the last pose.
        return (clip.frames.Length+0.2f)/clip.fps;
    }

    private void UpdateEnemyFacing(Enemy e,Vector3 velocity,float dt,float turnSpeedMultiplier=1f)
    {
        if(enemyArchetypes[e.archetypeIndex].modelPrefab!=null)
        {
            velocity.y=0f;
            if(velocity.sqrMagnitude>0.001f)
            {
                Vector3 current=e.facingDirection;
                current.y=0f;
                if(current.sqrMagnitude<0.001f)current=velocity.normalized;
                float maxRadians=e.turnSpeedDegrees*Mathf.Max(0.1f,turnSpeedMultiplier)*Mathf.Deg2Rad*dt;
                e.facingDirection=Vector3.RotateTowards(current.normalized,velocity.normalized,maxRadians,0f).normalized;
            }
            return;
        }
        float screenHorizontal=Vector3.Dot(velocity,cam.transform.right);
        // Every Stage 1 source sheet has a native screen-right pose.
        if(screenHorizontal>0.08f)e.flipX=false;
        else if(screenHorizontal<-0.08f)e.flipX=true;
    }

    private void SyncModelEnemyVisual(Enemy e,float dt)
    {
        if(e.modelVisual==null)return;
        Vector3 facing=e.facingDirection;facing.y=0f;
        if(facing.sqrMagnitude<0.001f)facing=Vector3.forward;
        float yawOffset=enemyArchetypes[e.archetypeIndex].modelYawOffset;
        Quaternion rotation=Quaternion.LookRotation(facing.normalized,Vector3.up)*Quaternion.Euler(0f,yawOffset,0f);
        float distance=Vector3.Distance(new Vector3(e.position.x,0f,e.position.z),new Vector3(player.position.x,0f,player.position.z));
        bool moving=e.actionState==EnemyActionState.Moving&&distance>e.collisionRadius+0.45f;
        moving&=e.encounterActivated;
        if(e.modelAnimator!=null&&e.modelAnimator.enabled)
        {
            e.modelAnimator.SetBool(EnemyMovingParameter,moving);
            e.modelAnimator.speed=moving?1f:e.idleAnimationSpeed;
        }
        float normalizedSpeed=Mathf.Clamp01(e.speed/2.5f);
        if(UseProceduralTyrannosaurusMotion&&e.proceduralMotion!=null)
        {
            e.proceduralMotion.ApplyMotion(e.position,rotation,moving,normalizedSpeed,dt);
        }
        else
        {
            float groundOffset=e.proceduralMotion!=null?e.proceduralMotion.GroundOffset:0f;
            e.modelVisual.transform.SetPositionAndRotation(e.position+Vector3.up*groundOffset,rotation);
        }
    }

    private static void DecayEnemyKnockback(Enemy e,float dt)
    {
        e.knockbackVelocity=Vector3.Lerp(e.knockbackVelocity,Vector3.zero,1f-Mathf.Exp(-8f*dt));
    }

    private void DamagePlayer(int damage,Vector3 sourcePosition)
    {
        if(damage<=0||playerHp<=0)return;
        playerHp=Mathf.Max(0,playerHp-damage);
        Vector3 away=player.position-sourcePosition;
        away.y=0f;
        if(away.sqrMagnitude<0.001f)away=-aimDirection;
        playerKnockbackVelocity+=away.normalized*30.6f;
        if(playerKnockbackVelocity.magnitude>37.2f)playerKnockbackVelocity=playerKnockbackVelocity.normalized*37.2f;
        if(playerDamageReaction!=null)playerDamageReaction.PlayHit();
        EmitImpactVoxels(player.position+Vector3.up*0.78f,away.normalized+Vector3.up*0.16f,new Color(0.88f,0.018f,0.012f),14,2.65f,2.3f,1.05f);
        EmitImpactVoxels(player.position+Vector3.up*0.72f,away.normalized,new Color(0.35f,0.003f,0.002f),6,2.15f,1.75f,1.2f);
        if(playerHp>0)StartPlayerHitStun();
        else
        {
            playerKnockbackVelocity=Vector3.zero;
            actionState=ActionState.Dead;
            if(playerRagdoll!=null)playerRagdoll.PlayDeath(away.normalized);
            RaisePlayerDefeated();
        }
    }

    private void StartPlayerHitStun()
    {
        actionState=ActionState.HitStunned;
        actionTimer=PlayerHitStunDuration;
        activeSkill=bufferedSkill=-1;
        activeCrackTarget=bufferedCrackTarget=null;
        inputBufferTimer=0f;
        attackAnimationRemaining=0f;
        attackVisualLife=0f;
        movementAmount=0f;
        movementDirection=Vector3.zero;
        attackVisual.gameObject.SetActive(false);
        if(playerAnimator==null)return;
        playerAnimator.speed=1f;
        playerAnimator.ResetTrigger(DashParameter);
        playerAnimator.ResetTrigger(AttackParameter);
        playerAnimator.SetFloat(LocomotionParameter,0f);
        playerAnimator.CrossFade("Idle",0.04f,0,0f);
        if(playerAnimator.layerCount>1)playerAnimator.SetLayerWeight(1,1f);
    }

    private void UpdatePlayerKnockback(float dt)
    {
        if(playerKnockbackVelocity.sqrMagnitude<0.0001f)return;
        player.position=ResolveArenaPosition(player.position+playerKnockbackVelocity*dt,0.5f);
        playerKnockbackVelocity=Vector3.Lerp(playerKnockbackVelocity,Vector3.zero,1f-Mathf.Exp(-14f*dt));
    }

    private static float SqrDistanceToSegment(Vector3 point,Vector3 start,Vector3 end)
    {
        point.y=start.y=end.y=0f;
        Vector3 segment=end-start;
        float lengthSq=segment.sqrMagnitude;
        if(lengthSq<0.0001f)return (point-start).sqrMagnitude;
        float t=Mathf.Clamp01(Vector3.Dot(point-start,segment)/lengthSq);
        return (point-(start+segment*t)).sqrMagnitude;
    }

    private void UpdateFragments(float dt)
    {
        for (int i=fragments.Count-1;i>=0;i--)
        {
            Fragment f=fragments[i];f.life-=dt;
            if(f.life<=0f){fragments.RemoveAt(i);continue;}
            f.velocity*=Mathf.Exp(-3.8f*dt);
            f.velocity+=Vector3.down*18f*dt;
            f.position+=f.velocity*dt;fragments[i]=f;
        }
    }

    private void RaisePlayerDefeated()
    {
        if(playerDefeatRaised)return;
        playerDefeatRaised=true;
        PlayerDefeated?.Invoke();
    }

    private void UpdatePhaseBricks(float dt)
    {
        for(int i=phaseBricks.Count-1;i>=0;i--)
        {
            PhaseBrick p=phaseBricks[i];
            p.age+=dt;
            if(p.age>=p.delay+p.duration){phaseBricks.RemoveAt(i);continue;}
            phaseBricks[i]=p;
        }
    }

    private void UpdateCamera(float dt)
    {
        Vector3 desired=cameraBase+new Vector3(player.position.x,0f,player.position.z);
        cam.transform.position=Vector3.Lerp(cam.transform.position,desired,1f-Mathf.Exp(-8f*dt));
    }

    private Vector3 BrickWorld(Enemy e,int x,int y)
    {
        EnemyArchetype archetype=enemyArchetypes[e.archetypeIndex];
        int displayX=e.flipX?Grid-1-x:x;
        float size=Cell*e.scale;
        Vector3 towardCamera=-cam.transform.forward;
        towardCamera.y=0f;
        if(towardCamera.sqrMagnitude<0.001f)towardCamera=Vector3.back;
        towardCamera.Normalize();
        Vector3 right=Vector3.Cross(Vector3.up,towardCamera).normalized;
        Vector3 up=Vector3.up;
        float groundOrigin=((Grid-1)*0.5f-archetype.minY)*size+0.05f;
        return e.position+up*groundOrigin+right*((displayX-(Grid-1)*0.5f)*size)+up*((y-(Grid-1)*0.5f)*size);
    }

    private EnemyAnimationClip GetEnemyAnimationClip(Enemy e,EnemyArchetype archetype)
    {
        if(e.actionState==EnemyActionState.RushReady&&archetype.rushReadyClip!=null)return archetype.rushReadyClip;
        if(e.actionState==EnemyActionState.Rushing&&archetype.rushAttackClip!=null)return archetype.rushAttackClip;
        return archetype.moveClip;
    }

    private int GetEnemyAnimationFrame(Enemy e,EnemyAnimationClip clip)
    {
        float phase=clip==enemyArchetypes[e.archetypeIndex].moveClip?e.phase:0f;
        int frame=Mathf.FloorToInt(e.animationTime*clip.fps+phase);
        return clip.loop?frame%clip.frames.Length:Mathf.Min(frame,clip.frames.Length-1);
    }

    private static void ClearAnimationBatches(EnemyAnimationClip clip)
    {
        if(clip==null)return;
        for(int frame=0;frame<clip.meshBatches.Length;frame++)
        {
            clip.meshBatches[frame].Clear();
            clip.damageLayers[frame].Clear();
        }
    }

    private void DrawAnimationBatches(EnemyAnimationClip clip)
    {
        if(clip==null)return;
        for(int frame=0;frame<clip.meshes.Length;frame++)
            DrawVoxelMeshBatch(clip.meshes[frame],clip.meshBatches[frame],clip.damageLayers[frame]);
    }

    private void RenderVoxelMeshEnemies()
    {
        FlushDamageMasks();
        for(int a=0;a<enemyArchetypes.Length;a++)
        {
            ClearAnimationBatches(enemyArchetypes[a].moveClip);
            ClearAnimationBatches(enemyArchetypes[a].rushReadyClip);
            ClearAnimationBatches(enemyArchetypes[a].rushAttackClip);
        }

        Vector3 towardCamera=-cam.transform.forward;towardCamera.y=0f;
        if(towardCamera.sqrMagnitude<0.001f)towardCamera=Vector3.back;
        towardCamera.Normalize();
        Quaternion billboard=Quaternion.LookRotation(towardCamera,Vector3.up);
        for(int n=0;n<enemies.Count;n++)
        {
            Enemy e=enemies[n];
            if(e.dead)continue;
            EnemyArchetype archetype=enemyArchetypes[e.archetypeIndex];
            if(archetype.modelPrefab!=null)continue;
            EnemyAnimationClip clip=GetEnemyAnimationClip(e,archetype);
            int frame=GetEnemyAnimationFrame(e,clip);
            Quaternion rotation=e.flipX?billboard*Quaternion.Euler(0f,180f,0f):billboard;
            Matrix4x4 matrix=Matrix4x4.TRS(e.position+Vector3.up*0.05f,rotation,Vector3.one*e.scale);
            clip.meshBatches[frame].Add(matrix);
            clip.damageLayers[frame].Add(e.damageMaskLayer);

            if(e.crackStacks<=0)continue;
            byte[] frameColors=clip.frames[frame];
            Vector3 overlayScale=new Vector3(Cell*1.01f,Cell*1.01f,Cell*0.18f)*e.scale;
            for(int index=0;index<e.visible.Length;index++)
            {
                if(!e.visible[index]||frameColors[index]==0||!IsCrackTintBrick(e,index))continue;
                int x=index%Grid,y=index/Grid;
                Vector3 position=BrickWorld(e,x,y)+towardCamera*(Cell*e.scale*1.05f);
                crackBrickBatch.Add(Matrix4x4.TRS(position,billboard,overlayScale));
            }
        }
        for(int a=0;a<enemyArchetypes.Length;a++)
        {
            DrawAnimationBatches(enemyArchetypes[a].moveClip);
            DrawAnimationBatches(enemyArchetypes[a].rushReadyClip);
            DrawAnimationBatches(enemyArchetypes[a].rushAttackClip);
        }
    }

    private void LateUpdate()
    {
        ApplyUpperBodyAim();
        for(int i=0;i<enemies.Count;i++)
            if(!enemies[i].dead&&enemyArchetypes[enemies[i].archetypeIndex].modelPrefab!=null)
                SyncModelEnemyVisual(enemies[i],Time.deltaTime);
        crackBrickBatch.Clear();
        for(int i=0;i<Palette.Length;i++)
        {
            if(brickBatches[i]==null)brickBatches[i]=new List<Matrix4x4>(2048);else brickBatches[i].Clear();
            if(fragmentBatches[i]==null)fragmentBatches[i]=new List<Matrix4x4>(128);else fragmentBatches[i].Clear();
            if(phaseBatches[i]==null)phaseBatches[i]=new List<Matrix4x4>(256);else phaseBatches[i].Clear();
        }
        if(useVoxelMeshRendering)RenderVoxelMeshEnemies();
        else for(int n=0;n<enemies.Count;n++)
        {
            Enemy e=enemies[n];
            EnemyArchetype archetype=enemyArchetypes[e.archetypeIndex];
            if(archetype.modelPrefab!=null)continue;
            EnemyAnimationClip clip=GetEnemyAnimationClip(e,archetype);
            byte[] frameColors=clip.frames[GetEnemyAnimationFrame(e,clip)];
            Vector3 towardCamera=-cam.transform.forward;towardCamera.y=0f;
            if(towardCamera.sqrMagnitude<0.001f)towardCamera=Vector3.back;
            towardCamera.Normalize();
            Quaternion billboard=Quaternion.LookRotation(towardCamera,Vector3.up);
            Vector3 right=Vector3.Cross(Vector3.up,towardCamera).normalized;
            Vector3 up=Vector3.up;
            float size=Cell*e.scale;
            Vector3 scale=new Vector3(Cell*1.01f,Cell*1.01f,Cell*0.72f)*e.scale;
            float groundOrigin=((Grid-1)*0.5f-archetype.minY)*size+0.05f;
            Vector3 origin=e.position+up*groundOrigin;
            for(int index=0;index<e.visible.Length;index++)
            {
                if(!e.visible[index])continue;
                byte frameColor=frameColors[index];
                if(frameColor==0)continue;
                int x=index%Grid,y=index/Grid;
                int displayX=e.flipX?Grid-1-x:x;
                Vector3 position=origin+right*((displayX-(Grid-1)*0.5f)*size)+up*((y-(Grid-1)*0.5f)*size);
                Matrix4x4 matrix=Matrix4x4.TRS(position,billboard,scale);
                if(e.crackStacks>0&&IsCrackTintBrick(e,index))crackBrickBatch.Add(matrix);
                else brickBatches[frameColor-1].Add(matrix);
            }
        }
        for(int i=0;i<fragments.Count;i++)
        {
            Fragment f=fragments[i];
            float life01=f.maxLife>0f?Mathf.Clamp01(f.life/f.maxLife):0f;
            float size=Cell*Mathf.SmoothStep(0.08f,0.72f,life01);
            fragmentBatches[f.color].Add(Matrix4x4.TRS(f.position,Quaternion.Euler(f.spin*elapsed*20f,f.spin*elapsed*14f,0f),Vector3.one*size));
        }
        Vector3 phaseFacing=-cam.transform.forward;phaseFacing.y=0f;
        if(phaseFacing.sqrMagnitude<0.001f)phaseFacing=Vector3.back;
        Quaternion phaseRotation=Quaternion.LookRotation(phaseFacing.normalized,Vector3.up);
        for(int i=0;i<phaseBricks.Count;i++)
        {
            PhaseBrick p=phaseBricks[i];
            float localTime=p.age-p.delay;
            float t=Mathf.Clamp01(localTime/p.duration);
            float scaleFactor=t<0.18f?Mathf.Lerp(1f,1.22f,t/0.18f):1.22f*(1f-Mathf.SmoothStep(0f,1f,(t-0.18f)/0.82f));
            float protrude=Mathf.Sin(Mathf.Min(1f,t/0.28f)*Mathf.PI)*0.22f;
            Vector3 position=p.origin-cam.transform.forward*protrude+cam.transform.forward*(t*t*2.15f)+cam.transform.right*(p.drift*t);
            Vector3 scale=new Vector3(p.size*1.01f*scaleFactor,p.size*1.01f*scaleFactor,p.size*0.72f*Mathf.Lerp(1f,0.035f,t));
            phaseBatches[p.color].Add(Matrix4x4.TRS(position,phaseRotation,scale));
        }
        for(int i=0;i<Palette.Length;i++)
        {
            DrawBatch(brickBatches[i],materials[i]);
            DrawBatch(fragmentBatches[i],materials[i]);
            DrawBatch(phaseBatches[i],phaseMaterials[i]);
        }
        DrawBatch(crackBrickBatch,crackMaterial);
        for(int i=0;i<crackPulses.Count;i++)
        {
            CrackPulse pulse=crackPulses[i];
            float localTime=pulse.age-pulse.delay;
            if(localTime<0f)continue;
            float t=Mathf.Clamp01(localTime/pulse.duration);
            float radius=pulse.radius*Mathf.Lerp(0.35f,1.08f,Mathf.SmoothStep(0f,1f,t));
            Graphics.DrawMesh(crackRingMesh,Matrix4x4.TRS(pulse.position,Quaternion.identity,new Vector3(radius,1f,radius)),crackMaterial,0,cam);
        }
    }

    private void ApplyUpperBodyAim()
    {
        if(playerUpperBody==null||playerVisual==null||playerHp<=0||actionState==ActionState.Dead||actionState==ActionState.Dashing||actionState==ActionState.HitStunned)return;
        Vector3 lowerForward=playerVisual.forward;lowerForward.y=0f;
        if(lowerForward.sqrMagnitude<0.001f||aimDirection.sqrMagnitude<0.001f)return;
        float yaw=Mathf.Clamp(Vector3.SignedAngle(lowerForward.normalized,aimDirection,Vector3.up),-85f,85f);
        playerUpperBody.rotation=Quaternion.AngleAxis(yaw,Vector3.up)*playerUpperBody.rotation;
    }

    private void TriggerHitStop(float duration)
    {
        hitStopRemaining=Mathf.Max(hitStopRemaining,duration);
        if(hitStopActive)return;
        hitStopActive=true;
        timeScaleBeforeHitStop=Time.timeScale>0f?Time.timeScale:1f;
        Time.timeScale=0f;
    }

    private void EndHitStop()
    {
        if(!hitStopActive)return;
        hitStopActive=false;
        hitStopRemaining=0f;
        Time.timeScale=timeScaleBeforeHitStop>0f?timeScaleBeforeHitStop:1f;
    }

    private void DrawBatch(List<Matrix4x4> matrices,Material material)
    {
        DrawMeshBatch(cubeMesh,matrices,material);
    }

    private void DrawMeshBatch(Mesh mesh,List<Matrix4x4> matrices,Material material)
    {
        for(int offset=0;offset<matrices.Count;offset+=1023)
        {
            int count=Mathf.Min(1023,matrices.Count-offset);matrices.CopyTo(offset,drawBuffer,0,count);
            Graphics.DrawMeshInstanced(mesh,0,material,drawBuffer,count,null,UnityEngine.Rendering.ShadowCastingMode.Off,false,0,cam);
        }
    }

    private void DrawVoxelMeshBatch(Mesh mesh,List<Matrix4x4> matrices,List<float> damageLayers)
    {
        for(int offset=0;offset<matrices.Count;offset+=1023)
        {
            int count=Mathf.Min(1023,matrices.Count-offset);
            matrices.CopyTo(offset,drawBuffer,0,count);
            damageLayers.CopyTo(offset,voxelDamageLayerBuffer,0,count);
            voxelMeshProperties.Clear();
            voxelMeshProperties.SetFloatArray(DamageLayerProperty,voxelDamageLayerBuffer);
            Graphics.DrawMeshInstanced(mesh,0,voxelMeshMaterial,drawBuffer,count,voxelMeshProperties,UnityEngine.Rendering.ShadowCastingMode.Off,false,0,cam);
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        GUI.Box(new Rect(18,18,330,190),string.Empty);
        if(mapMode==PrototypeMapMode.ScaleTrial)
        {
            int alive=0;
            for(int i=0;i<enemies.Count;i++)if(!enemies[i].dead)alive++;
            string choice=scaleTrialState==ScaleTrialState.Choosing?"LEFT: SETTLE   RIGHT: CHALLENGE":"ALL REWARD AT RISK";
            GUI.Label(new Rect(30,25,305,30),"SCALE TRIAL TEST",titleStyle);
            GUI.Label(new Rect(34,60,300,140),$"HP  {playerHp}/{PlayerMaxHealth}   Score  {score:N0}\nStage  {scaleTrialStage}/{scaleTrialMap.stageCount}   State  {scaleTrialState}\nReward  x{scaleTrialMap.GetRewardMultiplier(scaleTrialStage):0.##}\nTargets  {alive}   Kills  {kills}\n{choice}   FPS  {fps:0}",textStyle);
        }
        else if(mapMode==PrototypeMapMode.SealExcavation)
        {
            int alive=0;
            for(int i=0;i<enemies.Count;i++)if(!enemies[i].dead)alive++;
            GUI.Label(new Rect(30,25,305,30),"SEAL EXCAVATION TEST",titleStyle);
            GUI.Label(new Rect(34,60,300,140),$"HP  {playerHp}/{PlayerMaxHealth}   Score  {score:N0}\nSelected  {selectedSealCount}/{sealExcavationMap.maximumSelectedSeals}   State  {sealExcavationState}\nActive seal  {activeSealOrder}/{selectedSealCount}\nPlayer  MOV x{playerMoveSpeedMultiplier:0.00}  DMG x{playerDamageMultiplier:0.00}\nATK SPD x{playerAttackSpeedMultiplier:0.00}  RARITY +{playerItemRarityBonus*100f:0}%\nTargets  {alive}   FPS  {fps:0}",textStyle);
            DrawSealExcavationUi();
        }
        else if(mapMode==PrototypeMapMode.TripleSealDefense)
        {
            int alive=0;
            for(int i=0;i<enemies.Count;i++)if(!enemies[i].dead)alive++;
            int surviving=0;
            for(int i=0;i<defenseSeals.Count;i++)if(!defenseSeals[i].Destroyed)surviving++;
            GUI.Label(new Rect(30,25,305,30),"TRIPLE SEAL DEFENSE",titleStyle);
            GUI.Label(new Rect(34,60,300,140),$"HP  {playerHp}/{PlayerMaxHealth}   Score  {score:N0}\nWave  {defenseWave}/{tripleSealDefenseMap.waveCount}   State  {tripleSealDefenseState}\nSeals  {surviving}/3 surviving\nTargets  {alive}   Kills  {kills}\nVoxel T-Rex   FPS  {fps:0}",textStyle);
            DrawTripleSealDefenseUi();
        }
        else if(mapMode==PrototypeMapMode.TimedSurvival)
        {
            int alive=CountAliveEnemies();
            string phase=timedSurvivalState==TimedSurvivalState.Cleanup?"CLEAR REMAINING ENEMIES":timedSurvivalState.ToString().ToUpperInvariant();
            GUI.Label(new Rect(30,25,305,30),"TIMED SURVIVAL",titleStyle);
            GUI.Label(new Rect(34,60,300,140),$"HP  {playerHp}/{PlayerMaxHealth}   Score  {score:N0}\nTime  {timedSurvivalRemaining:0.0}s   {phase}\nTargets  {alive}/{timedSurvivalMap.maximumAliveEnemies}\nSpawned  {timedSurvivalTotalSpawned}   Kills  {kills}\nVoxel T-Rex   FPS  {fps:0}",textStyle);
        }
        else
        {
            GetEliminationProgress(out int totalGroups,out int clearedGroups,out int activeGroups,out int encounterEnemies);
            GUI.Label(new Rect(30,25,305,30),"ANNIHILATION TEST",titleStyle);
            GUI.Label(new Rect(34,60,300,140),$"HP  {playerHp}/{PlayerMaxHealth}   Score  {score:N0}\nDamage  {AttackDamage:0}   Cooldown  {AttackCooldown:0.00}s\nGroups  {clearedGroups}/{totalGroups} cleared   {activeGroups} active\nTargets  {encounterEnemies}   Kills  {kills}\nVoxel T-Rex   FPS  {fps:0}",textStyle);
        }
        GUI.Label(new Rect(Screen.width-275,22,255,145),"WASD  Move\nMOUSE  Aim\nLMB  Skill Slot 1\nRMB  Skill Slot 2\nSPACE  Dash\nF1  Add 20 enemies\nF2  Stress: "+(stressMode?"ON":"OFF")+"\nF3  Render: "+(useVoxelMeshRendering?"MESH":"CUBES")+"\nR  Reset",textStyle);
        float hudY=Screen.height-74f;
        DrawCommandSlot(new Rect(Screen.width*0.5f-190f,hudY,115f,48f),"LMB CRACK",skillCooldownRemaining[0],skillCooldown[0],new Color(0.25f,0.85f,1f));
        DrawCommandSlot(new Rect(Screen.width*0.5f-57f,hudY,115f,48f),"SPACE DASH",dashCooldownRemaining,DashCooldown,new Color(0.4f,1f,0.75f));
        DrawCommandSlot(new Rect(Screen.width*0.5f+75f,hudY,115f,48f),"RMB BOOM "+lastSkillHits[1],skillCooldownRemaining[1],skillCooldown[1],new Color(1f,0.25f,0.72f));
        float hudMoveMultiplier=Mathf.Lerp(1f,DashSlowMultiplier,Mathf.Clamp01(dashSlowRemaining/DashSlowDuration));
        GUI.Label(new Rect(Screen.width*0.5f-140f,hudY-25f,280f,22f),"STATE: "+actionState+"   MOVE "+Mathf.RoundToInt(hudMoveMultiplier*100f)+"%",centerStyle);
        if(mapMode==PrototypeMapMode.ScaleTrial&&scaleTrialState==ScaleTrialState.Choosing)
            DrawScaleTrialChoiceUi();
        if(playerHp<=0)
        {
            GUI.Box(new Rect(Screen.width*0.5f-210f,Screen.height*0.5f-70f,420f,140f),string.Empty);
            GUI.Label(new Rect(Screen.width*0.5f-190f,Screen.height*0.5f-45f,380f,40f),"PLAYER DOWN",titleStyle);
            GUI.Label(new Rect(Screen.width*0.5f-190f,Screen.height*0.5f+5f,380f,45f),"Press R to retry",centerStyle);
        }
        else if(eliminationMapComplete)
        {
            GUI.Box(new Rect(Screen.width*0.5f-230f,42f,460f,92f),string.Empty);
            string completeTitle=mapMode==PrototypeMapMode.ScaleTrial?"SCALE TRIAL SETTLED":
                mapMode==PrototypeMapMode.SealExcavation?"SEAL EXCAVATION COMPLETE":
                mapMode==PrototypeMapMode.TripleSealDefense?"TRIPLE SEAL DEFENDED":
                mapMode==PrototypeMapMode.TimedSurvival?"TIMED SURVIVAL COMPLETE":"ANNIHILATION COMPLETE";
            GUI.Label(new Rect(Screen.width*0.5f-210f,49f,420f,34f),completeTitle,titleStyle);
            GUI.Label(new Rect(Screen.width*0.5f-210f,82f,420f,42f),$"Clear {eliminationClearTime:0.0}s  |  Reward dropped  |  Exit opened",centerStyle);
        }
    }

    private static Rect GetDefenseBeginRect()
    {
        return new Rect(Screen.width*0.5f-150f,Screen.height-150f,300f,58f);
    }

    private void DrawTripleSealDefenseUi()
    {
        float cardWidth=Mathf.Min(245f,(Screen.width-80f)/3f);
        float totalWidth=cardWidth*3f+20f;
        float startX=(Screen.width-totalWidth)*0.5f;
        for(int i=0;i<defenseSeals.Count;i++)
        {
            DefenseSeal seal=defenseSeals[i];
            Rect card=new Rect(startX+i*(cardWidth+10f),18f,cardWidth,66f);
            Color old=GUI.color;
            GUI.color=seal.Destroyed?new Color(0.24f,0.02f,0.02f,0.88f):new Color(seal.baseColor.r,seal.baseColor.g,seal.baseColor.b,0.88f);
            GUI.DrawTexture(card,Texture2D.whiteTexture);
            GUI.color=old;
            GUI.Box(card,string.Empty);
            float hp01=seal.hp/Mathf.Max(1f,seal.maxHp);
            string status=seal.Destroyed?"DESTROYED":$"{Mathf.CeilToInt(seal.hp)}/{Mathf.CeilToInt(seal.maxHp)}";
            GUI.Label(new Rect(card.x+8f,card.y+5f,card.width-16f,24f),$"{seal.rewardName}  {status}",centerStyle);
            GUI.color=new Color(0.08f,0.08f,0.08f,0.9f);
            GUI.DrawTexture(new Rect(card.x+12f,card.y+39f,card.width-24f,12f),Texture2D.whiteTexture);
            GUI.color=seal.Destroyed?new Color(0.35f,0.02f,0.02f):Color.Lerp(Color.red,Color.green,hp01);
            GUI.DrawTexture(new Rect(card.x+12f,card.y+39f,(card.width-24f)*hp01,12f),Texture2D.whiteTexture);
            GUI.color=old;
        }

        if(defenseAlertTime>0f)
        {
            GUI.Box(new Rect(Screen.width*0.5f-240f,98f,480f,44f),string.Empty);
            GUI.Label(new Rect(Screen.width*0.5f-225f,105f,450f,30f),defenseAlert,centerStyle);
        }

        if(tripleSealDefenseState==TripleSealDefenseState.Preparing)
        {
            Rect begin=GetDefenseBeginRect();
            if(GUI.Button(begin,"BEGIN TRIPLE SEAL DEFENSE"))BeginTripleSealDefense();
            GUI.Label(new Rect(begin.x-160f,begin.y-33f,begin.width+320f,25f),
                "Protect the reward seals. At least one must survive.",centerStyle);
        }
        else if(tripleSealDefenseState==TripleSealDefenseState.Failed)
        {
            GUI.Box(new Rect(Screen.width*0.5f-220f,Screen.height*0.5f-55f,440f,110f),string.Empty);
            GUI.Label(new Rect(Screen.width*0.5f-200f,Screen.height*0.5f-35f,400f,34f),"DEFENSE FAILED",titleStyle);
            GUI.Label(new Rect(Screen.width*0.5f-200f,Screen.height*0.5f+8f,400f,28f),"Press R to retry",centerStyle);
        }
    }

    private static Rect GetSealConfirmRect()
    {
        return new Rect(Screen.width*0.5f-150f,Screen.height-150f,300f,58f);
    }

    private void DrawSealExcavationUi()
    {
        if(sealRewardMessageTime>0f)
        {
            GUI.Box(new Rect(Screen.width*0.5f-250f,42f,500f,48f),string.Empty);
            GUI.Label(new Rect(Screen.width*0.5f-235f,50f,470f,32f),sealRewardMessage,centerStyle);
        }
        if(sealExcavationState!=SealExcavationState.Planning)return;

        for(int i=0;i<sealStones.Count;i++)
        {
            SealStone stone=sealStones[i];
            Vector3 screen=cam.WorldToScreenPoint(stone.position+Vector3.up*2.1f);
            if(screen.z<=0f)continue;
            float width=176f,height=stone.blessing==SealBlessingType.None?50f:68f;
            Rect rect=new Rect(screen.x-width*0.5f,Screen.height-screen.y-height*0.5f,width,height);
            Color old=GUI.color;
            GUI.color=stone.selectionOrder>0?new Color(1f,0.55f,0.22f,0.94f):new Color(0.72f,0.62f,0.88f,0.9f);
            GUI.Box(rect,string.Empty);
            GUI.color=old;
            string order=stone.selectionOrder>0?$"[{stone.selectionOrder}] ":string.Empty;
            string blessing=stone.blessing==SealBlessingType.None?"":$"\n+ {GetSealBlessingLabel(stone.blessing)}";
            GUI.Label(new Rect(rect.x+5f,rect.y+4f,rect.width-10f,rect.height-8f),
                $"{order}{GetSealRiskLabel(stone.risk)}{blessing}",centerStyle);
        }

        Rect confirm=GetSealConfirmRect();
        bool canStart=selectedSealCount>0;
        GUI.enabled=canStart;
        if(GUI.Button(confirm,canStart?$"BEGIN {selectedSealCount} SEAL EXPLORATION":"SELECT AT LEAST 1 SEAL"))
            BeginSealExcavation();
        GUI.enabled=true;
        GUI.Label(new Rect(confirm.x-90f,confirm.y-30f,confirm.width+180f,24f),
            "Click seals in the order you want. Click again to remove.",centerStyle);
    }

    private string GetSealRiskLabel(SealRiskType risk)
    {
        switch(risk)
        {
            case SealRiskType.Vitality:return $"Enemy HP +{sealExcavationMap.enemyHealthIncrease*100f:0}%";
            case SealRiskType.Ferocity:return $"Enemy DMG +{sealExcavationMap.enemyDamageIncrease*100f:0}%";
            case SealRiskType.Movement:return $"Enemy MOV +{sealExcavationMap.enemyMoveSpeedIncrease*100f:0}%";
            case SealRiskType.AttackSpeed:return $"Enemy ATK SPD +{sealExcavationMap.enemyAttackSpeedIncrease*100f:0}%";
            case SealRiskType.Swarm:return $"Enemies +{sealExcavationMap.swarmEnemyIncrease}";
            default:return risk.ToString();
        }
    }

    private string GetSealBlessingLabel(SealBlessingType blessing)
    {
        switch(blessing)
        {
            case SealBlessingType.MovementSpeed:return $"Move speed +{sealExcavationMap.playerMoveSpeedIncrease*100f:0}%";
            case SealBlessingType.AttackDamage:return $"Attack damage +{sealExcavationMap.playerDamageIncrease*100f:0}%";
            case SealBlessingType.AttackSpeed:return $"Attack speed +{sealExcavationMap.playerAttackSpeedIncrease*100f:0}%";
            case SealBlessingType.ItemRarity:return $"Item rarity +{sealExcavationMap.itemRarityIncrease*100f:0}%";
            default:return "No player blessing";
        }
    }

    private void DrawScaleTrialChoiceUi()
    {
        Color old=GUI.color;
        GUI.color=new Color(0f,0f,0f,0.72f);
        GUI.DrawTexture(new Rect(0f,0f,Screen.width,Screen.height),Texture2D.whiteTexture);
        GUI.color=old;

        GetScaleChoiceRects(out Rect settleRect,out Rect challengeRect);
        Rect header=new Rect(settleRect.x,settleRect.y-92f,challengeRect.xMax-settleRect.x,78f);
        GUI.Box(header,string.Empty);
        GUI.Label(new Rect(header.x+15f,header.y+8f,header.width-30f,30f),"THE SCALE DEMANDS A CHOICE",titleStyle);
        GUI.Label(new Rect(header.x+15f,header.y+40f,header.width-30f,25f),"No time limit. Combat controls are locked.",centerStyle);

        DrawScaleChoiceCard(settleRect,new Color(0.08f,0.55f,0.28f),"SETTLE",$"Secure stage {scaleTrialStage} reward\nReward x{scaleTrialMap.GetRewardMultiplier(scaleTrialStage):0.##}\nCLICK TO CONFIRM");
        int nextStage=Mathf.Min(scaleTrialMap.stageCount,scaleTrialStage+1);
        float nextReward=scaleTrialMap.GetRewardMultiplier(nextStage);
        string difficulty=GetScaleTrialDifficultyPreview(nextStage);
        DrawScaleChoiceCard(challengeRect,new Color(0.65f,0.08f,0.06f),"CHALLENGE",$"{difficulty}\nNext reward x{nextReward:0.##}\nHOLD CLICK {scaleTrialMap.challengeHoldDuration:0.0}s");

        if(scaleTrialState==ScaleTrialState.Choosing)
        {
            float progress=Mathf.Clamp01(scaleChallengeHold/Mathf.Max(0.01f,scaleTrialMap.challengeHoldDuration));
            GUI.color=new Color(1f,0.18f,0.08f);
            GUI.DrawTexture(new Rect(challengeRect.x+12f,challengeRect.yMax-13f,(challengeRect.width-24f)*progress,6f),Texture2D.whiteTexture);
            GUI.color=old;
            if(scaleWaitForMouseRelease||scaleChoiceDelay>0f)
            {
                string wait=scaleWaitForMouseRelease?"RELEASE MOUSE TO ARM CHOICE":$"CHOICE READY IN {scaleChoiceDelay:0.0}s";
                GUI.Label(new Rect(settleRect.x,settleRect.yMax+10f,challengeRect.xMax-settleRect.x,28f),wait,centerStyle);
            }
        }
    }

    private void DrawScaleChoiceCard(Rect rect,Color color,string title,string description)
    {
        Color old=GUI.color;
        GUI.color=new Color(color.r,color.g,color.b,0.72f);
        GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color=old;
        GUI.Box(rect,string.Empty);
        GUI.Label(new Rect(rect.x+12f,rect.y+12f,rect.width-24f,34f),title,titleStyle);
        GUI.Label(new Rect(rect.x+18f,rect.y+56f,rect.width-36f,88f),description,centerStyle);
    }

    private string GetScaleTrialDifficultyPreview(int nextStage)
    {
        int currentEnemies=scaleTrialMap.GetEnemyCount(scaleTrialStage);
        int nextEnemies=scaleTrialMap.GetEnemyCount(nextStage);
        float currentHealth=scaleTrialMap.GetHealthMultiplier(scaleTrialStage);
        float nextHealth=scaleTrialMap.GetHealthMultiplier(nextStage);
        float currentSpeed=scaleTrialMap.GetSpeedMultiplier(scaleTrialStage);
        float nextSpeed=scaleTrialMap.GetSpeedMultiplier(nextStage);
        int healthIncrease=Mathf.RoundToInt((nextHealth/currentHealth-1f)*100f);
        int speedIncrease=Mathf.RoundToInt((nextSpeed/currentSpeed-1f)*100f);
        int enemyIncrease=nextEnemies-currentEnemies;
        return $"NEXT STAGE {nextStage}: {nextEnemies} enemies ({enemyIncrease:+#;-#;0})\nEnemy HP +{healthIncrease}%   Speed +{speedIncrease}%";
    }

    private void GetEliminationProgress(out int totalGroups,out int clearedGroups,out int activeGroups,out int encounterEnemies)
    {
        totalGroups=eliminationMap.ResolvedGroupCount;
        if(currentEliminationGroupCount>0)totalGroups=currentEliminationGroupCount;
        clearedGroups=0;
        activeGroups=0;
        encounterEnemies=0;
        for(int group=0;group<totalGroups;group++)
        {
            bool alive=false,active=false;
            for(int i=0;i<enemies.Count;i++)
            {
                Enemy enemy=enemies[i];
                if(enemy.dead||enemy.encounterGroup!=group)continue;
                alive=true;active|=enemy.encounterActivated;encounterEnemies++;
            }
            if(!alive)clearedGroups++;
            else if(active)activeGroups++;
        }
    }

    private void DrawCommandSlot(Rect rect,string label,float remaining,float cooldown,Color readyColor)
    {
        GUI.Box(rect,string.Empty);
        float ready=1f-Mathf.Clamp01(remaining/cooldown);
        Color old=GUI.color;
        GUI.color=remaining<=0f?readyColor:new Color(0.35f,0.38f,0.45f);
        GUI.DrawTexture(new Rect(rect.x+4f,rect.y+rect.height-9f,(rect.width-8f)*ready,5f),Texture2D.whiteTexture);
        GUI.color=old;
        GUI.Label(new Rect(rect.x,rect.y+4f,rect.width,22f),label,centerStyle);
        GUI.Label(new Rect(rect.x,rect.y+22f,rect.width,18f),remaining<=0f?"READY":remaining.ToString("0.0s"),centerStyle);
    }

    private void EnsureStyles()
    {
        if(titleStyle!=null)return;
        titleStyle=new GUIStyle(GUI.skin.label){fontSize=21,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};titleStyle.normal.textColor=new Color(0.35f,0.95f,1f);
        textStyle=new GUIStyle(GUI.skin.label){fontSize=14};textStyle.normal.textColor=new Color(0.92f,0.96f,1f);
        centerStyle=new GUIStyle(textStyle){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
    }

    private void OnDestroy()
    {
        EndHitStop();
        if(enemyArchetypes!=null)for(int a=0;a<enemyArchetypes.Length;a++)
        {
            DestroyAnimationMeshes(enemyArchetypes[a].moveClip);
            DestroyAnimationMeshes(enemyArchetypes[a].rushReadyClip);
            DestroyAnimationMeshes(enemyArchetypes[a].rushAttackClip);
        }
        if(materials!=null)for(int i=0;i<materials.Length;i++)Destroy(materials[i]);
        if(phaseMaterials!=null)for(int i=0;i<phaseMaterials.Length;i++)Destroy(phaseMaterials[i]);
        if(voxelMeshMaterial!=null)Destroy(voxelMeshMaterial);
        if(voxelPaletteTexture!=null)Destroy(voxelPaletteTexture);
        if(voxelDamageMasks!=null)Destroy(voxelDamageMasks);
        for(int i=0;i<skillSignalMaterials.Length;i++)if(skillSignalMaterials[i]!=null)Destroy(skillSignalMaterials[i]);
        if(crackMaterial!=null)Destroy(crackMaterial);if(crackRingMesh!=null)Destroy(crackRingMesh);
        if(attackMaterial!=null)Destroy(attackMaterial);
        for(int i=0;i<eliminationMaterials.Count;i++)if(eliminationMaterials[i]!=null)Destroy(eliminationMaterials[i]);
        foreach(Stack<GameObject> pool in modelVisualPools.Values)
        {
            while(pool.Count>0)
            {
                GameObject visual=pool.Pop();
                if(visual!=null)Destroy(visual);
            }
        }
        modelVisualPools.Clear();
        modelVisualArchetypes.Clear();
        modelBaseScales.Clear();
    }

    private void DestroyAnimationMeshes(EnemyAnimationClip clip)
    {
        if(clip?.meshes==null)return;
        for(int i=0;i<clip.meshes.Length;i++)if(clip.meshes[i]!=null)Destroy(clip.meshes[i]);
    }
}
