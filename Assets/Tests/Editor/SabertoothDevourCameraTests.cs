using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SabertoothDevourCameraTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject host;
    private Camera camera;
    private CombatCameraDirector director;
    private static readonly Vector3 Point=Vector3.forward*10f;

    [SetUp] public void Setup()
    {
        host=new GameObject("Devour bite camera test");camera=host.AddComponent<Camera>();
        director=host.AddComponent<CombatCameraDirector>();
    }

    [TearDown] public void Cleanup()
    {
        if(director!=null)Call("OnDisable");
        Object.DestroyImmediate(host);
    }

    [TestCase(false,false)] [TestCase(true,false)] [TestCase(false,true)] [TestCase(true,true)]
    public void TheFinalBiteShakesTheActualImageEvenWhenBattleEndHappensOnEitherSideOfTheHit(
        bool battleEndedBeforeImpact,bool physicalCamera)
    {
        camera.usePhysicalProperties=physicalCamera;Begin();
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        float fov=camera.fieldOfView,speed=Time.timeScale;Vector2 lens=camera.lensShift;
        Vector3 screen=camera.WorldToScreenPoint(Point);
        if(battleEndedBeforeImpact)director.EndBattle();
        director.ReportSabertoothDevourBite();director.ReportSilkLanceImpact(false);
        if(!battleEndedBeforeImpact)director.EndBattle();
        float start=Get("shakeStartedAt"),end=Get("shakeUntil"),sample=Mathf.Lerp(start,end,.1f);
        Assert.That(Get("shakeAmplitudePixels"),Is.EqualTo(18f));
        Assert.That(end-start,Is.EqualTo(.28f).Within(.001f));
        Assert.That(end,Is.GreaterThan(Time.unscaledTime));
        Call("ApplyShakeProjection",sample);
        float full=Vector3.Distance(camera.WorldToScreenPoint(Point),screen);
        Assert.That(full,Is.GreaterThan(10f).And.LessThan(25f));
        director.SetShakeStrength(.35f);Call("ApplyShakeProjection",sample);
        float reduced=Vector3.Distance(camera.WorldToScreenPoint(Point),screen);
        Assert.That(reduced,Is.GreaterThan(0f).And.LessThan(full));
        Assert.That(reduced,Is.EqualTo(full*.35f).Within(1f));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.lensShift,Is.EqualTo(lens));
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(director.AppliedAnticipationPanOffset,Is.EqualTo(Vector2.zero));
        Assert.That(director.AppliedAnticipationZoom,Is.EqualTo(1f));Assert.That(Get("eventUntil"),Is.Zero);
        Assert.That(Time.timeScale,Is.EqualTo(speed));
        Call("ApplyShakeProjection",end+.001f);
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(Point),screen),Is.LessThan(.01f));
        director.BeginBattle();Call("ApplyShakeProjection",sample);
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(Point),screen),Is.LessThan(.01f));
    }

    [Test]
    public void TurningShakeOffOrDisablingTheCameraClearsTheBiteWithoutReplayingIt()
    {
        Begin();Vector3 screen=camera.WorldToScreenPoint(Point);
        director.ReportSabertoothDevourBite();
        float sample=Mathf.Lerp(Get("shakeStartedAt"),Get("shakeUntil"),.1f);
        Call("ApplyShakeProjection",sample);
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(Point),screen),Is.GreaterThan(10f));
        director.SetShakeStrength(0f);director.EndBattle();director.ReportSabertoothDevourBite();
        Assert.That(Get("shakeUntil"),Is.Zero);
        director.SetShakeStrength(1f);Call("ApplyShakeProjection",sample);
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(Point),screen),Is.LessThan(.01f));
        director.BeginBattle();director.ReportSabertoothDevourBite();Call("OnDisable");
        Assert.That(Get("shakeUntil"),Is.Zero);
        Call("ApplyShakeProjection",sample);
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(Point),screen),Is.LessThan(.01f));
    }

    private void Begin()
    {
        director.Initialize(camera,false);director.SetShakeStrength(1f);director.BeginBattle();
    }
    private float Get(string field)=>(float)typeof(CombatCameraDirector).GetField(field,Private).GetValue(director);
    private void Call(string method,params object[] args)=>
        typeof(CombatCameraDirector).GetMethod(method,Private).Invoke(director,args);
}

