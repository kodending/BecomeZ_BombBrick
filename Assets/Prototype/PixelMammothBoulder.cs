using UnityEngine;
using UnityEngine.Rendering;

// A low-resolution faceted sprite, animated on a fixed pixel grid while it rolls.
[DefaultExecutionOrder(10030)]
public sealed class PixelMammothBoulder:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite[] frames;
    private SpriteRenderer body,shadow;
    private PixelMammothCharge debris;
    private Vector3 origin,direction,ground;
    private float radius,age,height,scaleY=1f,broken=-1f;
    private bool released;
    private readonly SpriteRenderer[] chunks=new SpriteRenderer[9];

    public static PixelMammothBoulder Play(SpriteRenderer reference,Vector3 origin,Vector3 direction,float scale)
    {
        if(frames==null||frames[0]==null)BuildFrames();
        var fx=new GameObject("Pixel Mammoth Mountain Toss").AddComponent<PixelMammothBoulder>();
        fx.origin=origin;fx.direction=direction;fx.radius=MonsterAutoBattlePrototype.TileSize*MammothRank3Rules.TossBodyRadiusTiles*scale;
        var group=fx.gameObject.AddComponent<SortingGroup>();
        group.sortingLayerID=reference==null?0:reference.sortingLayerID;
        group.sortingOrder=reference==null?10:reference.sortingOrder;
        fx.body=fx.Part("Faceted Boulder",frames[0],0);
        fx.shadow=fx.Part("Ground Shadow",PixelStarProjectile.StoneSprite,-1);
        fx.debris=PixelMammothCharge.Play(reference,direction);
        fx.debris.transform.SetParent(fx.transform,true);
        for(int i=0;i<fx.chunks.Length;i++){fx.chunks[i]=fx.Part("Boulder Fragment "+i,frames[i%frames.Length],1);fx.chunks[i].enabled=false;}
        fx.Prepare(0);return fx;
    }
    private SpriteRenderer Part(string name,Sprite sprite,int order)
    {
        var child=new GameObject(name);child.transform.SetParent(transform,false);
        var sr=child.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=order;
        sr.spriteSortPoint=SpriteSortPoint.Pivot;return sr;
    }
    public void Prepare(float time)
    {
        if(this==null)return;
        age=time;ground=origin+direction*MonsterAutoBattlePrototype.TileSize*.55f;
        float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-.17f)/.35f));
        float lift=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-.36f)/.31f));
        scaleY=Mathf.Lerp(.12f,1f,rise);height=radius*scaleY+lift*.65f;
        body.enabled=time>=.17f;body.color=new Color(1,1,1,rise);
        debris.Pose(ground,time);Render();
    }
    public void Fly(Vector3 position,float flight)
    {
        if(this==null)return;
        released=true;ground=position;age=MammothRank3Rules.TossRelease+flight;scaleY=1f;
        float t=Mathf.Clamp01(flight/MammothRank3Rules.TossFirstBounce);
        height=radius+(flight<MammothRank3Rules.TossFirstBounce?
            .65f*(1f-t)+.8f*Mathf.Sin(t*Mathf.PI):.07f*Mathf.Abs(Mathf.Sin((flight-MammothRank3Rules.TossFirstBounce)*31f)));
        body.color=Color.white;debris.Pose(ground,age);Render();
    }
    public void Bounce(float flight){if(this!=null)debris.Impact(ground,MammothRank3Rules.TossRelease+flight,radius*.85f);}
    public void Hit(Vector3 center,float flight,bool steel){if(this!=null)debris.Hit(center,MammothRank3Rules.TossRelease+flight,steel);}
    public void Shatter(float flight,float blastRadius)
    {
        if(this==null||broken>=0)return;
        broken=MammothRank3Rules.TossRelease+flight;
        debris.Impact(ground,broken,blastRadius);body.enabled=false;Render();
    }
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Render();
        }
    }
    private void Render()
    {
        if(body==null)return;
        Quaternion view=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        Vector3 up=view*Vector3.up,right=view*Vector3.right;
        transform.position=ground;
        float rotationSign=Vector3.Dot(direction,right)>=0?-1f:1f;
        int frame=released?Mathf.FloorToInt((age-MammothRank3Rules.TossRelease)*24f)* (rotationSign<0?-1:1):0;
        body.sprite=frames[(frame%frames.Length+frames.Length)%frames.Length];
        body.transform.SetPositionAndRotation(ground+up*height,view);
        body.transform.localScale=new Vector3(radius*2f,radius*2f*scaleY,1);
        shadow.transform.SetPositionAndRotation(ground+up*.02f,view);
        shadow.transform.localScale=new Vector3(radius*2.3f/shadow.sprite.bounds.size.x,radius*.55f/shadow.sprite.bounds.size.y,1);
        shadow.color=new Color(0,0,0,broken>=0?Mathf.Clamp01(1f-(age-broken)/.2f)*.22f:.18f);
        if(broken<0)return;
        float t=Mathf.Max(0,age-broken);
        for(int i=0;i<chunks.Length;i++)
        {
            float a=i*2.399963f,seed=Mathf.Repeat(i*.618f+.2f,1);
            var chunk=chunks[i];chunk.enabled=t<.28f;
            chunk.transform.SetPositionAndRotation(ground+up*radius+
                right*Mathf.Cos(a)*t*(4f+seed*5f)+up*(Mathf.Sin(a)*t*5f-12f*t*t),view*Quaternion.Euler(0,0,i*39f+t*320f));
            chunk.transform.localScale=Vector3.one*radius*(.2f+seed*.25f);
            chunk.color=new Color(1,1,1,Mathf.Clamp01((.28f-t)/.1f));
        }
    }
    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}

    private static bool Polygon(Vector2 point,Vector2[] vertices)
    {
        bool inside=false;
        for(int i=0,j=vertices.Length-1;i<vertices.Length;j=i++)
            if((vertices[i].y>point.y)!=(vertices[j].y>point.y)&&
                point.x<(vertices[j].x-vertices[i].x)*(point.y-vertices[i].y)/(vertices[j].y-vertices[i].y)+vertices[i].x)inside=!inside;
        return inside;
    }
    private static void BuildFrames()
    {
        const int size=40;frames=new Sprite[16];
        var edge=new[]{new Vector2(-17,-8),new Vector2(-11,-16),new Vector2(3,-18),new Vector2(15,-11),
            new Vector2(18,1),new Vector2(13,14),new Vector2(1,18),new Vector2(-12,13),new Vector2(-18,3)};
        Color32[] palette={new Color32(46,43,42,255),new Color32(79,76,70,255),new Color32(113,107,92,255),
            new Color32(151,143,120,255),new Color32(192,185,158,255),new Color32(220,213,183,255)};
        for(int frame=0;frame<frames.Length;frame++)
        {
            var pixels=new Color32[size*size];float angle=frame*Mathf.PI*2/frames.Length,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x-19.5f,v=y-19.5f;var p=new Vector2(c*u+s*v,-s*u+c*v);
                if(!Polygon(p,edge))continue;
                bool border=!Polygon(p*1.07f,edge);
                int shade=p.y>5&&p.x<5?4:p.x+p.y>7?3:p.x< -4&&p.y> -8?3:p.y< -6?1:2;
                if(p.y>10&&p.x<0)shade=5;
                if(Mathf.Abs(p.x*.6f+p.y-4f)<.75f&&p.x> -12&&p.x<11||
                    Mathf.Abs(p.x-p.y*.3f-3f)<.65f&&p.y<5)shade=1;
                if(border)shade=0;
                pixels[y*size+x]=palette[shade];
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Mountain Boulder "+frame,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            frames[frame]=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size,0,SpriteMeshType.FullRect);
            frames[frame].hideFlags=HideFlags.HideAndDontSave;
        }
    }
}
