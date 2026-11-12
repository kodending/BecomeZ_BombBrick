using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10040)]
public sealed class PixelHaetaeDevourer:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite[] frames;
    private SpriteRenderer body;
    private Material veil;
    private ParticleSystem motes,streams;
    private readonly ParticleSystem.Particle[] pixels=new ParticleSystem.Particle[1536];
    private readonly List<Meal> meals=new List<Meal>();
    private Func<float> clock;
    private Func<bool> valid;
    private Action<Vector3> swallowed;
    private Action<Vector3> absorbed;
    private bool swallowPending;
    private Renderer[] cinematicRenderers;
    private bool[] cinematicRendererHidden;
    private bool cinematicHidden;
    private float cinematicHiddenAt,cinematicDelay;
    private Vector3 anchor,mouth;
    private Quaternion view;
    private float born,height,side,feedingStart=-100f,lastArrival=-100f,ending=-1f;
    private int mealSequence;
    public bool Ending=>ending>=0f;
    public Vector3 MouthPosition=>mouth;
    public SpriteRenderer BodyRenderer=>body;
    public bool PresentationHidden=>cinematicHidden;
    private float PresentationTime=>cinematicHidden?cinematicHiddenAt-cinematicDelay:clock()-cinematicDelay;
    private sealed class Meal { public Vector3 start;public float time,duration;public int seed;public CombatBenefitKind kind;public bool soundPending; }

    public static PixelHaetaeDevourer Create(SpriteRenderer reference,Bounds arena,bool allied,Func<float> clock,Func<bool> valid,
        Action<Vector3> swallowed=null,Action<Vector3> absorbed=null,bool cinematicHidden=false)
    {
        var texture=Resources.Load<Texture2D>("CombatEffects/Haetae/Devourer16");
        if(reference==null||texture==null)return null;
        if(frames==null||frames[0]==null)
        {
            frames=new Sprite[16];float cell=texture.width/4f;
            for(int i=0;i<16;i++)
            {
                frames[i]=Sprite.Create(texture,new Rect(i%4*cell,(3-i/4)*cell,cell,cell),new Vector2(.5f,.5f),cell,0,SpriteMeshType.FullRect);
                frames[i].name="Devouring Beast "+i.ToString("D2");frames[i].hideFlags=HideFlags.HideAndDontSave;
            }
        }
        var fx=new GameObject("Haetae - Devouring Beast").AddComponent<PixelHaetaeDevourer>();
        fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;fx.born=fx.clock();fx.side=allied?1f:-1f;
        fx.swallowed=swallowed;fx.absorbed=absorbed;
        var camera=Camera.main;fx.view=camera==null?reference.transform.rotation:camera.transform.rotation;
        fx.height=arena.size.z*.55f;fx.anchor=arena.center+Vector3.right*(arena.extents.x+2f)+Vector3.up*3f;
        if(camera!=null)
        {
            float depth=camera.WorldToViewportPoint(arena.center).z;
            fx.anchor=camera.ViewportToWorldPoint(new Vector3(allied?.88f:.12f,.59f,depth));
            fx.height=Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.5f,0f,depth)),
                camera.ViewportToWorldPoint(new Vector3(.5f,1f,depth)))*.28f;
        }
        fx.body=new GameObject("Devourer Sprite Animation").AddComponent<SpriteRenderer>();
        fx.body.transform.SetParent(fx.transform,false);fx.body.transform.rotation=fx.view;
        fx.body.transform.localScale=Vector3.one*fx.height;fx.body.sprite=frames[0];fx.body.flipX=!allied;
        fx.body.sortingLayerID=reference.sortingLayerID;fx.body.sortingOrder=8;
        fx.veil=new Material(Resources.Load<Shader>("CombatEffects/Haetae/DevourerVeil"));fx.body.sharedMaterial=fx.veil;
        fx.streams=PixelMushroomGas.CreateParticles("Devoured Support Streams",fx.transform,fx.pixels.Length,
            PixelMushroomGas.Material(false),reference,8,true);
        fx.motes=PixelMushroomGas.CreateParticles("Devourer Mouth Sparks",fx.transform,fx.pixels.Length,
            PixelMushroomGas.Material(true),reference,9,true);
        fx.SetCinematicHidden(cinematicHidden);fx.Draw(fx.PresentationTime,false);return fx;
    }

    public void SetCinematicHidden(bool hidden,bool revealHead=false)
    {
        if(hidden&&!cinematicHidden)
        {
            cinematicHiddenAt=clock();cinematicHidden=true;
            cinematicRenderers=GetComponentsInChildren<Renderer>(true);
            cinematicRendererHidden=new bool[cinematicRenderers.Length];
            for(int i=0;i<cinematicRenderers.Length;i++)cinematicRendererHidden[i]=cinematicRenderers[i].forceRenderingOff;
        }
        else if(!hidden&&cinematicHidden)
        {cinematicDelay+=Mathf.Max(0f,clock()-cinematicHiddenAt);cinematicHidden=false;}
        if(cinematicRenderers==null)return;
        for(int i=0;i<cinematicRenderers.Length;i++)
            if(cinematicRenderers[i]!=null)
                cinematicRenderers[i].forceRenderingOff=cinematicRendererHidden[i]||hidden&&(!revealHead||cinematicRenderers[i]!=body);
        if(!hidden){cinematicRenderers=null;cinematicRendererHidden=null;Draw(PresentationTime,false);}
    }

    public void CompleteCinematicEntrance()
    {
        float now=PresentationTime;born=Mathf.Min(born,now-.9f);Draw(now,false);
    }
    private static float Seed(int n)=>Mathf.Repeat(Mathf.Sin(n*127.1f+311.7f)*43758.5453f,1f);
    private static Color Tint(CombatBenefitKind kind)=>kind==CombatBenefitKind.Healing?new Color(.35f,1f,.55f):
        kind==CombatBenefitKind.Shield?new Color(.3f,.8f,1f):new Color(1f,.78f,.28f);
    private static ParticleSystem.Particle Pixel(Vector3 point,float size,Color color,int seed)=>
        new ParticleSystem.Particle{position=point,startSize=size,startColor=color,startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(seed+1)};

    public void Absorb(Vector3 from,CombatBenefitKind kind)
    {
        if(this==null||Ending)return;
        float now=PresentationTime;
        if(now>lastArrival+.25f)feedingStart=now;
        float duration=Mathf.Clamp(.5f+Vector3.Distance(from,mouth)*.025f,.55f,.85f);
        lastArrival=Mathf.Max(lastArrival,now+duration);
        swallowPending=true;
        // Visual packets are bounded; the combat gate has already rejected every effect.
        if(meals.Count>=32)meals.RemoveAt(0);
        meals.Add(new Meal{start=from,time=now,duration=duration,kind=kind,seed=mealSequence++,soundPending=cinematicHidden});
        if(!cinematicHidden)absorbed?.Invoke(from);
    }
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(clock==null){Cancel();return;}
            if(cinematicHidden)
            {
                if(valid!=null&&valid())return;
                SetCinematicHidden(false);
            }
            float now=PresentationTime;
            if((valid==null||!valid())&&ending<0f)ending=now;
            if(ending>=0f&&now-ending>.65f){Cancel();return;}
            Draw(now);
        }
    }
    private Vector3 Path(Meal meal,float t)
    {
        t=Mathf.Pow(Mathf.Clamp01(t),2.2f);
        Vector3 bend=(meal.start+mouth)*.5f+view*Vector3.up*height*(.25f+Seed(meal.seed)*.22f);
        return (1f-t)*(1f-t)*meal.start+2f*(1f-t)*t*bend+t*t*mouth;
    }
    private void Draw(float now,bool feedback=true)
    {
        float age=Mathf.Max(0f,now-born),appear=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/.9f));
        float disappear=ending<0f?0f:Mathf.Clamp01((now-ending)/.65f);
        float visible=appear*(1f-disappear);
        body.transform.position=anchor+view*Vector3.right*(side*height*(1f-appear)*.18f);
        mouth=body.transform.position+view*new Vector3(-side*height*.23f,-height*.02f,0f);
        int frame;
        if(!cinematicHidden&&now<=lastArrival)
            frame=now-feedingStart<.25f?4+Mathf.Min(3,Mathf.FloorToInt((now-feedingStart)*16f)):
                8+Mathf.FloorToInt((now-feedingStart)*10f)%3;
        else if(!cinematicHidden&&now-lastArrival<.3f)frame=11+Mathf.Min(4,Mathf.FloorToInt((now-lastArrival)*16.7f));
        else frame=Mathf.FloorToInt(age*4f)%4;
        body.sprite=frames[frame];body.color=new Color(1f,1f,1f,.75f*visible);veil.SetFloat("_Reveal",visible);
        if(feedback&&!cinematicHidden&&ending<0f&&swallowPending&&now>lastArrival)
        {
            swallowPending=false;
            swallowed?.Invoke(mouth);
        }
        int count=0;
        for(int n=meals.Count-1;n>=0;n--)
        {
            Meal meal=meals[n];float t=(now-meal.time)/meal.duration;
            if(feedback&&!cinematicHidden&&ending<0f&&meal.soundPending&&now>meal.time)
            {meal.soundPending=false;absorbed?.Invoke(meal.start);}
            if(t>1.16f){meals.RemoveAt(n);continue;}
            for(int i=0;i<30;i++)
            {
                float s=Seed(i+meal.seed*37),tail=i<12?i/12f*.25f:0f;
                float q=Mathf.Clamp01(t-tail);Vector3 point=Path(meal,q);
                float spread=(.2f+s*.25f)*(1f-q*.88f);
                point+=view*new Vector3(Mathf.Cos(i*2.4f+now*8f)*spread,Mathf.Sin(i*2.4f+now*8f)*spread,0f);
                Color color=Color.Lerp(Tint(meal.kind),Color.white,s*.45f);
                color.a=visible*(i<12?.4f:.85f)*Mathf.Clamp01(t/.08f)*Mathf.Clamp01((1.15f-t)*7f);
                pixels[count++]=Pixel(point,(i<12?.07f:.16f)*(1f-q*.55f)*(1f+s*.5f),color,i+meal.seed*31);
            }
        }
        streams.SetParticles(pixels,count);count=0;
        float feast=now<=lastArrival+.25f?1f:0f;
        for(int i=0;i<70;i++)
        {
            float s=Seed(i),phase=Mathf.Repeat(age*(.35f+s*.2f)+s,1f);
            Vector3 point;
            float alpha;
            if(i<34)
            {
                point=mouth+view*new Vector3(-side*height*(1f-phase)*.25f,
                    Mathf.Sin(i*2.4f+age*2f)*height*(1f-phase)*.10f,0f);
                alpha=feast*.58f*Mathf.Sin(phase*Mathf.PI);
            }
            else
            {
                point=anchor+view*new Vector3((Seed(i+73)-.5f)*height*.85f,
                    (Seed(i+12)-.5f)*height*.75f,0f);
                point+=view*Vector3.right*side*disappear*height*.4f;
                alpha=(1f-appear+disappear)*.8f;
            }
            Color color=Color.Lerp(new Color(.38f,.8f,.7f),new Color(1f,.85f,.4f),s);
            color.a=alpha*(1f-disappear)*Mathf.Clamp01(age/.12f);
            pixels[count++]=Pixel(point,.035f+s*.055f,color,i);
        }
        motes.SetParticles(pixels,count);
    }
    public void Cancel()
    {if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
    private void OnDestroy()
    {if(veil!=null){if(Application.isPlaying)Destroy(veil);else DestroyImmediate(veil);}}
}
