using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class DeploymentExpansionFeedbackTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject host;
    private MonsterAutoBattlePrototype battle;
    private ExpeditionRunSave run;
    private bool previousTestMode;

    [SetUp]public void Setup()
    {
        previousTestMode=CombatEffectTestConfiguration.Active;
        host=new GameObject("Inactive deployment feedback tests");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        run=new ExpeditionRunSave{maxDeploymentCount=3,expeditionCurrency=40,regionId=HunterRegionCatalog.FirstRegionId};
        Set("expeditionRun",run);
    }
    [TearDown]public void Cleanup()
    {CombatEffectTestConfiguration.Active=previousTestMode;Object.DestroyImmediate(host);}

    [Test]public void PaidExpansionQueuesTheActualBeforeAndAfterWithoutChangingItsCost()
    {
        int cost=ExpeditionDeploymentSystem.NextUpgradeCost(3);
        Call("BuyDeploymentExpansion");
        Assert.That(run.maxDeploymentCount,Is.EqualTo(4));
        Assert.That(run.expeditionCurrency,Is.EqualTo(40-cost));
        Assert.That(Get<int>("deploymentExpansionFrom"),Is.EqualTo(3));
        Assert.That(Get<int>("deploymentExpansionTo"),Is.EqualTo(4));
        Assert.That(Get<bool>("deploymentExpansionFeedbackPending"),Is.True);
        Call("UpdateDeploymentExpansionFeedback");
        Assert.That(Get<bool>("deploymentExpansionFeedbackPending"),Is.False);
        Assert.That(Get<float>("deploymentExpansionFeedbackStartedAt"),Is.GreaterThanOrEqualTo(0f));
        float started=Get<float>("deploymentExpansionFeedbackStartedAt");
        Call("UpdateDeploymentExpansionFeedback");
        Assert.That(Get<float>("deploymentExpansionFeedbackStartedAt"),Is.EqualTo(started),"Repeated UI frames must not restart the cue.");
    }

    [TestCase("poor")] [TestCase("maximum")] [TestCase("battle")] [TestCase("outcome")]
    public void RejectedPurchasesNeverCreateSuccessFeedback(string reason)
    {
        if(reason=="poor")run.expeditionCurrency=0;
        else if(reason=="maximum")battle.SetAdditionalDeploymentSlots(5);
        else if(reason=="battle")Set("battleStarted",true);
        else Set("battleEnding",true);
        Set("deploymentExpansionFeedbackPending",false);Set("deploymentExpansionFeedbackStartedAt",-1f);
        int limit=run.maxDeploymentCount,gold=run.expeditionCurrency;
        Call("BuyDeploymentExpansion");
        Assert.That(run.maxDeploymentCount,Is.EqualTo(limit));Assert.That(run.expeditionCurrency,Is.EqualTo(gold));
        Assert.That(Get<bool>("deploymentExpansionFeedbackPending"),Is.False);
    }

    [Test]public void AutomaticOrAugmentCapacityWaitsForPreparationAndCombinesRapidIncreases()
    {
        Set("battleEnding",true);battle.SetAdditionalDeploymentSlots(1);
        Call("UpdateDeploymentExpansionFeedback");
        Assert.That(Get<bool>("deploymentExpansionFeedbackPending"),Is.True);
        Assert.That(Get<float>("deploymentExpansionFeedbackStartedAt"),Is.EqualTo(-1f));
        battle.SetAdditionalDeploymentSlots(2);
        Assert.That(Get<int>("deploymentExpansionFrom"),Is.EqualTo(3));Assert.That(Get<int>("deploymentExpansionTo"),Is.EqualTo(5));
        Set("battleEnding",false);Call("UpdateDeploymentExpansionFeedback");
        Assert.That(Get<bool>("deploymentExpansionFeedbackPending"),Is.False);
        Assert.That(Get<float>("deploymentExpansionFeedbackStartedAt"),Is.GreaterThanOrEqualTo(0f));
    }

    [Test]public void SameOrLowerCapacityAndMlSetupDoNotProduceAFalseReward()
    {
        battle.SetAdditionalDeploymentSlots(0);
        Assert.That(Get<bool>("deploymentExpansionFeedbackPending"),Is.False);
        Set("mlEpisodeActive",true);battle.SetAdditionalDeploymentSlots(1);
        Assert.That(Get<bool>("deploymentExpansionFeedbackPending"),Is.False);
        Set("mlEpisodeActive",false);battle.SetAdditionalDeploymentSlots(0);
        Assert.That(Get<bool>("deploymentExpansionFeedbackPending"),Is.False);
    }

    private void Set(string name,object value)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).SetValue(battle,value);
    private T Get<T>(string name)=>(T)typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).GetValue(battle);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Hidden).Invoke(battle,args);
}
