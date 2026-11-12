using System;
using System.Collections;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SkillFocusShadeTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject host,cameraHost,body,opponent;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private Camera camera;
    private SpriteRenderer source;
    private SkillFocusSpriteMirror foreground;
    private Texture2D texture,pixels;
    private Sprite sprite;
    private RenderTexture target,previousTarget;
    private object unit,previousFrontend;
    private float previousSpeed;
    private bool previousReduced;
    private static readonly Vector2Int[] Corners={new Vector2Int(8,8),new Vector2Int(119,8),new Vector2Int(8,119),new Vector2Int(119,119)};

    [SetUp]
    public void SetUp()
    {
        previousSpeed=Time.timeScale;previousReduced=CombatFlashPresentation.Reduced;
        previousTarget=RenderTexture.active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        host=new GameObject("Inactive production skill background test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();Set(battle,"battleStarted",true);
        cameraHost=new GameObject("Skill background pixel camera");camera=cameraHost.AddComponent<Camera>();
        camera.transform.position=new Vector3(10000f,10000f,-10f);
        camera.fieldOfView=45f;camera.nearClipPlane=.1f;camera.farClipPlane=20f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.white;
        camera.allowHDR=false;camera.allowMSAA=false;
        target=new RenderTexture(128,128,24);camera.targetTexture=target;
        pixels=new Texture2D(128,128,TextureFormat.RGB24,false);
        director=cameraHost.AddComponent<CombatCameraDirector>();
        Set(director,"battleCamera",camera);Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        director.BeginBattle();Time.timeScale=1f;

        texture=new Texture2D(4,4,TextureFormat.RGBA32,false);
        var colors=new Color[16];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;
        texture.SetPixels(colors);texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0f,0f,4f,4f),Vector2.one*.5f,4f,0,SpriteMeshType.FullRect);
        body=new GameObject("Solid caster pixel sample");body.layer=31;
        body.transform.position=new Vector3(10000f,10000f,0f);
        var driver=body.AddComponent<SpriteMonsterAnimationDriver>();
        var frames=new[]{sprite};driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        source=body.GetComponent<SpriteRenderer>();source.sprite=sprite;source.color=new Color(.2f,.8f,.3f,1f);
        opponent=new GameObject("Live skill background test opponent");opponent.transform.position=body.transform.position+Vector3.right*.8f;
        Type unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        unit=Activator.CreateInstance(unitType,true);object enemy=Activator.CreateInstance(unitType,true);
        unitType.GetField("model").SetValue(unit,body);unitType.GetField("alive").SetValue(unit,true);
        unitType.GetField("spriteAnimation").SetValue(unit,driver);
        unitType.GetField("skillId").SetValue(unit,"guardian_taunt");
        unitType.GetField("model").SetValue(enemy,opponent);unitType.GetField("alive").SetValue(enemy,true);
        unitType.GetField("team").SetValue(enemy,Enum.ToObject(unitType.GetField("team").FieldType,1));
        unitType.GetField("target").SetValue(unit,enemy);
        var units=(IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle);
        units.Add(unit);units.Add(enemy);
    }

    [TearDown]
    public void TearDown()
    {
        if(battle!=null)Call(battle,"CancelCommonSkillFocus");
        if(director!=null)director.EndBattle();
        if(foreground!=null)Object.DestroyImmediate(foreground.gameObject);
        camera.targetTexture=null;RenderTexture.active=previousTarget;
        Object.DestroyImmediate(host);Object.DestroyImmediate(body);Object.DestroyImmediate(opponent);Object.DestroyImmediate(cameraHost);
        target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
        Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
        CombatFlashPresentation.Apply(previousReduced);Time.timeScale=previousSpeed;
    }

    [TestCase(false)] [TestCase(true)]
    public void CasterEmphasisNeverCreatesAShadeOrChangesTheSelectedSpeed(bool reduced)
    {
        CombatFlashPresentation.Apply(reduced);Begin();UpdatePresentation();
        AssertNoShade();Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(foreground.GetComponent<SpriteRenderer>().enabled,Is.True);
        Call(battle,"CancelCommonSkillFocus");AssertNoShade();
        Assert.That(foreground.gameObject.activeSelf,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void ForegroundPixelsKeepTheBackgroundAndCasterBrightForBothFlashSettings()
    {
        Begin();
        foreach(bool reduced in new[]{false,true})
        {CombatFlashPresentation.Apply(reduced);UpdatePresentation();AssertUnchangedBackgroundAndCaster();}
        Call(battle,"CancelCommonSkillFocus");Render();
        foreach(var corner in Corners)Assert.That(Luma(pixels.GetPixel(corner.x,corner.y)),Is.GreaterThan(.9f));
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test]
    public void CameraFramingChangesDoNotIntroduceBackgroundDimmingOrLoseTheForeground()
    {
        CombatFlashPresentation.Apply(false);Begin();UpdatePresentation();
        SkillFocusSpriteMirror sameForeground=foreground;
        camera.transform.SetPositionAndRotation(new Vector3(10002f,9998f,-12f),Quaternion.Euler(-6f,8f,0f));
        camera.fieldOfView=66f;camera.aspect=1.4f;source.transform.rotation=camera.transform.rotation;
        UpdatePresentation();Assert.That(foreground,Is.SameAs(sameForeground));
        AssertUnchangedBackgroundAndCaster();Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    private object Presentation=>((IList)typeof(MonsterAutoBattlePrototype).GetField("localSkillPresentations",Private).GetValue(battle))[0];
    private void Begin()
    {
        Call(battle,"BeginLocalSkillPresentation",unit);
        foreground=(SkillFocusSpriteMirror)Presentation.GetType().GetField("mirror").GetValue(Presentation);
        AssertNoShade();Assert.That(foreground,Is.Not.Null);
        foreground.transform.SetParent(null,true);
    }

    private void UpdatePresentation()
    {
        Presentation.GetType().GetField("committedAt").SetValue(Presentation,Time.timeAsDouble-.1d);
        Call(battle,"UpdateCommonSkillFocus");Call(foreground,"LateUpdate");
        Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Get<float>(source.GetComponent<SpriteMonsterAnimationDriver>(),"skillFocusScale"),Is.EqualTo(1f));
    }

    private void AssertUnchangedBackgroundAndCaster()
    {
        AssertNoShade();SpriteRenderer mirror=foreground.GetComponent<SpriteRenderer>();
        Assert.That(mirror.enabled,Is.True);Assert.That(mirror.sortingOrder,Is.EqualTo(30004));
        mirror.enabled=false;Render();
        var background=new float[Corners.Length];
        for(int i=0;i<Corners.Length;i++)
        {background[i]=Luma(pixels.GetPixel(Corners[i].x,Corners[i].y));Assert.That(background[i],Is.GreaterThan(.9f));}
        Vector3 caster=camera.WorldToViewportPoint(source.bounds.center);
        Assert.That(caster.x,Is.InRange(.1f,.9f));Assert.That(caster.y,Is.InRange(.1f,.9f));
        int x=Mathf.RoundToInt(caster.x*127f),y=Mathf.RoundToInt(caster.y*127f);Color bright=pixels.GetPixel(x,y);
        Assert.That(bright.g,Is.GreaterThan(bright.r+.1f));mirror.enabled=true;Render();
        for(int i=0;i<Corners.Length;i++)
            Assert.That(Luma(pixels.GetPixel(Corners[i].x,Corners[i].y))/background[i],Is.EqualTo(1f).Within(.01f));
        Assert.That(Vector3.Distance(Rgb(pixels.GetPixel(x,y)),Rgb(bright)),Is.LessThan(.03f));
    }

    private void AssertNoShade()
    {
        Assert.That(typeof(MonsterAutoBattlePrototype).GetField("skillFocusShade",Private),Is.Null);
        Assert.That(GameObject.Find("Skill Focus Shade"),Is.Null);
        foreach(Transform child in host.GetComponentsInChildren<Transform>(true))
            Assert.That(child.name,Is.Not.EqualTo("Skill Focus Shade"));
    }

    private void Render()
    {
        camera.Render();RenderTexture.active=target;
        pixels.ReadPixels(new Rect(0f,0f,128f,128f),0,0);pixels.Apply();
    }
    private static float Luma(Color value)=>value.r*.2126f+value.g*.7152f+value.b*.0722f;
    private static Vector3 Rgb(Color value)=>new Vector3(value.r,value.g,value.b);
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
    private static void Set(object value,string name,object field)=>value.GetType().GetField(name,Private).SetValue(value,field);
    private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Private).Invoke(value,args);
}
