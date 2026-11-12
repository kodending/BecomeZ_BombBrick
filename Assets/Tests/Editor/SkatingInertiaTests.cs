using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BombBrick.Combat;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SkatingInertiaTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private IList units;
    private object source,target;
    private MonsterDefinition naturalMeleeDefinition;
    private readonly List<GameObject> objects=new List<GameObject>();

    [SetUp] public void Setup()
    {
        GameObject host=Go("Skating inertia test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle);
        BattleField("combatBounds",new Bounds(Vector3.zero,new Vector3(24,10,18)));
        FieldInfo run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        BattleField("mlEpisodeActive",true);
        BattleField("battleStarted",true);
        source=Unit(0,Vector3.zero);
        target=Unit(1,Vector3.forward*1.5f);
        Set(source,"target",target);
    }

    [TearDown] public void Cleanup()
    {
        foreach(GameObject go in objects)if(go!=null)Object.DestroyImmediate(go);
        objects.Clear();
        if(naturalMeleeDefinition!=null)Object.DestroyImmediate(naturalMeleeDefinition);
        naturalMeleeDefinition=null;
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void OrdinaryAttackStopSlidesTheActualBodyWhileTheAttackRemainsLocked(int fps)
    {
        BeginOrdinary(4f);Set(source,"attackMovementLockTimer",1f);
        Advance(1f/fps);
        Assert.That(Position(source).x,Is.GreaterThan(.02f),"The ground position, rather than only a child sprite, must continue moving.");
        Assert.That((bool)Get(source,"moving"),Is.False,"The AI movement flag must not reseed momentum on every attack stop.");
        Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(1f));
        Finish(fps);
        Assert.That(Position(source).x,Is.GreaterThan(.3f));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.LessThan(MonsterAutoBattlePrototype.AttackDistanceForRange(1,.3f,.3f)));
        Vector3 settled=Position(source);Advance(1f/fps);
        Assert.That(Position(source),Is.EqualTo(settled));
    }

    [Test]
    public void ARepeatedAttackStopDoesNotEraseOrRestartTheRemainingGlide()
    {
        BeginOrdinary(4f);Advance(.1f);
        Vector3 remaining=(Vector3)Get(source,"inertiaVelocity");
        Call("StopForBasicAttack",source);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(remaining));
        Advance(.1f);
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,Is.LessThan(remaining.magnitude));
    }

    [Test]
    public void TheNormalAiUpdateKeepsGlidingThroughAttackMovementLock()
    {
        BeginOrdinary(4f);Set(source,"attackMovementLockTimer",1f);
        for(int i=0;i<6;i++)Call("UpdateUnit",source,1f/60f);
        Assert.That(Position(source).x,Is.GreaterThan(.2f));
        Assert.That((float)Get(source,"attackMovementLockTimer"),Is.InRange(.89f,.91f));
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0));
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,Is.GreaterThan(1f));
    }

    [TestCase(30,1f)] [TestCase(60,1f)] [TestCase(120,1f)]
    [TestCase(30,2f)] [TestCase(60,2f)] [TestCase(120,2f)]
    [TestCase(30,3f)] [TestCase(60,3f)] [TestCase(120,3f)]
    public void NaturalMutualApproachStartsAttackingWithMomentumAndGlidesWithinCombatSpacing(int fps,float speedTiles)
    {
        naturalMeleeDefinition=ScriptableObject.CreateInstance<MonsterDefinition>();
        naturalMeleeDefinition.monsterId="inertia_melee_fixture";
        naturalMeleeDefinition.familyId="melee_fixture";
        naturalMeleeDefinition.attackInterval=1f;
        Model(source).transform.position=Vector3.left*6f;
        Model(target).transform.position=Vector3.right*6f;
        Set(target,"target",source);
        var actors=new[]{source,target};
        foreach(object unit in actors)
        {
            Set(unit,"definition",naturalMeleeDefinition);
            Set(unit,"speedTilesPerSecond",speedTiles);Set(unit,"cooldown",0f);
            Set(unit,"damage",20f);Set(unit,"attackInterval",1f);
        }
        var driver=Driver();
        float dt=1f/fps,approachSpeed=0f,coastSeed=0f,attackEntrySpeed=0f,glideDistance=0f,glideDuration=0f;
        bool coastStarted=false,coastFinished=false,attackStartedWithMomentum=false,sourceImpactDispatched=false;
        var random=UnityEngine.Random.state;
        try
        {
            for(int frame=0;frame<fps*8;frame++)
            {
                Vector3 previous=Position(source);
                foreach(object unit in actors)
                {
                    object opponent=Get(unit,"target");int attacks=(int)Get(unit,"basicAttackCount");
                    float distance=Vector3.Distance(Position(unit),Position(opponent));
                    float entrySpeed=((Vector3)Get(unit,"velocity")).magnitude;
                    Call("UpdateUnit",unit,dt);
                    if((int)Get(unit,"basicAttackCount")>attacks)
                    {
                        Assert.That(distance,Is.LessThanOrEqualTo((float)Call("MeleeArrivalDistance",unit,opponent)+.026f));
                        Assert.That(Call("IsImmediatelyAttackable",unit,opponent),Is.True);
                        if(unit==source&&attacks==0)
                        {
                            attackEntrySpeed=entrySpeed;
                            attackStartedWithMomentum=((Vector3)Get(source,"inertiaVelocity")).sqrMagnitude>.000001f;
                        }
                    }
                }
                bool pendingImpact=driver.HasPendingBasicAttackHit;
                float targetHp=(float)Get(target,"hp");
                StepDriver(driver,dt);
                if(pendingImpact&&!driver.HasPendingBasicAttackHit&&(float)Get(target,"hp")<targetHp)
                    sourceImpactDispatched=true;
                float coastSpeed=((Vector3)Get(source,"inertiaVelocity")).magnitude;
                if(!coastStarted)
                {
                    if(coastSpeed>.001f)
                    {
                        coastStarted=true;coastSeed=coastSpeed;
                        glideDistance=Vector3.Distance(previous,Position(source));glideDuration=dt;
                    }
                    else approachSpeed=Mathf.Max(approachSpeed,((Vector3)Get(source,"velocity")).magnitude);
                }
                else if(!coastFinished)
                {
                    glideDistance+=Vector3.Distance(previous,Position(source));glideDuration+=dt;
                    if(coastSpeed<=.001f)coastFinished=true;
                }
                Assert.That(Vector3.Distance(Position(source),Position(target)),Is.GreaterThanOrEqualTo(.599f));
                if(coastFinished&&(int)Get(source,"basicAttackCount")>0&&(int)Get(target,"basicAttackCount")>0&&
                   (float)Get(source,"hp")<5000f&&(float)Get(target,"hp")<5000f)break;
            }
        }
        finally{UnityEngine.Random.state=random;}
        Assert.That(approachSpeed,Is.GreaterThan((float)Call("CurrentMovementSpeed",source)*.8f));
        Assert.That(coastStarted,Is.True,"The ordinary approach must create inertia without a manually seeded stop.");
        Assert.That(attackEntrySpeed,Is.GreaterThanOrEqualTo(approachSpeed*.95f),"Walking speed must reach the attack transition before arrival braking begins.");
        Assert.That(coastSeed,Is.GreaterThan(.01f));
        Assert.That(attackStartedWithMomentum,Is.True,"The first attack must begin before the visible arrival glide has finished.");
        Assert.That(glideDistance,Is.GreaterThan(.05f));
        Assert.That(glideDuration,Is.InRange(.45f,.65f+dt),"Arrival should retain visible residual movement after its first attack.");
        Assert.That(coastFinished,Is.True);
        Assert.That(sourceImpactDispatched,Is.True,"The source's real animation callback must apply basic damage to the target.");
        foreach(object unit in actors)
        {
            Assert.That(Get(unit,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
            Assert.That(((Vector3)Get(unit,"velocity")).magnitude,Is.LessThanOrEqualTo(.4f));
            object opponent=Get(unit,"target");float distance=Vector3.Distance(Position(unit),Position(opponent));
            Assert.That(distance,Is.GreaterThanOrEqualTo((float)Call("MeleeContactDistance",unit,opponent)-.002f));
            Assert.That(distance,Is.LessThanOrEqualTo((float)Call("MeleeArrivalDistance",unit,opponent)+.002f));
            Assert.That(Get(unit,"basicAttackCount"),Is.GreaterThan(0));
            Assert.That((float)Get(unit,"hp"),Is.LessThan(5000f),unit==source
                ?"Source HP must decrease from the target's immediate basic attack."
                :"Target HP must decrease from the source driver's basic-hit callback.");
        }
    }

    [Test]
    public void GlidingKeepsRunFramesAdvancingAndFacesTheTargetRatherThanTheCarriedVelocity()
    {
        var driver=Driver();
        var camera=Go("Glide facing camera").AddComponent<Camera>();
        var billboard=driver.gameObject.AddComponent<SpriteMonsterBillboard>();
        typeof(SpriteMonsterBillboard).GetMethod("Awake",Private).Invoke(billboard,null);
        typeof(SpriteMonsterBillboard).GetField("cachedCamera",Private).SetValue(billboard,camera);
        Set(source,"spriteBillboard",billboard);
        Model(target).transform.position=Vector3.left*1.5f;
        BeginOrdinary(4f);Set(source,"attackMovementLockTimer",1f);
        Call("UpdateUnit",source,.15f);StepDriver(driver,.15f);
        Assert.That(Position(source).x,Is.GreaterThan(0f),"The body still carries its original rightward momentum.");
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Run"));
        int firstFrame=driver.CurrentFrameIndex;
        Assert.That(firstFrame,Is.GreaterThan(0));
        Call("UpdateUnit",source,.15f);StepDriver(driver,.15f);
        Assert.That(driver.CurrentFrameIndex,Is.GreaterThan(firstFrame),"Repeated coast updates must not restart the run clip.");
        Assert.That((Vector3)Get(source,"facingDirection"),Is.EqualTo(Vector3.left));
        Assert.That(driver.GetComponent<SpriteRenderer>().flipX,Is.True,"The visible sprite must face its target on the left.");
        Assert.That((bool)Get(source,"moving"),Is.False);
        Finish(60);
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
    }

    [Test]
    public void HardStopImmediatelyClearsTheVisibleRunLoopAndStoredMomentum()
    {
        var driver=Driver();BeginOrdinary(4f);
        Advance(.1f);StepDriver(driver,.1f);
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Run"));
        Vector3 before=Position(source);
        Call("StopUnitMovement",source);
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Advance(.1f);
        Assert.That(Position(source),Is.EqualTo(before));
    }

    [TestCase(false)] [TestCase(true)]
    public void GlidingPreservesAttackHitsAndResumesRunWhenTheOneShotFinishes(bool skill)
    {
        var driver=Driver();int hits=0;
        BeginOrdinary(6f);
        if(skill)driver.PlaySkillAttack(1f,()=>hits++);
        else driver.PlayBasicAttack(1f,()=>hits++);
        Advance(.05f);StepDriver(driver,.05f);
        Assert.That(driver.CurrentMotionName,Is.EqualTo(skill?"SkillAttack":"Attack"));
        Assert.That(driver.IsActionLocked,Is.True);
        Assert.That(hits,Is.Zero);
        StepDriver(driver,.52f);
        Assert.That(hits,Is.EqualTo(1),"Coast locomotion must preserve the original one-shot hit callback.");
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Run"));
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).sqrMagnitude,Is.GreaterThan(0f));
        Finish(60);
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
    }

    [Test]
    public void FasterMovementSkillLeavesARealLongerGlideAndHasABoundedSeedSpeed()
    {
        float slowSeed=4f*CombatStatFormula.CombatAnimationRate;
        float maximum=CombatMovementInertia.MaximumCoastSpeed*CombatStatFormula.CombatAnimationRate;
        Call("BeginSkillInertia",source,Vector3.right*2f,.5f);
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,Is.EqualTo(slowSeed).Within(.0001f));
        Finish(60);float slow=Position(source).x;
        Model(source).transform.position=Vector3.zero;
        Call("BeginSkillInertia",source,Vector3.right*4f,.5f);
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,Is.EqualTo(maximum).Within(.0001f));
        Finish(60);float fast=Position(source).x;
        Assert.That(slow,Is.GreaterThan(.3f));
        Assert.That(slow,Is.EqualTo(slowSeed*slowSeed/(2f*CombatMovementInertia.CoastDecelerationForSpeed(slowSeed))).Within(.0001f));
        Assert.That(fast,Is.EqualTo(maximum*maximum/(2f*CombatMovementInertia.CoastDecelerationForSpeed(maximum))).Within(.0001f));
        Assert.That(fast/slow,Is.InRange(1.9f,2.1f),"Fast skill momentum stays stronger while the slower glide is easier to see.");
        Call("BeginSkillInertia",source,new Vector3(100f,100f,0f),.01f);
        Vector3 velocity=(Vector3)Get(source,"inertiaVelocity");
        Assert.That(velocity.y,Is.Zero);
        Assert.That(velocity.magnitude,Is.EqualTo(maximum).Within(.0001f));
        Call("ClearUnitInertia",source);
        Call("BeginSkillInertia",source,Vector3.up*10f,.2f);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)] [TestCase(1)]
    public void SlowAttackStopMovesFartherWithoutChangingItsInitialSpeedOrRestartingTheAction(int fps)
    {
        BeginOrdinary(2f);Set(source,"attackMovementLockTimer",1f);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.right*2f));
        Finish(fps);
        Assert.That(Position(source).x,Is.InRange(.5651f,.5653f));
        Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(1f));
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(source,"inertiaDeceleration"),Is.EqualTo(0f));
    }

    [Test]
    public void SubMillimeterCoastStepsAtHighFrameRatesDoNotDiscardRemainingMomentum()
    {
        BeginOrdinary(.1f);
        Advance(1f/120f);
        Assert.That(Position(source).x,Is.InRange(.0006f,.0008f));
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).x,Is.InRange(.0704f,.0706f),
            "A submillimeter frame displacement is still valid motion, not a blocked collision.");
        Finish(120);
        Assert.That(Position(source).x,Is.EqualTo(.1f*.1f/(2f*CombatMovementInertia.CoastDecelerationForSpeed(.1f))).Within(.00001f));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void FastDashKeepsItsOriginalDragWhenItsRemainingVelocityBecomesSlow()
    {
        BeginOrdinary(6f);
        float initial=((Vector3)Get(source,"inertiaVelocity")).magnitude;
        Advance(.5f);
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,Is.LessThan(2.5f));
        float drag=CombatMovementInertia.CoastDecelerationForSpeed(initial);
        Assert.That(Get(source,"inertiaDeceleration"),Is.EqualTo(drag));
        Finish(120);
        Assert.That(Position(source).x,Is.EqualTo(initial*initial/(2f*drag)).Within(.0001f));
        Assert.That(Get(source,"inertiaDeceleration"),Is.EqualTo(0f));
        Model(source).transform.position=Vector3.zero;
        BeginOrdinary(2f);
        Assert.That(Get(source,"inertiaDeceleration"),Is.EqualTo(CombatMovementInertia.CoastDecelerationForSpeed(2f)));
        Call("ClearUnitInertia",source);
        Assert.That(Get(source,"inertiaDeceleration"),Is.EqualTo(0f));
    }

    [Test]
    public void BodyCollisionStopsCoastWithoutOverlapOrAStoredPush()
    {
        object blocker=Unit(0,Vector3.right*.7f);
        BeginOrdinary(4f);Finish(120);
        Assert.That(Position(source).x,Is.LessThan(.2f));
        Assert.That(Vector3.Distance(Position(source),Position(blocker)),Is.GreaterThanOrEqualTo(.6f));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Model(blocker).transform.position=Vector3.right*5f;
        Vector3 stopped=Position(source);Advance(.1f);
        Assert.That(Position(source),Is.EqualTo(stopped),"Removing a blocker must not release an old hidden impulse.");
    }

    [TestCase(1f)] [TestCase(100f)]
    public void OneLongFrameCannotCarryASkillGlideThroughAnEntireBody(float dt)
    {
        object blocker=Unit(0,Vector3.right*1.6f);
        Call("BeginSkillInertia",source,Vector3.right*4f,.5f);
        Advance(dt);
        Assert.That(Position(source).x,Is.InRange(0f,1f),
            "A free endpoint beyond a monster does not make the travelled segment passable.");
        Assert.That(Vector3.Distance(Position(source),Position(blocker)),Is.GreaterThanOrEqualTo(.6f));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void ArenaBoundaryStopsCoastWithoutLeavingThePlayableArea()
    {
        BattleField("combatBounds",new Bounds(Vector3.zero,new Vector3(1.4f,10,18)));
        BeginOrdinary(8f);Finish(120);
        Assert.That(Position(source).x,Is.InRange(0f,.40001f));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [TestCase(CombatStatusKind.Freeze)] [TestCase(CombatStatusKind.Root)]
    [TestCase(CombatStatusKind.Knockdown)] [TestCase(CombatStatusKind.Airborne)]
    [TestCase(CombatStatusKind.Fear)]
    public void HardControlCancelsMomentumAndDoesNotResumeItWhenRemoved(CombatStatusKind status)
    {
        BeginOrdinary(4f);
        CombatStatusState statuses=(CombatStatusState)Get(source,"combatStatuses");
        statuses.ApplyTimed(status,1f);Advance(.02f);
        Assert.That(Position(source),Is.EqualTo(Vector3.zero));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        statuses.Remove(status);Advance(.02f);
        Assert.That(Position(source),Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void ReleasingAStackedControlSeparatesRestingBodiesSmoothlyWithoutRestartingMovement()
    {
        object ally=Unit(0,Vector3.right*.1f);Set(ally,"target",target);
        foreach(object unit in new[]{source,ally})
        {
            Set(unit,"attackMovementLockTimer",1f);
            ((CombatStatusState)Get(unit,"combatStatuses")).ApplyTimed(CombatStatusKind.Freeze,2f);
        }
        Vector3 initialSource=Position(source),initialAlly=Position(ally);
        Call("UpdateUnit",source,.05f);Call("UpdateUnit",ally,.05f);
        Assert.That(Position(source),Is.EqualTo(initialSource));
        Assert.That(Position(ally),Is.EqualTo(initialAlly),"A frozen or held body must retain its authored position.");
        foreach(object unit in new[]{source,ally})
            ((CombatStatusState)Get(unit,"combatStatuses")).Remove(CombatStatusKind.Freeze);

        const float dt=1f/60f;
        for(int frame=0;frame<30;frame++)
        {
            foreach(object unit in new[]{source,ally})
            {
                Vector3 previous=Position(unit);
                float speed=(float)Call("CurrentMovementSpeed",unit);
                Call("UpdateUnit",unit,dt);
                Assert.That(Vector3.Distance(previous,Position(unit)),Is.LessThanOrEqualTo(speed*dt+.0001f));
                Assert.That(Get(unit,"velocity"),Is.EqualTo(Vector3.zero));
                Assert.That(Get(unit,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
                Assert.That((bool)Get(unit,"moving"),Is.False);
            }
        }
        float clearance=(float)typeof(MonsterAutoBattlePrototype).GetField("UnitMovementClearance",
            BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue();
        Assert.That(Vector3.Distance(Position(source),Position(ally)),Is.GreaterThanOrEqualTo(.6f+clearance-.001f));
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0));
        Assert.That(Get(ally,"basicAttackCount"),Is.EqualTo(0));
    }

    [TestCase("death")] [TestCase("inactive")] [TestCase("revision")]
    [TestCase("generation")] [TestCase("stun")] [TestCase("displacement")]
    [TestCase("hard-stop")]
    public void InvalidatedCoastCannotMoveOrLeakIntoTheNextCombatAction(string reason)
    {
        BeginOrdinary(4f);
        if(reason=="death")Set(source,"alive",false);
        if(reason=="inactive")Model(source).SetActive(false);
        if(reason=="revision")Set(source,"positionRevision",1);
        if(reason=="generation")BattleField("combatGeneration",1);
        if(reason=="stun")Set(source,"giraffeStunTimer",1f);
        if(reason=="displacement")Set(source,"displacing",true);
        if(reason=="hard-stop")Call("StopUnitMovement",source);
        Advance(.02f);
        Assert.That(Position(source),Is.EqualTo(Vector3.zero));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Set(source,"alive",true);Model(source).SetActive(true);
        Set(source,"giraffeStunTimer",0f);Set(source,"displacing",false);
        Advance(.02f);Assert.That(Position(source),Is.EqualTo(Vector3.zero));
    }

    [TestCase(0f)] [TestCase(-.1f)] [TestCase(.02f)]
    public void PausedOrZeroTimeCoastKeepsItsMomentumUntilPlayResumes(float dt)
    {
        BeginOrdinary(4f);
        if(dt>0f)BattleField("paused",true);
        Vector3 initial=(Vector3)Get(source,"inertiaVelocity");Advance(dt);
        Assert.That(Position(source),Is.EqualTo(Vector3.zero));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(initial));
        BattleField("paused",false);Advance(.02f);
        Assert.That(Position(source).x,Is.GreaterThan(0f));
    }

    private GameObject Go(string name){GameObject go=new GameObject(name);objects.Add(go);return go;}
    private object Unit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);
        GameObject model=Go("Skating unit");model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",Go("Ring"));Set(unit,"id",units.Count);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"attackRange",1);Set(unit,"collisionRadius",.3f);Set(unit,"skillTimer",100f);
        Set(unit,"skillId","focus_chain");Set(unit,"cooldown",100f);Set(unit,"speedTilesPerSecond",2f);
        Set(unit,"definition",Resources.LoadAll<MonsterDefinition>("MonsterData").First(d=>d.monsterId=="velos"));
        Set(unit,"meleeSlotReady",true);Set(unit,"targetLockTimer",2.4f);
        units.Add(unit);return unit;
    }
    private void BeginOrdinary(float speed)
    {Set(source,"velocity",Vector3.right*speed);Set(source,"moving",true);Call("StopForBasicAttack",source);}
    private SpriteMonsterAnimationDriver Driver()
    {
        var visual=Go("Skating sprite");visual.transform.SetParent(Model(source).transform,false);
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        driver.ConfigureHitFrames(3,3);Set(source,"spriteAnimation",driver);
        return driver;
    }
    private static void StepDriver(SpriteMonsterAnimationDriver driver,float dt)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }
    private void Advance(float dt)=>Call("AdvanceUnitInertia",source,dt);
    private void Finish(int fps){for(int i=0;i<fps*2;i++)Advance(1f/fps);}
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private Vector3 Position(object unit)=>Model(unit).transform.position;
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private void BattleField(string field,object value)=>typeof(MonsterAutoBattlePrototype).GetField(field,Private).SetValue(battle,value);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype)
        .GetMethod(method,BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic).Invoke(battle,args);
}
