using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MultiCasterSkillFocusTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private readonly Dictionary<object,int> attempts=new Dictionary<object,int>();
    private readonly Dictionary<object,int> commits=new Dictionary<object,int>();
    private GameObject host,cameraHost,primaryModel,secondaryModel,targetModel,fourthModel;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private Type unitType;
    private object primary,secondary,target,rejected,previousFrontend;
    private float previousSpeed;

    [SetUp]
    public void SetUp()
    {
        attempts.Clear();commits.Clear();rejected=null;fourthModel=null;
        previousSpeed=Time.timeScale;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        host=new GameObject("Inactive multi-caster presentation test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);Set(battle,"expeditionRun",new ExpeditionRunSave());
        Set(battle,"combatBounds",new Bounds(Vector3.zero,Vector3.one*100f));
        cameraHost=new GameObject("Multi-caster test camera");
        var camera=cameraHost.AddComponent<Camera>();
        camera.transform.position=Vector3.back*10f;
        director=cameraHost.AddComponent<CombatCameraDirector>();
        Set(director,"battleCamera",camera);Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        Set(director,"homeFieldOfView",camera.fieldOfView);Set(director,"homeLensShift",camera.lensShift);
        director.BeginBattle();Time.timeScale=1f;
        primaryModel=Spawn();secondaryModel=Spawn();targetModel=Spawn();
        secondaryModel.transform.position=Vector3.forward*.3f;targetModel.transform.position=Vector3.right*.8f;
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        primary=Unit(primaryModel,0);secondary=Unit(secondaryModel,0);target=Unit(targetModel,1);
        SetUnit(primary,"target",target);SetUnit(secondary,"target",target);SetUnit(target,"target",primary);

        var field=typeof(MonsterAutoBattlePrototype).GetField("skillExecutor",Private);
        object executor=Activator.CreateInstance(field.FieldType);
        field.SetValue(battle,executor);
        RegisterSpy("guardian_taunt");
    }

    [TearDown]
    public void TearDown()
    {
        if(battle!=null)Call(battle,"CancelCommonSkillFocus");
        if(director!=null)director.EndBattle();
        if(primary!=null)
        {
            var fan=UnitField<PixelWizardStarFan>(primary,"wizardBarrageFan");
            if(fan!=null)Object.DestroyImmediate(fan.gameObject);
        }
        Object.DestroyImmediate(host);Object.DestroyImmediate(primaryModel);
        Object.DestroyImmediate(secondaryModel);Object.DestroyImmediate(targetModel);
        Object.DestroyImmediate(fourthModel);Object.DestroyImmediate(cameraHost);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
        Time.timeScale=previousSpeed;
    }

    [TestCase(0)] [TestCase(1)]
    public void AlliedCohortsPrepareTogetherWhileEnemiesKeepTheirOwnPreparation(int secondTeam)
    {
        SetUnit(secondary,"team",Enum.ToObject(unitType.GetField("team").FieldType,secondTeam));
        SetUnit(secondary,"target",secondTeam==0?target:primary);
        var camera=Get<Camera>(battle,"mainCamera");
        float fov=camera.fieldOfView;Vector3 position=camera.transform.position;
        Quaternion rotation=camera.transform.rotation;Matrix4x4 projection=camera.projectionMatrix;
        Vector3 primarySize=Driver(primary).transform.lossyScale,secondarySize=Driver(secondary).transform.lossyScale;
        Vector3 bodyScale=primaryModel.transform.localScale,bodyPosition=primaryModel.transform.position;
        float radius=UnitField<float>(primary,"collisionRadius"),combatScale=UnitField<float>(primary,"combatBodyScale");
        Assert.That(Use(primary),Is.True);object first=Local(primary);
        var firstMirror=Get<SkillFocusSpriteMirror>(first,"mirror");
        Assert.That(Vector3.Distance(Driver(primary).transform.lossyScale,primarySize*1.5f),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(firstMirror.transform.lossyScale,primarySize*1.5f),Is.LessThan(.0001f));
        Assert.That(primaryModel.transform.localScale,Is.EqualTo(bodyScale));
        Assert.That(primaryModel.transform.position,Is.EqualTo(bodyPosition));
        Assert.That(UnitField<float>(primary,"collisionRadius"),Is.EqualTo(radius));
        Assert.That(UnitField<float>(primary,"combatBodyScale"),Is.EqualTo(combatScale));
        Assert.That(firstMirror.GetComponent<SpriteRenderer>().enabled,Is.True);
        Assert.That(Get<float>(firstMirror,"gatherDuration"),Is.EqualTo(1f));
        Assert.That(Time.timeScale,Is.EqualTo(.08f));
        var shade=Get<SpriteRenderer>(battle,"allyPreparationShade");
        Assert.That(shade,Is.Not.Null);Assert.That(shade.gameObject.activeSelf,Is.True);
        double started=Get<double>(firstMirror,"gatherStartedAt");
        Assert.That(Use(secondary),Is.True);
        Assert.That(Vector3.Distance(Driver(secondary).transform.lossyScale,
            secondarySize*(secondTeam==0?1.5f:1f)),Is.LessThan(.0001f));
        Assert.That(Get<float>(Get<SkillFocusSpriteMirror>(Local(secondary),"mirror"),"gatherDuration"),
            Is.EqualTo(secondTeam==0?1f:.35f));
        Assert.That(Count(commits,primary),Is.Zero);Assert.That(Count(commits,secondary),Is.Zero);
        Assert.That(Get<object>(Local(primary),"pending"),Is.Not.Null);
        Assert.That(Get<object>(Local(secondary),"pending"),Is.Not.Null);
        Assert.That(Pool.Count,Is.EqualTo(2));Assert.That(Local(primary),Is.SameAs(first));
        Call(battle,"BeginLocalSkillPresentation",primary);
        Assert.That(Pool.Count,Is.EqualTo(2));
        Assert.That(Get<double>(firstMirror,"gatherStartedAt"),Is.EqualTo(started));
        AssertPresented(primary);AssertPresented(secondary);
        if(secondTeam==0)
        {
            Set(first,"startedAt",Time.unscaledTimeAsDouble-1.001d);
            Set(battle,"allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble+.5d);
            Call(battle,"UpdateCommonSkillFocus");
            Assert.That(Count(commits,primary),Is.Zero);Assert.That(Count(commits,secondary),Is.Zero,
                "A completed ally must wait until the latest cohort member has finished its own second.");
            Assert.That(Driver(primary).IsSkillAnticipating,Is.True);
            Assert.That(Driver(secondary).IsSkillAnticipating,Is.True);
            Assert.That(Time.timeScale,Is.EqualTo(.08f));
            MaturePreparation(secondary);
        }
        else
        {
            MaturePreparation(secondary);
            Assert.That(Count(commits,primary),Is.Zero);Assert.That(Count(commits,secondary),Is.EqualTo(1));
            Assert.That(Time.timeScale,Is.EqualTo(.08f),"An enemy can finish its .35s preparation during the allied slow sequence.");
            Assert.That(Get<object>(first,"pending"),Is.Not.Null);Assert.That(Scale(primary),Is.EqualTo(1.5f));
            MaturePreparation(primary);
        }
        Assert.That(Count(commits,primary),Is.EqualTo(1));
        Assert.That(Count(commits,secondary),Is.EqualTo(1));
        Assert.That(Scale(primary),Is.EqualTo(1.5f));Assert.That(Scale(secondary),Is.EqualTo(secondTeam==0?1.5f:1f));
        Assert.That(firstMirror.GetComponent<SpriteRenderer>().sortingOrder,Is.EqualTo(30004));
        Assert.That(Get<SkillFocusSpriteMirror>(Local(secondary),"mirror").GetComponent<SpriteRenderer>().sortingOrder,Is.EqualTo(30004));
        Assert.That(Use(secondary),Is.False);Assert.That(Count(commits,secondary),Is.EqualTo(1));
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(director.SkillFocusActive,Is.False);Assert.That(shade.gameObject.activeSelf,Is.False);
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov).Within(.00001f));
        for(int i=0;i<16;i++)Assert.That(camera.projectionMatrix[i],Is.EqualTo(projection[i]).Within(.00001f));
    }

    [Test]
    public void GroundCirclesIdentifyTeamsWithoutADirectionPointAndClearBetweenBattles()
    {
        var ring=UnitField<GameObject>(primary,"ring").AddComponent<CombatTargetRing>();
        var material=new Material(Shader.Find("Sprites/Default"));
        try
        {
            ring.Configure(.4f,Color.cyan,material);SetUnit(primary,"targetRing",ring);
            Transform indicator=ring.transform.Find("Combat Target Ring");
            Transform point=indicator.Find("Target Direction");
            Set(battle,"battleStarted",false);Call(battle,"UpdateTargetRing",primary);
            Assert.That(indicator.gameObject.activeSelf,Is.False);
            Set(battle,"battleStarted",true);Call(battle,"UpdateTargetRing",primary);
            Assert.That(indicator.gameObject.activeSelf,Is.True);
            Assert.That(point.gameObject.activeSelf,Is.False);
            Assert.That(indicator.Find("Circle").GetComponent<MeshRenderer>().enabled,Is.True);
            Assert.That(ring.transform.Find("Attack Direction Frame"),Is.Null);
            SetUnit(secondary,"team",Enum.ToObject(unitType.GetField("team").FieldType,1));
            secondaryModel.transform.position=Vector3.back*3f;SetUnit(primary,"target",secondary);
            Call(battle,"UpdateTargetRing",primary);
            Assert.That(point.gameObject.activeSelf,Is.False);
            SetUnit(secondary,"alive",false);Call(battle,"UpdateTargetRing",primary);
            Assert.That(point.gameObject.activeSelf,Is.False);
            Assert.That(indicator.gameObject.activeSelf,Is.True);
            SetUnit(primary,"inStorage",true);Call(battle,"UpdateTargetRing",primary);
            Assert.That(indicator.gameObject.activeSelf,Is.False);
            SetUnit(primary,"inStorage",false);Set(battle,"battleEnding",true);
            Call(battle,"UpdateTargetRing",primary);
            Assert.That(indicator.gameObject.activeSelf,Is.False);
        }
        finally
        {
            Call(ring,"OnDestroy");Object.DestroyImmediate(ring);
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void CompletedSkillPresentationDoesNotRestoreTeamOutlines()
    {
        Assert.That(Use(primary),Is.True);MaturePreparation(primary);AssertPresented(primary);
        FinishPresentation(primary);
        Call(battle,"RefreshUnitOutlines");
        Assert.That(UnitField<ScreenSpaceOutlineTarget>(primary,"outline").IsOutlined,Is.False);
        Assert.That(UnitField<ScreenSpaceOutlineTarget>(target,"outline").IsOutlined,Is.False);
        Assert.That(Has(primary),Is.False);
    }

    [Test]
    public void ConcurrentPresentationsRestoreTheirOwnersIndependentlyOnDeathAndNormalCompletion()
    {
        foreach(object caster in new[]{primary,secondary,target})
        {Assert.That(Use(caster),Is.True);Assert.That(Count(commits,caster),Is.Zero);AssertPresented(caster);}
        foreach(object caster in new[]{primary,secondary,target})
        {MaturePreparation(caster);Assert.That(Count(commits,caster),Is.EqualTo(1));}
        Assert.That(Pool.Count,Is.EqualTo(3));
        SetUnit(primary,"alive",false);Call(battle,"UpdateCommonSkillFocus");
        Assert.That(Has(primary),Is.False);Assert.That(Scale(primary),Is.EqualTo(1f));
        AssertPresented(secondary);AssertPresented(target);
        FinishPresentation(secondary);
        Assert.That(Has(secondary),Is.False);Assert.That(Scale(secondary),Is.EqualTo(1f));
        Assert.That(Hud(secondary),Is.True);AssertPresented(target);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase("death")] [TestCase("silence")] [TestCase("timer stun")] [TestCase("lost target")]
    public void AnInvalidCasterCannotSpendItsSkillOrStartAPresentation(string invalid)
    {
        if(invalid=="death")SetUnit(secondary,"alive",false);
        else if(invalid=="silence")UnitField<CombatStatusState>(secondary,"combatStatuses").ApplyTimed(CombatStatusKind.Silence,2f);
        else if(invalid=="timer stun")SetUnit(secondary,"giraffeStunTimer",2f);
        else SetUnit(secondary,"target",null);
        Assert.That(Call(battle,"BeginCommonSkillFocus",secondary,false),Is.False);
        Assert.That(Count(attempts,secondary),Is.Zero);Assert.That(Count(commits,secondary),Is.Zero);
        Assert.That(UnitField<int>(secondary,"skillUses"),Is.Zero);Assert.That(UnitField<float>(secondary,"skillTimer"),Is.Zero);
        Assert.That(Has(secondary),Is.False);Assert.That(Scale(secondary),Is.EqualTo(1f));Assert.That(Pool.Count,Is.Zero);
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void ARejectedExecutorRetriesTheSameAcceptedCastWithoutRestartingItsPresentation()
    {
        SetUnit(secondary,"skillCooldownDuration",8f);Assert.That(Use(primary),Is.True);rejected=secondary;
        Assert.That(Use(secondary),Is.True);Assert.That(Count(attempts,secondary),Is.Zero);
        object state=Local(secondary),request=Get<object>(Local(secondary),"pending");
        MaturePreparation(secondary);
        double started=Get<double>(state,"startedAt");
        Assert.That(Count(attempts,secondary),Is.EqualTo(1));Assert.That(Count(commits,secondary),Is.Zero);
        Assert.That(UnitField<int>(secondary,"skillUses"),Is.Zero);Assert.That(UnitField<float>(secondary,"skillTimer"),Is.EqualTo(.5f));
        Assert.That(UnitField<float>(secondary,"skillTimer"),Is.LessThan(UnitField<float>(secondary,"skillCooldownDuration")));
        Assert.That(Local(secondary),Is.SameAs(state));Assert.That(Get<object>(state,"pending"),Is.SameAs(request));
        Assert.That(Scale(secondary),Is.EqualTo(1f),"A rejected commit has not started its body action.");
        Assert.That(Use(secondary),Is.False);Assert.That(Count(attempts,secondary),Is.EqualTo(1));
        Call(battle,"UpdateCommonSkillFocus");Assert.That(Count(attempts,secondary),Is.EqualTo(1));
        rejected=null;Set(request,"nextAttemptAt",0d);Call(battle,"UpdateCommonSkillFocus");
        Assert.That(Count(attempts,secondary),Is.EqualTo(2));Assert.That(Count(commits,secondary),Is.EqualTo(1));
        Assert.That(Scale(secondary),Is.EqualTo(1.5f));
        Assert.That(Get<double>(state,"startedAt"),Is.EqualTo(started));Assert.That(Get<object>(state,"pending"),Is.Null);
        Assert.That(Pool.Count,Is.EqualTo(2));AssertPresented(primary);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase(FamilySharedSkillSetup.SabertoothSkillId)]
    [TestCase(FamilySharedSkillSetup.CrocodileSkillId)]
    [TestCase(FamilySharedSkillSetup.FrogSkillId)]
    [TestCase(FamilySharedSkillSetup.ArachneSkillId)]
    [TestCase(FamilySharedSkillSetup.RaptorSkillId)]
    [TestCase(FamilySharedSkillSetup.NinjaCatSkillId)]
    [TestCase(FamilySharedSkillSetup.WolfSkillId)]
    [TestCase(FamilySharedSkillSetup.WizardMouseSkillId)]
    [TestCase(FamilySharedSkillSetup.MammothSkillId)]
    [TestCase(FamilySharedSkillSetup.MonkeySkillId)]
    [TestCase(FamilySharedSkillSetup.BeetleSkillId)]
    [TestCase(FamilySharedSkillSetup.MushroomSkillId)]
    [TestCase(FamilySharedSkillSetup.MantisSkillId)]
    [TestCase(FamilySharedSkillSetup.WyvernSkillId)]
    [TestCase(FamilySharedSkillSetup.GriffinSkillId)]
    public void AContactOrRangeLimitedSkillRequiresReachThenItsOwnPreparationBeforeCasting(string skillId)
    {
        SetUnit(primary,"skillId",skillId);RegisterSpy(skillId);
        targetModel.transform.position=Vector3.right*50f;
        Vector3 casterPosition=primaryModel.transform.position;
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.False);
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.False);
        Assert.That(Has(primary),Is.False);Assert.That(Pool.Count,Is.Zero);
        Assert.That(director.SkillFocusActive,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(primaryModel.transform.position,Is.EqualTo(casterPosition));
        Assert.That(Count(attempts,primary),Is.Zero);Assert.That(UnitField<float>(primary,"skillTimer"),Is.Zero);

        float reach=(float)Call(battle,"SkillActivationDistance",primary,target,GameMasterData.Skill(skillId));
        Assert.That(float.IsNaN(reach)||float.IsInfinity(reach),Is.False);
        Assert.That(reach,Is.GreaterThan(0f));
        targetModel.transform.position=Vector3.right*Mathf.Min(.8f,reach*.5f);
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.True);
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.True);
        Assert.That(director.SkillFocusActive,Is.False);Assert.That(Time.timeScale,Is.EqualTo(.08f));
        Assert.That(Count(attempts,primary),Is.Zero);AssertPresented(primary);
        MaturePreparation(primary);
        Assert.That(Count(attempts,primary),Is.EqualTo(1));
        Assert.That(Count(commits,primary),Is.EqualTo(1));AssertPresented(primary);
        Assert.That(UnitField<int>(primary,"skillUses"),Is.EqualTo(1));
        Assert.That(primaryModel.transform.position,Is.EqualTo(casterPosition));
    }

    [TestCase(FamilySharedSkillSetup.SabertoothSkillId)]
    [TestCase(FamilySharedSkillSetup.CrocodileSkillId)]
    [TestCase(FamilySharedSkillSetup.FrogSkillId)]
    [TestCase(FamilySharedSkillSetup.ArachneSkillId)]
    [TestCase(FamilySharedSkillSetup.RaptorSkillId)]
    [TestCase(FamilySharedSkillSetup.NinjaCatSkillId)]
    [TestCase(FamilySharedSkillSetup.WolfSkillId)]
    [TestCase(FamilySharedSkillSetup.WizardMouseSkillId)]
    [TestCase(FamilySharedSkillSetup.MammothSkillId)]
    [TestCase(FamilySharedSkillSetup.MonkeySkillId)]
    [TestCase(FamilySharedSkillSetup.BeetleSkillId)]
    [TestCase(FamilySharedSkillSetup.MushroomSkillId)]
    [TestCase(FamilySharedSkillSetup.MantisSkillId)]
    [TestCase(FamilySharedSkillSetup.WyvernSkillId)]
    [TestCase(FamilySharedSkillSetup.GriffinSkillId)]
    public void APreparedSkillCommitsExactlyOnceWhenItsLivingTargetLeavesActivationRange(string skillId)
    {
        SetUnit(primary,"skillId",skillId);RegisterSpy(skillId);
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.True);
        targetModel.transform.position=Vector3.right*50f;
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.False,
            "The distant target must remain outside the unchanged activation range for a new cast.");
        MaturePreparation(primary);
        Assert.That(Count(attempts,primary),Is.EqualTo(1));
        Assert.That(Count(commits,primary),Is.EqualTo(1));
        Assert.That(UnitField<int>(primary,"skillUses"),Is.EqualTo(1));
        Assert.That(Get<object>(Local(primary),"pending"),Is.Null);AssertPresented(primary);
        Call(battle,"UpdateCommonSkillFocus");
        Assert.That(Count(attempts,primary),Is.EqualTo(1));
        Assert.That(UnitField<bool>(target,"alive"),Is.True);
    }

    [TestCase(FamilySharedSkillSetup.WizardMouseSkillId)]
    [TestCase(FamilySharedSkillSetup.MushroomSkillId)]
    [TestCase(FamilySharedSkillSetup.RaptorSkillId)]
    [TestCase(FamilySharedSkillSetup.FrogSkillId)]
    [TestCase(FamilySharedSkillSetup.SabertoothSkillId)]
    [TestCase(FamilySharedSkillSetup.CrocodileSkillId)]
    [TestCase(FamilySharedSkillSetup.ArachneSkillId)]
    [TestCase(FamilySharedSkillSetup.NinjaCatSkillId)]
    [TestCase(FamilySharedSkillSetup.WolfSkillId)]
    [TestCase(FamilySharedSkillSetup.MammothSkillId)]
    [TestCase(FamilySharedSkillSetup.MonkeySkillId)]
    [TestCase(FamilySharedSkillSetup.BeetleSkillId)]
    [TestCase(FamilySharedSkillSetup.MantisSkillId)]
    [TestCase(FamilySharedSkillSetup.WyvernSkillId)]
    [TestCase(FamilySharedSkillSetup.GriffinSkillId)]
    [TestCase(FamilySharedSkillSetup.KingKongSkillId)]
    [TestCase(FamilySharedSkillSetup.TyrannoSkillId)]
    public void PreparedRangedAndContactSkillsReachTheirRealHandlerWhenEveryEnemyMovesAway(string skillId)
    {
        SetUnit(primary,"skillId",skillId);SetUnit(primary,"skillCooldownDuration",8f);
        SetUnit(secondary,"team",Enum.ToObject(unitType.GetField("team").FieldType,1));
        secondaryModel.transform.position=Vector3.right*35f;
        Call(battle,"ConfigureSkillExecutor");
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.True);
        targetModel.transform.position=Vector3.right*40f;
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.False);
        if(skillId==FamilySharedSkillSetup.CrocodileSkillId)
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                "Coroutine couldn't be started because the the game object 'Inactive multi-caster presentation test' is inactive!");
        MaturePreparation(primary);
        Assert.That(UnitField<int>(primary,"skillUses"),Is.EqualTo(1),
            "The real skill handler must honor preparation that began while its target was in reach.");
        Assert.That(UnitField<float>(primary,"skillTimer"),Is.EqualTo(8f));
        Assert.That(Get<object>(Local(primary),"pending"),Is.Null);AssertPresented(primary);
        if(skillId==FamilySharedSkillSetup.WizardMouseSkillId)
            Assert.That(UnitField<bool>(primary,"wizardBarragePending"),Is.True);
        else if(skillId==FamilySharedSkillSetup.MushroomSkillId)
            Assert.That(UnitField<object>(primary,"mushroomColonyTarget"),Is.SameAs(target));
        else if(skillId==FamilySharedSkillSetup.RaptorSkillId)
            Assert.That(UnitField<bool>(primary,"leaping"),Is.True);
        else if(skillId==FamilySharedSkillSetup.FrogSkillId)
            Assert.That(UnitField<object>(primary,"tongueHarpoonTarget"),Is.SameAs(target));
        Assert.That(UnitField<bool>(target,"alive"),Is.True);
        Assert.That(UnitField<bool>(secondary,"alive"),Is.True);
    }

    [TestCase("guardian_taunt")]
    [TestCase("cluster_breath")]
    public void APreparedAreaSkillCanMissWithoutReachingEnemiesOutsideItsAuthoredArea(string skillId)
    {
        SetUnit(primary,"skillId",skillId);SetUnit(primary,"skillCooldownDuration",8f);
        SetUnit(secondary,"team",Enum.ToObject(unitType.GetField("team").FieldType,1));
        secondaryModel.transform.position=Vector3.right*35f;
        Call(battle,"ConfigureSkillExecutor");
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.True);
        targetModel.transform.position=Vector3.right*40f;
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.False);
        float targetHealth=UnitField<float>(target,"hp"),secondaryHealth=UnitField<float>(secondary,"hp");
        MaturePreparation(primary);
        Assert.That(UnitField<int>(primary,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitField<float>(primary,"skillTimer"),Is.EqualTo(8f));
        Assert.That(UnitField<int>(primary,"skillHits"),Is.Zero);
        Assert.That(UnitField<float>(target,"hp"),Is.EqualTo(targetHealth));
        Assert.That(UnitField<float>(secondary,"hp"),Is.EqualTo(secondaryHealth));
        Assert.That(UnitField<object>(target,"forcedTarget"),Is.Null);
        Assert.That(UnitField<object>(secondary,"forcedTarget"),Is.Null);
        Assert.That(Get<object>(Local(primary),"pending"),Is.Null);AssertPresented(primary);
    }

    [TestCase(FamilySharedSkillSetup.WizardMouseSkillId,"dead")]
    [TestCase(FamilySharedSkillSetup.WizardMouseSkillId,"untargetable")]
    [TestCase(FamilySharedSkillSetup.FrogSkillId,"dead")]
    [TestCase(FamilySharedSkillSetup.FrogSkillId,"untargetable")]
    public void AnAcceptedSkillRetargetsAnotherLivingEnemyWithoutRestartingPreparation(string skillId,string cause)
    {
        SetUnit(primary,"skillId",skillId);SetUnit(primary,"skillCooldownDuration",8f);
        SetUnit(secondary,"team",Enum.ToObject(unitType.GetField("team").FieldType,1));
        secondaryModel.transform.position=Vector3.right*35f;
        Call(battle,"ConfigureSkillExecutor");
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.True);
        object state=Local(primary),request=Get<object>(Local(primary),"pending");
        if(cause=="dead")SetUnit(target,"alive",false);else SetUnit(target,"ninjaUntargetableTimer",1f);
        MaturePreparation(primary);
        Assert.That(Local(primary),Is.SameAs(state));Assert.That(Get<object>(state,"pending"),Is.Null);
        Assert.That(Get<object>(request,"castTarget"),Is.SameAs(secondary));
        Assert.That(UnitField<int>(primary,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitField<float>(primary,"skillTimer"),Is.EqualTo(8f));
        if(skillId==FamilySharedSkillSetup.WizardMouseSkillId)
            Assert.That((IList)UnitField<object>(primary,"wizardBarrageTargets"),Does.Contain(secondary));
        else Assert.That(UnitField<object>(primary,"tongueHarpoonTarget"),Is.SameAs(secondary));
        AssertPresented(primary);
    }

    [Test]
    public void AnAcceptedSkillWaitsForATemporarilyUntargetableLastEnemyAndThenReleasesOnce()
    {
        SetUnit(primary,"skillId",FamilySharedSkillSetup.FrogSkillId);Call(battle,"ConfigureSkillExecutor");
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.True);
        object state=Local(primary),request=Get<object>(Local(primary),"pending");
        SetUnit(target,"ninjaUntargetableTimer",1f);MaturePreparation(primary);
        double started=Get<double>(state,"startedAt");
        Assert.That(Local(primary),Is.SameAs(state));Assert.That(Get<object>(state,"pending"),Is.SameAs(request));
        Assert.That(UnitField<int>(primary,"skillUses"),Is.Zero);
        SetUnit(target,"ninjaUntargetableTimer",0f);Call(battle,"UpdateCommonSkillFocus");
        Assert.That(Local(primary),Is.SameAs(state));Assert.That(Get<double>(state,"startedAt"),Is.EqualTo(started));
        Assert.That(Get<object>(state,"pending"),Is.Null);Assert.That(UnitField<int>(primary,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitField<object>(primary,"tongueHarpoonTarget"),Is.SameAs(target));
        Call(battle,"UpdateCommonSkillFocus");Assert.That(UnitField<int>(primary,"skillUses"),Is.EqualTo(1));
    }

    [Test]
    public void APreparedRepeatedWizardBarrageRetainsItsTargetPlanWhenTheTargetMovesAway()
    {
        SetUnit(primary,"skillId",FamilySharedSkillSetup.WizardMouseSkillId);
        SetUnit(primary,"skillTimer",4f);Call(battle,"ConfigureSkillExecutor");
        Assert.That(Call(battle,"QueueRepeatedSkillFocus",primary,target,.5f),Is.True);
        object request=Get<object>(Local(primary),"pending");
        Assert.That((IList)Get<object>(request,"castTargets"),Does.Contain(target));
        targetModel.transform.position=Vector3.right*40f;
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.False);
        MaturePreparation(primary);
        Assert.That(UnitField<int>(primary,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitField<float>(primary,"skillTimer"),Is.EqualTo(4f),
            "Repeating the skill preserves the original cooldown.");
        Assert.That(UnitField<bool>(primary,"wizardBarragePending"),Is.True);
        Assert.That((IList)UnitField<object>(primary,"wizardBarrageTargets"),Does.Contain(target));
        Assert.That(Get<object>(Local(primary),"pending"),Is.Null);AssertPresented(primary);
    }

    [TestCase(FamilySharedSkillSetup.RaptorSkillId,"leapTarget")]
    [TestCase(FamilySharedSkillSetup.GriffinSkillId,"griffinSkyfallTarget")]
    [TestCase(FamilySharedSkillSetup.NinjaCatSkillId,"ninjaHuntTarget")]
    public void ARepeatedSelectorSkillCapturesItsInitiallyReachableTargetInsteadOfAFarRepeatTarget(string skillId,string targetField)
    {
        SetUnit(primary,"skillId",skillId);SetUnit(primary,"skillTimer",4f);
        targetModel.transform.position=Vector3.right*40f;
        SetUnit(secondary,"team",Enum.ToObject(unitType.GetField("team").FieldType,1));
        secondaryModel.transform.position=Vector3.right*.8f;
        Call(battle,"ConfigureSkillExecutor");
        Assert.That(Call(battle,"QueueRepeatedSkillFocus",primary,target,.5f),Is.True);
        Assert.That(Get<object>(Get<object>(Local(primary),"pending"),"castTarget"),Is.SameAs(secondary));
        secondaryModel.transform.position=Vector3.right*35f;
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.False);
        MaturePreparation(primary);
        Assert.That(UnitField<int>(primary,"skillUses"),Is.EqualTo(1));
        Assert.That(UnitField<float>(primary,"skillTimer"),Is.EqualTo(4f));
        if(skillId==FamilySharedSkillSetup.GriffinSkillId)
            Assert.That(UnitField<bool>(primary,"griffinSkyfalling"),Is.True);
        else Assert.That(UnitField<object>(primary,targetField),Is.SameAs(secondary));
        Assert.That(Get<object>(Local(primary),"pending"),Is.Null);AssertPresented(primary);
    }

    [TestCase(FamilySharedSkillSetup.NinjaCatSkillId)]
    [TestCase(FamilySharedSkillSetup.WizardMouseSkillId)]
    public void AnAreaTargetSelectorCanStartForANearbyEnemyWhileTheCurrentTargetIsFarAway(string skillId)
    {
        SetUnit(primary,"skillId",skillId);RegisterSpy(skillId);
        targetModel.transform.position=Vector3.right*50f;
        SetUnit(secondary,"team",Enum.ToObject(unitType.GetField("team").FieldType,1));
        secondaryModel.transform.position=Vector3.right*.8f;
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.True);
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.True);
        Assert.That(Get<object>(Local(primary),"unit"),Is.SameAs(primary));
        Assert.That(UnitField<object>(primary,"target"),Is.SameAs(target),
            "The preflight must accept the real handler's area selector without silently changing the current target.");
        Assert.That(Count(commits,primary),Is.Zero);MaturePreparation(primary);
        Assert.That(Count(commits,primary),Is.EqualTo(1));Assert.That(Time.timeScale,Is.EqualTo(1f));
        AssertPresented(primary);
    }

    [Test]
    public void TheLeapPreflightHonorsAFarForcedTargetBeforeChoosingAnAvailableNearbyEnemy()
    {
        SetUnit(primary,"skillId",FamilySharedSkillSetup.RaptorSkillId);
        RegisterSpy(FamilySharedSkillSetup.RaptorSkillId);
        targetModel.transform.position=Vector3.right*50f;
        SetUnit(secondary,"team",Enum.ToObject(unitType.GetField("team").FieldType,1));
        secondaryModel.transform.position=Vector3.right*.8f;
        SetUnit(primary,"forcedTarget",target);SetUnit(primary,"forcedTargetTimer",2f);
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.False);
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.False);
        Assert.That(Has(primary),Is.False);Assert.That(director.SkillFocusActive,Is.False);
        Assert.That(Count(attempts,primary),Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(1f));

        SetUnit(primary,"forcedTarget",null);SetUnit(primary,"forcedTargetTimer",0f);
        float castRange=Mathf.Max(1f,GameMasterData.Skill(FamilySharedSkillSetup.RaptorSkillId).castRangeTiles);
        Assert.That(Call(battle,"SelectRaptorLeapTarget",primary,castRange),Is.SameAs(secondary));
        Assert.That(Call(battle,"CanStartFocusedSkill",primary,target),Is.True);
        Assert.That(Call(battle,"BeginCommonSkillFocus",primary,false),Is.True);
        Assert.That(Count(commits,primary),Is.Zero);MaturePreparation(primary);
        Assert.That(Count(commits,primary),Is.EqualTo(1));Assert.That(Time.timeScale,Is.EqualTo(1f));
        AssertPresented(primary);
    }

    [TestCase(false)] [TestCase(true)]
    public void HaetaeSupportCannotStartAfterTheLastEnemyDiesEvenWhenAnotherCasterIsAlreadyPresenting(bool otherCasterActive)
    {
        SetUnit(secondary,"skillId",FamilySharedSkillSetup.HaetaeSkillId);RegisterSpy(FamilySharedSkillSetup.HaetaeSkillId);
        UnitField<CombatStatusState>(secondary,"combatStatuses").ApplyTimed(CombatStatusKind.Poison,5f,target);
        Assert.That(Call(battle,"HaetaeSupportTargets",secondary,GameMasterData.Skill(FamilySharedSkillSetup.HaetaeSkillId)),Is.GreaterThan(0));
        if(otherCasterActive){Assert.That(Use(primary),Is.True);MaturePreparation(primary);}
        SetUnit(target,"alive",false);
        Assert.That(Call(battle,"BeginCommonSkillFocus",secondary,false),Is.False);Call(battle,"UpdateCommonSkillFocus");
        Assert.That(Has(secondary),Is.False);Assert.That(Count(attempts,secondary),Is.Zero);
        Assert.That(UnitField<int>(secondary,"skillUses"),Is.Zero);Assert.That(UnitField<float>(secondary,"skillTimer"),Is.Zero);
        Assert.That(UnitField<CombatStatusState>(secondary,"combatStatuses").Has(CombatStatusKind.Poison),Is.True);
        Assert.That(Has(primary),Is.EqualTo(otherCasterActive));Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void MenuRetainsAcceptedCastsWhileDisableClearsAndReusesTheirCachedMirrors()
    {
        foreach(object caster in new[]{primary,secondary,target})Assert.That(Use(caster),Is.True);
        object retained=Local(primary);var mirror=Get<SkillFocusSpriteMirror>(retained,"mirror");
        var ghosts=Get<SpriteRenderer[]>(mirror,"gatherSprites");
        battle.PrepareForSystemMenu();
        foreach(object caster in new[]{primary,secondary,target})
        {Assert.That(Has(caster),Is.True);Assert.That(Get<object>(Local(caster),"pending"),Is.Not.Null);}
        Assert.That(Local(primary),Is.SameAs(retained));
        Call(battle,"OnDisable");
        foreach(object caster in new[]{primary,secondary,target})
        {Assert.That(Has(caster),Is.False);Assert.That(Scale(caster),Is.EqualTo(1f));}
        Assert.That(Get<bool>(retained,"active"),Is.False);Assert.That(mirror.gameObject.activeSelf,Is.False);
        ReadyAgain(primary);ReadyAgain(secondary);
        Assert.That(Use(secondary),Is.True);Assert.That(Use(primary),Is.True);
        Assert.That(Pool.Count,Is.EqualTo(3));Assert.That(Local(secondary),Is.SameAs(retained));
        Assert.That(Get<SkillFocusSpriteMirror>(retained,"mirror"),Is.SameAs(mirror));
        Assert.That(Get<SpriteRenderer[]>(mirror,"gatherSprites"),Is.SameAs(ghosts));
        Call(battle,"OnDisable");
        Assert.That(Has(primary),Is.False);Assert.That(Has(secondary),Is.False);
        Assert.That(Scale(primary),Is.EqualTo(1f));Assert.That(Scale(secondary),Is.EqualTo(1f));
        Assert.That(mirror.gameObject.activeSelf,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    private void FinishPresentation(object caster)
    {
        object state=Local(caster);Advance(Driver(caster),Driver(caster).SkillAttackDuration+1f);
        Set(state,"committedAt",Time.timeAsDouble-.2d);Call(battle,"UpdateCommonSkillFocus");
        Set(state,"recoveryAt",Time.timeAsDouble-.15d);Call(battle,"UpdateCommonSkillFocus");
    }
    private void MaturePreparation(object caster)
    {
        double now=Time.unscaledTimeAsDouble;
        Set(Local(caster),"startedAt",now-PreparationDuration(caster)-.001d);
        if(Get<bool>(Local(caster),"allyPreparationMember"))
        {
            foreach(object state in Pool)
                if(Get<bool>(state,"active")&&Get<bool>(state,"allyPreparationMember"))
                    Set(state,"startedAt",now-PreparationDuration(Get<object>(state,"unit"))-.001d);
            Set(battle,"allyPreparationSequenceEndsAt",now-.001d);
        }
        Call(battle,"UpdateCommonSkillFocus");
    }

    private float PreparationDuration(object caster)=>Convert.ToInt32(UnitField<object>(caster,"team"))==0?1f:.35f;

    private bool Commit<TContext>(TContext context,SkillMasterRow skill)
    {
        object caster=context.GetType().GetField("source").GetValue(context);
        attempts[caster]=Count(attempts,caster)+1;
        if(caster==rejected)return false;
        commits[caster]=Count(commits,caster)+1;
        SetUnit(caster,"skillUses",Count(commits,caster));SetUnit(caster,"skillTimer",4f);
        Driver(caster).PlaySkillAttack(1f,null);
        return true;
    }

    private void RegisterSpy(string skillId)
    {
        var field=typeof(MonsterAutoBattlePrototype).GetField("skillExecutor",Private);
        object executor=field.GetValue(battle);
        Type context=field.FieldType.GetGenericArguments()[0];
        var handler=typeof(Func<,,>).MakeGenericType(context,typeof(SkillMasterRow),typeof(bool));
        var spy=Delegate.CreateDelegate(handler,this,GetType().GetMethod("Commit",Private).MakeGenericMethod(context));
        field.FieldType.GetMethod("Register").Invoke(executor,new object[]{
            GameMasterData.Skill(skillId).behavior,MonsterSkillTrigger.Active,spy});
    }

    private void AssertPresented(object caster)
    {
        Assert.That(Has(caster),Is.True);
        bool member=Get<bool>(Local(caster),"allyPreparationMember");
        bool spotlight=!Get<bool>(battle,"allyPreparationSequenceActive")||member;
        bool executingAlly=Get<object>(Local(caster),"pending")==null&&Get<bool>(Local(caster),"allySkillEmphasis");
        Assert.That(Scale(caster),Is.EqualTo(member||executingAlly?1.5f:1f));
        Assert.That(Hud(caster),Is.True,"Health, cooldown and buff icons remain visible during the skill presentation.");
        Call(battle,"RefreshUnitOutlines");
        var outline=UnitField<ScreenSpaceOutlineTarget>(caster,"outline");
        Assert.That(outline.IsOutlined,Is.EqualTo(spotlight));
        if(!spotlight)return;
        Color emphasis=Get<Color>(outline,"color");
        Assert.That(emphasis.r,Is.GreaterThan(emphasis.b+.15f));
        Assert.That(emphasis.g,Is.GreaterThan(emphasis.b+.1f),"Both teams need the gold skill emphasis rather than their normal team outline.");
    }

    private object Unit(GameObject model,int team)
    {
        object caster=Activator.CreateInstance(unitType,true);
        SetUnit(caster,"model",model);var ring=new GameObject("Multi-caster ring");ring.transform.SetParent(model.transform);
        SetUnit(caster,"ring",ring);SetUnit(caster,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        SetUnit(caster,"definition",Resources.Load<MonsterDefinition>("MonsterData/Raputo"));
        SetUnit(caster,"stableId",Guid.NewGuid().ToString());SetUnit(caster,"skillId","guardian_taunt");
        SetUnit(caster,"alive",true);SetUnit(caster,"hp",5000f);SetUnit(caster,"maxHp",5000f);
        SetUnit(caster,"damage",100f);SetUnit(caster,"attackInterval",1f);SetUnit(caster,"attackRange",1);
        SetUnit(caster,"collisionRadius",.3f);
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        SetUnit(caster,"spriteAnimation",driver);
        if(driver!=null)SetUnit(caster,"outline",model.GetComponent<ScreenSpaceOutlineTarget>()??model.AddComponent<ScreenSpaceOutlineTarget>());
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle)).Add(caster);
        return caster;
    }

    private IList Pool=>(IList)typeof(MonsterAutoBattlePrototype).GetField("localSkillPresentations",Private).GetValue(battle);
    private object Local(object caster)
    {
        foreach(object state in Pool)if(Get<bool>(state,"active")&&Get<object>(state,"unit")==caster)return state;
        Assert.Fail("No active local presentation for the requested caster.");return null;
    }
    private bool Use(object caster)=>(bool)Call(battle,"TryUseSkill",caster,false);
    private bool Has(object caster)=>(bool)Call(battle,"HasSkillCastPresentation",caster);
    private float Scale(object caster)=>Get<float>(Driver(caster),"skillFocusScale");
    private SpriteMonsterAnimationDriver Driver(object caster)=>UnitField<SpriteMonsterAnimationDriver>(caster,"spriteAnimation");
    private bool Hud(object caster)
    {object[] args={caster,new Rect()};return (bool)Call(battle,"TryGetUnitWorldHudRect",args);}
    private void ReadyAgain(object caster){Driver(caster).InterruptCurrentAction(true);SetUnit(caster,"skillTimer",0f);}
    private static void Advance(SpriteMonsterAnimationDriver driver,float seconds){Call(driver,"Advance",seconds);Call(driver,"LateUpdate");}
    private static int Count(Dictionary<object,int> values,object caster)=>values.TryGetValue(caster,out int count)?count:0;
    private static GameObject Spawn()=>PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
    private void SetUnit(object caster,string field,object value)=>unitType.GetField(field).SetValue(caster,value);
    private T UnitField<T>(object caster,string field)=>(T)unitType.GetField(field).GetValue(caster);
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Fields).SetValue(value,data);
    private static T Get<T>(object value,string field)=>(T)value.GetType().GetField(field,Fields).GetValue(value);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Private).Invoke(value,args);
}
