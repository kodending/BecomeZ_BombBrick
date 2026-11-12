using System.Collections.Generic;
using UnityEngine;

// Presentation only: combat owns the clock, gathering, damage and interruption.
public sealed class PixelSabertoothDevour : MonoBehaviour
{
    public const float FadeDuration=.24f;
    private const int Capacity=320;
    private const string HeadPartsResource="CombatEffects/SabertoothDevour/DevourHeadParts";
    private static readonly int FlashId=Shader.PropertyToID("_Flash");
    private static readonly Rect UpperPartRect=new Rect(16f,326f,736f,672f);
    private static readonly Rect LowerPartRect=new Rect(800f,222f,416f,288f);
    private static readonly Vector2 UpperHinge=new Vector2(366f/736f,213f/672f);
    private static readonly Vector2 LowerHinge=new Vector2(39f/416f,236f/288f);
    private readonly ParticleSystem.Particle[] motes=new ParticleSystem.Particle[Capacity];
    private SpriteRenderer source,upper,lower;
    private ParticleSystem particles;
    private Material material,particleMaterial;
    private Texture2D pixelTexture;
    private Sprite upperSprite,lowerSprite;
    private Transform[] victims;
    private Vector3 center,lastMouth;
    private Quaternion view;
    private float width,height,headWidth,facing,lastElapsed,fadeElapsed,visualScale,cinematicHandoffWidth;
    private float cinematicHandoffElapsed=-1f;
    private bool cancelling,released;
    private static readonly Color Mint=new Color(.57f,1f,.78f),Gold=new Color(1f,.69f,.18f);

    public SpriteRenderer UpperJaw=>upper;
    public SpriteRenderer LowerJaw=>lower;
    public SpriteRenderer Head=>upper;
    public ParticleSystem Particles=>particles;
    public float Age=>lastElapsed;
    public Vector3 Mouth=>lastMouth;

    public static PixelSabertoothDevour Create(SpriteRenderer source,Vector3 forward,Vector3 center,
        float width,float height,List<Transform> victims,float visualScale=2.2f)
    {
        if(source==null)return null;
        Texture2D parts=Resources.Load<Texture2D>(HeadPartsResource);
        Shader neon=Resources.Load<Shader>("CombatEffects/SabertoothDevour/DevourNeon");
        if(parts==null||neon==null||parts.width<LowerPartRect.xMax||parts.height<UpperPartRect.yMax)return null;
        var root=new GameObject("Sabertooth Apex Devour");
        root.layer=source.gameObject.layer==LayerMask.NameToLayer("ScreenSpaceOutline")?0:source.gameObject.layer;
        var fx=root.AddComponent<PixelSabertoothDevour>();fx.source=source;fx.center=center;
        fx.width=Mathf.Max(.75f,width);fx.height=Mathf.Max(.7f,height);
        fx.visualScale=Mathf.Max(.1f,visualScale);
        fx.headWidth=Mathf.Max(1.25f,source.bounds.size.x*2.4f);
        fx.view=Camera.main==null?source.transform.rotation:Camera.main.transform.rotation;
        fx.facing=Vector3.Dot(forward,fx.view*Vector3.right)<0f?-1f:1f;
        int count=victims==null?0:Mathf.Min(8,victims.Count);
        fx.victims=new Transform[count];for(int i=0;i<count;i++)fx.victims[i]=victims[i];
        fx.pixelTexture=new Texture2D(2,2,TextureFormat.RGBA32,false)
            {name="Devour crisp particle pixels",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        fx.pixelTexture.SetPixels32(new[]{new Color32(255,255,255,255),new Color32(255,255,255,255),
            new Color32(255,255,255,255),new Color32(255,255,255,255)});fx.pixelTexture.Apply(false,true);
        fx.material=new Material(neon)
            {name="Devour neon shadow head",mainTexture=parts,hideFlags=HideFlags.HideAndDontSave};
        fx.particleMaterial=new Material(neon)
            {name="Devour mint and gold pixel aura",mainTexture=fx.pixelTexture,hideFlags=HideFlags.HideAndDontSave};
        fx.particleMaterial.SetFloat("_ParticleMode",1f);
        fx.particleMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.One);
        fx.upperSprite=Sprite.Create(parts,UpperPartRect,UpperHinge,128f,0,SpriteMeshType.FullRect);
        fx.lowerSprite=Sprite.Create(parts,LowerPartRect,LowerHinge,128f,0,SpriteMeshType.FullRect);
        fx.upperSprite.name="Devour shaded sabertooth head";
        fx.lowerSprite.name="Devour shaded sabertooth lower jaw";
        fx.upper=fx.CreateJaw("Sabertooth hologram head",fx.upperSprite);
        fx.lower=fx.CreateJaw("Sabertooth hinged lower jaw",fx.lowerSprite);
        var stream=new GameObject("Devour mint and gold pixels");stream.layer=root.layer;
        stream.transform.SetParent(root.transform,false);fx.particles=stream.AddComponent<ParticleSystem>();
        fx.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=fx.particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=Capacity;
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0f;main.startSize3D=true;
        var emission=fx.particles.emission;emission.enabled=false;
        var shape=fx.particles.shape;shape.enabled=false;
        var renderer=stream.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=fx.particleMaterial;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.alignment=ParticleSystemRenderSpace.View;
        renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder+40;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        fx.particles.Play(false);fx.particles.Pause(false);
        fx.Advance(0f,source.bounds.center);return fx;
    }

    private SpriteRenderer CreateJaw(string name,Sprite sprite)
    {
        var child=new GameObject(name);child.layer=gameObject.layer;child.transform.SetParent(transform,false);
        var renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=material;
        renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder+1;
        renderer.enabled=false;return renderer;
    }

    public void Advance(float elapsed,Vector3 mouth,float handoffElapsed=-1f)
    {
        if(released||cancelling)return;
        float age=Mathf.Max(0f,elapsed);
        if(handoffElapsed>=0f)cinematicHandoffElapsed=handoffElapsed;
        else if(cinematicHandoffElapsed>=0f)cinematicHandoffElapsed+=Mathf.Max(0f,age-lastElapsed);
        lastElapsed=age;lastMouth=mouth;
        if(lastElapsed>=SabertoothDevourRules.Duration){Cancel(true);return;}
        Draw(lastElapsed,mouth,1f);
    }

    public void BeginCinematicHandoff(float jawWidth)
    {
        cinematicHandoffWidth=Mathf.Max(0f,jawWidth);cinematicHandoffElapsed=-1f;
    }

    public void Cancel(bool immediate=false)
    {
        if(released)return;
        if(immediate){DestroyEffect();return;}
        if(CombatEffectEndPlayback.IsPreserving(this))return;
        if(cancelling)return;
        cancelling=true;fadeElapsed=0f;
    }

    private void Update()
    {
        if(cancelling)AdvanceFade(Time.unscaledDeltaTime);
    }

    private void AdvanceFade(float dt)
    {
        if(released||!cancelling)return;
        fadeElapsed+=Mathf.Max(0f,dt);
        if(fadeElapsed>=FadeDuration){DestroyEffect();return;}
        Draw(lastElapsed,lastMouth,1f-Mathf.SmoothStep(0f,1f,fadeElapsed/FadeDuration));
    }

    private void Draw(float elapsed,Vector3 mouth,float opacity)
    {
        Vector3 right=view*Vector3.right,up=view*Vector3.up;
        float reveal=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.05f,.43f,elapsed));
        float snap=Mathf.Clamp01(Mathf.InverseLerp(SabertoothDevourRules.SnapStart,
            SabertoothDevourRules.HitTime,elapsed));
        snap=1f-(1f-snap)*(1f-snap)*(1f-snap);
        float after=Mathf.Max(0f,elapsed-SabertoothDevourRules.HitTime);
        float recoveryStart=Mathf.Max(SabertoothDevourRules.SnapStart+SabertoothDevourRules.SnapDuration,
            SabertoothDevourRules.HitTime+SabertoothDevourRules.BiteClampHold);
        float recovery=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(
            recoveryStart,SabertoothDevourRules.Duration,elapsed));
        float opening=reveal*(1f-snap);
        float growth=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(
            SabertoothDevourRules.PullStart,SabertoothDevourRules.PullEnd,elapsed));
        float finalWidth=Mathf.Max(width*.9f,(Mathf.Abs(Vector3.Dot(center-mouth,right))+width*.3f)/.46f);
        float currentWidth=Mathf.Lerp(Mathf.Min(headWidth,finalWidth),finalWidth,growth)*visualScale;
        // The opening can hold at .45 while the live cast clock starts at zero.
        float handoffTime=cinematicHandoffElapsed>=0f?cinematicHandoffElapsed:elapsed-SabertoothDevourRules.PullStart;
        float handoff=cinematicHandoffWidth>0f?Mathf.SmoothStep(0f,1f,Mathf.Clamp01(handoffTime/.5f)):1f;
        if(cinematicHandoffWidth>0f)currentWidth=Mathf.Lerp(cinematicHandoffWidth,currentWidth,handoff);
        float hingeGrowth=Mathf.Lerp(1f,growth,handoff);
        Vector3 hinge=mouth-right*facing*currentWidth*.12f*(1f-hingeGrowth);
        float compression=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(after/.025f))*
            (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(SabertoothDevourRules.BiteClampHold,
                SabertoothDevourRules.BiteClampHold+.18f,after)));
        hinge+=right*facing*.14f*compression;
        float size=currentWidth/upperSprite.bounds.size.x*(.86f+.14f*reveal);
        Vector3 scale=new Vector3(facing*size,size,1f);
        upper.transform.SetPositionAndRotation(hinge,view*Quaternion.Euler(0f,0f,facing*opening*6f));
        lower.transform.SetPositionAndRotation(hinge,view*Quaternion.Euler(0f,0f,-facing*opening*34f));
        upper.transform.localScale=scale;lower.transform.localScale=scale;
        float flash=elapsed>=SabertoothDevourRules.HitTime?
            1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.035f,.15f,after)):snap*.35f;
        material.SetFloat(FlashId,flash);
        Color tint=Color.Lerp(new Color(.83f,1f,.94f),new Color(1f,.8f,.32f),flash);
        tint.a=(Mathf.Lerp(.32f,Mathf.Lerp(.46f,.32f,growth),handoff)+flash*.34f)*reveal*recovery*opacity;
        upper.color=tint;lower.color=tint;upper.enabled=lower.enabled=tint.a>.001f;
        Vector3 inlet=hinge+right*facing*currentWidth*.46f-up*currentWidth*.085f;
        float air=(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(
            SabertoothDevourRules.PullEnd,SabertoothDevourRules.PullEnd+.16f,elapsed)))*
            Mathf.Clamp01(elapsed/.12f)*opacity;
        int count=0;
        if(air>.001f)DrawInhale(ref count,elapsed,inlet,right,up,air);
        if(elapsed<SabertoothDevourRules.HitTime)
        {
            float swirl=Mathf.Max(air*.7f,reveal*recovery*opacity*.35f);
            float charge=Mathf.InverseLerp(SabertoothDevourRules.PullEnd,SabertoothDevourRules.HitTime,elapsed);
            for(int i=0;i<32;i++)
            {
                float seed=Hash(i+371),progress=Mathf.Repeat(elapsed*(.9f+seed*.3f)+seed,1f);
                float radius=.06f+(1f-progress)*Mathf.Min(.7f,height*.23f);
                float angle=elapsed*7f+i*2.399963f;
                Vector3 point=inlet+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle)*.65f)*radius;
                Color color=Color.Lerp(Mint,Gold,charge*.75f);color.a=swirl*Mathf.Sin(progress*Mathf.PI);
                Add(ref count,point,color,new Vector3(.055f,.13f,1f),-angle*Mathf.Rad2Deg);
            }
        }
        if(elapsed>=SabertoothDevourRules.HitTime&&after<.34f)
            DrawImpact(ref count,after,right,up,opacity,mouth);
        particles.SetParticles(motes,count);
    }

    private void DrawInhale(ref int count,float elapsed,Vector3 inlet,Vector3 right,Vector3 up,float air)
    {
        for(int stream=0;stream<8;stream++)
        {
            float streamSeed=Hash(stream+131),side=stream%2==0?1f:-1f;
            Transform victim=victims.Length==0?null:victims[stream%victims.Length];
            Vector3 start=victim==null?center+right*((streamSeed-.5f)*width):
                victim.position+Vector3.up*.35f+right*((streamSeed-.5f)*width*.12f);
            start+=up*((Hash(stream+53)-.5f)*height*.55f);
            Vector3 control=Vector3.Lerp(start,inlet,.52f)+up*side*(.25f+streamSeed*.3f)*height;
            for(int segment=0;segment<14;segment++)
            {
                float seed=Hash(stream*14+segment);
                float progress=Mathf.Repeat(elapsed*(.7f+streamSeed*.18f)+segment/14f+streamSeed,1f);
                Vector3 point=Bezier(start,control,inlet,progress);
                Vector3 tangent=Vector3.Lerp(control-start,inlet-control,progress);
                float angle=Mathf.Atan2(Vector3.Dot(tangent,up),Vector3.Dot(tangent,right))*Mathf.Rad2Deg;
                float ends=Mathf.SmoothStep(0f,1f,progress/.1f)*Mathf.SmoothStep(0f,1f,(1f-progress)/.12f);
                Color halo=Mint;halo.a=air*ends*.2f;
                Color core=Color.Lerp(Mint,new Color(1f,.98f,.83f),.12f+streamSeed*.18f);
                core.a=air*ends*(.58f+seed*.18f);
                Add(ref count,point,halo,new Vector3(.12f+seed*.025f,.23f+seed*.2f,1f),90f-angle);
                Add(ref count,point,core,new Vector3(.065f+seed*.035f,.18f+seed*.2f,1f),90f-angle);
                if(segment%5==0)
                {
                    Color fleck=new Color(.94f,1f,.84f,air*ends*.8f);
                    Vector3 offset=up*Mathf.Sin(elapsed*6f+stream+segment)*.045f;
                    Add(ref count,point+offset,fleck,Vector3.one*(.045f+seed*.045f),elapsed*65f+seed*90f);
                }
            }
        }
    }

    private void DrawImpact(ref int count,float after,Vector3 right,Vector3 up,float opacity,Vector3 mouth)
    {
        float life=after/.34f,fade=(1f-life)*(1f-life)*opacity;
        Vector3 impact=new Vector3(center.x,Mathf.Max(mouth.y,center.y+.35f),center.z)+up*.12f;
        float flight=(1f-Mathf.Exp(-after*9f))/9f;
        for(int i=0;i<144;i++)
        {
            float seed=Hash(i+219),angle=i*2.399963f;
            Vector3 direction=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
            Vector3 point=impact+right*((Hash(i+71)-.5f)*width*.45f)+
                direction*(.15f+flight*(7f+seed*7f))-up*after*after*2.3f;
            Color color=i%4==0?new Color(1f,.96f,.73f):Color.Lerp(Gold,new Color(1f,.86f,.38f),seed);
            color.a=fade*(.68f+seed*.28f);
            Add(ref count,point,color,new Vector3(.09f+seed*.085f,i%3==0?.4f:.15f,1f),90f-angle*Mathf.Rad2Deg);
        }
        float ringRadius=Mathf.Max(.55f,width*.24f)+after*5f;
        Vector3 flatRight=new Vector3(right.x,0f,right.z).normalized;
        if(flatRight.sqrMagnitude<.001f)flatRight=Vector3.right;
        Vector3 flatForward=Vector3.Cross(flatRight,Vector3.up);
        for(int i=0;i<48;i++)
        {
            float angle=i*(Mathf.PI*2f/48f);
            Vector3 point=center+Vector3.up*.12f+
                (flatRight*Mathf.Cos(angle)+flatForward*Mathf.Sin(angle))*ringRadius;
            Vector3 tangent=-flatRight*Mathf.Sin(angle)+flatForward*Mathf.Cos(angle);
            float rotation=90f-Mathf.Atan2(Vector3.Dot(tangent,up),Vector3.Dot(tangent,right))*Mathf.Rad2Deg;
            var halo=Gold;halo.a=fade*.28f;
            var core=new Color(1f,.9f,.48f,fade*.9f);
            float length=.17f+after*.5f;
            Add(ref count,point,halo,new Vector3(.24f,length+.05f,1f),rotation);
            Add(ref count,point,core,new Vector3(.1125f,length,1f),rotation);
        }
        float glint=1f-Mathf.Clamp01(after/.14f);
        if(glint>.001f)
        {
            for(int i=0;i<8;i++)
            {
                float angle=i*(Mathf.PI*.25f),length=Mathf.Clamp(2f+height*.12f,2f,2.6f)*(i%2==0?1f:.62f);
                Vector3 direction=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                Vector3 point=impact+direction*length*.27f;
                var halo=Gold;halo.a=glint*opacity*.9f;
                var core=new Color(1f,.97f,.8f,glint*opacity*.9f);
                Add(ref count,point,halo,new Vector3(.21f,length,1f),90f-angle*Mathf.Rad2Deg);
                Add(ref count,point,core,new Vector3(.075f,length*.76f,1f),90f-angle*Mathf.Rad2Deg);
            }
        }
    }

    private void Add(ref int count,Vector3 point,Color color,Vector3 size,float rotation)
    {
        motes[count++]=new ParticleSystem.Particle {position=point,startColor=color,startSize3D=size,
            startLifetime=1f,remainingLifetime=1f,rotation=rotation,velocity=Vector3.zero};
    }

    private static float Hash(int value)=>Mathf.Repeat(Mathf.Sin(value*127.1f+13.7f)*43758.547f,1f);
    private static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,float t)=>
        a*((1f-t)*(1f-t))+b*(2f*(1f-t)*t)+c*(t*t);

    private void DestroyEffect()
    {
        ReleaseOwned();gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void OnDestroy()=>ReleaseOwned();
    private void ReleaseOwned()
    {
        if(released)return;released=true;
        if(particles!=null){particles.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);particles.Clear(false);}
        if(upper!=null)upper.enabled=false;if(lower!=null)lower.enabled=false;
        DestroyOwned(upperSprite);DestroyOwned(lowerSprite);DestroyOwned(pixelTexture);
        DestroyOwned(material);DestroyOwned(particleMaterial);
    }
    private static void DestroyOwned(Object value)
    {
        if(value==null)return;
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }
}
