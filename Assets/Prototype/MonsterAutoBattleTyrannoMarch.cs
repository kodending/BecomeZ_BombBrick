using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class TyrannoMarchState
    {
        public Vector3 origin,direction;
        public float time,damageScale,radiusScale,travel;
        public int generation,revision,step;
        public bool departed,blocked,impacted;
        public ParticleSystem dust;
        public readonly HashSet<Unit> hit=new HashSet<Unit>();
        public readonly HashSet<Unit> pushed=new HashSet<Unit>();
    }

    private static bool TyrannoMarchProtected(Unit unit)=>unit.tyrannoMarch!=null&&
        unit.tyrannoMarch.time<TyrannoRank3Rules.MarchWindup+TyrannoRank3Rules.MarchDuration;

    private bool TyrannoMarchValid(Unit source,TyrannoMarchState march)=>
        !battleEnding&&source.tyrannoMarch==march&&march.generation==combatGeneration&&
        IsCombatActive(source)&&source.positionRevision==march.revision&&
        !SpiderSilkBound(source)&&!source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !source.combatStatuses.Has(CombatStatusKind.Fear)&&source.giraffeStunTimer<=0f;

    private void StartTyrannoMarch(Unit source,int generation)
    {
        if(source.tyrannoMarch!=null)return;
        Vector3 origin=Flat(source.model.transform.position);
        Unit target=IsCombatTargetable(source.target)?source.target:
            FindNearestEnemyToPoint(source,null,origin,TileSize*8f);
        Vector3 direction=target==null?source.facingDirection:Flat(target.model.transform.position)-origin;
        if(direction.sqrMagnitude<.001f)direction=Vector3.forward;
        var march=new TyrannoMarchState{origin=origin,direction=direction.normalized,
            generation=generation,revision=source.positionRevision,
            damageScale=source.tyrannoTremorDamageMultiplier,radiusScale=source.tyrannoTremorRadiusMultiplier};
        source.tyrannoMarch=march;
        ClearRoute(source);source.hexStepping=false;
        source.velocity=Vector3.zero;SetMoving(source,false);
        source.facingDirection=march.direction;
        source.spriteBillboard?.SetFacingDirection(march.direction);
        if(!mlEpisodeActive)
        {
            march.dust=PixelCombatEffect.PlayTyrannoTremorImpact(origin,.2f,Rank3Renderer(source),false);
            march.dust.name="Tyrant March Foot Dust";
            march.dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission=march.dust.emission;emission.enabled=false;
            var shape=march.dust.shape;shape.enabled=false;
            var main=march.dust.main;main.maxParticles=96;main.loop=true;
            var velocity=march.dust.velocityOverLifetime;velocity.enabled=false;
            march.dust.Play();
            EmitTyrannoMarchDust(source,march,10,.6f);
        }
        if(source.spriteAnimation!=null)
            source.spriteAnimation.PlayTyrannoMarch(()=>TyrannoMarchValid(source,march),
                time=>AdvanceTyrannoMarch(source,march,time),()=>EndTyrannoMarch(source,march));
        else StartCombatCoroutine(source,AdvanceTyrannoMarchFallback(source,march));
    }

    private IEnumerator AdvanceTyrannoMarchFallback(Unit source,TyrannoMarchState march)
    {
        while(TyrannoMarchValid(source,march)&&march.time<TyrannoRank3Rules.MarchEnd)
        {
            if(!source.combatStatuses.Has(CombatStatusKind.Freeze))
                AdvanceTyrannoMarch(source,march,march.time+CombatSkillClock.Delta*
                    RaptorCombatRules.IceSpeedMultiplier(source.raptorSlowTimer>0f)*
                    (source.rank3ActionSlowTimer>0f?1f-source.rank3ActionSlowRatio:1f));
            yield return null;
        }
        EndTyrannoMarch(source,march);
    }

    private void AdvanceTyrannoMarch(Unit source,TyrannoMarchState march,float time)
    {
        if(!TyrannoMarchValid(source,march))return;
        march.time=time;
        source.attackMovementLockTimer=.1f;
        source.tyrannoTremorPending=time<TyrannoRank3Rules.MarchWindup;
        source.currentAction=time<TyrannoRank3Rules.MarchWindup?"tyrant_march_prepare":"tyrant_march";
        if(!march.departed&&time>=TyrannoRank3Rules.MarchWindup)
        {
            march.departed=true;
            if(IsCombatTargetable(source.target))
            {
                Vector3 aim=Flat(source.target.model.transform.position)-march.origin;
                if(aim.sqrMagnitude>.001f)march.direction=aim.normalized;
            }
            source.facingDirection=march.direction;
            source.spriteBillboard?.SetFacingDirection(march.direction,true);
        }
        // Each stride decelerates into contact, then holds its ground while the body takes the weight.
        float travel=TyrannoRank3Rules.MarchTravelProgress(time-TyrannoRank3Rules.MarchWindup)*
            TileSize*TyrannoRank3Rules.MarchDistanceTiles;
        float remaining=travel-march.travel;
        while(remaining>.0001f&&!march.blocked&&TyrannoMarchValid(source,march))
        {
            float step=Mathf.Min(remaining,TileSize*.12f);
            Vector3 previous=Flat(source.model.transform.position),next=previous+march.direction*step;
            if((ResolveFreePosition(next,source.collisionRadius)-next).sqrMagnitude>.0001f)
            {march.blocked=true;break;}
            // Move already caught bodies with the same displacement; a wall stops the procession.
            foreach(Unit enemy in march.pushed)
            {
                if(!IsCombatActive(enemy)||enemy.tyrannoPushedBy!=source)continue;
                Vector3 point=Flat(enemy.model.transform.position)+march.direction*step;
                if((ResolveFreePosition(point,enemy.collisionRadius)-point).sqrMagnitude>.0001f)
                {march.blocked=true;break;}
            }
            if(march.blocked)break;
            for(int index=0;index<units.Count;index++)
            {
                Unit enemy=units[index];
                if(!IsCombatTargetable(enemy)||enemy.team==source.team||march.hit.Contains(enemy))continue;
                Vector3 point=Flat(enemy.model.transform.position);
                if(Vector3.Dot(point-previous,march.direction)<-.001f)continue;
                if(!CrocodileCombatRules.TryPreyCannonContact(previous,next,point,
                    TileSize*TyrannoRank3Rules.MarchWidthTiles*.5f*march.radiusScale,out float contact))continue;
                march.hit.Add(enemy);
                Rank3Hit(source,enemy,TyrannoRank3Rules.MarchCollisionRatio*march.damageScale);
                if(!TyrannoMarchValid(source,march))return;
                if(!mlEpisodeActive)
                {
                    PixelCombatEffect.PlayTyrannoTremorHit(enemy.model.transform,Rank3Renderer(enemy));
                    PixelCombatEffect.PlayWolfHuntImpact(enemy.model.transform,Rank3Renderer(enemy),
                        march.direction,new Color(1f,.9f,.7f),false,.9f);
                }
                if(!IsCombatActive(enemy))continue;
                if(PreyDisplacementImmune(enemy)){next=Vector3.Lerp(previous,next,contact);march.blocked=true;break;}
                InterruptPositionalAction(enemy);
                enemy.spriteAnimation?.InterruptCurrentAction(true);
                enemy.displacing=false;enemy.preyDraggedBy=null;
                ClearRoute(enemy);enemy.hexStepping=false;
                enemy.velocity=Vector3.zero;SetMoving(enemy,false);
                enemy.tyrannoPushedBy=source;march.pushed.Add(enemy);
            }
            Vector3 delta=next-previous;
            foreach(Unit enemy in march.pushed)
            {
                if(!IsCombatActive(enemy)||enemy.tyrannoPushedBy!=source)continue;
                if(PreyDisplacementImmune(enemy)){enemy.tyrannoPushedBy=null;continue;}
                PlaceTyrannoUnit(enemy,ResolveFreePosition(Flat(enemy.model.transform.position)+delta,enemy.collisionRadius));
            }
            PlaceTyrannoUnit(source,next);
            march.travel+=delta.magnitude;remaining=travel-march.travel;
        }
        while(march.step<2&&time>=TyrannoRank3Rules.MarchWindup+TyrannoRank3Rules.MarchFootContact+
            march.step*TyrannoRank3Rules.MarchStrideDuration)
        {
            march.step++;
            PlayTyrannoSound(source,TyrannoSound.Step,Flat(source.model.transform.position));
            if(!mlEpisodeActive)
            {
                Vector3 side=Vector3.Cross(Vector3.up,march.direction);
                Vector3 foot=Flat(source.model.transform.position)+march.direction*TileSize*.2f+
                    side*TileSize*(march.step==1?-.13f:.13f);
                PixelTyrannoStomp.MarchFootstep(foot,TileSize*.95f,Rank3Renderer(source));
                EmitTyrannoMarchDust(source,march,28,1.35f);
                combatCameraDirector?.ReportMarchFootstep();
            }
        }
        if(!march.impacted&&time>=TyrannoRank3Rules.MarchContact)
        {
            march.impacted=true;ReleaseTyrannoMarchVictims(source,march);
            Vector3 center=Flat(source.model.transform.position);
            PlayTyrannoSound(source,TyrannoSound.MarchFinish,center);
            float radius=TileSize*TyrannoRank3Rules.MarchFinalRadiusTiles*march.radiusScale;
            foreach(Unit enemy in Rank3Enemies(source,center,radius))
            {
                if(!IsCombatActive(source))return;
                march.hit.Add(enemy);
                Rank3Hit(source,enemy,TyrannoRank3Rules.MarchFinalRatio*march.damageScale);
                Rank3Knockdown(source,enemy,TyrannoRank3Rules.MarchKnockdown);
                if(!mlEpisodeActive)PixelCombatEffect.PlayTyrannoTremorHit(enemy.model.transform,Rank3Renderer(enemy));
            }
            if(!IsCombatActive(source))return;
            if(!mlEpisodeActive)
            {
                PixelTyrannoStomp.MarchFinisher(center,radius,Rank3Renderer(source));
                meadowAmbientParticles?.Impact(center,radius,Rank3Renderer(source));
                combatCameraDirector?.ReportGroundSlam(true);
            }
            if(march.hit.Count>0)ApplyTyrannoRank3Intrinsic(source,march.hit,center,radius,march.hit.Count);
            source.traitActivations++;
            RecordSkillEvent(source,TyrannoRank3Rules.TyrantMarch,march.hit.Count,source.skillDamage);
        }
    }

    private void PlaceTyrannoUnit(Unit unit,Vector3 ground)
    {
        unit.model.transform.position=ground+Vector3.up*unit.visualHeightOffset;
        if(unit.ring!=null)unit.ring.transform.position=ground+Vector3.up*.025f;
        unit.lastGroundPosition=ground;unit.velocity=Vector3.zero;
        OccupyUnit(unit,NearestGridCoordinate(ground));
    }

    private void ReleaseTyrannoMarchVictims(Unit source,TyrannoMarchState march)
    {
        foreach(Unit enemy in march.pushed)
            if(enemy.tyrannoPushedBy==source){enemy.tyrannoPushedBy=null;enemy.decisionTimer=0f;}
        march.pushed.Clear();
    }

    private void EndTyrannoMarch(Unit source,TyrannoMarchState march)
    {
        ReleaseTyrannoMarchVictims(source,march);
        if(march.dust!=null&&!CombatEffectEndFade.IsFading(march.dust))Destroy(march.dust.gameObject);
        if(source.tyrannoMarch!=march)return;
        source.tyrannoMarch=null;source.tyrannoTremorPending=false;
        source.tyrannoTremorDamageMultiplier=1f;source.tyrannoTremorRadiusMultiplier=1f;
        source.attackMovementLockTimer=0f;source.decisionTimer=0f;
        if(IsCombatActive(source))source.currentAction="rank3_tyranno_complete";
    }

    private void EmitTyrannoMarchDust(Unit source,TyrannoMarchState march,int count,float strength)
    {
        if(march.dust==null)return;
        Vector3 side=Vector3.Cross(Vector3.up,march.direction);
        Vector3 ground=Flat(source.model.transform.position)+march.direction*TileSize*.12f;
        for(int i=0;i<count;i++)
            march.dust.Emit(new ParticleSystem.EmitParams {
                position=ground+side*((i%7-3)*.13f)+Vector3.up*.05f,
                velocity=(-march.direction*(.7f+(i%4)*.3f)+side*((i%5-2)*.65f))*strength+
                    Vector3.up*(.5f+(i%3)*.2f),
                startLifetime=.24f+(i%3)*.055f,startSize=.11f+(i%3)*.035f,
                startColor=i%3==0?new Color(.91f,.8f,.62f):new Color(.46f,.34f,.23f),rotation=i*47f },1);
    }
}
