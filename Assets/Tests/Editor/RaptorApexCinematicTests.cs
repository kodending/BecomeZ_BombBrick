using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class RaptorApexCinematicTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private Camera camera;
    private MonsterDefinition definition;
    private object source,target,previousFrontend;
    private float previousSpeed;
    private bool previousTestMode;
    private HashSet<GameObject> existing;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        Assert.That(CombatSkillClock.Active,Is.False,"A previous battle must release its clock.");
        Time.timeScale=1f;CombatEffectTestConfiguration.Active=true;
        var host=Create("Inactive apex cinematic test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);Set(battle,"expeditionRun",new ExpeditionRunSave());
        Set(battle,"combatBounds",new Bounds(Vector3.zero,Vector3.one*40f));
        var cameraHost=Create("Apex cinematic camera");camera=cameraHost.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-10f),Quaternion.Euler(35f,0f,0f));
        camera.fieldOfView=40f;camera.aspect=16f/9f;
        director=cameraHost.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        definition=ScriptableObject.CreateInstance<MonsterDefinition>();
        definition.monsterId="velos";definition.familyId="raptor";definition.skillId="raptor_hunting_leap";
        source=Unit(0,Vector3.zero);target=Unit(1,Vector3.right*.8f);
        UnitSet(source,"target",target);UnitSet(target,"target",source);
    }

    [TearDown]
    public void TearDown()
    {
        if(battle!=null)Call("CancelCommonSkillFocus");
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)
                Object.DestroyImmediate(value);
        if(definition!=null)Object.DestroyImmediate(definition);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"Cinematic cleanup must release the global actor freeze.");
    }

    [TestCase(.5f)] [TestCase(1f)] [TestCase(2f)]
    public void CancelRestoresTheSelectedSpeedAndBothActorsWithoutSpendingTheFourSecondBuff(float speed)
    {
        Time.timeScale=speed;
        Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        object state=State;
        Assert.That(Time.timeScale,Is.Zero);
        Assert.That(CombatSkillClock.Active,Is.True);
        Assert.That(CombatSkillClock.ActorFrozen(UnitGet<GameObject>(source,"model").transform),Is.True);
        Assert.That(CombatSkillClock.ActorFrozen(UnitGet<GameObject>(target,"model").transform),Is.True);
        Assert.That(Field<float>(state,"savedSpeed"),Is.EqualTo(speed));
        Assert.That(Field<CombatSkillClock>(state,"clock").Executing,Is.False);
        Assert.That(UnitGet<float>(source,"raptorApexPredatorTimer"),Is.EqualTo(4f));
        Call("CancelRaptorApexCinematic");
        Assert.That(State,Is.Null);Assert.That(Time.timeScale,Is.EqualTo(speed));
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(director.enabled,Is.True);
        Assert.That(UnitGet<float>(source,"raptorApexPredatorTimer"),Is.EqualTo(4f));
        Call("CancelRaptorApexCinematic");Assert.That(Time.timeScale,Is.EqualTo(speed),"Repeated cancellation must be harmless.");
    }

    [Test]
    public void CleanupDoesNotOverwriteAClockChangedByAnotherOwner()
    {
        Time.timeScale=2f;Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        Time.timeScale=.75f;
        Call("CancelRaptorApexCinematic");
        Assert.That(Time.timeScale,Is.EqualTo(.75f));Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void CancellationRestoresTheCameraInsteadOfLeakingTheCinematicLensIntoCombat()
    {
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        camera.lensShift=new Vector2(.02f,-.01f);Vector2 lens=camera.lensShift;
        Matrix4x4 projection=camera.projectionMatrix;projection.m02+=.035f;camera.projectionMatrix=projection;
        float fov=camera.fieldOfView;
        Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        camera.fieldOfView=23f;
        camera.lensShift=new Vector2(.12f,.08f);
        Call("CancelRaptorApexCinematic");
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.lensShift,Is.EqualTo(lens));
        for(int i=0;i<16;i++)Assert.That(camera.projectionMatrix[i],Is.EqualTo(projection[i]).Within(.00001f),"Projection element "+i);
    }

    [Test]
    public void APreviouslyDisabledCameraDirectorIsNotEnabledByCinematicCleanup()
    {
        director.enabled=false;
        Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        Call("CancelRaptorApexCinematic");
        Assert.That(director.enabled,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void AnEnemyAwakeningDuringAnAuthoredImpactNeverBakesTheShakeIntoTheRestoredProjection()
    {
        Matrix4x4 clean=camera.projectionMatrix;
        director.SetShakeStrength(1f);director.ReportGroundSlam(true);
        float start=Get<float>(director,"shakeStartedAt"),end=Get<float>(director,"shakeUntil");
        typeof(CombatCameraDirector).GetMethod("ApplyShakeProjection",Private).Invoke(director,
            new object[]{Mathf.Lerp(start,end,.1f)});
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(Get<Vector2>(director,"appliedShakeOffset").sqrMagnitude,Is.GreaterThan(0f));
        Assert.That(Call("BeginRaptorApexCinematic",target),Is.True);
        Call("CancelRaptorApexCinematic");
        for(int i=0;i<16;i++)Assert.That(camera.projectionMatrix[i],Is.EqualTo(clean[i]).Within(.00001f),
            "A transient hit shake must not become a permanent projection offset at element "+i);
    }

    [TestCase("CancelCommonSkillFocus")]
    [TestCase("PrepareForSystemMenu")]
    [TestCase("OnDisable")]
    public void ExistingBattleCleanupAlsoReleasesTheCinematicAndItsQueuedSuccessor(string cleanup)
    {
        Time.timeScale=2f;Call("QueueRaptorApexCinematic",target);
        Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        Call(cleanup);
        Assert.That(State,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void GameplayPauseRestoresTheRealSpeedBeforeTheFrontendStoresItsResumeSpeed()
    {
        Time.timeScale=.5f;Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        CombatCameraDirector.PrepareForGameplayPause();
        Assert.That(State,Is.Null);Assert.That(Time.timeScale,Is.EqualTo(.5f));
        Assert.That(Queue,Is.Empty);Assert.That(CombatSkillClock.Active,Is.False);
    }

    [TestCase("death")]
    [TestCase("result")]
    [TestCase("paused")]
    [TestCase("ending")]
    public void InterruptedCinematicCannotLeaveTheFollowingWaveFrozen(string interruption)
    {
        Time.timeScale=2f;Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        if(interruption=="death")UnitSet(source,"alive",false);
        else if(interruption=="result")Set(battle,"resultText","아군 승리");
        else if(interruption=="paused")Set(battle,"paused",true);
        else Set(battle,"battleEnding",true);
        UpdateCinematicAfterPrelude("UpdateRaptorCinematic");
        Assert.That(State,Is.Null);Assert.That(Time.timeScale,Is.EqualTo(2f));
        Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void QueueWaitsUntilTheNextBattleUpdateAndDeduplicatesOneActivation()
    {
        Call("QueueRaptorApexCinematic",source);Call("QueueRaptorApexCinematic",source);
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(State,Is.Null);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(UpdateCinematicAfterPrelude("UpdateRaptorCinematic"),Is.True);
        Assert.That(State,Is.Not.Null);Assert.That(Field<object>(State,"unit"),Is.SameAs(source));
        Assert.That(Queue,Is.Empty);Assert.That(Time.timeScale,Is.Zero);
    }

    [TestCase(false)] [TestCase(true)]
    public void AQueuedRequestLosingItsSourceOrCameraDoesNotStartAnInvisibleFrozenStage(bool missingCamera)
    {
        Call("QueueRaptorApexCinematic",source);
        if(missingCamera)Set(battle,"mainCamera",null);else UnitSet(source,"alive",false);
        UpdateCinematicAfterPrelude("UpdateRaptorCinematic");
        Assert.That(State,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void RefreshingAnAlreadyActiveApexBuffDoesNotQueueAnotherFullCinematic()
    {
        UnitSet(source,"raptorApexPredatorTimer",0f);
        Call("ActivateRaptorApexPredator",source);
        Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(UnitGet<float>(source,"raptorApexPredatorTimer"),Is.EqualTo(4f));
        Call("ActivateRaptorApexPredator",source);
        Assert.That(Queue.Count,Is.EqualTo(1),"Ordinary refreshes and kill extensions must not replay the full stage.");
    }

    [TestCase("velos",0)] [TestCase("velos",1)]
    [TestCase("velok",0)] [TestCase("velok",1)]
    [TestCase("raputo",0)] [TestCase("raputo",1)]
    [TestCase("wilo",0)] [TestCase("wilo",1)]
    public void ExpiringAndReactivatingApexKeepsItsBuffAndWingsWithoutReplayingTheStage(
        string species,int team)
    {
        definition.monsterId=species;
        object actor=team==0?source:target;
        UnitSet(actor,"raptorApexPredatorTimer",0f);
        Call("ActivateRaptorApexPredator",actor);
        Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(UpdateCinematicAfterPrelude("UpdateRaptorCinematic"),Is.True);
        Assert.That(Field<object>(State,"unit"),Is.SameAs(actor));
        State.GetType().GetField("elapsed").SetValue(State,6.1f);
        UpdateCinematicAfterPrelude("UpdateRaptorCinematic");
        Assert.That(State,Is.Null);

        for(int i=0;i<2;i++)
        {
            UnitSet(actor,"raptorApexPredatorTimer",0f);
            UnitGet<PixelRaptorApexWings>(actor,"raptorApexWings").SetAwakened(false);
            Call("ActivateRaptorApexPredator",actor);
            Assert.That(UnitGet<float>(actor,"raptorApexPredatorTimer"),Is.EqualTo(4f));
            Assert.That(UnitGet<PixelRaptorApexWings>(actor,"raptorApexWings").IsAwakened,Is.True);
            Assert.That(Queue,Is.Empty);
            Assert.That(Call("BeginRaptorApexCinematic",actor),Is.False);
            Assert.That(UpdateCinematicAfterPrelude("UpdateRaptorCinematic"),Is.False);
            Assert.That(State,Is.Null);
            Assert.That(Time.timeScale,Is.EqualTo(1f));
            Assert.That(CombatSkillClock.Active,Is.False);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void EachMonsterKeepsItsOwnCinematicAllowanceEvenOnTheSameTeam(bool sameTeam)
    {
        object second=sameTeam?Unit(0,Vector3.left*2f):target;
        Call("QueueRaptorApexCinematic",source);
        Call("QueueRaptorApexCinematic",second);
        Assert.That(Queue.Count,Is.EqualTo(2));
        Assert.That(UpdateCinematicAfterPrelude("UpdateRaptorCinematic"),Is.True);
        Assert.That(Field<object>(State,"unit"),Is.SameAs(source));
        State.GetType().GetField("elapsed").SetValue(State,6.1f);
        UpdateCinematicAfterPrelude("UpdateRaptorCinematic");
        Assert.That(State,Is.Null);
        Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(UpdateCinematicAfterPrelude("UpdateRaptorCinematic"),Is.True);
        Assert.That(Field<object>(State,"unit"),Is.SameAs(second));
    }

    [Test]
    public void CancellingAStartedStageStillConsumesOnlyTheCurrentWavesAllowance()
    {
        Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        Call("CancelRaptorApexCinematic");
        Call("QueueRaptorApexCinematic",source);
        Assert.That(Queue,Is.Empty);
        Assert.That(Call("BeginRaptorApexCinematic",source),Is.False);

        int nextGeneration=Get<int>(battle,"combatGeneration")+1;
        Set(battle,"combatGeneration",nextGeneration);
        Call("QueueRaptorApexCinematic",source);
        Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(UpdateCinematicAfterPrelude("UpdateRaptorCinematic"),Is.True);
        Assert.That(UnitGet<int>(source,"raptorApexCinematicGeneration"),Is.EqualTo(nextGeneration));
    }

    [TestCase(false)] [TestCase(true)]
    public void AFailedStageStartDoesNotConsumeTheWavesAllowance(bool missingCamera)
    {
        SpriteRenderer renderer=UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation")
            .GetComponent<SpriteRenderer>();
        if(missingCamera)Set(battle,"mainCamera",null);else renderer.enabled=false;
        Call("QueueRaptorApexCinematic",source);
        Assert.That(UpdateCinematicAfterPrelude("UpdateRaptorCinematic"),Is.False);
        Assert.That(State,Is.Null);
        Assert.That(UnitGet<int>(source,"raptorApexCinematicGeneration"),Is.EqualTo(int.MinValue));
        Assert.That(CombatSkillClock.Active,Is.False);

        Set(battle,"mainCamera",camera);renderer.enabled=true;
        Call("QueueRaptorApexCinematic",source);
        Assert.That(UpdateCinematicAfterPrelude("UpdateRaptorCinematic"),Is.True);
        Assert.That(Field<object>(State,"unit"),Is.SameAs(source));
    }

    [TestCase("rank3")]
    [TestCase("otherFamily")]
    [TestCase("expired")]
    [TestCase("dead")]
    [TestCase("storage")]
    [TestCase("ml")]
    [TestCase("ending")]
    [TestCase("paused")]
    [TestCase("preparation")]
    public void IneligibleRequestsNeverAcquireTheGlobalClock(string reason)
    {
        if(reason=="rank3")UnitSet(source,"augmentTranscended",false);
        else if(reason=="otherFamily")definition.familyId="bear";
        else if(reason=="expired")UnitSet(source,"raptorApexPredatorTimer",0f);
        else if(reason=="dead")UnitSet(source,"alive",false);
        else if(reason=="storage")UnitSet(source,"inStorage",true);
        else if(reason=="ml")Set(battle,"mlEpisodeActive",true);
        else if(reason=="ending")Set(battle,"battleEnding",true);
        else if(reason=="paused")Set(battle,"paused",true);
        else Set(battle,"battleStarted",false);
        Call("QueueRaptorApexCinematic",source);UpdateCinematicAfterPrelude("UpdateRaptorCinematic");
        Assert.That(Queue,Is.Empty);Assert.That(State,Is.Null);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void EnemyBossUsesTheSameCinematicAndFullWorldFreezeAsAnAlliedRaptor()
    {
        Assert.That(Call("BeginRaptorApexCinematic",target),Is.True);
        Assert.That(Field<object>(State,"unit"),Is.SameAs(target));
        Assert.That(Time.timeScale,Is.Zero);Assert.That(CombatSkillClock.Active,Is.True);
        Assert.That(CombatSkillClock.ActorFrozen(UnitGet<GameObject>(source,"model").transform),Is.True);
    }

    [Test]
    public void LocalAwakeningDimsOnlyWithTheEyeAndStageAndReturnsToAClearBattlefield()
    {
        var shade=typeof(MonsterAutoBattlePrototype).GetMethod("RaptorCinematicShadeAlpha",Static);
        foreach(float time in new[]{0f,.12f,.65f,1.05f,5.85f,6f})
            Assert.That((float)shade.Invoke(null,new object[]{time,true}),Is.Zero,"Battlefield at "+time);
        float previous=0f;
        foreach(float time in new[]{1.10f,1.3f,1.6f})
        {
            float alpha=(float)shade.Invoke(null,new object[]{time,true});
            Assert.That(alpha,Is.GreaterThan(previous));previous=alpha;
        }
        Assert.That(previous,Is.EqualTo(.82f).Within(.000001f));
        Assert.That((float)shade.Invoke(null,new object[]{4.5f,true}),Is.EqualTo(previous));
        Assert.That((float)shade.Invoke(null,new object[]{5.6f,true}),Is.GreaterThan(0f).And.LessThan(previous));
    }

    [Test]
    public void LocalAwakeningKeepsTheBattlefieldActorAtItsRealSizeBeforeTheAuthoredCloseup()
    {
        var scale=typeof(MonsterAutoBattlePrototype).GetMethod("RaptorCinematicBodyScale",Static);
        var driver=UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
        Vector3 original=driver.transform.localScale;
        foreach(float time in new[]{0f,.06f,.12f,.65f,1.05f,1.54f})
            Assert.That((float)scale.Invoke(null,new object[]{time,true}),Is.EqualTo(1f),"Actor at "+time);
        Assert.That(driver.transform.localScale,Is.EqualTo(original));
        Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        Assert.That(driver.transform.localScale,Is.EqualTo(original));
        Assert.That(Time.timeScale,Is.Zero,"The deliberate 4th-rank stage still owns its freeze.");
    }

    [Test]
    public void BattlefieldLensIsRestoredBeforeTheStageRevealsCombat()
    {
        Matrix4x4 projection=camera.projectionMatrix;
        Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        State.GetType().GetField("elapsed").SetValue(State,5.41f);
        UpdateCinematicAfterPrelude("UpdateRaptorCinematic");
        Assert.That(State,Is.Not.Null,"The stage must still cover the returning camera.");
        for(int i=0;i<16;i++)Assert.That(camera.projectionMatrix[i],Is.EqualTo(projection[i]).Within(.0001f));
        Assert.That(Time.timeScale,Is.Zero,"Camera restoration cannot advance the frozen combat.");
    }

    [Test]
    public void FinishingTheStageResumesCombatWithTheEntireApexBuffStillAvailable()
    {
        Time.timeScale=2f;Assert.That(Call("BeginRaptorApexCinematic",source),Is.True);
        State.GetType().GetField("elapsed").SetValue(State,6.1f);
        UpdateCinematicAfterPrelude("UpdateRaptorCinematic");
        Assert.That(State,Is.Null);Assert.That(Time.timeScale,Is.EqualTo(2f));
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(director.enabled,Is.True);
        Assert.That(UnitGet<float>(source,"raptorApexPredatorTimer"),Is.EqualTo(4f));
    }

    [Test]
    public void AWinningRaptorsAuraFadesDuringTheExistingResultMessageWithoutAdvancingItsBuffOrBattleClock()
    {
        var renderer=UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation").GetComponent<SpriteRenderer>();
        var wings=PixelRaptorApexWings.Attach(renderer);UnitSet(source,"raptorApexWings",wings);
        wings.SetAwakened(true);wings.SetReveal(1f);wings.Tick(.05f);
        float opacity=wings.Opacity;
        Set(battle,"battleEnding",true);Set(battle,"battleElapsed",12f);Set(battle,"waveOutcomeTimer",2f);
        Call("EndBattleUnitAction",source);
        Assert.That(wings.IsAwakened,Is.False);Assert.That(wings.IsVisible,Is.True);
        Call("UpdateBattleEnding",0f);
        Assert.That(wings.Opacity,Is.EqualTo(opacity),"A paused ending must not consume the fade.");
        Call("UpdateBattleEnding",.2f);
        Assert.That(wings.Opacity,Is.GreaterThan(0f).And.LessThan(opacity));
        Call("UpdateBattleEnding",PixelRaptorApexWings.FadeDuration);
        Assert.That(wings.IsVisible,Is.False);Assert.That(wings.WingParticles.particleCount,Is.Zero);
        Assert.That(Get<float>(battle,"battleElapsed"),Is.EqualTo(12f));
        Assert.That(UnitGet<float>(source,"raptorApexPredatorTimer"),Is.EqualTo(4f));
        Assert.That(Get<bool>(battle,"battleEnding"),Is.True,"The existing two-second result timing is unchanged.");
        Assert.That(Get<float>(battle,"waveOutcomeTimer"),Is.EqualTo(2f-.2f-PixelRaptorApexWings.FadeDuration).Within(.0001f));
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void OrdinaryRankOneToThreePreparationUsesAlliedSlowMotionWithoutTheApexClock(int rank)
    {
        UnitSet(source,"augmentTranscended",false);UnitSet(source,"raptorApexPredatorTimer",0f);
        UnitSet(source,"skillId","guardian_taunt");UnitSet(source,"expeditionRank",(ExpeditionMonsterRank)rank);
        Get<ExpeditionRunSave>(battle,"expeditionRun").monsters.Add(new ExpeditionMonsterStateSave
            {instanceId=UnitGet<string>(source,"stableId"),monsterId="velos",rank=(ExpeditionMonsterRank)rank});
        Time.timeScale=2f;
        Assert.That(Call("BeginCommonSkillFocus",source,false),Is.True);
        Assert.That(Call("HasSkillCastPresentation",source),Is.True);
        Assert.That(State,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(Time.timeScale,Is.EqualTo(2f*.08f));
        Assert.That(Get<bool>(battle,"allyPreparationSequenceActive"),Is.True);
        Assert.That(CombatSkillClock.Active,Is.False);
    }

    private object State=>Get<object>(battle,"raptorCinematic");
    private IList Queue=>Get<IList>(battle,"raptorCinematicQueue");
    private GameObject Create(string name)=>new GameObject(name);
    private object Unit(int team,Vector3 position)
    {
        Type type=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        object unit=Activator.CreateInstance(type,true);var model=Create("Apex actor "+team);model.transform.position=position;
        var renderer=model.AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
        var driver=model.AddComponent<SpriteMonsterAnimationDriver>();var frames=new[]{renderer.sprite};
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        var ring=Create("Apex actor ring");ring.transform.SetParent(model.transform,false);
        UnitSet(unit,"model",model);UnitSet(unit,"ring",ring);UnitSet(unit,"spriteAnimation",driver);
        UnitSet(unit,"definition",definition);UnitSet(unit,"skillId","raptor_hunting_leap");
        UnitSet(unit,"stableId","apex-cinematic-test-"+team);UnitSet(unit,"displayName","Velos");
        UnitSet(unit,"alive",true);UnitSet(unit,"hp",1000f);UnitSet(unit,"maxHp",1000f);
        UnitSet(unit,"augmentTranscended",true);UnitSet(unit,"raptorApexPredatorTimer",4f);
        UnitSet(unit,"team",Enum.ToObject(type.GetField("team").FieldType,team));
        ((IList)Get<object>(battle,"units")).Add(unit);return unit;
    }
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

    private object Call(string name,params object[] arguments)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private|BindingFlags.Public).Invoke(battle,arguments);
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Private).SetValue(value,data);
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name).GetValue(value);
    private static void UnitSet(object value,string name,object data)=>value.GetType().GetField(name).SetValue(value,data);
    private static T UnitGet<T>(object value,string name)=>(T)value.GetType().GetField(name).GetValue(value);
}
