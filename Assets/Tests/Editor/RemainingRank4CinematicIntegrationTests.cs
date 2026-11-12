using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class RemainingRank4CinematicIntegrationTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private readonly Type type=typeof(MonsterAutoBattlePrototype);
    private MonsterAutoBattlePrototype battle;
    private Camera camera;
    private CombatCameraDirector director;
    private LayeredBattleEnvironment environment;
    private Type unitType;
    private object source,primary,secondary,previousFrontend;
    private HashSet<GameObject> existing;
    private float previousSpeed;
    private bool previousTestMode;
    private int nextUnitId;

    [SetUp]
    public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());nextUnitId=0;
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Hidden).GetValue(null);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,null);
        Assert.That(CombatSkillClock.Active,Is.False);Time.timeScale=1f;CombatEffectTestConfiguration.Active=true;
        var host=new GameObject("Inactive remaining rank4 integration tests");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=type.GetNestedType("Unit",Hidden);
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",false);
        BattleSet("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        camera=new GameObject("Remaining rank4 integration camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-12f),Quaternion.Euler(35f,0f,0f));
        camera.fieldOfView=43f;camera.aspect=16f/9f;camera.lensShift=new Vector2(.015f,-.021f);
        director=camera.gameObject.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        environment=camera.gameObject.AddComponent<LayeredBattleEnvironment>();
        BattleSet("mainCamera",camera);BattleSet("combatCameraDirector",director);BattleSet("layeredBattleEnvironment",environment);
        source=Unit("aranea",0,Vector3.zero);Set(source,"augmentTranscended",true);
        primary=Unit("geomchi",1,Vector3.right);secondary=Unit("tino",1,new Vector3(1.3f,0f,.3f));Set(source,"target",primary);
    }

    [TearDown]
    public void Cleanup()
    {
        if(battle!=null)
        {Call("DisposeRank4Cinematics");Call("CancelCommonSkillFocus");Call("ResetFamilyTranscendenceStates");}
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)Object.DestroyImmediate(value);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"No presentation may leave the battle clock held.");
    }

    [TestCase("aranea",0)] [TestCase("aranea",1)]
    [TestCase("chundongi",0)] [TestCase("chundongi",1)]
    [TestCase("tino",0)] [TestCase("tino",1)]
    [TestCase("chiwoo",0)] [TestCase("chiwoo",1)]
    [TestCase("mantius",0)] [TestCase("mantius",1)]
    [TestCase("versus",0)] [TestCase("versus",1)]
    [TestCase("kogason",0)] [TestCase("kogason",1)]
    [TestCase("jangpung",0)] [TestCase("jangpung",1)]
    [TestCase("holydeer",0)] [TestCase("holydeer",1)]
    [TestCase("pone",0)] [TestCase("pone",1)]
    [TestCase("vivre",0)] [TestCase("vivre",1)]
    [TestCase("haeta",0)] [TestCase("haeta",1)]
    public void EachFamilyQueuesOnceForEachUnitOnEitherTeamAndResetsForNextWave(string species,int team)
    {
        ReplaceSource(species,team);ArmReleasedFollowup(source);
        Assert.That(QueueSource(),Is.True);Assert.That(QueueSource(),Is.False);Assert.That(Queue.Count,Is.EqualTo(1));
        BeginQueued(source);Assert.That(Presentation,Is.Not.Null);Assert.That(Stage.Active,Is.True);
        Assert.That(Stage.Family,Is.EqualTo(Rank4CinematicCatalog.Canonical(Definition(source).familyId)));
        Assert.That(QueueSource(),Is.False,"Re-entering the same trigger cannot replay an active stage.");
        Call("FinishRank4Cinematic");Assert.That(QueueSource(),Is.False,"Later skill casts in this wave keep their normal short effects.");
        Call("ResetRank4Cinematics");BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);ArmReleasedFollowup(source);
        Assert.That(QueueSource(),Is.True);BeginQueued(source);AssertNoDamage();
    }

    [TestCase(0)] [TestCase(1)]
    public void SnapshotKeepsRealSpeciesShinyFramesFlipColorTargetsAndAllies(int team)
    {
        ReplaceSource("vivre",team,true);ArmReleasedFollowup(source);
        object friend=Unit("woongi",team,Vector3.left*2f);
        Renderer(source).flipX=team==0;Renderer(source).color=new Color(.68f,.84f,.73f,.91f);
        Renderer(friend).color=new Color(.85f,.65f,.55f,1f);
        Color color=Renderer(source).color;bool flip=Renderer(source).flipX;Sprite body=Renderer(source).sprite;
        Assert.That(QueueSource(),Is.True);var context=(Rank4StageContext)Field(Queue[0],"context");
        Assert.That(context.source.species,Is.EqualTo("vivre"));Assert.That(context.source.shiny,Is.True);
        Assert.That(context.source.sprite,Is.SameAs(body));Assert.That(context.source.flipX,Is.EqualTo(flip));Assert.That(context.source.color,Is.EqualTo(color));
        var authored=(SpriteMonsterAnimationDriver)Get(source,"spriteAnimation");
        Assert.That(context.source.frames.Length,Is.EqualTo(authored.SkillAttackFrameCount));
        for(int i=0;i<context.source.frames.Length;i++)Assert.That(context.source.frames[i],Is.SameAs(authored.GetSkillAttackFrame(i)));
        Assert.That(context.enemy,Is.EqualTo(team==1));Assert.That(context.targets.Select(a=>a.species),Is.EquivalentTo(new[]{"geomchi","tino"}));
        Assert.That(context.allies.Length,Is.EqualTo(1));Assert.That(context.allies[0].species,Is.EqualTo("woongi"));
        Assert.That(context.allies[0].color,Is.EqualTo(Renderer(friend).color));
        Renderer(source).flipX=!flip;Renderer(source).color=Color.magenta;
        Assert.That(context.source.flipX,Is.EqualTo(flip));Assert.That(context.source.color,Is.EqualTo(color),"The stage owns a value snapshot, not live mutable sprite styling.");
        Assert.That(context.returnHeight,Is.GreaterThan(0f));
        Vector3 viewport=camera.WorldToViewportPoint(Model(source).transform.position);
        Assert.That(Vector2.Distance(context.returnPosition,new Vector2((viewport.x-.5f)*16f,(viewport.y-.5f)*9f)),Is.LessThan(.0001f));
    }

    [Test]
    public void DeathBloomSnapshotUsesTheFallenCasterAndKeepsLivingEnemyFrames()
    {
        ReplaceSource("versus",0);ArmReleasedFollowup(source);
        Assert.That(QueueSource(),Is.True);
        var context=(Rank4StageContext)Field(Queue[0],"context");
        Assert.That(context.source.frames,Is.EqualTo(((SpriteMonsterAnimationDriver)Get(source,"spriteAnimation")).DeathFrames));
        Assert.That(context.targets[0].frames,Is.SameAs(((SpriteMonsterAnimationDriver)Get(primary,"spriteAnimation")).DragFrames));
        Assert.That(context.targets[1].frames,Is.SameAs(((SpriteMonsterAnimationDriver)Get(secondary,"spriteAnimation")).DragFrames));
    }

    [TestCase("aranea")] [TestCase("chundongi")] [TestCase("tino")] [TestCase("chiwoo")]
    [TestCase("mantius")] [TestCase("versus")] [TestCase("kogason")] [TestCase("jangpung")]
    [TestCase("holydeer")] [TestCase("pone")] [TestCase("vivre")] [TestCase("haeta")]
    public void EveryV3FilmStartsAndTicksItsOwnTimedAudioWhileCombatTimeIsStopped(string species)
    {
        ReplaceSource(species,0);ArmReleasedFollowup(source);Assert.That(QueueSource(),Is.True);BeginQueued(source);
        object audio=BattleGet("familyRank4CinematicAudio");Assert.That(audio,Is.Not.Null);
        object bank=Field(audio,"bank");Assert.That(bank,Is.Not.Null);
        Assert.That(Field(bank,"family"),Is.EqualTo(Rank4CinematicCatalog.Canonical(Definition(source).familyId)));
        AudioSource voice=(AudioSource)Field(audio,"source");AudioClip clip=voice.clip;
        SetField(Presentation,"elapsed",.56f-Time.unscaledDeltaTime);Assert.That(UpdateRank4AndPrelude(),Is.True);
        Assert.That(Field(audio,"bank"),Is.SameAs(bank));
        Assert.That(Field(audio,"source"),Is.SameAs(voice));Assert.That(voice.clip,Is.SameAs(clip));
        Assert.That(Time.timeScale,Is.Zero);Assert.That(CombatSkillClock.Active,Is.True);
        Assert.That(((AudioSource)Field(audio,"source")).isPlaying,Is.False,"EditMode validates scheduling without audible playback.");
        Call("FinishRank4Cinematic");Assert.That(Field(audio,"bank"),Is.Null);Assert.That(Field(audio,"playAudio"),Is.False);
    }

    [TestCase("finish")] [TestCase("reset")] [TestCase("invalid")]
    public void NormalFinishingKeepsTheTailWhileResettingOrInvalidatingAV3FilmStopsIt(string reason)
    {
        ArmReleasedFollowup(source);Assert.That(QueueSource(),Is.True);BeginQueued(source);
        object audio=BattleGet("familyRank4CinematicAudio");Assert.That(Field(audio,"bank"),Is.Not.Null);
        var voice=(AudioSource)Field(audio,"source");
        var tail=AudioClip.Create("Silent finishing tail",4800,2,48000,false);voice.clip=tail;
        try
        {
            if(reason=="finish")Call("FinishRank4Cinematic");
            else if(reason=="reset")Call("ResetRank4Cinematics");
            else
            {
                BattleSet("mainCamera",null);
                try{Assert.That(UpdateRank4AndPrelude(),Is.False);}
                finally{BattleSet("mainCamera",camera);}
            }
            Assert.That(Presentation,Is.Null);Assert.That(Field(audio,"bank"),Is.Null);
            Assert.That(Field(audio,"playAudio"),Is.False);
            if(reason=="finish")Assert.That(voice.clip,Is.SameAs(tail));else Assert.That(voice.clip,Is.Null);
        }
        finally{voice.clip=null;Object.DestroyImmediate(tail);}
    }

    [Test]
    public void AStageCleanupExceptionStillStopsTheV3AudioAndReleasesTheBattleClock()
    {
        ArmReleasedFollowup(source);Assert.That(QueueSource(),Is.True);BeginQueued(source);
        object state=Presentation,audio=BattleGet("familyRank4CinematicAudio");Rank4CinematicStage stage=Stage;
        SetField(state,"stage",null);
        try{Assert.Throws<TargetInvocationException>(()=>Call("FinishRank4Cinematic"));}
        finally{SetField(state,"stage",stage);}
        Assert.That(Field(audio,"bank"),Is.Null);Assert.That(Field(audio,"playAudio"),Is.False);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.GreaterThan(0f));
    }

    [Test]
    public void SimultaneousUnitsUseSeparateRequestsAndEveryActorIsFrozenDuringTheReadOnlyIntro()
    {
        object second=Unit("chundongi",0,Vector3.left*2f);Set(second,"augmentTranscended",true);
        ArmReleasedFollowup(second);
        Assert.That(QueueSource(),Is.True);Assert.That(Call("QueueRank4Cinematic",second,null),Is.True);
        Assert.That(Queue.Count,Is.EqualTo(2));BeginQueued(source);
        var clock=(CombatSkillClock)Field(Presentation,"clock");
        Assert.That(Time.timeScale,Is.Zero);Assert.That(CombatSkillClock.Active,Is.True);Assert.That(clock.Executing,Is.False);
        foreach(object unit in Units)Assert.That(CombatSkillClock.ActorFrozen(Model(unit).transform),Is.True);
        object first=Presentation;SetField(first,"elapsed",.05f-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.True);Assert.That(Presentation,Is.SameAs(first));Assert.That(Queue.Count,Is.EqualTo(1));
        Call("FinishRank4Cinematic");BeginQueued(second);Assert.That(Field(Field(Presentation,"request"),"source"),Is.SameAs(second));
        AssertNoDamage();
    }

    [Test]
    public void ForeignExclusiveClockDelaysRequestsButFreezeCannotUndoAnAlreadyCommittedPresentation()
    {
        Assert.That(QueueSource(),Is.True);var foreign=new CombatSkillClock(camera.transform);
        try
        {
            foreign.Hold();Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(Presentation,Is.Null);
        }
        finally{foreign.End();}
        Status(source).ApplyTimed(CombatStatusKind.Freeze,1f,primary);
        Assert.That(UpdateRank4AndPrelude(),Is.True);Assert.That(Queue,Is.Empty);Assert.That(Presentation,Is.Not.Null);
        Assert.That(Stage.Active,Is.True);Assert.That(Status(source).Has(CombatStatusKind.Freeze),Is.True);
    }

    [Test]
    public void PendingRequestIsDiscardedSafelyIfTheBattleCameraIsDestroyedBeforeBegin()
    {
        Assert.That(QueueSource(),Is.True);Object.DestroyImmediate(camera.gameObject);
        Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Queue,Is.Empty);Assert.That(Presentation,Is.Null);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Renderer(source).forceRenderingOff,Is.False);AssertNoDamage();
    }

    [TestCase("FinishRank4Cinematic",false)] [TestCase("FinishRank4Cinematic",true)]
    [TestCase("natural",false)] [TestCase("natural",true)]
    [TestCase("ResetRank4Cinematics",false)] [TestCase("ResetRank4Cinematics",true)]
    [TestCase("OnDisable",false)] [TestCase("OnDisable",true)]
    public void FinishResetAndDisableRestoreExactCameraVisibilityAndSpeed(string method,bool originallyHidden)
    {
        Time.timeScale=1.75f;Renderer(source).forceRenderingOff=originallyHidden;
        Matrix4x4 projection=camera.projectionMatrix;projection.m03+=.023f;camera.projectionMatrix=projection;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        float fov=camera.fieldOfView;Vector2 shift=camera.lensShift;
        Assert.That(QueueSource(),Is.True);BeginQueued(source);Rank4CinematicStage stage=Stage;
        Assert.That(Renderer(source).forceRenderingOff,Is.True);Assert.That(director.enabled,Is.False);Assert.That(environment.enabled,Is.False);
        camera.transform.SetPositionAndRotation(new Vector3(20f,19f,18f),Quaternion.Euler(12f,24f,36f));
        camera.fieldOfView=22f;camera.lensShift=Vector2.one;camera.ResetProjectionMatrix();
        if(method=="natural"){SetField(Presentation,"elapsed",stage.Duration);UpdateRank4AndPrelude();}
        else Call(method);
        Assert.That(Presentation,Is.Null);Assert.That(stage.Active,Is.False);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(1.75f));Assert.That(Renderer(source).forceRenderingOff,Is.EqualTo(originallyHidden));
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.lensShift,Is.EqualTo(shift));Assert.That(camera.projectionMatrix,Is.EqualTo(projection));
        Assert.That(director.enabled,Is.True);Assert.That(environment.enabled,Is.True);AssertNoDamage();
    }

    [Test]
    public void PreviouslyDisabledDirectorAndEnvironmentStayDisabledAfterTheStage()
    {
        director.enabled=false;environment.enabled=false;Assert.That(QueueSource(),Is.True);BeginQueued(source);Call("FinishRank4Cinematic");
        Assert.That(director.enabled,Is.False);Assert.That(environment.enabled,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase(false)][TestCase(true)]
    public void AscentSnapshotKeepsTheActualBillboardHeightOnASteepBattleCamera(bool flipped)
    {
        camera.transform.SetPositionAndRotation(new Vector3(0f,12f,-7.5f),Quaternion.Euler(58f,0f,0f));
        var body=Renderer(source);body.transform.rotation=camera.transform.rotation;
        body.transform.localScale=Vector3.one*1.5f;body.flipX=flipped;body.flipY=flipped;
        Bounds visible=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{body.sprite});
        Vector3 center=body.transform.TransformPoint(new Vector3(flipped?-visible.center.x:visible.center.x,
            flipped?-visible.center.y:visible.center.y,0f));
        float depth=Vector3.Dot(center-camera.transform.position,camera.transform.forward);
        float expected=9f*visible.size.y*Mathf.Abs(body.transform.lossyScale.y)/
            (2f*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f));
        var context=new Rank4StageContext();Call("CaptureRank4ReturnBody",context,body);
        Assert.That(context.returnHeight,Is.EqualTo(expected).Within(.002f));
        Assert.That(QueueSource(),Is.True);BeginQueued(source);
        Assert.That(Stage.CurrentContext.returnHeight,Is.EqualTo(expected).Within(.002f));
    }

    [Test]public void SilkQueenConsumesOneCommittedCastAndTheLaterAnimationCallbackCannotRepeatIt()
    {
        Set(source,"spiderWebPending",true);Set(source,"spiderWebDamageMultiplier",1.5f);
        var cast=(IEnumerator)Call("CommitSilkQueenCocoons",source);
        Assert.That(cast,Is.Not.Null);Assert.That(Get(source,"spiderWebPending"),Is.False);
        Assert.That(Call("CommitSilkQueenCocoons",source),Is.Null);
        try
        {
            Assert.That(cast.MoveNext(),Is.True);
            float hp=(float)Get(primary,"hp");Assert.That(hp,Is.LessThan(5000f));
            Call("LaunchSpiderCaptureWeb",source,GameMasterData.Skill(SpiderCombatRules.SkillId),1f,false,0);
            Assert.That(Get(primary,"hp"),Is.EqualTo(hp));
        }
        finally{(cast as IDisposable)?.Dispose();}
    }

    [TestCase("death")] [TestCase("zeroHp")] [TestCase("revision")]
    [TestCase("generation")] [TestCase("ending")] [TestCase("pause")] [TestCase("externalSpeed")]
    public void InvalidSourceAndExternalPauseCancelSafelyWithoutRepeatingGameplay(string reason)
    {
        Time.timeScale=1.4f;Assert.That(QueueSource(),Is.True);BeginQueued(source);
        if(reason=="death")Set(source,"alive",false);
        else if(reason=="zeroHp")Set(source,"hp",0f);
        else if(reason=="revision")Set(source,"positionRevision",(int)Get(source,"positionRevision")+1);
        else if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else if(reason=="ending")BattleSet("battleEnding",true);
        else if(reason=="pause")BattleSet("paused",true);
        else Time.timeScale=.25f;
        Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Presentation,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(director.enabled,Is.True);Assert.That(environment.enabled,Is.True);
        Assert.That(Time.timeScale,Is.EqualTo(reason=="externalSpeed"?.25f:1.4f));AssertNoDamage();
    }

    [TestCase("versus")] [TestCase("vivre")]
    public void ReleasedMigrationAndHeavenBreathRequireRealFollowupStateAndKeepTheirDeathPolicy(string species)
    {
        ReplaceSource(species,0);
        bool queued=QueueSource();
        if(queued){Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Queue,Is.Empty);}
        Assert.That(Presentation,Is.Null);Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
        ArmReleasedFollowup(source);Assert.That(QueueSource(),Is.True);BeginQueued(source);
        Set(source,"alive",false);Set(source,"hp",0f);SetField(Presentation,"elapsed",.05f-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.True);
        Assert.That(Presentation,Is.Not.Null,"A released battle-owned effect retains the original owner-death policy.");AssertNoDamage();
        Call("FinishRank4Cinematic");Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void ReadOnlySpiderIntroDoesNotAdvanceBattleTimersAttackCountersOrDamage()
    {
        Set(source,"skillUses",7);Set(source,"skillHits",13);Set(source,"skillDamage",111f);BattleSet("battleElapsed",3.25f);
        Vector3 before=Model(source).transform.position,targetBefore=Model(primary).transform.position;
        float skillTimer=(float)Get(source,"skillTimer"),cooldown=(float)Get(source,"cooldown");
        Assert.That(QueueSource(),Is.True);BeginQueued(source);
        for(int i=0;i<8;i++)
        {
            SetField(Presentation,"elapsed",.2f-Time.unscaledDeltaTime);Call("Update");
            typeof(SpriteMonsterAnimationDriver).GetMethod("Update",Hidden).Invoke(Get(source,"spriteAnimation"),null);
        }
        Assert.That(Get(source,"skillUses"),Is.EqualTo(7));Assert.That(Get(source,"skillHits"),Is.EqualTo(13));Assert.That(Get(source,"skillDamage"),Is.EqualTo(111f));
        Assert.That(BattleGet("battleElapsed"),Is.EqualTo(3.25f));Assert.That(Get(source,"skillTimer"),Is.EqualTo(skillTimer));Assert.That(Get(source,"cooldown"),Is.EqualTo(cooldown));
        Assert.That(Model(source).transform.position,Is.EqualTo(before));Assert.That(Model(primary).transform.position,Is.EqualTo(targetBefore));AssertNoDamage();
        Call("FinishRank4Cinematic");Assert.That(QueueSource(),Is.False);Assert.That(Get(source,"skillUses"),Is.EqualTo(7));AssertNoDamage();
    }

    [Test]
    public void CeilingSpiderRevealsItsDomainAtReturnWithoutSpendingTheEightSecondBattleDuration()
    {
        object state=Call("FamilyTranscendenceStateFor",source,true);
        Call("BeginSilkQueenDomain",source,primary,state);
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(Field(state,"silkDomain"),Is.Null);
        BeginQueued(source);
        foreach(float sample in new[]{.30f,1.85f,3.20f,4.40f})
        {
            SetField(Presentation,"elapsed",sample-Time.unscaledDeltaTime);UpdateRank4AndPrelude();
            Call("UpdateFamilyTranscendence",source,.25f);
            Assert.That(Field(state,"timer"),Is.EqualTo(8f));Assert.That(Field(state,"silkDomain"),Is.Null);
            Assert.That(((Rank4CinematicStage)Field(Presentation,"stage")).ActionTime,Is.EqualTo(-1f));AssertNoDamage();
        }
        SetField(Presentation,"elapsed",SpiderQueenRank4Stage.FieldReturnTime+.35f-Time.unscaledDeltaTime);UpdateRank4AndPrelude();
        var effect=(PixelSilkQueenDomain)Field(state,"silkDomain");Assert.That(effect,Is.Not.Null);
        Assert.That(effect.GetComponent<MeshRenderer>().sharedMaterial.color.a,Is.GreaterThan(.6f));
        Call("UpdateFamilyTranscendence",source,.25f);Assert.That(Field(state,"timer"),Is.EqualTo(8f));
        Call("FinishRank4Cinematic");Call("UpdateFamilyTranscendence",source,.25f);
        Assert.That(Field(state,"timer"),Is.EqualTo(7.75f));Assert.That(Field(state,"silkDomain"),Is.SameAs(effect));
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));AssertNoDamage();
    }

    [Test]
    public void InterruptedCeilingIntroStillRevealsTheOriginalDomainWhenCombatResumes()
    {
        object state=Call("FamilyTranscendenceStateFor",source,true);
        Call("BeginSilkQueenDomain",source,primary,state);BeginQueued(source);
        Call("FinishRank4Cinematic");Assert.That(Field(state,"silkDomain"),Is.Null);
        Call("UpdateFamilyTranscendence",source,.1f);
        Assert.That(Field(state,"silkDomain"),Is.Not.Null);Assert.That(Field(state,"timer"),Is.EqualTo(7.9f));
        Assert.That(Field(state,"charges"),Is.EqualTo(2));AssertNoDamage();
    }

    [TestCase(false)][TestCase(true)]
    public void CeilingAscentHidesEveryAttachedBodyOutlineAndRestoresItsPreviousVisibility(bool interrupt)
    {
        var outline=new GameObject("Attached transcendence flame").AddComponent<SpriteRenderer>();
        outline.transform.SetParent(Model(source).transform,false);outline.sprite=Renderer(source).sprite;
        var hidden=new GameObject("Already hidden body overlay").AddComponent<SpriteRenderer>();
        hidden.transform.SetParent(Model(source).transform,false);hidden.sprite=outline.sprite;hidden.forceRenderingOff=true;
        Assert.That(QueueSource(),Is.True);BeginQueued(source);
        Assert.That(outline.forceRenderingOff,Is.True);Assert.That(hidden.forceRenderingOff,Is.True);
        if(!interrupt)
        {
            SetField(Presentation,"elapsed",Stage.Duration-.25f-Time.unscaledDeltaTime);UpdateRank4AndPrelude();
            Assert.That(outline.forceRenderingOff,Is.False);Assert.That(hidden.forceRenderingOff,Is.True);
        }
        Call("FinishRank4Cinematic");
        Assert.That(outline.forceRenderingOff,Is.False);Assert.That(hidden.forceRenderingOff,Is.True);
        Assert.That(Renderer(source).forceRenderingOff,Is.False);
    }

    [TestCase(0,false)] [TestCase(1,false)] [TestCase(0,true)] [TestCase(1,true)]
    public void HeavenBreathKeepsItsNativeShadowFallAndOneImpactUntilAfterTheFilm(int team,bool casterDies)
    {
        ReplaceSource("vivre",team);object first=CreateRealHeavenBreath(source);
        var effect=(PixelWyvernHeavenBreath)Field(first,"effect");Assert.That(effect,Is.Not.Null);
        // The fixture disables its battle host to suppress Awake; production effects have an active host.
        effect.transform.SetParent(null,true);Assert.That(effect.gameObject.activeInHierarchy,Is.True);
        foreach(var particles in effect.GetComponentsInChildren<ParticleSystem>(true))particles.Play(false);
        var shadow=effect.transform.Find("Passing Dragon Shadow").GetComponent<SpriteRenderer>();
        var mass=effect.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="Heaven Breath Mass");
        var fragments=effect.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="Heaven Breath Fragments");
        object other=Unit("igvire",team,Vector3.left*2f);Set(other,"augmentTranscended",true);Set(other,"target",primary);
        object waiting=CreateRealHeavenBreath(other);Assert.That(Queue.Count,Is.EqualTo(2));BeginQueued(source);
        foreach(float filmTime in WyvernHeavenRank4Stage.BoardTimes)
        {
            SetField(Presentation,"elapsed",filmTime-Time.unscaledDeltaTime);Call("Update");
            Assert.That(Stage.ActionTime,Is.EqualTo(-1f));
            Assert.That(Field(first,"time"),Is.EqualTo(0f));Assert.That(Field(first,"hit"),Is.False);
            Assert.That(Field(waiting,"time"),Is.EqualTo(0f));Assert.That(Field(waiting,"hit"),Is.False);
            Assert.That(Field(first,"effect"),Is.SameAs(effect));Assert.That(effect.gameObject.activeSelf,Is.True);
            Assert.That(Time.timeScale,Is.Zero);AssertNoDamage();
        }
        if(casterDies){Set(source,"hp",0f);Set(source,"alive",false);}
        SetField(Presentation,"elapsed",Stage.Duration+.001f-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Presentation,Is.Null);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(effect!=null&&effect.gameObject.activeSelf,Is.True,"Returning must keep the original field effect alive, including a released cast whose owner died.");

        Call("AdvanceWyvernHeavenBreath",first,.10f);
        Assert.That(shadow.enabled,Is.True);Assert.That(shadow.color.a,Is.GreaterThan(0f));Assert.That(mass.particleCount,Is.Zero);
        Call("AdvanceWyvernHeavenBreath",first,WyvernHeavenBreathRules.ShadowDuration-.10f+.001f);
        Assert.That(shadow.enabled,Is.False);Assert.That(mass.particleCount,Is.Zero);AssertNoDamage();
        Call("AdvanceWyvernHeavenBreath",first,WyvernHeavenBreathRules.PauseDuration+.10f);
        Assert.That(mass.particleCount,Is.GreaterThan(0));AssertNoDamage();
        Call("AdvanceWyvernHeavenBreath",first,WyvernHeavenBreathRules.ImpactTime-.001f-(float)Field(first,"time"));
        AssertNoDamage();Call("AdvanceWyvernHeavenBreath",first,.002f);
        Assert.That(Get(primary,"hp"),Is.EqualTo(4700f).Within(.01f));Assert.That(Get(secondary,"hp"),Is.EqualTo(4700f).Within(.01f));
        Assert.That(Field(first,"hit"),Is.True);Assert.That(Field(waiting,"time"),Is.EqualTo(0f));Assert.That(Field(waiting,"hit"),Is.False);
        Assert.That(fragments.particleCount,Is.GreaterThan(0));
        int hits=(int)Get(source,"skillHits"),uses=(int)Get(source,"skillUses");
        Call("AdvanceWyvernHeavenBreath",first,.01f);
        Call("AdvanceWyvernHeavenBreath",first,WyvernHeavenBreathRules.EndTime-(float)Field(first,"time")+.001f);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(hits));Assert.That(Get(source,"skillUses"),Is.EqualTo(uses));
        Assert.That(effect==null,Is.True);Assert.That(Get(primary,"hp"),Is.EqualTo(4700f).Within(.01f));
        Assert.That(((IList)BattleGet("wyvernHeavenBreaths")).Contains(first),Is.False);
        Assert.That(((IList)BattleGet("wyvernHeavenBreaths")).Contains(waiting),Is.True);
        if(!casterDies)
        {
            int queued=Queue.Count;Set(source,"skillTimer",0f);
            Assert.That(Call("BeginWyvernBreath",source,GameMasterData.Skill(WyvernCombatRules.SkillId)),Is.True);
            Assert.That(Get(source,"wyvernBreath"),Is.Null);Assert.That(Queue.Count,Is.EqualTo(queued),"Later rank-four casts do not repeat the wave cinematic.");
            Assert.That(Get(source,"skillUses"),Is.EqualTo(uses+1));
            Assert.That(((IList)BattleGet("wyvernBreaths")),Is.Empty);
            object next=((IList)BattleGet("wyvernHeavenBreaths")).Cast<object>().Single(c=>Field(c,"source")==source);
            Assert.That(Field(next,"time"),Is.EqualTo(0f));
            Call("AdvanceWyvernHeavenBreath",next,WyvernHeavenBreathRules.ImpactTime);
            Assert.That(Get(primary,"hp"),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(secondary,"hp"),Is.EqualTo(4400f).Within(.01f));
            Call("AdvanceWyvernHeavenBreath",next,WyvernHeavenBreathRules.EndTime);
        }
        Assert.That(((IList)BattleGet("wyvernHeavenBreaths")).Count,Is.EqualTo(1),"Only the other caster's waiting native cast remains.");
    }

    [TestCase(0)][TestCase(1)]
    public void MotherReturnsToTheActualFieldBeforeItsOneLandingAndRestoresTheImpactCamera(int team)
    {
        ReplaceSource("tino",team);object state=CreateRealTyrannoReign(source);BeginQueued(source);
        var nativeFoot=(PixelTyrannoReignStomp)Field(state,"tyrannoStomp");
        Assert.That(Stage.CurrentContext.battlefieldBounds,Is.EqualTo((Bounds)BattleGet("combatBounds")));
        Assert.That(Stage.CurrentContext.hasTyrannoFootprint,Is.True);
        Assert.That(Stage.CurrentContext.tyrannoFootprintViewport,Is.EqualTo(nativeFoot.ContactViewportRect(camera)));
        foreach(float time in new[]{TyrannoReignRank4Stage.FieldReturnTime-.01f,
            TyrannoReignRank4Stage.FieldReturnTime+.01f,TyrannoReignRank4Stage.ImpactTime+.01f})
        {
            SetField(Presentation,"elapsed",time-Time.unscaledDeltaTime);UpdateRank4AndPrelude();
            bool returned=time>=TyrannoReignRank4Stage.FieldReturnTime;
            Assert.That(Renderer(source).forceRenderingOff,Is.EqualTo(!returned));
            Assert.That(Stage.CurrentContext.returnBodyVisible,Is.EqualTo(!returned));
            Assert.That(director.enabled,Is.EqualTo(time>=TyrannoReignRank4Stage.ImpactTime));
            Assert.That(CombatSkillClock.Active,Is.True);
            if(time<TyrannoReignRank4Stage.ImpactTime)AssertNoDamage();
            else
            {
                Assert.That(Field(state,"tyrannoStompHit"),Is.True);
                Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
            }
        }
        Call("FinishRank4Cinematic");Assert.That(Renderer(source).forceRenderingOff,Is.False);
        Assert.That(director.enabled,Is.True);Assert.That(CombatSkillClock.Active,Is.False);
    }

    [TestCase(0)][TestCase(1)]
    public void GodsThreeApproachingFootstepsBounceEightRealBodiesWithoutMovingTheirCombatPositionsOrShadows(int team)
    {
        ReplaceSource("tino",team);object friend=Unit("woongi",team,Vector3.left*3f);
        for(int i=0;i<6;i++)Unit("woongi",1-team,new Vector3(3f+i,0f,i-3f));
        object[] enemies=Units.Cast<object>().Where(u=>!Get(u,"team").Equals(Get(source,"team"))).ToArray();
        Assert.That(enemies.Length,Is.EqualTo(8));
        Vector3[] ground=enemies.Select(u=>Model(u).transform.position).ToArray();
        Vector3[] body=enemies.Select(u=>Renderer(u).transform.position).ToArray();
        Vector3[] local=enemies.Select(u=>Renderer(u).transform.localPosition).ToArray();
        Vector3[] shadow=enemies.Select(u=>((GameObject)Get(u,"ring")).transform.position).ToArray();
        int[] statuses=enemies.Select(u=>Status(u).Count).ToArray();
        Vector3 friendBody=Renderer(friend).transform.position,sourceBody=Renderer(source).transform.position;
        Vector3 savedCamera=camera.transform.position;object cast=CreateRealTyrannoReign(source);
        var foot=(PixelTyrannoReignStomp)Field(cast,"tyrannoStomp");Assert.That(foot,Is.Not.Null);
        BeginQueued(source);Assert.That(Renderer(source).forceRenderingOff,Is.False);
        Assert.That(foot.gameObject.activeSelf,Is.False,"The approaching god must remain offscreen during the real-field prelude.");
        for(int step=0;step<3;step++)
        {
            float time=TyrannoGodFootstepsRules.StepTimeAt(step)+TyrannoGodFootstepsRules.BounceDuration*.5f;
            SetField(Presentation,"elapsed",time-Time.unscaledDeltaTime);UpdateRank4AndPrelude();
            float height=step==0?.25f:step==1?.36f:.48f;
            for(int i=0;i<enemies.Length;i++)
            {
                Assert.That(Vector3.Distance(Renderer(enemies[i]).transform.position,body[i]+Vector3.up*height),Is.LessThan(.0001f));
                Assert.That(Model(enemies[i]).transform.position,Is.EqualTo(ground[i]));
                Assert.That(((GameObject)Get(enemies[i],"ring")).transform.position,Is.EqualTo(shadow[i]));
                Assert.That(Get(enemies[i],"hp"),Is.EqualTo(5000f));Assert.That(Status(enemies[i]).Count,Is.EqualTo(statuses[i]));
            }
            Assert.That(Renderer(friend).transform.position,Is.EqualTo(friendBody));Assert.That(Get(friend,"hp"),Is.EqualTo(5000f));
            Assert.That(Renderer(source).transform.position,Is.EqualTo(sourceBody));Assert.That(Renderer(source).forceRenderingOff,Is.False);
            Assert.That(foot.gameObject.activeSelf,Is.False);Assert.That(Get(source,"skillHits"),Is.EqualTo(0));
            Assert.That(CombatSkillClock.Active,Is.True);Assert.That(Time.timeScale,Is.Zero);
            Assert.That(camera.transform.position,Is.Not.EqualTo(savedCamera));
            var dust=(ParticleSystem[])Field(Presentation,"tyrannoStepDust");
            Assert.That(dust.Count(p=>p!=null),Is.EqualTo(step+1));
            Assert.That(dust[step].main.useUnscaledTime,Is.True);Assert.That(dust[step].main.loop,Is.False);
            Assert.That(dust[step].GetComponentsInChildren<SpriteRenderer>(true),Is.Empty,"Footstep dust cannot reveal the summoned foot.");
            var particles=new ParticleSystem.Particle[24];int count=dust[step].GetParticles(particles);
            Assert.That(count,Is.EqualTo(16+step*4));float inward=team==0?1f:-1f;
            Assert.That(particles.Take(count).All(p=>p.velocity.x*inward>0f),Is.True,"Dust travels inward from the mirrored arena edge.");
        }
        Assert.That(Field(Presentation,"tyrannoStepSounds"),Is.EqualTo(3));
        ParticleSystem[] preludeDust=((ParticleSystem[])Field(Presentation,"tyrannoStepDust")).ToArray();
        SetField(Presentation,"elapsed",TyrannoGodFootstepsRules.FieldPreludeEndTime+.01f-Time.unscaledDeltaTime);UpdateRank4AndPrelude();
        Assert.That(enemies.Select(u=>Renderer(u).transform.localPosition).ToArray(),Is.EqualTo(local));
        Assert.That(Renderer(source).forceRenderingOff,Is.True);Assert.That(foot.gameObject.activeSelf,Is.False);
        Assert.That(camera.transform.position,Is.EqualTo(savedCamera));
        Assert.That(preludeDust.All(p=>p==null||!p.gameObject.activeSelf),Is.True);
        Assert.That(((ParticleSystem[])Field(Presentation,"tyrannoStepDust")).All(p=>p==null),Is.True);
        SetField(Presentation,"elapsed",TyrannoReignRank4Stage.FieldReturnTime+.01f-Time.unscaledDeltaTime);UpdateRank4AndPrelude();
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(foot.gameObject.activeSelf,Is.True);
        Assert.That(enemies.All(u=>(float)Get(u,"hp")==5000f),Is.True,"The actual damage belongs to the later landing.");
        Call("FinishRank4Cinematic");
    }

    [TestCase("FinishRank4Cinematic")][TestCase("ResetRank4Cinematics")][TestCase("OnDisable")][TestCase("caster death")]
    public void CancellingAnApproachingGodRestoresEveryBouncingBodyCameraAndNativeFoot(string reason)
    {
        ReplaceSource("tino",0);for(int i=0;i<6;i++)Unit("woongi",1,new Vector3(3f+i,0f,i-3f));
        object[] enemies=Units.Cast<object>().Where(u=>!Get(u,"team").Equals(Get(source,"team"))).ToArray();
        Vector3[] local=enemies.Select(u=>Renderer(u).transform.localPosition).ToArray();
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        object cast=CreateRealTyrannoReign(source);var foot=(PixelTyrannoReignStomp)Field(cast,"tyrannoStomp");
        BeginQueued(source);Rank4CinematicStage stage=Stage;
        SetField(Presentation,"elapsed",.50f-Time.unscaledDeltaTime);UpdateRank4AndPrelude();
        Assert.That(Renderer(primary).transform.localPosition.y,Is.GreaterThan(local[0].y));
        Assert.That(camera.transform.position,Is.Not.EqualTo(position));Assert.That(foot.gameObject.activeSelf,Is.False);
        ParticleSystem[] dust=((ParticleSystem[])Field(Presentation,"tyrannoStepDust")).ToArray();
        Assert.That(dust.Count(p=>p!=null),Is.EqualTo(1));
        if(reason=="caster death"){Set(source,"hp",0f);UpdateRank4AndPrelude();}
        else Call(reason);
        Assert.That(Presentation,Is.Null);Assert.That(stage.Active,Is.False);
        Assert.That(enemies.Select(u=>Renderer(u).transform.localPosition).ToArray(),Is.EqualTo(local));
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        if(foot!=null)Assert.That(foot.gameObject.activeSelf,Is.True,"An uncancelled native foot regains its original active state.");
        Assert.That(enemies.All(u=>(float)Get(u,"hp")==5000f),Is.True);Assert.That(Get(source,"skillHits"),Is.EqualTo(0));
        Assert.That(dust.All(p=>p==null||!p.gameObject.activeSelf),Is.True,"Cancellation cannot leave unscaled dust playing behind the next sequence.");
    }

    [Test]
    public void TyrannoHandoffConsumesOnlyItsExistingStompAndCannotHitAgainAfterTheStage()
    {
        ReplaceSource("tino",0);object state=CreateRealTyrannoReign(source);
        float expected=5000f-(float)Call("SkillPower",source)*FamilyTranscendenceRules.TyrannoStompDamageRatio;
        object other=Unit("magno",0,Vector3.left*2f);Set(other,"augmentTranscended",true);object waiting=CreateRealTyrannoReign(other);
        Assert.That(Queue.Count,Is.EqualTo(2));BeginQueued(source);
        HandoffAt(FamilyTranscendenceRules.TyrannoStompWarning-.01f);AssertNoDamage();
        HandoffAt(FamilyTranscendenceRules.TyrannoStompWarning+.01f);
        Assert.That(Get(primary,"hp"),Is.EqualTo(expected).Within(.01f));Assert.That(Get(secondary,"hp"),Is.EqualTo(expected).Within(.01f));
        Assert.That(Field(state,"tyrannoStompHit"),Is.True);Assert.That(Field(waiting,"tyrannoStompTime"),Is.EqualTo(0f));
        int hits=(int)Get(source,"skillHits");HandoffAt(FamilyTranscendenceRules.TyrannoStompEnd);Call("FinishRank4Cinematic");
        Call("TickTyrannoReign",source,state,2f);
        Assert.That(Get(primary,"hp"),Is.EqualTo(expected).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(hits));
        Assert.That(Field(state,"tyrannoPending"),Is.EqualTo(0));Assert.That(Field(waiting,"tyrannoStompTime"),Is.EqualTo(0f));
    }

    [Test]
    public void ImperialHornHandoffLandsOnceAtOriginalPositionsAndLeavesAnotherCastersStateWaiting()
    {
        ReplaceSource("jangpung",0);CreateBeetleBoard();Place(source,1,1);Place(primary,5,1);Place(secondary,7,1);
        object other=Unit("noepung",0,Vector3.zero);Set(other,"augmentTranscended",true);Place(other,1,3);
        object distant=Unit("woongi",1,Vector3.zero);Place(distant,6,3);
        Assert.That(Call("BeginBeetleImperialHorn",source,GameMasterData.Skill(BeetleCombatRules.SkillId)),Is.True);
        Assert.That(Call("BeginBeetleImperialHorn",other,GameMasterData.Skill(BeetleCombatRules.SkillId)),Is.True);
        object first=Get(source,"beetleOverturn"),waiting=Get(other,"beetleOverturn");
        Assert.That(first,Is.Not.Null);Assert.That(waiting,Is.Not.Null);Assert.That(Queue.Count,Is.EqualTo(2));BeginQueued(source);
        HandoffAt(BeetleOverturnRules.Warning+.1f);Assert.That(Field(first,"launched"),Is.True);Assert.That(Field(waiting,"time"),Is.EqualTo(0f));
        HandoffAt(BeetleOverturnRules.Landing+.01f);Assert.That(Get(primary,"currentTile"),Is.EqualTo(new Vector2Int(5,1)));Assert.That(Get(secondary,"currentTile"),Is.EqualTo(new Vector2Int(7,1)));
        Assert.That(Get(primary,"hp"),Is.EqualTo(4700f).Within(.01f));Assert.That(Get(secondary,"hp"),Is.EqualTo(4700f).Within(.01f));
        Vector3 firstLanding=Model(primary).transform.position;HandoffAt(BeetleOverturnRules.Landing+.01f);HandoffAt(BeetleOverturnRules.End);
        Assert.That(Model(primary).transform.position,Is.EqualTo(firstLanding));Assert.That(Get(source,"beetleOverturn"),Is.Null);
        Assert.That(Get(distant,"currentTile"),Is.EqualTo(new Vector2Int(6,3)));Assert.That(Field(waiting,"launched"),Is.False);
        Assert.That(Get(primary,"beetleOverturnedBy"),Is.Null);Assert.That(Get(secondary,"beetleOverturnedBy"),Is.Null);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));Assert.That(Get(other,"skillHits"),Is.Zero);
        Call("FinishRank4Cinematic");Assert.That(Get(source,"beetleOverturn"),Is.Null);
    }

    [Test]
    public void MushroomHandoffStartsOnlyItsOwnFlowerClockAndDoesNotRepeatDamage()
    {
        ReplaceSource("versus",0);Set(source,"hp",0f);Set(source,"alive",false);CreateDeathBloomPresentation(source);
        object first=Call("MushroomDeathBloomFor",source);Assert.That(first,Is.Not.Null);
        object other=Unit("kingmore",0,Vector3.left*2f);Set(other,"augmentTranscended",true);
        Set(other,"hp",0f);Set(other,"alive",false);CreateDeathBloomPresentation(other);
        object waiting=Call("MushroomDeathBloomFor",other);
        Assert.That(Queue.Count,Is.EqualTo(2));BeginQueued(source);HandoffAt(0f);AssertNoDamage();
        Assert.That(Field(first,"age"),Is.EqualTo(0f));Assert.That(Field(first,"awaitingPresentation"),Is.True);
        SetField(Presentation,"elapsed",MushroomColonyRank4Stage.FieldReturnTime+.5f);HandoffAt(.5f);
        Assert.That(Field(first,"age"),Is.EqualTo(.5f).Within(.0001f));
        Assert.That(Field(waiting,"age"),Is.EqualTo(0f));Assert.That(Field(waiting,"awaitingPresentation"),Is.True);
        Assert.That(Get(primary,"hp"),Is.LessThan(5000f));float hp=(float)Get(primary,"hp");
        HandoffAt(.5f);Assert.That(Get(primary,"hp"),Is.EqualTo(hp));
        Call("FinishRank4Cinematic");Call("UpdateMushroomDeathBlooms",.001f);
        Assert.That(Get(primary,"hp"),Is.EqualTo(hp),"Returning to normal battle cannot replay an already applied tick.");
    }

    [Test]
    public void MantisHandoffKeepsBroodHealingHatchConditionAndFinishesTheirEmergenceWithoutAdvancingAnotherCocoon()
    {
        ReplaceSource("mantius",0);Set(source,"hp",0f);
        Assert.That(Call("TryBeginMantisDeathMolt",source,primary),Is.True);object state=Get(source,"mantisMolt");
        object other=Unit("flotis",0,Vector3.left*3f);Set(other,"augmentTranscended",true);Set(other,"hp",0f);
        Assert.That(Call("TryBeginMantisDeathMolt",other,primary),Is.True);object waiting=Get(other,"mantisMolt");
        // The inactive EditMode fixture uses the established brood setup; real prefab spawning is checked in Play QA.
        var children=(IList)Field(state,"children");
        for(int i=0;i<3;i++)
        {
            Vector3 destination=new Vector3(i-1,0f,1.5f);object child=Unit("mantius",0,destination);
            Call("ConfigureMantisBroodling",source,child,Get(source,"maxHp"));
            Set(child,"mantisBroodEnd",destination);Set(child,"mantisBroodRise",.32f);Model(child).transform.position=Model(source).transform.position;children.Add(child);
        }
        SetField(state,"spawned",3);Assert.That(Queue.Count,Is.EqualTo(2));BeginQueued(source);
        HandoffAt(.57f);Assert.That(Field(state,"age"),Is.EqualTo(.57f).Within(.0001f));
        Assert.That(Get(source,"hp"),Is.EqualTo(1500f+5000f*.03f*.57f).Within(.01f));
        Assert.That(Field(waiting,"age"),Is.EqualTo(0f));Assert.That(Field(waiting,"spawned"),Is.EqualTo(0));
        Assert.That(Field(state,"hatchAge"),Is.EqualTo(-1f),"A living brood keeps the cocoon alive instead of forcing a scripted hatch.");
        HandoffAt(.9f);HandoffAt(.9f);Assert.That(children.Count,Is.EqualTo(3));
        foreach(object child in children)
        {Assert.That(Get(child,"mantisBroodRise"),Is.EqualTo(0f));Assert.That(Model(child).transform.position,Is.EqualTo(Get(child,"mantisBroodEnd")));}
        Assert.That(Get(source,"hp"),Is.EqualTo(1500f+5000f*.03f*.57f).Within(.01f),"Repeated presentation ticks cannot repeatedly heal the owner.");
        Call("FinishRank4Cinematic");
        foreach(object child in children){Set(child,"alive",false);Model(child).SetActive(false);}
        Call("TickMantisDeathMolt",source,.79f);Assert.That(Get(source,"mantisMolt"),Is.SameAs(state));
        Call("TickMantisDeathMolt",source,.02f);Assert.That(Get(source,"mantisMolt"),Is.Null);Assert.That(Get(source,"alive"),Is.True);
        Assert.That(Field(waiting,"age"),Is.EqualTo(0f));AssertNoDamage();
    }

    [Test]
    public void MammothHandoffLaunchesAllEnemiesOnceAndLeavesAnotherCasterWaiting()
    {
        ReplaceSource("kogason",0);Call("StartMammothExpulsion",source,primary);
        object state=Get(source,"mammothExpulsion");Assert.That(state,Is.Not.Null);
        object other=Unit("seolgason",0,Vector3.left*6f);Set(other,"augmentTranscended",true);
        object waitingTarget=Unit("woongi",1,Vector3.left*5f);Set(other,"target",waitingTarget);
        Call("StartMammothExpulsion",other,waitingTarget);object waiting=Get(other,"mammothExpulsion");Assert.That(waiting,Is.Not.Null);
        Assert.That(Queue.Count,Is.EqualTo(2));BeginQueued(source);HandoffAt(MammothExpulsionRules.Contact-.01f);
        Assert.That(Get(primary,"hp"),Is.EqualTo(5000f));Assert.That(Field(state,"launched"),Is.False);
        HandoffAt(MammothExpulsionRules.Contact+.01f);
        foreach(object victim in new[]{primary,secondary,waitingTarget})
        {
            Assert.That((float)Get(victim,"hp"),Is.InRange(1f,4999f));Assert.That(Get(victim,"alive"),Is.True);
            Assert.That(Get(victim,"mammothExpelled"),Is.False);Assert.That(Get(victim,"mammothRicochetActive"),Is.True);
        }
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));
        int kills=(int)Get(source,"kills");float damage=(float)Get(source,"skillDamage");
        Assert.That(Field(waiting,"time"),Is.EqualTo(0f));Assert.That(Field(waiting,"launched"),Is.False);
        HandoffAt(MammothExpulsionRules.Contact+.01f);Call("FinishRank4Cinematic");
        Assert.That(Get(source,"kills"),Is.EqualTo(kills));Assert.That(Get(source,"skillHits"),Is.EqualTo(3));Assert.That(Get(source,"skillDamage"),Is.EqualTo(damage));
        Assert.That(Get(source,"mammothExpulsion"),Is.SameAs(state));
        Assert.That(Field(waiting,"time"),Is.EqualTo(0f));
        Assert.That(((IList)BattleGet("mammothExpulsionFlights")).Count,Is.EqualTo(1),"The real charge and ricochets continue after battle time resumes.");
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
    }

    [TestCase("castCleared")] [TestCase("targetDied")] [TestCase("targetHealed")] [TestCase("outOfReach")]
    public void WholeArenaMammothOnlyCancelsWhenItsOwnCastHasEnded(string reason)
    {
        ReplaceSource("kogason",0);Set(primary,"hp",500f);Call("StartMammothExpulsion",source,primary);
        object cast=Get(source,"mammothExpulsion");Assert.That(cast,Is.Not.Null);Assert.That(Queue.Count,Is.EqualTo(1));
        if(reason=="castCleared")Call("EndMammothExpulsion",source,cast);
        else if(reason=="targetDied"){Set(primary,"hp",0f);Set(primary,"alive",false);}
        else if(reason=="targetHealed")Set(primary,"hp",2000f);
        else Model(primary).transform.position=Vector3.right*10f;
        bool continues=reason!="castCleared";
        Assert.That(UpdateRank4AndPrelude(),Is.EqualTo(continues));Assert.That(Queue,Is.Empty);
        Assert.That(Presentation==null,Is.EqualTo(!continues));
        Assert.That(CombatSkillClock.Active,Is.EqualTo(continues));Assert.That(Time.timeScale,Is.EqualTo(continues?0f:1f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(0));
        Assert.That(Get(secondary,"hp"),Is.EqualTo(5000f));
    }

    [Test]
    public void MammothReturnsBattleTimeAtContactWhileNativeRicochetsKeepMoving()
    {
        ReplaceSource("kogason",0);Call("StartMammothExpulsion",source,primary);BeginQueued(source);
        SetField(Presentation,"elapsed",MammothExpulsionRank4Stage.FieldReturnTime+.001f);
        Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Presentation,Is.Null);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Get(primary,"mammothRicochetActive"),Is.True);
        Vector3 casterStart=Model(source).transform.position,victimStart=Model(primary).transform.position;
        Call("UpdateMammothExpulsionFlights",.2f);
        Assert.That(Vector3.Distance(Model(source).transform.position,casterStart),Is.GreaterThan(.1f));
        Assert.That(Vector3.Distance(Model(primary).transform.position,victimStart),Is.GreaterThan(.1f));
        Call("UpdateMammothExpulsionFlights",MammothExpulsionRules.Duration);
        Assert.That(Get(source,"mammothExpulsion"),Is.Null);Assert.That(Get(primary,"mammothRicochetActive"),Is.False);
        Assert.That(((IList)BattleGet("mammothExpulsionFlights")).Count,Is.Zero);
    }

    [TestCase(0)][TestCase(1)]
    public void WaterMantleReleasesBattleAndNativeGuardianAtTheFieldReturnWithoutAnExtraFrozenTail(int team)
    {
        ReplaceSource("holydeer",team);BattleSet("battleElapsed",3.25f);Time.timeScale=1.5f;
        var own=AddNativeGuardian(team==0);var opposite=AddNativeGuardian(team!=0);
        var original=SpiritBattleRendererFlags(own,opposite);
        Assert.That(QueueSource(),Is.True);Assert.That(Call("UpdateRank4Cinematic"),Is.True);
        Assert.That(PreludeDuration,Is.EqualTo(.45f));Assert.That(Prelude,Is.Not.Null);Assert.That(Presentation,Is.Null);
        SetField(Prelude,"elapsed",.44f-Mathf.Clamp(Time.unscaledDeltaTime,0f,.05f));
        Assert.That(Call("UpdateRank4SkillPrelude"),Is.True);Assert.That(Presentation,Is.Null);
        CompletePrelude();Assert.That(Call("UpdateRank4Cinematic"),Is.True);
        Rank4CinematicStage stage=Stage;Assert.That(stage.Duration,Is.EqualTo(3.50f));
        Sprite settledPose=own.BodyRenderer.sprite;

        SetField(Presentation,"elapsed",3.49f-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.True);Assert.That(Time.timeScale,Is.Zero);
        Assert.That(CombatSkillClock.Active,Is.True);Assert.That(BattleGet("battleElapsed"),Is.EqualTo(3.25f));
        Assert.That(own.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),Is.True);
        RefreshNativeGuardian(own);Assert.That(own.BodyRenderer.sprite,Is.SameAs(settledPose));

        SetField(Presentation,"elapsed",3.50f-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.False,"A returned field cannot keep blocking the normal battle Update.");
        Assert.That(Presentation,Is.Null);Assert.That(stage.Active,Is.False);Assert.That(Prelude,Is.Null);
        Assert.That(Time.timeScale,Is.EqualTo(1.5f));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(director.enabled,Is.True);Assert.That(environment.enabled,Is.True);
        foreach(var pair in original)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
        foreach(object unit in Units)Assert.That(CombatSkillClock.ActorFrozen(Model(unit).transform),Is.False);
        Assert.That(BattleGet("battleElapsed"),Is.EqualTo(3.25f));

        BattleSet("battleElapsed",3.50f);RefreshNativeGuardian(own);
        Assert.That(own.BodyRenderer.sprite,Is.Not.SameAs(settledPose),"The next normal battle-time advance must animate the native goddess immediately.");
        Assert.That(own.GetComponentInChildren<ParticleSystem>(true).particleCount,Is.GreaterThan(0));AssertNoDamage();
    }

    [TestCase("kogason",0)] [TestCase("seolgason",0)] [TestCase("irontusk",0)] [TestCase("gaiason",0)]
    [TestCase("kogason",1)] [TestCase("seolgason",1)] [TestCase("irontusk",1)] [TestCase("gaiason",1)]
    public void MammothCooldownRecastsItsNativeSkillWithoutRepeatingTheWaveCinematic(string species,int team)
    {
        ReplaceSource(species,team);Set(source,"skillTimer",0f);
        SkillMasterRow skill=GameMasterData.Skill(MammothCombatRules.SkillId);
        Assert.That(Call("BeginEarthCharge",source,skill),Is.True);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillTimer"),Is.EqualTo(5f));
        Assert.That(Queue.Count,Is.EqualTo(1));BeginQueued(source);
        HandoffAt(MammothExpulsionRules.Contact+.001f);Call("FinishRank4Cinematic");
        Call("UpdateMammothExpulsionFlights",MammothExpulsionRules.Duration);
        Assert.That(Get(source,"mammothExpulsion"),Is.Null);Assert.That(Queue,Is.Empty);
        Set(source,"skillTimer",0f);
        Assert.That(Call("BeginEarthCharge",source,skill),Is.True);
        object next=Get(source,"mammothExpulsion");Assert.That(next,Is.Not.Null);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(2));Assert.That(Get(source,"skillTimer"),Is.EqualTo(5f));
        Assert.That(Queue,Is.Empty,"Only the cinematic is once per Wave; the active skill is not.");
        float hp=(float)Get(primary,"hp");Call("AdvanceMammothExpulsion",source,next,MammothExpulsionRules.Contact);
        Assert.That((float)Get(primary,"hp"),Is.LessThan(hp));Assert.That(Get(primary,"mammothRicochetActive"),Is.True);
        Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Presentation,Is.Null);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase("mantius")] [TestCase("jangpung")] [TestCase("pone")] [TestCase("tino")]
    public void PendingPresentationIsDiscardedWhenItsCommittedMoltOverturnWindOrStompHasEnded(string species)
    {
        ReplaceSource(species,0);ArmReleasedFollowup(source);Assert.That(QueueSource(),Is.True);
        if(species=="mantius")Set(source,"mantisMolt",null);
        else if(species=="jangpung")Call("EndBeetleOverturn",Get(source,"beetleOverturn"));
        else if(species=="pone")BattleSet("battleElapsed",GriffinStormOrbRules.End+.01f);
        else Call("CancelTyrannoReign",Call("FamilyTranscendenceStateFor",source,false),true);
        Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Presentation,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));AssertNoDamage();
    }

    [Test]
    public void ACommittedBeetleStillPresentsAfterSourceRevisionChangesAndReturnsToItsCurrentPosition()
    {
        ReplaceSource("jangpung",0);CreateBeetleBoard();Place(source,1,1);Place(primary,5,1);Place(secondary,7,1);
        Assert.That(Call("BeginBeetleImperialHorn",source,GameMasterData.Skill(BeetleCombatRules.SkillId)),Is.True);
        object cast=Get(source,"beetleOverturn");Assert.That(cast,Is.Not.Null);Assert.That(Queue.Count,Is.EqualTo(1));
        var queued=(Rank4StageContext)Field(Queue[0],"context");Vector2 stale=queued.returnPosition;
        Call("InterruptPositionalAction",source);Model(source).transform.position+=new Vector3(2f,0f,2f);
        Assert.That(Get(source,"beetleOverturn"),Is.SameAs(cast),"Moving away from the original cast position cannot revoke a committed horn strike.");
        Assert.That(UpdateRank4AndPrelude(),Is.True);Assert.That(Presentation,Is.Not.Null);
        Vector3 viewport=camera.WorldToViewportPoint(Model(source).transform.position);
        Vector2 expected=new Vector2((viewport.x-.5f)*16f,(viewport.y-.5f)*9f);
        Assert.That(Vector2.Distance(Stage.CurrentContext.returnPosition,expected),Is.LessThan(.0001f));Assert.That(expected,Is.Not.EqualTo(stale));
        HandoffAt(BeetleOverturnRules.End);Assert.That(Get(source,"beetleOverturn"),Is.Null);
        SetField(Presentation,"elapsed",Stage.Duration-.02f-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.True,"The committed presentation may finish after its original cast state was consumed.");
        Assert.That(Get(primary,"hp"),Is.EqualTo(4700f).Within(.01f));Assert.That(Get(secondary,"hp"),Is.EqualTo(4700f).Within(.01f));
        Call("FinishRank4Cinematic");
    }

    [Test]
    public void DestroyedDeathFlowerCannotPlayItsFilmOrCancelAnotherMushroomsRequest()
    {
        ReplaceSource("versus",0);Set(source,"hp",0f);Set(source,"alive",false);CreateDeathBloomPresentation(source);
        object other=Unit("kingmore",0,Vector3.left*2f);Set(other,"augmentTranscended",true);
        Set(other,"hp",0f);Set(other,"alive",false);CreateDeathBloomPresentation(other);
        object flower=Call("MushroomDeathBloomUnit",source);Set(flower,"hp",0f);Set(flower,"alive",false);
        Assert.That(UpdateRank4AndPrelude(),Is.True);
        Assert.That(Field(Field(Presentation,"request"),"source"),Is.SameAs(other));
        Assert.That(Stage.Active,Is.True);Assert.That(Call("IsMushroomDeathBloomPending",other),Is.True);
    }

    [TestCase("chundongi",0)][TestCase("chundongi",1)]
    [TestCase("seolmyo",0)][TestCase("seolmyo",1)]
    [TestCase("sundongi",0)][TestCase("sundongi",1)]
    [TestCase("hongryeon",0)][TestCase("hongryeon",1)]
    public void SpacetimeFilmReturnsToOneRealAreaHitForEveryCatAndTeam(string species,int team)
    {
        ReplaceSource(species,team);object friend=Unit("woongi",team,Vector3.left*3f);
        for(int i=0;i<6;i++)Unit("geomchi",1-team,new Vector3(7f+i,0f,i-3f));
        ArmReleasedFollowup(source);Assert.That(QueueSource(),Is.True);BeginQueued(source);
        Assert.That(Stage.CurrentContext.battlefieldBackdrop,Is.Not.Null);
        Assert.That(Stage.CurrentContext.targets.Length,Is.EqualTo(8));
        Stage.Tick(NinjaSubstitutionRank4Stage.FilmImpactTime-.01f,0f);HandoffAt(Stage.ActionTime);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(0));AssertNoDamage();
        Stage.Tick(NinjaSubstitutionRank4Stage.FilmImpactTime,0f);HandoffAt(Stage.ActionTime);
        object[] opponents=Units.Cast<object>().Where(u=>!Get(u,"team").Equals(Get(source,"team"))).ToArray();
        Assert.That(opponents.All(u=>(float)Get(u,"hp")<5000f),Is.True);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(8));Assert.That(Get(friend,"hp"),Is.EqualTo(5000f));
        float[] hp=opponents.Select(u=>(float)Get(u,"hp")).ToArray();
        HandoffAt(NinjaSpacetimeRules.Duration);HandoffAt(NinjaSpacetimeRules.Duration);
        Assert.That(opponents.Select(u=>(float)Get(u,"hp")).ToArray(),Is.EqualTo(hp));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"ninjaHuntPending"),Is.False);
        Call("FinishRank4Cinematic");
        Assert.That(Call("BeginNinjaSpacetime",source,GameMasterData.Skill(Definition(source).skillId)),Is.True);
        Assert.That(Queue,Is.Empty,"Later casts retain their short field effects instead of replaying the film.");
        Call("AdvanceNinjaSpacetime",source,NinjaSpacetimeRules.Windup);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(16));
    }

    [Test]public void SpacetimeCasterDeathBeforeContactReleasesTheFilmAndNeverDealsDamage()
    {
        ReplaceSource("chundongi",0);ArmReleasedFollowup(source);
        Assert.That(QueueSource(),Is.True);BeginQueued(source);Set(source,"hp",0f);
        Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Presentation,Is.Null);
        Call("AdvanceNinjaSpacetime",source,NinjaSpacetimeRules.Duration);
        AssertNoDamage();Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    private object CreateRealHeavenBreath(object unit)
    {
        Set(unit,"target",primary);
        Assert.That(Call("BeginWyvernBreath",unit,GameMasterData.Skill(WyvernCombatRules.SkillId)),Is.True);
        Assert.That(Get(unit,"wyvernBreath"),Is.Null);
        object state=((IList)BattleGet("wyvernHeavenBreaths")).Cast<object>().Single(c=>Field(c,"source")==unit);
        Assert.That(Field(state,"started"),Is.True);return state;
    }

    private object CreateRealTyrannoReign(object unit)
    {
        Call("QueueTyrannoReign",unit,BattleGet("combatGeneration"));object state=Call("FamilyTranscendenceStateFor",unit,true);
        Call("TickTyrannoReign",unit,state,0f);return state;
    }

    private void HandoffAt(float actionTime)
    {
        typeof(Rank4CinematicStage).GetProperty("ActionTime",Hidden).SetValue(Stage,actionTime);
        Call("AdvanceRank4CinematicHandoff",Presentation);
    }

    private void CreateBeetleBoard()
    {
        Type tileType=type.GetNestedType("Tile",Hidden);var tiles=(IDictionary)BattleGet("tiles");
        for(int y=0;y<4;y++)for(int x=0;x<8;x++)
        {
            var coordinate=new Vector2Int(x,y);object tile=Activator.CreateInstance(tileType,true);
            SetField(tile,"coordinate",coordinate);SetField(tile,"world",Call("GridWorld",coordinate,false));tiles.Add(coordinate,tile);
        }
    }
    private void Place(object unit,int x,int y)
    {
        var coordinate=new Vector2Int(x,y);Set(unit,"currentTile",coordinate);Set(unit,"initialTile",coordinate);
        Model(unit).transform.position=(Vector3)Call("GridWorld",coordinate,false);Set(unit,"lastGroundPosition",Model(unit).transform.position);
    }

    private void ReplaceSource(string species,int team,bool shiny=false)
    {
        Units.Remove(source);Object.DestroyImmediate(Model(source));source=Unit(species,team,Vector3.zero,shiny);
        Set(source,"augmentTranscended",true);Team(primary,1-team);Team(secondary,1-team);Set(source,"target",primary);
    }

    private void ArmReleasedFollowup(object unit)
    {
        string family=Definition(unit).familyId;
        if(family=="mushroom")
        {
            if(((IList)BattleGet("mushroomDeathBlooms")).Cast<object>().Any(b=>Field(b,"source")==unit))
                Call("ClearMushroomDeathBlooms",false);
            Set(unit,"hp",0f);Set(unit,"alive",false);CreateDeathBloomPresentation(unit);
            foreach(object request in Queue.Cast<object>().Where(r=>Field(r,"source")==unit).ToArray())Queue.Remove(request);
            return;
        }
        if(family=="ninja_cat")
        {
            Call("CancelNinjaSpacetime",unit);
            bool previous=(bool)BattleGet("mlEpisodeActive");BattleSet("mlEpisodeActive",true);
            try{Assert.That(Call("BeginNinjaSpacetime",unit,GameMasterData.Skill(Definition(unit).skillId)),Is.True);}
            finally{BattleSet("mlEpisodeActive",previous);}
            return;
        }
        if(family=="monkey")
        {
            Call("ClearMonkeyStorms",false);
            object cast=Activator.CreateInstance(type.GetNestedType("MonkeyStormCast",Hidden),true);
            SetField(cast,"source",unit);SetField(cast,"generation",BattleGet("combatGeneration"));
            SetField(cast,"attack",Get(unit,"damage"));SetField(cast,"damageScale",1f);
            SetField(cast,"random",new System.Random(1));SetField(cast,"awaitingPresentation",true);
            ((IList)BattleGet("monkeyStorms")).Add(cast);return;
        }
        if(family=="mammoth")
        {
            object cast=Activator.CreateInstance(type.GetNestedType("MammothExpulsionState",Hidden),true);
            SetField(cast,"target",primary);SetField(cast,"direction",Vector3.right);SetField(cast,"generation",BattleGet("combatGeneration"));
            SetField(cast,"revision",Get(unit,"positionRevision"));Set(unit,"mammothExpulsion",cast);return;
        }
        if(family=="mantis")
        {
            object cast=Activator.CreateInstance(type.GetNestedType("MantisMoltState",Hidden),true);
            SetField(cast,"generation",BattleGet("combatGeneration"));SetField(cast,"fullHp",Get(unit,"maxHp"));Set(unit,"mantisMolt",cast);return;
        }
        if(family=="tyranno")
        {
            object reignState=Call("FamilyTranscendenceStateFor",unit,true);SetField(reignState,"tyrannoStompTime",0f);SetField(reignState,"tyrannoStompHit",false);return;
        }
        if(family=="griffin")
        {
            var winds=(IList)BattleGet("griffinCrosswinds");winds.Clear();object wind=Activator.CreateInstance(type.GetNestedType("GriffinCrosswind",Hidden),true);
            SetField(wind,"owner",unit);SetField(wind,"started",BattleGet("battleElapsed"));
            SetField(wind,"generation",BattleGet("combatGeneration"));winds.Add(wind);return;
        }
        if(family=="beetle")
        {
            object previous=Get(unit,"beetleOverturn");if(previous!=null)Call("EndBeetleOverturn",previous);
            object cast=Activator.CreateInstance(type.GetNestedType("BeetleOverturnState",Hidden),true);
            SetField(cast,"source",unit);SetField(cast,"generation",BattleGet("combatGeneration"));
            var passengers=(IList)Field(cast,"passengers");
            foreach(object enemy in new[]{primary,secondary})
            {
                object p=Activator.CreateInstance(type.GetNestedType("BeetleOverturnPassenger",Hidden),true);
                SetField(p,"unit",enemy);SetField(p,"owner",cast);SetField(p,"from",Model(enemy).transform.position);
                SetField(p,"revision",Get(enemy,"positionRevision"));SetField(p,"seed",passengers.Count);passengers.Add(p);
            }
            Set(unit,"beetleOverturn",cast);((IList)BattleGet("beetleOverturns")).Add(cast);return;
        }
        if(family!="wyvern")return;
        string nested="WyvernHeavenBreath";
        string list="wyvernHeavenBreaths";
        var values=(IList)BattleGet(list);values.Clear();object state=Activator.CreateInstance(type.GetNestedType(nested,Hidden),true);
        SetField(state,"source",unit);SetField(state,"generation",BattleGet("combatGeneration"));
        SetField(state,"started",true);SetField(state,"arena",BattleGet("combatBounds"));
        values.Add(state);
    }

    private object Unit(string species,int team,Vector3 position,bool shiny=false)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species)??
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.FirstOrDefault(value=>value!=null&&value.monsterId==species);
        Assert.That(definition,Is.Not.Null,species);
        string path=shiny&&!string.IsNullOrEmpty(definition.shinyResourcePath)?definition.shinyResourcePath:definition.resourcePath;
        var prefab=Resources.Load<GameObject>(path);Assert.That(prefab,Is.Not.Null);
        var authored=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);Assert.That(authored,Is.Not.Null);
        var model=new GameObject("Remaining rank4 actor "+species);model.transform.position=position;
        var visual=new GameObject("Real authored sprite");visual.transform.SetParent(model.transform,false);
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        driver.Configure(Frames(authored,"idleFrames"),Frames(authored,"runFrames"),Frames(authored,"attackFrames"),Frames(authored,"skillAttackFrames"),
            Frames(authored,"deathFrames"),Frames(authored,"captureFrames"),Frames(authored,"struggleFrames"));
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        visual.transform.rotation=camera.transform.rotation;var ring=new GameObject("Remaining rank4 actor shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",nextUnitId++);Set(unit,"model",model);Set(unit,"ring",ring);Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"definition",definition);Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",definition.skillId);
        Set(unit,"ownedInstance",new OwnedMonsterSave{monsterId=species,isShiny=shiny});
        Team(unit,team);Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);
        Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);Set(unit,"collisionRadius",.3f);
        Set(unit,"skillCooldownDuration",5f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"lastGroundPosition",position);
        Status(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);Units.Add(unit);return unit;
    }

    private bool QueueSource()=>(bool)Call("QueueRank4Cinematic",source,null);
    private void BeginQueued(object unit)
    {
        Assert.That(Queue.Count,Is.GreaterThan(0));object request=Queue[0];Queue.RemoveAt(0);Assert.That(Field(request,"source"),Is.SameAs(unit));
        Assert.That(Call("BeginRank4Cinematic",request),Is.True);Assert.That(Presentation,Is.Not.Null);
    }
    private void AssertNoDamage(){Assert.That(Get(primary,"hp"),Is.EqualTo(5000f));Assert.That(Get(secondary,"hp"),Is.EqualTo(5000f));}
    private object Presentation=>BattleGet("rank4Presentation");
    private Rank4CinematicStage Stage=>(Rank4CinematicStage)Field(Presentation,"stage");
    private IList Queue=>(IList)BattleGet("rank4PresentationQueue");
    private IList Units=>(IList)BattleGet("units");
    private MonsterDefinition Definition(object unit)=>(MonsterDefinition)Get(unit,"definition");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private SpriteRenderer Renderer(object unit)=>((SpriteMonsterAnimationDriver)Get(unit,"spriteAnimation")).GetComponent<SpriteRenderer>();
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private static Sprite[] Frames(SpriteMonsterAnimationDriver authored,string name)=>(Sprite[])Field(authored,name);
    private void Team(object unit,int team)=>Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
    private object Get(object unit,string name)=>unitType.GetField(name,Hidden).GetValue(unit);
    private void Set(object unit,string name,object value)=>unitType.GetField(name,Hidden).SetValue(unit,value);
    private object BattleGet(string name)=>type.GetField(name,Hidden).GetValue(battle);
    private void BattleSet(string name,object value)=>type.GetField(name,Hidden).SetValue(battle,value);
    private static object Field(object value,string name)=>value.GetType().GetField(name,Hidden).GetValue(value);
    private static void SetField(object value,string name,object field)=>value.GetType().GetField(name,Hidden).SetValue(value,field);
    private object Call(string method,params object[] args)=>type.GetMethod(method,Hidden).Invoke(battle,args);
}
