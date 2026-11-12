using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BombBrick.Game.Core;

public static class FamilyTranscendenceRules
{
    public const float KingKongZoneDuration=6f;
    public const float KingKongZoneRadiusTiles=3.5f;
    public const float BearAvatarDuration=6f;
    public const float BearAvatarScale=1.8f;
    public const float BearFearDuration=1f;
    public const float ArachneZoneDuration=8f;
    public const float ArachnePuppetDuration=5f;
    public const float ArachnePuppetHealthRatio=.2f,ArachnePuppetDamageRatio=.4f;
    public const int ArachnePuppetLimit=2;
    public const float TyrannoStompWarning=.5f,TyrannoStompKnockdown=1.2f;
    public const float TyrannoStompDamageRatio=2f;
    public const float TyrannoStompEnd=TyrannoStompWarning+TyrannoStompKnockdown;

    public static string DisplayName(string family)
    {
        switch((family??string.Empty).ToLowerInvariant())
        {
            case "sabertooth": return SabertoothDevourRules.DisplayName;
            case "frog": return "만물 낚시";
            case "crocodile": return CrocodileAbyssDiveRules.DisplayName;
            case "kingkong": return "지맥의 심장";
            case "wizard_mouse": return "무한 성좌";
            case "bear": return "웅신 강림";
            case "wolf": return "만월의 대사냥";
            case "arachne": case "spider": return "거미 여왕의 영역";
            case "ninja_cat": return NinjaSpacetimeRules.DisplayName;
            case "tyranno": return "폭군의 군림";
            case "monkey": return "천상뇌우";
            case "mantis": return "죽음 탈피";
            case "mushroom": return "마지막 개화";
            case "mammoth": return "거인의 연쇄 충돌";
            case "beetle": return BeetleOverturnRules.DisplayName;
            case "spirit_deer": return "수호정령 강림";
            case "griffin": return "폭풍 구체";
            case "wyvern": return "천룡의 숨결";
            case "haetae": return "천명 집행자";
            default: return string.Empty;
        }
    }

    public static bool IsSupportedFamily(string family)
    {
        return string.Equals(family,"sabertooth",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"frog",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"crocodile",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"kingkong",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"wizard_mouse",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"bear",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"wolf",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"arachne",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"spider",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"ninja_cat",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"tyranno",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"monkey",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"mantis",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"mushroom",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"mammoth",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"beetle",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"spirit_deer",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"griffin",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"wyvern",StringComparison.OrdinalIgnoreCase)||
               string.Equals(family,"haetae",StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanActivate(string family,bool transcended,
        bool shadowClone=false,bool eclipseClone=false,bool summon=false)
    {
        return transcended&&!shadowClone&&!eclipseClone&&!summon&&
               IsSupportedFamily(family);
    }
}

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class FamilyTranscendenceState
    {
        public float timer;
        public float pulseTimer;
        public Unit target;
        public Vector3 center;
        public Vector3 forward=Vector3.forward;
        public int charges;
        public int basicCount;
        public bool griffinStormOrbUsed;
        public PixelKingKongLeylineAura leylineAura;
        public int kingKongCinematicGeneration=int.MinValue;
        public int bearCinematicGeneration=int.MinValue;
        public int wolfCinematicGeneration=int.MinValue;
        public bool releasedEffectsCancelled;
        public PixelBearAvatarDeparture bearDeparture;
        public int silkPuppetsPending;
        public PixelSilkQueenDomain silkDomain;
        public int tyrannoLastCast=-1,tyrannoPending;
        public float tyrannoStompTime=-1f;
        public bool tyrannoStompHit;
        public PixelTyrannoReignStomp tyrannoStomp;
    }

    private readonly Dictionary<Unit,FamilyTranscendenceState>
        familyTranscendenceStates=
            new Dictionary<Unit,FamilyTranscendenceState>();

    private FamilyTranscendenceState FamilyTranscendenceStateFor(
        Unit unit,bool create=true)
    {
        if(unit==null)return null;
        FamilyTranscendenceState state;
        if(familyTranscendenceStates.TryGetValue(unit,out state)||!create)return state;
        state=new FamilyTranscendenceState();
        familyTranscendenceStates.Add(unit,state);
        return state;
    }

    private static bool HasFamilyTranscendence(Unit unit)
    {
        return unit!=null&&unit.definition!=null&&
               FamilyTranscendenceRules.CanActivate(
                   unit.definition.familyId,unit.augmentTranscended,
                   unit.augmentShadowClone,false,
                   unit.rank3MycokingSummon);
    }

    private void ResetFamilyTranscendenceStates()
    {
        ResetRank4Cinematics();
        if(battleEnding)CancelNinjaSpacetimeCasts();else ResetNinjaSpacetime();
        ResetWolfStampedes(!battleEnding);
        ClearWizardCygnusShowers(!battleEnding);
        ClearMonkeyStorms(battleEnding);
        foreach(Unit unit in units)EndCrocodileAbyssDive(unit,true);
        foreach(Unit unit in units)EndFrogGrandSlam(unit,true);
        foreach(Unit unit in units)EndSabertoothDevour(unit,true);
        CancelRaptorApexCinematic();
        foreach(Unit unit in units)unit.raptorApexWings?.Clear();
        ClearHaetaeRoars();
        ClearWyvernBreaths();
        ClearGriffinCrosswinds();
        foreach(Unit unit in units)ClearUnitShieldVisual(unit);
        ClearSpiritGuardians();
        ClearHaetaeDevourers();
        while(beetleOverturns.Count>0)EndBeetleOverturn(beetleOverturns[beetleOverturns.Count-1]);
        beetleOverturnPreviewTiles.Clear();
        foreach(Unit unit in familyTranscendenceStates.Keys)
        {
            CleanupMantisDeathMolt(unit);unit.mantisMoltUsed=false;
            if(IsBearFamily(unit))EndBearAvatar(unit);
            EndMonkeyNimbus(unit);
        }
        foreach(FamilyTranscendenceState state in familyTranscendenceStates.Values)
        {
            state.releasedEffectsCancelled=true;
            if(state.leylineAura!=null)state.leylineAura.Stop(true);
            if(state.silkDomain!=null)state.silkDomain.Cancel();
            CancelTyrannoReign(state);
        }
        familyTranscendenceStates.Clear();
    }

    private void TriggerFamilyTranscendenceSkill(Unit source)
    {
        // Sabertooth owns a separate rank-4 cast instead of adding a proc to Fang Lock.
        if(!HasFamilyTranscendence(source)||IsNinjaCatFamily(source)||IsWolfFamily(source)||IsWizardMouseFamily(source)||IsBearFamily(source)||IsRaptorFamily(source)||IsSabertoothFamily(source)||IsMantisFamily(source)||
           IsCrocodileFamily(source)||IsTyrannoFamily(source)||source.definition.familyId=="monkey"||source.definition.familyId=="mushroom"||source.definition.familyId=="mammoth"||source.definition.familyId=="beetle"||IsSpiritDeerFamily(source)||
           IsWyvernFamily(source)||IsHaetaeFamily(source)||source.definition.familyId=="griffin"||FrogHopMovementRules.IsFrogFamily(source.definition.familyId))return;
        FamilyTranscendenceState state=FamilyTranscendenceStateFor(source);
        string family=source.definition.familyId.ToLowerInvariant();
        Unit target=PendingTranscendenceTarget(source)??source.target;
        SkillMasterRow skill=GameMasterData.Skill(source.skillId);
        source.traitActivations++;
        PlayFamilyTranscendenceFeedback(source,family);

        switch(family)
        {
            case "kingkong":
                state.timer=FamilyTranscendenceRules.KingKongZoneDuration;
                state.pulseTimer=0f;
                if(state.leylineAura==null)
                    state.leylineAura=PixelCombatEffect.PlayKingKongLeylineAura(
                        source.model.transform,source.spriteAnimation==null?null:
                            source.spriteAnimation.GetComponent<SpriteRenderer>());
                else state.leylineAura.Refresh();
                if(!QueueKingKongLeylineCinematic(source))
                    PlayKingKongSound(source,KingKongSound.Awaken,source.model.transform.position);
                break;
            case "arachne": case "spider":
                BeginSilkQueenDomain(source,target,state);
                break;
        }
    }

    private void UpdateFamilyTranscendence(Unit source,float dt)
    {
        if(IsNinjaCatFamily(source)){TickNinjaSpacetime(source,dt);return;}
        FamilyTranscendenceState state=FamilyTranscendenceStateFor(source,false);
        if(state==null)return;
        if(!HasFamilyTranscendence(source)||!IsCombatActive(source)||source.hp<=0f)
        {
            if(IsBearFamily(source))EndBearAvatar(source);
            EndMonkeyNimbus(source);
            if(state.silkDomain!=null)state.silkDomain.Break();
            CancelTyrannoReign(state);
            state.silkDomain=null;state.timer=0f;
            return;
        }
        string family=source.definition.familyId.ToLowerInvariant();
        if(IsSpiderFamily(source)&&SilkQueenCinematicPending(source))return;
        if(family=="kingkong"&&IsKingKongLeylineCinematicPending(source))return;
        if(family=="bear"&&IsBearAvatarCinematicPending(source))return;
        if(family=="monkey")
        {
            TickMonkeyNimbus(source,dt);return;
        }
        if(family=="tyranno")
        {
            TickTyrannoReign(source,state,dt);return;
        }
        float previous=state.timer;
        if(state.timer>0f)state.timer=Mathf.Max(0f,state.timer-dt);
        if(family=="bear"&&previous>0f&&state.timer<=0f)EndBearAvatar(source,true);

        if(family=="kingkong")
        {
            if(state.timer>0f)TickTranscendentKingKongZone(source,state,dt);
            else if(previous>0f)
            {
                if(state.leylineAura!=null)state.leylineAura.Stop();
                state.leylineAura=null;
            }
        }
        else if(IsSpiderFamily(source))
        {
            if(source.spiderWebPending&&
               (source.spriteAnimation==null||!source.spriteAnimation.HasPendingSkillAttackHit))
                ReleaseSilkQueenCocoons(source);
            if(state.timer>0f)RevealSilkQueenDomain(source,state);
            else if(state.silkDomain!=null){state.silkDomain.Break();state.silkDomain=null;}
        }
    }

    private static Unit PendingTranscendenceTarget(Unit source)
    {
        if(source==null)return null;
        string family=source.definition==null?"":source.definition.familyId;
        switch((family??string.Empty).ToLowerInvariant())
        {
            case "sabertooth": return source.sabertoothDevour!=null?source.target:source.fangLockTarget;
            case "frog": return source.tongueHarpoonTarget;
            case "crocodile": return source.crocodileAbyssDive?.target??source.crocodileRollTarget;
            case "wolf": return source.wolfHuntTarget;
            case "ninja_cat": return source.ninjaHuntTarget;
            case "monkey": return source.monkeyBarrageTarget;
            case "mantis": return source.mantisScytheTarget;
            case "mushroom": return source.mushroomColonyTarget;
            case "mammoth": return source.mammothChargeTarget;
            case "beetle": return source.beetleFlip?.target??source.beetleSkyFlipTarget;
            case "wyvern": return source.wyvernBreathTarget;
            default: return source.target;
        }
    }

    private void PlayFamilyTranscendenceFeedback(Unit source,string family)
    {
        Color color=FamilyTranscendenceColor(family);
        AddCombatFloatText(source,FamilyTranscendenceRules.DisplayName(family),color,true);
        PlayTraitBurst(source,color,true);
        PlayTraitBurst(source,Color.Lerp(color,Color.white,.38f),false);
    }

    private static Color FamilyTranscendenceColor(string family)
    {
        switch(family)
        {
            case "bear": return new Color(.42f,.94f,1f);
            case "crocodile": return PixelCombatEffect.CrocodileAbyssColor;
            case "frog": case "mushroom": return new Color(.4f,1f,.28f);
            case "kingkong": case "mammoth": return new Color(.82f,.52f,.2f);
            case "wizard_mouse": case "spirit_deer": return new Color(.62f,.82f,1f);
            case "wolf": return new Color(.78f,.88f,1f);
            case "arachne": case "spider": case "ninja_cat": return new Color(.72f,.25f,1f);
            case "beetle": case "wyvern": return new Color(.28f,.88f,1f);
            case "griffin": return new Color(.45f,.86f,1f);
            case "haetae": return new Color(1f,.86f,.3f);
            default: return new Color(1f,.2f,.04f);
        }
    }

    private void HandleFamilyTranscendenceBasicAttack(
        Unit source,Unit target,float attackDamage)
    {
        if(!HasFamilyTranscendence(source)||!IsCombatActive(target))return;
        FamilyTranscendenceState state=FamilyTranscendenceStateFor(source,false);
        if(state==null)return;
        string family=source.definition.familyId.ToLowerInvariant();
        if(family=="kingkong"&&state.timer>0f&&!IsKingKongLeylineCinematicPending(source))
        {
            Vector3 center=Flat(target.model.transform.position);
            DamageEnemiesInRadius(source,Flat(target.model.transform.position),
                TileSize*1.2f,SkillPower(source)*.4f,target,true);
            PixelCombatEffect.PlayKingKongLeylineWave(center,TileSize*1.2f,
                source.spriteAnimation==null?null:source.spriteAnimation.GetComponent<SpriteRenderer>(),true);
        }
    }

    private void HandleFamilyTranscendenceDeath(Unit killer,Unit defeated)
    {
        if(defeated==null)return;
        CancelWolfStampedeFor(defeated);
        CancelWizardCygnusShowerFor(defeated);
        CancelKingKongLeylineCinematicFor(defeated);
        InterruptCrocodileAbyssDive(defeated);
        EndSabertoothDevour(defeated);
        if(defeated.sabertoothDevouredBy!=null)ReleaseSabertoothDevourVictim(defeated.sabertoothDevouredBy,defeated);
        if(IsBearFamily(defeated))EndBearAvatar(defeated);
        FamilyTranscendenceState defeatedState=FamilyTranscendenceStateFor(defeated,false);
        if(defeatedState!=null)
        {
            CancelTyrannoReign(defeatedState);
            if(defeatedState.leylineAura!=null)defeatedState.leylineAura.Stop(true);
            defeatedState.leylineAura=null;
        }
    }

    private void TickTranscendentKingKongZone(
        Unit source,FamilyTranscendenceState state,float dt)
    {
        state.pulseTimer-=dt;
        if(state.pulseTimer>0f)return;
        state.pulseTimer+=1f;
        Vector3 center=Flat(source.model.transform.position);
        float raw=SkillPower(source)*.45f+OffensiveMaxHealth(source)*.01f;
        foreach(Unit enemy in units.Where(enemy=>IsCombatActive(enemy)&&
                    enemy.team!=source.team&&
                    Vector3.Distance(center,Flat(enemy.model.transform.position))<=
                        TileSize*FamilyTranscendenceRules.KingKongZoneRadiusTiles+enemy.collisionRadius).ToList())
        {
            float dealt=ApplyGroundSkillDamage(source,enemy,raw,false);
            if(dealt>0f){source.skillDamage+=dealt;source.skillHits++;}
            PlayKingKongLeylineHit(enemy,center);
        }
        PixelCombatEffect.PlayKingKongLeylineWave(center,TileSize*FamilyTranscendenceRules.KingKongZoneRadiusTiles,
            source.spriteAnimation==null?null:source.spriteAnimation.GetComponent<SpriteRenderer>());
        if(state.leylineAura!=null)state.leylineAura.Pulse();
        PlayKingKongSound(source,KingKongSound.Pulse,center,1.2f);
    }

    private void PlayKingKongLeylineHit(Unit enemy,Vector3 origin)
    {
        if(enemy==null||enemy.model==null)return;
        PlayTraitBurst(enemy,PixelCombatEffect.LeylineGold,true);
        enemy.reaction?.PlayTintHit(Color.white);
        enemy.spriteAnimation?.PlayImpactRecoil(Flat(enemy.model.transform.position)-origin,0.065f);
        PixelCombatEffect.PlayKingKongLeylineHitDebris(Flat(enemy.model.transform.position),
            enemy.spriteAnimation==null?null:enemy.spriteAnimation.GetComponent<SpriteRenderer>());
    }





    private void DamageEnemiesInRadius(Unit source,Vector3 center,float radius,
        float damage,Unit excluded=null,bool leylineHit=false)
    {
        foreach(Unit enemy in units.Where(enemy=>enemy!=excluded&&
                    IsCombatActive(enemy)&&enemy.team!=source.team&&
                    Vector3.Distance(center,Flat(enemy.model.transform.position))<=
                        radius+enemy.collisionRadius).ToList())
        {
            float dealt=ApplySkillDamage(source,enemy,damage,false);
            if(dealt>0f){source.skillDamage+=dealt;source.skillHits++;}
            if(leylineHit)PlayKingKongLeylineHit(enemy,center);
        }
    }



    private static float DistanceToSegment(Vector3 point,Vector3 start,Vector3 end)
    {
        Vector3 segment=end-start;
        if(segment.sqrMagnitude<.001f)return Vector3.Distance(point,start);
        float t=Mathf.Clamp01(Vector3.Dot(point-start,segment)/segment.sqrMagnitude);
        return Vector3.Distance(point,start+segment*t);
    }
}
