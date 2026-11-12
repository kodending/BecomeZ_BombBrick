using System.Collections.Generic;
using BombBrick.Combat;
using UnityEngine;

public static class WolfStampedeRules
{
    public const float Preparation=.35f,Duration=2f,DamageRatio=1.2f;
    public const int WaveCount=3;
    public static float HitTime(int wave,float progress)=>.16f+Mathf.Clamp01(progress)*.92f+wave*.28f;
    public static float Front(float time,int wave)=>(time-.16f-wave*.28f)/.92f;
}

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class WolfStampedeState
    {
        public MonsterAutoBattlePrototype owner;
        public Unit source,bodyTarget;
        public Unit[] targets;
        public bool[] impacts;
        public int generation,revision,soundWave=-1;
        public float attack,damageScale,time,poseTime,damage;
        public bool released,cancelled,echo,bodyHuntStarted;
        public Vector3 direction;
        public Bounds bounds;
        public PixelWolfStampede effect;
    }
    private readonly List<WolfStampedeState> wolfStampedes=new List<WolfStampedeState>();
    private static bool UsesWolfStampede(Unit unit)=>IsWolfFamily(unit)&&HasFamilyTranscendence(unit);

    private bool BeginWolfStampede(Unit source,SkillMasterRow skill)
    {
        if(!UsesWolfStampede(source)||!IsCombatActive(source)||source.hp<=0f||skill==null||
           source.wolfStampede!=null||battleEnding)return false;
        bool enemyPresent=false;
        foreach(Unit enemy in units)if(enemy.team!=source.team&&IsCombatTargetable(enemy)&&enemy.hp>0f)
        {enemyPresent=true;break;}
        if(!enemyPresent)return false;
        Vector3 right=mainCamera==null?Vector3.right:Flat(mainCamera.transform.right).normalized;
        if(right.sqrMagnitude<.01f)right=Vector3.right;
        var state=new WolfStampedeState{owner=this,source=source,bodyTarget=CommittedSkillTarget(source)??source.target,generation=combatGeneration,
            revision=source.positionRevision,attack=source.damage,damageScale=source.skillTraitDamageMultiplier,
            bounds=combatBounds,direction=source.team==Team.Player?right:-right};
        source.wolfStampede=state;wolfStampedes.Add(state);
        source.skillTimer=source.skillCooldownDuration;source.skillUses++;source.traitActivations++;
        source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,WolfStampedeRules.Preparation+WolfStampedeRules.Duration);
        CancelWolfHuntAfterimages(source);ClearUnitInertia(source);ClearRoute(source);StopUnitMovement(source);
        source.currentAction="wolf_stampede_howl";
        ApplyPostSkillItemEffects(source);RecordSkillEvent(source,"wolf_full_moon_stampede_start",0,0f);
        if(!mlEpisodeActive)
        {
            PlayFamilyTranscendenceFeedback(source,"wolf");
            source.spriteAnimation?.PlaySkillAttackWithHold(1,WolfStampedeRules.Preparation*CombatStatFormula.CombatAnimationRate,
                1f,null,()=>state.poseTime);
        }
        QueueWolfStampedeCinematic(source,state);
        return true;
    }

    private bool WolfStampedePreparationValid(WolfStampedeState state)
    {
        Unit source=state.source;
        return IsCombatActive(source)&&source.hp>0f&&source.wolfStampede==state&&UsesWolfStampede(source)&&
            source.positionRevision==state.revision&&source.sabertoothDevouredBy==null&&source.frogGrandSlamHeldBy==null&&
            source.crocodileAbyssHeldBy==null&&!source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
            !source.combatStatuses.Has(CombatStatusKind.Fear)&&!source.combatStatuses.Has(CombatStatusKind.Airborne)&&
            !SpiderSilkBound(source)&&source.giraffeStunTimer<=0f;
    }

    private void ReleaseWolfStampede(Unit source)
    {
        var state=source?.wolfStampede;
        if(state==null||state.released||state.cancelled||state.generation!=combatGeneration||battleEnding||
           !WolfStampedePreparationValid(state)||source.combatStatuses.Has(CombatStatusKind.Freeze))return;
        var targets=new List<Unit>();
        foreach(Unit enemy in units)
            if(enemy.team!=source.team&&IsCombatTargetable(enemy)&&enemy.hp>0f)targets.Add(enemy);
        state.targets=targets.ToArray();state.impacts=new bool[state.targets.Length*WolfStampedeRules.WaveCount];
        state.released=true;state.time=0f;state.poseTime=WolfStampedeRules.Preparation;
        source.currentAction="wolf_full_moon_stampede";
        if(!mlEpisodeActive)
        {
            state.effect=PixelWolfStampede.Create(state.bounds,state.direction,Rank3Renderer(source));
            PlayWolfSound(source,WolfSound.PackPass,source.model.transform.position,.8f);
        }
    }

    private void BeginWolfStampedeCasterHunt(WolfStampedeState state)
    {
        Unit source=state.source,target=state.bodyTarget;
        if(state.echo||state.bodyHuntStarted||source.wolfStampede!=state||
           wolfStampedeCinematic?.request.cast==state)return;
        state.bodyHuntStarted=true;
        if(!WolfStampedePreparationValid(state)||!IsCombatActive(target)||target.team==source.team)return;
        source.spriteAnimation?.InterruptCurrentAction(true);
        SetTarget(source,target);
        PendingSkillFocus previous=committingSkill;
        float multiplier=source.skillTraitDamageMultiplier;
        try
        {
            // The committed original hunt owns its dash, contact damage and far-side landing.
            committingSkill=new PendingSkillFocus{unit=source,castTarget=target};
            source.skillTraitDamageMultiplier=state.damageScale;
            if(BeginThreeSidedHuntBody(source,GameMasterData.Skill(source.skillId),false))
                PlayExecutedSkillAction(source);
        }
        finally{committingSkill=previous;source.skillTraitDamageMultiplier=multiplier;}
    }

    private void UpdateWolfStampedes(float dt)
    {
        if(paused||dt<=0f)return;
        for(int index=wolfStampedes.Count-1;index>=0;index--)
        {
            if(index>=wolfStampedes.Count)continue;
            var state=wolfStampedes[index];
            if(state.cancelled){wolfStampedes.RemoveAt(index);continue;}
            if(state.generation!=combatGeneration||battleEnding)
            {StopWolfStampede(state,!battleEnding);wolfStampedes.RemoveAt(index);continue;}
            float step=dt;
            if(!state.released)
            {
                if(!WolfStampedePreparationValid(state))
                {StopWolfStampede(state,true);wolfStampedes.RemoveAt(index);continue;}
                if(IsWolfStampedeCinematicPending(state.source)||state.source.combatStatuses.Has(CombatStatusKind.Freeze))continue;
                state.time+=dt;state.poseTime+=dt;
                if(state.time<WolfStampedeRules.Preparation)continue;
                step=Mathf.Max(0f,state.time-WolfStampedeRules.Preparation);
                ReleaseWolfStampede(state.source);
                if(!state.released||step<=0f)continue;
            }
            AdvanceWolfStampede(state,step);
            if(state.cancelled||state.generation!=combatGeneration||battleEnding)continue;
            if(state.time<WolfStampedeRules.Duration)continue;
            StopWolfStampede(state,true);wolfStampedes.RemoveAt(index);
        }
    }

    private void AdvanceWolfStampede(WolfStampedeState state,float dt)
    {
        if(!state.released||state.cancelled||state.generation!=combatGeneration||battleEnding)return;
        float step=Mathf.Max(0f,dt);state.time+=step;
        // The pack leads the wipe; the body and its animation begin once the stage is gone.
        if(wolfStampedeCinematic?.request.cast!=state)state.poseTime+=step;
        Unit source=state.source;
        if(source.wolfStampede==state&&IsCombatActive(source))
            source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,WolfStampedeRules.Duration-state.time);
        if(step>0f)BeginWolfStampedeCasterHunt(state);
        state.effect?.Advance(state.time);
        for(int wave=0;wave<WolfStampedeRules.WaveCount;wave++)
        for(int targetIndex=0;targetIndex<state.targets.Length;targetIndex++)
        {
            if(state.cancelled||battleEnding||state.generation!=combatGeneration)return;
            int index=wave*state.targets.Length+targetIndex;
            if(state.impacts[index])continue;
            Unit target=state.targets[targetIndex];
            if(target==null||target.model==null){state.impacts[index]=true;continue;}
            float half=PixelWolfStampede.HalfExtent(state.bounds,state.direction);
            float progress=(Vector3.Dot(Flat(target.model.transform.position)-Flat(state.bounds.center),state.direction)+half)/(half*2f);
            if(state.time<WolfStampedeRules.HitTime(wave,progress))continue;
            state.impacts[index]=true;
            if(!IsCombatTargetable(target)||target.hp<=0f||target.team==source.team)continue;
            float dealt=ApplySkillDamage(source,target,new SkillDamageFormulaResult(
                state.attack*WolfStampedeRules.DamageRatio*state.damageScale,0f,0f),false,
                applyTraitMultiplier:false,allowHitSlide:false);
            source.skillHits++;source.skillDamage+=dealt;state.damage+=dealt;
            RecordSkillEvent(source,state.echo?"wolf_full_moon_stampede_echo":"wolf_full_moon_stampede",1,dealt);
            if(mlEpisodeActive)continue;
            if(target.spriteAnimation!=null)
            {
                PixelCombatEffect.PlayWolfHuntImpact(target.spriteAnimation.transform,Rank3Renderer(target),state.direction,
                    new Color(.75f,.9f,1f),wave==WolfStampedeRules.WaveCount-1);
                target.spriteAnimation.PlayImpactRecoil(state.direction,.075f);
            }
            if(state.soundWave<wave)
            {
                state.soundWave=wave;PlayWolfSound(source,WolfSound.PackHit,target.model.transform.position,.7f);
                if(wave==WolfStampedeRules.WaveCount-1&&!state.echo)combatCameraDirector?.ReportGroundSlam(true);
            }
        }
    }

    private void ResolveWolfStampedeEcho(Unit source,float scale)
    {
        if(!UsesWolfStampede(source)||!IsCombatActive(source)||battleEnding||scale<=0f)return;
        var targets=new List<Unit>();
        foreach(Unit enemy in units)if(enemy.team!=source.team&&IsCombatTargetable(enemy)&&enemy.hp>0f)targets.Add(enemy);
        if(targets.Count==0)return;
        Vector3 right=mainCamera==null?Vector3.right:Flat(mainCamera.transform.right).normalized;
        if(right.sqrMagnitude<.01f)right=Vector3.right;
        var state=new WolfStampedeState{owner=this,source=source,targets=targets.ToArray(),released=true,echo=true,
            generation=combatGeneration,attack=source.damage,damageScale=scale,bounds=combatBounds,
            direction=source.team==Team.Player?right:-right};
        state.impacts=new bool[state.targets.Length*WolfStampedeRules.WaveCount];wolfStampedes.Add(state);
        if(!mlEpisodeActive)state.effect=PixelWolfStampede.Create(state.bounds,state.direction,Rank3Renderer(source));
    }

    private void ReleaseWolfStampedeCaster(WolfStampedeState state)
    {
        state.poseTime=WolfStampedeRules.Preparation+WolfStampedeRules.Duration+2f;
        Unit source=state.source;if(source.wolfStampede!=state)return;
        source.wolfHuntPending=false;source.wolfHuntTarget=null;
        source.wolfStampede=null;source.attackMovementLockTimer=0f;source.decisionTimer=0f;
        source.spriteAnimation?.InterruptCurrentAction(true);
        if(IsCombatActive(source))source.currentAction="idle";
    }

    private void CancelWolfStampedeFor(Unit source)
    {
        var state=source?.wolfStampede;if(state==null)return;
        if(state.bodyHuntStarted&&source.positionRevision==state.revision)source.positionRevision++;
        CancelWolfHuntAfterimages(source);
        if(!state.released){state.cancelled=true;CancelWolfStampedeCinematicFor(source);state.effect?.Cancel();state.effect=null;}
        ReleaseWolfStampedeCaster(state);
    }

    private void StopWolfStampede(WolfStampedeState state,bool immediate)
    {
        state.cancelled=true;
        if(battleEnding&&state.bodyHuntStarted&&state.source.positionRevision==state.revision)
            state.source.positionRevision++;
        if(state.source.wolfStampede==state)CancelWolfStampedeCinematicFor(state.source);
        ReleaseWolfStampedeCaster(state);
        if(state.effect!=null)
        {
            if(immediate)state.effect.Cancel();else state.effect.FinishNaturally();
            state.effect=null;
        }
    }

    private void ResetWolfStampedes(bool immediate)
    {
        foreach(var state in wolfStampedes)StopWolfStampede(state,immediate);
        wolfStampedes.Clear();
    }
}
