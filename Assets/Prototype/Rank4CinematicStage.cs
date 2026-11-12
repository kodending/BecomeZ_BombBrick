using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class Rank4StageContext
{
    public string family;
    public FrogCatchCinematicStage.Actor source;
    public FrogCatchCinematicStage.Actor[] targets=Array.Empty<FrogCatchCinematicStage.Actor>();
    public FrogCatchCinematicStage.Actor[] allies=Array.Empty<FrogCatchCinematicStage.Actor>();
    public bool[] beetleLiftAllowed=Array.Empty<bool>();
    public Vector2 griffinOrbViewport;
    public float griffinOrbHeight;
    public bool enemy;
    public Texture2D battlefieldBackdrop;
    public string sceneryRegion;
    public Sprite wyvernBattlefieldSprite;
    public Rect wyvernBattlefieldViewportRect;
    public Bounds battlefieldBounds;
    public Rect tyrannoFootprintViewport;
    public bool hasTyrannoFootprint;
    public Vector2 returnPosition;
    public float returnHeight;
    public Vector2 entryPosition;
    public float entryHeight;
    public Sprite returnSprite;
    public bool returnBodyVisible=true;
    public Sprite guardianReturnSprite;
    public Vector2 guardianReturnPosition;
    public float guardianReturnHeight;
    public Color guardianReturnColor=Color.white;
    public bool guardianReturnVisible;
    public float guardianReturnRearEdge=-1f;
}

// Rendering only: the battle owns activation, damage, exclusive time and restoration.
public abstract class Rank4CinematicStage:IDisposable
{
    private const int Layer=30,PixelLimit=4096;
    protected readonly Transform Root;
    protected readonly Camera View;
    protected Rank4StageContext Context;
    private readonly RenderTexture texture;
    private readonly Material surface;
    private readonly List<SpriteRenderer> renderers=new List<SpriteRenderer>();
    private readonly List<Sprite> ownedSprites=new List<Sprite>();
    private readonly List<Mesh> ownedMeshes=new List<Mesh>();
    private readonly List<Material> ownedMaterials=new List<Material>();
    private readonly Dictionary<Texture,Material> geometryMaterials=new Dictionary<Texture,Material>();
    private readonly Dictionary<string,Sprite[]> atlases=new Dictionary<string,Sprite[]>();
    private readonly ParticleSystem pixels;
    private readonly ParticleSystem.Particle[] pixelBuffer=new ParticleSystem.Particle[PixelLimit];
    private int pixelCount;
    private bool disposed;
    public abstract string Family{get;}
    public abstract float Duration{get;}
    public Texture Texture=>texture;
    public bool Active=>!disposed&&View!=null&&View.enabled;
    public int Shot{get;protected set;}
    public Rank4StageContext CurrentContext=>Context;
    public IEnumerable<Sprite> ArtworkFrames=>ownedSprites;
    public float Opacity{get;protected set;}=1f;
    public float ActionTime{get;protected set;}=-1f;
    public int SoundBeat{get;protected set;}=-1;

    protected Rank4CinematicStage(Transform parent,string name,Color clear,int width=640,int height=360)
    {
        var root=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};
        Root=root.transform;Root.SetParent(parent,false);Root.position=new Vector3(52000f,42000f,52000f);
        var cameraObject=new GameObject(name+" camera"){layer=Layer,hideFlags=HideFlags.DontSave};
        cameraObject.transform.SetParent(Root,false);cameraObject.transform.localPosition=Vector3.back*20f;
        View=cameraObject.AddComponent<Camera>();View.enabled=false;View.orthographic=true;View.orthographicSize=4.5f;
        View.nearClipPlane=.05f;View.farClipPlane=80f;View.cullingMask=1<<Layer;View.clearFlags=CameraClearFlags.SolidColor;
        View.backgroundColor=clear;View.allowHDR=false;View.allowMSAA=false;View.depth=-100f;View.aspect=16f/9f;
        texture=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32)
            {name=name+" pixel stage",filterMode=FilterMode.Point,hideFlags=HideFlags.DontSave};
        texture.Create();View.targetTexture=texture;
        surface=new Material(Shader.Find("Sprites/Default")){name=name+" pixel surface",hideFlags=HideFlags.DontSave};
        var pixelObject=new GameObject(name+" free pixels"){layer=Layer,hideFlags=HideFlags.DontSave};
        pixelObject.transform.SetParent(Root,false);pixels=pixelObject.AddComponent<ParticleSystem>();
        var main=pixels.main;main.playOnAwake=false;main.maxParticles=PixelLimit;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startLifetime=1f;main.startSpeed=0f;
        var emission=pixels.emission;emission.enabled=false;var shape=pixels.shape;shape.enabled=false;
        var particleRenderer=pixels.GetComponent<ParticleSystemRenderer>();particleRenderer.sharedMaterial=surface;
        particleRenderer.renderMode=ParticleSystemRenderMode.Billboard;particleRenderer.sortingOrder=2000;
        pixels.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);root.SetActive(false);
    }

    public bool Begin(Rank4StageContext context)
    {
        if(disposed||context==null||context.source.sprite==null)return false;
        Hide();Context=context;Opacity=1f;ActionTime=-1f;SoundBeat=-1;Root.gameObject.SetActive(true);View.enabled=true;
        View.transform.localPosition=Vector3.back*20f;View.transform.localRotation=Quaternion.identity;View.orthographicSize=4.5f;
        pixels.Play(false);pixels.Pause(false);Tick(0f,0f);return true;
    }

    public void Tick(float time,float dt)
    {
        if(!Active)return;
        for(int i=0;i<renderers.Count;i++)if(renderers[i].enabled)renderers[i].enabled=false;
        pixelCount=0;Animate(float.IsNaN(time)||float.IsInfinity(time)?0f:Mathf.Clamp(time,0f,Duration),Mathf.Max(0f,dt));
        pixels.SetParticles(pixelBuffer,pixelCount);
    }

    protected abstract void Animate(float time,float dt);

    protected void CameraPose(Vector3 position,Vector3 target,float lens,bool orthographic=false,Vector3? up=null)
    {
        View.orthographic=orthographic;
        if(orthographic)View.orthographicSize=lens;else View.fieldOfView=lens;
        View.transform.localPosition=position;
        View.transform.localRotation=Quaternion.LookRotation(target-position,up??Vector3.up);
    }

    protected Quaternion CameraFacing=>View.transform.localRotation;

    protected void PaintWorld(SpriteRenderer renderer,Sprite sprite,Vector3 feet,float height,Color color,int order,Quaternion rotation,bool flip=false)
    {
        if(sprite==null||height<=0f||color.a<=0f)return;
        Bounds body=PixelMantisReaper.VisibleBody(sprite);float scale=height/Mathf.Max(.001f,body.size.y);
        Vector3 foot=new Vector3(flip?-body.center.x:body.center.x,body.min.y,0f);
        renderer.enabled=true;renderer.sprite=sprite;renderer.flipX=flip;renderer.color=color;renderer.sortingOrder=order;
        renderer.transform.localRotation=rotation;renderer.transform.localScale=Vector3.one*scale;
        renderer.transform.localPosition=feet-rotation*(foot*scale);
    }

    protected void PaintArtWorld(SpriteRenderer renderer,Sprite sprite,Vector3 center,Vector2 size,Color color,int order,Quaternion rotation)
    {
        PaintArt(renderer,sprite,center,size,color,order);
        renderer.transform.localPosition=center;renderer.transform.localRotation=rotation;
    }

    protected MeshRenderer Geometry(string name,Vector3[] vertices,int[] triangles,Color[] colors,Vector2[] uv=null,Texture artwork=null)
    {
        var child=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};child.transform.SetParent(Root,false);
        var mesh=new Mesh{name=name,hideFlags=HideFlags.DontSave};mesh.vertices=vertices;mesh.triangles=triangles;
        if(colors!=null)mesh.colors=colors;if(uv!=null)mesh.uv=uv;mesh.RecalculateBounds();ownedMeshes.Add(mesh);
        child.AddComponent<MeshFilter>().sharedMesh=mesh;
        Texture albedo=artwork==null?Texture2D.whiteTexture:artwork;
        if(!geometryMaterials.TryGetValue(albedo,out Material material))
        {
            material=new Material(Shader.Find("Hidden/Rank4PixelGeometry")){hideFlags=HideFlags.DontSave};
            material.mainTexture=albedo;geometryMaterials[albedo]=material;ownedMaterials.Add(material);
        }
        var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;
    }

    protected SpriteRenderer MakeSprite(string name,int order=0)
    {
        var child=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};child.transform.SetParent(Root,false);
        var renderer=child.AddComponent<SpriteRenderer>();renderer.sharedMaterial=surface;renderer.sortingOrder=order;
        renderer.enabled=false;renderers.Add(renderer);return renderer;
    }

    protected Material OwnMaterial(Shader shader)
    {
        if(shader==null){Dispose();throw new InvalidOperationException("Missing rank-4 stage shader.");}
        var material=new Material(shader){hideFlags=HideFlags.DontSave};ownedMaterials.Add(material);return material;
    }

    protected Sprite[] LoadAtlas(string resource,int columns,int rows=1)
    {
        if(atlases.TryGetValue(resource,out Sprite[] frames))return frames;
        var atlas=Resources.Load<Texture2D>(resource);
        if(atlas==null)
        {Dispose();throw new InvalidOperationException("Missing rank-4 stage artwork: "+resource);}
        if(columns<1||rows<1)throw new ArgumentOutOfRangeException(nameof(columns));
        frames=new Sprite[columns*rows];float width=atlas.width/(float)columns,height=atlas.height/(float)rows;
        for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)
        {
            Sprite sprite=Sprite.Create(atlas,new Rect(column*width,atlas.height-(row+1)*height,width,height),new Vector2(.5f,.5f),64f,0,SpriteMeshType.FullRect);
            sprite.name=resource+" "+row+":"+column;frames[row*columns+column]=sprite;ownedSprites.Add(sprite);
        }
        atlases[resource]=frames;return frames;
    }

    protected Sprite OwnSprite(Sprite sprite)
    {if(sprite!=null)ownedSprites.Add(sprite);return sprite;}

    // Positions for actors are their visible feet; artwork uses its center.
    protected void Paint(SpriteRenderer renderer,Sprite sprite,Vector2 position,float height,Color color,int order,bool flip=false,float angle=0f)
    {
        if(sprite==null||height<=0f||color.a<=0f)return;
        Bounds body=PixelMantisReaper.VisibleBody(sprite);float scale=height/Mathf.Max(.001f,body.size.y);
        Vector3 foot=new Vector3(flip?-body.center.x:body.center.x,body.min.y,0f);
        renderer.enabled=true;renderer.sprite=sprite;renderer.flipX=flip;renderer.color=color;renderer.sortingOrder=order;
        renderer.transform.localRotation=Quaternion.Euler(0f,0f,angle);renderer.transform.localScale=Vector3.one*scale;
        renderer.transform.localPosition=(Vector3)position-renderer.transform.localRotation*(foot*scale);
    }

    protected void PaintArt(SpriteRenderer renderer,Sprite sprite,Vector2 position,Vector2 size,Color color,int order,float angle=0f)
    {
        if(sprite==null||size.x<=0f||size.y<=0f||color.a<=0f)return;
        renderer.enabled=true;renderer.sprite=sprite;renderer.flipX=false;renderer.color=color;renderer.sortingOrder=order;
        renderer.transform.localPosition=position;renderer.transform.localRotation=Quaternion.Euler(0f,0f,angle);
        renderer.transform.localScale=new Vector3(size.x/Mathf.Max(.001f,sprite.bounds.size.x),size.y/Mathf.Max(.001f,sprite.bounds.size.y),1f);
    }

    protected void PaintActor(SpriteRenderer renderer,FrogCatchCinematicStage.Actor actor,Vector2 position,float height,float frameProgress,int order,float alpha=1f,float angle=0f)
    {
        Color color=actor.color;color.a*=Mathf.Clamp01(alpha);
        Paint(renderer,Frame(actor.frames,frameProgress)??actor.sprite,position,height,color,order,actor.flipX,angle);
    }

    protected void PaintReturn(SpriteRenderer renderer,int order,float alpha=1f)
    {
        if(!Context.returnBodyVisible)return;
        Color color=Context.source.color;color.a*=Mathf.Clamp01(alpha);
        Paint(renderer,Context.returnSprite??Context.source.sprite,Context.returnPosition,Mathf.Max(.3f,Context.returnHeight),
            color,order,Context.source.flipX);
    }

    protected static Sprite Frame(Sprite[] frames,float progress)=>frames==null||frames.Length==0?null:
        frames[Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress)*(frames.Length-1)+.0001f),0,frames.Length-1)];
    protected static float Ease(float start,float end,float time)=>Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(start,end,time));
    protected static float Noise(int seed)=>Mathf.Repeat(Mathf.Sin(seed*12.9898f+78.233f)*43758.5453f,1f);

    protected void Pixel(Vector2 position,float size,Color color,float rotation=0f)
        =>Pixel(new Vector3(position.x,position.y,-.01f),size,color,rotation);

    protected void Pixel(Vector3 position,float size,Color color,float rotation=0f)
    {
        if(pixelCount>=PixelLimit||size<=0f||color.a<=0f)return;
        pixelBuffer[pixelCount++]=new ParticleSystem.Particle{position=position,
            startColor=color,startSize=size,rotation=rotation,startLifetime=1f,remainingLifetime=1f};
    }

    protected void Burst(Vector2 center,float age,int count,float speed,float life,Color color,float size)
    {
        if(age<0f||age>=life||life<=0f)return;
        float progress=age/life;color.a*=1f-Ease(.25f,1f,progress);
        for(int i=0;i<count;i++)
        {
            float angle=Noise(i*31+17)*Mathf.PI*2f;
            Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            Vector2 point=center+direction*speed*age*(.4f+Noise(i*19+7)*.8f)+Vector2.down*(age*age*.6f);
            Pixel(point,size*(.45f+Noise(i*7+3)*.7f)*(1f-progress*.65f),color,Noise(i+19)*360f);
        }
    }

    public void Hide()
    {
        if(disposed)return;
        View.enabled=false;pixels.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Root.gameObject.SetActive(false);
    }

    public virtual void Dispose()
    {
        if(disposed)return;Hide();disposed=true;
        PixelMantisReaper.ReleaseVisibleBodies(ownedSprites);
        foreach(Sprite sprite in ownedSprites)DestroyOwned(sprite);
        foreach(Mesh mesh in ownedMeshes)DestroyOwned(mesh);
        foreach(Material material in ownedMaterials)DestroyOwned(material);
        texture.Release();DestroyOwned(texture);DestroyOwned(surface);DestroyOwned(Root.gameObject);
    }

    private static void DestroyOwned(Object value)
    {if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
