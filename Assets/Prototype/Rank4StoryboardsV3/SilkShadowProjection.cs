using UnityEngine;

// Projection math only. Each film keeps its own actions, camera, timing and physical prop construction.
internal static class SilkShadowProjection
{
    public static Vector3 Point(Transform root,Camera view,Vector2 topLeft,float depth)
        =>root.InverseTransformPoint(view.ViewportToWorldPoint(new Vector3(topLeft.x,1f-topLeft.y,depth)));
    public static float Height(Camera view,float fraction,float depth)
        =>fraction*(view.orthographic?view.orthographicSize*2f:2f*depth*Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f));
    public static Vector3 Anchor(float time,float[] times,Vector3[] anchors)
    {
        if(time<=times[0])return anchors[0];
        for(int i=1;i<Mathf.Min(times.Length,anchors.Length);i++)if(time<=times[i])
            return Vector3.Lerp(anchors[i-1],anchors[i],Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(times[i-1],times[i],time)));
        return anchors[anchors.Length-1];
    }
}
