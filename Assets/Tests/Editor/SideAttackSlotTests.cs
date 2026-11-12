using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SideAttackSlotTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> owned=new List<GameObject>();
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private IList units;
    private MonsterDefinition definition;
    private bool previousEffectTest;
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()
    {
        random=UnityEngine.Random.state;
        previousEffectTest=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=false;
        var host=Go("Melee contact integration");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)Field("units").GetValue(battle);
        definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos");
        Assert.That(definition,Is.Not.Null);
        Field("combatBounds").SetValue(battle,new Bounds(Vector3.zero,new Vector3(28f,10f,20f)));
        Field("expeditionRun").SetValue(battle,new ExpeditionRunSave
            {runId="melee_contact",regionId=HunterRegionCatalog.FirstRegionId});
        Field("playerProfile").SetValue(battle,new PlayerProfileSave());
        Field("mlEpisodeActive").SetValue(battle,true);
        Field("battleStarted").SetValue(battle,true);
    }

    [TearDown] public void Cleanup()
    {
        for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);
        owned.Clear();CombatEffectTestConfiguration.Active=previousEffectTest;
        UnityEngine.Random.state=random;
    }

    [TestCase(0,0)] [TestCase(0,45)] [TestCase(0,90)] [TestCase(0,180)]
    [TestCase(1,0)] [TestCase(1,45)] [TestCase(1,90)] [TestCase(1,180)]
    public void AnUnobstructedMeleeActorApproachesDirectlyFromItsCurrentAngleAndAttacks(int team,int degrees)
    {
        float angle=degrees*Mathf.Deg2Rad;
        Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
        var target=Actor(1-team,Vector3.zero);
        var source=Actor(team,direction*6f);Set(source,"target",target);
        float contact=Contact(source,target),arrival=Arrival(source,target);
        for(int frame=0;frame<360;frame++)
        {
            int attacks=(int)Get(source,"basicAttackCount");
            Call("UpdateUnit",source,1f/60f);
            Assert.That(Vector3.Cross(Position(source),direction).magnitude,Is.LessThan(.035f),
                "A clear radial approach must not detour into an authored side column.");
            Assert.That(Vector3.Distance(Position(source),Position(target)),Is.GreaterThanOrEqualTo(.599f));
            if((int)Get(source,"basicAttackCount")>attacks)
                Assert.That(Vector3.Distance(Position(source),Position(target)),Is.LessThanOrEqualTo(arrival+.03f));
        }
        Assert.That((int)Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That((float)Get(target,"hp"),Is.LessThan(5000f));
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.InRange(contact-.01f,arrival+.03f));
        Assert.That(Get(source,"target"),Is.SameAs(target));
    }

    [TestCase(0)] [TestCase(1)]
    public void AnAlreadyInRangeActorKeepsItsCurrentContactInsteadOfOrbiting(int team)
    {
        var target=Actor(1-team,Vector3.zero);
        var source=Actor(team,Vector3.right*(team==0?-3f:3f));Set(source,"target",target);
        Vector3 original=Vector3.right*((Contact(source,target)+Arrival(source,target))*.5f)*(team==0?-1f:1f);
        Model(source).transform.position=original;Set(source,"lastGroundPosition",original);
        for(int frame=0;frame<180;frame++)
        {
            Call("UpdateUnit",source,1f/60f);
            Assert.That(Vector3.Distance(Position(source),original),Is.LessThan(.01f));
        }
        Assert.That((int)Get(source,"basicAttackCount"),Is.GreaterThanOrEqualTo(2));
        Assert.That((float)Get(target,"hp"),Is.LessThan(5000f));
    }

    [TestCase(0)] [TestCase(1)]
    public void SmallTargetJitterDoesNotMakeAnInRangeActorChaseNewContactPositions(int team)
    {
        var target=Actor(1-team,Vector3.zero);
        var source=Actor(team,Vector3.right*(team==0?-3f:3f));Set(source,"target",target);
        Vector3 original=Vector3.right*((Contact(source,target)+Arrival(source,target))*.5f)*(team==0?-1f:1f);
        Model(source).transform.position=original;Set(source,"lastGroundPosition",original);
        for(int frame=0;frame<300;frame++)
        {
            float sign=frame%2==0?1f:-1f;
            Model(target).transform.position=(frame%4<2?Vector3.right:Vector3.forward)*(.12f*sign);
            Call("UpdateUnit",source,1f/60f);
            Assert.That(Vector3.Distance(Position(source),original),Is.LessThan(.02f));
            Assert.That(Get(source,"target"),Is.SameAs(target));
        }
        Assert.That((int)Get(source,"basicAttackCount"),Is.GreaterThanOrEqualTo(2));
        Assert.That((float)Get(target,"hp"),Is.LessThan(5000f));
    }

    [TestCase(0)] [TestCase(1)]
    public void ADisplacedTargetIsApproachedAgainBeforeMoreBasicAttacksLand(int team)
    {
        var target=Actor(1-team,Vector3.zero);
        var source=Actor(team,Vector3.right*(team==0?-3f:3f));Set(source,"target",target);
        Vector3 original=Vector3.right*((Contact(source,target)+Arrival(source,target))*.5f)*(team==0?-1f:1f);
        Model(source).transform.position=original;Set(source,"lastGroundPosition",original);
        Call("UpdateUnit",source,1f/60f);
        int previousAttacks=(int)Get(source,"basicAttackCount");
        Assert.That(previousAttacks,Is.GreaterThan(0));
        float previousHp=(float)Get(target,"hp");
        Vector3 displaced=Vector3.forward*3f;
        Model(target).transform.position=displaced;
        float arrival=Arrival(source,target);
        for(int frame=0;frame<360;frame++)
        {
            int attacks=(int)Get(source,"basicAttackCount");
            Call("UpdateUnit",source,1f/60f);
            Assert.That(Vector3.Distance(Position(source),Position(target)),Is.GreaterThanOrEqualTo(.599f));
            if((int)Get(source,"basicAttackCount")>attacks)
                Assert.That(Vector3.Distance(Position(source),Position(target)),Is.LessThanOrEqualTo(arrival+.03f));
        }
        Assert.That((int)Get(source,"basicAttackCount"),Is.GreaterThan(previousAttacks));
        Assert.That((float)Get(target,"hp"),Is.LessThan(previousHp));
        Assert.That(Position(target),Is.EqualTo(displaced));
        Assert.That(Vector3.Distance(Position(source),original),Is.GreaterThan(1f));
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.InRange(Contact(source,target)-.01f,arrival+.03f));
    }

    [TestCase(0,30)] [TestCase(0,60)] [TestCase(0,120)]
    [TestCase(1,30)] [TestCase(1,60)] [TestCase(1,120)]
    public void TheFirstArrivalAttackKeepsMomentumAndSlidesVisiblyBeforeStopping(int team,int fps)
    {
        var target=Actor(1-team,Vector3.zero);
        var source=Actor(team,Vector3.right*(team==0?-5f:5f));Set(source,"target",target);
        float dt=1f/fps;
        bool firstAttackSeen=false,attackedWithMomentum=false;
        Vector3 firstGlidingAttack=Vector3.zero;
        for(int frame=0;frame<fps*6;frame++)
        {
            int attacks=(int)Get(source,"basicAttackCount");
            Call("UpdateUnit",source,dt);
            if(!firstAttackSeen&&(int)Get(source,"basicAttackCount")>attacks)
            {
                firstAttackSeen=true;
                attackedWithMomentum=((Vector3)Get(source,"inertiaVelocity")).sqrMagnitude>.0001f;
                firstGlidingAttack=Position(source);
            }
            Assert.That(Vector3.Distance(Position(source),Position(target)),Is.GreaterThanOrEqualTo(.599f));
        }
        Assert.That(attackedWithMomentum,Is.True,"Arrival must visibly glide while a basic attack starts.");
        Assert.That(Vector3.Distance(Position(source),firstGlidingAttack),Is.GreaterThan(.12f),
            "Attack arrival must retain enough movement to be visible instead of stopping immediately.");
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.InRange(Contact(source,target)-.01f,Arrival(source,target)+.03f));
        Assert.That((float)Get(target,"hp"),Is.LessThan(5000f));
    }

    [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
    [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
    public void MutualApproachesKeepTheirContactGapAndBothFightWithoutDrifting(int fps,bool reverseUpdate)
    {
        var source=Actor(0,Vector3.left*5f);var target=Actor(1,Vector3.right*5f);
        Set(source,"target",target);Set(target,"target",source);
        float dt=1f/fps;
        for(int frame=0;frame<fps*7;frame++)
        {
            Call("UpdateUnit",reverseUpdate?target:source,dt);
            Call("UpdateUnit",reverseUpdate?source:target,dt);
            Assert.That(Vector3.Distance(Position(source),Position(target)),Is.GreaterThanOrEqualTo(.599f));
            Assert.That(Mathf.Abs(Position(source).z),Is.LessThan(.1f));
            Assert.That(Mathf.Abs(Position(target).z),Is.LessThan(.1f));
        }
        Assert.That((int)Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That((int)Get(target,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That((float)Get(source,"hp"),Is.LessThan(5000f));
        Assert.That((float)Get(target,"hp"),Is.LessThan(5000f));
        Assert.That(Vector3.Distance(Position(source),Position(target)),
            Is.InRange(Contact(source,target)-.01f,Arrival(source,target)+.03f));
    }

    [TestCase(false)] [TestCase(true)]
    public void ATargetCycleWithNoMutualPairsStillLetsEveryActorFight(bool reverseUpdate)
    {
        object[] actors={
            Actor(0,new Vector3(-4f,0f,-3f)),Actor(1,new Vector3(4f,0f,-3f)),
            Actor(0,new Vector3(4f,0f,3f)),Actor(1,new Vector3(-4f,0f,3f))};
        for(int i=0;i<actors.Length;i++)Set(actors[i],"target",actors[(i+1)%actors.Length]);
        for(int frame=0;frame<600;frame++)
        {
            for(int i=0;i<actors.Length;i++)Call("UpdateUnit",actors[reverseUpdate?actors.Length-1-i:i],1f/60f);
            for(int i=0;i<actors.Length;i++)
            {
                Assert.That(Mathf.Abs(Position(actors[i]).z),Is.LessThanOrEqualTo(4f));
                for(int j=i+1;j<actors.Length;j++)
                    Assert.That(Vector3.Distance(Position(actors[i]),Position(actors[j])),Is.GreaterThanOrEqualTo(.599f));
            }
        }
        foreach(object actor in actors)
            Assert.That((int)Get(actor,"basicAttackCount"),Is.GreaterThan(0));
    }

    [Test]
    public void EightVersusEightKeepsPhysicalBodiesSeparateWhileEveryActorAttacks()
    {
        var actors=new List<object>();
        for(int row=0;row<8;row++)
        {
            float z=(row-3.5f)*1.3f;
            var ally=Actor(0,new Vector3(-5f,0f,z));var enemy=Actor(1,new Vector3(5f,0f,z));
            Set(ally,"target",enemy);Set(enemy,"target",ally);actors.Add(ally);actors.Add(enemy);
        }
        Assert.That(actors.Count,Is.EqualTo(16));
        for(int frame=0;frame<480;frame++)
        {
            foreach(object actor in actors)Call("UpdateUnit",actor,1f/60f);
            for(int i=0;i<actors.Count;i++)for(int j=i+1;j<actors.Count;j++)
                Assert.That(Vector3.Distance(Position(actors[i]),Position(actors[j])),Is.GreaterThanOrEqualTo(.599f));
        }
        foreach(object actor in actors)
        {
            Assert.That((int)Get(actor,"basicAttackCount"),Is.GreaterThan(0));
            Assert.That((float)Get(actor,"hp"),Is.LessThan(5000f));
        }
    }

    private object Actor(int team,Vector3 position,float size=1f)
    {
        object unit=Activator.CreateInstance(unitType,true);var model=Go("Melee contact actor");model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",Go("Melee contact shadow"));Set(unit,"id",units.Count);
        Set(unit,"stableId","melee_contact_"+units.Count);Set(unit,"definition",definition);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",20f);
        Set(unit,"attackRange",1);Set(unit,"collisionRadius",.3f);Set(unit,"combatBodyScale",size);
        Set(unit,"attackInterval",.8f);Set(unit,"speedTilesPerSecond",2f);Set(unit,"skillTimer",100f);
        Set(unit,"skillId","focus_chain");Set(unit,"targetLockTimer",2.4f);
        Set(unit,"lastGroundPosition",position);units.Add(unit);return unit;
    }
    private float Contact(object source,object target)=>(float)Call("MeleeContactDistance",source,target);
    private float Arrival(object source,object target)=>(float)Call("MeleeArrivalDistance",source,target);
    private Vector3 Position(object unit)=>Model(unit).transform.position;
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private GameObject Go(string name){var value=new GameObject(name);owned.Add(value);return value;}
    private FieldInfo Field(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private).Invoke(battle,args);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
}
