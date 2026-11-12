using UnityEngine;

// Individual pixel particles follow past wing poses; no wing sprite or continuous ribbon is drawn.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(10030)]
public sealed class PixelRaptorApexWings : MonoBehaviour
{
    public const float RevealDuration=.65f;
    public const float FadeDuration=.4f;
    public const float MaximumFollowDelay=.38f;
    public const int ParticleCapacity=256;
    private const int FeathersPerWing=5,Feathers=FeathersPerWing*2,HistoryCapacity=128;
    private static Material particleMaterial;
    private SpriteRenderer source;
    private Transform auraRoot;
    private ParticleSystem particles;
    private ParticleSystemRenderer particleRenderer;
    private readonly ParticleSystem.Particle[] lightParticles=new ParticleSystem.Particle[ParticleCapacity];
    private readonly WingPose[] poses=new WingPose[HistoryCapacity];
    private bool awakened,foreground,cinematic,visible;
    private float reveal,fade,age,opacity;
    private int poseHead,poseCount;
    private Vector3? backAnchor;
    private static readonly Color Gold=new Color(1f,.64f,.16f),Ivory=new Color(1f,.91f,.52f);

    private struct WingPose
    {
        public float time;
        public Matrix4x4 matrix;
    }

    public bool IsAwakened=>awakened;
    public bool IsVisible=>visible;
    public float Reveal=>reveal;
    public float Opacity=>opacity;
    public ParticleSystem WingParticles=>particles;
    public Transform AuraRoot=>auraRoot;

    public static PixelRaptorApexWings Attach(SpriteRenderer source)
    {
        if(source==null)return null;
        var effect=source.GetComponent<PixelRaptorApexWings>();
        if(effect!=null)return effect;
        Warmup();effect=source.gameObject.AddComponent<PixelRaptorApexWings>();effect.source=source;
        var root=new GameObject("Apex Predator Delayed Pixel Wings");
        root.layer=EffectLayer(source);root.transform.SetParent(source.transform,false);effect.auraRoot=root.transform;
        var stream=new GameObject("Flowing Golden Pixel Particles");stream.layer=root.layer;
        stream.transform.SetParent(root.transform,false);effect.particles=stream.AddComponent<ParticleSystem>();
        effect.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=effect.particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=ParticleCapacity;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.simulationSpeed=1f;
        main.scalingMode=ParticleSystemScalingMode.Hierarchy;
        main.startSpeed=0f;main.startSize3D=true;main.startRotation3D=true;
        var emission=effect.particles.emission;emission.enabled=false;
        var shape=effect.particles.shape;shape.enabled=false;
        effect.particleRenderer=stream.GetComponent<ParticleSystemRenderer>();
        effect.particleRenderer.sharedMaterial=particleMaterial;
        effect.particleRenderer.renderMode=ParticleSystemRenderMode.Billboard;
        effect.particleRenderer.alignment=ParticleSystemRenderSpace.View;
        effect.particleRenderer.sortMode=ParticleSystemSortMode.None;effect.particleRenderer.enabled=false;
        effect.particles.Play(false);effect.particles.Pause(false);
        effect.SyncFollow();return effect;
    }

    public static void Warmup()
    {
        if(particleMaterial==null)
            particleMaterial=new Material(Resources.Load<Shader>("CombatCinematics/RaptorApex/ApexAuraParticle"))
                {name="Raptor Apex Golden Pixel Motes",hideFlags=HideFlags.HideAndDontSave};
    }

    public void SetAwakened(bool active)
    {
        if(awakened==active)return;
        awakened=active;
        if(active)
        {
            if(fade>=FadeDuration||reveal<=0f)reveal=0f;
            fade=0f;age=0f;poseCount=0;poseHead=0;
        }
        else fade=0f;
        SyncFollow();Draw();
    }

    public void SetReveal(float amount)
    {reveal=Mathf.Clamp01(amount);SyncFollow();Draw();}
    public void SetForeground(bool value)
    {foreground=value;SyncFollow();}
    public void SetCinematic(bool value)
    {cinematic=value;SyncFollow();Draw();}
    public void SetBackAnchor(Vector3? localPoint)
    {backAnchor=localPoint;SyncFollow();}

    public void Tick(float deltaTime)
    {
        float dt=Mathf.Max(0f,deltaTime);
        if(dt>0f)
        {
            age+=dt;
            if(awakened)reveal=Mathf.Min(1f,reveal+dt/RevealDuration);
            else
            {
                fade=Mathf.Min(FadeDuration,fade+dt);
                if(fade>=FadeDuration)reveal=0f;
            }
        }
        SyncFollow();
        if(dt>0f&&visible)
        {
            RecordPose();
            particles.Simulate(dt,false,false,false);
        }
        Draw();
    }

    public void Clear()
    {
        awakened=false;foreground=false;cinematic=false;reveal=0f;fade=FadeDuration;age=0f;
        backAnchor=null;poseCount=0;poseHead=0;visible=false;opacity=0f;Hide();
    }

    private void LateUpdate()
    {
        SyncFollow();
        // Movement and billboarding finish after the combat Tick; replace that same-time pose.
        if(visible&&poseCount>0)RecordPose();
        Draw();
    }
    private void OnDisable()=>Clear();

    private void SyncFollow()
    {
        if(auraRoot==null)return;
        visible=source!=null&&source.enabled&&!source.forceRenderingOff&&source.gameObject.activeInHierarchy&&
            source.sprite!=null&&reveal>0f&&(awakened||fade<FadeDuration);
        opacity=visible?Mathf.Clamp01(source.color.a*(cinematic?1.2f:1f))*
            (awakened?1f:1f-Mathf.SmoothStep(0f,1f,fade/FadeDuration)):0f;
        if(!visible){Hide();return;}
        Bounds body=PixelMantisReaper.VisibleBody(source.sprite);
        Vector3 back=backAnchor??body.center+new Vector3(body.size.x*.04f,body.size.y*.13f,0f);
        if(source.flipX)back.x=-back.x;if(source.flipY)back.y=-back.y;
        auraRoot.localPosition=back;auraRoot.localRotation=Quaternion.identity;
        float size=body.size.y*(cinematic?1.35f:1f);
        auraRoot.localScale=new Vector3(source.flipX?-size:size,source.flipY?-size:size,size);
        int layer=EffectLayer(source);auraRoot.gameObject.layer=layer;
        particleRenderer.enabled=true;particleRenderer.sortingLayerID=source.sortingLayerID;
        particleRenderer.sortingOrder=foreground?30000:source.sortingOrder-1;particleRenderer.gameObject.layer=layer;
    }

    private static int EffectLayer(SpriteRenderer renderer)=>
        renderer.gameObject.layer==LayerMask.NameToLayer("ScreenSpaceOutline")?0:renderer.gameObject.layer;

    private void RecordPose()
    {
        if(poseCount==0||poses[poseHead].time!=age)
        {
            poseHead=(poseHead+1)%HistoryCapacity;poseCount=Mathf.Min(HistoryCapacity,poseCount+1);
        }
        poses[poseHead]=new WingPose{time=age,matrix=auraRoot.localToWorldMatrix};
    }

    private Vector3 PastWorldPoint(Vector3 point,float delay,Matrix4x4 current)
    {
        if(poseCount==0)return current.MultiplyPoint3x4(point);
        float time=age-delay;int newer=poseHead;
        for(int i=1;i<poseCount;i++)
        {
            int older=(poseHead-i+HistoryCapacity)%HistoryCapacity;
            if(poses[older].time<=time)
            {
                float blend=Mathf.InverseLerp(poses[older].time,poses[newer].time,time);
                return Vector3.Lerp(poses[older].matrix.MultiplyPoint3x4(point),poses[newer].matrix.MultiplyPoint3x4(point),blend);
            }
            newer=older;
        }
        return poses[newer].matrix.MultiplyPoint3x4(point);
    }

    private void Hide()
    {
        if(particleRenderer!=null)particleRenderer.enabled=false;
        if(particles!=null)particles.SetParticles(lightParticles,0);
    }

    private Vector3 Feather(int featherIndex,float t)
    {
        float side=featherIndex<FeathersPerWing?-1f:1f;int feather=featherIndex%FeathersPerWing;
        float unfold=Mathf.SmoothStep(0f,1f,reveal);
        float beat=Mathf.Sin(age*4.2f+side*.45f)*.18f;
        float flutter=Mathf.Sin(age*7.1f+feather*1.3f+side)*.055f;
        Vector2 start,control,end;
        if(feather==0)
        {
            start=Vector2.zero;control=new Vector2(side*.24f,.22f);
            end=new Vector2(side*.9f,.8f+beat);
        }
        else
        {
            float attach=.2f+feather*.16f;
            start=2f*(1f-attach)*attach*new Vector2(side*.24f,.22f)+
                attach*attach*new Vector2(side*.9f,.8f+beat);
            control=start+new Vector2(side*(.19f+feather*.03f),.04f+flutter);
            end=new Vector2(side*(.58f+feather*.115f),.07f+feather*.12f+beat*.7f+flutter);
        }
        float inverse=1f-t;
        Vector2 point=inverse*inverse*start+2f*inverse*t*control+t*t*end;
        point.x*=Mathf.Lerp(.5f,1f,unfold)*(1f+Mathf.Sin(age*3.6f+side*.5f)*.12f);
        point.y*=Mathf.Lerp(.3f,1f,unfold);
        point.y+=Mathf.Sin(t*7f-age*6.2f+feather*.7f)*t*t*.09f;
        return point;
    }

    private void Draw()
    {
        if(!visible)return;
        float alpha=opacity*Mathf.SmoothStep(0f,1f,reveal);
        Matrix4x4 current=auraRoot.localToWorldMatrix,inverse=auraRoot.worldToLocalMatrix;
        float maximumOffset=current.MultiplyVector(Vector3.up).magnitude*1.1f;
        for(int i=0;i<ParticleCapacity;i++)
        {
            int feather=i%Feathers;float seed=(i*.61803399f)%1f;
            float life=.65f+seed*.45f,progress=Mathf.Repeat(age/life+i*.381966f,1f);
            float guide=Mathf.Min(progress,Mathf.Clamp01(reveal*1.2f-feather%FeathersPerWing*.04f));
            Vector3 point=Feather(feather,guide),ahead=Feather(feather,Mathf.Min(1f,guide+.015f));
            Vector3 tangent=(ahead-point).normalized,normal=new Vector3(-tangent.y,tangent.x,0f);
            point+=normal*(Mathf.Sin(i*2.4f+age*4.3f)*(.035f+Mathf.Sin(progress*Mathf.PI)*.08f));
            bool spark=i%4==0;
            if(spark&&progress>.65f)point+=new Vector3(Mathf.Sign(point.x)*.24f,.32f,0f)*(progress-.65f);
            float delay=MaximumFollowDelay*(.08f+.92f*guide)*(.82f+.18f*seed);
            Vector3 present=current.MultiplyPoint3x4(point),past=PastWorldPoint(point,delay,current);
            Vector3 world=present+Vector3.ClampMagnitude(past-present,maximumOffset);
            Color color=Color.Lerp(Gold,Ivory,seed*.65f);
            color.a=Mathf.Clamp01(alpha*Mathf.Pow(Mathf.Sin(progress*Mathf.PI),.45f)*
                (.8f+seed*.2f)*Mathf.Clamp01((reveal-progress)*8f+.4f));
            float size=(spark?.036f:.027f)*(1f+seed*.4f)*(cinematic?.65f:1.15f);
            lightParticles[i]=new ParticleSystem.Particle
            {
                position=inverse.MultiplyPoint3x4(world),startColor=color,startSize3D=Vector3.one*size,
                rotation3D=Vector3.zero,startLifetime=life,remainingLifetime=Mathf.Max(.01f,life*(1f-progress)),randomSeed=(uint)(i+1)
            };
        }
        particles.SetParticles(lightParticles,ParticleCapacity);
    }
}
