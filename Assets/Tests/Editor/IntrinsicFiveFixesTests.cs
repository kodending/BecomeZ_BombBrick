using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private CombatStatusState IntrinsicStatus(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private void IntrinsicSetup(string monster,string skill)
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private).SetValue(battle,
            new Bounds(Vector3.zero,new Vector3(40f,2f,40f)));
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+monster));
        Set(source,"skillId",skill);Model(target).transform.position=Vector3.right;
    }
    private void IntrinsicPulse(bool echo=false)=>MammothCall("ResolveEarthPulseImpact",source,
        GameMasterData.Skill(KingKongCombatRules.SkillId),1f,echo,-1,3f,CombatStatusKind.Airborne,0f,null,null,false);

    [Test]
    public void ChundongiAnimatedAttackWaitsForTheNormalHitFrame()
    {
        IntrinsicSetup("chundongi",NinjaCatCombatRules.SkillId);Trait(NinjaCatCombatRules.ChundongiTraitId);
        var body=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/037_chundongi"));
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();Set(source,"spriteAnimation",driver);
        Set(source,"ninjaEmpoweredBasicAttacks",2);MammothCall("PrepareNinjaOpeningStrike",source);
        MammothCall("Attack",source,target);Assert.That(Hp(target),Is.EqualTo(5000f));
        for(int i=0;i<100;i++)
        {
            typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{.02f});
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
        }
        Assert.That(Hp(target),Is.EqualTo(4856f).Within(.01f));
    }

    [TestCase(CombatStatusKind.Poison)]
    [TestCase(CombatStatusKind.ComboChilled)]
    public void IntrinsicContributorExpiresEvenWhenTheCasterRefreshesTheSameStatus(CombatStatusKind kind)
    {
        var ally=Unit(0,Vector3.left*20f);var status=IntrinsicStatus(target);
        status.ApplyStacks(kind,.2f,1,5,ally,0f,0f,true);
        status.ApplyStacks(kind,4f,1,5,source,0f,0f,true);
        status.Tick(.3f);
        Assert.That(status.Has(kind),Is.True);
        Assert.That(status.Get(kind).contributors.Any(donor=>donor==ally),Is.False);
        Assert.That(status.Get(kind).contributors.Any(donor=>donor==source),Is.True);
        status.RemoveAllHarmful();status.ApplyStacks(kind,4f,1,5,source,0f,0f,true);
        Assert.That(status.Get(kind).contributors.Any(donor=>donor==ally),Is.False);
    }

    [TestCase("")]
    [TestCase(NinjaCatRank3Rules.ShadowlessCross)]
    [TestCase(NinjaCatRank3Rules.BlackMoonHunt)]
    [TestCase(NinjaCatRank3Rules.EightGateFormation)]
    public void ChundongiEverySkillQueuesOneNormalRangeDoubleAttack(string rank3)
    {
        IntrinsicSetup("chundongi",NinjaCatCombatRules.SkillId);Trait(NinjaCatCombatRules.ChundongiTraitId);
        if(rank3.Length>0)Trait(rank3);
        Set(source,"cooldown",5f);Set(source,"ninjaHuntPending",true);Set(source,"ninjaHuntTarget",target);
        MammothCall("ResolveShadowHunt",source,GameMasterData.Skill(NinjaCatCombatRules.SkillId),1f,false,0);
        Assert.That(Get(source,"ninjaOpeningStrikeReady"),Is.True);
        Assert.That(Get(source,"cooldown"),Is.EqualTo(0f));
        Assert.That(Get(source,"ninjaEmpoweredBasicAttacks"),Is.EqualTo(2));
        // Bringing a target into range is a normal movement decision, not another teleport.
        Model(source).transform.position=Vector3.zero;Model(target).transform.position=Vector3.right;
        float before=Hp(target);
        MammothCall("Attack",source,target);
        Assert.That(before-Hp(target),Is.EqualTo(144f).Within(.01f));
        Assert.That(Get(source,"ninjaOpeningStrikeReady"),Is.False);
        Assert.That(Get(source,"ninjaEmpoweredBasicAttacks"),Is.EqualTo(0));
    }

    [Test]
    public void ChundongiRecastRefreshesReadinessWithoutStackingOrTeleporting()
    {
        IntrinsicSetup("chundongi",NinjaCatCombatRules.SkillId);Trait(NinjaCatCombatRules.ChundongiTraitId);
        Model(target).transform.position=Vector3.right*50f;
        MammothCall("PrepareNinjaOpeningStrike",source);MammothCall("PrepareNinjaOpeningStrike",source);
        Assert.That(Get(source,"ninjaOpeningStrikeReady"),Is.True);
        Assert.That(MammothCall("IsImmediatelyAttackable",source,target),Is.False);
        Assert.That(Model(source).transform.position,Is.EqualTo(Vector3.zero));Assert.That(Hp(target),Is.EqualTo(5000f));
        Model(target).transform.position=Vector3.right;
        MammothCall("Attack",source,target);Assert.That(Hp(target),Is.EqualTo(4880f).Within(.01f));
        MammothCall("Attack",source,target);Assert.That(Hp(target),Is.EqualTo(4780f).Within(.01f));
    }

    [TestCase("self",false)]
    [TestCase("ally",true)]
    [TestCase("mixed",true)]
    [TestCase("expired",false)]
    [TestCase("enemy",false)]
    public void BingareuConsumesOnlyAnActiveAlliedChillAndAddsTwentyPercent(string origin,bool combo)
    {
        IntrinsicSetup("bingareu",KingKongCombatRules.SkillId);Trait(KingKongCombatRules.BingareuTraitId);
        object donor=origin=="self"?source:Unit(origin=="enemy"?1:0,Vector3.left*20f);
        var status=IntrinsicStatus(target);
        status.ApplyStacks(CombatStatusKind.ComboChilled,origin=="expired"?.2f:4f,1,3,donor);
        if(origin=="expired")status.Tick(.3f);
        if(origin=="mixed")status.ApplyStacks(CombatStatusKind.ComboChilled,4f,1,3,source,0f,0f,true);
        var control=Unit(1,Vector3.forward);
        IntrinsicPulse();
        float baseDamage=5000f-Hp(control),damage=5000f-Hp(target);
        Assert.That(damage,Is.EqualTo(baseDamage*(combo?1.2f:1f)).Within(.01f));
        Assert.That(status.Has(CombatStatusKind.Freeze),Is.EqualTo(combo));
        if(combo)
        {
            Assert.That(status.Get(CombatStatusKind.Freeze).remaining,Is.EqualTo(1.25f));
            Assert.That(status.Has(CombatStatusKind.ComboChilled),Is.False);
            status.Tick(.25f);IntrinsicPulse(true);
            Assert.That(status.Get(CombatStatusKind.Freeze).remaining,Is.EqualTo(1f));
        }
    }

    [TestCase("control")]
    [TestCase("debuff")]
    [TestCase("resistance")]
    public void BingareuComboRespectsImmunityAndDurationResistance(string protection)
    {
        IntrinsicSetup("bingareu",KingKongCombatRules.SkillId);Trait(KingKongCombatRules.BingareuTraitId);
        var donor=Unit(0,Vector3.left*20f);var status=IntrinsicStatus(target);
        status.ApplyStacks(CombatStatusKind.ComboChilled,4f,1,3,donor);
        if(protection=="control")status.ControlImmune=true;
        if(protection=="debuff")status.DebuffImmune=true;
        if(protection=="resistance")status.harmfulDurationMultiplier=.5f;
        IntrinsicPulse();
        Assert.That(status.Has(CombatStatusKind.Freeze),Is.EqualTo(protection=="resistance"));
        if(protection=="resistance")Assert.That(status.Get(CombatStatusKind.Freeze).remaining,Is.EqualTo(.625f));
    }

    [TestCase("self_poison",25f)]
    [TestCase("ally_poison",40f)]
    [TestCase("mixed_poison",40f)]
    [TestCase("expired_poison",25f)]
    [TestCase("cleansed_poison",25f)]
    [TestCase("self_shred",25f)]
    [TestCase("ally_shred",40f)]
    [TestCase("mixed_shred",40f)]
    [TestCase("expired_shred",25f)]
    public void MagrilOnlyFirstTickUsesOtherAllysActivePoisonOrExposure(string origin,float first)
    {
        IntrinsicSetup("magril",KingKongCombatRules.SkillId);
        var ally=Unit(0,Vector3.left*20f);var status=IntrinsicStatus(target);
        var donor=origin.StartsWith("self")?source:ally;
        if(origin.EndsWith("poison"))
        {
            status.ApplyStacks(CombatStatusKind.Poison,4f,1,5,donor);
            if(origin.StartsWith("mixed"))status.ApplyStacks(CombatStatusKind.Poison,4f,1,5,source,0f,0f,true);
            if(origin.StartsWith("expired"))status.Tick(4.1f);
            if(origin.StartsWith("cleansed"))status.RemoveAllHarmful();
        }
        else
        {
            var apply=typeof(MonsterAutoBattlePrototype).GetMethod("ApplyDefenseShred",BindingFlags.Static|BindingFlags.NonPublic);
            apply.Invoke(null,new[]{donor,target,.2f,4f});
            if(origin.StartsWith("mixed"))apply.Invoke(null,new[]{source,target,.2f,6f});
            if(origin.StartsWith("expired"))((CombatEffectSources)Get(target,"defenseShredSources")).Tick(4.1f);
        }
        cast=(IEnumerator)MammothCall("ResolveLavaFissure",source,Vector3.zero,5f,0);
        Assert.That(cast.MoveNext(),Is.True);Assert.That(cast.MoveNext(),Is.True);
        Assert.That(Hp(target),Is.EqualTo(5000f-first).Within(.01f));
        Assert.That(cast.MoveNext(),Is.True);Assert.That(Hp(target),Is.EqualTo(5000f-first-25f).Within(.01f));
    }

    [TestCase("ally",true)]
    [TestCase("self",false)]
    [TestCase("crocodile",false)]
    [TestCase("depleted",false)]
    [TestCase("weak_self",true)]
    [TestCase("weak_ally",false)]
    [TestCase("blocked",false)]
    public void MoskaRalliesFullHealthRecipientsOnlyForTheirRemainingExternalShield(string origin,bool rally)
    {
        IntrinsicSetup("moska",KingKongCombatRules.SkillId);
        var ally=Unit(0,Vector3.forward);var status=IntrinsicStatus(ally);
        if(origin=="crocodile")Set(ally,"crocodileShield",100f);
        else
        {
            status.ApplyShield(6f,100f,origin=="self"||origin=="weak_ally"?ally:source);
            if(origin=="depleted")status.AbsorbShield(100f);
            if(origin=="weak_self")status.ApplyShield(6f,10f,ally);
            if(origin=="weak_ally")status.ApplyShield(6f,10f,source);
        }
        if(origin=="blocked")status.externalBenefitBlock=()=>true;
        MammothCall("ApplyLifeResonance",source,0f,5f);
        Assert.That(Get(ally,"kingKongRallyTimer"),Is.EqualTo(rally?4f:0f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));
        if(rally)
        {
            Set(ally,"kingKongRallyTimer",1f);MammothCall("ApplyLifeResonance",source,0f,5f);
            Assert.That(Get(ally,"kingKongRallyTimer"),Is.EqualTo(4f));
            Assert.That(status.Get(CombatStatusKind.ComboRally).stacks,Is.EqualTo(1));
        }
    }
}
