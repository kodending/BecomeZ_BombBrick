using System.Collections.Generic;
using UnityEngine;

// The storm owns its locked strike points; the caster can move, be stunned or die.
public sealed class PixelMonkeyStorm:MonoBehaviour
{
    public const string CloudResourcePath="CombatCinematics/Rank4V3/wyvern/CloudBandAtlas";
    public const string ZapResourcePath="CombatEffects/Monkey/StormZap";
    public const int ConcurrentStrikeLimit=2;
    private const int CloudCount=14,ParticleLimit=144;
    private sealed class Strike
    {
        public int index;
        public Vector3 point;
        public float impactAt;
        public bool cancelled;
    }
    private sealed class Bolt
    {
        public PixelHaetaeThunder visual;
        public Strike strike;
    }
    private static Sprite[] cloudFrames;
    private static GameObject stormZap;
    private readonly List<Strike> strikes=new List<Strike>(MonkeyStormRules.StrikeCount);
    private readonly Bolt[] bolts=new Bolt[ConcurrentStrikeLimit];
    private readonly SpriteRenderer[] clouds=new SpriteRenderer[CloudCount];
    private readonly ParticleSystem.Particle[] pixels=new ParticleSystem.Particle[ParticleLimit];
    private ParticleSystem sparks;
    private Bounds arena;
    private Camera view;
    private float stoppedAt=-1f,endAt=MonkeyStormRules.Duration+MonkeyStormRules.Tail;
    public float Elapsed {get;private set;}
    public float RemainingLifetime=>Mathf.Max(0f,endAt-Elapsed);
    public Color ScreenFlashColor
    {
        get
        {
            float flash=0f;
            foreach(var strike in strikes)
            {
                float age=Elapsed-strike.impactAt;
                if(strike.cancelled||age<0f||age>=.14f)continue;
                flash=Mathf.Max(flash,1f-Mathf.SmoothStep(0f,1f,age/.14f));
            }
            return CombatFlashPresentation.Flash(new Color(.84f,.94f,1f,flash*.62f));
        }
    }

    public static Sprite[] CloudFrames()
    {
        if(cloudFrames!=null&&cloudFrames[0]!=null)return cloudFrames;
        var texture=Resources.Load<Texture2D>(CloudResourcePath);
        if(texture==null)return null;
        cloudFrames=new Sprite[4];float cell=texture.width/4f;
        for(int i=0;i<cloudFrames.Length;i++)
        {
            cloudFrames[i]=Sprite.Create(texture,new Rect(i*cell,0f,cell,texture.height),Vector2.one*.5f,64f,0,SpriteMeshType.FullRect);
            cloudFrames[i].name="Authored storm cloud frame "+i;
            cloudFrames[i].hideFlags=HideFlags.HideAndDontSave;
        }
        return cloudFrames;
    }

    public static PixelHaetaeThunder CreateAnimatedBolt(Vector3 point,SpriteRenderer reference,int layer=0)
    {
        if(stormZap==null)stormZap=Resources.Load<GameObject>(ZapResourcePath);
        var visual=PixelHaetaeThunder.Create(point,0,reference,zapPrefab:stormZap);visual.enabled=false;
        visual.transform.position=point;
        visual.visualScale=new Vector3(2.7f,1.6f,2.7f);
        visual.Pose(HaetaeRank3Rules.HeavenlyDelay+.001f);
        foreach(var child in visual.GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;
        foreach(var particles in visual.GetComponentsInChildren<ParticleSystem>(true))
        {
            var sheet=particles.textureSheetAnimation;
            if(!sheet.enabled||sheet.numTilesX*sheet.numTilesY<4)continue;
            // Play the asset's four authored tiles instead of holding one random tile.
            sheet.timeMode=ParticleSystemAnimationTimeMode.Lifetime;
            sheet.startFrame=new ParticleSystem.MinMaxCurve(0f);
            sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,0f,1f,.9999f));
            sheet.cycleCount=1;
            if(particles.name=="Zap Add Floor")
            {
                var main=particles.main;main.startLifetime=.24f;main.startSize3D=false;main.startSize=1.4f;
                main.startSpeed=0f;main.startRotation3D=false;main.startRotation=0f;
                var shape=particles.shape;shape.enabled=false;
                var size=particles.sizeOverLifetime;size.enabled=false;
                var renderer=particles.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode=ParticleSystemRenderMode.HorizontalBillboard;renderer.pivot=Vector3.zero;
            }
        }
        visual.Pose(HaetaeRank3Rules.HeavenlyDelay+.001f);
        return visual;
    }

    public static void PoseBolt(PixelHaetaeThunder visual,float age,Camera camera)
    {
        if(visual==null)return;
        visual.Pose(HaetaeRank3Rules.HeavenlyDelay+Mathf.Max(.001f,age));
    }

    public static PixelMonkeyStorm Play(Bounds arena,SpriteRenderer reference,Camera camera)
    {
        var root=new GameObject("Monkey Heaven Thunderstorm");root.AddComponent<CombatEffectEndMarker>();
        var storm=root.AddComponent<PixelMonkeyStorm>();storm.arena=arena;storm.view=camera;
        Sprite[] animation=CloudFrames();
        for(int i=0;i<CloudCount;i++)
        {
            var cloud=new GameObject("Animated storm canopy "+i).AddComponent<SpriteRenderer>();
            cloud.transform.SetParent(root.transform,false);cloud.sortingLayerID=reference==null?0:reference.sortingLayerID;
            cloud.sortingOrder=(reference==null?0:reference.sortingOrder)+8+i%2;
            cloud.flipX=i%2!=0;cloud.sprite=animation==null?null:animation[i%animation.Length];storm.clouds[i]=cloud;
        }
        storm.sparks=PixelMushroomGas.CreateParticles("Storm warning and impact pixels",root.transform,ParticleLimit,
            PixelMushroomGas.Material(true),reference,5,true);
        // Reuse the imported four-frame Zap and floor flipbooks without instantiating at every hit.
        for(int i=0;i<storm.bolts.Length;i++)
        {
            var visual=CreateAnimatedBolt(Vector3.zero,reference);
            visual.name="Pooled four-frame storm bolt "+i;visual.transform.SetParent(root.transform,true);
            visual.gameObject.SetActive(false);storm.bolts[i]=new Bolt{visual=visual};
        }
        storm.Advance(0f);return storm;
    }

    public void QueueStrike(int index,Vector3 point,float impactAt)
    {
        if(stoppedAt>=0f||index<0||index>=MonkeyStormRules.StrikeCount)return;
        foreach(var strike in strikes)if(strike.index==index)return;
        point.y=arena.center.y+.045f;
        strikes.Add(new Strike{index=index,point=point,impactAt=impactAt});
    }

    public void Advance(float age)
    {
        if(this==null)return;
        Elapsed=Mathf.Max(Elapsed,Mathf.Max(0f,age));
        PoseClouds();
        foreach(var bolt in bolts)
        {
            if(bolt==null||bolt.strike==null)continue;
            if(!bolt.strike.cancelled&&Elapsed<bolt.strike.impactAt+MonkeyStormRules.Tail)continue;
            bolt.visual.gameObject.SetActive(false);bolt.strike=null;
        }
        foreach(var strike in strikes)
        {
            float local=Elapsed-strike.impactAt;
            if(strike.cancelled||local<0f||local>=MonkeyStormRules.Tail)continue;
            Bolt chosen=null;
            foreach(var bolt in bolts)if(bolt.strike==strike){chosen=bolt;break;}
            if(chosen==null)foreach(var bolt in bolts)if(bolt.strike==null){chosen=bolt;break;}
            if(chosen==null)continue;
            chosen.strike=strike;chosen.visual.transform.position=strike.point;
            chosen.visual.gameObject.SetActive(true);
            PoseBolt(chosen.visual,local,view);
        }
        PosePixels();
    }

    private void OnGUI()
    {
        if(Event.current.type!=EventType.Repaint)return;
        Color flash=ScreenFlashColor;if(flash.a<=0f)return;
        Color color=GUI.color;int depth=GUI.depth;Matrix4x4 matrix=GUI.matrix;
        GUI.depth=-10000;GUI.matrix=Matrix4x4.identity;GUI.color=flash;
        GUI.DrawTexture(new Rect(0f,0f,Screen.width,Screen.height),Texture2D.whiteTexture);
        GUI.color=color;GUI.depth=depth;GUI.matrix=matrix;
    }

    private void PoseClouds()
    {
        Sprite[] animation=CloudFrames();if(animation==null)return;
        float fade=stoppedAt>=0f?1f-Mathf.Clamp01((Elapsed-stoppedAt)/CombatEffectEndFade.Duration):
            Mathf.Clamp01((MonkeyStormRules.Duration+MonkeyStormRules.Tail-Elapsed)/MonkeyStormRules.Tail);
        float alpha=Mathf.SmoothStep(0f,1f,Elapsed/.32f)*fade;
        float depth=view==null?24f:Mathf.Max(2f,Vector3.Dot(arena.center-view.transform.position,view.transform.forward));
        float screenHeight=view==null?arena.size.x*.85f:view.orthographic?view.orthographicSize*2f:
            2f*depth*Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad);
        for(int i=0;i<clouds.Length;i++)
        {
            int row=i/7;float height=screenHeight*(row==0?.29f:.26f);
            var cloud=clouds[i];cloud.enabled=alpha>0f;
            cloud.sprite=animation[(Mathf.FloorToInt(Elapsed/.18f)+i)%animation.Length];
            Bounds visible=PixelMantisReaper.VisibleBody(cloud.sprite);
            float scale=height/Mathf.Max(.01f,visible.size.y);cloud.transform.localScale=Vector3.one*scale;
            cloud.transform.rotation=view==null?Quaternion.Euler(58f,0f,0f):view.transform.rotation;
            float x=-.075f+i%7*.18f+row*.055f+Mathf.Sin(Elapsed*.35f+i*1.7f)*.012f;
            float y=(row==0?1.035f:.922f)+Mathf.Sin(i*1.9f)*.006f;
            Vector3 center=view==null?arena.center+new Vector3((x-.5f)*arena.size.x,8f,arena.extents.z):
                view.ViewportToWorldPoint(new Vector3(x,y,depth));
            Vector3 offset=visible.center;if(cloud.flipX)offset.x=-offset.x;
            cloud.transform.position=center-cloud.transform.rotation*(offset*scale);
            float glow=0f;
            foreach(var strike in strikes)
            {
                float local=Elapsed-strike.impactAt;if(strike.cancelled||local<0f||local>.065f)continue;
                float strikeX=view==null?Mathf.InverseLerp(arena.min.x,arena.max.x,strike.point.x):view.WorldToViewportPoint(strike.point).x;
                glow=Mathf.Max(glow,(1f-local/.065f)*Mathf.Clamp01(1f-Mathf.Abs(x-strikeX)/.18f));
            }
            cloud.color=Color.Lerp(new Color(.45f,.56f,.72f,alpha*.95f),new Color(.70f,.85f,1f,alpha),glow*.42f);
        }
    }

    private void PosePixels()
    {
        int count=0;
        foreach(var strike in strikes)
        {
            if(strike.cancelled)continue;
            float local=Elapsed-strike.impactAt;
            bool warning=local>=-MonkeyStormRules.Warning&&local<0f;
            if(!warning&&(local<0f||local>.36f))continue;
            int total=warning?12:30;
            for(int i=0;i<total&&count<pixels.Length;i++)
            {
                float seed=Mathf.Repeat(i*.618034f+strike.index*.137f,1f),angle=i*2.399963f+strike.index;
                float p=warning?1f+local/MonkeyStormRules.Warning:local/.36f;
                float radius=warning?.72f*(1f-p*.25f):local*(2f+seed*3f);
                Vector3 point=strike.point+new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*radius;
                point.y+=warning?.025f+p*.07f:Mathf.Max(0f,local*(2f+seed*2f)-local*local*5f);
                Color color=warning?new Color(.63f,.83f,1f,.32f+p*.52f):new Color(.86f,.94f,1f,(1f-p)*.9f);
                pixels[count++]=new ParticleSystem.Particle{position=point,startSize=warning?.045f+p*.025f:.14f+seed*.10f,
                    startColor=color,startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(i+1)};
            }
        }
        sparks.SetParticles(pixels,count);
    }

    public void StopEmission()
    {
        if(this==null||stoppedAt>=0f)return;
        stoppedAt=Elapsed;endAt=Elapsed+CombatEffectEndFade.Duration;
        foreach(var strike in strikes)
        {
            if(strike.impactAt>Elapsed)strike.cancelled=true;
            else endAt=Mathf.Max(endAt,strike.impactAt+MonkeyStormRules.Tail);
        }
    }

    public void Cancel()
    {
        if(this==null||CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
