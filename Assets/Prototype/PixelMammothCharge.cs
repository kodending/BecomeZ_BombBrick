using System.Collections.Generic;
using UnityEngine;

// Native point-filtered particles; the action clock controls their entire trajectory.
public sealed class PixelMammothCharge:MonoBehaviour
{
    private struct Piece { public Vector3 origin,velocity;public Color color;public float birth,life,size;public bool chip; }
    private readonly List<Piece> pieces=new List<Piece>();
    private readonly ParticleSystem.Particle[] puffs=new ParticleSystem.Particle[128],chips=new ParticleSystem.Particle[128];
    private ParticleSystem dust,debris;
    private Vector3 direction,side;
    private bool scraped;
    private float clock;
    private SpriteRenderer reference;

    public static PixelMammothCharge Play(SpriteRenderer reference,Vector3 direction,bool scrape=true)
    {
        var fx=new GameObject("Pixel Mammoth Earth Charge").AddComponent<PixelMammothCharge>();
        fx.reference=reference;
        fx.scraped=!scrape;fx.direction=direction;fx.side=Vector3.Cross(Vector3.up,direction);
        fx.dust=PixelMushroomGas.CreateParticles("Low Hoof Dust",fx.transform,128,
            PixelMushroomGas.Material(false),reference,0,true);
        fx.debris=PixelMushroomGas.CreateParticles("Earth And Tusk Fragments",fx.transform,128,
            PixelMushroomGas.Material(true),reference,1,true);
        return fx;
    }

    public void Pose(Vector3 ground,float time)
    {
        if(this==null)return;clock=time;
        if(!scraped&&time>=.19f){scraped=true;Step(ground,time,0);}
        Render();
    }

    public void Step(Vector3 ground,float time,int step)
    {
        Vector3 at=ground+side*(step%2==0?.28f:-.28f);
        for(int i=0;i<14;i++)
        {
            float seed=Mathf.Repeat(i*.618f+.17f,1f);
            Add(at+side*(seed-.5f)*.5f,-direction*(1.1f+seed*1.8f)+side*Mathf.Sin(i*2.4f)*1.2f+
                Vector3.up*(.5f+seed*.65f),time,.3f,.09f+seed*.1f,i%3==0,
                MeadowAmbientParticles.TerrainColor(Color.Lerp(new Color(.42f,.29f,.16f,.8f),new Color(.8f,.66f,.43f,.62f),seed)));
        }
        Render();
    }

    public void Hit(Vector3 center,float time,bool steel)
    {
        Quaternion facing=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        for(int i=0;i<18;i++)
        {
            float a=i*2.399963f,seed=Mathf.Repeat(i*.618f+.13f,1f);
            Vector3 spread=facing*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0f);
            Add(center,spread*(2f+seed*2f)+direction*.8f,time,.18f,.055f+seed*.085f,true,
                steel?new Color(.75f,.89f,1f):new Color(1f,.9f,.64f));
        }
        Render();
    }

    public void Impact(Vector3 ground,float time,float radius,bool heavy=false)
    {
        for(int i=0;i<(heavy?88:64);i++)
        {
            float a=i*2.399963f,seed=Mathf.Repeat(i*.618f+.13f,1f);
            Vector3 radial=new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a));
            // Fast radial travel makes the actual landing radius visible before recovery ends.
            Add(ground+Vector3.up*.04f,radial*radius*(.75f+seed*.25f)/.18f+Vector3.up*(.5f+seed)*(heavy&&i%3==0?2.5f:1f),
                time,.22f,((i%3==0?.11f:.2f)+seed*.1f)*(heavy?1.25f:1f),i%3==0,
                MeadowAmbientParticles.TerrainColor(Color.Lerp(new Color(.45f,.3f,.15f,.85f),new Color(.88f,.73f,.48f,.6f),seed)));
        }
        Render();
    }

    private void Add(Vector3 at,Vector3 velocity,float time,float life,float size,bool chip,Color color)
    {
        if(pieces.Count>=240)pieces.RemoveAt(0);
        pieces.Add(new Piece {origin=at,velocity=velocity,birth=time,life=life,size=size,chip=chip,color=PixelSpiritConcert.Tint(reference,color)});
    }

    private void Render()
    {
        int p=0,c=0;
        for(int i=pieces.Count-1;i>=0;i--)
        {
            Piece piece=pieces[i];float age=Mathf.Max(0f,clock-piece.birth),t=age/piece.life;
            if(t>=1f){pieces.RemoveAt(i);continue;}
            Color color=piece.color;color.a*=1f-t*t;
            var particle=new ParticleSystem.Particle {position=piece.origin+piece.velocity*age-Vector3.up*3f*age*age,
                startColor=color,startSize3D=Vector3.one*piece.size*(piece.chip?1f:1f+t*1.2f),
                startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(i+1),rotation=i*37f};
            if(piece.chip){if(c<chips.Length)chips[c++]=particle;}
            else if(p<puffs.Length)puffs[p++]=particle;
        }
        dust.SetParticles(puffs,p);debris.SetParticles(chips,c);
    }

    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
}
