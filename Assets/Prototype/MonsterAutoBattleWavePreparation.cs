using System;
using System.Collections;
using System.Linq;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool wavePreparationAcrossFrames;
    public bool IsSceneReady { get; private set; }

    private IEnumerator WarmupUnitBodyBounds()
    {
        // Share one preparation budget across the whole roster.
        var frames=units
            .Where(unit=>unit.spriteAnimation!=null)
            .SelectMany(unit=>unit.spriteAnimation.CombatFrames());
        if(units.Any(unit=>IsBearFamily(unit)&&HasFamilyTranscendence(unit)))
            frames=frames.Concat(new[]{"Idle","Run","Attack","SkillAttack","Death","Struggle"}
                .SelectMany(motion=>PixelBearUpperBodyHologram.SpiritFrames(motion,true)));
        yield return PixelMantisReaper.WarmupVisibleBodies(frames);
        yield return WarmupFrogCatchCinematic();
        yield return WarmupCrocodileAbyssDiveCinematic();
        yield return WarmupKingKongLeylineCinematic();
        yield return WarmupWizardCygnusCinematic();
        yield return WarmupBearAvatarCinematic();
        yield return WarmupWolfStampedeCinematic();
        yield return WarmupRemainingRank4Cinematics();
    }

    private IEnumerator InitializePlayerScene()
    {
        var priority=UnityEngine.Application.backgroundLoadingPriority;
        UnityEngine.Application.backgroundLoadingPriority=UnityEngine.ThreadPriority.Low;
        wavePreparationAcrossFrames=true;
        try
        {
            yield return null;
            yield return BombBrick.Game.Core.GameAudio.WarmupCombatSounds();
            yield return Rank4CinematicAudio.WarmupClips();
            yield return BombBrick.Game.Core.GameResourceCache.WarmupCombatEffects();
            WarmupReadabilityPresentation();
            yield return BombBrick.Game.Core.GameResourceCache.WarmupTexture("CombatCinematics/RaptorApex/ApexPrimordialStage");
            foreach(string prefix in new[]{"Velos","Velok","Raputo","Wilo"})
            {
                yield return BombBrick.Game.Core.GameResourceCache.WarmupTexture(RaptorApexCinematicStage.ResourceRoot+prefix+"Awakening_12Frames");
                yield return BombBrick.Game.Core.GameResourceCache.WarmupTexture(RaptorApexCinematicStage.ResourceRoot+prefix+"PredatorEye");
            }
            WarmupRaptorApexCinematic();
            yield return PixelMantisReaper.WarmupVisibleBodies(RaptorApexCinematicStage.AllFrames());
            // Allocate and render the stage behind loading, before the first awakening needs it.
            if(raptorCinematicStage==null)raptorCinematicStage=new RaptorApexCinematicStage(transform);
            yield return raptorCinematicStage.WarmupRender();
#if UNITY_EDITOR
            yield return WarmupEditorCombatMethods();
#endif
            // Register callbacks after their methods are prepared, before any units can use them.
            periodicStatusTick=HandlePeriodicStatusTick;
            ConfigureSkillExecutor();
            CreateMaterialsAndMeshes();
            CalculateMapOffset();
            yield return null;
            yield return CreateHexArenaRoutine();
            FrameCameraToMap();
            yield return null;
            bool spawned=false;
            yield return SpawnTeamsRoutine(value=>spawned=value);
            if(!spawned){enabled=false;yield break;}
            foreach(var selection in expeditionRun.augments)
            {
                if(selection==null||string.IsNullOrWhiteSpace(selection.augmentId))continue;
                yield return BombBrick.Game.Core.GameResourceCache.WarmupTexture(
                    "UI/AugmentIcons/"+selection.augmentId);
            }
            if(mainCamera!=null)
            {
                combatCameraDirector=mainCamera.GetComponent<CombatCameraDirector>()??
                    mainCamera.gameObject.AddComponent<CombatCameraDirector>();
                combatCameraDirector.Initialize(mainCamera,automaticFraming:false);
            }
            IsSceneReady=true;
        }
        finally
        {
            if(!IsSceneReady)enabled=false;
            wavePreparationAcrossFrames=false;
            UnityEngine.Application.backgroundLoadingPriority=priority;
        }
    }

    private void PreparePlayerWave(bool generateNewLayout, Action prepared)
    {
        if (wavePreparationAcrossFrames) return;
        if (!isActiveAndEnabled || !UnityEngine.Application.isPlaying)
        { PrepareNextWave(generateNewLayout); prepared?.Invoke(); return; }
        StartCoroutine(PreparePlayerWaveRoutine(generateNewLayout, prepared));
    }

    private IEnumerator PreparePlayerWaveRoutine(bool generateNewLayout, Action prepared)
    {
        wavePreparationAcrossFrames = true;
        try { yield return PrepareNextWaveRoutine(generateNewLayout); }
        finally { wavePreparationAcrossFrames = false; }
        prepared?.Invoke();
    }
}
