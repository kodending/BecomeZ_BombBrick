using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class WizardMouseBarrageTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private object source,target;
    private HashSet<GameObject> existing;
    private IEnumerator cast;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Wizard Barrage Test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        var run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        source=Unit(0,Vector3.zero);target=Unit(1,Vector3.right*4f);
        Set(source,"target",target);
    }

    [TearDown]
    public void TearDown()
    {
        (cast as IDisposable)?.Dispose();
        foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
            if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
               created.transform.parent==null)Object.DestroyImmediate(created);
    }

    private object Unit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var model=new GameObject("Wizard Unit");model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",new GameObject("Wizard Ring"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_seoljwi"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",WizardMouseCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",3);
        Set(unit,"collisionRadius",0.3f);
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle)).Add(unit);
        return unit;
    }
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private void Trait(string id)=>((IList)Get(source,"activeTraitIds")).Add(id);
    private PixelStarProjectile[] Originals()=>Object.FindObjectsOfType<PixelStarProjectile>()
        .Where(p=>p.name.StartsWith("Wizard Barrage Star ")).OrderBy(p=>p.name).ToArray();
    private void StartCast()
    {
        cast=(IEnumerator)typeof(MonsterAutoBattlePrototype).GetMethod("ResolveMagicStarBarrage",Private)
            .Invoke(battle,new[]{source,GameMasterData.Skill(WizardMouseCombatRules.SkillId),1f,false,
                typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle)});
        Assert.That(cast.MoveNext(),Is.True); // preparation
        Assert.That(Originals(),Is.Empty);
    }
    private void LaunchAll()
    {
        StartCast();
        for(int i=0;i<5;i++)Assert.That(cast.MoveNext(),Is.True);
    }
    private static void Arrive(PixelStarProjectile projectile)
    {
        var callback=typeof(PixelStarProjectile).GetField("onArrive",Private);
        var action=(Action)callback.GetValue(projectile);
        callback.SetValue(projectile,null);
        action?.Invoke();
        Object.DestroyImmediate(projectile.gameObject);
    }

    private void PrepareBarrageBeforeTargetsMove(Action movement)
    {
        var type=typeof(MonsterAutoBattlePrototype);
        var requestType=type.GetNestedType("PendingSkillFocus",BindingFlags.NonPublic);
        object request=Activator.CreateInstance(requestType,true);
        requestType.GetField("unit").SetValue(request,source);
        CygnusCall("CaptureFocusedCastPlan",request);
        movement();
        var model=(GameObject)Get(source,"model");model.AddComponent<SpriteRenderer>();
        Set(source,"spriteAnimation",model.AddComponent<SpriteMonsterAnimationDriver>());
        var committing=type.GetField("committingSkill",Private);
        object previous=committing.GetValue(battle);committing.SetValue(battle,request);
        try{Assert.That(CygnusCall("BeginMagicStarBarrage",source,GameMasterData.Skill(WizardMouseCombatRules.SkillId)),Is.True);}
        finally{committing.SetValue(battle,previous);Set(source,"spriteAnimation",null);}
        var fan=(PixelWizardStarFan)Get(source,"wizardBarrageFan");
        Set(source,"wizardBarrageFan",null);Object.DestroyImmediate(fan.gameObject);
    }

    [TestCase(false)] [TestCase(true)]
    public void PreparedBarrageKeepsFiveShotsOnMovedTargetsAndIgnoresNewEntrants(bool focused)
    {
        if(focused)Trait(WizardMouseCombatRules.PatjwiTraitId);
        object outsider=Unit(1,Vector3.right*40f);Set(outsider,"hp",100f);
        PrepareBarrageBeforeTargetsMove(()=>
        {
            ((GameObject)Get(target,"model")).transform.position=Vector3.right*30f;
            ((GameObject)Get(outsider,"model")).transform.position=Vector3.right*3f;
        });
        StartCast();Assert.That(Get(source,"wizardBarrageTargets"),Is.Null);
        for(int i=0;i<5;i++)Assert.That(cast.MoveNext(),Is.True);
        var stars=Originals();Assert.That(stars.Length,Is.EqualTo(5));
        foreach(var star in stars)
        {
            Assert.That(typeof(PixelStarProjectile).GetField("target",Private).GetValue(star),
                Is.SameAs(((GameObject)Get(target,"model")).transform));
            Arrive(star);
        }
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(focused?4773.2f:4790f).Within(.01f));
        Assert.That(Hp(outsider),Is.EqualTo(100f));
    }

    [Test]
    public void UnfiredStarsRetargetMovedPlannedCandidatesButStopWhenOnlyNewEntrantsRemain()
    {
        object replacement=Unit(1,Vector3.right*5f),outsider=Unit(1,Vector3.right*40f);
        StartCast();Assert.That(cast.MoveNext(),Is.True);
        Set(target,"alive",false);Set(target,"hp",0f);Arrive(Originals()[0]);
        ((GameObject)Get(replacement,"model")).transform.position=Vector3.right*30f;
        ((GameObject)Get(outsider,"model")).transform.position=Vector3.right*3f;
        Assert.That(cast.MoveNext(),Is.True);
        var shot=Originals().Single();
        Assert.That(typeof(PixelStarProjectile).GetField("target",Private).GetValue(shot),
            Is.SameAs(((GameObject)Get(replacement,"model")).transform));
        Arrive(shot);Assert.That(Hp(replacement),Is.EqualTo(4958f));
        Set(replacement,"alive",false);Set(replacement,"hp",0f);
        Assert.That(cast.MoveNext(),Is.False);Assert.That(Originals(),Is.Empty);
        Assert.That(Hp(outsider),Is.EqualTo(5000f));
    }

    [Test]
    public void EchoKeepsItsOwnRangeChecksInsteadOfConsumingTheOriginalBarragePlan()
    {
        object outsider=Unit(1,Vector3.right*40f);
        object plan=CygnusCall("CaptureMagicStarTargets",source,
            MonsterAutoBattlePrototype.TileSize*GameMasterData.Skill(WizardMouseCombatRules.SkillId).castRangeTiles);
        Set(source,"wizardBarrageTargets",plan);
        cast=(IEnumerator)CygnusCall("ResolveMagicStarBarrage",source,
            GameMasterData.Skill(WizardMouseCombatRules.SkillId),1f,true,0);
        Assert.That(cast.MoveNext(),Is.True);
        Object.DestroyImmediate(Object.FindObjectOfType<PixelWizardStarFan>().gameObject);
        Assert.That(cast.MoveNext(),Is.True);
        ((GameObject)Get(target,"model")).transform.position=Vector3.right*30f;
        ((GameObject)Get(outsider,"model")).transform.position=Vector3.right*3f;
        for(int i=0;i<4;i++)Assert.That(cast.MoveNext(),Is.True);
        var stars=Originals();Assert.That(stars.Length,Is.EqualTo(5));
        foreach(var star in stars)Arrive(star);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(4958f));Assert.That(Hp(outsider),Is.EqualTo(4832f));
        Assert.That(Get(source,"wizardBarrageTargets"),Is.SameAs(plan));
    }

    [Test]
    public void FiveStarsDealFullRepeatedDamageOnlyAtArrival()
    {
        LaunchAll();
        var stars=Originals();
        Assert.That(stars.Length,Is.EqualTo(5));
        Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(stars[0].transform.GetChild(0).localScale.x,
            Is.EqualTo(PixelStarProjectile.VisualScale(false)*2f).Within(.0001f));
        Assert.That(stars[4].transform.GetChild(0).localScale.x,
            Is.GreaterThan(stars[0].transform.GetChild(0).localScale.x));
        foreach(var star in stars)Arrive(star);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(4790f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(5));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(210f).Within(.01f));
    }

    [Test]
    public void DeadTargetDoesNotTakeImpactAndUnfiredStarSelectsLivingEnemy()
    {
        object replacement=Unit(1,Vector3.right*5f);
        StartCast();cast.MoveNext();
        Set(target,"alive",false);Set(target,"hp",0f);
        Arrive(Originals()[0]);
        Assert.That(Hp(target),Is.Zero);
        cast.MoveNext();
        var next=Originals()[0];
        Assert.That(typeof(PixelStarProjectile).GetField("target",Private).GetValue(next),
            Is.SameAs(((GameObject)Get(replacement,"model")).transform));
        Arrive(next);
        Assert.That(Hp(replacement),Is.EqualTo(4958f).Within(.01f));
    }

    [Test]
    public void FrostStacksWaitForArrivalAndThirdHitFreezes()
    {
        Trait(WizardMouseCombatRules.SeoljwiTraitId);
        LaunchAll();
        var status=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(status.Has(CombatStatusKind.Freeze),Is.False);
        var stars=Originals();
        Arrive(stars[0]);Arrive(stars[1]);
        Assert.That(status.Has(CombatStatusKind.Freeze),Is.False);
        Arrive(stars[2]);
        Assert.That(status.Has(CombatStatusKind.Freeze),Is.True);
    }

    [Test]
    public void MilkyWayFragmentsLaunchAndDamageAfterOriginalArrival()
    {
        Trait(WizardMouseCombatRules.MilkyWayBarrageTraitId);
        object second=Unit(1,Vector3.right*5f),third=Unit(1,Vector3.right*6f);
        LaunchAll();
        Assert.That(Object.FindObjectsOfType<PixelStarProjectile>().Length,Is.EqualTo(5));
        foreach(var star in Originals())Arrive(star);
        var fragments=Object.FindObjectsOfType<PixelStarProjectile>();
        Assert.That(fragments.Length,Is.EqualTo(10));
        Assert.That(Hp(target)+Hp(second)+Hp(third),Is.EqualTo(14790f).Within(.01f));
        Assert.That(cast.MoveNext(),Is.True);
        foreach(var star in fragments)Arrive(star);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target)+Hp(second)+Hp(third),Is.EqualTo(14370f).Within(.01f));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(630f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(15));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void FragmentsChooseAtMostTwoDifferentNearbyEnemiesAndNeverReturn(int nearby)
    {
        Trait(WizardMouseCombatRules.MilkyWayBarrageTraitId);
        var neighbors=new List<object>();
        for(int i=0;i<nearby;i++)neighbors.Add(Unit(1,Vector3.right*(5f+i)));
        Unit(0,Vector3.right*4.2f);Unit(1,Vector3.right*20f);
        StartCast();cast.MoveNext();Arrive(Originals()[0]);
        var fragments=Object.FindObjectsOfType<PixelStarProjectile>();
        Assert.That(fragments.Length,Is.EqualTo(Math.Min(nearby,2)));
        var destinations=fragments.Select(p=>(Transform)typeof(PixelStarProjectile).GetField("target",Private).GetValue(p)).ToArray();
        Assert.That(destinations.Distinct().Count(),Is.EqualTo(fragments.Length));
        CollectionAssert.AreEquivalent(neighbors.Take(2).Select(u=>((GameObject)Get(u,"model")).transform),destinations);
        foreach(var fragment in fragments)Arrive(fragment);
        Assert.That(Hp(target),Is.EqualTo(4958f).Within(.01f));
    }

    [Test]
    public void SeoljwiFragmentsStackChillOnlyOnArrivalAndThirdFragmentFreezes()
    {
        Trait(WizardMouseCombatRules.MilkyWayBarrageTraitId);Trait(WizardMouseCombatRules.SeoljwiTraitId);
        object chilled=Unit(1,Vector3.right*5f);Unit(1,Vector3.right*6f);
        LaunchAll();
        var targetField=typeof(PixelStarProjectile).GetField("target",Private);
        foreach(var star in Originals().Where(p=>(Transform)targetField.GetValue(p)==((GameObject)Get(target,"model")).transform))Arrive(star);
        var status=(CombatStatusState)Get(chilled,"combatStatuses");
        var fragments=Object.FindObjectsOfType<PixelStarProjectile>().Where(p=>p.name=="Milky Way Fragment Star"&&
            (Transform)targetField.GetValue(p)==((GameObject)Get(chilled,"model")).transform).ToArray();
        Assert.That(fragments.Length,Is.EqualTo(3));
        Assert.That(status.Has(CombatStatusKind.ComboChilled),Is.False);
        Arrive(fragments[0]);Assert.That(status.Has(CombatStatusKind.ComboChilled),Is.True);
        Arrive(fragments[1]);Assert.That(status.Has(CombatStatusKind.Freeze),Is.False);
        Arrive(fragments[2]);Assert.That(status.Has(CombatStatusKind.Freeze),Is.True);
        Assert.That(status.Has(CombatStatusKind.ComboChilled),Is.False);
        Assert.That(Hp(chilled),Is.EqualTo(4874f).Within(.01f));
    }

    [Test]
    public void PatjwiFocusAppliesToOriginalsWhileFragmentsSpreadWithoutLastHitBonus()
    {
        Trait(WizardMouseCombatRules.MilkyWayBarrageTraitId);Trait(WizardMouseCombatRules.PatjwiTraitId);
        Set(target,"hp",2000f);
        object second=Unit(1,Vector3.right*5f),third=Unit(1,Vector3.right*6f);
        LaunchAll();foreach(var star in Originals())Arrive(star);
        Assert.That(Hp(target),Is.EqualTo(1773.2f).Within(.01f));
        var fragments=Object.FindObjectsOfType<PixelStarProjectile>();
        Assert.That(fragments.Length,Is.EqualTo(10));
        foreach(var fragment in fragments)Arrive(fragment);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(1773.2f).Within(.01f));
        Assert.That(Hp(second),Is.EqualTo(4790f).Within(.01f));
        Assert.That(Hp(third),Is.EqualTo(4790f).Within(.01f));
    }

    [Test]
    public void FragmentsFanAcrossTheirChordAndCurveIntoTheTargetAfterFortyFiveHundredths()
    {
        Trait(WizardMouseCombatRules.MilkyWayBarrageTraitId);
        Unit(1,Vector3.right*5f);Unit(1,Vector3.right*6f);
        StartCast();cast.MoveNext();Arrive(Originals()[0]);
        var fragment=Object.FindObjectsOfType<PixelStarProjectile>()[0];
        Assert.That(fragment.FlightDuration,Is.EqualTo(.45f));
        var type=typeof(PixelStarProjectile);
        var update=type.GetMethod("UpdateVisual",Private);
        Vector3 start=(Vector3)type.GetField("start",Private).GetValue(fragment);
        Vector3 end=(Vector3)type.GetField("lastTarget",Private).GetValue(fragment);
        type.GetField("elapsed",Private).SetValue(fragment,.25f);update.Invoke(fragment,new object[]{.5f});
        Vector3 bend=fragment.transform.GetChild(0).position-(start+end)*.5f;
        Assert.That(bend.magnitude,Is.GreaterThanOrEqualTo(.79f));
        Assert.That(Mathf.Abs(Vector3.Dot(bend.normalized,(end-start).normalized)),Is.LessThan(.001f));
        var ribbon=fragment.GetComponent<LineRenderer>();
        Assert.That(fragment.transform.GetChild(0).localScale.x,
            Is.EqualTo(PixelStarProjectile.VisualScale(false)*.85f*2f).Within(.0001f));
        Assert.That(ribbon.endWidth,Is.EqualTo(.32f).Within(.0001f));
        Assert.That(ribbon.positionCount,Is.EqualTo(16));
        Assert.That(Vector3.Distance(ribbon.GetPosition(15),fragment.transform.GetChild(0).position),Is.LessThan(.001f));
        update.Invoke(fragment,new object[]{1f});
        Assert.That(Vector3.Distance(fragment.transform.GetChild(0).position,end),Is.LessThan(.001f));
        Assert.That(fragment.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name=="Star Core"),Is.True);
    }

    [Test]
    public void DeadFragmentTargetReceivesNeitherDamageNorChillAndTheCastFinishes()
    {
        Trait(WizardMouseCombatRules.MilkyWayBarrageTraitId);Trait(WizardMouseCombatRules.SeoljwiTraitId);
        object dead=Unit(1,Vector3.right*5f);
        LaunchAll();foreach(var star in Originals())Arrive(star);
        Set(dead,"alive",false);Set(dead,"hp",0f);((CombatStatusState)Get(dead,"combatStatuses")).Clear();
        foreach(var fragment in Object.FindObjectsOfType<PixelStarProjectile>())Arrive(fragment);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(dead),Is.Zero);
        Assert.That(((CombatStatusState)Get(dead,"combatStatuses")).Has(CombatStatusKind.ComboChilled),Is.False);
    }

    [Test]
    public void RefractionStartsAtImpactAndWaitsForItsOwnArrival()
    {
        Trait(WizardMouseCombatRules.ByeoljwiTraitId);
        object reflected=Unit(1,Vector3.right*5f);
        StartCast();cast.MoveNext();
        Assert.That(Hp(reflected),Is.EqualTo(5000f));
        Arrive(Originals()[0]);
        var reflection=Object.FindObjectsOfType<PixelStarProjectile>().Single();
        Assert.That(Hp(reflected),Is.EqualTo(5000f));
        Arrive(reflection);
        Assert.That(Hp(reflected),Is.EqualTo(4975f).Within(.01f));
    }

    [Test]
    public void CombatResetInvalidatesInFlightDamage()
    {
        LaunchAll();
        var generation=typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private);
        generation.SetValue(battle,(int)generation.GetValue(battle)+1);
        foreach(var star in Originals())Arrive(star);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [Test]
    public void PatjwiKeepsFocusAndOnlyItsLastStarGetsFortyPercentBonus()
    {
        Trait(WizardMouseCombatRules.PatjwiTraitId);
        object focused=Unit(1,Vector3.right*5f);Set(focused,"hp",2000f);
        LaunchAll();
        foreach(var star in Originals())Arrive(star);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(Hp(focused),Is.EqualTo(1773.2f).Within(.01f));
    }

    [Test]
    public void PentagramWaitsForAllOriginalArrivals()
    {
        Trait(WizardMouseCombatRules.PentagramAnnihilationTraitId);
        LaunchAll();var stars=Originals();
        for(int i=0;i<4;i++)Arrive(stars[i]);
        Assert.That(cast.MoveNext(),Is.True);
        Assert.That(Hp(target),Is.EqualTo(4832f).Within(.01f));
        Arrive(stars[4]);
        Assert.That(Hp(target),Is.EqualTo(4790f).Within(.01f));
        Assert.That(cast.MoveNext(),Is.True);
        var pulses=(IEnumerator)cast.Current;
        Assert.That(pulses.MoveNext(),Is.True);
        Assert.That(Hp(target),Is.EqualTo(4690f).Within(.01f));
        Assert.That(pulses.MoveNext(),Is.True);
        Assert.That(pulses.MoveNext(),Is.False);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(4490f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(8));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(510f).Within(.01f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Airborne),Is.False);
    }

    [Test]
    public void PentagramKeepsFirstActualImpactCenterAndRechecksOccupants()
    {
        Trait(WizardMouseCombatRules.PentagramAnnihilationTraitId);
        LaunchAll();var stars=Originals();
        // A later-launched star can arrive first. Subsequent impacts must not move the field.
        var model=(GameObject)Get(target,"model");
        model.transform.position=Vector3.right*7f;Arrive(stars[3]);
        var field=Object.FindObjectOfType<PixelWizardPentagramField>();
        Assert.That(field.transform.position.x,Is.EqualTo(7f));
        model.transform.position=Vector3.right*15f;
        for(int i=0;i<5;i++)if(i!=3)Arrive(stars[i]);
        object entrant=Unit(1,Vector3.right*7f),ally=Unit(0,Vector3.right*7f);
        Assert.That(cast.MoveNext(),Is.True);var pulses=(IEnumerator)cast.Current;
        pulses.MoveNext();
        Assert.That(Hp(entrant),Is.EqualTo(4900f));Assert.That(Hp(target),Is.EqualTo(4790f));
        ((GameObject)Get(entrant,"model")).transform.position=Vector3.right*15f;
        model.transform.position=Vector3.right*7f;pulses.MoveNext();pulses.MoveNext();cast.MoveNext();
        Assert.That(Hp(entrant),Is.EqualTo(4900f));Assert.That(Hp(target),Is.EqualTo(4590f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));
        Assert.That(field.transform.position.x,Is.EqualTo(7f));
    }

    [Test]
    public void PentagramEachPulseAddsFrostToAnEnemyMissedByOriginalStars()
    {
        Trait(WizardMouseCombatRules.PentagramAnnihilationTraitId);Trait(WizardMouseCombatRules.SeoljwiTraitId);
        LaunchAll();foreach(var star in Originals())Arrive(star);
        object entrant=Unit(1,Vector3.right*5f);
        var status=(CombatStatusState)Get(entrant,"combatStatuses");
        cast.MoveNext();var pulses=(IEnumerator)cast.Current;
        pulses.MoveNext();Assert.That(status.Has(CombatStatusKind.Freeze),Is.False);
        pulses.MoveNext();Assert.That(status.Has(CombatStatusKind.Freeze),Is.False);
        pulses.MoveNext();Assert.That(status.Has(CombatStatusKind.Freeze),Is.True);
        Assert.That(Hp(entrant),Is.EqualTo(4700f));cast.MoveNext();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PentagramSurvivesCasterDeathAndResetCancelsFuturePulses(bool reset)
    {
        Trait(WizardMouseCombatRules.PentagramAnnihilationTraitId);
        LaunchAll();foreach(var star in Originals())Arrive(star);
        cast.MoveNext();var pulses=(IEnumerator)cast.Current;pulses.MoveNext();
        if(reset)
        {
            var generation=typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private);
            generation.SetValue(battle,(int)generation.GetValue(battle)+1);
        }
        else{Set(source,"alive",false);Set(source,"hp",0f);}
        while(pulses.MoveNext()){}
        Assert.That(Hp(target),Is.EqualTo(reset?4690f:4490f));
        if(reset)Assert.That(Object.FindObjectsOfType<PixelWizardPentagramField>(),Is.Empty);
    }

    [Test]
    public void PentagramDoesNotFormWithoutAnyValidOriginalImpact()
    {
        Trait(WizardMouseCombatRules.PentagramAnnihilationTraitId);
        LaunchAll();Set(target,"alive",false);Set(target,"hp",0f);
        foreach(var star in Originals())Arrive(star);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Object.FindObjectsOfType<PixelWizardPentagramField>(),Is.Empty);
    }

    private IEnumerator LaunchSupernova()
    {
        Trait(WizardMouseCombatRules.SupernovaDescentTraitId);
        StartCast();Assert.That(cast.MoveNext(),Is.True);
        var meteor=(IEnumerator)cast.Current;
        for(int i=0;i<5;i++)Assert.That(meteor.MoveNext(),Is.True);
        return meteor;
    }

    [Test]
    public void PreparedSupernovaKeepsItsMovedTargetAndUsesTheActualLandingArea()
    {
        Trait(WizardMouseCombatRules.SupernovaDescentTraitId);
        object outsider=Unit(1,Vector3.right*40f);
        PrepareBarrageBeforeTargetsMove(()=>
        {
            ((GameObject)Get(target,"model")).transform.position=Vector3.right*30f;
            ((GameObject)Get(outsider,"model")).transform.position=Vector3.right*3f;
        });
        StartCast();Assert.That(cast.MoveNext(),Is.True);var meteor=(IEnumerator)cast.Current;
        for(int i=0;i<5;i++)Assert.That(meteor.MoveNext(),Is.True);
        Assert.That(Object.FindObjectOfType<PixelWizardSupernova>().transform.position.x,Is.EqualTo(30f));
        Assert.That(meteor.MoveNext(),Is.True);Assert.That(meteor.MoveNext(),Is.True);
        Assert.That(meteor.MoveNext(),Is.False);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(4540f));Assert.That(Hp(outsider),Is.EqualTo(5000f));
    }

    [Test]
    public void SupernovaStoresFiveStarsAndDealsOneImpactWithFrostAndKnockdown()
    {
        Trait(WizardMouseCombatRules.SeoljwiTraitId);
        object second=Unit(1,Vector3.right*5f),ally=Unit(0,Vector3.right*4f);
        var meteor=LaunchSupernova();
        Assert.That(Originals(),Is.Empty);Assert.That(Hp(target),Is.EqualTo(5000f));
        var visual=Object.FindObjectOfType<PixelWizardSupernova>();
        Assert.That(visual.transform.Find("Skyward Star 1 Trail 0").localScale.x,
            Is.EqualTo(1.2f).Within(.0001f));
        Assert.That(meteor.MoveNext(),Is.True); // descent
        var falling=visual.transform.Find("Giant Meteor Star").GetComponent<SpriteRenderer>();
        float radius=MonsterAutoBattlePrototype.TileSize*WizardMouseCombatRules.SupernovaRadiusTiles;
        Assert.That(falling.sprite.bounds.size.x*falling.transform.localScale.x,
            Is.EqualTo(radius*1.25f*1.35f*.72f).Within(.0001f));
        Assert.That(meteor.MoveNext(),Is.True); // pull
        Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(meteor.MoveNext(),Is.False);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(4540f).Within(.01f));Assert.That(Hp(second),Is.EqualTo(4750f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));
        foreach(object enemy in new[]{target,second})
        {
            var status=(CombatStatusState)Get(enemy,"combatStatuses");
            Assert.That(status.Has(CombatStatusKind.Knockdown),Is.True);
            Assert.That(status.Get(CombatStatusKind.ComboChilled).stacks,Is.EqualTo(1));
        }
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(710f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
    }

    [Test]
    public void SupernovaLocksGroundPositionAndDoesNotTransferStoredDamageWhenPrimaryLeaves()
    {
        var meteor=LaunchSupernova();
        ((GameObject)Get(target,"model")).transform.position=Vector3.right*20f;
        object entrant=Unit(1,Vector3.right*4f);
        meteor.MoveNext();meteor.MoveNext();meteor.MoveNext();cast.MoveNext();
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Hp(entrant),Is.EqualTo(4750f));
        Assert.That(Object.FindObjectOfType<PixelWizardSupernova>().transform.position.x,Is.EqualTo(4f));
    }

    [Test]
    public void SupernovaPatjwiKeepsLowHealthFocusAndStoredLastStarBonus()
    {
        Trait(WizardMouseCombatRules.PatjwiTraitId);
        object focused=Unit(1,Vector3.right*5f);Set(focused,"hp",2000f);
        var meteor=LaunchSupernova();meteor.MoveNext();meteor.MoveNext();meteor.MoveNext();cast.MoveNext();
        Assert.That(Hp(focused),Is.EqualTo(1523.2f).Within(.01f));
        Assert.That(Hp(target),Is.EqualTo(4750f));
    }

    [Test]
    public void SupernovaKongjwiHealsOnceUsingActualImpactVictims()
    {
        Trait(WizardMouseCombatRules.KongjwiTraitId);Set(source,"hp",4000f);
        Unit(1,Vector3.right*5f);var meteor=LaunchSupernova();
        Assert.That(Hp(source),Is.EqualTo(4000f));
        meteor.MoveNext();meteor.MoveNext();meteor.MoveNext();cast.MoveNext();
        Assert.That(Hp(source),Is.EqualTo(4090f).Within(.01f));
    }

    [TestCase("")]
    [TestCase(WizardMouseCombatRules.MilkyWayBarrageTraitId)]
    [TestCase(WizardMouseCombatRules.PentagramAnnihilationTraitId)]
    [TestCase(WizardMouseCombatRules.SupernovaDescentTraitId)]
    public void KongjwiEverySkillHealsLowestHealthRatioAllyInsteadOfCaster(string rank3)
    {
        Trait(WizardMouseCombatRules.KongjwiTraitId);Set(source,"hp",4000f);
        object ally=Unit(0,Vector3.forward),higherRatio=Unit(0,Vector3.back);
        Set(ally,"hp",200f);Set(ally,"maxHp",2000f);
        Set(higherRatio,"hp",100f);Set(higherRatio,"maxHp",400f);
        if(rank3==WizardMouseCombatRules.SupernovaDescentTraitId)
        {
            var meteor=LaunchSupernova();
            meteor.MoveNext();meteor.MoveNext();meteor.MoveNext();cast.MoveNext();
        }
        else
        {
            if(rank3.Length>0)Trait(rank3);
            LaunchAll();foreach(var star in Originals())Arrive(star);
            if(rank3==WizardMouseCombatRules.PentagramAnnihilationTraitId)
            {
                Assert.That(cast.MoveNext(),Is.True);
                var pulses=(IEnumerator)cast.Current;
                pulses.MoveNext();pulses.MoveNext();pulses.MoveNext();
            }
            Assert.That(cast.MoveNext(),Is.False);
        }
        Assert.That(Hp(ally),Is.EqualTo(275f).Within(.01f));
        Assert.That(Hp(higherRatio),Is.EqualTo(100f));
        Assert.That(Hp(source),Is.EqualTo(4000f));
    }

    [Test]
    public void SupernovaByeoljwiRefractsOnceAndOnlyDealsRefractionOnArrival()
    {
        Trait(WizardMouseCombatRules.ByeoljwiTraitId);
        object second=Unit(1,Vector3.right*5f);
        var meteor=LaunchSupernova();meteor.MoveNext();meteor.MoveNext();meteor.MoveNext();cast.MoveNext();
        Assert.That(Hp(second),Is.EqualTo(4750f));
        var reflected=Object.FindObjectsOfType<PixelStarProjectile>().Single();
        Assert.That(reflected.name,Is.EqualTo("Supernova Refraction Star"));Arrive(reflected);
        Assert.That(Hp(second),Is.EqualTo(4725f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SupernovaSurvivesCasterDeathAndResetCancelsLandingAndShadow(bool reset)
    {
        var meteor=LaunchSupernova();meteor.MoveNext();
        if(reset)
        {
            var generation=typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private);
            generation.SetValue(battle,(int)generation.GetValue(battle)+1);
        }
        else{Set(source,"alive",false);Set(source,"hp",0f);}
        while(meteor.MoveNext()){}
        Assert.That(Hp(target),Is.EqualTo(reset?5000f:4540f));
        if(reset)Assert.That(Object.FindObjectsOfType<PixelWizardSupernova>(),Is.Empty);
    }

    [Test]
    public void CasterDeathPreservesCommittedBarrageAndInFlightDamage()
    {
        StartCast();cast.MoveNext();
        Set(source,"alive",false);Set(source,"hp",0f);
        Arrive(Originals()[0]);
        for(int i=0;i<4;i++)Assert.That(cast.MoveNext(),Is.True);
        foreach(var star in Originals())Arrive(star);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Originals(),Is.Empty);
        Assert.That(Hp(target),Is.EqualTo(4790f));
    }

    private object CygnusCall(string method,params object[] args)=>
        typeof(MonsterAutoBattlePrototype).GetMethod(method,Private).Invoke(battle,args);
    [Test]
    public void ReadyStarsReleaseFromDistinctFanPositions()
    {
        var model=(GameObject)Get(source,"model");
        var fan=PixelWizardStarFan.Play(model.transform,null,Color.magenta,null);
        typeof(PixelWizardStarFan).GetField("elapsed",Private).SetValue(fan,.25f);
        Vector3 first=fan.Release(0),last=fan.Release(4);
        Assert.That(Vector3.Distance(first,last),Is.GreaterThan(1f));
        Assert.That(fan.transform.Find("Ready Star 1").localScale.x,Is.GreaterThan(1.3f));
        Assert.That(fan.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.enabled),Is.EqualTo(3));
        var projectile=PixelStarProjectile.Play(model.transform,null,
            ((GameObject)Get(target,"model")).transform,null,Color.magenta,false);
        projectile.LaunchFrom(first,0);
        Assert.That(Vector3.Distance(projectile.transform.GetChild(0).position,first),Is.LessThan(.001f));
        Assert.That(projectile.transform.GetChild(0).localScale.x,
            Is.EqualTo(PixelStarProjectile.VisualScale(false)*2f).Within(.0001f));
        projectile.SetCritical(true);projectile.SetCritical(false);
        Assert.That(projectile.transform.GetChild(0).localScale.x,
            Is.EqualTo(PixelStarProjectile.VisualScale(false)*2f).Within(.0001f));
        Assert.That(projectile.FlightDuration,Is.InRange(.14f,.34f));
    }
}
