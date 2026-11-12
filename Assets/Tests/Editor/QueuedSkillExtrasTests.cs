using System;
using System.Collections;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class QueuedSkillExtrasTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject host,cameraHost,commanderModel,contributorModel,targetModel,selectedModel;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private ExpeditionRunSave run;
    private Type unitType,requestType,groupType;
    private object commander,contributor,target,selected,previousFrontend;
    private float previousSpeed,observedMultiplier;
    private int attempts;
    private string outcome;
    private object observedContextTarget,observedSelectedTarget;

    [SetUp]
    public void SetUp()
    {
        attempts=0;outcome="success";observedMultiplier=0f;
        observedContextTarget=null;observedSelectedTarget=null;
        previousSpeed=Time.timeScale;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        host=new GameObject("Inactive queued extras test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        run=new ExpeditionRunSave();Set(battle,"battleStarted",true);Set(battle,"expeditionRun",run);
        cameraHost=new GameObject("Queued extras camera");
        var camera=cameraHost.AddComponent<Camera>();camera.transform.position=Vector3.back*10f;
        director=cameraHost.AddComponent<CombatCameraDirector>();
        Set(director,"battleCamera",camera);Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        director.BeginBattle();Time.timeScale=1f;
        Type type=typeof(MonsterAutoBattlePrototype);
        unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        requestType=type.GetNestedType("PendingSkillFocus",BindingFlags.NonPublic);
        groupType=type.GetNestedType("QueuedCombinedSkillGroup",BindingFlags.NonPublic);
        commanderModel=Spawn();contributorModel=Spawn();
        targetModel=new GameObject("Repeat target");selectedModel=new GameObject("Original selected target");
        targetModel.transform.position=Vector3.right*.8f;selectedModel.transform.position=Vector3.left*.8f;
        commander=Unit(commanderModel,0);contributor=Unit(contributorModel,0);
        target=Unit(targetModel,1);selected=Unit(selectedModel,1);
        Set(commander,"target",target);Set(contributor,"target",target);
        var state=new ExpeditionMonsterStateSave{instanceId=Get<string>(commander,"stableId")};
        state.appliedSpecialItemIds.Add("special_combined_cast_core");run.monsters.Add(state);
        run.augments.Add(new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.Protagonist,targetMonsterInstanceId=state.instanceId});

        FieldInfo field=type.GetField("skillExecutor",Private);
        object executor=Activator.CreateInstance(field.FieldType);
        Type context=field.FieldType.GetGenericArguments()[0];
        Type handler=typeof(Func<,,>).MakeGenericType(context,typeof(SkillMasterRow),typeof(bool));
        Delegate spy=Delegate.CreateDelegate(handler,this,GetType().GetMethod("Execute",Private).MakeGenericMethod(context));
        field.FieldType.GetMethod("Register").Invoke(executor,new object[]{
            GameMasterData.Skill("guardian_taunt").behavior,MonsterSkillTrigger.Active,spy});
        field.SetValue(battle,executor);
    }

    [TearDown]
    public void TearDown()
    {
        if(battle!=null)Call(battle,"CancelCommonSkillFocus");
        if(director!=null)director.EndBattle();
        Object.DestroyImmediate(host);Object.DestroyImmediate(commanderModel);Object.DestroyImmediate(contributorModel);
        Object.DestroyImmediate(targetModel);Object.DestroyImmediate(selectedModel);Object.DestroyImmediate(cameraHost);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);Time.timeScale=previousSpeed;
    }

    [TestCase("success")]
    [TestCase("false")]
    [TestCase("throw")]
    public void RepeatExecutesOnlyTheRawHandlerAndRestoresItsMultiplierCooldownAndSelectedTarget(string result)
    {
        outcome=result;
        Set(commander,"skillTraitDamageMultiplier",1.5f);Set(commander,"skillTimer",7f);
        Set(commander,"augmentProtagonistGauge",17f);Set(commander,"target",selected);
        object request=Request(commander);Set(request,"repeatTarget",target);Set(request,"repeatRatio",.4f);
        if(result=="throw")
        {
            var error=Assert.Throws<TargetInvocationException>(()=>Call(battle,"ExecuteRepeatedSkillFocus",request));
            Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());
        }
        else Assert.That(Call(battle,"ExecuteRepeatedSkillFocus",request),Is.EqualTo(result=="success"));

        Assert.That(attempts,Is.EqualTo(1));
        Assert.That(observedMultiplier,Is.EqualTo(.6f).Within(.00001f));
        Assert.That(observedContextTarget,Is.SameAs(target));Assert.That(observedSelectedTarget,Is.SameAs(target));
        Assert.That(Get<float>(commander,"skillTraitDamageMultiplier"),Is.EqualTo(1.5f));
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(7f));
        Assert.That(Get<object>(commander,"target"),Is.SameAs(selected));
        Assert.That(Get<float>(commander,"augmentProtagonistGauge"),Is.EqualTo(17f),
            "A repeat must not invoke the normal cast's augment pipeline again.");
        Assert.That(Get<float>(commander,"skillEchoTimer"),Is.Zero);
        Assert.That(Call(battle,"HasSkillCastPresentation",commander),Is.False,"A raw repeat must not rerun the normal augment/presentation pipeline.");
        Assert.That(Get<IList>(battle,"combinedCastPulseContributorIds"),Is.Empty);
    }

    [Test]
    public void AnActualCombinedSuccessAddsOneCostAndTheSameContributorCannotChargeTwice()
    {
        Set(commander,"skillTimer",8f);object group=Group();
        Call(battle,"CommitCombinedSkillFocus",group,contributor);
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8.5f));
        Assert.That(Get<IList>(group,"committedIds"),Has.Count.EqualTo(1));
        Assert.That(Get<IList>(battle,"combinedCastPulseContributorIds"),Is.EquivalentTo(new[]{Get<string>(contributor,"stableId")}));
        Call(battle,"CommitCombinedSkillFocus",group,contributor);
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8.5f));Assert.That(Get<IList>(group,"committedIds"),Has.Count.EqualTo(1));
    }

    [TestCase("false")] [TestCase("invalid")]
    public void AFailedAcceptedCombinedContributorRetriesWithoutChargingTwiceWhileAnInvalidContributorCannotStart(string result)
    {
        Set(commander,"skillTimer",8f);outcome=result;
        if(result=="invalid")Set(contributor,"alive",false);
        Call(battle,"HandleAugmentSkillUse",commander,false);
        Assert.That(attempts,Is.Zero);Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8f));
        object state=result=="invalid"?null:Local(contributor);
        object request=state==null?null:Get<object>(state,"pending");
        if(result!="invalid")MaturePreparation(contributor);
        Assert.That(attempts,Is.EqualTo(result=="invalid"?0:1));
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8f));
        Assert.That(Get<int>(contributor,"skillUses"),Is.Zero);
        Assert.That(Call(battle,"HasSkillCastPresentation",contributor),Is.EqualTo(result!="invalid"));
        Assert.That(Get<IList>(battle,"combinedCastPulseContributorIds"),Is.Empty);
        if(result!="invalid")
        {
            double started=Get<double>(state,"startedAt");
            Assert.That(Local(contributor),Is.SameAs(state));Assert.That(Get<object>(state,"pending"),Is.SameAs(request));
            Call(battle,"UpdateCommonSkillFocus");Assert.That(attempts,Is.EqualTo(1),"The retry delay prevents same-frame attempts.");
            outcome="success";Set(request,"nextAttemptAt",0d);Call(battle,"UpdateCommonSkillFocus");
            Assert.That(attempts,Is.EqualTo(2));Assert.That(Get<int>(contributor,"skillUses"),Is.EqualTo(1));
            Assert.That(Get<double>(state,"startedAt"),Is.EqualTo(started));Assert.That(Get<object>(state,"pending"),Is.Null);
            Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8.5f));
            object group=Get<object>(request,"combinedGroup");
            Assert.That(Get<IList>(group,"committedIds"),Has.Count.EqualTo(1));
            Call(battle,"UpdateCommonSkillFocus");Call(battle,"CommitCombinedSkillFocus",group,contributor);
            Assert.That(attempts,Is.EqualTo(2));Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8.5f));
        }
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void TheRealCombinedPipelineWaitsForPreparationAndChargesOnlyItsSuccessfulContributor()
    {
        Set(commander,"skillTimer",8f);Call(battle,"HandleAugmentSkillUse",commander,false);
        Assert.That(attempts,Is.Zero);Assert.That(Get<int>(contributor,"skillUses"),Is.Zero);
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8f));
        Assert.That(Get<object>(Get<object>(Local(contributor),"pending"),"combinedGroup"),Is.Not.Null);
        Call(battle,"HandleAugmentSkillUse",commander,false);
        Assert.That(attempts,Is.Zero);Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8f));
        MaturePreparation(contributor);
        Assert.That(attempts,Is.EqualTo(1));Assert.That(Get<int>(contributor,"skillUses"),Is.EqualTo(1));
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8.5f));
        Assert.That(Call(battle,"HasSkillCastPresentation",contributor),Is.True);
        Assert.That(Get<IList>(battle,"combinedCastPulseContributorIds"),Is.EquivalentTo(new[]{Get<string>(contributor,"stableId")}));
        Call(battle,"HandleAugmentSkillUse",commander,false);
        Assert.That(attempts,Is.EqualTo(1));Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8.5f));
        Call(battle,"CancelCommonSkillFocus");Assert.That(Call(battle,"HasSkillCastPresentation",contributor),Is.False);
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8.5f));Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase("death")] [TestCase("explicit cancel")]
    [TestCase("silence")] [TestCase("menu")]
    public void AnAcceptedCombinedContributorWaitsThroughControlAndMenusButHonorsCancellation(string cause)
    {
        Set(commander,"skillTimer",8f);Call(battle,"HandleAugmentSkillUse",commander,false);
        object state=Local(contributor),request=Get<object>(Local(contributor),"pending");
        Assert.That(request,Is.Not.Null);Set(state,"startedAt",Time.unscaledTimeAsDouble-1.001d);
        double started=Get<double>(state,"startedAt");
        if(cause=="death")Set(contributor,"alive",false);
        else if(cause=="silence")Get<CombatStatusState>(contributor,"combatStatuses").ApplyTimed(CombatStatusKind.Silence,2f);
        else if(cause=="explicit cancel")Call(battle,"CancelCommonSkillFocus");
        else {Time.timeScale=0f;battle.PrepareForSystemMenu();}
        Call(battle,"UpdateCommonSkillFocus");
        Assert.That(attempts,Is.Zero);Assert.That(Get<int>(contributor,"skillUses"),Is.Zero);
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8f));
        bool cancelled=cause=="death"||cause=="explicit cancel";
        Assert.That(Call(battle,"HasSkillCastPresentation",contributor),Is.EqualTo(!cancelled));
        Assert.That(Get<IList>(battle,"combinedCastPulseContributorIds"),Is.Empty);
        if(cancelled)return;
        Assert.That(Local(contributor),Is.SameAs(state));Assert.That(Get<object>(state,"pending"),Is.SameAs(request));
        Get<CombatStatusState>(contributor,"combatStatuses").Remove(CombatStatusKind.Silence);Time.timeScale=1f;
        Call(battle,"UpdateCommonSkillFocus");Call(battle,"UpdateCommonSkillFocus");
        Assert.That(attempts,Is.EqualTo(1));Assert.That(Get<int>(contributor,"skillUses"),Is.EqualTo(1));
        Assert.That(Get<double>(state,"startedAt"),Is.EqualTo(started));Assert.That(Get<object>(state,"pending"),Is.Null);
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(8.5f));
        Assert.That(Get<IList>(Get<object>(request,"combinedGroup"),"committedIds"),Has.Count.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void ARepeatedSkillKeepsItsAcceptedPreparationThroughFreezeAndPreservesItsRealCooldown(bool temporarilyFrozen)
    {
        Set(commander,"skillTraitDamageMultiplier",1.5f);Set(commander,"skillTimer",7f);
        Set(commander,"augmentProtagonistGauge",17f);Set(commander,"target",selected);
        Assert.That(Call(battle,"QueueRepeatedSkillFocus",commander,target,.4f),Is.True);
        Assert.That(attempts,Is.Zero);Assert.That(Get<int>(commander,"skillUses"),Is.Zero);
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(7f));
        object state=Local(commander),request=Get<object>(Local(commander),"pending");
        if(temporarilyFrozen)Get<CombatStatusState>(commander,"combatStatuses").ApplyTimed(CombatStatusKind.Freeze,2f);
        MaturePreparation(commander);
        double started=Get<double>(state,"startedAt");
        Assert.That(attempts,Is.EqualTo(temporarilyFrozen?0:1));
        Assert.That(Get<int>(commander,"skillUses"),Is.EqualTo(temporarilyFrozen?0:1));
        if(temporarilyFrozen)
        {
            Assert.That(Local(commander),Is.SameAs(state));Assert.That(Get<object>(state,"pending"),Is.SameAs(request));
            Get<CombatStatusState>(commander,"combatStatuses").Remove(CombatStatusKind.Freeze);
            Call(battle,"UpdateCommonSkillFocus");
        }
        Call(battle,"UpdateCommonSkillFocus");
        Assert.That(attempts,Is.EqualTo(1));Assert.That(Get<int>(commander,"skillUses"),Is.EqualTo(1));
        Assert.That(Get<double>(state,"startedAt"),Is.EqualTo(started));Assert.That(Get<object>(state,"pending"),Is.Null);
        Assert.That(Get<float>(commander,"skillTimer"),Is.EqualTo(7f));
        Assert.That(Get<float>(commander,"skillTraitDamageMultiplier"),Is.EqualTo(1.5f));
        Assert.That(Get<object>(commander,"target"),Is.SameAs(selected));
        Assert.That(Get<float>(commander,"augmentProtagonistGauge"),Is.EqualTo(17f));
        Assert.That(observedMultiplier,Is.EqualTo(.6f).Within(.00001f));
        Assert.That(observedContextTarget,Is.SameAs(target));
        Assert.That(Call(battle,"HasSkillCastPresentation",commander),Is.True);
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    private bool Execute<TContext>(TContext context,SkillMasterRow skill)
    {
        attempts++;
        object source=context.GetType().GetField("source").GetValue(context);
        observedMultiplier=Get<float>(source,"skillTraitDamageMultiplier");
        observedContextTarget=context.GetType().GetField("target").GetValue(context);
        observedSelectedTarget=Get<object>(source,"target");
        Set(source,"skillTraitDamageMultiplier",99f);Set(source,"skillTimer",40f);
        if(outcome=="throw")throw new InvalidOperationException("Repeat handler failure fixture");
        if(outcome=="success")Set(source,"skillUses",Get<int>(source,"skillUses")+1);
        return outcome=="success";
    }

    private object Unit(GameObject model,int team)
    {
        object unit=Activator.CreateInstance(unitType,true);
        Set(unit,"model",model);var ring=new GameObject("Queued extras ring");ring.transform.SetParent(model.transform);Set(unit,"ring",ring);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Raputo"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId","guardian_taunt");
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",1);Set(unit,"collisionRadius",.3f);
        Set(unit,"spriteAnimation",model.GetComponentInChildren<SpriteMonsterAnimationDriver>());
        ((IList)Get<object>(battle,"units")).Add(unit);return unit;
    }

    private object Request(object source){object request=Activator.CreateInstance(requestType,true);Set(request,"unit",source);return request;}
    private object Local(object caster)
    {
        foreach(object presentation in Get<IList>(battle,"localSkillPresentations"))
            if(Get<bool>(presentation,"active")&&Get<object>(presentation,"unit")==caster)return presentation;
        Assert.Fail("No active preparation for the requested caster.");return null;
    }
    private void MaturePreparation(object caster)
    {
        Set(Local(caster),"startedAt",Time.unscaledTimeAsDouble-1.001d);
        Set(battle,"allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble-.001d);
        Call(battle,"UpdateCommonSkillFocus");
    }
    private object Group()
    {object group=Activator.CreateInstance(groupType,true);Set(group,"commander",commander);Set(group,"perSkill",.5f);Set(group,"maximum",18f);return group;}
    private static GameObject Spawn()=>PrefabUtility.InstantiatePrefab(
        AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Fields).SetValue(value,data);
    private static T Get<T>(object value,string field)=>(T)value.GetType().GetField(field,Fields).GetValue(value);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Private).Invoke(value,args);
}
