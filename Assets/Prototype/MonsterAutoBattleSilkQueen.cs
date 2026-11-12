using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private void BeginSilkQueenDomain(Unit source,Unit target,FamilyTranscendenceState state)
    {
        state.center=Flat(combatBounds.center);
        state.timer=FamilyTranscendenceRules.ArachneZoneDuration;
        state.charges=FamilyTranscendenceRules.ArachnePuppetLimit;
        if(state.silkDomain!=null)state.silkDomain.Cancel();
        state.silkDomain=null;
        if(!QueueRank4Cinematic(source))
        {
            PlaySpiderSound(source,SpiderSound.Domain,state.center);
            RevealSilkQueenDomain(source,state);
        }
    }

    private bool SilkQueenCinematicPending(Unit source)=>
        rank4Presentation?.request.source==source&&rank4Presentation.request.context.family=="spider"||
        rank4PresentationQueue.Any(r=>r.source==source&&r.context.family=="spider");

    private void RevealSilkQueenDomain(Unit source,FamilyTranscendenceState state,float visualAge=-1f)
    {
        if(mlEpisodeActive||battleEnding||!battleStarted||state==null||state.releasedEffectsCancelled||
           state.timer<=0f||!IsCombatActive(source)||source.hp<=0f)return;
        if(state.silkDomain==null)state.silkDomain=PixelSilkQueenDomain.Create(combatBounds,Rank3Renderer(source));
        if(visualAge>=0f)state.silkDomain.Reveal(visualAge);
    }

    private IEnumerator CommitSilkQueenCocoons(Unit source)
    {
        if(!battleStarted||battleEnding||!IsCombatActive(source)||!HasFamilyTranscendence(source)||
           !IsSpiderFamily(source)||!source.spiderWebPending)return null;
        float scale=source.spiderWebDamageMultiplier,radius=source.spiderWebRadiusMultiplier;
        source.spiderWebPending=false;
        source.spiderWebDamageMultiplier=source.spiderWebRadiusMultiplier=1f;
        source.spiderWebCharge?.Cancel();source.spiderWebCharge=null;
        return ResolveSilkQueenCocoons(source,source.spiderWebPrimaryTarget??source.target,
            source.spiderWebTarget,scale,radius,combatGeneration);
    }

    private void ReleaseSilkQueenCocoons(Unit source)
    {
        IEnumerator cast=CommitSilkQueenCocoons(source);
        // Open behind the return wipe; native timed ticks wait for battlefield time to resume.
        if(cast!=null)StartCoroutine(TrackReleasedSkill(source,cast));
    }

    // Reserve during damage resolution; spawn next frame after death frees its tile.
    // This also avoids modifying units inside a skill's live enumeration.
    private Unit ReserveSilkPuppet(Unit defeated)
    {
        if(defeated==null||defeated.hp>0f||defeated.model==null||defeated.silkHarvested||
           defeated.augmentShadowClone||defeated.rank3MycokingSummon||defeated.silkPuppet)return null;
        foreach(var pair in familyTranscendenceStates)
        {
            var owner=pair.Key;var state=pair.Value;
            if(!IsCombatActive(owner)||owner.hp<=0f||!HasFamilyTranscendence(owner)||
               !IsSpiderFamily(owner)||owner.team==defeated.team||state.timer<=0f||state.charges<=0||
               state.silkPuppetsPending+units.Count(u=>u.silkPuppet&&u.rank3SummonOwner==owner&&
                   IsCombatActive(u))>=FamilyTranscendenceRules.ArachnePuppetLimit)continue;
            defeated.silkHarvested=true;state.charges--;state.silkPuppetsPending++;
            return owner;
        }
        return null;
    }

    private void TryHarvestSilkPuppet(Unit defeated)
    {
        Unit owner=ReserveSilkPuppet(defeated);
        if(owner==null)return;
        StartCombatCoroutine(owner,RaiseSilkPuppet(owner,defeated,FamilyTranscendenceStateFor(owner),
            Flat(defeated.model.transform.position),combatGeneration));
    }

    private IEnumerator RaiseSilkPuppet(Unit owner,Unit defeated,FamilyTranscendenceState state,
        Vector3 position,int generation)
    {
        try
        {
            yield return null;
            if(generation!=combatGeneration||battleEnding||!IsCombatActive(owner)||owner.hp<=0f||
               FamilyTranscendenceStateFor(owner,false)!=state)yield break;
            Tile tile=Rank3SpawnTile(position);if(tile==null)yield break;
            var owned=PlayerProfileStore.CreateMonster(defeated.definition,
                "silk_puppet_"+combatGeneration+"_"+defeated.id,false);
            Unit puppet=SpawnUnit(defeated.definition,owner.team,tile.coordinate,owned,false);
            puppet.silkPuppet=true;puppet.silkHarvested=true;puppet.augmentShadowClone=true;
            puppet.augmentTranscended=false;puppet.deploymentSlots=0;puppet.skillId="";
            puppet.activeTraitIds.Clear();puppet.itemModifiers=new CombatItemModifiers();
            puppet.damageTakenMultiplier=puppet.augmentBasicDamageMultiplier=puppet.augmentSkillPowerMultiplier=1f;
            puppet.maxHp=owner.maxHp*FamilyTranscendenceRules.ArachnePuppetHealthRatio;puppet.hp=puppet.maxHp;
            puppet.damage=owner.damage*FamilyTranscendenceRules.ArachnePuppetDamageRatio;
            puppet.defense=0f;puppet.statusResistance=0f;puppet.attackInterval=1f/BombBrick.Combat.CombatStatFormula.CombatAttackRate;puppet.cooldown=0f;
            puppet.rank3SummonOwner=owner;puppet.rank3SummonTimer=FamilyTranscendenceRules.ArachnePuppetDuration;
            puppet.silkPuppetRise=PixelSilkPuppet.RiseDuration;
            puppet.displayName=defeated.definition.displayName+" · 실인형";
            puppet.model.name="Silk Puppet - "+defeated.definition.monsterId;
            puppet.facingDirection=defeated.facingDirection;
            puppet.model.transform.rotation=Quaternion.LookRotation(puppet.facingDirection.sqrMagnitude>.01f?
                puppet.facingDirection:Vector3.forward,Vector3.up);
            if(Vector3.Distance(Flat(tile.world),position)<TileSize)
                puppet.model.transform.position=position+Vector3.up*puppet.visualHeightOffset;
            PlaySpiderSound(owner,SpiderSound.Puppet,puppet.model.transform.position);
            if(!mlEpisodeActive)
                puppet.silkPuppetFx=PixelSilkPuppet.Create(Rank3Renderer(puppet),()=>
                    this!=null&&generation==combatGeneration);
        }
        finally{state.silkPuppetsPending=Mathf.Max(0,state.silkPuppetsPending-1);}
    }

    private void AttackSilkPuppet(Unit source,Unit target)
    {
        if(source.silkPuppetRise>0f||!IsCombatTargetable(target))return;
        RotateUnit(source,target.model.transform.position-source.model.transform.position,1f);
        source.basicAttackCount++;source.cooldown=CurrentAttackInterval(source);
        float speed=SpriteBasicAttackPlaybackSpeed(source,source.attackInterval);
        source.attackMovementLockTimer=source.spriteAnimation==null?.3f:
            Mathf.Max(.3f,source.spriteAnimation.BasicAttackDuration/speed);
        int generation=combatGeneration;
        if(source.spriteAnimation!=null)
            source.spriteAnimation.PlayBasicAttack(speed,()=>
            {
                if(!battleEnding&&generation==combatGeneration&&IsCombatActive(source)&&IsCombatTargetable(target))
                    BeginBasicAttackImpact(source,target,source.damage,false,speed);
            });
        else BeginBasicAttackImpact(source,target,source.damage,false);
    }
}
