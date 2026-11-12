using System;
using UnityEngine;

// Short-lived sprites only: the substitute never becomes a combat unit or a collider.
public sealed class PixelNinjaSubstitution:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite logSprite,smokeSprite,cutSprite,pixelSprite,contactSprite,logLeft,logRight;
    private SpriteRenderer main,flash;
    private SpriteRenderer[] pieces,accents;
    private Vector3[] origins,velocities;
    private Func<bool> valid;
    private Color tint;
    private float elapsed,size;
    private int mode;

    public static void PlayVanish(SpriteRenderer source)
    {
        if(source==null)return;
        var fx=Create("Ninja Vanish Puff",source,3,Color.white);
        fx.CreatePieces(8,true);fx.Animate(0f);
    }

    public static PixelNinjaSubstitution PlayLog(SpriteRenderer source,Func<bool> valid)
    {
        if(source==null)return null;
        var fx=Create("Ninja Substitution Log",source,0,new Color(.65f,.61f,.55f));
        fx.valid=valid;
        fx.main=fx.Child("Rope-bound Log",LogSprite,Color.white,2);
        fx.main.transform.localPosition=Vector3.up*fx.size*.36f;
        fx.main.transform.localScale=Vector3.one*fx.size*.78f;
        fx.CreatePieces(10,true);fx.CreatePopAccents();
        fx.Animate(0f);return fx;
    }

    public static void PlayArrival(SpriteRenderer source,Color color)
    {
        if(source==null)return;
        var fx=Create("Ninja Shadow Arrival",source,1,color);
        fx.CreatePieces(7,true);fx.CreatePopAccents();fx.Animate(0f);
    }

    public static PixelNinjaSubstitution PlayBrokenLog(SpriteRenderer source,Func<bool> valid)
    {
        if(source==null||LogSprite==null)return null;
        if(logLeft==null)
        {
            Rect rect=LogSprite.rect;float half=rect.width*.5f;
            logLeft=Sprite.Create(LogSprite.texture,new Rect(rect.x,rect.y,half,rect.height),
                Vector2.one*.5f,LogSprite.pixelsPerUnit);
            logRight=Sprite.Create(LogSprite.texture,new Rect(rect.x+half,rect.y,half,rect.height),
                Vector2.one*.5f,LogSprite.pixelsPerUnit);
            logLeft.hideFlags=logRight.hideFlags=HideFlags.HideAndDontSave;
        }
        var fx=Create("Ninja Broken Substitute",source,4,new Color(.72f,.49f,.28f));
        fx.valid=valid;
        fx.main=fx.Child("Log Left",logLeft,Color.white,2);
        fx.flash=fx.Child("Log Right",logRight,Color.white,2);
        fx.CreatePieces(8,false);fx.Animate(0f);return fx;
    }

    public static void PlayFlameAfterimage(Vector3 position,SpriteRenderer source)
    {
        if(source==null||source.sprite==null)return;
        var ghost=PixelCombatEffect.PlayWolfHuntAfterimage(source,position,position,
            new Color(.8f,.17f,.04f,.72f),.32f,"Ninja Ember Silhouette");
        if(ghost!=null)PixelCombatEffect.PlayWolfAmbushScatter(ghost,Vector3.up*.2f,new Color(1f,.36f,.06f));
    }

    public static PixelNinjaSubstitution PlayCut(SpriteRenderer victim,Vector3 direction,Color color,bool reverse,bool finisher=false)
    {
        if(victim==null)return null;
        var fx=Create("Ninja Crescent Cut",victim,2,color);
        if(finisher){fx.size*=1.45f;fx.name="Ninja Cross Finisher";}
        fx.transform.position=victim.bounds.center;
        float side=Camera.main==null?direction.x:Vector3.Dot(direction,Camera.main.transform.right);
        fx.main=fx.Child("Pixel Blade",CutSprite,color,12);
        fx.main.flipX=side<0f;
        fx.main.transform.localRotation=Quaternion.Euler(0,0,reverse?-28f:32f);
        fx.flash=fx.Child("Contact",ContactSprite,Color.white,14);
        fx.CreatePieces(12,false);fx.Animate(0f);return fx;
    }

    private static PixelNinjaSubstitution Create(string name,SpriteRenderer source,int mode,Color color)
    {
        var go=new GameObject(name);
        go.transform.position=source.transform.position;
        go.transform.rotation=Camera.main==null?source.transform.rotation:Camera.main.transform.rotation;
        var fx=go.AddComponent<PixelNinjaSubstitution>();
        // Character frames include generous transparent margins; the prop should fit the visible body.
        fx.mode=mode;fx.tint=color;
        fx.size=Mathf.Clamp(source.bounds.size.y*(mode==2?.65f:.48f),.35f,1.8f);
        // A fixed world position retains its depth after the caster teleports elsewhere.
        fx.sortingLayer=source.sortingLayerID;fx.sortingOrder=source.sortingOrder;
        return fx;
    }

    private int sortingLayer,sortingOrder;
    private SpriteRenderer Child(string name,Sprite sprite,Color color,int order)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
        renderer.color=color;renderer.sortingLayerID=sortingLayer;
        renderer.sortingOrder=sortingOrder+order;return renderer;
    }

    private void CreatePieces(int count,bool smoke)
    {
        pieces=new SpriteRenderer[count];origins=new Vector3[count];velocities=new Vector3[count];
        for(int i=0;i<count;i++)
        {
            float angle=(i*137.5f+21f)*Mathf.Deg2Rad;
            Vector3 ray=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
            origins[i]=smoke?new Vector3(ray.x*.22f,.38f+ray.y*.24f,0)*size:Vector3.zero;
            velocities[i]=ray*size*(mode==2?2.3f:1.45f)+Vector3.up*(smoke?.25f:.04f);
            pieces[i]=Child(smoke?"Pixel Smoke":"Pixel Spark",smoke?SmokeSprite:PixelSprite,
                smoke?Color.white:tint,mode==2?13:mode==3?4:mode==1?-6:0);
            pieces[i].transform.localPosition=origins[i];
        }
    }

    private void CreatePopAccents()
    {
        accents=new SpriteRenderer[5];
        for(int i=0;i<accents.Length;i++)
        {
            float angle=20f+i*35f;
            accents[i]=Child("Comic Pop Accent",ContactSprite,mode==0?new Color(1f,.94f,.77f):tint,5);
            accents[i].transform.localRotation=Quaternion.Euler(0,0,angle-90f);
        }
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
        float life=mode==0?1.05f:mode==2?.20f:mode==4?.40f:.14f;
        if(elapsed>=life||(valid!=null&&!valid()))
        {
            gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
            return;
        }
        if(mode==0)
        {
            float fall=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.25f,.48f,elapsed));
            float hop=elapsed<.32f?Mathf.Sin(elapsed/.32f*Mathf.PI)*.14f*size:0f;
            float pop=Mathf.Clamp01(elapsed/.05f),settle=Mathf.InverseLerp(.05f,.13f,elapsed);
            float stretchX=Mathf.Lerp(Mathf.Lerp(1.18f,.92f,pop),1f,settle);
            float stretchY=Mathf.Lerp(Mathf.Lerp(.28f,1.17f,pop),1f,settle);
            main.transform.localScale=new Vector3(stretchX,stretchY,1)*size*.78f;
            main.transform.localPosition=new Vector3(fall*size*.15f,size*(.36f*stretchY-.14f*fall)+hop,0);
            main.transform.localRotation=Quaternion.Euler(0,0,-67f*fall);
            main.color=new Color(1,1,1,1f-Mathf.InverseLerp(.78f,1.05f,elapsed));
        }
        if(mode==4)
        {
            float split=Mathf.Max(0f,elapsed-.055f);
            float width=LogSprite.bounds.size.x*size*.78f*.25f;
            for(int side=0;side<2;side++)
            {
                var half=side==0?main:flash;float sign=side==0?-1f:1f;
                half.transform.localScale=Vector3.one*size*.78f;
                half.transform.localPosition=new Vector3(sign*(width+split*size*.9f),
                    size*(.36f+split*.7f-split*split*5f),0);
                half.transform.localRotation=Quaternion.Euler(0,0,-sign*split*180f);
                half.color=new Color(1,1,1,1f-Mathf.InverseLerp(.21f,.40f,elapsed));
            }
        }
        if(accents!=null)
        {
            float pop=Mathf.Clamp01(elapsed/.14f);
            for(int i=0;i<accents.Length;i++)
            {
                float angle=(20f+i*35f)*Mathf.Deg2Rad;
                accents[i].transform.localPosition=(Vector3.up*.42f+
                    new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*(.5f+.18f*pop))*size;
                accents[i].transform.localScale=new Vector3(.055f,.20f,1)*size*(1f-pop*.4f);
                Color color=accents[i].color;color.a=1f-Mathf.InverseLerp(.07f,.14f,elapsed);
                accents[i].color=color;
            }
        }
        if(mode==2)
        {
            float reveal=Mathf.Clamp01(elapsed/.045f);
            main.transform.localScale=new Vector3(size*(1.1f+.22f*reveal),size*(.65f+.2f*reveal),1);
            Color color=Color.Lerp(Color.white,tint,Mathf.Clamp01(elapsed/.045f));
            color.a=1f-Mathf.InverseLerp(.06f,.20f,elapsed);main.color=color;
            flash.transform.localScale=new Vector3(size*.10f,size*.23f,1)*(1f+reveal);
            flash.color=new Color(1,1,1,1f-Mathf.Clamp01(elapsed/.065f));
        }
        for(int i=0;i<pieces.Length;i++)
        {
            float pieceLife=mode==0?.26f:life;
            float t=Mathf.Clamp01(elapsed/pieceLife);
            float travel=mode==4?Mathf.Max(0f,elapsed-.055f):elapsed;
            Vector3 p=origins[i]+velocities[i]*travel;
            if(mode==4)p+=Vector3.up*size*(.36f-travel*travel*4f);
            // Quantized movement keeps the smoke and sparks on a visible pixel grid.
            p.x=Mathf.Round(p.x*64f)/64f;p.y=Mathf.Round(p.y*64f)/64f;
            pieces[i].transform.localPosition=p;
            float scale=mode==2||mode==4?size*.035f*(1f-t*.55f):
                size*.5f*((mode==1?.26f:.34f)+.14f*Mathf.Sin(t*Mathf.PI))*(1f+(i%3)*.12f);
            pieces[i].transform.localScale=mode==2||mode==4?new Vector3(scale*2f,scale,1):Vector3.one*scale;
            Color color=pieces[i].color;
            color.a=mode==4&&elapsed<.055f?0f:mode==2||mode==4?1f-t:1f-Mathf.InverseLerp(.48f,1f,t);pieces[i].color=color;
        }
    }

    internal static Sprite MakeSprite(string name,int width,int height,float ppu,Func<int,int,Color> pixel)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {name=name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        var colors=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)colors[y*width+x]=pixel(x,y);
        texture.SetPixels(colors);
        // Tight mesh generation needs the pixels before their CPU copy is discarded.
        var sprite=Sprite.Create(texture,new Rect(0,0,width,height),Vector2.one*.5f,ppu);
        texture.Apply(false,true);
        sprite.name=name;sprite.hideFlags=HideFlags.HideAndDontSave;return sprite;
    }

    private static Sprite PixelSprite=>pixelSprite!=null?pixelSprite:
        pixelSprite=MakeSprite("Ninja Square Pixel",1,1,1f,(x,y)=>Color.white);

    private static Sprite SmokeSprite
    {
        get
        {
            if(smokeSprite!=null)return smokeSprite;
            string[] rows={"....oooo....","..ooWWWWoo..",".oWWWWWWWWo.",".oWWWWWWWWWo",
                "oWWWWWWWWWWo","oWWWWWWWWWWo",".oWWWWWWWWWo",".oWWWWWWWWSo",
                "oWWWWWWWWSSo","oWWWWWWWSSSo",".ooSSSSSSoo.","...oooooo..."};
            return smokeSprite=MakeSprite("Ninja Comic Cloud",12,12,12f,(x,y)=>
            {
                char c=rows[11-y][x];
                return c=='o'?new Color(.35f,.22f,.36f):c=='W'?new Color(1f,.97f,.89f):
                    c=='S'?new Color(.82f,.74f,.71f):Color.clear;
            });
        }
    }

    private static Sprite CutSprite=>cutSprite!=null?cutSprite:
        cutSprite=MakeSprite("Ninja Tapered Crescent",64,40,48f,(x,y)=>
        {
            float t=(x-4f)/55f;
            if(t<=0f||t>=1f)return Color.clear;
            float curve=Mathf.Sin(t*Mathf.PI),center=10f+curve*16f;
            float distance=Mathf.Abs(y-center),width=curve*2.1f;
            if(distance>width)return Color.clear;
            return new Color(1,1,1,distance<width*.65f?1f:.65f);
        });

    private static Sprite ContactSprite=>contactSprite!=null?contactSprite:
        contactSprite=MakeSprite("Ninja Contact Glint",8,12,8f,(x,y)=>
            Mathf.Abs(x-3.5f)/3.5f+Mathf.Abs(y-5.5f)/5.5f<=1f?Color.white:Color.clear);

    private static Sprite LogSprite=>logSprite!=null?logSprite:
        logSprite=Resources.Load<Sprite>("CombatEffects/NinjaSubstitution/Log");
}
