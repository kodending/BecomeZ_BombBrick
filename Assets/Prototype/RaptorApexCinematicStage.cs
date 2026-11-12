using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

// A presentation-only camera: no combat actor, damage callback or game-clock advancement.
public sealed class RaptorApexCinematicStage:IDisposable
{
    public const string ResourceRoot="CombatCinematics/RaptorApex/v02/";
    private const int StageLayer=30;
    private static readonly Dictionary<string,Sprite[]> sprites=new Dictionary<string,Sprite[]>();
    private static readonly Dictionary<string,Texture2D> portraits=new Dictionary<string,Texture2D>();
    private readonly GameObject root;
    private readonly Camera camera;
    private readonly SpriteRenderer actor;
    private readonly SpriteRenderer bodyCurrent,bodyPrevious;
    private readonly PixelRaptorApexWings wings;
    private readonly RenderTexture target,straightTarget;
    private readonly Material palette,straightAlpha;
    private readonly RaptorApexStageImpact impact;
    private readonly Rank4CinematicAudio continuousAudio;
    private RenderTexture shinyEye;
    private Sprite[] frames;
    private float bodyScale;
    private bool facingLeft;
    private string speciesId;
    private static readonly float[] PoseStarts={0f,2.2f,2.45f,2.7f,3f,3.25f,3.55f,4.1f,4.5f,4.7f,4.9f,5.12f};
    private static readonly Vector2[] VelosBacks={new Vector2(.55859375f,.33984375f),new Vector2(.58203125f,.33203125f),
        new Vector2(.548828125f,.435546875f),new Vector2(.548828125f,.46875f),new Vector2(.521484375f,.46875f),
        new Vector2(.560546875f,.470703125f),new Vector2(.544921875f,.482421875f),new Vector2(.49609375f,.34765625f),
        new Vector2(.6015625f,.431640625f),new Vector2(.609375f,.41796875f),new Vector2(.46875f,.48046875f),new Vector2(.49609375f,.513671875f)};

    // The camera writes premultiplied light; IMGUI consumes straight alpha.
    public Texture Texture=>straightTarget;
    public RenderTexture CameraTarget=>target;
    public Texture EyeTexture {get;private set;}
    public SpriteRenderer Actor=>actor;
    public PixelRaptorApexWings Wings=>wings;
    public bool Active=>camera!=null&&camera.enabled;
    public int FrameIndex {get;private set;}
    public Vector2 CameraOffset=>impact.CameraOffset;

    private static string Prefix(string species)=>species=="velok"?"Velok":species=="raputo"?"Raputo":species=="wilo"?"Wilo":"Velos";

    public static Sprite[] Frames(string species)
    {
        string prefix=Prefix(species);
        if(sprites.TryGetValue(prefix,out Sprite[] result))return result;
        Texture2D atlas=Resources.Load<Texture2D>(ResourceRoot+prefix+"Awakening_12Frames");
        if(atlas==null||atlas.width%4!=0||atlas.height%3!=0)return null;
        int width=atlas.width/4,height=atlas.height/3;
        result=new Sprite[12];
        for(int i=0;i<result.Length;i++)
        {
            result[i]=Sprite.Create(atlas,new Rect(i%4*width,(2-i/4)*height,width,height),
                new Vector2(.5f,.140625f),128f,0,SpriteMeshType.FullRect);
            result[i].name=prefix+" Retouched Awakening "+i;
            result[i].hideFlags=HideFlags.HideAndDontSave;
        }
        sprites.Add(prefix,result);
        return result;
    }

    public static Texture2D Portrait(string species)
    {
        string prefix=Prefix(species);
        if(portraits.TryGetValue(prefix,out Texture2D result))return result;
        result=Resources.Load<Texture2D>(ResourceRoot+prefix+"PredatorEye");
        if(result!=null)portraits.Add(prefix,result);
        return result;
    }

    public static void Warmup()
    {
        foreach(string species in new[]{"velos","velok","raputo","wilo"})
        {Frames(species);Portrait(species);}
        PixelRaptorApexWings.Warmup();
    }

    public static IEnumerable<Sprite> AllFrames()
    {
        foreach(Sprite[] collection in sprites.Values)
            foreach(Sprite frame in collection)yield return frame;
    }

    public RaptorApexCinematicStage(Transform parent)
    {
        root=new GameObject("Apex retouched cinematic stage (presentation only)");
        root.hideFlags=HideFlags.DontSave;root.layer=StageLayer;
        root.transform.SetParent(parent,false);root.transform.position=new Vector3(12000f,12000f,12000f);
        var view=new GameObject("Apex stage camera");view.layer=StageLayer;
        view.transform.SetParent(root.transform,false);view.transform.localPosition=new Vector3(0f,1.4f,-10f);
        camera=view.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3.3f;
        camera.nearClipPlane=.1f;camera.farClipPlane=25f;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=Color.clear;camera.cullingMask=1<<StageLayer;camera.depth=-100f;
        camera.allowHDR=true;camera.allowMSAA=false;
        int height=Mathf.Clamp(Screen.height,540,1080),width=Mathf.RoundToInt(height*Mathf.Max(.5f,Screen.width/(float)Mathf.Max(1,Screen.height)));
        target=new RenderTexture(width,height,16,SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32)
            {name="Apex retouched stage and flowing wings",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        target.Create();camera.targetTexture=target;camera.enabled=false;
        straightTarget=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32)
            {name="Apex stage straight alpha composite",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        straightTarget.Create();
        straightAlpha=new Material(Resources.Load<Shader>("CombatCinematics/RaptorApex/StageStraightAlpha"))
            {hideFlags=HideFlags.DontSave};
        var body=new GameObject("Retouched cinematic actor");body.layer=StageLayer;body.transform.SetParent(root.transform,false);
        actor=body.AddComponent<SpriteRenderer>();actor.sortingOrder=20;
        palette=new Material(Resources.Load<Shader>("CombatCinematics/RaptorApex/RetouchedPalette"))
            {name="Retouched raptor identity palette",hideFlags=HideFlags.DontSave};
        actor.sharedMaterial=palette;
        var hiddenBody=new MaterialPropertyBlock();hiddenBody.SetColor("_Color",Color.clear);actor.SetPropertyBlock(hiddenBody);
        bodyCurrent=CreateBodyLayer("Retouched pose",actor.transform,palette,21);
        bodyPrevious=CreateBodyLayer("Pose transition",actor.transform,palette,22);
        wings=PixelRaptorApexWings.Attach(actor);
        impact=new RaptorApexStageImpact(root.transform);
        continuousAudio=new Rank4CinematicAudio(root.transform);
    }

    public bool Begin(string species,bool flipX,bool shiny=false,bool playSounds=true)
    {
        frames=Frames(species);
        if(frames==null)return false;
        speciesId=species;EyeTexture=Portrait(species);facingLeft=flipX;FrameIndex=0;
        continuousAudio.Begin("raptor",playSounds);impact.Clear();
        palette.SetFloat("_HueShift",shiny&&species=="velos"?.29f:0f);
        if(shiny&&species=="velos"&&EyeTexture!=null)
        {
            if(shinyEye==null)
            {
                shinyEye=new RenderTexture(EyeTexture.width,EyeTexture.height,0,RenderTextureFormat.ARGB32)
                    {name="Retouched shiny raptor eye",hideFlags=HideFlags.DontSave};
                shinyEye.Create();
            }
            RenderTexture previous=RenderTexture.active;
            try{Graphics.Blit(EyeTexture,shinyEye,palette);}
            finally{RenderTexture.active=previous;}
            EyeTexture=shinyEye;
        }
        actor.sprite=frames[0];actor.flipX=flipX;actor.color=Color.white;
        bodyCurrent.flipX=bodyPrevious.flipX=flipX;
        Bounds body=PixelMantisReaper.VisibleBody(frames[3]);
        bodyScale=2.35f/Mathf.Max(.01f,body.size.y);
        actor.transform.localScale=Vector3.one*bodyScale;
        actor.transform.localPosition=Vector3.zero;actor.enabled=true;
        wings.Clear();wings.SetCinematic(true);wings.SetAwakened(true);wings.SetReveal(0f);
        camera.enabled=true;
        Tick(0f,0f);
        return true;
    }

    public IEnumerator WarmupRender()
    {
        try
        {
            if(!Begin("velos",false,true,false))yield break;
            Tick(3.65f,.016f);
            yield return null;
            ResolveTexture();
        }
        finally{Hide();}
    }

    public void Tick(float elapsed,float deltaTime)
    {
        if(!Active||frames==null)return;
        float t=elapsed;
        FrameIndex=t<2.2f?0:t<2.45f?1:t<2.7f?2:t<3f?3:t<3.25f?4:
            t<3.55f?5:t<4.1f?6:t<4.5f?7:t<4.7f?8:t<4.9f?9:t<5.12f?10:11;
        actor.sprite=frames[FrameIndex];
        float transition=FrameIndex==0?1f:Ease(PoseStarts[FrameIndex],PoseStarts[FrameIndex]+.14f,t);
        float alpha=1f-Ease(5.25f,5.65f,t);
        bodyCurrent.sprite=actor.sprite;bodyCurrent.enabled=true;bodyCurrent.color=new Color(1f,1f,1f,alpha*transition);
        bodyPrevious.sprite=frames[Mathf.Max(0,FrameIndex-1)];bodyPrevious.enabled=transition<1f;
        bodyPrevious.color=new Color(1f,1f,1f,alpha*(1f-transition));
        if(speciesId=="velos")
        {
            Vector2 back=Vector2.Lerp(VelosBacks[Mathf.Max(0,FrameIndex-1)],VelosBacks[FrameIndex],transition);
            wings.SetBackAnchor(new Vector3((back.x*512f-256f)/128f,(back.y*512f-72f)/128f,0f));
        }
        float dash=Ease(4.5f,5.25f,t),pull=Ease(1.95f,2.7f,t);
        float turn=facingLeft?-1f:1f;
        float lunge=dash*dash;
        float breath=Mathf.Sin(Mathf.Clamp01((t-3.1f)/1.1f)*Mathf.PI)*.055f;
        Bounds visible=PixelMantisReaper.VisibleBody(actor.sprite);
        Bounds previous=PixelMantisReaper.VisibleBody(bodyPrevious.sprite);
        Vector3 poseCenter=Vector3.Lerp(previous.center,visible.center,transition);
        float poseHeight=Mathf.Lerp(previous.size.y,visible.size.y,transition);
        actor.transform.localPosition=new Vector3(turn*(dash*.35f),Mathf.Sin(dash*Mathf.PI)*.45f+breath,-lunge*2f);
        float scale=Mathf.Min(bodyScale*(1f+lunge*1.3f+breath*.4f),5.2f/Mathf.Max(.01f,poseHeight));
        actor.transform.localScale=Vector3.one*scale;
        actor.color=new Color(1f,1f,1f,alpha);
        impact.Tick(t,Mathf.Max(0f,deltaTime));
        Vector2 shake=impact.CameraOffset;
        Vector3 head=visible.center+new Vector3(visible.size.x*.24f,visible.size.y*.25f,0f);
        if(actor.flipX)head.x=-head.x;
        head=actor.transform.localPosition+Vector3.Scale(head,actor.transform.localScale);
        camera.orthographicSize=Mathf.Lerp(.95f,3.4f,pull)-breath*1.3f;
        Vector3 framing=Vector3.Lerp(new Vector3(head.x,head.y,-10f),new Vector3(0f,1.4f,-10f),pull);
        if(facingLeft)poseCenter.x=-poseCenter.x;
        Vector3 chargeCenter=actor.transform.localPosition+poseCenter*scale;
        framing=Vector3.Lerp(framing,new Vector3(chargeCenter.x,chargeCenter.y,-10f),dash);
        camera.transform.localPosition=framing+new Vector3(shake.x,shake.y,0f);
        TickAudio(t);
        wings.Tick(Mathf.Max(0f,deltaTime));
        wings.SetReveal(Ease(2.4f,3.45f,t));
        ResolveTexture();
    }

    private void TickAudio(float elapsed)
        =>continuousAudio.Tick(elapsed);

    public void PlayPrelude(bool playSounds=true)
    {
        HideVisuals();continuousAudio.PlayPrelude("raptor",playSounds);
    }

    // Resolve outside OnGUI: Graphics.Blit changes the graphics matrices and render target.
    public void ResolveTexture()
    {
        RenderTexture previous=RenderTexture.active;
        try{Graphics.Blit(target,straightTarget,straightAlpha);}
        finally{RenderTexture.active=previous;}
    }

    public void Hide()
    {
        HideVisuals();continuousAudio.Stop();
    }

    public void CompletePresentation()
    {
        HideVisuals();continuousAudio.Finish();
    }

    public void CancelPresentation()
    {
        HideVisuals();continuousAudio.CancelPresentation();
    }

    private void HideVisuals()
    {
        if(camera!=null)camera.enabled=false;
        if(wings!=null)wings.Clear();
        impact?.Clear();
        if(bodyCurrent!=null)bodyCurrent.enabled=false;
        if(bodyPrevious!=null)bodyPrevious.enabled=false;
        if(actor!=null)actor.enabled=false;
    }

    public void Dispose()
    {
        Hide();
        continuousAudio.Dispose();
        if(camera!=null)camera.targetTexture=null;
        if(target!=null){target.Release();Destroy(target);}
        if(straightTarget!=null){straightTarget.Release();Destroy(straightTarget);}
        if(shinyEye!=null){shinyEye.Release();Destroy(shinyEye);}
        if(palette!=null)Destroy(palette);
        if(straightAlpha!=null)Destroy(straightAlpha);
        impact?.Dispose();
        if(root!=null)Destroy(root);
    }

    private static void Destroy(Object value)
    {if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    private static float Ease(float start,float end,float t)=>Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(start,end,t));

    private static SpriteRenderer CreateBodyLayer(string name,Transform parent,Material material,int order)
    {
        var layer=new GameObject(name);layer.layer=StageLayer;layer.transform.SetParent(parent,false);
        var renderer=layer.AddComponent<SpriteRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
        renderer.enabled=false;return renderer;
    }
}
