using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MammothTossState
    {
        public Unit source;
        public Vector3 origin,direction,ground;
        public float time,flight,scale,radiusScale,power,steel,damage,shatteredAt=-1f;
        public int generation,revision,hits;
        public bool lifted,launched,bounced,finished;
        public System.Action cameraEffectCompleted;
        public void CompleteCameraEffect(){var completed=cameraEffectCompleted;cameraEffectCompleted=null;completed?.Invoke();}
        public PixelMammothBoulder effect;
        public readonly HashSet<Unit> hit=new HashSet<Unit>();
    }
    private readonly List<MammothTossState> mammothBoulders=new List<MammothTossState>();

    private bool MammothTossValid(Unit source,MammothTossState toss)=>!battleEnding&&source.mammothToss==toss&&
        !toss.finished&&toss.generation==combatGeneration&&IsCombatActive(source)&&source.positionRevision==toss.revision&&
        !SpiderSilkBound(source)&&!source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !source.combatStatuses.Has(CombatStatusKind.Fear)&&source.giraffeStunTimer<=0f;

    private void StartMammothToss(Unit source,int generation)
    {
        if(source.mammothToss!=null)return;
        Vector3 origin=Flat(source.model.transform.position),direction=source.mammothChargeEnd-origin;
        if(direction.sqrMagnitude<.001f)direction=source.facingDirection;
        var toss=new MammothTossState {source=source,origin=origin,direction=direction.normalized,
            generation=generation,revision=source.positionRevision,scale=source.mammothChargeDamageMultiplier,
            radiusScale=source.mammothChargeRadiusMultiplier,cameraEffectCompleted=HoldSkillCameraEffect(source)};
        toss.ground=origin;
        source.mammothToss=toss;source.mammothChargePending=false;source.mammothChargeTarget=null;
        ClearRoute(source);source.hexStepping=false;source.velocity=Vector3.zero;
        SetMoving(source,false);mammothBoulders.Add(toss);
        if(!mlEpisodeActive)
        {
            toss.effect=PixelMammothBoulder.Play(Rank3Renderer(source),origin,toss.direction,toss.radiusScale);
            toss.effect.transform.SetParent(transform,true);
        }
        if(source.spriteAnimation!=null)
        {
            if(!source.spriteAnimation.PlayMammothCharge(()=>MammothTossValid(source,toss),
                time=>AdvanceMammothToss(source,toss,time),()=>EndMammothToss(source,toss),true))
                EndMammothToss(source,toss);
        }
        else if(isActiveAndEnabled)StartCombatCoroutine(source,MammothTossFallback(source,toss));
    }

    private IEnumerator MammothTossFallback(Unit source,MammothTossState toss)
    {
        while(MammothTossValid(source,toss)&&toss.time<MammothRank3Rules.TossCastEnd)
        {
            if(!paused&&!source.combatStatuses.Has(CombatStatusKind.Freeze))
                AdvanceMammothToss(source,toss,toss.time+CombatSkillClock.Delta);
            yield return null;
        }
        EndMammothToss(source,toss);
    }

    private void AdvanceMammothToss(Unit source,MammothTossState toss,float time)
    {
        if(!MammothTossValid(source,toss)){EndMammothToss(source,toss);return;}
        toss.time=time;if(toss.launched)return;
        toss.effect?.Prepare(time);
        if(!toss.lifted&&time>=.17f)
        {
            toss.lifted=true;
            PlayMammothSound(source,MammothSound.Lift,toss.origin,.8f);
        }
        if(time<MammothRank3Rules.TossRelease)return;
        toss.launched=true;toss.power=SkillPower(source);
        toss.steel=HasRank3(source,MammothCombatRules.IrontuskTraitId)?CurrentDefense(source)*MammothCombatRules.SteelTuskDefenseRatio:0f;
        source.currentAction="mountain_toss_release";
        PlayMammothSound(source,MammothSound.Throw,toss.origin);
        toss.ground=toss.origin;
        AdvanceMammothBoulder(toss,0f);
        if(isActiveAndEnabled)StartCombatCoroutine(source,FlyMammothBoulder(toss));
    }

    private IEnumerator FlyMammothBoulder(MammothTossState toss)
    {
        // Released projectiles finish their flight even if the caster dies or is interrupted.
        try
        {
            while(!battleEnding&&toss.generation==combatGeneration&&!toss.finished)
            {
                yield return null;
                if(!paused)AdvanceMammothBoulder(toss,toss.flight+CombatSkillClock.Delta);
            }
        }
        finally
        {
            toss.CompleteCameraEffect();
            toss.effect?.Cancel();mammothBoulders.Remove(toss);
        }
    }

    private void AdvanceMammothBoulder(MammothTossState toss,float flight)
    {
        if(toss.finished)return;
        if(battleEnding||toss.generation!=combatGeneration)
        {toss.finished=true;toss.CompleteCameraEffect();toss.effect?.Cancel();return;}
        toss.flight=flight;
        if(toss.shatteredAt>=0f)
        {
            toss.effect?.Fly(toss.ground,flight);
            if(flight-toss.shatteredAt>=.28f){toss.finished=true;toss.effect?.Cancel();}
            return;
        }
        float travel=Mathf.Min(TileSize*MammothRank3Rules.TossRangeTiles,
            TileSize*.55f+flight*TileSize*MammothRank3Rules.TossSpeedTiles);
        Vector3 intended=toss.origin+toss.direction*travel;
        bool blocked=false;
        Unit[] candidates=units.ToArray();
        while(Vector3.Distance(toss.ground,intended)>.001f)
        {
            Vector3 next=Vector3.MoveTowards(toss.ground,intended,TileSize*.12f);
            blocked=(ResolveFreePosition(next,TileSize*.12f)-next).sqrMagnitude>.001f||
                !HasClearPathSegment(toss.ground,next,TileSize*.12f);
            foreach(var wall in rank3IceWalls)
                if(wall.team!=toss.source.team&&Rank3SegmentCrossesWall(toss.ground,next,wall)){blocked=true;break;}
            if(blocked)break;
            foreach(Unit enemy in candidates)
                if(IsCombatTargetable(enemy)&&enemy.team!=toss.source.team&&!toss.hit.Contains(enemy)&&
                    CrocodileCombatRules.TryPreyCannonContact(toss.ground,next,Flat(enemy.model.transform.position),
                        TileSize*MammothRank3Rules.TossBodyRadiusTiles*toss.radiusScale+enemy.collisionRadius,out float contact))
                {
                    HitMammothBoulder(toss,enemy,MammothRank3Rules.TossHitRatio);
                    if(toss.finished||toss.generation!=combatGeneration)return;
                }
            toss.ground=next;
        }
        toss.effect?.Fly(toss.ground,flight);
        if(!toss.bounced&&flight>=MammothRank3Rules.TossFirstBounce)
        {
            toss.bounced=true;toss.effect?.Bounce(flight);
            PlayMammothSound(toss.source,MammothSound.Collision,toss.ground,.65f);
            if(!mlEpisodeActive)combatCameraDirector?.ReportGroundSlam();
        }
        if(blocked||travel>=TileSize*MammothRank3Rules.TossRangeTiles-.001f)
        {
            toss.shatteredAt=flight;
            float radius=TileSize*MammothRank3Rules.TossBlastTiles*toss.radiusScale;
            toss.effect?.Shatter(flight,radius);
            foreach(Unit enemy in units.ToArray())
                if(IsCombatTargetable(enemy)&&enemy.team!=toss.source.team&&
                    Vector3.Distance(Flat(enemy.model.transform.position),toss.ground)<=radius+enemy.collisionRadius)
                {
                    HitMammothBoulder(toss,enemy,MammothRank3Rules.TossBlastRatio);
                    if(toss.finished||toss.generation!=combatGeneration)return;
                }
            ApplyMammothChargeTraits(toss.source,toss.hit,toss.origin,toss.ground);
            if(!mlEpisodeActive){combatCameraDirector?.ReportGroundSlam(true);meadowAmbientParticles?.Impact(toss.ground,radius,Rank3Renderer(toss.source));}
            PlayMammothSound(toss.source,MammothSound.Shatter,toss.ground,1.1f);
            RecordSkillEvent(toss.source,MammothRank3Rules.MountainToss,toss.hits,toss.damage);
            toss.CompleteCameraEffect();
        }
    }

    private void HitMammothBoulder(MammothTossState toss,Unit enemy,float ratio)
    {
        bool first=toss.hit.Add(enemy);
        float dealt=ApplySkillDamage(toss.source,enemy,(toss.power*ratio+(first?toss.steel:0f))*toss.scale,false);
        toss.source.skillHits++;toss.source.skillDamage+=dealt;toss.hits++;toss.damage+=dealt;
        if(first&&toss.hit.Count==1&&ratio==MammothRank3Rules.TossHitRatio)
            PlayMammothSound(toss.source,MammothSound.Collision,Rank3Center(enemy),.6f);
        if(!mlEpisodeActive)
        {
            toss.effect?.Hit(Rank3Center(enemy),toss.flight,toss.steel>0f);
            enemy.reaction?.PlayTintHit(new Color(1f,.89f,.67f));
            enemy.spriteAnimation?.PlayImpactRecoil(toss.direction,.12f);
        }
    }

    private void EndMammothToss(Unit source,MammothTossState toss)
    {
        if(!toss.launched)
        {toss.finished=true;toss.CompleteCameraEffect();toss.effect?.Cancel();mammothBoulders.Remove(toss);}
        if(source.mammothToss!=toss)return;
        source.mammothToss=null;source.mammothChargePending=false;source.mammothChargeTarget=null;
        source.mammothChargeDamageMultiplier=source.mammothChargeRadiusMultiplier=1f;
        source.attackMovementLockTimer=0f;source.decisionTimer=0f;
        if(IsCombatActive(source))source.currentAction="mountain_toss_complete";
    }

    private void ClearMammothBoulders()
    {
        foreach(var toss in mammothBoulders){toss.finished=true;toss.CompleteCameraEffect();toss.effect?.Cancel();}
        mammothBoulders.Clear();
        ClearMammothExpulsionFlights();
    }
}
