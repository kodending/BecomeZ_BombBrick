using System;
using UnityEngine;

// A short, chunky pixel stream. Its front reaches the final tile before the first damage tick.
public sealed class PixelWyvernBreath:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private ParticleSystem body,flecks;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[144];
    private SpriteRenderer reference;
    private Vector3 origin,forward,mouth;
    private float length,width,windup,age,finishClock,finishAge,mouthOffset,emissionEnd=-1f;
    private int element;
    private bool finishing,overheat;
    private bool absorbing;
    private Vector3 absorptionAnchor;
    private float absorptionStarted;
    private Func<float> clock;
    private Func<bool> valid;

    public float EndAge=>emissionEnd>=0f?windup+emissionEnd+(overheat?.22f:.16f)+.01f:
        windup+(overheat?WyvernRank3Rules.CoreDuration:WyvernCombatRules.BreathDuration)+.28f;

    public static Color Tint(int element)=>element==1?new Color(1f,.25f,.035f):
        element==2?new Color(.28f,.76f,1f):element==3?new Color(.35f,.55f,1f):new Color(.55f,.17f,.72f);

    public static PixelWyvernBreath Create(SpriteRenderer reference,Vector3 origin,Vector3 forward,
        float length,float width,float windup,int element,Func<float> clock,Func<bool> valid,bool overheat=false)
    {
        var fx=new GameObject("Pixel Wyvern Channeled Breath").AddComponent<PixelWyvernBreath>();
        fx.reference=reference;fx.origin=origin;fx.forward=forward;fx.length=length;fx.width=width;
        fx.windup=windup;fx.element=element;fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;fx.overheat=overheat;
        if(overheat)fx.name="Pixel Wyvern Core Overheat";
        fx.body=PixelMushroomGas.CreateParticles("Breath Body",fx.transform,144,
            PixelMushroomGas.Material(element==2),reference,5,true);
        fx.flecks=PixelMushroomGas.CreateParticles("Breath Sparks",fx.transform,48,
            PixelMushroomGas.Material(true),reference,6,true);
        fx.Pose(0f,forward);return fx;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(emissionEnd>=0f){Pose(age+(skillClock==null?Time.deltaTime:skillClock.DeltaTime),forward);return;}
            if(valid!=null&&!valid()){Cancel();return;}
            if(finishing)Pose(finishAge+Mathf.Max(0f,clock()-finishClock),forward);
        }
    }

    private static float Seed(int i)=>Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.5453f,1f);

    public void Pose(float time,Vector3 direction)
    {
        if(this==null)return;
        age=time;forward=direction;
        float breath=time-windup;
        float duration=overheat?WyvernRank3Rules.CoreDuration:WyvernCombatRules.BreathDuration;
        float heat=overheat?Mathf.Clamp01(breath/duration):0f;
        if(time>EndAge){Cancel();return;}
        if(emissionEnd<0f)
        {
            Vector3 center=reference==null?origin+Vector3.up*.5f:reference.bounds.center;
            mouthOffset=reference==null?.35f:Mathf.Clamp(reference.bounds.extents.x*.7f,.25f,.65f);
            mouth=origin+Vector3.up*(center.y-origin.y+.08f)+forward*mouthOffset;
        }
        Vector3 side=Vector3.Cross(Vector3.up,forward);
        Color tint=Tint(element),hot=element==0?new Color(.74f,.98f,.23f):element==1?
            new Color(1f,.9f,.45f):new Color(.9f,.99f,1f);
        if(breath<0f)
        {
            float charge=Mathf.Clamp01(time/Mathf.Max(.01f,windup));
            for(int i=0;i<32;i++)
            {
                float seed=Seed(i),progress=Mathf.Repeat(charge*1.8f+seed,1f),angle=i*2.4f;
                Vector3 offset=side*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle);
                Color color=Color.Lerp(tint,hot,progress);color.a=Mathf.Sin(progress*Mathf.PI)*charge*.85f;
                particles[i]=Particle(mouth+offset*(1f-progress)*.9f,Vector3.one*(.05f+seed*.06f),color,i);
            }
            flecks.SetParticles(particles,32);
            Color core=hot;core.a=charge*.85f;
            particles[0]=Particle(mouth,Vector3.one*(.12f+charge*.23f),core,0);
            body.SetParticles(particles,1);return;
        }
        for(int layer=0;layer<2;layer++)
        {
            int count=layer==0?144:48;
            for(int i=0;i<count;i++)
            {
                float transit=overheat?.22f:.16f;
                float seed=Seed(i+layer*47),delay=seed*transit;
                float travel=Mathf.Repeat(breath-delay,transit),birth=breath-travel;
                float t=travel/transit;
                float a=Seed(i+283+layer*71)*Mathf.PI*2f+travel*(12f+heat*45f);
                float radius=width*(.1f+.78f*t)*(layer==0?Mathf.Sqrt(Seed(i+11)):1f);
                Vector3 offset=side*(Mathf.Cos(a)*radius)+Vector3.up*(Mathf.Sin(a)*radius*.35f);
                if(overheat)offset+=side*Mathf.Sin(t*16f-breath*22f)*width*.12f*heat+
                    Vector3.up*Mathf.Cos(t*13f-breath*25f)*width*.12f*heat;
                Vector3 position=mouth+forward*((length-mouthOffset)*t)+offset;
                float final=overheat?.8f+heat*.75f:1f+.25f*Mathf.Clamp01((birth-.46f)/.12f);
                float size=layer==0?(.28f+seed*.28f)*(1f+t*.7f):.035f+seed*.065f;
                if(element==2)size*=.7f;
                float core=1f-Mathf.Clamp01(radius/Mathf.Max(.01f,width));
                Color color=Color.Lerp(tint,hot,layer==1?.75f:Mathf.Clamp01(core*.85f+heat*.18f));
                color.a=layer==0?(element==0?.42f:.6f):.9f;
                if(overheat)color.a*=.75f+heat*.4f;
                color.a*=Mathf.Clamp01((1f-t)/.2f);
                if(breath<delay||birth>(emissionEnd>=0f?emissionEnd:duration))color.a=0f;
                Vector3 shape=Vector3.one*size*final;
                if(element==1&&layer==0)shape.y*=.75f;
                if(absorbing)
                {
                    float blend=Mathf.Clamp01((time-absorptionStarted)/.06f);
                    position=Vector3.Lerp(position,GriffinStormOrbRules.AbsorbPosition(position,mouth,absorptionAnchor,t),blend);
                    shape*=Mathf.Lerp(1f,1f-t*.82f,blend);
                }
                particles[i]=Particle(position,shape,color,i);
                particles[i].rotation=element==2?45f:element==3?i%2*90f:0f;
            }
            (layer==0?body:flecks).SetParticles(particles,count);
        }
    }

    private static ParticleSystem.Particle Particle(Vector3 position,Vector3 size,Color color,int index)=>
        new ParticleSystem.Particle {position=position,startSize3D=size*PixelCombatEffect.PreviewVisualScale,startColor=CombatFlashPresentation.Peak(color),
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(index+1)};

    public static void Hit(Vector3 position,SpriteRenderer reference,int element,bool final)
        =>PixelMushroomGas.Hit(position,reference,Tint(element),final);

    public static void OverheatHit(Vector3 point,SpriteRenderer reference,int element,Vector3 direction,int stage)
    {
        var ps=PixelMushroomGas.CreateParticles("Overheat Breath Contact",null,24,
            PixelMushroomGas.Material(true),reference,8,false);
        float force=1f+stage*.18f;Vector3 side=Vector3.Cross(Vector3.up,direction);
        for(int i=0;i<24;i++)
        {
            float seed=Seed(i),angle=i*2.4f;
            ps.Emit(new ParticleSystem.EmitParams {position=point,
                velocity=(side*Mathf.Cos(angle)*(2f+seed*2f)+Vector3.up*Mathf.Sin(angle)*2f-direction*.5f)*force,
                startSize=(i==0?.2f*force:(.035f+seed*.05f)*force)*PixelCombatEffect.PreviewVisualScale,
                startLifetime=i==0?.045f:.1f+seed*.14f,
                startColor=Color.Lerp(Tint(element),Color.white,.35f+stage*.09f)},1);
        }
        if(Application.isPlaying)Destroy(ps.gameObject,.4f);
    }

    public void StopEmission()
    {
        if(this==null||emissionEnd>=0f)return;
        if(age<windup){Cancel();return;}
        // Keep the emitted stream in place while its last particles travel away from the mouth.
        emissionEnd=age-windup;finishing=false;
        Pose(age,forward);
    }

    public void AbsorbTo(Vector3 anchor)
    {
        if(!absorbing)absorptionStarted=age;
        absorbing=true;absorptionAnchor=anchor;
    }
    public void ResumeEmission()=>absorbing=false;

    public void Finish(){if(this==null)return;finishing=true;finishClock=clock();finishAge=age;}
    public void Cancel()
    {
        if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
