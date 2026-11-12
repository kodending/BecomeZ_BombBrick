using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class BattleStartPositionTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> objects=new List<GameObject>();
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private object unit;
    private UnityEngine.Random.State randomState;
    private float timeScale,captureDeltaTime;

    [SetUp] public void Setup()
    {
        randomState=UnityEngine.Random.state;
        timeScale=Time.timeScale;captureDeltaTime=Time.captureDeltaTime;
        var host=Go("Battle start position test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        Field("expeditionRun").SetValue(battle,new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId});
        Field("playerProfile").SetValue(battle,new PlayerProfileSave());
        Field("combatBounds").SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
        Field("tileBlock").SetValue(battle,new MaterialPropertyBlock());
        Field("placementMarkerRoot").SetValue(battle,Go("Test markers").transform);
        Tile(Vector2Int.zero,false);
        Tile(new Vector2Int(-1,-1),true);

        var definition=Resources.LoadAll<MonsterDefinition>("MonsterData").First(item=>item.monsterId=="velos");
        Assert.That(definition,Is.Not.Null);
        unit=Activator.CreateInstance(unitType,true);
        Set("id",1);Set("definition",definition);
        Set("ownedInstance",PlayerProfileStore.CreateMonster(definition,"battle_start_position_test",false));
        Set("model",Go("Test ally"));Set("ring",Go("Test ring"));
        Set("footprint",Vector2Int.one);
        Set("baseModelScale",Vector3.one);
        Set("baseVisualHeightOffset",MonsterAutoBattlePrototype.PixelMonsterGroundOffset);
        Set("alive",true);Set("hp",100f);Set("maxHp",100f);Set("collisionRadius",.3f);
        ((IList)Field("units").GetValue(battle)).Add(unit);
    }

    [TearDown] public void Cleanup()
    {
        foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);
        objects.Clear();
        UnityEngine.Random.state=randomState;
        Time.timeScale=timeScale;Time.captureDeltaTime=captureDeltaTime;
    }

    [TestCase(false)] [TestCase(true)]
    public void StartTitlePunchFollowsTheDrumsAndRespectsReducedFlashes(bool reduced)
    {
        bool previous=CombatFlashPresentation.Reduced;
        try
        {
            CombatFlashPresentation.Apply(reduced);
            float opening=(float)Call("BattleStartCueScale",0f);
            Assert.That(opening,Is.GreaterThan(1f));
            Assert.That(opening,Is.LessThanOrEqualTo(reduced?1.071f:1.281f));
            foreach(float hit in new[]{.137f,.312f})
            {
                Assert.That((float)Call("BattleStartCueScale",hit-.001f),Is.EqualTo(1f).Within(.0001f));
                Assert.That((float)Call("BattleStartCueScale",hit),Is.GreaterThan(1f));
            }
            Assert.That((float)Call("BattleStartCueScale",.41f),Is.EqualTo(1f).Within(.0001f));
        }
        finally{CombatFlashPresentation.Apply(previous);}
    }

    [Test] public void FastStartCueReachesTheFinalDrumBeforeCombatAndIgnoresRepeatedStart()
    {
        ((ExpeditionRunSave)Field("expeditionRun").GetValue(battle)).regionId="poison_fog_swamp";
        Call("StartCurrentBattle");
        Assert.That(Field("waveIntroActive").GetValue(battle),Is.True);
        Call("UpdateWaveIntro",.2f);Call("StartCurrentBattle");
        Assert.That(Field("waveIntroElapsed").GetValue(battle),Is.EqualTo(.2f));
        Call("UpdateWaveIntro",.12f);
        Assert.That(Field("battleStarted").GetValue(battle),Is.False,"Keep the final drum impact visible before engagement.");
        Call("UpdateWaveIntro",.23f);
        Assert.That(Field("waveIntroActive").GetValue(battle),Is.False);
        Assert.That(Field("battleStarted").GetValue(battle),Is.True,"The new cue must finish within .55 seconds.");
    }

    [TestCase(false)] [TestCase(true)]
    public void RealCombatEntryAutomaticallySummonsTheGriffinOrbWithoutSpendingItsNormalSkill(bool simulation)
    {
        Set("definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_pone"));
        Set("augmentTranscended",true);Set("skillTimer",6.25f);Set("skillCooldownDuration",9f);
        Set("skillUses",4);Set("currentAction","awaiting_battle");
        Field("mlEpisodeActive").SetValue(battle,simulation);
        Assert.That(((IList)Field("griffinCrosswinds").GetValue(battle)).Count,Is.Zero);
        Call("BeginSavedBattle");
        Assert.That(Field("battleStarted").GetValue(battle),Is.True);
        Assert.That(((IList)Field("griffinCrosswinds").GetValue(battle)).Count,Is.EqualTo(1));
        Assert.That(Get("skillTimer"),Is.EqualTo(6.25f));Assert.That(Get("skillUses"),Is.EqualTo(4));
        Assert.That(Get("currentAction"),Is.EqualTo("awaiting_battle"));
        Call("BeginGriffinStormOrbs");
        Assert.That(((IList)Field("griffinCrosswinds").GetValue(battle)).Count,Is.EqualTo(1));
        Call("ClearGriffinCrosswinds");
    }

    [TestCase(true,false)]
    [TestCase(false,false)]
    [TestCase(true,true)]
    [TestCase(false,true)]
    public void RetryDoesNotResumePreviousBattleDisplacement(bool died,bool stored)
    {
        Model.transform.position=new Vector3(6f,0f,2f);
        Assert.That(Call("DisplaceUnit",unit,Vector3.right,2f,true,false,false,false),Is.True);
        Set("displacementProgress",.4f);
        Set("displacementArcHeight",1f);
        Set("personalityPendingTarget",unit);Set("personalityPendingTimer",1f);
        Set("personalityMoveTimer",1f);Set("personalityCueCooldown",5f);
        Set("initialTile",stored?new Vector2Int(-1,-1):Vector2Int.zero);
        Set("alive",!died);
        Set("inStorage",stored);
        Field("battleStarted").SetValue(battle,true);

        Call("PrepareNextWave",false);
        Assert.That(Get("alive"),Is.True);
        Assert.That(Get("inStorage"),Is.EqualTo(stored));
        if(stored)
        {
            Call("OccupyUnit",unit,Vector2Int.zero);
            Set("inStorage",false);
            Call("ApplyDeploymentTransform",unit);
        }
        Vector3 deployment=Model.transform.position;
        Field("battleStarted").SetValue(battle,true);
        Call("UpdateUnit",unit,0f);

        Assert.That(Vector3.Distance(Model.transform.position,deployment),Is.LessThan(.0001f),
            "The first combat update must not resume an old knockback from another position.");
        Assert.That(Get("displacing"),Is.False);
        Assert.That(Get("displacementArcHeight"),Is.EqualTo(0f));
        Assert.That(Get("personalityPendingTarget"),Is.Null);
        Assert.That(Get("personalityMoveTimer"),Is.EqualTo(0f));
        Assert.That(Get("personalityCueCooldown"),Is.EqualTo(0f));
    }

    private void Tile(Vector2Int coordinate,bool bench)
    {
        Type type=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        object tile=Activator.CreateInstance(type,true);
        var go=Go("Test tile "+coordinate);
        type.GetField("coordinate").SetValue(tile,coordinate);
        type.GetField("bench").SetValue(tile,bench);
        type.GetField("walkable").SetValue(tile,!bench);
        type.GetField("world").SetValue(tile,Call("GridWorld",coordinate,true));
        type.GetField("gameObject").SetValue(tile,go);
        type.GetField("renderer").SetValue(tile,go.AddComponent<MeshRenderer>());
        ((IDictionary)Field("tiles").GetValue(battle)).Add(coordinate,tile);
    }

    [Test] public void NavigationPreparationRunsBeforeActorsAreHiddenWithoutChangingCombatState()
    {
        object enemy=Activator.CreateInstance(unitType,true);
        var enemyModel=Go("Navigation preparation enemy");
        var goal=new Vector2Int(1,0);
        enemyModel.transform.position=(Vector3)Call("GridWorld",goal,true);
        unitType.GetField("model").SetValue(enemy,enemyModel);
        unitType.GetField("team").SetValue(enemy,Enum.ToObject(unitType.GetField("team").FieldType,1));
        unitType.GetField("alive").SetValue(enemy,true);
        unitType.GetField("currentTile").SetValue(enemy,goal);
        unitType.GetField("collisionRadius").SetValue(enemy,.3f);
        ((IList)Field("units").GetValue(battle)).Add(enemy);
        Tile(goal,false);
        Type tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        foreach(object tile in ((IDictionary)Field("tiles").GetValue(battle)).Values)
            tileType.GetField("walkable").SetValue(tile,true);
        Call("OccupyUnit",unit,Vector2Int.zero);
        Model.transform.position=(Vector3)Call("GridWorld",Vector2Int.zero,true);
        Set("target",enemy);
        Set("hp",83f);
        var route=(List<Vector3>)Get("route");
        route.Add(new Vector3(7f,0f,3f));
        Vector3 position=Model.transform.position;
        UnityEngine.Random.State beforeRandom=UnityEngine.Random.state;
        var preparation=(IEnumerator)Call("WarmupEditorNavigation");
        int steps=0;
        try{while(preparation.MoveNext())steps++;}
        finally{((IDisposable)preparation).Dispose();}
        Assert.That(steps,Is.EqualTo(4),"Prepare routes and attack positions for both deployed actors.");
        Assert.That(Get("target"),Is.SameAs(enemy));
        Assert.That(route,Is.EqualTo(new[]{new Vector3(7f,0f,3f)}));
        Assert.That(Model.transform.position,Is.EqualTo(position));
        Assert.That(Get("currentTile"),Is.EqualTo(Vector2Int.zero));
        Assert.That(Get("hp"),Is.EqualTo(83f));
        Assert.That(((IDictionary)Field("occupancy").GetValue(battle))[Vector2Int.zero],Is.SameAs(unit));
        Assert.That(UnityEngine.Random.state,Is.EqualTo(beforeRandom));

        Call("HideMapIntroductionObjects");
        var hiddenPreparation=(IEnumerator)Call("WarmupEditorNavigation");
        try
        {
            Assert.That(hiddenPreparation.MoveNext(),Is.False,"Hidden actors must not run navigation queries or schedule unit warmup.");
        }
        finally{((IDisposable)hiddenPreparation).Dispose();}
    }

    [Test] public void DeploymentRefreshPaintsFinalZonesAndKeepsBenchTilesBlocked()
    {
        var tiles=(IDictionary)Field("tiles").GetValue(battle);
        for(int row=0;row<MonsterAutoBattlePrototype.BattleDeploymentRows;row++)
        for(int column=0;column<MonsterAutoBattlePrototype.HexDeploymentColumns;column++)
        {
            var coordinate=new Vector2Int(column,row);
            if(!tiles.Contains(coordinate))Tile(coordinate,false);
        }
        Call("ApplyBattlefieldLayoutForCurrentWave");
        Call("GeneratePlayerDeploymentZone");
        Type tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        foreach(DictionaryEntry pair in tiles)
        {
            var coordinate=(Vector2Int)pair.Key;
            object tile=pair.Value;
            bool bench=(bool)tileType.GetField("bench").GetValue(tile);
            bool ally=!bench&&coordinate.x<MonsterAutoBattlePrototype.AllyDeploymentColumns;
            bool enemy=!bench&&coordinate.x>=MonsterAutoBattlePrototype.HexDeploymentColumns-
                MonsterAutoBattlePrototype.EnemyDeploymentColumns;
            object team=tileType.GetField("deploymentTeam").GetValue(tile);
            Assert.That(team==null?(int?)null:Convert.ToInt32(team),Is.EqualTo(ally?(int?)0:enemy?(int?)1:null));
            Assert.That(tileType.GetField("walkable").GetValue(tile),Is.EqualTo(!bench));
            Assert.That(tileType.GetField("neutralZone").GetValue(tile),Is.EqualTo(!bench&&!ally&&!enemy));
            Color neutral=MonsterAutoBattlePrototype.BattleTileColor(coordinate);
            Color expected=bench?new Color(.075f,.18f,.24f):ally
                ?Color.Lerp(neutral,new Color(.025f,.32f,.55f),.48f):enemy
                ?Color.Lerp(neutral,new Color(.48f,.055f,.04f),.42f)
                :Color.Lerp(neutral,new Color(.42f,.34f,.13f),.34f);
            var renderer=(Renderer)tileType.GetField("renderer").GetValue(tile);
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            Assert.That(Vector4.Distance((Vector4)block.GetColor("_BaseColor"),(Vector4)expected),Is.LessThan(1e-6f));
            var marker=(GameObject)tileType.GetField("placementMarker").GetValue(tile);
            Assert.That(marker,Is.Not.Null);
            Assert.That(marker.activeSelf,Is.True);
        }
        var markerRoot=(Transform)Field("placementMarkerRoot").GetValue(battle);
        Assert.That(markerRoot.childCount,Is.EqualTo(tiles.Count));
    }

    [TestCase("before")]
    [TestCase("after")]
    [TestCase("removed")]
    [TestCase("bench")]
    public void OpeningShieldUsesFinalEquipmentWhenCombatActuallyBegins(string equipment)
    {
        var run=(ExpeditionRunSave)Field("expeditionRun").GetValue(battle);
        var owned=(OwnedMonsterSave)Get("ownedInstance");
        run.monsters.Add(new ExpeditionMonsterStateSave{instanceId=owned.instanceId,monsterId=owned.monsterId});
        var item=ExpeditionItemSystem.CreateItem(run,"complete_crown_carapace");
        string error;
        if(equipment!="after")Assert.That(ExpeditionItemSystem.Equip(run,owned.instanceId,item.instanceId,0,out error),Is.True);
        Set("initialTile",Vector2Int.zero);
        Model.AddComponent<SpriteRenderer>().sprite=PixelStarProjectile.BeanSprite;
        Field("balanceTestRunning").SetValue(battle,true);

        Call("PrepareNextWave",false);
        Assert.That(Get("itemShield"),Is.EqualTo(0f),"Opening protection must not be granted during preparation.");
        if(equipment=="after")Assert.That(ExpeditionItemSystem.Equip(run,owned.instanceId,item.instanceId,0,out error),Is.True);
        if(equipment=="removed")Assert.That(ExpeditionItemSystem.Unequip(run,owned.instanceId,0,out error),Is.True);
        Call("RefreshUnitFromOwnedData",unit,Get("definition"),owned);
        if(equipment=="bench")Set("inStorage",true);

        Call("BeginSavedBattle");
        bool shielded=equipment!="removed"&&equipment!="bench";
        Assert.That(Get("itemShield"),Is.EqualTo(shielded?(float)Get("maxHp")*.15f:0f).Within(.001f));
        Assert.That(Get("itemShieldTimer"),Is.EqualTo(shielded?8f:0f));
        var fx=(PixelUnitShield)Get("shieldVisual");
        Assert.That(fx!=null,Is.EqualTo(shielded));
        if(!shielded)return;
        Assert.That(fx.GetComponentInChildren<MeshRenderer>().enabled,Is.True,"Reveal the shield on the very first combat frame.");
        Call("UpdateUnit",unit,7.9f);
        Assert.That((float)Get("itemShield"),Is.GreaterThan(0f));
        Call("UpdateUnit",unit,.11f);
        Assert.That(Get("itemShield"),Is.EqualTo(0f));
        Call("PrepareNextWave",false);
        Call("BeginSavedBattle");
        Assert.That(Get("itemShield"),Is.EqualTo((float)Get("maxHp")*.15f).Within(.001f),"Retry grants one shield, without stacking the previous attempt.");
    }

    [Test] public void ProtagonistStartsEmptyTriggersOnceAndResetsForNextBattle()
    {
        var run=(ExpeditionRunSave)Field("expeditionRun").GetValue(battle);
        var owned=(OwnedMonsterSave)Get("ownedInstance");
        run.augments.Add(new ExpeditionAugmentSelectionSave{
            augmentId=ExpeditionAugmentRuntime.Protagonist,targetMonsterInstanceId=owned.instanceId});
        Set("initialTile",Vector2Int.zero);
        Call("PrepareNextWave",false);
        Assert.That(Call("ProtagonistGaugeRatio",unit),Is.EqualTo(0f));
        Assert.That(Call("AddProtagonistGauge",unit,4f),Is.True);
        Call("AddProtagonistGauge",unit,25f);
        Call("AddProtagonistGauge",unit,-20f);
        Assert.That(Get("augmentProtagonistGauge"),Is.EqualTo(29f));
        Assert.That(Call("ProtagonistGaugeRatio",unit),Is.EqualTo(.29f).Within(.001f));

        // Keep the actual threshold and healing logic, without starting scene VFX in an EditMode test.
        Model.SetActive(false);
        Call("AddProtagonistGauge",unit,71f);
        Assert.That(Get("augmentClimaxUsed"),Is.True);
        Assert.That(Get("augmentClimaxTimer"),Is.EqualTo(8f));
        Assert.That(Call("ProtagonistGaugeRatio",unit),Is.EqualTo(1f));
        Set("augmentClimaxTimer",4f);
        Assert.That(Call("ProtagonistGaugeRatio",unit),Is.EqualTo(.5f));
        Set("augmentClimaxTimer",0f);
        Assert.That(Call("AddProtagonistGauge",unit,100f),Is.False);
        Assert.That(Call("ProtagonistGaugeRatio",unit),Is.EqualTo(0f));
        Call("PrepareNextWave",false);
        Assert.That(Get("augmentClimaxUsed"),Is.False);
        Assert.That(Get("augmentProtagonistGauge"),Is.EqualTo(0f));
    }

    [Test] public void ProtagonistGaugeLeavesVisibleEmptyInteriorOnTheSharedPixelGrid()
    {
        var health=new Rect(101,87,52,8);
        var gauge=(Rect)Call("ProtagonistGaugeRect",health);
        var top=BombBrickUiTheme.PixelRect(new Rect(gauge.x,gauge.y,gauge.width,2));
        var bottom=BombBrickUiTheme.PixelRect(new Rect(gauge.x,gauge.yMax-2,gauge.width,2));
        Assert.That(bottom.y-top.yMax,Is.GreaterThanOrEqualTo(6f));
        Assert.That(gauge.y,Is.GreaterThanOrEqualTo(health.y+14f),"Keep a gap below the skill cooldown bar.");
    }
    [TestCase(false)] [TestCase(true)]
    public void RetryAndNextWaveClearAllPreviousEffectPlaybackBeforePreparingActors(bool newLayout)
    {
        var target=Go("Old visual target");target.transform.position=Vector3.right*4f;
        int damageCallbacks=0;
        var effect=PixelStarProjectile.PlayBean(Model.transform,null,target.transform,null,
            Color.white,false,1f,()=>damageCallbacks++);
        objects.Add(effect.gameObject);
        CombatEffectEndPlayback.Begin(effect.gameObject,new HashSet<Type>{typeof(PixelStarProjectile)});
        var finalImpact=Go("Old projectile final impact");
        finalImpact.AddComponent<CombatEffectEndMarker>();
        var particles=finalImpact.AddComponent<ParticleSystem>();
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.loop=false;main.startLifetime=5f;
        particles.Play();particles.Emit(3);
        Assert.That(particles.particleCount,Is.GreaterThan(0));
        Assert.That(finalImpact.GetComponent<CombatEffectEndPlayback>(),Is.Null,
            "A final impact created after capture is not registered with the original playback.");
        int generation=(int)Field("combatGeneration").GetValue(battle);

        if(!newLayout)Call("PrepareNextWave",false);
        else
        {
            var type=typeof(MonsterAutoBattlePrototype);
            int x=(int)type.GetProperty("ActivePlayerDeployStartColumn",Private).GetValue(null);
            int y=(int)type.GetProperty("ActivePlayerDeployStartRow",Private).GetValue(null);
            int columns=(int)type.GetProperty("ActivePlayerDeployColumns",Private).GetValue(null);
            int rows=(int)type.GetProperty("ActivePlayerDeployRows",Private).GetValue(null);
            var tiles=(IDictionary)Field("tiles").GetValue(battle);
            for(int row=y;row<y+rows;row++)
            for(int column=x;column<x+columns;column++)
            {
                var coordinate=new Vector2Int(column,row);
                if(!tiles.Contains(coordinate))Tile(coordinate,false);
            }
            object enemy=Activator.CreateInstance(unitType,true);
            unitType.GetField("definition").SetValue(enemy,Get("definition"));
            unitType.GetField("team").SetValue(enemy,Enum.ToObject(unitType.GetField("team").FieldType,1));
            unitType.GetField("model").SetValue(enemy,Go("Old enemy visual"));
            unitType.GetField("ring").SetValue(enemy,Go("Old enemy ring"));
            unitType.GetField("stableId").SetValue(enemy,"enemy_effect_reset_test");
            unitType.GetField("resourcePath").SetValue(enemy,"test/effect_reset");
            ((IList)Field("units").GetValue(battle)).Add(enemy);
            Field("wavePreparationAcrossFrames").SetValue(battle,true);
            var preparation=(IEnumerator)Call("PrepareNextWaveRoutine",true);
            try
            {
                Assert.That(preparation.MoveNext(),Is.True,
                    "Inspect the transition at its first enemy-rebuild yield, before loading new actors.");
            }
            finally
            {
                (preparation as IDisposable)?.Dispose();
                Field("recyclingEnemyVisuals").SetValue(battle,false);
                Field("wavePreparationAcrossFrames").SetValue(battle,false);
            }
        }
        Assert.That(Field("combatGeneration").GetValue(battle),Is.EqualTo(generation+1));
        Assert.That(effect==null||!effect.gameObject.activeSelf,Is.True);
        Assert.That(finalImpact==null||!finalImpact.activeSelf,Is.True,
            "The transition must clear late impact particles as well as registered playback.");
        Assert.That(damageCallbacks,Is.Zero);
    }

    private GameObject Model=>(GameObject)Get("model");
    private GameObject Go(string name){var go=new GameObject(name);objects.Add(go);return go;}
    private static FieldInfo Field(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private);
    private void Set(string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private object Get(string name)=>unitType.GetField(name).GetValue(unit);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private).Invoke(battle,args);
}
