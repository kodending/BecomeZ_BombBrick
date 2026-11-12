using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private CombatStatusState GuardianStatus(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private void StartGuardian()
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_holydeer"));
        Set(source,"augmentTranscended",true);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        typeof(MonsterAutoBattlePrototype).GetField("battleStarted",Private).SetValue(battle,true);
        MammothCall("BeginSpiritGuardians");
    }

    [Test]
    public void SpiritDeerGuardianBlocksEveryHarmfulStatusButAllowsShieldAndEnemyDebuffs()
    {
        object ally=Unit(0,Vector3.right);StartGuardian();
        foreach(CombatStatusKind kind in Enum.GetValues(typeof(CombatStatusKind)))
        {
            if(kind==CombatStatusKind.Shield||kind==CombatStatusKind.ComboShielded||kind==CombatStatusKind.ComboRally)continue;
            GuardianStatus(ally).ApplyTimed(kind,10f,target,1f);
            GuardianStatus(ally).ApplyStacks(kind,10f,1,5,target,10f,1f,true);
            Assert.That(GuardianStatus(ally).Has(kind),Is.False,kind.ToString());
        }
        GuardianStatus(ally).ApplyShield(4f,100f,source);
        Assert.That(GuardianStatus(ally).Has(CombatStatusKind.Shield),Is.True);
        Assert.That(GuardianStatus(source).DebuffImmune,Is.True);
        GuardianStatus(target).ApplyTimed(CombatStatusKind.Root,2f,source);
        Assert.That(GuardianStatus(target).Has(CombatStatusKind.Root),Is.True);
    }

    [Test]
    public void SpiritDeerGuardianSurvivesCasterDeathAndCoversReinforcementsUntilBattleEnd()
    {
        var ally=Unit(0,Vector3.right);StartGuardian();Set(source,"alive",false);Set(source,"hp",0f);
        UnityEngine.Object.DestroyImmediate(Model(source));
        var reinforcement=Unit(0,Vector3.left);MammothCall("BindSpiritGuardianProtection",reinforcement);
        foreach(var u in new[]{ally,reinforcement})
        {
            GuardianStatus(u).DebuffImmune=true;GuardianStatus(u).DebuffImmune=false;GuardianStatus(u).Clear();
            Assert.That(GuardianStatus(u).DebuffImmune,Is.True);
        }
        typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).SetValue(battle,true);
        Assert.That(GuardianStatus(ally).DebuffImmune,Is.False);
        typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).SetValue(battle,false);
        MammothCall("ClearSpiritGuardians");
        Assert.That(GuardianStatus(reinforcement).DebuffImmune,Is.False);
    }

    [Test]
    public void SpiritDeerGuardianDoesNotPreventDamageOrLethalHits()
    {
        var ally=Unit(0,Vector3.right);StartGuardian();
        Assert.That(SpiritReturnDamage(target,ally,100f),Is.EqualTo(100f));
        Assert.That(Hp(ally),Is.EqualTo(4900f));
        Assert.That(SpiritReturnDamage(target,ally,9000f),Is.EqualTo(4900f));
        Assert.That(Get(ally,"alive"),Is.False);
    }

    [Test]
    public void SpiritDeerGuardianBlocksLegacyDebuffsAndForcedMovementWithoutInterruptingActions()
    {
        var ally=Unit(0,Vector3.right);StartGuardian();
        Set(ally,"skillTimer",2f);Set(ally,"skillCooldownDuration",10f);Set(ally,"velocity",Vector3.forward);
        MammothCall("DrainFrogCooldownCharge",target,ally);
        MammothCall("Rank3Shred",ally,.5f,5f,target);
        MammothCall("ApplyItemBurn",target,ally,.1f,4f,.3f);
        MammothCall("ApplySolarMark",target,ally);
        MammothCall("LockDeathRollTarget",target,ally,2f);
        Assert.That(Get(ally,"skillTimer"),Is.EqualTo(2f));
        Assert.That(Get(ally,"itemDefenseShredRatio"),Is.Zero);
        Assert.That(Get(ally,"itemBurnTimer"),Is.Zero);Assert.That(Get(ally,"solarBurnTimer"),Is.Zero);
        Assert.That(Get(ally,"crocodileImmobilizedTimer"),Is.Zero);
        Assert.That(Get(ally,"velocity"),Is.EqualTo(Vector3.forward));
        var immune=typeof(MonsterAutoBattlePrototype).GetMethod("PreyDisplacementImmune",BindingFlags.NonPublic|BindingFlags.Static);
        Assert.That(immune.Invoke(null,new[]{ally}),Is.True);
        Assert.That(MammothCall("BeginFrogHarpoonDisplacement",ally,Vector3.right,1f,2f,true,true),Is.False);
        Assert.That(Model(ally).transform.position,Is.EqualTo(Vector3.right));
    }

    [Test]
    public void SpiritDeerGuardianStartsOnceAndDoesNotTransferIntoPersonalImmunity()
    {
        StartGuardian();MammothCall("BeginSpiritGuardians");
        Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));
        Assert.That(GuardianStatus(source).PersonalDebuffImmune,Is.False);
        GuardianStatus(source).DebuffImmune=true;
        MammothCall("ClearSpiritGuardians");
        Assert.That(GuardianStatus(source).DebuffImmune,Is.True);
        GuardianStatus(source).DebuffImmune=false;
        Assert.That(GuardianStatus(source).DebuffImmune,Is.False);
    }

    [Test]
    public void SpiritDeerGuardianBothTeamsRemainIndependentAndBenchCastersCannotActivate()
    {
        StartGuardian();MammothCall("ClearSpiritGuardians");Set(source,"inStorage",true);
        MammothCall("BeginSpiritGuardians");Assert.That(GuardianStatus(source).DebuffImmune,Is.False);
        Set(source,"inStorage",false);Set(target,"definition",Get(source,"definition"));Set(target,"augmentTranscended",true);
        MammothCall("BeginSpiritGuardians");
        Assert.That(GuardianStatus(source).DebuffImmune,Is.True);
        Assert.That(GuardianStatus(target).DebuffImmune,Is.True);
    }

    [Test]
    public void SpiritDeerGuardianPreventsImperialHornAirborneButNotItsPhysicalDamage()
    {
        BeetleOverturnBoard();
        Set(target,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_holydeer"));
        Set(target,"augmentTranscended",true);
        typeof(MonsterAutoBattlePrototype).GetField("battleStarted",Private).SetValue(battle,true);
        MammothCall("BeginSpiritGuardians");BeginImperialHorn();AdvanceImperialHorn(.6f);
        Assert.That(GuardianStatus(target).DebuffImmune,Is.True);
        Assert.That(Get(target,"beetleOverturnedBy"),Is.Null);
        AdvanceImperialHorn(1f);Assert.That(Hp(target),Is.EqualTo(4700f).Within(.01f));
    }

    [Test]
    public void SpiritDeerGuardianEnglishTooltipIncludesTranslatedBattleStartCondition()
    {
        string language=GameLocalization.CurrentLanguage;
        try
        {
            GameLocalization.SetLanguage("en");
            var trait=MonsterAbilityTooltipContent.Trait("trait_transcend_spirit_deer_eternal_sanctuary");
            string tooltip=MonsterAbilityTooltipContent.TraitSummary(trait);
            Assert.That(tooltip,Does.Contain("At battle start"));
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(tooltip,"[가-힣]"),Is.False);
        }
        finally{GameLocalization.SetLanguage(language);}
    }
}

