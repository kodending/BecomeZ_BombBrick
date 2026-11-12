using System;
using UnityEngine;

// Trembling stones reveal the damage radius before erupting outward at contact.
public sealed class PixelTyrannoStomp:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private ParticleSystem particles;
    private SpriteRenderer source;
    private SpriteMonsterAnimationDriver animationDriver;
    private Func<bool> valid;
    private readonly ParticleSystem.Particle[] stones=new ParticleSystem.Particle[112];
    private readonly Vector3[] origins=new Vector3[112];
    private Vector3 center;
    private float age,radius;
    private int wave;
    private bool impacted;
    private bool extinction;
    private bool automaticSpread;
    private bool marchFootstep;
    private static Sprite[] burstFrames;
    private SpriteRenderer burst;
    private float spreadProgress,tailStart=-1f;

    public static PixelTyrannoStomp Charge(Vector3 position,float range,SpriteRenderer source,Func<bool> valid,bool extinction=false)
    {
        var particles=PixelCombatEffect.PlayTyrannoTremorImpact(position,.5f,source,false);
        particles.name="Tyranno Stomp Ground Fragments";
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.startSpeed=0f;main.gravityModifier=0f;
        main.maxParticles=extinction?512:128;main.startSize=.12f;main.startLifetime=20f;
        var emission=particles.emission;emission.enabled=false;
        var shape=particles.shape;shape.enabled=false;
        var velocity=particles.velocityOverLifetime;velocity.enabled=false;
        var fade=particles.colorOverLifetime;fade.enabled=false;
        var renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder=2;
        var fx=particles.gameObject.AddComponent<PixelTyrannoStomp>();
        fx.particles=particles;fx.source=source;fx.center=position;
        fx.extinction=extinction;
        fx.radius=Mathf.Max(.1f,range);fx.animationDriver=source.GetComponent<SpriteMonsterAnimationDriver>();
        particles.Play();particles.Emit(fx.stones.Length);
        particles.GetParticles(fx.stones);
        int index=0;
        for(int ring=0;ring<4;ring++)
        {
            int count=16+ring*8;
            for(int i=0;i<count;i++,index++)
            {
                float angle=(i+.23f*ring)*Mathf.PI*2f/count;
                fx.origins[index]=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*fx.radius*(ring+1)/4f;
                fx.stones[index].startColor=i%3==0?new Color(.91f,.79f,.56f):new Color(.48f,.34f,.19f);
                fx.stones[index].rotation=i*47f;
            }
        }
        fx.Animate(0f);fx.valid=valid;return fx;
    }

    public void Impact(float range)
    {
        if(this==null||impacted)return;
        impacted=true;valid=null;age=0f;radius=Mathf.Max(.1f,range);
        particles.Clear();
        var main=particles.main;main.gravityModifier=1.2f;
        var fade=particles.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.45f),new GradientAlphaKey(0,1)});
        fade.color=gradient;
        if(extinction)Spread(0f);else EmitWave();
    }

    public static void MarchFinisher(Vector3 position,float range,SpriteRenderer source)
    {
        if(source==null)return;
        var fx=Charge(position,range,source,null,true);
        fx.automaticSpread=true;fx.Impact(range);
    }

    public static void MarchFootstep(Vector3 position,float range,SpriteRenderer source)
    {
        if(source==null)return;
        var fx=Charge(position,range,source,null,true);
        fx.name="Tyrant March Planted Foot";
        fx.marchFootstep=true;fx.automaticSpread=true;fx.Impact(range);
    }

    // The damage wave and its fragments advance from the same coroutine clock.
    public void Spread(float progress)
    {
        if(this==null||!impacted||!extinction)return;
        spreadProgress=Mathf.Max(spreadProgress,Mathf.Clamp01(progress));
        if(spreadProgress>=1f&&tailStart<0f)tailStart=age;
        while(wave<3&&wave*.5f<=progress)EmitExtinctionWave();
        AnimateBurst();
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Animate((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }
    private void Animate(float dt)
    {
        if(!impacted)
        {
            if(source==null||!source.gameObject.activeInHierarchy||(valid!=null&&!valid())){Cancel();return;}
            age+=dt;
            float progress=animationDriver!=null?animationDriver.TyrannoStompProgress:age/TyrannoCombatRules.TremorCastSeconds;
            float front=Mathf.Clamp01(progress/.8f);
            for(int i=0;i<stones.Length;i++)
            {
                float reveal=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((front-origins[i].magnitude/radius+.25f)/.25f));
                stones[i].position=center+origins[i]*Mathf.Lerp(.78f,1f,reveal)+Vector3.up*
                    (.06f+Mathf.Abs(Mathf.Sin(age*63f+i))*.035f*reveal);
                float weight=extinction?(origins[i].magnitude<=radius*2f/3.5f?1.65f:1.25f):1f;
                stones[i].startSize=(.11f+(i%4)*.027f)*reveal*weight;
                stones[i].velocity=Vector3.zero;stones[i].remainingLifetime=20f;
            }
            particles.SetParticles(stones,stones.Length);return;
        }
        age+=dt;
        if(automaticSpread)Spread(age/(marchFootstep?.065f:.12f));
        if(extinction)AnimateBurst();
        if(!extinction)while(wave<4&&age>=wave*.055f)EmitWave();
        if(age>(marchFootstep?.45f:.85f))Cancel();
    }
    private void EmitWave()
    {
        int count=16+wave*8;
        float ring=radius*(wave+1)/4f;
        for(int i=0;i<count;i++)
        {
            float angle=(i+.23f*wave)*Mathf.PI*2f/count;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var emit=new ParticleSystem.EmitParams {
                position=center+direction*ring+Vector3.up*.06f,
                velocity=direction*(wave==3?.06f:.4f)+Vector3.up*(.85f+(i%4)*.17f),
                startLifetime=.32f+(i%3)*.055f,
                startSize=.11f+(i%4)*.027f,
                startColor=i%3==0?new Color(.91f,.79f,.56f):new Color(.48f,.34f,.19f),
                rotation=i*47f };
            particles.Emit(emit,1);
        }
        wave++;
    }

    private void EmitExtinctionWave()
    {
        if(burst==null)
        {
            var texture=Resources.Load<Texture2D>("CombatEffects/Tyranno/ExtinctionBurst");
            if(texture!=null)
            {
                if(burstFrames==null||burstFrames[0]==null)
                {
                    burstFrames=new Sprite[16];
                    float cell=texture.width/4f;
                    for(int i=0;i<burstFrames.Length;i++)
                    {
                        burstFrames[i]=Sprite.Create(texture,
                            new Rect(i%4*cell,(3-i/4)*cell,cell,cell),
                            Vector2.one*.5f,cell,0,SpriteMeshType.FullRect);
                        burstFrames[i].name="ExtinctionBurst_"+i.ToString("00");
                        burstFrames[i].hideFlags=HideFlags.HideAndDontSave;
                    }
                }
                burst=new GameObject("Extinction Animated Rupture").AddComponent<SpriteRenderer>();
                burst.transform.SetParent(transform,true);
                burst.transform.position=center+Vector3.up*.045f;
                burst.transform.rotation=Quaternion.Euler(90,0,0);
                burst.sortingLayerID=source==null?0:source.sortingLayerID;
                burst.sortingOrder=2;
            }
        }
        // Small airborne chips support the authored burst without hiding its silhouette.
        int count=marchFootstep?6+wave*2:8+wave*6;
        for(int i=0;i<count;i++)
        {
            float angle=(i+.27f*wave)*Mathf.PI*2f/count;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            particles.Emit(new ParticleSystem.EmitParams {
                position=center+direction*radius*(wave==0?.12f:wave==1?.5f:.93f)+Vector3.up*.06f,
                velocity=direction*(marchFootstep?1.1f:.45f)+Vector3.up*((marchFootstep?.8f:1.4f)+(i%3)*.3f),
                startLifetime=.22f+(i%3)*.035f,startSize=.08f+(i%3)*.025f,
                startColor=i%3==0?new Color(.91f,.79f,.56f):new Color(.4f,.28f,.17f),
                rotation=i*47f },1);
        }
        wave++;
    }

    private void AnimateBurst()
    {
        if(burst==null)return;
        // Frames 0, 4 and 8 are the three impact accents, driven by the damage front.
        int frame=tailStart<0f?Mathf.FloorToInt(spreadProgress*8f):
            8+Mathf.FloorToInt((age-tailStart)*(marchFootstep?48f:32f));
        burst.enabled=frame<16;
        if(frame>=16)return;
        burst.sprite=burstFrames[frame];
        float scale=radius*2.05f;
        burst.transform.localScale=new Vector3(scale,scale,1f);
        float flash=frame==0||frame==4||frame==8?1.18f:1f;
        burst.color=new Color(flash,flash,flash,frame<12?1f:(16-frame)/4f);
        if(marchFootstep)burst.color*=new Color(.9f,.76f,.57f,.9f);
        burst.color=CombatFlashPresentation.Peak(burst.color);
    }
    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
