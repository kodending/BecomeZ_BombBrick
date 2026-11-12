using UnityEngine;

// Native particles with bounded, independently drifting positions. The combat clock drives the cloud.
public sealed class PixelMushroomGas:MonoBehaviour
{
    private const int PuffCount=48,MoteCount=24;
    private ParticleSystem cloud,motes;
    private readonly ParticleSystem.Particle[] puffs=new ParticleSystem.Particle[PuffCount];
    private readonly ParticleSystem.Particle[] specks=new ParticleSystem.Particle[MoteCount];
    private static Material mistMaterial,moteMaterial;
    private Vector3 center;
    private float radius,duration,phase;
    private Color tint;
    private ParticleSystem resonance;
    private readonly ParticleSystem.Particle[] burst=new ParticleSystem.Particle[28];
    private Vector3 resonanceCenter;
    private float resonanceStart=-100f,resonanceRadius;
    private bool rootedBreaths;

    public void RootedBreaths(){rootedBreaths=true;Pose(0f);}

    public void Resonate(Vector3 point,float age,float burstRadius)
    {
        resonanceCenter=point;resonanceStart=age;resonanceRadius=burstRadius;
        if(resonance!=null)return;
        resonance=CreateParticles("Resonant Spore Burst",transform,burst.Length,Material(false),null,3,true);
        var renderer=resonance.GetComponent<ParticleSystemRenderer>();
        var gasRenderer=cloud.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID=gasRenderer.sortingLayerID;renderer.sortingOrder=gasRenderer.sortingOrder+2;
    }

    public static PixelMushroomGas Play(Vector3 center,float radius,float duration,Color tint,SpriteRenderer reference)
    {
        var fx=new GameObject("Pixel Mushroom Gas").AddComponent<PixelMushroomGas>();
        fx.center=center;fx.radius=radius;fx.duration=duration;fx.tint=PixelSpiritConcert.Tint(reference,tint);
        fx.phase=center.x*1.7f+center.z*.9f;
        fx.transform.position=center;
        fx.cloud=CreateParticles("Drifting Gas",fx.transform,PuffCount,Material(false),reference,1,true);
        fx.motes=CreateParticles("Drifting Spores",fx.transform,MoteCount,Material(true),reference,2,true);
        fx.Pose(0f);return fx;
    }

    internal static ParticleSystem CreateParticles(string name,Transform parent,int count,Material material,
        SpriteRenderer reference,int order,bool manual)
    {
        var root=new GameObject(name);if(parent!=null)root.transform.SetParent(parent,false);
        root.AddComponent<CombatEffectEndMarker>();
        var ps=root.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=false;main.playOnAwake=false;main.maxParticles=count;
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0f;
        if(manual){main.simulationSpeed=0f;main.startSize3D=true;}
        var emission=ps.emission;emission.enabled=false;
        var shape=ps.shape;shape.enabled=false;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sortMode=ParticleSystemSortMode.Distance;
        renderer.sortingLayerID=reference==null?0:reference.sortingLayerID;
        renderer.sortingOrder=(reference==null?0:reference.sortingOrder)+order;
        ps.Play();CombatSkillClock.TrackParticle(ps);return ps;
    }

    public void Pose(float age)
    {
        float breathAge=rootedBreaths?Mathf.Repeat(age,MushroomRank3Rules.MycokingBreathInterval):age;
        float spread=1f-Mathf.Pow(1f-Mathf.Clamp01(breathAge/MushroomCombatRules.GasSpreadDuration),3f);
        float fade=Mathf.Clamp01((duration-age)/.45f);
        for(int i=0;i<PuffCount+MoteCount;i++)
        {
            bool mote=i>=PuffCount;int n=mote?i-PuffCount:i,total=mote?MoteCount:PuffCount;
            float seed=Mathf.Repeat(n*.618034f+.17f,1f),angle=n*2.399963f+phase;
            float size=mote?.04f+seed*.035f:radius*(.24f+seed*.13f);
            float travelRadius=Mathf.Max(0f,radius-size*.55f);
            float distance=Mathf.Sqrt((n+.5f)/total)*travelRadius;
            float drift=age*(.16f+seed*.12f)*(n%2==0?1f:-1f);
            Vector3 offset=new Vector3(Mathf.Cos(angle+drift),0f,Mathf.Sin(angle+drift))*distance;
            offset+=new Vector3(Mathf.Sin(age*.9f+n*1.6f),0f,Mathf.Cos(age*.7f+n*2.1f))*radius*.085f;
            offset=Vector3.ClampMagnitude(offset,travelRadius)*spread;
            float bob=(.5f+.5f*Mathf.Sin(age*1.8f+n*1.3f));
            float height=mote?.18f+bob*.4f:.08f+bob*.17f;
            Color color=Color.Lerp(tint,new Color(.9f,.88f,.69f),seed*.23f);
            color.a=(mote?.4f:.14f+seed*.06f)*fade*Mathf.Clamp01(breathAge/.12f);
            if(CombatReadabilityPresentation.RefinedSkills)color.a*=.6f;
            if(rootedBreaths)color.a*=Mathf.Clamp01((MushroomRank3Rules.MycokingBreathInterval-breathAge)/.3f);
            float breathe=1f+Mathf.Sin(age*1.9f+n*2.3f)*.13f;
            Vector3 position=center+offset+Vector3.up*height;
            float pulseAge=age-resonanceStart;
            float squeeze=pulseAge<0f?0f:pulseAge<MushroomRank3Rules.ResonanceWindup
                ?pulseAge/MushroomRank3Rules.ResonanceWindup
                :1f-Mathf.Clamp01((pulseAge-MushroomRank3Rules.ResonanceWindup)/.2f);
            Vector3 toImpact=resonanceCenter+Vector3.up*height-position;
            float local=Mathf.Clamp01(1f-toImpact.magnitude/Mathf.Max(.01f,resonanceRadius));
            position+=toImpact*squeeze*local*.9f;
            var particle=new ParticleSystem.Particle
            {
                position=position,
                startSize3D=new Vector3(size*breathe,size*(2f-breathe),size)*Mathf.Lerp(.35f,1f,spread)*PixelCombatEffect.PreviewVisualScale,
                startColor=color,startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(i+1)
            };
            if(mote)specks[n]=particle;else puffs[n]=particle;
        }
        cloud.SetParticles(puffs,PuffCount);motes.SetParticles(specks,MoteCount);
        PoseResonance(age-resonanceStart-MushroomRank3Rules.ResonanceWindup);
    }

    private void PoseResonance(float age)
    {
        if(resonance==null)return;
        if(age<0f||age>=.45f){resonance.SetParticles(burst,0);return;}
        float t=age/.45f,outward=1f-Mathf.Pow(1f-Mathf.Clamp01(age/.18f),3f);
        for(int i=0;i<burst.Length;i++)
        {
            float seed=Mathf.Repeat(i*.618034f,.999f),angle=i*2.399963f;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            float distance=i==0?0f:resonanceRadius*(.15f+seed*.85f)*outward;
            Vector3 offset=resonanceCenter+direction*distance-center;
            offset=Vector3.ClampMagnitude(offset,Mathf.Max(0f,radius-.12f));
            Color color=Color.Lerp(tint,new Color(1f,.97f,.78f),
                (i%4==0?1f:.55f)*(1f-Mathf.Clamp01(age/.18f)));
            color.a=i==0?Mathf.Pow(1f-t,6f)*.9f:Mathf.Pow(1f-t,1.4f)*.65f;
            float size=i==0?resonanceRadius*.6f:resonanceRadius*(.1f+seed*.13f);
            burst[i]=new ParticleSystem.Particle
            {
                position=center+offset+Vector3.up*(.28f+Mathf.Sin(t*Mathf.PI)*(.12f+seed*.25f)),
                startSize3D=Vector3.one*size*(1f+t*.7f)*PixelCombatEffect.PreviewVisualScale,startColor=color,
                startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(i+1)
            };
        }
        resonance.SetParticles(burst,burst.Length);
    }

    public static void Hit(Vector3 point,SpriteRenderer reference,Color color,bool strong=false
        ,bool recurringGas=false
    )
    {
        int count=strong?11:5;
        float lifetime=.3f;
        if(recurringGas&&CombatReadabilityPresentation.RefinedSkills&&!strong){count=3;lifetime=.2f;}
        var ps=CreateParticles(strong?"Spore Resonance Hit":"Mushroom Gas Tick",null,count,Material(true),reference,strong?4:2,false);
        var fade=ps.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(.8f,0f),new GradientAlphaKey(0f,1f)});fade.color=gradient;
        for(int i=0;i<count;i++)
        {
            float a=i*2.4f;
            ps.Emit(new ParticleSystem.EmitParams{position=point+new Vector3(Mathf.Cos(a)*.11f,0f,Mathf.Sin(a)*.11f),
                velocity=new Vector3(Mathf.Cos(a)*.3f,.3f+i*.04f,Mathf.Sin(a)*.3f)*(strong?2.5f:1f),
                startSize=(.045f+i*.008f)*(strong?1.4f:1f)*PixelCombatEffect.PreviewVisualScale,startLifetime=lifetime,
                startColor=strong?Color.Lerp(color,new Color(1f,.97f,.78f),.65f):color},1);
        }
        if(Application.isPlaying)Destroy(ps.gameObject,.4f);
    }

    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    internal static Material Material(bool mote)
    {
        if(mote&&moteMaterial!=null)return moteMaterial;
        if(!mote&&mistMaterial!=null)return mistMaterial;
        int width=mote?2:12;var pixels=new Color32[width*width];
        for(int y=0;y<width;y++)for(int x=0;x<width;x++)
        {
            float nx=(x+0.5f)/width*2f-1f,ny=(y+0.5f)/width*2f-1f;
            float distance=nx*nx+ny*ny;
            // Stepped alpha and an irregular silhouette keep the gas at the actors' pixel density.
            float edge=1f+Mathf.Sin(x*1.8f+y*.7f)*.14f;
            byte alpha=mote?(byte)255:distance>edge?(byte)0:distance>.68f?(byte)90:distance>.36f?(byte)180:(byte)255;
            pixels[y*width+x]=new Color32(255,255,255,alpha);
        }
        var texture=new Texture2D(width,width,TextureFormat.RGBA32,false)
            {name=mote?"Pixel Spore Fleck":"Pixel Gas Puff",filterMode=FilterMode.Point,
             wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        var material=new Material(Shader.Find("Sprites/Default")){mainTexture=texture,hideFlags=HideFlags.HideAndDontSave};
        if(mote)moteMaterial=material;else mistMaterial=material;
        return material;
    }
}
