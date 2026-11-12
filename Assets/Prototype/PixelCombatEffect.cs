using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PixelCombatEffect:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    internal static float PreviewVisualScale
    {
        get
        {
            return CombatReadabilityPresentation.EffectVisualScale;
        }
    }

    internal static void ScaleParticleSize(ParticleSystem.MainModule main)
    {
        // Unity's size multiplier changes only the maximum of a two-constant range.
        var size=main.startSize;
        float scale=PreviewVisualScale;
        main.startSize=size.mode==ParticleSystemCurveMode.TwoConstants
            ?new ParticleSystem.MinMaxCurve(size.constantMin*scale,size.constantMax*scale)
            :new ParticleSystem.MinMaxCurve(size.constant*scale);
    }
    private static Vector3 AuthoredBodySize(SpriteRenderer reference)
    {
        // Actor art is already smaller; measure its authored body before scaling the effect once.
        Vector3 size=reference.bounds.size;
        if(reference.GetComponent<SpriteMonsterAnimationDriver>()!=null)size/=CombatReadabilityPresentation.BodyVisualScale;
        return size;
    }
    private const int MaxPooledEffects=64;
    private const int MaxContactImpacts=8;
    private static Sprite[] contactImpactFrames;
    private static readonly HashSet<PixelCombatEffect> ContactImpacts=new HashSet<PixelCombatEffect>(MaxContactImpacts);
    public static int ActiveContactImpacts
    {
        get
        {
            ContactImpacts.RemoveWhere(effect=>effect==null||!effect.gameObject.activeInHierarchy);
            return ContactImpacts.Count;
        }
    }
    public const string RaptorBiteResourcePath="CombatEffects/Raptor/Bite";
    public const float RaptorBiteFramesPerSecond=18f;
    public const int RaptorBiteImpactFrame=3;
    public const float RaptorApexBiteFramesPerSecond=48f;
    public const int RaptorApexBiteImpactFrame=2;
    public const string RaptorClawResourcePath="CombatEffects/Raptor/ClawSkill";
    public const float RaptorClawFramesPerSecond=20f;
    public const int RaptorClawImpactFrame=3;
    public const string SabertoothClawResourcePath=
        "CombatEffects/Sabertooth/BasicClaw";
    public const float SabertoothClawFramesPerSecond=18f;
    public const int SabertoothClawImpactFrame=3;
    public const string SabertoothFangLockResourcePath=
        "CombatEffects/Sabertooth/FangLock";
    public const float SabertoothFangLockFramesPerSecond=36f;
    public const string CrocodileTailStrikeResourcePath=
        "CombatEffects/Crocodile/TailStrike";
    public const float CrocodileTailStrikeFramesPerSecond=20f;
    public const int CrocodileTailStrikeImpactFrame=3;
    public const string CrocodileDeathRollResourcePath=
        "CombatEffects/Crocodile/DeathRoll";
    public const float CrocodileDeathRollFramesPerSecond=22f;
    public const int CrocodileDeathRollImpactFrame=3;
    public const string KingKongHeavyPunchResourcePath=
        "CombatEffects/KingKong/HeavyPunch";
    public const float KingKongHeavyPunchFramesPerSecond=22f;
    public const int KingKongHeavyPunchImpactFrame=3;
    public const string KingKongEarthPulseResourcePath=
        "CombatEffects/KingKong/EarthPulse";
    public const float KingKongEarthPulseFramesPerSecond=20f;
    public const int KingKongEarthPulseImpactFrame=4;
    public const string BearPawSwipeResourcePath="CombatEffects/Bear/PawSwipe";
    public const float BearPawSwipeFramesPerSecond=20f;
    public const int BearPawSwipeImpactFrame=2;
    public const string BearMountainHologramResourcePath=
        "CombatEffects/Bear/OriginalBear_";
    public const float NinjaKunaiSlashFramesPerSecond=22f;
    public const int NinjaKunaiSlashImpactFrame=3;
    public const float TyrannoKingJawBiteFramesPerSecond=24f;
    public const int TyrannoKingJawBiteImpactFrame=3;
    public const float MantisScytheSlashFramesPerSecond=30f;
    public const int MantisScytheSlashImpactFrame=3;
    public const float MammothTuskRamFramesPerSecond=26f;
    public const int MammothTuskRamImpactFrame=3;
    public const float BeetleHornUppercutFramesPerSecond=28f;
    public const int BeetleHornUppercutImpactFrame=3;
    public const float BeetleHornThrustFramesPerSecond=28f;
    public const int BeetleHornThrustImpactFrame=3;
    public const float GriffinEagleClawFramesPerSecond=30f;
    public const int GriffinEagleClawImpactFrame=3;
    public const string CriticalResourcePath="CombatEffects/Common/Critical";
    public const float CriticalFramesPerSecond=22f;

    private static readonly Dictionary<string,Sprite[]> FrameCache=
        new Dictionary<string,Sprite[]>(StringComparer.Ordinal);
    private static readonly Stack<PixelCombatEffect> AvailableEffects=
        new Stack<PixelCombatEffect>();
    private static Sprite[] raptorBitePlaybackFrames;
    private static Sprite[] raptorApexBiteFrames;
    private static Sprite[] wolfHuntImpactFrames;
    private static Sprite[] wolfSpectralImpactFrames;
    private static Texture2D raptorApexFangIcon;
    private static Sprite[] fangLockBindingFrames;
    private static Sprite[] tigerBiteFrames;
    private static Texture2D brokenShieldIcon;
    private static Sprite[] armorBreakFrames;
    private static readonly Dictionary<Color,Sprite[]> RaptorImpactFrames=
        new Dictionary<Color,Sprite[]>();
    private static Sprite[][] bearRagingStrikeFrames;
    private static Sprite[] bearEmpoweredPawFrames;
    private static Sprite[] bearMountainHitFrames;
    private static Sprite[] bearMountainCrescentFrames;
    private static Sprite[] bearPawImpactFrames;
    private static Sprite[] monkeyFruitPopFrames;
    private static Sprite[] monkeyFruitPopReadabilityFrames;
    private static Sprite[] bearCleaveHitFrames;
    private static Sprite[] bearRagingGroundFractureFrames;
    private static Sprite[][] bearKingRoarBurstFrames;
    private static Sprite[] bearAvatarRoarFrames;
    private static Material bearKingRoarSpiritMaterial;
    private static Sprite bearSpiritParticleSprite;
    private static Material bearSpiritParticleMaterial;
    private static Sprite freezeIceSprite;
    private static Sprite freezeBodySprite;
    private static Sprite freezeShardSprite;
    private static Material freezeShardMaterial;
    private static Sprite spiderWebGroundSprite;
    private static Sprite spiritSanctuaryGroundSprite;
    private static Sprite kingKongSlamWaveSprite;
    private static Sprite leylineGlowSprite;
    private static Material spiritDeerCrescentMaterial;
    private static Material spiritDeerLeafMaterial;
    private static Sprite mushroomPoisonGroundSprite;
    private static Sprite mushroomChildSprite;
    private static Sprite[] tyrannoLavaSpreadFrames;
    private static Sprite tyrannoFissureGroundSprite;
    private static Sprite healingCrossSprite;
    private static Material healingCrossMaterial;
    private static Sprite[] ninjaKunaiSlashFrames;
    private static Sprite[] tyrannoKingJawBiteFrames;
    private static Sprite[] mantisScytheSlashFrames;
    private static Sprite[] mammothTuskRamFrames;
    private static Sprite mammothPathGroundSprite;
    private static Sprite beetleHornShockwaveSprite;
    private static Sprite beetleFortressAuraSprite;
    private static Sprite[] beetleHornUppercutFrames;
    private static Sprite[] beetleHornThrustFrames;
    private static Sprite[] griffinEagleClawFrames;
    private static Sprite ninjaLinePixelSprite;

    private SpriteRenderer effectRenderer;
    private Color effectTint=Color.white;
    private SpriteRenderer anchorRenderer;
    private Transform anchor;
    private Sprite[] frames=Array.Empty<Sprite>();
    private Camera effectCamera;
    private bool faceCamera=true;
    private Quaternion cameraRotationOffset=Quaternion.identity;
    private float framesPerSecond;
    private float elapsed;
    private bool pooled;
    private bool contactImpact;
    private int contactStartedFrame=-1;
    private int readabilityBiteVersion;
    private int readabilityBiteHitFrame=-1;
    public int ReadabilityBiteVersion=>readabilityBiteVersion;

    public void AlignReadabilityBiteContact(int version)
    {
        if(!CombatReadabilityPresentation.Adjusted||pooled||version!=readabilityBiteVersion||
           name!="Raptor Bite Effect"||frames.Length<3)return;
        elapsed=2f/framesPerSecond;
        effectRenderer.sprite=frames[2];
        readabilityBiteHitFrame=Time.frameCount;
    }
    private CombatStatusState fangLockStatuses;
    private float fangLockBaseScale;
    private float fangLockBloodPulseRemaining;

    public void PulseFangLockBloodReflux()
    {
        if(fangLockStatuses==null||pooled)return;
        fangLockBloodPulseRemaining=0.2f;
        SetColor(new Color(1f,0.16f,0.24f));
    }

    public bool IsFangLockBinding(CombatStatusState statuses)=>
        !pooled&&fangLockStatuses!=null&&fangLockStatuses==statuses;

    public void PulseFangLockBite(float scale=1f)
    {
        if(fangLockStatuses==null||pooled)return;
        elapsed=0f;
        effectRenderer.sprite=frames[0];
        transform.localScale=Vector3.one*fangLockBaseScale*scale;
    }

    public void SetColor(Color color)
    {
        effectTint=color;
        if(effectRenderer!=null)effectRenderer.color=contactImpact?
            CombatFlashPresentation.Flash(color):CombatFlashPresentation.Peak(color);
    }

    public static void PrepareContactImpacts()
    {
        if(contactImpactFrames==null)contactImpactFrames=CreateContactImpactFrames();
        int cached=0;
        foreach(var effect in AvailableEffects)if(effect!=null)cached++;
        for(int i=cached;i<MaxContactImpacts;i++)
        {
            var instance=new GameObject("Contact impact pool");
            var effect=instance.AddComponent<PixelCombatEffect>();
            effect.effectRenderer=instance.AddComponent<SpriteRenderer>();
            effect.pooled=true;
            instance.SetActive(false);
            AvailableEffects.Push(effect);
        }
    }

    public static PixelCombatEffect PlayContactImpact(Transform victim,SpriteRenderer reference,
        Vector3 direction,bool strong,bool skill=false)
    {
        if(CombatReadabilityPresentation.Active&&!CombatReadabilityPresentation.ContactFeedback)return null;
        skill&=CombatReadabilityPresentation.Local;
        if(victim==null||reference==null||reference.sprite==null||!reference.enabled||
           !victim.gameObject.activeInHierarchy||ActiveContactImpacts>=MaxContactImpacts)return null;
        if(contactImpactFrames==null)PrepareContactImpacts();
        float size=Mathf.Clamp(AuthoredBodySize(reference).y*(strong?.5f:.38f),.26f,.56f);
        size*=skill?1.1f:CombatReadabilityPresentation.ContactSize;
        var effect=Play("Contact impact",contactImpactFrames,40f,null,reference,size,5);
        effect.contactImpact=true;
        effect.contactStartedFrame=Time.frameCount;
        ContactImpacts.Add(effect);
        var camera=effect.effectCamera;
        Vector3 right=camera!=null?camera.transform.right:reference.transform.right;
        Vector3 up=camera!=null?camera.transform.up:reference.transform.up;
        Vector2 projected=new Vector2(Vector3.Dot(direction,right),Vector3.Dot(direction,up));
        if(projected.sqrMagnitude<.0001f)projected=Vector2.right;
        projected.Normalize();
        Vector3 center=reference.bounds.center;
        if(CombatReadabilityPresentation.RefinedSkills)center=CombatReadabilityPresentation.VisibleBodyCenter(reference);
        effect.transform.position=center-
            (right*projected.x+up*projected.y)*Mathf.Min(.18f,reference.bounds.size.y*.18f);
        effect.cameraRotationOffset=Quaternion.Euler(0f,0f,Mathf.Atan2(projected.y,projected.x)*Mathf.Rad2Deg);
        float alpha=strong?.9f:.8f;
        alpha=skill?(strong?.94f:.84f):alpha*Mathf.Lerp(1f,CombatReadabilityPresentation.ContactIntensity,.5f);
        effect.SetColor(new Color(1f,strong?.88f:.78f,strong?.62f:.48f,alpha));
        effect.UpdateTransform();
        return effect;
    }

    private static Sprite[] CreateContactImpactFrames()
    {
        const int size=24;
        var result=new Sprite[4];
        for(int frame=0;frame<result.Length;frame++)
        {
            var pixels=new Color32[size*size];
            for(int y=2;y<size-2;y++)for(int x=2;x<size-2;x++)
            {
                float dx=x-11.5f,dy=y-11.5f;
                float ax=Mathf.Abs(dx),ay=Mathf.Abs(dy);
                bool core=frame<2&&ax+ay*.8f<(frame==0?3.5f:2.5f);
                bool rays=frame<2&&(ay<1f&&ax<5f+frame*3f||ax<1f&&ay<4f+frame*2f);
                bool chips=frame>0&&ax>3f+frame&&ax<6f+frame&&
                    Mathf.Abs(ay-ax*.65f)<1f;
                if(!core&&!rays&&!chips)continue;
                byte alpha=frame<2?(byte)255:frame==2?(byte)140:(byte)55;
                pixels[y*size+x]=core?new Color32(255,244,204,alpha):
                    new Color32(232,174,93,alpha);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {name="ContactImpact_"+frame,filterMode=FilterMode.Point,
                 wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            result[frame]=Sprite.Create(texture,new Rect(0f,0f,size,size),Vector2.one*.5f,24f);
            result[frame].name=texture.name;result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    public static float RaptorBiteLeadTime(
        float attackHitTime,float playbackSpeed,bool apex=false)
    {
        float effectSpeed=(apex?RaptorApexBiteFramesPerSecond:RaptorBiteFramesPerSecond)*
            Mathf.Max(0.01f,playbackSpeed);
        int impactFrame=apex?RaptorApexBiteImpactFrame:RaptorBiteImpactFrame;
        // The normal bite closes on playback [0,1,2,1,0]'s middle frame.
        if(!apex&&CombatReadabilityPresentation.Adjusted)impactFrame=2;
        return Mathf.Max(0f,attackHitTime-impactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayRaptorBite(
        Transform anchor,
        SpriteRenderer anchorRenderer,
        float playbackSpeed,bool apex=false)
    {
        if(apex)
        {
            if(raptorApexBiteFrames==null)raptorApexBiteFrames=CreateRaptorApexBiteFrames();
            float scale=anchorRenderer==null?1.35f:
                Mathf.Max(1.05f,Mathf.Max(AuthoredBodySize(anchorRenderer).x,
                    AuthoredBodySize(anchorRenderer).y)*1.05f);
            return Play("Raptor Apex Crushing Bite",raptorApexBiteFrames,
                RaptorApexBiteFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
                anchor,anchorRenderer,scale,30);
        }
        if(raptorBitePlaybackFrames==null)
        {
            Sprite[] source=LoadFrames(RaptorBiteResourcePath);
            if(source.Length<3)return null;
            raptorBitePlaybackFrames=new[]{source[0],source[1],source[2],source[1],source[0]};
        }
        return Play(
            "Raptor Bite Effect",
            raptorBitePlaybackFrames,
            RaptorBiteFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,
            anchorRenderer,
            0.9f,
            20);
    }

    public static PixelSpiritSanctuaryEffect PlayRaptorApexActivation(
        Transform anchor,SpriteRenderer sortingReference)
    {
        if(anchor==null)return null;
        float radius=sortingReference==null?0.9f:
            Mathf.Max(0.7f,sortingReference.bounds.size.x*0.65f);
        return PlayGroundShockwave("Raptor Apex Awakening",anchor.position,radius,
            sortingReference,new Color(0.84f,0.9f,0.82f,0.65f),0.18f);
    }

    public static Texture2D RaptorApexFangIcon
    {
        get
        {
            if(raptorApexFangIcon!=null)return raptorApexFangIcon;
            Color[] pixels=new Color[16*16];
            for(int fang=0;fang<2;fang++)
            for(int depth=0;depth<10;depth++)
            for(int width=0;width<(depth<3?3:depth<7?2:1);width++)
            {
                int x=fang==0?3+depth/4+width:12-depth/4-width;
                pixels[(12-depth)*16+x]=width==0&&depth<7
                    ?new Color(0.53f,0.64f,0.64f):new Color(1f,0.96f,0.8f);
            }
            raptorApexFangIcon=new Texture2D(16,16,TextureFormat.RGBA32,false)
            {
                name="Apex Predator Ivory Fangs",filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave
            };
            raptorApexFangIcon.SetPixels(pixels);raptorApexFangIcon.Apply(false,true);
            return raptorApexFangIcon;
        }
    }

    private static Sprite[] CreateRaptorApexBiteFrames()
    {
        const int size=48;
        int[] gaps={10,7,0,0,0,0,0,0};
        Sprite[] result=new Sprite[gaps.Length];
        Color shade=new Color(0.43f,0.57f,0.6f);
        Color ivory=new Color(1f,0.97f,0.84f);
        for(int frame=0;frame<result.Length;frame++)
        {
            // Hold the crushed pose for three frames: visual hit-stop, not a combat pause.
            if(frame>RaptorApexBiteImpactFrame&&frame<=4)
            {
                result[frame]=result[RaptorApexBiteImpactFrame];
                continue;
            }
            Color[] pixels=new Color[size*size];
            // Separate curved fang strokes, without a solid jaw/gum outline.
            for(int fang=0;fang<6;fang++)
            {
                bool upper=fang<4;
                int sign=upper?1:-1;
                int startX=fang==0?12:fang==1?21:fang==2?27:fang==3?36:
                    fang==4?18:30;
                int inward=startX<24?1:-1;
                bool canine=fang==0||fang>=3;
                int length=canine?10:5;
                int tail=frame<5?0:(frame-4)*3;
                for(int depth=tail;depth<length;depth++)
                {
                    int bend=canine?depth*depth/24:depth/3;
                    int x=startX+inward*bend;
                    int y=24+sign*((canine?8:9)+gaps[frame]-depth);
                    int thickness=canine&&depth<3?3:depth<length-3?2:1;
                    for(int width=0;width<thickness;width++)
                    {
                        pixels[y*size+x+inward*width]=width==0&&thickness>1?shade:ivory;
                    }
                }
                // One brief trailing stroke makes the inward snap readable.
                if(frame==1&&canine)
                    for(int streak=0;streak<5;streak++)
                        pixels[(24+sign*(17+streak))*size+startX]=
                            new Color(shade.r,shade.g,shade.b,0.65f-streak*0.1f);
            }
            if(frame==RaptorApexBiteImpactFrame)
                for(int side=-1;side<=1;side+=2)
                for(int step=0;step<6;step++)
                {
                    pixels[(24+step/2)*size+24+side*(4+step)]=ivory;
                    pixels[(23-step/2)*size+24+side*(4+step)]=ivory;
                }
            float alpha=frame==0?0.8f:frame<=4?1f:(8f-frame)/5f;
            for(int i=0;i<pixels.Length;i++)pixels[i].a*=alpha;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Apex Piercing Fangs "+frame,filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave
            };
            texture.SetPixels(pixels);texture.Apply(false,true);
            result[frame]=Sprite.Create(texture,new Rect(0,0,size,size),
                new Vector2(0.5f,0.5f),size);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    public static PixelCombatEffect PlayCritical(
        Transform anchor,
        SpriteRenderer anchorRenderer)
    {
        return Play(
            "Critical Hit Effect",
            CriticalResourcePath,
            CriticalFramesPerSecond,
            anchor,
            anchorRenderer,
            1.5f,
            50);
    }

    public static PixelCombatEffect PlayWolfHuntImpact(
        Transform target,SpriteRenderer reference,Vector3 direction,Color tint,bool finishing,float strength=1f)
    {
        if(wolfHuntImpactFrames==null)wolfHuntImpactFrames=CreateWolfHuntImpactFrames();
        float size=reference==null?1f:Mathf.Max(AuthoredBodySize(reference).x,AuthoredBodySize(reference).y);
        var effect=Play("Wolf Hunt Contact Flash",wolfHuntImpactFrames,50f,target,reference,
            Mathf.Clamp(size*.72f,.55f,1.6f)*(finishing?1.15f:1f)*strength,26);
        effect.anchor=null;
        Vector3 screen=effect.transform.InverseTransformDirection(direction);
        effect.cameraRotationOffset=Quaternion.Euler(0,0,Mathf.Atan2(screen.y,screen.x)*Mathf.Rad2Deg);
        effect.SetColor(tint);effect.UpdateTransform();
        return effect;
    }

    public static ParticleSystem PlayWolfSpectralImpact(
        Transform target,SpriteRenderer reference,bool crimson,float strength=1f,float radius=0f)
    {
        Color tint=crimson?new Color(1f,.45f,.58f):new Color(.45f,.85f,1f);
        if(wolfSpectralImpactFrames==null)wolfSpectralImpactFrames=CreateWolfSpectralImpactFrames();
        float diameter=reference==null?1f:Mathf.Max(AuthoredBodySize(reference).x,AuthoredBodySize(reference).y);
        var flash=Play("Wolf Spectral Center Flash",wolfSpectralImpactFrames,60f,target,reference,
            Mathf.Clamp(diameter,.9f,1.8f)*strength,40);
        flash.anchor=null;
        var particles=CreateBearSpiritParticles("Wolf Spectral Contact Burst",target,reference,false);
        particles.transform.SetParent(null,true);
        particles.transform.position=flash.transform.position;
        var main=particles.main;
        main.duration=.24f;main.maxParticles=32;main.startSpeed=0f;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.scalingMode=ParticleSystemScalingMode.Shape;
        var emission=particles.emission;emission.enabled=false;
        var shape=particles.shape;shape.enabled=false;
        var size=particles.sizeOverLifetime;size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,1f,1f,.15f));
        var fade=particles.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(1f,.25f),new GradientAlphaKey(0f,1f)});
        fade.color=gradient;
        particles.GetComponent<ParticleSystemRenderer>().sortingOrder=
            (reference==null?0:reference.sortingOrder)+40;
        Vector3 right=particles.transform.right,up=particles.transform.up;
        if(radius>0f){right=Vector3.right;up=Vector3.forward;}
        particles.Play();
        for(int i=0;i<Mathf.Max(8,Mathf.RoundToInt(32*strength));i++)
        {
            float angle=i*2.399963f;
            float variation=(i%7)/6f;
            Vector3 ray=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
            float lifetime=Mathf.Lerp(.12f,.22f,variation);
            float speed=radius>0f?(radius*Mathf.Lerp(.65f,1f,variation)-.06f)/lifetime
                :Mathf.Lerp(6f,10f,variation)*strength;
            particles.Emit(new ParticleSystem.EmitParams
            {
                position=particles.transform.position+ray*.06f,
                velocity=ray*speed,
                startLifetime=lifetime,
                startSize=Mathf.Lerp(.075f,.14f,variation)*strength*PreviewVisualScale,
                startColor=i%3==0?Color.white:tint
            },1);
        }
        if(Application.isPlaying)Destroy(particles.gameObject,.4f);
        return particles;
    }

    private static Sprite[] CreateWolfSpectralImpactFrames()
    {
        const int size=24;
        int[] radii={5,8,6,3};
        var frames=new Sprite[radii.Length];
        for(int frame=0;frame<frames.Length;frame++)
        {
            var pixels=new Color[size*size];
            int radius=radii[frame];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                int dx=Mathf.Abs(x-12),dy=Mathf.Abs(y-12);
                // A filled, compact pixel flash: no rays, slash or hollow ring.
                if(Mathf.Max(dx,dy)>radius||dx+dy>radius*3/2)continue;
                pixels[y*size+x]=Mathf.Max(dx,dy)<radius-1
                    ?new Color(1f,1f,1f,1f-frame*.18f)
                    :new Color(.65f,.9f,1f,.8f-frame*.18f);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {name="Wolf Spectral Center "+frame,filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels(pixels);texture.Apply(false,true);
            frames[frame]=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            frames[frame].name=texture.name;frames[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return frames;
    }

    private static Sprite[] CreateWolfHuntImpactFrames()
    {
        const int size=32;
        var frames=new Sprite[6];
        for(int frame=0;frame<frames.Length;frame++)
        {
            var pixels=new Color[size*size];
            // A compressed white contact, then a few tapered splinters in the travel direction.
            float alpha=frame<2?1f:(6f-frame)/4f;
            for(int ray=0;ray<5;ray++)
            {
                float angle=new[]{0f,.72f,-.85f,2.55f,-2.7f}[ray];
                Vector2 axis=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                float length=ray==0?12f:ray<3?8f:5f;
                float drift=Mathf.Max(0,frame-1)*2f;
                for(int y=2;y<size-2;y++)for(int x=2;x<size-2;x++)
                {
                    Vector2 p=new Vector2(x-16,y-16);
                    float along=Vector2.Dot(p,axis)-drift;
                    float side=Mathf.Abs(p.x*axis.y-p.y*axis.x);
                    float width=(ray==0?2.1f:1.4f)*(1f-along/length);
                    if(along<0||along>length||side>width)continue;
                    pixels[y*size+x]=side<width*.45f
                        ?new Color(1f,1f,.94f,alpha)
                        :new Color(.46f,.83f,.9f,alpha*.65f);
                }
            }
            if(frame<2)for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)
                if(Mathf.Abs(x)+Mathf.Abs(y)<=2)pixels[(16+y)*size+16+x]=Color.white;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {name="Wolf Contact Splinters "+frame,filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels(pixels);texture.Apply(false,true);
            frames[frame]=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            frames[frame].name=texture.name;frames[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return frames;
    }

    public static ParticleSystem PlayWolfAmbushScatter(SpriteRenderer source,Vector3 direction,Color tint)
    {
        var particles=CreateBearSpiritParticles("Wolf Ambush Pixel Scatter",source.transform,source,false);
        var main=particles.main;
        main.duration=WolfCombatRules.HuntScatterDuration;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.scalingMode=ParticleSystemScalingMode.Shape;
        main.startSpeed=0f;main.maxParticles=24;
        var emission=particles.emission;emission.enabled=false;
        var shape=particles.shape;shape.enabled=false;
        var size=particles.sizeOverLifetime;size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,1f,1f,.3f));
        var fade=particles.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(.85f,.45f),new GradientAlphaKey(0f,1f)});
        fade.color=gradient;
        var renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder=source.sortingOrder;
        Vector3 right=source.transform.right,up=source.transform.up;
        Vector3 center=source.transform.TransformPoint(source.sprite.bounds.center+
            Vector3.down*source.sprite.bounds.size.y*.11f);
        particles.Play();
        for(int i=0;i<24;i++)
        {
            float noise=Mathf.Repeat(Mathf.Sin((i+1)*12.9898f)*43758.5453f,1f);
            float height=Mathf.Repeat(Mathf.Sin((i+1)*78.233f)*19341.317f,1f);
            float outward=Mathf.Repeat(Mathf.Sin((i+1)*37.719f)*28731.513f,1f);
            float upward=Mathf.Repeat(Mathf.Sin((i+1)*53.117f)*12345.678f,1f);
            var particle=new ParticleSystem.EmitParams
            {
                position=center+right*((noise-.5f)*source.bounds.size.x*.48f)+
                    up*((height-.5f)*source.bounds.size.y*.14f),
                velocity=direction*(.4f+noise*.55f)+right*((outward-.5f)*2.1f)+
                    up*((upward-.35f)*1.6f),
                startLifetime=.28f+noise*.16f,
                startSize=(.035f+noise*.045f)*PreviewVisualScale,
                startColor=Color.Lerp(tint,Color.white,noise*.6f)
            };
            particles.Emit(particle,1);
        }
        return particles;
    }

    public static SpriteRenderer PlayWolfHuntAfterimage(
        SpriteRenderer source,Vector3 start,Vector3 end,Color color,float duration,
        string objectName="Wolf Hunt Afterimage",float movementDelay=0f,
        Sprite spriteOverride=null)
    {
        if(source==null||(source.sprite==null&&spriteOverride==null))return null;
        Sprite visualSprite=spriteOverride??source.sprite;
        GameObject root=new GameObject(objectName);
        SpriteRenderer ghost=root.AddComponent<SpriteRenderer>();
        ghost.sprite=visualSprite;
        ghost.flipX=source.flipX;
        ghost.flipY=source.flipY;
        ghost.sortingLayerID=source.sortingLayerID;
        ghost.sortingOrder=source.sortingOrder+8;
        ghost.sharedMaterial=source.sharedMaterial;
        GameObject glowObject=new GameObject("Neon Rim");
        glowObject.transform.SetParent(root.transform,false);
        glowObject.transform.localScale=Vector3.one*1.16f;
        SpriteRenderer glow=glowObject.AddComponent<SpriteRenderer>();
        glow.sprite=visualSprite;
        glow.flipX=source.flipX;
        glow.flipY=source.flipY;
        glow.sortingLayerID=source.sortingLayerID;
        glow.sortingOrder=source.sortingOrder+7;
        glow.sharedMaterial=source.sharedMaterial;
        root.transform.rotation=source.transform.rotation;
        root.transform.localScale=source.transform.lossyScale;
        root.AddComponent<PixelWolfHuntAfterimage>().Configure(
            ghost,glow,start,end,color,duration,movementDelay);
        return ghost;
    }

    public static SpriteRenderer PlayWolfSpectralSpirit(
        SpriteRenderer source,Vector3 start,Vector3 end,int spiritIndex,Sprite pose=null)
    {
        Color color=spiritIndex%2==0
            ?new Color(0.34f,0.88f,1f,0.88f)
            :new Color(0.66f,0.36f,1f,0.88f);
        return PlayWolfHuntAfterimage(
            source,start,end,color,WolfCombatRules.SpectralSpiritTravelDuration,
            "Celestial Wolf Spirit "+(spiritIndex+1),0f,pose);
    }

    private static Sprite[] RaptorClawImpactFrames(Color tipColor)
    {
        if(!RaptorImpactFrames.TryGetValue(tipColor,out Sprite[] impactFrames))
        {
            impactFrames=new Sprite[6];
            for(int frame=0;frame<impactFrames.Length;frame++)
            {
                const int size=32;
                Color[] pixels=new Color[size*size];
                const int length=24;
                int tail=frame<2?0:(frame-1)*5;
                float alpha=frame<2?1f:(6f-frame)/4f;
                for(int claw=0;claw<3;claw++)
                for(int step=tail;step<length;step++)
                {
                    int x=2+step;
                    int y=27-claw*6-step/2;
                    int thickness=step>=length-3?1:2;
                    for(int width=0;width<thickness;width++)
                    {
                        Color color=step>=length-5?tipColor:new Color(1f,0.97f,0.85f);
                        color.a=alpha;
                        pixels[(y+width)*size+x]=color;
                    }
                }
                Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {
                    name="Raptor Parallel Claws "+frame,
                    filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,
                    hideFlags=HideFlags.HideAndDontSave
                };
                texture.SetPixels(pixels);texture.Apply(false,true);
                impactFrames[frame]=Sprite.Create(texture,new Rect(0,0,size,size),
                    new Vector2(0.5f,0.5f),size);
                impactFrames[frame].hideFlags=HideFlags.HideAndDontSave;
            }
            RaptorImpactFrames[tipColor]=impactFrames;
        }
        return impactFrames;
    }

    public static PixelCombatEffect PlayRaptorClawImpact(
        Transform anchor,SpriteRenderer sortingReference,Vector3 direction,Color tipColor,int strike=-1)
    {
        Sprite[] impactFrames=RaptorClawImpactFrames(tipColor);
        float sizeInWorld=sortingReference==null?1.4f:
            Mathf.Max(AuthoredBodySize(sortingReference).x,AuthoredBodySize(sortingReference).y)*1.05f;
        PixelCombatEffect effect=Play("Raptor Contact Claws",impactFrames,60f,
            anchor,sortingReference,Mathf.Max(0.8f,sizeInWorld)*
                (strike<0?1f:strike==2?1.25f:0.85f),25);
        Camera camera=Camera.main;
        if(camera!=null)
        {
            Vector3 screenDirection=camera.transform.InverseTransformDirection(direction);
            float angle=Mathf.Atan2(screenDirection.y,screenDirection.x)*Mathf.Rad2Deg;
            if(strike==1)angle+=70f;
            else if(strike==2)angle=-63.435f;
            effect.cameraRotationOffset=Quaternion.Euler(0,0,angle);
            effect.UpdateTransform();
        }
        // Keep the impact in place, including when its victim dies or is captured.
        effect.anchor=null;
        effect.anchorRenderer=null;
        return effect;
    }

    public static float RaptorClawLeadTime(float skillHitTime,float playbackSpeed)
    {
        float effectSpeed=RaptorClawFramesPerSecond*Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,skillHitTime-RaptorClawImpactFrame/effectSpeed);
    }

    public static Texture2D BrokenShieldIcon
    {
        get
        {
            if(brokenShieldIcon!=null)return brokenShieldIcon;
            Color[] pixels=new Color[16*16];
            for(int y=0;y<16;y++)
            for(int x=0;x<16;x++)
                pixels[(15-y)*16+x]=BrokenShieldPixel(x,y);
            brokenShieldIcon=new Texture2D(16,16,TextureFormat.RGBA32,false)
            {
                name="Broken Steel Shield",filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave
            };
            brokenShieldIcon.SetPixels(pixels);
            brokenShieldIcon.Apply(false,true);
            return brokenShieldIcon;
        }
    }

    // One pointed shield silhouette for both the status icon and the breaking plates.
    private static Color BrokenShieldPixel(int x,int y)
    {
        if(y<2||y>12)return Color.clear;
        int halfWidth=y==2?5:y<8?6:13-y;
        int crack=(y/2)%2==0?7:8;
        if(x<8-halfWidth||x>7+halfWidth||x==crack)return Color.clear;
        if(y==2||x==8-halfWidth||x==7+halfWidth||y>=11)
            return new Color(0.88f,0.96f,1f);
        if(y==5&&(x==4||x==11))return Color.white;
        return x<crack?new Color(0.56f,0.72f,0.82f):new Color(0.28f,0.44f,0.57f);
    }

    public static PixelCombatEffect PlayArmorBreak(
        Transform anchor,SpriteRenderer sortingReference)
    {
        if(anchor==null)return null;
        if(armorBreakFrames==null)armorBreakFrames=CreateArmorBreakFrames();
        float scale=sortingReference==null?2.4f:
            Mathf.Max(1.8f,Mathf.Max(AuthoredBodySize(sortingReference).x,
                AuthoredBodySize(sortingReference).y)*2.3f);
        PixelCombatEffect effect=Play("Armor Break Shatter",armorBreakFrames,20f,
            anchor,sortingReference,scale,40);
        // Debris stays at impact even if the victim recoils, moves or despawns.
        effect.anchor=null;
        effect.anchorRenderer=null;
        return effect;
    }

    private static Sprite[] CreateArmorBreakFrames()
    {
        const int size=48;
        Sprite[] result=new Sprite[10];
        for(int frame=0;frame<result.Length;frame++)
        {
            Color[] pixels=new Color[size*size];
            float spread=Mathf.Max(0,frame-1);
            float alpha=frame<6?1f:(10f-frame)/4f;
            for(int y=0;y<16;y++)
            for(int x=0;x<16;x++)
            {
                Color color=BrokenShieldPixel(x,y);
                if(color.a==0f)continue;
                int side=x<((y/2)%2==0?7:8)?-1:1;
                float angle=-side*spread*0.09f;
                float localX=x-(side<0?5f:10f),localY=7.5f-y;
                int px=Mathf.RoundToInt(23.5f+side*(2.5f+spread*1.35f)+
                    localX*Mathf.Cos(angle)-localY*Mathf.Sin(angle));
                int py=Mathf.RoundToInt(25.5f+localX*Mathf.Sin(angle)+
                    localY*Mathf.Cos(angle)+spread*0.7f-spread*spread*0.18f);
                if(frame==0)color=Color.Lerp(color,Color.white,0.65f);
                color.a=alpha;
                if(px>=0&&px<size&&py>=0&&py<size)pixels[py*size+px]=color;
            }
            // Angular chips and short hot sparks, not a soft generic particle cloud.
            for(int chip=0;chip<10&&frame>=1;chip++)
            {
                float angle=chip*Mathf.PI*0.2f+0.18f;
                float radius=6f+spread*(chip%2==0?2.05f:1.5f);
                int cx=24+Mathf.RoundToInt(Mathf.Cos(angle)*radius);
                int cy=26+Mathf.RoundToInt(Mathf.Sin(angle)*radius-spread*spread*0.12f);
                bool spark=chip%3==0;
                if(spark&&frame>5)continue;
                Color color=spark?new Color(1f,0.66f,0.18f,alpha):
                    new Color(0.62f,0.78f,0.88f,alpha);
                for(int dy=0;dy<2;dy++)
                for(int dx=0;dx<(spark?3:2);dx++)
                {
                    int px=cx+dx,py=cy+dy;
                    if(px<0||px>=size||py<0||py>=size||!spark&&dx==1&&dy==1)continue;
                    pixels[py*size+px]=dy==0?new Color(1f,0.97f,0.85f,alpha):color;
                }
            }
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Armor Break Shatter "+frame,filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave
            };
            texture.SetPixels(pixels);texture.Apply(false,true);
            result[frame]=Sprite.Create(texture,new Rect(0,0,size,size),
                new Vector2(0.5f,0.5f),size);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    public static PixelCombatEffect PlayRaptorClaw(
        Transform anchor,
        SpriteRenderer sortingReference,
        float playbackSpeed)
    {
        return Play(
            "Raptor Claw Skill Effect",
            RaptorClawResourcePath,
            RaptorClawFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,
            sortingReference,
            1f,
            25);
    }

    public static float SabertoothClawLeadTime(float attackHitTime,float playbackSpeed)
    {
        float effectSpeed=SabertoothClawFramesPerSecond*Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,attackHitTime-SabertoothClawImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlaySabertoothClaw(
        Transform anchor,
        SpriteRenderer sortingReference,
        float playbackSpeed,
        bool flipX)
    {
        PixelCombatEffect effect=Play(
            "Sabertooth Basic Claw Effect",
            SabertoothClawResourcePath,
            SabertoothClawFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,
            sortingReference,
            0.9f,
            20);
        if(effect!=null)effect.effectRenderer.flipX=flipX;
        return effect;
    }

    public static PixelCombatEffect PlaySabertoothFangLock(
        Transform anchor,
        SpriteRenderer sortingReference,
        bool flipX,
        float scale=1f)
    {
        if(tigerBiteFrames==null)tigerBiteFrames=CreateTigerBiteFrames(false);
        PixelCombatEffect effect=Play(
            "Sabertooth Fang Lock Effect",
            tigerBiteFrames,
            SabertoothFangLockFramesPerSecond,
            anchor,
            sortingReference,
            scale,
            25);
        if(effect!=null)effect.effectRenderer.flipX=flipX;
        return effect;
    }

    public static Texture2D FangLockRootIcon=>CombatHudIconArt.Get(CombatStatusKind.Root);

    public static Texture2D FangLockSlowIcon=>CombatHudIconArt.Get(CombatEffectIconKind.AttackSpeedDown);

    public static Texture2D SilenceIcon=>CombatHudIconArt.Get(CombatStatusKind.Silence);

    public static float FangLockBodyScale(SpriteRenderer reference)=>
        reference==null?1.4f:Mathf.Max(1.1f,
            Mathf.Max(AuthoredBodySize(reference).x,AuthoredBodySize(reference).y)*1.1f);

    public static PixelCombatEffect PlaySabertoothFangBinding(
        Transform anchor,SpriteRenderer reference,CombatStatusState statuses)
    {
        if(anchor==null||!SabertoothCombatRules.HasFangLockDebuff(statuses))return null;
        if(fangLockBindingFrames==null)fangLockBindingFrames=CreateTigerBiteFrames(true);
        float scale=FangLockBodyScale(reference);
        PixelCombatEffect effect=Play("Sabertooth Fang Binding",fangLockBindingFrames,
            30f,anchor,reference,scale,18);
        effect.fangLockStatuses=statuses;
        effect.fangLockBaseScale=effect.transform.localScale.x;
        return effect;
    }

    private static Sprite[] CreateTigerBiteFrames(bool held)
    {
        const int size=48;
        int[] gaps=held?new[]{7,3,0,0,1,0}:new[]{7,3,0,0,1,2,3,4};
        Sprite[] result=new Sprite[gaps.Length];
        for(int frame=0;frame<result.Length;frame++)
        {
            Color32[] pixels=new Color32[size*size];
            byte alpha=held?(byte)(frame<2?230:185):
                (byte)(frame<4?255:Mathf.RoundToInt((8-frame)*51f));
            Color32 stripe=new Color32(57,40,30,alpha);
            Color32 fur=new Color32(215,155,65,alpha);
            Color32 cream=new Color32(255,229,171,alpha);
            int upper=29+gaps[frame],lower=16-gaps[frame];

            // Tiger muzzle: amber striped cheeks, cream whisker pads and a dark nose.
            DrawTigerJawPad(pixels,size,24,upper+2,17,6,fur,stripe);
            for(int side=-1;side<=1;side+=2)
            {
                for(int mark=0;mark<3;mark++)
                for(int step=0;step<4;step++)
                for(int width=0;width<2;width++)
                {
                    int x=24+side*(14-mark*4-step/2);
                    SetPixel(pixels,size,x,upper+6-step-width,stripe);
                }
                DrawTigerJawPad(pixels,size,24+side*7,upper-1,8,4,cream,stripe);
                SetPixel(pixels,size,24+side*10,upper, stripe);
                SetPixel(pixels,size,24+side*7,upper-2,stripe);
                SetPixel(pixels,size,24+side*12,upper-2,stripe);
            }
            for(int row=0;row<3;row++)
            for(int x=21+row;x<=26-row;x++)
                SetPixel(pixels,size,x,upper+2-row,stripe);
            SetPixel(pixels,size,24,upper-1,stripe);
            SetPixel(pixels,size,23,upper-2,stripe);
            SetPixel(pixels,size,25,upper-2,stripe);

            DrawTigerJawPad(pixels,size,24,lower-1,14,4,fur,stripe);
            DrawTigerJawPad(pixels,size,24,lower-2,10,2,cream,stripe);
            // Four curved canines, with smaller incisors between them.
            for(int side=-1;side<=1;side+=2)
            {
                DrawTigerCanine(pixels,size,24+side*11,upper-3,10,-1,-side,alpha);
                DrawTigerCanine(pixels,size,24+side*7,lower+2,6,1,-side,alpha);
            }
            Color32 ivory=new Color32(255,252,225,alpha);
            for(int tooth=0;tooth<4;tooth++)
            for(int depth=0;depth<3;depth++)
            for(int width=0;width<(depth<2?2:1);width++)
                SetPixel(pixels,size,19+tooth*3+width,upper-4-depth,ivory);
            for(int tooth=0;tooth<3;tooth++)
            for(int depth=0;depth<2;depth++)
                SetPixel(pixels,size,21+tooth*3,lower+2+depth,ivory);

            if(!held&&frame>=2&&frame<=3)
                for(int side=-1;side<=1;side+=2)
                for(int ray=0;ray<5;ray++)
                    SetPixel(pixels,size,24+side*(15+ray),23+side*(ray/2),ivory);
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name=(held?"Tiger Jaw Hold ":"Tiger Crushing Bite ")+frame,filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);texture.Apply(false,true);
            result[frame]=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(0.5f,0.5f),size);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static void DrawTigerJawPad(Color32[] pixels,int size,int cx,int cy,
        int rx,int ry,Color32 fill,Color32 edge)
    {
        for(int y=-ry;y<=ry;y++)
        for(int x=-rx;x<=rx;x++)
        {
            float radius=x*x/(float)(rx*rx)+y*y/(float)(ry*ry);
            if(radius<=1f)SetPixel(pixels,size,cx+x,cy+y,radius>0.75f?edge:fill);
        }
    }

    private static void DrawTigerCanine(Color32[] pixels,int size,int x,int y,
        int length,int vertical,int inward,byte alpha)
    {
        Color32 edge=new Color32(132,100,54,alpha);
        Color32 shade=new Color32(220,195,139,alpha);
        Color32 ivory=new Color32(255,252,225,alpha);
        for(int depth=0;depth<length;depth++)
        {
            int tipX=x+inward*(depth*depth/32);
            int width=depth<3?4:depth<length-3?3:1;
            SetPixel(pixels,size,tipX-inward,y+vertical*depth,edge);
            for(int across=0;across<width;across++)
                SetPixel(pixels,size,tipX+inward*across,y+vertical*depth,across==0?shade:ivory);
        }
    }

    public static float CrocodileTailStrikeLeadTime(
        float attackHitTime,float playbackSpeed)
    {
        float effectSpeed=CrocodileTailStrikeFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,attackHitTime-CrocodileTailStrikeImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayCrocodileTailStrike(
        Transform anchor,
        SpriteRenderer sortingReference,
        float playbackSpeed,
        bool flipX)
    {
        PixelCombatEffect effect=Play(
            "Crocodile Tail Strike Effect",
            CrocodileTailStrikeResourcePath,
            CrocodileTailStrikeFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,
            sortingReference,
            1f,
            20);
        if(effect!=null)effect.effectRenderer.flipX=flipX;
        return effect;
    }

    public static float CrocodileDeathRollLeadTime(
        float hitTime,float playbackSpeed)
    {
        float effectSpeed=CrocodileDeathRollFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,hitTime-CrocodileDeathRollImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayCrocodileDeathRoll(
        Transform anchor,
        SpriteRenderer sortingReference,
        float playbackSpeed,
        bool reverse=false,bool transcended=false)
    {
        PixelCombatEffect effect=Play(
            "Crocodile Death Roll Effect",
            CrocodileDeathRollResourcePath,
            CrocodileDeathRollFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,
            sortingReference,
            1.15f,
            25);
        if(effect!=null)
        {
            effect.effectRenderer.flipX=reverse;
            if(transcended)effect.SetColor(Color.Lerp(CrocodileAbyssColor,Color.white,0.45f));
        }
        return effect;
    }

    public static readonly Color CrocodileAbyssColor=new Color(0.12f,0.65f,0.55f);
    public static readonly Color CrocodileAbyssHighlight=new Color(0.98f,0.86f,0.5f);

    public static ParticleSystem PlayCrocodileCrushingRollChips(
        Vector3 center,float radius,SpriteRenderer reference,bool empowered=false)
    {
        var chips=PlayGroundImpactBurst("Crocodile Crushing Roll Chips",center+Vector3.up*0.5f,
            radius,reference,empowered?CrocodileAbyssColor:new Color(0.38f,0.48f,0.18f),
            empowered?CrocodileAbyssHighlight:new Color(0.95f,0.9f,0.6f),0.3f,0.2f);
        chips.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var shape=chips.shape;shape.radiusThickness=0.12f;
        var main=chips.main;main.startSize=new ParticleSystem.MinMaxCurve(0.06f,0.1f);PixelCombatEffect.ScaleParticleSize(main);
        var emission=chips.emission;emission.SetBursts(new[]{new ParticleSystem.Burst(0f,12)});
        chips.Play();return chips;
    }

    public static PixelCombatEffect PlayCrocodileAbsoluteJaw(
        Transform anchor,SpriteRenderer sortingReference,bool transcended=false)
    {
        PixelCombatEffect effect=PlayTyrannoKingJawBite(
            anchor,sortingReference,0.75f,transcended?1.9f:1.75f,
            transcended?Color.Lerp(CrocodileAbyssColor,Color.white,0.6f):new Color(1f,0.72f,0.32f));
        if(effect!=null)
        {
            effect.gameObject.name="Crocodile Absolute Jaw Crush";
            // The owning roll has already braced. Close on the damage frame.
            effect.elapsed=TyrannoKingJawBiteImpactFrame/effect.framesPerSecond;
            effect.effectRenderer.sprite=effect.frames[TyrannoKingJawBiteImpactFrame];
        }
        return effect;
    }

    public static PixelCombatEffect PlayCrocodileJawCrush(
        Transform anchor,SpriteRenderer sortingReference)
    {
        PixelCombatEffect effect=PlayTyrannoKingJawBite(
            anchor,sortingReference,1f,1.5f,new Color(0.82f,1f,0.25f));
        if(effect!=null)
        {
            effect.name="Crocodile Jaw Crush";
            effect.elapsed=TyrannoKingJawBiteImpactFrame/effect.framesPerSecond;
            effect.effectRenderer.sprite=effect.frames[TyrannoKingJawBiteImpactFrame];
        }
        return effect;
    }

    public static float KingKongHeavyPunchLeadTime(
        float attackHitTime,float playbackSpeed)
    {
        float effectSpeed=KingKongHeavyPunchFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,attackHitTime-KingKongHeavyPunchImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayKingKongHeavyPunch(
        Transform anchor,SpriteRenderer sortingReference,float playbackSpeed)
    {
        return Play(
            "KingKong Heavy Punch Effect",
            KingKongHeavyPunchResourcePath,
            KingKongHeavyPunchFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,sortingReference,1.15f,20);
    }

    public static float KingKongEarthPulseLeadTime(
        float skillHitTime,float playbackSpeed)
    {
        float effectSpeed=KingKongEarthPulseFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,skillHitTime-KingKongEarthPulseImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayKingKongEarthPulse(
        Vector3 position,SpriteRenderer sortingReference,float playbackSpeed,
        float diameter)
    {
        PixelCombatEffect effect=Play(
            "KingKong Earth Pulse Effect",KingKongEarthPulseResourcePath,
            KingKongEarthPulseFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            null,sortingReference,diameter,-20);
        if(effect==null)return null;
        effect.faceCamera=false;
        effect.transform.position=position+Vector3.up*0.015f;
        effect.transform.rotation=Quaternion.Euler(90f,0f,0f);
        return effect;
    }

    public static float BearPawSwipeLeadTime(
        float attackHitTime,float playbackSpeed)
    {
        float effectSpeed=BearPawSwipeFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,attackHitTime-BearPawSwipeImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayBearPawSwipe(
        Transform anchor,SpriteRenderer sortingReference,float playbackSpeed,bool flipX,bool empowered=false)
    {
        if(empowered)
        {
            if(bearEmpoweredPawFrames==null)bearEmpoweredPawFrames=CreateBearEmpoweredPawFrames();
            var swipe=Play("Bear Empowered Paw Sweep",bearEmpoweredPawFrames,
                BearPawSwipeFramesPerSecond*Mathf.Max(.01f,playbackSpeed),anchor,sortingReference,1.35f,20);
            if(swipe!=null)swipe.effectRenderer.flipX=flipX;
            return swipe;
        }
        PixelCombatEffect effect=Play(
            "Bear Paw Swipe Effect",BearPawSwipeResourcePath,
            BearPawSwipeFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,sortingReference,0.95f,20);
        if(effect!=null)effect.effectRenderer.flipX=flipX;
        return effect;
    }

    public static PixelCombatEffect PlayBearMountainCrescent(
        SpriteRenderer source,Vector3 target,float playbackSpeed)
    {
        if(source==null||source.sprite==null)return null;
        if(bearMountainCrescentFrames==null)
            bearMountainCrescentFrames=CreateBearMountainCrescentFrames();
        Vector3 direction=target-source.bounds.center;
        float radius=Mathf.Max(direction.magnitude*.9f,AuthoredBodySize(source).y*.9f);
        var effect=Play("Bear Mountain Crescent",bearMountainCrescentFrames,
            BearPawSwipeFramesPerSecond*Mathf.Max(.01f,playbackSpeed),
            source.transform,source,radius*32f/52f,14);
        Quaternion view=effect.effectCamera!=null?effect.effectCamera.transform.rotation:source.transform.rotation;
        Vector3 screenDirection=Quaternion.Inverse(view)*direction;
        effect.cameraRotationOffset=Quaternion.Euler(0f,0f,
            Mathf.Atan2(screenDirection.y,screenDirection.x)*Mathf.Rad2Deg-12f);
        effect.UpdateTransform();
        return effect;
    }

    public static void PlayEightfoldContact(SpriteRenderer target,Color color,bool final)
    {
        if(target==null)return;
        var effect=Play("Eightfold Silk Contact",PixelEightfoldWeb.ContactFrames,final?24f:36f,
            target.transform,target,Mathf.Max(.6f,AuthoredBodySize(target).y)*(final?1.4f:.9f),4);
        if(effect!=null)effect.SetColor(color);
    }

    public static PixelCombatEffect PlayBearCleaveHit(Transform anchor,SpriteRenderer reference,bool heavy=false)
    {
        if(bearRagingStrikeFrames==null)bearRagingStrikeFrames=CreateBearRagingStrikeFrames();
        if(heavy)
        {
            if(bearMountainHitFrames==null)bearMountainHitFrames=bearRagingStrikeFrames[2].Skip(2).ToArray();
            return Play("Bear Mountain Claw Hit",bearMountainHitFrames,24f,anchor,reference,1.05f,20);
        }
        if(bearCleaveHitFrames==null)bearCleaveHitFrames=bearRagingStrikeFrames[0].Skip(2).ToArray();
        return Play("Bear Spirit Cleave Hit",bearCleaveHitFrames,20f,anchor,reference,.55f,20);
    }

    public static PixelCombatEffect PlayBearRagingStrike(
        Transform anchor,SpriteRenderer sortingReference,int strikeIndex,bool flipX)
    {
        if(bearPawImpactFrames==null)bearPawImpactFrames=CreateBearPawImpactFrames();
        int index=Mathf.Clamp(strikeIndex,0,2);
        PixelCombatEffect effect=Play(
            $"Bear Raging Strike {index+1}",bearPawImpactFrames,index==2?32f:56f,
            anchor,sortingReference,index==2?1.4f:.8f,24);
        if(effect!=null)effect.effectRenderer.flipX=flipX^(index==1);
        return effect;
    }

    public static float NinjaKunaiSlashLeadTime(
        float attackHitTime,float playbackSpeed)
    {
        float effectSpeed=NinjaKunaiSlashFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,attackHitTime-NinjaKunaiSlashImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayNinjaKunaiSlash(
        Transform anchor,SpriteRenderer sortingReference,float playbackSpeed,
        bool flipX,float scale=1f)
    {
        if(ninjaKunaiSlashFrames==null)
            ninjaKunaiSlashFrames=CreateNinjaKunaiSlashFrames();
        PixelCombatEffect effect=Play(
            "Ninja Kunai Slash Effect",ninjaKunaiSlashFrames,
            NinjaKunaiSlashFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,sortingReference,scale,25);
        if(effect!=null)effect.effectRenderer.flipX=flipX;
        return effect;
    }

    public static float TyrannoKingJawBiteLeadTime(
        float attackHitTime,float playbackSpeed)
    {
        float effectSpeed=TyrannoKingJawBiteFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,attackHitTime-TyrannoKingJawBiteImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayTyrannoKingJawBite(
        Transform anchor,SpriteRenderer sortingReference,float playbackSpeed,
        float scale=1.2f,Color? tint=null)
    {
        if(tyrannoKingJawBiteFrames==null)
            tyrannoKingJawBiteFrames=CreateTyrannoKingJawBiteFrames();
        PixelCombatEffect effect=Play(
            "Tyranno King Jaw Bite Effect",tyrannoKingJawBiteFrames,
            TyrannoKingJawBiteFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,sortingReference,scale,24);
        if(effect!=null&&tint.HasValue)effect.SetColor(tint.Value);
        return effect;
    }

    public static float MantisScytheSlashLeadTime(
        float attackHitTime,float playbackSpeed)
    {
        float effectSpeed=MantisScytheSlashFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,attackHitTime-MantisScytheSlashImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayMantisScytheSlash(
        Transform anchor,SpriteRenderer sortingReference,float playbackSpeed,bool flipX,
        float scale=1.05f,Color? tint=null)
    {
        if(mantisScytheSlashFrames==null)
            mantisScytheSlashFrames=CreateMantisScytheSlashFrames();
        PixelCombatEffect effect=Play(
            "Mantis Scythe Slash Effect",mantisScytheSlashFrames,
            MantisScytheSlashFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,sortingReference,scale,24);
        if(effect!=null)
        {
            effect.SetColor(tint??new Color(0.62f,1f,0.3f));
            effect.effectRenderer.flipX=flipX;
        }
        return effect;
    }

    public static void PlayMantisPreciseCross(
        Transform anchor,SpriteRenderer sortingReference)
    {
        PlayMantisScytheSlash(anchor,sortingReference,1.3f,false,1.32f,
            new Color(1f,0.96f,0.72f));
        PlayMantisScytheSlash(anchor,sortingReference,1.3f,true,1.32f,
            new Color(1f,0.2f,0.12f));
    }

    public static void PlayMantisCutImpact(Transform anchor,SpriteRenderer reference,Color color,bool second)
    {
        if(anchor==null)return;
        var cut=PlayMantisScytheSlash(anchor,reference,2.5f,second,second?.82f:.65f,
            Color.Lerp(color,Color.white,second?.8f:.55f));
        if(cut!=null)cut.effectRenderer.transform.localScale=new Vector3(.48f,1f,1f)*PreviewVisualScale;
        Vector3 center=reference==null?anchor.position+Vector3.up*.4f:reference.bounds.center;
        var shards=PixelStarProjectile.PlaySpiritLeafImpact(center,reference,color,
            "Mantis Cut Fragments",2f,4f,.1f,0f,1f,1);
        shards.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=shards.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.1f,.18f);
        main.startSize=new ParticleSystem.MinMaxCurve(.025f,.055f);PixelCombatEffect.ScaleParticleSize(main);
        shards.Emit(second?9:6);shards.Play();
    }

    public static void PlayMantisWindup(SpriteRenderer reference)
    {
        if(reference==null)return;
        var particles=PixelStarProjectile.PlaySpiritLeafImpact(reference.bounds.center,reference,
            new Color(.88f,1f,.65f),"Mantis Blade Tips",0f,0f,0f,0f,.5f,1);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        Vector3 right=Camera.main==null?Vector3.right:Camera.main.transform.right;
        for(int i=-1;i<=1;i+=2)
            particles.Emit(new ParticleSystem.EmitParams{
                position=reference.bounds.center+right*(reference.bounds.extents.x*.55f*i),
                velocity=Vector3.zero,startSize=.08f*PreviewVisualScale,startLifetime=MantisCombatRules.WindupDuration,
                startColor=new Color(.94f,1f,.8f)},1);
        particles.Play();
    }

    public static float MammothTuskRamLeadTime(
        float attackHitTime,float playbackSpeed)
    {
        float effectSpeed=MammothTuskRamFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,attackHitTime-MammothTuskRamImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayMammothTuskRam(
        Transform anchor,SpriteRenderer sortingReference,float playbackSpeed,bool flipX,
        Color? tint=null,float scale=1.15f)
    {
        if(mammothTuskRamFrames==null)
            mammothTuskRamFrames=CreateMammothTuskRamFrames();
        PixelCombatEffect effect=Play(
            "Mammoth Tusk Ram Effect",mammothTuskRamFrames,
            MammothTuskRamFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,sortingReference,scale,26);
        if(effect!=null)
        {
            effect.effectRenderer.flipX=flipX;
            if(tint.HasValue)effect.SetColor(tint.Value);
        }
        return effect;
    }

    public static ParticleSystem PlayMammothTuskDust(
        Transform anchor,SpriteRenderer sortingReference)
    {
        if(anchor==null)return null;
        GameObject root=new GameObject("Mammoth Tusk Ground Dust");
        root.transform.position=sortingReference!=null&&sortingReference.sprite!=null
            ?anchor.TransformPoint(sortingReference.sprite.bounds.center-
                Vector3.up*sortingReference.sprite.bounds.extents.y*0.58f)
            :anchor.position+Vector3.up*0.12f;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.12f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.18f,0.34f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.32f,0.78f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.035f,0.075f);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
        main.startColor=new ParticleSystem.MinMaxGradient(
            MeadowAmbientParticles.TerrainColor(new Color(0.42f,0.25f,0.11f)),MeadowAmbientParticles.TerrainColor(new Color(0.88f,0.7f,0.4f)));
        main.gravityModifier=0.55f;
        main.maxParticles=12;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.enabled=true;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=0.06f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+28;
        particles.Emit(9);
        particles.Play();
        if(Application.isPlaying)Destroy(root,0.8f);
        return particles;
    }

    public static float BeetleHornUppercutLeadTime(
        float hitTime,float playbackSpeed)
    {
        float effectSpeed=BeetleHornUppercutFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,hitTime-BeetleHornUppercutImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayBeetleHornUppercut(
        Transform anchor,SpriteRenderer sortingReference,float playbackSpeed,
        bool flipX,float scale=1f)
    {
        if(beetleHornUppercutFrames==null)
            beetleHornUppercutFrames=CreateBeetleHornUppercutFrames();
        PixelCombatEffect effect=Play(
            "Beetle Horn Uppercut Effect",beetleHornUppercutFrames,
            BeetleHornUppercutFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,sortingReference,scale,26);
        if(effect!=null)effect.effectRenderer.flipX=flipX;
        return effect;
    }

    public static float BeetleHornThrustLeadTime(
        float hitTime,float playbackSpeed)
    {
        float effectSpeed=BeetleHornThrustFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,hitTime-BeetleHornThrustImpactFrame/effectSpeed);
    }

    public static PixelCombatEffect PlayBeetleHornThrust(
        Transform anchor,SpriteRenderer sortingReference,float playbackSpeed,
        bool flipX)
    {
        if(beetleHornThrustFrames==null)
            beetleHornThrustFrames=CreateBeetleHornThrustFrames();
        PixelCombatEffect effect=Play(
            "Beetle Horn Thrust Effect",beetleHornThrustFrames,
            BeetleHornThrustFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,sortingReference,1f,26);
        if(effect!=null)effect.effectRenderer.flipX=flipX;
        return effect;
    }

    public static ParticleSystem PlayBeetleHornImpact(
        Transform anchor,SpriteRenderer sortingReference)
    {
        if(anchor==null)return null;
        GameObject root=new GameObject("Beetle Horn Impact Sparks And Dust");
        root.transform.position=sortingReference!=null&&sortingReference.sprite!=null
            ?anchor.TransformPoint(sortingReference.sprite.bounds.center-
                Vector3.up*sortingReference.sprite.bounds.extents.y*0.42f)
            :anchor.position+Vector3.up*0.16f;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.1f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.16f,0.3f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.45f,1.05f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.035f,0.08f);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(1f,0.5f,0.06f),new Color(0.48f,0.27f,0.1f));
        main.gravityModifier=0.5f;
        main.maxParticles=14;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.enabled=true;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=0.07f;
        ParticleSystem.VelocityOverLifetimeModule velocity=particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=0.5f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+30;
        particles.Emit(11);
        particles.Play();
        if(Application.isPlaying)Destroy(root,0.8f);
        return particles;
    }

    public static PixelSpiritSanctuaryEffect PlayBeetleHornShockwave(
        Vector3 position,float radius,SpriteRenderer sortingReference)
    {
        return PlayGroundShockwave("Jangpung Horn Shockwave",position,radius,
            sortingReference,new Color(1f,0.5f,0.06f,0.92f),0.22f);
    }

    public static ParticleSystem PlayCrocodilePreyCannonImpact(
        Vector3 position,float radius,SpriteRenderer sortingReference,bool transcended=false)
    {
        var debris=PlayLandingDebris("Crocodile Prey Cannon Debris",position,radius,sortingReference);
        if(transcended)
        {
            debris.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=debris.main;
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(0.48f,0.4f,0.22f),CrocodileAbyssHighlight);
            debris.Play();
        }
        return debris;
    }

    public static PixelCombatEffect PlayBearRagingHundredStrikesShockwave(
        Vector3 position,float radius,SpriteRenderer sortingReference)
    {
        if(bearRagingGroundFractureFrames==null)
            bearRagingGroundFractureFrames=CreateBearRagingGroundFractureFrames();
        float diameter=radius*2f;
        float spriteWidth=bearRagingGroundFractureFrames[0].bounds.size.x;
        PixelCombatEffect effect=Play(
            "Bear Raging Paw Fracture",bearRagingGroundFractureFrames,14f,
            null,sortingReference,diameter/Mathf.Max(0.01f,spriteWidth),-54);
        if(effect==null)return null;
        effect.faceCamera=false;
        effect.transform.position=position+Vector3.up*0.026f;
        effect.transform.rotation=Quaternion.Euler(90f,0f,0f);
        return effect;
    }

    public static PixelCombatEffect PlayBearKingRoarBurst(
        Vector3 position,float radius,SpriteRenderer sortingReference,bool ending)
    {
        if(bearKingRoarBurstFrames==null)
            bearKingRoarBurstFrames=new[]{CreateBearKingRoarBurstFrames(false),
                CreateBearKingRoarBurstFrames(true)};
        PixelCombatEffect effect=Play(ending?"Bear King Final Roar":"Bear King Opening Roar",
            bearKingRoarBurstFrames[ending?1:0],24f,null,sortingReference,1f,0);
        if(effect==null)return null;
        effect.faceCamera=false;
        effect.transform.position=position;
        (effect.GetComponent<PixelBearRoarPressure>()??
            effect.gameObject.AddComponent<PixelBearRoarPressure>()).Configure(
                effect.GetComponent<SpriteRenderer>(),position,radius,sortingReference,ending);
        return effect;
    }

    public static PixelCombatEffect PlayBearRoarHit(
        Transform anchor,SpriteRenderer reference,bool ending)
    {
        if(bearPawImpactFrames==null)bearPawImpactFrames=CreateBearPawImpactFrames();
        return Play("Bear Roar Contact",bearPawImpactFrames,
            48f,anchor,reference,ending?.9f:.6f,2);
    }

    public static PixelCombatEffect PlayBearAvatarRoar(
        Vector3 position,float radius,SpriteRenderer reference)
    {
        if(bearAvatarRoarFrames==null)bearAvatarRoarFrames=CreateBearKingRoarBurstFrames(true,true);
        PixelCombatEffect effect=Play("Bear Avatar Global Fear Roar",bearAvatarRoarFrames,
            14f,null,reference,1f,0);
        if(effect==null)return null;
        effect.faceCamera=false;
        effect.transform.position=position;
        (effect.GetComponent<PixelBearRoarPressure>()??
            effect.gameObject.AddComponent<PixelBearRoarPressure>()).Configure(
                effect.GetComponent<SpriteRenderer>(),position,radius,reference,true,true);
        return effect;
    }

    public void CancelBearRoar(){if(this!=null)Release();}

    private static PixelSpiritSanctuaryEffect PlayGroundShockwave(
        string objectName,Vector3 position,float radius,
        SpriteRenderer sortingReference,Color color,float duration)
    {
        GameObject root=new GameObject(objectName);
        root.transform.position=position+Vector3.up*0.03f;
        root.transform.rotation=Quaternion.Euler(90f,0f,0f);
        SpriteRenderer ground=root.AddComponent<SpriteRenderer>();
        ground.sprite=BeetleHornShockwaveSprite;
        ground.color=color;
        ground.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        // The battlefield backdrop is a transparent sprite at order 0.
        ground.sortingOrder=Mathf.Max(1,(sortingReference==null?10:sortingReference.sortingOrder)-1);
        float scale=radius*2f/Mathf.Max(0.01f,ground.sprite.bounds.size.x);
        PixelSpiritSanctuaryEffect effect=
            root.AddComponent<PixelSpiritSanctuaryEffect>();
        effect.Configure(ground,null,scale,duration);
        return effect;
    }

    public static ParticleSystem PlayBeetleHornDebris(
        Vector3 position,float radius,SpriteRenderer sortingReference)
    {
        return PlayGroundImpactBurst(
            "Jangpung Horn Ground Debris",position,radius,sortingReference,
            MeadowAmbientParticles.TerrainColor(new Color(0.38f,0.2f,0.06f)),MeadowAmbientParticles.TerrainColor(new Color(1f,0.58f,0.08f)),0.8f,0.52f);
    }

    public static ParticleSystem PlayBeetleSolarBurst(
        Vector3 position,float radius,SpriteRenderer sortingReference)
    {
        return PlayGroundImpactBurst(
            "Solarbeetle Sun Horn Burst",position,radius,sortingReference,
            new Color(1f,0.28f,0.025f),new Color(1f,0.94f,0.26f),0.18f,0.7f);
    }

    public static GameObject PlayBeetleLightningArc(
        Vector3 start,Vector3 end,SpriteRenderer sortingReference,
        string objectName="Noepung Jagged Chain Lightning",
        float lifetime=0.22f,float thicknessScale=1f)
    {
        float length=(end-start).magnitude;
        if(length<0.001f)return null;
        GameObject root=new GameObject(objectName);
        Vector3 side=Camera.main==null?Vector3.right:Camera.main.transform.right;
        int segmentCount=Mathf.Clamp(Mathf.CeilToInt(length/0.45f),4,8);
        float jitter=Mathf.Min(0.18f,length*0.09f);
        Vector3 previous=start;
        for(int i=1;i<=segmentCount;i++)
        {
            float progress=i/(float)segmentCount;
            Vector3 point=Vector3.Lerp(start,end,progress);
            if(i<segmentCount)
            {
                float taper=0.65f+Mathf.Sin(progress*Mathf.PI)*0.35f;
                point+=side*((i%2==0?-1f:1f)*jitter*taper);
            }
            float delay=(i-1)*0.012f;
            AddBeetleLightningSegment(root.transform,previous,point,
                sortingReference,new Color(0.1f,0.4f,1f),
                1.9f*thicknessScale,delay,lifetime);
            AddBeetleLightningSegment(root.transform,previous,point,
                sortingReference,new Color(0.88f,0.98f,1f),
                0.72f*thicknessScale,delay,lifetime);
            if(length>1.5f&&i<segmentCount&&(i==2||i==4))
            {
                Vector3 branchEnd=point+side*(i==2?-0.42f:0.36f)+
                                  Vector3.down*0.18f;
                AddBeetleLightningSegment(root.transform,point,branchEnd,
                    sortingReference,new Color(0.5f,0.9f,1f),
                    0.52f*thicknessScale,delay+0.018f,lifetime);
            }
            previous=point;
        }
        if(Application.isPlaying)Destroy(root,Mathf.Max(0.45f,lifetime+0.3f));
        return root;
    }

    private static void AddBeetleLightningSegment(
        Transform parent,Vector3 start,Vector3 end,
        SpriteRenderer sortingReference,Color color,float thickness,float delay,
        float lifetime)
    {
        GameObject segment=new GameObject("Lightning Segment");
        segment.transform.SetParent(parent,true);
        segment.AddComponent<PixelNinjaLineSlash>().Configure(
            NinjaLinePixelSprite,start,end,sortingReference,lifetime,color,
            thickness,delay);
    }

    public static ParticleSystem PlayBeetleThunderImpact(
        Vector3 position,float radius,SpriteRenderer sortingReference)
    {
        return PlayGroundImpactBurst(
            "Noepung Thunder Ground Impact",position,radius,sortingReference,
            new Color(0.12f,0.5f,1f),new Color(0.92f,1f,1f),0.12f,0.82f);
    }

    public static SpriteRenderer PlayBeetleFortressAura(
        Transform anchor,SpriteRenderer sortingReference,float duration,
        string objectName="Atlagon Fortress Carapace Aura",Color? color=null)
    {
        if(anchor==null)return null;
        GameObject root=new GameObject(objectName);
        root.AddComponent<CombatEffectEndMarker>();
        root.transform.SetParent(anchor,false);
        root.transform.localPosition=sortingReference!=null&&sortingReference.sprite!=null
            ?sortingReference.sprite.bounds.center:Vector3.up*0.5f;
        SpriteRenderer aura=root.AddComponent<SpriteRenderer>();
        aura.sprite=BeetleFortressAuraSprite;
        aura.color=color??new Color(0.62f,0.86f,1f,0.72f);
        aura.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        aura.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+27;
        if(sortingReference!=null&&sortingReference.sprite!=null)
        {
            Vector2 target=sortingReference.sprite.bounds.size;
            Vector2 shield=aura.sprite.bounds.size;
            root.transform.localScale=new Vector3(
                target.x*0.78f/shield.x,target.y*0.72f/shield.y,1f);
        }
        if(Application.isPlaying)Destroy(root,Mathf.Max(0.1f,duration));
        return aura;
    }

    public static float GriffinEagleClawLeadTime(
        float hitTime,float playbackSpeed)
    {
        float effectSpeed=GriffinEagleClawFramesPerSecond*
                          Mathf.Max(0.01f,playbackSpeed);
        return Mathf.Max(0f,hitTime-GriffinEagleClawImpactFrame/effectSpeed);
    }

    public static int GriffinEagleClawY(int step)=>
        Mathf.RoundToInt(27f-step*0.72f);

    public static PixelCombatEffect PlayGriffinEagleClaw(
        Transform anchor,SpriteRenderer sortingReference,float playbackSpeed,
        bool flipX,float scale=1.05f)
    {
        if(griffinEagleClawFrames==null)
            griffinEagleClawFrames=CreateGriffinEagleClawFrames();
        PixelCombatEffect effect=Play(
            "Griffin Eagle Claw Effect",griffinEagleClawFrames,
            GriffinEagleClawFramesPerSecond*Mathf.Max(0.01f,playbackSpeed),
            anchor,sortingReference,Mathf.Max(0.1f,scale),26);
        if(effect!=null)effect.effectRenderer.flipX=flipX;
        return effect;
    }

    public static ParticleSystem PlayGriffinFeatherImpact(
        Transform anchor,SpriteRenderer sortingReference)
    {
        if(anchor==null)return null;
        GameObject root=new GameObject("Griffin Golden Feather Impact");
        root.transform.position=sortingReference!=null&&sortingReference.sprite!=null
            ?anchor.TransformPoint(sortingReference.sprite.bounds.center)
            :anchor.position+Vector3.up*0.35f;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.12f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.22f,0.4f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.45f,1.05f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.035f,0.075f);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(1f,0.72f,0.15f),new Color(1f,0.96f,0.62f));
        main.gravityModifier=0.22f;
        main.maxParticles=12;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=0.07f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+30;
        particles.Emit(8);
        particles.Play();
        if(Application.isPlaying)Destroy(root,0.9f);
        return particles;
    }

    public static ParticleSystem PlayBleedBurst(
        Transform anchor,SpriteRenderer sortingReference)
    {
        return PlayStatusTickBurst(
            "Bleed Tick Burst",anchor,sortingReference,
            new Color(0.95f,0.02f,0.03f),new Color(0.42f,0.005f,0.01f),
            0.35f,0.65f,10,ParticleSystemShapeType.Circle,false);
    }

    public static ParticleSystem PlayPoisonBurst(
        Transform anchor,SpriteRenderer sortingReference)
    {
        return PlayStatusTickBurst(
            "Poison Bubbles",anchor,sortingReference,
            new Color(0.28f,1f,0.12f,0.82f),new Color(0.62f,0.08f,0.82f,0.82f),
            0f,0f,12,ParticleSystemShapeType.Circle,true,1.88f);
    }

    public static ParticleSystem PlayNinjaPoisonMist(
        Transform anchor,SpriteRenderer sortingReference,float duration,
        string objectName="Ninja Poison Mist")
    {
        return PlayFloatingStatusMotes(
            objectName,anchor,sortingReference,duration,
            new Color(0.12f,0.42f,0.08f,0.58f),
            new Color(0.38f,0.05f,0.48f,0.62f),11f,0.11f,0.32f);
    }

    public static ParticleSystem PlayMantisHallucinatoryPollen(
        Transform anchor,SpriteRenderer sortingReference,float duration)
    {
        return PlayFloatingStatusMotes(
            "Flotis Hallucinatory Pollen",anchor,sortingReference,duration,
            new Color(1f,0.32f,0.76f,0.72f),
            new Color(0.58f,0.18f,0.92f,0.68f),16f,0.16f,0.42f);
    }

    private static ParticleSystem PlayFloatingStatusMotes(
        string objectName,Transform anchor,SpriteRenderer sortingReference,
        float duration,Color colorMin,Color colorMax,float rate,
        float riseSpeed,float radius)
    {
        if(anchor==null)return null;
        GameObject root=new GameObject(objectName);
        root.transform.SetParent(anchor,false);
        root.transform.localPosition=sortingReference!=null&&sortingReference.sprite!=null
            ?sortingReference.sprite.bounds.center:Vector3.up*0.42f;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=Mathf.Max(0.1f,duration);
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=48;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.55f,0.9f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.02f,0.12f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.08f,0.17f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(colorMin,colorMax);
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.rateOverTime=rate;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=radius;
        ParticleSystem.VelocityOverLifetimeModule velocity=
            particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=riseSpeed;
        ParticleSystem.NoiseModule noise=particles.noise;
        noise.enabled=true;
        noise.strength=0.08f;
        noise.frequency=0.8f;
        noise.scrollSpeed=0.35f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+31;
        particles.Play();
        if(Application.isPlaying)Destroy(root,Mathf.Max(0.1f,duration)+1f);
        return particles;
    }

    public static ParticleSystem PlayMantisSandTrail(
        Vector3 start,Vector3 end,float halfWidth,SpriteRenderer sortingReference)
    {
        Vector3 direction=end-start;
        direction.y=0f;
        float length=direction.magnitude;
        if(length<0.01f)return null;
        GameObject root=new GameObject("Santius Sand Scythe Trail");
        root.transform.position=(start+end)*0.5f+Vector3.up*0.04f;
        root.transform.rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.08f;
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=48;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.2f,0.42f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.02f,0.12f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.025f,0.065f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(0.95f,0.68f,0.18f,0.76f),
            new Color(0.48f,0.24f,0.06f,0.68f));
        main.gravityModifier=0.12f;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.rateOverTime=0f;
        emission.SetBursts(new[]{new ParticleSystem.Burst(
            0f,(short)Mathf.Clamp(Mathf.RoundToInt(length*9f),20,44))});
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(Mathf.Max(0.1f,halfWidth*2f),0.04f,length);
        shape.randomDirectionAmount=1f;
        ParticleSystem.VelocityOverLifetimeModule velocity=
            particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=0.09f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+25;
        particles.Play();
        if(Application.isPlaying)Destroy(root,0.8f);
        return particles;
    }

    private static ParticleSystem PlayStatusTickBurst(
        string objectName,Transform anchor,SpriteRenderer sortingReference,
        Color colorMin,Color colorMax,float heightRatio,float gravity,
        int count,ParticleSystemShapeType shapeType,bool noisy,float anchorHeight=0f)
    {
        if(anchor==null)return null;
        GameObject root=new GameObject(objectName);
        root.transform.position=anchorHeight>0f
            ?anchor.position+Vector3.up*anchorHeight
            :sortingReference!=null&&sortingReference.sprite!=null
            ?sortingReference.transform.TransformPoint(
                sortingReference.sprite.bounds.center+
                Vector3.up*sortingReference.sprite.bounds.extents.y*heightRatio)
            :anchor.position+Vector3.up*0.48f;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.08f;
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=Mathf.Max(12,count);
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=noisy
            ?new ParticleSystem.MinMaxCurve(0.85f,1.2f)
            :new ParticleSystem.MinMaxCurve(0.18f,0.32f);
        main.startSpeed=noisy
            ?new ParticleSystem.MinMaxCurve(0f)
            :new ParticleSystem.MinMaxCurve(0.38f,0.9f);
        main.startSize=noisy
            ?new ParticleSystem.MinMaxCurve(0.075f,0.14f)
            :new ParticleSystem.MinMaxCurve(0.09f,0.16f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(colorMin,colorMax);
        main.gravityModifier=gravity;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=shapeType;
        shape.radius=noisy?0.12f:0.08f;
        if(noisy)
        {
            ParticleSystem.VelocityOverLifetimeModule velocity=
                particles.velocityOverLifetime;
            velocity.enabled=true;
            velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=new ParticleSystem.MinMaxCurve(-0.06f,0.06f);
            velocity.y=new ParticleSystem.MinMaxCurve(0.5f,0.78f);
            velocity.z=new ParticleSystem.MinMaxCurve(-0.02f,0.02f);
            ParticleSystem.NoiseModule noise=particles.noise;
            noise.enabled=true;
            noise.strength=0.05f;
            noise.frequency=1f;
            noise.scrollSpeed=0.4f;
        }
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+38;
        particles.Emit(count);
        particles.Play();
        if(Application.isPlaying)Destroy(root,noisy?1.4f:0.8f);
        return particles;
    }

    public static PixelGroundCastTelegraph PlayTyrannoTremorTelegraph(
        Vector3 position,float radius,float duration,Material outlineMaterial,
        SpriteRenderer sortingReference)
    {
        return PlayGroundAreaTelegraph(
            "Tyranno Tremor Telegraph",position,radius,duration,
            outlineMaterial,sortingReference);
    }

    public static PixelGroundCastTelegraph PlayGriffinSkyDiveTelegraph(
        Vector3 position,float radius,float duration,Material outlineMaterial,
        SpriteRenderer sortingReference)
    {
        return PlayGroundAreaTelegraph(
            "Griffin Sky Dive Landing Area",position,radius,duration,
            outlineMaterial,sortingReference,new Color(0.2f,0.64f,1f,0.32f));
    }

    public static PixelGroundCastTelegraph PlayWiloIceField(
        Vector3 position,float radius,float duration,Material outlineMaterial,
        SpriteRenderer sortingReference)
    {
        PixelGroundCastTelegraph field=PlayGroundAreaTelegraph(
            "Wilo Ice Field",position,radius,duration,
            outlineMaterial,sortingReference,new Color(0.18f,0.68f,1f,0.38f));
        field.HoldFilled(duration);
        return field;
    }

    public static PixelGroundCastTelegraph PlayGroundAreaTelegraph(
        string objectName,Vector3 position,float radius,float duration,
        Material outlineMaterial,SpriteRenderer sortingReference,
        Color? warningColor=null)
    {
        GameObject root=new GameObject(objectName);
        root.transform.position=position+Vector3.up*0.06f;
        PixelGroundCastTelegraph telegraph=
            root.AddComponent<PixelGroundCastTelegraph>();
        telegraph.Configure(
            Mathf.Max(0.1f,radius),Mathf.Max(0.1f,duration),
            outlineMaterial,sortingReference,
            warningColor??new Color(1f,0.16f,0.025f,0.28f));
        return telegraph;
    }

    public static PixelGroundCastTelegraph PlayMushroomSporeCharge(
        Vector3 position,float radius,float duration,Material outlineMaterial,
        SpriteRenderer sortingReference,Color color)
    {
        PixelGroundCastTelegraph telegraph=PlayGroundAreaTelegraph(
            "Mushroom Spore Charge",position,radius,duration,
            outlineMaterial,sortingReference,new Color(color.r,color.g,color.b,0.34f));
        telegraph.SetCenterSprite(PixelStarProjectile.BeanSprite,color,sortingReference);
        return telegraph;
    }

    public static PixelGroundCastTelegraph PlayMushroomChildCharge(
        Vector3 position,float radius,float duration,Material outlineMaterial,
        SpriteRenderer sortingReference)
    {
        Color color=new Color(1f,0.72f,0.2f);
        PixelGroundCastTelegraph telegraph=PlayGroundAreaTelegraph(
            "Kingmore Child Mushroom",position,radius,duration,
            outlineMaterial,sortingReference,new Color(color.r,color.g,color.b,0.34f));
        telegraph.SetCenterSprite(MushroomChildSprite,color,sortingReference);
        return telegraph;
    }

    public static PixelSpiritSanctuaryEffect PlayMushroomPoisonZone(
        Vector3 center,float radius,float duration,SpriteRenderer sortingReference)
    {
        return PlayTyrannoGroundDecal(
            "Doksirum Poison Mycelium",MushroomPoisonGroundSprite,
            center,radius,duration,sortingReference,-96);
    }

    public static PixelCombatEffect PlayRaptorGroundScuff(
        Vector3 position,Vector3 direction,SpriteRenderer sortingReference)
    {
        direction.y=0f;
        if(direction.sqrMagnitude<0.001f)return null;
        direction.Normalize();
        position.y=0f;
        Vector3 side=Vector3.Cross(Vector3.up,direction);
        Sprite[] frames=RaptorClawImpactFrames(new Color(0.66f,0.76f,0.4f));
        PixelCombatEffect scuff=null;
        for(int layer=0;layer<2;layer++)
        {
            scuff=Play(layer==0?"Raptor Ground Gouge":"Raptor Ground Scuff",
                frames,24f,null,sortingReference,layer==0?1.9f:1.8f,-3+layer);
            scuff.faceCamera=false;
            scuff.anchorRenderer=null;
            scuff.transform.position=position+Vector3.up*(0.035f+layer*0.005f)+
                side*(layer==0?0.055f:0f);
            scuff.transform.rotation=Quaternion.LookRotation(Vector3.up,side)*
                Quaternion.Euler(0f,0f,26.565f);
            scuff.SetColor(layer==0?new Color(0.16f,0.12f,0.06f,0.6f):
                new Color(0.78f,0.84f,0.55f,0.72f));
            if(sortingReference==null)scuff.effectRenderer.sortingOrder=7+layer;
        }
        return scuff;
    }

    public static GameObject PlayMammothChargeGuide(
        Vector3 start,Vector3 end,float halfWidth,Material material,
        SpriteRenderer sortingReference)
    {
        Vector3 forward=end-start;
        if(forward.sqrMagnitude<0.001f)return null;
        GameObject root=new GameObject("Mammoth Earth Charge Guide");
        root.AddComponent<CombatEffectEndMarker>();
        GameObject plate=GameObject.CreatePrimitive(PrimitiveType.Quad);
        plate.name="Red Ground Plate";
        plate.transform.SetParent(root.transform,false);
        plate.transform.position=(start+end)*0.5f+Vector3.up*0.045f;
        plate.transform.rotation=Quaternion.LookRotation(Vector3.up,forward.normalized);
        plate.transform.localScale=new Vector3(
            Mathf.Max(0.1f,halfWidth*2f),forward.magnitude,1f);
        Collider collider=plate.GetComponent<Collider>();
        if(collider!=null)
        {
            collider.enabled=false;
            if(Application.isPlaying)Destroy(collider);
            else DestroyImmediate(collider);
        }
        MeshRenderer renderer=plate.GetComponent<MeshRenderer>();
        renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)-85;
        MaterialPropertyBlock color=new MaterialPropertyBlock();
        color.SetColor("_Color",new Color(1f,0.04f,0.025f,0.34f));
        renderer.SetPropertyBlock(color);
        return root;
    }

    public static GameObject PlayMammothGlacierTrail(
        Vector3 start,Vector3 end,float width,float duration,
        SpriteRenderer sortingReference)
    {
        return PlayMammothTraitPath(
            "Seolgason Glacier Trail",start,end,width,duration,sortingReference,
            new Color(0.26f,0.78f,1f,0.78f),
            new Color(0.48f,0.86f,1f),Color.white);
    }

    public static GameObject PlayMammothAncestorTrail(
        Vector3 start,Vector3 end,float width,float duration,
        SpriteRenderer sortingReference)
    {
        return PlayMammothTraitPath(
            "Gaiason Ancestor Trail",start,end,width,duration,sortingReference,
            new Color(0.28f,0.86f,0.34f,0.62f),
            new Color(0.36f,1f,0.48f),new Color(1f,0.84f,0.2f));
    }

    public static GameObject PlayKingKongContinentalFissure(
        Vector3 center,float duration,SpriteRenderer sortingReference)
    {
        GameObject root=new GameObject("KingKong Continental Fissure");
        root.AddComponent<CombatEffectEndMarker>();
        root.transform.position=center+Vector3.up*0.04f;
        root.transform.rotation=Quaternion.LookRotation(Vector3.up,Vector3.forward);
        SpriteRenderer crest=root.AddComponent<SpriteRenderer>();
        crest.sprite=KingKongSlamWaveSprite;
        crest.color=new Color(1f,0.85f,0.5f);
        crest.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        crest.sortingOrder=Mathf.Max(1,(sortingReference==null?10:sortingReference.sortingOrder)-1);
        root.transform.localScale=Vector3.zero;

        GameObject moteRoot=new GameObject("Fissure Expanding Debris");
        moteRoot.transform.SetParent(root.transform,false);
        // Only the emission circle expands; already emitted fragments keep their world positions.
        moteRoot.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=moteRoot.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;
        main.loop=true;main.playOnAwake=false;main.maxParticles=64;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.scalingMode=ParticleSystemScalingMode.Shape;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.18f,0.32f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.85f,1.4f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.07f,0.14f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(MeadowAmbientParticles.TerrainColor(new Color(0.5f,0.36f,0.2f)),MeadowAmbientParticles.TerrainColor(new Color(1f,0.87f,0.58f)));
        main.gravityModifier=0.8f;
        var emission=particles.emission;emission.rateOverTime=144f;
        var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=crest.sprite.bounds.extents.x;shape.radiusThickness=0f;
        var velocity=particles.velocityOverLifetime;velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.Local;
        velocity.x=new ParticleSystem.MinMaxCurve(0f,0f);
        velocity.y=new ParticleSystem.MinMaxCurve(0f,0f);
        velocity.z=new ParticleSystem.MinMaxCurve(0.85f,1.4f);
        var color=particles.colorOverLifetime;color.enabled=true;
        var fade=new Gradient();
        fade.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(0f,1f)});
        color.color=fade;
        var renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=crest.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+18;
        particles.Play();
        if(Application.isPlaying)Destroy(root,duration+0.6f);
        return root;
    }

    public static ParticleSystem PlayMammothStoneShieldBurst(
        Vector3 position,float radius,SpriteRenderer sortingReference)
    {
        return PlayGroundImpactBurst(
            "Kogason Stone Shield Burst",position,radius,sortingReference,
            new Color(0.34f,0.2f,0.08f),new Color(1f,0.72f,0.2f),1.05f,0.72f);
    }

    public static GameObject PlayWyvernFireTrail(
        Vector3 start,Vector3 end,float width,float duration,
        SpriteRenderer sortingReference)
    {
        return PlayMammothTraitPath(
            "Igvire Fire Breath Trail",start,end,width,duration,sortingReference,
            new Color(0.62f,0.04f,0.015f,0.82f),
            new Color(1f,0.16f,0.025f),new Color(1f,0.76f,0.08f));
    }

    public static GameObject PlayWyvernGlacierTrail(
        Vector3 start,Vector3 end,float width,float duration,
        SpriteRenderer sortingReference)
    {
        return PlayMammothTraitPath(
            "Frovire Glacier Breath Trail",start,end,width,duration,sortingReference,
            new Color(0.18f,0.72f,1f,0.76f),
            new Color(0.48f,0.9f,1f),Color.white);
    }

    private static GameObject PlayMammothTraitPath(
        string objectName,Vector3 start,Vector3 end,float width,float duration,
        SpriteRenderer sortingReference,Color groundColor,
        Color particleMin,Color particleMax)
    {
        Vector3 forward=end-start;
        float length=forward.magnitude;
        if(length<0.001f)return null;
        forward/=length;
        duration=Mathf.Max(0.1f,duration);
        width=Mathf.Max(0.1f,width);

        GameObject root=new GameObject(objectName);
        root.AddComponent<CombatEffectEndMarker>();
        root.transform.position=(start+end)*0.5f+Vector3.up*0.035f;
        root.transform.rotation=Quaternion.LookRotation(Vector3.up,forward);
        GameObject groundObject=new GameObject("Pixel Ground Path");
        groundObject.transform.SetParent(root.transform,false);
        SpriteRenderer ground=groundObject.AddComponent<SpriteRenderer>();
        ground.sprite=MammothPathGroundSprite;
        ground.color=groundColor;
        ground.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        ground.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)-92;
        ground.transform.localScale=new Vector3(
            width/ground.sprite.bounds.size.x,length/ground.sprite.bounds.size.y,1f);

        GameObject moteRoot=new GameObject("Low Trail Motes");
        moteRoot.transform.SetParent(root.transform,true);
        moteRoot.transform.position=(start+end)*0.5f+Vector3.up*0.08f;
        moteRoot.transform.rotation=Quaternion.LookRotation(forward,Vector3.up);
        moteRoot.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=moteRoot.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=duration;
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=72;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.35f,0.7f);
        main.startSpeed=0f;
        main.startSize=new ParticleSystem.MinMaxCurve(0.025f,0.065f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(particleMin,particleMax);
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.rateOverTime=16f;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(width*0.9f,0.025f,length*0.92f);
        ParticleSystem.VelocityOverLifetimeModule velocity=
            particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=0.08f;
        ParticleSystemRenderer particleRenderer=
            particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode=ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial=BearSpiritParticleMaterial;
        particleRenderer.sortingLayerID=ground.sortingLayerID;
        particleRenderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+18;
        particles.Play();
        if(Application.isPlaying)Destroy(root,duration+0.75f);
        return root;
    }

    public static ParticleSystem PlayWyvernBreath(
        Transform anchor,SpriteRenderer sortingReference,Vector3 forward,
        float length,float halfWidth,float duration,Material particleMaterial,
        Color? minimumColor=null,Color? maximumColor=null,
        string objectName="Wyvern Breath")
    {
        Vector3 direction=forward.sqrMagnitude<0.001f
            ?Vector3.forward:forward.normalized;
        GameObject root=new GameObject(objectName);
        Bounds bounds=sortingReference!=null?sortingReference.bounds:
            new Bounds(anchor.position+Vector3.up*0.35f,Vector3.one*0.5f);
        root.transform.position=bounds.center+
            Vector3.up*Mathf.Max(0.04f,bounds.extents.y*0.18f)+
            direction*Mathf.Max(0.42f,bounds.extents.x*0.85f);
        root.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=Mathf.Max(0.1f,duration);
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=112;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.3f,0.44f);
        float speed=Mathf.Max(2f,length/0.4f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(speed*0.72f,speed);
        main.startSize=new ParticleSystem.MinMaxCurve(0.075f,0.16f);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*0.5f);
        main.startColor=new ParticleSystem.MinMaxGradient(
            minimumColor??new Color(0.22f,1f,0.42f),
            maximumColor??new Color(0.68f,1f,0.78f));
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.rateOverTime=180f;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Cone;
        shape.radius=0.04f;
        shape.angle=Mathf.Clamp(
            Mathf.Atan2(Mathf.Max(0.1f,halfWidth),Mathf.Max(0.1f,length))*
            Mathf.Rad2Deg,3f,18f);
        ParticleSystem.SizeOverLifetimeModule size=particles.sizeOverLifetime;
        size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(
            new Keyframe(0f,0.22f),new Keyframe(0.65f,0.86f),
            new Keyframe(1f,1.18f)));
        ParticleSystem.VelocityOverLifetimeModule velocity=
            particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.Local;
        AnimationCurve flatVelocity=AnimationCurve.Linear(0f,0f,1f,0f);
        velocity.x=new ParticleSystem.MinMaxCurve(1f,flatVelocity);
        velocity.y=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(
            new Keyframe(0f,0f),new Keyframe(1f,0.34f)));
        velocity.z=new ParticleSystem.MinMaxCurve(1f,flatVelocity);
        ParticleSystem.NoiseModule noise=particles.noise;
        noise.enabled=true;
        noise.strength=0.1f;
        noise.frequency=1.6f;
        noise.scrollSpeed=0.7f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=particleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+34;
        particles.Play();
        if(Application.isPlaying)Destroy(root,Mathf.Max(0.1f,duration)+0.7f);
        return particles;
    }

    public static ParticleSystem PlayHaetaeRoarShout(
        Transform anchor,SpriteRenderer sortingReference,Material particleMaterial,
        Color? minimumColor=null,Color? maximumColor=null,
        string objectName="Haetae Shout")
    {
        GameObject root=new GameObject(objectName);
        Bounds bounds=sortingReference!=null?sortingReference.bounds:
            new Bounds(anchor.position+Vector3.up*0.35f,Vector3.one*0.5f);
        root.transform.position=bounds.center+
                                Vector3.up*(bounds.extents.y+0.14f);
        root.transform.rotation=Quaternion.FromToRotation(
            Vector3.forward,Vector3.up);
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.08f;
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=20;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.2f,0.34f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.75f,1.35f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.055f,0.12f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(
            minimumColor??new Color(1f,0.86f,0.3f),
            maximumColor??new Color(0.64f,0.94f,1f));
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.rateOverTime=0f;
        emission.SetBursts(new[]{new ParticleSystem.Burst(0f,16)});
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Cone;
        shape.radius=0.05f;
        shape.angle=38f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Stretch;
        renderer.lengthScale=0.22f;
        renderer.velocityScale=0.06f;
        renderer.sharedMaterial=particleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+42;
        particles.Play();
        if(Application.isPlaying)Destroy(root,0.8f);
        return particles;
    }

    public static PixelHaetaeRoarWave PlayHaetaeRoarWave(
        Vector3 center,float radius,SpriteRenderer sortingReference,
        Color? waveColor=null)
    {
        GameObject root=new GameObject("Haetae Roar Ground Wave");
        root.transform.position=center+Vector3.up*0.04f;
        root.transform.rotation=Quaternion.Euler(90f,0f,0f);
        SpriteRenderer renderer=root.AddComponent<SpriteRenderer>();
        renderer.sprite=SpiritSanctuaryGroundSprite;
        renderer.color=waveColor??new Color(0.72f,0.94f,1f,0.88f);
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)-82;
        float scale=Mathf.Max(0.1f,radius)*2f/
                    Mathf.Max(0.01f,renderer.sprite.bounds.size.x);
        PixelHaetaeRoarWave wave=root.AddComponent<PixelHaetaeRoarWave>();
        wave.Configure(renderer,scale,0.48f);
        return wave;
    }

    public static ParticleSystem PlayTyrannoTremorCharge(
        Transform anchor,SpriteRenderer sortingReference,float duration)
    {
        GameObject root=new GameObject("Tyranno Tremor Charge");
        root.transform.SetParent(anchor,false);
        root.transform.localPosition=sortingReference!=null&&sortingReference.sprite!=null
            ?sortingReference.sprite.bounds.center:Vector3.up*0.35f;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=Mathf.Max(0.1f,duration);
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=32;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.35f,0.6f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(-0.75f,-0.42f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.025f,0.05f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(1f,0.28f,0.035f),new Color(1f,0.8f,0.16f));
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.rateOverTime=14f;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=0.72f;
        shape.radiusThickness=0.08f;
        shape.scale=new Vector3(1f,0.72f,1f);
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+24;
        particles.Play();
        if(Application.isPlaying)Destroy(root,Mathf.Max(0.1f,duration)+0.8f);
        return particles;
    }

    public static ParticleSystem PlayTyrannoTremorImpact(
        Vector3 position,float radius,SpriteRenderer sortingReference,bool autoDestroy=true)
    {
        return PlayGroundImpactBurst(
            "Tyranno Tremor Ground Debris",position,radius,sortingReference,
            MeadowAmbientParticles.TerrainColor(new Color(0.48f,0.15f,0.035f)),MeadowAmbientParticles.TerrainColor(new Color(1f,0.46f,0.08f)),
            0.8f,0.65f,autoDestroy);
    }

    public static ParticleSystem PlayFrogTongueSlamImpact(
        Vector3 position,SpriteRenderer sortingReference)
    {
        return PlayLandingDebris("Frog Tongue Slam Debris",position,1.35f,sortingReference);
    }

    public static readonly Color LeylineGold=new Color(1f,0.81f,0.3f);

    public static PixelKingKongLeylineAura PlayKingKongLeylineAura(
        Transform anchor,SpriteRenderer source)
    {
        if(anchor==null)return null;
        var root=new GameObject("KingKong Leyline Heart");
        root.transform.position=new Vector3(anchor.position.x,0f,anchor.position.z);
        ParticleSystem stones=PlayKingKongWorldOverturnCharge(root.transform.position,2.1f,source);
        stones.name="KingKong Leyline Floating Stones";
        stones.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        stones.transform.SetParent(root.transform,true);
        var shape=stones.shape;
        shape.radius=MonsterAutoBattlePrototype.TileSize*FamilyTranscendenceRules.KingKongZoneRadiusTiles;
        var main=stones.main;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.maxParticles=12;
        main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        main.startLifetime=FamilyTranscendenceRules.KingKongZoneDuration+1f;
        main.startSize=new ParticleSystem.MinMaxCurve(0.14f,0.24f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(MeadowAmbientParticles.TerrainColor(new Color(0.65f,0.47f,0.22f)),MeadowAmbientParticles.TerrainColor(LeylineGold));
        var emission=stones.emission;emission.rateOverTime=0f;
        var velocity=stones.velocityOverLifetime;velocity.enabled=false;
        stones.Play();stones.Emit(main.maxParticles);
        var aura=root.AddComponent<PixelKingKongLeylineAura>();
        aura.Configure(anchor,source,stones,LeylineGlowSprite,BearSpiritParticleSprite);
        return aura;
    }

    public static PixelHaetaeRoarWave PlayKingKongLeylineWave(
        Vector3 position,float radius,SpriteRenderer reference,bool basic=false)
    {
        var root=new GameObject(basic?"KingKong Leyline Basic Wave":"KingKong Leyline Heartbeat Wave");
        root.transform.position=new Vector3(position.x,0.045f,position.z);
        root.transform.rotation=Quaternion.Euler(90f,0f,0f);
        var renderer=root.AddComponent<SpriteRenderer>();
        renderer.sprite=KingKongSlamWaveSprite;
        renderer.color=basic?new Color(1f,0.93f,0.65f):LeylineGold;
        renderer.sortingLayerID=reference==null?0:reference.sortingLayerID;
        renderer.sortingOrder=Mathf.Max(1,(reference==null?10:reference.sortingOrder)-1);
        var wave=root.AddComponent<PixelHaetaeRoarWave>();
        wave.Configure(renderer,2f*radius/renderer.sprite.bounds.size.x,basic?0.26f:0.42f,0.65f);
        return wave;
    }

    public static ParticleSystem PlayKingKongLeylineHitDebris(Vector3 position,SpriteRenderer reference)
    {
        return PlayLandingDebris("KingKong Leyline Hit Debris",position,0.55f,reference,0.45f);
    }

    private static Sprite LeylineGlowSprite
    {
        get
        {
            if(leylineGlowSprite!=null)return leylineGlowSprite;
            var texture=new Texture2D(17,17,TextureFormat.RGBA32,false)
            {name="Leyline Pixel Glow",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,
             hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[17*17];
            for(int y=0;y<17;y++)for(int x=0;x<17;x++)
            {
                float light=Mathf.Clamp01(1f-new Vector2(x-8,y-8).magnitude/8.5f);
                pixels[y*17+x]=new Color(1f,1f,1f,light*light);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            leylineGlowSprite=Sprite.Create(texture,new Rect(0,0,17,17),new Vector2(0.5f,0.5f),16f);
            leylineGlowSprite.name=texture.name;leylineGlowSprite.hideFlags=HideFlags.HideAndDontSave;
            return leylineGlowSprite;
        }
    }

    public static ParticleSystem PlayKingKongWorldOverturnCharge(
        Vector3 position,float radius,SpriteRenderer sortingReference)
    {
        var root=new GameObject("KingKong Floating Ground Fragments");
        root.transform.position=position+Vector3.up*0.05f;
        root.AddComponent<CombatEffectEndMarker>();
        var particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;
        main.loop=true;main.playOnAwake=false;main.maxParticles=48;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.45f,0.65f);
        main.startSpeed=0f;main.gravityModifier=0f;
        main.startSize=new ParticleSystem.MinMaxCurve(0.09f,0.17f);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
        main.startColor=new ParticleSystem.MinMaxGradient(MeadowAmbientParticles.TerrainColor(new Color(0.42f,0.34f,0.23f)),MeadowAmbientParticles.TerrainColor(new Color(0.88f,0.81f,0.65f)));
        var emission=particles.emission;emission.rateOverTime=72f;
        var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=Mathf.Max(0.1f,radius*0.8f);shape.radiusThickness=1f;
        shape.rotation=new Vector3(-90f,0f,0f);
        var velocity=particles.velocityOverLifetime;velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=new ParticleSystem.MinMaxCurve(0.65f,AnimationCurve.EaseInOut(0f,1f,1f,0.15f));
        velocity.x=new ParticleSystem.MinMaxCurve(0f,AnimationCurve.Linear(0f,0f,1f,0f));
        velocity.z=new ParticleSystem.MinMaxCurve(0f,AnimationCurve.Linear(0f,0f,1f,0f));
        var renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+18;
        // The animation owns cleanup at contact, interruption, disable and death.
        particles.Play();
        return particles;
    }

    public static ParticleSystem PlayKingKongGroundSlam(
        Vector3 position,float radius,SpriteRenderer sortingReference,bool heavy=false,bool worldOverturn=false)
    {
        GameObject root=new GameObject("KingKong Ground Slam Wave");
        root.transform.position=new Vector3(position.x,0.04f,position.z);
        root.transform.rotation=Quaternion.Euler(90f,0f,0f);
        SpriteRenderer wave=root.AddComponent<SpriteRenderer>();
        wave.sprite=KingKongSlamWaveSprite;
        wave.color=worldOverturn?new Color(1f,0.75f,0.42f):heavy?new Color(1f,0.87f,0.54f):Color.white;
        wave.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        wave.sortingOrder=Mathf.Max(1,(sortingReference==null?10:sortingReference.sortingOrder)-1);
        float scale=Mathf.Max(0.1f,radius)*2f/wave.sprite.bounds.size.x;
        root.AddComponent<PixelHaetaeRoarWave>().Configure(wave,scale,worldOverturn?0.36f:heavy?0.48f:0.42f,worldOverturn?0.6f:0.75f);
        return PlayLandingDebris("KingKong Ground Slam Debris",position,radius,sortingReference,worldOverturn?1.5f:heavy?1.35f:1f);
    }

    public static ParticleSystem PlaySilkLanceGroundImpact(Vector3 position,SpriteRenderer reference,bool final)
    {
        var debris=PlayLandingDebris("Silk Lance Ground Chips",position,.24f,reference,.45f);
        debris.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=debris.main;main.startSize=new ParticleSystem.MinMaxCurve(.055f,.10f);PixelCombatEffect.ScaleParticleSize(main);
        main.startLifetime=new ParticleSystem.MinMaxCurve(.20f,.34f);
        main.maxParticles=final?7:4;
        var emission=debris.emission;
        emission.SetBursts(new[]{new ParticleSystem.Burst(0f,(short)(final?7:4))});
        debris.GetComponent<ParticleSystemRenderer>().sortingOrder=(reference==null?10:reference.sortingOrder)+1;
        debris.Play();return debris;
    }

    private static ParticleSystem PlayLandingDebris(
        string name,Vector3 position,float radius,SpriteRenderer sortingReference,float strength=1f)
    {
        position.y=0f;
        ParticleSystem debris=PlayGroundImpactBurst(name,position,radius*0.3f,
            sortingReference,MeadowAmbientParticles.TerrainColor(new Color(0.42f,0.34f,0.23f)),MeadowAmbientParticles.TerrainColor(new Color(0.88f,0.81f,0.65f)),
            1f,1.65f);
        debris.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=debris.main;
        main.startSize=new ParticleSystem.MinMaxCurve(0.12f*strength,0.22f*strength);PixelCombatEffect.ScaleParticleSize(main);
        main.startSpeed=new ParticleSystem.MinMaxCurve(1.8f*strength,2.7f*strength);
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.6f,0.85f);
        // Launch clear of the floor; constant upward velocity fights collision bounce.
        var velocity=debris.velocityOverLifetime;velocity.enabled=false;
        var shape=debris.shape;
        shape.shapeType=ParticleSystemShapeType.Cone;
        shape.angle=65f;
        shape.rotation=new Vector3(-90f,0f,0f);
        shape.position=Vector3.up*0.12f;
        shape.scale=Vector3.one;
        shape.randomDirectionAmount=0f;
        var emission=debris.emission;
        emission.SetBursts(new[]{new ParticleSystem.Burst(0f,(short)Mathf.RoundToInt(18f*strength))});
        var collision=debris.collision;
        collision.enabled=true;
        collision.type=ParticleSystemCollisionType.Planes;
        collision.SetPlane(0,debris.transform);
        collision.bounce=0.35f;
        collision.dampen=0.45f;
        collision.lifetimeLoss=0.15f;
        var color=debris.colorOverLifetime;color.enabled=true;
        var fade=new Gradient();
        fade.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(1f,0.65f),new GradientAlphaKey(0f,1f)});
        color.color=new ParticleSystem.MinMaxGradient(fade);
        debris.Play();
        return debris;
    }

    public static ParticleSystem PlayGriffinSkyDiveImpact(
        Vector3 position,float radius,SpriteRenderer sortingReference)
    {
        return PlayGroundImpactBurst(
            "Griffin Sky Dive Feather Shockwave",position,radius,sortingReference,
            new Color(0.2f,0.65f,1f),new Color(1f,0.82f,0.24f),
            0.25f,0.42f);
    }

    public static ParticleSystem PlayNinjaFlameAfterimage(
        Vector3 position,float radius,SpriteRenderer sortingReference)
    {
        return PlayGroundImpactBurst(
            "Ninja Flame Afterimage",position,radius,sortingReference,
            new Color(0.72f,0.025f,0.015f),new Color(1f,0.62f,0.08f),
            0.1f,0.52f);
    }

    private static ParticleSystem PlayGroundImpactBurst(
        string objectName,Vector3 position,float radius,
        SpriteRenderer sortingReference,Color colorMin,Color colorMax,
        float gravity,float upwardVelocity,bool autoDestroy=true)
    {
        GameObject root=new GameObject(objectName);
        root.transform.position=position+Vector3.up*0.05f;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.12f;
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=64;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.22f,0.42f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.12f,0.45f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.025f,0.06f);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
        main.startColor=new ParticleSystem.MinMaxGradient(colorMin,colorMax);
        main.gravityModifier=gravity;

        ParticleSystem.EmissionModule emission=particles.emission;
        emission.rateOverTime=0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(
                0f,(short)Mathf.Clamp(Mathf.RoundToInt(radius*radius*8f),24,48))
        });
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=Mathf.Max(0.1f,radius);
        shape.radiusThickness=1f;
        shape.scale=new Vector3(1f,0.025f,1f);
        shape.randomDirectionAmount=1f;
        ParticleSystem.VelocityOverLifetimeModule velocity=
            particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=upwardVelocity;

        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+32;
        particles.Play();
        if(Application.isPlaying&&autoDestroy)Destroy(root,1.4f);
        return particles;
    }

    public static ParticleSystem PlayTyrannoTremorHit(
        Transform anchor,SpriteRenderer sortingReference)
    {
        GameObject root=new GameObject("Tyranno Tremor Hit");
        root.transform.position=sortingReference!=null&&sortingReference.sprite!=null
            ?anchor.TransformPoint(sortingReference.sprite.bounds.center)
            :anchor.position+Vector3.up*0.35f;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.06f;
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=10;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.12f,0.22f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.35f,0.8f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.03f,0.065f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(1f,0.38f,0.04f),new Color(1f,0.95f,0.42f));
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.rateOverTime=0f;
        emission.SetBursts(new[]{new ParticleSystem.Burst(0f,8)});
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=0.08f;
        shape.randomDirectionAmount=1f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+36;
        particles.Play();
        if(Application.isPlaying)Destroy(root,0.6f);
        return particles;
    }


    public static ParticleSystem PlayNinjaVanishBurst(
        Transform anchor,SpriteRenderer sortingReference)
    {
        GameObject root=new GameObject("Ninja Vanish Particles");
        root.transform.position=sortingReference!=null&&sortingReference.sprite!=null
            ?anchor.TransformPoint(sortingReference.sprite.bounds.center)
            :anchor.position+Vector3.up*0.35f;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.25f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.22f,0.42f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.45f,1.25f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.07f,0.14f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(0.35f,0.08f,0.72f),new Color(0.88f,0.25f,1f));
        main.maxParticles=24;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(0.45f,0.5f,0.05f);
        ParticleSystem.VelocityOverLifetimeModule velocity=particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=0.55f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+20;
        particles.Emit(18);
        particles.Play();
        if(Application.isPlaying)Destroy(root,0.9f);
        return particles;
    }

    public static GameObject PlayNinjaLineSlash(
        Vector3 start,Vector3 end,SpriteRenderer sortingReference,
        Color? color=null)
    {
        GameObject root=new GameObject("Ninja Shadow Line Slash");
        root.AddComponent<PixelNinjaLineSlash>().Configure(
            NinjaLinePixelSprite,start,end,sortingReference,0.22f,
            color??new Color(0.92f,0.34f,1f));
        return root;
    }

    public static GameObject PlayMantisSoulThread(
        Vector3 start,Vector3 end,SpriteRenderer sortingReference)
    {
        GameObject root=new GameObject("Nectis Soul Thread");
        root.AddComponent<PixelNinjaLineSlash>().Configure(
            NinjaLinePixelSprite,start,end,sortingReference,0.32f,
            new Color(0.46f,0.08f,0.68f,0.9f));
        return root;
    }

    public static ParticleSystem PlayBearSpiritBurst(
        Transform anchor,SpriteRenderer sortingReference)
    {
        ParticleSystem particles=CreateBearSpiritParticles(
            "Bear Fighting Spirit Burst",anchor,sortingReference,false);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.35f;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.24f,0.42f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(1.2f,2.1f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.1f,0.18f);PixelCombatEffect.ScaleParticleSize(main);
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=0.22f;
        particles.Emit(14);
        particles.Play();
        if(Application.isPlaying)Destroy(particles.gameObject,0.9f);
        return particles;
    }

    public static ParticleSystem PlayBearSpiritAura(
        Transform anchor,SpriteRenderer sortingReference)
    {
        ParticleSystem particles=CreateBearSpiritParticles(
            "Bear Fighting Spirit Aura",anchor,sortingReference,true);
        ParticleSystem.MainModule main=particles.main;
        main.duration=1f;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.55f,0.85f);
        main.startSpeed=0f;
        main.startSize=new ParticleSystem.MinMaxCurve(0.08f,0.14f);PixelCombatEffect.ScaleParticleSize(main);
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=true;
        emission.rateOverTime=9f;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(0.7f,0.12f,0.02f);
        ParticleSystem.VelocityOverLifetimeModule velocity=particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.Local;
        velocity.y=0.7f;
        particles.Play();
        return particles;
    }

    public static PixelTranscendenceFlameOutline PlayTranscendenceFlameOutline(
        SpriteRenderer sourceRenderer)
    {
        if(sourceRenderer==null)return null;
        Transform existing=sourceRenderer.transform.Find(
            "Transcendence Red Flame Outline");
        if(existing!=null)
            return existing.GetComponent<PixelTranscendenceFlameOutline>();
        GameObject root=new GameObject("Transcendence Red Flame Outline");
        root.transform.SetParent(sourceRenderer.transform,false);
        PixelTranscendenceFlameOutline outline=
            root.AddComponent<PixelTranscendenceFlameOutline>();
        outline.Configure(sourceRenderer);
        return outline;
    }

    public static PixelTranscendenceFlameOutline PlayProtagonistClimaxFlameOutline(
        SpriteRenderer sourceRenderer,float duration)
    {
        if(sourceRenderer==null)return null;
        Transform existing=sourceRenderer.transform.Find(
            "Protagonist Climax Flame Outline");
        if(existing!=null)Destroy(existing.gameObject);
        GameObject root=new GameObject("Protagonist Climax Flame Outline");
        root.AddComponent<CombatEffectEndMarker>();
        root.transform.SetParent(sourceRenderer.transform,false);
        PixelTranscendenceFlameOutline outline=
            root.AddComponent<PixelTranscendenceFlameOutline>();
        outline.ConfigureClimax(sourceRenderer,duration);
        return outline;
    }

    public static SpriteRenderer PlayBearMountainColossusHologram(
        Transform anchor,SpriteRenderer sourceRenderer)
    {
        if(anchor==null||sourceRenderer==null||sourceRenderer.sprite==null)return null;
        GameObject root=new GameObject("Bear Mountain Colossus Hologram");
        root.transform.SetParent(anchor,false);
        SpriteRenderer hologram=root.AddComponent<SpriteRenderer>();
        root.AddComponent<PixelBearUpperBodyHologram>().Configure(
            sourceRenderer,hologram);
        ParticleSystem shimmer=CreateBearSpiritParticles(
            "Bear Mountain Hologram Shimmer",root.transform,hologram,true);
        shimmer.transform.localPosition=Vector3.zero;
        ParticleSystem.MainModule main=shimmer.main;
        main.maxParticles=36;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.9f,1.5f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.04f,0.14f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.035f,0.075f);PixelCombatEffect.ScaleParticleSize(main);
        Gradient palette=new Gradient();
        palette.SetKeys(
            new[]{
                new GradientColorKey(new Color(0.52f,0.91f,0.84f),0f),
                new GradientColorKey(new Color(0.9f,0.86f,0.66f),0.55f),
                new GradientColorKey(new Color(0.38f,0.29f,0.2f),1f)},
            new[]{new GradientAlphaKey(0.86f,0f),new GradientAlphaKey(0.68f,1f)});
        ParticleSystem.MinMaxGradient randomPalette=
            new ParticleSystem.MinMaxGradient(palette);
        randomPalette.mode=ParticleSystemGradientMode.RandomColor;
        main.startColor=randomPalette;
        ParticleSystem.EmissionModule emission=shimmer.emission;
        emission.rateOverTime=14f;
        ParticleSystem.ShapeModule shape=shimmer.shape;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=0.5f;
        ParticleSystem.VelocityOverLifetimeModule velocity=
            shimmer.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.Local;
        velocity.y=0.16f;
        ParticleSystem.NoiseModule noise=shimmer.noise;
        noise.enabled=true;
        noise.quality=ParticleSystemNoiseQuality.Low;
        noise.strength=0.055f;
        noise.frequency=0.65f;
        ParticleSystemRenderer shimmerRenderer=
            shimmer.GetComponent<ParticleSystemRenderer>();
        shimmerRenderer.sortingOrder=hologram.sortingOrder+1;
        shimmer.Play();
        return hologram;
    }

    public static SpriteRenderer PlayBearKingRoarSpirit(
        Transform anchor,SpriteRenderer sourceRenderer,bool ending=false)
    {
        if(anchor==null||sourceRenderer==null||sourceRenderer.sprite==null)return null;
        if(bearKingRoarSpiritMaterial==null)
        {
            Shader shader=Shader.Find("BecomeZ/Effects/Bear Roar Spirit Fade");
            if(shader==null)return null;
            bearKingRoarSpiritMaterial=new Material(shader)
            {
                name="Bear King Roar Spirit Fade",
                hideFlags=HideFlags.HideAndDontSave
            };
        }
        GameObject root=new GameObject("Bear King Roar Upper Spirit");
        root.transform.SetParent(anchor,false);
        SpriteRenderer spirit=root.AddComponent<SpriteRenderer>();
        spirit.sharedMaterial=bearKingRoarSpiritMaterial;
        root.AddComponent<PixelBearKingRoarSpirit>().Configure(
            sourceRenderer,spirit,ending);
        return spirit;
    }

    public static void StopBearMountainColossusHologram(SpriteRenderer hologram)
    {
        if(hologram==null||CombatEffectEndPlayback.IsPreserving(hologram))return;
        if(Application.isPlaying)Destroy(hologram.gameObject);
        else DestroyImmediate(hologram.gameObject);
    }

    public static ParticleSystem PlayKingpaRoyalAura(
        Transform anchor,SpriteRenderer sortingReference,float duration,
        Color? minimumColor=null,Color? maximumColor=null,
        string objectName="Kingpa Royal Decree Aura")
    {
        ParticleSystem particles=CreateBearSpiritParticles(
            objectName,anchor,sortingReference,false);
        ParticleSystem.MainModule main=particles.main;
        main.duration=Mathf.Max(0.1f,duration);
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.5f,0.8f);
        main.startSpeed=0f;
        main.startSize=new ParticleSystem.MinMaxCurve(0.06f,0.12f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(
            minimumColor??new Color(1f,0.68f,0.08f),
            maximumColor??new Color(1f,0.96f,0.48f));
        main.stopAction=ParticleSystemStopAction.Destroy;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=true;
        emission.rateOverTime=11f;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(0.7f,0.12f,0.02f);
        ParticleSystem.VelocityOverLifetimeModule velocity=
            particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.Local;
        velocity.y=0.72f;
        particles.Play();
        return particles;
    }

    public static SpriteRenderer PlayFreezeOverlay(
        Transform anchor,SpriteRenderer sortingReference,float duration,
        CombatStatusState statuses=null)
    {
        if(anchor==null||statuses!=null&&!statuses.Has(CombatStatusKind.Freeze))return null;
        PixelFreezeShell existing=anchor.GetComponentInChildren<PixelFreezeShell>();
        if(existing!=null)
        {
            existing.Refresh(duration,statuses);
            return existing.GetComponent<SpriteRenderer>();
        }
        GameObject root=new GameObject("Common Freeze Ice Block");
        root.transform.SetParent(anchor,false);
        root.transform.localPosition=sortingReference!=null&&sortingReference.sprite!=null
            ?sortingReference.sprite.bounds.center:Vector3.up*0.55f;
        SpriteRenderer overlay=root.AddComponent<SpriteRenderer>();
        overlay.color=Color.clear;
        overlay.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        overlay.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+2;
        root.AddComponent<PixelFreezeShell>().Configure(overlay,sortingReference,duration,statuses);
        return overlay;
    }

    public static ParticleSystem PlayFreezeDeathShatter(
        Transform anchor,SpriteRenderer sortingReference,bool death=true)
    {
        if(anchor==null)return null;
        Transform[] children=anchor.GetComponentsInChildren<Transform>(true);
        for(int index=0;index<children.Length;index++)
        {
            Transform child=children[index];
            if(!death||child==null||child==anchor||child.name!="Common Freeze Ice Block")continue;
            child.gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        GameObject root=new GameObject(death?"Common Freeze Death Shatter":"Common Freeze Thaw Shatter");
        root.transform.position=sortingReference!=null&&sortingReference.sprite!=null
            ?sortingReference.transform.TransformPoint(sortingReference.sprite.bounds.center)
            :anchor.position+Vector3.up*0.5f;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.08f;
        main.loop=false;
        main.playOnAwake=false;
        main.maxParticles=death?24:10;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.22f,death?0.52f:0.38f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.45f,death?1.85f:1.15f);
        float bodyScale=sortingReference==null?1f:Mathf.Clamp(AuthoredBodySize(sortingReference).y*0.5f,0.65f,2f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.12f*bodyScale,0.25f*bodyScale);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(0.42f,0.82f,1f,0.95f),
            new Color(0.9f,0.98f,1f,1f));
        main.gravityModifier=0.55f;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        float radius=sortingReference!=null&&sortingReference.sprite!=null
            ?Mathf.Max(0.18f,sortingReference.bounds.extents.magnitude*0.48f):0.32f;
        shape.radius=radius;
        shape.scale=new Vector3(1f,0.9f,0.18f);
        shape.randomDirectionAmount=1f;
        ParticleSystem.ColorOverLifetimeModule fading=particles.colorOverLifetime;
        fading.enabled=true;
        Gradient fade=new Gradient();
        fade.SetKeys(
            new[]{new GradientColorKey(Color.white,0f),
                new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(1f,0f),
                new GradientAlphaKey(0.9f,0.58f),
                new GradientAlphaKey(0f,1f)});
        fading.color=new ParticleSystem.MinMaxGradient(fade);
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        if(freezeShardMaterial==null)
            freezeShardMaterial=new Material(BearSpiritParticleMaterial)
            {
                name="Common Freeze Crystal Material",mainTexture=FreezeShardSprite.texture,
                hideFlags=HideFlags.HideAndDontSave
            };
        renderer.sharedMaterial=freezeShardMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+3;
        particles.Emit(main.maxParticles);
        particles.Play();
        if(Application.isPlaying)Destroy(root,1f);
        return particles;
    }

    public static SpriteRenderer PlayStarMarker(
        string objectName,Transform anchor,SpriteRenderer sortingReference,
        Color color,float duration,float scale=0.62f)
    {
        if(anchor==null)return null;
        GameObject root=new GameObject(objectName);
        root.AddComponent<CombatEffectEndMarker>();
        root.transform.SetParent(anchor,false);
        root.transform.localPosition=sortingReference!=null&&sortingReference.sprite!=null
            ?new Vector3(sortingReference.sprite.bounds.center.x,
                sortingReference.sprite.bounds.max.y+0.22f,0f)
            :Vector3.up*0.9f;
        root.transform.localScale=Vector3.one*Mathf.Max(0.1f,scale);
        SpriteRenderer marker=root.AddComponent<SpriteRenderer>();
        marker.sprite=PixelStarProjectile.SharedSprite;
        marker.color=color;
        marker.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        marker.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+30;
        if(Application.isPlaying)Destroy(root,Mathf.Max(0.1f,duration));
        return marker;
    }

    public static void StopBearSpiritAura(ParticleSystem particles)
    {
        if(particles==null)return;
        if(CombatEffectEndFade.IsFading(particles))return;
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(Application.isPlaying)Destroy(particles.gameObject);
        else DestroyImmediate(particles.gameObject);
    }

    public static GameObject PlaySpiderWebGround(
        Vector3 center,float radius,Color color,SpriteRenderer sortingReference)
    {
        GameObject root=new GameObject("Pixel Spider Web Ground");
        root.transform.position=center+Vector3.up*0.015f;
        root.transform.rotation=Quaternion.Euler(90f,0f,0f);
        GameObject visual=new GameObject("Web");
        visual.transform.SetParent(root.transform,false);
        SpriteRenderer renderer=visual.AddComponent<SpriteRenderer>();
        renderer.sprite=SpiderWebGroundSprite;
        renderer.color=color;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)-100;
        float scale=radius*2f/Mathf.Max(0.01f,renderer.sprite.bounds.size.x);
        visual.AddComponent<PixelGroundWebReveal>().Configure(renderer,scale,0.18f);
        return root;
    }

    public static PixelSpiritSanctuaryEffect PlaySpiritSanctuary(
        Vector3 center,float radius,float duration,Color color,
        SpriteRenderer sortingReference,string objectName="Pixel Spirit Sanctuary",
        bool stableGround=false)
    {
        GameObject root=new GameObject(objectName);
        root.transform.position=center+Vector3.up*0.025f;

        GameObject ground=new GameObject("Sanctuary Ground");
        ground.transform.SetParent(root.transform,false);
        ground.transform.rotation=Quaternion.Euler(90f,0f,0f);
        SpriteRenderer groundRenderer=ground.AddComponent<SpriteRenderer>();
        groundRenderer.sprite=SpiritSanctuaryGroundSprite;
        groundRenderer.color=new Color(color.r,color.g,color.b,0.72f);
        groundRenderer.sortingLayerID=sortingReference==null
            ?0:sortingReference.sortingLayerID;
        groundRenderer.sortingOrder=(sortingReference==null
            ?0:sortingReference.sortingOrder)-95;

        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=Mathf.Max(0.1f,duration);
        main.loop=true;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(1.05f,1.65f);
        main.startSpeed=0f;
        main.startSize=new ParticleSystem.MinMaxCurve(0.075f,0.14f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(
            color,Color.Lerp(color,Color.white,0.65f));
        main.maxParticles=56;
        main.simulationSpace=ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission=particles.emission;
        emission.rateOverTime=4f;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=Mathf.Max(0.1f,radius*0.88f);
        shape.radiusThickness=1f;
        shape.scale=new Vector3(1f,0.02f,1f);
        shape.randomDirectionAmount=1f;
        ParticleSystem.VelocityOverLifetimeModule velocity=particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=0.28f;
        ParticleSystem.NoiseModule noise=particles.noise;
        noise.enabled=true;
        noise.strength=0.035f;
        noise.frequency=0.55f;

        ParticleSystemRenderer particleRenderer=
            particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode=ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial=BearSpiritParticleMaterial;
        particleRenderer.sortingLayerID=groundRenderer.sortingLayerID;
        particleRenderer.sortingOrder=(sortingReference==null
            ?0:sortingReference.sortingOrder)+22;

        float scale=radius*2f/
                    Mathf.Max(0.01f,groundRenderer.sprite.bounds.size.x);
        PixelSpiritSanctuaryEffect effect=
            root.AddComponent<PixelSpiritSanctuaryEffect>();
        effect.Configure(groundRenderer,particles,scale,duration,stableGround);
        particles.Play();
        return effect;
    }

    public static ParticleSystem PlaySpiritDeerCleanse(
        Transform anchor,SpriteRenderer sortingReference,
        string objectName="Holydeer Purifying Light")
    {
        return PlaySpiritDeerTraitMotes(
            objectName,anchor,sortingReference,
            BearSpiritParticleMaterial,new Color(1f,0.78f,0.2f),Color.white,
            20,0.58f,0.1f,0.18f);
    }

    public static ParticleSystem PlaySpiritDeerMoonHeal(
        Transform anchor,SpriteRenderer sortingReference)
    {
        return PlaySpiritDeerTraitMotes(
            "Lunadeer Moonlight Crescent",anchor,sortingReference,
            SpiritDeerCrescentMaterial,new Color(0.38f,0.64f,1f),
            new Color(0.84f,0.72f,1f),10,0.34f,0.15f,0.24f);
    }

    public static ParticleSystem PlaySpiritDeerForestHaste(
        Transform anchor,SpriteRenderer sortingReference)
    {
        return PlaySpiritDeerTraitMotes(
            "Forestdeer Leaf Wind",anchor,sortingReference,
            SpiritDeerLeafMaterial,new Color(0.18f,0.82f,0.24f),
            new Color(0.76f,1f,0.34f),14,0.4f,0.1f,0.18f);
    }

    public static ParticleSystem PlaySpiritDeerEclipseCurse(
        Transform anchor,SpriteRenderer sortingReference,
        string objectName="Eclipsedeer Shadow Crescent")
    {
        return PlaySpiritDeerTraitMotes(
            objectName,anchor,sortingReference,
            SpiritDeerCrescentMaterial,new Color(0.18f,0.025f,0.28f),
            new Color(0.76f,0.22f,0.92f),12,-0.08f,0.14f,0.23f);
    }

    private static ParticleSystem PlaySpiritDeerTraitMotes(
        string objectName,Transform anchor,SpriteRenderer sortingReference,
        Material material,Color colorMin,Color colorMax,int count,float rise,
        float sizeMin,float sizeMax)
    {
        if(anchor==null)return null;
        GameObject root=new GameObject(objectName);
        root.transform.position=sortingReference!=null&&sortingReference.sprite!=null
            ?sortingReference.bounds.center+Vector3.up*0.08f
            :anchor.position+Vector3.up*0.65f;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.28f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.48f,0.82f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.04f,0.2f);
        main.startSize=new ParticleSystem.MinMaxCurve(sizeMin,sizeMax);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(-0.7f,0.7f);
        main.startColor=new ParticleSystem.MinMaxGradient(colorMin,colorMax);
        main.maxParticles=32;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=0.34f;
        shape.scale=new Vector3(1f,0.62f,0.3f);
        shape.randomDirectionAmount=1f;
        ParticleSystem.VelocityOverLifetimeModule velocity=
            particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=rise;
        ParticleSystem.NoiseModule noise=particles.noise;
        noise.enabled=true;
        noise.strength=0.08f;
        noise.frequency=1.1f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=material;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+40;
        particles.Emit(count);
        particles.Play();
        if(Application.isPlaying)Destroy(root,1.2f);
        return particles;
    }

    public static PixelTyrannoLavaFieldEffect PlayTyrannoLavaField(
        Vector3 center,float radius,float duration,SpriteRenderer sortingReference)
    {
        GameObject root=new GameObject("Magno Lava Footprint");
        root.transform.position=center+Vector3.up*0.026f;
        GameObject groundObject=new GameObject("Lava Ground");
        groundObject.transform.SetParent(root.transform,false);
        groundObject.transform.rotation=Quaternion.Euler(90f,0f,0f);
        SpriteRenderer ground=groundObject.AddComponent<SpriteRenderer>();
        ground.sprite=TyrannoLavaSpreadFrames[0];
        ground.color=Color.white;
        ground.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        ground.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)-94;

        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem eruption=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(eruption);
        eruption.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=eruption.main;
        main.duration=0.42f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.48f,0.82f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.16f,0.42f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.1f,0.22f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(1f,0.22f,0.01f),new Color(1f,0.82f,0.08f));
        main.gravityModifier=0.72f;
        main.maxParticles=72;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission=eruption.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=eruption.shape;
        shape.enabled=true;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=Mathf.Max(0.1f,radius*0.88f);
        shape.radiusThickness=1f;
        shape.scale=new Vector3(1f,0.025f,1f);
        shape.randomDirectionAmount=1f;
        ParticleSystem.VelocityOverLifetimeModule velocity=
            eruption.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=1.18f;
        ParticleSystem.NoiseModule noise=eruption.noise;
        noise.enabled=true;
        noise.strength=0.16f;
        noise.frequency=1.4f;
        ParticleSystemRenderer particleRenderer=
            eruption.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode=ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial=BearSpiritParticleMaterial;
        particleRenderer.sortingLayerID=ground.sortingLayerID;
        particleRenderer.sortingOrder=(sortingReference==null
            ?0:sortingReference.sortingOrder)+26;

        float scale=radius*2f/
                    Mathf.Max(0.01f,TyrannoLavaGroundSprite.bounds.size.x);
        PixelTyrannoLavaFieldEffect effect=
            root.AddComponent<PixelTyrannoLavaFieldEffect>();
        effect.Configure(
            ground,eruption,TyrannoLavaSpreadFrames,scale,duration);
        return effect;
    }

    public static PixelSpiritSanctuaryEffect PlayTyrannoFissure(
        Vector3 center,float radius,float duration,SpriteRenderer sortingReference)
    {
        return PlayTyrannoGroundDecal(
            "Bonrex Ancient Fissure",TyrannoFissureGroundSprite,
            center,radius,duration,sortingReference,-92);
    }

    public static ParticleSystem PlayHealingBurst(
        Transform anchor,SpriteRenderer sortingReference)
    {
        if(anchor==null)return null;
        GameObject root=new GameObject("Common Healing Cross Burst");
        root.transform.position=sortingReference!=null&&sortingReference.sprite!=null
            ?sortingReference.bounds.center+Vector3.up*0.12f
            :anchor.position+Vector3.up*0.65f;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.45f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.55f,0.9f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.08f,0.28f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.15f,0.25f);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(-0.12f,0.12f);
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(0.18f,1f,0.38f),new Color(0.82f,1f,0.72f));
        main.maxParticles=32;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.enabled=true;
        shape.shapeType=ParticleSystemShapeType.Box;
        shape.scale=new Vector3(0.72f,0.22f,0.04f);
        ParticleSystem.VelocityOverLifetimeModule velocity=
            particles.velocityOverLifetime;
        velocity.enabled=true;
        velocity.space=ParticleSystemSimulationSpace.World;
        velocity.y=0.62f;
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=HealingCrossMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+40;
        particles.Emit(22);
        particles.Play();
        if(Application.isPlaying)Destroy(root,1.4f);
        return particles;
    }

    private static PixelSpiritSanctuaryEffect PlayTyrannoGroundDecal(
        string objectName,Sprite sprite,Vector3 center,float radius,float duration,
        SpriteRenderer sortingReference,int sortingOffset)
    {
        GameObject root=new GameObject(objectName);
        root.transform.position=center+Vector3.up*0.024f;
        root.transform.rotation=Quaternion.Euler(90f,0f,0f);
        SpriteRenderer ground=root.AddComponent<SpriteRenderer>();
        ground.sprite=sprite;
        ground.color=Color.white;
        ground.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        ground.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+
                            sortingOffset;
        float scale=radius*2f/Mathf.Max(0.01f,sprite.bounds.size.x);
        PixelSpiritSanctuaryEffect effect=
            root.AddComponent<PixelSpiritSanctuaryEffect>();
        effect.Configure(ground,null,scale,duration);
        return effect;
    }

    public static Sprite SpiderWebGroundSprite
    {
        get
        {
            if(spiderWebGroundSprite!=null)return spiderWebGroundSprite;
            const int size=33;
            const int center=16;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Pixel Spider Web Ground",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                int dx=x-center,dy=y-center;
                float distance=Mathf.Sqrt(dx*dx+dy*dy);
                bool ring=Mathf.Abs(distance-6f)<0.6f||
                          Mathf.Abs(distance-11f)<0.6f||
                          Mathf.Abs(distance-15f)<0.6f;
                bool spoke=(dx==0&&Mathf.Abs(dy)<=15)||
                           (dy==0&&Mathf.Abs(dx)<=15)||
                           (dx==dy&&Mathf.Abs(dx)<=11)||
                           (dx==-dy&&Mathf.Abs(dx)<=11);
                if(ring||spoke)pixels[y*size+x]=new Color32(255,255,255,210);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            spiderWebGroundSprite=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            spiderWebGroundSprite.name="Pixel Spider Web Ground";
            spiderWebGroundSprite.hideFlags=HideFlags.HideAndDontSave;
            return spiderWebGroundSprite;
        }
    }


    private static Sprite KingKongSlamWaveSprite
    {
        get
        {
            if(kingKongSlamWaveSprite!=null)return kingKongSlamWaveSprite;
            const int size=65,center=32;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="KingKong Broken Ground Wave",
                filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color[] pixels=new Color[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                float dx=x-center,dy=y-center;
                float angle=Mathf.Atan2(dy,dx);
                float edge=1f-0.025f*(1f+Mathf.Sin(angle*11f));
                float depth=edge-Mathf.Sqrt(dx*dx+dy*dy)/(size*0.5f);
                if(depth<0f||depth>0.26f)continue;
                float crest=Mathf.Clamp01(1f-depth/0.09f);
                float trail=Mathf.Clamp01(1f-depth/0.26f);
                float broken=Mathf.Abs(Mathf.Sin(angle*7f+depth*12f));
                Color color=Color.Lerp(new Color(0.72f,0.49f,0.26f),
                    new Color(1f,0.94f,0.75f),crest);
                color.a=(crest*0.75f+trail*0.3f)*Mathf.Lerp(0.2f,1f,broken);
                pixels[y*size+x]=color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false,true);
            kingKongSlamWaveSprite=Sprite.Create(texture,new Rect(0,0,size,size),
                new Vector2(0.5f,0.5f),32f,0,SpriteMeshType.FullRect);
            kingKongSlamWaveSprite.name=texture.name;
            kingKongSlamWaveSprite.hideFlags=HideFlags.HideAndDontSave;
            return kingKongSlamWaveSprite;
        }
    }

    public static Sprite SpiritSanctuaryGroundSprite
    {
        get
        {
            if(spiritSanctuaryGroundSprite!=null)return spiritSanctuaryGroundSprite;
            const int size=65,center=32;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Pixel Spirit Sanctuary Ground",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                int dx=x-center,dy=y-center;
                int ax=Mathf.Abs(dx),ay=Mathf.Abs(dy);
                float distance=Mathf.Sqrt(dx*dx+dy*dy);
                if(distance>30.5f)continue;
                bool ring=Mathf.Abs(distance-30f)<0.75f||
                          Mathf.Abs(distance-22f)<0.65f||
                          Mathf.Abs(distance-6f)<0.65f;
                bool ray=(ax<=1||ay<=1||Mathf.Abs(ax-ay)<=1)&&
                         distance>=8f&&distance<=20f;
                bool diamond=Mathf.Abs(ax+ay-11)<=1&&ax<=9&&ay<=9;
                byte alpha=(byte)(ring?235:ray||diamond?175:24);
                pixels[y*size+x]=new Color32(255,255,255,alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            spiritSanctuaryGroundSprite=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            spiritSanctuaryGroundSprite.name=texture.name;
            spiritSanctuaryGroundSprite.hideFlags=HideFlags.HideAndDontSave;
            return spiritSanctuaryGroundSprite;
        }
    }

    public static Sprite MushroomPoisonGroundSprite
    {
        get
        {
            if(mushroomPoisonGroundSprite!=null)return mushroomPoisonGroundSprite;
            const int size=65,center=32;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Doksirum Poison Mycelium Ground",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            Color32 rim=new Color32(54,18,68,185);
            Color32 core=new Color32(94,190,42,225);
            for(int branch=0;branch<12;branch++)
            {
                float baseAngle=branch*Mathf.PI/6f+(branch%2==0?-0.08f:0.1f);
                for(int step=0;step<42;step++)
                {
                    float distance=2f+step*0.7f;
                    float angle=baseAngle+Mathf.Sin(step*0.52f+branch)*0.09f;
                    int x=center+Mathf.RoundToInt(Mathf.Cos(angle)*distance);
                    int y=center+Mathf.RoundToInt(Mathf.Sin(angle)*distance);
                    PaintCrackPixel(pixels,size,x,y,rim,true);
                    PaintCrackPixel(pixels,size,x,y,core,false);
                    if(step==16||step==30)
                    {
                        int nodeRadius=step==16?2:1;
                        for(int oy=-nodeRadius;oy<=nodeRadius;oy++)
                        for(int ox=-nodeRadius;ox<=nodeRadius;ox++)
                            if(ox*ox+oy*oy<=nodeRadius*nodeRadius)
                                PaintCrackPixel(pixels,size,x+ox,y+oy,
                                    new Color32(154,72,188,210),false);
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            mushroomPoisonGroundSprite=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            mushroomPoisonGroundSprite.name=texture.name;
            mushroomPoisonGroundSprite.hideFlags=HideFlags.HideAndDontSave;
            return mushroomPoisonGroundSprite;
        }
    }

    public static Sprite MushroomChildSprite
    {
        get
        {
            if(mushroomChildSprite!=null)return mushroomChildSprite;
            const int width=13,height=15;
            Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Kingmore Child Mushroom",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[width*height];
            int[] capWidths={3,7,11,13,13,11,7};
            for(int y=0;y<capWidths.Length;y++)
            {
                int left=(width-capWidths[y])/2;
                for(int x=left;x<left+capWidths[y];x++)
                    pixels[(height-1-y)*width+x]=new Color32(255,255,255,255);
            }
            for(int y=1;y<=7;y++)
            for(int x=5;x<=7;x++)
                pixels[y*width+x]=new Color32(255,255,255,235);
            for(int x=4;x<=8;x++)pixels[width+x]=new Color32(255,255,255,220);
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            mushroomChildSprite=Sprite.Create(
                texture,new Rect(0f,0f,width,height),new Vector2(0.5f,0.08f),16f);
            mushroomChildSprite.name=texture.name;
            mushroomChildSprite.hideFlags=HideFlags.HideAndDontSave;
            return mushroomChildSprite;
        }
    }

    public static Sprite[] TyrannoLavaSpreadFrames
    {
        get
        {
            if(tyrannoLavaSpreadFrames!=null)return tyrannoLavaSpreadFrames;
            const int size=65,center=32,frameCount=9,spreadFrames=6;
            tyrannoLavaSpreadFrames=new Sprite[frameCount];
            for(int frame=0;frame<frameCount;frame++)
            {
                Texture2D texture=new Texture2D(
                    size,size,TextureFormat.RGBA32,false)
                {
                    name="Pixel Tyranno Lava Spread "+frame,
                    filterMode=FilterMode.Point,
                    wrapMode=TextureWrapMode.Clamp,
                    hideFlags=HideFlags.HideAndDontSave
                };
                Color32[] pixels=new Color32[size*size];
                float progress=frame<spreadFrames
                    ?(frame+1f)/spreadFrames:1f;
                float reach=Mathf.Lerp(3f,29f,progress);
                bool erupting=frame>=spreadFrames;
                Color32 rim=new Color32(115,20,2,235);
                Color32 core=erupting
                    ?new Color32(255,190,18,255)
                    :new Color32(255,83,5,250);
                for(int branch=0;branch<15;branch++)
                {
                    float baseAngle=branch*Mathf.PI*2f/15f+
                        (((branch*13)%7)-3)*0.024f;
                    float branchReach=reach*(0.64f+((branch*11)%37)*0.01f);
                    float curveStrength=0.034f+(branch%4)*0.016f;
                    float curveFrequency=0.38f+(branch%5)*0.065f;
                    for(int step=0;step<58;step++)
                    {
                        float radial=step*0.5f;
                        if(radial>branchReach)break;
                        float angle=baseAngle+
                            Mathf.Sin(step*curveFrequency+branch*1.31f)*curveStrength;
                        int px=center+Mathf.RoundToInt(Mathf.Cos(angle)*radial);
                        int py=center+Mathf.RoundToInt(Mathf.Sin(angle)*radial);
                        Color32 branchColor=branch%3==0
                            ?core:branch%3==1
                                ?new Color32(255,112,6,245)
                                :new Color32(224,52,3,238);
                        PaintLavaPixel(pixels,size,px,py,branchColor,false);
                        if(branch%5==0&&step%5==0)
                            PaintLavaPixel(pixels,size,
                                px+(branch%2==0?1:-1),py,rim,false);
                        int forkStep=22+(branch%4)*6;
                        if(step==forkStep&&branch%2==0)
                        {
                            float forkAngle=angle+(branch%4<2?0.38f:-0.42f);
                            int forkLength=5+branch%5;
                            for(int fork=1;fork<=forkLength;fork++)
                            {
                                int fx=px+Mathf.RoundToInt(
                                    Mathf.Cos(forkAngle)*fork*0.62f);
                                int fy=py+Mathf.RoundToInt(
                                    Mathf.Sin(forkAngle)*fork*0.62f);
                                PaintLavaPixel(
                                    pixels,size,fx,fy,branchColor,false);
                            }
                        }
                        if(erupting&&step>12&&step%19==branch%5)
                            PaintLavaPool(pixels,size,px,py,
                                frame==frameCount-1?1:0,branchColor);
                    }
                }
                if(erupting)PaintLavaPool(
                    pixels,size,center,center,frame==frameCount-1?2:1,core);
                texture.SetPixels32(pixels);
                texture.Apply(false,false);
                Sprite sprite=Sprite.Create(texture,
                    new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
                sprite.name=texture.name;
                sprite.hideFlags=HideFlags.HideAndDontSave;
                tyrannoLavaSpreadFrames[frame]=sprite;
            }
            return tyrannoLavaSpreadFrames;
        }
    }

    public static Sprite TyrannoLavaGroundSprite=>
        TyrannoLavaSpreadFrames[TyrannoLavaSpreadFrames.Length-1];

    private static void PaintLavaPixel(
        Color32[] pixels,int size,int x,int y,Color32 color,bool thick)
    {
        int radius=thick?1:0;
        for(int oy=-radius;oy<=radius;oy++)
        for(int ox=-radius;ox<=radius;ox++)
        {
            int px=x+ox,py=y+oy;
            if(px<0||px>=size||py<0||py>=size)continue;
            pixels[py*size+px]=color;
        }
    }

    private static void PaintLavaPool(
        Color32[] pixels,int size,int centerX,int centerY,int radius,Color32 color)
    {
        for(int y=-radius;y<=radius;y++)
        for(int x=-radius;x<=radius;x++)
            if(x*x+y*y<=radius*radius)
                PaintLavaPixel(pixels,size,centerX+x,centerY+y,color,false);
    }

    public static Sprite TyrannoFissureGroundSprite
    {
        get
        {
            if(tyrannoFissureGroundSprite!=null)return tyrannoFissureGroundSprite;
            const int size=65,center=32;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Pixel Tyranno Ancient Fissure Ground",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            Color32 rim=new Color32(38,14,18,225);
            Color32 core=new Color32(184,62,36,255);
            for(int branch=0;branch<8;branch++)
            {
                float baseAngle=branch*Mathf.PI*0.25f+(branch%2==0?-0.08f:0.12f);
                for(int step=0;step<54;step++)
                {
                    float radius=2f+step*0.53f;
                    float angle=baseAngle+Mathf.Sin(step*0.58f+branch*1.7f)*0.075f;
                    int px=center+Mathf.RoundToInt(Mathf.Cos(angle)*radius);
                    int py=center+Mathf.RoundToInt(Mathf.Sin(angle)*radius);
                    PaintCrackPixel(pixels,size,px,py,rim,true);
                    PaintCrackPixel(pixels,size,px,py,core,false);
                    if(step==25||step==37)
                    {
                        float split=angle+(step==25?0.48f:-0.42f);
                        for(int twig=1;twig<=10;twig++)
                        {
                            int tx=px+Mathf.RoundToInt(Mathf.Cos(split)*twig*0.72f);
                            int ty=py+Mathf.RoundToInt(Mathf.Sin(split)*twig*0.72f);
                            PaintCrackPixel(pixels,size,tx,ty,rim,true);
                            PaintCrackPixel(pixels,size,tx,ty,core,false);
                        }
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            tyrannoFissureGroundSprite=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            tyrannoFissureGroundSprite.name=texture.name;
            tyrannoFissureGroundSprite.hideFlags=HideFlags.HideAndDontSave;
            return tyrannoFissureGroundSprite;
        }
    }

    private static void PaintCrackPixel(
        Color32[] pixels,int size,int x,int y,Color32 color,bool thick)
    {
        int radius=thick?1:0;
        for(int oy=-radius;oy<=radius;oy++)
        for(int ox=-radius;ox<=radius;ox++)
        {
            int px=x+ox,py=y+oy;
            if(px<0||px>=size||py<0||py>=size)continue;
            pixels[py*size+px]=color;
        }
    }

    public static Sprite FreezeIceSprite
    {
        get
        {
            if(freezeIceSprite!=null)return freezeIceSprite;
            const int width=14,height=32;
            Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Common Freeze Ice Spike",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[width*height];
            for(int y=1;y<height-1;y++)
            {
                int half=Mathf.FloorToInt(y<6?Mathf.Lerp(3f,6f,y/6f):6f*(30-y)/24f);
                int ridge=7+Mathf.RoundToInt((y-6)*0.04f);
                for(int x=7-half;x<=Mathf.Min(width-1,7+half);x++)
                {
                    pixels[y*width+x]=x==7-half||x==ridge
                        ?new Color32(222,251,255,240)
                        :x<ridge?new Color32(139,221,245,215)
                        :new Color32(49,132,194,200);
                    // The shared center stays translucent even where several crystal bases overlap.
                    pixels[y*width+x].a=(byte)(pixels[y*width+x].a*
                        Mathf.Lerp(0.15f,1f,Mathf.Clamp01(y/10f)));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            freezeIceSprite=Sprite.Create(texture,
                new Rect(0f,0f,width,height),new Vector2(0.5f,0.06f),32f);
            freezeIceSprite.name=texture.name;
            freezeIceSprite.hideFlags=HideFlags.HideAndDontSave;
            return freezeIceSprite;
        }
    }

    public static Sprite FreezeBodySprite
    {
        get
        {
            if(freezeBodySprite!=null)return freezeBodySprite;
            const int width=48,height=64;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Common Freeze Fractured Ice Body",filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave
            };
            Vector2[] edge={new Vector2(7,7),new Vector2(2,26),new Vector2(6,35),
                new Vector2(4,46),new Vector2(14,47),new Vector2(19,61),new Vector2(25,51),
                new Vector2(35,58),new Vector2(38,42),new Vector2(45,34),
                new Vector2(41,15),new Vector2(31,4)};
            Color32[] faces={new Color32(118,210,236,104),new Color32(170,239,247,92),
                new Color32(67,137,181,118),new Color32(199,245,251,133),
                new Color32(113,196,224,90),new Color32(69,157,199,114)};
            var pixels=new Color32[width*height];
            Vector2 center=new Vector2(23,29);
            for(int i=0;i<edge.Length;i++)
            {
                Vector2 a=edge[i],b=edge[(i+1)%edge.Length];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    Vector2 p=new Vector2(x+.5f,y+.5f);
                    float ab=(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);
                    float bc=(center.x-b.x)*(p.y-b.y)-(center.y-b.y)*(p.x-b.x);
                    float ca=(a.x-center.x)*(p.y-center.y)-(a.y-center.y)*(p.x-center.x);
                    if(!((ab>=0f&&bc>=0f&&ca>=0f)||(ab<=0f&&bc<=0f&&ca<=0f)))continue;
                    pixels[y*width+x]=i%3==0&&Mathf.Abs(ab)/(b-a).magnitude<0.7f
                        ?new Color32(214,249,255,185):faces[i%faces.Length];
                }
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            freezeBodySprite=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),32f);
            freezeBodySprite.hideFlags=HideFlags.HideAndDontSave;
            return freezeBodySprite;
        }
    }

    public static Sprite FreezeShardSprite
    {
        get
        {
            if(freezeShardSprite!=null)return freezeShardSprite;
            const int width=10,height=18;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Common Freeze Crystal Shard",filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave
            };
            var pixels=new Color32[width*height];
            for(int y=1;y<height-1;y++)
            {
                int half=Mathf.Max(0,Mathf.FloorToInt((1f-Mathf.Abs(y-8f)/8f)*4f));
                for(int x=5-half;x<=Mathf.Min(9,5+half);x++)
                    pixels[y*width+x]=x<5?new Color32(204,247,255,225):
                        x==5?new Color32(240,255,255,245):new Color32(79,176,223,190);
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            freezeShardSprite=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),16f);
            freezeShardSprite.hideFlags=HideFlags.HideAndDontSave;
            return freezeShardSprite;
        }
    }

    private static ParticleSystem CreateBearSpiritParticles(
        string objectName,Transform anchor,SpriteRenderer sortingReference,bool loop)
    {
        GameObject root=new GameObject(objectName);
        root.transform.SetParent(anchor,false);
        root.transform.localPosition=sortingReference!=null&&sortingReference.sprite!=null
            ?sortingReference.sprite.bounds.center+Vector3.down*0.12f
            :Vector3.up*0.35f;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;
        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.loop=loop;
        main.playOnAwake=false;
        main.maxParticles=24;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.startColor=new ParticleSystem.MinMaxGradient(
            new Color(1f,0.28f,0.03f),new Color(1f,0.86f,0.18f));
        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BearSpiritParticleMaterial;
        renderer.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+18;
        return particles;
    }

    private static Material BearSpiritParticleMaterial
    {
        get
        {
            if(bearSpiritParticleMaterial!=null)return bearSpiritParticleMaterial;
            Texture2D texture=new Texture2D(5,5,TextureFormat.RGBA32,false)
            {
                name="Bear Spirit Pixel",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[25];
            for(int y=0;y<5;y++)
            for(int x=0;x<5;x++)
                if(Mathf.Abs(x-2)+Mathf.Abs(y-2)<=2)
                    pixels[y*5+x]=Color.white;
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            Shader shader=Shader.Find("Sprites/Default");
            bearSpiritParticleMaterial=new Material(shader)
            {
                name="Bear Spirit Particle Material",
                hideFlags=HideFlags.HideAndDontSave,
                mainTexture=texture
            };
            return bearSpiritParticleMaterial;
        }
    }

    internal static Sprite BearSpiritParticleSprite
    {
        get
        {
            if(bearSpiritParticleSprite!=null)return bearSpiritParticleSprite;
            var texture=(Texture2D)BearSpiritParticleMaterial.mainTexture;
            bearSpiritParticleSprite=Sprite.Create(texture,new Rect(0f,0f,texture.width,texture.height),
                new Vector2(0.5f,0.5f),16f);
            bearSpiritParticleSprite.hideFlags=HideFlags.HideAndDontSave;
            return bearSpiritParticleSprite;
        }
    }

    private static Material HealingCrossMaterial
    {
        get
        {
            if(healingCrossMaterial!=null)return healingCrossMaterial;
            const int size=7;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Common Healing Cross Pixel",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int y=1;y<size-1;y++)
            for(int x=1;x<size-1;x++)
                if(x==3||y==3)pixels[y*size+x]=Color.white;
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            healingCrossSprite=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),16f);
            Shader shader=Shader.Find("Sprites/Default");
            healingCrossMaterial=new Material(shader)
            {
                name="Common Healing Cross Material",
                hideFlags=HideFlags.HideAndDontSave,
                mainTexture=healingCrossSprite.texture
            };
            return healingCrossMaterial;
        }
    }

    private static Material SpiritDeerCrescentMaterial
    {
        get
        {
            if(spiritDeerCrescentMaterial!=null)return spiritDeerCrescentMaterial;
            const int size=9,center=4;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Spirit Deer Crescent Pixel",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                int dx=x-center,dy=y-center;
                int cutX=x-(center+2);
                if(dx*dx+dy*dy<=16&&cutX*cutX+dy*dy>10)
                    pixels[y*size+x]=Color.white;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            spiritDeerCrescentMaterial=new Material(Shader.Find("Sprites/Default"))
            {
                name="Spirit Deer Crescent Material",
                hideFlags=HideFlags.HideAndDontSave,
                mainTexture=texture
            };
            return spiritDeerCrescentMaterial;
        }
    }

    private static Material SpiritDeerLeafMaterial
    {
        get
        {
            if(spiritDeerLeafMaterial!=null)return spiritDeerLeafMaterial;
            const int width=7,height=9;
            int[] rowWidths={1,3,5,7,7,5,3,1,1};
            Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Spirit Deer Leaf Pixel",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[width*height];
            for(int y=0;y<height;y++)
            {
                int left=(width-rowWidths[y])/2;
                for(int x=left;x<left+rowWidths[y];x++)
                    pixels[y*width+x]=Color.white;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            spiritDeerLeafMaterial=new Material(Shader.Find("Sprites/Default"))
            {
                name="Spirit Deer Leaf Material",
                hideFlags=HideFlags.HideAndDontSave,
                mainTexture=texture
            };
            return spiritDeerLeafMaterial;
        }
    }

    private static Sprite[] CreateNinjaKunaiSlashFrames()
    {
        const int size=32;
        int[] lengths={6,14,24,28,18,8};
        Sprite[] result=new Sprite[lengths.Length];
        for(int frame=0;frame<result.Length;frame++)
        {
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Ninja Kunai Slash "+frame,
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            int length=lengths[frame];
            int start=(size-length)/2;
            for(int i=0;i<length;i++)
            {
                int x=start+i;
                int y=start+i;
                pixels[y*size+x]=new Color32(255,245,255,255);
                if(frame>=1&&frame<=4&&y>0)
                    pixels[(y-1)*size+x]=new Color32(204,72,255,170);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            result[frame]=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static Sprite[] CreateMantisScytheSlashFrames()
    {
        const int size=32;
        int[] steps={5,11,18,26,20,10};
        byte[] alpha={170,215,245,255,180,90};
        Sprite[] result=new Sprite[steps.Length];
        for(int frame=0;frame<result.Length;frame++)
        {
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Mantis Scythe Slash "+frame,
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int step=0;step<steps[frame];step++)
            {
                float angle=Mathf.Lerp(78f,-72f,step/25f)*Mathf.Deg2Rad;
                int x=Mathf.RoundToInt(15.5f+Mathf.Cos(angle)*12f);
                int y=Mathf.RoundToInt(15.5f+Mathf.Sin(angle)*12f);
                Color32 bright=new Color32(255,255,255,alpha[frame]);
                Color32 trail=new Color32(210,255,185,(byte)(alpha[frame]*0.55f));
                SetPixel(pixels,size,x,y,bright);
                SetPixel(pixels,size,x-1,y,trail);
                SetPixel(pixels,size,x,y-1,trail);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            result[frame]=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static Sprite[] CreateMammothTuskRamFrames()
    {
        const int size=32;
        int[] starts={0,0,0,0,10,21};
        int[] ends={6,14,24,29,29,29};
        byte[] alpha={175,220,245,255,175,90};
        Sprite[] result=new Sprite[starts.Length];
        for(int frame=0;frame<result.Length;frame++)
        {
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Mammoth Tusk Ram "+frame,
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int step=starts[frame];step<ends[frame];step++)
            {
                float progress=step/28f;
                int x=2+step;
                int gap=Mathf.RoundToInt(
                    Mathf.Lerp(7f,1f,progress)+Mathf.Sin(progress*Mathf.PI)*2f);
                for(int side=-1;side<=1;side+=2)
                {
                    int y=15+side*gap;
                    SetPixel(pixels,size,x,y,
                        new Color32(255,247,205,alpha[frame]));
                    SetPixel(pixels,size,x,y-side,
                        new Color32(218,174,95,(byte)(alpha[frame]*0.62f)));
                }
            }
            if(frame==3)
                for(int offset=-3;offset<=3;offset++)
                {
                    SetPixel(pixels,size,30,15+offset,
                        new Color32(255,255,235,(byte)(255-Mathf.Abs(offset)*42)));
                    SetPixel(pixels,size,30-Mathf.Abs(offset),15,
                        new Color32(255,225,145,(byte)(255-Mathf.Abs(offset)*42)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            result[frame]=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static Sprite[] CreateBeetleHornUppercutFrames()
    {
        const int size=32;
        int[] starts={0,0,0,0,10,21};
        int[] ends={6,14,23,29,29,29};
        byte[] alpha={165,210,240,255,170,80};
        Sprite[] result=new Sprite[starts.Length];
        for(int frame=0;frame<result.Length;frame++)
        {
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Beetle Horn Uppercut "+frame,
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int step=starts[frame];step<ends[frame];step++)
            {
                float t=step/28f;
                float inverse=1f-t;
                int x=Mathf.RoundToInt(inverse*inverse*5f+
                    2f*inverse*t*28f+t*t*24f);
                int y=Mathf.RoundToInt(inverse*inverse*4f+
                    2f*inverse*t*5f+t*t*29f);
                SetPixel(pixels,size,x,y,new Color32(255,248,205,alpha[frame]));
                SetPixel(pixels,size,x-1,y,new Color32(
                    255,126,18,(byte)(alpha[frame]*0.74f)));
                SetPixel(pixels,size,x,y-1,new Color32(
                    211,66,8,(byte)(alpha[frame]*0.48f)));
            }
            if(frame==BeetleHornUppercutImpactFrame)
                for(int offset=-3;offset<=3;offset++)
                {
                    SetPixel(pixels,size,24+offset,29,
                        new Color32(255,238,120,(byte)(255-Mathf.Abs(offset)*45)));
                    SetPixel(pixels,size,24,29+offset,
                        new Color32(255,255,225,(byte)(255-Mathf.Abs(offset)*45)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            result[frame]=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static Sprite[] CreateGriffinEagleClawFrames()
    {
        const int size=32;
        int[] starts={0,0,0,0,8,18};
        int[] ends={5,11,18,26,26,26};
        byte[] alpha={170,215,245,255,175,85};
        Sprite[] result=new Sprite[starts.Length];
        for(int frame=0;frame<result.Length;frame++)
        {
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Griffin Eagle Claw "+frame,
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int claw=-1;claw<=1;claw++)
            for(int step=starts[frame];step<ends[frame];step++)
            {
                int x=Mathf.RoundToInt(6f+step*0.72f)+claw*4;
                int y=GriffinEagleClawY(step);
                SetPixel(pixels,size,x,y,
                    new Color32(255,238,150,alpha[frame]));
                SetPixel(pixels,size,x,y+1,
                    new Color32(255,168,35,(byte)(alpha[frame]*0.58f)));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            result[frame]=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static Sprite[] CreateBeetleHornThrustFrames()
    {
        const int size=32;
        int[] starts={0,0,0,0,10,21};
        int[] ends={6,14,23,29,29,29};
        byte[] alpha={165,210,240,255,170,80};
        Sprite[] result=new Sprite[starts.Length];
        for(int frame=0;frame<result.Length;frame++)
        {
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Beetle Horn Thrust "+frame,
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int step=starts[frame];step<ends[frame];step++)
            {
                float t=step/28f;
                int x=2+step;
                int y=15+Mathf.RoundToInt(Mathf.Sin(t*Mathf.PI)*1.5f);
                int halfWidth=Mathf.RoundToInt(Mathf.Lerp(2f,0f,t));
                for(int offset=-halfWidth;offset<=halfWidth;offset++)
                    SetPixel(pixels,size,x,y+offset,
                        offset==0
                            ?new Color32(255,248,205,alpha[frame])
                            :new Color32(255,126,18,(byte)(alpha[frame]*0.6f)));
                SetPixel(pixels,size,x-1,y-2,new Color32(
                    211,66,8,(byte)(alpha[frame]*0.42f)));
            }
            if(frame==BeetleHornThrustImpactFrame)
                for(int offset=-3;offset<=3;offset++)
                {
                    SetPixel(pixels,size,30,16+offset,
                        new Color32(255,238,120,(byte)(255-Mathf.Abs(offset)*45)));
                    SetPixel(pixels,size,30-Mathf.Abs(offset),16,
                        new Color32(255,255,225,(byte)(255-Mathf.Abs(offset)*45)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            result[frame]=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static Sprite[] CreateTyrannoKingJawBiteFrames()
    {
        const int size=32;
        int[] upperY={3,5,7,8,5};
        int[] lowerY={28,26,24,23,26};
        int[] fangX={7,16,25};
        Sprite[] result=new Sprite[6];
        for(int frame=0;frame<result.Length;frame++)
        {
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Tyranno King Jaw Bite "+frame,
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            if(frame<5)
            {
                if(frame==4)
                    for(int i=0;i<fangX.Length;i++)
                    {
                        DrawTyrannoFang(pixels,size,fangX[i],8,true,90);
                        DrawTyrannoFang(pixels,size,fangX[i],23,false,90);
                    }
                for(int i=0;i<fangX.Length;i++)
                {
                    DrawTyrannoFang(pixels,size,fangX[i],upperY[frame],true,255);
                    DrawTyrannoFang(pixels,size,fangX[i],lowerY[frame],false,255);
                    if(frame==1||frame==2)
                    {
                        int trail=frame==1?2:3;
                        for(int j=0;j<trail;j++)
                        {
                            SetPixel(pixels,size,fangX[i],upperY[frame]+9+j*2,
                                new Color32(55,110,255,(byte)(125-j*28)));
                            SetPixel(pixels,size,fangX[i],lowerY[frame]-9-j*2,
                                new Color32(55,110,255,(byte)(125-j*28)));
                        }
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            result[frame]=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static void DrawTyrannoFang(
        Color32[] pixels,int size,int centerX,int baseY,bool pointsDown,byte alpha)
    {
        Color32 shadow=new Color32(22,72,219,alpha);
        Color32 cyan=new Color32(35,231,255,alpha);
        Color32 white=new Color32(255,255,255,alpha);
        for(int row=0;row<8;row++)
        {
            int halfWidth=row<3?2:row<6?1:0;
            int y=baseY+(pointsDown?row:-row);
            for(int x=centerX-halfWidth-1;x<=centerX+halfWidth;x++)
                SetPixel(pixels,size,x,y,shadow);
            for(int x=centerX-halfWidth;x<=centerX+halfWidth;x++)
                SetPixel(pixels,size,x,y,cyan);
            if(row>0&&row<6)
                for(int x=centerX-Mathf.Max(0,halfWidth-1);
                    x<=centerX+Mathf.Max(0,halfWidth-1);x++)
                    SetPixel(pixels,size,x,y,white);
        }
    }

    private static Sprite[] CreateBearMountainCrescentFrames()
    {
        const int width=96,height=80;
        const float cx=27f,cy=40f,rx=52f,ry=27f,start=2.05f,end=-.88f;
        float[] head={.18f,.62f,1f,1f,1f,1f,1f};
        float[] tail={0f,0f,0f,.15f,.4f,.68f,.87f};
        byte[] opacity={110,220,255,225,160,95,40};
        var frames=new Sprite[head.Length];
        for(int frame=0;frame<frames.Length;frame++)
        {
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float nx=(x-cx)/rx,ny=(y-cy)/ry;
                float u=(start-Mathf.Atan2(ny,nx))/(start-end);
                if(u<tail[frame]||u>head[frame])continue;
                float along=(u-tail[frame])/(head[frame]-tail[frame]);
                float thickness=28f*Mathf.Pow(Mathf.Sin(along*Mathf.PI),1.25f)*(.35f+.8f*along);
                float depth=(1f-Mathf.Sqrt(nx*nx+ny*ny))*rx;
                if(depth<0f||depth>thickness)continue;
                // A single filled blade, with a sharp ivory lip and a cool shadow.
                // The receding tail breaks in two-pixel blocks, never a soft blur.
                int grain=((x/2)*17+(y/2)*29)%19;
                if(frame>=3&&along<.45f&&grain<(frame-2)*3*(1f-along/.45f))continue;
                float band=depth/Mathf.Max(.01f,thickness);
                Color32 color=depth<1.6f?new Color32(255,250,224,255):
                    band<.38f?new Color32(248,212,137,255):
                    band<.72f?new Color32(118,205,197,210):new Color32(47,110,119,135);
                color.a=(byte)(color.a*opacity[frame]/255);
                pixels[y*width+x]=color;
            }
            if(frame>=2&&frame<=5)for(int chip=0;chip<4;chip++)
            {
                float u=Mathf.Clamp01(tail[frame]+chip*.1f);
                float angle=Mathf.Lerp(start,end,u);
                float drift=(frame-1)*1.8f+chip;
                int px=Mathf.RoundToInt(cx+Mathf.Cos(angle)*(rx+drift));
                int py=Mathf.RoundToInt(cy+Mathf.Sin(angle)*(ry+drift));
                for(int y=0;y<2;y++)for(int x=0;x<2;x++)
                    if(px+x>=0&&px+x<width&&py+y>=0&&py+y<height)
                        pixels[(py+y)*width+px+x]=new Color32(170,223,209,(byte)(opacity[frame]*.65f));
            }
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
                {name="BearMountainCrescent_"+frame,filterMode=FilterMode.Point,
                 wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            frames[frame]=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(cx/width,cy/height),32f);
            frames[frame].name=texture.name;frames[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return frames;
    }

    public static PixelCombatEffect PlayMonkeyFruitPop(Vector3 position,SpriteRenderer reference,Color color,bool heavy,float scale=1f
        ,bool secondary=false
    )
    {
        if(monkeyFruitPopFrames==null)
        {
            const int size=40,center=20;
            monkeyFruitPopFrames=new Sprite[8];
            for(int frame=0;frame<monkeyFruitPopFrames.Length;frame++)
            {
                var pixels=new Color32[size*size];
                for(int y=1;y<size-1;y++)for(int x=1;x<size-1;x++)
                {
                    float dx=x-center,dy=y-center;
                    float angle=Mathf.Atan2(dy,dx);
                    float edge=(frame==0?6f:frame==1?12f:14f+frame*.45f)*
                        (1f+.16f*Mathf.Sin(angle*5f)+.1f*Mathf.Cos(angle*7f));
                    float depth=Mathf.Sqrt(dx*dx+dy*dy)/edge;
                    if(depth>1f)continue;
                    int grain=(x/3*17+y/3*11)%13;
                    if(frame>=3&&(grain<(frame-2)*2||depth<(frame-2)*.19f))continue;
                    byte alpha=(byte)(frame<3?255:255-(frame-2)*43);
                    pixels[y*size+x]=depth<.5f?new Color32(255,255,235,alpha):
                        depth<.77f?new Color32(255,233,156,alpha):new Color32(187,129,61,alpha);
                }
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
                    {name="Monkey Fruit Pop "+frame,filterMode=FilterMode.Point,
                     wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                texture.SetPixels32(pixels);texture.Apply(false,true);
                Sprite sprite=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,20f);
                sprite.hideFlags=HideFlags.HideAndDontSave;monkeyFruitPopFrames[frame]=sprite;
            }
        }
        float speed=heavy?26f:40f;
        float opacity=1f;
        Sprite[] playback=monkeyFruitPopFrames;
        if(CombatReadabilityPresentation.RefinedSkills)
        {
            // Show the full impact before clearing its center, without extending the eight-frame tail.
            if(monkeyFruitPopReadabilityFrames==null)
                monkeyFruitPopReadabilityFrames=new[]{monkeyFruitPopFrames[0],monkeyFruitPopFrames[2],
                    monkeyFruitPopFrames[3],monkeyFruitPopFrames[3],monkeyFruitPopFrames[4],
                    monkeyFruitPopFrames[5],monkeyFruitPopFrames[6],monkeyFruitPopFrames[7]};
            playback=monkeyFruitPopReadabilityFrames;
        }
        if(secondary&&CombatReadabilityPresentation.RefinedSkills){speed=56f;scale*=.85f;opacity=.62f;}
        var effect=Play(heavy?"Monkey Heavy Fruit Pop":"Monkey Pellet Pop",
            playback,speed,null,reference,(heavy?1.15f:.38f)*scale,36);
        effect.transform.position=position;
        Color tint=Color.Lerp(color,Color.white,heavy?.6f:.35f);tint.a*=opacity;
        effect.SetColor(tint);
        return effect;
    }

    private static Sprite[] CreateBearPawImpactFrames()
    {
        const int size=64,center=32;
        float[] radii={9f,17f,20f,22f,23f,24f,25f};
        byte[] opacity={255,255,240,190,125,70,25};
        float[] lobes={1.2f,.63f,1.02f,.7f,1.15f,.62f,.9f,.68f,
            .96f,.65f,.84f,.6f,1.12f,.68f,.88f,.64f};
        Vector2[] chips={new Vector2(.96f,.1f),new Vector2(.63f,.68f),
            new Vector2(.72f,-.56f),new Vector2(-.62f,.52f),new Vector2(-.48f,-.65f)};
        var frames=new Sprite[radii.Length];
        for(int frame=0;frame<frames.Length;frame++)
        {
            var pixels=new Color32[size*size];
            for(int y=4;y<size-4;y++)for(int x=4;x<size-4;x++)
            {
                float dx=(x-center)/(frame==0?.55f:1.15f),dy=y-center;
                float sector=(Mathf.Atan2(dy,dx)+Mathf.PI)*8f/Mathf.PI;
                int side=Mathf.FloorToInt(sector)%lobes.Length;
                float radius=radii[frame]*Mathf.Lerp(lobes[side],lobes[(side+1)%lobes.Length],sector-Mathf.Floor(sector));
                float depth=Mathf.Sqrt(dx*dx+dy*dy)/radius;
                if(depth>1f)continue;
                // A solid, chunky contact breaks into broad pieces; no outlined ring or blade.
                int grain=((x/3)*17+(y/3)*23)%19;
                if(frame>=3&&(depth<.26f+(frame-3)*.14f||grain<(frame-2)*3))continue;
                pixels[y*size+x]=depth<.48f?new Color32(255,253,224,opacity[frame]):
                    depth<.78f?new Color32(255,207,103,opacity[frame]):
                    new Color32(190,116,38,(byte)(opacity[frame]*.8f));
            }
            // Short blunt chips carry the force out of the contact, without blade trails or rings.
            for(int chip=0;chip<chips.Length;chip++)
            {
                Vector2 p=new Vector2(center,center)+chips[chip]*(13f+frame*2.8f);
                int width=frame<3?4:3,height=frame<3?3:2;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    SetPixel(pixels,size,Mathf.RoundToInt(p.x)+x,Mathf.RoundToInt(p.y)+y,
                        new Color32(255,225,148,opacity[frame]));
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {name="BearPawImpact_"+frame,filterMode=FilterMode.Point,
                 wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            frames[frame]=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,32f);
            frames[frame].name=texture.name;frames[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return frames;
    }

    private static Sprite[] CreateBearEmpoweredPawFrames()
    {
        const int size=48;
        float[] progress={.3f,.68f,1f,1f,1f,1f,1f};
        float[] tail={0f,0f,0f,.16f,.4f,.65f,.85f};
        byte[] alpha={140,225,255,205,155,100,45};
        var result=new Sprite[progress.Length];
        for(int frame=0;frame<result.Length;frame++)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {name="BearSpiritClaw_"+frame,filterMode=FilterMode.Point,
                 wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color32[size*size];
            for(int claw=-1;claw<=1;claw++)
                DrawBearRagingClaw(pixels,size,claw*7,progress[frame],alpha[frame],
                    new Color32(255,240,187,255),new Color32(172,116,48,180),tail[frame]);
            texture.SetPixels32(pixels);texture.Apply(false,true);
            result[frame]=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),32f);
            result[frame].name=texture.name;result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static Sprite[][] CreateBearRagingStrikeFrames()
    {
        const int strikeTypes=3,frameCount=5,size=48;
        float[] progress={0.32f,0.68f,1f,1f,1f};
        byte[] alpha={150,230,255,170,70};
        Color32[] cores={
            new Color32(255,194,48,255),
            new Color32(255,72,22,255),
            new Color32(255,248,205,255)};
        Color32[] glows={
            new Color32(190,46,8,140),
            new Color32(126,8,3,155),
            new Color32(255,116,18,180)};
        Sprite[][] result=new Sprite[strikeTypes][];
        for(int strike=0;strike<strikeTypes;strike++)
        {
            result[strike]=new Sprite[frameCount];
            for(int frame=0;frame<frameCount;frame++)
            {
                Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {
                    name=$"BearRagingStrike{strike+1}_{frame}",
                    filterMode=FilterMode.Point,
                    wrapMode=TextureWrapMode.Clamp,
                    hideFlags=HideFlags.HideAndDontSave
                };
                Color32[] pixels=new Color32[size*size];
                for(int claw=0;claw<=strike;claw++)
                {
                    int offset=Mathf.RoundToInt((claw-strike*0.5f)*6f);
                    DrawBearRagingClaw(
                        pixels,size,offset,progress[frame],alpha[frame],
                        cores[strike],glows[strike]);
                }
                if(frame>=1&&frame<=3)
                    DrawBearRagingImpactSpark(
                        pixels,size,40,36,alpha[frame],cores[strike],strike+2);
                texture.SetPixels32(pixels);
                texture.Apply(false,true);
                result[strike][frame]=Sprite.Create(
                    texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
                result[strike][frame].name=texture.name;
                result[strike][frame].hideFlags=HideFlags.HideAndDontSave;
            }
        }
        return result;
    }

    private static void DrawBearRagingClaw(
        Color32[] pixels,int size,int offset,float progress,byte alpha,
        Color32 core,Color32 glow,float tailProgress=0f)
    {
        int length=Mathf.Clamp(Mathf.RoundToInt(34f*progress),2,34);
        Color32 fadedCore=new Color32(core.r,core.g,core.b,alpha);
        Color32 fadedGlow=new Color32(
            glow.r,glow.g,glow.b,(byte)(glow.a*alpha/255));
        for(int step=Mathf.RoundToInt(34f*tailProgress);step<length;step++)
        {
            int x=6+step;
            int zigzag=(step/5)%2;
            int y=10+Mathf.RoundToInt(step*0.72f)+offset+zigzag;
            SetPixel(pixels,size,x,y-2,fadedGlow);
            SetPixel(pixels,size,x,y+2,fadedGlow);
            SetPixel(pixels,size,x-1,y-1,fadedGlow);
            SetPixel(pixels,size,x+1,y+1,fadedGlow);
            SetPixel(pixels,size,x,y-1,fadedCore);
            SetPixel(pixels,size,x,y,fadedCore);
            if(step>length*0.45f)SetPixel(pixels,size,x,y+1,fadedCore);
        }
    }

    private static void DrawBearRagingImpactSpark(
        Color32[] pixels,int size,int centerX,int centerY,byte alpha,
        Color32 color,int radius)
    {
        Color32 faded=new Color32(color.r,color.g,color.b,alpha);
        for(int offset=-radius;offset<=radius;offset++)
        {
            if(Mathf.Abs(offset)==radius-1)continue;
            SetPixel(pixels,size,centerX+offset,centerY,faded);
            SetPixel(pixels,size,centerX,centerY+offset,faded);
        }
    }

    private static Sprite[] CreateBearRagingGroundFractureFrames()
    {
        const int frameCount=6,size=65,center=32;
        float[] progress={0.18f,0.42f,0.68f,0.9f,1f,1f};
        byte[] alpha={170,230,255,255,195,80};
        int[,] crackEnds={
            {3,7},{2,27},{8,54},{22,63},{42,62},
            {59,55},{63,36},{60,13},{47,2},{26,1}};
        Sprite[] result=new Sprite[frameCount];
        for(int frame=0;frame<frameCount;frame++)
        {
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name=$"BearRagingPawFracture_{frame}",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int crack=0;crack<crackEnds.GetLength(0);crack++)
                DrawBearRagingCrack(
                    pixels,size,center,center,crackEnds[crack,0],crackEnds[crack,1],
                    progress[frame],alpha[frame],crack);
            DrawBearRagingPawStamp(pixels,size,progress[frame],alpha[frame]);
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            result[frame]=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            result[frame].name=texture.name;
            result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static void DrawBearRagingCrack(
        Color32[] pixels,int size,int startX,int startY,int endX,int endY,
        float progress,byte alpha,int seed)
    {
        int targetX=Mathf.RoundToInt(Mathf.Lerp(startX,endX,progress));
        int targetY=Mathf.RoundToInt(Mathf.Lerp(startY,endY,progress));
        int steps=Mathf.Max(Mathf.Abs(targetX-startX),Mathf.Abs(targetY-startY));
        Color32 glow=new Color32(255,92,10,(byte)(alpha*0.42f));
        Color32 core=new Color32(88,12,4,alpha);
        for(int step=2;step<=steps;step++)
        {
            float t=step/(float)Mathf.Max(1,steps);
            int x=Mathf.RoundToInt(Mathf.Lerp(startX,targetX,t));
            int y=Mathf.RoundToInt(Mathf.Lerp(startY,targetY,t));
            int jitter=((step/4+seed)%3)-1;
            if(Mathf.Abs(endX-startX)>Mathf.Abs(endY-startY))y+=jitter;
            else x+=jitter;
            SetPixel(pixels,size,x-1,y,glow);
            SetPixel(pixels,size,x+1,y,glow);
            SetPixel(pixels,size,x,y-1,glow);
            SetPixel(pixels,size,x,y+1,glow);
            SetPixel(pixels,size,x,y,core);
        }
    }

    private static void DrawBearRagingPawStamp(
        Color32[] pixels,int size,float progress,byte alpha)
    {
        float stampScale=Mathf.Clamp01(progress*1.8f);
        DrawBearRagingPad(pixels,size,32,27,
            Mathf.RoundToInt(9f*stampScale),Mathf.RoundToInt(8f*stampScale),alpha);
        int[,] toes={{19,39},{27,44},{37,44},{46,39}};
        for(int toe=0;toe<toes.GetLength(0);toe++)
            DrawBearRagingPad(pixels,size,toes[toe,0],toes[toe,1],
                Mathf.Max(1,Mathf.RoundToInt(4f*stampScale)),
                Mathf.Max(1,Mathf.RoundToInt(5f*stampScale)),alpha);
    }

    private static void DrawBearRagingPad(
        Color32[] pixels,int size,int centerX,int centerY,int radiusX,int radiusY,
        byte alpha)
    {
        Color32 edge=new Color32(255,174,28,alpha);
        Color32 fill=new Color32(105,12,5,(byte)(alpha*0.88f));
        for(int y=-radiusY;y<=radiusY;y++)
        for(int x=-radiusX;x<=radiusX;x++)
        {
            float normalized=x*x/(float)Mathf.Max(1,radiusX*radiusX)+
                             y*y/(float)Mathf.Max(1,radiusY*radiusY);
            if(normalized>1f)continue;
            SetPixel(pixels,size,centerX+x,centerY+y,
                normalized>0.62f?edge:fill);
        }
    }

    private static Sprite[] CreateBearKingRoarBurstFrames(bool ending,bool avatar=false)
    {
        const int frameCount=14,width=48,height=32;
        var result=new Sprite[frameCount];
        for(int frame=0;frame<frameCount;frame++)
        {
            float progress=frame/(float)(frameCount-1);
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
            {
                float dx=(x-23.5f)/22f,dy=(y-14f)/12f;
                // A wide crest, shaded body and broken trailing edge, without a closed outline.
                float bow=.26f-.62f*dx*dx;
                float shape=dx*dx+Mathf.Pow((dy-bow)/(avatar?.9f:.65f),2f);
                if(shape>1f)continue;
                int grain=(x/3*17+y/2*13)%19;
                if(progress>.48f&&grain<(progress-.48f)*32f)continue;
                float fade=1f-Mathf.SmoothStep(.42f,1f,progress);
                bool crest=dy>bow+(avatar?-.02f:.12f)&&dy<bow+(avatar?.55f:.42f);
                byte alpha=(byte)((crest?215f:dy>bow-.12f?145f:65f)*fade*
                    Mathf.Clamp01((1f-Mathf.Abs(dx))*4f));
                Color32 color=crest?
                    new Color32(255,ending?(byte)242:(byte)255,ending?(byte)201:(byte)255,alpha):
                    new Color32(ending?(byte)211:(byte)140,ending?(byte)180:(byte)201,ending?(byte)111:(byte)221,alpha);
                if(avatar)
                {
                    alpha=(byte)((crest?245f:dy>bow-.25f?185f:105f)*fade*
                        Mathf.Clamp01((1f-Mathf.Abs(dx))*4f));
                    color=crest?new Color32(224,255,255,alpha):
                        dy>bow-.25f?new Color32(92,209,231,alpha):new Color32(36,93,142,alpha);
                }
                if(dy<bow-.25f&&grain<8)continue;
                pixels[y*width+x]=color;
            }
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){
                name=(avatar?"BearAvatarPressure_":ending?"BearKingFinalPressure_":"BearKingOpeningPressure_")+frame,
                filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            result[frame]=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),32f);
            result[frame].name=texture.name;result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }

    private static void SetPixel(
        Color32[] pixels,int size,int x,int y,Color32 color)
    {
        if(x<0||x>=size||y<0||y>=size)return;
        int index=y*size+x;
        if(color.a>=pixels[index].a)pixels[index]=color;
    }

    private static Sprite MammothPathGroundSprite
    {
        get
        {
            if(mammothPathGroundSprite!=null)return mammothPathGroundSprite;
            const int width=17,height=65,center=8;
            Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Mammoth Trait Path Ground",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[width*height];
            for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
            {
                int edge=Mathf.Min(x,width-1-x);
                if(edge<=1)
                    pixels[y*width+x]=new Color32(255,255,255,145);
                else
                    pixels[y*width+x]=new Color32(255,255,255,32);
                int crack=center+Mathf.RoundToInt(Mathf.Sin(y*0.27f)*2.5f);
                bool spine=Mathf.Abs(x-crack)<=(y%7==0?1:0);
                bool branch=y%13>=7&&y%13<=9&&
                            Mathf.Abs(x-crack)==y%13-6;
                bool rune=y%18>=2&&y%18<=6&&
                          Mathf.Abs(x-center)+Mathf.Abs(y%18-4)==3;
                if(spine||branch||rune)
                    pixels[y*width+x]=new Color32(255,255,255,225);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            mammothPathGroundSprite=Sprite.Create(
                texture,new Rect(0f,0f,width,height),new Vector2(0.5f,0.5f),32f);
            mammothPathGroundSprite.name=texture.name;
            mammothPathGroundSprite.hideFlags=HideFlags.HideAndDontSave;
            return mammothPathGroundSprite;
        }
    }

    private static Sprite BeetleHornShockwaveSprite
    {
        get
        {
            if(beetleHornShockwaveSprite!=null)return beetleHornShockwaveSprite;
            const int size=65,center=32;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Jangpung Horn Shockwave Ground",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                int dx=x-center,dy=y-center;
                float distance=Mathf.Sqrt(dx*dx+dy*dy);
                if(Mathf.Abs(distance-25f)<0.65f)
                    pixels[y*size+x]=new Color32(255,255,255,170);
            }
            for(int spoke=0;spoke<8;spoke++)
            {
                float angle=spoke*Mathf.PI*0.25f;
                Vector2 side=new Vector2(-Mathf.Sin(angle),Mathf.Cos(angle));
                for(int step=7;step<=30;step++)
                {
                    int x=center+Mathf.RoundToInt(Mathf.Cos(angle)*step);
                    int y=center+Mathf.RoundToInt(Mathf.Sin(angle)*step);
                    SetPixel(pixels,size,x,y,new Color32(255,255,255,235));
                    if(step>=20)
                    {
                        int width=step>=27?2:1;
                        for(int offset=1;offset<=width;offset++)
                        {
                            SetPixel(pixels,size,
                                x+Mathf.RoundToInt(side.x*offset),
                                y+Mathf.RoundToInt(side.y*offset),
                                new Color32(255,255,255,150));
                            SetPixel(pixels,size,
                                x-Mathf.RoundToInt(side.x*offset),
                                y-Mathf.RoundToInt(side.y*offset),
                                new Color32(255,255,255,150));
                        }
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            beetleHornShockwaveSprite=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            beetleHornShockwaveSprite.name=texture.name;
            beetleHornShockwaveSprite.hideFlags=HideFlags.HideAndDontSave;
            return beetleHornShockwaveSprite;
        }
    }

    private static Sprite BeetleFortressAuraSprite
    {
        get
        {
            if(beetleFortressAuraSprite!=null)return beetleFortressAuraSprite;
            const int size=33,center=16;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {
                name="Atlagon Fortress Hex Aura",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            for(int ring=0;ring<2;ring++)
            {
                float radius=ring==0?15f:10f;
                Vector2[] vertices=new Vector2[6];
                for(int i=0;i<6;i++)
                {
                    float angle=(60f*i-30f)*Mathf.Deg2Rad;
                    vertices[i]=new Vector2(
                        center+Mathf.Cos(angle)*radius,
                        center+Mathf.Sin(angle)*radius);
                }
                for(int edge=0;edge<6;edge++)
                for(int step=0;step<=16;step++)
                {
                    float t=step/16f;
                    int x=Mathf.RoundToInt(Mathf.Lerp(
                        vertices[edge].x,vertices[(edge+1)%6].x,t));
                    int y=Mathf.RoundToInt(Mathf.Lerp(
                        vertices[edge].y,vertices[(edge+1)%6].y,t));
                    Color32 color=ring==0
                        ?new Color32(255,255,255,220)
                        :new Color32(255,255,255,105);
                    SetPixel(pixels,size,x,y,color);
                    if(ring==0)SetPixel(pixels,size,x,y-1,
                        new Color32(255,255,255,105));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            beetleFortressAuraSprite=Sprite.Create(
                texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),32f);
            beetleFortressAuraSprite.name=texture.name;
            beetleFortressAuraSprite.hideFlags=HideFlags.HideAndDontSave;
            return beetleFortressAuraSprite;
        }
    }


    private static Sprite NinjaLinePixelSprite
    {
        get
        {
            if(ninjaLinePixelSprite!=null)return ninjaLinePixelSprite;
            const int width=16;
            const int height=3;
            Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Ninja Clean Cut",
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[width*height];
            for(int x=1;x<width-1;x++)
            {
                pixels[width+x]=new Color32(255,245,255,255);
                if(x>=3&&x<=width-4)
                    pixels[x]=new Color32(204,72,255,130);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            ninjaLinePixelSprite=Sprite.Create(
                texture,new Rect(0f,0f,width,height),new Vector2(0.5f,0.5f),16f);
            ninjaLinePixelSprite.name=texture.name;
            ninjaLinePixelSprite.hideFlags=HideFlags.HideAndDontSave;
            return ninjaLinePixelSprite;
        }
    }

    public static Sprite[] LoadFrames(string resourcePath)
    {
        if(FrameCache.TryGetValue(resourcePath,out Sprite[] cached))return cached;
        Sprite[] loaded=Resources.LoadAll<Sprite>(resourcePath)
            .OrderBy(sprite=>sprite.name,StringComparer.Ordinal)
            .ToArray();
        if(loaded.Length>0)FrameCache[resourcePath]=loaded;
        return loaded;
    }

    private void Configure(
        Sprite[] effectFrames,
        float effectFramesPerSecond,
        Transform followAnchor,
        SpriteRenderer followRenderer,
        float scale,
        int sortingOffset)
    {
        skillClock=CombatSkillClock.Capture();
        readabilityBiteVersion++;
        readabilityBiteHitFrame=-1;
        ClearContactImpact();
        pooled=false;
        GetComponent<PixelBearRoarPressure>()?.ResetForPool();
        fangLockStatuses=null;
        fangLockBloodPulseRemaining=0f;
        elapsed=0f;
        faceCamera=true;
        cameraRotationOffset=Quaternion.identity;
        frames=effectFrames;
        framesPerSecond=Mathf.Max(1f,effectFramesPerSecond);
        anchor=followAnchor;
        anchorRenderer=followRenderer;
        if(effectRenderer==null)
        {
            effectRenderer=GetComponent<SpriteRenderer>();
            if(effectRenderer==null)
                effectRenderer=gameObject.AddComponent<SpriteRenderer>();
        }
        effectRenderer.enabled=true;
        SetColor(PixelSpiritConcert.Tint(followRenderer,Color.white));
        effectRenderer.flipX=false;
        effectRenderer.flipY=false;
        effectRenderer.sprite=frames[0];
        effectRenderer.sortingLayerID=
            anchorRenderer==null?0:anchorRenderer.sortingLayerID;
        effectRenderer.sortingOrder=
            (anchorRenderer==null?0:anchorRenderer.sortingOrder)+sortingOffset;
        transform.rotation=Quaternion.identity;
        transform.localScale=Vector3.one*scale*PreviewVisualScale;
        UpdateTransform();
    }

    private static PixelCombatEffect Play(
        string objectName,
        string resourcePath,
        float effectFramesPerSecond,
        Transform anchor,
        SpriteRenderer anchorRenderer,
        float scale,
        int sortingOffset)
    {
        return Play(objectName,LoadFrames(resourcePath),effectFramesPerSecond,
            anchor,anchorRenderer,scale,sortingOffset);
    }

    private static PixelCombatEffect Play(
        string objectName,
        Sprite[] loaded,
        float effectFramesPerSecond,
        Transform anchor,
        SpriteRenderer anchorRenderer,
        float scale,
        int sortingOffset)
    {
        if(loaded.Length==0)return null;

        PixelCombatEffect effect=Rent(objectName);
        effect.Configure(
            loaded,
            effectFramesPerSecond,
            anchor,
            anchorRenderer,
            scale,
            sortingOffset);
        return effect;
    }

    private static PixelCombatEffect Rent(string objectName)
    {
        while(AvailableEffects.Count>0)
        {
            PixelCombatEffect cached=AvailableEffects.Pop();
            if(cached==null)continue;
            cached.gameObject.name=objectName;
            cached.gameObject.SetActive(true);
            return cached;
        }
        GameObject instance=new GameObject(objectName);
        return instance.AddComponent<PixelCombatEffect>();
    }

    private void Update()
    {
        if(CombatReadabilityPresentation.Adjusted&&
           (contactImpact&&contactStartedFrame==Time.frameCount||readabilityBiteHitFrame==Time.frameCount))return;
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Advance((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }

    internal void Advance(float deltaTime)
    {
        elapsed+=deltaTime;
        int frame=Mathf.FloorToInt(elapsed*framesPerSecond);
        if(fangLockStatuses!=null)
        {
            if(fangLockBloodPulseRemaining>0f)
            {
                fangLockBloodPulseRemaining=Mathf.Max(0f,
                    fangLockBloodPulseRemaining-deltaTime);
                SetColor(Color.Lerp(Color.white,new Color(1f,0.16f,0.24f),
                    fangLockBloodPulseRemaining/0.2f));
            }
            if(anchor==null||!anchor.gameObject.activeInHierarchy||
               !SabertoothCombatRules.HasFangLockDebuff(fangLockStatuses))
            {
                Release();return;
            }
            // Close once, then squeeze until the actual debuff expires or is cleansed.
            if(frame>=frames.Length)frame=2+(frame-2)%(frames.Length-2);
        }
        if(frame>=frames.Length)
        {
            Release();
            return;
        }
        effectRenderer.sprite=frames[frame];
    }

    private void Release()
    {
        if(CombatEffectEndFade.IsFading(this))return;
        if(pooled)return;
        ClearContactImpact();
        GetComponent<PixelBearRoarPressure>()?.ResetForPool();
        pooled=true;
        fangLockStatuses=null;
        fangLockBloodPulseRemaining=0f;
        frames=Array.Empty<Sprite>();
        skillClock=null;
        anchor=null;
        anchorRenderer=null;
        if(effectRenderer!=null)
        {
            effectRenderer.sprite=null;
            effectRenderer.enabled=false;
        }
        gameObject.SetActive(false);
        if(AvailableEffects.Count<MaxPooledEffects)
            AvailableEffects.Push(this);
        else if(Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }

    public void ReleaseAfterBattleEndFade()=>Release();

    private void ClearContactImpact()
    {
        if(!contactImpact)return;
        contactImpact=false;
        ContactImpacts.Remove(this);
    }

    private void OnDisable(){if(!gameObject.activeInHierarchy)ClearContactImpact();}
    private void OnDestroy()=>ClearContactImpact();

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(effectRenderer!=null)effectRenderer.color=contactImpact?
                CombatFlashPresentation.Flash(effectTint):CombatFlashPresentation.Peak(effectTint);
            UpdateTransform();
        }
    }

    private void UpdateTransform()
    {
        if(anchor!=null)
        {
            transform.position=anchorRenderer!=null&&anchorRenderer.sprite!=null
                ?anchor.TransformPoint(anchorRenderer.sprite.bounds.center)
                :anchor.position+Vector3.up*0.35f;
        }
        if(!faceCamera)return;
        if(effectCamera==null)effectCamera=Camera.main;
        if(effectCamera!=null)transform.rotation=effectCamera.transform.rotation*cameraRotationOffset;
    }
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(10030)]
public sealed class PixelFreezeShell:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private SpriteRenderer shell,source,bodyIce;
    private readonly SpriteRenderer[] crystals=new SpriteRenderer[8];
    private static readonly float[] CrystalAngles={-18f,27f,66f,103f,151f,192f,236f,287f};
    private CombatStatusState statuses;
    private Vector2 bodySize;
    private float remaining,age,releaseAge;
    private bool releasing;

    public void Configure(SpriteRenderer output,SpriteRenderer reference,float duration,CombatStatusState state)
    {
        skillClock=CombatSkillClock.Capture();
        shell=output;source=reference;
        bodySize=source!=null&&source.sprite!=null?source.sprite.bounds.size:new Vector2(1f,1.4f);
        bodyIce=new GameObject("Freeze Solid Ice Body").AddComponent<SpriteRenderer>();
        bodyIce.transform.SetParent(transform,false);
        bodyIce.sprite=PixelCombatEffect.FreezeBodySprite;
        for(int i=0;i<crystals.Length;i++)
        {
            var crystal=new GameObject("Freeze Surface Crystal").AddComponent<SpriteRenderer>();
            crystal.transform.SetParent(transform,false);
            crystal.sprite=PixelCombatEffect.FreezeIceSprite;
            crystals[i]=crystal;
        }
        Refresh(duration,state);Draw(0f);
    }

    public void Refresh(float duration,CombatStatusState state)
    {
        remaining=Mathf.Max(remaining,Mathf.Max(0f,duration));statuses=state;
        if(releasing){releasing=false;releaseAge=0f;age=0f;}
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(shell==null){Remove();return;}
            age+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            remaining=statuses==null?remaining-(skillClock==null?Time.deltaTime:skillClock.DeltaTime):
                (statuses.Get(CombatStatusKind.Freeze)?.remaining??0f);
            if(!releasing&&remaining<=0f)
            {
                releasing=true;
                PixelCombatEffect.PlayFreezeDeathShatter(transform.parent,source,false);
            }
            if(releasing)
            {
                releaseAge+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
                if(releaseAge>=0.18f){Remove();return;}
            }
            Draw(age);
        }
    }

    private void Draw(float time)
    {
        float fade=releasing?1f-Mathf.Clamp01(releaseAge/0.18f):1f;
        if(source!=null)
        {
            shell.sortingLayerID=source.sortingLayerID;shell.sortingOrder=source.sortingOrder+2;
            shell.enabled=source.enabled;
        }
        float bodyGrow=Mathf.SmoothStep(0.08f,1f,Mathf.Clamp01(time/0.12f));
        Vector2 bodySpriteSize=bodyIce.sprite.bounds.size;
        bodyIce.transform.localScale=new Vector3(
            bodySize.x*0.70f/bodySpriteSize.x*bodyGrow,
            bodySize.y*0.78f/bodySpriteSize.y*bodyGrow,1f);
        bodyIce.color=new Color(1f,1f,1f,0.82f*fade);
        bodyIce.sortingLayerID=shell.sortingLayerID;
        bodyIce.sortingOrder=shell.sortingOrder;
        bodyIce.enabled=shell.enabled;
        for(int i=0;i<crystals.Length;i++)
        {
            float angle=CrystalAngles[i];
            float grow=Mathf.SmoothStep(0.04f,1f,Mathf.Clamp01((time-(i%3)*0.014f)/0.12f));
            float length=bodySize.y*(0.20f+(i%3)*0.035f);
            float width=bodySize.x*(0.23f+(i%2)*0.065f);
            crystals[i].transform.localPosition=new Vector3(
                Mathf.Cos(angle*Mathf.Deg2Rad)*bodySize.x*0.19f,
                Mathf.Sin(angle*Mathf.Deg2Rad)*bodySize.y*0.21f,0f);
            crystals[i].transform.localRotation=Quaternion.Euler(0f,0f,angle-90f);
            Vector2 spriteSize=crystals[i].sprite.bounds.size;
            crystals[i].transform.localScale=new Vector3(width/spriteSize.x*grow,length/spriteSize.y*grow,1f);
            crystals[i].color=new Color(1f,1f,1f,0.46f*fade);
            crystals[i].sortingLayerID=shell.sortingLayerID;
            crystals[i].sortingOrder=shell.sortingOrder+(i%3==1?-3:1);
            crystals[i].enabled=shell.enabled;
        }
    }

    private void OnDisable(){if(Application.isPlaying&&!CombatEffectEndFade.IsFading(this))Destroy(gameObject);}
    private void Remove()
    {
        if(Application.isPlaying)Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}

public sealed class PixelKingKongLeylineAura:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private Transform anchor;
    private SpriteRenderer source;
    private SpriteMonsterAnimationDriver animationDriver;
    private ParticleSystem stones;
    private ParticleSystem.Particle[] hoveringStones;
    private Vector3[] stoneOrigins;
    private SpriteRenderer[] stoneSprites;
    private float stoneElapsed;
    private SpriteRenderer[] lights;
    private float elapsed;
    private float pulseElapsed;
    private float ending=-1f;
    private bool presentationPaused;
    private float presentationSimulationSpeed;

    public void Configure(Transform owner,SpriteRenderer reference,ParticleSystem fragments,Sprite glow,Sprite stoneSprite)
    {
        skillClock=CombatSkillClock.Capture();
        anchor=owner;source=reference;stones=fragments;
        hoveringStones=new ParticleSystem.Particle[stones.main.maxParticles];
        stoneOrigins=new Vector3[stones.GetParticles(hoveringStones)];
        // A particle renderer sorts the whole cloud as one object. Separate sprites can
        // interleave with every monster using Unity's normal transparent depth sorting.
        stones.GetComponent<ParticleSystemRenderer>().enabled=false;
        stoneSprites=new SpriteRenderer[stoneOrigins.Length];
        for(int i=0;i<stoneOrigins.Length;i++)
        {
            float angle=i*2.399963f;
            float radius=stones.shape.radius*0.96f*Mathf.Sqrt((i+1f)/stoneOrigins.Length);
            stoneOrigins[i]=new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
            var stone=new GameObject("Leyline Floating Stone "+i);
            stone.transform.SetParent(transform,false);
            stoneSprites[i]=stone.AddComponent<SpriteRenderer>();stoneSprites[i].sprite=stoneSprite;
        }
        animationDriver=source==null?null:source.GetComponent<SpriteMonsterAnimationDriver>();
        lights=new SpriteRenderer[3];
        for(int i=0;i<lights.Length;i++)
        {
            var light=new GameObject(i==0?"Leyline Heart Glow":"Leyline Fist Glow "+i);
            light.transform.SetParent(transform,false);
            lights[i]=light.AddComponent<SpriteRenderer>();lights[i].sprite=glow;
        }
        Sync();
    }

    public void Refresh(){elapsed=0f;Pulse();}
    public void Pulse(){pulseElapsed=0f;}

    public void SetPresentationPaused(bool paused)
    {
        if(presentationPaused==paused)return;
        presentationPaused=paused;
        if(stones==null)return;
        var main=stones.main;
        if(paused){presentationSimulationSpeed=main.simulationSpeed;main.simulationSpeed=0f;}
        else main.simulationSpeed=presentationSimulationSpeed;
    }

    public void Stop(bool immediate=false)
    {
        if(CombatEffectEndFade.IsFading(this))return;
        if(immediate)
        {
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
            return;
        }
        if(ending>=0f)return;
        ending=0f;
        if(stones==null)return;
        stones.Stop(true,ParticleSystemStopBehavior.StopEmitting);
        var velocity=stones.velocityOverLifetime;velocity.enabled=false;
        var main=stones.main;main.gravityModifier=1.4f;
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(presentationPaused){Sync();return;}
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(anchor==null||!anchor.gameObject.activeInHierarchy||
               (source!=null&&(!source.gameObject.activeInHierarchy||
                 (animationDriver!=null&&(!animationDriver.enabled||animationDriver.CurrentMotionName=="Death")))))
            {Stop(true);return;}
            elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);pulseElapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);stoneElapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            // The combat timer normally ends it; also bound lifetime if its owner stops ticking.
            if(ending<0f&&elapsed>=FamilyTranscendenceRules.KingKongZoneDuration)Stop();
            if(ending>=0f)
            {
                ending+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
                if(ending>=0.45f){Stop(true);return;}
            }
            Sync();
        }
    }

    private void Sync()
    {
        if(anchor==null||lights==null)return;
        transform.position=new Vector3(anchor.position.x,0f,anchor.position.z);
        if(stones!=null)
        {
            int count=stones.GetParticles(hoveringStones);
            for(int i=0;ending<0f&&i<count;i++)
            {
                float phase=i*2.4f;
                float lift=Mathf.SmoothStep(0f,1f,(stoneElapsed-(i%4)*0.07f)/0.65f);
                hoveringStones[i].position=stoneOrigins[i]+lift*new Vector3(
                    Mathf.Sin(stoneElapsed*1.4f+phase)*0.025f,
                    0.5f+(i%4)*0.2f+Mathf.Sin(stoneElapsed*2f+phase)*0.045f,
                    Mathf.Cos(stoneElapsed*1.4f+phase)*0.025f);
                hoveringStones[i].velocity=Vector3.zero;
                // Keep these same stones through skill refreshes; Stop releases them to gravity.
                hoveringStones[i].remainingLifetime=FamilyTranscendenceRules.KingKongZoneDuration+1f;
            }
            stones.SetParticles(hoveringStones,count);
            Camera camera=Camera.main;
            Quaternion facing=camera!=null?camera.transform.rotation:source!=null?source.transform.rotation:Quaternion.identity;
            for(int i=0;i<stoneSprites.Length;i++)
            {
                var stone=stoneSprites[i];stone.enabled=i<count;
                if(i>=count)continue;
                var particle=hoveringStones[i];
                stone.transform.position=stones.transform.TransformPoint(particle.position);
                stone.transform.rotation=facing*Quaternion.Euler(0f,0f,-particle.rotation);
                stone.transform.localScale=Vector3.one*(particle.startSize/stone.sprite.bounds.size.x);
                stone.color=particle.startColor;
                stone.sortingLayerID=source==null?0:source.sortingLayerID;
                stone.sortingOrder=source==null?10:source.sortingOrder;
            }
        }
        float fade=ending<0f?Mathf.Clamp01(elapsed/0.18f):1f-ending/0.45f;
        float beat=Mathf.Exp(-pulseElapsed*7f);
        float brightness=fade*(0.38f+beat*0.55f);
        for(int i=0;i<lights.Length;i++)
        {
            var light=lights[i];light.enabled=source!=null&&source.enabled;
            if(source==null||source.sprite==null)continue;
            var bounds=source.sprite.bounds;
            bool raised=animationDriver!=null&&animationDriver.CurrentMotionName=="SkillAttack"&&animationDriver.CurrentFrameIndex<=2;
            Vector3 local=new Vector3(bounds.center.x+(i==0?0f:i==1?-0.22f:0.22f)*bounds.size.x,
                bounds.min.y+bounds.size.y*(i==0?0.49f:raised?0.79f:0.3f),0f);
            light.transform.position=source.transform.TransformPoint(local);
            light.transform.rotation=source.transform.rotation;
            float size=bounds.size.y*(i==0?0.52f:0.4f)*(1f+beat*0.12f);
            light.transform.localScale=source.transform.lossyScale*(size/light.sprite.bounds.size.y);
            light.sortingLayerID=source.sortingLayerID;light.sortingOrder=source.sortingOrder+2;
            light.color=new Color(1f,0.8f,0.28f,brightness);
        }
    }
}

[DisallowMultipleComponent]
public sealed class PixelTranscendenceFlameOutline:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static readonly Vector2[] Directions=
    {
        Vector2.left,Vector2.right,Vector2.up,Vector2.down,
        new Vector2(-1f,-1f).normalized,new Vector2(-1f,1f).normalized,
        new Vector2(1f,-1f).normalized,new Vector2(1f,1f).normalized
    };
    private static readonly Vector2[] RisingFlameDirections=
    {
        new Vector2(-1f,-1f),new Vector2(0f,-1f),new Vector2(1f,-1f),
        new Vector2(-1f,0f),new Vector2(1f,0f),
        new Vector2(-1f,1f),new Vector2(0f,1f),new Vector2(1f,1f),
        new Vector2(-2f,2f),new Vector2(-1f,2f),new Vector2(0f,2f),
        new Vector2(1f,2f),new Vector2(2f,2f),
        new Vector2(-2f,3f),new Vector2(-1f,3f),new Vector2(0f,3f),
        new Vector2(1f,3f),new Vector2(2f,3f),
        new Vector2(-1f,4f),new Vector2(0f,4f),new Vector2(1f,4f)
    };

    private SpriteRenderer source;
    private SpriteRenderer[] edges;
    private Vector2[] directions;
    private bool risingFlame;
    private float remaining=-1f;

    public void Configure(SpriteRenderer sourceRenderer)
    {
        skillClock=CombatSkillClock.Capture();
        Configure(sourceRenderer,Directions.Length);
    }

    public void ConfigureClimax(SpriteRenderer sourceRenderer,float duration)
    {
        skillClock=CombatSkillClock.Capture();
        risingFlame=true;
        remaining=Mathf.Max(0.1f,duration);
        Configure(sourceRenderer,RisingFlameDirections.Length);
    }

    private void Configure(SpriteRenderer sourceRenderer,int edgeCount)
    {
        skillClock=CombatSkillClock.Capture();
        source=sourceRenderer;
        directions=risingFlame?RisingFlameDirections:Directions;
        edges=new SpriteRenderer[edgeCount];
        for(int i=0;i<edges.Length;i++)
        {
            GameObject edge=new GameObject("Flame Edge "+i);
            edge.transform.SetParent(transform,false);
            edges[i]=edge.AddComponent<SpriteRenderer>();
        }
        Sync();
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(source==null)
            {
                Destroy(gameObject);
                return;
            }
            if(remaining>0f)
            {
                remaining-=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
                if(remaining<=0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            Sync();
        }
    }

    private void Sync()
    {
        if(source==null||edges==null)return;
        float pulse=0.5f+0.5f*Mathf.Sin((skillClock==null?Time.time:skillClock.Time)*7f);
        float distance=Mathf.Lerp(0.018f,0.027f,pulse);
        float pixel=source.sprite==null?0.02f:
            1f/Mathf.Max(1f,source.sprite.pixelsPerUnit);
        int flameFrame=Mathf.FloorToInt((skillClock==null?Time.time:skillClock.Time)*10f);
        for(int i=0;i<edges.Length;i++)
        {
            SpriteRenderer edge=edges[i];
            if(edge==null)continue;
            edge.sprite=source.sprite;
            edge.flipX=source.flipX;
            edge.flipY=source.flipY;
            edge.enabled=source.enabled&&source.gameObject.activeInHierarchy;
            edge.sortingLayerID=source.sortingLayerID;
            edge.sortingOrder=source.sortingOrder-1;
            if(risingFlame)
            {
                float height=Mathf.InverseLerp(-1f,4f,directions[i].y);
                edge.color=Color.Lerp(
                    new Color(0.72f,0.025f,0.005f,0.9f),
                    new Color(1f,0.82f,0.08f,0.98f),
                    Mathf.Clamp01(height*0.78f+pulse*0.18f));
                Vector2 offset=directions[i]*pixel;
                if(directions[i].y>=2f)
                {
                    offset.x+=((flameFrame+i)%3-1)*pixel;
                    if((flameFrame+i)%4==0)offset.y+=pixel;
                }
                edge.transform.localPosition=(Vector3)offset;
            }
            else
            {
                edge.color=Color.Lerp(
                    new Color(0.68f,0.005f,0.002f,0.78f),
                    new Color(1f,0.22f,0.015f,0.94f),
                    (pulse+i%2)*0.5f);
                edge.transform.localPosition=(Vector3)(directions[i]*distance);
            }
        }
    }
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(10020)]
public sealed class PixelBearRoarPressure:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private readonly SpriteRenderer[] clouds=new SpriteRenderer[32];
    private SpriteRenderer frameSource;
    private Vector3 origin;
    private float radius,height,elapsed;
    private bool ending,avatar;

    public void Configure(SpriteRenderer sprite,Vector3 center,float range,SpriteRenderer reference,bool final,bool avatarRoar=false)
    {
        skillClock=CombatSkillClock.Capture();
        frameSource=sprite;origin=center;radius=range;ending=final;avatar=avatarRoar;elapsed=0f;
        height=reference==null?.6f:Mathf.Max(.4f,reference.bounds.size.y*.48f);
        if(avatar&&reference!=null)
        {
            // The full bear is foot-pivoted: pressure starts at its chest/mouth, not the canvas centre.
            origin=reference.transform.TransformPoint(new Vector3(0f,.32f,0f));
            height=0f;
        }
        for(int i=0;i<(avatar?32:16);i++)
        {
            if(clouds[i]==null)
            {
                var cloud=new GameObject("Roar Air Layer "+i).AddComponent<SpriteRenderer>();
                cloud.transform.SetParent(transform,false);clouds[i]=cloud;
            }
            clouds[i].sortingLayerID=sprite.sortingLayerID;
            clouds[i].sortingOrder=sprite.sortingOrder;
            clouds[i].enabled=true;
        }
        enabled=true;Sync();
    }

    public void ResetForPool()
    {
        enabled=false;
        for(int i=0;i<clouds.Length;i++)if(clouds[i]!=null)clouds[i].enabled=false;
    }

    private void LateUpdate(){
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);Sync();
        }
    }

    private void Sync()
    {
        if(frameSource==null)return;
        frameSource.enabled=false;
        var camera=Camera.main;
        for(int i=0;i<(avatar?32:16);i++)
        {
            var cloud=clouds[i];cloud.sprite=frameSource.sprite;
            float age=elapsed-(avatar&&i>=16?.14f:0f);
            cloud.enabled=age>=0f;
            if(age<0f)continue;
            float progress=Mathf.Clamp01(age/(avatar?BearCombatRules.AvatarRoarTravelDuration:
                BearCombatRules.KingRoarTravelDuration));
            float reach=radius*(avatar?Mathf.Pow(progress,.7f):progress)*(i>=16?.86f:1f);
            float angle=(i%16+(i>=16?.5f:0f))*Mathf.PI*2f/16f;
            var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            cloud.transform.position=origin+direction*reach*(i%2==0?1f:.88f)+
                Vector3.up*(height+(i%2==0?.08f:-.08f)*progress);
            cloud.transform.rotation=camera==null?Quaternion.identity:camera.transform.rotation;
            if(camera!=null)
            {
                Vector3 screenDirection=camera.WorldToScreenPoint(origin+direction)-camera.WorldToScreenPoint(origin);
                cloud.transform.rotation*=Quaternion.Euler(0,0,Mathf.Atan2(screenDirection.y,screenDirection.x)*Mathf.Rad2Deg-90f);
            }
            float scale=(.22f+progress*1.35f)*(ending?1.2f:1f);
            if(avatar)scale*=i>=16?1.15f:1.5f;
            cloud.transform.localScale=new Vector3(scale*1.45f,
                scale*(avatar?1.25f:i%2==0?.9f:.65f),1f);
            cloud.color=CombatFlashPresentation.Peak(new Color(1f,1f,1f,Mathf.Lerp(.15f,1f,Mathf.Clamp01(progress*4f))));
            cloud.flipX=i%2==0;
        }
    }
}

[DisallowMultipleComponent]
public sealed class PixelGroundCastTelegraph:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private const int Segments=32;
    private Transform fill;
    private Mesh fillMesh;
    private Material fillMaterial;
    private LineRenderer outline;
    private float duration;
    private float elapsed;
    private float impactElapsed;
    private bool impacted;
    private bool holding;
    private float holdDuration;
    private Color warningColor;
    private Transform centerSprite;
    private SpriteRenderer centerRenderer;

    public bool IsHolding => holding;

    public void Configure(
        float radius,float castDuration,Material outlineMaterial,
        SpriteRenderer sortingReference,Color color)
    {
        skillClock=CombatSkillClock.Capture();
        duration=castDuration;
        warningColor=color;

        GameObject fillObject=new GameObject("Charging Fill");
        fillObject.transform.SetParent(transform,false);
        fill=fillObject.transform;
        MeshFilter filter=fillObject.AddComponent<MeshFilter>();
        MeshRenderer renderer=fillObject.AddComponent<MeshRenderer>();
        fillMesh=CreateDisc(radius);
        filter.sharedMesh=fillMesh;
        Shader shader=Shader.Find("Sprites/Default");
        fillMaterial=new Material(shader)
        {
            name="Tyranno Tremor Fill",
            hideFlags=HideFlags.HideAndDontSave,
            color=new Color(color.r,color.g,color.b,color.a*0.3f)
        };
        renderer.sharedMaterial=fillMaterial;
        renderer.sortingLayerID=sortingReference==null
            ?0:sortingReference.sortingLayerID;
        renderer.sortingOrder=(sortingReference==null
            ?0:sortingReference.sortingOrder)-100;
        fill.localScale=new Vector3(0.01f,1f,0.01f);

        outline=gameObject.AddComponent<LineRenderer>();
        outline.sharedMaterial=outlineMaterial;
        outline.useWorldSpace=false;
        outline.loop=true;
        outline.positionCount=Segments;
        outline.startWidth=0.065f;
        outline.endWidth=0.065f;
        outline.numCornerVertices=0;
        outline.shadowCastingMode=
            UnityEngine.Rendering.ShadowCastingMode.Off;
        outline.receiveShadows=false;
        outline.sortingLayerID=sortingReference==null
            ?0:sortingReference.sortingLayerID;
        outline.sortingOrder=(sortingReference==null
            ?0:sortingReference.sortingOrder)-90;
        SetOutlineColor(new Color(color.r,color.g,color.b,0.96f));
        for(int i=0;i<Segments;i++)
        {
            float angle=i*Mathf.PI*2f/Segments;
            outline.SetPosition(i,new Vector3(
                Mathf.Cos(angle)*radius,0.01f,Mathf.Sin(angle)*radius));
        }
    }

    public void SetCenterSprite(
        Sprite sprite,Color color,SpriteRenderer sortingReference)
    {
        if(sprite==null)return;
        GameObject center=new GameObject("Charging Spore");
        center.transform.SetParent(transform,false);
        center.transform.localPosition=Vector3.up*0.18f;
        if(Camera.main!=null)center.transform.rotation=Camera.main.transform.rotation;
        centerSprite=center.transform;
        centerSprite.localScale=Vector3.one*0.45f;
        centerRenderer=center.AddComponent<SpriteRenderer>();
        centerRenderer.sprite=sprite;
        centerRenderer.color=color;
        centerRenderer.sortingLayerID=sortingReference==null
            ?0:sortingReference.sortingLayerID;
        centerRenderer.sortingOrder=(sortingReference==null
            ?0:sortingReference.sortingOrder)+35;
    }

    public void Impact()
    {
        if(impacted)return;
        impacted=true;
        impactElapsed=0f;
        fill.localScale=Vector3.one;
        fillMaterial.color=new Color(
            warningColor.r,warningColor.g,warningColor.b,0.48f);
        outline.startWidth=0.13f;
        outline.endWidth=0.13f;
        SetOutlineColor(new Color(
            warningColor.r,warningColor.g,warningColor.b,1f));
        if(centerSprite!=null)centerSprite.localScale=Vector3.one*1.35f;
    }

    public void HoldFilled(float seconds)
    {
        holding=true;
        holdDuration=Mathf.Max(0.1f,seconds);
        elapsed=0f;
        fill.localScale=Vector3.one;
        fillMaterial.color=new Color(
            warningColor.r,warningColor.g,warningColor.b,warningColor.a);
        outline.startWidth=outline.endWidth=0.08f;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(holding)
            {
                elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
                float pulse=0.82f+Mathf.Sin(elapsed*5f)*0.12f;
                fillMaterial.color=new Color(
                    warningColor.r,warningColor.g,warningColor.b,
                    warningColor.a*pulse);
                if(elapsed>=holdDuration)Destroy(gameObject);
                return;
            }
            if(!impacted)
            {
                elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
                float progress=Mathf.Clamp01(elapsed/duration);
                float eased=progress*progress*(3f-2f*progress);
                fill.localScale=new Vector3(
                    Mathf.Max(0.01f,eased),1f,Mathf.Max(0.01f,eased));
                fillMaterial.color=new Color(
                    warningColor.r,warningColor.g,warningColor.b,
                    Mathf.Lerp(warningColor.a*0.3f,warningColor.a,progress));
                if(centerSprite!=null)
                    centerSprite.localScale=Vector3.one*
                        (Mathf.Lerp(0.45f,1.15f,eased)*(1f+Mathf.Sin(progress*Mathf.PI*6f)*0.05f));
                float width=0.065f+Mathf.Sin(progress*Mathf.PI*8f)*0.012f;
                outline.startWidth=width;
                outline.endWidth=width;
                if(elapsed>duration+0.5f)Destroy(gameObject);
                return;
            }

            impactElapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            float impactProgress=Mathf.Clamp01(impactElapsed/0.24f);
            float scale=1f+impactProgress*0.18f;
            transform.localScale=new Vector3(scale,1f,scale);
            fillMaterial.color=new Color(
                warningColor.r,warningColor.g,warningColor.b,
                0.48f*(1f-impactProgress));
            SetOutlineColor(new Color(
                warningColor.r,warningColor.g,warningColor.b,1f-impactProgress));
            if(centerRenderer!=null)
                centerRenderer.color=new Color(
                    warningColor.r,warningColor.g,warningColor.b,1f-impactProgress);
            outline.startWidth=outline.endWidth=
                Mathf.Lerp(0.13f,0.02f,impactProgress);
            if(impactProgress>=1f)Destroy(gameObject);
        }
    }

    private void SetOutlineColor(Color color)
    {
        outline.startColor=color;
        outline.endColor=color;
    }

    private static Mesh CreateDisc(float radius)
    {
        Mesh mesh=new Mesh
        {
            name="Tyranno Tremor Telegraph Disc",
            hideFlags=HideFlags.HideAndDontSave
        };
        Vector3[] vertices=new Vector3[Segments+2];
        int[] triangles=new int[Segments*3];
        vertices[0]=Vector3.zero;
        for(int i=0;i<=Segments;i++)
        {
            float angle=i*Mathf.PI*2f/Segments;
            vertices[i+1]=new Vector3(
                Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
        }
        for(int i=0;i<Segments;i++)
        {
            int triangle=i*3;
            triangles[triangle]=0;
            triangles[triangle+1]=i+1;
            triangles[triangle+2]=i+2;
        }
        mesh.vertices=vertices;
        mesh.triangles=triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if(fillMesh!=null)
        {
            if(Application.isPlaying)Destroy(fillMesh);
            else DestroyImmediate(fillMesh);
        }
        if(fillMaterial!=null)
        {
            if(Application.isPlaying)Destroy(fillMaterial);
            else DestroyImmediate(fillMaterial);
        }
    }
}

[DisallowMultipleComponent]
public sealed class PixelHaetaeRoarWave:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private SpriteRenderer wave;
    private float targetScale;
    private float duration;
    private float elapsed;
    private Color color;
    private float expansionEnd;

    public void Configure(
        SpriteRenderer renderer,float scale,float lifetime,float expandUntil=1f)
    {
        skillClock=CombatSkillClock.Capture();
        wave=renderer;
        targetScale=Mathf.Max(0.01f,scale);
        duration=Mathf.Max(0.01f,lifetime);
        color=renderer.color;
        expansionEnd=Mathf.Clamp(expandUntil,0.01f,1f);
        transform.localScale=Vector3.one*(targetScale*0.04f);
        wave.color=new Color(color.r,color.g,color.b,0f);
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            float progress=Mathf.Clamp01(elapsed/duration);
            float eased=1f-Mathf.Pow(1f-Mathf.Clamp01(progress/expansionEnd),2.4f);
            transform.localScale=Vector3.one*Mathf.Lerp(
                targetScale*0.04f,targetScale,eased);
            wave.color=new Color(
                color.r,color.g,color.b,color.a*Mathf.Sin(progress*Mathf.PI));
            if(progress>=1f)Destroy(gameObject);
        }
    }
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(10001)]
public sealed class PixelWolfHuntAfterimage:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Material huntMaterial;
    private MaterialPropertyBlock huntProperties;
    private Func<bool> huntValid;
    private SpriteRenderer huntTarget;
    private Sprite huntLeapPose;
    private SpriteMonsterAnimationDriver huntAnimation;
    private float stopDistance;
    private bool ambush,launched,contact;
    private bool rushing;
    private bool scattered;
    private float contactHold;
    private Vector3 contactDirection;
    private SpriteRenderer ghost;
    private SpriteRenderer glow;
    private Vector3 start;
    private Vector3 end;
    private Color color;
    private float duration;
    private float movementDelay;
    private float elapsed;
    private bool companion;
    private Func<Vector3> companionHome;
    private float companionStrikeAge=-1f,companionStrikeDelay;
    private float companionAttackPlaybackSpeed=1f;
    private float companionEndAge=-1f;
    private Vector3 companionLastHome;
    private bool alphaStrike,alphaHitDone;
    private Action alphaHit;
    private SpriteRenderer alphaVictim;
    private Vector3 alphaOffset;

    public void PrepareAlphaStrike(SpriteRenderer owner,SpriteMonsterAnimationDriver animation,
        SpriteRenderer victim,Func<bool> valid,Action hit)
    {
        PrepareAmbush(owner,valid,null,animation);
        alphaStrike=true;alphaHit=hit;alphaVictim=victim;
        alphaOffset=transform.position-victim.transform.position;
        ghost.flipX=Vector3.Dot(victim.transform.position-transform.position,transform.right)<0f;
        ghost.color=color;
        SetCompanionPose(false,0f,false);
    }



    private void AnimateAlphaStrike(float dt)
    {
        if(huntTarget==null||huntAnimation==null||(!alphaHitDone&&huntValid!=null&&!huntValid()))
        {Cancel();return;}
        elapsed+=dt;
        if(!alphaHitDone&&alphaVictim!=null)transform.position=alphaVictim.transform.position+alphaOffset;
        float age=Mathf.Max(0f,elapsed-.06f);
        SetCompanionPose(elapsed>=.06f&&age<huntAnimation.BasicAttackDuration,age,false);
        if(alphaVictim!=null)
            ghost.sortingOrder=alphaVictim.sortingOrder+(transform.position.z<=alphaVictim.transform.position.z?2:-2);
        if(!alphaHitDone&&elapsed>=.06f+huntAnimation.BasicAttackHitTime)
        {
            alphaHitDone=true;alphaHit?.Invoke();alphaHit=null;
        }
        float scatterAge=age-huntAnimation.BasicAttackDuration;
        if(scatterAge>=0f&&!scattered)
        {
            scattered=true;
            PixelCombatEffect.PlayWolfAmbushScatter(ghost,transform.right*(ghost.flipX?-1f:1f),color);
        }
        float fade=Mathf.Clamp01(scatterAge/.12f);
        huntProperties.SetFloat("_TailDissolve",fade);ghost.SetPropertyBlock(huntProperties);
        ghost.color=new Color(color.r,color.g,color.b,color.a*(1f-fade));
        if(scatterAge>=WolfCombatRules.HuntScatterDuration)Cancel();
    }

    public void PrepareCompanion(SpriteRenderer owner,SpriteMonsterAnimationDriver animation,
        Func<Vector3> home,Func<bool> valid,Color tint)
    {
        PrepareAmbush(owner,valid,null,animation);
        color=tint;start=transform.position;
        companion=true;companionHome=home;companionLastHome=home();
        ghost.flipX=owner.flipX;
        SetCompanionPose(false,0f,false);
    }

    public void StrikeCompanion(SpriteRenderer victim,float delay,float playbackSpeed)
    {
        if(this==null||!companion||companionEndAge>=0f||victim==null)return;
        companionStrikeAge=0f;companionStrikeDelay=delay;
        companionAttackPlaybackSpeed=Mathf.Max(.01f,playbackSpeed)*huntAnimation.GlobalSpeedMultiplier;
        ghost.flipX=huntTarget.flipX;
        SetCompanionPose(false,elapsed,false);
    }

    public void FinishCompanion()
    {
        if(this==null||!companion||companionEndAge>=0f)return;
        start=transform.position;
        companionEndAge=0f;companionStrikeAge=-1f;
        SetCompanionPose(false,0f,false);
        ghost.color=color;
    }

    private void SetCompanionPose(bool striking,float age,bool moving)
    {
        Sprite pose=striking?huntAnimation.GetBasicAttackFrame(age):moving?huntAnimation.GetRunFrame(age):huntAnimation.GetIdleFrame(age);
        SetAmbushSprite(pose);
        float scale=huntAnimation.WolfCompanionScale(striking,moving)/
            Mathf.Max(.001f,Mathf.Abs(huntTarget.transform.localScale.x));
        transform.localScale=huntTarget.transform.lossyScale*scale;
    }

    private void AnimateCompanion(float dt)
    {
        if(huntTarget==null||huntAnimation==null||(huntValid!=null&&!huntValid())){Cancel();return;}
        elapsed+=dt;
        if(companionEndAge>=0f)
        {
            companionEndAge+=dt;
            float blinkAge=companionEndAge-WolfCombatRules.EclipseIdleDuration;
            float progress=Mathf.Clamp01(blinkAge/WolfCombatRules.EclipseBlinkDuration);
            transform.position=start;
            SetCompanionPose(false,companionEndAge,false);
            // Integrated frequency: each white/transparent blink gets shorter.
            float phase=Mathf.Max(0f,blinkAge)*3f+9f*Mathf.Max(0f,blinkAge)*Mathf.Max(0f,blinkAge);
            bool bright=blinkAge>=0f&&Mathf.Repeat(phase,1f)<.5f;
            Color tint=bright?Color.Lerp(color,Color.white,.65f+progress*.35f):color;
            tint.a=blinkAge<0f?color.a:bright?1f:.16f;
            if(CombatFlashPresentation.Reduced && blinkAge>=0f)
            {
                // A continuous build-up still warns of detonation without the accelerating strobe.
                tint=CombatFlashPresentation.Peak(Color.Lerp(color,Color.white,progress*.2f));
                tint.a=Mathf.Lerp(color.a,.85f,progress);
            }
            ghost.color=tint;
            if(blinkAge>=WolfCombatRules.EclipseBlinkDuration)
            {
                var burst=PixelCombatEffect.PlayWolfSpectralImpact(transform,ghost,false,1f,
                    MonsterAutoBattlePrototype.TileSize*WolfCombatRules.EclipseBurstRadiusTiles);
                burst.name="Wolf Eclipse Dissolve Burst";
                Cancel();return;
            }
        }
        else
        {
            Vector3 home=companionHome();
            bool moving=(home-companionLastHome).sqrMagnitude>.00001f;
            companionLastHome=home;
            if(companionStrikeAge>=0f)
            {
                companionStrikeAge+=dt;
                float attackAge=(companionStrikeAge-companionStrikeDelay)*companionAttackPlaybackSpeed;
                bool attacking=attackAge>=0f&&attackAge<huntAnimation.BasicAttackDuration;
                transform.position=home;
                SetCompanionPose(attacking,attacking?attackAge:elapsed,false);
                ghost.color=color;
                if(attackAge>=huntAnimation.BasicAttackDuration)companionStrikeAge=-1f;
            }
            else
            {
                transform.position=Vector3.Lerp(start,home,Mathf.Clamp01(elapsed/WolfCombatRules.EclipseReturnDuration));
                ghost.flipX=huntTarget.flipX;
                SetCompanionPose(false,elapsed,moving&&huntAnimation.CurrentMotionName=="Run");
                ghost.color=color;
            }
        }
        if(companionEndAge<0f)
            ghost.sortingOrder=huntTarget.sortingOrder+(transform.position.z<=huntTarget.transform.position.z?2:-2);
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(alphaStrike)AnimateAlphaStrike((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
            else if(companion)AnimateCompanion((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }

    public void Configure(
        SpriteRenderer renderer,SpriteRenderer glowRenderer,
        Vector3 from,Vector3 to,Color tint,float lifetime,float delay=0f)
    {
        skillClock=CombatSkillClock.Capture();
        ghost=renderer;
        glow=glowRenderer;
        start=from;
        end=to;
        color=tint;
        duration=Mathf.Max(0.01f,lifetime);
        movementDelay=Mathf.Clamp(delay,0f,duration-0.01f);
        transform.position=start;
        ghost.color=color;
        glow.color=new Color(0.08f,0.9f,1f,color.a*0.58f);
    }

    public void PrepareAmbush(SpriteRenderer target,Func<bool> valid,Sprite leapPose=null,
        SpriteMonsterAnimationDriver animation=null)
    {
        ambush=true;huntTarget=target;huntValid=valid;elapsed=0f;
        huntLeapPose=leapPose;huntAnimation=animation;
        glow.enabled=false;
        if(huntMaterial==null)
        {
            huntMaterial=new Material(Resources.Load<Shader>("CombatEffects/Bear/FightingSpiritSilhouette"))
            {name="Wolf Pixel Spirit",hideFlags=HideFlags.HideAndDontSave};
            huntMaterial.SetFloat("_KeepSpriteDetail",1f);
        }
        ghost.sharedMaterial=huntMaterial;
        huntProperties=new MaterialPropertyBlock();
        SetAmbushSprite(ghost.sprite);
        ghost.color=new Color(color.r,color.g,color.b,.42f);
        ghost.flipX=Vector3.Dot(end-start,transform.right)<0f;
    }

    private void SetAmbushSprite(Sprite sprite)
    {
        if(sprite==null)return;
        ghost.sprite=sprite;
        Vector2 minimum=(Vector2)sprite.bounds.min*sprite.pixelsPerUnit+sprite.pivot+sprite.rect.position;
        Vector2 size=(Vector2)sprite.bounds.size*sprite.pixelsPerUnit;
        huntProperties.SetVector("_SpriteUV",new Vector4(minimum.x/sprite.texture.width,
            minimum.y/sprite.texture.height,size.x/sprite.texture.width,size.y/sprite.texture.height));
        huntProperties.SetTexture("_MainTex",sprite.texture);
        ghost.SetPropertyBlock(huntProperties);
    }

    public void LaunchAmbush(Vector3 destination,float landingDistance)
    {
        if(this==null||!ambush)return;
        start=transform.position;end=destination;elapsed=0f;launched=true;
        stopDistance=Mathf.Max(0f,landingDistance);
        SetAmbushSprite(huntLeapPose);
        ghost.flipX=Vector3.Dot(end-start,transform.right)<0f;
    }

    public Vector3 ContactAmbush(bool finishing)
    {
        if(this==null)return Vector3.zero;
        contactDirection=(end-start).normalized;
        transform.position=end;elapsed=0f;contact=true;
        contactHold=finishing?WolfCombatRules.HuntFinalHoldDuration:0.015f;
        ghost.color=new Color(color.r,color.g,color.b,1f);
        return contactDirection;
    }

    public void LaunchRush(Vector3 direction)
    {
        LaunchAmbush(transform.position+direction,0f);
        rushing=true;contactDirection=direction.normalized;
        ghost.color=new Color(color.r,color.g,color.b,1f);
    }

    public void SetPreparationGlow(float reveal,float ready)
    {
        Color tint=Color.Lerp(color,Color.white,ready*.65f);
        tint.a=Mathf.Lerp(.15f,.7f,reveal)+ready*.3f;
        ghost.color=tint;
    }

    public void FinishRush()
    {
        if(this==null)return;
        end=transform.position;contact=true;elapsed=0f;contactHold=0f;
    }

    public void EmitRushTrail()
    {
        var trail=PixelCombatEffect.PlayWolfHuntAfterimage(ghost,transform.position,transform.position,
            new Color(color.r,color.g,color.b,.22f),.13f,"Wolf Rush Wake");
        trail.transform.GetChild(0).gameObject.SetActive(false);
        trail.sortingOrder=ghost.sortingOrder-1;
    }

    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    private void AnimateAmbush(float dt)
    {
        if(huntValid!=null&&!huntValid()){Cancel();return;}
        elapsed+=dt;
        if(!launched)
        {
            if(elapsed>2f){Cancel();return;}
            if(huntTarget!=null)
                ghost.sortingOrder=huntTarget.sortingOrder+(transform.position.z<=huntTarget.transform.position.z?2:-2);
            return;
        }
        if(contact)
        {
            float travel=rushing?1f:Mathf.Clamp01((elapsed-contactHold)/WolfCombatRules.HitInterval);
            transform.position=end+contactDirection*(stopDistance*travel);
            float idleAge=elapsed-contactHold-(rushing?0f:WolfCombatRules.HitInterval);
            if(idleAge>=0f&&huntAnimation!=null)
            {
                Sprite idle=huntAnimation.GetIdleFrame(Mathf.Min(idleAge,WolfCombatRules.HuntIdleDuration));
                if(ghost.sprite!=idle)SetAmbushSprite(idle);
            }
            float scatterAge=idleAge-WolfCombatRules.HuntIdleDuration;
            if(scatterAge>=0f&&!scattered)
            {
                scattered=true;
                PixelCombatEffect.PlayWolfAmbushScatter(ghost,contactDirection,color);
            }
            float fade=Mathf.Clamp01(scatterAge/.12f);
            huntProperties.SetFloat("_TailDissolve",fade);
            ghost.SetPropertyBlock(huntProperties);
            ghost.color=new Color(color.r,color.g,color.b,1f-fade);
            if(scatterAge>=WolfCombatRules.HuntScatterDuration){Cancel();return;}
        }
        else if(!rushing)
        {
            float t=Mathf.Clamp01(elapsed/WolfCombatRules.HitInterval);
            transform.position=Vector3.Lerp(start,end,t);
            ghost.color=new Color(color.r,color.g,color.b,Mathf.Lerp(.42f,1f,t));
            if(elapsed>WolfCombatRules.HitInterval+.25f){Cancel();return;}
        }
        if(huntTarget!=null)
            ghost.sortingOrder=huntTarget.sortingOrder+(transform.position.z<=huntTarget.transform.position.z?2:-2);
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(companion||alphaStrike)return;
            if(ambush){AnimateAmbush((skillClock==null?Time.deltaTime:skillClock.DeltaTime));return;}
            elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            float progress=Mathf.Clamp01(elapsed/duration);
            float movementProgress=Mathf.Clamp01(
                (elapsed-movementDelay)/Mathf.Max(0.01f,duration-movementDelay));
            transform.position=Vector3.Lerp(
                start,end,movementDelay>0f
                    ?movementProgress*movementProgress
                    :1f-Mathf.Pow(1f-progress,3f));
            Color faded=color;
            faded.a*=movementDelay>0f?1f-movementProgress*0.65f:1f-progress;
            ghost.color=faded;
            Color glowColor=glow.color;
            glowColor.a=color.a*0.58f*(1f-progress);
            glow.color=glowColor;
            if(progress>=1f)Destroy(gameObject);
        }
    }
}

[DisallowMultipleComponent]
public sealed class PixelGroundWebReveal:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private SpriteRenderer web;
    private float targetScale;
    private float duration;
    private float elapsed;
    private Color color;

    public void Configure(SpriteRenderer renderer,float scale,float revealDuration)
    {
        skillClock=CombatSkillClock.Capture();
        web=renderer;
        targetScale=scale;
        duration=Mathf.Max(0.01f,revealDuration);
        color=renderer.color;
        transform.localScale=Vector3.one*(targetScale*0.12f);
        Color hidden=color;
        hidden.a=0f;
        web.color=hidden;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(elapsed>=duration)return;
            elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            float progress=Mathf.Clamp01(elapsed/duration);
            float eased=1f-Mathf.Pow(1f-progress,3f);
            transform.localScale=Vector3.one*Mathf.Lerp(targetScale*0.12f,targetScale,eased);
            Color revealed=color;
            revealed.a*=progress;
            web.color=revealed;
        }
    }
}

[DisallowMultipleComponent]
public sealed class PixelTyrannoLavaFieldEffect:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private const int SpreadFrameCount=6;
    private SpriteRenderer ground;
    private ParticleSystem eruption;
    private Sprite[] frames;
    private float fullScale;
    private float duration;
    private float elapsed;
    private float pulse;
    private bool erupted;

    public float Duration=>duration;
    public ParticleSystem EruptionParticles=>eruption;

    public void Configure(
        SpriteRenderer groundRenderer,ParticleSystem eruptionParticles,
        Sprite[] animationFrames,float scale,float lifetime)
    {
        skillClock=CombatSkillClock.Capture();
        ground=groundRenderer;
        eruption=eruptionParticles;
        frames=animationFrames;
        fullScale=Mathf.Max(0.01f,scale)*PixelCombatEffect.PreviewVisualScale;
        duration=Mathf.Max(0.6f,lifetime);
        ground.transform.localScale=Vector3.one*fullScale;
        ground.sprite=frames[0];
    }

    public void Pulse()
    {
        pulse=0.22f;
        if(eruption!=null)
        {
            eruption.Emit(9);
            if(!eruption.isPlaying)eruption.Play();
        }
    }

    public void Stop()
    {
        if(CombatEffectEndPlayback.IsPreserving(this))return;
        elapsed=Mathf.Max(elapsed,duration);
        if(eruption!=null)
            eruption.Stop(false,ParticleSystemStopBehavior.StopEmitting);
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Advance((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }
    internal void Advance(float deltaTime)
    {
        elapsed+=deltaTime;
        pulse=Mathf.Max(0f,pulse-deltaTime);
        const float spreadDuration=0.56f;
        const float eruptionDuration=0.32f;
        if(elapsed<spreadDuration)
        {
            int frame=Mathf.Min(SpreadFrameCount-1,
                Mathf.FloorToInt(elapsed/spreadDuration*SpreadFrameCount));
            ground.sprite=frames[frame];
        }
        else
        {
            if(!erupted)
            {
                erupted=true;
                eruption.Emit(46);
                eruption.Play();
            }
            int eruptionFrames=frames.Length-SpreadFrameCount;
            int frame=SpreadFrameCount+Mathf.Min(eruptionFrames-1,
                Mathf.FloorToInt((elapsed-spreadDuration)/eruptionDuration*
                    eruptionFrames));
            ground.sprite=frames[frame];
        }
        float fade=elapsed<=duration
            ?1f:1f-Mathf.Clamp01((elapsed-duration)/0.42f);
        float beat=1f+Mathf.Sin((skillClock==null?Time.time:skillClock.Time)*4.6f)*0.018f+
                   Mathf.Clamp01(pulse/0.22f)*0.055f;
        ground.transform.localScale=Vector3.one*(fullScale*beat);
        ground.color=new Color(1f,1f,1f,fade);
        if(fade<=0f){if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
    }
}

[DisallowMultipleComponent]
public sealed class PixelSpiritSanctuaryEffect:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private SpriteRenderer ground;
    private ParticleSystem particles;
    private float fullScale;
    private float duration;
    private float elapsed;
    private float pulse;
    private Color color;
    private bool stableGround;

    public float Duration => duration;
    public bool StableGround => stableGround;

    public void Configure(
        SpriteRenderer groundRenderer,ParticleSystem particleSystem,
        float scale,float lifetime,bool keepScaleStable=false)
    {
        skillClock=CombatSkillClock.Capture();
        ground=groundRenderer;
        particles=particleSystem;
        fullScale=Mathf.Max(0.01f,scale)*PixelCombatEffect.PreviewVisualScale;
        duration=Mathf.Max(0.1f,lifetime);
        stableGround=keepScaleStable;
        color=ground.color;
        ground.transform.localScale=Vector3.one*(fullScale*0.08f);
    }

    public void Pulse()
    {
        if(stableGround)return;
        pulse=0.3f;
        if(particles!=null)particles.Emit(12);
    }

    public void Stop()
    {
        if(CombatEffectEndPlayback.IsPreserving(this))return;
        elapsed=Mathf.Max(elapsed,duration);
        if(particles!=null)
            particles.Stop(false,ParticleSystemStopBehavior.StopEmitting);
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Advance((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }
    internal void Advance(float deltaTime)
    {
        elapsed+=deltaTime;
        pulse=Mathf.Max(0f,pulse-deltaTime);
        float reveal=1f-Mathf.Pow(1f-Mathf.Clamp01(elapsed/0.22f),3f);
        float fade=elapsed<=duration
            ?1f:1f-Mathf.Clamp01((elapsed-duration)/0.45f);
        float beat=stableGround?1f:
            1f+Mathf.Sin((skillClock==null?Time.time:skillClock.Time)*2.8f)*0.012f+
            Mathf.Clamp01(pulse/0.3f)*0.07f;
        ground.transform.localScale=Vector3.one*(fullScale*reveal*beat);
        ground.color=new Color(color.r,color.g,color.b,color.a*fade);
        if(elapsed>=duration&&particles!=null&&particles.isEmitting)
            particles.Stop(false,ParticleSystemStopBehavior.StopEmitting);
        if(fade<=0f){if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
    }
}

[DisallowMultipleComponent]
public sealed class PixelNinjaLineSlash:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private SpriteRenderer cut;
    private Vector3 fullScale;
    private float duration;
    private float delay;
    private float elapsed;

    public void Configure(
        Sprite sprite,Vector3 start,Vector3 end,SpriteRenderer sortingReference,
        float lifetime,Color color,float thickness=0.58f,float startDelay=0f)
    {
        skillClock=CombatSkillClock.Capture();
        duration=Mathf.Max(0.01f,lifetime);
        delay=Mathf.Max(0f,startDelay);
        Camera camera=Camera.main;
        Vector3 direction=end-start;
        Vector3 right=camera==null?Vector3.right:camera.transform.right;
        Vector3 up=camera==null?Vector3.up:camera.transform.up;
        float angle=Mathf.Atan2(Vector3.Dot(direction,up),
            Vector3.Dot(direction,right))*Mathf.Rad2Deg;

        GameObject line=new GameObject("Slash Cut");
        line.transform.SetParent(transform,false);
        line.transform.position=(start+end)*0.5f;
        line.transform.rotation=(camera==null?Quaternion.identity:camera.transform.rotation)*
                                Quaternion.Euler(0f,0f,angle);
        fullScale=new Vector3(
            Mathf.Max(0.01f,direction.magnitude/sprite.bounds.size.x),
            Mathf.Max(0.1f,thickness),1f);
        line.transform.localScale=new Vector3(fullScale.x*0.55f,fullScale.y,1f);
        cut=line.AddComponent<SpriteRenderer>();
        cut.sprite=sprite;
        cut.color=new Color(color.r,color.g,color.b,0f);
        cut.sortingLayerID=sortingReference==null?0:sortingReference.sortingLayerID;
        cut.sortingOrder=(sortingReference==null?0:sortingReference.sortingOrder)+26;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(elapsed<delay)return;
            float progress=Mathf.Clamp01((elapsed-delay)/duration);
            float reveal=Mathf.Clamp01(progress/0.18f);
            Color color=cut.color;
            color.a=reveal*Mathf.Clamp01((1f-progress)*1.45f);
            cut.color=color;
            cut.transform.localScale=new Vector3(
                fullScale.x*Mathf.Lerp(0.55f,1f,reveal),
                fullScale.y*Mathf.Lerp(1f,0.72f,progress),1f);
            if(progress>=1f)Destroy(gameObject);
        }
    }
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(10020)]
public sealed class PixelBearUpperBodyHologram:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private const int FramePixels=256;
    private static readonly Dictionary<string,Sprite[]> spiritFrameCache=
        new Dictionary<string,Sprite[]>(StringComparer.Ordinal);
    private SpriteRenderer source;
    private SpriteRenderer hologram;
    private SpriteMonsterAnimationDriver animationDriver;
    private float visualScale;
    private float sourceScaleAtConfigure;

    public void Configure(SpriteRenderer sourceRenderer,SpriteRenderer output)
    {
        skillClock=CombatSkillClock.Capture();
        source=sourceRenderer;
        hologram=output;
        animationDriver=source.GetComponent<SpriteMonsterAnimationDriver>();
        sourceScaleAtConfigure=Mathf.Max(
            0.001f,Mathf.Abs(source.transform.localScale.y));
        SyncVisual();
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(source==null||hologram==null)
            {
                Destroy(gameObject);
                return;
            }
            SyncVisual();
            float pulse=1f+Mathf.Sin((skillClock==null?Time.time:skillClock.Time)*2.8f)*0.005f;
            transform.localScale=Vector3.one*(StableScale()*pulse);
            hologram.color=new Color(
                0.58f,0.92f,1f,0.34f+Mathf.Sin((skillClock==null?Time.time:skillClock.Time)*3.2f)*0.035f);
        }
    }

    private void SyncVisual()
    {
        if(source==null||hologram==null||source.sprite==null)return;
        string motion=animationDriver!=null
            ?animationDriver.CurrentMotionName:"Idle";
        Sprite[] frames=SpiritFrames(motion);
        if(frames.Length==0)return;
        int frameIndex=animationDriver!=null
            ?animationDriver.CurrentFrameIndex:0;
        hologram.sprite=frames[Mathf.Clamp(frameIndex,0,frames.Length-1)];
        if(visualScale<=0f)
            visualScale=1.24f*source.sprite.bounds.size.y/
                hologram.sprite.bounds.size.y;
        hologram.flipX=source.flipX;
        hologram.flipY=source.flipY;
        hologram.sortingLayerID=source.sortingLayerID;
        hologram.sortingOrder=source.sortingOrder-2;
        Bounds bounds=source.sprite.bounds;
        float backOffset=bounds.size.x*0.14f;
        float stableScale=StableScale();
        bool broadBear=source.sprite.name.StartsWith("Pixel026_",StringComparison.Ordinal)||
                       source.sprite.name.StartsWith("Pixel027_",StringComparison.Ordinal);
        float overlapOffset=broadBear?0.48f:0.65f;
        transform.localPosition=new Vector3(
            bounds.center.x+(source.flipX?backOffset:-backOffset),
            bounds.max.y-hologram.sprite.bounds.extents.y*stableScale*overlapOffset,
            0.02f);
        transform.localScale=Vector3.one*stableScale;
        hologram.color=new Color(0.58f,0.92f,1f,0.34f);
    }

    private float StableScale()
    {
        return visualScale*sourceScaleAtConfigure/Mathf.Max(
            0.001f,Mathf.Abs(source.transform.localScale.y));
    }

    internal static Sprite[] SpiritFrames(string motion,bool grounded=false)
    {
        string key=grounded?motion+"_Grounded":motion;
        if(spiritFrameCache.TryGetValue(key,out Sprite[] frames))return frames;
        Texture2D strip=Resources.Load<Texture2D>(
            PixelCombatEffect.BearMountainHologramResourcePath+motion);
        if(strip==null)return Array.Empty<Sprite>();
        frames=new Sprite[Mathf.Max(1,strip.width/FramePixels)];
        for(int index=0;index<frames.Length;index++)
        {
            frames[index]=Sprite.Create(
                strip,new Rect(index*FramePixels,0f,FramePixels,FramePixels),
                // Every source strip shares a sole baseline at pixel 82 from the bottom.
                // The old centred pivot is still used by the upper-body spirit hologram.
                new Vector2(0.5f,grounded?82f/FramePixels:0.5f),FramePixels,0,SpriteMeshType.Tight);
            frames[index].name="OriginalBear_"+motion+"_"+index.ToString("00");
            frames[index].hideFlags=HideFlags.HideAndDontSave;
        }
        spiritFrameCache[key]=frames;
        return frames;
    }
}

[DisallowMultipleComponent]
public sealed class PixelBearKingRoarSpirit:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private SpriteRenderer source;
    private SpriteRenderer spirit;
    private SpriteMonsterAnimationDriver animationDriver;
    private Sprite[] frames=Array.Empty<Sprite>();
    private Bounds sourceBounds;
    private float sourceScaleAtConfigure;
    private float visualScale;
    private float elapsed;
    private bool observedSkillAnimation;
    private bool ending;

    public void Configure(SpriteRenderer sourceRenderer,SpriteRenderer output,bool final=false)
    {
        skillClock=CombatSkillClock.Capture();
        source=sourceRenderer;ending=final;
        spirit=output;
        animationDriver=source.GetComponent<SpriteMonsterAnimationDriver>();
        frames=PixelBearUpperBodyHologram.SpiritFrames("SkillAttack");
        sourceBounds=source.sprite.bounds;
        sourceScaleAtConfigure=Mathf.Max(
            0.001f,Mathf.Abs(source.transform.localScale.y));
        if(frames.Length==0)
        {
            Destroy(gameObject);
            return;
        }
        spirit.sprite=frames[0];
        visualScale=(ending?2.35f:1.9f)*sourceBounds.size.y/
            Mathf.Max(0.01f,spirit.sprite.bounds.size.y);
        SyncFrame(0);
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(source==null||!source.gameObject.activeInHierarchy||spirit==null||frames.Length==0||
               animationDriver!=null&&animationDriver.CurrentMotionName=="Death")
            {
                Destroy(gameObject);
                return;
            }
            elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            bool skillAnimation=animationDriver!=null&&string.Equals(
                animationDriver.CurrentMotionName,"SkillAttack",StringComparison.Ordinal);
            if(skillAnimation)
            {
                observedSkillAnimation=true;
                SyncFrame(Mathf.Clamp(
                    animationDriver.CurrentFrameIndex,0,frames.Length-1));
                return;
            }
            if(animationDriver==null)
            {
                int frame=Mathf.FloorToInt(elapsed*16f);
                if(frame<frames.Length)SyncFrame(frame);
                else Destroy(gameObject);
                return;
            }
            if(observedSkillAnimation||elapsed>0.6f)Destroy(gameObject);
        }
    }

    private void SyncFrame(int frameIndex)
    {
        spirit.sprite=frames[frameIndex];
        spirit.flipX=source.flipX;
        spirit.flipY=source.flipY;
        spirit.sortingLayerID=source.sortingLayerID;
        spirit.sortingOrder=source.sortingOrder-2;
        float stableScale=visualScale*sourceScaleAtConfigure/Mathf.Max(
            0.001f,Mathf.Abs(source.transform.localScale.y));
        float progress=frames.Length<=1?1f:frameIndex/(float)(frames.Length-1);
        float rise=Mathf.SmoothStep(0f,sourceBounds.size.y*0.08f,progress);
        float sideOffset=sourceBounds.size.x*0.06f;
        transform.localPosition=new Vector3(
            sourceBounds.center.x+(source.flipX?sideOffset:-sideOffset),
            sourceBounds.max.y-spirit.sprite.bounds.extents.y*stableScale*0.48f+rise,
            -0.015f);
        transform.localScale=Vector3.one*stableScale;
        float reveal=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress/0.18f));
        float fade=1f-Mathf.SmoothStep(
            0f,1f,Mathf.Clamp01((progress-0.72f)/0.28f));
        float breath=frameIndex<2?.93f:1f+Mathf.Sin(Mathf.Clamp01((progress-.3f)/.5f)*Mathf.PI)*.06f;
        transform.localScale=new Vector3(stableScale/breath,stableScale*breath,stableScale);
        spirit.color=ending?new Color(1f,.9f,.66f,.95f*reveal*fade):
            new Color(.78f,.94f,1f,.8f*reveal*fade);
    }
}

[DisallowMultipleComponent]
public sealed class PixelWizardStarFan:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private readonly SpriteRenderer[] stars=new SpriteRenderer[WizardMouseCombatRules.ProjectileCount];
    private Transform source;
    private SpriteRenderer reference;
    private Func<bool> keepAlive;
    private Color color;
    private float elapsed;

    public static PixelWizardStarFan Play(Transform source,SpriteRenderer reference,Color color,Func<bool> keepAlive)
    {
        var fan=new GameObject("Wizard Mouse Ready Stars").AddComponent<PixelWizardStarFan>();
        fan.source=source;fan.reference=reference;fan.color=color;fan.keepAlive=keepAlive;
        for(int i=0;i<fan.stars.Length;i++)
        {
            var star=new GameObject("Ready Star "+(i+1)).AddComponent<SpriteRenderer>();
            star.transform.SetParent(fan.transform,false);
            star.sprite=PixelStarProjectile.SharedSprite;
            star.sortingLayerID=reference==null?0:reference.sortingLayerID;
            star.sortingOrder=(reference==null?0:reference.sortingOrder)+30;
            fan.stars[i]=star;
        }
        fan.UpdateVisual();
        return fan;
    }

    public Vector3 Release(int index)
    {
        UpdateVisual();
        SpriteRenderer star=stars[index];
        Vector3 position=star.transform.position;
        star.enabled=false;
        return position;
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            elapsed+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(source==null||elapsed>1.5f||(keepAlive!=null&&!keepAlive()))
            {
                Destroy(gameObject);
                return;
            }
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if(source==null)return;
        Camera camera=Camera.main;
        Vector3 right=camera==null?Vector3.right:camera.transform.right;
        Vector3 up=camera==null?Vector3.up:camera.transform.up;
        Vector3 center=reference==null?source.position+Vector3.up*0.8f:reference.bounds.center;
        float height=reference==null?0.8f:Mathf.Max(0.6f,reference.bounds.size.y);
        float spread=Mathf.SmoothStep(0f,1f,elapsed/WizardMouseCombatRules.PreparationDuration);
        for(int i=0;i<stars.Length;i++)
        {
            float angle=Mathf.Lerp(155f,25f,i/(float)(stars.Length-1))*Mathf.Deg2Rad;
            Vector3 offset=right*(Mathf.Cos(angle)*height*0.85f)+
                           up*(Mathf.Sin(angle)*height*0.65f+height*0.3f);
            stars[i].transform.position=center+offset*Mathf.Lerp(0.35f,1f,spread);
            if(camera!=null)stars[i].transform.rotation=camera.transform.rotation;
            float pulse=1f+Mathf.Sin(elapsed*16f+i)*0.06f;
            stars[i].transform.localScale=Vector3.one*PixelStarProjectile.VisualScale(false)*PixelStarProjectile.WizardStarVisualMultiplier*
                Mathf.Lerp(0.12f,1f,spread)*pulse*(i==4?1.15f:1f);
            Color tint=Color.Lerp(color,Color.white,i==4?0.5f:0.2f);
            tint.a=Mathf.Lerp(0.15f,1f,spread);
            stars[i].color=tint;
        }
    }
}

public sealed class PixelStarProjectile:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    public const float CriticalScaleMultiplier=2f;
    internal const float WizardStarVisualMultiplier=2f;
    internal const float MushroomSporeVisualMultiplier=2f;
    private static float NormalScale=>0.72f*PixelCombatEffect.PreviewVisualScale;
    private const int TrailCount=4;
    private static Sprite sharedSprite;
    private static Sprite sharedStone;
    private static Sprite sharedBean;
    private static Sprite sharedMonkeyFruit;
    private static readonly Sprite[] crackedMonkeyFruit=new Sprite[2];
    private static Sprite sharedSilkNeedle;
    private static Sprite sharedWebBundle;
    private static Sprite sharedScytheWind;
    private static Sprite[] sharedWingTornadoFrames;
    private static Sprite sharedSpiritLight;
    private static Material sharedImpactMaterial;
    private static Material sharedStoneImpactMaterial;
    private static Material sharedBeanImpactMaterial;

    private readonly SpriteRenderer[] trail=new SpriteRenderer[TrailCount];
    private SpriteRenderer head;
    private Transform target;
    private SpriteRenderer targetRenderer;
    private Vector3 start;
    private Vector3 lastTarget;
    private float duration;
    private float elapsed;
    private float curveSide;
    private Color projectileColor;
    private Func<float> concertClock;
    private Func<bool> concertValid;
    private float concertPreviousTime;
    private bool critical;
    private bool playImpact=true;
    private bool orientToPath;
    private bool beanExplosion;
    private bool straightPath;
    private bool milkyWayArc;
    private bool monkeyArc;
    private float monkeyArcHeight;
    private int kingFruitStage=-1;
    private float fruitLandingAge=-1f;
    private Vector3 fruitLandingScale;
    private Vector3 milkyWaySide;
    private LineRenderer milkyWayTrail;
    private float beanExplosionRadius;
    private int activeTrailCount=TrailCount;
    private float projectileSpeed=18f;
    private float visualScaleMultiplier=1f;
    private Sprite projectileSprite;
    private Sprite[] projectileFrames;
    private float projectileFramesPerSecond;
    private GameObject ownedTarget;
    private Action onArrive;
    private CombatProjectileSource.Flight flight;
    private float flightClock;
    private bool cancelled;
    private bool readabilityBodyAnchors;
    private Vector3 readabilityStartOffset,readabilityLogicalHead,readabilityIncomingDirection;

    public void UseReadabilityBodyAnchors(SpriteRenderer source)
    {
        if(!CombatReadabilityPresentation.RefinedSkills||source==null||source.sprite==null||
           targetRenderer==null||targetRenderer.sprite==null||head==null)return;
        Vector3 origin=CombatReadabilityPresentation.VisibleBodyCenter(source);
        Vector3 direction=CombatReadabilityPresentation.VisibleBodyCenter(targetRenderer)-origin;
        if(direction.sqrMagnitude>.001f)origin+=direction.normalized*.12f;
        readabilityBodyAnchors=true;
        readabilityStartOffset=origin-start;
        readabilityIncomingDirection=direction;
        UpdateVisual(0f);
    }

    private Vector3 ReadabilityFlightOffset(float progress)
    {
        if(!readabilityBodyAnchors||!CombatReadabilityPresentation.RefinedSkills)return Vector3.zero;
        Vector3 end=targetRenderer==null||targetRenderer.sprite==null?Vector3.zero:
            CombatReadabilityPresentation.VisibleBodyCenter(targetRenderer)-lastTarget;
        return Vector3.Lerp(readabilityStartOffset,end,progress);
    }
    public bool Deflected { get; private set; }
    public Action OnDeflected;

    public void SetCombatOwner(Transform owner)
    {
        flight.finished?.Invoke();
        flight=CombatProjectileSource.Capture(owner);
        flightClock=flight.clock==null?0:flight.clock();
    }

    private Action<float,float> scytheSweep;
    private Func<bool> scytheValid;
    private Func<float,Vector3> scythePath;
    private float scytheProgress;

    public static Sprite SharedSprite
    {
        get
        {
            if(sharedSprite==null)sharedSprite=CreateStarSprite();
            return sharedSprite;
        }
    }

    public static float VisualScale(bool critical)
    {
        return NormalScale*(critical?CriticalScaleMultiplier:1f);
    }

    public float FlightDuration => duration;
    public bool FruitBounceReady => fruitLandingAge>=.08f;

    public void SetImpactEnabled(bool enabled){playImpact=enabled;}

    public void LaunchFrom(Vector3 position,int starIndex)
    {
        start=position;
        duration=Mathf.Clamp(Vector3.Distance(start,lastTarget)/22f,0.14f,0.34f);
        curveSide=starIndex%2==0?1f:-1f;
        UpdateVisual(0f);
    }

    public void SetMilkyWayArc(int fragmentIndex)
    {
        milkyWayArc=true;
        duration=WizardMouseCombatRules.MilkyWayFragmentFlightDuration;
        Vector3 direction=lastTarget-start;
        Camera camera=Camera.main;
        Vector3 normal=camera==null?Vector3.up:camera.transform.forward;
        Vector3 side=Vector3.Cross(normal,direction).normalized;
        if(side.sqrMagnitude<0.001f)side=camera==null?Vector3.forward:camera.transform.right;
        milkyWaySide=side*(fragmentIndex%2==0?-1f:1f)*
            Mathf.Clamp(direction.magnitude*0.9f,1.6f,4f);
        milkyWayTrail=gameObject.AddComponent<LineRenderer>();
        milkyWayTrail.sharedMaterial=head.sharedMaterial;
        milkyWayTrail.useWorldSpace=true;
        milkyWayTrail.positionCount=16;
        milkyWayTrail.startWidth=0.015f*visualScaleMultiplier*PixelCombatEffect.PreviewVisualScale;
        milkyWayTrail.endWidth=0.16f*visualScaleMultiplier*PixelCombatEffect.PreviewVisualScale;
        milkyWayTrail.startColor=new Color(projectileColor.r,projectileColor.g,projectileColor.b,0f);
        milkyWayTrail.endColor=Color.Lerp(projectileColor,Color.white,0.35f);
        milkyWayTrail.sortingLayerID=head.sortingLayerID;
        milkyWayTrail.sortingOrder=head.sortingOrder-1;
        milkyWayTrail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        milkyWayTrail.receiveShadows=false;
        for(int i=0;i<activeTrailCount;i++)
        {
            trail[i].transform.localScale=Vector3.one*NormalScale*visualScaleMultiplier*(0.72f-i*0.1f);
            Color tint=projectileColor;tint.a=0.65f-i*0.12f;trail[i].color=tint;
        }
        SpriteRenderer core=CreateRenderer("Star Core",head.sortingLayerID,head.sortingOrder+1,
            Color.Lerp(projectileColor,Color.white,0.85f));
        core.transform.SetParent(head.transform,false);
        core.transform.localScale=Vector3.one*0.38f;
        UpdateVisual(0f);
    }

    public static ParticleSystem PlayMilkyWayImpact(
        Vector3 position,SpriteRenderer renderer,Color color,bool critical)
    {
        ParticleSystem burst=PlayImpact(position,renderer,color,critical,false,1.65f);
        burst.name="Milky Way Heavy Star Burst";
        burst.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=burst.main;
        main.maxParticles=20;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.22f,0.38f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(3f,5f);
        main.gravityModifier=0.35f;
        var size=burst.sizeOverLifetime;size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(
            new Keyframe(0f,1f),new Keyframe(0.15f,0.65f),
            new Keyframe(0.3f,1.35f),new Keyframe(1f,0.05f)));
        Gradient fade=new Gradient();
        fade.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(color,0.4f)},
            new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(1f,0.4f),new GradientAlphaKey(0f,1f)});
        var fading=burst.colorOverLifetime;fading.enabled=true;fading.color=fade;
        burst.Emit(14);
        var flash=new ParticleSystem.EmitParams
        {
            position=Vector3.zero,velocity=Vector3.zero,startLifetime=0.14f,
            startSize=(critical?1.15f:0.85f)*PixelCombatEffect.PreviewVisualScale,startColor=Color.Lerp(color,Color.white,0.85f),
            applyShapeToPosition=false
        };
        // EmitParams positions use world coordinates because the shared star burst does.
        flash.position=position;
        burst.Emit(flash,1);
        burst.Play();
        return burst;
    }

    public static Sprite SilkNeedleSprite
    {
        get
        {
            if(sharedSilkNeedle==null)sharedSilkNeedle=CreateSilkNeedleSprite();
            return sharedSilkNeedle;
        }
    }

    public static Sprite StoneSprite
    {
        get
        {
            if(sharedStone==null)sharedStone=CreateStoneSprite();
            return sharedStone;
        }
    }

    public static Sprite BeanSprite
    {
        get
        {
            if(sharedBean==null)sharedBean=CreateBeanSprite();
            return sharedBean;
        }
    }

    public static Sprite MonkeyFruitSprite
    {
        get
        {
            if(sharedMonkeyFruit==null)sharedMonkeyFruit=CreateMonkeyFruitSprite();
            return sharedMonkeyFruit;
        }
    }

    public static Sprite WebBundleSprite
    {
        get
        {
            if(sharedWebBundle==null)sharedWebBundle=CreateWebBundleSprite();
            return sharedWebBundle;
        }
    }

    public static Sprite ScytheWindSprite
    {
        get
        {
            if(sharedScytheWind==null)sharedScytheWind=CreateScytheWindSprite();
            return sharedScytheWind;
        }
    }

    public static Sprite SpiritLightSprite
    {
        get
        {
            if(sharedSpiritLight==null)sharedSpiritLight=CreateSpiritLightSprite();
            return sharedSpiritLight;
        }
    }

    public static Sprite[] WingTornadoFrames
    {
        get
        {
            if(sharedWingTornadoFrames==null)
                sharedWingTornadoFrames=CreateWingTornadoFrames();
            return sharedWingTornadoFrames;
        }
    }

    public static PixelStarProjectile Play(
        Transform source,SpriteRenderer sourceRenderer,
        Transform target,SpriteRenderer targetRenderer,
        Color color,bool critical,Action onArrive=null)
    {
        if(source==null||target==null)return null;
        GameObject root=new GameObject("Pixel Guided Star");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.Configure(
            source,sourceRenderer,target,targetRenderer,color,critical,onArrive);
        projectile.MultiplyVisualScale(WizardStarVisualMultiplier);
        return projectile;
    }

    public static PixelStarProjectile PlaySilkNeedle(
        Transform source,SpriteRenderer sourceRenderer,
        Transform target,SpriteRenderer targetRenderer,
        Color color,bool critical,Action onArrive)
    {
        if(source==null||target==null)return null;
        GameObject root=new GameObject("Pixel Silk Needle");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,target,targetRenderer,color,critical,onArrive,
            SilkNeedleSprite,false,TrailCount,true,24f,null);
        return projectile;
    }

    public static PixelStarProjectile PlayStone(
        Transform source,SpriteRenderer sourceRenderer,
        Transform target,SpriteRenderer targetRenderer,
        Color color,Action onArrive)
    {
        if(source==null||target==null)return null;
        GameObject root=new GameObject("Pixel Monkey Stone");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,target,targetRenderer,color,false,onArrive,
            StoneSprite,true,2,true,18f,null);
        projectile.SetMonkeyArc(false,true);
        return projectile;
    }

    public static PixelStarProjectile PlayBean(
        Transform source,SpriteRenderer sourceRenderer,
        Transform target,SpriteRenderer targetRenderer,
        Color color,bool explosive,float explosionRadius,Action onArrive)
    {
        if(source==null||target==null)return null;
        GameObject root=new GameObject(explosive
            ?"Pixel Monkey Last Bean":"Pixel Monkey Bean");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,target,targetRenderer,color,false,onArrive,
            explosive?MonkeyFruitSprite:BeanSprite,true,2,true,20f,null);
        projectile.beanExplosion=explosive;
        projectile.beanExplosionRadius=Mathf.Max(0f,explosionRadius);
        projectile.SetMonkeyArc(explosive,false);
        projectile.MultiplyVisualScale(MonkeyCombatRules.SkillProjectileVisualMultiplier);
        return projectile;
    }

    private void SetMonkeyArc(bool heavy,bool basic)
    {
        monkeyArc=true;
        float distance=Vector3.Distance(start,lastTarget);
        monkeyArcHeight=heavy?Mathf.Clamp(distance*.4f,1.7f,3.2f):
            basic?Mathf.Clamp(distance*.36f,1.05f,2.45f):Mathf.Clamp(distance*.28f,.75f,1.75f);
        duration=heavy?Mathf.Clamp(distance/13f,.46f,.66f):
            basic?Mathf.Clamp(distance/13f,.34f,.58f):Mathf.Clamp(distance/20f,.26f,.42f);
        for(int i=0;i<activeTrailCount;i++)
        {
            Color tint=projectileColor;tint.a=.22f-i*.08f;trail[i].color=tint;
        }
        UpdateVisual(0f);
    }

    public static PixelStarProjectile PlayKingFruit(Vector3 from,Vector3 landing,
        SpriteRenderer reference,Color color,int stage,Action onArrive)
    {
        var marker=new GameObject("King Fruit Landing");
        marker.transform.position=landing+Vector3.up*.2f;
        var root=new GameObject("Monkey King Fruit "+stage);
        root.transform.position=from-Vector3.up*.35f;
        var fruit=root.AddComponent<PixelStarProjectile>();
        fruit.ConfigureProjectile(root.transform,null,marker.transform,null,color,false,onArrive,
            KingFruitSprite(stage),false,2,false,13f,marker);
        fruit.SetCombatOwner(reference==null?null:reference.transform);
        fruit.start=from;
        fruit.SetMonkeyArc(true,false);
        fruit.kingFruitStage=stage;
        if(stage>0)
        {
            fruit.duration=stage==1?.3f:.44f;
            fruit.monkeyArcHeight=stage==1?1.05f:2.1f;
        }
        fruit.MultiplyVisualScale(1.25f);
        foreach(var part in fruit.GetComponentsInChildren<SpriteRenderer>())
        {
            part.sortingLayerID=reference==null?0:reference.sortingLayerID;
            part.sortingOrder=(reference==null?0:reference.sortingOrder)+30;
        }
        fruit.UpdateVisual(0f);
        return fruit;
    }

    private static Sprite KingFruitSprite(int cracks)
    {
        if(cracks<=0)return MonkeyFruitSprite;
        int index=Mathf.Clamp(cracks-1,0,1);
        if(crackedMonkeyFruit[index]==null)crackedMonkeyFruit[index]=CreateMonkeyFruitSprite(index+1);
        return crackedMonkeyFruit[index];
    }

    public static PixelStarProjectile PlayCloudBean(
        Transform source,SpriteRenderer sourceRenderer,
        Transform target,SpriteRenderer targetRenderer,Action onArrive)
    {
        if(source==null||target==null)return null;
        GameObject root=new GameObject("Pixel Monkey Cloud Bounce");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,target,targetRenderer,
            new Color(0.78f,0.95f,1f),false,onArrive,
            BeanSprite,true,3,true,14f,null);
        projectile.MultiplyVisualScale(1.3f*MonkeyCombatRules.SkillProjectileVisualMultiplier);
        return projectile;
    }

    public static PixelStarProjectile PlaySporeShot(
        Transform source,SpriteRenderer sourceRenderer,
        Transform target,SpriteRenderer targetRenderer,
        Color color,Action onArrive)
    {
        if(source==null||target==null)return null;
        GameObject root=new GameObject("Pixel Mushroom Spore Shot");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,target,targetRenderer,color,false,
            ()=>
            {
                PlaySporeImpact(projectile.lastTarget,targetRenderer,color);
                onArrive?.Invoke();
            },BeanSprite,false,3,false,17f,null);
        projectile.head.transform.localScale*=1.25f;
        projectile.MultiplyVisualScale(MushroomSporeVisualMultiplier);
        return projectile;
    }

    public static PixelStarProjectile PlaySpiritLight(
        Transform source,SpriteRenderer sourceRenderer,
        Transform target,SpriteRenderer targetRenderer,
        Color color,Action onArrive)
    {
        if(source==null||target==null)return null;
        GameObject root=new GameObject("Pixel Spirit Deer Light");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,target,targetRenderer,color,false,
            () =>
            {
                PlaySpiritLeafImpact(projectile.lastTarget,targetRenderer,color);
                onArrive?.Invoke();
            },SpiritLightSprite,false,3,false,20f,null);
        return projectile;
    }

    public static PixelStarProjectile PlaySporeSplit(
        Transform source,SpriteRenderer sourceRenderer,
        Vector3 destination,Color color,Action onArrive)
    {
        if(source==null)return null;
        GameObject marker=new GameObject("Mushroom Split Spore Target");
        marker.transform.position=destination-Vector3.up*0.35f;
        GameObject root=new GameObject("Pixel Mushroom Split Spore");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,marker.transform,null,color,false,
            ()=>
            {
                PlaySporeImpact(projectile.lastTarget,sourceRenderer,color);
                onArrive?.Invoke();
            },BeanSprite,false,2,false,16f,marker);
        projectile.head.transform.localScale*=1.15f;
        projectile.MultiplyVisualScale(MushroomSporeVisualMultiplier);
        return projectile;
    }

    public static PixelStarProjectile PlayWebBundle(
        Transform source,SpriteRenderer sourceRenderer,
        Vector3 destination,Color color,Action onArrive)
    {
        if(source==null)return null;
        GameObject marker=new GameObject("Spider Web Target");
        marker.transform.position=destination-Vector3.up*0.3f;
        GameObject root=new GameObject("Pixel Web Bundle");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,marker.transform,null,color,false,onArrive,
            WebBundleSprite,false,2,false,16f,marker);
        return projectile;
    }

    public static PixelStarProjectile PlayScytheWind(
        Transform source,SpriteRenderer sourceRenderer,
        Vector3 destination,Color color,float scale,Vector3? groundOrigin=null,
        float halfWidth=0f,bool reverse=false,Action<float,float> sweep=null,
        Func<bool> valid=null)
    {
        if(source==null)return null;
        GameObject marker=new GameObject("Mantis Scythe Wind Target");
        marker.transform.position=destination;
        GameObject root=new GameObject("Pixel Mantis Scythe Wind");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,marker.transform,null,color,false,null,
            ScytheWindSprite,false,2,true,MantisCombatRules.WindSpeed,marker);
        projectile.straightPath=true;
        if(groundOrigin.HasValue)
            projectile.start=new Vector3(groundOrigin.Value.x,projectile.start.y,groundOrigin.Value.z);
        marker.transform.position=new Vector3(destination.x,projectile.start.y-.35f,destination.z);
        projectile.lastTarget=marker.transform.position+Vector3.up*.35f;
        projectile.duration=Mathf.Max(.12f,Vector3.Distance(projectile.start,projectile.lastTarget)/MantisCombatRules.WindSpeed);
        Vector3 direction=(projectile.lastTarget-projectile.start).normalized;
        Vector3 across=Vector3.Cross(Vector3.up,direction)*halfWidth*2f;
        float span=Camera.main==null?across.magnitude:
            Vector3.ProjectOnPlane(across,Camera.main.transform.forward).magnitude;
        float visualScale=halfWidth>0f?Mathf.Max(.8f,span/ScytheWindSprite.bounds.size.y):Mathf.Max(.8f,scale);
        projectile.head.transform.localScale=Vector3.one*visualScale*PixelCombatEffect.PreviewVisualScale;
        projectile.head.flipY=reverse;
        projectile.head.color=Color.Lerp(color,Color.white,.38f);
        for(int i=0;i<projectile.activeTrailCount;i++)
        {
            projectile.trail[i].transform.localScale=new Vector3(.72f-i*.16f,.86f-i*.15f,1f)*visualScale*PixelCombatEffect.PreviewVisualScale;
            projectile.trail[i].flipY=reverse;
            projectile.trail[i].color=new Color(color.r,color.g,color.b,.22f-i*.12f);
        }
        projectile.scytheSweep=sweep;projectile.scytheValid=valid;
        projectile.UpdateVisual(0f);
        return projectile;
    }

    public static PixelStarProjectile PlayMyriadScythe(
        Transform source,SpriteRenderer reference,Vector3 origin,Vector3 forward,
        float angle,float range,float height,Color color,bool reverse,
        Action<float,float> sweep,Func<bool> valid)
    {
        Vector3 end=MantisRank3Rules.MyriadPoint(origin,forward,angle,range,1f);
        var shot=PlayScytheWind(source,reference,end,color,1f,origin,0f,reverse,sweep,valid);
        if(shot==null)return null;
        shot.name="Pixel Mantis Myriad Scythe";
        float y=shot.start.y;
        shot.scythePath=t=>MantisRank3Rules.MyriadPoint(origin,forward,angle,range,t)+Vector3.up*y;
        shot.duration=Mathf.Max(.1f,range/MantisRank3Rules.MyriadFlightSpeed);
        shot.SizeMyriadScythe(height);
        shot.onArrive=()=>DissolveMyriadScythe(end+Vector3.up*y,reference,color);
        shot.UpdateVisual(0f);return shot;
    }

    public static PixelStarProjectile PlayMyriadEcho(Vector3 origin,SpriteRenderer reference,
        Transform target,SpriteRenderer targetRenderer,float height,Color color,Action hit,Func<bool> valid)
    {
        if(target==null)return null;
        var root=new GameObject("Pixel Mantis Myriad Echo");
        var shot=root.AddComponent<PixelStarProjectile>();
        shot.ConfigureProjectile(target,reference,target,targetRenderer,color,false,null,
            ScytheWindSprite,false,2,true,MantisRank3Rules.MyriadFlightSpeed,null);
        shot.start=origin;shot.straightPath=true;shot.scytheValid=valid;
        shot.duration=Mathf.Max(.12f,Vector3.Distance(origin,shot.lastTarget)/MantisRank3Rules.MyriadFlightSpeed);
        shot.SizeMyriadScythe(height);
        shot.onArrive=()=>{if(valid())hit();DissolveMyriadScythe(shot.lastTarget,targetRenderer,color);};
        shot.UpdateVisual(0f);return shot;
    }

    private void SizeMyriadScythe(float height)
    {
        float size=height/ScytheWindSprite.bounds.size.y*PixelCombatEffect.PreviewVisualScale;
        head.transform.localScale=Vector3.one*size;
        head.color=Color.Lerp(projectileColor,Color.white,.42f);
        for(int i=0;i<activeTrailCount;i++)
        {
            trail[i].transform.localScale=new Vector3(.7f-i*.15f,.85f-i*.16f,1f)*size;
            trail[i].color=new Color(projectileColor.r,projectileColor.g,projectileColor.b,.24f-i*.1f);
        }
    }

    private static void DissolveMyriadScythe(Vector3 point,SpriteRenderer reference,Color color)
    {
        var bits=PlaySpiritLeafImpact(point,reference,color,"Mantis Scythe Dissolve",.7f,1.8f,0f,0f,.5f,1);
        bits.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=bits.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.08f,.16f);
        main.startSize=new ParticleSystem.MinMaxCurve(.035f,.065f);PixelCombatEffect.ScaleParticleSize(main);
        bits.Emit(5);bits.Play();
    }

    public static PixelStarProjectile PlayWingGust(
        Transform source,SpriteRenderer sourceRenderer,
        Transform target,SpriteRenderer targetRenderer,
        Color color,Action onArrive)
    {
        if(source==null||target==null)return null;
        GameObject root=new GameObject("Pixel Wyvern Wing Gust");
        PixelStarProjectile projectile=root.AddComponent<PixelStarProjectile>();
        projectile.ConfigureProjectile(
            source,sourceRenderer,target,targetRenderer,color,false,
            () =>
            {
                PlayWingGustImpact(projectile.lastTarget,targetRenderer,color);
                onArrive?.Invoke();
            },WingTornadoFrames[0],false,3,false,18f,null,
            WingTornadoFrames,18f);
        projectile.straightPath=true;
        projectile.head.transform.localScale*=0.9f;
        for(int i=0;i<projectile.activeTrailCount;i++)
            projectile.trail[i].transform.localScale*=0.9f;
        return projectile;
    }

    public void SetCritical(bool critical)
    {
        this.critical=critical;
        if(head!=null)head.transform.localScale=Vector3.one*VisualScale(critical)*visualScaleMultiplier;
    }

    public void MultiplyVisualScale(float multiplier)
    {
        float scale=Mathf.Max(0.1f,multiplier);
        visualScaleMultiplier*=scale;
        if(head!=null)head.transform.localScale*=scale;
        for(int i=0;i<activeTrailCount;i++)
            if(trail[i]!=null)trail[i].transform.localScale*=scale;
    }

    public static ParticleSystem PlayImpact(
        Vector3 position,SpriteRenderer targetRenderer,Color color,bool critical,
        bool stone=false,float visualScale=1f)
    {
        GameObject root=new GameObject(stone?"Pixel Stone Hit":"Pixel Star Hit");
        root.transform.position=position;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;

        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.3f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.2f,0.34f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(1.4f,2.4f);
        float size=(critical?0.25f:0.19f)*Mathf.Max(0.1f,visualScale);
        main.startSize=new ParticleSystem.MinMaxCurve(size*0.75f,size*1.25f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=color;
        main.maxParticles=16;
        main.simulationSpace=ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.enabled=true;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=0.04f;

        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=stone?StoneImpactMaterial:ImpactMaterial;
        renderer.sortingLayerID=targetRenderer==null?0:targetRenderer.sortingLayerID;
        renderer.sortingOrder=(targetRenderer==null?0:targetRenderer.sortingOrder)+35;

        particles.Play();
        particles.Emit(critical?11:8);
        if(Application.isPlaying)Destroy(root,0.8f);
        return particles;
    }

    public static ParticleSystem PlayBeanImpact(
        Vector3 position,SpriteRenderer targetRenderer,Color color,
        bool explosive,float radius,float popScale=1f
        ,bool secondary=false
    )
    {
        PixelCombatEffect.PlayMonkeyFruitPop(position,targetRenderer,color,explosive,popScale
            ,secondary
        );
        GameObject root=new GameObject(explosive
            ?"Pixel Bean Barrage Explosion":"Pixel Bean Hit");
        root.transform.position=position;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;

        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.3f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=explosive
            ?new ParticleSystem.MinMaxCurve(0.22f,0.3f)
            :new ParticleSystem.MinMaxCurve(0.12f,0.2f);
        main.startSpeed=explosive
            ?new ParticleSystem.MinMaxCurve(radius*1.8f,radius*2.6f)
            :new ParticleSystem.MinMaxCurve(0.7f,1.3f);
        main.startSize=explosive
            ?new ParticleSystem.MinMaxCurve(0.14f,0.26f)
            :new ParticleSystem.MinMaxCurve(0.08f,0.13f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=color;
        main.maxParticles=32;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        int fragments=explosive?28:4;
        float fragmentLifetime=.24f,fragmentScale=1f;
        if(secondary&&CombatReadabilityPresentation.RefinedSkills)
        {
            fragments=explosive?12:2;fragmentLifetime=.16f;fragmentScale=.8f;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.1f,.18f);
            main.startSize=new ParticleSystem.MinMaxCurve(explosive?.11f:.065f,explosive?.2f:.105f);PixelCombatEffect.ScaleParticleSize(main);
        }
        if(explosive)
        {
            main.gravityModifier=.7f;
            main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
            var shrink=particles.sizeOverLifetime;shrink.enabled=true;
            shrink.size=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.EaseInOut(0f,1f,1f,.15f));
        }

        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.enabled=true;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=explosive?0.12f:0.025f;

        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BeanImpactMaterial;
        renderer.sortingLayerID=targetRenderer==null?0:targetRenderer.sortingLayerID;
        renderer.sortingOrder=(targetRenderer==null?0:targetRenderer.sortingOrder)+35;

        particles.Play();
        if(explosive)
        {
            // Scatter across the actual XZ damage area, not a camera-facing circle.
            for(int i=0;i<fragments;i++)
            {
                float angle=i*2.399963f;
                float reach=radius*(i%3==0?.72f:1f);
                var chip=new ParticleSystem.EmitParams
                {
                    position=position,applyShapeToPosition=false,
                    velocity=new Vector3(Mathf.Cos(angle)*reach/fragmentLifetime,
                        1.1f+(i%4)*.35f,Mathf.Sin(angle)*reach/fragmentLifetime),
                    startLifetime=fragmentLifetime,startSize=(i%4==0?.27f:.15f)*fragmentScale*PixelCombatEffect.PreviewVisualScale,
                    startColor=i%3==0?Color.Lerp(color,new Color(.2f,.12f,.07f),.6f):
                        Color.Lerp(color,new Color(1f,.96f,.65f),.5f)
                };
                particles.Emit(chip,1);
            }
        }
        else particles.Emit(fragments);
        if(Application.isPlaying)Destroy(root,0.9f);
        return particles;
    }

    public static ParticleSystem PlaySporeImpact(
        Vector3 position,SpriteRenderer targetRenderer,Color color)
    {
        GameObject root=new GameObject("Pixel Mushroom Spore Burst");
        root.transform.position=position;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;

        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.4f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.38f,0.62f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.65f,1.15f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.08f,0.14f);PixelCombatEffect.ScaleParticleSize(main);
        main.startColor=new ParticleSystem.MinMaxGradient(
            color,Color.Lerp(color,Color.white,0.5f));
        main.gravityModifier=-0.12f;
        main.maxParticles=20;
        main.simulationSpace=ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.enabled=true;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=0.08f;

        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BeanImpactMaterial;
        renderer.sortingLayerID=targetRenderer==null?0:targetRenderer.sortingLayerID;
        renderer.sortingOrder=(targetRenderer==null?0:targetRenderer.sortingOrder)+35;

        particles.Emit(16);
        particles.Play();
        if(Application.isPlaying)Destroy(root,1.1f);
        return particles;
    }

    public static ParticleSystem PlaySpiritLeafImpact(
        Vector3 position,SpriteRenderer targetRenderer,Color color,
        string objectName="Pixel Spirit Leaf Burst",float minimumSpeed=0.45f,
        float maximumSpeed=0.9f,float gravity=-0.08f,float noiseStrength=0.16f,
        float visualScale=1f,int particleCount=11)
    {
        GameObject root=new GameObject(objectName);
        root.transform.position=position;
        if(Camera.main!=null)root.transform.rotation=Camera.main.transform.rotation;

        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.3f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.28f,0.48f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(minimumSpeed,maximumSpeed);
        float scale=Mathf.Max(0.1f,visualScale);
        main.startSize=new ParticleSystem.MinMaxCurve(0.045f*scale,0.085f*scale);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
        main.startColor=new ParticleSystem.MinMaxGradient(
            color,Color.Lerp(color,Color.white,0.55f));
        main.gravityModifier=gravity;
        main.maxParticles=Mathf.Max(14,particleCount);
        main.simulationSpace=ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.enabled=true;
        shape.shapeType=ParticleSystemShapeType.Circle;
        shape.radius=0.06f*scale;

        ParticleSystem.NoiseModule noise=particles.noise;
        noise.enabled=true;
        noise.strength=noiseStrength;
        noise.frequency=1.2f;

        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BeanImpactMaterial;
        renderer.sortingLayerID=targetRenderer==null?0:targetRenderer.sortingLayerID;
        renderer.sortingOrder=(targetRenderer==null?0:targetRenderer.sortingOrder)+35;

        particles.Emit(Mathf.Max(1,particleCount));
        particles.Play();
        if(Application.isPlaying)Destroy(root,1f);
        return particles;
    }

    public static ParticleSystem PlayWingGustImpact(
        Vector3 position,SpriteRenderer targetRenderer,Color color)
    {
        ParticleSystem particles=PlaySpiritLeafImpact(
            position,targetRenderer,color,"Pixel Wyvern Wing Gust Impact",
            0.8f,1.5f,-0.02f,0.24f);
        return particles;
    }

    public static ParticleSystem PlaySporeExplosion(
        Vector3 position,SpriteRenderer targetRenderer,Color color,float radius)
    {
        GameObject root=new GameObject("Pixel Mushroom Spore Explosion");
        root.transform.position=position+Vector3.up*0.05f;

        root.AddComponent<CombatEffectEndMarker>();
        ParticleSystem particles=root.AddComponent<ParticleSystem>();
        CombatSkillClock.TrackParticle(particles);
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main=particles.main;
        main.duration=0.45f;
        main.loop=false;
        main.playOnAwake=false;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.58f,0.9f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(0.04f,0.16f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.02f,0.05f);PixelCombatEffect.ScaleParticleSize(main);
        main.startRotation=new ParticleSystem.MinMaxCurve(0f,Mathf.PI*2f);
        main.startColor=new ParticleSystem.MinMaxGradient(
            color,Color.Lerp(color,Color.white,0.58f));
        main.gravityModifier=0f;
        main.maxParticles=64;
        main.simulationSpace=ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission=particles.emission;
        emission.enabled=false;
        ParticleSystem.ShapeModule shape=particles.shape;
        shape.enabled=true;
        shape.shapeType=ParticleSystemShapeType.Sphere;
        shape.radius=Mathf.Max(0.1f,radius);
        shape.radiusThickness=1f;
        shape.scale=new Vector3(1f,0.025f,1f);
        shape.randomDirectionAmount=1f;

        ParticleSystem.NoiseModule noise=particles.noise;
        noise.enabled=true;
        noise.strength=new ParticleSystem.MinMaxCurve(0.05f,0.16f);
        noise.frequency=0.7f;
        noise.scrollSpeed=0.18f;
        noise.damping=true;

        Gradient fade=new Gradient();
        fade.SetKeys(
            new[]{new GradientColorKey(Color.white,0f),
                new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(0.9f,0f),
                new GradientAlphaKey(0.72f,0.55f),
                new GradientAlphaKey(0f,1f)});
        ParticleSystem.ColorOverLifetimeModule fading=particles.colorOverLifetime;
        fading.enabled=true;
        fading.color=new ParticleSystem.MinMaxGradient(fade);

        ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial=BeanImpactMaterial;
        renderer.sortingLayerID=targetRenderer==null?0:targetRenderer.sortingLayerID;
        renderer.sortingOrder=(targetRenderer==null?0:targetRenderer.sortingOrder)+40;

        particles.Emit(Mathf.Clamp(Mathf.RoundToInt(radius*radius*16f),32,56));
        particles.Play();
        if(Application.isPlaying)Destroy(root,1.2f);
        return particles;
    }

    private void Configure(
        Transform source,SpriteRenderer sourceRenderer,
        Transform destination,SpriteRenderer destinationRenderer,
        Color color,bool critical,Action arrival)
    {
        skillClock=CombatSkillClock.Capture();
        ConfigureProjectile(
            source,sourceRenderer,destination,destinationRenderer,color,critical,arrival,
            SharedSprite,true,TrailCount,false,18f,null);
    }

    private void ConfigureProjectile(
        Transform source,SpriteRenderer sourceRenderer,
        Transform destination,SpriteRenderer destinationRenderer,
        Color color,bool critical,Action arrival,
        Sprite visualSprite,bool impact,int trailCount,bool orient,float speed,
        GameObject targetMarker,Sprite[] animationFrames=null,
        float animationFramesPerSecond=0f)
    {
        skillClock=CombatSkillClock.Capture();
        target=destination;
        targetRenderer=destinationRenderer;
        SetCombatOwner(sourceRenderer==null?source:sourceRenderer.transform);
        color=PixelSpiritConcert.Tint(sourceRenderer,color);
        var concert=sourceRenderer==null?null:sourceRenderer.GetComponent<PixelSpiritConcert>();
        if(concert!=null){concertClock=concert.Clock;concertValid=concert.Valid;concertPreviousTime=concertClock();}
        projectileColor=color;
        projectileSprite=visualSprite;
        playImpact=impact;
        activeTrailCount=Mathf.Clamp(trailCount,0,TrailCount);
        orientToPath=orient;
        projectileSpeed=Mathf.Max(1f,speed);
        projectileFrames=animationFrames;
        projectileFramesPerSecond=Mathf.Max(0f,animationFramesPerSecond);
        ownedTarget=targetMarker;
        start=SpriteCenter(source,sourceRenderer);
        lastTarget=SpriteCenter(target,targetRenderer);
        Vector3 direction=lastTarget-start;
        start+=direction.sqrMagnitude>0.001f?direction.normalized*0.12f:Vector3.zero;
        float distance=Vector3.Distance(start,lastTarget);
        duration=Mathf.Clamp(distance/projectileSpeed,0.14f,0.34f);
        curveSide=(GetInstanceID()&1)==0?1f:-1f;
        onArrive=arrival;

        int layer=sourceRenderer==null?0:sourceRenderer.sortingLayerID;
        int order=(sourceRenderer==null?0:sourceRenderer.sortingOrder)+30;
        head=CreateRenderer("Star",layer,order,color);
        SetCritical(critical);
        for(int i=0;i<activeTrailCount;i++)
        {
            Color faded=color;
            faded.a*=0.42f-i*0.075f;
            trail[i]=CreateRenderer("Tail "+(i+1),layer,order-i-1,faded);
            trail[i].transform.localScale=Vector3.one*(NormalScale*(0.52f-i*0.08f));
            trail[i].enabled=false;
        }
        UpdateVisual(0f);
    }

    private SpriteRenderer CreateRenderer(string objectName,int layer,int order,Color color)
    {
        GameObject child=new GameObject(objectName);
        child.transform.SetParent(transform,false);
        SpriteRenderer renderer=child.AddComponent<SpriteRenderer>();
        renderer.sprite=projectileSprite??SharedSprite;
        renderer.color=color;
        renderer.sortingLayerID=layer;
        renderer.sortingOrder=order;
        return renderer;
    }

    public void Cancel()
    {
        if(CombatEffectEndFade.IsFading(this))return;
        if(cancelled)return;cancelled=true;flight.finished?.Invoke();gameObject.SetActive(false);
        onArrive=null;OnDeflected=null;
        scytheSweep=null;scytheValid=null;scythePath=null;
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(flight.valid!=null&&!flight.valid()){Cancel();return;}
            if(concertValid!=null&&!concertValid()){Cancel();return;}
            float dt=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(flight.clock!=null){float now=flight.clock();dt=Mathf.Max(0f,now-flightClock);flightClock=now;}
            if(concertClock!=null){float now=concertClock();dt=Mathf.Max(0f,now-concertPreviousTime);concertPreviousTime=now;}
            Advance(dt);
        }
    }

    private void Advance(float deltaTime)
    {
        // Sample curved flight paths consistently across frame rates.
        while(deltaTime>0f&&!cancelled)
        {
            float step=Mathf.Min(deltaTime,1f/60f);deltaTime-=step;
            AdvanceFlight(step);
        }
    }
    private void AdvanceFlight(float deltaTime)
    {
        if(scytheValid!=null&&!scytheValid()){Cancel();return;}
        elapsed+=deltaTime;
        float progress=Mathf.Clamp01(elapsed/Mathf.Max(0.01f,duration));
        if(fruitLandingAge>=0f)
        {
            fruitLandingAge+=deltaTime;
            float t=Mathf.Clamp01(fruitLandingAge/.08f);
            head.transform.localScale=Vector3.Scale(fruitLandingScale,new Vector3(1.28f-.28f*t,.65f+.35f*t,1f));
            head.transform.position=lastTarget+Vector3.up*
                ((head.transform.localScale.y-fruitLandingScale.y)*head.sprite.bounds.extents.y);
            // Keep the restored fruit visible until the barrage starts the next arc.
            return;
        }
        if(milkyWayArc)progress*=0.55f+0.45f*progress;
        Vector3 previousPosition=head.transform.position;
        Vector3 previousVisualPosition=previousPosition;
        if(readabilityBodyAnchors)previousPosition=readabilityLogicalHead;
        UpdateVisual(progress);
        Vector3 visualDirection=head.transform.position-previousVisualPosition;
        if(readabilityBodyAnchors&&visualDirection.sqrMagnitude>.000001f)
            readabilityIncomingDirection=visualDirection;
        Vector3 collisionPosition=head.transform.position;
        // Body-aligned art must not change the path checked by defensive skills.
        if(readabilityBodyAnchors)collisionPosition=readabilityLogicalHead;
        if(flight.deflect!=null&&flight.deflect(previousPosition,collisionPosition,head))
        {Deflected=true;OnDeflected?.Invoke();Cancel();return;}
        if(scytheSweep!=null&&progress>scytheProgress)
        {
            float previous=scytheProgress;scytheProgress=progress;
            scytheSweep(previous,progress);
        }
        if(progress<1f)return;
        Vector3 impactPosition=lastTarget;
        if(readabilityBodyAnchors&&CombatReadabilityPresentation.RefinedSkills)impactPosition=head.transform.position;
        if(playImpact)
        {
            if(projectileSprite==BeanSprite||projectileSprite==MonkeyFruitSprite)
                PlayBeanImpact(impactPosition,targetRenderer,projectileColor,
                    beanExplosion,beanExplosionRadius
                    ,secondary:!beanExplosion
                );
            else
                PlayImpact(impactPosition,targetRenderer,projectileColor,critical,
                    projectileSprite==StoneSprite);
        }
        Action callback=onArrive;
        onArrive=null;
        var previousContact=CombatReadabilityPresentation.ProjectileContactRenderer;
        Vector3 previousDirection=CombatReadabilityPresentation.ProjectileContactDirection;
        bool connected=readabilityBodyAnchors&&CombatReadabilityPresentation.RefinedSkills&&targetRenderer!=null;
        if(connected)
        {
            CombatReadabilityPresentation.ProjectileContactRenderer=targetRenderer;
            CombatReadabilityPresentation.ProjectileContactDirection=readabilityIncomingDirection;
        }
        try {callback?.Invoke();}
        finally
        {
            if(connected)
            {
                CombatReadabilityPresentation.ProjectileContactRenderer=previousContact;
                CombatReadabilityPresentation.ProjectileContactDirection=previousDirection;
            }
        }
        if(kingFruitStage>=0&&kingFruitStage<2)
        {
            fruitLandingAge=0f;fruitLandingScale=head.transform.localScale;
            head.sprite=KingFruitSprite(kingFruitStage+1);
            if(Camera.main!=null)head.transform.rotation=Camera.main.transform.rotation;
            for(int i=0;i<activeTrailCount;i++)trail[i].enabled=false;
            return;
        }
        Cancel();
    }

    private void UpdateVisual(float progress)
    {
        if(projectileFrames!=null&&projectileFrames.Length>0&&head!=null)
        {
            int frame=Mathf.FloorToInt(elapsed*projectileFramesPerSecond)%
                      projectileFrames.Length;
            head.sprite=projectileFrames[frame];
            for(int i=0;i<activeTrailCount;i++)
                trail[i].sprite=projectileFrames[
                    (frame-i-1+projectileFrames.Length*2)%projectileFrames.Length];
        }
        if(target!=null)lastTarget=SpriteCenter(target,targetRenderer);
        Vector3 direction=lastTarget-start;
        Vector3 flat=new Vector3(direction.x,0f,direction.z);
        Vector3 side=flat.sqrMagnitude<0.001f
            ?Vector3.right
            :new Vector3(-flat.z,0f,flat.x).normalized;
        float distance=direction.magnitude;
        Vector3 control=(start+lastTarget)*0.5f;
        if(!straightPath)
            control+=Vector3.up*Mathf.Clamp(distance*0.1f,0.22f,0.62f)+
                     side*(curveSide*Mathf.Clamp(distance*0.07f,0.16f,0.48f));
        head.transform.position=FlightPosition(control,progress);
        readabilityLogicalHead=head.transform.position;
        head.transform.position+=ReadabilityFlightOffset(progress);
        if(milkyWayArc)
        {
            float grow=Mathf.SmoothStep(0.55f,1f,elapsed/0.04f);
            head.transform.localScale=Vector3.one*NormalScale*visualScaleMultiplier*0.85f*grow;
            for(int i=0;i<milkyWayTrail.positionCount;i++)
                milkyWayTrail.SetPosition(i,FlightPosition(control,
                    Mathf.Lerp(Mathf.Max(0f,progress-0.42f),progress,
                        i/(float)(milkyWayTrail.positionCount-1))));
        }
        for(int i=0;i<activeTrailCount;i++)
        {
            float trailProgress=progress-(i+1)*0.055f;
            trail[i].enabled=trailProgress>0f;
            if(trail[i].enabled)
            {
                trail[i].transform.position=FlightPosition(control,Mathf.Clamp01(trailProgress));
                trail[i].transform.position+=ReadabilityFlightOffset(Mathf.Clamp01(trailProgress));
            }
        }
        Camera camera=Camera.main;
        if(camera==null)return;
        Quaternion rotation=camera.transform.rotation;
        if(monkeyArc)
            rotation*=Quaternion.AngleAxis(curveSide*progress*(beanExplosion||kingFruitStage>=0?220f:460f),Vector3.forward);
        else if(orientToPath)
        {
            Vector3 current=head.transform.position;
            Vector3 next=FlightPosition(control,Mathf.Min(1f,progress+0.02f));
            next+=ReadabilityFlightOffset(Mathf.Min(1f,progress+0.02f));
            Vector3 tangent=next-current;
            if(tangent.sqrMagnitude>0.0001f)
            {
                float angle=Mathf.Atan2(
                    Vector3.Dot(tangent,camera.transform.up),
                    Vector3.Dot(tangent,camera.transform.right))*Mathf.Rad2Deg;
                rotation*=Quaternion.AngleAxis(angle,Vector3.forward);
            }
        }
        head.transform.rotation=rotation;
        for(int i=0;i<activeTrailCount;i++)trail[i].transform.rotation=rotation;
    }

    private Vector3 FlightPosition(Vector3 control,float progress)
    {
        if(scythePath!=null)return scythePath(progress);
        if(monkeyArc)
            return Vector3.Lerp(start,lastTarget,progress)+Vector3.up*(4f*monkeyArcHeight*progress*(1f-progress));
        if(!milkyWayArc)return Bezier(start,control,lastTarget,progress);
        // Fan sideways from the impact, then curl into the target like a magic echo.
        return Bezier(start,(start+lastTarget)*0.5f+milkyWaySide,lastTarget,progress);
    }

    private void OnDestroy()
    {
        flight.finished?.Invoke();
        if(ownedTarget==null)return;
        if(Application.isPlaying)Destroy(ownedTarget);
        else DestroyImmediate(ownedTarget);
    }

    private static Vector3 SpriteCenter(Transform anchor,SpriteRenderer renderer)
    {
        return renderer!=null&&renderer.sprite!=null
            ?anchor.TransformPoint(renderer.sprite.bounds.center)
            :anchor.position+Vector3.up*0.35f;
    }

    private static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,float t)
    {
        float inverse=1f-t;
        return inverse*inverse*a+2f*inverse*t*b+t*t*c;
    }

    private static Sprite CreateStarSprite()
    {
        const int size=9;
        Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
        {
            name="Wizard Mouse Star",
            filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave
        };
        Color32[] pixels=new Color32[size*size];
        int[] widths={1,1,3,5,9,5,3,1,1};
        for(int y=0;y<size;y++)
        {
            int width=widths[y];
            int left=(size-width)/2;
            for(int x=left;x<left+width;x++)
                pixels[y*size+x]=new Color32(255,255,255,
                    (byte)((x==4||y==4)?255:210));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        Sprite sprite=Sprite.Create(
            texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),16f);
        sprite.name="Wizard Mouse Guided Star";
        sprite.hideFlags=HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateSilkNeedleSprite()
    {
        const int width=9,height=5;
        Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
        {
            name="Spider Silk Needle",
            filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave
        };
        Color32[] pixels=new Color32[width*height];
        for(int x=1;x<=7;x++)pixels[2*width+x]=new Color32(225,255,255,255);
        pixels[1*width+6]=new Color32(145,238,255,230);
        pixels[3*width+6]=new Color32(145,238,255,230);
        pixels[1*width+7]=new Color32(205,252,255,245);
        pixels[3*width+7]=new Color32(205,252,255,245);
        pixels[2*width+8]=new Color32(255,255,255,255);
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        Sprite sprite=Sprite.Create(
            texture,new Rect(0f,0f,width,height),new Vector2(0.5f,0.5f),16f);
        sprite.name="Spider Silk Needle";
        sprite.hideFlags=HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateStoneSprite()
    {
        const int width=9,height=7;
        Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
        {
            name="Monkey Stone",
            filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave
        };
        Color32[] pixels=new Color32[width*height];
        int[] widths={3,7,8,9,8,6,3};
        for(int y=0;y<height;y++)
        {
            int rowWidth=widths[y];
            int left=(width-rowWidth)/2;
            for(int x=left;x<left+rowWidth;x++)
            {
                byte shade=(byte)(x==left||x==left+rowWidth-1||y==0||y==height-1?55:
                    y<2?135:x<=4&&y>=4?255:218);
                pixels[y*width+x]=new Color32(shade,shade,shade,255);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        Sprite sprite=Sprite.Create(
            texture,new Rect(0f,0f,width,height),new Vector2(0.5f,0.5f),16f);
        sprite.name="Monkey Thrown Stone";
        sprite.hideFlags=HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateBeanSprite()
    {
        const int width=6,height=5;
        Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
        {
            name="Monkey Bean Pellet",
            filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave
        };
        Color32[] pixels=new Color32[width*height];
        int[] left={2,1,0,0,1};
        int[] rowWidth={2,4,6,5,3};
        for(int y=0;y<height;y++)
            for(int x=left[y];x<left[y]+rowWidth[y];x++)
                pixels[y*width+x]=new Color32(255,255,255,
                    (byte)((x==left[y]||x==left[y]+rowWidth[y]-1)?210:255));
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        Sprite sprite=Sprite.Create(
            texture,new Rect(0f,0f,width,height),new Vector2(0.5f,0.5f),16f);
        sprite.name="Monkey Bean Pellet";
        sprite.hideFlags=HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateMonkeyFruitSprite(int cracks=0)
    {
        // Draw at the same 16 pixels/unit as the small projectiles; size comes from more pixels.
        string[] rows={
            "....................","..........DD........",".........DGGL.......",
            "......DDDDGLL.......","....DDMMMMMDDD......","...DMMHHHMMMMMD.....",
            "..DMMHLLHHMMMMMD....","..DMHLLLHHMMMMMMD...",".DMMHLLHHMMMMMMMD...",
            ".DMMHHHMMMMMMMMMD...",".DMMMMMMMMMMMMMMMD..",".DMMMMMMMMMMMMMMMD..",
            ".DMMMMMMMMMMMMMSMD..","..DMMMMMMMMMMSSMD...","..DMMMMMMMMMSSSMD...",
            "...DMMMMMMSSSSMD....","....DMMSSSSSSMD.....",".....DDSSSSDDD......",
            ".......DDDD.........","...................."};
        const int size=20;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            char c=rows[y][x];
            pixels[(size-1-y)*size+x]=c=='D'?new Color32(66,43,34,255):
                c=='S'?new Color32(151,105,65,255):c=='M'?new Color32(243,207,146,255):
                c=='H'?new Color32(255,235,187,255):c=='L'?new Color32(255,254,228,255):
                c=='G'?new Color32(106,159,64,255):new Color32(0,0,0,0);
        }
        if(cracks>0)
        {
            for(int y=4;y<=14;y++)
            {
                int x=9+(y/3%2==0?0:2);
                pixels[y*size+x]=new Color32(63,35,30,255);
                if(cracks>1&&y<11)
                    for(int branch=0;branch<4;branch++)
                        pixels[(y+branch/2)*size+x-branch]=new Color32(63,35,30,255);
            }
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {name="Monkey Heavy Fruit "+cracks,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,
             hideFlags=HideFlags.HideAndDontSave};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        Sprite sprite=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,16f);
        sprite.name=texture.name;sprite.hideFlags=HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateWebBundleSprite()
    {
        const int size=11,center=5;
        Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
        {
            name="Spider Web Bundle",
            filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave
        };
        Color32[] pixels=new Color32[size*size];
        for(int y=0;y<size;y++)
        for(int x=0;x<size;x++)
        {
            int dx=x-center,dy=y-center;
            float distance=Mathf.Sqrt(dx*dx+dy*dy);
            bool ring=Mathf.Abs(distance-4f)<0.7f;
            bool spoke=(dx==0||dy==0||dx==dy||dx==-dy)&&distance<=4.5f;
            if(ring||spoke)pixels[y*size+x]=new Color32(235,255,255,235);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        Sprite sprite=Sprite.Create(
            texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),16f);
        sprite.name="Spider Web Bundle";
        sprite.hideFlags=HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateScytheWindSprite()
    {
        const int width=32,height=48;
        Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
        {
            name="Mantis Scythe Wind",
            filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave
        };
        Color32[] pixels=new Color32[width*height];
        for(int y=1;y<height-1;y++)
        {
            float v=(y-23.5f)/23f,fullness=1f-v*v;
            int edge=Mathf.RoundToInt(6f+20f*fullness);
            // Keep the cutting edge and reach fixed; fill the blade behind it.
            int thickness=1+Mathf.RoundToInt(Mathf.Pow(fullness,.8f)*12f);
            int back=edge-thickness+(y%11<2&&fullness>.3f?2:0);
            for(int x=Mathf.Max(0,back);x<=edge;x++)
            {
                int depth=edge-x;
                Color32 ink=depth<=1?new Color32(255,255,250,255):
                    depth<=Mathf.Max(2,thickness/3)?new Color32(232,255,215,255):
                    x>=back+3?new Color32(156,214,128,245):
                    new Color32(84,152,92,180);
                pixels[y*width+x]=ink;
            }
            // Short broken wedges give the filled blade a torn, pixel wind wake.
            int streak=y%12;
            if(streak<3&&fullness>.25f)
                for(int x=Mathf.Max(0,back-6+streak);x<back-2;x++)
                    pixels[y*width+x]=new Color32(184,230,157,(byte)(190-streak*35));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        Sprite sprite=Sprite.Create(
            texture,new Rect(0f,0f,width,height),new Vector2(26f/width,0.5f),24f);
        sprite.name=texture.name;
        sprite.hideFlags=HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite[] CreateWingTornadoFrames()
    {
        const int width=19,height=23,center=9,frameCount=6;
        Sprite[] frames=new Sprite[frameCount];
        for(int frame=0;frame<frameCount;frame++)
        {
            Texture2D texture=new Texture2D(
                width,height,TextureFormat.RGBA32,false)
            {
                name="Wyvern Wing Tornado "+frame,
                filterMode=FilterMode.Point,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[width*height];
            float framePhase=frame*Mathf.PI*2f/frameCount;
            for(int y=1;y<height-1;y++)
            {
                float radius=1.2f+y*0.31f;
                float phase=framePhase+y*0.48f;
                int left=Mathf.Clamp(
                    Mathf.RoundToInt(center+Mathf.Cos(phase)*radius),0,width-1);
                int right=Mathf.Clamp(
                    Mathf.RoundToInt(center-Mathf.Cos(phase)*radius),0,width-1);
                pixels[y*width+left]=new Color32(235,255,255,255);
                pixels[y*width+right]=new Color32(120,228,255,225);
                if((y+frame)%4!=0)continue;
                int start=Mathf.Min(left,right),end=Mathf.Max(left,right);
                for(int x=start+1;x<end;x++)
                    if(pixels[y*width+x].a==0)
                        pixels[y*width+x]=new Color32(100,205,255,95);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            frames[frame]=Sprite.Create(
                texture,new Rect(0f,0f,width,height),new Vector2(0.5f,0.45f),20f);
            frames[frame].name=texture.name;
            frames[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return frames;
    }

    private static Sprite CreateSpiritLightSprite()
    {
        const int size=9,center=4;
        Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
        {
            name="Spirit Deer Light",
            filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave
        };
        Color32[] pixels=new Color32[size*size];
        int[] widths={1,1,3,5,7,5,3,1,1};
        for(int y=0;y<size;y++)
        {
            int width=widths[y];
            int left=(size-width)/2;
            for(int x=left;x<left+width;x++)
                pixels[y*size+x]=new Color32(255,255,255,
                    (byte)((x==center||y==center)?255:190));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        Sprite sprite=Sprite.Create(
            texture,new Rect(0f,0f,size,size),new Vector2(0.5f,0.5f),16f);
        sprite.name=texture.name;
        sprite.hideFlags=HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Material ImpactMaterial
    {
        get
        {
            if(sharedImpactMaterial!=null)return sharedImpactMaterial;
            Shader shader=Shader.Find("Sprites/Default");
            sharedImpactMaterial=new Material(shader)
            {
                name="Pixel Star Hit Material",
                hideFlags=HideFlags.HideAndDontSave,
                mainTexture=SharedSprite.texture
            };
            return sharedImpactMaterial;
        }
    }

    private static Material StoneImpactMaterial
    {
        get
        {
            if(sharedStoneImpactMaterial!=null)return sharedStoneImpactMaterial;
            Shader shader=Shader.Find("Sprites/Default");
            sharedStoneImpactMaterial=new Material(shader)
            {
                name="Pixel Stone Hit Material",
                hideFlags=HideFlags.HideAndDontSave,
                mainTexture=StoneSprite.texture
            };
            return sharedStoneImpactMaterial;
        }
    }

    private static Material BeanImpactMaterial
    {
        get
        {
            if(sharedBeanImpactMaterial!=null)return sharedBeanImpactMaterial;
            Shader shader=Shader.Find("Sprites/Default");
            sharedBeanImpactMaterial=new Material(shader)
            {
                name="Pixel Bean Hit Material",
                hideFlags=HideFlags.HideAndDontSave,
                mainTexture=BeanSprite.texture
            };
            return sharedBeanImpactMaterial;
        }
    }
}
