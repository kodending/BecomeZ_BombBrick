using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ExpeditionAugmentCombatModifiers
{
    public int deploymentSlots=1;
    public float maxHealthBonus;
    public float attackDamageBonus;
    public float attackSpeedBonus;
    public float defenseBonus;
    public float statusResistanceBonus;
    public float defenseMultiplier=1f;
    public float statusResistanceMultiplier=1f;
    public float harmfulDurationMultiplier=1f;
    public float damageReduction;
    public float basicAttackDamageMultiplier=1f;
    public float skillCooldownMultiplier=1f;
    public float minimumSkillCooldown;
    public float skillPowerMultiplier=1f;
    public float skillAreaMultiplier=1f;
    public float modelScaleMultiplier=1f;
    public bool protagonist;
    public bool transcended;
    public bool returnMarked;
    public float returnDelay=0.5f;
    public float returnHealthRatio=0.5f;
    public float returnInvulnerableSeconds=1f;
    public float returnSkillChargeRatio=0.5f;
    public bool combinedCaster;
}

public static class ExpeditionAugmentRuntime
{
    private static readonly int[,] FickleBlessingOrders=
    {
        {0,1,2},{0,2,1},{1,0,2},
        {1,2,0},{2,0,1},{2,1,0}
    };

    public const string EarlyAwakening="augment_early_awakening";
    public const string TwinCommand="augment_twin_command";
    public const string CombinedCast="augment_combined_cast";
    public const string DoubleSupply="augment_double_supply";
    public const string PandoraWorkshop="augment_pandora_workshop";
    public const string ReserveReinforcement="augment_reserve_reinforcement";
    public const string FickleBlessing="augment_fickle_blessing";
    public const string ReturnMark="augment_return_mark";
    public const string TimeCompression="augment_time_compression";
    public const string GiantSigil="augment_giant_sigil";
    public const string CorruptionCatalyst="augment_corruption_catalyst";
    public const string ShadowClone="augment_shadow_clone";
    public const string LegacyInheritance="augment_legacy_inheritance";
    public const string Protagonist="augment_protagonist";
    public const string TranscendenceGene="augment_transcendence_gene";
    public const string LastBlessing="augment_last_blessing";
    public const string AutoDeployment="augment_auto_deployment";
    public const string FieldLoan="augment_field_loan";
    public const string LifeMortgage="augment_life_mortgage";
    public const string LifeExpansion="augment_life_expansion";
    private static readonly HashSet<string> supportedIds=new HashSet<string>(
        new[]
        {
            EarlyAwakening,TwinCommand,CombinedCast,DoubleSupply,
            PandoraWorkshop,ReserveReinforcement,FickleBlessing,
            ReturnMark,TimeCompression,GiantSigil,
            CorruptionCatalyst,ShadowClone,LegacyInheritance,
            Protagonist,TranscendenceGene,LastBlessing,AutoDeployment,FieldLoan,
            LifeMortgage,LifeExpansion
        },StringComparer.Ordinal);

    public static bool Supports(string augmentId)
    {
        return !string.IsNullOrEmpty(augmentId)&&supportedIds.Contains(augmentId);
    }

    public static float ReduceRemainingCooldown(float remaining,float ratio)
    {
        return Math.Max(0f,remaining*(1f-Math.Max(0f,Math.Min(1f,ratio))));
    }

    public static float ChargeSkillCooldown(float remaining,float fullCooldown,float ratio)
    {
        float charge=Math.Max(0f,fullCooldown)*Math.Max(0f,Math.Min(1f,ratio));
        return Math.Max(0f,remaining-charge);
    }

    public static float AddCombinedCastCooldown(
        float remaining,int additionalCasts,float secondsPerCast,float maximum)
    {
        float added=Math.Max(0,additionalCasts)*Math.Max(0f,secondsPerCast);
        return Math.Min(Math.Max(0f,maximum),Math.Max(0f,remaining)+added);
    }

    public static int ReserveReinforcementGrowth(int acquiredWave)
    {
        return Math.Max(1,Math.Min(9,acquiredWave));
    }

    public static float ProtagonistParam(string key,float fallback)
    {
        AugmentMasterRow master=GameMasterData.Augment(Protagonist);
        return master==null?fallback:master.NumericParam(key,fallback);
    }

    public static float TranscendenceParam(string key,float fallback)
    {
        AugmentMasterRow master=GameMasterData.Augment(TranscendenceGene);
        return master==null?fallback:master.NumericParam(key,fallback);
    }

    public static ExpeditionAugmentSelectionSave Selection(
        ExpeditionRunSave run,string augmentId)
    {
        if(run==null||run.augments==null)return null;
        foreach(var value in run.augments)
            if(value!=null&&value.augmentId==augmentId)return value;
        return null;
    }

    public static bool TryGrantLastBlessingReward(
        ExpeditionRunSave run,out int gold,out ItemMasterRow grantedItem)
    {
        gold=0;
        grantedItem=null;
        ExpeditionAugmentSelectionSave blessing=Selection(run,LastBlessing);
        if(run==null||blessing==null||blessing.triggered||run.lives!=1)return false;

        EnsureCollections(run);
        blessing.triggered=true;
        AugmentMasterRow master=GameMasterData.Augment(LastBlessing);
        gold=Math.Max(0,(int)Math.Round(master==null?20f:
            master.NumericParam("currency_reward",20f)));
        run.expeditionCurrency+=gold;

        List<ItemMasterRow> completed=GameMasterData.Database==null
            ?new List<ItemMasterRow>()
            :GameMasterData.Database.items.Where(item=>
                item!=null&&item.enabled&&item.IsCompleted&&
                item.itemPoolId=="default_equipment").OrderBy(item=>item.id).ToList();
        if(completed.Count>0)
        {
            int seed=MonsterInstanceGenerator.StableSeed(
                run.runId+"_last_blessing_item_"+run.wave);
            grantedItem=completed[(seed&int.MaxValue)%completed.Count];
            ExpeditionItemSystem.CreateItem(run,grantedItem.id,true);
        }
        return true;
    }

    public static bool BlocksDeploymentPurchase(ExpeditionRunSave run)
    {
        return ExpeditionAugmentSystem.Has(run,AutoDeployment);
    }

    public static bool BlocksGrowthShop(
        ExpeditionRunSave run,string monsterInstanceId,int wave)
    {
        ExpeditionAugmentSelectionSave early=Selection(run,EarlyAwakening);
        return Targets(early,monsterInstanceId)&&wave>early.acquiredWave&&
               wave<=early.acquiredWave+3;
    }

    public static ExpeditionSpecialItemSave GrantSpecialItem(
        ExpeditionRunSave run,string itemId,int count=1,bool temporary=false,
        bool occupiesItemSlot=false)
    {
        if(run==null||string.IsNullOrWhiteSpace(itemId)||count<=0)return null;
        EnsureCollections(run);
        ExpeditionSpecialItemSave first=null;
        for(int i=0;i<count;i++)
        {
            ExpeditionSpecialItemSave created=new ExpeditionSpecialItemSave
            {
                instanceId="special_"+Guid.NewGuid().ToString("N"),
                itemId=itemId,
                count=1,
                temporary=temporary,
                occupiesItemSlot=occupiesItemSlot
            };
            run.specialItems.Add(created);
            if(first==null)first=created;
        }
        return first;
    }

    public static bool TryConsumeSpecialItem(
        ExpeditionRunSave run,string itemId,out string error)
    {
        error="";
        EnsureCollections(run);
        ExpeditionSpecialItemSave stack=run.specialItems.FirstOrDefault(value=>
            value!=null&&value.itemId==itemId&&value.count>0&&
            string.IsNullOrEmpty(value.boundMonsterInstanceId));
        if(stack==null){error="사용 가능한 특수 아이템이 없습니다.";return false;}
        return TryConsumeSpecialItemInstance(run,stack.instanceId,out error);
    }

    public static bool TryConsumeSpecialItemInstance(
        ExpeditionRunSave run,string instanceId,out string error)
    {
        error="";
        EnsureCollections(run);
        ExpeditionSpecialItemSave item=run.specialItems.FirstOrDefault(value=>
            value!=null&&value.instanceId==instanceId&&value.count>0);
        if(item==null){error="사용할 특수 아이템을 찾을 수 없습니다.";return false;}
        if(!string.IsNullOrEmpty(item.boundMonsterInstanceId))
        {error="장착 중인 특수 아이템은 소모할 수 없습니다.";return false;}
        run.specialItems.Remove(item);
        return true;
    }

    public static bool TryBindSpecialItem(
        ExpeditionRunSave run,string specialInstanceId,string monsterInstanceId,
        out string error)
    {
        error="";
        EnsureCollections(run);
        ExpeditionSpecialItemSave item=run.specialItems.FirstOrDefault(value=>
            value!=null&&value.instanceId==specialInstanceId&&value.count>0);
        if(item==null){error="특수 아이템을 찾을 수 없습니다.";return false;}
        if(!run.monsters.Any(value=>value!=null&&value.instanceId==monsterInstanceId))
        {error="대상 몬스터를 찾을 수 없습니다.";return false;}
        item.boundMonsterInstanceId=monsterInstanceId;
        return true;
    }

    public static bool TryApplyPermanentMonsterSpecialItem(
        ExpeditionRunSave run,string specialInstanceId,string monsterInstanceId,
        out string error)
    {
        error="";
        EnsureCollections(run);
        ExpeditionSpecialItemSave item=run.specialItems.FirstOrDefault(value=>
            value!=null&&value.instanceId==specialInstanceId&&value.count>0);
        if(item==null){error="특수 아이템을 찾을 수 없습니다.";return false;}
        ExpeditionMonsterStateSave monster=run.monsters.FirstOrDefault(value=>
            value!=null&&value.instanceId==monsterInstanceId);
        if(monster==null){error="대상 몬스터를 찾을 수 없습니다.";return false;}
        EnsureMonsterSpecialMarks(monster);
        if(monster.appliedSpecialItemIds.Contains(item.itemId))
        {error="이미 같은 표식이 적용된 몬스터입니다.";return false;}
        monster.appliedSpecialItemIds.Add(item.itemId);
        run.specialItems.Remove(item);
        return true;
    }

    public static bool HasBoundSpecialItem(
        ExpeditionRunSave run,string itemId,string monsterInstanceId)
    {
        if(run==null)return false;
        EnsureCollections(run);
        ExpeditionMonsterStateSave monster=run.monsters==null?null:
            run.monsters.FirstOrDefault(value=>value!=null&&
                value.instanceId==monsterInstanceId);
        if(monster!=null)
        {
            EnsureMonsterSpecialMarks(monster);
            if(monster.appliedSpecialItemIds.Contains(itemId))return true;
        }
        return run.specialItems!=null&&run.specialItems.Any(value=>
            value!=null&&value.itemId==itemId&&value.count>0&&
            value.boundMonsterInstanceId==monsterInstanceId);
    }

    public static void ApplyOnAcquire(
        ExpeditionRunSave run,ExpeditionAugmentSelectionSave selected,Random random)
    {
        if(run==null||selected==null)return;
        if(random==null)random=new Random(selected.deterministicSeed);
        EnsureCollections(run);
        selected.runtimeWave=run.wave;
        selected.remainingWaves=selected.augmentId==EarlyAwakening?3:0;

        switch(selected.augmentId)
        {
            case EarlyAwakening:
                ApplyEarlyAwakening(run,selected);
                break;
            case TwinCommand:
                // These augments roll current-Wave runtime data in the scene
                // preparation pass. Mark them uninitialized so acquisition on the
                // current Wave is not mistaken for an already prepared state.
                selected.runtimeWave=0;
                break;
            case CombinedCast:
                GrantSpecialItem(run,"special_combined_cast_core",1,false,true);
                break;
            case DoubleSupply:
                break;
            case PandoraWorkshop:
                selected.runtimeWave=0;
                selected.runtimeState="";
                break;
            case ReserveReinforcement:
                selected.runtimeState="";
                selected.targetItemInstanceId="";
                break;
            case ReturnMark:
                ExpeditionItemInstanceSave returnMark=
                    ExpeditionItemSystem.CreateItem(run,"special_return_mark",true);
                returnMark.transformLocked=true;
                break;
            case TranscendenceGene:
                TryActivateTranscendence(run,selected.targetMonsterInstanceId);
                break;
            case CorruptionCatalyst:
                GrantSpecialItem(run,"special_corruption_catalyst");
                break;
            case AutoDeployment:
                int baseLimit=GameMasterData.ExpeditionInt("base_deployment_limit",3);
                for(int limit=baseLimit;limit<run.maxDeploymentCount;limit++)
                    selected.counter+=ExpeditionDeploymentSystem.NextUpgradeCost(limit);
                run.expeditionCurrency+=selected.counter;
                run.maxDeploymentCount=Math.Min(
                    ExpeditionDeploymentSystem.MaximumLimit,run.maxDeploymentCount+1);
                break;
            case FieldLoan:
                AugmentMasterRow windfall=GameMasterData.Augment(FieldLoan);
                run.expeditionCurrency+=Math.Max(0,(int)Math.Round(windfall==null?30f:
                    windfall.NumericParam("immediate_currency",30f)));
                break;
            case LifeMortgage:
                AugmentMasterRow mortgage=GameMasterData.Augment(LifeMortgage);
                int lifeCost=Math.Max(1,(int)Math.Round(mortgage==null?1f:
                    mortgage.NumericParam("life_cost",1f)));
                int currencyReward=Math.Max(0,(int)Math.Round(mortgage==null?15f:
                    mortgage.NumericParam("currency_reward",15f)));
                run.lives=Math.Max(1,run.lives-lifeCost);
                run.expeditionCurrency+=currencyReward;
                TryGrantLastBlessingReward(run,out _,out _);
                break;
            case LifeExpansion:
                AugmentMasterRow expansion=GameMasterData.Augment(LifeExpansion);
                int maximumLives=Math.Max(1,(int)Math.Round(expansion==null?5f:
                    expansion.NumericParam("max_lives",5f)));
                int lifeGain=Math.Max(0,(int)Math.Round(expansion==null?2f:
                    expansion.NumericParam("life_gain",2f)));
                run.maximumLives=Math.Max(run.maximumLives,maximumLives);
                run.lives=Math.Min(run.maximumLives,run.lives+lifeGain);
                break;
        }
    }

    public static void ApplyTranscendenceModifiers(ExpeditionAugmentCombatModifiers result)
    {
        result.maxHealthBonus+=TranscendenceParam("health_bonus",0.35f);
        result.attackDamageBonus+=TranscendenceParam("attack_bonus",0.30f);
        result.attackSpeedBonus+=TranscendenceParam("attack_speed_bonus",0.15f);
        float defenseAndResistance=TranscendenceParam("def_res_bonus",0.20f);
        result.defenseMultiplier*=1f+defenseAndResistance;
        result.statusResistanceMultiplier*=1f+defenseAndResistance;
        result.harmfulDurationMultiplier*=1f-Math.Max(0f,Math.Min(1f,
            TranscendenceParam("status_duration_reduction",0.20f)));
        result.skillPowerMultiplier*=1f+
            TranscendenceParam("skill_magnitude_bonus",0.25f);
        result.skillAreaMultiplier*=1f+
            TranscendenceParam("skill_area_bonus",0.20f);
        result.skillCooldownMultiplier*=1f-Math.Max(0f,Math.Min(1f,
            TranscendenceParam("skill_cooldown_reduction",0.15f)));
        result.modelScaleMultiplier*=TranscendenceParam("model_scale",1.10f);
        result.transcended=true;
    }

    public static ExpeditionAugmentCombatModifiers CombatModifiers(
        ExpeditionRunSave run,string monsterInstanceId,int wave)
    {
        ExpeditionAugmentCombatModifiers result=new ExpeditionAugmentCombatModifiers();
        if(run==null||string.IsNullOrEmpty(monsterInstanceId))return result;
        ExpeditionAugmentSelectionSave giant=Selection(run,GiantSigil);
        if(Targets(giant,monsterInstanceId))
        {
            AugmentMasterRow master=GameMasterData.Augment(GiantSigil);
            result.deploymentSlots=Math.Max(1,(int)Math.Round(master==null?2f:
                master.NumericParam("deployment_cost",2f)));
            result.maxHealthBonus+=master==null?0.80f:master.NumericParam("max_health_bonus",0.80f);
            result.attackDamageBonus+=master==null?0.35f:master.NumericParam("attack_bonus",0.35f);
            result.skillAreaMultiplier*=1f+(master==null?0.30f:master.NumericParam("skill_area_bonus",0.30f));
            result.skillPowerMultiplier*=1f+(master==null?0.25f:master.NumericParam("skill_damage_bonus",0.25f));
            result.modelScaleMultiplier*=master==null?1.35f:master.NumericParam("model_scale",1.35f);
        }

        if(ExpeditionAugmentSystem.Has(run,TimeCompression))
        {
            AugmentMasterRow master=GameMasterData.Augment(TimeCompression);
            result.skillCooldownMultiplier*=master==null?0.70f:
                master.NumericParam("skill_cooldown_multiplier",0.70f);
            result.basicAttackDamageMultiplier*=master==null?0.75f:
                master.NumericParam("basic_attack_damage_multiplier",0.75f);
            result.minimumSkillCooldown=Math.Max(result.minimumSkillCooldown,
                master==null?2f:master.NumericParam("min_skill_cooldown",2f));
        }

        ExpeditionAugmentSelectionSave hero=Selection(run,Protagonist);
        if(Targets(hero,monsterInstanceId))
        {
            result.maxHealthBonus+=ProtagonistParam("health_bonus",0.15f);
            result.attackDamageBonus+=ProtagonistParam("attack_bonus",0.15f);
            result.protagonist=true;
        }

        ExpeditionMonsterStateSave monster=run.monsters==null?null:
            run.monsters.FirstOrDefault(value=>value!=null&&value.instanceId==monsterInstanceId);
        if(monster!=null&&monster.transcended)
        {
            ApplyTranscendenceModifiers(result);
        }

        ExpeditionAugmentSelectionSave blessing=Selection(run,FickleBlessing);
        if(blessing!=null&&BlessingMode(blessing,wave)==0)
        {
            AugmentMasterRow master=GameMasterData.Augment(FickleBlessing);
            result.attackDamageBonus+=master==null?0.20f:
                master.NumericParam("red_damage",0.20f);
            float defenseAndResistance=master==null?10f:
                master.NumericParam("red_def_res",10f);
            result.defenseBonus+=defenseAndResistance;
            result.statusResistanceBonus+=defenseAndResistance;
        }
        result.returnMarked=HasEquippedItemEffect(
            run,monsterInstanceId,"ReturnMark");
        if(result.returnMarked)
        {
            AugmentMasterRow returnMark=GameMasterData.Augment(ReturnMark);
            result.returnDelay=returnMark==null?0.5f:
                returnMark.NumericParam("revive_delay",0.5f);
            result.returnHealthRatio=returnMark==null?0.5f:
                returnMark.NumericParam("revive_health_ratio",0.5f);
            result.returnInvulnerableSeconds=returnMark==null?1f:
                returnMark.NumericParam("invulnerable_seconds",1f);
            result.returnSkillChargeRatio=returnMark==null?0.5f:
                returnMark.NumericParam("skill_charge_ratio",0.5f);
        }
        result.combinedCaster=HasBoundSpecialItem(
            run,"special_combined_cast_core",monsterInstanceId);
        return result;
    }

    public static int BlessingMode(ExpeditionAugmentSelectionSave selection,int wave)
    {
        if(selection==null)return -1;
        int order=(selection.deterministicSeed&int.MaxValue)%6;
        int step=Math.Max(0,wave-selection.acquiredWave)%3;
        return FickleBlessingOrders[order,step];
    }

    public static int BlessingGrowthBonus(ExpeditionRunSave run,int wave)
    {
        ExpeditionAugmentSelectionSave selection=Selection(run,FickleBlessing);
        if(selection==null||BlessingMode(selection,wave)!=2)return 0;
        AugmentMasterRow master=GameMasterData.Augment(FickleBlessing);
        return Math.Max(0,(int)Math.Round(master==null?1f:
            master.NumericParam("blue_growth_bonus",1f)));
    }

    public static void ProcessWaveCleared(ExpeditionRunSave run,int clearedWave,int seed)
    {
        if(run==null||run.lastAugmentProcessedWave==clearedWave)return;
        EnsureCollections(run);

        ExpeditionAugmentSelectionSave deployment=Selection(run,AutoDeployment);
        if(deployment!=null)
        {
            AugmentMasterRow master=GameMasterData.Augment(AutoDeployment);
            int interval=Math.Max(1,(int)Math.Round(master==null?2f:
                master.NumericParam("interval_waves",2f)));
            int increase=Math.Max(1,(int)Math.Round(master==null?1f:
                master.NumericParam("increase_each",1f)));
            if(clearedWave>deployment.acquiredWave&&
               (clearedWave-deployment.acquiredWave)%interval==0)
                run.maxDeploymentCount=Math.Min(
                    ExpeditionDeploymentSystem.MaximumLimit,
                    run.maxDeploymentCount+increase);
        }

        run.lastAugmentProcessedWave=clearedWave;
    }

    public static void PreparePandoraWorkshopWave(ExpeditionRunSave run)
    {
        ExpeditionAugmentSelectionSave pandora=Selection(run,PandoraWorkshop);
        if(run==null||pandora==null)return;
        EnsureCollections(run);
        pandora.runtimeWave=run.wave;
        pandora.runtimeState=string.Join("|",ExpeditionItemSystem.InventoryItems(run)
            .Select(instance=>instance.instanceId));
    }

    public static ExpeditionItemInstanceSave ProcessPandoraWorkshopWaveEntered(
        ExpeditionRunSave run,int completedWave,int seed,List<string> transformedItemIds=null)
    {
        ExpeditionAugmentSelectionSave pandora=Selection(run,PandoraWorkshop);
        if(run==null||pandora==null||run.wave<=completedWave)return null;
        // Pandora's counter is the last Wave that received a component.
        if(pandora.counter>=run.wave)return null;
        EnsureCollections(run);
        Random random=new Random(seed);
        if(pandora.runtimeWave==completedWave)
        {
            IEnumerable<string> eligibleIds=string.IsNullOrEmpty(pandora.runtimeState)
                ?Enumerable.Empty<string>()
                :pandora.runtimeState.Split('|').Where(id=>!string.IsNullOrEmpty(id));
            RerollStoredItems(run,eligibleIds,random,transformedItemIds);
        }
        pandora.runtimeWave=0;
        pandora.runtimeState="";

        List<ItemMasterRow> bases=GameMasterData.ItemsInPool("default_equipment");
        if(bases.Count==0)return null;
        ExpeditionItemInstanceSave granted=ExpeditionItemSystem.CreateItem(
            run,bases[random.Next(bases.Count)].id,true);
        pandora.counter=run.wave;
        return granted;
    }

    public static bool TryActivateTranscendence(
        ExpeditionRunSave run,string monsterInstanceId)
    {
        ExpeditionAugmentSelectionSave gene=Selection(run,TranscendenceGene);
        ExpeditionMonsterStateSave monster=run==null||run.monsters==null?null:
            run.monsters.FirstOrDefault(value=>value!=null&&value.instanceId==monsterInstanceId);
        int requiredGrowth=(int)Math.Round(TranscendenceParam(
            "required_growth",ExpeditionGrowthSystem.MaximumGrowth));
        if(gene==null||monster==null||monster.temporary||monster.transcended||
           !Targets(gene,monsterInstanceId)||
           monster.rank<ExpeditionMonsterRank.Rank3||
           monster.growthTotal<requiredGrowth)return false;
        monster.transcended=true;
        monster.transcendenceFailureBonus=0;
        return true;
    }

    public static bool Targets(
        ExpeditionAugmentSelectionSave selection,string monsterInstanceId)
    {
        return selection!=null&&selection.targetMonsterInstanceId==monsterInstanceId;
    }

    private static void GrantRandomCompletedItem(
        ExpeditionRunSave run,Random random,bool temporary,bool destroyOnUnequip,
        string targetMonsterInstanceId)
    {
        List<ItemMasterRow> pool=GameMasterData.Database==null
            ?new List<ItemMasterRow>()
            :GameMasterData.Database.items.Where(value=>value!=null&&value.enabled&&
                value.IsCompleted&&value.itemPoolId=="default_equipment").ToList();
        if(pool.Count==0)return;
        ExpeditionItemInstanceSave item=ExpeditionItemSystem.CreateItem(
            run,pool[random.Next(pool.Count)].id,true);
        item.temporary=temporary;
        item.destroyOnUnequip=destroyOnUnequip;
        item.transformLocked=temporary;
        if(string.IsNullOrEmpty(targetMonsterInstanceId))return;
        ExpeditionMonsterStateSave monster=ExpeditionItemSystem.MonsterState(
            run,targetMonsterInstanceId);
        int empty=monster==null?-1:monster.itemInstanceIds.FindIndex(string.IsNullOrEmpty);
        if(empty>=0)ExpeditionItemSystem.Equip(
            run,targetMonsterInstanceId,item.instanceId,empty,out _);
    }

    private static void ApplyEarlyAwakening(
        ExpeditionRunSave run,ExpeditionAugmentSelectionSave selected)
    {
        ExpeditionMonsterStateSave monster=run.monsters.FirstOrDefault(value=>
            value!=null&&value.instanceId==selected.targetMonsterInstanceId);
        if(monster==null)return;
        MonsterGrowthMasterRow growth=GameMasterData.MonsterGrowth(monster.monsterId);
        int rank2=growth==null?3:growth.rank2RequiredTotalGrowth;
        int rank3=growth==null?9:growth.rank3RequiredTotalGrowth;
        ExpeditionMonsterRank rank=ExpeditionGrowthSystem.RankForGrowth(
            monster.monsterId,monster.growthTotal);
        int target=rank==ExpeditionMonsterRank.Rank1?rank2:
            rank==ExpeditionMonsterRank.Rank2?rank3:monster.growthTotal;
        monster.growthTotal=Math.Min(
            ExpeditionGrowthSystem.MaximumGrowth,
            Math.Max(monster.growthTotal,target));
        monster.rank=ExpeditionGrowthSystem.RankForGrowth(
            monster.monsterId,monster.growthTotal);
    }

    private static void RerollStoredItems(
        ExpeditionRunSave run,IEnumerable<string> eligibleIds,Random random,
        List<string> transformedItemIds)
    {
        List<ItemMasterRow> items=GameMasterData.Database?.items;
        if(items==null)return;
        HashSet<string> storedIds=new HashSet<string>(
            run.inventoryItemInstanceIds,StringComparer.Ordinal);
        foreach(string instanceId in eligibleIds.Distinct())
        {
            if(!storedIds.Contains(instanceId))continue;
            ExpeditionItemInstanceSave instance=ExpeditionItemSystem.ItemInstance(run,instanceId);
            ItemMasterRow current=instance==null?null:GameMasterData.Item(instance.itemId);
            if(current==null)continue;
            bool needsTransformData=instance.transformState!=ExpeditionItemTransformState.None&&
                instance.transformState!=ExpeditionItemTransformState.Radiant;
            List<ItemMasterRow> alternatives=items.Where(value=>value!=null&&value.enabled&&
                value.id!=current.id&&value.itemPoolId==current.itemPoolId&&
                string.Equals(value.itemKind,current.itemKind,StringComparison.OrdinalIgnoreCase)&&
                (!needsTransformData||GameMasterData.ItemTransform(value.id)!=null)).ToList();
            if(alternatives.Count==0)continue;
            instance.itemId=alternatives[random.Next(alternatives.Count)].id;
            if(transformedItemIds!=null)transformedItemIds.Add(instanceId);
        }
    }

    private static void EnsureCollections(ExpeditionRunSave run)
    {
        if(run.augments==null)run.augments=new List<ExpeditionAugmentSelectionSave>();
        if(run.specialItems==null)run.specialItems=new List<ExpeditionSpecialItemSave>();
        if(run.monsters==null)run.monsters=new List<ExpeditionMonsterStateSave>();
        if(run.itemInstances==null)run.itemInstances=new List<ExpeditionItemInstanceSave>();
        if(run.inventoryItemInstanceIds==null)run.inventoryItemInstanceIds=new List<string>();
        if(run.maximumLives<=0)run.maximumLives=Math.Max(3,run.lives);
        run.augments.RemoveAll(value=>value!=null&&
            value.augmentId=="augment_overcharge_workshop");
        run.specialItems.RemoveAll(value=>value!=null&&
            value.itemId=="special_overcharge_catalyst");
        NormalizeSpecialItemInstances(run);
        MigrateLegacyReturnMark(run);
        MigrateBoundMonsterSpecialItems(run);
    }

    private static void NormalizeSpecialItemInstances(ExpeditionRunSave run)
    {
        List<ExpeditionSpecialItemSave> stacked=run.specialItems.Where(value=>
            value!=null&&value.count>1).ToList();
        foreach(ExpeditionSpecialItemSave source in stacked)
        {
            int additional=source.count-1;
            source.count=1;
            for(int i=0;i<additional;i++)
                run.specialItems.Add(new ExpeditionSpecialItemSave
                {
                    instanceId="special_"+Guid.NewGuid().ToString("N"),
                    itemId=source.itemId,
                    count=1,
                    temporary=source.temporary,
                    occupiesItemSlot=source.occupiesItemSlot
                });
        }
    }

    private static void MigrateBoundMonsterSpecialItems(ExpeditionRunSave run)
    {
        List<ExpeditionSpecialItemSave> bound=run.specialItems.Where(value=>
            value!=null&&!string.IsNullOrEmpty(value.boundMonsterInstanceId)&&
            value.itemId=="special_combined_cast_core").ToList();
        foreach(ExpeditionSpecialItemSave item in bound)
        {
            ExpeditionMonsterStateSave monster=run.monsters.FirstOrDefault(value=>
                value!=null&&value.instanceId==item.boundMonsterInstanceId);
            if(monster==null)continue;
            EnsureMonsterSpecialMarks(monster);
            if(!monster.appliedSpecialItemIds.Contains(item.itemId))
                monster.appliedSpecialItemIds.Add(item.itemId);
            run.specialItems.Remove(item);
        }
    }

    private static bool HasEquippedItemEffect(
        ExpeditionRunSave run,string monsterInstanceId,string effectId)
    {
        ExpeditionMonsterStateSave monster=run.monsters==null?null:
            run.monsters.FirstOrDefault(value=>value!=null&&
                value.instanceId==monsterInstanceId);
        return monster!=null&&ExpeditionItemSystem.Aggregate(run,monster)
            .specialEffects.Any(value=>value.specialEffect==effectId);
    }

    private static void MigrateLegacyReturnMark(ExpeditionRunSave run)
    {
        ExpeditionMonsterStateSave holder=run.monsters.FirstOrDefault(value=>
            value!=null&&value.appliedSpecialItemIds!=null&&
            value.appliedSpecialItemIds.Contains("special_return_mark"));
        ExpeditionSpecialItemSave legacy=run.specialItems.FirstOrDefault(value=>
            value!=null&&value.itemId=="special_return_mark");
        if(holder==null&&legacy==null)return;
        if(holder==null&&!string.IsNullOrEmpty(legacy.boundMonsterInstanceId))
            holder=run.monsters.FirstOrDefault(value=>value!=null&&
                value.instanceId==legacy.boundMonsterInstanceId);

        ExpeditionItemInstanceSave item=run.itemInstances.FirstOrDefault(value=>
            value!=null&&value.itemId=="special_return_mark");
        if(item==null)
            item=ExpeditionItemSystem.CreateItem(run,"special_return_mark",true);
        item.transformLocked=true;
        if(holder!=null&&!run.monsters.Any(value=>value!=null&&
               value.itemInstanceIds!=null&&value.itemInstanceIds.Contains(item.instanceId)))
        {
            ExpeditionMonsterStateSave state=
                ExpeditionItemSystem.MonsterState(run,holder.instanceId);
            int slot=state.itemInstanceIds.FindIndex(string.IsNullOrEmpty);
            if(slot>=0)ExpeditionItemSystem.Equip(
                run,holder.instanceId,item.instanceId,slot,out _);
        }
        foreach(ExpeditionMonsterStateSave monster in run.monsters)
            monster?.appliedSpecialItemIds?.RemoveAll(id=>id=="special_return_mark");
        run.specialItems.RemoveAll(value=>
            value!=null&&value.itemId=="special_return_mark");
    }

    private static void EnsureMonsterSpecialMarks(ExpeditionMonsterStateSave monster)
    {
        if(monster.appliedSpecialItemIds==null)
            monster.appliedSpecialItemIds=new List<string>();
    }
}
