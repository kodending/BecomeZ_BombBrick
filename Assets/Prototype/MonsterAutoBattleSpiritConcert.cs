using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    // Detached casters reuse the real skill/animation path without joining AI, occupancy or victory counts.
    private readonly List<Unit> spiritConcertEchoes=new List<Unit>();
    private static float ConcertDurationScale(Unit source)=>source?.spiritConcertOwner==null?1f:SpiritDeerRank3Rules.ConcertDurationScale;

    private object SkillWait(Unit source,float seconds)
    {
        CombatSkillClock clock=CaptureCombatSkillClock(source);
        float until=(clock==null?battleElapsed:clock.Time)+seconds;int generation=combatGeneration;
        return new WaitUntil(()=>battleEnding||generation!=combatGeneration||
            !paused&&(clock==null?battleElapsed:clock.Time)>=until);
    }

    private IEnumerator ResolveMyriadConcert(Unit source,Vector3 center,float scale,int generation)
    {
        float born=battleElapsed,radius=TileSize*SpiritDeerRank3Rules.RadiusTiles;
        var entered=new HashSet<Unit>();var cursed=new HashSet<Unit>();
        if(!mlEpisodeActive)PixelSpiritBlessing.Sanctuary(center,radius,SpiritDeerRank3Rules.Duration,
            PixelSpiritConcert.Color,Rank3Renderer(source),()=>battleElapsed,
            ()=>generation==combatGeneration&&!battleEnding);
        while(generation==combatGeneration&&!battleEnding&&
              battleElapsed-born<SpiritDeerRank3Rules.Duration)
        {
            if(paused){yield return null;continue;}
            float reach=radius*PixelSpiritBlessing.SpreadAt(battleElapsed-born);
            if(HasRank3(source,SpiritDeerCombatRules.EclipsedeerTraitId))
                foreach(Unit enemy in Rank3Enemies(source,center,reach))
                    if(cursed.Add(enemy))ApplySpiritDeerEclipseCurse(source,enemy);
            foreach(Unit ally in Rank3Allies(source,center,reach))
            {
                if(ally.augmentShadowClone||!entered.Add(ally))continue;
                ApplySpiritDeerSanctuaryBlessing(source,ally,scale);
                if(!string.IsNullOrEmpty(ally.skillId))StartCombatCoroutine(source,PerformSpiritConcert(ally,scale,generation,source));
            }
            yield return null;
        }
        if(battleEnding||generation!=combatGeneration)yield break;
        source.traitActivations++;RecordSkillEvent(source,SpiritDeerRank3Rules.MyriadConcert,0,0f);
    }

    private void ConfigureSpiritConcertGhost(Unit ghost,Unit owner)
    {
        ghost.spiritConcertOwner=owner;ghost.augmentShadowClone=true;ghost.deploymentSlots=0;
        ghost.damage=owner.damage;ghost.defense=owner.defense;ghost.maxHp=owner.maxHp;ghost.hp=owner.hp;
        ghost.regionDamageMultiplier=owner.regionDamageMultiplier;
        ghost.attackRange=owner.attackRange;ghost.attackInterval=owner.attackInterval;
        ghost.collisionRadius=owner.collisionRadius;ghost.speedTilesPerSecond=owner.speedTilesPerSecond;
        ghost.combatBodyScale=owner.combatBodyScale;
        ghost.spriteAnimation?.SetCombatPlaybackSpeed(BombBrick.Combat.CombatStatFormula.CombatAnimationRate);
        ghost.skillId=owner.skillId;ghost.skillCooldownDuration=owner.skillCooldownDuration;
        ghost.personalityPolicy=owner.personalityPolicy;ghost.skillTimer=0f;
        ghost.augmentSkillPowerMultiplier=owner.augmentSkillPowerMultiplier;
        ghost.activeTraitIds.AddRange(owner.activeTraitIds.Where(id=>id.StartsWith("intrinsic_",StringComparison.Ordinal)));
        ghost.model.name="Concert Spirit - "+owner.definition.monsterId;
        ghost.model.transform.localScale=owner.model.transform.localScale;
        ghost.baseModelScale=ghost.model.transform.localScale;
        ghost.visualHeightOffset=owner.visualHeightOffset;
        ghost.baseVisualHeightOffset=owner.visualHeightOffset;
        Vector3 forward=Flat(owner.facingDirection).normalized;
        if(forward.sqrMagnitude<.01f)forward=owner.team==Team.Player?Vector3.right:Vector3.left;
        ghost.model.transform.position=SpiritConcertSpawnPosition(owner);
        ghost.facingDirection=forward;ghost.spriteBillboard?.SetFacingDirection(forward);
        ghost.ring.SetActive(false);
    }

    private Vector3 SpiritConcertSpawnPosition(Unit owner)
    {
        Vector3 origin=owner.model.transform.position,best=origin;
        Vector3 view=Camera.main==null?Vector3.up:Camera.main.transform.forward;
        float bestScore=float.NegativeInfinity;
        // A dozen nearby slots are enough for the eight-unit roster; score their on-screen separation.
        for(int i=0;i<12;i++)
        {
            float angle=i*Mathf.PI/6f;
            Vector3 at=origin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*TileSize*1.2f;
            if(combatBounds.size.x>TileSize)
            {at.x=Mathf.Clamp(at.x,combatBounds.min.x+.2f,combatBounds.max.x-.2f);
             at.z=Mathf.Clamp(at.z,combatBounds.min.z+.2f,combatBounds.max.z-.2f);}
            float score=TileSize*2f;
            foreach(Unit other in units.Concat(spiritConcertEchoes))
                if(IsCombatActive(other))score=Mathf.Min(score,
                    Vector3.ProjectOnPlane(at-other.model.transform.position,view).magnitude);
            if(score>bestScore){bestScore=score;best=at;}
        }
        return best;
    }

    private bool ApproachSpiritConcertTarget(Unit ghost,float dt)
    {
        SkillMasterRow skill=GameMasterData.Skill(ghost.skillId);
        if(skill==null||!IsCombatTargetable(ghost.target))return false;
        Vector3 delta=Flat(ghost.target.model.transform.position-ghost.model.transform.position);
        float reach=Mathf.Max(.2f,SkillActivationDistance(ghost,ghost.target,skill)-TileSize*.12f);
        if(delta.magnitude<=reach+.0001f){SetMoving(ghost,false);return true;}
        ghost.model.transform.position+=delta.normalized*Mathf.Min(delta.magnitude-reach,TileSize*5f*BombBrick.Combat.CombatStatFormula.CombatMovementRate*dt);
        ghost.facingDirection=delta.normalized;ghost.spriteBillboard?.SetFacingDirection(delta,true);
        SetMoving(ghost,true);return false;
    }

    private IEnumerator PerformSpiritConcert(Unit owner,float scale,int generation,Unit conductor)
    {
        if(battleEnding||!IsCombatActive(owner)||owner.augmentShadowClone)yield break;
        var owned=PlayerProfileStore.CreateMonster(owner.definition,"concert_"+owner.stableId,false);
        owned.isShiny=owner.ownedInstance!=null&&owner.ownedInstance.isShiny;
        Unit ghost=SpawnUnitModel(owner.definition,owner.team,owner.currentTile,owned,false,owner);
        ghost.spiritConcertScale=SpiritDeerRank3Rules.ConcertSkillScale*Mathf.Max(0f,scale);
        spiritConcertEchoes.Add(ghost);
        PixelSpiritConcert fx=mlEpisodeActive?null:PixelSpiritConcert.Create(Rank3Renderer(ghost),()=>battleElapsed,
            ()=>generation==combatGeneration&&!battleEnding);
        if(fx!=null)PlaySpiritDeerSound(conductor,SpiritDeerSound.Appear,ghost.model.transform.position,.85f);
        float born=battleElapsed,last=born;
        try
        {
            while(battleElapsed-born<PixelSpiritConcert.ReadyToCast)
            {
                if(generation!=combatGeneration||battleEnding||!IsCombatActive(owner))yield break;
                ghost.spriteAnimation?.SetFrozen(paused);
                yield return null;
            }
            ghost.target=IsCombatTargetable(owner.target)?owner.target:SelectTarget(owner);
            string behavior=MonsterSkillExecutor<SkillExecutionContext>.Behavior(ghost.skillId);
            bool support=behavior=="SpiritSanctuary"||behavior=="FightingSpirit";
            if(support)
            {
                ghost.skillUses++;
                Action impact=()=>
                {
                    if(generation!=combatGeneration||battleEnding||!IsCombatActive(owner))return;
                    if(behavior=="SpiritSanctuary")ResolveConcertBlessingWave(ghost);
                    else ApplyConcertBearBlessing(ghost,owner);
                };
                if(ghost.spriteAnimation!=null)
                {
                    if(behavior=="FightingSpirit")ghost.spriteAnimation.PlayBearFightingSpirit(1f,impact);
                    else
                    {
                        PlaySpiritDeerSound(ghost,SpiritDeerSound.Prepare,ghost.model.transform.position,.85f);
                        ghost.spriteAnimation.PlaySkillAttack(1f,impact);
                    }
                }
                else impact();
            }
            else
            {
                float approachStarted=battleElapsed;last=battleElapsed;
                while(battleElapsed-approachStarted<2.5f)
                {
                    if(generation!=combatGeneration||battleEnding||!IsCombatActive(owner))yield break;
                    ghost.spriteAnimation?.SetFrozen(paused);
                    if(paused){yield return null;continue;}
                    if(!IsCombatTargetable(ghost.target))ghost.target=SelectTarget(owner);
                    if(!IsCombatTargetable(ghost.target))break;
                    float step=battleElapsed-last;last=battleElapsed;
                    if(ApproachSpiritConcertTarget(ghost,step)&&TryUseSkill(ghost,true))break;
                    yield return null;
                }
                SetMoving(ghost,false);
            }
            last=battleElapsed;float castAt=last,quietSince=last;bool departed=false;
            while(battleElapsed-castAt<12f)
            {
                if(generation!=combatGeneration||battleEnding)yield break;
                if(!IsCombatActive(owner)&&IsCombatActive(ghost))
                {fx?.Depart();departed=true;InterruptPositionalAction(ghost);ghost.alive=false;}
                ghost.spriteAnimation?.SetFrozen(paused);
                float dt=battleElapsed-last;last=battleElapsed;
                if(dt>0f&&!support&&IsCombatActive(ghost))UpdateUnit(ghost,dt);
                if(ConcertActionBusy(ghost))quietSince=battleElapsed;
                if(!departed&&battleElapsed-castAt>.7f&&battleElapsed-quietSince>.28f)
                {
                    fx?.Depart();departed=true;
                    if(fx!=null)PlaySpiritDeerSound(conductor,SpiritDeerSound.Depart,ghost.model.transform.position,.85f);
                }
                // Keep the invisible source alive until launched projectiles / gas have resolved.
                yield return null;
            }
        }
        finally
        {
            fx?.Depart();ghost.alive=false;
            if(ghost.tongueWhip!=null)ghost.tongueWhip.Cancel();
            if(ghost.bearSpiritFx!=null)ghost.bearSpiritFx.Cancel();
            // Retain the hidden source until battle cleanup for delayed effects and chained projectiles.
        }
    }

    private static bool ConcertActionBusy(Unit u)=>u.spriteAnimation!=null&&u.spriteAnimation.IsActionLocked||
        u.leaping||u.shadowSlashing||u.griffinSkyfalling||u.displacing||u.frogHopping||
        u.tongueHarpoonTarget!=null||u.fangLockTarget!=null||u.crocodileBiteLockTimer>0f||u.giraffeSlamming||
        u.attackMovementLockTimer>0f||u.rank3SkillLockTimer>0f||u.mammothCharge!=null||u.beetleFlip!=null;

    private void ClearSpiritConcertEchoes()
    {
        foreach(Unit ghost in spiritConcertEchoes)
        {ghost.alive=false;if(ghost.model!=null&&!CombatEffectEndFade.IsFading(ghost.model.transform))Destroy(ghost.model);if(ghost.ring!=null)Destroy(ghost.ring);}
        spiritConcertEchoes.Clear();
    }

    private void ResolveConcertBlessingWave(Unit ghost)
    {
        SkillMasterRow skill=GameMasterData.Skill(ghost.skillId);
        float radius=TileSize*skill.radiusTiles;
        Unit anchor=SelectSpiritSanctuaryAnchor(ghost,TileSize*skill.castRangeTiles,radius);
        Vector3 center=Flat((anchor??ghost.spiritConcertOwner).model.transform.position);
        if(!mlEpisodeActive)PixelSpiritBlessing.Sanctuary(center,radius,.4f,PixelSpiritConcert.Color,
            Rank3Renderer(ghost),()=>battleElapsed,()=>IsCombatActive(ghost)&&!battleEnding);
        PlaySpiritDeerSound(ghost,SpiritDeerSound.Spread,center);
        foreach(Unit ally in Rank3Allies(ghost,center,radius))
            if(!ally.augmentShadowClone)ApplySpiritDeerSanctuaryBlessing(ghost,ally,1f);
        if(HasRank3(ghost,SpiritDeerCombatRules.EclipsedeerTraitId))
            foreach(Unit enemy in Rank3Enemies(ghost,center,radius))ApplySpiritDeerEclipseCurse(ghost,enemy);
    }

    private void ApplyConcertBearBlessing(Unit ghost,Unit owner)
    {
        float duration=GameMasterData.Skill(ghost.skillId).durationSeconds*SpiritDeerRank3Rules.ConcertDurationScale;
        owner.bearResolveTimer=Mathf.Max(owner.bearResolveTimer,duration);
        owner.bearResolveTotalDuration=Mathf.Max(owner.bearResolveTotalDuration,duration);
        owner.bearResolveAttackSpeedBonus=GateHaetaeBenefit(owner,Mathf.Max(owner.bearResolveAttackSpeedBonus,BearCombatRules.ResolveAttackSpeedBonus));
        owner.bearResolveDamageReduction=GateHaetaeBenefit(owner,Mathf.Max(owner.bearResolveDamageReduction,BearCombatRules.ResolveDamageReduction));
        if(HasRank3(ghost,BearCombatRules.DamiTraitId))owner.combatStatuses.ApplyShield(duration,
            BearCombatRules.DamiShield(ghost.maxHp)*ghost.spiritConcertScale,owner);
        if(!mlEpisodeActive)PixelSpiritBlessing.Bless(Rank3Renderer(owner),1,PixelSpiritConcert.Color,.65f,
            ()=>battleElapsed,()=>IsCombatActive(owner));
    }
}
