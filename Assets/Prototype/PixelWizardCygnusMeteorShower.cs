using System;
using UnityEngine;
using Object=UnityEngine.Object;

// An explicit visual clock lets already-released stars outlive their caster without applying damage here.
public sealed class PixelWizardCygnusMeteorShower:MonoBehaviour
{
    public const float Duration=WizardCygnusShowerRules.Duration;
    public const string MeteorResource="CombatCinematics/WizardCygnusShower/CygnusMeteorAction";
    private const int ParticleLimit=6144,TrailSamples=138;
    private const float FlightSeconds=WizardCygnusShowerRules.FlightSeconds;
    private const float MeteorSize=.375f,ImpactSize=.75f,TrailStep=1f/360f,TrailLifetime=.14f;
    private static readonly Vector2[] LaunchNodes={new Vector2(.50f,.43f),new Vector2(.50f,.78f),
        new Vector2(.30f,.63f),new Vector2(.72f,.62f),new Vector2(.36f,.43f),new Vector2(.64f,.43f),
        new Vector2(.22f,.70f),new Vector2(.82f,.70f)};
    private static Texture2D meteorAtlas;
    private static Sprite[] meteorFrames;
    private static float flightBodyWidth,impactVisibleWidth;
    private Vector3 origin,screenRight,screenUp;
    private Quaternion facing;
    private Vector3[] targets,landings,launches;
    private Vector3[] trailPoints;
    private int[] trailCounts;
    private bool[] landed,launched,absorbed;
    private Func<int,Vector3> liveTarget;
    private SpriteRenderer[] stars;
    private Sprite[] frames;
    private ParticleSystem particles;
    private ParticleSystem.Particle[] buffer;
    private Material material;
    private WizardCygnusSkyVisual sky;
    private float scale=1f;
    private int count;
    public float Elapsed{get;private set;}
    public int TargetCount=>targets==null?0:targets.Length;
    public float RemainingLifetime=>Mathf.Max(0f,WizardCygnusShowerRules.DurationForTargets(TargetCount)-Elapsed);

    public static void Warmup()=>LoadMeteorFrames();

    public static PixelWizardCygnusMeteorShower Create(Vector3 origin,Vector3[] targets,SpriteRenderer reference,Func<int,Vector3> liveTarget)
    {
        if(targets==null||targets.Length==0)return null;
        var host=new GameObject("Wizard Cygnus released meteor shower");host.transform.position=origin;
        var effect=host.AddComponent<PixelWizardCygnusMeteorShower>();effect.origin=origin;effect.liveTarget=liveTarget;
        effect.targets=(Vector3[])targets.Clone();effect.landings=new Vector3[WizardCygnusShowerRules.ShotCount(targets.Length)];effect.landed=new bool[effect.landings.Length];
        effect.launches=new Vector3[effect.landings.Length];effect.launched=new bool[effect.landings.Length];
        effect.absorbed=new bool[effect.landings.Length];
        effect.trailPoints=new Vector3[effect.landings.Length*TrailSamples];effect.trailCounts=new int[effect.landings.Length];
        int layer=reference==null?0:reference.gameObject.layer,sortLayer=reference==null?0:reference.sortingLayerID;
        if(layer==LayerMask.NameToLayer("ScreenSpaceOutline"))layer=0;
        int order=(reference==null?0:reference.sortingOrder)+28;host.layer=layer;
        Camera camera=Camera.main;effect.facing=camera==null?Quaternion.identity:camera.transform.rotation;
        effect.screenRight=effect.facing*Vector3.right;effect.screenUp=effect.facing*Vector3.up;
        if(reference!=null&&reference.sprite!=null)
            effect.scale=Mathf.Clamp(PixelMantisReaper.VisibleBody(reference.sprite).size.y*Mathf.Abs(reference.transform.lossyScale.y),.65f,1.45f);
        effect.frames=LoadMeteorFrames();
        effect.stars=new SpriteRenderer[effect.landings.Length];
        for(int i=0;i<effect.stars.Length;i++)
        {
            var sprite=new GameObject("Cygnus meteor "+(i+1)){layer=layer}.AddComponent<SpriteRenderer>();sprite.transform.SetParent(host.transform,false);
            sprite.sprite=effect.frames==null?PixelStarProjectile.SharedSprite:effect.frames[0];
            sprite.sortingLayerID=sortLayer;sprite.sortingOrder=order+2;sprite.enabled=false;effect.stars[i]=sprite;
        }
        effect.material=new Material(Shader.Find("Sprites/Default")){mainTexture=Texture2D.whiteTexture,name="Cygnus meteor square fragments",hideFlags=HideFlags.DontSave};
        var motes=new GameObject("Cygnus meteor tails and impact pixels"){layer=layer};motes.transform.SetParent(host.transform,false);
        effect.particles=motes.AddComponent<ParticleSystem>();effect.particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=effect.particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=ParticleLimit;main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.simulationSpeed=0f;main.startSpeed=0f;main.startSize3D=true;
        var emission=effect.particles.emission;emission.enabled=false;var shape=effect.particles.shape;shape.enabled=false;
        var renderer=effect.particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=effect.material;renderer.sortingLayerID=sortLayer;renderer.sortingOrder=order;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;effect.buffer=new ParticleSystem.Particle[ParticleLimit];
        effect.sky=new WizardCygnusSkyVisual(host.transform,layer,order-30,sortLayer);
        effect.particles.Play(false);effect.particles.Pause(false);effect.Advance(0f);return effect;
    }

    public void Advance(float elapsed)
    {
        if(targets==null||particles==null)return;
        float t=Mathf.Max(0f,elapsed);
        if(t<Elapsed){Array.Clear(landed,0,landed.Length);Array.Clear(launched,0,launched.Length);Array.Clear(trailCounts,0,trailCounts.Length);}
        Elapsed=t;count=0;
        Camera camera=Camera.main;if(camera!=null)facing=camera.transform.rotation;
        screenRight=facing*Vector3.right;screenUp=facing*Vector3.up;
        if(liveTarget!=null)for(int i=0;i<targets.Length;i++)targets[i]=liveTarget(i);
        float life=WizardCygnusShowerRules.DurationForTargets(TargetCount);
        if(t>=life)
        {
            for(int i=0;i<stars.Length;i++)stars[i].enabled=false;
            sky?.Hide();particles.Clear(false);return;
        }
        RenderSky(t,life,camera);
        for(int index=0;index<stars.Length;index++)
        {
            int target=index%targets.Length,wave=index/targets.Length;SpriteRenderer star=stars[index];
            if(absorbed[index]){star.enabled=false;trailCounts[index]=0;continue;}
            float contact=WizardCygnusShowerRules.ImpactTime(index,targets.Length),age=t-contact;
            if(age>=0f&&!landed[index]){landed[index]=true;landings[index]=targets[target];}
            Vector3 end=landed[index]?landings[index]:targets[target];end.y=Mathf.Max(.035f,end.y+.035f);
            float launchTime=contact-FlightSeconds;
            if(t>=launchTime&&!launched[index])
            {
                // Sample the logical launch clock, so skipped frames do not alter its origin.
                RenderSky(launchTime,life,camera);
                launches[index]=sky.Point(LaunchNodes[(target+wave*3)%LaunchNodes.Length]);launched[index]=true;
                RenderSky(t,life,camera);
            }
            if(launched[index])
            {
                RecordTrail(index,launches[index],end,t-launchTime);
                if(age<TrailLifetime)RenderTrail(index,t-launchTime);
            }
            if(age<0f&&age>=-FlightSeconds)
            {
                float u=1f+age/FlightSeconds;Vector3 start=launches[index];
                Vector3 position=CurvePoint(start,end,u,index);
                Vector3 tangent=Quaternion.Inverse(facing)*(CurvePoint(start,end,Mathf.Min(1f,u+.006f),index)-
                    CurvePoint(start,end,Mathf.Max(0f,u-.006f),index));
                if(frames!=null)star.sprite=frames[FlightFrame(t-launchTime)];
                float heading=Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg+(frames==null?-90f:90f);
                star.enabled=true;star.transform.position=position;star.transform.rotation=facing*Quaternion.Euler(0f,0f,heading);
                float desired=(1.12f+.16f*Seed(index))*scale*MeteorSize;
                star.transform.localScale=Vector3.one*(desired/Mathf.Max(.01f,frames==null?star.sprite.bounds.size.x:flightBodyWidth));
                star.color=frames==null?new Color(.76f,.96f,1f,1f):Color.white;
                LaunchPulse(start,t-launchTime,index);
                Add(position,.16f*scale*MeteorSize,new Color(1f,.93f,.62f,.9f));
                for(int j=0;j<6;j++)
                {
                    float angle=t*5f+j*Mathf.PI/3f+index;
                    Vector3 halo=facing*new Vector3(Mathf.Cos(angle)*.29f,Mathf.Sin(angle)*.29f,0f)*scale*MeteorSize;
                    Add(position+halo,(.08f+Seed(j+index)*.045f)*scale*MeteorSize,new Color(.36f,.78f,1f,.75f));
                }
            }
            else if(age>=0f&&age<.35f)
            {
                Impact(end,age,index);
                star.transform.rotation=facing;
                if(frames!=null)
                {
                    star.sprite=frames[ImpactFrame(age)];star.enabled=true;star.transform.position=end;
                    star.transform.localScale=Vector3.one*(3.6f*scale*ImpactSize/Mathf.Max(.01f,impactVisibleWidth));
                    star.color=new Color(1f,1f,1f,1f-Ease(.20f,.35f,age));
                }
                else
                {
                    float flash=1f-Mathf.Clamp01(age/.13f);star.enabled=flash>.001f;
                    star.transform.position=end+Vector3.up*(.12f*ImpactSize);
                    star.transform.localScale=Vector3.one*(scale*ImpactSize*(1.35f+(1f-flash)*1.35f)/Mathf.Max(.01f,star.sprite.bounds.size.x));
                    star.color=new Color(.8f,.96f,1f,flash*.9f);
                }
            }
            else star.enabled=false;
        }
        particles.SetParticles(buffer,count);
    }

    public SpriteRenderer ProjectileRenderer(int index)
    {
        if(stars==null||index<0||index>=stars.Length)return null;
        if(!launched[index])
        {
            int target=index%targets.Length,wave=index/targets.Length;
            if(liveTarget!=null)targets[target]=liveTarget(target);
            float launch=WizardCygnusShowerRules.ImpactTime(index,targets.Length)-FlightSeconds;
            RenderSky(launch,WizardCygnusShowerRules.DurationForTargets(TargetCount),Camera.main);
            launches[index]=sky.Point(LaunchNodes[(target+wave*3)%LaunchNodes.Length]);launched[index]=true;
            var star=stars[index];star.sprite=frames==null?PixelStarProjectile.SharedSprite:frames[0];
            star.transform.SetPositionAndRotation(launches[index],facing);
            float desired=(1.12f+.16f*Seed(index))*scale*MeteorSize;
            star.transform.localScale=Vector3.one*(desired/Mathf.Max(.01f,frames==null?star.sprite.bounds.size.x:flightBodyWidth));
            star.color=frames==null?new Color(.76f,.96f,1f):Color.white;
        }
        return stars[index];
    }

    public void AbsorbProjectile(int index)
    {
        if(stars==null||index<0||index>=stars.Length)return;
        absorbed[index]=true;trailCounts[index]=0;stars[index].enabled=false;
    }

    private static int FlightFrame(float age)=>Mathf.FloorToInt(Mathf.Max(0f,age)*20f)%4;
    private static int ImpactFrame(float age)=>age<.045f?4:age<.12f?5:age<.23f?6:7;

    private void RecordTrail(int index,Vector3 start,Vector3 end,float clock)
    {
        int last=clock>=FlightSeconds?TrailSamples-1:Mathf.FloorToInt(clock/TrailStep+.0001f);
        last=Mathf.Clamp(last,0,TrailSamples-1);
        for(int sample=trailCounts[index];sample<=last;sample++)
        {
            float time=Mathf.Min(sample*TrailStep,FlightSeconds);
            trailPoints[index*TrailSamples+sample]=CurvePoint(start,end,time/FlightSeconds,index);
        }
        trailCounts[index]=Mathf.Max(trailCounts[index],last+1);
    }

    private void RenderTrail(int index,float clock)
    {
        for(int sample=0;sample<trailCounts[index];sample++)
        {
            float age=clock-Mathf.Min(sample*TrailStep,FlightSeconds);
            if(age<0f||age>=TrailLifetime)continue;
            float fade=1f-age/TrailLifetime,seed=Seed(sample+index*13);
            Vector3 point=trailPoints[index*TrailSamples+sample];
            float size=(.42f+seed*.08f)*scale*MeteorSize*fade;
            Add(point,size,new Color(.28f+.4f*fade,.57f+.37f*fade,1f,fade*fade*.9f));
            if(sample%6==0)
            {
                Vector3 drift=screenRight*((seed-.5f)*age*.8f)+screenUp*(age*.35f);
                Add(point+drift,.055f*scale*MeteorSize*fade,new Color(1f,.76f,.24f,fade*.8f));
            }
        }
    }

    private static Sprite[] LoadMeteorFrames()
    {
        Texture2D atlas=Resources.Load<Texture2D>(MeteorResource);
        if(atlas==null||atlas.width<4||atlas.height<2)return null;
        if(meteorAtlas==atlas&&meteorFrames!=null&&meteorFrames[0]!=null)return meteorFrames;
        if(meteorFrames!=null)foreach(Sprite previous in meteorFrames)
            if(previous!=null){if(Application.isPlaying)Object.Destroy(previous);else Object.DestroyImmediate(previous);}
        atlas.filterMode=FilterMode.Point;atlas.wrapMode=TextureWrapMode.Clamp;
        meteorAtlas=atlas;meteorFrames=new Sprite[8];float flightWidth=0f;impactVisibleWidth=0f;
        Color32[] colors=atlas.isReadable?atlas.GetPixels32():null;
        for(int i=0;i<8;i++)
        {
            int left=Mathf.FloorToInt(i%4*atlas.width/4f),right=Mathf.FloorToInt((i%4+1)*atlas.width/4f);
            int bottom=Mathf.FloorToInt((1-i/4)*atlas.height/2f),top=Mathf.FloorToInt((2-i/4)*atlas.height/2f);
            float pivot=i<4?.21f:i==4?.25f:i==5?.18f:.15f;
            Sprite frame=Sprite.Create(atlas,new Rect(left,bottom,right-left,top-bottom),new Vector2(.5f,pivot),128f,0,SpriteMeshType.FullRect);
            frame.name="Cygnus meteor "+(i<4?"flight ":"impact ")+(i%4);frame.hideFlags=HideFlags.HideAndDontSave;meteorFrames[i]=frame;
            float width;
            if(colors==null)width=PixelMantisReaper.VisibleBody(frame).size.x;
            else
            {
                int min=right,max=left-1;
                for(int y=bottom;y<top;y++)for(int x=left;x<right;x++)if(colors[y*atlas.width+x].a>=32)
                {min=Mathf.Min(min,x);max=Mathf.Max(max,x);}
                width=Mathf.Max(1,max-min+1)/128f;
            }
            if(i<4)flightWidth=Mathf.Max(flightWidth,width);else impactVisibleWidth=Mathf.Max(impactVisibleWidth,width);
        }
        // The lower rock core is about two thirds as wide as the authored flame silhouette.
        flightBodyWidth=Mathf.Max(.01f,flightWidth*.65f);impactVisibleWidth=Mathf.Max(.01f,impactVisibleWidth);
        return meteorFrames;
    }

    private void RenderSky(float t,float life,Camera camera)
    {
        if(sky==null)return;
        Vector3 middle=Vector3.zero;for(int i=0;i<targets.Length;i++)middle+=targets[i];middle/=targets.Length;
        float alpha=.80f*(1f-Ease(life-.4f,life-.04f,t));
        if(camera!=null)
        {
            float depth=Mathf.Max(6f,Vector3.Dot(middle-camera.transform.position,camera.transform.forward));
            float half=camera.orthographic?camera.orthographicSize:depth*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            Vector3 center=camera.ViewportToWorldPoint(new Vector3(.5f,.83f,depth));
            float width=Mathf.Min(half*2f*camera.aspect*.82f,half*2f*.30f*sky.VisibleAspect);
            sky.Render(t+2f,2f,center-origin,facing,width,alpha,1f);
        }
        else sky.Render(t+2f,2f,middle-origin+screenUp*7f,facing,9f,alpha,1f);
    }

    private static Vector3 CurvePoint(Vector3 start,Vector3 end,float u,int index)
    {
        if(u<=0f)return start;if(u>=1f)return end;
        Vector3 line=end-start;float distance=line.magnitude;
        Vector3 forward=distance>.001f?line/distance:Vector3.down;
        Vector3 side=Vector3.Cross(forward,Vector3.up);if(side.sqrMagnitude<.001f)side=Vector3.right;side.Normalize();
        Vector3 bend=Vector3.Cross(side,forward).normalized;
        float seed=Seed(index+43),direction=Seed(index+7)<.5f?-1f:1f;
        Vector3 arc=(side*direction*(.6f+seed*.4f)+bend*.45f)*Mathf.Clamp(distance*.08f,.45f,1.2f);
        float progress=.12f*u+.88f*u*u;
        return Vector3.Lerp(start,end,progress)+arc*(4f*progress*(1f-progress));
    }

    private void LaunchPulse(Vector3 point,float age,int index)
    {
        if(age<0f||age>.16f)return;float u=age/.16f,fade=1f-u;
        Add(point,(.26f+u*.5f)*scale,new Color(.76f,.95f,1f,fade*.85f));
        for(int i=0;i<10;i++)
        {
            float angle=i*Mathf.PI*.2f+index,radius=(.08f+u*.48f)*scale;
            Vector3 offset=facing*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0f);
            Add(point+offset,(.035f+fade*.035f)*scale,new Color(.46f,.84f,1f,fade*.85f));
        }
    }

    private void Impact(Vector3 center,float age,int index)
    {
        float u=age/.35f,fade=1f-u,impactScale=scale*ImpactSize;
        int ring=targets.Length>16?36:56,debris=targets.Length>16?20:34;
        for(int i=0;i<ring;i++)
        {
            float angle=i*Mathf.PI*2f/ring,radius=2f*(.12f+u*1.08f)*impactScale;
            Vector3 point=center+new Vector3(Mathf.Cos(angle)*radius,.012f,Mathf.Sin(angle)*radius*.72f);
            Add(point,(.060f+fade*.040f)*impactScale,new Color(.42f,.82f,1f,fade*.94f));
        }
        for(int i=0;i<debris;i++)
        {
            float seed=Seed(i+index*13),angle=i*2.399963f+index;
            float radius=(.24f+seed*1.7f)*u*impactScale;
            Vector3 point=center+new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(u*Mathf.PI)*(.24f+seed*1.1f)*impactScale,Mathf.Sin(angle)*radius);
            Color debrisColor=i%5==0?new Color(1f,.73f,.2f,fade*.95f):i%3==0?
                new Color(.22f,.15f,.41f,fade*.9f):new Color(.36f,.76f,1f,fade*.88f);
            Add(point,new Vector3(.050f+seed*.035f,.075f+seed*.055f,.04f)*impactScale,
                debrisColor,i*39f);
        }
        if(age<.12f)
        {
            float alpha=1f-age/.12f;
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)
            {
                int edge=Mathf.Abs(x)+Mathf.Abs(y);if(edge>3)continue;
                float jitter=(Seed(x*17+y*31+index*13)-.5f)*.035f;
                Vector3 offset=facing*new Vector3(x*(.12f+age*.9f)+jitter,y*(.12f+age*.9f)-jitter,0f)*impactScale;
                Add(center+Vector3.up*(.08f*ImpactSize)+offset,(.19f-edge*.017f)*impactScale,
                    new Color(.78f+.06f*(3-edge),.94f,1f,alpha*.9f*(1f-edge*.12f)));
            }
            Add(center+Vector3.up*(.1f*ImpactSize),new Vector3(.75f,.10f,.03f)*impactScale,new Color(.94f,.98f,1f,alpha),0f);
            Add(center+Vector3.up*(.1f*ImpactSize),new Vector3(.10f,.75f,.03f)*impactScale,new Color(.94f,.98f,1f,alpha),0f);
        }
    }
    private void Add(Vector3 point,float size,Color color)=>Add(point,Vector3.one*size,color,0f);
    private void Add(Vector3 point,Vector3 size,Color color,float rotation)
    {
        if(count>=ParticleLimit||color.a<.003f)return;
        buffer[count]=new ParticleSystem.Particle{position=point-origin,startSize3D=size,startColor=color,rotation=rotation,
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(count+1)};count++;
    }
    public void FreezeTargets()
    {if(liveTarget!=null&&targets!=null)for(int i=0;i<targets.Length;i++)targets[i]=liveTarget(i);liveTarget=null;}
    public void Cancel()
    {if(this==null)return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
    private void OnDestroy()
    {
        sky?.Dispose();if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        liveTarget=null;
    }
    private static float Seed(int index)=>Mathf.Repeat(index*.61803399f+.371f,1f);
    private static float Ease(float start,float end,float time){float u=Mathf.InverseLerp(start,end,time);return u*u*(3f-2f*u);}
}
