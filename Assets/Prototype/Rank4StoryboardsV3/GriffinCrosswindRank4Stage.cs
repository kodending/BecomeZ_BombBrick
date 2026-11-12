using UnityEngine;

// Only summon the persistent orb here; the native battle owns absorption and the counterattack.
public sealed class GriffinCrosswindRank4Stage:Rank4CinematicStage
{
    private const string ArtRoot="CombatCinematics/GriffinStormOrbV1/";
    private const float SourceHeight=1.8f,CoreHeight=1.55f;
    public const float PrivateStageTime=.25f,FieldRevealTime=3.10f,FieldReturnTime=4.05f;
    public static readonly float[] BoardTimes={.15f,.75f,1.85f,2.75f,3.55f,4.12f};
    public override string Family=>"griffin";
    public override float Duration=>4.25f;
    private readonly Sprite[] bodyFrames,orbFrames,burstFrames;
    private readonly SpriteRenderer lead,orb,release,shadow;
    private readonly Sprite groundShadow;
    private readonly FrogCatchCinematicEnvironment scenery;
    private readonly MeshRenderer[] currents=new MeshRenderer[3];
    private readonly Mesh[] currentMeshes=new Mesh[3];
    private readonly Vector3[][] currentVertices=new Vector3[3][];
    private readonly Color[][] currentColors=new Color[3][];
    private Rank4StageContext sceneryContext;
    private const int Segments=96;
    private float Side=>Context.enemy?-1f:1f;
    private Vector2 OrbAnchor=>Context.griffinOrbViewport.y>0f?Context.griffinOrbViewport:new Vector2(Context.enemy?.76f:.55f,.85f);
    private float ReturnCoreHeight=>Context.griffinOrbHeight>0f?Context.griffinOrbHeight:.11f;

    public GriffinCrosswindRank4Stage(Transform parent):base(parent,"Griffin sovereign storm orb",Color.clear,1280,720)
    {
        Root.position=new Vector3(1460f,320f,1460f);
        bodyFrames=LoadAtlas(ArtRoot+"Body60",4,15);orbFrames=LoadAtlas(ArtRoot+"Orb8",4,2);burstFrames=LoadAtlas(ArtRoot+"Burst8",4,2);
        lead=MakeSprite("Lead storm orb griffin",40);orb=MakeSprite("Sovereign storm orb core",65);
        release=MakeSprite("Released storm orb feather pressure",20);shadow=MakeSprite("Planted griffin storm orb contact",0);
        groundShadow=PixelMonkeyMortar.ShadowSprite();scenery=new FrogCatchCinematicEnvironment(Root);
        int[] triangles=new int[Segments*6];
        for(int i=0;i<Segments;i++)
        {int v=i*2,k=i*6;triangles[k]=v;triangles[k+1]=v+1;triangles[k+2]=v+2;triangles[k+3]=v+1;triangles[k+4]=v+3;triangles[k+5]=v+2;}
        for(int i=0;i<currents.Length;i++)
        {
            currentVertices[i]=new Vector3[(Segments+1)*2];currentColors[i]=new Color[(Segments+1)*2];
            currents[i]=Geometry("Three-dimensional feather wind "+i,currentVertices[i],triangles,currentColors[i]);
            currentMeshes[i]=currents[i].GetComponent<MeshFilter>().sharedMesh;currents[i].enabled=false;
        }
    }

    protected override void Animate(float time,float dt)
    {
        View.ResetProjectionMatrix();ActionTime=-1f;SoundBeat=time<.30f?-1:time<1.35f?0:time<2.42f?1:2;
        Shot=time<PrivateStageTime?0:time<1.35f?1:time<2.25f?2:time<FieldRevealTime?3:time<FieldReturnTime?4:5;
        Opacity=1f-Ease(FieldReturnTime,Duration,time);foreach(var current in currents)current.enabled=false;
        if(time<PrivateStageTime||time>=FieldReturnTime)
        {
            scenery.Hide();View.backgroundColor=Color.clear;CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
            if(time<PrivateStageTime)
                Paint(lead,Context.returnSprite??Context.source.sprite,Context.entryHeight>0f?Context.entryPosition:Context.returnPosition,
                    Mathf.Max(.3f,Context.entryHeight>0f?Context.entryHeight:Context.returnHeight),Context.source.color,40,Context.source.flipX);
            else {PaintReturn(lead,40);PaintScreenOrb(OrbAnchor,ReturnCoreHeight,0,1f);}
            return;
        }
        EnsureScenery();WorldCamera(time);
        float field=Ease(FieldRevealTime,FieldReturnTime,time),entrance=Ease(PrivateStageTime,.38f,time);
        // Reveal the real sky before the orb finishes its separate menu-anchor match.
        scenery.SetOpacity(entrance*(1f-Ease(FieldRevealTime,3.45f,time)));
        View.backgroundColor=time<.38f||time>=FieldRevealTime?Color.clear:scenery.SkyColor;
        int group=time<1.35f?0:time<2.25f?1:2;
        int pose=group*4+Step(time,group==0?.25f:group==1?1.35f:2.25f,group==0?1.35f:group==1?2.25f:3.10f,4);
        PaintBody(pose,group);if(time>=FieldRevealTime)MatchSourceToField(time);
        PaintArtWorld(shadow,groundShadow,new Vector3(0f,.012f,0f),new Vector2(1.3f,.65f),
            new Color(.025f,.04f,.045f,(1f-field)*.35f),0,Quaternion.Euler(90f,0f,0f));
        if(time>=.45f)
        {
            Vector3 center=OrbPosition(time);float size=CoreHeight*Mathf.Lerp(.24f,1f,Ease(.45f,1.35f,time));int frame=OrbFrame(time);
            if(time<FieldRevealTime)PaintCore(orb,orbFrames[frame],center,size,1f,65);
            else
            {
                Vector3 projected=View.WorldToViewportPoint(Root.TransformPoint(center));
                Vector2 viewport=Vector2.Lerp(new Vector2(projected.x,projected.y),OrbAnchor,field);
                PaintScreenOrb(viewport,Mathf.Lerp(ProjectedHeight(center,size),ReturnCoreHeight,Ease(3.55f,FieldReturnTime,time)),frame,1f);
            }
            DrawCurrents(time,center,size,1f-field);DrawFeathers(time,center,size,1f-field);
        }
        if(time>=2.28f&&time<3.10f)
            PaintCore(release,burstFrames[Step(time,2.28f,3.10f,8)],new Vector3(0f,.95f,0f),3.4f,
                (1f-Ease(2.80f,3.10f,time))*.76f,20);
    }

    private int SpeciesRow()=>Context.source.shiny&&Context.source.species=="holophonas"?4:
        Context.source.species=="griphonas"?1:Context.source.species=="holophonas"?2:Context.source.species=="entpone"?3:0;
    private static int Step(float time,float start,float end,int count)=>Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(start,end,time)*count),0,count-1);
    private static int OrbFrame(float time)=>time<1.35f?Step(time,.45f,1.35f,8):Mathf.FloorToInt(Mathf.Repeat((time-FieldReturnTime)/.6f,1f)*8f)%8;
    private Vector3 Mirror(float x,float y,float z)=>new Vector3(x*Side,y,z);

    private void PaintBody(int pose,int group)
    {
        int start=SpeciesRow()*12;
        PaintWorld(lead,bodyFrames[start+group*4+3],Vector3.zero,SourceHeight,Context.source.color,40,CameraFacing,Context.enemy);
        lead.sprite=bodyFrames[start+pose];
    }

    private void PaintCore(SpriteRenderer renderer,Sprite sprite,Vector3 center,float height,float alpha,int order)
    {
        // Common peak anchors keep tiny wisps from being normalized to full core size.
        Bounds visible=PixelMantisReaper.VisibleBody(renderer==release?burstFrames[3]:orbFrames[0]);
        float scale=height/Mathf.Max(.001f,visible.size.y);
        renderer.enabled=alpha>0f;renderer.sprite=sprite;renderer.flipX=false;renderer.color=new Color(1f,1f,1f,alpha);renderer.sortingOrder=order;
        renderer.transform.localRotation=CameraFacing;renderer.transform.localScale=Vector3.one*scale;
        renderer.transform.localPosition=center-CameraFacing*(visible.center*scale);
    }

    private void PaintScreenOrb(Vector2 viewport,float height,int frame,float alpha)
    {
        float physicalHeight=View.orthographic?height*View.orthographicSize*2f:height*24f*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f);
        PaintCore(orb,orbFrames[frame],ScreenWorld(viewport),physicalHeight,alpha,65);
    }
    private Vector3 ScreenWorld(Vector2 viewport)=>Root.InverseTransformPoint(View.ViewportToWorldPoint(new Vector3(viewport.x,viewport.y,12f)));
    private float ProjectedHeight(Vector3 center,float height)
    {
        Vector3 up=CameraFacing*Vector3.up*(height*.5f);
        return Mathf.Abs(View.WorldToViewportPoint(Root.TransformPoint(center+up)).y-View.WorldToViewportPoint(Root.TransformPoint(center-up)).y);
    }
    private Vector3 OrbPosition(float time)
    {
        float release=Ease(2.35f,3.10f,time),ascent=Ease(3.10f,3.70f,time);
        return Mirror(Mathf.Lerp(1.1f,.06f,release),Mathf.Lerp(1.28f,3.10f,release)+ascent*3f,.10f);
    }

    private void MatchSourceToField(float time)
    {
        float blend=Ease(FieldRevealTime,3.50f,time);
        Vector3 projected=View.WorldToViewportPoint(Root.TransformPoint(CameraFacing*Vector3.up*(SourceHeight*.5f)));
        float height=ProjectedHeight(CameraFacing*Vector3.up*(SourceHeight*.5f),SourceHeight),desiredHeight=Mathf.Max(.001f,Context.returnHeight/9f);
        Vector2 target=new Vector2(Context.returnPosition.x/16f+.5f,Context.returnPosition.y/9f+.5f+desiredHeight*.5f);
        Vector2 center=Vector2.Lerp(new Vector2(projected.x,projected.y),target,blend);float normalizedHeight=Mathf.Lerp(height,desiredHeight,blend);
        Vector3 feet=ScreenWorld(center-Vector2.up*(normalizedHeight*.5f));
        float physicalHeight=normalizedHeight*24f*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f);
        Sprite sprite=time>=3.50f?Context.returnSprite??Context.source.sprite:bodyFrames[SpeciesRow()*12+11];
        PaintWorld(lead,sprite,feet,physicalHeight,Context.source.color,40,CameraFacing,time>=3.50f?Context.source.flipX:Context.enemy);
        lead.enabled=Context.returnBodyVisible;
    }

    private void DrawCurrents(float time,Vector3 center,float size,float alpha)
    {
        if(alpha<=0f)return;float gather=Ease(.45f,1.1f,time);
        for(int layer=0;layer<currents.Length;layer++)
        {
            Quaternion tilt=Quaternion.Euler(30f+layer*48f,layer*57f,layer*31f);
            for(int step=0;step<=Segments;step++)
            {
                float t=step/(float)Segments,angle=(t*.84f+time*.52f+layer*.29f)*Mathf.PI*2f,radius=size*(.52f+.12f*layer);
                Vector3 point=center+tilt*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,.10f*Mathf.Sin(angle*2f));
                Vector3 tangent=tilt*new Vector3(-Mathf.Sin(angle),Mathf.Cos(angle),0f),width=Vector3.Cross(tangent,CameraFacing*Vector3.forward).normalized;
                float thickness=(.018f+layer*.004f)*Mathf.Sin(t*Mathf.PI)*gather;int v=step*2;
                currentVertices[layer][v]=point-width*thickness;currentVertices[layer][v+1]=point+width*thickness;
                Color color=Color.Lerp(new Color(.57f,1f,.88f),new Color(1f,.84f,.38f),layer*.48f);color.a=alpha*Mathf.Sin(t*Mathf.PI)*.85f;
                currentColors[layer][v]=currentColors[layer][v+1]=color;
            }
            currentMeshes[layer].vertices=currentVertices[layer];currentMeshes[layer].colors=currentColors[layer];
            currentMeshes[layer].RecalculateBounds();currents[layer].enabled=true;
        }
    }

    private void DrawFeathers(float time,Vector3 center,float size,float alpha)
    {
        for(int i=0;i<45&&alpha>0f;i++)
        {
            float phase=Mathf.Repeat(time*.45f+Noise(i*17),1f),angle=(Noise(i*29)+phase*.8f)*Mathf.PI*2f;
            float radius=(1f-phase)*(1.5f+Noise(i*31)*1.4f)+size*.35f;
            Vector3 position=center+new Vector3(Mathf.Cos(angle)*radius,.35f*Mathf.Sin(angle*1.4f),Mathf.Sin(angle)*radius);
            Color color=Color.Lerp(new Color(.60f,1f,.84f),new Color(1f,.83f,.38f),Noise(i*43));color.a=alpha*Mathf.Sin(phase*Mathf.PI)*.8f;
            Pixel(position,.025f+Noise(i*23)*.045f,color,angle*Mathf.Rad2Deg);
        }
    }

    private void EnsureScenery()
    {
        if(sceneryContext==Context&&scenery.Active)return;sceneryContext=Context;
        if(!scenery.Begin(Context.sceneryRegion,Vector3.back,Context.source.ground))
            throw new System.InvalidOperationException("Missing sovereign storm orb regional cinematic scenery.");
    }
    private void WorldCamera(float time)
    {
        Vector3 position,target;float lens;
        if(time<1.35f){position=Mirror(1.8f,1.3f,-3.1f);target=Mirror(0f,1.10f,0f);lens=38f;}
        else if(time<2.25f){position=Mirror(1.1f,1.85f,-2.5f);target=Mirror(.5f,1.65f,0f);lens=50f;}
        else
        {
            float following=Ease(3.00f,3.68f,time);
            position=Vector3.Lerp(Mirror(-1.8f,.95f,-3f),Mirror(-3f,1.3f,-7f),following);
            target=Vector3.Lerp(Mirror(.15f,1.5f,0f),Mirror(0f,4.2f,0f),following);lens=Mathf.Lerp(43f,40f,following);
        }
        CameraPose(position,target,lens);
        if(time>=2.25f&&time<3.45f)
        {
            Vector3 center=View.WorldToViewportPoint(Root.TransformPoint(CameraFacing*Vector3.up*(SourceHeight*.5f)));
            float hold=1f-Ease(FieldRevealTime,3.45f,time);
            Matrix4x4 projection=View.projectionMatrix;projection.m02+=2f*(center.x-.5f)*hold;projection.m12+=2f*(center.y-.31f)*hold;View.projectionMatrix=projection;
        }
    }
    public override void Dispose(){scenery?.Dispose();base.Dispose();}
}
