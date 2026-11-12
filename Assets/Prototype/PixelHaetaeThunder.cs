using UnityEngine;

// Holds build-safe references to the imported Zap prefabs and sound; originals stay untouched.
public sealed class PixelHaetaeThunder:MonoBehaviour
{
    public const float Afterglow=.65f;
    public const string ResourcePath="CombatEffects/Haetae/HeavenlyThunder";
    public GameObject[] variants=new GameObject[4];
    public AudioClip impactSound;
    public Vector3 visualScale=new Vector3(.65f,.9f,.65f);
    private static PixelHaetaeThunder template;
    private ParticleSystem warning,zap;
    private ParticleSystemRenderer[] zapRenderers;
    private MaterialPropertyBlock emission;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[24];
    private Vector3 point;
    private Color tint;
    private int element,layer,order;
    private float radius;
    private static PixelHaetaeThunder Template=>template!=null?template:
        template=Resources.Load<PixelHaetaeThunder>(ResourcePath);
    public static AudioClip ImpactClip=>Template==null?null:Template.impactSound;

    internal static System.Collections.IEnumerator WarmupTemplate()
    {
        if(template!=null)yield break;
        var request=Resources.LoadAsync<PixelHaetaeThunder>(ResourcePath);
        while(!request.isDone)yield return null;
        template=request.asset as PixelHaetaeThunder;
    }

    public static PixelHaetaeThunder Create(Vector3 point,int element,SpriteRenderer reference,float radius=1.02f,GameObject zapPrefab=null)
    {
        var fx=Template==null?new GameObject("Heavenly Thunder").AddComponent<PixelHaetaeThunder>():Instantiate(Template);
        fx.name="Heavenly Thunder";fx.point=point;fx.element=element;fx.radius=radius;
        fx.transform.position=point+Vector3.up*.04f;
        fx.tint=PixelHaetaeJusticeRoar.Tint(element);
        fx.layer=reference==null?0:reference.sortingLayerID;fx.order=reference==null?5:reference.sortingOrder+5;
        fx.warning=PixelMushroomGas.CreateParticles("Heavenly Warning Pixels",fx.transform,24,
            PixelMushroomGas.Material(true),reference,3,true);
        if(zapPrefab!=null)fx.variants=new[]{zapPrefab};
        fx.Pose(0f);return fx;
    }

    public void Pose(float age)
    {
        if(this==null)return;
        if(age<HaetaeRank3Rules.HeavenlyDelay)
        {
            float p=Mathf.Clamp01(age/HaetaeRank3Rules.HeavenlyDelay);
            for(int i=0;i<particles.Length;i++)
            {
                float seed=Mathf.Repeat(i*.618034f+.13f,1f),angle=i*2.399963f;
                Vector3 offset=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*radius*(.3f+seed*.7f);
                Color color=Color.Lerp(tint,Color.white,p*.7f);color.a=.35f+p*.6f;
                particles[i]=new ParticleSystem.Particle {position=point+offset+Vector3.up*(.06f+p*(.25f+seed*.65f)),
                    startSize=.045f+seed*.04f,startColor=color,startLifetime=10f,remainingLifetime=10f};
            }
            warning.SetParticles(particles,particles.Length);return;
        }
        warning.Clear();
        if(zap==null)
        {
            if(element>=variants.Length||variants[element]==null)return;
            var visual=Instantiate(variants[element],transform,false);visual.transform.localScale=visualScale;
            foreach(var ps in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.playOnAwake=false;main.loop=false;main.stopAction=ParticleSystemStopAction.None;
                main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.useUnscaledTime=false;
                ps.useAutoRandomSeed=false;ps.randomSeed=17u;
                var lights=ps.lights;lights.enabled=false;
                if(ps.name=="Scorch"||ps.name=="Light Spawn")ps.gameObject.SetActive(false);
                CombatSkillClock.TrackParticle(ps);
            }
            foreach(var light in visual.GetComponentsInChildren<Light>(true))light.enabled=false;
            zapRenderers=visual.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach(var renderer in zapRenderers)
            {renderer.sortingLayerID=layer;renderer.sortingOrder=order;}
            zap=visual.GetComponent<ParticleSystem>();
        }
        if(zap!=null)zap.Simulate(Mathf.Max(.001f,age-HaetaeRank3Rules.HeavenlyDelay),true,true,false);
        if(zapRenderers!=null)foreach(var renderer in zapRenderers)
        {
            var material=renderer.sharedMaterial;
            if(material==null||!material.HasProperty("_EmissiveIntensity"))continue;
            emission??=new MaterialPropertyBlock();
            renderer.GetPropertyBlock(emission);
            float original=material.GetFloat("_EmissiveIntensity");
            emission.SetFloat("_EmissiveIntensity",CombatFlashPresentation.Reduced?Mathf.Min(.75f,original*.2f):original);
            renderer.SetPropertyBlock(emission);
        }
    }

    public void Cancel()
    {if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
}
