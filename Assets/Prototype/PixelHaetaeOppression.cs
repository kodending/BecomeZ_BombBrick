using System.Collections.Generic;
using UnityEngine;

// The combat clock drives the floor, five pressure fronts and impact particles together.
public sealed class PixelHaetaeOppression:MonoBehaviour
{
    private ParticleSystem floor,crest,guard;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[192];
    private readonly ParticleSystem.Particle[] guards=new ParticleSystem.Particle[192];
    private readonly List<Impact> impacts=new List<Impact>();
    private int guardCount;
    private float radius;
    private Color tint;
    private sealed class Impact
    {
        public ParticleSystem ps;
        public SpriteRenderer target;
        public Vector3 point;
        public float time;
    }

    public static PixelHaetaeOppression Create(SpriteRenderer reference,float radius,int element)
    {
        var fx=new GameObject("Pixel Divine Oppression").AddComponent<PixelHaetaeOppression>();
        fx.radius=radius;
        fx.tint=PixelSpiritConcert.Tint(reference,PixelHaetaeJusticeRoar.Tint(element));
        // Floor-level mist stays beneath character art. Contacts get the target's own order.
        fx.floor=PixelMushroomGas.CreateParticles("Oppression Ground Breath",fx.transform,192,
            PixelMushroomGas.Material(false),reference,-3,true);
        fx.crest=PixelMushroomGas.CreateParticles("Oppression Pixel Front",fx.transform,192,
            PixelMushroomGas.Material(true),reference,-2,true);
        fx.guard=PixelMushroomGas.CreateParticles("Oppression Ally Foot Motes",fx.transform,192,
            PixelMushroomGas.Material(true),reference,-1,true);
        return fx;
    }

    private static float Seed(int n)=>Mathf.Repeat(n*.618034f+.23f,1f);
    private static ParticleSystem.Particle Particle(Vector3 point,Vector3 size,Color color,int seed)=>
        new ParticleSystem.Particle{position=point,startSize3D=size,startColor=color,
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(seed+1)};

    public void Pose(float age,Vector3 center)
    {
        if(this==null)return;
        transform.position=center;guardCount=0;
        float spread=HaetaeCombatRules.WaveProgress(age);
        float fade=Mathf.Clamp01((HaetaeRank3Rules.OppressionDuration-age)/.4f);
        float pulseAge=Mathf.Repeat(age,1f);
        float front=HaetaeCombatRules.WaveProgress(pulseAge);
        float pulseFade=Mathf.Clamp01(1f-Mathf.Max(0f,pulseAge-HaetaeCombatRules.WaveDuration)/.16f);
        for(int layer=0;layer<2;layer++)
        {
            int count=0;
            int ambientCount=layer==0?40:64;
            for(int i=0;i<ambientCount;i++)
            {
                float s=Seed(i),a=i*2.399963f+age*(.08f+s*.08f);
                float flow=Mathf.Repeat(age*(.15f+s*.08f)+s,1f);
                float distance=radius*Mathf.Sqrt((i+.5f)/ambientCount)*(.72f+flow*.2f)*spread;
                Vector3 direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 point=center+direction*distance+Vector3.up*(.055f+s*.12f);
                Color color=Color.Lerp(tint,Color.white,layer==0?.12f:.45f);
                color.a=fade*spread*(layer==0?.18f:.5f)*Mathf.Sin(flow*Mathf.PI);
                float size=layer==0?radius*(.17f+s*.12f):.04f+s*.035f;
                particles[count++]=Particle(point,new Vector3(size,size*(layer==0?.56f:1f),size),color,i);
            }
            if(pulseFade>0f)
                for(int i=0;i<(layer==0?56:104);i++)
                {
                    float s=Seed(i+31),a=i*2.399963f;
                    Vector3 direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    float distance=radius*front*(.68f+s*.30f);
                    Vector3 point=center+direction*distance+Vector3.up*(.06f+Mathf.Sin(front*Mathf.PI)*(.08f+s*.13f));
                    Color color=Color.Lerp(tint,Color.white,layer==0?.30f:.65f);
                    color.a=(layer==0?.30f:.68f)*pulseFade*fade*Mathf.Clamp01(pulseAge/.015f);
                    float size=layer==0?radius*(.095f+s*.055f):.045f+s*.045f;
                    particles[count++]=Particle(point,new Vector3(size,size*(layer==0?.65f:1f),size),color,i+80);
                }
            (layer==0?floor:crest).SetParticles(particles,count);
        }
        for(int i=impacts.Count-1;i>=0;i--)
        {
            var hit=impacts[i];float t=(age-hit.time)/.23f;
            if(hit.ps==null||t>=1f)
            {if(hit.ps!=null)DestroyFx(hit.ps.gameObject);impacts.RemoveAt(i);continue;}
            Vector3 right=Camera.main==null?Vector3.right:Camera.main.transform.right;
            Vector3 up=Camera.main==null?Vector3.up:Camera.main.transform.up;
            if(hit.target!=null)
            {
                hit.point=hit.target.bounds.center;
                hit.ps.GetComponent<ParticleSystemRenderer>().sortingOrder=hit.target.sortingOrder+5;
            }
            for(int n=0;n<18;n++)
            {
                float a=n*2.399963f,s=Seed(n);Vector3 direction=right*Mathf.Cos(a)+up*Mathf.Sin(a)*.6f;
                Color color=Color.Lerp(tint,Color.white,.65f);color.a=(1f-t)*.9f;
                particles[n]=Particle(hit.point+direction*(.12f+t*(.45f+s*.2f))-up*t*.12f,
                    Vector3.one*(.04f+s*.04f)*(1f-t*.45f),color,n);
            }
            hit.ps.SetParticles(particles,18);
        }
    }

    public void Protect(SpriteRenderer reference,Vector3 point,float age)
    {
        if(this==null)return;
        float size=reference==null?.3f:Mathf.Clamp(reference.bounds.size.x*.3f,.2f,.55f);
        for(int i=0;i<8&&guardCount<guards.Length;i++)
        {
            float a=i*Mathf.PI*.25f+age*.6f;
            Color color=Color.Lerp(tint,Color.white,.55f);color.a=.6f;
            Vector3 p=point+new Vector3(Mathf.Cos(a)*size,.08f+Seed(i)*.09f,Mathf.Sin(a)*size);
            guards[guardCount++]=Particle(p,Vector3.one*(.045f+Seed(i)*.02f),color,i);
        }
    }
    public void EndProtectionFrame(){if(this!=null)guard.SetParticles(guards,guardCount);}
    public void Contact(SpriteRenderer reference,Vector3 point,float age)
    {
        if(this==null)return;
        impacts.Add(new Impact{target=reference,point=point,time=age,
            ps=PixelMushroomGas.CreateParticles("Oppression Pressure Contact",transform,18,
                PixelMushroomGas.Material(true),reference,5,true)});
    }
    private static void DestroyFx(GameObject go)
    {go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;DestroyFx(gameObject);}
}
