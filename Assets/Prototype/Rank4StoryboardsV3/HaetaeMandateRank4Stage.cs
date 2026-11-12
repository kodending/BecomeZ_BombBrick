using System.Collections.Generic;
using UnityEngine;

// The paw wakes a separate shadow; only the existing battlefield guardian survives the pullback.
public sealed class HaetaeMandateRank4Stage:Rank4CinematicStage
{
    private const string ArtRoot="CombatCinematics/HaetaeShadowV1/";
    public const float FieldReturnTime=4.05f,NativeRevealTime=4.60f;
    public static readonly float[] BoardTimes={.38f,1.10f,1.90f,2.70f,3.65f,4.53f};
    public override string Family=>"haetae";
    public override float Duration=>4.80f;
    private readonly Sprite[] bodyFrames,shadowFrames,maneFrames,closedFrames,eyeFrames,nativeFrames;
    private readonly Sprite[,] maneBands=new Sprite[4,3];
    private readonly SpriteRenderer lead,shadow,closed,eye,native;
    private readonly SpriteRenderer[] mane=new SpriteRenderer[3];
    private readonly List<SpriteRenderer> targets=new List<SpriteRenderer>(),allies=new List<SpriteRenderer>();
    private readonly FrogCatchCinematicEnvironment scenery;
    private readonly ParticleSystem maneParticles;
    private readonly ParticleSystem.Particle[] curls=new ParticleSystem.Particle[12];
    private Rank4StageContext sceneryContext;
    private float Side=>Context.enemy?-1f:1f;
    private Vector3 HeadFeet=>new Vector3(Side*7f,0f,.7f);

    public HaetaeMandateRank4Stage(Transform parent):base(parent,"Haetae shadow awakening",Color.clear,1280,720)
    {
        Root.position=new Vector3(960f,320f,960f);
        bodyFrames=LoadAtlas(ArtRoot+"Body20",4,5);
        shadowFrames=LoadAtlas(ArtRoot+"Shadow4",4);
        maneFrames=LoadAtlas(ArtRoot+"Mane4",4);
        closedFrames=LoadAtlas(ArtRoot+"Closed4",4);
        eyeFrames=LoadAtlas(ArtRoot+"Eye4",4);
        nativeFrames=LoadAtlas("CombatEffects/Haetae/Devourer16",4,4);
        lead=MakeSprite("Lead Haetae paw",40);
        shadow=MakeSprite("Independent waking ground shadow",0);
        closed=MakeSprite("Closed-eye jade guardian",30);
        eye=MakeSprite("Opening gold eye and inhale",30);
        native=MakeSprite("Native jade guardian continuation",30);
        var nativeVeil=OwnMaterial(Resources.Load<Shader>("CombatEffects/Haetae/DevourerVeil"));
        nativeVeil.SetFloat("_Reveal",1f);native.sharedMaterial=nativeVeil;
        scenery=new FrogCatchCinematicEnvironment(Root);
        for(int band=0;band<mane.Length;band++)
        {
            mane[band]=MakeSprite("Layered jade mane "+band,12+band*3);
            for(int frame=0;frame<4;frame++)
            {
                Sprite source=maneFrames[frame];Rect cell=source.rect;
                var crop=new Rect(cell.x+cell.width*band/3f,cell.y,cell.width/3f,cell.height);
                maneBands[frame,band]=OwnSprite(Sprite.Create(source.texture,crop,Vector2.one*.5f,source.pixelsPerUnit,0,SpriteMeshType.FullRect));
                maneBands[frame,band].name=source.name+" depth band "+band;
            }
        }
        var curlObject=new GameObject("Layered four-drawing jade mane particles"){layer=Root.gameObject.layer,hideFlags=HideFlags.DontSave};
        curlObject.transform.SetParent(Root,false);maneParticles=curlObject.AddComponent<ParticleSystem>();
        var main=maneParticles.main;main.playOnAwake=false;main.maxParticles=curls.Length;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startLifetime=1f;main.startSpeed=0f;
        var emission=maneParticles.emission;emission.enabled=false;var shape=maneParticles.shape;shape.enabled=false;
        var material=OwnMaterial(Shader.Find("Sprites/Default"));material.mainTexture=maneFrames[0].texture;
        var renderer=maneParticles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sortingOrder=18;
        var sheet=maneParticles.textureSheetAnimation;sheet.enabled=true;sheet.mode=ParticleSystemAnimationMode.Sprites;
        sheet.timeMode=ParticleSystemAnimationTimeMode.Lifetime;sheet.cycleCount=1;
        sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,0f,1f,.999f));
        for(int frame=0;frame<4;frame++)
        {
            Sprite source=maneFrames[frame];Rect cell=source.rect;
            var crop=new Rect(cell.x,cell.y,cell.width*.46f,cell.height*.47f);
            Sprite curl=OwnSprite(Sprite.Create(source.texture,crop,Vector2.one*.5f,source.pixelsPerUnit,0,SpriteMeshType.FullRect));
            curl.name=ArtRoot+"Mane4 authored jade curl "+frame;sheet.AddSprite(curl);
        }
        maneParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    protected override void Animate(float time,float dt)
    {
        View.ResetProjectionMatrix();ActionTime=-1f;SoundBeat=time<.34f?-1:0;
        Shot=time<.65f?0:time<1.50f?1:time<2.30f?2:time<3.10f?3:time<FieldReturnTime?4:5;
        Opacity=1f-Ease(NativeRevealTime,Duration,time);
        maneParticles.SetParticles(curls,0);
        if(time>=3.90f){ReturnToField(time);return;}
        if(time<.10f)
        {
            scenery.Hide();View.backgroundColor=Color.clear;
            CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
            Paint(lead,Context.returnSprite??Context.source.sprite,Context.entryPosition,
                Mathf.Max(.3f,Context.entryHeight),Context.source.color,40,Context.source.flipX);
            return;
        }
        EnsureScenery();scenery.SetOpacity(1f);
        View.backgroundColor=scenery.SkyColor;WorldCamera(time);Bystanders();
        int paw=Step(time,0f,.65f,4);
        PaintFixedWorld(lead,bodyFrames[SpeciesRow()*4+paw],bodyFrames[SpeciesRow()*4+3],Vector3.zero,
            1.25f,Context.source.color,40,Context.source.flipX);
        GroundShadow(time);PawContact(time);
        if(time<1.50f)return;
        float emergence=Ease(1.50f,2.30f,time);
        if(time<2.52f)LayeredMane(time,emergence,1f-Ease(2.30f,2.52f,time));
        ManeCurls(time,emergence,1f-Ease(3.88f,FieldReturnTime,time));
        if(time>=2.30f&&time<3.10f)
            PaintFixedWorld(closed,closedFrames[Step(time,2.30f,3.10f,4)],closedFrames[3],HeadFeet,
                6.5f,White(Ease(2.30f,2.43f,time)),30,Context.enemy);
        else if(time>=3.10f&&time<3.48f)
            PaintFixedWorld(eye,eyeFrames[Step(time,3.10f,3.48f,4)],eyeFrames[0],HeadFeet,6.5f,Color.white,30,Context.enemy);
        else if(time>=3.48f)
            PaintFixedWorld(native,NativeFrame(time),nativeFrames[0],HeadFeet,6.5f,new Color(1f,1f,1f,.88f),30,Context.enemy);
        if(time>=3.30f)AmbientInhale(time);
    }

    private int SpeciesRow()=>Context.source.shiny&&Context.source.species=="haeta"?4:
        Context.source.species=="yeomhae"?1:Context.source.species=="geumgangsu"?2:Context.source.species=="mukryeong"?3:0;
    private static int Step(float time,float start,float end,int count)=>
        Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(start,end,time)*count),0,count-1);
    private static Color White(float alpha)=>new Color(1f,1f,1f,alpha);
    private Vector3 Mirrored(float x,float y,float z)=>new Vector3(x*Side,y,z);

    private void WorldCamera(float time)
    {
        if(time<.65f)
        {
            float approach=Ease(.38f,.65f,time);
            CameraPose(Vector3.Lerp(Mirrored(2.0f,3.2f,-6.2f),Mirrored(1.1f,1.2f,-2.0f),approach),
                Vector3.Lerp(Mirrored(2.1f,1.6f,.6f),Mirrored(1.1f,.20f,.6f),approach),42f);
        }
        else if(time<1.50f)
        {
            float track=Ease(1.12f,1.50f,time);
            CameraPose(Vector3.Lerp(Mirrored(1.1f,1.2f,-2.0f),Mirrored(2.9f,1.2f,-9.5f),track),
                Vector3.Lerp(Mirrored(1.1f,.20f,.6f),Mirrored(3.6f,.80f,1.4f),track),42f);
        }
        else if(time<2.30f)
        {
            float rise=Ease(1.50f,1.90f,time);
            CameraPose(Vector3.Lerp(Mirrored(2.9f,1.2f,-9.5f),Mirrored(4.0f,3.5f,-9.5f),rise),
                Vector3.Lerp(Mirrored(3.6f,.80f,1.4f),Mirrored(4.0f,3.2f,.7f),rise),42f-rise*3.5f);
        }
        else
        {
            float breathe=Ease(2.30f,FieldReturnTime,time);
            CameraPose(Vector3.Lerp(Mirrored(4.0f,3.5f,-9.5f),Mirrored(4.10f,3.35f,-9.0f),breathe),
                Mirrored(4.0f,3.2f,.7f),38.5f);
        }
    }

    private void PaintFixedWorld(SpriteRenderer renderer,Sprite sprite,Sprite anchor,Vector3 feet,float height,Color color,int order,bool flip)
    {
        // Shared cell pivots retain the drawn paw descent, jaw opening and mane breathing.
        PaintWorld(renderer,anchor,feet,height,color,order,CameraFacing,flip);renderer.sprite=sprite;
        renderer.transform.localScale*=sprite.pixelsPerUnit/anchor.pixelsPerUnit;
    }

    private void Bystanders(float alpha=1f)
    {
        DrawFormation(targets,Context.targets,"Actual opposing Haetae witness ",alpha);
        DrawFormation(allies,Context.allies,"Actual allied Haetae witness ",alpha);
    }

    private void DrawFormation(List<SpriteRenderer> renderers,FrogCatchCinematicStage.Actor[] actors,string name,float alpha)
    {
        while(renderers.Count<actors.Length)renderers.Add(MakeSprite(name+renderers.Count,10));
        for(int i=0;i<actors.Length;i++)
        {
            var actor=actors[i];Vector2 relative=actor.ground-Context.source.ground;
            Vector3 feet=new Vector3(relative.x*11f,0f,relative.y*8f);
            Color color=actor.color;color.a*=alpha;
            PaintWorld(renderers[i],actor.sprite,feet,1.25f*actor.size,color,10-i,CameraFacing,actor.flipX);
        }
    }

    private void GroundShadow(float time)
    {
        float reach=Ease(.38f,1.10f,time),travel=Ease(1.10f,1.50f,time);
        float alpha=Ease(.27f,.55f,time)*(1f-Ease(2.1f,2.52f,time));
        if(alpha<=0f)return;
        int frame=Step(time,.72f,1.62f,4);
        PaintArtWorld(shadow,shadowFrames[frame],Mirrored(.7f+reach+travel*2.45f,.015f,2.0f),
            new Vector2(1.8f+reach*2f+travel*4f,.8f+reach*2f+travel*.7f),
            new Color(.62f,.85f,.82f,alpha*.88f),0,Quaternion.Euler(90f,0f,0f));
        shadow.flipX=Context.enemy;
    }

    private void PawContact(float time)
    {
        float alpha=Ease(.30f,.40f,time)*(1f-Ease(.52f,1.05f,time));
        for(int i=0;i<32&&alpha>0f;i++)
        {
            float angle=i*Mathf.PI*2f/32f;
            Vector3 point=Mirrored(.40f+Mathf.Cos(angle)*.34f,.03f,.03f+Mathf.Sin(angle)*.19f);
            Pixel(point,.022f,new Color(.60f,.92f,.79f,alpha*.65f));
        }
    }

    private void LayeredMane(float time,float emergence,float alpha)
    {
        int frame=Step(time,1.50f,2.30f,4);Sprite anchor=maneFrames[3];
        Bounds bounds=PixelMantisReaper.VisibleBody(anchor);
        float scale=5.0f/Mathf.Max(.001f,bounds.size.y);
        Vector3 foot=new Vector3(Context.enemy?-bounds.center.x:bounds.center.x,bounds.min.y,0f);
        Vector3 center=HeadFeet-CameraFacing*(foot*scale);
        for(int band=0;band<3;band++)
        {
            SpriteRenderer renderer=mane[band];renderer.enabled=alpha>0f;renderer.sprite=maneBands[frame,band];
            renderer.color=White(alpha);renderer.flipX=Context.enemy;renderer.sortingOrder=12+band*3;
            float offset=(band-1)*anchor.rect.width/(3f*anchor.pixelsPerUnit)*scale*Side;
            renderer.transform.localScale=Vector3.one*scale;renderer.transform.localRotation=CameraFacing;
            renderer.transform.localPosition=center+CameraFacing*new Vector3(offset,-(1f-emergence)*.65f,0f)+
                new Vector3(0f,0f,(band-1)*.32f);
        }
    }

    private void ManeCurls(float time,float emergence,float alpha)
    {
        if(alpha<=0f)return;
        if(!maneParticles.isPaused){maneParticles.Play(false);maneParticles.Pause(false);}
        int frame=Step(time,1.50f,2.30f,4);
        for(int i=0;i<curls.Length;i++)
        {
            float angle=Noise(i*37)*Mathf.PI*2f,depth=Noise(i*19+1);
            float height=(.30f+Noise(i*71)*5.2f)*emergence;
            Vector3 position=HeadFeet+Mirrored(1.2f+Mathf.Cos(angle)*.7f,height,Mathf.Sin(angle)*1.1f);
            position.y+=Mathf.Sin(time*1.6f+i)*.045f;
            curls[i]=new ParticleSystem.Particle{position=position,startSize=.16f+depth*.18f,
                startColor=new Color(.68f,.94f,.84f,alpha*(.08f+depth*.08f)),rotation=Side*(Noise(i*13)-.5f)*32f,
                startLifetime=1f,remainingLifetime=1f-(frame+.20f)/4f,randomSeed=(uint)(i+1)};
        }
        maneParticles.SetParticles(curls,curls.Length);
    }

    private void AmbientInhale(float time)
    {
        float alpha=Ease(3.30f,3.52f,time)*(1f-Ease(3.82f,4.02f,time));
        Vector3 mouth=HeadFeet+CameraFacing*new Vector3(-Side*1.40f,2.6f,0f);
        for(int i=0;i<52&&alpha>0f;i++)
        {
            float phase=Mathf.Repeat((time-3.30f)*1.8f+Noise(i*23),1f);
            Vector3 start=mouth+Mirrored(-2.2f-Noise(i*41)*3.3f,(Noise(i*73)-.5f)*2.1f,(Noise(i*31)-.5f)*1.8f);
            Vector3 point=Vector3.Lerp(start,mouth,phase*phase);
            point.y+=Mathf.Sin(phase*Mathf.PI)*.20f;
            Pixel(point,.018f+Noise(i*17)*.022f,new Color(.74f,.88f,.79f,alpha*Mathf.Sin(phase*Mathf.PI)*.46f));
        }
    }

    private Sprite NativeFrame(float time)
    {
        if(time<3.86f)return nativeFrames[4+Step(time,3.48f,3.86f,4)];
        if(time<4.28f)return nativeFrames[11+Step(time,3.86f,4.28f,5)];
        return nativeFrames[Step(time,4.28f,4.52f,4)];
    }

    private void ReturnToField(float time)
    {
        PullbackCamera(time);View.backgroundColor=Color.clear;
        if(time>=NativeRevealTime)
        {
            scenery.Hide();return;
        }
        EnsureScenery();scenery.SetOpacity(1f-Ease(4.35f,NativeRevealTime,time));
        if(time<4.35f)View.backgroundColor=scenery.SkyColor;
        float formation=1f-Ease(FieldReturnTime,4.30f,time);
        if(formation>0f)
        {
            Bystanders(formation);Color color=Context.source.color;color.a*=formation;
            PaintFixedWorld(lead,bodyFrames[SpeciesRow()*4+3],bodyFrames[SpeciesRow()*4+3],Vector3.zero,1.25f,color,40,Context.source.flipX);
        }
        ManeCurls(time,1f,1f-Ease(3.88f,FieldReturnTime,time));AmbientInhale(time);
        Color tint=Color.Lerp(new Color(1f,1f,1f,.88f),Context.guardianReturnColor,Ease(FieldReturnTime,4.52f,time));
        Sprite sprite=time>=4.52f&&Context.guardianReturnSprite!=null?Context.guardianReturnSprite:NativeFrame(time);
        PaintFixedWorld(native,sprite,nativeFrames[0],HeadFeet,6.5f,tint,30,Context.enemy);
    }

    private void EnsureScenery()
    {
        if(sceneryContext==Context&&scenery.Active)return;
        sceneryContext=Context;
        if(!scenery.Begin(Context.sceneryRegion,Vector3.back,Context.source.ground))
            throw new System.InvalidOperationException("Missing Haetae regional cinematic scenery.");
    }

    private Vector3 NativeCenter(Sprite sprite,Quaternion rotation)
    {
        Bounds anchor=PixelMantisReaper.VisibleBody(nativeFrames[0]),body=PixelMantisReaper.VisibleBody(sprite);
        Vector3 center=body.center*(sprite.pixelsPerUnit/nativeFrames[0].pixelsPerUnit);
        center.x=(center.x-anchor.center.x)*(Context.enemy?-1f:1f);center.y-=anchor.min.y;
        return HeadFeet+rotation*(center*(6.5f/Mathf.Max(.001f,anchor.size.y)));
    }

    private void PullbackCamera(float time)
    {
        WorldCamera(3.90f);Vector3 start=View.transform.localPosition;Quaternion rotation=CameraFacing;
        Sprite face=Context.guardianReturnSprite??nativeFrames[0];
        Vector3 initial=View.WorldToViewportPoint(Root.TransformPoint(NativeCenter(face,rotation)));
        Vector2 destination=Context.guardianReturnSprite!=null?Context.guardianReturnPosition:new Vector2(Side*6.08f,-.45f);
        float targetHeight=Context.guardianReturnSprite!=null?Context.guardianReturnHeight/9f:.28f;
        Vector2 target=new Vector2(destination.x/16f+.5f,destination.y/9f+.5f+targetHeight*.5f);
        Bounds anchor=PixelMantisReaper.VisibleBody(nativeFrames[0]),body=PixelMantisReaper.VisibleBody(face);
        float physicalHeight=6.5f*body.size.y*face.pixelsPerUnit/(nativeFrames[0].pixelsPerUnit*anchor.size.y);
        Quaternion above=Quaternion.Euler(25f,0f,0f);
        Vector3 finish=NativeCenter(face,above)-above*Vector3.forward*16f;
        float lens=2f*Mathf.Atan(physicalHeight/(32f*Mathf.Max(.01f,targetHeight)))*Mathf.Rad2Deg;
        float pullback=Ease(3.90f,4.52f,time);
        View.transform.localPosition=Vector3.Lerp(start,finish,pullback);
        View.transform.localRotation=Quaternion.Slerp(rotation,above,pullback);
        View.fieldOfView=Mathf.Lerp(38.5f,lens,pullback);
        Vector3 projected=View.WorldToViewportPoint(Root.TransformPoint(NativeCenter(face,CameraFacing)));
        Vector2 center=Vector2.Lerp(new Vector2(initial.x,initial.y),target,pullback);
        // Move the perspective frustum as the camera retreats; the physical head never becomes a UI replica.
        Matrix4x4 projection=View.projectionMatrix;
        projection.m02+=2f*(projected.x-center.x);projection.m12+=2f*(projected.y-center.y);
        View.projectionMatrix=projection;
    }

    public override void Dispose(){scenery?.Dispose();base.Dispose();}
}
