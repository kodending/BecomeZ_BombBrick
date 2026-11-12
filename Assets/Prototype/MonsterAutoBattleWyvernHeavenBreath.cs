using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class WyvernHeavenBreathRules
{
    public const string TraitId="trait_transcend_wyvern_dragon_blood_awakening";
    public const float ShadowDuration=.9f,PauseDuration=.2f,FallDuration=.55f;
    public const float ImpactTime=ShadowDuration+PauseDuration+FallDuration;
    public const float EndTime=ImpactTime+.55f,DamageRatio=3f;
}

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class WyvernHeavenBreath
    {
        public CombatSkillClock skillClock=CombatSkillClock.Capture();
        public Unit source;
        public int generation;
        public float time;
        public bool started,hit,absorbed;
        public System.Action cameraEffectCompleted;
        public void CompleteCameraEffect(){var completed=cameraEffectCompleted;cameraEffectCompleted=null;completed?.Invoke();}
        public Bounds arena;
        public PixelWyvernHeavenBreath effect;
    }
    private readonly List<WyvernHeavenBreath> wyvernHeavenBreaths=new List<WyvernHeavenBreath>();

    private bool BeginWyvernHeavenBreath(Unit source,SkillMasterRow skill)
    {
        if(skill==null||battleEnding||!IsCombatActive(source)||!HasFamilyTranscendence(source)||!IsWyvernFamily(source)||
           wyvernHeavenBreaths.Any(c=>c.source==source&&c.generation==combatGeneration)||
           !units.Any(u=>u.team!=source.team&&IsCombatTargetable(u)))return false;
        source.wyvernBreathPending=false;source.wyvernBreathTarget=null;
        source.wyvernBreathDamageMultiplier=source.wyvernBreathRadiusMultiplier=1f;
        source.skillTimer=source.skillCooldownDuration;source.skillUses++;
        source.currentAction="wyvern_heaven_prepare";
        StopBeforeAttack(source);SetMoving(source,false);
        ApplyPostSkillItemEffects(source);
        RecordSkillEvent(source,WyvernHeavenBreathRules.TraitId+"_start",0,0f);
        var cast=new WyvernHeavenBreath{source=source,generation=combatGeneration,
            arena=combatBounds.size.x>0f?combatBounds:CenteredArenaBounds(),
            cameraEffectCompleted=HoldSkillCameraEffect(source)};
        wyvernHeavenBreaths.Add(cast);
        AdvanceWyvernHeavenBreath(cast,0f);
        return true;
    }

    private void UpdateWyvernHeavenBreaths(float dt)
    {
        for(int i=wyvernHeavenBreaths.Count-1;i>=0;i--)
        {
            var cast=wyvernHeavenBreaths[i];
            if(PausedBySkillTime(cast.skillClock))continue;
            AdvanceWyvernHeavenBreath(cast,dt);
        }
    }

    private void AdvanceWyvernHeavenBreath(WyvernHeavenBreath cast,float dt)
    {
            Unit source=cast.source;
            if(battleEnding||cast.generation!=combatGeneration||!HasFamilyTranscendence(source))
            {cast.CompleteCameraEffect();cast.effect?.Cancel();wyvernHeavenBreaths.Remove(cast);return;}
            if(paused)return;
            if(!cast.started)
            {
                cast.started=true;
                QueueRank4Cinematic(source);
                if(!mlEpisodeActive)
                {
                    int order=units.Where(IsCombatActive).Select(Rank3Renderer).Where(r=>r!=null)
                        .Select(r=>r.sortingOrder).DefaultIfEmpty(0).Max()+8;
                    cast.effect=PixelWyvernHeavenBreath.Create(cast.arena,Rank3Renderer(source),order,WyvernElement(source));
                    cast.effect.transform.SetParent(transform,true);
                    AddCombatFloatText(source,FamilyTranscendenceRules.DisplayName("wyvern"),PixelWyvernBreath.Tint(WyvernElement(source)),true);
                }
                return;
            }
            if(rank4PresentationQueue.Any(request=>request.source==source&&Rank4PresentationValid(request)))return;
            float previousTime=cast.time;
            cast.time+=Mathf.Max(0f,dt);
            if(previousTime<=0f&&cast.time>0f)PlayWyvernSound(source,WyvernSound.Shadow,cast.arena.center,.9f);
            float fall=WyvernHeavenBreathRules.ShadowDuration+WyvernHeavenBreathRules.PauseDuration;
            if(previousTime<fall&&cast.time>=fall)PlayWyvernSound(source,WyvernSound.HeavenFall,cast.arena.center);
            if(!cast.hit&&!cast.absorbed&&cast.time>=fall&&AbsorbGriffinEnergy(source,
                cast.effect==null?cast.arena.center+Vector3.up*12f:cast.effect.FallOrigin,
                cast.arena.center,PixelWyvernBreath.Tint(WyvernElement(source)),out Vector3 anchor))
            {cast.absorbed=true;cast.effect?.AbsorbTo(anchor);}
            cast.effect?.Advance(cast.time);
            if(!cast.hit&&cast.time+.00001f>=WyvernHeavenBreathRules.ImpactTime)
            {
                cast.hit=true;int hits=0;float damage=0f;
                if(!cast.absorbed)PlayWyvernSound(source,WyvernSound.HeavenHit,cast.arena.center,1.1f);
                foreach(Unit enemy in units.Where(u=>!cast.absorbed&&u.team!=source.team&&IsCombatTargetable(u)).ToArray())
                {
                    Vector3 p=Flat(enemy.model.transform.position);
                    if(p.x<cast.arena.min.x||p.x>cast.arena.max.x||p.z<cast.arena.min.z||p.z>cast.arena.max.z)continue;
                    damage+=Rank3Hit(source,enemy,WyvernHeavenBreathRules.DamageRatio);hits++;
                    if(!mlEpisodeActive)
                    {
                        PixelWyvernBreath.OverheatHit(Rank3Center(enemy),Rank3Renderer(enemy),WyvernElement(source),Vector3.forward,5);
                        enemy.spriteAnimation?.PlayImpactRecoil(Vector3.forward,.1f);
                    }
                    if(source==null)break;
                }
                if(!mlEpisodeActive&&!cast.absorbed)combatCameraDirector?.ReportGroundSlam(true);
                source.traitActivations++;RecordSkillEvent(source,WyvernHeavenBreathRules.TraitId,hits,damage);
                cast.CompleteCameraEffect();
            }
            if(cast.time<WyvernHeavenBreathRules.EndTime)return;
            cast.CompleteCameraEffect();cast.effect?.Cancel();wyvernHeavenBreaths.Remove(cast);
    }

    private void ClearWyvernHeavenBreaths()
    {
        foreach(var cast in wyvernHeavenBreaths){cast.CompleteCameraEffect();cast.effect?.Cancel();}
        wyvernHeavenBreaths.Clear();
    }
}
