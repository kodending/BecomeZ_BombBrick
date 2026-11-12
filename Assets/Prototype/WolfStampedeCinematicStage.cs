using System;
using UnityEngine;
using Object=UnityEngine.Object;

// The moon-hunt set only renders. The caller owns the battle, its clock, and the live-field handoff.
public sealed class WolfStampedeCinematicStage:IDisposable
{
    public const string ResourceRoot="CombatCinematics/WolfStampede/";
    public const float Duration=3.6f;
    private const int Layer=30,PackCount=24,AirLimit=2400;
    private sealed class Body
    {
        public SpriteRenderer current,previous,shadow;
        public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
    }
    private sealed class Marks
    {
        public Mesh mesh;
        public Vector3[] vertices;
        public Vector2[] uv;
        public Color32[] colors;
        public int count;
    }
    private readonly GameObject root;
    private readonly Camera camera;
    private readonly RenderTexture texture;
    private readonly Material surface,groundMaterial,panoramaMaterial,pawMaterial,dustMaterial,pixelMaterial;
    private readonly Mesh floorMesh,pixelMesh;
    private readonly Transform panorama;
    private readonly Body caster,foreground;
    private readonly Body[] pack=new Body[PackCount];
    private readonly Marks paws,dust;
    private readonly ParticleSystem air;
    private readonly ParticleSystem.Particle[] airPixels=new ParticleSystem.Particle[AirLimit];
    private readonly Rect[] effectUV=new Rect[8];
    private readonly float[] effectAspect=new float[2];
    private Sprite[] frames;
    private Bounds packBounds,sourceBounds;
    private Texture2D panoramaArt,groundArt,effectsArt,background;
    private FrogCatchCinematicStage.Actor source;
    private int airCount;
    private bool disposed;
    public Texture Texture=>texture;
    public bool Active=>!disposed&&camera!=null&&camera.enabled;
    public int Shot{get;private set;}
    public FrogCatchCinematicStage.Actor Source=>source;
    // Live-field identity only: no material in the private set ever receives this texture.
    public Texture2D Background=>background;

    public WolfStampedeCinematicStage(Transform parent)
    {
        root=new GameObject("Wolf stampede private moon hunt set"){layer=Layer,hideFlags=HideFlags.DontSave};
        root.transform.SetParent(parent,false);root.transform.position=new Vector3(43000f,27000f,43000f);
        camera=Child("Wolf stampede private camera").AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=false;camera.nearClipPlane=.035f;camera.farClipPlane=100f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(8,20,40,255);
        camera.cullingMask=1<<Layer;camera.depth=-100f;camera.allowHDR=false;camera.allowMSAA=false;
        float resolutionScale=Mathf.Min(1f,Mathf.Min(1600f/Mathf.Max(1,Screen.width),900f/Mathf.Max(1,Screen.height)));
        int width=Mathf.Max(1,Mathf.RoundToInt(Screen.width*resolutionScale)),height=Mathf.Max(1,Mathf.RoundToInt(Screen.height*resolutionScale));
        texture=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32)
            {name="Wolf dedicated moon hunt cinematic",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear};
        texture.Create();camera.targetTexture=texture;camera.aspect=width/(float)height;
        Shader actorShader=Resources.Load<Shader>(ResourceRoot+"WolfStampedeSurface")??Shader.Find("Sprites/Default");
        surface=new Material(actorShader){name="Wolf moonlit spectral bodies",hideFlags=HideFlags.DontSave};
        Shader setShader=Resources.Load<Shader>(ResourceRoot+"WolfStampedeTerrain");
        if(setShader!=null)
        {
            groundMaterial=SetMaterial(setShader,"Moon hunt authored ground",0f,2000);
            panoramaMaterial=SetMaterial(setShader,"Moon hunt authored panorama",1f,1990);
            Shader contactShader=Resources.Load<Shader>(ResourceRoot+"WolfStampedeContact")??setShader;
            pawMaterial=SetMaterial(contactShader,"Moon hunt shaped paw impressions",2f,2950);
            dustMaterial=SetMaterial(contactShader,"Moon hunt contact dust",2f,3030);
        }
        floorMesh=CreateFloor();pixelMesh=CreateQuad();
        CreateMesh("Wolf dedicated moon hunt ground",floorMesh,groundMaterial);
        panorama=CreateMesh("Wolf dedicated moon hunt panorama",pixelMesh,panoramaMaterial).transform;
        panorama.SetParent(camera.transform,false);
        caster=CreateBody("Wolf original caster",false);foreground=CreateBody("Wolf lens crossing spirit",false);
        for(int i=0;i<PackCount;i++)pack[i]=CreateBody("Wolf running spirit "+i,true);
        paws=CreateMarks("Wolf shaped paw impressions",160,pawMaterial);
        dust=CreateMarks("Wolf foot contact dust",64,dustMaterial);
        pixelMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=Texture2D.whiteTexture,name="Wolf airborne pixel wakes",hideFlags=HideFlags.DontSave};
        air=CreateParticles();root.SetActive(false);
    }

    public void Warmup()
    {
        if(disposed)return;
        PixelWolfStampede.Warmup();
        Sprite[] loaded=PixelWolfStampede.Frames;
        if(loaded!=null&&loaded.Length>=8&&loaded!=frames){frames=loaded;packBounds=Union(frames);}
        panoramaArt=Resources.Load<Texture2D>(ResourceRoot+"MoonHuntPanorama");
        groundArt=Resources.Load<Texture2D>(ResourceRoot+"MoonHuntGround");
        Texture2D marks=Resources.Load<Texture2D>(ResourceRoot+"HuntEffectsAction");
        if(marks!=null&&marks!=effectsArt)
        {
            effectsArt=marks;
            CacheContactBounds(marks);
        }
    }
    public bool Begin(FrogCatchCinematicStage.Actor actor,Texture2D map)
    {
        if(disposed)return false;Hide();
        if(actor.sprite==null||groundMaterial==null||!surface.HasProperty("_CellUV"))return false;
        Warmup();if(frames==null||panoramaArt==null||groundArt==null||effectsArt==null)return false;
        source=actor;background=map;
        sourceBounds=PixelMantisReaper.VisibleBody(actor.sprite);
        if(actor.frames!=null)foreach(Sprite frame in actor.frames)
            if(frame!=null)sourceBounds.Encapsulate(PixelMantisReaper.VisibleBody(frame));
        groundMaterial.mainTexture=groundArt;panoramaMaterial.mainTexture=panoramaArt;
        pawMaterial.mainTexture=dustMaterial.mainTexture=effectsArt;
        root.SetActive(true);camera.enabled=true;air.Play(false);air.Pause(false);Tick(0f,0f);return true;
    }

    public void Tick(float elapsed,float dt)
    {
        if(!Active)return;
        float t=Finite(elapsed)?Mathf.Clamp(elapsed,0f,Duration):0f;airCount=paws.count=dust.count=0;
        Shot=t<.65f?1:t<1.4f?2:t<2.8f?3:4;
        MoveCamera(t);PlacePanorama(t);groundMaterial.SetFloat("_Clock",t);
        DrawCaster(t);for(int i=0;i<PackCount;i++)DrawPackMember(i,t);
        DrawForeground(t);FootContacts(t);Wakes(t);
        Upload(paws);Upload(dust);air.SetParticles(airPixels,airCount);
    }

    private void MoveCamera(float t)
    {
        float reveal=Ease(.5f,1.03f,t),inside=Ease(1.35f,1.85f,t),first=Ease(0f,.65f,t);
        float track=Mathf.Max(0f,t-1.12f)*8.1f;
        Vector3 view=Vector3.Lerp(new Vector3(-.2f+first*.32f,1.8f,-3.2f),new Vector3(-1.1f,2.1f,-9.4f),reveal);
        Vector3 look=Vector3.Lerp(new Vector3(view.x,.01f,.3f),new Vector3(view.x,.65f,.1f),reveal);
        view=Vector3.Lerp(view,new Vector3(track-1.8f,1f,-6.6f),inside);
        look=Vector3.Lerp(look,new Vector3(track-1.8f,.7f,.8f),inside);
        look.x=view.x;
        view=Mirror(view);look=Mirror(look);
        camera.transform.localPosition=view;camera.transform.localRotation=Quaternion.LookRotation(look-view,Vector3.up);
        camera.fieldOfView=Mathf.Lerp(43f,54f,inside);
    }
    private void PlacePanorama(float t)
    {
        const float depth=55f;
        float artAspect=panoramaArt.width/(float)panoramaArt.height;
        float viewHeight=2f*depth*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
        float height=Mathf.Max(viewHeight,viewHeight*camera.aspect/artAspect)*1.13f;
        float parallax=(Ease(.65f,3.2f,t)-.5f)*.018f*(source.flipX?-1f:1f);
        panorama.localPosition=new Vector3(parallax*height,.018f*height,depth);
        panorama.localRotation=Quaternion.identity;panorama.localScale=new Vector3(height*artAspect,height,1f);
        panoramaMaterial.SetFloat("_PanoramaReveal",Ease(.5f,1.05f,t));
    }
    private void DrawCaster(float t)
    {
        Sprite frame=source.sprite;
        if(source.frames!=null&&source.frames.Length>0)
            frame=source.frames[Mathf.Min(source.frames.Length-1,Mathf.FloorToInt(Ease(.6f,1.3f,t)*(source.frames.Length-1)))]??frame;
        float alpha=Ease(.55f,.8f,t)*(1f-Ease(1.3f,1.7f,t));
        Draw(caster.current,frame,sourceBounds,Mirror(new Vector3(-2.7f,.025f,-.8f)),1.5f,alpha,source.flipX,0f,1f,0f,t,SourceColor(),caster.properties,false);
        caster.previous.enabled=false;
    }
    private void DrawPackMember(int i,float t)
    {
        Body wolf=pack[i];float enter=PackEntry(i),appear=Ease(enter,enter+.35f,t);
        float phase=Mathf.Max(0f,t-enter)+Seed(i*17+11)*.6f;
        float cycle=phase/(.35f+Seed(i*23+41)*.13f)*frames.Length;int index=Mathf.FloorToInt(cycle)%frames.Length;
        float height=PackHeight(i);
        Vector3 foot=PackPosition(i,t);foot.y=.025f-(1f-appear)*.32f;
        float alpha=appear*(.73f+Seed(i*29+5)*.2f);
        Draw(wolf.current,frames[index],packBounds,Mirror(foot),height,alpha,source.flipX,1f,appear,0f,phase,Color.white,wolf.properties,false);
        wolf.previous.enabled=false;
        Draw(wolf.shadow,frames[index],packBounds,Mirror(new Vector3(foot.x,.012f,foot.z)),height,
            appear*.30f,source.flipX,0f,1f,0f,phase,Color.white,wolf.properties,true);
    }
    private void DrawForeground(float t)
    {
        float u=Ease(2.78f,Duration,t),alpha=Ease(2.8f,3.04f,t)*.97f,phase=t*1.2f;
        int frame=Mathf.FloorToInt(phase/.40f*frames.Length)%frames.Length;
        float depth=.9f,half=depth*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
        float x=Mathf.Lerp(-1.05f,1.5f,u);if(source.flipX)x=1f-x;
        Vector3 foot=ViewPoint(new Vector2(x,-.34f),depth);
        Draw(foreground.current,frames[frame],packBounds,foot,half*2f*1.85f,alpha,source.flipX,1f,1f,
            Ease(3.3f,Duration,t)*.94f,phase,Color.white,foreground.properties,false);
        foreground.previous.enabled=false;
    }
    private static Vector3 PackPosition(int i,float t)
    {
        float run=Mathf.Max(0f,t-(1.02f+Seed(i*13+7)*.31f));
        float speed=9f+Seed(i*31+19)*2.6f;
        float x=-6.8f+Seed(i*47+3)*10.6f+run*speed;
        float z=-2.8f+Seed(i*67+29)*6.4f+Mathf.Sin(run*2.2f+i*2.31f)*.16f;
        return new Vector3(x,.025f,z);
    }
    private static float PackEntry(int i)=>.69f+Seed(i*53+31)*.4f;
    private static float PackHeight(int i)=>1.06f+Seed(i*37+17)*.55f;

    private void FootContacts(float t)
    {
        for(int i=0;i<4;i++)Contact(new Vector3(-.25f+i*.68f,.024f,i%2==0?-.24f:.24f),t-(.06f+i*.18f),.95f,true);
        for(int wolf=0;wolf<PackCount;wolf++)for(int step=0;step<11;step++)
        {
            float born=PackEntry(wolf)+.27f+Seed(wolf*11+5)*.12f+step*(.19f+Seed(wolf*7+13)*.04f),age=t-born;
            if(age<0f||age>1.14f)continue;
            Vector3 foot=PackPosition(wolf,born);foot.x+=step%2==0?.36f:-.35f;
            foot.z+=step%2==0?.11f:-.11f;foot.y=.024f;Contact(foot,age,.38f,false);
        }
    }
    private void Contact(Vector3 foot,float age,float size,bool opening)
    {
        if(age<0f||age>1.5f)return;
        int frame=Mathf.Clamp(Mathf.FloorToInt(age/.065f),0,3);
        float alpha=Ease(0f,.025f,age)*(1f-Ease(opening?.9f:.65f,opening?1.5f:1.14f,age));
        float stamp=size*(1f+.12f*(1f-Ease(0f,.13f,age)));
        AddMark(paws,Mirror(foot),Vector3.forward,Mirror(Vector3.right),stamp,stamp/effectAspect[0],frame,
            new Color(.69f,.87f,1f,alpha*(opening?.95f:.65f)));
        if(age>.34f)return;
        int dustFrame=4+Mathf.Min(3,Mathf.FloorToInt(age/.085f));
        float dustAlpha=Ease(0f,.025f,age)*(1f-Ease(.12f,.34f,age));
        float breadth=(opening?1.15f:.58f)*(1f+age*1.1f);Quaternion facing=camera.transform.localRotation;
        float dustHeight=breadth/effectAspect[1];
        Vector3 puff=Mirror(foot+Vector3.left*age*(opening?.7f:1.3f))+facing*Vector3.up*(dustHeight*.5f+.015f+age*.18f);
        AddMark(dust,puff,facing*Vector3.right,facing*Vector3.up,breadth,dustHeight,dustFrame,
            new Color(.68f,.81f,.94f,dustAlpha*(opening?.9f:.55f)));
    }
    private void CacheContactBounds(Texture2D atlas)
    {
        Color32[] pixels=atlas.isReadable?atlas.GetPixels32():null;
        for(int row=0;row<2;row++)
        {
            float minX=1f,minY=1f,maxX=0f,maxY=0f;
            for(int col=0;col<4&&pixels!=null;col++)
            {
                int left=Mathf.FloorToInt(col*atlas.width/4f),right=Mathf.FloorToInt((col+1)*atlas.width/4f);
                int bottom=Mathf.FloorToInt((1-row)*atlas.height/2f),top=Mathf.FloorToInt((2-row)*atlas.height/2f);
                for(int y=bottom;y<top;y++)for(int x=left;x<right;x++)if(pixels[y*atlas.width+x].a>32)
                {
                    minX=Mathf.Min(minX,(x-left)/(float)(right-left));maxX=Mathf.Max(maxX,(x-left+1f)/(right-left));
                    minY=Mathf.Min(minY,(y-bottom)/(float)(top-bottom));maxY=Mathf.Max(maxY,(y-bottom+1f)/(top-bottom));
                }
            }
            if(maxX<=minX||maxY<=minY){minX=minY=0f;maxX=maxY=1f;}
            float padding=4f/Mathf.Min(atlas.width/4f,atlas.height/2f);
            minX=Mathf.Max(0f,minX-padding);minY=Mathf.Max(0f,minY-padding);
            maxX=Mathf.Min(1f,maxX+padding);maxY=Mathf.Min(1f,maxY+padding);
            effectAspect[row]=(maxX-minX)*atlas.width/4f/((maxY-minY)*atlas.height/2f);
            for(int col=0;col<4;col++)
            {
                int left=Mathf.FloorToInt(col*atlas.width/4f),right=Mathf.FloorToInt((col+1)*atlas.width/4f);
                int bottom=Mathf.FloorToInt((1-row)*atlas.height/2f),top=Mathf.FloorToInt((2-row)*atlas.height/2f);
                effectUV[row*4+col]=new Rect((left+minX*(right-left)+.5f)/atlas.width,(bottom+minY*(top-bottom)+.5f)/atlas.height,
                    ((maxX-minX)*(right-left)-1f)/atlas.width,((maxY-minY)*(top-bottom)-1f)/atlas.height);
            }
        }
    }
    private void AddMark(Marks batch,Vector3 center,Vector3 right,Vector3 up,float width,float height,int frame,Color color)
    {
        if(color.a<.003f||batch.count>=batch.vertices.Length/4)return;
        int n=batch.count++*4;right*=width*.5f;up*=height*.5f;Rect uv=effectUV[frame];
        batch.vertices[n]=center-right-up;batch.vertices[n+1]=center-right+up;
        batch.vertices[n+2]=center+right+up;batch.vertices[n+3]=center+right-up;
        batch.uv[n]=new Vector2(uv.xMin,uv.yMin);batch.uv[n+1]=new Vector2(uv.xMin,uv.yMax);
        batch.uv[n+2]=new Vector2(uv.xMax,uv.yMax);batch.uv[n+3]=new Vector2(uv.xMax,uv.yMin);
        for(int i=0;i<4;i++)batch.colors[n+i]=color;
    }
    private static void Upload(Marks batch)
    {
        for(int i=batch.count*4;i<batch.vertices.Length;i++){batch.colors[i]=new Color32(0,0,0,0);batch.vertices[i]=Vector3.zero;}
        batch.mesh.vertices=batch.vertices;batch.mesh.uv=batch.uv;batch.mesh.colors32=batch.colors;batch.mesh.RecalculateBounds();
    }
    private void Wakes(float t)
    {
        float vanish=1f-Ease(3.35f,Duration,t);
        for(int wolf=0;wolf<PackCount;wolf++)
        {
            float start=PackEntry(wolf)+.12f,height=PackHeight(wolf);
            int last=Mathf.FloorToInt((t-start)*54f);
            float tail=packBounds.size.x/Mathf.Max(.02f,packBounds.size.y)*height*.42f;
            for(int sample=0;sample<44;sample++)
            {
                int birth=last-sample;if(birth<0)break;
                float born=start+birth/54f,age=t-born,life=age/.8f;
                if(life<0f||life>=1f)continue;
                float seed=Seed(wolf*131+birth*43+17),curl=Seed(birth*29+wolf*71+5)*2f-1f;
                // Birth stays fixed in world space; only the old pixels drift and spread.
                float tailLift=height*(.50f+.17f*Mathf.Sin(born*9f+wolf*2.31f));
                Vector3 p=PackPosition(wolf,born)+new Vector3(-tail,tailLift,0f);
                p+=new Vector3(-age*(.3f+seed*.65f),curl*age*.85f+age*.28f,(Seed(birth*19+wolf*101)-.5f)*age*2.2f);
                float alpha=(1f-life)*(1f-life)*Ease(start,start+.18f,born)*vanish;
                AddAir(Mirror(p),(.07f+seed*.075f)*(1f+life*.7f),
                    new Color(.67f+seed*.16f,.82f+seed*.12f,1f,alpha*.55f),(uint)(wolf*100000+birth+1));
                Vector3 core=PackPosition(wolf,born)+new Vector3(-tail-age*.5f,tailLift+curl*age*.4f,age*curl*.5f);
                AddAir(Mirror(core),(.12f+seed*.07f)*(1f+life*.9f),
                    new Color(.65f,.83f,1f,alpha*.20f),(uint)(5000000+wolf*100000+birth+1));
            }
        }
        float wipe=Ease(2.93f,3.22f,t)*(1f-Ease(3.45f,Duration,t));
        for(int i=0;i<160;i++)
        {
            float seed=Seed(i*i+17),u=Ease(2.78f,Duration,Mathf.Max(2.78f,t-seed*.13f));
            float x=Mathf.Lerp(-.7f,1.4f,u)+(seed-.5f)*.3f;if(source.flipX)x=1f-x;
            AddAir(ViewPoint(new Vector2(x,Seed(i+19)),.81f+seed*.08f),.006f+seed*.009f,
                new Color(.66f,.84f,1f,wipe*(1f-seed)*.72f));
        }
    }
    private void AddAir(Vector3 position,float size,Color color,uint seed=0)
    {
        if(airCount>=AirLimit||color.a<.003f)return;
        airPixels[airCount]=new ParticleSystem.Particle{position=position,startSize3D=Vector3.one*size,startColor=color,
            startLifetime=10f,remainingLifetime=10f,randomSeed=seed==0?(uint)(airCount+1):seed};airCount++;
    }

    private void Draw(SpriteRenderer renderer,Sprite frame,Bounds reference,Vector3 foot,float height,float alpha,bool flip,
        float spectral,float reveal,float dissolve,float clock,Color tint,MaterialPropertyBlock properties,bool shadow)
    {
        if(frame==null||alpha<.002f){renderer.enabled=false;return;}
        float scale=height/Mathf.Max(.02f,reference.size.y);
        Vector3 anchor=new Vector3(reference.center.x,reference.min.y,0f);if(flip)anchor.x=-anchor.x;
        Quaternion facing=shadow?Quaternion.Euler(90f,0f,0f):camera.transform.localRotation;
        Vector3 size=shadow?new Vector3(scale,scale*.48f,scale):Vector3.one*scale;
        renderer.transform.localRotation=facing;renderer.transform.localScale=size;
        renderer.transform.localPosition=foot-facing*Vector3.Scale(anchor,size);
        renderer.sprite=frame;renderer.flipX=flip;renderer.color=new Color(tint.r,tint.g,tint.b,alpha*tint.a);renderer.enabled=true;
        Rect cell=frame.rect;properties.SetTexture("_MainTex",frame.texture);
        properties.SetVector("_CellUV",new Vector4(cell.x/frame.texture.width,cell.y/frame.texture.height,cell.width/frame.texture.width,cell.height/frame.texture.height));
        properties.SetFloat("_Spectral",spectral);properties.SetFloat("_Reveal",reveal);properties.SetFloat("_Dissolve",dissolve);
        properties.SetFloat("_Clock",clock);properties.SetFloat("_Shadow",shadow?1f:0f);renderer.SetPropertyBlock(properties);
        renderer.sortingOrder=shadow?-20:20+Mathf.RoundToInt(-foot.z*8f);
        if(renderer==foreground.current)renderer.sortingOrder=200;
    }
    private Vector3 ViewPoint(Vector2 uv,float depth)
    {
        float half=depth*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
        return camera.transform.localPosition+camera.transform.localRotation*new Vector3((uv.x-.5f)*half*2f*camera.aspect,(uv.y-.5f)*half*2f,depth);
    }
    private Vector3 Mirror(Vector3 point){if(source.flipX)point.x=-point.x;return point;}
    private Color SourceColor()=>source.color.a>0f?source.color:Color.white;
    private static Bounds Union(Sprite[] action)
    {Bounds body=PixelMantisReaper.VisibleBody(action[0]);for(int i=1;i<action.Length;i++)body.Encapsulate(PixelMantisReaper.VisibleBody(action[i]));return body;}
    private Body CreateBody(string name,bool shadow)
    {
        var body=new Body{current=CreateRenderer(name),previous=CreateRenderer(name+" pose bridge")};
        if(shadow)body.shadow=CreateRenderer(name+" ground shadow");return body;
    }
    private SpriteRenderer CreateRenderer(string name)
    {var renderer=Child(name).AddComponent<SpriteRenderer>();renderer.sharedMaterial=surface;renderer.enabled=false;return renderer;}
    private static Material SetMaterial(Shader shader,string name,float mode,int queue)
    {var material=new Material(shader){name=name,hideFlags=HideFlags.DontSave,renderQueue=queue};if(material.HasProperty("_Mode"))material.SetFloat("_Mode",mode);return material;}
    private GameObject CreateMesh(string name,Mesh mesh,Material material)
    {
        var host=Child(name);host.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=host.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=material;renderer.enabled=material!=null;return host;
    }
    private Marks CreateMarks(string name,int count,Material material)
    {
        var batch=new Marks{mesh=new Mesh{name=name,hideFlags=HideFlags.DontSave},vertices=new Vector3[count*4],
            uv=new Vector2[count*4],colors=new Color32[count*4]};
        var triangles=new int[count*6];for(int i=0;i<count;i++)
        {int n=i*4,k=i*6;triangles[k]=n;triangles[k+1]=n+1;triangles[k+2]=n+2;triangles[k+3]=n;triangles[k+4]=n+2;triangles[k+5]=n+3;}
        batch.mesh.MarkDynamic();batch.mesh.vertices=batch.vertices;batch.mesh.uv=batch.uv;batch.mesh.colors32=batch.colors;batch.mesh.triangles=triangles;
        CreateMesh(name,batch.mesh,material);return batch;
    }
    private ParticleSystem CreateParticles()
    {
        var p=Child("Wolf historical pixel wakes and wind").AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var m=p.main;m.playOnAwake=false;m.loop=false;m.maxParticles=AirLimit;m.simulationSpace=ParticleSystemSimulationSpace.Local;
        m.simulationSpeed=0f;m.startSpeed=0f;m.startSize3D=true;var emission=p.emission;emission.enabled=false;var shape=p.shape;shape.enabled=false;
        var renderer=p.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=pixelMaterial;renderer.sortingOrder=-10;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.alignment=ParticleSystemRenderSpace.View;return p;
    }
    private static Mesh CreateFloor()
    {
        var mesh=new Mesh{name="Moon hunt ground plane",hideFlags=HideFlags.DontSave};
        mesh.vertices=new[]{new Vector3(-65f,0f,-65f),new Vector3(-65f,0f,65f),new Vector3(65f,0f,65f),new Vector3(65f,0f,-65f)};
        mesh.uv=new[]{Vector2.zero,Vector2.up*13f,Vector2.one*13f,Vector2.right*13f};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();return mesh;
    }
    private static Mesh CreateQuad()
    {
        var mesh=new Mesh{name="Moon hunt aspect-preserving matte quad",hideFlags=HideFlags.DontSave};
        mesh.vertices=new[]{new Vector3(-.5f,-.5f,0f),new Vector3(-.5f,.5f,0f),new Vector3(.5f,.5f,0f),new Vector3(.5f,-.5f,0f)};
        mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();return mesh;
    }
    private GameObject Child(string name)
    {var child=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};child.transform.SetParent(root.transform,false);return child;}
    public void Hide()
    {
        if(disposed)return;camera.enabled=false;air.Clear(false);root.SetActive(false);
        caster.current.enabled=caster.previous.enabled=foreground.current.enabled=foreground.previous.enabled=false;
        foreach(Body wolf in pack)wolf.current.enabled=wolf.previous.enabled=wolf.shadow.enabled=false;
    }
    public void Dispose()
    {
        if(disposed)return;Hide();disposed=true;camera.targetTexture=null;texture.Release();Destroy(texture);
        Destroy(surface);Destroy(groundMaterial);Destroy(panoramaMaterial);Destroy(pawMaterial);Destroy(dustMaterial);Destroy(pixelMaterial);
        Destroy(floorMesh);Destroy(pixelMesh);Destroy(paws.mesh);Destroy(dust.mesh);Destroy(root);
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    private static float Seed(int i)=>Mathf.Repeat(i*.61803399f+.371f,1f);
    private static float Ease(float a,float b,float t){float u=Mathf.InverseLerp(a,b,t);return u*u*(3f-2f*u);}
    private static void Destroy(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
