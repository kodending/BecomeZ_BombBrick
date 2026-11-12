using BombBrick.Game.Core;
using BombBrick.Combat;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class BeetleFlipState
    {
        public Unit target;
        public SkillMasterRow skill;
        public Vector3 landing,direction,jumpFrom,jumpTo;
        public int generation,revision,targetRevision,hits;
        public float time,scale,radiusScale,dealt;
        public bool hooked,landed,crush,leapt,clamped,dived,casterLanded;
        public PixelMammothCharge effect;
        public float Fall=>crush?BeetleFlipRules.CrushFall:BeetleFlipRules.FallStart;
        public float Landing=>crush?BeetleFlipRules.CrushLanding:BeetleFlipRules.Landing;
        public float End=>crush?BeetleFlipRules.CrushEnd:BeetleFlipRules.End;
    }

    private bool HasBeetleRank3(Unit unit)=>HasRank3(unit,BeetleRank3Rules.ZeroGravity);

    private bool BeetleFlipValid(Unit source,BeetleFlipState flip)=>!battleEnding&&source.beetleFlip==flip&&
        flip.generation==combatGeneration&&source.positionRevision==flip.revision&&IsCombatActive(source)&&
        !SpiderSilkBound(source)&&!source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !source.combatStatuses.Has(CombatStatusKind.Airborne)&&!source.combatStatuses.Has(CombatStatusKind.Fear)&&
        source.giraffeStunTimer<=0f;

    private void StartBeetleFlip(Unit source,Unit target,SkillMasterRow skill,int generation)
    {
        source.beetleSkyFlipPending=false;source.beetleSkyFlipTarget=null;
        if(source.beetleFlip!=null||!IsCombatActive(target)||skill==null)return;
        Vector3 direction=Flat(target.model.transform.position-source.model.transform.position).normalized;
        if(direction.sqrMagnitude<.001f)direction=source.facingDirection;
        var flip=new BeetleFlipState{target=target,skill=skill,generation=generation,
            revision=source.positionRevision,landing=Flat(target.model.transform.position),direction=direction,
            crush=HasRank3(source,BeetleRank3Rules.GoldenCoffin),jumpFrom=Flat(source.model.transform.position),
            scale=source.beetleSkyFlipDamageMultiplier,radiusScale=source.beetleSkyFlipRadiusMultiplier};
        source.beetleFlip=flip;source.beetleSkyFlipDamageMultiplier=source.beetleSkyFlipRadiusMultiplier=1f;
        ClearRoute(source);source.hexStepping=false;source.velocity=Vector3.zero;
        source.spriteBillboard?.SetFacingDirection(direction,true);SetMoving(source,false);
        if(!mlEpisodeActive)
        {
            flip.effect=PixelMammothCharge.Play(Rank3Renderer(source),direction);
            flip.effect.name=flip.crush?"Pixel Beetle Skyfall Crush":"Pixel Beetle Horn Landing";
            flip.effect.transform.SetParent(transform,true);
        }
        source.spriteAnimation?.PlayBeetleFlip(()=>BeetleFlipValid(source,flip),
            time=>AdvanceBeetleFlip(source,flip,time),()=>EndBeetleFlip(source,flip),flip.crush,1f/ConcertDurationScale(source));
    }

    private void AdvanceBeetleFlip(Unit source,BeetleFlipState flip,float time)
    {
        if(!BeetleFlipValid(source,flip)){EndBeetleFlip(source,flip);return;}
        flip.time=time;Unit target=flip.target;
        flip.effect?.Pose(Flat(source.model.transform.position),time);
        source.currentAction=time<BeetleFlipRules.Hook?"beetle_hook_prepare":
            time<flip.Fall?"beetle_flip_lift":time<flip.Landing?"beetle_flip_slam":"beetle_flip_recover";
        if(!flip.hooked&&time>=BeetleFlipRules.Hook)
        {
            flip.hooked=true;
            // The cast already accepted its target; movement during the windup cannot revoke it.
            if(!IsCombatActive(target))
            {EndBeetleFlip(source,flip);return;}
            flip.landing=Flat(target.model.transform.position);
            flip.targetRevision=target.positionRevision;
            float separation=source.collisionRadius+target.collisionRadius+UnitMovementClearance+.05f;
            flip.jumpTo=Vector3.MoveTowards(flip.jumpFrom,flip.landing,
                Mathf.Max(0,Vector3.Distance(flip.jumpFrom,flip.landing)-separation));
            // Reserve only the sprite pose. The target keeps its ground occupancy and remains hittable.
            if(target.beetleFlippedBy==null&&!PreyDisplacementImmune(target)&&
               !target.combatStatuses.Has(CombatStatusKind.Freeze)&&!target.combatStatuses.Has(CombatStatusKind.Knockdown)&&
               !target.combatStatuses.Has(CombatStatusKind.Airborne))
            {
                InterruptPositionalAction(target);ClearRoute(target);
                target.hexStepping=false;target.velocity=Vector3.zero;
                target.beetleFlippedBy=source;flip.targetRevision=target.positionRevision;
                SetMoving(target,false);target.spriteAnimation?.SetStruggling(true);
                float airborne=(flip.Landing-BeetleFlipRules.Hook)/CombatStatFormula.CombatAnimationRate;
                target.combatStatuses.ApplyTimed(CombatStatusKind.Airborne,airborne,source,airborne);
            }
            flip.effect?.Hit(Rank3Center(target),time,false);
            PlayBeetleSound(source,BeetleSound.Flip,flip.landing);
            float dealt=ApplySkillFormulaDamage(source,target,flip.skill,false,0,flip.scale);
            source.skillDamage+=dealt;source.skillHits++;flip.dealt+=dealt;flip.hits++;
        }
        if(target.beetleFlippedBy==source)
        {
            if(!IsCombatActive(target)||target.positionRevision!=flip.targetRevision||PreyDisplacementImmune(target))
                ReleaseBeetleVictim(source,flip);
            else
            {
                float rise=Mathf.Clamp01((time-BeetleFlipRules.Hook)/(BeetleFlipRules.RiseEnd-BeetleFlipRules.Hook));
                float fall=Mathf.Clamp01((time-flip.Fall)/(flip.Landing-flip.Fall));
                float lift=Mathf.Sin(rise*Mathf.PI*.5f)*(1f-(flip.crush?fall:fall*fall));
                float catchUp=flip.crush?Mathf.Clamp01((time-BeetleFlipRules.CrushJump)/(BeetleFlipRules.CrushApex-BeetleFlipRules.CrushJump)):0f;
                Vector3 offset=Vector3.up*(TileSize*BeetleFlipRules.HeightTiles*lift)-flip.direction*(TileSize*.45f*lift*(1-catchUp));
                float sign=Vector3.Dot(Camera.main==null?Vector3.right:Camera.main.transform.right,flip.direction)>=0?1f:-1f;
                target.spriteBillboard?.SetRollPose(sign*(150f*rise+40f*fall),new Vector3(0,.5f,0),offset);
            }
        }
        if(flip.crush)PoseBeetleCrush(source,flip,time);
        if(!flip.landed&&time>=flip.Landing)
        {
            flip.landed=true;bool held=target.beetleFlippedBy==source;
            ReleaseBeetleVictim(source,flip);
            if(held&&IsCombatActive(target))
            {
                var knockdown=target.combatStatuses.ApplyTimed(CombatStatusKind.Knockdown,
                    HostileStatusDuration(target,BeetleFlipRules.Knockdown),source);
                target.spriteAnimation?.SetKnockdownActive(knockdown.remaining);
            }
            int hits;float dealt=BeetleLandingDamage(source,flip.landing,flip.skill,flip.scale,flip.radiusScale,false,out hits,
                flip.crush&&target.positionRevision==flip.targetRevision?target:null);
            dealt+=BeetleLightningDamage(source,target,flip.landing,flip.scale,false,out int lightningHits);hits+=lightningHits;
            source.skillDamage+=dealt;source.skillHits+=hits;flip.dealt+=dealt;flip.hits+=hits;
            float radius=TileSize*flip.skill.radiusTiles*flip.radiusScale;
            flip.effect?.Impact(flip.landing,time,radius,true);
            PlayBeetleSound(source,flip.crush?BeetleSound.Crush:BeetleSound.Land,flip.landing);
            if(!mlEpisodeActive)
            {combatCameraDirector?.ReportGroundSlam(true);meadowAmbientParticles?.Impact(flip.landing,radius,Rank3Renderer(source));}
            RecordSkillEvent(source,flip.skill.id+"_slam",flip.hits,flip.dealt);
            if(flip.crush)source.traitActivations++;
        }
        if(time>=flip.End)EndBeetleFlip(source,flip);
    }

    private void PoseBeetleCrush(Unit source,BeetleFlipState flip,float time)
    {
        if(time<BeetleFlipRules.CrushJump)return;
        float rise=Mathf.Clamp01((time-BeetleFlipRules.CrushJump)/(BeetleFlipRules.CrushApex-BeetleFlipRules.CrushJump));
        float fall=Mathf.Clamp01((time-BeetleFlipRules.CrushFall)/(BeetleFlipRules.CrushLanding-BeetleFlipRules.CrushFall));
        float settle=Mathf.Clamp01((time-BeetleFlipRules.CrushLanding)/(BeetleFlipRules.CrushCasterLanding-BeetleFlipRules.CrushLanding));
        // A direct dash into the catch, a full stop, then an immediate plunge without easing in.
        float height=Mathf.Lerp((TileSize*BeetleFlipRules.HeightTiles+.6f)*rise,.35f,fall)*(1-settle);
        Vector3 old=Flat(source.model.transform.position);
        Vector3 next=BlockRank3IceWallMovement(source,old,ResolveUnitMovement(source,old,
            Vector3.Lerp(flip.jumpFrom,flip.jumpTo,rise)));
        source.model.transform.position=next+Vector3.up*source.visualHeightOffset;
        source.lastGroundPosition=next;source.currentTile=NearestGridCoordinate(next);
        if(source.ring!=null)source.ring.transform.position=next+Vector3.up*.025f;
        float sign=Vector3.Dot(Camera.main==null?Vector3.right:Camera.main.transform.right,flip.direction)>=0?1f:-1f;
        float roll=-sign*Mathf.Lerp(65f,135f,fall)*rise*
            (1-Mathf.Clamp01((time-BeetleFlipRules.CrushLanding)/.16f));
        source.spriteBillboard?.SetRollPose(roll,new Vector3(0,.5f,0),
            Vector3.up*height);
        source.currentAction=time<BeetleFlipRules.CrushApex?"beetle_crush_jump":
            time<BeetleFlipRules.CrushFall?"beetle_crush_hold":time<BeetleFlipRules.CrushCasterLanding?"beetle_crush_dive":"beetle_crush_recover";
        if(!flip.leapt)
        {
            flip.leapt=true;flip.effect?.Step(flip.jumpFrom,time,1);
            PlayBeetleSound(source,BeetleSound.Jump,flip.jumpFrom,.85f);
        }
        if(!flip.clamped&&time>=BeetleFlipRules.CrushApex)
        {flip.clamped=true;if(IsCombatActive(flip.target))flip.effect?.Hit(Rank3Center(flip.target),time,false);}
        if(!flip.dived&&time>=BeetleFlipRules.CrushFall)
        {
            flip.dived=true;
            PlayBeetleSound(source,BeetleSound.Dive,next,.85f);
        }
        if(!flip.casterLanded&&time>=BeetleFlipRules.CrushCasterLanding)
        {flip.casterLanded=true;flip.effect?.Step(next,time,0);}
    }

    private static void ReleaseBeetleVictim(Unit source,BeetleFlipState flip)
    {
        Unit target=flip.target;if(target==null||target.beetleFlippedBy!=source)return;
        target.beetleFlippedBy=null;
        target.spriteBillboard?.SetRollPose(0,Vector3.zero,Vector3.zero);
        target.spriteAnimation?.SetStruggling(false);
        var airborne=target.combatStatuses.Get(CombatStatusKind.Airborne);
        if(airborne!=null&&airborne.source==source)target.combatStatuses.Remove(CombatStatusKind.Airborne);
    }

    private void EndBeetleFlip(Unit source,BeetleFlipState flip)
    {
        if(source.beetleFlip!=flip)return;
        bool completed=flip.crush&&flip.landed&&flip.casterLanded&&flip.time>=flip.End&&
            BeetleFlipValid(source,flip);
        Vector3 travelled=completed?Flat(source.model.transform.position)-flip.jumpFrom:Vector3.zero;
        source.beetleFlip=null;ReleaseBeetleVictim(source,flip);flip.effect?.Cancel();
        if(flip.crush)source.spriteBillboard?.SetRollPose(0,Vector3.zero,Vector3.zero);
        source.attackMovementLockTimer=0f;source.spriteAnimation?.SetSkillPoseScale(Vector3.one);
        if(!battleEnding&&flip.landed&&flip.generation==combatGeneration&&IsCombatActive(source)&&
           HasRank3(source,BeetleRank3Rules.MassTheft))GrantBeetleSweep(source,flip.landing);
        if(completed)BeginSkillInertia(source,travelled,BeetleFlipRules.CrushApex-BeetleFlipRules.CrushJump);
    }
}
