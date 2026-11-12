using System.Collections.Generic;
using BombBrick.Combat;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class SabertoothDevourState
    {
        public MonsterAutoBattlePrototype owner;
        public float time,damageScale,width,height;
        public int generation,revision;
        public bool captured,hit,pullSound,cinematicOpen;
        public Vector3 forward,center;
        public PixelSabertoothDevour effect;
        public readonly List<Unit> targets=new List<Unit>();
        public readonly Dictionary<Unit,Vector3> plans=new Dictionary<Unit,Vector3>();
        public readonly Dictionary<Unit,float> holdUntil=new Dictionary<Unit,float>();
        public readonly Dictionary<Unit,DevourPriorRoot> previousRoots=new Dictionary<Unit,DevourPriorRoot>();
    }

    private struct DevourPriorRoot
    {
        public CombatStatusEffect effect,capture;
        public float battleTime,castTime;
    }

    private static bool UsesSabertoothDevour(Unit source)=>
        IsSabertoothFamily(source)&&HasFamilyTranscendence(source);

    private bool BeginSabertoothDevour(Unit source,SkillMasterRow skill)
    {
        if(!UsesSabertoothDevour(source)||!IsCombatActive(source)||
           source.sabertoothDevour!=null||source.sabertoothDevouredBy!=null||skill==null)return false;
        var state=new SabertoothDevourState {owner=this,generation=combatGeneration,
            revision=source.positionRevision,damageScale=source.skillTraitDamageMultiplier,
            cinematicOpen=source==sabertoothCinematicHandoffUnit};
        foreach(Unit enemy in units)
            if(enemy.team!=source.team&&IsCombatTargetable(enemy))state.targets.Add(enemy);
        if(state.targets.Count==0)return false;
        Vector3 origin=Flat(source.model.transform.position);
        state.targets.Sort((a,b)=>(Flat(a.model.transform.position)-origin).sqrMagnitude.CompareTo(
            (Flat(b.model.transform.position)-origin).sqrMagnitude));
        state.forward=IsCombatTargetable(source.target)?
            Flat(source.target.model.transform.position)-origin:DeploymentFacing(source.team);
        if(state.forward.sqrMagnitude<.0001f)state.forward=DeploymentFacing(source.team);
        state.forward.Normalize();
        PlanSabertoothDevour(source,state,origin);
        source.sabertoothDevour=state;
        source.skillTimer=source.skillCooldownDuration;
        source.skillUses++;source.traitActivations++;
        source.fangLockDamageTakenStart=source.damageTaken;
        source.fangLockTimer=SabertoothDevourRules.Duration;
        source.attackMovementLockTimer=SabertoothDevourRules.Duration;
        StopUnitMovement(source);ClearRoute(source);
        RotateUnit(source,state.forward,1f);
        source.currentAction="sabertooth_devour_inhale";
        PlaySabertoothSound(source,SabertoothSound.Start,source.model.transform.position);
        ApplyPostSkillItemEffects(source);
        if(source.spriteAnimation!=null)
            source.spriteAnimation.PlaySkillAttackWithHold(1,
                SabertoothDevourRules.HitTime*CombatStatFormula.CombatAnimationRate,1f,null,
                ()=>state.time);
        if(!mlEpisodeActive&&source.spriteAnimation!=null)
        {
            var victims=new List<Transform>(state.targets.Count);
            foreach(Unit enemy in state.targets)victims.Add(enemy.spriteAnimation!=null?
                enemy.spriteAnimation.transform:enemy.model.transform);
            state.effect=PixelSabertoothDevour.Create(Rank3Renderer(source),state.forward,
                state.center,state.width,state.height,victims);
            if(state.cinematicOpen)state.effect?.BeginCinematicHandoff(sabertoothCinematicHandoffWidth);
            state.effect?.Advance(state.cinematicOpen?SabertoothDevourRules.PullStart:0f,
                SabertoothDevourMouth(source,state),state.cinematicOpen?0f:-1f);
        }
        RecordSkillEvent(source,"sabertooth_grand_devour_start",0,0f);
        return true;
    }

    private void PlanSabertoothDevour(Unit source,SabertoothDevourState state,Vector3 origin)
    {
        float radius=.25f;
        foreach(Unit enemy in state.targets)radius=Mathf.Max(radius,CombatBodyRadius(enemy));
        float spacing=radius*2f+.12f;
        float sideSpacing=Mathf.Max(spacing,TileSize*1.25f);
        float standOff=CombatBodyRadius(source)+radius+.65f;
        Vector3 side=Vector3.Cross(Vector3.up,state.forward);
        int rows=state.targets.Count>1?2:1;
        Vector3 min=Vector3.positiveInfinity,max=Vector3.negativeInfinity;
        for(int i=0;i<state.targets.Count;i++)
        {
            Unit enemy=state.targets[i];
            Vector3 goal=origin+state.forward*(standOff+(i/rows)*spacing)+
                side*((i%rows)-(rows-1)*.5f)*sideSpacing;
            goal=ResolveFreePosition(goal,CombatBodyRadius(enemy));
            // These are destinations for this pull only, never combat position reservations.
            for(int attempt=0;attempt<12&&!DevourDestinationClear(source,state,enemy,goal);attempt++)
                goal=ResolveFreePosition(origin+state.forward*(standOff+(i/rows+1+attempt/4)*spacing)+
                    side*((attempt%4)-1.5f)*sideSpacing,CombatBodyRadius(enemy));
            if(!DevourDestinationClear(source,state,enemy,goal))goal=Flat(enemy.model.transform.position);
            state.plans[enemy]=goal;
            min=Vector3.Min(min,goal);max=Vector3.Max(max,goal);
        }
        state.center=(min+max)*.5f;
        state.width=Mathf.Max(TileSize*2.4f,(max-min).magnitude+radius*2f+.4f);
        state.height=Mathf.Max(TileSize*1.6f,sideSpacing+radius*2f+.6f);
    }

    private bool DevourDestinationClear(Unit source,SabertoothDevourState state,Unit enemy,Vector3 goal)
    {
        float radius=CombatBodyRadius(enemy);
        if(!HasClearPathSegment(goal,goal,radius))return false;
        foreach(var plan in state.plans)
            if((goal-plan.Value).sqrMagnitude<Mathf.Pow(radius+CombatBodyRadius(plan.Key)+.08f,2f))return false;
        foreach(Unit other in units)
        {
            if(other==enemy||!IsCombatActive(other)||other!=source&&state.targets.Contains(other))continue;
            if((goal-Flat(other.model.transform.position)).sqrMagnitude<
               Mathf.Pow(radius+CombatBodyRadius(other)+.08f,2f))return false;
        }
        return true;
    }

    private Vector3 SabertoothDevourMouth(Unit source,SabertoothDevourState state)
    {
        if(source.spriteAnimation==null)return source.model.transform.position+Vector3.up*.45f;
        var renderer=Rank3Renderer(source);
        return renderer.bounds.center+state.forward*Mathf.Max(.2f,renderer.bounds.size.x*.32f);
    }

    private void CaptureSabertoothDevour(Unit source,SabertoothDevourState state)
    {
        state.captured=true;
        if(state.time<SabertoothDevourRules.PullEnd)
        {PlaySabertoothSound(source,SabertoothSound.DevourInhale,source.model.transform.position,.65f);state.pullSound=true;}
        foreach(Unit enemy in state.targets)
        {
            if(!IsCombatTargetable(enemy)||enemy.sabertoothDevouredBy!=null||PreyDisplacementImmune(enemy))continue;
            float duration=HostileStatusDuration(enemy,SabertoothDevourRules.HitTime-SabertoothDevourRules.PullStart);
            var previousRoot=enemy.combatStatuses.Get(CombatStatusKind.Root);
            if(previousRoot!=null)
            {
                state.previousRoots[enemy]=new DevourPriorRoot {
                    effect=previousRoot,battleTime=battleElapsed,castTime=state.time};
                enemy.combatStatuses.Remove(CombatStatusKind.Root);
            }
            var root=enemy.combatStatuses.ApplyTimed(CombatStatusKind.Root,duration,source);
            if(root==null||root.source!=source)continue;
            if(state.previousRoots.TryGetValue(enemy,out DevourPriorRoot saved))
            {saved.capture=root;state.previousRoots[enemy]=saved;}
            if(enemy.sabertoothDevour!=null)EndSabertoothDevour(enemy);
            InterruptPositionalAction(enemy);
            if(enemy.fangLockTarget!=null)FinishFangLock(enemy,false);
            var presentation=SkillPresentationFor(enemy);
            if(presentation!=null&&presentation.pending==null)ClearLocalSkillPresentation(presentation);
            enemy.displacing=false;enemy.preyDraggedBy=null;
            ClearRoute(enemy);StopUnitMovement(enemy);
            enemy.sabertoothDevouredBy=source;
            state.holdUntil[enemy]=SabertoothDevourRules.PullStart+root.remaining;
            enemy.spriteAnimation?.SetFrozen(true);
        }
    }

    private void AdvanceSabertoothDevour(Unit source,float dt)
    {
        var state=source.sabertoothDevour;
        if(state==null)return;
        if(battleEnding||!IsCombatActive(source)||source.hp<=0f||state.generation!=combatGeneration||
           state.revision!=source.positionRevision||source.sabertoothDevouredBy!=null||
           source.combatStatuses.Has(CombatStatusKind.Knockdown)||source.combatStatuses.Has(CombatStatusKind.Fear)||
           source.combatStatuses.Has(CombatStatusKind.Airborne)||SpiderSilkBound(source)||source.giraffeStunTimer>0f)
        {EndSabertoothDevour(source);return;}
        if(paused||dt<=0f||source.combatStatuses.Has(CombatStatusKind.Freeze))return;
        float previous=state.time;
        state.time=Mathf.Min(SabertoothDevourRules.Duration,state.time+dt);
        if(!state.captured&&state.time>=SabertoothDevourRules.PullStart)CaptureSabertoothDevour(source,state);
        if(!state.hit&&state.captured)PullSabertoothDevour(source,state,previous);
        if(state.pullSound&&state.time>=SabertoothDevourRules.PullEnd)
        {StopSabertoothDragSound(source);state.pullSound=false;}
        // The stage already opened the jaw; only its visual reveal is carried into the live cast.
        state.effect?.Advance(state.cinematicOpen?Mathf.Max(SabertoothDevourRules.PullStart,state.time):state.time,
            SabertoothDevourMouth(source,state),state.cinematicOpen?state.time:-1f);
        source.currentAction=state.time<SabertoothDevourRules.PullStart?"sabertooth_devour_inhale":
            state.time<SabertoothDevourRules.PullEnd?"sabertooth_devour_pull":
            state.time<SabertoothDevourRules.SnapStart?"sabertooth_devour_hold":"sabertooth_devour_bite";
        if(!state.hit&&state.time>=SabertoothDevourRules.HitTime)
        {
            state.hit=true;ReleaseSabertoothDevourVictims(source,state);
            float damage=0f;int hits=0;
            PlaySabertoothSound(source,SabertoothSound.DevourSnap,state.center,1.1f);
            foreach(Unit enemy in state.targets)
            {
                if(!IsCombatActive(source)||!IsCombatTargetable(enemy)||enemy.team==source.team)continue;
                bool execution=MonsterTraitRuntime.Has(source.activeTraitIds,SabertoothCombatRules.NoxTraitId)&&
                    enemy.hp/Mathf.Max(1f,enemy.maxHp)<=SabertoothCombatRules.ExecutionHealthThreshold;
                float dealt=ApplySkillDamage(source,enemy,new SkillDamageFormulaResult(
                    SkillPower(source)*SabertoothDevourRules.DamageRatio*state.damageScale,0f,
                    execution?SabertoothCombatRules.ExecutionDefenseIgnoreRatio:0f),
                    applyTraitMultiplier:false,allowHitSlide:false);
                damage+=dealt;hits++;source.skillHits++;source.skillDamage+=dealt;
                if(execution)source.traitActivations++;
                ApplySeolaFrostFang(source,enemy);
                enemy.spriteAnimation?.PlayImpactRecoil(enemy.model.transform.position-source.model.transform.position,.16f);
                if(!mlEpisodeActive&&enemy.spriteAnimation!=null&&
                   Vector3.Distance(Flat(enemy.model.transform.position),state.center)>state.width*.55f)
                    PixelCombatEffect.PlaySabertoothFangLock(enemy.spriteAnimation.transform,Rank3Renderer(enemy),false,1.25f);
            }
            if(!mlEpisodeActive)combatCameraDirector?.ReportSabertoothDevourBite();
            RecordSkillEvent(source,"sabertooth_grand_devour",hits,damage);
        }
        if(state.time>=SabertoothDevourRules.Duration)EndSabertoothDevour(source);
    }

    private void PullSabertoothDevour(Unit source,SabertoothDevourState state,float previous)
    {
        float before=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(SabertoothDevourRules.PullStart,SabertoothDevourRules.PullEnd,previous));
        float after=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(SabertoothDevourRules.PullStart,SabertoothDevourRules.PullEnd,state.time));
        foreach(Unit enemy in state.targets)
        {
            if(enemy.sabertoothDevouredBy!=source)continue;
            var root=enemy.combatStatuses.Get(CombatStatusKind.Root);
            if(!IsCombatActive(enemy)||PreyDisplacementImmune(enemy)||root==null||root.source!=source||
               !state.holdUntil.TryGetValue(enemy,out float until)||state.time>=until)
            {ReleaseSabertoothDevourVictim(source,enemy);continue;}
            Vector3 ground=Flat(enemy.model.transform.position),goal=state.plans[enemy];
            Vector3 movement=(goal-ground)*Mathf.Clamp01((after-before)/Mathf.Max(.00001f,1f-before));
            int steps=Mathf.Max(1,Mathf.CeilToInt(movement.magnitude/Mathf.Max(.04f,CombatBodyRadius(enemy)*.25f)));
            Vector3 step=movement/steps;
            for(int i=0;i<steps;i++)
            {
                Vector3 candidate=ResolveBodyMovement(enemy,ground,ground+step,false);
                candidate=Vector3.MoveTowards(ground,candidate,step.magnitude);
                if(Vector3.Dot(candidate-ground,step)<=0f||
                   !HasClearPathSegment(ground,candidate,CombatBodyRadius(enemy))||
                   IsHostileIceWallBlocking(enemy,ground,candidate))break;
                ground=candidate;
            }
            MovePreyBody(enemy,ground-Flat(enemy.model.transform.position));
            enemy.currentAction=state.time<SabertoothDevourRules.PullEnd
                ?"sabertooth_devoured_pull":"sabertooth_devoured_hold";
        }
    }

    private void ReleaseSabertoothDevourVictim(Unit source,Unit enemy)
    {
        if(enemy.sabertoothDevouredBy!=source)return;
        enemy.sabertoothDevouredBy=null;
        var root=enemy.combatStatuses.Get(CombatStatusKind.Root);
        var state=source.sabertoothDevour;
        DevourPriorRoot previous=default;
        bool prior=state!=null&&state.previousRoots.TryGetValue(enemy,out previous);
        bool expiredCapture=root==null&&prior&&previous.capture!=null&&previous.capture.remaining<=0f;
        if(root!=null&&root.source==source||expiredCapture)
        {
            enemy.combatStatuses.Remove(CombatStatusKind.Root);
            if(prior&&IsCombatActive(enemy))
            {
                float remaining=previous.effect.remaining-Mathf.Max(
                    battleElapsed-previous.battleTime,state.time-previous.castTime);
                if(remaining>0f)
                {
                    var restored=enemy.combatStatuses.ApplyTimed(CombatStatusKind.Root,remaining,
                        previous.effect.source,previous.effect.magnitude);
                    // The saved duration already includes resistance and source multipliers.
                    if(restored.source==previous.effect.source)
                    {restored.remaining=remaining;restored.activeHolds=previous.effect.activeHolds;}
                }
            }
        }
        if(IsCombatActive(enemy))enemy.spriteAnimation?.SetFrozen(enemy.combatStatuses.Has(CombatStatusKind.Freeze)||SpiderSilkBound(enemy));
        enemy.decisionTimer=0f;
    }

    private void ReleaseSabertoothDevourVictims(Unit source,SabertoothDevourState state)
    {
        foreach(Unit enemy in state.targets)ReleaseSabertoothDevourVictim(source,enemy);
        state.holdUntil.Clear();
    }

    private void EndSabertoothDevour(Unit source,bool immediate=false)
    {
        var state=source?.sabertoothDevour;
        if(state==null)return;
        ReleaseSabertoothDevourVictims(source,state);
        source.sabertoothDevour=null;
        StopSabertoothDragSound(source);
        state.effect?.Cancel(immediate);
        source.attackMovementLockTimer=0f;source.decisionTimer=0f;
        source.spriteAnimation?.SetSkillPoseScale(Vector3.one);
        source.spriteAnimation?.InterruptCurrentAction(true);
        FinishFangLock(source,state.hit&&IsCombatActive(source));
        if(IsCombatActive(source))source.currentAction="idle";
    }

    private void ResolveSabertoothDevourEcho(Unit source,float scale)
    {
        float damage=0f;int hits=0;
        foreach(Unit enemy in units)
        {
            if(enemy.team==source.team||!IsCombatTargetable(enemy))continue;
            float dealt=ApplySkillDamage(source,enemy,
                SkillPower(source)*SabertoothDevourRules.DamageRatio*scale,false);
            damage+=dealt;hits++;source.skillHits++;source.skillDamage+=dealt;
            if(!mlEpisodeActive&&enemy.spriteAnimation!=null)
                PixelCombatEffect.PlaySabertoothFangLock(enemy.spriteAnimation.transform,Rank3Renderer(enemy),false,1.1f);
        }
        RecordSkillEvent(source,"sabertooth_grand_devour_echo",hits,damage);
    }
}
