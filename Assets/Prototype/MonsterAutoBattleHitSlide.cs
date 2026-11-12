using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private static bool HitSlideBlocked(Unit unit)=>InertiaBlocked(unit)||unit.hp<=0f||
        unit.rank3MycokingSummon||unit.spiritConcertOwner!=null||unit.mantisBroodOwner!=null||
        unit.combatStatuses.ControlImmune||unit.combatStatuses.DebuffImmune||
        unit.rank3ForceImmuneTimer>0f||MountainColossusActive(unit)||MantisReaperFlying(unit)||
        TyrannoExtinctionCharging(unit)||TyrannoMarchProtected(unit)||TyrannoMawProtected(unit)||
        unit.ninjaCrossActive||unit.griffinHunt!=null||unit.mammothCharge!=null||
        unit.mammothExpulsion!=null;

    private void BeginHitSlide(Unit source,Unit target,bool critical,bool skill)
    {
        if(!skill||target==null||IsWyvernFamily(source))return;
        if(HitSlideBlocked(target)) {target.hitSlideVelocity=Vector3.zero;return;}
        if(paused||source==null||source==target||source.model==null||source.team==target.team)return;
        if(target.hitSlideGeneration!=combatGeneration||target.hitSlidePositionRevision!=target.positionRevision)
        {target.hitSlideVelocity=Vector3.zero;target.hitSlideNextAt=0f;}
        if(battleElapsed<target.hitSlideNextAt)return;
        Vector3 direction=Flat(target.model.transform.position-source.model.transform.position);
        if(direction.sqrMagnitude<.000001f)direction=Flat(source.facingDirection);
        if(direction.sqrMagnitude<.000001f)return;
        float speed=critical?3.3f:3f;
        // Impact replaces carried locomotion momentum without interrupting the current action.
        target.inertiaVelocity=Vector3.zero;
        target.hitSlideVelocity=Vector3.ClampMagnitude(target.hitSlideVelocity+direction.normalized*speed,3.3f);
        target.hitSlideNextAt=battleElapsed+.12f;
        target.hitSlideGeneration=combatGeneration;
        target.hitSlidePositionRevision=target.positionRevision;
    }

    private bool HitSlideControlsMovement(Unit unit)=>unit.hitSlideVelocity.sqrMagnitude>.000001f&&
        unit.hitSlideGeneration==combatGeneration&&unit.hitSlidePositionRevision==unit.positionRevision&&
        !HitSlideBlocked(unit);

    private bool YieldVoluntaryMovementToHitSlide(Unit unit,float dt)
    {
        if(!HitSlideControlsMovement(unit))return false;
        SetMoving(unit,true);
        RotateUnit(unit,IsCombatActive(unit.target)?unit.target.model.transform.position-
            unit.model.transform.position:unit.facingDirection,dt);
        return true;
    }

    private void AdvanceHitSlide(Unit unit,float dt)
    {
        if(unit.hitSlideVelocity.sqrMagnitude<=.000001f){unit.hitSlideVelocity=Vector3.zero;return;}
        if(HitSlideBlocked(unit)||unit.hitSlideGeneration!=combatGeneration||
           unit.hitSlidePositionRevision!=unit.positionRevision)
        {unit.hitSlideVelocity=Vector3.zero;return;}
        if(paused||dt<=0f)return;
        Vector3 previous=Flat(unit.model.transform.position),ground=previous;
        float overlapCost=BodyPenetrationCost(unit,ground);
        float speed=unit.hitSlideVelocity.magnitude;
        dt=Mathf.Min(dt,speed/CombatMovementInertia.CoastDeceleration);
        int steps=Mathf.Max(1,Mathf.CeilToInt(speed*dt/
            Mathf.Max(.025f,Mathf.Min(.08f,CombatBodyRadius(unit)*.5f))));
        for(int i=0;i<steps&&unit.hitSlideVelocity.sqrMagnitude>.000001f;i++)
        {
            Vector3 step=CombatMovementInertia.Coast(unit.hitSlideVelocity,dt/steps,out Vector3 next);
            Vector3 from=ground,candidate=ResolveBodyMovement(unit,from,from+step,false);
            candidate=Vector3.MoveTowards(from,candidate,step.magnitude);
            if(Vector3.Dot(candidate-from,step)<=0f)candidate=from;
            // A rejected hit step must not change the actor's AI velocity or action.
            foreach(var wall in rank3IceWalls)
                if(wall.team!=unit.team&&Rank3SegmentCrossesWall(from,candidate,wall))
                {candidate=from;break;}
            float nextOverlapCost=BodyPenetrationCost(unit,candidate);
            // Close melee contacts may escape, but a hit must never drive them farther into a body.
            if(nextOverlapCost>overlapCost+.00000001f||IntroducesBodyOverlap(unit,from,candidate,true)||
               !HasClearPathSegment(from,candidate,CombatBodyRadius(unit)))candidate=from;
            else overlapCost=nextOverlapCost;
            ground=candidate;
            Vector3 resolved=CombatMovementInertia.AfterCollision(step,ground-from,1f);
            unit.hitSlideVelocity=step.sqrMagnitude>.000001f?resolved.normalized*next.magnitude*
                Mathf.Min(1f,resolved.magnitude/step.magnitude):Vector3.zero;
        }
        if((ground-previous).sqrMagnitude<=.000000000001f)return;
        unit.model.transform.position=ground+Vector3.up*unit.model.transform.position.y;
        unit.lastGroundPosition=ground;
        if(unit.ring!=null)unit.ring.transform.position=ground+Vector3.up*.025f;
        unit.currentTile=NearestGridCoordinate(ground);
    }
}
