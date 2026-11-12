using System;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class SkillFocusTimeTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject cameraHost,frontendHost;
    private CombatCameraDirector director;
    private GameFrontend frontend;
    private object previousFrontend,previousFlow;
    private float previousSpeed,previousVolume;
    private bool previousTestMode;
    private Scene scene,previousScene;
    private bool ownsScene;
    private string previousSceneName;

    [SetUp]
    public void SetUp()
    {
        previousSpeed=Time.timeScale;previousVolume=AudioListener.volume;
        previousTestMode=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=false;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        previousFlow=typeof(GameFlowController).GetField("instance",Static).GetValue(null);
        previousScene=SceneManager.GetActiveScene();
        scene=default(Scene);ownsScene=false;previousSceneName=null;
        if(string.IsNullOrEmpty(previousScene.path))scene=previousScene;
        else
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                Scene loaded=SceneManager.GetSceneAt(i);
                if(loaded.isLoaded&&string.IsNullOrEmpty(loaded.path)){scene=loaded;break;}
            }
        // Unity cannot add another scene while an untitled test-runner scene is loaded.
        if(!scene.IsValid())
        {
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            ownsScene=true;
        }
        previousSceneName=scene.name;
        scene.name=GameSceneCatalog.Battle;
        SceneManager.SetActiveScene(scene);
        typeof(GameFrontend).GetField("sceneNameFrame",Static).SetValue(null,-1);

        frontendHost=new GameObject("Inactive skill focus frontend test");frontendHost.SetActive(false);
        var flow=frontendHost.AddComponent<GameFlowController>();
        frontend=frontendHost.AddComponent<GameFrontend>();
        typeof(GameFlowController).GetField("instance",Static).SetValue(null,flow);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,frontend);
        Set(frontend,"initialized",true);Set(frontend,"applicationFocused",true);
        Set(frontend,"settings",new GameUserSettings {pauseWhenUnfocused=true,masterVolume=.6f});
        var audio=frontendHost.AddComponent<GameAudio>();
        var voice=frontendHost.AddComponent<AudioSource>();voice.playOnAwake=false;
        foreach(string field in new[]{"combatVoices","combatLayers"})
        {
            var voices=Get<AudioSource[]>(audio,field);
            for(int i=0;i<voices.Length;i++)voices[i]=voice;
        }
        Set(audio,"effects",voice);Set(frontend,"audioPlayer",audio);

        cameraHost=new GameObject("Skill focus clock test");
        var camera=cameraHost.AddComponent<Camera>();
        director=cameraHost.AddComponent<CombatCameraDirector>();
        Set(director,"battleCamera",camera);
        director.BeginBattle();Time.timeScale=2f;
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if(director!=null)director.EndBattle();
            if(cameraHost!=null)Object.DestroyImmediate(cameraHost);
            if(frontendHost!=null)Object.DestroyImmediate(frontendHost);
        }
        finally
        {
            try
            {
                if(!ownsScene&&scene.IsValid()&&scene.isLoaded&&string.IsNullOrEmpty(scene.path)&&previousSceneName!=null)
                    scene.name=previousSceneName;
                if(previousScene.IsValid()&&previousScene.isLoaded)SceneManager.SetActiveScene(previousScene);
                if(ownsScene&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
            }
            finally
            {
                typeof(GameFrontend).GetField("sceneNameFrame",Static).SetValue(null,-1);
                typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
                typeof(GameFlowController).GetField("instance",Static).SetValue(null,previousFlow);
                Time.timeScale=previousSpeed;AudioListener.volume=previousVolume;
                CombatEffectTestConfiguration.Active=previousTestMode;
            }
        }
    }

    [TestCase(.5f)] [TestCase(1f)] [TestCase(2f)]
    public void LegacySkillTimeRequestsNeverStopSlowOrChangeTheSelectedSpeed(float speed)
    {
        Time.timeScale=speed;
        Assert.That(director.BeginSkillFocus(),Is.False);
        director.BeginSkillExecution();Call(director,"LateUpdate");
        Assert.That(director.SkillFocusActive,Is.False);Assert.That(director.SkillFocusExecuting,Is.False);
        Assert.That(director.SkillFocusCastReady,Is.False);Assert.That(director.SkillFocusStrength,Is.Zero);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(speed));
        director.CancelSkillFocus();director.EndBattle();
        Assert.That(Time.timeScale,Is.EqualTo(speed));
    }

    [TestCase(0f)] [TestCase(3f)]
    public void ExternalPauseOrSpeedChangeIsNeverOverwritten(float externalScale)
    {
        Assert.That(director.BeginSkillFocus(),Is.False);
        Time.timeScale=externalScale;Call(director,"LateUpdate");director.CancelSkillFocus();
        Assert.That(Time.timeScale,Is.EqualTo(externalScale));
        director.EndBattle();Assert.That(Time.timeScale,Is.EqualTo(externalScale));
    }

    [Test]
    public void MenuPausePreservesAndRestoresTheSelectedBattleSpeed()
    {
        frontend.OpenMenu();
        Assert.That(Get<float>(frontend,"previousTimeScale"),Is.EqualTo(2f));Assert.That(Time.timeScale,Is.Zero);
        director.CancelSkillFocus();Assert.That(Time.timeScale,Is.Zero);
        frontend.ClosePanels();Assert.That(Time.timeScale,Is.EqualTo(2f));
    }

    [Test]
    public void BackgroundPauseRemainsPausedUntilFocusReturns()
    {
        Call(frontend,"OnApplicationFocus",false);
        Assert.That(Get<float>(frontend,"backgroundTimeScale"),Is.EqualTo(2f));Assert.That(Time.timeScale,Is.Zero);
        director.CancelSkillFocus();Assert.That(Time.timeScale,Is.Zero);
        Call(frontend,"OnApplicationFocus",true);Assert.That(Time.timeScale,Is.EqualTo(2f));
    }

    [Test]
    public void SaveFailurePauseKeepsTheSelectedSpeedForRetry()
    {
        bool blocked=true;
        LogAssert.Expect(LogType.Warning,"[Save Retry] Test-only blocked checkpoint");
        Assert.That(GameFrontend.TrySaveProgress(()=>
        {if(blocked)throw new System.IO.IOException("Test-only blocked checkpoint");},"test-only-no-files"),Is.False);
        Assert.That(Get<float>(frontend,"saveTimeScale"),Is.EqualTo(2f));Assert.That(Time.timeScale,Is.Zero);
        director.CancelSkillFocus();Assert.That(Time.timeScale,Is.Zero);
        blocked=false;Assert.That(frontend.RetryPendingSave(),Is.True);Assert.That(Time.timeScale,Is.EqualTo(2f));
    }

    private static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    private static void Call(object target,string method,params object[] args)=>
        target.GetType().GetMethod(method,Private).Invoke(target,args);
}
