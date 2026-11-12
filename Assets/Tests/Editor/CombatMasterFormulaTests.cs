using System.Linq;
using BombBrick.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatMasterFormulaTests
{
    [Test]
    public void MonsterMaster_ResetRosterHasIndividualDefenseAndResistance()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        MonsterDefinition[] active=database.monsters
            .Where(monster=>monster!=null&&monster.enabled&&
                int.TryParse(monster.serialNumber,out int serial)&&serial>=1&&serial<=4)
            .ToArray();
        Assert.That(active,Has.Length.EqualTo(4));
        Assert.That(active.All(monster=>monster.defense>0f),Is.True);
        Assert.That(active.All(monster=>monster.statusResistance>0f),Is.True);

        MonsterDefinition velos=active.Single(monster=>monster.serialNumber=="001");
        Assert.That(velos.maxHp,Is.EqualTo(108f));
        Assert.That(velos.attackInterval,Is.EqualTo(1f/1.42f).Within(0.0001f));
        Assert.That(velos.defense,Is.EqualTo(11f));
        Assert.That(velos.statusResistance,Is.EqualTo(12f));
        Assert.That(velos.basicAttackId,Is.EqualTo("basic_raptor_bite"));
    }

    [Test]
    public void SkillFormula_UsesPrimarySecondaryAndCasterHealthScaling()
    {
        SkillMasterRow frog=new SkillMasterRow
        {
            damageMultiplier=1.1f,
            secondaryDamageMultiplier=0.7f
        };
        Assert.That(SkillDamageFormulaResolver.Resolve(
            frog,20f,100f,100f,100f).physicalDamage,Is.EqualTo(22f).Within(0.001f));
        Assert.That(SkillDamageFormulaResolver.Resolve(
            frog,20f,100f,100f,100f,true).physicalDamage,Is.EqualTo(14f).Within(0.001f));

        SkillMasterRow kingKong=new SkillMasterRow
        {
            damageMultiplier=1.1f,
            casterMaxHpRatio=0.05f
        };
        Assert.That(SkillDamageFormulaResolver.Resolve(
            kingKong,20f,100f,100f,100f).physicalDamage,Is.EqualTo(27f).Within(0.001f));
    }

    [Test]
    public void BattlePacing_IncreasesDurabilityAndPreservesAttackSpeedRatios()
    {
        Assert.That(CombatStatFormula.BattleHealth(100f),Is.EqualTo(300f));
        Assert.That(CombatStatFormula.BattleDefense(20f),Is.EqualTo(40f));
        Assert.That(CombatStatFormula.BattleAttackInterval(1f),Is.EqualTo(1.08f));
        Assert.That(
            CombatStatFormula.BattleAttackInterval(.5f)/
            CombatStatFormula.BattleAttackInterval(1f),
            Is.EqualTo(.5f).Within(.0001f));
        Assert.That(CombatStatFormula.BattleInitialSkillCooldown(8f),
            Is.EqualTo(8f));
        Assert.That(CombatStatFormula.CooldownAfterBasicAttack(8f,8f),
            Is.EqualTo(7.2f).Within(.0001f));
        Assert.That(CombatStatFormula.CooldownAfterDamageTaken(
                8f,8f,18f,180f),
            Is.EqualTo(7.76f).Within(.0001f));
        Assert.That(ExpeditionAugmentRuntime.ChargeSkillCooldown(
                CombatStatFormula.BattleInitialSkillCooldown(8f),8f,.3f),
            Is.EqualTo(5.6f).Within(.0001f));
        Assert.That(CombatStatFormula.BattleOvertimeDamageMultiplier(37.9f),
            Is.EqualTo(1f));
        Assert.That(CombatStatFormula.BattleOvertimeDamageMultiplier(38f),
            Is.EqualTo(1.1f).Within(.0001f));
        Assert.That(CombatStatFormula.BattleOvertimeDamageMultiplier(42f),
            Is.EqualTo(1.3f).Within(.0001f));
    }

    [Test]
    public void BattleHealthPacingPreservesDirectCasterHealthDamage()
    {
        var skill=GameMasterData.Skill("kingkong_earth_pulse");
        var damage=SkillDamageFormulaResolver.Resolve(skill,0f,
            CombatStatFormula.BattleHealth(100f),0f,0f);
        Assert.That(damage.physicalDamage,Is.EqualTo(7.5f).Within(.0001f));
    }

    [Test]
    public void SkillFormula_CapsHealthScalingAndSeparatesTrueDamage()
    {
        SkillMasterRow crocodile=new SkillMasterRow
        {
            damageMultiplier=1.25f,
            targetMissingHpRatio=0.1f,
            hpScalingCapAttackMultiplier=1f,
            hpScalingTrueDamage=true
        };
        SkillDamageFormulaResult result=SkillDamageFormulaResolver.Resolve(
            crocodile,20f,100f,100f,1000f);
        Assert.That(result.physicalDamage,Is.EqualTo(25f).Within(0.001f));
        Assert.That(result.trueDamage,Is.EqualTo(20f).Within(0.001f));
    }

    [Test]
    public void SkillFormula_StackDamageAndStatusResistanceUseApprovedCurves()
    {
        SkillMasterRow spider=new SkillMasterRow
        {
            damageMultiplier=0.55f,
            damagePerStackMultiplier=0.15f
        };
        Assert.That(SkillDamageFormulaResolver.Resolve(
            spider,20f,100f,100f,100f,false,3).physicalDamage,
            Is.EqualTo(20f).Within(0.001f));
        Assert.That(CombatStatusResistance.Apply(1f,20f),
            Is.EqualTo(0.833333f).Within(0.0001f));
    }

    [Test]
    public void DefenseIgnore_IncreasesPhysicalDamageWithoutChangingTrueDamage()
    {
        DamagePacket normal=DamagePacket.Physical(
            100f,DamageSourceId.Skill,DamageTags.Attack);
        DamagePacket penetrating=normal;
        penetrating.armorPenetration=0.35f;
        CombatDefense defense=new CombatDefense
        {
            armor=20f,
            moreDamageTakenMultiplier=1f
        };
        Assert.That(CombatDamageResolver.Resolve(penetrating,defense).total,
            Is.GreaterThan(CombatDamageResolver.Resolve(normal,defense).total));
    }

    [Test]
    public void ArmorUsesAutoChessPercentageReductionCurve()
    {
        DamagePacket hit=DamagePacket.Physical(
            100f,DamageSourceId.BasicAttack,DamageTags.Attack);
        Assert.That(CombatDamageResolver.Resolve(hit,new CombatDefense
            {armor=20f,moreDamageTakenMultiplier=1f}).total,
            Is.EqualTo(83.3333f).Within(0.001f));
        Assert.That(CombatDamageResolver.Resolve(hit,new CombatDefense
            {armor=100f,moreDamageTakenMultiplier=1f}).total,
            Is.EqualTo(50f).Within(0.001f));
    }

    [Test]
    public void SharedCombatStatsUseAutoBattlerCapsAndNonStacking()
    {
        Assert.That(CombatStatFormula.AttackInterval(0.5f,2f,0f),
            Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(CombatStatFormula.AttackInterval(1f,1f,0.5f),
            Is.EqualTo(1f).Within(0.001f));
        Assert.That(CombatStatFormula.CriticalChance(1.4f),Is.EqualTo(1f));
        Assert.That(CombatStatFormula.CriticalDamageMultiplier(1.4f,0.3f),
            Is.EqualTo(2f).Within(0.001f));
        Assert.That(CombatStatFormula.Cooldown(10f,0.25f),Is.EqualTo(8f));
        Assert.That(CombatStatFormula.Strongest(0.33f,0.5f),Is.EqualTo(0.5f));

        DamagePacket hit=DamagePacket.Physical(
            100f,DamageSourceId.Skill,DamageTags.Attack);
        hit.trueDamage=40f;
        DamageResult result=CombatDamageResolver.Resolve(hit,new CombatDefense
        {
            armor=100f,
            increasedDamageTaken=1f,
            moreDamageTakenMultiplier=1f
        });
        Assert.That(result.physical,Is.EqualTo(100f).Within(0.001f));
        Assert.That(result.trueDamage,Is.EqualTo(40f).Within(0.001f));
    }

    [Test]
    public void TrueDamageUsesAttackerAmplificationButIgnoresTargetMitigation()
    {
        DamagePacket hit=DamagePacket.Physical(
            100f,DamageSourceId.Skill,DamageTags.Attack);
        hit.trueDamage=40f;
        hit=hit.Scaled(1.5f);
        hit.physical*=0.5f;

        DamageResult result=CombatDamageResolver.Resolve(hit,new CombatDefense
        {
            armor=100f,
            moreDamageTakenMultiplier=1f
        });

        Assert.That(result.physical,Is.EqualTo(37.5f).Within(0.001f));
        Assert.That(result.trueDamage,Is.EqualTo(60f).Within(0.001f));
    }

    [Test]
    public void SabertoothFangLock_DealsThreeEqualHitsAndScalesFromTargetHealth()
    {
        Assert.That(Resources.Load<MonsterDefinition>("MonsterData/Monster_geomchi")
            .basicAttackId,Is.EqualTo("basic_sabertooth_claw_slash"));
        SkillDamageFormulaResult hit=SabertoothCombatRules.DamagePerHit(30f,1.8f,200f);
        Assert.That(hit.physicalDamage,Is.EqualTo(18f).Within(0.001f));
        Assert.That(hit.trueDamage,Is.EqualTo(40f/3f).Within(0.001f));
        Assert.That(hit.Total*SabertoothCombatRules.HitCount,
            Is.EqualTo(94f).Within(0.001f));
        Assert.That(GameMasterData.Skill(SabertoothCombatRules.SkillId).damageFormulaNote,
            Does.Contain("MaxHP×20% 고정 피해"));
        Assert.That(GameMasterData.Skill(SabertoothCombatRules.SkillId).hpScalingTrueDamage,Is.True);
        Assert.That(SabertoothCombatRules.PredatoryRecovery(100f),
            Is.EqualTo(35f));
        Assert.That(SabertoothCombatRules.StoneShield(50f),
            Is.EqualTo(30f).Within(.001f));
        Assert.That(SabertoothCombatRules.FrostDuration,Is.EqualTo(4f));
        Assert.That(SabertoothCombatRules.ExecutionFangEffectScale,
            Is.EqualTo(1.5f));
    }

    [Test]
    public void SabertoothRank3Traits_SilencePreyDriveAndBloodReflux()
    {
        Assert.That(SabertoothCombatRules.SilenceAfterReleaseDuration,Is.EqualTo(1.5f));
        Assert.That(GameMasterData.Database.traits.Find(
                trait=>trait.id==SabertoothCombatRules.PreyDriveTraitId).displayName,
            Is.EqualTo("먹잇감 몰이"));
        Assert.That(SabertoothCombatRules.BloodRefluxRatio,Is.EqualTo(0.4f));
        Assert.That(GameMasterData.Database.traits.Find(
                trait=>trait.id==SabertoothCombatRules.BloodRefluxTraitId).displayName,
            Is.EqualTo("피의 역류"));

        CombatStatusState statuses=new CombatStatusState();
        statuses.ApplyTimed(CombatStatusKind.Poison,2f);
        statuses.ControlImmune=true;
        statuses.ApplyTimed(CombatStatusKind.Root,2f);
        statuses.Tick(1f,null,true);
        Assert.That(statuses.Has(CombatStatusKind.Root),Is.False);
        Assert.That(statuses.Get(CombatStatusKind.Poison).remaining,
            Is.EqualTo(2f));
    }

    [Test]
    public void FrogTongueHarpoon_UsesApprovedDamageAndRecoveryRatios()
    {
        float damage=20f*(1.3f+0.5f);
        Assert.That(damage,Is.EqualTo(36f));
        Assert.That(FrogCombatRules.SoulHealing(damage),
            Is.EqualTo(10.8f).Within(0.001f));
        Assert.That(FrogCombatRules.PullDistanceTiles,Is.EqualTo(0.75f));
        Assert.That(FrogCombatRules.RecoilDistanceTiles,Is.EqualTo(1.5f));
        Assert.That(FrogCombatRules.ReflexSnatchTraitId,
            Is.EqualTo("trait_frog_reflex_snatch"));
        Assert.That(FrogCombatRules.BuffThiefTraitId,
            Is.EqualTo("trait_frog_buff_thief"));
        Assert.That(FrogCombatRules.StolenShield(100f,30f,false),
            Is.EqualTo(12f));
        Assert.That(FrogCombatRules.StolenShield(100f,30f,true),
            Is.EqualTo(6f));
        Assert.That(FrogCombatRules.StolenMagnitude(0.2f,true),
            Is.EqualTo(0.1f));
        Assert.That(FrogCombatRules.TongueSlamTraitId,
            Is.EqualTo("trait_frog_hunting_chorus"));
    }

    [Test]
    public void CrocodileDeathRoll_UsesApprovedThreeHitFormulaAndTraitRatios()
    {
        Assert.That(Resources.Load<MonsterDefinition>("MonsterData/Monster_ager")
            .basicAttackId,Is.EqualTo("basic_crocodile_tail_strike"));
        float damage=CrocodileCombatRules.HitDamage(30f,0,100f)+
                     CrocodileCombatRules.HitDamage(30f,1,100f)+
                     CrocodileCombatRules.HitDamage(30f,2,100f);
        Assert.That(damage,Is.EqualTo(116f).Within(0.001f));
        Assert.That(CrocodileCombatRules.GoldenFeastHealing(damage),
            Is.EqualTo(40.6f).Within(0.001f));
        Assert.That(CrocodileCombatRules.CooldownAfterMarkedDeath(6f),
            Is.EqualTo(3f));
        Assert.That(CrocodileCombatRules.PreyCannonDamageRatio,Is.EqualTo(1.2f));
        Assert.That(CrocodileCombatRules.PreyCannonRadiusTiles,Is.EqualTo(1f));
        Assert.That(CrocodileCombatRules.PreyCannonDistanceTiles,Is.EqualTo(2f));
        Assert.That(CrocodileCombatRules.PreyCannonKnockdownDuration,Is.EqualTo(0.6f));
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_ager"),true)
            .Any(trait=>trait.id==CrocodileCombatRules.PreyCannonTraitId),Is.True);
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_ager"),true)
            .Any(trait=>trait.id==CrocodileCombatRules.CrushingRollTraitId),Is.True);
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_ager"),true)
            .Any(trait=>trait.id==CrocodileCombatRules.AbsoluteJawTraitId),Is.True);
    }

    [Test]
    public void KingKongEarthPulse_UsesApprovedComboAndTraitRatios()
    {
        float baseDamage=30f*1.1f+200f*0.05f;
        Assert.That(baseDamage,Is.EqualTo(43f));
        Assert.That(baseDamage*KingKongCombatRules.ComboDamageMultiplier,
            Is.EqualTo(53.75f));
        Assert.That(200f*KingKongCombatRules.LilasShieldPerHitRatio*3f,
            Is.EqualTo(18f));
        Assert.That(30f*KingKongCombatRules.LavaTickDamageRatio*
                    (1f+KingKongCombatRules.LavaComboBonus),
            Is.EqualTo(12f).Within(0.001f));
        Assert.That(KingKongCombatRules.GlacialChillStacksToFreeze,Is.EqualTo(3));
        Assert.That(KingKongCombatRules.GlacialFreezeDuration,Is.EqualTo(1f));
        Assert.That(baseDamage*KingKongCombatRules.LifeHealingRatio,
            Is.EqualTo(10.75f));
        Assert.That(KingKongCombatRules.TriplePulseFollowupDelay,Is.EqualTo(0.44f));
        Assert.That(KingKongCombatRules.TriplePulseFollowupRadiusTiles,Is.EqualTo(2.7f));
        Assert.That(baseDamage*KingKongCombatRules.TriplePulseFollowupDamageRatio,
            Is.EqualTo(30.1f).Within(0.001f));
        Assert.That(KingKongCombatRules.TriplePulseFinalDelay,Is.EqualTo(0.51f));
        Assert.That(KingKongCombatRules.TriplePulseFinalRadiusTiles,Is.EqualTo(3.2f));
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_lilas"),true)
            .Any(trait=>trait.id==KingKongCombatRules.TripleEarthPulseTraitId),Is.True);
        Assert.That(baseDamage*KingKongCombatRules.ContinentalFissureDamageRatio,
            Is.EqualTo(51.6f).Within(0.001f));
        Assert.That(KingKongCombatRules.InsideContinentalFissure(
            new Vector3(3f,0f,4f),5f),Is.True);
        Assert.That(KingKongCombatRules.InsideContinentalFissure(
            new Vector3(-4f,0f,-3f),5f),Is.True);
        Assert.That(KingKongCombatRules.InsideContinentalFissure(
            new Vector3(0f,0f,5.1f),5f),Is.False);
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_lilas"),true)
            .Any(trait=>trait.id==KingKongCombatRules.ContinentalFissureTraitId),Is.True);
        Assert.That(baseDamage*KingKongCombatRules.WorldOverturnDamageRatio,
            Is.EqualTo(86f).Within(0.001f));
        Assert.That(KingKongCombatRules.WorldOverturnKnockdownDuration,Is.EqualTo(1f));
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_lilas"),true)
            .Any(trait=>trait.id==KingKongCombatRules.WorldOverturnTraitId),Is.True);
    }

    [Test]
    public void WizardMouseBarrage_UsesApprovedProjectileAndTraitRatios()
    {
        Assert.That(WizardMouseCombatRules.ProjectileDamage(
            20f,0.42f,false,0),Is.EqualTo(8.4f).Within(0.001f));
        Assert.That(WizardMouseCombatRules.ProjectileDamage(
            20f,0.42f,false,4),Is.EqualTo(8.4f).Within(0.001f));
        Assert.That(WizardMouseCombatRules.ProjectileDamage(
            20f,0.42f,true,4),Is.EqualTo(11.76f).Within(0.001f));
        Assert.That(WizardMouseCombatRules.GratefulHealing(20f,4),
            Is.EqualTo(20f*1.2f).Within(0.001f));
        Assert.That(WizardMouseCombatRules.FrostStacksToFreeze,Is.EqualTo(3));
        Assert.That(WizardMouseCombatRules.FreezeDuration,Is.EqualTo(1f));
        Assert.That(WizardMouseCombatRules.MilkyWayFragmentCount,Is.EqualTo(2));
        Assert.That(WizardMouseCombatRules.MilkyWayFragmentRadiusTiles,Is.EqualTo(3f));
        Assert.That(WizardMouseCombatRules.MilkyWayFragmentDamage(100f),
            Is.EqualTo(42f));
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_kongjwi"),true)
            .Any(trait=>trait.id==WizardMouseCombatRules.MilkyWayBarrageTraitId),Is.True);
        Assert.That(WizardMouseCombatRules.PentagramPulseDamage(100f),Is.EqualTo(100f));
        Assert.That(WizardMouseCombatRules.PentagramRadiusTiles,Is.EqualTo(2.2f));
        Assert.That(WizardMouseCombatRules.PentagramPulseCount,Is.EqualTo(3));
        Assert.That(WizardMouseCombatRules.PentagramPulseInterval,Is.EqualTo(1f));
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_kongjwi"),true)
            .Any(trait=>trait.id==WizardMouseCombatRules.PentagramAnnihilationTraitId),Is.True);
        Assert.That(WizardMouseCombatRules.SupernovaDelay,Is.EqualTo(0.55f));
        Assert.That(WizardMouseCombatRules.SupernovaAscentDuration,Is.EqualTo(.32f));
        Assert.That(WizardMouseCombatRules.SupernovaPullDuration,Is.EqualTo(.25f));
        Assert.That(WizardMouseCombatRules.SupernovaStoredStarDamage(100f,.42f,false),Is.EqualTo(210f).Within(.01f));
        Assert.That(WizardMouseCombatRules.SupernovaStoredStarDamage(100f,.42f,true),Is.EqualTo(226.8f).Within(.01f));
        Assert.That(WizardMouseCombatRules.SupernovaRadiusTiles,Is.EqualTo(2.5f));
        Assert.That(WizardMouseCombatRules.SupernovaDamage(100f),Is.EqualTo(250f));
        Assert.That(WizardMouseCombatRules.SupernovaPullTiles,Is.EqualTo(0.75f));
        Assert.That(WizardMouseCombatRules.SupernovaKnockdownDuration,Is.EqualTo(1f));
        Assert.That(WizardMouseCombatRules.SupernovaColor("kongjwi"),
            Is.Not.EqualTo(WizardMouseCombatRules.SupernovaColor("patjwi")));
        Assert.That(WizardMouseCombatRules.SupernovaColor("seoljwi"),
            Is.Not.EqualTo(WizardMouseCombatRules.SupernovaColor("byeoljwi")));
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_kongjwi"),true)
            .Any(trait=>trait.id==WizardMouseCombatRules.SupernovaDescentTraitId),Is.True);
    }

    [Test]
    public void BearFightingSpirit_UsesApprovedBuffAndTraitRatios()
    {
        Assert.That(GameMasterData.Skill(BearCombatRules.SkillId).baseCooldown,Is.EqualTo(10f));
        Assert.That(BearCombatRules.ResolveDuration,Is.EqualTo(4f));
        Assert.That(BearCombatRules.DamageTakenMultiplier(true),Is.EqualTo(0.85f));
        Assert.That(BearCombatRules.WoongiHealing(200f),Is.EqualTo(2f));
        Assert.That(BearCombatRules.DamiShield(200f),Is.EqualTo(20f));
        Assert.That(BearCombatRules.IronKumaDamage(20f),Is.EqualTo(7f));
        Assert.That(BearCombatRules.BerberAttackSpeed(5),
            Is.EqualTo(0.3f).Within(0.001f));
        Assert.That(BearCombatRules.MountainDuration,Is.EqualTo(3f));
        Assert.That(BearCombatRules.MountainShield(200f),
            Is.EqualTo(30f).Within(0.001f));
        Assert.That(BearCombatRules.MountainAttackRangeTiles,Is.EqualTo(1.8f));
        Assert.That(BearCombatRules.MountainPrimaryDamageRatio,Is.EqualTo(1.4f));
        Assert.That(BearCombatRules.MountainSecondaryDamageRatio,Is.EqualTo(1f));
        Assert.That(BearCombatRules.MountainAttackArcDegrees,Is.EqualTo(180f));
        Assert.That(BearCombatRules.IsInsideMountainAttackArc(
            Vector3.forward,Vector3.right),Is.True);
        Assert.That(BearCombatRules.IsInsideMountainAttackArc(
            Vector3.forward,Vector3.back),Is.False);
        Assert.That(BearCombatRules.RagingStrikeCount,Is.EqualTo(3));
        Assert.That(BearCombatRules.RagingStrikeDamage(100f,0),Is.EqualTo(70f));
        Assert.That(BearCombatRules.RagingStrikeDamage(100f,1),Is.EqualTo(70f));
        Assert.That(BearCombatRules.RagingStrikeDamage(100f,2),Is.EqualTo(150f));
        Assert.That(BearCombatRules.RagingSecondHitTime,Is.EqualTo(.08f));
        Assert.That(BearCombatRules.RagingThirdHitTime,Is.EqualTo(.24f));
        Assert.That(BearCombatRules.RagingFinisherStaggerDuration,Is.EqualTo(.2f));
        Assert.That(BearCombatRules.KingRoarStartRadiusTiles,Is.EqualTo(3f));
        Assert.That(BearCombatRules.KingRoarAttackSpeedSlow,Is.EqualTo(.2f));
        Assert.That(BearCombatRules.KingRoarIntimidationDuration,Is.EqualTo(4f));
        Assert.That(BearCombatRules.KingRoarEndDamage(100f,0),Is.EqualTo(220f));
        Assert.That(BearCombatRules.KingRoarEndDamage(100f,5),Is.EqualTo(330f));
        Assert.That(BearCombatRules.KingRoarEndRadiusTiles,Is.EqualTo(3f));
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_woongi"),true)
            .Any(trait=>trait.id==BearCombatRules.MountainColossusTraitId),Is.True);
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_woongi"),true)
            .Any(trait=>trait.id==BearCombatRules.RagingHundredStrikesTraitId),Is.True);
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_woongi"),true)
            .Any(trait=>trait.id==BearCombatRules.KingOfBeastsRoarTraitId),Is.True);
    }

    [Test]
    public void WolfThreeSidedHunt_UsesApprovedHitAndTraitRatios()
    {
        Assert.That(WolfCombatRules.HitCount,Is.EqualTo(3));
        Assert.That(WolfCombatRules.HitInterval,Is.EqualTo(0.18f));
        Assert.That(WolfCombatRules.HuntDashDuration,Is.EqualTo(0.12f));
        Assert.That(WolfCombatRules.HitDamage(20f,0.6f,50f,false,true),
            Is.EqualTo(12f).Within(0.001f));
        Assert.That(WolfCombatRules.HitDamage(20f,0.6f,50f,true,true),
            Is.EqualTo(16f).Within(0.001f));
        Assert.That(WolfCombatRules.ChaseBasicDamage(20f,true),
            Is.EqualTo(23f).Within(0.001f));
        Assert.That(WolfCombatRules.AfterimageShield(200f,2),
            Is.EqualTo(16f).Within(0.001f));
        Assert.That(WolfCombatRules.RushDamage(100f,0f,false),
            Is.EqualTo(240f).Within(0.001f));
        Assert.That(WolfCombatRules.RushDamage(100f,50f,true),
            Is.EqualTo(244f).Within(0.001f));
        Assert.That(WolfCombatRules.SpectralSpiritCount,Is.EqualTo(8));
        Assert.That(WolfCombatRules.SpectralSpiritInterval,Is.EqualTo(0.04f));
        Assert.That(WolfCombatRules.SpectralSpiritDamage(100f),
            Is.EqualTo(30f).Within(0.001f));
        Assert.That(WolfCombatRules.SpectralExecutionDamage(100f,50f,true),
            Is.EqualTo(184f).Within(0.001f));
        Assert.That(WolfCombatRules.SpectralExecutionDefenseIgnoreRatio,
            Is.EqualTo(0.3f));
        Assert.That(WolfCombatRules.SpectralSpiritAddsFrost(1),Is.False);
        Assert.That(WolfCombatRules.SpectralSpiritAddsFrost(2),Is.True);
        Assert.That(WolfCombatRules.SpectralSpiritAddsFrost(5),Is.True);
        Assert.That(WolfCombatRules.ChaseBasicDamage(100f,true,true),
            Is.EqualTo(130f).Within(0.001f));
        Assert.That(WolfCombatRules.ChaseMoveSpeedBonusFor(true),
            Is.EqualTo(0.45f).Within(0.001f));
        Assert.That(WolfCombatRules.EclipseDuration,Is.EqualTo(4f));
        Assert.That(WolfCombatRules.EclipseCloneDamage(100f,false),
            Is.EqualTo(55f).Within(0.001f));
        Assert.That(WolfCombatRules.EclipseCloneDamage(100f,true),
            Is.EqualTo(63.25f).Within(0.001f));
        CombatStatusState eclipseShield=new CombatStatusState();
        eclipseShield.ApplyShield(4f,20f);
        eclipseShield.Tick(1f,null,false,true);
        Assert.That(eclipseShield.Get(CombatStatusKind.Shield).remaining,
            Is.EqualTo(4f).Within(0.001f));
        eclipseShield.Tick(1f);
        Assert.That(eclipseShield.Get(CombatStatusKind.Shield).remaining,
            Is.EqualTo(3f).Within(0.001f));
        Assert.That(WolfCombatRules.DistanceToSegment(
            Vector3.zero,Vector3.forward,-Vector3.forward),Is.EqualTo(0f).Within(0.001f));
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_nukgu"),true)
            .Any(trait=>trait.id==WolfCombatRules.PackRushTraitId),
            Is.True);
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_nukgu"),true)
            .Any(trait=>trait.id==WolfCombatRules.CelestialSpectralPackTraitId),
            Is.True);
        Assert.That(MonsterTraitCatalog.TestSelectable(
                Resources.Load<MonsterDefinition>("MonsterData/Monster_nukgu"),true)
            .Any(trait=>trait.id==WolfCombatRules.EclipsePackTraitId),
            Is.True);
    }

    [Test]
    public void ArachneGiantCaptureWeb_UsesApprovedControlAndTraitRatios()
    {
        Assert.That(SpiderCombatRules.BasicAttackId,Is.EqualTo("basic_silk_needle"));
        Assert.That(GameMasterData.Skill(SpiderCombatRules.SkillId).radiusTiles,
            Is.EqualTo(1.3f));
        Assert.That(SpiderCombatRules.RootDuration(false),Is.EqualTo(1f));
        Assert.That(SpiderCombatRules.RootDuration(true),Is.EqualTo(1.6f));
        Assert.That(SpiderCombatRules.SlowRatio(true),Is.EqualTo(0.5f));
        Assert.That(SpiderCombatRules.DesertSilkDamage(20f),Is.EqualTo(5f));
        Assert.That(SpiderCombatRules.ToxicPrankDamage(20f),Is.EqualTo(14f));
        MonsterDefinition aranea=Resources.Load<MonsterDefinition>("MonsterData/Monster_aranea");
        Assert.That(MonsterTraitCatalog.ResolveTags(aranea)&MonsterCombatTag.SpiderFamily,
            Is.Not.EqualTo(MonsterCombatTag.None));
    }

    [Test]
    public void NinjaCatShadowHunt_UsesApprovedTraitRatios()
    {
        Assert.That(NinjaCatCombatRules.EmpoweredBasicDamage(20f,true),
            Is.EqualTo(24f).Within(0.001f));
        Assert.That(NinjaCatCombatRules.OpeningStrikeDamage(20f),
            Is.EqualTo(12f).Within(0.001f));
        Assert.That(NinjaCatCombatRules.PoisonTickDamage(20f),
            Is.EqualTo(5f).Within(0.001f));
        Assert.That(NinjaCatCombatRules.AfterimageDamage(20f),
            Is.EqualTo(11f).Within(0.001f));
        MonsterDefinition chundongi=Resources.Load<MonsterDefinition>(
            "MonsterData/Monster_chundongi");
        Assert.That(
            MonsterTraitCatalog.ResolveTags(chundongi)&MonsterCombatTag.NinjaCatFamily,
            Is.Not.EqualTo(MonsterCombatTag.None));
    }

    [Test]
    public void TyrannoTremor_UsesApprovedTraitRatios()
    {
        Assert.That(TyrannoCombatRules.FeedingBonusDamage(20f),Is.EqualTo(12f));
        Assert.That(TyrannoCombatRules.FeedingHealing(200f),Is.EqualTo(10f));
        Assert.That(TyrannoCombatRules.SteelSkinShield(60f,2),Is.EqualTo(24f));
        Assert.That(TyrannoCombatRules.SteelSkinShield(60f,8),Is.EqualTo(36f));
        Assert.That(TyrannoCombatRules.TremorWindupFrames,Is.EqualTo(3));
        string[] prefabs={"041_tino","042_magno","043_cheolno","044_bonrex"};
        foreach(string prefabName in prefabs)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Enemies/PixelMonsters/"+prefabName+".prefab");
            Assert.That(prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true)
                .SkillAttackHitFrameIndex,Is.EqualTo(TyrannoCombatRules.TremorWindupFrames));
        }
        MonsterDefinition tino=Resources.Load<MonsterDefinition>("MonsterData/Monster_tino");
        Assert.That(MonsterTraitCatalog.ResolveTags(tino)&MonsterCombatTag.TyrannoFamily,
            Is.Not.EqualTo(MonsterCombatTag.None));
    }

    [Test]
    public void MonkeyAcrobaticBarrage_UsesApprovedTraitRatios()
    {
        Assert.That(MonkeyCombatRules.ProjectileDamage(
            20f,0.45f,0,false,false),Is.EqualTo(9f));
        Assert.That(MonkeyCombatRules.ProjectileDamage(
            20f,0.45f,3,true,false),Is.EqualTo(11.7f).Within(0.001f));
        Assert.That(MonkeyCombatRules.ProjectileDamage(
            20f,0.45f,0,false,true),Is.EqualTo(14.4f).Within(0.001f));
        Assert.That(MonkeyCombatRules.PoisonBurstDamage(20f),Is.EqualTo(14f));
        MonsterDefinition chiwoo=Resources.Load<MonsterDefinition>(
            "MonsterData/Monster_chiwoo");
        Assert.That(MonsterTraitCatalog.ResolveTags(chiwoo)&MonsterCombatTag.MonkeyFamily,
            Is.Not.EqualTo(MonsterCombatTag.None));
    }

    [Test]
    public void MantisCrossScytheWind_UsesApprovedTraitRatios()
    {
        Assert.That(MantisCombatRules.HitDamage(
            20f,0.8f,0,true),Is.EqualTo(16f));
        Assert.That(MantisCombatRules.HitDamage(
            20f,0.8f,1,true),Is.EqualTo(22.4f).Within(0.001f));
        Assert.That(MantisCombatRules.BleedTickDamage(20f),Is.EqualTo(4f));
        Assert.That(MantisCombatRules.RangeTiles(3.5f,true),Is.EqualTo(5f));
        Assert.That(MantisCombatRules.WidthTiles(1.2f,true),
            Is.EqualTo(1.8f).Within(0.001f));
        MonsterDefinition mantius=Resources.Load<MonsterDefinition>(
            "MonsterData/Monster_mantius");
        Assert.That(MonsterTraitCatalog.ResolveTags(mantius)&MonsterCombatTag.MantisFamily,
            Is.Not.EqualTo(MonsterCombatTag.None));
    }

    [Test]
    public void MushroomSporeColony_UsesApprovedTraitRatios()
    {
        Assert.That(MushroomCombatRules.ExplosionDamage(
            20f,0.55f,0,true),Is.EqualTo(11f).Within(0.001f));
        Assert.That(MushroomCombatRules.ExplosionDamage(
            20f,0.55f,2,true),Is.EqualTo(15.4f).Within(0.001f));
        Assert.That(MushroomCombatRules.PoisonTickDamage(20f),Is.EqualTo(4f));
        Assert.That(MushroomCombatRules.Healing(20f),Is.EqualTo(7f));
        Assert.That(MushroomCombatRules.SporeSeedDuration,Is.EqualTo(4f));
        MonsterDefinition versus=Resources.Load<MonsterDefinition>(
            "MonsterData/Monster_versus");
        Assert.That(
            MonsterTraitCatalog.ResolveTags(versus)&MonsterCombatTag.MushroomFamily,
            Is.Not.EqualTo(MonsterCombatTag.None));
    }

    [Test]
    public void MammothEarthCharge_UsesApprovedTraitRatios()
    {
        Assert.That(MammothCombatRules.Damage(
            20f,1.2f,30f,false),Is.EqualTo(24f));
        Assert.That(MammothCombatRules.Damage(
            20f,1.2f,30f,true),Is.EqualTo(34.5f));
        Assert.That(MammothCombatRules.KogasonShield(200f,3),Is.EqualTo(18f));
        Assert.That(MammothCombatRules.AncestorShield(200f),Is.EqualTo(16f));
        Assert.That(Vector3.Distance(Vector3.zero,
            MammothCombatRules.ChargeEnd(Vector3.zero,Vector3.right,1f)),
            Is.EqualTo(3.5f).Within(0.0001f));
        MonsterDefinition kogason=Resources.Load<MonsterDefinition>(
            "MonsterData/Monster_kogason");
        Assert.That(
            MonsterTraitCatalog.ResolveTags(kogason)&MonsterCombatTag.MammothFamily,
            Is.Not.EqualTo(MonsterCombatTag.None));
    }

    [Test]
    public void LateRosterFamilies_UseApprovedRatiosAndTags()
    {
        Assert.That(BeetleCombatRules.Damage(
            20f,BeetleCombatRules.LightningDamageRatio),Is.EqualTo(12f));
        Assert.That(SpiritDeerCombatRules.HolyHealing(200f),
            Is.EqualTo(20f).Within(.001f));
        Assert.That(SpiritDeerCombatRules.MoonHealing(20f,100f),
            Is.EqualTo(32f).Within(.001f));
        Assert.That(SpiritDeerCombatRules.ForestHealing(200f),
            Is.EqualTo(12f).Within(.001f));
        Assert.That(SpiritDeerCombatRules.EclipseHealing(20f),
            Is.EqualTo(12f).Within(.001f));
        SkillMasterRow sanctuary=GameMasterData.Skill(SpiritDeerCombatRules.SkillId);
        Assert.That(sanctuary.statusValue,Is.Zero);
        Assert.That(sanctuary.targetMissingHpRatio,Is.Zero);
        Assert.That(sanctuary.casterMaxHpRatio,Is.Zero);
        Assert.That(sanctuary.statusEffect,Is.EqualTo("OneShotHealCleanseZone"));
        Assert.That(sanctuary.castRangeTiles,Is.EqualTo(4.5f),
            "Spirit Sanctuary targets the lowest-health ally in cast range.");
        Assert.That(SpiritDeerCombatRules.HolyShield(200f),Is.EqualTo(16f));
        Assert.That(GriffinCombatRules.RoyalClawDamage(20f),Is.EqualTo(10f));
        Assert.That(GriffinCombatRules.HellfireDamage(20f),Is.EqualTo(5f));
        Assert.That(GriffinCombatRules.AfterimageDamage(
            20f,1.7f),Is.EqualTo(17f).Within(.001f));
        Assert.That(GriffinCombatRules.Shield(200f),Is.EqualTo(16f));
        Assert.That(WyvernCombatRules.Damage(20f,1.5f),Is.EqualTo(30f));
        Assert.That(WyvernCombatRules.Damage(
            20f,WyvernCombatRules.VenomTickRatio),Is.EqualTo(5f));
        Assert.That(HaetaeCombatRules.Damage(20f,1.2f),Is.EqualTo(24f));
        Assert.That(HaetaeCombatRules.ClearHealing(20f),Is.EqualTo(12f));
        Assert.That(HaetaeCombatRules.DiamondShield(200f),Is.EqualTo(16f));
        SkillMasterRow roar=GameMasterData.Skill(HaetaeCombatRules.SkillId);
        Assert.That(roar.statusEffect,Does.Contain("CleanseAll"));
        Assert.That(roar.damageFormulaNote,Does.Contain("모두 제거"));

        string[] paths=
        {
            "MonsterData/Monster_jangpung","MonsterData/Monster_holydeer",
            "MonsterData/Monster_pone","MonsterData/Monster_vivre",
            "MonsterData/Monster_haeta"
        };
        MonsterCombatTag[] tags=
        {
            MonsterCombatTag.BeetleFamily,MonsterCombatTag.SpiritDeerFamily,
            MonsterCombatTag.GriffinFamily,MonsterCombatTag.WyvernFamily,
            MonsterCombatTag.HaetaeFamily
        };
        for(int i=0;i<paths.Length;i++)
        {
            MonsterDefinition monster=Resources.Load<MonsterDefinition>(paths[i]);
            Assert.That(monster,Is.Not.Null,paths[i]);
            Assert.That(MonsterTraitCatalog.ResolveTags(monster)&tags[i],
                Is.Not.EqualTo(MonsterCombatTag.None),paths[i]);
        }
    }

    [Test]
    public void TimedDebuffImmunityBlocksHarmfulEffectsUntilItExpires()
    {
        CombatStatusState state=new CombatStatusState();
        state.ApplyDebuffImmunity(2f);
        state.ApplyTimed(CombatStatusKind.Poison,4f);
        Assert.That(state.Has(CombatStatusKind.Poison),Is.False);

        state.Tick(2.01f);
        state.ApplyTimed(CombatStatusKind.Poison,4f);
        Assert.That(state.Has(CombatStatusKind.Poison),Is.True);
    }

    [Test]
    public void ComboFeedbackUsesApprovedShortNames()
    {
        string[] traitIds=
        {
            "trait_combo_antigravity","trait_combo_fall_shatter",
            "trait_combo_frost_shackle","trait_combo_venom_mutation",
            "trait_combo_chemical_ignition","trait_combo_open_wound",
            "trait_combo_morale_veil","trait_combo_resolve_barrier"
        };
        Assert.That(traitIds.Select(MonsterTraitRuntime.ComboShortName),
            Is.EqualTo(new[]{"반중력","낙하분쇄","서리족쇄","독혈변이",
                "화학점화","상처개방","사기장막","결의방벽"}));
    }

    [Test]
    public void ComboScenario_AirborneFallsIntoKnockdownAndExposure()
    {
        object opener=new object();
        CombatStatusState target=new CombatStatusState();
        target.ApplyTimed(CombatStatusKind.Airborne,1f,opener);
        Assert.That(target.Get(CombatStatusKind.Airborne).source,Is.SameAs(opener));
        target.Remove(CombatStatusKind.Airborne);
        target.ApplyTimed(CombatStatusKind.Knockdown,0.8f,opener);
        target.ApplyTimed(CombatStatusKind.ComboWeakened,4f,opener);
        Assert.That(target.Has(CombatStatusKind.Knockdown),Is.True);
        Assert.That(target.Has(CombatStatusKind.ComboWeakened),Is.True);
    }

    [Test]
    public void ComboScenario_PoisonIsConsumedIntoChemicalBurning()
    {
        object opener=new object();
        CombatStatusState target=new CombatStatusState();
        target.ApplyStacks(CombatStatusKind.Poison,5f,2,5,opener,2f,1f,true);
        Assert.That(target.ConsumeStacks(CombatStatusKind.Poison,1),Is.EqualTo(1));
        target.ApplyTimed(CombatStatusKind.ComboBurning,4f,opener);
        Assert.That(target.Get(CombatStatusKind.Poison).stacks,Is.EqualTo(1));
        Assert.That(target.Has(CombatStatusKind.ComboBurning),Is.True);
    }

    [Test]
    public void ComboScenario_AlliedShieldFeedsRallyAndResolveBarrier()
    {
        object provider=new object();
        CombatStatusState receiver=new CombatStatusState();
        receiver.ApplyShield(5f,16f,provider);
        Assert.That(receiver.Get(CombatStatusKind.Shield).source,Is.SameAs(provider));

        receiver.ApplyTimed(CombatStatusKind.ComboRally,5f,provider,0.2f);
        Assert.That(receiver.Get(CombatStatusKind.ComboRally).magnitude,
            Is.EqualTo(0.2f));
        receiver.ApplyShield(5f,8f,receiver);
        Assert.That(receiver.Get(CombatStatusKind.Shield).magnitude,
            Is.EqualTo(16f));
    }
}
