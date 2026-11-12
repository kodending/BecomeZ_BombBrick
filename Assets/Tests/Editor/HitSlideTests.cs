using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class HitSlideTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> objects=new List<GameObject>();
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private IList units;
    private object attacker,victim,previousFrontend;
    private MonsterDefinition definition;
    private float previousTimeScale;

    [SetUp] public void Setup()
    {
        previousTimeScale=Time.timeScale;Time.timeScale=1f;
        previousFrontend=typeof(GameFrontend).GetField("instance",Private).GetValue(null);
        typeof(GameFrontend).GetField("instance",Private).SetValue(null,null);
        GameObject host=Go("Inactive hit slide test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle);
        BattleField("combatBounds",new Bounds(Vector3.zero,new Vector3(24f,10f,18f)));
        FieldInfo run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        BattleField("mlEpisodeActive",true);BattleField("battleStarted",true);
        definition=Resources.LoadAll<MonsterDefinition>("MonsterData").First(d=>d.monsterId=="velos");
        attacker=Unit(1,Vector3.left*1.5f);victim=Unit(0,Vector3.zero);
        Set(victim,"target",attacker);Set(attacker,"target",victim);
    }

    [TearDown] public void Cleanup()
    {
        foreach(GameObject go in objects)if(go!=null)Object.DestroyImmediate(go);
        objects.Clear();Time.timeScale=previousTimeScale;
        typeof(GameFrontend).GetField("instance",Private).SetValue(null,previousFrontend);
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void AHitMovesTheActualGroundBodyAndSettlesAtTheSameDistanceAcrossFrameRates(int fps)
    {
        Set(victim,"visualHeightOffset",.2f);Model(victim).transform.position=Vector3.up*.2f;
        Begin(false,true);Advance(1f/fps);
        Assert.That(Position(victim).x,Is.GreaterThan(.005f));
        Assert.That(Position(victim).y,Is.EqualTo(.2f));
        Finish(fps);
        Assert.That(Position(victim).x,Is.EqualTo(3f*3f/(2f*9.2f)).Within(.0001f));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That((Vector3)Get(victim,"lastGroundPosition"),Is.EqualTo(new Vector3(Position(victim).x,0f,0f)));
        Assert.That(Model(victim).transform.position.x,Is.EqualTo(((GameObject)Get(victim,"ring")).transform.position.x));
        Vector3 settled=Position(victim);Advance(.5f);Assert.That(Position(victim),Is.EqualTo(settled));
    }

    [TestCase(false)] [TestCase(true)]
    public void AHitSlidesWithoutRestartingOrCancellingAnAttackOrSkill(bool skill)
    {
        var visual=Go("Hit victim sprite");visual.transform.SetParent(Model(victim).transform,false);
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);driver.ConfigureHitFrames(3,3);
        Set(victim,"spriteAnimation",driver);Set(victim,"currentAction","pending_authored_hit");
        Set(victim,"attackMovementLockTimer",1f);Set(victim,"skillTimer",7f);Set(victim,"cooldown",4f);
        Vector3 aiVelocity=new Vector3(0f,0f,2f);Set(victim,"velocity",aiVelocity);
        int hits=0;if(skill)driver.PlaySkillAttack(1f,()=>hits++);else driver.PlayBasicAttack(1f,()=>hits++);
        Begin(false,true);Advance(.05f);
        Assert.That(Position(victim).x,Is.GreaterThan(.05f));
        Assert.That(driver.CurrentMotionName,Is.EqualTo(skill?"SkillAttack":"Attack"));
        Assert.That(driver.IsActionLocked,Is.True);Assert.That(hits,Is.Zero);
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
        Assert.That(Get(victim,"velocity"),Is.EqualTo(aiVelocity));
        Assert.That(Get(victim,"currentAction"),Is.EqualTo("pending_authored_hit"));
        Assert.That(Get(victim,"attackMovementLockTimer"),Is.EqualTo(1f));
        Assert.That(Get(victim,"skillTimer"),Is.EqualTo(7f));Assert.That(Get(victim,"cooldown"),Is.EqualTo(4f));
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{.65f});
        Finish(60);
        Assert.That(hits,Is.EqualTo(1),"Physical hit movement must preserve the original one-shot impact callback.");
        Assert.That(Get(victim,"positionRevision"),Is.EqualTo(0));
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test] public void TheNormalUnitLoopAppliesHitSlideThroughAnAttackLock()
    {
        Set(victim,"attackMovementLockTimer",1f);Begin(false,true);
        for(int i=0;i<8;i++)Call("UpdateUnit",victim,1f/60f);
        Assert.That(Position(victim).x,Is.GreaterThan(.07f));
        Assert.That(Get(victim,"basicAttackCount"),Is.EqualTo(0));
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
    }

    [TestCase(30,0)] [TestCase(60,0)] [TestCase(120,0)]
    [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)]
    public void MeleeCombatKeepsRealHitMovementAndReapproachesAfterARepeatedImpact(int fps,int team)
    {
        Set(victim,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(attacker,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
        float contact=(float)Call("MeleeContactDistance",victim,attacker),side=team==0?1f:-1f;
        Vector3 origin=Vector3.right*(side*contact*.5f),opponentGoal=-origin;
        Model(victim).transform.position=origin;Model(attacker).transform.position=opponentGoal;
        foreach(object actor in new[]{attacker,victim})
        {
            Set(actor,"lastGroundPosition",Position(actor));Set(actor,"cooldown",0f);
            Set(actor,"damage",20f);Set(actor,"attackInterval",.8f);
        }
        Vector3 away=(origin-opponentGoal).normalized;float furthest=0f;
        bool sawSlide=false,repeated=false,reapproached=false;
        Begin(false,true);float dt=1f/fps;
        for(int frame=0;frame<fps*3;frame++)
        {
            BattleField("battleElapsed",frame*dt);
            if(!repeated&&frame*dt>=.15f){Begin(false,true);repeated=true;}
            bool sliding=((Vector3)Get(victim,"hitSlideVelocity")).sqrMagnitude>.000001f;
            Call("UpdateUnit",victim,dt);
            float previousDistance=Vector3.Distance(Position(victim),Position(attacker));
            Call("UpdateUnit",attacker,dt);
            float distance=Vector3.Distance(Position(victim),Position(attacker));
            if(previousDistance>(float)Call("MeleeArrivalDistance",attacker,victim)+.025f&&distance<previousDistance-.00001f)
                reapproached=true;
            float displacement=Vector3.Dot(Position(victim)-origin,away);furthest=Mathf.Max(furthest,displacement);
            sawSlide|=sliding;
            Assert.That(distance,Is.GreaterThanOrEqualTo(.599f));
        }
        Assert.That(sawSlide,Is.True);Assert.That(furthest,Is.GreaterThan(.6f));
        Assert.That(reapproached,Is.True,"A real impact outside the arrival band must resume pursuit without a stored destination.");
        float settled=Vector3.Distance(Position(victim),Position(attacker));
        Assert.That(settled,Is.GreaterThanOrEqualTo((float)Call("MeleeContactDistance",victim,attacker)-.002f));
        Assert.That(settled,Is.LessThanOrEqualTo((float)Call("MeleeArrivalDistance",victim,attacker)+.025f));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(victim,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Get(attacker,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That((float)Get(victim,"hp"),Is.LessThan(5000f));Assert.That((float)Get(attacker,"hp"),Is.LessThan(5000f));
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
        Assert.That(Get(attacker,"target"),Is.SameAs(victim));
    }

    [TestCase(2f,false,1)] [TestCase(4f,false,1)]
    [TestCase(2f,true,1)] [TestCase(4f,true,1)] [TestCase(4f,true,3)]
    public void AdvancingActorsYieldOnlyLocomotionToTheHitThenResumeTheirOriginalPursuit(float speed,bool critical,int range)
    {
        float distance=range>=3?11f:8f;
        Assert.That(distance,Is.GreaterThan(MonsterAutoBattlePrototype.AttackDistanceForRange(range,.3f,.3f)+.5f));
        Model(attacker).transform.position=Vector3.left*distance;
        Set(victim,"attackRange",range);Set(victim,"speedTilesPerSecond",speed/MonsterAutoBattlePrototype.TileSize);
        Set(victim,"velocity",Vector3.left*speed);Set(victim,"inertiaVelocity",Vector3.left*speed);
        Set(victim,"moving",true);Set(victim,"cooldown",4f);Set(victim,"skillTimer",7f);
        var statuses=(CombatStatusState)Get(victim,"combatStatuses");
        statuses.ApplyTimed(CombatStatusKind.AttackSpeedDown,2f);
        var driver=CreateDriver();driver.SetMoving(true);
        Begin(critical,true);
        Assert.That(Get(victim,"inertiaVelocity"),Is.EqualTo(Vector3.zero),"An impact replaces the previous carried movement momentum.");
        const float dt=1f/60f;int frames=0;
        while(((Vector3)Get(victim,"hitSlideVelocity")).sqrMagnitude>.000001f&&frames++<60)
        {
            Vector3 previous=Position(victim);
            Call("UpdateUnit",victim,dt);
            StepDriver(driver,dt);
            Assert.That(Position(victim).x,Is.GreaterThanOrEqualTo(previous.x-.00001f),
                "The actual body must move away from the attacker even when its original walk was faster than the hit impulse.");
            Assert.That(Get(victim,"target"),Is.SameAs(attacker));
            Assert.That(driver.CurrentMotionName,Is.EqualTo("Run"));
        }
        float impulse=critical?3.3f:3f;
        Assert.That(Position(victim).x,Is.EqualTo(impulse*impulse/(2f*9.2f)).Within(.0002f));
        Assert.That((float)Get(victim,"cooldown"),Is.LessThan(4f));
        Assert.That((float)Get(victim,"skillTimer"),Is.LessThan(7f));
        Assert.That(statuses.Get(CombatStatusKind.AttackSpeedDown).remaining,Is.LessThan(2f).And.GreaterThan(1f));
        Assert.That(Get(victim,"basicAttackCount"),Is.EqualTo(0));
        Assert.That(Get(victim,"positionRevision"),Is.EqualTo(0));
        float afterHit=Position(victim).x;
        for(int i=0;i<20;i++)Call("UpdateUnit",victim,dt);
        Assert.That(Position(victim).x,Is.LessThan(afterHit-.1f),"Ordinary pursuit must resume once the physical hit has settled.");
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase(false)] [TestCase(true)]
    public void TheFullUnitLoopKeepsAnAuthoredImpactCallbackRunningDuringHitMovement(bool skill)
    {
        Model(attacker).transform.position=Vector3.left*8f;
        var driver=CreateDriver();int hits=0;
        if(skill)driver.PlaySkillAttack(1f,()=>hits++);else driver.PlayBasicAttack(1f,()=>hits++);
        Set(victim,"cooldown",4f);Set(victim,"skillTimer",7f);Begin(false,true);
        const float dt=1f/60f;
        Call("UpdateUnit",victim,dt);StepDriver(driver,dt);
        Assert.That(Position(victim).x,Is.GreaterThan(.02f));
        Assert.That(driver.CurrentMotionName,Is.EqualTo(skill?"SkillAttack":"Attack"));
        for(int i=0;i<44;i++){Call("UpdateUnit",victim,dt);StepDriver(driver,dt);}
        Assert.That(hits,Is.EqualTo(1));
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
        Assert.That((float)Get(victim,"cooldown"),Is.LessThan(4f));
        Assert.That((float)Get(victim,"skillTimer"),Is.LessThan(7f));
    }

    [Test] public void StartingAnAttackDuringHitSlideCannotReseedTheOldForwardCoast()
    {
        var driver=CreateDriver();int hits=0;driver.PlayBasicAttack(1f,()=>hits++);
        Set(victim,"velocity",Vector3.left*4f);Set(victim,"moving",true);Begin(false,true);
        Call("StopForBasicAttack",victim);
        Assert.That(Get(victim,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Call("AdvanceUnitInertia",victim,.05f);Advance(.05f);
        Assert.That(Position(victim).x,Is.GreaterThan(.05f));
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Attack"));
        StepDriver(driver,.65f);Assert.That(hits,Is.EqualTo(1));
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
    }

    [Test] public void AnOrdinaryHexStepAlsoYieldsWithoutLosingItsDestination()
    {
        Type tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        object tile=Activator.CreateInstance(tileType,true);Vector2Int destination=Vector2Int.left;
        tileType.GetField("coordinate").SetValue(tile,destination);
        tileType.GetField("world").SetValue(tile,Vector3.left*MonsterAutoBattlePrototype.TileSize);
        ((IDictionary)typeof(MonsterAutoBattlePrototype).GetField("tiles",Private).GetValue(battle)).Add(destination,tile);
        Set(victim,"hexStepping",true);Set(victim,"hexStepTarget",destination);
        Set(victim,"velocity",Vector3.left*4f);Begin(false,true);
        const float dt=1f/60f;
        while(((Vector3)Get(victim,"hitSlideVelocity")).sqrMagnitude>.000001f)
        {
            Call("UpdateHiddenHexStep",victim,dt);Advance(dt);
            Assert.That(Get(victim,"hexStepping"),Is.EqualTo(true));
            Assert.That(Get(victim,"hexStepTarget"),Is.EqualTo(destination));
        }
        float afterHit=Position(victim).x;Assert.That(afterHit,Is.GreaterThan(.2f));
        Call("UpdateHiddenHexStep",victim,dt);
        Assert.That(Position(victim).x,Is.LessThan(afterHit));
    }

    [Test] public void SkillCriticalsAreStrongerButARepeatedCrowdHitCannotAccumulateUnlimitedSpeed()
    {
        Begin(false,true);float ordinary=((Vector3)Get(victim,"hitSlideVelocity")).magnitude;
        Set(victim,"hitSlideVelocity",Vector3.zero);BattleField("battleElapsed",.2f);
        Begin(true,true);float critical=((Vector3)Get(victim,"hitSlideVelocity")).magnitude;
        Assert.That(ordinary,Is.EqualTo(3f).Within(.00001f));
        Assert.That(critical,Is.EqualTo(3.3f).Within(.00001f));Assert.That(critical,Is.GreaterThan(ordinary));
        for(int i=0;i<20;i++)Begin(true,true);
        Assert.That(((Vector3)Get(victim,"hitSlideVelocity")).magnitude,Is.EqualTo(critical));
        for(int i=0;i<20;i++){BattleField("battleElapsed",1f+i*.13f);Begin(true,true);}
        Assert.That(((Vector3)Get(victim,"hitSlideVelocity")).magnitude,Is.LessThanOrEqualTo(3.30001f));
        Advance(10f);
        Assert.That(Position(victim).x,Is.InRange(.59f,.593f),"Even a long frame integrates only the capped remaining slide.");
    }

    [TestCase(false,false,false)] [TestCase(false,true,false)] [TestCase(false,true,true)]
    [TestCase(true,false,false)] [TestCase(true,false,true)]
    public void OnlyDirectSkillDamageContactSeedsThePhysicalHit(bool skill,bool basic,bool critical)
    {
        Set(attacker,"lastBasicAttackCritical",critical);Set(attacker,"lastSkillCritical",critical);
        float applied=(float)Call("ApplyDamage",attacker,victim,10f,skill,basic,true,false,0f,0f,1f,false,false,false);
        Assert.That(applied,Is.GreaterThan(0f));
        float slideSquared=((Vector3)Get(victim,"hitSlideVelocity")).sqrMagnitude;
        if(skill)Assert.That(slideSquared,Is.GreaterThan(0f));
        else Assert.That(slideSquared,Is.Zero,"Basic hits, critical basics and periodic damage must never start physical hit movement.");
    }

    [TestCase(false)] [TestCase(true)]
    public void BasicHitsPreserveVoluntaryAndCarriedMovementAndDoNotThrottleTheNextSkillHit(bool critical)
    {
        Vector3 walking=Vector3.left*3f,carried=Vector3.left*2f;
        Set(victim,"velocity",walking);Set(victim,"inertiaVelocity",carried);Set(victim,"moving",true);
        Set(victim,"currentAction","ordinary_pursuit");Set(attacker,"lastBasicAttackCritical",critical);
        Begin(critical,false);
        Call("ApplyDamage",attacker,victim,10f,false,true,true,false,0f,0f,1f,false,false,false);
        Assert.That(Get(victim,"velocity"),Is.EqualTo(walking));
        Assert.That(Get(victim,"inertiaVelocity"),Is.EqualTo(carried));
        Assert.That(Get(victim,"moving"),Is.EqualTo(true));
        Assert.That(Get(victim,"currentAction"),Is.EqualTo("ordinary_pursuit"));
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(victim,"hitSlideNextAt"),Is.EqualTo(0f));
        Call("AdvanceUnitInertia",victim,.02f);Assert.That(Position(victim).x,Is.LessThan(0f));
        Begin(false,true);
        Assert.That(((Vector3)Get(victim,"hitSlideVelocity")).magnitude,Is.GreaterThan(0f),
            "A basic hit must not consume the skill hit's victim rate limit.");
    }

    [TestCase(false)] [TestCase(true)]
    public void BasicHitsCannotStackRestartOrExtendAnExistingSkillHitSlide(bool critical)
    {
        Begin(false,true);Advance(.03f);
        Vector3 velocity=(Vector3)Get(victim,"hitSlideVelocity");
        float next=(float)Get(victim,"hitSlideNextAt");
        BattleField("battleElapsed",.2f);Set(attacker,"lastBasicAttackCritical",critical);
        for(int i=0;i<5;i++)
        {
            Begin(critical,false);
            Call("ApplyDamage",attacker,victim,10f,false,true,true,false,0f,0f,1f,false,false,false);
        }
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(velocity));
        Assert.That(Get(victim,"hitSlideNextAt"),Is.EqualTo(next));
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
        Advance(1f);
        Assert.That(Position(victim).x,Is.EqualTo(3f*3f/(2f*9.2f)).Within(.0001f));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
    }

    [TestCase("control")] [TestCase("debuff")] [TestCase("held")] [TestCase("displacing")]
    [TestCase("static")] [TestCase("force-immune")] [TestCase("channel")] [TestCase("freeze")]
    public void AuthoredControlAndPositionOwnershipRejectAndClearHitSlideWithoutFiringImmunityCallbacks(string reason)
    {
        Begin(false,true);var statuses=(CombatStatusState)Get(victim,"combatStatuses");int blocked=0;
        statuses.harmfulBlocked=()=>blocked++;
        if(reason=="control")statuses.ControlImmune=true;
        if(reason=="debuff")statuses.DebuffImmune=true;
        if(reason=="held")Set(victim,"preyDraggedBy",attacker);
        if(reason=="displacing")Set(victim,"displacing",true);
        if(reason=="static")Set(victim,"rank3MycokingSummon",true);
        if(reason=="force-immune")Set(victim,"rank3ForceImmuneTimer",1f);
        if(reason=="channel")Set(victim,"crocodileBiteLockTimer",1f);
        if(reason=="freeze")statuses.ApplyTimed(CombatStatusKind.Freeze,1f);
        Advance(.05f);Begin(true,true);
        Assert.That(Position(victim),Is.EqualTo(Vector3.zero));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(blocked,Is.Zero,"A visual/physical reaction must not trigger gameplay immunity callbacks.");
    }

    [TestCase(false)][TestCase(true)]
    public void MammothChargeAndRicochetOwnTheirMovementInsteadOfMixingWithAHitSlide(bool ricochet)
    {
        object state=ricochet?null:Activator.CreateInstance(typeof(MonsterAutoBattlePrototype).GetNestedType("MammothExpulsionState",BindingFlags.NonPublic),true);
        string action=ricochet?"mammoth_chain_slide":"mammoth_expulsion_charge";
        Set(victim,"mammothExpulsion",state);Set(victim,"mammothRicochetActive",ricochet);Set(victim,"currentAction",action);
        Vector3 ownedVelocity=Vector3.left*3f;Set(victim,"velocity",ownedVelocity);
        Begin(false,true);Advance(.05f);
        Assert.That(Position(victim),Is.EqualTo(Vector3.zero));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(victim,"velocity"),Is.EqualTo(ownedVelocity));
        Assert.That(Get(victim,"currentAction"),Is.EqualTo(action));

        Set(victim,"mammothExpulsion",null);Set(victim,"mammothRicochetActive",false);Set(victim,"currentAction","move");Begin(false,true);
        Assert.That(((Vector3)Get(victim,"hitSlideVelocity")).magnitude,Is.GreaterThan(0f));
        Set(victim,"mammothExpulsion",state);Set(victim,"mammothRicochetActive",ricochet);Set(victim,"currentAction",action);Advance(.05f);
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Position(victim),Is.EqualTo(Vector3.zero));
    }

    [TestCase("self")] [TestCase("ally")]
    public void SelfOrFriendlyDamageDoesNotSeedHostileMovement(string reason)
    {
        if(reason=="ally")Set(attacker,"team",Get(victim,"team"));
        Call("BeginHitSlide",reason=="self"?victim:attacker,victim,true,true);
        Advance(.1f);Assert.That(Position(victim),Is.EqualTo(Vector3.zero));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
    }

    [Test] public void AnAlreadyReleasedEnemySkillStillProducesAHitAfterItsCasterDies()
    {
        Set(attacker,"alive",false);Model(attacker).SetActive(false);
        Begin(false,true);Advance(.05f);
        Assert.That(Position(victim).x,Is.GreaterThan(.05f));
    }

    [TestCase("death")] [TestCase("generation")] [TestCase("revision")]
    public void InvalidatedMovementCannotLeakIntoAReviveOrTheNextWave(string reason)
    {
        Begin(false,true);
        if(reason=="death")Set(victim,"alive",false);
        if(reason=="generation")BattleField("combatGeneration",1);
        if(reason=="revision")Set(victim,"positionRevision",1);
        Advance(.1f);Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Set(victim,"alive",true);Advance(.1f);
        Assert.That(Position(victim),Is.EqualTo(Vector3.zero));
        BattleField("battleElapsed",reason=="death"?0.2f:0f);Begin(false,true);Advance(.02f);
        Assert.That(Position(victim).x,Is.GreaterThan(0f),"Redeployment must also reset the prior wave's hit rate limit.");
    }

    [Test] public void PauseAndZeroTimePreserveTheBoundedSlideUntilPlayResumes()
    {
        Begin(false,true);Vector3 velocity=(Vector3)Get(victim,"hitSlideVelocity");
        BattleField("paused",true);Advance(1f);BattleField("paused",false);Advance(0f);Advance(-1f);
        Assert.That(Position(victim),Is.EqualTo(Vector3.zero));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(velocity));
        Advance(.02f);Assert.That(Position(victim).x,Is.GreaterThan(0f));
    }

    [TestCase("body")] [TestCase("wall")] [TestCase("boundary")]
    public void ABlockedHitDoesNotTunnelOrStoreAPushThatReappearsAfterTheObstacleMoves(string obstacle)
    {
        object blocker=null;
        if(obstacle=="body")blocker=Unit(0,Vector3.right*.73f);
        if(obstacle=="wall")Call("RegisterRank3IceWall",attacker,Vector3.right*.08f,Vector3.right,Vector3.forward,5f,10f);
        if(obstacle=="boundary")BattleField("combatBounds",new Bounds(Vector3.zero,new Vector3(.78f,10f,18f)));
        Set(victim,"currentAction","uninterrupted_skill");Set(victim,"velocity",Vector3.forward*3f);
        Begin(true,true);Advance(100f);
        Assert.That(Position(victim).x,Is.InRange(0f,.1301f));
        if(blocker!=null)Assert.That(Vector3.Distance(Position(victim),Position(blocker)),Is.GreaterThanOrEqualTo(.5999f));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(victim,"currentAction"),Is.EqualTo("uninterrupted_skill"));
        Assert.That(Get(victim,"velocity"),Is.EqualTo(Vector3.forward*3f));
        if(blocker!=null)Model(blocker).transform.position=Vector3.right*5f;
        Call("ClearRank3IceWalls");BattleField("combatBounds",new Bounds(Vector3.zero,new Vector3(24f,10f,18f)));
        Vector3 stopped=Position(victim);Advance(.1f);Assert.That(Position(victim),Is.EqualTo(stopped));
    }

    [TestCase(.61f)] [TestCase(.55f)]
    public void ACloseMeleeContactCanSlideAwayInsteadOfDiscardingTheWholeImpulse(float distance)
    {
        Model(attacker).transform.position=Vector3.left*distance;
        float before=(float)Call("BodyOverlapCost",victim,Vector3.zero);
        Assert.That(before,Is.GreaterThan(0f),"Exercise both the spacing buffer and an existing body overlap.");
        Begin(false,true);Advance(1f/120f);
        Assert.That(Position(victim).x,Is.GreaterThan(.02f));
        Assert.That(((Vector3)Get(victim,"hitSlideVelocity")).magnitude,Is.GreaterThan(2.8f));
        Assert.That((float)Call("BodyOverlapCost",victim,Position(victim)),Is.LessThan(before));
        Finish(120);
        Assert.That(Position(victim).x,Is.EqualTo(3f*3f/(2f*9.2f)).Within(.0001f),
            "Resolving an old overlap must not add extra momentum or displacement to the impulse.");
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
    }

    [TestCase(.6f)] [TestCase(.55f)]
    public void AHitIntoAnAlreadyTouchingOrOverlappingNeighbourCannotIncreasePenetrationOrSaveMomentum(float distance)
    {
        object neighbour=Unit(0,Vector3.right*distance);
        float initialDistance=Vector3.Distance(Position(victim),Position(neighbour));
        float previousCost=(float)Call("BodyPenetrationCost",victim,Position(victim));
        Begin(true,true);
        for(int i=0;i<60;i++)
        {
            Advance(1f/120f);
            float cost=(float)Call("BodyPenetrationCost",victim,Position(victim));
            Assert.That(cost,Is.LessThanOrEqualTo(previousCost+.00000001f));
            Assert.That(Vector3.Distance(Position(victim),Position(neighbour)),Is.GreaterThanOrEqualTo(initialDistance-.00001f));
            previousCost=cost;
        }
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Model(neighbour).transform.position=Vector3.right*6f;
        Vector3 stopped=Position(victim);Advance(.1f);
        Assert.That(Position(victim),Is.EqualTo(stopped));
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
    }

    [TestCase(1f,false)] [TestCase(1.3f,false)] [TestCase(2f,false)]
    [TestCase(1f,true)] [TestCase(1.3f,true)] [TestCase(2f,true)]
    public void AHitCanUseAlliesSpacingBufferWithoutLosingItsSlideOrChangingTheAttack(float scale,bool fullUnitLoop)
    {
        Set(victim,"combatBodyScale",scale);Model(victim).transform.localScale=Vector3.one*scale;
        float radius=.3f*scale;
        object above=Unit(0,Vector3.zero),below=Unit(0,Vector3.zero);
        float physical=radius+.3f,clearance=(float)Call("BodyClearance",victim,above,0f);
        Assert.That(clearance,Is.GreaterThan(physical));
        float lateral=(physical+clearance)*.5f;
        float forward=Mathf.Sqrt(clearance*clearance-lateral*lateral)*.5f;
        Model(above).transform.position=new Vector3(forward,0f,lateral);
        Model(below).transform.position=new Vector3(forward,0f,-lateral);
        object[] neighbours={above,below};
        Set(victim,"attackMovementLockTimer",1f);Set(victim,"currentAction","uninterrupted_skill");
        Vector3 aiVelocity=Vector3.forward*2f;Set(victim,"velocity",aiVelocity);
        Assert.That((float)Call("BodyOverlapCost",victim,Position(victim)),Is.GreaterThan(0f));
        Assert.That((float)Call("BodyPenetrationCost",victim,Position(victim)),Is.Zero);
        Begin(false,true);int frames=0;
        while(((Vector3)Get(victim,"hitSlideVelocity")).sqrMagnitude>.000001f&&frames++<60)
        {
            if(fullUnitLoop)Call("UpdateUnit",victim,1f/120f);else Advance(1f/120f);
            foreach(object neighbour in neighbours)
                Assert.That(Vector3.Distance(Position(victim),Position(neighbour)),Is.GreaterThanOrEqualTo(radius+.3f-.0001f));
        }
        Assert.That(Position(victim).x,Is.EqualTo(3f*3f/(2f*9.2f)).Within(.0001f),
            "A placement buffer is not a physical wall, even after the victim grows.");
        Assert.That(Position(victim).z,Is.EqualTo(0f).Within(.0001f));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(victim,"target"),Is.SameAs(attacker));
        Assert.That(Get(victim,"currentAction"),Is.EqualTo(fullUnitLoop?"attack_movement_lock":"uninterrupted_skill"));
        Assert.That(Get(victim,"velocity"),Is.EqualTo(fullUnitLoop?Vector3.zero:aiVelocity));
        Assert.That(Get(victim,"collisionRadius"),Is.EqualTo(.3f));
    }

    [TestCase(1.3f,"body")] [TestCase(2f,"body")]
    [TestCase(1.3f,"wall")] [TestCase(2f,"wall")]
    [TestCase(1.3f,"boundary")] [TestCase(2f,"boundary")]
    public void AScaledBodyStillStopsAtPhysicalBodiesAndWallsAndDiscardsBlockedMomentum(float scale,string obstacle)
    {
        Set(victim,"combatBodyScale",scale);float radius=.3f*scale;object blocker=null;
        if(obstacle=="body")blocker=Unit(0,Vector3.right*(radius+.3f+.12f));
        if(obstacle=="wall")Call("RegisterRank3IceWall",attacker,Vector3.right*.12f,Vector3.right,Vector3.forward,5f,10f);
        if(obstacle=="boundary")BattleField("combatBounds",new Bounds(Vector3.zero,new Vector3((radius+.12f)*2f,10f,18f)));
        Begin(true,true);Advance(100f);
        Assert.That(Position(victim).x,Is.InRange(0f,.1201f));
        if(blocker!=null)
            Assert.That(Vector3.Distance(Position(victim),Position(blocker)),Is.GreaterThanOrEqualTo(radius+.3f-.0001f));
        Assert.That(Get(victim,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        if(blocker!=null)Model(blocker).transform.position=Vector3.right*6f;
        Call("ClearRank3IceWalls");BattleField("combatBounds",new Bounds(Vector3.zero,new Vector3(24f,10f,18f)));
        Vector3 stopped=Position(victim);Advance(.1f);Assert.That(Position(victim),Is.EqualTo(stopped));
    }

    [Test] public void EightVersusEightHitSlidesKeepPhysicalBodiesAndExistingTargets()
    {
        units.Clear();Model(attacker).SetActive(false);Model(victim).SetActive(false);
        var actors=new object[16];
        for(int row=0;row<4;row++)for(int col=0;col<4;col++)
            actors[row*4+col]=Unit(col<2?0:1,new Vector3((col-1.5f)*.75f,0f,(row-1.5f)*.75f));
        for(int i=0;i<16;i++)Set(actors[i],"target",actors[i<8?i+8:i-8]);
        object[] targets=actors.Select(u=>Get(u,"target")).ToArray();
        for(int i=0;i<16;i++)Call("BeginHitSlide",actors[(i+2)%4+i/4*4],actors[i],true,true);
        for(int frame=0;frame<60;frame++)for(int i=0;i<16;i++)
        {
            Call("AdvanceHitSlide",actors[i],1f/120f);
            for(int j=0;j<16;j++)if(i!=j)
                Assert.That(Vector3.Distance(Position(actors[i]),Position(actors[j])),Is.GreaterThanOrEqualTo(.5998f));
            Assert.That(Get(actors[i],"target"),Is.SameAs(targets[i]));
            Assert.That(Get(actors[i],"positionRevision"),Is.EqualTo(0));
        }
        foreach(object actor in actors)Assert.That(Get(actor,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    private void Begin(bool critical,bool skill)=>Call("BeginHitSlide",attacker,victim,critical,skill);
    private SpriteMonsterAnimationDriver CreateDriver()
    {
        var visual=Go("Moving hit victim sprite");visual.transform.SetParent(Model(victim).transform,false);
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);driver.ConfigureHitFrames(3,3);
        Set(victim,"spriteAnimation",driver);return driver;
    }
    private static void StepDriver(SpriteMonsterAnimationDriver driver,float dt)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }
    private void Advance(float dt)=>Call("AdvanceHitSlide",victim,dt);
    private void Finish(int fps){for(int i=0;i<fps;i++)Advance(1f/fps);}
    private GameObject Go(string name){var go=new GameObject(name);objects.Add(go);return go;}
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private Vector3 Position(object unit)=>Model(unit).transform.position;
    private object Unit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);GameObject model=Go("Hit slide actor");model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",Go("Hit slide ring"));Set(unit,"id",units.Count);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"definition",definition);
        Set(unit,"attackRange",1);Set(unit,"collisionRadius",.3f);Set(unit,"skillTimer",100f);
        Set(unit,"skillId","focus_chain");Set(unit,"cooldown",100f);Set(unit,"speedTilesPerSecond",2f);
        Set(unit,"meleeSlotReady",true);Set(unit,"targetLockTimer",10f);Set(unit,"stableId",Guid.NewGuid().ToString("N"));
        units.Add(unit);return unit;
    }
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private void BattleField(string field,object value)=>typeof(MonsterAutoBattlePrototype).GetField(field,Private).SetValue(battle,value);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Private).Invoke(battle,args);
}
