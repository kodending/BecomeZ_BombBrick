public sealed partial class MonsterAutoBattlePrototype
{
    private static MonsterGrowthMasterRow SkillScalingRow(Unit unit)
    {
        return unit==null||unit.definition==null?null:GameMasterData.MonsterGrowth(unit.definition.monsterId);
    }

    private static bool UsesAttackSkillScaling(Unit unit)
    {
        return SpeciesSkillScaling.UsesAttack(SkillScalingRow(unit));
    }

    private static float OffensiveMaxHealth(Unit unit)
    {
        // The preview's extra HP buys observation time without amplifying HP-based attacks.
        return unit.maxHp/(unit.guardianPreview?GuardianPreviewHealthMultiplier:1f);
    }

    private float SkillPower(Unit unit)
    {
        if(unit==null)return 0f;
        MonsterGrowthMasterRow row=SkillScalingRow(unit);
        return SpeciesSkillScaling.Power(row,unit.damage,OffensiveMaxHealth(unit),
            row!=null&&row.skillScalingStat=="Defense"?CurrentDefense(unit):unit.defense);
    }

    private string SkillScalingSummary(Unit unit)
    {
        if(unit==null)return "";
        return SpeciesSkillScaling.Summary(SkillScalingRow(unit),unit.damage,OffensiveMaxHealth(unit),CurrentDefense(unit));
    }
}
