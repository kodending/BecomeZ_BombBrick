using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class PlacementMarkerVisibilityTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private GameObject host,markerRoot,marker;
    private bool previousTestMode;

    [SetUp]
    public void Setup()
    {
        previousTestMode=CombatEffectTestConfiguration.Active;
        host=new GameObject("Inactive placement marker visibility tests");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        markerRoot=new GameObject("Placement markers");markerRoot.transform.SetParent(host.transform,false);
        marker=new GameObject("Existing placement highlight");marker.transform.SetParent(markerRoot.transform,false);
        Set("placementMarkerRoot",markerRoot.transform);
        Set("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId});
    }

    [TearDown]
    public void Cleanup()
    {
        CombatEffectTestConfiguration.Active=previousTestMode;
        if(host!=null)Object.DestroyImmediate(host);
    }

    [TestCase("battleStarted")]
    [TestCase("battleEnding")]
    [TestCase("mapIntroductionActive")]
    [TestCase("waveIntroActive")]
    [TestCase("waveArrivalActive")]
    [TestCase("wavePreparationAcrossFrames")]
    [TestCase("augmentDraftIntroductionActive")]
    [TestCase("growthShopVisible")]
    [TestCase("shopVisible")]
    [TestCase("capturePresentationResolving")]
    [TestCase("mlEpisodeActive")]
    public void MarkersOnlyReturnToTheUnobstructedPreparationPhase(string blockingState)
    {
        UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.True);
        Set(blockingState,true);UpdateVisibility();
        Assert.That(markerRoot.activeSelf,Is.False,blockingState);
        Assert.That(marker.activeSelf,Is.True,"Hiding the whole layer must preserve its existing placement hints.");
        Set(blockingState,false);UpdateVisibility();
        Assert.That(markerRoot.activeSelf,Is.True);
    }

    [TestCase("아군 승리")]
    [TestCase("적군 승리")]
    public void FinalizingTheWaveCannotShowThePlacementGridBehindItsResult(string outcome)
    {
        CombatEffectTestConfiguration.Active=true;
        Set("battleStarted",true);Set("battleEnding",true);Set("pendingResult",outcome);
        UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.False);
        Call("FinalizeWaveOutcome");
        Assert.That(Get("battleStarted"),Is.False);Assert.That(Get("battleEnding"),Is.False);
        Assert.That(Get("resultText"),Is.EqualTo(outcome));
        UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.False,
            "Both outcomes change battleStarted to false before their result is dismissed.");
        Set("resultText","");UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.True);
    }

    [TestCase("completedItemChoiceIds")]
    [TestCase("reserveReinforcementChoiceIds")]
    public void RewardChoicesKeepMarkersHiddenUntilTheyAreDismissed(string choiceField)
    {
        var choices=(IList)Get(choiceField);choices.Add("one");choices.Add("two");choices.Add("three");
        UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.False);
        choices.Clear();UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.True);
    }

    [Test]
    public void AugmentSelectionAndPostWaveLootCannotRevealTheGrid()
    {
        var run=(ExpeditionRunSave)Get("expeditionRun");
        run.wave=2;run.augmentDraftWave=2;run.augmentDraftIds.AddRange(new[]{"one","two","three"});
        UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.False);
        run.augmentDraftIds.Clear();UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.True);
        Call("BeginWaveLootReview",true,"");
        UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.False);
    }

    [Test]
    public void AValidDragHighlightSurvivesTheHiddenResultLayerAndReturnsForPlacement()
    {
        Type tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        object tile=Activator.CreateInstance(tileType,true);
        var coordinate=new Vector2Int(1,1);
        tileType.GetField("coordinate").SetValue(tile,coordinate);
        tileType.GetField("placementMarker").SetValue(tile,marker);
        marker.AddComponent<SpriteRenderer>();
        Set("highlightedPlacementColor",new Color(1.45f,.88f,.12f,1f));
        ((List<Vector2Int>)Get("highlightedPlacementTiles")).Add(coordinate);
        Call("RefreshPlacementMarker",tile);
        Color highlighted=marker.GetComponent<SpriteRenderer>().color;
        Assert.That(highlighted.a,Is.EqualTo(1f));
        Assert.That(marker.activeSelf,Is.True);
        Set("battleEnding",true);UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.False);
        Set("battleEnding",false);UpdateVisibility();Assert.That(markerRoot.activeSelf,Is.True);
        Assert.That(marker.activeSelf,Is.True);
        Assert.That(marker.GetComponent<SpriteRenderer>().color,Is.EqualTo(highlighted));
    }

    private void UpdateVisibility()=>Call("UpdatePlacementMarkerRootVisibility");
    private void Set(string name,object value)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).SetValue(battle,value);
    private object Get(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).GetValue(battle);
    private object Call(string name,params object[] args)=>
        typeof(MonsterAutoBattlePrototype).GetMethod(name,Hidden).Invoke(battle,args);
}
