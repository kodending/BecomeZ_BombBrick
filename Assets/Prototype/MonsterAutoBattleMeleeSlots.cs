using System.Collections.Generic;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool TryMeleeSlotRoute(Unit unit,Unit target,IReadOnlyList<Vector3> positions,out List<Vector3> route)
    {
        bool previousQuery=BeginNavigationQuery();
        try
        {
            Vector3 origin=Flat(unit.model.transform.position);
            route=null;
            if(positions.Count==0)return false;
            int nearest=0;
            for(int i=1;i<positions.Count;i++)
                if((positions[i]-origin).sqrMagnitude<(positions[nearest]-origin).sqrMagnitude)nearest=i;
            if(MeleeSlotSegmentClear(unit,target,origin,positions[nearest]))
            {route=new List<Vector3>{positions[nearest]};return true;}
            if(positions.Count==1&&TryLocalMeleeBypass(unit,target,origin,positions[0],out route))return true;
            if(TryCrowdMeleeBypass(unit,target,origin,positions,out route))return true;
            Vector2Int start=NearestWalkableCoordinate(NearestGridCoordinate(origin));
            float bestRoute=float.PositiveInfinity;
            foreach(Vector3 position in positions)
            {
                if(Vector3.Distance(origin,position)>=bestRoute)continue;
                Vector2Int gridGoal=NearestGridCoordinate(position);
                var approaches=new List<Vector2Int>();
                List<Vector3> result=null;
                float bestDistance=float.PositiveInfinity;
                // An attack position can be reachable even when its nearest coarse grid cell is inside a body.
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                {
                    Vector2Int cell=gridGoal+new Vector2Int(x,z);
                    if(!IsNavigationCoordinate(cell))continue;
                    Vector3 point=NavigationWorld(unit,cell);
                    if(!MeleeSlotSegmentClear(unit,target,point,position))continue;
                    approaches.Add(cell);
                    if(!MeleeSlotSegmentClear(unit,target,origin,point))continue;
                    float distance=Vector3.Distance(origin,point)+Vector3.Distance(point,position);
                    if(distance>=bestDistance)continue;
                    bestDistance=distance;result=new List<Vector3>{point,position};
                }
                if(result==null&&approaches.Count>0)
                {
                    approaches.Sort((a,b)=>Vector3.Distance(origin,NavigationWorld(unit,a)).CompareTo(
                        Vector3.Distance(origin,NavigationWorld(unit,b))));
                    if(TryFindGridRoute(unit,start,approaches[0],out var candidate,out _,out bool reached,
                           target,approaches,position)&&reached)
                    {
                        candidate.Add(position);
                        result=SimplifyMeleeSlotRoute(unit,target,origin,candidate);
                    }
                    // An off-grid origin can invalidate the first segment; retain the other approach attempts.
                    if(result==null)foreach(Vector2Int goal in approaches)
                    {
                        if(!TryFindGridRoute(unit,start,goal,out candidate,out _,out reached,target,null,position)||!reached)continue;
                        candidate.Add(position);
                        result=SimplifyMeleeSlotRoute(unit,target,origin,candidate);
                        if(result!=null)break;
                    }
                }
                if(result==null)continue;
                float length=RouteDistance(origin,result);
                if(length>=bestRoute)continue;
                bestRoute=length;route=result;
            }
            return route!=null;
        }
        finally{navigationQueryActive=previousQuery;}
    }

    private bool TryLocalMeleeBypass(Unit unit,Unit target,Vector3 origin,Vector3 goal,out List<Vector3> route)
    {
        route=null;float best=float.PositiveInfinity;
        float bodyRadius=CombatBodyRadius(unit);
        Vector3 direct=goal-origin;
        if(direct.sqrMagnitude<.000001f)return false;
        foreach(Unit body in units)
        {
            if(body==unit||!IsCombatActive(body))continue;
            Vector3 center=Flat(body.model.transform.position);
            float clearance=BodyClearance(unit,body,UnitMovementClearance);
            float along=Mathf.Clamp01(Vector3.Dot(center-origin,direct)/direct.sqrMagnitude);
            if((origin+direct*along-center).sqrMagnitude>=clearance*clearance)continue;
            // Tight tangent arcs avoid jumping to a whole hex row to pass one body.
            const float step=Mathf.PI/8f;
            float radius=clearance/Mathf.Cos(step*.5f)+.015f;
            Vector3 a=origin-center,b=goal-center;
            if(a.magnitude<clearance||b.magnitude<clearance)continue;
            for(int side=-1;side<=1;side+=2)
            {
                // A touching endpoint reaches the padded arc radially; the full connector is checked below.
                float start=Mathf.Atan2(a.z,a.x)-side*Mathf.Acos(Mathf.Clamp01(radius/a.magnitude));
                float end=Mathf.Atan2(b.z,b.x)+side*Mathf.Acos(Mathf.Clamp01(radius/b.magnitude));
                float arc=Mathf.Repeat(-side*(end-start),Mathf.PI*2f)*-side;
                int count=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(arc)/step));
                var candidate=new List<Vector3>(count+2);
                for(int i=0;i<=count;i++)
                {
                    float angle=start+arc*i/count;
                    candidate.Add(center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius);
                }
                candidate.Add(goal);
                if(candidate.Exists(p=>p.x<combatBounds.min.x+bodyRadius||p.x>combatBounds.max.x-bodyRadius||
                    p.z<combatBounds.min.z+bodyRadius||p.z>combatBounds.max.z-bodyRadius))continue;
                List<Vector3> simplified=SimplifyMeleeSlotRoute(unit,target,origin,candidate);
                if(simplified==null)continue;
                float distance=RouteDistance(origin,simplified);
                if(distance>=best)continue;
                best=distance;route=simplified;
            }
        }
        return route!=null;
    }

    private bool TryCrowdMeleeBypass(Unit unit,Unit target,Vector3 origin,IReadOnlyList<Vector3> goals,out List<Vector3> route)
    {
        route=null;
        float bodyRadius=CombatBodyRadius(unit);
        var nodes=new List<Vector3>{origin};
        foreach(Vector3 goal in goals)
            if(goal.x>=combatBounds.min.x+bodyRadius&&goal.x<=combatBounds.max.x-bodyRadius&&
               goal.z>=combatBounds.min.z+bodyRadius&&goal.z<=combatBounds.max.z-bodyRadius)nodes.Add(goal);
        int goalCount=nodes.Count-1;
        if(goalCount==0||origin.x<combatBounds.min.x+bodyRadius||origin.x>combatBounds.max.x-bodyRadius||
           origin.z<combatBounds.min.z+bodyRadius||origin.z>combatBounds.max.z-bodyRadius)return false;
        const int sides=16;
        float step=Mathf.PI*2f/sides;
        foreach(Unit body in units)
        {
            if(body==unit||!IsCombatActive(body))continue;
            Vector3 center=Flat(body.model.transform.position);
            float radius=BodyClearance(unit,body,UnitMovementClearance)/Mathf.Cos(step*.5f)+.015f;
            for(int i=0;i<sides;i++)
                AddNode(center+new Vector3(Mathf.Cos(i*step),0f,Mathf.Sin(i*step))*radius);
            AddNode(center+(origin-center).normalized*radius);
            for(int i=1;i<=goalCount;i++)AddNode(center+(nodes[i]-center).normalized*radius);
        }
        void AddNode(Vector3 point)
        {
            if(point.x<combatBounds.min.x+bodyRadius||point.x>combatBounds.max.x-bodyRadius||
               point.z<combatBounds.min.z+bodyRadius||point.z>combatBounds.max.z-bodyRadius||
               Vector3.Distance(point,ResolveFreePosition(point,bodyRadius))>.001f)return;
            foreach(Vector3 existing in nodes)if((point-existing).sqrMagnitude<.000001f)return;
            nodes.Add(point);
        }
        var open=new List<int>{0};
        var closed=new bool[nodes.Count];
        var previous=new int[nodes.Count];
        var cost=new float[nodes.Count];
        var heuristic=new float[nodes.Count];
        for(int i=1;i<cost.Length;i++)cost[i]=float.PositiveInfinity;
        for(int i=0;i<nodes.Count;i++)
        {
            heuristic[i]=float.PositiveInfinity;
            for(int goal=1;goal<=goalCount;goal++)
                heuristic[i]=Mathf.Min(heuristic[i],Vector3.Distance(nodes[i],nodes[goal]));
        }
        while(open.Count>0)
        {
            int best=0;float score=float.PositiveInfinity;
            for(int i=0;i<open.Count;i++)
            {
                int node=open[i];
                float candidate=cost[node]+heuristic[node];
                if(candidate<score){best=i;score=candidate;}
            }
            int current=open[best];open.RemoveAt(best);
            if(current>0&&current<=goalCount)
            {
                route=new List<Vector3>();
                for(int node=current;node!=0;node=previous[node])route.Add(nodes[node]);
                route.Reverse();return true;
            }
            closed[current]=true;
            for(int next=1;next<nodes.Count;next++)
            {
                if(closed[next]||next==current)continue;
                float candidate=cost[current]+Vector3.Distance(nodes[current],nodes[next]);
                // Test only edges that improve an expanded A* node, with the same body/wall rules as movement.
                if(candidate>=cost[next]||!MeleeSlotSegmentClear(unit,target,nodes[current],nodes[next]))continue;
                cost[next]=candidate;previous[next]=current;
                if(!open.Contains(next))open.Add(next);
            }
        }
        return false;
    }

    private List<Vector3> SimplifyMeleeSlotRoute(Unit unit,Unit target,Vector3 origin,List<Vector3> route)
    {
        var simplified=new List<Vector3>();
        Vector3 previous=origin;
        for(int i=0;i<route.Count;)
        {
            int next=route.Count-1;
            while(next>=i&&!MeleeSlotSegmentClear(unit,target,previous,route[next]))next--;
            if(next<i)return null;
            previous=route[next];simplified.Add(previous);i=next+1;
        }
        return simplified;
    }

    private bool MeleeSlotSegmentClear(Unit unit,Unit target,Vector3 from,Vector3 to)
    {
        if(!HasClearNavigationSegment(unit,from,to))return false;
        Vector3 center=Flat(target.model.transform.position),segment=to-from;
        float t=segment.sqrMagnitude<.000001f?0f:Mathf.Clamp01(Vector3.Dot(center-from,segment)/segment.sqrMagnitude);
        float clearance=BodyClearance(unit,target,UnitMovementClearance);
        return (from+segment*t-center).sqrMagnitude>=clearance*clearance;
    }

}
