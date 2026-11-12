public sealed partial class MonsterAutoBattlePrototype
{
    private bool BeginCommonSkillFocus(Unit unit,bool combinedCastTrigger)
    {
        if(!ShouldPresentCommonSkill(unit)||HasSkillCastPresentation(unit))return false;
        var request=new PendingSkillFocus{unit=unit,combinedCastTrigger=combinedCastTrigger};
        return BeginSkillPreparation(request);
    }

    private void UpdateCommonSkillFocus()=>UpdateLocalSkillPresentations();
    private void CancelCommonSkillFocus()
    {
        ResetRank4SkillPreludes();
        CancelRaptorApexCinematic();
        CancelFamilyRank4CinematicAudio();
        ClearLocalSkillPresentations();
    }

    private static bool CommonSkillActionActive(Unit unit)
    {
        // Released projectiles and ground effects outlive their caster's presentation.
        return unit.spriteAnimation!=null&&unit.spriteAnimation.IsSkillActionActive||
            unit.leaping||unit.shadowSlashing||unit.griffinSkyfalling||unit.griffinHunt!=null||
            unit.wolfStampede!=null||unit.wizardCygnusShower!=null||unit.crocodileAbyssDive!=null||unit.sabertoothDevour!=null||unit.fangLockTarget!=null||unit.tongueHarpoonTarget!=null||unit.crocodileBiteLockTimer>0f||
            unit.monkeyNimbus!=null||unit.wyvernBreath!=null&&!unit.wyvernBreath.finished||
            unit.haetaeRoar!=null&&!unit.haetaeRoar.completed&&!unit.haetaeRoar.cancelled||
            unit.highHeadSlam!=null&&unit.highHeadSlam.IsPlaying||unit.bearAvatarRoarPending||
            unit.tyrannoMarch!=null||unit.tyrannoMaw!=null&&!unit.tyrannoMaw.finished||
            unit.mantisGuillotine!=null&&!unit.mantisGuillotine.finished||
            unit.mantisReaperAction!=null&&unit.mantisReaperAction.kind==2||
            unit.mammothCharge!=null||unit.mammothToss!=null&&!unit.mammothToss.finished||
            unit.mammothBrace!=null||unit.mammothExpulsion!=null||unit.beetleFlip!=null||unit.beetleOverturn!=null;
    }

}
