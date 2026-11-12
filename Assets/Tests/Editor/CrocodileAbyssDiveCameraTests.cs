using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CrocodileAbyssDiveCameraTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject host;
    private Camera camera;
    private CombatCameraDirector director;

    [SetUp] public void Setup(){host=new GameObject("Crocodile abyss crush camera");camera=host.AddComponent<Camera>();director=host.AddComponent<CombatCameraDirector>();}
    [TearDown] public void Cleanup(){Object.DestroyImmediate(host);}

    [TestCase(false,false)] [TestCase(true,false)] [TestCase(false,true)] [TestCase(true,true)]
    public void OneHeavyLandingShakesTheImageAndFinishesNaturallyEvenOnTheKillingHit(bool endFirst,bool physical)
    {
        camera.usePhysicalProperties=physical;director.Initialize(camera,false);director.SetShakeStrength(1f);director.BeginBattle();
        Vector3 point=Vector3.forward*10f,screen=camera.WorldToScreenPoint(point),position=camera.transform.position;
        Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        if(endFirst)director.EndBattle();director.ReportCrocodileAbyssCrush();if(!endFirst)director.EndBattle();
        float start=Get("shakeStartedAt"),end=Get("shakeUntil");
        Assert.That(Get("shakeAmplitudePixels"),Is.EqualTo(18f));Assert.That(end-start,Is.EqualTo(.24f).Within(.001f));
        Call("ApplyShakeProjection",Mathf.Lerp(start,end,.1f));
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.GreaterThan(6f).And.LessThan(25f));
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));Assert.That(camera.fieldOfView,Is.EqualTo(fov));
        Call("ApplyShakeProjection",end+.001f);Assert.That(Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.LessThan(.01f));
        director.BeginBattle();Assert.That(Get("shakeUntil"),Is.Zero);
    }

    [Test]
    public void CameraShakeOffAndWaveResetCannotReplayTheFinishingSlam()
    {
        director.Initialize(camera,false);director.SetShakeStrength(1f);director.BeginBattle();director.ReportCrocodileAbyssCrush();
        director.SetShakeStrength(0f);director.EndBattle();director.ReportCrocodileAbyssCrush();Assert.That(Get("shakeUntil"),Is.Zero);
        director.SetShakeStrength(1f);director.BeginBattle();director.ReportCrocodileAbyssCrush();Call("OnDisable");Assert.That(Get("shakeUntil"),Is.Zero);
    }
    private float Get(string field)=>(float)typeof(CombatCameraDirector).GetField(field,Private).GetValue(director);
    private void Call(string method,params object[] args)=>typeof(CombatCameraDirector).GetMethod(method,Private).Invoke(director,args);
}
