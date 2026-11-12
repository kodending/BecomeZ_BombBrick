using System.Collections.Generic;
using BombBrick.Combat;
using UnityEngine;

public static class WizardCygnusShowerRules
{
    public const float Preparation=.35f,DamageRatio=.9f,Duration=1.6f;
    public const float FlightSeconds=.38f,FirstImpact=.38f,WaveInterval=.34f;
    public const int WaveCount=3;
    public static float HitTime(int wave,int target)=>FirstImpact+WaveInterval*wave+.025f*target;
    public static float DurationForTargets(int count)=>Mathf.Max(Duration,HitTime(WaveCount-1,Mathf.Max(0,count-1))+.35f);
    public static int ShotCount(int count)=>Mathf.CeilToInt(Mathf.Max(0,count)*WaveCount*1.5f);
    public static float ImpactTime(int index,int count)
    {
        if(count<=0)return FirstImpact;
        int total=ShotCount(count),previousTotal=count*WaveCount;
        float position=Mathf.Clamp(index,0,total-1)*(previousTotal-1f)/Mathf.Max(1,total-1);
        int before=Mathf.FloorToInt(position),after=Mathf.Min(previousTotal-1,before+1);
        return Mathf.Lerp(HitTime(before/count,before%count),HitTime(after/count,after%count),position-before);
    }
}

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class WizardCygnusShowerState
    {
        public MonsterAutoBattlePrototype owner;
        public Unit source;
        public Unit[] targets;
        public Vector3[] positions;
        public bool[] impacts;
        public int generation,revision,nextImpact;
        public float time,poseTime,damageScale,attack,damage,nextHitSoundAt;
        public bool released,cancelled,echo,finalShakePlayed;
        public PixelWizardCygnusMeteorShower effect;
    }

    private readonly List<WizardCygnusShowerState> wizardCygnusShowers=new List<WizardCygnusShowerState>();

    private static bool UsesWizardCygnusShower(Unit source)=>IsWizardMouseFamily(source)&&HasFamilyTranscendence(source);

    private bool BeginWizardCygnusShower(Unit source,SkillMasterRow skill)
    {
        if(!UsesWizardCygnusShower(source)||!IsCombatActive(source)||source.hp<=0f||skill==null||
           source.wizardCygnusShower!=null||battleEnding)return false;
        Unit primary=CommittedSkillTarget(source)??source.target;
        if(!IsCombatTargetable(primary)||primary.team==source.team)return false;
        float reach=TileSize*Mathf.Max(1f,skill.castRangeTiles)+source.collisionRadius+primary.collisionRadius;
        if(!IsWithinCommittedSkillReach(source,primary,reach))return false;

        var targets=new List<Unit>();
        foreach(Unit enemy in units)
            if(enemy.team!=source.team&&IsCombatTargetable(enemy)&&enemy.hp>0f&&!enemy.crocodilePreyFlying)
                targets.Add(enemy);
        if(targets.Count==0)return false;
        var state=new WizardCygnusShowerState{owner=this,source=source,targets=targets.ToArray(),
            generation=combatGeneration,revision=source.positionRevision,
            attack=source.damage,damageScale=source.skillTraitDamageMultiplier};
        state.positions=new Vector3[state.targets.Length];
        state.impacts=new bool[WizardCygnusShowerRules.ShotCount(state.targets.Length)];
        for(int i=0;i<state.targets.Length;i++)state.positions[i]=Flat(state.targets[i].model.transform.position);
        source.wizardCygnusShower=state;wizardCygnusShowers.Add(state);
        source.skillTimer=source.skillCooldownDuration;source.skillUses++;source.traitActivations++;
        source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,
            WizardCygnusShowerRules.Preparation+WizardCygnusShowerRules.DurationForTargets(state.targets.Length));
        ClearUnitInertia(source);ClearRoute(source);StopUnitMovement(source);
        Vector3 direction=primary.model.transform.position-source.model.transform.position;
        RotateUnit(source,direction,1f);source.spriteBillboard?.SetFacingDirection(direction,true);
        source.currentAction="wizard_cygnus_prepare";
        ApplyPostSkillItemEffects(source);
        RecordSkillEvent(source,"wizard_mouse_cygnus_shower_start",0,0f);
        if(!mlEpisodeActive)
        {
            PlayFamilyTranscendenceFeedback(source,"wizard_mouse");
            PlayWizardSound(source,WizardSound.Prepare,source.model.transform.position,.7f);
            source.spriteAnimation?.PlaySkillAttackWithHold(1,
                WizardCygnusShowerRules.Preparation*CombatStatFormula.CombatAnimationRate,1f,null,()=>state.poseTime);
        }
        QueueWizardCygnusCinematic(source,state);
        return true;
    }

    private void UpdateWizardCygnusShowers(float dt)
    {
        if(paused||dt<=0f)return;
        for(int index=wizardCygnusShowers.Count-1;index>=0;index--)
        {
            if(index>=wizardCygnusShowers.Count)continue;
            var state=wizardCygnusShowers[index];
            if(state.cancelled){wizardCygnusShowers.RemoveAt(index);continue;}
            if(state.generation!=combatGeneration||battleEnding)
            {StopWizardCygnusShower(state,!battleEnding);wizardCygnusShowers.RemoveAt(index);continue;}
            Unit source=state.source;
            float step=dt;
            if(!state.released)
            {
                if(!WizardCygnusPreparationValid(state))
                {StopWizardCygnusShower(state,true);wizardCygnusShowers.RemoveAt(index);continue;}
                if(IsWizardCygnusCinematicPending(source)||source.combatStatuses.Has(CombatStatusKind.Freeze))continue;
                state.time+=dt;state.poseTime+=dt;
                if(state.time<WizardCygnusShowerRules.Preparation)continue;
                step=Mathf.Max(0f,state.time-WizardCygnusShowerRules.Preparation);
                state.poseTime=WizardCygnusShowerRules.Preparation;
                ReleaseWizardCygnusShower(source);
                if(!state.released||step<=0f)continue;
            }
            state.time+=step;state.poseTime+=step;
            if(source.wizardCygnusShower==state&&IsCombatActive(source))
                source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,
                    WizardCygnusShowerRules.DurationForTargets(state.targets.Length)-state.time);
            int total=state.impacts.Length;
            for(int impact=0;impact<total&&!state.cancelled&&!battleEnding&&state.generation==combatGeneration;impact++)
            {
                if(state.impacts[impact])continue;
                int targetIndex=impact%state.targets.Length;
                float contact=WizardCygnusShowerRules.ImpactTime(impact,state.targets.Length);
                if(state.time<contact-WizardCygnusShowerRules.FlightSeconds)continue;
                Unit target=state.targets[targetIndex];
                if(IsCombatTargetable(target)&&target.team!=source.team)
                {
                    SpriteRenderer meteor=state.effect==null?null:state.effect.ProjectileRenderer(impact);
                    Vector3 end=WizardCygnusTargetPosition(state,targetIndex);
                    Vector3 from=meteor==null?end+Vector3.up*12f:meteor.transform.position;
                    if(AbsorbGriffinRangedShot(source,from,end,meteor,meteor==null?new Color(.76f,.96f,1f):meteor.color,out _))
                    {
                        state.impacts[impact]=true;state.nextImpact++;
                        state.effect?.AbsorbProjectile(impact);continue;
                    }
                }
                if(state.time<contact)continue;
                state.impacts[impact]=true;state.nextImpact++;
                if(!IsCombatTargetable(target)||target.hp<=0f||target.team==source.team||target.crocodilePreyFlying)continue;
                float dealt=ApplySkillDamage(source,target,new SkillDamageFormulaResult(
                    state.attack*WizardCygnusShowerRules.DamageRatio*state.damageScale,0f,0f),false,
                    applyTraitMultiplier:false,allowHitSlide:false,bypassProjectileWalls:true);
                source.skillHits++;source.skillDamage+=dealt;state.damage+=dealt;
                RecordSkillEvent(source,state.echo?"wizard_mouse_cygnus_shower_echo":"wizard_mouse_cygnus_shower",1,dealt);
                if(!mlEpisodeActive)
                {
                    if(impact==total-1&&!state.finalShakePlayed)
                    {
                        state.finalShakePlayed=true;
                        if(!state.echo)combatCameraDirector?.ReportGroundSlam();
                        PlayWizardSound(source,WizardSound.MeteorFinish,state.positions[targetIndex],.9f);
                    }
                    else if(state.time>=state.nextHitSoundAt)
                    {
                        state.nextHitSoundAt=state.time+.08f;
                        PlayWizardSound(source,WizardSound.MeteorHit,state.positions[targetIndex],.65f);
                    }
                }
            }
            if(state.effect!=null)state.effect.Advance(state.time);
            if(state.cancelled||battleEnding||state.generation!=combatGeneration)continue;
            if(state.time<WizardCygnusShowerRules.DurationForTargets(state.targets.Length))continue;
            StopWizardCygnusShower(state,true);wizardCygnusShowers.RemoveAt(index);
        }
    }

    private bool WizardCygnusPreparationValid(WizardCygnusShowerState state)
    {
        Unit source=state.source;
        return IsCombatActive(source)&&source.hp>0f&&source.wizardCygnusShower==state&&
            source.positionRevision==state.revision&&HasFamilyTranscendence(source)&&
            source.sabertoothDevouredBy==null&&source.frogGrandSlamHeldBy==null&&source.crocodileAbyssHeldBy==null&&
            !source.combatStatuses.Has(CombatStatusKind.Knockdown)&&!source.combatStatuses.Has(CombatStatusKind.Fear)&&
            !source.combatStatuses.Has(CombatStatusKind.Airborne)&&!SpiderSilkBound(source)&&source.giraffeStunTimer<=0f;
    }

    private void ReleaseWizardCygnusShower(Unit source)
    {
        var state=source?.wizardCygnusShower;
        if(state==null||state.cancelled||state.released||state.generation!=combatGeneration||battleEnding||
           !WizardCygnusPreparationValid(state)||source.combatStatuses.Has(CombatStatusKind.Freeze))return;
        state.released=true;state.time=0f;state.poseTime=Mathf.Max(state.poseTime,WizardCygnusShowerRules.Preparation);
        source.currentAction="wizard_cygnus_meteor_shower";
        CreateWizardCygnusShowerVisual(state);
        if(!mlEpisodeActive)PlayWizardSound(source,WizardSound.MeteorRelease,source.model.transform.position,.8f);
    }

    private void CreateWizardCygnusShowerVisual(WizardCygnusShowerState state)
    {
        if(mlEpisodeActive)return;
        Unit source=state.source;
        Vector3 origin=source.model==null?state.positions[0]:Flat(source.model.transform.position);
        state.effect=PixelWizardCygnusMeteorShower.Create(origin,state.positions,Rank3Renderer(source),
            index=>WizardCygnusTargetPosition(state,index));
        state.effect?.Advance(0f);
    }

    private Vector3 WizardCygnusTargetPosition(WizardCygnusShowerState state,int index)
    {
        Unit target=state.targets[index];
        if(IsCombatActive(target)&&target.hp>0f)state.positions[index]=Flat(target.model.transform.position);
        return state.positions[index];
    }

    private void ResolveWizardCygnusShowerEcho(Unit source,Unit target,float scale)
    {
        if(!UsesWizardCygnusShower(source)||!IsCombatActive(source)||!IsCombatTargetable(target)||
           source.team==target.team||battleEnding||scale<=0f)return;
        var state=new WizardCygnusShowerState{owner=this,source=source,targets=new[]{target},
            positions=new[]{Flat(target.model.transform.position)},impacts=new bool[WizardCygnusShowerRules.ShotCount(1)],generation=combatGeneration,
            revision=source.positionRevision,attack=source.damage,damageScale=scale,released=true,echo=true};
        wizardCygnusShowers.Add(state);CreateWizardCygnusShowerVisual(state);
    }

    private void CancelWizardCygnusShowerFor(Unit source)
    {
        var state=source?.wizardCygnusShower;if(state==null)return;
        if(!state.released)
        {
            state.cancelled=true;state.effect?.Cancel();state.effect=null;
            CancelWizardCygnusCinematicFor(source);
        }
        ReleaseWizardCygnusCasterPose(state);
    }

    private void ReleaseWizardCygnusCasterPose(WizardCygnusShowerState state)
    {
        // A frozen driver must also be able to finish its held pose when it thaws later.
        state.poseTime=WizardCygnusShowerRules.Preparation+WizardCygnusShowerRules.DurationForTargets(state.targets.Length)+2f;
        Unit source=state.source;if(source.wizardCygnusShower!=state)return;
        source.wizardCygnusShower=null;source.attackMovementLockTimer=0f;source.decisionTimer=0f;
        source.spriteAnimation?.InterruptCurrentAction(true);
        if(IsCombatActive(source))source.currentAction="idle";
    }

    private void StopWizardCygnusShower(WizardCygnusShowerState state,bool immediate)
    {
        state.cancelled=true;
        if(state.source.wizardCygnusShower==state)CancelWizardCygnusCinematicFor(state.source);
        ReleaseWizardCygnusCasterPose(state);
        if(state.effect==null)return;
        if(immediate)state.effect.Cancel();
        else
        {
            state.effect.FreezeTargets();
            CombatEffectEndPlayback.Begin(state.effect.gameObject,EndingEffectTypes);
        }
        state.effect=null;
    }

    private void ClearWizardCygnusShowers(bool immediate=true)
    {
        foreach(var state in wizardCygnusShowers)StopWizardCygnusShower(state,immediate);
        wizardCygnusShowers.Clear();
    }
}
