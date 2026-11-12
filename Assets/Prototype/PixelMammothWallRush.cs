using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Authored whole-wall flipbooks: rise, contact, fracture and dispersed fragments.
public sealed class PixelMammothWallRush:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private struct Mote {public Vector3 at,velocity;public Color color;public float birth,life,size,spreadTime;public bool dust;}
    private const int ParticleCapacity=512;
    private static readonly string[] sheets={"RockWall16","IceWall16","IronWall16","RootWall16"};
    private static readonly Sprite[][] frames=new Sprite[4][];
    // Slice the clear atlas row gutters; every row shares its own ground baseline.
    private static readonly int[][] rowEdges={new[]{0,360,650,1010,1254},new[]{0,350,685,1040,1254},
        new[]{0,400,700,1080,1254},new[]{0,344,650,1020,1254}};
    private static readonly int[][] baselines={new[]{315,599,900,1194},new[]{306,620,932,1219},
        new[]{359,660,967,1229},new[]{304,600,905,1195}};
    private readonly SpriteRenderer[] walls=new SpriteRenderer[3];
    private readonly Vector3[] centers=new Vector3[3];
    private readonly float[] broken={-1,-1,-1};
    private readonly List<Mote> motes=new List<Mote>();
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[ParticleCapacity];
    private readonly ParticleSystem.Particle[] dustParticles=new ParticleSystem.Particle[ParticleCapacity];
    private ParticleSystem chips,dust;private float clock,radius;private int material;
    private static int MaterialId(string species)=>species=="seolgason"?1:species=="irontusk"?2:species=="gaiason"?3:0;
    public static Color HitColor(string species)=>Tint(MaterialId(species),true);
    private static Color Tint(int kind,bool bright)=>kind==1?(bright?new Color(.78f,.97f,1):new Color(.24f,.63f,.79f)):
        kind==2?(bright?new Color(1,.68f,.25f):new Color(.34f,.41f,.48f)):
        kind==3?(bright?new Color(.82f,.95f,.49f):new Color(.32f,.48f,.26f)):
        bright?new Color(.87f,.75f,.54f):new Color(.48f,.34f,.22f);

    public static int FrameAt(float time,int wall,float brokenAt)
    {
        if(brokenAt>=0){float age=time-brokenAt;return age>=.36f?-1:Mathf.Min(15,5+Mathf.FloorToInt(age/.033f));}
        float rise=time-(.06f+wall*.1f);return rise<0?-1:Mathf.Min(4,Mathf.FloorToInt(rise/.05f));
    }
    public static PixelMammothWallRush Play(SpriteRenderer reference,string species,Vector3 start,Vector3 direction,float radiusScale)
    {
        int kind=MaterialId(species);var texture=Resources.Load<Texture2D>("CombatEffects/Mammoth/"+sheets[kind]);
        if(texture==null)return null;
        if(frames[kind]==null||frames[kind][0]==null)
        {
            frames[kind]=new Sprite[16];float width=texture.width/4f,scale=texture.height/1254f;
            for(int i=0;i<16;i++)
            {
                int row=i/4;float top=rowEdges[kind][row]*scale,bottom=rowEdges[kind][row+1]*scale;
                float baseline=baselines[kind][row]*scale;
                var frame=Sprite.Create(texture,new Rect(i%4*width,texture.height-bottom,width,bottom-top),
                    new Vector2(.5f,(bottom-baseline)/(bottom-top)),width,0,SpriteMeshType.FullRect);
                frame.name=sheets[kind]+"_"+i.ToString("D2");frame.hideFlags=HideFlags.HideAndDontSave;frames[kind][i]=frame;
            }
        }
        var fx=new GameObject("Pixel Mammoth Wall Rush").AddComponent<PixelMammothWallRush>();
        fx.material=kind;fx.radius=MonsterAutoBattlePrototype.TileSize*MammothRank3Rules.WallRushBlastTiles*radiusScale;
        fx.chips=PixelMushroomGas.CreateParticles("Wall Radial Debris",fx.transform,ParticleCapacity,PixelMushroomGas.Material(true),reference,1,true);
        fx.dust=PixelMushroomGas.CreateParticles("Wall Blast Dust",fx.transform,ParticleCapacity,PixelMushroomGas.Material(false),reference,0,true);
        for(int i=0;i<3;i++)
        {
            var root=new GameObject("Authored Wall "+(i+1));root.transform.SetParent(fx.transform,false);
            fx.centers[i]=start+direction*MonsterAutoBattlePrototype.TileSize*(i+1);root.transform.position=fx.centers[i];
            var group=root.AddComponent<SortingGroup>();group.sortingLayerID=reference==null?0:reference.sortingLayerID;
            group.sortingOrder=reference==null?10:reference.sortingOrder;
            var art=new GameObject("Animated "+sheets[kind]);art.transform.SetParent(root.transform,false);
            fx.walls[i]=art.AddComponent<SpriteRenderer>();fx.walls[i].spriteSortPoint=SpriteSortPoint.Pivot;
            fx.walls[i].flipX=Camera.main!=null&&Vector3.Dot(direction,Camera.main.transform.right)<0;
        }
        fx.Pose(0);return fx;
    }
    public void Pose(float time){if(this==null)return;clock=time;Render();}
    public void Break(int wall,float time)
    {
        if(this==null||broken[wall]>=0)return;broken[wall]=time;clock=time;
        for(int i=0;i<144;i++)
        {
            bool puff=i>=96;int n=puff?i-96:i;
            float a=n*2.399963f+wall*.7f+(puff?.9f:0),seed=Mathf.Repeat(n*.754878f+.17f,1f);
            Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            float size=puff?.34f+seed*.26f:n%4==0?.18f+seed*.08f:.07f+seed*.07f;
            // Some debris marks the full damage radius; the rest fills the blast interior.
            float distance=(radius-size*.5f)*(!puff&&n%3==0?1f:Mathf.Sqrt(seed)*.92f);
            Color color=Color.Lerp(Tint(material,false),Tint(material,true),puff?.2f+seed*.3f:seed);
            color.a=puff?.58f:.95f;
            Add(centers[wall]+Vector3.up*.08f,radial*distance+Vector3.up*(puff?.1f+seed*.12f:.12f+seed*.42f),
                time,puff?.38f+seed*.04f:.3f+seed*.1f,size,color,.16f+seed*.04f,puff);
        }
        Render();
    }
    public void Hit(Vector3 center,float time)
    {
        if(this==null)return;Quaternion view=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        for(int i=0;i<12;i++){float a=i*2.4f;Add(center,view*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*3.5f,
            time,.16f,.07f+i%3*.018f,Tint(material,true));}Render();
    }
    private void Add(Vector3 at,Vector3 velocity,float birth,float life,float size,Color color,float spreadTime=0,bool dust=false)
    {
        if(motes.Count>=ParticleCapacity)motes.RemoveAt(0);
        motes.Add(new Mote{at=at,velocity=velocity,birth=birth,life=life,size=size,color=color,spreadTime=spreadTime,dust=dust});
    }
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Render();
        }
    }
    private void Render()
    {
        if(chips==null)return;Quaternion view=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        for(int i=0;i<3;i++)
        {
            int frame=FrameAt(clock,i,broken[i]);var wall=walls[i];wall.enabled=frame>=0;if(frame<0)continue;
            wall.sprite=frames[material][frame];wall.transform.SetPositionAndRotation(centers[i],view);
            wall.transform.localScale=new Vector3(1.65f,2.4f,1);wall.color=new Color(1,1,1,material==1?.84f:1f);
        }
        int count=0,dustCount=0;
        for(int i=motes.Count-1;i>=0;i--)
        {
            Mote mote=motes[i];float age=clock-mote.birth,t=age/mote.life;
            if(t>=1){motes.RemoveAt(i);continue;}
            Color color=mote.color;Vector3 position;float size=mote.size;
            if(mote.spreadTime>0)
            {
                // Fast launch, then settle inside the radius instead of flying beyond it while fading.
                float spread=1-Mathf.Pow(1-Mathf.Clamp01(age/mote.spreadTime),3);
                position=mote.at+new Vector3(mote.velocity.x,0,mote.velocity.z)*spread
                    +Vector3.up*mote.velocity.y*4*t*(1-t);
                color.a*=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-.12f)/(mote.life-.12f)));
                if(mote.dust)size*=Mathf.Lerp(.35f,1,spread);
            }
            else
            {
                position=mote.at+mote.velocity*age-Vector3.up*5f*age*age;color.a*=1-t*t;
            }
            var particle=new ParticleSystem.Particle{position=position,
                startColor=color,startSize3D=Vector3.one*size,startLifetime=10,remainingLifetime=10,rotation=i*31,randomSeed=(uint)(i+1)};
            if(mote.dust)dustParticles[dustCount++]=particle;else particles[count++]=particle;
        }
        chips.SetParticles(particles,count);dust.SetParticles(dustParticles,dustCount);
    }
    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
}

