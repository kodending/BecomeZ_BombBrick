using System;
using UnityEngine;

// A particle-only straight pulse, driven by the combat clock so pause also freezes its tail.
public sealed class PixelWyvernBolt:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[192];
    private static Material neonMaterial;
    private ParticleSystem beam;
    private SpriteRenderer reference;
    private Vector3 mouth,direction,wind,escape;
    private float length,reach,rotation,started,deflectedAt=-1f;
    private int element;
    private Func<float> clock,charge;
    private Func<Vector3> origin,aim;
    private Func<bool> valid;

    private static PixelWyvernBolt Create(SpriteRenderer reference,int element,Func<bool> valid)
    {
        var fx=new GameObject("Pixel Wyvern Piercing Bolt").AddComponent<PixelWyvernBolt>();
        fx.reference=reference;fx.element=element;fx.valid=valid;
        fx.beam=PixelMushroomGas.CreateParticles("Neon Cannon",fx.transform,192,
            NeonMaterial(),reference,6,true);
        return fx;
    }

    private static Material NeonMaterial()
    {
        if(neonMaterial==null)neonMaterial=new Material(Resources.Load<Shader>("CombatEffects/Wyvern/NeonParticle"))
            {name="Wyvern Additive Light",hideFlags=HideFlags.HideAndDontSave};
        return neonMaterial;
    }

    public static PixelWyvernBolt Charge(SpriteRenderer reference,int element,Func<Vector3> origin,
        Func<Vector3> aim,float reach,Func<float> progress,Func<bool> valid)
    {
        var fx=Create(reference,element,valid);fx.charge=progress;fx.aim=aim;
        fx.origin=origin;fx.reach=reach;
        fx.name="Pixel Wyvern Bolt Charge";return fx;
    }

    public static PixelWyvernBolt Fire(SpriteRenderer reference,Vector3 origin,Vector3 direction,
        float reach,int element,Func<float> clock,Func<bool> valid)
    {
        var fx=Create(reference,element,valid);fx.direction=direction;fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.started=fx.clock();
        fx.reach=reach;fx.SetBeamOrigin(origin);fx.Pose(0f);return fx;
    }

    private void SetBeamOrigin(Vector3 position)
    {
        float offset=reference==null?.35f:Mathf.Clamp(reference.bounds.extents.x*.75f,.25f,.7f);
        mouth=position+direction*offset+Vector3.up*(reference==null?.55f:reference.bounds.center.y+.08f);
        length=Mathf.Max(.1f,reach-offset);
        var camera=Camera.main;
        Vector3 right=camera==null?Vector3.right:camera.transform.right;
        Vector3 up=camera==null?Vector3.forward:camera.transform.up;
        rotation=-Mathf.Atan2(Vector3.Dot(direction,up),Vector3.Dot(direction,right))*Mathf.Rad2Deg;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(valid!=null&&!valid()){Cancel();return;}
            if(charge!=null){PoseCharge();return;}
            Pose(clock()-started);
        }
    }

    private static float Seed(int i)=>Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.5453f,1f);
    private void PoseCharge()
    {
        if(reference==null){Cancel();return;}
        direction=aim();
        if(direction.sqrMagnitude<.0001f)direction=reference.flipX?Vector3.left:Vector3.right;
        SetBeamOrigin(origin());
        float t=Mathf.Clamp01((charge()-.65f)/.35f);
        // A hairline spans the whole range before the attack frame; it never grows lengthwise.
        Color line=Color.Lerp(PixelWyvernBreath.Tint(element),Color.white,.72f);
        line.a=t>0f?.8f:0f;
        for(int i=0;i<80;i++)
        {
            particles[i]=Particle(mouth+direction*(length*(i+.5f)/80f),
                new Vector3(length/80f*3f,.055f,.055f),line,i);
            particles[i].rotation=rotation;
        }
        Vector3 side=Vector3.Cross(Vector3.up,direction);
        for(int i=0;i<24;i++)
        {
            float s=Seed(i),a=i*2.4f;
            Vector3 offset=(side*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a))*(1f-t)*(.3f+s*.3f);
            Color color=Color.Lerp(PixelWyvernBreath.Tint(element),Color.white,t);color.a=t*.9f;
            particles[80+i]=Particle(mouth+offset,Vector3.one*(.04f+s*.04f),color,i);
        }
        beam.SetParticles(particles,104);
    }

    public void Pose(float age)
    {
        if(this==null)return;
        if(deflectedAt>=0f){PoseDeflected(age-deflectedAt);return;}
        if(age>=WyvernCombatRules.BoltHold+WyvernCombatRules.BoltFade){Cancel();return;}
        float fade=1f-Mathf.Clamp01((age-WyvernCombatRules.BoltHold)/WyvernCombatRules.BoltFade);
        float kick=1f+.4f*(1f-Mathf.Clamp01(age/.025f));
        var camera=Camera.main;
        Vector3 across=camera==null?Vector3.up:Vector3.Cross(camera.transform.forward,direction).normalized;
        Color tint=PixelWyvernBreath.Tint(element);
        for(int i=0;i<160;i++)
        {
            bool core=i>=80;int n=i%80;float along=(n+.5f)/80f;
            Vector3 position=mouth+direction*(length*along);
            Color color=core?Color.Lerp(tint,Color.white,.8f):tint;
            float ends=Mathf.Clamp01((1f-along)*14f)*Mathf.Clamp01(along*22f);
            color.a=(core?.95f:.35f)*fade*ends;
            float size=length/80f*3.5f;
            float ripple=1f+.09f*Mathf.Sin(along*31f-age*130f);
            float width=(core?.25f:1.25f)*kick*fade*ripple;
            particles[i]=Particle(position,new Vector3(size,width,width),color,i);
            particles[i].rotation=rotation;
        }
        // Short moving light flecks break up the halo without bending the straight core.
        for(int i=160;i<188;i++)
        {
            float s=Seed(i),along=Mathf.Repeat(s+age*(1.7f+s),1f);
            float offset=(i%2==0?1f:-1f)*(.18f+Seed(i+51)*.38f+age*1.2f);
            Color color=Color.Lerp(tint,Color.white,.25f);color.a=.65f*fade*Mathf.Sin(along*Mathf.PI);
            particles[i]=Particle(mouth+direction*(length*along)+across*offset,
                new Vector3(.14f+s*.5f,.035f+s*.025f,.04f),color,i);
            particles[i].rotation=rotation;
        }
        float flash=Mathf.Pow(Mathf.Clamp01(1f-age/.11f),2f);
        for(int i=188;i<192;i++)
        {
            bool glow=i==188;Color color=glow?tint:Color.Lerp(tint,Color.white,.85f);
            color.a=(glow?.45f:.9f)*flash;
            Vector3 size=glow?Vector3.one*1.5f:new Vector3(i==189?.7f:1.1f,.09f,.09f);
            particles[i]=Particle(mouth,size,color,i);particles[i].rotation=rotation+(i-189)*60f;
        }
        beam.SetParticles(particles,particles.Length);
    }

    public void Deflect(Vector3 windDirection,Bounds arena)
    {
        if(this==null)return;
        deflectedAt=clock()-started;wind=windDirection;
        escape=new Vector3(wind.x>0f?arena.max.x+3f:arena.min.x-3f,mouth.y,mouth.z);
        PoseDeflected(0f);
    }

    private void PoseDeflected(float age)
    {
        float t=Mathf.Clamp01(age/.4f);if(t>=1f){Cancel();return;}
        Vector3 center=Vector3.Lerp(mouth,escape,t*t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*2f;
        for(int i=0;i<64;i++)
        {
            float s=Seed(i),a=i*2.4f+t*12f;
            Vector3 offset=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0f)*(.2f+s*.7f);
            Color color=Color.Lerp(PixelWyvernBreath.Tint(element),Color.white,s*.6f);
            color.a=Mathf.Clamp01((1f-t)/.2f);
            particles[i]=Particle(center+offset-wind*s*.7f,Vector3.one*(.06f+s*.12f),color,i);
        }
        beam.SetParticles(particles,64);
    }

    private static ParticleSystem.Particle Particle(Vector3 position,Vector3 size,Color color,int index)=>
        new ParticleSystem.Particle {position=position,startSize3D=size,startColor=color,
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(index+1)};

    public static void Hit(Vector3 point,SpriteRenderer reference,int element,Vector3 forward)
    {
        var ps=PixelMushroomGas.CreateParticles("Wyvern Bolt Contact",null,10,NeonMaterial(),reference,7,false);
        var fade=ps.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(0f,1f)});fade.color=gradient;
        Vector3 side=Vector3.Cross(Vector3.up,forward);
        for(int i=0;i<10;i++)
        {
            float a=i*2.4f,s=Seed(i);
            ps.Emit(new ParticleSystem.EmitParams {position=point,
                velocity=forward*(1f+s*2f)+side*Mathf.Cos(a)*2f+Vector3.up*Mathf.Sin(a)*1.6f,
                startSize=i==0?.55f:.07f+s*.09f,startLifetime=i==0?.05f:.1f+s*.07f,
                startColor=Color.Lerp(PixelWyvernBreath.Tint(element),Color.white,i==0?1f:.65f)},1);
        }
        if(Application.isPlaying)Destroy(ps.gameObject,.3f);
    }

    public void Cancel()
    {
        if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
