using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class PendingSkillFocus
    {
        public Unit unit;
        public bool combinedCastTrigger;
        public QueuedCombinedSkillGroup combinedGroup;
        public Unit repeatTarget;
        public Unit castTarget;
        public System.Collections.Generic.List<Unit> castTargets;
        public double nextAttemptAt;
        public float repeatRatio=-1f;
    }

    private bool ShouldPresentCommonSkill(Unit unit)
    {
        if(mlEpisodeActive||!battleStarted||battleEnding||paused||GameFrontend.BlocksGameplay||
           !IsCombatActive(unit)||unit.spiritConcertOwner!=null||unit.augmentShadowClone||
           unit.mantisBroodOwner!=null||unit.spriteAnimation==null||combatCameraDirector==null)return false;
        var renderer=unit.spriteAnimation.GetComponent<SpriteRenderer>();
        return renderer!=null&&renderer.enabled;
    }

    private bool CanCommitSkillFocus(PendingSkillFocus request)=>CanContinueSkillFocus(request,true);

    private bool CanContinueSkillFocus(PendingSkillFocus request,bool requireReady)
    {
        if(!requireReady)return request!=null&&battleStarted&&!battleEnding&&IsCombatActive(request.unit);
        if(request==null||!ShouldPresentCommonSkill(request.unit))return false;
        Unit unit=request.unit;
        bool opponent=false;
        for(int i=0;i<units.Count;i++)
            if(IsCombatActive(units[i])&&units[i].team!=unit.team&&
               !units[i].silkPuppet&&!units[i].rank3MycokingSummon){opponent=true;break;}
        if(!opponent)return false;
        return !unit.combatStatuses.Has(CombatStatusKind.Silence)&&
            !unit.combatStatuses.Has(CombatStatusKind.Fear)&&
            !unit.combatStatuses.Has(CombatStatusKind.Freeze)&&
            !unit.combatStatuses.Has(CombatStatusKind.Knockdown)&&
            !unit.combatStatuses.Has(CombatStatusKind.Airborne)&&!SpiderSilkBound(unit)&&
            unit.giraffeStunTimer<=0f&&unit.crocodileImmobilizedTimer<=0f&&!unit.displacing&&
            unit.preyDraggedBy==null&&unit.griffinHeldBy==null&&!unit.crocodilePreyFlying&&
            unit.sabertoothDevouredBy==null&&unit.sabertoothDevour==null&&
            unit.frogGrandSlamHeldBy==null&&unit.frogGrandSlam==null&&
            unit.crocodileAbyssHeldBy==null&&unit.crocodileAbyssDive==null&&unit.wizardCygnusShower==null&&
            unit.tyrannoPushedBy==null&&unit.tyrannoMawPulledBy==null&&
            unit.beetleFlippedBy==null&&unit.beetleGravityHeld==null&&unit.beetleOverturn==null&&
            !(unit.beetleOverturnedBy!=null&&unit.beetleOverturnedBy.flying)&&
            !unit.bearKingRoarEnding&&!unit.bearAvatarRoarPending&&
            !unit.spriteAnimation.IsActionLocked&&unit.rank3SkillLockTimer<=0f&&
            (request.combinedCastTrigger||request.repeatRatio>=0f||unit.skillTimer<=0f)&&
            (IsCombatTargetable(FocusedRequestTarget(request))||
             HaetaeSupportTargets(unit,GameMasterData.Skill(unit.skillId))>0)&&
            (!requireReady||FocusedSkillReady(unit,request.repeatRatio>=0f?request.repeatTarget:unit.target,request.repeatRatio>=0f));
    }

    private static bool PreparedSkillNeedsControlUpdate(Unit unit)=>
        unit.crocodileAbyssHeldBy!=null||unit.crocodileAbyssDive!=null||
        unit.frogGrandSlamHeldBy!=null||unit.frogGrandSlam!=null||
        unit.sabertoothDevouredBy!=null||unit.displacing||unit.preyDraggedBy!=null||unit.griffinHeldBy!=null||unit.crocodilePreyFlying||
        unit.tyrannoPushedBy!=null||unit.tyrannoMawPulledBy!=null||unit.beetleFlippedBy!=null||
        unit.beetleGravityHeld!=null||unit.beetleOverturn!=null||unit.mantisMolt!=null||unit.monkeyNimbus!=null||
        unit.beetleOverturnedBy!=null&&unit.beetleOverturnedBy.flying||
        unit.combatStatuses.Has(CombatStatusKind.Fear)||unit.combatStatuses.Has(CombatStatusKind.Airborne)||
        unit.bearKingRoarEnding||unit.bearAvatarRoarPending;

    private bool PreparedSkillReleaseReady(PendingSkillFocus request)
    {
        if(!CanContinueSkillFocus(request,false)||paused||GameFrontend.BlocksGameplay||Time.timeScale<=0f)return false;
        Unit unit=request.unit;
        if(PreparedSkillNeedsControlUpdate(unit)||unit.spriteAnimation==null||
           unit.spriteAnimation.IsActionLocked||unit.rank3SkillLockTimer>0f||unit.augmentReturnTimer>0f||
           unit.combatStatuses.Has(CombatStatusKind.Silence)||unit.combatStatuses.Has(CombatStatusKind.Freeze)||
           unit.combatStatuses.Has(CombatStatusKind.Knockdown)||SpiderSilkBound(unit)||
           unit.giraffeStunTimer>0f||unit.crocodileImmobilizedTimer>0f)return false;
        bool opponent=false;
        for(int i=0;i<units.Count;i++)
            if(IsCombatActive(units[i])&&units[i].team!=unit.team&&
               !units[i].silkPuppet&&!units[i].rank3MycokingSummon){opponent=true;break;}
        return opponent&&RefreshFocusedCastTarget(request);
    }

    private bool CanStartFocusedSkill(Unit source,Unit target)
    {
        return FocusedSkillReady(source,target,false);
    }

    private bool FocusedSkillReady(Unit source,Unit target,bool repeated)
    {
        SkillMasterRow skill=GameMasterData.Skill(source.skillId);
        if(skill==null)return false;
        switch(skill.behavior)
        {
            case "GuardianTaunt": case "ClusterBreath": case "EarthPulse": case "TyrantTremor": case "JusticeRoar":
                return CanStartFocusedAreaSkill(source,target,skill,repeated);
            case "FangLock":
                if(UsesSabertoothDevour(source))return IsCombatTargetable(target);
                return IsCombatTargetable(target)&&Vector3.Distance(Flat(source.model.transform.position),
                    Flat(target.model.transform.position))<=AttackDistance(source,target)+TileSize*.25f;
            case "DeathRoll":
                if(!IsCombatTargetable(target))return false;
                float biteReach=MonsterTraitRuntime.Has(source.activeTraitIds,"trait_skill_charge_bite")
                    ?TileSize*3f+source.collisionRadius+target.collisionRadius
                    :AttackDistance(source,target)+TileSize*.35f;
                return Vector3.Distance(Flat(source.model.transform.position),Flat(target.model.transform.position))<=biteReach;
            case "HighHeadSlam":
                return IsCombatTargetable(target)&&Vector3.Distance(Flat(source.model.transform.position),
                    Flat(target.model.transform.position))<=AttackDistance(source,target)+.15f;
            case "TongueHarpoon": case "ThreeSidedHunt": case "GiantCaptureWeb":
            case "AcrobaticBarrage": case "SkyFlip": case "SporeColony": case "CrossScytheWind":
                return IsCombatTargetable(target)&&Vector3.Distance(Flat(source.model.transform.position),
                    Flat(target.model.transform.position))<=SkillActivationDistance(source,target,skill);
            case "WyvernBreath":
                if(!IsCombatTargetable(target))return false;
                if(HasFamilyTranscendence(source))return true;
                float breathReach=HasWyvernRank3(source)?SkillActivationDistance(source,target,skill):
                    TileSize*Mathf.Max(1f,skill.castRangeTiles)+source.collisionRadius+target.collisionRadius;
                return Vector3.Distance(Flat(source.model.transform.position),Flat(target.model.transform.position))<=breathReach;
            case "Skyfall": case "SkyDive":
                return GriffinDiveTarget(source,skill)!=null;
            case "EarthCharge":
                if(!IsCombatTargetable(target))return false;
                if(HasFamilyTranscendence(source))return true;
                float chargeReach=SkillActivationDistance(source,target,skill);
                if(HasRank3(source,MammothRank3Rules.MountainBrace))
                {
                    float plannedRadius=FocusedSkillRadiusMultiplier(source,repeated);
                    chargeReach=TileSize*MammothRank3Rules.BraceSweepTiles*plannedRadius+target.collisionRadius;
                }
                return Vector3.Distance(Flat(source.model.transform.position),Flat(target.model.transform.position))<=chargeReach;
            case "AssassinLeap":
                return SelectRaptorLeapTarget(source,Mathf.Max(1f,skill.castRangeTiles))!=null;
            case "MagicStarBarrage": case "ShadowHunt":
                for(int i=0;i<units.Count;i++)
                {
                    Unit enemy=units[i];
                    if(enemy.team==source.team||!IsCombatActive(enemy)||
                       skill.behavior=="ShadowHunt"&&!IsCombatTargetable(enemy))continue;
                    if(Vector3.Distance(Flat(source.model.transform.position),Flat(enemy.model.transform.position))<=
                       SkillActivationDistance(source,enemy,skill))return true;
                }
                return false;
            default:return true;
        }
    }

    private bool QueueRepeatedSkillFocus(Unit source,Unit target,float ratio)
    {
        if(!ShouldPresentCommonSkill(source)||HasSkillCastPresentation(source))return false;
        var request=new PendingSkillFocus{unit=source,repeatTarget=target,repeatRatio=ratio};
        return BeginSkillPreparation(request);
    }

    private bool ExecuteFocusedSkill(PendingSkillFocus request)
    {
        float grandReady=CorruptValue(request.unit,"grand_ready"),grandIdle=CorruptValue(request.unit,"grand_idle");
        bool used=RunCommittedFocusedSkill(request);
        if(!used&&grandReady>0f)
        {
            request.unit.corruptValues["grand_ready"]=grandReady;
            request.unit.corruptValues["grand_idle"]=grandIdle;
        }
        // Keep the accepted request pending while a transient geometry failure retries.
        if(!used)request.unit.skillTimer=Mathf.Max(request.unit.skillTimer,.5f);
        if(used)
        {
            BeginLocalSkillPresentation(request.unit);
            if(request.combinedGroup!=null)CommitCombinedSkillFocus(request.combinedGroup,request.unit);
        }
        return used;
    }
}
