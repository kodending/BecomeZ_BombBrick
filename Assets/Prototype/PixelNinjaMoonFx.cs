using UnityEngine;

// Local sprite bursts and short approach streaks; never draws a line between combatants.
public sealed class PixelNinjaMoonFx:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite impactSprite,shardSprite,pixelSprite;
    private SpriteRenderer core,echo;
    private SpriteRenderer[] shards;
    private Vector3[] rays;
    private float elapsed,size;
    private bool burst,rush;
    private static readonly Color Violet=new Color(.64f,.35f,1f);
    private Color tint=Violet;

    public static void PlayRush(SpriteRenderer source,Vector3 direction,bool departure)
    {
        if(source==null||source.sprite==null)return;
        Vector3 travel=direction.normalized;
        for(int i=0;i<2;i++)
        {
            var ghost=Rank3CombatFx.Ghost("Black Moon Rush Echo",source,
                source.transform.position-travel*(departure?.15f+i*.2f:.45f+i*.42f),
                new Color(.7f,.52f,1f,.62f-i*.20f),1f,.1f+i*.025f);
            if(ghost!=null){ghost.transform.rotation=source.transform.rotation;ghost.sortingOrder=source.sortingOrder-1;}
        }
        var fx=Create(source,false);fx.rush=true;fx.name="Black Moon Vanishing Streaks";
        Vector3 screen=fx.transform.InverseTransformDirection(travel);
        float angle=Mathf.Atan2(screen.y,screen.x)*Mathf.Rad2Deg;
        fx.transform.rotation*=Quaternion.Euler(0,0,angle);
        fx.shards=new SpriteRenderer[5];fx.rays=new Vector3[5];
        for(int i=0;i<5;i++)
        {
            fx.shards[i]=fx.Child("Short Speed Shard",Shard,Color.Lerp(Violet,Color.white,i%2),0);
            fx.shards[i].transform.localRotation=Quaternion.Euler(0,0,-90f);
            fx.rays[i]=new Vector3(-.2f-(i%2)*.15f,(i-2)*.14f,0);
        }
        fx.Animate(0f);
    }

    public static PixelNinjaMoonFx PlayImpact(SpriteRenderer target,Vector3 direction,bool burst,Color? color=null)
    {
        if(target==null)return null;
        var fx=Create(target,burst);fx.name=burst?"Black Moon Shatter Burst":"Black Moon Contact Burst";
        fx.tint=color??Violet;
        fx.core=fx.Child("White Impact Core",Impact,Color.white,2);
        fx.echo=fx.Child("Impact Edge",Impact,fx.tint,0);
        fx.shards=new SpriteRenderer[burst?14:8];fx.rays=new Vector3[fx.shards.Length];
        for(int i=0;i<fx.shards.Length;i++)
        {
            float angle=(i*360f/fx.shards.Length+(burst?12f:25f))*Mathf.Deg2Rad;
            fx.rays[i]=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            fx.shards[i]=fx.Child("Flying Moon Fragment",i%3==0?Pixel:Shard,i%2==0?Color.white:fx.tint,3);
            fx.shards[i].transform.localRotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg-90f);
        }
        fx.Animate(0f);return fx;
    }

    private static PixelNinjaMoonFx Create(SpriteRenderer target,bool burst)
    {
        var fx=new GameObject().AddComponent<PixelNinjaMoonFx>();fx.burst=burst;
        fx.transform.position=target.bounds.center;
        fx.transform.rotation=Camera.main==null?target.transform.rotation:Camera.main.transform.rotation;
        fx.size=Mathf.Clamp(target.bounds.size.y*.8f,1.25f,2.1f)*(burst?1.45f:1f);
        fx.layer=target.sortingLayerID;fx.order=target.sortingOrder+24;
        return fx;
    }
    private int layer,order;
    private SpriteRenderer Child(string name,Sprite sprite,Color color,int extra)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.color=color;
        r.sortingLayerID=layer;r.sortingOrder=order+extra;return r;
    }
    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Animate((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }
    private void Animate(float dt)
    {
        elapsed+=Mathf.Max(0f,dt);
        float life=rush?.13f:burst?.46f:.26f;
        if(elapsed>=life)
        {
            gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
            return;
        }
        if(rush)
        {
            float p=elapsed/life;
            for(int i=0;i<shards.Length;i++)
            {
                shards[i].transform.localPosition=(rays[i]+Vector3.left*p*.5f)*size;
                shards[i].transform.localScale=new Vector3(.065f,1.05f-i*.10f,1)*size*(1f-p*.4f);
                Color c=shards[i].color;c.a=1f-p;shards[i].color=c;
            }
            return;
        }
        float expand=Mathf.Clamp01(elapsed/.055f);
        core.transform.localScale=Vector3.one*size*Mathf.Lerp(.55f,1.05f,expand);
        echo.transform.localScale=core.transform.localScale*1.18f;
        float white=1f-Mathf.InverseLerp(.055f,burst?.18f:.12f,elapsed);
        core.color=new Color(1f,.98f,1f,white);
        echo.color=new Color(tint.r,tint.g,tint.b,1f-Mathf.InverseLerp(.06f,burst?.25f:.17f,elapsed));
        float p2=Mathf.Clamp01(elapsed/life);
        for(int i=0;i<shards.Length;i++)
        {
            float travel=(.12f+Mathf.Sqrt(p2)*(burst?1.15f:.8f))*size;
            Vector3 position=rays[i]*travel;
            position.x=Mathf.Round(position.x*32f)/32f;position.y=Mathf.Round(position.y*32f)/32f;
            shards[i].transform.localPosition=position;
            float width=(i%3==0?.105f:.13f)*size*(1f-p2*.6f);
            shards[i].transform.localScale=new Vector3(width,width*(i%3==0?1f:3.6f),1);
            Color c=shards[i].color;c.a=1f-Mathf.InverseLerp(.35f,1f,p2);shards[i].color=c;
        }
    }

    private static Sprite Pixel=>pixelSprite!=null?pixelSprite:
        pixelSprite=PixelNinjaSubstitution.MakeSprite("Moon Pixel Chip",1,1,1f,(x,y)=>Color.white);
    private static Sprite Shard=>shardSprite!=null?shardSprite:
        shardSprite=PixelNinjaSubstitution.MakeSprite("Moon Tapered Shard",10,24,24f,(x,y)=>
            Mathf.Abs(x-4.5f)<1f+3.5f*Mathf.Sin((y+1f)/25f*Mathf.PI)?Color.white:Color.clear);
    private static Sprite Impact=>impactSprite!=null?impactSprite:
        impactSprite=PixelNinjaSubstitution.MakeSprite("Moon Jagged Impact",40,40,40f,(x,y)=>
        {
            float dx=x-19.5f,dy=y-19.5f,angle=Mathf.Atan2(dy,dx);
            float radius=10f+9f*Mathf.Pow(Mathf.Abs(Mathf.Cos(angle*4f)),10f);
            float distance=Mathf.Sqrt(dx*dx+dy*dy);
            return distance<=radius?Color.white:Color.clear;
        });
}
