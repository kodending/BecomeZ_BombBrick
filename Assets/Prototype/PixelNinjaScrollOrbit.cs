using System;
using UnityEngine;

// The three fixed slots are the charge display; spending a charge only hides its scroll.
public sealed class PixelNinjaScrollOrbit:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite scrollSprite,pixelSprite;
    private readonly SpriteRenderer[] scrolls=new SpriteRenderer[3];
    private SpriteRenderer source;
    private Func<bool> valid;
    private SpriteRenderer[] fragments;
    private Vector3[] fragmentOrigins;
    private float phase,fade;
    private bool stopping;
    private int charges=3;

    public static PixelNinjaScrollOrbit Play(SpriteRenderer source,Func<bool> valid)
    {
        if(source==null)return null;
        var fx=new GameObject("Ninja Three Scrolls").AddComponent<PixelNinjaScrollOrbit>();
        fx.source=source;fx.valid=valid;
        for(int i=0;i<3;i++)fx.scrolls[i]=fx.Child("Ninjutsu Scroll "+(i+1),ScrollSprite);
        fx.LateUpdate();return fx;
    }

    public void SetCharges(int count)
    {
        if(this==null||stopping)return;
        charges=Mathf.Clamp(count,0,3);
        for(int i=0;i<3;i++)scrolls[i].enabled=i<charges;
    }

    public void Stop(bool scatter)
    {
        if(this==null||stopping)return;
        if(CombatEffectEndFade.IsFading(this))return;
        if(!scatter||charges==0){Remove();return;}
        stopping=true;
        fragments=new SpriteRenderer[charges*4];fragmentOrigins=new Vector3[fragments.Length];
        for(int i=0;i<fragments.Length;i++)
        {
            var scroll=scrolls[i/4];
            fragments[i]=Child("Scroll Pixel",PixelSprite);
            fragments[i].sortingOrder=scroll.sortingOrder;
            fragments[i].transform.position=scroll.transform.position;
            fragmentOrigins[i]=scroll.transform.position;
            fragments[i].transform.localScale=Vector3.one*.035f;
            fragments[i].color=i%2==0?new Color(.83f,.73f,.95f):new Color(.55f,.38f,.73f);
        }
    }

    private SpriteRenderer Child(string label,Sprite sprite)
    {
        var go=new GameObject(label);go.transform.SetParent(transform,false);
        var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
        renderer.sortingLayerID=source.sortingLayerID;return renderer;
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(source==null||valid!=null&&!valid()){Remove();return;}
            var camera=Camera.main;
            Quaternion rotation=camera==null?source.transform.rotation:camera.transform.rotation;
            if(stopping)
            {
                fade+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
                float alpha=1f-Mathf.Clamp01(fade/.28f);
                for(int i=0;i<3;i++)scrolls[i].color=new Color(1,1,1,alpha*alpha);
                for(int i=0;i<fragments.Length;i++)
                {
                    float angle=(i%4*90f+i/4*31f)*Mathf.Deg2Rad;
                    Vector3 drift=new Vector3(Mathf.Cos(angle)*.7f,.45f+Mathf.Sin(angle)*.35f,0);
                    fragments[i].transform.position=fragmentOrigins[i]+rotation*drift*fade;
                    fragments[i].transform.rotation=rotation;
                    Color color=fragments[i].color;color.a=alpha;fragments[i].color=color;
                }
                if(alpha<=0f)Remove();return;
            }
            phase+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime)*70f*Mathf.Deg2Rad;
            float body=Mathf.Clamp(source.bounds.size.y,.7f,3f);
            float radius=body*.40f;
            Vector3 right=rotation*Vector3.right;
            Vector3 depth=camera==null?Vector3.forward:Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            Vector3 center=source.bounds.center-Vector3.up*body*.12f;
            for(int i=0;i<3;i++)
            {
                float angle=phase+i*Mathf.PI*2f/3f;
                float back=Mathf.Sin(angle);
                var scroll=scrolls[i];
                scroll.transform.position=center+right*(Mathf.Cos(angle)*radius)+depth*(back*radius*.65f)+
                    Vector3.up*(Mathf.Sin(angle*2f)*body*.025f);
                scroll.transform.rotation=rotation*Quaternion.Euler(0,0,Mathf.Cos(angle)*9f);
                scroll.transform.localScale=Vector3.one*Mathf.Clamp(body*.16f,.19f,.38f);
                scroll.sortingLayerID=source.sortingLayerID;
                scroll.sortingOrder=source.sortingOrder+(back>0f?-1:1);
            }
        }
    }

    private void Remove()
    {
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    private static Sprite PixelSprite=>pixelSprite!=null?pixelSprite:
        pixelSprite=PixelNinjaSubstitution.MakeSprite("Scroll Fragment",1,1,1f,(x,y)=>Color.white);

    private static Sprite ScrollSprite=>scrollSprite!=null?scrollSprite:
        scrollSprite=PixelNinjaSubstitution.MakeSprite("Pixel Ninjutsu Scroll",16,24,24f,(x,y)=>
        {
            Color outline=new Color(.20f,.12f,.28f),paper=new Color(.92f,.84f,.65f);
            if(y<2||y>21)
            {
                if(x<1||x>14)return Color.clear;
                return x==1||x==14||y==0||y==23?outline:new Color(.53f,.35f,.60f);
            }
            if(x<3||x>12)return Color.clear;
            if(x==3||x==12)return outline;
            if(y==2||y==21)return new Color(.68f,.52f,.44f);
            if(x==4)return new Color(1f,.94f,.78f);
            if(x==11)return new Color(.73f,.60f,.47f);
            bool ink=(y==17||y==12||y==7)&&x>=6&&x<=9||
                x==7&&y>=7&&y<=18||x==9&&(y==16||y==11||y==8);
            return ink?new Color(.45f,.23f,.65f):paper;
        });
}
