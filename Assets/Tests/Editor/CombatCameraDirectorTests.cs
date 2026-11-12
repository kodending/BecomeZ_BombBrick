using NUnit.Framework;

public sealed class CombatCameraDirectorTests
{
    [Test]
    public void EveryShakePathScalesRenderedMotionAndOffImmediatelyClearsWithoutReplay()
    {
        var host = new UnityEngine.GameObject("Camera Shake Settings Test");
        float speed = UnityEngine.Time.timeScale;
        try
        {
            var camera = host.AddComponent<UnityEngine.Camera>();
            var director = host.AddComponent<CombatCameraDirector>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(CombatCameraDirector);
            var point = UnityEngine.Vector3.forward * 10f;
            var baseline = camera.WorldToScreenPoint(point);
            float fov = camera.fieldOfView;
            var reports = new System.Action<CombatCameraDirector>[] {
                d => d.ReportGroundSlam(), d => d.ReportGroundSlam(true), d => d.ReportMonkeyStormThunder(), d => d.ReportMarchFootstep(),
                d => d.ReportPressureBreath(UnityEngine.Vector3.right),
                d => d.ReportMonkeyFruitImpact(), d => d.ReportMonkeyFruitImpact(true),
                d => d.ReportGriffinLanding(), d => d.ReportHaetaeThunder(), d => d.ReportJawCrush(),
                d => d.ReportSilkLanceImpact(false), d => d.ReportSilkLanceImpact(true), d => d.ReportBearAvatarRoar(),
                d => d.ReportSpacetimeCollapse(),
                d => d.ReportStrongCritical("ally", true, UnityEngine.Vector3.zero, point)
            };
            for (int i = 0; i < reports.Length; i++)
            {
                director.Initialize(camera, true);
                director.SetShakeStrength(1f);
                director.BeginBattle();
                reports[i](director);
                float start = (float)type.GetField("shakeStartedAt", flags).GetValue(director);
                float end = (float)type.GetField("shakeUntil", flags).GetValue(director);
                float sample = UnityEngine.Mathf.Lerp(start, end, 0.1f);
                var apply = type.GetMethod("ApplyShakeProjection", flags);
                apply.Invoke(director, new object[] { sample });
                float full = UnityEngine.Vector3.Distance(baseline, camera.WorldToScreenPoint(point));
                Assert.That(full, Is.GreaterThan(1f), "Report " + i);

                director.SetShakeStrength(0.35f);
                apply.Invoke(director, new object[] { sample });
                float reduced = UnityEngine.Vector3.Distance(baseline, camera.WorldToScreenPoint(point));
                Assert.That(reduced, Is.LessThan(full), "Report " + i);
                Assert.That(reduced, Is.EqualTo(full * 0.35f).Within(1f), "Integer pixel rounding, report " + i);

                director.SetShakeStrength(0f);
                Assert.That(UnityEngine.Vector3.Distance(baseline, camera.WorldToScreenPoint(point)), Is.LessThan(0.01f));
                reports[i](director);
                Assert.That(type.GetField("shakeUntil", flags).GetValue(director), Is.EqualTo(0f));
                director.SetShakeStrength(1f);
                apply.Invoke(director, new object[] { sample });
                Assert.That(UnityEngine.Vector3.Distance(baseline, camera.WorldToScreenPoint(point)), Is.LessThan(0.01f));
                Assert.That(camera.fieldOfView, Is.EqualTo(fov));
                Assert.That(camera.transform.position, Is.EqualTo(UnityEngine.Vector3.zero));
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(speed));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Time.timeScale = speed; }
    }

    [Test]
    public void FinalSilkImpactOverridesAWeakerShakeAndResistsSubsequentWeakHits()
    {
        var host=new UnityEngine.GameObject("Overlapping Silk Impact Test");
        try
        {
            var camera=host.AddComponent<UnityEngine.Camera>();
            var director=host.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.SetShakeStrength(1f);
            director.BeginBattle();director.ReportSilkLanceImpact(false);
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var type=typeof(CombatCameraDirector);
            director.ReportSilkLanceImpact(true);
            Assert.That(type.GetField("shakeAmplitudePixels",flags).GetValue(director),Is.EqualTo(11f));
            float end=(float)type.GetField("shakeUntil",flags).GetValue(director);
            director.ReportSilkLanceImpact(false);
            Assert.That(type.GetField("shakeAmplitudePixels",flags).GetValue(director),Is.EqualTo(11f));
            Assert.That(type.GetField("shakeUntil",flags).GetValue(director),Is.EqualTo(end));
            Assert.That(type.GetField("eventUntil",flags).GetValue(director),Is.EqualTo(0f));
        }
        finally{UnityEngine.Object.DestroyImmediate(host);}
    }

    [TestCase(false)] [TestCase(true)]
    public void SilkGroundContactShakesWithoutZoomIncludingTheWinningBlow(bool final)
    {
        var host=new UnityEngine.GameObject("Silk Impact Camera Test");
        try
        {
            var camera=host.AddComponent<UnityEngine.Camera>();
            var director=host.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.SetShakeStrength(1f);
            float fov=camera.fieldOfView,speed=UnityEngine.Time.timeScale;
            var point=UnityEngine.Vector3.forward*10;var screen=camera.WorldToScreenPoint(point);
            director.BeginBattle();if(final)director.EndBattle();director.ReportSilkLanceImpact(final);
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var type=typeof(CombatCameraDirector);float now=UnityEngine.Time.unscaledTime;
            Assert.That(type.GetField("shakeAmplitudePixels",flags).GetValue(director),Is.EqualTo(final?11f:3.5f));
            type.GetField("shakeStartedAt",flags).SetValue(director,now-(final?.02f:.006f));
            type.GetField("shakeUntil",flags).SetValue(director,now+(final?.20f:.059f));
            type.GetMethod("LateUpdate",flags).Invoke(director,null);
            Assert.That(UnityEngine.Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.GreaterThan(final?5f:1f));
            Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(UnityEngine.Time.timeScale,Is.EqualTo(speed));
            Assert.That(type.GetField("eventUntil",flags).GetValue(director),Is.EqualTo(0f));
            type.GetMethod("OnDisable",flags).Invoke(director,null);
            Assert.That(UnityEngine.Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.LessThan(.01f));
        }
        finally{UnityEngine.Object.DestroyImmediate(host);}
    }

    [TestCase(false,false,false)]
    [TestCase(false,true,false)]
    [TestCase(true,false,false)]
    [TestCase(true,true,false)]
    [TestCase(false,false,true)]
    [TestCase(false,true,true)]
    [TestCase(true,false,true)]
    [TestCase(true,true,true)]
    public void GroundSlamMovesActualScreenCoordinatesWithoutZoomAndDisableRestoresCamera(
        bool automaticFraming,bool physicalCamera,bool heavy)
    {
        var host=new UnityEngine.GameObject("Slam Camera Test");
        try
        {
            var camera=host.AddComponent<UnityEngine.Camera>();
            camera.usePhysicalProperties=physicalCamera;
            var director=host.AddComponent<CombatCameraDirector>();director.Initialize(camera,automaticFraming);director.SetShakeStrength(1f);
            float homeFov=camera.fieldOfView;
            var point=UnityEngine.Vector3.forward*10f;
            var originalScreen=camera.WorldToScreenPoint(point);
            director.BeginBattle();director.ReportGroundSlam(heavy);
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var type=typeof(CombatCameraDirector);
            float start=(float)type.GetField("shakeStartedAt",flags).GetValue(director);
            float end=(float)type.GetField("shakeUntil",flags).GetValue(director);
            Assert.That(end-start,Is.EqualTo(heavy?.26f:.22f).Within(.001f));
            Assert.That((float)type.GetField("eventUntil",flags).GetValue(director),Is.Zero);
            Assert.That(director.CurrentFocusActorId,Is.Empty);
            type.GetField("shakeStartedAt",flags).SetValue(director,UnityEngine.Time.unscaledTime-.022f);
            type.GetField("shakeUntil",flags).SetValue(director,UnityEngine.Time.unscaledTime+.198f);
            type.GetMethod("LateUpdate",flags).Invoke(director,null);
            Assert.That(camera.fieldOfView,Is.EqualTo(homeFov));
            Assert.That(UnityEngine.Mathf.Abs(camera.WorldToScreenPoint(point).y-originalScreen.y),
                Is.GreaterThan(6f),"A changed lensShift field alone does not prove that the image moved.");
            // This non-ExecuteAlways component needs an explicit lifecycle call in EditMode.
            type.GetMethod("OnDisable",flags).Invoke(director,null);
            Assert.That(camera.lensShift,Is.EqualTo(UnityEngine.Vector2.zero));
            Assert.That(UnityEngine.Vector3.Distance(camera.WorldToScreenPoint(point),originalScreen),Is.LessThan(.01f));
        }
        finally{UnityEngine.Object.DestroyImmediate(host);}
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrdinaryCombatOnlyFramesCameraWhenEnabled(bool automaticFraming)
    {
        var host=new UnityEngine.GameObject("Automatic Framing Test");
        float originalSpeed=UnityEngine.Time.timeScale;
        try
        {
            var camera=host.AddComponent<UnityEngine.Camera>();
            var director=host.AddComponent<CombatCameraDirector>();director.Initialize(camera,automaticFraming);director.SetShakeStrength(1f);
            float homeFov=camera.fieldOfView;
            var position=UnityEngine.Vector3.right*3f;
            director.BeginBattle();
            director.ReportDamage("ally",true,UnityEngine.Vector3.zero,position,20f);
            director.ReportSkill("ally",true,UnityEngine.Vector3.zero,position);
            director.ReportStrongCritical("ally",true,UnityEngine.Vector3.zero,position);
            director.ReportDeath("ally",true,"enemy",true,UnityEngine.Vector3.zero,position);
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var type=typeof(CombatCameraDirector);
            float eventUntil=(float)type.GetField("eventUntil",flags).GetValue(director);
            float shakeUntil=(float)type.GetField("shakeUntil",flags).GetValue(director);
            if(automaticFraming)
            {
                Assert.That(eventUntil,Is.GreaterThan(UnityEngine.Time.unscaledTime));
                Assert.That(shakeUntil,Is.Zero,"Cast and death notifications alone must not imply a skill hit.");
            }
            else
            {
                Assert.That(eventUntil,Is.Zero);Assert.That(shakeUntil,Is.Zero);
                Assert.That(director.CurrentFocusActorId,Is.Empty);
                type.GetMethod("LateUpdate",flags).Invoke(director,null);
                Assert.That(camera.fieldOfView,Is.EqualTo(homeFov));
                Assert.That(camera.lensShift,Is.EqualTo(UnityEngine.Vector2.zero));
                Assert.That(UnityEngine.Time.timeScale,Is.EqualTo(originalSpeed));
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Time.timeScale=originalSpeed;}
    }
    [Test]
    public void DamageLeaderNeedsClearAdvantageAfterFocusLock()
    {
        Assert.That(CombatCameraDirector.ShouldSwitchFocus(100f, 130f, false), Is.False);
        Assert.That(CombatCameraDirector.ShouldSwitchFocus(100f, 119f, true), Is.False);
        Assert.That(CombatCameraDirector.ShouldSwitchFocus(100f, 120f, true), Is.True);
    }

    [Test]
    public void SlowMotionOnlyTriggersForFocusedAllyKillingEnemy()
    {
        Assert.That(CombatCameraDirector.ShouldSlowFocusedKill(
            "ally-a", "ally-a", true, true), Is.True);
        Assert.That(CombatCameraDirector.ShouldSlowFocusedKill(
            "ally-a", "ally-b", true, true), Is.False);
        Assert.That(CombatCameraDirector.ShouldSlowFocusedKill(
            "ally-a", "ally-a", true, false), Is.False);
        Assert.That(CombatCameraDirector.ShouldSlowFocusedKill(
            "ally-a", "enemy-a", false, true), Is.False);
    }

    [Test]
    public void FocusScoreBalancesTotalAndRecentDamage()
    {
        Assert.That(CombatCameraDirector.DamageFocusScore(100f, 50f),
            Is.EqualTo(80f).Within(0.001f));
    }
}
