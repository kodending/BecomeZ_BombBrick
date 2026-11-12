using UnityEngine;

public static class NinjaCatRank3Rules
{
    public const string ShadowlessCross="trait_ninja_cat_shadowless_cross_annihilation";
    public const string BlackMoonHunt="trait_ninja_cat_black_moon_great_hunt";
    public const string EightGateFormation="trait_ninja_cat_eight_gate_explosion_formation";
    public const float CrossHitRatio=.7f, CrossPathRatio=.5f, ExecutionRatio=2.2f;
    public const float ExecutionDefenseIgnore=.5f, ExecutionMissingHpRatio=.12f;
    public const int CrossHitCount=4, BlackMoonAttackCount=6;
    public const float CrossFirstHitTime=.36f, CrossStepInterval=.09f;
    public const float CrossFinisherArrival=.68f, CrossFinisherHitTime=.78f;
    public const float CrossFinisherHold=.05f, CrossRecoveryEnd=.93f;
    public const float BlackMoonRadiusTiles=3.5f, BlackMoonCutRatio=.5f, BlackMoonBurstRatio=1f;
    public const float BlackMoonExecutionRatio=1.8f;
    public const float BlackMoonStepInterval=.18f, BlackMoonContactDelay=.055f;
    public const float BlackMoonVanishDelay=.125f, BlackMoonSheatheDuration=.22f;
    public const int KunaiCount=8, KunaiMaximumHits=3;
    public const float KunaiRadiusTiles=3f, KunaiPathRatio=.6f;
    public const float KunaiExplosionRadiusTiles=2.5f, KunaiExplosionRatio=2.4f;
    public const float GateInstallTime=.2f, GateInstallInterval=.025f;
    public const float GateArrivalTime=.48f, GateChargeDuration=.35f;
    public const float GateLaunchTime=GateArrivalTime+GateChargeDuration;
    public const float GateFlightDuration=.18f, GateBlastTime=GateLaunchTime+GateFlightDuration;
    public const float GateRecoveryEnd=GateBlastTime+.16f, GateKnockdownDuration=.8f;

    public static float GateFlightProgress(float time)=>Mathf.Clamp01((time-GateLaunchTime)/GateFlightDuration);
    public static Vector3 GatePoint(Vector3 center,int index)=>center+
        Quaternion.AngleAxis(22.5f+index*45f,Vector3.up)*Vector3.forward*
        MonsterAutoBattlePrototype.TileSize*KunaiRadiusTiles;
}

public static class TyrannoRank3Rules
{
    public const string MassExtinction="trait_tyranno_mass_extinction_tremor";
    public const string TyrantMarch="trait_tyranno_tyrant_great_march";
    public const string PrimordialMaw="trait_tyranno_primordial_great_maw";
    public const float ExtinctionWindup=1.2f,ExtinctionWaveDuration=.25f;
    public const float ExtinctionCoreTiles=2f,ExtinctionRadiusTiles=3.5f;
    public const float ExtinctionCoreRatio=4.2f,ExtinctionOuterRatio=3f;
    public const float ChargeReduction=.5f;
    public const float MarchWindup=.6f,MarchDuration=1f,MarchSlamWindup=.18f;
    public const float MarchStrideDuration=MarchDuration/2f,MarchFootContact=.34f;
    public const float MarchContact=MarchWindup+MarchDuration+MarchSlamWindup;
    public const float MarchEnd=MarchContact+.25f;
    public const float MarchDistanceTiles=4f,MarchWidthTiles=2f,MarchCollisionRatio=2f;
    public const float MarchFinalRadiusTiles=2f,MarchFinalRatio=2.6f,MarchKnockdown=.8f;
    public const float MawRadiusTiles=3f,MawCloseRadiusTiles=2f,MawOpenRatio=1.2f,MawCloseRatio=3f;
    public const float MawDefenseIgnore=.3f, MawMissingHpRatio=.1f;
    public const float MawMissingHpCap=1.5f,MawKnockdown=.8f,MawPullTiles=.25f;
    public const float MawWindup=1f,MawPullDuration=.35f,MawCloseTime=MawWindup+MawPullDuration;
    public const float MawVisualLead=.35f;
    public const float MawEnd=MawCloseTime+.38f;

    public static float MawBonusDamage(float attack,float missingHealth,float scale)=>
        Mathf.Min(Mathf.Max(0f,missingHealth)*MawMissingHpRatio,Mathf.Max(0f,attack)*MawMissingHpCap)*Mathf.Max(0f,scale);

    public static float MarchTravelProgress(float elapsed)
    {
        float time=Mathf.Clamp(elapsed,0f,MarchDuration);
        int stride=Mathf.Min(1,Mathf.FloorToInt(time/MarchStrideDuration));
        float advance=Mathf.Clamp01((time-stride*MarchStrideDuration)/MarchFootContact);
        return (stride+Mathf.SmoothStep(0f,1f,advance))*.5f;
    }
}

public static class MonkeyRank3Rules
{
    public const string MountainCollapse="trait_monkey_hwagwasan_collapse";
    public const string FiveOrbJuggling="trait_monkey_celestial_five_orb_juggling";
    public const string MyriadReturn="trait_monkey_myriad_streams_return";
    public const float BaseProjectileRatio=.45f;
    public const float FruitBounceRatio=.6f, FruitFinalRatio=1.2f;
    public const float FruitBounceRadiusTiles=1f, FruitFinalRadiusTiles=2f;
    public const float FruitStepTiles=1f, FruitSearchTiles=3f;
    public const float MortarRatio=.75f, MortarFinalRatio=1.5f;
    public const float MortarRadiusTiles=1.5f, MortarFinalRadiusTiles=2f;
    public const float ReturnOutboundRatio=.2f, ReturnInboundRatio=.4f, ReturnFinalRatio=.9f;
    public const float ReturnThrowInterval=.1f, BoomerangOutDuration=.58f, ReturnFlightDuration=.42f;
    public const float BoomerangRangeTiles=5f, BoomerangCurveTiles=.9f, BoomerangRadiusTiles=.2f;
    public const int ReturnMaximumCombo=4;
}

public static class MantisRank3Rules
{
    public const string DivineGuillotine="trait_mantis_divine_guillotine";
    public const string MyriadEyes="trait_mantis_myriad_eye_predation";
    public const string ReaperEmergence="trait_mantis_reaper_emergence";
    public const float GuillotineBladeRatio=1.8f;
    public const float GuillotineLengthTiles=6f, GuillotineWidthTiles=1.25f;
    public const float GuillotineLaunch=.3f, GuillotineInterval=.12f, GuillotineFlight=.2f;
    public const float GuillotineLand=.84f, GuillotineEnd=.94f, GuillotineScarDuration=.6f;

    public static Vector3 GuillotineDirection(Vector3 forward,int cut)=>
        Quaternion.AngleAxis(cut==0?45f:-45f,Vector3.up)*forward.normalized;

    public static bool InsideGuillotine(Vector3 point,Vector3 center,Vector3 direction,
        float length,float width,float bodyRadius)
    {
        Vector3 delta=point-center;delta.y=0f;
        float along=Vector3.Dot(delta,direction);
        float end=Mathf.Max(0f,Mathf.Abs(along)-length*.5f);
        float side=Mathf.Max(0f,Vector3.Cross(delta,direction).magnitude-width*.5f);
        return end*end+side*side<=bodyRadius*bodyRadius;
    }
    public const float MyriadPrimaryRatio=1.6f, MyriadOtherRatio=1f;
    public const float MyriadRadiusTiles=7f;
    public const int MyriadBladeCount=7;
    public const float MyriadWaveInterval=.12f,MyriadFlightSpeed=24f;

    public static float MyriadAngle(int wave,int index)=>wave==0?(index-3)*20f:
        index==3?0f:index<3?(index-3)*20f+10f:(index-4)*20f+10f;

    public static Vector3 MyriadPoint(Vector3 origin,Vector3 forward,float angle,float range,float progress)
    {
        float spread=Mathf.Lerp(.55f,1f,Mathf.SmoothStep(0f,1f,progress/.28f));
        return origin+Quaternion.AngleAxis(angle*spread,Vector3.up)*forward*(range*progress);
    }
    public const float ReaperHealMissingRatio=.2f, ReaperAttackBonus=.3f;
    public const float ReaperSpeedBonus=.25f, ReaperScale=1.2f;
    public const float ReaperSkillRatio=1.2f,ReaperBasicRatio=.6f;
    public const float ReaperMoltEnd=.6f,ReaperFlightEnd=.7f,ReaperBasicEnd=.42f;
    public const float ReaperLaunch=.12f,ReaperCutInterval=.1f,ReaperCutFlight=.3f;
    public const float ReaperFlightTiles=4f,ReaperCooldown=6f;
}

public static class MushroomRank3Rules
{
    // Keep the saved trait ID when replacing its behavior and display name.
    public const string SporeResonance="trait_mushroom_subterranean_mycelial_kingdom";
    public const string ParasiticCrown="trait_mushroom_parasitic_crown";
    public const string MycokingDescent="trait_mushroom_mycoking_descent";
    public const float ResonanceRadiusTiles=1f,ResonanceDamageRatio=.4f;
    public const float ResonanceInterval=.7f,ResonanceWindup=.08f;
    public const int ResonanceBurstLimit=6;
    public const float CrownDuration=3f,CrownTickRatio=.5f;
    public const int CrownTickCount=3;
    public const float CrownFinalRatio=2.4f,CrownFinalRadiusTiles=1.5f;
    public const float MycokingSpawnRatio=.8f, MycokingRadiusTiles=2.5f;
    public const float MycokingDuration=8f, MycokingHealthRatio=.4f;
    public const float MycokingBreathInterval=2f, MycokingGasRatio=.2f;
}

public static class MammothRank3Rules
{
    public const string MountainToss="trait_mammoth_annihilation_plow";
    public const string WallRush="trait_mammoth_ice_age_wall";
    public const string MountainBrace="trait_mammoth_primordial_migration";
    public const float TossRangeTiles=5f,TossSpeedTiles=8f,TossBodyRadiusTiles=.6f;
    public const float TossHitRatio=2.2f,TossBlastRatio=1.4f,TossBlastTiles=1.5f;
    public const float TossRelease=.8f,TossCastEnd=1.05f,TossFirstBounce=.26f;
    public const float WallRushWindup=.45f,WallRushArrival=1.27f,WallRushEnd=1.55f;
    public const float WallRushNoseTiles=.45f,WallRushBlastTiles=1.5f,WallRushRatio=1f;
    public static float WallRushContactTime(int wall)=>.61f+wall*.23f;
    public static float WallRushTravel(float time)
    {
        float t=Mathf.Max(0f,time-WallRushWindup),from=0f;
        for(int i=0;i<3;i++)
        {
            float to=i+1-WallRushNoseTiles,duration=i==0?.16f:.18f;
            if(t<duration)return Mathf.Lerp(from,to,Mathf.Pow(t/duration,1.5f));
            t-=duration;if(t<.05f)return to;t-=.05f;from=to;
        }
        return Mathf.Lerp(from,MammothCombatRules.MaximumChargeDistanceTiles,Mathf.Clamp01(t/.15f));
    }
    public const float BraceWindup=.35f,BraceGuardEnd=3.35f,BraceStrike=3.5f,BraceEnd=3.9f;
    public const float BraceRadiusTiles=2.5f,BraceSweepTiles=2f,BraceShareRatio=.3f,BraceTransferRatio=.5f;
    public const float BraceDamageRatio=2f,BraceHalfAngleCos=.5f;
}

public static class BeetleRank3Rules
{
    public const string MassTheft="trait_beetle_mass_theft";
    public const string GoldenCoffin="trait_beetle_golden_carapace_coffin";
    public const string ZeroGravity="trait_beetle_zero_gravity_domain";
    public const float MassDuration=6f,MassSweepRadiusTiles=1.5f,MassSplashRatio=.8f;
    public const float GravityRadiusTiles=2f, GravityDuration=1.5f;
    public const float GravityRise=.2f,GravityDrop=.12f;
    public const float GravityFall=GravityRise+GravityDuration,GravityLanding=GravityFall+GravityDrop;
    public const float GravityFallRatio=1.8f;
}

public static class SpiritDeerRank3Rules
{
    public const string LifeTithe="trait_spirit_deer_life_tithe_decree";
    public const string InviolableSanctuary=
        "trait_spirit_deer_inviolable_sanctuary";
    public const string LegacySoulSeparation=
        "trait_spirit_deer_soul_separation";
    public const string MyriadConcert="trait_spirit_deer_myriad_spirit_concert";
    public const float RadiusTiles=SpiritDeerCombatRules.MaximumSanctuaryRadiusTiles;
    public const float Duration=5f;
    public const float LifeReturnRatio=.35f,LifeReturnDuration=5f;
    public const float LifeReturnShieldRatio=.08f,LifeReturnShieldDuration=3f;
    public const float InviolableHitCap=.15f;
    public const float ConcertSkillScale=.5f;
    public const float ConcertDurationScale=.5f;

    public static float CapHitDamage(float damage,float maximumHealth,float capRatio)
    {
        return Mathf.Min(Mathf.Max(0f,damage),
            Mathf.Max(0f,maximumHealth)*Mathf.Clamp01(capRatio));
    }
}

public static class GriffinRank3Rules
{
    public const string TyphoonDive="trait_griffin_typhoon_dive";
    public const string RaptorHunt="trait_griffin_raptor_hunt";
    public const string RoyalDescent="trait_griffin_royal_descent";
    public const float TyphoonRadiusTiles=2.5f, TyphoonRatio=2.8f;
    public const float HuntRangeTiles=7f;
    public const float RoyalDuration=6f, RoyalWindRatio=.8f;
    public const int RoyalCharges=3;
    public const float RoyalWindTiles=3f, RoyalWindWidthTiles=1.5f, RoyalWindDuration=.24f;
    public const float RoyalWingLead=.14f, RoyalWingRelease=.18f;
    public static float RoyalWindProgress(float age)
    {
        float t=UnityEngine.Mathf.Clamp01(age/RoyalWindDuration);
        return t*(1.55f-.55f*t);
    }
}

public static class WyvernRank3Rules
{
    public const string CataclysmBreath="trait_wyvern_cataclysm_breath";
    public const string WingSweep="trait_wyvern_wing_sweep";
    public const string DragonCore="trait_wyvern_dragon_core_bombardment";
    public const float PressureWindup=.5f, PressureDuration=.2f;
    public const float PressureRatio=3f, PressureRangeTiles=4f, PressureAngle=90f;
    public static float PressureFront(float age)=>1f-Mathf.Pow(1f-Mathf.Clamp01(age/PressureDuration),2f);
    public const int RainCount=6;
    public const float RainWindup=.35f, RainWarning=.8f, RainInterval=.26f, RainFall=.55f;
    public const float RainRangeTiles=5.5f, RainAreaTiles=4f, RainRadiusTiles=1f, RainRatio=.7f;
    public static float RainImpact(int index)=>RainWindup+RainWarning+index*RainInterval;
    public const float CoreCharge=.4f, CoreRangeTiles=4f, CoreDuration=2f;
    public const float CoreDefenseIgnore=.2f, CoreTurnDegrees=240f;
    public const int CoreHits=6;
    public static float CoreHitTime(int index)=>CoreDuration*(index+1)/CoreHits;
    public static float CoreHitRatio(int index)=>index<4?.3f+index*.1f:index==4?.8f:1f;
}

public static class HaetaeRank3Rules
{
    public const string HeavenlyJudgment="trait_haetae_heavenly_judgment_roar";
    public const string DivineOppression="trait_haetae_divine_beast_oppression";
    public const string JudgmentBellow="trait_haetae_judgment_bellow";
    public const float HeavenlyRadiusTiles=3.5f, HeavenlyRatio=2.4f;
    public const float HeavenlyWindup=.45f,HeavenlyDelay=.35f,HeavenlyImpactRadiusTiles=.6f;
    public const float HeavenlyAttackReduction=.2f, HeavenlyDuration=4f,HeavenlySilence=1f;
    public const float OppressionRadiusTiles=3f, OppressionDuration=5f;
    public const float OppressionTickRatio=.5f, OppressionSlow=.25f, OppressionProtection=.2f;
    public const int OppressionPulses=5;
    public const float BellowRangeTiles=4f, BellowAngle=90f, BellowSupportTiles=3f;
    public const float BellowRatio=2.8f, BellowPushTiles=2f;
    public const float BellowWindup=.5f, BellowWaveDuration=.3f, BellowPushDuration=.38f, BellowKnockdown=.6f;
    public static float BellowFront(float age)=>Mathf.Clamp01(age/BellowWaveDuration);
    public static float BellowPushProgress(float t)=>1f-Mathf.Pow(1f-Mathf.Clamp01(t),3f);
}

public static class ArachneRank3Rules
{
    public const string HeavenlyNet="trait_arachne_heavenly_net";
    public const string CocoonFuneral="trait_arachne_myriad_cocoon_funeral";
    public const string EightfoldWeb="trait_arachne_eightfold_severing_web";
    public const float HeavenlyRadiusTiles=2.5f, HeavenlyOpenRatio=1.6f;
    public const float HeavenlyTickRatio=.5f, HeavenlyFinalRatio=2.2f;
    public const float HeavenlyDuration=4f, HeavenlyPullDuration=.4f;
    public const float HeavenlyPullTiles=1.25f, HeavenlyPrecisePullTiles=1.5f;
    public const float CocoonRadiusTiles=2f, CocoonOpenRatio=1f;
    public const float CocoonTickRatio=.6f, CocoonExplosionRatio=2.2f;
    public const float CocoonDuration=1.5f, CocoonTickInterval=.5f;
    public const float CocoonSplashRadiusTiles=1.25f,CocoonSplashRatio=1f;
    public const int CocoonTargets=3,CocoonSplashHits=2;
    public const float EightfoldRadiusTiles=2.5f, EightfoldLineRatio=.9f;
    public const float EightfoldFinalRatio=2.4f, EightfoldDefenseIgnore=.3f;
    public const float EightfoldPreparation=.25f, EightfoldInterval=.12f;
    public const float EightfoldFinalTension=.07f;
}
