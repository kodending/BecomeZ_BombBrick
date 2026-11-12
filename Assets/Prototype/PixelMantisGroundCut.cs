using UnityEngine;

// The combat timeline drives flight and contact, so freezing the caster freezes the blade too.
public sealed class PixelMantisGroundCut : MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite bladeSprite,scarSprite,chipSprite;
    private SpriteRenderer blade,scar;
    private readonly SpriteRenderer[] chips=new SpriteRenderer[24];
    private Vector3 sky,ground,direction,side;
    private Quaternion groundRotation;
    private Color tint;
    private float length,width,age;
    private bool released,contacted;

    public static PixelMantisGroundCut Play(Vector3 sky,Vector3 ground,Vector3 direction,
        float length,float width,Color color,SpriteRenderer sorting)
    {
        if(bladeSprite==null)bladeSprite=BuildSprite(false);
        if(scarSprite==null)scarSprite=BuildSprite(true);
        if(chipSprite==null)
        {
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};
            t.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});t.Apply(false,true);
            chipSprite=Sprite.Create(t,new Rect(0,0,2,2),Vector2.one*.5f,16f);
            chipSprite.hideFlags=HideFlags.HideAndDontSave;
        }
        var fx=new GameObject("Mantis Descending Ground Cut").AddComponent<PixelMantisGroundCut>();
        fx.sky=sky;fx.ground=ground+Vector3.up*.065f;fx.direction=direction.normalized;
        fx.side=Vector3.Cross(direction,Vector3.up).normalized;
        fx.groundRotation=Quaternion.LookRotation(Vector3.up,fx.side);
        fx.length=length;fx.width=width;fx.tint=color;
        int layer=sorting==null?0:sorting.sortingLayerID;
        fx.blade=fx.Renderer("Descending Scythe",bladeSprite,layer,(sorting==null?20:sorting.sortingOrder+30));
        fx.scar=fx.Renderer("Carved Ground",scarSprite,layer,2);
        fx.scar.transform.SetPositionAndRotation(fx.ground,fx.groundRotation);
        fx.scar.transform.localScale=new Vector3(length/scarSprite.bounds.size.x,width/scarSprite.bounds.size.y,1f);
        fx.scar.enabled=false;fx.Advance(0f);return fx;
    }

    private SpriteRenderer Renderer(string name,Sprite sprite,int layer,int order)
    {
        var child=new GameObject(name);child.transform.SetParent(transform,false);
        var r=child.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingLayerID=layer;r.sortingOrder=order;
        return r;
    }

    public void Advance(float time)
    {
        if(this==null)return;
        age=Mathf.Max(0f,time);
        float progress=Mathf.Clamp01(age/MantisRank3Rules.GuillotineFlight);
        float dive=progress*progress;
        blade.enabled=progress<1f;
        blade.transform.position=Vector3.Lerp(sky,ground,dive);
        blade.transform.rotation=Quaternion.AngleAxis(55f*(1f-dive),direction)*groundRotation;
        float grow=Mathf.Lerp(.45f,1f,progress);
        blade.transform.localScale=new Vector3(length/bladeSprite.bounds.size.x*grow,
            width/bladeSprite.bounds.size.y*(1.25f-.25f*progress),1f);
        blade.color=Color.Lerp(tint,Color.white,.4f);
        if(progress<1f)return;
        if(!contacted)
        {
            contacted=true;scar.enabled=true;
            for(int i=0;i<chips.Length;i++)
                chips[i]=Renderer("Cut Debris",chipSprite,scar.sortingLayerID,5);
        }
        float t=age-MantisRank3Rules.GuillotineFlight;
        blade.enabled=t<.065f;
        blade.transform.SetPositionAndRotation(ground+Vector3.up*.008f,groundRotation);
        blade.color=Color.Lerp(tint,Color.white,.8f)*new Color(1f,1f,1f,1f-Mathf.Clamp01(t/.065f));
        float fade=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.22f,MantisRank3Rules.GuillotineScarDuration,t));
        scar.color=Color.Lerp(tint,Color.white,Mathf.Max(0f,1f-t/.09f)*.75f)*new Color(1f,1f,1f,fade);
        for(int i=0;i<chips.Length;i++)
        {
            float seed=((i*17)%23)/23f;
            float sign=i%2==0?1f:-1f;
            float travel=Mathf.Min(t,.4f);
            var r=chips[i];r.enabled=t<.42f;
            r.transform.position=ground+direction*((seed-.5f)*length*.94f)+
                side*sign*(width*.25f+travel*(1.5f+seed*2f))+
                Vector3.up*Mathf.Max(.025f,travel*(3f+seed*2f)-travel*travel*11f);
            r.transform.rotation=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
            r.transform.localScale=Vector3.one*(.45f+seed*.8f);
            r.color=MeadowAmbientParticles.TerrainColor(Color.Lerp(new Color(.15f,.18f,.09f),tint,i%3==0?.9f:.2f))*
                new Color(1f,1f,1f,1f-Mathf.Clamp01(t/.42f));
        }
        if(t>=MantisRank3Rules.GuillotineScarDuration)Cancel();
    }

    public void Release(){released=true;}
    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(released)Advance(age+(skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }
    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}

    private static Sprite BuildSprite(bool ground)
    {
        const int w=128,h=24;
        var texture=new Texture2D(w,h,TextureFormat.RGBA32,false)
        {name=ground?"Mantis Pixel Ground Incision":"Mantis Falling Blade",filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color32[w*h];
        for(int x=1;x<w-1;x++)
        {
            float u=x/(float)(w-1),taper=Mathf.Pow(Mathf.Sin(u*Mathf.PI),.45f);
            int half=Mathf.Max(1,Mathf.RoundToInt((ground?9f:10f)*taper));
            int mid=12+(ground?(x/7%3)-1:Mathf.RoundToInt(Mathf.Sin(u*Mathf.PI)*2f)-2);
            for(int y=Mathf.Max(0,mid-half);y<=Mathf.Min(h-1,mid+half);y++)
            {
                int depth=Mathf.Abs(y-mid),rim=half-depth;
                if(rim==0&&x%7<2)continue;
                Color32 color;
                if(ground)
                    color=depth<=Mathf.Max(1,half/3)?new Color32(16,28,18,235):
                        rim<2?new Color32(215,255,173,240):
                        rim<4?new Color32(104,172,80,220):new Color32(42,69,31,225);
                else
                    color=depth<2?new Color32(255,255,247,255):
                        depth<half*.5f?new Color32(234,255,205,255):
                        y>mid?new Color32(138,205,108,235):new Color32(90,157,103,130);
                pixels[y*w+x]=color;
            }
        }
        texture.SetPixels32(pixels);texture.Apply(false,true);
        var sprite=Sprite.Create(texture,new Rect(0,0,w,h),Vector2.one*.5f,16f);
        sprite.name=texture.name;sprite.hideFlags=HideFlags.HideAndDontSave;return sprite;
    }
}
