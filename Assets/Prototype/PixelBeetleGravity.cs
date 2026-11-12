using UnityEngine;

// Fixed fragments rise once, hover, then drop. They share the combat field's clock.
public sealed class PixelBeetleGravity:MonoBehaviour
{
    private readonly SpriteRenderer[] stones=new SpriteRenderer[28];
    private readonly Vector3[] origins=new Vector3[28];
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[80];
    private ParticleSystem currents;
    private PixelMammothCharge impact;
    private Vector3 center;
    private float radius,time;

    public static PixelBeetleGravity Play(Vector3 center,float radius,SpriteRenderer reference)
    {
        var fx=new GameObject("Pixel Beetle Zero Gravity").AddComponent<PixelBeetleGravity>();
        fx.center=center;fx.radius=radius;
        fx.currents=PixelMushroomGas.CreateParticles("Rising Pixel Currents",fx.transform,80,
            PixelMushroomGas.Material(true),reference,-1,true);
        fx.impact=PixelMammothCharge.Play(reference,Vector3.forward);
        fx.impact.transform.SetParent(fx.transform,true);
        for(int i=0;i<fx.stones.Length;i++)
        {
            float a=i*2.399963f,r=radius*.96f*Mathf.Sqrt((i+.5f)/fx.stones.Length);
            fx.origins[i]=center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
            var stone=new GameObject("Hovering Ground Chip").AddComponent<SpriteRenderer>();
            stone.transform.SetParent(fx.transform,true);stone.sprite=PixelCombatEffect.BearSpiritParticleSprite;
            stone.sortingLayerID=reference==null?0:reference.sortingLayerID;
            stone.sortingOrder=reference==null?10:reference.sortingOrder;
            fx.stones[i]=stone;
        }
        fx.Pose(0);return fx;
    }

    public void Pose(float age)
    {
        if(this==null)return;time=age;
        float fall=Mathf.Clamp01((age-BeetleRank3Rules.GravityFall)/BeetleRank3Rules.GravityDrop);
        float fade=1f-Mathf.Clamp01((age-BeetleRank3Rules.GravityLanding)/.22f);
        Quaternion facing=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        for(int i=0;i<stones.Length;i++)
        {
            float seed=Mathf.Repeat(i*.618034f,.99f),phase=i*2.4f;
            float lift=Mathf.Sin(Mathf.Clamp01((age-seed*.07f)/.23f)*Mathf.PI*.5f)*(1f-fall);
            var stone=stones[i];
            stone.transform.position=origins[i]+new Vector3(Mathf.Sin(age*1.4f+phase)*.06f,
                (.32f+seed*.9f+Mathf.Sin(age*3f+phase)*.06f)*lift,Mathf.Cos(age*1.7f+phase)*.04f);
            stone.transform.rotation=facing*Quaternion.Euler(0,0,phase*30f+Mathf.Sin(age*2f+phase)*12f);
            stone.transform.localScale=Vector3.one*((.12f+seed*.14f)/stone.sprite.bounds.size.x);
            Color color=MeadowAmbientParticles.TerrainColor(Color.Lerp(new Color(.39f,.32f,.43f),new Color(.83f,.72f,.58f),seed));
            color.a=fade;stone.color=color;
        }
        for(int i=0;i<particles.Length;i++)
        {
            float seed=Mathf.Repeat(i*.618034f+.13f,1f),a=i*2.399963f;
            float spread=Mathf.Clamp01(age/.22f),r=radius*Mathf.Sqrt((i+.5f)/particles.Length)*spread;
            float pulse=Mathf.Repeat(age*(.55f+seed*.35f)+seed,1f);
            Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            float height=age<.22f?spread*.55f:pulse*(.7f+seed*.7f);
            particles[i]=new ParticleSystem.Particle{position=center+radial*r+Vector3.up*height*(1f-fall),
                startColor=new Color(.68f,.58f,.91f,(.20f+.4f*Mathf.Sin(pulse*Mathf.PI))*fade),
                startSize3D=new Vector3(.035f+seed*.025f,.07f+seed*.10f,.04f)*(age<.22f?1.7f:1f),
                startLifetime=10,remainingLifetime=10,randomSeed=(uint)(i+1)};
        }
        currents.SetParticles(particles,particles.Length);impact.Pose(center,age);
    }

    public void Land(Vector3 ground,Vector3 hit)
    {
        if(this==null)return;
        impact.Impact(ground,time,.55f);impact.Hit(hit,time,false);
    }

    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
}
