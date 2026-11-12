using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is=UnityEngine.TestTools.Constraints.Is;

public sealed class HunterShopSystemTests
{
    private MonsterDatabase database;
    private string telemetryRoot;

    [SetUp]
    public void SetUp()
    {
        telemetryRoot=TemporaryRoot("Telemetry");
        HunterEconomyTelemetry.SetOutputDirectoryForTests(
            telemetryRoot);
        GameMasterData.ClearCache();
        database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        HunterEconomyTelemetry.ResetOutputDirectoryForTests();
        DeleteRoot(telemetryRoot);
    }

    [Test]
    public void RegionLookupKeepsOrdinalMatchingAndFirstRegionFallback()
    {
        var regions=HunterRegionCatalog.All;
        foreach(var region in regions)
            Assert.That(HunterRegionCatalog.Get(region.id).order,Is.EqualTo(region.order));
        foreach(string missing in new[]{null,string.Empty,"missing",regions[1].id.ToUpperInvariant()})
            Assert.That(HunterRegionCatalog.Get(missing).order,Is.EqualTo(regions[0].order));
    }

    [Test]
    public void RegionRuleCachePreservesLiveParsingAndDoesNotAllocateAfterWarmup()
    {
        var region=new HunterRegionDefinition();
        var parsedRule=(Func<HunterRegionRule>)Delegate.CreateDelegate(typeof(Func<HunterRegionRule>),
            region,typeof(HunterRegionDefinition).GetMethod("ParsedRule",BindingFlags.Instance|BindingFlags.NonPublic));
        foreach(string source in new[]{null,string.Empty,"invalid","pOiSoNfOg"," FrostWave ",
            "3","PoisonFog, CrimsonHeat","99","None"})
        {
            region.ruleId=source;
            HunterRegionRule expected;
            if(!Enum.TryParse(source,true,out expected))expected=HunterRegionRule.None;
            Assert.That(parsedRule(),Is.EqualTo(expected));
            Assert.That(parsedRule(),Is.EqualTo(expected));
        }
        region.ruleId="pOiSoNfOg";
        bool matches=true;
        TestDelegate work=()=>
        {
            for(int i=0;i<2048;i++)matches&=parsedRule()==HunterRegionRule.PoisonFog;
        };
        work();Assert.That(work,Is.Not.AllocatingGCMemory());
        Assert.That(matches,Is.True);
        region.ruleId="frostwave";
        Assert.That(parsedRule(),Is.EqualTo(HunterRegionRule.FrostWave));
        region.ruleId=null;
        Assert.That(parsedRule(),Is.EqualTo(HunterRegionRule.None));
    }

    [Test]
    public void FiveRegionsContainAllEightyMonstersWithoutOverlap()
    {
        IReadOnlyList<HunterRegionDefinition> regions=
            HunterRegionCatalog.All;
        Assert.That(regions,Has.Count.EqualTo(5));

        List<string> everyMonster=new List<string>();
        foreach(HunterRegionDefinition region in regions)
        {
            IReadOnlyList<string> pool=region.MonsterIds();
            Assert.That(pool.Count,Is.EqualTo(16),region.id);
            Assert.That(
                pool.Select(id=>database.Get(id))
                    .All(definition=>
                        definition!=null&&definition.enabled),
                Is.True,
                region.id);
            Assert.That(
                pool.Select(id=>database.Get(id).familyId)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.GreaterThanOrEqualTo(13),
                region.id);
            everyMonster.AddRange(pool);
        }

        Assert.That(
            everyMonster.Distinct(StringComparer.Ordinal).Count(),
            Is.EqualTo(80));
    }

    [TestCase("prototype_region","velos|terakan|ager|lilas|woongi|nukgu|aranea|chundongi|tino|chiwoo|kogason|kingmore|gaiason|jangpung|forestdeer|entpone")]
    [TestCase("poison_fog_swamp","raputo|geomchi|golgaebi|kimbul|ragon|versus|sundongi|bonrex|toto|nectis|doksirum|flotis|eclipsedeer|holophonas|vivre|mukryeong")]
    [TestCase("crimson_jungle","velok|nox|rokrok|tarvil|magril|patjwi|berber|honga|hongryeon|magno|maka|mantius|silanti|griphonas|igvire|yeomhae")]
    [TestCase("frozen_highlands","wilo|seola|krogal|bingareu|seoljwi|ironkuma|seollang|icearachne|seolmyo|cheolno|seolgason|irontusk|atlagon|lunadeer|frovire|haeta")]
    [TestCase("sky_sanctuary","kingpa|frosk|moska|kongjwi|byeoljwi|dami|wolyeong|uno|santius|lumirum|solarbeetle|noepung|holydeer|pone|temvire|geumgangsu")]
    public void RegionPoolsMatchApprovedDistribution(
        string regionId,string expected)
    {
        Assert.That(
            string.Join("|",HunterRegionCatalog.Get(regionId).MonsterIds()),
            Is.EqualTo(expected));
    }

    [Test]
    public void ShopContentListsExposeTheExactBoxPools()
    {
        foreach(HunterRegionDefinition region in HunterRegionCatalog.All)
            Assert.That(
                HunterShopSystem.RegionBoxContents(database,region.id)
                    .Select(definition=>definition.monsterId),
                Is.EqualTo(region.MonsterIds()),region.id);

        foreach(string familyId in database.monsters
                    .Where(definition=>definition!=null&&definition.enabled)
                    .Select(definition=>definition.familyId)
                    .Distinct(StringComparer.Ordinal))
            Assert.That(
                HunterShopSystem.FamilyBoxContents(database,familyId),
                Is.Not.Empty,familyId);
    }

    [Test]
    public void EveryMonsterHasOneStrongOneWeakAndThreeNeutralMaps()
    {
        var regions=HunterRegionCatalog.All;
        for(int origin=0;origin<regions.Count;origin++)
        foreach(string monster in regions[origin].MonsterIds())
        {
            for(int map=0;map<regions.Count;map++)
            {
                float expected=map==(origin+1)%5?1.2f:
                    map==(origin+4)%5?0.8f:1f;
                Assert.That(HunterRegionCatalog.RegionDamageMultiplier(
                    regions[map].id,monster),Is.EqualTo(expected).Within(.0001f),
                    monster+" @ "+regions[map].id);
            }
        }
        Assert.That(HunterRegionCatalog.RegionDamageMultiplier(
            "unknown","velos"),Is.EqualTo(1f));
        Assert.That(HunterRegionCatalog.RegionDamageMultiplier(
            regions[0].id,"unknown"),Is.EqualTo(1f));
    }

    [TestCase("prototype_region",15)]
    public void FullClearRewardMatchesApprovedTotals(
        string regionId,int expected)
    {
        Assert.That(
            HunterTokenRewardSystem.Calculate(regionId,12,12,true),
            Is.EqualTo(expected));
    }

    [Test]
    public void FailedExpeditionPaysOnlyForCompletedWaves()
    {
        Assert.That(
            HunterTokenRewardSystem.Calculate(
                "prototype_region",5,12,false),
            Is.EqualTo(5));
    }

    [Test]
    public void WaveCompositionIsDeterministicAndRegionBound()
    {
        List<string> first=HunterRegionCatalog.BuildWaveMonsterIds(
            "prototype_region",7,14,91234);
        List<string> second=HunterRegionCatalog.BuildWaveMonsterIds(
            "prototype_region",7,14,91234);
        HashSet<string> pool=new HashSet<string>(
            HunterRegionCatalog.Get("prototype_region").MonsterIds(),
            StringComparer.Ordinal);

        Assert.That(first,Is.EqualTo(second));
        Assert.That(first,Has.Count.EqualTo(14));
        Assert.That(first.All(pool.Contains),Is.True);
    }

    [Test]
    public void ExpeditionCompositionsVaryByRunWhileKeepingEveryWaveBalanced()
    {
        foreach(HunterRegionDefinition region in HunterRegionCatalog.All)
        {
            HashSet<string> pool=new HashSet<string>(
                region.MonsterIds(),StringComparer.Ordinal);
            for(int wave=1;wave<=12;wave++)
            {
                WaveScalingMasterRow scaling=GameMasterData.WaveScaling(
                    region.id,wave);
                List<List<string>> variants=Enumerable.Range(0,4)
                    .Select(index=>HunterRegionCatalog
                        .BuildExpeditionWaveMonsterIds(
                            region.id,wave,scaling.enemyCount,
                            "expedition_"+index,database))
                    .ToList();

                Assert.That(variants.All(composition=>
                    composition.Count==scaling.enemyCount&&
                    composition.All(pool.Contains)),Is.True,
                    region.id+" Wave "+wave);
                if(wave!=8&&scaling.enemyCount>2)
                    Assert.That(variants.Select(composition=>string.Join("|",
                            composition.OrderBy(id=>id,StringComparer.Ordinal)))
                        .Distinct(StringComparer.Ordinal).Count(),
                        Is.GreaterThan(1),region.id+" Wave "+wave);
                Assert.That(
                    HunterRegionCatalog.BuildExpeditionWaveMonsterIds(
                        region.id,wave,scaling.enemyCount,
                        "expedition_0",database),
                    Is.EqualTo(variants[0]),region.id+" Wave "+wave);
            }
        }
    }

    [TestCase("prototype_region")]
    [TestCase("poison_fog_swamp")]
    [TestCase("crimson_jungle")]
    [TestCase("frozen_highlands")]
    [TestCase("sky_sanctuary")]
    public void LonePreviewMatchesFinalGuardianAcrossRetriesAndFramePreparation(string regionId)
    {
        const string runId="guardian_preview_resume";
        int finalCount=GameMasterData.WaveScaling(regionId,12).enemyCount;
        var random=UnityEngine.Random.state;
        List<string> preview=HunterRegionCatalog.BuildExpeditionWaveMonsterIds(
            regionId,8,1,runId,database);
        List<string> finale=HunterRegionCatalog.BuildExpeditionWaveMonsterIds(
            regionId,12,finalCount,runId,database);
        Assert.That(preview,Has.Count.EqualTo(1));
        Assert.That(finale,Has.Count.EqualTo(finalCount));
        Assert.That(preview[0],Is.EqualTo(finale[0]));
        Assert.That(HunterRegionCatalog.Get(regionId).MonsterIds(),Does.Contain(preview[0]));

        foreach(int wave in new[]{8,12})
        {
            int count=wave==8?1:finalCount;
            List<string> expected=wave==8?preview:finale;
            Assert.That(HunterRegionCatalog.BuildExpeditionWaveMonsterIds(
                regionId,wave,count,runId,database),Is.EqualTo(expected),"Retry "+wave);
            List<string> prepared=null;
            var preparation=HunterRegionCatalog.BuildExpeditionWaveMonsterIdsAcrossFrames(
                regionId,wave,count,runId,database,value=>prepared=value);
            try{while(preparation.MoveNext()) { }}
            finally{(preparation as IDisposable)?.Dispose();}
            Assert.That(prepared,Is.EqualTo(expected),"Across frames "+wave);
        }
        Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
    }

    [Test]
    public void PreviewGuardianPrefersOffensivePatternAndKeepsSupportFallback()
    {
        MethodInfo choose=typeof(HunterRegionCatalog).GetMethod("PreviewBossIndex",
            BindingFlags.Static|BindingFlags.NonPublic);
        Assert.That(choose,Is.Not.Null);
        Assert.That(choose.Invoke(null,new object[]{new[]{"forestdeer","woongi","chundongi"},database}),
            Is.EqualTo(1),"Self-buff attackers can demonstrate their pattern without allies.");
        Assert.That(choose.Invoke(null,new object[]{new[]{"gaiason","woongi"},database}),
            Is.EqualTo(1),"Prefer a visible cast over a low-health execution condition.");
        Assert.That(choose.Invoke(null,new object[]{new[]{"gaiason","versus","jangpung","woongi"},database}),
            Is.EqualTo(3),"Death chains and deployment-only effects can miss the preview entirely.");
        Assert.That(choose.Invoke(null,new object[]{new[]{"forestdeer","holydeer"},database}),
            Is.Zero,"A support-only roster must retain its original first member.");
    }

    [Test]
    public void LaterRegionsSearchMoreCompositionsInsteadOfInflatingStats()
    {
        int[] candidates=HunterRegionCatalog.All.Select(region=>
            HunterRegionCatalog.CompositionCandidateCount(region.id,12)).ToArray();

        Assert.That(candidates,Is.Ordered.Ascending);
        Assert.That(candidates.Distinct().Count(),Is.EqualTo(candidates.Length));
        Assert.That(HunterRegionCatalog.All.Select(region=>
            HunterRegionCatalog.EnemyHealthMultiplier(region.id)),
            Is.All.EqualTo(1f));
        Assert.That(HunterRegionCatalog.All.Select(region=>
            HunterRegionCatalog.EnemyAttackMultiplier(region.id)),
            Is.All.EqualTo(1f));
    }

    [Test]
    public void CompositionTargetsKeepEarlyAndFinalCrisisBudgets()
    {
        foreach(HunterRegionDefinition region in HunterRegionCatalog.All)
        {
            Assert.That(HunterRegionCatalog.CompositionTargetScore(region.id,4),
                Is.GreaterThan(HunterRegionCatalog.CompositionTargetScore(region.id,3)));
            Assert.That(HunterRegionCatalog.CompositionTargetScore(region.id,4),
                Is.GreaterThan(HunterRegionCatalog.CompositionTargetScore(region.id,5)));
            Assert.That(HunterRegionCatalog.CompositionTargetScore(region.id,12),
                Is.GreaterThan(HunterRegionCatalog.CompositionTargetScore(region.id,11)));
        }
        for(int index=1;index<HunterRegionCatalog.All.Count;index++)
            Assert.That(HunterRegionCatalog.CompositionTargetScore(
                    HunterRegionCatalog.All[index].id,12),
                Is.GreaterThan(HunterRegionCatalog.CompositionTargetScore(
                    HunterRegionCatalog.All[index-1].id,12)));
    }

    [Test]
    public void WaveDatabaseDefinesDynamicCompositionBudgetsWithoutStatInflation()
    {
        foreach(HunterRegionDefinition region in HunterRegionCatalog.All)
        {
            for(int wave=1;wave<=12;wave++)
            {
                WaveScalingMasterRow scaling=GameMasterData.WaveScaling(
                    region.id,wave);
                Assert.That(scaling,Is.Not.Null,region.id+" Wave "+wave);
                Assert.That(scaling.enemyCount,Is.GreaterThan(0));
                Assert.That(scaling.hpMultiplier,Is.EqualTo(1f));
                Assert.That(scaling.attackDamageMultiplier,Is.EqualTo(1f));
                Assert.That(scaling.attackSpeedMultiplier,Is.EqualTo(1f));
                Assert.That(scaling.moveSpeedMultiplier,Is.EqualTo(1f));
            }
        }
    }

    [Test]
    public void FinalGuardianRetainsStrongerCompositionThanOrdinaryLateWaves()
    {
        foreach(HunterRegionDefinition region in HunterRegionCatalog.All)
        {
            Dictionary<int,float> scores=new Dictionary<int,float>();
            foreach(int wave in new[]{7,9,12})
            {
                WaveScalingMasterRow scaling=GameMasterData.WaveScaling(region.id,wave);
                List<string> composition=HunterRegionCatalog.BuildExpeditionWaveMonsterIds(
                    region.id,wave,scaling.enemyCount,"balance_curve_reference",database);
                Assert.That(composition,Has.Count.EqualTo(scaling.enemyCount));
                scores[wave]=HunterRegionCatalog.CompositionScore(region.id,composition,database);
            }
            Assert.That(scores[12],Is.GreaterThan(scores[7]),region.id+" Wave 7");
            Assert.That(scores[12],Is.GreaterThan(scores[9]),region.id+" Wave 9");
        }
    }

    [Test]
    public void EarlyWavesRejectCompletedComboLinks()
    {
        foreach(HunterRegionDefinition region in HunterRegionCatalog.All)
        for(int wave=1;wave<=3;wave++)
        {
            WaveScalingMasterRow scaling=GameMasterData.WaveScaling(region.id,wave);
            List<string> composition=HunterRegionCatalog.BuildWaveMonsterIds(
                region.id,wave,scaling.enemyCount,31877,database);
            Assert.That(HunterRegionCatalog.CompositionComboLinkCount(
                composition,database),Is.Zero,region.id+" Wave "+wave);
        }
    }

    [TestCase("prototype_region","lilas|tino","velos|woongi")]
    [TestCase("poison_fog_swamp","nectis|vivre","geomchi|kimbul")]
    [TestCase("crimson_jungle","magril|maka","berber|velok")]
    [TestCase("frozen_highlands","ironkuma|irontusk","seola|seolgason")]
    [TestCase("sky_sanctuary","santius|temvire","kingpa|lumirum")]
    public void MlBalanceSignalsSeparateKnownStrongAndWeakCompositions(
        string regionId,string strongIds,string weakIds)
    {
        List<string> strong=strongIds.Split('|').ToList();
        List<string> weak=weakIds.Split('|').ToList();
        Assert.That(HunterRegionCatalog.MlCompositionSignalScore(
                regionId,strong),
            Is.GreaterThan(HunterRegionCatalog.MlCompositionSignalScore(
                regionId,weak)),regionId);
    }

    [Test]
    public void ComboDatabaseCoversAllMonsters()
    {
        Assert.That(GameMasterData.Database.monsterCombos,Has.Count.EqualTo(80));
        Assert.That(GameMasterData.Database.monsterCombos
            .Select(row=>row.monsterId).Distinct(StringComparer.Ordinal).Count(),
            Is.EqualTo(80));
    }

    [Test]
    public void FamilyStockRequiresEveryNormalSpeciesAndIsStableForCycle()
    {
        string root=TemporaryRoot("FamilyStock");
        try
        {
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            profile.encounteredMonsterIds.Clear();
            profile.encounteredMonsterIds.AddRange(
                new[]{"velos","velok","raputo"});
            Assert.That(
                HunterShopSystem.EligibleFamilies(profile,database),
                Is.Empty);

            profile.encounteredMonsterIds.Add("wilo");
            Assert.That(
                HunterShopSystem.EligibleFamilies(profile,database),
                Is.EqualTo(new[]{"raptor"}));
            Assert.That(
                HunterShopSystem.EnsureFamilyStock(profile,database),
                Is.True);
            Assert.That(
                profile.hunterFamilyStockIds,
                Is.EqualTo(new[]{"raptor"}));
            Assert.That(
                HunterShopSystem.EnsureFamilyStock(profile,database),
                Is.False);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void NormalizeProfileInitializesShopUnlockNotificationState()
    {
        PlayerProfileSave profile=new PlayerProfileSave
        {
            notifiedHunterRegionBoxIds=null,
            notifiedHunterFamilyBoxIds=null
        };

        HunterShopSystem.NormalizeProfile(profile,database);

        Assert.That(profile.notifiedHunterRegionBoxIds,Is.Not.Null);
        Assert.That(profile.notifiedHunterFamilyBoxIds,Is.Not.Null);
    }

    [Test]
    public void FamilyBoxPriceEscalatesAndCapsForOneStockCycle()
    {
        PlayerProfileSave profile=new PlayerProfileSave();
        int[] expected={30,38,48,60,60};
        for(int purchaseCount=0;
            purchaseCount<expected.Length;
            purchaseCount++)
        {
            profile.hunterFamilyPurchaseCount=purchaseCount;
            Assert.That(
                HunterShopSystem.CurrentFamilyBoxCost(profile),
                Is.EqualTo(expected[purchaseCount]));
        }
        Assert.That(
            expected[0],
            Is.GreaterThan(HunterRegionCatalog.All.Max(
                region=>region.regionBoxCost)));
    }

    [Test]
    public void RegionBoxRemainsLockedUntilBeginnerClear()
    {
        string root=TemporaryRoot("RegionLocked");
        try
        {
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            profile.hunterTokens=50;

            bool purchased=HunterShopSystem.TryPurchaseRegionBox(
                profile,store,database,"prototype_region",
                out OwnedMonsterSave acquired,out string error,
                new System.Random(7));

            Assert.That(purchased,Is.False);
            Assert.That(acquired,Is.Null);
            Assert.That(error,Does.Contain("초보"));
            Assert.That(profile.hunterTokens,Is.EqualTo(50));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void RegionBoxPurchaseConsumesTokensAndPermanentlyAddsMonster()
    {
        string root=TemporaryRoot("RegionPurchase");
        try
        {
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            int beforeCount=profile.monsters.Count;
            profile.hunterTokens=50;
            ExpeditionDifficultyProgress.RegisterClear(
                profile,"prototype_region",ExpeditionDifficulty.Beginner);

            bool purchased=HunterShopSystem.TryPurchaseRegionBox(
                profile,store,database,"prototype_region",
                out OwnedMonsterSave acquired,out string error,
                new System.Random(7));

            Assert.That(purchased,Is.True,error);
            Assert.That(profile.hunterTokens,Is.EqualTo(40));
            Assert.That(profile.monsters,Has.Count.EqualTo(beforeCount+1));
            Assert.That(acquired,Is.Not.Null);
            Assert.That(acquired.isNew,Is.True);
            Assert.That(
                HunterRegionCatalog.Get("prototype_region")
                    .MonsterIds(),
                Does.Contain(acquired.monsterId));

            PlayerProfileSave reloaded=
                new PlayerProfileStore(root).LoadOrCreate(database);
            Assert.That(
                reloaded.monsters.Any(monster=>
                    monster!=null&&
                    monster.instanceId==acquired.instanceId),
                Is.True);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void FamilyBoxUsesEncounteredFamilyPoolAndEscalatesPrice()
    {
        string root=TemporaryRoot("FamilyPurchase");
        try
        {
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            profile.hunterTokens=100;
            profile.encounteredMonsterIds.Clear();
            profile.encounteredMonsterIds.AddRange(
                new[]{"velos","velok","raputo","wilo"});
            profile.hunterFamilyStockIds.Add("raptor");

            bool purchased=HunterShopSystem.TryPurchaseFamilyBox(
                profile,store,database,"raptor",
                out OwnedMonsterSave acquired,out string error,
                new System.Random(3));

            Assert.That(purchased,Is.True,error);
            Assert.That(profile.hunterTokens,Is.EqualTo(70));
            Assert.That(profile.hunterFamilyPurchaseCount,Is.EqualTo(1));
            Assert.That(
                new[]{"velos","velok","raputo","wilo"},
                Does.Contain(acquired.monsterId));
            Assert.That(
                HunterShopSystem.CurrentFamilyBoxCost(profile),
                Is.EqualTo(38));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void HunterShopSellsOnlyTheStandardCaptureNet()
    {
        string root=TemporaryRoot("CaptureNetPurchase");
        try
        {
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            profile.hunterTokens=CaptureNetCatalog.TokenCost;

            bool purchased=HunterShopSystem.TryPurchaseCaptureNet(
                profile,store,out string error);

            Assert.That(purchased,Is.True,error);
            Assert.That(profile.hunterTokens,Is.Zero);
            Assert.That(CaptureNetInventory.Count(
                profile,CaptureNetCatalog.StandardId),Is.EqualTo(1));
            Assert.That(CaptureNetInventory.Count(
                profile,CaptureNetCatalog.RareId),Is.Zero);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void RareCaptureNetCanDropOnlyFromExpeditionSettlement()
    {
        string root=TemporaryRoot("RareCaptureNetReward");
        try
        {
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            ExpeditionRunSave run=Enumerable.Range(0,1000)
                .Select(index=>new ExpeditionRunSave
                {
                    runId="rare_net_reward_"+index,
                    regionId="prototype_region",
                    lastCompletedWave=12,
                    difficulty=ExpeditionDifficulty.Beginner
                })
                .First(candidate=>
                    HunterTokenRewardSystem.CalculateRareCaptureNetReward(
                        candidate)>0);

            HunterTokenRewardSystem.Settle(
                profile,store,database,run,true,out int firstDrop);
            HunterTokenRewardSystem.Settle(
                profile,store,database,run,true,out int repeatedDrop);

            Assert.That(firstDrop,Is.EqualTo(1));
            Assert.That(repeatedDrop,Is.Zero);
            Assert.That(CaptureNetInventory.Count(
                profile,CaptureNetCatalog.RareId),Is.EqualTo(1));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void SettlementIsIdempotentAndUnlocksNextRegion()
    {
        string root=TemporaryRoot("Settlement");
        try
        {
            PlayerProfileStore store=new PlayerProfileStore(root);
            PlayerProfileSave profile=store.LoadOrCreate(database);
            ExpeditionRunSave run=new ExpeditionRunSave
            {
                runId="hunter_settlement_test",
                regionId="prototype_region",
                lastCompletedWave=12,
                difficulty=ExpeditionDifficulty.Beginner
            };

            int first=HunterTokenRewardSystem.Settle(
                profile,store,database,run,true);
            int second=HunterTokenRewardSystem.Settle(
                profile,store,database,run,true);

            Assert.That(first,Is.EqualTo(15));
            Assert.That(second,Is.Zero);
            Assert.That(profile.hunterTokens,Is.EqualTo(15));
            Assert.That(
                profile.unlockedRegionIds,
                Is.EqualTo(new[]{"prototype_region","poison_fog_swamp"}));
            Assert.That(profile.hunterShopCycle,Is.EqualTo(1));
            Assert.That(ExpeditionDifficultyProgress.IsCleared(
                profile,"prototype_region",ExpeditionDifficulty.Beginner),
                Is.True);
            Assert.That(ExpeditionDifficultyProgress.IsUnlocked(
                profile,"prototype_region",ExpeditionDifficulty.Normal),
                Is.True);
            Assert.That(ExpeditionDifficultyProgress.IsUnlocked(
                profile,"prototype_region",ExpeditionDifficulty.Veteran),
                Is.False);

            PlayerProfileSave reloaded=
                new PlayerProfileStore(root).LoadOrCreate(database);
            Assert.That(reloaded.hunterTokens,Is.EqualTo(15));
            Assert.That(
                reloaded.unlockedRegionIds,
                Is.EqualTo(new[]{"prototype_region","poison_fog_swamp"}));
            Assert.That(ExpeditionDifficultyProgress.IsUnlocked(
                reloaded,"prototype_region",ExpeditionDifficulty.Normal),
                Is.True);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Test]
    public void RegionDifficultiesUnlockOnlyAfterPreviousClear()
    {
        PlayerProfileSave profile=new PlayerProfileSave();
        const string regionId="prototype_region";

        Assert.That(ExpeditionDifficultyProgress.HighestUnlocked(
            profile,regionId),Is.EqualTo(ExpeditionDifficulty.Beginner));
        Assert.That(ExpeditionDifficultyProgress.IsUnlocked(
            profile,regionId,ExpeditionDifficulty.Normal),Is.False);

        ExpeditionDifficultyProgress.RegisterClear(
            profile,regionId,ExpeditionDifficulty.Beginner);
        Assert.That(ExpeditionDifficultyProgress.HighestUnlocked(
            profile,regionId),Is.EqualTo(ExpeditionDifficulty.Normal));

        ExpeditionDifficultyProgress.RegisterClear(
            profile,regionId,ExpeditionDifficulty.Normal);
        Assert.That(ExpeditionDifficultyProgress.HighestUnlocked(
            profile,regionId),Is.EqualTo(ExpeditionDifficulty.Veteran));

        ExpeditionDifficultyProgress.RegisterClear(
            profile,regionId,ExpeditionDifficulty.Veteran);
        Assert.That(ExpeditionDifficultyProgress.HighestUnlocked(
            profile,regionId),Is.EqualTo(ExpeditionDifficulty.Hell));
        Assert.That(ExpeditionDifficultyProgress.IsUnlocked(
            profile,"poison_fog_swamp",ExpeditionDifficulty.Normal),Is.False);
    }

    [Test]
    public void EconomyGuardrailsKeepClearAndFailureRewardsInTargetBand()
    {
        foreach(HunterRegionDefinition region in HunterRegionCatalog.All)
        {
            int clearReward=HunterTokenRewardSystem.Calculate(
                region.id,12,12,true);
            int midFailureReward=HunterTokenRewardSystem.Calculate(
                region.id,5,12,false);

            Assert.That(
                clearReward,
                Is.GreaterThanOrEqualTo(region.regionBoxCost),
                region.id);
            Assert.That(
                clearReward,
                Is.LessThan(region.regionBoxCost*2),
                region.id);
            Assert.That(
                (float)midFailureReward/clearReward,
                Is.InRange(0.25f,0.45f),
                region.id);
        }
    }

    [Test]
    public void EconomyTelemetryPersistsAndSummarizesTokenFlow()
    {
        PlayerProfileSave profile=new PlayerProfileSave
        {
            profileId="telemetry_profile",
            hunterTokens=15,
            hunterShopCycle=1
        };
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            runId="telemetry_run",
            regionId="prototype_region",
            lastCompletedWave=12
        };
        HunterEconomyTelemetry.RecordExpeditionSettlement(
            profile,run,true,15,0);

        MonsterDefinition definition=database.Get(
            "raptor_reference_palette_v2");
        OwnedMonsterSave acquired=PlayerProfileStore.CreateMonster(
            definition,"telemetry_box_monster");
        profile.hunterTokens=5;
        HunterEconomyTelemetry.RecordBoxPurchase(
            profile,
            "region",
            "prototype_region",
            10,
            15,
            acquired,
            true);

        List<HunterEconomyTelemetryEvent> events=
            HunterEconomyTelemetry.ReadAll(telemetryRoot);
        HunterEconomyTelemetrySummary summary=
            HunterEconomyTelemetry.Analyze(events);

        Assert.That(events,Has.Count.EqualTo(2));
        Assert.That(summary.expeditionCount,Is.EqualTo(1));
        Assert.That(summary.clearCount,Is.EqualTo(1));
        Assert.That(summary.tokensEarned,Is.EqualTo(15));
        Assert.That(summary.tokensSpent,Is.EqualTo(10));
        Assert.That(summary.NetTokens,Is.EqualTo(5));
        Assert.That(summary.regionBoxesPurchased,Is.EqualTo(1));
        Assert.That(summary.familyBoxesPurchased,Is.Zero);
        Assert.That(summary.repeatSpeciesPurchases,Is.EqualTo(1));
        Assert.That(summary.regions,Has.Count.EqualTo(1));
        Assert.That(
            summary.regions[0].AverageCompletedWave,
            Is.EqualTo(12f));
    }

    [Test]
    public void EveryCurrentFamilyHasAKoreanShopName()
    {
        foreach(string familyId in database.monsters
            .Where(monster=>monster!=null&&monster.enabled)
            .Select(monster=>monster.familyId)
            .Distinct())
        {
            string displayName=HunterShopSystem.FamilyDisplayName(familyId);
            Assert.That(displayName,Is.Not.Empty,familyId);
            Assert.That(displayName,Is.Not.EqualTo(familyId),familyId);
        }
    }

    [Test]
    public void FamilyNamesFollowSelectedLanguage()
    {
        string previous=GameLocalization.CurrentLanguage;
        try
        {
            GameLocalization.SetLanguage(GameLocalization.English);
            Assert.That(HunterShopSystem.FamilyDisplayName("raptor"),
                Is.EqualTo("Raptor"));
            Assert.That(HunterShopSystem.FamilyDisplayName("spirit_deer"),
                Is.EqualTo("Spirit Deer"));
        }
        finally
        {
            GameLocalization.SetLanguage(previous);
        }
    }

    private static string TemporaryRoot(string name)
    {
        return Path.Combine(
            Path.GetTempPath(),
            "BombBrickHunterShopTests",
            name+"_"+Guid.NewGuid().ToString("N"));
    }

    private static void DeleteRoot(string root)
    {
        if(Directory.Exists(root))
            Directory.Delete(root,true);
    }
}
