using System;
using UnityEngine;

// Mortar-style ground shadows; every breath comet and burst shares the combat contact clock.
public sealed class PixelWyvernRain:MonoBehaviour
{
    private const float ProjectileVisualScale=2f;
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private ParticleSystem body,sparks;
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[400];
    private readonly SpriteRenderer[] shadows=new SpriteRenderer[WyvernRank3Rules.RainCount];
    private readonly float[] divertedAt=new float[WyvernRank3Rules.RainCount];
    private readonly Vector3[] divertedFrom=new Vector3[WyvernRank3Rules.RainCount],escape=new Vector3[WyvernRank3Rules.RainCount];
    private Vector3[] points,offsets;
    private bool[] blocked;
    private SpriteRenderer reference;
    private Vector3 origin,direction;
    private float radius,age,finishAge,finishClock;
    private int element;
    private bool finishing,fire,mouthEmissionStopped;
    private Func<float> clock;
    private Func<bool> valid;

    public static Vector3 SkyOffset(Vector3 point,Camera camera)
    {
        Vector3 right=camera==null?Vector3.right:camera.transform.right;
        right.y=0f;right.Normalize();
        for(float h=8f;h<=40f;h+=2f)
        {
            Vector3 offset=Vector3.up*h+right*h*.7f;
            if(camera==null)return offset;
            Vector3 view=camera.WorldToViewportPoint(point+offset);
            if(view.y>1.15f||view.x>1.15f||view.z<=camera.nearClipPlane)return offset;
        }
        return Vector3.up*40f+right*28f;
    }

    public static Vector3 FlightPoint(Vector3 point,Vector3 offset,int index,float time)
    {
        float t=Mathf.Clamp01((time-WyvernRank3Rules.RainImpact(index)+WyvernRank3Rules.RainFall)/WyvernRank3Rules.RainFall);
        return point+Vector3.up*.12f+offset*(1f-t*t);
    }

    public static PixelWyvernRain Create(SpriteRenderer reference,Vector3 origin,Vector3 direction,
        Vector3[] points,Vector3[] offsets,bool[] blocked,float radius,int element,Func<float> clock,Func<bool> valid)
    {
        var fx=new GameObject("Pixel Wyvern Falling Breath").AddComponent<PixelWyvernRain>();
        fx.reference=reference;fx.origin=origin;fx.direction=direction;fx.points=points;fx.offsets=offsets;
        fx.blocked=blocked;fx.radius=radius;fx.element=element;fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;
        fx.body=PixelMushroomGas.CreateParticles("Falling Breath Clouds",fx.transform,400,
            PixelMushroomGas.Material(false),reference,20,true);
        fx.sparks=PixelMushroomGas.CreateParticles("Falling Breath Fragments",fx.transform,400,
            PixelMushroomGas.Material(true),reference,21,true);
        for(int i=0;i<points.Length;i++)
        {
            fx.divertedAt[i]=-1f;
            var shadow=new GameObject("Breath Landing Shadow "+i).AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(fx.transform,false);shadow.sprite=PixelMonkeyMortar.ShadowSprite();
            shadow.sortingLayerID=reference==null?0:reference.sortingLayerID;shadow.sortingOrder=1;
            shadow.transform.position=points[i]+Vector3.up*.035f;
            shadow.transform.rotation=Quaternion.Euler(90f,0f,0f);fx.shadows[i]=shadow;
        }
        fx.Pose(0f);return fx;
    }

    private static float Seed(int n)=>Mathf.Repeat(Mathf.Sin(n*127.1f+311.7f)*43758.5453f,1f);
    private Color Hot=>element==0?new Color(.9f,.63f,1f):element==1?new Color(1f,.95f,.55f):new Color(.86f,.98f,1f);
    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(valid!=null&&!valid()){Cancel();return;}
            if(finishing&&!fire)Pose(finishAge+Mathf.Max(0f,clock()-finishClock));
        }
    }

    public void Pose(float time)
    {
        if(this==null)return;age=time;
        float final=WyvernRank3Rules.RainImpact(points.Length-1);
        if(finishing&&!fire&&time>final+.8f){Cancel();return;}
        for(int i=0;i<points.Length;i++)
        {
            float impact=WyvernRank3Rules.RainImpact(i);
            float warning=time-impact+WyvernRank3Rules.RainWarning;
            shadows[i].enabled=!blocked[i]&&warning>=0f&&time<impact;
            if(!shadows[i].enabled)continue;
            float growth=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(warning/WyvernRank3Rules.RainWarning));
            shadows[i].transform.localScale=Vector3.one*(2f*radius*Mathf.Lerp(.12f,1f,growth));
            shadows[i].color=new Color(.035f,.025f,.055f,Mathf.Lerp(.08f,.52f,growth));
        }
        for(int layer=0;layer<2;layer++)
        {
            int count=0;
            // A short upward exhale links the caster to the rain without changing the body scale.
            if(!mouthEmissionStopped&&time<.65f)
            {
                Vector3 mouth=origin+direction*.4f+Vector3.up*(reference==null?.6f:reference.bounds.center.y);
                for(int n=0;n<32;n++)
                {
                    float s=Seed(n),release=time-WyvernRank3Rules.RainWindup;
                    float t=Mathf.Clamp01(release/.3f),a=n*2.4f;
                    Vector3 p=release<0f?mouth+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(.5f+s*.4f)*(1f-time/.35f):
                        mouth+Vector3.up*(t*7f*(.3f+s))+
                        (direction*(t*.5f)+Vector3.forward*Mathf.Sin(a)*t*.3f)*ProjectileVisualScale;
                    Color color=Color.Lerp(PixelWyvernBreath.Tint(element),Hot,s);
                    color.a=Mathf.Clamp01(time/.2f)*(1f-t)*(layer==0?.55f:.85f);
                    particles[count++]=Particle(p,(layer==0?.18f+s*.22f:.04f+s*.05f)*ProjectileVisualScale,color,n);
                }
            }
            for(int i=0;i<points.Length;i++)
            {
                float impact=WyvernRank3Rules.RainImpact(i),after=time-impact;
                float fall=impact-WyvernRank3Rules.RainFall;
                bool diverted=divertedAt[i]>=0f;
                if(blocked[i]&&!diverted)continue;
                if(diverted||time>=fall&&after<0f)
                {
                    float elapsed=time-divertedAt[i];
                    if(diverted&&elapsed>.7f)continue;
                    Vector3 head=diverted?Vector3.Lerp(divertedFrom[i],escape[i],Mathf.Pow(Mathf.Clamp01(elapsed/.7f),.8f)):
                        FlightPoint(points[i],offsets[i],i,time);
                    Vector3 tail=diverted?(divertedFrom[i]-escape[i]).normalized:offsets[i].normalized;
                    var camera=Camera.main;
                    Vector3 side=Vector3.Cross(tail,camera==null?Vector3.forward:camera.transform.forward).normalized;
                    for(int n=0;n<48;n++)
                    {
                        float s=Seed(n+i*73),trail=n<16?s*.18f:(n-16)/32f;
                        float curl=Mathf.Sin(time*28f+n*2.4f)*(.08f+trail*.14f);
                        Vector3 p=head+(tail*(trail*2.7f)+side*(curl+(s-.5f)*.32f*(1f-trail)))*ProjectileVisualScale;
                        Color color=Color.Lerp(Hot,PixelWyvernBreath.Tint(element),trail*.85f+s*.15f);
                        color.a=(layer==0?.8f:.95f)*(1f-trail)*Mathf.Clamp01((time-fall)/.025f);
                        if(diverted)color.a*=1f-Mathf.Clamp01((elapsed-.48f)/.22f);
                        particles[count++]=Particle(p,(layer==0?(.48f+s*.25f)*(1f-trail*.7f):.055f+s*.07f)*ProjectileVisualScale,color,n);
                    }
                }
                else if(after>=0f&&after<.36f)
                {
                    float spread=1f-Mathf.Pow(1f-Mathf.Clamp01(after/.14f),3f);
                    for(int n=0;n<48;n++)
                    {
                        float s=Seed(n+i*97),a=n*2.399963f;
                        float distance=radius*spread*(n<32?.75f+s*.25f:s*.6f);
                        Vector3 p=points[i]+new Vector3(Mathf.Cos(a)*distance,
                            .14f+Mathf.Sin(Mathf.Clamp01(after/.36f)*Mathf.PI)*(.2f+s*.65f),Mathf.Sin(a)*distance);
                        Color color=Color.Lerp(Hot,PixelWyvernBreath.Tint(element),Mathf.Clamp01(after/.2f));
                        color.a=(layer==0?.64f:.9f)*(1f-after/.36f);
                        particles[count++]=Particle(p,layer==0?(.24f+s*.28f)*(1f+after):.045f+s*.08f,color,n);
                    }
                }
                if(fire&&!blocked[i]&&time>=final)
                {
                    float fireAge=time-final;
                    for(int n=0;n<16;n++)
                    {
                        float s=Seed(n+i*67),t=Mathf.Repeat(fireAge*(1.1f+s)+s,1f),a=n*2.4f;
                        Vector3 p=points[i]+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*Mathf.Sqrt(s)+Vector3.up*(.1f+t*.4f);
                        Color color=Color.Lerp(Hot,PixelWyvernBreath.Tint(1),t);
                        color.a=Mathf.Sin(t*Mathf.PI)*.5f*Mathf.Clamp01((4f-fireAge)/.3f);
                        particles[count++]=Particle(p,(layer==0?.2f:.065f)*(1f-t*.6f),color,n);
                    }
                }
            }
            (layer==0?body:sparks).SetParticles(particles,count);
        }
    }

    private ParticleSystem.Particle Particle(Vector3 p,float size,Color color,int n)=>
        new ParticleSystem.Particle {position=p,startSize3D=Vector3.one*size,startColor=color,
            rotation=element==2?45f:0f,startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(n+1)};

    public void Deflect(int index,float time,Vector3 wind,Bounds arena)
    {
        if(this==null)return;
        divertedAt[index]=time;divertedFrom[index]=FlightPoint(points[index],offsets[index],index,time);
        escape[index]=divertedFrom[index]+wind*(arena.size.x+8f)+Vector3.up*3f;
    }
    public void StopMouthEmission()
    {
        if(this==null||mouthEmissionStopped)return;
        mouthEmissionStopped=true;Pose(age);
    }
    public void Finish(bool leaveFire)
    {if(this==null)return;finishing=true;fire=leaveFire;finishAge=age;finishClock=clock();}
    public void Cancel()
    {
        if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
