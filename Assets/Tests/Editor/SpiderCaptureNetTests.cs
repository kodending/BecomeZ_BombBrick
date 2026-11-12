using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SpiderCaptureNetTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly Type type=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object spider,enemy;
    private SkillMasterRow skill;
    private IList Zones=>(IList)type.GetField("spiderWebZones",Private).GetValue(battle);
    private CombatStatusState Status=>(CombatStatusState)Get(enemy,"combatStatuses");

    [SetUp] public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Capture net test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100));
        spider=Unit(0,-3);enemy=Unit(1,0);Set(spider,"target",enemy);
        skill=GameMasterData.Skill(SpiderCombatRules.SkillId);
    }
    [TearDown] public void TearDown()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }
    private object Unit(int team,float x)
    {
        var u=Activator.CreateInstance(unitType,true);var units=(IList)type.GetField("units",Private).GetValue(battle);
        var model=new GameObject("Web unit");model.transform.position=Vector3.right*x;
        Set(u,"id",units.Count);Set(u,"stableId","web-"+units.Count);Set(u,"model",model);
        Set(u,"ring",new GameObject("Web ring"));Set(u,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(u,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+(team==0?"aranea":"woongi")));
        Set(u,"skillId",team==0?SpiderCombatRules.SkillId:BearCombatRules.SkillId);
        Set(u,"alive",true);Set(u,"hp",5000f);Set(u,"maxHp",5000f);Set(u,"damage",100f);
        Set(u,"attackInterval",1f);Set(u,"collisionRadius",.3f);Set(u,"skillCooldownDuration",9f);
        units.Add(u);
        ((CombatStatusState)Get(u,"combatStatuses")).Applied+=effect=>Call("HandleCombatStatusApplied",u,effect);
        return u;
    }
    private void Set(object u,string field,object value)=>unitType.GetField(field).SetValue(u,value);
    private object Get(object u,string field)=>unitType.GetField(field).GetValue(u);
    private object Call(string method,params object[] args)=>type.GetMethod(method,Private).Invoke(battle,args);
    private static void Advance(Component fx,float dt)=>fx.GetType().GetMethod("Animate",Private).Invoke(fx,new object[]{dt});
    private void Tick(float dt)=>Call("UpdateSpiderWebZones",dt);
    private void Zone()=>Call("CreateSpiderWebZone",spider,Vector3.zero,1.3f*MonsterAutoBattlePrototype.TileSize,4f,true);

    [Test] public void FlightLandsOnceAndUsesTheOriginalTargetAndModifiers()
    {
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        Set(spider,"spiderWebTarget",Vector3.zero);Set(spider,"spiderWebDamageMultiplier",1.5f);
        Set(spider,"spiderWebRadiusMultiplier",1.2f);
        Call("LaunchSpiderCaptureWeb",spider,skill,1f,false,0);
        var fx=Object.FindObjectOfType<PixelSpiderCaptureNet>();Assert.That(fx,Is.Not.Null);
        Set(spider,"spiderWebTarget",Vector3.right*20);Set(spider,"spiderWebDamageMultiplier",3f);
        Advance(fx,.2f);Assert.That(Zones.Count,Is.Zero);Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f));
        Advance(fx,.11f);Assert.That(Zones.Count,Is.Zero);
        Advance(fx,.3f);Assert.That(fx==null,Is.True);Assert.That(Zones.Count,Is.EqualTo(1));
        Assert.That(Get(enemy,"hp"),Is.EqualTo(4835f).Within(.01f));
        var z=Zones[0];Assert.That(z.GetType().GetField("center").GetValue(z),Is.EqualTo(Vector3.zero));
        Assert.That((float)z.GetType().GetField("radius").GetValue(z),Is.EqualTo(1.3f*1.7f*1.2f).Within(.001f));
        Assert.That(z.GetType().GetField("remaining").GetValue(z),Is.EqualTo(4f));
    }

    [TestCase(false)] [TestCase(true)] public void AirborneNetSurvivesDeathAndBattleResetCancelsIt(bool reset)
    {
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        Call("LaunchSpiderCaptureWeb",spider,skill,1f,false,0);
        var fx=Object.FindObjectOfType<PixelSpiderCaptureNet>();
        if(reset)type.GetField("combatGeneration",Private).SetValue(battle,1);else Set(spider,"alive",false);
        Advance(fx,1f);Assert.That(fx==null,Is.True);Assert.That(Zones.Count,Is.EqualTo(reset?0:1));
        Assert.That(Get(enemy,"hp"),Is.EqualTo(reset?5000f:4890f));
    }

    [Test] public void RootNeedsContinuousStayAndOccursOnlyOncePerNet()
    {
        Zone();Tick(.79f);Assert.That(Status.Has(CombatStatusKind.Root),Is.False);
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*4;Tick(.01f);
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.zero;Tick(.79f);
        Assert.That(Status.Has(CombatStatusKind.Root),Is.False);Tick(.02f);
        Assert.That(Status.Get(CombatStatusKind.Root).remaining,Is.EqualTo(1f));
        Status.Remove(CombatStatusKind.Root);Tick(1f);Assert.That(Status.Has(CombatStatusKind.Root),Is.False);
        Assert.That((float)Call("SpiderMovementMultiplier",enemy),Is.EqualTo(.65f).Within(.001f));
        Tick(4f);Assert.That(Zones.Count,Is.Zero);Assert.That(Call("SpiderMovementMultiplier",enemy),Is.EqualTo(1f));
    }

    [TestCase(SpiderCombatRules.AraneaTraitId,1.6f,false)]
    [TestCase(SpiderCombatRules.IcearachneTraitId,1.25f,true)]
    public void IntrinsicControlDurationAndSlowStayUnchanged(string trait,float duration,bool freeze)
    {
        ((IList)Get(spider,"activeTraitIds")).Add(trait);Zone();Tick(.8f);
        Assert.That(Status.Get(freeze?CombatStatusKind.Freeze:CombatStatusKind.Root).remaining,Is.EqualTo(duration));
        Assert.That((float)Call("SpiderMovementMultiplier",enemy),Is.EqualTo(freeze?.5f:.65f).Within(.001f));
    }

    [TestCase(SpiderCombatRules.SilantiTraitId,75f)]
    [TestCase(SpiderCombatRules.RagonTraitId,70f)]
    public void IntrinsicThreeSecondDamageStaysUnchanged(string trait,float damage)
    {
        ((IList)Get(spider,"activeTraitIds")).Add(trait);Zone();Tick(1f);Tick(1f);Tick(1f);
        Assert.That((float)Get(enemy,"hp"),Is.EqualTo(5000f-damage).Within(.01f));
    }

    [Test] public void LandedNetKeepsItsExactRangeAndBreaksCleanly()
    {
        var root=PixelSpiderCaptureNet.Ground(null,Vector3.zero,2.21f,Color.white);
        var fx=root.GetComponent<PixelSpiderCaptureNet>();Advance(fx,.2f);
        var sr=root.transform.Find("Silk").GetComponent<SpriteRenderer>();
        Assert.That(sr.bounds.size.x,Is.EqualTo(4.42f).Within(.001f));
        Advance(fx,2f);Assert.That(sr.bounds.size.x,Is.EqualTo(4.42f).Within(.001f));
        fx.Break();Advance(fx,.25f);Assert.That(fx==null,Is.True);
    }

    [TestCase("033_aranea")] [TestCase("034_silanti")] [TestCase("035_ragon")] [TestCase("036_icearachne")]
    public void AllSpeciesKeepTheirSkillAnimationScaleAndSingleRelease(string id)
    {
        var driver=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+id)).GetComponentInChildren<SpriteMonsterAnimationDriver>();
        int hits=0;
        driver.PlaySkillAttack(1f,()=>hits++);
        for(int i=0;i<120;i++)
        {
            typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{1f/60f});
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
            Assert.That(driver.transform.localScale,
                Is.EqualTo(Vector3.one*(driver.CurrentMotionName=="SkillAttack"?1.24f:1f)));
        }
        Assert.That(hits,Is.EqualTo(1));
    }

    [Test] public void LateRootSilkOutlivesTheNetAndReleasesWithTheActualStatus()
    {
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        var driver=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/033_aranea")).GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(enemy,"spriteAnimation",driver);
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*5;Zone();Tick(3f);
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.zero;Tick(.81f);
        var silk=Object.FindObjectOfType<PixelSpiderSilkBind>();Assert.That(silk,Is.Not.Null);Assert.That(silk.Bound,Is.True);
        Tick(.2f);Advance(silk,.1f);Assert.That(Zones.Count,Is.Zero);Assert.That(silk!=null,Is.True);
        Status.Remove(CombatStatusKind.Root);Advance(silk,.25f);Assert.That(silk==null,Is.True);
    }

    [Test] public void SilkBindingBlocksMovementBasicAttackAndCombinedSkillUntilReleased()
    {
        Set(enemy,"target",spider);Set(enemy,"speedTilesPerSecond",3f);Set(enemy,"skillTimer",0f);
        Status.ApplyTimed(CombatStatusKind.Root,1f,spider);
        Vector3 position=((GameObject)Get(enemy,"model")).transform.position;
        Call("Attack",enemy,spider);Assert.That(Get(enemy,"basicAttackCount"),Is.EqualTo(0));
        Assert.That(Call("TryUseSkill",enemy,false),Is.False);Assert.That(Call("TryUseSkill",enemy,true),Is.False);
        Call("BeginBasicAttackImpact",enemy,spider,100f,false,1f);Assert.That(Get(spider,"hp"),Is.EqualTo(5000f));
        Call("UpdateUnit",enemy,.1f);
        Assert.That(((GameObject)Get(enemy,"model")).transform.position,Is.EqualTo(position));
        Assert.That(Get(enemy,"currentAction"),Is.EqualTo("silk_bound"));
        Status.Remove(CombatStatusKind.Root);
        Call("Attack",enemy,spider);Assert.That(Get(enemy,"basicAttackCount"),Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void BindingCancelsAQueuedAttackOrHeldSkillAndNeverFiresItOnRelease(bool skillAttack)
    {
        var driver=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/025_woongi")).GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(enemy,"spriteAnimation",driver);int hits=0;
        if(skillAttack)driver.PlaySkillAttackWithHold(2,1f,1f,()=>hits++);
        else driver.PlayBasicAttack(1f,()=>hits++);
        Status.ApplyTimed(CombatStatusKind.Root,1f,spider);
        Sprite pose=driver.GetComponent<SpriteRenderer>().sprite;
        for(int i=0;i<90;i++)
        {
            typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{1f/60f});
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
            Assert.That(driver.GetComponent<SpriteRenderer>().sprite,Is.SameAs(pose));
        }
        Assert.That(hits,Is.Zero);Status.Remove(CombatStatusKind.Root);driver.SetFrozen(false);
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{2f});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
        Assert.That(hits,Is.Zero);driver.PlayBasicAttack(1f,()=>hits++);
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{1f});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
        Assert.That(hits,Is.EqualTo(1));
    }

    [Test] public void ControlImmunityDoesNotStopAnAttackOrShowAClosedCocoon()
    {
        Status.ControlImmune=true;Status.ApplyTimed(CombatStatusKind.Root,1f,spider);
        Call("Attack",enemy,spider);Assert.That(Get(enemy,"basicAttackCount"),Is.EqualTo(1));
        Assert.That(Status.Has(CombatStatusKind.Root),Is.False);
    }

    [TestCase(false)] [TestCase(true)]
    public void HeavenlyPullFinishesBeforeItsSingleBindAndNeverOvershoots(bool precise)
    {
        if(precise)((IList)Get(spider,"activeTraitIds")).Add(SpiderCombatRules.AraneaTraitId);
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*4f;
        Call("ResolveHeavenlyCaptureNet",spider,Vector3.zero,1f,1f);
        Assert.That(Get(enemy,"hp"),Is.EqualTo(4840f));
        Assert.That(Get(enemy,"displacementDuration"),Is.EqualTo(.4f));
        Assert.That(((Vector3)Get(enemy,"displacementEnd")).x,Is.EqualTo(4f-1.7f*(precise?1.5f:1.25f)).Within(.01f));
        Tick(.39f);Assert.That(Status.Has(CombatStatusKind.Root),Is.False);
        Call("UpdateDisplacement",enemy,.4f);Tick(.02f);
        Assert.That(Get(enemy,"displacing"),Is.False);
        Assert.That(Status.Get(CombatStatusKind.Root).remaining,Is.EqualTo(precise?1.6f:1f));
        Status.Remove(CombatStatusKind.Root);Tick(4f);
        Assert.That(Status.Has(CombatStatusKind.Root),Is.False);
        Assert.That(Status.Has(CombatStatusKind.Knockdown),Is.False);
        Assert.That(Get(enemy,"hp"),Is.EqualTo(4420f));
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*.2f;
        Call("ResolveHeavenlyCaptureNet",spider,Vector3.zero,1f,1f);
        Assert.That(((Vector3)Get(enemy,"displacementEnd")).x,Is.GreaterThanOrEqualTo(-.001f));
    }

    [TestCase("",580f)] [TestCase(SpiderCombatRules.SilantiTraitId,680f)]
    [TestCase(SpiderCombatRules.RagonTraitId,650f)] [TestCase(SpiderCombatRules.IcearachneTraitId,580f)]
    public void HeavenlyDealsExactlyFourTicksAndOneFinalWithScaledIntrinsics(string intrinsic,float total)
    {
        ((IList)Get(spider,"activeTraitIds")).Add(intrinsic);
        Call("ResolveHeavenlyCaptureNet",spider,Vector3.zero,1.5f,1f);
        Tick(.4f);
        if(intrinsic==SpiderCombatRules.IcearachneTraitId)
        {
            Assert.That(Status.Get(CombatStatusKind.Freeze).remaining,Is.EqualTo(1.25f));
            Assert.That(Status.Has(CombatStatusKind.Root),Is.False);
            Assert.That(Call("SpiderMovementMultiplier",enemy),Is.EqualTo(.5f));
        }
        Tick(.6f);Tick(1f);Tick(1f);Tick(3f);Tick(1f);
        Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f-total*1.5f).Within(.01f));
        Assert.That(Zones.Count,Is.Zero);
    }

    [Test] public void HeavenlyUsesExactBoundaryAndDoesNotBindLateArrivalsOrEscapedTargets()
    {
        var late=Unit(1,4.26f);var ally=Unit(0,1f);
        Call("ResolveHeavenlyCaptureNet",spider,Vector3.zero,1f,1f);
        Assert.That(Get(late,"hp"),Is.EqualTo(5000f));Assert.That(Get(ally,"hp"),Is.EqualTo(5000f));
        ((GameObject)Get(late,"model")).transform.position=Vector3.right*3f;
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*6f;
        Tick(.4f);Assert.That(Status.Has(CombatStatusKind.Root),Is.False);
        Assert.That(((CombatStatusState)Get(late,"combatStatuses")).Has(CombatStatusKind.Root),Is.False);
        Tick(4f);Assert.That(Get(late,"hp"),Is.EqualTo(4580f));
        Assert.That(Get(enemy,"hp"),Is.EqualTo(4840f));
    }

    [TestCase(false)] [TestCase(true)]
    public void HeavenlyStillPullsUpToACentralBlockerInsteadOfCancellingAllMovement(bool freeze)
    {
        if(freeze)((IList)Get(spider,"activeTraitIds")).Add(SpiderCombatRules.IcearachneTraitId);
        Unit(1,0);((GameObject)Get(enemy,"model")).transform.position=Vector3.right*2f;
        Call("ResolveHeavenlyCaptureNet",spider,Vector3.zero,1f,1f);
        Assert.That(Get(enemy,"displacing"),Is.True);
        for(int i=0;i<27;i++)
        {
            Tick(1f/60f);Call("UpdateUnit",enemy,1f/60f);
            if(Status.Has(freeze?CombatStatusKind.Freeze:CombatStatusKind.Root))
                Assert.That(Get(enemy,"displacing"),Is.False,"Control must not suspend the unfinished pull.");
        }
        float x=((GameObject)Get(enemy,"model")).transform.position.x;
        Assert.That(x,Is.InRange(.625f,.66f));Assert.That(Get(enemy,"displacing"),Is.False);
        Assert.That(Status.Has(freeze?CombatStatusKind.Freeze:CombatStatusKind.Root),Is.True);
    }

    [TestCase(15,false)] [TestCase(60,false)] [TestCase(120,false)]
    [TestCase(15,true)] [TestCase(60,true)] [TestCase(120,true)]
    public void HeavenlyPullsATouchingCrowdAsSpaceOpens(int fps,bool reverseOrder)
    {
        var crowd=new[]{enemy,Unit(1,1.525f),Unit(1,2.15f)};
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*.9f;
        foreach(var u in crowd){Set(u,"cooldown",999f);Set(u,"skillTimer",999f);}
        Call("ResolveHeavenlyCaptureNet",spider,Vector3.zero,1f,1f);
        for(int frame=0;frame<Mathf.CeilToInt(.4f*fps)+2;frame++)
        {
            Tick(1f/fps);
            for(int i=0;i<crowd.Length;i++)Call("UpdateUnit",crowd[reverseOrder?crowd.Length-1-i:i],1f/fps);
        }
        for(int i=0;i<crowd.Length;i++)
        {
            float x=((GameObject)Get(crowd[i],"model")).transform.position.x;
            Assert.That(x,Is.LessThan(.9f+.625f*i-.35f),"Every captured body should follow newly opened space.");
            Assert.That(Get(crowd[i],"displacing"),Is.False);
            if(i>0)Assert.That(x-((GameObject)Get(crowd[i-1],"model")).transform.position.x,
                Is.GreaterThanOrEqualTo(.625f-.001f),"Captured bodies must not overlap.");
        }
    }

    [TestCase(CombatStatusKind.Freeze)] [TestCase(CombatStatusKind.Knockdown)]
    public void HeavenlyPullMovesAlreadyControlledBodiesWithoutResumingTheirActions(CombatStatusKind control)
    {
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*3f;
        Status.ApplyTimed(control,2f,spider);Set(enemy,"cooldown",999f);Set(enemy,"skillTimer",999f);
        Call("ResolveHeavenlyCaptureNet",spider,Vector3.zero,1f,1f);
        for(int i=0;i<27;i++){Tick(1f/60f);Call("UpdateUnit",enemy,1f/60f);}
        Assert.That(((GameObject)Get(enemy,"model")).transform.position.x,Is.EqualTo(.875f).Within(.02f));
        Assert.That(Get(enemy,"displacing"),Is.False);
        Assert.That(Status.Has(control),Is.True);Assert.That(Get(enemy,"basicAttackCount"),Is.Zero);
    }

    [Test] public void HeavenlySourceDeathPreservesPendingCaptureAndFinalDamage()
    {
        Call("ResolveHeavenlyCaptureNet",spider,Vector3.zero,1f,1f);
        Set(spider,"alive",false);Tick(5f);
        Assert.That(Zones.Count,Is.Zero);Assert.That(Get(enemy,"hp"),Is.EqualTo(4420f));
        Assert.That(Status.Has(CombatStatusKind.Root),Is.True);
    }

    [Test] public void HeavenlyFlightAndLandedWeaveShareTheTraitRadiusAndSnapshotModifiers()
    {
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        ((IList)Get(spider,"activeTraitIds")).Add(ArachneRank3Rules.HeavenlyNet);
        Set(spider,"spiderWebTarget",Vector3.zero);Set(spider,"spiderWebDamageMultiplier",1.5f);
        Set(spider,"spiderWebRadiusMultiplier",1.2f);
        Call("LaunchSpiderCaptureWeb",spider,skill,1f,false,0);
        var flight=Object.FindObjectOfType<PixelSpiderCaptureNet>();
        Assert.That(typeof(PixelSpiderCaptureNet).GetField("radius",Private).GetValue(flight),Is.EqualTo(5.1f).Within(.001f));
        Set(spider,"spiderWebDamageMultiplier",3f);Set(spider,"spiderWebRadiusMultiplier",3f);
        Advance(flight,1f);Assert.That(Get(enemy,"hp"),Is.EqualTo(4760f));
        var root=(GameObject)Zones[0].GetType().GetField("root").GetValue(Zones[0]);
        var fx=root.GetComponent<PixelSpiderCaptureNet>();var mesh=root.GetComponentInChildren<MeshFilter>().sharedMesh;
        float before=mesh.vertices[17].magnitude;Advance(fx,.4f);
        Assert.That(mesh.vertices[17].magnitude,Is.LessThan(before));
        Assert.That(mesh.bounds.size.x,Is.EqualTo(10.2f).Within(.001f));
        fx.Pulse();Advance(fx,.11f);Assert.That(mesh.bounds.size.x,Is.EqualTo(10.2f).Within(.001f));
        fx.Break();Advance(fx,.34f);Assert.That(fx==null,Is.True);Assert.That(mesh==null,Is.True);
    }

    private IEnumerator Eightfold(float scale=1f,float radiusScale=1f)=>
        (IEnumerator)Call("ResolveEightfoldWeb",spider,Vector3.zero,scale,radiusScale,0);

    [TestCase(1f,1f)] [TestCase(1.5f,1.2f)]
    public void EightfoldSweepsTheWholeRadiusFourTimesAndFinishesWithoutPullOrKnockdown(float scale,float radiusScale)
    {
        float radius=MonsterAutoBattlePrototype.TileSize*2.5f*radiusScale;
        var edge=Unit(1,radius+.29f);var outside=Unit(1,radius+.31f);var ally=Unit(0,.2f);
        var cast=Eightfold(scale,radiusScale);Assert.That(cast.MoveNext(),Is.True);
        Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f));
        for(int i=0;i<4;i++)
        {
            Assert.That(cast.MoveNext(),Is.True);
            Assert.That(cast.MoveNext(),Is.True);
            Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f-90f*scale*(i+1)).Within(.01f));
            Assert.That(Get(edge,"hp"),Is.EqualTo(Get(enemy,"hp")));
        }
        Assert.That(cast.MoveNext(),Is.True); // Loaded final frame, still no final damage.
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f-600f*scale).Within(.01f));
        Assert.That(Get(edge,"hp"),Is.EqualTo(Get(enemy,"hp")));
        Assert.That(Get(outside,"hp"),Is.EqualTo(5000f));Assert.That(Get(ally,"hp"),Is.EqualTo(5000f));
        Assert.That(Get(enemy,"displacing"),Is.False);
        Assert.That(Status.Has(CombatStatusKind.Knockdown),Is.False);
        Assert.That(Get(spider,"skillHits"),Is.EqualTo(10));Assert.That(Get(spider,"traitActivations"),Is.EqualTo(1));
    }

    [TestCase(false,0)] [TestCase(false,3)] [TestCase(false,9)]
    [TestCase(true,0)] [TestCase(true,3)] [TestCase(true,9)]
    public void EightfoldSurvivesCasterDeathAndResetCleansTheSpriteSequence(bool reset,int cutSteps)
    {
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        var cast=Eightfold();cast.MoveNext();for(int i=0;i<cutSteps;i++)cast.MoveNext();
        float hp=(float)Get(enemy,"hp");
        Assert.That(Object.FindObjectsOfType<PixelEightfoldWeb>().Length,Is.EqualTo(1));
        if(reset){type.GetField("combatGeneration",Private).SetValue(battle,1);Call("ClearSpiderWebZones");}
        else Set(spider,"alive",false);
        while(cast.MoveNext()){}
        Assert.That(Get(enemy,"hp"),Is.EqualTo(reset?hp:4400f));
        if(reset)Assert.That(Object.FindObjectsOfType<PixelEightfoldWeb>().Length,Is.Zero);
    }

    [Test] public void EightfoldLocksTheCastCenterButRechecksMovingEnemiesOnEachCut()
    {
        var cast=Eightfold();cast.MoveNext();cast.MoveNext();cast.MoveNext();
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*20;
        var entrant=Unit(1,1f);while(cast.MoveNext()){}
        Assert.That(Get(enemy,"hp"),Is.EqualTo(4910f));
        Assert.That(Get(entrant,"hp"),Is.EqualTo(4490f));
    }

    [TestCase(SpiderCombatRules.AraneaTraitId)] [TestCase(SpiderCombatRules.IcearachneTraitId)]
    public void EightfoldKeepsSpeciesFinalControlWithoutTheSharedKnockdown(string intrinsic)
    {
        ((IList)Get(spider,"activeTraitIds")).Add(intrinsic);
        var cast=Eightfold();while(cast.MoveNext()){}
        Assert.That(Status.Has(intrinsic==SpiderCombatRules.AraneaTraitId?CombatStatusKind.Root:CombatStatusKind.Freeze),Is.True);
        Assert.That(Status.Has(CombatStatusKind.Knockdown),Is.False);Assert.That(Get(enemy,"displacing"),Is.False);
    }

    [Test] public void EightfoldUsesSixteenDistinctPointFilteredFramesAndFinishesAfterItsSourceRendererIsGone()
    {
        var frames=PixelEightfoldWeb.Frames;Assert.That(frames.Length,Is.EqualTo(16));
        Assert.That(frames[0].texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(frames[0].rect.width,Is.EqualTo(128));
        for(int i=1;i<frames.Length;i++)Assert.That(frames[i].rect,Is.Not.EqualTo(frames[i-1].rect));
        var fx=PixelEightfoldWeb.Play(Vector3.zero,4.25f,null,Color.white,false,()=>true);
        fx.Aim(0,Array.Empty<SpriteRenderer>(),true);Advance(fx,.071f);
        ((IList)type.GetField("eightfoldWebs",Private).GetValue(battle)).Add(fx);
        Call("ClearSpiderWebZonesAfterBattle");Assert.That(fx!=null,Is.True);
        Call("ClearSpiderWebZones");Assert.That(fx==null,Is.True);
        fx=PixelEightfoldWeb.Play(Vector3.zero,4.25f,null,Color.white,false,()=>true);
        fx.Aim(0,Array.Empty<SpriteRenderer>(),true);Advance(fx,.66f);Assert.That(fx==null,Is.True);
    }

    [TestCase(false)] [TestCase(true)]
    public void SilkLancePassesTheBodyThenPinsItsTipAndOnlyShakesOnGroundContact(bool final)
    {
        var driver=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/025_woongi")).GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var target=driver.GetComponent<SpriteRenderer>();int impacts=0;
        var fx=PixelEightfoldWeb.Play(Vector3.zero,4.25f,target,Color.white,false,()=>true,
            heavy=>{Assert.That(heavy,Is.EqualTo(final));impacts++;});
        fx.Aim(0,new[]{target},final);
        var rear=fx.transform.Find("Silk Lance Rear");
        var front=rear.GetComponentInChildren<SpriteRenderer>(true);
        Vector3 start=rear.position;
        Advance(fx,final?.07f:.06f);Vector3 contact=rear.position;
        Assert.That(contact.y,Is.LessThan(start.y));Assert.That(impacts,Is.Zero);
        Advance(fx,.046f);Vector3 pinned=rear.position;
        Assert.That(pinned.y,Is.LessThan(contact.y));Assert.That(impacts,Is.EqualTo(1));
        Assert.That(fx.GetComponentsInChildren<ParticleSystem>().Length,Is.EqualTo(final?8:2));
        var tip=rear.Find("Silk Lance Emerging Tip").GetComponent<SpriteRenderer>();
        Assert.That(tip.enabled,Is.True);
        Assert.That(tip.sortingOrder,Is.GreaterThan(target.sortingOrder));
        Assert.That(rear.GetComponent<SpriteRenderer>().sortingOrder,Is.LessThan(target.sortingOrder));
        Object.DestroyImmediate(driver.gameObject);Advance(fx,.05f);
        Assert.That(rear.position,Is.EqualTo(pinned));Assert.That(impacts,Is.EqualTo(1));
        fx.Cancel();Assert.That(fx==null,Is.True);
    }

    private IEnumerator Funeral(object primary=null,float scale=1f,float radiusScale=1f)=>
        (IEnumerator)Call("ResolveCocoonFuneral",spider,primary??enemy,Vector3.zero,scale,radiusScale,0);

    [TestCase(0)] [TestCase(1)]
    public void SilkQueenWrapsAllEightScatteredEnemiesOnEitherTeamAndKeepsAlliesSafe(int sourceTeam)
    {
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        Set(spider,"team",Enum.ToObject(unitType.GetField("team").FieldType,sourceTeam));
        Set(enemy,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-sourceTeam));
        var victims=new List<object>{enemy};for(int i=1;i<8;i++)victims.Add(Unit(1-sourceTeam,i*6f));
        var ally=Unit(sourceTeam,0f);
        for(int i=0;i<victims.Count;i++)
        {
            var model=(GameObject)Get(victims[i],"model");model.transform.position=new Vector3(-21f+i*6f,0f,i%2==0?-12f:12f);
            var driver=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/025_woongi")).GetComponentInChildren<SpriteMonsterAnimationDriver>();
            driver.transform.SetParent(model.transform,false);Set(victims[i],"spriteAnimation",driver);
        }
        using(var cast=(IDisposable)Call("ResolveSilkQueenCocoons",spider,null,Vector3.zero,1f,1f,0))
        {
            Assert.That(((IEnumerator)cast).MoveNext(),Is.True);
            foreach(var victim in victims)
            {
                Assert.That(Get(victim,"hp"),Is.EqualTo(4900f));
                Assert.That(((CombatStatusState)Get(victim,"combatStatuses")).Has(CombatStatusKind.Root),Is.True);
            }
            Assert.That(Object.FindObjectsOfType<PixelSpiderSilkBind>().Length,Is.EqualTo(8),"The actual field needs a damaging cocoon on each enemy, not decorative stage shells.");
            foreach(var fx in Object.FindObjectsOfType<PixelSpiderSilkBind>())
            {
                var shell=fx.transform.Find("Cocoon Left").GetComponent<SpriteRenderer>();
                Assert.That(shell.enabled,Is.True);Assert.That(shell.color.a,Is.GreaterThan(.9f),"The return wipe must reveal closed cocoons even while the battlefield clock is held.");
            }
            Assert.That(Get(ally,"hp"),Is.EqualTo(5000f));
            Assert.That(((CombatStatusState)Get(ally,"combatStatuses")).Has(CombatStatusKind.Root),Is.False);
        }
        Assert.That(Object.FindObjectsOfType<PixelSpiderSilkBind>().Length,Is.Zero);
    }

    [TestCase(1f)] [TestCase(1.5f)]
    public void SilkQueenAppliesEachExistingCompressionAndFinalDamageExactlyOnceToDistantEnemies(float scale)
    {
        var distant=Unit(1,35f);
        var cast=(IEnumerator)Call("ResolveSilkQueenCocoons",spider,enemy,Vector3.zero,scale,1f,0);
        Assert.That(cast.MoveNext(),Is.True);
        foreach(var victim in new[]{enemy,distant})Assert.That(Get(victim,"hp"),Is.EqualTo(5000f-100f*scale));
        Assert.That(((WaitForSeconds)cast.Current),Is.Not.Null);
        Assert.That(cast.MoveNext(),Is.True);
        foreach(var victim in new[]{enemy,distant})Assert.That(Get(victim,"hp"),Is.EqualTo(5000f-160f*scale));
        Assert.That(cast.MoveNext(),Is.True);
        foreach(var victim in new[]{enemy,distant})Assert.That(Get(victim,"hp"),Is.EqualTo(5000f-220f*scale));
        Assert.That(cast.MoveNext(),Is.False);
        foreach(var victim in new[]{enemy,distant})Assert.That(Get(victim,"hp"),Is.EqualTo(5000f-500f*scale));
        Assert.That(cast.MoveNext(),Is.False);
        foreach(var victim in new[]{enemy,distant})Assert.That(Get(victim,"hp"),Is.EqualTo(5000f-500f*scale));
    }

    [Test]public void SilkQueenCapsOverlappingBurstsAtTwoWithoutRestoringTheThreeVictimLimit()
    {
        var victims=new List<object>{enemy};for(int i=1;i<8;i++)victims.Add(Unit(1,i*.12f));
        var ally=Unit(0,.3f);var cast=(IEnumerator)Call("ResolveSilkQueenCocoons",spider,enemy,Vector3.zero,1f,1f,0);
        while(cast.MoveNext()){}
        foreach(var victim in victims)Assert.That(Get(victim,"hp"),Is.EqualTo(4300f));
        Assert.That(Get(ally,"hp"),Is.EqualTo(5000f));
    }

    [TestCase(false)] [TestCase(true)]
    public void SilkQueenReleasedCocoonsKeepOwnerDeathPolicyAndStopOnGenerationReset(bool reset)
    {
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        var driver=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/025_woongi")).GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(enemy,"spriteAnimation",driver);
        var cast=(IEnumerator)Call("ResolveSilkQueenCocoons",spider,enemy,Vector3.zero,1f,1f,0);Assert.That(cast.MoveNext(),Is.True);
        Assert.That(Object.FindObjectsOfType<PixelSpiderSilkBind>().Length,Is.EqualTo(1));
        if(reset){type.GetField("combatGeneration",Private).SetValue(battle,1);Call("ClearSpiderWebZones");}
        else Set(spider,"alive",false);
        while(cast.MoveNext()){}
        Assert.That(Get(enemy,"hp"),Is.EqualTo(reset?4900f:4500f));
        if(reset)Assert.That(Object.FindObjectsOfType<PixelSpiderSilkBind>().Length,Is.Zero);
    }

    [Test]public void SilkQueenControlImmunityStillReceivesDamageWithTheExistingSlowFallback()
    {
        Status.ControlImmune=true;
        var cast=(IEnumerator)Call("ResolveSilkQueenCocoons",spider,enemy,Vector3.zero,1f,1f,0);Assert.That(cast.MoveNext(),Is.True);
        Assert.That(Status.Has(CombatStatusKind.Root),Is.False);Assert.That(Get(enemy,"rank3ActionSlowRatio"),Is.EqualTo(.5f));
        while(cast.MoveNext()){}Assert.That(Get(enemy,"hp"),Is.EqualTo(4500f));
    }

    [Test] public void FuneralChoosesPrimaryAndOnlyTheTwoClosestEnemiesAroundIt()
    {
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*3f;
        var near=Unit(1,3.4f);var second=Unit(1,4f);var far=Unit(1,4.6f);var ally=Unit(0,3.1f);
        using(var cast=(IDisposable)Funeral())
        {
            Assert.That(((IEnumerator)cast).MoveNext(),Is.True);
            foreach(var u in new[]{enemy,near,second})Assert.That(Get(u,"hp"),Is.EqualTo(4900f));
            foreach(var u in new[]{far,ally})Assert.That(Get(u,"hp"),Is.EqualTo(5000f));
            Assert.That(Status.Get(CombatStatusKind.Root).remaining,Is.EqualTo(1.5f));
        }
    }

    [TestCase(1f)] [TestCase(1.5f)]
    public void FuneralHasThreeHalfSecondPressesAndFiveHundredPercentSingleTargetDamage(float scale)
    {
        var cast=Funeral(scale:scale);Assert.That(cast.MoveNext(),Is.True);
        Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f-100f*scale));
        Assert.That(cast.MoveNext(),Is.True);Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f-160f*scale));
        Assert.That(cast.MoveNext(),Is.True);Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f-220f*scale));
        Assert.That(cast.MoveNext(),Is.False);Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f-500f*scale));
        Assert.That(cast.MoveNext(),Is.False);Assert.That(Get(enemy,"skillUses"),Is.Zero);
    }

    [Test] public void FuneralCapsNeighborBurstsAtTwoAndKeepsFriendlyBodiesSafe()
    {
        var second=Unit(1,.3f);var third=Unit(1,-.3f);var splashOnly=Unit(1,.9f);var ally=Unit(0,.4f);
        var cast=Funeral();while(cast.MoveNext()){}
        foreach(var u in new[]{enemy,second,third})Assert.That(Get(u,"hp"),Is.EqualTo(4300f));
        Assert.That(Get(splashOnly,"hp"),Is.EqualTo(4800f));Assert.That(Get(ally,"hp"),Is.EqualTo(5000f));
        Assert.That(((CombatStatusState)Get(splashOnly,"combatStatuses")).Has(CombatStatusKind.Root),Is.False);
    }

    [Test] public void FuneralSnapshotsSimultaneousBurstsBeforeAnyShellDies()
    {
        var second=Unit(1,.3f);var third=Unit(1,-.3f);var splashOnly=Unit(1,.9f);
        foreach(var u in new[]{enemy,second,third})Set(u,"hp",350f);
        var cast=Funeral();while(cast.MoveNext()){}
        Assert.That(Get(splashOnly,"hp"),Is.EqualTo(4800f));
    }

    [TestCase(false)] [TestCase(true)]
    public void FuneralSurvivesCasterDeathAndResetStopsRemainingPresses(bool reset)
    {
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        var driver=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/025_woongi")).GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(enemy,"spriteAnimation",driver);var cast=Funeral();cast.MoveNext();
        Assert.That(Object.FindObjectsOfType<PixelSpiderSilkBind>().Length,Is.EqualTo(1));
        if(reset){type.GetField("combatGeneration",Private).SetValue(battle,1);Call("ClearSpiderWebZones");}
        else Set(spider,"alive",false);
        while(cast.MoveNext()){}
        Assert.That(Get(enemy,"hp"),Is.EqualTo(reset?4900f:4500f));
        if(reset)Assert.That(Object.FindObjectsOfType<PixelSpiderSilkBind>().Length,Is.Zero);
    }

    [Test] public void FuneralSkipsAnEarlyDeadShellAndUsesCurrentPositionsForTheExplosion()
    {
        var second=Unit(1,.3f);var third=Unit(1,-.3f);var splashOnly=Unit(1,.9f);
        var cast=Funeral();cast.MoveNext();Set(second,"alive",false);
        ((GameObject)Get(third,"model")).transform.position=Vector3.right*10f;
        while(cast.MoveNext()){}
        Assert.That(Get(splashOnly,"hp"),Is.EqualTo(4900f));Assert.That(Get(enemy,"hp"),Is.EqualTo(4500f));
    }

    [TestCase(SpiderCombatRules.AraneaTraitId,500f,1.6f)]
    [TestCase(SpiderCombatRules.SilantiTraitId,575f,1.5f)]
    [TestCase(SpiderCombatRules.RagonTraitId,500f,1.5f)]
    [TestCase(SpiderCombatRules.IcearachneTraitId,500f,1.5f)]
    public void FuneralRetainsSpeciesCompressionAndStatusEffects(string intrinsic,float damage,float root)
    {
        ((IList)Get(spider,"activeTraitIds")).Add(intrinsic);var cast=Funeral();cast.MoveNext();
        Assert.That(Status.Get(CombatStatusKind.Root).remaining,Is.EqualTo(root));
        while(cast.MoveNext()){}
        Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f-damage));
        if(intrinsic==SpiderCombatRules.RagonTraitId)Assert.That(Status.Get(CombatStatusKind.Poison).stacks,Is.EqualTo(5));
        if(intrinsic==SpiderCombatRules.IcearachneTraitId)Assert.That(Status.Get(CombatStatusKind.Freeze).remaining,Is.EqualTo(1.25f));
    }

    [Test] public void FuneralImmuneTargetStillTakesDamageWithoutFalseBinding()
    {
        Status.ControlImmune=true;var cast=Funeral();cast.MoveNext();
        Assert.That(Status.Has(CombatStatusKind.Root),Is.False);Assert.That(Get(enemy,"rank3ActionSlowRatio"),Is.EqualTo(.5f));
        while(cast.MoveNext()){}Assert.That(Get(enemy,"hp"),Is.EqualTo(4500f));
    }

    [TestCase(2.125f)] [TestCase(3.4f)]
    public void FuneralCocoonCompressesThenParticlesTravelToTheSplashBoundary(float radius)
    {
        var driver=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/025_woongi")).GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var fx=PixelSpiderSilkBind.PlayFuneral(driver.GetComponent<SpriteRenderer>(),Color.white,()=>true);
        Advance(fx,.35f);var left=fx.transform.Find("Cocoon Left").GetComponent<SpriteRenderer>();float width=left.bounds.size.x;
        fx.Compress();Advance(fx,.1f);Assert.That(left.bounds.size.x,Is.LessThan(width*.65f));
        Advance(fx,.07f);Assert.That(left.bounds.size.x,Is.LessThan(width*.65f),"The crush must hold before recovering.");
        Advance(fx,.24f);Assert.That(left.bounds.size.x,Is.EqualTo(width).Within(.01f));
        Assert.That(left.enabled,Is.True);
        Vector3 impact=new Vector3(4f,0f,-2f);fx.Burst(radius,impact);Advance(fx,.035f);
        var particles=fx.GetComponentsInChildren<ParticleSystem>();Assert.That(particles.Length,Is.EqualTo(2));
        var buffer=new ParticleSystem.Particle[72];
        foreach(var system in particles)
        {
            int count=system.GetParticles(buffer);Assert.That(count,Is.EqualTo(72));
            float maximum=0f;
            for(int i=0;i<count;i++)maximum=Mathf.Max(maximum,Vector2.Distance(
                new Vector2(buffer[i].position.x,buffer[i].position.z),new Vector2(impact.x,impact.z)));
            Assert.That(maximum,Is.InRange(radius*.4f,radius*.8f),"Show outward travel, not an instantly expanded shape.");
        }
        Advance(fx,.07f);
        foreach(var system in particles)
        {
            int count=system.GetParticles(buffer),edge=0,brightEdge=0;
            for(int i=0;i<count;i++)
            {
                float distance=Vector2.Distance(new Vector2(buffer[i].position.x,buffer[i].position.z),new Vector2(impact.x,impact.z));
                Assert.That(distance,Is.LessThanOrEqualTo(radius+.001f));
                if(distance<radius-.001f)continue;
                edge++;if(buffer[i].GetCurrentColor(system).a>230)brightEdge++;
            }
            Assert.That(edge,Is.GreaterThanOrEqualTo(24));
            Assert.That(brightEdge,Is.GreaterThanOrEqualTo(24),"The leading fragments must arrive within .105s at full brightness.");
        }
        Object.DestroyImmediate(driver.gameObject);Advance(fx,.05f);
        Assert.That(fx!=null,Is.True,"Lethal hits must not erase their area burst.");
        Assert.That(particles[0].particleCount,Is.EqualTo(72));
        Advance(fx,.18f);Assert.That(fx==null,Is.True);
    }

    [Test] public void TheWinningCocoonBurstFinishesButAResetStillCancelsItImmediately()
    {
        var driver=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/025_woongi")).GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var renderer=driver.GetComponent<SpriteRenderer>();
        var bursting=PixelSpiderSilkBind.PlayFuneral(renderer,Color.white,()=>true);
        var binding=PixelSpiderSilkBind.PlayFuneral(renderer,Color.white,()=>true);
        var effects=(IList)type.GetField("funeralCocoons",Private).GetValue(battle);
        effects.Add(bursting);effects.Add(binding);bursting.Burst(2.125f,Vector3.zero);
        Call("ClearSpiderWebZonesAfterBattle");
        Assert.That(binding==null,Is.True);Assert.That(bursting!=null,Is.True);
        Assert.That(effects.Count,Is.EqualTo(1));Advance(bursting,.12f);
        Assert.That(bursting!=null,Is.True);
        Call("ClearSpiderWebZones");Assert.That(bursting==null,Is.True);Assert.That(effects.Count,Is.Zero);
    }

    [Test] public void TheCastersInwardPullStillFinishesWhileTheVictimCannotAct()
    {
        ((IList)Get(spider,"activeTraitIds")).Add(SpiderCombatRules.AraneaTraitId);
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*1.9f;
        Set(enemy,"target",spider);Zone();Tick(.81f);
        Assert.That(Get(enemy,"displacing"),Is.True);
        for(int i=0;i<8;i++)Call("UpdateUnit",enemy,.05f);
        Assert.That(Status.Has(CombatStatusKind.Root),Is.True);
        Assert.That(Get(enemy,"displacing"),Is.False);
        Assert.That(((GameObject)Get(enemy,"model")).transform.position.x,Is.LessThan(1.3f));
        Assert.That(Get(enemy,"basicAttackCount"),Is.EqualTo(0));
    }
}
