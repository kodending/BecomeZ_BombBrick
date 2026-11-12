using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private Rank4CinematicAudio familyRank4CinematicAudio;

    private bool FamilyRank4AudioEnabled(Unit source)=>!mlEpisodeActive&&source!=null&&
        (!CombatEffectTestConfiguration.Active||!CombatEffectTestSceneController.MuteEnemySounds||source.team==Team.Player);

    private void BeginFamilyRank4PreludeAudio(Unit source)
    {
        string family=source?.definition?.familyId;
        if(!Rank4CinematicAudio.Supports(family))return;
        if(familyRank4CinematicAudio==null)familyRank4CinematicAudio=new Rank4CinematicAudio(transform);
        familyRank4CinematicAudio.PlayPrelude(family,FamilyRank4AudioEnabled(source));
    }

    private void BeginFamilyRank4CinematicAudio(Unit source,string family)
    {
        if(!Rank4CinematicAudio.Supports(family))return;
        if(familyRank4CinematicAudio==null)familyRank4CinematicAudio=new Rank4CinematicAudio(transform);
        familyRank4CinematicAudio.Begin(family,FamilyRank4AudioEnabled(source));
    }

    private void TickFamilyRank4CinematicAudio(float elapsed)=>familyRank4CinematicAudio?.Tick(elapsed);
    private void EndFamilyRank4CinematicAudio()=>familyRank4CinematicAudio?.Finish();
    private void CancelFamilyRank4CinematicAudio()=>familyRank4CinematicAudio?.CancelPresentation();
    private void StopFamilyRank4CinematicAudio()=>familyRank4CinematicAudio?.Stop();
}
