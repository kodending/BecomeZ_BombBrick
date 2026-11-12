using UnityEngine;

// Native pixel puffs form a thick moving pressure front; the same clock resolves contacts.
public sealed class PixelHaetaeJusticeRoar:MonoBehaviour
{
    private ParticleSystem mist,pixels;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[160];
    private SpriteRenderer caster;
    private Vector3 origin;
    private float radius,windup;
    private Color tint;

    public static Color Tint(int element)=>element==1?new Color(1f,.28f,.055f):
        element==2?new Color(1f,.77f,.30f):element==3?new Color(.57f,.25f,.77f):new Color(.8f,.93f,1f);

    public static PixelHaetaeJusticeRoar Create(SpriteRenderer caster,Vector3 origin,float radius,float windup,int element)
    {
        var fx=new GameObject("Pixel Justice Roar").AddComponent<PixelHaetaeJusticeRoar>();
        fx.caster=caster;fx.origin=origin;fx.radius=radius;fx.windup=windup;
        fx.tint=PixelSpiritConcert.Tint(caster,Tint(element));
        fx.mist=PixelMushroomGas.CreateParticles("Roar Pressure Front",fx.transform,160,PixelMushroomGas.Material(false),caster,3,true);
        fx.pixels=PixelMushroomGas.CreateParticles("Roar Pixel Crest",fx.transform,160,PixelMushroomGas.Material(true),caster,4,true);
        fx.Pose(0f,origin);return fx;
    }
    private static float Seed(int n)=>Mathf.Repeat(n*.618034f+.23f,1f);
    private static ParticleSystem.Particle Particle(Vector3 p,Vector3 size,Color color,int n)=>
        new ParticleSystem.Particle{position=p,startSize3D=size,startColor=CombatFlashPresentation.Peak(color),startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(n+1)};

    public void Pose(float time,Vector3 center)
    {
        if(this==null)return;origin=center;
        float age=time-windup;
        var camera=Camera.main;Vector3 right=camera==null?Vector3.right:camera.transform.right;
        Vector3 up=camera==null?Vector3.up:camera.transform.up;
        Vector3 mouth=caster==null?origin+Vector3.up*.6f:caster.bounds.center+up*caster.bounds.size.y*.12f+
            right*caster.bounds.size.x*(caster.flipX?-.22f:.22f);
        for(int layer=0;layer<2;layer++)
        {
            int count=0;
            if(age<0f)
            {
                float prepare=Mathf.Clamp01(time/Mathf.Max(.01f,windup));
                for(int i=0;i<40;i++)
                {
                    float s=Seed(i),angle=i*2.399963f;
                    float distance=(.28f+s*.6f)*(1f-prepare*.88f);
                    Vector3 p=mouth+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*distance;
                    Color color=Color.Lerp(tint,Color.white,.45f);color.a=(layer==0?.28f:.85f)*Mathf.Sin(prepare*Mathf.PI);
                    particles[count++]=Particle(p,Vector3.one*(layer==0?.13f:.035f+s*.035f),color,i);
                }
            }
            else
            {
                float progress=HaetaeCombatRules.WaveProgress(age),front=radius*progress;
                float fade=Mathf.Clamp01(1f-Mathf.Max(0f,age-HaetaeCombatRules.WaveDuration)/.18f);
                for(int i=0;i<144;i++)
                {
                    float s=Seed(i),angle=i*2.399963f;
                    float distance=Mathf.Max(0f,front-radius*(.02f+Seed(i+53)*.14f)*progress);
                    Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
                    Vector3 p=origin+direction*distance+Vector3.up*(.10f+s*.42f);
                    Color color=Color.Lerp(tint,Color.white,layer==0?.25f:.65f);
                    color.a=(layer==0?.24f:.66f)*fade*(.5f+s*.5f)*Mathf.Clamp01(age/.018f);
                    float size=layer==0?radius*(.09f+s*.06f):.055f+s*.055f;
                    particles[count++]=Particle(p,new Vector3(size,size*(layer==0?1.45f:1f),size),color,i);
                }
                // Short mouth blast links the outward wave to the roaring frame.
                if(age<.13f)
                    for(int i=0;i<12;i++)
                    {
                        float s=Seed(i),a=i*2.4f;
                        Vector3 p=mouth+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*age*(2f+s*3f);
                        Color color=Color.Lerp(tint,Color.white,.7f);color.a=(1f-age/.13f)*(layer==0?.7f:1f);
                        particles[count++]=Particle(p,Vector3.one*(layer==0?.15f+s*.12f:.035f+s*.04f),color,i);
                    }
            }
            (layer==0?mist:pixels).SetParticles(particles,count);
        }
    }

    public static void Contact(SpriteRenderer reference,Vector3 point,int element,bool cleanse)
    {
        var ps=PixelMushroomGas.CreateParticles(cleanse?"Roar Cleansed Debris":"Roar Pressure Contact",null,
            cleanse?24:14,PixelMushroomGas.Material(true),reference,5,false);
        float radius=reference==null?.35f:Mathf.Clamp(reference.bounds.size.y*.38f,.25f,.7f);
        Color tint=Tint(element);var camera=Camera.main;
        Vector3 right=camera==null?Vector3.right:camera.transform.right,up=camera==null?Vector3.up:camera.transform.up;
        for(int i=0;i<(cleanse?24:14);i++)
        {
            float a=i*2.399963f,s=Seed(i);Vector3 direction=right*Mathf.Cos(a)+up*Mathf.Sin(a);
            Color color=cleanse&&i%2==0?new Color(.16f,.12f,.25f,.9f):Color.Lerp(tint,Color.white,.55f);
            ps.Emit(new ParticleSystem.EmitParams{position=point+direction*radius*(cleanse?.8f:.15f),
                velocity=direction*(cleanse?.9f:1.8f)+up*(cleanse?.5f:0f),startSize=.04f+s*.05f,
                startLifetime=cleanse?.38f:.22f,startColor=color},1);
        }
        var fade=ps.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});fade.color=gradient;
        if(Application.isPlaying)Destroy(ps.gameObject,.45f);
    }
    public void Cancel()
    {if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
}
