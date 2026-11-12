using System;
using UnityEngine;

public sealed class PixelWolfAlphaMoon:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite moonSprite;
    private SpriteRenderer target,moon;
    private Func<bool> valid;
    private float height;

    public static PixelWolfAlphaMoon Create(SpriteRenderer target,Func<bool> valid)
    {
        if(moonSprite==null)
        {
            var texture=new Texture2D(16,16,TextureFormat.RGBA32,false)
                {name="Alpha Full Moon",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[256];
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)
            {
                float distance=new Vector2(x-7.5f,y-7.5f).magnitude;
                if(distance>6.5f)continue;
                bool crater=(x-5)*(x-5)+(y-10)*(y-10)<5||(x-10)*(x-10)+(y-5)*(y-5)<3;
                pixels[y*16+x]=distance>5.5f||crater?new Color(.45f,.6f,.86f):new Color(.87f,.94f,1f);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            moonSprite=Sprite.Create(texture,new Rect(0,0,16,16),Vector2.one*.5f,32f);
        }
        var go=new GameObject("Alpha Moon Mark");
        var effect=go.AddComponent<PixelWolfAlphaMoon>();
        effect.target=target;effect.valid=valid;
        effect.height=target.bounds.size.y;
        effect.moon=go.AddComponent<SpriteRenderer>();effect.moon.sprite=moonSprite;
        effect.moon.sortingLayerID=target.sortingLayerID;
        effect.LateUpdate();return effect;
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(target==null||(valid!=null&&!valid()))
            {
                gameObject.SetActive(false);
                if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
                return;
            }
            transform.position=target.transform.position+Vector3.up*(height+.25f);
            if(Camera.main!=null)transform.rotation=Camera.main.transform.rotation;
            moon.sortingOrder=target.sortingOrder+60;
        }
    }
}
