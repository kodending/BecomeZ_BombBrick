using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

// Render-only stage. The caller owns combat, the unscaled clock and the live meteor shower.
public sealed class WizardCygnusCinematicStage:IDisposable
{
    public const float Duration=2.6f;
    public const string ResourceRoot="CombatCinematics/WizardCygnusShower/";
    private const int Layer=30,ParticleLimit=1800;
    private static readonly float[] PoseTimes={0f,.18f,.42f,.77f,1.28f,1.52f,1.86f,2.15f};
    private static readonly int[] OriginalFrames={0,1,2,2,3,4,4,5};
    private sealed class Body
    {
        public SpriteRenderer renderer;
        public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
    }
    private readonly GameObject root;
    private readonly Camera camera;
    private readonly RenderTexture texture;
    private readonly Material material,pixelMaterial,veilMaterial;
    private readonly Mesh veilMesh;
    private readonly Transform veil;
    private readonly FrogCatchCinematicEnvironment scenery;
    private readonly Body sourceBody,previousBody;
    private readonly WizardCygnusSkyVisual sky;
    private readonly ParticleSystem particles;
    private readonly ParticleSystem.Particle[] buffer=new ParticleSystem.Particle[ParticleLimit];
    private readonly Dictionary<string,Sprite[]> poseCache=new Dictionary<string,Sprite[]>();
    private readonly List<Sprite> ownedSprites=new List<Sprite>();
    private readonly Sprite[] fallbackPoses=new Sprite[8];
    private FrogCatchCinematicStage.Actor source;
    private Sprite[] poses;
    private Vector3 away;
    private Vector2 returnFoot,meteorReturn=new Vector2(.65f,.25f);
    private float referenceWidth,referenceHeight,returnHeight,shakeScale;
    private int count;
    private bool disposed,hasReturnView;
    public Texture Texture=>texture;
    public bool Active=>!disposed&&camera!=null&&camera.enabled;
    public FrogCatchCinematicStage.Actor Source=>source;
    public string SceneryRegion=>scenery.Region;
    public Texture2D SceneryKit=>scenery.Kit;
    public Texture2D Background{get;private set;}
    public Rect SourceViewportRect=>ViewportRect();
    public int Shot{get;private set;}

    public static string PoseResource(string species)
    {
        switch(species)
        {
            case "kongjwi":return ResourceRoot+"KongjwiAction";
            case "patjwi":return ResourceRoot+"PatjwiAction";
            case "seoljwi":return ResourceRoot+"SeoljwiAction";
            case "byeoljwi":return ResourceRoot+"ByeoljwiAction";
            default:return null;
        }
    }

    public WizardCygnusCinematicStage(Transform parent)
    {
        root=new GameObject("Wizard Cygnus private sky stage"){layer=Layer,hideFlags=HideFlags.DontSave};
        root.transform.SetParent(parent,false);root.transform.position=new Vector3(31000f,21000f,31000f);
        camera=Child("Cygnus private camera").AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=false;camera.nearClipPlane=.03f;camera.farClipPlane=160f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.cullingMask=1<<Layer;camera.depth=-100f;camera.allowHDR=false;camera.allowMSAA=false;
        int height=Mathf.Clamp(Screen.height,540,900),width=Mathf.Clamp(Mathf.RoundToInt(height*Screen.width/(float)Mathf.Max(1,Screen.height)),540,1600);
        texture=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32){name="Cygnus constellation cinematic",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        texture.Create();camera.targetTexture=texture;camera.aspect=width/(float)height;
        Shader actorShader=Resources.Load<Shader>(ResourceRoot+"CygnusActorSurface")??Shader.Find("Sprites/Default");
        material=new Material(actorShader){name="Actual Cygnus caster colors",hideFlags=HideFlags.DontSave};
        pixelMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=Texture2D.whiteTexture,name="Cygnus square stardust",hideFlags=HideFlags.DontSave};
        scenery=new FrogCatchCinematicEnvironment(root.transform);sourceBody=CreateBody("Cygnus actual caster");previousBody=CreateBody("Cygnus pose bridge");
        sky=new WizardCygnusSkyVisual(root.transform,Layer,15);
        Shader veilShader=Resources.Load<Shader>(ResourceRoot+"CygnusSkyVeil");
        if(veilShader!=null)veilMaterial=new Material(veilShader){name="Cygnus twilight over actual scenery",hideFlags=HideFlags.DontSave};
        veilMesh=new Mesh{name="Cygnus soft upper sky veil",hideFlags=HideFlags.DontSave};
        veilMesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0)};
        veilMesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};veilMesh.triangles=new[]{0,1,2,0,2,3};veilMesh.RecalculateBounds();
        GameObject skyVeil=Child("Cygnus sky twilight gradient");veil=skyVeil.transform;skyVeil.AddComponent<MeshFilter>().sharedMesh=veilMesh;
        var veilRenderer=skyVeil.AddComponent<MeshRenderer>();veilRenderer.sharedMaterial=veilMaterial;veilRenderer.sortingOrder=-50;veilRenderer.enabled=veilMaterial!=null;
        particles=Child("Cygnus pixel star wind and falling feathers").AddComponent<ParticleSystem>();
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=ParticleLimit;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.simulationSpeed=0f;main.startSpeed=0f;main.startSize3D=true;
        var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
        var particleRenderer=particles.GetComponent<ParticleSystemRenderer>();particleRenderer.sharedMaterial=pixelMaterial;particleRenderer.sortingOrder=90;
        particleRenderer.renderMode=ParticleSystemRenderMode.Billboard;root.SetActive(false);
    }

    public void Warmup(string species)
    {
        if(disposed)return;Sprite[] frames=LoadPoses(species);
        if(frames!=null)for(int i=0;i<frames.Length;i++)PixelMantisReaper.VisibleBody(frames[i]);
    }
    public bool Begin(Texture2D background,FrogCatchCinematicStage.Actor source,string region)
    {
        if(disposed)return false;Hide();if(background==null||ActualFrame(source,0)==null)return false;
        Background=background;this.source=source;poses=source.shiny?null:LoadPoses(source.species);
        if(poses==null){for(int i=0;i<8;i++)fallbackPoses[i]=ActualFrame(source,OriginalFrames[i]);poses=fallbackPoses;}
        for(int i=0;i<poses.Length;i++)PixelMantisReaper.VisibleBody(poses[i]);
        Bounds bounds=PixelMantisReaper.VisibleBody(poses[0]);referenceWidth=Mathf.Max(.02f,bounds.size.x);
        referenceHeight=2.85f*bounds.size.y/referenceWidth;
        hasReturnView=false;meteorReturn=new Vector2(.65f,.25f);shakeScale=BombBrick.Game.Core.GameUserSettings.Load().CameraShakeScale;
        away=new Vector3(source.flipX?-1f:1f,0f,-1.4f).normalized;
        if(!scenery.Begin(region,away,source.ground))return false;
        OverscanScenery();
        root.SetActive(true);camera.enabled=true;particles.Play(false);particles.Pause(false);Tick(0f,0f);return true;
    }
    private void OverscanScenery()
    {
        // The upward 58-degree shot sees wider than the frog stage. Extend only this
        // instance's horizon wings, keeping its central landscape and floor density.
        Transform environment=root.transform.Find("Frog generated cinematic environment");
        if(environment==null)return;
        Vector3 right=Vector3.Cross(away,Vector3.up).normalized;
        float extra=Mathf.Max(1.5f,camera.aspect*.95f)-1f;
        OverscanWall(environment.Find("Generated curved horizon"),right,extra);
        OverscanWall(environment.Find("Generated sky above the horizon"),right,extra);
        OverscanWall(environment.Find("Generated irregular near foliage"),right,extra);
    }
    private static void OverscanWall(Transform wall,Vector3 right,float extra)
    {
        if(wall==null)return;MeshFilter filter=wall.GetComponent<MeshFilter>();
        if(filter==null||filter.sharedMesh==null)return;
        Mesh mesh=filter.sharedMesh;Vector3[] points=mesh.vertices;
        for(int i=0;i<points.Length;i++)points[i]+=right*(Vector3.Dot(points[i],right)*extra);
        mesh.vertices=points;mesh.RecalculateBounds();
    }
    public void SetReturnView(Vector2 foot,float height)
    {returnFoot=foot;returnHeight=Mathf.Clamp(height,.005f,.8f);hasReturnView=true;}
    public void SetMeteorReturnView(Vector2 targetViewport)
    {meteorReturn=new Vector2(Mathf.Clamp(targetViewport.x,.03f,.97f),Mathf.Clamp(targetViewport.y,.03f,.97f));}

    public void Tick(float elapsed,float dt)
    {
        if(!Active)return;float t=Mathf.Clamp(elapsed,0f,Duration);count=0;
        Shot=t<.4f?1:t<1.3f?2:t<2f?3:4;
        float upward=Ease(.26f,1.16f,t),unfurl=Ease(1.25f,1.72f,t),restore=Ease(2f,Duration,t);
        Vector3 view=Vector3.Lerp(away*8.2f+Vector3.up*2.1f,away*15f+Vector3.up*3.2f,upward);
        Vector3 look=Vector3.up*Mathf.Lerp(Mathf.Clamp(referenceHeight*.47f,1.2f,2.4f),5.3f,upward);
        view=Vector3.Lerp(view,away*9.8f+Vector3.up*5f,restore);look=Vector3.Lerp(look,Vector3.up*1.4f,restore);
        float impact=Pulse(t,1.28f,.10f)*.4f+Pulse(t,1.56f,.12f)*.6f;
        view+=Vector3.Cross(Vector3.up,away)*(Mathf.Sin(t*94f)*impact*.022f*shakeScale);
        camera.transform.localPosition=view;camera.transform.localRotation=Quaternion.LookRotation(look-view,Vector3.up);
        camera.fieldOfView=Mathf.Lerp(Mathf.Lerp(45f,58f,upward),47f,restore);
        camera.backgroundColor=Color.Lerp(scenery.SkyColor,new Color(.018f,.035f,.11f),upward*(1f-restore)*.72f);
        if(veilMaterial!=null)
        {
            float distance=1.2f,half=distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            veil.localPosition=view+camera.transform.localRotation*Vector3.forward*distance;veil.localRotation=camera.transform.localRotation;
            veil.localScale=new Vector3(half*camera.aspect,half,1f);veilMaterial.SetFloat("_Night",Ease(.12f,.87f,t)*(1f-restore));
        }
        int pose=PoseAt(t),last=Mathf.Max(0,pose-1);float blend=pose==0?1f:Ease(PoseTimes[pose],PoseTimes[pose]+.055f,t);
        Sprite frame=poses[pose];if(frame==poses[last])blend=1f;
        float release=Pulse(t,1.55f,.2f),rise=Ease(.1f,.75f,t)*(1f-Ease(1.75f,2.2f,t));
        Vector2 stretch=new Vector2(1f+release*.045f,1f+rise*.025f-release*.035f);
        Vector3 foot=Vector3.up*(Mathf.Sin(rise*Mathf.PI)*.035f);
        float scale=2.85f/referenceWidth,returnBlend=hasReturnView?Ease(2.15f,Duration,t):0f;
        Bounds body=PixelMantisReaper.VisibleBody(frame);
        if(returnBlend>0f)
        {
            float distance=Vector3.Dot(foot-view,camera.transform.localRotation*Vector3.forward);
            float half=distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            scale=Mathf.Lerp(scale,returnHeight*2f*half/Mathf.Max(.02f,body.size.y*stretch.y),returnBlend);
            foot=Vector3.Lerp(foot,ViewportPoint(returnFoot,distance),returnBlend);
        }
        DrawBody(sourceBody,frame,foot,scale,stretch,blend,(.18f+rise*.4f)*(1f-returnBlend),70);
        previousBody.renderer.enabled=blend<.999f;
        if(previousBody.renderer.enabled)DrawBody(previousBody,poses[last],foot,scale,stretch,1f-blend,rise*.4f*(1f-returnBlend),71);
        if(returnBlend>0f)
        {
            Rect actual=SourceViewportRect;float distance=Vector3.Dot(sourceBody.renderer.transform.localPosition-view,camera.transform.localRotation*Vector3.forward);
            float half=distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            Vector3 shift=camera.transform.localRotation*new Vector3((returnFoot.x-actual.center.x)*2f*half*camera.aspect,(returnFoot.y-actual.yMin)*2f*half,0f)*returnBlend;
            sourceBody.renderer.transform.localPosition+=shift;previousBody.renderer.transform.localPosition+=shift;
        }
        float skyAlpha=Ease(.26f,.78f,t)*(1f-Ease(2.02f,Duration,t));
        float halfSky=18f*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
        float skyWidth=Mathf.Min(halfSky*2f*camera.aspect*.90f,halfSky*2f*.53f*sky.VisibleAspect);
        float skyPose=t<.96f?Ease(.4f,.96f,t):t<1.24f?1f+Ease(.96f,1.24f,t):t<1.6f?2f:2f+Ease(1.6f,2.02f,t);
        sky.Render(t,skyPose,ViewportPoint(new Vector2(.5f,.68f),18f),camera.transform.localRotation,
            skyWidth*Mathf.Lerp(.75f,1f,upward),skyAlpha,Ease(.27f,1.14f,t));
        Starlight(t,upward,unfurl,restore,returnBlend);particles.SetParticles(buffer,count);
    }

    private void Starlight(float t,float upward,float unfurl,float restore,float returnBlend)
    {
        Quaternion view=camera.transform.localRotation;Vector3 right=view*Vector3.right,up=view*Vector3.up;
        Vector3 wand=BodyPoint(new Vector2(.68f,.96f));
        float wind=Ease(.04f,.25f,t)*(1f-Ease(1.55f,2.05f,t));
        Add(wand,.15f,new Color(.85f,.98f,1f,wind));
        for(int i=0;i<48;i++)
        {
            float seed=Seed(i+4),life=Mathf.Repeat(t*.55f+i*.618034f,1f),angle=life*Mathf.PI*4f+i;
            Vector3 point=wand+right*(Mathf.Cos(angle)*(.13f+life*.4f))+up*(life*1.3f-.35f);
            Add(point,.025f+seed*.027f,new Color(.55f,.84f,1f,(1f-life)*wind*.75f));
        }
        for(int i=0;i<155;i++)
        {
            float seed=Seed(i+31),x=Seed(i+144),y=.37f+Seed(i+231)*.65f;
            Vector3 point=ViewportPoint(new Vector2(x,y),20f+seed*2f);
            float twinkle=.25f+.75f*Mathf.Pow(.5f+.5f*Mathf.Sin(t*(1.5f+seed)+i),5);
            Add(point,.025f+seed*.028f,new Color(.61f,.81f,1f,upward*(1f-restore)*twinkle*.75f));
        }
        float launch=Ease(1.56f,1.82f,t);
        for(int i=0;i<19;i++)
        {
            float offset=i*.051f,flight=t-(1.6f+offset*.53f);if(flight<0f||flight>.8f)continue;
            float u=flight/.8f,seed=Seed(i+42);
            Vector2 begin=new Vector2(-.08f+seed*.76f,1.12f+Seed(i+5)*.25f);
            Vector2 end=Vector2.Lerp(new Vector2(.72f+seed*.2f,.17f+seed*.2f),meteorReturn,restore);
            float progress=.18f*u+.82f*u*u;
            for(int j=0;j<15;j++)
            {
                float tail=j/15f,behind=Mathf.Max(0f,progress-tail*.24f);
                Vector2 position=Vector2.Lerp(begin,end,behind);
                Vector3 point=ViewportPoint(position,8f+seed*3f);
                Add(point,new Vector3(.035f+(.045f*(1f-tail)),.08f+.1f*(1f-tail),.035f),
                    Color.Lerp(new Color(.6f,.83f,1f,launch*(1f-tail)*.55f),new Color(1f,.98f,.83f,launch),j==0?1f:0f),-38f);
            }
        }
        float halo=unfurl*(1f-returnBlend);
        for(int i=0;i<58;i++)
        {
            float seed=Seed(i+111),life=Mathf.Repeat(t*.65f+i*.618034f,1f);
            Vector2 point=new Vector2(.14f+seed*.72f,.74f-life*.32f);
            Add(ViewportPoint(point,14f),new Vector3(.025f,.04f+seed*.065f,.025f),new Color(.67f,.88f,1f,halo*(1f-life)*.6f),35f);
        }
    }
    private Vector3 ViewportPoint(Vector2 point,float distance)
    {
        float half=distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
        return camera.transform.localPosition+camera.transform.localRotation*new Vector3((point.x-.5f)*2f*half*camera.aspect,(point.y-.5f)*2f*half,distance);
    }
    private void Add(Vector3 point,float size,Color color)=>Add(point,Vector3.one*size,color,0f);
    private void Add(Vector3 point,Vector3 size,Color color,float rotation)
    {
        if(count>=ParticleLimit||color.a<.003f)return;
        buffer[count]=new ParticleSystem.Particle{position=point,startSize3D=size,startColor=color,rotation=rotation,startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(count+1)};count++;
    }
    private Body CreateBody(string name)
    {var renderer=Child(name).AddComponent<SpriteRenderer>();renderer.sharedMaterial=material;renderer.enabled=false;return new Body{renderer=renderer};}
    private void DrawBody(Body body,Sprite frame,Vector3 foot,float scale,Vector2 stretch,float alpha,float glow,int order)
    {
        if(frame==null){body.renderer.enabled=false;return;}Bounds bounds=PixelMantisReaper.VisibleBody(frame);
        Vector3 anchor=new Vector3(bounds.center.x,bounds.min.y,0f);if(source.flipX)anchor.x=-anchor.x;
        Transform transform=body.renderer.transform;transform.localRotation=camera.transform.localRotation;transform.localScale=new Vector3(scale*stretch.x,scale*stretch.y,scale);
        transform.localPosition=foot-transform.localRotation*Vector3.Scale(anchor,transform.localScale);
        body.renderer.sprite=frame;body.renderer.flipX=source.flipX;body.renderer.sortingOrder=order;body.renderer.enabled=true;
        Color color=source.color;if(color.a<=0f)color=Color.white;color.a*=alpha;body.renderer.color=color;
        body.properties.SetTexture("_MainTex",frame.texture);body.properties.SetFloat("_Glow",glow);body.renderer.SetPropertyBlock(body.properties);
    }
    private Vector3 BodyPoint(Vector2 point)
    {
        var renderer=sourceBody.renderer;Bounds bounds=PixelMantisReaper.VisibleBody(renderer.sprite);
        Vector3 local=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,point.x),Mathf.Lerp(bounds.min.y,bounds.max.y,point.y),0f);if(source.flipX)local.x=-local.x;
        return renderer.transform.localPosition+renderer.transform.localRotation*Vector3.Scale(local,renderer.transform.localScale);
    }
    private Rect ViewportRect()
    {
        var renderer=sourceBody.renderer;if(renderer==null||renderer.sprite==null)return Rect.zero;
        Bounds bounds=PixelMantisReaper.VisibleBody(renderer.sprite);Quaternion inverse=Quaternion.Inverse(camera.transform.localRotation);
        float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity,tangent=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
        for(int i=0;i<4;i++)
        {
            Vector3 point=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,0f);if(source.flipX)point.x=-point.x;
            Vector3 position=renderer.transform.localPosition+renderer.transform.localRotation*Vector3.Scale(point,renderer.transform.localScale);
            Vector3 local=inverse*(position-camera.transform.localPosition);float half=Mathf.Max(.001f,local.z*tangent);
            float x=.5f+local.x/(2f*half*camera.aspect),y=.5f+local.y/(2f*half);
            minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
        }
        return Rect.MinMaxRect(minX,minY,maxX,maxY);
    }
    private Sprite[] LoadPoses(string species)
    {
        if(string.IsNullOrEmpty(species))return null;Sprite[] cached;if(poseCache.TryGetValue(species,out cached))return cached;
        string path=PoseResource(species);Texture2D atlas=path==null?null:Resources.Load<Texture2D>(path);if(atlas==null)return null;
        var frames=new Sprite[8];
        for(int i=0;i<frames.Length;i++)
        {
            int left=Mathf.FloorToInt(i%4*atlas.width/4f),right=Mathf.FloorToInt((i%4+1)*atlas.width/4f);
            int bottom=Mathf.FloorToInt((1-i/4)*atlas.height/2f),top=Mathf.FloorToInt((2-i/4)*atlas.height/2f);
            Sprite frame=Sprite.Create(atlas,new Rect(left,bottom,right-left,top-bottom),new Vector2(.5f,.15f),128f,0,SpriteMeshType.FullRect);
            frame.name=species+" Cygnus action "+i;frame.hideFlags=HideFlags.DontSave;frames[i]=frame;ownedSprites.Add(frame);
        }
        poseCache.Add(species,frames);return frames;
    }
    private static Sprite ActualFrame(FrogCatchCinematicStage.Actor actor,int index)
    {return actor.frames!=null&&actor.frames.Length>0?actor.frames[Mathf.Clamp(index,0,actor.frames.Length-1)]??actor.sprite:actor.sprite;}
    private static int PoseAt(float t){for(int i=PoseTimes.Length-1;i>0;i--)if(t>=PoseTimes[i])return i;return 0;}
    private GameObject Child(string name){var host=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};host.transform.SetParent(root.transform,false);return host;}
    public void Hide(){if(disposed)return;camera.enabled=false;scenery.Hide();sky.Hide();particles.Clear(false);root.SetActive(false);}
    public void Dispose()
    {
        if(disposed)return;Hide();disposed=true;sky.Dispose();scenery.Dispose();camera.targetTexture=null;texture.Release();Destroy(texture);Destroy(material);Destroy(pixelMaterial);Destroy(veilMaterial);Destroy(veilMesh);
        foreach(Sprite frame in ownedSprites)Destroy(frame);ownedSprites.Clear();Destroy(root);
    }
    private static float Seed(int index)=>Mathf.Repeat(index*.61803399f+.371f,1f);
    private static float Ease(float start,float end,float time){float u=Mathf.InverseLerp(start,end,time);return u*u*(3f-2f*u);}
    private static float Pulse(float time,float at,float width)=>Mathf.Pow(Mathf.Max(0f,1f-Mathf.Abs(time-at)/width),2f);
    private static void Destroy(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
