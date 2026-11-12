using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class FrogCatchCinematicEnvironmentTests
{
    private GameObject host;
    private FrogCatchCinematicEnvironment environment;

    [SetUp]
    public void SetUp()
    {host=new GameObject("Generated frog scenery tests");environment=new FrogCatchCinematicEnvironment(host.transform);}

    [TearDown]
    public void TearDown()
    {environment.Dispose();Object.DestroyImmediate(host);}

    [Test]
    public void SceneryOpacityDefaultsToOneIsPerInstanceAndResetsWhenAnEnvironmentIsReused()
    {
        var otherHost=new GameObject("Independent generated scenery opacity");
        var other=new FrogCatchCinematicEnvironment(otherHost.transform);
        var material=new Material(Resources.Load<Shader>(FrogCatchCinematicEnvironment.ShaderResource));
        try
        {
            Assert.That(material.GetFloat("_SceneOpacity"),Is.EqualTo(1f));
            Assert.That(environment.Begin("PrimordialMeadow",Vector3.forward,Vector2.zero),Is.True);
            Assert.That(other.Begin("PrimordialMeadow",Vector3.forward,Vector2.zero),Is.True);
            environment.SetOpacity(.25f);
            foreach(var renderer in Renderers())Assert.That(renderer.sharedMaterial.GetFloat("_SceneOpacity"),Is.EqualTo(.25f));
            foreach(var renderer in otherHost.GetComponentsInChildren<MeshRenderer>(true))
                Assert.That(renderer.sharedMaterial.GetFloat("_SceneOpacity"),Is.EqualTo(1f));
            environment.Hide();Assert.That(environment.Begin("SkySanctuary",Vector3.back,Vector2.zero),Is.True);
            foreach(var renderer in Renderers())Assert.That(renderer.sharedMaterial.GetFloat("_SceneOpacity"),Is.EqualTo(1f));
        }
        finally{Object.DestroyImmediate(material);other.Dispose();Object.DestroyImmediate(otherHost);}
    }

    [TestCase("PrimordialMeadow","PrimordialMeadow")]
    [TestCase("Environment/PoisonFogSwamp","PoisonFogSwamp")]
    [TestCase("CrimsonJungle","CrimsonJungle")]
    [TestCase("Environment\\FrozenHighlands","FrozenHighlands")]
    [TestCase("CombatCinematics/FrogCatch/Environments/SkySanctuary","SkySanctuary")]
    [TestCase("unknown","PrimordialMeadow")]
    [TestCase(null,"PrimordialMeadow")]
    public void EachRegionUsesItsIndependentFloorAndAlphaPanoramaAndUnknownRegionsUseTheMeadow(string requested,string expected)
    {
        Assert.That(environment.Active,Is.False);
        Assert.That(environment.Begin(requested,Vector3.forward,Vector2.zero),Is.True);
        Assert.That(environment.Region,Is.EqualTo(expected));Assert.That(environment.Active,Is.True);
        Texture2D generated=Resources.Load<Texture2D>(FrogCatchCinematicEnvironment.FloorResourceRoot+expected);
        Texture2D scenery=Resources.Load<Texture2D>(FrogCatchCinematicEnvironment.PanoramaResourceRoot+expected);
        Assert.That(environment.Kit,Is.SameAs(generated));Assert.That(generated.width,Is.EqualTo(generated.height));
        Assert.That(environment.Panorama,Is.SameAs(scenery));
        Assert.That(environment.Kit,Is.Not.SameAs(Resources.Load<Texture2D>("Environment/"+expected+"/BattleBackground")));
        Assert.That(environment.Kit,Is.Not.SameAs(Resources.Load<Texture2D>(FrogCatchCinematicEnvironment.ResourceRoot+expected)));
        Vector4 terrain=Mesh("Generated continuous battle ground").GetComponent<MeshRenderer>().sharedMaterial.GetVector("_RegionUV");
        Vector4 panorama=Mesh("Generated curved horizon").GetComponent<MeshRenderer>().sharedMaterial.GetVector("_RegionUV");
        Assert.That(terrain,Is.EqualTo(new Vector4(0f,0f,1f,1f)));
        Assert.That(panorama,Is.EqualTo(new Vector4(0f,0f,1f,1f)));
        Vector3[] body=Mesh("Generated curved horizon").sharedMesh.vertices;
        float groundV=-body[0].y/(body[1].y-body[0].y);int solid=0;
        for(int i=0;i<64;i++)if(scenery.GetPixel(Mathf.FloorToInt((i+.5f)*scenery.width/64f),Mathf.FloorToInt(groundV*scenery.height)).a>.9f)solid++;
        Assert.That(solid,Is.GreaterThanOrEqualTo(63),"The continuous forest body must reach the ground; dangling roots alone leave a clear-sky hole.");
        foreach(MeshRenderer renderer in Renderers())
        {
            Assert.That(renderer.sharedMaterial.mainTexture,Is.SameAs(renderer.name=="Generated continuous battle ground"?generated:scenery));
            Assert.That(renderer.sharedMaterial.renderQueue,Is.LessThan(2500));
            Assert.That(renderer.sharedMaterial.GetTag("RenderType",false),Is.EqualTo(renderer.sharedMaterial.GetFloat("_Cutout")>.5f?"TransparentCutout":"Opaque"));
            Assert.That(renderer.gameObject.layer,Is.EqualTo(30));
        }
    }

    [Test]
    public void TheLargeSquareTerrainKeepsTheOriginalTexelDensityAndMovesRepeatSeamsAwayFromTheCaster()
    {
        Assert.That(environment.Begin("PrimordialMeadow",Vector3.forward,new Vector2(.8f,-.6f)),Is.True);
        MeshFilter floor=Mesh("Generated continuous battle ground");
        Assert.That(floor.sharedMesh.bounds.size,Is.EqualTo(new Vector3(256f,0f,256f)));
        Assert.That(floor.transform.localScale,Is.EqualTo(Vector3.one));
        Assert.That(floor.transform.localPosition,Is.EqualTo(new Vector3(-2.4f,-.045f,1.2f)));
        Assert.That(floor.sharedMesh.uv,Is.EqualTo(new[]{new Vector2(.5f,.5f),new Vector2(4.5f,.5f),new Vector2(4.5f,4.5f),new Vector2(.5f,4.5f)}));
        Assert.That(floor.sharedMesh.bounds.size.x/(floor.sharedMesh.uv[1].x-floor.sharedMesh.uv[0].x),Is.EqualTo(64f));
        Assert.That(floor.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_Repeat"),Is.EqualTo(1f));
        Assert.That(environment.Kit.wrapMode,Is.EqualTo(TextureWrapMode.Repeat));
        Assert.That(floor.GetComponent<MeshRenderer>().sharedMaterial.GetVector("_RegionUV"),Is.EqualTo(new Vector4(0f,0f,1f,1f)));
        Mesh before=floor.sharedMesh;
        Assert.That(environment.Begin("SkySanctuary",Vector3.back,Vector2.one),Is.True);
        Assert.That(floor.sharedMesh,Is.SameAs(before));Assert.That(floor.sharedMesh.bounds.size,Is.EqualTo(new Vector3(256f,0f,256f)));
        Assert.That(floor.transform.localPosition,Is.EqualTo(new Vector3(-3f,-.045f,-2f)));
        Assert.That(floor.GetComponent<MeshRenderer>().sharedMaterial.GetVector("_RegionUV"),Is.EqualTo(new Vector4(0f,0f,1f,1f)));
    }

    [TestCase(0f,0f,1f)]
    [TestCase(1f,0f,0f)]
    [TestCase(1f,8f,-2f)]
    [TestCase(0f,1f,0f)]
    public void ThePanoramaIsAContinuousThirtyTwoSegmentArcBehindTheCasterWithUniformSkyAboveIt(float x,float y,float z)
    {
        Vector3 away=new Vector3(x,y,z);Assert.That(environment.Begin("PrimordialMeadow",away,Vector2.zero),Is.True);
        away.y=0f;if(away.sqrMagnitude<.0001f)away=Vector3.forward;away.Normalize();
        MeshFilter panorama=Mesh("Generated curved horizon"),sky=Mesh("Generated sky above the horizon");
        Vector3[] wall=panorama.sharedMesh.vertices,upper=sky.sharedMesh.vertices;
        float fullHeight=22f*140f*Mathf.Deg2Rad/(environment.Panorama.width/(float)environment.Panorama.height);
        float bottom=-fullHeight*.52f,top=bottom+fullHeight;
        Assert.That(wall.Length,Is.EqualTo(66));Assert.That(panorama.sharedMesh.triangles.Length,Is.EqualTo(32*6));
        Assert.That(Vector3.Distance(wall[32],-away*22f+Vector3.up*bottom),Is.LessThan(.0001f));
        for(int i=0;i<33;i++)
        {
            Assert.That(wall[i*2].y,Is.EqualTo(bottom).Within(.0001f));Assert.That(wall[i*2+1].y,Is.EqualTo(top).Within(.0001f));
            Vector3 point=wall[i*2];point.y=0f;Assert.That(point.magnitude,Is.EqualTo(22f).Within(.0001f));
            Assert.That(upper[i*2],Is.EqualTo(wall[i*2+1]));Assert.That(upper[i*2+1].y,Is.EqualTo(Mathf.Max(50f,top+12f)).Within(.0001f));
            Assert.That(Vector3.Angle(point,-away),Is.LessThanOrEqualTo(70.001f));
        }
        Assert.That((22f*140f*Mathf.Deg2Rad)/(wall[1].y-wall[0].y),Is.EqualTo(environment.Panorama.width/(float)environment.Panorama.height).Within(.0001f),"The scenery must keep its source aspect instead of stretching sideways.");
        Assert.That(panorama.GetComponent<MeshRenderer>().sharedMaterial.GetVector("_RegionUV"),Is.EqualTo(new Vector4(0f,0f,1f,1f)));
        Assert.That(panorama.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_SkyBlend"),Is.EqualTo(1f));
        Assert.That(sky.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_SkyBlend"),Is.EqualTo(2f));
        Color skyTint=sky.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_SkyColor");
        Assert.That(((Vector4)skyTint-(Vector4)environment.SkyColor).sqrMagnitude,Is.LessThan(.00000001f));
        MeshFilter fringe=Mesh("Generated irregular near foliage");
        Vector3[] leaves=fringe.sharedMesh.vertices;
        for(int i=0;i<33;i++)
        {
            Assert.That(leaves[i*2].y,Is.EqualTo(-3f));
            Assert.That(leaves[i*2+1].y,Is.EqualTo(3.5f));
            Assert.That(leaves[i*2+1].y,Is.LessThan(4f));
            Vector3 point=leaves[i*2];point.y=0f;Assert.That(point.magnitude,Is.EqualTo(18f).Within(.0001f));
        }
        Assert.That(fringe.GetComponent<MeshRenderer>().sharedMaterial.GetVector("_RegionUV"),Is.EqualTo(new Vector4(0f,.30f,1f,.35f)));
        Assert.That(fringe.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_Fringe"),Is.EqualTo(1f),"The cropped near layer must dissolve before its straight top edge.");
        foreach(MeshFilter mesh in host.GetComponentsInChildren<MeshFilter>(true))
        {
            float maximum=mesh.name=="Generated continuous battle ground"?4.5f:1f;
            foreach(Vector2 uv in mesh.sharedMesh.uv){Assert.That(uv.x,Is.InRange(0f,maximum));Assert.That(uv.y,Is.InRange(0f,maximum));}
            foreach(Vector3 point in mesh.sharedMesh.vertices)
                Assert.That(float.IsNaN(point.x)||float.IsInfinity(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.y)||float.IsNaN(point.z)||float.IsInfinity(point.z),Is.False);
        }
    }

    [Test]
    public void TheFloorAndLowFoliageShareOneSubtleRegionalHazeAtTheActualCasterOrigin()
    {
        host.transform.position=new Vector3(21000f,18000f,21000f);
        Assert.That(environment.Begin("PrimordialMeadow",Vector3.forward,Vector2.zero),Is.True);
        var ground=Mesh("Generated continuous battle ground").GetComponent<MeshRenderer>().sharedMaterial;
        Color meadow=ground.GetColor("_FogTint");
        Assert.That(ground.GetFloat("_FogMode"),Is.Zero);Assert.That(ground.GetFloat("_FogStrength"),Is.InRange(.1f,.4f));
        foreach(MeshRenderer renderer in Renderers())
        {
            Assert.That(renderer.sharedMaterial.GetColor("_FogTint"),Is.EqualTo(meadow));
            Assert.That(renderer.sharedMaterial.GetVector("_FogOrigin"),Is.EqualTo(new Vector4(21000f,18000f,21000f,0f)));
        }
        Assert.That(Mesh("Generated curved horizon").GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_FogMode"),Is.EqualTo(1f));
        Assert.That(Mesh("Generated sky above the horizon").GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_FogMode"),Is.EqualTo(2f));
        Assert.That(environment.Begin("FrozenHighlands",Vector3.forward,Vector2.zero),Is.True);
        Assert.That(ground.GetColor("_FogTint"),Is.Not.EqualTo(meadow));
        Assert.That(environment.SkyColor.a,Is.EqualTo(1f));
    }

    [Test]
    public void SkyColorUsesOnlyOpaqueTopPixelsAndFallsBackForTransparentOrUnreadableImages()
    {
        var read=typeof(FrogCatchCinematicEnvironment).GetMethod("ReadSky",BindingFlags.NonPublic|BindingFlags.Static);
        Color fallback=new Color(.2f,.4f,.6f,1f);var texture=new Texture2D(2,2);
        try
        {
            texture.SetPixels(new[]{Color.white,Color.white,Color.blue,new Color(1f,0f,0f,0f)});texture.Apply();
            Assert.That((Color)read.Invoke(null,new object[]{texture,fallback}),Is.EqualTo(Color.blue));
            texture.SetPixels(Enumerable.Repeat(Color.clear,4).ToArray());texture.Apply();
            Assert.That((Color)read.Invoke(null,new object[]{texture,fallback}),Is.EqualTo(fallback));
            texture.Apply(false,true);
            Assert.That((Color)read.Invoke(null,new object[]{texture,fallback}),Is.EqualTo(fallback));
        }
        finally{Object.DestroyImmediate(texture);}
    }

    [Test]
    public void TheShaderClipsRealNegativeSpaceAndDissolvesTheNearLayersTopInsteadOfDrawingACropLine()
    {
        var texture=new Texture2D(8,8){filterMode=FilterMode.Point};var pixels=new Color[64];
        try
        {
            for(int i=0;i<64;i++)pixels[i]=i<32?new Color(1f,0f,0f,0f):Color.white;
            texture.SetPixels(pixels);texture.Apply();Color[] clipped=RenderMask(texture,true,false);
            Assert.That(clipped[16*64+32].b,Is.GreaterThan(.9f),"Transparent lower foliage must reveal the magenta background.");
            Assert.That(clipped[48*64+32].g,Is.GreaterThan(.9f));
            Color[] opaque=RenderMask(texture,false,false);
            Assert.That(opaque[16*64+32].b,Is.LessThan(.1f),"Solid ground keeps writing depth even if a supplied texel has alpha zero.");
            for(int i=0;i<64;i++)pixels[i]=Color.white;
            texture.SetPixels(pixels);texture.Apply();Color[] leaves=RenderMask(texture,true,true);
            int mixed=Enumerable.Range(0,64).Count(x=>leaves[50*64+x].g>.9f);
            Assert.That(mixed,Is.GreaterThan(0).And.LessThan(64),"The upper foliage breaks up into stable irregular pixels.");
            Assert.That(Enumerable.Range(0,64).Count(x=>leaves[63*64+x].g>.9f),Is.Zero,"No hard rectangular crop edge may remain at the top.");
        }
        finally{Object.DestroyImmediate(texture);}
    }

    [TestCase(0f)][TestCase(1f)][TestCase(2f)]
    public void SceneryFadeWritesContinuousAlphaAcrossGroundPanoramaAndSkyWithoutDitherOrRgbDarkening(float skyBlend)
    {
        var texture=new Texture2D(2,2);texture.SetPixels(Enumerable.Repeat(Color.white,4).ToArray());texture.Apply();
        try
        {
            Color[] opaque=RenderMask(texture,false,false,skyBlend:skyBlend);
            Color[] partial=RenderMask(texture,false,false,skyBlend:skyBlend,sceneOpacity:.2f);
            Color[] hidden=RenderMask(texture,false,false,skyBlend:skyBlend,sceneOpacity:0f);
            for(int y=16;y<48;y++)for(int x=16;x<48;x++)
            {
                int index=y*64+x;
                Assert.That(opaque[index].a,Is.EqualTo(1f).Within(.01f));
                Assert.That(partial[index].a,Is.EqualTo(.2f).Within(.01f));
                Assert.That(hidden[index].a,Is.LessThan(.01f));
                Assert.That(ColorDifference(opaque[index],partial[index]),Is.LessThan(.015f),
                    "The private render texture keeps straight RGB while its alpha controls the return fade.");
            }
        }
        finally{Object.DestroyImmediate(texture);}
    }

    [Test]
    public void RepeatedTerrainActuallySamplesTheNextTileInsteadOfClampingItToTheLastPixel()
    {
        var texture=new Texture2D(2,1){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
        try
        {
            texture.SetPixels(new[]{Color.red,Color.blue});texture.Apply();Color[] pixels=RenderMask(texture,false,false,true);
            Assert.That(pixels[32*64+8].r,Is.GreaterThan(.9f));
            Assert.That(pixels[32*64+40].r,Is.GreaterThan(.9f));
            Assert.That(pixels[32*64+40].b,Is.LessThan(.1f),"Clamping an out-of-range floor UV would turn the repeated red pixel blue.");
        }
        finally{Object.DestroyImmediate(texture);}
    }

    [Test]
    public void TheSkyExtensionIsUniformMatchesCameraClearAndBlendsThePanoramasTopWithoutVerticalStripes()
    {
        var texture=new Texture2D(8,8){filterMode=FilterMode.Point};var pixels=new Color[64];
        try
        {
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)pixels[y*8+x]=x%2==0?Color.red:Color.blue;
            texture.SetPixels(pixels);texture.Apply();Color[] uniform=RenderMask(texture,false,false,skyBlend:2f);
            Color sky=uniform[32*64+32],clear=uniform[0];
            Assert.That(ColorDifference(sky,clear),Is.LessThan(.015f),"Camera clear and the extended sky must agree in this project's color space.");
            for(int x=8;x<56;x++)Assert.That(ColorDifference(uniform[32*64+x],sky),Is.LessThan(.015f));
            Color[] panorama=RenderMask(texture,false,false,skyBlend:1f);
            for(int x=8;x<56;x++)Assert.That(ColorDifference(panorama[57*64+x],sky),Is.LessThan(.025f),"The upper panorama must meet the uniform sky with no vertical texture columns.");
            Assert.That(Enumerable.Range(8,48).Count(x=>panorama[16*64+x].r>.9f),Is.GreaterThan(0).And.LessThan(48),"The main panorama artwork stays intact below the upper blend.");
        }
        finally{Object.DestroyImmediate(texture);}
    }

    [Test]
    public void ReuseHideAndDisposeKeepTheCameraAndClockUntouchedAndReleaseOnlyOwnedObjects()
    {
        var cameraHost=new GameObject("Unrelated camera");cameraHost.transform.SetParent(host.transform,false);
        Camera camera=cameraHost.AddComponent<Camera>();camera.enabled=false;camera.transform.localPosition=new Vector3(2f,7f,-5f);
        camera.orthographic=true;camera.orthographicSize=4f;float previousScale=Time.timeScale;
        try
        {
            Time.timeScale=0f;Assert.That(environment.Begin("PrimordialMeadow",Vector3.forward,Vector2.zero),Is.True);
            Texture2D kit=environment.Kit,panorama=environment.Panorama;Mesh[] meshes=host.GetComponentsInChildren<MeshFilter>(true).Select(value=>value.sharedMesh).ToArray();
            Material[] materials=Renderers().Select(value=>value.sharedMaterial).ToArray();
            environment.Hide();Assert.That(environment.Active,Is.False);
            Assert.That(environment.Begin("FrozenHighlands",Vector3.left,Vector2.one),Is.True);
            Assert.That(host.GetComponentsInChildren<MeshFilter>(true).Select(value=>value.sharedMesh).ToArray(),Is.EqualTo(meshes));
            Assert.That(Renderers().Select(value=>value.sharedMaterial).ToArray(),Is.EqualTo(materials));
            Assert.That(host.GetComponentsInChildren<Camera>(true).Length,Is.EqualTo(1));
            Assert.That(camera.transform.localPosition,Is.EqualTo(new Vector3(2f,7f,-5f)));
            Assert.That(camera.orthographic&&camera.orthographicSize==4f&&!camera.enabled&&camera.targetTexture==null,Is.True);
            Assert.That(Time.timeScale,Is.Zero);environment.Dispose();environment.Dispose();
            Assert.That(environment.Active,Is.False);Assert.That(environment.Begin("PrimordialMeadow",Vector3.forward,Vector2.zero),Is.False);
            Assert.That(meshes.All(value=>value==null),Is.True);Assert.That(materials.All(value=>value==null),Is.True);
            Assert.That(kit!=null&&kit.width>0,Is.True,"The generated resource texture is shared and must never be destroyed.");
            Assert.That(panorama!=null&&panorama.width>0,Is.True,"The independent panorama is also a shared resource.");
        }
        finally{Time.timeScale=previousScale;}
    }

    [Test]
    public void OpaqueTerrainActuallyOccludesASpriteBelowGroundButLeavesASpriteAboveItVisible()
    {
        Assert.That(environment.Begin("PrimordialMeadow",Vector3.forward,Vector2.zero),Is.True);
        RenderTexture previous=RenderTexture.active;
        var viewHost=new GameObject("Private scenery depth probe");viewHost.transform.SetParent(host.transform,false);
        Camera view=viewHost.AddComponent<Camera>();view.enabled=false;view.orthographic=true;view.orthographicSize=3f;
        view.nearClipPlane=.1f;view.farClipPlane=80f;view.cullingMask=1<<30;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.black;
        view.transform.SetPositionAndRotation(Vector3.up*8f,Quaternion.LookRotation(Vector3.down,Vector3.forward));
        var target=new RenderTexture(64,64,16,RenderTextureFormat.ARGB32);target.Create();view.targetTexture=target;
        var readable=new Texture2D(1,1,TextureFormat.RGBA32,false);var art=new Texture2D(2,2);
        art.SetPixels(new[]{Color.magenta,Color.magenta,Color.magenta,Color.magenta});art.Apply();
        Sprite sprite=Sprite.Create(art,new Rect(0,0,2,2),Vector2.one*.5f,1f);
        var actor=new GameObject("Transparent depth-test actor"){layer=30};actor.transform.SetParent(host.transform,false);
        var renderer=actor.AddComponent<SpriteRenderer>();var material=new Material(Resources.Load<Shader>("CombatCinematics/FrogCatch/StageSurface"));
        renderer.sharedMaterial=material;renderer.sprite=sprite;actor.transform.rotation=view.transform.rotation;renderer.enabled=false;
        try
        {
            Color baseline=ReadCenter(view,target,readable);actor.transform.position=Vector3.down*2f;renderer.enabled=true;
            Color hidden=ReadCenter(view,target,readable);
            Assert.That(Mathf.Abs(hidden.r-baseline.r)+Mathf.Abs(hidden.g-baseline.g)+Mathf.Abs(hidden.b-baseline.b),Is.LessThan(.015f));
            actor.transform.position=Vector3.up*2f;Color visible=ReadCenter(view,target,readable);
            Assert.That(visible.r,Is.GreaterThan(.9f));Assert.That(visible.g,Is.LessThan(.1f));Assert.That(visible.b,Is.GreaterThan(.9f));
        }
        finally
        {
            view.targetTexture=null;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);
            Object.DestroyImmediate(readable);Object.DestroyImmediate(material);Object.DestroyImmediate(sprite);Object.DestroyImmediate(art);
        }
    }

    private static Color ReadCenter(Camera camera,RenderTexture target,Texture2D readable)
    {
        camera.Render();RenderTexture.active=target;readable.ReadPixels(new Rect(32f,32f,1f,1f),0,0);readable.Apply();return readable.GetPixel(0,0);
    }
    private Color[] RenderMask(Texture2D texture,bool cutout,bool fringe,bool repeated=false,float skyBlend=0f,float sceneOpacity=1f)
    {
        RenderTexture previous=RenderTexture.active;
        var cameraHost=new GameObject("Alpha scenery probe");cameraHost.transform.SetParent(host.transform,false);
        Camera camera=cameraHost.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1f;camera.aspect=1f;
        camera.nearClipPlane=.1f;camera.farClipPlane=10f;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.magenta;
        Color skyColor=new Color(.23f,.37f,.61f,1f);
        if(skyBlend>0f)camera.backgroundColor=skyColor;
        camera.transform.localPosition=Vector3.back*3f;
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.layer=30;quad.transform.SetParent(host.transform,false);quad.transform.localScale=Vector3.one*2f;
        if(skyBlend>0f)quad.transform.localScale=Vector3.one*1.6f;
        var material=new Material(Resources.Load<Shader>(FrogCatchCinematicEnvironment.ShaderResource));
        material.mainTexture=texture;material.SetFloat("_Cutout",cutout?1f:0f);material.SetFloat("_Fringe",fringe?1f:0f);
        material.SetFloat("_SkyBlend",skyBlend);material.SetColor("_SkyColor",skyColor);material.SetFloat("_SceneOpacity",sceneOpacity);
        if(repeated){material.SetFloat("_Repeat",1f);material.SetVector("_RegionUV",new Vector4(0f,0f,2f,1f));}
        quad.GetComponent<MeshRenderer>().sharedMaterial=material;
        var target=new RenderTexture(64,64,16,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        var readable=new Texture2D(64,64,TextureFormat.RGBA32,false);
        try
        {
            camera.Render();RenderTexture.active=target;readable.ReadPixels(new Rect(0f,0f,64f,64f),0,0);readable.Apply();return readable.GetPixels();
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);
            Object.DestroyImmediate(readable);Object.DestroyImmediate(material);Object.DestroyImmediate(quad);Object.DestroyImmediate(cameraHost);
        }
    }
    private static float ColorDifference(Color first,Color second)=>Mathf.Abs(first.r-second.r)+Mathf.Abs(first.g-second.g)+Mathf.Abs(first.b-second.b);
    private MeshRenderer[] Renderers()=>host.GetComponentsInChildren<MeshRenderer>(true);
    private MeshFilter Mesh(string name)=>host.GetComponentsInChildren<MeshFilter>(true).Single(value=>value.name==name);
}
