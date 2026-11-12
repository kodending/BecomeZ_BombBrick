using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private IList DeathBlooms=>(IList)typeof(MonsterAutoBattlePrototype).GetField("mushroomDeathBlooms",Private).GetValue(battle);
    private void DeathBloomReady(object caster=null)
    {
        caster=caster??source;
        Set(caster,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_versus"));
        Set(caster,"skillId",MushroomCombatRules.SkillId);Set(caster,"augmentTranscended",true);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private).SetValue(battle,
            new Bounds(Vector3.zero,new Vector3(20f,.2f,10f)));
    }
    private object KillForDeathBloom(object caster=null,object attacker=null)
    {
        caster=caster??source;MoltDamage(attacker??target,caster,999999f);
        var flower=ReaperCall("MushroomDeathBloomUnit",caster);Assert.That(flower,Is.Not.Null);
        // The fixture's host stays inactive to avoid scene setup; actors remain active independently.
        Model(flower).transform.SetParent(null,true);
        ((GameObject)Get(flower,"ring")).transform.SetParent(null,true);
        ReaperCall("FlushMushroomDeathBlooms");
        return flower;
    }
    private void DeathBloomTick(float dt)=>ReaperCall("UpdateMushroomDeathBlooms",dt);
    private object DeathBloomField(int index,string field)=>DeathBlooms[index].GetType().GetField(field).GetValue(DeathBlooms[index]);
    private void DeathBloomResult()=>ReaperCall("CheckBattleResult");
    private object DeathBloomBattleField(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private).GetValue(battle);

    [TestCase(0)]
    [TestCase(1)]
    public void DeathBloomCopiesOneHundredPercentMaximumHealthDefenseAndAttackAtTheExactDeathPosition(int team)
    {
        DeathBloomReady();Set(source,"team",System.Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(target,"team",System.Enum.ToObject(unitType.GetField("team").FieldType,1-team));
        Set(source,"maxHp",4321f);Set(source,"defense",173f);Set(source,"damage",137f);
        Vector3 death=new Vector3(3.7f,0f,-2.3f);Model(source).transform.position=death;
        var flower=KillForDeathBloom();
        Assert.That(Hp(source),Is.LessThanOrEqualTo(0f));Assert.That(Get(source,"alive"),Is.False);
        Assert.That(Hp(flower),Is.EqualTo(4321f));Assert.That(Get(flower,"maxHp"),Is.EqualTo(4321f));
        Assert.That(Get(flower,"defense"),Is.EqualTo(173f));Assert.That(Get(flower,"damage"),Is.EqualTo(137f));
        Assert.That(Get(flower,"team"),Is.EqualTo(Get(source,"team")));
        Assert.That(Model(flower).transform.position,Is.EqualTo(death));
        Assert.That(Get(flower,"speedTilesPerSecond"),Is.EqualTo(0f));
        Assert.That(Get(flower,"augmentTranscended"),Is.False);Assert.That(Get(flower,"ownedInstance"),Is.Null);
    }

    [TestCase(0f,960f)]
    [TestCase(50f,640f)]
    [TestCase(100f,480f)]
    public void DeathBloomDeliversSixteenIndependentTicksFromTheFrozenAttackSnapshot(float defense,float expected)
    {
        DeathBloomReady();Set(target,"defense",defense);var flower=KillForDeathBloom();
        Set(source,"damage",9000f);Set(source,"defense",0f);Set(source,"maxHp",1f);
        DeathBloomTick(.49f);Assert.That(Hp(target),Is.EqualTo(5000f));
        DeathBloomTick(.01f);Assert.That(Hp(target),Is.LessThan(5000f));
        DeathBloomTick(7.5f);Assert.That(Hp(target),Is.EqualTo(5000f-expected).Within(.02f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(16));Assert.That(Get(source,"skillDamage"),Is.EqualTo(expected).Within(.02f));
        Assert.That(Get(flower,"maxHp"),Is.EqualTo(5000f));
        DeathBloomResult();Assert.That(Get(flower,"alive"),Is.False);Assert.That(DeathBlooms.Count,Is.Zero);
        DeathBloomTick(12f);Assert.That(Hp(target),Is.EqualTo(5000f-expected).Within(.02f));
    }

    [Test]
    public void DeathBloomIsAnAttackableEnemyAndDestructionCancelsFutureDamageWithoutRecursion()
    {
        DeathBloomReady();var flower=KillForDeathBloom();
        Assert.That(ReaperCall("SelectNearestTarget",target,false,null),Is.SameAs(flower));
        Assert.That(ReaperCall("ResolveRank3BasicAttack",flower,target,999f),Is.True);
        DeathBloomTick(1f);MoltDamage(target,flower,999999f);DeathBloomTick(20f);
        Assert.That(Get(flower,"alive"),Is.False);Assert.That(Hp(target),Is.EqualTo(4880f));
        Assert.That(DeathBlooms.Count,Is.Zero);Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));
    }

    [Test]
    public void DeathBloomDoesNotReplaceAnExistingRankThreeFlower()
    {
        var ordinary=MycokingReady();DeathBloomReady();var death=KillForDeathBloom();
        Assert.That(Get(ordinary,"alive"),Is.True);Assert.That(death,Is.Not.SameAs(ordinary));
        Assert.That(Get(ordinary,"rank3MycokingSummon"),Is.True);Assert.That(DeathBlooms.Count,Is.EqualTo(1));
        ReaperCall("RetirePreviousMycokings",source,false);
        Assert.That(Get(ordinary,"alive"),Is.False);
        Assert.That(Get(death,"alive"),Is.True,"A previously released rank-three cast cannot replace the death flower.");
    }

    [Test]
    public void DeathBloomOnlyTriggersOncePerOriginalUnitUntilWaveCleanup()
    {
        DeathBloomReady();var first=KillForDeathBloom();Set(source,"alive",true);Set(source,"hp",1f);
        Model(source).SetActive(true);MoltDamage(target,source,999999f);
        Assert.That(DeathBlooms.Count,Is.EqualTo(1));Assert.That(ReaperCall("MushroomDeathBloomUnit",source),Is.SameAs(first));
        ReaperCall("ClearMushroomGasZones");Assert.That(Get(first,"alive"),Is.False);
        Assert.That(DeathBlooms.Count,Is.Zero);Set(source,"alive",true);Set(source,"hp",1f);Model(source).SetActive(true);
        var next=KillForDeathBloom();Assert.That(next,Is.Not.SameAs(first));Assert.That(DeathBlooms.Count,Is.EqualTo(1));
    }

    [Test]
    public void DeathBloomChecksCurrentPositionsAndTargetabilityWithoutSlidingOrFriendlyDamage()
    {
        DeathBloomReady();var flower=KillForDeathBloom();object ally=Unit(0,Vector3.zero);
        Vector3 fixedAt=Model(flower).transform.position;DeathBloomTick(.5f);
        Assert.That(Hp(target),Is.EqualTo(4940f));Assert.That(Hp(ally),Is.EqualTo(5000f));
        Model(target).transform.position=Vector3.right*30f;DeathBloomTick(.5f);
        Assert.That(Hp(target),Is.EqualTo(4940f));Model(target).transform.position=Vector3.right*4f;
        Set(target,"ninjaUntargetableTimer",1f);DeathBloomTick(.5f);Assert.That(Hp(target),Is.EqualTo(4940f));
        Set(target,"ninjaUntargetableTimer",0f);DeathBloomTick(.5f);Assert.That(Hp(target),Is.EqualTo(4880f));
        Assert.That(Model(flower).transform.position,Is.EqualTo(fixedAt));
        Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LastSurvivingDeathFlowerCanWinFromEitherTeamOnItsFinalValidTick(bool enemy)
    {
        DeathBloomReady();
        if(enemy)
        {
            Set(source,"team",System.Enum.ToObject(unitType.GetField("team").FieldType,1));
            Set(target,"team",System.Enum.ToObject(unitType.GetField("team").FieldType,0));
        }
        KillForDeathBloom();Set(target,"hp",950f);
        DeathBloomTick(7.5f);Assert.That(Hp(target),Is.EqualTo(50f).Within(.001f));DeathBloomResult();
        Assert.That(DeathBloomBattleField("battleEnding"),Is.False);
        DeathBloomTick(.5f);DeathBloomResult();Assert.That(Get(target,"alive"),Is.False);
        Assert.That(DeathBloomBattleField("battleEnding"),Is.True);
        Assert.That(DeathBloomBattleField("pendingResult"),Is.EqualTo(enemy?"적군 승리":"아군 승리"));
    }

    [Test]
    public void ExpiredLastFlowerLosesWhenAnEnemyStillSurvives()
    {
        DeathBloomReady();var flower=KillForDeathBloom();DeathBloomTick(8f);DeathBloomResult();
        Assert.That(Get(flower,"alive"),Is.False);Assert.That(Get(target,"alive"),Is.True);
        Assert.That(DeathBloomBattleField("pendingResult"),Is.EqualTo("적군 승리"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BothTeamsDeathFlowersResolveSimultaneousTicksBeforeTheExistingDoubleWipeLoss(bool reverseOrder)
    {
        DeathBloomReady();DeathBloomReady(target);var a=KillForDeathBloom();var b=KillForDeathBloom(target,source);
        if(reverseOrder){var first=DeathBlooms[0];DeathBlooms.RemoveAt(0);DeathBlooms.Add(first);}
        Set(a,"hp",50f);Set(b,"hp",50f);DeathBloomResult();
        Assert.That(DeathBloomBattleField("battleEnding"),Is.False);
        DeathBloomTick(.5f);Assert.That(Get(a,"alive"),Is.False);Assert.That(Get(b,"alive"),Is.False);
        DeathBloomResult();Assert.That(DeathBloomBattleField("pendingResult"),Is.EqualTo("적군 승리"));
    }

    [Test]
    public void OrdinaryRankThreeFlowerDoesNotDelayTheExistingImmediateBattleResult()
    {
        MycokingReady();Set(source,"alive",false);DeathBloomResult();
        Assert.That(DeathBloomBattleField("battleEnding"),Is.True);
        Assert.That(DeathBloomBattleField("pendingResult"),Is.EqualTo("적군 승리"));
    }

    [Test]
    public void DeathBloomPauseGenerationAndCleanupNeverLeakDamageIntoTheNextWave()
    {
        DeathBloomReady();var flower=KillForDeathBloom();DeathBloomTick(0f);Assert.That(Hp(target),Is.EqualTo(5000f));
        typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        DeathBloomTick(20f);Assert.That(Get(flower,"alive"),Is.False);Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(DeathBlooms.Count,Is.Zero);
    }

    [Test]
    public void DeathBloomCinematicAgeHandoffAndDeletedPendingQueueCannotDuplicateTicks()
    {
        DeathBloomReady();KillForDeathBloom();var state=DeathBlooms[0];
        state.GetType().GetField("awaitingPresentation").SetValue(state,true);
        ReaperCall("AdvanceMushroomDeathBloomCinematic",source,1f);Assert.That(Hp(target),Is.EqualTo(5000f));
        DeathBloomTick(0f);Assert.That(ReaperCall("IsMushroomDeathBloomPending",source),Is.False);
        ReaperCall("AdvanceMushroomDeathBloomCinematic",source,.7f);Assert.That(Hp(target),Is.EqualTo(4940f));
        ReaperCall("AdvanceMushroomDeathBloomCinematic",source,.7f);Assert.That(Hp(target),Is.EqualTo(4940f));
        DeathBloomTick(.3f);Assert.That(Hp(target),Is.EqualTo(4880f));
        Assert.That((float)DeathBloomField(0,"age"),Is.EqualTo(1f).Within(.0001f));
    }

    [Test]
    public void DeathBloomCreatedByAreaDamageDoesNotInvalidateItsCallersUnitEnumeration()
    {
        DeathBloomReady();var units=(IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle);
        Assert.DoesNotThrow(()=>
        {
            foreach(object unit in units)
                if(unit==source)MoltDamage(target,source,999999f);
        });
        Assert.That(DeathBlooms.Count,Is.EqualTo(1));Assert.That(units.Count,Is.EqualTo(2));
        ReaperCall("FlushMushroomDeathBlooms");Assert.That(units.Count,Is.EqualTo(3));
    }

    [TestCase(0f,0f)]
    [TestCase(10f,5f)]
    [TestCase(-10f,-5f)]
    [TestCase(9.8f,-4.8f)]
    public void DeathBloomCircleCoversHalfOfTheActualArenaEvenAtItsCorners(float x,float z)
    {
        var arena=new Bounds(Vector3.zero,new Vector3(20f,.2f,10f));var center=new Vector3(x,0f,z);
        float radius=MushroomDeathBloomRules.CoverageRadius(arena,center);int covered=0;
        const int columns=300,rows=200;
        for(int ix=0;ix<columns;ix++)for(int iz=0;iz<rows;iz++)
        {
            var point=new Vector3(arena.min.x+(ix+.5f)*arena.size.x/columns,0f,
                arena.min.z+(iz+.5f)*arena.size.z/rows);
            if((point-center).sqrMagnitude<=radius*radius)covered++;
        }
        Assert.That((float)covered/(columns*rows),Is.InRange(.495f,.505f));
    }
}
