using System;
using UnityEngine;

// Battle-owned goddess: the caster's sprite and lifetime do not affect this apparition.
[DefaultExecutionOrder(10040)]
public sealed class PixelSpiritGuardian:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite[] frames;
    private SpriteRenderer body;
    private Material bodyMaterial;
    private ParticleSystem lightParticles;
    private readonly ParticleSystem.Particle[] pixels=new ParticleSystem.Particle[176];
    private Func<float> clock;
    private Func<bool> valid;
    private Bounds arena;
    private Vector3 anchor;
    private Quaternion view;
    private float born,height;
    private Color tint,bodyTint;
    private const float BlessingStart=.7f;
    private Action blessingStarted;
    public bool IsAllied{get;private set;}
    public SpriteRenderer BodyRenderer=>body;

    public void CompleteCinematicEntrance()
    {
        if(clock==null||body==null)return;
        // The film replaces the descent while battle time remains frozen; returning must not restart it.
        float age=Mathf.Max(2.4f,clock()-born);
        blessingStarted=null;
        born=clock()-age;Draw(age);
    }

    public static PixelSpiritGuardian Create(SpriteRenderer source,Bounds arena,bool allied,
        Func<float> clock,Func<bool> valid,float side=0f,Action blessingStarted=null)
    {
        var texture=Resources.Load<Texture2D>("CombatEffects/SpiritDeer/GuardianGoddess16");
        if(source==null||texture==null)return null;
        if(frames==null||frames[0]==null)
        {
            frames=new Sprite[16];float w=texture.width/4f,h=texture.height/4f;
            for(int i=0;i<frames.Length;i++)
            {
                frames[i]=Sprite.Create(texture,new Rect(i%4*w,(3-i/4)*h,w,h),
                    new Vector2(.5f,.5f),h,0,SpriteMeshType.FullRect);
                frames[i].name="Guardian Goddess "+i.ToString("D2");
                frames[i].hideFlags=HideFlags.HideAndDontSave;
            }
        }
        var fx=new GameObject("Guardian Spirit - Battle Protection").AddComponent<PixelSpiritGuardian>();
        fx.IsAllied=allied;
        fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;fx.born=fx.clock();fx.arena=arena;
        fx.blessingStarted=blessingStarted;
        fx.tint=allied?new Color(1f,.87f,.52f):new Color(.83f,.69f,1f);
        fx.bodyTint=allied?Color.white:new Color(.84f,.79f,1f);
        var camera=Camera.main;
        fx.view=camera==null?source.transform.rotation:camera.transform.rotation;
        fx.height=Mathf.Clamp(arena.size.z*.85f,7f,12f);
        fx.anchor=arena.center+fx.view*Vector3.up*(fx.height*.24f)+Vector3.right*side;
        var environment=camera==null?null:camera.GetComponent<LayeredBattleEnvironment>();
        if(environment!=null)
        {
            // Appear beyond the rear edge of the platform; its foreground masks the lower robe.
            float depth=camera.WorldToViewportPoint(arena.center).z;
            fx.anchor=camera.ViewportToWorldPoint(new Vector3(.5f,.79f,depth))+Vector3.right*side;
            float viewHeight=Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.5f,0f,depth)),
                camera.ViewportToWorldPoint(new Vector3(.5f,1f,depth)));
            fx.height=viewHeight*.4f;
        }
        if(side!=0f)fx.height*=.78f;
        fx.body=new GameObject("Guardian Goddess - Behind Battle").AddComponent<SpriteRenderer>();
        fx.body.transform.SetParent(fx.transform,false);
        fx.body.sprite=frames[0];fx.body.transform.rotation=fx.view;
        fx.body.transform.localScale=Vector3.one*fx.height;
        fx.body.sortingLayerID=source.sortingLayerID;
        fx.body.sortingOrder=environment==null?1:environment.GuardianBackdropOrder;
        fx.bodyMaterial=new Material(Resources.Load<Shader>("CombatEffects/SpiritDeer/GuardianVeil"));
        fx.bodyMaterial.SetFloat("_RearEdge",environment==null?-1f:environment.GuardianRearEdgeViewportY);
        fx.body.sharedMaterial=fx.bodyMaterial;
        fx.lightParticles=PixelMushroomGas.CreateParticles("Goddess Blessing Light",fx.transform,fx.pixels.Length,
            PixelMushroomGas.Material(true),fx.body,0,true);
        // Blessing reaches the ground, but actors and their attacks remain in front of the light.
        fx.lightParticles.GetComponent<ParticleSystemRenderer>().sortingOrder=7;
        fx.Draw(0f);return fx;
    }

    private static float Seed(int n)=>Mathf.Repeat(n*.618033989f,1f);
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(valid==null||!valid()){Cancel();return;}
            float age=Mathf.Max(0f,clock()-born);
            Draw(age);
            if(age>=BlessingStart&&blessingStarted!=null)
            {Action callback=blessingStarted;blessingStarted=null;callback();}
        }
    }

    private void Draw(float age)
    {
        float t=Mathf.Clamp01(age/2.4f),ease=1f-Mathf.Pow(1f-t,3f);
        Vector3 at=anchor+view*Vector3.up*(height*((1f-ease)*.8f+Mathf.Sin(age*1.1f)*.006f));
        body.transform.position=at;
        body.sprite=frames[Mathf.FloorToInt(age*10f)%frames.Length];
        Color color=bodyTint;color.a=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/1.1f))*(.46f+Mathf.Sin(age*1.1f)*.015f);
        body.color=color;
        float blessing=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((age-BlessingStart)/1.7f));
        for(int i=0;i<pixels.Length;i++)
        {
            float seed=Seed(i*71+7),fall=Mathf.Repeat(age*(.15f+seed*.04f)+Seed(i*19+2),1f);
            Vector3 floor=new Vector3(Mathf.Lerp(arena.min.x,arena.max.x,Seed(i+1)),.08f,
                Mathf.Lerp(arena.min.z,arena.max.z,Seed(i*37+13)));
            Vector3 position;float alpha,size=.035f+seed*.035f,length=1f;
            if(i<64)
            {
                Vector3 hand=at+view*new Vector3((i%2==0?-1f:1f)*height*.30f,height*.025f,0f);
                position=Vector3.Lerp(hand,floor,fall);
                position+=view*Vector3.up*Mathf.Sin(fall*Mathf.PI)*height*.055f;
                alpha=blessing*Mathf.Sin(fall*Mathf.PI)*.48f;
                length=2.5f;
            }
            else if(i<144)
            {
                position=floor+Vector3.up*(1f-fall)*(1.6f+seed)+Vector3.right*Mathf.Sin(age*.6f+i)*.13f;
                alpha=blessing*Mathf.Sin(fall*Mathf.PI)*.36f;
            }
            else
            {
                // Sparse vertical glints accompany descent, then yield to the steady blessing.
                position=at+view*new Vector3((Seed(i*13)-.5f)*height*.85f,(fall-.25f)*height,0f);
                alpha=Mathf.Clamp01(age/.4f)*Mathf.Clamp01((3.1f-age)/1.1f)*Mathf.Sin(fall*Mathf.PI)*.32f;
                length=5f;
            }
            Color c=Color.Lerp(tint,Color.white,seed*.65f);c.a=alpha;
            pixels[i]=new ParticleSystem.Particle{position=position,startSize3D=new Vector3(size,size*length,size),
                startColor=c,startLifetime=10,remainingLifetime=10,randomSeed=(uint)(i+1)};
        }
        lightParticles.SetParticles(pixels,pixels.Length);
    }

    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void OnDestroy()
    {
        if(bodyMaterial==null)return;
        if(Application.isPlaying)Destroy(bodyMaterial);else DestroyImmediate(bodyMaterial);
    }
}
