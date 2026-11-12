using UnityEngine;

// Driven by the battle clock: budding, flight and debris freeze together on pause.
public sealed class PixelMushroomMigration:MonoBehaviour
{
    private SpriteRenderer bud;
    private ParticleSystem dust;
    private readonly ParticleSystem.Particle[] flecks=new ParticleSystem.Particle[24];
    private Vector3 origin;
    private Color tint;

    public static PixelMushroomMigration Play(Vector3 origin,SpriteRenderer reference,Color tint)
    {
        var fx=new GameObject("Migrating Mushroom Spore").AddComponent<PixelMushroomMigration>();
        fx.origin=origin;fx.tint=tint;
        fx.bud=fx.gameObject.AddComponent<SpriteRenderer>();fx.bud.sprite=PixelMycoking.SporeBudSprite;
        fx.bud.sortingLayerID=reference==null?0:reference.sortingLayerID;
        fx.bud.sortingOrder=reference==null?10:reference.sortingOrder;
        fx.bud.spriteSortPoint=SpriteSortPoint.Pivot;
        fx.dust=PixelMushroomGas.CreateParticles("Trailing Spore Flecks",fx.transform,fx.flecks.Length,
            PixelMushroomGas.Material(true),reference,1,true);
        fx.Pose(origin,0f);return fx;
    }

    private Vector3 Point(Vector3 target,float t)
    {
        Vector3 delta=target-origin;
        Vector3 side=Vector3.Cross(Vector3.up,delta).normalized;
        float arc=Mathf.Sin(t*Mathf.PI);
        return Vector3.Lerp(origin,target,t)+Vector3.up*arc*1.7f+side*arc*.35f;
    }

    public void Pose(Vector3 target,float age)
    {
        if(this==null)return;
        float t=Mathf.Clamp01((age-.16f)/(MushroomPropagationRules.FlightDuration-.16f));
        float grow=Mathf.SmoothStep(.05f,1f,Mathf.Clamp01(age/.16f));
        Quaternion facing=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        transform.SetPositionAndRotation(Point(target,t),facing*Quaternion.Euler(0f,0f,Mathf.Sin(t*Mathf.PI*2f)*18f));
        transform.localScale=Vector3.one*1.9f*PixelStarProjectile.MushroomSporeVisualMultiplier*grow;
        bud.color=Color.Lerp(Color.white,tint,.18f);
        for(int i=0;i<flecks.Length;i++)
        {
            float seed=Mathf.Repeat(i*.618034f+.17f,1f),lag=(i+1f)/flecks.Length;
            float tail=Mathf.Max(0f,t-lag*.22f);
            Vector3 flutter=facing*new Vector3(Mathf.Sin(i*2.4f+age*5f),Mathf.Cos(i*1.9f+age*3f),0f);
            Color color=Color.Lerp(tint,new Color(1f,.87f,.53f),seed);
            color.a=(1f-lag)*.75f*grow*Mathf.Clamp01(t*12f);
            flecks[i]=new ParticleSystem.Particle {position=Point(target,tail)+flutter*(.08f+lag*.25f),
                startSize3D=Vector3.one*(.035f+seed*.055f)*PixelStarProjectile.MushroomSporeVisualMultiplier,startColor=color,
                startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(i+1)};
        }
        dust.SetParticles(flecks,t>0f?flecks.Length:0);
    }

    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
}
