using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10025)]
public sealed class PixelMantisReaper:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite wingSprite,shellSprite,chipSprite;
    private static readonly Dictionary<Sprite,Bounds> visibleBounds=new Dictionary<Sprite,Bounds>();
    private SpriteRenderer reference;
    private readonly SpriteRenderer[] wings=new SpriteRenderer[4],shells=new SpriteRenderer[2],chips=new SpriteRenderer[14],cuts=new SpriteRenderer[6];
    private readonly List<SpriteRenderer> ghosts=new List<SpriteRenderer>();
    private float height,moltAge,flightAge=-1f,width,ghostClock;
    private Vector3 start,end,shedPoint,bodyCenter;
    private bool shed;

    public static PixelMantisReaper Play(SpriteRenderer reference)
    {
        if(reference==null)return null;
        if(wingSprite==null)wingSprite=BuildWing(false);
        if(shellSprite==null)shellSprite=BuildWing(true);
        if(chipSprite==null)
        {
            var t=new Texture2D(2,2){filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};
            t.SetPixels(new[]{Color.white,Color.white,Color.white,Color.clear});t.Apply(false,true);
            chipSprite=Sprite.Create(t,new Rect(0,0,2,2),Vector2.one*.5f,16f);
            chipSprite.hideFlags=HideFlags.HideAndDontSave;
        }
        var fx=new GameObject("Mantis Reaper Wings").AddComponent<PixelMantisReaper>();
        Bounds body=VisibleBody(reference.sprite);
        fx.reference=reference;fx.bodyCenter=body.center;
        fx.height=Mathf.Max(.65f,body.size.y*Mathf.Abs(reference.transform.lossyScale.y))*1.2f;
        for(int i=0;i<4;i++)fx.wings[i]=fx.Make("Folded Membrane Wing",wingSprite,-3-i);
        for(int i=0;i<2;i++)fx.shells[i]=fx.Make("Molt Carapace",shellSprite,1);
        for(int i=0;i<14;i++)fx.chips[i]=fx.Make("Molt Pixel",chipSprite,1);
        for(int i=0;i<6;i++)fx.cuts[i]=fx.Make("Reaper Crossing Blade",PixelStarProjectile.ScytheWindSprite,8);
        fx.LateUpdate();return fx;
    }
    private SpriteRenderer Make(string name,Sprite sprite,int order)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingLayerID=reference.sortingLayerID;
        r.sortingOrder=reference.sortingOrder+order;r.enabled=false;return r;
    }
    public void Molt(float age){moltAge=age;}
    public void Flight(float age,Vector3 origin,Vector3 destination,float halfWidth)
    {flightAge=age;start=origin;end=destination;width=halfWidth;}
    public void Rest(){moltAge=1f;flightAge=-1f;ghostClock=0f;foreach(var r in cuts)r.enabled=false;}

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(reference==null){Cancel();return;}
            var camera=Camera.main;Quaternion rotation=camera==null?reference.transform.rotation:camera.transform.rotation;
            Vector3 right=rotation*Vector3.right,up=rotation*Vector3.up;
            float facing=reference.flipX?-1f:1f;
            Vector3 center=bodyCenter;center.x*=facing;
            Vector3 back=reference.transform.TransformPoint(center)-right*facing*height*.12f+up*height*.08f;
            float grow=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.18f,.4f,moltAge));
            float open=flightAge<0f?0f:Mathf.SmoothStep(0f,1f,flightAge/.12f)*
                (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.5f,.68f,flightAge)));
            if(moltAge<.6f)open=Mathf.Sin(Mathf.Clamp01((moltAge-.2f)/.4f)*Mathf.PI)*.75f;
            for(int i=0;i<4;i++)
            {
                var r=wings[i];float side=i%2==0?-1f:1f,layer=i<2?1f:.78f;
                r.enabled=reference.enabled&&grow>0f;r.sortingOrder=reference.sortingOrder-3-i;
                r.transform.position=back+right*side*height*.035f;
                float angle=facing*110f+side*Mathf.Lerp(9f,48f,open)+Mathf.Sin(flightAge*65f+i)*open*4f;
                r.transform.rotation=rotation*Quaternion.Euler(0f,0f,angle);
                r.transform.localScale=new Vector3(Mathf.Lerp(.36f,1f,open),1f,1f)*height*.43f*layer*grow;
                r.color=new Color(.76f,.69f,1f,i<2?.78f:.52f);
            }
            if(moltAge>=.2f&&!shed){shed=true;shedPoint=back;}
            float peel=Mathf.Clamp01((moltAge-.2f)/.35f);
            for(int i=0;i<2;i++)
            {
                var r=shells[i];r.enabled=reference.enabled&&moltAge<.58f;
                float side=i==0?-1f:1f;r.sortingOrder=reference.sortingOrder+1;
                r.transform.position=(shed?shedPoint:back)+right*side*height*(.025f+peel*.27f)-up*height*peel*peel*.45f;
                r.transform.rotation=rotation*Quaternion.Euler(0f,0f,facing*110f+side*(8f+peel*45f));
                r.transform.localScale=Vector3.one*height*.25f;
                r.color=new Color(.48f,.57f,.32f,1f-peel);
            }
            for(int i=0;i<chips.Length;i++)
            {
                var r=chips[i];r.enabled=shed&&moltAge>=.24f&&moltAge<.6f;
                float t=Mathf.Max(0f,moltAge-.24f),seed=((i*7)%13)/13f,side=i%2==0?-1f:1f;
                r.transform.position=shedPoint+right*side*height*(.08f+t*(.3f+seed))+
                    up*height*(seed*.25f+t*.7f-t*t*6f);
                r.transform.rotation=rotation;r.transform.localScale=Vector3.one*(.35f+seed*.5f);
                r.color=new Color(.66f,.72f,.43f,1f-t/.36f);r.sortingOrder=reference.sortingOrder+2;
            }
            for(int i=0;i<cuts.Length;i++)
            {
                int strike=i/3,trail=i%3;var r=cuts[i];
                float age=flightAge-.12f-strike*.1f-trail*.025f,progress=age/.3f;
                r.enabled=reference.enabled&&flightAge>=0f&&progress>=0f&&progress<=1f;
                Vector3 direction=end-start;
                float angle=Mathf.Atan2(Vector3.Dot(direction,up),Vector3.Dot(direction,right))*Mathf.Rad2Deg;
                r.transform.position=Vector3.Lerp(start,end,Mathf.Clamp01(progress))+Vector3.up*height*.45f;
                r.transform.rotation=rotation*Quaternion.Euler(0f,0f,angle+(strike==0?32f:-32f));
                r.transform.localScale=Vector3.one*(width*2.6f/PixelStarProjectile.ScytheWindSprite.bounds.size.y)*(1f-trail*.12f);
                r.flipY=strike==1;r.color=new Color(.86f,.76f,1f,trail==0?1f:.2f/trail);
                r.sortingOrder=reference.sortingOrder+8-trail;
            }
            if(open>.8f&&flightAge-ghostClock>=.065f)
            {
                ghostClock=flightAge;ghosts.RemoveAll(x=>x==null);
                var ghost=Rank3CombatFx.Ghost("Reaper Flight Afterimage",reference,reference.transform.position,
                    new Color(.7f,.58f,1f,.23f),1f,.14f);
                if(ghost!=null){ghost.sortingOrder=reference.sortingOrder-8;ghosts.Add(ghost);}
            }
        }
    }
    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        foreach(var r in ghosts)if(r!=null){if(Application.isPlaying)Destroy(r.gameObject);else DestroyImmediate(r.gameObject);}
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    internal static System.Collections.IEnumerator WarmupVisibleBodies(IEnumerable<Sprite> frames)
    {
        long slice=System.Diagnostics.Stopwatch.GetTimestamp();
        foreach(Sprite frame in frames)
        {
            if(frame==null||visibleBounds.ContainsKey(frame))continue;
            VisibleBody(frame);
            if((System.Diagnostics.Stopwatch.GetTimestamp()-slice)*1000d/
               System.Diagnostics.Stopwatch.Frequency<2d)continue;
            yield return null;
            slice=System.Diagnostics.Stopwatch.GetTimestamp();
        }
    }

    internal static Bounds VisibleBody(Sprite sprite)
    {
        Bounds result;if(visibleBounds.TryGetValue(sprite,out result))return result;
        const int size=64;
        var rt=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        rt.filterMode=FilterMode.Point;var previous=RenderTexture.active;Texture2D readable=null;
        try
        {
            Rect frame=sprite.rect;Texture2D texture=sprite.texture;
            Graphics.Blit(texture,rt,new Vector2(frame.width/texture.width,frame.height/texture.height),
                new Vector2(frame.x/texture.width,frame.y/texture.height));
            RenderTexture.active=rt;readable=new Texture2D(size,size,TextureFormat.RGBA32,false);
            readable.ReadPixels(new Rect(0,0,size,size),0,0,false);readable.Apply(false,false);
            Color32[] pixels=readable.GetPixels32();int minX=size,minY=size,maxX=-1,maxY=-1;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(pixels[y*size+x].a>32)
            {minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
            result=sprite.bounds;
            if(maxY>=minY)
            {
                Vector2 center=new Vector2((minX+maxX+1)*.5f/size*frame.width,(minY+maxY+1)*.5f/size*frame.height);
                result=new Bounds((center-sprite.pivot)/sprite.pixelsPerUnit,
                    new Vector3((maxX-minX+1)/(float)size*frame.width,(maxY-minY+1)/(float)size*frame.height,0f)/sprite.pixelsPerUnit);
            }
            visibleBounds[sprite]=result;return result;
        }
        finally
        {
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
            if(readable!=null){if(Application.isPlaying)Destroy(readable);else DestroyImmediate(readable);}
        }
    }
    internal static void ReleaseVisibleBodies(IEnumerable<Sprite> frames)
    {
        foreach(Sprite frame in frames)visibleBounds.Remove(frame);
    }
    private static Sprite BuildWing(bool shell)
    {
        const int w=24,h=40;var pixels=new Color32[w*h];
        for(int y=1;y<h-1;y++)
        {
            float u=y/(float)(h-1);int mid=12+Mathf.RoundToInt(Mathf.Sin(u*3f)*3f);
            int half=Mathf.Max(1,Mathf.RoundToInt(Mathf.Pow(Mathf.Sin(u*Mathf.PI),.65f)*(shell?7f:6f)));
            for(int x=Mathf.Max(0,mid-half);x<=Mathf.Min(w-1,mid+half);x++)
            {
                bool rim=Mathf.Abs(x-mid)>=half-1,vein=x==mid||(y+(x-mid)*2)%11==0;
                pixels[y*w+x]=shell?(rim?new Color32(68,83,42,255):new Color32(164,179,116,245)):
                    rim?new Color32(217,232,205,180):vein?new Color32(225,247,217,185):new Color32(194,213,193,85);
            }
        }
        var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,0f),16f);
        sprite.hideFlags=HideFlags.HideAndDontSave;return sprite;
    }
}
