using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SkillFocusCameraTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject host;
    private Camera camera;
    private CombatCameraDirector director;
    private float previousSpeed,homeFov;
    private Matrix4x4 projection;
    private Vector3 position;
    private Quaternion rotation;
    private bool previousTestMode;
    private object previousFrontend;

    [SetUp]
    public void SetUp()
    {
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        CombatEffectTestConfiguration.Active=true;
        host=new GameObject("Stationary skill camera test");camera=host.AddComponent<Camera>();
        camera.fieldOfView=37.5f;camera.lensShift=Vector2.zero;
        director=host.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);
        director.SetShakeStrength(1f);director.BeginBattle();Time.timeScale=2f;
        homeFov=camera.fieldOfView;position=camera.transform.position;rotation=camera.transform.rotation;
        projection=camera.projectionMatrix;projection.m02+=.035f;projection.m12-=.018f;
        camera.projectionMatrix=projection;
    }

    [TearDown]
    public void TearDown()
    {
        director.EndBattle();Object.DestroyImmediate(host);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
    }

    [TestCase(true)] [TestCase(false)]
    public void AlliedAndEnemySkillReportsDoNotMoveZoomOrStopTheCamera(bool allied)
    {
        director.ReportSkill("caster",allied,new Vector3(3f,0f,10f),new Vector3(-2f,1f,8f));
        Assert.That(director.BeginSkillFocus(),Is.False);
        director.BeginSkillExecution();
        for(int i=0;i<4;i++){Call("LateUpdate");AssertStationary();}
        Assert.That(director.SkillFocusExecuting,Is.False);Assert.That(Time.timeScale,Is.EqualTo(2f));
        director.CancelSkillFocus();director.EndBattle();Call("LateUpdate");AssertStationary();
        Assert.That(director.SkillFocusActive,Is.False);Assert.That(Time.timeScale,Is.EqualTo(2f));
    }

    [TestCase("CancelSkillFocus")] [TestCase("EndBattle")] [TestCase("OnDisable")] [TestCase("Pause")]
    public void PresentationCleanupDoesNotChangeTheCameraOrSelectedSpeed(string exit)
    {
        Assert.That(director.BeginSkillFocus(),Is.False);Call("LateUpdate");AssertStationary();
        if(exit=="Pause")CombatCameraDirector.PrepareForGameplayPause();
        else if(exit=="CancelSkillFocus")director.CancelSkillFocus();
        else if(exit=="EndBattle")director.EndBattle();
        else Call("OnDisable");
        AssertStationary();Assert.That(director.SkillFocusActive,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(2f));
    }

    private void AssertStationary()
    {
        Assert.That(camera.fieldOfView,Is.EqualTo(homeFov));
        Assert.That(camera.lensShift,Is.EqualTo(Vector2.zero));
        Assert.That(camera.transform.position,Is.EqualTo(position));
        Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Matrix4x4 actual=camera.projectionMatrix;
        for(int i=0;i<16;i++)Assert.That(actual[i],Is.EqualTo(projection[i]).Within(.00001f),"Projection element "+i);
    }
    private void Call(string method)=>typeof(CombatCameraDirector).GetMethod(method,Private).Invoke(director,null);
}
