using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class FrogHarpoonMotionTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object source,target;
    private int nextId;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Frog Harpoon Test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=battleType.GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        battleType.GetField("combatBounds",Private).SetValue(battle,
            new Bounds(Vector3.zero,Vector3.one*100f));
        source=Unit(0,Vector3.zero);target=Unit(1,Vector3.right*4f);
        Set(source,"target",target);Set(target,"target",source);
        Set(source,"tongueHarpoonTarget",target);
    }

    [TearDown]
    public void TearDown()
    {
        foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
            if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
               created.transform.parent==null)Object.DestroyImmediate(created);
    }

    private object Unit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var model=new GameObject("Harpoon Unit");model.transform.position=position;
        Set(unit,"id",nextId++);Set(unit,"model",model);Set(unit,"ring",new GameObject("Harpoon Ring"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_golgaebi"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",FrogCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);
        Set(unit,"collisionRadius",0.3f);
        ((IList)battleType.GetField("units",Private).GetValue(battle)).Add(unit);
        return unit;
    }
    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private Vector3 Position(object unit)=>((GameObject)Get(unit,"model")).transform.position;
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private object Call(string name,params object[] args)=>battleType.GetMethod(name,Private).Invoke(battle,args);
    private IEnumerator Cast()=> (IEnumerator)Call("ResolveTongueHarpoon",source,target,
        GameMasterData.Skill(FrogCombatRules.SkillId),
        battleType.GetField("combatGeneration",Private).GetValue(battle),1f,1f);
    private void Step(float dt)
    {
        foreach(var unit in (IList)battleType.GetField("units",Private).GetValue(battle))
            if((bool)Get(unit,"displacing"))Call("UpdateDisplacement",unit,dt);
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void TensionThenSmoothPullAndHopDealReturnDamageOnlyOnLanding(int fps)
    {
        var cast=Cast();Assert.That(cast.MoveNext(),Is.True);
        Assert.That(Position(source),Is.EqualTo(Vector3.zero));
        Assert.That(Position(target).x,Is.EqualTo(4f));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(4870f).Within(0.01f));
        Assert.That((float)Get(target,"frogSlowTimer"),Is.Zero);
        Assert.That(cast.MoveNext(),Is.True);
        Step(1f/fps);
        Assert.That(Mathf.Abs(Position(source).x),Is.InRange(0.001f,0.05f));
        Assert.That(Position(source).y,Is.GreaterThan(0f));
        Assert.That(Position(target).y,Is.Zero);
        float elapsed=1f/fps;
        while(elapsed<FrogCombatRules.PullDuration-0.00001f)
        {
            Assert.That((float)Get(target,"hp"),Is.EqualTo(4870f).Within(0.01f));
            Assert.That(cast.MoveNext(),Is.True);
            float dt=Mathf.Min(1f/fps,FrogCombatRules.PullDuration-elapsed);
            Step(dt);elapsed+=dt;
        }
        Step(0.00001f);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Position(source).x,Is.EqualTo(-2.55f).Within(0.001f));
        Assert.That(Position(source).y,Is.Zero);
        Assert.That(Position(target).x,Is.EqualTo(2.725f).Within(0.001f));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(4820f).Within(0.01f));
        Assert.That((float)Get(target,"frogSlowTimer"),Is.EqualTo(4f).Within(0.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Get(source,"tongueHarpoonTarget"),Is.Null);
    }

    [TestCase("unit")]
    [TestCase("edge")]
    public void BlockedRecoilStopsAtObstacleAndStillLands(string obstacle)
    {
        if(obstacle=="unit")Unit(0,Vector3.left*1.1f);
        else battleType.GetField("combatBounds",Private).SetValue(battle,
            new Bounds(new Vector3(4.5f,0,0),new Vector3(10,10,10)));
        var cast=Cast();cast.MoveNext();cast.MoveNext();
        for(int i=0;i<30;i++){Step(0.02f);if(!cast.MoveNext())break;}
        Assert.That(Position(source).x,Is.GreaterThan(-0.6f));
        Assert.That(Position(source).y,Is.Zero);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
    }

    [Test]
    public void ControlImmuneEnemyStaysPutWhileImmuneCasterCanRecoil()
    {
        Status(target).ControlImmune=true;Status(source).ControlImmune=true;
        var cast=Cast();cast.MoveNext();cast.MoveNext();Step(0.45f);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Position(target).x,Is.EqualTo(4f));
        Assert.That(Position(source).x,Is.EqualTo(-2.55f).Within(0.001f));
        Assert.That(Position(source).y,Is.Zero);
    }

    [TestCase("source_death")]
    [TestCase("target_death")]
    [TestCase("source_interrupt")]
    [TestCase("target_interrupt")]
    public void InterruptedPullReleasesBothAndDoesNotDealReturnDamage(string reason)
    {
        var cast=Cast();cast.MoveNext();cast.MoveNext();Step(0.2f);
        object unit=reason.StartsWith("source")?source:target;
        if(reason.EndsWith("death"))Set(unit,"alive",false);
        else Call("InterruptPositionalAction",unit);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Get(source,"tongueHarpoonTarget"),Is.Null);
        Assert.That((float)Get(target,"hp"),Is.EqualTo(4870f).Within(0.01f));
        Assert.That(Position(source).y,Is.Zero);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
    }

    [Test]
    public void SoulRecoveryStillHealsWhenFirstHitKillsTarget()
    {
        ((List<string>)Get(source,"activeTraitIds")).Add(FrogCombatRules.FroskTraitId);
        Set(source,"hp",4000f);Set(target,"hp",100f);
        var cast=Cast();cast.MoveNext();Assert.That(cast.MoveNext(),Is.False);
        Assert.That((float)Get(source,"hp"),Is.EqualTo(4030f).Within(0.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
    }

    [TestCase(0f,4f)]
    [TestCase(3f,5.4568f)]
    [TestCase(7f,7.4568f)]
    [TestCase(8f,7.9568f)]
    [TestCase(10f,9.9136f)]
    public void ReflexSnatchRemovesHalfTheChargedGaugeOnlyOnTheReturnHit(float remaining,float expected)
    {
        ((List<string>)Get(source,"activeTraitIds")).Add(FrogCombatRules.ReflexSnatchTraitId);
        Set(target,"skillCooldownDuration",8f);Set(target,"skillTimer",remaining);
        var cast=Cast();cast.MoveNext();
        // The normal damage-taken cooldown recovery still applies on both hits.
        Assert.That((float)Get(target,"skillTimer"),Is.EqualTo(Mathf.Max(0f,remaining-.0624f)).Within(.001f));
        cast.MoveNext();Step(0.45f);Assert.That(cast.MoveNext(),Is.False);
        Assert.That((float)Get(target,"skillTimer"),Is.EqualTo(expected).Within(.001f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
    }

    [TestCase(10f,4f,7f)]
    [TestCase(10f,10f,10f)]
    [TestCase(10f,0f,5f)]
    [TestCase(10f,12f,12f)]
    [TestCase(0f,0f,0f)]
    public void ReflexSnatchDrainsEarnedChargeWithoutShorteningExistingLongerDelays(
        float duration,float remaining,float expected)
    {
        Set(target,"skillCooldownDuration",duration);Set(target,"skillTimer",remaining);
        Call("DrainFrogCooldownCharge",source,target);
        Assert.That((float)Get(target,"skillTimer"),Is.EqualTo(expected).Within(.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BuffTheftPrefersShieldAndCopiesOnlyHalfFromBosses(bool boss)
    {
        var definition=Object.Instantiate((MonsterDefinition)Get(target,"definition"));
        try
        {
            definition.isBoss=boss;Set(target,"definition",definition);
            Set(source,"maxHp",100f);Set(source,"hp",100f);
            Set(target,"itemShield",40f);Set(target,"itemShieldTimer",5f);
            Set(target,"itemAttackSpeedBonus",0.3f);Set(target,"itemAttackSpeedTimer",5f);
            Call("ApplyFrogBuffTheft",source,target);
            Assert.That((float)Call("TotalVisibleShield",source),Is.EqualTo(boss?6f:12f).Within(.001f));
            Assert.That(Get(target,"itemShield"),Is.EqualTo(boss?40f:28f));
            Assert.That(Get(target,"itemAttackSpeedBonus"),Is.EqualTo(.3f));
        }
        finally{Object.DestroyImmediate(definition);}
    }

    [TestCase(FrogCombatRules.KingpaTraitId)]
    [TestCase(FrogCombatRules.FroskTraitId)]
    public void BufflessEnemyLosesAttackSpeedAndCasterGetsTheFullBuff(string intrinsic)
    {
        ((List<string>)Get(source,"activeTraitIds")).Add(intrinsic);
        object ally=Unit(0,Vector3.forward*4f);
        Set(ally,"hp",10f);Set(ally,"frogRoyalAttackSpeedTimer",3f);
        Call("ApplyFrogBuffTheft",source,target);
        Assert.That(Get(source,"frogStolenBuffMagnitude"),Is.EqualTo(.15f));
        Assert.That(Get(target,"frogStolenBuffMagnitude"),Is.EqualTo(-.15f));
        Assert.That(Get(source,"frogStolenBuffTimer"),Is.EqualTo(4f));
        Assert.That(Get(ally,"frogStolenBuffTimer"),Is.EqualTo(0f));
    }

    [Test]
    public void TongueSlamRisesOnAnArcThenFallsFastAndUsesTheKnockdownAnimation()
    {
        ((List<string>)Get(source,"activeTraitIds")).Add(FrogCombatRules.TongueSlamTraitId);
        var model=(GameObject)Get(target,"model");model.AddComponent<SpriteRenderer>();
        var animation=model.AddComponent<SpriteMonsterAnimationDriver>();
        var frames=new[]{PixelStarProjectile.BeanSprite,PixelStarProjectile.BeanSprite};
        animation.Configure(frames,frames,frames,frames,frames,frames,frames);
        animation.ConfigureSpecialClips(null,null,UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Generated/PixelSpecialAnimationClips/024/024_Knockdown.anim"));
        Set(target,"spriteAnimation",animation);
        var cast=Cast();cast.MoveNext();cast.MoveNext();
        Step(.35f);
        Assert.That(Position(target).y,Is.EqualTo(2.4f).Within(.001f));
        Assert.That(Status(target).Has(CombatStatusKind.Knockdown),Is.False);
        Step(.15f);
        Assert.That(Position(target).y,Is.EqualTo(2.4f).Within(.001f));
        Step(.1f);
        Assert.That(Position(target).y,Is.EqualTo(1.8f).Within(.001f));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(4870f).Within(.01f));
        Step(.101f);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Position(target).y,Is.Zero);
        Assert.That(Position(target).x,Is.EqualTo(2.725f).Within(.001f));
        Assert.That(Status(target).Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(.8f));
        Assert.That(animation.CurrentMotionName,Is.EqualTo("Death"));
        Assert.That(animation.KnockdownClip.name,Does.EndWith("_Knockdown"));
        Assert.That(Get(target,"alive"),Is.True);
        Assert.That((float)typeof(SpriteMonsterAnimationDriver).GetField("knockdownRemaining",Private)
            .GetValue(animation),Is.EqualTo(.8f));
        var debris=GameObject.Find("Frog Tongue Slam Debris").GetComponent<ParticleSystem>();
        Assert.That(GameObject.Find("Frog Tongue Slam Dust"),Is.Null);
        Assert.That(GameObject.Find("Frog Tongue Slam Ring"),Is.Null);
        Assert.That(GameObject.Find("Frog Tongue Slam Crater"),Is.Null);
        Assert.That(debris.GetComponent<ParticleSystemRenderer>().sortingOrder,Is.GreaterThan(0));
        Assert.That(debris.collision.enabled,Is.True);
        Assert.That(debris.collision.GetPlane(0),Is.SameAs(debris.transform));
        Assert.That(debris.main.startSize.constantMin,Is.GreaterThanOrEqualTo(.08f));
        debris.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        debris.useAutoRandomSeed=false;debris.randomSeed=71;
        for(int frame=0;frame<12;frame++)debris.Simulate(.02f,true,frame==0,false);
        Assert.That(debris.particleCount,Is.EqualTo(18),
            "Landing fragments must clear the floor instead of dying on spawn.");
        var fragments=new ParticleSystem.Particle[debris.particleCount];
        debris.GetParticles(fragments);
        foreach(var fragment in fragments)Assert.That(fragment.position.y,Is.GreaterThan(.05f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
        Status(target).Tick(.8f);
        animation.SetKnockdownActive(0f);
        Assert.That(animation.CurrentMotionName,Is.EqualTo("Idle"));
    }

    [Test]
    public void TongueSlamStillLiftsVerticallyWhenTheBoundaryBlocksItsPull()
    {
        battleType.GetField("combatBounds",Private).SetValue(battle,
            new Bounds(new Vector3(4.2f,0,0),new Vector3(1f,10f,10f)));
        Assert.That(Call("BeginFrogHarpoonDisplacement",target,Vector3.left,1f,2.4f,true,true),Is.True);
        Step(.35f);
        Assert.That(Position(target).x,Is.EqualTo(4f).Within(.01f));
        Assert.That(Position(target).y,Is.EqualTo(2.4f).Within(.001f));
        Step(.351f);
        Assert.That(Position(target).y,Is.Zero);
    }

    [Test]
    public void InterruptedSlamDoesNotShakeOrLeaveTheEnemyFloating()
    {
        ((List<string>)Get(source,"activeTraitIds")).Add(FrogCombatRules.TongueSlamTraitId);
        var cast=Cast();cast.MoveNext();cast.MoveNext();Step(.25f);
        Set(source,"alive",false);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Position(target).y,Is.Zero);
        Assert.That(Status(target).Has(CombatStatusKind.Knockdown),Is.False);
        Assert.That(GameObject.Find("Frog Tongue Slam Debris"),Is.Null);
    }

    [Test]
    public void ControlImmuneEnemyCannotBeLiftedOrKnockedDownByTongueSlam()
    {
        ((List<string>)Get(source,"activeTraitIds")).Add(FrogCombatRules.TongueSlamTraitId);
        Status(target).ControlImmune=true;
        var cast=Cast();cast.MoveNext();cast.MoveNext();Step(.7f);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Position(target),Is.EqualTo(Vector3.right*4f));
        Assert.That(Status(target).Has(CombatStatusKind.Knockdown),Is.False);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
    }

    [Test]
    public void FishingSelectsTwoNearestEnemiesWithinThreeTilesOfPrimaryIncludingBeyondCasterRange()
    {
        ((GameObject)Get(target,"model")).transform.position=Vector3.right*8f;
        object near=Unit(1,new Vector3(9,0,0));
        object edge=Unit(1,new Vector3(13.1f,0,0));
        Unit(1,new Vector3(13.11f,0,0));Unit(0,new Vector3(8,0,.5f));
        var selected=(IList)Call("FrogFishingSecondaries",source,target);
        Assert.That(selected.Count,Is.EqualTo(2));
        Assert.That(selected[0],Is.SameAs(near));Assert.That(selected[1],Is.SameAs(edge));
        object closer=Unit(1,new Vector3(8,0,2));
        selected=(IList)Call("FrogFishingSecondaries",source,target);
        Assert.That(selected[1],Is.SameAs(closer));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TranscendentFishingBranchesBeforePullingAndLandsThreeTargetsTogether(bool slam)
    {
        Set(source,"augmentTranscended",true);
        ((List<string>)Get(source,"activeTraitIds")).Add(slam?FrogCombatRules.TongueSlamTraitId:FrogCombatRules.ReflexSnatchTraitId);
        object left=Unit(1,new Vector3(4,0,-2)),right=Unit(1,new Vector3(4,0,2));
        foreach(var unit in new[]{target,left,right}){Set(unit,"skillCooldownDuration",10f);Set(unit,"skillTimer",4f);}
        var cast=Cast();Assert.That(cast.MoveNext(),Is.True);
        Assert.That(Get(source,"currentAction"),Is.EqualTo("tongue_branch"));
        Assert.That(Get(left,"hp"),Is.EqualTo(5000f));
        Assert.That(cast.MoveNext(),Is.True);Assert.That(Get(left,"hp"),Is.EqualTo(4935f));
        Assert.That(Get(left,"displacing"),Is.False);
        Assert.That(cast.MoveNext(),Is.True);Step(slam?.5f:.2f);
        foreach(var unit in new[]{target,left,right})
        {
            Assert.That(Get(unit,"displacing"),Is.True);
            if(slam)Assert.That(Position(unit).y,Is.EqualTo(2.4f).Within(.001f));
        }
        Step(.26f);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(6));
        Assert.That(Get(target,"hp"),Is.EqualTo(4820f));Assert.That(Get(left,"hp"),Is.EqualTo(4910f));
        foreach(var unit in new[]{target,left,right})
        {
            Assert.That(Position(unit).y,Is.Zero);
            Assert.That(Get(unit,"frogSlowRatio"),Is.EqualTo(.5f));
            Assert.That(Get(unit,"rank3ActionSlowTimer"),Is.EqualTo(0f));
            if(slam)Assert.That(Status(unit).Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(1f));
            else Assert.That((float)Get(unit,"skillTimer"),Is.InRange(8.47f,8.5f));
        }
        Assert.That(Vector3.Distance(new Vector3(4,0,-2),Position(left)),Is.EqualTo(1.275f).Within(.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TranscendentTheftTakesAllTimedStatsAndWholeShieldsIncludingBosses(bool boss)
    {
        Set(source,"augmentTranscended",true);Set(source,"maxHp",100f);
        var definition=Object.Instantiate((MonsterDefinition)Get(target,"definition"));
        try
        {
            definition.isBoss=boss;Set(target,"definition",definition);
            Set(target,"itemShield",80f);Set(target,"itemShieldTimer",5f);
            Set(target,"itemAttackSpeedBonus",.3f);Set(target,"itemAttackSpeedTimer",5f);
            Set(target,"rank3AttackBonusRatio",.4f);Set(target,"rank3AttackBonusTimer",5f);
            Set(target,"itemTemporaryDefenseBonus",20f);Set(target,"itemTemporaryResistanceBonus",10f);Set(target,"itemTemporaryStatTimer",5f);
            Set(target,"familyMoveSpeedBonus",.25f);Set(target,"familyMoveSpeedTimer",5f);
            Call("ApplyFrogBuffTheft",source,target);
            Assert.That((float)Call("TotalVisibleShield",source),Is.EqualTo(80f));
            Assert.That((float)Call("TotalVisibleShield",target),Is.Zero);
            Assert.That((float)Call("CurrentAttackInterval",source),Is.EqualTo(1f/1.3f).Within(.001f));
            Assert.That((float)Call("CurrentAttackInterval",target),Is.EqualTo(1f));
            Assert.That((float)Call("CurrentDefense",source),Is.EqualTo(20f));
            Assert.That((float)Call("CurrentDefense",target),Is.Zero);
            Assert.That((float)Call("CurrentFrogStolenBuff",source,FrogStolenBuffKind.AttackDamage),Is.EqualTo(.4f));
            Assert.That((float)Call("CurrentFrogStolenBuff",source,FrogStolenBuffKind.MoveSpeed),Is.EqualTo(.25f));
            Assert.That((float)Call("CurrentStatusResistance",source),Is.EqualTo(10f));
            Assert.That(Get(target,"rank3AttackBonusRatio"),Is.EqualTo(0f));
            Assert.That(Get(target,"familyMoveSpeedBonus"),Is.EqualTo(0f));
            object extra=Unit(1,new Vector3(4,0,2));Set(extra,"itemShield",50f);Set(extra,"itemShieldTimer",5f);
            Call("ApplyFrogBuffTheft",source,extra);
            Assert.That((float)Call("TotalVisibleShield",source),Is.EqualTo(130f));
            foreach(FrogStolenBuff buff in (IList)Get(source,"frogStolenBuffs"))buff.remaining=0f;
            Assert.That((float)Call("CurrentDefense",source),Is.Zero);
        }
        finally{Object.DestroyImmediate(definition);}
    }

    [Test]
    public void BufflessFallbackAppliesToEveryEnemyButDoesNotStackCasterHaste()
    {
        Set(source,"augmentTranscended",true);
        foreach(var unit in new[]{target,Unit(1,new Vector3(4,0,2)),Unit(1,new Vector3(4,0,-2))})
        {
            Call("ApplyFrogBuffTheft",source,unit);
            Assert.That(Get(unit,"frogStolenBuffMagnitude"),Is.EqualTo(-.15f));
        }
        Assert.That((float)Call("CurrentFrogStolenBuff",source,FrogStolenBuffKind.AttackSpeed),Is.EqualTo(.15f));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void InterruptedMultislamReleasesItsOwnTargetsAndPreservesNewExternalDisplacement(bool primaryInterrupted)
    {
        Set(source,"augmentTranscended",true);
        ((List<string>)Get(source,"activeTraitIds")).Add(FrogCombatRules.TongueSlamTraitId);
        object extra=Unit(1,new Vector3(4,0,2));
        var cast=Cast();cast.MoveNext();cast.MoveNext();cast.MoveNext();Step(.3f);
        object interrupted=primaryInterrupted?target:extra;
        Call("DisplaceUnit",interrupted,Vector3.forward,1f,true,false,false,false);
        if(primaryInterrupted)
        {
            Assert.That(cast.MoveNext(),Is.False);
            Assert.That(Position(extra).y,Is.Zero);Assert.That(Get(target,"displacing"),Is.True);
        }
        else
        {
            Assert.That(cast.MoveNext(),Is.True);Step(.41f);Assert.That(cast.MoveNext(),Is.False);
            Assert.That(Get(extra,"hp"),Is.EqualTo(4935f));
            Assert.That(Status(extra).Has(CombatStatusKind.Knockdown),Is.False);
            Assert.That(Get(source,"skillHits"),Is.EqualTo(3));
        }
    }
}
