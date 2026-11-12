using System.Collections.Generic;
using UnityEngine;

// Combat owns the targets and clock; these pixel tethers and fragments never move actors.
public sealed class PixelFrogGrandSlam : MonoBehaviour
{
    public const float FadeDuration=.24f;
    public const float EndTime=FrogGrandSlamRules.HitTime+.5f;
    private static readonly Color Pink=new Color(1f,.36f,.67f),Mint=new Color(.48f,1f,.85f),Gold=new Color(1f,.79f,.3f);
    private ParticleSystem particles;
    private ParticleSystem.Particle[] pixels;
    private SpriteRenderer source;
    private Transform[] targets;
    private Vector3[] endpoints,landingPoints;
    private Vector3 center,mouth,right,up;
    private float width,age,fadeAge;
    private bool ending,fadeBeforeImpact,frozen,released,landingsFixed;

    public float Age=>age;
    public Vector3 Center=>center;
    public ParticleSystem Particles=>particles;
    public float RemainingEndTime=>fadeBeforeImpact?Mathf.Max(0f,FadeDuration-fadeAge):Mathf.Max(0f,EndTime-age);

    public static PixelFrogGrandSlam Create(SpriteRenderer source,Vector3 groundCenter,float groundWidth,List<Transform> targets)
    {
        if(source==null)return null;
        var root=new GameObject("Frog Catch-All Grand Slam");
        root.layer=source.gameObject.layer==LayerMask.NameToLayer("ScreenSpaceOutline")?0:source.gameObject.layer;
        var fx=root.AddComponent<PixelFrogGrandSlam>();fx.source=source;fx.center=groundCenter;
        fx.width=Mathf.Max(1.4f,groundWidth);fx.targets=targets==null?new Transform[0]:targets.ToArray();
        fx.endpoints=new Vector3[fx.targets.Length];fx.landingPoints=new Vector3[fx.targets.Length];
        fx.pixels=new ParticleSystem.Particle[Mathf.Max(1024,fx.targets.Length*96+160)];
        var view=Camera.main==null?source.transform.rotation:Camera.main.transform.rotation;
        fx.right=view*Vector3.right;fx.up=view*Vector3.up;
        fx.particles=PixelMushroomGas.CreateParticles("Frog Pink Tethers and Slam Pixels",root.transform,
            fx.pixels.Length,PixelMushroomGas.Material(true),source,18,true);
        fx.particles.gameObject.layer=root.layer;fx.particles.Pause(false);
        fx.Advance(0f,groundCenter);return fx;
    }

    public void Advance(float elapsed,Vector3 groundCenter)
    {
        if(released||ending)return;
        age=Mathf.Max(0f,elapsed);center=groundCenter;
        if(age>=EndTime){Release();return;}
        CacheAnchors();Draw(1f);
    }

    public void Cancel(bool immediate=false)
    {
        if(released)return;
        if(immediate){Release();return;}
        if(CombatEffectEndPlayback.IsPreserving(this)||ending)return;
        FreezeAnchors();ending=true;fadeBeforeImpact=age<FrogGrandSlamRules.HitTime;fadeAge=0f;
    }

    public void PrepareEndPlayback()
    {
        FreezeAnchors();ending=true;
    }

    public void AdvanceEnd(float dt)
    {
        if(released)return;
        if(fadeBeforeImpact)
        {
            fadeAge+=Mathf.Max(0f,dt);
            if(fadeAge>=FadeDuration){Release();return;}
            Draw(1f-Mathf.SmoothStep(0f,1f,fadeAge/FadeDuration));return;
        }
        age+=Mathf.Max(0f,dt);
        if(age>=EndTime){Release();return;}
        Draw(1f);
    }

    private void Update(){if(ending)AdvanceEnd(Time.unscaledDeltaTime);}
    private void FreezeAnchors(){if(frozen)return;CacheAnchors();frozen=true;}
    private void CacheAnchors()
    {
        if(frozen)return;
        if(source!=null)mouth=source.bounds.center+up*.08f;
        for(int i=0;i<targets.Length;i++)if(targets[i]!=null)endpoints[i]=targets[i].position+Vector3.up*.3f;
    }

    private void Draw(float opacity)
    {
        int count=0;
        if(age<FrogGrandSlamRules.HitTime)
        {
            landingsFixed=false;
            DrawTethers(ref count,opacity);
            if(age>=FrogGrandSlamRules.PullEnd)DrawLift(ref count,opacity);
        }
        else
        {
            if(!landingsFixed)
            {
                for(int i=0;i<endpoints.Length;i++)landingPoints[i]=new Vector3(endpoints[i].x,center.y,endpoints[i].z);
                landingsFixed=true;
            }
            float after=age-FrogGrandSlamRules.HitTime;
            DrawImpact(ref count,after,opacity);DrawLandings(ref count,after,opacity);
        }
        particles.SetParticles(pixels,count);
    }

    private void DrawTethers(ref int count,float opacity)
    {
        float extension=1f-Mathf.Pow(1f-Mathf.Clamp01(age/FrogGrandSlamRules.CastContact),3f);
        float lift=FrogGrandSlamRules.HeightAt(age);
        for(int target=0;target<endpoints.Length;target++)
        {
            Vector3 end=Vector3.Lerp(mouth,endpoints[target],extension);
            Vector3 control=Vector3.Lerp(mouth,end,.52f)+up*(.35f+lift*.15f)+right*((target%2==0?1f:-1f)*.24f);
            for(int segment=0;segment<28;segment++)
            {
                float t=(segment+.5f)/28f;
                Vector3 point=Bezier(mouth,control,end,t);
                Vector3 tangent=Vector3.Lerp(control-mouth,end-control,t);
                float rotation=90f-Mathf.Atan2(Vector3.Dot(tangent,up),Vector3.Dot(tangent,right))*Mathf.Rad2Deg;
                float length=Mathf.Max(.07f,tangent.magnitude*2.2f/28f);
                float pulse=.86f+.14f*Mathf.Sin(age*9f-segment*.5f+target);
                var edge=new Color(.49f,.09f,.27f,opacity*.75f*extension);
                var body=Color.Lerp(Pink,new Color(1f,.85f,.76f),pulse*.32f);body.a=opacity*extension;
                Add(ref count,point,edge,new Vector3(.19f,length+.025f,1f),rotation);
                Add(ref count,point,body,new Vector3(.115f*pulse,length,1f),rotation);
            }
            if(age<FrogGrandSlamRules.PullEnd)continue;
            for(int mote=0;mote<12;mote++)
            {
                float a=mote*Mathf.PI/6f+age*3f,seed=Hash(mote+target*17);
                Vector3 point=end+right*Mathf.Cos(a)*.4f+up*Mathf.Sin(a)*.27f;
                var color=Color.Lerp(Mint,Gold,seed);color.a=opacity*.75f;
                Add(ref count,point,color,Vector3.one*(.055f+seed*.04f),a*Mathf.Rad2Deg);
            }
        }
    }

    private void DrawLift(ref int count,float opacity)
    {
        float height=FrogGrandSlamRules.HeightAt(age);
        bool falling=age>=FrogGrandSlamRules.SlamStart;
        float reveal=Mathf.Clamp01((age-FrogGrandSlamRules.PullEnd)/.14f);
        for(int i=0;i<72&&!falling;i++)
        {
            float seed=Hash(i+43),phase=Mathf.Repeat(age*(falling?2.3f:1.2f)+seed,1f);
            Vector3 point=center+right*((Hash(i+901)-.5f)*width)+Vector3.up*(.2f+height)+
                up*((falling?1f-phase:phase)*(.5f+seed*.7f));
            var color=Color.Lerp(Mint,Gold,falling?.7f:.25f);color.a=opacity*reveal*Mathf.Sin(phase*Mathf.PI)*.75f;
            Add(ref count,point,color,new Vector3(.05f+seed*.04f,falling?1.15f:.25f+seed*.2f,1f));
        }
        for(int i=0;i<24;i++)
        {
            float seed=Hash(i+653),a=i*2.399963f;
            Vector3 ground=center+new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a))*width*(.16f+seed*.2f);
            var color=Color.Lerp(new Color(.39f,.29f,.18f),new Color(.78f,.65f,.44f),seed);color.a=opacity*reveal*.8f;
            Add(ref count,ground+Vector3.up*(.13f+height*.32f),color,Vector3.one*(.10f+seed*.12f),seed*90f);
        }
        if(falling)DrawThrows(ref count,opacity,height);
    }

    private void DrawThrows(ref int count,float opacity,float height)
    {
        float progress=Mathf.InverseLerp(FrogGrandSlamRules.SlamStart,FrogGrandSlamRules.HitTime,age);
        for(int target=0;target<endpoints.Length;target++)
        {
            Vector3 ground=new Vector3(endpoints[target].x,center.y,endpoints[target].z);
            Vector3 outward=ground-center;
            if(outward.sqrMagnitude<.001f)
            {float angle=target*Mathf.PI*2f/Mathf.Max(1,endpoints.Length);outward=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));}
            Vector3 direction=(outward.normalized*1.3f-Vector3.up*(.5f+progress*3f)).normalized;
            float rotation=90f-Mathf.Atan2(Vector3.Dot(direction,up),Vector3.Dot(direction,right))*Mathf.Rad2Deg;
            Vector3 tip=ground+Vector3.up*(height+.35f);
            for(int segment=0;segment<12;segment++)
            {
                float tail=segment/12f;
                Vector3 point=tip-direction*(.12f+tail*1.6f);
                var halo=Pink;halo.a=opacity*(1f-tail)*.22f;
                var core=Color.Lerp(new Color(1f,.96f,.8f),Gold,tail);core.a=opacity*(1f-tail)*.75f;
                Add(ref count,point,halo,new Vector3(.19f,.27f,1f),rotation);
                Add(ref count,point,core,new Vector3(.07f,.23f,1f),rotation);
            }
        }
    }

    private void DrawLandings(ref int count,float after,float opacity)
    {
        float life=Mathf.Clamp01(after/.5f),fade=(1f-life)*(1f-life)*opacity;
        float travel=(1f-Mathf.Exp(-after*10f))/10f;
        for(int target=0;target<landingPoints.Length;target++)
        for(int mote=0;mote<16;mote++)
        {
            float seed=Hash(target*31+mote+1301),angle=mote*2.399963f;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            Vector3 point=landingPoints[target]+direction*(.15f+travel*(4f+seed*5f))+
                Vector3.up*(.1f+Mathf.Sin(life*Mathf.PI)*(.3f+seed*.6f));
            var color=mote%3==0?Color.Lerp(new Color(.39f,.28f,.16f),new Color(.78f,.63f,.4f),seed):Color.Lerp(Gold,new Color(1f,.96f,.8f),seed*.6f);
            color.a=fade;
            Add(ref count,point,color,Vector3.one*(.075f+seed*.115f),seed*90f+after*120f);
        }
    }

    private void DrawImpact(ref int count,float after,float opacity)
    {
        float life=Mathf.Clamp01(after/.5f),fade=(1f-life)*(1f-life)*opacity;
        float flight=(1f-Mathf.Exp(-after*9f))/9f;
        Vector3 groundRight=new Vector3(right.x,0f,right.z).normalized;
        if(groundRight.sqrMagnitude<.001f)groundRight=Vector3.right;
        Vector3 forward=Vector3.Cross(groundRight,Vector3.up);
        for(int i=0;i<112;i++)
        {
            float seed=Hash(i+211),a=i*2.399963f;
            Vector3 direction=right*Mathf.Cos(a)+up*Mathf.Sin(a);
            Vector3 point=center+Vector3.up*.35f+right*((Hash(i+53)-.5f)*width*.7f)+
                direction*(.15f+flight*(8f+seed*8f))-up*after*after*3f;
            var color=i%4==0?new Color(1f,.97f,.8f):Color.Lerp(Gold,Pink,seed*.4f);color.a=fade;
            Add(ref count,point,color,new Vector3(.09f+seed*.09f,i%3==0?.6f:.2f,1f),90f-a*Mathf.Rad2Deg);
        }
        float radius=Mathf.Max(1.1f,width*.45f)+after*9f;
        for(int i=0;i<128;i++)
        {
            float a=(i%64)*Mathf.PI/32f,r=radius*(i<64?1f:1.26f);
            Vector3 direction=groundRight*Mathf.Cos(a)+forward*Mathf.Sin(a);
            Vector3 point=center+Vector3.up*.06f+direction*r;
            Vector3 tangent=-groundRight*Mathf.Sin(a)+forward*Mathf.Cos(a);
            float rotation=90f-Mathf.Atan2(Vector3.Dot(tangent,up),Vector3.Dot(tangent,right))*Mathf.Rad2Deg;
            var halo=Pink;halo.a=fade*.25f;var core=Gold;core.a=fade*.85f;
            Add(ref count,point,halo,new Vector3(.26f,.23f+after*.5f,1f),rotation);
            Add(ref count,point,core,new Vector3(.10f,.20f+after*.5f,1f),rotation);
        }
        for(int i=0;i<48;i++)
        {
            float seed=Hash(i+971),a=i*2.399963f;
            Vector3 direction=groundRight*Mathf.Cos(a)+forward*Mathf.Sin(a);
            Vector3 point=center+direction*(width*.12f+flight*(6f+seed*7f))+
                Vector3.up*(.14f+Mathf.Sin(life*Mathf.PI)*(.6f+seed*.7f));
            var stone=Color.Lerp(new Color(.34f,.24f,.15f),new Color(.78f,.66f,.45f),seed);stone.a=fade;
            Add(ref count,point,stone,Vector3.one*(.11f+seed*.18f),seed*90f+after*90f);
            var dust=new Color(.73f,.62f,.44f,fade*.35f);
            Add(ref count,point-Vector3.up*.18f,dust,Vector3.one*(.25f+seed*.28f+life*.5f),seed*90f);
        }
        for(int i=0;i<36;i++)
        {
            int ray=i/3,segment=i%3;float a=ray*Mathf.PI/6f;
            Vector3 direction=groundRight*Mathf.Cos(a)+forward*Mathf.Sin(a);
            Vector3 point=center+Vector3.up*.055f+direction*(segment+.7f)*width*.16f;
            float rotation=90f-Mathf.Atan2(Vector3.Dot(direction,up),Vector3.Dot(direction,right))*Mathf.Rad2Deg;
            var color=Gold;color.a=fade*(1f-segment*.2f);
            Add(ref count,point,color,new Vector3(.07f,width*.18f,1f),rotation);
        }
        float flash=1f-Mathf.Clamp01(after/.12f);
        for(int i=0;i<8&&flash>0f;i++)
        {
            float a=i*Mathf.PI*.25f,length=(i%2==0?2.4f:1.6f);
            Vector3 direction=right*Mathf.Cos(a)+up*Mathf.Sin(a),point=center+Vector3.up*.3f+direction*length*.25f;
            var halo=Gold;halo.a=flash*opacity*.75f;var core=new Color(1f,.98f,.85f,flash*opacity);
            Add(ref count,point,halo,new Vector3(.24f,length,1f),90f-a*Mathf.Rad2Deg);
            Add(ref count,point,core,new Vector3(.08f,length*.8f,1f),90f-a*Mathf.Rad2Deg);
        }
    }

    private void Add(ref int count,Vector3 position,Color color,Vector3 size,float rotation=0f)
    {
        pixels[count++]=new ParticleSystem.Particle{position=position,startColor=color,startSize3D=size,
            rotation=rotation,startLifetime=10f,remainingLifetime=10f,velocity=Vector3.zero};
    }
    private static float Hash(int value)=>Mathf.Repeat(Mathf.Sin(value*127.1f+13.7f)*43758.547f,1f);
    private static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,float t)=>a*((1f-t)*(1f-t))+b*(2f*(1f-t)*t)+c*(t*t);
    private void Release()
    {
        if(released)return;released=true;
        if(particles!=null){particles.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);particles.Clear(false);}
        gameObject.SetActive(false);if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
