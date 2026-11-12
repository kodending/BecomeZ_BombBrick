using System.Collections.Generic;
using System.Linq;
using BombBrick.Combat;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class WyvernBreathCast
    {
        public CombatSkillClock skillClock=CombatSkillClock.Capture();
        public MonsterAutoBattlePrototype owner;
        public Unit source,target;
        public SkillMasterRow skill;
        public Vector3 origin,direction;
        public float time,windup,scale,length,width;
        public int generation,revision,nextHit,element,hits;
        public float damage;
        public bool echo,launched,finished,pressure,rain,overheat,shaken,sounding,pressureAbsorbed,unabsorbedPulse;
        public Vector3[] rainPoints,rainOffsets;
        public bool[] rainBlocked;
        public float rainRadius,previousTime;
        public PixelWyvernRain rainEffect;
        public float fireAge;
        public int fireTicks;
        public bool fireField;
        public System.Action cameraEffectCompleted;
        public void CompleteCameraEffect(){var completed=cameraEffectCompleted;cameraEffectCompleted=null;completed?.Invoke();}
        public PixelWyvernBreath effect;
        public PixelWyvernPressure pressureEffect;
        public readonly HashSet<Unit> intrinsicHits=new HashSet<Unit>();
    }
    private readonly List<WyvernBreathCast> wyvernBreaths=new List<WyvernBreathCast>();

    private static bool HasWyvernRank3(Unit source)=>HasRank3(source,WyvernRank3Rules.CataclysmBreath)||
        HasRank3(source,WyvernRank3Rules.WingSweep)||HasRank3(source,WyvernRank3Rules.DragonCore);

    private bool StartWyvernBreath(Unit source,Unit target,SkillMasterRow skill,float scale,bool echo,int generation)
    {
        if(generation!=combatGeneration||!IsCombatActive(source)||!IsCombatActive(target)||skill==null)return false;
        if(HasFamilyTranscendence(source))return !echo&&BeginWyvernHeavenBreath(source,skill);
        Vector3 direction=Flat(target.model.transform.position-source.model.transform.position);
        if(direction.sqrMagnitude<.001f)direction=source.team==Team.Player?Vector3.right:Vector3.left;
        bool pressure=!echo&&HasRank3(source,WyvernRank3Rules.CataclysmBreath);
        bool rain=!echo&&HasRank3(source,WyvernRank3Rules.WingSweep);
        bool overheat=!echo&&HasRank3(source,WyvernRank3Rules.DragonCore);
        var cast=new WyvernBreathCast {owner=this,source=source,target=target,skill=skill,echo=echo,generation=generation,pressure=pressure,rain=rain,overheat=overheat,
            revision=source.positionRevision,windup=echo?0f:pressure?WyvernRank3Rules.PressureWindup:rain?WyvernRank3Rules.RainWindup:overheat?WyvernRank3Rules.CoreCharge:WyvernCombatRules.Windup,
            origin=Flat(source.model.transform.position),direction=direction.normalized,
            scale=scale*(echo?1f:source.wyvernBreathDamageMultiplier),
            length=TileSize*(pressure?WyvernRank3Rules.PressureRangeTiles:overheat?WyvernRank3Rules.CoreRangeTiles:Mathf.Max(1f,skill.castRangeTiles)),
            width=TileSize*Mathf.Max(.1f,skill.radiusTiles)*(echo?1f:source.wyvernBreathRadiusMultiplier),
            element=HasRank3(source,WyvernCombatRules.IgvireTraitId)?1:
                HasRank3(source,WyvernCombatRules.FrovireTraitId)?2:HasRank3(source,WyvernCombatRules.TemvireTraitId)?3:0};
        if(rain)PrepareWyvernRain(cast,source.wyvernBreathRadiusMultiplier);
        if(!echo)
        {
            cast.cameraEffectCompleted=HoldSkillCameraEffect(source);
            source.wyvernBreath=cast;source.wyvernBreathPending=false;
            source.wyvernBreathDamageMultiplier=source.wyvernBreathRadiusMultiplier=1f;
        }
        wyvernBreaths.Add(cast);
        if(!echo)PlayWyvernSound(source,pressure?WyvernSound.Compress:WyvernSound.Prepare,cast.origin,.8f);
        if(!mlEpisodeActive)
        {
            System.Func<bool> valid=()=>ReleasedSkillValid(source,generation)&&(cast.launched||IsCombatActive(source));
            if(rain)
            {
                cast.rainEffect=PixelWyvernRain.Create(Rank3Renderer(source),cast.origin,cast.direction,
                    cast.rainPoints,cast.rainOffsets,cast.rainBlocked,cast.rainRadius,cast.element,CaptureSkillTime(source),valid);
                cast.rainEffect.transform.SetParent(transform,true);
            }
            else if(pressure)
            {
                cast.pressureEffect=PixelWyvernPressure.Create(Rank3Renderer(source),cast.origin,cast.direction,
                    cast.length,cast.element,CaptureSkillTime(source),valid);
                cast.pressureEffect.transform.SetParent(transform,true);
            }
            else
            {
                cast.effect=PixelWyvernBreath.Create(Rank3Renderer(source),cast.origin,cast.direction,
                    cast.length,cast.width,cast.windup,cast.element,CaptureSkillTime(source),valid,overheat);
                cast.effect.transform.SetParent(transform,true);
            }
        }
        return true;
    }

    private bool WyvernBreathValid(WyvernBreathCast cast)=>!cast.finished&&!battleEnding&&
        cast.generation==combatGeneration&&(cast.pressure||cast.rain||cast.source.alive)&&
        (cast.launched||IsCombatActive(cast.source)&&
        cast.revision==cast.source.positionRevision&&!cast.source.displacing&&
        !SpiderSilkBound(cast.source)&&!cast.source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !cast.source.combatStatuses.Has(CombatStatusKind.Fear)&&
        !cast.source.combatStatuses.Has(CombatStatusKind.Airborne)&&cast.source.giraffeStunTimer<=0f);

    private void UpdateWyvernBreaths(float dt)
    {
        UpdateWyvernHeavenBreaths(dt);
        for(int i=wyvernBreaths.Count-1;i>=0;i--)
        {
            var cast=wyvernBreaths[i];
            if(PausedBySkillTime(cast.skillClock))continue;
            if(cast.finished&&cast.fireField)
            {
                if(cast.rain)AdvanceWyvernRainFire(cast,paused?0f:Mathf.Max(0f,dt));
                else AdvanceWyvernPressureFire(cast,paused?0f:Mathf.Max(0f,dt));
                if(!cast.fireField)wyvernBreaths.RemoveAt(i);
                continue;
            }
            if(!WyvernBreathValid(cast))EndWyvernBreath(cast);
            else if(!cast.launched&&cast.source.combatStatuses.Has(CombatStatusKind.Freeze))SetWyvernBreathSound(cast,false);
            else if(!paused)
            {
                if(cast.launched)SetWyvernBreathSound(cast,true);
                float speed=cast.launched?1f:RaptorCombatRules.IceSpeedMultiplier(cast.source.raptorSlowTimer>0f)*
                    (cast.source.rank3ActionSlowTimer>0f?1f-Mathf.Clamp01(cast.source.rank3ActionSlowRatio):1f);
                AdvanceWyvernBreath(cast,cast.time+Mathf.Max(0f,dt)*speed*CombatStatFormula.CombatAnimationRate);
            }
            if(cast.finished&&!cast.fireField)wyvernBreaths.RemoveAt(i);
        }
    }

    private void AdvanceWyvernBreath(WyvernBreathCast cast,float time)
    {
        if(!WyvernBreathValid(cast)){EndWyvernBreath(cast);return;}
        cast.previousTime=cast.time;cast.time=time;
        if(cast.overheat){AdvanceWyvernOverheat(cast);return;}
        if(cast.rain){AdvanceWyvernRain(cast);return;}
        if(cast.pressure){AdvanceWyvernPressure(cast);return;}
        Unit source=cast.source;
        if(!cast.echo)
        {
            source.velocity=Vector3.zero;SetMoving(source,false);
            float lean=Mathf.Clamp01(time/Mathf.Max(.01f,cast.windup));
            source.spriteAnimation?.SetSkillPoseScale(time<cast.windup?
                new Vector3(1f-.07f*lean,1f+.07f*lean,1f):new Vector3(1.04f,.97f,1f));
        }
        if(!cast.launched&&time>=cast.windup)
        {
            cast.launched=true;
            if(cast.echo)PlayWyvernSound(source,WyvernSound.Breath,cast.origin,.45f);
            else SetWyvernBreathSound(cast,true);
            // Aim follows only during the windup; keep firing if that target later dies or moves.
            if(IsCombatActive(cast.target))
            {
                Vector3 aim=Flat(cast.target.model.transform.position)-cast.origin;
                if(aim.sqrMagnitude>.001f)cast.direction=aim.normalized;
            }
            if(!cast.echo){source.currentAction="wyvern_breath";RotateUnit(source,cast.direction,1f);}
        }
        cast.effect?.Pose(time,cast.direction);
        while(WyvernBreathValid(cast)&&cast.nextHit<WyvernCombatRules.BreathHits&&
              time+.00001f>=cast.windup+WyvernCombatRules.HitTime(cast.nextHit))
        {
            ResolveWyvernBreathTick(cast,cast.nextHit==WyvernCombatRules.BreathHits-1);
            cast.nextHit++;
        }
        if(cast.finished)return;
        if(time+.00001f>=cast.windup+WyvernCombatRules.BreathDuration)
        {
            if(!cast.echo&&cast.element==1&&cast.unabsorbedPulse)
            {
                if(!mlEpisodeActive)PixelCombatEffect.PlayWyvernFireTrail(cast.origin,
                    cast.origin+cast.direction*cast.length,cast.width*2f,WyvernCombatRules.DotDuration,Rank3Renderer(source));
                if(isActiveAndEnabled)StartCombatCoroutine(source,ResolveWyvernFireLine(source,cast.origin,cast.direction,
                    cast.length,cast.width,cast.scale,cast.generation));
            }
            RecordSkillEvent(source,cast.skill.id+(cast.echo?"_echo":""),cast.hits,cast.damage);
            EndWyvernBreath(cast,true);
        }
    }

    private void ResolveWyvernBreathTick(WyvernBreathCast cast,bool final)
    {
        Unit source=cast.source;
        if(AbsorbGriffinEnergy(source,cast.origin+Vector3.up*.5f,
            cast.origin+cast.direction*cast.length+Vector3.up*.5f,PixelWyvernBreath.Tint(cast.element),out Vector3 anchor))
        {cast.effect?.AbsorbTo(anchor);return;}
        cast.unabsorbedPulse=true;cast.effect?.ResumeEmission();
        bool sounded=false;
        var victims=units.Where(enemy=>IsCombatActive(enemy)&&enemy.team!=source.team&&
            IsPointInsideForwardSkillLine(cast.origin,cast.direction,Flat(enemy.model.transform.position),
                cast.length,cast.width+enemy.collisionRadius)).ToList();
        foreach(var enemy in victims)
        {
            if(!WyvernBreathValid(cast))break;
            if(!sounded){sounded=true;PlayWyvernSound(source,WyvernSound.Hit,Rank3Center(enemy),cast.echo?.45f:.7f);}
            float damage=ApplySkillFormulaDamage(source,enemy,cast.skill,false,0,
                cast.scale/WyvernCombatRules.BreathHits,!cast.echo);
            cast.damage+=damage;cast.hits++;source.skillDamage+=damage;source.skillHits++;
            if(!mlEpisodeActive)
            {
                PixelWyvernBreath.Hit(Rank3Center(enemy),Rank3Renderer(enemy),cast.element,final);
                if(final)enemy.spriteAnimation?.PlayImpactRecoil(cast.direction,.06f);
            }
            if(!cast.echo&&WyvernBreathValid(cast)&&cast.intrinsicHits.Add(enemy))ApplyWyvernBreathIntrinsic(cast,enemy);
        }
    }

    private void ApplyWyvernBreathIntrinsic(WyvernBreathCast cast,Unit enemy)
    {
        Unit source=cast.source;
        if(cast.element==0&&IsCombatActive(enemy)&&HasRank3(source,WyvernCombatRules.VivreTraitId))
            enemy.combatStatuses.ApplyStacks(CombatStatusKind.Poison,WyvernCombatRules.DotDuration,1,
                CombatStatusState.MaximumPoisonStacks,source,source.damage*WyvernCombatRules.VenomTickRatio*cast.scale,
                WyvernCombatRules.TickInterval,true);
        if(cast.element==2&&IsCombatActive(enemy))
        {
            bool impaired=HasPriorChill(source,enemy)||enemy.combatStatuses.Has(CombatStatusKind.Root)||
                enemy.combatStatuses.Has(CombatStatusKind.Knockdown);
            enemy.combatStatuses.ApplyStacks(CombatStatusKind.ComboChilled,WyvernCombatRules.ChillDuration,
                WyvernCombatRules.ChillStacks,3,source,0f,0f,true);
            if(impaired)
            {
                float duration=HostileStatusDuration(enemy,WyvernCombatRules.FreezeDuration);
                enemy.combatStatuses.ApplyTimed(CombatStatusKind.Freeze,duration,source);
                if(!mlEpisodeActive)PlayFreezeVisual(enemy,duration);
            }
        }
        if(cast.element==3)
        {
            Unit chain=units.Where(other=>IsCombatActive(other)&&other.team!=source.team&&other!=enemy)
                .OrderBy(other=>Vector3.Distance(Flat(enemy.model.transform.position),Flat(other.model.transform.position))).FirstOrDefault();
            if(chain!=null)
            {
                float damage=ApplySkillDamage(source,chain,source.damage*WyvernCombatRules.StormChainRatio*cast.scale,false);
                source.skillHits++;source.skillDamage+=damage;cast.hits++;cast.damage+=damage;
                if(!mlEpisodeActive)
                {
                    PixelCombatEffect.PlayBeetleLightningArc(Rank3Center(enemy),Rank3Center(chain),
                        Rank3Renderer(chain),"Temvire Breath Lightning",.22f,1.3f);
                    PixelWyvernBreath.Hit(Rank3Center(chain),Rank3Renderer(chain),3,true);
                }
            }
        }
        if(cast.intrinsicHits.Count==1)source.traitActivations++;
    }

    private static void InterruptWyvernBreath(WyvernBreathCast cast)
    {
        if(cast==null)return;
        if(!cast.launched){EndWyvernBreath(cast);return;}
        if(cast.source.wyvernBreath==cast)cast.source.wyvernBreath=null;
    }

    private void StopWyvernBreathsOnDeath(Unit source)
    {
        foreach(var cast in wyvernBreaths)
        {
            if(cast.source!=source||cast.finished)continue;
            if(!cast.launched)EndWyvernBreath(cast);
            else if(cast.rain)cast.rainEffect?.StopMouthEmission();
            else if(!cast.pressure)EndWyvernBreath(cast,stopEmission:true);
        }
    }

    private static void EndWyvernBreath(WyvernBreathCast cast,bool completed=false,bool stopEmission=false)
    {
        if(cast==null||cast.finished)return;
        cast.owner?.SetWyvernBreathSound(cast,false,stopEmission||!cast.source.alive);
        cast.finished=true;
        cast.CompleteCameraEffect();
        if(completed)cast.effect?.Finish();
        else if(stopEmission||!cast.source.alive)cast.effect?.StopEmission();
        else cast.effect?.Cancel();
        if(completed)
        {
            if(!cast.fireField)cast.pressureEffect?.Finish();
        }
        else cast.pressureEffect?.Cancel();
        if(completed)cast.rainEffect?.Finish(cast.fireField);else cast.rainEffect?.Cancel();
        if(cast.source.wyvernBreath!=cast)return;
        cast.source.wyvernBreath=null;cast.source.wyvernBreathPending=false;cast.source.wyvernBreathTarget=null;
        cast.source.spriteAnimation?.SetSkillPoseScale(Vector3.one);
        if(cast.pressure||cast.rain||cast.overheat)cast.source.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);
        if(!completed)cast.source.spriteAnimation?.InterruptCurrentAction(true);
    }

    private void ClearWyvernBreaths()
    {
        ClearWyvernHeavenBreaths();
        foreach(var cast in wyvernBreaths){cast.pressureEffect?.Cancel();cast.rainEffect?.Cancel();EndWyvernBreath(cast);}
        wyvernBreaths.Clear();
    }
}
