using System;
using UnityEngine;

namespace BombBrick.Combat
{
    public enum DamageType
    {
        Physical,
        Fire,
        Cold,
        Lightning
    }

    public enum DamageSourceId
    {
        Unknown,
        BasicAttack,
        Skill,
        CrackExplosion
    }

    [Flags]
    public enum DamageTags
    {
        None = 0,
        Attack = 1 << 0,
        Melee = 1 << 1,
        Area = 1 << 2,
        Explosion = 1 << 3,
        Basic = 1 << 4,
        Crack = 1 << 5,
        Knockback = 1 << 6
    }

    [Serializable]
    public struct CombatDefense
    {
        public float armor;
        [Range(-0.5f, 0.75f)] public float fireResistance;
        [Range(-0.5f, 0.75f)] public float coldResistance;
        [Range(-0.5f, 0.75f)] public float lightningResistance;
        public float increasedDamageTaken;
        public float moreDamageTakenMultiplier;

        public static CombatDefense None => new CombatDefense
        {
            moreDamageTakenMultiplier = 1f
        };
    }

    [Serializable]
    public struct DamagePacket
    {
        public DamageSourceId source;
        public DamageTags tags;
        public float physical;
        public float trueDamage;
        public float fire;
        public float cold;
        public float lightning;
        public float increasedDamage;
        public float increasedPhysicalDamage;
        public float increasedFireDamage;
        public float increasedColdDamage;
        public float increasedLightningDamage;
        public float moreDamageMultiplier;
        public float criticalMultiplier;
        [Range(0f, 1f)] public float armorPenetration;
        public float firePenetration;
        public float coldPenetration;
        public float lightningPenetration;
        public float ailmentPowerMultiplier;

        public static DamagePacket Physical(float amount, DamageSourceId source, DamageTags tags)
        {
            return new DamagePacket
            {
                source = source,
                tags = tags,
                physical = Mathf.Max(0f, amount),
                moreDamageMultiplier = 1f,
                criticalMultiplier = 1f,
                ailmentPowerMultiplier = 1f
            };
        }

        public DamagePacket Scaled(float multiplier)
        {
            multiplier = Mathf.Max(0f, multiplier);
            DamagePacket result = this;
            result.physical *= multiplier;
            result.trueDamage *= multiplier;
            result.fire *= multiplier;
            result.cold *= multiplier;
            result.lightning *= multiplier;
            return result;
        }

        public DamagePacket ConvertPhysicalTo(DamageType targetType, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (targetType == DamageType.Physical || ratio <= 0f || physical <= 0f)
                return this;

            DamagePacket result = this;
            float converted = result.physical * ratio;
            result.physical -= converted;
            switch (targetType)
            {
                case DamageType.Fire: result.fire += converted; break;
                case DamageType.Cold: result.cold += converted; break;
                case DamageType.Lightning: result.lightning += converted; break;
            }
            return result;
        }
    }

    public readonly struct DamageResult
    {
        public readonly float physical;
        public readonly float trueDamage;
        public readonly float fire;
        public readonly float cold;
        public readonly float lightning;
        public readonly float total;

        public DamageResult(float physical, float trueDamage, float fire, float cold, float lightning)
        {
            this.physical = physical;
            this.trueDamage = trueDamage;
            this.fire = fire;
            this.cold = cold;
            this.lightning = lightning;
            total = physical + trueDamage + fire + cold + lightning;
        }
    }

    public static class CombatDamageResolver
    {
        public const float MinimumResistance = -0.5f;
        public const float MaximumResistance = 0.75f;

        public static DamageResult Resolve(DamagePacket packet, CombatDefense defense)
        {
            float more = packet.moreDamageMultiplier > 0f ? packet.moreDamageMultiplier : 1f;
            float critical = packet.criticalMultiplier > 0f ? packet.criticalMultiplier : 1f;
            float takenMore = defense.moreDamageTakenMultiplier > 0f ? defense.moreDamageTakenMultiplier : 1f;
            float taken = Mathf.Max(0f, 1f + defense.increasedDamageTaken) * takenMore;

            float physical = ApplyOutgoing(packet.physical, packet.increasedDamage + packet.increasedPhysicalDamage, more, critical);
            float trueDamage = Mathf.Max(0f, packet.trueDamage);
            float fire = ApplyOutgoing(packet.fire, packet.increasedDamage + packet.increasedFireDamage, more, critical);
            float cold = ApplyOutgoing(packet.cold, packet.increasedDamage + packet.increasedColdDamage, more, critical);
            float lightning = ApplyOutgoing(packet.lightning, packet.increasedDamage + packet.increasedLightningDamage, more, critical);

            physical *= 1f - CalculateArmorReduction(defense.armor, packet.armorPenetration);
            fire *= 1f - CalculateResistance(defense.fireResistance, packet.firePenetration);
            cold *= 1f - CalculateResistance(defense.coldResistance, packet.coldPenetration);
            lightning *= 1f - CalculateResistance(defense.lightningResistance, packet.lightningPenetration);

            return new DamageResult(physical * taken, trueDamage, fire * taken, cold * taken, lightning * taken);
        }

        private static float ApplyOutgoing(float damage, float increased, float more, float critical)
        {
            return Mathf.Max(0f, damage) * Mathf.Max(0f, 1f + increased) * more * critical;
        }

        private static float CalculateArmorReduction(float armor, float penetration)
        {
            if (armor <= 0f)
                return 0f;

            float effectiveArmor = Mathf.Max(0f, armor) * (1f - Mathf.Clamp01(penetration));
            return effectiveArmor / (100f + effectiveArmor);
        }

        private static float CalculateResistance(float resistance, float penetration)
        {
            return Mathf.Clamp(resistance - Mathf.Max(0f, penetration), MinimumResistance, MaximumResistance);
        }
    }

    public static class CombatStatFormula
    {
        public const float CombatAttackRate=.8f;
        public const float CombatMovementRate=.8f;
        public const float CombatAnimationRate=.8f;
        public const float MaximumAttackSpeed=5f*CombatAttackRate;
        public const float BaseCriticalDamageMultiplier=1.5f;
        public static float BattleHealthMultiplier=>
            Mathf.Max(1,GameMasterData.ExpeditionInt("battle_health_percent",300))/100f;
        public const float BattleDefenseMultiplier=2f;
        public const float BattleAttackIntervalMultiplier=1.08f;
        public const float BattleOvertimeStartSeconds=38f;
        public const float BattleOvertimeStepSeconds=2f;
        public const float BattleOvertimeDamagePerStep=0.10f;
        public const float BasicAttackSkillChargeRatio=0.10f;
        public const float DamageTakenSkillChargePerHealth=0.30f;

        public static float BattleHealth(float value)
        {
            return Mathf.Max(0f,value)*BattleHealthMultiplier;
        }

        public static float BattleDefense(float value)
        {
            return Mathf.Max(0f,value)*BattleDefenseMultiplier;
        }

        public static float BattleAttackInterval(float value)
        {
            return Mathf.Max(0.01f,value)*BattleAttackIntervalMultiplier;
        }

        public static float BattleInitialSkillCooldown(float fullCooldown)
        {
            return Mathf.Max(0f,fullCooldown);
        }

        public static float CooldownAfterBasicAttack(
            float remaining,float fullCooldown)
        {
            return Mathf.Max(0f,remaining-
                Mathf.Max(0f,fullCooldown)*BasicAttackSkillChargeRatio);
        }

        public static float CooldownAfterDamageTaken(
            float remaining,float fullCooldown,float damage,float maximumHealth)
        {
            float healthRatio=Mathf.Max(0f,damage)/Mathf.Max(1f,maximumHealth);
            return Mathf.Max(0f,remaining-
                Mathf.Max(0f,fullCooldown)*healthRatio*
                DamageTakenSkillChargePerHealth);
        }

        public static float BattleOvertimeDamageMultiplier(float elapsedSeconds)
        {
            if(elapsedSeconds<BattleOvertimeStartSeconds)return 1f;
            int steps=Mathf.FloorToInt(
                (elapsedSeconds-BattleOvertimeStartSeconds)/
                BattleOvertimeStepSeconds)+1;
            return 1f+steps*BattleOvertimeDamagePerStep;
        }

        public static float AttackInterval(float baseInterval,float bonusAttackSpeed,float slow)
        {
            float attacksPerSecond=1f/Mathf.Max(0.01f,baseInterval);
            attacksPerSecond*=Mathf.Max(0.01f,1f+bonusAttackSpeed)*
                              Mathf.Max(0.01f,1f-Mathf.Clamp01(slow));
            return 1f/Mathf.Min(MaximumAttackSpeed,attacksPerSecond);
        }

        public static float CriticalChance(float chance)
        {
            return Mathf.Clamp01(chance);
        }

        public static float CriticalDamageMultiplier(float chance,float bonus)
        {
            return BaseCriticalDamageMultiplier+Mathf.Max(0f,bonus)+
                   Mathf.Max(0f,chance-1f)*0.5f;
        }

        public static float Cooldown(float baseDuration,float haste,float multiplier=1f)
        {
            return Mathf.Max(0f,baseDuration)*Mathf.Max(0f,multiplier)/
                   Mathf.Max(1f,1f+Mathf.Max(0f,haste));
        }

        public static float Strongest(float current,float candidate)
        {
            return Mathf.Max(Mathf.Max(0f,current),Mathf.Max(0f,candidate));
        }
    }
}

[Serializable]
public readonly struct SkillDamageFormulaResult
{
    public readonly float physicalDamage;
    public readonly float trueDamage;
    public readonly float defenseIgnoreRatio;

    public SkillDamageFormulaResult(
        float physicalDamage,float trueDamage,float defenseIgnoreRatio)
    {
        this.physicalDamage=Mathf.Max(0f,physicalDamage);
        this.trueDamage=Mathf.Max(0f,trueDamage);
        this.defenseIgnoreRatio=Mathf.Clamp01(defenseIgnoreRatio);
    }

    public float Total=>physicalDamage+trueDamage;
}

public static class SkillDamageFormulaResolver
{
    public static SkillDamageFormulaResult Resolve(
        SkillMasterRow skill,float sourceAttack,float sourceMaxHp,
        float targetCurrentHp,float targetMaxHp,bool secondary=false,int consumedStacks=0)
    {
        if(skill==null)return new SkillDamageFormulaResult(0f,0f,0f);

        float attack=Mathf.Max(0f,sourceAttack);
        float attackMultiplier=secondary&&skill.secondaryDamageMultiplier>0f
            ?skill.secondaryDamageMultiplier:skill.damageMultiplier;
        attackMultiplier=Mathf.Max(0f,attackMultiplier)+
                         Mathf.Max(0,consumedStacks)*
                         Mathf.Max(0f,skill.damagePerStackMultiplier);
        float attackDamage=attack*attackMultiplier;

        float hpDamage=Mathf.Max(0f,sourceMaxHp)*Mathf.Max(0f,skill.casterMaxHpRatio)+
                       Mathf.Max(0f,targetCurrentHp)*Mathf.Max(0f,skill.targetCurrentHpRatio)+
                       Mathf.Max(0f,targetMaxHp-targetCurrentHp)*
                       Mathf.Max(0f,skill.targetMissingHpRatio);
        if(skill.hpScalingCapAttackMultiplier>0f)
            hpDamage=Mathf.Min(hpDamage,attack*skill.hpScalingCapAttackMultiplier);

        float physical=attackDamage+(skill.hpScalingTrueDamage?0f:hpDamage);
        float trueDamage=skill.hpScalingTrueDamage?hpDamage:0f;
        return new SkillDamageFormulaResult(
            physical,trueDamage,skill.defenseIgnoreRatio);
    }
}

public static class CombatStatusResistance
{
    public static float DurationMultiplier(float resistance)
    {
        float value=Mathf.Max(0f,resistance);
        return 100f/(100f+value);
    }

    public static float Apply(float baseDuration,float resistance)
    {
        return Mathf.Max(0f,baseDuration)*DurationMultiplier(resistance);
    }
}
