using System;
using UnityEngine;

// Four overlapping lobes travel together from the cap; the battle owns the clock and lifetime.
public sealed class PixelMushroomDeathBloom:MonoBehaviour
{
    public const float TailDuration=2.4f;
    private const int CloudCount=64,JetCount=32,SporeCount=8,Lobes=4;
    private const float NativePrewarm=1.2f;
    private static Material smokeMaterial;
    private ParticleSystem cloud,jet,spores;
    private readonly ParticleSystem.Particle[] clouds=new ParticleSystem.Particle[CloudCount];
    private readonly ParticleSystem.Particle[] jets=new ParticleSystem.Particle[JetCount];
    private readonly ParticleSystem.Particle[] specks=new ParticleSystem.Particle[SporeCount];
    private MaterialPropertyBlock clip;
    private SpriteRenderer reference;
    private Camera presentationCamera;
    private Bounds? bounds;
    private Vector3 ground;
    private Color tint;
    private int sortingOrder;
    private float radius,duration,stoppedAt=-1f,opacity=1f,pressure=1f,mouthHeight=2.3f;
    public float Elapsed{get;private set;}
    public float RemainingLifetime=>Mathf.Max(0f,EmissionEnd+TailDuration-Elapsed);
    private float EmissionEnd=>stoppedAt<0f?duration:Mathf.Min(duration,stoppedAt);

    public static PixelMushroomDeathBloom Play(Vector3 ground,float radius,float duration,SpriteRenderer reference,Color tint,Bounds? combatBounds=null)
    {
        Material smoke=SmokeMaterial();
        var effect=new GameObject("Last Bloom connected billowing smoke").AddComponent<PixelMushroomDeathBloom>();
        effect.clip=new MaterialPropertyBlock();effect.ground=ground;effect.radius=Mathf.Max(.1f,radius);
        effect.duration=Mathf.Max(.1f,duration);effect.reference=reference;effect.tint=tint;effect.bounds=combatBounds;
        effect.sortingOrder=reference==null?0:reference.sortingOrder;
        effect.transform.position=ground;effect.gameObject.layer=reference==null?0:reference.gameObject.layer;
        effect.cloud=effect.Make("Connected low poison billows",CloudCount,smoke,1);
        effect.jet=effect.Make("Pressurized soft spore plumes",JetCount,smoke,2);
        effect.spores=effect.Make("Green and violet supporting spores",SporeCount,PixelMushroomGas.Material(true),3);
        effect.Advance(0f);return effect;
    }

    private static Material SmokeMaterial()
    {
        if(smokeMaterial!=null)return smokeMaterial;
        var shader=Shader.Find("Hidden/PixelMushroomDeathBloomSmoke");
        if(shader==null)throw new InvalidOperationException("Missing Last Bloom procedural volume shader.");
        smokeMaterial=new Material(shader){name="Last Bloom procedural volume",hideFlags=HideFlags.HideAndDontSave};
        const int width=32;var values=new Color32[width*width*width];
        for(int i=0;i<values.Length;i++)
            values[i]=new Color32((byte)(Hash(i*4)*255f),(byte)(Hash(i*4+1)*255f),(byte)(Hash(i*4+2)*255f),(byte)(Hash(i*4+3)*255f));
        var density=new Texture3D(width,width,width,TextureFormat.RGBA32,false)
            {name="Procedural smoke density field",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Repeat,hideFlags=HideFlags.HideAndDontSave};
        density.SetPixels32(values);density.Apply(false,true);smokeMaterial.SetTexture("_DensityNoise",density);
        return smokeMaterial;
    }

    private ParticleSystem Make(string name,int count,Material material,int order)
    {
        var system=PixelMushroomGas.CreateParticles(name,transform,count,material,reference,order,true);system.gameObject.layer=gameObject.layer;
        var sheet=system.textureSheetAnimation;sheet.enabled=false;
        clip.SetFloat("_ClipBounds",bounds.HasValue?1f:0f);
        if(bounds.HasValue)
        {
            Bounds area=bounds.Value;clip.SetVector("_GroundBounds",new Vector4(area.min.x,area.max.x,area.min.z,area.max.z));
        }
        system.GetComponent<Renderer>().SetPropertyBlock(clip);return system;
    }

    public void SetPresentation(Camera camera,float alpha,float burstPressure=1f)
    {presentationCamera=camera;opacity=Mathf.Clamp01(alpha);pressure=Mathf.Max(.1f,burstPressure);}
    public void SetGround(Vector3 point){ground=point;transform.position=point;}

    private static float Hash(int value)
    {
        unchecked
        {
            uint n=(uint)value+0x9e3779b9u;n=(n^(n>>16))*0x85ebca6bu;n=(n^(n>>13))*0xc2b2ae35u;
            return ((n^(n>>16))&0x00ffffffu)/16777216f;
        }
    }
    private static float Signed(int seed)=>Hash(seed)*2f-1f;
    private static float Smooth(float t)=>Mathf.SmoothStep(0f,1f,Mathf.Clamp01(t));
    private static Vector3 Roll(int seed,float age)
    {
        float phase=Hash(seed)*6.28318f;
        return new Vector3(Mathf.Sin(age*1.4f+phase)+Mathf.Sin(age*.6f+Hash(seed+7)*6.28318f)*.35f,
            Mathf.Sin(age*1.8f+Hash(seed+3)*6.28318f),Mathf.Sin(age*.95f+Hash(seed+11)*6.28318f));
    }
    private bool Inside(Vector3 point)
    {
        if(presentationCamera!=null)return true;
        Vector3 delta=point-ground;delta.y=0f;
        return delta.sqrMagnitude<=radius*radius&&(!bounds.HasValue||point.x>=bounds.Value.min.x&&point.x<=bounds.Value.max.x&&
            point.z>=bounds.Value.min.z&&point.z<=bounds.Value.max.z);
    }
    private Vector3 FlowVelocity(int packet)
    {
        float reach=radius*.85f;
        float minX=-reach,maxX=reach,minZ=-reach,maxZ=reach;
        if(bounds.HasValue)
        {
            Bounds area=bounds.Value;minX=Mathf.Max(minX,area.min.x-ground.x+.15f);maxX=Mathf.Min(maxX,area.max.x-ground.x-.15f);
            minZ=Mathf.Max(minZ,area.min.z-ground.z+.15f);maxZ=Mathf.Min(maxZ,area.max.z-ground.z-.15f);
        }
        return new Vector3(Mathf.Lerp(minX,maxX,Hash(packet*31+17)),0f,Mathf.Lerp(minZ,maxZ,Hash(packet*47+29)));
    }
    private static ParticleSystem.Particle Particle(Vector3 point,Vector3 size,Color color,float life,float age,int seed,float rotation)
        =>new ParticleSystem.Particle{position=point,startSize3D=size,startColor=color,startLifetime=life,
            remainingLifetime=Mathf.Max(.0001f,life-age),rotation=rotation,randomSeed=(uint)(seed+1)};

    private int Billows(ParticleSystem.Particle[] buffer,float clock,float prewarm,Vector3 mouth,Vector3 toward,Vector3 right,Vector3 up,bool plume)
    {
        const float spacing=.145f;int newest=Mathf.FloorToInt(clock/spacing),count=0;
        bool wall=presentationCamera!=null;
        for(int back=0;back<buffer.Length/Lobes;back++)
        {
            int packet=newest-back;if(packet<0)continue;
            int key=packet*73+(plume?1009:31);
            float birth=packet*spacing+Hash(key)*spacing*.35f;
            // Births, stopping and durations all use the public clock, including the internal native prewarm.
            float age=clock-birth,life=plume?1.10f:2f+Hash(key+3)*.20f;
            if(age<0f||age>=life||birth-prewarm>=EmissionEnd)continue;
            float travel=1f-Mathf.Exp(-age*(plume?2.7f:1.5f));
            float diameter=plume?1.05f+Hash(key+4)*.55f:Mathf.Clamp(radius*(.35f+Hash(key+4)*.10f),2.3f,5f);
            Vector3 center;
            if(wall)
            {
                Vector3 direction=(toward+right*Signed(key+5)*.40f+up*Signed(key+6)*.14f).normalized;
                float distance=(2.2f+Hash(key+7)*2.4f)*pressure*travel;
                float cameraDistance=Vector3.Distance(mouth,presentationCamera.transform.position);
                distance=Mathf.Min(distance,cameraDistance*.86f);
                center=mouth+direction*distance;diameter=(plume?1.7f:2.3f)+Hash(key+4)*.75f;diameter*=1f+pressure*.40f;
            }
            else if(plume)
                center=mouth+new Vector3(Signed(key+5),0f,Signed(key+6))*travel*.45f;
            else
                center=ground+FlowVelocity(packet)*travel+Vector3.up*1.40f;
            float grow=Mathf.Lerp(.45f,1f,Smooth(age/.48f));
            for(int lobe=0;lobe<Lobes;lobe++)
            {
                int seed=key+lobe*181;Vector3 roll=Roll(seed,age);
                Vector3 origin=wall||plume?mouth:ground+Vector3.up*1.40f;
                float along=.22f+lobe*.26f+Signed(seed+37)*.035f;
                Vector3 point=Vector3.Lerp(origin,center,along)+new Vector3(roll.x*diameter*.10f,roll.y*(wall?.10f:.18f),roll.z*diameter*.10f);
                if(wall)point+=up*roll.y*diameter*.07f;
                if(!Inside(point))continue;
                float size=diameter*(.82f+Hash(seed+13)*.27f)*grow;
                if(wall&&presentationCamera.orthographic)
                {
                    float start=Vector3.Dot(mouth-presentationCamera.transform.position,presentationCamera.transform.forward);
                    float depth=Vector3.Dot(point-presentationCamera.transform.position,presentationCamera.transform.forward);
                    size*=Mathf.Lerp(1f,3f,Smooth((start-depth)/Mathf.Max(1f,start*.80f)));
                }
                float tall=wall?size*(.82f+Hash(seed+19)*.15f):plume?Mathf.Min(1.7f,size*.92f):Mathf.Clamp(diameter*(.82f+Hash(seed+13)*.27f)*.95f,2.2f,3.0f)*grow;
                float fade=Smooth(age/.10f)*Smooth((life-age)/.32f);
                Color color=Color.Lerp(new Color(.97f,1f,.95f),new Color(1f,.94f,1f),Hash(seed+23));
                color=Color.Lerp(color,tint,.018f);color.a=fade*(wall?.70f:plume?.14f:.46f+Hash(seed+47)*.06f)*opacity;
                float turn=wall?Signed(seed+29)*24f+roll.y*13f:Signed(seed+29)*2f+roll.y;
                buffer[count++]=Particle(point,new Vector3(size,tall,size),color,life,age,packet*Lobes+lobe+(plume?2000:0),turn);
            }
        }
        return count;
    }

    public void Advance(float absoluteAge)
    {
        if(this==null)return;Elapsed=Mathf.Max(0f,absoluteAge);
        if(cloud.isStopped)cloud.Play(false);if(jet.isStopped)jet.Play(false);if(spores.isStopped)spores.Play(false);
        Camera camera=presentationCamera!=null?presentationCamera:Camera.main;
        Vector3 up=camera==null?Vector3.up:camera.transform.up,right=camera==null?Vector3.right:camera.transform.right;
        if(reference!=null&&reference.sprite!=null)
            mouthHeight=PixelMantisReaper.VisibleBody(reference.sprite).size.y*Mathf.Abs(reference.transform.lossyScale.y);
        bool wall=presentationCamera!=null;
        if(reference!=null)sortingOrder=reference.sortingOrder;
        cloud.GetComponent<Renderer>().sortingOrder=sortingOrder+(wall?5:-2);
        jet.GetComponent<Renderer>().sortingOrder=sortingOrder+(wall?6:-1);
        spores.GetComponent<Renderer>().sortingOrder=sortingOrder+1;
        clip.SetFloat("_SmokeTime",Elapsed);cloud.GetComponent<Renderer>().SetPropertyBlock(clip);jet.GetComponent<Renderer>().SetPropertyBlock(clip);
        Vector3 mouth=wall?ground+up*Mathf.Max(.6f,mouthHeight*.43f):ground+Vector3.up*Mathf.Clamp(mouthHeight*.43f,.65f,1.1f);
        Vector3 toward=camera==null?Vector3.back:(camera.transform.position-mouth).normalized;
        float prewarm=wall?0f:NativePrewarm,clock=Elapsed+prewarm;
        cloud.SetParticles(clouds,Billows(clouds,clock,prewarm,mouth,toward,right,up,false));
        jet.SetParticles(jets,Billows(jets,clock,prewarm,mouth,toward,right,up,true));
        int count=0;
        for(int i=0;i<SporeCount;i++)
        {
            const float life=1.6f;float first=-i*.1f,birth=first+Mathf.Floor((Elapsed-first)/life)*life;
            float age=Elapsed-birth;if(birth>=EmissionEnd)continue;
            Vector3 roll=Roll(i*97+401,Elapsed*.45f);
            Vector3 point=ground+new Vector3(Signed(i+101)*.7f,.43f,Signed(i+113)*.7f)+new Vector3(roll.x*.12f,roll.y*.10f,roll.z*.12f);
            if(!Inside(point))continue;
            Color color=i%2==0?new Color(.72f,1f,.53f):new Color(.94f,.59f,1f);
            color.a=(.35f+.25f*Mathf.Sin(age/life*Mathf.PI))*Smooth((life-age)/.2f)*opacity;
            float size=.025f+Hash(i+109)*.022f;
            specks[count++]=Particle(point,Vector3.one*size,color,life,age,i+4000,0f);
        }
        spores.SetParticles(specks,count);
    }

    public void StopEmission(){if(stoppedAt<0f)stoppedAt=Elapsed;}
    public void Cancel()
    {
        if(this==null||CombatEffectEndFade.IsFading(this))return;
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
