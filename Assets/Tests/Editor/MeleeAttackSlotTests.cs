using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MeleeAttackSlotTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private readonly List<GameObject> objects=new List<GameObject>();
    private IList units;
    private object enemy;

    [SetUp] public void Setup()
    {
        var host=Go("Melee slots test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,new Vector3(24,10,18)));
        var run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        enemy=Unit(1,Vector3.zero);
    }
    [TearDown] public void Cleanup()
    {
        foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);
        objects.Clear();
    }
    private GameObject Go(string name){var go=new GameObject(name);objects.Add(go);return go;}
    private object Unit(int team,Vector3 position,float radius=.3f)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var model=Go("Melee unit");model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",Go("Ring"));Set(unit,"id",units.Count);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"attackRange",1);Set(unit,"collisionRadius",radius);Set(unit,"skillTimer",100f);
        Set(unit,"skillId","focus_chain");Set(unit,"cooldown",100f);Set(unit,"speedTilesPerSecond",2f);
        Set(unit,"definition",Resources.LoadAll<MonsterDefinition>("MonsterData").First(d=>d.monsterId=="velos"));
        Set(unit,"targetLockTimer",2.4f);
        units.Add(unit);return unit;
    }
    private object Attacker(Vector3 p,float radius=.3f)
    {var u=Unit(0,p,radius);Set(u,"target",enemy);return u;}
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Private|BindingFlags.Static).Invoke(battle,args);
    private Vector3 Position(object unit)=>((GameObject)Get(unit,"model")).transform.position;

    [Test] public void NavigationBodySnapshotsKeepTargetExclusionsAndRefreshAfterMovementOrDeath()
    {
        var source=Attacker(Vector3.zero);
        var blocker=Unit(1,Vector3.right*2f);
        var dead=Unit(1,Vector3.zero);Set(dead,"alive",false);
        var inactive=Unit(1,Vector3.zero);((GameObject)Get(inactive,"model")).SetActive(false);
        var active=typeof(MonsterAutoBattlePrototype).GetField("navigationQueryActive",Private);
        Func<bool> blocked=()=>(bool)Call("HasUnitBlockingSegment",source,Vector3.left*4f,Vector3.right*4f);
        Assert.That(blocked(),Is.True);
        Set(blocker,"alive",false);
        Assert.That(blocked(),Is.False,"Source, current target, dead and inactive units must not block.");
        Set(blocker,"alive",true);
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*6f;
        Assert.That(Call("BeginNavigationQuery"),Is.False);
        try
        {
            Assert.That(blocked(),Is.True);
            Assert.That(Call("BeginNavigationQuery"),Is.True);
            Set(source,"target",blocker);
            Assert.That(blocked(),Is.False,"Target identity must remain live inside the body snapshot.");
            Set(source,"target",enemy);
            Assert.That(blocked(),Is.True);
        }
        finally{active.SetValue(battle,false);}
        ((GameObject)Get(blocker,"model")).transform.position=new Vector3(2,0,4);
        Assert.That(blocked(),Is.False,"Outside a query, body positions must be read live.");
        Assert.That(Call("BeginNavigationQuery"),Is.False);
        try{Assert.That(blocked(),Is.False,"A new query must see the moved body.");}
        finally{active.SetValue(battle,false);}
        ((GameObject)Get(blocker,"model")).transform.position=Vector3.right*2f;
        Set(blocker,"alive",false);
        Assert.That(Call("BeginNavigationQuery"),Is.False);
        try{Assert.That(blocked(),Is.False,"A new query must exclude a dead body.");}
        finally{active.SetValue(battle,false);}
    }

    [TestCase(0,false)] [TestCase(1,false)]
    [TestCase(0,true)] [TestCase(1,true)]
    public void ShortArrivalSegmentsCannotIgnoreANeighbourAtTheirEndpoint(int team,bool snapshot)
    {
        var source=Attacker(Vector3.zero);
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        var blocker=Unit(team,Vector3.right*.85f);
        Vector3 step=Vector3.right*.1f;
        Assert.That(Vector3.Distance(Position(source),Position(blocker)),Is.GreaterThan(
            (float)Call("BodyClearance",source,blocker,.025f)));
        if(snapshot)Assert.That(Call("BeginNavigationQuery"),Is.False);
        try
        {
            Assert.That(Call("HasUnitBlockingSegment",source,Vector3.zero,step),Is.True,
                "The entire short arrival segment used to fit inside the ignored near-origin projection.");
            Assert.That(Call("HasClearNavigationSegment",source,Vector3.zero,step),Is.False);
        }
        finally{typeof(MonsterAutoBattlePrototype).GetField("navigationQueryActive",Private).SetValue(battle,false);}
    }

    [TestCase(0,false)] [TestCase(1,false)]
    [TestCase(0,true)] [TestCase(1,true)]
    public void AnExistingAlliedSpacingOverlapAllowsOnlyNonDeepeningEscape(int team,bool snapshot)
    {
        var source=Attacker(Vector3.zero);
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        var blocker=Unit(team,Vector3.right*.8f);
        Assert.That(Vector3.Distance(Position(source),Position(blocker)),Is.LessThan(
            (float)Call("BodyClearance",source,blocker,.025f)));
        if(snapshot)Assert.That(Call("BeginNavigationQuery"),Is.False);
        try
        {
            Assert.That(Call("HasUnitBlockingSegment",source,Vector3.zero,Vector3.left*.1f),Is.False);
            Assert.That(Call("HasUnitBlockingSegment",source,Vector3.zero,Vector3.forward*.1f),Is.False);
            Assert.That(Call("HasUnitBlockingSegment",source,Vector3.zero,Vector3.right*.1f),Is.True,
                "Starting inside a spacing buffer must not make movement deeper into the body navigable.");
        }
        finally{typeof(MonsterAutoBattlePrototype).GetField("navigationQueryActive",Private).SetValue(battle,false);}
    }

    [Test] public void NestedNavigationQueriesPreserveObstacleGeometryAndRestoreTheirScope()
    {
        var bt=typeof(MonsterAutoBattlePrototype);
        var tileType=bt.GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)bt.GetField("tiles",Private).GetValue(battle);
        var active=bt.GetField("navigationQueryActive",Private);
        for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
        {
            var cell=new Vector2Int(x,z);var tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,cell);
            tileType.GetField("world").SetValue(tile,Call("GridWorld",cell,false));
            tileType.GetField("walkable").SetValue(tile,(x+z)%3!=0);tiles.Add(cell,tile);
        }
        var points=Enumerable.Range(0,50).Select(i=>new Vector3((i%10-5)*.7f,0,(i/10-2)*.7f)).ToArray();
        var expected=points.Select(point=>(Vector3)Call("ResolveFreePosition",point,.42f)).ToArray();
        Assert.That(Call("BeginNavigationQuery"),Is.False);
        try
        {
            for(int i=0;i<points.Length;i++)
                Assert.That(Call("ResolveFreePosition",points[i],.42f),Is.EqualTo(expected[i]));
            var source=Attacker(new Vector3(-5,0,1));
            object[] args={source,new Vector2Int(-2,0),new Vector2Int(-1,0),null,0f,false,null,null,null};
            Call("TryFindGridRoute",args);
            Assert.That(active.GetValue(battle),Is.True,"An inner route must preserve its caller's snapshot.");
        }
        finally{active.SetValue(battle,false);}
        Assert.That(active.GetValue(battle),Is.False,"The navigation query must restore its snapshot scope.");
        foreach(var point in points)
            Assert.That(Call("ResolveFreePosition",point,.42f),Is.EqualTo(expected[Array.IndexOf(points,point)]));
    }

    [Test] public void MeleeGridSearchChecksItsFirstSegmentFromTheActualOffGridOrigin()
    {
        var bt=typeof(MonsterAutoBattlePrototype);
        var tileType=bt.GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)bt.GetField("tiles",Private).GetValue(battle);
        for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
        {
            var cell=new Vector2Int(x,z);var tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,cell);
            tileType.GetField("world").SetValue(tile,Call("GridWorld",cell,false));
            tileType.GetField("walkable").SetValue(tile,true);tiles.Add(cell,tile);
        }
        ((GameObject)Get(enemy,"model")).transform.position=new Vector3(10,0,8);
        Vector3 origin=(Vector3)Call("GridWorld",new Vector2Int(0,1),false)*.4f;
        var source=Attacker(origin,.05f);
        Vector3 next=(Vector3)Call("GridWorld",new Vector2Int(1,0),false);
        Unit(1,(origin+next)*.5f,.05f);
        Assert.That(Call("MeleeSlotSegmentClear",source,enemy,Vector3.zero,next),Is.True);
        Assert.That(Call("MeleeSlotSegmentClear",source,enemy,origin,next),Is.False);
        object[] args={source,Vector2Int.zero,new Vector2Int(2,0),null,0f,false,enemy,null,null};
        Assert.That(Call("TryFindGridRoute",args),Is.True);Assert.That(args[5],Is.True);
        Vector3 previous=origin;
        foreach(Vector3 point in (List<Vector3>)args[3])
        {Assert.That(Call("MeleeSlotSegmentClear",source,enemy,previous,point),Is.True);previous=point;}
    }

    [Test]
    public void ObstacleApproachReachesActualAttackDistanceUsingClearRouteSegments()
    {
        var unit=Attacker(new Vector3(-6f,0f,-1.5f));
        var bt=typeof(MonsterAutoBattlePrototype);
        var tileType=bt.GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)bt.GetField("tiles",Private).GetValue(battle);
        Bounds bounds=(Bounds)bt.GetField("combatBounds",Private).GetValue(battle);
        for(int x=-10;x<=10;x++)for(int z=-10;z<=10;z++)
        {
            var cell=new Vector2Int(x,z);Vector3 world=(Vector3)Call("GridWorld",cell,false);
            if(!MonsterAutoBattlePrototype.NavigationPointInsideBounds(world,bounds))continue;
            var tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,cell);tileType.GetField("world").SetValue(tile,world);
            tileType.GetField("walkable").SetValue(tile,true);tiles.Add(cell,tile);
        }
        Vector3 wallCenter=(Position(unit)+Position(enemy))*.5f;
        var wallCell=(Vector2Int)Call("NearestGridCoordinate",wallCenter);
        object wall=tiles[wallCell];Assert.That(wall,Is.Not.Null);
        tileType.GetField("world").SetValue(wall,wallCenter);
        tileType.GetField("walkable").SetValue(wall,false);
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,Position(unit),
            Call("ClosestMeleeAttackPosition",unit,enemy)),Is.False,
            "The wall must actually obstruct the closest melee approach before testing its detour.");
        Assert.That(Call("BuildPursuitRoute",unit,enemy),Is.True);
        var route=(List<Vector3>)Get(unit,"route");Assert.That(route,Is.Not.Empty);
        Vector3 destination=route.Last();
        Assert.That(RouteLength(Position(unit),route),Is.GreaterThan(Vector3.Distance(Position(unit),destination)+.05f),
            "The obstacle must require a detour rather than a direct movement segment.");
        Vector3 previous=Position(unit);
        foreach(Vector3 point in route)
        {Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,previous,point),Is.True);previous=point;}
        Assert.That(Vector3.Distance(destination,Position(enemy)),Is.LessThanOrEqualTo((float)Call("AttackDistance",unit,enemy)));
        Assert.That(Vector3.Distance(destination,Position(enemy)),Is.GreaterThanOrEqualTo(.6f));
    }

    private static float RouteLength(Vector3 origin,IEnumerable<Vector3> route)
    {
        float distance=0f;
        foreach(Vector3 point in route){distance+=Vector3.Distance(origin,point);origin=point;}
        return distance;
    }

    [TestCase(false)] [TestCase(true)]
    public void TheFinalAttackApproachLegIsNotSkippedAtTheCoarseGridWaypointTolerance(bool existingRoute)
    {
        var unit=Attacker(Vector3.left*5f);
        float arrival=(float)Call("MeleeArrivalDistance",unit,enemy);
        Vector3 destination=Vector3.left*arrival;
        Vector3 origin=destination+Vector3.left*.3f;
        ((GameObject)Get(unit,"model")).transform.position=origin;
        var route=(List<Vector3>)Get(unit,"route");
        if(existingRoute)
        {
            route.Add(destination);Set(unit,"detourTarget",enemy);
            Set(unit,"detourTimer",3f);Set(unit,"repathTimer",.2f);
        }
        Assert.That(Call("HasClearNavigationSegment",unit,origin,destination),Is.True);
        Assert.That(Vector3.Distance(origin,Position(enemy)),Is.GreaterThan(arrival));
        // A blocked direct approach can still have a short, clear final detour leg.
        Vector3 direction=(Vector3)Call("GetNavigationDirection",unit,destination-origin,true);
        Assert.That(direction.magnitude,Is.GreaterThan(.2f),
            "The last .3m of approach must not disappear under the .544m grid-waypoint tolerance.");
        Assert.That(Vector3.Dot(direction,destination-origin),Is.GreaterThan(0f));
        Assert.That(route,Is.Not.Empty);
        Assert.That(route.Last(),Is.EqualTo(destination));
        int index=(int)Get(unit,"routeIndex");
        Assert.That(index,Is.LessThan(route.Count));
        Assert.That(MonsterAutoBattlePrototype.ShouldWaitForOpenPath(true,index,route.Count),Is.False,
            "A fighter outside contact range must keep walking the reachable final leg instead of waiting forever.");
    }

    [TestCase(false)] [TestCase(true)]
    public void AlliesAttackingDifferentTargetsApproachWithoutBodyOverlapOrChangingDamageReach(bool reverse)
    {
        Set(enemy,"collisionRadius",.2f);
        var other=Unit(1,Vector3.forward*1.1f,.2f);
        var a=Attacker(Vector3.left*5f);var b=Attacker(new Vector3(-5f,0f,2.5f));Set(b,"target",other);
        if(reverse)foreach(object actor in units)
            Set(actor,"team",Enum.ToObject(unitType.GetField("team").FieldType,actor==enemy||actor==other?0:1));
        for(int frame=0;frame<300;frame++)
        {
            Call("UpdateUnit",a,1f/60f);Call("UpdateUnit",b,1f/60f);
            Assert.That(Vector3.Distance(Position(a),Position(b)),Is.GreaterThanOrEqualTo(.599f));
        }
        foreach(object unit in new[]{a,b})
        {
            object target=Get(unit,"target");
            Assert.That(Vector3.Distance(Position(unit),Position(target)),Is.LessThanOrEqualTo((float)Call("AttackDistance",unit,target)));
            Assert.That((float)Call("AttackDistance",unit,target),
                Is.EqualTo(MonsterAutoBattlePrototype.AttackDistanceForRange(1,.3f,.2f)));
        }
        Assert.That(Get(a,"target"),Is.SameAs(enemy));Assert.That(Get(b,"target"),Is.SameAs(other));
    }

    [Test] public void AFreeContactPointRemainsReachableWhenItsNearestGridCellIsCrowded()
    {
        var tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)typeof(MonsterAutoBattlePrototype).GetField("tiles",Private).GetValue(battle);
        for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
        {
            var cell=new Vector2Int(x,z);var tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,cell);
            tileType.GetField("world").SetValue(tile,Call("GridWorld",cell,false));
            tileType.GetField("walkable").SetValue(tile,true);tiles.Add(cell,tile);
        }
        ((GameObject)Get(enemy,"model")).transform.position=new Vector3(.286f,0f,.55f);
        var waiting=Attacker(new Vector3(-6f,0f,.55f));
        Vector3 destination=Position(enemy)+Vector3.right*(float)Call("MeleeContactDistance",waiting,enemy);
        var nearest=(Vector2Int)Call("NearestGridCoordinate",destination);
        Unit(0,(Vector3)Call("GridWorld",nearest,false));
        Assert.That(Call("IsTemporarilyOccupied",waiting,nearest),Is.True);
        Assert.That(Call("IsOpenAttackPosition",waiting,enemy,destination),Is.True);
        Assert.That(Call("MeleeSlotSegmentClear",waiting,enemy,Position(waiting),destination),Is.False);
        object[] args={waiting,enemy,new[]{destination},null};
        Assert.That(Call("TryMeleeSlotRoute",args),Is.True,
            "A free contact point must use a reachable approach cell, not wait behind the occupied cell.");
        Vector3 previous=Position(waiting);previous.y=0;
        foreach(Vector3 point in (IEnumerable<Vector3>)args[3])
        {
            Assert.That(Call("MeleeSlotSegmentClear",waiting,enemy,previous,point),Is.True);
            previous=point;
        }
        Assert.That(previous,Is.EqualTo(destination));
    }
    [Test] public void NormalUpdateBringsTwoAlliesIntoActualAttackRangeWithoutOverlapping()
    {
        var a=Attacker(new Vector3(-5,0,1));var b=Attacker(new Vector3(-5,0,-1));
        for(int frame=0;frame<240;frame++)
        {
            Call("UpdateUnit",a,.025f);Call("UpdateUnit",b,.025f);
            Assert.That(Vector3.Distance(Position(a),Position(b)),Is.GreaterThanOrEqualTo(.599f));
        }
        Assert.That(Position(a).magnitude,Is.LessThanOrEqualTo((float)Call("AttackDistance",a,enemy)));
        Assert.That(Position(b).magnitude,Is.LessThanOrEqualTo((float)Call("AttackDistance",b,enemy)));
        Assert.That(Get(a,"target"),Is.SameAs(enemy));Assert.That(Get(b,"target"),Is.SameAs(enemy));
    }

    [TestCase(false)] [TestCase(true)]
    public void MeleeApproachReachesItsContactDistanceAndDealsBasicAttackDamage(bool reverse)
    {
        var unit=Attacker(Vector3.left*5f);Set(unit,"speedTilesPerSecond",.5f);
        if(reverse)foreach(object actor in units)
            Set(actor,"team",Enum.ToObject(unitType.GetField("team").FieldType,actor==enemy?0:1));
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        float reach=(float)Call("AttackDistance",unit,enemy);
        for(int frame=0;frame<360;frame++)Call("UpdateUnit",unit,1f/60f);
        float distance=Vector3.Distance(Position(unit),Position(enemy));
        Assert.That(distance,Is.LessThanOrEqualTo(reach));
        Assert.That(distance,Is.InRange((float)Call("MeleeContactDistance",unit,enemy)-.1f,
            (float)Call("MeleeArrivalDistance",unit,enemy)+.1f));
        Assert.That(Call("IsImmediatelyAttackable",unit,enemy),Is.True);
        Set(unit,"damage",40f);Set(unit,"attackInterval",1f);Set(unit,"cooldown",0f);
        float hp=(float)Get(enemy,"hp");var random=UnityEngine.Random.state;
        try
        {
            Call("UpdateUnit",unit,1f/60f);
            Assert.That(Get(unit,"basicAttackCount"),Is.EqualTo(1));
            Assert.That((float)Get(enemy,"hp"),Is.LessThan(hp));
        }
        finally{UnityEngine.Random.state=random;}
    }

    [Test] public void WidenedMeleeReachStillRejectsTargetsOutsideItsBoundary()
    {
        var unit=Attacker(Vector3.left*5f);
        float reach=(float)Call("AttackDistance",unit,enemy);
        ((GameObject)Get(unit,"model")).transform.position=Vector3.left*(reach-.04f);
        Assert.That(Call("IsImmediatelyAttackable",unit,enemy),Is.True);
        ((GameObject)Get(unit,"model")).transform.position=Vector3.left*(reach+.001f);
        Assert.That(Call("IsImmediatelyAttackable",unit,enemy),Is.False);
    }

    [TestCase(30,1,.5208f)] [TestCase(60,1,.5208f)] [TestCase(120,1,.5208f)]
    [TestCase(30,1,.588f)] [TestCase(60,1,.588f)] [TestCase(120,1,.588f)]
    [TestCase(30,3,.5208f)] [TestCase(60,3,.5208f)] [TestCase(120,3,.5208f)]
    [TestCase(30,3,.588f)] [TestCase(60,3,.588f)] [TestCase(120,3,.588f)]
    public void InertialApproachEntersStrictAttackRangeWithLargeBodies(int fps,int range,float radius)
    {
        Set(enemy,"collisionRadius",.5208f);
        var unit=Attacker(Vector3.left*9f,radius);Set(unit,"attackRange",range);
        float dt=1f/fps;
        for(int frame=0;frame<fps*5;frame++)
        {
            Call("UpdateUnit",unit,dt);
        }
        float distance=Vector3.Distance(Position(unit),Position(enemy));
        float approach=(float)Call("CombatApproachDistance",unit,enemy);
        float attack=(float)Call("AttackDistance",unit,enemy);
        // Enlarged bodies must still enter authored damage reach without extending it.
        Assert.That(distance,Is.LessThanOrEqualTo(approach+.0001f),
            "Movement must reach actual attack range rather than stop beyond its boundary.");
        Assert.That(distance,Is.LessThanOrEqualTo(attack+.0001f));
        Assert.That(Call("IsImmediatelyAttackable",unit,enemy),Is.True);
        Assert.That(((Vector3)Get(unit,"velocity")).magnitude,Is.LessThan(.001f));
        Assert.That(Get(unit,"currentAction"),Is.EqualTo("recover"));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void MutuallyClosingLargeBodiesKeepTheirContactSpacingAndBothDealBasicDamage(int fps)
    {
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*6f;
        Set(enemy,"collisionRadius",.5208f);
        var unit=Attacker(Vector3.left*6f,.588f);Set(enemy,"target",unit);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        foreach(object actor in new[]{unit,enemy})
        {Set(actor,"cooldown",0f);Set(actor,"attackInterval",.8f);Set(actor,"damage",20f);}
        float dt=1f/fps,reach=(float)Call("AttackDistance",unit,enemy);
        for(int frame=0;frame<fps*5;frame++)
        {
            Call("UpdateUnit",unit,dt);Call("UpdateUnit",enemy,dt);
            Assert.That(Vector3.Distance(Position(unit),Position(enemy)),Is.GreaterThanOrEqualTo(.588f+.5208f-.001f));
        }
        float distance=Vector3.Distance(Position(unit),Position(enemy));
        Assert.That(distance,Is.LessThanOrEqualTo(reach));
        Assert.That(distance,Is.EqualTo((float)Call("MeleeContactDistance",unit,enemy)).Within(.12f));
        Assert.That(Get(unit,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Get(enemy,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That((float)Get(unit,"hp"),Is.LessThan(5000f));Assert.That((float)Get(enemy,"hp"),Is.LessThan(5000f));
    }

    [TestCase(true)] [TestCase(false)]
    public void OneSearchKeepsApproachPreferenceAndFallsBackAcrossDisconnectedCells(bool reachablePreferred)
    {
        var tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)typeof(MonsterAutoBattlePrototype).GetField("tiles",Private).GetValue(battle);
        Vector2Int start=Vector2Int.zero,preferred=new Vector2Int(2,0),fallback=new Vector2Int(0,2);
        for(int x=-20;x<=20;x++)for(int y=-20;y<=20;y++)
        {
            var coordinate=new Vector2Int(x,y);var tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,coordinate);
            tileType.GetField("world").SetValue(tile,Call("GridWorld",coordinate,false));
            tileType.GetField("walkable").SetValue(tile,
                x==0&&y>=0&&y<=2||coordinate==preferred||
                reachablePreferred&&y==0&&x>=0&&x<=2);
            tiles.Add(coordinate,tile);
        }
        ((GameObject)Get(enemy,"model")).transform.position=new Vector3(10,0,8);
        var unit=Attacker((Vector3)Call("GridWorld",start,false));
        var goals=new[]{preferred,fallback};
        object[] args={unit,start,preferred,null,0f,false,null,goals,null};
        Assert.That(Call("TryFindGridRoute",args),Is.True);
        Assert.That(args[5],Is.True);
        Vector2Int expected=reachablePreferred?preferred:fallback;
        var route=(List<Vector3>)args[3];
        Assert.That(route.Last(),Is.EqualTo(Call("NavigationWorld",unit,expected)));
        var retained=route.ToArray();
        object[] single={unit,start,expected,null,0f,false,null,null,null};
        Assert.That(Call("TryFindGridRoute",single),Is.True);
        Assert.That(args[4],Is.EqualTo(single[4]).Within(.0001f));
        Assert.That(route,Is.EqualTo(retained),"The next search must not overwrite an earlier route.");
        if(reachablePreferred)
        {
            tileType.GetField("walkable").SetValue(tiles[new Vector2Int(1,0)],false);
            object[] changed={unit,start,preferred,null,0f,false,null,goals,null};
            Assert.That(Call("TryFindGridRoute",changed),Is.True);
            Assert.That(((List<Vector3>)changed[3]).Last(),Is.EqualTo(Call("NavigationWorld",unit,fallback)),
                "A later query must see obstacles added after the previous query.");
        }
        Assert.That(typeof(MonsterAutoBattlePrototype).GetField("navigationQueryActive",Private).GetValue(battle),Is.False);
    }

    [Test]
    public void MeleeSearchChoosesTheShortestTotalApproachInsteadOfTheFirstPreferredGoal()
    {
        ((GameObject)Get(enemy,"model")).transform.position=new Vector3(10f,0f,8f);
        Vector2Int start=Vector2Int.zero,preferred=new Vector2Int(3,0),shorter=new Vector2Int(0,1);
        Vector3 origin=(Vector3)Call("GridWorld",start,false),destination=new Vector3(0f,0f,4f);
        var unit=Attacker(origin);
        object[] args={unit,start,preferred,null,0f,false,enemy,new[]{preferred,shorter},destination};
        Assert.That(Call("TryFindGridRoute",args),Is.True);Assert.That(args[5],Is.True);
        var route=(List<Vector3>)args[3];
        Assert.That(route.Last(),Is.EqualTo(Call("NavigationWorld",unit,shorter)),
            "An earlier preferred cell must not override a shorter reachable approach.");
        Assert.That((float)args[4],Is.EqualTo(RouteLength(origin,route)+
            Vector3.Distance(route.Last(),destination)).Within(.0001f),
            "Approach costs must include the last segment to the real attack position.");
    }

    [Test]
    public void MeleePathLengthDoesNotReturnToTheOriginalDeploymentRow()
    {
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,new Vector3(80f,10f,40f)));
        ((GameObject)Get(enemy,"model")).transform.position=new Vector3(35f,0f,-15f);
        Vector2Int start=new Vector2Int(0,3),goal=new Vector2Int(10,3);
        Vector3 origin=(Vector3)Call("GridWorld",start,false);
        var unit=Attacker(origin);
        float direct=Vector3.Distance(origin,(Vector3)Call("NavigationWorld",unit,goal));
        foreach(int initialRow in new[]{0,3})
        {
            Set(unit,"initialTile",new Vector2Int(0,initialRow));
            object[] args={unit,start,goal,null,0f,false,enemy,null,null};
            Assert.That(Call("TryFindGridRoute",args),Is.True);Assert.That(args[5],Is.True);
            var route=(List<Vector3>)args[3];
            Assert.That(RouteLength(origin,route),Is.EqualTo(direct).Within(.001f),
                "A displaced fighter must use actual route length rather than return to its deployment row.");
            Assert.That((float)args[4],Is.EqualTo(direct).Within(.001f));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void MeleeDetourSkipsRedundantCornersOnlyWhenTheWholeShortcutIsClear(bool blocked)
    {
        Vector3 origin=Vector3.left*5f;
        var unit=Attacker(origin);
        Vector3 destination=Vector3.left*(float)Call("MeleeArrivalDistance",unit,enemy);
        var route=(List<Vector3>)Get(unit,"route");
        route.Add(origin+Vector3.forward*2f);
        route.Add(destination+Vector3.forward*2f);
        route.Add(destination);
        Set(unit,"detourTarget",enemy);Set(unit,"detourTimer",3f);
        if(blocked)Unit(0,new Vector3(-3.2f,0f,0f));
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,destination),Is.EqualTo(!blocked));
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,route[1]),Is.True,
            "The second corner remains directly reachable even when the final contact point is blocked.");
        Vector3 direction=(Vector3)Call("GetNavigationDirection",unit,destination-origin,true);
        int expected=blocked?1:2;
        Assert.That(Get(unit,"routeIndex"),Is.EqualTo(expected));
        Assert.That(direction,Is.EqualTo(route[expected]-origin));
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,route[expected]),Is.True,
            "Lookahead must keep the complete shortcut outside the target and intervening bodies.");
    }

    [TestCase(false)] [TestCase(true)]
    public void AnOpenClosestApproachDoesNotDiscardAValidBypassToAnotherContactPoint(bool reverse)
    {
        Vector3 origin=Vector3.left*5f,corner=origin+Vector3.forward*3f;
        var unit=Attacker(origin);
        Vector3 destination=Vector3.right*(float)Call("MeleeArrivalDistance",unit,enemy);
        Unit(0,new Vector3(-2.5f,0f,1.5f));
        if(reverse)foreach(object actor in units)
            Set(actor,"team",Enum.ToObject(unitType.GetField("team").FieldType,actor==enemy?0:1));
        var route=(List<Vector3>)Get(unit,"route");
        route.Add(corner);route.Add(Vector3.forward*3f);route.Add(destination);
        Set(unit,"detourTarget",enemy);Set(unit,"repathTimer",.1f);
        Vector3 direct=(Vector3)Call("ClosestMeleeAttackPosition",unit,enemy)-origin;
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,origin+direct),Is.True);
        Assert.That(Call("IsOpenAttackPosition",unit,enemy,destination),Is.True);
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,route[1]),Is.False);
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,destination),Is.False);

        Assert.That(Call("GetNavigationDirection",unit,direct,false),Is.EqualTo(corner-origin),
            "A clear closest point must not reset a valid route to a different contact point every frame.");
        Assert.That(route.Last(),Is.EqualTo(destination));
        Assert.That(Get(unit,"routeIndex"),Is.EqualTo(0));
    }

    [TestCase(false)] [TestCase(true)]
    public void ACollisionClearedBypassIsNotSteeredIntoTheCrowdByNearbySeparation(bool reverse)
    {
        Vector3 origin=Vector3.left*5f,corner=origin+Vector3.forward*3f;
        var unit=Attacker(origin);
        Unit(0,new Vector3(-3.2f,0f,0f));
        Unit(0,new Vector3(-2.5f,0f,1.5f));
        var neighbour=Unit(1,origin+Vector3.left*.75f);
        if(reverse)foreach(object actor in units)
            Set(actor,"team",Enum.ToObject(unitType.GetField("team").FieldType,actor==enemy||actor==neighbour?0:1));
        var route=(List<Vector3>)Get(unit,"route");
        route.Add(corner);route.Add(Vector3.forward*3f);
        route.Add(Vector3.right*(float)Call("MeleeArrivalDistance",unit,enemy));
        Set(unit,"detourTarget",enemy);Set(unit,"detourTimer",3f);Set(unit,"repathTimer",.1f);
        Set(unit,"decisionTimer",10f);Set(unit,"lastGroundPosition",origin);
        Vector3 direct=(Vector3)Call("ClosestMeleeAttackPosition",unit,enemy);
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,direct),Is.False);
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,corner),Is.True);
        Assert.That(((Vector3)Call("CalculateSeparation",unit,1f)).x,Is.GreaterThan(.01f),
            "The neighbouring opponent must actually introduce a sideways steering force.");

        Call("UpdateUnit",unit,1f/60f);

        Vector3 movement=Position(unit)-origin;
        Assert.That(movement.z,Is.GreaterThan(.0001f));
        Assert.That(movement.x,Is.EqualTo(0f).Within(.00001f),
            "Follow the collision-cleared corner instead of adding separation toward the blocking crowd.");
        Assert.That(Get(unit,"target"),Is.SameAs(enemy));
        Assert.That(Get(unit,"routeIndex"),Is.EqualTo(0));
    }

    [Test]
    public void AShortMeleeBypassCornerIsReachedBeforeAnUnsafeNextSegment()
    {
        Vector3 origin=Vector3.left*5f;
        var unit=Attacker(origin);
        Vector3 corner=origin+Vector3.forward*.3f;
        Vector3 destination=Vector3.left*(float)Call("MeleeArrivalDistance",unit,enemy)+Vector3.forward*.3f;
        Unit(1,new Vector3(-4.4f,0f,-.15f),.05f);
        var route=(List<Vector3>)Get(unit,"route");route.Add(corner);route.Add(destination);
        Set(unit,"detourTarget",enemy);Set(unit,"repathTimer",.1f);
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,corner),Is.True);
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,corner,destination),Is.True);
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,destination),Is.False);
        Assert.That(Call("GetNavigationDirection",unit,destination-origin,true),Is.EqualTo(corner-origin));
        Assert.That(Get(unit,"routeIndex"),Is.EqualTo(0),
            "A .3m body-bypass corner must not be skipped at the .544m coarse-grid tolerance.");
    }

    [Test]
    public void ADisplacedMeleeTargetRefreshesTheRouteAtTheRepathInterval()
    {
        Vector3 origin=Vector3.left*5f;
        var unit=Attacker(origin);
        Vector3 oldDestination=(Vector3)Call("ClosestMeleeAttackPosition",unit,enemy);
        var route=(List<Vector3>)Get(unit,"route");route.Add(oldDestination);
        Set(unit,"detourTarget",enemy);Set(unit,"repathTimer",.1f);
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*2f;
        Vector3 desired=(Vector3)Call("ClosestMeleeAttackPosition",unit,enemy)-origin;
        Call("GetNavigationDirection",unit,desired,true);
        Assert.That(route.Last(),Is.EqualTo(oldDestination),
            "A moving target must not rebuild the approach on every frame.");
        Set(unit,"repathTimer",0f);
        Vector3 direction=(Vector3)Call("GetNavigationDirection",unit,desired,true);
        Assert.That(Get(unit,"target"),Is.SameAs(enemy));
        Assert.That((float)Get(unit,"repathTimer"),Is.EqualTo(.2f));
        Assert.That(route.Last(),Is.Not.EqualTo(oldDestination));
        Assert.That(Vector3.Distance(route.Last(),Position(enemy)),
            Is.LessThanOrEqualTo((float)Call("MeleeArrivalDistance",unit,enemy)+.001f));
        Assert.That(direction.magnitude,Is.GreaterThan(.1f));
    }

    [TestCase(false)] [TestCase(true)]
    public void AnOccupiedMeleeEndpointRefreshesBeforeFollowingAnOldDetour(bool allPositionsBlocked)
    {
        Vector3 origin=Vector3.left*5f;
        var unit=Attacker(origin);
        float arrival=(float)Call("MeleeArrivalDistance",unit,enemy);
        Vector3 oldDestination=Vector3.right*arrival,corner=Vector3.forward*4f;
        Unit(0,Vector3.left*3f);
        if(allPositionsBlocked)
            for(int i=0;i<16;i++)
                Unit(0,Quaternion.AngleAxis(i*22.5f,Vector3.up)*oldDestination);
        else Unit(0,oldDestination);
        var route=(List<Vector3>)Get(unit,"route");
        route.Add(origin+corner);route.Add(corner);route.Add(oldDestination);
        Set(unit,"detourTarget",enemy);Set(unit,"repathTimer",.1f);
        Vector3 desired=(Vector3)Call("ClosestMeleeAttackPosition",unit,enemy)-origin;
        Assert.That(Call("IsOpenAttackPosition",unit,enemy,oldDestination),Is.False);
        Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,corner),Is.True,
            "A clear intermediate waypoint must not hide an unusable final attack position.");
        Assert.That(Call("GetNavigationDirection",unit,desired,true),Is.EqualTo(corner-origin));
        Assert.That(route.Last(),Is.EqualTo(oldDestination),
            "Endpoint changes must respect the existing repath interval.");

        Set(unit,"repathTimer",0f);
        Vector3 direction=(Vector3)Call("GetNavigationDirection",unit,desired,true);
        if(allPositionsBlocked)
        {
            Assert.That(route,Has.Count.EqualTo(1),"Discard the old detour and approach the crowd's clear edge.");
            Assert.That(direction.x,Is.GreaterThan(.1f));
            Assert.That(Vector3.Distance(route[0],Position(enemy)),Is.LessThan(Vector3.Distance(origin,Position(enemy))));
            Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,origin,route[0]),Is.True);
        }
        else
        {
            Assert.That(route.Last(),Is.Not.EqualTo(oldDestination),
                "Repath before continuing toward the wall when another body occupies the endpoint.");
            Assert.That(Call("IsOpenAttackPosition",unit,enemy,route.Last()),Is.True);
            Assert.That(Vector3.Distance(route.Last(),Position(enemy)),Is.LessThanOrEqualTo(arrival+.001f));
            Assert.That(direction.sqrMagnitude,Is.GreaterThan(.01f));
        }
        Assert.That(Get(unit,"target"),Is.SameAs(enemy));
        Assert.That((float)Get(unit,"repathTimer"),Is.EqualTo(.2f));
    }

    [TestCase(0f)] [TestCase(-1f)] [TestCase(1f)]
    public void ABodyBypassStaysNearTheBodyAndUsesTheOpenSide(float blockedSide)
    {
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*5f;
        var unit=Attacker(Vector3.left*5f);
        var blocker=Unit(0,Vector3.zero);
        if(blockedSide!=0f)Unit(0,Vector3.forward*(blockedSide*1.4f));
        Vector3 origin=Position(unit),destination=(Vector3)Call("ClosestMeleeAttackPosition",unit,enemy);
        float clearance=(float)Call("BodyClearance",unit,blocker,.025f);
        object[] args={unit,enemy,new[]{destination},null};
        Assert.That(Call("TryMeleeSlotRoute",args),Is.True);
        var route=(List<Vector3>)args[3];Assert.That(route,Is.Not.Empty);
        Assert.That(route.Last(),Is.EqualTo(destination));
        Vector3 previous=origin;
        foreach(Vector3 point in route)
        {
            Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,previous,point),Is.True);
            previous=point;
        }
        Assert.That(route.Max(point=>Mathf.Abs(point.z)),Is.LessThanOrEqualTo(clearance+.15f),
            "Passing one body must not jump to an entire coarse hex row.");
        Assert.That(RouteLength(origin,route),Is.LessThanOrEqualTo(Vector3.Distance(origin,destination)+1f));
        if(blockedSide!=0f)
            Assert.That(route.Any(point=>point.z*blockedSide<-.1f),Is.True,
                "The unobstructed side must win when the other tangent arc is occupied.");
    }

    [TestCase(1f,false)] [TestCase(1f,true)]
    [TestCase(-1f,false)] [TestCase(-1f,true)]
    public void ABodyBypassNearTheArenaEdgeFallsBackToAReachableRoute(float edge,bool reverse)
    {
        var bounds=new Bounds(Vector3.zero,new Vector3(16f,10f,5.4f));
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private).SetValue(battle,bounds);
        ((GameObject)Get(enemy,"model")).transform.position=new Vector3(5f,0f,edge*1.7f);
        var unit=Attacker(new Vector3(-5f,0f,edge*1.7f));
        Unit(0,Vector3.forward*(edge*1.7f));Unit(0,Vector3.forward*(edge*.3f));
        if(reverse)foreach(object actor in units)
            Set(actor,"team",Enum.ToObject(unitType.GetField("team").FieldType,actor==enemy?0:1));
        Vector3 origin=Position(unit),destination=(Vector3)Call("ClosestMeleeAttackPosition",unit,enemy);
        object[] local={unit,enemy,origin,destination,null};
        Assert.That(Call("TryLocalMeleeBypass",local),Is.False,
            "The tight outer arc fits inside the map by its center but not by the moving body's radius.");
        object[] args={unit,enemy,new[]{destination},null};
        Assert.That(Call("TryMeleeSlotRoute",args),Is.True,
            "Rejecting an unreachable boundary arc must still allow the open inner route.");
        var route=(List<Vector3>)args[3];Assert.That(route,Is.Not.Empty);
        float radius=(float)Call("CombatBodyRadius",unit);
        Vector3 previous=origin;
        foreach(Vector3 point in route)
        {
            Assert.That(point.x,Is.InRange(bounds.min.x+radius-.001f,bounds.max.x-radius+.001f));
            Assert.That(point.z,Is.InRange(bounds.min.z+radius-.001f,bounds.max.z-radius+.001f));
            Assert.That(Call("MeleeSlotSegmentClear",unit,enemy,previous,point),Is.True);
            Assert.That(Vector3.Distance(point,(Vector3)Call("ResolveFreePosition",point,radius)),
                Is.LessThan(.001f),"Every route corner must be reachable without arena clamping.");
            previous=point;
        }
        Assert.That(previous,Is.EqualTo(destination));
    }

    [TestCase(true,false,false)] [TestCase(false,true,false)] [TestCase(true,true,false)]
    [TestCase(true,false,true)] [TestCase(false,true,true)] [TestCase(true,true,true)]
    public void ALocalBodyBypassCanStartOrEndTouchingTheRealClearanceInsideItsPaddedArc(
        bool nearStart,bool nearEnd,bool mirror)
    {
        float side=mirror?-1f:1f;
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*(7f*side);
        var source=Attacker(Vector3.left*(5f*side));
        var blocker=Unit(0,Vector3.zero);
        float clearance=(float)Call("BodyClearance",source,blocker,.025f);
        Vector3 origin=Vector3.left*((nearStart?clearance+.0002f:5f)*side);
        Vector3 destination=Vector3.right*((nearEnd?clearance+.0002f:5f)*side);
        ((GameObject)Get(source,"model")).transform.position=origin;
        Assert.That(Call("MeleeSlotSegmentClear",source,enemy,origin,destination),Is.False);
        object[] args={source,enemy,origin,destination,null};
        Assert.That(Call("TryLocalMeleeBypass",args),Is.True,
            "A touching body must use a short outward connector rather than lose its local bypass to the coarse grid.");
        var route=(List<Vector3>)args[4];Vector3 previous=origin;
        Assert.That(route,Is.Not.Empty);
        Assert.That(route[route.Count-1],Is.EqualTo(destination));
        foreach(Vector3 point in route)
        {
            Assert.That(Call("MeleeSlotSegmentClear",source,enemy,previous,point),Is.True);
            previous=point;
        }
        Assert.That(route.Max(point=>Mathf.Abs(point.z)),Is.LessThanOrEqualTo(clearance+.15f),
            "Contact must not turn a one-body bypass into a full deployment-row detour.");
    }

    [TestCase(false)] [TestCase(true)]
    public void AnInterlockingBodyCrowdHasAShortCompleteVisibilityRouteFromItsTouchingEntrance(bool mirror)
    {
        Vector3[] positions={
            new Vector3(-1.162f,0f,.9730572f),new Vector3(.7626061f,0f,2.34735f),
            new Vector3(-2.011053f,0f,-.9362304f),new Vector3(1.317463f,0f,-.2452068f),
            new Vector3(-.530875f,0f,2.790561f),new Vector3(.01381675f,0f,-.5498057f),
            new Vector3(.7620406f,0f,.9730572f)};
        if(mirror)for(int i=0;i<positions.Length;i++)positions[i].x=-positions[i].x;
        ((GameObject)Get(enemy,"model")).transform.position=positions[0];
        Set(enemy,"collisionRadius",.42f);Set(enemy,"combatBodyScale",1.43f);
        var source=Attacker(positions[3],.462f);Set(source,"combatBodyScale",1.15f);
        for(int i=1;i<positions.Length;i++)
        {
            if(i==3)continue;
            var body=Unit(0,positions[i],.462f);Set(body,"combatBodyScale",1.15f);
        }
        Vector3 origin=Position(source),goal=new Vector3(mirror?3.4030933f:-3.4030933f,0f,-.5291042f);
        Assert.That(Call("IsOpenAttackPosition",source,enemy,goal),Is.True);
        object[] local={source,enemy,origin,goal,null};
        Assert.That(Call("TryLocalMeleeBypass",local),Is.False,
            "The regression requires a chain of bodies rather than one padded tangent arc.");
        object[] args={source,enemy,origin,new[]{goal},null};
        Assert.That(Call("TryCrowdMeleeBypass",args),Is.True);
        var route=(List<Vector3>)args[4];Vector3 previous=origin;
        Assert.That(route[route.Count-1],Is.EqualTo(goal));
        foreach(Vector3 point in route)
        {
            Assert.That(Call("MeleeSlotSegmentClear",source,enemy,previous,point),Is.True);
            Assert.That(Vector3.Distance(point,Position(enemy)),
                Is.LessThan(Vector3.Distance(origin,Position(enemy))+MonsterAutoBattlePrototype.TileSize),
                "A local chain of touching bodies must not force a coarse deployment-grid excursion.");
            previous=point;
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void OneCrowdSearchChoosesTheShortestReachableGoalInsteadOfTheNearestBlockedGoal(bool reverseGoals)
    {
        ((GameObject)Get(enemy,"model")).transform.position=Vector3.right*7f;
        var source=Attacker(Vector3.left*4f);
        for(int i=-2;i<=2;i++)Unit(0,Vector3.forward*(i*1.5f),.6f);
        Vector3 blockedGoal=Vector3.right*2f,openGoal=new Vector3(-3f,0f,7f);
        Vector3[] goals=reverseGoals?new[]{openGoal,blockedGoal}:new[]{blockedGoal,openGoal};
        Assert.That(Vector3.Distance(Position(source),blockedGoal),Is.LessThan(Vector3.Distance(Position(source),openGoal)));
        Assert.That(Call("MeleeSlotSegmentClear",source,enemy,Position(source),blockedGoal),Is.False);
        Assert.That(Call("MeleeSlotSegmentClear",source,enemy,Position(source),openGoal),Is.True);
        object[] args={source,enemy,goals,null};
        Assert.That(Call("TryMeleeSlotRoute",args),Is.True);
        var route=(List<Vector3>)args[3];
        Assert.That(route[route.Count-1],Is.EqualTo(openGoal),
            "One multi-goal search must compare complete travel costs rather than prefer candidate order or straight distance.");
        Assert.That((float)Call("RouteDistance",Position(source),route),
            Is.EqualTo(Vector3.Distance(Position(source),openGoal)).Within(.001f));
    }

    [Test] public void EditorWarmupDoesNotRunCombatActionsOrMoveUnits()
    {
        var unit=Attacker(Vector3.left*5);Attacker(new Vector3(-5,0,3));
        Set(unit,"detourTarget",enemy);Set(unit,"detourTimer",1.7f);Set(unit,"repathTimer",.15f);
        Set(unit,"skillTimer",7f);Set(unit,"cooldown",.2f);
        ((List<Vector3>)Get(unit,"route")).Add(new Vector3(-3,0,1));
        var actors=units.Cast<object>().ToArray();var positions=actors.Select(Position).ToArray();
        string[] fields={"target","hp","alive","currentAction","currentTile","detourTarget","detourTimer","repathTimer","targetLockTimer","skillTimer","cooldown","skillUses"};
        var values=actors.Select(actor=>fields.Select(field=>Get(actor,field)).ToArray()).ToArray();
        var routes=actors.Select(actor=>((List<Vector3>)Get(actor,"route")).ToArray()).ToArray();
        var query=typeof(MonsterAutoBattlePrototype).GetField("navigationQueryActive",Private);
        var random=UnityEngine.Random.state;
        var warmup=(IEnumerator)Call("WarmupEditorCombatMethods");
        while(warmup.MoveNext()) { }
        warmup=(IEnumerator)Call("WarmupEditorNavigation");
        while(warmup.MoveNext())Assert.That(query.GetValue(battle),Is.False,"No navigation snapshot may remain active across a loading frame.");
        for(int actor=0;actor<actors.Length;actor++)
        {
            Assert.That(Position(actors[actor]),Is.EqualTo(positions[actor]));
            for(int field=0;field<fields.Length;field++)
                Assert.That(Get(actors[actor],fields[field]),Is.EqualTo(values[actor][field]),fields[field]);
            Assert.That((List<Vector3>)Get(actors[actor],"route"),Is.EqualTo(routes[actor]));
        }
        Assert.That(query.GetValue(battle),Is.False);
        Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
    }
}
