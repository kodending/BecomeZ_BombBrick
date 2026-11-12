using System;
using UnityEngine;

[DefaultExecutionOrder(10040)]
public sealed class PixelSpiritConcert:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    public const float ArrivalDuration=.55f;
    public const float ReadyToCast=ArrivalDuration+.24f;
    public static readonly Color Color=new Color(.48f,.95f,1f,.84f);
    private SpriteRenderer body,edge;
    private Material material,outline;
    private Func<float> clock;
    private Func<bool> valid;
    private float born;
    private bool departed;
    private ParticleSystem arrivalPixels;
    private ParticleSystemRenderer arrivalRenderer;
    private readonly ParticleSystem.Particle[] motes=new ParticleSystem.Particle[48];
    public Func<float> Clock=>clock;
    public Func<bool> Valid=>valid;

    public static PixelSpiritConcert Create(SpriteRenderer body,Func<float> clock,Func<bool> valid)
    {
        if(body==null)return null;
        var fx=body.gameObject.AddComponent<PixelSpiritConcert>();
        fx.body=body;fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;fx.born=fx.clock();
        var shader=Resources.Load<Shader>("CombatEffects/Bear/FightingSpiritSilhouette");
        fx.material=new Material(shader);fx.material.SetFloat("_KeepSpriteDetail",1f);
        body.sharedMaterial=fx.material;body.color=Color;
        fx.outline=new Material(shader);fx.outline.SetFloat("_OutlineOnly",1f);
        fx.edge=new GameObject("Concert Spirit Pixel Rim").AddComponent<SpriteRenderer>();
        fx.edge.transform.SetParent(body.transform,false);fx.edge.sharedMaterial=fx.outline;
        var arrival=PixelBearAvatarDeparture.Create(body,valid,Color,clock,true,ArrivalDuration);
        if(arrival!=null)arrival.name="Concert Spirit Gathering Pixels";
        fx.arrivalPixels=PixelMushroomGas.CreateParticles("Concert Spirit Rising Pixels",body.transform,
            fx.motes.Length,PixelMushroomGas.Material(true),body,2,true);
        fx.arrivalRenderer=fx.arrivalPixels.GetComponent<ParticleSystemRenderer>();
        body.forceRenderingOff=true;fx.edge.enabled=false;return fx;
    }

    public static Color Tint(SpriteRenderer reference,Color original)
    {
        if(reference==null||reference.GetComponent<PixelSpiritConcert>()==null)return original;
        Color color=Color.Lerp(Color,UnityEngine.Color.white,.22f);color.a=original.a*.8f;return color;
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(body==null||valid==null||!valid()){enabled=false;return;}
            float age=clock()-born;
            bool visible=!departed&&age>=ArrivalDuration;
            body.forceRenderingOff=!visible;body.color=Color;
            edge.sprite=body.sprite;edge.flipX=body.flipX;edge.flipY=body.flipY;
            edge.sortingLayerID=body.sortingLayerID;edge.sortingOrder=body.sortingOrder+1;
            edge.color=new Color(.76f,1f,1f,.65f);edge.enabled=visible&&body.enabled;
            if(arrivalPixels!=null)DrawArrival(age);
        }
    }

    private void DrawArrival(float age)
    {
        if(age>ReadyToCast+.15f){Destroy(arrivalPixels.gameObject);return;}
        Bounds bounds=PixelMantisReaper.VisibleBody(body.sprite);
        float t=Mathf.Clamp01(age/ReadyToCast);
        for(int i=0;i<motes.Length;i++)
        {
            float seed=Mathf.Repeat(i*.618034f,1f),angle=i*2.399963f+t*2.4f;
            float rise=Mathf.Clamp01((t-seed*.25f)/.75f);
            Vector3 local=new Vector3(bounds.center.x+Mathf.Cos(angle)*bounds.size.x*(.62f-.38f*rise),
                bounds.min.y+bounds.size.y*rise,0);
            Color color=Color.Lerp(Color,UnityEngine.Color.white,seed*.5f);
            color.a=Mathf.Sin(Mathf.PI*rise)*.85f;
            motes[i]=new ParticleSystem.Particle{position=body.transform.TransformPoint(local),
                startSize3D=Vector3.one*(.035f+seed*.025f),startColor=color,startLifetime=10,remainingLifetime=10};
        }
        arrivalRenderer.sortingLayerID=body.sortingLayerID;arrivalRenderer.sortingOrder=body.sortingOrder+2;
        arrivalPixels.SetParticles(motes,motes.Length);
    }

    public void Depart()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(this==null||departed)return;
            if(CombatEffectEndFade.IsFading(this))return;
            departed=true;
            var departure=PixelBearAvatarDeparture.Create(body,valid,Color,clock,false,.72f);
            if(departure!=null)departure.name="Concert Spirit Dissolving Pixels";
            body.forceRenderingOff=true;edge.enabled=false;
        }
    }

    private void OnDestroy()
    {
        foreach(var owned in new[]{material,outline})
            if(owned!=null){if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);}
    }
}
