using System;
using UnityEngine;

// Four drawn contact poses, eight drawn dust poses and physical fragments share the collision clock.
public sealed class PixelMammothChainImpact:MonoBehaviour
{
    public const string ImpactResourcePath="CombatCinematics/MammothChainV1/ChainImpact4";
    public const string DustResourcePath="CombatCinematics/MammothChainV1/ChainDust8";
    public const float Duration=.72f,ImpactFrameSeconds=.045f;
    private static Sprite[] impactFrames,dustFrames;
    private static Material dustMaterial;
    private readonly ParticleSystem.Particle[] fragments=new ParticleSystem.Particle[40];
    private readonly ParticleSystem.Particle[] puffs=new ParticleSystem.Particle[16];
    private ParticleSystem chips,dust;
    private SpriteRenderer impact;
    private CombatSkillClock skillClock;
    private Camera presentationCamera;
    private Vector3 point;
    private Color tint;
    private bool wall,strong,manual;
    public float Elapsed{get;private set;}
    public float RemainingLifetime=>Mathf.Max(0f,Duration-Elapsed);
    public static Sprite[] ImpactFrames=>Frames(ref impactFrames,ImpactResourcePath,4,1);
    public static Sprite[] DustFrames=>Frames(ref dustFrames,DustResourcePath,4,2);
    public static Material DustMaterial
    {
        get
        {
            if(dustMaterial==null)dustMaterial=new Material(Shader.Find("Sprites/Default"))
                {name="Mammoth eight-pose collision dust",mainTexture=DustFrames[0].texture,hideFlags=HideFlags.HideAndDontSave};
            return dustMaterial;
        }
    }
    private static Sprite[] Frames(ref Sprite[] frames,string path,int columns,int rows)
    {
        if(frames!=null&&frames[0]!=null)return frames;var texture=Resources.Load<Texture2D>(path);
        if(texture==null)throw new InvalidOperationException("Missing Mammoth chain-collision animation: "+path);
        frames=new Sprite[columns*rows];float width=texture.width/(float)columns,height=texture.height/(float)rows;
        for(int i=0;i<frames.Length;i++)
        {
            frames[i]=Sprite.Create(texture,new Rect(i%columns*width,(rows-1-i/columns)*height,width,height),Vector2.one*.5f,width,0,SpriteMeshType.FullRect);
            frames[i].name=path+"_"+i;frames[i].hideFlags=HideFlags.HideAndDontSave;
        }
        return frames;
    }
    public static void AnimateDust(ParticleSystem system)
    {
        var sheet=system.textureSheetAnimation;sheet.enabled=true;sheet.mode=ParticleSystemAnimationMode.Sprites;
        foreach(var sprite in DustFrames)sheet.AddSprite(sprite);
        sheet.timeMode=ParticleSystemAnimationTimeMode.Lifetime;sheet.cycleCount=1;
        sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,0f,1f,.999f));
        sheet.startFrame=0f;
    }
    public static PixelMammothChainImpact Play(Vector3 point,SpriteRenderer reference,Color tint,bool wall,bool strong)
    {
        var effect=new GameObject(wall?"Mammoth wall ricochet contact":"Mammoth body collision contact").AddComponent<PixelMammothChainImpact>();
        effect.point=point;effect.tint=tint;effect.wall=wall;effect.strong=strong;effect.skillClock=CombatSkillClock.Capture();
        effect.gameObject.layer=reference==null?0:reference.gameObject.layer;
        effect.impact=new GameObject("Four drawn heavy-contact poses").AddComponent<SpriteRenderer>();
        effect.impact.transform.SetParent(effect.transform,false);effect.impact.gameObject.layer=effect.gameObject.layer;
        effect.impact.sortingLayerID=reference==null?0:reference.sortingLayerID;effect.impact.sortingOrder=(reference==null?0:reference.sortingOrder)+7;
        effect.chips=PixelMushroomGas.CreateParticles("Physical stone and tusk-contact fragments",effect.transform,40,PixelMushroomGas.Material(true),reference,8,true);
        effect.dust=PixelMushroomGas.CreateParticles("Eight-frame collision dust bank",effect.transform,16,DustMaterial,reference,1,true);
        effect.chips.gameObject.layer=effect.dust.gameObject.layer=effect.gameObject.layer;AnimateDust(effect.dust);effect.Pose(0f);return effect;
    }
    private static float Seed(int value)=>Mathf.Repeat(Mathf.Sin(value*127.1f+31.7f)*43758.5453f,1f);
    public void SetPresentation(Camera camera){presentationCamera=camera;manual=true;}
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
            Advance(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
    }
    private void Advance(float deltaTime)
    {
        if(manual||deltaTime<=0f||CombatEffectEndPlayback.IsPreserving(this))return;
        Pose(Elapsed+deltaTime);if(Elapsed>=Duration)Cancel();
    }
    public void Pose(float absoluteAge,Camera camera=null)
    {
        if(this==null)return;Elapsed=Mathf.Max(0f,absoluteAge);
        if(camera!=null)SetPresentation(camera);camera=presentationCamera==null?Camera.main:presentationCamera;
        Quaternion view=camera==null?Quaternion.identity:camera.transform.rotation;
        int frame=Mathf.FloorToInt(Elapsed/ImpactFrameSeconds);impact.enabled=frame<4;
        if(frame<4)
        {
            impact.sprite=ImpactFrames[frame];impact.transform.SetPositionAndRotation(point+Vector3.up*.24f,view);
            impact.transform.localScale=Vector3.one*(strong?2.5f:1.65f);
            impact.color=CombatFlashPresentation.Flash(Color.Lerp(Color.white,tint,.09f));
        }
        if(chips.isStopped)chips.Play(false);if(dust.isStopped)dust.Play(false);
        int count=0,dustCount=0;float weight=strong?1f:.65f;
        for(int i=0;i<fragments.Length;i++)
        {
            float seed=Seed(i+19),life=.24f+seed*.26f;if(Elapsed>=life)continue;
            float side=Seed(i+41)*2f-1f;Vector3 velocity=view*new Vector3(side*(wall?4.7f:3.8f),.4f+Seed(i+71)*4.6f,0f)*weight;
            Vector3 at=point+Vector3.up*.20f+velocity*Elapsed-Vector3.up*(Elapsed*Elapsed*4.8f);at.y=Mathf.Max(point.y+.04f,at.y);
            Color color=i%4==0?Color.Lerp(new Color(1f,.94f,.72f),tint,.10f):new Color(.49f+seed*.20f,.39f+seed*.17f,.24f+seed*.13f);
            color.a=(1f-Elapsed/life)*(i%4==0?.9f:.8f);if(i%4==0)color=CombatFlashPresentation.Flash(color);
            fragments[count++]=Particle(at,Vector3.one*(.055f+seed*.10f)*weight,color,life,Elapsed,i,seed*360f+Elapsed*180f);
        }
        for(int i=0;i<puffs.Length;i++)
        {
            float seed=Seed(i+103),life=.52f+seed*.18f;if(Elapsed>=life)continue;float t=Elapsed/life;
            Vector3 radial=new Vector3(Seed(i+131)*2f-1f,0f,Seed(i+157)*2f-1f);
            float spread=(1f-Mathf.Exp(-Elapsed*8f))*(.35f+seed*.65f)*weight;
            Vector3 at=point+radial*spread+Vector3.up*(.08f+t*.22f);
            Color color=MeadowAmbientParticles.TerrainColor(Color.Lerp(new Color(.86f,.76f,.57f),new Color(.52f,.46f,.34f),seed));
            color.a=(1f-Mathf.SmoothStep(0f,1f,t))*.67f;
            float size=(.48f+seed*.42f)*Mathf.Lerp(.7f,1.5f,t)*weight;
            puffs[dustCount++]=Particle(at,new Vector3(size,size*.68f,1f),color,life,Elapsed,i+100,(seed-.5f)*26f);
        }
        chips.SetParticles(fragments,count);dust.SetParticles(puffs,dustCount);
    }
    private static ParticleSystem.Particle Particle(Vector3 at,Vector3 size,Color color,float life,float age,int seed,float rotation)=>
        new ParticleSystem.Particle{position=at,startSize3D=size,startColor=color,startLifetime=life,
            remainingLifetime=Mathf.Max(.0001f,life-age),randomSeed=(uint)(seed+1),rotation=rotation};
    public void Cancel()
    {
        if(this==null||CombatEffectEndFade.IsFading(this))return;
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
