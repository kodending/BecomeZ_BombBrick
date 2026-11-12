using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private object ReaperCall(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype)
        .GetMethod(method,Private).Invoke(battle,args);
    private void ReaperReady(string intrinsic=null)
    {
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,new Vector3(40f,1f,40f)));
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_mantius"));
        Set(source,"skillId",MantisCombatRules.SkillId);Set(source,"skillCooldownDuration",8f);Set(source,"skillTimer",8f);
        Trait(MantisRank3Rules.ReaperEmergence);if(intrinsic!=null)Trait(intrinsic);
        ReaperCall("ActivateMantisReaper",source);ReaperActionTick(.6f);
    }
    private void ReaperTick(float dt)=>ReaperCall("TickMantisReaper",source,dt);
    private void ReaperActionTick(float seconds)=>ReaperTick(CombatActionSeconds(seconds));
    private void ReaperFly()=>ReaperCall("StartMantisReaperFlight",source,target,1f,1f,0);

    [Test]
    public void ReaperMoltsOnceHealsMissingHealthAndRestoresScaleAndCooldownOnCleanup()
    {
        Set(source,"hp",2500f);ReaperReady();
        Assert.That(Hp(source),Is.EqualTo(3000f));Assert.That(Get(source,"rank3MantisReaper"),Is.True);
        Assert.That(Model(source).transform.localScale,Is.EqualTo(Vector3.one*1.2f));
        Assert.That(Get(source,"skillCooldownDuration"),Is.EqualTo(6f));
        Assert.That(Get(source,"rank3ForceImmuneTimer"),Is.EqualTo(0f));
        ReaperCall("ActivateMantisReaper",source);Assert.That(Hp(source),Is.EqualTo(3000f));
        Assert.That(Get(source,"mantisReaperAction"),Is.Null);
        ReaperCall("CleanupRank3Unit",source);
        Assert.That(Model(source).transform.localScale,Is.EqualTo(Vector3.one));
        Assert.That(Get(source,"skillCooldownDuration"),Is.EqualTo(8f));
    }

    [Test]
    public void ReaperBasicStrikesAtTwoFramesAndOnlySecondCleaveHitsTheFront()
    {
        Model(target).transform.position=Vector3.right*2f;
        object front=Unit(1,new Vector3(1.7f,0f,.7f)),back=Unit(1,Vector3.left),ally=Unit(0,Vector3.right);
        ReaperReady();float timer=(float)Get(source,"skillTimer");
        ReaperCall("StartMantisReaperBasic",source,target,100f,false,1f);
        ReaperTick(.119f);Assert.That(Hp(target),Is.EqualTo(5000f));
        ReaperTick(.002f);Assert.That(Hp(target),Is.EqualTo(4922f));Assert.That(Hp(front),Is.EqualTo(5000f));
        float firstTimer=(float)Get(source,"skillTimer");Assert.That(firstTimer,Is.LessThan(timer));
        ReaperTick(.1f);Assert.That(Hp(target),Is.EqualTo(4844f));Assert.That(Hp(front),Is.EqualTo(4922f));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(firstTimer));
        ReaperTick(.3f);Assert.That(Hp(back),Is.EqualTo(5000f));Assert.That(Hp(ally),Is.EqualTo(5000f));
        Assert.That(Get(source,"mantisReaperAction"),Is.Null);
    }

    [Test]
    public void ReaperBasicRechecksTargetDistanceBeforeEachStrike()
    {
        Model(target).transform.position=Vector3.right*2f;ReaperReady();
        ReaperCall("StartMantisReaperBasic",source,target,100f,false,1f);ReaperTick(.12f);
        Model(target).transform.position=Vector3.right*12f;ReaperTick(.3f);
        Assert.That(Hp(target),Is.EqualTo(4922f));
    }

    [Test]
    public void ReaperBasicRetainsThePacedPointTwoMinimumThroughBothContacts()
    {
        Model(target).transform.position=Vector3.right*2f;ReaperReady();
        ReaperCall("StartMantisReaperBasic",source,target,100f,false,.2f);
        // This rate is already derived from the paced attack interval; do not clamp it back to .25.
        ReaperTick(.59f);Assert.That(Hp(target),Is.EqualTo(5000f));
        ReaperTick(.011f);Assert.That(Hp(target),Is.EqualTo(4922f));
        ReaperTick(.489f);Assert.That(Hp(target),Is.EqualTo(4922f));
        ReaperTick(.011f);Assert.That(Hp(target),Is.EqualTo(4844f));
    }

    [TestCase(null,312f,false)]
    [TestCase(MantisCombatRules.MantiusTraitId,374.4f,false)]
    [TestCase(MantisCombatRules.FlotisTraitId,312f,true)]
    public void ReaperFlightSweepsTwoContactCutsAndKeepsSpeciesEffects(string intrinsic,float damage,bool pollen)
    {
        object side=Unit(1,new Vector3(2f,0f,3f)),ally=Unit(0,Vector3.right*2f);
        ReaperReady(intrinsic);ReaperFly();ReaperActionTick(.119f);Assert.That(Hp(target),Is.EqualTo(5000f));
        ReaperActionTick(.201f);Assert.That(5000f-Hp(target),Is.EqualTo(156f).Within(.01f));
        ReaperActionTick(.5f);Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
        var statuses=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(statuses.Has(CombatStatusKind.Bleed),Is.True);
        Assert.That(statuses.Has(CombatStatusKind.HallucinatoryPollen),Is.EqualTo(pollen));
        Assert.That(Hp(side),Is.EqualTo(5000f));Assert.That(Hp(ally),Is.EqualTo(5000f));
        Assert.That(Model(source).transform.position.x,Is.GreaterThan(Model(target).transform.position.x));
        Assert.That(Model(source).transform.position.magnitude,Is.LessThanOrEqualTo(4f*MonsterAutoBattlePrototype.TileSize+.001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReaperSantiusExtendsBladesWhileBodyAlwaysStaysWithinFourTiles(bool sand)
    {
        Model(target).transform.position=Vector3.right*6f;
        object far=Unit(1,Vector3.right*8f),wide=Unit(1,new Vector3(2f,0f,1.45f));
        ReaperReady(sand?MantisCombatRules.SantiusTraitId:null);ReaperFly();ReaperActionTick(1f);
        Assert.That(Hp(far),Is.EqualTo(sand?4688f:5000f));
        Assert.That(Hp(wide),Is.EqualTo(sand?4688f:5000f));
        Assert.That(Model(source).transform.position.x,Is.LessThanOrEqualTo(6.801f));
    }

    [Test]
    public void ReaperFlightFreezeStopsMovementPoseAndHitsUntilThawed()
    {
        ReaperReady();ReaperFly();ReaperActionTick(.2f);Vector3 position=Model(source).transform.position;
        ((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(CombatStatusKind.Freeze,2f,source);
        ReaperTick(1f);Assert.That(Model(source).transform.position,Is.EqualTo(position));Assert.That(Hp(target),Is.EqualTo(5000f));
        ((CombatStatusState)Get(source,"combatStatuses")).Remove(CombatStatusKind.Freeze);
        ReaperActionTick(.5f);Assert.That(Hp(target),Is.EqualTo(4688f));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void ReaperFlightInterruptionCancelsRemainingDamage(int interruption)
    {
        ReaperReady();ReaperFly();ReaperActionTick(.16f);
        if(interruption==0)Set(source,"alive",false);
        else if(interruption==1)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else if(interruption==2)((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(CombatStatusKind.Knockdown,1f,source);
        else if(interruption==3)((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(CombatStatusKind.Root,1f,source);
        else Set(source,"griffinRootTimer",1f);
        ReaperTick(1f);Assert.That(Get(source,"mantisReaperAction"),Is.Null);Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(Model(source).transform.position.y,Is.EqualTo(0f));
    }

    [Test]
    public void ReaperForceMovementImmunityExistsOnlyDuringFlight()
    {
        ReaperReady();var method=typeof(MonsterAutoBattlePrototype).GetMethod("MantisReaperFlying",
            System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        Assert.That(method.Invoke(null,new[]{source}),Is.False);ReaperFly();Assert.That(method.Invoke(null,new[]{source}),Is.False);
        ReaperActionTick(.2f);Assert.That(method.Invoke(null,new[]{source}),Is.True);
        ReaperActionTick(.33f);Assert.That(method.Invoke(null,new[]{source}),Is.False);
    }

    [Test]
    public void ReaperWingsAndBladesAreRemovedWithTheOwner()
    {
        var renderer=Model(source).AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
        ReaperReady();Assert.That(Object.FindObjectsOfType<PixelMantisReaper>().Length,Is.EqualTo(1));
        ReaperFly();ReaperActionTick(.25f);ReaperCall("CleanupRank3Unit",source);
        Assert.That(Object.FindObjectsOfType<PixelMantisReaper>(),Is.Empty);
        Assert.That(Get(source,"mantisReaperAction"),Is.Null);
    }
}
