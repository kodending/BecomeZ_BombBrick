using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SpriteGroundClipTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private const int TestLayer=31;
    private readonly List<GameObject> hosts=new List<GameObject>();
    private readonly List<Material> materials=new List<Material>();
    private SpriteMonsterBillboard billboard;
    private SpriteRenderer body;
    private Camera camera;
    private Texture2D art,readback;
    private Sprite sprite;
    private RenderTexture target;
    private Material ordinaryMaterial;

    [SetUp]
    public void Setup()
    {
        art=new Texture2D(16,16,TextureFormat.RGBA32,false);
        var pixels=new Color[16*16];for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white;
        art.SetPixels(pixels);art.Apply();art.filterMode=FilterMode.Point;
        sprite=Sprite.Create(art,new Rect(0,0,16,16),Vector2.one*.5f,16f);
        camera=Host("Ground clip isolated camera").AddComponent<Camera>();
        camera.enabled=true;camera.orthographic=true;camera.orthographicSize=1.5f;
        camera.transform.SetPositionAndRotation(new Vector3(0,0,-6),Quaternion.identity);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        camera.cullingMask=1<<TestLayer;camera.allowHDR=false;camera.allowMSAA=false;
        target=new RenderTexture(96,96,16,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        readback=new Texture2D(96,96,TextureFormat.RGBA32,false);
        ordinaryMaterial=Material("Sprites/Default");
        body=Host("Ground clipped live sprite").AddComponent<SpriteRenderer>();body.sprite=sprite;body.sharedMaterial=ordinaryMaterial;
        billboard=body.gameObject.AddComponent<SpriteMonsterBillboard>();Invoke(billboard,"Awake");
        typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
    }

    [TearDown]
    public void Cleanup()
    {
        if(camera!=null)camera.targetTexture=null;
        for(int i=hosts.Count-1;i>=0;i--)if(hosts[i]!=null)Object.DestroyImmediate(hosts[i]);hosts.Clear();
        foreach(Material material in materials)if(material!=null)Object.DestroyImmediate(material);materials.Clear();
        if(target!=null){target.Release();Object.DestroyImmediate(target);}
        Object.DestroyImmediate(readback);Object.DestroyImmediate(sprite);Object.DestroyImmediate(art);
    }

    [Test]
    public void EnablingChangingAndClearingTheFloorPreservesCurrentHitFlashAndTint()
    {
        var properties=new MaterialPropertyBlock();Color tint=new Color(.62f,.31f,.84f,.8f);
        properties.SetFloat("_HitAmount",.67f);properties.SetColor("_Color",tint);properties.SetColor("_HitColor",Color.red);
        body.SetPropertyBlock(properties);
        billboard.SetGroundClip(true,.12f);AssertClip(body,true,.12f);
        billboard.SetGroundClip(true,-.3f);AssertClip(body,true,-.3f);
        billboard.SetGroundClip(false);AssertClip(body,false);
        body.GetPropertyBlock(properties);
        Assert.That(properties.GetFloat("_HitAmount"),Is.EqualTo(.67f));
        Assert.That(Vector4.Distance(properties.GetColor("_Color"),tint),Is.LessThan(.0001f));
        Assert.That(Vector4.Distance(properties.GetColor("_HitColor"),Color.red),Is.LessThan(.0001f));
    }

    [Test]
    public void AClipCannotResetOrAccumulateAnExistingRollAndSlidePose()
    {
        billboard.SetRollPose(23f,new Vector3(.12f,.1f,0),new Vector3(.03f,-.2f,0));TickBillboard();
        Vector3 position=body.transform.position;Quaternion rotation=body.transform.rotation;
        billboard.SetGroundClip(true,.07f);TickBillboard();TickBillboard();
        Assert.That(Vector3.Distance(body.transform.position,position),Is.LessThan(.00001f));
        Assert.That(Quaternion.Angle(body.transform.rotation,rotation),Is.LessThan(.001f));
        billboard.SetGroundClip(false);TickBillboard();
        Assert.That(Vector3.Distance(body.transform.position,position),Is.LessThan(.00001f));
        Assert.That(Quaternion.Angle(body.transform.rotation,rotation),Is.LessThan(.001f));AssertClip(body,false);
    }

    [Test]
    public void ActiveClippingSurvivesAnExternalMaterialAndPropertyBlockUpdate()
    {
        billboard.SetGroundClip(true,.16f);
        body.sharedMaterial=ordinaryMaterial;
        var properties=new MaterialPropertyBlock();properties.SetFloat("_HitAmount",.81f);properties.SetColor("_Color",Color.cyan);
        body.SetPropertyBlock(properties);TickBillboard();
        Assert.That(body.sharedMaterial.HasProperty("_GroundClip"),Is.True);AssertClip(body,true,.16f);
        body.GetPropertyBlock(properties);Assert.That(properties.GetFloat("_HitAmount"),Is.EqualTo(.81f));
        Assert.That(properties.GetColor("_Color"),Is.EqualTo(Color.cyan));
    }

    [Test]
    public void DisablingAndReenablingTheBillboardClearsTheFloorWithoutRestoringAnOldFlash()
    {
        billboard.SetGroundClip(true,.4f);billboard.SetGroundFullySubmerged(true);
        var properties=new MaterialPropertyBlock();body.GetPropertyBlock(properties);
        properties.SetFloat("_HitAmount",.91f);body.SetPropertyBlock(properties);
        billboard.enabled=false;Invoke(billboard,"OnDisable");AssertClip(body,false);
        Assert.That(billboard.IsFullySubmerged,Is.False);
        body.GetPropertyBlock(properties);Assert.That(properties.GetFloat("_HitAmount"),Is.EqualTo(.91f));
        billboard.enabled=true;Invoke(billboard,"OnEnable");TickBillboard();AssertClip(body,false);
    }

    [Test]
    public void ADelayedHitFlashMaterialRestoreCannotReactivateAClearedFloor()
    {
        billboard.SetGroundClip(true,.4f);Material previousFlashMaterial=body.sharedMaterial;
        billboard.SetGroundClip(false);body.sharedMaterial=ordinaryMaterial;
        body.sharedMaterial=previousFlashMaterial;TickBillboard();AssertClip(body,false);
        Render();AssertOpaque(0,-.25f);AssertOpaque(0,.25f);
    }

    [Test]
    public void TheGpuCutsBelowTheWorldFloorAndRevealsThosePixelsAgainWhenCleared()
    {
        Render();AssertOpaque(0,-.25f);AssertOpaque(0,.25f);
        billboard.SetGroundClip(true);TickBillboard();Render();AssertTransparent(0,-.25f);AssertOpaque(0,.25f);
        billboard.SetGroundClip(true,.3f);TickBillboard();Render();AssertTransparent(0,.25f);AssertOpaque(0,.43f);
        billboard.SetGroundClip(false);TickBillboard();Render();AssertOpaque(0,-.25f);AssertOpaque(0,.25f);
    }

    [Test]
    public void AFlippedRollingSpriteStillMeetsAHorizontalWorldFloor()
    {
        body.transform.position=new Vector3(0,.12f,0);body.flipX=true;
        billboard.SetRollPose(37f,Vector3.zero,Vector3.down*.1f);TickBillboard();
        Render();AssertOpaque(-.25f,-.06f);AssertOpaque(.25f,-.06f);
        billboard.SetGroundClip(true);TickBillboard();Render();
        AssertOpaque(-.25f,.06f);AssertOpaque(.25f,.06f);
        AssertTransparent(-.25f,-.06f);AssertTransparent(.25f,-.06f);
    }

    [Test]
    public void TheForegroundMirrorCopiesTheFloorAndHonorsTheSourcesForcedVisibility()
    {
        billboard.SetGroundClip(true);TickBillboard();body.gameObject.layer=30;
        var mirror=Host("Ground clipped foreground mirror").AddComponent<SkillFocusSpriteMirror>();mirror.Follow(body,0f);
        var foreground=mirror.GetComponent<SpriteRenderer>();AssertClip(foreground,true);
        Render();AssertTransparent(0,-.25f);AssertOpaque(0,.25f);
        billboard.SetGroundClip(false);Invoke(mirror,"LateUpdate");Render();AssertOpaque(0,-.25f);
        body.forceRenderingOff=true;Invoke(mirror,"LateUpdate");
        Assert.That(!foreground.enabled||foreground.forceRenderingOff,Is.True,"A hidden caster must not remain visible through its raised preview copy.");
        Render();AssertTransparent(0,.25f);
        body.forceRenderingOff=false;Invoke(mirror,"LateUpdate");Render();AssertOpaque(0,.25f);
    }

    [Test]
    public void TheOutlineMaskUsesTheSameFloorSoAnInvisibleBodyCannotLeaveAGhostOutline()
    {
        body.sharedMaterial=Material("Hidden/BombBrick/Boss Part Outline Mask");
        Assert.That(body.sharedMaterial.HasProperty("_GroundClip"),Is.True);
        var properties=new MaterialPropertyBlock();properties.SetVector("_GroundClip",new Vector4(0,1,0,0));
        properties.SetFloat("_OutlineStrength",1f);properties.SetColor("_OutlineColor",Color.white);body.SetPropertyBlock(properties);
        Render();AssertOpaque(0,.25f);AssertTransparent(0,-.25f);
        properties.SetVector("_GroundClip",new Vector4(0,1,1,0));body.SetPropertyBlock(properties);
        Render();AssertTransparent(0,.25f);AssertTransparent(0,-.25f);
        properties.SetVector("_GroundClip",Vector4.zero);body.SetPropertyBlock(properties);Render();AssertOpaque(0,-.25f);
    }

    private GameObject Host(string name)
    {var host=new GameObject(name){layer=TestLayer};hosts.Add(host);return host;}
    private Material Material(string shaderName)
    {Shader shader=Shader.Find(shaderName);Assert.That(shader,Is.Not.Null,shaderName);var material=new Material(shader);materials.Add(material);return material;}
    private static void Invoke(object instance,string method)=>instance.GetType().GetMethod(method,Hidden).Invoke(instance,null);
    private void TickBillboard()
    {typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);Invoke(billboard,"LateUpdate");}
    private static void AssertClip(SpriteRenderer renderer,bool active,float height=0f)
    {
        var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);Vector4 clip=properties.GetVector("_GroundClip");
        Assert.That(clip.y,Is.EqualTo(active?1f:0f));if(active)Assert.That(clip.x,Is.EqualTo(height).Within(.00001f));
    }
    private void Render()
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Ground clipping needs a graphics device for pixel readback.");
        camera.Render();RenderTexture previous=RenderTexture.active;
        try{RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0);readback.Apply();}
        finally{RenderTexture.active=previous;}
    }
    private float Alpha(float x,float y)
    {
        Vector3 viewport=camera.WorldToViewportPoint(new Vector3(x,y,0));
        return readback.GetPixel(Mathf.Clamp(Mathf.FloorToInt(viewport.x*readback.width),0,readback.width-1),
            Mathf.Clamp(Mathf.FloorToInt(viewport.y*readback.height),0,readback.height-1)).a;
    }
    private void AssertOpaque(float x,float y)=>Assert.That(Alpha(x,y),Is.GreaterThan(.9f),"Expected a visible sprite at world ("+x+", "+y+").");
    private void AssertTransparent(float x,float y)=>Assert.That(Alpha(x,y),Is.LessThan(.02f),"Pixels below the floor or on a hidden copy must not be drawn at world ("+x+", "+y+").");
}
