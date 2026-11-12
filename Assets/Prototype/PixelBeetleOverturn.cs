using System.Collections.Generic;
using BombBrick.Combat;
using UnityEngine;

// One authored horn stroke; the battle alone owns lifting, damage and landing.
public sealed class PixelBeetleOverturn:MonoBehaviour
{
    public const string ResourceRoot="CombatCinematics/BeetleImperialHornV1/";
    public const float Afterglow=.95f;
    private static Sprite[] bodies,horns,earthFrames,impactFrames;
    private static readonly Dictionary<string,Sprite[]> sourceFrames=new Dictionary<string,Sprite[]>();
    private SpriteRenderer reference,horn;
    private readonly SpriteRenderer[] earth=new SpriteRenderer[8];
    private readonly List<Landing> landings=new List<Landing>();
    private ParticleSystem fragments;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[768];
    private Bounds area;
    private Vector3 joint,direction;
    private Quaternion view;
    private float size,time;
    private bool ending;
    private sealed class Landing {public Vector3 point;public float birth;public SpriteRenderer dust;}

    public static Sprite[] SourceFrames(string species,bool shiny)
    {
        string key=shiny&&species=="jangpung"?"shiny_jangpung":species??"jangpung";
        Sprite[] frames;
        if(sourceFrames.TryGetValue(key,out frames)&&frames.Length>0&&frames[0]!=null)return frames;
        if(bodies==null||bodies.Length==0||bodies[0]==null)bodies=Load("Body40",4,10,Vector2.one*.5f);
        if(bodies.Length!=40)return System.Array.Empty<Sprite>();
        int row=key=="noepung"?1:key=="atlagon"?2:key=="solarbeetle"?3:key=="shiny_jangpung"?4:0;
        frames=new Sprite[8];System.Array.Copy(bodies,row*8,frames,0,8);sourceFrames[key]=frames;return frames;
    }

    private static Sprite[] Load(string name,int columns,int rows,Vector2 pivot)
    {
        var texture=Resources.Load<Texture2D>(ResourceRoot+name);
        if(texture==null)return System.Array.Empty<Sprite>();
        float width=texture.width/(float)columns,height=texture.height/(float)rows;
        var frames=new Sprite[columns*rows];
        for(int i=0;i<frames.Length;i++)
        {
            frames[i]=Sprite.Create(texture,new Rect(i%columns*width,(rows-1-i/columns)*height,width,height),pivot,height,0,SpriteMeshType.FullRect);
            frames[i].name=ResourceRoot+name+" frame "+i;frames[i].hideFlags=HideFlags.HideAndDontSave;
        }
        return frames;
    }

    public static PixelBeetleOverturn Play(List<Bounds> lanes,SpriteRenderer reference,Vector3 direction=default(Vector3))
    {
        var fx=new GameObject("Pixel Beetle Imperial Horn").AddComponent<PixelBeetleOverturn>();
        fx.reference=reference;fx.area=lanes!=null&&lanes.Count>0?lanes[0]:new Bounds(reference==null?Vector3.zero:reference.transform.position,Vector3.one*3f);
        if(lanes!=null)for(int i=1;i<lanes.Count;i++)fx.area.Encapsulate(lanes[i]);
        fx.view=Camera.main==null?(reference==null?Quaternion.identity:reference.transform.rotation):Camera.main.transform.rotation;
        direction.y=0f;fx.direction=direction.sqrMagnitude>.001f?direction.normalized:Vector3.right;
        fx.size=Mathf.Clamp(fx.area.size.magnitude*.68f,3.5f,8f);
        if(horns==null||horns.Length==0||horns[0]==null)horns=Load("Horn8",4,2,new Vector2(.12f,.16f));
        if(earthFrames==null||earthFrames.Length==0||earthFrames[0]==null)earthFrames=Load("Earth4",4,1,new Vector2(.5f,.22f));
        if(impactFrames==null||impactFrames.Length==0||impactFrames[0]==null)impactFrames=Load("Impact8",4,2,new Vector2(.5f,.22f));
        fx.horn=fx.Renderer("Eight drawing imperial horn",4);
        for(int i=0;i<fx.earth.Length;i++)fx.earth[i]=fx.Renderer("Four drawing turf upheaval "+i,-6);
        fx.fragments=PixelMushroomGas.CreateParticles("Imperial horn three dimensional clods",fx.transform,fx.particles.Length,
            PixelMushroomGas.Material(true),reference,2,true);
        fx.Pose(0f);return fx;
    }

    private SpriteRenderer Renderer(string name,int order)
    {
        var child=new GameObject(name);child.transform.SetParent(transform,false);
        var renderer=child.AddComponent<SpriteRenderer>();
        if(reference!=null){renderer.sortingLayerID=reference.sortingLayerID;renderer.sortingOrder=reference.sortingOrder+order;}
        else renderer.sortingOrder=order;
        return renderer;
    }

    private static float Seed(int value)=>Mathf.Repeat(Mathf.Sin(value*127.1f+311.7f)*43758.5453f,1f);
    private static int Frame(float age,float duration,int count)=>Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(age/duration)*count),0,count-1);
    public static int ImpactFrame(float age)=>age<.025f?0:age<.05f?1:age<.14f?2:age<.26f?3:
        age<.43f?4:age<.62f?5:age<.80f?6:7;
    public static int HornFrame(float age)=>age<.16f?0:age<.34f?1:age<BeetleOverturnRules.Warning?2:age<BeetleOverturnRules.Warning+.12f?3:
        age<BeetleOverturnRules.ApexEnd?4:age<BeetleOverturnRules.ApexEnd+.07f?5:age<BeetleOverturnRules.Landing+.10f?6:7;
    private static float GroundY(Vector3 point)=>point.y+.025f;

    public void Pose(float age)
    {
        if(this==null||fragments==null)return;
        time=Mathf.Max(0f,age);
        if(reference!=null&&reference.sprite!=null)
        {
            Bounds body=PixelMantisReaper.VisibleBody(reference.sprite);
            float headX=body.center.x+body.size.x*.08f;
            joint=reference.transform.TransformPoint(new Vector3(reference.flipX?-headX:headX,body.min.y+body.size.y*.52f,0f));
        }
        PoseHorn();PoseGround();PoseLandings();
    }

    private void PoseHorn()
    {
        horn.enabled=horns.Length==8&&time<BeetleOverturnRules.End&&!ending;
        if(!horn.enabled)return;
        Vector3 projected=Quaternion.Inverse(view)*direction;
        bool flip=Mathf.Abs(projected.x)>.08f?projected.x<0f:reference!=null&&reference.flipX;
        // The lever raises vertically even when its victims surround the caster in depth.
        float tilt=BeetleOverturnRules.SourcePose(time).y*180f;
        horn.sprite=horns[HornFrame(time)];
        horn.flipX=flip;horn.transform.rotation=view*Quaternion.Euler(0f,0f,flip?-tilt:tilt);
        horn.transform.position=joint;
        horn.transform.localScale=Vector3.one*size;
        horn.color=new Color(1f,1f,1f,Mathf.SmoothStep(0f,1f,time/.16f));
    }

    private void PoseGround()
    {
        float phase=Mathf.Clamp01((time-.16f)/(BeetleOverturnRules.Landing-.16f));
        bool visible=earthFrames.Length==4&&time>=.16f&&time<BeetleOverturnRules.Landing&&!ending;
        for(int i=0;i<earth.Length;i++)
        {
            var renderer=earth[i];renderer.enabled=visible;if(!visible)continue;
            Vector3 point=new Vector3(Mathf.Lerp(area.min.x,area.max.x,(i%4+.5f)/4f),GroundY(area.center),
                Mathf.Lerp(area.min.z,area.max.z,(i/4+.5f)/2f));
            renderer.sprite=earthFrames[Frame(phase,1f,4)];renderer.transform.rotation=view;
            renderer.transform.position=point;renderer.transform.localScale=Vector3.one*(.7f+Seed(i)*.35f);
            renderer.flipX=i%2==0;renderer.color=new Color(1f,1f,1f,.72f);
        }
    }

    public void Land(Vector3 at)
    {
        if(this==null||ending||landings.Count>=16)return;
        var dust=Renderer("Eight drawing ground impact "+landings.Count,-1);
        landings.Add(new Landing{point=at,birth=time,dust=dust});PoseLandings();
    }

    private void PoseLandings()
    {
        int count=0;
        if(time<BeetleOverturnRules.Warning)
        {
            float charge=Mathf.SmoothStep(0f,1f,time/BeetleOverturnRules.Warning);
            for(int i=0;i<20;i++)
            {
                float phase=Mathf.Repeat(time*1.5f+Seed(i+53),1f),angle=i*2.399963f;
                Vector3 inward=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f)*(.25f+.65f*(1f-phase));
                float scale=.025f+Seed(i+71)*.025f;
                particles[count++]=new ParticleSystem.Particle{position=joint+view*inward,startSize3D=Vector3.one*scale,
                    startColor=new Color(1f,.76f,.37f,charge*Mathf.Sin(phase*Mathf.PI)*.8f),
                    startLifetime=1f,remainingLifetime=1f,randomSeed=(uint)(i+1)};
            }
        }
        for(int landingIndex=0;landingIndex<landings.Count;landingIndex++)
        {
            var landing=landings[landingIndex];
            float age=Mathf.Max(0f,time-landing.birth),t=Mathf.Clamp01(age/Afterglow);
            landing.dust.enabled=impactFrames.Length==8&&age<Afterglow;
            if(landing.dust.enabled)
            {
                landing.dust.sprite=impactFrames[ImpactFrame(age)];
                landing.dust.transform.SetPositionAndRotation(landing.point+Vector3.up*.025f,view);
                // Keep every drawing on one scale; transparent atlas padding must not shrink the landing burst.
                landing.dust.transform.localScale=Vector3.one*(2.70f/Mathf.Max(.01f,PixelMantisReaper.VisibleBody(impactFrames[2]).size.y));
                landing.dust.color=new Color(1f,1f,1f,1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.72f,1f,t)));
            }
            if(age>=Afterglow)continue;
            for(int i=0;i<44&&count<particles.Length;i++)
            {
                int index=i+landingIndex*53;float seed=Seed(index+7),angle=index*2.399963f;
                float spread=(1f-Mathf.Exp(-age*7f))*(.65f+seed*1.25f);
                Vector3 radial=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
                float up=Mathf.Max(0f,(1.8f+seed*2.4f)*age-4.8f*age*age);
                Vector3 point=landing.point+radial*spread+Vector3.up*(up+.04f);
                Color color=Color.Lerp(new Color(.29f,.20f,.11f),new Color(.78f,.61f,.34f),seed);
                color.a=(1f-t)*(i%4==0?.78f:.95f);
                float scale=(.035f+seed*.075f)*(1f-t*.45f);
                particles[count++]=new ParticleSystem.Particle{position=point,startSize3D=new Vector3(scale,scale*(.7f+seed),scale),
                    rotation3D=new Vector3(index*19f+age*170f,index*31f+age*230f,index*11f),startColor=color,
                    startLifetime=1f,remainingLifetime=1f,randomSeed=(uint)(index+1)};
            }
        }
        if(time>=BeetleOverturnRules.Warning&&time<BeetleOverturnRules.Landing)
        {
            float rise=BeetleOverturnRules.Height(time);
            for(int i=0;i<64&&count<particles.Length;i++)
            {
                float angle=i*2.399963f,seed=Seed(i+13);
                Vector3 point=new Vector3(Mathf.Lerp(area.min.x,area.max.x,Seed(i*19+2)),GroundY(area.center),
                    Mathf.Lerp(area.min.z,area.max.z,Seed(i*31+3)));
                point+=new Vector3(Mathf.Cos(angle)*.08f,rise*(.25f+seed*.55f),Mathf.Sin(angle)*.08f);
                float scale=.03f+seed*.045f;
                particles[count++]=new ParticleSystem.Particle{position=point,startSize3D=Vector3.one*scale,
                    startColor=new Color(.65f,.49f,.27f,rise*.6f),startLifetime=1f,remainingLifetime=1f,randomSeed=(uint)(i+1)};
            }
        }
        fragments.SetParticles(particles,count);
    }

    public void Cancel()
    {
        if(this==null||ending||CombatEffectEndFade.IsFading(this))return;
        if(!Application.isPlaying){DestroyImmediate(gameObject);return;}
        ending=true;horn.enabled=false;foreach(var patch in earth)patch.enabled=false;
    }

    private void Update()
    {
        if(!ending||CombatEffectEndFade.IsFading(this))return;
        float dt=Time.deltaTime*CombatStatFormula.CombatAnimationRate;
        time+=Mathf.Max(0f,dt);PoseLandings();
        if(landings.Count==0||time>=BeetleOverturnRules.Landing+Afterglow)Destroy(gameObject);
    }
}
