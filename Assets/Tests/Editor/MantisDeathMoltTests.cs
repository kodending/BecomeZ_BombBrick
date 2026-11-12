using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void MoltReady(string trait=MantisRank3Rules.MyriadEyes)
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_mantius"));
        Set(source,"skillId",MantisCombatRules.SkillId);Set(source,"augmentTranscended",true);
        Set(source,"skillCooldownDuration",8f);Trait(trait);Trait(MantisCombatRules.MantiusTraitId);
    }
    private float MoltDamage(object attacker,object victim,float damage)=>(float)ReaperCall("ApplyDamage",
        attacker,victim,damage,false,false,false,false,0f,0f,1f,false,false,false);
    private object MoltState=>Get(source,"mantisMolt");
    private object MoltField(string name)=>MoltState.GetType().GetField(name).GetValue(MoltState);
    private object[] MoltChildren()
    {
        var children=(IList)MoltField("children");
        for(int i=0;i<3;i++)
        {
            object child=Unit(0,new Vector3(i+1,0f,1f));
            Set(child,"definition",Get(source,"definition"));
            ReaperCall("ConfigureMantisBroodling",source,child,(float)MoltField("fullHp"));
            children.Add(child);
        }
        MoltState.GetType().GetField("spawned").SetValue(MoltState,3);
        return children.Cast<object>().ToArray();
    }

    [Test]
    public void DeathMoltOnlyReplacesLethalHealthDamageAfterShields()
    {
        MoltReady();Set(source,"itemShield",200f);Set(source,"hp",100f);
        MoltDamage(target,source,150f);Assert.That(MoltState,Is.Null);Assert.That(Hp(source),Is.EqualTo(100f));
        MoltDamage(target,source,149f);Assert.That(MoltState,Is.Null);Assert.That(Hp(source),Is.EqualTo(1f));
        MoltDamage(target,source,1f);Assert.That(MoltState,Is.Not.Null);
        Assert.That(Hp(source),Is.EqualTo(1500f));Assert.That(Get(source,"maxHp"),Is.EqualTo(5000f));
        Assert.That(Get(source,"alive"),Is.True);Assert.That(Get(source,"mantisMoltUsed"),Is.True);
        Assert.That(Get(target,"kills"),Is.EqualTo(0));
    }

    [TestCase(false,false)]
    [TestCase(true,true)]
    public void DeathMoltRejectsNonTranscendedUnitsAndChildren(bool transcended,bool child)
    {
        MoltReady();Set(source,"augmentTranscended",transcended);
        if(child)Set(source,"mantisBroodOwner",target);
        Set(source,"hp",0f);Assert.That(ReaperCall("TryBeginMantisDeathMolt",source,target),Is.False);
    }

    [TestCase(MantisRank3Rules.DivineGuillotine)]
    [TestCase(MantisRank3Rules.MyriadEyes)]
    [TestCase(MantisRank3Rules.ReaperEmergence)]
    public void DeathMoltChildrenKeepTheirSelectedSkillButCannotMoltAgain(string trait)
    {
        MoltReady(trait);MoltDamage(target,source,99999f);var children=MoltChildren();
        Assert.That(children.Length,Is.EqualTo(3));
        foreach(var child in children)
        {
            Assert.That(Hp(child),Is.EqualTo(750f));Assert.That(Get(child,"damage"),Is.EqualTo(30f).Within(.001f));
            Assert.That(Get(child,"skillTimer"),Is.EqualTo(0f));
            Assert.That(((IList)Get(child,"activeTraitIds")).Contains(trait),Is.True);
            Assert.That(Get(child,"augmentTranscended"),Is.False);
            Assert.That(Get(child,"rank3MantisReaper"),Is.EqualTo(trait==MantisRank3Rules.ReaperEmergence));
            float scale=trait==MantisRank3Rules.ReaperEmergence?.66f:.55f;
            Assert.That(Model(child).transform.localScale.x,Is.EqualTo(scale).Within(.001f));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeathMoltAssistOrDirectKillDoesNotTriggerHatching(bool allyFinisher)
    {
        MoltReady();MoltDamage(target,source,99999f);var children=MoltChildren();
        MoltDamage(children[0],target,1f);Assert.That(MoltField("hatchAge"),Is.EqualTo(-1f));
        object killer=allyFinisher?Unit(0,Vector3.left):children[0];
        MoltDamage(killer,target,99999f);Assert.That(MoltField("hatchAge"),Is.EqualTo(-1f));
        ReaperCall("TickMantisDeathMolt",source,.8f);
        Assert.That(MoltState,Is.Not.Null);Assert.That(Hp(source),Is.EqualTo(1620f).Within(.01f));
        Assert.That(children.All(child=>(bool)Get(child,"alive")),Is.True);
    }

    [Test]
    public void DeathMoltUnrelatedKillDoesNotHatchAndCocoonCannotAttack()
    {
        MoltReady();MoltDamage(target,source,99999f);MoltChildren();
        Assert.That(MoltDamage(source,target,1000f),Is.EqualTo(0f));
        MoltDamage(Unit(0,Vector3.left),target,99999f);
        Assert.That(MoltField("hatchAge"),Is.EqualTo(-1f));
    }

    [Test]
    public void DeathMoltLastChildDeathHatchesWithRecoveredHealthAndCannotMoltTwice()
    {
        MoltReady();MoltDamage(target,source,99999f);var children=MoltChildren();
        ReaperCall("TickMantisDeathMolt",source,2f);
        Assert.That(Hp(source),Is.EqualTo(1800f).Within(.01f));
        MoltDamage(target,children[0],99999f);MoltDamage(target,children[1],99999f);
        Assert.That(MoltField("hatchAge"),Is.EqualTo(-1f));
        ReaperCall("TickMantisDeathMolt",source,3f);
        Assert.That(Hp(source),Is.EqualTo(2250f).Within(.01f));
        MoltDamage(target,children[2],99999f);
        Assert.That(MoltField("hatchAge"),Is.EqualTo(0f));
        // Hatching neither heals further nor exposes the cocoon to a pending hit.
        Assert.That(MoltDamage(target,source,99999f),Is.EqualTo(0f));
        ReaperCall("TickMantisDeathMolt",source,.8f);
        Assert.That(MoltState,Is.Null);Assert.That(Get(source,"alive"),Is.True);
        Assert.That(Hp(source),Is.EqualTo(2250f).Within(.01f));
        Assert.That(children.All(child=>!(bool)Get(child,"alive")),Is.True);
        Assert.That(Get(source,"maxHp"),Is.EqualTo(5000f));
        MoltDamage(target,source,99999f);
        Assert.That(Get(source,"alive"),Is.False);Assert.That(MoltState,Is.Null);
    }

    [TestCase(false,false,false)]
    [TestCase(true,false,false)]
    [TestCase(false,true,false)]
    [TestCase(false,false,true)]
    public void DeathMoltCocoonBlocksBasicSkillOngoingAndShieldBypassingDamage(bool skill,bool selfTick,bool trueHit)
    {
        MoltReady();MoltDamage(target,source,99999f);MoltChildren();Set(source,"itemShield",200f);
        object result=ReaperCall("ApplyDamage",selfTick?source:target,source,99999f,skill,!skill&&!selfTick,
            false,false,1f,trueHit?99999f:0f,1f,trueHit,trueHit,false);
        Assert.That(result,Is.EqualTo(0f));Assert.That(Hp(source),Is.EqualTo(1500f));
        Assert.That(Get(source,"itemShield"),Is.EqualTo(200f));
        Assert.That(Get(source,"alive"),Is.True);
    }

    [Test]
    public void DeathMoltCocoonDropsEnemyTargetAndCannotBeSelectedOrDisplaced()
    {
        MoltReady();Set(target,"target",source);MoltDamage(target,source,99999f);
        var children=MoltChildren();
        Assert.That(Get(target,"target"),Is.Null);
        Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("IsCombatTargetable",
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)
            .Invoke(null,new[]{source}),Is.False);
        Assert.That(children,Does.Contain(ReaperCall("SelectTarget",target,false,null)));
        Assert.That(ReaperCall("DisplaceUnit",source,Vector3.right,2f,true,false,false,false),Is.False);
        Assert.That(Model(source).transform.position,Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void DeathMoltRecoveryCapsAtFullHealthButWaitsForLastChild()
    {
        MoltReady();MoltDamage(target,source,99999f);MoltChildren();
        ReaperCall("TickMantisDeathMolt",source,30f);
        Assert.That(Hp(source),Is.EqualTo(5000f));Assert.That(MoltField("hatchAge"),Is.EqualTo(-1f));
    }

    [Test]
    public void DeathMoltChildRemovalOutsideDamageAlsoHatches()
    {
        MoltReady();MoltDamage(target,source,99999f);var children=MoltChildren();
        foreach(var child in children)Set(child,"alive",false);
        ReaperCall("TickMantisDeathMolt",source,1f);
        Assert.That(Get(source,"alive"),Is.True);Assert.That(MoltState,Is.Null);
        Assert.That(Hp(source),Is.EqualTo(1500f));
    }

    [Test]
    public void DeathMoltPreservesAdultFormWithoutStackingTransformationBonuses()
    {
        ReaperReady();Set(source,"augmentTranscended",true);
        float scale=Model(source).transform.localScale.x;
        MoltDamage(target,source,99999f);var children=MoltChildren();
        Assert.That(Model(children[0]).transform.localScale.x,Is.EqualTo(scale*.55f).Within(.001f));
        foreach(var child in children)MoltDamage(target,child,99999f);
        ReaperCall("TickMantisDeathMolt",source,1f);
        Assert.That(Get(source,"rank3MantisReaper"),Is.True);
        Assert.That(Model(source).transform.localScale.x,Is.EqualTo(scale));
        Assert.That(Get(source,"skillCooldownDuration"),Is.EqualTo(6f));
        Assert.That(Get(source,"rank3AttackBonusRatio"),Is.EqualTo(.3f));
    }

    [Test]
    public void DeathMoltCombatResetRestoresHealthCapAndRemovesBrood()
    {
        MoltReady();MoltDamage(target,source,99999f);var children=MoltChildren();
        ReaperCall("CleanupMantisDeathMolt",source,false);
        Assert.That(MoltState,Is.Null);Assert.That(Get(source,"maxHp"),Is.EqualTo(5000f));
        Assert.That(children.All(child=>!(bool)Get(child,"alive")),Is.True);
    }

    [Test]
    public void DeathMoltInterruptsAnAdultFlightAndAllowsOneMoltInTheNextBattle()
    {
        ReaperReady();Set(source,"augmentTranscended",true);ReaperFly();ReaperTick(.2f);
        MoltDamage(target,source,99999f);
        Assert.That(Get(source,"mantisReaperAction"),Is.Null);
        Assert.That(Get(source,"rank3MantisReaper"),Is.True);
        Assert.That(Model(source).transform.position.y,Is.EqualTo(0f));
        MoltChildren();ReaperCall("ResetFamilyTranscendenceStates");
        Assert.That(MoltState,Is.Null);Assert.That(Get(source,"mantisMoltUsed"),Is.False);
        Set(source,"hp",0f);Assert.That(ReaperCall("TryBeginMantisDeathMolt",source,target),Is.True);
    }
}
