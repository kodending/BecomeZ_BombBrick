using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MammothChargeState
    {
        public Vector3 start,end,direction;
        public float time,scale,radiusScale,damage;
        public int generation,revision,hits,step,walls,wallsRaised;
        public bool wallRush;
        public PixelMammothWallRush wallEffect;
        public bool launched,landed,blocked,shook,previousImmunity;
        public PixelMammothCharge effect;
        public readonly HashSet<Unit> hit=new HashSet<Unit>();
    }

    private static bool HasMammothRank3(Unit source)=>HasRank3(source,MammothRank3Rules.MountainToss)||
        HasRank3(source,MammothRank3Rules.WallRush)||HasRank3(source,MammothRank3Rules.MountainBrace);

    private bool MammothChargeValid(Unit source,MammothChargeState charge)=>!battleEnding&&source.mammothCharge==charge&&
        charge.generation==combatGeneration&&source.positionRevision==charge.revision&&IsCombatActive(source)&&
        !SpiderSilkBound(source)&&!source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !source.combatStatuses.Has(CombatStatusKind.Fear)&&source.giraffeStunTimer<=0f;

    private void StartMammothCharge(Unit source,int generation)
    {
        if(source.mammothCharge!=null)return;
        var charge=new MammothChargeState {start=source.mammothChargeStart,end=source.mammothChargeEnd,
            scale=source.mammothChargeDamageMultiplier,radiusScale=source.mammothChargeRadiusMultiplier,
            generation=generation,revision=source.positionRevision,previousImmunity=source.combatStatuses.PersonalDebuffImmune,
            wallRush=HasRank3(source,MammothRank3Rules.WallRush)};
        charge.direction=(charge.end-charge.start).normalized;
        source.mammothCharge=charge;source.mammothChargePending=false;source.mammothChargeTarget=null;
        ClearRoute(source);source.hexStepping=false;
        source.velocity=Vector3.zero;SetMoving(source,false);
        PlayMammothSound(source,MammothSound.Prepare,charge.start,.85f);
        if(!mlEpisodeActive)
        {
            charge.effect=PixelMammothCharge.Play(Rank3Renderer(source),charge.direction);
            charge.effect.transform.SetParent(transform,true);
            if(charge.wallRush){
                charge.wallEffect=PixelMammothWallRush.Play(Rank3Renderer(source),source.definition.monsterId,charge.start,charge.direction,charge.radiusScale);
                if(charge.wallEffect!=null)charge.wallEffect.transform.SetParent(transform,true);
            }
        }
        if(source.spriteAnimation!=null)
        {
            if(!source.spriteAnimation.PlayMammothCharge(()=>MammothChargeValid(source,charge),
                time=>AdvanceMammothCharge(source,charge,time),()=>EndMammothCharge(source,charge),false,charge.wallRush))
                EndMammothCharge(source,charge);
        }
        else if(isActiveAndEnabled)StartCombatCoroutine(source,MammothChargeFallback(source,charge));
    }

    private IEnumerator MammothChargeFallback(Unit source,MammothChargeState charge)
    {
        while(MammothChargeValid(source,charge)&&charge.time<(charge.wallRush?MammothRank3Rules.WallRushEnd:MammothCombatRules.EndTime))
        {
            if(!paused&&!source.combatStatuses.Has(CombatStatusKind.Freeze))
                AdvanceMammothCharge(source,charge,charge.time+CombatSkillClock.Delta);
            yield return null;
        }
        EndMammothCharge(source,charge);
    }

    private void AdvanceMammothCharge(Unit source,MammothChargeState charge,float time)
    {
        if(!MammothChargeValid(source,charge)){EndMammothCharge(source,charge);return;}
        if(source.combatStatuses.Has(CombatStatusKind.Freeze))return;
        charge.time=time;
        if(charge.wallRush){AdvanceMammothWallRush(source,charge,time);return;}
        Vector3 previous=Flat(source.model.transform.position);
        charge.effect?.Pose(previous,time);
        if(time<MammothCombatRules.Windup)return;
        if(!charge.launched)
        {
            charge.launched=true;source.combatStatuses.DebuffImmune=true;
            source.currentAction="earth_charge_run";
            PlayMammothSound(source,MammothSound.Charge,previous);
        }
        if(charge.landed)return;
        var skill=GameMasterData.Skill(MammothCombatRules.SkillId);
        Vector3 next=Vector3.Lerp(charge.start,charge.end,MammothCombatRules.ChargeProgress(time-MammothCombatRules.Windup));
        // Sweep only the newly travelled segment. A long frame cannot skip a victim.
        foreach(Unit enemy in units.Where(u=>IsCombatTargetable(u)&&u.team!=source.team&&!charge.hit.Contains(u))
            .OrderBy(u=>Vector3.Dot(Flat(u.model.transform.position)-previous,charge.direction)).ToArray())
        {
            Vector3 point=Flat(enemy.model.transform.position);
            if(Vector3.Dot(point-charge.start,charge.direction)<0f||
               !CrocodileCombatRules.TryPreyCannonContact(previous,next,point,
                   TileSize*.48f*charge.radiusScale+enemy.collisionRadius,out float contact))continue;
            ApplyMammothChargeHit(source,enemy,charge,skill.damageMultiplier,true);
            if(!MammothChargeValid(source,charge))return;
        }
        PlaceTyrannoUnit(source,next);
        while(charge.step<3&&time>=MammothCombatRules.Windup+.08f+charge.step*.18f)
        {charge.step++;charge.effect?.Step(next,time,charge.step);}
        if(time+.0001f<MammothCombatRules.ContactTime)return;
        charge.landed=true;source.currentAction="earth_charge_brake";
        float radius=TileSize*skill.radiusTiles*charge.radiusScale;
        charge.effect?.Impact(next,time,radius);
        if(!mlEpisodeActive){combatCameraDirector?.ReportGroundSlam();meadowAmbientParticles?.Impact(next,radius,Rank3Renderer(source));}
        foreach(Unit enemy in units.ToArray())
            if(IsCombatTargetable(enemy)&&enemy.team!=source.team&&
               Vector3.Distance(next,Flat(enemy.model.transform.position))<=radius+enemy.collisionRadius)
            {
                ApplyMammothChargeHit(source,enemy,charge,skill.secondaryDamageMultiplier,false);
                if(!MammothChargeValid(source,charge))return;
            }
        ApplyMammothChargeTraits(source,charge.hit,charge.start,next);
        source.combatStatuses.DebuffImmune=charge.previousImmunity;
        PlayMammothSound(source,MammothSound.Collision,next);
        RecordSkillEvent(source,MammothCombatRules.SkillId,charge.hits,charge.damage);
        if((next-charge.start).sqrMagnitude>.000001f&&MammothChargeValid(source,charge))
            BeginSkillInertia(source,next-charge.start,MammothCombatRules.ChargeDuration);
    }

    private void ApplyMammothChargeHit(Unit source,Unit enemy,MammothChargeState charge,float ratio,bool push)
    {
        bool first=charge.hit.Add(enemy);
        bool steel=first&&HasRank3(source,MammothCombatRules.IrontuskTraitId);
        float dealt=ApplySkillDamage(source,enemy,MammothCombatRules.Damage(
            SkillPower(source),ratio,steel?CurrentDefense(source):0f,steel)*charge.scale,false);
        source.skillHits++;source.skillDamage+=dealt;charge.hits++;charge.damage+=dealt;
        if(!MammothChargeValid(source,charge))return;
        if(!charge.shook&&push)
        {
            charge.shook=true;
            PlayMammothSound(source,MammothSound.Collision,Rank3Center(enemy),.75f);
            if(!mlEpisodeActive)combatCameraDirector?.ReportMarchFootstep();
        }
        if(!mlEpisodeActive)
        {
            charge.effect?.Hit(Rank3Center(enemy),charge.time,steel);
            enemy.reaction?.PlayTintHit(new Color(1f,.93f,.72f));
            enemy.spriteAnimation?.PlayImpactRecoil(charge.direction,.1f);
        }
        if(!IsCombatActive(enemy))return;
        if(push)DisplaceUnit(enemy,charge.direction,TileSize*MammothCombatRules.KnockbackDistanceTiles);
        Rank3Knockdown(source,enemy,GameMasterData.Skill(MammothCombatRules.SkillId).statusDurationSeconds);
    }

    private void AdvanceMammothWallRush(Unit source,MammothChargeState charge,float time)
    {
        Vector3 previous=Flat(source.model.transform.position);
        charge.effect?.Pose(previous,time);charge.wallEffect?.Pose(time);
        while(charge.wallsRaised<3&&time>=.06f+charge.wallsRaised*.1f)
        {
            int wall=++charge.wallsRaised;
            PlayMammothSound(source,MammothSound.Wall,charge.start+charge.direction*TileSize*wall,.6f);
        }
        if(time<MammothRank3Rules.WallRushWindup||charge.landed)return;
        if(!charge.launched)
        {
            charge.launched=true;source.combatStatuses.DebuffImmune=true;
            PlayMammothSound(source,MammothSound.Charge,previous);
        }
        source.currentAction="wall_rush_run";
        Vector3 desired=charge.start+charge.direction*TileSize*MammothRank3Rules.WallRushTravel(time);
        // Check the travelled segment so a newly appeared obstacle cannot be crossed during the rush.
        Vector3 next=BlockRank3IceWallMovement(source,previous,ResolveFreePosition(desired,source.collisionRadius));
        bool blocked=!HasClearPathSegment(previous,next,source.collisionRadius)||(next-desired).sqrMagnitude>.0064f;
        if(blocked)next=previous;
        PlaceTyrannoUnit(source,next);
        float distance=Vector3.Dot(next-charge.start,charge.direction)/TileSize+MammothRank3Rules.WallRushNoseTiles;
        while(charge.walls<3&&distance+.0001f>=charge.walls+1)
        {
            int wall=charge.walls++;
            Vector3 center=charge.start+charge.direction*TileSize*(wall+1);
            float radius=TileSize*MammothRank3Rules.WallRushBlastTiles*charge.radiusScale;
            charge.wallEffect?.Break(wall,time);
            source.currentAction="wall_rush_break_"+(wall+1);
            foreach(Unit enemy in units.ToArray())
                if(IsCombatTargetable(enemy)&&enemy.team!=source.team&&
                    Vector3.Distance(center,Flat(enemy.model.transform.position))<=radius+enemy.collisionRadius)
                {
                    bool first=charge.hit.Add(enemy);
                    bool steel=first&&HasRank3(source,MammothCombatRules.IrontuskTraitId);
                    float dealt=ApplySkillDamage(source,enemy,MammothCombatRules.Damage(SkillPower(source),
                        MammothRank3Rules.WallRushRatio,steel?CurrentDefense(source):0f,steel)*charge.scale,false);
                    source.skillHits++;source.skillDamage+=dealt;charge.hits++;charge.damage+=dealt;
                    if(!MammothChargeValid(source,charge))return;
                    if(!mlEpisodeActive){charge.wallEffect?.Hit(Rank3Center(enemy),time);
                        enemy.reaction?.PlayTintHit(PixelMammothWallRush.HitColor(source.definition.monsterId));
                        enemy.spriteAnimation?.PlayImpactRecoil(charge.direction,.12f);}
                }
            if(!mlEpisodeActive){combatCameraDirector?.ReportGroundSlam(wall==2);
                meadowAmbientParticles?.Impact(center,radius,Rank3Renderer(source));}
            PlayMammothSound(source,MammothSound.WallBreak,center,wall==2?1.1f:.85f);
        }
        while(charge.step<3&&time>=MammothRank3Rules.WallRushWindup+.06f+charge.step*.23f)
        {charge.step++;charge.effect?.Step(next,time,charge.step);}
        if(!blocked&&time+.0001f<MammothRank3Rules.WallRushArrival)return;
        charge.landed=true;charge.blocked=blocked;source.currentAction="wall_rush_brake";
        ApplyMammothChargeTraits(source,charge.hit,charge.start,next);
        source.combatStatuses.DebuffImmune=charge.previousImmunity;
        RecordSkillEvent(source,MammothRank3Rules.WallRush,charge.hits,charge.damage);
        if(!blocked&&(next-charge.start).sqrMagnitude>.000001f&&MammothChargeValid(source,charge))
            BeginSkillInertia(source,next-charge.start,MammothRank3Rules.WallRushArrival-MammothRank3Rules.WallRushWindup);
        if(blocked)source.spriteAnimation?.InterruptCurrentAction(true);
    }

    private void EndMammothCharge(Unit source,MammothChargeState charge)
    {
        bool completed=MammothChargeValid(source,charge)&&charge.landed&&!charge.blocked&&
            charge.time+.0001f>=(charge.wallRush?MammothRank3Rules.WallRushEnd:MammothCombatRules.EndTime)&&
            !source.combatStatuses.Has(CombatStatusKind.Freeze)&&
            !source.combatStatuses.Has(CombatStatusKind.Root)&&
            !source.combatStatuses.Has(CombatStatusKind.Airborne)&&
            !source.combatStatuses.Has(CombatStatusKind.Displaced);
        charge.effect?.Cancel();charge.wallEffect?.Cancel();
        if(source.mammothCharge!=charge)return;
        source.mammothCharge=null;source.mammothChargePending=false;source.mammothChargeTarget=null;
        source.mammothChargeDamageMultiplier=source.mammothChargeRadiusMultiplier=1f;
        source.combatStatuses.DebuffImmune=charge.previousImmunity;
        source.attackMovementLockTimer=0f;source.decisionTimer=0f;
        if(IsCombatActive(source))source.currentAction="earth_charge_complete";
        if(!completed)ClearUnitInertia(source);
    }
}
