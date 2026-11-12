using System;
using System.Collections.Generic;
using System.Linq;

public enum CombatGrowthCardRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum CombatGrowthCardEffect
{
    MaxHealth,
    AttackDamage,
    AttackSpeed,
    DamageReduction,
    KillHeal,
    LowHealthAttackSpeed,
    FocusFireDamage,
    VengeanceAttackDamage,
    Defense,
    StatusResistanceRecoveryGuard,
    SkillPower,
    CooldownRecoverySpeed,
    ExecuteDamage,
    OpeningDamage,
    PersistentTargetDamage,
    KillTeamAttackSpeed,
    MissingHealthRegeneration,
    OpeningShield,
    LowHealthDamageReduction,
    DeathShield,
    LongBattleAttackDamage,
    TeamKillMissingHealthHeal,
    SkillCycleHeal
}

[Serializable]
public sealed class CombatGrowthCardSelectionSave
{
    public string cardId;
    public CombatGrowthCardRarity rarity;
}

public sealed class CombatGrowthCardDefinition
{
    private readonly float[] values;
    private readonly float[] secondaryValues;

    public string id { get; }
    public string displayName { get; }
    public CombatGrowthCardEffect effect { get; }

    public CombatGrowthCardDefinition(string id,string displayName,CombatGrowthCardEffect effect,
        float common,float rare,float epic,float legendary,
        float commonSecondary=0f,float rareSecondary=0f,
        float epicSecondary=0f,float legendarySecondary=0f)
    {
        this.id=id;
        this.displayName=displayName;
        this.effect=effect;
        values=new[]{common,rare,epic,legendary};
        secondaryValues=new[]{commonSecondary,rareSecondary,epicSecondary,legendarySecondary};
    }

    public float SecondaryValue(CombatGrowthCardRarity rarity)
    {
        return secondaryValues[(int)rarity];
    }

    public float Value(CombatGrowthCardRarity rarity)
    {
        return values[(int)rarity];
    }

    public string EffectText(CombatGrowthCardRarity rarity)
    {
        float value=Value(rarity);
        string number=value.ToString("0.#");
        switch(effect)
        {
            case CombatGrowthCardEffect.MaxHealth:return "모든 아군 최대 체력 +"+number+"%";
            case CombatGrowthCardEffect.AttackDamage:return "모든 아군 공격력 +"+number+"%";
            case CombatGrowthCardEffect.AttackSpeed:return "모든 아군 공격 속도 +"+number+"%";
            case CombatGrowthCardEffect.DamageReduction:return "모든 아군이 받는 피해 -"+number+"%";
            case CombatGrowthCardEffect.Defense:return "모든 아군 방어력 +"+number;
            case CombatGrowthCardEffect.StatusResistanceRecoveryGuard:return "상태이상 저항 +"+number+
                ", 상태이상 종료 후 5초간 받는 피해 -"+SecondaryValue(rarity).ToString("0.#")+"%";
            case CombatGrowthCardEffect.SkillPower:return "모든 아군 스킬 위력 +"+number+"%";
            case CombatGrowthCardEffect.CooldownRecoverySpeed:return "스킬 재사용 속도 +"+number+"%";
            case CombatGrowthCardEffect.FocusFireDamage:return "같은 적을 공격하는 추가 아군당 피해 +"+number+"%";
            case CombatGrowthCardEffect.ExecuteDamage:return "체력 30% 이하 적에게 피해 +"+number+"%";
            case CombatGrowthCardEffect.OpeningDamage:return "전투 시작 후 8초간 피해 +"+number+"%";
            case CombatGrowthCardEffect.PersistentTargetDamage:return "같은 대상 연속 공격마다 피해 +"+number+"%";
            case CombatGrowthCardEffect.KillTeamAttackSpeed:return "적 처치 시 전체 공격 속도 5초간 +"+number+"%";
            case CombatGrowthCardEffect.MissingHealthRegeneration:return "4초마다 잃은 체력 "+number+"% 회복";
            case CombatGrowthCardEffect.OpeningShield:return "전투 시작 시 최대 체력 "+number+"% 보호막 (8초)";
            case CombatGrowthCardEffect.LowHealthDamageReduction:return "체력 40% 이하에서 받는 피해 -"+number+"%";
            case CombatGrowthCardEffect.LowHealthAttackSpeed:return "체력 40% 이하에서 공격 속도 +"+number+"%";
            case CombatGrowthCardEffect.VengeanceAttackDamage:return "아군 사망마다 생존 아군 공격력 +"+number+"%";
            case CombatGrowthCardEffect.DeathShield:return "아군 사망 시 생존 아군에게 최대 체력 "+number+"% 보호막";
            case CombatGrowthCardEffect.LongBattleAttackDamage:return "전투 5초마다 공격력 +"+number+"%";
            case CombatGrowthCardEffect.TeamKillMissingHealthHeal:return "적 사망 시 생존 아군이 잃은 체력 "+number+"% 회복";
            case CombatGrowthCardEffect.SkillCycleHeal:return "스킬 사용 시 최저 체력 아군 최대 체력 "+number+"% 회복";
        }
        if(effect==CombatGrowthCardEffect.MaxHealth)
            return "모든 아군 최대 체력 +"+value.ToString("0")+"%";
        if(effect==CombatGrowthCardEffect.AttackDamage)
            return "모든 아군 공격력 +"+value.ToString("0")+"%";
        if(effect==CombatGrowthCardEffect.AttackSpeed)
            return "모든 아군 공격속도 +"+value.ToString("0")+"%";
        if(effect==CombatGrowthCardEffect.DamageReduction)
            return "모든 아군이 받는 피해 -"+value.ToString("0")+"%";
        if(effect==CombatGrowthCardEffect.KillHeal)
            return "적 처치 시 처치한 아군이 최대 체력의 "+value.ToString("0")+"% 회복";
        if(effect==CombatGrowthCardEffect.LowHealthAttackSpeed)
            return "체력 40% 이하인 아군의 공격속도 +"+value.ToString("0")+"%";
        if(effect==CombatGrowthCardEffect.FocusFireDamage)
            return "같은 적을 노리는 아군이 2명 이상이면 해당 적에게 주는 피해 +"+value.ToString("0")+"%";
        return "아군 사망 1회당 생존 아군 공격력 +"+value.ToString("0")+"% (Wave 동안 중첩)";
    }
}

[Serializable]
public sealed class CombatGrowthCardDraftEntry
{
    public string cardId;
    public CombatGrowthCardRarity rarity;
}

public sealed class CombatGrowthCardModifiers
{
    public float maxHealthBonus;
    public float attackDamageBonus;
    public float attackSpeedBonus;
    public float damageTakenMultiplier=1f;
    public float lowHealthAttackSpeedBonus;
    public float focusFireDamageBonus;
    public float vengeanceAttackDamagePerDeath;
    public float defenseBonus;
    public float statusResistanceBonus;
    public float statusRecoveryDamageTakenMultiplier=1f;
    public float skillPowerBonus;
    public float cooldownRecoverySpeed;
    public float executeDamageBonus;
    public float openingDamageBonus;
    public float persistentTargetDamagePerHit;
    public float killTeamAttackSpeedBonus;
    public float missingHealthRegenerationRatio;
    public float openingShieldRatio;
    public float lowHealthDamageTakenMultiplier=1f;
    public float deathShieldRatio;
    public float longBattleAttackDamagePerInterval;
    public float teamKillMissingHealthHealRatio;
    public float skillCycleHealRatio;
}

public static class CombatGrowthCardBalancePreset
{
    public static List<CombatGrowthCardSelectionSave> CreateForWave(int targetWave)
    {
        int clampedWave=Math.Max(1,targetWave);
        List<CombatGrowthCardSelectionSave> result=
            new List<CombatGrowthCardSelectionSave>(Math.Max(0,clampedWave-1));
        IReadOnlyList<CombatGrowthCardDefinition> catalog=CombatGrowthCardCatalog.All;
        if(catalog.Count==0)return result;
        for(int clearedWave=1;clearedWave<clampedWave;clearedWave++)
        {
            if(!CombatGrowthCardDraftService.IsRewardAfterWave(clearedWave))continue;
            CombatGrowthCardDefinition selected=catalog[(clearedWave-1)%catalog.Count];
            result.Add(new CombatGrowthCardSelectionSave
            {
                cardId=selected.id,
                rarity=RepresentativeRarity(clearedWave)
            });
        }
        return result;
    }

    private static CombatGrowthCardRarity RepresentativeRarity(int wave)
    {
        // Van der Corput samples spread a short 12-wave run across the whole
        // probability range without allowing a lucky fixed seed to dominate.
        double quantile=VanDerCorputBaseTwo(wave);
        GrowthCardRarityMasterRow master=GameMasterData.GrowthRarity(wave);
        if(master==null)return CombatGrowthCardRarity.Common;
        double common=Math.Max(0d,master.commonWeight);
        double rare=Math.Max(0d,master.rareWeight);
        double epic=Math.Max(0d,master.epicWeight);
        double legendary=Math.Max(0d,master.legendaryWeight);
        double total=common+rare+epic+legendary;
        if(total<=0d)return CombatGrowthCardRarity.Common;
        double roll=quantile*total;
        if(roll<common)return CombatGrowthCardRarity.Common;
        if(roll<common+rare)return CombatGrowthCardRarity.Rare;
        if(roll<common+rare+epic)return CombatGrowthCardRarity.Epic;
        return CombatGrowthCardRarity.Legendary;
    }

    private static double VanDerCorputBaseTwo(int index)
    {
        double result=0d;
        double denominator=1d;
        int value=Math.Max(1,index);
        while(value>0)
        {
            denominator*=2d;
            result+=(value%2)/denominator;
            value/=2;
        }
        return result;
    }
}

public sealed class AutoBattleBalanceStatistics
{
    public int CompletedRuns { get; private set; }
    public int Wins { get; private set; }
    public int TotalPlayerDeaths { get; private set; }
    public float TotalBattleSeconds { get; private set; }
    public float WinRate=>CompletedRuns<=0?0f:(float)Wins/CompletedRuns;
    public float AverageBattleSeconds=>CompletedRuns<=0?0f:TotalBattleSeconds/CompletedRuns;
    public float AveragePlayerDeaths=>CompletedRuns<=0?0f:(float)TotalPlayerDeaths/CompletedRuns;

    public void Reset()
    {
        CompletedRuns=0;
        Wins=0;
        TotalPlayerDeaths=0;
        TotalBattleSeconds=0f;
    }

    public void Record(bool won,float battleSeconds,int playerDeaths)
    {
        CompletedRuns++;
        if(won)Wins++;
        TotalBattleSeconds+=Math.Max(0f,battleSeconds);
        TotalPlayerDeaths+=Math.Max(0,playerDeaths);
    }
}

public static class CombatGrowthCardCombatRules
{
    public static float OutgoingDamageMultiplier(
        CombatGrowthCardModifiers modifiers,int alliedDeathsThisWave,int alliesTargetingSameEnemy,
        float targetHealthRatio=1f,float battleSeconds=0f,int consecutiveTargetHits=0)
    {
        if(modifiers==null)return 1f;
        float multiplier=1f+
            modifiers.vengeanceAttackDamagePerDeath*Math.Max(0,alliedDeathsThisWave);
        int additionalAllies=Math.Max(0,alliesTargetingSameEnemy-1);
        if(additionalAllies>0&&modifiers.focusFireDamageBonus>0f)
            multiplier*=1f+modifiers.focusFireDamageBonus*additionalAllies;
        if(targetHealthRatio<=0.3f)multiplier*=1f+modifiers.executeDamageBonus;
        if(battleSeconds<=8f)multiplier*=1f+modifiers.openingDamageBonus;
        if(consecutiveTargetHits>0)
            multiplier*=1f+modifiers.persistentTargetDamagePerHit*consecutiveTargetHits;
        int longBattleIntervals=Math.Max(0,(int)Math.Floor(battleSeconds/5f));
        multiplier*=1f+modifiers.longBattleAttackDamagePerInterval*longBattleIntervals;
        return multiplier;
    }
}

public static class CombatGrowthCardCatalog
{
    private const string DatabaseResourcePath="CombatData/CombatGrowthCardDatabase";
    private static IReadOnlyList<CombatGrowthCardDefinition> loaded;
    private static readonly List<CombatGrowthCardDefinition> fallback=new List<CombatGrowthCardDefinition>
    {
        new CombatGrowthCardDefinition("growth_vitality","강인한 육체",
            CombatGrowthCardEffect.MaxHealth,8f,12f,18f,27f),
        new CombatGrowthCardDefinition("growth_fang","예리한 발톱",
            CombatGrowthCardEffect.AttackDamage,6f,9f,14f,21f),
        new CombatGrowthCardDefinition("growth_battle_rhythm","사냥의 리듬",
            CombatGrowthCardEffect.AttackSpeed,6f,9f,14f,21f),
        new CombatGrowthCardDefinition("growth_hardened_hide","두꺼운 가죽",
            CombatGrowthCardEffect.DamageReduction,4f,6f,9f,14f),
        new CombatGrowthCardDefinition("growth_survival_feast","약육강식",
            CombatGrowthCardEffect.KillHeal,5f,8f,12f,18f),
        new CombatGrowthCardDefinition("growth_last_stand","최후의 발악",
            CombatGrowthCardEffect.LowHealthAttackSpeed,12f,18f,27f,40f),
        new CombatGrowthCardDefinition("growth_focus_fire","집중 포화",
            CombatGrowthCardEffect.FocusFireDamage,8f,12f,18f,27f),
        new CombatGrowthCardDefinition("growth_vengeance","복수심",
            CombatGrowthCardEffect.VengeanceAttackDamage,6f,9f,14f,21f)
    };
    private static readonly List<CombatGrowthCardDefinition> approvedFallback=
        new List<CombatGrowthCardDefinition>
    {
        new CombatGrowthCardDefinition("growth_vitality","강인한 육체",CombatGrowthCardEffect.MaxHealth,8,12,18,27),
        new CombatGrowthCardDefinition("growth_fang","예리한 발톱",CombatGrowthCardEffect.AttackDamage,6,9,14,21),
        new CombatGrowthCardDefinition("growth_battle_rhythm","사냥의 리듬",CombatGrowthCardEffect.AttackSpeed,6,9,14,21),
        new CombatGrowthCardDefinition("growth_hardened_hide","두꺼운 가죽",CombatGrowthCardEffect.DamageReduction,4,6,9,14),
        new CombatGrowthCardDefinition("growth_iron_bones","단단한 골격",CombatGrowthCardEffect.Defense,6,10,15,23),
        new CombatGrowthCardDefinition("growth_unshaken_mind","흔들리지 않는 정신",CombatGrowthCardEffect.StatusResistanceRecoveryGuard,8,13,20,30,3,5,7,10),
        new CombatGrowthCardDefinition("growth_overflowing_magic","넘치는 마력",CombatGrowthCardEffect.SkillPower,8,12,18,27),
        new CombatGrowthCardDefinition("growth_fast_cycle","빠른 순환",CombatGrowthCardEffect.CooldownRecoverySpeed,8,13,20,30),
        new CombatGrowthCardDefinition("growth_focus_fire","집중 포화",CombatGrowthCardEffect.FocusFireDamage,3,4,6,9),
        new CombatGrowthCardDefinition("growth_weakness_exploit","약점 포착",CombatGrowthCardEffect.ExecuteDamage,10,15,23,35),
        new CombatGrowthCardDefinition("growth_first_strike","선제공격",CombatGrowthCardEffect.OpeningDamage,8,12,18,27),
        new CombatGrowthCardDefinition("growth_relentless_pursuit","끈질긴 추격",CombatGrowthCardEffect.PersistentTargetDamage,1,1.5f,2.2f,3.3f),
        new CombatGrowthCardDefinition("growth_victory_acceleration","승리의 가속",CombatGrowthCardEffect.KillTeamAttackSpeed,6,9,14,21),
        new CombatGrowthCardDefinition("growth_regeneration","재생 본능",CombatGrowthCardEffect.MissingHealthRegeneration,4,6,9,14),
        new CombatGrowthCardDefinition("growth_battle_preparation","전투 준비",CombatGrowthCardEffect.OpeningShield,8,12,18,27),
        new CombatGrowthCardDefinition("growth_last_defense","최후의 방어",CombatGrowthCardEffect.LowHealthDamageReduction,8,12,18,27),
        new CombatGrowthCardDefinition("growth_last_stand","최후의 발악",CombatGrowthCardEffect.LowHealthAttackSpeed,12,18,27,40),
        new CombatGrowthCardDefinition("growth_vengeance","복수심",CombatGrowthCardEffect.VengeanceAttackDamage,6,9,14,21),
        new CombatGrowthCardDefinition("growth_unyielding_bond","불굴의 결속",CombatGrowthCardEffect.DeathShield,5,8,12,18),
        new CombatGrowthCardDefinition("growth_long_battle","장기전 태세",CombatGrowthCardEffect.LongBattleAttackDamage,2,3,4.5f,7),
        new CombatGrowthCardDefinition("growth_survival_feast","약육강식",CombatGrowthCardEffect.TeamKillMissingHealthHeal,3,4.5f,7,10),
        new CombatGrowthCardDefinition("growth_life_cycle","생명의 순환",CombatGrowthCardEffect.SkillCycleHeal,3,4.5f,7,10)
    };

    public static IReadOnlyList<CombatGrowthCardDefinition> All
    {
        get
        {
            EnsureLoaded();
            return loaded;
        }
    }

    public static CombatGrowthCardDefinition Get(string cardId)
    {
        EnsureLoaded();
        return loaded.FirstOrDefault(card=>string.Equals(card.id,cardId,StringComparison.Ordinal));
    }

    public static void ReloadFromResources()
    {
        loaded=null;
        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if(loaded!=null)return;
        CombatGrowthCardDatabase database=
            UnityEngine.Resources.Load<CombatGrowthCardDatabase>(DatabaseResourcePath);
        if(database==null||database.cards==null)
        {
            loaded=approvedFallback;
            return;
        }

        List<CombatGrowthCardDefinition> definitions=new List<CombatGrowthCardDefinition>();
        HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(CombatGrowthCardDatabaseRow row in database.cards)
        {
            if(row==null||!row.enabled||string.IsNullOrWhiteSpace(row.cardId)||!ids.Add(row.cardId))continue;
            definitions.Add(new CombatGrowthCardDefinition(
                row.cardId,row.displayName,row.effect,
                row.commonValue,row.rareValue,row.epicValue,row.legendaryValue,
                row.commonSecondaryValue,row.rareSecondaryValue,
                row.epicSecondaryValue,row.legendarySecondaryValue));
        }
        loaded=definitions.Count>=CombatGrowthCardDraftService.DraftSize?definitions:approvedFallback;
    }

    public static string RarityDisplayName(CombatGrowthCardRarity rarity)
    {
        if(rarity==CombatGrowthCardRarity.Rare)return "희귀";
        if(rarity==CombatGrowthCardRarity.Epic)return "에픽";
        if(rarity==CombatGrowthCardRarity.Legendary)return "전설";
        return "일반";
    }

    public static CombatGrowthCardModifiers Aggregate(IEnumerable<CombatGrowthCardSelectionSave> selections)
    {
        CombatGrowthCardModifiers result=new CombatGrowthCardModifiers();
        if(selections==null)return result;
        foreach(CombatGrowthCardSelectionSave selection in selections)
        {
            if(selection==null)continue;
            CombatGrowthCardDefinition definition=Get(selection.cardId);
            if(definition==null)continue;
            float ratio=definition.Value(selection.rarity)*0.01f;
            if(definition.effect==CombatGrowthCardEffect.MaxHealth)result.maxHealthBonus+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.AttackDamage)result.attackDamageBonus+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.AttackSpeed)result.attackSpeedBonus+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.DamageReduction)
                result.damageTakenMultiplier*=1f-ratio;
            else if(definition.effect==CombatGrowthCardEffect.LowHealthAttackSpeed)
                result.lowHealthAttackSpeedBonus+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.FocusFireDamage)
                result.focusFireDamageBonus+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.VengeanceAttackDamage)
                result.vengeanceAttackDamagePerDeath+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.Defense)
                result.defenseBonus+=definition.Value(selection.rarity);
            else if(definition.effect==CombatGrowthCardEffect.StatusResistanceRecoveryGuard)
            {
                result.statusResistanceBonus+=definition.Value(selection.rarity);
                result.statusRecoveryDamageTakenMultiplier*=
                    1f-definition.SecondaryValue(selection.rarity)*0.01f;
            }
            else if(definition.effect==CombatGrowthCardEffect.SkillPower)result.skillPowerBonus+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.CooldownRecoverySpeed)result.cooldownRecoverySpeed+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.ExecuteDamage)result.executeDamageBonus+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.OpeningDamage)result.openingDamageBonus+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.PersistentTargetDamage)
                result.persistentTargetDamagePerHit+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.KillTeamAttackSpeed)
                result.killTeamAttackSpeedBonus+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.MissingHealthRegeneration)
                result.missingHealthRegenerationRatio+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.OpeningShield)result.openingShieldRatio+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.LowHealthDamageReduction)
                result.lowHealthDamageTakenMultiplier*=1f-ratio;
            else if(definition.effect==CombatGrowthCardEffect.DeathShield)result.deathShieldRatio+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.LongBattleAttackDamage)
                result.longBattleAttackDamagePerInterval+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.TeamKillMissingHealthHeal)
                result.teamKillMissingHealthHealRatio+=ratio;
            else if(definition.effect==CombatGrowthCardEffect.SkillCycleHeal)
                result.skillCycleHealRatio+=ratio;
        }
        return result;
    }
}

public static class CombatGrowthCardDraftService
{
    public static int DraftSize=>GameMasterData.ExpeditionInt("growth_card_draft_size",3);

    public static bool IsRewardAfterWave(int clearedWave)
    {
        return clearedWave>0&&
               clearedWave<GameMasterData.ExpeditionInt("wave_count",12)&&
               !ExpeditionAugmentSystem.IsOfferWave(clearedWave+1);
    }

    public static List<CombatGrowthCardDraftEntry> CreateDraft(int wave,int seed)
    {
        List<CombatGrowthCardDefinition> pool=CombatGrowthCardCatalog.All.ToList();
        if(pool.Count<DraftSize)throw new InvalidOperationException("Not enough combat growth cards.");
        Random random=new Random(seed);
        bool sharedRarity=GameMasterData.ExpeditionBool(
            "growth_card_shared_draft_rarity",true);
        CombatGrowthCardRarity rarity=sharedRarity
            ?RollRarity(wave,random)
            :CombatGrowthCardRarity.Common;
        List<CombatGrowthCardDraftEntry> result=new List<CombatGrowthCardDraftEntry>(DraftSize);
        for(int i=0;i<DraftSize;i++)
        {
            int index=random.Next(pool.Count);
            CombatGrowthCardDefinition definition=pool[index];
            pool.RemoveAt(index);
            result.Add(new CombatGrowthCardDraftEntry
            {
                cardId=definition.id,
                rarity=sharedRarity?rarity:RollRarity(wave,random)
            });
        }
        return result;
    }

    public static CombatGrowthCardRarity RollRarity(int wave,Random random)
    {
        if(random==null)throw new ArgumentNullException(nameof(random));
        GrowthCardRarityMasterRow master=GameMasterData.GrowthRarity(wave);
        if(master!=null)
        {
            double roll01=random.NextDouble();
            double common=Math.Max(0d,master.commonWeight);
            double rare=common+Math.Max(0d,master.rareWeight);
            double epic=rare+Math.Max(0d,master.epicWeight);
            double total=epic+Math.Max(0d,master.legendaryWeight);
            if(total>0d)
            {
                roll01*=total;
                if(roll01<common)return CombatGrowthCardRarity.Common;
                if(roll01<rare)return CombatGrowthCardRarity.Rare;
                if(roll01<epic)return CombatGrowthCardRarity.Epic;
                return CombatGrowthCardRarity.Legendary;
            }
        }

        int roll=random.Next(10000);
        if(wave<=3)
        {
            if(roll<7400)return CombatGrowthCardRarity.Common;
            if(roll<9600)return CombatGrowthCardRarity.Rare;
            if(roll<9900)return CombatGrowthCardRarity.Epic;
            return CombatGrowthCardRarity.Legendary;
        }
        if(wave<=6)
        {
            if(roll<5500)return CombatGrowthCardRarity.Common;
            if(roll<8700)return CombatGrowthCardRarity.Rare;
            if(roll<9800)return CombatGrowthCardRarity.Epic;
            return CombatGrowthCardRarity.Legendary;
        }
        if(wave<=9)
        {
            if(roll<3500)return CombatGrowthCardRarity.Common;
            if(roll<7300)return CombatGrowthCardRarity.Rare;
            if(roll<9500)return CombatGrowthCardRarity.Epic;
            return CombatGrowthCardRarity.Legendary;
        }
        if(roll<1500)return CombatGrowthCardRarity.Common;
        if(roll<5000)return CombatGrowthCardRarity.Rare;
        if(roll<8500)return CombatGrowthCardRarity.Epic;
        return CombatGrowthCardRarity.Legendary;
    }
}
