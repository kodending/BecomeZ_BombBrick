using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    [TestCase(false,false)] [TestCase(false,true)]
    [TestCase(true,false)] [TestCase(true,true)]
    public void AttachedRankThreeWyvernBreathStopsDamageOnDeath(bool overheat,bool lethalHit)
    {
        Unit(0,Vector3.left*3f);
        if(overheat)OverheatSetup();else WyvernSetup();
        BreathActionTick(overheat?.74f:.52f);
        float hp=Hp(target);int hits=(int)Get(source,"skillHits");
        Assert.That(hp,Is.LessThan(5000f),"The channel must already have released and hit before death.");
        Assert.That(Get(source,"wyvernBreath"),Is.Not.Null);

        if(lethalHit)DisableReleasedCaster("death");
        else {Set(source,"alive",false);Set(source,"hp",0f);BreathTick(.01f);}
        Assert.That(Get(source,"wyvernBreath"),Is.Null,"A dead caster must release its channel pointer.");
        Assert.That(Get(source,"wyvernBreathPending"),Is.False);
        Assert.That(Get(source,"wyvernBreathTarget"),Is.Null);
        BreathTick(5f);
        Assert.That(Hp(target),Is.EqualTo(hp));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(hits));
        Assert.That(HeavenBreaths,Is.Empty,"Ordinary channels cannot create a rank-four cast.");
        Assert.That((IList)typeof(MonsterAutoBattlePrototype).GetField("wyvernBreaths",Private).GetValue(battle),Is.Empty);
        ReaperCall("CheckBattleResult");
        Assert.That(typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).GetValue(battle),Is.False,
            "Only the caster died; the remaining ally must keep fighting.");
    }

    [Test] public void CasterDeathStopsTheAttachedBreathAndItsReleasedEchoTogether()
    {
        Unit(0,Vector3.left*3f);
        WyvernSetup();BreathActionTick(.52f);
        int generation=(int)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle);
        Assert.That(GriffinCall("StartWyvernBreath",source,target,GameMasterData.Skill(WyvernCombatRules.SkillId),
            1f,true,generation),Is.True);
        BreathTick(.05f);
        var casts=(IList)typeof(MonsterAutoBattlePrototype).GetField("wyvernBreaths",Private).GetValue(battle);
        Assert.That(casts.Count,Is.EqualTo(2));
        float hp=Hp(target);int hits=(int)Get(source,"skillHits");
        DisableReleasedCaster("death");
        Assert.That(Get(source,"wyvernBreath"),Is.Null);
        foreach(var castState in casts)
            Assert.That(castState.GetType().GetField("finished").GetValue(castState),Is.True,
                "The echo must not retain its own channel after its caster dies.");
        BreathTick(5f);
        Assert.That(Hp(target),Is.EqualTo(hp));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(hits));
        Assert.That(casts,Is.Empty);Assert.That(HeavenBreaths,Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public void LethalReflectionDuringABreathHitCannotDispatchItsLightningIntrinsic(bool overheat)
    {
        if(overheat)OverheatSetup("temvire",WyvernCombatRules.TemvireTraitId);
        else WyvernSetup("temvire",WyvernCombatRules.TemvireTraitId);
        var chain=Unit(1,new Vector3(4f,0f,4f));
        Set(target,"corruptReflectionRatio",1f);Set(source,"hp",1f);
        BreathActionTick(overheat?.74f:.52f);
        Assert.That(Get(source,"alive"),Is.False);
        Assert.That(Hp(target),Is.EqualTo(overheat?4970f:4950f).Within(.001f),
            "The hit that caused the reflection remains confirmed.");
        Assert.That(Hp(chain),Is.EqualTo(5000f),"The dead caster cannot dispatch a new lightning hit afterward.");
        Assert.That(Get(source,"wyvernBreath"),Is.Null);
        BreathTick(3f);
        Assert.That(Hp(chain),Is.EqualTo(5000f));
    }

    [TestCase(false)] [TestCase(true)]
    public void CasterDeathKeepsAlreadyReleasedPressureAndRainDamage(bool rain)
    {
        Unit(0,Vector3.left*3f);
        if(rain)RainSetup();else PressureSetup();
        BreathActionTick(rain?1.15f:.54f);
        float before=Hp(target);
        DisableReleasedCaster("death");BreathTick(4f);
        Assert.That(Hp(target),Is.LessThan(before),"Detached attacks must finish after the caster dies.");
        Assert.That(Hp(target),Is.EqualTo(rain?4580f:4700f).Within(.001f));
        Assert.That(Get(source,"wyvernBreath"),Is.Null);
    }
}
