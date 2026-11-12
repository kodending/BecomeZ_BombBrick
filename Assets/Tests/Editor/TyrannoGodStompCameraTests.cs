using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class TyrannoGodStompCameraTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject host;
    private Camera camera;
    private CombatCameraDirector director;
    private float previousSpeed;
    private float Read(string name)=>(float)typeof(CombatCameraDirector).GetField(name,Hidden).GetValue(director);
    private void Set(string name,float value)=>typeof(CombatCameraDirector).GetField(name,Hidden).SetValue(director,value);
    private void Late()=>typeof(CombatCameraDirector).GetMethod("LateUpdate",Hidden).Invoke(director,null);

    [SetUp]public void Setup()
    {
        previousSpeed=Time.timeScale;host=new GameObject("Tyranno god stomp camera");camera=host.AddComponent<Camera>();
        director=host.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.SetShakeStrength(1f);director.BeginBattle();
        Time.timeScale=0f;
    }

    [TearDown]public void Cleanup(){Object.DestroyImmediate(host);Time.timeScale=previousSpeed;}

    [TestCase(false,false)][TestCase(true,false)][TestCase(false,true)][TestCase(true,true)]
    public void StoppedWorldLandingShakesForBothPhysicalLensesAndEitherSideOfTheKillingHit(bool endFirst,bool physical)
    {
        camera.usePhysicalProperties=physical;director.Initialize(camera,false);director.SetShakeStrength(1f);director.BeginBattle();
        Vector3 point=Vector3.forward*10f,screen=camera.WorldToScreenPoint(point);
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        if(endFirst)director.EndBattle();director.ReportTyrannoGodStomp();if(!endFirst)director.EndBattle();
        Assert.That(Read("shakeAmplitudePixels"),Is.EqualTo(18f));
        Assert.That(Read("shakeUntil")-Read("shakeStartedAt"),Is.EqualTo(.30f).Within(.001f));
        float now=Time.unscaledTime;Set("shakeStartedAt",now-.03f);Set("shakeUntil",now+.27f);
        Late();Assert.That(Time.timeScale,Is.Zero);
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.GreaterThan(10f).And.LessThan(30f));
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));
        Set("shakeUntil",now-.001f);Late();
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.LessThan(.01f));
    }

    [Test]public void ShakeSettingDisableAndNextWaveClearTheSharedFinishingSlamState()
    {
        director.SetShakeStrength(0f);director.ReportTyrannoGodStomp();Assert.That(Read("shakeUntil"),Is.Zero);
        director.SetShakeStrength(1f);director.ReportTyrannoGodStomp();director.BeginBattle();
        Assert.That(Read("shakeUntil"),Is.Zero);Assert.That(Read("heavyGroundSlamShakeUntil"),Is.Zero);
        Vector3 point=Vector3.forward*10f,screen=camera.WorldToScreenPoint(point);
        director.ReportTyrannoGodStomp();float now=Time.unscaledTime;Set("shakeStartedAt",now-.03f);Set("shakeUntil",now+.27f);Late();
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.GreaterThan(10f));
        director.enabled=false;typeof(CombatCameraDirector).GetMethod("OnDisable",Hidden).Invoke(director,null);
        Assert.That(Read("shakeUntil"),Is.Zero);Assert.That(Read("heavyGroundSlamShakeUntil"),Is.Zero);
        Assert.That(Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.LessThan(.01f));
    }
}
