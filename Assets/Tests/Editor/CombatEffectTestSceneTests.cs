using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CombatEffectTestSceneTests
{
    [Test]
    public void TestSceneExists()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(
            CombatEffectTestSceneSetup.ScenePath));
    }

    [Test]
    public void TestCombatantBaselineIsAverageBAndUsesSelectedPersonality()
    {
        OwnedMonsterSave monster=new OwnedMonsterSave
        {
            hpMultiplier=.85f,damageMultiplier=1.15f,
            attackSpeedMultiplier=.9f,defenseMultiplier=1.15f,
            statusResistanceMultiplier=.85f,moveSpeedMultiplier=1.06f,
            isShiny=true,personalityId="calm"
        };

        CombatEffectTestConfiguration.ApplyTestBaseline(monster,"eccentric");

        Assert.That(MonsterStatQuality.OverallGrade(monster),Is.EqualTo("B"));
        foreach(MonsterIndividualStat stat in
                System.Enum.GetValues(typeof(MonsterIndividualStat)))
            Assert.That(MonsterStatQuality.Multiplier(monster,stat),Is.EqualTo(1f));
        Assert.That(monster.isShiny,Is.False);
        Assert.That(monster.personalityId,Is.EqualTo("eccentric"));
    }

    [Test]
    public void StartRequestIsConsumedOnlyOnce()
    {
        CombatEffectTestConfiguration.ClearStartRequest();
        Assert.That(CombatEffectTestConfiguration.ConsumeStartRequest(),Is.False);
        CombatEffectTestConfiguration.RequestStart();
        Assert.That(CombatEffectTestConfiguration.ConsumeStartRequest(),Is.True);
        Assert.That(CombatEffectTestConfiguration.ConsumeStartRequest(),Is.False);
    }

    [Test]
    public void HexDistanceUsesSixAdjacentDirections()
    {
        Vector2Int center=new Vector2Int(3,2);
        Vector2Int[] neighbors=
        {
            new Vector2Int(2,2),new Vector2Int(4,2),
            new Vector2Int(2,1),new Vector2Int(3,1),
            new Vector2Int(2,3),new Vector2Int(3,3)
        };

        Assert.That(neighbors.All(tile=>
            MonsterAutoBattlePrototype.HexDistance(center,tile)==1),Is.True);
        Assert.That(MonsterAutoBattlePrototype.HexDistance(
            center,new Vector2Int(3,4)),Is.EqualTo(2));
    }

    [Test]
    public void HiddenHexCombatUsesWholeTileAttackRanges()
    {
        Vector2Int source=new Vector2Int(2,1);

        Assert.That(MonsterAutoBattlePrototype.BasicAttackHexRange(1),Is.EqualTo(1));
        Assert.That(MonsterAutoBattlePrototype.BasicAttackHexRange(2),Is.EqualTo(2));
        Assert.That(MonsterAutoBattlePrototype.BasicAttackHexRange(4),Is.EqualTo(3));
        Assert.That(MonsterAutoBattlePrototype.CanAttackAtHexRange(
            source,new Vector2Int(3,1),1),Is.True);
        Assert.That(MonsterAutoBattlePrototype.CanAttackAtHexRange(
            source,new Vector2Int(4,1),1),Is.False);
        Assert.That(MonsterAutoBattlePrototype.CanAttackAtHexRange(
            source,new Vector2Int(5,1),3),Is.True);
    }

    [Test]
    public void HiddenHexCombatChoosesTheShortestAttackRoute()
    {
        bool InArena(Vector2Int tile)=>
            tile.x>=0&&tile.x<6&&tile.y>=0&&tile.y<5&&tile!=new Vector2Int(4,2);

        Assert.That(MonsterAutoBattlePrototype.TryFindShortestAttackHexStep(
            new Vector2Int(0,2),new Vector2Int(4,2),1,InArena,
            out Vector2Int straightStep),Is.True);
        Assert.That(straightStep,Is.EqualTo(new Vector2Int(1,2)));

        Assert.That(MonsterAutoBattlePrototype.TryFindShortestAttackHexStep(
            new Vector2Int(0,0),new Vector2Int(1,4),1,InArena,
            out Vector2Int diagonalStep),Is.True);
        Assert.That(diagonalStep,Is.EqualTo(new Vector2Int(0,1)));
    }

    [Test]
    public void HiddenHexCombatDoesNotTakeAnExtraStepWhenAlreadyInRange()
    {
        Vector2Int source=new Vector2Int(3,2);

        Assert.That(MonsterAutoBattlePrototype.TryFindShortestAttackHexStep(
            source,new Vector2Int(4,2),1,_=>true,
            out Vector2Int next),Is.False);
        Assert.That(next,Is.EqualTo(source));
    }

    [Test]
    public void ScreenOriginCannotHoverTheFirstBenchMarker()
    {
        Assert.That(MonsterAutoBattlePrototype.PointerCanHoverBattlefield(
            Vector3.zero,1920,1080),Is.False);
        Assert.That(MonsterAutoBattlePrototype.PointerCanHoverBattlefield(
            new Vector3(960f,540f),1920,1080),Is.True);
        Assert.That(MonsterAutoBattlePrototype.HasHoveredTile(
            new Vector2Int(int.MinValue,int.MinValue)),Is.False);
        Assert.That(MonsterAutoBattlePrototype.HasHoveredTile(
            new Vector2Int(-1,-1)),Is.True,
            "첫 대기석 좌표와 마우스 미지정 상태가 충돌하면 안 됩니다.");
    }

    [Test]
    public void HoveredBenchUnitShowsHealthAndCenteredRankAboveIt()
    {
        Assert.That(MonsterAutoBattlePrototype.ShouldShowWorldHealthBar(
            false,true,true,true),Is.True);
        Assert.That(MonsterAutoBattlePrototype.ShouldShowWorldHealthBar(
            false,true,false,true),Is.False);
        Rect health=new Rect(100f,80f,52f,8f);
        Rect rank=MonsterAutoBattlePrototype.RankStarsRect(health);
        Assert.That(rank.center.x,Is.EqualTo(health.center.x));
        Assert.That(rank.yMax,Is.LessThan(health.y));
    }

    [Test]
    public void DeploymentGridAddsFifthRowWithoutChangingArenaBounds()
    {
        bool previous=CombatEffectTestConfiguration.Active;
        Mesh mesh=null;
        GameObject cameraObject=null;
        try
        {
            CombatEffectTestConfiguration.Active=true;
            Assert.That(MonsterAutoBattlePrototype.HexDeploymentColumns,Is.EqualTo(8));
            Assert.That(MonsterAutoBattlePrototype.HexDeploymentRows,Is.EqualTo(4));
            Assert.That(MonsterAutoBattlePrototype.BattleDeploymentRows,Is.EqualTo(5));
            Assert.That(MonsterAutoBattlePrototype.AllyDeploymentColumns,Is.EqualTo(3));
            Assert.That(MonsterAutoBattlePrototype.NeutralDeploymentColumns,Is.EqualTo(2));
            Assert.That(MonsterAutoBattlePrototype.EnemyDeploymentColumns,Is.EqualTo(3));
            Assert.That(MonsterAutoBattlePrototype.AllyDeploymentColumns+
                        MonsterAutoBattlePrototype.NeutralDeploymentColumns+
                        MonsterAutoBattlePrototype.EnemyDeploymentColumns,
                Is.EqualTo(MonsterAutoBattlePrototype.HexDeploymentColumns));

            Vector3 even=MonsterAutoBattlePrototype.CenteredGridWorld(
                new Vector2Int(0,0));
            Vector3 odd=MonsterAutoBattlePrototype.CenteredGridWorld(
                new Vector2Int(0,1));
            Vector3 bench=MonsterAutoBattlePrototype.CenteredGridWorld(
                new Vector2Int(0,-1));
            Assert.That(MonsterAutoBattlePrototype.HexDeploymentTileSize,
                Is.EqualTo(2.45f));
            Assert.That(MonsterAutoBattlePrototype.HexDeploymentTileVisualScale,
                Is.EqualTo(0.78f));
            Assert.That(MonsterAutoBattlePrototype.HexDeploymentSpacingScale,
                Is.EqualTo(1.2f));
            Assert.That(MonsterAutoBattlePrototype.HexDeploymentUnitScale,
                Is.EqualTo(0.84f));
            Assert.That(MonsterAutoBattlePrototype.HexDeploymentMonsterVisualScale,
                Is.EqualTo(1.05f).Within(0.001f));
            Assert.That(odd.x-even.x,Is.EqualTo(1.47f).Within(0.001f));
            Assert.That(odd.z-even.z,Is.EqualTo(2.5461f).Within(0.001f));
            Assert.That(even.z-bench.z,Is.EqualTo(4.0961f).Within(0.001f));
            Vector3 extraTopRow=MonsterAutoBattlePrototype.CenteredGridWorld(
                new Vector2Int(0,4));
            Vector3 previousTopRow=MonsterAutoBattlePrototype.CenteredGridWorld(
                new Vector2Int(0,3));
            Assert.That(extraTopRow.z-previousTopRow.z,
                Is.EqualTo(2.5461f).Within(0.001f));

            mesh=MonsterAutoBattlePrototype.CreateBattleTileMesh();
            Assert.That(mesh.name,Is.EqualTo("Battle Hex Tile"));
            Assert.That(mesh.vertexCount,Is.EqualTo(12));

            cameraObject=new GameObject("Hex Deployment Camera Test");
            Camera camera=cameraObject.AddComponent<Camera>();
            camera.aspect=16f/9f;
            MonsterAutoBattlePrototype.FrameCameraToBattleArena(
                camera,new Bounds(Vector3.zero,new Vector3(8.8f,0.2f,6.67f)));
            float pitch=-Mathf.Asin(camera.transform.forward.y)*Mathf.Rad2Deg;
            float groundDistance=-camera.transform.position.y/camera.transform.forward.y;
            Vector3 groundLook=camera.transform.position+
                               camera.transform.forward*groundDistance;
            Assert.That(pitch,Is.EqualTo(
                MonsterAutoBattlePrototype.HexDeploymentCameraPitchDegrees).Within(0.1f));
            Assert.That(groundLook.z,Is.EqualTo(
                MonsterAutoBattlePrototype.HexDeploymentCameraLookAhead).Within(0.001f));
            Assert.That(Vector3.Distance(
                    camera.transform.position,
                    Vector3.forward*MonsterAutoBattlePrototype.HexDeploymentCameraLookAhead),
                Is.InRange(20f,26f));

            Bounds arena=MonsterAutoBattlePrototype.CenteredArenaBounds();
            Assert.That(arena.size.x,Is.EqualTo(24.5f).Within(0.001f));
            Assert.That(arena.size.z,Is.EqualTo(9.7601f).Within(0.001f));

            CombatEffectTestConfiguration.Active=false;
            Vector3 productionEven=MonsterAutoBattlePrototype.CenteredGridWorld(
                new Vector2Int(0,0));
            Assert.That(productionEven,Is.EqualTo(even));
            Assert.That(MonsterAutoBattlePrototype.CenteredArenaBounds(),Is.EqualTo(arena));
            Vector3 productionOdd=MonsterAutoBattlePrototype.CenteredGridWorld(
                new Vector2Int(0,1));
            Assert.That(productionOdd.x-productionEven.x,
                Is.EqualTo(1.47f).Within(0.001f));
            Assert.That(productionOdd.z-productionEven.z,
                Is.EqualTo(2.5461f).Within(0.001f));
        }
        finally
        {
            if(mesh!=null)Object.DestroyImmediate(mesh);
            if(cameraObject!=null)Object.DestroyImmediate(cameraObject);
            CombatEffectTestConfiguration.Active=previous;
        }
    }

    [Test]
    public void RaptorLeapUsesPreviousAreaSkillTuning()
    {
        SkillMasterRow skill=GameMasterData.Skill("raptor_hunting_leap");
        Assert.That(skill.baseCooldown,Is.EqualTo(6.5f));
        Assert.That(skill.castRangeTiles,Is.EqualTo(5f));
        Assert.That(skill.radiusTiles,Is.EqualTo(1.5f));
    }

    [Test]
    public void HealthBarTracksProjectedVisualCenterAndTop()
    {
        Rect visualBounds=new Rect(100f,200f,40f,50f);
        Rect healthBar=MonsterAutoBattlePrototype.HealthBarRectForVisualBounds(
            visualBounds);

        Assert.That(healthBar.center.x,Is.EqualTo(visualBounds.center.x));
        Assert.That(healthBar.yMax,Is.EqualTo(visualBounds.yMin-6f));
        Assert.That(healthBar.size,Is.EqualTo(new Vector2(52f,8f)));

        Rect grownBounds=new Rect(80f,150f,80f,100f);
        Rect grownHealthBar=MonsterAutoBattlePrototype.HealthBarRectForVisualBounds(
            grownBounds);
        Assert.That(grownHealthBar.center.x,Is.EqualTo(grownBounds.center.x));
        Assert.That(grownHealthBar.yMax,Is.EqualTo(grownBounds.yMin-6f));
        Assert.That(grownHealthBar.y,Is.LessThan(healthBar.y));
    }

    [Test]
    public void SelectorListsEveryEnabledMonsterInChosenFamily()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        var members=CombatEffectTestSceneController.GetFamilyMembers(
            database,"raptor");
        CollectionAssert.AreEqual(
            new[]{"velos","velok","raputo","wilo"},
            members.Select(member=>member.monsterId).ToArray());
    }

    [Test]
    public void MixedRosterUsesEveryAddedMonsterAndFallsBackToCount()
    {
        string previous=CombatEffectTestConfiguration.AllyMonsterId;
        int previousCount=CombatEffectTestConfiguration.AllyCount;
        string[] previousRoster=CombatEffectTestConfiguration.AllyMonsterIds.ToArray();
        try
        {
            CombatEffectTestConfiguration.AllyMonsterId="velos";
            CombatEffectTestConfiguration.AllyCount=3;
            CombatEffectTestConfiguration.AllyMonsterIds.Clear();
            CollectionAssert.AreEqual(new[]{"velos","velos","velos"},
                CombatEffectTestConfiguration.ConfiguredMonsterIds(true));

            CombatEffectTestConfiguration.AllyMonsterIds.AddRange(
                new[]{"velos","wilo","raputo"});
            CollectionAssert.AreEqual(new[]{"velos","wilo","raputo"},
                CombatEffectTestConfiguration.ConfiguredMonsterIds(true));
        }
        finally
        {
            CombatEffectTestConfiguration.AllyMonsterId=previous;
            CombatEffectTestConfiguration.AllyCount=previousCount;
            CombatEffectTestConfiguration.AllyMonsterIds.Clear();
            CombatEffectTestConfiguration.AllyMonsterIds.AddRange(previousRoster);
        }
    }

    [Test]
    public void RandomMatchBuildsSixteenDistinctCombatants()
    {
        string[] pool=Enumerable.Range(1,20).Select(index=>"monster_"+index).ToArray();
        var first=CombatEffectTestSceneController.RandomDistinctMonsterIds(pool,16,7);
        var second=CombatEffectTestSceneController.RandomDistinctMonsterIds(pool,16,7);

        Assert.That(first.Count,Is.EqualTo(16));
        Assert.That(first.Distinct().Count(),Is.EqualTo(16));
        CollectionAssert.AreEqual(first,second);
    }

    [Test]
    public void FocusScoringBuildsSmallEngagementGroupsInsteadOfDuels()
    {
        float solo=MonsterAutoBattlePrototype.FocusGroupScoreAdjustment(0,0.25f);
        float pair=MonsterAutoBattlePrototype.FocusGroupScoreAdjustment(1,0.25f);
        float trio=MonsterAutoBattlePrototype.FocusGroupScoreAdjustment(2,0.25f);
        float overcrowded=MonsterAutoBattlePrototype.FocusGroupScoreAdjustment(4,0.25f);

        Assert.That(trio,Is.LessThan(pair));
        Assert.That(pair,Is.LessThan(solo));
        Assert.That(overcrowded,Is.GreaterThan(solo));
    }

    [Test]
    public void FrontlineAggroPrioritizesTheLeadingColumnForBothTeams()
    {
        float playerFront=MonsterAutoBattlePrototype.FrontlineAggroScore(2,true,10);
        float playerBack=MonsterAutoBattlePrototype.FrontlineAggroScore(0,true,10);
        float enemyFront=MonsterAutoBattlePrototype.FrontlineAggroScore(7,false,10);
        float enemyBack=MonsterAutoBattlePrototype.FrontlineAggroScore(9,false,10);

        Assert.That(playerFront,Is.LessThan(playerBack));
        Assert.That(enemyFront,Is.LessThan(enemyBack));
        Assert.That(playerFront,Is.EqualTo(enemyFront));
    }

    [Test]
    public void BasicTargetSelectionAlwaysPrefersTheNearestEnemy()
    {
        Vector3 source=Vector3.zero;

        Assert.That(MonsterAutoBattlePrototype.IsPreferredNearestTarget(
            source,new Vector3(1f,0f,0f),20,
            new Vector3(3f,0f,0f),1),Is.True);
        Assert.That(MonsterAutoBattlePrototype.IsPreferredNearestTarget(
            source,new Vector3(3f,0f,0f),1,
            new Vector3(1f,0f,0f),20),Is.False);
        Assert.That(MonsterAutoBattlePrototype.IsPreferredNearestTarget(
            source,new Vector3(1f,4f,0f),1,
            new Vector3(-1f,0f,0f),20),Is.True,
            "Height and personality ordering must not override equal ground distance.");
    }

    [Test]
    public void CombatReadabilityPacingSeparatesSpaceAndOpeningAttacks()
    {
        float reach=MonsterAutoBattlePrototype.AttackDistanceForRange(1,0.52f,0.46f);
        float inner=MonsterAutoBattlePrototype.AttackSlotDistanceForRing(
            reach,0.52f,0.46f,0);
        float outer=MonsterAutoBattlePrototype.AttackSlotDistanceForRing(
            reach,0.52f,0.46f,1);

        Assert.That(inner,Is.EqualTo(2.319f).Within(0.0001f));
        Assert.That(outer,Is.EqualTo(2.744f).Within(0.0001f));
        Assert.That(outer,Is.EqualTo(reach-.08f).Within(0.0001f));
        Assert.That(MonsterAutoBattlePrototype.OpeningAttackDelay(0),
            Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(MonsterAutoBattlePrototype.OpeningAttackDelay(3),
            Is.EqualTo(0.34f).Within(0.0001f));
        Assert.That(MonsterAutoBattlePrototype.CombatMovementSpeedScale,
            Is.EqualTo(0.68f));
        Assert.That(MonsterAutoBattlePrototype.CombatAttackIntervalScale,
            Is.EqualTo(1.5625f));
    }

    [Test]
    public void MixedRankPresetAppliesIndividualGradesAndSpeciesTraitsWithoutSharedOverrides()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        MonsterDefinition[] roster=database.monsters.Where(definition=>
                definition!=null&&definition.enabled&&
                !string.IsNullOrWhiteSpace(definition.resourcePath))
            .GroupBy(definition=>definition.familyId).Select(family=>family.First())
            .Take(12).ToArray();
        Assert.That(roster.Length,Is.EqualTo(12));
        bool previousMixed=CombatEffectTestConfiguration.MixedRank4v8;
        ExpeditionMonsterRank previousAllyRank=CombatEffectTestConfiguration.AllyRank;
        ExpeditionMonsterRank previousEnemyRank=CombatEffectTestConfiguration.EnemyRank;
        bool previousAllyTranscended=CombatEffectTestConfiguration.AllyTranscended;
        bool previousEnemyTranscended=CombatEffectTestConfiguration.EnemyTranscended;
        string[] previousTraits={CombatEffectTestConfiguration.AllyRank2TraitId,
            CombatEffectTestConfiguration.AllyRank3TraitId,
            CombatEffectTestConfiguration.EnemyRank2TraitId,
            CombatEffectTestConfiguration.EnemyRank3TraitId};
        try
        {
            CombatEffectTestConfiguration.MixedRank4v8=true;
            CombatEffectTestConfiguration.AllyRank=ExpeditionMonsterRank.Rank1;
            CombatEffectTestConfiguration.EnemyRank=ExpeditionMonsterRank.Rank3;
            CombatEffectTestConfiguration.AllyTranscended=true;
            CombatEffectTestConfiguration.EnemyTranscended=true;
            CombatEffectTestConfiguration.AllyRank2TraitId=
                CombatEffectTestConfiguration.EnemyRank2TraitId="trait_rank2_might";
            CombatEffectTestConfiguration.AllyRank3TraitId=
                CombatEffectTestConfiguration.EnemyRank3TraitId="trait_raptor_chain_leap";
            for(int i=0;i<roster.Length;i++)
            {
                bool ally=i<4;
                ExpeditionMonsterRank expected=i<6?ExpeditionMonsterRank.Rank3:
                    i<9?ExpeditionMonsterRank.Rank2:ExpeditionMonsterRank.Rank1;
                MonsterDefinition definition=roster[i];
                MonsterGrowthMasterRow growth=GameMasterData.MonsterGrowth(definition.monsterId);
                ExpeditionMonsterStateSave state=new ExpeditionMonsterStateSave
                {
                    transcended=true,growthTotal=ExpeditionGrowthSystem.MaximumGrowth,
                    rank2TraitId="stale_rank2",rank3TraitId="stale_rank3"
                };

                CombatEffectTestConfiguration.ApplyTestProgression(state,ally,i-4,definition);

                Assert.That(state.rank,Is.EqualTo(expected),definition.monsterId);
                Assert.That(state.growthTotal,Is.EqualTo(i<6?9:i<9?3:0));
                Assert.That(state.transcended,Is.False);
                Assert.That(state.rank2TraitId,Is.EqualTo(i<9?growth.fixedRank2TraitId:""));
                Assert.That(state.rank3TraitId,Is.EqualTo(i<6?growth.fixedRank3TraitId:""));
                var active=MonsterTraitRuntime.ActiveTraitIds(definition,null,state);
                string[] before=active.ToArray();
                CombatEffectTestConfiguration.AddSelectedTraits(active,ally);
                CollectionAssert.AreEqual(before,active,
                    "Mixed ranks must use each monster's own unlocked traits.");
                Assert.That(ExpeditionTraitProgression.ActiveTraitIds(state).Count,
                    Is.EqualTo(i<6?2:i<9?1:0));
            }
        }
        finally
        {
            CombatEffectTestConfiguration.MixedRank4v8=previousMixed;
            CombatEffectTestConfiguration.AllyRank=previousAllyRank;
            CombatEffectTestConfiguration.EnemyRank=previousEnemyRank;
            CombatEffectTestConfiguration.AllyTranscended=previousAllyTranscended;
            CombatEffectTestConfiguration.EnemyTranscended=previousEnemyTranscended;
            CombatEffectTestConfiguration.AllyRank2TraitId=previousTraits[0];
            CombatEffectTestConfiguration.AllyRank3TraitId=previousTraits[1];
            CombatEffectTestConfiguration.EnemyRank2TraitId=previousTraits[2];
            CombatEffectTestConfiguration.EnemyRank3TraitId=previousTraits[3];
        }
    }

    [Test]
    public void TestProgressionAppliesSelectedGradeAndTraits()
    {
        bool previousMixed=CombatEffectTestConfiguration.MixedRank4v8;
        ExpeditionMonsterRank previousRank=CombatEffectTestConfiguration.AllyRank;
        bool previousTranscended=CombatEffectTestConfiguration.AllyTranscended;
        string previousRank2=CombatEffectTestConfiguration.AllyRank2TraitId;
        string previousRank3=CombatEffectTestConfiguration.AllyRank3TraitId;
        try
        {
            CombatEffectTestConfiguration.MixedRank4v8=false;
            CombatEffectTestConfiguration.AllyRank=ExpeditionMonsterRank.Rank3;
            CombatEffectTestConfiguration.AllyTranscended=true;
            CombatEffectTestConfiguration.AllyRank2TraitId="trait_rank2_might";
            CombatEffectTestConfiguration.AllyRank3TraitId="trait_raptor_chain_leap";
            ExpeditionMonsterStateSave state=new ExpeditionMonsterStateSave();

            CombatEffectTestConfiguration.ApplyTestProgression(state,true);
            var active=new System.Collections.Generic.List<string>();
            CombatEffectTestConfiguration.AddSelectedTraits(active,true);

            Assert.That(state.rank,Is.EqualTo(ExpeditionMonsterRank.Rank3));
            Assert.That(state.transcended,Is.True);
            Assert.That(state.growthTotal,
                Is.EqualTo(ExpeditionGrowthSystem.MaximumGrowth));
            Assert.That(state.rank2TraitId,Is.EqualTo("trait_rank2_might"));
            Assert.That(state.rank3TraitId,Is.EqualTo("trait_raptor_chain_leap"));
            CollectionAssert.AreEqual(
                new[]{"trait_rank2_might","trait_raptor_chain_leap"},active);
        }
        finally
        {
            CombatEffectTestConfiguration.MixedRank4v8=previousMixed;
            CombatEffectTestConfiguration.AllyRank=previousRank;
            CombatEffectTestConfiguration.AllyTranscended=previousTranscended;
            CombatEffectTestConfiguration.AllyRank2TraitId=previousRank2;
            CombatEffectTestConfiguration.AllyRank3TraitId=previousRank3;
        }
    }

    [Test]
    public void LatestFamilyTraitsAreActiveInProduction()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        MonsterDefinition velos=database.Get("velos");
        string[] rank3=MonsterTraitCatalog.TestSelectable(velos,true)
            .Select(trait=>trait.id).ToArray();

        CollectionAssert.IsSubsetOf(new[]{
            "trait_raptor_blood_trail",
            "trait_raptor_chain_leap",
            "trait_raptor_pack_target"
        },rank3);
        Assert.That(rank3,Does.Not.Contain("trait_skill_remote_projection"));
        Assert.That(MonsterTraitCatalog.Rank3.Select(trait=>trait.id),
            Does.Contain("trait_raptor_chain_leap"));
    }

    [Test]
    public void FrogRank3TraitsAreSelectableInTestEnvironment()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        MonsterDefinition golgaebi=database.Get("golgaebi");

        string[] traits=MonsterTraitCatalog.TestSelectable(golgaebi,true)
            .Select(trait=>trait.id).ToArray();
        CollectionAssert.IsSubsetOf(new[]{
            "trait_frog_reflex_snatch","trait_frog_buff_thief",
            "trait_frog_hunting_chorus"
        },traits);
    }

    [Test]
    public void ProductionBattleSceneIsRuntimeLoadable()
    {
        Assert.IsTrue(EditorBuildSettings.scenes.Any(scene=>scene.enabled&&
            Path.GetFileNameWithoutExtension(scene.path)==
            CombatEffectTestSceneController.BattleSceneName));
    }

    [Test]
    public void TestSceneContainsOnlyPersistentSelector()
    {
        Scene scene=EditorSceneManager.OpenScene(
            CombatEffectTestSceneSetup.ScenePath,OpenSceneMode.Additive);
        try
        {
            GameObject[] roots=scene.GetRootGameObjects();
            Assert.AreEqual(1,roots.Length);
            Assert.IsNotNull(
                roots[0].GetComponent<CombatEffectTestSceneController>());
            Assert.IsNull(roots[0].GetComponent<Camera>());
            Assert.IsNull(roots[0].GetComponent<MonsterAutoBattlePrototype>());
        }
        finally
        {
            EditorSceneManager.CloseScene(scene,true);
        }
    }
}
