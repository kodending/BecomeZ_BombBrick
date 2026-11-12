using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class WallCrowdingTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private GameObject host;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private readonly List<GameObject> models=new List<GameObject>();
    private MonsterDefinition definition;
    private UnityEngine.Random.State random;
    private bool previousEffectTest;
    private float previousTimeScale;

    [SetUp] public void Setup()
    {
        random=UnityEngine.Random.state;previousTimeScale=Time.timeScale;Time.timeScale=1f;
        previousEffectTest=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=false;
        host=new GameObject("Wall clearance regression");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        Field("combatBounds").SetValue(battle,new Bounds(Vector3.zero,new Vector3(24f,10f,18f)));
        Type tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        object tile=Activator.CreateInstance(tileType,true);
        tileType.GetField("coordinate").SetValue(tile,Vector2Int.zero);
        tileType.GetField("world").SetValue(tile,Vector3.zero);
        tileType.GetField("walkable").SetValue(tile,false);
        ((IDictionary)Field("tiles").GetValue(battle)).Add(Vector2Int.zero,tile);
    }

    [TearDown] public void Cleanup()
    {
        foreach(GameObject model in models)if(model!=null)Object.DestroyImmediate(model);
        models.Clear();
        if(host!=null)Object.DestroyImmediate(host);
        if(definition!=null)Object.DestroyImmediate(definition);
        definition=null;UnityEngine.Random.state=random;Time.timeScale=previousTimeScale;
        CombatEffectTestConfiguration.Active=previousEffectTest;
    }

    [TestCase(false,false)] [TestCase(true,false)]
    [TestCase(false,true)] [TestCase(true,true)]
    public void NavigationRejectsTheSameNarrowWallStripThatMovesThePhysicalBody(bool alongZ,bool snapshot)
    {
        const float radius=.62f;
        float physicalExtent=MonsterAutoBattlePrototype.TileHalfExtent+radius;
        float previousExtent=MonsterAutoBattlePrototype.TileHalfExtent*.82f+radius+.08f;
        Vector3 normal=alongZ?Vector3.right:Vector3.forward;
        Vector3 tangent=alongZ?Vector3.forward:Vector3.right;
        Vector3 center=normal*(physicalExtent-.03f);
        Vector3 from=center-tangent*3f,to=center+tangent*3f;
        Assert.That(Call("SegmentIntersectsSquare",from,to,Vector3.zero,previousExtent),Is.False,
            "This route must reproduce the strip the previous smaller navigation obstacle accepted.");
        if(snapshot)Assert.That(Call("BeginNavigationQuery"),Is.False);
        try
        {
            Assert.That(Call("HasClearPathSegment",from,to,radius),Is.False,
                "A route must not enter a strip where the movement resolver pushes the body out.");
            Vector3 resolved=(Vector3)Call("ResolveFreePosition",center,radius);
            Assert.That(Vector3.Distance(resolved,center),Is.GreaterThan(.02f));
            Assert.That(Vector3.Dot(resolved,normal),Is.EqualTo(physicalExtent).Within(.00001f));

            Vector3 clear=normal*(physicalExtent+.002f);
            Assert.That(Call("HasClearPathSegment",clear-tangent*3f,clear+tangent*3f,radius),Is.True);
            Assert.That(Call("ResolveFreePosition",clear,radius),Is.EqualTo(clear),
                "A physically clear parallel route must remain available.");
        }
        finally{Field("navigationQueryActive").SetValue(battle,false);}
    }

    [TestCase(0,false)] [TestCase(1,false)] [TestCase(0,true)] [TestCase(1,true)]
    public void RegisteredIceWallsBlockHostileQueriesExactlyAsTheyBlockMovement(int team,bool friendly)
    {
        ((IDictionary)Field("tiles").GetValue(battle)).Clear();
        object source=Actor(team,Vector3.left*2f),target=Actor(1-team,Vector3.right*2f);
        Set(source,"target",target);
        Call("RegisterRank3IceWall",friendly?source:target,Vector3.zero,Vector3.right,Vector3.forward,1.6f,30f);
        Vector3 from=Position(source),to=Position(target);
        Assert.That(Call("BlockRank3IceWallMovement",source,from,to),Is.EqualTo(friendly?to:from));
        for(int query=0;query<2;query++)
        {
            if(query==1)Assert.That(Call("BeginNavigationQuery"),Is.False);
            try
            {
                Assert.That(Call("HasClearPath",source,to),Is.EqualTo(friendly));
                Assert.That(Call("HasClearNavigationSegment",source,from,to),Is.EqualTo(friendly));
                Assert.That(Call("HasClearAttackLine",source,target),Is.EqualTo(friendly));
                // Crossing beyond the real end of the wall must stay available to either team.
                Vector3 endBypass=Vector3.forward*1.8f;
                Assert.That(Call("HasClearNavigationSegment",source,from+endBypass,to+endBypass),Is.True);
            }
            finally{Field("navigationQueryActive").SetValue(battle,false);}
        }
        Call("ClearRank3IceWalls");
        Assert.That(Call("HasClearPath",source,to),Is.True);
        Assert.That(Call("HasClearAttackLine",source,target),Is.True);
    }

    [Test]
    public void AnIceWallInsideTheArrivalRingKeepsItsNecessaryLongEndBypass()
    {
        ((IDictionary)Field("tiles").GetValue(battle)).Clear();AddWalkableGrid();
        object source=Actor(0,Vector3.left*5f),target=Actor(1,Vector3.zero);
        Set(source,"target",target);
        Vector3 origin=Position(source),center=Position(target);
        Vector3 nearest=(Vector3)Call("ClosestMeleeAttackPosition",source,target);
        Call("RegisterRank3IceWall",target,Vector3.left*.6f,Vector3.right,Vector3.forward,4f,30f);
        Assert.That(Call("HasClearNavigationSegment",source,origin,nearest),Is.True,
            "The wall must sit beyond the nearest arrival position, outside the walking approach segment.");
        Assert.That(Call("IsHostileIceWallBlocking",source,nearest,center),Is.True);
        Assert.That(Call("HasClearAttackLine",source,target),Is.False);
        float crowdLimit=Vector3.Distance(origin,nearest)+Mathf.Max(MonsterAutoBattlePrototype.TileSize*1.5f,
            (float)Call("MeleeContactDistance",source,target));
        Assert.That(Call("BuildClosestMeleeRoute",source,target),Is.True);
        var route=(List<Vector3>)Get(source,"route");
        Assert.That((float)Call("RouteDistance",origin,route),Is.GreaterThan(crowdLimit+.01f),
            "The necessary wall-end route must remain available beyond the body-only detour allowance.");
        Vector3 previous=origin;
        foreach(Vector3 point in route)
        {
            Assert.That(Call("MeleeSlotSegmentClear",source,target,previous,point),Is.True);
            previous=point;
        }
        Assert.That(Call("IsHostileIceWallBlocking",source,route[route.Count-1],center),Is.False);
    }

    [TestCase(0)] [TestCase(1)]
    public void TwoMeleeAttackersGoAroundAHostileWallEndAndDealRealBasicDamage(int wallTeam)
    {
        ((IDictionary)Field("tiles").GetValue(battle)).Clear();AddWalkableGrid();
        Field("expeditionRun").SetValue(battle,new ExpeditionRunSave
            {runId="wall_crowding",regionId=HunterRegionCatalog.FirstRegionId});
        Field("playerProfile").SetValue(battle,new PlayerProfileSave());
        Field("mlEpisodeActive").SetValue(battle,true);Field("battleStarted").SetValue(battle,true);
        definition=ScriptableObject.CreateInstance<MonsterDefinition>();
        definition.monsterId="wall_fixture";definition.familyId="wall_fixture";
        var attackers=new object[2];var defenders=new object[2];var crossed=new bool[2];
        for(int i=0;i<2;i++)
        {
            float z=i==0?-.8f:.8f;
            attackers[i]=Actor(1-wallTeam,new Vector3(-4f,0f,z));
            defenders[i]=Actor(wallTeam,new Vector3(4f,0f,z));
            Set(attackers[i],"target",defenders[i]);Set(defenders[i],"target",attackers[i]);
            Set(defenders[i],"speedTilesPerSecond",0f);
        }
        Call("RegisterRank3IceWall",defenders[0],Vector3.zero,Vector3.right,Vector3.forward,1.6f,30f);
        const float dt=1f/60f;
        for(int frame=0;frame<720;frame++)
        {
            for(int i=0;i<2;i++)
            {
                Vector3 previous=Position(attackers[i]);Call("UpdateUnit",attackers[i],dt);
                Vector3 current=Position(attackers[i]);
                Assert.That(Call("IsHostileIceWallBlocking",attackers[i],previous,current),Is.False,
                    "The route must use a real end of the wall, rather than crossing its blocked slice.");
                Assert.That(Vector3.Distance(previous,current),Is.LessThanOrEqualTo(
                    (float)Call("CurrentMovementSpeed",attackers[i])*1.18f*dt+.015f));
                if(previous.x<0f&&current.x>=0f)crossed[i]=true;
                Call("UpdateUnit",defenders[i],dt);
                Assert.That(Vector3.Distance(current,Position(defenders[i])),Is.GreaterThanOrEqualTo(.599f));
            }
            Assert.That(Vector3.Distance(Position(attackers[0]),Position(attackers[1])),Is.GreaterThanOrEqualTo(.599f));
        }
        for(int i=0;i<2;i++)
        {
            Assert.That(crossed[i],Is.True,"Each attacker must actually pass the wall end.");
            Assert.That(Get(attackers[i],"basicAttackCount"),Is.GreaterThan(0));
            Assert.That((float)Get(defenders[i],"hp"),Is.LessThan(5000f));
            Assert.That(Get(attackers[i],"target"),Is.SameAs(defenders[i]));
        }
    }

    private void AddWalkableGrid()
    {
        Type tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)Field("tiles").GetValue(battle);
        for(int x=-6;x<=6;x++)for(int z=-6;z<=6;z++)
        {
            var coordinate=new Vector2Int(x,z);Vector3 world=(Vector3)Call("GridWorld",coordinate,false);
            if(Mathf.Abs(world.x)>12f||Mathf.Abs(world.z)>9f)continue;
            object tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,coordinate);tileType.GetField("world").SetValue(tile,world);
            tileType.GetField("walkable").SetValue(tile,true);tiles.Add(coordinate,tile);
        }
    }
    private object Actor(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);var model=new GameObject("Wall query actor");models.Add(model);
        model.transform.position=position;Set(unit,"model",model);Set(unit,"ring",new GameObject("Wall query shadow"));
        ((GameObject)Get(unit,"ring")).transform.SetParent(model.transform,false);
        var units=(IList)Field("units").GetValue(battle);
        Set(unit,"id",units.Count);Set(unit,"stableId","wall_"+units.Count);Set(unit,"definition",definition);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",10f);
        Set(unit,"collisionRadius",.3f);Set(unit,"attackRange",1);Set(unit,"attackInterval",.8f);
        Set(unit,"speedTilesPerSecond",2f);Set(unit,"skillTimer",100f);Set(unit,"skillId","focus_chain");
        Set(unit,"targetLockTimer",30f);Set(unit,"lastGroundPosition",position);units.Add(unit);return unit;
    }
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private Vector3 Position(object unit)=>((GameObject)Get(unit,"model")).transform.position;
    private FieldInfo Field(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private).Invoke(battle,args);
}
