using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;

// A private, explicitly timed transformation stage. Combat and the fear callback belong to the caller.
public sealed class BearAvatarCinematicStage:IDisposable
{
    public const string ResourceRoot="CombatCinematics/BearAvatar/";
    public const float Duration=2.8f;
    private const int Layer=30,ParticleLimit=1100;
    private static readonly float[] ShotTimes={0f,.4f,.7f,1f,1.5f,1.9f,Duration};
    private static readonly Rect[] OriginalParts={new Rect(.08f,0f,.84f,.29f),new Rect(.49f,.03f,.40f,.57f),
        new Rect(.49f,.03f,.40f,.57f),new Rect(.17f,.19f,.66f,.67f),new Rect(.49f,.37f,.51f,.63f)};
    private sealed class Surface
    {
        public MeshRenderer renderer;
        public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        public Rect viewport;
    }
    private readonly GameObject root;
    private readonly Camera camera;
    private readonly RenderTexture texture;
    private readonly Mesh mesh;
    private readonly Material surfaceMaterial,pixelMaterial,backdropMaterial;
    private readonly Surface current,previous,shotBridge,actual;
    private readonly ParticleSystem particles;
    private readonly ParticleSystem.Particle[] pixels=new ParticleSystem.Particle[ParticleLimit];
    private readonly List<Sprite> ownedFrames=new List<Sprite>();
    private readonly Bounds[] rowBounds=new Bounds[5];
    private Sprite[] parts,head;
    private Sprite returnSprite;
    private FrogCatchCinematicStage.Actor source;
    private Vector2 returnFoot;
    private float returnHeight,shakeScale;
    private int pixelCount;
    private bool disposed,hasReturnView;
    public Texture Texture=>texture;
    public bool Active=>!disposed&&camera!=null&&camera.enabled;
    public FrogCatchCinematicStage.Actor Source=>source;
    public Rect SourceViewportRect=>current.viewport;
    public int Shot{get;private set;}

    public BearAvatarCinematicStage(Transform parent)
    {
        root=new GameObject("Bear avatar private transformation stage"){layer=Layer,hideFlags=HideFlags.DontSave};
        root.transform.SetParent(parent,false);root.transform.position=new Vector3(35000f,23000f,35000f);
        camera=Child("Bear avatar private camera").AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=5f;camera.nearClipPlane=.03f;camera.farClipPlane=40f;
        camera.transform.localPosition=new Vector3(0f,0f,-10f);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.008f,.022f,.033f,1f);camera.cullingMask=1<<Layer;camera.depth=-100f;
        camera.allowHDR=false;camera.allowMSAA=false;
        float resolutionScale=Mathf.Min(1f,Mathf.Min(1600f/Mathf.Max(1,Screen.width),900f/Mathf.Max(1,Screen.height)));
        int width=Mathf.Max(1,Mathf.RoundToInt(Screen.width*resolutionScale)),height=Mathf.Max(1,Mathf.RoundToInt(Screen.height*resolutionScale));
        texture=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32)
            {name="Bear body-part transformation cinematic",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        texture.Create();camera.targetTexture=texture;camera.aspect=width/(float)height;
        mesh=CreateMesh();
        Shader shader=Resources.Load<Shader>(ResourceRoot+"BearAvatarSurface");
        if(shader==null)shader=Shader.Find("Sprites/Default");
        surfaceMaterial=new Material(shader){name="Bear spirit transformation surface",hideFlags=HideFlags.DontSave};
        pixelMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=Texture2D.whiteTexture,name="Bear square spirit and dust",hideFlags=HideFlags.DontSave};
        Shader backdrop=Resources.Load<Shader>(ResourceRoot+"BearAvatarBackdrop");
        if(backdrop!=null)backdropMaterial=new Material(backdrop){name="Bear ancestral void",hideFlags=HideFlags.DontSave};
        if(backdropMaterial!=null)
        {
            GameObject background=Child("Bear soft ancestral space");background.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=background.AddComponent<MeshRenderer>();renderer.sharedMaterial=backdropMaterial;renderer.sortingOrder=-100;
            background.transform.localPosition=new Vector3(0f,0f,3f);background.transform.localScale=new Vector3(camera.aspect*10.2f,10.2f,1f);
        }
        current=CreateSurface("Bear current transforming part",30);previous=CreateSurface("Bear transforming pose blend",29);
        shotBridge=CreateSurface("Bear previous shot bridge",28);actual=CreateSurface("Bear actual species underlay",27);
        particles=Child("Bear moving pixel spirits and heavy foot dust").AddComponent<ParticleSystem>();
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=ParticleLimit;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.simulationSpeed=0f;main.startSpeed=0f;main.startSize3D=true;
        var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
        var particleRenderer=particles.GetComponent<ParticleSystemRenderer>();particleRenderer.sharedMaterial=pixelMaterial;
        particleRenderer.renderMode=ParticleSystemRenderMode.Billboard;particleRenderer.sortingOrder=60;root.SetActive(false);
    }

    public void Warmup(string species="woongi")
    {
        if(disposed)return;
        if(parts==null)parts=LoadFrames("PartsAction",3);
        if(head==null)head=LoadFrames("HeadRoarAction",2);
        if(parts!=null)for(int row=0;row<3;row++)rowBounds[row]=RowBounds(parts,row);
        if(head!=null)for(int row=0;row<2;row++)rowBounds[row+3]=RowBounds(head,row);
    }

    public bool Begin(FrogCatchCinematicStage.Actor actor)
    {
        if(disposed)return false;Hide();
        if(actor.sprite==null)return false;
        Warmup(actor.species);if(parts==null||head==null||!surfaceMaterial.HasProperty("_CellUV")||backdropMaterial==null)return false;
        source=actor;hasReturnView=false;
        Sprite[] original=PixelBearUpperBodyHologram.SpiritFrames("SkillAttack",true);
        returnSprite=original.Length>0?original[Mathf.Min(4,original.Length-1)]:null;
        if(returnSprite!=null)PixelMantisReaper.VisibleBody(returnSprite);
        PixelMantisReaper.VisibleBody(actor.sprite);
        shakeScale=BombBrick.Game.Core.GameUserSettings.Load().CameraShakeScale;
        root.SetActive(true);camera.enabled=true;particles.Play(false);particles.Pause(false);Tick(0f,0f);return true;
    }

    public void SetReturnView(Vector2 viewportFoot,float height)
    {
        if(!Finite(viewportFoot.x)||!Finite(viewportFoot.y)||!Finite(height))return;
        returnFoot=viewportFoot;returnHeight=Mathf.Clamp(height,.005f,.9f);hasReturnView=true;
    }
    public void SetReturnSprite(Sprite sprite)
    {if(sprite==null)return;returnSprite=sprite;PixelMantisReaper.VisibleBody(sprite);}

    public void Tick(float elapsed,float dt)
    {
        if(!Active)return;
        float t=Finite(elapsed)?Mathf.Clamp(elapsed,0f,Duration):0f;pixelCount=0;
        int shot=0;while(shot<5&&t>=ShotTimes[shot+1])shot++;Shot=shot+1;
        float u=Mathf.Clamp01((t-ShotTimes[shot])/(ShotTimes[shot+1]-ShotTimes[shot]));
        float cross=shot==0?1f:Ease(ShotTimes[shot],ShotTimes[shot]+.055f,t);
        DrawShot(shot,u,t,cross);
        shotBridge.renderer.enabled=shot>0&&cross<.999f;
        if(shotBridge.renderer.enabled)
        {
            Sprite frame=Frame(shot-1,3);Bounds reference=Reference(shot-1);
            Draw(shotBridge,frame,reference,ShotCenter(shot-1,1f),ShotHeight(shot-1),Flip(shot-1),
                Vector2.one,1f-cross,.2f,t,1f,FullCrop(),Color.white);
        }
        float hit=Pulse(t,.10f,.10f)+Pulse(t,.57f,.065f)*.5f+Pulse(t,.87f,.065f)*.5f+Pulse(t,2.26f,.15f)*1.7f;
        camera.transform.localPosition=new Vector3(Mathf.Sin(t*103f)*hit*.022f*shakeScale,Mathf.Sin(t*127f)*hit*.014f*shakeScale,-10f);
        if(backdropMaterial!=null){backdropMaterial.SetFloat("_Clock",t);backdropMaterial.SetFloat("_Roar",Ease(2.05f,2.3f,t));}
        SpiritPixels(t,shot,u,cross);particles.SetParticles(pixels,pixelCount);
    }

    private void DrawShot(int shot,float u,float t,float cross)
    {
        float progress=shot==5?Mathf.Min(2f,u/.4f*2f):Ease(.03f,.96f,u)*3f;
        int frame=Mathf.Min(3,Mathf.FloorToInt(progress)),next=Mathf.Min(3,frame+1);
        float blend=Ease(.65f,1f,progress-frame);
        Sprite first=Frame(shot,frame),second=Frame(shot,next);Bounds reference=Reference(shot);
        Vector2 center=ShotCenter(shot,u);float height=ShotHeight(shot);
        float lockPulse=Pulse(u,.42f,.28f),grow=Ease(.05f,.74f,u);
        var stretch=new Vector2(1f+lockPulse*.023f,1f-lockPulse*.018f+Mathf.Sin(u*Mathf.PI)*.015f);
        float returnBlend=shot==5&&hasReturnView?Ease(2.45f,Duration,t):0f;
        if(returnBlend>0f)
        {
            height=Mathf.Lerp(height,returnHeight*10f,returnBlend);
            center=Vector2.Lerp(center,new Vector2((returnFoot.x-.5f)*10f*camera.aspect,
                (returnFoot.y-.5f)*10f+height*.5f),returnBlend);
        }
        float reveal=shot<5?Ease(.02f,.65f,u):1f;
        float sourceAlpha=shot<5?(1f-Ease(.15f,.60f,u))*cross:0f;
        float alpha=cross;
        // The old species is visible beneath each incoming spirit part, including shiny actors.
        actual.renderer.enabled=sourceAlpha>.001f||returnBlend>.001f;
        if(sourceAlpha>.001f)
            Draw(actual,source.sprite,PixelMantisReaper.VisibleBody(source.sprite),center,height,Flip(shot),
                stretch,sourceAlpha,.12f,t,0f,OriginalParts[shot],SourceColor(),reveal,true);
        float pulse=(.12f+lockPulse*.35f+grow*.1f)*(1f-returnBlend);
        Draw(current,first,reference,center,height,Flip(shot),stretch,alpha*(1f-blend)*(1f-returnBlend),pulse,t,1f,FullCrop(),Color.white,reveal);
        previous.renderer.enabled=next!=frame&&blend>.001f&&returnBlend<.999f;
        if(previous.renderer.enabled)
            Draw(previous,second,reference,center,height,Flip(shot),stretch,alpha*blend*(1f-returnBlend),pulse,t,1f,FullCrop(),Color.white,reveal);
        if(returnBlend>.001f&&returnSprite!=null)
        {
            Bounds target=PixelMantisReaper.VisibleBody(returnSprite);
            Draw(actual,returnSprite,target,center,height,source.flipX,Vector2.one,returnBlend,0f,t,0f,FullCrop(),Color.white);
            if(returnBlend>=.5f)current.viewport=actual.viewport;
        }
        else if(previous.renderer.enabled&&blend>=.5f)current.viewport=previous.viewport;
    }

    private Sprite Frame(int shot,int index)=>shot<4?parts[(shot==0?0:shot<3?1:2)*4+index]:head[(shot==4?0:1)*4+index];
    private Bounds Reference(int shot)=>rowBounds[shot==0?0:shot<3?1:shot==3?2:shot==4?3:4];
    private bool Flip(int shot)=>source.flipX^(shot==2);
    private static Vector2 ShotCenter(int shot,float u)
    {
        float push=Mathf.Sin(u*Mathf.PI)*.06f;
        return shot==0?new Vector2(0f,1.7f+push):shot==1?new Vector2(.65f-push,1.25f):
            shot==2?new Vector2(-.65f+push,1.25f):shot==3?new Vector2(0f,push):
            shot==4?new Vector2(0f,.12f):new Vector2(0f,-.12f+push);
    }
    private static float ShotHeight(int shot)=>shot==0?9.5f:shot<3?10f:shot==3?12.5f:shot==4?7.4f:7.1f;
    private Color SourceColor()=>source.color.a>0f?source.color:Color.white;

    private void Draw(Surface surface,Sprite sprite,Bounds reference,Vector2 center,float height,bool flip,
        Vector2 stretch,float alpha,float glow,float clock,float spirit,Rect crop,Color tint,float reveal=1f,bool old=false)
    {
        if(sprite==null||alpha<.001f){surface.renderer.enabled=false;return;}
        Bounds visible=PixelMantisReaper.VisibleBody(sprite);
        Bounds clipped=CropBounds(visible,crop);
        if(crop!=FullCrop())reference=clipped;
        float fit=Mathf.Min(height/Mathf.Max(.02f,reference.size.y),camera.aspect*8.5f/Mathf.Max(.02f,reference.size.x));
        Vector2 size=new Vector2(sprite.bounds.size.x*fit*stretch.x,sprite.bounds.size.y*fit*stretch.y);
        Vector2 offset=new Vector2((sprite.bounds.center.x-reference.center.x)*fit*stretch.x,
            (sprite.bounds.center.y-reference.center.y)*fit*stretch.y);if(flip)offset.x=-offset.x;
        Transform transform=surface.renderer.transform;transform.localPosition=new Vector3(center.x+offset.x,center.y+offset.y,0f);
        transform.localScale=new Vector3(flip?-size.x:size.x,size.y,1f);
        var p=surface.properties;Rect cell=sprite.rect;float texWidth=sprite.texture.width,texHeight=sprite.texture.height;
        p.SetTexture("_MainTex",sprite.texture);p.SetVector("_CellUV",new Vector4(cell.x/texWidth,cell.y/texHeight,cell.width/texWidth,cell.height/texHeight));
        float left=(clipped.min.x*sprite.pixelsPerUnit+sprite.pivot.x)/cell.width;
        float bottom=(clipped.min.y*sprite.pixelsPerUnit+sprite.pivot.y)/cell.height;
        p.SetVector("_CropUV",crop==FullCrop()?new Vector4(0f,0f,1f,1f):
            new Vector4(left,bottom,clipped.size.x*sprite.pixelsPerUnit/cell.width,clipped.size.y*sprite.pixelsPerUnit/cell.height));
        tint.a*=Mathf.Clamp01(alpha);p.SetColor("_Color",tint);p.SetFloat("_Glow",glow);p.SetFloat("_Clock",clock);
        p.SetFloat("_Spirit",spirit);p.SetFloat("_Breathe",spirit*.0035f);
        p.SetFloat("_Reveal",reveal);p.SetFloat("_Old",old?1f:0f);surface.renderer.SetPropertyBlock(p);surface.renderer.enabled=true;
        float x=(clipped.center.x-reference.center.x)*fit*stretch.x;if(flip)x=-x;
        float y=(clipped.center.y-reference.center.y)*fit*stretch.y;
        surface.viewport=new Rect(.5f+(center.x+x-clipped.size.x*fit*stretch.x*.5f)/(camera.aspect*10f),
            .5f+(center.y+y-clipped.size.y*fit*stretch.y*.5f)/10f,
            clipped.size.x*fit*stretch.x/(camera.aspect*10f),clipped.size.y*fit*stretch.y/10f);
    }

    private void SpiritPixels(float t,int shot,float u,float cross)
    {
        Vector2 center=ShotCenter(shot,u);float fade=1f-Ease(2.46f,Duration,t);
        for(int i=0;i<125;i++)
        {
            float seed=Seed(i+27),life=Mathf.Repeat(t*(.35f+seed*.12f)+i*.618034f,1f);
            float angle=Seed(i*i+71)*Mathf.PI*2f+life*Mathf.PI*1.35f;
            float radius=(1f-life)*2.5f+.35f;
            Vector3 point=new Vector3(center.x+Mathf.Cos(angle)*radius*(shot==0?1.5f:1f),
                center.y+(life-.5f)*(shot==0?3f:6.3f),-.03f);
            Add(point,.035f+seed*.045f,new Color(.27f,.9f,.84f,Mathf.Sin(life*Mathf.PI)*.7f*fade));
        }
        float burst=shot==0?Ease(.06f,.1f,t)*(1f-Ease(.15f,.39f,t)):shot==5?Ease(2.10f,2.27f,t):0f;
        if(burst>0f)
        {
            float age=shot==0?Mathf.Clamp01((t-.08f)/.32f):Mathf.Clamp01((t-2.16f)/.64f);
            Vector2 foot=shot==0?new Vector2(0f,FeetBaseline(u)):new Vector2(0f,-3.45f);
            for(int i=0;i<82;i++)
            {
                float seed=Seed(i+93),angle=i*2.399963f,radius=(.35f+seed*4.5f)*age;
                Vector3 point=new Vector3(foot.x+Mathf.Cos(angle)*radius,foot.y+Mathf.Sin(angle)*radius*.16f+Mathf.Sin(age*Mathf.PI)*seed*.65f,-.02f);
                Add(point,.045f+seed*.09f,new Color(.63f,.53f,.34f,(1f-age)*burst*fade));
            }
        }
        if(shot==5)
        {
            float roar=Ease(2.08f,2.26f,t)*fade;
            for(int ring=0;ring<3;ring++)for(int i=0;i<92;i++)
            {
                float age=Mathf.Clamp01((t-2.13f-ring*.065f)/.55f);if(age<=0f||age>=1f)continue;
                float angle=i*Mathf.PI*2f/92f;if(Seed(i+ring*31)<.17f)continue;
                float radius=.55f+age*7.8f;
                Vector3 point=new Vector3(Mathf.Cos(angle)*radius,-1.1f+Mathf.Sin(angle)*radius*.45f,-.05f);
                Add(point,.035f+(1f-age)*.055f,new Color(.57f,.98f,.91f,roar*(1f-age)*.85f));
            }
        }
        if(shot>0&&cross<.999f)for(int i=0;i<75;i++)
        {
            float seed=Seed(i+shot*43),x=Mathf.Lerp(-camera.aspect*5.5f,camera.aspect*5.5f,cross);
            Add(new Vector3(x+(seed-.5f)*2.5f,(Seed(i+71)-.5f)*10f,-.08f),.045f+seed*.07f,
                new Color(.5f,.95f,.87f,Mathf.Sin(cross*Mathf.PI)*.65f));
        }
    }

    private float FeetBaseline(float u)
    {
        Sprite frame=Frame(0,2);Bounds reference=Reference(0);
        float fit=Mathf.Min(ShotHeight(0)/Mathf.Max(.02f,reference.size.y),camera.aspect*8.5f/Mathf.Max(.02f,reference.size.x));
        // All four feet drawings plant their soles at 92% down the shared canvas.
        return ShotCenter(0,u).y+(frame.bounds.min.y+frame.bounds.size.y*.08f-reference.center.y)*fit;
    }

    private void Add(Vector3 point,float size,Color color)
    {
        if(pixelCount>=ParticleLimit||color.a<.003f)return;
        pixels[pixelCount]=new ParticleSystem.Particle{position=point,startSize3D=Vector3.one*size,startColor=color,
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(pixelCount+1)};pixelCount++;
    }
    private Sprite[] LoadFrames(string resource,int rows)
    {
        Texture2D atlas=Resources.Load<Texture2D>(ResourceRoot+resource);if(atlas==null||atlas.width<4||atlas.height<rows)return null;
        var frames=new Sprite[4*rows];
        for(int i=0;i<frames.Length;i++)
        {
            int x=Mathf.FloorToInt(i%4*atlas.width/4f),right=Mathf.FloorToInt((i%4+1)*atlas.width/4f);
            int y=Mathf.FloorToInt((rows-1-i/4)*atlas.height/(float)rows),top=Mathf.FloorToInt((rows-i/4)*atlas.height/(float)rows);
            Sprite frame=Sprite.Create(atlas,new Rect(x,y,right-x,top-y),Vector2.one*.5f,128f,0,SpriteMeshType.FullRect);
            frame.name="Bear "+resource+" "+i;frame.hideFlags=HideFlags.DontSave;frames[i]=frame;ownedFrames.Add(frame);
        }
        return frames;
    }
    private static Bounds RowBounds(Sprite[] frames,int row)
    {
        Bounds result=PixelMantisReaper.VisibleBody(frames[row*4]);
        for(int i=1;i<4;i++)result.Encapsulate(PixelMantisReaper.VisibleBody(frames[row*4+i]));return result;
    }
    private static Bounds CropBounds(Bounds bounds,Rect region)=>new Bounds(
        new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,region.center.x),Mathf.Lerp(bounds.min.y,bounds.max.y,region.center.y),0f),
        new Vector3(bounds.size.x*region.width,bounds.size.y*region.height,bounds.size.z));
    private static Rect FullCrop()=>new Rect(0f,0f,1f,1f);
    private Surface CreateSurface(string name,int order)
    {
        GameObject host=Child(name);host.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=host.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=surfaceMaterial;renderer.sortingOrder=order;renderer.enabled=false;return new Surface{renderer=renderer};
    }
    private GameObject Child(string name)
    {var child=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};child.transform.SetParent(root.transform,false);return child;}
    private static Mesh CreateMesh()
    {
        const int columns=20,rows=16;var points=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[points.Length];
        var triangles=new int[columns*rows*6];int cursor=0;
        for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
        {int i=y*(columns+1)+x;uv[i]=new Vector2(x/(float)columns,y/(float)rows);points[i]=new Vector3(uv[i].x-.5f,uv[i].y-.5f,0f);}
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
        {int i=y*(columns+1)+x;triangles[cursor++]=i;triangles[cursor++]=i+1;triangles[cursor++]=i+columns+1;triangles[cursor++]=i+1;triangles[cursor++]=i+columns+2;triangles[cursor++]=i+columns+1;}
        var mesh=new Mesh{name="Bear subtly deforming pixel surface",hideFlags=HideFlags.DontSave};mesh.vertices=points;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
    }
    public void Hide()
    {
        if(disposed)return;camera.enabled=false;particles.Clear(false);root.SetActive(false);
        current.renderer.enabled=previous.renderer.enabled=shotBridge.renderer.enabled=actual.renderer.enabled=false;
    }
    public void Dispose()
    {
        if(disposed)return;Hide();disposed=true;camera.targetTexture=null;texture.Release();Destroy(texture);
        Destroy(surfaceMaterial);Destroy(pixelMaterial);Destroy(backdropMaterial);Destroy(mesh);
        foreach(Sprite frame in ownedFrames)Destroy(frame);ownedFrames.Clear();Destroy(root);
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    private static float Seed(int i)=>Mathf.Repeat(i*.61803399f+.371f,1f);
    private static float Ease(float a,float b,float t){float u=Mathf.InverseLerp(a,b,t);return u*u*(3f-2f*u);}
    private static float Pulse(float t,float at,float width)=>Mathf.Pow(Mathf.Max(0f,1f-Mathf.Abs(t-at)/width),2f);
    private static void Destroy(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
