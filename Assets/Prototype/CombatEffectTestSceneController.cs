using System;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombatEffectTestConfiguration
{
    public static readonly List<string> AllyMonsterIds=new List<string>();
    public static readonly List<string> EnemyMonsterIds=new List<string>();
    public static bool Active;
    public static string RegionId=HunterRegionCatalog.FirstRegionId;
    public static string AllyMonsterId="velos";
    public static string EnemyMonsterId="ironkuma";
    public static string AllyPersonalityId="vengeful";
    public static string EnemyPersonalityId="vengeful";
    public static int AllyCount=1;
    public static int EnemyCount=2;
    public static ExpeditionMonsterRank AllyRank=ExpeditionMonsterRank.Rank3;
    public static ExpeditionMonsterRank EnemyRank=ExpeditionMonsterRank.Rank2;
    public static bool AllyTranscended;
    public static bool EnemyTranscended;
    public static bool MixedRank4v8;
    public static string AllyRank2TraitId="";
    public static string AllyRank3TraitId="";
    public static string EnemyRank2TraitId="";
    public static string EnemyRank3TraitId="";
#if UNITY_EDITOR
    public static bool ReadabilitySpeciesTraits;
#endif
    private static bool startRequested;

    public static void RequestStart(){startRequested=true;}

    public static bool ConsumeStartRequest()
    {
        bool requested=startRequested;
        startRequested=false;
        return requested;
    }

    public static void ClearStartRequest(){startRequested=false;}

    public static PlayerProfileSave CreateIsolatedProfile() => new PlayerProfileSave
    {
        profileId="combat_effect_test",
        firstExpeditionGuideInitialized=true,
        firstExpeditionGuideVersion=2,
        firstExpeditionGuideStep=8,
        firstExpeditionCaptureGuideStep=8
    };

    public static List<string> ConfiguredMonsterIds(bool ally)
    {
        List<string> roster=ally?AllyMonsterIds:EnemyMonsterIds;
        if(roster.Count>0)return roster.Take(8).ToList();
        return Enumerable.Repeat(ally?AllyMonsterId:EnemyMonsterId,
            Mathf.Clamp(ally?AllyCount:EnemyCount,1,8)).ToList();
    }

    public static void ApplyTestProgression(
        ExpeditionMonsterStateSave monster,bool ally,int enemyIndex=-1,MonsterDefinition definition=null)
    {
        if(monster==null)return;
        if(MixedRank4v8
#if UNITY_EDITOR
           ||CombatReadabilityPreview.Active&&ReadabilitySpeciesTraits
#endif
          )
        {
            monster.transcended=!MixedRank4v8&&(ally?AllyTranscended:EnemyTranscended);
            monster.rank=ally?AllyRank:EnemyRank;
            if(MixedRank4v8)monster.rank=ally||enemyIndex<2?ExpeditionMonsterRank.Rank3:
                enemyIndex<5?ExpeditionMonsterRank.Rank2:ExpeditionMonsterRank.Rank1;
            if(monster.transcended)monster.rank=ExpeditionMonsterRank.Rank3;
            monster.growthTotal=monster.transcended?ExpeditionGrowthSystem.MaximumGrowth:monster.rank>=ExpeditionMonsterRank.Rank3?9:
                monster.rank>=ExpeditionMonsterRank.Rank2?3:0;
            monster.rank2TraitId=monster.rank>=ExpeditionMonsterRank.Rank2
                ?ExpeditionTraitProgression.FixedTrait(definition,2).id:"";
            monster.rank3TraitId=monster.rank>=ExpeditionMonsterRank.Rank3
                ?ExpeditionTraitProgression.FixedTrait(definition,3).id:"";
            return;
        }
        bool transcended=ally?AllyTranscended:EnemyTranscended;
        monster.rank=transcended?ExpeditionMonsterRank.Rank3:
            ally?AllyRank:EnemyRank;
        monster.transcended=transcended;
        monster.growthTotal=transcended?ExpeditionGrowthSystem.MaximumGrowth:
            monster.rank>=ExpeditionMonsterRank.Rank3?9:
            monster.rank>=ExpeditionMonsterRank.Rank2?3:0;
        monster.rank2TraitId=ally?AllyRank2TraitId:EnemyRank2TraitId;
        monster.rank3TraitId=ally?AllyRank3TraitId:EnemyRank3TraitId;
    }

    public static void AddSelectedTraits(ICollection<string> traits,bool ally)
    {
        if(traits==null||MixedRank4v8)return;
#if UNITY_EDITOR
        if(CombatReadabilityPreview.Active&&ReadabilitySpeciesTraits)return;
#endif
        ExpeditionMonsterRank rank=ally?AllyRank:EnemyRank;
        if(rank>=ExpeditionMonsterRank.Rank2)
            AddUnique(traits,ally?AllyRank2TraitId:EnemyRank2TraitId);
        if(rank>=ExpeditionMonsterRank.Rank3)
            AddUnique(traits,ally?AllyRank3TraitId:EnemyRank3TraitId);
    }

    private static void AddUnique(ICollection<string> traits,string traitId)
    {
        if(string.IsNullOrWhiteSpace(traitId)||traits.Contains(traitId))return;
        traits.Add(traitId);
    }

    public static void ApplyTestBaseline(
        OwnedMonsterSave monster,string personalityId)
    {
        if(monster==null)return;
        monster.hpMultiplier=1f;
        monster.damageMultiplier=1f;
        monster.attackSpeedMultiplier=1f;
        monster.defenseMultiplier=1f;
        monster.statusResistanceMultiplier=1f;
        monster.moveSpeedMultiplier=1f;
        monster.isShiny=false;
        monster.personalityId=PersonalityCombatPolicy.IsSupported(personalityId)
            ?personalityId:"vengeful";
    }
}

[DefaultExecutionOrder(-1000)]
public sealed partial class CombatEffectTestSceneController:MonoBehaviour
{
    public const string BattleSceneName="MonsterAutoBattleTilePrototype";
    private readonly List<MonsterDefinition> families=new List<MonsterDefinition>();
    private readonly Dictionary<string,List<MonsterDefinition>> familyMembers=
        new Dictionary<string,List<MonsterDefinition>>(StringComparer.OrdinalIgnoreCase);
    private readonly List<PersonalityMasterRow> personalities=
        new List<PersonalityMasterRow>();
    private int allyIndex;
    private int enemyIndex;
    private int allyMemberIndex;
    private int enemyMemberIndex;
    private int allyPersonalityIndex;
    private int enemyPersonalityIndex;
    private int allyCount=1;
    private int enemyCount=1;
    private readonly List<string> allyRoster=new List<string>();
    private readonly List<string> enemyRoster=new List<string>();
    private ExpeditionMonsterRank allyRank=ExpeditionMonsterRank.Rank1;
    private ExpeditionMonsterRank enemyRank=ExpeditionMonsterRank.Rank1;
    private bool allyTranscended;
    private bool enemyTranscended;
    private bool mixedRank4v8;
    private string allyRank2TraitId="";
    private string allyRank3TraitId="";
    private string enemyRank2TraitId="";
    private string enemyRank3TraitId="";
    private int openMonsterList;
    private int openTraitList;
    private static Rect activePopupRect;
    private Vector2 traitScroll;
    private bool deploymentPrepared;
    private MonsterAutoBattlePrototype activeBattle;
    private GameAudio testAudio;
    private AudioListener bootstrapListener;
    private float previousListenerVolume;
    private bool allySoundsOnly;
    private static bool sidebarCollapsed;
    private int sidebarTab;
    private Vector2 sidebarScroll;
    private string evolutionPreviewMessage="아군을 배치한 뒤 진화 효과를 확인하세요.";
    private static readonly string[] SidebarTabs={"편성","사운드","점검"};
    private static readonly string[] RankLabels={"1등급","2등급","3등급","4등급"};
    private static readonly string[] SoundPresetIds={"velos","lilas","kingmore","holydeer"};
    private static readonly string[] SoundPresetNames={"랩터","고릴라","버섯","사슴"};
    private static readonly string[] SoundCueNames={"공격","명중","시전","발현","지속","사망"};
    public static bool MuteEnemySounds { get; private set; }

    private void Awake()
    {
        sidebarCollapsed=false;
#if UNITY_EDITOR
        CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
#endif
        bootstrapListener=GetComponent<AudioListener>();
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        if(database==null)throw new InvalidOperationException("Monster database missing.");
        families.AddRange(database.monsters
            .Where(definition=>definition!=null&&definition.enabled&&
                !string.IsNullOrWhiteSpace(definition.familyId)&&
                !string.IsNullOrWhiteSpace(definition.resourcePath))
            .GroupBy(definition=>definition.familyId,StringComparer.OrdinalIgnoreCase)
            .Select(group=>group.OrderBy(definition=>definition.serialNumber).First())
            .OrderBy(definition=>definition.serialNumber));
        if(families.Count==0)throw new InvalidOperationException("No enabled monster families.");
        for(int i=0;i<families.Count;i++)
            familyMembers[families[i].familyId]=
                GetFamilyMembers(database,families[i].familyId);
        GameMasterDatabase master=GameMasterData.Database;
        if(master!=null&&master.personalities!=null)
            personalities.AddRange(master.personalities.Where(row=>row!=null&&row.enabled&&
                PersonalityCombatPolicy.IsFeatured(row.id)).OrderBy(row=>row.priority));
        if(personalities.Count==0)
            throw new InvalidOperationException("No enabled personalities.");

        ResolveSelection(
            CombatEffectTestConfiguration.AllyMonsterId,0,
            out allyIndex,out allyMemberIndex);
        ResolveSelection(
            CombatEffectTestConfiguration.EnemyMonsterId,allyIndex,
            out enemyIndex,out enemyMemberIndex);
        allyPersonalityIndex=PersonalityIndexOf(
            CombatEffectTestConfiguration.AllyPersonalityId,0);
        enemyPersonalityIndex=PersonalityIndexOf(
            CombatEffectTestConfiguration.EnemyPersonalityId,allyPersonalityIndex);
        allyCount=Mathf.Clamp(CombatEffectTestConfiguration.AllyCount,1,8);
        enemyCount=Mathf.Clamp(CombatEffectTestConfiguration.EnemyCount,1,8);
        allyRoster.AddRange(CombatEffectTestConfiguration.AllyMonsterIds.Take(8));
        enemyRoster.AddRange(CombatEffectTestConfiguration.EnemyMonsterIds.Take(8));
        allyRank=CombatEffectTestConfiguration.AllyRank;
        enemyRank=CombatEffectTestConfiguration.EnemyRank;
        allyTranscended=CombatEffectTestConfiguration.AllyTranscended;
        enemyTranscended=CombatEffectTestConfiguration.EnemyTranscended;
        mixedRank4v8=CombatEffectTestConfiguration.MixedRank4v8;
        allyRank2TraitId=CombatEffectTestConfiguration.AllyRank2TraitId;
        allyRank3TraitId=CombatEffectTestConfiguration.AllyRank3TraitId;
        enemyRank2TraitId=CombatEffectTestConfiguration.EnemyRank2TraitId;
        enemyRank3TraitId=CombatEffectTestConfiguration.EnemyRank3TraitId;
        EnsureTraitSelections(1);
        EnsureTraitSelections(2);
        ApplyConfiguration();
        previousListenerVolume=AudioListener.volume;
        AudioListener.volume=GameUserSettings.Load().masterVolume;
        testAudio=GameAudio.EnableTestAudio(gameObject);
        allySoundsOnly=false;
        MuteEnemySounds=allySoundsOnly;
        Time.timeScale=1f;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        deploymentPrepared=true;
        if(SceneManager.GetActiveScene().name!=BattleSceneName)
            SceneManager.LoadScene(BattleSceneName,LoadSceneMode.Single);
        else
            activeBattle=FindObjectOfType<MonsterAutoBattlePrototype>();
    }

    private void OnEnable(){SceneManager.sceneLoaded+=HandleSceneLoaded;}

    private void OnDisable(){SceneManager.sceneLoaded-=HandleSceneLoaded;}

    private void HandleSceneLoaded(Scene scene,LoadSceneMode mode)
    {
        activeBattle=scene.name==BattleSceneName
            ?FindObjectOfType<MonsterAutoBattlePrototype>()
            :null;
        if(testAudio!=null)testAudio.StopCombatSounds();
        if(activeBattle==null||Camera.main==null)return;
        if(Camera.main.GetComponent<AudioListener>()==null)
            Camera.main.gameObject.AddComponent<AudioListener>();
        if(bootstrapListener!=null)bootstrapListener.enabled=false;
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        CombatReadabilityPreview.Reset();
        CombatEffectTestConfiguration.ReadabilitySpeciesTraits=false;
#endif
        activePopupRect=new Rect();sidebarCollapsed=false;
        if(testAudio!=null)testAudio.EndTestAudio();
        AudioListener.volume=previousListenerVolume;
        MuteEnemySounds=false;
        CombatEffectTestConfiguration.Active=false;
        CombatEffectTestConfiguration.ClearStartRequest();
    }

    private void Update()
    {
        if(openMonsterList!=0||openTraitList!=0)return;
        if(!Input.GetKeyDown(KeyCode.R))return;
        if(deploymentPrepared)StartPreparedBattle();
        else PrepareDeployment();
    }

    public static List<MonsterDefinition> GetFamilyMembers(
        MonsterDatabase database,string familyId)
    {
        if(database==null||database.monsters==null)
            return new List<MonsterDefinition>();
        return database.monsters.Where(definition=>definition!=null&&
                definition.enabled&&!string.IsNullOrWhiteSpace(definition.resourcePath)&&
                string.Equals(definition.familyId,familyId,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(definition=>definition.serialNumber).ToList();
    }

    public static List<string> RandomDistinctMonsterIds(
        IEnumerable<string> monsterIds,int count,int seed)
    {
        List<string> pool=monsterIds.Where(id=>!string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToList();
        System.Random random=new System.Random(seed);
        for(int i=pool.Count-1;i>0;i--)
        {
            int swap=random.Next(i+1);
            string value=pool[i];pool[i]=pool[swap];pool[swap]=value;
        }
        return pool.Take(Mathf.Clamp(count,0,pool.Count)).ToList();
    }

    private void ResolveSelection(
        string monsterId,int fallback,out int familyIndex,out int memberIndex)
    {
        familyIndex=families.FindIndex(family=>familyMembers[family.familyId]
            .Any(member=>string.Equals(
                member.monsterId,monsterId,StringComparison.Ordinal)));
        if(familyIndex<0)familyIndex=Mathf.Clamp(fallback,0,families.Count-1);
        List<MonsterDefinition> members=familyMembers[families[familyIndex].familyId];
        memberIndex=members.FindIndex(member=>string.Equals(
            member.monsterId,monsterId,StringComparison.Ordinal));
        if(memberIndex<0)memberIndex=0;
    }

    private MonsterDefinition SelectedMonster(int familyIndex,int memberIndex)
    {
        List<MonsterDefinition> members=familyMembers[families[familyIndex].familyId];
        return members[Mathf.Clamp(memberIndex,0,members.Count-1)];
    }

    private void EnsureTraitSelections(int side)
    {
        MonsterDefinition definition=side==1
            ?SelectedMonster(allyIndex,allyMemberIndex)
            :SelectedMonster(enemyIndex,enemyMemberIndex);
        if(side==1)
        {
            allyRank2TraitId=ResolveTrait(definition,false,allyRank2TraitId);
            allyRank3TraitId=ResolveTrait(definition,true,allyRank3TraitId);
        }
        else
        {
            enemyRank2TraitId=ResolveTrait(definition,false,enemyRank2TraitId);
            enemyRank3TraitId=ResolveTrait(definition,true,enemyRank3TraitId);
        }
    }

    private static string ResolveTrait(
        MonsterDefinition definition,bool rank3,string selectedId)
    {
        IReadOnlyList<MonsterTraitDefinition> choices=
            MonsterTraitCatalog.TestSelectable(definition,rank3);
        MonsterTraitDefinition selected=choices.FirstOrDefault(trait=>
            string.Equals(trait.id,selectedId,StringComparison.Ordinal));
        return selected!=null?selected.id:choices.Count==0?"":choices[0].id;
    }

    private static string TraitName(string traitId)
    {
        TraitMasterRow row=GameMasterData.Database==null?null:
            GameMasterData.Database.traits.FirstOrDefault(trait=>trait!=null&&
                string.Equals(trait.id,traitId,StringComparison.Ordinal));
        return row==null?(string.IsNullOrWhiteSpace(traitId)?"-":traitId):row.displayName;
    }

    private int PersonalityIndexOf(string personalityId,int fallback)
    {
        int index=personalities.FindIndex(row=>string.Equals(
            row.id,personalityId,StringComparison.Ordinal));
        return index<0?fallback:index;
    }

    private void ApplyConfiguration()
    {
        deploymentPrepared=false;
        CombatEffectTestConfiguration.Active=true;
        CombatEffectTestConfiguration.AllyMonsterId=
            SelectedMonster(allyIndex,allyMemberIndex).monsterId;
        CombatEffectTestConfiguration.EnemyMonsterId=
            SelectedMonster(enemyIndex,enemyMemberIndex).monsterId;
        CombatEffectTestConfiguration.AllyPersonalityId=
            personalities[allyPersonalityIndex].id;
        CombatEffectTestConfiguration.EnemyPersonalityId=
            personalities[enemyPersonalityIndex].id;
        CombatEffectTestConfiguration.AllyCount=allyCount;
        CombatEffectTestConfiguration.EnemyCount=enemyCount;
        CombatEffectTestConfiguration.AllyMonsterIds.Clear();
        CombatEffectTestConfiguration.AllyMonsterIds.AddRange(allyRoster);
        CombatEffectTestConfiguration.EnemyMonsterIds.Clear();
        CombatEffectTestConfiguration.EnemyMonsterIds.AddRange(enemyRoster);
        CombatEffectTestConfiguration.AllyRank=allyRank;
        CombatEffectTestConfiguration.EnemyRank=enemyRank;
        CombatEffectTestConfiguration.AllyTranscended=allyTranscended;
        CombatEffectTestConfiguration.EnemyTranscended=enemyTranscended;
        CombatEffectTestConfiguration.MixedRank4v8=mixedRank4v8;
        CombatEffectTestConfiguration.AllyRank2TraitId=allyRank2TraitId;
        CombatEffectTestConfiguration.AllyRank3TraitId=allyRank3TraitId;
        CombatEffectTestConfiguration.EnemyRank2TraitId=enemyRank2TraitId;
        CombatEffectTestConfiguration.EnemyRank3TraitId=enemyRank3TraitId;
    }

    private void ApplyManualConfiguration()
    {
#if UNITY_EDITOR
        ResetReadabilityScenario();
#endif
        if(mixedRank4v8)
        {
            mixedRank4v8=false;
            EnsureTraitSelections(1);EnsureTraitSelections(2);
        }
        ApplyConfiguration();
    }

    private void PrepareDeployment()
    {
        openMonsterList=0;
        openTraitList=0;
        ApplyConfiguration();
        deploymentPrepared=true;
        CombatEffectTestConfiguration.ClearStartRequest();
        SceneManager.LoadScene(BattleSceneName,LoadSceneMode.Single);
    }

    private void StartPreparedBattle()
    {
        if(activeBattle!=null&&activeBattle.StartPreparedCombatEffectTestBattle())
        {
            sidebarCollapsed=true;
            openMonsterList=0;
            openTraitList=0;
        }
    }

    public static Rect TestSidebarRect(float width,float height,bool collapsed=false)=>
        new Rect(8f,8f,Mathf.Max(150f,Mathf.Min(320f,width*.24f)),collapsed?32f:Mathf.Max(32f,height-16f));

    public static Rect TestSidebarContentRect(float width,float height)
    {
        Rect panel=TestSidebarRect(width,height);
        return new Rect(panel.x+8f,panel.y+222f,panel.width-16f,Mathf.Max(20f,panel.height-230f));
    }

    public static Rect EvolutionPreviewButtonRect(Rect panel,int rank)
    {
        float width=(panel.width-24f)/3f;
        return new Rect(panel.x+8f+(rank-2)*(width+4f),panel.y+118f,width,28f);
    }

    private void OnGUI()
    {
        Rect panel=TestSidebarRect(Screen.width,Screen.height,sidebarCollapsed);
        bool popupOpen=openMonsterList!=0||openTraitList!=0;
        Event input=Event.current;
        if(popupOpen&&((input.type==EventType.MouseDown&&!activePopupRect.Contains(input.mousePosition))||
                      (input.type==EventType.KeyDown&&input.keyCode==KeyCode.Escape)))
        {
            openMonsterList=openTraitList=0;activePopupRect=new Rect();
            input.Use();
        }
        bool enabled=GUI.enabled;int depth=GUI.depth;GUIStyle label=GUI.skin.label;
        try
        {
            GUI.depth=-100;
            GUI.skin.label=BombBrickUiTheme.BodyStyle(10,TextAnchor.MiddleLeft);
            GUI.enabled=enabled&&!popupOpen;
            PixelBox(panel,"",true);
            GUI.Label(new Rect(panel.x+10f,panel.y+4f,panel.width-86f,24f),"전투 테스트",
                BombBrickUiTheme.TitleStyle(12));
            if(PixelButton(new Rect(panel.xMax-70f,panel.y+4f,62f,24f),sidebarCollapsed?"펼치기":"접기"))
            {
                sidebarCollapsed=!sidebarCollapsed;openMonsterList=openTraitList=0;activePopupRect=new Rect();
            }
            if(sidebarCollapsed)return;
            bool running=activeBattle!=null&&activeBattle.CombatEffectTestBattleRunning;
            float x=panel.x+8f,w=panel.width-16f,y=panel.y;
            if(PixelButton(new Rect(x,y+36f,(w-4f)*.5f,28f),"배치 / 재배치"))
            {evolutionPreviewMessage="선택한 편성 배치 중…";PrepareDeployment();}
            GUI.enabled=enabled&&!popupOpen&&deploymentPrepared&&!running;
            if(PixelButton(new Rect(x+(w+4f)*.5f,y+36f,(w-4f)*.5f,28f),"전투 [R]"))StartPreparedBattle();
            GUI.enabled=enabled&&!popupOpen;
            for(int i=0;i<3;i++)
                if(PixelButton(new Rect(x+i*(w+4f)/3f,y+68f,(w-8f)/3f,24f),i==0?"0.5x":i==1?"1x":"2x"))
                    Time.timeScale=i==0?.5f:i==1?1f:2f;
            GUI.Label(new Rect(x,y+96f,w,20f),"진화 미리보기 · "+SelectedMonster(allyIndex,allyMemberIndex).displayName);
            bool ready=deploymentPrepared&&activeBattle!=null&&activeBattle.IsSceneReady&&!running;
            GUI.enabled=enabled&&!popupOpen&&ready;
            for(int rank=2;rank<=4;rank++)
                if(PixelButton(EvolutionPreviewButtonRect(panel,rank),rank+"등급"))PreviewEvolution(rank);
            GUI.enabled=enabled&&!popupOpen;
            string status=running?"진화 효과는 전투 전 배치 중에 확인할 수 있어요.":
                !deploymentPrepared?"선택을 바꿨다면 먼저 [배치 / 재배치]를 누르세요.":
                activeBattle==null||!activeBattle.IsSceneReady?"전장 준비 중…":evolutionPreviewMessage;
            GUI.Label(new Rect(x,y+150f,w,34f),status);
            for(int i=0;i<SidebarTabs.Length;i++)
                if(PixelButton(new Rect(x+i*(w+4f)/3f,y+188f,(w-8f)/3f,26f),
                    (sidebarTab==i?"● ":"")+SidebarTabs[i]))
                {sidebarTab=i;sidebarScroll=Vector2.zero;}

            Rect viewport=TestSidebarContentRect(Screen.width,Screen.height);
            float contentWidth=viewport.width-18f;
            float contentHeight=sidebarTab==0?730f:sidebarTab==1?384f:270f;
            sidebarScroll=GUI.BeginScrollView(viewport,sidebarScroll,
                new Rect(0f,0f,contentWidth,Mathf.Max(viewport.height,contentHeight)));
            if(sidebarTab==0)
            {
                GUI.enabled=enabled&&!popupOpen&&!running;
                DrawFormationControls(contentWidth);
            }
            else if(sidebarTab==1)DrawSoundTest(new Rect(0f,0f,contentWidth,384f));
#if UNITY_EDITOR
            else DrawReadabilityChecks(new Rect(0f,0f,contentWidth,270f));
#endif
            GUI.EndScrollView();
            GUI.enabled=enabled;
            DrawMonsterList();DrawTraitList();
            if(openMonsterList==0&&openTraitList==0)activePopupRect=new Rect();
        }
        finally{GUI.enabled=enabled;GUI.skin.label=label;GUI.depth=depth;}
    }

    private void DrawFormationControls(float width)
    {
        if(PixelButton(new Rect(0f,0f,width,26f),"맵: "+HunterRegionCatalog.Get(CombatEffectTestConfiguration.RegionId).displayName+" ▶"))
        {
            var regions=HunterRegionCatalog.All;int next=0;
            for(int i=0;i<regions.Count;i++)if(regions[i].id==CombatEffectTestConfiguration.RegionId){next=(i+1)%regions.Count;break;}
            CombatEffectTestConfiguration.RegionId=regions[next].id;PrepareDeployment();
        }
        if(PixelButton(new Rect(0f,32f,width,26f),"랜덤 8 VS 8 · 1등급"))PrepareRandomRank1Battle();
        if(PixelButton(new Rect(0f,64f,width,26f),"4 VS 8 · 3등급 대 혼합"))PrepareRandomMixed4v8Battle();
        DrawSide("아군",ref allyIndex,ref allyMemberIndex,ref allyCount,
            ref allyPersonalityIndex,ref allyRank,ref allyTranscended,
            ref allyRank2TraitId,ref allyRank3TraitId,allyRoster,104f,1,width);
        DrawSide("적군",ref enemyIndex,ref enemyMemberIndex,ref enemyCount,
            ref enemyPersonalityIndex,ref enemyRank,ref enemyTranscended,
            ref enemyRank2TraitId,ref enemyRank3TraitId,enemyRoster,390f,2,width);
        GUI.Label(new Rect(0f,676f,width,48f),"몬스터 추가 → 배치 → 위치 조정 → 전투 [R]\n스크롤해 양 진영의 편성을 변경하세요.");
    }

    public bool PreviewEvolution(int rank)
    {
        if(!deploymentPrepared||activeBattle==null||!activeBattle.IsSceneReady||activeBattle.CombatEffectTestBattleRunning)
        {evolutionPreviewMessage="먼저 [배치 / 재배치]로 아군을 준비하세요.";return false;}
        var definition=SelectedMonster(allyIndex,allyMemberIndex);
        bool played=activeBattle.PreviewCombatEffectTestEvolution(definition,rank);
        evolutionPreviewMessage=played?definition.displayName+" · "+rank+"등급 진화 효과":
            definition.displayName+"가 전장에 없어요. 아군에 추가하고 배치하세요.";
        return played;
    }

    public void PrepareRandomRank1Battle()
    {
#if UNITY_EDITOR
        ResetReadabilityScenario();
#endif
        List<string> randomized=RandomDistinctMonsterIds(
            familyMembers.Values.SelectMany(members=>members)
                .Select(monster=>monster.monsterId),16,Guid.NewGuid().GetHashCode());
        if(randomized.Count<16)return;
        allyRoster.Clear();allyRoster.AddRange(randomized.Take(8));
        enemyRoster.Clear();enemyRoster.AddRange(randomized.Skip(8).Take(8));
        allyCount=enemyCount=8;
        allyRank=enemyRank=ExpeditionMonsterRank.Rank1;
        mixedRank4v8=false;
        allyTranscended=enemyTranscended=false;
        allyRank2TraitId=allyRank3TraitId=enemyRank2TraitId=enemyRank3TraitId="";
        ResolveSelection(allyRoster[0],0,out allyIndex,out allyMemberIndex);
        ResolveSelection(enemyRoster[0],0,out enemyIndex,out enemyMemberIndex);
        Time.timeScale=1f;
        PrepareDeployment();
    }

    public void PrepareRandomMixed4v8Battle()
    {
#if UNITY_EDITOR
        ResetReadabilityScenario();
#endif
        List<string> randomized=RandomDistinctMonsterIds(
            familyMembers.Values.SelectMany(members=>members)
                .Select(monster=>monster.monsterId),12,Guid.NewGuid().GetHashCode());
        if(randomized.Count<12)return;
        allyRoster.Clear();allyRoster.AddRange(randomized.Take(4));
        enemyRoster.Clear();enemyRoster.AddRange(randomized.Skip(4));
        allyCount=4;enemyCount=8;
        allyRank=ExpeditionMonsterRank.Rank3;enemyRank=ExpeditionMonsterRank.Rank1;
        allyTranscended=enemyTranscended=false;
        allyRank2TraitId=allyRank3TraitId=enemyRank2TraitId=enemyRank3TraitId="";
        mixedRank4v8=true;
        ResolveSelection(allyRoster[0],0,out allyIndex,out allyMemberIndex);
        ResolveSelection(enemyRoster[0],0,out enemyIndex,out enemyMemberIndex);
        Time.timeScale=1f;
        PrepareDeployment();
    }

    public void SelectSoundPreset(int preset)
    {
#if UNITY_EDITOR
        ResetReadabilityScenario();
#endif
        preset=Mathf.Clamp(preset,0,SoundPresetIds.Length-1);
        ResolveSelection(SoundPresetIds[preset],0,out allyIndex,out allyMemberIndex);
        ResolveSelection("ironkuma",0,out enemyIndex,out enemyMemberIndex);
        allyRoster.Clear();enemyRoster.Clear();
        allyCount=1;enemyCount=2;
        // A sanctuary needs an ally to support, not a solo damage test.
        if(preset==3){allyRoster.Add("holydeer");allyRoster.Add("velos");}
        allyRank=ExpeditionMonsterRank.Rank3;
        enemyRank=ExpeditionMonsterRank.Rank2;
        mixedRank4v8=false;
        allyTranscended=enemyTranscended=false;
        allyRank2TraitId=allyRank3TraitId=enemyRank2TraitId=enemyRank3TraitId="";
        EnsureTraitSelections(1);EnsureTraitSelections(2);
        Time.timeScale=1f;
        PrepareDeployment();
    }

    public static bool PointerOverControls(Vector2 guiPoint)=>CombatEffectTestConfiguration.Active&&
        (activePopupRect.width>0f||TestSidebarRect(Screen.width,Screen.height,sidebarCollapsed).Contains(guiPoint));

    private void DrawSoundTest(Rect area)
    {
        float x=area.x,y=area.y,w=area.width;
        GUI.Label(new Rect(x,y,w,20f),"SFX 비교 · BGM 꺼짐");
        for(int i=0;i<SoundPresetIds.Length;i++)
            if(PixelButton(new Rect(x+i%2*(w+4f)/2f,y+24f+i/2*28f,(w-4f)/2f,24f),SoundPresetNames[i]))SelectSoundPreset(i);
        GUI.Label(new Rect(x,y+84f,w,22f),"전투 중 재생 ON / OFF");
        for(int i=0;i<SoundCueNames.Length;i++)
        {
            var cue=(CombatSoundCue)i;bool on=testAudio!=null&&testAudio.CueEnabled(cue);
            if(PixelButton(new Rect(x+i%3*(w+4f)/3f,y+110f+i/3*28f,(w-8f)/3f,24f),
                (on?"● ":"○ ")+SoundCueNames[i])&&testAudio!=null)testAudio.ToggleCue(cue);
        }
        GUI.Label(new Rect(x,y+172f,w,22f),"선택 아군 · 미리 듣기");
        var monster=SelectedMonster(allyIndex,allyMemberIndex);
        for(int i=0;i<SoundCueNames.Length;i++)
        {
            var cue=(CombatSoundCue)i;var style=CombatSoundBank.StyleForCue(monster.familyId,monster.attackRange>1,cue);
            if(PixelButton(new Rect(x+i%3*(w+4f)/3f,y+198f+i/3*28f,(w-8f)/3f,24f),SoundCueNames[i])&&testAudio!=null)testAudio.Preview(style,cue);
        }
        if(PixelButton(new Rect(x,y+258f,w,26f),allySoundsOnly?"아군 소리만":"양 진영 소리"))
        {allySoundsOnly=!allySoundsOnly;MuteEnemySounds=allySoundsOnly;}
        if(PixelButton(new Rect(x,y+290f,w,26f),"아군 스킬 대기시간 해제")&&activeBattle!=null)activeBattle.ReadyCombatEffectTestSkills();
        GUI.Label(new Rect(x,y+324f,w,48f),testAudio==null?"오디오 준비 중":testAudio.LastCombatSound);
    }

    private void DrawSide(string label,ref int index,ref int memberIndex,ref int count,
        ref int personalityIndex,ref ExpeditionMonsterRank rank,ref bool transcended,
        ref string rank2TraitId,ref string rank3TraitId,List<string> roster,float y,int listId,float width)
    {
        const float x=0f;
        GUI.Label(new Rect(x,y,width-84f,24f),label,BombBrickUiTheme.TitleStyle(12));
        if(PixelButton(new Rect(width-82f,y,24f,24f),"−")){count=Mathf.Max(1,count-1);ApplyManualConfiguration();}
        GUI.Label(new Rect(width-54f,y,26f,24f),count.ToString());
        if(PixelButton(new Rect(width-24f,y,24f,24f),"+")){count=Mathf.Min(8,count+1);ApplyManualConfiguration();}
        if(PixelButton(new Rect(x,y+28f,26f,24f),"<"))
        {index=(index-1+families.Count)%families.Count;memberIndex=0;openMonsterList=openTraitList=0;EnsureTraitSelections(listId);ApplyManualConfiguration();}
        var selected=SelectedMonster(index,memberIndex);
        if(PixelButton(new Rect(x+30f,y+28f,width-60f,24f),selected.displayName+" ▼"))
        {openMonsterList=openMonsterList==listId?0:listId;openTraitList=0;traitScroll=Vector2.zero;}
        if(PixelButton(new Rect(width-26f,y+28f,26f,24f),">"))
        {index=(index+1)%families.Count;memberIndex=0;openMonsterList=openTraitList=0;EnsureTraitSelections(listId);ApplyManualConfiguration();}
        if(PixelButton(new Rect(x,y+56f,26f,24f),"<"))
        {personalityIndex=(personalityIndex-1+personalities.Count)%personalities.Count;ApplyManualConfiguration();}
        GUI.Label(new Rect(x+30f,y+56f,width-60f,24f),"성격: "+personalities[personalityIndex].displayName);
        if(PixelButton(new Rect(width-26f,y+56f,26f,24f),">"))
        {personalityIndex=(personalityIndex+1)%personalities.Count;ApplyManualConfiguration();}
        int grade=mixedRank4v8&&listId==2?-1:transcended?3:(int)rank-1;
        int next=GUI.Toolbar(new Rect(x,y+84f,width,24f),grade,RankLabels,BombBrickUiTheme.ButtonStyle(10,GUI.enabled));
        if(next!=grade)
        {transcended=next==3;rank=transcended?ExpeditionMonsterRank.Rank3:(ExpeditionMonsterRank)(next+1);openTraitList=0;ApplyManualConfiguration();}
        if(mixedRank4v8)
            GUI.Label(new Rect(x,y+112f,width,48f),listId==1?"개체별 고정 2·3등급 특성":"3등급 2명 · 2등급 3명 · 1등급 3명\n개체별 고정 특성");
        else if(rank==ExpeditionMonsterRank.Rank1)
            GUI.Label(new Rect(x,y+112f,width,24f),"1등급 · 추가 특성 없음");
        else
        {
            if(PixelButton(new Rect(x,y+112f,width,24f),"2등급 · "+TraitName(rank2TraitId)+" ▼"))OpenTraitList(listId,2);
            if(rank>=ExpeditionMonsterRank.Rank3&&PixelButton(new Rect(x,y+140f,width,24f),"3등급 · "+TraitName(rank3TraitId)+" ▼"))OpenTraitList(listId,3);
            if(transcended)GUI.Label(new Rect(x,y+168f,width,24f),"4등급 · "+(selected.familyId=="raptor"?"정점 포식자":FamilyTranscendenceRules.DisplayName(selected.familyId)));
        }
        float row=y+196f,cell=(width-8f)/3f;
        if(PixelButton(new Rect(x,row,cell,24f),"추가 ×"+count))
        {for(int i=0;i<count&&roster.Count<8;i++)roster.Add(selected.monsterId);ApplyManualConfiguration();}
        if(PixelButton(new Rect(x+cell+4f,row,cell,24f),"취소")&&roster.Count>0)
        {roster.RemoveAt(roster.Count-1);ApplyManualConfiguration();}
        if(PixelButton(new Rect(x+(cell+4f)*2f,row,cell,24f),"비우기")){roster.Clear();ApplyManualConfiguration();}
        GUI.Label(new Rect(x,y+224f,width,48f),(roster.Count==0?"자동 편성: ":roster.Count+" / 8 · ")+RosterLabel(roster));
    }

    private string RosterLabel(List<string> roster)
    {
        if(roster.Count==0)return "현재 선택 × 수량";
        return string.Join(", ",roster.Select(id=>
        {
            MonsterDefinition definition=families.SelectMany(family=>
                    familyMembers[family.familyId])
                .FirstOrDefault(member=>string.Equals(
                    member.monsterId,id,StringComparison.Ordinal));
            return definition==null?id:definition.displayName;
        }));
    }

    private void OpenTraitList(int side,int rank)
    {
        int list=side*10+rank;
        openTraitList=openTraitList==list?0:list;
        openMonsterList=0;
        traitScroll=Vector2.zero;
    }

    private void DrawMonsterList()
    {
        if(openMonsterList==0)return;
        int familyIndex=openMonsterList==1?allyIndex:enemyIndex;
        int selectedIndex=openMonsterList==1?allyMemberIndex:enemyMemberIndex;
        List<MonsterDefinition> members=
            familyMembers[families[familyIndex].familyId];
        Rect viewport=TestSidebarContentRect(Screen.width,Screen.height);
        float x=viewport.x,y=viewport.y,width=viewport.width;const float rowHeight=28f;
        activePopupRect=new Rect(x,y,width,Mathf.Min(viewport.height,30f+members.Count*rowHeight));
        PixelBox(activePopupRect,
            families[familyIndex].displayName+" 개체",true);
        Rect listViewport=new Rect(x+8f,y+24f,width-16f,activePopupRect.height-28f);
        Rect content=new Rect(0f,0f,width-34f,Mathf.Max(listViewport.height,members.Count*rowHeight));
        traitScroll=GUI.BeginScrollView(listViewport,traitScroll,content);
        for(int i=0;i<members.Count;i++)
        {
            MonsterDefinition member=members[i];
            string marker=i==selectedIndex?"✓  ":string.Empty;
            if(!PixelButton(new Rect(0f,i*rowHeight,content.width,24f),
                   marker+member.serialNumber+"  "+member.displayName))continue;
            if(openMonsterList==1)allyMemberIndex=i;
            else enemyMemberIndex=i;
            EnsureTraitSelections(openMonsterList);
            openMonsterList=0;
            ApplyManualConfiguration();
        }
        GUI.EndScrollView();
    }

    private void DrawTraitList()
    {
        if(openTraitList==0)return;
        int side=openTraitList/10;
        bool rank3=openTraitList%10==3;
        MonsterDefinition selected=side==1
            ?SelectedMonster(allyIndex,allyMemberIndex)
            :SelectedMonster(enemyIndex,enemyMemberIndex);
        IReadOnlyList<MonsterTraitDefinition> choices=
            MonsterTraitCatalog.TestSelectable(selected,rank3);
        Rect sidebar=TestSidebarContentRect(Screen.width,Screen.height);
        float x=sidebar.x,y=sidebar.y,width=sidebar.width;const float rowHeight=28f;
        float height=Mathf.Min(sidebar.height,Mathf.Min(360f,36f+choices.Count*rowHeight));
        activePopupRect=new Rect(x,y,width,height);
        PixelBox(activePopupRect,
            (rank3?"3등급":"2등급")+" 특성 · "+selected.familyId,true);
        Rect viewport=new Rect(x+8f,y+24f,width-16f,height-32f);
        Rect content=new Rect(0f,0f,width-34f,
            Mathf.Max(viewport.height,choices.Count*rowHeight));
        traitScroll=GUI.BeginScrollView(viewport,traitScroll,content);
        for(int i=0;i<choices.Count;i++)
        {
            MonsterTraitDefinition trait=choices[i];
            string selectedId=side==1
                ?(rank3?allyRank3TraitId:allyRank2TraitId)
                :(rank3?enemyRank3TraitId:enemyRank2TraitId);
            string marker=string.Equals(selectedId,trait.id,StringComparison.Ordinal)
                ?"✓  ":string.Empty;
            if(!PixelButton(new Rect(0f,i*rowHeight,content.width,24f),
                   marker+trait.displayName))continue;
            if(side==1)
            {
                if(rank3)allyRank3TraitId=trait.id;
                else allyRank2TraitId=trait.id;
            }
            else
            {
                if(rank3)enemyRank3TraitId=trait.id;
                else enemyRank2TraitId=trait.id;
            }
            openTraitList=0;
            ApplyManualConfiguration();
        }
        GUI.EndScrollView();
    }

    private static bool PixelButton(Rect rect,string label)
    {
        return GUI.Button(rect,label,
            BombBrickUiTheme.ButtonStyle(10,GUI.enabled));
    }

    private static void PixelBox(Rect rect,string title,bool raised=false)
    {
        BombBrickUiTheme.DrawPanel(rect,raised);
        if(!string.IsNullOrEmpty(title))
            GUI.Label(new Rect(rect.x+8f,rect.y+1f,rect.width-16f,22f),title,
                BombBrickUiTheme.TitleStyle(11,TextAnchor.MiddleCenter));
    }
}
