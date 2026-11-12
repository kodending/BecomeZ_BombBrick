using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class FrogCatchCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private Camera camera;
    private CombatCameraDirector director;
    private Type unitType;
    private object source,primary,previousFrontend;
    private readonly List<object> enemies=new List<object>();
    private readonly List<Vector3> startingPositions=new List<Vector3>();
    private HashSet<GameObject> existing;
    private float previousSpeed;
    private bool previousTestMode;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Hidden).GetValue(null);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,null);
        Assert.That(CombatSkillClock.Active,Is.False,"An earlier cinematic must release its actor clock.");
        Time.timeScale=1f;CombatEffectTestConfiguration.Active=true;
        var host=new GameObject("Inactive frog catch integration test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",Hidden);
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",false);
        BattleSet("expeditionRun",new ExpeditionRunSave {regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());
        BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        camera=new GameObject("Frog catch integration camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-12f),Quaternion.Euler(35f,0f,0f));
        camera.fieldOfView=40f;camera.aspect=16f/9f;
        director=camera.gameObject.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        BattleSet("mainCamera",camera);BattleSet("combatCameraDirector",director);
        source=Unit(0,Vector3.zero,"golgaebi");Set(source,"augmentTranscended",true);
        for(int i=0;i<8;i++)
        {
            Vector3 position=new Vector3(4f+i%3*2f,0f,(i/3-1)*2f);
            enemies.Add(Unit(1,position,i%2==0?"geomchi":"tino"));startingPositions.Add(position);
        }
        primary=enemies[0];Set(source,"target",primary);
    }

    [TearDown]
    public void TearDown()
    {
        if(battle!=null)
        {
            Call("CancelFrogCatchCinematic");Call("CancelCommonSkillFocus");
            Call("ResetFamilyTranscendenceStates");
        }
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)
                Object.DestroyImmediate(value);
        enemies.Clear();startingPositions.Clear();
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"Cleanup must release the global actor freeze.");
    }

    [TestCase(.5f)]
    [TestCase(5f)]
    public void ALongContactFrameStopsAtTheCommittedFirstHitBeforeTheCinematic(float dt)
    {
        Begin();object cast=Cast;Advance(dt);
        AssertContactOnly(cast);Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(Field(Queue[0],"unit"),Is.SameAs(source));Assert.That(Field(Queue[0],"state"),Is.SameAs(cast));
        Assert.That(Field(Queue[0],"generation"),Is.EqualTo(BattleGet("combatGeneration")));
        Assert.That(Call("IsFrogCatchCinematicPending",source),Is.True);
        Advance(5f);AssertContactOnly(cast);Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void TheGlobalFreezeReturnsTheSameCastAtTheThrowWithoutSpendingOrDamagingTwice()
    {
        Begin();object cast=Cast;Advance(.5f);StartStage();
        Assert.That(Field(Cinematic,"unit"),Is.SameAs(source));Assert.That(Field(Cinematic,"cast"),Is.SameAs(cast));
        Assert.That(CombatSkillClock.ActorFrozen(Model(source).transform),Is.True);
        foreach(var enemy in enemies)Assert.That(CombatSkillClock.ActorFrozen(Model(enemy).transform),Is.True);
        Vector3[] held=enemies.Select(Position).ToArray();
        AssertContactOnly(cast);
        Call("FinishFrogCatchCinematic",true);
        Assert.That(Cast,Is.SameAs(cast));Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(CastTime,Is.EqualTo(FrogGrandSlamRules.SlamStart).Within(.00002f));
        Assert.That(Field(cast,"contacted"),Is.True);Assert.That(Field(cast,"landed"),Is.False);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));AssertFirstHitHealth();
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(enemies.Where((enemy,i)=>Vector3.Distance(Position(enemy),held[i])>.01f).Any(),Is.True,
            "The handoff must advance the real pull instead of only changing the visible stage.");
        foreach(var enemy in enemies)
        {
            Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.SameAs(source));
            Assert.That(Status(enemy).Get(CombatStatusKind.Airborne).source,Is.SameAs(source));
        }
        AdvanceTo(FrogGrandSlamRules.HitTime-.001f);AssertFirstHitHealth();
        Advance(.002f);
        Assert.That(Hp(primary),Is.EqualTo(4820f).Within(.01f));
        foreach(var enemy in enemies.Skip(1))Assert.That(Hp(enemy),Is.EqualTo(4910f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(16));Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        foreach(var enemy in enemies)Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.Null);
        Advance(5f);Assert.That(Get(source,"skillHits"),Is.EqualTo(16));
    }

    [Test]
    public void FullyImmuneEnemiesKeepTheOrdinaryDamageWithoutStartingAnEmptyStage()
    {
        foreach(var enemy in enemies)Status(enemy).ControlImmune=true;
        Begin();Advance(.5f);
        Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);
        Assert.That(UpdateCinematicAfterPrelude("UpdateFrogCatchCinematic"),Is.False);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(CastTime,Is.GreaterThan(FrogGrandSlamRules.CastContact));AssertFirstHitHealth();
        AdvanceTo(FrogGrandSlamRules.HitTime+.001f);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(16));
        Assert.That(Hp(primary),Is.EqualTo(4820f).Within(.01f));
        foreach(var enemy in enemies)Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.Null);
    }

    [Test]
    public void APrimaryKilledOnContactDoesNotRemoveTheOtherSevenCapturedEnemiesOrTheirStage()
    {
        Set(primary,"hp",100f);Begin();object cast=Cast;Advance(.5f);
        Assert.That(Get(primary,"alive"),Is.False);Assert.That(Captured(cast).Count,Is.EqualTo(7));
        Assert.That(Captured(cast).Any(victim=>Field(victim,"unit")==primary),Is.False);
        Assert.That(Queue.Count,Is.EqualTo(1));StartStage();
        Assert.That(Field(Cinematic,"cast"),Is.SameAs(cast));Call("FinishFrogCatchCinematic",true);
        AdvanceTo(FrogGrandSlamRules.HitTime+.001f);
        foreach(var enemy in enemies.Skip(1))Assert.That(Hp(enemy),Is.EqualTo(4910f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(15));
    }

    [Test]
    public void ReflectionKillingTheCasterDuringContactCannotQueueACinematicOrOrphanAControlHold()
    {
        Set(source,"hp",1f);foreach(var enemy in enemies.Skip(1))Set(enemy,"corruptReflectionRatio",1f);
        Begin();Advance(.5f);
        Assert.That(Get(source,"alive"),Is.False);Assert.That(Cast,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(UpdateCinematicAfterPrelude("UpdateFrogCatchCinematic"),Is.False);Assert.That(Cinematic,Is.Null);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(195f).Within(.01f));
        foreach(var enemy in enemies)
        {Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.Null);Assert.That(Status(enemy).Has(CombatStatusKind.Root),Is.False);}
        Assert.That(CombatSkillClock.Active,Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CancellingTheStagePreservesTheCommittedCastAndRestoresCameraSpeedAndVisibility(bool wasHidden)
    {
        Time.timeScale=2f;Renderer(source).forceRenderingOff=wasHidden;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        float fieldOfView=camera.fieldOfView;Vector2 lens=camera.lensShift;Matrix4x4 projection=camera.projectionMatrix;
        Begin();object cast=Cast;Advance(.5f);StartStage();
        Assert.That((float)Field(Cinematic,"savedSpeed"),Is.EqualTo(2f));
        Call("CancelFrogCatchCinematic");
        Assert.That(Cast,Is.SameAs(cast));Assert.That(CastTime,Is.EqualTo(FrogGrandSlamRules.CastContact).Within(.00002f));
        Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(director.enabled,Is.True);
        Assert.That(Renderer(source).forceRenderingOff,Is.EqualTo(wasHidden));
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fieldOfView));Assert.That(camera.lensShift,Is.EqualTo(lens));
        for(int row=0;row<4;row++)for(int column=0;column<4;column++)
            Assert.That(camera.projectionMatrix[row,column],Is.EqualTo(projection[row,column]).Within(.00001f));
        AssertFirstHitHealth();Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Advance(.1f);Assert.That(CastTime,Is.GreaterThan(FrogGrandSlamRules.CastContact));
    }

    [TestCase("interrupt")]
    [TestCase("death")]
    [TestCase("generation")]
    [TestCase("disable")]
    public void InvalidatingTheCastReleasesTheStageClockAndItsExactCapturedControls(string reason)
    {
        Time.timeScale=2f;Begin();Advance(.5f);
        var roots=enemies.Select(enemy=>Status(enemy).Get(CombatStatusKind.Root)).ToArray();StartStage();
        if(reason=="interrupt")Call("InterruptPositionalAction",source);
        else if(reason=="death")Set(source,"alive",false);
        else if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else Call("OnDisable");
        UpdateCinematicAfterPrelude("UpdateFrogCatchCinematic");Advance(0f);
        Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(Cast,Is.Null);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(2f));
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(director.enabled,Is.True);
        for(int i=0;i<enemies.Count;i++)
        {
            Assert.That(roots[i].activeHolds,Is.Zero);Assert.That(Get(enemies[i],"frogGrandSlamHeldBy"),Is.Null);
            Assert.That(Status(enemies[i]).Has(CombatStatusKind.Root),Is.False);
        }
        AssertFirstHitHealth();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisablingTheBattleAfterItsEffectWasDestroyedStillReleasesTheCatch(bool duringStage)
    {
        Begin();Advance(.5f);if(duringStage)StartStage();
        var effect=(PixelFrogGrandSlam)Field(Cast,"effect");Assert.That(effect,Is.Not.Null);
        Object.DestroyImmediate(effect.gameObject);
        Assert.That(effect==null,Is.True);
        Assert.DoesNotThrow(()=>Call("OnDisable"));
        Assert.That(Cast,Is.Null);Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        foreach(var enemy in enemies)
        {
            Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.Null);
            Assert.That(Status(enemy).Has(CombatStatusKind.Root),Is.False);
            Assert.That(Status(enemy).Has(CombatStatusKind.Airborne),Is.False);
        }
    }

    [Test]
    public void AQueuedCatchWaitsWithoutStealingAnotherCinematicsExclusiveClock()
    {
        Begin();Advance(.5f);object cast=Cast;var foreign=new CombatSkillClock(camera.transform);
        try
        {
            foreign.Hold();Assert.That(UpdateCinematicAfterPrelude("UpdateFrogCatchCinematic"),Is.False);
            Assert.That(Cinematic,Is.Null);Assert.That(Queue.Count,Is.EqualTo(1));AssertContactOnly(cast);
        }
        finally{foreign.End();}
        StartStage();Assert.That(Field(Cinematic,"cast"),Is.SameAs(cast));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PausingOrFreezingDuringTheStageRestoresTheWorldWithoutSkippingTheCommittedCast(bool freeze)
    {
        Begin();object cast=Cast;Advance(.5f);StartStage();
        if(freeze)Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);else BattleSet("paused",true);
        UpdateCinematicAfterPrelude("UpdateFrogCatchCinematic");
        Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Cast,Is.SameAs(cast));Assert.That(CastTime,Is.EqualTo(FrogGrandSlamRules.CastContact).Within(.00002f));
        AssertFirstHitHealth();Advance(.5f);
        Assert.That(CastTime,Is.EqualTo(FrogGrandSlamRules.CastContact).Within(.00002f));
        if(freeze)Status(source).Remove(CombatStatusKind.Freeze);else BattleSet("paused",false);
        Advance(.1f);Assert.That(CastTime,Is.GreaterThan(FrogGrandSlamRules.CastContact));
    }

    [TestCase("golgaebi",0)]
    [TestCase("golgaebi",1)]
    [TestCase("kingpa",0)]
    [TestCase("kingpa",1)]
    [TestCase("rokrok",0)]
    [TestCase("rokrok",1)]
    [TestCase("frosk",0)]
    [TestCase("frosk",1)]
    public void TheSameMonsterGetsOnlyItsFirstCatchStageInEachWaveOnEitherTeam(string species,int team)
    {
        if(species!="golgaebi")
        {
            ((IList)BattleGet("units")).Remove(source);source=Unit(0,Vector3.zero,species);
            Set(source,"augmentTranscended",true);Set(source,"target",primary);
        }
        SetTeam(team);Begin();Advance(.5f);StartStage();Call("FinishFrogCatchCinematic",true);
        Assert.That(Get(source,"frogCatchCinematicGeneration"),Is.EqualTo(BattleGet("combatGeneration")));
        Call("EndFrogGrandSlam",source,true);ResetForNextCast();
        Begin();Advance(.5f);Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);
        Assert.That(Call("IsFrogCatchCinematicPending",source),Is.False);Assert.That(Get(source,"skillUses"),Is.EqualTo(2));
        Call("EndFrogGrandSlam",source,true);ResetForNextCast();
        BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        Begin();Advance(.5f);Assert.That(Queue.Count,Is.EqualTo(1));StartStage();
        Assert.That(Get(source,"skillUses"),Is.EqualTo(3));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void AnotherMonsterOfTheSameFamilyHasItsOwnFirstStageAllowance(int team)
    {
        SetTeam(team);Begin();Advance(.5f);StartStage();Call("CancelFrogCatchCinematic");
        Call("EndFrogGrandSlam",source,true);ResetForNextCast();
        object second=Unit(team,Vector3.back*.8f,"golgaebi");Set(second,"augmentTranscended",true);Set(second,"target",primary);
        Assert.That(Call("BeginTongueHarpoon",second,GameMasterData.Skill(FrogCombatRules.SkillId)),Is.True);
        Call("AdvanceFrogGrandSlam",second,.5f);
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(Field(Queue[0],"unit"),Is.SameAs(second));
        Assert.That(UpdateCinematicAfterPrelude("UpdateFrogCatchCinematic"),Is.True);
        Assert.That(Field(Cinematic,"unit"),Is.SameAs(second));Assert.That(Get(second,"skillUses"),Is.EqualTo(1));
    }

    [Test]
    public void ATransformedVictimKeepsItsLiveSpriteWhileOrdinaryVictimsKeepTheirAuthoredLoop()
    {
        object enemy=enemies[0];var driver=Driver(enemy);var renderer=Renderer(enemy);
        Sprite original=renderer.sprite;
        Sprite[] expected=driver.DragFrames!=null&&driver.DragFrames.Length>0?driver.DragFrames:driver.IdleFrames;
        Assert.That(expected,Is.Not.Empty);
        var ordinary=(Sprite[])Field(Call("FrogCatchActor",enemy,1f,false),"frames");
        CollectionAssert.AreEqual(expected,ordinary,"An ordinary victim must retain its complete Drag or Idle loop.");
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        Sprite transformed=null;
        try
        {
            texture.SetPixels(new[]{Color.magenta,Color.magenta,Color.magenta,Color.magenta});texture.Apply();
            transformed=Sprite.Create(texture,new Rect(0f,0f,2f,2f),Vector2.one*.5f,2f);
            renderer.sprite=transformed;
            var frames=(Sprite[])Field(Call("FrogCatchActor",enemy,1f,false),"frames");
            Assert.That(frames,Has.Length.EqualTo(1));Assert.That(frames[0],Is.SameAs(transformed),
                "A live transformed body outside the authored animation arrays must not revert to its original species.");
        }
        finally
        {
            renderer.sprite=original;
            if(transformed!=null)Object.DestroyImmediate(transformed);
            Object.DestroyImmediate(texture);
        }
    }

    private void AssertContactOnly(object cast)
    {
        Assert.That(Cast,Is.SameAs(cast));Assert.That(CastTime,Is.EqualTo(FrogGrandSlamRules.CastContact).Within(.00002f));
        Assert.That(Field(cast,"contacted"),Is.True);Assert.That(Field(cast,"landed"),Is.False);
        Assert.That(Field(cast,"hits"),Is.EqualTo(8));Assert.That(Captured(cast).Count,Is.EqualTo(8));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillHits"),Is.Zero);AssertFirstHitHealth();
    }
    private void AssertFirstHitHealth()
    {
        Assert.That(Hp(primary),Is.EqualTo(4870f).Within(.01f));
        foreach(var enemy in enemies.Skip(1))Assert.That(Hp(enemy),Is.EqualTo(4935f).Within(.01f));
    }
    private void StartStage()
    {
        Assert.That(UpdateCinematicAfterPrelude("UpdateFrogCatchCinematic"),Is.True);Assert.That(Cinematic,Is.Not.Null);
        Assert.That(Time.timeScale,Is.Zero);Assert.That(CombatSkillClock.Active,Is.True);
        CollectionAssert.AreEquivalent(Captured(Cast),((IList)Field(Cinematic,"victims")).Cast<object>().ToArray(),
            "The presentation must retain the successful live capture references, including a missing dead primary.");
    }
    private void ResetForNextCast()
    {
        Call("CancelFrogCatchCinematic");
        Set(source,"skillTimer",0f);Driver(source).InterruptCurrentAction(true);
        for(int i=0;i<enemies.Count;i++)
        {Status(enemies[i]).RemoveAllHarmful();Model(enemies[i]).transform.position=startingPositions[i];Driver(enemies[i]).InterruptCurrentAction(true);}
    }
    private void SetTeam(int team)
    {
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        foreach(var enemy in enemies)Set(enemy,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
    }
    private object Unit(int team,Vector3 position,string species)
    {
        object unit=Activator.CreateInstance(unitType,true);var list=(IList)BattleGet("units");
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null);
        var authored=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);Assert.That(authored,Is.Not.Null);
        var model=new GameObject("Frog catch actor "+species);model.transform.position=position;
        var visual=new GameObject("Authored sprite");visual.transform.SetParent(model.transform,false);
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        driver.Configure(Frames(authored,"idleFrames"),Frames(authored,"runFrames"),Frames(authored,"attackFrames"),
            Frames(authored,"skillAttackFrames"),Frames(authored,"deathFrames"),Frames(authored,"captureFrames"),Frames(authored,"struggleFrames"));
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        visual.transform.rotation=camera.transform.rotation;
        var ring=new GameObject("Frog catch shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",list.Count);Set(unit,"model",model);Set(unit,"ring",ring);Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"definition",definition);Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",FrogCombatRules.SkillId);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));Set(unit,"alive",true);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);
        Set(unit,"collisionRadius",.3f);Set(unit,"skillCooldownDuration",7f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"lastGroundPosition",position);
        Status(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);list.Add(unit);return unit;
    }
    private static Sprite[] Frames(SpriteMonsterAnimationDriver authored,string field)=>(Sprite[])Field(authored,field);
    private static List<object> Captured(object cast)=>((IList)Field(cast,"victims")).Cast<object>().Where(victim=>(bool)Field(victim,"captured")).ToList();
    private void Begin()=>Assert.That(Call("BeginTongueHarpoon",source,GameMasterData.Skill(FrogCombatRules.SkillId)),Is.True);
    private void Advance(float dt)=>Call("AdvanceFrogGrandSlam",source,dt);
    private void AdvanceTo(float time)
    {
        for(int i=0;Cast!=null&&CastTime<time-.000001f;i++)
        {Assert.That(i,Is.LessThan(200),"A pending cinematic must not silently stall the live cast.");Advance(Mathf.Min(.025f,time-CastTime));}
    }
    private object Cast=>Get(source,"frogGrandSlam");
    private float CastTime=>(float)Field(Cast,"time");
    private object Cinematic=>BattleGet("frogCatchCinematic");
    private IList Queue=>(IList)BattleGet("frogCatchCinematicQueue");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private Vector3 Position(object unit)=>Model(unit).transform.position;
    private SpriteMonsterAnimationDriver Driver(object unit)=>(SpriteMonsterAnimationDriver)Get(unit,"spriteAnimation");
    private SpriteRenderer Renderer(object unit)=>Driver(unit).GetComponent<SpriteRenderer>();
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private object Get(object unit,string name)=>unitType.GetField(name,Hidden).GetValue(unit);
    private void Set(object unit,string name,object value)=>unitType.GetField(name,Hidden).SetValue(unit,value);
    private object BattleGet(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).GetValue(battle);
    private void BattleSet(string name,object value)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).SetValue(battle,value);
    private static object Field(object value,string name)=>value.GetType().GetField(name,Hidden).GetValue(value);
    private object UpdateCinematicAfterPrelude(string method)
    {
        object result=Call(method);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        object prelude=typeof(MonsterAutoBattlePrototype).GetField("rank4SkillPrelude",flags).GetValue(battle);
        if(prelude==null)return result;
        prelude.GetType().GetField("elapsed",flags).SetValue(prelude,.451f);
        Call("UpdateRank4SkillPrelude");
        return Call(method);
    }

    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Hidden).Invoke(battle,args);
}
