using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void PressureSetup(string id="vivre",string intrinsic=null)
    {Trait(WyvernRank3Rules.CataclysmBreath);WyvernSetup(id,intrinsic);}

    [Test]
    public void WyvernPressureFrontHitsOnceInAForwardConeWithoutPushingOrSlidingVictims()
    {
        PressureSetup();object near=Unit(1,Vector3.right*2f),far=Unit(1,new Vector3(5.6f,0,3f));
        object side=Unit(1,new Vector3(2,0,4)),behind=Unit(1,Vector3.left),outside=Unit(1,Vector3.right*7.2f),ally=Unit(0,Vector3.right);
        BreathActionTick(.5f);Assert.That(Hp(near),Is.EqualTo(5000));
        BreathActionTick(.035f);Assert.That(Hp(near),Is.EqualTo(4700));Assert.That(Hp(target),Is.EqualTo(5000));
        Assert.That(Get(near,"displacing"),Is.False);
        Assert.That(Get(near,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Model(near).transform.position,Is.EqualTo(Vector3.right*2f));
        BreathActionTick(.165f);Assert.That(Hp(near),Is.EqualTo(4700));Assert.That(Hp(target),Is.EqualTo(4700));Assert.That(Hp(far),Is.EqualTo(4700));
        foreach(var unit in new[]{side,behind,outside,ally})Assert.That(Hp(unit),Is.EqualTo(5000));
        Assert.That(Get(source,"wyvernBreath"),Is.Null);BreathTick(1f);Assert.That(Hp(near),Is.EqualTo(4700));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));
    }

    [Test]
    public void WyvernPressureRangeMatchesApproachAndCastAndNeverUsesTheOldChannel()
    {
        GriffinSetup("vivre");Trait(WyvernRank3Rules.CataclysmBreath);
        var skill=GameMasterData.Skill(WyvernCombatRules.SkillId);
        float range=(float)GriffinCall("SkillActivationDistance",source,target,skill);
        Assert.That(range,Is.EqualTo(4f*MonsterAutoBattlePrototype.TileSize+.3f));
        Model(target).transform.position=Vector3.right*(range+.02f);
        Assert.That(GriffinCall("BeginWyvernBreath",source,skill),Is.False);
        Model(target).transform.position=Vector3.right*(range-.02f);
        Assert.That(GriffinCall("BeginWyvernBreath",source,skill),Is.True);
        BreathActionTick(.49f);Assert.That(Hp(target),Is.EqualTo(5000));
        BreathActionTick(.21f);Assert.That(Hp(target),Is.EqualTo(4700));
    }

    [Test]
    public void WyvernPressureFreezesAndPausesWithCombatAndDisplacementCancelsIt()
    {
        PressureSetup();var statuses=(CombatStatusState)Get(source,"combatStatuses");
        statuses.ApplyTimed(CombatStatusKind.Freeze,2f,target);BreathTick(2f);Assert.That(Hp(target),Is.EqualTo(5000));
        statuses.Remove(CombatStatusKind.Freeze);
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);
        BreathTick(2f);Assert.That(Hp(target),Is.EqualTo(5000));
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);
        BreathActionTick(.49f);GriffinCall("InterruptPositionalAction",source);BreathTick(.5f);
        Assert.That(Hp(target),Is.EqualTo(5000));Assert.That(Get(source,"wyvernBreath"),Is.Null);
    }

    [TestCase("vivre",WyvernCombatRules.VivreTraitId,CombatStatusKind.Poison,1)]
    [TestCase("frovire",WyvernCombatRules.FrovireTraitId,CombatStatusKind.ComboChilled,2)]
    public void WyvernPressureSpeciesEffectsApplyOnceWithoutDisplacementOrAnExtraFreeze(string id,string intrinsic,CombatStatusKind kind,int stacks)
    {
        PressureSetup(id,intrinsic);BreathActionTick(.7f);BreathTick(.2f);
        var statuses=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(Hp(target),Is.EqualTo(4700));Assert.That(statuses.Get(kind).stacks,Is.EqualTo(stacks));
        Assert.That(statuses.Has(CombatStatusKind.Freeze),Is.False);
        Assert.That(Get(target,"displacing"),Is.False);
        Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void WyvernPressureControlImmuneEnemyStillTakesDamageWithoutMoving()
    {
        PressureSetup();((CombatStatusState)Get(target,"combatStatuses")).ControlImmune=true;
        BreathActionTick(.7f);Assert.That(Hp(target),Is.EqualTo(4700));Assert.That(Get(target,"displacing"),Is.False);
    }

    [Test]
    public void WyvernPressureLightningChainsOncePerContact()
    {
        PressureSetup("temvire",WyvernCombatRules.TemvireTraitId);object chain=Unit(1,new Vector3(4,0,6));
        BreathActionTick(.7f);BreathTick(.5f);Assert.That(Hp(target),Is.EqualTo(4700));Assert.That(Hp(chain),Is.EqualTo(4950));
    }

    [Test]
    public void WyvernPressureFireStaysInTheConeTicksFourTimesAndRespectsPauseAndReset()
    {
        PressureSetup("igvire",WyvernCombatRules.IgvireTraitId);
        object outside=Unit(1,new Vector3(0,0,5));BreathActionTick(.7f);Assert.That(Hp(target),Is.EqualTo(4700));
        Assert.That(Get(source,"wyvernBreath"),Is.Null);
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);
        BreathTick(2f);Assert.That(Hp(target),Is.EqualTo(4700));
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);
        BreathTick(4f);Assert.That(Hp(target),Is.EqualTo(4620));Assert.That(Hp(outside),Is.EqualTo(5000));
        BreathTick(4f);Assert.That(Hp(target),Is.EqualTo(4620));
        PressureSetup("igvire",WyvernCombatRules.IgvireTraitId);BreathActionTick(.7f);float hp=Hp(target);
        GriffinCall("ClearWyvernBreaths");BreathTick(4f);Assert.That(Hp(target),Is.EqualTo(hp));
    }

    [TestCase("breath",false)] [TestCase("breath",true)]
    [TestCase("storm_chain",false)] [TestCase("storm_chain",true)]
    [TestCase("pressure",false)] [TestCase("pressure",true)]
    [TestCase("rain",false)] [TestCase("rain",true)]
    [TestCase("overheat",false)] [TestCase("overheat",true)]
    [TestCase("heaven",false)] [TestCase("heaven",true)]
    public void WyvernDamageNeverStartsOrChangesVictimSlidingOrForcedDisplacement(string attack,bool alreadySliding)
    {
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,1f);
        object victim=attack=="storm_chain"?Unit(1,new Vector3(4f,0f,4f)):target;
        if(attack=="pressure")PressureSetup();
        else if(attack=="rain")RainSetup();
        else if(attack=="overheat")OverheatSetup();
        else if(attack=="storm_chain")WyvernSetup("temvire",WyvernCombatRules.TemvireTraitId);
        else
        {
            if(attack=="heaven")Set(source,"augmentTranscended",true);
            WyvernSetup();
            if(attack=="heaven")
            {
                Assert.That(HeavenBreaths.Count,Is.EqualTo(1));Assert.That(Get(source,"wyvernBreath"),Is.Null);
            }
        }

        Vector3 carried=alreadySliding?Vector3.left*.7f:Vector3.zero;
        Vector3 inertia=Vector3.forward*.6f,position=Model(victim).transform.position;
        Set(victim,"hitSlideVelocity",carried);Set(victim,"hitSlideNextAt",.4f);Set(victim,"inertiaVelocity",inertia);
        float hp=Hp(victim);int revision=(int)Get(victim,"positionRevision");
        if(attack=="heaven")HeavenTick(WyvernHeavenBreathRules.ImpactTime);
        else BreathActionTick(attack=="pressure"?.7f:attack=="rain"?2.45f:attack=="overheat"?2.4f:1f);

        float expected=attack=="storm_chain"?50f:attack=="pressure"||attack=="heaven"?300f:
            attack=="rain"?420f:attack=="overheat"?360f:200f;
        Assert.That(Hp(victim),Is.EqualTo(hp-expected).Within(.003f),"Removing movement feedback must preserve real skill damage.");
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(carried));
        Assert.That(Get(victim,"hitSlideNextAt"),Is.EqualTo(.4f),"A Wyvern hit must not consume another skill's slide throttle.");
        Assert.That(Get(victim,"inertiaVelocity"),Is.EqualTo(inertia));
        Assert.That(Get(victim,"displacing"),Is.False);
        Assert.That(Get(victim,"displacementDuration"),Is.EqualTo(0f));
        Assert.That(Get(victim,"positionRevision"),Is.EqualTo(revision));
        Assert.That(Model(victim).transform.position,Is.EqualTo(position));
    }

    [Test]
    public void ADirectSkillFromAnotherFamilyStillSlidesAfterAWyvernHit()
    {
        WyvernSetup();BreathActionTick(1f);
        Assert.That(Hp(target),Is.EqualTo(4800f));Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_pone"));
        float hp=Hp(target);Vector3 position=Model(target).transform.position;
        float dealt=(float)GriffinCall("Rank3Hit",source,target,.5f,0f,0f);
        Assert.That(dealt,Is.GreaterThan(0f));Assert.That(Hp(target),Is.EqualTo(hp-dealt).Within(.001f));
        Assert.That(((Vector3)Get(target,"hitSlideVelocity")).sqrMagnitude,Is.GreaterThan(0f));
        GriffinCall("AdvanceHitSlide",target,.05f);
        Assert.That(Model(target).transform.position.x,Is.GreaterThan(position.x));
    }
}
