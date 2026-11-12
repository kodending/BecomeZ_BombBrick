using System;
using UnityEngine;
using Object=UnityEngine.Object;

// Only the isolated cinematic stage is moved or rendered; the battle clock remains frozen.
public sealed class RaptorApexStageImpact:IDisposable
{
    public const int DustCapacity=96,PebbleCapacity=16;
    private const int StageLayer=30,RingPoints=72;
    private readonly GameObject root;
    private readonly LineRenderer[] rings=new LineRenderer[2];
    private readonly Vector3[] ringPositions=new Vector3[RingPoints];
    private readonly ParticleSystem dust,pebbles;
    private readonly ParticleSystemRenderer dustRenderer,pebbleRenderer;
    private readonly ParticleSystem.Particle[] dustBuffer=new ParticleSystem.Particle[DustCapacity];
    private readonly ParticleSystem.Particle[] pebbleBuffer=new ParticleSystem.Particle[PebbleCapacity];
    private readonly Material ringMaterial,dustMaterial,pebbleMaterial;
    private bool disposed;

    public Vector2 CameraOffset {get;private set;}

    public RaptorApexStageImpact(Transform stageRoot)
    {
        if(stageRoot==null)throw new ArgumentNullException(nameof(stageRoot));
        root=new GameObject("Apex stage ground impact and gathering light");root.layer=StageLayer;
        root.transform.SetParent(stageRoot,false);
        ringMaterial=new Material(Resources.Load<Shader>("CombatCinematics/RaptorApex/ApexAuraFlow"))
            {name="Apex stage warm shockwave",hideFlags=HideFlags.DontSave};
        Shader shader=Resources.Load<Shader>("CombatCinematics/RaptorApex/ApexStageDust");
        dustMaterial=new Material(shader){name="Apex stage soft earth dust",hideFlags=HideFlags.DontSave};
        pebbleMaterial=new Material(shader){name="Apex stage small flying stones",hideFlags=HideFlags.DontSave};
        pebbleMaterial.SetFloat("_Hardness",1f);
        for(int i=0;i<rings.Length;i++)
        {
            var host=Child("Roar ground shockwave "+(i+1));
            var line=host.AddComponent<LineRenderer>();rings[i]=line;
            line.sharedMaterial=ringMaterial;line.useWorldSpace=false;line.loop=true;
            line.positionCount=RingPoints;line.alignment=LineAlignment.View;
            line.textureMode=LineTextureMode.Stretch;line.numCornerVertices=2;line.numCapVertices=2;
            line.sortingOrder=0;line.enabled=false;
        }
        dust=Particles("Ground dust and gathering sparks",DustCapacity,dustMaterial,out dustRenderer);
        pebbles=Particles("Roar lifted pebbles",PebbleCapacity,pebbleMaterial,out pebbleRenderer);
        Clear();
    }

    public void Tick(float elapsed,float unscaledDt)
    {
        if(disposed)return;
        float t=Mathf.Max(0f,elapsed),dt=Mathf.Max(0f,unscaledDt);
        if(t<2.35f||t>=5.75f){Clear();return;}
        root.SetActive(true);
        CameraOffset=Quake(t-3.25f,.105f,.82f)+Quake(t-3.75f,.073f,.72f)+Quake(t-4.5f,.035f,.42f);
        CameraOffset=Vector2.ClampMagnitude(CameraOffset,.085f);
        ringMaterial.SetFloat("_AuraTime",t);
        DrawRing(0,t-3.25f,.88f,2.8f);
        DrawRing(1,t-3.75f,.95f,3.6f);
        int dustCount=0,stoneCount=0;
        Gather(t,ref dustCount);
        Burst(t-3.25f,0,32,ref dustCount);
        Burst(t-3.75f,32,24,ref dustCount);
        DashWake(t,ref dustCount);
        Stones(t-3.25f,0,10,ref stoneCount);
        Stones(t-3.75f,10,6,ref stoneCount);
        SetParticles(dust,dustRenderer,dustBuffer,dustCount,dt);
        SetParticles(pebbles,pebbleRenderer,pebbleBuffer,stoneCount,dt);
    }

    public void Clear()
    {
        CameraOffset=Vector2.zero;
        if(root==null)return;
        for(int i=0;i<rings.Length;i++)if(rings[i]!=null)rings[i].enabled=false;
        if(dust!=null)dust.SetParticles(dustBuffer,0);
        if(pebbles!=null)pebbles.SetParticles(pebbleBuffer,0);
        if(dustRenderer!=null)dustRenderer.enabled=false;
        if(pebbleRenderer!=null)pebbleRenderer.enabled=false;
        root.SetActive(false);
    }

    public void Dispose()
    {
        if(disposed)return;
        Clear();disposed=true;Destroy(root);Destroy(ringMaterial);Destroy(dustMaterial);Destroy(pebbleMaterial);
    }

    private GameObject Child(string name)
    {
        var host=new GameObject(name);host.layer=StageLayer;host.transform.SetParent(root.transform,false);return host;
    }

    private ParticleSystem Particles(string name,int capacity,Material material,out ParticleSystemRenderer renderer)
    {
        var host=Child(name);var system=host.AddComponent<ParticleSystem>();
        system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=system.main;main.playOnAwake=false;main.loop=false;main.maxParticles=capacity;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.useUnscaledTime=true;
        main.startSpeed=0f;main.startSize3D=true;
        var emission=system.emission;emission.enabled=false;
        var shape=system.shape;shape.enabled=false;
        renderer=host.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.alignment=ParticleSystemRenderSpace.Local;
        renderer.sortMode=ParticleSystemSortMode.None;renderer.sortingOrder=0;
        system.Play(false);system.Pause(false);return system;
    }

    private void DrawRing(int index,float age,float duration,float radius)
    {
        LineRenderer line=rings[index];
        line.enabled=age>=0f&&age<duration;
        if(!line.enabled)return;
        float u=Mathf.Clamp01(age/duration),r=Mathf.Lerp(.3f,radius,1f-Mathf.Pow(1f-u,2f));
        float alpha=Mathf.Pow(1f-u,1.6f)*Mathf.Clamp01(age/.035f);
        line.startWidth=line.endWidth=Mathf.Lerp(.14f,.065f,u);
        line.startColor=line.endColor=new Color(1f,.66f,.28f,alpha*.88f);
        for(int i=0;i<RingPoints;i++)
        {
            float angle=i*Mathf.PI*2f/RingPoints;
            ringPositions[i]=new Vector3(Mathf.Cos(angle)*r,.045f+Mathf.Sin(angle)*r*.2f,.2f);
        }
        line.SetPositions(ringPositions);
    }

    private void Gather(float t,ref int count)
    {
        float age=t-2.4f;
        if(age<0f||age>=1.06f)return;
        float envelope=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/.18f))*(1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((age-.72f)/.34f)));
        for(int i=0;i<20;i++)
        {
            float p=Mathf.Repeat(age*1.05f+Seed(i+31),1f),angle=i*2.399963f+age*1.6f;
            float radius=(1f-p)*(1.15f+Seed(i+53)*.55f);
            Vector3 position=new Vector3(Mathf.Cos(angle)*radius,.07f+p*.92f+Mathf.Sin(angle)*radius*.13f,.15f);
            float size=(.045f+Seed(i+7)*.045f)*Mathf.Sin(p*Mathf.PI);
            Add(dustBuffer,ref count,position,new Vector2(size,size),new Color(1f,.72f,.3f,envelope*.9f),0f);
        }
    }

    private void Burst(float age,int seed,int amount,ref int count)
    {
        if(age<0f||age>1.38f)return;
        for(int i=0;i<amount;i++)
        {
            float life=.8f+Seed(seed+i+8)*.52f,u=age/life;
            if(u>=1f)continue;
            float angle=(i+.5f)*Mathf.PI*2f/amount,x=Mathf.Cos(angle);
            float speed=1.4f+Seed(seed+i+15)*2.3f;
            Vector3 position=new Vector3(x*(.22f+age*speed*.75f),
                .05f+Mathf.Max(0f,(.5f+Seed(seed+i+28)*1.5f)*age-1.5f*age*age),.24f+Mathf.Sin(angle)*.04f);
            float size=.19f+Seed(seed+i)*.18f+age*.46f;
            Color color=Color.Lerp(new Color(.35f,.23f,.12f),new Color(.74f,.57f,.3f),Seed(seed+i+41));
            color.a=Mathf.Sin(Mathf.Clamp01(u)*Mathf.PI)*.42f;
            Add(dustBuffer,ref count,position,new Vector2(size,size*.62f),color,Seed(i+3)*360f+age*18f);
        }
    }

    private void DashWake(float t,ref int count)
    {
        for(int i=0;i<16;i++)
        {
            float age=t-(4.5f+i*.028f),life=.7f+Seed(i+90)*.22f;
            if(age<0f||age>=life)continue;
            float side=i%2==0?-1f:1f,u=age/life;
            Vector3 position=new Vector3(side*(.12f+age*(1.7f+Seed(i+31)*1.3f)),.04f+Mathf.Sin(u*Mathf.PI)*.19f,.28f);
            float size=.24f+age*.65f;
            Add(dustBuffer,ref count,position,new Vector2(size*1.45f,size*.5f),
                new Color(.63f,.44f,.21f,Mathf.Sin(u*Mathf.PI)*.35f),side*age*25f);
        }
    }

    private void Stones(float age,int seed,int amount,ref int count)
    {
        if(age<0f||age>1.05f)return;
        for(int i=0;i<amount;i++)
        {
            float life=.58f+Seed(seed+i+70)*.42f,u=age/life;
            if(u>=1f)continue;
            float side=(i%2==0?-1f:1f),speed=.8f+Seed(seed+i+11)*1.4f;
            Vector3 position=new Vector3(side*(.2f+age*speed),.055f+Mathf.Sin(u*Mathf.PI)*(.3f+Seed(seed+i+21)*.65f),.12f);
            float size=.035f+Seed(seed+i+37)*.045f;
            Add(pebbleBuffer,ref count,position,new Vector2(size,size*.7f),
                new Color(.5f,.4f,.26f,1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((u-.7f)/.3f))),i*71f+age*side*220f);
        }
    }

    private static void Add(ParticleSystem.Particle[] buffer,ref int count,Vector3 position,Vector2 size,Color color,float rotation)
    {
        if(count>=buffer.Length)return;
        buffer[count++]=new ParticleSystem.Particle
            {position=position,startSize3D=new Vector3(size.x,size.y,1f),startColor=color,
             rotation=rotation,startLifetime=2f,remainingLifetime=2f,randomSeed=1};
    }

    private static void SetParticles(ParticleSystem system,ParticleSystemRenderer renderer,ParticleSystem.Particle[] buffer,int count,float dt)
    {
        if(!system.isPaused){system.Play(false);system.Pause(false);}
        if(dt>0f)system.Simulate(dt,false,false,false);
        system.SetParticles(buffer,count);renderer.enabled=count>0;
    }

    private static Vector2 Quake(float age,float amplitude,float duration)
    {
        if(age<=0f||age>=duration)return Vector2.zero;
        float envelope=Mathf.Exp(-age*5.5f)*Mathf.Pow(1f-age/duration,1.2f);
        return new Vector2(Mathf.Sin(age*61f),Mathf.Sin(age*82f)*.62f)*amplitude*envelope;
    }

    private static float Seed(int value)=>Mathf.Repeat(Mathf.Sin(value*127.1f+311.7f)*43758.5453f,1f);
    private static void Destroy(Object value)
    {if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
