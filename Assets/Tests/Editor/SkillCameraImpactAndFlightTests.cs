using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class SkillCameraImpactAndFlightTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private HashSet<GameObject> existing;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private SpriteMonsterAnimationDriver driver;
    private Type unitType;
    private object source,target,previousFrontend;
    private float previousSpeed;
    private CombatSkillClock clock;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousSpeed=Time.timeScale;Time.timeScale=1f;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        var host=new GameObject("Inactive impact camera test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);Set(battle,"expeditionRun",new ExpeditionRunSave());
        Set(battle,"combatBounds",new Bounds(Vector3.zero,new Vector3(40f,4f,40f)));
        var cameraHost=new GameObject("Impact completion camera");
        var camera=cameraHost.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-10f),Quaternion.Euler(35f,0f,0f));
        director=cameraHost.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        source=Unit(0,Vector3.zero);target=Unit(1,Vector3.right*.8f);
        UnitSet(source,"target",target);driver=UnitGet<SpriteMonsterAnimationDriver>(source,"spriteAnimation");
    }

    [TearDown]
    public void TearDown()
    {
        clock?.End();
        if(battle!=null)Call(battle,"CancelCommonSkillFocus");
        if(director!=null)director.EndBattle();
        foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
            if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&created.transform.parent==null)
                Object.DestroyImmediate(created);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
        Time.timeScale=previousSpeed;
    }

    [TestCase("")]
    [TestCase(RaptorCombatRules.PiercingHuntTraitId)]
    [TestCase(RaptorCombatRules.RelentlessHuntTraitId)]
    [TestCase(RaptorCombatRules.ArmorHuntTraitId)]
    public void RaptorReleasesAfterItsLastContactWhileItsBodyStillReturns(string trait)
    {
        Prepare("raptor_hunting_leap","MonsterData/Raputo",trait);
        Assert.That(Call(battle,"StartAssassinLeap",source,target,1f),Is.True);
        driver.PlaySkillAttack(1f,null);
        UnitSet(source,"leapProgress",1f);
        UnitGet<GameObject>(source,"model").transform.position=UnitGet<Vector3>(source,"leapEnd");
        if(trait==RaptorCombatRules.PiercingHuntTraitId)
            Call(battle,"ApplyRaptorPiercingSweep",source,Vector3.zero,Vector3.right*2f);
        Call(battle,"ResolveAssassinLeapImpact",source);
        if(trait==RaptorCombatRules.RelentlessHuntTraitId)
        {
            Call(battle,"UpdateSkillCameraPresentation");
            Assert.That(director.AnticipationPanActive,Is.True,"The first contact must not release a three-hit hunt.");
            Assert.That(UnitGet<int>(source,"leapHitCount"),Is.EqualTo(1));
            Call(battle,"UpdateRelentlessHunt",source,.4f);
            Assert.That(UnitGet<int>(source,"leapHitCount"),Is.EqualTo(3));
        }
        Assert.That(UnitGet<float>(target,"hp"),Is.LessThan(5000f));
        Call(battle,"UpdateSkillCameraPresentation");
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(UnitGet<bool>(source,"leaping"),Is.True);
        Assert.That(UnitGet<bool>(source,"leapReturning"),Is.True);
        Assert.That(driver.IsSkillActionActive,Is.True,"Only camera ownership ends; the body animation remains intact.");
        Assert.That(Call(battle,"HasSkillCastPresentation",source),Is.True);
    }

    [Test]
    public void RelentlessHuntReleasesWhenItsVictimDiesBeforeTheRemainingContacts()
    {
        Prepare("raptor_hunting_leap","MonsterData/Raputo",RaptorCombatRules.RelentlessHuntTraitId);
        Call(battle,"StartAssassinLeap",source,target,1f);driver.PlaySkillAttack(1f,null);
        UnitSet(source,"leapProgress",1f);
        UnitGet<GameObject>(source,"model").transform.position=UnitGet<Vector3>(source,"leapEnd");
        Call(battle,"ResolveAssassinLeapImpact",source);
        Assert.That(UnitGet<int>(source,"leapHitCount"),Is.EqualTo(1));
        float remaining=UnitGet<float>(target,"hp");UnitSet(target,"alive",false);
        Call(battle,"UpdateRelentlessHunt",source,.1f);Call(battle,"UpdateSkillCameraPresentation");
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(UnitGet<float>(target,"hp"),Is.EqualTo(remaining));
        Assert.That(UnitGet<bool>(source,"leapReturning"),Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PreyCannonKeepsTheCameraThroughFlightAndReleasesAfterLandingDamageOrCancellation(bool cancel)
    {
        Prepare(CrocodileCombatRules.SkillId,"MonsterData/Monster_ager",CrocodileCombatRules.PreyCannonTraitId);
        UnitSet(source,"crocodileRollTarget",target);UnitSet(source,"skillUses",1);
        driver.PlaySkillAttack(1f,null);
        IEnumerator cast=(IEnumerator)Call(battle,"ResolveDeathRoll",source,GameMasterData.Skill(CrocodileCombatRules.SkillId),Generation);
        object focus=Get<object>(battle,"skillCameraPresentation");
        try
        {
            bool more=true;
            for(int step=0;step<30&&!UnitGet<bool>(target,"crocodilePreyFlying");step++)
            {more=Step(cast,.1f);if(!more)break;}
            Assert.That(more,Is.True);Assert.That(UnitGet<bool>(target,"crocodilePreyFlying"),Is.True);
            Assert.That(Field<int>(focus,"pendingEffects"),Is.EqualTo(1));
            driver.InterruptCurrentAction(true);Call(battle,"UpdateSkillCameraPresentation");
            Assert.That(director.AnticipationPanActive,Is.True,"The caster has recovered but its thrown victim is still airborne.");
            float beforeLanding=UnitGet<float>(target,"hp");
            if(cancel)(cast as IDisposable).Dispose();
            else
            {
                Call(battle,"UpdateDisplacement",target,1f);
                Assert.That(UnitGet<bool>(target,"displacing"),Is.False);
                Assert.That(Step(cast,.1f),Is.False);
                Assert.That(UnitGet<float>(target,"hp"),Is.LessThan(beforeLanding),"Release follows actual landing damage, not the throw.");
                Assert.That(UnitGet<int>(source,"skillHits"),Is.EqualTo(3));
            }
            Assert.That(Field<int>(focus,"pendingEffects"),Is.Zero);
            Call(battle,"UpdateSkillCameraPresentation");Assert.That(director.AnticipationPanActive,Is.False);
        }
        finally{(cast as IDisposable).Dispose();}
        Assert.That(Field<int>(focus,"pendingEffects"),Is.Zero,"The finally path must not complete the same hold twice.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ContinentalFissureKeepsTheCameraUntilTheLastWaveDamageButDoesNotWaitForItsTail(bool cancel)
    {
        Prepare(KingKongCombatRules.SkillId,"MonsterData/Monster_lilas",KingKongCombatRules.ContinentalFissureTraitId);
        object far=Unit(1,Vector3.right*(MonsterAutoBattlePrototype.TileSize*KingKongCombatRules.ContinentalFissureRadiusTiles-.01f));
        IEnumerator wave=(IEnumerator)Call(battle,"ResolveContinentalFissure",source,GameMasterData.Skill(KingKongCombatRules.SkillId),Vector3.zero,1f,Generation);
        object focus=Get<object>(battle,"skillCameraPresentation");
        bool disposed=false;
        const string editModeDestroy="Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.";
        try
        {
            Assert.That(Step(wave,.1f),Is.True);Assert.That(Field<int>(focus,"pendingEffects"),Is.EqualTo(1));
            Call(battle,"UpdateSkillCameraPresentation");Assert.That(director.AnticipationPanActive,Is.True);
            Assert.That(UnitGet<float>(far,"hp"),Is.EqualTo(5000f));
            if(cancel)
            {
                LogAssert.Expect(LogType.Error,editModeDestroy);
                (wave as IDisposable).Dispose();disposed=true;
            }
            else
            {
                bool more=true;
                for(int step=0;step<10&&Field<int>(focus,"pendingEffects")>0;step++)more=Step(wave,.1f);
                Assert.That(more,Is.True,"The visual fade is still alive after its damage front finishes.");
                Assert.That(UnitGet<float>(far,"hp"),Is.LessThan(5000f),"The farthest target must be damaged before camera release.");
            }
            Assert.That(Field<int>(focus,"pendingEffects"),Is.Zero);
            Call(battle,"UpdateSkillCameraPresentation");Assert.That(director.AnticipationPanActive,Is.False);
        }
        finally
        {
            if(!disposed)
            {
                LogAssert.Expect(LogType.Error,editModeDestroy);
                (wave as IDisposable).Dispose();
            }
        }
        Assert.That(Field<int>(focus,"pendingEffects"),Is.Zero);
    }

    private void Prepare(string skill,string definition,string trait)
    {
        UnitSet(source,"skillId",skill);UnitSet(source,"definition",Resources.Load<MonsterDefinition>(definition));
        Assert.That(UnitGet<MonsterDefinition>(source,"definition"),Is.Not.Null);
        if(!string.IsNullOrEmpty(trait))((IList)unitType.GetField("activeTraitIds").GetValue(source)).Add(trait);
        Assert.That(Call(battle,"BeginCommonSkillFocus",source,false),Is.True);
        Assert.That(director.AnticipationPanActive,Is.True);
        object local=Call(battle,"SkillPresentationFor",source);
        // Exercise real impact/flight routines without dispatching a second skill through the preparation loop.
        local.GetType().GetField("pending").SetValue(local,null);
        local.GetType().GetField("committedAt").SetValue(local,Time.timeAsDouble-.2d);
        driver.SetSkillAnticipating(false);
    }

    private object Unit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var model=new GameObject("Impact camera unit");model.transform.position=position;
        var renderer=model.AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
        var animation=model.AddComponent<SpriteMonsterAnimationDriver>();
        var frames=new[]{renderer.sprite,renderer.sprite,renderer.sprite,renderer.sprite,renderer.sprite,renderer.sprite};
        animation.Configure(frames,frames,frames,frames,frames,frames,frames);
        var ring=new GameObject("Impact camera ring");ring.transform.SetParent(model.transform,false);
        UnitSet(unit,"id",((IList)Get<object>(battle,"units")).Count);UnitSet(unit,"stableId",Guid.NewGuid().ToString());
        UnitSet(unit,"model",model);UnitSet(unit,"ring",ring);UnitSet(unit,"spriteAnimation",animation);
        UnitSet(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        UnitSet(unit,"alive",true);UnitSet(unit,"hp",5000f);UnitSet(unit,"maxHp",5000f);
        UnitSet(unit,"damage",100f);UnitSet(unit,"attackInterval",1f);UnitSet(unit,"attackRange",1);
        UnitSet(unit,"collisionRadius",.3f);UnitSet(unit,"skillTraitDamageMultiplier",1f);UnitSet(unit,"skillTraitRadiusMultiplier",1f);
        ((IList)Get<object>(battle,"units")).Add(unit);return unit;
    }

    private bool Step(IEnumerator routine,float dt)
    {
        if(clock==null){clock=new CombatSkillClock(UnitGet<GameObject>(source,"model").transform);clock.BeginExecution(1f);}
        Set(clock,"delta",dt);Set(clock,"credit",Get<float>(clock,"credit")+dt);
        using(CombatSkillClock.Enter(clock))return routine.MoveNext();
    }
    private int Generation=>Get<int>(battle,"combatGeneration");
    private void UnitSet(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private T UnitGet<T>(object unit,string name)=>(T)unitType.GetField(name).GetValue(unit);
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name).GetValue(value);
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Private).SetValue(value,data);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Private).Invoke(value,args);
}
