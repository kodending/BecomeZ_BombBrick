using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void DevourStart()
    {
        HaetaeSetup();Set(source,"augmentTranscended",true);
        typeof(MonsterAutoBattlePrototype).GetField("battleStarted",Private).SetValue(battle,true);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        MammothCall("BeginHaetaeDevourers");
    }
    private object DevourStatic(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype)
        .GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);

    [Test]
    public void HaetaeDevourBlocksHealingOverflowAndAllStatusBenefitsBeforeApplication()
    {
        DevourStart();Set(target,"hp",1000f);
        Assert.That(MammothCall("Heal",target,10000f),Is.EqualTo(0f));Assert.That(Hp(target),Is.EqualTo(1000f));
        Assert.That(Get(target,"healingReceived"),Is.EqualTo(0f));
        var status=GuardianStatus(target);
        Assert.That(status.ApplyShield(5f,900f,source).magnitude,Is.Zero);
        foreach(var kind in new[]{CombatStatusKind.Shield,CombatStatusKind.ComboRally,CombatStatusKind.ComboShielded})
        {
            status.ApplyTimed(kind,5f,source,1f);status.ApplyStacks(kind,5f,1,2,source,1f);
            Assert.That(status.Has(kind),Is.False,kind.ToString());
        }
        status.ApplyDebuffImmunity(3f);Assert.That(status.DebuffImmune,Is.False);
        status.ApplyTimed(CombatStatusKind.Poison,3f,source);Assert.That(status.Has(CombatStatusKind.Poison),Is.True);
        Set(source,"hp",1000f);MammothCall("Heal",source,100f);Assert.That(Hp(source),Is.EqualTo(1100f));
        GuardianStatus(source).ApplyShield(3f,100f);Assert.That(GuardianStatus(source).Has(CombatStatusKind.Shield),Is.True);
    }
    [Test]
    public void HaetaeDevourBlocksRealItemAndForestSupportGrantsButKeepsBaseStats()
    {
        DevourStart();Set(target,"hp",1000f);Set(target,"defense",45f);Set(target,"speedTilesPerSecond",2f);
        var items=(CombatItemModifiers)Get(target,"itemModifiers");
        items.specialEffects.Add(new ItemMasterRow{specialEffect="PostSkillShield",effectParam1=.5f,effectParam2=4f});
        items.specialEffects.Add(new ItemMasterRow{specialEffect="PostSkillAttackSpeed",effectParam1=.5f,effectParam2=4f});
        items.specialEffects.Add(new ItemMasterRow{specialEffect="PostSkillDefenseResistance",effectParam1=100f,effectParam2=80f,effectParam3=4f});
        MammothCall("ApplyPostSkillItemEffects",target);
        Assert.That(Get(target,"itemShield"),Is.EqualTo(0f));Assert.That(Get(target,"itemPostSkillStatTimer"),Is.EqualTo(0f));
        Assert.That(MammothCall("CurrentAttackInterval",target),Is.EqualTo(1f));Assert.That(MammothCall("CurrentDefense",target),Is.EqualTo(45f));
        var deer=Unit(1,Vector3.right);
        ((IList)Get(deer,"activeTraitIds")).Add(SpiritDeerCombatRules.ForestdeerTraitId);
        MammothCall("ApplySpiritDeerSanctuaryBlessing",deer,target,1f);
        Assert.That(Hp(target),Is.EqualTo(1000f));Assert.That(Get(target,"familyAttackSpeedTimer"),Is.EqualTo(0f));
        Assert.That(Get(target,"familyMoveSpeedTimer"),Is.EqualTo(0f));
        Assert.That(MammothCall("CurrentMovementSpeed",target),Is.EqualTo(3.4f).Within(.001f));
    }
    [Test]
    public void HaetaeDevourRemovesOpeningBenefitsAndDoesNotRestoreConsumedBenefitsOnDeath()
    {
        Set(target,"itemShield",900f);Set(target,"rank3AttackBonusTimer",5f);Set(target,"rank3AttackBonusRatio",.5f);
        GuardianStatus(target).ApplyShield(4f,300f);GuardianStatus(target).ApplyTimed(CombatStatusKind.ComboRally,5f,null,.5f);
        DevourStart();Assert.That(DevourStatic("TotalVisibleShield",target),Is.EqualTo(0f));
        Assert.That(Get(target,"rank3AttackBonusTimer"),Is.EqualTo(0f));
        Assert.That(GuardianStatus(target).Has(CombatStatusKind.ComboRally),Is.False);
        Set(source,"hp",0f);Assert.That(GuardianStatus(target).BenefitsBlocked,Is.False);
        Assert.That(DevourStatic("TotalVisibleShield",target),Is.EqualTo(0f));
        GuardianStatus(target).ApplyShield(2f,200f);Assert.That(DevourStatic("TotalVisibleShield",target),Is.EqualTo(200f));
    }
    [Test]
    public void HaetaeDevourStacksByLivingCastersAndCoversReinforcementsWithoutDebuffInteraction()
    {
        DevourStart();var second=Unit(0,Vector3.left);Set(second,"definition",Get(source,"definition"));Set(second,"augmentTranscended",true);
        MammothCall("BeginHaetaeDevourers");Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));
        var added=Unit(1,Vector3.forward);MammothCall("BindSpiritGuardianProtection",added);
        GuardianStatus(added).externalDebuffImmunity=()=>true;
        Assert.That(GuardianStatus(added).RejectHarmfulEffect(),Is.True);
        Assert.That(GuardianStatus(added).RejectBenefit(),Is.True,"Guardian immunity cannot cleanse a rule on receiving support.");
        Set(source,"hp",0f);Assert.That(GuardianStatus(added).BenefitsBlocked,Is.True);
        Set(second,"hp",0f);Assert.That(GuardianStatus(added).BenefitsBlocked,Is.False);
        Set(second,"hp",100f);Assert.That(GuardianStatus(added).BenefitsBlocked,Is.True);
        MammothCall("ClearHaetaeDevourers");Assert.That(GuardianStatus(added).BenefitsBlocked,Is.False);
    }
    [Test]
    public void HaetaeDevourDoesNotActivateFromBenchOrClonesAndBothTeamsCanSuppressEachOther()
    {
        DevourStart();MammothCall("ClearHaetaeDevourers");Set(source,"inStorage",true);
        MammothCall("BeginHaetaeDevourers");Assert.That(GuardianStatus(target).BenefitsBlocked,Is.False);
        Set(source,"inStorage",false);Set(source,"augmentShadowClone",true);
        MammothCall("BeginHaetaeDevourers");Assert.That(GuardianStatus(target).BenefitsBlocked,Is.False);
        Set(source,"augmentShadowClone",false);Set(target,"definition",Get(source,"definition"));Set(target,"augmentTranscended",true);
        MammothCall("BeginHaetaeDevourers");Assert.That(GuardianStatus(target).BenefitsBlocked,Is.True);
        Assert.That(GuardianStatus(source).BenefitsBlocked,Is.True);
        typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).SetValue(battle,true);
        Assert.That(GuardianStatus(target).BenefitsBlocked,Is.False);
    }
    [Test]
    public void HaetaeDevourKeepsRankThreeSkillAndRemovesOldAutomaticEcho()
    {
        DevourStart();Trait(HaetaeRank3Rules.JudgmentBellow);
        Assert.That(MammothCall("BeginHaetaeJusticeRoar",source,GameMasterData.Skill(HaetaeCombatRules.SkillId)),Is.True);
        RoarActionTick(.8f);Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        int before=(int)Get(source,"traitActivations");MammothCall("TriggerFamilyTranscendenceSkill",source);
        MammothCall("UpdateFamilyTranscendence",source,6f);
        Assert.That(Get(source,"traitActivations"),Is.EqualTo(before));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
    }
    [Test]
    public void HaetaeDevourConsumedBearBuffDoesNotReturnThroughTimerDerivedHudIcons()
    {
        DevourStart();Set(target,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_ironkuma"));
        Set(target,"skillId",BearCombatRules.SkillId);Set(target,"defense",45f);
        ((IList)Get(target,"activeTraitIds")).Add(BearCombatRules.IronKumaTraitId);
        var skill=GameMasterData.Skill(BearCombatRules.SkillId);
        object generation=typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle);
        var icons=(IList)typeof(MonsterAutoBattlePrototype).GetField("combatHudIcons",Private).GetValue(battle);
        Func<string[]> collect=()=>
        {
            MammothCall("CollectCombatHudIcons",target);
            return icons.Cast<object>().Select(entry=>(entry.GetType().GetField("status").GetValue(entry)??
                entry.GetType().GetField("effect").GetValue(entry)).ToString()).ToArray();
        };
        Assert.That(MammothCall("ApplyBearFightingSpirit",target,skill,1f,false,generation),Is.True);
        Assert.That((float)Get(target,"bearResolveTimer"),Is.GreaterThan(0f),"Keep the skill action running.");
        Assert.That(MammothCall("CurrentDefense",target),Is.EqualTo(45f));
        Assert.That(MammothCall("CurrentAttackInterval",target),Is.EqualTo(1f));
        Assert.That(collect(),Does.Not.Contain("AttackUp"));
        GuardianStatus(target).ApplyTimed(CombatStatusKind.Burning,4f,source);
        GuardianStatus(target).ControlImmune=true;
        Assert.That(collect(),Is.EquivalentTo(new[]{"Burning","ControlImmunity"}),
            "Only the consumed buff disappears; live debuffs and action immunity remain visible.");
        Set(target,"bearResolveTimer",0f);Set(source,"hp",0f);
        Assert.That(MammothCall("ApplyBearFightingSpirit",target,skill,1f,false,generation),Is.True);
        Assert.That(collect(),Does.Contain("AttackUp"),"A new successful buff can show after Haetae dies.");
        Assert.That((float)MammothCall("CurrentDefense",target),Is.GreaterThan(45f));
        Set(target,"bearResolveTimer",0f);Assert.That(collect(),Does.Not.Contain("AttackUp"));
    }
    [Test]
    public void HaetaeDevourAnimationAndAbsorptionFollowCombatClockAndCleanUp()
    {
        var reference=new GameObject("Devourer Reference").AddComponent<SpriteRenderer>();float clock=0f;bool valid=true;
        int swallowed=0;
        var fx=PixelHaetaeDevourer.Create(reference,new Bounds(Vector3.zero,new Vector3(20,1,12)),true,()=>clock,()=>valid,
            point=>{swallowed++;});
        Assert.That(fx,Is.Not.Null);clock=1f;
        var late=typeof(PixelHaetaeDevourer).GetMethod("LateUpdate",Private);late.Invoke(fx,null);
        fx.Absorb(Vector3.zero,CombatBenefitKind.Healing);clock=1.35f;late.Invoke(fx,null);
        var body=fx.GetComponentInChildren<SpriteRenderer>();var pose=body.sprite;
        var ps=fx.transform.Find("Devoured Support Streams").GetComponent<ParticleSystem>();
        var a=new ParticleSystem.Particle[1536];var b=new ParticleSystem.Particle[1536];int count=ps.GetParticles(a);
        Assert.That(count,Is.GreaterThan(0));late.Invoke(fx,null);Assert.That(body.sprite,Is.SameAs(pose));
        Assert.That(ps.GetParticles(b),Is.EqualTo(count));for(int i=0;i<count;i++)Assert.That(a[i].position,Is.EqualTo(b[i].position));
        Assert.That(body.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(swallowed,Is.Zero,"No swallow before the streams arrive.");
        clock=2f;late.Invoke(fx,null);late.Invoke(fx,null);
        Assert.That(swallowed,Is.EqualTo(1),"One callback for the completed feeding batch.");
        fx.Absorb(Vector3.zero,CombatBenefitKind.Shield);
        valid=false;late.Invoke(fx,null);clock+=.7f;late.Invoke(fx,null);Assert.That(fx==null,Is.True);
        Assert.That(swallowed,Is.EqualTo(1),"A disappearing beast must not swallow a pending packet.");
    }
}
