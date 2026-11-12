using System;
using System.Collections;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SkillAnticipationCameraTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject host,firstHost,secondHost,thirdHost,battleHost,enemyHost;
    private Camera camera;
    private CombatCameraDirector director;
    private SpriteRenderer first,second;
    private Texture2D texture;
    private Sprite sprite;
    private Vector3 home;
    private Quaternion rotation;
    private float fov,previousSpeed;
    private object previousFrontend;

    [SetUp]
    public void SetUp()
    {
        previousSpeed=Time.timeScale;Time.timeScale=2f;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        host=new GameObject("Preparation pan camera");camera=host.AddComponent<Camera>();
        host.transform.SetPositionAndRotation(new Vector3(0f,8f,-10f),Quaternion.Euler(35f,0f,0f));
        camera.fieldOfView=40f;camera.aspect=1.6f;
        director=host.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);
        director.SetShakeStrength(1f);director.BeginBattle();
        home=host.transform.position;rotation=host.transform.rotation;fov=camera.fieldOfView;
        texture=new Texture2D(8,8,TextureFormat.RGBA32,false);
        var pixels=new Color32[64];
        for(int y=2;y<6;y++)for(int x=1;x<5;x++)pixels[y*8+x]=new Color32(255,255,255,255);
        texture.SetPixels32(pixels);texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0f,0f,8f,8f),Vector2.one*.5f,4f,0,SpriteMeshType.FullRect);
        firstHost=new GameObject("First allied caster");first=Renderer(firstHost,3f,1f);
        first.transform.localScale=Vector3.one*1.5f;first.flipX=true;
        secondHost=new GameObject("Second caster");second=Renderer(secondHost,-3f,-1f);
    }

    [TearDown]
    public void TearDown()
    {
        director.EndBattle();
        if(battleHost!=null)Object.DestroyImmediate(battleHost);
        if(enemyHost!=null)Object.DestroyImmediate(enemyHost);
        if(thirdHost!=null)Object.DestroyImmediate(thirdHost);
        Object.DestroyImmediate(firstHost);Object.DestroyImmediate(secondHost);Object.DestroyImmediate(host);
        var cache=(IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",Static).GetValue(null);
        cache.Remove(sprite);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
        Time.timeScale=previousSpeed;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AlliedSkillOwnershipNeverMovesOrZoomsTheCamera(bool allied)
    {
        float before=CenterError(first);
        Matrix4x4 projection=camera.projectionMatrix;
        Assert.That(director.TryBeginAnticipationPan(first,allied),Is.EqualTo(allied));
        Step(1f/120f);
        Step(.8f);
        Assert.That(CenterError(first),Is.EqualTo(before).Within(.00001f));
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
        Assert.That(director.AppliedAnticipationPanOffset,Is.EqualTo(Vector2.zero));
        Assert.That(camera.transform.position,Is.EqualTo(home));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Matrix4x4 raw=BaseProjection();
        for(int i=0;i<16;i++)Assert.That(raw[i],Is.EqualTo(projection[i]).Within(.00001f));
        Assert.That(Time.timeScale,Is.EqualTo(2f));
    }

    [Test]
    public void OverlappingCasterWaitsForImpactOwnershipWithoutMovingTheCamera()
    {
        Assert.That(director.TryBeginAnticipationPan(first,true),Is.True);Step(.25f);
        Assert.That(director.TryBeginAnticipationPan(second,true),Is.False);
        director.EndAnticipationPan(second);Assert.That(director.AnticipationPanActive,Is.True);
        Step(.25f);Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(first));
        director.EndAnticipationPan(first);Step(.05f);
        Vector3 offset=director.AnticipationPanOffset;
        Vector2 applied=director.AppliedAnticipationPanOffset;
        float zoom=director.AppliedAnticipationZoom;
        Assert.That(Get<bool>(director,"anticipationPanReturning"),Is.False);
        Assert.That(director.TryBeginAnticipationPan(second,true),Is.True);
        Assert.That(director.AnticipationPanOffset,Is.EqualTo(offset));
        Assert.That(director.AppliedAnticipationPanOffset,Is.EqualTo(applied));
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(zoom),"Accepting the next cast must preserve the currently drawn framing.");
        Assert.That(Get<bool>(director,"anticipationPanReturning"),Is.False);
        director.EndAnticipationPan(first);
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(second),"A late end from the old owner must not release the new owner.");
        Step(1f/120f);
        Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
        Step(.9f);Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
        Assert.That(camera.transform.position,Is.EqualTo(home));
    }

    [Test]
    public void DefeatedCasterReleasesItsOwnershipOnTheNextCameraUpdate()
    {
        bool alive=true;
        Assert.That(director.TryBeginAnticipationPan(first,true,()=>alive),Is.True);Step(.3f);
        alive=false;StepFrame(1f/120f);
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.Null);
        Assert.That(Get<bool>(director,"anticipationPanReturning"),Is.False);
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
        Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
        Step(2f);Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
        Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void FreshAllyCanClaimTheCameraOnTheSameFrameItsPreviousCasterDies()
    {
        bool alive=true;
        Assert.That(director.TryBeginAnticipationPan(first,true,()=>alive),Is.True);Step(.3f);
        Vector3 offset=director.AnticipationPanOffset;
        Vector2 applied=director.AppliedAnticipationPanOffset;
        float zoom=director.AppliedAnticipationZoom;
        alive=false;
        Assert.That(director.TryBeginAnticipationPan(second,true),Is.True,
            "A fresh cast must not be lost while waiting for the defeated caster's later cleanup.");
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(second));
        Assert.That(director.AnticipationPanOffset,Is.EqualTo(offset));
        Assert.That(director.AppliedAnticipationPanOffset,Is.EqualTo(applied));
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(zoom));
        director.EndAnticipationPan(first);StepFrame(1f/120f);
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(second),
            "Neither the old end callback nor its failed liveness predicate may release the replacement caster.");
        Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void ADeveloperPositionedCasterDoesNotPullTheCameraTowardTheCenter()
    {
        first.transform.position=camera.ViewportToWorldPoint(new Vector3(.53f,.48f,10f))-
            first.transform.TransformVector(new Vector3(.25f,0f,0f));
        float before=CenterError(first);
        Assert.That(director.TryBeginAnticipationPan(first,true),Is.True);Step(.9f);
        Assert.That(CenterError(first),Is.EqualTo(before).Within(.00001f));
        AssertStationaryFraming();
    }

    [Test]
    public void SkillFramingStaysUnchangedAtThirtySixtyAndOneHundredTwentyFrames()
    {
        foreach(int fps in new[]{30,60,120})
        {
            director.CancelAnticipationPan(true);
            Assert.That(director.TryBeginAnticipationPan(first,true),Is.True);
            for(int frame=0;frame<fps;frame++)StepFrame(1f/fps);
            Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f),"Frame rate "+fps);
            Assert.That(director.AppliedAnticipationPanOffset,Is.EqualTo(Vector2.zero));
        }
    }

    [Test]
    public void HighFrameRateCleanupReleasesImpactOwnershipForTheNextAlly()
    {
        Assert.That(director.TryBeginAnticipationPan(first,true),Is.True);Step(.9f);
        director.EndAnticipationPan(first);
        for(int frame=0;frame<2100;frame++)StepFrame(.001f);
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
        Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
        Assert.That(Get<bool>(director,"anticipationPanReturning"),Is.False,
            "Returning at 1000 FPS must not stall just above one and leave a permanent return state.");
        Assert.That(director.TryBeginAnticipationPan(second,true),Is.True);
    }

    [TestCase("cancel")]
    [TestCase("inactive")]
    [TestCase("destroyed")]
    public void InvalidatedPreparationLeavesTheCameraAtItsHomePosition(string reason)
    {
        director.TryBeginAnticipationPan(first,true);Step(.3f);
        if(reason=="cancel")director.EndAnticipationPan(first);
        else if(reason=="inactive")firstHost.SetActive(false);
        else Object.DestroyImmediate(firstHost);
        Step(.05f);
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
        Step(2f);Assert.That(camera.transform.position,Is.EqualTo(home));
        Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
    }

    [TestCase("EndBattle")]
    [TestCase("OnDisable")]
    public void BattleAndSceneCleanupRestoreHomeImmediatelyBeforeTheNextWave(string exit)
    {
        director.TryBeginAnticipationPan(first,true);Step(.3f);
        if(exit=="EndBattle")director.EndBattle();else Call(director,"OnDisable");
        Assert.That(camera.transform.position,Is.EqualTo(home));Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
        director.BeginBattle();Assert.That(director.TryBeginAnticipationPan(second,true),Is.True);
        Step(.3f);director.EndBattle();Assert.That(camera.transform.position,Is.EqualTo(home));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
    }

    [Test]
    public void AuthoredImpactShakeRemainsVisibleDuringCastOwnershipWithoutPanOrZoom()
    {
        director.TryBeginAnticipationPan(first,true);Step(.3f);
        Vector3 before=camera.WorldToScreenPoint(VisibleCenter(first));
        director.ReportGroundSlam(true);
        float start=Get<float>(director,"shakeStartedAt"),end=Get<float>(director,"shakeUntil");
        Call(director,"ApplyShakeProjection",Mathf.Lerp(start,end,.1f));
        Assert.That(Vector3.Distance(before,camera.WorldToScreenPoint(VisibleCenter(first))),Is.GreaterThan(1f));
        Assert.That(director.AnticipationPanActive,Is.True);
        director.EndBattle();Assert.That(camera.transform.position,Is.EqualTo(home));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(Time.timeScale,Is.EqualTo(2f));
    }

    [Test]
    public void StrongShakeRemainsVisibleDuringCastOwnershipWithoutZooming()
    {
        director.TryBeginAnticipationPan(first,true);director.ReportGroundSlam(true);
        Set(director,"shakeStartedAt",Time.unscaledTime-.026f);Set(director,"shakeUntil",Time.unscaledTime+.234f);
        Step(.9f);Assert.That(Get<Vector2>(director,"appliedShakeOffset").sqrMagnitude,Is.GreaterThan(0f));
        director.EndAnticipationPan(first);Step(2f);
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
    }

    [Test]
    public void SkillOwnershipAtTheMapEdgePreservesAuthoredDirectionalShakeWithoutZooming()
    {
        director.TryBeginAnticipationPan(first,true);Step(.8f);
        director.ReportPressureBreath(Vector3.right);
        float start=Get<float>(director,"shakeStartedAt"),end=Get<float>(director,"shakeUntil");
        Call(director,"ApplyShakeProjection",Mathf.Lerp(start,end,.1f));
        Assert.That(Get<Vector2>(director,"appliedShakeOffset").x,Is.GreaterThan(0f));
        AssertStationaryFraming();
    }

    [Test]
    public void CastOwnershipDoesNotTurnDamageReportsIntoAutomaticOrRepeatedShakes()
    {
        director.TryBeginAnticipationPan(first,true);Step(.5f);
        director.ReportDamage("first",true,first.transform.position,second.transform.position,25f);
        Assert.That(Get<float>(director,"shakeUntil"),Is.Zero);
        director.ReportPressureBreath(Vector3.right);
        float start=Get<float>(director,"shakeStartedAt"),end=Get<float>(director,"shakeUntil");
        for(int hit=0;hit<8;hit++)
        {
            director.ReportDamage("first",true,first.transform.position,second.transform.position,25f);
            director.ReportDamage("second",true,second.transform.position,first.transform.position,25f);
        }
        Assert.That(Get<float>(director,"shakeStartedAt"),Is.EqualTo(start));
        Assert.That(Get<float>(director,"shakeUntil"),Is.EqualTo(end));
        Call(director,"ApplyShakeProjection",Mathf.Lerp(start,end,.1f));
        Assert.That(Get<Vector2>(director,"appliedShakeOffset").magnitude,Is.GreaterThan(0f));
        AssertStationaryFraming();
        director.EndAnticipationPan(first);director.BeginBattle();
        director.ReportDamage("first",true,first.transform.position,second.transform.position,25f);
        Assert.That(Get<float>(director,"shakeUntil"),Is.Zero);
    }

    [Test]
    public void SkillOwnershipLeavesThePaintedGroundAndActorsAtDifferentDepthsStationary()
    {
        Vector3 actor=home+camera.transform.forward*10f,ground=home+camera.transform.forward*90f;
        Vector3 actorBefore=camera.WorldToViewportPoint(actor),groundBefore=camera.WorldToViewportPoint(ground);
        director.TryBeginAnticipationPan(first,true);Step(.3f);
        Vector3 actorDelta=camera.WorldToViewportPoint(actor)-actorBefore,groundDelta=camera.WorldToViewportPoint(ground)-groundBefore;
        Assert.That(Vector2.Distance(new Vector2(actorDelta.x,actorDelta.y),director.AppliedAnticipationPanOffset),Is.LessThan(.00001f));
        Assert.That(Vector2.Distance(new Vector2(actorDelta.x,actorDelta.y),new Vector2(groundDelta.x,groundDelta.y)),Is.LessThan(.00001f));
        Assert.That(camera.transform.position,Is.EqualTo(home));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        director.CancelAnticipationPan(true);
        Assert.That(Vector3.Distance(camera.WorldToViewportPoint(actor),actorBefore),Is.LessThan(.00001f));
        Assert.That(Vector3.Distance(camera.WorldToViewportPoint(ground),groundBefore),Is.LessThan(.00001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PanCleanupPreservesCustomProjectionAndRestoresAutomaticProjectionUpdates(bool custom)
    {
        Matrix4x4 original=camera.projectionMatrix;
        if(custom){original.m02+=.035f;original.m12-=.018f;camera.projectionMatrix=original;}
        director.TryBeginAnticipationPan(first,true);Step(.3f);
        director.CancelAnticipationPan(true);
        for(int i=0;i<16;i++)Assert.That(camera.projectionMatrix[i],Is.EqualTo(original[i]).Within(.00001f));
        if(!custom)
        {
            camera.fieldOfView=fov+7f;
            Assert.That(Mathf.Abs(camera.projectionMatrix.m11-original.m11),Is.GreaterThan(.01f),
                "An originally automatic projection must continue responding to later camera settings.");
        }
    }

    [Test]
    public void PreparationIntegrationSelectsTheFirstAllyAndNeverQueuesOverlappingFollowers()
    {
        battleHost=new GameObject("Inactive preparation pan integration");battleHost.SetActive(false);
        var battle=battleHost.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        Set(battle,"expeditionRun",new ExpeditionRunSave());
        second.transform.position=first.transform.position+Vector3.right*.3f;
        enemyHost=new GameObject("Enemy preparation caster");Renderer(enemyHost,3f,1f);
        enemyHost.transform.position=first.transform.position+Vector3.right*.6f;
        object a=Unit(battle,firstHost,0),b=Unit(battle,secondHost,0),enemy=Unit(battle,enemyHost,1);
        UnitSet(a,"target",enemy);UnitSet(b,"target",enemy);UnitSet(enemy,"target",a);
        Assert.That(Call(battle,"BeginCommonSkillFocus",enemy,false),Is.True);
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(Call(battle,"BeginCommonSkillFocus",a,false),Is.True);
        Assert.That(Call(battle,"BeginCommonSkillFocus",b,false),Is.True);
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(first));Step(.25f);
        Call(battle,"ClearLocalSkillPresentation",Local(battle,a));Step(2f);
        Assert.That(director.AnticipationPanActive,Is.False);Assert.That(camera.transform.position,Is.EqualTo(home));
        Assert.That(Call(battle,"BeginCommonSkillFocus",a,false),Is.True);
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(first),
            "An ignored preparation must not block a fresh allied cast once the camera is idle.");
        Call(battle,"ClearLocalSkillPresentations");Assert.That(camera.transform.position,Is.EqualTo(home));
        Assert.That(Call(battle,"BeginCommonSkillFocus",a,false),Is.True);
        Assert.That(director.AnticipationPanActive,Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IdleCameraAcceptsAnyAlliesFreshCastDespiteEarlierLocalRecordsAndIgnoredPreparations(bool repeatIgnoredCaster)
    {
        battleHost=new GameObject("Inactive fresh allied focus integration");battleHost.SetActive(false);
        var battle=battleHost.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        Set(battle,"expeditionRun",new ExpeditionRunSave());
        second.transform.position=first.transform.position+Vector3.right*.3f;
        thirdHost=new GameObject("Later allied caster");var third=Renderer(thirdHost,3f,1f);
        thirdHost.transform.position=first.transform.position+Vector3.right*.6f;
        enemyHost=new GameObject("Fresh allied focus opponent");Renderer(enemyHost,3f,1f);
        enemyHost.transform.position=first.transform.position+Vector3.right*.9f;
        object a=Unit(battle,firstHost,0),b=Unit(battle,secondHost,0),c=Unit(battle,thirdHost,0),enemy=Unit(battle,enemyHost,1);
        UnitSet(a,"target",enemy);UnitSet(b,"target",enemy);UnitSet(c,"target",enemy);UnitSet(enemy,"target",a);
        Assert.That(Call(battle,"BeginCommonSkillFocus",a,false),Is.True);
        Assert.That(Call(battle,"BeginCommonSkillFocus",b,false),Is.True);
        if(repeatIgnoredCaster)Assert.That(Call(battle,"BeginCommonSkillFocus",c,false),Is.True);
        Step(.3f);Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(first));
        Call(battle,"ClearLocalSkillPresentation",Local(battle,a));
        if(repeatIgnoredCaster)Call(battle,"ClearLocalSkillPresentation",Local(battle,b));
        Step(2f);
        Assert.That(director.AnticipationPanActive,Is.False,"Earlier ignored casts must not become queued camera requests.");
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
        object ignored=repeatIgnoredCaster?c:b;
        Assert.That(Local(battle,ignored).GetType().GetField("pending").GetValue(Local(battle,ignored)),Is.Not.Null);
        Assert.That(Call(battle,"BeginCommonSkillFocus",repeatIgnoredCaster?b:c,false),Is.True);
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(repeatIgnoredCaster?second:third),
            "Only an occupied camera may reject a new allied cast; old presentation order and another pending cast are irrelevant.");
        Call(battle,"ClearLocalSkillPresentations");
    }

    [Test]
    public void FirstCasterKeepsFocusThroughCommitBodyAndChannelAndNeverHandsOffToAnOverlappingAlly()
    {
        battleHost=new GameObject("Inactive full skill focus integration");battleHost.SetActive(false);
        var battle=battleHost.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        Set(battle,"expeditionRun",new ExpeditionRunSave());second.transform.position=first.transform.position+Vector3.right*.3f;
        enemyHost=new GameObject("Full focus opponent");Renderer(enemyHost,3f,1f);enemyHost.transform.position=first.transform.position+Vector3.right*.6f;
        object a=Unit(battle,firstHost,0),b=Unit(battle,secondHost,0),enemy=Unit(battle,enemyHost,1);
        UnitSet(a,"target",enemy);UnitSet(b,"target",enemy);UnitSet(enemy,"target",a);RegisterSpy(battle);
        Assert.That(Call(battle,"BeginCommonSkillFocus",a,false),Is.True);
        Assert.That(Call(battle,"BeginCommonSkillFocus",b,false),Is.True);
        object state=Local(battle,a);
        foreach(object caster in new[]{a,b})UnitSet(Local(battle,caster),"startedAt",Time.unscaledTimeAsDouble-1.001d);
        Set(battle,"allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble-.001d);
        Call(battle,"UpdateCommonSkillFocus");
        Assert.That(state.GetType().GetField("pending").GetValue(state),Is.Null);
        var driver=firstHost.GetComponent<SpriteMonsterAnimationDriver>();Assert.That(driver.IsSkillActionActive,Is.True);
        Step(.8f);Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(first));
        Assert.That(director.TryBeginAnticipationPan(second,true),Is.False);
        driver.InterruptCurrentAction(true);
        var channelField=a.GetType().GetField("wyvernBreath");object channel=Activator.CreateInstance(channelField.FieldType,true);channelField.SetValue(a,channel);
        state.GetType().GetField("committedAt").SetValue(state,Time.timeAsDouble-.2d);Call(battle,"UpdateCommonSkillFocus");
        Assert.That(director.AnticipationPanActive,Is.True,"The first caster's channel continues after its drawn pose ends.");
        channel.GetType().GetField("finished").SetValue(channel,true);Call(battle,"UpdateCommonSkillFocus");
        Assert.That(director.AnticipationPanActive,Is.False);Step(2f);
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));Assert.That(director.AnticipationPanOffset,Is.EqualTo(Vector3.zero));
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.Null);
        Assert.That(Local(battle,b).GetType().GetField("pending").GetValue(Local(battle,b)),Is.Null,
            "Both allies release together after their full preparation; the second cast must not take over the camera.");
        Call(battle,"ClearLocalSkillPresentations");
    }

    [Test]
    public void CameraWaitsForTheSelectedEffectAfterTheCasterBodyAndActionLockHaveRecovered()
    {
        object a,b;var battle=CameraBattle(out a,out b);object state=CommitCamera(battle,a);
        var complete=(Action)Call(battle,"HoldSkillCameraEffect",a);
        Assert.That(complete,Is.Not.Null);
        RetireCameraBody(battle,a,state);
        Assert.That((bool)state.GetType().GetField("active").GetValue(state),Is.False);
        Assert.That(Call(battle,"HasSkillCastPresentation",a),Is.False);
        Assert.That(firstHost.GetComponent<SpriteMonsterAnimationDriver>().IsActionLocked,Is.False);
        Assert.That(Get<float>(firstHost.GetComponent<SpriteMonsterAnimationDriver>(),"skillFocusScale"),Is.EqualTo(1f));
        Assert.That(director.AnticipationPanActive,Is.True);
        Assert.That(director.TryBeginAnticipationPan(second,true),Is.False);
        complete();complete();Call(battle,"UpdateCommonSkillFocus");
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(Call(battle,"BeginCommonSkillFocus",b,false),Is.True);
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(second));
    }

    [Test]
    public void MultipleEffectsReleaseOnlyAfterTheLastCompletionAndOldCallbacksCannotReleaseANewCaster()
    {
        object a,b;var battle=CameraBattle(out a,out b);object state=CommitCamera(battle,a);
        var firstDone=(Action)Call(battle,"HoldSkillCameraEffect",a);
        var lastDone=(Action)Call(battle,"HoldSkillCameraEffect",a);
        Assert.That(Call(battle,"HoldSkillCameraEffect",b),Is.Null);
        RetireCameraBody(battle,a,state);firstDone();firstDone();Call(battle,"UpdateCommonSkillFocus");
        Assert.That(director.AnticipationPanActive,Is.True);
        lastDone();Call(battle,"UpdateCommonSkillFocus");
        Assert.That(director.AnticipationPanActive,Is.False);
        object next=CommitCamera(battle,b);var nextDone=(Action)Call(battle,"HoldSkillCameraEffect",b);
        RetireCameraBody(battle,b,next);firstDone();lastDone();Call(battle,"UpdateCommonSkillFocus");
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(second));
        nextDone();Call(battle,"UpdateCommonSkillFocus");Assert.That(director.AnticipationPanActive,Is.False);
    }

    [Test]
    public void ANewCastFromTheSameCasterCannotReuseOrExtendTheEarlierCamerasEffectLease()
    {
        object a,b;var battle=CameraBattle(out a,out b);object state=CommitCamera(battle,a);
        var complete=(Action)Call(battle,"HoldSkillCameraEffect",a);RetireCameraBody(battle,a,state);
        UnitSet(a,"skillTimer",0f);
        Assert.That(Call(battle,"BeginCommonSkillFocus",a,false),Is.True);
        object later=Local(battle,a);Assert.That(later,Is.Not.SameAs(state));
        Assert.That(Call(battle,"HoldSkillCameraEffect",a),Is.Null);
        // Even if the ignored new cast begins its body, it must not prolong the old effect's camera.
        firstHost.GetComponent<SpriteMonsterAnimationDriver>().SetSkillAnticipating(false);
        firstHost.GetComponent<SpriteMonsterAnimationDriver>().PlaySkillAttack();
        complete();Call(battle,"UpdateSkillCameraPresentation");
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(Get<object>(battle,"skillCameraPresentation"),Is.Null);
    }

    [TestCase("cancel")]
    [TestCase("death")]
    [TestCase("cleanup")]
    public void CameraOnlyEffectsNeverRetainOwnershipAfterCancellationDeathOrWaveCleanup(string reason)
    {
        object a,b;var battle=CameraBattle(out a,out b);object state=CommitCamera(battle,a);
        var complete=(Action)Call(battle,"HoldSkillCameraEffect",a);
        if(reason=="cancel")Call(battle,"ClearLocalSkillPresentation",state);
        else if(reason=="death"){UnitSet(a,"alive",false);Call(battle,"UpdateCommonSkillFocus");}
        else Call(battle,"ClearLocalSkillPresentations");
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(Get<object>(battle,"skillCameraPresentation"),Is.Null);
        object next=CommitCamera(battle,b);complete();Call(battle,"UpdateCommonSkillFocus");
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(second));
    }

    [Test]
    public void FocusLossWithoutAGameplayPauseClearsDragInputAndKeepsTheExecutingCamera()
    {
        object a,b;var battle=CameraBattle(out a,out b);CommitCamera(battle,a);
        var complete=(Action)Call(battle,"HoldSkillCameraEffect",a);
        Set(battle,"draggingItem",true);Set(battle,"draggedItemInstanceId","camera-focus-test-item");
        Call(battle,"OnApplicationFocus",false);
        Assert.That(Get<bool>(battle,"draggingItem"),Is.False);
        Assert.That(Get<string>(battle,"draggedItemInstanceId"),Is.Empty);
        Assert.That(firstHost.GetComponent<SpriteMonsterAnimationDriver>().IsSkillActionActive,Is.True);
        Assert.That(director.AnticipationPanActive,Is.True,
            "Changing window focus while combat continues must not cancel an executing skill's camera.");
        Assert.That(Get<object>(battle,"skillCameraPresentation"),Is.Not.Null);
        Assert.That(Call(battle,"HasSkillCastPresentation",a),Is.True);
        complete();
    }

    [Test]
    public void TransformedReaperCanPrepareItsNextSkillWithoutRequestingAnotherCameraFocus()
    {
        object a,b;var battle=CameraBattle(out a,out b);UnitSet(a,"rank3MantisReaper",true);
        Assert.That(Call(battle,"BeginCommonSkillFocus",a,false),Is.True);
        Assert.That(Local(battle,a).GetType().GetField("pending").GetValue(Local(battle,a)),Is.Not.Null);
        Assert.That(director.AnticipationPanActive,Is.False);
        Assert.That(Get<object>(battle,"skillCameraPresentation"),Is.Null);
        Assert.That(Call(battle,"BeginCommonSkillFocus",b,false),Is.True);
        Assert.That(Get<SpriteRenderer>(director,"anticipationPanSource"),Is.SameAs(second));
    }

    private MonsterAutoBattlePrototype CameraBattle(out object a,out object b)
    {
        battleHost=new GameObject("Inactive camera completion integration");battleHost.SetActive(false);
        var battle=battleHost.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        Set(battle,"expeditionRun",new ExpeditionRunSave());second.transform.position=first.transform.position+Vector3.right*.3f;
        enemyHost=new GameObject("Camera completion opponent");Renderer(enemyHost,3f,1f);
        enemyHost.transform.position=first.transform.position+Vector3.right*.6f;
        a=Unit(battle,firstHost,0);b=Unit(battle,secondHost,0);object enemy=Unit(battle,enemyHost,1);
        UnitSet(a,"target",enemy);UnitSet(b,"target",enemy);UnitSet(enemy,"target",a);RegisterSpy(battle);return battle;
    }

    private object CommitCamera(MonsterAutoBattlePrototype battle,object unit)
    {
        Assert.That(Call(battle,"BeginCommonSkillFocus",unit,false),Is.True);
        object state=Local(battle,unit);UnitSet(state,"startedAt",Time.unscaledTimeAsDouble-1.001d);
        Set(battle,"allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble-.001d);
        Call(battle,"UpdateCommonSkillFocus");
        Assert.That(state.GetType().GetField("pending").GetValue(state),Is.Null);
        UnitSet(state,"committedAt",Time.timeAsDouble-.2d);return state;
    }

    private void RetireCameraBody(MonsterAutoBattlePrototype battle,object unit,object state)
    {
        ((SpriteMonsterAnimationDriver)unit.GetType().GetField("spriteAnimation").GetValue(unit)).InterruptCurrentAction(true);
        Call(battle,"UpdateCommonSkillFocus");Call(battle,"ResetLocalSkillPresentation",state);
        Call(battle,"UpdateCommonSkillFocus");
    }

    private SpriteRenderer Renderer(GameObject value,float x,float y)
    {
        value.transform.SetPositionAndRotation(home+camera.transform.forward*10f+camera.transform.right*x+camera.transform.up*y,rotation);
        var result=value.AddComponent<SpriteRenderer>();result.sprite=sprite;return result;
    }
    private Vector3 VisibleCenter(SpriteRenderer value)=>value.transform.TransformPoint(new Vector3(value.flipX?.25f:-.25f,0f,0f));
    private float CenterError(SpriteRenderer value)
    {
        Vector3 point=camera.WorldToViewportPoint(VisibleCenter(value));
        return Vector2.Distance(new Vector2(point.x,point.y),Vector2.one*.5f);
    }
    private void Step(float seconds)
    {
        for(float elapsed=0f;elapsed<seconds-.00001f;elapsed+=1f/120f)
            StepFrame(Mathf.Min(1f/120f,seconds-elapsed));
    }
    private void StepFrame(float dt)
    {
        Call(director,"ClearShakeProjection");Call(director,"UpdateAnticipationPan",dt);
        Call(director,"ApplyShakeProjection",Time.unscaledTime);
        AssertStationaryFraming();
    }
    private Matrix4x4 BaseProjection()
    {
        Vector2 offset=director.AppliedAnticipationPanOffset+Get<Vector2>(director,"appliedShakeOffset");
        float zoom=director.AppliedAnticipationZoom;
        return Matrix4x4.Scale(new Vector3(1f/zoom,1f/zoom,1f))*
            Matrix4x4.Translate(new Vector3(-offset.x*2f,-offset.y*2f,0f))*camera.projectionMatrix;
    }
    private void AssertStationaryFraming()
    {
        Assert.That(director.AppliedAnticipationPanOffset,Is.EqualTo(Vector2.zero));
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));
        Assert.That(camera.transform.position,Is.EqualTo(home));
        Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));
    }
    private void RegisterSpy(MonsterAutoBattlePrototype battle)
    {
        var field=typeof(MonsterAutoBattlePrototype).GetField("skillExecutor",Private);object executor=Activator.CreateInstance(field.FieldType);field.SetValue(battle,executor);
        Type context=field.FieldType.GetGenericArguments()[0],handler=typeof(Func<,,>).MakeGenericType(context,typeof(SkillMasterRow),typeof(bool));
        var callback=Delegate.CreateDelegate(handler,this,GetType().GetMethod("Commit",Private).MakeGenericMethod(context));
        field.FieldType.GetMethod("Register").Invoke(executor,new object[]{GameMasterData.Skill("guardian_taunt").behavior,MonsterSkillTrigger.Active,callback});
    }
    private bool Commit<TContext>(TContext context,SkillMasterRow skill)
    {
        object source=typeof(TContext).GetField("source").GetValue(context);UnitSet(source,"skillUses",1);UnitSet(source,"skillTimer",4f);
        ((SpriteMonsterAnimationDriver)source.GetType().GetField("spriteAnimation").GetValue(source)).PlaySkillAttack(1f,null);return true;
    }
    private object Unit(MonsterAutoBattlePrototype battle,GameObject model,int team)
    {
        Type type=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        object unit=Activator.CreateInstance(type,true);
        var driver=model.AddComponent<SpriteMonsterAnimationDriver>();var frames=new[]{sprite};driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        UnitSet(unit,"model",model);var ring=new GameObject("Pan test ring");ring.transform.SetParent(model.transform,false);
        UnitSet(unit,"ring",ring);UnitSet(unit,"spriteAnimation",driver);UnitSet(unit,"alive",true);
        UnitSet(unit,"hp",100f);UnitSet(unit,"maxHp",100f);UnitSet(unit,"skillId","guardian_taunt");
        UnitSet(unit,"team",Enum.ToObject(type.GetField("team").FieldType,team));
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle)).Add(unit);return unit;
    }
    private object Local(MonsterAutoBattlePrototype battle,object unit)
    {
        foreach(object state in (IList)typeof(MonsterAutoBattlePrototype).GetField("localSkillPresentations",Private).GetValue(battle))
            if((bool)state.GetType().GetField("active").GetValue(state)&&state.GetType().GetField("unit").GetValue(state)==unit)return state;
        Assert.Fail("The caster has no local presentation.");return null;
    }
    private static void UnitSet(object value,string field,object data)=>value.GetType().GetField(field).SetValue(value,data);
    private static T Get<T>(object value,string field)=>(T)value.GetType().GetField(field,Private).GetValue(value);
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Private).SetValue(value,data);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Private).Invoke(value,args);
}
