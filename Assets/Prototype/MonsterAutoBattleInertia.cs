using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private static void ClearUnitInertia(Unit unit)
    {
        bool coasting=unit.inertiaVelocity.sqrMagnitude>0f;
        unit.inertiaVelocity=Vector3.zero;
        unit.inertiaDeceleration=0f;
        unit.meleeApproachCoasting=false;
        unit.meleeArrivalSeedSpeed=0f;unit.meleeArrivalElapsed=0f;
        if(!coasting||unit.combatStatuses.Has(CombatStatusKind.Freeze))return;
        unit.spriteAnimation?.SetMoving(unit.moving);
        if(unit.animator!=null&&unit.animator.enabled)unit.animator.SetBool("Moving",unit.moving);
    }

    private static bool InertiaBlocked(Unit unit)=>!IsCombatActive(unit)||unit.mammothRicochetActive||
        unit.crocodileAbyssDive!=null||unit.crocodileAbyssHeldBy!=null||
        unit.frogGrandSlam!=null||unit.frogGrandSlamHeldBy!=null||
        unit.sabertoothDevour!=null||unit.sabertoothDevouredBy!=null||
        unit.combatStatuses.Has(CombatStatusKind.Freeze)||
        unit.combatStatuses.Has(CombatStatusKind.Knockdown)||
        unit.combatStatuses.Has(CombatStatusKind.Airborne)||
        unit.combatStatuses.Has(CombatStatusKind.Root)||
        unit.combatStatuses.Has(CombatStatusKind.Fear)||SpiderSilkBound(unit)||
        unit.giraffeStunTimer>0f||unit.griffinRootTimer>0f||unit.crocodileImmobilizedTimer>0f||
        unit.displacing||unit.preyDraggedBy!=null||unit.griffinHeldBy!=null||
        unit.leaping||unit.shadowSlashing||unit.frogHopping||
        unit.griffinSkyfalling&&(unit.griffinHunt==null||!unit.griffinHunt.landed)||
        unit.mammothCharge!=null&&!unit.mammothCharge.landed||unit.monkeyNimbus!=null||unit.tyrannoMarch!=null||
        unit.tyrannoMaw!=null||unit.tyrannoMawPulledBy!=null||unit.tyrannoPushedBy!=null||
        unit.mantisReaperAction!=null||unit.mantisGuillotine!=null||
        unit.beetleFlip!=null||unit.beetleFlippedBy!=null||unit.beetleGravityHeld!=null||
        unit.beetleOverturn!=null||unit.beetleOverturnedBy!=null&&unit.beetleOverturnedBy.flying||
        unit.mammothToss!=null||unit.mammothBrace!=null||unit.mammothExpulsion!=null||
        unit.wyvernBreath!=null||unit.haetaeRoar!=null||unit.mantisMolt!=null||
        unit.crocodileBiteLockTimer>0f||unit.crocodilePreyFlying||unit.fangLockTarget!=null||
        unit.giraffeSlamming||unit.tongueHarpoonTarget!=null||unit.bearKingRoarEnding||unit.bearAvatarRoarPending;

    private void BeginUnitInertia(Unit unit,Vector3 velocity)
    {
        unit.meleeApproachCoasting=false;
        unit.meleeArrivalSeedSpeed=0f;unit.meleeArrivalElapsed=0f;
        if(InertiaBlocked(unit)||HitSlideControlsMovement(unit))return;
        unit.inertiaVelocity=Vector3.ClampMagnitude(Flat(velocity),CombatMovementInertia.MaximumCoastSpeed*
            BombBrick.Combat.CombatStatFormula.CombatAnimationRate);
        unit.inertiaDeceleration=CombatMovementInertia.CoastDecelerationForSpeed(unit.inertiaVelocity.magnitude);
        unit.inertiaGeneration=combatGeneration;
        unit.inertiaPositionRevision=unit.positionRevision;
        unit.moving=false;
        SetMoving(unit,false);
    }

    private void BeginSkillInertia(Unit unit,Vector3 displacement,float travelSeconds)
    {
        BeginUnitInertia(unit,Flat(displacement)/Mathf.Max(.04f,travelSeconds)*
            BombBrick.Combat.CombatStatFormula.CombatAnimationRate);
    }

    private void StopForBasicAttack(Unit unit)
    {
        if(unit.moving)BeginUnitInertia(unit,unit.velocity);
        unit.velocity=Vector3.zero;
        unit.moving=false;
        SetMoving(unit,false);
    }

    private bool TryBeginArrivalGlide(Unit unit,Vector3 destination,float dt)
    {
        Vector3 toward=Flat(destination-unit.model.transform.position);
        float distance=toward.magnitude;
        float speed=Mathf.Min(Flat(unit.velocity).magnitude,CombatMovementInertia.MaximumCoastSpeed*
            BombBrick.Combat.CombatStatFormula.CombatAnimationRate);
        if(!unit.moving||speed<=.4f||distance<=.08f||
           Vector3.Dot(Flat(unit.velocity).normalized,toward.normalized)<.985f)return false;
        float drag=CombatMovementInertia.CoastDecelerationForSpeed(speed);
        if(distance>speed*speed/(2f*drag)+speed*dt)return false;
        BeginUnitInertia(unit,toward.normalized*speed);
        if(unit.inertiaVelocity.sqrMagnitude<=.000001f)return false;
        // Begin braking before arrival, rather than arriving with no momentum left to show.
        unit.inertiaDeceleration=unit.inertiaVelocity.sqrMagnitude/(2f*distance);
        unit.velocity=Vector3.zero;
        unit.currentAction="inertia_coast";
        return true;
    }

    private void AdvanceUnitInertia(Unit unit,float dt)
    {
        if(InertiaBlocked(unit)||unit.inertiaGeneration!=combatGeneration||
           unit.inertiaPositionRevision!=unit.positionRevision)
        {ClearUnitInertia(unit);return;}
        if(unit.inertiaVelocity.sqrMagnitude<=.000001f){ClearUnitInertia(unit);return;}
        if(paused||dt<=0f)return;
        float actionSpeed=RaptorCombatRules.IceSpeedMultiplier(unit.raptorSlowTimer>0f)*
            (unit.rank3ActionSlowTimer>0f?1f-Mathf.Clamp01(unit.rank3ActionSlowRatio):1f);
        dt*=actionSpeed;
        if(dt<=0f)return;
        Vector3 previous=Flat(unit.model.transform.position);
        Vector3 ground=previous;
        float overlapCost=BodyPenetrationCost(unit,ground);
        float speed=unit.inertiaVelocity.magnitude;
        float deceleration=unit.inertiaDeceleration>0f?unit.inertiaDeceleration:CombatMovementInertia.CoastDeceleration;
        bool arrival=unit.meleeApproachCoasting&&unit.meleeArrivalSeedSpeed>.001f;
        dt=Mathf.Min(dt,arrival?Mathf.Max(0f,CombatMovementInertia.ApproachCoastDuration(
            unit.meleeArrivalSeedSpeed,deceleration)-unit.meleeArrivalElapsed):speed/deceleration);
        if(dt<=0f){ClearUnitInertia(unit);unit.velocity=Vector3.zero;return;}
        int steps=Mathf.Max(1,Mathf.CeilToInt(speed*dt/
            Mathf.Max(.05f,Mathf.Min(.15f,CombatBodyRadius(unit)*.5f))));
        float travelled=0f;
        for(int i=0;i<steps&&unit.inertiaVelocity.sqrMagnitude>.000001f;i++)
        {
            Vector3 next;
            Vector3 step=arrival?CombatMovementInertia.ApproachCoast(unit.inertiaVelocity,
                unit.meleeArrivalSeedSpeed,unit.meleeArrivalElapsed,dt/steps,deceleration,out next):
                CombatMovementInertia.Coast(unit.inertiaVelocity,dt/steps,out next,deceleration);
            if(arrival)unit.meleeArrivalElapsed+=dt/steps;
            Vector3 from=ground;
            Vector3 candidate=ResolveBodyMovement(unit,from,from+step,false);
            candidate=Vector3.MoveTowards(from,candidate,step.magnitude);
            candidate=BlockRank3IceWallMovement(unit,from,candidate);
            candidate=LimitMeleeArrivalStep(unit,from,candidate);
            float nextOverlapCost=BodyPenetrationCost(unit,candidate);
            // An existing spacing overlap may glide out; do not create a new one or cross a body.
            if(Vector3.Dot(candidate-from,step)<=0f||nextOverlapCost>overlapCost+.00000001f||
               IntroducesBodyOverlap(unit,from,candidate,true)||IntroducesBodyOverlap(unit,from,candidate,false)||
               !HasClearPathSegment(from,candidate,CombatBodyRadius(unit)))candidate=from;
            else overlapCost=nextOverlapCost;
            ground=candidate;
            Vector3 resolved=CombatMovementInertia.AfterCollision(step,ground-from,1f);
            // A wall or body absorbs the blocked momentum instead of saving it for a later burst.
            unit.inertiaVelocity=step.sqrMagnitude>.000000000001f?resolved.normalized*next.magnitude*
                Mathf.Min(1f,resolved.magnitude/step.magnitude):Vector3.zero;
            if(Vector3.Dot(unit.inertiaVelocity,ground-from)<=0f)unit.inertiaVelocity=Vector3.zero;
            travelled+=(ground-from).magnitude;
        }
        Vector3 actual=ground-previous;
        if(unit.inertiaVelocity.sqrMagnitude<=.000001f)
        {
            unit.inertiaDeceleration=0f;unit.meleeApproachCoasting=false;
            unit.meleeArrivalSeedSpeed=0f;unit.meleeArrivalElapsed=0f;
        }
        unit.velocity=unit.inertiaVelocity;
        unit.lastGroundPosition=ground;
        unit.model.transform.position=ground+Vector3.up*unit.visualHeightOffset;
        if(unit.ring!=null)unit.ring.transform.position=ground+Vector3.up*.025f;
        unit.currentTile=NearestGridCoordinate(ground);
        ConsumeRangedRetreatDistance(unit,travelled);
        if(actual.sqrMagnitude>.000001f)UpdateFootstepDust(unit,actual);
        SetMoving(unit,unit.moving);
        RotateUnit(unit,IsCombatActive(unit.target)?
            unit.target.model.transform.position-unit.model.transform.position:actual,dt);
    }

    private void UpdateUnit(Unit unit,float dt)
    {
        if(InertiaBlocked(unit))ClearUnitInertia(unit);
        Vector3 previous=unit.model==null?Vector3.zero:Flat(unit.model.transform.position);
        UpdateUnitCore(unit,dt);
        if(IsPreparingSkill(unit))
        {AdvanceUnitInertia(unit,dt);AdvanceHitSlide(unit,dt);return;}
        if(IsCombatActive(unit))
        {
            float speed=CurrentMovementSpeed(unit);
            float moved=(Flat(unit.model.transform.position)-previous).magnitude;
            if(speed>0f&&moved<speed*dt)SeparateRestingUnit(unit,dt-moved/speed);
        }
        AdvanceUnitInertia(unit,dt);
        AdvanceHitSlide(unit,dt);
    }

    private void SeparateRestingUnit(Unit unit,float dt)
    {
        if(paused||dt<=0f||InertiaBlocked(unit)||HitSlideControlsMovement(unit)||
           unit.inertiaVelocity.sqrMagnitude>.000001f||
           unit.spiritConcertOwner!=null||unit.mantisBroodOwner!=null)return;
        Vector3 previous=Flat(unit.model.transform.position);
        float bestCost=BodyOverlapCost(unit,previous);
        if(bestCost<=0f)return;
        float penetration=BodyPenetrationCost(unit,previous);
        float step=Mathf.Min(CurrentMovementSpeed(unit)*dt,TileSize*.08f);
        if(step<=0f)return;
        Vector3 resolved=ResolveUnitMovement(unit,previous,previous);
        Vector3 ground=previous;
        Vector3 forward=IsCombatActive(unit.target)?Flat(unit.target.model.transform.position)-previous:
            Flat(unit.facingDirection);
        forward=forward.sqrMagnitude>.0001f?forward.normalized:Vector3.forward;
        Vector3 side=new Vector3(forward.z,0f,-forward.x);
        // Opposing contacts can cancel their pushes; try a small clear step to either side.
        bool escaped=false;
        for(int attempt=0;attempt<4&&!escaped;attempt++,step*=.5f)
        for(int i=-1;i<8;i++)
        {
            float angle=i*Mathf.PI*.25f;
            Vector3 candidate=i<0?resolved:previous+(forward*Mathf.Cos(angle)+side*Mathf.Sin(angle))*step;
            candidate=Vector3.MoveTowards(previous,ResolveFreePosition(candidate,CombatBodyRadius(unit)),step);
            foreach(var wall in rank3IceWalls)
                if(wall.team!=unit.team&&Rank3SegmentCrossesWall(previous,candidate,wall))
                {candidate=previous;break;}
            float cost=BodyOverlapCost(unit,candidate);
            if(cost>=bestCost||BodyPenetrationCost(unit,candidate)>penetration+.00000001f||
               IntroducesBodyOverlap(unit,previous,candidate,true)||
               !HasClearPathSegment(previous,candidate,CombatBodyRadius(unit)))continue;
            bestCost=cost;ground=candidate;escaped=true;
        }
        if((ground-previous).sqrMagnitude<=.000000000001f)return;
        // Pulls can stack bodies; release the overlap even while both actors keep attacking.
        unit.model.transform.position=ground+Vector3.up*unit.visualHeightOffset;
        unit.lastGroundPosition=ground;
        if(unit.ring!=null)unit.ring.transform.position=ground+Vector3.up*.025f;
        unit.currentTile=NearestGridCoordinate(ground);
    }

    private float BodyOverlapCost(Unit unit,Vector3 position)
    {
        float cost=0f;
        foreach(Unit other in units)
        {
            if(other==unit||!IsCombatActive(other)||other.monkeyNimbus!=null)continue;
            float clearance=BodyClearance(unit,other,UnitMovementClearance)-.0001f;
            float depth=Mathf.Max(0f,clearance-(position-Flat(other.model.transform.position)).magnitude);
            cost+=depth*depth;
        }
        return cost;
    }

    private float BodyPenetrationCost(Unit unit,Vector3 position)
    {
        float cost=0f;
        foreach(Unit other in units)
        {
            if(other==unit||!IsCombatActive(other)||other.monkeyNimbus!=null)continue;
            float depth=Mathf.Max(0f,CombatBodyRadius(unit)+CombatBodyRadius(other)-.0001f-
                (position-Flat(other.model.transform.position)).magnitude);
            cost+=depth*depth;
        }
        return cost;
    }

    private bool IntroducesBodyOverlap(Unit unit,Vector3 previous,Vector3 proposed,bool recovery)
    {
        Vector3 segment=proposed-previous;
        foreach(Unit other in units)
        {
            if(other==unit||!IsCombatActive(other)||other.monkeyNimbus!=null)continue;
            Vector3 center=Flat(other.model.transform.position);
            // Escape can use the spacing buffer so neighbours can yield, but not cross a body.
            float clearance=(recovery?CombatBodyRadius(unit)+CombatBodyRadius(other):
                BodyClearance(unit,other,UnitMovementClearance))-.0001f;
            float squared=clearance*clearance;
            if((previous-center).sqrMagnitude<squared)continue;
            float t=recovery&&segment.sqrMagnitude>.000001f?
                Mathf.Clamp01(Vector3.Dot(center-previous,segment)/segment.sqrMagnitude):1f;
            if((previous+segment*t-center).sqrMagnitude<squared)return true;
        }
        return false;
    }
}
