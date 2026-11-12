using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SkillFocusHudTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject battleHost,cameraHost,sourceModel,allyModel;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private Camera camera;
    private SpriteRenderer renderer;
    private SpriteMonsterAnimationDriver driver;
    private Type unitType;
    private object source,ally,previousFrontend;
    private float previousSpeed;
    private Texture2D testTexture;
    private Sprite testSprite;

    [SetUp]
    public void SetUp()
    {
        previousSpeed=Time.timeScale;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        battleHost=new GameObject("Inactive skill focus HUD test");battleHost.SetActive(false);
        battle=battleHost.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);
        cameraHost=new GameObject("Skill focus HUD camera");
        camera=cameraHost.AddComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=5f;
        camera.pixelRect=new Rect(0f,0f,Screen.width,Screen.height);
        camera.transform.position=Vector3.back*10f;
        director=cameraHost.AddComponent<CombatCameraDirector>();
        Set(director,"battleCamera",camera);Set(battle,"mainCamera",camera);
        Set(battle,"combatCameraDirector",director);
        director.BeginBattle();Time.timeScale=1f;
        sourceModel=SpawnRaptor();allyModel=SpawnRaptor();
        allyModel.transform.position=Vector3.right*3f;
        driver=sourceModel.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        renderer=driver.GetComponent<SpriteRenderer>();
        renderer.transform.rotation=Quaternion.identity;
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        source=Unit(sourceModel,0);ally=Unit(allyModel,0);
        SetUnit(source,"skillId","guardian_taunt");
        var enemyModel=new GameObject("HUD test opponent");
        enemyModel.transform.SetParent(allyModel.transform,false);
        enemyModel.transform.position=sourceModel.transform.position+Vector3.right*.8f;
        SetUnit(source,"target",Unit(enemyModel,1));
    }

    [TearDown]
    public void TearDown()
    {
        if(battle!=null)Call(battle,"CancelCommonSkillFocus");
        if(director!=null)director.EndBattle();
        Object.DestroyImmediate(battleHost);Object.DestroyImmediate(sourceModel);
        Object.DestroyImmediate(allyModel);Object.DestroyImmediate(cameraHost);
        if(testSprite!=null)
        {
            var cache=(IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",Static).GetValue(null);
            cache.Remove(testSprite);Object.DestroyImmediate(testSprite);
        }
        if(testTexture!=null)Object.DestroyImmediate(testTexture);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
        Time.timeScale=previousSpeed;
    }

    [Test]
    public void RealCombatSpritePlacesItsHudNearTheVisibleHeadInsteadOfTheTransparentFrameTop()
    {
        var frames=(System.Collections.Generic.IEnumerable<Sprite>)typeof(SpriteMonsterAnimationDriver)
            .GetMethod("CombatFrames",Private).Invoke(driver,null);
        Sprite frame=frames.Where(value=>value!=null).Distinct()
            .OrderByDescending(value=>value.bounds.max.y-VisibleBounds(value).max.y).First();
        renderer.sprite=frame;
        Rect fullFrame=ProjectBounds(frame.bounds,renderer);
        Assert.That(MonsterAutoBattlePrototype.TryGetSpriteBodyGuiRect(camera,renderer,out Rect body),Is.True);
        Assert.That(body.yMin-fullFrame.yMin,Is.GreaterThan(1f),
            "A real combat frame's transparent top must not keep the HUD suspended above the head.");
        Rect health=MonsterAutoBattlePrototype.HealthBarRectForVisualBounds(body);
        Assert.That(health.yMax,Is.EqualTo(body.yMin-6f).Within(.001f));
        Assert.That(health.yMax+4f,Is.LessThan(body.yMin),
            "The cooldown strip beneath health must still clear the visible head.");
        Assert.That(health.size,Is.EqualTo(new Vector2(52f,8f)));
    }

    [Test]
    public void AHighAttachedSkillRendererCannotMoveTheBodyHudAnchor()
    {
        Assert.That(TryHud(source,out Rect before),Is.True);
        var effect=new GameObject("High attached skill effect");
        effect.transform.SetParent(sourceModel.transform,false);
        effect.transform.localPosition=Vector3.up*20f;
        var effectRenderer=effect.AddComponent<SpriteRenderer>();
        effectRenderer.sprite=renderer.sprite;
        effect.transform.localScale=Vector3.one*10f;

        Assert.That(TryHud(source,out Rect after),Is.True);
        AssertRect(after,before);
        Assert.That(MonsterAutoBattlePrototype.TryGetSpriteBodyGuiRect(camera,renderer,out Rect body),Is.True);
        AssertRect(after,body);
    }

    [Test]
    public void BodyProjectionRespectsBothFlipsRotationAndMovementAndTheWholeHudFollowsTogether()
    {
        testTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        var pixels=new Color32[64*64];
        for(int y=12;y<48;y++)for(int x=8;x<24;x++)pixels[y*64+x]=new Color32(255,255,255,255);
        testTexture.SetPixels32(pixels);testTexture.Apply(false,true);
        testSprite=Sprite.Create(testTexture,new Rect(0f,0f,64f,64f),Vector2.one*.5f,32f,0,SpriteMeshType.FullRect);
        renderer.sprite=testSprite;
        renderer.transform.position=new Vector3(2f,-1f,0f);
        renderer.transform.rotation=Quaternion.Euler(0f,0f,30f);
        renderer.transform.localScale=new Vector3(1.7f,.8f,1f);
        var knownAlphaBody=new Bounds(new Vector3(-.5f,-.0625f,0f),new Vector3(.5f,1.125f,0f));
        foreach(bool flipX in new[]{false,true})foreach(bool flipY in new[]{false,true})
        {
            renderer.flipX=flipX;renderer.flipY=flipY;
            Assert.That(MonsterAutoBattlePrototype.TryGetSpriteBodyGuiRect(camera,renderer,out Rect actual),Is.True);
            AssertRect(actual,ProjectBounds(knownAlphaBody,renderer));
        }

        Assert.That(TryHud(source,out Rect first),Is.True);
        Rect firstBar=MonsterAutoBattlePrototype.HealthBarRectForVisualBounds(first);
        Rect firstIcon=MonsterAutoBattlePrototype.CombatHudIconRect(firstBar,7,8);
        renderer.transform.position+=new Vector3(1.2f,.4f,0f);
        Assert.That(TryHud(source,out Rect moved),Is.True);
        Rect movedBar=MonsterAutoBattlePrototype.HealthBarRectForVisualBounds(moved);
        Rect movedIcon=MonsterAutoBattlePrototype.CombatHudIconRect(movedBar,7,8);
        Vector2 delta=moved.position-first.position;
        Assert.That(delta.magnitude,Is.GreaterThan(1f));
        Assert.That(Vector2.Distance(movedBar.position-firstBar.position,delta),Is.LessThan(.001f));
        Assert.That(Vector2.Distance(movedIcon.position-firstIcon.position,delta),Is.LessThan(.001f));
        Assert.That(movedIcon.yMax,Is.LessThanOrEqualTo(movedBar.yMin-6f));
    }

    [TestCase("cancel")]
    [TestCase("source death")]
    [TestCase("normal completion")]
    public void FocusKeepsHealthCooldownAndBuffHudVisibleAtTheOriginalBodySizeThroughEveryExit(string exit)
    {
        Assert.That(TryHud(source,out Rect before),Is.True);
        Assert.That(TryHud(ally,out _),Is.True);
        Call(battle,"BeginLocalSkillPresentation",source);
        Assert.That(TryHud(source,out Rect focused),Is.True,
            "Health, cooldown and buff icons must remain visible throughout the skill presentation.");
        Assert.That(TryHud(ally,out _),Is.True,"Other combatants must keep their combat information.");
        Assert.That(focused.width,Is.EqualTo(before.width).Within(.001f));
        Assert.That(focused.height,Is.EqualTo(before.height).Within(.001f));
        AssertRect(focused,ProjectBounds(VisibleBounds(renderer.sprite),renderer));
        Rect health=MonsterAutoBattlePrototype.HealthBarRectForVisualBounds(focused);
        Rect icon=MonsterAutoBattlePrototype.CombatHudIconRect(health,0,1);
        Assert.That(health.yMax,Is.EqualTo(focused.yMin-6f).Within(.001f));
        Assert.That(icon.yMax,Is.LessThanOrEqualTo(health.yMin-6f));
        driver.SetSkillAnticipating(true);
        Assert.That(TryHud(source,out Rect preparing),Is.True);
        AssertRect(preparing,focused);
        driver.SetSkillAnticipating(false);
        Assert.That(TryHud(source,out Rect casting),Is.True);
        AssertRect(casting,focused);

        if(exit=="cancel")Call(battle,"CancelCommonSkillFocus");
        else if(exit=="source death")
        {
            SetUnit(source,"alive",false);
            Call(battle,"UpdateCommonSkillFocus");
            Assert.That(TryHud(source,out _),Is.False,"Dead units must keep their ordinary hidden-HUD rule.");
            SetUnit(source,"alive",true);
        }
        else
        {
            var presentations=(System.Collections.IList)typeof(MonsterAutoBattlePrototype).GetField("localSkillPresentations",Private).GetValue(battle);
            object presentation=presentations[0];
            presentation.GetType().GetField("committedAt").SetValue(presentation,Time.timeAsDouble-.2d);
            driver.InterruptCurrentAction(true);
            Call(battle,"UpdateCommonSkillFocus");
            presentation.GetType().GetField("recoveryAt").SetValue(presentation,Time.timeAsDouble-.15d);
            Call(battle,"UpdateCommonSkillFocus");
        }
        Assert.That(director.SkillFocusActive,Is.False);
        Assert.That(TryHud(source,out _),Is.True);
        Assert.That(TryHud(ally,out _),Is.True);
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    private Rect ProjectBounds(Bounds bounds,SpriteRenderer sprite)
    {
        Vector2 minimum=Vector2.one*float.MaxValue,maximum=Vector2.one*float.MinValue;
        foreach(float x in new[]{bounds.min.x,bounds.max.x})foreach(float y in new[]{bounds.min.y,bounds.max.y})
        {
            Vector3 local=new Vector3(sprite.flipX?-x:x,sprite.flipY?-y:y,0f);
            Vector3 projected=camera.WorldToScreenPoint(sprite.transform.TransformPoint(local));
            Vector2 gui=BombBrickUiLayout.ScreenToGuiPoint(projected);
            minimum=Vector2.Min(minimum,gui);maximum=Vector2.Max(maximum,gui);
        }
        return Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y);
    }

    private object Unit(GameObject model,int team)
    {
        object unit=Activator.CreateInstance(unitType,true);
        SetUnit(unit,"model",model);SetUnit(unit,"ring",new GameObject("HUD test ring"));
        ((GameObject)unitType.GetField("ring").GetValue(unit)).transform.SetParent(model.transform);
        SetUnit(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        SetUnit(unit,"alive",true);SetUnit(unit,"hp",100f);SetUnit(unit,"maxHp",100f);
        SetUnit(unit,"spriteAnimation",model.GetComponentInChildren<SpriteMonsterAnimationDriver>());
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle)).Add(unit);
        return unit;
    }

    private bool TryHud(object unit,out Rect rect)
    {
        object[] args={unit,new Rect()};
        bool result=(bool)Call(battle,"TryGetUnitWorldHudRect",args);
        rect=(Rect)args[1];return result;
    }

    private static GameObject SpawnRaptor()=>PrefabUtility.InstantiatePrefab(
        AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
    private static Bounds VisibleBounds(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper)
        .GetMethod("VisibleBody",Static).Invoke(null,new object[]{sprite});
    private static void AssertRect(Rect actual,Rect expected)
    {
        Assert.That(Vector2.Distance(actual.position,expected.position),Is.LessThan(.001f));
        Assert.That(Vector2.Distance(actual.size,expected.size),Is.LessThan(.001f));
    }
    private void SetUnit(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
}
