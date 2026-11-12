using System;
using UnityEngine;

// One clock owns the visible contact frames and their combat callbacks.
public sealed class PixelMonkeyMortar:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private readonly SpriteRenderer[,] fruit=new SpriteRenderer[5,3];
    private readonly bool[] launched=new bool[5],landed=new bool[5],falling=new bool[5];
    private readonly Vector3[] origins=new Vector3[5];
    private SpriteRenderer shadow;
    private static Sprite shadowSprite;
    private Func<bool> valid;
    private Func<Vector3> launchPosition;
    private Action<int> onLaunch,onImpact,onFall;
    private Vector3 origin,center;
    private Quaternion billboard;
    private Color color;
    private float age,height,radius,finalRadius;
    private bool cancelled;
    private CombatProjectileSource.Flight flight;
    private float previousClock;
    public bool Complete { get; private set; }

    public static float ImpactTime(int index)=>index<4?1.02f+index*.14f:1.76f;
    public static float FallDuration(int index)=>index==4?.2f:.18f;

    public static PixelMonkeyMortar Play(Vector3 origin,Vector3 center,SpriteRenderer sorting,
        Color color,float radius,float finalRadius,Func<bool> valid,Action<int> launch,Action<int> impact,
        Func<Vector3> launchPosition=null,Action<int> fall=null)
    {
        var fx=new GameObject("Monkey Mortar Barrage").AddComponent<PixelMonkeyMortar>();
        fx.origin=origin;fx.center=center;fx.color=color;fx.radius=radius;fx.finalRadius=finalRadius;
        fx.valid=valid;fx.onLaunch=launch;fx.onImpact=impact;
        fx.launchPosition=launchPosition;
        fx.onFall=fall;
        fx.flight=CombatProjectileSource.Capture(sorting==null?null:sorting.transform);
        fx.previousClock=fx.flight.clock==null?0:fx.flight.clock();
        var camera=Camera.main;
        fx.billboard=camera==null?Quaternion.identity:camera.transform.rotation;
        fx.height=Mathf.Max(AboveScreen(origin,camera),AboveScreen(center,camera));
        int layer=sorting==null?0:sorting.sortingLayerID;
        int order=(sorting==null?0:sorting.sortingOrder)+30;
        for(int i=0;i<5;i++)for(int j=0;j<3;j++)
            fx.fruit[i,j]=fx.Sprite("Mortar Fruit "+i+" Trail "+j,
                PixelStarProjectile.MonkeyFruitSprite,layer,order-j);
        fx.shadow=fx.Sprite("Mortar Landing Shadow",ShadowSprite(),layer,1);
        fx.shadow.transform.position=center+Vector3.up*.035f;
        fx.shadow.transform.rotation=Quaternion.Euler(90f,0f,0f);
        return fx;
    }

    private static float AboveScreen(Vector3 point,Camera camera)
    {
        if(camera==null)return 12f;
        for(float h=6f;h<=40f;h+=2f)
        {
            var viewport=camera.WorldToViewportPoint(point+Vector3.up*h);
            if(viewport.y>1.2f||viewport.z<=camera.nearClipPlane)return h;
        }
        return 40f;
    }

    private SpriteRenderer Sprite(string name,Sprite sprite,int layer,int order)
    {
        var renderer=new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(transform,false);renderer.sprite=sprite;
        renderer.transform.rotation=billboard;renderer.sortingLayerID=layer;
        renderer.sortingOrder=order;renderer.enabled=false;return renderer;
    }

    internal static Sprite ShadowSprite()
    {
        if(shadowSprite!=null)return shadowSprite;
        const int size=32;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {name="Pixel Mortar Shadow",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float distance=new Vector2(x+.5f-size*.5f,y+.5f-size*.5f).magnitude/(size*.5f);
            byte alpha=distance<.65f?(byte)255:distance<.83f?(byte)200:distance<.97f?(byte)100:(byte)0;
            pixels[y*size+x]=new Color32(255,255,255,alpha);
        }
        texture.SetPixels32(pixels);texture.Apply(false,true);
        shadowSprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
        return shadowSprite;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(flight.valid!=null&&!flight.valid()){Cancel();return;}
            float dt=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(flight.clock!=null){float now=flight.clock();dt=Mathf.Max(0,now-previousClock);previousClock=now;}
            Advance(dt);
        }
    }

    private void Advance(float deltaTime)
    {
        while(deltaTime>0&&!cancelled&&!Complete)
        {
            float step=Mathf.Min(deltaTime,1f/60f);deltaTime-=step;AdvanceStep(step);
        }
    }

    private void AdvanceStep(float deltaTime)
    {
        if(cancelled||Complete)return;
        if(valid!=null&&!valid()){Cancel();return;}
        float previous=age;age+=deltaTime;
        for(int i=0;i<5;i++)
        {
            if(!launched[i]&&age>=i*.09f)
            {launched[i]=true;origins[i]=launchPosition==null?origin:launchPosition();onLaunch?.Invoke(i);}
            DrawFruit(i);
            float launch=i*.09f,fall=ImpactTime(i)-FallDuration(i);
            if(!landed[i]&&launched[i]&&flight.deflect!=null&&
                ((age>=launch&&previous<launch+.28f)||(age>=fall&&previous<ImpactTime(i))))
            {
                bool falling=age>=fall;
                Vector3 from=FlightPoint(i,Mathf.Max(previous,falling?fall:launch),falling);
                Vector3 to=FlightPoint(i,age,falling);
                if(flight.deflect(from,to,fruit[i,0]))
                {
                    landed[i]=true;
                    for(int j=0;j<3;j++)fruit[i,j].enabled=false;
                }
            }
            if(!landed[i]&&!falling[i]&&age>=fall)
            {falling[i]=true;onFall?.Invoke(i);}
            if(!landed[i]&&age>=ImpactTime(i))
            {
                landed[i]=true;
                for(int j=0;j<3;j++)fruit[i,j].enabled=false;
                onImpact?.Invoke(i);
            }
        }
        DrawShadow();
        if(landed[0]&&landed[1]&&landed[2]&&landed[3]&&landed[4]){Complete=true;onLaunch=null;onImpact=null;onFall=null;}
    }

    private Vector3 FlightPoint(int index,float time,bool falling)
    {
        float t=Mathf.Clamp01(falling?(time-ImpactTime(index)+FallDuration(index))/FallDuration(index):
            (time-index*.09f)/.28f);
        return falling?center+Vector3.up*((index==4?1.05f:.64f)*MonkeyCombatRules.SkillProjectileVisualMultiplier*.48f+height*(1f-Mathf.Pow(t,1.35f))):
            origins[index]+Vector3.up*(height*t*t);
    }

    private void DrawFruit(int index)
    {
        float launchAge=age-index*.09f;
        float fallAge=age-(ImpactTime(index)-FallDuration(index));
        bool rising=launchAge>=0f&&launchAge<.28f;
        bool falling=fallAge>=0f&&age<ImpactTime(index);
        float size=(index==4?1.05f:.64f)*MonkeyCombatRules.SkillProjectileVisualMultiplier;
        for(int j=0;j<3;j++)
        {
            var renderer=fruit[index,j];renderer.enabled=!landed[index]&&(rising||falling);
            if(!renderer.enabled)continue;
            float t=Mathf.Clamp01(rising?(launchAge-j*.012f)/.28f:
                (fallAge-j*.008f)/FallDuration(index));
            Vector3 position=rising?origins[index]+Vector3.up*(height*t*t):
                center+Vector3.up*(size*.48f+height*(1f-Mathf.Pow(t,1.35f)));
            renderer.transform.position=position;
            renderer.transform.rotation=billboard*Quaternion.Euler(0f,0f,rising?t*240f:index*37f);
            renderer.transform.localScale=new Vector3(1f,falling?1.16f:1f,1f)*size*(1f-j*.18f);
            Color tint=color;tint.a=j==0?1f:.24f/j;renderer.color=tint;
        }
    }

    private void DrawShadow()
    {
        int next=0;while(next<5&&landed[next])next++;
        shadow.enabled=next<5&&age>=ImpactTime(next)-.42f;
        if(!shadow.enabled)return;
        float t=Mathf.Clamp01((age-ImpactTime(next)+.42f)/.42f);
        float growth=Mathf.SmoothStep(0f,1f,t);
        shadow.transform.localScale=Vector3.one*2f*(next==4?finalRadius:radius)*Mathf.Lerp(.16f,1f,growth);
        shadow.color=new Color(.045f,.027f,.06f,Mathf.Lerp(.06f,.48f,growth));
    }

    public void Cancel()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(this==null)return;
            if(CombatEffectEndFade.IsFading(this))return;
            if(cancelled)return;
            cancelled=true;flight.finished?.Invoke();onLaunch=null;onImpact=null;onFall=null;
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
        }
    }
    private void OnDestroy()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            flight.finished?.Invoke();
        }
    }
}
