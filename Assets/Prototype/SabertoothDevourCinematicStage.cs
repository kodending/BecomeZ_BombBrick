using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Retouched presentation only: this stage has no Unit, combat callback or gameplay clock.
public sealed class SabertoothDevourCinematicStage:IDisposable
{
    public const float Duration=4f;
    public const string ResourceRoot="CombatCinematics/SabertoothDevour/";
    private const int StageLayer=30;
    private static readonly Dictionary<string,Sprite[]> framesBySpecies=new Dictionary<string,Sprite[]>();
    private static readonly Dictionary<string,Bounds[]> boundsBySpecies=new Dictionary<string,Bounds[]>();
    private static readonly float[] PoseStarts={0f,1.45f,1.75f,1.95f,2.2f,2.5f,2.85f,3.1f};
    private static readonly int BackdropTimeId=Shader.PropertyToID("_BackdropTime"),StagePhaseId=Shader.PropertyToID("_StagePhase"),
        ActorRectId=Shader.PropertyToID("_ActorRect");
    private readonly GameObject root;
    private readonly Camera camera;
    private readonly SpriteRenderer actor,current,previous;
    private readonly RenderTexture target,composite,background;
    private readonly Texture2D domain;
    private readonly Material palette,straightAlpha,windMaterial,backdrop;
    private readonly ParticleSystem wind;
    private readonly ParticleSystem.Particle[] windPixels=new ParticleSystem.Particle[48];
    private Sprite[] frames;
    private Bounds[] bodies;
    private PixelSabertoothDevour devour;
    private float bodyScale,windAge,backdropAge;
    private bool facingLeft,disposed;

    public Texture Texture=>composite;
    public Texture BackgroundTexture=>background;
    public RenderTexture CameraTarget=>target;
    public SpriteRenderer Actor=>actor;
    public Sprite EyePose=>frames==null?null:frames[0];
    public PixelSabertoothDevour Devour=>devour;
    public bool Active=>!disposed&&camera!=null&&camera.enabled;
    public int FrameIndex {get;private set;}
    public Rect ActorViewportRect {get;private set;}
    public Vector2 MouthViewport {get;private set;}
    public float JawBodyWidthRatio
    {
        get
        {
            if(devour==null||bodies==null)return 0f;
            float jawWidth=devour.UpperJaw.sprite.bounds.size.x*Mathf.Abs(devour.UpperJaw.transform.lossyScale.x);
            float width=bodies[FrameIndex].size.x*Mathf.Abs(actor.transform.lossyScale.x);
            return jawWidth/Mathf.Max(.001f,width);
        }
    }

    public static Sprite[] Frames(string species)
    {
        if(string.IsNullOrEmpty(species))return null;
        if(framesBySpecies.TryGetValue(species,out Sprite[] cached))return cached;
        bool first=species=="geomchi"||species=="seola";
        if(!first&&species!="nox"&&species!="terakan")return null;
        string atlasName=first?"SabertoothStage_Geomchi_Seola_8Frames":"SabertoothStage_Nox_Terrakan_8Frames";
        Texture2D atlas=Resources.Load<Texture2D>(ResourceRoot+atlasName);
        if(atlas==null||atlas.width<1024||atlas.height<1024)return null;
        Color32[] pixels=ReadPixels(atlas);
        for(int half=0;half<2;half++)
        {
            string id=first?(half==0?"geomchi":"seola"):(half==0?"nox":"terakan");
            var frames=new Sprite[8];var bodies=new Bounds[8];
            for(int i=0;i<8;i++)
            {
                int column=i%4,row=3-half*2-i/4;
                int x=column*atlas.width/4,y=row*atlas.height/4;
                int width=(column+1)*atlas.width/4-x,height=(row+1)*atlas.height/4-y;
                int minX=width,minY=height,maxX=-1,maxY=-1;
                for(int py=0;py<height;py++)for(int px=0;px<width;px++)
                    if(pixels[(y+py)*atlas.width+x+px].a>=48)
                    {minX=Mathf.Min(minX,px);minY=Mathf.Min(minY,py);maxX=Mathf.Max(maxX,px);maxY=Mathf.Max(maxY,py);}
                if(maxX<minX){minX=0;minY=0;maxX=width-1;maxY=height-1;}
                int footMin=width,footMax=-1,band=Mathf.Max(2,(maxY-minY+1)/8);
                for(int py=minY;py<=Mathf.Min(maxY,minY+band);py++)for(int px=minX;px<=maxX;px++)
                    if(pixels[(y+py)*atlas.width+x+px].a>=48){footMin=Mathf.Min(footMin,px);footMax=Mathf.Max(footMax,px);}
                float footX=(footMax<footMin?minX+maxX+1:footMin+footMax+1)*.5f;
                Vector2 pivot=new Vector2(footX,minY);
                float ppu=(maxX-minX+1)/3f;
                frames[i]=Sprite.Create(atlas,new Rect(x,y,width,height),pivot/new Vector2(width,height),
                    ppu,0,SpriteMeshType.FullRect);
                frames[i].name=id+" Devour Stage "+i;frames[i].hideFlags=HideFlags.HideAndDontSave;
                Vector2 center=(new Vector2((minX+maxX+1)*.5f,(minY+maxY+1)*.5f)-pivot)/ppu;
                bodies[i]=new Bounds(center,new Vector3(maxX-minX+1,maxY-minY+1,0f)/ppu);
            }
            framesBySpecies.Add(id,frames);boundsBySpecies.Add(id,bodies);
        }
        return framesBySpecies[species];
    }

    public static bool HasAssets(string species)=>Frames(species)!=null&&
        Resources.Load<Shader>(ResourceRoot+"StagePalette")!=null&&
        Resources.Load<Shader>(ResourceRoot+"StageBackdrop")!=null&&
        Resources.Load<Texture2D>(ResourceRoot+"PredatorDomain")!=null&&
        Resources.Load<Shader>("CombatCinematics/RaptorApex/StageStraightAlpha")!=null&&
        Resources.Load<Texture2D>("CombatEffects/SabertoothDevour/DevourHeadParts")!=null&&
        Resources.Load<Shader>("CombatEffects/SabertoothDevour/DevourNeon")!=null;

    public static void Warmup()
    {
        foreach(string species in new[]{"geomchi","seola","nox","terakan"})HasAssets(species);
    }

    public SabertoothDevourCinematicStage(Transform parent)
    {
        root=new GameObject("Sabertooth devour stage (presentation only)") {hideFlags=HideFlags.DontSave,layer=StageLayer};
        root.transform.SetParent(parent,false);root.transform.position=new Vector3(14000f,14000f,14000f);
        var view=new GameObject("Sabertooth stage camera") {layer=StageLayer};
        view.transform.SetParent(root.transform,false);view.transform.localPosition=new Vector3(0f,1.25f,-10f);
        camera=view.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3.2f;
        camera.nearClipPlane=.1f;camera.farClipPlane=30f;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=Color.clear;camera.cullingMask=1<<StageLayer;camera.depth=-100f;
        camera.allowHDR=true;camera.allowMSAA=false;camera.enabled=false;
        int height=Mathf.Clamp(Screen.height,540,1080);
        float aspect=Screen.width/(float)Mathf.Max(1,Screen.height);
        int width=Mathf.RoundToInt(height*Mathf.Max(.5f,aspect));
        camera.aspect=width/(float)height;
        target=new RenderTexture(width,height,16,SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?
            RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32)
            {name="Sabertooth isolated stage",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        target.Create();camera.targetTexture=target;
        composite=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32)
            {name="Sabertooth straight-alpha stage composite",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        composite.Create();
        background=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32)
            {name="Sabertooth moving predator domain",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        background.Create();domain=Resources.Load<Texture2D>(ResourceRoot+"PredatorDomain");
        backdrop=new Material(Resources.Load<Shader>(ResourceRoot+"StageBackdrop"))
            {name="Sabertooth layered moving backdrop",hideFlags=HideFlags.DontSave};
        straightAlpha=new Material(Resources.Load<Shader>("CombatCinematics/RaptorApex/StageStraightAlpha"))
            {hideFlags=HideFlags.DontSave};
        palette=new Material(Resources.Load<Shader>(ResourceRoot+"StagePalette"))
            {name="Sabertooth stage identity palette",hideFlags=HideFlags.DontSave};
        var body=new GameObject("Retouched sabertooth presentation actor") {layer=StageLayer};
        body.transform.SetParent(root.transform,false);actor=body.AddComponent<SpriteRenderer>();
        actor.sharedMaterial=palette;actor.sortingOrder=20;actor.forceRenderingOff=true;actor.enabled=false;
        current=CreateBody("Current retouched pose",21);previous=CreateBody("Retouched pose transition",22);
        var air=new GameObject("Stage-only inward pixel motes") {layer=StageLayer};air.transform.SetParent(root.transform,false);
        wind=air.AddComponent<ParticleSystem>();wind.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=wind.main;main.playOnAwake=false;main.loop=false;main.maxParticles=windPixels.Length;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startSpeed=0f;main.startSize3D=true;
        var emission=wind.emission;emission.enabled=false;var shape=wind.shape;shape.enabled=false;
        windMaterial=new Material(Resources.Load<Shader>("CombatEffects/SabertoothDevour/DevourNeon"))
            {name="Sabertooth stage pixel wind",hideFlags=HideFlags.DontSave,mainTexture=Texture2D.whiteTexture};
        windMaterial.SetFloat("_ParticleMode",1f);windMaterial.SetFloat("_DstBlend",(float)BlendMode.One);
        var renderer=air.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=windMaterial;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.alignment=ParticleSystemRenderSpace.View;
        renderer.sortingOrder=70;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        wind.Play(false);wind.Pause(false);
        RenderPipelineManager.endCameraRendering+=OnCameraRendered;
    }

    public bool Begin(string species,bool flipX,bool shiny=false)
    {
        if(disposed)return false;
        Hide();
        if(!HasAssets(species))return false;
        frames=Frames(species);bodies=boundsBySpecies[species];facingLeft=flipX;FrameIndex=0;windAge=backdropAge=0f;
        root.transform.rotation=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        palette.SetFloat("_ShinyNox",shiny&&species=="nox"?1f:0f);
        actor.sprite=frames[0];actor.flipX=flipX;actor.color=Color.white;actor.enabled=true;
        current.flipX=previous.flipX=flipX;
        bodyScale=2.15f/Mathf.Max(.01f,bodies[1].size.y);
        actor.transform.localScale=Vector3.one*bodyScale;actor.transform.localPosition=Vector3.zero;
        RenderTexture old=RenderTexture.active;
        try{RenderTexture.active=target;GL.Clear(true,true,Color.clear);RenderTexture.active=composite;GL.Clear(false,true,Color.clear);}
        finally{RenderTexture.active=old;}
        camera.enabled=true;Tick(0f,0f);return true;
    }

    public void Tick(float elapsed,float deltaTime)
    {
        if(!Active||frames==null)return;
        float t=Mathf.Clamp(elapsed,0f,Duration),poseTime=Mathf.Min(t,3.25f);
        FrameIndex=0;for(int i=1;i<PoseStarts.Length;i++)if(poseTime>=PoseStarts[i])FrameIndex=i;
        float blend=FrameIndex==0?1f:Ease(PoseStarts[FrameIndex],PoseStarts[FrameIndex]+.12f,poseTime);
        actor.sprite=frames[FrameIndex];current.sprite=actor.sprite;previous.sprite=frames[Mathf.Max(0,FrameIndex-1)];
        current.enabled=true;previous.enabled=blend<1f;
        current.color=new Color(1f,1f,1f,blend);previous.color=new Color(1f,1f,1f,1f-blend);
        float turn=facingLeft?-1f:1f,pullback=Ease(1.2f,1.95f,poseTime),charge=Ease(1.95f,3.2f,poseTime);
        float breath=Mathf.Sin(charge*Mathf.PI)*.035f;
        actor.transform.localPosition=new Vector3(-turn*.22f*pullback,-charge*.045f+breath,0f);
        actor.transform.localScale=Vector3.one*bodyScale*(1f+breath*.25f);
        Bounds body=bodies[FrameIndex],prior=bodies[Mathf.Max(0,FrameIndex-1)];
        Vector3 poseCenter=Vector3.Lerp(prior.center,body.center,blend),poseSize=Vector3.Lerp(prior.size,body.size,blend);
        Vector3 eye=poseCenter+new Vector3(poseSize.x*.26f,poseSize.y*.24f,0f);
        if(facingLeft)eye.x=-eye.x;
        eye=actor.transform.localPosition+Vector3.Scale(eye,actor.transform.localScale);
        Vector3 full=new Vector3(turn*.35f,1.35f,-10f);
        float close=Ease(.5f,1f,poseTime)*(1f-pullback);
        camera.transform.localPosition=Vector3.Lerp(full,new Vector3(eye.x,eye.y,-10f),close);
        camera.orthographicSize=Mathf.Lerp(3.2f,.28f,close);
        Vector3 mouthLocal=poseCenter+new Vector3(poseSize.x*.38f,poseSize.y*.12f,0f);
        if(facingLeft)mouthLocal.x=-mouthLocal.x;
        Vector3 mouth=actor.transform.TransformPoint(mouthLocal);
        if(t>=1.95f&&devour==null)
        {
            Vector3 forward=root.transform.right*turn;
            devour=PixelSabertoothDevour.Create(actor,forward,mouth+forward*.8f,4f,2.2f,null,1f);
            if(devour!=null)
            {
                devour.transform.SetParent(root.transform,true);
            }
        }
        if(devour!=null)
        {
            // Hold an open mouth. The one real snap belongs to the battlefield after this stage.
            float effectTime=Mathf.Lerp(0f,SabertoothDevourRules.PullEnd+.12f,Ease(1.95f,3.22f,poseTime));
            devour.Advance(effectTime,mouth);
        }
        windAge+=Mathf.Max(0f,deltaTime);
        DrawWind(root.transform.InverseTransformPoint(mouth),turn,Ease(1.95f,2.6f,t));
        ActorViewportRect=BodyRect(body);
        Vector3 viewport=camera.WorldToViewportPoint(mouth);MouthViewport=new Vector2(viewport.x,viewport.y);
        backdropAge+=Mathf.Max(0f,deltaTime);
        DrawBackdrop(t);
    }

    private void DrawWind(Vector3 mouth,float turn,float opacity)
    {
        for(int i=0;i<windPixels.Length;i++)
        {
            float seed=Hash(i),progress=Mathf.Repeat(windAge*(.55f+seed*.3f)+seed,1f);
            Vector3 start=mouth+new Vector3(turn*(.7f+seed*3.2f),(Hash(i+79)-.5f)*3.1f,0f);
            Vector3 control=Vector3.Lerp(start,mouth,.5f)+Vector3.up*Mathf.Sin(i*2.4f)*.7f;
            Vector3 point=start*((1f-progress)*(1f-progress))+control*(2f*(1f-progress)*progress)+mouth*(progress*progress);
            Vector3 tangent=Vector3.Lerp(control-start,mouth-control,progress);
            float angle=Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg;
            var color=new Color(.63f,1f,.8f,opacity*Mathf.Sin(progress*Mathf.PI)*(.18f+seed*.2f));
            windPixels[i]=new ParticleSystem.Particle {position=point,startColor=color,
                startSize3D=new Vector3(.035f+seed*.02f,.13f+seed*.12f,1f),rotation=90f-angle,
                startLifetime=1f,remainingLifetime=1f,velocity=Vector3.zero};
        }
        wind.SetParticles(windPixels,opacity>.001f?windPixels.Length:0);
    }

    private Rect BodyRect(Bounds body)
    {
        Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=-min;
        for(int i=0;i<4;i++)
        {
            Vector3 local=new Vector3(i%2==0?body.min.x:body.max.x,i<2?body.min.y:body.max.y,0f);
            if(facingLeft)local.x=-local.x;
            Vector3 point=camera.WorldToViewportPoint(actor.transform.TransformPoint(local));
            min=Vector2.Min(min,point);max=Vector2.Max(max,point);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    private SpriteRenderer CreateBody(string name,int order)
    {
        var body=new GameObject(name) {layer=StageLayer};body.transform.SetParent(actor.transform,false);
        var renderer=body.AddComponent<SpriteRenderer>();renderer.sharedMaterial=palette;renderer.sortingOrder=order;
        renderer.enabled=false;return renderer;
    }

    private void OnCameraRendered(ScriptableRenderContext context,Camera rendered)
    {
        if(!Active||rendered!=camera)return;
        RenderTexture previous=RenderTexture.active;
        try{Graphics.Blit(target,composite,straightAlpha);}
        finally{RenderTexture.active=previous;}
    }

    private void DrawBackdrop(float elapsed)
    {
        backdrop.SetFloat(BackdropTimeId,backdropAge);backdrop.SetFloat(StagePhaseId,Ease(1.95f,3.25f,elapsed));
        backdrop.SetVector(ActorRectId,new Vector4(ActorViewportRect.x,ActorViewportRect.y,
            ActorViewportRect.width,ActorViewportRect.height));
        RenderTexture previous=RenderTexture.active;
        try{Graphics.Blit(domain,background,backdrop);}
        finally{RenderTexture.active=previous;}
    }

    public void Hide()
    {
        if(disposed)return;
        camera.enabled=false;actor.enabled=false;current.enabled=false;previous.enabled=false;
        wind.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);wind.Clear(false);
        wind.Play(false);wind.Pause(false);
        if(devour!=null){devour.Cancel(true);devour=null;}
        windAge=backdropAge=0f;
        ActorViewportRect=Rect.zero;MouthViewport=Vector2.zero;
    }

    public void Dispose()
    {
        if(disposed)return;
        Hide();disposed=true;RenderPipelineManager.endCameraRendering-=OnCameraRendered;
        camera.targetTexture=null;
        if(RenderTexture.active==target||RenderTexture.active==composite||RenderTexture.active==background)RenderTexture.active=null;
        target.Release();composite.Release();background.Release();Destroy(target);Destroy(composite);Destroy(background);
        Destroy(palette);Destroy(straightAlpha);Destroy(windMaterial);Destroy(backdrop);Destroy(root);
    }

    private static float Ease(float from,float to,float value)=>Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(from,to,value));
    private static float Hash(int value)=>Mathf.Repeat(Mathf.Sin(value*127.1f+13.7f)*43758.547f,1f);
    private static Color32[] ReadPixels(Texture2D atlas)
    {
        if(atlas.isReadable)return atlas.GetPixels32();
        var copy=RenderTexture.GetTemporary(atlas.width,atlas.height,0,RenderTextureFormat.ARGB32);
        RenderTexture previous=RenderTexture.active;Texture2D readable=null;
        try
        {
            Graphics.Blit(atlas,copy);RenderTexture.active=copy;
            readable=new Texture2D(atlas.width,atlas.height,TextureFormat.RGBA32,false);
            readable.ReadPixels(new Rect(0f,0f,atlas.width,atlas.height),0,0);readable.Apply();return readable.GetPixels32();
        }
        finally
        {
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(copy);Destroy(readable);
        }
    }
    private static void Destroy(Object value)
    {if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
