using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatFlashPresentationTests
{
    private bool previous;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [SetUp] public void Remember() { previous = CombatFlashPresentation.Reduced; CombatFlashPresentation.Apply(false); }
    [TearDown] public void Restore() => CombatFlashPresentation.Apply(previous);

    [Test]
    public void LegacySettingsAndBothChoicesRoundTripWithoutLosingOtherPreferences()
    {
        Assert.That(JsonUtility.FromJson<GameUserSettings>("{\"cameraShake\":1}").reduceFlashes, Is.False);
        foreach (bool enabled in new[] { false, true })
        {
            var data = new GameUserSettings { reduceFlashes = enabled, cameraShake = 1, effectsVolume = .4f };
            var loaded = JsonUtility.FromJson<GameUserSettings>(JsonUtility.ToJson(data));
            Assert.That(loaded.reduceFlashes, Is.EqualTo(enabled));
            Assert.That(loaded.cameraShake, Is.EqualTo(1));
            Assert.That(loaded.effectsVolume, Is.EqualTo(.4f));
        }
    }

    [Test]
    public void ReducedFlashesPreserveHueOpacityAndDarkColorsExceptForExplicitFlashLayers()
    {
        var color = new Color(1.2f, .6f, .3f, .8f);
        Assert.That(CombatFlashPresentation.Peak(color), Is.EqualTo(color));
        Assert.That(CombatFlashPresentation.Flash(color), Is.EqualTo(color));
        CombatFlashPresentation.Apply(true);
        var peak = CombatFlashPresentation.Peak(color);
        Assert.That(peak.r, Is.EqualTo(.68f).Within(.001f));
        Assert.That(peak.g / peak.r, Is.EqualTo(.5f).Within(.001f));
        Assert.That(peak.a, Is.EqualTo(color.a));
        Assert.That(CombatFlashPresentation.Flash(color).a, Is.EqualTo(.2f).Within(.001f));
        Assert.That(CombatFlashPresentation.Peak(new Color(.2f, .3f, .4f, .5f)), Is.EqualTo(new Color(.2f,.3f,.4f,.5f)));
        Assert.That(Shader.GetGlobalFloat("_CombatFlashReduction"), Is.EqualTo(1f));
    }

    [Test]
    public void CommonHitSpriteUpdatesOnToggleWithoutChangingFrameOrSize()
    {
        var fx = PixelCombatEffect.PlayCritical(null, null);
        try
        {
            Assert.That(fx, Is.Not.Null);
            var renderer = fx.GetComponent<SpriteRenderer>();
            var sprite = renderer.sprite; var scale = fx.transform.localScale; var tint = renderer.color;
            CombatFlashPresentation.Apply(true);
            typeof(PixelCombatEffect).GetMethod("LateUpdate", Private).Invoke(fx, null);
            Assert.That(renderer.color.r, Is.LessThan(tint.r));
            Assert.That(renderer.color.a, Is.EqualTo(tint.a));
            Assert.That(renderer.sprite, Is.SameAs(sprite)); Assert.That(fx.transform.localScale, Is.EqualTo(scale));
            CombatFlashPresentation.Apply(false);
            typeof(PixelCombatEffect).GetMethod("LateUpdate", Private).Invoke(fx, null);
            Assert.That(renderer.color, Is.EqualTo(tint));
        }
        finally { if(fx!=null)Object.DestroyImmediate(fx.gameObject); }
    }

    [Test]
    public void SupernovaKeepsLandingShadowRadiusAndDebrisWhileItsGroundFlashDimsAndRestores()
    {
        var fx = PixelWizardSupernova.Create(Vector3.zero, Vector3.left, 2.5f, null, Color.cyan, () => true);
        try
        {
            fx.BeginDescent();
            var shadow = fx.transform.Find("Growing Landing Shadow").GetComponent<SpriteRenderer>();
            var shadowColor = shadow.color; var shadowScale = shadow.transform.localScale;
            CombatFlashPresentation.Apply(true); fx.BeginDescent();
            Assert.That(shadow.color, Is.EqualTo(shadowColor)); Assert.That(shadow.transform.localScale, Is.EqualTo(shadowScale));
            CombatFlashPresentation.Apply(false); fx.Impact();
            var flash = fx.transform.Find("Supernova Ground Flash").GetComponent<SpriteRenderer>();
            Color original = flash.color; Vector3 scale = flash.transform.localScale;
            int children = fx.transform.childCount;
            CombatFlashPresentation.Apply(true); fx.Impact();
            Assert.That(flash.color.a, Is.EqualTo(original.a * .25f).Within(.001f));
            Assert.That(flash.transform.localScale, Is.EqualTo(scale)); Assert.That(fx.transform.childCount, Is.EqualTo(children));
            CombatFlashPresentation.Apply(false); fx.Impact(); Assert.That(flash.color, Is.EqualTo(original));
        }
        finally { Object.DestroyImmediate(fx.gameObject); }
    }

    [Test]
    public void ConstellationRetainsFiveBoundaryLinesAndTheirAlphaWhileReducingFilledPulse()
    {
        var fx = PixelWizardPentagramField.Create(Vector3.zero, 2.2f, null, null, () => true);
        try
        {
            fx.Complete(); fx.Pulse();
            var fill = (MeshRenderer)typeof(PixelWizardPentagramField).GetField("fill", Private).GetValue(fx);
            var block = new MaterialPropertyBlock(); fill.GetPropertyBlock(block); Color original = block.GetColor("_Color");
            var lines = fx.GetComponentsInChildren<LineRenderer>();
            Vector3 end = lines[0].GetPosition(1); float alpha = lines[0].startColor.a;
            CombatFlashPresentation.Apply(true); fx.Pulse(); fill.GetPropertyBlock(block);
            Assert.That(block.GetColor("_Color").a, Is.EqualTo(original.a * .25f).Within(.001f));
            Assert.That(lines.Length, Is.EqualTo(5)); Assert.That(lines[0].enabled, Is.True);
            Assert.That(lines[0].GetPosition(1), Is.EqualTo(end)); Assert.That(lines[0].startColor.a, Is.EqualTo(alpha));
            CombatFlashPresentation.Apply(false); fx.Pulse(); fill.GetPropertyBlock(block);
            Assert.That(block.GetColor("_Color"), Is.EqualTo(original));
        }
        finally { Object.DestroyImmediate(fx.gameObject); }
    }

    [Test]
    public void WolfDetonationWarningBuildsContinuouslyInsteadOfStrobing()
    {
        var body = Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/029_nukgu"));
        SpriteRenderer ghost = null;
        try
        {
            var source = body.GetComponentInChildren<SpriteRenderer>();
            var driver = body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            ghost = PixelCombatEffect.PlayWolfHuntAfterimage(source, Vector3.zero, Vector3.zero, Color.cyan, 4f);
            var fx = ghost.GetComponent<PixelWolfHuntAfterimage>();
            fx.PrepareCompanion(source, driver, () => Vector3.zero, () => true, Color.cyan);
            fx.FinishCompanion(); CombatFlashPresentation.Apply(true);
            var animate = typeof(PixelWolfHuntAfterimage).GetMethod("AnimateCompanion", Private);
            animate.Invoke(fx, new object[] { WolfCombatRules.EclipseIdleDuration });
            Color previousColor = ghost.color;
            for(int i=0;i<30;i++)
            {
                animate.Invoke(fx, new object[] { .02f });
                Assert.That(Mathf.Abs(ghost.color.a-previousColor.a), Is.LessThan(.01f));
                Assert.That(Mathf.Abs(ghost.color.r-previousColor.r), Is.LessThan(.02f));
                Assert.That(ghost.color.a, Is.GreaterThan(.7f));
                Assert.That(ghost.transform.position, Is.EqualTo(Vector3.zero));
                previousColor=ghost.color;
            }
        }
        finally { if(ghost!=null)Object.DestroyImmediate(ghost.gameObject); Object.DestroyImmediate(body); }
    }

    [Test]
    public void ImportedThunderDimmingIsPerInstanceAndRestoresWithoutEditingMaterialAssets()
    {
        var fx = PixelHaetaeThunder.Create(Vector3.zero, 0, null);
        try
        {
            fx.Pose(HaetaeRank3Rules.HeavenlyDelay + .08f);
            var renderers = fx.GetComponentsInChildren<ParticleSystemRenderer>(true);
            int verified = 0; var block = new MaterialPropertyBlock();
            CombatFlashPresentation.Apply(true); fx.Pose(HaetaeRank3Rules.HeavenlyDelay + .08f);
            foreach (var r in renderers)
            {
                var m = r.sharedMaterial; if (m == null || !m.HasProperty("_EmissiveIntensity")) continue;
                float original = m.GetFloat("_EmissiveIntensity"); r.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_EmissiveIntensity"), Is.EqualTo(Mathf.Min(.75f, original*.2f)).Within(.001f));
                Assert.That(m.GetFloat("_EmissiveIntensity"), Is.EqualTo(original)); verified++;
            }
            Assert.That(verified, Is.GreaterThan(0));
            CombatFlashPresentation.Apply(false); fx.Pose(HaetaeRank3Rules.HeavenlyDelay + .08f);
            foreach(var r in renderers)
                if(r.sharedMaterial!=null && r.sharedMaterial.HasProperty("_EmissiveIntensity"))
                { r.GetPropertyBlock(block); Assert.That(block.GetFloat("_EmissiveIntensity"), Is.EqualTo(r.sharedMaterial.GetFloat("_EmissiveIntensity"))); }
        }
        finally { Object.DestroyImmediate(fx.gameObject); }
    }
}
