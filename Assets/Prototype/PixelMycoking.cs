using System;
using UnityEngine;

[DefaultExecutionOrder(10030)]
public sealed class PixelMycoking:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite[] frames;
    private static Sprite[] deathBloomFrames;
    private Sprite[] artwork;
    private bool deathBloom;
    private SpriteRenderer body,reference;
    private ParticleSystem breath,flakes;
    private readonly ParticleSystem.Particle[] mist=new ParticleSystem.Particle[32],pieces=new ParticleSystem.Particle[28];
    private Vector3 ground;
    private float height,age,depart=-1f,hitAge=-10f;
    private Color tint;
    private Func<bool> paused;
    public SpriteRenderer BodyRenderer=>body;
    public static Sprite[] FlowerFrames { get { if(frames==null||frames[0]==null)LoadFrames();return frames; } }
    public static Sprite SporeBudSprite { get { if(frames==null||frames[0]==null)LoadFrames();return frames[0]; } }
    public const string DeathBloomResourcePath="CombatCinematics/Rank4V3/mushroom/SporeBloom12";
    public static Sprite[] DeathBloomFrames { get { if(deathBloomFrames==null||deathBloomFrames[0]==null)LoadDeathBloomFrames();return deathBloomFrames; } }
    public ScreenSpaceOutlineTarget Outline { get; private set; }

    public static PixelMycoking Play(SpriteRenderer reference,Vector3 ground,Color tint,Func<bool> paused)
    {
        if(frames==null||frames[0]==null)LoadFrames();
        var fx=new GameObject("Pixel Rooted Mycoking").AddComponent<PixelMycoking>();
        fx.reference=reference;fx.ground=ground;fx.tint=tint;fx.paused=paused;fx.artwork=frames;
        fx.height=reference==null?2.6f:Mathf.Clamp(PixelMantisReaper.VisibleBody(reference.sprite).size.y*
            Mathf.Abs(reference.transform.lossyScale.y)*1.65f,2.2f,3.4f);
        fx.body=fx.Part("Rooted Mycoking Animation",frames[0]);
        // Cache the body before adding particles, so its health bar excludes gas bounds.
        fx.Outline=fx.gameObject.AddComponent<ScreenSpaceOutlineTarget>();
        fx.Outline.RefreshRenderers();
        fx.breath=PixelMushroomGas.CreateParticles("Under Cap Exhalation",fx.transform,fx.mist.Length,
            PixelMushroomGas.Material(false),reference,3,true);
        fx.flakes=PixelMushroomGas.CreateParticles("Mycoking Fragments",fx.transform,fx.pieces.Length,
            PixelMushroomGas.Material(true),reference,4,true);
        fx.Pose(0f);return fx;
    }

    private SpriteRenderer Part(string name,Sprite sprite)
    {
        var child=new GameObject(name);child.transform.SetParent(transform,false);
        var renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
        renderer.sortingLayerID=reference==null?0:reference.sortingLayerID;
        renderer.sortingOrder=reference==null?10:reference.sortingOrder;
        renderer.spriteSortPoint=SpriteSortPoint.Pivot;return renderer;
    }

    public void Pose(float time){age=time;Render();}
    public void UseDeathBloomArtwork()
    {
        if(deathBloom)return;
        deathBloom=true;artwork=DeathBloomFrames;height*=1.3f;Render();
    }
    public void ScaleSize(float scale){height*=scale;Render();}
    public void Hit(){hitAge=age;}
    public void Depart(){if(this==null||CombatEffectEndPlayback.IsPreserving(this)||depart>=0f)return;depart=0f;Render();}
    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(depart>=0f&&!(paused?.Invoke()??false))
            {depart+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);if(depart>=.65f){Cancel();return;}}
            Render();
        }
    }

    private void Render()
    {
        Quaternion rotation=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        Vector3 right=rotation*Vector3.right,up=rotation*Vector3.up;
        float cycle=Mathf.Repeat(age,MushroomRank3Rules.MycokingBreathInterval);
        float rise=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/.42f));
        float flash=Mathf.Clamp01(1f-(age-hitAge)/.15f);
        body.sprite=artwork[deathBloom?DeathBloomFrameAt(age,depart):FrameAt(age,depart)];
        body.transform.SetPositionAndRotation(ground,rotation);
        body.transform.localScale=Vector3.one*height*1.18f;
        float alpha=depart<0f?1f:Mathf.Clamp01((.65f-depart)/.22f);
        body.color=new Color(1f,1f-flash*.04f,1f-flash*.14f,alpha);
        if(reference!=null){body.sortingLayerID=reference.sortingLayerID;body.sortingOrder=reference.sortingOrder;}
        float outT=Mathf.Clamp01(cycle/.5f),spread=1f-Mathf.Pow(1f-outT,3f);
        for(int i=0;i<mist.Length;i++)
        {
            float seed=Seed(i),a=i*2.399963f;
            Vector3 direction=new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a));
            Vector3 origin=ground+up*height*.35f;
            Vector3 end=ground+direction*height*(.45f+seed*.35f)*spread+Vector3.up*(.12f+seed*.18f);
            Color c=tint;c.a=depart<0f?(1f-outT)*.42f*rise:0f;
            mist[i]=Particle(Vector3.Lerp(origin,end,spread),height*(.12f+seed*.12f)*(1f+outT),c,i);
        }
        breath.SetParticles(mist,!deathBloom&&depart<0f&&cycle<.5f?mist.Length:0);
        float debrisAge=depart>=0f?depart:age;
        float t=Mathf.Clamp01(debrisAge/.65f);
        for(int i=0;i<pieces.Length;i++)
        {
            float seed=Seed(i+33),a=i*2.399963f;
            Vector3 origin=depart>=0f?ground+up*height*(.2f+seed*.55f):ground;
            Vector3 travel=right*Mathf.Cos(a)*height*(.3f+seed)*t+
                up*height*((.6f+seed)*t-1.8f*t*t);
            Color c=depart>=0f?Color.Lerp(tint,deathBloom?new Color(.91f,.80f,1f):new Color(.92f,.79f,.55f),seed):new Color(.52f,.39f,.23f);
            c.a=1f-t;pieces[i]=Particle(origin+travel,height*(.025f+seed*.035f),c,i);
        }
        flakes.SetParticles(pieces,depart>=0f||!deathBloom&&age<.65f?pieces.Length:0);
    }

    private static float Seed(int i)=>Mathf.Repeat(Mathf.Sin(i*12.9898f+3.31f)*43758.5453f,1f);
    private static ParticleSystem.Particle Particle(Vector3 position,float size,Color color,int i)=>
        new ParticleSystem.Particle{position=position,startSize3D=Vector3.one*size,startColor=color,
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(i+1)};

    public static int FrameAt(float time,float departure=-1f)
    {
        if(departure>=0f)return departure<.12f?12:departure<.27f?13:departure<.45f?14:15;
        if(time<.42f)return time<.10f?0:time<.22f?1:time<.34f?2:3;
        float cycle=Mathf.Repeat(time,MushroomRank3Rules.MycokingBreathInterval);
        return cycle<.12f?8:cycle<.25f?9:cycle<.40f?10:cycle<.55f?11:
            cycle<1.10f?4:cycle<1.40f?5:cycle<1.65f?6:7;
    }

    public static int DeathBloomFrameAt(float time,float departure=-1f)
    {
        if(departure>=0f)return departure<.12f?8:departure<.27f?9:departure<.45f?10:11;
        if(time<.42f)return time<.10f?0:time<.22f?1:time<.34f?2:3;
        return 4+Mathf.Min(3,Mathf.FloorToInt(Mathf.Repeat(time-.42f,2f)*2f));
    }

    private static void LoadDeathBloomFrames()
    {
        var texture=Resources.Load<Texture2D>(DeathBloomResourcePath);
        if(texture==null)throw new InvalidOperationException("Missing dedicated mushroom blossom animation");
        deathBloomFrames=new Sprite[12];float width=texture.width/4f,height=texture.height/3f;
        for(int row=0;row<3;row++)for(int column=0;column<4;column++)
        {
            int i=row*4+column;
            deathBloomFrames[i]=Sprite.Create(texture,new Rect(column*width,texture.height-(row+1)*height,width,height),
                new Vector2(.5f,.0625f),width,0,SpriteMeshType.FullRect);
            deathBloomFrames[i].name=DeathBloomResourcePath+" "+i.ToString("D2");
            deathBloomFrames[i].hideFlags=HideFlags.HideAndDontSave;
        }
    }

    private static void LoadFrames()
    {
        var texture=Resources.Load<Texture2D>("CombatEffects/Mycoking/RootedKing16");
        if(texture==null)throw new InvalidOperationException("Missing RootedKing16 sprite atlas");
        // Art bounds include full roots even where they cross an equal atlas-cell boundary.
        // Pixel coordinates are top-left based; all pivots register the root base to ground.
        var bounds=new Rect[] {
            new Rect(76,195,166,117),new Rect(389,152,169,160),
            new Rect(670,67,229,245),new Rect(949,32,286,280),
            new Rect(21,391,275,243),new Rect(345,391,273,243),
            new Rect(640,353,292,281),new Rect(949,331,301,303),
            new Rect(16,714,304,221),new Rect(350,742,269,193),
            new Rect(648,689,277,246),new Rect(957,693,278,242),
            new Rect(64,1007,216,211),new Rect(375,1044,232,174),
            new Rect(675,1080,226,138),new Rect(1004,1132,188,85)};
        float[] rootX={155f,471.5f,782.5f,1095.5f,164f,478.5f,786.5f,1098f,
            163.5f,474.5f,787.5f,1100f,165.5f,479.5f,787.5f,1098f};
        frames=new Sprite[bounds.Length];
        for(int i=0;i<frames.Length;i++)
        {
            Rect r=bounds[i];
            var rect=new Rect(r.x-3,texture.height-r.y-r.height-3,r.width+6,r.height+6);
            frames[i]=Sprite.Create(texture,rect,new Vector2((rootX[i]-rect.x)/rect.width,3f/rect.height),
                313.5f,0,SpriteMeshType.FullRect);
            frames[i].name="Rooted Mycoking "+i.ToString("D2");
            frames[i].hideFlags=HideFlags.HideAndDontSave;
        }
    }
}
