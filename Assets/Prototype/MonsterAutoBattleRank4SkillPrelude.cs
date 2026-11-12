using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private const float Rank4SkillPreludeDuration=.45f;
    private static MonsterAutoBattlePrototype rank4SkillPreludeOwner;
    private sealed class Rank4SkillPrelude
    {
        public Unit unit;
        public object key;
        public int generation;
        public float elapsed,savedSpeed;
        public double startedAt;
        public CombatSkillClock clock;
        public SpriteRenderer renderer;
        public bool sourceHidden;
        public Rank4CinematicVisibility spawnedVisibility;
    }
    private Rank4SkillPrelude rank4SkillPrelude;
    private Rank4SkillPreludeVisual rank4SkillPreludeVisual;
    private readonly HashSet<object> rank4SkillPreludeCompleted=new HashSet<object>();

    private static bool HasRank4SkillPrelude(Unit unit)=>unit!=null&&unit.augmentTranscended&&
        !unit.augmentShadowClone&&unit.spiritConcertOwner==null&&unit.mantisBroodOwner==null&&
        (HasFamilyTranscendence(unit)||IsRaptorFamily(unit));

    private bool Rank4PreludeSourceValid(Unit unit)=>unit?.model!=null&&
        (IsCombatActive(unit)&&unit.hp>0f||IsMushroomDeathBloomActive(unit));

    private bool TryBeginRank4SkillPrelude(Unit unit,object key)
    {
        // Cinematic queues own this cue; ordinary field casts keep their local anticipation.
        if(key==null||!HasRank4SkillPrelude(unit)||mlEpisodeActive)return false;
        if(rank4SkillPrelude!=null)return true;
        if(rank4SkillPreludeCompleted.Contains(key))return false;
        if(!battleStarted||battleEnding||paused||GameFrontend.BlocksGameplay||mainCamera==null||
           CombatSkillClock.Active||rank4Presentation!=null||LegacyRank4PresentationActive()||
           Time.timeScale<=0f||!Rank4PreludeSourceValid(unit))return false;
        SpriteRenderer renderer=Rank3Renderer(unit);
        if(renderer==null||renderer.sprite==null)return false;
        EndAllyPreparationSequence();EndSkillCameraPresentation();
        if(rank4SkillPreludeOwner!=null&&rank4SkillPreludeOwner!=this)
            rank4SkillPreludeOwner.CancelRank4SkillPrelude();
        var state=new Rank4SkillPrelude{unit=unit,key=key,generation=combatGeneration,
            savedSpeed=Time.timeScale,startedAt=Time.unscaledTimeAsDouble,
            clock=new CombatSkillClock(unit.model.transform),renderer=renderer,sourceHidden=renderer.forceRenderingOff};
        try
        {
            if(rank4SkillPreludeVisual==null)rank4SkillPreludeVisual=new Rank4SkillPreludeVisual(transform);
            rank4SkillPreludeVisual.Begin(renderer);
            rank4SkillPrelude=state;rank4SkillPreludeOwner=this;
            if(IsMushroomDeathBloomActive(unit)||unit.mantisMolt?.cocoon!=null)
            {
                state.spawnedVisibility=new Rank4CinematicVisibility();
                if(IsMushroomDeathBloomActive(unit))
                    foreach(var visual in MushroomDeathBloomVisualRoots(unit))state.spawnedVisibility.Track(visual);
                if(unit.mantisMolt?.cocoon!=null)state.spawnedVisibility.Track(unit.mantisMolt.cocoon.gameObject);
                state.spawnedVisibility.Hide();
            }
            renderer.forceRenderingOff=true;
            state.clock.Hold();Time.timeScale=0f;
            if(IsRaptorFamily(unit))BeginRaptorPreludeAudio(unit);
            else BeginFamilyRank4PreludeAudio(unit);
            return true;
        }
        catch
        {
            rank4SkillPrelude=state;CancelRank4SkillPrelude();throw;
        }
    }

    private bool UpdateRank4SkillPrelude()
    {
        var state=rank4SkillPrelude;if(state==null)return false;
        if(state.generation!=combatGeneration||!battleStarted||battleEnding||paused||GameFrontend.BlocksGameplay||
           mainCamera==null||state.renderer==null||!Rank4PreludeSourceValid(state.unit)||Time.timeScale!=0f)
        {CancelRank4SkillPrelude();return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelRank4SkillPrelude();paused=true;return true;}
        // A loading hitch must not consume the entire attention cue before it is drawn.
        state.elapsed+=Mathf.Min(.05f,Mathf.Max(0f,Time.unscaledDeltaTime));
        rank4SkillPreludeVisual.Draw(state.elapsed/Rank4SkillPreludeDuration);
        if(state.elapsed<Rank4SkillPreludeDuration)return true;
        rank4SkillPreludeCompleted.Add(state.key);
        FinishRank4SkillPrelude(true);
        return false;
    }

    private void CancelRank4SkillPrelude()=>FinishRank4SkillPrelude(false);

    private void FinishRank4SkillPrelude(bool completed)
    {
        var state=rank4SkillPrelude;rank4SkillPrelude=null;
        if(rank4SkillPreludeOwner==this)rank4SkillPreludeOwner=null;
        rank4SkillPreludeVisual?.Clear();
        if(state==null)return;
        if(!completed)
        {
            if(IsRaptorFamily(state.unit))raptorCinematicStage?.CancelPresentation();
            else CancelFamilyRank4CinematicAudio();
        }
        state.clock?.End();
        state.spawnedVisibility?.Restore();
        if(state.renderer!=null)state.renderer.forceRenderingOff=state.sourceHidden;
        double held=System.Math.Max(0d,Time.unscaledTimeAsDouble-state.startedAt);
        foreach(var presentation in localSkillPresentations)
            if(presentation.active&&presentation.pending!=null&&presentation.unit!=state.unit)
            {presentation.startedAt+=held;presentation.preparationObservedAt=Time.unscaledTimeAsDouble;}
        if(Time.timeScale==0f&&!paused&&!GameFrontend.BlocksGameplay)Time.timeScale=state.savedSpeed;
        UpdateHaetaeCinematicPresentation();
    }

    private void ResetRank4SkillPreludes()
    {CancelRank4SkillPrelude();rank4SkillPreludeCompleted.Clear();}

    private void DisposeRank4SkillPrelude()
    {ResetRank4SkillPreludes();rank4SkillPreludeVisual?.Dispose();rank4SkillPreludeVisual=null;}
}
