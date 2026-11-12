using System.Collections.Generic;
using UnityEngine;

// One cast owns every grounded skid; real bodies keep their authored animation and never become flying clones.
public sealed class PixelMammothExpulsion:MonoBehaviour
{
    public const float TailDuration=.70f;
    private const int Capacity=384;
    private struct Mote{public Vector3 at,velocity;public Color color;public float birth,life,size,turn;public bool skid;}
    private struct Sample{public Vector3 at;public float time;}
    private readonly Mote[] motes=new Mote[Capacity];
    private readonly ParticleSystem.Particle[] dustBuffer=new ParticleSystem.Particle[Capacity+24];
    private readonly ParticleSystem.Particle[] skidBuffer=new ParticleSystem.Particle[Capacity];
    private readonly Dictionary<int,Sample> samples=new Dictionary<int,Sample>();
    private ParticleSystem dust,skids;
    private Vector3 ground,direction,lastChargeGround;
    private float lastChargeTime,stoppedAt=-1f;
    private int cursor;
    private bool charging;
    public float Elapsed{get;private set;}
    public float RemainingLifetime=>Mathf.Max(0f,(stoppedAt<0f?Elapsed:stoppedAt)+TailDuration-Elapsed);

    public static PixelMammothExpulsion Prepare(SpriteRenderer source,Vector3 ground,Vector3 direction,Color tint)
    {
        var fx=new GameObject("Mammoth grounded chain-collision trails").AddComponent<PixelMammothExpulsion>();
        fx.ground=ground;fx.lastChargeGround=ground;fx.direction=FlatDirection(direction);
        fx.gameObject.layer=source==null?0:source.gameObject.layer;
        fx.dust=PixelMushroomGas.CreateParticles("Eight-frame hoof and sliding dust",fx.transform,Capacity+24,PixelMammothChainImpact.DustMaterial,source,0,true);
        PixelMammothChainImpact.AnimateDust(fx.dust);
        fx.skids=PixelMushroomGas.CreateParticles("Grounded skid grooves",fx.transform,Capacity,PixelMushroomGas.Material(true),source,-2,true);
        fx.skids.GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.HorizontalBillboard;
        fx.dust.gameObject.layer=fx.skids.gameObject.layer=fx.gameObject.layer;fx.Pose(0f);return fx;
    }
    private static Vector3 FlatDirection(Vector3 value){value.y=0f;return value.sqrMagnitude<.0001f?Vector3.right:value.normalized;}
    private static float Seed(int value)=>Mathf.Repeat(Mathf.Sin(value*127.1f+17.7f)*43758.5453f,1f);
    private void Add(Mote mote){motes[cursor]=mote;cursor=(cursor+1)%Capacity;}

    public void Charge(Vector3 at,Vector3 forward,float absoluteAge)
    {
        if(this==null||stoppedAt>=0f)return;direction=FlatDirection(forward);ground=at;charging=true;
        float dt=absoluteAge-lastChargeTime;
        if(dt<=.00001f)return;
        Vector3 velocity=(at-lastChargeGround)/dt;Vector3 side=Vector3.Cross(Vector3.up,direction)*.35f;
        SampleSkid(-1,at+side,velocity,absoluteAge,true);SampleSkid(-2,at-side,velocity,absoluteAge,true);
        lastChargeGround=at;lastChargeTime=absoluteAge;
    }
    public void Slide(Vector3 at,Vector3 velocity,float absoluteAge,SpriteRenderer body)
    {
        if(this==null||stoppedAt>=0f||body==null)return;SampleSkid(body.GetInstanceID(),at,velocity,absoluteAge,false);
    }
    private void SampleSkid(int id,Vector3 at,Vector3 velocity,float time,bool hoof)
    {
        velocity.y=0f;if(velocity.sqrMagnitude<.04f)return;
        bool seen=samples.TryGetValue(id,out Sample previous);
        if(seen&&time-previous.time<.055f)return;
        Vector3 from=seen?previous.at:at;float distance=Vector3.Distance(from,at);
        int steps=Mathf.Clamp(Mathf.CeilToInt(distance/.40f),1,3);Vector3 forward=FlatDirection(velocity),side=Vector3.Cross(Vector3.up,forward);
        for(int i=0;i<steps;i++)
        {
            Vector3 point=Vector3.Lerp(from,at,(i+1f)/steps);int key=cursor+id*7;float seed=Seed(key);
            float turn=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            Add(new Mote{at=point+Vector3.up*.035f,velocity=forward*.12f,birth=time,life=.62f,size=.20f+seed*.12f,
                turn=turn,color=MeadowAmbientParticles.TerrainColor(new Color(.39f,.31f,.23f,.54f)),skid=true});
            if(i<steps-1)continue;
            Add(new Mote{at=point+Vector3.up*.12f,velocity=-forward*(.8f+seed*.8f)+side*(seed-.5f)*.8f+Vector3.up*(.2f+seed*.35f),
                birth=time,life=.48f+seed*.16f,size=(hoof?.42f:.30f)+seed*.28f,turn=(seed-.5f)*28f,
                color=MeadowAmbientParticles.TerrainColor(Color.Lerp(new Color(.88f,.77f,.56f,.66f),new Color(.58f,.51f,.36f,.54f),seed))});
        }
        samples[id]=new Sample{at=at,time=time};
    }
    public void Pose(float absoluteAge)
    {
        if(this==null)return;Elapsed=Mathf.Max(0f,absoluteAge);
        if(dust.isStopped)dust.Play(false);if(skids.isStopped)skids.Play(false);
        int count=0,grooves=0;
        if(!charging&&(stoppedAt<0f||Elapsed-stoppedAt<.35f))
        {
            float compression=Mathf.Clamp01(Elapsed/Mathf.Max(.01f,MammothExpulsionRules.Contact));
            float tail=stoppedAt<0f?1f:Mathf.Clamp01(1f-(Elapsed-stoppedAt)/.35f);
            Vector3 side=Vector3.Cross(Vector3.up,direction);
            for(int i=0;i<24;i++)
            {
                float seed=Seed(i+13),phase=Mathf.Repeat(Elapsed*1.6f+seed,1f);
                Vector3 at=ground+side*(i%2==0?-.38f:.38f)-direction*(.08f+phase*.35f)+Vector3.up*(.08f+Mathf.Sin(phase*Mathf.PI)*.15f);
                Color color=MeadowAmbientParticles.TerrainColor(new Color(.75f,.66f,.49f,compression*(1f-phase)*.28f*tail));
                dustBuffer[count++]=Particle(at,new Vector3(.26f+phase*.28f,.22f+phase*.24f,1f),color,1f,phase,i,0f);
            }
        }
        for(int i=0;i<motes.Length;i++)
        {
            Mote mote=motes[i];float age=Elapsed-mote.birth;if(mote.life<=0f||age<0f||age>=mote.life)continue;
            float t=age/mote.life;Color color=mote.color;color.a*=1f-Mathf.SmoothStep(0f,1f,t);
            float drag=(1f-Mathf.Exp(-age*5f))/5f;Vector3 at=mote.at+mote.velocity*drag;
            Vector3 size=mote.skid?new Vector3(mote.size,.30f+mote.size*.9f,1f):Vector3.one*mote.size*Mathf.Lerp(.70f,1.35f,t);
            var particle=Particle(at,size,color,mote.life,age,i+24,mote.turn);
            if(mote.skid)skidBuffer[grooves++]=particle;else dustBuffer[count++]=particle;
        }
        dust.SetParticles(dustBuffer,count);skids.SetParticles(skidBuffer,grooves);
    }
    private static ParticleSystem.Particle Particle(Vector3 at,Vector3 size,Color color,float life,float age,int seed,float rotation)=>
        new ParticleSystem.Particle{position=at,startSize3D=size,startColor=color,startLifetime=life,
            remainingLifetime=Mathf.Max(.0001f,life-age),randomSeed=(uint)(seed+1),rotation=rotation};
    public static PixelMammothChainImpact Contact(Vector3 point,SpriteRenderer source,Color tint,bool wall,bool strong)=>
        PixelMammothChainImpact.Play(point,source,tint,wall,strong);
    public void StopEmission(){if(stoppedAt<0f)stoppedAt=Elapsed;}
    public void Cancel()
    {
        if(this==null||CombatEffectEndFade.IsFading(this))return;
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
