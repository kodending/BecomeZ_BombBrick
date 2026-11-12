using System.Collections.Generic;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private struct ProjectedMeleeBody
    {
        public Unit unit;
        public Rect bounds;
    }

    private readonly List<ProjectedMeleeBody> meleeReadabilityBodies=new List<ProjectedMeleeBody>(16);

    private void CacheMeleeReadabilityBody(Unit unit)
    {
        unit.meleeBodyShape=new Rect();
        if(unit.spriteAnimation==null)return;
        Sprite idle=unit.spriteAnimation.GetIdleFrame(0f);
        if(idle==null)return;
        Bounds body=PixelMantisReaper.VisibleBody(idle);
        Transform parent=unit.spriteAnimation.transform.parent;
        Vector3 scale=(parent==null?Vector3.one:parent.lossyScale)*unit.spriteAnimation.IdleScale;
        // A symmetric idle footprint stays stable through facing flips, recoil and skill poses.
        float halfWidth=Mathf.Max(Mathf.Abs(body.min.x),Mathf.Abs(body.max.x))*Mathf.Abs(scale.x);
        unit.meleeBodyShape=Rect.MinMaxRect(-halfWidth,body.min.y*Mathf.Abs(scale.y),
            halfWidth,body.max.y*Mathf.Abs(scale.y));
    }

    private void PrepareMeleeReadability(Unit target)
    {
        meleeReadabilityBodies.Clear();
        if(mainCamera==null||!mainCamera.isActiveAndEnabled)return;
        for(int i=0;i<units.Count;i++)
        {
            Unit other=units[i];
            // Moving groups will vacate their current positions; only settled bodies are stable blockers.
            if(!IsCombatActive(other)||other.spiritConcertOwner!=null||
               other!=target&&(other.moving||other.inertiaVelocity.sqrMagnitude>.000001f))continue;
            if(TryProjectMeleeBody(other,Flat(other.model.transform.position),out Rect bounds))
                meleeReadabilityBodies.Add(new ProjectedMeleeBody{unit=other,bounds=bounds});
        }
    }

    private bool TryProjectMeleeBody(Unit unit,Vector3 ground,out Rect bounds)
    {
        bounds=new Rect();Rect shape=unit.meleeBodyShape;
        if(mainCamera==null||!mainCamera.isActiveAndEnabled||shape.width<=0f||shape.height<=0f)return false;
        Vector3 origin=ground+Vector3.up*unit.visualHeightOffset;
        Vector3 right=mainCamera.transform.right,up=mainCamera.transform.up;
        float minX=float.PositiveInfinity,minY=float.PositiveInfinity;
        float maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            Vector3 point=mainCamera.WorldToViewportPoint(origin+
                right*(x==0?shape.xMin:shape.xMax)+up*(y==0?shape.yMin:shape.yMax));
            if(point.z<=mainCamera.nearClipPlane||float.IsNaN(point.x)||float.IsInfinity(point.x)||
               float.IsNaN(point.y)||float.IsInfinity(point.y))return false;
            minX=Mathf.Min(minX,point.x);minY=Mathf.Min(minY,point.y);
            maxX=Mathf.Max(maxX,point.x);maxY=Mathf.Max(maxY,point.y);
        }
        bounds=Rect.MinMaxRect(minX,minY,maxX,maxY);
        return bounds.width>0f&&bounds.height>0f;
    }

    private float MeleeReadabilityPenalty(Unit unit,Vector3 position)
    {
        if(meleeReadabilityBodies.Count==0||!TryProjectMeleeBody(unit,position,out Rect body))return 0f;
        float overlap=0f;
        for(int i=0;i<meleeReadabilityBodies.Count;i++)
        {
            ProjectedMeleeBody other=meleeReadabilityBodies[i];
            if(other.unit==unit)continue;
            float width=Mathf.Max(0f,Mathf.Min(body.xMax,other.bounds.xMax)-Mathf.Max(body.xMin,other.bounds.xMin));
            float height=Mathf.Max(0f,Mathf.Min(body.yMax,other.bounds.yMax)-Mathf.Max(body.yMin,other.bounds.yMin));
            float area=Mathf.Min(body.width*body.height,other.bounds.width*other.bounds.height);
            if(area>0f)overlap+=width*height/area;
        }
        // Keep visual preference below the existing slot-change cost.
        return Mathf.Clamp01(overlap)*TileSize*.6f;
    }
}
