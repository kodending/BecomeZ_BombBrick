using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class FamilyRank3TraitTests
{
    private static IEnumerable<TestCaseData> FamilyCases()
    {
        yield return Case("Monster_chundongi",NinjaCatRank3Rules.ShadowlessCross,
            NinjaCatRank3Rules.BlackMoonHunt,NinjaCatRank3Rules.EightGateFormation);
        yield return Case("Monster_tino",TyrannoRank3Rules.MassExtinction,
            TyrannoRank3Rules.TyrantMarch,TyrannoRank3Rules.PrimordialMaw);
        yield return Case("Monster_chiwoo",MonkeyRank3Rules.MountainCollapse,
            MonkeyRank3Rules.FiveOrbJuggling,MonkeyRank3Rules.MyriadReturn);
        yield return Case("Monster_mantius",MantisRank3Rules.DivineGuillotine,
            MantisRank3Rules.MyriadEyes,MantisRank3Rules.ReaperEmergence);
        yield return Case("Monster_versus",MushroomRank3Rules.SporeResonance,
            MushroomRank3Rules.ParasiticCrown,MushroomRank3Rules.MycokingDescent);
        yield return Case("Monster_kogason",MammothRank3Rules.MountainToss,
            MammothRank3Rules.WallRush,MammothRank3Rules.MountainBrace);
        yield return Case("Monster_jangpung",BeetleRank3Rules.MassTheft,
            BeetleRank3Rules.GoldenCoffin,BeetleRank3Rules.ZeroGravity);
        yield return Case("Monster_holydeer",SpiritDeerRank3Rules.LifeTithe,
            SpiritDeerRank3Rules.InviolableSanctuary,
            SpiritDeerRank3Rules.MyriadConcert);
        yield return Case("Monster_pone",GriffinRank3Rules.TyphoonDive,
            GriffinRank3Rules.RaptorHunt,GriffinRank3Rules.RoyalDescent);
        yield return Case("Monster_vivre",WyvernRank3Rules.CataclysmBreath,
            WyvernRank3Rules.WingSweep,WyvernRank3Rules.DragonCore);
        yield return Case("Monster_haeta",HaetaeRank3Rules.HeavenlyJudgment,
            HaetaeRank3Rules.DivineOppression,HaetaeRank3Rules.JudgmentBellow);
        yield return Case("Monster_aranea",ArachneRank3Rules.HeavenlyNet,
            ArachneRank3Rules.CocoonFuneral,ArachneRank3Rules.EightfoldWeb);
    }

    private static TestCaseData Case(string monster,params string[] traits)
    {
        return new TestCaseData(monster,traits).SetName(monster+"_Rank3TraitsAreSelectable");
    }

    [TestCaseSource(nameof(FamilyCases))]
    public void EveryRemainingFamilyTraitIsPresentAndTestSelectable(
        string monsterAsset,string[] traitIds)
    {
        MonsterDefinition monster=Resources.Load<MonsterDefinition>(
            "MonsterData/"+monsterAsset);
        Assert.That(monster,Is.Not.Null,monsterAsset);
        string[] selectable=MonsterTraitCatalog.TestSelectable(monster,true)
            .Select(trait=>trait.id).ToArray();
        CollectionAssert.IsSubsetOf(traitIds,selectable,monsterAsset);
        foreach(string traitId in traitIds)
        {
            TraitMasterRow row=GameMasterData.Database.traits.FirstOrDefault(
                trait=>trait!=null&&trait.id==traitId);
            Assert.That(row,Is.Not.Null,traitId);
            Assert.That(row.enabled,Is.True,traitId);
            Assert.That(row.dataVersion,Is.GreaterThanOrEqualTo(5),traitId);
            Assert.That(row.effectSummary,Is.Not.Empty,traitId);
        }
    }

    [Test]
    public void SporeResonanceDatabaseKeepsSavedIdWithNewNameAndRules()
    {
        TraitMasterRow row=GameMasterData.Database.traits.First(t=>t.id==MushroomRank3Rules.SporeResonance);
        Assert.That(row.displayName,Is.EqualTo("포자 공명"));
        Assert.That(row.dataVersion,Is.EqualTo(7));
        Assert.That(row.effectSummary,Does.Contain("0.7초"));
        Assert.That(row.effectSummary,Does.Contain("최대 6회"));
        Assert.That(row.effectSummary,Does.Not.Contain("끌어당"));
    }

    [Test]
    public void Rank3RuleConstantsMatchDestructiveDesignValues()
    {
        Assert.That(NinjaCatRank3Rules.ExecutionRatio,Is.EqualTo(2.2f));
        Assert.That(TyrannoRank3Rules.MarchDistanceTiles,Is.EqualTo(4f));
        Assert.That(MonkeyRank3Rules.FruitFinalRatio,Is.EqualTo(1.2f));
        Assert.That(MantisRank3Rules.GuillotineBladeRatio,Is.EqualTo(1.8f));
        Assert.That(MushroomRank3Rules.MycokingDuration,Is.EqualTo(8f));
        Assert.That(MammothRank3Rules.WallRushBlastTiles,Is.EqualTo(1.5f));
        Assert.That(BeetleRank3Rules.GravityDuration,Is.EqualTo(1.5f));
        Assert.That(SpiritDeerRank3Rules.LifeReturnRatio,Is.EqualTo(.35f));
        Assert.That(SpiritDeerRank3Rules.RadiusTiles,Is.EqualTo(2.5f));
        Assert.That(SpiritDeerCombatRules.ClampSanctuaryRadiusTiles(9f),
            Is.EqualTo(2.5f));
        Assert.That(SpiritDeerCombatRules.ClampSanctuaryRadiusTiles(2f),
            Is.EqualTo(2f));
        Assert.That(SpiritDeerRank3Rules.CapHitDamage(80f,100f,SpiritDeerRank3Rules.InviolableHitCap),
            Is.EqualTo(15f).Within(.001f));
        Assert.That(SpiritDeerRank3Rules.CapHitDamage(12f,100f,SpiritDeerRank3Rules.InviolableHitCap),
            Is.EqualTo(12f));
        Assert.That(SpiritDeerRank3Rules.ConcertSkillScale,Is.EqualTo(.5f));
        Assert.That(GriffinHuntRules.GrabRatio+GriffinHuntRules.SlamRatio,Is.EqualTo(3.6f).Within(.001f));
        Assert.That(WyvernRank3Rules.CoreDefenseIgnore,Is.EqualTo(.2f));
        Assert.That(HaetaeRank3Rules.BellowRatio,Is.EqualTo(2.8f));
        Assert.That(ArachneRank3Rules.HeavenlyRadiusTiles,Is.EqualTo(2.5f));
        Assert.That(ArachneRank3Rules.EightfoldFinalRatio,Is.EqualTo(2.4f));
    }
}
