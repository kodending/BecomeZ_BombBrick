using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool HasPendingHaetaeCinematic(Team team)
    {
        if(paused||GameFrontend.BlocksGameplay||!battleStarted||battleEnding)return false;
        var active=rank4Presentation?.request;
        if(active?.context.family=="haetae"&&active.source.team==team&&Rank4PresentationValid(active))return true;
        var prelude=rank4SkillPrelude?.key as Rank4PresentationRequest;
        if(prelude?.context.family=="haetae"&&prelude.source.team==team&&Rank4PresentationValid(prelude))return true;
        return rank4PresentationQueue.Any(r=>r.context.family=="haetae"&&r.source.team==team&&Rank4PresentationValid(r));
    }

    private void CaptureHaetaeGuardianReturn(Rank4StageContext context,Unit source)
    {
        context.guardianReturnVisible=false;
        if(!haetaeDevourEffects.TryGetValue(source.team,out var fx)||fx==null||fx.Ending)return;
        fx.SetCinematicHidden(true);fx.CompleteCinematicEntrance();
        SpriteRenderer body=fx.BodyRenderer;if(body?.sprite==null)return;
        var snapshot=new Rank4StageContext();CaptureRank4ReturnBody(snapshot,body);
        context.guardianReturnPosition=snapshot.returnPosition;
        context.guardianReturnHeight=snapshot.returnHeight;
        context.guardianReturnSprite=body.sprite;
        context.guardianReturnColor=body.color;
        context.guardianReturnVisible=body.enabled&&body.gameObject.activeSelf&&fx.gameObject.activeSelf;
    }

    private void UpdateHaetaeCinematicPresentation()
    {
        foreach(var entry in haetaeDevourEffects)
        {
            var fx=entry.Value;if(fx==null||fx.Ending)continue;
            bool pending=HasPendingHaetaeCinematic(entry.Key);
            var active=rank4Presentation;
            bool revealHead=pending&&active?.request.context.family=="haetae"&&active.request.source.team==entry.Key&&
                active.elapsed>=HaetaeMandateRank4Stage.NativeRevealTime;
            if(pending)
            {
                fx.SetCinematicHidden(true,revealHead);fx.CompleteCinematicEntrance();
            }
            else if(fx.PresentationHidden)
            {
                fx.CompleteCinematicEntrance();fx.SetCinematicHidden(false);
            }
            if((revealHead||!pending)&&!paused&&!GameFrontend.BlocksGameplay&&battleStarted&&!battleEnding&&
                haetaeDevourAppearancePending.Contains(entry.Key))
            {
                var living=haetaeDevourers.FirstOrDefault(u=>u.team==entry.Key&&IsCombatActive(u)&&u.hp>0f&&HasFamilyTranscendence(u));
                if(living!=null)
                {
                    haetaeDevourAppearancePending.Remove(entry.Key);
                    PlayHaetaeSound(living,HaetaeSound.Appear,fx.MouthPosition,.9f);
                }
            }
        }
    }
}
