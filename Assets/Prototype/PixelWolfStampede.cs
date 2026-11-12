using UnityEngine;

// Damage belongs to the cast; this is one explicitly timed, map-wide front of animated spirits.
public sealed class PixelWolfStampede:MonoBehaviour
{
    public const string AtlasResource="CombatCinematics/WolfStampede/SpectralGallopAction";
    public const string ContactResource="CombatCinematics/WolfStampede/HuntEffectsAction";
    private static Sprite[] frames;
    private static Sprite[] nearFrames;
    private static Sprite[] contactFrames;
    private static Rect[] contactCrops;
    private static Bounds frameBounds;
    private const int SpiritsPerWave=8,StepsPerSpirit=8,PixelLimit=3200;
    private const float TailLifetime=.48f,TailCadence=.008f;
    private SpriteRenderer[] wolves;
    private ParticleSystem particles;
    private ParticleSystem.Particle[] pixels;
    private Material pixelMaterial;
    private Material wolfMaterial;
    private MaterialPropertyBlock wolfProperties;
    private Mesh contactMesh;
    private Material contactMaterial;
    private Vector3[] contactVertices;
    private Vector2[] contactUv;
    private Color[] contactColors;
    private Bounds area;
    private Vector3 direction,across;
    private float half,depth,size;
    private int pixelCount;
    private bool autonomous;
    public float Elapsed{get;private set;}
    public static Sprite[] Frames {get{Warmup();return frames;}}
    public static Sprite[] NearFrames
    {
        get
        {
            if(nearFrames!=null&&nearFrames[0]!=null)return nearFrames;
            Texture2D atlas=Resources.Load<Texture2D>("CombatCinematics/WolfStampede/PackRushForegroundAction");
            if(atlas==null)return null;
            nearFrames=new Sprite[4];
            for(int i=0;i<4;i++)
            {
                int left=Mathf.FloorToInt(i*atlas.width/4f),right=Mathf.FloorToInt((i+1)*atlas.width/4f);
                nearFrames[i]=Sprite.Create(atlas,new Rect(left,0,right-left,atlas.height),new Vector2(.5f,.5f),128f);
                nearFrames[i].name="Wolf low three-quarter stride "+i;nearFrames[i].hideFlags=HideFlags.DontSave;
            }
            return nearFrames;
        }
    }
    public static Sprite RunFrame(float elapsed){Warmup();return frames==null?null:frames[Mathf.FloorToInt(elapsed*15f)&7];}
    public static float HalfExtent(Bounds bounds,Vector3 axis)=>Mathf.Max(.5f,Mathf.Abs(axis.x)*bounds.extents.x+Mathf.Abs(axis.z)*bounds.extents.z);

    public static void WarmupContacts()
    {
        if(contactCrops!=null&&contactFrames!=null&&contactFrames[0]!=null)return;
        Texture2D atlas=Resources.Load<Texture2D>(ContactResource);if(atlas==null)return;
        contactFrames=new Sprite[8];contactCrops=new Rect[2];
        for(int row=0;row<2;row++)
        {
            Vector2 minimum=Vector2.one,maximum=Vector2.zero;
            for(int column=0;column<4;column++)
            {
                int left=Mathf.FloorToInt(column*atlas.width/4f),right=Mathf.FloorToInt((column+1)*atlas.width/4f);
                int bottom=Mathf.FloorToInt((1-row)*atlas.height/2f),top=Mathf.FloorToInt((2-row)*atlas.height/2f);
                var sprite=Sprite.Create(atlas,new Rect(left,bottom,right-left,top-bottom),Vector2.one*.5f,128f);
                sprite.name="Drawn wolf ground contact "+(row*4+column);sprite.hideFlags=HideFlags.DontSave;
                contactFrames[row*4+column]=sprite;Bounds visible=PixelMantisReaper.VisibleBody(sprite);
                Vector2 low=((Vector2)visible.min*128f+sprite.pivot)/sprite.rect.size;
                Vector2 high=((Vector2)visible.max*128f+sprite.pivot)/sprite.rect.size;
                minimum=Vector2.Min(minimum,low);maximum=Vector2.Max(maximum,high);
            }
            contactCrops[row]=Rect.MinMaxRect(Mathf.Max(0f,minimum.x-.01f),Mathf.Max(0f,minimum.y-.01f),
                Mathf.Min(1f,maximum.x+.01f),Mathf.Min(1f,maximum.y+.01f));
        }
    }

    public static void Warmup()
    {
        if(frames!=null&&frames.Length==8&&frames[0]!=null)return;
        Texture2D atlas=Resources.Load<Texture2D>(AtlasResource);if(atlas==null)return;
        frames=new Sprite[8];float width=atlas.width/4f,height=atlas.height/2f;
        Color32[] pixels=atlas.isReadable?atlas.GetPixels32():null;var baseline=new float[]{.5f,.5f};
        if(pixels!=null)for(int row=0;row<2;row++)
        {
            int bottom=Mathf.FloorToInt((1-row)*height),top=Mathf.FloorToInt((2-row)*height),minimum=top;
            for(int y=bottom;y<top&&minimum==top;y++)for(int x=0;x<atlas.width;x++)
                if(pixels[y*atlas.width+x].a>=32){minimum=y;break;}
            if(minimum<top)baseline[row]=(minimum-bottom)/(float)(top-bottom);
        }
        for(int i=0;i<8;i++)
        {
            int left=Mathf.FloorToInt(i%4*width),bottom=Mathf.FloorToInt((1-i/4)*height);
            int right=Mathf.FloorToInt((i%4+1)*width),top=Mathf.FloorToInt((2-i/4)*height);
            frames[i]=Sprite.Create(atlas,new Rect(left,bottom,right-left,top-bottom),new Vector2(.5f,baseline[i/4]),128f);
            frames[i].name="Spectral wolf gallop "+i;frames[i].hideFlags=HideFlags.DontSave;
            Bounds visible=PixelMantisReaper.VisibleBody(frames[i]);
            if(i==0)frameBounds=visible;else frameBounds.Encapsulate(visible);
        }
    }

    public static PixelWolfStampede Create(Bounds bounds,Vector3 direction,SpriteRenderer reference)
    {
        Warmup();if(frames==null)return null;
        var host=new GameObject("Full Moon released spectral stampede");
        var effect=host.AddComponent<PixelWolfStampede>();effect.area=bounds;effect.wolfProperties=new MaterialPropertyBlock();
        effect.direction=direction.sqrMagnitude>.001f?direction.normalized:Vector3.right;
        effect.across=Vector3.Cross(Vector3.up,effect.direction).normalized;
        effect.half=HalfExtent(bounds,effect.direction);effect.depth=HalfExtent(bounds,effect.across);
        effect.size=reference?.sprite==null?1.2f:Mathf.Clamp(PixelMantisReaper.VisibleBody(reference.sprite).size.y*Mathf.Abs(reference.transform.lossyScale.y),.8f,1.4f);
        int layer=reference==null?0:reference.gameObject.layer;
        if(layer==LayerMask.NameToLayer("ScreenSpaceOutline"))layer=0;
        host.layer=layer;effect.wolves=new SpriteRenderer[SpiritsPerWave*WolfStampedeRules.WaveCount];
        var bodyShader=Resources.Load<Shader>("CombatCinematics/WolfStampede/WolfStampedeSurface");
        if(bodyShader!=null)
        {
            effect.wolfMaterial=new Material(bodyShader){hideFlags=HideFlags.DontSave};
            effect.wolfMaterial.SetFloat("_Spectral",1f);
        }
        for(int i=0;i<effect.wolves.Length;i++)
        {
            var wolf=new GameObject("Galloping spirit "+i){layer=layer}.AddComponent<SpriteRenderer>();
            wolf.transform.SetParent(host.transform,false);wolf.sprite=frames[0];
            if(effect.wolfMaterial!=null)wolf.sharedMaterial=effect.wolfMaterial;
            wolf.sortingLayerID=reference==null?0:reference.sortingLayerID;
            wolf.sortingOrder=(reference==null?0:reference.sortingOrder)+18+i%SpiritsPerWave;effect.wolves[i]=wolf;
        }
        effect.pixelMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=Texture2D.whiteTexture,hideFlags=HideFlags.DontSave};
        var motes=new GameObject("History-born spectral tail pixels"){layer=layer};motes.transform.SetParent(host.transform,false);
        effect.particles=motes.AddComponent<ParticleSystem>();effect.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=effect.particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=PixelLimit;
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.simulationSpeed=0f;main.startSpeed=0f;main.startSize3D=true;
        var emission=effect.particles.emission;emission.enabled=false;var shape=effect.particles.shape;shape.enabled=false;
        var renderer=effect.particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=effect.pixelMaterial;
        renderer.sortingLayerID=reference==null?0:reference.sortingLayerID;renderer.sortingOrder=(reference==null?0:reference.sortingOrder)+12;
        effect.pixels=new ParticleSystem.Particle[PixelLimit];
        effect.CreateContactMesh(layer,reference==null?0:reference.sortingLayerID,(reference==null?0:reference.sortingOrder)+11);
        effect.particles.Play(false);effect.particles.Pause(false);
        effect.Advance(0f);return effect;
    }

    public void Advance(float elapsed)
    {
        if(wolves==null)return;Elapsed=Mathf.Max(0f,elapsed);pixelCount=0;
        if(Elapsed>=WolfStampedeRules.Duration)
        {
            foreach(var wolf in wolves)if(wolf!=null)wolf.enabled=false;
            particles.Clear(false);ReleaseGraphics();return;
        }
        Camera camera=Camera.main;Quaternion facing=camera==null?Quaternion.identity:camera.transform.rotation;
        bool flip=Vector3.Dot(direction,facing*Vector3.right)<0f;
        for(int i=0;i<wolves.Length;i++)
        {
            float progress=Progress(i,Elapsed);
            float opacity=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-.20f,.04f,progress))*
                (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1.01f,1.21f,progress)));
            var wolf=wolves[i];wolf.enabled=opacity>.001f;if(!wolf.enabled)continue;
            Vector3 ground=Ground(i,Elapsed);
            float phase=Elapsed*Mathf.Lerp(13f,18f,Seed(i+159))+i*1.73f;
            wolf.sprite=frames[Mathf.FloorToInt(phase)&7];wolf.flipX=flip;
            Rect cell=wolf.sprite.rect;Texture2D atlas=wolf.sprite.texture;
            wolfProperties.SetTexture("_MainTex",atlas);
            wolfProperties.SetVector("_CellUV",new Vector4(cell.x/atlas.width,cell.y/atlas.height,cell.width/atlas.width,cell.height/atlas.height));
            wolfProperties.SetFloat("_Clock",Elapsed);wolf.SetPropertyBlock(wolfProperties);
            float scale=BodyHeight(i)/Mathf.Max(.01f,frameBounds.size.y);
            Vector3 offset=new Vector3(frameBounds.center.x,frameBounds.min.y,0f);if(flip)offset.x=-offset.x;
            wolf.transform.SetPositionAndRotation(ground+Vector3.up*.06f-facing*(offset*scale),facing);
            wolf.transform.localScale=Vector3.one*scale;wolf.color=new Color(.78f,.9f,1f,opacity*.84f);
        }
        for(int i=0;i<wolves.Length;i++)Tail(i,Elapsed);
        UpdateContacts(facing);
        particles.SetParticles(pixels,pixelCount);
    }

    private void CreateContactMesh(int layer,int sortingLayer,int sortingOrder)
    {
        Texture2D atlas=Resources.Load<Texture2D>(ContactResource);if(atlas==null)return;WarmupContacts();
        int count=wolves.Length*(StepsPerSpirit+1);
        contactVertices=new Vector3[count*4];contactUv=new Vector2[count*4];contactColors=new Color[count*4];
        int[] triangles=new int[count*6];
        for(int i=0;i<count;i++)
        {int v=i*4,t=i*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;}
        contactMesh=new Mesh{name="Drawn wolf pads and landing dust",hideFlags=HideFlags.DontSave};contactMesh.MarkDynamic();
        contactMesh.vertices=contactVertices;contactMesh.uv=contactUv;contactMesh.colors=contactColors;contactMesh.triangles=triangles;
        Shader contactShader=Resources.Load<Shader>("CombatCinematics/WolfStampede/WolfStampedeContact")??Shader.Find("Sprites/Default");
        contactMaterial=new Material(contactShader){mainTexture=atlas,hideFlags=HideFlags.DontSave};
        var host=new GameObject("Animated drawn wolf pawprints and ground contacts"){layer=layer};host.transform.SetParent(transform,false);
        host.AddComponent<MeshFilter>().sharedMesh=contactMesh;var renderer=host.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=contactMaterial;renderer.sortingLayerID=sortingLayer;renderer.sortingOrder=sortingOrder;
    }

    private void UpdateContacts(Quaternion facing)
    {
        if(contactMesh==null)return;
        int count=0;float tail=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1.7f,2f,Elapsed));
        for(int i=0;i<wolves.Length;i++)for(int step=0;step<StepsPerSpirit;step++)
        {
            float p=(step+.5f)/StepsPerSpirit,born=ContactTime(i,p),age=Elapsed-born;
            Vector3 position=Ground(i,born)+across*(step%2==0?.1f:-.1f)+Vector3.up*.045f;
            float alpha=age>=0f&&age<.65f?tail*(1f-age/.65f)*.48f:0f;
            ContactQuad(count++,position,across*.19f,direction*.23f,Mathf.Clamp(Mathf.FloorToInt(age/.85f*4f),0,3),
                new Color(.72f,.88f,1f,alpha));
        }
        for(int i=0;i<wolves.Length;i++)
        {
            float progress=Progress(i,Elapsed);
            float cycle=Mathf.Repeat(Elapsed*4.5f+i*.173f,1f),life=Mathf.Clamp01(cycle/.42f);
            Vector3 foot=Ground(i,Elapsed)+Vector3.up*.07f;
            float alpha=cycle<.42f&&progress>0f&&progress<1f?(1f-life)*.7f:0f;
            ContactQuad(count++,foot+(facing*Vector3.up)*.22f,facing*Vector3.right*.48f,facing*Vector3.up*.22f,
                4+Mathf.Clamp(Mathf.FloorToInt(life*4f),0,3),new Color(.72f,.85f,1f,alpha));
        }
        contactMesh.vertices=contactVertices;contactMesh.uv=contactUv;contactMesh.colors=contactColors;contactMesh.RecalculateBounds();
    }

    private static float EntryOffset(int i)=>(Seed(i*13+47)-.5f)*.24f;
    private static float TravelFactor(int i)=>Mathf.Lerp(.94f,1.06f,Seed(i*7+17));
    private static float Progress(int i,float time)=>WolfStampedeRules.Front(time-EntryOffset(i),i/SpiritsPerWave)/TravelFactor(i);
    private static float ContactTime(int i,float progress)=>WolfStampedeRules.HitTime(i/SpiritsPerWave,progress*TravelFactor(i))+EntryOffset(i);
    private float BodyHeight(int i)=>size*Mathf.Lerp(1.02f,1.49f,Seed(i*11+31));
    private Vector3 Ground(int i,float time)
    {
        int wave=i/SpiritsPerWave,slot=(i%SpiritsPerWave*5+wave*3)%SpiritsPerWave;
        float lateral=((slot+.5f+(Seed(i*17+83)-.5f)*.78f)/SpiritsPerWave*2f-1f)*depth*.94f;
        lateral+=Mathf.Sin(time*(.8f+Seed(i+55))*.9f+i*2.31f)*.14f;
        return FlatGround(area.center)+direction*Mathf.LerpUnclamped(-half,half,Progress(i,time))+across*lateral;
    }
    private void Tail(int i,float time)
    {
        float finish=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1.68f,WolfStampedeRules.Duration,time)),height=BodyHeight(i);
        int latest=Mathf.FloorToInt(time/TailCadence),oldest=Mathf.Max(0,Mathf.CeilToInt((time-TailLifetime)/TailCadence));
        for(int birth=latest;birth>=oldest;birth--)
        {
            float born=(birth+(Seed(i*137+birth*23)-.5f)*.8f)*TailCadence;
            float progress=Progress(i,born);if(progress<-.04f||progress>1.08f)continue;
            float age=time-born,life=age/TailLifetime;if(life<0f||life>=1f)continue;
            float fade=(1f-life)*(1f-life)*finish;
            float lift=height*(.46f+.17f*Mathf.Sin(born*17f+i*2.31f));
            Vector3 origin=Ground(i,born)-direction*height*.72f+Vector3.up*lift;
            for(int strand=0;strand<2;strand++)
            {
                float seed=Seed(i*197+birth*11+strand*71),other=Seed(i*31+birth*7+strand*19);
                float spread=height*(.18f+life*.95f);
                Vector3 drift=across*((seed-.5f)*spread)+Vector3.up*((other-.35f)*spread+age*.48f)-direction*(age*.5f);
                drift-=direction*((Seed(i*17+birth*31+strand*47)-.5f)*height*.35f);
                float pixel=height*Mathf.Lerp(.085f,.16f,other)*(1f-life*.72f);
                float alpha=fade*Mathf.Lerp(.34f,.67f,seed);if(alpha<=.003f||pixelCount>=pixels.Length)continue;
                pixels[pixelCount++]=new ParticleSystem.Particle{position=origin+drift,startSize=pixel,
                    startColor=new Color(.63f+other*.18f,.82f+other*.12f,1f,alpha),startLifetime=TailLifetime,
                    remainingLifetime=TailLifetime-age,randomSeed=(uint)(i*10000+birth*2+strand+1)};
            }
        }
    }
    private void ContactQuad(int index,Vector3 center,Vector3 horizontal,Vector3 vertical,int frame,Color color)
    {
        int v=index*4;contactVertices[v]=center-horizontal-vertical;contactVertices[v+1]=center-horizontal+vertical;
        contactVertices[v+2]=center+horizontal+vertical;contactVertices[v+3]=center+horizontal-vertical;
        Rect cell=contactFrames[frame].rect,crop=contactCrops[frame/4];Texture2D atlas=contactFrames[frame].texture;
        float left=(cell.x+crop.xMin*cell.width)/atlas.width,right=(cell.x+crop.xMax*cell.width)/atlas.width;
        float bottom=(cell.y+crop.yMin*cell.height)/atlas.height,top=(cell.y+crop.yMax*cell.height)/atlas.height;
        contactUv[v]=new Vector2(left,bottom);contactUv[v+1]=new Vector2(left,top);
        contactUv[v+2]=new Vector2(right,top);contactUv[v+3]=new Vector2(right,bottom);
        for(int i=0;i<4;i++)contactColors[v+i]=color;
    }

    private static Vector3 FlatGround(Vector3 position)=>new Vector3(position.x,.03f,position.z);
    private static float Seed(int i)=>Mathf.Repeat(Mathf.Sin(i*12.9898f+4.1414f)*43758.5453f,1f);
    public void FinishNaturally(){autonomous=true;}
    private void Update(){if(!autonomous)return;Advance(Elapsed+CombatSkillClock.Delta);if(Elapsed>=WolfStampedeRules.Duration)Cancel();}
    public void Cancel()
    {
        ReleaseGraphics();
        if(gameObject!=null){gameObject.SetActive(false);if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
    }
    private void ReleaseGraphics()
    {
        DestroyOwned(pixelMaterial);DestroyOwned(wolfMaterial);DestroyOwned(contactMaterial);DestroyOwned(contactMesh);
        pixelMaterial=null;wolfMaterial=null;contactMaterial=null;contactMesh=null;
    }
    private static void DestroyOwned(Object value){if(value!=null){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}}
    private void OnDestroy()=>ReleaseGraphics();
}
