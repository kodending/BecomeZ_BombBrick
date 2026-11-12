using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private float FocusedSkillRadiusMultiplier(Unit source,bool repeated)
    {
        if(source==null)return 0f;
        if(repeated)return source.skillTraitRadiusMultiplier;
        float multiplier=MonsterTraitRuntime.PreviewSkillUse(source.activeTraitIds,source.traitState).radiusMultiplier*
            source.augmentSkillAreaMultiplier;
        ItemTransformMasterRow grand=CorruptionEffect(source,"CorruptGrandSpellTimer");
        if(grand!=null&&CorruptValue(source,"grand_ready")>0f)
            multiplier*=1f+grand.GreatParam("skill_area_bonus",.3f);
        return multiplier;
    }

    private bool CanStartFocusedAreaSkill(Unit source,Unit target,SkillMasterRow skill,bool repeated)
    {
        if(!IsCombatActive(source)||skill==null)return false;
        string behavior=skill.behavior;
        float radiusTiles=skill.radiusTiles;
        switch(behavior)
        {
            case "GuardianTaunt":break;
            case "ClusterBreath":if(!IsCombatActive(target))return false;break;
            case "EarthPulse":
                if(HasRank3(source,KingKongCombatRules.WorldOverturnTraitId))
                    radiusTiles=KingKongCombatRules.WorldOverturnRadiusTiles;
                break;
            case "TyrantTremor":
                if(HasFamilyTranscendence(source))
                    return units.Exists(u=>u.team!=source.team&&IsCombatTargetable(u)&&u.hp>0f);
                if(HasRank3(source,TyrannoRank3Rules.MassExtinction))radiusTiles=TyrannoRank3Rules.ExtinctionRadiusTiles;
                else if(HasRank3(source,TyrannoRank3Rules.PrimordialMaw))radiusTiles=TyrannoRank3Rules.MawRadiusTiles;
                break;
            case "JusticeRoar":
                if(source.haetaeRoar!=null)return false;
                radiusTiles=HaetaeRoarRadius(source,skill);
                if(HaetaeSupportTargets(source,skill)>0)return true;
                break;
            default:return false;
        }
        Vector3 origin=Flat(source.model.transform.position);
        float radius=TileSize*radiusTiles*FocusedSkillRadiusMultiplier(source,repeated);
        // A queued repeat supplies its captured target without changing the caster's current AI target.
        Vector3 forward=IsCombatActive(target)?Flat(target.model.transform.position)-origin:
            Flat(source.model.transform.forward);
        bool forwardDeploy=behavior=="EarthPulse"&&MonsterTraitRuntime.Has(source.activeTraitIds,"trait_skill_forward_deploy");
        if(forwardDeploy&&forward.sqrMagnitude<.001f)forward=Vector3.forward;
        forward.Normalize();
        for(int i=0;i<units.Count;i++)
        {
            Unit enemy=units[i];
            if(!IsCombatActive(enemy)||enemy.team==source.team)continue;
            Vector3 offset=Flat(enemy.model.transform.position)-origin;
            float distance=offset.magnitude;
            switch(behavior)
            {
                case "GuardianTaunt":
                    // RejectHarmfulEffect notifies gameplay listeners; readiness only reads immunity.
                    if(distance<=radius&&!enemy.combatStatuses.ControlImmune&&!enemy.combatStatuses.DebuffImmune)return true;
                    break;
                case "ClusterBreath":
                    if(distance<=radius&&Vector3.Dot(forward,offset.normalized)>=.55f)return true;
                    break;
                case "EarthPulse":
                    if(forwardDeploy?distance<=radius*1.8f&&offset.sqrMagnitude>.001f&&
                       Vector3.Dot(forward,offset.normalized)>=.55f:distance<=radius)return true;
                    break;
                case "TyrantTremor":if(distance<=radius)return true;break;
                case "JusticeRoar":
                    if(IsCombatTargetable(enemy)&&distance<=radius+enemy.collisionRadius)return true;
                    break;
            }
        }
        return false;
    }
}
