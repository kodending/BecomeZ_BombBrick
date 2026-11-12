using System;

public enum PersonalityTargetStyle
{
    Nearest,
    Persistent,
    LowestHealth,
    Strongest,
    Vengeful,
    Rotating,
    Cooperative,
    Independent,
    Protective,
    Opportunistic,
    Impulsive,
    Cautious,
    Cluster,
    Eccentric
}

public sealed class PersonalityCombatPolicy
{
    public string personalityId;
    public PersonalityTargetStyle targetStyle;
    public float distanceWeight;
    public float weakTargetWeight;
    public float strongTargetWeight;
    public float retaliationBonus;
    public float focusFireBonus;
    public float unfocusedTargetBonus;
    public float protectAllyBonus;
    public float clusterOpportunityBonus;
    public float disabledTargetBonus;
    public float targetPersistenceBonus;
    public float targetSwitchThreshold;
    public float preferredRangeRatio;
    public int rangedAttacksBeforeRetreat;
    public float rearApproachBias;
    public bool rotateAfterBasicAttack;
    public bool timidRetreat;
    public float reconsiderInterval=0.35f;
    public float targetLockDuration=1.8f;
    public int attacksPerRotation;

    public static PersonalityCombatPolicy From(OwnedMonsterSave monster)
    {
        return FromId(monster==null?null:monster.personalityId);
    }

    public static PersonalityCombatPolicy FromId(string personalityId)
    {
        PersonalityCombatPolicy policy=Balanced();
        policy.personalityId=string.IsNullOrWhiteSpace(personalityId)
            ?"persistent":personalityId;

        switch(policy.personalityId)
        {
            case "persistent":
                policy.targetStyle=PersonalityTargetStyle.Persistent;
                policy.targetPersistenceBonus=100f;
                policy.targetSwitchThreshold=100f;
                break;
            case "calm":
                policy.targetStyle=PersonalityTargetStyle.LowestHealth;
                policy.weakTargetWeight=15f;
                policy.targetSwitchThreshold=0.2f;
                policy.preferredRangeRatio=0.68f;
                break;
            case "challenger":
                policy.targetStyle=PersonalityTargetStyle.Strongest;
                policy.strongTargetWeight=8f;
                policy.rearApproachBias=-0.15f;
                break;
            case "vengeful":
                policy.targetStyle=PersonalityTargetStyle.Vengeful;
                policy.retaliationBonus=40f;
                policy.targetSwitchThreshold=0f;
                break;
            case "fickle":
                policy.targetStyle=PersonalityTargetStyle.Rotating;
                policy.rotateAfterBasicAttack=true;
                policy.attacksPerRotation=3;
                policy.targetSwitchThreshold=0f;
                break;
            case "cooperative":
                policy.targetStyle=PersonalityTargetStyle.Cooperative;
                policy.focusFireBonus=9f;
                policy.protectAllyBonus=0.8f;
                policy.targetSwitchThreshold=0.1f;
                break;
            case "independent":
                policy.targetStyle=PersonalityTargetStyle.Independent;
                policy.unfocusedTargetBonus=9f;
                policy.focusFireBonus=-2.5f;
                policy.targetSwitchThreshold=0.1f;
                break;
            case "protective":
                policy.targetStyle=PersonalityTargetStyle.Protective;
                policy.protectAllyBonus=18f;
                policy.retaliationBonus=2f;
                break;
            case "opportunist":
                policy.targetStyle=PersonalityTargetStyle.Opportunistic;
                policy.disabledTargetBonus=14f;
                policy.weakTargetWeight=6f;
                policy.targetSwitchThreshold=0.1f;
                break;
            case "impulsive":
                policy.targetStyle=PersonalityTargetStyle.Impulsive;
                policy.distanceWeight=3.5f;
                policy.targetSwitchThreshold=0f;
                policy.reconsiderInterval=0.6f;
                policy.targetLockDuration=1.25f;
                break;
            case "cautious":
                policy.targetStyle=PersonalityTargetStyle.Cautious;
                policy.distanceWeight=1.7f;
                policy.retaliationBonus=6f;
                policy.preferredRangeRatio=0.78f;
                policy.rangedAttacksBeforeRetreat=1;
                policy.rearApproachBias=0.9f;
                break;
            case "bold":
                policy.targetStyle=PersonalityTargetStyle.Nearest;
                policy.preferredRangeRatio=0.42f;
                policy.rangedAttacksBeforeRetreat=3;
                break;
            case "timid":
                policy.targetStyle=PersonalityTargetStyle.Cautious;
                policy.timidRetreat=true;
                policy.distanceWeight=1.5f;
                policy.retaliationBonus=8f;
                policy.preferredRangeRatio=0.82f;
                policy.rangedAttacksBeforeRetreat=1;
                break;
            case "eccentric":
                policy.targetStyle=PersonalityTargetStyle.Eccentric;
                policy.targetSwitchThreshold=0f;
                policy.reconsiderInterval=2.4f;
                policy.targetLockDuration=2.4f;
                break;
        }
        return policy;
    }

    public static PersonalityCombatPolicy Balanced()
    {
        return new PersonalityCombatPolicy
        {
            personalityId="balanced",
            targetStyle=PersonalityTargetStyle.Nearest,
            distanceWeight=1f,
            weakTargetWeight=1.5f,
            retaliationBonus=0.8f,
            focusFireBonus=0.25f,
            protectAllyBonus=0.25f,
            clusterOpportunityBonus=0.2f,
            targetPersistenceBonus=1.5f,
            targetSwitchThreshold=0.7f,
            preferredRangeRatio=0.58f,
            rangedAttacksBeforeRetreat=2,
            rearApproachBias=0.25f,
            reconsiderInterval=0.35f
        };
    }

    public static bool IsSupported(string personalityId)
    {
        switch(personalityId)
        {
            case "persistent": case "calm": case "challenger": case "vengeful":
            case "fickle": case "cooperative": case "independent": case "protective":
            case "opportunist": case "impulsive": case "cautious": case "bold":
            case "timid": case "eccentric":
                return true;
            default:
                return false;
        }
    }

    public static bool IsFeatured(string personalityId)
    {
        switch(personalityId)
        {
            case "vengeful": case "fickle": case "cooperative":
            case "protective": case "bold": case "timid":
                return true;
            default:
                return false;
        }
    }

    public static bool IsWithinLocalTargetWindow(
        float nearestDistance,float candidateDistance,float extraDistance)
    {
        return candidateDistance<=nearestDistance+Math.Max(0f,extraDistance)+0.0001f;
    }

    public static bool ShouldVengefulRetarget(
        float hitDamage,float accumulatedDamage,float maximumHealth,bool cooldownReady)
    {
        float health=Math.Max(1f,maximumHealth);
        return cooldownReady&&
               (hitDamage/health>=0.0499f||accumulatedDamage/health>=0.0999f);
    }

    public static bool ShouldProtectiveIntercept(
        float allyHealth,float allyMaximumHealth,bool cooldownReady)
    {
        return cooldownReady&&allyHealth>0f&&
               allyHealth<=Math.Max(1f,allyMaximumHealth)*0.60f;
    }

    public static bool ShouldTimidRetreat(
        bool mutualDuel,bool hasNearbySupport,bool hasLivingAlly,
        bool hasPathToAlly,bool cooldownReady,int attackers=1,bool lowHealth=false)
    {
        return hasLivingAlly&&hasPathToAlly&&cooldownReady&&
               (mutualDuel&&!hasNearbySupport||attackers>=2||lowHealth&&attackers>0);
    }

    public static int EccentricCriterion(int personalitySeed)
    {
        return (int)((uint)personalitySeed%4u);
    }

    public bool ShouldRotateAfterBasicAttack(int basicAttackCount)
    {
        return rotateAfterBasicAttack&&attacksPerRotation>0&&basicAttackCount>0&&
               basicAttackCount%attacksPerRotation==0;
    }
}
