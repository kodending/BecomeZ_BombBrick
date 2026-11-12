using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class WizardCygnusCinematicTests
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
        var host=new GameObject("Inactive Cygnus integration tests");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",Hidden);
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",false);
        BattleSet("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        camera=new GameObject("Cygnus integration battle camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0,8,-12),Quaternion.Euler(35,0,0));camera.fieldOfView=40f;camera.aspect=16f/9f;
        director=camera.gameObject.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        BattleSet("mainCamera",camera);BattleSet("combatCameraDirector",director);
        source=Unit("kongjwi",0,Vector3.zero);Set(source,"augmentTranscended",true);
        primary=Unit("geomchi",1,Vector3.right);secondary=Unit("tino",1,new Vector3(1.2f,0,.2f));Set(source,"target",primary);
    }

    [TearDown] public void Cleanup()
    {
        if(battle!=null){Call("CancelWizardCygnusCinematic");Call("CancelCommonSkillFocus");Call("ResetFamilyTranscendenceStates");}
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)Object.DestroyImmediate(value);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"A Cygnus stage must never leak the exclusive actor clock.");
    }

    [TestCase("kongjwi",0)][TestCase("kongjwi",1)]
    [TestCase("patjwi",0)][TestCase("patjwi",1)]
    [TestCase("seoljwi",0)][TestCase("seoljwi",1)]
    [TestCase("byeoljwi",0)][TestCase("byeoljwi",1)]
    public void EachSpeciesAndTeamShowsOnlyItsFirstActualCinematicPerWave(string species,int team)
    {
        ((IList)BattleGet("units")).Remove(source);Model(source).SetActive(false);
        source=Unit(species,team,Vector3.zero);Set(source,"augmentTranscended",true);Set(source,"target",primary);
        foreach(object enemy in new[]{primary,secondary})Set(enemy,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
        Trigger();Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(Get(source,"wizardCygnusCinematicGeneration"),Is.Not.EqualTo(BattleGet("combatGeneration")));
        StartStage();Assert.That(Get(source,"wizardCygnusCinematicGeneration"),Is.EqualTo(BattleGet("combatGeneration")));
        Call("FinishWizardCygnusCinematic",true);Call("UpdateWizardCygnusShowers",WizardCygnusShowerRules.DurationForTargets(2)+.001f);
        Trigger();Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);
        Call("ClearWizardCygnusShowers",true);BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        Trigger();Assert.That(Queue.Count,Is.EqualTo(1));StartStage();
    }

    [Test]
    public void QueuingIsIdempotentAndForeignClocksDoNotConsumeTheFirstCinematicOrAdvanceDamage()
    {
        Trigger();Call("QueueWizardCygnusCinematic",source,State);Assert.That(Queue.Count,Is.EqualTo(1));
        var foreign=new CombatSkillClock(camera.transform);
        try
        {
            foreign.Hold();Assert.That(UpdateCinematicAfterPrelude("UpdateWizardCygnusCinematic"),Is.False);
            Call("UpdateWizardCygnusShowers",1f);
            Assert.That(Cinematic,Is.Null);Assert.That(Queue.Count,Is.EqualTo(1));AssertNoDamage();
            Assert.That(Get(source,"wizardCygnusCinematicGeneration"),Is.Not.EqualTo(BattleGet("combatGeneration")));
            Assert.That(Field(State,"released"),Is.False);Assert.That(Field(State,"time"),Is.Zero);
        }
        finally{foreign.End();}
        StartStage();
    }

    [Test]
    public void StageFreezesAllActorsAndOnlyReleasesDamageAfterTheReturnAndTheFirstMeteorArrival()
    {
        Trigger();StartStage();
        foreach(object unit in new[]{source,primary,secondary})Assert.That(CombatSkillClock.ActorFrozen(Model(unit).transform),Is.True);
        var stage=(WizardCygnusCinematicStage)BattleGet("wizardCygnusCinematicStage");
        foreach(float time in new[]{0f,.4f,1.3f,2f,2.59f}){stage.Tick(time,.4f);AssertNoDamage();}
        Assert.That(Field(State,"released"),Is.False);Assert.That(Field(State,"time"),Is.Zero);
        Call("FinishWizardCygnusCinematic",true);Assert.That(Field(State,"released"),Is.True);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));AssertNoDamage();
        Call("UpdateWizardCygnusShowers",WizardCygnusShowerRules.ImpactTime(0,2)-.001f);AssertNoDamage();
        Call("UpdateWizardCygnusShowers",.002f);Assert.That(Hp(primary),Is.EqualTo(4910f).Within(.01f));
        Assert.That(Hp(secondary),Is.EqualTo(5000f));
        Call("UpdateWizardCygnusShowers",WizardCygnusShowerRules.ImpactTime(1,2)-WizardCygnusShowerRules.ImpactTime(0,2));
        Assert.That(Hp(secondary),Is.EqualTo(4910f).Within(.01f));
    }

    [Test]
    public void TheExplicitDurationAutomaticallyCompletesTheReturnAndStartsTheReleasedShower()
    {
        Trigger();StartStage();object cast=State;
        Cinematic.GetType().GetField("elapsed",Hidden).SetValue(Cinematic,WizardCygnusCinematicStage.Duration);
        Assert.That(UpdateCinematicAfterPrelude("UpdateWizardCygnusCinematic"),Is.False);Assert.That(Cinematic,Is.Null);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(Field(cast,"released"),Is.True);AssertNoDamage();
    }

    [TestCase(false)][TestCase(true)]
    public void CancellationRestoresPriorVisibilityCameraAndSpeedWithoutSpendingOrReleasingDamage(bool hidden)
    {
        Time.timeScale=2f;Renderer(source).forceRenderingOff=hidden;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        float fov=camera.fieldOfView;Vector2 lens=camera.lensShift;Matrix4x4 projection=camera.projectionMatrix;
        Trigger();StartStage();Call("CancelWizardCygnusCinematic");
        Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(director.enabled,Is.True);
        Assert.That(Renderer(source).forceRenderingOff,Is.EqualTo(hidden));Assert.That(camera.transform.position,Is.EqualTo(position));
        Assert.That(camera.transform.rotation,Is.EqualTo(rotation));Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.lensShift,Is.EqualTo(lens));
        for(int row=0;row<4;row++)for(int col=0;col<4;col++)Assert.That(camera.projectionMatrix[row,col],Is.EqualTo(projection[row,col]).Within(.00001f));
        Assert.That(Field(State,"released"),Is.False);AssertNoDamage();
    }

    [TestCase("pause")][TestCase("freeze")][TestCase("death")][TestCase("generation")][TestCase("disable")]
    public void InvalidatedStageAlwaysRestoresTheActorAndClock(string reason)
    {
        Trigger();StartStage();object cast=State;
        if(reason=="pause")BattleSet("paused",true);
        else if(reason=="freeze")Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);
        else if(reason=="death")Set(source,"alive",false);
        else if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else Call("OnDisable");
        UpdateCinematicAfterPrelude("UpdateWizardCygnusCinematic");
        Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(director.enabled,Is.True);
        Assert.That(Field(cast,"released"),Is.False);AssertNoDamage();
    }

    [Test]
    public void FrozenQueuedCastersKeepTheirFirstStageAndDoNotReleaseEarly()
    {
        Trigger();Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);
        Assert.That(UpdateCinematicAfterPrelude("UpdateWizardCygnusCinematic"),Is.False);
        Call("UpdateWizardCygnusShowers",WizardCygnusShowerRules.DurationForTargets(2)+.001f);
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(Field(State,"time"),Is.Zero);AssertNoDamage();
        Status(source).Remove(CombatStatusKind.Freeze);StartStage();
    }

    [TestCase("ml")][TestCase("no camera")]
    public void UnavailablePresentationStillReleasesTheSameSkillWithoutHoldingTheWorld(string reason)
    {
        if(reason=="ml")BattleSet("mlEpisodeActive",true);else BattleSet("mainCamera",null);
        Trigger();Assert.That(Queue,Is.Empty);Call("UpdateWizardCygnusShowers",WizardCygnusShowerRules.Preparation);
        Assert.That(Field(State,"released"),Is.True);Assert.That(CombatSkillClock.Active,Is.False);AssertNoDamage();
        Call("UpdateWizardCygnusShowers",WizardCygnusShowerRules.ImpactTime(0,2)-.001f);AssertNoDamage();
        Call("UpdateWizardCygnusShowers",.002f);Assert.That(Hp(primary),Is.EqualTo(4910f).Within(.01f));
    }

    private void AssertNoDamage(){Assert.That(Hp(primary),Is.EqualTo(5000f));Assert.That(Hp(secondary),Is.EqualTo(5000f));}
    private void Trigger()=>Assert.That(Call("BeginWizardCygnusShower",source,GameMasterData.Skill(WizardMouseCombatRules.SkillId)),Is.True);
    private void StartStage()
    {
        Assert.That(UpdateCinematicAfterPrelude("UpdateWizardCygnusCinematic"),Is.True);Assert.That(Cinematic,Is.Not.Null);
        Assert.That(CombatSkillClock.Active,Is.True);Assert.That(Time.timeScale,Is.Zero);
    }    private object Unit(string species,int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);var list=(IList)BattleGet("units");
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null);
        var authored=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);Assert.That(authored,Is.Not.Null);
        var model=new GameObject("Cygnus actor "+species);model.transform.position=position;
        var visual=new GameObject("Authored sprite");visual.transform.SetParent(model.transform,false);
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        driver.Configure(Frames(authored,"idleFrames"),Frames(authored,"runFrames"),Frames(authored,"attackFrames"),
            Frames(authored,"skillAttackFrames"),Frames(authored,"deathFrames"),Frames(authored,"captureFrames"),Frames(authored,"struggleFrames"));
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        visual.transform.rotation=camera.transform.rotation;var ring=new GameObject("Cygnus shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",list.Count);Set(unit,"model",model);Set(unit,"ring",ring);Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"definition",definition);Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",WizardMouseCombatRules.SkillId);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));Set(unit,"alive",true);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);
        Set(unit,"collisionRadius",.3f);Set(unit,"skillCooldownDuration",8f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"lastGroundPosition",position);
        Status(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);list.Add(unit);return unit;
    }
    private object State=>Get(source,"wizardCygnusShower");
    private object Cinematic=>BattleGet("wizardCygnusCinematic");
    private IList Queue=>(IList)BattleGet("wizardCygnusCinematicQueue");
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
