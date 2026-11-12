using System;
using UnityEngine;

// Arena wind and harmless thrown sprites share the originating battle clock.
public sealed class PixelGriffinCrosswind:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private Func<float> clock;
    private Func<bool> valid;
    private float started;
    private Bounds area;
    private Vector3 forward,origin,incoming;
    private Color tint;
    private ParticleSystem wind,dust;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[180];
    private SpriteRenderer thrown;
    private SpriteRenderer[] echoes;
    private Vector3 thrownScale;
    private float throwDistance,throwDuration,throwSide,throwHeight;
    private Quaternion rotation;
    private bool tossed;

    public static PixelGriffinCrosswind Create(Bounds area,Vector3 forward,Vector3 landing,
        SpriteRenderer reference,Color tint,Func<float> clock,Func<bool> valid)
    {
        var fx=new GameObject("Griffin Sovereign Headwind").AddComponent<PixelGriffinCrosswind>();
        fx.area=area;fx.forward=forward;fx.origin=landing;fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;fx.started=fx.clock();
        fx.tint=Color.Lerp(new Color(.7f,.88f,.96f),tint,.2f);
        fx.wind=PixelMushroomGas.CreateParticles("Flowing Wind Pixels",fx.transform,180,
            PixelMushroomGas.Material(true),reference,2,true);
        var renderer=fx.wind.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder=3;
        fx.dust=PixelMushroomGas.CreateParticles("Loose Wind Dust",fx.transform,36,
            PixelMushroomGas.Material(false),reference,3,true);
        fx.dust.GetComponent<ParticleSystemRenderer>().sortingOrder=4;
        fx.Pose(0f);return fx;
    }

    public static PixelGriffinCrosswind Deflect(SpriteRenderer sprite,Vector3 contact,Vector3 incoming,
        Vector3 forward,Bounds arena,Func<float> clock,Func<bool> valid)
    {
        var fx=new GameObject("Projectile Swept Away").AddComponent<PixelGriffinCrosswind>();
        fx.tossed=true;fx.origin=contact;fx.incoming=incoming;fx.forward=forward;
        fx.area=arena;
        float edge=forward.x>0f?arena.max.x:arena.min.x;
        fx.throwDistance=Mathf.Abs(edge-contact.x)+MonsterAutoBattlePrototype.TileSize*2.5f+sprite.bounds.extents.x;
        fx.throwDuration=Mathf.Clamp(fx.throwDistance/26f+.25f,.7f,1.35f);
        float seed=Seed(fx.GetInstanceID());
        fx.throwSide=Mathf.Lerp(-.8f,.8f,seed);fx.throwHeight=1.3f+seed*.8f;
        fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;fx.started=fx.clock();fx.tint=sprite.color;
        fx.rotation=sprite.transform.rotation;fx.thrownScale=sprite.transform.lossyScale;
        fx.thrown=fx.gameObject.AddComponent<SpriteRenderer>();
        fx.thrown.sprite=sprite.sprite;fx.thrown.sharedMaterial=sprite.sharedMaterial;
        fx.thrown.sortingLayerID=sprite.sortingLayerID;fx.thrown.sortingOrder=sprite.sortingOrder;
        fx.thrown.flipX=sprite.flipX;fx.thrown.flipY=sprite.flipY;
        fx.echoes=new SpriteRenderer[3];
        for(int i=0;i<fx.echoes.Length;i++)
        {
            var echo=new GameObject("Swept Projectile Echo").AddComponent<SpriteRenderer>();
            echo.transform.SetParent(fx.transform,false);echo.sprite=sprite.sprite;
            echo.sharedMaterial=sprite.sharedMaterial;echo.sortingLayerID=sprite.sortingLayerID;
            echo.sortingOrder=sprite.sortingOrder-1;echo.flipX=sprite.flipX;echo.flipY=sprite.flipY;
            fx.echoes[i]=echo;
        }
        fx.Pose(0f);return fx;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(valid!=null&&!valid()){Cancel();return;}
            Pose(Mathf.Max(0f,clock()-started));
        }
    }

    private static float Seed(int i)=>Mathf.Repeat(Mathf.Sin(i*12.9898f+1.17f)*43758.5453f,1f);

    private Vector3 ThrownPosition(float age)
    {
        float t=Mathf.Clamp01(age/throwDuration);
        float launch=Mathf.Clamp01((age-.08f)/(throwDuration-.08f));
        return origin+incoming*(.18f*Mathf.Sin(t*Mathf.PI))+
            forward*(throwDistance*(.18f*launch+.82f*launch*launch))+
            Vector3.forward*(throwSide*t*t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*throwHeight);
    }

    private float ThrownAlpha(Vector3 point)
    {
        float outside=forward.x>0f?point.x-area.max.x:area.min.x-point.x;
        return 1f-Mathf.Clamp01((outside-MonsterAutoBattlePrototype.TileSize*.5f)/
            (MonsterAutoBattlePrototype.TileSize*2f));
    }

    private void Pose(float age)
    {
        if(tossed)
        {
            float t=Mathf.Clamp01(age/throwDuration);
            transform.position=ThrownPosition(age);
            transform.rotation=rotation*Quaternion.Euler(0,0,-forward.x*(180f*t+900f*t*t));
            transform.localScale=thrownScale*(1f+.18f*Mathf.Sin(Mathf.Clamp01(age/.2f)*Mathf.PI));
            Color color=tint;color.a*=ThrownAlpha(transform.position);thrown.color=color;
            for(int i=0;i<echoes.Length;i++)
            {
                float delay=.035f*(i+1),echoAge=Mathf.Max(0f,age-delay);
                float echoT=Mathf.Clamp01(echoAge/throwDuration);
                var echo=echoes[i];echo.transform.position=ThrownPosition(echoAge);
                echo.transform.rotation=rotation*Quaternion.Euler(0,0,-forward.x*(180f*echoT+900f*echoT*echoT));
                Color echoColor=tint;echoColor.a*=age>delay?ThrownAlpha(echo.transform.position)*(.24f-i*.06f):0f;
                echo.color=echoColor;
            }
            if(age>=throwDuration+.12f)Cancel();return;
        }
        const float duration=MonsterAutoBattlePrototype.GriffinCrosswindDuration;
        if(age>=duration+.18f){Cancel();return;}
        float fade=Mathf.Clamp01(age/.09f)*Mathf.Clamp01((duration+.18f-age)/.18f);
        // Loose square particles travel with the gust; no drawn wind lines or stretched quads.
        for(int i=0;i<180;i++)
        {
            float seed=Seed(i),cycle=Mathf.Repeat(age*(1.5f+seed)+Seed(i+11),1f);
            Vector3 point=new Vector3(Mathf.Lerp(area.min.x,area.max.x,Seed(i+31)),.07f,
                Mathf.Lerp(area.min.z,area.max.z,Seed(i+57)));
            point+=forward*(age*(12f+seed*8f));
            point.x=area.min.x+Mathf.Repeat(point.x-area.min.x,area.size.x);
            point.z=area.min.z+Mathf.Repeat(point.z-area.min.z,area.size.z);
            point.y=.07f+seed*.3f;
            float reveal=Mathf.Clamp01((age-Vector3.Distance(origin,new Vector3(point.x,0,point.z))/35f)*20f);
            float pulse=Mathf.Sin(cycle*Mathf.PI);
            Color color=Color.Lerp(tint,Color.white,.45f+seed*.45f);
            color.a=fade*reveal*pulse*(.45f+seed*.3f);
            particles[i]=new ParticleSystem.Particle {position=point,
                startSize3D=Vector3.one*(.045f+seed*.065f),startColor=color,
                startLifetime=10,remainingLifetime=10,randomSeed=(uint)(i+1)};
        }
        wind.SetParticles(particles,180);
        for(int i=0;i<36;i++)
        {
            float seed=Seed(i+71),t=Mathf.Repeat(age*1.7f+seed,1f);
            Vector3 p=new Vector3(Mathf.Lerp(area.min.x,area.max.x,Seed(i+100)),0,
                Mathf.Lerp(area.min.z,area.max.z,Seed(i+150)))+forward*(age*(8f+seed*5f));
            p.x=area.min.x+Mathf.Repeat(p.x-area.min.x,area.size.x);
            p.z=area.min.z+Mathf.Repeat(p.z-area.min.z,area.size.z);
            p.y=.08f+seed*.25f;
            Color color=i%3==0?MeadowAmbientParticles.TerrainColor(new Color(.64f,.62f,.46f)):tint;
            color.a=fade*Mathf.Sin(t*Mathf.PI)*.15f;
            particles[i]=new ParticleSystem.Particle {position=p,startSize3D=Vector3.one*(.23f+seed*.24f),
                startColor=color,startLifetime=10,remainingLifetime=10,randomSeed=(uint)(i+91)};
        }
        dust.SetParticles(particles,36);
    }

    public void Cancel()
    {
        if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
