using BombBrick.Combat;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class CrocodileAbyssDiveState
    {
        public MonsterAutoBattlePrototype owner;
        public Unit target;
        public SkillMasterRow skill;
        public int generation,revision,targetRevision;
        public int visualFrame=-1;
        public float time,damageScale,holdUntil,previousRootAt,previousRootCastAt;
        public float visualHeight=.7f;
        public bool contacted,captured,visualCaptured,hit;
        public bool liveRollPlayed,liveSurgePlayed;
        public CombatStatusEffect captureRoot,previousRoot;
        public Vector3 center;
        public PixelCrocodileAbyssDive effect;
        public CrocodileAbyssFloorVisibility sourceFloor,targetFloor;
    }

    private sealed class CrocodileAbyssFloorVisibility
    {
        public Renderer[] renderers;
        public bool[] hidden;
    }

    private static bool UsesCrocodileAbyssDive(Unit source)=>
        IsCrocodileFamily(source)&&HasFamilyTranscendence(source);

    private bool BeginCrocodileAbyssDive(Unit source,SkillMasterRow skill)
    {
        if(!UsesCrocodileAbyssDive(source)||!IsCombatActive(source)||skill==null||
           source.crocodileAbyssDive!=null||source.crocodileAbyssHeldBy!=null)return false;
        Unit target=CommittedSkillTarget(source)??source.target;
        if(!IsCombatTargetable(target)||target.team==source.team)return false;
        bool charge=MonsterTraitRuntime.Has(source.activeTraitIds,"trait_skill_charge_bite");
        float reach=charge?TileSize*3f+source.collisionRadius+target.collisionRadius:
            AttackDistance(source,target)+TileSize*.35f;
        if(!IsWithinCommittedSkillReach(source,target,reach))return false;

        var state=new CrocodileAbyssDiveState {owner=this,target=target,skill=skill,
            generation=combatGeneration,revision=source.positionRevision,
            damageScale=source.skillTraitDamageMultiplier,
            center=(Flat(source.model.transform.position)+Flat(target.model.transform.position))*.5f};
        source.crocodileAbyssDive=state;
        source.crocodileRollTarget=null;
        source.skillTimer=source.skillCooldownDuration;
        source.skillUses++;source.traitActivations++;
        source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,CrocodileAbyssDiveRules.Duration);
        ClearUnitInertia(source);ClearRoute(source);StopUnitMovement(source);
        RotateUnit(source,target.model.transform.position-source.model.transform.position,1f);
        source.spriteBillboard?.SetFacingDirection(target.model.transform.position-source.model.transform.position,true);
        source.currentAction="crocodile_abyss_prepare";
        ApplyPostSkillItemEffects(source);
        ApplyCrocodileAbyssIntrinsics(source,target);
        if(!mlEpisodeActive)
        {
            PlayFamilyTranscendenceFeedback(source,"crocodile");
            SpriteRenderer body=Rank3Renderer(source);
            if(body!=null&&body.sprite!=null)state.visualHeight=PixelMantisReaper.VisibleBody(body.sprite).size.y*Mathf.Abs(body.transform.lossyScale.y);
            source.spriteAnimation?.PlaySkillAttackWithHold(0,
                CrocodileAbyssDiveRules.HitTime*CombatStatFormula.CombatAnimationRate,1f,null,()=>state.time);
            float width=Mathf.Max(TileSize*1.4f,Vector3.Distance(Flat(source.model.transform.position),
                Flat(target.model.transform.position))+CombatBodyRadius(source)+CombatBodyRadius(target));
            state.effect=PixelCrocodileAbyssDive.Create(Rank3Renderer(source),Rank3Renderer(target),state.center,width);
        }
        return true;
    }

    private void ApplyCrocodileAbyssIntrinsics(Unit source,Unit target)
    {
        if(MonsterTraitRuntime.Has(source.activeTraitIds,CrocodileCombatRules.AgerTraitId))
        {
            source.crocodileToughHideTimer=Mathf.Max(source.crocodileToughHideTimer,
                GateHaetaeBenefit(source,CrocodileAbyssDiveRules.Duration));
            source.traitActivations++;
        }
        if(MonsterTraitRuntime.Has(source.activeTraitIds,CrocodileCombatRules.KrogalTraitId))
        {
            source.crocodileShield=GateHaetaeBenefit(source,
                source.crocodileShield+source.maxHp*CrocodileCombatRules.GuardianShieldRatio*GuardianShieldMultiplier(source),CombatBenefitKind.Shield);
            source.crocodileShieldTimer=Mathf.Max(source.crocodileShieldTimer,CrocodileCombatRules.GuardianShieldDuration);
            source.traitActivations++;
        }
        if(MonsterTraitRuntime.Has(source.activeTraitIds,CrocodileCombatRules.TarvilTraitId)&&
           !target.combatStatuses.RejectHarmfulEffect())
        {
            target.crocodileDeathMarkSource=source;
            target.crocodileDeathMarkTimer=CrocodileCombatRules.UnderworldMarkDuration;
            source.traitActivations++;
        }
    }

    private void AdvanceCrocodileAbyssDive(Unit source,float dt)
    {
        var state=source?.crocodileAbyssDive;
        if(state==null)return;
        if(battleEnding||state.generation!=combatGeneration||!IsCombatActive(source)||source.hp<=0f||
           source.positionRevision!=state.revision||!IsCombatActive(state.target)||
           source.sabertoothDevouredBy!=null||source.frogGrandSlamHeldBy!=null||source.crocodileAbyssHeldBy!=null||
           source.combatStatuses.Has(CombatStatusKind.Knockdown)||source.combatStatuses.Has(CombatStatusKind.Fear)||
           source.combatStatuses.Has(CombatStatusKind.Airborne)||SpiderSilkBound(source)||source.giraffeStunTimer>0f)
        {EndCrocodileAbyssDive(source);return;}
        if(state.captured&&!CrocodileAbyssCaptureValid(source,state))
            ReleaseCrocodileAbyssCapture(source,state,CrocodileAbyssNaturalRelease(state));
        if(paused||dt<=0f||source.combatStatuses.Has(CombatStatusKind.Freeze)||IsCrocodileAbyssDiveCinematicPending(source))return;

        float remaining=dt;
        while(source.crocodileAbyssDive==state&&remaining>.000001f)
        {
            float boundary=!state.contacted?CrocodileAbyssDiveRules.ContactTime:
                !state.hit?CrocodileAbyssDiveRules.HitTime:CrocodileAbyssDiveRules.Duration;
            float step=Mathf.Min(remaining,Mathf.Max(.000001f,boundary-state.time));
            state.time=Mathf.Min(CrocodileAbyssDiveRules.Duration,state.time+step);
            remaining-=step;
            if(!state.contacted&&state.time>=CrocodileAbyssDiveRules.ContactTime)
            {
                state.contacted=true;
                CaptureCrocodileAbyssVictim(source,state);
                if(source.crocodileAbyssDive!=state)return;
                if(state.effect!=null)state.effect.Advance(state.time,state.center);
                if(!mlEpisodeActive&&state.captured&&QueueCrocodileAbyssDiveCinematic(source,state))return;
                if(!mlEpisodeActive)PlayCrocodileSound(source,CrocodileSound.AbyssEnter,state.center);
            }
            if(!state.liveRollPlayed&&state.time>=.36f)
            {state.liveRollPlayed=true;if(!mlEpisodeActive&&!crocAbyssAdvancingCast)PlayCrocodileSound(source,CrocodileSound.AbyssRoll,state.center);}
            if(!state.liveSurgePlayed&&state.time>=.70f)
            {state.liveSurgePlayed=true;if(!mlEpisodeActive&&!crocAbyssAdvancingCast)PlayCrocodileSound(source,CrocodileSound.AbyssSurge,state.center);}
            if(state.captured&&!CrocodileAbyssCaptureValid(source,state))
                ReleaseCrocodileAbyssCapture(source,state,CrocodileAbyssNaturalRelease(state));
            UpdateCrocodileAbyssPose(source,state);
            if(state.effect!=null)state.effect.Advance(state.time,state.center);
            source.currentAction=state.time<CrocodileAbyssDiveRules.ContactTime?"crocodile_abyss_prepare":
                state.time<CrocodileAbyssDiveRules.HitTime?"crocodile_abyss_dive":"crocodile_abyss_recover";
            if(!state.hit&&state.time>=CrocodileAbyssDiveRules.HitTime)ResolveCrocodileAbyssHit(source,state);
            if(source.crocodileAbyssDive==state&&state.time>=CrocodileAbyssDiveRules.Duration)EndCrocodileAbyssDive(source);
        }
    }

    private void CaptureCrocodileAbyssVictim(Unit source,CrocodileAbyssDiveState state)
    {
        Unit target=state.target;
        if(!IsCombatTargetable(target)||PreyDisplacementImmune(target)||target.crocodileAbyssHeldBy!=null)return;
        float duration=HostileStatusDuration(target,CrocodileAbyssDiveRules.HitTime-state.time);
        if(duration<=0f)return;
        InterruptPositionalAction(target);
        if(source.crocodileAbyssDive!=state||!IsCombatActive(target))return;
        state.previousRoot=target.combatStatuses.Get(CombatStatusKind.Root);
        state.previousRootAt=battleElapsed;state.previousRootCastAt=state.time;
        if(state.previousRoot!=null)target.combatStatuses.Remove(CombatStatusKind.Root);
        state.captureRoot=target.combatStatuses.ApplyTimed(CombatStatusKind.Root,duration,source);
        if(state.captureRoot.source!=source||state.captureRoot.remaining<=0f)return;
        state.captured=true;state.visualCaptured=true;state.targetRevision=target.positionRevision;
        state.holdUntil=state.time+state.captureRoot.remaining;
        target.crocodileAbyssHeldBy=source;
        target.displacing=false;target.preyDraggedBy=null;
        ClearRoute(target);StopUnitMovement(target);
        target.spriteAnimation?.SetFrozen(true);
    }

    private bool CrocodileAbyssCaptureValid(Unit source,CrocodileAbyssDiveState state)=>
        state!=null&&state.captured&&IsCombatActive(source)&&state.generation==combatGeneration&&
        source.positionRevision==state.revision&&IsCombatActive(state.target)&&state.target.crocodileAbyssHeldBy==source&&
        state.target.positionRevision==state.targetRevision&&!PreyDisplacementImmune(state.target)&&
        ReferenceEquals(state.target.combatStatuses.Get(CombatStatusKind.Root),state.captureRoot)&&
        state.time<state.holdUntil;

    private bool UpdateCrocodileAbyssVictim(Unit target)
    {
        Unit source=target.crocodileAbyssHeldBy;
        var state=source?.crocodileAbyssDive;
        if(state==null||!IsCombatActive(source)||state.generation!=combatGeneration||
           !CrocodileAbyssCaptureValid(source,state))
        {ReleaseCrocodileAbyssVictimCore(target,state!=null&&CrocodileAbyssNaturalRelease(state));return false;}
        StopUnitMovement(target);target.spriteAnimation?.SetFrozen(true);
        target.currentAction="crocodile_abyss_captured";
        return true;
    }

    private void UpdateCrocodileAbyssPose(Unit source,CrocodileAbyssDiveState state)
    {
        if(mlEpisodeActive)return;
        float film=Mathf.Lerp(.7f,4f,Mathf.InverseLerp(CrocodileAbyssDiveRules.ContactTime,CrocodileAbyssDiveRules.HitTime,state.time));
        float turns=PixelCrocodileAbyssDive.CinematicRollTurns(film);
        int phase=Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(turns,1f)*4f),0,3);
        int frame=state.hit?1:film<1.35f||film>=2.8f?0:film>=2.64f?2:phase==0?2:phase==1?3:phase==2?4:2;
        ShowCrocodileAbyssFrame(source,state,frame);
        SpriteRenderer renderer=Rank3Renderer(source);
        if(renderer==null||renderer.sprite==null||source.spriteBillboard==null)return;
        float facing=renderer!=null&&renderer.flipX?-1f:1f;
        float height=state.visualHeight;
        if(state.visualCaptured&&state.target.spriteBillboard!=null)
        {
            SpriteRenderer captured=Rank3Renderer(state.target);
            if(captured!=null&&captured.sprite!=null)
                height=Mathf.Max(height,PixelMantisReaper.VisibleBody(captured.sprite).size.y*Mathf.Abs(captured.transform.lossyScale.y));
        }
        float depth=CrocodileAbyssDiveRules.DepthAt(state.time)*height*1.35f;
        float roll=Mathf.Sin(turns*Mathf.PI*2f)*18f*facing;
        float surge=Mathf.Sin(Mathf.PI*Mathf.InverseLerp(.72f,CrocodileAbyssDiveRules.HitTime,state.time))*state.visualHeight*.65f;
        Vector3 mouth=CrocodileAbyssMouthLocal(source,renderer);
        Vector3 sourceOffset=Vector3.down*depth+Vector3.up*surge;
        if(state.visualCaptured&&!CrocodileAbyssVisualValid(state))ClearCrocodileAbyssVisual(state);
        if((state.visualCaptured||!state.contacted)&&state.target.spriteBillboard!=null)
        {
            SpriteRenderer prey=Rank3Renderer(state.target);
            if(prey!=null&&prey.sprite!=null)
            {
                Vector3 bite=PixelMantisReaper.VisibleBody(prey.sprite).center;
                if(prey.flipX)bite.x=-bite.x;if(prey.flipY)bite.y=-bite.y;
                Vector3 gap=state.target.spriteBillboard.UnposedPoint(bite)-source.spriteBillboard.UnposedPoint(mouth);
                float contact=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.06f,CrocodileAbyssDiveRules.ContactTime,state.time));
                contact*=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(CrocodileAbyssDiveRules.HitTime+.055f,CrocodileAbyssDiveRules.Duration,state.time));
                sourceOffset+=gap*(contact*.75f);
                if(state.visualCaptured)state.target.spriteBillboard.SetRollPose(roll,bite,-gap*(contact*.25f)+Vector3.down*depth+Vector3.up*surge);
            }
        }
        source.spriteBillboard.SetRollPose(roll,mouth,sourceOffset);
        bool submerged=!state.hit&&state.time>CrocodileAbyssDiveRules.ContactTime;
        source.spriteBillboard.SetGroundClip(submerged,state.center.y+.025f);
        if(state.visualCaptured)state.target.spriteBillboard?.SetGroundClip(submerged,state.center.y+.025f);
        bool fullySubmerged=!state.hit&&CrocodileAbyssDiveRules.FullySubmergedAt(state.time);
        SetCrocodileAbyssSubmerged(source,fullySubmerged,ref state.sourceFloor);
        if(state.visualCaptured)
            SetCrocodileAbyssSubmerged(state.target,fullySubmerged&&CrocodileAbyssCaptureValid(source,state),ref state.targetFloor);
    }

    private static void SetCrocodileAbyssSubmerged(Unit unit,bool submerged,ref CrocodileAbyssFloorVisibility saved)
    {
        if(submerged)
        {
            if(saved==null)
            {
                var renderers=unit.ring==null?System.Array.Empty<Renderer>():unit.ring.GetComponentsInChildren<Renderer>(true);
                saved=new CrocodileAbyssFloorVisibility{renderers=renderers,hidden=new bool[renderers.Length]};
                for(int i=0;i<renderers.Length;i++)saved.hidden[i]=renderers[i].forceRenderingOff;
            }
            unit.spriteBillboard?.SetGroundFullySubmerged(true);
            foreach(Renderer renderer in saved.renderers)if(renderer!=null)renderer.forceRenderingOff=true;
            return;
        }
        if(saved==null)return;
        unit.spriteBillboard?.SetGroundFullySubmerged(false);
        for(int i=0;i<saved.renderers.Length;i++)if(saved.renderers[i]!=null)saved.renderers[i].forceRenderingOff=saved.hidden[i];
        saved=null;
    }

    private static void ShowCrocodileAbyssFrame(Unit source,CrocodileAbyssDiveState state,int index)
    {
        if(state.visualFrame==index||source.spriteAnimation==null)return;
        state.visualFrame=index;
        source.spriteAnimation.HoldSkillAttackFrame(index,Mathf.Max(.04f,CrocodileAbyssDiveRules.Duration-state.time)*CombatStatFormula.CombatAnimationRate);
        Sprite frame=source.spriteAnimation.GetSkillAttackFrame(index);SpriteRenderer renderer=Rank3Renderer(source);
        if(frame!=null&&renderer!=null)renderer.sprite=frame;
    }

    private static Vector3 CrocodileAbyssMouthLocal(Unit source,SpriteRenderer renderer)
    {
        // Registered to open 00 and upright closed 01; Ager's shiny shares their pixel geometry.
        bool closed=source.crocodileAbyssDive!=null&&source.crocodileAbyssDive.hit;
        Vector2 pixel=closed?(source.definition.monsterId=="kimbul"?new Vector2(169f,162f):
            source.definition.monsterId=="krogal"?new Vector2(168f,160f):
            source.definition.monsterId=="tarvil"?new Vector2(168f,166f):new Vector2(163f,165f)):
            source.definition.monsterId=="kimbul"?new Vector2(160f,146f):
            source.definition.monsterId=="krogal"?new Vector2(166f,144f):
            source.definition.monsterId=="tarvil"?new Vector2(166f,153f):new Vector2(158f,152f);
        if(!closed&&source.crocodileAbyssDive!=null&&source.crocodileAbyssDive.visualFrame>=2)
            pixel=new Vector2(165f,source.crocodileAbyssDive.visualFrame==3?156f:166f);
        Sprite sprite=renderer.sprite;
        Vector3 point=new Vector3((pixel.x/256f*sprite.rect.width-sprite.pivot.x)/sprite.pixelsPerUnit,
            ((1f-pixel.y/256f)*sprite.rect.height-sprite.pivot.y)/sprite.pixelsPerUnit,0f);
        if(renderer.flipX)point.x=-point.x;if(renderer.flipY)point.y=-point.y;
        return point;
    }

    private void ResolveCrocodileAbyssHit(Unit source,CrocodileAbyssDiveState state)
    {
        state.hit=true;
        Unit target=state.target;
        ReleaseCrocodileAbyssCapture(source,state,true);
        UpdateCrocodileAbyssPose(source,state);
        if(!IsCombatActive(target)||target.team==source.team||
           !IsCombatTargetable(target)&&target.crocodileAbyssHeldBy==null)return;
        if(!mlEpisodeActive)
        {
            PlayCrocodileSound(source,CrocodileSound.AbyssCrush,target.model.transform.position);
            combatCameraDirector?.ReportCrocodileAbyssCrush();
        }
        float dealt=ApplySkillDamage(source,target,new SkillDamageFormulaResult(
            source.damage*CrocodileAbyssDiveRules.DamageRatio*state.damageScale,0f,
            CrocodileAbyssDiveRules.DefenseIgnoreRatio),false,applyTraitMultiplier:false,allowHitSlide:false);
        source.skillHits++;source.skillDamage+=dealt;
        if(dealt>0f&&IsCombatActive(source)&&MonsterTraitRuntime.Has(source.activeTraitIds,CrocodileCombatRules.KimbulTraitId))
        {Heal(source,CrocodileCombatRules.GoldenFeastHealing(dealt));source.traitActivations++;}
        RecordSkillEvent(source,"crocodile_abyss_dive",1,dealt);
    }

    private void ResolveCrocodileAbyssDiveEcho(Unit source,Unit target,float scale)
    {
        var state=source?.crocodileAbyssDive;
        bool ownCapture=state!=null&&state.target==target&&CrocodileAbyssCaptureValid(source,state);
        if(!IsCombatActive(source)||!IsCombatTargetable(target)&&!ownCapture||source.team==target.team)return;
        float dealt=ApplySkillDamage(source,target,new SkillDamageFormulaResult(
            source.damage*CrocodileAbyssDiveRules.DamageRatio*Mathf.Max(0f,scale),0f,
            CrocodileAbyssDiveRules.DefenseIgnoreRatio),false,allowHitSlide:false);
        source.skillDamage+=dealt;source.skillHits++;
        RecordSkillEvent(source,"crocodile_abyss_dive_echo",1,dealt);
    }

    private bool CrocodileAbyssNaturalRelease(CrocodileAbyssDiveState state)
    {
        Unit source=state.target.crocodileAbyssHeldBy;
        if(!IsCombatActive(source)||source.crocodileAbyssDive!=state||state.generation!=combatGeneration||
           source.positionRevision!=state.revision)return false;
        var root=state.target.combatStatuses.Get(CombatStatusKind.Root);
        return state.captureRoot!=null&&(state.captureRoot.remaining<=0f||state.time>=state.holdUntil)&&
            (root==null||ReferenceEquals(root,state.captureRoot))&&CrocodileAbyssVisualValid(state);
    }

    private static bool CrocodileAbyssVisualValid(CrocodileAbyssDiveState state)=>
        IsCombatActive(state.target)&&state.target.positionRevision==state.targetRevision&&
        !PreyDisplacementImmune(state.target)&&state.target.sabertoothDevouredBy==null&&
        state.target.frogGrandSlamHeldBy==null&&(state.hit&&state.target.crocodileAbyssHeldBy==null||
            ReferenceEquals(state.target.crocodileAbyssHeldBy,state.captureRoot?.source));

    private static void ClearCrocodileAbyssVisual(CrocodileAbyssDiveState state)
    {
        if(!state.visualCaptured)return;
        state.visualCaptured=false;
        Unit target=state.target;
        if(target.crocodileAbyssHeldBy==null||target.crocodileAbyssHeldBy.crocodileAbyssDive==state)
        {
            SetCrocodileAbyssSubmerged(target,false,ref state.targetFloor);
            target.spriteBillboard?.SetGroundClip(false);
        }
        if(IsCombatActive(target)&&target.positionRevision==state.targetRevision&&
           target.sabertoothDevouredBy==null&&target.frogGrandSlamHeldBy==null&&target.crocodileAbyssHeldBy==null)
            ResetDeathRollPose(target);
    }

    private void ReleaseCrocodileAbyssVictim(Unit target)=>ReleaseCrocodileAbyssVictimCore(target,false);

    private void ReleaseCrocodileAbyssCapture(Unit source,CrocodileAbyssDiveState state,bool preserveVisual)
    {
        if(state.target.crocodileAbyssHeldBy==source)ReleaseCrocodileAbyssVictimCore(state.target,preserveVisual);
        else
        {
            state.captured=false;
            if(!preserveVisual||state.target.crocodileAbyssHeldBy!=null)ClearCrocodileAbyssVisual(state);
        }
    }

    private void ReleaseCrocodileAbyssVictimCore(Unit target,bool preserveVisual)
    {
        Unit source=target?.crocodileAbyssHeldBy;
        if(source==null)return;
        var state=source.crocodileAbyssDive;
        target.crocodileAbyssHeldBy=null;
        if(state!=null&&state.target==target)
        {
            state.captured=false;
            var root=target.combatStatuses.Get(CombatStatusKind.Root);
            bool owned=ReferenceEquals(root,state.captureRoot);
            bool expired=root==null&&state.captureRoot!=null&&state.captureRoot.remaining<=0f;
            if(owned)target.combatStatuses.Remove(CombatStatusKind.Root);
            // A cleanse must also remove the saved root instead of resurrecting it.
            if((owned||expired)&&state.previousRoot!=null&&IsCombatActive(target))
            {
                float duration=state.previousRoot.remaining-Mathf.Max(battleElapsed-state.previousRootAt,state.time-state.previousRootCastAt);
                if(duration>0f)
                {
                    var restored=target.combatStatuses.ApplyTimed(CombatStatusKind.Root,duration,
                        state.previousRoot.source,state.previousRoot.magnitude);
                    if(restored.source==state.previousRoot.source)
                    {restored.remaining=duration;restored.activeHolds=state.previousRoot.activeHolds;}
                }
            }
        }
        if(state!=null&&!preserveVisual)
        {ClearCrocodileAbyssVisual(state);UpdateCrocodileAbyssPose(source,state);}
        if(!preserveVisual)ResetDeathRollPose(target);
        if(IsCombatActive(target))target.spriteAnimation?.SetFrozen(target.combatStatuses.Has(CombatStatusKind.Freeze)||SpiderSilkBound(target));
        target.decisionTimer=0f;
    }

    private void EndCrocodileAbyssDive(Unit source,bool immediate=false)
    {
        var state=source?.crocodileAbyssDive;
        if(state==null)return;
        ReleaseCrocodileAbyssCapture(source,state,false);
        ClearCrocodileAbyssVisual(state);
        SetCrocodileAbyssSubmerged(source,false,ref state.sourceFloor);
        source.spriteBillboard?.SetGroundClip(false);
        source.crocodileAbyssDive=null;
        CancelCrocodileAbyssDiveCinematicFor(source);
        if(state.effect!=null)state.effect.Cancel(immediate);
        source.crocodileToughHideTimer=0f;
        source.crocodileBiteLockTimer=0f;source.attackMovementLockTimer=0f;source.decisionTimer=0f;
        ResetDeathRollPose(source);
        source.spriteAnimation?.InterruptCurrentAction(true);
        if(IsCombatActive(source))source.currentAction="idle";
    }

    private static void InterruptCrocodileAbyssDive(Unit unit)
    {
        if(unit.crocodileAbyssHeldBy!=null)
            unit.crocodileAbyssHeldBy.crocodileAbyssDive?.owner.ReleaseCrocodileAbyssVictim(unit);
        unit.crocodileAbyssDive?.owner.EndCrocodileAbyssDive(unit);
    }

    private void ResumeCrocodileAbyssDiveFromCinematic(Unit source)
    {
        var state=source?.crocodileAbyssDive;
        if(state==null||state.hit)return;
        bool previous=crocAbyssAdvancingCast;crocAbyssAdvancingCast=true;
        try {AdvanceCrocodileAbyssDive(source,Mathf.Max(0f,CrocodileAbyssDiveRules.HitTime-.001f-state.time));}
        finally {crocAbyssAdvancingCast=previous;}
    }
}
