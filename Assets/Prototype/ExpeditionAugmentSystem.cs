using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public enum CorruptionRollOutcome
{
    GreatSuccess,
    Success,
    NoChange,
    Failure
}

public static class ExpeditionAugmentSystem
{
    public static int ChoiceCount
    {
        get{return Math.Max(1,GameMasterData.ExpeditionInt("augment_choice_count",3));}
    }

    public static int FreeRerollCount
    {
        get{return Math.Max(0,GameMasterData.ExpeditionInt("augment_free_reroll_count",1));}
    }

    public static bool IsOfferWave(int wave)
    {
        string raw=GameMasterData.ExpeditionString("augment_offer_waves","1|7");
        return raw.Split('|').Any(value=>
            int.TryParse(value.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out int parsed)&&
            parsed==wave);
    }

    public static IReadOnlyList<string> CreateOrRestoreDraft(
        ExpeditionRunSave run,int seed,bool forceReroll=false)
    {
        if(run==null)throw new ArgumentNullException(nameof(run));
        EnsureCollections(run);
        if(!IsOfferWave(run.wave))return Array.Empty<string>();

        bool sameDraft=run.augmentDraftWave==run.wave&&
                       run.augmentDraftIds.Count==ChoiceCount;
        if(sameDraft&&!forceReroll)return run.augmentDraftIds;

        if(run.augmentDraftWave!=run.wave)
        {
            run.augmentDraftWave=run.wave;
            run.augmentRerollsRemaining=FreeRerollCount;
        }
        else if(forceReroll)
        {
            if(run.augmentRerollsRemaining<=0)return run.augmentDraftIds;
            run.augmentRerollsRemaining--;
        }

        HashSet<string> selected=new HashSet<string>(
            run.augments.Where(value=>value!=null&&!string.IsNullOrWhiteSpace(value.augmentId))
                .Select(value=>value.augmentId),StringComparer.Ordinal);
        List<AugmentMasterRow> pool=GameMasterData.Augments
            .Where(row=>ExpeditionAugmentRuntime.Supports(row.id))
            .Where(row=>row.IsOfferedAt(run.wave)&&!selected.Contains(row.id))
            .Where(row=>!ConflictsWithSelected(row,selected))
            .Where(row=>IsEligible(run,row))
            .OrderBy(row=>row.id,StringComparer.Ordinal)
            .ToList();

        Random random=new Random(seed);
        for(int i=pool.Count-1;i>0;i--)
        {
            int swap=random.Next(i+1);
            AugmentMasterRow temp=pool[i];
            pool[i]=pool[swap];
            pool[swap]=temp;
        }
        run.augmentDraftIds=pool.Take(ChoiceCount).Select(row=>row.id).ToList();
        return run.augmentDraftIds;
    }

    public static bool TrySelect(
        ExpeditionRunSave run,string augmentId,string targetMonsterInstanceId,
        string targetItemInstanceId,int deterministicSeed,out string error)
    {
        error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        EnsureCollections(run);
        if(run.augmentDraftWave!=run.wave||!run.augmentDraftIds.Contains(augmentId))
        {error="현재 제시된 증강이 아닙니다.";return false;}
        AugmentMasterRow master=GameMasterData.Augment(augmentId);
        if(master==null||!IsEligible(run,master))
        {error="현재 선택할 수 없는 증강입니다.";return false;}
        if(run.augments.Any(value=>value!=null&&value.augmentId==augmentId))
        {error="동일 증강은 중복 선택할 수 없습니다.";return false;}
        HashSet<string> selected=new HashSet<string>(
            run.augments.Where(value=>value!=null).Select(value=>value.augmentId),
            StringComparer.Ordinal);
        if(ConflictsWithSelected(master,selected))
        {error="이미 선택한 증강과 충돌합니다.";return false;}

        bool needsMonster=master.selectionMode.IndexOf("Monster",StringComparison.OrdinalIgnoreCase)>=0;
        if(needsMonster&&!CanTargetMonster(
               run,augmentId,targetMonsterInstanceId))
        {
            error=augmentId==ExpeditionAugmentRuntime.EarlyAwakening
                ?"이미 3성에 도달한 몬스터는 조기 각성할 수 없습니다."
                :"증강을 적용할 몬스터가 필요합니다.";
            return false;
        }
        if(!string.IsNullOrEmpty(targetItemInstanceId)&&
           !run.itemInstances.Any(value=>value!=null&&value.instanceId==targetItemInstanceId))
        {error="증강을 적용할 아이템을 찾을 수 없습니다.";return false;}

        ExpeditionAugmentSelectionSave selectionSave=new ExpeditionAugmentSelectionSave
        {
            augmentId=augmentId,
            acquiredWave=run.wave,
            targetMonsterInstanceId=targetMonsterInstanceId??"",
            targetItemInstanceId=targetItemInstanceId??"",
            deterministicSeed=deterministicSeed
        };
        run.augments.Add(selectionSave);
        ExpeditionAugmentRuntime.ApplyOnAcquire(
            run,selectionSave,new Random(deterministicSeed));
        run.augmentDraftIds.Clear();
        return true;
    }

    public static bool Has(ExpeditionRunSave run,string augmentId)
    {
        return run!=null&&run.augments!=null&&
               run.augments.Any(value=>value!=null&&value.augmentId==augmentId);
    }

    public static bool CanTargetMonster(
        ExpeditionRunSave run,string augmentId,string monsterInstanceId)
    {
        ExpeditionMonsterStateSave monster=run==null||run.monsters==null?null:
            run.monsters.FirstOrDefault(value=>value!=null&&
                value.instanceId==monsterInstanceId);
        if(monster==null)return false;
        if(augmentId==ExpeditionAugmentRuntime.TranscendenceGene)
            return !monster.temporary;
        return augmentId!=ExpeditionAugmentRuntime.EarlyAwakening||
               ExpeditionGrowthSystem.RankForGrowth(
                   monster.monsterId,monster.growthTotal)<
               ExpeditionMonsterRank.Rank3;
    }

    private static bool ConflictsWithSelected(AugmentMasterRow candidate,HashSet<string> selected)
    {
        if(candidate.ConflictIds().Any(selected.Contains))return true;
        foreach(string selectedId in selected)
        {
            AugmentMasterRow row=GameMasterData.Augment(selectedId);
            if(row!=null&&row.ConflictIds().Contains(candidate.id))return true;
        }
        return false;
    }

    private static bool IsEligible(ExpeditionRunSave run,AugmentMasterRow row)
    {
        if(row==null||!row.enabled)return false;
        if(row.id=="augment_life_mortgage"&&run.lives<2)return false;
        if(row.id==ExpeditionAugmentRuntime.LifeExpansion&&run.lives>=Math.Max(1,
           (int)Math.Round(row.NumericParam("max_lives",5f))))return false;
        if(row.id==ExpeditionAugmentRuntime.AutoDeployment&&
           run.maxDeploymentCount>=ExpeditionDeploymentSystem.MaximumLimit)return false;
        if(row.id=="augment_giant_sigil"&&run.maxDeploymentCount<2)return false;
        if(row.id==ExpeditionAugmentRuntime.TwinCommand&&
           (run.maxDeploymentCount<2||run.monsters.Count(value=>value!=null)<2))
            return false;
        if(row.id==ExpeditionAugmentRuntime.CombinedCast&&
           run.monsters.Count(value=>value!=null)<2)
            return false;
        if(row.id==ExpeditionAugmentRuntime.ReserveReinforcement&&
           run.monsters.Count(value=>value!=null)>=
           GameMasterData.ExpeditionInt("roster_size",8)+1)
            return false;
        if(row.id==ExpeditionAugmentRuntime.EarlyAwakening&&
           !run.monsters.Any(value=>value!=null&&
               ExpeditionGrowthSystem.RankForGrowth(
                   value.monsterId,value.growthTotal)<ExpeditionMonsterRank.Rank3))
            return false;
        if(row.id==ExpeditionAugmentRuntime.TranscendenceGene&&
           !run.monsters.Any(value=>value!=null&&!value.temporary))
            return false;
        if(row.id=="augment_corruption_catalyst")
        {
            return run.itemInstances.Any(value=>value!=null&&
                !value.transformLocked&&
                value.transformState==ExpeditionItemTransformState.None&&
                GameMasterData.Item(value.itemId)?.IsCompleted==true&&
                GameMasterData.ItemTransform(value.itemId)!=null);
        }
        return true;
    }

    private static void EnsureCollections(ExpeditionRunSave run)
    {
        if(run.augments==null)run.augments=new List<ExpeditionAugmentSelectionSave>();
        if(run.augmentDraftIds==null)run.augmentDraftIds=new List<string>();
        if(run.monsters==null)run.monsters=new List<ExpeditionMonsterStateSave>();
        if(run.itemInstances==null)run.itemInstances=new List<ExpeditionItemInstanceSave>();
        if(run.specialItems==null)run.specialItems=new List<ExpeditionSpecialItemSave>();
    }
}

public static class ExpeditionItemTransformSystem
{
    public static bool TryApplyCorruption(
        ExpeditionRunSave run,string itemInstanceId,int deterministicSeed,
        out CorruptionRollOutcome outcome,out string error)
    {
        outcome=CorruptionRollOutcome.NoChange;
        if(!TryGetTransformable(run,itemInstanceId,out ExpeditionItemInstanceSave item,
            out ItemTransformMasterRow transform,out error))return false;
        outcome=RollCorruption(transform,new Random(deterministicSeed));
        item.transformSeed=deterministicSeed;
        if(outcome==CorruptionRollOutcome.GreatSuccess)
            item.transformState=ExpeditionItemTransformState.CorruptedGreatSuccess;
        else if(outcome==CorruptionRollOutcome.Success)
            item.transformState=ExpeditionItemTransformState.CorruptedSuccess;
        else if(outcome==CorruptionRollOutcome.Failure)
            item.transformState=ExpeditionItemTransformState.CorruptedFailure;
        item.transformLocked=outcome!=CorruptionRollOutcome.NoChange;
        return true;
    }

    public static CorruptionRollOutcome RollCorruption(
        ItemTransformMasterRow row,Random random)
    {
        if(row==null)throw new ArgumentNullException(nameof(row));
        if(random==null)throw new ArgumentNullException(nameof(random));
        int total=row.greatSuccessPercent+row.successPercent+
                  row.noChangePercent+row.failurePercent;
        if(total!=100)throw new InvalidOperationException("Corruption probability total must be 100.");
        int roll=random.Next(100);
        if(roll<row.greatSuccessPercent)return CorruptionRollOutcome.GreatSuccess;
        roll-=row.greatSuccessPercent;
        if(roll<row.successPercent)return CorruptionRollOutcome.Success;
        roll-=row.successPercent;
        if(roll<row.noChangePercent)return CorruptionRollOutcome.NoChange;
        return CorruptionRollOutcome.Failure;
    }

    public static ItemMasterRow ResolveEffectiveMaster(ExpeditionItemInstanceSave instance)
    {
        ItemMasterRow source=instance==null?null:GameMasterData.Item(instance.itemId);
        if(source==null)return null;
        ItemMasterRow resolved=Copy(source);
        if(instance.transformState==ExpeditionItemTransformState.Radiant)
        {
            ApplyOvercharge(resolved);
            resolved.displayName="찬란한 "+source.displayName;
        }
        else if(instance.transformState==ExpeditionItemTransformState.Overcharged||
           instance.transformState==ExpeditionItemTransformState.CorruptedSuccess||
           instance.transformState==ExpeditionItemTransformState.CorruptedGreatSuccess)
            ApplyOvercharge(resolved);
        else if(instance.transformState==ExpeditionItemTransformState.CorruptedFailure)
            ApplyFailure(resolved);
        return resolved;
    }

    public static string DisplayName(ExpeditionItemInstanceSave instance)
    {
        ItemMasterRow item=instance==null?null:GameMasterData.Item(instance.itemId);
        if(item==null)return "";
        ItemTransformMasterRow transform=GameMasterData.ItemTransform(item.id);
        if(instance.transformState==ExpeditionItemTransformState.Radiant)
            return "찬란한 "+item.displayName;
        if(instance.transformState==ExpeditionItemTransformState.Overcharged)
            return "과충전된 "+item.displayName;
        if(instance.transformState==ExpeditionItemTransformState.CorruptedGreatSuccess)
            return transform==null||string.IsNullOrWhiteSpace(transform.corruptionGreatName)
                ?"타락한 "+item.displayName:transform.corruptionGreatName;
        if(instance.transformState==ExpeditionItemTransformState.CorruptedSuccess)
            return "타락한 "+item.displayName;
        if(instance.transformState==ExpeditionItemTransformState.CorruptedFailure)
            return "불완전 타락 "+item.displayName;
        return item.displayName;
    }

    private static bool TryGetTransformable(
        ExpeditionRunSave run,string itemInstanceId,out ExpeditionItemInstanceSave item,
        out ItemTransformMasterRow transform,out string error)
    {
        item=null;transform=null;error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        item=run.itemInstances==null?null:
            run.itemInstances.FirstOrDefault(value=>value!=null&&value.instanceId==itemInstanceId);
        ItemMasterRow master=item==null?null:GameMasterData.Item(item.itemId);
        if(item==null||master==null){error="아이템을 찾을 수 없습니다.";return false;}
        if(!master.IsCompleted){error="완성 아이템만 변형할 수 있습니다.";return false;}
        if(item.transformLocked){error="이 아이템은 변형할 수 없습니다.";return false;}
        if(item.transformState!=ExpeditionItemTransformState.None)
        {error="이미 변형된 아이템입니다.";return false;}
        transform=GameMasterData.ItemTransform(master.id);
        if(transform==null){error="아이템 변형 데이터가 없습니다.";return false;}
        return true;
    }

    private static ItemMasterRow Copy(ItemMasterRow source)
    {
        return new ItemMasterRow
        {
            id=source.id,displayName=source.displayName,itemKind=source.itemKind,
            componentAId=source.componentAId,componentBId=source.componentBId,
            itemPoolId=source.itemPoolId,attackDamageBonus=source.attackDamageBonus,
            attackSpeedBonus=source.attackSpeedBonus,maxHealthBonus=source.maxHealthBonus,
            damageReductionBonus=source.damageReductionBonus,criticalChance=source.criticalChance,
            criticalDamageBonus=source.criticalDamageBonus,skillPowerBonus=source.skillPowerBonus,
            defenseBonus=source.defenseBonus,statusResistanceBonus=source.statusResistanceBonus,
            skillCooldownReductionBonus=source.skillCooldownReductionBonus,
            specialEffect=source.specialEffect,effectParam1=source.effectParam1,
            effectParam2=source.effectParam2,effectParam3=source.effectParam3,
            enabled=source.enabled,dataVersion=source.dataVersion
        };
    }

    private static void ApplyFailure(ItemMasterRow item)
    {
        const float multiplier=0.75f;
        item.attackDamageBonus*=multiplier;
        item.attackSpeedBonus*=multiplier;
        item.maxHealthBonus*=multiplier;
        item.damageReductionBonus*=multiplier;
        item.criticalChance*=multiplier;
        item.criticalDamageBonus*=multiplier;
        item.skillPowerBonus*=multiplier;
        item.defenseBonus*=multiplier;
        item.statusResistanceBonus*=multiplier;
        item.skillCooldownReductionBonus*=multiplier;
        item.effectParam1*=multiplier;
        item.effectParam2*=multiplier;
        item.effectParam3*=multiplier;

        if(item.id=="complete_vengeance_armor")
            item.effectParam1=(float)Math.Ceiling(4f*1.25f);
        else if(item.id=="complete_spirit_king_talisman")
            item.effectParam2=(float)Math.Ceiling(2f*1.25f);
        else if(item.id=="complete_vampiric_feather")
            item.effectParam1=(float)Math.Ceiling(5f*1.25f);
        else if(item.id=="complete_archmage_clock")
            item.effectParam2=(float)Math.Ceiling(4f*1.25f);
        else if(item.id=="complete_redemption_heart")
            item.effectParam2=(float)Math.Ceiling(6f*1.25f);
    }

    private static void ApplyOvercharge(ItemMasterRow item)
    {
        switch(item.id)
        {
            case "complete_tyrant_fang":item.effectParam1=0.12f;break;
            case "complete_storm_feather":item.effectParam1=0.026f;break;
            case "complete_ancient_core":item.effectParam1=0.065f;break;
            case "complete_eternal_hourglass":item.effectParam1=0.45f;break;
            case "complete_immortal_carapace":item.effectParam1=0.108f;break;
            case "complete_spirit_king_talisman":item.effectParam1=0.027f;break;
            case "complete_giant_heart":item.maxHealthBonus=0.45f;break;
            case "complete_slaughter_crystal":item.criticalChance=0.30f;break;
            case "complete_berserker_claw":item.effectParam1=0.0195f;break;
            case "complete_magic_blade_core":item.effectParam1=0.135f;break;
            case "complete_hunter_gear":item.effectParam1=0.81f;break;
            case "complete_vengeance_armor":item.effectParam1=3f;break;
            case "complete_blood_talisman":item.effectParam1=0.108f;item.effectParam3=0.338f;break;
            case "complete_predator_heart":item.effectParam1=0.135f;break;
            case "complete_executioner_eye":item.criticalDamageBonus=0.525f;break;
            case "complete_magic_accelerator":item.effectParam1=0.338f;break;
            case "complete_swift_hourglass":item.effectParam1=0.203f;break;
            case "complete_battle_carapace":item.effectParam1=0.013f;break;
            case "complete_purifying_feather":item.effectParam1=0.473f;break;
            case "complete_vampiric_feather":item.effectParam2=0.0675f;break;
            case "complete_eye_of_storm":item.effectParam1=0.27f;break;
            case "complete_archmage_clock":item.effectParam1=0.104f;break;
            case "complete_crown_carapace":item.effectParam1=0.203f;item.effectParam3=0.27f;break;
            case "complete_resonance_talisman":item.effectParam2=20.25f;break;
            case "complete_plague_core":item.effectParam1=0.02025f;item.effectParam3=0.405f;break;
            case "complete_chaos_crystal_core":item.effectParam1=0.35f;break;
            case "complete_guardian_hourglass":item.effectParam2=0.27f;item.effectParam3=0.405f;break;
            case "complete_cycle_talisman":item.effectParam1=13.5f;item.effectParam2=13.5f;break;
            case "complete_redemption_heart":item.effectParam1=0.081f;break;
            case "complete_fate_crystal_clock":item.effectParam1=0.203f;item.effectParam2=0.162f;break;
            case "complete_ironwall_charm":item.effectParam1=5.4f;item.effectParam2=5.4f;break;
            case "complete_sun_carapace":item.effectParam1=0.02025f;item.effectParam3=0.405f;break;
            case "complete_unyielding_mirror":item.effectParam1=0.108f;item.effectParam2=0.203f;break;
            case "complete_twilight_shroud":item.effectParam1=0.203f;item.effectParam2=13.5f;item.effectParam3=13.5f;break;
            case "complete_liberation_crystal":item.effectParam1=7.5f;item.effectParam2=0.054f;break;
            case "complete_blood_crystal":item.effectParam1=0.027f;break;
        }
    }
}
