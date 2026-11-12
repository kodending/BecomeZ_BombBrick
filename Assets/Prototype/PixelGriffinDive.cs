using System;
using UnityEngine;

// One combat-clock effect owns the landing shadow, short wing echoes and spreading wind.
[DefaultExecutionOrder(10030)]
public sealed class PixelGriffinDive:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private SpriteRenderer source,shadow;
    private readonly SpriteRenderer[] echoes=new SpriteRenderer[4];
    private readonly float[] echoTimes={-10f,-10f,-10f,-10f};
    private ParticleSystem.Particle[] pixels=new ParticleSystem.Particle[112];
    private ParticleSystem wind;
    private Vector3 launch,landing;
    private Color tint;
    private Func<float> clock;
    private Func<bool> valid;
    private float ascent,radius,age,lastEcho=-1f,landedAt=-1f;
    private int echoIndex;
    private bool heavy,hunting;

    public static PixelGriffinDive Create(SpriteRenderer source,Vector3 launch,Vector3 landing,
        float radius,float ascent,Color tint,Func<float> clock,Func<bool> valid,bool heavy=false,bool hunting=false)
    {
        if(source==null)return null;
        var fx=new GameObject("Griffin Wing Dive").AddComponent<PixelGriffinDive>();
        fx.source=source;fx.launch=launch;fx.landing=landing;fx.radius=radius;fx.ascent=ascent;
        fx.tint=tint;fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;fx.heavy=heavy;fx.hunting=hunting;
        if(hunting)fx.name="Griffin Hunting Flight";
        if(heavy){fx.name="Griffin Typhoon Dive";fx.pixels=new ParticleSystem.Particle[192];}
        fx.shadow=new GameObject("Fixed Landing Shadow").AddComponent<SpriteRenderer>();
        fx.shadow.transform.SetParent(fx.transform,false);fx.shadow.sprite=source.sprite;
        fx.shadow.transform.rotation=Quaternion.Euler(90,0,0);
        fx.shadow.sortingLayerID=source.sortingLayerID;fx.shadow.sortingOrder=7;
        fx.wind=PixelMushroomGas.CreateParticles("Wing Gust Pixels",fx.transform,fx.pixels.Length,
            PixelMushroomGas.Material(true),source,-1,true);
        for(int i=0;i<fx.echoes.Length;i++)
        {
            var echo=new GameObject("Short Wing Echo").AddComponent<SpriteRenderer>();
            echo.transform.SetParent(fx.transform,false);echo.enabled=false;
            echo.sortingLayerID=source.sortingLayerID;echo.sortingOrder=source.sortingOrder-2;
            fx.echoes[i]=echo;
        }
        return fx;
    }
    public void Pose(float elapsed)=>age=elapsed;
    public void SetLanding(Vector3 ground)=>landing=ground;
    public void Land(){landedAt=clock();shadow.enabled=false;}
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(valid==null||!valid()||source==null){Cancel();return;}
            float impact=landedAt<0f?-1f:clock()-landedAt;
            if(impact>(heavy?.64f:.48f)){Cancel();return;}
            float glide=GriffinCombatRules.GlideProgress(age,ascent);
            if(impact<0f)
            {
                float shadowProgress=heavy||hunting?Mathf.Clamp01(age/(ascent+GriffinCombatRules.SkyDiveDescentDuration)):glide;
                shadow.transform.localScale=source.transform.lossyScale*Mathf.Lerp(.45f,heavy?1.8f:1.05f,shadowProgress);
                shadow.transform.position=landing+Vector3.up*.04f-
                    shadow.transform.TransformVector(PixelMantisReaper.VisibleBody(shadow.sprite).center);
                shadow.color=new Color(.06f,.08f,.12f,.12f+shadowProgress*(heavy?.30f:.20f));
                if(age>=(heavy||hunting?GriffinCombatRules.SkyDiveWindup:ascent)&&age-lastEcho>=.02f)
                {
                    lastEcho=age;var echo=echoes[echoIndex];echo.sprite=source.sprite;echo.flipX=source.flipX;
                    echo.flipY=source.flipY;echo.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                    echo.transform.localScale=source.transform.lossyScale;echoTimes[echoIndex]=clock();
                    echoIndex=(echoIndex+1)%echoes.Length;
                }
            }
            for(int i=0;i<echoes.Length;i++)
            {
                float alpha=Mathf.Clamp01(1f-(clock()-echoTimes[i])/(heavy?.14f:.10f))*(heavy?.38f:.25f);
                echoes[i].enabled=alpha>0f;Color c=tint;c.a=alpha;echoes[i].color=c;
            }
            float takeoff=age-GriffinCombatRules.SkyDiveWindup;
            for(int i=0;i<pixels.Length;i++)
            {
                float seed=Mathf.Repeat(i*.618034f,1f),angle=i*2.399963f;
                if(heavy||hunting)seed=Mathf.Repeat(Mathf.Sin(i*12.9898f+1.17f)*43758.5453f,1f);
                bool grit=i>=(heavy?120:80);float t=impact>=0f?Mathf.Clamp01(impact/(heavy?(grit?.62f:.44f):(grit?.46f:.32f))):
                    Mathf.Clamp01(takeoff/.36f);
                float spread=1f-Mathf.Pow(1f-(heavy&&impact>=0f?Mathf.Clamp01(impact/.18f):t),3f);
                float reach=impact>=0f?radius:radius*.65f;
                float distance=reach*spread*(heavy?.12f+.88f*Mathf.Sqrt(seed):i<20?.85f+seed*.15f:.25f+seed*.75f);
                // Uneven curling fragments form the gust; no outlined circle or rays.
                angle+=(impact>=0f?1f:-1f)*(1f-t)*.6f;
                Vector3 point=(impact>=0f?landing:launch)+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*distance;
                point.y=.08f+Mathf.Sin(t*Mathf.PI)*(grit?.3f+seed*.45f:.10f+seed*.15f);
                if(heavy)point.y+=(impact>=0f?Mathf.Sin(t*Mathf.PI)*(grit?.65f:.15f):Mathf.Sin(t*Mathf.PI)*.8f);
                Color color=grit?MeadowAmbientParticles.TerrainColor(new Color(.56f,.47f,.30f)):Color.Lerp(tint,Color.white,.65f);
                color.a=(takeoff<0f&&impact<0f?0f:1f)*Mathf.Pow(1f-t,grit?.8f:.6f)*(grit?.85f:.7f);
                if(hunting)color.a=0f; // Flight shadows/echoes only; contact uses the talon debris effect.
                float size=grit?.065f+seed*.09f:.075f+seed*.07f;
                if(heavy)size*=1.55f;
                pixels[i]=new ParticleSystem.Particle{position=point,startColor=color,
                    startSize3D=new Vector3(size*(grit?1f:heavy?1.5f:2.5f),size,size),rotation=angle*Mathf.Rad2Deg,
                    startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(i+1)};
            }
            wind.SetParticles(pixels,pixels.Length);
        }
    }
    public void Cancel()
    {
        if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
