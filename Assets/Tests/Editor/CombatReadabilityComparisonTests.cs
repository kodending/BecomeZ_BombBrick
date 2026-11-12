using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatReadabilityComparisonTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private bool previousActive,previousMixed,previousEnabled,previousSpeciesTraits;
    private int previousSeed;
    private ExpeditionMonsterRank previousAllyRank,previousEnemyRank;

    [SetUp] public void Setup()
    {
        previousActive=CombatEffectTestConfiguration.Active;previousMixed=CombatEffectTestConfiguration.MixedRank4v8;
        previousEnabled=CombatReadabilityPreview.Enabled;previousSeed=CombatReadabilityPreview.Seed;
        previousAllyRank=CombatEffectTestConfiguration.AllyRank;previousEnemyRank=CombatEffectTestConfiguration.EnemyRank;
        previousSpeciesTraits=CombatEffectTestConfiguration.ReadabilitySpeciesTraits;
    }
    [TearDown] public void Cleanup()
    {
        CombatEffectTestConfiguration.Active=previousActive;CombatEffectTestConfiguration.MixedRank4v8=previousMixed;
        CombatReadabilityPreview.Enabled=previousEnabled;CombatReadabilityPreview.Seed=previousSeed;
        CombatEffectTestConfiguration.AllyRank=previousAllyRank;CombatEffectTestConfiguration.EnemyRank=previousEnemyRank;
        CombatEffectTestConfiguration.ReadabilitySpeciesTraits=previousSpeciesTraits;
    }

    [TestCase(0,1,1)] [TestCase(1,1,1)] [TestCase(2,1,1)]
    [TestCase(3,4,8)] [TestCase(4,8,8)] [TestCase(5,4,1)]
    public void ScenariosUseValidStableRostersWithoutConsumingCombatRandom(int scenario,int allies,int enemies)
    {
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        string before=JsonUtility.ToJson(Random.state);
        var left=CombatEffectTestSceneController.ReadabilityScenarioRoster(scenario,true);
        var right=CombatEffectTestSceneController.ReadabilityScenarioRoster(scenario,false);
        Assert.That(left.Length,Is.EqualTo(allies));Assert.That(right.Length,Is.EqualTo(enemies));
        Assert.That(left.Concat(right).All(id=>database.Get(id)!=null),Is.True);
        CollectionAssert.AreEqual(left,CombatEffectTestSceneController.ReadabilityScenarioRoster(scenario,true));
        Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(before));
    }

    [TestCase(false,false)] [TestCase(false,true)] [TestCase(true,false)] [TestCase(true,true)]
    public void OnlyAnEnabledTestRunReceivesTheComparisonRunIdAndSeed(bool test,bool enabled)
    {
        CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=enabled;CombatReadabilityPreview.Seed=20261026;
        var host=new GameObject("Inactive readability seed test");host.SetActive(false);
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var run=new ExpeditionRunSave{runId="untouched",activeBattleSeed=9};
            typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Hidden).SetValue(battle,run);
            typeof(MonsterAutoBattlePrototype).GetMethod("ConfigureReadabilityRun",Hidden).Invoke(battle,null);
            Assert.That(run.runId,Is.EqualTo(test&&enabled?"combat_readability_20261026":"untouched"));
            Assert.That(run.activeBattleSeed,Is.EqualTo(test&&enabled?20261026:9));
            Assert.That(battle.CaptureReadabilityFormation(),Is.Null,"Readiness must precede any snapshot.");
            Assert.That(battle.RestoreReadabilityFormation(new Dictionary<string,Vector2Int>()),Is.False);
        }
        finally{Object.DestroyImmediate(host);}
    }

    [TestCase(1280,720,false,true)] [TestCase(1280,720,true,true)] [TestCase(1280,720,true,false)]
    [TestCase(960,540,false,true)] [TestCase(960,540,true,true)] [TestCase(1920,1080,false,true)]
    public void ComparisonControlsStayInsideTheGameView(int width,int height,bool running,bool expanded)
    {
        Rect rect=CombatEffectTestSceneController.ReadabilityPanelRect(width,height,running,expanded);
        Assert.That(rect.xMin,Is.GreaterThanOrEqualTo(0f));Assert.That(rect.yMin,Is.GreaterThanOrEqualTo(0f));
        Assert.That(rect.xMax,Is.LessThanOrEqualTo(width));Assert.That(rect.yMax,Is.LessThanOrEqualTo(height));
    }

    [TestCase(640,480)] [TestCase(960,540)] [TestCase(1280,720)] [TestCase(1920,1080)]
    public void SidebarAndItsScrollableContentLeaveTheBattlefieldCenterClear(int width,int height)
    {
        Rect sidebar=CombatEffectTestSceneController.TestSidebarRect(width,height);
        Rect content=CombatEffectTestSceneController.TestSidebarContentRect(width,height);
        Assert.That(sidebar.xMin,Is.GreaterThanOrEqualTo(0f));Assert.That(sidebar.yMin,Is.GreaterThanOrEqualTo(0f));
        Assert.That(sidebar.xMax,Is.LessThanOrEqualTo(width));Assert.That(sidebar.yMax,Is.LessThanOrEqualTo(height));
        Assert.That(sidebar.width,Is.LessThanOrEqualTo(width*.24f+.001f));
        Assert.That(sidebar.Contains(new Vector2(width*.5f,height*.5f)),Is.False);
        Assert.That(content.width,Is.GreaterThan(0f));Assert.That(content.height,Is.GreaterThan(0f));
        Assert.That(content.xMin,Is.GreaterThanOrEqualTo(sidebar.xMin));Assert.That(content.xMax,Is.LessThanOrEqualTo(sidebar.xMax));
        Assert.That(content.yMin,Is.GreaterThan(sidebar.yMin));Assert.That(content.yMax,Is.LessThanOrEqualTo(sidebar.yMax));
        Rect collapsed=CombatEffectTestSceneController.TestSidebarRect(width,height,true);
        Assert.That(collapsed.width,Is.EqualTo(sidebar.width));Assert.That(collapsed.yMin,Is.EqualTo(sidebar.yMin));
        Assert.That(collapsed.yMax,Is.LessThanOrEqualTo(content.yMin));
    }

    [TestCase("mapIntroductionActive")] [TestCase("waveIntroActive")] [TestCase("waveArrivalActive")]
    [TestCase("wavePreparationAcrossFrames")] [TestCase("battleStarted")] [TestCase("battleEnding")]
    public void AutoStartWaitsForTheVisibleMapIntroductionToFinish(string blocker)
    {
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Enabled=true;
        var host=new GameObject("Inactive readability intro test");host.SetActive(false);
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            typeof(MonsterAutoBattlePrototype).GetField("<IsSceneReady>k__BackingField",Hidden).SetValue(battle,true);
            Assert.That(battle.ReadabilityReadyToStart,Is.True);
            typeof(MonsterAutoBattlePrototype).GetField(blocker,Hidden).SetValue(battle,true);
            Assert.That(battle.ReadabilityReadyToStart,Is.False);
        }
        finally{Object.DestroyImmediate(host);}
    }

    [Test]
    public void InvalidSavedFormationResetsTheComparisonOnceWithoutConsumingPendingAutoStart()
    {
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Enabled=true;
        var host=new GameObject("Inactive readability invalid formation test");host.SetActive(false);
        float listenerVolume=AudioListener.volume;
        CombatReadabilityPreset preset=CombatReadabilityPreview.Preset;
        bool basic=CombatReadabilityPreview.BasicOnly,contact=CombatReadabilityPreview.ContactFeedback,audio=CombatReadabilityPreview.SkillAudio;
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var controller=host.AddComponent<CombatEffectTestSceneController>();
            typeof(MonsterAutoBattlePrototype).GetField("<IsSceneReady>k__BackingField",Hidden).SetValue(battle,true);
            typeof(MonsterAutoBattlePrototype).GetField("mapIntroductionActive",Hidden).SetValue(battle,true);
            var type=typeof(CombatEffectTestSceneController);
            type.GetField("activeBattle",Hidden).SetValue(controller,battle);
            var formation=type.GetField("readabilityFormation",Hidden);
            var invalid=new Dictionary<string,Vector2Int>{{"removed_unit",new Vector2Int(999,999)}};
            formation.SetValue(controller,invalid);
            type.GetField("readabilityAwaitingFormation",Hidden).SetValue(controller,true);
            type.GetField("readabilityAutoStart",Hidden).SetValue(controller,true);
            LogAssert.Expect(LogType.Warning,"[전투 점검] 기존 배치를 복원할 수 없어 현재 배치로 초기화했습니다.");
            type.GetMethod("LateUpdate",Hidden).Invoke(controller,null);
            var replacement=(Dictionary<string,Vector2Int>)formation.GetValue(controller);
            Assert.That(replacement,Is.Not.Null.And.Empty);
            Assert.That(replacement,Is.Not.SameAs(invalid));
            Assert.That(type.GetField("readabilityAwaitingFormation",Hidden).GetValue(controller),Is.False);
            Assert.That(type.GetField("readabilityAutoStart",Hidden).GetValue(controller),Is.True,"The map introduction still blocks auto-start.");
            type.GetMethod("LateUpdate",Hidden).Invoke(controller,null);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(host);AudioListener.volume=listenerVolume;
            CombatReadabilityPreview.Preset=preset;CombatReadabilityPreview.BasicOnly=basic;
            CombatReadabilityPreview.ContactFeedback=contact;CombatReadabilityPreview.SkillAudio=audio;
        }
    }

    [Test]
    public void MixedComparisonUsesEachSpeciesTraitsAndRejectsGlobalTraitInjection()
    {
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Enabled=true;
        CombatEffectTestConfiguration.ReadabilitySpeciesTraits=true;
        CombatEffectTestConfiguration.MixedRank4v8=false;
        CombatEffectTestConfiguration.AllyRank=ExpeditionMonsterRank.Rank3;
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        foreach(string id in new[]{"velos","chiwoo","kingmore"})
        {
            MonsterDefinition definition=database.Get(id);
            var state=new ExpeditionMonsterStateSave();
            CombatEffectTestConfiguration.ApplyTestProgression(state,true,definition:definition);
            Assert.That(state.rank2TraitId,Is.EqualTo(ExpeditionTraitProgression.FixedTrait(definition,2).id));
            Assert.That(state.rank3TraitId,Is.EqualTo(ExpeditionTraitProgression.FixedTrait(definition,3).id));
            var traits=new List<string>{state.rank2TraitId,state.rank3TraitId};
            CombatEffectTestConfiguration.AddSelectedTraits(traits,true);
            Assert.That(traits.Count,Is.EqualTo(2));
        }
    }

    [Test]
    public void EnablingComparisonOnAManualSetupPreservesItsSelectedTraits()
    {
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Enabled=true;
        CombatEffectTestConfiguration.MixedRank4v8=false;CombatEffectTestConfiguration.ReadabilitySpeciesTraits=false;
        CombatEffectTestConfiguration.AllyRank=ExpeditionMonsterRank.Rank3;
        string rank2=CombatEffectTestConfiguration.AllyRank2TraitId,rank3=CombatEffectTestConfiguration.AllyRank3TraitId;
        try
        {
            CombatEffectTestConfiguration.AllyRank2TraitId="manual_rank2";CombatEffectTestConfiguration.AllyRank3TraitId="manual_rank3";
            var state=new ExpeditionMonsterStateSave();
            CombatEffectTestConfiguration.ApplyTestProgression(state,true);
            Assert.That(state.rank2TraitId,Is.EqualTo("manual_rank2"));Assert.That(state.rank3TraitId,Is.EqualTo("manual_rank3"));
        }
        finally{CombatEffectTestConfiguration.AllyRank2TraitId=rank2;CombatEffectTestConfiguration.AllyRank3TraitId=rank3;}
    }

    [TestCase(CombatReadabilityPreset.Current)] [TestCase(CombatReadabilityPreset.Compact)]
    public void ManualScenarioResetAlwaysKeepsLocalEmphasisAndPreservesItsRosterAndGrades(CombatReadabilityPreset obsolete)
    {
        var host=new GameObject("Inactive local emphasis reset test");host.SetActive(false);
        var preset=CombatReadabilityPreview.Preset;
        bool basic=CombatReadabilityPreview.BasicOnly,contact=CombatReadabilityPreview.ContactFeedback,audio=CombatReadabilityPreview.SkillAudio;
        float volume=AudioListener.volume;
        var allies=CombatEffectTestConfiguration.AllyMonsterIds.ToArray();
        var enemies=CombatEffectTestConfiguration.EnemyMonsterIds.ToArray();
        var allyGrade=CombatEffectTestConfiguration.AllyRank;var enemyGrade=CombatEffectTestConfiguration.EnemyRank;
        try
        {
            var controller=host.AddComponent<CombatEffectTestSceneController>();
            var type=typeof(CombatEffectTestSceneController);
            var roster=(List<string>)type.GetField("allyRoster",Hidden).GetValue(controller);
            roster.AddRange(new[]{"velos","wilo"});
            type.GetField("allyRank",Hidden).SetValue(controller,ExpeditionMonsterRank.Rank3);
            CombatReadabilityPreview.Enabled=false;CombatReadabilityPreview.Preset=obsolete;
            CombatEffectTestConfiguration.ReadabilitySpeciesTraits=true;
            type.GetMethod("ResetReadabilityScenario",Hidden).Invoke(controller,null);
            Assert.That(CombatReadabilityPreview.Enabled,Is.True);
            Assert.That(CombatReadabilityPreview.Preset,Is.EqualTo(CombatReadabilityPreset.Local));
            Assert.That(CombatEffectTestConfiguration.ReadabilitySpeciesTraits,Is.False);
            CollectionAssert.AreEqual(new[]{"velos","wilo"},roster);
            Assert.That(type.GetField("allyRank",Hidden).GetValue(controller),Is.EqualTo(ExpeditionMonsterRank.Rank3));
            CollectionAssert.AreEqual(allies,CombatEffectTestConfiguration.AllyMonsterIds);
            CollectionAssert.AreEqual(enemies,CombatEffectTestConfiguration.EnemyMonsterIds);
            Assert.That(CombatEffectTestConfiguration.AllyRank,Is.EqualTo(allyGrade));
            Assert.That(CombatEffectTestConfiguration.EnemyRank,Is.EqualTo(enemyGrade));
        }
        finally
        {
            Object.DestroyImmediate(host);AudioListener.volume=volume;
            CombatReadabilityPreview.Preset=preset;CombatReadabilityPreview.BasicOnly=basic;
            CombatReadabilityPreview.ContactFeedback=contact;CombatReadabilityPreview.SkillAudio=audio;
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void SidebarConsumesPointerOnlyInsideItsVisibleAreaAndOnlyInTestMode(bool isCollapsed)
    {
        var field=typeof(CombatEffectTestSceneController).GetField("sidebarCollapsed",Hidden);
        var popup=typeof(CombatEffectTestSceneController).GetField("activePopupRect",Hidden);
        object before=field.GetValue(null),previousPopup=popup.GetValue(null);
        try
        {
            field.SetValue(null,isCollapsed);popup.SetValue(null,new Rect());
            Rect sidebar=CombatEffectTestSceneController.TestSidebarRect(Screen.width,Screen.height,isCollapsed);
            Rect content=CombatEffectTestSceneController.TestSidebarContentRect(Screen.width,Screen.height);
            CombatEffectTestConfiguration.Active=true;
            Assert.That(CombatEffectTestSceneController.PointerOverControls(sidebar.center),Is.True);
            Assert.That(CombatEffectTestSceneController.PointerOverControls(content.center),Is.EqualTo(!isCollapsed));
            Assert.That(CombatEffectTestSceneController.PointerOverControls(new Vector2(Screen.width*.5f,Screen.height*.5f)),Is.False);
            CombatEffectTestConfiguration.Active=false;
            Assert.That(CombatEffectTestSceneController.PointerOverControls(sidebar.center),Is.False);
        }
        finally{field.SetValue(null,before);popup.SetValue(null,previousPopup);}
    }
}
