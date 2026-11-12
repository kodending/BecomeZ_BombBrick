using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Bounded visual geometry. Every position is a function of the supplied cinematic clock.
internal sealed class PixelKingKongLeylineStage:IDisposable
{
    private const int Layer=30,StoneCount=12,ParticleLimit=1100;
    private static readonly int RevealProperty=Shader.PropertyToID("_Reveal"),PulseProperty=Shader.PropertyToID("_Pulse"),
        OpacityProperty=Shader.PropertyToID("_Opacity"),PhaseProperty=Shader.PropertyToID("_Phase"),PixelGridProperty=Shader.PropertyToID("_PixelGrid");
    private readonly GameObject root;
    private readonly Material material,shadowMaterial;
    private Material fissureMaterial;
    private readonly Mesh stoneMesh,fissureMesh;
    private readonly MeshRenderer fissures;
    private readonly MaterialPropertyBlock fissureProperties=new MaterialPropertyBlock();
    private Texture2D fissureTexture;
    private readonly Transform[] stones=new Transform[StoneCount],shadows=new Transform[StoneCount];
    private readonly ParticleSystem particles;
    private readonly ParticleSystem.Particle[] buffer=new ParticleSystem.Particle[ParticleLimit];
    private readonly Color[] stoneColors=new Color[8];
    private Color terrain;
    private bool disposed;
    private int count;

    public PixelKingKongLeylineStage(Transform parent)
    {
        root=Child("Leyline visual ground and floating stones",parent);
        material=new Material(Shader.Find("Sprites/Default")){name="Leyline square pixels and stone surfaces",hideFlags=HideFlags.DontSave};
        material.mainTexture=Texture2D.whiteTexture;
        shadowMaterial=new Material(material){name="Leyline floating stone contact shadows",hideFlags=HideFlags.DontSave};
        shadowMaterial.color=new Color(.055f,.055f,.035f,.22f);
        stoneMesh=CreateStoneMesh();
        for(int i=0;i<StoneCount;i++)
        {
            stones[i]=Stone("Floating terrain fragment "+(i+1),material).transform;
            shadows[i]=Stone("Fragment ground shadow "+(i+1),shadowMaterial).transform;
        }
        fissureMesh=CreateFissurePlane();
        GameObject ground=Child("Leyline pixel ground fissures",root.transform);
        ground.AddComponent<MeshFilter>().sharedMesh=fissureMesh;fissures=ground.AddComponent<MeshRenderer>();
        ground.transform.localPosition=Vector3.up*.014f;fissures.enabled=false;
        fissures.sortingOrder=4;fissures.shadowCastingMode=ShadowCastingMode.Off;fissures.receiveShadows=false;
        particles=Child("Hand chest and depth drifting square pixels",root.transform).AddComponent<ParticleSystem>();
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=ParticleLimit;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.simulationSpeed=0f;main.startSpeed=0f;main.startSize3D=true;
        var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
        var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sortMode=ParticleSystemSortMode.Distance;renderer.sortingOrder=85;
        root.SetActive(false);
    }

    public void Begin(string region)
    {
        if(disposed)return;
        switch(region)
        {
            case "FrozenHighlands":terrain=new Color(.49f,.65f,.70f);break;
            case "PoisonFogSwamp":terrain=new Color(.25f,.33f,.24f);break;
            case "CrimsonJungle":terrain=new Color(.43f,.26f,.18f);break;
            case "SkySanctuary":terrain=new Color(.64f,.61f,.45f);break;
            default:terrain=new Color(.43f,.40f,.23f);break;
        }
        for(int i=0;i<stoneColors.Length;i++)stoneColors[i]=Color.Lerp(terrain,Color.black,.08f+(i%3)*.15f);
        stoneMesh.colors=stoneColors;PrepareFissures();root.SetActive(true);particles.Play(false);particles.Pause(false);
    }

    public void Tick(float t,Vector3 chest,Vector3 left,Vector3 right,Vector3 feet,Quaternion view,float opacity)
    {
        if(disposed)return;count=0;
        float rising=Ease(.05f,.65f,t),roar=Ease(1.25f,1.75f,t),pulse=Pulse(t,.72f,.13f)+Pulse(t,1.10f,.15f);
        float opening=Ease(0f,.20f,t),fade=opacity*opening;
        Vector3 viewRight=view*Vector3.right,viewUp=view*Vector3.up;
        if(fissureMaterial!=null&&fissureTexture!=null)
        {
            float reveal=Ease(.08f,1.55f,t);
            fissureProperties.SetFloat(RevealProperty,reveal);
            fissureProperties.SetFloat(PulseProperty,pulse+Pulse(t,1.65f,.22f));
            fissureProperties.SetFloat(OpacityProperty,opacity);
            fissureProperties.SetFloat(PhaseProperty,t);
            fissures.SetPropertyBlock(fissureProperties);
            fissures.enabled=reveal>.001f&&opacity>.001f;
        }
        for(int i=0;i<StoneCount;i++)
        {
            float seed=Seed(i),angle=i*2.399963f+.1f*t*(i%2==0?1f:-1f);
            float radius=1.35f+seed*1.65f;
            Vector3 groundPosition=new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
            float height=(.18f+seed*.35f+roar*.20f+Mathf.Sin(t*2.8f+i*1.7f)*.055f)*rising;
            float size=(.12f+seed*.15f)*Mathf.Lerp(.20f,1f,rising)*Mathf.Sqrt(opacity);
            stones[i].localPosition=groundPosition+Vector3.up*(height+pulse*.11f);
            stones[i].localRotation=Quaternion.Euler(i*29f+t*(5f+seed*8f),i*47f+t*11f,i*13f+Mathf.Sin(t*2f+i)*8f);
            stones[i].localScale=new Vector3(size,size*(.65f+seed*.6f),size*.75f);
            shadows[i].localPosition=groundPosition+Vector3.up*.006f;
            shadows[i].localRotation=Quaternion.identity;
            shadows[i].localScale=new Vector3(size*1.1f,.006f,size*.9f);
        }
        // A compact warm core, not an icon or an opaque disk over the actor's face.
        float glow=fade*(.28f+pulse*.55f+roar*.35f);
        Add(chest,.15f+pulse*.13f,new Color(1f,.87f,.36f,glow));
        for(int ring=0;ring<2;ring++)
        {
            float radius=.17f+ring*.06f+pulse*.065f;
            for(int i=0;i<24;i++)
            {
                float angle=i*Mathf.PI*2f/24f+t*(ring==0?.8f:-.65f);
                Add(chest+(viewRight*Mathf.Cos(angle)+viewUp*Mathf.Sin(angle))*radius,.035f,
                    new Color(1f,.69f,.16f,glow*.85f));
            }
        }
        BeatRing(t-.72f,chest,viewRight,viewUp,.42f,1.15f,fade);
        BeatRing(t-1.10f,chest,viewRight,viewUp,.49f,1.45f,fade);
        BeatRing(t-1.65f,chest,viewRight,viewUp,.62f,2.25f,fade*.75f);
        Vein(chest,left,t,roar,fade,viewRight,0);
        Vein(chest,right,t,roar,fade,viewRight,1);
        Vein(chest,feet,t,roar,fade,viewRight,2);
        // Shallow dust and square motes react to each beat, keeping the face and bystanders readable.
        for(int i=0;i<96;i++)
        {
            float seed=Seed(i+31),life=Mathf.Repeat(t*(.38f+seed*.26f)+i*.618034f,1f);
            float angle=i*2.399963f+t*.16f,radius=(.7f+seed*3.5f)*(1f+pulse*.08f);
            Vector3 point=new Vector3(Mathf.Cos(angle)*radius,.025f+life*(.22f+seed*.40f)*rising,Mathf.Sin(angle)*radius);
            Color color=Color.Lerp(terrain,new Color(1f,.71f,.30f),i%5==0?.7f:.13f);
            color.a=(1f-life)*fade*(i%5==0?.60f:.22f);
            Add(point,.025f+seed*.06f,color);
        }
        GroundWave(t-1.65f,fade);
        for(int i=0;i<48;i++)
        {
            float seed=Seed(i+149),life=Mathf.Repeat(t*.65f+i*.618034f,1f);
            float angle=i*2.399963f+t*.4f;
            Vector3 point=chest+viewRight*(Mathf.Cos(angle)*(.2f+seed*.7f))+viewUp*(life*.9f-.1f);
            point+=(view*Vector3.forward)*(Mathf.Sin(angle)*.25f);
            Add(point,.024f+seed*.025f,new Color(1f,.72f,.24f,(1f-life)*(.12f+pulse*.55f+roar*.24f)*fade));
        }
        particles.SetParticles(buffer,count);
    }

    private void BeatRing(float age,Vector3 center,Vector3 right,Vector3 up,float duration,float radius,float alpha)
    {
        if(age<0f||age>duration)return;
        float u=age/duration,outward=1f-Mathf.Pow(1f-u,3f),r=.12f+radius*outward;
        for(int i=0;i<48;i++)
        {
            float angle=i*Mathf.PI*2f/48f;
            Add(center+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*r,.027f+(1f-u)*.025f,
                new Color(1f,.77f,.28f,Mathf.Pow(1f-u,1.7f)*alpha*.8f));
        }
    }
    private void Vein(Vector3 chest,Vector3 end,float t,float roar,float alpha,Vector3 side,int index)
    {
        float activation=Ease(.29f,1.55f,t),flow=Mathf.Repeat(t*.8f+index*.13f,1f);
        for(int i=0;i<22;i++)
        {
            float u=i/21f;
            Vector3 point=Vector3.Lerp(chest,end,u)+side*(Mathf.Sin(i*1.9f+index)*.024f);
            float strength=.25f+Mathf.Max(0f,1f-Mathf.Abs(u-flow)*5f)*.75f;
            Add(point,.023f+roar*.013f,new Color(1f,.69f,.16f,activation*strength*alpha*(.28f+roar*.30f)));
        }
    }
    private void GroundWave(float age,float alpha)
    {
        if(age<0f||age>.72f)return;
        float u=age/.72f,radius=.4f+u*5f;
        for(int i=0;i<100;i++)
        {
            float angle=i*Mathf.PI*2f/100f,seed=Seed(i+78);
            Vector3 point=new Vector3(Mathf.Cos(angle)*radius,.025f+Mathf.Sin(u*Mathf.PI)*seed*.16f,Mathf.Sin(angle)*radius);
            Add(point,.055f+seed*.055f,Color.Lerp(new Color(1f,.68f,.16f,(1f-u)*alpha*.68f),
                new Color(terrain.r,terrain.g,terrain.b,(1f-u)*alpha*.36f),seed));
        }
    }
    private void Add(Vector3 position,float size,Color color)
    {
        if(count>=ParticleLimit||color.a<.003f)return;
        buffer[count]=new ParticleSystem.Particle{position=position,startSize3D=Vector3.one*size,startColor=color,
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(count+1)};count++;
    }
    public void Hide(){if(disposed)return;particles.Clear(false);fissures.enabled=false;root.SetActive(false);}
    public void Dispose()
    {if(disposed)return;Hide();disposed=true;Destroy(stoneMesh);Destroy(fissureMesh);Destroy(material);Destroy(shadowMaterial);Destroy(fissureMaterial);Destroy(root);}
    private void PrepareFissures()
    {
        fissures.enabled=false;
        if(fissureTexture==null)fissureTexture=Resources.Load<Texture2D>(KingKongLeylineCinematicStage.ResourceRoot+"GroundFissures");
        if(fissureTexture==null)return;
        if(fissureMaterial==null)
        {
            Shader shader=Resources.Load<Shader>(KingKongLeylineCinematicStage.ResourceRoot+"GroundFissureSurface");
            if(shader==null)return;
            fissureMaterial=new Material(shader){name="Authored pixel ground fissure reveal",hideFlags=HideFlags.DontSave};
            fissureMaterial.mainTexture=fissureTexture;fissures.sharedMaterial=fissureMaterial;
        }
        fissureProperties.SetVector(PixelGridProperty,new Vector4(256f,256f,1f/256f,1f/256f));
        fissureProperties.SetFloat(RevealProperty,0f);fissureProperties.SetFloat(PulseProperty,0f);
        fissureProperties.SetFloat(OpacityProperty,1f);fissureProperties.SetFloat(PhaseProperty,0f);
        fissures.SetPropertyBlock(fissureProperties);
    }
    private static Mesh CreateFissurePlane()
    {
        var mesh=new Mesh{name="Authored fissure floor XZ quad",hideFlags=HideFlags.DontSave};
        const float half=5.4f;
        mesh.vertices=new[]{new Vector3(-half,0f,-half),new Vector3(-half,0f,half),new Vector3(half,0f,half),new Vector3(half,0f,-half)};
        mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
        mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();return mesh;
    }
    private GameObject Stone(string name,Material surface)
    {
        GameObject stone=Child(name,root.transform);stone.AddComponent<MeshFilter>().sharedMesh=stoneMesh;
        MeshRenderer renderer=stone.AddComponent<MeshRenderer>();renderer.sharedMaterial=surface;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortingOrder=8;return stone;
    }
    private static Mesh CreateStoneMesh()
    {
        var mesh=new Mesh{name="Irregular low-poly map fragments",hideFlags=HideFlags.DontSave};
        mesh.vertices=new[]{new Vector3(-.8f,-.4f,-.5f),new Vector3(.65f,-.5f,-.6f),new Vector3(.8f,-.4f,.55f),new Vector3(-.55f,-.5f,.65f),
            new Vector3(-.55f,.4f,-.45f),new Vector3(.5f,.65f,-.35f),new Vector3(.6f,.45f,.45f),new Vector3(-.4f,.5f,.5f)};
        mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
        mesh.RecalculateBounds();return mesh;
    }
    private static GameObject Child(string name,Transform parent)
    {var child=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};child.transform.SetParent(parent,false);return child;}
    private static float Seed(int index)=>Mathf.Repeat(index*.61803399f+.371f,1f);
    private static float Ease(float start,float end,float t){float u=Mathf.InverseLerp(start,end,t);return u*u*(3f-2f*u);}
    private static float Pulse(float t,float at,float width)=>Mathf.Pow(Mathf.Max(0f,1f-Mathf.Abs(t-at)/width),2f);
    private static void Destroy(Object value){if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
