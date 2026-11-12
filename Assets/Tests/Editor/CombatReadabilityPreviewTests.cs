using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatReadabilityPreviewTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private bool testMode, enabled, basicOnly, feedback, skillAudio;
    private int seed;
    private CombatReadabilityPreset preset;

    [SetUp]
    public void Setup()
    {
        testMode = CombatEffectTestConfiguration.Active;
        enabled = CombatReadabilityPreview.Enabled; preset = CombatReadabilityPreview.Preset;
        basicOnly = CombatReadabilityPreview.BasicOnly; feedback = CombatReadabilityPreview.ContactFeedback;
        skillAudio = CombatReadabilityPreview.SkillAudio; seed = CombatReadabilityPreview.Seed;
        CombatReadabilityPreview.Reset();
    }

    [TearDown]
    public void Cleanup()
    {
        CombatEffectTestConfiguration.Active = testMode;
        CombatReadabilityPreview.Enabled = enabled; CombatReadabilityPreview.Preset = preset;
        CombatReadabilityPreview.BasicOnly = basicOnly; CombatReadabilityPreview.ContactFeedback = feedback;
        CombatReadabilityPreview.SkillAudio = skillAudio; CombatReadabilityPreview.Seed = seed;
    }

    [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)]
    public void ProductionOrAnUnselectedComparisonKeepsTheExistingPresentation(bool test, bool selected)
    {
        CombatEffectTestConfiguration.Active = test;
        CombatReadabilityPreview.Enabled = selected;
        CombatReadabilityPreview.Preset = CombatReadabilityPreset.Local;
        Assert.That(CombatReadabilityPreview.Active, Is.False);
        Assert.That(CombatReadabilityPreview.PreparationDuration, Is.EqualTo(1f));
        Assert.That(CombatReadabilityPreview.PreparationSpeed, Is.EqualTo(.08f));
        Assert.That(CombatReadabilityPreview.ShadeAlpha, Is.EqualTo(.5f));
        Assert.That(CombatReadabilityPreview.SkillBodyScale, Is.EqualTo(1.5f));
        Assert.That(CombatReadabilityPreview.HologramCount, Is.EqualTo(3));
        Assert.That(CombatReadabilityPreview.ContactIntensity, Is.EqualTo(1f));
        Assert.That(CombatReadabilityPreview.SkillSoundGain, Is.EqualTo(1f));
    }

    [TestCase(CombatReadabilityPreset.Current, 1f, .08f, .5f, 1.5f, 3)]
    [TestCase(CombatReadabilityPreset.Compact, .3f, .45f, .15f, 1.12f, 1)]
    [TestCase(CombatReadabilityPreset.Local, .35f, 1f, 0f, 1f, 1)]
    public void ComparisonPresetsHaveExplicitAndDistinctEmphasis(CombatReadabilityPreset mode,
        float duration, float speed, float shade, float scale, int ghosts)
    {
        Enable(mode);
        Assert.That(CombatReadabilityPreview.PreparationDuration, Is.EqualTo(duration));
        Assert.That(CombatReadabilityPreview.PreparationSpeed, Is.EqualTo(speed));
        Assert.That(CombatReadabilityPreview.ShadeAlpha, Is.EqualTo(shade));
        Assert.That(CombatReadabilityPreview.SkillBodyScale, Is.EqualTo(scale));
        Assert.That(CombatReadabilityPreview.HologramCount, Is.EqualTo(ghosts));
    }

    [TestCase(CombatReadabilityPreset.Current, 1.5f)]
    [TestCase(CombatReadabilityPreset.Compact, 1.12f)]
    [TestCase(CombatReadabilityPreset.Local, 1f)]
    public void EmphasisOnlyChangesTheSpriteAndRestoresItsAuthoredPose(CombatReadabilityPreset mode, float scale)
    {
        Enable(mode);
        GameObject instance = PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
        try
        {
            var driver = instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            instance.transform.position = new Vector3(4f, 0f, -2f);
            driver.PlaySkillAttack(1f, null);
            Advance(driver);
            Vector3 pose = driver.transform.localScale;
            Vector3 root = instance.transform.position;
            Sprite sprite = driver.GetComponent<SpriteRenderer>().sprite;
            for (int i = 0; i < 3; i++) { driver.SetSkillFocus(1f); Advance(driver); }
            Assert.That(Vector3.Distance(driver.transform.localScale, pose * scale), Is.LessThan(.00001f));
            Assert.That(instance.transform.position, Is.EqualTo(root));
            Assert.That(driver.GetComponent<SpriteRenderer>().sprite, Is.SameAs(sprite));
            driver.SetSkillFocus(0f); Advance(driver);
            Assert.That(Vector3.Distance(driver.transform.localScale, pose), Is.LessThan(.00001f));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void LocalContactReturnsFromOneImpulseWithoutMovingTheCollisionBody()
    {
        Enable(CombatReadabilityPreset.Local);
        GameObject instance = PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
        try
        {
            var driver = instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            Advance(driver);
            Vector3 root = instance.transform.position;
            driver.PlayContactReaction(driver.transform.right, false);
            Vector3 offset = (Vector3)typeof(SpriteMonsterAnimationDriver).GetField("contactShakeOffset", Private).GetValue(driver);
            Assert.That(offset.magnitude, Is.GreaterThan(0f));
            typeof(SpriteMonsterAnimationDriver).GetField("contactShakeRemaining", Private).SetValue(driver, .09f);
            Advance(driver);
            Vector3 applied = (Vector3)typeof(SpriteMonsterAnimationDriver).GetField("appliedRecoilOffset", Private).GetValue(driver);
            Assert.That(Vector3.Dot(applied, offset), Is.GreaterThan(0f), "One directional impulse must not reverse into a repeated shake.");
            Assert.That(instance.transform.position, Is.EqualTo(root));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void BasicOnlyComparisonDoesNotEnqueueOrStartASkill()
    {
        Enable(CombatReadabilityPreset.Local);
        CombatReadabilityPreview.BasicOnly = true;
        var host = new GameObject("Readability basic-only gate test"); host.SetActive(false);
        try
        {
            var battle = host.AddComponent<MonsterAutoBattlePrototype>();
            Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("TryUseSkill", Private)
                .Invoke(battle, new object[] { null, false }), Is.EqualTo(false));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase(CombatReadabilityPreset.Current, 1f)]
    [TestCase(CombatReadabilityPreset.Compact, 1f)]
    [TestCase(CombatReadabilityPreset.Local, .675f)]
    public void CompleteWorldHudShrinksAroundTheHeadClearanceOnlyInLocalPreview(
        CombatReadabilityPreset mode, float scale)
    {
        Enable(mode);
        var body = new Rect(200f, 300f, 60f, 75f);
        var anchor = new Vector3(body.center.x, body.yMin - 6f, 0f);
        var screen = Matrix4x4.TRS(new Vector3(13f, 7f, 0f), Quaternion.identity,
            new Vector3(1.5f, 1.5f, 1f));
        var hud = CombatReadabilityPreview.WorldHudMatrix(screen, body);
        Assert.That(Vector3.Distance(hud.MultiplyPoint3x4(anchor), screen.MultiplyPoint3x4(anchor)), Is.LessThan(.001f));
        Assert.That(hud.MultiplyVector(Vector3.right).magnitude, Is.EqualTo(1.5f * scale).Within(.001f));
        Assert.That(hud.MultiplyVector(Vector3.up).magnitude, Is.EqualTo(1.5f * scale).Within(.001f));
        CombatEffectTestConfiguration.Active = false;
        Assert.That(CombatReadabilityPreview.WorldHudMatrix(screen, body), Is.EqualTo(screen));
        CombatEffectTestConfiguration.Active = true;
        CombatReadabilityPreview.Enabled = false;
        Assert.That(CombatReadabilityPreview.WorldHudMatrix(screen, body), Is.EqualTo(screen));
    }

    private static void Enable(CombatReadabilityPreset mode)
    {
        CombatEffectTestConfiguration.Active = true;
        CombatReadabilityPreview.Enabled = true;
        CombatReadabilityPreview.Preset = mode;
    }

    private static void Advance(SpriteMonsterAnimationDriver driver)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance", Private).Invoke(driver, new object[] { 0f });
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate", Private).Invoke(driver, null);
    }
}
