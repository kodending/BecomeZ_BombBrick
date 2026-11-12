using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Actor snapshots and a private camera only. This renderer never owns combat units or their clocks.
public sealed class FrogCatchCinematicStage:IDisposable
{
    public const float Duration=4f;
    public const string ResourceRoot="CombatCinematics/FrogCatch/";
    private const int Layer=30;
    private const float ArenaHalfWidth=11f,ArenaHalfDepth=8f;
    // Mouth centers in the painted 627px cells, measured from each cell's top-left.
    private static readonly Vector2[] GolgaebiMouths={new Vector2(480,355),new Vector2(420,312),new Vector2(500,195),new Vector2(495,338)};
    private static readonly Vector2[] KingpaMouths={new Vector2(483,370),new Vector2(433,326),new Vector2(526,241),new Vector2(485,373)};
    private static readonly Vector2[] RokrokMouths={new Vector2(492,396),new Vector2(450,319),new Vector2(510,202),new Vector2(496,360)};
    private static readonly Vector2[] FroskMouths={new Vector2(497,433),new Vector2(467,337),new Vector2(513,212),new Vector2(490,400)};
    public struct Actor
    {
        public string species;
        public Sprite sprite;
        public Sprite[] frames;
        public bool flipX,shiny;
        public Color color;
        public Vector2 ground;
        public float size;
    }
    private sealed class Body
    {
        public Actor actor;
        public SpriteRenderer renderer;
        public Vector3 ground,position,suspended,scatter;
        public float height;
    }
    private sealed class Tongue
    {
        public Mesh mesh;
        public MeshRenderer renderer;
        public readonly Vector3[] vertices=new Vector3[33*4];
        public readonly Color[] colors=new Color[33*4];
    }

    private readonly GameObject root;
    private readonly Camera camera;
    private readonly RenderTexture target;
    private readonly Material surface,foregroundMaterial,tongueMaterial;
    private readonly FrogCatchCinematicEnvironment scenery;
    private readonly List<Body> capturedBodies=new List<Body>(),bystanderBodies=new List<Body>();
    private readonly List<Tongue> tongues=new List<Tongue>(),tongueCuffs=new List<Tongue>();
    private readonly ParticleSystem dust;
    private readonly ParticleSystem.Particle[] dustPixels=new ParticleSystem.Particle[144];
    private readonly Tongue lensTongue;
    private Body sourceBody,sourcePrevious,foreground;
    private Sprite[] poses;
    private readonly Dictionary<string,Sprite[]> speciesPoses=new Dictionary<string,Sprite[]>();
    private readonly List<Sprite> ownedPoses=new List<Sprite>();
    private bool disposed,tongueTextureLoaded;
    private float eyeDistance;
    private Vector3 away,right,mouth;
    private Texture2D background;
    private Actor source;
    private Actor[] captured=Array.Empty<Actor>(),bystanders=Array.Empty<Actor>();

    public Texture Texture=>target;
    public Texture2D Background=>background;
    public Texture2D SceneryKit=>scenery.Kit;
    public string SceneryRegion=>scenery.Region;
    public Actor Source=>source;
    public IReadOnlyList<Actor> Captured=>captured;
    public IReadOnlyList<Actor> Bystanders=>bystanders;
    public bool Active=>!disposed&&camera!=null&&camera.enabled;
    public int Shot {get;private set;}

    public void Warmup(string species="golgaebi")
    {
        if(disposed)return;
        LoadTongueTexture();
        Sprite[] frames=LoadPoses(species);if(frames==null)return;
        foreach(Sprite frame in frames)PixelMantisReaper.VisibleBody(frame);
    }

    public FrogCatchCinematicStage(Transform parent)
    {
        root=new GameObject("Frog captive-sky private stage") {hideFlags=HideFlags.DontSave,layer=Layer};
        root.transform.SetParent(parent,false);root.transform.position=new Vector3(21000f,18000f,21000f);
        var view=Child("Private cinematic perspective camera");camera=view.AddComponent<Camera>();
        camera.enabled=false;camera.orthographic=false;camera.nearClipPlane=.03f;camera.farClipPlane=80f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.045f,1f);
        camera.cullingMask=1<<Layer;camera.depth=-100f;camera.allowHDR=false;camera.allowMSAA=false;
        int height=Mathf.Clamp(Screen.height,540,900),width=Mathf.Clamp(Mathf.RoundToInt(height*Screen.width/(float)Mathf.Max(1,Screen.height)),540,1600);
        target=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32)
            {name="Frog captive-sky cinematic",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        target.Create();camera.targetTexture=target;camera.aspect=width/(float)height;
        var shader=Resources.Load<Shader>(ResourceRoot+"StageSurface")??Shader.Find("Sprites/Default");
        surface=new Material(shader){name="Frog stage actors",hideFlags=HideFlags.DontSave};
        tongueMaterial=new Material(surface){name="Painted pixel frog tongue",hideFlags=HideFlags.DontSave};
        foregroundMaterial=new Material(surface){name="Actual enemy foreground body crop",hideFlags=HideFlags.DontSave};
        scenery=new FrogCatchCinematicEnvironment(root.transform);
        sourceBody=CreateBody(source,"Frog cinematic source");sourcePrevious=CreateBody(source,"Frog cinematic pose bridge");
        foreground=CreateBody(source,"Actual enemy lens wipe");
        foreground.renderer.sharedMaterial=foregroundMaterial;
        lensTongue=CreateTongue("Tongue wrapped around the lens",180);
        var air=Child("Frog strength dust and tongue glints");dust=air.AddComponent<ParticleSystem>();
        dust.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=dust.main;main.playOnAwake=false;main.maxParticles=dustPixels.Length;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.startSpeed=0f;main.startSize3D=true;var emission=dust.emission;emission.enabled=false;
        var shape=dust.shape;shape.enabled=false;
        var particles=air.GetComponent<ParticleSystemRenderer>();particles.sharedMaterial=surface;particles.renderMode=ParticleSystemRenderMode.Billboard;
        particles.sortingOrder=130;particles.shadowCastingMode=ShadowCastingMode.Off;particles.receiveShadows=false;
        dust.Play(false);dust.Pause(false);
        root.SetActive(false);
    }

    public bool Begin(Texture2D background,Actor source,IReadOnlyList<Actor> captured,IReadOnlyList<Actor> bystanders,string region="PrimordialMeadow")
    {
        if(disposed)return false;Hide();
        if(background==null||captured==null||captured.Count==0||Frame(source,0f)==null)return false;
        this.background=background;this.source=source;
        this.captured=Copy(captured);this.bystanders=Copy(bystanders);
        if(this.captured.Length==0)return false;
        sourceBody.actor=sourcePrevious.actor=source;sourceBody.ground=Vector3.zero;
        foreground.actor=this.captured[0];
        EnsureBodies(capturedBodies,this.captured,"Captured cinematic enemy");EnsureBodies(bystanderBodies,this.bystanders,"Bystander cinematic actor");
        Vector3 mean=Vector3.zero;for(int i=0;i<this.captured.Length;i++)mean+=capturedBodies[i].ground;
        away=mean/Mathf.Max(1,this.captured.Length);away.y=0f;
        if(away.sqrMagnitude<.1f)away=new Vector3(source.flipX?-1f:1f,0f,-1f);
        away.Normalize();right=Vector3.Cross(Vector3.up,away).normalized;
        if(!LoadTongueTexture()||!scenery.Begin(region,away,source.ground))return false;
        camera.backgroundColor=scenery.SkyColor;
        eyeDistance=6.2f;
        for(int i=0;i<this.captured.Length;i++)eyeDistance=Mathf.Max(eyeDistance,Vector3.Dot(capturedBodies[i].ground,away)+3f);
        ArrangeSuspendedBodies();
        poses=source.shiny?null:LoadPoses(source.species);
        root.SetActive(true);camera.enabled=true;dust.Play(false);dust.Pause(false);Tick(0f,0f);return true;
    }

    public void Tick(float elapsed,float dt)
    {
        if(!Active)return;
        float t=Mathf.Clamp(elapsed,0f,Duration);
        Shot=t<.85f?1:t<1.95f?2:t<3.05f?3:4;
        float lift=Ease(.15f,1.30f,t),charge=Ease(1.95f,3.05f,t),throwing=Ease(3.10f,3.85f,t);
        float top=Ease(.67f,1.02f,t)*(1f-Ease(1.77f,2.13f,t));
        float low=Ease(1.77f,2.13f,t)*(1f-Ease(3.02f,3.35f,t));
        Vector3 eye=away*(eyeDistance-Ease(0f,.7f,t)*.8f)+Vector3.up*1.05f;
        Vector3 overhead=away*.75f+right*.35f+Vector3.up*12.3f;
        Vector3 force=away*2.65f+right*.12f+Vector3.up*.20f;
        Vector3 exit=away*6.4f+Vector3.up*1.8f;
        Vector3 position=Vector3.Lerp(eye,overhead,top);position=Vector3.Lerp(position,force,low);
        position=Vector3.Lerp(position,exit,Ease(3.02f,3.35f,t));
        Quaternion rotation=Quaternion.Slerp(Quaternion.LookRotation(Vector3.up*.75f-eye,Vector3.up),
            Quaternion.LookRotation(away*.6f+Vector3.up*1.6f-overhead,-away),top);
        rotation=Quaternion.Slerp(rotation,Quaternion.LookRotation(Vector3.up*.95f-force,Vector3.up),low);
        rotation=Quaternion.Slerp(rotation,Quaternion.LookRotation(Vector3.up*.75f-exit,Vector3.up),Ease(3.02f,3.35f,t));
        float strain=Mathf.Sin(charge*Mathf.PI),shake=low*strain*.024f;
        position+=right*Mathf.Sin(t*72f)*shake+Vector3.up*Mathf.Sin(t*93f)*shake*.6f;
        camera.transform.localPosition=position;camera.transform.localRotation=rotation*Quaternion.Euler(0f,0f,-9f*top+Mathf.Sin(t*37f)*low*.35f);
        camera.fieldOfView=Mathf.Lerp(54f,62f,Mathf.Max(top,low));
        int pose=t<1.95f?0:t<2.38f?1:t<3.05f?2:3;
        Sprite sourceFrame=SourceFrame(pose),previousFrame=SourceFrame(Mathf.Max(0,pose-1));
        float brace=low*(.08f+.065f*Mathf.Sin(charge*Mathf.PI*3f)),release=Mathf.Sin(throwing*Mathf.PI);
        sourceBody.position=Vector3.up*(-brace*.35f)+right*(-release*.13f);
        DrawBody(sourceBody,sourceFrame,1.55f*(1f+low*.45f),new Vector2(1f+brace,1f-brace),
            low*Mathf.Sin(t*44f)*strain*1.2f-release*(source.flipX?-10f:10f),t<.85f?150:40,poses==null?0f:2f*(1f+low*.45f));
        float poseStart=pose==1?1.95f:pose==2?2.38f:3.05f;
        float poseBlend=pose==0||sourceFrame==previousFrame?1f:Ease(poseStart,poseStart+.13f,t);
        sourcePrevious.renderer.enabled=poseBlend<1f;
        if(poseBlend<1f)
        {
            sourcePrevious.position=sourceBody.position;
            DrawBody(sourcePrevious,previousFrame,1.55f,new Vector2(1f+brace,1f-brace),0f,41,poses==null?0f:2f*(1f+low*.45f));
            sourcePrevious.renderer.color=Tint(source.color,1f-poseBlend);sourceBody.renderer.color=Tint(source.color,poseBlend);
            sourceBody.height=Mathf.Lerp(sourcePrevious.height,sourceBody.height,poseBlend);
        }
        mouth=SourceMouth(sourceBody,pose);
        if(poseBlend<1f)mouth=Vector3.Lerp(SourceMouth(sourcePrevious,pose-1),mouth,poseBlend);
        for(int i=0;i<bystanders.Length;i++)
        {
            Body body=bystanderBodies[i];
            body.position=body.ground;DrawBody(body,Frame(body.actor,t*.7f),1.4f,Vector2.one,0f,10);
        }
        for(int i=0;i<captured.Length;i++)
        {
            Body body=capturedBodies[i];
            Vector3 held=Vector3.Lerp(body.ground,body.suspended,lift);
            float height=lift*(3.4f+Hash(i+7)*.8f)*(1f-throwing*.65f);
            body.position=held+Vector3.up*(height+Mathf.Sin(t*8f+i*1.9f)*lift*.12f)+body.scatter*throwing*5.5f;
            float wriggle=lift*(Mathf.Sin(t*13f+i*2.1f)*7f+Mathf.Sin(t*6.3f+i)*3f);
            DrawBody(body,Frame(body.actor,t*(1.1f+Hash(i)*.3f)),1.4f,new Vector2(1f,1f),wriggle+throwing*Vector3.Dot(body.scatter,right)*25f,60+i);
            float reveal=Ease(.58f,.85f,t);
            body.renderer.color=Tint(body.actor.color,reveal);
            body.renderer.enabled=reveal>.001f&&!(i==0&&t>3.22f);
            Vector3 wrapCenter=BodyPoint(body,new Vector2(.5f,.52f));
            float wrapWidth=PixelMantisReaper.VisibleBody(body.renderer.sprite).size.x*Mathf.Abs(body.renderer.transform.localScale.x)*.35f;
            Quaternion wrapFacing=body.renderer.transform.localRotation;
            Vector3 end=wrapCenter+wrapFacing*new Vector3(Mathf.Cos(-.15f*Mathf.PI)*wrapWidth,Mathf.Sin(-.15f*Mathf.PI)*body.height*.12f,0f);
            Vector3 control=Vector3.Lerp(mouth,end,.5f)+Vector3.up*(.35f+Mathf.Sin(t*8f+i)*.12f)*(1f-charge);
            DrawTongue(tongues[i],mouth,control,end,.065f+strain*.027f,1f-throwing*.7f,t+i);
            DrawTongueWrap(tongueCuffs[i],wrapCenter,wrapFacing,wrapWidth,body.height*.12f,.032f,
                body.renderer.enabled?reveal*(1f-throwing*.7f):0f);
        }
        DrawForeground(t,throwing);
        DrawDust(t,low,strain,throwing);
    }

    private void DrawForeground(float t,float throwing)
    {
        float opening=1f-Ease(.12f,.58f,t),rushing=Ease(3.25f,3.90f,t),cover=Ease(3.90f,4f,t);
        foreground.renderer.enabled=opening>.001f||rushing>.001f;
        if(foreground.renderer.enabled)
        {
            Sprite frame=Frame(foreground.actor,t*1.8f);Bounds bounds=PixelMantisReaper.VisibleBody(frame);
            float size=Mathf.Clamp(foreground.actor.size>0f?foreground.actor.size:1f,.45f,2.8f);
            float distance=t<1f?2f:Mathf.Lerp(Mathf.Lerp(3.2f,1.4f,rushing),.35f,cover);
            float viewHeight=2f*distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad),viewWidth=viewHeight*camera.aspect;
            float fraction=t<1f?.72f:Mathf.Lerp(Mathf.Lerp(.16f,.64f,rushing),7f,cover);
            float height=viewHeight*fraction,roll=t<1f?0f:Mathf.Lerp(-9f,14f,rushing);
            Vector3 offset;
            if(t<1f)
            {
                // Show the real enemy's paws/lower body at the bottom edge, rather than its enormous torso.
                offset=new Vector3(-viewWidth*.20f,-viewHeight*.48f,0f);
                foregroundMaterial.SetVector("_BodyClip",new Vector4(0f,bounds.min.y+bounds.size.y*.30f,bounds.size.y*.035f,1f));
            }
            else
            {
                foregroundMaterial.SetVector("_BodyClip",Vector4.zero);
                Vector2 face=Vector2.Lerp(new Vector2(.19f,.28f),new Vector2(.25f,.55f),rushing);
                face=Vector2.Lerp(face,new Vector2(.5f,.5f),cover);
                float faceX=foreground.actor.flipX?.28f:.72f;
                Vector3 faceFromFeet=Quaternion.Euler(0f,0f,roll)*new Vector3((faceX-.5f)*height*bounds.size.x/Mathf.Max(.02f,bounds.size.y),height*.72f,0f);
                offset=new Vector3((face.x-.5f)*viewWidth,(face.y-.5f)*viewHeight,0f)-faceFromFeet;
            }
            foreground.position=camera.transform.localPosition+camera.transform.localRotation*(Vector3.forward*distance+offset);
            DrawBody(foreground,frame,height/size,Vector2.one,roll,220);
            if(t<1f)foreground.renderer.color=Tint(foreground.actor.color,opening);
        }
        float wrap=Ease(.05f,.19f,t)*(1f-Ease(.36f,.60f,t));
        lensTongue.renderer.enabled=wrap>.001f;
        if(wrap>.001f)
        {
            Quaternion rotation=camera.transform.localRotation;Vector3 position=camera.transform.localPosition;
            float height=2f*.8f*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad),width=height*camera.aspect;
            float sweep=Ease(.05f,.6f,t);
            Vector3 a=position+rotation*new Vector3(-width*.57f,height*.16f,.8f);
            Vector3 b=position+rotation*new Vector3(-width*.47f+width*.13f*sweep,height*.67f,.8f);
            Vector3 c=position+rotation*new Vector3(-width*.13f+width*.18f*sweep,height*.57f,.8f);
            DrawTongue(lensTongue,a,b,c,height*.026f,wrap,t*2f);
        }
    }

    private Vector3 BodyPoint(Body body,Vector2 point)
    {
        Bounds bounds=PixelMantisReaper.VisibleBody(body.renderer.sprite);
        Vector3 local=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,point.x),Mathf.Lerp(bounds.min.y,bounds.max.y,point.y),0f);
        return BodyLocalPoint(body,local);
    }
    private Vector3 SourceMouth(Body body,int pose)
    {
        Vector2[] points=source.species=="golgaebi"?GolgaebiMouths:source.species=="kingpa"?KingpaMouths:source.species=="rokrok"?RokrokMouths:source.species=="frosk"?FroskMouths:null;
        Vector2 point;
        if(poses==null)
        {
            if(!source.shiny||source.species!="frosk")return BodyPoint(body,new Vector2(.9f,.65f));
            point=(new Vector2(159,128)+Vector2.one*.5f)/256f;
        }
        else
        {
            if(points==null)return BodyPoint(body,new Vector2(.9f,.65f));
            point=(points[pose]+Vector2.one*.5f)/627f;
        }
        Sprite sprite=body.renderer.sprite;
        return BodyLocalPoint(body,new Vector3((point.x*sprite.rect.width-sprite.pivot.x)/sprite.pixelsPerUnit,
            ((1f-point.y)*sprite.rect.height-sprite.pivot.y)/sprite.pixelsPerUnit,0f));
    }
    private Vector3 BodyLocalPoint(Body body,Vector3 local)
    {
        if(body.actor.flipX)local.x=-local.x;
        Transform image=body.renderer.transform;
        return image.localPosition+image.localRotation*Vector3.Scale(local,image.localScale);
    }

    private void ArrangeSuspendedBodies()
    {
        // Keep the real angular ordering. Spread nearby catches around a broad ellipse so eight identities stay legible.
        int count=captured.Length;var order=new List<int>(count);var angles=new float[count];
        for(int i=0;i<count;i++)
        {order.Add(i);angles[i]=Mathf.Atan2(Vector3.Dot(capturedBodies[i].ground,right),Vector3.Dot(capturedBodies[i].ground,away));}
        order.Sort((a,b)=>angles[a]==angles[b]?a.CompareTo(b):angles[a].CompareTo(angles[b]));
        float step=2f*Mathf.PI/count,phase=0f;
        for(int rank=0;rank<count;rank++)phase+=angles[order[rank]]-rank*step;
        phase/=count;
        float width=Mathf.Min(6.2f,2.7f+count*.45f),depth=Mathf.Min(3.2f,1.6f+count*.2f);
        for(int rank=0;rank<count;rank++)
        {
            Body body=capturedBodies[order[rank]];float angle=phase+rank*step;
            Vector3 direction=right*Mathf.Sin(angle)+away*Mathf.Cos(angle);
            body.suspended=count==1?away*.7f:right*(Mathf.Sin(angle)*width)+away*(.6f+Mathf.Cos(angle)*depth);
            body.scatter=direction.normalized;
        }
    }

    private void DrawBody(Body body,Sprite sprite,float height,Vector2 stretch,float roll,int order,float width=0f)
    {
        if(sprite==null){body.renderer.enabled=false;return;}
        Bounds bounds=PixelMantisReaper.VisibleBody(sprite);
        float scale=height*Mathf.Clamp(body.actor.size>0f?body.actor.size:1f,.45f,2.8f)/Mathf.Max(.02f,bounds.size.y);
        if(width>0f)scale=width*Mathf.Clamp(body.actor.size>0f?body.actor.size:1f,.45f,2.8f)/Mathf.Max(.02f,bounds.size.x);
        body.height=bounds.size.y*scale*stretch.y;
        body.renderer.enabled=true;body.renderer.sprite=sprite;body.renderer.flipX=body.actor.flipX;
        body.renderer.color=Tint(body.actor.color,1f);body.renderer.sortingOrder=order;
        Quaternion facing=camera.transform.localRotation*Quaternion.Euler(0f,0f,roll);
        Vector3 pivot=new Vector3((body.actor.flipX?1f:-1f)*bounds.center.x,-bounds.min.y,0f);
        body.renderer.transform.localPosition=body.position+facing*Vector3.Scale(pivot,new Vector3(scale*stretch.x,scale*stretch.y,scale));
        body.renderer.transform.localRotation=facing;body.renderer.transform.localScale=new Vector3(scale*stretch.x,scale*stretch.y,scale);
    }

    private void DrawTongue(Tongue tongue,Vector3 start,Vector3 control,Vector3 end,float width,float opacity,float t)
    {
        tongue.renderer.enabled=opacity>.001f;
        if(!tongue.renderer.enabled)return;
        for(int i=0;i<=32;i++)
        {
            float p=i/32f,q=1f-p;
            Vector3 point=start*(q*q)+control*(2f*q*p)+end*(p*p);
            Vector3 tangent=Vector3.Lerp(control-start,end-control,p);
            Vector3 side=Vector3.Cross(tangent,camera.transform.localRotation*Vector3.forward).normalized;
            point+=side*Mathf.Sin(p*9f-t*5.2f)*Mathf.Sin(p*Mathf.PI)*.055f;
            float w=width*Mathf.Lerp(.22f,1f,Mathf.SmoothStep(0f,1f,Mathf.Clamp01(p/.18f)))*
                (1f+Mathf.SmoothStep(0f,1f,Mathf.Clamp01((p-.88f)/.12f))*.45f);
            for(int band=0;band<4;band++)
            {
                int index=i*4+band;float edge=band/3f*2f-1f;
                tongue.vertices[index]=point+side*w*edge;
                tongue.colors[index]=new Color(1f,1f,1f,opacity);
            }
        }
        tongue.mesh.vertices=tongue.vertices;tongue.mesh.colors=tongue.colors;tongue.mesh.RecalculateBounds();
    }

    private void DrawTongueWrap(Tongue tongue,Vector3 center,Quaternion facing,float radius,float height,float width,float opacity)
    {
        tongue.renderer.enabled=opacity>.001f;if(!tongue.renderer.enabled)return;
        for(int i=0;i<=32;i++)
        {
            float angle=Mathf.Lerp(-.15f*Mathf.PI,1.25f*Mathf.PI,i/32f);
            Vector3 point=center+facing*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*height,0f);
            Vector3 normal=facing*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f);
            for(int band=0;band<4;band++)
            {
                int index=i*4+band;
                tongue.vertices[index]=point+normal*width*(band/3f*2f-1f);
                tongue.colors[index]=new Color(1f,1f,1f,opacity);
            }
        }
        tongue.mesh.vertices=tongue.vertices;tongue.mesh.colors=tongue.colors;tongue.mesh.RecalculateBounds();
    }

    private bool LoadTongueTexture()
    {
        if(tongueTextureLoaded)return true;
        Texture2D texture=Resources.Load<Texture2D>(ResourceRoot+"TongueRibbonV2");if(texture==null)return false;
        var sprite=Sprite.Create(texture,new Rect(0f,0f,texture.width,texture.height),Vector2.zero,1f,0,SpriteMeshType.FullRect);
        sprite.hideFlags=HideFlags.DontSave;ownedPoses.Add(sprite);
        Bounds visible=PixelMantisReaper.VisibleBody(sprite);
        tongueMaterial.mainTexture=texture;
        tongueMaterial.SetVector("_UvRect",new Vector4(visible.min.x/texture.width,visible.min.y/texture.height,
            visible.size.x/texture.width,visible.size.y/texture.height));
        tongueTextureLoaded=true;
        return true;
    }

    private void DrawDust(float t,float low,float strain,float throwing)
    {
        int count=0;
        Color earth;
        switch(scenery.Region)
        {
            case "PoisonFogSwamp":earth=new Color(.30f,.38f,.17f);break;
            case "CrimsonJungle":earth=new Color(.55f,.17f,.12f);break;
            case "FrozenHighlands":earth=new Color(.66f,.81f,.95f);break;
            case "SkySanctuary":earth=new Color(.82f,.75f,.55f);break;
            default:earth=new Color(.42f,.32f,.2f);break;
        }
        for(int i=0;i<96;i++)
        {
            float seed=Hash(i),phase=Mathf.Repeat(t*(.6f+seed*.6f)+seed,1f),angle=i*2.39996f;
            float force=low*strain+throwing*.5f;
            Vector3 position=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*(.65f+phase*(1f+force*2f));
            position.y=.02f+Mathf.Sin(phase*Mathf.PI)*(.15f+force*.75f);
            Color color=i%4==0?new Color(1f,.55f,.68f):i%3==0?new Color(.95f,.78f,.49f):earth;
            color.a=(1f-phase)*Mathf.Clamp01(.2f+force);
            var pixel=new ParticleSystem.Particle{position=position,startColor=color,startSize=.0125f+seed*.035f,startLifetime=20f,remainingLifetime=20f};
            dustPixels[count++]=pixel;
        }
        dust.SetParticles(dustPixels,count);
    }

    private void EnsureBodies(List<Body> bodies,Actor[] actors,string name)
    {
        while(bodies.Count<actors.Length)bodies.Add(CreateBody(default,name));
        for(int i=0;i<bodies.Count;i++)
        {
            bodies[i].renderer.enabled=i<actors.Length;
            if(i>=actors.Length)continue;
            bodies[i].actor=actors[i];bodies[i].ground=new Vector3((actors[i].ground.x-source.ground.x)*ArenaHalfWidth,0f,(actors[i].ground.y-source.ground.y)*ArenaHalfDepth);
        }
        for(int i=0;i<captured.Length;i++)
            if(tongues.Count<=i)
            {
                tongues.Add(CreateTongue("Captured enemy pink branch tongue",100+i));
                tongueCuffs.Add(CreateTongue("Sticky tongue wrap around captured enemy",110+i));
            }
        for(int i=0;i<tongues.Count;i++)tongues[i].renderer.enabled=tongueCuffs[i].renderer.enabled=i<captured.Length;
    }

    private Body CreateBody(Actor actor,string name)
    {
        var renderer=Child(name).AddComponent<SpriteRenderer>();renderer.sharedMaterial=surface;renderer.enabled=false;
        return new Body{actor=actor,renderer=renderer};
    }
    private Tongue CreateTongue(string name,int order)
    {
        var tongue=new Tongue{mesh=new Mesh{name=name+" ribbon",hideFlags=HideFlags.DontSave}};
        var uv=new Vector2[33*4];var triangles=new int[32*3*6];
        for(int i=0;i<=32;i++)for(int band=0;band<4;band++)uv[i*4+band]=new Vector2(i/32f,band/3f);
        for(int i=0;i<32;i++)for(int band=0;band<3;band++)
        {
            int index=(i*3+band)*6,a=i*4+band,b=a+4;
            triangles[index]=a;triangles[index+1]=b;triangles[index+2]=a+1;
            triangles[index+3]=a+1;triangles[index+4]=b;triangles[index+5]=b+1;
        }
        tongue.mesh.MarkDynamic();tongue.mesh.vertices=tongue.vertices;tongue.mesh.uv=uv;tongue.mesh.triangles=triangles;
        tongue.renderer=MeshObject(name,tongue.mesh,tongueMaterial,order);tongue.renderer.enabled=false;return tongue;
    }
    private MeshRenderer MeshObject(string name,Mesh mesh,Material material,int order)
    {
        var host=Child(name);host.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=host.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=material;renderer.sortingOrder=order;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
    }
    private GameObject Child(string name)
    {var host=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};host.transform.SetParent(root.transform,false);return host;}
    public static string PoseResource(string species)
    {
        switch(species)
        {
            case "golgaebi":return ResourceRoot+"GolgaebiPoses";
            case "kingpa":return ResourceRoot+"KingpaPoses";
            case "rokrok":return ResourceRoot+"RokrokPoses";
            case "frosk":return ResourceRoot+"FroskPoses";
            default:return null;
        }
    }
    private Sprite[] LoadPoses(string species)
    {
        string resource=PoseResource(species);if(resource==null)return null;
        Sprite[] cached;if(speciesPoses.TryGetValue(species,out cached))return cached;
        Texture2D atlas=Resources.Load<Texture2D>(resource);if(atlas==null)return null;
        var result=new Sprite[4];int width=atlas.width/2,height=atlas.height/2;
        Vector2[] feet={new Vector2(.548644f,.089314f),new Vector2(.501595f,.060606f),new Vector2(.531898f,.256778f),new Vector2(.501595f,.256778f)};
        for(int i=0;i<4;i++)
        {
            var sprite=Sprite.Create(atlas,new Rect(i%2*width,(1-i/2)*height,width,height),species=="golgaebi"?feet[i]:new Vector2(.5f,.18f),128f,0,SpriteMeshType.FullRect);
            sprite.name=species+" captive-sky pose "+i;sprite.hideFlags=HideFlags.DontSave;ownedPoses.Add(sprite);result[i]=sprite;
        }
        speciesPoses.Add(species,result);return result;
    }
    private Sprite SourceFrame(int pose)
    {
        if(poses!=null)return poses[pose];
        // Preserve an unretouched/shiny body without replaying its entire combat loop during a held catch.
        if(source.frames!=null&&source.frames.Length>0)
        {
            int frame=Mathf.Min(3,source.frames.Length-1);
            if(source.frames[frame]!=null)return source.frames[frame];
        }
        return source.sprite;
    }
    private static Actor[] Copy(IReadOnlyList<Actor> actors)
    {
        if(actors==null)return Array.Empty<Actor>();var result=new Actor[actors.Count];
        for(int i=0;i<actors.Count;i++)result[i]=actors[i];return result;
    }
    private static Sprite Frame(Actor actor,float time)
    {
        if(actor.frames!=null&&actor.frames.Length>0)
        {Sprite frame=actor.frames[Mathf.FloorToInt(time*8f)%actor.frames.Length];if(frame!=null)return frame;}
        return actor.sprite;
    }
    private static Color Tint(Color color,float alpha)
    {if(color.a<=0f)color=Color.white;color.a*=alpha;return color;}
    private static float Ease(float from,float to,float time)=>Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(from,to,time));
    private static float Hash(int index)=>Mathf.Repeat(Mathf.Sin(index*127.1f+13.7f)*43758.547f,1f);

    public void Hide()
    {
        if(disposed)return;camera.enabled=false;scenery.Hide();root.SetActive(false);dust.Clear(false);Shot=0;
        foreach(var body in capturedBodies)body.renderer.enabled=false;foreach(var body in bystanderBodies)body.renderer.enabled=false;
        foreach(var tongue in tongues)tongue.renderer.enabled=false;foreach(var tongue in tongueCuffs)tongue.renderer.enabled=false;lensTongue.renderer.enabled=false;
        sourceBody.renderer.enabled=sourcePrevious.renderer.enabled=foreground.renderer.enabled=false;
    }
    public void Dispose()
    {
        if(disposed)return;Hide();disposed=true;camera.targetTexture=null;
        if(RenderTexture.active==target)RenderTexture.active=null;
        target.Release();Destroy(target);Destroy(surface);Destroy(tongueMaterial);Destroy(foregroundMaterial);scenery.Dispose();
        foreach(var tongue in tongues)Destroy(tongue.mesh);foreach(var tongue in tongueCuffs)Destroy(tongue.mesh);
        Destroy(lensTongue.mesh);foreach(var sprite in ownedPoses)Destroy(sprite);Destroy(root);
    }
    private static void Destroy(Object value)
    {if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
