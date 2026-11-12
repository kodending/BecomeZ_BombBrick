using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void ChainReady(string species="kogason",int team=0,float arenaSize=100f)
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*arenaSize));
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(source,"skillId",MammothCombatRules.SkillId);Set(source,"augmentTranscended",true);
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
    }
    private object ChainBegin(object caster=null,object victim=null)
    {
        caster=caster??source;victim=victim??target;
        MammothCall("StartMammothExpulsion",caster,victim);
        object state=Get(caster,"mammothExpulsion");Assert.That(state,Is.Not.Null);
        MammothCall("AdvanceMammothExpulsion",caster,state,MammothExpulsionRules.Contact);
        return state;
    }
    private static object ChainField(object state,string name)=>state.GetType().GetField(name).GetValue(state);
    private static void ChainSet(object state,string name,object value)=>state.GetType().GetField(name).SetValue(state,value);
    private IList ChainFlights=>(IList)typeof(MonsterAutoBattlePrototype).GetField("mammothExpulsionFlights",Private).GetValue(battle);
    private IDictionary ChainBodies=>(IDictionary)typeof(MonsterAutoBattlePrototype).GetField("mammothRicochetByUnit",Private).GetValue(battle);
    private void ChainTick(float dt)=>MammothCall("UpdateMammothExpulsionFlights",dt);
    private void ChainClock(float age)=>typeof(MonsterAutoBattlePrototype).GetField("mammothRicochetClock",Private).SetValue(battle,age);
    private void ChainVelocity(object victim,Vector3 velocity)=>ChainSet(ChainBodies[victim],"velocity",velocity);

    [TestCase("kogason",0)] [TestCase("seolgason",0)] [TestCase("irontusk",0)] [TestCase("gaiason",0)]
    [TestCase("kogason",1)] [TestCase("seolgason",1)] [TestCase("irontusk",1)] [TestCase("gaiason",1)]
    public void MammothChainHitsEveryLivingEnemyOnceAtContactWithoutHealthOrRangeExecution(string species,int team)
    {
        ChainReady(species,team);Model(target).transform.position=Vector3.right*25f;
        var other=Unit(1-team,Vector3.left*25f);var ally=Unit(team,Vector3.forward*10f);
        MammothCall("StartMammothExpulsion",source,target);object state=Get(source,"mammothExpulsion");
        MammothCall("AdvanceMammothExpulsion",source,state,.449f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(ChainFlights,Is.Empty);
        MammothCall("AdvanceMammothExpulsion",source,state,.45f);
        MammothCall("AdvanceMammothExpulsion",source,state,.8f);
        Assert.That(Hp(target),Is.EqualTo(4900f).Within(.001f));Assert.That(Hp(other),Is.EqualTo(4900f).Within(.001f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));Assert.That(Get(target,"alive"),Is.True);
        Assert.That(Get(target,"mammothExpelled"),Is.False);
        Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));Assert.That(ChainFlights.Count,Is.EqualTo(1));
    }

    [TestCase(0f,100f)] [TestCase(50f,66.66667f)] [TestCase(100f,50f)]
    public void MammothChainInitialDamageUsesOrdinaryArmor(float defense,float damage)
    {
        ChainReady();Set(target,"defense",defense);ChainBegin();
        Assert.That(Hp(target),Is.EqualTo(5000f-damage).Within(.01f));
    }

    [Test]
    public void MammothChainInvulnerableEnemyTakesNoDamageAndIsNotDisplaced()
    {
        ChainReady();Set(target,"augmentReturnInvulnerabilityTimer",2f);Vector3 before=Model(target).transform.position;
        ChainBegin();ChainTick(.2f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Get(target,"mammothRicochetActive"),Is.False);
        Assert.That(Model(target).transform.position,Is.EqualTo(before));
    }

    [Test]
    public void MammothChainRespectsShieldsAndControlImmunityWithoutSuppressingOrdinaryDamage()
    {
        ChainReady();var statuses=(CombatStatusState)Get(target,"combatStatuses");
        statuses.ApplyShield(10f,60f,target);statuses.ControlImmune=true;
        ChainBegin();
        Assert.That(Hp(target),Is.EqualTo(4960f).Within(.001f));
        Assert.That(Get(target,"mammothRicochetActive"),Is.False);
        Assert.That(statuses.ControlImmune,Is.True,"The spell must not overwrite an existing immunity.");
    }

    [Test]
    public void MammothChargeActuallyMovesTheCasterWhileVictimsSlideOnTheGroundAndEventuallyRelease()
    {
        ChainReady();Set(target,"visualHeightOffset",.7f);Model(target).transform.position=new Vector3(8f,.7f,0f);
        ChainBegin();Vector3 caster=Model(source).transform.position,victim=Model(target).transform.position;
        ChainTick(.2f);
        Assert.That(Vector3.Distance(caster,Model(source).transform.position),Is.GreaterThan(1f));
        Assert.That(Vector3.Distance(victim,Model(target).transform.position),Is.GreaterThan(1f));
        Assert.That(Model(target).transform.position.y,Is.EqualTo(.7f).Within(.001f));
        ChainTick(.5f);Assert.That(Get(source,"mammothExpulsion"),Is.Null);
        ChainTick(2.5f);
        Assert.That(Get(target,"mammothRicochetActive"),Is.False);Assert.That(ChainBodies.Count,Is.Zero);
        Assert.That(ChainFlights.Count,Is.Zero);Assert.That((Vector3)Get(target,"velocity"),Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void MammothWallReflectionDealsOneCollisionEventAndKeepsTheBodyInsideTheArena()
    {
        ChainReady(arenaSize:10f);Model(target).transform.position=Vector3.right*4.65f;ChainBegin();
        ChainTick(.03f);
        Assert.That(Hp(target),Is.EqualTo(4840f).Within(.001f));
        Assert.That(((Vector3)ChainField(ChainBodies[target],"velocity")).x,Is.LessThan(0f));
        Assert.That(Model(target).transform.position.x,Is.InRange(-4.7001f,4.7001f));
        ChainTick(.02f);Assert.That(Hp(target),Is.EqualTo(4840f).Within(.001f));
    }

    [Test]
    public void MammothCornerReflectionIsOneDamageEventRatherThanOnePerAxis()
    {
        ChainReady(arenaSize:10f);Model(target).transform.position=new Vector3(4.65f,0f,4.65f);ChainBegin();
        ChainTick(.03f);Vector3 velocity=(Vector3)ChainField(ChainBodies[target],"velocity");
        Assert.That(velocity.x,Is.LessThan(0f));Assert.That(velocity.z,Is.LessThan(0f));
        Assert.That(Hp(target),Is.EqualTo(4840f).Within(.001f));
    }

    [Test]
    public void MammothBodyCollisionReflectsAndDamagesBothEnemiesButDoesNotRepeatWhileSeparating()
    {
        ChainReady();var other=Unit(1,Vector3.left*4f);ChainBegin();
        Model(target).transform.position=Vector3.left*.29f;Model(other).transform.position=Vector3.right*.29f;
        ChainVelocity(target,Vector3.right*8f);ChainVelocity(other,Vector3.left*8f);
        MammothCall("ResolveMammothBodyCollision",target,other);
        Assert.That(Hp(target),Is.EqualTo(4840f).Within(.001f));Assert.That(Hp(other),Is.EqualTo(4840f).Within(.001f));
        Assert.That(((Vector3)ChainField(ChainBodies[target],"velocity")).x,Is.LessThan(0f));
        Assert.That(((Vector3)ChainField(ChainBodies[other],"velocity")).x,Is.GreaterThan(0f));
        MammothCall("ResolveMammothBodyCollision",target,other);
        Assert.That(Hp(target),Is.EqualTo(4840f).Within(.001f));Assert.That(Hp(other),Is.EqualTo(4840f).Within(.001f));
    }

    [Test]
    public void MammothRepeatedContactsAreCappedAtSixPerVictimAndPerCast()
    {
        ChainReady();var other=Unit(1,Vector3.left*4f);ChainBegin();
        for(int i=0;i<10;i++)
        {
            ChainClock(i*.2f);
            Model(target).transform.position=Vector3.left*.29f;Model(other).transform.position=Vector3.right*.29f;
            ChainVelocity(target,Vector3.right*8f);ChainVelocity(other,Vector3.left*8f);
            MammothCall("ResolveMammothBodyCollision",target,other);
        }
        Assert.That(Hp(target),Is.EqualTo(4540f).Within(.001f));Assert.That(Hp(other),Is.EqualTo(4540f).Within(.001f));
        Assert.That(((IDictionary)ChainField(ChainFlights[0],"collisionHits"))[target],Is.EqualTo(6));
    }

    [Test]
    public void MammothContactFeedbackPrefersWallsAndCoalescesManyHitsInsideOneCooldown()
    {
        ChainReady();ChainBegin();object flight=ChainFlights[0];
        MammothCall("MammothRicochetCollisionDamage",flight,target,Vector3.right,false);
        MammothCall("MammothRicochetCollisionDamage",flight,target,Vector3.left,true);
        MammothCall("MammothRicochetCollisionDamage",flight,target,Vector3.forward,false);
        Assert.That(ChainField(flight,"collisionFeedbackWall"),Is.True);
        Assert.That(ChainField(flight,"collisionFeedbackPoint"),Is.EqualTo(Vector3.left));
        MammothCall("FlushMammothCollisionFeedback",flight);
        Assert.That(ChainField(flight,"collisionFeedbackPending"),Is.False);
        Assert.That(ChainField(flight,"collisionShakePlayed"),Is.True);
        Assert.That((float)ChainField(flight,"nextCollisionFeedbackAt"),Is.EqualTo(.18f).Within(.001f));
        ChainClock(.1f);MammothCall("MammothRicochetCollisionDamage",flight,target,Vector3.zero,true);
        MammothCall("FlushMammothCollisionFeedback",flight);
        Assert.That((float)ChainField(flight,"nextCollisionFeedbackAt"),Is.EqualTo(.18f).Within(.001f));
    }

    [Test]
    public void MammothSubstepsCatchFastBodyCollisionsEvenAcrossOneLargeFrame()
    {
        ChainReady();var other=Unit(1,Vector3.left*4f);ChainBegin();
        Model(target).transform.position=Vector3.left*2f;Model(other).transform.position=Vector3.right*2f;
        ChainVelocity(target,Vector3.right*30f);ChainVelocity(other,Vector3.left*30f);
        ChainTick(.1f);
        Assert.That(Hp(target),Is.EqualTo(4840f).Within(.001f));Assert.That(Hp(other),Is.EqualTo(4840f).Within(.001f));
        Assert.That(Model(target).transform.position.x,Is.LessThan(Model(other).transform.position.x));
    }

    [Test]
    public void MammothLargeDeltaAndSmallDeltasHaveTheSameWallDamageAndSimilarGroundPosition()
    {
        ChainReady(arenaSize:10f);Model(target).transform.position=Vector3.right*3f;ChainBegin();ChainTick(.8f);
        Vector3 single=Model(target).transform.position;float hp=Hp(target);
        MammothCall("ClearMammothExpulsionFlights");
        Set(source,"hp",5000f);Model(source).transform.position=Vector3.zero;
        Set(target,"hp",5000f);Model(target).transform.position=Vector3.right*3f;ChainBegin();
        for(int i=0;i<80;i++)ChainTick(.01f);
        Assert.That(Hp(target),Is.EqualTo(hp).Within(.001f));
        Assert.That(Vector3.Distance(single,Model(target).transform.position),Is.LessThan(.12f));
    }

    [Test]
    public void MammothGiantBodiesInATinyArenaRemainFiniteAndNeverKeepAnOrphanMovementLock()
    {
        ChainReady(arenaSize:10f);Set(target,"combatBodyScale",200f);ChainBegin();ChainTick(4f);
        Vector3 point=Model(target).transform.position;
        Assert.That(float.IsNaN(point.x)||float.IsNaN(point.z)||float.IsInfinity(point.x)||float.IsInfinity(point.z),Is.False);
        Assert.That(point.x,Is.EqualTo(0f).Within(.001f));Assert.That(point.z,Is.EqualTo(0f).Within(.001f));
        Assert.That(Get(target,"mammothRicochetActive"),Is.False);Assert.That(ChainBodies.Count,Is.Zero);
    }

    [Test]
    public void MammothPauseAndZeroDeltaDoNotAdvanceEitherCasterOrVictim()
    {
        ChainReady();ChainBegin();Vector3 a=Model(source).transform.position,b=Model(target).transform.position;
        ChainTick(0f);typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);ChainTick(2f);
        Assert.That(Model(source).transform.position,Is.EqualTo(a));Assert.That(Model(target).transform.position,Is.EqualTo(b));
        Assert.That(Hp(target),Is.EqualTo(4900f).Within(.001f));
    }

    [Test]
    public void MammothPreparationInterruptionBeforeContactDoesNotCreateAFlightOrHitEnemies()
    {
        ChainReady();MammothCall("StartMammothExpulsion",source,target);object state=Get(source,"mammothExpulsion");
        Set(source,"positionRevision",1);MammothCall("AdvanceMammothExpulsion",source,state,.5f);
        Assert.That(Get(source,"mammothExpulsion"),Is.Null);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(ChainFlights.Count,Is.Zero);
    }

    [Test]
    public void MammothBeingHitDuringItsOwnPreparationReleasesThatPreparationBeforeSliding()
    {
        ChainReady();Set(target,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_kogason"));
        Set(target,"augmentTranscended",true);MammothCall("StartMammothExpulsion",target,source);
        Assert.That(Get(target,"mammothExpulsion"),Is.Not.Null);
        ChainBegin();Assert.That(Get(target,"mammothExpulsion"),Is.Null);
        Assert.That(Get(target,"mammothRicochetActive"),Is.True);
        ChainTick(4f);Assert.That(Get(target,"mammothRicochetActive"),Is.False);
    }

    [Test]
    public void MammothReleasedVictimsContinueWhenTheCasterDiesButDeadVictimsReleaseImmediately()
    {
        ChainReady();ChainBegin();Set(source,"alive",false);Set(source,"hp",0f);
        Vector3 before=Model(target).transform.position;ChainTick(.2f);
        Assert.That(Vector3.Distance(before,Model(target).transform.position),Is.GreaterThan(1f));
        Set(target,"alive",false);Set(target,"hp",0f);ChainTick(.01f);
        Assert.That(Get(target,"mammothRicochetActive"),Is.False);Assert.That(ChainBodies.Count,Is.Zero);
    }

    [Test]
    public void MammothResetCancelsEveryBodyAndOldGenerationCannotDealMoreDamage()
    {
        ChainReady(arenaSize:10f);ChainBegin();float hp=Hp(target);
        typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        ChainTick(.2f);Assert.That(Hp(target),Is.EqualTo(hp));Assert.That(Get(target,"mammothRicochetActive"),Is.False);
        MammothCall("ClearMammothExpulsionFlights");ChainTick(1f);
        Assert.That(ChainFlights.Count,Is.Zero);Assert.That(ChainBodies.Count,Is.Zero);
        Assert.That(Get(source,"mammothExpulsion"),Is.Null);Assert.That(Hp(target),Is.EqualTo(hp));
    }

    [Test]
    public void MammothTwoSimultaneousCastsHaveOneMovementOwnerAndNoSummedLaunchVelocity()
    {
        ChainReady();var other=Unit(0,Vector3.left*3f);
        Set(other,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_kogason"));
        Set(other,"augmentTranscended",true);ChainBegin();ChainBegin(other,target);
        Assert.That(Hp(target),Is.EqualTo(4800f).Within(.001f));Assert.That(ChainBodies.Count,Is.EqualTo(1));
        Assert.That(((Vector3)ChainField(ChainBodies[target],"velocity")).magnitude,
            Is.EqualTo(17f).Within(.001f),"A second cast may replace the impulse but must not double its speed.");
        ChainTick(4f);Assert.That(ChainBodies.Count,Is.Zero);Assert.That(ChainFlights.Count,Is.Zero);
    }

    [Test]
    public void MammothNativeChainCanStartAgainAfterRecoveryWithoutAWaveReset()
    {
        ChainReady();ChainBegin();ChainTick(4f);
        Assert.That(Get(source,"mammothExpulsion"),Is.Null);Assert.That(ChainFlights,Is.Empty);
        float hp=Hp(target);ChainBegin();
        Assert.That(Hp(target),Is.EqualTo(hp-100f).Within(.001f));
        Assert.That(Get(source,"traitActivations"),Is.EqualTo(2));Assert.That(ChainFlights.Count,Is.EqualTo(1));
    }

    [Test]
    public void MammothEarlierRicochetExpiryCannotReleaseANewerCastOrItsVictim()
    {
        ChainReady();ChainBegin();ChainTick(MammothExpulsionRules.Duration-.1f);
        Assert.That(Get(source,"mammothExpulsion"),Is.Null);Assert.That(ChainFlights.Count,Is.EqualTo(1));
        object next=ChainBegin();ChainTick(.11f);
        Assert.That(ChainFlights.Count,Is.EqualTo(1));
        Assert.That(Get(source,"mammothExpulsion"),Is.SameAs(next));
        Assert.That(Get(target,"mammothRicochetActive"),Is.True);
        Assert.That(ChainField(ChainBodies[target],"flight"),Is.SameAs(ChainFlights[0]));
        ChainTick(MammothExpulsionRules.Duration);
        Assert.That(Get(target,"mammothRicochetActive"),Is.False);Assert.That(ChainBodies,Is.Empty);
    }
}

public sealed class MammothRicochetMathTests
{
    [TestCase(4.6f,20f)] [TestCase(-4.6f,-20f)]
    public void RadiusAwareWallReflectsVelocityAndRetainsMomentum(float x,float speed)
    {
        Vector3 velocity=Vector3.right*speed;
        bool hit=MammothExpulsionRules.MoveInside(new Bounds(Vector3.zero,Vector3.one*10f),Vector3.right*x,
            ref velocity,.3f,.02f,out Vector3 ground,out Vector3 normal);
        Assert.That(hit,Is.True);Assert.That(Mathf.Sign(velocity.x),Is.EqualTo(-Mathf.Sign(speed)));
        Assert.That(Mathf.Abs(velocity.x),Is.EqualTo(Mathf.Abs(speed)*.92f).Within(.001f));
        Assert.That(ground.x,Is.InRange(-4.7f,4.7f));Assert.That(normal.sqrMagnitude,Is.GreaterThan(.9f));
    }

    [Test]
    public void DegenerateArenaAxisClampsAnOversizedBodyWithoutNaN()
    {
        Vector3 velocity=new Vector3(10f,0f,10f);
        MammothExpulsionRules.MoveInside(new Bounds(Vector3.zero,new Vector3(3f,1f,3f)),Vector3.one*10f,
            ref velocity,20f,1f,out Vector3 ground,out _);
        Assert.That(ground,Is.EqualTo(Vector3.zero));Assert.That(velocity,Is.EqualTo(Vector3.zero));
    }
}
