using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class BearAvatarCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private const string HostName="Inactive bear cinematic integration tests";
    private MonsterAutoBattlePrototype battle;
    private Camera camera;
    private CombatCameraDirector director;
    private Type unitType;
    private object source,primary,secondary,previousFrontend;
    private HashSet<GameObject> existing;
    private float previousSpeed;
    private bool previousTestMode;

    [SetUp] public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Hidden).GetValue(null);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,null);
        Assert.That(CombatSkillClock.Active,Is.False);
        Time.timeScale=1f;CombatEffectTestConfiguration.Active=true;
        var host=new GameObject(HostName);host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",Hidden);
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",false);
        BattleSet("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());
        BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        camera=new GameObject("Bear integration battle camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-12f),Quaternion.Euler(35f,0f,0f));
        camera.fieldOfView=40f;camera.aspect=16f/9f;
        director=camera.gameObject.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        BattleSet("mainCamera",camera);BattleSet("combatCameraDirector",director);
        source=Unit("woongi",0,Vector3.zero);Set(source,"augmentTranscended",true);
        primary=Unit("geomchi",1,Vector3.right);secondary=Unit("tino",1,new Vector3(1.2f,0f,.2f));
        Set(source,"target",primary);
    }

    [TearDown] public void Cleanup()
    {
        if(battle!=null){Call("CancelBearAvatarCinematic");Call("CancelCommonSkillFocus");Call("ResetFamilyTranscendenceStates");}
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)
                Object.DestroyImmediate(value);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"The transformation must always release the battle clock.");
    }

    [TestCase("woongi",0)] [TestCase("woongi",1)]
    [TestCase("dami",0)] [TestCase("dami",1)]
    [TestCase("ironkuma",0)] [TestCase("ironkuma",1)]
    [TestCase("berber",0)] [TestCase("berber",1)]
    public void EachBearAndTeamShowsOnlyItsFirstAcceptedTransformationPerWave(string species,int team)
    {
        ((IList)BattleGet("units")).Remove(source);Model(source).SetActive(false);
        source=Unit(species,team,Vector3.zero);Set(source,"augmentTranscended",true);Set(source,"target",primary);
        foreach(object enemy in new[]{primary,secondary})Set(enemy,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
        Trigger();Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(Field(State,"bearCinematicGeneration"),Is.Not.EqualTo(BattleGet("combatGeneration")));
        StartStage();Assert.That(Field(State,"bearCinematicGeneration"),Is.EqualTo(BattleGet("combatGeneration")));
        CompleteStage();Driver(source).InterruptCurrentAction(true);
        Trigger();Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);
        Assert.That(Field(State,"timer"),Is.EqualTo(6f));
        Set(source,"bearAvatarRoarPending",false);Driver(source).InterruptCurrentAction(true);
        BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        Trigger();Assert.That(Queue.Count,Is.EqualTo(1));StartStage();
    }

    [Test]
    public void QueuedAndActiveTransformationPreserveSixSecondsWithoutPrematureFearOrDamage()
    {
        Trigger();var prepare=RoarRoutine();Assert.That(prepare.MoveNext(),Is.True);
        var foreign=new CombatSkillClock(camera.transform);
        try
        {
            foreign.Hold();Call("QueueBearAvatarCinematic",source);
            Assert.That(UpdateCinematicAfterPrelude("UpdateBearAvatarCinematic"),Is.False);Assert.That(Queue.Count,Is.EqualTo(1));
            Assert.That(Field(State,"bearCinematicGeneration"),Is.Not.EqualTo(BattleGet("combatGeneration")));
            Call("UpdateFamilyTranscendence",source,5f);
            Assert.That(Field(State,"timer"),Is.EqualTo(6f));AssertNoDamageOrFear();
        }
        finally { foreign.End(); }
        StartStage();
        foreach(object unit in new[]{source,primary,secondary})
            Assert.That(CombatSkillClock.ActorFrozen(Model(unit).transform),Is.True);
        Call("UpdateFamilyTranscendence",source,5f);
        Assert.That(Field(State,"timer"),Is.EqualTo(6f));Assert.That(prepare.MoveNext(),Is.True);
        AssertNoDamageOrFear();(prepare as IDisposable)?.Dispose();
    }

    [Test]
    public void AutomaticReturnDispatchesTheExistingRoarExactlyOnceWithoutASecondWindup()
    {
        int applied=0;Status(primary).Applied+=effect=>{if(effect.kind==CombatStatusKind.Fear)applied++;};
        Trigger();var prepare=RoarRoutine();Assert.That(prepare.MoveNext(),Is.True);StartStage();
        CompleteStage();
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Cinematic,Is.Null);Assert.That(Renderer(source).forceRenderingOff,Is.False);
        Assert.That(applied,Is.EqualTo(1));Assert.That(Get(source,"bearAvatarRoarPending"),Is.False);
        Assert.That(prepare.MoveNext(),Is.False,"The old coroutine must not play a second .85-second preparation.");
        Driver(source).NotifySkillAttackHitFrameFromAnimation();UpdateCinematicAfterPrelude("UpdateBearAvatarCinematic");
        Assert.That(applied,Is.EqualTo(1));Assert.That(Field(State,"timer"),Is.EqualTo(6f));
        foreach(object enemy in new[]{primary,secondary})
        {
            Assert.That(Status(enemy).Get(CombatStatusKind.Fear).remaining,Is.EqualTo(1f));
            Assert.That(Hp(enemy),Is.EqualTo(5000f));
        }
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(0f));
    }

    [TestCase(false)] [TestCase(true)]
    public void CancellationRestoresPriorVisibilityCameraAndSpeedAndAllowsTheExistingRoarToResume(bool hidden)
    {
        Time.timeScale=2f;Renderer(source).forceRenderingOff=hidden;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        float fov=camera.fieldOfView;Vector2 lens=camera.lensShift;Matrix4x4 projection=camera.projectionMatrix;
        Trigger();StartStage();
        Call("PrepareBearAvatarCinematicRoar",Cinematic);AssertNoDamageOrFear();
        Call("CancelBearAvatarCinematic");
        Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(director.enabled,Is.True);
        Assert.That(Renderer(source).forceRenderingOff,Is.EqualTo(hidden));
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.lensShift,Is.EqualTo(lens));
        for(int row=0;row<4;row++)for(int col=0;col<4;col++)
            Assert.That(camera.projectionMatrix[row,col],Is.EqualTo(projection[row,col]).Within(.00001f));
        Assert.That(Field(State,"timer"),Is.EqualTo(6f));AssertNoDamageOrFear();
        Assert.That(Get(source,"bearAvatarRoarPending"),Is.True);
        var prepare=RoarRoutine();Assert.That(prepare.MoveNext(),Is.True);
        AdvanceRoar(prepare);Assert.That(Status(primary).Has(CombatStatusKind.Fear),Is.True);
        Assert.That(Get(source,"bearAvatarRoarPending"),Is.False);
    }

    [TestCase("pause")] [TestCase("freeze")] [TestCase("death")]
    [TestCase("generation")] [TestCase("disable")] [TestCase("system menu")]
    public void InvalidatedPresentationReleasesTheActorClockAndDoesNotLeakFear(string reason)
    {
        Trigger();StartStage();
        if(reason=="pause")BattleSet("paused",true);
        else if(reason=="freeze")Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);
        else if(reason=="death")Set(source,"alive",false);
        else if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else if(reason=="system menu")MonsterAutoBattlePrototype.PrepareForRaptorCinematicGameplayPause();
        else Call("OnDisable");
        UpdateCinematicAfterPrelude("UpdateBearAvatarCinematic");
        Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(Renderer(source).forceRenderingOff,Is.False);
        Assert.That(director.enabled,Is.True);AssertNoDamageOrFear();
    }

    [Test]
    public void FrozenQueuedBearWaitsWithoutConsumingItsBuffOrItsFirstPresentation()
    {
        Trigger();Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);
        Assert.That(UpdateCinematicAfterPrelude("UpdateBearAvatarCinematic"),Is.False);
        Call("UpdateFamilyTranscendence",source,5f);
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(Field(State,"timer"),Is.EqualTo(6f));
        Assert.That(Field(State,"bearCinematicGeneration"),Is.Not.EqualTo(BattleGet("combatGeneration")));
        AssertNoDamageOrFear();Status(source).Remove(CombatStatusKind.Freeze);StartStage();
    }

    [TestCase("ml")] [TestCase("no camera")]
    public void UnavailablePresentationKeepsTheExistingTransformationAndOrdinaryRoar(string reason)
    {
        if(reason=="ml")BattleSet("mlEpisodeActive",true);else BattleSet("mainCamera",null);
        Trigger();Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Field(State,"timer"),Is.EqualTo(6f));Assert.That(Driver(source).BearAvatarActive,Is.True);
        var prepare=RoarRoutine();Assert.That(prepare.MoveNext(),Is.True);AssertNoDamageOrFear();
        AdvanceRoar(prepare);Assert.That(Status(primary).Has(CombatStatusKind.Fear),Is.True);
        Assert.That(Hp(primary),Is.EqualTo(5000f));
    }

    private void Trigger()
    {
        LogAssert.Expect(LogType.Error,"Coroutine couldn't be started because the the game object '"+HostName+"' is inactive!");
        Call("BeginBearAvatar",source);
    }
    private void StartStage()
    {
        Assert.That(UpdateCinematicAfterPrelude("UpdateBearAvatarCinematic"),Is.True);Assert.That(Cinematic,Is.Not.Null);
        Assert.That(CombatSkillClock.Active,Is.True);Assert.That(Time.timeScale,Is.Zero);
    }
    private void CompleteStage()
    {
        Cinematic.GetType().GetField("elapsed",Hidden).SetValue(Cinematic,BearAvatarCinematicStage.Duration);
        Assert.That(UpdateCinematicAfterPrelude("UpdateBearAvatarCinematic"),Is.False);
    }
    private IEnumerator RoarRoutine()=>(IEnumerator)Call("PrepareBearAvatarRoar",source,(int)BattleGet("combatGeneration"),(int)Get(source,"positionRevision"));
    private void AdvanceRoar(IEnumerator routine)
    {
        for(int frame=0;frame<180;frame++)
        {
            typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Hidden).Invoke(Driver(source),new object[]{1f/60f});
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Hidden).Invoke(Driver(source),null);
            if(!routine.MoveNext())return;
        }
        Assert.Fail("The existing roar did not complete.");
    }
    private void AssertNoDamageOrFear()
    {
        foreach(object enemy in new[]{primary,secondary})
        {Assert.That(Hp(enemy),Is.EqualTo(5000f));Assert.That(Status(enemy).Has(CombatStatusKind.Fear),Is.False);}
    }
    private object Unit(string species,int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);var list=(IList)BattleGet("units");
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null);
        var authored=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var model=new GameObject("Bear cinematic actor "+species);model.transform.position=position;
        var visual=new GameObject("Authored sprite");visual.transform.SetParent(model.transform,false);
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        driver.Configure(Frames(authored,"idleFrames"),Frames(authored,"runFrames"),Frames(authored,"attackFrames"),
            Frames(authored,"skillAttackFrames"),Frames(authored,"deathFrames"),Frames(authored,"captureFrames"),Frames(authored,"struggleFrames"));
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();
        typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        visual.transform.rotation=camera.transform.rotation;var ring=new GameObject("Bear cinematic shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",list.Count);Set(unit,"model",model);Set(unit,"ring",ring);Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"definition",definition);Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",definition.skillId);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));Set(unit,"alive",true);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);
        Set(unit,"collisionRadius",.3f);Set(unit,"skillCooldownDuration",8f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);
        Set(unit,"lastGroundPosition",position);Status(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);list.Add(unit);return unit;
    }
    private object State=>Call("FamilyTranscendenceStateFor",source,false);
    private object Cinematic=>BattleGet("bearAvatarCinematic");
    private IList Queue=>(IList)BattleGet("bearAvatarCinematicQueue");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private SpriteMonsterAnimationDriver Driver(object unit)=>(SpriteMonsterAnimationDriver)Get(unit,"spriteAnimation");
    private SpriteRenderer Renderer(object unit)=>Driver(unit).GetComponent<SpriteRenderer>();
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private static Sprite[] Frames(SpriteMonsterAnimationDriver authored,string name)=>(Sprite[])Field(authored,name);
    private object Get(object unit,string name)=>unitType.GetField(name,Hidden).GetValue(unit);
    private void Set(object unit,string name,object value)=>unitType.GetField(name,Hidden).SetValue(unit,value);
    private object BattleGet(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).GetValue(battle);
    private void BattleSet(string name,object value)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).SetValue(battle,value);
    private static object Field(object value,string name)=>value.GetType().GetField(name,Hidden).GetValue(value);
    private object UpdateCinematicAfterPrelude(string method)
    {
        object result=Call(method);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        object prelude=typeof(MonsterAutoBattlePrototype).GetField("rank4SkillPrelude",flags).GetValue(battle);
        if(prelude==null)return result;
        prelude.GetType().GetField("elapsed",flags).SetValue(prelude,.451f);
        Call("UpdateRank4SkillPrelude");
        return Call(method);
    }

    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Hidden).Invoke(battle,args);
}
