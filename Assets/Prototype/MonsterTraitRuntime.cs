using System;
using System.Collections.Generic;

public sealed class MonsterTraitCombatState
{
    public string lastBasicTargetId="";
    public int sameTargetHits;
    public string markedTargetId="";
    public int markStacks;
    public bool skillEchoUsed;
    public int storedInstinctStacks;
    public int afterglowAttacks;
    public int backflowAttacks;
    public int totalBasicHits;
    public string brandedTargetId="";
    public bool finisherReady;
    public float assistSignalCooldown;
    public float sharedMarkCooldown;
    public float protectiveCounterCooldown;
    public string sharedMarkedTargetId="";
    public int commandedBasicAttacks;
    public int elementResonanceAttacks;
    public readonly HashSet<string> giantHuntTargets=new HashSet<string>(StringComparer.Ordinal);
    public bool crossHuntAlternate;
    public string crossHuntFirstTargetId="";
    public string crossHuntSecondTargetId="";
    public string openWoundTargetId="";
    public int openWoundHits;
    public float observedAlliedShield;
    public object observedShieldSource;
    public bool bearDefenseStance;
    public int bearResolveUses;

    public void Reset()
    {
        lastBasicTargetId="";
        sameTargetHits=0;
        markedTargetId="";
        markStacks=0;
        skillEchoUsed=false;
        storedInstinctStacks=0;
        afterglowAttacks=0;
        backflowAttacks=0;
        totalBasicHits=0;
        brandedTargetId="";
        finisherReady=false;
        assistSignalCooldown=0f;
        sharedMarkCooldown=0f;
        protectiveCounterCooldown=0f;
        sharedMarkedTargetId="";
        commandedBasicAttacks=0;
        elementResonanceAttacks=0;
        giantHuntTargets.Clear();
        crossHuntAlternate=false;
        crossHuntFirstTargetId="";
        crossHuntSecondTargetId="";
        openWoundTargetId="";
        openWoundHits=0;
        observedAlliedShield=0f;
        observedShieldSource=null;
        bearDefenseStance=false;
        bearResolveUses=0;
    }

    public void Tick(float deltaTime)
    {
        assistSignalCooldown=Math.Max(0f,assistSignalCooldown-deltaTime);
        sharedMarkCooldown=Math.Max(0f,sharedMarkCooldown-deltaTime);
        protectiveCounterCooldown=Math.Max(0f,protectiveCounterCooldown-deltaTime);
    }
}

public struct SkillHitTraitResult
{
    public float splitDamage;
    public float delayedAreaDamage;
    public float killChainDamage;
    public float afterimageDamage;
    public float spreadingDamage;
    public int activations;
    public int secondaryTargets;
}

public struct BasicAttackTraitResult
{
    public float bonusPrimaryDamage;
    public float bonusFlatDamage;
    public float splashDamage;
    public bool piercingSplash;
    public bool chainConduction;
    public bool overkillTransfer;
    public bool barrierBreaker;
    public bool gravityKnot;
    public bool switchTarget;
    public float targetSkillDelay;
    public float skillCooldownReduction;
    public int activations;
}

public struct SkillUseTraitResult
{
    public float damageMultiplier;
    public float radiusMultiplier;
    public int activations;
}

public static class MonsterTraitRuntime
{
    public static string ComboShortName(string traitId)
    {
        switch(traitId)
        {
            case "trait_combo_antigravity":return "반중력";
            case "trait_combo_fall_shatter":return "낙하분쇄";
            case "trait_combo_frost_shackle":return "서리족쇄";
            case "trait_combo_venom_mutation":return "독혈변이";
            case "trait_combo_chemical_ignition":return "화학점화";
            case "trait_combo_open_wound":return "상처개방";
            case "trait_combo_morale_veil":return "사기장막";
            case "trait_combo_resolve_barrier":return "결의방벽";
            default:return "연계";
        }
    }

    public static List<string> ActiveTraitIds(MonsterDefinition definition,OwnedMonsterSave owned,
        ExpeditionMonsterStateSave expeditionState)
    {
        List<string> result=new List<string>(3);
        string intrinsic=owned!=null&&!string.IsNullOrWhiteSpace(owned.intrinsicTraitId)
            ?owned.intrinsicTraitId:definition==null?"":definition.intrinsicTraitId;
        AddUnique(result,intrinsic);
        foreach(string traitId in ExpeditionTraitProgression.ActiveTraitIds(expeditionState))
            AddUnique(result,traitId);
        return result;
    }

    public static BasicAttackTraitResult ResolveBasicAttack(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState state,string targetId,bool ranged,bool recentlyHitByAlly,
        float sourceDamage=0f,float sourceMaxHp=0f,float sourceDefense=0f,
        float sourceStatusResistance=0f,float targetCurrentHp=0f)
    {
        BasicAttackTraitResult result=new BasicAttackTraitResult();
        if(activeTraits==null||state==null||string.IsNullOrWhiteSpace(targetId))return result;

        bool newTarget=!string.Equals(state.lastBasicTargetId,targetId,StringComparison.Ordinal);
        state.sameTargetHits=newTarget?1:state.sameTargetHits+1;
        state.lastBasicTargetId=targetId;
        state.totalBasicHits++;

        if(Has(activeTraits,"trait_vital_strike")&&state.totalBasicHits%4==0)
        {
            result.bonusFlatDamage+=sourceMaxHp*0.03f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_ironclad_strike")&&state.totalBasicHits%3==0)
        {
            result.bonusFlatDamage+=sourceDefense*0.75f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_resolute_strike"))
        {
            result.bonusFlatDamage+=sourceDamage*Math.Max(0f,sourceStatusResistance)*0.6f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_giant_hunt")&&state.giantHuntTargets.Add(targetId))
        {
            result.bonusFlatDamage+=Math.Max(0f,targetCurrentHp)*0.03f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_chain_conduction")&&state.totalBasicHits%4==0)
        {
            result.chainConduction=true;
            result.activations++;
        }
        if(Has(activeTraits,"trait_cross_hunt"))
        {
            if(string.IsNullOrEmpty(state.crossHuntFirstTargetId))state.crossHuntFirstTargetId=targetId;
            else if(!string.Equals(state.crossHuntFirstTargetId,targetId,StringComparison.Ordinal))
                state.crossHuntSecondTargetId=targetId;
            state.crossHuntAlternate=!state.crossHuntAlternate;
            result.switchTarget=state.crossHuntAlternate;
            result.activations++;
        }
        if(Has(activeTraits,"trait_overkill_transfer"))result.overkillTransfer=true;
        if(Has(activeTraits,"trait_barrier_breaker"))result.barrierBreaker=true;
        if(Has(activeTraits,"trait_tech_disruption")&&state.totalBasicHits%5==0)
        {
            result.targetSkillDelay=0.8f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_gravity_knot")&&state.totalBasicHits%4==0)
        {
            result.gravityKnot=true;
            result.activations++;
        }
        if(Has(activeTraits,"trait_stored_instinct"))
            state.storedInstinctStacks=Math.Min(5,state.storedInstinctStacks+1);
        if(Has(activeTraits,"trait_hunt_afterglow")&&state.afterglowAttacks>0)
        {
            state.afterglowAttacks--;
            result.splashDamage+=0.25f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_battle_acceleration")&&state.totalBasicHits%3==0)
        {
            result.skillCooldownReduction=0.75f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_skill_brand")&&
           string.Equals(state.brandedTargetId,targetId,StringComparison.Ordinal))
        {
            result.bonusPrimaryDamage+=0.35f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_backflow")&&state.backflowAttacks>0)
        {
            state.backflowAttacks--;
            result.bonusPrimaryDamage+=0.2f;
            result.splashDamage+=0.15f;
            result.activations++;
        }
        if(state.commandedBasicAttacks>0)
        {
            state.commandedBasicAttacks--;
            result.splashDamage+=0.3f;
        }
        if(state.elementResonanceAttacks>0)
        {
            state.elementResonanceAttacks--;
            result.bonusPrimaryDamage+=0.25f;
            result.splashDamage+=0.15f;
        }
        if(Has(activeTraits,"trait_rank2_pursuit")&&state.sameTargetHits>=3)
        {
            result.bonusPrimaryDamage+=0.3f;
            result.activations++;
        }
        return result;
    }

    public static SkillUseTraitResult PreviewSkillUse(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState state)
    {
        SkillUseTraitResult result=new SkillUseTraitResult{damageMultiplier=1f,radiusMultiplier=1f};
        if(Has(activeTraits,"trait_skill_condense"))
        {
            result.damageMultiplier*=1.8f;
            result.radiusMultiplier*=0.6f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_skill_overcharge"))
        {
            result.damageMultiplier*=1.7f;
            result.radiusMultiplier*=1.3f;
            result.activations++;
        }
        if(Has(activeTraits,"trait_rank2_technique"))
            result.damageMultiplier*=1.3f;
        if(Has(activeTraits,"trait_skill_instant_manifest"))
            result.damageMultiplier*=0.8f;
        if(state==null||!Has(activeTraits,"trait_stored_instinct")||state.storedInstinctStacks<=0)
            return result;
        result.damageMultiplier+=state.storedInstinctStacks*0.08f;
        result.radiusMultiplier+=state.storedInstinctStacks*0.04f;
        return result;
    }

    public static int CommitSkillUse(IReadOnlyCollection<string> activeTraits,MonsterTraitCombatState state)
    {
        if(state==null)return 0;
        int activations=0;
        if(Has(activeTraits,"trait_stored_instinct")&&state.storedInstinctStacks>0)
        {
            state.storedInstinctStacks=0;
            activations++;
        }
        if(Has(activeTraits,"trait_hunt_afterglow"))
        {
            state.afterglowAttacks=2;
            activations++;
        }
        if(Has(activeTraits,"trait_backflow"))
        {
            state.backflowAttacks=2;
            activations++;
        }
        if(state.finisherReady)
        {
            state.finisherReady=false;
            activations++;
        }
        return activations;
    }

    public static bool RecordSkillHit(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState state,string targetId)
    {
        if(state==null||!Has(activeTraits,"trait_skill_brand"))return false;
        state.brandedTargetId=targetId??string.Empty;
        return true;
    }

    public static void RecordShareableMark(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState state,string targetId,bool statusOrMarkSkill)
    {
        if(state!=null&&statusOrMarkSkill&&Has(activeTraits,"trait_shared_mark"))
            state.sharedMarkedTargetId=targetId??string.Empty;
    }

    public static void RecordBasicAttackKill(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState state)
    {
        if(state!=null&&Has(activeTraits,"trait_finisher_link"))state.finisherReady=true;
    }

    public static bool TryTriggerAssistSignal(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState state)
    {
        if(state==null||state.assistSignalCooldown>0f||!Has(activeTraits,"trait_assist_signal"))return false;
        state.assistSignalCooldown=1.5f;
        return true;
    }

    public static bool TryConsumeSharedMark(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState state,string targetId)
    {
        if(state==null||state.sharedMarkCooldown>0f||!Has(activeTraits,"trait_shared_mark")||
           !string.Equals(state.sharedMarkedTargetId,targetId,StringComparison.Ordinal))return false;
        state.sharedMarkedTargetId="";
        state.sharedMarkCooldown=0.5f;
        return true;
    }

    public static bool TryTriggerProtectiveCounter(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState state)
    {
        if(state==null||state.protectiveCounterCooldown>0f||
           !Has(activeTraits,"trait_protective_counter"))return false;
        state.protectiveCounterCooldown=2f;
        return true;
    }

    public static bool GrantCommandedAttack(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState receiver)
    {
        if(receiver==null||!Has(activeTraits,"trait_chain_command"))return false;
        receiver.commandedBasicAttacks++;
        return true;
    }

    public static bool GrantElementResonance(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState receiver)
    {
        if(receiver==null||!Has(activeTraits,"trait_element_resonance"))return false;
        receiver.elementResonanceAttacks++;
        return true;
    }

    public static bool TryScheduleSkillEcho(IReadOnlyCollection<string> activeTraits,
        MonsterTraitCombatState state)
    {
        if(state==null||state.skillEchoUsed||!Has(activeTraits,"trait_skill_dual_manifest"))return false;
        state.skillEchoUsed=true;
        return true;
    }

    public static SkillHitTraitResult ResolveSkillHit(IReadOnlyCollection<string> activeTraits,
        bool singleTargetSkill,bool movementSkill,bool statusOrMarkSkill,bool killedTarget)
    {
        SkillHitTraitResult result=new SkillHitTraitResult();
        if(activeTraits==null)return result;
        if(singleTargetSkill&&Has(activeTraits,"trait_skill_distributed_cast"))
        {
            result.splitDamage=0.6f;
            result.secondaryTargets=2;
            result.activations++;
        }
        if(Has(activeTraits,"trait_aftershock"))
        {
            result.delayedAreaDamage=0.3f;
            result.activations++;
        }
        if(killedTarget&&Has(activeTraits,"trait_devour_chain"))
        {
            result.killChainDamage=0.5f;
            result.activations++;
        }
        if(movementSkill&&Has(activeTraits,"trait_afterimage"))
        {
            result.afterimageDamage=0.3f;
            result.activations++;
        }
        if(statusOrMarkSkill&&Has(activeTraits,"trait_spreading_wound"))
        {
            result.spreadingDamage=0.25f;
            result.activations++;
        }
        return result;
    }

    public static bool Has(IReadOnlyCollection<string> traits,string traitId)
    {
        if(traits==null)return false;
        if(traits is IList<string> indexed)
        {
            for(int i=0;i<indexed.Count;i++)
                if(string.Equals(indexed[i],traitId,StringComparison.Ordinal))return true;
            return false;
        }
        foreach(string active in traits)
            if(string.Equals(active,traitId,StringComparison.Ordinal))return true;
        return false;
    }

    private static void AddUnique(List<string> result,string traitId)
    {
        if(string.IsNullOrWhiteSpace(traitId)||result.Contains(traitId))return;
        result.Add(traitId);
    }
}
