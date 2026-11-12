using System.Collections.Generic;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private static readonly float[] MeleeApproachAngles={0f,15f,-15f,30f,-30f,45f,-45f,60f,-60f,90f,-90f,120f,-120f,150f,-150f,180f};

    private bool CanKeepMeleeAttackPosition(Unit unit,Unit target)
    {
        return IsImmediatelyAttackable(unit,target)&&Vector3.Distance(
            Flat(unit.model.transform.position),Flat(target.model.transform.position))<=MeleeArrivalDistance(unit,target)+.001f;
    }

    private float MeleeContactDistance(Unit unit,Unit target)
    {
        return Mathf.Min(BodyClearance(unit,target,MeleeContactPadding),
            Mathf.Max(.05f,CombatApproachDistance(unit,target)-.05f));
    }

    private float MeleeArrivalDistance(Unit unit,Unit target)
    {
        float scale=(unit.combatBodyScale+target.combatBodyScale)*.5f;
        return Mathf.Min(CombatApproachDistance(unit,target),MeleeContactDistance(unit,target)+.6f*scale);
    }

    private Vector3 ClosestMeleeAttackPosition(Unit unit,Unit target)
    {
        Vector3 origin=Flat(unit.model.transform.position),center=Flat(target.model.transform.position);
        Vector3 away=origin-center;
        float radius=MeleeArrivalDistance(unit,target);
        if(away.sqrMagnitude<=radius*radius)return origin;
        return center+away.normalized*radius;
    }

    private bool MeleePursuitRouteNeedsRefresh(Unit unit)
    {
        Unit target=unit.target;
        Vector3 center=Flat(target.model.transform.position),end=unit.route[unit.route.Count-1];
        return Vector3.Distance(end,center)>MeleeArrivalDistance(unit,target)+.1f||
           !IsOpenAttackPosition(unit,target,end)||!HasClearPathSegment(end,center,.08f)||
           IsHostileIceWallBlocking(unit,end,center);
    }

    private void BeginMeleeArrivalInertia(Unit unit)
    {
        bool moving=unit.moving;
        StopForBasicAttack(unit);
        if(!moving||IsRangedUnit(unit)||!IsCombatTargetable(unit.target)||
           unit.inertiaVelocity.sqrMagnitude<=.000001f)return;
        Unit target=unit.target;
        unit.meleeApproachCoasting=true;
        Vector3 toward=Flat(target.model.transform.position-unit.model.transform.position);
        float available=Mathf.Max(0f,toward.magnitude-MeleeContactDistance(unit,target));
        float closing=Vector3.Dot(unit.inertiaVelocity,toward.normalized);
        if(closing<=.01f)return;
        float otherClosing=0f;
        if(target.target==unit)
        {
            if(target.meleeApproachCoasting&&target.meleeArrivalSeedSpeed>.001f)
            {
                float remaining=CombatMovementInertia.ApproachCoastRemainingDistance(
                    target.inertiaVelocity.magnitude,target.meleeArrivalSeedSpeed,
                    target.meleeArrivalElapsed,target.inertiaDeceleration);
                available=Mathf.Max(0f,available-remaining*Mathf.Max(0f,
                    Vector3.Dot(target.inertiaVelocity.normalized,-toward.normalized)));
            }
            else otherClosing=Mathf.Max(0f,Vector3.Dot(target.velocity,-toward.normalized));
        }
        // Preserve the partner's existing coast instead of treating its slow tail as stopped.
        float travel=available*unit.inertiaVelocity.magnitude/(closing+otherClosing);
        unit.inertiaDeceleration=Mathf.Max(unit.inertiaDeceleration,
            unit.inertiaVelocity.sqrMagnitude/(2f*Mathf.Max(.01f,travel)));
        unit.meleeArrivalSeedSpeed=unit.inertiaVelocity.magnitude;unit.meleeArrivalElapsed=0f;
    }

    private Vector3 LimitMeleeArrivalStep(Unit unit,Vector3 from,Vector3 to)
    {
        if(!unit.meleeApproachCoasting||!IsCombatTargetable(unit.target))return to;
        Vector3 center=Flat(unit.target.model.transform.position),offset=from-center,step=to-from;
        float radius=Mathf.Min(offset.magnitude,MeleeContactDistance(unit,unit.target));
        float squared=step.sqrMagnitude,dot=Vector3.Dot(offset,step);
        if(squared<.000000000001f||dot>=0f)return to;
        float discriminant=dot*dot-squared*(offset.sqrMagnitude-radius*radius);
        if(discriminant<=0f)return to;
        float entry=(-dot-Mathf.Sqrt(discriminant))/squared;
        return entry>=1f?to:from+step*Mathf.Clamp01(entry);
    }

    private bool BuildClosestMeleeRoute(Unit unit,Unit target)
    {
        ClearRoute(unit);unit.repathTimer=.2f;
        if(!IsCombatTargetable(target))return false;
        Vector3 origin=Flat(unit.model.transform.position),center=Flat(target.model.transform.position);
        Vector3 away=origin-center;
        if(away.sqrMagnitude<.0001f)away=unit.team==Team.Player?Vector3.left:Vector3.right;
        away.Normalize();float radius=MeleeArrivalDistance(unit,target);
        var positions=new List<Vector3>();
        foreach(float angle in MeleeApproachAngles)
        {
            Vector3 point=center+Quaternion.AngleAxis(angle,Vector3.up)*away*radius;
            if(IsOpenAttackPosition(unit,target,point)&&HasClearPathSegment(point,center,.08f)&&
               !IsHostileIceWallBlocking(unit,point,center))positions.Add(point);
        }
        TryMeleeSlotRoute(unit,target,positions,out List<Vector3> route);
        // An incomplete forward step must never replace a reachable route around the crowd.
        if(route==null)route=BuildMeleeApproachStep(unit,target,origin);
        if(route==null)return false;
        unit.route.AddRange(route);unit.detourTimer=3f;unit.detourTarget=target;
        return true;
    }

    private List<Vector3> BuildMeleeApproachStep(Unit unit,Unit target,Vector3 origin)
    {
        Vector3 toward=Flat(target.model.transform.position)-origin;
        float reach=Mathf.Min(toward.magnitude-MeleeArrivalDistance(unit,target),TileSize*2f);
        if(reach<=.1f)return null;
        Vector3 bestPoint=origin;float bestGap=toward.magnitude-.1f;
        // A busy attack ring must not prevent the rear from approaching its nearest clear edge.
        foreach(float angle in MeleeApproachAngles)
        {
            if(Mathf.Abs(angle)>60f)continue;
            Vector3 step=Quaternion.AngleAxis(angle,Vector3.up)*toward.normalized*reach;
            float low=0f,high=1f;
            if(MeleeSlotSegmentClear(unit,target,origin,origin+step))low=1f;
            else for(int i=0;i<12;i++)
            {
                float middle=(low+high)*.5f;
                if(MeleeSlotSegmentClear(unit,target,origin,origin+step*middle))low=middle;
                else high=middle;
            }
            Vector3 point=origin+step*Mathf.Max(0f,low-(low<1f?.1f/reach:0f));
            float gap=Vector3.Distance(point,Flat(target.model.transform.position));
            if(gap>=bestGap||Vector3.Distance(point,ResolveFreePosition(point,CombatBodyRadius(unit)))>.001f||
               !IsOpenAttackPosition(unit,target,point))continue;
            bestGap=gap;bestPoint=point;
        }
        return bestPoint==origin?null:new List<Vector3>{bestPoint};
    }
}
