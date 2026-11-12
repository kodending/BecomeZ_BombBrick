using System;
using UnityEngine;

// Visual clock follows combat's launch/descent/impact events; it never applies damage.
public sealed class PixelWizardSupernova:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private readonly SpriteRenderer[,] rising=new SpriteRenderer[5,4];
    private readonly Vector3[] starts=new Vector3[5];
    private readonly float[] launchAges={-1f,-1f,-1f,-1f,-1f};
    private readonly SpriteRenderer[] tail=new SpriteRenderer[8];
    private readonly SpriteRenderer[] debris=new SpriteRenderer[18];
    private SpriteRenderer meteor,core,shadow,impactFlash;
    private Sprite shadowSprite;
    private Texture2D shadowTexture;
    private Func<bool> valid;
    private Color color;
    private Vector3 gatherPoint,sky,screenRight,screenUp;
    private Quaternion billboard;
    private float radius,age,phaseAge;
    private int phase; // 0: skyward stars, 1: meteor and shadow, 2: impact
    private CombatProjectileSource.Flight flight;
    private float previousClock;

    public static PixelWizardSupernova Create(Vector3 center,Vector3 caster,
        float radius,SpriteRenderer sorting,Color color,Func<bool> valid)
    {
        var effect=new GameObject("Wizard Supernova Meteor").AddComponent<PixelWizardSupernova>();
        effect.transform.position=new Vector3(center.x,.035f,center.z);
        effect.radius=radius;effect.color=color;effect.valid=valid;
        effect.flight=CombatProjectileSource.Capture(sorting==null?null:sorting.transform);
        effect.previousClock=effect.flight.clock==null?0:effect.flight.clock();
        Camera camera=Camera.main;
        effect.billboard=camera==null?Quaternion.identity:camera.transform.rotation;
        effect.screenRight=camera==null?Vector3.right:camera.transform.right;
        effect.screenUp=camera==null?Vector3.up:camera.transform.up;
        effect.gatherPoint=caster+effect.screenUp*9f;
        // Camera-plane axes make the meteor visibly diagonal in either battlefield orientation.
        effect.sky=effect.transform.position+(effect.screenUp-effect.screenRight)*9f;
        int layer=sorting==null?0:sorting.sortingLayerID;
        int order=(sorting==null?0:sorting.sortingOrder)+35;
        for(int i=0;i<5;i++)for(int j=0;j<4;j++)
            effect.rising[i,j]=effect.Star("Skyward Star "+(i+1)+" Trail "+j,layer,order-j);
        for(int i=0;i<effect.tail.Length;i++)
            effect.tail[i]=effect.Star("Meteor Tail "+i,layer,order-i);
        effect.meteor=effect.Star("Giant Meteor Star",layer,order+1);
        effect.core=effect.Star("White Hot Core",layer,order+2);
        effect.shadow=effect.Star("Growing Landing Shadow",layer,1);
        effect.CreateShadow();
        effect.shadow.sprite=effect.shadowSprite;
        effect.shadow.transform.rotation=Quaternion.Euler(90f,0f,0f);
        effect.impactFlash=effect.Star("Supernova Ground Flash",layer,2);
        effect.impactFlash.transform.rotation=Quaternion.Euler(90f,0f,0f);
        for(int i=0;i<effect.debris.Length;i++)
            effect.debris[i]=effect.Star("Supernova Star Debris",layer,order+i%3);
        return effect;
    }

    public void Launch(Vector3 start,int index)
    {
        starts[index]=start;launchAges[index]=age;UpdateVisual();
    }
    public void BeginDescent(){phase=1;phaseAge=0f;meteor.transform.position=sky;UpdateVisual();}
    public bool DeflectBeforeImpact()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(flight.deflect==null||!flight.deflect(meteor.transform.position,transform.position,meteor))return false;
            Cancel();return true;
        }
    }
    public void Impact(){phase=2;phaseAge=0f;UpdateVisual();}
    public void Cancel()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;flight.finished?.Invoke();Release(gameObject);
        }
    }

    private SpriteRenderer Star(string name,int layer,int order)
    {
        var star=new GameObject(name).AddComponent<SpriteRenderer>();
        star.transform.SetParent(transform,false);star.sprite=PixelStarProjectile.SharedSprite;
        star.transform.rotation=billboard;star.sortingLayerID=layer;star.sortingOrder=order;
        star.enabled=false;return star;
    }

    private void CreateShadow()
    {
        const int size=48;
        shadowTexture=new Texture2D(size,size,TextureFormat.RGBA32,false)
        {name="Soft Star Meteor Shadow",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float dx=(x+.5f-size*.5f)/(size*.5f),dy=(y+.5f-size*.5f)/(size*.5f);
            float angle=Mathf.Atan2(dy,dx);
            float boundary=.77f+.18f*Mathf.Cos(angle*4f);
            float distance=Mathf.Sqrt(dx*dx+dy*dy)/boundary;
            byte alpha=(byte)(255f*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.52f,1f,distance))));
            pixels[y*size+x]=new Color32(255,255,255,alpha);
        }
        shadowTexture.SetPixels32(pixels);shadowTexture.Apply(false,true);
        shadowSprite=Sprite.Create(shadowTexture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(flight.valid!=null&&!flight.valid()){Cancel();return;}
            float dt=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(flight.clock!=null){float now=flight.clock();dt=Mathf.Max(0,now-previousClock);previousClock=now;}
            age+=dt;phaseAge+=dt;
            if(age>5f||(valid!=null&&!valid())||(phase==2&&phaseAge>=.7f)){Cancel();return;}
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        for(int i=0;i<5;i++)for(int j=0;j<4;j++)
        {
            var star=rising[i,j];
            float t=(age-launchAges[i]-j*.027f)/WizardMouseCombatRules.SupernovaAscentDuration;
            star.enabled=phase==0&&launchAges[i]>=0f&&t>=0f&&t<1f;
            if(!star.enabled)continue;
            float curve=Mathf.Clamp01(t);
            star.transform.position=Vector3.Lerp(starts[i],gatherPoint,curve*curve)+
                screenRight*((i-2)*.25f*Mathf.Sin(curve*Mathf.PI));
            star.transform.localScale=new Vector3(.6f,1.1f,1f)*PixelStarProjectile.WizardStarVisualMultiplier*(1f-j*.18f);
            Color tint=Color.Lerp(color,Color.white,.65f);tint.a=(1f-curve*.7f)*(1f-j*.22f);
            star.color=tint;
        }
        if(phase==0)return;
        Vector3 center=transform.position;
        float bodySize=radius*1.25f*1.35f/PixelStarProjectile.SharedSprite.bounds.size.x;
        if(phase==1)
        {
            float t=Mathf.Clamp01(phaseAge/WizardMouseCombatRules.SupernovaDelay);
            float travel=.1f*t+.9f*t*t*t;
            Vector3 position=Vector3.Lerp(sky,center,travel);
            if(flight.deflect!=null&&flight.deflect(meteor.transform.position,position,meteor)){Cancel();return;}
            meteor.enabled=core.enabled=shadow.enabled=true;
            meteor.transform.position=core.transform.position=position;
            meteor.transform.rotation=core.transform.rotation=billboard*Quaternion.Euler(0f,0f,-18f+t*32f);
            meteor.transform.localScale=Vector3.one*bodySize*Mathf.Lerp(.72f,1f,t);
            core.transform.localScale=meteor.transform.localScale*.68f;
            meteor.color=CombatFlashPresentation.Peak(Color.Lerp(color,Color.white,.2f));core.color=CombatFlashPresentation.Peak(new Color(1f,.98f,.86f));
            Vector3 away=(sky-center).normalized;
            for(int i=0;i<tail.Length;i++)
            {
                float behind=(i+1)/(float)tail.Length;
                tail[i].enabled=true;
                tail[i].transform.position=position+away*behind*Mathf.Lerp(1.2f,6f,t);
                tail[i].transform.rotation=billboard*Quaternion.Euler(0f,0f,-45f);
                tail[i].transform.localScale=new Vector3(1f,1.45f,1f)*bodySize*(1f-behind*.9f)*.85f;
                Color tint=Color.Lerp(color,Color.white,(1f-behind)*.4f);
                tint.a=(1f-behind)*.38f;tail[i].color=tint;
            }
            shadow.transform.position=center;
            float shadowGrowth=Mathf.SmoothStep(0f,1f,t);
            shadow.transform.localScale=Vector3.one*radius*2f*Mathf.Lerp(.18f,1f,shadowGrowth);
            shadow.color=new Color(.025f,.015f,.055f,Mathf.Lerp(.12f,.68f,shadowGrowth));
            return;
        }
        float elapsed=phaseAge,fade=1f-Mathf.Clamp01(elapsed/.7f);
        for(int i=0;i<tail.Length;i++)tail[i].enabled=false;
        shadow.enabled=elapsed<.12f;
        shadow.transform.localScale=Vector3.one*radius*2f;
        shadow.color=new Color(.025f,.015f,.055f,.68f*(1f-Mathf.Clamp01(elapsed/.12f)));
        meteor.enabled=core.enabled=elapsed<.1f;
        meteor.transform.position=core.transform.position=center+Vector3.up*.16f;
        meteor.transform.rotation=core.transform.rotation=billboard;
        meteor.transform.localScale=new Vector3(1f+elapsed*4f,Mathf.Max(.18f,1f-elapsed*9f),1f)*bodySize;
        core.transform.localScale=meteor.transform.localScale*.68f;
        meteor.color=CombatFlashPresentation.Peak(Color.Lerp(color,Color.white,.7f));core.color=CombatFlashPresentation.Flash(Color.white);
        impactFlash.enabled=true;
        impactFlash.transform.position=center+Vector3.up*.02f;
        impactFlash.transform.localScale=Vector3.one*(radius*2f/PixelStarProjectile.SharedSprite.bounds.size.x)*
            Mathf.Lerp(.25f,1f,Mathf.Clamp01(elapsed/.13f));
        Color flash=Color.Lerp(color,Color.white,.75f);flash.a=.85f*(1f-Mathf.Clamp01(elapsed/.32f));
        impactFlash.color=CombatFlashPresentation.Flash(flash);
        for(int i=0;i<debris.Length;i++)
        {
            var shard=debris[i];shard.enabled=true;
            float t=Mathf.Clamp01(elapsed/(.5f+i%3*.08f));
            float angle=(i*137.5f)*Mathf.Deg2Rad;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            shard.transform.position=center+direction*radius*t*(.55f+i%3*.2f)+
                Vector3.up*(4f*t*(1f-t)*(1.1f+i%3*.55f));
            shard.transform.rotation=billboard*Quaternion.Euler(0f,0f,t*(i%2==0?160f:-160f));
            shard.transform.localScale=Vector3.one*(.5f+i%3*.28f)*(1f-t*.65f);
            Color tint=Color.Lerp(new Color(1f,.95f,.72f),color,t);
            tint.a=(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.55f,1f,t)))*fade;shard.color=tint;
        }
    }

    private void OnDestroy()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            flight.finished?.Invoke();Release(shadowSprite);Release(shadowTexture);
        }
    }
    private static void Release(UnityEngine.Object value)
    {
        if(value==null)return;
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }
}
