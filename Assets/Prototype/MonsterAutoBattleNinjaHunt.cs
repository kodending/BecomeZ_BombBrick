using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private static bool HasNinjaRank3(Unit unit)=>
        HasRank3(unit,NinjaCatRank3Rules.ShadowlessCross)||
        HasRank3(unit,NinjaCatRank3Rules.BlackMoonHunt)||
        HasRank3(unit,NinjaCatRank3Rules.EightGateFormation);

    private bool ShadowHuntValid(Unit unit,int generation)=>
        generation==combatGeneration&&!battleEnding&&IsCombatActive(unit)&&unit.hp>0f&&
        unit.ninjaHuntPending&&IsCombatTargetable(unit.ninjaHuntTarget)&&unit.ninjaHuntTarget.hp>0f;

    private void VanishShadowHunt(Unit unit)
    {
        if(!unit.ninjaCrossActive)unit.ninjaUntargetableTimer=NinjaCatCombatRules.UntargetableDuration;
        foreach(Unit enemy in units)
            if(IsCombatActive(enemy)&&enemy.team!=unit.team&&enemy.target==unit)SetTarget(enemy,null);
        PlayNinjaSound(unit,NinjaSound.Blink,unit.model.transform.position);
        if(mlEpisodeActive||unit.spriteAnimation==null)return;
        PixelNinjaSubstitution.PlayVanish(unit.spriteAnimation.GetComponent<SpriteRenderer>());
    }

    private void SubstituteShadowHunt(Unit unit,int generation)
    {
        if(mlEpisodeActive||unit.spriteAnimation==null)return;
        PlayNinjaSound(unit,NinjaSound.Log,unit.model.transform.position);
        PixelNinjaSubstitution.PlayLog(unit.spriteAnimation.GetComponent<SpriteRenderer>(),
            ()=>this!=null&&generation==combatGeneration&&!battleEnding);
    }

    private void ArriveShadowHunt(Unit unit)
    {
        Unit target=unit.ninjaHuntTarget;
        if(!IsCombatTargetable(target)||target.hp<=0f)
        {
            unit.spriteAnimation?.InterruptCurrentAction();return;
        }
        Vector3 forward=Flat(target.model.transform.position)-unit.ninjaHuntStart;
        if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
        if(!TryResolveShadowSlashLanding(unit,target,unit.ninjaHuntStart,
            forward.normalized,out Vector3 landing))
        {
            Vector3 current=Flat(unit.model.transform.position),previous=unit.ninjaHuntLanding;
            landing=IsOpenAttackPosition(unit,null,previous)&&
                HasClearPathSegment(current,previous,unit.collisionRadius*.55f)?previous:current;
        }
        unit.ninjaHuntLanding=landing;
        unit.model.transform.position=landing+Vector3.up*unit.visualHeightOffset;
        unit.ring.transform.position=landing+Vector3.up*.025f;
        unit.lastGroundPosition=landing;
        OccupyUnit(unit,NearestGridCoordinate(landing));
        Vector3 facing=target.model.transform.position-unit.model.transform.position;
        RotateUnit(unit,facing,1f);
        unit.spriteBillboard?.SetFacingDirection(facing,true);
        PlayNinjaSound(unit,NinjaSound.Blink,unit.model.transform.position,.7f);
        if(!mlEpisodeActive&&unit.spriteAnimation!=null)
            PixelNinjaSubstitution.PlayArrival(unit.spriteAnimation.GetComponent<SpriteRenderer>(),NinjaHuntColor(unit));
    }

    private static Color NinjaHuntColor(Unit source)
    {
        if(MonsterTraitRuntime.Has(source.activeTraitIds,NinjaCatCombatRules.SeolmyoTraitId))return new Color(.52f,.88f,1f);
        if(MonsterTraitRuntime.Has(source.activeTraitIds,NinjaCatCombatRules.SundongiTraitId))return new Color(.48f,.82f,.40f);
        if(MonsterTraitRuntime.Has(source.activeTraitIds,NinjaCatCombatRules.HongryeonTraitId))return new Color(1f,.42f,.16f);
        return new Color(.96f,.87f,.63f);
    }

    private void PlayShadowHuntCut(Unit source,Unit target,bool followup)
    {
        if(mlEpisodeActive||target.spriteAnimation==null)return;
        var renderer=target.spriteAnimation.GetComponent<SpriteRenderer>();
        Vector3 direction=target.model.transform.position-source.model.transform.position;
        PixelNinjaSubstitution.PlayCut(renderer,direction,NinjaHuntColor(source),followup);
        target.spriteAnimation.PlayImpactRecoil(direction,followup?.035f:.065f);
    }

    private void PrepareNinjaOpeningStrike(Unit source)
    {
        if(!HasRank3(source,NinjaCatCombatRules.ChundongiTraitId))return;
        source.ninjaOpeningStrikeReady=true;
        source.cooldown=0f;
        source.traitActivations++;
    }

    private void BeginNinjaOpeningAttack(Unit source,Unit target,float damage,bool critical,float speed)
    {
        // The regular attack decision supplies the target and range; skill completion never teleports.
        source.ninjaOpeningStrikeReady=false;
        int generation=combatGeneration,revision=source.positionRevision,cast=source.skillUses;
        System.Action impact=()=>
        {
            if(!NinjaOpeningHitValid(source,target,generation,revision,cast))return;
            ResolveNinjaOpeningHit(source,target,damage,critical,speed,true);
            if(source.spriteAnimation!=null&&!mlEpisodeActive)
                StartCombatCoroutine(source,ResolveNinjaOpeningSecondHit(source,target,damage,critical,speed,generation,revision,cast));
            else if(NinjaOpeningHitValid(source,target,generation,revision,cast))
                ResolveNinjaOpeningHit(source,target,damage,critical,speed,false);
        };
        if(source.spriteAnimation!=null)source.spriteAnimation.PlayBasicAttack(speed,impact);
        else impact();
    }

    private bool NinjaOpeningHitValid(Unit source,Unit target,int generation,int revision,int cast)=>
        generation==combatGeneration&&!battleEnding&&IsCombatActive(source)&&source.hp>0f&&
        IsCombatTargetable(target)&&target.hp>0f&&source.positionRevision==revision&&source.skillUses==cast&&
        source.giraffeStunTimer<=0f&&!source.displacing&&source.preyDraggedBy==null&&
        !SpiderSilkBound(source)&&!source.combatStatuses.Has(CombatStatusKind.Freeze)&&
        !source.combatStatuses.Has(CombatStatusKind.Knockdown)&&!source.combatStatuses.Has(CombatStatusKind.Airborne)&&
        !source.combatStatuses.Has(CombatStatusKind.Fear)&&IsImmediatelyAttackable(source,target);

    private IEnumerator ResolveNinjaOpeningSecondHit(Unit source,Unit target,float damage,bool critical,
        float speed,int generation,int revision,int cast)
    {
        yield return new WaitForSeconds(.06f);
        if(NinjaOpeningHitValid(source,target,generation,revision,cast))
            ResolveNinjaOpeningHit(source,target,damage,critical,speed,false);
    }

    private void ResolveNinjaOpeningHit(Unit source,Unit target,float damage,bool critical,float speed,bool first)
    {
        bool empowered=source.ninjaEmpoweredBasicAttacks>0;
        if(empowered)source.ninjaEmpoweredBasicAttacks--;
        damage=NinjaCatCombatRules.EmpoweredBasicDamage(
            NinjaCatCombatRules.OpeningStrikeDamage(damage),empowered);
        PlayShadowHuntCut(source,target,!first);
        bool previousCritical=source.lastBasicAttackCritical;
        source.lastBasicAttackCritical=critical;
        if(first)BeginBasicAttackImpact(source,target,damage,critical,speed);
        else ResolveBasicAttackImpact(source,target,damage);
        source.lastBasicAttackCritical=previousCritical;
    }

    private void BeginNinjaCross(Unit source,int generation)
    {
        Unit target=source.ninjaHuntTarget;
        int cast=source.skillUses,revision=source.positionRevision;
        float scale=source.ninjaHuntDamageMultiplier;
        int hitsBefore=source.skillHits;float damageBefore=source.skillDamage;
        source.spriteAnimation.PlayNinjaCrossAttack(
            ()=>ShadowHuntValid(source,generation)&&source.skillUses==cast&&source.positionRevision==revision,
            ()=>{source.ninjaCrossActive=true;VanishShadowHunt(source);source.traitActivations++;},
            ()=>SubstituteShadowHunt(source,generation),
            step=>MoveNinjaCross(source,target,step),
            step=>HitNinjaCross(source,target,step,scale),
            ()=>
            {
                source.ninjaCrossActive=false;
                if(source.skillUses!=cast)return;
                source.ninjaHuntPending=false;source.ninjaHuntTarget=null;source.ninjaHuntDamageMultiplier=1f;
                if(generation==combatGeneration)
                    RecordSkillEvent(source,NinjaCatRank3Rules.ShadowlessCross,
                        source.skillHits-hitsBefore,source.skillDamage-damageBefore);
            });
    }

    private bool MoveNinjaCross(Unit source,Unit target,int step)
    {
        if(!IsCombatTargetable(target)||target.hp<=0f)return false;
        Vector3 direction=step==4?-Flat(target.facingDirection):
            Quaternion.AngleAxis(step*90f,Vector3.up)*Vector3.forward;
        if(!MoveNinjaToAttackPosition(source,target,direction,step>0,step==4))
        {
            source.ninjaHuntPending=false;return false;
        }
        source.currentAction=step==4?"ninja_cross_finisher_prepare":"ninja_cross_step_"+step;
        return true;
    }

    private bool MoveNinjaToAttackPosition(Unit source,Unit target,Vector3 direction,bool afterimage,bool arrival)
    {
        if(!IsCombatTargetable(target)||target.hp<=0f)return false;
        Vector3 start=Flat(source.model.transform.position),center=Flat(target.model.transform.position);
        if(direction.sqrMagnitude<.001f)direction=center-source.ninjaHuntStart;
        if(direction.sqrMagnitude<.001f)direction=Vector3.forward;
        if(!TryResolveShadowSlashLanding(source,target,start,direction.normalized,out Vector3 landing))
            return false;
        var renderer=Rank3Renderer(source);
        if(afterimage&&!mlEpisodeActive&&renderer!=null)
        {
            var ghost=Rank3CombatFx.Ghost("Ninja Cross Afterimage",renderer,renderer.transform.position,
                new Color(.72f,.8f,1f,.32f),1f,.10f);
            if(ghost!=null){ghost.transform.rotation=renderer.transform.rotation;ghost.sortingOrder=renderer.sortingOrder-1;}
        }
        source.ninjaHuntLanding=landing;
        source.model.transform.position=landing+Vector3.up*source.visualHeightOffset;
        if(source.ring!=null)source.ring.transform.position=landing+Vector3.up*.025f;
        source.lastGroundPosition=landing;source.velocity=Vector3.zero;
        OccupyUnit(source,NearestGridCoordinate(landing));
        Vector3 facing=target.model.transform.position-source.model.transform.position;
        RotateUnit(source,facing,1f);source.spriteBillboard?.SetFacingDirection(facing,true);
        PlayNinjaSound(source,NinjaSound.Blink,source.model.transform.position,.7f);
        if(arrival&&!mlEpisodeActive)PixelNinjaSubstitution.PlayArrival(renderer,NinjaHuntColor(source));
        return true;
    }

    private void HitNinjaCross(Unit source,Unit target,int step,float scale)
    {
        if(!IsCombatActive(source)||source.hp<=0f||!IsCombatTargetable(target)||target.hp<=0f)return;
        Vector3 center=Flat(target.model.transform.position);
        if(step<4)
        {
            PlayNinjaSound(source,NinjaSound.Slash,center);
            Vector3 start=Flat(source.model.transform.position),direction=center-start;
            var path=units.Where(enemy=>enemy!=target&&enemy.team!=source.team&&IsCombatTargetable(enemy)&&
                IsPointInsideForwardSkillLine(start,direction.normalized,Flat(enemy.model.transform.position),
                    direction.magnitude,TileSize*.38f+enemy.collisionRadius)).ToArray();
            PlayShadowHuntCut(source,target,step%2==1);
            Rank3Hit(source,target,NinjaCatRank3Rules.CrossHitRatio*scale);
            foreach(Unit enemy in path)
            {
                PlayShadowHuntCut(source,enemy,step%2==1);
                Rank3Hit(source,enemy,NinjaCatRank3Rules.CrossPathRatio*scale);
            }
        }
        else
        {
            PlayNinjaSound(source,NinjaSound.Finisher,center);
            if(!mlEpisodeActive)
            {
                PixelNinjaSubstitution.PlayCut(Rank3Renderer(target),
                    target.model.transform.position-source.model.transform.position,NinjaHuntColor(source),false,true);
                target.spriteAnimation?.PlayImpactRecoil(center-Flat(source.model.transform.position),.085f);
            }
            float missing=Mathf.Max(0f,target.maxHp-target.hp)*NinjaCatRank3Rules.ExecutionMissingHpRatio*scale;
            Rank3Hit(source,target,NinjaCatRank3Rules.ExecutionRatio*scale,NinjaCatRank3Rules.ExecutionDefenseIgnore,missing);
            if(IsCombatActive(target)&&target.hp>0f)ApplyNinjaRank3Intrinsic(source,target,center,true,true);
            source.ninjaEmpoweredBasicAttacks=Mathf.Max(source.ninjaEmpoweredBasicAttacks,NinjaCatCombatRules.EmpoweredBasicAttackCount);
            PrepareNinjaOpeningStrike(source);
            source.ninjaCrossActive=false;source.currentAction="ninja_cross_finisher";
        }
    }

    private void ResolveNinjaCrossImmediate(Unit source,Unit target,float scale,int generation)
    {
        // Sprite-free simulation uses the same ordered movement and damage without presentation delays.
        for(int step=0;step<5;step++)
        {
            if(generation!=combatGeneration||!IsCombatActive(source)||source.hp<=0f||
               !IsCombatTargetable(target)||target.hp<=0f||!MoveNinjaCross(source,target,step))break;
            HitNinjaCross(source,target,step,scale);
        }
        source.ninjaCrossActive=false;source.traitActivations++;
    }
}
