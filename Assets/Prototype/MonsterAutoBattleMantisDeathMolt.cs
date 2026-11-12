using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MantisMoltState
    {
        public float age,hatchAge=-1f,fullHp;
        public int spawned,generation;
        public PixelMantisCocoon cocoon;
        public readonly List<Unit> children=new List<Unit>(3);
    }

    private static bool MantisOffenseSuspended(Unit unit)=>unit!=null&&
        (unit.mantisMolt!=null||unit.mantisBroodOwner!=null&&
         (unit.mantisBroodOwner.mantisMolt==null||unit.mantisBroodOwner.mantisMolt.hatchAge>=0f));

    private bool TryBeginMantisDeathMolt(Unit unit,Unit attacker)
    {
        if(unit==null||unit.hp>0f||unit.mantisMoltUsed||unit.mantisBroodOwner!=null||
           !IsCombatActive(unit)||!IsMantisFamily(unit)||!HasFamilyTranscendence(unit))return false;
        unit.mantisMoltUsed=true;
        FamilyTranscendenceStateFor(unit);
        var state=new MantisMoltState{fullHp=unit.maxHp,generation=combatGeneration};
        unit.mantisMolt=state;
        StopMantisMoltAction(unit);
        unit.hp=state.fullHp*.3f;
        foreach(Unit enemy in units)
            if(enemy.target==unit){SetTarget(enemy,null);enemy.decisionTimer=0f;}
        unit.currentAction="mantis_cocoon";unit.traitActivations++;
        if(!mlEpisodeActive)
        {
            string species=unit.definition.monsterId;
            int row=species=="flotis"?1:species=="santius"?2:species=="nectis"?3:0;
            if(species=="flotis"&&unit.ownedInstance!=null&&unit.ownedInstance.isShiny)row=4;
            state.cocoon=PixelMantisCocoon.Play(Rank3Renderer(unit),row);
        }
        PlayMantisSound(unit,MantisSound.Cocoon,Rank3Center(unit));
        AddCombatFloatText(unit,"죽음 탈피",new Color(.83f,.94f,.56f),true);
        QueueRank4Cinematic(unit,attacker==null?null:new[]{attacker});
        return true;
    }

    private void StopMantisMoltAction(Unit unit)
    {
        if(unit.mantisGuillotine!=null)EndMantisGuillotine(unit,unit.mantisGuillotine);
        if(unit.mantisReaperAction!=null)EndMantisReaperAction(unit,false);
        InterruptPositionalAction(unit);
        unit.mantisScythePending=false;unit.mantisScytheTarget=null;
        unit.spriteAnimation?.InterruptCurrentAction(true);
        unit.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);
        unit.attackMovementLockTimer=0f;unit.hexStepping=false;unit.displacing=false;
        unit.velocity=Vector3.zero;ClearRoute(unit);ReleaseAttackSlot(unit);SetMoving(unit,false);
        SetTarget(unit,null);
    }

    private void TickMantisDeathMolt(Unit unit,float dt)
    {
        var state=unit.mantisMolt;if(state==null||!IsCombatActive(unit)||unit.hp<=0f)return;
        if(state.generation!=combatGeneration){CleanupMantisDeathMolt(unit);return;}
        BeginMantisHatchWhenBroodGone(unit);
        unit.velocity=Vector3.zero;SetMoving(unit,false);
        if(unit.ring!=null)unit.ring.transform.position=Flat(unit.model.transform.position)+Vector3.up*.025f;
        unit.currentAction=state.hatchAge<0f?"mantis_cocoon":"mantis_hatching";
        state.age+=dt;
        if(state.hatchAge>=0f)
        {
            state.hatchAge+=dt;
            state.cocoon?.Pose(state.age,Mathf.Clamp01((state.hatchAge-.35f)/.4f));
            if(state.hatchAge>=.8f)HatchMantisCocoon(unit);
            return;
        }
        if(state.children.Any(IsCombatActive))
            unit.hp=Mathf.Min(unit.maxHp,unit.hp+GateHaetaeBenefit(unit,state.fullHp*.03f*dt,CombatBenefitKind.Healing));
        state.cocoon?.Pose(state.age,0f);
        while(state.spawned<3&&state.age>=.32f+state.spawned*.12f)
        {
            Unit child=SpawnMantisBroodling(unit,state,state.spawned);
            if(child==null)break; // Retry when a deployment tile becomes available.
            state.children.Add(child);state.cocoon?.Birth(state.spawned);state.spawned++;
            PlayMantisSound(unit,MantisSound.Birth,Rank3Center(unit),.8f);
        }
    }

    private Unit SpawnMantisBroodling(Unit owner,MantisMoltState state,int index)
    {
        Vector3 forward=Flat(owner.facingDirection);
        if(forward.sqrMagnitude<.01f)forward=owner.team==Team.Player?Vector3.right:Vector3.left;
        Vector3 direction=Quaternion.AngleAxis((index-1)*110f,Vector3.up)*forward.normalized;
        Tile tile=Rank3SpawnTile(Flat(owner.model.transform.position)+direction*TileSize);
        if(tile==null)return null;
        var owned=PlayerProfileStore.CreateMonster(owner.definition,
            "mantis_brood_"+owner.stableId+"_"+combatGeneration+"_"+index,false);
        if(owner.ownedInstance!=null)owned.personalityId=owner.ownedInstance.personalityId;
        Unit child=SpawnUnit(owner.definition,owner.team,tile.coordinate,owned,false);
        ConfigureMantisBroodling(owner,child,state.fullHp);
        child.mantisBroodEnd=child.model.transform.position;
        child.mantisBroodRise=.32f;child.model.transform.position=owner.model.transform.position;
        RotateUnit(child,direction,1f);SetMoving(child,true);
        return child;
    }

    private void ConfigureMantisBroodling(Unit owner,Unit child,float fullHp)
    {
        child.mantisBroodOwner=owner;child.rank3SummonOwner=owner;
        child.augmentShadowClone=true;child.augmentTranscended=false;child.mantisMoltUsed=true;
        child.deploymentSlots=0;child.expeditionRank=owner.expeditionRank;
        child.skillId=owner.skillId;child.activeTraitIds.Clear();
        child.activeTraitIds.AddRange(owner.activeTraitIds.Where(id=>
            id.StartsWith("intrinsic_",System.StringComparison.Ordinal)||
            id==MantisRank3Rules.DivineGuillotine||id==MantisRank3Rules.MyriadEyes||id==MantisRank3Rules.ReaperEmergence));
        child.itemModifiers=new CombatItemModifiers();child.maxHp=fullHp*.15f;child.hp=child.maxHp;
        child.damage=owner.damage*.3f;child.defense=owner.defense;child.statusResistance=owner.statusResistance;
        child.attackInterval=owner.attackInterval;child.attackRange=owner.attackRange;
        child.speedTilesPerSecond=owner.speedTilesPerSecond;child.collisionRadius=owner.collisionRadius*.55f;
        child.combatBodyScale=owner.rank3MantisReaper?owner.rank3MantisBaseBodyScale:owner.combatBodyScale;
        ConfigureTargetRing(child);
        child.skillCooldownDuration=owner.skillCooldownDuration/(owner.rank3MantisReaper ? .75f:1f);
        child.model.name="Mantis Broodling "+owner.definition.name;
        child.model.transform.localScale=(owner.rank3MantisReaper?owner.rank3MantisBaseScale:owner.model.transform.localScale)*.55f;
        child.baseModelScale=child.model.transform.localScale;
        CacheMeleeReadabilityBody(child);
        float fallbackShadowScale=.4f;
        fallbackShadowScale*=CombatReadabilityPresentation.BodyVisualScale;
        if(child.ring!=null)child.ring.transform.localScale=owner.ring==null?Vector3.one*fallbackShadowScale:owner.ring.transform.localScale*.55f;
        if(HasRank3(child,MantisRank3Rules.ReaperEmergence))
        {ActivateMantisReaper(child);TickMantisReaper(child,MantisRank3Rules.ReaperMoltEnd);}
        child.skillTimer=0f;child.cooldown=0f;
    }

    private bool TickMantisBroodling(Unit child,float dt)
    {
        Unit owner=child.mantisBroodOwner;if(owner==null)return false;
        if(!IsCombatActive(owner)||owner.mantisMolt==null)
        {RetireMantisBroodling(child);return true;}
        if(owner.mantisMolt.hatchAge>=0f)return true;
        if(child.mantisBroodRise<=0f)return false;
        child.mantisBroodRise=Mathf.Max(0f,child.mantisBroodRise-dt);
        float t=1f-child.mantisBroodRise/.32f;
        child.model.transform.position=Vector3.Lerp(owner.model.transform.position,child.mantisBroodEnd,t);
        if(child.ring!=null)child.ring.transform.position=Flat(child.model.transform.position)+Vector3.up*.025f;
        if(child.mantisBroodRise<=0f){SetMoving(child,false);child.decisionTimer=0f;}
        return true;
    }

    // Also checked by the cocoon tick, so removal outside damage resolution cannot strand it.
    private void BeginMantisHatchWhenBroodGone(Unit owner)
    {
        var state=owner==null?null:owner.mantisMolt;
        if(state!=null&&state.hatchAge<0f&&state.spawned==3&&!state.children.Any(IsCombatActive))
            state.hatchAge=0f;
    }

    private void HatchMantisCocoon(Unit owner)
    {
        var state=owner.mantisMolt;if(state==null||owner.hp<=0f)return;
        foreach(Unit child in state.children)RetireMantisBroodling(child);
        owner.mantisMolt=null;
        state.cocoon?.Finish(true);
        PlayMantisSound(owner,MantisSound.Hatch,Rank3Center(owner));
        owner.spriteAnimation?.InterruptCurrentAction(true);
        owner.mantisReaperFx?.Rest();owner.cooldown=.25f;owner.decisionTimer=0f;
        owner.currentAction="mantis_hatched";
        AddCombatFloatText(owner,"부화",new Color(.83f,.94f,.56f),true);
    }

    private void RetireMantisBroodling(Unit child)
    {
        if(child==null)return;
        if(child.mantisGuillotine!=null)EndMantisGuillotine(child,child.mantisGuillotine);
        CleanupRank3Unit(child);DeactivateRank3Special(child);
    }

    private void CleanupMantisDeathMolt(Unit owner,bool broken=false)
    {
        var state=owner.mantisMolt;if(state==null)return;owner.mantisMolt=null;
        foreach(Unit child in state.children)RetireMantisBroodling(child);
        owner.maxHp=state.fullHp;
        if(broken)state.cocoon?.Finish(false);else state.cocoon?.Cancel();
    }
}
