using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void OverheatSetup(string id="vivre",string intrinsic=null)
    {Trait(WyvernRank3Rules.DragonCore);WyvernSetup(id,intrinsic);}

    [Test]
    public void WyvernOverheatDealsSixIncreasingHitsAlongThePathAndIgnoresTwentyPercentArmor()
    {
        OverheatSetup();object rear=Unit(1,Vector3.right*6.5f),side=Unit(1,new Vector3(4,0,3)),ally=Unit(0,Vector3.right*3);
        Set(rear,"defense",100f);BreathActionTick(.732f);Assert.That(Hp(target),Is.EqualTo(5000));
        BreathActionTick(.002f);Assert.That(Hp(target),Is.EqualTo(4970).Within(.001f));
        float previous=4970f;float[] damage={40,50,60,80,100};
        foreach(float hit in damage){BreathActionTick(1f/3f);Assert.That(previous-Hp(target),Is.EqualTo(hit).Within(.001f));previous=Hp(target);}
        Assert.That(Hp(target),Is.EqualTo(4640).Within(.001f));
        Assert.That(Hp(rear),Is.EqualTo(4800).Within(.003f));
        Assert.That(Hp(side),Is.EqualTo(5000));Assert.That(Hp(ally),Is.EqualTo(5000));
        Assert.That(Get(source,"wyvernBreath"),Is.Null);BreathTick(3f);Assert.That(Hp(target),Is.EqualTo(4640).Within(.001f));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));
    }

    [Test]
    public void WyvernOverheatFollowsMovingTargetsAndRetargetsAfterDeathWithoutRestartingDamageRamp()
    {
        OverheatSetup();BreathActionTick(.74f);Assert.That(Hp(target),Is.EqualTo(4970));
        Model(target).transform.position=new Vector3(4,0,2f);BreathActionTick(.34f);Assert.That(Hp(target),Is.EqualTo(4930));
        object next=Unit(1,new Vector3(4,0,-2f));Set(target,"alive",false);BreathActionTick(.33f);
        Assert.That(Get(source,"target"),Is.SameAs(next));Assert.That(Hp(next),Is.EqualTo(4950));
        BreathActionTick(1f);Assert.That(Hp(next),Is.EqualTo(4710));
    }

    [Test]
    public void WyvernOverheatRetargetsWithinOneSlowFrameAndCanResumeWhenANewEnemyEntersRange()
    {
        OverheatSetup();Set(target,"hp",20f);object next=Unit(1,new Vector3(3,0,2.5f));
        BreathActionTick(1.4f);Assert.That(Hp(next),Is.EqualTo(4910));
        Set(next,"alive",false);BreathActionTick(.34f);
        object late=Unit(1,Vector3.right*4);BreathActionTick(.67f);Assert.That(Hp(late),Is.EqualTo(4820));
    }

    [Test]
    public void WyvernOverheatApproachAndCastUseFourTilesAndCannotStartTwice()
    {
        GriffinSetup("vivre");Trait(WyvernRank3Rules.DragonCore);var skill=GameMasterData.Skill(WyvernCombatRules.SkillId);
        float range=(float)GriffinCall("SkillActivationDistance",source,target,skill);
        Assert.That(range,Is.EqualTo(4f*MonsterAutoBattlePrototype.TileSize+.3f));
        Model(target).transform.position=Vector3.right*(range+.01f);Assert.That(GriffinCall("BeginWyvernBreath",source,skill),Is.False);
        Model(target).transform.position=Vector3.right*(range-.01f);Assert.That(GriffinCall("BeginWyvernBreath",source,skill),Is.True);
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.False);
    }

    [Test]
    public void WyvernOverheatPausesFreezesAndCancelsWithControlOrReset()
    {
        OverheatSetup();var statuses=(CombatStatusState)Get(source,"combatStatuses");
        statuses.ApplyTimed(CombatStatusKind.Freeze,3f,target);BreathTick(3f);Assert.That(Hp(target),Is.EqualTo(5000));statuses.Remove(CombatStatusKind.Freeze);
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,true);BreathTick(3f);Assert.That(Hp(target),Is.EqualTo(5000));
        typeof(MonsterAutoBattlePrototype).GetField("paused",Private).SetValue(battle,false);BreathActionTick(.74f);Assert.That(Hp(target),Is.EqualTo(4970));
        statuses.ApplyTimed(CombatStatusKind.Knockdown,1f,target);BreathActionTick(2f);Assert.That(Hp(target),Is.EqualTo(4640));
        Assert.That(Get(source,"wyvernBreath"),Is.Null);Assert.That(statuses.PersonalDebuffImmune,Is.False);
        statuses.Remove(CombatStatusKind.Knockdown);OverheatSetup();GriffinCall("ClearWyvernBreaths");BreathTick(3f);Assert.That(Hp(target),Is.EqualTo(4640));
    }

    [TestCase("vivre",WyvernCombatRules.VivreTraitId,CombatStatusKind.Poison,1)]
    [TestCase("frovire",WyvernCombatRules.FrovireTraitId,CombatStatusKind.ComboChilled,2)]
    [TestCase("igvire",WyvernCombatRules.IgvireTraitId,CombatStatusKind.Burning,1)]
    public void WyvernOverheatAppliesSpeciesEffectsOnlyOncePerEnemy(string id,string intrinsic,CombatStatusKind status,int stacks)
    {
        OverheatSetup(id,intrinsic);BreathActionTick(.74f);
        var statuses=(CombatStatusState)Get(target,"combatStatuses");statuses.Get(status).remaining=2f;
        BreathActionTick(1.66f);Assert.That(Hp(target),Is.EqualTo(4640).Within(.001f));
        Assert.That(statuses.Get(status).stacks,Is.EqualTo(stacks));Assert.That(statuses.Get(status).remaining,Is.EqualTo(2f));
        Assert.That(statuses.Has(CombatStatusKind.Freeze),Is.False);
    }

    [Test]
    public void WyvernOverheatStormChainsOnlyOncePerVictim()
    {
        OverheatSetup("temvire",WyvernCombatRules.TemvireTraitId);object other=Unit(1,new Vector3(4,0,4));
        BreathActionTick(2.4f);Assert.That(Hp(target),Is.EqualTo(4640));Assert.That(Hp(other),Is.EqualTo(4950));
    }

    [Test]
    public void WyvernOverheatParticlesGrowStrongerAndPersistForTheFullChannel()
    {
        var fx=PixelWyvernBreath.Create(null,Vector3.zero,Vector3.right,6.8f,1f,.4f,0,()=>0f,()=>true,true);
        var ps=fx.GetComponentsInChildren<ParticleSystem>().First(p=>p.name=="Breath Body");
        var particles=new ParticleSystem.Particle[144];fx.Pose(.8f,Vector3.right);int earlyCount=ps.GetParticles(particles);
        float early=particles.Take(earlyCount).Average(p=>p.startSize3D.x);
        fx.Pose(2.1f,Vector3.forward);Assert.That(fx!=null,Is.True);int lateCount=ps.GetParticles(particles);
        Assert.That(particles.Take(lateCount).Average(p=>p.startSize3D.x),Is.GreaterThan(early*1.3f));
        Assert.That(particles.Take(lateCount).Any(p=>p.startColor.a>0),Is.True);
        fx.Pose(2.4f,Vector3.forward);Assert.That(fx!=null,Is.True);fx.Pose(2.7f,Vector3.forward);Assert.That(fx==null,Is.True);
    }
}
