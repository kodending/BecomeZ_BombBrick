using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Combat;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    // 0: molt, 1: paired basic attack, 2: low flight. One combat clock owns pose and hits.
    private sealed class MantisReaperAction
    {
        public int kind,generation,revision,strikes;
        public float age,scale=1f,width,rate=1f,damage;
        public bool critical,killed;
        public System.Action cameraEffectCompleted;
        public Unit target;
        public Vector3 origin,end,bladeEnd,forward,killPoint;
        public readonly HashSet<Unit>[] hits={new HashSet<Unit>(),new HashSet<Unit>()};
    }
    private static bool MantisReaperFlying(Unit unit)=>unit!=null&&unit.mantisReaperAction!=null&&
        unit.mantisReaperAction.kind==2&&unit.mantisReaperAction.age>=.12f&&unit.mantisReaperAction.age<.52f;

    private float MantisReaperCastRange(Unit unit)=>TileSize*(HasRank3(unit,MantisCombatRules.SantiusTraitId)?4f:3f);

    private bool MantisReaperValid(Unit unit,MantisReaperAction action)=>
        !battleEnding&&unit.mantisReaperAction==action&&action.generation==combatGeneration&&IsCombatActive(unit)&&
        action.revision==unit.positionRevision&&!unit.displacing&&!SpiderSilkBound(unit)&&
        (action.kind!=2||(!unit.combatStatuses.Has(CombatStatusKind.Root)&&unit.griffinRootTimer<=0f))&&
        !unit.combatStatuses.Has(CombatStatusKind.Knockdown)&&!unit.combatStatuses.Has(CombatStatusKind.Fear)&&unit.giraffeStunTimer<=0f;

    private void BeginMantisReaperAction(Unit unit,MantisReaperAction action)
    {
        unit.spriteAnimation?.InterruptCurrentAction(true);
        action.generation=combatGeneration;action.revision=unit.positionRevision;
        unit.mantisReaperAction=action;unit.velocity=Vector3.zero;unit.hexStepping=false;
        ClearRoute(unit);SetMoving(unit,false);
        unit.mantisScythePending=false;unit.mantisScytheTarget=null;
        unit.mantisScytheDamageMultiplier=1f;unit.mantisScytheRadiusMultiplier=1f;
        unit.spriteAnimation?.PlayMantisReaper(action.kind,()=>
        {if(unit.mantisReaperAction==action)EndMantisReaperAction(unit,false);});
        TickMantisReaper(unit,0f);
    }

    private void ActivateMantisReaper(Unit source)=>ActivateMantisReaperWithCameraCompletion(source,null);

    private void ActivateMantisReaperWithCameraCompletion(Unit source,System.Action cameraEffectCompleted)
    {
        if(!IsCombatActive(source)||source.rank3MantisReaper){cameraEffectCompleted?.Invoke();return;}
        source.rank3MantisReaper=true;source.rank3MantisBaseScale=source.model.transform.localScale;
        source.rank3MantisBaseBodyScale=source.combatBodyScale;
        source.rank3AttackBonusTimer=source.rank3AttackSpeedBonusTimer=source.rank3MoveSpeedBonusTimer=GateHaetaeBenefit(source,9999f);
        source.rank3AttackBonusRatio=Mathf.Max(source.rank3AttackBonusRatio,MantisRank3Rules.ReaperAttackBonus);
        source.rank3AttackSpeedBonusRatio=Mathf.Max(source.rank3AttackSpeedBonusRatio,MantisRank3Rules.ReaperSpeedBonus);
        source.rank3MoveSpeedBonusRatio=Mathf.Max(source.rank3MoveSpeedBonusRatio,MantisRank3Rules.ReaperSpeedBonus);
        source.skillCooldownDuration*=.75f;source.skillTimer=Mathf.Min(source.skillTimer,source.skillCooldownDuration);
        Heal(source,(source.maxHp-source.hp)*MantisRank3Rules.ReaperHealMissingRatio);
        source.combatEffectIcons?.Show(CombatEffectIconKind.AttackUp,9999f);
        source.combatEffectIcons?.Show(CombatEffectIconKind.Haste,9999f);
        if(!mlEpisodeActive)source.mantisReaperFx=PixelMantisReaper.Play(Rank3Renderer(source));
        BeginMantisReaperAction(source,new MantisReaperAction{kind=0,cameraEffectCompleted=cameraEffectCompleted});
    }

    private void StartMantisReaperBasic(Unit source,Unit target,float damage,bool critical,float rate)
    {
        if(source.mantisReaperAction!=null||!IsCombatTargetable(target))return;
        Vector3 forward=Flat(target.model.transform.position-source.model.transform.position).normalized;
        BeginMantisReaperAction(source,new MantisReaperAction{kind=1,target=target,forward=forward,
            damage=damage,critical=critical,rate=Mathf.Max(.25f*CombatStatFormula.CombatAttackRate,rate)});
    }

    private void StartMantisReaperFlight(Unit source,Unit target,float scale,float radiusScale,int generation)
    {
        if(source.mantisReaperAction!=null||generation!=combatGeneration||!IsCombatTargetable(target))return;
        Vector3 origin=Flat(source.model.transform.position),center=Flat(target.model.transform.position);
        Vector3 forward=(center-origin).normalized;if(forward.sqrMagnitude<.01f)forward=Vector3.right;
        bool sand=HasRank3(source,MantisCombatRules.SantiusTraitId);
        float length=Mathf.Min(TileSize*MantisRank3Rules.ReaperFlightTiles,Vector3.Distance(origin,center)+TileSize*.75f);
        Vector3 end=origin+forward*length;
        // Stop before scenery while passing through units; do not teleport through a wall on landing.
        for(float travel=.2f;travel<=length+.2f;travel+=.2f)
        {
            Vector3 next=origin+forward*Mathf.Min(length,travel);
            if((ResolveFreePosition(next,source.collisionRadius)-next).sqrMagnitude>.0001f)
            {end=origin+forward*Mathf.Max(0f,travel-.2f);break;}
        }
        var action=new MantisReaperAction{kind=2,target=target,origin=origin,end=end,forward=forward,
            bladeEnd=end+forward*(sand?TileSize*1.5f:0f),scale=scale,width=TileSize*.5f*radiusScale*(sand?1.5f:1f)};
        BeginMantisReaperAction(source,action);
    }

    private void TickMantisReaper(Unit source,float dt)
    {
        var action=source.mantisReaperAction;if(action==null)return;
        if(!MantisReaperValid(source,action)){EndMantisReaperAction(source,false);return;}
        float previous=action.age;
        // The paired basic attack already derives its rate from the paced attack interval.
        if(!source.combatStatuses.Has(CombatStatusKind.Freeze))action.age+=Mathf.Max(0f,dt)*action.rate*
            (action.kind==1?1f:CombatStatFormula.CombatAnimationRate);
        float time=action.age;
        source.attackMovementLockTimer=.1f;source.velocity=Vector3.zero;
        source.currentAction=action.kind==0?"mantis_molt":action.kind==1?"mantis_reaper_basic":"mantis_reaper_flight";
        source.spriteAnimation?.PoseMantisReaper(time);
        if(action.kind==0)
        {
            float formScale=Mathf.Lerp(1f,MantisRank3Rules.ReaperScale,
                Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.2f,.44f,time)));
            source.model.transform.localScale=source.rank3MantisBaseScale*formScale;
            source.combatBodyScale=source.rank3MantisBaseBodyScale*formScale;
            CacheMeleeReadabilityBody(source);
            source.targetRing?.SetRadius(Mathf.Max(.3f,CombatBodyRadius(source)*1.15f));
            source.mantisReaperFx?.Molt(time);
            if(previous<.2f&&time>=.2f&&source.mantisBroodOwner==null)
                PlayMantisSound(source,MantisSound.Molt,Rank3Center(source));
        }
        else if(action.kind==1)
        {
            for(int i=0;i<2;i++)if(action.strikes<=i&&time>=.12f+i*.1f)
            {action.strikes=i+1;MantisReaperBasicContact(source,action,i);if(!MantisReaperValid(source,action))return;}
        }
        else
        {
            float progress=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.12f,.52f,time));
            Vector3 ground=Vector3.Lerp(action.origin,action.end,progress);
            source.model.transform.position=ground+Vector3.up*source.visualHeightOffset;
            source.lastGroundPosition=ground;
            if(source.ring!=null)source.ring.transform.position=ground+Vector3.up*.025f;
            float lift=TileSize*.45f*Mathf.Sin(Mathf.Clamp01((time-.06f)/.52f)*Mathf.PI);
            source.spriteBillboard?.SetRollPose(-8f*Mathf.Sin(progress*Mathf.PI),Vector3.zero,Vector3.up*lift);
            source.mantisReaperFx?.Flight(time,action.origin,action.bladeEnd,action.width);
            for(int i=0;i<2;i++)
            {
                float launch=MantisRank3Rules.ReaperLaunch+i*MantisRank3Rules.ReaperCutInterval;
                if(time<launch||previous>=launch+MantisRank3Rules.ReaperCutFlight)continue;
                float from=Mathf.Clamp01((previous-launch)/MantisRank3Rules.ReaperCutFlight);
                float to=Mathf.Clamp01((time-launch)/MantisRank3Rules.ReaperCutFlight);
                Vector3 a=Vector3.Lerp(action.origin,action.bladeEnd,from),b=Vector3.Lerp(action.origin,action.bladeEnd,to);
                if(action.strikes<=i)
                {
                    action.strikes=i+1;ReaperSlash(source,i,1.15f);
                    if(i==0)PlayMantisSound(source,MantisSound.Flight,Rank3Center(source));
                }
                foreach(Unit enemy in units.ToArray())
                {
                    if(!IsCombatTargetable(enemy)||enemy.team==source.team||action.hits[i].Contains(enemy)||
                       WolfCombatRules.DistanceToSegment(Flat(enemy.model.transform.position),a,b)>action.width+enemy.collisionRadius)continue;
                    action.hits[i].Add(enemy);
                    Rank3Hit(source,enemy,MantisCombatRules.HitDamage(1f,MantisRank3Rules.ReaperSkillRatio,i,
                        HasRank3(source,MantisCombatRules.MantiusTraitId))*action.scale);
                    bool both=action.hits[1-i].Contains(enemy);
                    if(!IsCombatActive(enemy)){action.killed=true;action.killPoint=Flat(enemy.model.transform.position);}
                    else if(both)ApplyMantisDoubleCutStatus(source,enemy);
                    ReaperContact(source,enemy,both);
                    if(!MantisReaperValid(source,action))return;
                }
            }
        }
        float end=action.kind==0?MantisRank3Rules.ReaperMoltEnd:action.kind==1?MantisRank3Rules.ReaperBasicEnd:MantisRank3Rules.ReaperFlightEnd;
        if(time>=end)EndMantisReaperAction(source,true);
    }

    private void MantisReaperBasicContact(Unit source,MantisReaperAction action,int strike)
    {
        Unit target=action.target;
        Vector3 origin=Flat(source.model.transform.position);
        bool primary=IsCombatTargetable(target)&&Vector3.Distance(origin,Flat(target.model.transform.position))<=AttackDistance(source,target)&&HasClearAttackLine(source,target);
        ReaperSlash(source,strike,strike==0?.95f:1.25f);
        PlayMantisSound(source,MantisSound.Strike,Rank3Center(source));
        if(primary)
        {
            bool saved=source.lastBasicAttackCritical;source.lastBasicAttackCritical=action.critical;
            if(strike==0)BeginBasicAttackImpact(source,target,action.damage*MantisRank3Rules.ReaperBasicRatio,action.critical);
            else ApplyDamage(source,target,action.damage*MantisRank3Rules.ReaperBasicRatio,false,true,false);
            source.lastBasicAttackCritical=saved;ReaperContact(source,target,false,true);
        }
        if(strike==0||!MantisReaperValid(source,action))return;
        foreach(Unit enemy in units.ToArray())
        {
            if(enemy==target||enemy.team==source.team||!IsCombatTargetable(enemy))continue;
            Vector3 delta=Flat(enemy.model.transform.position)-origin;
            if(delta.magnitude>TileSize*1.5f+enemy.collisionRadius||
               Vector3.Dot(delta.normalized,action.forward)<.5f||!HasClearAttackLine(source,enemy))continue;
            ApplyDamage(source,enemy,action.damage*MantisRank3Rules.ReaperBasicRatio,false,true,false);
            ReaperContact(source,enemy,false,true);
            if(!MantisReaperValid(source,action))return;
        }
    }

    private void ReaperSlash(Unit source,int strike,float size)
    {
        if(mlEpisodeActive)return;
        var renderer=Rank3Renderer(source);
        PixelCombatEffect.PlayMantisScytheSlash(renderer==null?source.model.transform:renderer.transform,
            renderer,2.5f,strike==1,size,new Color(.72f,.65f,1f));
    }
    private void ReaperContact(Unit source,Unit target,bool doubled,bool basic=false)
    {
        if(mlEpisodeActive||target.model==null)return;
        PixelCombatEffect.PlayMantisCutImpact(target.model.transform,Rank3Renderer(target),new Color(.73f,.63f,1f),doubled);
        // Paired basic strikes retain their shared hit/critical sound; only skills add this contact.
        if(!basic)
            PlayMantisSound(source,MantisSound.Hit,Flat(target.model.transform.position),doubled?1f:.8f);
    }

    private void EndMantisReaperAction(Unit source,bool complete)
    {
        var action=source.mantisReaperAction;if(action==null)return;
        bool completed=complete&&action.kind==2&&action.age>=MantisRank3Rules.ReaperFlightEnd&&
            MantisReaperValid(source,action);
        source.mantisReaperAction=null;
        source.spriteAnimation?.InterruptCurrentAction(true);
        source.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);
        if(source.mantisReaperFx!=null&&!CombatEffectEndFade.IsFading(source.mantisReaperFx))source.mantisReaperFx.Rest();
        if(source.model!=null)
        {
            if(action.kind==0)
            {
                source.model.transform.localScale=source.rank3MantisBaseScale*MantisRank3Rules.ReaperScale;
                source.combatBodyScale=source.rank3MantisBaseBodyScale*MantisRank3Rules.ReaperScale;
                CacheMeleeReadabilityBody(source);
                source.targetRing?.SetRadius(Mathf.Max(.3f,CombatBodyRadius(source)*1.15f));
            }
            Vector3 ground=ResolveFreePosition(Flat(source.model.transform.position),source.collisionRadius);
            source.model.transform.position=ground+Vector3.up*source.visualHeightOffset;
            source.lastGroundPosition=ground;if(source.ring!=null)source.ring.transform.position=ground+Vector3.up*.025f;
            OccupyUnit(source,NearestGridCoordinate(ground));
        }
        source.attackMovementLockTimer=0f;source.velocity=Vector3.zero;source.decisionTimer=0f;
        ClearRoute(source);
        action.cameraEffectCompleted?.Invoke();
        if(!complete||!IsCombatActive(source))return;
        if(action.kind==2)
        {
            if(completed)BeginSkillInertia(source,Flat(source.model.transform.position)-action.origin,
                .4f/Mathf.Max(.25f,action.rate));
            source.traitActivations++;RecordSkillEvent(source,MantisRank3Rules.ReaperEmergence,source.skillHits,source.skillDamage);
            if(action.killed&&HasRank3(source,MantisCombatRules.NectisTraitId)&&Application.isPlaying)
                StartReleasedSkill(source,MantisReaperEcho(source,action));
        }
    }

    private IEnumerator MantisReaperEcho(Unit source,MantisReaperAction action)
    {
        if(!IsCombatActive(source))yield break;
        var shots=new List<PixelStarProjectile>();
        System.Func<bool> valid=()=>ReleasedSkillValid(source,action.generation);
        Unit target=Rank3Enemies(source,action.killPoint,TileSize*7f).Where(IsCombatTargetable)
            .OrderBy(x=>(Flat(x.model.transform.position)-action.killPoint).sqrMagnitude).FirstOrDefault();
        if(target==null)yield break;
        try
        {
            for(int i=0;i<2;i++)
            {
                if(!valid()||!IsCombatTargetable(target))yield break;
                PlayMantisSound(source,MantisSound.Launch,action.killPoint,.65f);
                shots.Add(PixelStarProjectile.PlayMyriadEcho(action.killPoint+Vector3.up*.6f,Rank3Renderer(source),
                    target.model.transform,Rank3Renderer(target),.95f,new Color(.7f,.45f,1f),()=>
                    {if(valid()&&IsCombatTargetable(target)){Rank3Hit(source,target,1.2f*.6f*action.scale);ReaperContact(source,target,false);}},
                    ()=>valid()&&IsCombatTargetable(target)));
                if(i==0)yield return SkillWait(source,.1f);
            }
            while(shots.Any(x=>x!=null)){if(!valid())yield break;yield return null;}
        }
        finally{foreach(var shot in shots)if(shot!=null)shot.Cancel();}
    }
}
