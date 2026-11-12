using System.Collections.Generic;
using BombBrick.Combat;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class GriffinHuntState
    {
        public Unit target;
        public Vector3 launch,landing,casterLanding,direction;
        public float time,scale,radiusScale,dealt;
        public int generation,revision,targetRevision,hits;
        public bool grabbed,landed,carryPose;
        public CombatStatusEffect hold;
        public PixelMammothCharge impact;
    }

    private bool GriffinHuntValid(Unit source,GriffinHuntState hunt)=>
        !battleEnding&&source.griffinHunt==hunt&&IsCombatActive(source)&&hunt.generation==combatGeneration&&
        source.positionRevision==hunt.revision&&!SpiderSilkBound(source)&&
        !source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !source.combatStatuses.Has(CombatStatusKind.Airborne)&&
        !source.combatStatuses.Has(CombatStatusKind.Fear)&&source.giraffeStunTimer<=0f;

    private void StartGriffinHunt(Unit source,Unit target,int generation)
    {
        var hunt=new GriffinHuntState {target=target,launch=Flat(source.model.transform.position),
            generation=generation,revision=source.positionRevision,
            scale=source.griffinSkyfallDamageMultiplier,radiusScale=source.griffinSkyfallRadiusMultiplier};
        source.griffinHunt=hunt;source.griffinSkyfalling=true;source.target=target;
        source.rank3GriffinLaunch=hunt.launch;
        source.griffinSkyfallDamageMultiplier=source.griffinSkyfallRadiusMultiplier=1f;
        AimGriffinHunt(source,hunt);
        if(!mlEpisodeActive)
        {
            hunt.impact=PixelMammothCharge.Play(Rank3Renderer(source),hunt.direction,false);
            hunt.impact.name="Griffin Hunting Talon Impact";hunt.impact.transform.SetParent(transform,true);
            source.griffinDiveFx=PixelGriffinDive.Create(Rank3Renderer(source),hunt.launch,hunt.landing,
                TileSize*.75f,GriffinHuntRules.Fall,GriffinDiveTint(source),CaptureSkillTime(source),
                ()=>generation==combatGeneration&&IsCombatActive(source),false,true);
            if(source.griffinDiveFx!=null)source.griffinDiveFx.transform.SetParent(transform,true);
        }
        // The driver applies combat tempo; convert the already paced hunt clock back to world seconds.
        source.spriteAnimation?.PlaySkillAttackWithHold(GriffinCombatRules.SkyDiveAscentFrames-1,
            GriffinHuntRules.Grab,1f,null,()=>hunt.time/CombatStatFormula.CombatAnimationRate);
    }

    private void AimGriffinHunt(Unit source,GriffinHuntState hunt)
    {
        hunt.landing=Flat(hunt.target.model.transform.position);
        hunt.direction=(hunt.landing-hunt.launch).normalized;
        if(hunt.direction.sqrMagnitude<.001f)hunt.direction=Vector3.right;
        float separation=source.collisionRadius+hunt.target.collisionRadius+UnitMovementClearance+.05f;
        hunt.casterLanding=ResolveFreePosition(hunt.landing-hunt.direction*separation,source.collisionRadius);
        source.griffinSkyfallOrigin=hunt.landing;
        source.spriteBillboard?.SetFacingDirection(hunt.direction,true);
        source.griffinDiveFx?.SetLanding(hunt.landing);
    }

    private void UpdateGriffinHunt(Unit source,float dt)
    {
        var hunt=source.griffinHunt;if(hunt==null)return;
        if(!GriffinHuntValid(source,hunt)){EndGriffinHunt(source);return;}
        Unit target=hunt.target;
        if(!hunt.landed&&!IsCombatActive(target)){EndGriffinHunt(source);return;}
        float previousTime=hunt.time;
        hunt.time=Mathf.Min(GriffinHuntRules.End,hunt.time+Mathf.Max(0,dt));
        if(hunt.time>=GriffinHuntRules.Landing-.00001f&&hunt.time<GriffinHuntRules.Landing)
            hunt.time=GriffinHuntRules.Landing;
        float time=hunt.time;
        if(!hunt.grabbed)
        {
            if(!IsCombatTargetable(target)||target.griffinHeldBy!=null)
            {EndGriffinHunt(source);return;}
            AimGriffinHunt(source,hunt);
        }
        PoseGriffinHunt(source,hunt);
        if(previousTime<GriffinHuntRules.DashStart&&time>=GriffinHuntRules.DashStart)
            PlayGriffinSound(source,GriffinSound.Rush,hunt.launch,.85f);
        if(previousTime<GriffinHuntRules.Fall&&time>=GriffinHuntRules.Fall)
            PlayGriffinSound(source,GriffinSound.Dive,hunt.landing,.8f);
        if(!hunt.grabbed&&time>=GriffinHuntRules.Grab)
        {
            hunt.grabbed=true;hunt.targetRevision=target.positionRevision;
            if(!PreyDisplacementImmune(target)&&!target.combatStatuses.Has(CombatStatusKind.Freeze)&&
               !target.combatStatuses.Has(CombatStatusKind.Knockdown)&&!target.combatStatuses.Has(CombatStatusKind.Airborne))
            {
                InterruptPositionalAction(target);target.spriteAnimation?.InterruptCurrentAction(true);
                ClearRoute(target);target.hexStepping=false;target.displacing=false;
                target.velocity=Vector3.zero;SetMoving(target,false);
                float duration=GriffinHuntRules.Landing-GriffinHuntRules.Grab;
                var hold=target.combatStatuses.ApplyTimed(CombatStatusKind.Airborne,duration,source,duration);
                if(hold.remaining>0f)
                {
                    hunt.hold=hold;hold.activeHolds++;
                    target.griffinHeldBy=source;target.spriteAnimation?.SetStruggling(true,()=>hunt.time);
                }
                hunt.targetRevision=target.positionRevision;
            }
            source.spriteAnimation?.HoldSkillAttackHitFrame(GriffinHuntRules.Landing-GriffinHuntRules.Grab);
            hunt.impact?.Hit(Rank3Center(target),time,false);
            PlayGriffinSound(source,GriffinSound.Grab,hunt.landing);
            if(!mlEpisodeActive)PixelCombatEffect.PlayGriffinEagleClaw(
                Rank3Renderer(target)==null?target.model.transform:Rank3Renderer(target).transform,
                Rank3Renderer(target),1.8f,false,.8f);
            GriffinHuntHit(source,hunt,GriffinHuntRules.GrabRatio);
            if(source.griffinHunt!=hunt)return;
            if(!IsCombatActive(target)){EndGriffinHunt(source);return;}
            PlayGriffinSound(source,GriffinSound.Rise,hunt.landing,.55f);
        }
        if(hunt.grabbed&&!hunt.carryPose&&time>=GriffinHuntRules.Grab+.05f&&time<GriffinHuntRules.Fall)
        {
            hunt.carryPose=true;
            source.spriteAnimation?.HoldSkillAttackFrame(GriffinCombatRules.SkyDiveAscentFrames-1,GriffinHuntRules.Fall-time);
        }
        if(target.griffinHeldBy==source)
        {
            if(target.positionRevision!=hunt.targetRevision||PreyDisplacementImmune(target)||
               target.combatStatuses.Get(CombatStatusKind.Airborne)!=hunt.hold||hunt.hold.source!=source)
                ReleaseGriffinHuntVictim(source,hunt);
            else target.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.up*GriffinHuntRules.Lift(time));
        }
        if(!hunt.landed&&time>=GriffinHuntRules.Landing)
        {
            hunt.landed=true;ReleaseGriffinHuntVictim(source,hunt);
            PlayGriffinSound(source,GriffinSound.Slam,hunt.landing);
            source.griffinDiveFx?.Land();source.griffinDiveFx=null;
            hunt.impact?.Hit(Rank3Center(target),time,true);
            hunt.impact?.Impact(hunt.landing,time,TileSize*.75f,true);
            source.spriteAnimation?.HoldSkillAttackHitFrame(.10f);
            if(!mlEpisodeActive)
            {
                combatCameraDirector?.ReportGroundSlam(true);
                PixelCombatEffect.PlayTyrannoTremorHit(target.model.transform,Rank3Renderer(target));
            }
            if(IsCombatActive(target)&&target.positionRevision==hunt.targetRevision&&
               Vector3.Distance(Flat(target.model.transform.position),hunt.landing)<=TileSize)
            {
                GriffinHuntHit(source,hunt,GriffinHuntRules.SlamRatio);
                Rank3Knockdown(source,target,GriffinHuntRules.Knockdown);
                float previous=source.skillTraitDamageMultiplier;source.skillTraitDamageMultiplier=hunt.scale;
                try { ApplyGriffinSkyfallIntrinsic(source,hunt.landing,new List<Unit>{target},
                    TileSize*GameMasterData.Skill(source.skillId).radiusTiles*hunt.radiusScale,1f,Rank3Renderer(source)); }
                finally {source.skillTraitDamageMultiplier=previous;}
            }
            source.traitActivations++;
            RecordSkillEvent(source,GriffinRank3Rules.RaptorHunt,hunt.hits,hunt.dealt);
            if((Flat(source.model.transform.position)-hunt.launch).sqrMagnitude>.000001f&&
               GriffinHuntValid(source,hunt))BeginSkillInertia(source,
                Flat(source.model.transform.position)-hunt.launch,GriffinHuntRules.Grab-GriffinHuntRules.DashStart);
        }
        if(time>=GriffinHuntRules.End)EndGriffinHunt(source);
    }

    private void GriffinHuntHit(Unit source,GriffinHuntState hunt,float ratio)
    {
        float previous=source.skillTraitDamageMultiplier;source.skillTraitDamageMultiplier=hunt.scale;
        float dealt;
        try {dealt=ApplySkillDamage(source,hunt.target,SkillPower(source)*ratio);}
        finally {source.skillTraitDamageMultiplier=previous;}
        source.skillDamage+=dealt;source.skillHits++;hunt.dealt+=dealt;hunt.hits++;
    }

    private void PoseGriffinHunt(Unit source,GriffinHuntState hunt)
    {
        float time=hunt.time,dash=Mathf.Clamp01((time-GriffinHuntRules.DashStart)/(GriffinHuntRules.Grab-GriffinHuntRules.DashStart));
        float fall=GriffinCombatRules.GlideProgress(time,GriffinHuntRules.Fall);
        float carry=Mathf.Clamp01((time-GriffinHuntRules.Grab)/.12f)*(1f-fall);
        Vector3 ground=hunt.landed?Flat(source.model.transform.position):
            Vector3.Lerp(hunt.launch,hunt.casterLanding,dash)+(hunt.landing-hunt.casterLanding)*(.8f*carry);
        float lift=time<GriffinHuntRules.Grab?Mathf.Sin(dash*Mathf.PI)*2.2f+GriffinHuntRules.CarryClearance*dash:
            GriffinHuntRules.Lift(time)+GriffinHuntRules.CarryClearance*(1f-fall);
        source.model.transform.position=ground+Vector3.up*(source.visualHeightOffset+lift);
        source.lastGroundPosition=ground;source.currentTile=NearestGridCoordinate(ground);
        source.ring.transform.position=ground+Vector3.up*.025f;
        source.velocity=Vector3.zero;SetMoving(source,false);
        float sign=Rank3Renderer(source)!=null&&Rank3Renderer(source).flipX?-1f:1f;
        float roll=time<GriffinHuntRules.Grab?-sign*28f*Mathf.Sin(dash*Mathf.PI):
            time<GriffinHuntRules.Landing?-sign*12f*fall:0f;
        source.spriteBillboard?.SetRollPose(roll,Vector3.zero,Vector3.zero);
        source.spriteAnimation?.SetSkillPoseScale(time<GriffinHuntRules.Grab?new Vector3(1.12f,.85f,1f):
            time<GriffinHuntRules.Landing?new Vector3(.95f,1.12f,1f):
            Vector3.Lerp(new Vector3(1.18f,.8f,1f),Vector3.one,(time-GriffinHuntRules.Landing)/.18f));
        source.griffinDiveFx?.Pose(time);hunt.impact?.Pose(hunt.landing,time);
        source.currentAction=time<GriffinHuntRules.Grab?"griffin_hunt_swoop":time<GriffinHuntRules.Apex?
            "griffin_hunt_carry":time<GriffinHuntRules.Fall?"griffin_hunt_apex":time<GriffinHuntRules.Landing?
                "griffin_hunt_plunge":"griffin_hunt_recover";
    }

    private static void ReleaseGriffinHuntVictim(Unit source,GriffinHuntState hunt)
    {
        if(hunt.hold!=null)
        {
            hunt.hold.activeHolds=Mathf.Max(0,hunt.hold.activeHolds-1);
            if(hunt.target.combatStatuses.Get(CombatStatusKind.Airborne)==hunt.hold&&hunt.hold.source==source)
                hunt.target.combatStatuses.Remove(CombatStatusKind.Airborne);
            hunt.hold=null;
        }
        if(hunt.target.griffinHeldBy!=source)return;
        hunt.target.griffinHeldBy=null;
        hunt.target.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);
        hunt.target.spriteAnimation?.SetStruggling(false);
    }

    private static void EndGriffinHunt(Unit source)
    {
        var hunt=source.griffinHunt;if(hunt==null)return;
        if(!hunt.landed||hunt.time<GriffinHuntRules.End||source.positionRevision!=hunt.revision||
           !IsCombatActive(source))ClearUnitInertia(source);
        source.griffinHunt=null;ReleaseGriffinHuntVictim(source,hunt);
        source.griffinSkyfalling=false;source.griffinSkyfallProgress=0f;
        source.griffinDiveFx?.Cancel();source.griffinDiveFx=null;hunt.impact?.Cancel();
        if(source.model!=null)source.model.transform.position=Flat(source.model.transform.position)+Vector3.up*source.visualHeightOffset;
        source.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);
        source.spriteAnimation?.SetSkillPoseScale(Vector3.one);
        source.spriteAnimation?.InterruptCurrentAction(true);
        source.hexStepTarget=source.currentTile;
    }
}
