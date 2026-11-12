using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class NinjaSpacetimeCast
    {
        public int generation;
        public float time,scale;
        public bool impacted;
        public PixelNinjaSpacetime effect;
    }
    private readonly Dictionary<Unit,NinjaSpacetimeCast> ninjaSpacetimeCasts=new Dictionary<Unit,NinjaSpacetimeCast>();
    private readonly List<PixelNinjaSpacetime> ninjaSpacetimeEffects=new List<PixelNinjaSpacetime>();

    private bool NinjaSpacetimeActive(Unit source)=>source!=null&&ninjaSpacetimeCasts.ContainsKey(source);

    private bool BeginNinjaSpacetime(Unit source,SkillMasterRow skill)
    {
        if(source==null||skill==null||battleEnding||!IsCombatActive(source)||source.hp<=0f||
           !HasFamilyTranscendence(source)||!IsNinjaCatFamily(source)||NinjaSpacetimeActive(source)||
           !units.Any(u=>u.team!=source.team&&IsCombatTargetable(u)&&u.hp>0f))return false;
        var cast=new NinjaSpacetimeCast{generation=combatGeneration,scale=Mathf.Max(0f,source.skillTraitDamageMultiplier)};
        ninjaSpacetimeCasts.Add(source,cast);FamilyTranscendenceStateFor(source);
        source.ninjaHuntPending=false;source.ninjaHuntTarget=null;source.ninjaCrossActive=false;source.ninjaUntargetableTimer=0f;
        source.skillTimer=source.skillCooldownDuration;source.skillUses++;source.traitActivations++;
        source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,NinjaSpacetimeRules.Duration);
        source.currentAction="ninja_spacetime_prepare";
        ClearRoute(source);StopBeforeAttack(source);SetMoving(source,false);
        source.spriteAnimation?.InterruptCurrentAction(true);
        source.spriteAnimation?.PlaySkillAttackWithHold(0,NinjaSpacetimeRules.Windup,1f,null,()=>cast.time);
        ApplyPostSkillItemEffects(source);RecordSkillEvent(source,"ninja_spacetime_start",0,0f);
        if(!mlEpisodeActive)
        {
            ninjaSpacetimeEffects.RemoveAll(fx=>fx==null);
            var bodies=units.Where(u=>u.team!=source.team&&IsCombatTargetable(u)).Select(u=>Rank3Renderer(u))
                .Where(r=>r!=null).Select(r=>r.bounds).ToArray();
            cast.effect=PixelNinjaSpacetime.Create(combatBounds,Rank3Renderer(source),bodies,source.definition.monsterId);
            if(cast.effect!=null)ninjaSpacetimeEffects.Add(cast.effect);
        }
        if(!QueueRank4Cinematic(source)&&!mlEpisodeActive)
            PlayNinjaSound(source,NinjaSound.SpacetimeCharge,source.model.transform.position,.7f);
        return true;
    }

    private void TickNinjaSpacetime(Unit source,float dt)
    {
        NinjaSpacetimeCast cast;
        if(source==null||!ninjaSpacetimeCasts.TryGetValue(source,out cast)||IsNinjaSpacetimeCinematicPending(source))return;
        AdvanceNinjaSpacetime(source,cast.time+Mathf.Max(0f,dt));
    }

    private void AdvanceNinjaSpacetime(Unit source,float time)
    {
        NinjaSpacetimeCast cast;
        if(source==null||!ninjaSpacetimeCasts.TryGetValue(source,out cast))return;
        if(cast.generation!=combatGeneration||battleEnding||!IsCombatActive(source)||source.hp<=0f||!HasFamilyTranscendence(source))
        {CancelNinjaSpacetime(source);return;}
        if(float.IsNaN(time)||float.IsInfinity(time))return;
        cast.time=Mathf.Max(cast.time,Mathf.Clamp(time,0f,NinjaSpacetimeRules.Duration));
        cast.effect?.Pose(cast.time);
        source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,NinjaSpacetimeRules.Duration-cast.time);
        if(!cast.impacted&&cast.time>=NinjaSpacetimeRules.Windup)
        {
            // Commit before resolving deaths: reflect/cleanup cannot replay the same area impact.
            cast.impacted=true;
            Unit[] victims=units.Where(u=>u.team!=source.team&&IsCombatTargetable(u)&&u.hp>0f).ToArray();
            var bodies=victims.Select(u=>Rank3Renderer(u)).Where(r=>r!=null).Select(r=>r.bounds).ToArray();
            cast.effect?.Impact(bodies);
            source.spriteAnimation?.InterruptCurrentAction(true);
            source.spriteAnimation?.PlaySkillAttack(3f,null);
            PlayNinjaSound(source,NinjaSound.SpacetimeCollapse,source.model.transform.position);
            float power=SkillPower(source)*NinjaSpacetimeRules.DamageRatio*cast.scale,total=0f;int hits=0;
            foreach(Unit victim in victims)
            {
                if(!IsCombatTargetable(victim)||victim.hp<=0f)continue;
                total+=ApplySkillDamage(source,victim,new SkillDamageFormulaResult(power,0f,0f),
                    triggerTraits:false,triggerTeamTraits:false,applyTraitMultiplier:false,allowHitSlide:false);
                hits++;
            }
            source.skillHits+=hits;source.skillDamage+=total;
            if(!mlEpisodeActive)combatCameraDirector?.ReportSpacetimeCollapse();
            source.currentAction="ninja_spacetime_collapse";
            RecordSkillEvent(source,"ninja_spacetime_collapse",hits,total);
        }
        NinjaSpacetimeCast current;
        if(cast.time>=NinjaSpacetimeRules.Duration&&ninjaSpacetimeCasts.TryGetValue(source,out current)&&ReferenceEquals(cast,current))
            CancelNinjaSpacetime(source);
    }

    private void CancelNinjaSpacetime(Unit source)
    {
        NinjaSpacetimeCast cast;
        if(source==null||!ninjaSpacetimeCasts.TryGetValue(source,out cast))return;
        ninjaSpacetimeCasts.Remove(source);
        source.attackMovementLockTimer=0f;
        source.spriteAnimation?.InterruptCurrentAction(true);
        if(cast.effect!=null){if(cast.impacted)cast.effect.Release();else cast.effect.Cancel();}
    }

    private void CancelNinjaSpacetimeCasts()
    {
        foreach(Unit source in ninjaSpacetimeCasts.Keys.ToArray())CancelNinjaSpacetime(source);
    }

    private void ResetNinjaSpacetime()
    {
        CancelNinjaSpacetimeCasts();
        foreach(var effect in ninjaSpacetimeEffects)if(effect!=null)effect.Clear();
        ninjaSpacetimeEffects.Clear();
    }
}
