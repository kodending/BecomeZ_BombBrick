using System.Collections.Generic;
using UnityEngine;

// A short, broad horn trail and discrete pixel chips. Driven by the combat and authored attack clocks.
public sealed class PixelBeetleSweep:MonoBehaviour
{
    private struct Chip {public Vector3 from,velocity;public float birth,life,size;public bool absorb;}
    private readonly List<Chip> chips=new List<Chip>();
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[320];
    private ParticleSystem system;
    private SpriteRenderer reference;
    private Vector3 origin,direction;
    private float radius,clock,depart;
    private bool departing;
    private static readonly Color Ivory=new Color(1f,.96f,.74f),Gold=new Color(.96f,.64f,.18f);

    public static PixelBeetleSweep Play(SpriteRenderer reference)
    {
        var fx=new GameObject("Pixel Beetle Empowered Horn").AddComponent<PixelBeetleSweep>();
        fx.reference=reference;
        fx.system=PixelMushroomGas.CreateParticles("Horn Edge And Absorbed Chips",fx.transform,320,
            PixelMushroomGas.Material(true),reference,2,true);
        return fx;
    }

    private Vector3 HornTip()
    {
        if(reference==null)return transform.position+Vector3.up;
        Vector3 right=Camera.main==null?Vector3.right:Camera.main.transform.right;
        return reference.bounds.center+Vector3.up*reference.bounds.extents.y*.7f+
            right*reference.bounds.extents.x*(reference.flipX?-.4f:.4f);
    }

    public void Absorb(Vector3 from)
    {
        if(this==null)return;
        departing=false;depart=0;
        for(int i=0;i<9;i++)chips.Add(new Chip{from=from+new Vector3(Mathf.Sin(i*2.4f)*.25f,i*.035f,0),
            birth=clock+i*.012f,life=.34f,size=.10f+(i%3)*.025f,absorb=true});
    }

    public void BeginSweep(Vector3 ground,Vector3 forward,float reach)
    {origin=ground;direction=forward;radius=reach;}

    public void Hit(Vector3 center)
    {
        if(this==null)return;
        Quaternion facing=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        for(int i=0;i<18;i++)
        {
            float a=i*2.399963f,seed=Mathf.Repeat(i*.618f+.13f,1f);
            chips.Add(new Chip{from=center,velocity=facing*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(2f+seed*3f),
                birth=clock,life=.18f,size=.06f+seed*.09f});
        }
        while(chips.Count>180)chips.RemoveAt(0);
    }

    public bool Pose(float dt,float phase,bool attacking,bool ready)
    {
        if(this==null||reference==null)return true;
        clock+=dt;int count=0;Vector3 tip=HornTip();
        if(!ready&&!attacking&&!departing)
        {departing=true;depart=clock;Hit(tip);}
        for(int i=chips.Count-1;i>=0;i--)
        {
            Chip chip=chips[i];float age=clock-chip.birth,t=age/chip.life;
            if(t>=1){chips.RemoveAt(i);continue;}if(t<0)continue;
            Vector3 point=chip.absorb?Vector3.Lerp(chip.from,tip,t*t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.35f:
                chip.from+chip.velocity*age-Vector3.up*4f*age*age;
            Add(ref count,point,chip.size,Color.Lerp(Gold,Ivory,chip.absorb?t:1-t),chip.absorb?1:1-t);
        }
        if(ready)
        {
            for(int i=0;i<5;i++)
            {
                float p=Mathf.Repeat(clock*.75f+i*.2f,1f);
                Add(ref count,tip+Vector3.up*(p-.5f)*.24f+Vector3.right*Mathf.Sin(i*2.4f)*.08f,.045f,Gold,.45f);
            }
        }
        if(attacking&&phase>=0&&phase<=1)
        {
            Vector3 side=Vector3.Cross(Vector3.up,direction);
            float fade=Mathf.Clamp01((1-phase)*5f),angle=Mathf.Lerp(-Mathf.PI*.5f,Mathf.PI*.5f,phase);
            // A thick ivory leading edge with two short, darker stepped trails; no full ring or lingering line.
            for(int i=0;i<22;i++)
            {
                float tail=i/21f,a=angle-tail*.8f;
                if(a<-Mathf.PI*.5f)continue;
                Vector3 radial=direction*Mathf.Cos(a)+side*Mathf.Sin(a);
                for(int band=0;band<3;band++)
                {
                    float r=radius*(1-band*.065f);
                    Add(ref count,origin+radial*r+Vector3.up*.55f,
                        (.10f+.16f*(1-tail))*(band==0?1f:.8f),band==0?Ivory:Gold,
                        (1-tail)*fade*(band==0?1f:.6f));
                }
            }
        }
        system.SetParticles(particles,count);
        var renderer=system.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID=reference.sortingLayerID;renderer.sortingOrder=reference.sortingOrder+2;
        return departing&&clock-depart>.3f;
    }

    private void Add(ref int count,Vector3 point,float size,Color color,float alpha)
    {
        if(count>=particles.Length)return;color.a*=alpha;
        particles[count++]=new ParticleSystem.Particle{position=point,startSize3D=Vector3.one*size,
            startColor=color,startLifetime=10,remainingLifetime=10,randomSeed=(uint)(count+1)};
    }

    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
}
