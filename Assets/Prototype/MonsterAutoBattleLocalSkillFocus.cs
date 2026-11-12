using System;
using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class LocalSkillPresentation
    {
        public Unit unit;
        public SpriteRenderer renderer;
        public SkillFocusSpriteMirror mirror;
        public PendingSkillFocus pending;
        public double startedAt;
        public double committedAt=-1d;
        public double recoveryAt=-1d;
        public double preparationObservedAt;
        public bool active,allyPreparationMember,allySkillEmphasis;
    }

    private readonly List<LocalSkillPresentation> localSkillPresentations=new List<LocalSkillPresentation>();

    private sealed class SkillCameraPresentation
    {
        public Unit unit;
        public SpriteRenderer renderer;
        public LocalSkillPresentation presentation;
        public int pendingEffects;
        public bool impactComplete;
    }

    private SkillCameraPresentation skillCameraPresentation;

    private const float SkillAnticipationDuration=.35f;
    private static float SkillPreparationDuration(Unit unit)
    {
        if(unit.team==Team.Player&&CombatReadabilityPresentation.Active)
            return CombatReadabilityPresentation.PreparationDuration;
        return unit.team==Team.Player?1f:SkillAnticipationDuration;
    }

    private LocalSkillPresentation SkillPresentationFor(Unit unit)
    {
        if(unit==null)return null;
        for(int i=0;i<localSkillPresentations.Count;i++)
            if(localSkillPresentations[i].active&&localSkillPresentations[i].unit==unit)
                return localSkillPresentations[i];
        return null;
    }

    private bool HasSkillCastPresentation(Unit unit)=>SkillPresentationFor(unit)!=null;
    private bool IsPreparingSkill(Unit unit)=>SkillPresentationFor(unit)?.pending!=null;

    private bool BeginSkillPreparation(PendingSkillFocus request)
    {
        if(!CanCommitSkillFocus(request)||HasSkillCastPresentation(request.unit))return false;
        CaptureFocusedCastPlan(request);
        BeginLocalSkillPresentation(request.unit);
        LocalSkillPresentation presentation=SkillPresentationFor(request.unit);
        if(presentation==null)return false;
        presentation.pending=request;presentation.committedAt=-1d;
        presentation.allySkillEmphasis=request.unit.team==Team.Player;
        BeginMeleeArrivalInertia(request.unit);
        request.unit.currentAction="skill_anticipation";
        request.unit.spriteAnimation.SetSkillAnticipating(true);
        presentation.mirror.Follow(presentation.renderer,SkillPreparationDuration(request.unit));
        JoinAllyPreparationSequence(presentation);
        if(request.unit.team==Team.Player&&!request.unit.rank3MantisReaper&&
           combatCameraDirector!=null&&combatCameraDirector.TryBeginAnticipationPan(
               presentation.renderer,true,()=>IsCombatActive(request.unit)))
            skillCameraPresentation=new SkillCameraPresentation
                {unit=request.unit,renderer=presentation.renderer,presentation=presentation};
        return true;
    }

    private void AttachCombinedSkillFocus(Unit unit,QueuedCombinedSkillGroup group)
    {
        LocalSkillPresentation presentation=SkillPresentationFor(unit);
        if(presentation?.pending!=null)presentation.pending.combinedGroup=group;
    }

    private void BeginLocalSkillPresentation(Unit unit)
    {
        if(mlEpisodeActive||!battleStarted||battleEnding||paused||GameFrontend.BlocksGameplay||
           !IsCombatActive(unit)||unit.spiritConcertOwner!=null||unit.augmentShadowClone||
           unit.mantisBroodOwner!=null||unit.spriteAnimation==null)return;
        SpriteRenderer renderer=unit.spriteAnimation.GetComponent<SpriteRenderer>();
        if(renderer==null||!renderer.enabled)return;
        LocalSkillPresentation presentation=null;
        for(int i=0;i<localSkillPresentations.Count;i++)
            if(localSkillPresentations[i].active&&localSkillPresentations[i].unit==unit)
            return;
        if(presentation==null)
            for(int i=0;i<localSkillPresentations.Count;i++)
                if(!localSkillPresentations[i].active&&
                   localSkillPresentations[i]!=skillCameraPresentation?.presentation)
                {presentation=localSkillPresentations[i];break;}
        if(presentation==null)
        {
            presentation=new LocalSkillPresentation();
            localSkillPresentations.Add(presentation);
        }
        if(presentation.mirror==null)
        {
            var foreground=new GameObject("Local Skill Focus Foreground");
            foreground.transform.SetParent(transform,false);
            presentation.mirror=foreground.AddComponent<SkillFocusSpriteMirror>();
        }
        presentation.unit=unit;presentation.renderer=renderer;
        presentation.startedAt=Time.unscaledTimeAsDouble;
        presentation.preparationObservedAt=presentation.startedAt;
        presentation.committedAt=Time.timeAsDouble;presentation.pending=null;
        presentation.recoveryAt=-1d;presentation.active=true;presentation.allySkillEmphasis=false;
        unit.spriteAnimation.SetSkillFocus(0f);
        if(unit.outline!=null)
            unit.outline.SetOutlined(true,true,CombatFlashPresentation.Peak(new Color(1f,.86f,.32f,1f)));
        presentation.mirror.Follow(renderer,0f);
        presentation.mirror.SetSpotlight(true);
        unit.raptorApexWings?.SetForeground(true);
        RefreshAllyPreparationEmphasis();
    }

    private void UpdateLocalSkillPresentations()
    {
        double now=Time.timeAsDouble,preparationNow=Time.unscaledTimeAsDouble;
        UpdateAllyPreparationSequence(preparationNow);
        try
        {
        for(int i=0;i<localSkillPresentations.Count;i++)
        {
            LocalSkillPresentation presentation=localSkillPresentations[i];
            if(!presentation.active)continue;
            if(!battleStarted||battleEnding||!IsCombatActive(presentation.unit))
            {ClearLocalSkillPresentation(presentation);continue;}
            if(paused||GameFrontend.BlocksGameplay||Time.timeScale<=0f)
            {
                if(presentation.pending!=null)
                    presentation.startedAt+=System.Math.Max(0d,preparationNow-presentation.preparationObservedAt);
                presentation.preparationObservedAt=preparationNow;
                continue;
            }
            presentation.preparationObservedAt=preparationNow;
            if(presentation.pending!=null)
            {
                if(!CanContinueSkillFocus(presentation.pending,false))
                {ClearLocalSkillPresentation(presentation);continue;}
                PendingSkillFocus request=presentation.pending;
                bool ready=PreparedSkillReleaseReady(request);
                presentation.unit.spriteAnimation.SetSkillAnticipating(ready);
                if(!ready||presentation.allyPreparationMember||
                   allyPreparationSequenceActive&&presentation.unit.team==Team.Player||
                   now<request.nextAttemptAt)continue;
                if(preparationNow-presentation.startedAt<SkillPreparationDuration(presentation.unit))continue;
                presentation.unit.spriteAnimation.SetSkillAnticipating(false);
                if(presentation.unit.currentAction=="skill_anticipation")presentation.unit.currentAction="idle";
                presentation.unit.spriteAnimation.SetSkillFocus(presentation.allySkillEmphasis?1f:0f);
                if(!ExecuteFocusedSkill(request))
                {
                    presentation.unit.spriteAnimation.SetSkillFocus(0f);
                    request.nextAttemptAt=now+.1d;
                    continue;
                }
                if(!presentation.active)continue;
                presentation.pending=null;
                presentation.committedAt=now;
            }
            float strength=1f;
            if(now-presentation.committedAt>=.12d&&!CommonSkillActionActive(presentation.unit))
            {
                if(presentation.recoveryAt<0d)
                {
                    presentation.recoveryAt=now;
                }
                strength=1f-Mathf.SmoothStep(0f,1f,(float)((now-presentation.recoveryAt)/.14d));
            }
            else presentation.recoveryAt=-1d;
            if(strength<=0f){ResetLocalSkillPresentation(presentation);continue;}
            presentation.unit.spriteAnimation?.SetSkillFocus(presentation.allySkillEmphasis?strength:0f);
        }
        }
        finally{FinishAllyPreparationUpdate();}
        UpdateSkillCameraPresentation();
    }

    private static float LocalSkillBodyEmphasis(LocalSkillPresentation presentation)
    {
        if(!presentation.allySkillEmphasis)return 0f;
        if(presentation.pending!=null)return presentation.allyPreparationMember?1f:0f;
        return presentation.recoveryAt<0d?1f:
            1f-Mathf.SmoothStep(0f,1f,(float)((Time.timeAsDouble-presentation.recoveryAt)/.14d));
    }

    private void ClearLocalSkillPresentation(LocalSkillPresentation presentation)
    {
        if(skillCameraPresentation?.presentation==presentation)EndSkillCameraPresentation();
        ResetLocalSkillPresentation(presentation);
    }

    private void ResetLocalSkillPresentation(LocalSkillPresentation presentation)
    {
        LeaveAllyPreparationSequence(presentation);
        if(presentation.unit!=null)
        {
            presentation.unit.raptorApexWings?.SetForeground(false);
            if(presentation.unit.spriteAnimation!=null)
            {
                presentation.unit.spriteAnimation.SetSkillAnticipating(false);
                presentation.unit.spriteAnimation.SetSkillFocus(0f);
            }
            if(presentation.unit.currentAction=="skill_anticipation")
                presentation.unit.currentAction="idle";
            if(presentation.unit.outline!=null)
                presentation.unit.outline.SetOutlined(false);
        }
        if(presentation.mirror!=null)presentation.mirror.Clear();
        presentation.active=false;presentation.allySkillEmphasis=false;
        presentation.pending=null;presentation.unit=null;presentation.renderer=null;
    }

    private void ClearLocalSkillPresentations()
    {
        EndAllyPreparationSequence();
        for(int i=0;i<localSkillPresentations.Count;i++)
            if(localSkillPresentations[i].active)ClearLocalSkillPresentation(localSkillPresentations[i]);
        EndSkillCameraPresentation();
        combatCameraDirector?.CancelAnticipationPan(true);
    }

    // Camera-only effect completion must not extend the caster's action lock or body emphasis.
    private Action HoldSkillCameraEffect(Unit source)
    {
        SkillCameraPresentation focus=skillCameraPresentation;
        if(focus==null||focus.unit!=source||focus.presentation!=SkillPresentationFor(source))return null;
        focus.pendingEffects++;
        bool completed=false;
        return ()=>
        {
            if(completed)return;
            completed=true;
            focus.pendingEffects--;
        };
    }

    private void CompleteSkillCameraImpact(Unit source)
    {
        if(skillCameraPresentation?.unit==source&&
           skillCameraPresentation.presentation==SkillPresentationFor(source))
            skillCameraPresentation.impactComplete=true;
    }

    private void UpdateSkillCameraPresentation()
    {
        SkillCameraPresentation focus=skillCameraPresentation;
        if(focus==null)return;
        if(!battleStarted||battleEnding||paused||GameFrontend.BlocksGameplay||!IsCombatActive(focus.unit)||
           combatCameraDirector==null||!combatCameraDirector.AnticipationPanActive)
        {EndSkillCameraPresentation();return;}
        if(focus.presentation.pending!=null)return;
        if(Time.timeAsDouble-focus.presentation.committedAt<.12d)return;
        if((IsRaptorFamily(focus.unit)&&focus.impactComplete)||
           (focus.pendingEffects==0&&(!focus.presentation.active||!CommonSkillActionActive(focus.unit))))
            EndSkillCameraPresentation();
    }

    private void EndSkillCameraPresentation()
    {
        SkillCameraPresentation focus=skillCameraPresentation;
        skillCameraPresentation=null;
        if(focus!=null)combatCameraDirector?.EndAnticipationPan(focus.renderer);
    }

}
