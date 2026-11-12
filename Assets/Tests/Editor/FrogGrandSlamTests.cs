using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class FrogGrandSlamTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private object source,primary;
    private Camera camera;
    private readonly List<object> enemies=new List<object>();
    private HashSet<GameObject> existing;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Grand Fishing gameplay test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",Hidden);
        var run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Hidden);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        BattleSet("combatBounds",new Bounds(Vector3.zero,Vector3.one*100f));BattleSet("mlEpisodeActive",true);
        source=Unit(0,Vector3.zero);Set(source,"augmentTranscended",true);
        for(int i=0;i<8;i++)enemies.Add(Unit(1,new Vector3(4f+i%3*2f,0f,(i/3-1)*2f)));
        primary=enemies[0];Set(source,"target",primary);
    }

    [TearDown]
    public void TearDown()
    {
        foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
            if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&created.transform.parent==null)Object.DestroyImmediate(created);
        enemies.Clear();
    }

    [TestCase(FrogCombatRules.TongueSlamTraitId)]
    [TestCase(FrogCombatRules.ReflexSnatchTraitId)]
    [TestCase(FrogCombatRules.BuffThiefTraitId)]
    public void EveryRankThreeChoicePullsAllEightThenHoversAndSlamsOnceWithTheExistingDamageRatios(string trait)
    {
        Traits(source).Add(trait);foreach(var enemy in enemies)AttachVisual(enemy);
        Begin();Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillTimer"),Is.EqualTo(7f));
        AdvanceTo(FrogGrandSlamRules.CastContact+.001f);
        Assert.That(Hp(primary),Is.EqualTo(4870f).Within(.01f));
        foreach(var enemy in enemies.Skip(1))Assert.That(Hp(enemy),Is.EqualTo(4935f).Within(.01f));
        AdvanceTo(FrogGrandSlamRules.LiftEnd);
        foreach(var enemy in enemies)
        {
            Assert.That(Status(enemy).Get(CombatStatusKind.Airborne).source,Is.SameAs(source));
            Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.SameAs(source));Pose(enemy);
            Assert.That(Visual(enemy).position.y,Is.EqualTo(FrogGrandSlamRules.LiftHeight).Within(.001f));
            Assert.That(Position(enemy).y,Is.Zero.Within(.001f));
            Assert.That(((GameObject)Get(enemy,"ring")).transform.position.y,Is.EqualTo(.025f).Within(.001f));
        }
        AdvanceTo(FrogGrandSlamRules.SlamStart);
        foreach(var enemy in enemies){Pose(enemy);Assert.That(Visual(enemy).position.y,Is.EqualTo(FrogGrandSlamRules.LiftHeight).Within(.001f));}
        AdvanceTo(FrogGrandSlamRules.HitTime+.001f);
        Assert.That(Hp(primary),Is.EqualTo(4820f).Within(.01f));
        foreach(var enemy in enemies.Skip(1))Assert.That(Hp(enemy),Is.EqualTo(4910f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(16));
        foreach(var enemy in enemies)
        {
            Pose(enemy);Assert.That(Visual(enemy).position.y,Is.Zero.Within(.001f));Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.Null);
            Assert.That(Status(enemy).Has(CombatStatusKind.Airborne),Is.False);
            Assert.That(Status(enemy).Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(1f).Within(.001f));
            Assert.That(Get(enemy,"frogSlowRatio"),Is.EqualTo(.5f));
        }
        AdvanceTo(FrogGrandSlamRules.Duration+.1f);Advance(2f);
        Assert.That(Get(source,"frogGrandSlam"),Is.Null);Assert.That(Get(source,"skillHits"),Is.EqualTo(16));
    }

    [Test]
    public void PrimaryDyingOnContactAndDistantTargetsDoNotCancelTheOtherSevenLandings()
    {
        Set(primary,"hp",100f);Begin();
        ((GameObject)Get(enemies[7],"model")).transform.position=new Vector3(35f,0f,15f);
        AdvanceTo(FrogGrandSlamRules.HitTime+.001f);
        Assert.That(Get(primary,"alive"),Is.False);
        foreach(var enemy in enemies.Skip(1))Assert.That(Hp(enemy),Is.EqualTo(4910f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(15));
    }

    [Test]
    public void ResistanceDoesNotEndTheScriptedCaptureAndFreezingTheCasterKeepsEveryVictimSuspended()
    {
        foreach(var enemy in enemies){Set(enemy,"statusResistance",45f);Status(enemy).harmfulDurationMultiplier=.2f;AttachVisual(enemy);}
        Begin();AdvanceTo(FrogGrandSlamRules.LiftEnd);
        Status(source).ApplyTimed(CombatStatusKind.Freeze,1f);
        for(int i=0;i<30;i++)
        {
            foreach(var enemy in enemies)Call("UpdateUnit",enemy,.025f);
            Advance(.025f);
        }
        Assert.That((float)Field(State,"time"),Is.EqualTo(FrogGrandSlamRules.LiftEnd).Within(.001f));
        foreach(var enemy in enemies)
        {
            Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.SameAs(source));Assert.That(Status(enemy).Has(CombatStatusKind.Airborne),Is.True);
            Pose(enemy);Assert.That(Visual(enemy).position.y,Is.EqualTo(FrogGrandSlamRules.LiftHeight).Within(.001f));
        }
        Status(source).Remove(CombatStatusKind.Freeze);AdvanceTo(FrogGrandSlamRules.HitTime+.001f);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(16));foreach(var enemy in enemies)Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.Null);
    }

    [TestCase("cleanse")]
    [TestCase("immune")]
    [TestCase("interrupt")]
    [TestCase("death")]
    public void OnlyTheAffectedVictimEscapesAndAnotherControllersAirborneIsNeverRemoved(string reason)
    {
        foreach(var enemy in enemies)AttachVisual(enemy);
        if(reason=="immune")Status(primary).ControlImmune=true;
        Begin();AdvanceTo(FrogGrandSlamRules.LiftEnd);Pose(primary);
        CombatStatusEffect foreign=null;
        if(reason=="cleanse")Status(primary).RemoveAllHarmful();
        if(reason=="interrupt")
        {
            Call("InterruptPositionalAction",primary);
            foreign=Status(primary).ApplyTimed(CombatStatusKind.Airborne,3f,enemies[1],3f);
        }
        if(reason=="death")Set(primary,"alive",false);
        Advance(.01f);
        Assert.That(Get(primary,"frogGrandSlamHeldBy"),Is.Null);Pose(primary);Assert.That(Visual(primary).position.y,Is.Zero.Within(.001f));
        if(foreign!=null)Assert.That(Status(primary).Get(CombatStatusKind.Airborne),Is.SameAs(foreign));
        AdvanceTo(FrogGrandSlamRules.HitTime+.001f);
        foreach(var enemy in enemies.Skip(1))Assert.That(Hp(enemy),Is.EqualTo(4910f).Within(.01f));
    }

    [TestCase("interrupt")]
    [TestCase("death")]
    [TestCase("wave")]
    [TestCase("disable")]
    public void CancellingTheCasterRestoresAllEightBodiesAndReleasesExactControlHolds(string cause)
    {
        foreach(var enemy in enemies)AttachVisual(enemy);Begin();AdvanceTo(FrogGrandSlamRules.LiftEnd);
        var controls=enemies.Select(enemy=>Status(enemy).Get(CombatStatusKind.Airborne)).ToList();
        if(cause=="interrupt")Call("InterruptPositionalAction",source);
        else if(cause=="death"){Set(source,"alive",false);Advance(.01f);}
        else if(cause=="wave")BattleSet("combatGeneration",1);
        else Call("OnDisable");
        Advance(.01f);
        Assert.That(Get(source,"frogGrandSlam"),Is.Null);
        for(int i=0;i<enemies.Count;i++)
        {
            Assert.That(controls[i].activeHolds,Is.Zero);Assert.That(Status(enemies[i]).Has(CombatStatusKind.Airborne),Is.False);
            Assert.That(Get(enemies[i],"frogGrandSlamHeldBy"),Is.Null);Pose(enemies[i]);Assert.That(Visual(enemies[i]).position.y,Is.Zero.Within(.001f));
            Assert.That(Hp(enemies[i]),Is.EqualTo(i==0?4870f:4935f).Within(.01f));
        }
    }

    [Test]
    public void PauseAndSkippedFramesCannotMoveTheLandingEarlierOrDuplicateTheDamage()
    {
        Begin();AdvanceTo(FrogGrandSlamRules.LiftEnd);BattleSet("paused",true);Advance(10f);
        Assert.That(Hp(primary),Is.EqualTo(4870f).Within(.01f));
        BattleSet("paused",false);Advance(10f);Advance(10f);
        Assert.That(Hp(primary),Is.EqualTo(4820f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(16));
    }

    [Test]
    public void ReplacingAirborneInPlaceReleasesOnlyThisCastsHoldAndPreservesTheNewSource()
    {
        AttachVisual(primary);Begin();AdvanceTo(FrogGrandSlamRules.LiftEnd);
        var owned=Status(primary).Get(CombatStatusKind.Airborne);
        var foreign=Status(primary).ApplyTimed(CombatStatusKind.Airborne,3f,enemies[1],3f);
        Assert.That(foreign,Is.SameAs(owned));Advance(.01f);
        Assert.That(Get(primary,"frogGrandSlamHeldBy"),Is.Null);
        Assert.That(Status(primary).Get(CombatStatusKind.Airborne),Is.SameAs(foreign));
        Assert.That(foreign.source,Is.SameAs(enemies[1]));Assert.That(foreign.activeHolds,Is.Zero);
        Pose(primary);Assert.That(Visual(primary).position.y,Is.Zero.Within(.001f));
    }

    [Test]
    public void AnotherAirborneDuringThePullReleasesOnlyThatVictimBeforeThisCastCanLiftIt()
    {
        Begin();AdvanceTo(FrogGrandSlamRules.CastContact+.1f);
        var foreign=Status(primary).ApplyTimed(CombatStatusKind.Airborne,3f,enemies[1],3f);
        Advance(.01f);Assert.That(Get(primary,"frogGrandSlamHeldBy"),Is.Null);
        AdvanceTo(FrogGrandSlamRules.LiftEnd);
        Assert.That(Status(primary).Get(CombatStatusKind.Airborne),Is.SameAs(foreign));
        Assert.That(foreign.source,Is.SameAs(enemies[1]));
        Assert.That(Get(enemies[1],"frogGrandSlamHeldBy"),Is.SameAs(source));
    }

    [Test]
    public void ReflectionKillingTheCasterOnASecondaryContactCannotOrphanControlOrDoubleThePreviousDamageCredit()
    {
        Set(source,"hp",1f);
        foreach(var enemy in enemies.Skip(1))Set(enemy,"corruptReflectionRatio",1f);
        Begin();AdvanceTo(FrogGrandSlamRules.CastContact+.001f);
        Assert.That(Get(source,"alive"),Is.False);Assert.That(State,Is.Null);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(195f).Within(.01f));
        foreach(var enemy in enemies)
        {Assert.That(Get(enemy,"frogGrandSlamHeldBy"),Is.Null);Assert.That(Status(enemy).Has(CombatStatusKind.Root),Is.False);}
    }

    [Test]
    public void PureHeightCurveHoldsThenAcceleratesDownToTheOnlyLandingFrame()
    {
        Assert.That(FrogGrandSlamRules.HeightAt(FrogGrandSlamRules.PullEnd),Is.Zero);
        Assert.That(FrogGrandSlamRules.HeightAt(FrogGrandSlamRules.LiftEnd),Is.EqualTo(2.8f));
        Assert.That(FrogGrandSlamRules.HeightAt(FrogGrandSlamRules.SlamStart),Is.EqualTo(2.8f));
        float fall=FrogGrandSlamRules.HitTime-FrogGrandSlamRules.SlamStart;
        Assert.That(2.8f-FrogGrandSlamRules.HeightAt(FrogGrandSlamRules.SlamStart+fall*.75f),
            Is.GreaterThan(10f*(2.8f-FrogGrandSlamRules.HeightAt(FrogGrandSlamRules.SlamStart+fall*.25f))));
        Assert.That(FrogGrandSlamRules.HeightAt(FrogGrandSlamRules.HitTime),Is.Zero);
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    [TestCase(0)]
    public void TheEightHeldEnemiesAreThrownInEightDirectionsDuringTheDropAndLandAtTheirSavedSeparateEndpoints(int fps)
    {
        foreach(var enemy in enemies)AttachVisual(enemy);Begin();AdvanceTo(FrogGrandSlamRules.SlamStart);
        var victims=(IList)Field(State,"victims");
        var endpoints=new Dictionary<object,Vector3>();
        var directions=new List<Vector3>();
        foreach(var victim in victims)
        {
            Assert.That(Field(victim,"throwing"),Is.True);
            object enemy=Field(victim,"unit");Vector3 start=(Vector3)Field(victim,"throwStart"),goal=(Vector3)Field(victim,"throwGoal");
            endpoints[enemy]=goal;directions.Add((Vector3)Field(victim,"throwDirection"));
            Assert.That(Vector3.Distance(start,goal),Is.GreaterThan(1.5f));
        }
        for(int a=0;a<directions.Count;a++)for(int b=a+1;b<directions.Count;b++)
            Assert.That(Vector3.Dot(directions[a],directions[b]),Is.LessThan(.71f));
        if(fps==0)Advance(5f);
        else
            while(State!=null&&(float)Field(State,"time")<FrogGrandSlamRules.HitTime+.001f-.000001f)
                Advance(Mathf.Min(1f/fps,FrogGrandSlamRules.HitTime+.001f-(float)Field(State,"time")));
        foreach(var enemy in enemies)
        {
            Assert.That(Vector3.Distance(Position(enemy),endpoints[enemy]),Is.LessThan(.001f));Pose(enemy);
            Assert.That(Visual(enemy).position.y,Is.Zero.Within(.001f));
            Assert.That(Vector3.Distance(((GameObject)Get(enemy,"ring")).transform.position,endpoints[enemy]+Vector3.up*.025f),Is.LessThan(.001f));
        }
        for(int a=0;a<enemies.Count;a++)for(int b=a+1;b<enemies.Count;b++)
            Assert.That(Vector3.Distance(Position(enemies[a]),Position(enemies[b])),Is.GreaterThan(.67f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(16));Assert.That(Hp(primary),Is.EqualTo(4820f).Within(.01f));
    }

    [TestCase("immune")]
    [TestCase("cleanse")]
    [TestCase("foreign")]
    public void EnemiesFreedBeforeTheThrowAreNotMovedOrRecapturedWhenTheOthersScatter(string reason)
    {
        if(reason=="immune")Status(primary).ControlImmune=true;
        Begin();AdvanceTo(FrogGrandSlamRules.LiftEnd);
        if(reason=="cleanse")Status(primary).RemoveAllHarmful();
        if(reason=="foreign")Call("InterruptPositionalAction",primary);
        Advance(.01f);Vector3 released=Position(primary);
        AdvanceTo(FrogGrandSlamRules.HitTime+.001f);
        Assert.That(Position(primary),Is.EqualTo(released));
        var victim=((IList)Field(State,"victims"))[0];Assert.That(Field(victim,"throwing"),Is.False);
        Assert.That(Get(primary,"frogGrandSlamHeldBy"),Is.Null);
        Assert.That(((IList)Field(State,"victims")).Cast<object>().Count(item=>(bool)Field(item,"throwing")),Is.EqualTo(7));
    }

    [Test]
    public void TheScatterLandsInsideArenaBoundsAndItsSavedPathsCannotCrossAWall()
    {
        Begin();AdvanceTo(FrogGrandSlamRules.LiftEnd);
        BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(24f,10f,24f)));
        var tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",Hidden);
        object wall=Activator.CreateInstance(tileType,true);var cell=new Vector2Int(2,2);
        tileType.GetField("coordinate").SetValue(wall,cell);tileType.GetField("world").SetValue(wall,new Vector3(3f,0f,4f));
        tileType.GetField("walkable").SetValue(wall,false);((IDictionary)BattleGet("tiles")).Add(cell,wall);
        AdvanceTo(FrogGrandSlamRules.SlamStart);
        foreach(var victim in (IList)Field(State,"victims"))
        {
            Vector3 start=(Vector3)Field(victim,"throwStart"),goal=(Vector3)Field(victim,"throwGoal");
            Assert.That(goal.x,Is.InRange(-11.7f,11.7f));Assert.That(goal.z,Is.InRange(-11.7f,11.7f));
            Assert.That(Call("HasClearPathSegment",start,goal,.3f),Is.True);
        }
        AdvanceTo(FrogGrandSlamRules.HitTime+.001f);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(16));
    }

    [Test]
    public void AHostileIceWallCreatedAfterTheThrowWasPlannedStillBlocksEveryMovementSegment()
    {
        Begin();AdvanceTo(FrogGrandSlamRules.SlamStart);
        object victim=((IList)Field(State,"victims"))[0];
        Vector3 start=(Vector3)Field(victim,"throwStart"),goal=(Vector3)Field(victim,"throwGoal");
        Vector3 normal=(goal-start).normalized;
        Call("RegisterRank3IceWall",source,Vector3.Lerp(start,goal,.4f),normal,Vector3.Cross(Vector3.up,normal),5f,5f);
        Assert.That(Call("IsHostileIceWallBlocking",primary,start,goal),Is.True);
        while((float)Field(State,"time")<FrogGrandSlamRules.HitTime+.001f-.000001f)
        {
            Vector3 previous=Position(primary);Advance(.01f);
            Assert.That(Call("IsHostileIceWallBlocking",primary,previous,Position(primary)),Is.False);
        }
        Assert.That(Vector3.Distance(Position(primary),goal),Is.GreaterThan(.5f));
        Assert.That(Get(primary,"frogGrandSlamHeldBy"),Is.Null);Assert.That(Get(source,"skillHits"),Is.EqualTo(16));
    }

    [Test]
    public void AnAllyEnteringTheSavedLandingPointCannotBeOverlappedByTheFallingEnemy()
    {
        Begin();AdvanceTo(FrogGrandSlamRules.SlamStart);
        object victim=((IList)Field(State,"victims"))[0];Vector3 goal=(Vector3)Field(victim,"throwGoal");
        object blocker=Unit(0,goal);AdvanceTo(FrogGrandSlamRules.HitTime+.001f);
        Assert.That(Vector3.Distance(Position(primary),Position(blocker)),Is.GreaterThanOrEqualTo(.599f));
        Assert.That(Get(primary,"frogGrandSlamHeldBy"),Is.Null);Assert.That(Get(source,"skillHits"),Is.EqualTo(16));
    }

    private object Unit(int side,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);var list=(IList)BattleGet("units");
        var model=new GameObject("Grand fishing unit");model.transform.position=position;
        Set(unit,"id",list.Count);Set(unit,"model",model);Set(unit,"ring",new GameObject("Grand fishing ground ring"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,side));Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_golgaebi"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",FrogCombatRules.SkillId);Set(unit,"alive",true);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);
        Set(unit,"collisionRadius",.3f);Set(unit,"skillCooldownDuration",7f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"lastGroundPosition",position);
        Status(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);list.Add(unit);return unit;
    }
    private void AttachVisual(object unit)
    {
        if(camera==null)camera=new GameObject("Grand fishing pose camera").AddComponent<Camera>();
        var child=new GameObject("Grand fishing sprite");child.transform.SetParent(((GameObject)Get(unit,"model")).transform,false);
        var renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
        Sprite[] frames={renderer.sprite,renderer.sprite,renderer.sprite,renderer.sprite};
        var driver=child.AddComponent<SpriteMonsterAnimationDriver>();driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        var billboard=child.AddComponent<SpriteMonsterBillboard>();typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
    }
    private void Begin()=>Assert.That(Call("BeginTongueHarpoon",source,GameMasterData.Skill(FrogCombatRules.SkillId)),Is.True);
    private void Advance(float dt)=>Call("AdvanceFrogGrandSlam",source,dt);
    private void AdvanceTo(float target)
    {
        while(State!=null&&(float)Field(State,"time")<target-.000001f)
            Advance(Mathf.Min(.025f,target-(float)Field(State,"time")));
    }
    private object State=>Get(source,"frogGrandSlam");
    private object Field(object instance,string field)=>instance.GetType().GetField(field,Hidden).GetValue(instance);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object BattleGet(string field)=>typeof(MonsterAutoBattlePrototype).GetField(field,Hidden).GetValue(battle);
    private void BattleSet(string field,object value)=>typeof(MonsterAutoBattlePrototype).GetField(field,Hidden).SetValue(battle,value);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Hidden).Invoke(battle,args);
    private List<string> Traits(object unit)=>(List<string>)Get(unit,"activeTraitIds");
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private Vector3 Position(object unit)=>((GameObject)Get(unit,"model")).transform.position;
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private Transform Visual(object unit)=>((SpriteMonsterBillboard)Get(unit,"spriteBillboard")).transform;
    private void Pose(object unit)=>typeof(SpriteMonsterBillboard).GetMethod("LateUpdate",Hidden).Invoke(Get(unit,"spriteBillboard"),null);
}
