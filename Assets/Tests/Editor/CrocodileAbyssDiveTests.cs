using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CrocodileAbyssDiveTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private object source,primary,near,ally;
    private HashSet<GameObject> existing;
    private float previousSpeed;
    private bool previousEffectTest;
    private UnityEngine.Random.State random;

    [SetUp]
    public void SetUp()
    {
        previousSpeed=Time.timeScale;Time.timeScale=1f;
        previousEffectTest=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=false;
        random=UnityEngine.Random.state;UnityEngine.Random.InitState(7341);
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Crocodile abyss gameplay test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=battleType.GetNestedType("Unit",Hidden);
        BattleSet("expeditionRun",new ExpeditionRunSave());BattleSet("playerProfile",new PlayerProfileSave());
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",true);
        BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(40f,10f,40f)));
        source=CreateUnit(0,Vector3.zero);primary=CreateUnit(1,Vector3.right);
        near=CreateUnit(1,new Vector3(1f,0f,.2f));ally=CreateUnit(0,new Vector3(0f,0f,.2f));
        Set(source,"augmentTranscended",true);Set(source,"target",primary);Set(source,"skillTimer",0f);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if(battle!=null)Call("EndCrocodileAbyssDive",source,true);
            foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
                if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&created.transform.parent==null)
                    Object.DestroyImmediate(created);
        }
        finally
        {
            Time.timeScale=previousSpeed;CombatEffectTestConfiguration.Active=previousEffectTest;
            UnityEngine.Random.state=random;
        }
    }

    [TestCase("ager",0)] [TestCase("ager",1)]
    [TestCase("kimbul",0)] [TestCase("kimbul",1)]
    [TestCase("krogal",0)] [TestCase("krogal",1)]
    [TestCase("tarvil",0)] [TestCase("tarvil",1)]
    public void EachTranscendedCrocodileDispatchesOnePrimaryStrikeWithAnEightSecondCooldown(string species,int team)
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));SetTeam(team);
        Assert.That(CrocodileAbyssDiveRules.HitTime,Is.EqualTo(.95f));
        Assert.That(CrocodileAbyssDiveRules.Duration,Is.EqualTo(1.2f));
        Assert.That(GameMasterData.Skill(CrocodileCombatRules.SkillId).baseCooldown,Is.EqualTo(8f));
        Assert.That(Call("BeginDeathRoll",source,Skill),Is.True);
        Assert.That(State,Is.Not.Null);Assert.That(StateField("target"),Is.SameAs(primary));
        Assert.That(Get(source,"crocodileRollTarget"),Is.Null);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillTimer"),Is.EqualTo(8f));
        Assert.That(Call("BeginCrocodileAbyssDive",source,Skill),Is.False,"A pending cast cannot spend its cooldown twice.");
        AdvanceTo(.949f);
        Assert.That(Hp(primary),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.Zero);
        Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.SameAs(source));
        Assert.That(Call("UpdateCrocodileAbyssVictim",primary),Is.True);
        Assert.That(Get(near,"crocodileAbyssHeldBy"),Is.Null);
        Advance(.002f);
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That(Hp(near),Is.EqualTo(5000f));Assert.That(Hp(ally),Is.EqualTo(5000f));
        AdvanceTo(1.21f);AssertReleased();Advance(10f);
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
    }

    [Test]
    public void OneHugeDeltaStillDealsOnlyOneStrikeAndCompletesItsCaptureAndRecovery()
    {
        Begin();Advance(10f);
        AssertReleased();
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(600f).Within(.01f));
        Assert.That(Hp(near),Is.EqualTo(5000f));Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(0f));
        Advance(10f);
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
    }

    [Test]
    public void CinematicResumeStopsBeforeTheHitWithoutReplacingTheCastOrLeakingItsBypassFlag()
    {
        Begin();Advance(CrocodileAbyssDiveRules.ContactTime+.01f);object cast=State;
        Assert.That(BattleGet("crocAbyssAdvancingCast"),Is.False);
        Call("ResumeCrocodileAbyssDiveFromCinematic",source);
        Assert.That(State,Is.SameAs(cast));
        Assert.That((float)StateField("time"),Is.EqualTo(.949f).Within(.00001f));
        Assert.That(StateField("hit"),Is.False);Assert.That(Hp(primary),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.Zero);Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(BattleGet("crocAbyssAdvancingCast"),Is.False);
        Advance(.002f);
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Advance(10f);AssertReleased();
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void AnotherCommittedCrocodileCannotReleaseTheFirstCastersCaptureWhenItCancelsOrHits(bool hit)
    {
        Begin();object firstCast=State;
        object second=CreateUnit(0,new Vector3(0f,0f,-.5f));
        Set(second,"augmentTranscended",true);Set(second,"target",primary);Set(second,"skillTimer",0f);
        Assert.That(Call("BeginCrocodileAbyssDive",second,Skill),Is.True,
            "Both casts must be accepted before the first crocodile makes the target untargetable.");
        try
        {
            Advance(.2f);
            CombatStatusEffect ownedRoot=Statuses(primary).Get(CombatStatusKind.Root);
            Assert.That(ownedRoot,Is.Not.Null);Assert.That(ownedRoot.source,Is.SameAs(source));
            Call("AdvanceCrocodileAbyssDive",second,.2f);
            object secondCast=Get(second,"crocodileAbyssDive");
            Assert.That(secondCast.GetType().GetField("captured",Hidden).GetValue(secondCast),Is.False);
            if(hit)Call("AdvanceCrocodileAbyssDive",second,CrocodileAbyssDiveRules.HitTime-.2f+.001f);
            else Call("EndCrocodileAbyssDive",second,true);
            Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.SameAs(source));
            Assert.That(Statuses(primary).Get(CombatStatusKind.Root),Is.SameAs(ownedRoot));
            Assert.That(State,Is.SameAs(firstCast));Assert.That((float)StateField("time"),Is.EqualTo(.2f).Within(.00001f));
            Assert.That(Hp(primary),Is.EqualTo(hit?4400f:5000f).Within(.01f));
            Assert.That(Get(second,"skillHits"),Is.EqualTo(hit?1:0));Assert.That(Get(source,"skillHits"),Is.Zero);
            Call("EndCrocodileAbyssDive",second,true);
            Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.SameAs(source));
            Assert.That(Statuses(primary).Get(CombatStatusKind.Root),Is.SameAs(ownedRoot));
        }
        finally{Call("EndCrocodileAbyssDive",second,true);}
    }

    [TestCase(0f,0f)] [TestCase(0f,150f)] [TestCase(.5f,150f)] [TestCase(.5f,1000f)]
    public void FullArmorPenetrationStillHonorsPhysicalDamageReductionAndOrdinaryShields(float reduction,float shield)
    {
        Set(primary,"defense",999f);Set(primary,"familyDamageReductionTimer",10f);
        Set(primary,"familyDamageReductionRatio",reduction);
        if(shield>0f)Statuses(primary).ApplyShield(10f,shield,primary);
        Begin();AdvanceTo(CrocodileAbyssDiveRules.Duration+.01f);
        float physical=600f*(1f-reduction),healthDamage=Mathf.Max(0f,physical-shield);
        Assert.That(Hp(primary),Is.EqualTo(5000f-healthDamage).Within(.01f));
        float remainingShield=Statuses(primary).Get(CombatStatusKind.Shield)?.magnitude??0f;
        Assert.That(remainingShield,Is.EqualTo(Mathf.Max(0f,shield-physical)).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(healthDamage).Within(.01f));AssertReleased();
    }

    [TestCase(CrocodileCombatRules.PreyCannonTraitId)]
    [TestCase(CrocodileCombatRules.CrushingRollTraitId)]
    [TestCase(CrocodileCombatRules.AbsoluteJawTraitId)]
    public void RankThreeAndMissingHealthCannotAddHitsAndTheCastAppliesItsOriginalDamageMultiplierOnce(string trait)
    {
        Traits(source).Add(trait);Set(source,"skillTraitDamageMultiplier",1.5f);
        Set(primary,"maxHp",50000f);Set(primary,"hp",3000f);Set(primary,"defense",999f);
        Assert.That(Call("BeginDeathRoll",source,Skill),Is.True);
        Set(source,"skillTraitDamageMultiplier",3f);
        AdvanceTo(CrocodileAbyssDiveRules.Duration+.01f);
        Assert.That(Hp(primary),Is.EqualTo(2100f).Within(.01f),
            "Use the snapshotted 1.5 multiplier once, without the old missing-health finisher.");
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));Assert.That(Get(source,"skillDamage"),Is.EqualTo(900f).Within(.01f));
        Assert.That(Get(source,"crocodileRollTarget"),Is.Null);Assert.That(Get(primary,"crocodilePreyFlying"),Is.False);
        Assert.That(Hp(near),Is.EqualTo(5000f));Assert.That(Hp(ally),Is.EqualTo(5000f));AssertReleased();
    }

    [TestCase(.5f,175f)] [TestCase(1f,400f)]
    public void AnEchoUsesSixHundredPercentBeforeItsScaleReductionAndShieldWithoutSpendingAnotherCast(float scale,float healthDamage)
    {
        Set(primary,"defense",999f);Set(primary,"familyDamageReductionTimer",10f);
        Set(primary,"familyDamageReductionRatio",.25f);Statuses(primary).ApplyShield(10f,50f,primary);
        Call("ResolveCrocodileAbyssDiveEcho",source,primary,scale);
        Assert.That(Hp(primary),Is.EqualTo(5000f-healthDamage).Within(.01f));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(healthDamage).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));Assert.That(Get(source,"skillUses"),Is.Zero);
        Assert.That(Get(source,"skillTimer"),Is.Zero);Assert.That(State,Is.Null);
        Assert.That(Hp(near),Is.EqualTo(5000f));Assert.That(Hp(ally),Is.EqualTo(5000f));
    }

    [Test]
    public void KimbulHealsOnlyFromTheHealthDamageLeftAfterReductionAndShield()
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_kimbul"));
        Traits(source).Add(CrocodileCombatRules.KimbulTraitId);Set(source,"hp",4000f);
        Set(primary,"defense",999f);Set(primary,"familyDamageReductionTimer",10f);
        Set(primary,"familyDamageReductionRatio",.5f);Statuses(primary).ApplyShield(10f,75f,primary);
        Begin();AdvanceTo(CrocodileAbyssDiveRules.Duration+.01f);
        Assert.That(Hp(primary),Is.EqualTo(4775f).Within(.01f));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(225f).Within(.01f));
        Assert.That(Hp(source),Is.EqualTo(4078.75f).Within(.01f));AssertReleased();
    }

    [TestCase("ager",CrocodileCombatRules.AgerTraitId)]
    [TestCase("kimbul",CrocodileCombatRules.KimbulTraitId)]
    [TestCase("krogal",CrocodileCombatRules.KrogalTraitId)]
    [TestCase("tarvil",CrocodileCombatRules.TarvilTraitId)]
    public void SpeciesIntrinsicsRemainAttachedToTheIndependentAbyssStrike(string species,string intrinsic)
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Traits(source).Add(intrinsic);Set(source,"hp",4000f);
        if(species=="tarvil")Set(primary,"hp",250f);
        Begin();
        if(species=="ager")Assert.That((float)Get(source,"crocodileToughHideTimer"),Is.GreaterThanOrEqualTo(1.2f));
        if(species=="krogal")
        {
            Assert.That(Get(source,"crocodileShield"),Is.EqualTo(600f).Within(.01f));
            Assert.That(Get(source,"crocodileShieldTimer"),Is.EqualTo(5f));
        }
        if(species=="tarvil")
        {
            Assert.That(Get(primary,"crocodileDeathMarkSource"),Is.SameAs(source));
            Assert.That(Get(primary,"crocodileDeathMarkTimer"),Is.EqualTo(5f));
        }
        AdvanceTo(CrocodileAbyssDiveRules.Duration+.01f);
        if(species=="kimbul")Assert.That(Hp(source),Is.EqualTo(4210f).Within(.01f));
        else Assert.That(Hp(source),Is.EqualTo(4000f));
        if(species=="tarvil")
        {
            Assert.That(Get(primary,"alive"),Is.False);
            Assert.That(Get(source,"skillTimer"),Is.EqualTo(4f),"A marked kill retains Tarvil's cooldown recovery.");
        }
        else Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));
        if(species=="krogal")Assert.That(Get(source,"crocodileShield"),Is.EqualTo(600f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));AssertReleased();
    }

    [Test]
    public void OrdinaryCrocodilesCannotEnterAbyssAndStillDispatchTheirOriginalDeathRoll()
    {
        Set(source,"augmentTranscended",false);
        Assert.That(Call("BeginCrocodileAbyssDive",source,Skill),Is.False);
        Assert.That(State,Is.Null);Assert.That(Get(source,"skillUses"),Is.Zero);
        Assert.That(Call("BeginDeathRoll",source,Skill),Is.True);
        Assert.That(State,Is.Null);Assert.That(Get(source,"crocodileRollTarget"),Is.SameAs(primary));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Hp(primary),Is.EqualTo(5000f));
    }

    [TestCase(false)] [TestCase(true)]
    public void ReleasingTheOwnedCaptureRestoresAnEarlierRootUnlessTheCaptureWasCleansed(bool cleansed)
    {
        CombatStatusEffect previous=Statuses(primary).ApplyTimed(CombatStatusKind.Root,8f,ally);
        Begin();AdvanceTo(CrocodileAbyssDiveRules.ContactTime+.05f);
        Assert.That(Statuses(primary).Get(CombatStatusKind.Root),Is.Not.SameAs(previous));
        Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.SameAs(source));
        if(cleansed)Statuses(primary).Remove(CombatStatusKind.Root);
        Call("EndCrocodileAbyssDive",source,true);AssertReleased(false);
        CombatStatusEffect restored=Statuses(primary).Get(CombatStatusKind.Root);
        if(cleansed)Assert.That(restored,Is.Null,"A cleanse cannot resurrect the root saved before the capture.");
        else
        {
            Assert.That(restored,Is.Not.Null);Assert.That(restored.source,Is.SameAs(ally));
            Assert.That(restored.remaining,Is.InRange(7f,8f));
        }
        Advance(3f);Assert.That(Hp(primary),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.Zero);
    }

    [TestCase("cleanse")] [TestCase("replace")] [TestCase("revision")]
    public void BreakingTheCaptureDoesNotRetargetOrCancelTheCommittedPrimaryStrike(string release)
    {
        Begin();AdvanceTo(CrocodileAbyssDiveRules.ContactTime+.02f);
        CombatStatusEffect replacement=null;
        if(release=="revision")Call("InterruptPositionalAction",primary);
        else
        {
            Statuses(primary).Remove(CombatStatusKind.Root);
            if(release=="replace")replacement=Statuses(primary).ApplyTimed(CombatStatusKind.Root,8f,ally);
        }
        Assert.That(Call("UpdateCrocodileAbyssVictim",primary),Is.False);
        Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.Null);Assert.That(State,Is.Not.Null);
        Model(primary).transform.position=new Vector3(9f,0f,8f);Set(source,"target",near);
        AdvanceTo(CrocodileAbyssDiveRules.Duration+.01f);
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Hp(near),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));AssertReleased(false);
        if(replacement!=null)Assert.That(Statuses(primary).Get(CombatStatusKind.Root),Is.SameAs(replacement));
    }

    [TestCase("control")] [TestCase("debuff")] [TestCase("force")]
    public void ImmuneTargetsKeepTheirPositionAndActionsWhileStillTakingTheArmorIgnoringStrike(string immunity)
    {
        if(immunity=="control")Statuses(primary).ControlImmune=true;
        else if(immunity=="debuff")Statuses(primary).DebuffImmune=true;
        else Set(primary,"rank3ForceImmuneTimer",10f);
        Vector3 position=Model(primary).transform.position;Begin();AdvanceTo(CrocodileAbyssDiveRules.ContactTime+.02f);
        Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.Null);
        Assert.That(Statuses(primary).Has(CombatStatusKind.Root),Is.False);
        Assert.That(Call("UpdateCrocodileAbyssVictim",primary),Is.False);
        AdvanceTo(CrocodileAbyssDiveRules.Duration+.01f);
        Assert.That(Model(primary).transform.position,Is.EqualTo(position));
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));AssertReleased();
    }

    [TestCase("pause")] [TestCase("freeze")]
    public void PausingOrFreezingTheCasterPreservesTheSameUnspentHitAndCaptureClock(string pause)
    {
        Begin();AdvanceTo(CrocodileAbyssDiveRules.ContactTime+.02f);
        object state=State;float age=(float)StateField("time");
        if(pause=="pause")BattleSet("paused",true);else Statuses(source).ApplyTimed(CombatStatusKind.Freeze,2f);
        Advance(2f);
        Assert.That(State,Is.SameAs(state));Assert.That(StateField("time"),Is.EqualTo(age));
        Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.SameAs(source));
        Assert.That(Hp(primary),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.Zero);
        if(pause=="pause")BattleSet("paused",false);else Statuses(source).Remove(CombatStatusKind.Freeze);
        AdvanceTo(CrocodileAbyssDiveRules.Duration+.01f);
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));AssertReleased();
    }

    [TestCase("cancel")] [TestCase("death")] [TestCase("target_death")]
    [TestCase("fear")] [TestCase("knockdown")] [TestCase("airborne")] [TestCase("stun")]
    [TestCase("revision")] [TestCase("generation")] [TestCase("ending")]
    public void InterruptionsReleaseTheExactCaptureAndCannotDealADelayedHit(string interruption)
    {
        Begin();AdvanceTo(CrocodileAbyssDiveRules.ContactTime+.02f);
        if(interruption=="cancel")Call("EndCrocodileAbyssDive",source,true);
        else if(interruption=="death")Set(source,"alive",false);
        else if(interruption=="target_death")Set(primary,"alive",false);
        else if(interruption=="fear")Statuses(source).ApplyTimed(CombatStatusKind.Fear,2f,primary);
        else if(interruption=="knockdown")Statuses(source).ApplyTimed(CombatStatusKind.Knockdown,2f,primary);
        else if(interruption=="airborne")Statuses(source).ApplyTimed(CombatStatusKind.Airborne,2f,primary);
        else if(interruption=="stun")Set(source,"giraffeStunTimer",2f);
        else if(interruption=="revision")Call("InterruptPositionalAction",source);
        else if(interruption=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else BattleSet("battleEnding",true);
        Advance(3f);AssertReleased();Advance(3f);
        Assert.That(Hp(primary),Is.EqualTo(5000f));Assert.That(Hp(near),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.Zero);Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(0f));
    }

    private object CreateUnit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);var units=(IList)BattleGet("units");
        var model=new GameObject("Abyss gameplay unit "+units.Count);model.transform.position=position;
        var ring=new GameObject("Abyss shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",units.Count);Set(unit,"model",model);Set(unit,"ring",ring);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_ager"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",CrocodileCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);
        Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);Set(unit,"collisionRadius",.3f);
        Set(unit,"skillCooldownDuration",8f);Set(unit,"skillTimer",100f);Set(unit,"lastGroundPosition",position);
        Statuses(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);
        units.Add(unit);return unit;
    }

    private void SetTeam(int team)
    {
        foreach(object unit in new[]{source,ally})Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        foreach(object unit in new[]{primary,near})Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
    }
    private void Begin()=>Assert.That(Call("BeginCrocodileAbyssDive",source,Skill),Is.True);
    private void Advance(float dt)=>Call("AdvanceCrocodileAbyssDive",source,dt);
    private void AdvanceTo(float time)
    {
        for(int i=0;State!=null&&(float)StateField("time")<time-.000001f;i++)
        {
            Assert.That(i,Is.LessThan(200),"A manual cast must finish or advance its explicit clock.");
            Advance(Mathf.Min(.025f,time-(float)StateField("time")));
        }
    }
    private void AssertReleased(bool root=true)
    {
        Assert.That(State,Is.Null);Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.Null);
        if(root)Assert.That(Statuses(primary).Has(CombatStatusKind.Root),Is.False);
    }
    private SkillMasterRow Skill=>GameMasterData.Skill(CrocodileCombatRules.SkillId);
    private object State=>Get(source,"crocodileAbyssDive");
    private object StateField(string name)=>State.GetType().GetField(name,Hidden).GetValue(State);
    private List<string> Traits(object unit)=>(List<string>)Get(unit,"activeTraitIds");
    private CombatStatusState Statuses(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private void Set(object unit,string name,object value)=>unitType.GetField(name,Hidden).SetValue(unit,value);
    private object Get(object unit,string name)=>unitType.GetField(name,Hidden).GetValue(unit);
    private object Call(string name,params object[] args)=>battleType.GetMethod(name,Hidden).Invoke(battle,args);
    private void BattleSet(string name,object value)=>battleType.GetField(name,Hidden).SetValue(battle,value);
    private object BattleGet(string name)=>battleType.GetField(name,Hidden).GetValue(battle);
}
