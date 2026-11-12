using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Combat;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class Rank3IceWall
    {
        public Unit source;
        public Team team;
        public Vector3 center;
        public Vector3 forward;
        public Vector3 side;
        public float halfLength;
        public float remaining;
    }

    private readonly List<Rank3IceWall> rank3IceWalls=new List<Rank3IceWall>();

    private static bool HasRank3(Unit unit,string traitId)
    {
        return unit!=null&&MonsterTraitRuntime.Has(unit.activeTraitIds,traitId);
    }

    private static SpriteRenderer Rank3Renderer(Unit unit)
    {
        if(unit==null||unit.model==null)return null;
        if(unit.mycokingFx!=null)return unit.mycokingFx.BodyRenderer;
        return unit.spriteAnimation==null
            ?unit.model.GetComponentInChildren<SpriteRenderer>()
            :unit.spriteAnimation.GetComponent<SpriteRenderer>();
    }

    private static Vector3 Rank3Center(Unit unit)
    {
        SpriteRenderer renderer=Rank3Renderer(unit);
        return renderer==null
            ?unit.model.transform.position+Vector3.up*.45f
            :renderer.bounds.center;
    }

    private List<Unit> Rank3Enemies(Unit source,Vector3 center,float radius)
    {
        return units.Where(unit=>IsCombatActive(unit)&&unit.team!=source.team&&
            Vector3.Distance(center,Flat(unit.model.transform.position))<=
            radius+unit.collisionRadius).ToList();
    }

    private List<Unit> Rank3Allies(Unit source,Vector3 center,float radius)
    {
        return units.Where(unit=>IsCombatActive(unit)&&unit.team==source.team&&
            Vector3.Distance(center,Flat(unit.model.transform.position))<=
            radius+unit.collisionRadius).ToList();
    }

    // Authored rank-3 contacts already include the cast's captured trait multiplier.
    private float Rank3AreaDamage(Unit source,Vector3 center,float radius,
        float ratio,float defenseIgnore=0f,HashSet<Unit> victims=null)
    {
        float dealt=0f;
        List<Unit> targets=Rank3Enemies(source,center,radius);
        for(int i=0;i<targets.Count;i++)
        {
            Unit enemy=targets[i];
            dealt+=ApplySkillDamage(source,enemy,new SkillDamageFormulaResult(
                SkillPower(source)*Mathf.Max(0f,ratio),0f,defenseIgnore),
                triggerTraits:false,triggerTeamTraits:false,applyTraitMultiplier:false,bypassProjectileWalls:true);
            victims?.Add(enemy);
        }
        source.skillHits+=targets.Count;
        source.skillDamage+=dealt;
        return dealt;
    }

    private float Rank3Hit(Unit source,Unit target,float ratio,
        float defenseIgnore=0f,float trueDamage=0f)
        =>ApplyRank3Hit(source,target,ratio,defenseIgnore,trueDamage,true);

    private float Rank3GroundHit(Unit source,Unit target,float ratio,
        float defenseIgnore=0f,float trueDamage=0f)
        =>ApplyRank3Hit(source,target,ratio,defenseIgnore,trueDamage,false,true);

    private float Rank3AreaHit(Unit source,Unit target,float ratio,
        float defenseIgnore=0f,float trueDamage=0f)
        =>ApplyRank3Hit(source,target,ratio,defenseIgnore,trueDamage,true,true);

    private float ApplyRank3Hit(Unit source,Unit target,float ratio,
        float defenseIgnore,float trueDamage,bool allowHitSlide,bool bypassProjectileWalls=false)
    {
        if(!IsCombatActive(target))return 0f;
        float dealt=ApplySkillDamage(source,target,new SkillDamageFormulaResult(
            SkillPower(source)*Mathf.Max(0f,ratio),trueDamage,defenseIgnore),
            triggerTraits:false,triggerTeamTraits:false,applyTraitMultiplier:false,allowHitSlide:allowHitSlide,
            bypassProjectileWalls:bypassProjectileWalls);
        source.skillHits++;
        source.skillDamage+=dealt;
        return dealt;
    }

    private void Rank3Knockdown(Unit source,Unit target,float duration)
    {
        if(!IsCombatActive(target)||target.combatStatuses.ControlImmune)return;
        float applied=HostileStatusDuration(target,Mathf.Max(.05f,duration));
        var effect=target.combatStatuses.ApplyTimed(CombatStatusKind.Knockdown,applied,source);
        target.spriteAnimation?.PlayKnockdown(effect.remaining);
    }

    private void Rank3Root(Unit source,Unit target,float duration)
    {
        if(!IsCombatActive(target))return;
        target.combatStatuses.ApplyTimed(CombatStatusKind.Root,
            HostileStatusDuration(target,duration),source);
    }

    private void Rank3Poison(Unit source,Unit target,float duration,float ratio=.2f)
    {
        if(!IsCombatActive(target))return;
        target.combatStatuses.ApplyStacks(CombatStatusKind.Poison,duration,1,
            CombatStatusState.MaximumPoisonStacks,source,source.damage*ratio,1f,true);
        PixelCombatEffect.PlayPoisonBurst(
            target.spriteAnimation==null?target.model.transform:
            target.spriteAnimation.transform,Rank3Renderer(target));
    }

    private void Rank3Burn(Unit source,Unit target,float duration,float ratio=.2f)
    {
        if(!IsCombatActive(target))return;
        target.combatStatuses.ApplyStacks(CombatStatusKind.Burning,duration,1,1,
            source,source.damage*ratio,1f);
        PlayTraitBurst(target,new Color(1f,.24f,.04f),true);
    }

    private void Rank3Frost(Unit source,Unit target,int stacks=1)
    {
        if(!IsCombatActive(target))return;
        for(int i=0;i<Mathf.Max(1,stacks);i++)
            ApplyStackingFrost(source,target,4f,3,1f);
    }

    private void Rank3Shred(Unit target,float ratio,float duration,Unit source)
    {
        if(!IsCombatActive(target)||target.combatStatuses.RejectHarmfulEffect())return;
        ApplyDefenseShred(source,target,ratio,duration);
        target.familyResistanceShredRatio=Mathf.Max(
            target.familyResistanceShredRatio,ratio);
        target.familyResistanceShredTimer=Mathf.Max(
            target.familyResistanceShredTimer,duration);
        target.combatEffectIcons.Show(CombatEffectIconKind.DefenseDown,duration);
        target.combatEffectIcons.Show(CombatEffectIconKind.ResistanceDown,duration);
    }

    private void Rank3Shield(Unit source,Unit target,float amount,float duration)
    {
        if(!IsCombatActive(target))return;
        target.combatStatuses.ApplyShield(duration,amount,source);
        target.combatEffectIcons.Show(CombatEffectIconKind.Shield,duration);
        PlayTraitBurst(target,new Color(.38f,.88f,1f),false);
    }

    private void Rank3Cleanse(Unit unit)
    {
        if(unit==null)return;
        List<CombatStatusKind> harmful=unit.combatStatuses.ActiveEffects
            .Where(effect=>IsRank3Harmful(effect.kind))
            .Select(effect=>effect.kind).ToList();
        for(int i=0;i<harmful.Count;i++)unit.combatStatuses.Remove(harmful[i]);
        unit.familyAttackReductionTimer=0f;
        unit.familyHealingReductionTimer=0f;
        unit.familyRecoveryShieldReductionTimer=0f;
        unit.familyResistanceShredTimer=0f;
        unit.itemDefenseShredTimer=0f;
        unit.defenseShredSources.Clear();
        unit.rank3ActionSlowTimer=0f;
        unit.rank3SkillLockTimer=0f;
    }

    private static bool IsRank3Harmful(CombatStatusKind kind)
    {
        switch(kind)
        {
            case CombatStatusKind.Poison:
            case CombatStatusKind.Burning:
            case CombatStatusKind.Root:
            case CombatStatusKind.Freeze:
            case CombatStatusKind.Airborne:
            case CombatStatusKind.ComboWeakened:
            case CombatStatusKind.ComboBurning:
            case CombatStatusKind.ComboChilled:
            case CombatStatusKind.Knockdown:
            case CombatStatusKind.VenomFruit:
            case CombatStatusKind.Bleed:
            case CombatStatusKind.HallucinatoryPollen:
            case CombatStatusKind.AttackSpeedDown:
            case CombatStatusKind.Fear:
            case CombatStatusKind.Displaced:
            case CombatStatusKind.Marked:return true;
            default:return false;
        }
    }

    private Rank3IceWall RegisterRank3IceWall(Unit source,Vector3 center,
        Vector3 forward,Vector3 side,float halfLength,float duration)
    {
        Rank3IceWall wall=new Rank3IceWall
        {
            source=source,
            team=source.team,
            center=center,
            forward=forward.normalized,
            side=side.normalized,
            halfLength=halfLength,
            remaining=duration
        };
        rank3IceWalls.Add(wall);
        return wall;
    }

    private void UpdateRank3IceWalls(float dt)
    {
        for(int i=rank3IceWalls.Count-1;i>=0;i--)
        {
            Rank3IceWall wall=rank3IceWalls[i];
            wall.remaining-=Mathf.Max(0f,dt);
            if(wall.remaining<=0f)
                rank3IceWalls.RemoveAt(i);
        }
    }

    private void ClearRank3IceWalls()
    {
        rank3IceWalls.Clear();
    }

    private Vector3 BlockRank3IceWallMovement(Unit unit,Vector3 start,Vector3 end)
    {
        if(unit==null||Vector3.Distance(start,end)<.0001f)return end;
        for(int i=0;i<rank3IceWalls.Count;i++)
        {
            Rank3IceWall wall=rank3IceWalls[i];
            if(wall.team==unit.team||!Rank3SegmentCrossesWall(start,end,wall))continue;
            unit.velocity=Vector3.zero;
            unit.currentAction="blocked_by_ice_age_wall";
            return start;
        }
        return end;
    }

    private bool IsHostileIceWallBlocking(Unit unit,Vector3 start,Vector3 end)
    {
        for(int i=0;i<rank3IceWalls.Count;i++)
            if(rank3IceWalls[i].team!=unit.team&&Rank3SegmentCrossesWall(start,end,rank3IceWalls[i]))return true;
        return false;
    }

    private bool IsRank3ProjectileBlocked(Unit source,Unit target,
        bool fromSkill,bool isBasicAttack)
    {
        if(!IsCombatActive(source)||!IsCombatActive(target)||source==target||
           rank3IceWalls.Count==0)return false;
        bool projectile=isBasicAttack&&source.attackRange>1;
        if(fromSkill)
        {
            string behavior=MonsterSkillExecutor<object>.Behavior(source.skillId)
                .ToLowerInvariant();
            projectile=behavior.Contains("magicstarbarrage")||
                       behavior.Contains("acrobaticbarrage")||
                       behavior.Contains("sporecolony")||
                       behavior.Contains("captureweb");
        }
        if(!projectile)return false;
        Vector3 start=Flat(source.model.transform.position);
        Vector3 end=Flat(target.model.transform.position);
        for(int i=0;i<rank3IceWalls.Count;i++)
        {
            Rank3IceWall wall=rank3IceWalls[i];
            if(wall.team==source.team||!Rank3SegmentCrossesWall(start,end,wall))continue;
            Rank3CombatFx.Line("Ice Wall Projectile Block",
                wall.center-wall.side*wall.halfLength,
                wall.center+wall.side*wall.halfLength,TileSize*.18f,
                new Color(.76f,.96f,1f),.22f,skillRadiusMarkerMaterial,
                Rank3Renderer(wall.source));
            return true;
        }
        return false;
    }

    private static bool Rank3SegmentCrossesWall(Vector3 start,Vector3 end,
        Rank3IceWall wall)
    {
        float from=Vector3.Dot(start-wall.center,wall.forward);
        float to=Vector3.Dot(end-wall.center,wall.forward);
        if(from*to>0f||Mathf.Abs(from-to)<.0001f)return false;
        float t=Mathf.Clamp01(from/(from-to));
        Vector3 crossing=Vector3.Lerp(start,end,t);
        return Mathf.Abs(Vector3.Dot(crossing-wall.center,wall.side))<=
               wall.halfLength;
    }

    private void Rank3Pulse(Unit source,Vector3 center,float radius,Color color,
        string name="Rank3 Pulse",float duration=.45f)
    {
        if(IsMushroomFamily(source))
            PlayCombatSound(source,CombatSoundCue.Impact,center);
        Rank3CombatFx.Ring(name,center,radius,color,duration,
            skillRadiusMarkerMaterial,Rank3Renderer(source));
        CreateSkillPulseAt(center,color,Mathf.Max(.5f,radius/TileSize));
    }

    private bool UpdateRank3SpecialUnit(Unit unit,float dt)
    {
        if(TickMantisBroodling(unit,dt))return true;
        if(unit.silkPuppet)
        {
            if(!IsCombatActive(unit.rank3SummonOwner)||unit.rank3SummonOwner.hp<=0f)
            {DeactivateRank3Special(unit);return true;}
            if(unit.silkPuppetRise>0f)
            {unit.silkPuppetRise=Mathf.Max(0f,unit.silkPuppetRise-dt);return true;}
            unit.rank3SummonTimer-=dt;
            if(unit.rank3SummonTimer<=0f){DeactivateRank3Special(unit);return true;}
            return false;
        }
        if(unit.rank3MycokingSummon)
            return false;
        return false;
    }

    private void CleanupRank3Unit(Unit unit)
    {
        if(unit==null)return;
        CancelNinjaSpacetime(unit);
        ClearUnitShieldVisual(unit);
        EndGriffinHunt(unit);
        InterruptGriffinRoyal(unit);
        if(unit.griffinHeldBy!=null&&unit.griffinHeldBy.griffinHunt!=null)
            ReleaseGriffinHuntVictim(unit.griffinHeldBy,unit.griffinHeldBy.griffinHunt);
        ReleaseBeetleOverturnPassenger(unit);
        if(unit.beetleOverturn!=null)EndBeetleOverturn(unit.beetleOverturn);
        ReleaseBeetleGravityVictim(unit);
        if(unit.beetleFlip!=null)EndBeetleFlip(unit,unit.beetleFlip);
        ClearBeetleSweep(unit);
        if(unit.beetleFlippedBy!=null&&unit.beetleFlippedBy.beetleFlip!=null)
            ReleaseBeetleVictim(unit.beetleFlippedBy,unit.beetleFlippedBy.beetleFlip);
        if(unit.mammothExpulsion!=null)EndMammothExpulsion(unit,unit.mammothExpulsion);
        if(unit.mammothBrace!=null)EndMammothBrace(unit,unit.mammothBrace);
        FinishMycoking(unit);
        CleanupMantisDeathMolt(unit);
        var domain=FamilyTranscendenceStateFor(unit,false);
        if(domain!=null&&IsSpiderFamily(unit))
        {
            if(domain.silkDomain!=null)domain.silkDomain.Break();
            domain.silkDomain=null;domain.timer=0f;
        }
        unit.leaping=false;
        if(unit.mantisReaperAction!=null)EndMantisReaperAction(unit,false);
        if(unit.mantisReaperFx!=null)unit.mantisReaperFx.Cancel();
        unit.mantisReaperFx=null;
        if(unit.rank3MantisReaper&&unit.model!=null)
        {
            unit.model.transform.localScale=unit.rank3MantisBaseScale;
            unit.combatBodyScale=unit.rank3MantisBaseBodyScale;
            CacheMeleeReadabilityBody(unit);
            unit.targetRing?.SetRadius(Mathf.Max(.3f,CombatBodyRadius(unit)*1.15f));
            unit.skillCooldownDuration/=.75f;
            unit.rank3MantisReaper=false;
        }
        for(int i=0;i<units.Count;i++)
        {
            Unit special=units[i];
            if(special==unit)continue;
            if(special.rank3SummonOwner==unit&&!special.rank3MycokingSummon)
                DeactivateRank3Special(special);
        }
    }

    private void DeactivateRank3Special(Unit unit)
    {
        if(unit==null||!unit.alive)return;
        FinishMycoking(unit);
        if(occupancy.TryGetValue(unit.currentTile,out Unit occupant)&&occupant==unit)
            occupancy.Remove(unit.currentTile);
        if(unit.silkPuppetFx!=null)unit.silkPuppetFx.Depart();
        unit.spriteAnimation?.InterruptCurrentAction(true);
        ReleaseAttackSlot(unit);
        unit.alive=false;
        unit.velocity=Vector3.zero;
        unit.combatStatuses.Clear();
        if(unit.ring!=null)unit.ring.SetActive(false);
        if(unit.model!=null&&!CombatEffectEndFade.IsFading(unit.model.transform))unit.model.SetActive(false);
    }

    private Tile Rank3SpawnTile(Vector3 desired)
    {
        return tiles.Values.Where(tile=>tile.walkable&&
                CanOccupySpawn(tile.coordinate,Vector2Int.one))
            .OrderBy(tile=>(Flat(tile.world)-desired).sqrMagnitude)
            .FirstOrDefault();
    }

    private bool ResolveRank3BasicAttack(Unit source,Unit target,float attackDamage)
    {
        if(!IsCombatActive(source)||!IsCombatActive(target))return false;
        if(source.rank3MycokingSummon)return true;
        FireGriffinRoyalWind(source,target);
        return false;
    }

    private void ApplyTyrannoRank3Intrinsic(Unit source,IEnumerable<Unit> victims,
        Vector3 center,float radius,int hits)
    {
        List<Unit> living=victims.Where(IsCombatActive).Distinct().ToList();
        if(HasRank3(source,TyrannoCombatRules.TinoTraitId))
        {
            source.tyrannoFeedingReady=true;
            Heal(source,source.maxHp*TyrannoCombatRules.FeedingHealRatio);
        }
        if(HasRank3(source,TyrannoCombatRules.MagnoTraitId))
            StartReleasedSkill(source,ResolveTyrannoLavaZone(source,center,radius,combatGeneration));
        if(HasRank3(source,TyrannoCombatRules.IronRexTraitId))
            Rank3Shield(source,source,
                TyrannoCombatRules.SteelSkinShield(CurrentDefense(source),hits),
                TyrannoCombatRules.ShieldDuration);
        if(HasRank3(source,TyrannoCombatRules.BoneRexTraitId))
            for(int i=0;i<living.Count;i++)Rank3Shred(living[i],
                TyrannoCombatRules.FissureShredRatio,TyrannoCombatRules.FissureDuration,source);
    }

}
