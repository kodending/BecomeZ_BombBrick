using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using Object=UnityEngine.Object;

// Snapshots only. This camera renders the same bite continuously without owning gameplay or time.
public sealed class CrocodileAbyssDiveCinematicStage:IDisposable
{
    public const float Duration=4f;
    public const string ResourceRoot="CombatCinematics/CrocodileAbyssDive/";
    private const int Layer=30;
    private static readonly Vector2[] AgerMouths={new Vector2(.829571f,.730248f),new Vector2(.897523f,.688488f),new Vector2(.779910f,.839729f),new Vector2(.807432f,.742664f),new Vector2(.838600f,.611486f),new Vector2(.699324f,.427928f),new Vector2(.843115f,.231982f),new Vector2(.811937f,.708333f)};
    private static readonly Vector2[] KimbulMouths={new Vector2(.779910f,.729120f),new Vector2(.850225f,.624153f),new Vector2(.750564f,.778781f),new Vector2(.717342f,.688488f),new Vector2(.836343f,.561937f),new Vector2(.692568f,.377252f),new Vector2(.725734f,.309685f),new Vector2(.766892f,.690315f)};
    private static readonly Vector2[] KrogalMouths={new Vector2(.766366f,.682844f),new Vector2(.845721f,.583521f),new Vector2(.703160f,.757336f),new Vector2(.753378f,.602709f),new Vector2(.820542f,.619369f),new Vector2(.674550f,.387387f),new Vector2(.737020f,.343468f),new Vector2(.744369f,.643018f)};
    private static readonly Vector2[] TarvilMouths={new Vector2(.791196f,.716704f),new Vector2(.895270f,.650113f),new Vector2(.809255f,.773138f),new Vector2(.805180f,.696388f),new Vector2(.856659f,.652027f),new Vector2(.667793f,.393018f),new Vector2(.716704f,.217342f),new Vector2(.789414f,.697072f)};
    private static readonly int[] RollFrames={3,4,5,2},OriginalFrames={0,0,2,3,4,2,0,1};
    private sealed class Body
    {
        public FrogCatchCinematicStage.Actor actor;
        public SpriteRenderer renderer;
        public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        public bool deform;
    }
    private readonly GameObject root;
    private readonly Camera camera;
    private readonly RenderTexture texture;
    private readonly Material actorMaterial,backgroundMaterial;
    private readonly FrogCatchCinematicEnvironment scenery;
    private readonly Body sourceBody,sourcePrevious,targetBody;
    private readonly List<Body> bystanderBodies=new List<Body>();
    private readonly Dictionary<string,Sprite[]> poseCache=new Dictionary<string,Sprite[]>();
    private readonly List<Sprite> ownedSprites=new List<Sprite>();
    private readonly Dictionary<Sprite,Sprite> authoredCopies=new Dictionary<Sprite,Sprite>();
    private readonly SpriteRenderer underwater;
    private Sprite[] poses;
    private PixelCrocodileAbyssDive water;
    private FrogCatchCinematicStage.Actor source,target;
    private FrogCatchCinematicStage.Actor[] bystanders=Array.Empty<FrogCatchCinematicStage.Actor>();
    private Vector3 mouth,away,referenceJawOffset;
    private bool disposed,painted,hasReturnView;
    private Vector2 returnFoot;
    private float returnHeight,drawTime,sourceReferenceHeight,sourceReferenceWidth,shakeScale=1f;
    private string region;
    public Texture Texture=>texture;
    public bool Active=>!disposed&&camera!=null&&camera.enabled;
    public int Shot{get;private set;}
    public float RollDegrees{get;private set;}
    public Texture2D Background{get;private set;}
    public Texture2D UnderwaterTexture{get;private set;}
    public string SceneryRegion=>scenery.Region;
    public Texture2D SceneryKit=>scenery.Kit;
    public FrogCatchCinematicStage.Actor Source=>source;
    public FrogCatchCinematicStage.Actor Target=>target;
    public IReadOnlyList<FrogCatchCinematicStage.Actor> Bystanders=>bystanders;
    public Rect SourceViewportRect=>ViewportRect(sourceBody);
    public Rect TargetViewportRect=>ViewportRect(targetBody);
    public Vector2 MouthViewport=>camera==null?Vector2.zero:(Vector2)camera.WorldToViewportPoint(root.transform.TransformPoint(mouth));

    public static string PoseResource(string species)
    {
        switch(species)
        {
            case "ager":return ResourceRoot+"AgerAction";
            case "kimbul":return ResourceRoot+"KimbulAction";
            case "krogal":return ResourceRoot+"KrogalAction";
            case "tarvil":return ResourceRoot+"TarvilAction";
            default:return null;
        }
    }

    public CrocodileAbyssDiveCinematicStage(Transform parent)
    {
        root=new GameObject("Crocodile abyss private stage"){layer=Layer,hideFlags=HideFlags.DontSave};
        root.transform.SetParent(parent,false);root.transform.position=new Vector3(27000f,19000f,27000f);
        var view=Child("Crocodile private cinematic camera");camera=view.AddComponent<Camera>();
        camera.enabled=false;camera.orthographic=false;camera.nearClipPlane=.03f;camera.farClipPlane=80f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.004f,.025f,.045f);
        camera.cullingMask=1<<Layer;camera.depth=-100f;camera.allowHDR=false;camera.allowMSAA=false;
        int height=Mathf.Clamp(Screen.height,540,900),width=Mathf.Clamp(Mathf.RoundToInt(height*Screen.width/(float)Mathf.Max(1,Screen.height)),540,1600);
        texture=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32)
            {name="Crocodile abyss cinematic",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        texture.Create();camera.targetTexture=texture;camera.aspect=width/(float)height;
        Shader shader=Resources.Load<Shader>(ResourceRoot+"AbyssSurface")??Shader.Find("Sprites/Default");
        actorMaterial=new Material(shader){name="Actual crocodile and prey sprites",hideFlags=HideFlags.DontSave};
        backgroundMaterial=new Material(shader){name="Deep-water backdrop",hideFlags=HideFlags.DontSave};
        // This is a full-view transition, not geometry behind the depth-writing terrain and horizon.
        backgroundMaterial.SetFloat("_ZTest",(float)UnityEngine.Rendering.CompareFunction.Always);
        scenery=new FrogCatchCinematicEnvironment(root.transform);
        sourceBody=CreateBody("Crocodile abyss source");sourcePrevious=CreateBody("Crocodile abyss pose bridge");targetBody=CreateBody("Actual abyss primary target");
        sourceBody.deform=sourcePrevious.deform=true;
        underwater=Child("Generated underwater depth").AddComponent<SpriteRenderer>();underwater.sharedMaterial=backgroundMaterial;underwater.sortingOrder=-200;
        root.SetActive(false);
    }

    public void Warmup(string species="ager")
    {
        if(disposed)return;
        Sprite[] frames=LoadPoses(species);
        if(frames!=null)foreach(Sprite frame in frames)PixelMantisReaper.VisibleBody(frame);
        LoadUnderwater();
    }

    public bool Begin(Texture2D background,FrogCatchCinematicStage.Actor source,FrogCatchCinematicStage.Actor target,
        IReadOnlyList<FrogCatchCinematicStage.Actor> bystanders,string region="PrimordialMeadow")
    {
        if(disposed)return false;Hide();
        if(background==null||ActualFrame(source,0)==null||ActualFrame(target,0)==null)return false;
        Background=background;this.source=source;this.target=target;this.region=region;
        this.bystanders=Copy(bystanders);sourceBody.actor=sourcePrevious.actor=source;targetBody.actor=target;
        poses=source.shiny?null:LoadPoses(source.species);painted=poses!=null;
        if(poses==null)poses=AuthoredPoses(source);
        Bounds reference=PixelMantisReaper.VisibleBody(poses[0]);
        sourceReferenceWidth=Mathf.Max(.02f,reference.size.x);
        sourceReferenceHeight=3.4f*reference.size.y/sourceReferenceWidth;
        Vector3 referenceAnchor=new Vector3(reference.center.x,Mathf.Lerp(reference.min.y,reference.max.y,.45f),0f);
        referenceJawOffset=(LocalMouthPoint(poses[0],0)-referenceAnchor)*(3.4f/sourceReferenceWidth);
        hasReturnView=false;shakeScale=BombBrick.Game.Core.GameUserSettings.Load().CameraShakeScale;
        LoadUnderwater();
        away=new Vector3(source.flipX?-1f:1f,0f,-.8f).normalized;
        if(!scenery.Begin(region,away,source.ground))return false;
        while(bystanderBodies.Count<this.bystanders.Length)bystanderBodies.Add(CreateBody("Actual abyss bystander"));
        for(int i=0;i<bystanderBodies.Count;i++)
        {bystanderBodies[i].renderer.enabled=i<this.bystanders.Length;if(i<this.bystanders.Length)bystanderBodies[i].actor=this.bystanders[i];}
        root.SetActive(true);camera.enabled=true;
        water=PixelCrocodileAbyssDive.Create(sourceBody.renderer,targetBody.renderer,root.transform.position,4.5f,true);
        if(water!=null)
        {water.transform.SetParent(root.transform,true);water.Particles.GetComponent<ParticleSystemRenderer>().sortingOrder=100;}
        Tick(0f,0f);return true;
    }

    public void Tick(float elapsed,float dt)
    {
        if(!Active)return;
        float t=Mathf.Clamp(elapsed,0f,Duration);drawTime=t;
        Shot=t<.7f?1:t<1.35f?2:t<2.8f?3:4;
        float dive=Ease(.7f,1.12f,t),rise=1f-Mathf.Pow(1f-Mathf.Clamp01((t-2.8f)/.5f),3f),depth=dive*(1f-rise);
        // Finish the water lens before the camera crosses the floor; reveal land only above it.
        float submerged=Ease(.68f,.88f,t)*(1f-Ease(3.18f,3.52f,t));
        float sizeDistance=Mathf.Max(0f,Mathf.Clamp(target.size>0f?target.size:1f,.4f,2.8f)-1f)*2.8f;
        Vector3 startView=away*(8.6f+sizeDistance)+Vector3.up*2.2f;
        Vector3 deepView=away*(7.6f+sizeDistance)+Vector3.up*(-2.55f);
        float turns=PixelCrocodileAbyssDive.CinematicRollTurns(t);RollDegrees=360f*turns;
        float rolling=Ease(1.28f,1.4f,t)*(1f-Ease(2.64f,2.8f,t));
        float turnAngle=turns*Mathf.PI*2f;
        float breach=Ease(2.8f,3.07f,t)*(1f-Ease(3.33f,3.85f,t));
        float pressure=Pulse(t,.25f,.11f)+Pulse(t,1.79f,.12f)+Pulse(t,2.49f,.12f)+Pulse(t,3.07f,.16f);
        Vector3 view=Vector3.Lerp(startView,deepView,depth);
        Vector3 look=Vector3.up*(.75f-depth*3.6f+breach*.75f)+Vector3.Cross(Vector3.up,away)*.4f;
        view+=Vector3.up*breach*.6f;
        view+=Vector3.Cross(Vector3.up,away)*(Mathf.Sin(t*83f)*pressure*.055f*shakeScale);
        camera.transform.localPosition=view;
        camera.transform.localRotation=Quaternion.LookRotation(look-view,Vector3.up);
        camera.fieldOfView=Mathf.Lerp(49f,52f,submerged)+breach*4f;
        camera.backgroundColor=Color.Lerp(scenery.SkyColor,new Color(.003f,.023f,.045f),submerged);
        bool land=t<.88f||t>=3.18f;
        if(!land&&scenery.Active)scenery.Hide();
        else if(land&&!scenery.Active)scenery.Begin(region,away,source.ground);
        actorMaterial.SetFloat("_Underwater",submerged);
        backgroundMaterial.SetVector("_Flow",new Vector4(t,rolling,breach,pressure*submerged));
        DrawUnderwater(submerged);

        int pose=FrameAt(t),previousPose=pose;float blend=1f;
        if(t>.04f&&FrameAt(t-.04f)!=pose)
        {
            float low=t-.04f,high=t;
            for(int i=0;i<7;i++){float middle=(low+high)*.5f;if(FrameAt(middle)==pose)high=middle;else low=middle;}
            previousPose=FrameAt(Mathf.Max(0f,low));blend=Ease(high,high+.04f,t);
        }
        Sprite frame=poses[pose],previous=poses[previousPose];if(previous==frame)blend=1f;
        float facing=source.flipX?-1f:1f;
        float roll=(Mathf.Sin(turnAngle)*18f*rolling-breach*9f+Pulse(t,.25f,.12f)*7f)*facing;
        float squeeze=Ease(3.88f,4f,t),coil=Ease(2.64f,2.78f,t)*(1f-Ease(2.8f,2.88f,t));
        float drive=Pulse(t,.21f,.17f)+Pulse(t,2.94f,.17f);
        float sourceWidth=3.4f;
        Vector2 stretch=new Vector2(1f+drive*.12f-coil*.12f+squeeze*.08f,
            1f-drive*.07f-coil*.11f-squeeze*.12f+Mathf.Sin(turnAngle)*rolling*.06f);
        Bounds frameBounds=PixelMantisReaper.VisibleBody(frame),previousBounds=PixelMantisReaper.VisibleBody(previous);
        float frameHeight=frameBounds.size.y/sourceReferenceWidth*sourceWidth*stretch.y;
        Vector3 side=camera.transform.localRotation*Vector3.right;
        float lunge=-1.05f*(1f-Ease(.10f,.26f,t))-.15f*Pulse(t,.29f,.13f);
        Vector3 bodyCenter=Vector3.up*(sourceReferenceHeight*.45f-depth*3.6f+breach*1.5f-coil*.12f);
        bodyCenter+=side*(facing*(lunge+Mathf.Sin(turnAngle)*rolling*.18f));
        float returnBlend=hasReturnView?Ease(3.55f,4f,t):0f;
        if(returnBlend>0f)
        {
            float distance=Vector3.Dot(root.transform.TransformPoint(bodyCenter)-camera.transform.position,camera.transform.forward);
            float desiredHeight=returnHeight*2f*distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            float desiredWidth=desiredHeight*sourceReferenceWidth/Mathf.Max(.02f,frameBounds.size.y)/Mathf.Max(.01f,stretch.y);
            sourceWidth=Mathf.Lerp(sourceWidth,desiredWidth,returnBlend);
            frameHeight=frameBounds.size.y/sourceReferenceWidth*sourceWidth*stretch.y;
            float halfHeight=distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            Vector3 foot=camera.transform.localPosition+camera.transform.localRotation*
                new Vector3((returnFoot.x-.5f)*2f*halfHeight*camera.aspect,(returnFoot.y-.5f)*2f*halfHeight,distance);
            bodyCenter=Vector3.Lerp(bodyCenter,foot+(camera.transform.localRotation*Vector3.up)*frameHeight*.45f,returnBlend);
        }
        DrawBody(sourceBody,frame,bodyCenter,sourceWidth*frameBounds.size.x/sourceReferenceWidth,0f,stretch,roll,70,new Vector2(.5f,.45f),blend);
        sourcePrevious.renderer.enabled=blend<.999f;
        if(sourcePrevious.renderer.enabled)
            DrawBody(sourcePrevious,previous,bodyCenter,sourceWidth*previousBounds.size.x/sourceReferenceWidth,0f,stretch,roll,71,new Vector2(.5f,.45f),1f-blend);
        Vector3 jawOffset=referenceJawOffset*(sourceWidth/3.4f);jawOffset.x*=facing;
        Vector3 grip=bodyCenter+camera.transform.localRotation*Quaternion.Euler(0f,0f,roll)*jawOffset;
        sourceBody.renderer.transform.localPosition+=(grip-SourceMouth(sourceBody,pose))*(1f-returnBlend);
        if(sourcePrevious.renderer.enabled)
            sourcePrevious.renderer.transform.localPosition+=(grip-SourceMouth(sourcePrevious,previousPose))*(1f-returnBlend);
        if(returnBlend>0f)
        {
            Rect actual=SourceViewportRect;
            float distance=Vector3.Dot(sourceBody.renderer.transform.position-camera.transform.position,camera.transform.forward);
            // Stay in private-stage coordinates; world unprojection loses precision this far from the origin.
            float halfHeight=distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            Vector3 shift=camera.transform.localRotation*new Vector3((returnFoot.x-actual.center.x)*2f*halfHeight*camera.aspect,
                (returnFoot.y-actual.yMin)*2f*halfHeight,0f)*returnBlend;
            sourceBody.renderer.transform.localPosition+=shift;sourcePrevious.renderer.transform.localPosition+=shift;
        }
        mouth=SourceMouth(sourceBody,pose);
        if(sourcePrevious.renderer.enabled)mouth=Vector3.Lerp(SourceMouth(sourcePrevious,previousPose),mouth,blend);
        float preyHeight=sourceReferenceHeight*Mathf.Clamp(target.size>0f?target.size:1f,.4f,2.8f)*(sourceWidth/3.4f);
        Vector3 targetPosition=mouth+side*facing*.8f*(1f-Ease(.10f,.25f,t));
        int preyFrame=target.frames==null||target.frames.Length==0?0:Mathf.FloorToInt(t*12f)%target.frames.Length;
        Vector2 preyStretch=new Vector2(1f+pressure*.08f,1f-pressure*.11f);
        DrawBody(targetBody,ActualFrame(target,preyFrame),targetPosition,0f,preyHeight,preyStretch,roll+Mathf.Sin(turnAngle)*rolling*6f,
            65,new Vector2(.5f,.58f),1f);

        for(int i=0;i<bystanderBodies.Count;i++)
        {
            Body body=bystanderBodies[i];body.renderer.enabled=land&&i<bystanders.Length;
            if(!body.renderer.enabled)continue;
            Vector3 position=new Vector3((body.actor.ground.x-source.ground.x)*11f,0f,(body.actor.ground.y-source.ground.y)*8f);
            DrawBody(body,ActualFrame(body.actor,0),position,0f,1.55f*Mathf.Clamp(body.actor.size>0f?body.actor.size:1f,.4f,2.8f),
                Vector2.one,0f,20,new Vector2(.5f,0f),1f);
        }
        if(water!=null){water.SetView(camera.transform.rotation);water.Advance(t,root.transform.position);}
    }

    public void SetReturnView(Vector2 foot,float height)
    {returnFoot=foot;returnHeight=Mathf.Clamp(height,.005f,.8f);hasReturnView=true;}

    private static int FrameAt(float t)
    {
        if(t<.13f)return 0;if(t<.64f)return 1;if(t<1.35f)return 2;
        if(t<2.64f)return RollFrames[Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(PixelCrocodileAbyssDive.CinematicRollTurns(t),1f)*4f),0,3)];
        if(t<2.8f)return 5;if(t<3.5f)return 6;if(t<3.88f)return 1;return 7;
    }

    private static float Pulse(float t,float at,float width)=>Mathf.Pow(Mathf.Max(0f,1f-Mathf.Abs(t-at)/width),2f);

    private void DrawUnderwater(float alpha)
    {
        underwater.enabled=alpha>.001f;if(!underwater.enabled)return;
        float distance=28f,height=2f*distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad),width=height*camera.aspect;
        Bounds bounds=underwater.sprite.bounds;float scale=Mathf.Max(width/bounds.size.x,height/bounds.size.y)*1.03f;
        // Keep the surface entrance at the top even when a wide viewport crops this 3:2 painting.
        float excessHeight=Mathf.Max(0f,bounds.size.y*scale-height);
        underwater.transform.localPosition=camera.transform.localPosition+camera.transform.localRotation*(Vector3.forward*distance-Vector3.up*excessHeight*.5f);
        underwater.transform.localRotation=camera.transform.localRotation;underwater.transform.localScale=Vector3.one*scale;
        underwater.color=UnderwaterTexture==null?new Color(.004f,.04f,.065f,alpha):new Color(1f,1f,1f,alpha);
    }
    private void DrawBody(Body body,Sprite sprite,Vector3 anchorPosition,float width,float height,Vector2 stretch,float roll,int order,Vector2 anchor,float alpha)
    {
        if(sprite==null){body.renderer.enabled=false;return;}
        Bounds bounds=PixelMantisReaper.VisibleBody(sprite);
        float scale=width>0f?width/Mathf.Max(.02f,bounds.size.x):height/Mathf.Max(.02f,bounds.size.y);
        Quaternion facing=camera.transform.localRotation*Quaternion.Euler(0f,0f,roll);
        Vector3 localAnchor=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,anchor.x),Mathf.Lerp(bounds.min.y,bounds.max.y,anchor.y),0f);
        if(body.actor.flipX)localAnchor.x=-localAnchor.x;
        var transform=body.renderer.transform;transform.localRotation=facing;transform.localScale=new Vector3(scale*stretch.x,scale*stretch.y,scale);
        transform.localPosition=anchorPosition-facing*Vector3.Scale(localAnchor,transform.localScale);
        body.renderer.sprite=sprite;body.renderer.flipX=body.actor.flipX;body.renderer.enabled=true;body.renderer.sortingOrder=order;
        Color color=body.actor.color;if(color.a<=0f)color=Color.white;color.a*=alpha;body.renderer.color=color;
        if(body.deform)
        {
            float underwater=Ease(1.28f,1.4f,drawTime)*(1f-Ease(2.65f,2.8f,drawTime));
            body.properties.SetTexture("_MainTex",sprite.texture);
            body.properties.SetVector("_BodySpan",new Vector4(bounds.min.x,bounds.max.x,0f,0f));
            body.properties.SetVector("_Flex",new Vector4(.012f+underwater*.026f,drawTime*22f,underwater*.012f,0f));
            body.renderer.SetPropertyBlock(body.properties);
        }
    }
    private Vector3 SourceMouth(Body body,int pose)
    {
        Vector3 point=LocalMouthPoint(body.renderer.sprite,pose);
        if(body.actor.flipX)point.x=-point.x;
        return body.renderer.transform.localPosition+body.renderer.transform.localRotation*Vector3.Scale(point,body.renderer.transform.localScale);
    }

    private Vector3 LocalMouthPoint(Sprite sprite,int pose)
    {
        Bounds bounds=PixelMantisReaper.VisibleBody(sprite);
        Vector3 point=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,.9f),Mathf.Lerp(bounds.min.y,bounds.max.y,.6f),0f);
        Vector2[] points=source.species=="ager"?AgerMouths:source.species=="kimbul"?KimbulMouths:
            source.species=="krogal"?KrogalMouths:source.species=="tarvil"?TarvilMouths:null;
        if(painted&&points!=null)
            point=new Vector3((points[pose].x*sprite.rect.width-sprite.pivot.x)/sprite.pixelsPerUnit,
                ((1f-points[pose].y)*sprite.rect.height-sprite.pivot.y)/sprite.pixelsPerUnit,0f);
        else
        {
            Vector2 pixel=OriginalMouth(source.species,pose);
            point=new Vector3((sprite.rect.width*pixel.x/256f-sprite.pivot.x)/sprite.pixelsPerUnit,
                (sprite.rect.height*(1f-pixel.y/256f)-sprite.pivot.y)/sprite.pixelsPerUnit,0f);
        }
        return point;
    }
    private Rect ViewportRect(Body body)
    {
        if(body.renderer==null||body.renderer.sprite==null)return Rect.zero;
        Bounds bounds=PixelMantisReaper.VisibleBody(body.renderer.sprite);float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        for(int i=0;i<4;i++)
        {
            Vector3 local=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,0f);
            if(body.renderer.flipX)local.x=-local.x;
            Vector3 point=camera.WorldToViewportPoint(body.renderer.transform.TransformPoint(local));
            minX=Mathf.Min(minX,point.x);minY=Mathf.Min(minY,point.y);maxX=Mathf.Max(maxX,point.x);maxY=Mathf.Max(maxY,point.y);
        }
        return Rect.MinMaxRect(minX,minY,maxX,maxY);
    }
    private void LoadUnderwater()
    {
        if(underwater.sprite!=null)return;
        UnderwaterTexture=Resources.Load<Texture2D>(ResourceRoot+"Underwater");
        Texture2D image=UnderwaterTexture!=null?UnderwaterTexture:Texture2D.whiteTexture;
        var sprite=Sprite.Create(image,new Rect(0f,0f,image.width,image.height),Vector2.one*.5f,128f,0,SpriteMeshType.FullRect);
        sprite.name="Abyss underwater backdrop";sprite.hideFlags=HideFlags.DontSave;ownedSprites.Add(sprite);underwater.sprite=sprite;
    }
    private Sprite[] LoadPoses(string species)
    {
        if(string.IsNullOrEmpty(species))return null;Sprite[] cached;if(poseCache.TryGetValue(species,out cached))return cached;
        string path=PoseResource(species);if(path==null)return null;
        Texture2D atlas=Resources.Load<Texture2D>(path);if(atlas==null)return null;
        var frames=new Sprite[8];
        for(int i=0;i<8;i++)
        {
            int left=Mathf.FloorToInt(i%4*atlas.width/4f),right=Mathf.FloorToInt((i%4+1)*atlas.width/4f);
            int bottom=Mathf.FloorToInt((1-i/4)*atlas.height/2f),top=Mathf.FloorToInt((2-i/4)*atlas.height/2f);
            var sprite=Sprite.Create(atlas,new Rect(left,bottom,right-left,top-bottom),new Vector2(.5f,.18f),128f,0,SpriteMeshType.FullRect);
            sprite.name=species+" abyss action "+i;sprite.hideFlags=HideFlags.DontSave;
            AddFlexibleGeometry(sprite);ownedSprites.Add(sprite);frames[i]=sprite;
        }
        poseCache.Add(species,frames);return frames;
    }

    private Sprite[] AuthoredPoses(FrogCatchCinematicStage.Actor actor)
    {
        var frames=new Sprite[8];bool animated=actor.frames!=null&&actor.frames.Length>=5;
        for(int i=0;i<frames.Length;i++)
        {
            Sprite original=ActualFrame(actor,animated?OriginalFrames[i]:0),copy;
            if(!authoredCopies.TryGetValue(original,out copy))
            {
                copy=Sprite.Create(original.texture,original.rect,new Vector2(original.pivot.x/original.rect.width,original.pivot.y/original.rect.height),original.pixelsPerUnit,0,SpriteMeshType.FullRect);
                copy.name=original.name+" abyss flexible copy";copy.hideFlags=HideFlags.DontSave;
                AddFlexibleGeometry(copy);ownedSprites.Add(copy);authoredCopies.Add(original,copy);
            }
            frames[i]=copy;
        }
        return frames;
    }

    private static void AddFlexibleGeometry(Sprite sprite)
    {
        const int columns=12,rows=6,count=(columns+1)*(rows+1);
        var vertices=new NativeArray<Vector3>(count,Allocator.Temp);
        var uv=new NativeArray<Vector2>(count,Allocator.Temp);
        var triangles=new NativeArray<ushort>(columns*rows*6,Allocator.Temp);
        try
        {
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                int index=y*(columns+1)+x;var pixel=new Vector2(sprite.rect.width*x/columns,sprite.rect.height*y/rows);
                vertices[index]=(pixel-sprite.pivot)/sprite.pixelsPerUnit;
                uv[index]=new Vector2((sprite.rect.x+pixel.x)/sprite.texture.width,(sprite.rect.y+pixel.y)/sprite.texture.height);
            }
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                int v=y*(columns+1)+x,t=(y*columns+x)*6;
                triangles[t]=(ushort)v;triangles[t+1]=(ushort)(v+1);triangles[t+2]=(ushort)(v+columns+1);
                triangles[t+3]=(ushort)(v+1);triangles[t+4]=(ushort)(v+columns+2);triangles[t+5]=(ushort)(v+columns+1);
            }
            // Private subdivided geometry must also support EditMode warmup.
            sprite.SetVertexCount(count);sprite.SetVertexAttribute(VertexAttribute.Position,vertices);
            sprite.SetVertexAttribute(VertexAttribute.TexCoord0,uv);sprite.SetIndices(triangles);
        }
        finally{vertices.Dispose();uv.Dispose();triangles.Dispose();}
    }

    private static Vector2 OriginalMouth(string species,int pose)
    {
        int index=OriginalFrames[pose];
        if(species=="ager"&&index>=2)return index==2?new Vector2(165,156):index==3?new Vector2(165,153):new Vector2(160,154);
        if(species=="kimbul"&&index>=2)return index==2?new Vector2(163,154):index==3?new Vector2(167,151):new Vector2(161,152);
        if(species=="krogal"&&index>=2)return index==2?new Vector2(160,146):index==3?new Vector2(169,146):new Vector2(161,148);
        if(index>=2)return new Vector2(165f,index==3?156f:166f);
        if(index==1)return species=="kimbul"?new Vector2(169,162):species=="krogal"?new Vector2(168,160):species=="tarvil"?new Vector2(168,166):new Vector2(163,165);
        return species=="kimbul"?new Vector2(160,146):species=="krogal"?new Vector2(166,144):species=="tarvil"?new Vector2(166,153):new Vector2(158,152);
    }
    private Body CreateBody(string name)
    {
        var renderer=Child(name).AddComponent<SpriteRenderer>();renderer.sharedMaterial=actorMaterial;renderer.enabled=false;
        return new Body{renderer=renderer};
    }
    private GameObject Child(string name)
    {var child=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};child.transform.SetParent(root.transform,false);return child;}
    private static FrogCatchCinematicStage.Actor[] Copy(IReadOnlyList<FrogCatchCinematicStage.Actor> actors)
    {
        if(actors==null)return Array.Empty<FrogCatchCinematicStage.Actor>();var result=new FrogCatchCinematicStage.Actor[actors.Count];
        for(int i=0;i<result.Length;i++)result[i]=actors[i];return result;
    }
    private static Sprite ActualFrame(FrogCatchCinematicStage.Actor actor,int index)
    {
        if(actor.frames!=null&&actor.frames.Length>0&&actor.frames[Mathf.Min(index,actor.frames.Length-1)]!=null)
            return actor.frames[Mathf.Min(index,actor.frames.Length-1)];
        return actor.sprite;
    }
    private static float Ease(float from,float to,float time)=>Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(from,to,time));
    public void Hide()
    {
        if(disposed)return;if(water!=null)water.Cancel(true);water=null;
        if(camera!=null)camera.enabled=false;scenery.Hide();if(root!=null)root.SetActive(false);Shot=0;RollDegrees=0f;
    }
    public void Dispose()
    {
        if(disposed)return;Hide();disposed=true;
        if(camera!=null)camera.targetTexture=null;
        if(RenderTexture.active==texture)RenderTexture.active=null;
        if(texture!=null){texture.Release();Destroy(texture);}scenery.Dispose();
        Destroy(actorMaterial);Destroy(backgroundMaterial);foreach(Sprite sprite in ownedSprites)Destroy(sprite);Destroy(root);
    }
    private static void Destroy(Object value)
    {if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
