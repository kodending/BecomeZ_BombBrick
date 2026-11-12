using BombBrick.Game.Core;
using BombBrick.Game.Scenes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools.Constraints;
using Is=UnityEngine.TestTools.Constraints.Is;

public sealed class GameFrontendTests
{
    [Test]
    public void ProductionSceneQueriesReuseNamesAndKeepSceneRequestAndTestModeChangesLive()
    {
        Scene originalScene=SceneManager.GetActiveScene(),production=default,expedition=default;
        string productionPath=null;
        foreach(string path in new[]{GameSceneCatalog.TownPath,GameSceneCatalog.ResultPath,
                    GameSceneCatalog.BattlePath,GameSceneCatalog.BootPath})
            if(!SceneManager.GetSceneByPath(path).isLoaded){productionPath=path;break;}
        Assert.That(productionPath,Is.Not.Null,"The fixture needs one unloaded production scene.");
        var singleton=typeof(GameFlowController).GetField("instance",
            System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        object previousFlow=singleton.GetValue(null);
        bool previousTestMode=CombatEffectTestConfiguration.Active,openedExpedition=false;
        var host=new GameObject("Production query test");host.SetActive(false);
        var flow=host.AddComponent<GameFlowController>();singleton.SetValue(null,flow);
        try
        {
            CombatEffectTestConfiguration.Active=false;
            production=EditorSceneManager.OpenScene(productionPath,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(production);
            Assert.That(GameFrontend.IsProduction,Is.True);
            bool productionQueries=true;
            TestDelegate query=()=>{for(int i=0;i<256;i++)productionQueries&=GameFrontend.IsProduction;};
            query();Assert.That(query,Is.Not.AllocatingGCMemory());
            Assert.That(productionQueries,Is.True);

            expedition=SceneManager.GetSceneByPath(GameSceneCatalog.ExpeditionBattlePath);
            if(!expedition.isLoaded)
            {
                expedition=EditorSceneManager.OpenScene(GameSceneCatalog.ExpeditionBattlePath,OpenSceneMode.Additive);
                openedExpedition=true;
            }
            int frame=Time.frameCount;
            SceneManager.SetActiveScene(production);
            Assert.That(GameFrontend.IsProduction,Is.True);
            SceneManager.SetActiveScene(expedition);
            Assert.That(GameFrontend.IsProduction,Is.False);
            Assert.That(Time.frameCount,Is.EqualTo(frame));
            flow.Run.BeginExpedition("prototype_region",false);
            Assert.That(GameFrontend.IsProduction,Is.True);
            CombatEffectTestConfiguration.Active=true;
            Assert.That(GameFrontend.IsProduction,Is.False);
            CombatEffectTestConfiguration.Active=false;
            Assert.That(GameFrontend.IsProduction,Is.True);
            flow.Run.LeaveExpeditionFlow();
            Assert.That(GameFrontend.IsProduction,Is.False);

            int previousHandle=production.handle;
            EditorSceneManager.CloseScene(production,true);
            production=EditorSceneManager.OpenScene(productionPath,OpenSceneMode.Additive);
            Assert.That(production.handle,Is.Not.EqualTo(previousHandle));
            SceneManager.SetActiveScene(production);
            Assert.That(GameFrontend.IsProduction,Is.True);
        }
        finally
        {
            SceneManager.SetActiveScene(originalScene);
            if(production.IsValid()&&production.isLoaded)EditorSceneManager.CloseScene(production,true);
            if(openedExpedition&&expedition.IsValid()&&expedition.isLoaded)EditorSceneManager.CloseScene(expedition,true);
            Object.DestroyImmediate(host);singleton.SetValue(null,previousFlow);
            CombatEffectTestConfiguration.Active=previousTestMode;
        }
    }

    [Test]
    public void CreditCatLoopsIdlePlaysSpecialOnceAndReturnsToIdleEvenWhilePaused()
    {
        var host = new GameObject("Credits Cat Test");
        host.SetActive(false);
        var frontend = host.AddComponent<GameFrontend>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(GameFrontend);
        float speed = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            type.GetField("creditsChundongiCycleStartedAt", flags).SetValue(frontend, 0f);
            type.GetField("creditsChundongiSpecialAt", flags).SetValue(frontend, 10f);
            var sample = type.GetMethod("CreditsChundongiFrame", flags);
            object[] args = { .875f, false };
            Assert.That(sample.Invoke(frontend, args), Is.EqualTo(7));
            Assert.That(args[1], Is.False);
            args[0] = 1f;
            Assert.That(sample.Invoke(frontend, args), Is.EqualTo(0));
            args[0] = 10f;
            Assert.That(sample.Invoke(frontend, args), Is.EqualTo(0));
            Assert.That(args[1], Is.True);
            args[0] = 10.875f;
            Assert.That(sample.Invoke(frontend, args), Is.EqualTo(7));
            args[0] = 11f;
            Assert.That(sample.Invoke(frontend, args), Is.EqualTo(0));
            Assert.That(args[1], Is.False);
            Assert.That(type.GetField("creditsChundongiSpecialAt", flags).GetValue(frontend), Is.InRange(17f, 23f));
        }
        finally { Time.timeScale = speed; Object.DestroyImmediate(host); }
    }

    [TestCase("South_Idle")]
    [TestCase("Special")]
    public void CreditCatSheetsKeepAllEightOriginalPixelFrames(string motion)
    {
        var sheet = Resources.Load<Texture2D>("UI/Chundongi/" + motion);
        Assert.That(sheet, Is.Not.Null);
        Assert.That(sheet.width, Is.EqualTo(512));
        Assert.That(sheet.height, Is.EqualTo(64));
        Assert.That(sheet.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(sheet.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        Assert.That(sheet.mipmapCount, Is.EqualTo(1));
    }

    [Test]
    public void MemorialClosesWithEscapeAndKeepsAllCreditNoticesReachable()
    {
        var host = new GameObject("Memorial UI Test");
        host.SetActive(false);
        var frontend = host.AddComponent<GameFrontend>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(GameFrontend);
        try
        {
            type.GetField("chundongiMemorialOpen", flags).SetValue(frontend, true);
            type.GetMethod("HandleEscape", flags).Invoke(frontend, null);
            Assert.That(type.GetField("chundongiMemorialOpen", flags).GetValue(frontend), Is.False);
            Assert.That(type.GetField("menuOpen", flags).GetValue(frontend), Is.False);
            type.GetMethod("SelectCreditsPage", flags).Invoke(frontend, new object[] { 1 });
            Assert.That(type.GetField("creditsNotice", flags).GetValue(frontend), Is.EqualTo(""));
            for (int i = 0; i < GameCredits.NoticeFiles.Count; i++)
            {
                type.GetMethod("SelectCreditsPage", flags).Invoke(frontend, new object[] { i + 2 });
                Assert.That(type.GetField("creditsNotice", flags).GetValue(frontend), Is.EqualTo(GameCredits.ReadNotice(i)));
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase("researchOpen")]
    [TestCase("encyclopediaOpen")]
    public void EscapeClosesTheTownModalBeforeOpeningTheSystemMenu(string modalFieldName)
    {
        Scene original = SceneManager.GetActiveScene();
        Scene townScene = SceneManager.GetSceneByPath(GameSceneCatalog.TownPath);
        bool openedTown = !townScene.isLoaded;
        if (openedTown)
            townScene = EditorSceneManager.OpenScene(GameSceneCatalog.TownPath, OpenSceneMode.Additive);
        var host = new GameObject("Town Escape Test");
        host.SetActive(false);
        try
        {
            SceneManager.SetActiveScene(townScene);
            var town = Object.FindObjectOfType<TownSceneController>();
            Assert.That(town, Is.Not.Null);
            var frontend = host.AddComponent<GameFrontend>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var modal = typeof(TownSceneController).GetField(modalFieldName, flags);
            var reset = typeof(TownSceneController).GetField("researchResetConfirmationUntil", flags);
            object previousModal = modal.GetValue(town), previousReset = reset.GetValue(town);
            try
            {
                modal.SetValue(town, true);
                reset.SetValue(town, 10f);
                typeof(GameFrontend).GetMethod("HandleEscape", flags).Invoke(frontend, null);
                Assert.That(modal.GetValue(town), Is.False);
                Assert.That(typeof(GameFrontend).GetField("menuOpen", flags).GetValue(frontend), Is.False);
                Assert.That(town.TryClosePreparationModal(), Is.False);
                if (modalFieldName == "researchOpen")
                    Assert.That(reset.GetValue(town), Is.EqualTo(0f));
            }
            finally { modal.SetValue(town, previousModal); reset.SetValue(town, previousReset); }
        }
        finally
        {
            Object.DestroyImmediate(host);
            SceneManager.SetActiveScene(original);
            if (openedTown) EditorSceneManager.CloseScene(townScene, true);
        }
    }

    [Test]
    public void CameraShakeSettingsPreserveLegacyDefaultsAndRoundTripAllChoices()
    {
        var legacy = JsonUtility.FromJson<GameUserSettings>("{\"width\":1280}");
        legacy.Normalize();
        Assert.That(legacy.cameraShake, Is.EqualTo(2));
        Assert.That(legacy.CameraShakeScale, Is.EqualTo(1f));
        foreach (int choice in new[] { 0, 1, 2, -1, 99 })
        {
            var settings = new GameUserSettings { cameraShake = choice };
            settings.Normalize();
            var loaded = JsonUtility.FromJson<GameUserSettings>(JsonUtility.ToJson(settings));
            Assert.That(loaded.cameraShake, Is.EqualTo(choice < 0 || choice > 2 ? 2 : choice));
            Assert.That(loaded.CameraShakeScale, Is.EqualTo(choice == 0 ? 0f : choice == 1 ? 0.35f : 1f));
        }
    }

    [Test]
    public void NewSoundSettingsStartAtFullVolume()
    {
        var settings = new GameUserSettings();
        Assert.That(settings.masterVolume, Is.EqualTo(1f));
        Assert.That(settings.musicVolume, Is.EqualTo(1f));
        Assert.That(settings.effectsVolume, Is.EqualTo(1f));
    }

    [Test]
    public void SettingsNormalizeCorruptedValuesWithoutTouchingGameProfile()
    {
        var settings = new GameUserSettings
        {
            width = -1, height = int.MaxValue, frameRate = -10,
            masterVolume = float.NaN, musicVolume = 5f, effectsVolume = -8f
        };
        settings.Normalize();
        Assert.That(settings.width, Is.EqualTo(960));
        Assert.That(settings.height, Is.EqualTo(4320));
        Assert.That(settings.frameRate, Is.EqualTo(60));
        Assert.That(settings.masterVolume, Is.InRange(0f, 1f));
        Assert.That(settings.musicVolume, Is.EqualTo(1f));
        Assert.That(settings.effectsVolume, Is.Zero);
        Assert.That(GameUserSettings.PreferenceKey, Does.Not.Contain("profile"));
    }

    [Test]
    public void DisplayRollbackSnapshotIsIndependentAndSettingsRoundTrip()
    {
        var original = new GameUserSettings { width = 1280, height = 720, frameRate = 120, showFps = true };
        GameUserSettings copy = original.Copy();
        copy.width = 3440;
        Assert.That(original.width, Is.EqualTo(1280));
        var loaded = JsonUtility.FromJson<GameUserSettings>(JsonUtility.ToJson(original));
        Assert.That(loaded.width, Is.EqualTo(1280));
        Assert.That(loaded.frameRate, Is.EqualTo(120));
        Assert.That(loaded.showFps, Is.True);
    }

    [TestCase(960, 540)]
    [TestCase(1280, 720)]
    [TestCase(1600, 900)]
    [TestCase(1920, 1080)]
    [TestCase(1920, 1200)]
    [TestCase(2560, 1440)]
    [TestCase(3440, 1440)]
    public void FrontendPanelFitsSupportedAspectRatios(int width, int height)
    {
        Rect safe = new Rect(12, 18, width - 24, height - 36);
        Rect panel = GameFrontend.FitPanel(safe, new Vector2(960, 540));
        Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(safe.xMin));
        Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(safe.yMin));
        Assert.That(panel.xMax, Is.LessThanOrEqualTo(safe.xMax));
        Assert.That(panel.yMax, Is.LessThanOrEqualTo(safe.yMax));
        Assert.That(panel.width / panel.height, Is.EqualTo(16f / 9f).Within(0.001f));
    }

    [Test]
    [TestCase(960, 540)]
    [TestCase(1280, 720)]
    [TestCase(1920, 1080)]
    [TestCase(1920, 1200)]
    [TestCase(1024, 768)]
    [TestCase(3440, 1440)]
    [TestCase(5120, 1440)]
    [TestCase(1080, 1920)]
    public void MainSkyWrapsWholeCloudsAndFlocksBeyondPhysicalScreen(int width, int height)
    {
        Rect viewport = GameFrontend.FitPanel(
            new Rect(12, 18, width - 24, height - 36), new Vector2(960, 540));
        Rect screen = GameFrontend.MainScreenRect(viewport, new Vector2(width, height));
        float scale = viewport.width / 960f;
        Assert.That(screen.xMin * scale + viewport.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(screen.yMin * scale + viewport.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(screen.xMax * scale + viewport.x, Is.EqualTo(width).Within(0.001f));
        Assert.That(screen.yMax * scale + viewport.y, Is.EqualTo(height).Within(0.001f));

        foreach (Vector2 extents in new[] { new Vector2(0, 55), new Vector2(0, 44), new Vector2(36, 11) })
        {
            float enter = GameFrontend.SkyTravelX(screen, 0f, extents.x, extents.y);
            float exit = GameFrontend.SkyTravelX(screen, 0.99999f, extents.x, extents.y);
            Assert.That(enter + extents.y, Is.LessThan(screen.xMin));
            Assert.That(exit - extents.x, Is.GreaterThan(screen.xMax));
            Assert.That(GameFrontend.SkyTravelX(screen, 1f, extents.x, extents.y), Is.EqualTo(enter));
            Assert.That(GameFrontend.SkyTravelX(screen, -0.00001f, extents.x, extents.y) - extents.x,
                Is.GreaterThan(screen.xMax), "Left-moving clouds wrap outside the right edge.");
            Assert.That(GameFrontend.SkyTravelX(screen, 0.5f, extents.x, extents.y),
                Is.InRange(screen.xMin, screen.xMax));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MenuOcclusionKeepsTreesInFrontOfBirdsAndMountainsBehindBirds(bool treesOnly)
    {
        Texture2D source = Resources.Load<Texture2D>("Environment/MainMenu/MainMenuExpeditionBase");
        Texture2D layer = GameFrontend.CreateMenuSkyOccluder(source, treesOnly);
        RenderTexture previous = RenderTexture.active;
        RenderTexture copy = RenderTexture.GetTemporary(layer.width, layer.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D readable = new Texture2D(layer.width, layer.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(layer, copy);
            RenderTexture.active = copy;
            readable.ReadPixels(new Rect(0, 0, layer.width, layer.height), 0, 0, false);
            foreach (Vector2Int point in new[] { new Vector2Int(600, 80), new Vector2Int(700, 80),
                new Vector2Int(720, 40), new Vector2Int(1330, 180), new Vector2Int(1280, 380),
                new Vector2Int(200, 240), new Vector2Int(1480, 330), new Vector2Int(1440, 500) })
                Assert.That(readable.GetPixel(point.x, layer.height - 1 - point.y).a,
                    Is.EqualTo(1f), "Foreground silhouette at " + point);
            foreach (Vector2Int point in new[] { new Vector2Int(991, 210), new Vector2Int(1000, 300),
                new Vector2Int(550, 300), new Vector2Int(1620, 250), new Vector2Int(1100, 340),
                new Vector2Int(600, 420) })
                Assert.That(readable.GetPixel(point.x, layer.height - 1 - point.y).a,
                    Is.EqualTo(treesOnly ? 0f : 1f), "Distant scenery at " + point);
            foreach (Vector2Int point in new[] { new Vector2Int(900, 0), new Vector2Int(1000, 60),
                new Vector2Int(900, 120), new Vector2Int(1100, 240), new Vector2Int(850, 300) })
                Assert.That(readable.GetPixel(point.x, layer.height - 1 - point.y).a,
                    Is.Zero, "Open sky at " + point);
            Assert.That(layer.filterMode, Is.EqualTo(FilterMode.Point));
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(copy);
            Object.DestroyImmediate(layer);
            Object.DestroyImmediate(readable);
        }
    }

    [Test]
    public void MenuCatPeeksThenFullyHidesAndStaysAnchoredToCroppedArtwork()
    {
        const float hold = 2f;
        Assert.That(GameFrontend.MenuPeekAmount(-1f, hold), Is.Zero);
        Assert.That(GameFrontend.MenuPeekAmount(0f, hold), Is.Zero);
        Assert.That(GameFrontend.MenuPeekAmount(0.54f, hold), Is.InRange(0.4f, 0.6f));
        Assert.That(GameFrontend.MenuPeekAmount(1.1f, hold), Is.EqualTo(1f));
        Assert.That(GameFrontend.MenuPeekAmount(3.38f, hold), Is.InRange(0.4f, 0.6f));
        Assert.That(GameFrontend.MenuPeekAmount(3.69f, hold), Is.Zero);
        Assert.That(GameFrontend.MenuPeekAmount(20f, hold), Is.Zero);
        Assert.That(GameFrontend.MenuPeekFrame(-1f, hold), Is.EqualTo(-1));
        Assert.That(GameFrontend.MenuPeekFrame(0f, hold), Is.Zero);
        Assert.That(GameFrontend.MenuPeekFrame(0.6f, hold), Is.EqualTo(5));
        Assert.That(GameFrontend.MenuPeekFrame(1.1f, hold), Is.EqualTo(8));
        Assert.That(GameFrontend.MenuPeekFrame(2.04f, hold), Is.EqualTo(9));
        Assert.That(GameFrontend.MenuPeekFrame(2.20f, hold), Is.EqualTo(10));
        Assert.That(GameFrontend.MenuPeekFrame(2.6f, hold), Is.EqualTo(8));
        Assert.That(GameFrontend.MenuPeekFrame(3.24f, hold), Is.EqualTo(12));
        Assert.That(GameFrontend.MenuPeekFrame(3.69f, hold), Is.EqualTo(-1));
        Texture2D source = Resources.Load<Texture2D>("Environment/MainMenu/ChundongiPeekAtlas_Pixel");
        Assert.That(source, Is.Not.Null);
        Assert.That(source.width, Is.EqualTo(1254), "Do not blend away source pixels during import.");
        Texture2D atlas = GameFrontend.CreateMenuPixelAtlas(source);
        try
        {
            Assert.That(atlas.width, Is.EqualTo(128), "4 columns of 32-pixel frames, rendered with Point sampling.");
            Assert.That(atlas.height, Is.EqualTo(128));
            Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Point));
        }
        finally { Object.DestroyImmediate(atlas); }
        foreach (Vector2 size in new[] { new Vector2(1920, 1080), new Vector2(1920, 1200),
            new Vector2(3440, 1440) })
        {
            Rect screen = new Rect(Vector2.zero, size);
            Rect art = GameFrontend.MainArtworkRect(screen, new Vector2(1672, 941));
            Assert.That(art.center, Is.EqualTo(screen.center));
            Assert.That(art.width, Is.GreaterThanOrEqualTo(screen.width - 0.001f));
            Assert.That(art.height, Is.GreaterThanOrEqualTo(screen.height - 0.001f));
            Assert.That(art.width / 1672f, Is.EqualTo(art.height / 941f).Within(0.001f));
            Vector2 tree = art.position + new Vector2(1400f, 500f) * (art.width / 1672f);
            Assert.That(screen.Contains(tree), Is.True, "Right arch remains the peek anchor.");
        }
    }

    [Test]
    public void GeneratedAudioHasBoundedAmplitudeAndSilentTail()
    {
        AudioClip clip = GameAudio.Tone("QA", new[] { 262f, 0f, 392f }, 0.05f);
        try
        {
            float[] samples = new float[clip.samples];
            clip.GetData(samples, 0);
            foreach (float sample in samples)
                Assert.That(sample, Is.InRange(-0.33f, 0.33f));
            Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(0.001f));
            Assert.That(clip.channels, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(clip); }
    }

    [Test]
    public void TownBackgroundMusicResourceIsAvailable()
    {
        AudioClip clip = Resources.Load<AudioClip>("Sounds/TownAfternoonBGM");
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.length, Is.GreaterThan(60f));
    }

    [Test]
    public void CachedStartupStillShowsLoadingFramesBeforeReleasingMainMenu()
    {
        const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        const System.Reflection.BindingFlags staticFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var mainComplete = typeof(GameResourceCache).GetField("mainWarmupComplete", staticFlags);
        var gameplayComplete = typeof(GameResourceCache).GetField("gameplayWarmupComplete", staticFlags);
        object previousMain = mainComplete.GetValue(null), previousGameplay = gameplayComplete.GetValue(null);
        var host = new GameObject("Startup loading gate test");
        host.SetActive(false);
        var frontend = host.AddComponent<GameFrontend>();
        var ready = typeof(GameFrontend).GetField("startupResourcesReady", instanceFlags);
        var logoStarted = typeof(GameFrontend).GetField("bootLogoStartedAt", instanceFlags);
        var startup = (System.Collections.IEnumerator)typeof(GameFrontend).GetMethod("WarmupStartupResources", instanceFlags).Invoke(frontend, null);
        try
        {
            mainComplete.SetValue(null, true);
            gameplayComplete.SetValue(null, true);
            logoStarted.SetValue(frontend, Time.unscaledTime + 100f);
            Assert.That(startup.MoveNext(), Is.True);
            Assert.That(((System.Collections.IEnumerator)startup.Current).MoveNext(), Is.False, "Main resources are already cached.");
            Assert.That(ready.GetValue(frontend), Is.False);
            Assert.That(startup.MoveNext(), Is.True);
            Assert.That(startup.Current, Is.Null, "The logo must finish before full resource preparation begins.");
            Assert.That(ready.GetValue(frontend), Is.False);

            logoStarted.SetValue(frontend, Time.unscaledTime - GameFrontend.BootLogoDuration - 1f);
            typeof(GameFrontend).GetField("bootLogoPresentationStarted", instanceFlags).SetValue(frontend, true);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(startup.MoveNext(), Is.True);
                Assert.That(startup.Current, Is.Null, "Loading must render before gameplay preparation, even when cached.");
                Assert.That(ready.GetValue(frontend), Is.False);
            }
            Assert.That(startup.MoveNext(), Is.True);
            Assert.That(startup.Current, Is.InstanceOf<System.Collections.IEnumerator>());
            Assert.That(ready.GetValue(frontend), Is.False, "Main menu cannot become ready while gameplay preparation is yielded.");
            Assert.That(startup.MoveNext(), Is.False);
            Assert.That(ready.GetValue(frontend), Is.True);
        }
        finally
        {
            ((System.IDisposable)startup).Dispose();
            Object.DestroyImmediate(host);
            mainComplete.SetValue(null, previousMain);
            gameplayComplete.SetValue(null, previousGameplay);
        }
    }

    [Test]
    public void BootLogoCannotExpireBeforeItsFirstVisibleFrame()
    {
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var host = new GameObject("Unpresented boot logo test"); host.SetActive(false);
        try
        {
            var frontend = host.AddComponent<GameFrontend>();
            typeof(GameFrontend).GetField("bootLogoStartedAt", hidden).SetValue(frontend, Time.unscaledTime - 30f);
            Assert.That(typeof(GameFrontend).GetProperty("BootLogoElapsed", hidden).GetValue(frontend), Is.EqualTo(0f));
            typeof(GameFrontend).GetField("bootLogoPresentationStarted", hidden).SetValue(frontend, true);
            Assert.That((float)typeof(GameFrontend).GetProperty("BootLogoElapsed", hidden).GetValue(frontend), Is.GreaterThanOrEqualTo(30f));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void BootLogoScramblesThenSettlesOnCapitalP()
    {
        Assert.That(GameFrontend.BootLogoGlyph(0.5f), Is.Not.EqualTo('P'));
        Assert.That(GameFrontend.BootLogoGlyph(GameFrontend.BootLogoSettleAt),
            Is.EqualTo('P'));
        Assert.That(GameFrontend.BootLogoDuration,
            Is.GreaterThan(GameFrontend.BootLogoSettleAt));
        Assert.That(GameFrontend.BootReadyForMain(
            GameFrontend.BootLogoDuration, false), Is.False);
        Assert.That(GameFrontend.BootReadyForMain(
            GameFrontend.BootLogoDuration, true), Is.False, "Cached startup still needs a visible loading phase.");
        Assert.That(GameFrontend.BootReadyForMain(
            GameFrontend.BootLogoDuration + GameFrontend.BootLoadingMinimumDuration, false), Is.False);
        Assert.That(GameFrontend.BootReadyForMain(
            GameFrontend.BootLogoDuration + GameFrontend.BootLoadingMinimumDuration, true), Is.True);
    }

}
