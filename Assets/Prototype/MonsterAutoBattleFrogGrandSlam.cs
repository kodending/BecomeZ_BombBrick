using System.Collections.Generic;
using System.Linq;
using BombBrick.Combat;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class FrogGrandSlamState
    {
        public MonsterAutoBattlePrototype owner;
        public Unit primary;
        public SkillMasterRow skill;
        public int generation,revision;
        public float time,damageScale,radiusScale,dealt,width;
        public int hits;
        public bool contacted,lifted,thrown,landed;
        public Vector3 center;
        public PixelFrogGrandSlam effect;
        public readonly List<FrogGrandSlamVictim> victims=new List<FrogGrandSlamVictim>();
    }

    private sealed class FrogGrandSlamVictim
    {
        public Unit unit;
        public Vector3 start,goal,throwStart,throwGoal,throwDirection,throwSafeGround;
        public int revision;
        public bool captured,throwing;
        public CombatStatusEffect root,airborne,previousRoot,previousAirborne;
        public CombatStatusEffect pullAirborne;
        public object pullAirborneSource;
        public float rootAt,airborneAt,rootCastAt,airborneCastAt;
    }

    private void BeginFrogGrandSlam(Unit source,Unit primary,SkillMasterRow skill,float damageScale,float radiusScale)
    {
        var state=new FrogGrandSlamState {owner=this,primary=primary,skill=skill,generation=combatGeneration,
            revision=source.positionRevision,damageScale=damageScale,radiusScale=radiusScale};
        Vector3 origin=Flat(source.model.transform.position);
        Vector3 direction=IsCombatActive(primary)?Flat(primary.model.transform.position)-origin:DeploymentFacing(source.team);
        if(direction.sqrMagnitude<.0001f)direction=DeploymentFacing(source.team);
        direction.Normalize();Vector3 side=Vector3.Cross(Vector3.up,direction);
        var enemies=units.Where(enemy=>enemy.team!=source.team&&IsCombatTargetable(enemy))
            .OrderBy(enemy=>enemy==primary?0:1).ThenBy(enemy=>(Flat(enemy.model.transform.position)-origin).sqrMagnitude).ToList();
        float radius=.3f;
        foreach(Unit enemy in enemies)radius=Mathf.Max(radius,CombatBodyRadius(enemy));
        float spacing=radius*2f+.16f,standOff=CombatBodyRadius(source)+radius+.8f;
        for(int i=0;i<enemies.Count;i++)
        {
            Unit enemy=enemies[i];
            Vector3 goal=ResolveFreePosition(origin+direction*(standOff+(i/3)*spacing)+side*((i%3)-1)*spacing,CombatBodyRadius(enemy));
            state.victims.Add(new FrogGrandSlamVictim {unit=enemy,start=Flat(enemy.model.transform.position),goal=goal});
            state.center+=goal;
        }
        state.center/=Mathf.Max(1,state.victims.Count);
        foreach(var victim in state.victims)state.width=Mathf.Max(state.width,Vector3.Distance(victim.goal,state.center)*2f+radius*2f);
        state.width=Mathf.Max(TileSize*2f,state.width);
        source.frogGrandSlam=state;source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,FrogGrandSlamRules.Duration);
        ClearUnitInertia(source);ClearRoute(source);StopUnitMovement(source);
        source.currentAction="frog_grand_fishing_cast";
        source.spriteAnimation?.PlaySkillAttackWithHold(1,FrogGrandSlamRules.HitTime*CombatStatFormula.CombatAnimationRate,1f,null,()=>state.time);
        PlayFrogSound(source,FrogSound.Launch,source.model.transform.position);
        if(!mlEpisodeActive)state.effect=PixelFrogGrandSlam.Create(Rank3Renderer(source),state.center,state.width,
            state.victims.Select(victim=>victim.unit.spriteAnimation==null?victim.unit.model.transform:victim.unit.spriteAnimation.transform).ToList());
    }

    private void AdvanceFrogGrandSlam(Unit source,float dt)
    {
        var state=source.frogGrandSlam;
        if(state==null)return;
        if(IsFrogCatchCinematicPending(source))return;
        if(paused||dt<=0f||source.combatStatuses.Has(CombatStatusKind.Freeze))
        {TickFrogGrandSlam(source,dt);return;}
        float remaining=dt;
        while(source.frogGrandSlam==state&&remaining>.000001f)
        {
            float boundary=state.time<FrogGrandSlamRules.CastContact?FrogGrandSlamRules.CastContact:
                state.time<FrogGrandSlamRules.PullEnd?FrogGrandSlamRules.PullEnd:
                state.time<FrogGrandSlamRules.SlamStart?FrogGrandSlamRules.SlamStart:
                state.time<FrogGrandSlamRules.HitTime?FrogGrandSlamRules.HitTime:FrogGrandSlamRules.Duration;
            float step=Mathf.Min(Mathf.Min(remaining,.025f),Mathf.Max(.000001f,boundary-state.time));
            float before=state.time;TickFrogGrandSlam(source,step);remaining-=step;
            if(state.time<=before||IsFrogCatchCinematicPending(source))break;
        }
    }

    private void TickFrogGrandSlam(Unit source,float dt)
    {
        var state=source.frogGrandSlam;
        if(state==null)return;
        if(battleEnding||state.generation!=combatGeneration||!IsCombatActive(source)||source.positionRevision!=state.revision||
           source.combatStatuses.Has(CombatStatusKind.Knockdown)||source.combatStatuses.Has(CombatStatusKind.Fear)||
           source.combatStatuses.Has(CombatStatusKind.Airborne)||SpiderSilkBound(source)||source.giraffeStunTimer>0f)
        {EndFrogGrandSlam(source);return;}
        foreach(var victim in state.victims)if(victim.captured&&!FrogGrandSlamControlValid(source,victim))ReleaseFrogGrandSlamVictim(source,victim);
        if(paused||dt<=0f||source.combatStatuses.Has(CombatStatusKind.Freeze))return;
        state.time=Mathf.Min(FrogGrandSlamRules.Duration,state.time+dt);
        if(!state.contacted&&state.time>=FrogGrandSlamRules.CastContact)
        {
            state.contacted=true;PlayFamilyTranscendenceFeedback(source,"frog");source.traitActivations++;
            PlayFrogSound(source,FrogSound.Hit,state.center);
            if(state.victims.Count>1)PlayFrogSound(source,FrogSound.Branch,state.center,1.1f);
            foreach(var victim in state.victims)
            {
                Unit enemy=victim.unit;if(!IsCombatTargetable(enemy))continue;
                FrogGrandSlamDamage(source,state,enemy,state.skill.damageMultiplier);
                if(source.frogGrandSlam!=state||!IsCombatActive(source)||state.generation!=combatGeneration){FinishFrogGrandSlamStatistics(source,state);return;}
                if(!IsCombatActive(enemy))continue;
                if(MonsterTraitRuntime.Has(source.activeTraitIds,FrogCombatRules.BuffThiefTraitId))ApplyFrogBuffTheft(source,enemy);
                if(enemy==state.primary&&MonsterTraitRuntime.Has(source.activeTraitIds,FrogCombatRules.GolgaebiTraitId)&&!enemy.combatStatuses.RejectHarmfulEffect())
                {ApplyDefenseShred(source,enemy,FrogCombatRules.AncientMucusDefenseReduction,FrogCombatRules.AncientMucusDuration);source.traitActivations++;}
                CaptureFrogGrandSlamVictim(source,state,victim);
            }
            if(QueueFrogCatchCinematic(source,state))
            {state.effect?.Advance(state.time,state.center);return;}
        }
        if(!state.lifted&&state.time>=FrogGrandSlamRules.PullEnd)
        {state.lifted=true;if(!frogCatchAdvancingCast)PlayFrogSound(source,FrogSound.Pull,source.model.transform.position,1.1f);}
        if(!state.thrown&&state.time>=FrogGrandSlamRules.SlamStart)PlanFrogGrandSlamThrow(source,state);
        foreach(var victim in state.victims)
        {
            if(!victim.captured)continue;
            if(!FrogGrandSlamControlValid(source,victim)){ReleaseFrogGrandSlamVictim(source,victim);continue;}
            Unit enemy=victim.unit;
            Vector3 ground=Flat(enemy.model.transform.position);
            if(victim.throwing)
            {
                MoveFrogGrandSlamThrow(victim,state.time);
            }
            else
            {
                float progress=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(FrogGrandSlamRules.CastContact,FrogGrandSlamRules.PullEnd,state.time));
                Vector3 delta=Vector3.Lerp(victim.start,victim.goal,progress)-ground;
                int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/Mathf.Max(.04f,CombatBodyRadius(enemy)*.25f)));
                for(int i=0;i<steps;i++)
                {
                    Vector3 next=ResolveBodyMovement(enemy,ground,ground+delta/steps,false);
                    next=Vector3.MoveTowards(ground,next,delta.magnitude/steps);
                    if(Vector3.Dot(next-ground,delta)<=0f||!HasClearPathSegment(ground,next,CombatBodyRadius(enemy))||IsHostileIceWallBlocking(enemy,ground,next))break;
                    ground=next;
                }
                MovePreyBody(enemy,ground-Flat(enemy.model.transform.position));
            }
            if(state.time>=FrogGrandSlamRules.PullEnd&&state.time<FrogGrandSlamRules.HitTime&&victim.airborne==null)
            {
                victim.previousAirborne=enemy.combatStatuses.Get(CombatStatusKind.Airborne);
                victim.airborneAt=battleElapsed;victim.airborneCastAt=state.time;
                if(victim.previousAirborne!=null)enemy.combatStatuses.Remove(CombatStatusKind.Airborne);
                victim.airborne=ApplyFrogGrandSlamControl(source,enemy,CombatStatusKind.Airborne,FrogGrandSlamRules.HitTime-state.time);
                if(victim.airborne==null){ReleaseFrogGrandSlamVictim(source,victim);continue;}
            }
            float throwProgress=FrogGrandSlamRules.ThrowProgressAt(state.time);
            float tilt=victim.throwing?Mathf.Sin(throwProgress*Mathf.PI)*32f*(victim.throwDirection.x>=0f?-1f:1f):0f;
            enemy.spriteBillboard?.SetRollPose(tilt,Vector3.zero,Vector3.up*FrogGrandSlamRules.HeightAt(state.time));
            victim.root.remaining=Mathf.Max(.0001f,FrogGrandSlamRules.HitTime-state.time);
            if(victim.airborne!=null)victim.airborne.remaining=victim.root.remaining;
            enemy.currentAction=FrogGrandSlamAction(state.time,true);
        }
        state.effect?.Advance(state.time,state.center);
        source.currentAction=FrogGrandSlamAction(state.time,false);
        if(!state.landed&&state.time>=FrogGrandSlamRules.HitTime)
        {
            state.landed=true;
            foreach(var victim in state.victims)ReleaseFrogGrandSlamVictim(source,victim);
            PlayFrogSound(source,FrogSound.Slam,state.center,1.2f);
            combatCameraDirector?.ReportFrogGrandSlam();
            foreach(var victim in state.victims)
            {
                Unit enemy=victim.unit;if(!IsCombatTargetable(enemy))continue;
                FrogGrandSlamDamage(source,state,enemy,state.skill.secondaryDamageMultiplier);
                if(source.frogGrandSlam!=state||!IsCombatActive(source)||state.generation!=combatGeneration){FinishFrogGrandSlamStatistics(source,state);return;}
                if(!IsCombatActive(enemy))continue;
                if(MonsterTraitRuntime.Has(source.activeTraitIds,FrogCombatRules.ReflexSnatchTraitId))DrainFrogCooldownCharge(source,enemy);
                ApplyFrogSlamKnockdown(source,enemy);
                enemy.frogSlowRatio=Mathf.Max(enemy.frogSlowRatio,Mathf.Clamp01(state.skill.statusValue));
                enemy.frogSlowTimer=Mathf.Max(enemy.frogSlowTimer,HostileStatusDuration(enemy,state.skill.statusDurationSeconds)*ConcertDurationScale(source));
            }
            if(MonsterTraitRuntime.Has(source.activeTraitIds,FrogCombatRules.RokrokTraitId))
            {
                float radius=TileSize*FrogCombatRules.ExplosiveSalivaRadiusTiles*Mathf.Max(.01f,state.radiusScale);
                Vector3 splashCenter=IsCombatActive(state.primary)?Flat(state.primary.model.transform.position):state.center;
                foreach(Unit enemy in units)
                    if(enemy!=state.primary&&enemy.team!=source.team&&IsCombatTargetable(enemy)&&Vector3.Distance(Flat(enemy.model.transform.position),splashCenter)<=radius)
                    {
                        float dealt=ApplySkillDamage(source,enemy,source.damage*FrogCombatRules.ExplosiveSalivaDamageRatio*state.damageScale);
                        state.dealt+=dealt;state.hits++;
                        if(source.frogGrandSlam!=state||!IsCombatActive(source)||state.generation!=combatGeneration)
                        {FinishFrogGrandSlamStatistics(source,state);return;}
                    }
            }
            FinishFrogGrandSlamStatistics(source,state);
        }
        if(source.frogGrandSlam==state&&state.time>=FrogGrandSlamRules.Duration)EndFrogGrandSlam(source);
    }

    private void PlanFrogGrandSlamThrow(Unit source,FrogGrandSlamState state)
    {
        state.thrown=true;
        var throwing=new List<FrogGrandSlamVictim>();
        foreach(var victim in state.victims)
            if(victim.captured&&FrogGrandSlamControlValid(source,victim))
            {victim.throwStart=victim.throwSafeGround=Flat(victim.unit.model.transform.position);throwing.Add(victim);}
        if(throwing.Count==0)return;
        throwing.Sort((a,b)=>
        {
            int angle=FrogGrandSlamThrowAngle(a.throwStart,state.center).CompareTo(FrogGrandSlamThrowAngle(b.throwStart,state.center));
            return angle!=0?angle:a.unit.id.CompareTo(b.unit.id);
        });
        float step=Mathf.PI*2f/throwing.Count,phaseX=0f,phaseY=0f;
        for(int i=0;i<throwing.Count;i++)
        {
            float phase=FrogGrandSlamThrowAngle(throwing[i].throwStart,state.center)-i*step;
            phaseX+=Mathf.Cos(phase);phaseY+=Mathf.Sin(phase);
        }
        float startAngle=Mathf.Atan2(phaseY,phaseX);
        for(int i=0;i<throwing.Count;i++)
        {
            var victim=throwing[i];
            float angle=startAngle+i*step;
            victim.throwDirection=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            float radius=CombatBodyRadius(victim.unit);
            float distance=Mathf.Max(state.width*.5f,Vector3.Distance(victim.throwStart,state.center))+TileSize*FrogGrandSlamRules.ThrowDistanceTiles;
            victim.throwGoal=victim.throwStart;
            for(int attempt=0;attempt<12;attempt++)
            {
                float reach=distance*(1f-attempt*.075f);
                Vector3 goal=FrogGrandSlamThrowEndpoint(victim.unit,victim.throwStart,state.center+victim.throwDirection*reach);
                bool clear=true;
                foreach(Unit other in units)
                {
                    if(other==victim.unit||!IsCombatActive(other))continue;
                    if((goal-Flat(other.model.transform.position)).sqrMagnitude<Mathf.Pow(radius+CombatBodyRadius(other)+.08f,2f)){clear=false;break;}
                }
                for(int previous=0;clear&&previous<i;previous++)
                    if((goal-throwing[previous].throwGoal).sqrMagnitude<Mathf.Pow(radius+CombatBodyRadius(throwing[previous].unit)+.08f,2f))clear=false;
                if(clear){victim.throwGoal=goal;break;}
            }
            victim.throwing=true;
        }
    }

    private static float FrogGrandSlamThrowAngle(Vector3 position,Vector3 center)=>Mathf.Atan2(position.z-center.z,position.x-center.x);

    private void MoveFrogGrandSlamThrow(FrogGrandSlamVictim victim,float time)
    {
        Unit unit=victim.unit;
        Vector3 ground=Flat(unit.model.transform.position);
        Vector3 desired=Vector3.Lerp(victim.throwStart,victim.throwGoal,FrogGrandSlamRules.ThrowProgressAt(time));
        float radius=CombatBodyRadius(unit);
        bool landing=FrogGrandSlamRules.HeightAt(time)<=radius;
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(ground,desired)/Mathf.Max(.04f,radius*.25f)));
        Vector3 delta=(desired-ground)/steps;
        for(int i=0;i<steps;i++)
        {
            Vector3 next=ground+delta;
            if(landing)next=Vector3.MoveTowards(ground,ResolveBodyMovement(unit,ground,next,false),delta.magnitude);
            if(!HasClearPathSegment(ground,next,radius)||IsHostileIceWallBlocking(unit,ground,next))break;
            ground=next;
            if(FrogGrandSlamLandingClear(unit,ground))victim.throwSafeGround=ground;
        }
        if(time>=FrogGrandSlamRules.HitTime&&!FrogGrandSlamLandingClear(unit,ground))
        {
            for(int i=0;i<=24;i++)
            {
                Vector3 safe=i==0?victim.throwSafeGround:Vector3.Lerp(ground,victim.throwStart,i/24f);
                if(!FrogGrandSlamLandingClear(unit,safe)||!HasClearPathSegment(ground,safe,radius)||IsHostileIceWallBlocking(unit,ground,safe))continue;
                ground=safe;break;
            }
        }
        MovePreyBody(unit,ground-Flat(unit.model.transform.position));
    }

    private bool FrogGrandSlamLandingClear(Unit unit,Vector3 ground)
    {
        foreach(Unit other in units)
            if(other!=unit&&IsCombatActive(other)&&(ground-Flat(other.model.transform.position)).sqrMagnitude<
               Mathf.Pow(CombatBodyRadius(unit)+CombatBodyRadius(other)-.001f,2f))return false;
        return true;
    }

    private Vector3 FrogGrandSlamThrowEndpoint(Unit unit,Vector3 start,Vector3 desired)
    {
        float radius=CombatBodyRadius(unit);
        Vector3 end=ResolveFreePosition(desired,radius),ground=start;
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(start,end)/Mathf.Max(.04f,radius*.25f)));
        for(int i=1;i<=steps;i++)
        {
            Vector3 next=Vector3.Lerp(start,end,i/(float)steps);
            if(!HasClearPathSegment(ground,next,radius)||IsHostileIceWallBlocking(unit,ground,next))break;
            ground=next;
        }
        return ground;
    }

    private static string FrogGrandSlamAction(float time,bool victim)
    {
        if(time<FrogGrandSlamRules.CastContact)return victim?"frog_fished_cast":"frog_grand_fishing_cast";
        if(time<FrogGrandSlamRules.PullEnd)return victim?"frog_fished_pull":"frog_grand_fishing_pull";
        if(time<FrogGrandSlamRules.LiftEnd)return victim?"frog_fished_lift":"frog_grand_fishing_lift";
        if(time<FrogGrandSlamRules.SlamStart)return victim?"frog_fished_hover":"frog_grand_fishing_hover";
        if(time<FrogGrandSlamRules.HitTime)return victim?"frog_fished_slam":"frog_grand_fishing_slam";
        return victim?"frog_fished_recover":"frog_grand_fishing_recover";
    }

    private void FrogGrandSlamDamage(Unit source,FrogGrandSlamState state,Unit enemy,float ratio)
    {
        float dealt=ApplySkillDamage(source,enemy,source.damage*ratio*state.damageScale*
            (enemy==state.primary?1f:FrogCombatRules.TranscendentSecondaryDamageRatio));
        state.dealt+=dealt;state.hits++;
    }

    private CombatStatusEffect ApplyFrogGrandSlamControl(Unit source,Unit enemy,CombatStatusKind kind,float remaining)
    {
        var effect=enemy.combatStatuses.ApplyTimed(kind,remaining,source,kind==CombatStatusKind.Airborne?remaining:0f);
        if(effect.source!=source||enemy.combatStatuses.Get(kind)!=effect||effect.stacks<=0)return null;
        // This scripted capture shares the cast clock; cleansing or replacement still releases it.
        effect.remaining=remaining;effect.activeHolds++;return effect;
    }

    private void CaptureFrogGrandSlamVictim(Unit source,FrogGrandSlamState state,FrogGrandSlamVictim victim)
    {
        Unit enemy=victim.unit;
        if(PreyDisplacementImmune(enemy)||enemy.itemOpeningImmunityTimer>0f)return;
        InterruptPositionalAction(enemy);
        if(enemy.fangLockTarget!=null)FinishFangLock(enemy,false);
        enemy.displacing=false;enemy.preyDraggedBy=null;
        victim.pullAirborne=enemy.combatStatuses.Get(CombatStatusKind.Airborne);
        victim.pullAirborneSource=victim.pullAirborne?.source;
        victim.previousRoot=enemy.combatStatuses.Get(CombatStatusKind.Root);
        if(victim.previousRoot!=null)enemy.combatStatuses.Remove(CombatStatusKind.Root);
        victim.root=ApplyFrogGrandSlamControl(source,enemy,CombatStatusKind.Root,FrogGrandSlamRules.HitTime-state.time);
        if(victim.root==null)return;
        victim.captured=true;victim.revision=enemy.positionRevision;victim.rootAt=battleElapsed;victim.rootCastAt=state.time;
        victim.start=Flat(enemy.model.transform.position);enemy.frogGrandSlamHeldBy=source;
        ClearRoute(enemy);StopUnitMovement(enemy);MovePreyBody(enemy,Vector3.zero);enemy.spriteAnimation?.SetFrozen(true);
    }

    private bool FrogGrandSlamControlValid(Unit source,FrogGrandSlamVictim victim)
    {
        Unit enemy=victim.unit;
        var airborne=enemy.combatStatuses.Get(CombatStatusKind.Airborne);
        bool controlled=victim.airborne!=null?airborne==victim.airborne&&airborne.source==source:
            airborne==null||airborne==victim.pullAirborne&&airborne.source==victim.pullAirborneSource;
        return IsCombatActive(source)&&IsCombatActive(enemy)&&!PreyDisplacementImmune(enemy)&&enemy.itemOpeningImmunityTimer<=0f&&
            enemy.positionRevision==victim.revision&&enemy.frogGrandSlamHeldBy==source&&
            victim.root!=null&&enemy.combatStatuses.Get(CombatStatusKind.Root)==victim.root&&victim.root.source==source&&
            controlled;
    }

    private bool UpdateFrogGrandSlamVictim(Unit enemy)
    {
        Unit source=enemy.frogGrandSlamHeldBy;var state=source?.frogGrandSlam;
        if(state==null){enemy.frogGrandSlamHeldBy=null;return false;}
        FrogGrandSlamVictim victim=null;
        foreach(var candidate in state.victims)if(candidate.unit==enemy){victim=candidate;break;}
        if(victim==null)return false;
        if(!FrogGrandSlamControlValid(source,victim)){ReleaseFrogGrandSlamVictim(source,victim);return false;}
        enemy.spriteAnimation?.SetFrozen(true);StopUnitMovement(enemy);enemy.currentAction=FrogGrandSlamAction(state.time,true);return true;
    }

    private void ReleaseFrogGrandSlamVictim(Unit source,FrogGrandSlamVictim victim)
    {
        if(!victim.captured)return;victim.captured=false;
        Unit enemy=victim.unit;
        if(enemy.frogGrandSlamHeldBy==source)enemy.frogGrandSlamHeldBy=null;
        enemy.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);
        RestoreFrogGrandSlamStatus(source,victim,CombatStatusKind.Root,victim.root,victim.previousRoot);
        RestoreFrogGrandSlamStatus(source,victim,CombatStatusKind.Airborne,victim.airborne,victim.previousAirborne);
        enemy.spriteAnimation?.SetFrozen(enemy.combatStatuses.Has(CombatStatusKind.Freeze)||SpiderSilkBound(enemy));
        enemy.decisionTimer=0f;
    }

    private void RestoreFrogGrandSlamStatus(Unit source,FrogGrandSlamVictim victim,CombatStatusKind kind,CombatStatusEffect owned,CombatStatusEffect previous)
    {
        if(owned==null)return;owned.activeHolds=Mathf.Max(0,owned.activeHolds-1);
        Unit enemy=victim.unit;
        if(enemy.combatStatuses.Get(kind)!=owned||owned.source!=source)return;
        enemy.combatStatuses.Remove(kind);
        if(previous==null||!IsCombatActive(enemy))return;
        var state=source.frogGrandSlam;
        float savedAt=kind==CombatStatusKind.Root?victim.rootAt:victim.airborneAt;
        float castAt=kind==CombatStatusKind.Root?victim.rootCastAt:victim.airborneCastAt;
        float remaining=previous.remaining-Mathf.Max(Mathf.Max(0f,battleElapsed-savedAt),state==null?0f:state.time-castAt);
        if(remaining<=0f)return;
        var restored=enemy.combatStatuses.ApplyTimed(kind,remaining,previous.source,previous.magnitude);
        if(restored.source==previous.source){restored.remaining=remaining;restored.magnitude=previous.magnitude;restored.activeHolds=previous.activeHolds;}
    }

    private void FinishFrogGrandSlamStatistics(Unit source,FrogGrandSlamState state)
    {
        if(state.generation!=combatGeneration){state.hits=0;state.dealt=0f;return;}
        if(state.hits<=0)return;
        source.skillHits+=state.hits;source.skillDamage+=state.dealt;RecordSkillEvent(source,state.skill.id,state.hits,state.dealt);
        if(state.generation==combatGeneration&&IsCombatActive(source)&&state.dealt>0f&&MonsterTraitRuntime.Has(source.activeTraitIds,FrogCombatRules.FroskTraitId))
        {
            Unit ally=units.Where(candidate=>IsCombatActive(candidate)&&!candidate.augmentShadowClone&&candidate.team==source.team&&candidate.hp<candidate.maxHp)
                .OrderBy(candidate=>candidate.hp/Mathf.Max(1f,candidate.maxHp)).FirstOrDefault();
            if(ally!=null){Heal(ally,FrogCombatRules.SoulHealing(state.dealt));source.traitActivations++;}
        }
        state.hits=0;state.dealt=0f;
    }

    private void EndFrogGrandSlam(Unit source,bool immediate=false)
    {
        var state=source?.frogGrandSlam;if(state==null)return;
        CancelFrogCatchCinematicFor(source);
        foreach(var victim in state.victims)ReleaseFrogGrandSlamVictim(source,victim);
        source.frogGrandSlam=null;
        // Scene teardown can destroy the effect before its combat owner is disabled.
        if(state.effect!=null)state.effect.Cancel(immediate);
        source.tongueHarpoonTarget=null;source.tongueWhip?.Cancel();
        source.attackMovementLockTimer=0f;source.decisionTimer=0f;source.spriteAnimation?.InterruptCurrentAction(true);
        FinishFrogGrandSlamStatistics(source,state);
        if(IsCombatActive(source))source.currentAction="idle";
    }

    private static void InterruptFrogGrandSlam(Unit unit)
    {
        if(unit==null)return;
        if(unit.frogGrandSlam!=null)unit.frogGrandSlam.owner.EndFrogGrandSlam(unit);
        Unit source=unit.frogGrandSlamHeldBy;var state=source?.frogGrandSlam;
        FrogGrandSlamVictim victim=null;
        if(state!=null)foreach(var candidate in state.victims)if(candidate.unit==unit){victim=candidate;break;}
        if(victim!=null)state.owner.ReleaseFrogGrandSlamVictim(source,victim);
    }
}
