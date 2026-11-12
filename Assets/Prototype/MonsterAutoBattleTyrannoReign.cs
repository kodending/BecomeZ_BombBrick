using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool TyrannoReignActive(Unit source)
    {
        var state=source==null?null:FamilyTranscendenceStateFor(source,false);
        return state!=null&&(state.tyrannoPending>0||state.tyrannoStompTime>=0f);
    }

    private bool BeginTyrannoReign(Unit source,SkillMasterRow skill)
    {
        if(source==null||skill==null||battleEnding||!IsCombatActive(source)||source.hp<=0f||
           !HasFamilyTranscendence(source)||!IsTyrannoFamily(source)||TyrannoReignActive(source)||
           !units.Any(u=>u.team!=source.team&&IsCombatTargetable(u)&&u.hp>0f))return false;
        source.skillTimer=source.skillCooldownDuration;source.skillUses++;
        source.currentAction="tyranno_reign_prepare";
        ClearRoute(source);StopBeforeAttack(source);SetMoving(source,false);
        ApplyPostSkillItemEffects(source);RecordSkillEvent(source,"tyranno_reign_start",0,0f);
        QueueTyrannoReign(source,combatGeneration);
        TickTyrannoReign(source,FamilyTranscendenceStateFor(source),0f);
        return true;
    }

    private void QueueTyrannoReign(Unit source,int generation)
    {
        if(generation!=combatGeneration||battleEnding||!IsCombatActive(source)||source.hp<=0f||
           !HasFamilyTranscendence(source)||!IsTyrannoFamily(source))return;
        var state=FamilyTranscendenceStateFor(source);
        if(state.tyrannoLastCast==source.skillUses)return;
        state.tyrannoLastCast=source.skillUses;state.tyrannoPending++;
    }

    private void TickTyrannoReign(Unit source,FamilyTranscendenceState state,float dt)
    {
        if(battleEnding||!IsCombatActive(source)||source.hp<=0f||!HasFamilyTranscendence(source))
        {CancelTyrannoReign(state);return;}
        if(state.tyrannoStompTime<0f)
        {
            if(state.tyrannoPending<=0||source.tyrannoTremorPending||source.tyrannoMaw!=null||source.tyrannoMarch!=null||
               source.spriteAnimation!=null&&source.spriteAnimation.IsActionLocked)return;
            state.tyrannoPending--;state.tyrannoStompTime=0f;state.tyrannoStompHit=false;
            QueueRank4Cinematic(source);
            PlayTyrannoSound(source,TyrannoSound.Warning,combatBounds.center);
            if(!mlEpisodeActive)
            {
                int order=units.Where(IsCombatActive).Select(Rank3Renderer).Where(r=>r!=null)
                    .Select(r=>r.sortingOrder).DefaultIfEmpty(0).Max()+5;
                state.tyrannoStomp=PixelTyrannoReignStomp.Create(combatBounds,Rank3Renderer(source),order,
                    source.definition.monsterId,source.ownedInstance!=null&&source.ownedInstance.isShiny,source.team==Team.Enemy,mainCamera);
                AddCombatFloatText(source,FamilyTranscendenceRules.DisplayName("tyranno"),new Color(1f,.65f,.25f),true);
            }
            return;
        }
        // Once summoned, the foot and the victims' knockdown use the same battle clock.
        state.tyrannoStompTime+=Mathf.Max(0f,dt);
        state.tyrannoStomp?.Advance(state.tyrannoStompTime);
        if(!state.tyrannoStompHit&&state.tyrannoStompTime>=FamilyTranscendenceRules.TyrannoStompWarning)
        {
            state.tyrannoStompHit=true;int hits=0;float damage=0f;
            PlayTyrannoSound(source,TyrannoSound.Reign,combatBounds.center);
            foreach(Unit enemy in units.Where(u=>u.team!=source.team&&IsCombatTargetable(u)).ToArray())
            {
                damage+=Rank3Hit(source,enemy,FamilyTranscendenceRules.TyrannoStompDamageRatio);hits++;
                if(!IsCombatActive(source)||source.hp<=0f){CancelTyrannoReign(state);return;}
                Rank3Knockdown(source,enemy,FamilyTranscendenceRules.TyrannoStompKnockdown);
                if(!mlEpisodeActive)
                {
                    var hit=PixelCombatEffect.PlayTyrannoTremorHit(enemy.model.transform,Rank3Renderer(enemy));
                    if(rank4Presentation?.request.source==source&&rank4Presentation.request.context.family=="tyranno")
                    {var main=hit.main;main.useUnscaledTime=true;}
                }
            }
            if(!mlEpisodeActive)combatCameraDirector?.ReportTyrannoGodStomp();
            source.traitActivations++;
            RecordSkillEvent(source,"trait_transcend_tyranno_tyrants_reign",hits,damage);
        }
        if(state.tyrannoStompTime>=FamilyTranscendenceRules.TyrannoStompEnd)
            CancelTyrannoReign(state,false);
    }

    private static void CancelTyrannoReign(FamilyTranscendenceState state,bool clearPending=true)
    {
        state.tyrannoStomp?.Cancel();state.tyrannoStomp=null;
        state.tyrannoStompTime=-1f;state.tyrannoStompHit=false;
        if(clearPending)state.tyrannoPending=0;
    }
}
