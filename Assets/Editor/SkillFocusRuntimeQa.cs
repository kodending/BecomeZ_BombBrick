using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Reflection and optional movie recording make this a presentation diagnostic, not a performance benchmark.
[InitializeOnLoad]
public static class SkillFocusRuntimeQa
{
    private const string Pending="SkillFocusRuntimeQa.Pending";
    static SkillFocusRuntimeQa()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
            SessionState.SetBool(Pending,false);
            EditorApplication.delayCall+=()=>Start(SessionState.GetString(Pending+".Label","rank1_full"));
        };
    }

    public static void Launch(string label)
    {
        Guard();
        if(EditorApplication.isPlaying){Start(label);return;}
        if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene before QA.");
        SessionState.SetString(Pending+".Scene",SceneManager.GetActiveScene().path);
        SessionState.SetString(Pending+".Label",label);
        SessionState.SetBool(Pending,true);
        EditorSceneManager.OpenScene(CombatEffectTestSceneSetup.ScenePath);
        EditorApplication.EnterPlaymode();
    }

    public static void Start(string label)
    {
        Guard();
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        if(UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("A skill-focus observer already exists.");
        var host=new GameObject("Skill focus observer (editor only)");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<SkillFocusRuntimeQaObserver>().Begin(label);
    }

    public static void Guard()
    {
        if(Application.dataPath!="C:/Unity/BecomeZ_BombBrick/Assets")throw new InvalidOperationException("Wrong project.");
    }
}

[DefaultExecutionOrder(10000)]
public sealed class SkillFocusRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const int Seed=20261019;
    private static readonly Type BattleType=typeof(MonsterAutoBattlePrototype);
    private static readonly Type UnitType=BattleType.GetNestedType("Unit",BindingFlags.NonPublic);
    private static readonly string[] Roster={"velos","ironkuma","geomchi","woongi","aranea","ager","kongjwi","holydeer"};
    private readonly Dictionary<FieldInfo,object> configuration=new Dictionary<FieldInfo,object>();
    private readonly StringBuilder csv=new StringBuilder("frame,realtime,unscaled,battleElapsed,timeScale,captureDelta,focus,strength,casterId,casterScale,shadeAlpha,exclamationActive,basicAttacks,skills,alive,focusStartedAt,driverMotion,driverFrame,focusElapsed,castPending,casterSkillUses,casterCooldown,casterSkillTimer,sourceOrder,foregroundOrder,foregroundEnabled,casterHudVisible,casterBodyTop,casterBarBottom,casterHeadGap,frameMinimumHeadGap,frameMaximumHeadGap,normalHudSamples,hiddenCasterHudSamples,unexpectedCasterHudSamples,unexpectedHiddenOtherHudSamples,sourceSortingLayer,foregroundSortingLayer,casterVisualOverlap,foregroundFrontmost,sourceGameObjectLayer,foregroundGameObjectLayer,casterOutlined,otherOutlinedCount\n");
    private readonly List<FocusEvent> events=new List<FocusEvent>();
    private readonly Dictionary<object,int> previousSkillUses=new Dictionary<object,int>();
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private string output,label;
    private bool prepared,seenBattle,finished,rank3,captureClip,wasFocus,capturedNeutral,capturedFocused,hudScenario;
    private float originalTimeScale,originalCaptureDelta,largestSampleDt;
    private double timeout,startedAt,previousSample;
    private int samples,hitStopSamples,slowSamples,normalSamples,actors;
    private FocusEvent current;
    private object currentCaster;
    private int clipFrames,clipWidth,clipHeight;
    private readonly StringBuilder clipTimes=new StringBuilder("frame,unscaled,battleElapsed,timeScale,deltaTime,realtime\n");
    private MethodInfo spriteBodyGuiRect,unitWorldHudRect;
    private int normalHudSamples,hiddenCasterHudSamples,unexpectedCasterHudSamples,unexpectedHiddenOtherHudSamples,visualOverlapSamples,foregroundOrderViolations,otherOutlineViolations,casterOutlineMissingSamples;
    private float minimumHeadGap=float.PositiveInfinity,maximumHeadGap=float.NegativeInfinity;

    [Serializable] private sealed class FocusEvent
    {
        public float startedAt,endedAt,seconds,maximumStrength,maximumCasterScale,maximumShadeAlpha;
        public int casterId=-1,hitStopSamples,slowSamples,skillsAtStart,skillsAtEnd;
        public int casterSkillsAtStart,casterSkillsAtEnd,castBeforeReadySamples;
        public float firstCommitElapsed=-1f;
        public bool exclamationSeen,casterChanged,pendingSeen,foregroundSeen;
    }
    [Serializable] private sealed class Report
    {
        public string label,result,movie;
        public string limitation="Editor actual production battle, isolated profile and fixed seed; sampled presentation state. Optional native PNG capture has extra overhead and is resampled against the real unscaled clock, not a performance benchmark. No skill or focus was forced.";
        public int seed,actors,samples,hitStopSamples,slowSamples,normalSamples,basicAttacks,skills,playerAlive,enemyAlive;
        public float realSeconds,battleSeconds,largestSampleDt,finalTimeScale,finalCaptureDelta;
        public bool focusActiveAtEnd;
        public int normalHudSamples,hiddenCasterHudSamples,unexpectedCasterHudSamples,unexpectedHiddenOtherHudSamples,visualOverlapSamples,foregroundOrderViolations,otherOutlineViolations,casterOutlineMissingSamples;
        public float minimumHeadGap,maximumHeadGap;
        public string[] participants;
        public FocusEvent[] focusEvents;
    }

    public void Begin(string requestedLabel)
    {
        SkillFocusRuntimeQa.Guard();
        if(string.IsNullOrWhiteSpace(requestedLabel)||requestedLabel.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))
            throw new ArgumentException("Invalid QA label.");
        label=requestedLabel;rank3=label.StartsWith("rank3",StringComparison.Ordinal);captureClip=label.EndsWith("_clip",StringComparison.Ordinal);
        hudScenario=label.Contains("_hud_");
        output=Path.GetFullPath(Path.Combine(hudScenario?"Logs/SkillFocusHudQA/20261019":"Logs/SkillFocusQA/20261019",label));
        if(Directory.Exists(output))throw new IOException("QA output already exists.");
        Directory.CreateDirectory(output);
        if(hudScenario)
        {
            spriteBodyGuiRect=BattleType.GetMethod("TryGetSpriteBodyGuiRect",BindingFlags.Static|BindingFlags.Public);
            unitWorldHudRect=BattleType.GetMethod("TryGetUnitWorldHudRect",Private);
            if(spriteBodyGuiRect==null||unitWorldHudRect==null)throw new InvalidOperationException("Production HUD anchor contract missing.");
        }
        if(captureClip){Directory.CreateDirectory(Path.Combine(output,"Frames"));StartCoroutine(CaptureClipFrames());}
        foreach(var field in typeof(CombatEffectTestConfiguration).GetFields(BindingFlags.Public|BindingFlags.Static))
            configuration.Add(field,field.GetValue(null) is IList list?list.Cast<object>().ToArray():field.GetValue(null));
        originalTimeScale=Time.timeScale;originalCaptureDelta=Time.captureDeltaTime;
        Time.timeScale=1f;Time.captureDeltaTime=0f;
        CombatEffectTestConfiguration.Active=true;
        CombatEffectTestConfiguration.RegionId=HunterRegionCatalog.FirstRegionId;
        CombatEffectTestConfiguration.AllyMonsterIds.Clear();CombatEffectTestConfiguration.AllyMonsterIds.AddRange(Roster);
        CombatEffectTestConfiguration.EnemyMonsterIds.Clear();CombatEffectTestConfiguration.EnemyMonsterIds.AddRange(Roster);
        CombatEffectTestConfiguration.AllyCount=CombatEffectTestConfiguration.EnemyCount=8;
        CombatEffectTestConfiguration.AllyRank=CombatEffectTestConfiguration.EnemyRank=rank3?ExpeditionMonsterRank.Rank3:ExpeditionMonsterRank.Rank1;
        CombatEffectTestConfiguration.AllyTranscended=CombatEffectTestConfiguration.EnemyTranscended=false;
        CombatEffectTestConfiguration.AllyRank2TraitId=CombatEffectTestConfiguration.EnemyRank2TraitId="";
        CombatEffectTestConfiguration.AllyRank3TraitId=CombatEffectTestConfiguration.EnemyRank3TraitId="";
        CombatEffectTestConfiguration.AllyPersonalityId=CombatEffectTestConfiguration.EnemyPersonalityId="vengeful";
        CombatEffectTestConfiguration.ClearStartRequest();
        UnityEngine.Random.InitState(Seed);
        timeout=Time.realtimeSinceStartupAsDouble+240d;
        SceneManager.LoadScene(CombatEffectTestSceneController.BattleSceneName);
    }

    private void LateUpdate()
    {
        if(finished)return;
        if(Time.realtimeSinceStartupAsDouble>timeout){Complete("TIMEOUT");return;}
        battle=UnityEngine.Object.FindObjectOfType<MonsterAutoBattlePrototype>();
        if(battle==null||!battle.IsSceneReady)return;
        var units=((IList)B("units")).Cast<object>().ToArray();
        if(!prepared)
        {
            director=UnityEngine.Object.FindObjectOfType<CombatCameraDirector>();
            if(director==null)throw new InvalidOperationException("No production camera director.");
            var run=(ExpeditionRunSave)B("expeditionRun");run.activeBattleSeed=Seed;
            if(rank3)ApplyFixedTraits(units,run);
            UnityEngine.Random.InitState(Seed);
            foreach(object unit in units)UnitType.GetField("personalityPolicy").SetValue(unit,PersonalityCombatPolicy.FromId("vengeful"));
            actors=units.Count(Active);
            foreach(object unit in units)previousSkillUses[unit]=(int)U(unit,"skillUses");
            File.WriteAllLines(Path.Combine(output,"starting-roster.csv"),new[]{"id,team,species,rank,traits,x,z"}.Concat(units.Select(u=>
                string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5:R},{6:R}",U(u,"id"),U(u,"team"),((MonsterDefinition)U(u,"definition")).monsterId,
                rank3?3:1,string.Join("|",((IEnumerable)U(u,"activeTraitIds")).Cast<object>()),Position(u).x,Position(u).z))));
            prepared=true;
            if(!battle.StartPreparedCombatEffectTestBattle())throw new InvalidOperationException("Test battle start rejected.");
            return;
        }
        bool running=(bool)B("battleStarted");
        if(!running&&!seenBattle)return;
        if(!seenBattle)
        {
            seenBattle=true;startedAt=Time.unscaledTimeAsDouble;previousSample=startedAt;
        }
        double now=Time.unscaledTimeAsDouble;largestSampleDt=Mathf.Max(largestSampleDt,(float)(now-previousSample));previousSample=now;
        bool focus=director.SkillFocusActive;
        float strength=director.SkillFocusStrength;
        object caster=BOptional("skillFocusUnit");
        int casterId=caster==null?-1:(int)U(caster,"id");
        var driver=caster==null?null:(SpriteMonsterAnimationDriver)U(caster,"spriteAnimation");
        float casterScale=driver==null?1f:OptionalFloat(driver,"skillFocusScale",1f);
        var shade=BOptional("skillFocusShade") as SpriteRenderer;
        float shadeAlpha=shade==null?0f:shade.color.a;
        var source=BOptional("skillFocusRenderer") as SpriteRenderer;
        var foregroundComponent=BOptional("skillFocusForeground") as Component;
        var foreground=foregroundComponent==null?null:foregroundComponent.GetComponent<SpriteRenderer>();
        bool foregroundEnabled=foreground!=null&&foreground.enabled&&foreground.gameObject.activeInHierarchy;
        bool castPending=Convert.ToBoolean(BOptional("skillFocusCastPending")??false);
        int casterUses=caster==null?-1:(int)U(caster,"skillUses");
        double focusStarted=Convert.ToDouble(director.GetType().GetField("skillFocusStartedAt",Private)?.GetValue(director)??-1d);
        double focusElapsed=focus?now-focusStarted:0d;
        // OnGUI creates this style only when the production exclamation is drawn; screenshots verify its pixels.
        bool indicatorVisible=focus&&strength>0f&&BOptional("skillFocusNoticeStyle")!=null;
        int basics=units.Sum(u=>(int)U(u,"basicAttackCount")),skills=units.Sum(u=>(int)U(u,"skillUses"));
        if(focus&&!wasFocus)
        {
            int prior=0;if(caster!=null)previousSkillUses.TryGetValue(caster,out prior);
            currentCaster=caster;
            current=new FocusEvent{startedAt=(float)(now-startedAt),casterId=casterId,skillsAtStart=skills,casterSkillsAtStart=prior};events.Add(current);
        }
        if(current!=null&&focus)
        {
            current.maximumStrength=Mathf.Max(current.maximumStrength,strength);
            current.maximumCasterScale=Mathf.Max(current.maximumCasterScale,casterScale);
            current.maximumShadeAlpha=Mathf.Max(current.maximumShadeAlpha,shadeAlpha);
            current.exclamationSeen|=indicatorVisible;
            current.casterChanged|=casterId>=0&&casterId!=current.casterId;
            current.pendingSeen|=castPending;
            current.foregroundSeen|=foregroundEnabled;
            if(casterUses>current.casterSkillsAtStart)
            {
                if(current.firstCommitElapsed<0f)current.firstCommitElapsed=(float)focusElapsed;
                if(focusElapsed<.24d-.0001d)current.castBeforeReadySamples++;
            }
            if(Time.timeScale==0f)current.hitStopSamples++;
            else if(Time.timeScale<.99f)current.slowSamples++;
        }
        if(!focus&&wasFocus&&current!=null)
        {
            current.endedAt=(float)(now-startedAt);current.seconds=current.endedAt-current.startedAt;current.skillsAtEnd=skills;
            current.casterSkillsAtEnd=currentCaster==null?-1:(int)U(currentCaster,"skillUses");current=null;currentCaster=null;
        }
        wasFocus=focus;samples++;
        if(Time.timeScale==0f)hitStopSamples++;else if(Time.timeScale<.99f)slowSamples++;else normalSamples++;
        csv.AppendFormat(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5:R},{6},{7:R},{8},{9:R},{10:R},{11},{12},{13},{14},{15:R},{16},{17},{18:R},{19},{20},{21:R},{22:R},{23},{24},{25}",
            Time.frameCount,Time.realtimeSinceStartupAsDouble,now-startedAt,B("battleElapsed"),Time.timeScale,Time.captureDeltaTime,focus,strength,casterId,casterScale,shadeAlpha,indicatorVisible,
            basics,skills,units.Count(Active),focusStarted,driver==null?"":driver.CurrentMotionName,driver==null?-1:driver.CurrentFrameIndex,
            focusElapsed,castPending,casterUses,caster==null?-1f:U(caster,"cooldown"),caster==null?-1f:U(caster,"skillTimer"),source==null?-1:source.sortingOrder,
            foreground==null?-1:foreground.sortingOrder,foregroundEnabled);
        if(hudScenario)ObserveHud(units,caster,source,foreground,focus);
        else csv.Append(",False,-1,-1,-1,-1,-1,0,0,0,0,-1,-1,False,False,-1,-1,False,0");
        csv.AppendLine();
        foreach(object unit in units)previousSkillUses[unit]=(int)U(unit,"skillUses");
        if(!captureClip&&!capturedNeutral&&now-startedAt>=1f){ScreenCapture.CaptureScreenshot(Path.Combine(output,"neutral.png"));capturedNeutral=true;}
        if(!captureClip&&!capturedFocused&&focus&&strength>.95f&&Time.timeScale>0f)
        {ScreenCapture.CaptureScreenshot(Path.Combine(output,"focused.png"));capturedFocused=true;}
        if(!running||((string)B("resultText")).Length>0)Complete("NATURAL_RESULT "+B("resultText"));
        else if(captureClip&&now-startedAt>=13f)Complete("REALTIME_CLIP_LIMIT");
        else if((float)B("battleElapsed")>=90f)Complete("BATTLE_LIMIT");
    }

    private void ObserveHud(object[] units,object caster,SpriteRenderer source,SpriteRenderer foreground,bool focus)
    {
        var camera=(Camera)B("mainCamera");
        bool casterVisible=false,casterOutlined=false,overlap=false,frontmost=foreground!=null&&foreground.enabled&&foreground.gameObject.activeInHierarchy;
        float casterTop=-1f,casterBottom=-1f,casterGap=-1f,frameMinimum=float.PositiveInfinity,frameMaximum=float.NegativeInfinity;
        int normal=0,hidden=0,unexpectedCaster=0,unexpectedOther=0,otherOutlined=0;
        Rect casterBody=new Rect();bool hasCasterBody=false;
        if(source!=null)
        {
            object[] bodyArgs={camera,source,new Rect()};
            hasCasterBody=(bool)spriteBodyGuiRect.Invoke(null,bodyArgs);casterBody=(Rect)bodyArgs[2];
            if(hasCasterBody){casterTop=casterBody.yMin;casterBottom=MonsterAutoBattlePrototype.HealthBarRectForVisualBounds(casterBody).yMax;casterGap=casterTop-casterBottom;}
        }
        foreach(object unit in units)
        {
            if(!Active(unit))continue;
            object[] args={unit,new Rect()};bool visible=(bool)unitWorldHudRect.Invoke(battle,args);
            var driver=(SpriteMonsterAnimationDriver)U(unit,"spriteAnimation");
            var renderer=driver==null?null:driver.GetComponent<SpriteRenderer>();
            object[] projectedArgs={camera,renderer,new Rect()};
            bool hasProjected=(bool)spriteBodyGuiRect.Invoke(null,projectedArgs);Rect projectedBody=(Rect)projectedArgs[2];
            var outline=(ScreenSpaceOutlineTarget)U(unit,"outline");
            if(unit==caster)casterOutlined=outline!=null&&outline.IsOutlined;
            else if(focus&&outline!=null&&outline.IsOutlined)otherOutlined++;
            if(unit==caster)casterVisible=visible;
            if(focus&&unit==caster)
            {
                if(visible)unexpectedCaster++;else hidden++;
            }
            else if(visible)
            {
                Rect body=(Rect)args[1];
                if(hasProjected)
                {
                    float gap=projectedBody.yMin-MonsterAutoBattlePrototype.HealthBarRectForVisualBounds(body).yMax;
                    frameMinimum=Mathf.Min(frameMinimum,gap);frameMaximum=Mathf.Max(frameMaximum,gap);normal++;
                }
                else unexpectedOther++;
            }
            else unexpectedOther++;
            if(!focus||unit==caster||renderer==null)continue;
            if(hasCasterBody&&hasProjected&&casterBody.Overlaps(projectedBody))overlap=true;
            if(foreground!=null)
            {
                int layer=SortingLayer.GetLayerValueFromID(renderer.sortingLayerID),frontLayer=SortingLayer.GetLayerValueFromID(foreground.sortingLayerID);
                if(layer>frontLayer||layer==frontLayer&&renderer.sortingOrder>=foreground.sortingOrder)frontmost=false;
            }
        }
        normalHudSamples+=normal;hiddenCasterHudSamples+=hidden;unexpectedCasterHudSamples+=unexpectedCaster;unexpectedHiddenOtherHudSamples+=unexpectedOther;
        if(normal>0){minimumHeadGap=Mathf.Min(minimumHeadGap,frameMinimum);maximumHeadGap=Mathf.Max(maximumHeadGap,frameMaximum);}
        if(focus&&overlap)visualOverlapSamples++;
        if(focus&&!frontmost)foregroundOrderViolations++;
        if(focus){otherOutlineViolations+=otherOutlined;if(!casterOutlined)casterOutlineMissingSamples++;}
        csv.AppendFormat(CultureInfo.InvariantCulture,",{0},{1:R},{2:R},{3:R},{4:R},{5:R},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}",casterVisible,casterTop,casterBottom,casterGap,
            normal>0?frameMinimum:-1f,normal>0?frameMaximum:-1f,normal,hidden,unexpectedCaster,unexpectedOther,source==null?-1:source.sortingLayerID,
            foreground==null?-1:foreground.sortingLayerID,overlap,frontmost,source==null?-1:source.gameObject.layer,foreground==null?-1:foreground.gameObject.layer,casterOutlined,otherOutlined);
    }

    private IEnumerator CaptureClipFrames()
    {
        var end=new WaitForEndOfFrame();
        while(!finished)
        {
            yield return end;
            if(finished||!seenBattle||battle==null)continue;
            Texture2D pixels=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if(pixels==null)throw new InvalidOperationException("No native Game View frame.");
                if(clipFrames==0){clipWidth=pixels.width;clipHeight=pixels.height;}
                if(pixels.width!=clipWidth||pixels.height!=clipHeight)throw new InvalidOperationException("Game View dimensions changed.");
                string path=Path.Combine(output,"Frames","frame"+(++clipFrames).ToString("D6")+".png");
                if(File.Exists(path))throw new IOException("Capture frame already exists.");
                File.WriteAllBytes(path,pixels.EncodeToPNG());
                clipTimes.AppendFormat(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5:R}\n",clipFrames,
                    Time.unscaledTimeAsDouble-startedAt,B("battleElapsed"),Time.timeScale,Time.deltaTime,Time.realtimeSinceStartupAsDouble);
                File.WriteAllText(Path.Combine(output,"frame-times.csv"),clipTimes.ToString());
            }
            finally{if(pixels!=null)Destroy(pixels);}
        }
    }

    private void ApplyFixedTraits(object[] units,ExpeditionRunSave run)
    {
        foreach(object unit in units)
        {
            var definition=(MonsterDefinition)U(unit,"definition");var owned=(OwnedMonsterSave)U(unit,"ownedInstance");
            var state=U(unit,"team").ToString()=="Player"?run.monsters.First(m=>m.instanceId==owned.instanceId):(ExpeditionMonsterStateSave)U(unit,"enemyProgression");
            state.rank2TraitId=ExpeditionTraitProgression.FixedTrait(definition,2).id;state.rank3TraitId=ExpeditionTraitProgression.FixedTrait(definition,3).id;
            BattleType.GetMethod("RefreshUnitFromOwnedData",Private).Invoke(battle,new object[]{unit,definition,owned});
            var active=(IList)U(unit,"activeTraitIds");
            if(!active.Contains(state.rank2TraitId)||!active.Contains(state.rank3TraitId))throw new InvalidOperationException("Fixed skill trait missing.");
            UnitType.GetField("hp").SetValue(unit,U(unit,"maxHp"));
        }
    }

    private void Complete(string result)
    {
        if(finished)return;finished=true;
        object[] units=battle==null?new object[0]:((IList)B("units")).Cast<object>().ToArray();
        if(current!=null)
        {
            current.endedAt=(float)(Time.unscaledTimeAsDouble-startedAt);current.seconds=current.endedAt-current.startedAt;current.skillsAtEnd=units.Sum(u=>(int)U(u,"skillUses"));
            current.casterSkillsAtEnd=currentCaster==null?-1:(int)U(currentCaster,"skillUses");
        }
        File.WriteAllText(Path.Combine(output,"samples.csv"),csv.ToString());
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Report{
            label=label,result=result,movie=captureClip?Path.Combine(output,"actual_1x.mp4"):"",seed=Seed,actors=actors,samples=samples,
            hitStopSamples=hitStopSamples,slowSamples=slowSamples,normalSamples=normalSamples,basicAttacks=units.Sum(u=>(int)U(u,"basicAttackCount")),skills=units.Sum(u=>(int)U(u,"skillUses")),
            playerAlive=units.Count(u=>Active(u)&&U(u,"team").ToString()=="Player"),enemyAlive=units.Count(u=>Active(u)&&U(u,"team").ToString()=="Enemy"),
            realSeconds=seenBattle?(float)(Time.unscaledTimeAsDouble-startedAt):0f,battleSeconds=battle==null?0f:(float)B("battleElapsed"),largestSampleDt=largestSampleDt,
            finalTimeScale=Time.timeScale,finalCaptureDelta=Time.captureDeltaTime,focusActiveAtEnd=director!=null&&director.SkillFocusActive,
            normalHudSamples=normalHudSamples,hiddenCasterHudSamples=hiddenCasterHudSamples,unexpectedCasterHudSamples=unexpectedCasterHudSamples,
            unexpectedHiddenOtherHudSamples=unexpectedHiddenOtherHudSamples,visualOverlapSamples=visualOverlapSamples,foregroundOrderViolations=foregroundOrderViolations,
            otherOutlineViolations=otherOutlineViolations,casterOutlineMissingSamples=casterOutlineMissingSamples,
            minimumHeadGap=float.IsInfinity(minimumHeadGap)?0f:minimumHeadGap,maximumHeadGap=float.IsInfinity(maximumHeadGap)?0f:maximumHeadGap,
            participants=units.Select(u=>U(u,"id")+":"+((MonsterDefinition)U(u,"definition")).monsterId+":basic="+U(u,"basicAttackCount")+":skills="+U(u,"skillUses")+":alive="+U(u,"alive")).ToArray(),
            focusEvents=events.ToArray()},true));
        File.WriteAllText(Path.Combine(output,"complete.txt"),result);
        Debug.Log("[Skill Focus QA] COMPLETE "+output+": "+result);
    }

    private object B(string name)=>BattleType.GetField(name,Private).GetValue(battle);
    private object BOptional(string name)=>BattleType.GetField(name,Private)?.GetValue(battle);
    private static object U(object unit,string name)=>UnitType.GetField(name).GetValue(unit);
    private static Vector3 Position(object unit)=>((GameObject)U(unit,"model")).transform.position;
    private static bool Active(object unit)=>unit!=null&&(bool)U(unit,"alive")&&!(bool)U(unit,"inStorage")&&((GameObject)U(unit,"model")).activeInHierarchy;
    private static float OptionalFloat(object instance,string name,float fallback)=>Convert.ToSingle(instance.GetType().GetField(name,Private)?.GetValue(instance)??fallback);
    private void OnDestroy()
    {
        Time.timeScale=originalTimeScale;Time.captureDeltaTime=originalCaptureDelta;
        foreach(var pair in configuration)
            if(pair.Key.GetValue(null) is IList list){list.Clear();foreach(object value in (object[])pair.Value)list.Add(value);}
            else pair.Key.SetValue(null,pair.Value);
    }
}
