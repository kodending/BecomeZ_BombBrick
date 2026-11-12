using System;
using UnityEngine;

// One body-bound pixel seal. On detonation it flashes, splits and expires at the impact position.
public sealed class PixelNinjaMoonMark:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite crescent,leftHalf,rightHalf;
    private SpriteRenderer target,main,left,right;
    private Func<bool> valid;
    private float elapsed,size,primeElapsed;
    private bool exploding,primed;

    public static PixelNinjaMoonMark Play(SpriteRenderer target,Func<bool> valid)
    {
        if(target==null)return null;
        if(crescent==null)
        {
            crescent=PixelNinjaSubstitution.MakeSprite("Black Moon Seal",24,28,28f,(x,y)=>
            {
                float outer=(x-11.5f)*(x-11.5f)/110f+(y-13.5f)*(y-13.5f)/156f;
                float inner=(x-17f)*(x-17f)/100f+(y-16f)*(y-16f)/132f;
                if(outer>1f||inner<.65f)return Color.clear;
                if(outer>.79f||inner<.93f)return new Color(.19f,.06f,.32f);
                return outer>.5f?new Color(.97f,.91f,1f):new Color(.73f,.43f,1f);
            });
            leftHalf=Sprite.Create(crescent.texture,new Rect(0,0,12,28),new Vector2(.5f,.5f),28f);
            rightHalf=Sprite.Create(crescent.texture,new Rect(12,0,12,28),new Vector2(.5f,.5f),28f);
        }
        var fx=new GameObject("Ninja Black Moon Mark").AddComponent<PixelNinjaMoonMark>();
        fx.target=target;fx.valid=valid;fx.size=Mathf.Clamp(target.bounds.size.y*.5f,.9f,1.3f);
        fx.main=fx.Child("Crescent Seal",crescent);
        fx.left=fx.Child("Split Left",leftHalf);fx.right=fx.Child("Split Right",rightHalf);
        fx.left.enabled=fx.right.enabled=false;fx.Animate(0f);return fx;
    }

    private SpriteRenderer Child(string name,Sprite sprite)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
        renderer.color=Color.white;renderer.sortingLayerID=target.sortingLayerID;
        renderer.sortingOrder=target.sortingOrder+40;return renderer;
    }

    public void Prime(){if(this!=null&&!exploding){primed=true;primeElapsed=0f;}}

    public void Explode()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(this==null||exploding)return;
            exploding=true;elapsed=0f;valid=null;
            if(target!=null)
            {
                PixelNinjaMoonFx.PlayImpact(target,Vector3.right,true);
                target.GetComponent<SpriteMonsterAnimationDriver>()?.PlayImpactRecoil(Vector3.back,.22f);
            }
            Animate(0f);
        }
    }

    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    private void LateUpdate()
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
        if(!exploding)
        {
            if(target==null||(valid!=null&&!valid())){Cancel();return;}
            transform.rotation=Camera.main==null?target.transform.rotation:Camera.main.transform.rotation;
            transform.position=target.bounds.center+transform.up*size*.68f;
            main.sortingOrder=left.sortingOrder=right.sortingOrder=target.sortingOrder+40;
            if(primed)primeElapsed+=Mathf.Max(0f,dt);
            float stamp=Mathf.Lerp(1.3f,1f,Mathf.Clamp01(elapsed/.11f));
            float charge=primed?1f+.45f*Mathf.Clamp01(primeElapsed/NinjaCatRank3Rules.BlackMoonSheatheDuration):1f;
            transform.localScale=Vector3.one*size*.6f*stamp*charge;
            main.color=Color.white;
            return;
        }
        if(elapsed>=.40f){Cancel();return;}
        main.color=new Color(.96f,.94f,1f);
        bool split=elapsed>=.065f;main.enabled=!split;left.enabled=right.enabled=split;
        float p=Mathf.InverseLerp(.065f,.40f,elapsed);
        left.transform.localPosition=new Vector3(-.214f-p*.8f,p*.5f,0f);
        right.transform.localPosition=new Vector3(.214f+p*.8f,-p*.5f,0f);
        left.color=right.color=new Color(.88f,.77f,1f,1f-p);
    }
}
