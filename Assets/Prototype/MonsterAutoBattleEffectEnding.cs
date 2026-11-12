using System;
using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private readonly List<VoxelEnemyReaction.SpriteDeathFragments> endingDeathBursts=
        new List<VoxelEnemyReaction.SpriteDeathFragments>();
    private readonly List<ParticleSystem> waveDeathParticles=new List<ParticleSystem>();

    private void TrackWaveDeathParticles(ParticleSystem particles)
    {
        if(particles!=null&&!mlEpisodeActive)waveDeathParticles.Add(particles);
    }

    private bool DeathPresentationPending()
    {
        for(int i=0;i<endingDeathBursts.Count;i++)
            if(endingDeathBursts[i]!=null&&endingDeathBursts[i].isActiveAndEnabled)return true;
        for(int i=0;i<waveDeathParticles.Count;i++)
            if(waveDeathParticles[i]!=null&&waveDeathParticles[i].gameObject.activeInHierarchy&&
               waveDeathParticles[i].IsAlive(true))return true;
        return false;
    }

    private void BeginWaveOutcomePresentation()
    {
        endingDeathBursts.Clear();
        if(!mlEpisodeActive)
            foreach(var root in gameObject.scene.GetRootGameObjects())
            foreach(var effect in root.GetComponentsInChildren<VoxelEnemyReaction.SpriteDeathFragments>())
                if(effect.IsDeathBurst&&effect.isActiveAndEnabled)endingDeathBursts.Add(effect);
        waveOutcomeStage=WaveOutcomeStage.Death;
        waveOutcomeTimer=0f;
        if(mlEpisodeActive||!DeathPresentationPending())BeginWaveOutcomeMessage();
    }

    private void BeginWaveOutcomeMessage()
    {
        endingDeathBursts.Clear();waveDeathParticles.Clear();
        waveOutcomeStage=WaveOutcomeStage.Message;
        waveOutcomeTimer=WaveOutcomeMessageDuration;
        GameAudio.SetBattlePhase(false);
        if(!mlEpisodeActive)GameAudio.Play(pendingResult=="아군 승리"?GameAudio.Cue.Victory:GameAudio.Cue.Defeat);
    }

    private void EndBattleUnitAction(Unit unit)
    {
        if(!IsCombatActive(unit))return;
        unit.raptorApexWings?.SetAwakened(false);
        Vector3 endingInertia=InertiaBlocked(unit)?Vector3.zero:unit.inertiaVelocity;
        if(!InertiaBlocked(unit)&&endingInertia.sqrMagnitude<=.000001f&&unit.moving)
            endingInertia=unit.velocity;
        float endingDeceleration=unit.inertiaDeceleration;
        InterruptPositionalAction(unit);
        CleanupRank3Unit(unit);
        if(unit.mammothToss!=null)EndMammothToss(unit,unit.mammothToss);
        if(unit.mammothCharge!=null)EndMammothCharge(unit,unit.mammothCharge);
        if(unit.tyrannoMarch!=null)EndTyrannoMarch(unit,unit.tyrannoMarch);
        if(unit.tyrannoMaw!=null)EndTyrannoMaw(unit,unit.tyrannoMaw);
        if(unit.mantisGuillotine!=null)EndMantisGuillotine(unit,unit.mantisGuillotine);
        if(unit.fangLockTarget!=null)FinishFangLock(unit,false);
        unit.spriteAnimation?.ResetForReuse();
        unit.spriteAnimation?.SetSkillPoseScale(Vector3.one);
        unit.attackMovementLockTimer=0f;
        unit.displacing=false;
        unit.hexStepping=false;
        unit.target=null;
        unit.velocity=Vector3.zero;
        unit.moving=false;
        ClearRoute(unit);
        ReleaseAttackSlot(unit);
        Vector3 ground=Flat(unit.model.transform.position);
        unit.model.transform.position=ground+Vector3.up*unit.visualHeightOffset;
        unit.lastGroundPosition=ground;
        if(unit.ring!=null)unit.ring.transform.position=ground+Vector3.up*0.025f;
        unit.currentAction="idle";
        BeginUnitInertia(unit,endingInertia);
        if(unit.inertiaVelocity.sqrMagnitude>0f&&endingDeceleration>0f)unit.inertiaDeceleration=endingDeceleration;
        if(unit.spriteAnimation==null&&unit.animator!=null&&unit.animator.isActiveAndEnabled)
        {
            unit.animator.SetBool("Moving",false);
            unit.animator.speed=1f;
            if(unit.animator.HasState(0,Animator.StringToHash("Idle")))unit.animator.Play("Idle",0,0f);
        }
    }

    // Only temporary combat visuals belong here, never ordinary actors or rank outlines.
    private static readonly HashSet<Type> EndingEffectTypes=new HashSet<Type>
    {
        typeof(ReadabilityAttackOriginEffect),typeof(MonsterRankUpFeedback),
        typeof(CombatEffectEndMarker),typeof(Rank3FxLifetime),typeof(PixelCombatEffect),typeof(PixelBearRoarPressure),
        typeof(PixelFreezeShell),typeof(PixelKingKongLeylineAura),typeof(PixelTranscendenceFlameOutline),typeof(PixelGroundCastTelegraph),
        typeof(PixelHaetaeRoarWave),typeof(PixelWolfHuntAfterimage),typeof(PixelGroundWebReveal),
        typeof(PixelTyrannoLavaFieldEffect),typeof(PixelSpiritSanctuaryEffect),typeof(PixelNinjaLineSlash),
        typeof(PixelBearUpperBodyHologram),typeof(PixelBearKingRoarSpirit),typeof(PixelWizardStarFan),
        typeof(PixelStarProjectile),typeof(PixelWyvernRain),typeof(PixelWyvernPressure),
        typeof(PixelWyvernHeavenBreath),typeof(PixelWyvernBreath),typeof(PixelWyvernBolt),
        typeof(PixelWolfStampede),typeof(PixelWizardSupernova),typeof(PixelWizardPentagramField),
        typeof(PixelWizardCygnusConstellation),typeof(PixelWizardCygnusMeteorShower),typeof(PixelUnitShield),typeof(PixelTyrannoStomp),
        typeof(PixelSabertoothDevour),typeof(PixelFrogGrandSlam),typeof(PixelCrocodileAbyssDive),typeof(PixelTyrannoReignStomp),typeof(PixelTyrannoMaw),typeof(PixelSpiritGuardian),
        typeof(PixelSpiritBlessing),typeof(PixelSpiritConcert),typeof(PixelSilkPuppet),
        typeof(PixelSpiderSilkBind),typeof(PixelSpiderCaptureNet),
        typeof(PixelSilkQueenDomain),typeof(PixelParasiticCrown),typeof(PixelNinjaSubstitution),
        typeof(PixelNinjaScrollOrbit),typeof(PixelNinjaMoonMark),typeof(PixelNinjaMoonFx),typeof(PixelNinjaSpacetime),
        typeof(PixelNinjaEightGate),typeof(PixelMycoking),typeof(PixelMushroomMigration),
        typeof(PixelMushroomGas),typeof(PixelMushroomDeathBloom),typeof(PixelMonkeyReturn),typeof(PixelMonkeyNimbus),typeof(PixelMonkeyStorm),
        typeof(PixelMonkeyMortar),typeof(PixelMantisReaper),typeof(PixelMantisGroundCut),
        typeof(PixelMantisCocoon),typeof(PixelMammothWallRush),typeof(PixelMammothExpulsion),typeof(PixelMammothChainImpact),
        typeof(PixelMammothCharge),typeof(PixelMammothBrace),typeof(PixelMammothBoulder),
        typeof(PixelHaetaeThunder),typeof(PixelHaetaeOppression),typeof(PixelHaetaeJusticeRoar),
        typeof(PixelHaetaeDevourer),typeof(PixelHaetaeBellow),typeof(PixelGriffinRoyal),
        typeof(PixelGriffinDive),typeof(PixelGriffinCrosswind),typeof(PixelEightfoldWeb),
        typeof(PixelCrocodileCrushingSweep),typeof(PixelBeetleSweep),typeof(PixelBeetleOverturn),
        typeof(PixelBeetleGravity),typeof(PixelBearFightingSpirit),typeof(PixelBearAvatarDeparture)
    };

    private void FadeBattleEffects()=>PrepareBattleEffectsForEnd(true);
    private void PreserveBattleEffects()=>PrepareBattleEffectsForEnd(false);
    private void ClearBattleEffects()
    {
        endingDeathBursts.Clear();waveDeathParticles.Clear();
        PrepareBattleEffectsForEnd(false,true);
    }

    private void PrepareBattleEffectsForEnd(bool fade,bool clear=false)
    {
        var roots=new HashSet<GameObject>();
        var permanent=new HashSet<GameObject>();
        foreach(var unit in units)
        {
            if(unit.transcendenceFlameParticles!=null)permanent.Add(unit.transcendenceFlameParticles.gameObject);
            if(unit.returnMarkAura!=null)permanent.Add(unit.returnMarkAura.gameObject);
        }
        var tongues=new List<FrogTongueWhip>();
        foreach(var sceneRoot in gameObject.scene.GetRootGameObjects())
        foreach(var effect in sceneRoot.GetComponentsInChildren<MonoBehaviour>())
        {
            if(effect==null||!effect.gameObject.activeInHierarchy)continue;
            if(clear&&effect is VoxelEnemyReaction.SpriteDeathFragments)
            {roots.Add(effect.gameObject);continue;}
            if(effect is PixelTranscendenceFlameOutline&&effect.GetComponent<CombatEffectEndMarker>()==null)continue;
            if(effect is FrogTongueWhip tongue)tongues.Add(tongue);
            if(permanent.Contains(effect.gameObject)||!EndingEffectTypes.Contains(effect.GetType()))continue;
            if(effect is PixelSpiritBlessing blessing)blessing.PrepareBattleEndFade();
            roots.Add(effect.gameObject);
        }
        // Collect first, since detaching an effect changes its ancestry.
        var topLevel=new List<GameObject>();
        foreach(var root in roots)
        {
            bool nested=false;
            for(var parent=root.transform.parent;parent!=null;parent=parent.parent)
                if(roots.Contains(parent.gameObject)){nested=true;break;}
            if(!nested)topLevel.Add(root);
        }
        foreach(var tongue in tongues)
            if(tongue!=null)
            {
                if(clear)tongue.Cancel();
                else if(fade)tongue.FadeAtBattleEnd();
                else tongue.FinishAtBattleEnd();
            }
        foreach(var root in topLevel)
        {
            if(clear)
            {
                var stampede=root.GetComponent<PixelWolfStampede>();
                if(stampede!=null){stampede.Cancel();continue;}
                var pooled=root.GetComponent<PixelCombatEffect>();
                if(pooled!=null)pooled.ReleaseAfterBattleEndFade();
                else
                {
                    root.SetActive(false);
                    if(Application.isPlaying)Destroy(root);else DestroyImmediate(root);
                }
            }
            else if(fade)CombatEffectEndFade.Begin(root);
            else CombatEffectEndPlayback.Begin(root,EndingEffectTypes);
        }
    }
}
