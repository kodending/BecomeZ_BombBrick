using System.Collections.Generic;
using UnityEngine;

// A physical horn lift launches each victim above its own footprint; the host owns the landing damage.
public sealed class BeetleOverturnRank4Stage:Rank4CinematicStage
{
    private const string ArtRoot="CombatCinematics/BeetleImperialHornV1/";
    private const float SourceHeight=1.55f,FilmLaunch=1.30f,FilmApex=2.12f,FilmFall=2.52f,FilmLanding=2.90f;
    private const int WaveColumns=24,WaveRows=14;
    public const float FieldReturnTime=3.70f;
    public static readonly float[] BoardTimes={.28f,.95f,1.62f,2.18f,2.96f,3.93f};
    public override string Family=>"beetle";
    public override float Duration=>4.20f;
    private readonly Sprite[] bodyFrames,hornFrames,earthFrames,impactFrames;
    private readonly SpriteRenderer lead,horn,sourceShadow;
    private readonly List<SpriteRenderer> victims=new List<SpriteRenderer>(),victimShadows=new List<SpriteRenderer>(),
        witnesses=new List<SpriteRenderer>(),impacts=new List<SpriteRenderer>();
    private readonly SpriteRenderer[] earth=new SpriteRenderer[10];
    private readonly FrogCatchCinematicEnvironment scenery;
    private readonly MeshRenderer wave;
    private readonly Mesh waveMesh;
    private readonly Vector3[] waveVertices=new Vector3[(WaveColumns+1)*(WaveRows+1)];
    private readonly Vector2[] waveUv=new Vector2[(WaveColumns+1)*(WaveRows+1)];
    private Rank4StageContext sceneryContext;
    private readonly Sprite groundShadow;
    private float Side=>Context.enemy?-1f:1f;

    public BeetleOverturnRank4Stage(Transform parent):base(parent,"Beetle imperial horn",Color.clear,1280,720)
    {
        Root.position=new Vector3(1280f,320f,1280f);
        bodyFrames=LoadAtlas(ArtRoot+"Body40",4,10);
        hornFrames=LoadAtlas(ArtRoot+"Horn8",4,2);
        earthFrames=LoadAtlas(ArtRoot+"Earth4",4);
        impactFrames=LoadAtlas(ArtRoot+"Impact8",4,2);
        lead=MakeSprite("Lead imperial horn beetle",70);
        horn=MakeSprite("Continuous forked imperial horn",30);
        sourceShadow=MakeSprite("Planted imperial horn contact",0);
        groundShadow=PixelMonkeyMortar.ShadowSprite();
        for(int i=0;i<earth.Length;i++)earth[i]=MakeSprite("Layered rising turf "+i,20+i);
        scenery=new FrogCatchCinematicEnvironment(Root);
        int[] triangles=new int[WaveColumns*WaveRows*6];
        Color[] colors=new Color[waveVertices.Length];
        for(int row=0;row<=WaveRows;row++)for(int column=0;column<=WaveColumns;column++)
        {
            int index=row*(WaveColumns+1)+column;colors[index]=Color.white;
            if(row==WaveRows||column==WaveColumns)continue;
            int offset=(row*WaveColumns+column)*6;
            triangles[offset]=index;triangles[offset+1]=index+WaveColumns+1;triangles[offset+2]=index+1;
            triangles[offset+3]=index+1;triangles[offset+4]=index+WaveColumns+1;triangles[offset+5]=index+WaveColumns+2;
        }
        wave=Geometry("Flexible local battlefield wave",waveVertices,triangles,colors,waveUv);
        waveMesh=wave.GetComponent<MeshFilter>().sharedMesh;wave.enabled=false;
    }

    protected override void Animate(float time,float dt)
    {
        View.ResetProjectionMatrix();ActionTime=OriginalTime(time);SoundBeat=-1;
        Shot=time<.60f?0:time<FilmLaunch?1:time<1.90f?2:time<FilmFall?3:time<3.18f?4:5;
        Opacity=1f-Ease(FieldReturnTime,Duration,time);wave.enabled=false;
        if(time>=FieldReturnTime)
        {
            scenery.Hide();View.backgroundColor=Color.clear;
            CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
            PaintReturn(lead,70);return;
        }
        if(time<.10f)
        {
            scenery.Hide();View.backgroundColor=Color.clear;
            CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
            Paint(lead,Context.returnSprite??Context.source.sprite,Context.entryPosition,
                Mathf.Max(.3f,Context.entryHeight),Context.source.color,70,Context.source.flipX);return;
        }
        EnsureScenery();
        float returnFade=Ease(3.30f,FieldReturnTime,time);
        scenery.SetOpacity((1f-returnFade)*Ease(.10f,.24f,time));
        View.backgroundColor=time<.24f||time>=3.30f?Color.clear:scenery.SkyColor;
        if(time<3.18f)WorldCamera(time);else PullbackCamera(time);
        float actors=1f-Ease(3.30f,FieldReturnTime,time);
        float companions=actors*Mathf.Clamp01(1f-Ease(.36f,.60f,time)+Ease(FilmLaunch,1.52f,time));
        DrawWitnesses(companions);
        int pose=BodyPose(time);
        PaintFixed(lead,bodyFrames[SpeciesRow()*8+pose],bodyFrames[SpeciesRow()*8+7],Vector3.zero,
            SourceHeight,WithAlpha(Context.source.color,actors),70,Context.source.flipX);
        ApplySourceImpulse(ActionTime);
        Shadow(sourceShadow,Vector3.zero,new Vector2(.90f,.50f),.40f*actors);
        if(time>=3.63f&&Context.returnSprite!=null)
            PaintWorld(lead,Context.returnSprite,Vector3.zero,SourceHeight,WithAlpha(Context.source.color,actors),70,CameraFacing,Context.source.flipX);
        DrawHorn(time,actors);
        GroundWave(time);
        DrawEarth(time,actors);
        DrawVictims(time,companions);
        LandingDebris(time,actors);
    }

    private int SpeciesRow()=>Context.source.shiny&&Context.source.species=="jangpung"?4:
        Context.source.species=="noepung"?1:Context.source.species=="atlagon"?2:Context.source.species=="solarbeetle"?3:0;
    private static int BodyPose(float time)=>BeetleOverturnRules.BodyFrame(OriginalTime(time));
    private static int Step(float time,float start,float end,int count)=>
        Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(start,end,time)*count),0,count-1);
    private static Color WithAlpha(Color color,float alpha){color.a*=Mathf.Clamp01(alpha);return color;}
    private Vector3 Mirrored(float x,float y,float z)=>new Vector3(x*Side,y,z);
    private Vector3 Footprint(FrogCatchCinematicStage.Actor actor)
    {Vector2 point=actor.ground-Context.source.ground;return new Vector3(point.x*9f,0f,point.y*4f);}

    private Vector3 AirborneOffset(float action,int index)
    {
        if(index<Context.beetleLiftAllowed.Length&&!Context.beetleLiftAllowed[index])return Vector3.zero;
        Vector3 offset=BeetleOverturnRules.AirOffset(action,index)*1.35f;offset.x*=Side;return offset;
    }

    private void ApplySourceImpulse(float action)
    {
        Vector3 scale=BeetleOverturnRules.SourceScale(action),pose=BeetleOverturnRules.SourcePose(action)*SourceHeight;
        pose.x*=Side;
        Quaternion rotation=lead.transform.localRotation;
        Vector3 pivotOffset=Quaternion.Inverse(rotation)*lead.transform.localPosition;
        lead.transform.localPosition=rotation*(Vector3.Scale(pivotOffset,scale)+pose);
        lead.transform.localScale=Vector3.Scale(lead.transform.localScale,scale);
    }

    private Vector3 GroupCameraOffset()
    {
        if(Context.targets.Length==0)return Vector3.zero;
        Vector3 center=Vector3.zero;
        foreach(var actor in Context.targets)center+=Footprint(actor);
        Vector3 offset=center/Context.targets.Length-Mirrored(4.3f,0f,.5f);
        // Keep the authored framing steady for small formation drift; follow close or surrounding groups.
        return offset*Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.75f,2f,offset.magnitude));
    }

    private Bounds TargetGroundBounds()
    {
        if(Context.targets.Length==0)return new Bounds(Mirrored(4.3f,0f,.5f),new Vector3(7.6f,0f,4.7f));
        var bounds=new Bounds(Footprint(Context.targets[0]),Vector3.zero);
        foreach(var actor in Context.targets)bounds.Encapsulate(Footprint(actor));
        bounds.Expand(new Vector3(.90f,0f,.90f));return bounds;
    }

    private void PaintFixed(SpriteRenderer renderer,Sprite sprite,Sprite anchor,Vector3 feet,float height,Color color,int order,bool flip)
    {
        PaintWorld(renderer,anchor,feet,height,color,order,CameraFacing,flip);renderer.sprite=sprite;
        renderer.transform.localScale*=sprite.pixelsPerUnit/anchor.pixelsPerUnit;
    }

    private void Shadow(SpriteRenderer renderer,Vector3 feet,Vector2 size,float alpha)
    {PaintArtWorld(renderer,groundShadow,feet+Vector3.up*.008f,size,new Color(.018f,.026f,.035f,alpha),0,Quaternion.Euler(90f,0f,0f));}

    private void DrawWitnesses(float alpha)
    {
        while(witnesses.Count<Context.allies.Length)witnesses.Add(MakeSprite("Actual allied imperial horn witness "+witnesses.Count,10));
        for(int i=0;i<Context.allies.Length;i++)
        {
            var actor=Context.allies[i];
            PaintWorld(witnesses[i],actor.sprite,Footprint(actor),1.4f*actor.size,WithAlpha(actor.color,alpha),10-i,CameraFacing,actor.flipX);
        }
    }

    private void DrawHorn(float time,float alpha)
    {
        if(time<.60f||time>=3.30f)return;
        int frame=time<.82f?0:time<1.10f?1:time<FilmLaunch?2:time<1.75f?3:
            time<FilmFall?4:time<2.78f?5:time<3.05f?6:7;
        Sprite sprite=hornFrames[frame];
        float fade=Ease(.60f,.82f,time)*(1f-Ease(2.62f,3.30f,time));
        float rotation=BeetleOverturnRules.SourcePose(OriginalTime(time)).y*180f*Side;
        Quaternion orientation=CameraFacing*Quaternion.Euler(0f,0f,rotation);
        Bounds body=PixelMantisReaper.VisibleBody(lead.sprite);
        float headX=body.center.x+body.size.x*.08f;
        Vector3 localJoint=new Vector3(lead.flipX?-headX:headX,body.min.y+body.size.y*.52f,0f);
        Vector3 joint=lead.transform.localPosition+lead.transform.localRotation*Vector3.Scale(localJoint,lead.transform.localScale);
        float scale=7.5f/sprite.bounds.size.y;
        Vector3 localBase=new Vector3((.12f-.5f)*sprite.bounds.size.x,(.16f-.5f)*sprite.bounds.size.y,0f);
        if(Context.enemy)localBase.x=-localBase.x;
        horn.enabled=fade*alpha>0f;horn.sprite=sprite;horn.color=new Color(1f,1f,1f,fade*alpha);
        horn.flipX=Context.enemy;horn.sortingOrder=30;horn.transform.localScale=Vector3.one*scale;
        horn.transform.localRotation=orientation;horn.transform.localPosition=joint-orientation*(localBase*scale);
        float strain=Ease(.62f,1.30f,time)*(1f-Ease(1.30f,1.55f,time));
        for(int i=0;i<24&&strain>0f;i++)
        {
            float phase=Mathf.Repeat(time*.7f+Noise(i*31),1f);
            Vector3 from=Mirrored(.5f+Noise(i*7)*2.4f,.03f,Noise(i*17)*1.1f-.55f);
            Vector3 point=Vector3.Lerp(from,joint,phase*phase);
            Pixel(point,.025f+Noise(i*43)*.022f,new Color(1f,.74f,.31f,strain*Mathf.Sin(phase*Mathf.PI)*.7f));
        }
    }

    private void DrawEarth(float time,float alpha)
    {
        if(time<FilmLaunch||time>=3.18f||Context.targets.Length==0)return;
        float age=time-FilmLaunch,progress=Ease(FilmLaunch,1.90f,time);
        float fade=1f-Ease(2.35f,3.18f,time);
        for(int i=0;i<earth.Length;i++)
        {
            float depth=Noise(i*17);
            Vector3 foot=Footprint(Context.targets[i%Context.targets.Length]);
            foot+=new Vector3((Noise(i*23)-.5f)*.25f,
                Mathf.Sin(Mathf.Clamp01(age/(.55f+depth*.2f))*Mathf.PI)*(.25f+depth*.7f),(Noise(i*43)-.5f)*.25f);
            foot.x+=Side*progress*(.08f+depth*.10f);
            Sprite sprite=earthFrames[Step(time,FilmLaunch,2.30f,4)];
            PaintFixed(earth[i],sprite,earthFrames[3],foot,.65f+depth*.75f,new Color(1f,1f,1f,fade*alpha),20+i,Context.enemy);
        }
    }

    private void GroundWave(float time)
    {
        if(time<FilmLaunch||time>=2.85f)return;
        float progress=Ease(FilmLaunch,1.92f,time),fade=1f-Ease(2.15f,2.85f,time);
        Bounds region=TargetGroundBounds();
        float crest=Context.enemy?Mathf.Lerp(region.max.x,region.min.x,progress):Mathf.Lerp(region.min.x,region.max.x,progress);
        Vector3 origin=new Vector3(-Mathf.Clamp(Context.source.ground.x,-1f,1f)*3f,0f,-Mathf.Clamp(Context.source.ground.y,-1f,1f)*2f);
        for(int row=0;row<=WaveRows;row++)for(int column=0;column<=WaveColumns;column++)
        {
            int index=row*(WaveColumns+1)+column;
            float x=Mathf.Lerp(region.min.x,region.max.x,column/(float)WaveColumns),z=Mathf.Lerp(region.min.z,region.max.z,row/(float)WaveRows);
            float edge=Mathf.Sin(row/(float)WaveRows*Mathf.PI)*Mathf.Sin(column/(float)WaveColumns*Mathf.PI);
            float distance=x-crest;
            float planted=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.5f,1.10f,new Vector2(x,z).magnitude));
            float lift=Mathf.Exp(-distance*distance*.8f)*(1.2f+.35f*Mathf.Sin(z*2.2f))*edge*fade*planted;
            Vector3 point=new Vector3(x,.014f+lift,z);waveVertices[index]=point;
            waveUv[index]=new Vector2((point.x-origin.x)/64f+2.5f,(point.z-origin.z)/64f+2.5f);
        }
        waveMesh.vertices=waveVertices;waveMesh.uv=waveUv;waveMesh.RecalculateBounds();wave.enabled=true;
    }

    private void DrawVictims(float time,float alpha)
    {
        while(victims.Count<Context.targets.Length)
        {
            int index=victims.Count;
            victims.Add(MakeSprite("Spinning imperial horn victim "+index,50+index));
            victimShadows.Add(MakeSprite("Original imperial landing footprint "+index,0));
            impacts.Add(MakeSprite("Crushing landing dust "+index,62+index));
        }
        float action=OriginalTime(time);
        for(int i=0;i<Context.targets.Length;i++)
        {
            var actor=Context.targets[i];Vector3 feet=Footprint(actor);float seed=Noise(i*37+7);
            bool lifted=i>=Context.beetleLiftAllowed.Length||Context.beetleLiftAllowed[i];
            float height=lifted?BeetleOverturnRules.Height(action,i):0f;
            float roll=lifted?BeetleOverturnRules.Roll(action,i)*(i%2==0?1f:-1f)*Side:0f;
            Vector3 airborne=AirborneOffset(action,i);
            Sprite sprite=Frame(actor.frames,Mathf.Repeat((time-FilmLaunch)*2.8f+seed,1f))??actor.sprite;
            if(time<FilmLaunch||time>=FilmLanding)sprite=actor.sprite;
            float bodyHeight=1.4f*actor.size;
            Color hit=Color.Lerp(actor.color,new Color(1f,.36f,.23f,actor.color.a),
                time>=FilmLanding?1f-Ease(FilmLanding,FilmLanding+.14f,time):0f);
            PaintWorld(victims[i],sprite,feet+airborne,bodyHeight,WithAlpha(hit,alpha),50+i,CameraFacing,actor.flipX);
            if(airborne.y>.001f)RotateAtCenter(victims[i],roll);
            Shadow(victimShadows[i],feet,new Vector2(.9f,.52f)*(1f-height*.20f),(.32f+height*.18f)*alpha);
            if(time>=FilmLanding&&time<3.70f)
            {
                float age=time-FilmLanding;
                int frame=age<.025f?0:age<.05f?1:age<.15f?2:age<.26f?3:age<.38f?4:age<.51f?5:age<.66f?6:7;
                float cloud=1f-Ease(3.24f,3.70f,time);
                PaintFixed(impacts[i],impactFrames[frame],impactFrames[2],feet,3.3f*(.90f+seed*.2f),
                    new Color(1f,1f,1f,alpha*cloud*.88f),40+i,Context.enemy);
            }
        }
    }

    private static void RotateAtCenter(SpriteRenderer renderer,float angle)
    {
        Vector3 center=PixelMantisReaper.VisibleBody(renderer.sprite).center;
        if(renderer.flipX)center.x=-center.x;
        Vector3 pivot=renderer.transform.TransformPoint(center);
        renderer.transform.RotateAround(pivot,renderer.transform.forward,angle);
    }

    private void LandingDebris(float time,float alpha)
    {
        float age=time-FilmLanding;if(age<0f||age>1f)return;
        for(int target=0;target<Context.targets.Length;target++)
        {
            Vector3 center=Footprint(Context.targets[target]);
            float fade=1f-Ease(.30f,.95f,age);
            for(int i=0;i<28;i++)
            {
                int key=i+target*41;float angle=Noise(key*13+1)*Mathf.PI*2f;
                float spread=(1f-Mathf.Exp(-age*5.2f))*(.6f+Noise(key*23)*1.5f);
                Vector3 point=center+new Vector3(Mathf.Cos(angle)*spread,
                    Mathf.Max(.02f,(.6f+Noise(key*19))*age*2.5f-age*age*3.5f),Mathf.Sin(angle)*spread*.65f);
                Color color=Color.Lerp(new Color(.29f,.24f,.14f),new Color(.8f,.68f,.40f),Noise(key*31));color.a=fade*alpha;
                Pixel(point,.04f+Noise(key*43)*.10f,color,Noise(key*29)*360f);
            }
            float flash=1f-Ease(0f,.10f,age);
            for(int i=0;i<14&&flash>0f;i++)
            {
                float angle=i*Mathf.PI*2f/14f;
                Pixel(center+new Vector3(Mathf.Cos(angle),.06f,Mathf.Sin(angle)*.6f)*(.25f+age*7f),
                    .035f,new Color(1f,.87f,.55f,flash*alpha));
            }
        }
    }

    private void EnsureScenery()
    {
        if(sceneryContext==Context&&scenery.Active)return;
        sceneryContext=Context;
        if(!scenery.Begin(Context.sceneryRegion,Vector3.back,Context.source.ground))
            throw new System.InvalidOperationException("Missing imperial horn regional cinematic scenery.");
        wave.sharedMaterial.mainTexture=scenery.Kit;
    }

    private void WorldCamera(float time)
    {
        Vector3 position,target;float lens;
        if(time<.60f)
        {
            float transition=Ease(.36f,.60f,time);
            position=Vector3.Lerp(Mirrored(3.4f,4.8f,-8f),Mirrored(1.2f,1.22f,-2.8f),transition);
            target=Vector3.Lerp(Mirrored(3f,2f,1f),Mirrored(1.25f,1f,.2f),transition);lens=Mathf.Lerp(36f,46f,transition);
        }
        else if(time<FilmLaunch)
        {
            position=Mirrored(1.2f,1.22f,-2.8f);target=Mirrored(1.25f,1f,.2f);lens=46f;
        }
        else if(time<1.90f)
        {
            float transition=Ease(FilmLaunch,1.59f,time);
            position=Vector3.Lerp(Mirrored(1.2f,1.22f,-2.8f),Mirrored(7.68f,14.4f,-11.2f),transition);
            target=Vector3.Lerp(Mirrored(1.25f,1f,.2f),Mirrored(7.2f,5.12f,1.28f),transition);lens=Mathf.Lerp(46f,40f,transition);
        }
        else if(time<FilmFall)
        {
            float transition=Ease(1.90f,2.12f,time);
            position=Vector3.Lerp(Mirrored(7.68f,14.4f,-11.2f),Mirrored(3.4f,.65f,-9f),transition);
            target=Vector3.Lerp(Mirrored(7.2f,5.12f,1.28f),Mirrored(4.3f,4.5f,.5f),transition);lens=Mathf.Lerp(40f,60f,transition);
        }
        else
        {
            float transition=Ease(FilmFall,2.82f,time);
            position=Vector3.Lerp(Mirrored(3.4f,.65f,-9f),Mirrored(3.8f,1.3f,-6.5f),transition);
            target=Vector3.Lerp(Mirrored(4.3f,4.5f,.5f),Mirrored(4.3f,2.2f,.7f),transition);lens=Mathf.Lerp(60f,48f,transition);
            float impact=1f-Ease(FilmLanding,3.30f,time);
            if(time>=FilmLanding)
            {
                int frame=Mathf.FloorToInt((time-FilmLanding)*60f);
                Vector3 shake=new Vector3(Noise(frame*17)-.5f,Noise(frame*31)-.5f,0f)*.12f*impact;
                position+=shake;target+=shake;
            }
        }
        float following=time<.60f?.65f*(1f-Ease(.25f,.60f,time)):Ease(FilmLaunch,1.59f,time);
        Vector3 offset=GroupCameraOffset()*following;
        float extraRise=0f;int lifted=0;
        for(int i=0;i<Context.targets.Length;i++)
            if(i>=Context.beetleLiftAllowed.Length||Context.beetleLiftAllowed[i])
            {extraRise+=Mathf.Max(0f,AirborneOffset(OriginalTime(time),i).y-3.6f*BeetleOverturnRules.Height(OriginalTime(time),i));lifted++;}
        if(lifted>0)extraRise/=lifted;
        float upward=Ease(1.62f,2.12f,time);
        position.y+=extraRise*Mathf.Lerp(.05f,.20f,upward);
        target.y+=extraRise*Mathf.Lerp(.22f,.85f,upward);
        CameraPose(position+offset,target+offset,lens);
        float framing=Ease(1.62f,1.90f,time)*(1f-Ease(FilmFall,2.82f,time));
        if(framing>0f)FrameAirborneVictims(time,framing);
    }

    private void FrameAirborneVictims(float time,float blend)
    {
        if(Context.targets.Length==0)return;
        float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        float action=OriginalTime(time);
        for(int i=0;i<Context.targets.Length;i++)
        {
            var actor=Context.targets[i];float seed=Noise(i*37+7),bodyHeight=1.4f*actor.size;
            bool lifted=i>=Context.beetleLiftAllowed.Length||Context.beetleLiftAllowed[i];
            Vector3 feet=Footprint(actor)+AirborneOffset(action,i);
            Sprite sprite=Frame(actor.frames,Mathf.Repeat((time-FilmLaunch)*2.8f+seed,1f))??actor.sprite;
            Bounds bounds=PixelMantisReaper.VisibleBody(sprite);
            float width=bodyHeight*bounds.size.x/Mathf.Max(.001f,bounds.size.y);
            float roll=lifted?BeetleOverturnRules.Roll(action,i)*(i%2==0?1f:-1f)*Side:0f;
            Quaternion orientation=CameraFacing*Quaternion.Euler(0f,0f,roll);
            Vector3 center=feet+CameraFacing*Vector3.up*(bodyHeight*.5f);
            for(int corner=0;corner<4;corner++)
            {
                Vector3 offset=new Vector3((corner%2==0?-1f:1f)*width*.5f,(corner<2?-1f:1f)*bodyHeight*.5f,0f);
                Vector3 point=View.transform.InverseTransformPoint(Root.TransformPoint(center+orientation*offset));
                float depth=Mathf.Max(.1f,point.z),x=point.x/depth,y=point.y/depth;
                minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
            }
        }
        float tangent=Mathf.Max((maxY-minY)/(2f*.76f),(maxX-minX)/(2f*.76f*View.aspect));
        float lens=Mathf.Clamp(2f*Mathf.Atan(tangent)*Mathf.Rad2Deg,18f,80f);
        View.fieldOfView=Mathf.Lerp(View.fieldOfView,lens,blend);
        tangent=Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f);
        Vector2 centerViewport=new Vector2(.5f+(minX+maxX)/(4f*tangent*View.aspect),.5f+(minY+maxY)/(4f*tangent));
        // Frame the real rotating silhouettes without moving any reserved ground position.
        Matrix4x4 projection=View.projectionMatrix;
        projection.m02+=2f*(centerViewport.x-.60f)*blend;projection.m12+=2f*(centerViewport.y-.57f)*blend;
        View.projectionMatrix=projection;
    }

    private void PullbackCamera(float time)
    {
        WorldCamera(3.18f);Vector3 start=View.transform.localPosition;Quaternion rotation=CameraFacing;
        Vector3 initial=View.WorldToViewportPoint(Root.TransformPoint(rotation*Vector3.up*(SourceHeight*.5f)));
        float targetHeight=Mathf.Max(.01f,Context.returnHeight/9f);
        Vector2 target=new Vector2(Context.returnPosition.x/16f+.5f,Context.returnPosition.y/9f+.5f+targetHeight*.5f);
        Quaternion above=Quaternion.Euler(25f,0f,0f);
        Vector3 finish=above*Vector3.up*(SourceHeight*.5f)-above*Vector3.forward*16f;
        float lens=2f*Mathf.Atan(SourceHeight/(32f*targetHeight))*Mathf.Rad2Deg;
        float pullback=Ease(3.18f,FieldReturnTime,time);
        View.transform.localPosition=Vector3.Lerp(start,finish,pullback);View.transform.localRotation=Quaternion.Slerp(rotation,above,pullback);
        View.fieldOfView=Mathf.Lerp(48f,lens,pullback);
        Vector3 projected=View.WorldToViewportPoint(Root.TransformPoint(CameraFacing*Vector3.up*(SourceHeight*.5f)));
        Vector2 center=Vector2.Lerp(new Vector2(initial.x,initial.y),target,pullback);
        Matrix4x4 projection=View.projectionMatrix;
        projection.m02+=2f*(projected.x-center.x);projection.m12+=2f*(projected.y-center.y);View.projectionMatrix=projection;
    }

    private static float OriginalTime(float time)
    {
        if(time<.60f)return Mathf.Lerp(0f,.22f,Mathf.InverseLerp(0f,.60f,time));
        if(time<FilmLaunch)return Mathf.Lerp(.22f,BeetleOverturnRules.Warning,Mathf.InverseLerp(.60f,FilmLaunch,time));
        if(time<FilmApex)return Mathf.Lerp(BeetleOverturnRules.Warning,BeetleOverturnRules.RiseEnd,Mathf.InverseLerp(FilmLaunch,FilmApex,time));
        if(time<FilmFall)return Mathf.Lerp(BeetleOverturnRules.RiseEnd,BeetleOverturnRules.ApexEnd,Mathf.InverseLerp(FilmApex,FilmFall,time));
        if(time<FilmLanding)return Mathf.Lerp(BeetleOverturnRules.ApexEnd,BeetleOverturnRules.Landing,Mathf.InverseLerp(FilmFall,FilmLanding,time));
        return Mathf.Lerp(BeetleOverturnRules.Landing,BeetleOverturnRules.End,Mathf.InverseLerp(FilmLanding,3.42f,time));
    }

    public override void Dispose(){scenery?.Dispose();base.Dispose();}
}
