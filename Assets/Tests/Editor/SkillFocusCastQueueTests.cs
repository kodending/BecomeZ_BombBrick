using System;
using System.Collections;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SkillFocusCastQueueTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject battleHost,cameraHost,sourceModel,targetModel;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private Type unitType;
    private object source,target,previousFrontend;
    private float previousSpeed;
    private int commits;
    private bool playSkillPresentation;

    [SetUp]
    public void SetUp()
    {
        commits=0;playSkillPresentation=false;
        previousSpeed=Time.timeScale;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        battleHost=new GameObject("Inactive focus cast queue test");battleHost.SetActive(false);
        battle=battleHost.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);
        var run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        cameraHost=new GameObject("Focus cast queue camera");
        var camera=cameraHost.AddComponent<Camera>();
        director=cameraHost.AddComponent<CombatCameraDirector>();
        Set(director,"battleCamera",camera);Set(battle,"mainCamera",camera);
        Set(battle,"combatCameraDirector",director);
        director.BeginBattle();Time.timeScale=1f;
        sourceModel=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
        targetModel=new GameObject("Focus cast queue target");
        targetModel.transform.position=Vector3.right*.8f;
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        source=Unit(sourceModel,0);target=Unit(targetModel,1);
        SetUnit(source,"target",target);
        SetUnit(source,"spriteAnimation",sourceModel.GetComponentInChildren<SpriteMonsterAnimationDriver>());

        var field=typeof(MonsterAutoBattlePrototype).GetField("skillExecutor",Private);
        object executor=Activator.CreateInstance(field.FieldType);
        Type context=field.FieldType.GetGenericArguments()[0];
        Type handler=typeof(Func<,,>).MakeGenericType(context,typeof(SkillMasterRow),typeof(bool));
        MethodInfo commit=GetType().GetMethod("Commit",Private).MakeGenericMethod(context);
        Delegate spy=Delegate.CreateDelegate(handler,this,commit);
        field.FieldType.GetMethod("Register").Invoke(executor,new object[]{
            GameMasterData.Skill("guardian_taunt").behavior,MonsterSkillTrigger.Active,spy});
        field.SetValue(battle,executor);
    }

    [TearDown]
    public void TearDown()
    {
        if(battle!=null)Call(battle,"CancelCommonSkillFocus");
        if(director!=null)director.EndBattle();
        var fan=source==null?null:GetUnit<PixelWizardStarFan>(source,"wizardBarrageFan");
        if(fan!=null)Object.DestroyImmediate(fan.gameObject);
        Object.DestroyImmediate(battleHost);Object.DestroyImmediate(sourceModel);
        Object.DestroyImmediate(targetModel);Object.DestroyImmediate(cameraHost);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
        Time.timeScale=previousSpeed;
    }

    [TestCase(.5f)] [TestCase(1f)] [TestCase(2f)]
    public void AValidAlliedCastUsesItsUnscaledDelayAndRestoresTheSelectedSpeedBeforeCommittingOnce(float speed)
    {
        Time.timeScale=speed;playSkillPresentation=true;
        float hitRadius=GetUnit<float>(source,"collisionRadius"),bodyScale=GetUnit<float>(source,"combatBodyScale");
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.True);
        Assert.That(Time.timeScale,Is.EqualTo(speed*.08f).Within(.00001f));
        var driver=GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
        Assert.That(commits,Is.Zero);Assert.That(GetUnit<int>(source,"skillUses"),Is.Zero);
        Assert.That(GetUnit<float>(source,"skillTimer"),Is.Zero);
        Assert.That(GetLocal<object>("pending"),Is.Not.Null);Assert.That(driver.IsSkillAnticipating,Is.True);
        Assert.That(Get<float>(driver,"skillFocusScale"),Is.EqualTo(1.5f));
        Assert.That(GetUnit<float>(source,"collisionRadius"),Is.EqualTo(hitRadius));
        Assert.That(GetUnit<float>(source,"combatBodyScale"),Is.EqualTo(bodyScale));
        SetLocal("startedAt",Time.unscaledTimeAsDouble-.999d);Call(battle,"UpdateCommonSkillFocus");
        Assert.That(commits,Is.Zero,"The cast must still be pending before its own preparation duration has elapsed.");
        MaturePreparation();
        Assert.That(commits,Is.EqualTo(1));Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));
        Assert.That(GetUnit<float>(source,"skillTimer"),Is.EqualTo(4f));
        Assert.That(GetLocal<object>("pending"),Is.Null);Assert.That(driver.IsSkillAnticipating,Is.False);
        Assert.That(Get<float>(driver,"skillFocusScale"),Is.EqualTo(1.5f));
        Assert.That(GetUnit<float>(source,"collisionRadius"),Is.EqualTo(hitRadius));
        Assert.That(GetUnit<float>(source,"combatBodyScale"),Is.EqualTo(bodyScale));
        Call(battle,"UpdateCommonSkillFocus");Call(battle,"UpdateCommonSkillFocus");
        Assert.That(commits,Is.EqualTo(1));Assert.That(Time.timeScale,Is.EqualTo(speed));
        Assert.That(director.SkillFocusActive,Is.False);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(typeof(MonsterAutoBattlePrototype).GetField("skillFocusShade",Private),Is.Null);
        Assert.That(GameObject.Find("Skill Focus Shade"),Is.Null);
    }

    [Test]
    public void PreparingOnlyHoldsTheCasterBodyWhileBothActorsCooldownsAndStatusesContinue()
    {
        // The inactive fixture has no generated arena; valid bounds prevent its safety clamp from moving the held body.
        Set(battle,"combatBounds",new Bounds(Vector3.zero,new Vector3(24f,10f,18f)));
        foreach(object unit in new[]{source,target})
        {
            Vector3 unitPosition=GetUnit<GameObject>(unit,"model").transform.position;
            Assert.That(Call(battle,"ResolveFreePosition",unitPosition,GetUnit<float>(unit,"collisionRadius")),Is.EqualTo(unitPosition));
        }
        SetUnit(source,"cooldown",4f);SetUnit(target,"cooldown",4f);SetUnit(target,"skillTimer",7f);
        SetUnit(target,"attackMovementLockTimer",1f);
        foreach(object unit in new[]{source,target})
            GetUnit<CombatStatusState>(unit,"combatStatuses").ApplyTimed(CombatStatusKind.AttackSpeedDown,2f);
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.True);
        Vector3 position=sourceModel.transform.position;
        Call(battle,"UpdateUnit",source,.1f);Call(battle,"UpdateUnit",target,.1f);
        Assert.That(sourceModel.transform.position,Is.EqualTo(position));
        Assert.That(GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation").IsSkillAnticipating,Is.True);
        foreach(object unit in new[]{source,target})
        {
            Assert.That(GetUnit<float>(unit,"cooldown"),Is.LessThan(4f));
            Assert.That(GetUnit<CombatStatusState>(unit,"combatStatuses").Get(CombatStatusKind.AttackSpeedDown).remaining,Is.LessThan(2f));
        }
        Assert.That(GetUnit<float>(target,"skillTimer"),Is.LessThan(7f));
        Assert.That(commits,Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(.08f).Within(.00001f));
        MaturePreparation();Assert.That(commits,Is.EqualTo(1));
    }

    [TestCase(0,false)] [TestCase(1,false)]
    [TestCase(0,true)] [TestCase(1,true)]
    public void PreparingACastPreservesSkillHitSlidingAndCommittedTargets(int team,bool hitBeforePreparation)
    {
        SetSourceTeam(team);playSkillPresentation=true;
        Set(battle,"combatBounds",new Bounds(Vector3.zero,new Vector3(24f,10f,18f)));
        float side=team==0?-1f:1f;
        sourceModel.transform.position=Vector3.right*(side*4f);
        targetModel.transform.position=Vector3.left*(side*4f);
        SetUnit(target,"target",source);SetUnit(target,"skillTimer",100f);
        foreach(object unit in new[]{source,target})
        {
            SetUnit(unit,"speedTilesPerSecond",2f);
            SetUnit(unit,"cooldown",100f);SetUnit(unit,"targetLockTimer",10f);
        }
        float gap=(float)Call(battle,"MeleeContactDistance",source,target);
        Vector3 sourceGoal=Vector3.right*(side*gap*.5f);
        Vector3 targetGoal=-sourceGoal;
        sourceModel.transform.position=sourceGoal;targetModel.transform.position=targetGoal;
        Vector3 away=(sourceGoal-targetGoal).normalized;
        if(hitBeforePreparation)Call(battle,"BeginHitSlide",target,source,false,true);
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.True);
        if(!hitBeforePreparation)Call(battle,"BeginHitSlide",target,source,false,true);
        float initialSlide=GetUnit<Vector3>(source,"hitSlideVelocity").magnitude;
        Assert.That(initialSlide,Is.GreaterThan(0f),"Starting preparation must preserve an already seeded skill impact.");
        var driver=GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
        object pending=GetLocal<object>("pending");float maximumTravel=0f;
        for(int frame=0;frame<15;frame++)
        {
            const float dt=1f/60f;
            Call(battle,"UpdateUnit",source,dt);Call(battle,"UpdateUnit",target,dt);
            Advance(driver,dt);
            maximumTravel=Mathf.Max(maximumTravel,Vector3.Dot(sourceModel.transform.position-sourceGoal,away));
            Assert.That(driver.IsSkillAnticipating,Is.True);
            Assert.That(GetLocal<object>("pending"),Is.SameAs(pending));
            Assert.That(Vector3.Distance(sourceModel.transform.position,targetModel.transform.position),Is.GreaterThan(.5f));
            Assert.That(GetUnit<object>(source,"target"),Is.SameAs(target));
            Assert.That(GetUnit<object>(target,"target"),Is.SameAs(source));
            Assert.That(GetUnit<int>(source,"basicAttackCount"),Is.Zero);
        }
        Assert.That(maximumTravel,Is.GreaterThan(.15f),
            "The full combat loop must move the ground body while the original preparation remains active.");
        Assert.That(GetUnit<Vector3>(source,"hitSlideVelocity").magnitude,Is.GreaterThan(0f).And.LessThan(initialSlide));
        Assert.That(commits,Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(team==0?.08f:1f).Within(.00001f));
        MaturePreparation();
        Assert.That(commits,Is.EqualTo(1));Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));
        Assert.That(GetLocal<object>("pending"),Is.Null);Assert.That(driver.IsSkillAnticipating,Is.False);
        Call(battle,"UpdateCommonSkillFocus");Call(battle,"UpdateCommonSkillFocus");
        Assert.That(commits,Is.EqualTo(1));Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void PreparingACastLetsActualWalkingMomentumFinishWithoutRestartingThePreparation()
    {
        playSkillPresentation=true;
        Set(battle,"combatBounds",new Bounds(Vector3.zero,new Vector3(24f,10f,18f)));
        float distance=(float)Call(battle,"MeleeContactDistance",source,target)+.25f;
        sourceModel.transform.position=Vector3.left*(distance*.5f);
        targetModel.transform.position=Vector3.right*(distance*.5f);
        SetUnit(source,"moving",true);SetUnit(source,"velocity",Vector3.right*2f);
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.True);
        object pending=GetLocal<object>("pending");
        float seed=GetUnit<Vector3>(source,"inertiaVelocity").magnitude;
        Vector3 before=sourceModel.transform.position;
        Call(battle,"UpdateUnit",source,.1f);
        Assert.That(sourceModel.transform.position.x-before.x,Is.GreaterThan(.05f));
        Assert.That(GetUnit<Vector3>(source,"inertiaVelocity").magnitude,Is.GreaterThan(0f).And.LessThan(seed));
        Assert.That(GetLocal<object>("pending"),Is.SameAs(pending));
        Assert.That(GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation").IsSkillAnticipating,Is.True);
        Assert.That(commits,Is.Zero);
        MaturePreparation();Assert.That(commits,Is.EqualTo(1));
    }

    [TestCase("source death")] [TestCase("removed")] [TestCase("battle end")]
    [TestCase("explicit cancel")] [TestCase("disabled")]
    public void EndingOrExplicitlyCancellingPreparationDoesNotConsumeTheSkill(string cause)
    {
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.True);
        var driver=GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
        if(cause=="source death")SetUnit(source,"alive",false);
        else if(cause=="removed")SetUnit(source,"inStorage",true);
        else if(cause=="battle end")Set(battle,"battleEnding",true);
        if(cause=="explicit cancel")Call(battle,"CancelCommonSkillFocus");
        else if(cause=="disabled")Call(battle,"OnDisable");
        else MaturePreparation();
        Assert.That(commits,Is.Zero);Assert.That(GetUnit<int>(source,"skillUses"),Is.Zero);
        Assert.That(GetUnit<float>(source,"skillTimer"),Is.Zero);
        AssertVisualsCleared(driver);Assert.That(driver.IsSkillAnticipating,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase("silence")] [TestCase("freeze")] [TestCase("timer stun")]
    [TestCase("dragged")] [TestCase("held")] [TestCase("action lock")]
    [TestCase("pause")] [TestCase("system menu")]
    public void AnAcceptedSkillWaitsThroughTemporaryControlAndPauseWithoutRestartingItsNotice(string cause)
    {
        playSkillPresentation=true;
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.True);
        object state=Presentation,request=GetLocal<object>("pending");
        var driver=GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
        if(cause=="silence"||cause=="freeze")GetUnit<CombatStatusState>(source,"combatStatuses")
            .ApplyTimed(cause=="silence"?CombatStatusKind.Silence:CombatStatusKind.Freeze,2f);
        else if(cause=="timer stun")SetUnit(source,"giraffeStunTimer",1f);
        else if(cause=="dragged")SetUnit(source,"preyDraggedBy",target);
        else if(cause=="held")SetUnit(source,"griffinHeldBy",target);
        else if(cause=="action lock")driver.PlayBasicAttack(1f,null);
        else
        {
            Time.timeScale=0f;
            if(cause=="pause")Set(battle,"paused",true);else battle.PrepareForSystemMenu();
        }
        MaturePreparation();double started=GetLocal<double>("startedAt");
        Assert.That(commits,Is.Zero);Assert.That(GetLocal<bool>("active"),Is.True);
        Assert.That(Presentation,Is.SameAs(state));Assert.That(GetLocal<object>("pending"),Is.SameAs(request));
        Assert.That(GetUnit<int>(source,"skillUses"),Is.Zero);
        if(cause=="silence"||cause=="freeze")GetUnit<CombatStatusState>(source,"combatStatuses")
            .Remove(cause=="silence"?CombatStatusKind.Silence:CombatStatusKind.Freeze);
        else if(cause=="timer stun")SetUnit(source,"giraffeStunTimer",0f);
        else if(cause=="dragged")SetUnit(source,"preyDraggedBy",null);
        else if(cause=="held")SetUnit(source,"griffinHeldBy",null);
        else if(cause=="action lock")Advance(driver,driver.BasicAttackDuration+1f);
        else {Time.timeScale=1f;Set(battle,"paused",false);}
        Call(battle,"UpdateCommonSkillFocus");Call(battle,"UpdateCommonSkillFocus");
        Assert.That(Presentation,Is.SameAs(state));Assert.That(GetLocal<double>("startedAt"),Is.EqualTo(started));
        Assert.That(GetLocal<object>("pending"),Is.Null);Assert.That(commits,Is.EqualTo(1));
        Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));Assert.That(driver.IsSkillActionActive,Is.True);
    }

    [Test]
    public void AQueuedSkillLetsForcedDisplacementFinishBeforeReleasingOnce()
    {
        Set(battle,"combatBounds",new Bounds(Vector3.zero,Vector3.one*100f));
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.True);
        object request=GetLocal<object>("pending");
        SetUnit(source,"displacing",true);SetUnit(source,"displacementStart",Vector3.zero);
        SetUnit(source,"displacementEnd",Vector3.left);SetUnit(source,"displacementDuration",.15f);
        Call(battle,"UpdateUnit",source,.05f);
        Assert.That(GetUnit<float>(source,"displacementProgress"),Is.GreaterThan(0f));
        Assert.That(GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation").IsSkillAnticipating,Is.False);
        Assert.That(GetLocal<object>("pending"),Is.SameAs(request));Assert.That(commits,Is.Zero);
        Call(battle,"UpdateUnit",source,.2f);
        Assert.That(GetUnit<bool>(source,"displacing"),Is.False);
        MaturePreparation();Assert.That(commits,Is.EqualTo(1));
        Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));
    }

    [TestCase(FamilySharedSkillSetup.WizardMouseSkillId,"wizardBarrageDamageMultiplier")]
    [TestCase(FamilySharedSkillSetup.FrogSkillId,"tongueHarpoonDamageMultiplier")]
    [TestCase(FamilySharedSkillSetup.MushroomSkillId,"mushroomColonyDamageMultiplier")]
    public void ARealRepeatedRangedSkillStartsItsAnimationAndDispatchesItsImpactExactlyOnce(string skillId,string multiplierField)
    {
        SetUnit(source,"skillId",skillId);SetUnit(source,"skillTimer",4f);
        SetUnit(source,"skillTraitDamageMultiplier",1.6f);Call(battle,"ConfigureSkillExecutor");
        Assert.That(Call(battle,"QueueRepeatedSkillFocus",source,target,.5f),Is.True);
        MaturePreparation();
        var driver=GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
        Assert.That(driver.IsSkillActionActive,Is.True);Assert.That(driver.CurrentMotionName,Is.EqualTo("SkillAttack"));
        Assert.That(driver.HasPendingSkillAttackHit,Is.True);
        Assert.That(Get<Action>(driver,"pendingSkillAttackHit"),Is.Not.Null);
        Assert.That(GetUnit<float>(source,multiplierField),Is.EqualTo(.8f).Within(.00001f));
        Assert.That(GetUnit<float>(source,"skillTraitDamageMultiplier"),Is.EqualTo(1.6f));
        Assert.That(GetUnit<float>(source,"skillTimer"),Is.EqualTo(4f));
        // Observe the real animation-event dispatch without starting coroutines on an inactive EditMode host.
        int impacts=0;Set(driver,"pendingSkillAttackHit",(Action)(()=>impacts++));
        driver.NotifySkillAttackHitFrameFromAnimation();driver.NotifySkillAttackHitFrameFromAnimation();
        Assert.That(impacts,Is.EqualTo(1));Assert.That(driver.HasPendingSkillAttackHit,Is.False);
        Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));
    }

    [Test]
    public void ARealRepeatedGriffinDiveStartsAndDealsLandingDamageOnlyOnce()
    {
        Set(battle,"combatBounds",new Bounds(Vector3.zero,Vector3.one*100f));
        SetUnit(source,"skillId",FamilySharedSkillSetup.GriffinSkillId);
        SetUnit(source,"skillTimer",4f);SetUnit(source,"cooldown",100f);
        SetUnit(source,"skillTraitDamageMultiplier",1.6f);Call(battle,"ConfigureSkillExecutor");
        Assert.That(Call(battle,"QueueRepeatedSkillFocus",source,target,.5f),Is.True);
        MaturePreparation();
        Assert.That(GetUnit<bool>(source,"griffinSkyfalling"),Is.True);
        Assert.That(GetUnit<bool>(source,"griffinSkyfallPending"),Is.False);
        Assert.That(GetUnit<float>(source,"griffinSkyfallDamageMultiplier"),Is.EqualTo(.8f).Within(.00001f));
        Assert.That(GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation").IsSkillActionActive,Is.True);
        Call(battle,"UpdateGriffinSkyfall",source,10f);
        float afterLanding=GetUnit<float>(target,"hp");
        Assert.That(afterLanding,Is.LessThan(5000f));Assert.That(GetUnit<bool>(source,"griffinSkyfalling"),Is.False);
        Assert.That(GetUnit<int>(source,"skillHits"),Is.EqualTo(1));
        Call(battle,"UpdateUnit",source,.1f);
        Assert.That(GetUnit<float>(target,"hp"),Is.EqualTo(afterLanding));
        Assert.That(GetUnit<int>(source,"skillHits"),Is.EqualTo(1));
        Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));
    }

    [TestCase("source death")] [TestCase("silence")] [TestCase("target death")]
    [TestCase("timer stun")] [TestCase("dragged")] [TestCase("held")]
    public void AnInvalidCastCannotConsumeTheSkillOrStartAnEmphasis(string cause)
    {
        if(cause=="source death")SetUnit(source,"alive",false);
        else if(cause=="target death")SetUnit(target,"alive",false);
        else if(cause=="timer stun")SetUnit(source,"giraffeStunTimer",1f);
        else if(cause=="dragged")SetUnit(source,"preyDraggedBy",target);
        else if(cause=="held")SetUnit(source,"griffinHeldBy",target);
        else GetUnit<CombatStatusState>(source,"combatStatuses").ApplyTimed(CombatStatusKind.Silence,2f);
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.False);
        Assert.That(commits,Is.Zero);Assert.That(GetUnit<int>(source,"skillUses"),Is.Zero);
        Assert.That(GetUnit<float>(source,"skillTimer"),Is.Zero);Assert.That(Pool.Count,Is.Zero);
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void TheCasterKeepsItsEmphasisForItsWholeBodyActionThenRestoresItsScale()
    {
        var driver=CommitSkill(true);SetLocal("committedAt",Time.timeAsDouble-20d);
        Call(battle,"UpdateCommonSkillFocus");
        Assert.That(driver.IsSkillActionActive,Is.True);Assert.That(Get<float>(driver,"skillFocusScale"),Is.EqualTo(1.5f));
        Assert.That(GetLocal<bool>("active"),Is.True);Assert.That(Time.timeScale,Is.EqualTo(1f));
        FinishPresentation(driver);AssertVisualsCleared(driver);Assert.That(commits,Is.EqualTo(1));
    }

    [Test]
    public void ReleasedSkillDelayAndOrdinaryMovementLocksDoNotExtendTheCasterPresentation()
    {
        var driver=CommitSkill(false);driver.InterruptCurrentAction(true);
        SetUnit(source,"frogHopping",true);SetUnit(source,"displacing",true);
        SetUnit(source,"attackMovementLockTimer",20f);SetUnit(source,"rank3SkillLockTimer",20f);
        SetUnit(source,"skillEchoTimer",20f);FinishPresentation(driver);AssertVisualsCleared(driver);
        Assert.That(GetUnit<float>(source,"skillEchoTimer"),Is.EqualTo(20f));
    }

    [Test]
    public void ALongBearBuffDoesNotExtendTheCompletedCastingBodyEmphasis()
    {
        var driver=CommitSkill(true);SetUnit(source,"bearResolveTimer",12f);
        FinishPresentation(driver);AssertVisualsCleared(driver);
        Assert.That(GetUnit<float>(source,"bearResolveTimer"),Is.EqualTo(12f));Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase("monkeyNimbus")] [TestCase("wyvernBreath")]
    public void AChannelKeepsItsEmphasisAfterThePoseEndsUntilTheChannelEndsWithoutStoppingOthers(string field)
    {
        var driver=CommitSkill(false);driver.InterruptCurrentAction(true);
        object channel=NewUnitState(field);SetUnit(source,field,channel);SetLocal("committedAt",Time.timeAsDouble-.2d);
        Call(battle,"UpdateCommonSkillFocus");
        Assert.That(driver.IsSkillActionActive,Is.False);Assert.That(GetLocal<bool>("active"),Is.True);
        Assert.That(Get<float>(driver,"skillFocusScale"),Is.EqualTo(1.5f));
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
        if(field=="monkeyNimbus")SetUnit(source,field,null);else channel.GetType().GetField("finished").SetValue(channel,true);
        FinishPresentation(driver);AssertVisualsCleared(driver);
    }

    [Test]
    public void CasterOwnedChannelsStayActiveWhileRetainedCompletedCastsAndBasicReaperActionsDoNot()
    {
        Assert.That(CommonSkillActionActive(),Is.False);
        SetUnit(source,"monkeyNimbus",NewUnitState("monkeyNimbus"));
        Assert.That(CommonSkillActionActive(),Is.True,"Nimbus deliberately uses normal Idle/Run poses.");
        SetUnit(source,"monkeyNimbus",null);
        foreach(string name in new[]{"wyvernBreath","tyrannoMaw","mantisGuillotine","mammothToss"})
        {
            object state=NewUnitState(name);SetUnit(source,name,state);
            Assert.That(CommonSkillActionActive(),Is.True,name);
            state.GetType().GetField("finished").SetValue(state,true);
            Assert.That(CommonSkillActionActive(),Is.False,name+" finished");
            SetUnit(source,name,null);
        }
        object roar=NewUnitState("haetaeRoar");SetUnit(source,"haetaeRoar",roar);
        Assert.That(CommonSkillActionActive(),Is.True);
        roar.GetType().GetField("completed").SetValue(roar,true);
        Assert.That(CommonSkillActionActive(),Is.False);
        roar.GetType().GetField("completed").SetValue(roar,false);
        roar.GetType().GetField("cancelled").SetValue(roar,true);
        Assert.That(CommonSkillActionActive(),Is.False);
        SetUnit(source,"haetaeRoar",null);
        object reaper=NewUnitState("mantisReaperAction");SetUnit(source,"mantisReaperAction",reaper);
        foreach(int kind in new[]{0,1,2})
        {
            reaper.GetType().GetField("kind").SetValue(reaper,kind);
            Assert.That(CommonSkillActionActive(),Is.EqualTo(kind==2),"Reaper action "+kind);
        }
        SetUnit(source,"mantisReaperAction",null);
    }

    [TestCase("source death")] [TestCase("battle end")] [TestCase("explicit cancel")] [TestCase("disabled")]
    public void PresentationCleanupRestoresScaleAndVisualsWithoutChangingTheSelectedSpeed(string exit)
    {
        var driver=CommitSkill(true);Assert.That(Get<float>(driver,"skillFocusScale"),Is.EqualTo(1.5f));
        if(exit=="source death"){SetUnit(source,"alive",false);Call(battle,"UpdateCommonSkillFocus");}
        else if(exit=="battle end"){Set(battle,"battleEnding",true);Call(battle,"UpdateCommonSkillFocus");}
        else if(exit=="explicit cancel")Call(battle,"CancelCommonSkillFocus");else Call(battle,"OnDisable");
        AssertVisualsCleared(driver);Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(commits,Is.EqualTo(1));
    }

    [TestCase("Freeze",0)] [TestCase("Freeze",1)]
    [TestCase("Fear",0)] [TestCase("Fear",1)]
    [TestCase("Knockdown",0)] [TestCase("Knockdown",1)]
    [TestCase("Airborne",0)] [TestCase("Airborne",1)]
    [TestCase("timer stun",0)] [TestCase("timer stun",1)]
    [TestCase("dragged",0)] [TestCase("dragged",1)]
    [TestCase("held",0)] [TestCase("held",1)]
    [TestCase("source death",0)] [TestCase("source death",1)]
    public void EndingACasterPresentationNeverInvalidatesItsReleasedProjectile(string cause,int team)
    {
        SetSourceTeam(team);var driver=CommitSkill(true);
        var owner=sourceModel.AddComponent<CombatProjectileSource>();owner.battle=battle;owner.team=team;
        var clock=new CombatSkillClock(sourceModel.transform);CombatProjectileSource.Flight flight;
        using(CombatSkillClock.Enter(clock))flight=CombatProjectileSource.Capture(sourceModel.transform);
        Assert.That(flight.valid(),Is.True);Assert.That(Get<int[]>(battle,"releasedEffectCounts")[team],Is.EqualTo(1));
        if(cause=="source death")SetUnit(source,"alive",false);
        else if(cause=="timer stun")SetUnit(source,"giraffeStunTimer",1f);
        else if(cause=="dragged")SetUnit(source,"preyDraggedBy",target);
        else if(cause=="held")SetUnit(source,"griffinHeldBy",target);
        else GetUnit<CombatStatusState>(source,"combatStatuses").ApplyTimed((CombatStatusKind)Enum.Parse(typeof(CombatStatusKind),cause),2f,target);
        driver.InterruptCurrentAction(true);FinishPresentation(driver);AssertVisualsCleared(driver);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(commits,Is.EqualTo(1));
        Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));Assert.That(flight.valid(),Is.True);
        Assert.That(flight.skillClock,Is.SameAs(clock));Assert.That(flight.clock(),Is.EqualTo(clock.Time));
        Assert.That(Get<int[]>(battle,"releasedEffectCounts")[team],Is.EqualTo(1));
        flight.finished();Assert.That(Get<int[]>(battle,"releasedEffectCounts")[team],Is.Zero);clock.End();
    }

    [TestCase(0)] [TestCase(1)]
    public void AnOpeningBeetleChannelCannotCastAgainUntilItsExistingChannelEnds(int team)
    {
        SetSourceTeam(team);SetUnit(source,"beetleOverturn",NewUnitState("beetleOverturn"));
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,true),Is.False);Assert.That(commits,Is.Zero);
        Assert.That(Pool.Count,Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(1f));
        SetUnit(source,"beetleOverturn",null);
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,true),Is.True);Assert.That(commits,Is.Zero);
        MaturePreparation();Assert.That(commits,Is.EqualTo(1));
        Assert.That(GetLocal<bool>("active"),Is.True);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase(0)] [TestCase(1)]
    public void SilenceAfterCommitDoesNotCancelAnAlreadyExecutingSkill(int team)
    {
        SetSourceTeam(team);CommitSkill(true);
        GetUnit<CombatStatusState>(source,"combatStatuses").ApplyTimed(CombatStatusKind.Silence,2f,target);
        Call(battle,"UpdateCommonSkillFocus");Assert.That(GetLocal<bool>("active"),Is.True);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(commits,Is.EqualTo(1));
    }

    private IList Pool=>(IList)typeof(MonsterAutoBattlePrototype).GetField("localSkillPresentations",Private).GetValue(battle);
    private object Presentation=>Pool[0];
    private T GetLocal<T>(string name)=>(T)Presentation.GetType().GetField(name).GetValue(Presentation);
    private void SetLocal(string name,object value)=>Presentation.GetType().GetField(name).SetValue(Presentation,value);
    private void SetSourceTeam(int team)
    {
        Type type=unitType.GetField("team").FieldType;
        SetUnit(source,"team",Enum.ToObject(type,team));SetUnit(target,"team",Enum.ToObject(type,1-team));
    }
    private SpriteMonsterAnimationDriver CommitSkill(bool presentation)
    {
        playSkillPresentation=presentation;Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.True);
        Assert.That(commits,Is.Zero);MaturePreparation();
        Assert.That(commits,Is.EqualTo(1));Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(CombatSkillClock.Active,Is.False);return GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
    }
    private void FinishPresentation(SpriteMonsterAnimationDriver driver)
    {
        Advance(driver,driver.SkillAttackDuration+1f);SetLocal("committedAt",Time.timeAsDouble-.2d);
        Call(battle,"UpdateCommonSkillFocus");SetLocal("recoveryAt",Time.timeAsDouble-.15d);
        Call(battle,"UpdateCommonSkillFocus");
    }
    private void MaturePreparation()
    {
        double duration=Convert.ToInt32(GetUnit<object>(source,"team"))==0?1d:.35d;
        SetLocal("startedAt",Time.unscaledTimeAsDouble-duration-.001d);
        Set(battle,"allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble-.001d);
        Call(battle,"UpdateCommonSkillFocus");
    }
    private void AssertVisualsCleared(SpriteMonsterAnimationDriver driver)
    {
        Assert.That(GetLocal<bool>("active"),Is.False);Assert.That(Get<float>(driver,"skillFocusScale"),Is.EqualTo(1f));
        Assert.That(GetLocal<SkillFocusSpriteMirror>("mirror").gameObject.activeSelf,Is.False);
        Assert.That(GameObject.Find("Skill Focus Shade"),Is.Null);
    }

    private object NewUnitState(string field)=>Activator.CreateInstance(unitType.GetField(field).FieldType,true);
    private bool CommonSkillActionActive()=>(bool)typeof(MonsterAutoBattlePrototype)
        .GetMethod("CommonSkillActionActive",Static).Invoke(null,new[]{source});
    private static void Advance(SpriteMonsterAnimationDriver driver,float seconds)
    {Call(driver,"Advance",seconds);Call(driver,"LateUpdate");}

    private bool Commit<TContext>(TContext context,SkillMasterRow skill)
    {
        commits++;
        if(playSkillPresentation)GetUnit<SpriteMonsterAnimationDriver>(source,"spriteAnimation").PlaySkillAttack(1f,null);
        SetUnit(source,"skillUses",GetUnit<int>(source,"skillUses")+1);
        SetUnit(source,"skillTimer",4f);
        return true;
    }

    private object Unit(GameObject model,int team)
    {
        object unit=Activator.CreateInstance(unitType,true);
        SetUnit(unit,"model",model);SetUnit(unit,"ring",new GameObject("Focus cast queue ring"));
        ((GameObject)unitType.GetField("ring").GetValue(unit)).transform.SetParent(model.transform);
        SetUnit(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        SetUnit(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Raputo"));
        SetUnit(unit,"stableId",Guid.NewGuid().ToString());SetUnit(unit,"skillId","guardian_taunt");
        SetUnit(unit,"alive",true);SetUnit(unit,"hp",5000f);SetUnit(unit,"maxHp",5000f);
        SetUnit(unit,"damage",100f);SetUnit(unit,"attackInterval",1f);SetUnit(unit,"attackRange",1);
        SetUnit(unit,"collisionRadius",.3f);
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle)).Add(unit);
        return unit;
    }

    private void SetUnit(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private T GetUnit<T>(object unit,string name)=>(T)unitType.GetField(name).GetValue(unit);
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
}
