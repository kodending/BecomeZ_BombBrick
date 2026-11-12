using System.Collections.Generic;
using System.Linq;
using BombBrick.Combat;
using UnityEngine;
using BombBrick.Game.Core;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class HaetaeRoarCast
    {
        public CombatSkillClock skillClock=CombatSkillClock.Capture();
        public Unit source;
        public SkillMasterRow skill;
        public Vector3 origin,forward;
        public float time,windup,radius,scale,damage;
        public int generation,revision,hits,element,pulse;
        public bool echo,released,completed,cancelled,heavenly,oppression,bellow,recorded,impactFeedback;
        public bool hitSound,cleanseSound,warningSound;
        public System.Action cameraEffectCompleted;
        public void CompleteCameraEffect(){var completed=cameraEffectCompleted;cameraEffectCompleted=null;completed?.Invoke();}
        public int soundPulse=-1;
        public PixelHaetaeJusticeRoar effect;
        public PixelHaetaeOppression aura;
        public PixelHaetaeBellow pressure;
        public readonly HashSet<Unit> touched=new HashSet<Unit>();
        public readonly HashSet<Unit> struck=new HashSet<Unit>();
        public readonly List<HaetaeThunderStrike> strikes=new List<HaetaeThunderStrike>();
    }
    private sealed class HaetaeThunderStrike
    {
        public Vector3 point;
        public float time;
        public bool resolved;
        public PixelHaetaeThunder effect;
    }
    private readonly List<HaetaeRoarCast> haetaeRoars=new List<HaetaeRoarCast>();
    private static int HaetaeElement(Unit u)=>HasRank3(u,HaetaeCombatRules.YeomhaeTraitId)?1:
        HasRank3(u,HaetaeCombatRules.GeumgangsuTraitId)?2:HasRank3(u,HaetaeCombatRules.MukryeongTraitId)?3:0;
    private static float HaetaeRoarRadius(Unit source,SkillMasterRow skill)=>
        HasRank3(source,HaetaeRank3Rules.HeavenlyJudgment)?HaetaeRank3Rules.HeavenlyRadiusTiles:
        HasRank3(source,HaetaeRank3Rules.DivineOppression)?HaetaeRank3Rules.OppressionRadiusTiles:
        HasRank3(source,HaetaeRank3Rules.JudgmentBellow)?HaetaeRank3Rules.BellowRangeTiles:skill.radiusTiles;

    private int HaetaeSupportTargets(Unit source,SkillMasterRow skill)
    {
        if(!IsCombatActive(source)||skill==null||skill.behavior!="JusticeRoar")return 0;
        float radius=TileSize*(HasRank3(source,HaetaeRank3Rules.JudgmentBellow)?
            HaetaeRank3Rules.BellowSupportTiles:HaetaeRoarRadius(source,skill))*source.augmentSkillAreaMultiplier*
            MonsterTraitRuntime.PreviewSkillUse(source.activeTraitIds,source.traitState).radiusMultiplier;
        Vector3 origin=Flat(source.model.transform.position);
        bool heal=HasRank3(source,HaetaeCombatRules.HaetaTraitId);
        int count=0;
        for(int i=0;i<units.Count;i++)
        {
            Unit ally=units[i];
            if(IsCombatActive(ally)&&!ally.augmentShadowClone&&ally.team==source.team&&
               Vector3.Distance(origin,Flat(ally.model.transform.position))<=radius+ally.collisionRadius&&
               (ally.combatStatuses.HarmfulCount>0||CleanseTimedHarmfulEffects(ally,true,false)>0||
                heal&&ally.hp<=ally.maxHp*HaetaeCombatRules.HealingCastThreshold))count++;
        }
        return count;
    }

    private bool BeginHaetaeJusticeRoar(Unit unit,SkillMasterRow skill)
    {
        if(!IsCombatActive(unit)||skill==null||unit.haetaeRoar!=null)return false;
        float radius=TileSize*HaetaeRoarRadius(unit,skill)*unit.skillTraitRadiusMultiplier;
        if(!IsCommittingFocusedSkill(unit)&&!units.Any(enemy=>IsCombatTargetable(enemy)&&enemy.team!=unit.team&&
            Vector3.Distance(Flat(unit.model.transform.position),Flat(enemy.model.transform.position))<=radius+enemy.collisionRadius)&&
            HaetaeSupportTargets(unit,skill)==0)return false;
        unit.haetaeRoarDamageMultiplier=unit.skillTraitDamageMultiplier;
        unit.haetaeRoarRadiusMultiplier=unit.skillTraitRadiusMultiplier;
        unit.skillTimer=unit.skillCooldownDuration;unit.skillUses++;
        unit.currentAction="haetae_roar_prepare";StopBeforeAttack(unit);SetMoving(unit,false);
        ApplyPostSkillItemEffects(unit);RecordSkillEvent(unit,skill.id+"_start",0,0f);
        CreateHaetaeRoar(unit,skill,1f,false,combatGeneration);
        return true;
    }

    private bool ResolveHaetaeJusticeRoar(Unit source,SkillMasterRow skill,float effectScale,bool traitEcho,int generation)
    {
        if(generation!=combatGeneration||!IsCombatActive(source)||skill==null)return false;
        if(!traitEcho&&source.haetaeRoar!=null)return true;
        CreateHaetaeRoar(source,skill,effectScale,traitEcho,generation);return true;
    }

    private void CreateHaetaeRoar(Unit source,SkillMasterRow skill,float scale,bool echo,int generation)
    {
        bool heavenly=!echo&&HasRank3(source,HaetaeRank3Rules.HeavenlyJudgment);
        bool bellow=!echo&&HasRank3(source,HaetaeRank3Rules.JudgmentBellow);
        var cast=new HaetaeRoarCast {source=source,skill=skill,echo=echo,generation=generation,
            heavenly=heavenly,oppression=!echo&&HasRank3(source,HaetaeRank3Rules.DivineOppression),bellow=bellow,
            revision=source.positionRevision,origin=Flat(source.model.transform.position),element=HaetaeElement(source),
            windup=echo?0f:heavenly?HaetaeRank3Rules.HeavenlyWindup:bellow?HaetaeRank3Rules.BellowWindup:HaetaeCombatRules.Windup,
            radius=TileSize*(echo?skill.radiusTiles:HaetaeRoarRadius(source,skill))*(echo?1f:source.haetaeRoarRadiusMultiplier),
            scale=scale*(echo?1f:source.haetaeRoarDamageMultiplier),
            cameraEffectCompleted=heavenly?HoldSkillCameraEffect(source):null};
        if(!echo)
        {
            source.haetaeRoar=cast;
            source.haetaeRoarDamageMultiplier=source.haetaeRoarRadiusMultiplier=1f;
        }
        if(bellow)
        {
            Unit target=CommittedSkillTarget(source);
            if(!IsCombatActive(target))
                target=IsCombatTargetable(source.target)&&Vector3.Distance(cast.origin,Flat(source.target.model.transform.position))<=
                    cast.radius+source.target.collisionRadius?source.target:
                    Rank3Enemies(source,cast.origin,cast.radius).Where(IsCombatTargetable)
                        .OrderBy(enemy=>(Flat(enemy.model.transform.position)-cast.origin).sqrMagnitude).FirstOrDefault();
            cast.forward=target==null?(source.team==Team.Player?Vector3.right:Vector3.left):
                Flat(target.model.transform.position)-cast.origin;
            if(cast.forward.sqrMagnitude<.001f)cast.forward=source.team==Team.Player?Vector3.right:Vector3.left;
            cast.forward.Normalize();RotateUnit(source,cast.forward,1f);
        }
        haetaeRoars.Add(cast);
        if(!echo)PlayHaetaeSound(source,bellow?HaetaeSound.Compress:HaetaeSound.Prepare,cast.origin,.8f);
        if(!mlEpisodeActive)
        {
            if(bellow)
            {
                cast.pressure=PixelHaetaeBellow.Create(Rank3Renderer(source),cast.origin,cast.forward,cast.radius,cast.element);
                cast.pressure.transform.SetParent(transform,true);
            }
            else
            {
                cast.effect=PixelHaetaeJusticeRoar.Create(Rank3Renderer(source),cast.origin,cast.radius,cast.windup,cast.element);
                cast.effect.transform.SetParent(transform,true);
            }
        }
    }

    private void UpdateHaetaeRoars(float dt)
    {
        for(int i=haetaeRoars.Count-1;i>=0;i--)
        {
            var cast=haetaeRoars[i];Unit source=cast.source;
            if(PausedBySkillTime(cast.skillClock))continue;
            if(cast.cancelled||!ReleasedSkillValid(source,cast.generation)||!cast.released&&(!IsCombatActive(source)||
                !cast.echo&&!cast.completed&&(cast.revision!=source.positionRevision||source.displacing||SpiderSilkBound(source)||
                source.combatStatuses.Has(CombatStatusKind.Knockdown)||source.combatStatuses.Has(CombatStatusKind.Fear)||
                source.combatStatuses.Has(CombatStatusKind.Airborne)||source.giraffeStunTimer>0f)))
            {CancelHaetaeRoar(cast);haetaeRoars.RemoveAt(i);continue;}
            if(paused||!cast.released&&!cast.echo&&!cast.completed&&source.combatStatuses.Has(CombatStatusKind.Freeze))continue;
            float speed=cast.echo?1f:RaptorCombatRules.IceSpeedMultiplier(source.raptorSlowTimer>0f)*
                (source.rank3ActionSlowTimer>0f?1f-Mathf.Clamp01(source.rank3ActionSlowRatio):1f);
            float previousTime=cast.time;
            float presentationRate=cast.oppression&&cast.released?1f:CombatStatFormula.CombatAnimationRate;
            cast.time+=Mathf.Max(0f,dt)*(cast.released||cast.completed?1f:speed)*presentationRate;
            if(!cast.released&&cast.time+.00001f>=cast.windup)
            {
                cast.released=true;cast.origin=Flat(source.model.transform.position);
                if(!cast.echo)source.currentAction="haetae_justice_roar";
                PlayHaetaeSound(source,cast.bellow?HaetaeSound.Bellow:cast.oppression?HaetaeSound.Domain:HaetaeSound.Roar,
                    cast.origin,cast.echo?.45f:cast.bellow?1f:.85f);
                if(cast.bellow)
                {
                    foreach(var ally in Rank3Allies(source,cast.origin,cast.radius*HaetaeRank3Rules.BellowSupportTiles/HaetaeRank3Rules.BellowRangeTiles))
                        if(!ally.augmentShadowClone)ApplyHaetaeRoarAlly(cast,ally);
                }
                if(cast.oppression)
                {
                    // Recasting refreshes this caster's aura; two copies never stack.
                    foreach(var other in haetaeRoars)
                        if(other!=cast&&other.oppression&&other.source==source)CancelHaetaeRoar(other);
                    foreach(var ally in Rank3Allies(source,cast.origin,cast.radius))
                        if(!ally.augmentShadowClone)ApplyHaetaeRoarAlly(cast,ally);
                    if(!mlEpisodeActive)
                    {
                        cast.aura=PixelHaetaeOppression.Create(Rank3Renderer(source),cast.radius,cast.element);
                        cast.aura.transform.SetParent(transform,true);
                    }
                }
            }
            cast.effect?.Pose(cast.time,cast.origin);
            cast.pressure?.Pose(cast.time);
            if(cast.oppression&&cast.released)
            {
                UpdateHaetaeOppression(cast);
                if(cast.time+.00001f>=cast.windup+HaetaeRank3Rules.OppressionDuration)
                {
                    RecordSkillEvent(source,HaetaeRank3Rules.DivineOppression,cast.hits,cast.damage);
                    CancelHaetaeRoar(cast);haetaeRoars.RemoveAt(i);
                }
                continue;
            }
            if(cast.released&&!cast.completed)
            {
                float front=cast.radius*(cast.bellow?HaetaeRank3Rules.BellowFront(cast.time-cast.windup):
                    HaetaeCombatRules.WaveProgress(cast.time-cast.windup));
                foreach(Unit unit in units.Where(IsCombatTargetable).ToArray())
                {
                    if(cast.cancelled)break;
                    float distance=Vector3.Distance(cast.origin,Flat(unit.model.transform.position));
                    if(cast.bellow&&(unit.team==source.team||!HaetaeBellowCone(cast,unit)))continue;
                    if(distance>cast.radius+unit.collisionRadius||distance>front+unit.collisionRadius||!cast.touched.Add(unit))continue;
                    if(unit.team==source.team){if(!unit.augmentShadowClone)ApplyHaetaeRoarAlly(cast,unit);}
                    else if(cast.heavenly)
                    {
                        float contact=cast.windup+HaetaeCombatRules.WaveDuration*(1f-Mathf.Sqrt(
                            1f-Mathf.Clamp01((distance-unit.collisionRadius)/cast.radius)));
                        var strike=new HaetaeThunderStrike {point=Flat(unit.model.transform.position),time=Mathf.Max(previousTime,contact)};
                        if(!cast.warningSound)
                        {
                            cast.warningSound=true;
                            PlayHaetaeSound(source,HaetaeSound.Warning,strike.point,.75f);
                        }
                        if(!mlEpisodeActive)
                        {
                            strike.effect=PixelHaetaeThunder.Create(strike.point,cast.element,Rank3Renderer(unit));
                            strike.effect.transform.SetParent(transform,true);
                        }
                        cast.strikes.Add(strike);
                    }
                    else if(cast.bellow)ApplyHaetaeBellowHit(cast,unit);
                    else ApplyHaetaeRoarEnemy(cast,unit);
                }
                if(cast.time+.00001f>=cast.windup+(cast.bellow?HaetaeRank3Rules.BellowWaveDuration:HaetaeCombatRules.WaveDuration))
                {
                    cast.completed=true;
                    if(source.haetaeRoar==cast)source.haetaeRoar=null;
                    if(!cast.echo&&cast.touched.Count>0)source.traitActivations++;
                }
            }
            foreach(var strike in cast.strikes)
            {
                if(cast.cancelled)break;
                strike.effect?.Pose(cast.time-strike.time);
                if(strike.resolved||cast.time+.00001f<strike.time+HaetaeRank3Rules.HeavenlyDelay)continue;
                strike.resolved=true;
                if(!cast.impactFeedback)
                {
                    cast.impactFeedback=true;
                    if(!mlEpisodeActive)
                    {
                        combatCameraDirector?.ReportHaetaeThunder();
                        PlayCombatSound(source,CombatSoundCue.Impact,strike.point,.85f,false,false,false,PixelHaetaeThunder.ImpactClip);
                    }
                }
                foreach(Unit enemy in units.Where(IsCombatTargetable).ToArray())
                {
                    if(cast.cancelled)break;
                    if(enemy.team==source.team||Vector3.Distance(strike.point,Flat(enemy.model.transform.position))>
                        TileSize*HaetaeRank3Rules.HeavenlyImpactRadiusTiles+enemy.collisionRadius||!cast.struck.Add(enemy))continue;
                    ApplyHaetaeRoarEnemy(cast,enemy);
                }
            }
            if(cast.completed&&!cast.recorded&&cast.strikes.All(strike=>strike.resolved))
            {
                cast.recorded=true;
                RecordSkillEvent(source,cast.heavenly?HaetaeRank3Rules.HeavenlyJudgment:
                    cast.bellow?HaetaeRank3Rules.JudgmentBellow:cast.skill.id+(cast.echo?"_echo":""),cast.hits,cast.damage);
                cast.CompleteCameraEffect();
            }
            float tail=cast.heavenly?HaetaeRank3Rules.HeavenlyDelay+PixelHaetaeThunder.Afterglow:cast.bellow?.48f:.18f;
            if(cast.time<cast.windup+(cast.bellow?HaetaeRank3Rules.BellowWaveDuration:HaetaeCombatRules.WaveDuration)+tail)continue;
            CancelHaetaeRoar(cast);haetaeRoars.RemoveAt(i);
        }
    }

    private static bool HaetaeBellowCone(HaetaeRoarCast cast,Unit unit)
    {
        Vector3 offset=Flat(unit.model.transform.position)-cast.origin;
        float distance=offset.magnitude;
        if(distance<=unit.collisionRadius)return true;
        float edgeAngle=Mathf.Asin(Mathf.Clamp01(unit.collisionRadius/distance))*Mathf.Rad2Deg;
        return Vector3.Angle(cast.forward,offset)<=HaetaeRank3Rules.BellowAngle*.5f+edgeAngle;
    }

    private void ApplyHaetaeBellowHit(HaetaeRoarCast cast,Unit enemy)
    {
        Unit source=cast.source;
        if(!cast.hitSound){cast.hitSound=true;PlayHaetaeSound(source,HaetaeSound.Hit,Rank3Center(enemy),.85f);}
        cast.damage+=Rank3Hit(source,enemy,HaetaeRank3Rules.BellowRatio*cast.scale);cast.hits++;
        if(!mlEpisodeActive)
        {
            cast.pressure?.Contact(Rank3Renderer(enemy),enemy.model.transform,cast.time);
            if(!cast.impactFeedback){cast.impactFeedback=true;combatCameraDirector?.ReportPressureBreath(cast.forward);}
        }
        ApplyHaetaeRoarEnemyIntrinsic(cast,enemy);
        if(!IsCombatActive(enemy)||PreyDisplacementImmune(enemy))return;
        if(DisplaceUnit(enemy,cast.forward,TileSize*HaetaeRank3Rules.BellowPushTiles,crowdPush:true))
        {
            enemy.haetaeBellowPushedBy=source;
            enemy.displacementDuration=HaetaeRank3Rules.BellowPushDuration;
            enemy.attackMovementLockTimer=Mathf.Max(enemy.attackMovementLockTimer,HaetaeRank3Rules.BellowPushDuration);
            enemy.spriteAnimation?.InterruptCurrentAction(true);
            enemy.spriteAnimation?.SetStruggling(true,()=>enemy.displacementProgress*enemy.displacementDuration);
            enemy.currentAction="haetae_pressure_pushed";
        }
        else Rank3Knockdown(source,enemy,HaetaeRank3Rules.BellowKnockdown);
    }

    private static void EndHaetaeBellowPush(Unit unit)
    {
        if(unit.haetaeBellowPushedBy==null)return;
        unit.haetaeBellowPushedBy=null;
        unit.spriteAnimation?.SetStruggling(false);
    }

    private bool HaetaeOppressionContains(HaetaeRoarCast cast,Unit unit)
    {
        return cast.oppression&&cast.released&&!cast.cancelled&&cast.generation==combatGeneration&&!battleEnding&&
            cast.time<cast.windup+HaetaeRank3Rules.OppressionDuration&&cast.source!=null&&
            IsCombatTargetable(unit)&&Vector3.Distance(Flat(cast.source.model.transform.position),
                Flat(unit.model.transform.position))<=cast.radius+unit.collisionRadius;
    }

    private bool HaetaeOppressionAffects(Unit unit,bool friendly)
    {
        if(!friendly&&unit.combatStatuses.DebuffImmune)return false;
        foreach(var cast in haetaeRoars)
            if((cast.source.team==unit.team)==friendly&&HaetaeOppressionContains(cast,unit))
                return !friendly||!unit.combatStatuses.RejectBenefit();
        return false;
    }

    private void UpdateHaetaeOppression(HaetaeRoarCast cast)
    {
        Unit source=cast.source;
        float age=cast.time-cast.windup;
        cast.origin=Flat(source.model.transform.position);
        if(!cast.completed&&age+.00001f>=HaetaeCombatRules.WaveDuration)
        {
            cast.completed=true;source.traitActivations++;
            if(source.haetaeRoar==cast)source.haetaeRoar=null;
        }
        // One hit per expanding front, including when one frame crosses several pulses.
        while(cast.pulse<HaetaeRank3Rules.OppressionPulses&&age+.00001f>=cast.pulse)
        {
            if(cast.soundPulse!=cast.pulse)
            {
                cast.soundPulse=cast.pulse;
                PlayHaetaeSound(source,HaetaeSound.Pulse,cast.origin,.7f);
            }
            float front=cast.radius*HaetaeCombatRules.WaveProgress(age-cast.pulse);
            foreach(var enemy in Rank3Enemies(source,cast.origin,front))
            {
                if(cast.cancelled)break;
                if(!IsCombatTargetable(enemy)||!cast.struck.Add(enemy))continue;
                cast.damage+=Rank3GroundHit(source,enemy,HaetaeRank3Rules.OppressionTickRatio*cast.scale);cast.hits++;
                ApplyHaetaeRoarEnemyIntrinsic(cast,enemy);
                if(!mlEpisodeActive)cast.aura?.Contact(Rank3Renderer(enemy),Rank3Center(enemy),age);
            }
            if(age+.00001f<cast.pulse+HaetaeCombatRules.WaveDuration)break;
            cast.pulse++;cast.struck.Clear();
        }
        if(cast.aura==null)return;
        cast.aura.Pose(age,cast.origin);
        foreach(var ally in units)
            if(ally.team==source.team&&!ally.augmentShadowClone&&HaetaeOppressionContains(cast,ally))
                cast.aura.Protect(Rank3Renderer(ally),Flat(ally.model.transform.position),age);
        cast.aura.EndProtectionFrame();
    }

    private void ApplyHaetaeRoarEnemy(HaetaeRoarCast cast,Unit enemy)
    {
        Unit source=cast.source;
        if(!cast.heavenly&&!cast.hitSound)
        {
            cast.hitSound=true;
            PlayHaetaeSound(source,HaetaeSound.Hit,Rank3Center(enemy),cast.echo?.45f:.75f);
        }
        float damage=ApplySkillFormulaDamage(source,enemy,cast.skill,false,0,cast.scale*
            (cast.heavenly?HaetaeRank3Rules.HeavenlyRatio/cast.skill.damageMultiplier:1f),!cast.echo);
        cast.damage+=damage;cast.hits++;source.skillDamage+=damage;source.skillHits++;
        if(!mlEpisodeActive)
        {
            PixelHaetaeJusticeRoar.Contact(Rank3Renderer(enemy),Rank3Center(enemy),cast.element,false);
            enemy.spriteAnimation?.PlayImpactRecoil(Flat(enemy.model.transform.position)-cast.origin,.09f);
        }
        if(!IsCombatActive(enemy)||enemy.combatStatuses.RejectHarmfulEffect())return;
        float reduction=cast.heavenly?HaetaeRank3Rules.HeavenlyAttackReduction:HaetaeCombatRules.AttackReduction;
        float duration=cast.heavenly?HaetaeRank3Rules.HeavenlyDuration:HaetaeCombatRules.AttackReductionDuration;
        enemy.familyAttackReductionRatio=Mathf.Max(enemy.familyAttackReductionRatio,reduction);
        enemy.familyAttackReductionTimer=Mathf.Max(enemy.familyAttackReductionTimer,duration);
        enemy.combatEffectIcons.Show(CombatEffectIconKind.AttackDown,duration);
        if(cast.heavenly)enemy.combatStatuses.ApplyStacks(CombatStatusKind.Silence,HaetaeRank3Rules.HeavenlySilence,1,1,source);
        if(cast.echo)return;
        ApplyHaetaeRoarEnemyIntrinsic(cast,enemy);
    }

    private void ApplyHaetaeRoarEnemyIntrinsic(HaetaeRoarCast cast,Unit enemy)
    {
        if(!IsCombatActive(enemy)||enemy.combatStatuses.RejectHarmfulEffect())return;
        Unit source=cast.source;
        if(HasRank3(source,HaetaeCombatRules.YeomhaeTraitId))
            enemy.combatStatuses.ApplyStacks(CombatStatusKind.Burning,HaetaeCombatRules.FlameDuration,1,1,source,
                SkillPower(source)*HaetaeCombatRules.FlameTickRatio*cast.scale,HaetaeCombatRules.FlameTickInterval,false);
        if(HasRank3(source,HaetaeCombatRules.MukryeongTraitId))
        {
            enemy.familyHealingReductionRatio=Mathf.Max(enemy.familyHealingReductionRatio,HaetaeCombatRules.KarmaHealingReduction);
            enemy.familyHealingReductionTimer=Mathf.Max(enemy.familyHealingReductionTimer,HaetaeCombatRules.KarmaDuration);
            enemy.familyRecoveryShieldReductionRatio=Mathf.Max(enemy.familyRecoveryShieldReductionRatio,HaetaeCombatRules.KarmaRecoveryShieldReduction);
            enemy.familyRecoveryShieldReductionTimer=Mathf.Max(enemy.familyRecoveryShieldReductionTimer,HaetaeCombatRules.KarmaDuration);
            RefreshHaetaeShieldGain(enemy);
            enemy.combatEffectIcons.Show(CombatEffectIconKind.HealingDown,HaetaeCombatRules.KarmaDuration);
        }
    }

    private void RefreshHaetaeShieldGain(Unit unit)
    {
        unit.combatStatuses.shieldGainMultiplier=1f-Mathf.Clamp01(unit.familyRecoveryShieldReductionRatio);
        if(unit.team==Team.Player)unit.combatStatuses.shieldGainMultiplier*=PermanentResearchSystem.RecoveryMultiplier(playerProfile);
    }

    private void ApplyHaetaeRoarAlly(HaetaeRoarCast cast,Unit ally)
    {
        Unit source=cast.source;int removed=CleanseAllHarmfulEffects(ally);
        if(removed>0&&!mlEpisodeActive)
        {
            if(!cast.cleanseSound)
            {
                cast.cleanseSound=true;
                PlayHaetaeSound(source,HaetaeSound.Cleanse,Rank3Center(ally),cast.echo?.45f:.75f);
            }
            PixelHaetaeJusticeRoar.Contact(Rank3Renderer(ally),Rank3Center(ally),cast.element,true);
            AddCombatFloatText(ally,"정화",new Color(1f,.94f,.65f),true);
        }
        if(cast.echo)return;
        if(HasRank3(source,HaetaeCombatRules.HaetaTraitId))
        {
            float before=ally.hp;
            Heal(ally,SkillSupportAmount(source,SkillPower(source)*HaetaeCombatRules.ClearHealingRatio*cast.scale));
            if(ally.hp>before&&!mlEpisodeActive)
                PixelCombatEffect.PlayHealingBurst(ally.spriteAnimation==null?ally.model.transform:ally.spriteAnimation.transform,Rank3Renderer(ally));
        }
        if(HasRank3(source,HaetaeCombatRules.GeumgangsuTraitId))
            ally.combatStatuses.ApplyShield(HaetaeCombatRules.DiamondShieldDuration,
                SkillSupportAmount(source,HaetaeCombatRules.DiamondShield(source.maxHp)*cast.scale),source);
    }

    private static void InterruptHaetaeRoar(HaetaeRoarCast cast)
    {
        if(cast==null)return;
        if(!cast.released){CancelHaetaeRoar(cast);return;}
        if(cast.source.haetaeRoar==cast)cast.source.haetaeRoar=null;
    }

    private static void CancelHaetaeRoar(HaetaeRoarCast cast)
    {
        if(cast==null||cast.cancelled)return;cast.cancelled=true;cast.effect?.Cancel();cast.aura?.Cancel();cast.pressure?.Cancel();
        cast.CompleteCameraEffect();
        foreach(var strike in cast.strikes)strike.effect?.Cancel();
        if(cast.source.haetaeRoar==cast)cast.source.haetaeRoar=null;
    }
    private void ClearHaetaeRoars()
    {foreach(var cast in haetaeRoars)CancelHaetaeRoar(cast);haetaeRoars.Clear();}
}
