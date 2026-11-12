using System;
using System.Collections.Generic;
using System.Linq;

public static class MonsterInstanceGenerator
{
    public const int ShinyOddsDenominator=65536;

    private static readonly string[] PersonalityIds={
        "vengeful","fickle","cooperative","protective","bold","timid"
    };

    public static void InitializeNewInstance(
        OwnedMonsterSave monster,MonsterDefinition definition,bool allowShiny=true)
    {
        if(monster==null)throw new ArgumentNullException(nameof(monster));
        if(definition==null)throw new ArgumentNullException(nameof(definition));

        monster.monsterId=definition.monsterId;
        monster.intrinsicTraitId=definition.intrinsicTraitId;
        if(monster.personalitySeed==0)monster.personalitySeed=StableSeed(monster.instanceId);

        Random random=new Random(monster.personalitySeed);
        // These axes remain serialized for old-save compatibility and future observation tools.
        // The public combat personality is now a separate, equally weighted permanent roll.
        monster.courage=RollAxis(random);
        monster.aggression=RollAxis(random);
        monster.discipline=RollAxis(random);
        monster.composure=RollAxis(random);
        monster.cooperation=RollAxis(random);
        monster.adaptability=RollAxis(random);
        monster.hpMultiplier=RollPercent(random,MonsterIndividualStat.Health);
        monster.damageMultiplier=RollPercent(random,MonsterIndividualStat.AttackDamage);
        monster.attackSpeedMultiplier=RollPercent(random,MonsterIndividualStat.AttackSpeed);
        monster.defenseMultiplier=RollPercent(random,MonsterIndividualStat.Defense);
        monster.statusResistanceMultiplier=RollPercent(
            random,MonsterIndividualStat.StatusResistance);
        monster.moveSpeedMultiplier=RollPercent(random,MonsterIndividualStat.MoveSpeed);
        monster.isShiny=allowShiny&&
            !string.IsNullOrWhiteSpace(definition.shinyResourcePath)&&
            RollShiny(monster.instanceId);
        if(monster.isShiny)ApplyShinyStats(monster);
        monster.personalityId=ResolvePersonalityId(monster);
    }

    public static void LimitStarterQuality(OwnedMonsterSave monster)
    {
        var random=new Random(StableSeed(monster.instanceId+"_starter_stats"));
        for(int attempt=0;attempt<128;attempt++)
        {
            float score=MonsterStatQuality.OverallScore(monster);
            if(score>=30f&&score<70f)return; // Overall B- through B+; individual stats keep their normal range.
            monster.hpMultiplier=RollPercent(random,MonsterIndividualStat.Health);
            monster.damageMultiplier=RollPercent(random,MonsterIndividualStat.AttackDamage);
            monster.attackSpeedMultiplier=RollPercent(random,MonsterIndividualStat.AttackSpeed);
            monster.defenseMultiplier=RollPercent(random,MonsterIndividualStat.Defense);
            monster.statusResistanceMultiplier=RollPercent(random,MonsterIndividualStat.StatusResistance);
            monster.moveSpeedMultiplier=RollPercent(random,MonsterIndividualStat.MoveSpeed);
        }
        throw new InvalidOperationException("Unable to generate starter stats in the B grade range.");
    }

    public static void EnsureInitialized(OwnedMonsterSave monster,MonsterDefinition definition)
    {
        if(monster==null||definition==null)return;
        if(monster.personalitySeed==0)monster.personalitySeed=StableSeed(monster.instanceId);
        if(string.IsNullOrWhiteSpace(monster.intrinsicTraitId))
            monster.intrinsicTraitId=definition.intrinsicTraitId;

        bool missingAxes=!AxesAreValid(monster);
        if(missingAxes)
        {
            Random random=new Random(monster.personalitySeed);
            monster.courage=RollAxis(random);
            monster.aggression=RollAxis(random);
            monster.discipline=RollAxis(random);
            monster.composure=RollAxis(random);
            monster.cooperation=RollAxis(random);
            monster.adaptability=RollAxis(random);

            if(AreDefaultMultipliers(monster))
            {
                monster.hpMultiplier=RollPercent(random,MonsterIndividualStat.Health);
                monster.damageMultiplier=RollPercent(random,MonsterIndividualStat.AttackDamage);
                monster.attackSpeedMultiplier=RollPercent(random,MonsterIndividualStat.AttackSpeed);
                monster.defenseMultiplier=RollPercent(random,MonsterIndividualStat.Defense);
                monster.statusResistanceMultiplier=RollPercent(
                    random,MonsterIndividualStat.StatusResistance);
                monster.moveSpeedMultiplier=RollPercent(random,MonsterIndividualStat.MoveSpeed);
            }
        }

        if(string.IsNullOrWhiteSpace(monster.personalityId)||
           !PersonalityCombatPolicy.IsSupported(monster.personalityId))
            monster.personalityId=ResolvePersonalityId(monster);
        if(monster.isShiny)ApplyShinyStats(monster);
    }

    public static void ApplyEnemyDifficultyQuality(OwnedMonsterSave monster,ExpeditionDifficulty difficulty)
    {
        if(monster==null)throw new ArgumentNullException(nameof(monster));
        if(monster.isShiny){ApplyShinyStats(monster);return;}
        var range=ExpeditionDifficultyRules.EnemyQualityScoreRange(difficulty);
        if(range.x<0f||range.x>=100f||range.y<=range.x||range.y>101f)
            throw new InvalidOperationException("Invalid enemy quality score range for "+difficulty+".");
        var random=new Random(StableSeed(monster.instanceId+"_enemy_quality_"+(int)ExpeditionDifficultyRules.Clamp(difficulty)));
        for(int attempt=0;attempt<256;attempt++)
        {
            monster.hpMultiplier=RollPercent(random,MonsterIndividualStat.Health);
            monster.damageMultiplier=RollPercent(random,MonsterIndividualStat.AttackDamage);
            monster.attackSpeedMultiplier=RollPercent(random,MonsterIndividualStat.AttackSpeed);
            monster.defenseMultiplier=RollPercent(random,MonsterIndividualStat.Defense);
            monster.statusResistanceMultiplier=RollPercent(random,MonsterIndividualStat.StatusResistance);
            monster.moveSpeedMultiplier=RollPercent(random,MonsterIndividualStat.MoveSpeed);
            float score=MonsterStatQuality.OverallScore(monster);
            if(score>=range.x&&score<range.y)return;
        }
        throw new InvalidOperationException("Unable to generate enemy stats for "+difficulty+".");
    }

    public static string ResolvePersonalityId(OwnedMonsterSave monster)
    {
        if(monster==null)return PersonalityIds[0];

        GameMasterDatabase master=GameMasterData.Database;
        if(master!=null&&master.personalities!=null)
        {
            List<string> configured=master.personalities
                .Where(row=>row!=null&&row.enabled&&
                    PersonalityCombatPolicy.IsFeatured(row.id))
                .OrderBy(row=>row.priority)
                .ThenBy(row=>row.id,StringComparer.Ordinal)
                .Select(row=>row.id)
                .ToList();
            if(configured.Count>0)
                return configured[PersonalityIndex(monster.personalitySeed,configured.Count)];
        }

        return PersonalityIds[PersonalityIndex(monster.personalitySeed,PersonalityIds.Length)];
    }

    public static string PersonalityDisplayName(string id)
    {
        PersonalityMasterRow master=GameMasterData.Personality(id);
        if(master!=null&&!string.IsNullOrWhiteSpace(master.displayName))return master.displayName;
        switch(id)
        {
            case "persistent": return "집요한";
            case "calm": return "냉정한";
            case "challenger": return "도전적인";
            case "vengeful": return "복수심 강한";
            case "fickle": return "변덕스러운";
            case "cooperative": return "협동적인";
            case "independent": return "독립적인";
            case "protective": return "보호적인";
            case "opportunist": return "기회주의적인";
            case "impulsive": return "성급한";
            case "cautious": return "신중한";
            case "bold": return "대담한";
            case "timid": return "겁이 많은";
            case "eccentric": return "엉뚱한";
            default: return "성격 미지정";
        }
    }

    public static bool AxesAreValid(OwnedMonsterSave monster)
    {
        return monster!=null&&InAxisRange(monster.courage)&&InAxisRange(monster.aggression)&&
               InAxisRange(monster.discipline)&&InAxisRange(monster.composure)&&
               InAxisRange(monster.cooperation)&&InAxisRange(monster.adaptability);
    }

    public static bool MultipliersAreValid(OwnedMonsterSave monster)
    {
        return monster!=null&&
               InRange(monster.hpMultiplier,Minimum(MonsterIndividualStat.Health),Maximum(MonsterIndividualStat.Health))&&
               InRange(monster.damageMultiplier,Minimum(MonsterIndividualStat.AttackDamage),Maximum(MonsterIndividualStat.AttackDamage))&&
               InRange(monster.attackSpeedMultiplier,Minimum(MonsterIndividualStat.AttackSpeed),Maximum(MonsterIndividualStat.AttackSpeed))&&
               InRange(monster.defenseMultiplier,Minimum(MonsterIndividualStat.Defense),Maximum(MonsterIndividualStat.Defense))&&
               InRange(monster.statusResistanceMultiplier,Minimum(MonsterIndividualStat.StatusResistance),Maximum(MonsterIndividualStat.StatusResistance))&&
               InRange(monster.moveSpeedMultiplier,Minimum(MonsterIndividualStat.MoveSpeed),Maximum(MonsterIndividualStat.MoveSpeed));
    }

    public static int StableSeed(string value)
    {
        unchecked
        {
            int hash=17;
            string source=value??string.Empty;
            for(int i=0;i<source.Length;i++)hash=hash*31+source[i];
            return hash==0?17:hash;
        }
    }

    public static void ApplyCaptureQualityRerolls(
        OwnedMonsterSave monster,ExpeditionDifficulty difficulty,int seed)
    {
        int rolls=ExpeditionDifficultyRules.CaptureQualityRolls(difficulty);
        if(monster==null||monster.isShiny)return;
        Random random=new Random(seed);
        // Capture quality is independent of the difficulty-limited encounter stats.
        float bestScore=-1f;
        for(int roll=0;roll<rolls;roll++)
        {
            float hp=RollPercent(random,MonsterIndividualStat.Health);
            float damage=RollPercent(random,MonsterIndividualStat.AttackDamage);
            float speed=RollPercent(random,MonsterIndividualStat.AttackSpeed);
            float defense=RollPercent(random,MonsterIndividualStat.Defense);
            float resistance=RollPercent(
                random,MonsterIndividualStat.StatusResistance);
            float move=RollPercent(random,MonsterIndividualStat.MoveSpeed);
            float score=MonsterStatQuality.QualityScore(hp,MonsterIndividualStat.Health);
            score+=MonsterStatQuality.QualityScore(damage,MonsterIndividualStat.AttackDamage);
            score+=MonsterStatQuality.QualityScore(speed,MonsterIndividualStat.AttackSpeed);
            score+=MonsterStatQuality.QualityScore(defense,MonsterIndividualStat.Defense);
            score+=MonsterStatQuality.QualityScore(resistance,MonsterIndividualStat.StatusResistance);
            score+=MonsterStatQuality.QualityScore(move,MonsterIndividualStat.MoveSpeed);
            score/=6f;
            if(score<=bestScore)continue;
            bestScore=score;
            monster.hpMultiplier=hp;
            monster.damageMultiplier=damage;
            monster.attackSpeedMultiplier=speed;
            monster.defenseMultiplier=defense;
            monster.statusResistanceMultiplier=resistance;
            monster.moveSpeedMultiplier=move;
        }
    }

    private static int PersonalityIndex(int seed,int count)
    {
        if(count<=1)return 0;
        unchecked
        {
            uint mixed=(uint)(seed==0?17:seed);
            mixed^=0x9E3779B9u;
            mixed*=0x85EBCA6Bu;
            mixed^=mixed>>13;
            return (int)(mixed%(uint)count);
        }
    }

    private static bool AreDefaultMultipliers(OwnedMonsterSave monster)
    {
        return NearlyOne(monster.hpMultiplier)&&NearlyOne(monster.damageMultiplier)&&
               NearlyOne(monster.attackSpeedMultiplier)&&
               NearlyOne(monster.defenseMultiplier)&&
               NearlyOne(monster.statusResistanceMultiplier)&&
               NearlyOne(monster.moveSpeedMultiplier);
    }

    private static bool NearlyOne(float value)
    {
        return Math.Abs(value-1f)<0.0001f;
    }

    private static bool RollShiny(string instanceId)
    {
        return new Random(StableSeed((instanceId??string.Empty)+"_shiny"))
            .Next(ShinyOddsDenominator)==0;
    }

    private static void ApplyShinyStats(OwnedMonsterSave monster)
    {
        monster.hpMultiplier=Maximum(MonsterIndividualStat.Health);
        monster.damageMultiplier=Maximum(MonsterIndividualStat.AttackDamage);
        monster.attackSpeedMultiplier=Maximum(MonsterIndividualStat.AttackSpeed);
        monster.defenseMultiplier=Maximum(MonsterIndividualStat.Defense);
        monster.statusResistanceMultiplier=Maximum(
            MonsterIndividualStat.StatusResistance);
        monster.moveSpeedMultiplier=Maximum(MonsterIndividualStat.MoveSpeed);
    }

    private static float RollPercent(Random random,int minimum,int maximum)
    {
        return random.Next(minimum,maximum+1)/100f;
    }

    private static float RollPercent(
        Random random,MonsterIndividualStat stat)
    {
        return RollPercent(
            random,
            MonsterStatQuality.MinimumPercent(stat),
            MonsterStatQuality.MaximumPercent(stat));
    }

    private static float Minimum(MonsterIndividualStat stat)
    {
        return MonsterStatQuality.MinimumPercent(stat)/100f;
    }

    private static float Maximum(MonsterIndividualStat stat)
    {
        return MonsterStatQuality.MaximumPercent(stat)/100f;
    }

    private static int RollAxis(Random random)
    {
        int minimum=Generation("axis_min",1);
        int maximum=Generation("axis_max",20);
        return (random.Next(minimum,maximum+1)+random.Next(minimum,maximum+1))/2;
    }

    private static bool InAxisRange(int value)
    {
        return value>=Generation("axis_min",1)&&value<=Generation("axis_max",20);
    }

    private static bool InRange(float value,float minimum,float maximum)
    {
        return value>=minimum-0.0001f&&value<=maximum+0.0001f;
    }

    private static int Generation(string key,int fallback)
    {
        return GameMasterData.GenerationInt(key,fallback);
    }
}
