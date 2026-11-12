using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class TyrannoMawState
    {
        public Unit source;
        public Vector3 center;
        public float time,damageScale,radiusScale;
        public int generation,revision;
        public bool opened,closed,detached,finished;
        public PixelTyrannoStomp charge;
        public PixelTyrannoMaw jaws;
        public readonly List<Unit> openingTargets=new List<Unit>();
        public readonly HashSet<Unit> hit=new HashSet<Unit>();
        public readonly Dictionary<Unit,Vector3> starts=new Dictionary<Unit,Vector3>();
    }

    private readonly List<TyrannoMawState> releasedTyrannoMaws=new List<TyrannoMawState>();
    private TyrannoMawState ActiveTyrannoMawFor(Unit source)=>source.tyrannoMaw??releasedTyrannoMaws
        .FirstOrDefault(m=>m.source==source&&!m.finished&&!m.closed&&ReleasedSkillValid(source,m.generation));

    private static bool TyrannoMawProtected(Unit unit)=>unit.tyrannoMaw!=null&&
        unit.tyrannoMaw.time<TyrannoRank3Rules.MawWindup;

    private bool TyrannoMawValid(Unit source,TyrannoMawState maw)=>!maw.finished&&ReleasedSkillValid(source,maw.generation)&&
        (maw.opened||source.tyrannoMaw==maw&&IsCombatActive(source)&&source.positionRevision==maw.revision&&
        !SpiderSilkBound(source)&&!source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !source.combatStatuses.Has(CombatStatusKind.Fear)&&source.giraffeStunTimer<=0f);

    private void StartTyrannoMaw(Unit source,int generation)
    {
        if(source.tyrannoMaw!=null)return;
        var maw=new TyrannoMawState {source=source,center=Flat(source.model.transform.position),generation=generation,
            revision=source.positionRevision,damageScale=source.tyrannoTremorDamageMultiplier,
            radiusScale=source.tyrannoTremorRadiusMultiplier};
        source.tyrannoMaw=maw;
        ClearRoute(source);source.hexStepping=false;
        source.velocity=Vector3.zero;SetMoving(source,false);
        if(!mlEpisodeActive)maw.charge=PixelTyrannoStomp.Charge(maw.center,
            TileSize*TyrannoRank3Rules.MawRadiusTiles*maw.radiusScale,Rank3Renderer(source),
            ()=>TyrannoMawValid(source,maw)&&!maw.opened);
        if(source.spriteAnimation!=null)
            source.spriteAnimation.PlayTyrannoMaw(()=>TyrannoMawValid(source,maw),
                time=>{if(!maw.detached)AdvanceTyrannoMaw(source,maw,time);},()=>EndTyrannoMaw(source,maw));
        else StartCombatCoroutine(source,AdvanceTyrannoMawFallback(source,maw));
    }

    private IEnumerator AdvanceTyrannoMawFallback(Unit source,TyrannoMawState maw)
    {
        while(!maw.detached&&TyrannoMawValid(source,maw)&&maw.time<TyrannoRank3Rules.MawEnd)
        {
            if(!source.combatStatuses.Has(CombatStatusKind.Freeze))
                AdvanceTyrannoMaw(source,maw,maw.time+CombatSkillClock.Delta*
                    RaptorCombatRules.IceSpeedMultiplier(source.raptorSlowTimer>0f)*
                    (source.rank3ActionSlowTimer>0f?1f-source.rank3ActionSlowRatio:1f));
            yield return null;
        }
        EndTyrannoMaw(source,maw);
    }

    private void AdvanceTyrannoMaw(Unit source,TyrannoMawState maw,float time)
    {
        if(!TyrannoMawValid(source,maw))return;
        maw.time=Mathf.Max(maw.time,time);time=maw.time;
        if(source.tyrannoMaw==maw)
        {source.attackMovementLockTimer=.1f;source.tyrannoTremorPending=time<TyrannoRank3Rules.MawWindup;
         source.currentAction=maw.opened?"primordial_maw":"primordial_maw_prepare";}
        float radius=TileSize*TyrannoRank3Rules.MawRadiusTiles*maw.radiusScale;
        float closeRadius=TileSize*TyrannoRank3Rules.MawCloseRadiusTiles*maw.radiusScale;
        if(!mlEpisodeActive&&maw.jaws==null&&time>=TyrannoRank3Rules.MawWindup-TyrannoRank3Rules.MawVisualLead)
        {
            maw.jaws=PixelTyrannoMaw.Open(maw.center,closeRadius,Rank3Renderer(source),source.facingDirection);
            if(maw.jaws!=null)PlayTyrannoSound(source,TyrannoSound.MawAppear,maw.center);
        }
        if(!maw.opened&&time>=TyrannoRank3Rules.MawWindup)
        {
            maw.opened=true;maw.charge?.Cancel();maw.charge=null;
            if(!maw.detached&&isActiveAndEnabled)
            {maw.detached=true;releasedTyrannoMaws.Add(maw);StartReleasedSkill(source,ContinueTyrannoMaw(source,maw));}
            foreach(Unit enemy in Rank3Enemies(source,maw.center,radius))
            {
                // Capture the original outer area, but do not hit while the mouth is still opening.
                maw.openingTargets.Add(enemy);
                if(!IsCombatActive(enemy)||PreyDisplacementImmune(enemy))continue;
                InterruptPositionalAction(enemy);enemy.displacing=false;enemy.preyDraggedBy=null;
                enemy.spriteAnimation?.InterruptCurrentAction(true);
                ClearRoute(enemy);enemy.hexStepping=false;
                enemy.velocity=Vector3.zero;SetMoving(enemy,false);
                enemy.tyrannoMawPulledBy=source;maw.starts.Add(enemy,Flat(enemy.model.transform.position));
            }
        }
        float elapsed=Mathf.Max(0f,time-TyrannoRank3Rules.MawWindup);
        if(maw.opened&&!maw.closed)
        {
            float progress=Mathf.SmoothStep(0f,1f,elapsed/TyrannoRank3Rules.MawPullDuration);
            foreach(var pair in maw.starts)
            {
                Unit enemy=pair.Key;
                if(!IsCombatActive(enemy)||enemy.tyrannoMawPulledBy!=source)continue;
                if(PreyDisplacementImmune(enemy)){enemy.tyrannoMawPulledBy=null;continue;}
                Vector3 delta=maw.center-pair.Value;
                float meleeStandOff=AttackSlotDistanceForRing(
                    AttackDistanceForRange(1,source.collisionRadius,enemy.collisionRadius),
                    source.collisionRadius,enemy.collisionRadius,0);
                float distance=Mathf.Min(TileSize*TyrannoRank3Rules.MawPullTiles,
                    Mathf.Max(0f,delta.magnitude-meleeStandOff));
                if(distance<=0f)continue;
                Vector3 point=pair.Value+delta.normalized*distance*progress;
                // A wall correction must not add distance or move the victim inside melee spacing.
                if((ResolveFreePosition(point,enemy.collisionRadius)-point).sqrMagnitude<.000001f)
                    PlaceTyrannoUnit(enemy,point);
            }
        }
        maw.jaws?.Advance(time-TyrannoRank3Rules.MawWindup);
        if(!maw.closed&&time>=TyrannoRank3Rules.MawCloseTime)
        {
            maw.closed=true;ReleaseTyrannoMawVictims(source,maw);
            PlayTyrannoSound(source,TyrannoSound.Bite,maw.center);
            // Both damage layers belong to the drawn chomp, including the outer-area hit.
            foreach(Unit enemy in maw.openingTargets)
            {
                if(!IsCombatActive(enemy))continue;
                maw.hit.Add(enemy);Rank3Hit(source,enemy,TyrannoRank3Rules.MawOpenRatio*maw.damageScale);
                if(!TyrannoMawValid(source,maw))return;
                if(!mlEpisodeActive)PixelCombatEffect.PlayTyrannoTremorHit(enemy.model.transform,Rank3Renderer(enemy));
            }
            // Recheck the actual bite area: an immune enemy outside it only takes the outer hit.
            foreach(Unit enemy in Rank3Enemies(source,maw.center,closeRadius))
            {
                maw.hit.Add(enemy);
                float bonus=TyrannoRank3Rules.MawBonusDamage(SkillPower(source),enemy.maxHp-enemy.hp,maw.damageScale);
                Rank3Hit(source,enemy,TyrannoRank3Rules.MawCloseRatio*maw.damageScale,TyrannoRank3Rules.MawDefenseIgnore,bonus);
                if(!TyrannoMawValid(source,maw))return;
                Rank3Knockdown(source,enemy,TyrannoRank3Rules.MawKnockdown);
                if(!mlEpisodeActive)
                    PixelCombatEffect.PlayWolfHuntImpact(enemy.model.transform,Rank3Renderer(enemy),
                        Vector3.down,new Color(1f,.88f,.63f),false,1.2f);
            }
            if(!mlEpisodeActive)
            {
                combatCameraDirector?.ReportGroundSlam(true);
                meadowAmbientParticles?.Impact(maw.center,closeRadius,Rank3Renderer(source));
            }
            if(maw.hit.Count>0)ApplyTyrannoRank3Intrinsic(source,maw.hit,maw.center,closeRadius,maw.hit.Count);
            source.traitActivations++;
            RecordSkillEvent(source,TyrannoRank3Rules.PrimordialMaw,maw.hit.Count,source.skillDamage);
        }
    }

    private void ReleaseTyrannoMawVictims(Unit source,TyrannoMawState maw)
    {
        foreach(Unit enemy in maw.starts.Keys)
            if(enemy.tyrannoMawPulledBy==source){enemy.tyrannoMawPulledBy=null;enemy.decisionTimer=0f;}
        maw.starts.Clear();
    }

    private void EndTyrannoMaw(Unit source,TyrannoMawState maw)
    {
        if(maw.finished)return;
        if(maw.opened&&ReleasedSkillValid(source,maw.generation)&&maw.time<TyrannoRank3Rules.MawEnd)
        {if(source.tyrannoMaw==maw){source.tyrannoMaw=null;source.tyrannoTremorPending=false;source.attackMovementLockTimer=0f;}return;}
        maw.finished=true;releasedTyrannoMaws.Remove(maw);
        ReleaseTyrannoMawVictims(source,maw);maw.charge?.Cancel();maw.jaws?.Cancel();
        if(source.tyrannoMaw==maw)
        {source.tyrannoMaw=null;source.tyrannoTremorPending=false;
         source.tyrannoTremorDamageMultiplier=1f;source.tyrannoTremorRadiusMultiplier=1f;source.attackMovementLockTimer=0f;source.decisionTimer=0f;}
        if(IsCombatActive(source))source.currentAction="rank3_tyranno_complete";
    }

    private IEnumerator ContinueTyrannoMaw(Unit source,TyrannoMawState maw)
    {
        try
        {
            while(TyrannoMawValid(source,maw)&&maw.time<TyrannoRank3Rules.MawEnd)
            {yield return null;if(!paused)AdvanceTyrannoMaw(source,maw,maw.time+CombatSkillClock.Delta);}
        }
        finally{EndTyrannoMaw(source,maw);}
    }
}
