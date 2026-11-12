using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class InertiaCrowdingTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> objects=new List<GameObject>();
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private IList units;
    private MonsterDefinition definition;
    private float previousTimeScale;

    [SetUp] public void Setup()
    {
        previousTimeScale=Time.timeScale;Time.timeScale=1f;
        GameObject host=Go("Inertia crowding test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle);
        BattleField("combatBounds",new Bounds(Vector3.zero,new Vector3(24,10,18)));
        FieldInfo run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        BattleField("mlEpisodeActive",true);BattleField("battleStarted",true);
        definition=Resources.LoadAll<MonsterDefinition>("MonsterData").First(d=>d.monsterId=="velos");
    }

    [TearDown] public void Cleanup()
    {
        foreach(GameObject go in objects)if(go!=null)Object.DestroyImmediate(go);
        objects.Clear();Time.timeScale=previousTimeScale;
    }

    [TestCase(30,0)] [TestCase(60,0)] [TestCase(120,0)]
    [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)]
    public void ASlowGlideBetweenTwoFixedBodiesEscapesInsteadOfCancellingOppositeCorrections(int fps,int blockerTeam)
    {
        object source=Unit(0,Vector3.zero),target=Unit(1,Vector3.forward*1.5f);
        object left=FixedUnit(blockerTeam,Vector3.left*.58f),right=FixedUnit(blockerTeam,Vector3.right*.58f);
        Set(source,"target",target);Set(source,"attackMovementLockTimer",10f);
        Set(source,"velocity",Vector3.right*.4f);Set(source,"moving",true);
        Call("StopForBasicAttack",source);
        Vector3 leftPosition=Position(left),rightPosition=Position(right);
        float dt=1f/fps;
        for(int frame=0;frame<fps*3;frame++)StepBounded(source,dt);
        AssertClear(source,left);AssertClear(source,right);
        Assert.That(Position(left),Is.EqualTo(leftPosition));
        Assert.That(Position(right),Is.EqualTo(rightPosition));
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [Test] public void ResolvingASafeStepCannotCreateANewOverlapWithAnEarlierBody()
    {
        object source=Unit(0,Vector3.zero);
        object first=FixedUnit(0,new Vector3(.15f,0,-.9f));
        object second=FixedUnit(1,new Vector3(.69f,0,.19f));
        AssertClear(source,first);AssertClear(source,second);
        float allyClearance=.6f+.24f;
        Assert.That(Vector3.Distance(Position(source),Position(first)),Is.GreaterThan(allyClearance),
            "The clean step starts outside the wider allied vacancy margin.");
        Vector3 slide=MonsterAutoBattlePrototype.FrictionlessSlide(Vector3.right*.15f,
            (Position(source)-Position(second)).normalized);
        Assert.That(Vector3.Distance(slide,Position(first)),Is.LessThan(allyClearance),
            "Sliding past the second body must still threaten the earlier ally's spacing.");
        Vector3 resolved=(Vector3)Call("ResolveUnitMovement",source,Vector3.zero,Vector3.right*.15f);
        Model(source).transform.position=resolved;
        AssertClear(source,first);AssertClear(source,second);
        Assert.That(Vector3.Distance(Position(source),Position(first)),Is.GreaterThanOrEqualTo(allyClearance-.001f));
        Assert.That(resolved.magnitude,Is.LessThanOrEqualTo(.1501f),
            "A clean movement request must not turn collision correction into a larger step.");
    }

    [TestCase(CombatStatusKind.Freeze)] [TestCase(CombatStatusKind.Knockdown)]
    public void ControlledBodiesStayFixedAndRecoverCrowdingOnlyAfterTheControlEnds(CombatStatusKind control)
    {
        object source=Unit(0,Vector3.zero),target=Unit(1,Vector3.forward*1.5f);
        object left=FixedUnit(0,Vector3.left*.58f),right=FixedUnit(1,Vector3.right*.58f);
        Set(source,"target",target);Set(source,"attackMovementLockTimer",10f);
        Set(source,"velocity",Vector3.right*2f);Set(source,"moving",true);
        Call("StopForBasicAttack",source);
        var status=(CombatStatusState)Get(source,"combatStatuses");status.ApplyTimed(control,10f);
        const float dt=1f/60f;
        for(int frame=0;frame<15;frame++)
        {
            StepBounded(source,dt);
            Assert.That(Position(source),Is.EqualTo(Vector3.zero),"Crowd recovery must not move a controlled actor.");
        }
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        status.Remove(control);
        for(int frame=0;frame<180;frame++)StepBounded(source,dt);
        AssertClear(source,left);AssertClear(source,right);
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Get(source,"moving"),Is.EqualTo(false));
        Assert.That(Get(source,"velocity"),Is.EqualTo(Vector3.zero));
    }

    [Test] public void ACarriedBodyStaysWithItsOwnerAndSeparatesAfterRelease()
    {
        object source=Unit(0,Vector3.zero),target=Unit(1,Vector3.forward*1.5f);
        object owner=FixedUnit(1,Vector3.left*.58f),right=FixedUnit(0,Vector3.right*.58f);
        Set(source,"target",target);Set(source,"attackMovementLockTimer",10f);
        Set(owner,"preyDragTarget",source);Set(source,"preyDraggedBy",owner);
        const float dt=1f/60f;
        for(int frame=0;frame<15;frame++)
        {
            StepBounded(source,dt);
            Assert.That(Position(source),Is.EqualTo(Vector3.zero),"A carry owns its victim's position until release.");
        }
        Set(owner,"preyDragTarget",null);Set(source,"preyDraggedBy",null);
        for(int frame=0;frame<180;frame++)StepBounded(source,dt);
        AssertClear(source,owner);AssertClear(source,right);
        Assert.That(Get(source,"target"),Is.SameAs(target));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void AReleasedEightVersusEightCrowdSeparatesWithoutTeleportingOrDroppingTargets(int fps)
    {
        var actors=new object[16];
        for(int row=0;row<4;row++)for(int col=0;col<4;col++)
            actors[row*4+col]=Unit(col<2?0:1,new Vector3((col-1.5f)*.5f,0,(row-1.5f)*.5f));
        for(int row=0;row<4;row++)for(int col=0;col<4;col++)
        {
            object unit=actors[row*4+col];
            Set(unit,"target",actors[row*4+(col<2?2:1)]);Set(unit,"attackMovementLockTimer",10f);
            ((CombatStatusState)Get(unit,"combatStatuses")).ApplyTimed(CombatStatusKind.Freeze,10f);
        }
        Vector3[] initial=actors.Select(Position).ToArray();
        for(int i=0;i<actors.Length;i++)
        {
            StepBounded(actors[i],1f/fps);
            Assert.That(Position(actors[i]),Is.EqualTo(initial[i]));
            ((CombatStatusState)Get(actors[i],"combatStatuses")).Remove(CombatStatusKind.Freeze);
        }
        object[] targets=actors.Select(unit=>Get(unit,"target")).ToArray();
        for(int frame=0;frame<fps*3;frame++)foreach(object unit in actors)StepBounded(unit,1f/fps);
        for(int i=0;i<actors.Length;i++)
        {
            Assert.That(Get(actors[i],"target"),Is.SameAs(targets[i]));
            Assert.That(Get(actors[i],"basicAttackCount"),Is.EqualTo(0));
            for(int j=i+1;j<actors.Length;j++)AssertClear(actors[i],actors[j]);
        }
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase(false)] [TestCase(true)]
    public void EscapingCrowdingKeepsTheActualAttackOrSkillAnimationAndItsSingleHitCallback(bool skill)
    {
        object source=Unit(0,Vector3.zero),target=Unit(1,Vector3.forward*1.5f);
        object left=FixedUnit(0,Vector3.left*.58f),right=FixedUnit(0,Vector3.right*.58f);
        Set(source,"target",target);Set(source,"attackMovementLockTimer",10f);
        var visual=Go("Crowding sprite");visual.transform.SetParent(Model(source).transform,false);
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);driver.ConfigureHitFrames(3,3);
        Set(source,"spriteAnimation",driver);int hits=0;
        if(skill)driver.PlaySkillAttack(1f,()=>hits++);else driver.PlayBasicAttack(1f,()=>hits++);
        const float dt=1f/60f;
        StepBounded(source,dt);
        Assert.That(driver.CurrentMotionName,Is.EqualTo(skill?"SkillAttack":"Attack"));
        Assert.That(driver.IsActionLocked,Is.True);Assert.That(hits,Is.Zero);
        for(int frame=0;frame<180;frame++)
        {
            StepBounded(source,dt);
            typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
        }
        AssertClear(source,left);AssertClear(source,right);
        Assert.That(hits,Is.EqualTo(1),"Crowd recovery must preserve the pending authored hit.");
        Assert.That(driver.IsActionLocked,Is.False);
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0));
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void ACoastCannotTunnelThroughANarrowBodyGateAndApproachesAgainWhenItOpens(int fps)
    {
        object source=Unit(0,Vector3.zero),target=Unit(1,Vector3.right*6f);
        object[] blockers={FixedUnit(0,new Vector3(1.2f,0,-.4f)),FixedUnit(1,new Vector3(1.2f,0,.4f))};
        Set(source,"target",target);
        Call("BeginSkillInertia",source,Vector3.right*4f,.5f);
        float dt=1f/fps;
        for(int frame=0;frame<fps*2;frame++)
        {
            Vector3 before=Position(source);Call("AdvanceUnitInertia",source,dt);
            Vector3 after=Position(source);
            foreach(object blocker in blockers)
                Assert.That(DistanceToSegment(Position(blocker),before,after),Is.GreaterThanOrEqualTo(.599f),
                    "A free endpoint must not allow residual momentum through a body.");
        }
        foreach(object blocker in blockers)AssertClear(source,blocker);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Model(blockers[0]).transform.position=new Vector3(1.2f,0,-5f);
        Model(blockers[1]).transform.position=new Vector3(1.2f,0,5f);
        Vector3 stopped=Position(source);Call("AdvanceUnitInertia",source,dt);
        Assert.That(Position(source),Is.EqualTo(stopped),"Moving blockers must not release a stored collision impulse.");
        for(int frame=0;frame<fps*5;frame++)
        {
            Call("UpdateUnit",source,dt);
        }
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Call("IsImmediatelyAttackable",source,target),Is.True,
            "Once space is available, the actor must resume approaching instead of staying jammed outside range.");
    }

    [Test] public void RecoveryFindsTheOpenSideOfAnEnemyIceWallWithoutChangingThePendingActionOrVelocity()
    {
        object source=Unit(0,Vector3.zero),target=Unit(1,Vector3.forward*1.5f);
        object left=FixedUnit(0,Vector3.left*.58f),right=FixedUnit(0,Vector3.right*.58f);
        Set(source,"target",target);Set(source,"currentAction","pending_hit_recovery");
        Vector3 velocity=new Vector3(2f,0,-3f);Set(source,"velocity",velocity);
        Call("RegisterRank3IceWall",target,Vector3.forward*.02f,Vector3.forward,Vector3.right,5f,10f);
        const float dt=1f/60f;
        for(int frame=0;frame<180;frame++)
        {
            Vector3 before=Position(source);float speed=(float)Call("CurrentMovementSpeed",source);
            Call("SeparateRestingUnit",source,dt);
            Assert.That(Position(source).z,Is.LessThanOrEqualTo(.020001f),
                "The recovery candidate must stay on its original side of the enemy wall.");
            Assert.That(Vector3.Distance(before,Position(source)),Is.LessThanOrEqualTo(speed*dt+.001f));
            Assert.That(Get(source,"velocity"),Is.EqualTo(velocity),
                "Testing a rejected candidate must not absorb the actor's unrelated AI velocity.");
            Assert.That(Get(source,"currentAction"),Is.EqualTo("pending_hit_recovery"),
                "Testing a rejected candidate must not replace the pending combat action.");
        }
        AssertClear(source,left);AssertClear(source,right);
        Assert.That(Position(source).z,Is.LessThan(-.1f),"The opposite side remains open for a gradual escape.");
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Get(source,"targetLockTimer"),Is.EqualTo(10f));
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
    [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
    public void AttackStopGlidingCannotCollapseTheSpacingAroundAnAlly(int fps,bool reverse)
    {
        int team=reverse?1:0;
        object source=Unit(team,Vector3.left*1.6f),ally=FixedUnit(team,Vector3.zero);
        object target=Unit(1-team,new Vector3(-1.6f,0f,1.8f));Set(source,"target",target);
        Set(source,"velocity",Vector3.right*3.4f);Set(source,"moving",true);
        Call("StopForBasicAttack",source);
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).sqrMagnitude,Is.GreaterThan(0f));
        Vector3 start=Position(source),fixedPosition=Position(ally);
        float clearance=.6f+.24f;
        for(int frame=0;frame<fps*2;frame++)
        {
            Call("AdvanceUnitInertia",source,1f/fps);
            Assert.That(Vector3.Distance(Position(source),Position(ally)),Is.GreaterThanOrEqualTo(clearance-.001f));
        }
        Assert.That(Position(source).x,Is.GreaterThan(start.x),"The approach must still retain a real residual glide.");
        Assert.That(Position(ally),Is.EqualTo(fixedPosition));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(source,"target"),Is.SameAs(target));
    }

    [TestCase(false,false)] [TestCase(true,false)]
    [TestCase(false,true)] [TestCase(true,true)]
    public void RestingAllySpacingRecoversWithoutLosingThePendingHitDamageOrTarget(bool skill,bool reverse)
    {
        int team=reverse?1:0;
        object source=Unit(team,Vector3.zero),target=Unit(1-team,Vector3.forward*1.9f);
        object left=FixedUnit(team,Vector3.left*.75f),right=FixedUnit(team,Vector3.right*.75f);
        Assert.That((float)Call("BodyOverlapCost",source,Position(source)),Is.GreaterThan(0f),
            "The pending hit must run while the smaller allied placement margin still needs recovery.");
        Assert.That((float)Call("BodyPenetrationCost",source,Position(source)),Is.Zero,
            "Placement recovery must start outside both physical bodies.");
        Set(source,"target",target);Set(source,"attackMovementLockTimer",10f);
        var visual=Go("Ally spacing pending hit");visual.transform.SetParent(Model(source).transform,false);
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);driver.ConfigureHitFrames(3,3);
        Set(source,"spriteAnimation",driver);int hits=0;float initialHp=(float)Get(target,"hp");
        Action impact=()=>
        {
            hits++;
            Call("ApplyDamage",source,target,40f,skill,!skill,false,false,0f,0f,1f,false,false,false);
        };
        if(skill)driver.PlaySkillAttack(1f,impact);else driver.PlayBasicAttack(1f,impact);
        for(int frame=0;frame<180;frame++)
        {
            StepBounded(source,1f/60f);
            typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{1f/60f});
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
        }
        float clearance=.6f+.24f;
        foreach(object ally in new[]{left,right})
            Assert.That(Vector3.Distance(Position(source),Position(ally)),Is.GreaterThanOrEqualTo(clearance-.001f));
        Assert.That(hits,Is.EqualTo(1));Assert.That((float)Get(target,"hp"),Is.EqualTo(initialHp-40f).Within(.001f));
        Assert.That(Get(source,"target"),Is.SameAs(target));Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0));
        Assert.That(Position(left),Is.EqualTo(Vector3.left*.75f));Assert.That(Position(right),Is.EqualTo(Vector3.right*.75f));
        Assert.That(Call("IsImmediatelyAttackable",source,target),Is.True);
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase(false)] [TestCase(true)]
    public void RecoveringAllySpacingCannotDeepenActualBodyPenetration(bool reverse)
    {
        int team=reverse?1:0;
        object source=Unit(team,Vector3.zero),ally=FixedUnit(team,Vector3.left*.75f);
        object target=FixedUnit(1-team,Vector3.right*.59f);Set(source,"target",target);
        float cost=(float)Call("BodyPenetrationCost",source,Position(source));
        Vector3 original=Position(source);
        Call("SeparateRestingUnit",source,.05f);
        Assert.That((float)Call("BodyPenetrationCost",source,Position(source)),Is.LessThanOrEqualTo(cost+.00000001f));
        Assert.That(Position(source),Is.Not.EqualTo(original),"A clear sideways recovery must remain available.");
        Assert.That(Position(ally),Is.EqualTo(Vector3.left*.75f));
        Assert.That(Position(target),Is.EqualTo(Vector3.right*.59f));
    }

    private static float DistanceToSegment(Vector3 point,Vector3 from,Vector3 to)
    {
        Vector3 delta=to-from;
        return Vector3.Distance(point,from+delta*(delta.sqrMagnitude>.000001f?
            Mathf.Clamp01(Vector3.Dot(point-from,delta)/delta.sqrMagnitude):0f));
    }

    private void StepBounded(object unit,float dt)
    {
        Vector3 before=Position(unit);
        float speed=(float)Call("CurrentMovementSpeed",unit);
        Call("UpdateUnit",unit,dt);
        Assert.That(Vector3.Distance(before,Position(unit)),Is.LessThanOrEqualTo(speed*dt+.001f),
            "Crowd recovery must move gradually rather than teleport a grounded body.");
    }
    private void AssertClear(object a,object b)
    {
        float clearance=(float)Get(a,"collisionRadius")+(float)Get(b,"collisionRadius")+.025f;
        Assert.That(Vector3.Distance(Position(a),Position(b)),Is.GreaterThanOrEqualTo(clearance-.001f),
            "A grounded body must recover space outside both neighbours, not just the last one visited.");
    }
    private object FixedUnit(int team,Vector3 position)
    {
        object unit=Unit(team,position);
        ((CombatStatusState)Get(unit,"combatStatuses")).ApplyTimed(CombatStatusKind.Freeze,20f);
        return unit;
    }
    private object Unit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);GameObject model=Go("Crowding unit");
        model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",Go("Ring"));Set(unit,"id",units.Count);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"attackRange",1);Set(unit,"collisionRadius",.3f);Set(unit,"skillTimer",100f);
        Set(unit,"skillId","focus_chain");Set(unit,"cooldown",100f);Set(unit,"speedTilesPerSecond",2f);
        Set(unit,"definition",definition);Set(unit,"meleeSlotReady",true);Set(unit,"targetLockTimer",10f);
        units.Add(unit);return unit;
    }
    private GameObject Go(string name){GameObject go=new GameObject(name);objects.Add(go);return go;}
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private Vector3 Position(object unit)=>Model(unit).transform.position;
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private void BattleField(string field,object value)=>typeof(MonsterAutoBattlePrototype).GetField(field,Private).SetValue(battle,value);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Private).Invoke(battle,args);
}
