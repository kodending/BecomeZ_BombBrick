using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class LastTargetCrowdingTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> owned=new List<GameObject>();
    private readonly List<object> actors=new List<object>();
    private MonsterAutoBattlePrototype battle;
    private MonsterDefinition definition;
    private Type unitType;
    private IList units;
    private bool previousEffectTest;
    private float previousSpeed;
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()
    {
        previousSpeed=Time.timeScale;Time.timeScale=1f;
        random=UnityEngine.Random.state;UnityEngine.Random.InitState(7021);
        previousEffectTest=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=false;
        var host=Go("Last target crowd integration");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)Field("units").GetValue(battle);
        definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos");
        Assert.That(definition,Is.Not.Null);
        Field("combatBounds").SetValue(battle,new Bounds(Vector3.zero,new Vector3(28f,10f,20f)));
        Field("expeditionRun").SetValue(battle,new ExpeditionRunSave
            {runId="last_target_crowding",regionId=HunterRegionCatalog.FirstRegionId});
        Field("playerProfile").SetValue(battle,new PlayerProfileSave());
        Field("mlEpisodeActive").SetValue(battle,true);Field("battleStarted").SetValue(battle,true);
        AddWalkableGrid();
    }

    [TearDown] public void Cleanup()
    {
        for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);
        owned.Clear();actors.Clear();CombatEffectTestConfiguration.Active=previousEffectTest;
        UnityEngine.Random.state=random;Time.timeScale=previousSpeed;
    }

    [TestCase(0,"melee",false)] [TestCase(1,"melee",true)]
    [TestCase(0,"moving",true)] [TestCase(1,"moving",false)]
    [TestCase(0,"mixed",false)] [TestCase(1,"mixed",true)]
    public void LosingSevenOpponentsDoesNotMakeTheRemainingFightJumpOrTraverseTheBattlefieldRepeatedly(
        int crowdedTeam,string arrangement,bool reverseUpdate)
    {
        var crowded=new List<object>();var opponents=new List<object>();
        float side=crowdedTeam==0?-1f:1f;
        for(int column=0;column<2;column++)for(int row=0;row<4;row++)
        {
            float x=5f+column*1.5f,z=(row-1.5f)*1.3f;
            object attacker=Actor(crowdedTeam,new Vector3(side*x,0f,z));
            object defender=Actor(1-crowdedTeam,new Vector3(-side*x,0f,z));
            crowded.Add(attacker);opponents.Add(defender);
            Set(attacker,"target",defender);Set(defender,"target",attacker);
        }
        object last=opponents[2];
        if(arrangement=="moving")Set(last,"speedTilesPerSecond",.7f);
        if(arrangement=="mixed")
        {
            // Keep damage immediate while exercising the production ranged movement branch.
            Set(last,"attackRange",3);Set(crowded[4],"attackRange",3);Set(crowded[7],"attackRange",3);
        }
        const float dt=1f/60f;
        bool lastMovedDuringWarmup=false;
        int warmupFrames=0;
        while(warmupFrames<90)
        {
            Step(reverseUpdate,dt);warmupFrames++;
            bool moving=(bool)Get(last,"moving");lastMovedDuringWarmup|=moving;
            if(arrangement=="moving"&&warmupFrames>=20&&moving)break;
        }
        if(arrangement=="moving")
        {
            TestContext.WriteLine("Moving-target trigger: team="+crowdedTeam+", frame="+warmupFrames+
                ", moved during warmup="+lastMovedDuringWarmup);
            Assert.That(Get(last,"moving"),Is.EqualTo(true),lastMovedDuringWarmup
                ?"The last target must still be approaching when its team disappears."
                :"The last target never moved during the 90-frame setup; investigate pursuit before changing this fixture.");
        }
        foreach(object opponent in opponents)
            if(opponent!=last){Set(opponent,"alive",false);Set(opponent,"hp",0f);}

        float initialHp=(float)Get(last,"hp"),maximumStep=0f,maximumExcursion=0f,minimumGap=float.MaxValue;
        int attacksBefore=0,attacksAfter=0;
        foreach(object attacker in crowded)attacksBefore+=(int)Get(attacker,"basicAttackCount");
        var previous=new Vector3[actors.Count];var previousAttacks=new int[actors.Count];var clearStarts=new bool[actors.Count];
        var largeSide=new int[crowded.Count];var crossings=new int[crowded.Count];
        for(int frame=0;frame<720;frame++)
        {
            for(int i=0;i<actors.Count;i++)
            {
                previous[i]=Position(actors[i]);previousAttacks[i]=(int)Get(actors[i],"basicAttackCount");
                clearStarts[i]=(float)Call("BodyOverlapCost",actors[i],previous[i])<=.000001f;
            }
            Step(reverseUpdate,dt);
            for(int i=0;i<actors.Count;i++)
            {
                object actor=actors[i];if(!(bool)Get(actor,"alive"))continue;
                float moved=Vector3.Distance(previous[i],Position(actor));maximumStep=Mathf.Max(maximumStep,moved);
                float budget=(float)Call("CurrentMovementSpeed",actor)*1.18f*dt+.015f;
                if(clearStarts[i])Assert.That(moved,Is.LessThanOrEqualTo(budget),
                    "Ordinary pursuit from separated bodies must not turn correction into a movement burst. "+Describe(actor));
                if((int)Get(actor,"basicAttackCount")>previousAttacks[i])
                {
                    object victim=Get(actor,"target");
                    Assert.That(Vector3.Distance(Position(actor),Position(victim)),
                        Is.LessThanOrEqualTo((float)Call("AttackDistance",actor,victim)+.1f),Describe(actor));
                }
                for(int j=i+1;j<actors.Count;j++)
                {
                    if(!(bool)Get(actors[j],"alive"))continue;
                    float gap=Vector3.Distance(Position(actor),Position(actors[j]))-.6f;
                    minimumGap=Mathf.Min(minimumGap,gap);
                    Assert.That(gap,Is.GreaterThanOrEqualTo(-.001f),Describe(actor));
                }
            }
            for(int i=0;i<crowded.Count;i++)
            {
                object attacker=crowded[i];Vector3 relative=Position(attacker)-Position(last);
                maximumExcursion=Mathf.Max(maximumExcursion,Mathf.Abs(relative.x));
                float band=(float)Call("AttackDistance",attacker,last)+MonsterAutoBattlePrototype.TileSize*.5f;
                int current=relative.x < -band?-1:relative.x > band?1:0;
                if(current==0)continue;
                if(largeSide[i]!=0&&largeSide[i]!=current)crossings[i]++;
                largeSide[i]=current;
            }
        }
        int maximumCrossings=0;
        foreach(int value in crossings)maximumCrossings=Mathf.Max(maximumCrossings,value);
        foreach(object attacker in crowded)
        {
            Assert.That(Get(attacker,"target"),Is.SameAs(last));
            attacksAfter+=(int)Get(attacker,"basicAttackCount");
        }
        TestContext.WriteLine("Last target: team="+crowdedTeam+", mode="+arrangement+
            ", max step="+maximumStep+", min body gap="+minimumGap+", max relative X="+maximumExcursion+
            ", full side traversals="+maximumCrossings+", new attacks="+(attacksAfter-attacksBefore)+
            ", damage="+(initialHp-(float)Get(last,"hp")));
        Assert.That(maximumCrossings,Is.LessThanOrEqualTo(2),
            "A flank may cross sides once; repeatedly travelling beyond both attack-range bands is battlefield oscillation.");
        Assert.That(attacksAfter,Is.GreaterThan(attacksBefore),"Crowding may make the rear wait, but the front must keep fighting.");
        Assert.That((float)Get(last,"hp"),Is.LessThan(initialHp));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void APreparedBodyBypassBrakesForItsShortCornerBeforeContinuingToAttack(int fps)
    {
        object target=Actor(1,Vector3.zero),source=Actor(0,new Vector3(-5f,0f,.24f));
        object blocker=Actor(1,new Vector3(-4.4f,0f,-.09f));Set(blocker,"collisionRadius",.05f);
        Set(source,"target",target);Set(source,"moving",true);Set(source,"velocity",Vector3.forward*3f);
        Set(source,"decisionTimer",100f);Set(source,"repathTimer",100f);
        Vector3 corner=new Vector3(-5f,0f,.3f);
        Vector3 goal=Vector3.left*(float)Call("MeleeArrivalDistance",source,target)+Vector3.forward*.3f;
        var route=(List<Vector3>)Get(source,"route");route.Add(corner);route.Add(goal);
        Set(source,"detourTarget",target);Set(source,"detourTimer",3f);
        Vector3 radial=(Vector3)Call("ClosestMeleeAttackPosition",source,target);
        Assert.That(Call("MeleeSlotSegmentClear",source,target,Position(source),radial),Is.False);
        Assert.That(Call("MeleeSlotSegmentClear",source,target,Position(source),corner),Is.True);
        Assert.That(Call("MeleeSlotSegmentClear",source,target,corner,goal),Is.True);
        float dt=1f/fps;Vector3 targetPosition=Position(target),blockerPosition=Position(blocker);
        Call("UpdateUnit",source,dt);
        Assert.That(Position(source).z,Is.LessThanOrEqualTo(corner.z+.001f),
            "The existing forward velocity must brake for the .06m corner instead of overshooting its .05m arrival tolerance.");
        float closestCorner=Vector3.Distance(Position(source),corner);
        for(int frame=1;frame<fps*5;frame++)
        {
            Call("UpdateUnit",source,dt);
            closestCorner=Mathf.Min(closestCorner,Vector3.Distance(Position(source),corner));
            Assert.That(Vector3.Distance(Position(source),Position(blocker)),Is.GreaterThanOrEqualTo(.349f));
            Assert.That(Vector3.Distance(Position(source),Position(target)),Is.GreaterThanOrEqualTo(.599f));
        }
        TestContext.WriteLine("Short corner: FPS="+fps+", closest corner="+closestCorner+
            ", final position="+Position(source)+", attacks="+Get(source,"basicAttackCount")+", target HP="+Get(target,"hp"));
        Assert.That(closestCorner,Is.LessThanOrEqualTo(.051f),"The actor must actually reach the short corner's arrival tolerance.");
        Assert.That(Position(source).x,Is.GreaterThan(blockerPosition.x+.35f),
            "A cleared route may reset its index, but the actor must still pass the blocker toward its target.");
        Assert.That((int)Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That((float)Get(target,"hp"),Is.LessThan(5000f));
        Assert.That(Position(target),Is.EqualTo(targetPosition));Assert.That(Position(blocker),Is.EqualTo(blockerPosition));
    }

    [TestCase(0,30)] [TestCase(0,60)] [TestCase(0,120)]
    [TestCase(1,30)] [TestCase(1,60)] [TestCase(1,120)]
    public void AlliedSpacingCorrectionDoesNotBoostAnOrdinaryWalkingStep(int team,int fps)
    {
        float side=team==0?1f:-1f;
        object source=Actor(team,Vector3.left*(4f*side));
        object ally=Actor(team,Vector3.left*(4.8f*side));
        object target=Actor(1-team,Vector3.right*(3f*side));
        Set(source,"target",target);Set(source,"cooldown",100f);
        Set(source,"velocity",Vector3.right*(3f*side));Set(source,"moving",true);
        Vector3 origin=Position(source),allyPosition=Position(ally),targetPosition=Position(target);
        Assert.That((float)Call("BodyOverlapCost",source,origin),Is.GreaterThan(0f),
            "The allied spacing margin overlaps even though the physical bodies are separate.");
        Assert.That(Vector3.Distance(origin,allyPosition),Is.GreaterThan(.6f));
        Vector3 destination=(Vector3)Call("ClosestMeleeAttackPosition",source,target);
        Assert.That(Call("MeleeSlotSegmentClear",source,target,origin,destination),Is.True,
            "The ally behind the actor must not divert this test into path waiting or inertia coasting.");
        float dt=1f/fps,budget=(float)Call("CurrentMovementSpeed",source)*1.18f*dt+.001f;
        Call("UpdateUnit",source,dt);
        Assert.That(Get(source,"currentAction"),Is.EqualTo("personality_pursue"));
        Assert.That(Get(source,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Vector3.Distance(origin,Position(source)),Is.LessThanOrEqualTo(budget),
            "Existing allied spacing pressure must not add a positional burst to the walking velocity.");
        Assert.That(Call("IntroducesBodyOverlap",source,origin,Position(source),true),Is.False);
        Assert.That(Vector3.Distance(Position(source),allyPosition),Is.GreaterThanOrEqualTo(.599f));
        Assert.That(Vector3.Distance(Position(source),targetPosition),Is.GreaterThanOrEqualTo(.599f));
        Assert.That(Position(ally),Is.EqualTo(allyPosition));Assert.That(Position(target),Is.EqualTo(targetPosition));
    }

    [TestCase(false)] [TestCase(true)]
    public void AReachableCrowdBypassKeepsItsAttackEndpointInsteadOfReturningToTheBlockedFront(bool cached)
    {
        // Natural 1:8 fight at 28.137s: a valid outer path was discarded for a partial forward step.
        string[] species={"geomchi","krogal","tino","dami","santius","igvire","berber","seola","geomchi"};
        float[] radii={.42f,.462f,.546f,.462f,.462f,.42f,.462f,.42f,.42f};
        Vector3[] positions={
            new Vector3(-2.708007f,0f,.6221679f),new Vector3(-4.025489f,0f,4.404775f),
            new Vector3(-.7697002f,0f,3.68297f),new Vector3(-1.523149f,0f,.521026f),
            new Vector3(-3.999567f,0f,6.469401f),new Vector3(-1.274858f,0f,6.550559f),
            new Vector3(-1.956959f,0f,2.235701f),new Vector3(-3.664275f,0f,2.447476f),
            new Vector3(-1.94683f,0f,5.055389f)};
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var snapshot=new object[positions.Length];
        for(int i=0;i<snapshot.Length;i++)
        {
            object actor=Actor(i==0?0:1,positions[i]);snapshot[i]=actor;
            MonsterDefinition monster=database.Get(species[i]);Assert.That(monster,Is.Not.Null);
            Set(actor,"definition",monster);Set(actor,"collisionRadius",radii[i]);
            Set(actor,"combatBodyScale",i==0?1.43f:1.15f);Set(actor,"attackRange",monster.attackRange);
            Set(actor,"skillId",monster.skillId);Set(actor,"speedTilesPerSecond",monster.moveSpeedTilesPerSecond);
            if(i>0)Set(actor,"target",snapshot[0]);
        }
        object target=snapshot[0],source=snapshot[1];Set(target,"target",snapshot[2]);
        Vector3 origin=Position(source),destination=(Vector3)Call("ClosestMeleeAttackPosition",source,target);
        Assert.That(Call("HasClearPathSegment",origin,destination,(float)Call("CombatBodyRadius",source)),Is.True,
            "Static obstacles must not invoke the unrestricted wall-detour case.");
        Assert.That(Call("MeleeSlotSegmentClear",source,target,origin,destination),Is.False,
            "The last opponent's nearby bodies are the obstruction in this snapshot.");
        var route=(List<Vector3>)Get(source,"route");
        if(cached)
        {
            Vector3 oldWaypoint=new Vector3(-8.085001f,0f,6.065287f);
            Vector3 oldEndpoint=new Vector3(-5.302638f,0f,1.361681f);
            Assert.That(Call("IsOpenAttackPosition",source,target,oldEndpoint),Is.True,
                "The completed bypass must still end at a usable attack position.");
            route.Add(oldWaypoint);route.Add(oldEndpoint);
            Set(source,"detourTarget",target);Set(source,"detourTimer",3f);Set(source,"repathTimer",.1f);
            Assert.That(Call("GetNavigationDirection",source,destination-origin,true),Is.EqualTo(oldWaypoint-origin),
                "A cached approach must remain stable until the repath interval elapses.");
            Set(source,"repathTimer",0f);
            Assert.That(Call("MeleePursuitRouteNeedsRefresh",source),Is.False,
                "A clear attack endpoint remains usable even when the body bypass exceeds the old distance budget.");
            Vector3[] expected=route.ToArray();
            Call("GetNavigationDirection",source,destination-origin,true);
            CollectionAssert.AreEqual(expected,route,
                "Reaching the repath interval must not replace an open bypass with a forward fragment.");
        }
        else Assert.That(Call("BuildClosestMeleeRoute",source,target),Is.True);
        Assert.That(route,Is.Not.Empty);
        Vector3 endpoint=route[route.Count-1],previous=origin;
        Assert.That(Vector3.Distance(endpoint,Position(target)),
            Is.LessThanOrEqualTo((float)Call("MeleeArrivalDistance",source,target)+.001f));
        Assert.That(Call("IsOpenAttackPosition",source,target,endpoint),Is.True);
        foreach(Vector3 point in route)
        {
            Assert.That(Call("MeleeSlotSegmentClear",source,target,previous,point),Is.True);
            previous=point;
        }
        Assert.That(Position(source),Is.EqualTo(origin));
        Assert.That(Get(source,"target"),Is.SameAs(target));
    }

    [TestCase(0,30)] [TestCase(0,120)] [TestCase(1,30)] [TestCase(1,120)]
    public void ACompletePathAroundADeepBodyBarrierWinsOverAShorterPartialForwardStep(int team,int fps)
    {
        float side=team==0?1f:-1f;
        object source=Actor(team,new Vector3(-5f*side,0f,0f));
        object target=Actor(1-team,new Vector3(2.5f*side,0f,0f));
        Set(source,"target",target);Set(source,"targetLockTimer",100f);
        for(int i=-2;i<=2;i++)
        {
            object blocker=Actor(team,new Vector3(-2.5f*side,0f,i*1.5f));
            Set(blocker,"collisionRadius",.55f);
        }
        Vector3 origin=Position(source),nearest=(Vector3)Call("ClosestMeleeAttackPosition",source,target);
        Assert.That(Call("BuildMeleeApproachStep",source,target,origin),Is.Not.Null,
            "A shorter forward fragment must be available to reproduce the old route replacement.");
        Assert.That(Call("BuildClosestMeleeRoute",source,target),Is.True);
        var route=(List<Vector3>)Get(source,"route");
        float oldLimit=Vector3.Distance(origin,nearest)+Mathf.Max(MonsterAutoBattlePrototype.TileSize*1.5f,
            (float)Call("MeleeContactDistance",source,target));
        Assert.That((float)Call("RouteDistance",origin,route),Is.GreaterThan(oldLimit+.01f));
        Assert.That(Vector3.Distance(route[route.Count-1],Position(target)),
            Is.LessThanOrEqualTo((float)Call("MeleeArrivalDistance",source,target)+.001f),
            "The path must end in attack range rather than stop at the blocking front line.");
        AssertBodyBypassReachesAttack(source,target,fps);
    }

    [TestCase(0,30)] [TestCase(0,120)] [TestCase(1,30)] [TestCase(1,120)]
    public void ABodyPocketAllowsAnInitialRetreatAndThenReachesTheTarget(int team,int fps)
    {
        float side=team==0?1f:-1f;
        object source=Actor(team,new Vector3(-4f*side,0f,0f));
        object target=Actor(1-team,new Vector3(4f*side,0f,0f));
        Set(source,"target",target);Set(source,"targetLockTimer",100f);
        Vector3[] centers={new Vector3(-2.8f,0f,0f),new Vector3(-4f,0f,1.2f),new Vector3(-4f,0f,-1.2f)};
        foreach(Vector3 center in centers)
        {
            object blocker=Actor(team,new Vector3(center.x*side,0f,center.z));
            Set(blocker,"collisionRadius",.6f);
        }
        Vector3 origin=Position(source);
        Assert.That(Call("BuildClosestMeleeRoute",source,target),Is.True);
        var route=(List<Vector3>)Get(source,"route");
        Assert.That(route[0].x*side,Is.LessThan(origin.x*side-.1f),
            "The closed front and sides require leaving through the open rear before going around the pocket.");
        AssertBodyBypassReachesAttack(source,target,fps);
        Assert.That(Position(source).x*side,Is.GreaterThan(origin.x*side));
    }

    private void AssertBodyBypassReachesAttack(object source,object target,int fps)
    {
        Vector3[] fixedPositions=new Vector3[actors.Count];
        for(int i=0;i<actors.Count;i++)fixedPositions[i]=Position(actors[i]);
        for(int frame=0;frame<fps*12;frame++)
        {
            Call("UpdateUnit",source,1f/fps);
            for(int i=0;i<actors.Count;i++)
            {
                if(actors[i]==source)continue;
                float minimum=(float)Call("CombatBodyRadius",source)+(float)Call("CombatBodyRadius",actors[i]);
                Assert.That(Vector3.Distance(Position(source),Position(actors[i])),Is.GreaterThanOrEqualTo(minimum-.001f),Describe(source));
            }
        }
        Assert.That(Get(source,"basicAttackCount"),Is.GreaterThan(0),
            "An available outer path must lead to an attack without moving the blocking neighbours. "+Describe(source));
        Assert.That(Call("CanKeepMeleeAttackPosition",source,target),Is.True);
        for(int i=0;i<actors.Count;i++)if(actors[i]!=source)Assert.That(Position(actors[i]),Is.EqualTo(fixedPositions[i]));
    }

    [TestCase(2,false)] [TestCase(2,true)]
    [TestCase(3,false)] [TestCase(3,true)]
    [TestCase(7,false)] [TestCase(7,true)]
    public void RearActorsApproachTheCrowdBeforeWaitingForAnAttackOpening(int sourceId,bool reverseTeams)
    {
        // Natural 1:8 fight at 4.923s: these three actors waited far away with no attacks.
        string[] species={"seola","krogal","tino","dami","santius","igvire","berber","seola","geomchi"};
        float[] radii={.42f,.462f,.5459999f,.462f,.462f,.42f,.462f,.42f,.42f};
        Vector3[] positions={
            new Vector3(-1.37834537f,0f,.9730572f),new Vector3(.5456607f,0f,.9730572f),
            new Vector3(6.599166f,0f,-3.99659252f),new Vector3(5.545306f,0f,4.96774864f),
            new Vector3(.55294174f,0f,-4.15559769f),new Vector3(4.61343145f,0f,3.51917219f),
            new Vector3(-.165843144f,0f,2.59420872f),new Vector3(11.0250006f,0f,3.51917219f),
            new Vector3(-.434586436f,0f,-.8074292f)};
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var snapshot=new object[positions.Length];
        int targetTeam=reverseTeams?1:0;
        for(int i=0;i<snapshot.Length;i++)
        {
            object actor=Actor(i==0?targetTeam:1-targetTeam,positions[i]);snapshot[i]=actor;
            MonsterDefinition monster=database.Get(species[i]);Assert.That(monster,Is.Not.Null);
            Set(actor,"definition",monster);Set(actor,"collisionRadius",radii[i]);
            Set(actor,"combatBodyScale",i==0?1.43f:1.15f);Set(actor,"attackRange",monster.attackRange);
            Set(actor,"skillId",monster.skillId);Set(actor,"speedTilesPerSecond",monster.moveSpeedTilesPerSecond);
            if(i>0)Set(actor,"target",snapshot[0]);
        }
        object target=snapshot[0],source=snapshot[sourceId];Set(target,"target",snapshot[1]);
        Vector3 origin=Position(source),toward=(Position(target)-origin).normalized;
        float initialDistance=Vector3.Distance(origin,Position(target));
        float sourceRadius=(float)Call("CombatBodyRadius",source),minimumGap=float.MaxValue;
        float maximumDistance=initialDistance,maximumLateral=0f;
        bool moved=false;
        for(int frame=0;frame<120;frame++)
        {
            Call("UpdateUnit",source,1f/60f);moved|=(bool)Get(source,"moving");
            Vector3 offset=Position(source)-origin;
            maximumDistance=Mathf.Max(maximumDistance,Vector3.Distance(Position(source),Position(target)));
            maximumLateral=Mathf.Max(maximumLateral,(offset-toward*Vector3.Dot(offset,toward)).magnitude);
            for(int i=0;i<snapshot.Length;i++)
            {
                if(i==sourceId)continue;
                float gap=Vector3.Distance(Position(source),Position(snapshot[i]))-sourceRadius-
                    (float)Call("CombatBodyRadius",snapshot[i]);
                minimumGap=Mathf.Min(minimumGap,gap);
                Assert.That(gap,Is.GreaterThanOrEqualTo(-.001f),Describe(source));
            }
        }
        float progress=initialDistance-Vector3.Distance(Position(source),Position(target));
        // Dami starts only .89m before Igvire's physical body along the approach line.
        float requiredProgress=sourceId==3?.5f:MonsterAutoBattlePrototype.TileSize;
        TestContext.WriteLine("Rear approach: id="+sourceId+", reverse="+reverseTeams+", progress="+progress+
            ", moved="+moved+", min gap="+minimumGap+", max target gap="+maximumDistance+
            ", max lateral="+maximumLateral+", "+Describe(source));
        Assert.That(moved,Is.True,"A distant rear actor must use its available approach before waiting.");
        Assert.That(progress,Is.GreaterThanOrEqualTo(requiredProgress),"A full attack slot is not required to approach the crowd.");
        Assert.That(maximumDistance,Is.LessThanOrEqualTo(initialDistance+.1f),"Approaching must not start an outward traversal.");
        Assert.That(maximumLateral,Is.LessThanOrEqualTo(MonsterAutoBattlePrototype.TileSize*2f),
            "A partial approach must stay near the fight instead of circling the battlefield.");
        Assert.That(Get(source,"target"),Is.SameAs(target));
        for(int i=0;i<snapshot.Length;i++)
        {
            Assert.That(Get(snapshot[i],"alive"),Is.EqualTo(true));
            if(i!=sourceId)Assert.That(Position(snapshot[i]),Is.EqualTo(positions[i]));
        }
    }

    [TestCase(false,false,false)] [TestCase(false,true,false)]
    [TestCase(true,false,false)] [TestCase(true,true,false)]
    [TestCase(false,false,true)] [TestCase(false,true,true)]
    [TestCase(true,false,true)] [TestCase(true,true,true)]
    public void ATrappedMeleeActorEscapesTheWallAndReachesTheLastOpponent(bool reverseTeams,bool mirrorWall,bool tightCorner)
    {
        var bounds=new Bounds(new Vector3(0f,0f,.6124997f),new Vector3(26.9500027f,10f,13.4351063f));
        Field("combatBounds").SetValue(battle,bounds);
        string[] species={"seola","krogal","tino","dami","santius","igvire","berber","seola","geomchi"};
        float[] radii={.42f,.462f,.5459999f,.462f,.462f,.42f,.462f,.42f,.42f};
        Vector3[] positions={
            new Vector3(-1.73487973f,0f,.9730572f),new Vector3(1.33919477f,0f,-.264968634f),
            new Vector3(6.53775072f,0f,6.70215273f),new Vector3(-1.37376189f,0f,-1.00962746f),
            new Vector3(.6348751f,0f,-1.94532764f),new Vector3(5.12753344f,0f,5.497235f),
            new Vector3(1.72238541f,0f,1.65475738f),new Vector3(.2909478f,0f,2.63902521f),
            new Vector3(.140832335f,0f,.9730572f)};
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var snapshot=new object[positions.Length];var bodyRadii=new float[positions.Length];
        int targetTeam=reverseTeams?1:0;
        for(int i=0;i<snapshot.Length;i++)
        {
            if(mirrorWall)positions[i].z=bounds.center.z*2f-positions[i].z;
            object actor=Actor(i==0?targetTeam:1-targetTeam,positions[i]);snapshot[i]=actor;
            MonsterDefinition monster=database.Get(species[i]);Assert.That(monster,Is.Not.Null);
            Set(actor,"definition",monster);Set(actor,"collisionRadius",radii[i]);
            Set(actor,"combatBodyScale",i==0?1.43f:1.15f);Set(actor,"attackRange",monster.attackRange);
            Set(actor,"skillId",monster.skillId);Set(actor,"speedTilesPerSecond",monster.moveSpeedTilesPerSecond);
            bodyRadii[i]=(float)Call("CombatBodyRadius",actor);
            if(i>0)Set(actor,"target",snapshot[0]);
        }
        object target=snapshot[0],source=snapshot[2];Set(target,"target",snapshot[8]);
        if(tightCorner)
        {
            // Keep an actual corner trap after reducing the allied movement margin.
            float clearance=(float)Call("BodyClearance",source,snapshot[5],.025f);
            positions[5]=positions[2]+(positions[5]-positions[2]).normalized*(clearance+.03f);
            ((GameObject)Get(snapshot[5],"model")).transform.position=positions[5];
            Set(snapshot[5],"lastGroundPosition",positions[5]);
            Assert.That(Call("BuildMeleeApproachStep",source,target,Position(source)),Is.Null);
        }
        Vector3 origin=Position(source);float radius=bodyRadii[2];
        float initialDistance=Vector3.Distance(origin,Position(target)),initialHp=(float)Get(target,"hp");
        float maximumDistance=initialDistance,minimumGap=float.MaxValue;
        bool moved=false,escapedCorner=false,leftWall=false,approachedAfterEscape=false;
        for(int frame=0;frame<720;frame++)
        {
            Call("UpdateUnit",source,1f/60f);moved|=(bool)Get(source,"moving");
            Vector3 position=Position(source);float distance=Vector3.Distance(position,Position(target));
            maximumDistance=Mathf.Max(maximumDistance,distance);
            escapedCorner|=position.x>origin.x+.1f;
            leftWall|=mirrorWall?position.z-(bounds.min.z+radius)>=.5f:bounds.max.z-radius-position.z>=.5f;
            approachedAfterEscape|=(tightCorner?escapedCorner:leftWall)&&distance<=initialDistance-MonsterAutoBattlePrototype.TileSize;
            Assert.That(position.x,Is.InRange(bounds.min.x+radius-.001f,bounds.max.x-radius+.001f),Describe(source));
            Assert.That(position.z,Is.InRange(bounds.min.z+radius-.001f,bounds.max.z-radius+.001f),Describe(source));
            for(int i=0;i<snapshot.Length;i++)
            {
                if(i==2)continue;
                float gap=Vector3.Distance(position,Position(snapshot[i]))-radius-bodyRadii[i];
                minimumGap=Mathf.Min(minimumGap,gap);
                Assert.That(gap,Is.GreaterThanOrEqualTo(-.001f),Describe(source));
            }
        }
        float finalDistance=Vector3.Distance(Position(source),Position(target));
        float wallClearance=mirrorWall?Position(source).z-(bounds.min.z+radius):bounds.max.z-radius-Position(source).z;
        TestContext.WriteLine("Wall escape: reverse="+reverseTeams+", mirror="+mirrorWall+", tight="+tightCorner+", initial gap="+initialDistance+
            ", final gap="+finalDistance+", max gap="+maximumDistance+", min body gap="+minimumGap+
            ", wall clearance="+wallClearance+", attacks="+Get(source,"basicAttackCount")+", damage="+
            (initialHp-(float)Get(target,"hp"))+", "+Describe(source));
        Assert.That(moved,Is.True,"A valid escape route must not be discarded while the forward approach is trapped.");
        Assert.That(leftWall,Is.True,"The actor must leave the boundary rather than remain trapped there.");
        if(tightCorner)Assert.That(escapedCorner,Is.True,"A tight corner requires clearing the blocking body before approaching.");
        Assert.That(approachedAfterEscape,Is.True,"A cached escape must lead back toward the fight instead of repeatedly restarting.");
        Assert.That(maximumDistance,Is.LessThanOrEqualTo(initialDistance+MonsterAutoBattlePrototype.TileSize*2f),
            "The necessary initial retreat must remain a bounded escape.");
        Assert.That(finalDistance,Is.LessThanOrEqualTo(initialDistance-MonsterAutoBattlePrototype.TileSize));
        Assert.That(wallClearance,Is.GreaterThanOrEqualTo(.5f));
        Assert.That(Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That((float)Get(target,"hp"),Is.LessThan(initialHp));
        Assert.That(Get(source,"target"),Is.SameAs(target));
        for(int i=0;i<snapshot.Length;i++)
        {
            Assert.That(Get(snapshot[i],"alive"),Is.EqualTo(true));
            if(i!=2)Assert.That(Position(snapshot[i]),Is.EqualTo(positions[i]));
        }
    }

    [TestCase(false,false)] [TestCase(true,false)]
    [TestCase(false,true)] [TestCase(true,true)]
    public void RankTwoBearInTheOneWolfSixBearSnapshotUsesAnOpenBypassInsteadOfPushingIntoItsNeighbours(
        bool reverseTeams,bool mirror)
    {
        // Natural 1:6 fight at 7.077769s: bear 3 pursued an allegedly clear short segment without moving.
        Vector3[] positions={
            new Vector3(-1.162f,0f,.9730572f),new Vector3(.7626061f,0f,2.34735f),
            new Vector3(-2.011053f,0f,-.9362304f),new Vector3(1.317463f,0f,-.2452068f),
            new Vector3(-.530875f,0f,2.790561f),new Vector3(.01381675f,0f,-.5498057f),
            new Vector3(.7620406f,0f,.9730572f)};
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var snapshot=new object[positions.Length];
        for(int i=0;i<snapshot.Length;i++)
        {
            if(mirror)positions[i].x=-positions[i].x;
            int team=i==0?0:1;if(reverseTeams)team=1-team;
            object actor=Actor(team,positions[i]);snapshot[i]=actor;
            MonsterDefinition monster=database.Get(i==0?"nukgu":"woongi");Assert.That(monster,Is.Not.Null);
            Set(actor,"definition",monster);Set(actor,"collisionRadius",monster.collisionRadius*.84f);
            Set(actor,"combatBodyScale",i==0?1.43f:1.15f);Set(actor,"attackRange",monster.attackRange);
            Set(actor,"skillId",monster.skillId);Set(actor,"speedTilesPerSecond",monster.moveSpeedTilesPerSecond);
            Set(actor,"targetLockTimer",100f);
            if(i>0)Set(actor,"target",snapshot[0]);
        }
        object source=snapshot[3],target=snapshot[0];Set(target,"target",snapshot[6]);
        Vector3 origin=Position(source),nearest=(Vector3)Call("ClosestMeleeAttackPosition",source,target);
        Assert.That(Vector3.Distance(origin,nearest),Is.LessThan((float)Call("CombatBodyRadius",source)*.6f),
            "This reproduces the arrival segment that the old near-origin skip ignored in its entirety.");
        Assert.That(Call("CanKeepMeleeAttackPosition",source,target),Is.False);
        Assert.That(Call("MeleeSlotSegmentClear",source,target,origin,nearest),Is.False,
            "The short radial approach is physically blocked; navigation must use the free side.");
        Assert.That(Call("BuildClosestMeleeRoute",source,target),Is.True);
        var route=(List<Vector3>)Get(source,"route");Vector3 previous=origin;
        foreach(Vector3 point in route)
        {
            Assert.That(Call("MeleeSlotSegmentClear",source,target,previous,point),Is.True);
            previous=point;
        }
        string initialRoute=string.Join(" -> ",route);
        float travelled=0f,maximumDistance=Vector3.Distance(origin,Position(target));
        Vector3 furthest=origin;
        for(int frame=0;frame<480;frame++)
        {
            previous=Position(source);Call("UpdateUnit",source,1f/60f);
            travelled+=Vector3.Distance(previous,Position(source));
            float targetDistance=Vector3.Distance(Position(source),Position(target));
            if(targetDistance>maximumDistance){maximumDistance=targetDistance;furthest=Position(source);}
            for(int i=0;i<snapshot.Length;i++)
            {
                if(i==3)continue;
                float minimum=(float)Call("CombatBodyRadius",source)+(float)Call("CombatBodyRadius",snapshot[i]);
                Assert.That(Vector3.Distance(Position(source),Position(snapshot[i])),Is.GreaterThanOrEqualTo(minimum-.001f),Describe(source));
            }
        }
        TestContext.WriteLine("Wolf/bear snapshot: reverse="+reverseTeams+", mirror="+mirror+", travelled="+travelled+
            ", maximum gap="+maximumDistance+", furthest="+furthest+", initial route="+initialRoute+
            ", attacks="+Get(source,"basicAttackCount")+", "+Describe(source));
        Assert.That(travelled,Is.GreaterThan(.3f),"A free bypass must cause real movement, not repeated pursuit into the same neighbour.");
        Assert.That(maximumDistance,Is.LessThan(Vector3.Distance(origin,Position(target))+MonsterAutoBattlePrototype.TileSize));
        Assert.That(Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Get(source,"target"),Is.SameAs(target));
        for(int i=0;i<snapshot.Length;i++)if(i!=3)Assert.That(Position(snapshot[i]),Is.EqualTo(positions[i]));
    }

    private void Step(bool reverse,float dt)
    {
        for(int i=0;i<actors.Count;i++)
            Call("UpdateUnit",actors[reverse?actors.Count-1-i:i],dt);
    }

    private void AddWalkableGrid()
    {
        Type tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)Field("tiles").GetValue(battle);
        for(int x=-12;x<=12;x++)for(int z=-12;z<=12;z++)
        {
            var coordinate=new Vector2Int(x,z);Vector3 world=(Vector3)Call("GridWorld",coordinate,false);
            if(Mathf.Abs(world.x)>14f||Mathf.Abs(world.z)>10f)continue;
            object tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,coordinate);tileType.GetField("world").SetValue(tile,world);
            tileType.GetField("walkable").SetValue(tile,true);tiles.Add(coordinate,tile);
        }
    }

    private object Actor(int team,Vector3 position)
    {
        object actor=Activator.CreateInstance(unitType,true);var model=Go("Last target actor");model.transform.position=position;
        Set(actor,"model",model);Set(actor,"ring",Go("Last target shadow"));Set(actor,"id",units.Count);
        Set(actor,"stableId","last_target_"+units.Count);Set(actor,"definition",definition);
        Set(actor,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(actor,"alive",true);Set(actor,"hp",5000f);Set(actor,"maxHp",5000f);Set(actor,"damage",4f);
        Set(actor,"attackRange",1);Set(actor,"collisionRadius",.3f);Set(actor,"combatBodyScale",1f);
        Set(actor,"attackInterval",.8f);Set(actor,"speedTilesPerSecond",2f);Set(actor,"skillTimer",100f);
        Set(actor,"skillId","focus_chain");Set(actor,"targetLockTimer",2.4f);
        Set(actor,"lastGroundPosition",position);units.Add(actor);actors.Add(actor);return actor;
    }
    private string Describe(object actor)=>"id="+Get(actor,"id")+", action="+Get(actor,"currentAction")+
        ", position="+Position(actor)+", velocity="+Get(actor,"velocity")+", route="+Get(actor,"routeIndex");
    private Vector3 Position(object actor)=>((GameObject)Get(actor,"model")).transform.position;
    private GameObject Go(string name){var value=new GameObject(name);owned.Add(value);return value;}
    private FieldInfo Field(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Private).Invoke(battle,args);
    private object Get(object actor,string name)=>unitType.GetField(name).GetValue(actor);
    private void Set(object actor,string name,object value)=>unitType.GetField(name).SetValue(actor,value);
}
