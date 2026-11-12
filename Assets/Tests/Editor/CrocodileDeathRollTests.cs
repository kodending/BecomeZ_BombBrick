using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CrocodileDeathRollTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private object source,target;
    private HashSet<GameObject> existing;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Death Roll Test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=battleType.GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        battleType.GetField("combatBounds",Private).SetValue(battle,
            new Bounds(Vector3.zero,new Vector3(40f,2f,40f)));
        source=Unit(0);target=Unit(1);
        Set(source,"target",target);Set(source,"crocodileRollTarget",target);
    }

    [TearDown]
    public void TearDown()
    {
        foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
            if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
               created.transform.parent==null)Object.DestroyImmediate(created);
    }

    private object Unit(int team)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var model=new GameObject("Roll Unit");model.transform.position=Vector3.right*team;
        Set(unit,"id",team);Set(unit,"model",model);Set(unit,"ring",new GameObject("Roll Ring"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_ager"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",CrocodileCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);
        Set(unit,"collisionRadius",0.3f);
        ((IList)battleType.GetField("units",Private).GetValue(battle)).Add(unit);
        return unit;
    }
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private object Invoke(string method,params object[] args)=>
        battleType.GetMethod(method,Private).Invoke(battle,args);
    private void Position(object unit,Vector3 position)=>
        ((GameObject)Get(unit,"model")).transform.position=position;
    private IEnumerator Cast(float duration)
    {
        var skill=JsonUtility.FromJson<SkillMasterRow>(JsonUtility.ToJson(GameMasterData.Skill(CrocodileCombatRules.SkillId)));
        skill.durationSeconds=duration;
        return (IEnumerator)battleType.GetMethod("ResolveDeathRoll",Private).Invoke(battle,new[]{source,skill,battleType.GetField("combatGeneration",Private).GetValue(battle)});
    }

    [Test]
    public void ThreeHitsUseDistinctFinalRatioAndMissingHealthAfterEarlierHits()
    {
        var cast=Cast(0f);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(5000f-100f-80f-180f-180f*.08f).Within(.01f));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DisplacingEitherParticipantCancelsRemainingHits(bool displaceSource)
    {
        var cast=Cast(1f);Assert.That(cast.MoveNext(),Is.True);
        object moved=displaceSource?source:target;
        battleType.GetMethod("InterruptPositionalAction",Private).Invoke(null,new[]{moved});
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(4900f).Within(.01f));
        Assert.That((float)Get(source,"crocodileBiteLockTimer"),Is.Zero);
    }

    private void SelectAbsoluteJaw()
    {
        ((IList)Get(source,"activeTraitIds")).Add(CrocodileCombatRules.AbsoluteJawTraitId);
    }

    private void FinishCast(IEnumerator cast)
    {
        for(int i=0;i<10000;i++)if(!cast.MoveNext())return;
        Assert.Fail("Death Roll did not finish within 10000 steps; delta time="+Time.deltaTime);
    }

    [TestCase(false,320f)]
    [TestCase(true,400f)]
    public void AbsoluteJawIgnoresArmorOnlyOnFinalHitAndUsesCurrentMissingHealth(bool transcended,float finalDamage)
    {
        Set(source,"augmentTranscended",transcended);
        Set(source,"hp",4000f);
        SelectAbsoluteJaw();Set(target,"defense",100f);
        var cast=Cast(1f);Assert.That(cast.MoveNext(),Is.True);
        Assert.That((float)Get(target,"hp"),Is.EqualTo(4950f).Within(.01f));
        Assert.That((float)Get(source,"crocodileBiteLockTimer"),Is.EqualTo(
            CrocodileCombatRules.AbsoluteJawDuration/BombBrick.Combat.CombatStatFormula.CombatAnimationRate));
        FinishCast(cast);
        Assert.That((float)Get(target,"hp"),Is.EqualTo(4910f-(finalDamage+90f*.08f)/1.5f).Within(.01f));
        Assert.That((float)Get(source,"hp"),Is.EqualTo(4000f),"Rank 4 does not add healing.");
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));
        Assert.That((float)Get(source,"crocodileBiteLockTimer"),Is.Zero);
        Assert.That((float)Get(target,"crocodileImmobilizedTimer"),Is.Zero);
    }

    [Test]
    public void AbsoluteJawDoesNotDoubleShieldDamageOrExecuteLowHealthTargets()
    {
        SelectAbsoluteJaw();Set(target,"crocodileShield",1000f);
        FinishCast(Cast(1f));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(5000f));
        Assert.That((float)Get(target,"crocodileShield"),Is.EqualTo(500f).Within(.01f));
        Set(target,"crocodileShield",0f);Set(target,"hp",1200f);
        Set(source,"crocodileRollTarget",target);
        FinishCast(Cast(1f));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(381.6f).Within(.02f));
        Assert.That(Get(target,"alive"),Is.True);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(6));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AbsoluteJawInterruptionBeforeCompressionCancelsTheFinisher(bool sourceDies)
    {
        SelectAbsoluteJaw();var cast=Cast(1f);Assert.That(cast.MoveNext(),Is.True);
        if(sourceDies)Set(source,"alive",false);
        else battleType.GetMethod("InterruptPositionalAction",Private).Invoke(null,new[]{target});
        FinishCast(cast);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(4900f));
        Assert.That((float)Get(source,"crocodileBiteLockTimer"),Is.Zero);
    }

    [TestCase(false,4840f)]
    [TestCase(true,4780f)]
    public void CrushingSweepsDamageOnlyOtherEnemiesWithinTwoTilesWithoutControlOrMovement(bool transcended,float remainingHp)
    {
        Set(source,"augmentTranscended",transcended);
        object near=Unit(1),edge=Unit(1),outside=Unit(1),friendly=Unit(0),dead=Unit(1);
        Position(near,new Vector3(1,0,2.5f));Position(edge,new Vector3(4.7f,0,0));
        Position(outside,new Vector3(4.71f,0,0));Position(dead,new Vector3(1,0,.2f));
        Set(dead,"alive",false);
        Invoke("ApplyCrocodileCrushingRollSweep",source,target,1f,false);
        Assert.That((float)Get(near,"hp"),Is.EqualTo(4920f));
        Invoke("ApplyCrocodileCrushingRollSweep",source,target,1f,true);
        Assert.That((float)Get(near,"hp"),Is.EqualTo(remainingHp));
        Assert.That((float)Get(edge,"hp"),Is.EqualTo(remainingHp));
        foreach(object untouched in new[]{source,target,outside,friendly,dead})
            Assert.That((float)Get(untouched,"hp"),Is.EqualTo(5000f));
        Assert.That(((GameObject)Get(near,"model")).transform.position,Is.EqualTo(new Vector3(1,0,2.5f)));
        Assert.That(Get(near,"displacing"),Is.False);
        Assert.That(Get(near,"crocodileImmobilizedTimer"),Is.EqualTo(0f));
        Assert.That(((CombatStatusState)Get(near,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.False);
    }

    [Test]
    public void CrushingSweepRechecksRangeAndCancelsWhenTheCasterDies()
    {
        object near=Unit(1);Position(near,new Vector3(1,0,1));
        Invoke("ApplyCrocodileCrushingRollSweep",source,target,.8f,false);
        Assert.That((float)Get(near,"hp"),Is.EqualTo(4936f));
        Position(near,new Vector3(5,0,1));
        Invoke("ApplyCrocodileCrushingRollSweep",source,target,.8f,false);
        Assert.That((float)Get(near,"hp"),Is.EqualTo(4936f));
        Position(near,new Vector3(1,0,1));Set(source,"alive",false);
        Invoke("ApplyCrocodileCrushingRollSweep",source,target,.8f,true);
        Assert.That((float)Get(near,"hp"),Is.EqualTo(4936f));
    }

    [Test]
    public void TranscendentCannonOnlyWidensSplashAndDoesNotAddTheOldDelayedSkill()
    {
        Set(source,"augmentTranscended",true);
        object extended=Unit(1),edge=Unit(1),outside=Unit(1);
        Position(extended,new Vector3(1f,0f,2.3f));
        Position(edge,new Vector3(1f,0f,2.85f));
        Position(outside,new Vector3(1f,0f,2.86f));
        Invoke("ApplyCrocodilePreyCannonSplash",source,target,Vector3.right,1f);
        Assert.That((float)Get(extended,"hp"),Is.EqualTo(4880f));
        Assert.That((float)Get(edge,"hp"),Is.EqualTo(4880f));
        Assert.That((float)Get(outside,"hp"),Is.EqualTo(5000f));
        Assert.That((float)Get(target,"hp"),Is.EqualTo(5000f));
        Assert.That(((CombatStatusState)Get(extended,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.True);
        Invoke("TriggerFamilyTranscendenceSkill",source);
        Assert.That(Get(source,"familyDamageReductionRatio"),Is.EqualTo(0f));
        Assert.That(Get(source,"familyDamageReductionTimer"),Is.EqualTo(0f));
        Assert.That(((IDictionary)battleType.GetField("familyTranscendenceStates",Private).GetValue(battle)).Count,Is.Zero);
    }

    [TestCase("ager",1.5f)]
    [TestCase("kimbul",1.45f)]
    [TestCase("krogal",1.6f)]
    [TestCase("tarvil",1.4f)]
    public void FamilyUsesSlowBasicAttacksAndEightSecondSkill(string id,float interval)
    {
        Assert.That(Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id).attackInterval,Is.EqualTo(interval));
        Assert.That(GameMasterData.Skill(CrocodileCombatRules.SkillId).baseCooldown,Is.EqualTo(8f));
    }

    [Test]
    public void RollPoseKeepsBitePivotFixedAndRestoresWithoutAccumulatingOffset()
    {
        var camera=new GameObject("Roll Camera").AddComponent<Camera>();
        var visual=new GameObject("Roll Sprite");visual.AddComponent<SpriteRenderer>();
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();
        typeof(SpriteMonsterBillboard).GetField("cachedCamera",Private).SetValue(billboard,camera);
        var late=typeof(SpriteMonsterBillboard).GetMethod("LateUpdate",Private);
        Vector3 pivot=new Vector3(.4f,.5f,0f),offset=Vector3.right*.3f;
        Vector3 anchor=billboard.UnposedPoint(pivot)+offset;
        for(int i=0;i<40;i++)
        {
            billboard.SetRollPose(i*23f,pivot,offset);late.Invoke(billboard,null);
            Assert.That(Vector3.Distance(visual.transform.TransformPoint(pivot),anchor),Is.LessThan(.001f));
        }
        billboard.SetRollPose(0f,Vector3.zero,Vector3.zero);
        Assert.That(visual.transform.position.magnitude,Is.LessThan(.001f));
        Assert.That(Quaternion.Angle(visual.transform.rotation,camera.transform.rotation),Is.LessThan(.001f));
    }

    [Test]
    public void BiteFrameStaysLatchedUntilInterrupted()
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/013_ager"));
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        driver.ResetForReuse();driver.PlaySkillAttack(1f,null);driver.SetDeathRollFrame(true);
        var type=typeof(SpriteMonsterAnimationDriver);
        type.GetMethod("Advance",Private).Invoke(driver,new object[]{2f});
        type.GetMethod("LateUpdate",Private).Invoke(driver,null);
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(driver.SkillAttackHitFrameIndex));
        driver.InterruptCurrentAction();
        type.GetMethod("Advance",Private).Invoke(driver,new object[]{.2f});
        type.GetMethod("LateUpdate",Private).Invoke(driver,null);
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
        Assert.That((bool)type.GetField("deathRollFrame",Private).GetValue(driver),Is.False);
    }

    [TestCase(.01f)]
    [TestCase(.3f)]
    public void FlyingBodyStopsAtFirstEnemyEvenAcrossALargeFrame(float dt)
    {
        object farther=Unit(1),nearer=Unit(1);
        Position(farther,new Vector3(4f,0f,0f));Position(nearer,new Vector3(3f,0f,0f));
        Assert.That(Invoke("BeginCrocodilePreyFlight",source,target),Is.True);
        for(int i=0;i<40&&(bool)Get(target,"displacing");i++)
            Invoke("UpdateDisplacement",target,dt);
        Assert.That(((GameObject)Get(target,"model")).transform.position.x,Is.EqualTo(2.4f).Within(.001f));
        Assert.That(Get(target,"crocodilePreyFlying"),Is.False);
        Assert.That(Get(target,"displacing"),Is.False);
        Invoke("ApplyCrocodilePreyCannonSplash",source,target,new Vector3(2.4f,0f,0f),1f);
        Assert.That((float)Get(target,"hp"),Is.EqualTo(5000f),"Primary must not receive splash twice.");
        Assert.That((float)Get(nearer,"hp"),Is.EqualTo(4880f).Within(.001f));
        foreach(object victim in new[]{target,nearer})
            Assert.That(((CombatStatusState)Get(victim,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.True);
    }

    [Test]
    public void ThrowReleasesMeleeButKeepsStationaryRangedAndExcludesNewTargets()
    {
        Set(source,"attackRange",1);
        object ranged=Unit(0),alternate=Unit(1);
        Position(alternate,new Vector3(-2f,0f,0f));
        Set(ranged,"target",target);
        Assert.That(Invoke("BeginCrocodilePreyFlight",source,target),Is.True);
        Assert.That(Get(source,"target"),Is.SameAs(alternate));
        Assert.That(Get(ranged,"target"),Is.SameAs(target));
        Assert.That(Invoke("SelectTarget",ranged,false,null),Is.SameAs(alternate));
        Position(ranged,new Vector3(-20f,0f,0f));
        Assert.That(Invoke("CanKeepCombatTarget",ranged,target),Is.False);
        Invoke("SetTarget",ranged,target);
        Assert.That(Get(ranged,"target"),Is.SameAs(alternate));
    }

    [TestCase(2f)]
    [TestCase(6f)]
    public void ThrowAimsAtNearestOtherEnemyAndNeverTravelsBeyondTwoTiles(float distance)
    {
        object other=Unit(1),farther=Unit(1),dead=Unit(1),ally=Unit(0);
        Position(other,new Vector3(1f,0f,distance));
        Position(farther,new Vector3(8f,0f,0f));
        Position(dead,new Vector3(1f,0f,.5f));Set(dead,"alive",false);
        Position(ally,new Vector3(1f,0f,.25f));
        Assert.That(Invoke("BeginCrocodilePreyFlight",source,target),Is.True);
        Vector3 end=(Vector3)Get(target,"displacementEnd");
        Assert.That(end.x,Is.EqualTo(1f).Within(.001f));
        Assert.That(end.z,Is.EqualTo(2f*MonsterAutoBattlePrototype.TileSize).Within(.001f));
        Invoke("UpdateDisplacement",target,.3f);
        Vector3 landed=((GameObject)Get(target,"model")).transform.position;
        Assert.That(landed.z,Is.EqualTo(Mathf.Min(distance-.6f,3.4f)).Within(.001f));
        Assert.That(landed.y,Is.Zero.Within(.001f));
    }

    [Test]
    public void SoleEnemyLandsTwoTilesAwayAndCanBeAcquiredAgain()
    {
        Set(source,"attackRange",1);
        Assert.That(Invoke("BeginCrocodilePreyFlight",source,target),Is.True);
        Assert.That(Get(source,"target"),Is.Null);
        Invoke("UpdateDisplacement",target,.15f);
        Assert.That(((GameObject)Get(target,"model")).transform.position.y,Is.EqualTo(.7f).Within(.001f));
        var damage=battleType.GetMethod("ApplySkillDamage",Private,null,
            new[]{unitType,unitType,typeof(float),typeof(bool)},null);
        damage.Invoke(battle,new[]{source,target,(object)50f,false});
        Assert.That((float)Get(target,"hp"),Is.EqualTo(4950f));
        Assert.That(Get(target,"crocodilePreyFlying"),Is.True,"Flight must not grant damage immunity.");
        Invoke("UpdateDisplacement",target,.15f);
        Assert.That(((GameObject)Get(target,"model")).transform.position.x,
            Is.EqualTo(1f+2f*MonsterAutoBattlePrototype.TileSize).Within(.001f));
        Assert.That(Invoke("SelectTarget",source,false,null),Is.SameAs(target));
    }

    [Test]
    public void InterruptingFlightRestoresGroundAndTargetEligibility()
    {
        Invoke("BeginCrocodilePreyFlight",source,target);
        Invoke("UpdateDisplacement",target,.15f);
        Invoke("InterruptPositionalAction",target);
        Assert.That(Get(target,"crocodilePreyFlying"),Is.False);
        Assert.That(((GameObject)Get(target,"model")).transform.position.y,Is.Zero.Within(.001f));
        Assert.That(Invoke("SelectTarget",source,false,null),Is.SameAs(target));
    }

    [Test]
    public void DisplacementImmunityKeepsTargetInPlaceAndDoesNotReleaseAllies()
    {
        Set(target,"rank3ForceImmuneTimer",1f);
        Assert.That(Invoke("BeginCrocodilePreyFlight",source,target),Is.False);
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Get(target,"crocodilePreyFlying"),Is.False);
        Assert.That(((GameObject)Get(target,"model")).transform.position.x,Is.EqualTo(1f));
    }
}
