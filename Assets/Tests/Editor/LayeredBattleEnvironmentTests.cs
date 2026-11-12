using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class LayeredBattleEnvironmentTests
{
    [Test]
    public void ImpactWeightFallsOffWithDistance()
    {
        Assert.That(LayeredBattleEnvironment.ImpactWeight(.2f,.2f,.5f),Is.EqualTo(1f));
        Assert.That(LayeredBattleEnvironment.ImpactWeight(.2f,.45f,.5f),Is.EqualTo(.25f).Within(.001f));
        Assert.That(LayeredBattleEnvironment.ImpactWeight(.2f,.8f,.5f),Is.Zero);
    }

    [Test]
    public void SpringMovesAnOffsetBackTowardRest()
    {
        float value=4f;
        float velocity=0f;
        for(int i=0;i<30;i++)
            LayeredBattleEnvironment.StepSpring(
                ref value,ref velocity,74f,10f,1f/60f);
        Assert.That(System.Math.Abs(value),Is.LessThan(1f));
    }

    [Test]
    public void MasterReflectsOutsideTheOriginalImageWithAnOwnedMeshAndKeepsItsCentralSize()
    {
        var primitive=GameObject.CreatePrimitive(PrimitiveType.Quad);
        Mesh builtin=primitive.GetComponent<MeshFilter>().sharedMesh;Vector3[] builtinVertices=builtin.vertices;
        Object.DestroyImmediate(primitive);
        GameObject host=null;Texture2D texture=null;
        try
        {
            Build(out host,out Camera camera,out texture,out LayeredBattleEnvironment environment);
            var filter=host.GetComponentInChildren<MeshFilter>();Mesh owned=filter.sharedMesh;
            Assert.That(owned,Is.Not.SameAs(builtin));Assert.That(owned.bounds.size,Is.EqualTo(new Vector3(3f,3f,0f)));
            for(int i=0;i<owned.vertexCount;i++)
            {
                Assert.That(owned.uv[i].x,Is.EqualTo(owned.vertices[i].x+.5f));
                Assert.That(owned.uv[i].y,Is.EqualTo(owned.vertices[i].y+.5f));
            }
            Vector3 lower=camera.WorldToViewportPoint(filter.transform.TransformPoint(new Vector3(-.5f,-.5f,0f)));
            Vector3 upper=camera.WorldToViewportPoint(filter.transform.TransformPoint(new Vector3(.5f,.5f,0f)));
            Assert.That(Vector2.Distance(new Vector2(lower.x,lower.y),Vector2.zero),Is.LessThan(.00001f));
            Assert.That(Vector2.Distance(new Vector2(upper.x,upper.y),Vector2.one),Is.LessThan(.00001f));
            Assert.That(texture.wrapMode,Is.EqualTo(TextureWrapMode.Mirror));
            Material material=filter.GetComponent<Renderer>().sharedMaterial;
            Assert.That(material.mainTexture,Is.SameAs(texture));
            // This non-ExecuteAlways component does not receive runtime lifecycle callbacks in EditMode.
            typeof(LayeredBattleEnvironment).GetMethod("OnDestroy",System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic).Invoke(environment,null);
            Object.DestroyImmediate(environment);
            Assert.That(owned==null,Is.True,"The explicit production cleanup path must release its runtime mesh.");
            Assert.That(material==null,Is.True);Assert.That(texture!=null,Is.True,"The caller's texture is reused, not owned.");
            Assert.That(builtin.vertices,Is.EqualTo(builtinVertices),"The built-in quad must never be edited.");
        }
        finally {if(host!=null)Object.DestroyImmediate(host);if(texture!=null)Object.DestroyImmediate(texture);}
    }

    [Test]
    public void SkillTrackingKeepsTheMasterImageStationaryWhileHitShakeStillWorks()
    {
        GameObject host=null,caster=null;Texture2D texture=null,pixels=null;Sprite sprite=null;RenderTexture target=null;
        RenderTexture previous=RenderTexture.active;
        try
        {
            Build(out host,out Camera camera,out texture,out _);
            target=new RenderTexture(128,128,24);camera.targetTexture=target;
            pixels=new Texture2D(128,128,TextureFormat.RGB24,false);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.3f,0f,.3f,1f);
            camera.allowHDR=false;camera.allowMSAA=false;
            Render(camera,target,pixels);
            AssertColor(pixels,new Vector2(.25f,.25f),Color.red);
            AssertColor(pixels,new Vector2(.75f,.25f),Color.green);
            AssertColor(pixels,new Vector2(.25f,.75f),Color.blue);
            AssertColor(pixels,new Vector2(.75f,.75f),Color.yellow);
            Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            Matrix4x4 projection=camera.projectionMatrix;
            var filter=host.GetComponentInChildren<MeshFilter>();
            var director=host.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
            caster=new GameObject("Edge-biased focus caster");caster.transform.position=camera.ViewportToWorldPoint(new Vector3(.85f,.75f,10f));
            sprite=Sprite.Create(texture,new Rect(0f,0f,8f,8f),Vector2.one*.5f,8f,0,SpriteMeshType.FullRect);
            var renderer=caster.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
            renderer.forceRenderingOff=true;
            Assert.That(director.TryBeginAnticipationPan(renderer,true),Is.True);
            for(int frame=0;frame<110;frame++)
            {
                StepFocus(director);AssertOriginalCoverage(camera,filter);
                Assert.That(camera.projectionMatrix,Is.EqualTo(projection));
                if(frame==20||frame==109)AssertOriginalPixels(camera,filter,target,pixels,texture);
            }
            Assert.That(director.AnticipationPanActive,Is.True,"Skill ownership remains available for hit and effect tracking.");
            Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
            Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
            Assert.That(director.AppliedAnticipationPanOffset,Is.EqualTo(Vector2.zero));
            director.SetShakeStrength(1f);director.ReportGroundSlam();
            typeof(CombatCameraDirector).GetMethod("ApplyShakeProjection",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(director,new object[]{Time.unscaledTime+.06f});
            Assert.That(camera.projectionMatrix,Is.Not.EqualTo(projection),
                "Tracking a skill must not suppress its hit shake when pan and zoom are disabled.");
            Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
            Assert.That(director.AppliedAnticipationPanOffset,Is.EqualTo(Vector2.zero));
            director.SetShakeStrength(0f);
            Assert.That(camera.projectionMatrix,Is.EqualTo(projection));
            director.EndAnticipationPan(renderer);
            for(int frame=0;frame<240;frame++)
            {
                StepFocus(director);AssertOriginalCoverage(camera,filter);
                if(frame==20||frame==239)AssertOriginalPixels(camera,filter,target,pixels,texture);
            }
            Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
            Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
            Assert.That(director.AnticipationPanActive,Is.False);
            Assert.That(camera.projectionMatrix,Is.EqualTo(projection));
            Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
            Assert.That(camera.fieldOfView,Is.EqualTo(fov));
        }
        finally
        {
            RenderTexture.active=previous;if(host!=null){host.GetComponent<Camera>().targetTexture=null;Object.DestroyImmediate(host);}
            if(target!=null){target.Release();Object.DestroyImmediate(target);}if(pixels!=null)Object.DestroyImmediate(pixels);
            if(caster!=null)Object.DestroyImmediate(caster);
            if(sprite!=null)
            {
                var cache=(System.Collections.IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",
                    System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
                cache.Remove(sprite);Object.DestroyImmediate(sprite);
            }
            if(texture!=null)Object.DestroyImmediate(texture);
        }
    }

    private static void Build(out GameObject host,out Camera camera,out Texture2D texture,out LayeredBattleEnvironment environment)
    {
        host=new GameObject("Master pan edge test");host.transform.position=new Vector3(100f,100f,-10f);
        camera=host.AddComponent<Camera>();camera.fieldOfView=50f;camera.aspect=1f;camera.farClipPlane=100f;
        texture=new Texture2D(8,8,TextureFormat.RGBA32,false);var colors=new Color[64];
        for(int y=0;y<8;y++)for(int x=0;x<8;x++)
            colors[y*8+x]=x==0||x==7||y==0||y==7?Color.white:x<4?(y<4?Color.red:Color.blue):(y<4?Color.green:Color.yellow);
        texture.SetPixels(colors);texture.Apply();environment=host.AddComponent<LayeredBattleEnvironment>();
        Assert.That(environment.BuildMaster(camera,texture),Is.True);
    }
    private static void Render(Camera camera,RenderTexture target,Texture2D pixels)
    {camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0f,0f,128f,128f),0,0);pixels.Apply();}
    private static void StepFocus(CombatCameraDirector director)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(CombatCameraDirector).GetMethod("ClearShakeProjection",flags).Invoke(director,null);
        typeof(CombatCameraDirector).GetMethod("UpdateAnticipationPan",flags).Invoke(director,new object[]{1f/120f});
        typeof(CombatCameraDirector).GetMethod("ApplyShakeProjection",flags).Invoke(director,new object[]{Time.unscaledTime});
    }
    private static Vector2 OriginalUv(Camera camera,MeshFilter filter,Vector2 viewport)
    {
        Vector3 world=camera.ViewportToWorldPoint(new Vector3(viewport.x,viewport.y,filter.transform.localPosition.z));
        Vector3 local=filter.transform.InverseTransformPoint(world);return new Vector2(local.x+.5f,local.y+.5f);
    }
    private static void AssertOriginalCoverage(Camera camera,MeshFilter filter)
    {
        foreach(float x in new[]{0f,1f})foreach(float y in new[]{0f,1f})
        {
            Vector2 uv=OriginalUv(camera,filter,new Vector2(x,y));
            Assert.That(uv.x,Is.InRange(-.0001f,1.0001f));Assert.That(uv.y,Is.InRange(-.0001f,1.0001f));
        }
    }
    private static void AssertOriginalPixels(Camera camera,MeshFilter filter,RenderTexture target,Texture2D pixels,Texture2D source)
    {
        Render(camera,target,pixels);
        foreach(int x in new[]{2,32,96,125})foreach(int y in new[]{2,32,96,125})
        {
            Vector2 uv=OriginalUv(camera,filter,new Vector2((x+.5f)/128f,(y+.5f)/128f));
            Assert.That(uv.x,Is.InRange(0f,1f));Assert.That(uv.y,Is.InRange(0f,1f));
            Color expected=source.GetPixel(Mathf.Min(source.width-1,Mathf.FloorToInt(uv.x*source.width)),
                Mathf.Min(source.height-1,Mathf.FloorToInt(uv.y*source.height)));
            Color actual=pixels.GetPixel(x,y);
            Assert.That(Vector3.Distance(new Vector3(actual.r,actual.g,actual.b),new Vector3(expected.r,expected.g,expected.b)),Is.LessThan(.03f));
        }
    }
    private static void AssertColor(Texture2D pixels,Vector2 point,Color expected,string message=null)
    {
        Color actual=pixels.GetPixel(Mathf.RoundToInt(point.x*127f),Mathf.RoundToInt(point.y*127f));
        Assert.That(Vector3.Distance(new Vector3(actual.r,actual.g,actual.b),new Vector3(expected.r,expected.g,expected.b)),Is.LessThan(.03f),message);
    }
}
