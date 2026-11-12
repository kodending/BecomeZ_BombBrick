using System.Collections.Generic;
using UnityEngine;

// A thick rolling pressure front, with a trailing wake and ground-level grit.
// Poses use the same clock as cone contacts; no independent particle timer can outrun combat.
public sealed class PixelHaetaeBellow:MonoBehaviour
{
    private ParticleSystem air,crest,ground;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[224];
    private readonly List<ContactCloud> contacts=new List<ContactCloud>();
    private SpriteRenderer caster;
    private Vector3 origin,forward,side,mouth;
    private float radius;
    private Color tint;

    private sealed class ContactCloud
    {
        public Transform target;
        public SpriteRenderer renderer;
        public ParticleSystem air,dust;
        public Vector3 start;
        public float time;
    }

    public static PixelHaetaeBellow Create(SpriteRenderer reference,Vector3 origin,Vector3 direction,float radius,int element)
    {
        var fx=new GameObject("Pixel Judgment Bellow").AddComponent<PixelHaetaeBellow>();
        fx.caster=reference;fx.origin=origin;fx.forward=direction.normalized;
        fx.side=Vector3.Cross(Vector3.up,fx.forward);fx.radius=radius;
        fx.tint=PixelSpiritConcert.Tint(reference,PixelHaetaeJusticeRoar.Tint(element));
        fx.air=PixelMushroomGas.CreateParticles("Bellow Dense Air",fx.transform,224,
            PixelMushroomGas.Material(false),reference,4,true);
        fx.crest=PixelMushroomGas.CreateParticles("Bellow Pressure Flecks",fx.transform,224,
            PixelMushroomGas.Material(true),reference,5,true);
        fx.ground=PixelMushroomGas.CreateParticles("Bellow Swept Ground",fx.transform,224,
            PixelMushroomGas.Material(false),reference,-1,true);
        fx.Pose(0f);return fx;
    }
    private static float Seed(int n)=>Mathf.Repeat(Mathf.Sin(n*127.1f+311.7f)*43758.5453f,1f);
    private static ParticleSystem.Particle Particle(Vector3 p,Vector3 size,Color color,int seed)=>
        new ParticleSystem.Particle{position=p,startSize3D=size,startColor=CombatFlashPresentation.Peak(color),
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(seed+1)};
    private Vector3 Ray(float degrees)=>forward*Mathf.Cos(degrees*Mathf.Deg2Rad)+side*Mathf.Sin(degrees*Mathf.Deg2Rad);

    public void Pose(float time)
    {
        if(this==null)return;
        float age=time-HaetaeRank3Rules.BellowWindup;
        if(age<=0f||mouth==Vector3.zero)
            mouth=caster==null?origin+Vector3.up*.65f:caster.bounds.center+forward*caster.bounds.size.x*.26f;
        if(age<0f){Prepare(time);return;}
        float progress=HaetaeRank3Rules.BellowFront(age);
        float fade=Mathf.Clamp01(1f-Mathf.Max(0f,age-HaetaeRank3Rules.BellowWaveDuration)/.28f);
        float appear=Mathf.Clamp01(age/.018f),front=radius*progress;
        float height=Mathf.Clamp(mouth.y-origin.y,.35f,1.4f);
        for(int layer=0;layer<2;layer++)
        {
            int count=0;
            // Overlapping tiers give the crescent a rolling, volumetric leading face.
            for(int i=0;i<108;i++)
            {
                int tier=i/36;float s=Seed(i),angle=-HaetaeRank3Rules.BellowAngle*.5f+
                    (i%36+.5f)/36f*HaetaeRank3Rules.BellowAngle;
                Vector3 ray=Ray(angle);
                float roll=age*24f+s*6.283f;
                float thickness=(.12f+tier*.14f+Seed(i+167)*.18f)*progress;
                float distance=Mathf.Max(0f,front-thickness);
                Vector3 point=origin+ray*distance+Vector3.up*(height+(tier-1)*(.18f+progress*.38f)+
                    Mathf.Sin(roll)*progress*.18f);
                point+=ray*Mathf.Cos(roll)*progress*.13f;
                float size=(.24f+s*.28f)*(1f+progress*1.05f);
                Color color=Color.Lerp(tint,Color.white,tier==0?.82f:.50f);
                color.a=(layer==0?(tier==0?.5f:.26f):.8f)*fade*appear;
                particles[count++]=Particle(point,layer==0?new Vector3(size*.78f,size*1.38f,size):
                    Vector3.one*(.035f+s*.065f),color,i);
            }
            // A few broken lanes of air stretch back toward the open mouth.
            for(int i=0;i<72;i++)
            {
                float s=Seed(i+400),lane=(i%8+.5f)/8f;
                float radial=.10f+(i/8f)/9f*.7f;
                Vector3 ray=Ray((lane-.5f)*HaetaeRank3Rules.BellowAngle*.82f);
                Vector3 point=Vector3.Lerp(mouth,origin+ray*front+Vector3.up*height,radial);
                point+=side*Mathf.Sin(age*23f+i)*.1f*progress+Vector3.up*Mathf.Sin(age*20f+i*.7f)*.13f;
                Color color=Color.Lerp(tint,Color.white,.62f);
                color.a=(layer==0?.18f:.45f)*fade*appear*(1f-radial*.45f);
                float size=(.24f+s*.27f)*(.6f+progress);
                particles[count++]=Particle(point,layer==0?new Vector3(size,size*.65f,size):
                    Vector3.one*(.025f+s*.045f),color,i+108);
            }
            if(age<.12f)
                for(int i=0;i<24;i++)
                {
                    float s=Seed(i+800),a=i*2.4f;
                    Vector3 point=mouth+forward*age*9f+(side*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a))*age*3.5f;
                    Color color=Color.Lerp(tint,Color.white,.9f);color.a=(1f-age/.12f)*(layer==0?.7f:1f);
                    particles[count++]=Particle(point,Vector3.one*(layer==0?.24f+s*.2f:.045f+s*.04f),color,i+180);
                }
            (layer==0?air:crest).SetParticles(particles,count);
        }
        for(int i=0;i<70;i++)
        {
            float s=Seed(i+900),angle=(Seed(i+977)-.5f)*HaetaeRank3Rules.BellowAngle;
            Vector3 ray=Ray(angle);float distance=front*(.2f+s*.79f);
            Vector3 point=origin+ray*distance+Vector3.up*(.045f+Mathf.Sin(age*25f+i)*.035f);
            Color color=MeadowAmbientParticles.TerrainColor(Color.Lerp(new Color(.43f,.34f,.22f),new Color(.87f,.81f,.64f),s));
            color.a=.46f*fade*appear*(.5f+s*.5f);
            float size=.18f+s*.26f;
            particles[i]=Particle(point,new Vector3(size,size*.50f,size),color,i);
        }
        ground.SetParticles(particles,70);
        PoseContacts(time);
    }

    private void Prepare(float time)
    {
        float t=Mathf.Clamp01(time/HaetaeRank3Rules.BellowWindup);
        for(int layer=0;layer<2;layer++)
        {
            for(int i=0;i<42;i++)
            {
                float s=Seed(i),a=i*2.399963f;
                float distance=(.35f+s*.7f)*(1f-t*.87f);
                Vector3 point=mouth+forward*(.12f+s*.5f)*(1f-t)+
                    (side*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a)*.6f)*distance;
                Color color=Color.Lerp(tint,Color.white,.7f);color.a=(layer==0?.30f:.75f)*Mathf.Sin(t*Mathf.PI*.8f);
                particles[i]=Particle(point,Vector3.one*(layer==0?.14f+s*.12f:.035f+s*.035f),color,i);
            }
            (layer==0?air:crest).SetParticles(particles,42);
        }
        for(int i=0;i<24;i++)
        {
            float s=Seed(i+80);Vector3 direction=Ray((s-.5f)*150f);
            Vector3 point=origin+direction*(.3f+(1f-t)*1.3f)+Vector3.up*.07f;
            Color color=MeadowAmbientParticles.TerrainColor(new Color(.77f,.70f,.55f,.24f*Mathf.Sin(t*Mathf.PI)));
            particles[i]=Particle(point,new Vector3(.17f,.075f,.17f),color,i);
        }
        ground.SetParticles(particles,24);
    }

    public void Contact(SpriteRenderer reference,Transform target,float time)
    {
        if(this==null||target==null)return;
        contacts.Add(new ContactCloud{target=target,renderer=reference,start=target.position,time=time,
            air=PixelMushroomGas.CreateParticles("Bellow Body Impact",transform,36,PixelMushroomGas.Material(true),reference,5,true),
            dust=PixelMushroomGas.CreateParticles("Bellow Drag Dust",transform,32,PixelMushroomGas.Material(false),reference,-1,true)});
        PoseContacts(time);
    }

    private void PoseContacts(float time)
    {
        foreach(var hit in contacts)
        {
            float age=time-hit.time,t=Mathf.Clamp01(age/HaetaeRank3Rules.BellowPushDuration);
            if(hit.target==null||age>.58f)
            {hit.air.SetParticles(particles,0);hit.dust.SetParticles(particles,0);continue;}
            Vector3 center=hit.renderer==null?hit.target.position+Vector3.up*.45f:hit.renderer.bounds.center;
            for(int i=0;i<32;i++)
            {
                float s=Seed(i+31),a=i*2.4f;
                Vector3 point=center+(side*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a))*(.14f+t*.36f)+forward*t*.32f;
                Color color=Color.Lerp(tint,Color.white,.82f);color.a=Mathf.Clamp01(1f-age/.24f)*.95f;
                particles[i]=Particle(point,Vector3.one*(.035f+s*.055f),color,i);
            }
            hit.air.SetParticles(particles,32);
            for(int i=0;i<30;i++)
            {
                float s=Seed(i+54),trail=Mathf.Repeat(age*4f+s,1f);
                Vector3 point=Vector3.Lerp(hit.start,hit.target.position,1f-trail*.7f)+
                    side*(Seed(i+16)-.5f)*.8f;
                point.y=origin.y+.06f+Mathf.Sin(trail*Mathf.PI)*.15f;
                Color color=MeadowAmbientParticles.TerrainColor(Color.Lerp(new Color(.47f,.36f,.22f),new Color(.86f,.80f,.61f),s));
                color.a=.48f*(1f-trail)*Mathf.Clamp01((.58f-age)/.16f);
                float size=.16f+s*.2f;
                particles[i]=Particle(point,new Vector3(size,size*.62f,size),color,i);
            }
            hit.dust.SetParticles(particles,30);
        }
    }
    public void Cancel()
    {if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
}
