using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class NinjaBlackMoonCast
    {
        public Unit source,primary,positioned;
        public Unit[] targets;
        public bool[] marked;
        public PixelNinjaMoonMark[] marks;
        public int generation,cast,revision,hitsBefore;
        public float scale,damageBefore;
        public bool detonated,finished;
    }

    private NinjaBlackMoonCast CreateNinjaBlackMoonCast(Unit source,Unit primary,float scale,int generation)
    {
        Vector3 center=Flat(primary.model.transform.position);
        Unit[] targets=Rank3Enemies(source,center,TileSize*NinjaCatRank3Rules.BlackMoonRadiusTiles)
            .Where(enemy=>IsCombatTargetable(enemy)&&enemy.hp>0f)
            .OrderBy(enemy=>enemy==primary?0:1)
            .ThenBy(enemy=>(Flat(enemy.model.transform.position)-center).sqrMagnitude)
            .Take(NinjaCatRank3Rules.BlackMoonAttackCount).ToArray();
        return new NinjaBlackMoonCast{source=source,primary=primary,targets=targets,
            marked=new bool[targets.Length],marks=new PixelNinjaMoonMark[targets.Length],
            scale=scale,generation=generation,cast=source.skillUses,revision=source.positionRevision,
            hitsBefore=source.skillHits,damageBefore=source.skillDamage};
    }

    private bool NinjaBlackMoonValid(NinjaBlackMoonCast cast)=>
        !cast.finished&&cast.generation==combatGeneration&&!battleEnding&&
        IsCombatActive(cast.source)&&cast.source.hp>0f&&cast.source.ninjaHuntPending&&
        cast.source.skillUses==cast.cast&&cast.source.positionRevision==cast.revision&&
        (cast.detonated||cast.targets.Any(enemy=>IsCombatActive(enemy)&&enemy.hp>0f));

    private void BeginNinjaBlackMoon(Unit source,int generation)
    {
        var cast=CreateNinjaBlackMoonCast(source,source.ninjaHuntTarget,source.ninjaHuntDamageMultiplier,generation);
        source.spriteAnimation.PlayNinjaCrossAttack(
            ()=>NinjaBlackMoonValid(cast),
            ()=>{source.ninjaCrossActive=true;VanishShadowHunt(source);source.traitActivations++;},
            ()=>SubstituteShadowHunt(source,generation),
            step=>MoveNinjaBlackMoon(cast,step),step=>HitNinjaBlackMoon(cast,step),
            ()=>FinishNinjaBlackMoon(cast),cast.targets.Length,true,
            step=>{if(!mlEpisodeActive)PixelNinjaMoonFx.PlayRush(Rank3Renderer(source),source.facingDirection,true);});
    }

    private void MoveNinjaBlackMoon(NinjaBlackMoonCast cast,int step)
    {
        bool final=step==cast.targets.Length;
        Unit victim=final?(IsCombatTargetable(cast.primary)&&cast.primary.hp>0f?cast.primary:
            cast.targets.LastOrDefault(enemy=>IsCombatTargetable(enemy)&&enemy.hp>0f)):cast.targets[step];
        cast.positioned=null;
        if(final)foreach(var mark in cast.marks)if(mark!=null)mark.Prime();
        Vector3 approach=victim==null?Vector3.forward:-Flat(victim.facingDirection);
        if(!final)approach=Quaternion.AngleAxis(step%2==0?65f:-65f,Vector3.up)*approach;
        if(victim==null||!MoveNinjaToAttackPosition(cast.source,victim,approach,false,final))return;
        cast.positioned=victim;
        if(!final&&!mlEpisodeActive)
        {
            Vector3 direction=victim.model.transform.position-cast.source.model.transform.position;
            cast.source.spriteAnimation?.SetNinjaMoonApproach(direction);
            PixelNinjaMoonFx.PlayRush(Rank3Renderer(cast.source),direction,false);
        }
        cast.source.currentAction=final?"ninja_moon_sheathe":"ninja_moon_step_"+step;
    }

    private void HitNinjaBlackMoon(NinjaBlackMoonCast cast,int step)
    {
        if(!NinjaBlackMoonValid(cast)||cast.detonated)return;
        Unit source=cast.source;
        if(step==cast.targets.Length)
        {
            // Commit every surviving mark in this callback: no coroutine or per-target delay.
            cast.detonated=true;
            PlayNinjaSound(source,NinjaSound.Sheathe,source.model.transform.position);
            for(int i=0;i<cast.targets.Length;i++)
            {
                Unit victim=cast.targets[i];
                if(!cast.marked[i]||!IsCombatTargetable(victim)||victim.hp<=0f)
                {if(cast.marks[i]!=null)cast.marks[i].Cancel();continue;}
                // The shared sound gate folds all same-frame mark bursts into one voice.
                PlayNinjaSound(source,NinjaSound.MoonBurst,victim.model.transform.position);
                if(cast.marks[i]!=null)cast.marks[i].Explode();
                Rank3Hit(source,victim,NinjaCatRank3Rules.BlackMoonBurstRatio*cast.scale);
            }
            if(cast.targets.Where((enemy,i)=>cast.marked[i]).Contains(cast.primary)&&
               IsCombatTargetable(cast.primary)&&cast.primary.hp>0f)
                Rank3Hit(source,cast.primary,NinjaCatRank3Rules.BlackMoonExecutionRatio*cast.scale);
            source.ninjaEmpoweredBasicAttacks=Mathf.Max(source.ninjaEmpoweredBasicAttacks,NinjaCatCombatRules.EmpoweredBasicAttackCount);
            PrepareNinjaOpeningStrike(source);
            source.ninjaCrossActive=false;source.currentAction="ninja_moon_detonate";
            return;
        }
        Unit target=cast.targets[step];
        if(cast.positioned!=target||!IsCombatTargetable(target)||target.hp<=0f)return;
        PlayNinjaSound(source,NinjaSound.Slash,target.model.transform.position);
        if(!mlEpisodeActive)
        {
            Vector3 direction=target.model.transform.position-source.model.transform.position;
            PixelNinjaMoonFx.PlayImpact(Rank3Renderer(target),direction,false);
            target.spriteAnimation?.PlayImpactRecoil(direction,TileSize*.11f);
        }
        Rank3Hit(source,target,NinjaCatRank3Rules.BlackMoonCutRatio*cast.scale);
        if(!IsCombatActive(target)||target.hp<=0f)return;
        cast.marked[step]=true;
        // Preserve one innate application per victim, regardless of the two damage phases.
        ApplyNinjaRank3Intrinsic(source,target,Flat(target.model.transform.position),false,true);
        if(!mlEpisodeActive)
            cast.marks[step]=PixelNinjaMoonMark.Play(Rank3Renderer(target),
                ()=>this!=null&&NinjaBlackMoonValid(cast)&&IsCombatActive(target)&&target.hp>0f);
    }

    private void FinishNinjaBlackMoon(NinjaBlackMoonCast cast)
    {
        if(cast.finished)return;
        cast.finished=true;
        if(!cast.detonated)
            foreach(var mark in cast.marks)if(mark!=null)mark.Cancel();
        Unit source=cast.source;
        if(source.skillUses!=cast.cast)return;
        source.ninjaCrossActive=false;source.ninjaHuntPending=false;
        source.ninjaHuntTarget=null;source.ninjaHuntDamageMultiplier=1f;
        if(cast.generation==combatGeneration)
            RecordSkillEvent(source,NinjaCatRank3Rules.BlackMoonHunt,
                source.skillHits-cast.hitsBefore,source.skillDamage-cast.damageBefore);
    }

    private void ResolveNinjaBlackMoonImmediate(Unit source,Unit target,float scale,int generation)
    {
        var cast=CreateNinjaBlackMoonCast(source,target,scale,generation);
        source.ninjaHuntPending=true;source.ninjaCrossActive=true;source.traitActivations++;
        for(int step=0;step<=cast.targets.Length&&NinjaBlackMoonValid(cast);step++)
        {MoveNinjaBlackMoon(cast,step);HitNinjaBlackMoon(cast,step);}
        FinishNinjaBlackMoon(cast);
    }
}
