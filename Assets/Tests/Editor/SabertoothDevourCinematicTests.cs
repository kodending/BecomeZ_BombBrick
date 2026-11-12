using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SabertoothDevourCinematicTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private Camera camera;
    private CombatCameraDirector director;
    private MonsterDefinition raptorDefinition;
    private object source,target,previousFrontend;
    private HashSet<GameObject> existing;
    private readonly List<Object> createdGraphics=new List<Object>();
    private float previousSpeed;
    private bool previousTestMode;

    [SetUp] public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        Assert.That(CombatSkillClock.Active,Is.False);
        Time.timeScale=1f;CombatEffectTestConfiguration.Active=true;
        var host=new GameObject("Inactive sabertooth cinematic integration test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);Set(battle,"expeditionRun",new ExpeditionRunSave());
        Set(battle,"playerProfile",new PlayerProfileSave());
        Set(battle,"combatBounds",new Bounds(Vector3.zero,Vector3.one*40f));
        var cameraHost=new GameObject("Sabertooth cinematic integration camera");camera=cameraHost.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-10f),Quaternion.Euler(35f,0f,0f));
        camera.fieldOfView=40f;camera.aspect=16f/9f;
        director=cameraHost.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        source=Unit(0,Vector3.zero);target=Unit(1,Vector3.right*2f);
        UnitSet(source,"target",target);UnitSet(target,"target",source);
    }

    [TearDown] public void TearDown()
    {
        if(battle!=null)Call("CancelCommonSkillFocus");
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)
                Object.DestroyImmediate(value);
        if(raptorDefinition!=null)Object.DestroyImmediate(raptorDefinition);
        foreach(Object graphic in createdGraphics)if(graphic!=null)Object.DestroyImmediate(graphic);
        createdGraphics.Clear();
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"Every cancellation must release the global actor clock.");
    }

    [Test]
    public void AcceptedCastQueuesWithoutSpendingItsCooldownOrApplyingAnyLiveSkill()
    {
        Assert.That(Call("UseReadySkill",source,false),Is.True);
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(State,Is.Null);
        Assert.That(UnitGet<int>(source,"skillUses"),Is.Zero);
        Assert.That(UnitGet<float>(source,"skillTimer"),Is.Zero);
        Assert.That(UnitGet<object>(source,"sabertoothDevour"),Is.Null);
        Assert.That(UnitGet<float>(target,"hp"),Is.EqualTo(5000f));
        Assert.That(Call("TryUseSkill",source,false),Is.False,"A held cast cannot enter a second preparation path.");
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.True);
        Assert.That(State,Is.Not.Null);Assert.That(Time.timeScale,Is.Zero);
        Assert.That(UnitGet<int>(source,"skillUses"),Is.Zero);
        Assert.That(CombatSkillClock.ActorFrozen(UnitGet<GameObject>(source,"model").transform),Is.True);
        Assert.That(CombatSkillClock.ActorFrozen(UnitGet<GameObject>(target,"model").transform),Is.True);
    }

    [Test]
    public void APostStageDeferredCastReleasesItsOrdinarySkillWithoutAnotherGlobalPrelude()
    {
        QueueCast(source);Queue[0].GetType().GetField("presented").SetValue(Queue[0],true);
        Assert.That(Call("UpdateSabertoothCinematic"),Is.False);
        Assert.That(Get<object>(battle,"rank4SkillPrelude"),Is.Null);
        Assert.That(State,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(UnitGet<int>(source,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitGet<object>(source,"sabertoothDevour"),Is.Not.Null);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
    }

    [TestCase("geomchi",0)] [TestCase("geomchi",1)]
    [TestCase("seola",0)] [TestCase("seola",1)]
    [TestCase("nox",0)] [TestCase("nox",1)]
    [TestCase("terakan",0)] [TestCase("terakan",1)]
    public void EachFamilyMemberOnEitherTeamGetsOneStagePerWaveThenOnlyTheOrdinaryCast(string species,int team)
    {
        object actor=team==0?source:target;
        UnitSet(actor,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        QueueCast(actor);FinishStage();
        Assert.That(UnitGet<int>(actor,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitGet<float>(actor,"skillTimer"),Is.EqualTo(7.5f));
        Assert.That(UnitGet<object>(actor,"sabertoothDevour"),Is.Not.Null);
        Call("EndSabertoothDevour",actor,true);Call("ClearLocalSkillPresentations");
        UnitGet<SpriteMonsterAnimationDriver>(actor,"spriteAnimation").InterruptCurrentAction();
        UnitSet(actor,"skillTimer",0f);
        Assert.That(Call("TryQueueSabertoothCinematic",actor,false),Is.False);
        Assert.That(Queue,Is.Empty);
        Assert.That(Call("BeginSabertoothDevour",actor,Skill),Is.True);
        Assert.That(UnitGet<int>(actor,"skillUses"),Is.EqualTo(2));
        Call("EndSabertoothDevour",actor,true);
        UnitGet<SpriteMonsterAnimationDriver>(actor,"spriteAnimation").InterruptCurrentAction();
        UnitSet(actor,"skillTimer",0f);Set(battle,"combatGeneration",Get<int>(battle,"combatGeneration")+1);
        Assert.That(Call("TryQueueSabertoothCinematic",actor,false),Is.True,"A new wave restores this monster's allowance.");
    }

    [Test]
    public void TheMatchedReturnKeepsTheHeadOpenButPreservesTheSinglePhysicalBiteAndItsBusinessTimeline()
    {
        QueueCast(source);FinishStage();
        object live=UnitGet<object>(source,"sabertoothDevour");
        Assert.That(Field<float>(live,"time"),Is.Zero);
        Assert.That(Field<bool>(live,"cinematicOpen"),Is.True);
        object presentation=Call("SkillPresentationFor",source);
        Assert.That(Field<bool>(presentation,"allySkillEmphasis"),Is.True,
            "The stage replaces preparation, but the ally keeps its ordinary enlarged skill emphasis.");
        Assert.That(Get<float>(UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation"),"skillFocusScale"),Is.EqualTo(1.5f));
        var effect=Field<PixelSabertoothDevour>(live,"effect");Assert.That(effect,Is.Not.Null);
        Assert.That(effect.Age,Is.EqualTo(SabertoothDevourRules.PullStart));
        Assert.That(effect.UpperJaw.enabled,Is.True);Assert.That(effect.LowerJaw.enabled,Is.True);
        Assert.That(UnitGet<int>(source,"skillUses"),Is.EqualTo(1));
        Call("AdvanceSabertoothDevour",source,SabertoothDevourRules.HitTime-.001f);
        Assert.That(UnitGet<float>(target,"hp"),Is.EqualTo(5000f));
        Call("AdvanceSabertoothDevour",source,.002f);
        float damage=(float)Call("SkillPower",source)*1.8f;
        Assert.That(UnitGet<float>(target,"hp"),Is.EqualTo(5000f-damage).Within(.02f));
        Assert.That(UnitGet<int>(source,"skillHits"),Is.EqualTo(1));
        Call("AdvanceSabertoothDevour",source,10f);UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");
        Assert.That(UnitGet<int>(source,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitGet<int>(source,"skillHits"),Is.EqualTo(1));
    }

    [TestCase(.5f)] [TestCase(1f)] [TestCase(2f)]
    public void CancellationRestoresSpeedCameraAndDirectorWithoutExecutingThePendingCast(float speed)
    {
        Time.timeScale=speed;camera.lensShift=new Vector2(.02f,-.03f);
        Matrix4x4 projection=camera.projectionMatrix;float fov=camera.fieldOfView;Vector2 lens=camera.lensShift;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        QueueCast(source);Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.True);
        camera.fieldOfView=23f;camera.lensShift=Vector2.one*.12f;
        Call("CancelCommonSkillFocus");Call("CancelCommonSkillFocus");
        Assert.That(State,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(Time.timeScale,Is.EqualTo(speed));
        Assert.That(director.enabled,Is.True);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.lensShift,Is.EqualTo(lens));
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        for(int i=0;i<16;i++)Assert.That(camera.projectionMatrix[i],Is.EqualTo(projection[i]).Within(.00001f));
        Assert.That(UnitGet<int>(source,"skillUses"),Is.Zero);
    }

    [TestCase("death")] [TestCase("ending")] [TestCase("wave")] [TestCase("paused")] [TestCase("result")]
    public void InterruptionsReleaseTheWorldAndCannotCommitAnOldCast(string interruption)
    {
        QueueCast(source);UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");
        if(interruption=="death")UnitSet(source,"alive",false);
        else if(interruption=="ending")Set(battle,"battleEnding",true);
        else if(interruption=="wave")Set(battle,"combatGeneration",Get<int>(battle,"combatGeneration")+1);
        else if(interruption=="paused")Set(battle,"paused",true);
        else Set(battle,"resultText","승리");
        UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");
        Assert.That(State,Is.Null);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(UnitGet<int>(source,"skillUses"),Is.Zero);
        Assert.That(UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation").GetComponent<SpriteRenderer>().forceRenderingOff,Is.False);
    }

    [TestCase("PrepareForSystemMenu")] [TestCase("OnDisable")]
    public void ExistingCleanupClearsTheCurrentStageAndItsSuccessor(string cleanup)
    {
        QueueCast(source);QueueCast(target);UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");Call(cleanup);
        Assert.That(State,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void GameplayPauseRestoresTheSelectedSpeedBeforeTheFrontendStoresIt()
    {
        Time.timeScale=2f;QueueCast(source);UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");
        CombatCameraDirector.PrepareForGameplayPause();
        Assert.That(State,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(Time.timeScale,Is.EqualTo(2f));
        Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void SerialStagesKeepEveryQueuedActorAndDoNotClobberAnExistingRaptorClock()
    {
        object raptor=Unit(0,Vector3.left*3f);
        raptorDefinition=ScriptableObject.CreateInstance<MonsterDefinition>();
        raptorDefinition.monsterId="velos";raptorDefinition.familyId="raptor";
        raptorDefinition.skillId="raptor_hunting_leap";
        UnitSet(raptor,"definition",raptorDefinition);
        UnitSet(raptor,"skillId","raptor_hunting_leap");UnitSet(raptor,"raptorApexPredatorTimer",4f);
        QueueCast(source);QueueCast(target);
        Assert.That(Call("BeginRaptorApexCinematic",raptor),Is.True);
        object raptorState=Get<object>(battle,"raptorCinematic");
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.False);Assert.That(Queue.Count,Is.EqualTo(2));
        Assert.That(Get<object>(battle,"raptorCinematic"),Is.SameAs(raptorState));
        raptorState.GetType().GetField("elapsed").SetValue(raptorState,6.1f);UpdateCinematicAfterPrelude("UpdateRaptorCinematic");
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.True);Assert.That(Field<object>(State,"unit"),Is.SameAs(source));
        Assert.That(UpdateCinematicAfterPrelude("UpdateRaptorCinematic"),Is.False,"A waiting raptor cannot replace the sabertooth's clock.");
        FinishStage();Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.True);Assert.That(Field<object>(State,"unit"),Is.SameAs(target));
        FinishStage();Assert.That(UnitGet<int>(source,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitGet<int>(target,"skillUses"),Is.EqualTo(1));Assert.That(Queue,Is.Empty);
    }

    [Test]
    public void CrowdControlWaitsWithoutLosingTheAcceptedCastOrBlockingAReadyQueuedActor()
    {
        QueueCast(source);QueueCast(target);
        Statuses(source).ApplyTimed(CombatStatusKind.Freeze,2f,target);
        UnitSet(source,"positionRevision",UnitGet<int>(source,"positionRevision")+1);
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.True);
        Assert.That(Field<object>(State,"unit"),Is.SameAs(target));Assert.That(Queue.Count,Is.EqualTo(1));
        FinishStage();Statuses(source).Remove(CombatStatusKind.Freeze);
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.True,"An accepted cast survives the interrupt's position revision.");
        Assert.That(Field<object>(State,"unit"),Is.SameAs(source));FinishStage();
        Assert.That(UnitGet<int>(source,"skillUses"),Is.EqualTo(1));
    }

    [Test]
    public void ControlArrivingDuringTheStageDefersOneCombinedCastAndItsCreditWithoutReplayingTheStage()
    {
        object commander=Unit(0,Vector3.left*3f);UnitSet(commander,"skillTimer",2f);
        UnitSet(source,"skillTimer",6f);
        Assert.That(Call("TryQueueSabertoothCinematic",source,true),Is.True);
        Type groupType=typeof(MonsterAutoBattlePrototype).GetNestedType("QueuedCombinedSkillGroup",BindingFlags.NonPublic);
        object group=Activator.CreateInstance(groupType,true);
        groupType.GetField("commander").SetValue(group,commander);
        groupType.GetField("perSkill").SetValue(group,.5f);groupType.GetField("maximum").SetValue(group,18f);
        Assert.That(Call("AttachSabertoothCinematicCombinedSkill",source,group),Is.True);
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.True);
        Statuses(source).ApplyTimed(CombatStatusKind.Silence,2f,target);FinishStage();
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(UnitGet<int>(source,"skillUses"),Is.Zero);
        Assert.That((IList)groupType.GetField("committedIds").GetValue(group),Is.Empty);
        Assert.That(UnitGet<float>(commander,"skillTimer"),Is.EqualTo(2f));
        Statuses(source).Remove(CombatStatusKind.Silence);
        object pending=Field<object>(Queue[0],"cast");pending.GetType().GetField("nextAttemptAt").SetValue(pending,0d);
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.False);
        Assert.That(State,Is.Null,"The completed stage must not replay while retrying its approved cast.");
        Assert.That(Queue,Is.Empty);Assert.That(UnitGet<int>(source,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitGet<float>(source,"skillTimer"),Is.EqualTo(7.5f));
        Assert.That(((IList)groupType.GetField("committedIds").GetValue(group)).Count,Is.EqualTo(1));
        Assert.That(UnitGet<float>(commander,"skillTimer"),Is.EqualTo(2.5f));
        UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");Assert.That(UnitGet<float>(commander,"skillTimer"),Is.EqualTo(2.5f));
    }

    [Test]
    public void AQueuedDisplacedCasterStillUpdatesItsForcedMovementBeforeItCanBeginTheStage()
    {
        QueueCast(source);UnitSet(source,"displacing",true);
        UnitSet(source,"displacementStart",Vector3.zero);UnitSet(source,"displacementEnd",Vector3.left*.5f);
        UnitSet(source,"displacementDuration",.1f);
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.False);Assert.That(Queue.Count,Is.EqualTo(1));
        Call("UpdateUnitCore",source,.2f);
        Assert.That(UnitGet<bool>(source,"displacing"),Is.False,
            "A held skill must not return before the existing displacement update can finish.");
        Assert.That(Queue.Count,Is.EqualTo(1));
    }

    [Test]
    public void MissingCameraAfterApprovalFallsBackToOneOrdinaryLiveCastWithoutFreezing()
    {
        QueueCast(source);Set(battle,"mainCamera",null);
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.False);
        Assert.That(State,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(UnitGet<int>(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Field<bool>(UnitGet<object>(source,"sabertoothDevour"),"cinematicOpen"),Is.False);
        Assert.That(UnitGet<int>(source,"sabertoothDevourCinematicGeneration"),Is.EqualTo(int.MinValue));
    }

    [Test]
    public void UnknownPresentationAssetsLeaveTheExistingNormalSkillPathAvailable()
    {
        var definition=ScriptableObject.CreateInstance<MonsterDefinition>();
        try
        {
            definition.monsterId="unreleased_sabertooth";definition.familyId="sabertooth";
            UnitSet(source,"definition",definition);
            Assert.That(Call("TryQueueSabertoothCinematic",source,false),Is.False);
            Assert.That(Queue,Is.Empty);Assert.That(CombatSkillClock.Active,Is.False);
            Assert.That(Time.timeScale,Is.EqualTo(1f));
        }
        finally{Object.DestroyImmediate(definition);}
    }

    [TestCase(false)] [TestCase(true)]
    public void TheBridgeHidesOnlyTheWorldRendererAndRestoresItsOriginalRenderingFlag(bool wasHidden)
    {
        var renderer=UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation").GetComponent<SpriteRenderer>();
        renderer.forceRenderingOff=wasHidden;QueueCast(source);UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");
        Assert.That(renderer.enabled,Is.True);Assert.That(renderer.forceRenderingOff,Is.True);
        Assert.That(Field<Sprite>(State,"pose"),Is.SameAs(renderer.sprite));
        Assert.That(Field<Rect>(State,"entryBody").height,Is.GreaterThan(0f));
        Call("CancelCommonSkillFocus");
        Assert.That(renderer.forceRenderingOff,Is.EqualTo(wasHidden));
        Assert.That(Get<object>(battle,"sabertoothCinematicReturn"),Is.Null);
    }

    [TestCase(false)] [TestCase(true)]
    public void EntryBodyAndReturnMouthMatchTheirWorldAnchorsForEitherFacing(bool left)
    {
        Rect viewport=new Rect(left?.2f:.55f,.25f,.25f,.4f);
        Rect body=new Rect(280f,360f,240f,216f);Vector2 size=new Vector2(1920f,1080f);
        Rect canvas=(Rect)typeof(MonsterAutoBattlePrototype).GetMethod("SabertoothMatchBodyRect",Static)
            .Invoke(null,new object[]{viewport,body,size});
        Rect mapped=new Rect(canvas.position+new Vector2(viewport.x,1f-viewport.yMax)*canvas.size,
            viewport.size*canvas.size);
        Assert.That(mapped.x,Is.EqualTo(body.x).Within(.001f));Assert.That(mapped.y,Is.EqualTo(body.y).Within(.001f));
        Assert.That(mapped.width,Is.EqualTo(body.width).Within(.001f));Assert.That(mapped.height,Is.EqualTo(body.height).Within(.001f));
        Vector2 mouthViewport=new Vector2(left?.24f:.76f,.56f),worldMouth=new Vector2(530f,420f);
        canvas=(Rect)typeof(MonsterAutoBattlePrototype).GetMethod("SabertoothMatchMouthRect",Static)
            .Invoke(null,new object[]{viewport,mouthViewport,body,worldMouth,size});
        Vector2 mouth=canvas.position+new Vector2(mouthViewport.x,1f-mouthViewport.y)*canvas.size;
        Assert.That(Vector2.Distance(mouth,worldMouth),Is.LessThan(.001f));
        Assert.That(viewport.width*canvas.width,Is.EqualTo(body.width).Within(.001f));
    }

    [TestCase(.15f,.5f,180f,180f)] [TestCase(.5f,.18f,120f,250f)]
    public void ReturningDifferentBodyProportionsStillMatchesTheLiveJawWidth(float stageWidth,float stageHeight,
        float liveWidth,float liveHeight)
    {
        Rect viewport=new Rect(.2f,.2f,stageWidth,stageHeight),body=new Rect(100f,250f,liveWidth,liveHeight);
        Vector2 mouthViewport=new Vector2(.7f,.6f),worldMouth=new Vector2(260f,320f);
        Rect canvas=(Rect)typeof(MonsterAutoBattlePrototype).GetMethod("SabertoothMatchMouthRect",Static)
            .Invoke(null,new object[]{viewport,mouthViewport,body,worldMouth,new Vector2(1920f,1080f)});
        Assert.That(viewport.width*canvas.width,Is.EqualTo(liveWidth).Within(.001f));
        Vector2 mouth=canvas.position+new Vector2(mouthViewport.x,1f-mouthViewport.y)*canvas.size;
        Assert.That(Vector2.Distance(mouth,worldMouth),Is.LessThan(.001f));
    }

    [Test]
    public void TheReturningPosePredictsTheActualFirstSkillFrameWithoutChangingItsWorldSpriteOrTransform()
    {
        UnitGet<GameObject>(target,"model").transform.position=Vector3.forward*2f;
        var driver=UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
        var renderer=driver.GetComponent<SpriteRenderer>();
        var texture=new Texture2D(8,8,TextureFormat.RGBA32,false);var pixels=new Color[64];
        for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white;
        texture.SetPixels(pixels);texture.Apply();createdGraphics.Add(texture);
        var wide=Sprite.Create(texture,new Rect(0f,0f,8f,4f),Vector2.one*.5f,8f);
        var tall=Sprite.Create(texture,new Rect(0f,0f,4f,8f),Vector2.one*.5f,8f);
        createdGraphics.Add(wide);createdGraphics.Add(tall);
        var ordinary=new[]{renderer.sprite,renderer.sprite,renderer.sprite,renderer.sprite};
        driver.Configure(ordinary,ordinary,ordinary,new[]{wide,tall,wide,tall},ordinary,ordinary,ordinary);
        QueueCast(source);UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");
        Sprite original=renderer.sprite;Vector3 originalScale=renderer.transform.localScale;
        MethodInfo geometry=typeof(MonsterAutoBattlePrototype).GetMethod("SabertoothReturnGeometry",Private);
        object[] predicted={source,renderer,true,null,null};
        Assert.That(geometry.Invoke(battle,predicted),Is.True);
        Assert.That(renderer.sprite,Is.SameAs(original));Assert.That(renderer.transform.localScale,Is.EqualTo(originalScale));
        Assert.That(UnitGet<int>(source,"skillUses"),Is.Zero);
        FinishStage(true);
        object[] actual={source,renderer,false,null,null};Assert.That(geometry.Invoke(battle,actual),Is.True);
        Rect before=(Rect)predicted[3],after=(Rect)actual[3];
        Assert.That(Vector2.Distance(before.position,after.position),Is.LessThan(.001f));
        Assert.That(Vector2.Distance(before.size,after.size),Is.LessThan(.001f));
        Assert.That(Vector2.Distance((Vector2)predicted[4],(Vector2)actual[4]),Is.LessThan(.001f));
        Assert.That(UnitGet<int>(source,"skillUses"),Is.EqualTo(1));
    }

    [Test]
    public void TheOpaqueReturnRetainsItsStageImageUntilTheLiveJawHasTakenOverBeforeTheNextStage()
    {
        QueueCast(source);QueueCast(target);UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");FinishStage(true);
        object tail=Get<object>(battle,"sabertoothCinematicReturn");Assert.That(tail,Is.Not.Null);
        var stage=Get<SabertoothDevourCinematicStage>(battle,"sabertoothCinematicStage");
        Assert.That(Field<Texture>(tail,"texture"),Is.SameAs(stage.Texture));
        Assert.That(Field<Rect>(tail,"actorViewport").width,Is.GreaterThan(0f));
        Assert.That(stage.Active,Is.False);Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(Call("BeginSabertoothCinematic",Queue[0]),Is.False,
            "A queued successor cannot clear or reuse the retained return texture.");
        Assert.That(UnitGet<int>(source,"skillUses"),Is.EqualTo(1));
        var effect=Field<PixelSabertoothDevour>(UnitGet<object>(source,"sabertoothDevour"),"effect");
        var renderer=UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation").GetComponent<SpriteRenderer>();
        var visible=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Static)
            .Invoke(null,new object[]{renderer.sprite});
        float actualBodyWidth=visible.size.x*Mathf.Abs(renderer.transform.lossyScale.x);
        float expectedJawWidth=actualBodyWidth*Field<float>(tail,"jawBodyWidthRatio");
        Assert.That(renderer.transform.localScale.x,Is.EqualTo(1.5f*1.24f).Within(.001f),
            "The match includes the real SkillAttack authored scale after the ally's focus enlargement.");
        Assert.That(Get<float>(effect,"cinematicHandoffWidth"),Is.EqualTo(expectedJawWidth).Within(.001f));
        Assert.That(effect.UpperJaw.sprite.bounds.size.x*Mathf.Abs(effect.UpperJaw.transform.lossyScale.x),
            Is.EqualTo(expectedJawWidth).Within(.001f));
        Call("AdvanceSabertoothCinematicReturn",.17f);
        Assert.That(Get<object>(battle,"sabertoothCinematicReturn"),Is.SameAs(tail));
        Call("AdvanceSabertoothCinematicReturn",.02f);
        Assert.That(Get<object>(battle,"sabertoothCinematicReturn"),Is.Null);
        Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.True);Assert.That(Field<object>(State,"unit"),Is.SameAs(target));
    }

    [TestCase("cancel")] [TestCase("wave")] [TestCase("paused")]
    public void InterruptedReturnsCannotLeakAStageImageIntoAMenuOrAnotherWave(string reason)
    {
        QueueCast(source);UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");FinishStage(true);
        Assert.That(Get<object>(battle,"sabertoothCinematicReturn"),Is.Not.Null);
        if(reason=="cancel")Call("CancelCommonSkillFocus");
        else
        {
            if(reason=="wave")Set(battle,"combatGeneration",Get<int>(battle,"combatGeneration")+1);
            else Set(battle,"paused",true);
            Call("AdvanceSabertoothCinematicReturn",0f);
        }
        Assert.That(Get<object>(battle,"sabertoothCinematicReturn"),Is.Null);
        Assert.That(UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation").GetComponent<SpriteRenderer>().forceRenderingOff,Is.False);
    }

    private void QueueCast(object actor)=>Assert.That(Call("TryQueueSabertoothCinematic",actor,false),Is.True);
    private void FinishStage(bool keepReturn=false)
    {
        if(State==null)Assert.That(UpdateCinematicAfterPrelude("UpdateSabertoothCinematic"),Is.True);
        Get<SabertoothDevourCinematicStage>(battle,"sabertoothCinematicStage").Tick(3.99f,0f);
        State.GetType().GetField("elapsed").SetValue(State,4.01f);UpdateCinematicAfterPrelude("UpdateSabertoothCinematic");
        Assert.That(State,Is.Null);Assert.That(Time.timeScale,Is.GreaterThan(0f));Assert.That(CombatSkillClock.Active,Is.False);
        if(!keepReturn)Call("ClearSabertoothCinematicReturn");
    }
    private SkillMasterRow Skill=>GameMasterData.Skill(SabertoothCombatRules.SkillId);
    private object State=>Get<object>(battle,"sabertoothCinematic");
    private IList Queue=>Get<IList>(battle,"sabertoothCinematicQueue");
    private CombatStatusState Statuses(object actor)=>UnitGet<CombatStatusState>(actor,"combatStatuses");
    private object Unit(int team,Vector3 position)
    {
        Type type=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        object unit=Activator.CreateInstance(type,true);var model=new GameObject("Devour cinematic actor "+team);
        model.transform.position=position;var renderer=model.AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
        var driver=model.AddComponent<SpriteMonsterAnimationDriver>();
        var frames=new[]{renderer.sprite,renderer.sprite,renderer.sprite,renderer.sprite};
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        var ring=new GameObject("Devour cinematic ring");ring.transform.SetParent(model.transform,false);
        UnitSet(unit,"model",model);UnitSet(unit,"ring",ring);UnitSet(unit,"spriteAnimation",driver);
        UnitSet(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_geomchi"));
        UnitSet(unit,"stableId",Guid.NewGuid().ToString());UnitSet(unit,"skillId",SabertoothCombatRules.SkillId);
        UnitSet(unit,"alive",true);UnitSet(unit,"hp",5000f);UnitSet(unit,"maxHp",5000f);UnitSet(unit,"damage",100f);
        UnitSet(unit,"attackInterval",1f);UnitSet(unit,"attackRange",1);UnitSet(unit,"collisionRadius",.3f);
        UnitSet(unit,"skillCooldownDuration",7.5f);UnitSet(unit,"augmentTranscended",true);
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

    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private|BindingFlags.Public).Invoke(battle,args);
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Private).SetValue(value,data);
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name).GetValue(value);
    private static void UnitSet(object value,string name,object data)=>value.GetType().GetField(name).SetValue(value,data);
    private static T UnitGet<T>(object value,string name)=>(T)value.GetType().GetField(name).GetValue(value);
}
