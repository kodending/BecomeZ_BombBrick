using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Generated scenery only: the caller owns the cinematic camera, actors and clock.
public sealed class FrogCatchCinematicEnvironment:IDisposable
{
    public const string ResourceRoot="CombatCinematics/FrogCatch/Environments/";
    public const string FloorResourceRoot=ResourceRoot+"Floors/";
    public const string PanoramaResourceRoot=ResourceRoot+"Panoramas/";
    public const string ShaderResource="CombatCinematics/FrogCatch/ScenerySurface";
    private const int Layer=30,Segments=32;
    private const float WallRadius=22f,FringeRadius=18f,ArcDegrees=140f;
    private readonly GameObject root;
    private readonly Transform floor;
    private readonly Mesh floorMesh,panoramaMesh,skyMesh,fringeMesh;
    private readonly Material floorMaterial,panoramaMaterial,skyMaterial,fringeMaterial;
    private readonly Vector3[] panoramaVertices=new Vector3[(Segments+1)*2],skyVertices=new Vector3[(Segments+1)*2],fringeVertices=new Vector3[(Segments+1)*2];
    private bool disposed;

    public Texture2D Kit {get;private set;}
    public Texture2D Panorama {get;private set;}
    public Color SkyColor {get;private set;}
    public string Region {get;private set;}=string.Empty;
    public bool Active=>!disposed&&root!=null&&root.activeSelf;

    public FrogCatchCinematicEnvironment(Transform parent)
    {
        root=new GameObject("Frog generated cinematic environment"){layer=Layer,hideFlags=HideFlags.DontSave};
        root.transform.SetParent(parent,false);root.SetActive(false);
        Shader shader=Resources.Load<Shader>(ShaderResource);
        if(shader==null)return;
        floorMaterial=CreateMaterial(shader,"Frog generated overhead terrain",new Vector4(0f,0f,1f,1f));
        floorMaterial.SetFloat("_Repeat",1f);
        panoramaMaterial=CreateMaterial(shader,"Frog generated distant panorama",new Vector4(0f,0f,1f,1f));
        skyMaterial=CreateMaterial(shader,"Frog uniform upper sky",new Vector4(0f,0f,1f,1f));
        panoramaMaterial.SetFloat("_SkyBlend",1f);skyMaterial.SetFloat("_SkyBlend",2f);
        fringeMaterial=CreateMaterial(shader,"Frog overlapping near foliage",new Vector4(0f,.30f,1f,.35f));
        SetCutout(panoramaMaterial);SetCutout(fringeMaterial);
        fringeMaterial.SetFloat("_Fringe",1f);
        floorMesh=new Mesh{name="Frog finite square terrain",hideFlags=HideFlags.DontSave};
        floorMesh.vertices=new[]{new Vector3(-128f,0f,-128f),new Vector3(128f,0f,-128f),new Vector3(128f,0f,128f),new Vector3(-128f,0f,128f)};
        // The central half-tile stays under the caster; repeat seams start about 32 units away, inside the haze.
        floorMesh.uv=new[]{new Vector2(.5f,.5f),new Vector2(4.5f,.5f),new Vector2(4.5f,4.5f),new Vector2(.5f,4.5f)};
        floorMesh.triangles=new[]{0,2,1,0,3,2};floorMesh.RecalculateBounds();
        floor=MeshObject("Generated continuous battle ground",floorMesh,floorMaterial);
        panoramaMesh=WallMesh("Frog curved distant panorama",panoramaVertices);
        skyMesh=WallMesh("Frog upper sky extension",skyVertices);
        fringeMesh=WallMesh("Frog irregular near foliage",fringeVertices);
        MeshObject("Generated curved horizon",panoramaMesh,panoramaMaterial);
        MeshObject("Generated sky above the horizon",skyMesh,skyMaterial);
        MeshObject("Generated irregular near foliage",fringeMesh,fringeMaterial);
    }

    public bool Begin(string region,Vector3 away,Vector2 sourceGround)
    {
        if(disposed)return false;
        Hide();Region=ResolveRegion(region);Kit=Resources.Load<Texture2D>(FloorResourceRoot+Region);
        Panorama=Resources.Load<Texture2D>(PanoramaResourceRoot+Region);
        SkyColor=ReadSky(Panorama,DefaultSky(Region));
        if(Kit==null||Panorama==null||floorMaterial==null)return false;
        SetOpacity(1f);
        floorMaterial.mainTexture=Kit;
        Kit.wrapMode=TextureWrapMode.Repeat;
        panoramaMaterial.mainTexture=skyMaterial.mainTexture=fringeMaterial.mainTexture=Panorama;
        panoramaMaterial.SetColor("_SkyColor",SkyColor);skyMaterial.SetColor("_SkyColor",SkyColor);
        // Move the terrain origin a little, keeping its entire image and fixed texel density intact.
        floor.localPosition=new Vector3(-Mathf.Clamp(sourceGround.x,-1f,1f)*3f,-.045f,-Mathf.Clamp(sourceGround.y,-1f,1f)*2f);
        Color haze=FogTint(Region);Vector3 origin=root.transform.position;
        SetFog(floorMaterial,0f,haze,origin);SetFog(panoramaMaterial,1f,haze,origin);
        SetFog(fringeMaterial,1f,haze,origin);SetFog(skyMaterial,2f,haze,origin);
        away.y=0f;if(away.sqrMagnitude<.0001f)away=Vector3.forward;away.Normalize();
        Vector3 right=Vector3.Cross(away,Vector3.up);
        float aspect=Panorama.width/(float)Mathf.Max(1,Panorama.height);
        // Align the continuous opaque forest body, rather than the much lower dangling alpha roots.
        float height=WallRadius*ArcDegrees*Mathf.Deg2Rad/aspect,bottom=-height*.52f,top=bottom+height;
        float skyTop=Mathf.Max(50f,top+12f);
        for(int i=0;i<=Segments;i++)
        {
            float angle=Mathf.Lerp(-ArcDegrees*.5f,ArcDegrees*.5f,i/(float)Segments)*Mathf.Deg2Rad;
            Vector3 point=(-away*Mathf.Cos(angle)+right*Mathf.Sin(angle))*WallRadius;
            panoramaVertices[i*2]=point+Vector3.up*bottom;panoramaVertices[i*2+1]=point+Vector3.up*top;
            skyVertices[i*2]=point+Vector3.up*top;skyVertices[i*2+1]=point+Vector3.up*skyTop;
            Vector3 near=point*(FringeRadius/WallRadius);
            fringeVertices[i*2]=near-Vector3.up*3f;fringeVertices[i*2+1]=near+Vector3.up*3.5f;
        }
        panoramaMesh.vertices=panoramaVertices;skyMesh.vertices=skyVertices;fringeMesh.vertices=fringeVertices;
        panoramaMesh.RecalculateBounds();skyMesh.RecalculateBounds();fringeMesh.RecalculateBounds();root.SetActive(true);return true;
    }

    private static string ResolveRegion(string region)
    {
        string name=(region??string.Empty).Replace('\\','/').TrimEnd('/');
        int slash=name.LastIndexOf('/');if(slash>=0)name=name.Substring(slash+1);
        if(string.Equals(name,"PoisonFogSwamp",StringComparison.OrdinalIgnoreCase))return "PoisonFogSwamp";
        if(string.Equals(name,"CrimsonJungle",StringComparison.OrdinalIgnoreCase))return "CrimsonJungle";
        if(string.Equals(name,"FrozenHighlands",StringComparison.OrdinalIgnoreCase))return "FrozenHighlands";
        if(string.Equals(name,"SkySanctuary",StringComparison.OrdinalIgnoreCase))return "SkySanctuary";
        return "PrimordialMeadow";
    }

    private static Color ReadSky(Texture2D texture,Color fallback)
    {
        if(texture==null||!texture.isReadable)return fallback;
        Color sum=Color.clear;int count=0;
        for(int i=0;i<16;i++)
        {
            Color color=texture.GetPixel(Mathf.Min(texture.width-1,Mathf.FloorToInt((i+.5f)*texture.width/16f)),texture.height-1);
            if(color.a<.9f)continue;sum+=color;count++;
        }
        if(count==0)return fallback;
        Color result=sum/count;result.a=1f;return result;
    }

    private static Color DefaultSky(string region)
    {
        switch(region)
        {
            case "PoisonFogSwamp":return new Color(.14f,.26f,.27f,1f);
            case "CrimsonJungle":return new Color(.22f,.035f,.095f,1f);
            case "FrozenHighlands":return new Color(.015f,.04f,.19f,1f);
            case "SkySanctuary":return new Color(.11f,.47f,.84f,1f);
            default:return new Color(.19f,.43f,.52f,1f);
        }
    }

    private static Color FogTint(string region)
    {
        switch(region)
        {
            case "PoisonFogSwamp":return new Color(.21f,.31f,.25f,1f);
            case "CrimsonJungle":return new Color(.31f,.18f,.17f,1f);
            case "FrozenHighlands":return new Color(.50f,.62f,.80f,1f);
            case "SkySanctuary":return new Color(.66f,.66f,.55f,1f);
            default:return new Color(.42f,.47f,.31f,1f);
        }
    }

    private static void SetCutout(Material material)
    {
        material.SetFloat("_Cutout",1f);material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=2450;
    }

    private static void SetFog(Material material,float mode,Color tint,Vector3 origin)
    {
        material.SetFloat("_FogMode",mode);material.SetFloat("_FogStrength",.32f);
        material.SetColor("_FogTint",tint);material.SetVector("_FogOrigin",origin);
    }

    private static Material CreateMaterial(Shader shader,string name,Vector4 region)
    {
        var material=new Material(shader){name=name,hideFlags=HideFlags.DontSave};
        material.SetVector("_RegionUV",region);return material;
    }

    private Transform MeshObject(string name,Mesh mesh,Material material)
    {
        var host=new GameObject(name){layer=Layer,hideFlags=HideFlags.DontSave};host.transform.SetParent(root.transform,false);
        host.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=host.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        return host.transform;
    }

    private static Mesh WallMesh(string name,Vector3[] vertices)
    {
        var mesh=new Mesh{name=name,hideFlags=HideFlags.DontSave};var uv=new Vector2[(Segments+1)*2];var indices=new int[Segments*6];
        for(int i=0;i<=Segments;i++){uv[i*2]=new Vector2(i/(float)Segments,0f);uv[i*2+1]=new Vector2(i/(float)Segments,1f);}
        for(int i=0;i<Segments;i++)
        {
            int v=i*2,t=i*6;indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;
            indices[t+3]=v+2;indices[t+4]=v+1;indices[t+5]=v+3;
        }
        mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;return mesh;
    }

    public void SetOpacity(float opacity)
    {
        opacity=Mathf.Clamp01(opacity);
        if(floorMaterial!=null)floorMaterial.SetFloat("_SceneOpacity",opacity);
        if(panoramaMaterial!=null)panoramaMaterial.SetFloat("_SceneOpacity",opacity);
        if(skyMaterial!=null)skyMaterial.SetFloat("_SceneOpacity",opacity);
        if(fringeMaterial!=null)fringeMaterial.SetFloat("_SceneOpacity",opacity);
    }

    public void Hide()
    {if(!disposed&&root!=null)root.SetActive(false);}

    public void Dispose()
    {
        if(disposed)return;Hide();disposed=true;
        Destroy(floorMesh);Destroy(panoramaMesh);Destroy(skyMesh);Destroy(fringeMesh);
        Destroy(floorMaterial);Destroy(panoramaMaterial);Destroy(skyMaterial);Destroy(fringeMaterial);Destroy(root);Kit=Panorama=null;
    }

    private static void Destroy(Object value)
    {if(value==null)return;if(Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
}
