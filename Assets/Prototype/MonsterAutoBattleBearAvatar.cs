using System.Collections;
using UnityEngine;
using BombBrick.Game.Core;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool BearAvatarActive(Unit unit)
    {
        return IsBearFamily(unit)&&FamilyTranscendenceStateFor(unit,false)?.timer>0f;
    }

    private void BeginBearAvatar(Unit source)
    {
        FamilyTranscendenceState state=FamilyTranscendenceStateFor(source);
        bool entering=state.timer<=0f;
        bool cinematic=QueueBearAvatarCinematic(source);
        if(state.bearDeparture!=null)state.bearDeparture.Cancel();
        state.bearDeparture=null;
        state.timer=FamilyTranscendenceRules.BearAvatarDuration;
        source.spriteAnimation?.SetBearAvatar(true);
        if(entering&&!cinematic)PlayBearSound(source,BearSound.Transform,source.model.transform.position);
        PixelCombatEffect.StopBearMountainColossusHologram(source.bearMountainHologram);
        source.bearMountainHologram=null;
        UpdateBearMountainScale(source,0f);
        source.traitActivations++;
        PlayFamilyTranscendenceFeedback(source,"bear");
        if(source.bearAvatarRoarPending)return;
        source.bearAvatarRoarPending=true;
        StartCombatCoroutine(source,PrepareBearAvatarRoar(source,combatGeneration,source.positionRevision));
    }

    private void EndBearAvatar(Unit source,bool releaseSoul=false)
    {
        source.bearAvatarRoarPending=false;
        CancelBearAvatarCinematicFor(source);
        FamilyTranscendenceState state=FamilyTranscendenceStateFor(source,false);
        if(state!=null)
        {
            state.timer=0f;
            if(state.bearDeparture!=null)state.bearDeparture.Cancel();
            state.bearDeparture=null;
        }
        if(source.spriteAnimation==null)return;
        if(releaseSoul&&state!=null&&source.spriteAnimation.BearAvatarActive&&
           IsCombatActive(source)&&!mlEpisodeActive&&!battleEnding)
        {
            int generation=combatGeneration;
            state.bearDeparture=PixelBearAvatarDeparture.Create(
                source.spriteAnimation.GetComponent<SpriteRenderer>(),
                ()=>this!=null&&isActiveAndEnabled&&!battleEnding&&generation==combatGeneration&&
                    IsCombatActive(source)&&source.hp>0f);
        }
        source.spriteAnimation.SetBearAvatar(false);
    }

    private IEnumerator PrepareBearAvatarRoar(Unit source,int generation,int revision)
    {
        try
        {
            // Finish the transforming pose before the spirit itself takes a second breath.
            while(!battleEnding&&generation==combatGeneration&&IsCombatActive(source)&&BearAvatarActive(source)&&
                  source.bearAvatarRoarPending&&source.positionRevision==revision&&source.spriteAnimation!=null&&
                  (IsBearAvatarCinematicPending(source)||source.spriteAnimation.IsActionLocked))
                yield return null;
            if(battleEnding||generation!=combatGeneration||!IsCombatActive(source)||!BearAvatarActive(source)||
               !source.bearAvatarRoarPending||source.positionRevision!=revision||
               source.combatStatuses.Has(CombatStatusKind.Fear))
                yield break;
            StopUnitMovement(source);
            if(source.spriteAnimation==null)
            {
                ResolveBearAvatarRoar(source);
                yield break;
            }
            source.spriteAnimation.PlayBearAvatarRoar(()=>
            {
                if(battleEnding||generation!=combatGeneration||!IsCombatActive(source)||!BearAvatarActive(source)||
                   !source.bearAvatarRoarPending||source.positionRevision!=revision)return;
                source.spriteAnimation.HoldSkillAttackHitFrame(BearCombatRules.AvatarRoarImpactHoldDuration);
                ResolveBearAvatarRoar(source);
            });
            while(!battleEnding&&generation==combatGeneration&&IsCombatActive(source)&&
                  source.spriteAnimation!=null&&source.spriteAnimation.IsActionLocked)
                yield return null;
        }
        finally
        {
            if(generation==combatGeneration)source.bearAvatarRoarPending=false;
        }
    }

    private void ResolveBearAvatarRoar(Unit source)
    {
        if(!IsCombatActive(source))return;
        Vector3 center=Flat(source.model.transform.position);
        float radius=TileSize*4f;
        int affected=0;
        foreach(Unit enemy in units)
        {
            if(enemy.team==source.team||!IsCombatActive(enemy))continue;
            radius=Mathf.Max(radius,Vector3.Distance(center,Flat(enemy.model.transform.position)));
            CombatStatusEffect fear=enemy.combatStatuses.ApplyTimed(CombatStatusKind.Fear,
                HostileStatusDuration(enemy,FamilyTranscendenceRules.BearFearDuration),source);
            if(fear.remaining>0f)affected++;
        }
        if(!mlEpisodeActive)
        {
            SpriteRenderer renderer=source.model.GetComponentInChildren<SpriteRenderer>();
            PixelCombatEffect.PlayBearAvatarRoar(center,radius,renderer);
            PlayBearSound(source,BearSound.Fear,source.model.transform.position);
            combatCameraDirector?.ReportBearAvatarRoar();
        }
        RecordSkillEvent(source,source.skillId+"_avatar_fear",affected,0f);
    }

    private void BeginFear(Unit unit)
    {
        float angle=UnityEngine.Random.Range(0f,Mathf.PI*2f);
        unit.fearDirection=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
        // Preserve a body already being thrown/carried; cancel its queued attacks instead.
        if(!unit.displacing&&unit.preyDraggedBy==null)InterruptPositionalAction(unit);
        else unit.spriteAnimation?.InterruptCurrentAction();
        if(unit.fangLockTarget!=null)FinishFangLock(unit,false);
        unit.highHeadSlam?.CancelAndRestore();
        unit.neckStrike?.CancelAndRestore();
        unit.giraffeSlamming=false;
        unit.bearSpiritPending=false;
        unit.bearAvatarRoarPending=false;
        if(unit.bearResolveTimer<=0f)unit.bearSpiritFx?.Cancel();
        SetTarget(unit,null);
        ClearRoute(unit);
        ReleaseAttackSlot(unit);
        unit.hexStepping=false;
        unit.currentAction="feared";
        StopUnitMovement(unit);
        AddCombatFloatText(unit,"공포",new Color(.76f,.61f,1f),true);
    }

    private void UpdateFearMovement(Unit unit,float dt)
    {
        unit.currentAction="feared";
        if(unit.griffinRootTimer>0f||unit.combatStatuses.Has(CombatStatusKind.Root))
        {
            StopUnitMovement(unit);
            return;
        }
        Vector3 previous=Flat(unit.model.transform.position);
        float speed=CurrentMovementSpeed(unit);
        unit.velocity=CombatMovementInertia.Advance(unit.velocity,unit.fearDirection*speed,speed,dt);
        Vector3 step=unit.velocity*Mathf.Max(0f,dt);
        Vector3 ground=ResolveUnitMovement(unit,previous,previous+step);
        ground=BlockRank3IceWallMovement(unit,previous,ground);
        // Collision depenetration may suggest a jump: fear can only walk this frame's distance.
        Vector3 moved=ground-previous;
        if(moved.sqrMagnitude>step.sqrMagnitude+.000001f||Vector3.Dot(moved,step)<0f)
        {
            ground=previous;
            moved=Vector3.zero;
        }
        unit.velocity=dt>0f?moved/dt:Vector3.zero;
        unit.lastGroundPosition=ground;
        unit.currentTile=NearestGridCoordinate(ground);
        unit.model.transform.position=ground+Vector3.up*unit.visualHeightOffset;
        if(unit.ring!=null)unit.ring.transform.position=ground+Vector3.up*.025f;
        unit.moving=HasMovementProgress(moved.magnitude,dt);
        SetMoving(unit,unit.moving);
        RotateUnit(unit,unit.fearDirection,dt);
        if(unit.moving)UpdateFootstepDust(unit,moved);
    }
}
