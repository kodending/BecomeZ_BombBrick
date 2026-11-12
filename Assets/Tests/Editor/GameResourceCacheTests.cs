using System.Collections;
using System.Linq;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GameResourceCacheTests
{
    [UnityTest]
    public IEnumerator CombatEffectWarmupRetainsThunderTemplateWithoutSpawningOrConsumingRandom()
    {
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var templateField=typeof(PixelHaetaeThunder).GetField("template",flags);
        var completeField=typeof(GameResourceCache).GetField("effectWarmupComplete",flags);
        var runningField=typeof(GameResourceCache).GetField("effectWarmupRunning",flags);
        object previousTemplate=templateField.GetValue(null);
        bool previousComplete=(bool)completeField.GetValue(null);
        Assert.That(runningField.GetValue(null),Is.False);
        int effects=Resources.FindObjectsOfTypeAll<PixelHaetaeThunder>().Count(fx=>fx.gameObject.scene.IsValid());
        int particles=Resources.FindObjectsOfTypeAll<ParticleSystem>().Count(ps=>ps.gameObject.scene.IsValid());
        var random=Random.state;
        templateField.SetValue(null,null);completeField.SetValue(null,false);
        try
        {
            yield return GameResourceCache.WarmupCombatEffects();
            var prepared=(PixelHaetaeThunder)templateField.GetValue(null);
            Assert.That(prepared,Is.Not.Null);
            Assert.That(prepared,Is.SameAs(Resources.Load<PixelHaetaeThunder>(PixelHaetaeThunder.ResourcePath)));
            Assert.That(prepared.gameObject.scene.IsValid(),Is.False);
            Assert.That(prepared.variants.Length,Is.EqualTo(4));
            CollectionAssert.AllItemsAreNotNull(prepared.variants);
            Assert.That(PixelHaetaeThunder.ImpactClip,Is.SameAs(prepared.impactSound));
            Assert.That(((IEnumerator)typeof(PixelHaetaeThunder).GetMethod("WarmupTemplate",flags)
                .Invoke(null,null)).MoveNext(),Is.False);
            Assert.That(GameResourceCache.WarmupCombatEffects().MoveNext(),Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<PixelHaetaeThunder>().Count(fx=>fx.gameObject.scene.IsValid()),Is.EqualTo(effects));
            Assert.That(Resources.FindObjectsOfTypeAll<ParticleSystem>().Count(ps=>ps.gameObject.scene.IsValid()),Is.EqualTo(particles));
            Assert.That(Random.state,Is.EqualTo(random));
        }
        finally
        {
            templateField.SetValue(null,previousTemplate);
            completeField.SetValue(null,previousComplete);
            runningField.SetValue(null,false);
        }
    }

    private static int warmupConstructorCalls;
    private sealed class WarmupConstructorProbe
    {
        public WarmupConstructorProbe()
        {
            warmupConstructorCalls++;
            Random.Range(0,100);
        }
    }

    [Test]
    public void EditorMethodPreparationDoesNotExecuteConstructorsOrConsumeRandom()
    {
        warmupConstructorCalls=0;
        var random=Random.state;
        var preparation=GameResourceCache.WarmupEditorMethods(typeof(WarmupConstructorProbe));
        while(preparation.MoveNext()) { }
        Assert.That(warmupConstructorCalls,Is.Zero);
        Assert.That(Random.state,Is.EqualTo(random));
    }

    [UnityTest]
    public IEnumerator PreparationIconsAreLoadedBeforeTheirFirstDrawAndReused()
    {
        yield return GameResourceCache.WarmupCommon();
        foreach(string id in new[]{"life","deployment","gold","augment","shop","reroll","fusion","twin_command"})
        {
            string path="UI/SystemIcons/"+id;
            Assert.That(GameResourceCache.WarmupTexture(path).MoveNext(),Is.False,path);
            Assert.That(GameResourceCache.GetTexture(path),Is.SameAs(Resources.Load<Texture2D>(path)));
        }
        foreach(string name in System.Enum.GetNames(typeof(CombatStatusKind))
            .Concat(System.Enum.GetNames(typeof(CombatEffectIconKind))).Distinct())
            Assert.That(GameResourceCache.WarmupTexture("UI/CombatStatusIcons/"+name).MoveNext(),Is.False,name);
        string augment="UI/AugmentIcons/augment_auto_deployment";
        yield return GameResourceCache.WarmupTexture(augment);
        Assert.That(GameResourceCache.GetTexture(augment),Is.Not.Null);
        Assert.That(GameResourceCache.WarmupTexture(augment).MoveNext(),Is.False);
    }

    [Test]
    public void AuthoredUiClickSoundExists()
    {
        Assert.That(Resources.Load<AudioClip>(
            "Sounds/Menu Pop Up Open 1"), Is.Not.Null);
    }

    [Test]
    public void AuthoredMonsterPlacementSoundExists()
    {
        Assert.That(Resources.Load<AudioClip>(
            "Sounds/45_Landing_01"), Is.Not.Null);
    }

    [Test]
    public void AuthoredCombatHitSoundsExist()
    {
        Assert.That(Resources.Load<AudioClip>(
            "Sounds/Play Game Mech Hit 5"), Is.Not.Null);
        Assert.That(Resources.Load<AudioClip>(
            "Sounds/Play Game Mech Hit 7"), Is.Not.Null);
    }

    [Test]
    public void AuthoredCombatDeathSoundExists()
    {
        Assert.That(Resources.Load<AudioClip>(
            "Sounds/Ray_Spell_v5_wav"), Is.Not.Null);
    }

    [Test]
    public void AuthoredSlideTransitionSoundsExist()
    {
        Assert.That(Resources.Load<AudioClip>(
            "Sounds/Organice Ice Next Page Slide Transition 1"), Is.Not.Null);
        Assert.That(Resources.Load<AudioClip>(
            "Sounds/Organice Ice Next Page Slide Transition 2"), Is.Not.Null);
    }

    [Test]
    public void AuthoredWaveResultSoundExists()
    {
        Assert.That(Resources.Load<AudioClip>("Sounds/Tribal Drum"), Is.Not.Null);
    }

    [Test]
    public void AuthoredBootLogoSoundExists()
    {
        Assert.That(Resources.Load<AudioClip>("Sounds/Special 4-6"), Is.Not.Null);
    }

    [Test]
    public void TownAndRegionPreviewTexturesExist()
    {
        Assert.That(Resources.Load<Texture2D>(
            "Environment/Town/TownExpeditionHub"), Is.Not.Null);
        foreach (HunterRegionDefinition region in HunterRegionCatalog.All)
        {
            Assert.That(Resources.Load<Texture2D>(
                "Environment/RegionIllustrations/" + region.id),
                Is.Not.Null, region.id);
        }
    }

    [UnityTest]
    public IEnumerator MonsterPreviewPreparationAloneDoesNotReleaseStartupGate()
    {
        var gameplayComplete = typeof(GameResourceCache).GetField("gameplayWarmupComplete",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        object previous = gameplayComplete.GetValue(null);
        gameplayComplete.SetValue(null, false);
        try
        {
            yield return GameResourceCache.WarmupStartup();
            yield return GameResourceCache.WarmupCommon();
            yield return GameResourceCache.WarmupMonsterPreviews();

            MonsterDatabase database = GameResourceCache.Database;
            Assert.That(database, Is.Not.Null);
            Assert.That(GameResourceCache.WarmupProgress, Is.EqualTo(1f));
            Assert.That(GameResourceCache.MonsterPreviewProgress,
                Is.EqualTo(1f));
            Assert.That(GameResourceCache.StartupProgress, Is.LessThan(1f));
            Assert.That(GameResourceCache.StartupWarmupComplete, Is.False);
            MonsterDefinition definition = database.monsters.First(monster =>
                monster != null && monster.enabled &&
                !string.IsNullOrWhiteSpace(monster.resourcePath));
            Assert.That(
                GameResourceCache.GetMonsterIdleSprite(definition, 0f),
                Is.Not.Null,
                definition.monsterId + " preview was not warmed up.");
            foreach (var monster in database.monsters.Where(monster => monster != null && monster.enabled))
                foreach (string path in new[] { monster.resourcePath, monster.shinyResourcePath }
                    .Where(path => !string.IsNullOrWhiteSpace(path)))
                    Assert.That(GameResourceCache.GetMonsterIdleSprite(monster, 0f, path),
                        Is.Not.Null, path + " must be ready for collection and arbitrary starter rerolls.");
        }
        finally { gameplayComplete.SetValue(null, previous); }
    }
}
