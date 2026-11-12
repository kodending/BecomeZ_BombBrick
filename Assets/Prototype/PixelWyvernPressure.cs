using System;
using UnityEngine;

// A rolling fan of breath particles. The same advancing front drives combat contacts.
public sealed class PixelWyvernPressure:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private ParticleSystem body,sparks;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[180];
    private SpriteRenderer reference;
    private Vector3 origin,forward;
    private float length,age,finishClock,finishAge;
    private int element;
    private bool finishing;
    private bool absorbing;
    private Vector3 absorptionAnchor;
    private float absorptionStarted;
    private Func<float> clock;
    private Func<bool> valid;

    public static PixelWyvernPressure Create(SpriteRenderer reference,Vector3 origin,Vector3 forward,
        float length,int element,Func<float> clock,Func<bool> valid)
    {
        var fx=new GameObject("Pixel Wyvern Pressure Breath").AddComponent<PixelWyvernPressure>();
        fx.reference=reference;fx.origin=origin;fx.forward=forward;fx.length=length;
        fx.element=element;fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;
        fx.body=PixelMushroomGas.CreateParticles("Rolling Breath",fx.transform,180,
            PixelMushroomGas.Material(false),reference,5,true);
        fx.sparks=PixelMushroomGas.CreateParticles("Breath Fragments",fx.transform,96,
            PixelMushroomGas.Material(true),reference,6,true);
        fx.Pose(0f,forward);return fx;
    }

    private static float Seed(int n)=>Mathf.Repeat(Mathf.Sin(n*127.1f+311.7f)*43758.5453f,1f);
    private Color Hot=>element==0?new Color(.83f,.48f,.97f):element==1?new Color(1f,.9f,.44f):new Color(.9f,.99f,1f);
    private Vector3 Ray(float angle)=>Quaternion.AngleAxis(angle,Vector3.up)*forward;
    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(valid!=null&&!valid()){Cancel();return;}
            if(finishing)Pose(finishAge+Mathf.Max(0f,clock()-finishClock),forward);
        }
    }

    public void Pose(float time,Vector3 direction)
    {
        if(this==null)return;age=time;forward=direction;
        float release=time-WyvernRank3Rules.PressureWindup;
        Vector3 mouth=origin+forward*.45f+Vector3.up*(reference==null?.6f:reference.bounds.center.y+.05f);
        Color tint=PixelWyvernBreath.Tint(element);
        if(release<0f)
        {
            float charge=Mathf.Clamp01(time/WyvernRank3Rules.PressureWindup);
            Vector3 side=Vector3.Cross(Vector3.up,forward);
            for(int i=0;i<48;i++)
            {
                float s=Seed(i),t=Mathf.Repeat(charge*1.6f+s,1f),a=i*2.4f;
                Vector3 offset=(side*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a))*(1f-t)*(.6f+s*.65f);
                Color color=Color.Lerp(tint,Hot,t);color.a=Mathf.Sin(t*Mathf.PI)*charge;
                particles[i]=Particle(mouth+offset,Vector3.one*(.045f+s*.07f),color,i);
            }
            sparks.SetParticles(particles,48);
            for(int i=0;i<8;i++)
            {
                Color color=Color.Lerp(tint,Hot,i/9f);color.a=charge*.4f;
                particles[i]=Particle(mouth+Vector3.up*Mathf.Sin(i*2.4f)*.07f,
                    Vector3.one*(.35f-i*.025f)*charge,color,i);
            }
            body.SetParticles(particles,8);return;
        }
        float dissipate=Mathf.Clamp01((release-WyvernRank3Rules.PressureDuration)/.24f);
        if(dissipate>=1f){Cancel();return;}
        float front=WyvernRank3Rules.PressureFront(release),fade=1f-dissipate;
        float crestOpacity=1f,interiorOpacity=1f;
        if(CombatReadabilityPresentation.RefinedSkills){crestOpacity=.85f;interiorOpacity=.45f;}
        for(int layer=0;layer<2;layer++)
        {
            int count=layer==0?180:96;
            for(int i=0;i<count;i++)
            {
                float s=Seed(i+layer*313),angle=(Seed(i+83)-.5f)*WyvernRank3Rules.PressureAngle;
                Vector3 ray=Ray(angle),side=Vector3.Cross(Vector3.up,ray);
                bool crest=i<60;
                float radial=crest?.83f+s*.17f:.1f+Mathf.Sqrt(s)*.78f;
                float roll=release*27f+Seed(i+49)*6.283f;
                float curl=(crest?.2f:.28f)*front;
                Vector3 position=origin+ray*(length*front*radial)+Vector3.up*(mouth.y+Mathf.Sin(roll)*curl);
                position+=side*Mathf.Cos(roll)*curl+ray*dissipate*(.2f+s*.35f);
                float size=layer==0?(.36f+s*.38f)*(1f+front*.85f):.04f+s*.07f;
                if(element==2&&layer==1)size*=1.35f;
                Color color=Color.Lerp(tint,Hot,crest?.5f+s*.35f:s*.25f);
                color.a=(layer==0?(crest?.7f*crestOpacity:.5f*interiorOpacity):.9f)*fade*Mathf.Clamp01(release/.018f);
                Vector3 shape=Vector3.one*size*(1f+dissipate*.4f);
                if(layer==0)shape.y*=crest?1.2f:.85f;
                if(absorbing)
                {
                    float intake=Mathf.Clamp01((time-absorptionStarted)/.24f);
                    position=GriffinStormOrbRules.AbsorbPosition(position,origin+forward*length*.45f+Vector3.up*mouth.y,absorptionAnchor,intake);
                    shape*=1f-intake*.94f;color.a*=1f-intake;
                }
                particles[i]=Particle(position,shape,color,i);
                particles[i].rotation=element==2?45f:element==3?i%2*90f:roll*Mathf.Rad2Deg;
            }
            (layer==0?body:sparks).SetParticles(particles,count);
        }
    }

    public void PoseFire(float time)
    {
        if(this==null)return;
        if(time<.24f)Pose(WyvernRank3Rules.PressureWindup+WyvernRank3Rules.PressureDuration+time,forward);
        else body.SetParticles(particles,0);
        float fade=Mathf.Clamp01((WyvernCombatRules.DotDuration-time)/.35f);
        Color tint=PixelWyvernBreath.Tint(1);
        for(int i=0;i<96;i++)
        {
            float s=Seed(i),t=Mathf.Repeat(time*(1.2f+s)+s,1f);
            Vector3 position=origin+Ray((Seed(i+19)-.5f)*WyvernRank3Rules.PressureAngle)*length*Mathf.Sqrt(s);
            position.y=.12f+t*.42f;
            Color color=Color.Lerp(Hot,tint,t);color.a=Mathf.Sin(t*Mathf.PI)*fade*.6f;
            particles[i]=Particle(position,new Vector3(.1f,.22f,.1f)*(1f-t*.7f),color,i);
        }
        sparks.SetParticles(particles,96);
    }

    public static void Hit(Vector3 point,SpriteRenderer reference,int element,Vector3 direction)
    {
        var ps=PixelMushroomGas.CreateParticles("Pressure Breath Contact",null,16,
            PixelMushroomGas.Material(true),reference,7,false);
        for(int i=0;i<16;i++)
        {
            float s=Seed(i),a=i*2.4f;
            Vector3 side=Vector3.Cross(Vector3.up,direction);
            ps.Emit(new ParticleSystem.EmitParams {position=point,velocity=direction*(2f+s*3f)+
                side*Mathf.Cos(a)*2.5f+Vector3.up*Mathf.Sin(a)*2f,
                startSize=(i==0?.32f:.045f+s*.08f)*PixelCombatEffect.PreviewVisualScale,startLifetime=i==0?.045f:.12f+s*.1f,
                startColor=Color.Lerp(PixelWyvernBreath.Tint(element),Color.white,.65f)},1);
        }
        if(Application.isPlaying)Destroy(ps.gameObject,.35f);
    }

    private static ParticleSystem.Particle Particle(Vector3 position,Vector3 size,Color color,int index)=>
        new ParticleSystem.Particle {position=position,startSize3D=size*PixelCombatEffect.PreviewVisualScale,startColor=CombatFlashPresentation.Peak(color),
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(index+1)};
    public void Finish(){if(this==null)return;finishing=true;finishClock=clock();finishAge=age;}
    public void AbsorbTo(Vector3 anchor)
    {if(absorbing)return;absorbing=true;absorptionAnchor=anchor;absorptionStarted=age;}
    public void Cancel()
    {
        if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
