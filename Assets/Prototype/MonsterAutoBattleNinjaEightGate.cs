using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class NinjaEightGateCast
    {
        public Unit source;
        public Vector3 center;
        public int generation,cast,revision,hitsBefore;
        public float scale,damageBefore,time;
        public bool detonated,finished,released;
        public readonly Dictionary<Unit,int> hits=new Dictionary<Unit,int>();
        public readonly HashSet<Unit>[] bladeVictims=new HashSet<Unit>[NinjaCatRank3Rules.KunaiCount];
        public readonly bool[] deflected=new bool[NinjaCatRank3Rules.KunaiCount];
        public PixelNinjaEightGate fx;
    }

    private NinjaEightGateCast CreateNinjaEightGateCast(Unit source,Unit primary,float scale,int generation)
    {
        var cast=new NinjaEightGateCast{source=source,center=Flat(primary.model.transform.position),
            scale=scale,generation=generation,cast=source.skillUses,revision=source.positionRevision,
            hitsBefore=source.skillHits,damageBefore=source.skillDamage};
        for(int i=0;i<cast.bladeVictims.Length;i++)cast.bladeVictims[i]=new HashSet<Unit>();
        return cast;
    }

    private bool NinjaEightGateValid(NinjaEightGateCast cast)=>
        !cast.finished&&cast.generation==combatGeneration&&!battleEnding&&
        (cast.released||IsCombatActive(cast.source)&&cast.source.hp>0f&&cast.source.ninjaHuntPending&&
        cast.source.skillUses==cast.cast&&cast.source.positionRevision==cast.revision);

    private void BeginNinjaEightGate(Unit source,int generation)
    {
        var cast=CreateNinjaEightGateCast(source,source.ninjaHuntTarget,source.ninjaHuntDamageMultiplier,generation);
        source.spriteAnimation.PlayNinjaEightGateAttack(
            ()=>NinjaEightGateValid(cast),
            ()=>{source.ninjaCrossActive=true;VanishShadowHunt(source);source.traitActivations++;},
            ()=>
            {
                SubstituteShadowHunt(source,generation);
                if(!mlEpisodeActive)cast.fx=PixelNinjaEightGate.Play(cast.center,Rank3Renderer(source),
                    ()=>this!=null&&NinjaEightGateValid(cast));
            },
            ()=>ArriveNinjaEightGate(cast),
            (previous,next)=>{if(!cast.released)AdvanceNinjaEightGate(cast,previous,next);},()=>FinishNinjaEightGate(cast));
    }

    private void ArriveNinjaEightGate(NinjaEightGateCast cast)
    {
        Unit source=cast.source;
        Vector3 approach=Flat(source.model.transform.position)-cast.center;
        if(approach.sqrMagnitude<.001f)approach=Vector3.back;
        Vector3 landing=Flat(source.model.transform.position);
        // Substitution can cross obstacles, but the destination must be open and outside the trap.
        for(int i=0;i<16;i++)
        {
            Vector3 candidate=cast.center+Quaternion.AngleAxis(i*22.5f,Vector3.up)*approach.normalized*
                (TileSize*NinjaCatRank3Rules.KunaiRadiusTiles+source.collisionRadius+.5f);
            if((ResolveFreePosition(candidate,source.collisionRadius)-candidate).sqrMagnitude>.0064f||
                units.Any(other=>other!=source&&IsCombatActive(other)&&
                    (Flat(other.model.transform.position)-candidate).sqrMagnitude<
                    Mathf.Pow(source.collisionRadius+other.collisionRadius+.1f,2f)))continue;
            landing=candidate;break;
        }
        source.ninjaHuntLanding=landing;
        source.model.transform.position=landing+Vector3.up*source.visualHeightOffset;
        if(source.ring!=null)source.ring.transform.position=landing+Vector3.up*.025f;
        source.lastGroundPosition=landing;source.velocity=Vector3.zero;
        OccupyUnit(source,NearestGridCoordinate(landing));
        Vector3 facing=cast.center-landing;
        RotateUnit(source,facing,1f);source.spriteBillboard?.SetFacingDirection(facing,true);
        source.currentAction="ninja_gate_hand_sign";
        PlayNinjaSound(source,NinjaSound.Blink,source.model.transform.position,.7f);
        if(!mlEpisodeActive)PixelNinjaSubstitution.PlayArrival(Rank3Renderer(source),NinjaHuntColor(source));
    }

    private void AdvanceNinjaEightGate(NinjaEightGateCast cast,float previous,float next)
    {
        if(!NinjaEightGateValid(cast)||cast.detonated)return;
        cast.time=Mathf.Max(cast.time,next);
        if(!cast.released&&next>=NinjaCatRank3Rules.GateLaunchTime)
        {cast.released=true;if(isActiveAndEnabled)StartReleasedSkill(cast.source,ContinueNinjaEightGate(cast));}
        if(cast.fx!=null)cast.fx.Sample(next);
        Unit source=cast.source;
        if(previous<NinjaCatRank3Rules.GateInstallTime&&next>=NinjaCatRank3Rules.GateInstallTime)
            PlayNinjaSound(source,NinjaSound.Plant,cast.center);
        float from=NinjaCatRank3Rules.GateFlightProgress(previous),to=NinjaCatRank3Rules.GateFlightProgress(next);
        if(to>from)
        {
            if(from<=0f)PlayNinjaSound(source,NinjaSound.Rush,cast.center);
            source.currentAction="ninja_gate_kunai";
            Unit[] candidates=units.ToArray();
            for(int i=0;i<cast.bladeVictims.Length;i++)
            {
                if(cast.deflected[i])continue;
                Vector3 origin=NinjaCatRank3Rules.GatePoint(cast.center,i);
                Vector3 a=Vector3.Lerp(origin,cast.center,from),b=Vector3.Lerp(origin,cast.center,to),segment=b-a;
                if(DeflectGriffinProjectile(source.team,a+Vector3.up*.55f,b+Vector3.up*.55f,
                    cast.fx==null?null:cast.fx.Blade(i)))
                {cast.deflected[i]=true;cast.fx?.RemoveBlade(i);continue;}
                foreach(Unit enemy in candidates)
                {
                    if(enemy.team==source.team||!IsCombatTargetable(enemy)||enemy.hp<=0f||cast.bladeVictims[i].Contains(enemy))continue;
                    int count=cast.hits.TryGetValue(enemy,out int prior)?prior:0;
                    if(count>=NinjaCatRank3Rules.KunaiMaximumHits)continue;
                    Vector3 p=Flat(enemy.model.transform.position);
                    Vector3 closest=a+segment*Mathf.Clamp01(Vector3.Dot(p-a,segment)/segment.sqrMagnitude);
                    if((p-closest).sqrMagnitude>Mathf.Pow(TileSize*.22f+enemy.collisionRadius,2f))continue;
                    cast.bladeVictims[i].Add(enemy);cast.hits[enemy]=count+1;
                    if(!mlEpisodeActive)PixelNinjaEightGate.PlayHit(Rank3Renderer(enemy),false);
                    Rank3Hit(source,enemy,NinjaCatRank3Rules.KunaiPathRatio*cast.scale);
                    if(IsCombatActive(enemy)&&enemy.hp>0f)ApplyNinjaRank3Intrinsic(source,enemy,p,false);
                }
            }
        }
        if(next<NinjaCatRank3Rules.GateBlastTime)return;
        cast.detonated=true;
        int arrived=cast.deflected.Count(blocked=>!blocked);
        if(arrived>0)PlayNinjaSound(source,NinjaSound.Blast,cast.center);
        if(cast.fx!=null){if(arrived>0)cast.fx.Detonate();else cast.fx.Cancel();}
        foreach(Unit enemy in Rank3Enemies(source,cast.center,TileSize*NinjaCatRank3Rules.KunaiExplosionRadiusTiles))
        {
            if(arrived==0)break;
            if(!IsCombatTargetable(enemy)||enemy.hp<=0f)continue;
            if(!mlEpisodeActive)PixelNinjaEightGate.PlayHit(Rank3Renderer(enemy),true);
            Rank3Hit(source,enemy,NinjaCatRank3Rules.KunaiExplosionRatio*cast.scale*arrived/cast.deflected.Length);
            Rank3Knockdown(source,enemy,NinjaCatRank3Rules.GateKnockdownDuration);
        }
        if(IsCombatActive(source)&&source.skillUses==cast.cast)
        {source.ninjaEmpoweredBasicAttacks=Mathf.Max(source.ninjaEmpoweredBasicAttacks,NinjaCatCombatRules.EmpoweredBasicAttackCount);
         PrepareNinjaOpeningStrike(source);}
        source.ninjaCrossActive=false;source.currentAction="ninja_gate_detonate";
    }

    private void FinishNinjaEightGate(NinjaEightGateCast cast)
    {
        if(cast.finished)return;
        if(cast.released&&!cast.detonated&&ReleasedSkillValid(cast.source,cast.generation))
        {
            if(cast.source.skillUses==cast.cast)
            {cast.source.ninjaCrossActive=false;cast.source.ninjaHuntPending=false;cast.source.ninjaHuntTarget=null;}
            return;
        }
        cast.finished=true;
        if(!cast.detonated&&cast.fx!=null)cast.fx.Cancel();
        Unit source=cast.source;
        if(source.skillUses!=cast.cast)return;
        source.ninjaCrossActive=false;source.ninjaHuntPending=false;
        source.ninjaHuntTarget=null;source.ninjaHuntDamageMultiplier=1f;
        if(cast.generation==combatGeneration)
            RecordSkillEvent(source,NinjaCatRank3Rules.EightGateFormation,
                source.skillHits-cast.hitsBefore,source.skillDamage-cast.damageBefore);
    }

    private IEnumerator ContinueNinjaEightGate(NinjaEightGateCast cast)
    {
        try
        {
            while(NinjaEightGateValid(cast)&&cast.time<NinjaCatRank3Rules.GateRecoveryEnd)
            {
                yield return null;if(paused)continue;
                float before=cast.time;cast.time+=CombatSkillClock.Delta;
                AdvanceNinjaEightGate(cast,before,cast.time);
            }
        }
        finally{FinishNinjaEightGate(cast);}
    }

    private void ResolveNinjaEightGateImmediate(Unit source,Unit target,float scale,int generation)
    {
        var cast=CreateNinjaEightGateCast(source,target,scale,generation);
        source.ninjaHuntPending=true;source.ninjaCrossActive=true;source.traitActivations++;
        ArriveNinjaEightGate(cast);
        AdvanceNinjaEightGate(cast,0f,NinjaCatRank3Rules.GateBlastTime);
        FinishNinjaEightGate(cast);
    }
}
