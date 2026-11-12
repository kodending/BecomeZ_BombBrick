using UnityEngine;

// Projection/curve arithmetic only; each production owns its space, poses and timing.
public abstract class MoltColonyV3Stage:Rank4CinematicStage
{
    protected MoltColonyV3Stage(Transform parent,string name,Color clear,int width=640,int height=360):base(parent,name,clear,width,height){}

    protected Vector3 ScreenPoint(float u,float v,float depth)
    {
        float half=View.orthographic?View.orthographicSize:depth*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f);
        return View.transform.localPosition+CameraFacing*new Vector3((u-.5f)*half*2f*16f/9f,(.5f-v)*half*2f,depth);
    }

    protected float ScreenHeight(float fraction,float depth)=>fraction*(View.orthographic?View.orthographicSize*2f:
        depth*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f)*2f);

    protected Vector3 FloorPoint(float u,float v,float floor=0f)
    {
        Vector3 origin=View.transform.localPosition,direction=(ScreenPoint(u,v,1f)-origin).normalized;
        float distance=Mathf.Abs(direction.y)<.0001f?12f:(floor-origin.y)/direction.y;
        return origin+direction*Mathf.Max(.1f,distance);
    }

    protected void GroundBodyAt(SpriteRenderer renderer,Sprite sprite,Vector3 anchor,int order,Color color,bool flip=false,float floor=0f)
    {
        Vector3 foot=FloorPoint(anchor.x,anchor.y+anchor.z*.5f,floor);
        float depth=Vector3.Dot(foot-View.transform.localPosition,CameraFacing*Vector3.forward);
        PaintWorld(renderer,sprite,foot,ScreenHeight(anchor.z,depth),color,order,CameraFacing,flip);
    }

    protected void BodyAt(SpriteRenderer renderer,Sprite sprite,Vector3 anchor,float depth,int order,Color color,bool flip=false,float roll=0f)
    {
        PaintWorld(renderer,sprite,ScreenPoint(anchor.x,anchor.y+anchor.z*.5f,depth),ScreenHeight(anchor.z,depth),
            color,order,CameraFacing*Quaternion.Euler(0f,0f,roll),flip);
    }

    protected void ArtAt(SpriteRenderer renderer,Sprite sprite,Vector2 center,Vector2 screenSize,float depth,int order,Color color,float roll=0f)
    {
        float h=ScreenHeight(1f,depth);
        PaintArtWorld(renderer,sprite,ScreenPoint(center.x,center.y,depth),new Vector2(screenSize.x*h*16f/9f,screenSize.y*h),
            color,order,CameraFacing*Quaternion.Euler(0f,0f,roll));
    }

    protected static float Curve(float t,float[] times,float[] values)
    {
        if(t<=times[0])return values[0];
        for(int i=1;i<times.Length;i++)if(t<=times[i])return Mathf.Lerp(values[i-1],values[i],Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(times[i-1],times[i],t)));
        return values[values.Length-1];
    }

    protected static Vector3 Curve(float t,float[] times,Vector3[] values)
    {
        if(t<=times[0])return values[0];
        for(int i=1;i<times.Length;i++)if(t<=times[i])return Vector3.Lerp(values[i-1],values[i],Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(times[i-1],times[i],t)));
        return values[values.Length-1];
    }

    protected static int SpeciesRow(string species,string second,string third,string fourth)
        =>species==second?1:species==third?2:species==fourth?3:0;

    protected void ReturnCamera()=>CameraPose(Vector3.back*20f,Vector3.zero,25.36f);
}
