using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool readabilityGroundImpact;

    private static void WarmupReadabilityPresentation()
    {
        if(!CombatReadabilityPresentation.Local)return;
        ReadabilityAttackOriginEffect.Warmup();
        VoxelEnemyReaction.SpriteDeathFragments.PrepareVoxelResources();
    }

    private static bool ReadabilityUsesMeleeSwing(int style)=>style==0||style==1||style==3||style==4;

    private static bool ReadabilityUsesTargetBodyLunge(Unit source)=>
        !IsMonkeyFamily(source)&&!IsWizardMouseFamily(source)&&!IsMushroomFamily(source)&&
        !IsSpiritDeerFamily(source)&&!IsSpiderFamily(source)&&
        !IsWyvernFamily(source)&&source.tongueWhip==null&&source.definition.familyId!="frog";

    private int ReadabilityBasicAttackStyle(Unit source)
    {
        if(!CombatReadabilityPresentation.RefinedSkills||source==null||source.definition==null||source.spriteAnimation==null||
           source.spriteAnimation.AttackFrameCount==0)return -1;
        if(source.silkPuppet||source.ninjaOpeningStrikeReady||RagingHundredStrikesActive(source)||MountainColossusActive(source)||
           source.rank3MantisReaper||BeetleSweepActive(source)||GriffinRoyalReady(source))return -1;
        switch(source.definition.familyId)
        {
            case "sabertooth":case "ninja_cat":case "mantis":case "griffin":return 0;
            case "bear":case "kingkong":return 1;
            case "wizard_mouse":case "spirit_deer":case "wyvern":return 2;
            case "raptor":case "wolf":case "tyranno":case "haetae":return 3;
            case "crocodile":case "mammoth":case "beetle":return 4;
            case "monkey":return 5;
            case "frog":case "arachne":case "spider":case "mushroom":return 6;
            default:return -1;
        }
    }

    private void TrackReadabilityMeleeAttack(Unit source,Unit target)
    {
        var driver=source.spriteAnimation;
        if(!CombatReadabilityPresentation.RefinedSkills||driver==null||
           !driver.HasPendingBasicAttackHit||driver.CurrentMotionName!="Attack"||
           driver.BasicAttackAnimationTime>0f)return;
        int style=ReadabilityBasicAttackStyle(source);
        if(style<0)return;
        if(ReadabilityUsesTargetBodyLunge(source))
            driver.BeginReadabilityTargetAttackPose(source.model.transform,target.model.transform,style);
        else driver.BeginReadabilityBasicAttackPose(target.model.transform.position-source.model.transform.position,style);
        if(!ReadabilityUsesMeleeSwing(style))return;
        int generation=combatGeneration,sequence=source.basicAttackCount;
        int effectStyle=IsKingKongFamily(source)?ReadabilityAttackOriginEffect.StraightPunchStyle:style;
        var effect=ReadabilityAttackOriginEffect.TrackAttack(driver,Rank3Renderer(target),effectStyle,
            ()=>generation==combatGeneration&&IsCombatActive(source)&&source.basicAttackCount==sequence&&
                (driver.BasicAttackAnimationTime>=driver.BasicAttackHitTime||IsCombatActive(target)),
            ()=>{if(!IsRangedUnit(source))PlayCombatSound(source,CombatSoundCue.Attack,source.model.transform.position);});
        if(effect==null&&!IsRangedUnit(source))PlayCombatSound(source,CombatSoundCue.Attack,source.model.transform.position);
    }

    private void PlayReadabilityLaunch(Unit source,Unit target)
    {
        if(source==null||target==null||!CombatReadabilityPresentation.RefinedSkills)return;
        int style=ReadabilityBasicAttackStyle(source);
        if(style<0)return;
        Color tint=Color.white;
        if(IsWizardMouseFamily(source))tint=new Color(.88f,.83f,1f);
        else if(IsWyvernFamily(source))tint=PixelWyvernBreath.Tint(WyvernElement(source));
        else if(IsSpiritDeerFamily(source))tint=new Color(.6f,.95f,.85f);
        else if(IsMonkeyFamily(source))tint=new Color(.92f,.77f,.52f);
        else if(IsMushroomFamily(source))tint=new Color(.65f,.86f,.4f);
        else if(IsSpiderFamily(source))tint=new Color(.65f,.9f,1f);
        else if(source.definition.familyId=="frog")tint=new Color(.95f,.55f,.65f);
        ReadabilityAttackOriginEffect.PlayRelease(Rank3Renderer(source),Rank3Renderer(target),style,tint);
    }

#if UNITY_EDITOR
    public bool ReadabilityReadyToStart=>CombatReadabilityPreview.Active&&IsSceneReady&&!wavePreparationAcrossFrames&&
        !mapIntroductionActive&&!waveIntroActive&&!waveArrivalActive&&!battleStarted&&!battleEnding&&string.IsNullOrEmpty(resultText);

    private void ConfigureReadabilityRun()
    {
        if(!CombatReadabilityPreview.Active||expeditionRun==null)return;
        WarmupReadabilityPresentation();
        expeditionRun.runId="combat_readability_"+CombatReadabilityPreview.Seed;
        expeditionRun.activeBattleSeed=CombatReadabilityPreview.Seed==0?1:CombatReadabilityPreview.Seed;
    }

    public Dictionary<string,Vector2Int> CaptureReadabilityFormation()
    {
        if(!CombatReadabilityPreview.Active||!IsSceneReady||battleStarted||battleEnding||waveIntroActive||waveArrivalActive||draggingUnit||
           !string.IsNullOrEmpty(resultText))return null;
        return units.Where(unit=>unit.model!=null).ToDictionary(unit=>unit.stableId,unit=>unit.currentTile);
    }

    public bool RestoreReadabilityFormation(IReadOnlyDictionary<string,Vector2Int> formation)
    {
        if(!CombatReadabilityPreview.Active||!IsSceneReady||battleStarted||battleEnding||waveIntroActive||waveArrivalActive||!string.IsNullOrEmpty(resultText)||formation==null||
           formation.Count!=units.Count||formation.Values.Distinct().Count()!=formation.Count||
           units.Any(unit=>!formation.ContainsKey(unit.stableId)||!tiles.TryGetValue(formation[unit.stableId],out Tile tile)||!tile.walkable||
               unit.team==Team.Enemy&&tile.bench||unit.team==Team.Player&&!tile.bench&&tile.deploymentTeam!=Team.Player))return false;
        foreach(Unit unit in units)ReleaseUnitOccupancy(unit);
        foreach(Unit unit in units)
        {
            Vector2Int coordinate=formation[unit.stableId];
            OccupyUnit(unit,coordinate);
            unit.inStorage=tiles[coordinate].bench;
            unit.initialTile=coordinate;
            ApplyDeploymentTransform(unit);
        }
        RefreshAllTileVisuals();
        return true;
    }
#endif
}
