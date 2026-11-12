using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Combat;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SkillInertiaCompletionTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> objects=new List<GameObject>();
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private object source,target;

    [SetUp] public void Setup()
    {
        GameObject host=Go("Skill inertia completion test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        BattleField("combatBounds",new Bounds(Vector3.zero,Vector3.one*100f));
        BattleField("mlEpisodeActive",true);
        FieldInfo run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        source=Unit(Vector3.right*2f);target=Unit(Vector3.right*20f);
    }

    [TearDown] public void Cleanup()
    {
        foreach(GameObject go in objects)if(go!=null)Object.DestroyImmediate(go);
        objects.Clear();
    }

    [TestCase("mammoth")] [TestCase("wall-rush")]
    [TestCase("griffin")] [TestCase("mantis")] [TestCase("beetle")]
    public void NaturalPhysicalSkillCompletionSeedsAndMovesTheActualGroundBody(string family)
    {
        object state=Prepare(family);
        Complete(family,state);
        Vector3 velocity=(Vector3)Get(source,"inertiaVelocity");
        Assert.That(velocity.x,Is.GreaterThan(0f).And.LessThanOrEqualTo(
            CombatMovementInertia.MaximumCoastSpeed*CombatStatFormula.CombatAnimationRate+.0001f));
        Assert.That(velocity.y,Is.Zero);
        Vector3 before=Model(source).transform.position;
        Call("AdvanceUnitInertia",source,.1f);
        Assert.That(Model(source).transform.position.x,Is.GreaterThan(before.x));
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,Is.LessThan(velocity.magnitude));
        Assert.That(Get(source,"moving"),Is.EqualTo(false));
    }

    [TestCase("mammoth")] [TestCase("wall-rush")]
    [TestCase("griffin")] [TestCase("mantis")] [TestCase("beetle")]
    public void CanceledPhysicalSkillCannotProduceANewSlide(string family)
    {
        object state=Prepare(family);
        Complete(family,state,false);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [TestCase("mammoth",false)] [TestCase("wall-rush",false)]
    [TestCase("griffin",false)] [TestCase("mantis",false)] [TestCase("beetle",false)]
    [TestCase("mammoth",true)] [TestCase("wall-rush",true)]
    [TestCase("griffin",true)] [TestCase("mantis",true)] [TestCase("beetle",true)]
    public void ANewPositionRevisionOrCombatGenerationInvalidatesNaturalCompletion(string family,bool generation)
    {
        object state=Prepare(family);
        if(generation)BattleField("combatGeneration",1);else Set(source,"positionRevision",1);
        Complete(family,state);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [TestCase("mammoth")] [TestCase("wall-rush")]
    [TestCase("griffin")] [TestCase("mantis")] [TestCase("beetle")]
    public void FreezeAtCompletionCannotCreateMomentumThatResumesAfterThaw(string family)
    {
        object state=Prepare(family);
        var statuses=(CombatStatusState)Get(source,"combatStatuses");
        statuses.ApplyTimed(CombatStatusKind.Freeze,1f);
        Complete(family,state);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        statuses.Remove(CombatStatusKind.Freeze);
        Vector3 before=Model(source).transform.position;
        Call("AdvanceUnitInertia",source,.1f);
        Assert.That(Model(source).transform.position,Is.EqualTo(before));
    }

    [TestCase("mammoth")] [TestCase("wall-rush")]
    [TestCase("griffin")] [TestCase("mantis")] [TestCase("beetle")]
    public void VerticalOnlySkillMovementDoesNotCreatePlanarMomentum(string family)
    {
        Model(source).transform.position=Vector3.up*3f;
        object state=Prepare(family);
        Complete(family,state);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [Test] public void AnObstructedWallRushDoesNotSeedACoastEvenAtItsNormalEndTime()
    {
        object state=Prepare("wall-rush");
        BattleField("combatBounds",new Bounds(Vector3.zero,new Vector3(4.6f,10f,100f)));
        Complete("wall-rush",state);
        Assert.That(state.GetType().GetField("blocked").GetValue(state),Is.EqualTo(true));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [TestCase("mammoth")] [TestCase("wall-rush")] [TestCase("griffin")]
    public void LandingCoastStartsBeforeRecoveryAndRecoveryCannotSnapBackOrRestartIt(string family)
    {
        object state=Prepare(family);Complete(family,state);
        Vector3 landed=Model(source).transform.position;
        Call("AdvanceUnitInertia",source,.1f);
        Vector3 coasting=Model(source).transform.position;
        Vector3 remaining=(Vector3)Get(source,"inertiaVelocity");
        Assert.That(coasting.x,Is.GreaterThan(landed.x),"Coast must already be visible during recovery.");
        if(family=="griffin")Call("UpdateGriffinHunt",source,GriffinHuntRules.End-GriffinHuntRules.Landing);
        else
        {
            Call("AdvanceMammothCharge",source,state,
                family=="wall-rush"?MammothRank3Rules.WallRushEnd:MammothCombatRules.EndTime);
            Call("EndMammothCharge",source,state);
        }
        if(family=="griffin")Call("EndGriffinHunt",source);else Call("EndMammothCharge",source,state);
        Assert.That(Model(source).transform.position,Is.EqualTo(coasting),"The recovery pose must not restore the old landing position.");
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(remaining),"Finishing recovery must not reapply the original impulse.");
    }

    [TestCase(0)] [TestCase(1)]
    public void StationaryMantisTransformationAndBasicAttackDoNotGainFlightMomentum(int kind)
    {
        object state=Prepare("mantis");State(state,"kind",kind);
        Complete("mantis",state);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [Test] public void ARegularBeetleThrowDoesNotGainTheCrushJumpMomentum()
    {
        object state=Prepare("beetle");State(state,"crush",false);State(state,"time",BeetleFlipRules.End);
        Complete("beetle",state);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
    }

    [TestCase(0f)] [TestCase(6f)] [TestCase(60f)]
    public void RaptorLeapCompletionPacesAndBoundsTheRecordedExitVelocityWithoutInventingMovement(float speed)
    {
        Set(source,"leaping",true);Set(source,"leapReturning",true);
        Set(source,"leapEnd",Vector3.right*2f);Set(source,"leapExitVelocity",Vector3.right*speed);
        Call("FinishAssassinLeap",source);
        Assert.That(Get(source,"leaping"),Is.EqualTo(false));
        float expectedSpeed=Mathf.Min(speed,CombatMovementInertia.MaximumCoastSpeed)*CombatStatFormula.CombatAnimationRate;
        Assert.That(Vector3.Distance((Vector3)Get(source,"inertiaVelocity"),Vector3.right*expectedSpeed),Is.LessThan(.0001f));
        Vector3 before=Model(source).transform.position;
        Call("AdvanceUnitInertia",source,.1f);
        Assert.That(Model(source).transform.position.x-before.x,
            speed>0f?(Constraint)Is.GreaterThan(0f):Is.EqualTo(0f));
    }

    [TestCase(true)] [TestCase(false)]
    public void JaguarDashCompletionConnectsOnlyItsActualHorizontalTravelToCoast(bool horizontal)
    {
        Set(source,"skillId",JaguarCombatRules.SkillId);Set(source,"shadowSlashing",true);
        Set(source,"shadowSlashProgress",1f);Set(source,"shadowSlashStart",Vector3.zero);
        Set(source,"shadowSlashEnd",horizontal?Vector3.right*2f:Vector3.zero);
        Call("UpdateShadowSlash",source,.001f);
        Assert.That(Get(source,"shadowSlashing"),Is.EqualTo(false));
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,
            horizontal?(Constraint)Is.GreaterThan(0f):Is.EqualTo(0f));
        Vector3 before=Model(source).transform.position;
        Call("AdvanceUnitInertia",source,.1f);
        Assert.That(Model(source).transform.position.x-before.x,
            horizontal?(Constraint)Is.GreaterThan(0f):Is.EqualTo(0f));
    }

    [TestCase(true)] [TestCase(false)]
    public void GriffinDiveGroundLandingKeepsHorizontalMomentumWithoutUsingItsFallHeight(bool horizontal)
    {
        Set(source,"skillId",GriffinCombatRules.SkillId);Set(source,"griffinSkyfalling",true);
        Set(source,"griffinSkyfallProgress",1f);Set(source,"rank3GriffinLaunch",Vector3.zero);
        Set(source,"griffinSkyfallOrigin",horizontal?Vector3.right*2f:Vector3.zero);
        Set(source,"griffinSkyfallLiftHeight",20f);
        Call("UpdateGriffinSkyfall",source,.001f);
        Assert.That(Get(source,"griffinSkyfalling"),Is.EqualTo(false));
        Vector3 velocity=(Vector3)Get(source,"inertiaVelocity");
        Assert.That(velocity.y,Is.Zero);
        Assert.That(velocity.magnitude,horizontal?(Constraint)Is.GreaterThan(0f):Is.EqualTo(0f));
        Vector3 before=Model(source).transform.position;
        Call("AdvanceUnitInertia",source,.1f);
        Assert.That(Model(source).transform.position.x-before.x,
            horizontal?(Constraint)Is.GreaterThan(0f):Is.EqualTo(0f));
    }

    [Test] public void InterruptedWolfDashDoesNotStartANewSlideFromItsAbandonedLanding()
    {
        IEnumerator dash=(IEnumerator)Call("ResolveThreeSidedHunt",source,target,Vector3.right*4f,
            GameMasterData.Skill(WolfCombatRules.SkillId),1f,false,0);
        try
        {
            Assert.That(dash.MoveNext(),Is.True);
            Set(source,"positionRevision",1);
            Assert.That(dash.MoveNext(),Is.False);
            Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        }
        finally{(dash as IDisposable)?.Dispose();}
    }

    [Test] public void BattleResultCleanupPreservesAnExistingSlideAndRebindsItToTheEndingGeneration()
    {
        Call("BeginUnitInertia",source,Vector3.right*4f);
        BattleField("combatGeneration",1);BattleField("battleEnding",true);
        Call("EndBattleUnitAction",source);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.right*4f));
        Assert.That(Get(source,"inertiaGeneration"),Is.EqualTo(1));
        Assert.That(Get(source,"inertiaPositionRevision"),Is.EqualTo(Get(source,"positionRevision")));
        Vector3 before=Model(source).transform.position;
        Call("AdvanceUnitInertia",source,.1f);
        Assert.That(Model(source).transform.position.x,Is.GreaterThan(before.x));
        Assert.That(Get(source,"target"),Is.Null,"Showing results must not resume the unit's AI.");
    }

    [Test] public void ForcedPositionInterruptionClearsMomentumBeforeTheNextAction()
    {
        Call("BeginUnitInertia",source,Vector3.right*4f);
        Call("InterruptPositionalAction",source);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Vector3 before=Model(source).transform.position;
        Call("AdvanceUnitInertia",source,.1f);
        Assert.That(Model(source).transform.position,Is.EqualTo(before));
    }

    [Test]
    public void BattleResultCleanupPreservesTheOriginalFastDragAfterTheCoastHasSlowed()
    {
        Model(source).transform.position=Vector3.zero;
        Call("BeginUnitInertia",source,Vector3.right*6f);
        float initial=((Vector3)Get(source,"inertiaVelocity")).magnitude;
        Call("AdvanceUnitInertia",source,.5f);
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,Is.LessThan(2.5f));
        BattleField("combatGeneration",1);BattleField("battleEnding",true);
        Call("EndBattleUnitAction",source);
        float drag=CombatMovementInertia.CoastDecelerationForSpeed(initial);
        Assert.That(Get(source,"inertiaDeceleration"),Is.EqualTo(drag));
        Call("AdvanceUnitInertia",source,10f);
        Assert.That(Model(source).transform.position.x,Is.EqualTo(initial*initial/(2f*drag)).Within(.0001f));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(source,"inertiaDeceleration"),Is.EqualTo(0f));
    }

    [TestCase(30)] [TestCase(120)]
    public void LosingTheLastTargetTransfersActualWalkingMomentumIntoOneCompleteCoast(int fps)
    {
        Set(source,"id",0);Set(target,"id",1);
        Set(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1));
        Set(source,"target",target);Set(source,"attackRange",1);
        Set(source,"speedTilesPerSecond",2f);Set(source,"skillTimer",100f);
        Set(source,"targetLockTimer",100f);Set(source,"personalityReconsiderTimer",100f);
        Set(source,"lastGroundPosition",Model(source).transform.position);
        float dt=1f/fps;
        for(int frame=0;frame<fps/2;frame++)
        {
            Call("UpdateUnit",source,dt);
        }
        Assert.That(Get(source,"moving"),Is.EqualTo(true));
        Vector3 walking=(Vector3)Get(source,"velocity");
        Assert.That(walking.x,Is.GreaterThan(1f),"Exercise a real pursuit, rather than manually seed the coast.");
        Vector3 before=Model(source).transform.position;
        Set(target,"alive",false);
        Call("UpdateUnit",source,dt);
        Assert.That(Get(source,"target"),Is.Null);
        Assert.That(Model(source).transform.position.x,Is.GreaterThan(before.x),
            "Reducing a stored velocity without integrating it must not make target loss an instant stop.");
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,Is.GreaterThan(0f));
        for(int frame=0;frame<fps*2;frame++)Call("UpdateUnit",source,dt);
        float expected=walking.sqrMagnitude/(2f*CombatMovementInertia.CoastDecelerationForSpeed(walking.magnitude));
        Assert.That(Vector3.Distance(Model(source).transform.position,before),Is.EqualTo(expected).Within(.0005f),
            "Idle updates must neither discard nor reseed the transferred walking impulse.");
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Vector3 stopped=Model(source).transform.position;
        Call("UpdateUnit",source,dt);
        Assert.That(Model(source).transform.position,Is.EqualTo(stopped));
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0));
    }

    [TestCase(ExpeditionMonsterRank.Rank1,1f)] [TestCase(ExpeditionMonsterRank.Rank3,2f)]
    public void APhysicallyClearAllyInsideThePlacementMarginCannotEraseOrDeflectCoast(ExpeditionMonsterRank rank,float scale)
    {
        Model(source).transform.position=Vector3.zero;
        Set(source,"combatBodyScale",scale);Set(source,"expeditionRank",rank);
        object neighbour=Unit(Vector3.forward*(.75f*scale));
        Set(neighbour,"combatBodyScale",scale);Set(neighbour,"expeditionRank",rank);
        Assert.That((float)Call("BodyOverlapCost",source,Vector3.zero),Is.GreaterThan(0f));
        Assert.That((float)Call("BodyPenetrationCost",source,Vector3.zero),Is.Zero,
            "The fixture occupies only the allied placement buffer, never an actual body.");
        Call("BeginUnitInertia",source,Vector3.right*2f);
        int frames=0;
        while(((Vector3)Get(source,"inertiaVelocity")).sqrMagnitude>.000001f&&frames++<240)
        {
            Call("AdvanceUnitInertia",source,1f/120f);
            Assert.That(Vector3.Distance(Model(source).transform.position,Model(neighbour).transform.position),
                Is.GreaterThanOrEqualTo(.6f*scale-.0001f));
        }
        Assert.That(Model(source).transform.position.x,
            Is.EqualTo(4f/(2f*CombatMovementInertia.CoastDecelerationForSpeed(2f))).Within(.0001f));
        Assert.That(Model(source).transform.position.z,Is.EqualTo(0f).Within(.0001f),
            "A neighbour's placement margin must not turn the carried impulse sideways.");
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That((float)Call("BodyPenetrationCost",source,Model(source).transform.position),Is.Zero);
    }

    [TestCase(2f)] [TestCase(4f)]
    public void BattleResultTransfersWalkingVelocityWithoutOverwritingItsNewDragOrResumingAI(float speed)
    {
        Model(source).transform.position=Vector3.zero;
        Set(source,"target",target);Set(source,"moving",true);Set(source,"velocity",Vector3.right*speed);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(source,"inertiaDeceleration"),Is.EqualTo(0f));
        BattleField("combatGeneration",1);BattleField("battleEnding",true);
        Call("EndBattleUnitAction",source);
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.right*speed));
        float drag=CombatMovementInertia.CoastDecelerationForSpeed(speed);
        Assert.That((float)Get(source,"inertiaDeceleration"),Is.EqualTo(drag).And.GreaterThan(0f),
            "A previously unset drag must not overwrite the newly captured walking drag with zero.");
        Assert.That(Get(source,"inertiaGeneration"),Is.EqualTo(1));
        Assert.That(Get(source,"inertiaPositionRevision"),Is.EqualTo(Get(source,"positionRevision")));
        Call("AdvanceUnitInertia",source,.1f);
        Assert.That(Model(source).transform.position.x,Is.GreaterThan(.1f));
        Call("AdvanceUnitInertia",source,10f);
        Assert.That(Model(source).transform.position.x,Is.EqualTo(speed*speed/(2f*drag)).Within(.0001f));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(source,"target"),Is.Null);
        Assert.That(Get(source,"moving"),Is.EqualTo(false));
        Assert.That(Get(source,"currentAction"),Is.EqualTo("idle"));
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(0));
    }

    private object Prepare(string family)
    {
        string type=family=="mammoth"||family=="wall-rush"?"MammothChargeState":
            family=="griffin"?"GriffinHuntState":family=="mantis"?"MantisReaperAction":"BeetleFlipState";
        object state=Activator.CreateInstance(typeof(MonsterAutoBattlePrototype).GetNestedType(type,BindingFlags.NonPublic),true);
        string field=family=="mammoth"||family=="wall-rush"?"mammothCharge":
            family=="griffin"?"griffinHunt":family=="mantis"?"mantisReaperAction":"beetleFlip";
        Set(source,field,state);
        if(family=="mammoth"||family=="wall-rush")
        {
            State(state,"wallRush",family=="wall-rush");
            Vector3 ground=Model(source).transform.position;ground.y=0f;
            State(state,"end",ground);State(state,"direction",ground.normalized);
        }
        else if(family=="griffin")
        {
            State(state,"target",target);State(state,"grabbed",true);
            State(state,"time",GriffinHuntRules.Landing-.001f);
            State(state,"casterLanding",new Vector3(Model(source).transform.position.x,0f,0f));
            State(state,"landing",Model(target).transform.position+Vector3.forward*5f);
            Set(source,"griffinSkyfalling",true);Set(source,"skillId",GriffinCombatRules.SkillId);
        }
        else if(family=="mantis")
        {
            State(state,"kind",2);State(state,"age",MantisRank3Rules.ReaperFlightEnd);
        }
        else
        {
            State(state,"target",target);State(state,"crush",true);State(state,"landed",true);
            State(state,"casterLanded",true);State(state,"time",BeetleFlipRules.CrushEnd);
        }
        return state;
    }

    private void Complete(string family,object state,bool natural=true)
    {
        if(family=="mammoth"||family=="wall-rush")
        {
            if(natural)Call("AdvanceMammothCharge",source,state,
                family=="wall-rush"?MammothRank3Rules.WallRushArrival:MammothCombatRules.ContactTime);
            else Call("EndMammothCharge",source,state);
        }
        else if(family=="griffin")
        {
            if(natural)Call("UpdateGriffinHunt",source,.01f);else Call("EndGriffinHunt",source);
        }
        else if(family=="mantis")Call("EndMantisReaperAction",source,natural);
        else
        {
            if(!natural)State(state,"time",0f);
            Call("EndBeetleFlip",source,state);
        }
    }

    private GameObject Go(string name){var go=new GameObject(name);objects.Add(go);return go;}
    private object Unit(Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);
        GameObject model=Go("Skill inertia body");model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",Go("Skill inertia ring"));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"collisionRadius",.3f);
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_berber"));
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle)).Add(unit);
        return unit;
    }
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private void State(object state,string field,object value)=>state.GetType().GetField(field).SetValue(state,value);
    private void BattleField(string field,object value)=>typeof(MonsterAutoBattlePrototype).GetField(field,Private).SetValue(battle,value);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Private).Invoke(battle,args);
}
