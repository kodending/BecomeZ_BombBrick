using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MeleeReengagementTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> owned=new List<GameObject>();
    private MonsterAutoBattlePrototype battle;
    private MonsterDatabase database;
    private Type unitType;
    private IList units;
    private bool previousEffectTest;
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()
    {
        random=UnityEngine.Random.state;
        previousEffectTest=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=false;
        var host=Go("Melee reengagement test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)Field("units").GetValue(battle);
        database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");Assert.That(database,Is.Not.Null);
        Field("playerProfile").SetValue(battle,new PlayerProfileSave());
        Field("expeditionRun").SetValue(battle,new ExpeditionRunSave
            {runId="melee_reengagement",regionId=HunterRegionCatalog.FirstRegionId});
        Field("combatBounds").SetValue(battle,new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        Field("mlEpisodeActive").SetValue(battle,true);Field("battleStarted").SetValue(battle,true);
    }

    [TearDown] public void Cleanup()
    {
        for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);
        owned.Clear();CombatEffectTestConfiguration.Active=previousEffectTest;
        UnityEngine.Random.state=random;
    }

    [TestCase(0)] [TestCase(1)]
    public void ASeparatedPairInsideDamageReachReapproachesTheActualContactBandBeforeBasicAttacks(int team)
    {
        float side=team==0?1f:-1f;
        object source=Actor("tino",team,Vector3.zero);
        object target=Actor("krogal",1-team,new Vector3(side*1.898f,0f,1.317f));
        Pair(source,target);
        Assert.That(Call("IsImmediatelyAttackable",source,target),Is.True);
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.GreaterThan((float)Call("MeleeArrivalDistance",source,target)));
        UpdateCheckingActualContact(source,target);
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0),
            "Authored damage reach alone must not resume a visibly separated basic attack.");
        for(int frame=0;frame<240;frame++)
        {
            UpdateCheckingActualContact(source,target);UpdateCheckingActualContact(target,source);
            AssertBodyClearance(source,target);
        }
        Assert.That(Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Get(target,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Number(source,"hp"),Is.LessThan(5000f));Assert.That(Number(target,"hp"),Is.LessThan(5000f));
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.InRange((float)Call("MeleeContactDistance",source,target)-.1f,
                (float)Call("MeleeArrivalDistance",source,target)+.1f));
    }

    [TestCase("horizontal",false)] [TestCase("horizontal",true)]
    [TestCase("diagonal",false)] [TestCase("diagonal",true)]
    public void MutualApproachFromEitherDirectionKeepsTargetsAndBothFightersDealDamage(string layout,bool reverse)
    {
        Vector3 start=layout=="horizontal"?Vector3.left*6f:new Vector3(-6f,0f,-4f);
        object source=Actor("tino",0,start),target=Actor("krogal",1,-start);Pair(source,target);
        float maximumCentroidOffset=0f;
        for(int frame=0;frame<420;frame++)
        {
            UpdateCheckingActualContact(reverse?target:source,reverse?source:target);
            UpdateCheckingActualContact(reverse?source:target,reverse?target:source);
            AssertBodyClearance(source,target);
            maximumCentroidOffset=Mathf.Max(maximumCentroidOffset,((Position(source)+Position(target))*.5f).magnitude);
            Assert.That(Get(source,"target"),Is.SameAs(target));Assert.That(Get(target,"target"),Is.SameAs(source));
        }
        Assert.That(Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Get(target,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Number(source,"hp"),Is.LessThan(5000f));Assert.That(Number(target,"hp"),Is.LessThan(5000f));
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.InRange((float)Call("MeleeContactDistance",source,target)-.1f,
                (float)Call("MeleeArrivalDistance",source,target)+.1f));
        Assert.That(maximumCentroidOffset,Is.LessThan(1f),"A mutual pursuit must not drift across the arena.");
    }

    [Test]
    public void ABasicAttackCanCommitWhileResidualGlideContinuesWithoutRestartingTheHit()
    {
        object source=Actor("tino",0,Vector3.zero),target=Actor("krogal",1,Vector3.right*5f);
        Pair(source,target);
        Model(target).transform.position=Vector3.right*((float)Call("MeleeArrivalDistance",source,target)-.05f);
        Call("BeginUnitInertia",source,Vector3.right*2f);
        Vector3 before=Position(source);
        UpdateCheckingActualContact(source,target);
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(1));
        Assert.That(Number(target,"hp"),Is.LessThan(5000f));
        Assert.That(Position(source).x,Is.GreaterThan(before.x));
        Vector3 remaining=(Vector3)Get(source,"inertiaVelocity");
        Assert.That(remaining.magnitude,Is.GreaterThan(0f),"Starting an attack must not erase the residual glide.");
        float hpAfterHit=Number(target,"hp");
        for(int frame=0;frame<6;frame++)Call("UpdateUnit",source,1f/60f);
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(1));
        Assert.That(Number(target,"hp"),Is.EqualTo(hpAfterHit),"The same attack must not be restarted by locomotion.");
        Assert.That(((Vector3)Get(source,"inertiaVelocity")).magnitude,Is.LessThan(remaining.magnitude));
        AssertBodyClearance(source,target);
    }

    [TestCase(0)] [TestCase(1)]
    public void SkillDisplacementInsideAuthoredReachStillRequiresActualReengagementBeforeAnotherBasic(int team)
    {
        float side=team==0?1f:-1f;
        object source=Actor("tino",team,Vector3.zero),target=Actor("krogal",1-team,Vector3.right*5f);
        Pair(source,target);
        float contact=(float)Call("MeleeContactDistance",source,target);
        Vector3 sourceStart=Vector3.left*(side*contact*.5f),targetStart=-sourceStart;
        Model(source).transform.position=sourceStart;Model(target).transform.position=targetStart;
        foreach(object unit in new[]{source,target})Set(unit,"lastGroundPosition",Position(unit));
        UpdateCheckingActualContact(source,target);UpdateCheckingActualContact(target,source);
        int sourceAttacks=(int)Get(source,"basicAttackCount"),targetAttacks=(int)Get(target,"basicAttackCount");
        Assert.That(sourceAttacks,Is.EqualTo(1));Assert.That(targetAttacks,Is.EqualTo(1));
        float sourceHp=Number(source,"hp"),targetHp=Number(target,"hp");
        foreach(object unit in new[]{source,target})
        {
            Set(unit,"cooldown",0f);Set(unit,"attackMovementLockTimer",0f);
            Set(unit,"velocity",Vector3.zero);Call("ClearUnitInertia",unit);
        }
        Model(source).transform.position=sourceStart+new Vector3(-side*.45f,0f,.25f);
        Model(target).transform.position=targetStart+new Vector3(side*.45f,0f,-.25f);
        Assert.That(Call("IsImmediatelyAttackable",source,target),Is.True);
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.GreaterThan((float)Call("MeleeArrivalDistance",source,target)));
        for(int frame=0;frame<180;frame++)
        {
            UpdateCheckingActualContact(source,target);UpdateCheckingActualContact(target,source);
            AssertBodyClearance(source,target);
        }
        Assert.That(Get(source,"basicAttackCount"),Is.GreaterThan(sourceAttacks));
        Assert.That(Get(target,"basicAttackCount"),Is.GreaterThan(targetAttacks));
        Assert.That(Number(source,"hp"),Is.LessThan(sourceHp));Assert.That(Number(target,"hp"),Is.LessThan(targetHp));
        Assert.That(Get(source,"target"),Is.SameAs(target));Assert.That(Get(target,"target"),Is.SameAs(source));
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.InRange((float)Call("MeleeContactDistance",source,target)-.1f,
                (float)Call("MeleeArrivalDistance",source,target)+.1f));
    }

    [Test]
    public void ADeadTargetIsReplacedAndCannotKeepTakingBasicDamage()
    {
        object source=Actor("tino",0,Vector3.zero),target=Actor("krogal",1,Vector3.right*5f);
        Pair(source,target);
        Model(target).transform.position=Vector3.right*(float)Call("MeleeContactDistance",source,target);
        UpdateCheckingActualContact(source,target);
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(1));
        object next=Actor("krogal",1,new Vector3(4f,0f,2f));
        float corpseHp=Number(target,"hp");
        Set(target,"alive",false);Set(source,"cooldown",0f);Set(source,"attackMovementLockTimer",0f);
        for(int frame=0;frame<240;frame++)
        {
            Call("UpdateUnit",source,1f/60f);
            Assert.That(Number(target,"hp"),Is.EqualTo(corpseHp));
            AssertBodyClearance(source,next);
        }
        Assert.That(Get(source,"target"),Is.SameAs(next));
        Assert.That(Get(source,"basicAttackCount"),Is.GreaterThan(1));
        Assert.That(Number(next,"hp"),Is.LessThan(5000f));
        Assert.That(Vector3.Distance(Position(source),Position(next)),Is.LessThanOrEqualTo((float)Call("AttackDistance",source,next)));
    }

    private void UpdateCheckingActualContact(object unit,object target)
    {
        int before=(int)Get(unit,"basicAttackCount");
        float distance=Vector3.Distance(Position(unit),Position(target));
        float arrival=(float)Call("MeleeArrivalDistance",unit,target);
        // The ordinary unit update owns inertia and hit slides; do not integrate them twice.
        Call("UpdateUnit",unit,1f/60f);
        if((int)Get(unit,"basicAttackCount")>before)
            Assert.That(distance,Is.LessThanOrEqualTo(arrival+.001f),
                "A new basic attack must start within the actual contact band, never at a stale skill position.");
    }

    private void Pair(object source,object target){Set(source,"target",target);Set(target,"target",source);}
    private void AssertBodyClearance(object a,object b)=>Assert.That(Vector3.Distance(Position(a),Position(b)),
        Is.GreaterThanOrEqualTo(Number(a,"collisionRadius")+Number(b,"collisionRadius")-.001f));
    private object Actor(string monster,int team,Vector3 position)
    {
        var definition=database.Get(monster);Assert.That(definition,Is.Not.Null);
        object unit=Activator.CreateInstance(unitType,true);var model=Go("Reengagement "+monster);model.transform.position=position;
        Set(unit,"id",units.Count);Set(unit,"stableId","reengagement_"+units.Count);
        Set(unit,"model",model);Set(unit,"ring",Go("Reengagement shadow"));Set(unit,"definition",definition);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",20f);
        Set(unit,"attackRange",definition.attackRange);Set(unit,"collisionRadius",definition.collisionRadius*.84f);
        Set(unit,"attackInterval",.8f);Set(unit,"speedTilesPerSecond",definition.moveSpeedTilesPerSecond);
        Set(unit,"skillTimer",100f);Set(unit,"skillId",definition.skillId);
        Set(unit,"targetLockTimer",100f);Set(unit,"personalityReconsiderTimer",100f);Set(unit,"lastGroundPosition",position);units.Add(unit);return unit;
    }
    private Vector3 Position(object unit)=>Model(unit).transform.position;
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private GameObject Go(string name){var value=new GameObject(name);owned.Add(value);return value;}
    private FieldInfo Field(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private).Invoke(battle,args);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private float Number(object unit,string name)=>(float)Get(unit,name);
    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
}
