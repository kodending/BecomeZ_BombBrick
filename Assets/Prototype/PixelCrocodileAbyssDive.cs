using UnityEngine;

// One visual clock, no combat callbacks: the pool and splash cannot damage or displace anyone.
public sealed class PixelCrocodileAbyssDive:MonoBehaviour
{
    public const float FadeDuration=.24f;
    public const float EndTime=CrocodileAbyssDiveRules.HitTime+.65f;
    public const int MaximumPixels=1024;
    public const string CrushResource="CombatEffects/Crocodile/AbyssWaterCrush8";
    private const int CrownColumns=24,CrownRows=3,RippleSegments=64;
    private static Sprite[] crushFrames,contactFrames;
    private static readonly Color Water=new Color(.035f,.63f,.8f),Foam=new Color(.7f,.99f,1f),Deep=new Color(.025f,.22f,.38f),Gold=new Color(1f,.83f,.4f);
    private SpriteRenderer source,target;
    private ParticleSystem particles;
    private readonly ParticleSystem.Particle[] pixels=new ParticleSystem.Particle[MaximumPixels];
    private Mesh poolMesh;
    private Transform pool;
    private Material poolMaterial;
    private Material impactMaterial;
    private SpriteRenderer waterCrush,contactFlash;
    private Mesh crownMesh,rippleMesh;
    private MeshRenderer crownRenderer,rippleRenderer;
    private readonly Vector3[] crownVertices=new Vector3[2*(CrownColumns+1)*(CrownRows+1)];
    private readonly Color[] crownColors=new Color[2*(CrownColumns+1)*(CrownRows+1)];
    private readonly Vector3[] rippleVertices=new Vector3[3*(RippleSegments+1)*2];
    private readonly Color[] rippleColors=new Color[3*(RippleSegments+1)*2];
    private readonly Color[] poolColors=new Color[65];
    private readonly Vector3[] poolVertices=new Vector3[65];
    private Vector3 center,mouth,pair,right=Vector3.right,up=Vector3.up;
    private float width,age,fadeAge;
    private bool cinematic,ending,fadeBeforeImpact,frozen,released;
    public float Age=>age;
    public Vector3 Center=>center;
    public ParticleSystem Particles=>particles;
    public float RemainingEndTime=>fadeBeforeImpact?Mathf.Max(0f,FadeDuration-fadeAge):Mathf.Max(0f,(cinematic?4.4f:EndTime)-age);

    public static PixelCrocodileAbyssDive Create(SpriteRenderer source,SpriteRenderer target,Vector3 groundCenter,float width,bool cinematic=false)
    {
        if(source==null||target==null)return null;
        var host=new GameObject("Crocodile Abyss Dive pixel water");
        host.layer=source.gameObject.layer==LayerMask.NameToLayer("ScreenSpaceOutline")?0:source.gameObject.layer;
        var effect=host.AddComponent<PixelCrocodileAbyssDive>();effect.source=source;effect.target=target;
        effect.cinematic=cinematic;effect.center=groundCenter;effect.width=Mathf.Max(2f,width);
        effect.SetView(Camera.main!=null?Camera.main.transform.rotation:source.transform.rotation);
        effect.particles=PixelMushroomGas.CreateParticles("Abyss pixel ripples bubbles and pressure crown",host.transform,
            MaximumPixels,PixelMushroomGas.Material(true),source,24,true);
        effect.particles.gameObject.layer=host.layer;effect.particles.Pause(false);
        Shader shader=Resources.Load<Shader>("CombatCinematics/CrocodileAbyssDive/AbyssSurface")??Shader.Find("Sprites/Default");
        effect.poolMaterial=new Material(shader){name="Crocodile local black water",hideFlags=HideFlags.DontSave};
        effect.poolMaterial.mainTexture=Texture2D.whiteTexture;
        effect.poolMesh=new Mesh{name="Crocodile torn reacting water surface",hideFlags=HideFlags.DontSave};effect.poolMesh.MarkDynamic();
        var uv=new Vector2[65];var triangles=new int[64*3];
        for(int i=0;i<64;i++)
        {
            float angle=i*Mathf.PI/32f;effect.poolVertices[i+1]=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));uv[i+1]=Vector2.one*.5f;
            int index=i*3;triangles[index]=0;triangles[index+1]=i+1;triangles[index+2]=(i+1)%64+1;
        }
        uv[0]=Vector2.one*.5f;effect.poolMesh.vertices=effect.poolVertices;effect.poolMesh.uv=uv;effect.poolMesh.triangles=triangles;
        effect.poolMesh.bounds=new Bounds(Vector3.zero,Vector3.one*3f);
        var poolHost=new GameObject("Local abyss entrance only"){layer=host.layer};poolHost.transform.SetParent(host.transform,false);effect.pool=poolHost.transform;
        poolHost.AddComponent<MeshFilter>().sharedMesh=effect.poolMesh;var renderer=poolHost.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=effect.poolMaterial;renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder-2;
        if(!cinematic)effect.CreateImpactLayers();
        effect.Advance(0f,groundCenter);return effect;
    }

    private void CreateImpactLayers()
    {
        if(crushFrames==null||crushFrames.Length!=8||crushFrames[0]==null)
        {
            var texture=Resources.Load<Texture2D>(CrushResource);
            if(texture!=null)
            {
                crushFrames=new Sprite[8];float w=texture.width/4f,h=texture.height/2f;
                for(int i=0;i<8;i++)
                {
                    crushFrames[i]=Sprite.Create(texture,new Rect(i%4*w,(1-i/4)*h,w,h),new Vector2(.5f,.18f),h,0,SpriteMeshType.FullRect);
                    crushFrames[i].name="Abyss water crush drawn phase "+i;crushFrames[i].hideFlags=HideFlags.HideAndDontSave;
                }
            }
        }
        if(contactFrames==null||contactFrames.Length==0||contactFrames[0]==null)
            contactFrames=PixelCombatEffect.LoadFrames(PixelCombatEffect.CriticalResourcePath);
        impactMaterial=new Material(Shader.Find("Sprites/Default")){name="Crocodile animated water and contact",hideFlags=HideFlags.DontSave};
        waterCrush=ImpactSprite("Eight drawn abyss water crush",source.sortingOrder-1);
        contactFlash=ImpactSprite("Six drawn abyss bite contact",source.sortingOrder+25);
        crownRenderer=ImpactMesh("Curled three dimensional pressure crown",out crownMesh,crownVertices,crownColors,
            CrownColumns,CrownRows,2,source.sortingOrder-1);
        rippleRenderer=ImpactMesh("Broken decelerating contact wakes",out rippleMesh,rippleVertices,rippleColors,
            RippleSegments,1,3,source.sortingOrder-2);
    }

    private SpriteRenderer ImpactSprite(string name,int order)
    {
        var child=new GameObject(name){layer=gameObject.layer};child.transform.SetParent(transform,false);
        var renderer=child.AddComponent<SpriteRenderer>();renderer.sharedMaterial=impactMaterial;
        renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=order;renderer.enabled=false;return renderer;
    }

    private MeshRenderer ImpactMesh(string name,out Mesh mesh,Vector3[] vertices,Color[] colors,int columns,int rows,int panels,int order)
    {
        var triangles=new int[columns*rows*panels*6];int panelVertices=(columns+1)*(rows+1);
        for(int panel=0;panel<panels;panel++)for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)
        {
            int v=panel*panelVertices+row*(columns+1)+column,k=((panel*rows+row)*columns+column)*6;
            triangles[k]=v;triangles[k+1]=v+columns+1;triangles[k+2]=v+1;
            triangles[k+3]=v+1;triangles[k+4]=v+columns+1;triangles[k+5]=v+columns+2;
        }
        mesh=new Mesh{name=name,hideFlags=HideFlags.DontSave};mesh.MarkDynamic();mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;
        var child=new GameObject(name){layer=gameObject.layer};child.transform.SetParent(transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=poolMaterial;renderer.sortingLayerID=source.sortingLayerID;
        renderer.sortingOrder=order;renderer.enabled=false;return renderer;
    }

    public void SetView(Quaternion rotation){right=rotation*Vector3.right;up=rotation*Vector3.up;}
    public void Advance(float elapsed,Vector3 groundCenter)
    {
        if(released||ending)return;
        age=Mathf.Max(0f,elapsed);center=groundCenter;
        if(age>=(cinematic?4.4f:EndTime)){Release();return;}
        CacheAnchors();Draw(1f);
    }
    public void Cancel(bool immediate=false)
    {
        if(released)return;
        if(immediate){Release();return;}
        if(ending||CombatEffectEndPlayback.IsPreserving(this))return;
        FreezeAnchors();ending=true;fadeBeforeImpact=cinematic||age<CrocodileAbyssDiveRules.HitTime;fadeAge=0f;
    }
    public void PrepareEndPlayback()
    {
        FreezeAnchors();if(ending)return;
        fadeBeforeImpact=cinematic||age<CrocodileAbyssDiveRules.HitTime;fadeAge=0f;ending=true;
    }
    public void AdvanceEnd(float dt)
    {
        if(released)return;
        if(fadeBeforeImpact)
        {
            fadeAge+=Mathf.Max(0f,dt);if(fadeAge>=FadeDuration){Release();return;}
            Draw(1f-Mathf.SmoothStep(0f,1f,fadeAge/FadeDuration));return;
        }
        age+=Mathf.Max(0f,dt);if(age>=(cinematic?4.4f:EndTime)){Release();return;}Draw(1f);
    }
    private void Update(){if(ending)AdvanceEnd(Time.unscaledDeltaTime);}
    private void FreezeAnchors(){if(frozen)return;CacheAnchors();frozen=true;}
    private void CacheAnchors()
    {
        if(frozen)return;
        if(source!=null&&source.sprite!=null)
        {
            Bounds visible=PixelMantisReaper.VisibleBody(source.sprite);
            Vector3 point=new Vector3(Mathf.Lerp(visible.min.x,visible.max.x,.88f),Mathf.Lerp(visible.min.y,visible.max.y,.62f),0f);
            if(source.flipX)point.x=-point.x;if(source.flipY)point.y=-point.y;
            mouth=source.transform.TransformPoint(point);pair=VisibleCenter(source);
        }
        if(target!=null&&target.sprite!=null)pair=(pair+VisibleCenter(target))*.5f;
    }
    private static Vector3 VisibleCenter(SpriteRenderer renderer)
    {
        Vector3 point=PixelMantisReaper.VisibleBody(renderer.sprite).center;
        if(renderer.flipX)point.x=-point.x;if(renderer.flipY)point.y=-point.y;
        return renderer.transform.TransformPoint(point);
    }

    // Two separate whips with a short loaded pause; the actor flipbook uses this same clock.
    public static float CinematicRollTurns(float time)=>Whip(1.35f,1.94f,time)+Whip(2.05f,2.64f,time);

    private static float Whip(float from,float to,float time)
    {
        float t=Mathf.InverseLerp(from,to,time);
        return t<.5f?4f*t*t*t:1f-Mathf.Pow(-2f*t+2f,3f)*.5f;
    }

    private void Draw(float opacity)
    {
        int count=0;float after=age-CrocodileAbyssDiveRules.HitTime;
        bool impact=!cinematic&&after>=0f;
        float fade=impact?Mathf.Pow(1f-Mathf.Clamp01(after/.65f),2f):1f;
        float filmTime=cinematic?age:age<CrocodileAbyssDiveRules.ContactTime
            ?Mathf.Lerp(0f,.7f,age/CrocodileAbyssDiveRules.ContactTime)
            :Mathf.Lerp(.7f,4f,Mathf.InverseLerp(CrocodileAbyssDiveRules.ContactTime,CrocodileAbyssDiveRules.HitTime,age));
        float open=Ease(0f,.24f,filmTime);
        float surface=cinematic?1f-Ease(.76f,.98f,filmTime)+Ease(2.94f,3.18f,filmTime):1f;
        float radius=width*(.43f+open*.18f)+(impact?width*.25f*(1f-Mathf.Exp(-after*8f)):0f);
        if(pool!=null)
        {
            pool.gameObject.SetActive(surface>.002f);
            pool.position=center+Vector3.up*.025f;pool.localScale=new Vector3(radius,1f,radius*.69f);
            poolColors[0]=new Color(.001f,.008f,.018f,opacity*fade*.96f*open*surface);
            for(int i=0;i<64;i++)
            {
                float angle=i*Mathf.PI/32f,edge=SurfaceEdge(angle,filmTime);
                poolVertices[i+1]=new Vector3(Mathf.Cos(angle)*edge,0f,Mathf.Sin(angle)*edge);
                Color color=Color.Lerp(Deep,Water,.08f+.08f*Mathf.Sin(angle*5f-filmTime*12f));
                color.a=opacity*fade*.7f*open*surface;poolColors[i+1]=color;
            }
            poolMesh.vertices=poolVertices;poolMesh.colors=poolColors;
        }
        if(surface>.002f)DrawSurface(ref count,radius,filmTime,opacity*fade*open*surface);
        if(impact)DrawImpact(ref count,after,opacity);
        else DrawCinematic(ref count,filmTime,opacity);
        DrawImpactLayers(after,opacity,impact);
        if(particles!=null)particles.SetParticles(pixels,count);
    }

    private static float SurfaceEdge(float angle,float time)=>1f+Mathf.Sin(angle*3f-time*9f)*.065f+
        Mathf.Sin(angle*7f+time*13f)*.04f+Mathf.Sin(angle*11f-time*4f)*.025f;

    private void DrawSurface(ref int count,float radius,float time,float opacity)
    {
        for(int ring=0;ring<2;ring++)for(int i=0;i<96;i++)
        {
            float angle=i*Mathf.PI/48f+time*(ring==0?.9f:-.65f),r=radius*(SurfaceEdge(angle,time)+ring*.16f);
            float crest=.5f+.5f*Mathf.Sin(angle*9f-time*17f);
            Vector3 point=center+new Vector3(Mathf.Cos(angle)*r,.05f+crest*.12f,Mathf.Sin(angle)*r*.69f);
            Color color=Color.Lerp(Water,Foam,crest);color.a=opacity*(ring==0?.86f:.42f)*(i%7==0?.3f:1f);
            Add(ref count,point,color,new Vector3(.065f+crest*.05f,.09f+crest*.075f,.06f));
        }
    }

    private void DrawCinematic(ref int count,float time,float opacity)
    {
        float entry=Window(.06f,.14f,.48f,.71f,time);
        float dive=Window(.67f,.79f,1.2f,1.43f,time);
        float rolling=Window(1.26f,1.42f,2.68f,2.85f,time);
        float rise=Window(2.78f,2.94f,3.38f,3.91f,time);
        if(entry>.002f)DrawEntry(ref count,time,entry*opacity);
        if(dive>.002f)DrawDive(ref count,time,dive*opacity);
        if(rolling>.002f)DrawVortex(ref count,time,rolling*opacity);
        if(rise>.002f)DrawRise(ref count,time,rise*opacity);
    }

    private void DrawEntry(ref int count,float time,float opacity)
    {
        float travel=Mathf.Max(0f,time-.12f);
        for(int i=0;i<96;i++)
        {
            float seed=Hash(i+17),angle=i*2.399963f,spread=travel*(3f+seed*5f);
            Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            Vector3 point=center+direction*(width*.22f+spread)+Vector3.up*(.08f+travel*(3f+seed*4f)-travel*travel*9f);
            Color color=Color.Lerp(Water,Foam,seed);color.a=opacity*(.55f+seed*.4f);
            Add(ref count,point,color,new Vector3(.07f+seed*.06f,.12f+seed*.16f,.08f),seed*150f);
        }
    }

    private void DrawDive(ref int count,float time,float opacity)
    {
        float pull=Ease(.67f,1.24f,time);
        for(int strand=0;strand<4;strand++)for(int segment=0;segment<32;segment++)
        {
            float p=segment/31f,a=strand*Mathf.PI*.5f+p*2.8f-time*8f;
            float radius=width*(.55f-p*.33f),tail=1f-p*.7f;
            Vector3 point=Vector3.Lerp(center+Vector3.up*.3f,pair,.5f)+right*Mathf.Cos(a)*radius+
                up*(Mathf.Sin(a)*radius*.45f+(p-.5f)*width*.7f-pull*.8f);
            Color color=Color.Lerp(Water,Foam,(.5f+.5f*Mathf.Sin(p*29f-time*19f))*.8f);color.a=opacity*tail*.82f;
            Add(ref count,point,color,new Vector3(.09f,.16f+tail*.22f,.08f),a*Mathf.Rad2Deg);
        }
        for(int i=0;i<96;i++)
        {
            float seed=Hash(i+413),phase=Mathf.Repeat(seed+time*1.9f,1f),angle=i*2.399963f-time*6f;
            Vector3 point=pair+right*Mathf.Cos(angle)*width*(.2f+phase*.55f)+up*(phase*3.8f-1f);
            Color color=Color.Lerp(Water,Foam,seed);color.a=opacity*Mathf.Sin(phase*Mathf.PI)*.8f;
            Add(ref count,point,color,new Vector3(.065f,.22f+seed*.3f,.07f),-angle*25f);
        }
    }

    private void DrawVortex(ref int count,float time,float opacity)
    {
        float turns=CinematicRollTurns(time),coil=Ease(2.63f,2.8f,time);
        for(int strand=0;strand<4;strand++)for(int segment=0;segment<48;segment++)
        {
            float p=segment/47f,seed=Hash(strand+921),taper=Mathf.Pow(Mathf.Max(0f,Mathf.Sin(p*Mathf.PI)),.7f);
            Vector2 arc=VortexArc(p,strand,turns,coil,time),tangent=VortexArc(Mathf.Min(1f,p+.01f),strand,turns,coil,time)-
                VortexArc(Mathf.Max(0f,p-.01f),strand,turns,coil,time);
            Vector3 point=pair+right*arc.x+up*arc.y;
            float thickness=width*(.012f+seed*.009f)*taper*(.8f+.2f*Mathf.Sin(p*13f+seed*9f));
            float length=tangent.magnitude*1.6f,rotation=RibbonAngle(tangent);
            Color edge=Deep;edge.a=opacity*taper*.3f;
            Color core=Color.Lerp(Water,Foam,(.5f+.5f*Mathf.Sin(p*18f-time*9f+seed*6f))*.6f);core.a=opacity*taper*.67f;
            Add(ref count,point,edge,new Vector3(thickness*1.8f,length,.06f),rotation);
            Add(ref count,point,core,new Vector3(thickness,length,.05f),rotation);
        }
        for(int i=0;i<96;i++)
        {
            float seed=Hash(i+933),phase=Mathf.Repeat(seed+time*(.7f+seed*.4f),1f),angle=i*2.399963f-turns*7f+phase*4f;
            float radius=width*(.28f+phase*.45f);
            Vector3 point=pair+right*Mathf.Cos(angle)*radius+up*(phase*5.8f-2.4f+Mathf.Sin(angle)*.35f);
            Color color=Foam;color.a=opacity*Mathf.Sin(phase*Mathf.PI)*(.36f+seed*.4f);
            Add(ref count,point,color,Vector3.one*(.04f+seed*.075f),angle*Mathf.Rad2Deg);
        }
        DrawPressureWave(ref count,time-1.79f,opacity);
        DrawPressureWave(ref count,time-2.49f,opacity);
    }

    private Vector2 VortexArc(float p,int strand,float turns,float coil,float time)
    {
        float seed=Hash(strand+921),angle=strand*2.399963f+seed*.65f-turns*Mathf.PI*2f+(p-.5f)*(2.5f+seed*2.3f);
        float radius=width*(.29f+strand*.07f+p*.13f)*(1f-coil*.24f)*
            (1f+Mathf.Sin(angle*3f+seed*8f)*.055f+Mathf.Sin(p*11f+time*3f)*.035f);
        return new Vector2(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.72f+Mathf.Sin(p*Mathf.PI)*width*.035f*Mathf.Sin(seed*9f));
    }

    private void DrawPressureWave(ref int count,float after,float opacity)
    {
        if(after<0f||after>.44f)return;
        float t=after/.44f,radius=width*(.26f+(1f-Mathf.Pow(1f-t,3f))*.75f),fade=(1f-t)*opacity;
        for(int i=0;i<96;i++)
        {
            float seed=Hash(i+177),angle=(i+seed*.6f)*Mathf.PI/48f,r=radius*(.97f+seed*.06f);
            Vector3 point=pair+right*Mathf.Cos(angle)*r+up*Mathf.Sin(angle)*r*.71f;
            Color color=Color.Lerp(Water,Foam,seed);color.a=fade*(.3f+seed*.5f);
            Add(ref count,point,color,new Vector3(.035f+seed*.035f,.08f+seed*.1f,.05f),
                RibbonAngle(new Vector2(-Mathf.Sin(angle),Mathf.Cos(angle)*.71f)));
        }
    }

    private void DrawRise(ref int count,float time,float opacity)
    {
        float after=Mathf.Max(0f,time-2.8f),height=width*(.7f+Ease(2.8f,3.08f,time)*.6f);
        for(int side=-1;side<=1;side+=2)for(int strand=0;strand<3;strand++)for(int segment=0;segment<28;segment++)
        {
            float p=segment/27f,seed=Hash(strand+side*17+51),taper=Mathf.Pow(1f-p,.6f);
            Vector2 arc=RiseArc(p,side,strand,height,after,time),tangent=RiseArc(Mathf.Min(1f,p+.01f),side,strand,height,after,time)-
                RiseArc(Mathf.Max(0f,p-.01f),side,strand,height,after,time);
            Vector3 point=center+right*arc.x+up*arc.y;
            Color color=Color.Lerp(Water,Foam,.15f+seed*.3f+p*.3f);color.a=opacity*(.5f+seed*.2f)*(.25f+taper*.75f);
            Add(ref count,point,color,new Vector3(width*(.009f+taper*(.025f+seed*.012f)),tangent.magnitude*2.05f,.05f),RibbonAngle(tangent));
        }
        for(int i=0;i<48;i++)
        {
            float seed=Hash(i+227),p=Hash(i+9211);int side=i%2==0?-1:1;
            Vector2 arc=RiseArc(p,side,i%3,height,after,time);
            arc.x+=Mathf.Sin(seed*18f+time*6f)*width*.035f;arc.y+=Mathf.Sin(seed*33f+time*5f)*.12f;
            Color color=Foam;color.a=opacity*(.25f+seed*.35f);
            Add(ref count,center+right*arc.x+up*arc.y,color,Vector3.one*(.035f+seed*.055f));
        }
        for(int i=0;i<128;i++)
        {
            float seed=Hash(i+723),angle=i*2.399963f,flight=Mathf.Max(0f,after-seed*.12f);
            Vector3 radial=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            Vector3 point=center+radial*(width*.25f+flight*(2f+seed*4f))+
                Vector3.up*(.06f+flight*(7.5f+seed*4.5f)-flight*flight*15f);
            Color color=Color.Lerp(Water,Foam,seed);color.a=opacity*(.45f+seed*.48f);
            Vector3 velocity=radial*(2f+seed*4f)+Vector3.up*(7.5f+seed*4.5f-flight*30f);
            Add(ref count,point,color,new Vector3(.04f+seed*.05f,.055f+seed*.11f,.05f),
                RibbonAngle(new Vector2(Vector3.Dot(velocity,right),Vector3.Dot(velocity,up))));
        }
    }

    private Vector2 RiseArc(float p,int side,int strand,float height,float after,float time)
    {
        float seed=Hash(strand+side*17+51),bend=Mathf.Sin(p*Mathf.PI);
        float x=side*width*(.16f+seed*.07f+p*.2f+p*p*.3f+Mathf.Sin(p*9f+seed*6f-time*4f)*bend*.035f);
        float y=.06f+(p*1.4f-p*p*.35f+bend*.08f)*height*(.86f+seed*.2f)-after*after*2.3f*p;
        return new Vector2(x,y);
    }

    private static float RibbonAngle(Vector2 tangent)=>Mathf.Atan2(tangent.x,tangent.y)*Mathf.Rad2Deg;

    private void DrawImpactLayers(float after,float opacity,bool impact)
    {
        if(crownRenderer==null)return;
        crownRenderer.enabled=rippleRenderer.enabled=impact;
        waterCrush.enabled=contactFlash.enabled=false;
        if(!impact)return;
        Quaternion facing=Quaternion.LookRotation(Vector3.Cross(right,up),up);
        if(crushFrames!=null&&crushFrames.Length==8)
        {
            int frame=after<.025f?0:after<.05f?1:after<.10f?2:after<.18f?3:after<.28f?4:after<.40f?5:after<.52f?6:7;
            waterCrush.enabled=true;waterCrush.sprite=crushFrames[frame];
            waterCrush.transform.SetPositionAndRotation(center+Vector3.up*.04f,facing);
            waterCrush.transform.localScale=Vector3.one*(width*2.1f/Mathf.Max(.01f,PixelMantisReaper.VisibleBody(crushFrames[3]).size.x));
            waterCrush.color=new Color(1f,1f,1f,opacity*(1f-Ease(.45f,.65f,after)));
        }
        if(after<.18f&&contactFrames!=null&&contactFrames.Length>=4)
        {
            contactFlash.enabled=true;contactFlash.sprite=contactFrames[Mathf.Clamp(Mathf.FloorToInt(after/.18f*contactFrames.Length),0,contactFrames.Length-1)];
            contactFlash.transform.SetPositionAndRotation(mouth,facing);
            contactFlash.transform.localScale=Vector3.one*(Mathf.Min(width*.45f,1.45f)/Mathf.Max(.01f,contactFrames[2].bounds.size.y));
            contactFlash.color=new Color(1f,1f,1f,opacity*(1f-Ease(.09f,.18f,after)));
        }
        DrawPressureCrown(after,opacity);DrawContactWakes(after,opacity);
    }

    private void DrawPressureCrown(float after,float opacity)
    {
        float rise=1f-Mathf.Exp(-after*18f),fall=1f-Ease(.17f,.65f,after),spread=.65f+.35f*(1f-Mathf.Exp(-after*8f));
        float height=width*1.4f*rise*fall,fade=opacity*(1f-Ease(.25f,.65f,after));
        Vector3 groundForward=Vector3.Cross(right,Vector3.up).normalized;
        for(int sideIndex=0;sideIndex<2;sideIndex++)for(int row=0;row<=CrownRows;row++)for(int column=0;column<=CrownColumns;column++)
        {
            float side=sideIndex==0?-1f:1f,p=column/(float)CrownColumns,t=row/(float)CrownRows,angle=Mathf.Lerp(-1.2f,1.2f,p);
            float crest=.83f+.12f*Mathf.Sin(p*19f-after*12f)+.05f*Mathf.Sin(p*37f+after*19f);
            float x=side*width*((.23f+.45f*Mathf.Cos(angle))*spread-t*t*.16f);
            float z=width*Mathf.Sin(angle)*(.35f+.10f*t)+t*t*width*.08f*Mathf.Sin(angle*2f);
            float y=.03f+t*height*(.60f+.40f*Mathf.Cos(angle))*Mathf.Lerp(1f,crest,t*t);
            int index=sideIndex*(CrownColumns+1)*(CrownRows+1)+row*(CrownColumns+1)+column;
            crownVertices[index]=right*x+groundForward*z+Vector3.up*y;
            Color color=t<.66f?Color.Lerp(Deep,Water,t/.66f):Color.Lerp(Water,Foam,(t-.66f)/.34f);
            color.a=fade*Mathf.Pow(Mathf.Max(0f,Mathf.Sin(p*Mathf.PI)),.4f)*Mathf.Lerp(.30f,.76f,t);
            crownColors[index]=color;
        }
        crownRenderer.transform.position=center;crownMesh.vertices=crownVertices;crownMesh.colors=crownColors;crownMesh.RecalculateBounds();
    }

    private void DrawContactWakes(float after,float opacity)
    {
        Vector3 groundForward=Vector3.Cross(right,Vector3.up).normalized;
        for(int band=0;band<3;band++)for(int row=0;row<2;row++)for(int segment=0;segment<=RippleSegments;segment++)
        {
            float local=Mathf.Max(0f,after-band*.04f),progress=Mathf.Clamp01(local/.65f),angle=segment*Mathf.PI*2f/RippleSegments;
            float radius=width*(.48f+.32f*(1f-Mathf.Exp(-local*7f)))+(row==0?-.5f:.5f)*width*.045f*(1f-progress*.7f);
            radius*=SurfaceEdge(angle,after)*(.93f+band*.035f);
            int index=band*(RippleSegments+1)*2+row*(RippleSegments+1)+segment;
            rippleVertices[index]=right*(Mathf.Cos(angle)*radius)+groundForward*(Mathf.Sin(angle)*radius*.69f)+Vector3.up*(.045f+band*.008f);
            float broken=Mathf.Clamp01(Mathf.Sin(angle*5f+band*2.1f-after*3f)*2f);
            Color color=Color.Lerp(Water,Foam,row*.6f);
            color.a=after>=band*.04f?opacity*(1f-Ease(.24f,.65f,after))*broken*(.38f-band*.07f):0f;rippleColors[index]=color;
        }
        rippleRenderer.transform.position=center;rippleMesh.vertices=rippleVertices;rippleMesh.colors=rippleColors;rippleMesh.RecalculateBounds();
    }

    private void DrawImpact(ref int count,float after,float opacity)
    {
        float life=Mathf.Clamp01(after/.65f),fade=opacity*(1f-life)*(1f-life),travel=(1f-Mathf.Exp(-after*13f))/13f;
        // Small curved droplets surround two open side fans; the bite itself remains readable.
        for(int i=0;i<128;i++)
        {
            float seed=Hash(i+321),side=i%2==0?-1f:1f,flight=Mathf.Max(0f,after-seed*.025f),angle=i*2.399963f;
            Vector3 horizontal=right*side*(3f+seed*5f)+Vector3.Cross(right,Vector3.up)*Mathf.Sin(angle)*(1.5f+seed*3f);
            Vector3 point=center+right*side*(width*.25f)+horizontal*((1f-Mathf.Exp(-flight*6f))/6f)+
                Vector3.up*(.08f+flight*(9f+seed*9f)-flight*flight*15f);
            Color color=Color.Lerp(Water,Foam,.25f+seed*.65f);color.a=fade*(.65f+seed*.35f);
            Add(ref count,point,color,new Vector3(.035f+seed*.055f,.055f+seed*.105f,.05f),side*(seed*90f-after*150f));
        }
        for(int i=0;i<128;i++)
        {
            float seed=Hash(i+651),side=i%2==0?-1f:1f,p=Hash(i+671),outward=(1f-Mathf.Exp(-after*8f));
            Vector3 point=center+right*side*(width*(.28f+p*.24f)+outward*(.4f+seed*.8f))+
                Vector3.Cross(right,Vector3.up)*(Mathf.Sin(i*2.399963f)*width*.30f)+
                Vector3.up*(.08f+width*1.2f*(1f-Mathf.Exp(-after*18f))*(1f-Ease(.15f,.58f,after))*p);
            Color color=i%4==0?Foam:Color.Lerp(Water,Foam,.4f);color.a=fade*(.4f+seed*.6f);
            Add(ref count,point,color,Vector3.one*(.025f+seed*.06f),seed*360f);
        }
        for(int i=0;i<96;i++)
        {
            float seed=Hash(i+833),angle=i*2.399963f;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            Vector3 point=center+direction*(width*.34f+travel*(4f+seed*8f))+
                Vector3.up*(.06f+after*(2f+seed*4f)-after*after*9f);
            Color color=Color.Lerp(new Color(.19f,.31f,.33f),new Color(.57f,.64f,.58f),seed);color.a=fade*.75f;
            Add(ref count,point,color,Vector3.one*(.02f+seed*.045f),angle*Mathf.Rad2Deg+after*170f);
        }
        float flash=1f-Mathf.Clamp01(after/.18f);
        for(int i=0;i<16;i++)
        {
            float angle=i*Mathf.PI/8f;Vector3 direction=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
            Color color=i%2==0?Gold:new Color(1f,.97f,.79f);color.a=opacity*flash;
            Add(ref count,mouth+direction*(.06f+after*4.2f),color,new Vector3(.04f,.08f+flash*.12f,.04f),90f-angle*Mathf.Rad2Deg);
        }
    }
    private void Add(ref int count,Vector3 position,Color color,Vector3 size,float rotation=0f)
    {
        if(count>=pixels.Length||color.a<.003f)return;
        pixels[count++]=new ParticleSystem.Particle{position=position,startColor=color,startSize3D=size,rotation=rotation,
            startLifetime=10f,remainingLifetime=10f,velocity=Vector3.zero};
    }
    private static float Hash(int index)=>Mathf.Repeat(Mathf.Sin(index*127.1f+13.7f)*43758.547f,1f);
    private static float Ease(float from,float to,float time)=>Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(from,to,time));
    private static float Window(float from,float full,float leave,float to,float time)=>Ease(from,full,time)*(1f-Ease(leave,to,time));
    private void Release()
    {
        if(released)return;released=true;
        if(particles!=null){particles.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);particles.Clear(false);}
        ReleaseGeometry();
        if(this==null)return;gameObject.SetActive(false);DestroyOwned(gameObject);
    }
    private void ReleaseGeometry()
    {
        DestroyOwned(poolMesh);DestroyOwned(crownMesh);DestroyOwned(rippleMesh);DestroyOwned(poolMaterial);DestroyOwned(impactMaterial);
        poolMesh=crownMesh=rippleMesh=null;poolMaterial=impactMaterial=null;
    }
    private void OnDestroy()=>ReleaseGeometry();
    private static void DestroyOwned(Object value)
    {if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
}
