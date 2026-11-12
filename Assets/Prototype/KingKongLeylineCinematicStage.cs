using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

// A snapshot stage only: the battle owns damage, its clock, and the following six-second aura.
public sealed class KingKongLeylineCinematicStage:IDisposable
{
    public const float Duration=3f;
    public const string ResourceRoot="CombatCinematics/KingKongLeyline/";
    private const int Layer=30;
    private static readonly float[] PoseTimes={0f,.55f,.69f,.85f,1.06f,1.25f,1.58f,2.30f};
    private static readonly int[] OriginalFrames={0,1,2,1,2,3,4,5};
    private sealed class Body
    {
        public FrogCatchCinematicStage.Actor actor;
        public SpriteRenderer renderer;
        public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
    }
    private readonly GameObject root;
    private readonly Camera camera;
    private readonly RenderTexture texture;
    private readonly Material material;
    private readonly FrogCatchCinematicEnvironment scenery;
    private readonly Body sourceBody,previousBody;
    private readonly Dictionary<string,Sprite[]> poseCache=new Dictionary<string,Sprite[]>();
    private readonly List<Sprite> ownedSprites=new List<Sprite>();
    private readonly Sprite[] fallbackPoses=new Sprite[8];
    private readonly PixelKingKongLeylineStage leyline;
    private FrogCatchCinematicStage.Actor source;
    private Sprite[] poses;
    private Vector3 away;
    private float referenceWidth,referenceHeight,shakeScale,returnHeight;
    private Vector2 returnFoot;
    private bool disposed,hasReturnView;
    public Texture Texture=>texture;
    public bool Active=>!disposed&&camera!=null&&camera.enabled;
    public FrogCatchCinematicStage.Actor Source=>source;
    public string SceneryRegion=>scenery.Region;
    public Texture2D SceneryKit=>scenery.Kit;
    public Texture2D Background{get;private set;}
    public Rect SourceViewportRect=>ViewportRect(sourceBody);
    public int Shot{get;private set;}

    public static string PoseResource(string species)
    {
        switch(species)
        {
            case "lilas":return ResourceRoot+"LilasAction";
            case "magril":return ResourceRoot+"MagrilAction";
            case "bingareu":return ResourceRoot+"BingareuAction";
            case "moska":return ResourceRoot+"MoskaAction";
            default:return null;
        }
    }

    public KingKongLeylineCinematicStage(Transform parent)
    {
        root=new GameObject("KingKong heart of leyline private stage"){layer=Layer,hideFlags=HideFlags.DontSave};
        root.transform.SetParent(parent,false);root.transform.position=new Vector3(29000f,19000f,29000f);
        camera=Child("Leyline private camera").AddComponent<Camera>();
        camera.enabled=false;camera.orthographic=false;camera.nearClipPlane=.03f;camera.farClipPlane=80f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.cullingMask=1<<Layer;camera.depth=-100f;
        camera.allowHDR=false;camera.allowMSAA=false;
        int height=Mathf.Clamp(Screen.height,540,900);
        int width=Mathf.Clamp(Mathf.RoundToInt(height*Screen.width/(float)Mathf.Max(1,Screen.height)),540,1600);
        texture=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32)
            {name="KingKong heart of leyline cinematic",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        texture.Create();camera.targetTexture=texture;camera.aspect=width/(float)height;
        Shader shader=Resources.Load<Shader>(ResourceRoot+"LeylineSurface")??Shader.Find("Sprites/Default");
        material=new Material(shader){name="Actual leyline actor colors",hideFlags=HideFlags.DontSave};
        scenery=new FrogCatchCinematicEnvironment(root.transform);
        sourceBody=CreateBody("Leyline actual caster");previousBody=CreateBody("Leyline pose bridge");
        leyline=new PixelKingKongLeylineStage(root.transform);
        root.SetActive(false);
    }

    public void Warmup(string species)
    {
        if(disposed)return;
        Sprite[] frames=LoadPoses(species);
        if(frames!=null)for(int i=0;i<frames.Length;i++)PixelMantisReaper.VisibleBody(frames[i]);
    }

    public bool Begin(Texture2D background,FrogCatchCinematicStage.Actor source,string region)
    {
        if(disposed)return false;Hide();
        if(background==null||ActualFrame(source,0)==null)return false;
        Background=background;this.source=source;sourceBody.actor=previousBody.actor=source;
        // The original shiny textures are authoritative; never recolor a normal atlas over them.
        poses=source.shiny?null:LoadPoses(source.species);
        if(poses==null)
        {
            for(int i=0;i<fallbackPoses.Length;i++)fallbackPoses[i]=ActualFrame(source,OriginalFrames[i]);
            poses=fallbackPoses;
        }
        for(int i=0;i<poses.Length;i++)PixelMantisReaper.VisibleBody(poses[i]);
        Bounds reference=PixelMantisReaper.VisibleBody(poses[0]);
        referenceWidth=Mathf.Max(.02f,reference.size.x);
        referenceHeight=3.55f*reference.size.y/referenceWidth;
        shakeScale=BombBrick.Game.Core.GameUserSettings.Load().CameraShakeScale;hasReturnView=false;
        away=new Vector3(source.flipX?-1f:1f,0f,-1.3f).normalized;
        if(!scenery.Begin(region,away,source.ground))return false;
        root.SetActive(true);leyline.Begin(scenery.Region);camera.enabled=true;Tick(0f,0f);return true;
    }

    public void Tick(float elapsed,float dt)
    {
        if(!Active)return;
        float t=Mathf.Clamp(elapsed,0f,Duration);
        Shot=t<.55f?1:t<1.25f?2:t<2.3f?3:4;
        float chest=Ease(.35f,.68f,t)*(1f-Ease(1.17f,1.51f,t));
        float roar=Ease(1.25f,1.62f,t)*(1f-Ease(2.20f,2.62f,t));
        float restore=Ease(2.30f,2.92f,t);
        float heroHeight=Mathf.Clamp(referenceHeight,2.2f,4.8f);
        Vector3 view=away*Mathf.Lerp(8.7f,6.8f,chest)+Vector3.up*Mathf.Lerp(2.5f,heroHeight*.62f,chest);
        Vector3 look=Vector3.up*Mathf.Lerp(heroHeight*.43f,heroHeight*.61f,chest);
        view=Vector3.Lerp(view,away*8.2f+Vector3.up*.75f,roar);
        look=Vector3.Lerp(look,Vector3.up*(heroHeight*.52f),roar);
        view=Vector3.Lerp(view,away*9.8f+Vector3.up*5.2f,restore);
        look=Vector3.Lerp(look,Vector3.up*(heroHeight*.36f),restore);
        float impact=Pulse(t,.72f,.095f)+Pulse(t,1.10f,.10f)+Pulse(t,1.67f,.15f)*.7f;
        Vector3 side=Vector3.Cross(Vector3.up,away);
        view+=side*(Mathf.Sin(t*93f)*impact*.038f*shakeScale)+Vector3.up*(Mathf.Sin(t*117f)*impact*.025f*shakeScale);
        camera.transform.localPosition=view;camera.transform.localRotation=Quaternion.LookRotation(look-view,Vector3.up);
        camera.fieldOfView=Mathf.Lerp(Mathf.Lerp(47f,40f,chest),49f,restore);
        camera.backgroundColor=scenery.SkyColor;

        int pose=PoseAt(t),previousPose=Mathf.Max(0,pose-1);
        float blend=pose==0?1f:Ease(PoseTimes[pose],PoseTimes[pose]+.045f,t);
        Sprite frame=poses[pose],previous=poses[previousPose];if(previous==frame)blend=1f;
        float left=Pulse(t,.72f,.12f),right=Pulse(t,1.10f,.12f),recoil=left+right;
        float lift=Ease(1.25f,1.6f,t)*(1f-Ease(2.12f,2.35f,t));
        float settle=Pulse(t,1.65f,.20f);
        Vector2 stretch=new Vector2(1f+recoil*.07f+settle*.055f,1f-recoil*.075f+lift*.045f);
        float roll=(left-right)*2.4f*(source.flipX?-1f:1f);
        Vector3 foot=Vector3.up*(lift*.055f-recoil*.045f);
        float scale=3.55f/referenceWidth;
        float returnBlend=hasReturnView?Ease(2.55f,Duration,t):0f;
        Bounds bounds=PixelMantisReaper.VisibleBody(frame);
        if(returnBlend>0f)
        {
            float distance=Vector3.Dot(foot-camera.transform.localPosition,camera.transform.localRotation*Vector3.forward);
            float halfHeight=distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            float targetScale=returnHeight*2f*halfHeight/Mathf.Max(.02f,bounds.size.y*stretch.y);
            scale=Mathf.Lerp(scale,targetScale,returnBlend);
            Vector3 matchedFoot=camera.transform.localPosition+camera.transform.localRotation*
                new Vector3((returnFoot.x-.5f)*2f*halfHeight*camera.aspect,(returnFoot.y-.5f)*2f*halfHeight,distance);
            foot=Vector3.Lerp(foot,matchedFoot,returnBlend);
        }
        float glow=(.12f+recoil*.45f+lift*.24f)*(1f-returnBlend);
        DrawBody(sourceBody,frame,foot,scale,stretch,roll,70,blend,glow);
        previousBody.renderer.enabled=blend<.999f;
        if(previousBody.renderer.enabled)DrawBody(previousBody,previous,foot,scale,stretch,roll,71,1f-blend,glow);
        if(returnBlend>0f)
        {
            Rect actual=SourceViewportRect;
            float distance=Vector3.Dot(sourceBody.renderer.transform.localPosition-camera.transform.localPosition,camera.transform.localRotation*Vector3.forward);
            float half=distance*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            Vector3 shift=camera.transform.localRotation*new Vector3((returnFoot.x-actual.center.x)*2f*half*camera.aspect,
                (returnFoot.y-actual.yMin)*2f*half,0f)*returnBlend;
            sourceBody.renderer.transform.localPosition+=shift;previousBody.renderer.transform.localPosition+=shift;
        }
        Vector3 core=BodyPoint(sourceBody,new Vector2(.56f,.52f));
        Vector3 handLeft=BodyPoint(sourceBody,new Vector2(.17f,.37f+lift*.19f));
        Vector3 handRight=BodyPoint(sourceBody,new Vector2(.83f,.37f+lift*.19f));
        Vector3 feet=BodyPoint(sourceBody,new Vector2(.5f,0f));
        leyline.Tick(t,core,handLeft,handRight,feet,camera.transform.localRotation,1f-returnBlend);
    }

    public void SetReturnView(Vector2 foot,float height)
    {returnFoot=foot;returnHeight=Mathf.Clamp(height,.005f,.8f);hasReturnView=true;}
    public void Hide()
    {
        if(disposed)return;camera.enabled=false;scenery.Hide();leyline.Hide();root.SetActive(false);
    }
    public void Dispose()
    {
        if(disposed)return;Hide();disposed=true;leyline.Dispose();scenery.Dispose();
        camera.targetTexture=null;texture.Release();Destroy(texture);Destroy(material);
        for(int i=0;i<ownedSprites.Count;i++)Destroy(ownedSprites[i]);ownedSprites.Clear();Destroy(root);
    }

    private Body CreateBody(string name)
    {
        SpriteRenderer renderer=Child(name).AddComponent<SpriteRenderer>();renderer.sharedMaterial=material;
        renderer.enabled=false;return new Body{renderer=renderer};
    }
    private GameObject Child(string name)
    {
        var child=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};child.transform.SetParent(root.transform,false);return child;
    }
    private void DrawBody(Body body,Sprite sprite,Vector3 foot,float scale,Vector2 stretch,float roll,int order,float alpha,float glow)
    {
        if(sprite==null){body.renderer.enabled=false;return;}
        Bounds bounds=PixelMantisReaper.VisibleBody(sprite);
        Vector3 localFoot=new Vector3(bounds.center.x,bounds.min.y,0f);if(body.actor.flipX)localFoot.x=-localFoot.x;
        Quaternion facing=camera.transform.localRotation*Quaternion.Euler(0f,0f,roll);
        Transform transform=body.renderer.transform;transform.localRotation=facing;
        transform.localScale=new Vector3(scale*stretch.x,scale*stretch.y,scale);
        transform.localPosition=foot-facing*Vector3.Scale(localFoot,transform.localScale);
        body.renderer.sprite=sprite;body.renderer.flipX=body.actor.flipX;body.renderer.sortingOrder=order;body.renderer.enabled=true;
        Color color=body.actor.color;if(color.a<=0f)color=Color.white;color.a*=alpha;body.renderer.color=color;
        body.properties.SetTexture("_MainTex",sprite.texture);body.properties.SetFloat("_Glow",glow);body.renderer.SetPropertyBlock(body.properties);
    }
    private Vector3 BodyPoint(Body body,Vector2 point)
    {
        Bounds bounds=PixelMantisReaper.VisibleBody(body.renderer.sprite);
        Vector3 local=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,point.x),Mathf.Lerp(bounds.min.y,bounds.max.y,point.y),0f);
        if(body.actor.flipX)local.x=-local.x;
        return body.renderer.transform.localPosition+body.renderer.transform.localRotation*Vector3.Scale(local,body.renderer.transform.localScale);
    }
    private Rect ViewportRect(Body body)
    {
        if(body.renderer==null||body.renderer.sprite==null)return Rect.zero;
        Bounds bounds=PixelMantisReaper.VisibleBody(body.renderer.sprite);
        float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        Quaternion inverse=Quaternion.Inverse(camera.transform.localRotation);
        float tangent=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
        // Project locally: this private camera lives far away, where world-coordinate unprojection loses pixels.
        for(int i=0;i<4;i++)
        {
            Vector3 local=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,0f);
            if(body.actor.flipX)local.x=-local.x;
            Vector3 position=body.renderer.transform.localPosition+body.renderer.transform.localRotation*Vector3.Scale(local,body.renderer.transform.localScale);
            Vector3 view=inverse*(position-camera.transform.localPosition);
            float half=Mathf.Max(.001f,view.z*tangent),x=.5f+view.x/(2f*half*camera.aspect),y=.5f+view.y/(2f*half);
            minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
        }
        return Rect.MinMaxRect(minX,minY,maxX,maxY);
    }
    private Sprite[] LoadPoses(string species)
    {
        if(string.IsNullOrEmpty(species))return null;
        Sprite[] cached;if(poseCache.TryGetValue(species,out cached))return cached;
        string path=PoseResource(species);if(path==null)return null;
        Texture2D atlas=Resources.Load<Texture2D>(path);if(atlas==null)return null;
        var frames=new Sprite[8];
        for(int i=0;i<frames.Length;i++)
        {
            int left=Mathf.FloorToInt(i%4*atlas.width/4f),right=Mathf.FloorToInt((i%4+1)*atlas.width/4f);
            int bottom=Mathf.FloorToInt((1-i/4)*atlas.height/2f),top=Mathf.FloorToInt((2-i/4)*atlas.height/2f);
            Sprite sprite=Sprite.Create(atlas,new Rect(left,bottom,right-left,top-bottom),new Vector2(.5f,.18f),128f,0,SpriteMeshType.FullRect);
            sprite.name=species+" leyline action "+i;sprite.hideFlags=HideFlags.DontSave;frames[i]=sprite;ownedSprites.Add(sprite);
        }
        poseCache.Add(species,frames);return frames;
    }
    private static Sprite ActualFrame(FrogCatchCinematicStage.Actor actor,int index)
    {
        if(actor.frames!=null&&actor.frames.Length>0)return actor.frames[Mathf.Clamp(index,0,actor.frames.Length-1)]??actor.sprite;
        return actor.sprite;
    }
    private static int PoseAt(float t)
    {for(int i=PoseTimes.Length-1;i>0;i--)if(t>=PoseTimes[i])return i;return 0;}
    private static float Ease(float start,float end,float t){float u=Mathf.InverseLerp(start,end,t);return u*u*(3f-2f*u);}
    private static float Pulse(float t,float at,float width)=>Mathf.Pow(Mathf.Max(0f,1f-Mathf.Abs(t-at)/width),2f);
    private static void Destroy(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
