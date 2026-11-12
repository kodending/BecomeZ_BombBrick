using UnityEngine;

// One battle clock: passing dragon, a single falling mass and its growing shadow, then impact.
public sealed class PixelWyvernHeavenBreath:MonoBehaviour
{
    private static Sprite dragonSprite,whiteSprite;
    private SpriteRenderer shadow,ground,landingShadow;
    private Material projection;
    private ParticleSystem breath,debris;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[288];
    private Bounds arena;
    private Vector3 center,forward;
    private float span,travel,height;
    private Color tint;
    private Camera shadowCamera;
    private float shadowDepth;
    private bool absorbing;
    private float age,absorptionStarted;
    private Vector3 absorptionAnchor,absorptionOrigin;
    private ParticleSystem.Particle[] absorptionParticles;
    private int absorptionCount;
    public Vector3 FallOrigin=>center+Vector3.up*height;

    public static PixelWyvernHeavenBreath Create(Bounds arena,SpriteRenderer reference,int order,int element)
    {
        var fx=new GameObject("Heavenly Dragon Breath").AddComponent<PixelWyvernHeavenBreath>();
        fx.arena=arena;fx.center=new Vector3(arena.center.x,.06f,arena.center.z);fx.tint=PixelWyvernBreath.Tint(element);
        var camera=Camera.main;
        fx.shadowCamera=camera;
        if(camera!=null)fx.shadowDepth=Mathf.Max(camera.nearClipPlane+1f,Vector3.Dot(fx.center-camera.transform.position,camera.transform.forward));
        fx.forward=camera==null?Vector3.forward:Vector3.ProjectOnPlane(camera.transform.up,Vector3.up).normalized;
        if(fx.forward.sqrMagnitude<.5f)fx.forward=Vector3.forward;
        fx.span=arena.size.x*1.13f;fx.travel=(fx.span+arena.size.z)*.55f;
        fx.height=12f;
        if(camera!=null)for(;fx.height<48f;fx.height+=2f)
            if(camera.WorldToViewportPoint(new Vector3(arena.center.x,fx.height,arena.min.z)).y>1.15f)break;
        if(dragonSprite==null)
        {
            var texture=Resources.Load<Texture2D>("CombatEffects/Wyvern/HeavenDragonShadow");
            if(texture!=null){dragonSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width);dragonSprite.hideFlags=HideFlags.HideAndDontSave;}
        }
        if(whiteSprite==null){whiteSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f),Texture2D.whiteTexture.width);whiteSprite.hideFlags=HideFlags.HideAndDontSave;}
        fx.projection=new Material(Shader.Find("BombBrick/Wyvern Battlefield Projection"));
        fx.projection.SetVector("_Arena",new Vector4(arena.min.x,arena.min.z,arena.max.x,arena.max.z));
        fx.shadow=fx.Projected("Passing Dragon Shadow",dragonSprite,reference,order);
        var shadowProperties=new MaterialPropertyBlock();fx.shadow.GetPropertyBlock(shadowProperties);
        shadowProperties.SetFloat("_ClipToArena",0f);
        if(dragonSprite!=null)shadowProperties.SetTexture("_MainTex",dragonSprite.texture);
        fx.shadow.SetPropertyBlock(shadowProperties);
        fx.shadow.transform.rotation=Quaternion.LookRotation(Vector3.down,fx.forward);
        fx.shadow.transform.localScale=Vector3.one*fx.span;
        fx.ground=fx.Projected("Breath Ground Light",whiteSprite,reference,order+1);
        fx.ground.transform.position=fx.center;fx.ground.transform.rotation=Quaternion.Euler(90f,0,0);
        fx.ground.transform.localScale=new Vector3(arena.size.x,arena.size.z,1);
        fx.landingShadow=fx.Projected("Heaven Breath Landing Shadow",PixelMonkeyMortar.ShadowSprite(),reference,1);
        fx.landingShadow.transform.position=fx.center;
        fx.landingShadow.transform.rotation=Quaternion.Euler(90f,0,0);
        var landingProperties=new MaterialPropertyBlock();fx.landingShadow.GetPropertyBlock(landingProperties);
        landingProperties.SetTexture("_MainTex",fx.landingShadow.sprite.texture);
        fx.landingShadow.SetPropertyBlock(landingProperties);
        fx.breath=PixelMushroomGas.CreateParticles("Heaven Breath Mass",fx.transform,288,PixelMushroomGas.Material(false),reference,0,true);
        fx.debris=PixelMushroomGas.CreateParticles("Heaven Breath Fragments",fx.transform,180,PixelMushroomGas.Material(true),reference,0,true);
        fx.breath.GetComponent<ParticleSystemRenderer>().sortingOrder=order+2;
        fx.debris.GetComponent<ParticleSystemRenderer>().sortingOrder=order+3;
        fx.Advance(0f);return fx;
    }

    private SpriteRenderer Projected(string name,Sprite sprite,SpriteRenderer reference,int order)
    {
        var r=new GameObject(name).AddComponent<SpriteRenderer>();r.transform.SetParent(transform,false);
        r.sprite=sprite;r.sharedMaterial=projection;r.sortingLayerID=reference==null?0:reference.sortingLayerID;r.sortingOrder=order;return r;
    }
    private static float Seed(int i)=>Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.5453f,1f);
    private static ParticleSystem.Particle Particle(Vector3 p,Vector3 size,Color color,int i)=>
        new ParticleSystem.Particle {position=p,startSize3D=size,startColor=CombatFlashPresentation.Peak(color),startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(i+1)};

    public void Advance(float time)
    {
        if(this==null)return;
        age=time;
        if(absorbing){PoseAbsorption(time);return;}
        float sweep=Mathf.Clamp01(time/WyvernHeavenBreathRules.ShadowDuration);
        shadow.enabled=time<WyvernHeavenBreathRules.ShadowDuration;
        if(shadowCamera!=null)
        {
            // Screen-space passage includes the environment, with both ends outside the viewport.
            Vector3 middle=shadowCamera.ViewportToWorldPoint(new Vector3(.5f,.5f,shadowDepth));
            float viewHeight=Vector3.Distance(shadowCamera.ViewportToWorldPoint(new Vector3(.5f,0f,shadowDepth)),
                shadowCamera.ViewportToWorldPoint(new Vector3(.5f,1f,shadowDepth)));
            float width=viewHeight*shadowCamera.aspect*1.08f;
            shadow.transform.rotation=shadowCamera.transform.rotation;
            shadow.transform.localScale=Vector3.one*width;
            shadow.transform.position=middle+shadowCamera.transform.up*Mathf.Lerp(-(viewHeight+width)*.5f,(viewHeight+width)*.5f,sweep);
        }
        else shadow.transform.position=center+forward*Mathf.Lerp(-travel,travel,sweep);
        shadow.color=new Color(.025f,.025f,.05f,.52f*Mathf.Clamp01(time/.055f)*Mathf.Clamp01((WyvernHeavenBreathRules.ShadowDuration-time)/.055f));
        float drop=time-WyvernHeavenBreathRules.ShadowDuration-WyvernHeavenBreathRules.PauseDuration;
        float after=time-WyvernHeavenBreathRules.ImpactTime;
        Color glow=Color.Lerp(tint,Color.white,after>=0f?.6f:.12f);
        glow.a=after<0f?0f:.32f*Mathf.Clamp01(1f-after/.10f);
        ground.color=CombatFlashPresentation.Flash(glow);ground.enabled=glow.a>0f;
        float warning=Mathf.Clamp01(drop/WyvernHeavenBreathRules.FallDuration);
        float growth=Mathf.Lerp(.08f,1.8f,Mathf.SmoothStep(0f,1f,warning));
        landingShadow.enabled=drop>=0f&&after<0f;
        landingShadow.transform.localScale=new Vector3(arena.size.x*growth,arena.size.z*growth,1f);
        landingShadow.color=new Color(.025f,.02f,.045f,Mathf.Lerp(.10f,.64f,warning));
        if(drop<0f){breath.SetParticles(particles,0);debris.SetParticles(particles,0);return;}
        float fall=Mathf.Clamp01(drop/WyvernHeavenBreathRules.FallDuration);
        float cell=Mathf.Max(arena.size.x/24f,arena.size.z/12f);
        float radius=Mathf.Max(arena.size.x*.30f,arena.size.z*.45f);
        Vector3 right=shadowCamera==null?Vector3.right:shadowCamera.transform.right;
        Vector3 up=shadowCamera==null?Vector3.up:shadowCamera.transform.up;
        Vector3 head=center+up*(radius*.98f)+Vector3.up*(height*(1f-fall*fall));
        float spread=1f-Mathf.Pow(1f-Mathf.Clamp01(after/.10f),3f);
        for(int i=0;i<288;i++)
        {
            float seed=Seed(i),x=(i%24+.3f+seed*.4f)/24f,z=(i/24+.3f+Seed(i+451)*.4f)/12f;
            Vector3 p;float size;Color color;
            if(after<0f)
            {
                // All puffs overlap one shared core. No separate columns or randomly staggered falls.
                float ring=Mathf.Sqrt((i%240+.5f)/240f),angle=i*2.399963f;
                p=head+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius*ring*.78f;
                size=radius*(.35f+seed*.09f);
                if(i>=240)
                {
                    float tail=(i-240)/48f;
                    p=head+Vector3.up*(radius*(.45f+tail*1.3f))+right*Mathf.Sin(angle)*radius*.3f*(1f-tail);
                    size=radius*(.65f-tail*.48f);
                }
                color=Color.Lerp(tint,new Color(1f,.98f,.84f),i>=240?.25f:(1f-ring)*.65f+seed*.10f);
                color.a=(i>=240?.55f:.94f)*Mathf.Clamp01(drop/.04f);
            }
            else
            {
                Vector3 destination=new Vector3(Mathf.Lerp(arena.min.x,arena.max.x,x),.2f,Mathf.Lerp(arena.min.z,arena.max.z,z));
                p=Vector3.Lerp(center,destination,spread);
                Vector3 outward=Vector3.ProjectOnPlane(p-center,Vector3.up).normalized;
                p+=outward*Mathf.Min(after,.18f)*(3f+seed*5f);
                p.y=.18f+Mathf.Sin(Mathf.Clamp01(after/.35f)*Mathf.PI)*(.3f+seed*.75f);
                size=cell*(1.6f+seed*.55f);
                color=Color.Lerp(tint,new Color(1f,.98f,.84f),.25f+seed*.4f+Mathf.Clamp01(1f-after/.1f)*.25f);
                color.a=.85f*Mathf.Pow(Mathf.Clamp01(1f-after/.3f),1.4f);
            }
            Vector3 shape=new Vector3(size,size*(after<0f?1f:.7f),size);
            particles[i]=Particle(p,shape,color,i);
        }
        breath.SetParticles(particles,288);
        if(after<0f){debris.SetParticles(particles,0);return;}
        for(int i=0;i<180;i++)
        {
            float seed=Seed(i+511),along=(i/4+.5f)/45f;int edge=i%4;
            Vector3 p=edge<2?new Vector3(Mathf.Lerp(arena.min.x,arena.max.x,along),.1f,edge==0?arena.min.z:arena.max.z):
                new Vector3(edge==2?arena.min.x:arena.max.x,.1f,Mathf.Lerp(arena.min.z,arena.max.z,along));
            Vector3 outward=(p-center).normalized;
            p=Vector3.Lerp(center,p,1f-Mathf.Pow(1f-Mathf.Clamp01(after/.09f),3f));
            p+=outward*(1f-Mathf.Pow(1f-Mathf.Clamp01(after/.25f),3f))*(.5f+seed*1.7f);
            p.y+=Mathf.Max(0f,after*(3f+seed*3f)-after*after*10f);
            Color color=Color.Lerp(tint,Color.white,.35f+seed*.4f);color.a=.9f*Mathf.Clamp01(1f-after/.55f);
            particles[i]=Particle(p,Vector3.one*(.07f+seed*.12f),color,i);particles[i].rotation=i*47f+after*160f;
        }
        debris.SetParticles(particles,180);
    }

    public void AbsorbTo(Vector3 anchor)
    {
        if(this==null||absorbing)return;
        Advance(Mathf.Min(WyvernHeavenBreathRules.ImpactTime-.001f,
            Mathf.Max(age,WyvernHeavenBreathRules.ShadowDuration+WyvernHeavenBreathRules.PauseDuration+.001f)));
        absorbing=true;absorptionStarted=age;absorptionAnchor=anchor;
        absorptionParticles=new ParticleSystem.Particle[288];absorptionCount=breath.GetParticles(absorptionParticles);
        absorptionOrigin=Vector3.zero;
        for(int i=0;i<absorptionCount;i++)absorptionOrigin+=absorptionParticles[i].position;
        if(absorptionCount>0)absorptionOrigin/=absorptionCount;
        shadow.enabled=ground.enabled=landingShadow.enabled=false;debris.Clear(false);
    }

    private void PoseAbsorption(float time)
    {
        float intake=Mathf.Clamp01((time-absorptionStarted)/.28f);
        for(int i=0;i<absorptionCount;i++)
        {
            var particle=absorptionParticles[i];
            particle.position=GriffinStormOrbRules.AbsorbPosition(particle.position,absorptionOrigin,absorptionAnchor,intake);
            particle.startSize3D*=1f-intake*.97f;
            Color color=particle.startColor;color.a*=1f-intake;particle.startColor=color;particles[i]=particle;
        }
        breath.SetParticles(particles,intake>=1f?0:absorptionCount);debris.SetParticles(particles,0);
    }

    public void Cancel()
    {
        if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void OnDestroy()
    {
        if(projection!=null){if(Application.isPlaying)Destroy(projection);else DestroyImmediate(projection);}
    }
}
