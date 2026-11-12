using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BasicAttackCameraJuiceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private Camera camera;
    private CombatCameraDirector director;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Basic Attack Camera Juice Test");
        camera = host.AddComponent<Camera>();
        director = host.AddComponent<CombatCameraDirector>();
        director.Initialize(camera, false);
        director.SetShakeStrength(1f);
        director.BeginBattle();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    [Test]
    public void ImpactMovesScreenBrieflyWithoutZoomAndRespectsShakeSettings()
    {
        Vector3 point = Vector3.forward * 10f;
        Vector3 baseline = camera.WorldToScreenPoint(point);
        float fieldOfView = camera.fieldOfView;
        float speed = Time.timeScale;
        director.ReportBasicAttackImpact(Vector3.right);
        float start = Get("shakeStartedAt");
        float end = Get("shakeUntil");
        Assert.That(end - start, Is.EqualTo(.08f).Within(.001f));
        float sample = Mathf.Lerp(start, end, .1f);
        Apply(sample);
        float full = Vector3.Distance(baseline, camera.WorldToScreenPoint(point));
        Assert.That(full, Is.InRange(1.5f, 2.5f));
        director.SetShakeStrength(.35f);
        Apply(sample);
        Assert.That(Vector3.Distance(baseline, camera.WorldToScreenPoint(point)), Is.LessThan(full));
        director.SetShakeStrength(0f);
        director.ReportBasicAttackImpact(Vector3.right);
        Assert.That(Get("shakeUntil"), Is.Zero);
        director.SetShakeStrength(1f);
        Apply(sample);
        Assert.That(Vector3.Distance(baseline, camera.WorldToScreenPoint(point)), Is.LessThan(.01f));
        Assert.That(Get("eventUntil"), Is.Zero);
        Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView));
        Assert.That(Time.timeScale, Is.EqualTo(speed));
    }

    [Test]
    public void HitsShareCooldownAndANewBattleStartsReady()
    {
        director.ReportBasicAttackImpact(Vector3.right);
        float cooldown = Get("nextBasicAttackShakeAt");
        Assert.That(cooldown - Get("shakeStartedAt"), Is.EqualTo(.18f).Within(.001f));
        Set("shakeUntil", Time.unscaledTime - .01f);
        director.ReportBasicAttackImpact(Vector3.left);
        Assert.That(Get("shakeUntil"), Is.LessThan(Time.unscaledTime));
        Set("nextBasicAttackShakeAt", Time.unscaledTime - .01f);
        director.ReportBasicAttackImpact(Vector3.left);
        Assert.That(Get("shakeUntil"), Is.GreaterThan(Time.unscaledTime));
        director.EndBattle();
        director.ReportBasicAttackImpact(Vector3.right);
        Assert.That(Get("shakeUntil"), Is.Zero);
        director.BeginBattle();
        director.ReportBasicAttackImpact(Vector3.right);
        Assert.That(Get("shakeUntil"), Is.GreaterThan(Time.unscaledTime));
    }

    [Test]
    public void EvenTheTailOfAHeavySkillShakeSurvivesABasicImpact()
    {
        director.ReportGroundSlam(true);
        float end = Time.unscaledTime + .02f;
        Set("shakeUntil", end);
        director.ReportBasicAttackImpact(Vector3.right);
        Assert.That(Get("shakeAmplitudePixels"), Is.EqualTo(12f));
        Assert.That(Get("shakeUntil"), Is.EqualTo(end));
        Assert.That(Get("nextBasicAttackShakeAt"), Is.Zero);
    }

    private float Get(string field) => (float)typeof(CombatCameraDirector).GetField(field, Private).GetValue(director);
    private void Set(string field, float value) => typeof(CombatCameraDirector).GetField(field, Private).SetValue(director, value);
    private void Apply(float time) => typeof(CombatCameraDirector).GetMethod("ApplyShakeProjection", Private).Invoke(director, new object[] { time });
}
