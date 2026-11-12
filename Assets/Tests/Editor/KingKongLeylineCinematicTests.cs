using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class KingKongLeylineCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
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
        var host=new GameObject("Inactive leyline integration tests");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",Hidden);
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",false);
        BattleSet("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        camera=new GameObject("Leyline integration battle camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0,8,-12),Quaternion.Euler(35,0,0));camera.fieldOfView=40f;camera.aspect=16f/9f;
        director=camera.gameObject.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        BattleSet("mainCamera",camera);BattleSet("combatCameraDirector",director);
        source=Unit("lilas",0,Vector3.zero);Set(source,"augmentTranscended",true);
        primary=Unit("geomchi",1,Vector3.right);secondary=Unit("tino",1,new Vector3(1.2f,0,.2f));Set(source,"target",primary);
    }

    [TearDown] public void Cleanup()
    {
        if(battle!=null){Call("CancelKingKongLeylineCinematic");Call("CancelCommonSkillFocus");Call("ResetFamilyTranscendenceStates");}
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)Object.DestroyImmediate(value);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"A leyline stage must never leak the exclusive actor clock.");
    }

    [TestCase("lilas",0)][TestCase("lilas",1)]
    [TestCase("magril",0)][TestCase("magril",1)]
    [TestCase("bingareu",0)][TestCase("bingareu",1)]
    [TestCase("moska",0)][TestCase("moska",1)]
    public void AllFourSpeciesOnBothTeamsPresentOnlyTheirFirstAcceptedAwakeningPerWave(string species,int team)
    {
        ((IList)BattleGet("units")).Remove(source);Model(source).SetActive(false);
        source=Unit(species,team,Vector3.zero);Set(source,"augmentTranscended",true);Set(source,"target",primary);
        foreach(object enemy in new[]{primary,secondary})Set(enemy,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
        Trigger();Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(Field(State,"kingKongCinematicGeneration"),Is.Not.EqualTo(BattleGet("combatGeneration")),"A queued request has not yet shown an awakening.");
        StartStage();Assert.That(Field(State,"kingKongCinematicGeneration"),Is.EqualTo(BattleGet("combatGeneration")));
        Call("FinishKingKongLeylineCinematic",true);Call("CancelKingKongLeylineCinematic");
        Trigger();Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);Assert.That(Field(State,"timer"),Is.EqualTo(6f));
        BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        Trigger();Assert.That(Queue.Count,Is.EqualTo(1));StartStage();
    }

    [Test]
    public void QueuingIsIdempotentAndDoesNotSpendTheFirstAwakeningUntilItCanActuallyBegin()
    {
        Trigger();Call("QueueKingKongLeylineCinematic",source);Assert.That(Queue.Count,Is.EqualTo(1));
        var foreign=new CombatSkillClock(camera.transform);
        try
        {
            foreign.Hold();Assert.That(UpdateCinematicAfterPrelude("UpdateKingKongLeylineCinematic"),Is.False);
            Assert.That(Cinematic,Is.Null);Assert.That(Queue.Count,Is.EqualTo(1));
            Assert.That(Field(State,"kingKongCinematicGeneration"),Is.Not.EqualTo(BattleGet("combatGeneration")));
        }
        finally{foreign.End();}
        Call("CancelKingKongLeylineCinematic");Assert.That(Queue,Is.Empty);
        Trigger();StartStage();
    }

    [Test]
    public void PresentationKeepsAllSixSecondsAndDefersTheFirstPulseAndBasicSplashWithoutAddingDamageOrControl()
    {
        Trigger();AssertFrozenAura();StartStage();
        Assert.That(CombatSkillClock.ActorFrozen(Model(source).transform),Is.True);
        foreach(object enemy in new[]{primary,secondary})Assert.That(CombatSkillClock.ActorFrozen(Model(enemy).transform),Is.True);
        var stage=(KingKongLeylineCinematicStage)BattleGet("kingKongCinematicStage");
        foreach(float time in new[]{0f,.72f,1.1f,1.8f,2.99f})
        {stage.Tick(time,.5f);AssertFrozenAura();}
        Call("FinishKingKongLeylineCinematic",true);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));AssertNoDamageOrControl();
        Assert.That(Field(State,"timer"),Is.EqualTo(6f));
        Call("UpdateFamilyTranscendence",source,.01f);
        Assert.That(Field(State,"timer"),Is.EqualTo(5.99f).Within(.0001f));
        float firstHp=Hp(primary),secondHp=Hp(secondary);
        Assert.That(firstHp,Is.LessThan(5000f));Assert.That(secondHp,Is.LessThan(5000f));
        Call("UpdateFamilyTranscendence",source,.5f);
        Assert.That(Hp(primary),Is.EqualTo(firstHp));Assert.That(Hp(secondary),Is.EqualTo(secondHp),"The cinematic must not manufacture an extra pulse.");
        Call("HandleFamilyTranscendenceBasicAttack",source,primary,100f);
        Assert.That(Hp(primary),Is.EqualTo(firstHp),"The ordinary splash still excludes the primary victim.");
        Assert.That(Hp(secondary),Is.LessThan(secondHp));
    }

    [Test]
    public void SameWaveRecastsRefreshTheExistingAuraWithoutStackingItOrReplayingTheStage()
    {
        Trigger();StartStage();Call("FinishKingKongLeylineCinematic",true);Call("CancelKingKongLeylineCinematic");
        object aura=Field(State,"leylineAura");Call("UpdateFamilyTranscendence",source,1.5f);
        Trigger();Assert.That(Field(State,"leylineAura"),Is.SameAs(aura));Assert.That(Field(State,"timer"),Is.EqualTo(6f));
        Assert.That(Field(State,"pulseTimer"),Is.Zero);Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);
    }

    [Test]
    public void ReachingTheStageDurationAutomaticallyReturnsToBattleWithoutWaitingForAnExternalFinishCall()
    {
        Trigger();StartStage();
        Cinematic.GetType().GetField("elapsed",Hidden).SetValue(Cinematic,KingKongLeylineCinematicStage.Duration);
        Assert.That(UpdateCinematicAfterPrelude("UpdateKingKongLeylineCinematic"),Is.False);
        Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(Field(State,"timer"),Is.EqualTo(6f));AssertNoDamageOrControl();
    }

    [TestCase(false)][TestCase(true)]
    public void CancellationRestoresTheExactPriorVisibilityCameraAndSpeedAndKeepsTheGameplayBuff(bool hidden)
    {
        Time.timeScale=2f;Renderer(source).forceRenderingOff=hidden;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        float fov=camera.fieldOfView;Vector2 lens=camera.lensShift;Matrix4x4 projection=camera.projectionMatrix;
        Trigger();StartStage();Assert.That(Renderer(source).forceRenderingOff,Is.True);
        Call("CancelKingKongLeylineCinematic");Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(director.enabled,Is.True);
        Assert.That(Renderer(source).forceRenderingOff,Is.EqualTo(hidden));Assert.That(camera.transform.position,Is.EqualTo(position));
        Assert.That(camera.transform.rotation,Is.EqualTo(rotation));Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.lensShift,Is.EqualTo(lens));
        for(int row=0;row<4;row++)for(int col=0;col<4;col++)Assert.That(camera.projectionMatrix[row,col],Is.EqualTo(projection[row,col]).Within(.00001f));
        Assert.That(Field(State,"timer"),Is.EqualTo(6f));AssertNoDamageOrControl();
    }

    [TestCase("pause")][TestCase("freeze")][TestCase("death")][TestCase("generation")][TestCase("disable")]
    public void InvalidatedPresentationAlwaysReleasesVisibilityAndTheActorClock(string reason)
    {
        Trigger();StartStage();
        if(reason=="pause")BattleSet("paused",true);
        else if(reason=="freeze")Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);
        else if(reason=="death")Set(source,"alive",false);
        else if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else Call("OnDisable");
        UpdateCinematicAfterPrelude("UpdateKingKongLeylineCinematic");
        Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(director.enabled,Is.True);AssertNoDamageOrControl();
    }

    [Test]
    public void FrozenQueuedSourcesWaitWithoutLosingTheirFirstAwakeningOrTheirBuffTime()
    {
        Trigger();Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);
        Assert.That(UpdateCinematicAfterPrelude("UpdateKingKongLeylineCinematic"),Is.False);Assert.That(Queue.Count,Is.EqualTo(1));AssertFrozenAura();
        Status(source).Remove(CombatStatusKind.Freeze);StartStage();
    }

    [Test]
    public void QueuedFreezeAlsoSuspendsTheVisibleAurasLifetimeAndParticlesAndRestoresTheirAuthoredSpeedOnEveryExit()
    {
        object state=Call("FamilyTranscendenceStateFor",source,true);
        var aura=PixelCombatEffect.PlayKingKongLeylineAura(Model(source).transform,Renderer(source));
        state.GetType().GetField("leylineAura",Hidden).SetValue(state,aura);
        var particles=aura.GetComponentInChildren<ParticleSystem>();var main=particles.main;main.simulationSpeed=.63f;
        Trigger();Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);
        Assert.That(UpdateCinematicAfterPrelude("UpdateKingKongLeylineCinematic"),Is.False);Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(particles.main.simulationSpeed,Is.Zero);
        typeof(PixelKingKongLeylineAura).GetField("elapsed",Hidden).SetValue(aura,5.9f);
        for(int i=0;i<30;i++)typeof(PixelKingKongLeylineAura).GetMethod("LateUpdate",Hidden).Invoke(aura,null);
        Assert.That(Field(aura,"elapsed"),Is.EqualTo(5.9f));Assert.That(Field(aura,"ending"),Is.LessThan(0f));
        Assert.That(Field(State,"timer"),Is.EqualTo(6f));
        Call("CancelKingKongLeylineCinematic");Assert.That(particles.main.simulationSpeed,Is.EqualTo(.63f).Within(.0001f));
        Status(source).Remove(CombatStatusKind.Freeze);Trigger();StartStage();Call("FinishKingKongLeylineCinematic",true);
        Assert.That(Field(State,"leylineAura"),Is.SameAs(aura));Assert.That(aura.gameObject.activeSelf,Is.True);
        Assert.That(particles.main.simulationSpeed,Is.EqualTo(.63f).Within(.0001f));Assert.That(Field(State,"timer"),Is.EqualTo(6f));
    }

    [TestCase("ml")][TestCase("no camera")][TestCase("not battling")]
    public void UnavailablePresentationDoesNotQueueOrBlockTheOrdinarySixSecondAbility(string reason)
    {
        if(reason=="ml")BattleSet("mlEpisodeActive",true);
        else if(reason=="no camera")BattleSet("mainCamera",null);
        else BattleSet("battleStarted",false);
        Trigger();Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Field(State,"timer"),Is.EqualTo(6f));Assert.That(Call("IsKingKongLeylineCinematicPending",source),Is.False);
    }

    private void AssertFrozenAura()
    {
        Call("UpdateFamilyTranscendence",source,1f);Call("HandleFamilyTranscendenceBasicAttack",source,primary,100f);
        Assert.That(Field(State,"timer"),Is.EqualTo(6f));Assert.That(Field(State,"pulseTimer"),Is.Zero);AssertNoDamageOrControl();
    }
    private void AssertNoDamageOrControl()
    {
        foreach(object enemy in new[]{primary,secondary})
        {
            Assert.That(Hp(enemy),Is.EqualTo(5000f));
            foreach(var kind in new[]{CombatStatusKind.Airborne,CombatStatusKind.Knockdown,CombatStatusKind.Freeze,CombatStatusKind.Root})
                Assert.That(Status(enemy).Has(kind),Is.False);
        }
    }
    private void Trigger()=>Call("TriggerFamilyTranscendenceSkill",source);
    private void StartStage()
    {
        Assert.That(UpdateCinematicAfterPrelude("UpdateKingKongLeylineCinematic"),Is.True);Assert.That(Cinematic,Is.Not.Null);
        Assert.That(CombatSkillClock.Active,Is.True);Assert.That(Time.timeScale,Is.Zero);
    }
    private object Unit(string species,int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);var list=(IList)BattleGet("units");
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null);
        var authored=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);Assert.That(authored,Is.Not.Null);
        var model=new GameObject("Leyline actor "+species);model.transform.position=position;
        var visual=new GameObject("Authored sprite");visual.transform.SetParent(model.transform,false);
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        driver.Configure(Frames(authored,"idleFrames"),Frames(authored,"runFrames"),Frames(authored,"attackFrames"),
            Frames(authored,"skillAttackFrames"),Frames(authored,"deathFrames"),Frames(authored,"captureFrames"),Frames(authored,"struggleFrames"));
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        visual.transform.rotation=camera.transform.rotation;var ring=new GameObject("Leyline shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",list.Count);Set(unit,"model",model);Set(unit,"ring",ring);Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"definition",definition);Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId","kingkong_earth_pulse");
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));Set(unit,"alive",true);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);
        Set(unit,"collisionRadius",.3f);Set(unit,"skillCooldownDuration",8f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"lastGroundPosition",position);
        Status(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);list.Add(unit);return unit;
    }
    private object State=>Call("FamilyTranscendenceStateFor",source,false);
    private object Cinematic=>BattleGet("kingKongCinematic");
    private IList Queue=>(IList)BattleGet("kingKongCinematicQueue");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private SpriteRenderer Renderer(object unit)=>((SpriteMonsterAnimationDriver)Get(unit,"spriteAnimation")).GetComponent<SpriteRenderer>();
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
