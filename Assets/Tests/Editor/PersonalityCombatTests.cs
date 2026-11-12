using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is=UnityEngine.TestTools.Constraints.Is;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class PersonalityCombatTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private IList units;
    private readonly List<GameObject> objects=new List<GameObject>();
    private UnityEngine.Random.State randomState;
    private string language;

    [SetUp] public void Setup()
    {
        randomState=UnityEngine.Random.state;language=GameLocalization.CurrentLanguage;
        var host=Go("Personality combat test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)Field("units").GetValue(battle);
        Field("combatBounds").SetValue(battle,new Bounds(Vector3.zero,new Vector3(40,10,40)));
        Field("expeditionRun").SetValue(battle,new ExpeditionRunSave());
        Field("playerProfile").SetValue(battle,new PlayerProfileSave());
        var tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)Field("tiles").GetValue(battle);
        for(int x=-6;x<=6;x++)for(int z=-6;z<=6;z++)
        {
            var cell=new Vector2Int(x,z);var tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,cell);
            tileType.GetField("world").SetValue(tile,Call("GridWorld",cell,false));
            tileType.GetField("walkable").SetValue(tile,true);tiles.Add(cell,tile);
        }
    }
    [TearDown] public void Cleanup()
    {
        foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);
        objects.Clear();UnityEngine.Random.state=randomState;GameLocalization.SetLanguage(language);
    }
    private FieldInfo Field(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private);
    private GameObject Go(string name){var go=new GameObject(name);objects.Add(go);return go;}
    private object Unit(int team,Vector3 position,string personality="balanced",int range=1)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var model=Go(personality);model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",Go("Ring"));Set(unit,"id",units.Count+1);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",1000f);Set(unit,"maxHp",1000f);
        Set(unit,"collisionRadius",.3f);Set(unit,"attackRange",range);
        Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"attackInterval",1f);
        Set(unit,"skillId","focus_chain");Set(unit,"speedTilesPerSecond",2f);
        Set(unit,"definition",Resources.LoadAll<MonsterDefinition>("MonsterData").First(d=>d.monsterId=="velos"));
        Set(unit,"personalityPolicy",PersonalityCombatPolicy.From(new OwnedMonsterSave{personalityId=personality}));
        units.Add(unit);return unit;
    }
    private void Set(object u,string name,object value)=>unitType.GetField(name).SetValue(u,value);
    private object Get(object u,string name)=>unitType.GetField(name).GetValue(u);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Private).Invoke(battle,args);
    private Vector3 Position(object u)=>((GameObject)Get(u,"model")).transform.position;
    private IList Cues=>(IList)Field("combatFloatTexts").GetValue(battle);
    private string Cue=>Cues.Count==0?"":(string)Cues[Cues.Count-1].GetType().GetField("label").GetValue(Cues[Cues.Count-1]);
    private void Step(object u,int frames=60)
    {for(int frame=0;frame<frames;frame++)Call("UpdateUnit",u,.025f);}

    [Test] public void NearestGridLookupPreservesFirstTieAndLiveTilesWithoutAllocating()
    {
        var tiles=(IDictionary)Field("tiles").GetValue(battle);
        var tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        object first=tiles[new Vector2Int(-6,-6)],second=tiles[new Vector2Int(-6,-5)],bench=tiles[new Vector2Int(-6,-4)];
        tiles.Clear();
        tileType.GetField("world").SetValue(first,new Vector3(-1,30,0));
        tileType.GetField("world").SetValue(second,new Vector3(1,-30,0));
        tileType.GetField("world").SetValue(bench,Vector3.zero);
        tileType.GetField("bench").SetValue(bench,true);
        tiles.Add(new Vector2Int(-6,-6),first);tiles.Add(new Vector2Int(-6,-5),second);tiles.Add(new Vector2Int(-6,-4),bench);
        var nearest=(Func<Vector3,Vector2Int>)typeof(MonsterAutoBattlePrototype)
            .GetMethod("NearestGridCoordinate",Private).CreateDelegate(typeof(Func<Vector3,Vector2Int>),battle);
        object firstInOrder=tiles.Values.Cast<object>().First(value=>value!=bench);
        Assert.That(nearest(Vector3.zero),Is.EqualTo(tileType.GetField("coordinate").GetValue(firstInOrder)));
        tileType.GetField("world").SetValue(second,Vector3.zero);
        Assert.That(nearest(Vector3.up*100),Is.EqualTo(new Vector2Int(-6,-5)),"Tile movement and flattened height must be visible immediately.");
        tileType.GetField("world").SetValue(first,new Vector3(float.NaN,0,0));
        Assert.That(nearest(Vector3.zero),Is.EqualTo(new Vector2Int(-6,-6)),"Keep OrderBy's float comparer behavior for NaN distances.");
        tileType.GetField("bench").SetValue(first,true);
        Assert.That(nearest(Vector3.zero),Is.EqualTo(new Vector2Int(-6,-5)));
        for(int i=0;i<16;i++)nearest(Vector3.zero);
        Assert.That(()=>{for(int i=0;i<1024;i++)nearest(Vector3.zero);},Is.Not.AllocatingGCMemory());
        tiles.Clear();Assert.That(nearest(Vector3.zero),Is.EqualTo(Vector2Int.zero));
    }

    [Test] public void TimidThreatScanDoesNotAllocateAndSeesChangedSupportAndAttackers()
    {
        var unit=Unit(0,Vector3.left*2,"timid",4);var enemy=Unit(1,Vector3.zero);
        Set(unit,"target",enemy);Set(enemy,"target",unit);
        var method=typeof(MonsterAutoBattlePrototype).GetMethod("ShouldRetreatForPersonality",Private);
        var check=Expression.Lambda<Func<bool>>(Expression.Call(Expression.Constant(battle),method,
            Expression.Constant(unit,unitType),Expression.Constant(enemy,unitType))).Compile();
        for(int i=0;i<16;i++)check();
        bool retreated=false;
        Assert.That(()=>{for(int i=0;i<1024;i++)retreated|=check();},Is.Not.AllocatingGCMemory());
        Assert.That(retreated,Is.False);
        var support=Unit(0,Vector3.left*5);
        Assert.That(check(),Is.False,"Nearby support prevents an ordinary mutual-duel retreat.");
        var secondAttacker=Unit(1,new Vector3(-1,0,1));Set(secondAttacker,"target",unit);
        Assert.That(check(),Is.True,"Two nearby attackers still trigger retreat despite support.");
        Assert.That(Get(unit,"personalityRetreatAnchor"),Is.SameAs(support));
        Assert.That(Get(unit,"target"),Is.SameAs(enemy));
        Set(unit,"griffinRootTimer",1f);
        Assert.That(check(),Is.False);Assert.That(Get(unit,"personalityRetreatAnchor"),Is.Null);
    }

    [Test] public void CorruptionTimerBufferKeepsUnitsIndependentAndAddsNoPerKeyAllocations()
    {
        var first=Unit(0,Vector3.zero);var second=Unit(0,Vector3.left*5);
        var firstTimers=(Dictionary<string,float>)Get(first,"corruptTimers");
        var secondTimers=(Dictionary<string,float>)Get(second,"corruptTimers");
        var method=typeof(MonsterAutoBattlePrototype).GetMethod("UpdateCorruptionGreatSuccessEffects",Private);
        var tick=Expression.Lambda<Action>(Expression.Call(Expression.Constant(battle),method,
            Expression.Constant(first,unitType),Expression.Constant(.25f))).Compile();
        firstTimers.Add("active",1f);firstTimers.Add("expired",.1f);secondTimers.Add("other",2f);
        tick();Call("UpdateCorruptionGreatSuccessEffects",second,.25f);
        Assert.That(firstTimers["active"],Is.EqualTo(.75f));Assert.That(firstTimers["expired"],Is.Zero);
        Assert.That(secondTimers["other"],Is.EqualTo(1.75f));Assert.That(secondTimers.Count,Is.EqualTo(1));
        firstTimers.Clear();
        for(int i=0;i<16;i++)tick();
        Assert.That(()=>{for(int i=0;i<128;i++)tick();},Is.Not.AllocatingGCMemory());
        firstTimers.Add("late",1f);tick();Assert.That(firstTimers["late"],Is.EqualTo(.75f));
        for(int i=0;i<16;i++)tick();
        Assert.That(()=>{for(int i=0;i<128;i++)tick();},Is.Not.AllocatingGCMemory(),
            "Populated timer snapshots must reuse their key storage.");
        Set(first,"itemModifiers",null);firstTimers["late"]=1f;tick();
        Assert.That(firstTimers["late"],Is.EqualTo(1f),"Keep the missing-modifier guard.");
    }

    [Test] public void CorruptionThreatScansIgnoreAlliesAndDeadEnemiesAndKeepNewTimerDurations()
    {
        var diagnostics=(Dictionary<string,int>)typeof(CorruptionGreatSuccessDiagnostics)
            .GetField("activations",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        var previous=new Dictionary<string,int>(diagnostics);
        try
        {
            Field("corruptionDiagnosticRunning").SetValue(battle,true);
            var owner=Unit(0,Vector3.zero);var ally=Unit(0,Vector3.left*5);
            var first=Unit(1,Vector3.right);var second=Unit(1,Vector3.right*2);var third=Unit(1,Vector3.right*3);
            foreach(var unit in new[]{ally,first,second,third})Set(unit,"target",owner);
            Set(third,"alive",false);
            var items=(CombatItemModifiers)Get(owner,"itemModifiers");
            items.transformedEffects.Add(new ItemTransformMasterRow{corruptionGreatEffectId="CorruptRoyalFortress"});
            items.transformedEffects.Add(new ItemTransformMasterRow{corruptionGreatEffectId="CorruptTwilightDecoy"});
            Call("UpdateCorruptionGreatSuccessEffects",owner,.25f);
            Assert.That(Get(owner,"corruptUntargetable"),Is.False);
            Assert.That((float)Get(owner,"itemTemporaryDefenseBonus"),Is.Zero);
            Set(third,"alive",true);Call("UpdateCorruptionGreatSuccessEffects",owner,.25f);
            Assert.That(Get(owner,"corruptUntargetable"),Is.True);
            Assert.That((float)Get(owner,"itemTemporaryDefenseBonus"),Is.EqualTo(30f));
            Assert.That((float)Get(owner,"corruptReflectionRatio"),Is.EqualTo(.2f));
            var timers=(Dictionary<string,float>)Get(owner,"corruptTimers");
            Assert.That(timers["royal_reflect"],Is.EqualTo(6f));
            Assert.That(timers["twilight_untargetable"],Is.EqualTo(1.5f));
            foreach(var unit in new[]{first,second,third})Assert.That(Get(unit,"target"),Is.Null);
            Assert.That(Get(ally,"target"),Is.SameAs(owner));
        }
        finally
        {
            diagnostics.Clear();foreach(var pair in previous)diagnostics.Add(pair.Key,pair.Value);
        }
    }

    [TestCase("cooldown")] [TestCase("rank3_lock")] [TestCase("silence")] [TestCase("fear")]
    public void RejectedSkillAttemptsDoNotAllocateOrChangeCombatState(string guard)
    {
        var unit=Unit(0,Vector3.left*2);var enemy=Unit(1,Vector3.zero);Set(unit,"target",enemy);
        Set(unit,"skillId",HaetaeCombatRules.SkillId);Set(unit,"skillTimer",guard=="cooldown"?2f:0f);
        if(guard=="rank3_lock")Set(unit,"rank3SkillLockTimer",2f);
        if(guard=="silence"||guard=="fear")
            ((CombatStatusState)Get(unit,"combatStatuses")).ApplyTimed(
                guard=="silence"?CombatStatusKind.Silence:CombatStatusKind.Fear,2f);
        var call=Expression.Lambda<Func<bool>>(Expression.Call(Expression.Constant(battle),
            typeof(MonsterAutoBattlePrototype).GetMethod("TryUseSkill",Private),
            Expression.Constant(unit,unitType),Expression.Constant(false))).Compile();
        bool accepted=false;
        TestDelegate work=()=>{for(int i=0;i<256;i++)accepted|=call();};
        work();Assert.That(work,Is.Not.AllocatingGCMemory());
        Assert.That(accepted,Is.False);Assert.That(Get(unit,"target"),Is.SameAs(enemy));
        Assert.That(Get(unit,"skillTimer"),Is.EqualTo(guard=="cooldown"?2f:0f));
        Assert.That(Get(unit,"skillUses"),Is.EqualTo(0));Assert.That(Get(unit,"hp"),Is.EqualTo(1000f));
    }

    [Test] public void EmptyItemPeriodicUpdatesDoNotAllocateAndIncomingBurnStillTicksAndExpires()
    {
        var unit=Unit(0,Vector3.zero);var enemy=Unit(1,Vector3.right);
        var tick=Expression.Lambda<Action>(Expression.Call(Expression.Constant(battle),
            typeof(MonsterAutoBattlePrototype).GetMethod("UpdateItemPeriodicEffects",Private),
            Expression.Constant(unit,unitType),Expression.Constant(.1f),Expression.Constant(false))).Compile();
        TestDelegate work=()=>{for(int i=0;i<256;i++)tick();};
        work();Assert.That(work,Is.Not.AllocatingGCMemory());
        Field("mlEpisodeActive").SetValue(battle,true);
        Set(unit,"itemBurnSource",enemy);Set(unit,"itemBurnTimer",.05f);
        Set(unit,"itemBurnTickTimer",0f);Set(unit,"itemBurnRatio",.1f);Set(unit,"itemHealingReduction",.2f);
        tick();
        Assert.That((float)Get(unit,"hp"),Is.LessThan(1000f),"An incoming burn must tick even without equipped effects.");
        Assert.That(Get(unit,"itemBurnTimer"),Is.EqualTo(0f));Assert.That(Get(unit,"itemBurnSource"),Is.Null);
        Assert.That(Get(unit,"itemBurnRatio"),Is.EqualTo(0f));Assert.That(Get(unit,"itemHealingReduction"),Is.EqualTo(0f));
    }

    [Test] public void PeriodicItemTargetScansKeepHealthRatioAndDistanceTiesAndReadLiveChanges()
    {
        var owner=Unit(0,Vector3.zero);Set(owner,"hp",800f);
        var firstAlly=Unit(0,Vector3.forward);Set(firstAlly,"maxHp",2000f);Set(firstAlly,"hp",1000f);
        var secondAlly=Unit(0,Vector3.back);Set(secondAlly,"hp",500f);
        var clone=Unit(0,Vector3.forward*2);Set(clone,"augmentShadowClone",true);Set(clone,"hp",1f);
        var firstEnemy=Unit(1,Vector3.right);var secondEnemy=Unit(1,Vector3.left);
        var items=(CombatItemModifiers)Get(owner,"itemModifiers");
        items.specialEffects.Add(new ItemMasterRow{specialEffect="PeriodicAllyHeal",effectParam1=.1f,effectParam2=-1f,effectParam3=4f});
        items.specialEffects.Add(new ItemMasterRow{specialEffect="SunCarapace",effectParam1=.05f,effectParam2=3f,effectParam3=.2f});
        Call("UpdateItemPeriodicEffects",owner,.1f,false);
        Assert.That(Get(firstAlly,"hp"),Is.EqualTo(1200f));Assert.That(Get(secondAlly,"hp"),Is.EqualTo(500f));
        Assert.That(Get(clone,"hp"),Is.EqualTo(1f));Assert.That(Get(owner,"hp"),Is.EqualTo(800f));
        Assert.That(Get(firstEnemy,"itemBurnSource"),Is.SameAs(owner));Assert.That(Get(secondEnemy,"itemBurnSource"),Is.Null);
        Assert.That(Get(owner,"itemPeriodicAllyHealTimer"),Is.EqualTo(.1f));Assert.That(Get(owner,"itemSunTimer"),Is.EqualTo(2f));
        Set(firstAlly,"hp",1800f);Set(firstEnemy,"alive",false);Set(owner,"itemSunTimer",0f);
        Call("UpdateItemPeriodicEffects",owner,.1f,false);
        Assert.That(Get(firstAlly,"hp"),Is.EqualTo(1800f));Assert.That(Get(secondAlly,"hp"),Is.EqualTo(600f));
        Assert.That(Get(secondEnemy,"itemBurnSource"),Is.SameAs(owner));
    }

    [TestCase("vengeful")] [TestCase("fickle")] [TestCase("protective")]
    [TestCase("bold")] [TestCase("timid")]
    public void OrdinaryTargetChoiceStartsWithNearestEnemy(string personality)
    {
        var u=Unit(0,Vector3.left*3,personality);
        var near=Unit(1,Vector3.zero);Unit(1,Vector3.right*5);
        Assert.That(Call("SelectTarget",u,false,null),Is.SameAs(near));
    }
    [Test] public void ExistingEngagementDoesNotSwitchWhenAnotherEnemyBecomesCloser()
    {
        var u=Unit(0,Vector3.left*2,"vengeful",4);
        var engaged=Unit(1,Vector3.zero);Unit(1,new Vector3(-1,0,1));Set(u,"target",engaged);
        Step(u);Assert.That(Get(u,"target"),Is.SameAs(engaged));Assert.That(Cues.Count,Is.Zero);
    }
    [Test] public void RevengeWaitsForAttackToFinishThenCommitsToNearbyAttacker()
    {
        var u=Unit(0,Vector3.left*2,"vengeful",4);
        var engaged=Unit(1,Vector3.zero);var attacker=Unit(1,new Vector3(0,0,2));Set(u,"target",engaged);
        Set(u,"attackMovementLockTimer",.4f);
        Call("HandlePersonalityDamageReaction",attacker,u,50f,50f);
        Assert.That(Get(u,"target"),Is.SameAs(engaged));
        Call("UpdateUnit",u,.1f);Assert.That(Get(u,"target"),Is.SameAs(engaged));
        Step(u,20);Assert.That(Get(u,"target"),Is.SameAs(attacker));
        Assert.That((float)Get(u,"targetLockTimer"),Is.GreaterThan(1f));Assert.That(Cue,Is.EqualTo("!"));
        Call("HandlePersonalityDamageReaction",engaged,u,100f,100f);
        Assert.That(Get(u,"personalityPendingTarget"),Is.Null);
    }
    [TestCase("lock")] [TestCase("taunt")] [TestCase("prey")] [TestCase("far")]
    public void RevengeCannotBreakPriorityOrChaseFarEnemies(string guard)
    {
        var u=Unit(0,Vector3.left*2,"vengeful");var current=Unit(1,Vector3.zero);
        var attacker=Unit(1,guard=="far"?Vector3.right*12:new Vector3(0,0,1));Set(u,"target",current);
        if(guard=="lock")Set(u,"targetLockTimer",1f);
        if(guard=="taunt"){Set(u,"forcedTarget",current);Set(u,"forcedTargetTimer",5f);}
        if(guard=="prey"){Set(u,"preyFocusTarget",current);Set(u,"preyFocusTimer",5f);}
        Call("HandlePersonalityDamageReaction",attacker,u,100f,100f);
        Assert.That(Get(u,"personalityPendingTarget"),Is.Null);Assert.That(Get(u,"target"),Is.SameAs(current));
    }
    [Test] public void FickleCanTakeOneShortStepToAnAlternateAfterThreeAttacks()
    {
        var u=Unit(0,Vector3.left*2,"fickle");var current=Unit(1,Vector3.zero);
        var alternate=Unit(1,new Vector3(0,0,2));Set(u,"target",current);Set(u,"basicAttackCount",3);
        Assert.That(Vector3.Distance(Position(u),Position(alternate)),Is.GreaterThan((float)Call("AttackDistance",u,alternate)));
        Call("TryRotateFickleTarget",u,current);Call("UpdatePersonalityReaction",u);
        Assert.That(Get(u,"target"),Is.SameAs(alternate));Assert.That(Cue,Is.EqualTo("!"));
    }
    [TestCase("taunt")] [TestCase("prey")]
    public void NewPriorityCancelsAnAlreadyQueuedReaction(string priority)
    {
        var u=Unit(0,Vector3.left*2,"vengeful",4);var current=Unit(1,Vector3.zero);
        var attacker=Unit(1,new Vector3(0,0,2));Set(u,"target",current);
        Call("HandlePersonalityDamageReaction",attacker,u,50f,50f);
        Assert.That(Get(u,"personalityPendingTarget"),Is.SameAs(attacker));
        if(priority=="taunt"){Set(u,"forcedTarget",current);Set(u,"forcedTargetTimer",5f);}
        else {Set(u,"preyFocusTarget",current);Set(u,"preyFocusTimer",5f);}
        Call("UpdatePersonalityReaction",u);
        Assert.That(Get(u,"personalityPendingTarget"),Is.Null);Assert.That(Get(u,"target"),Is.SameAs(current));
        Assert.That(Cues.Count,Is.Zero);
    }
    [Test] public void ExpiredReactionCannotRetargetAfterALongAction()
    {
        var u=Unit(0,Vector3.left*2,"vengeful",4);var current=Unit(1,Vector3.zero);
        var attacker=Unit(1,new Vector3(0,0,2));Set(u,"target",current);
        Call("HandlePersonalityDamageReaction",attacker,u,50f,50f);Set(u,"personalityPendingTimer",0f);
        Call("UpdatePersonalityReaction",u);Assert.That(Get(u,"target"),Is.SameAs(current));Assert.That(Cues.Count,Is.Zero);
    }
    [Test] public void FocusedTimidCanRetreatDespiteNearbySupport()
    {
        Assert.That(PersonalityCombatPolicy.ShouldTimidRetreat(false,true,true,true,true,2),Is.True);
        Assert.That(PersonalityCombatPolicy.ShouldTimidRetreat(false,true,true,true,true,1,true),Is.True);
        Assert.That(PersonalityCombatPolicy.ShouldTimidRetreat(false,true,false,true,true,2),Is.False);
    }
    [Test] public void BusyProtectorDoesNotPreventAnotherFromResponding()
    {
        var enemy=Unit(1,Vector3.zero);var other=Unit(1,new Vector3(0,0,3));
        var ally=Unit(0,new Vector3(-1,0,1));Set(ally,"hp",500f);
        var busy=Unit(0,new Vector3(-2,0,1),"protective",4);
        var ready=Unit(0,new Vector3(-3,0,1.5f),"protective",4);
        Set(busy,"target",enemy);Set(busy,"personalityPendingTarget",enemy);Set(busy,"personalityPendingTimer",1f);
        Set(ready,"target",other);Call("HandlePersonalityDamageReaction",enemy,ally,10f,10f);
        Assert.That(Get(ready,"personalityPendingTarget"),Is.SameAs(enemy));
    }
    [Test] public void FickleKeepsCurrentTargetWithoutNearbyAlternative()
    {
        var u=Unit(0,Vector3.left*2,"fickle");var current=Unit(1,Vector3.zero);
        Unit(1,Vector3.right*12);Set(u,"target",current);Set(u,"basicAttackCount",3);
        Call("TryRotateFickleTarget",u,current);Call("UpdatePersonalityReaction",u);
        Assert.That(Get(u,"target"),Is.SameAs(current));Assert.That(Cues.Count,Is.Zero);
    }
    [Test] public void CooperativeJoinsNearbyAllyFocusAndKeepsThatEngagement()
    {
        var u=Unit(0,Vector3.left*3,"cooperative",4);var current=Unit(1,Vector3.zero);
        var focus=Unit(1,new Vector3(0,0,2));var ally=Unit(0,new Vector3(-2,0,3));
        Set(u,"target",current);Set(ally,"target",focus);Call("UpdatePersonalityReaction",u);
        Assert.That(Get(u,"target"),Is.SameAs(focus));Assert.That(Cue,Is.EqualTo("!"));
        Set(ally,"target",current);Call("UpdatePersonalityReaction",u);
        Assert.That(Get(u,"target"),Is.SameAs(focus));
    }
    [Test] public void ProtectiveMovesTowardThreatenedAllyWhileKeepingSharedEnemy()
    {
        var u=Unit(0,new Vector3(-2,0,-.8f),"protective",4);var enemy=Unit(1,Vector3.zero);
        var ally=Unit(0,new Vector3(-1,0,1.6f));Set(ally,"hp",550f);Set(u,"target",enemy);Set(enemy,"target",ally);
        Vector3 before=Position(u);Call("HandlePersonalityDamageReaction",enemy,ally,10f,10f);
        Call("UpdatePersonalityReaction",u);
        Assert.That(Get(u,"target"),Is.SameAs(enemy));Assert.That((float)Get(u,"personalityMoveTimer"),Is.GreaterThan(0f));
        Vector3 destination=(Vector3)Get(u,"personalityMovePosition");
        Assert.That(Vector3.Distance(destination,Position(ally)),Is.LessThan(Vector3.Distance(before,Position(ally))));
        Step(u,30);Assert.That(Vector3.Distance(Position(u),before),Is.GreaterThan(.2f));Assert.That(Cue,Is.EqualTo("!"));
    }
    [Test] public void BoldAdvancesWithinRangeWithoutChangingEnemy()
    {
        var u=Unit(0,Vector3.left*3,"bold",4);var enemy=Unit(1,Vector3.zero);Set(u,"target",enemy);
        Vector3 before=Position(u);Step(u,35);
        Assert.That(Get(u,"target"),Is.SameAs(enemy));Assert.That(Position(u).magnitude,Is.LessThan(before.magnitude-.3f));
        Assert.That(Cue,Is.EqualTo("!"));
    }
    [Test] public void TimidRetreatsToSaferAllyAndKeepsItsEnemy()
    {
        var u=Unit(0,Vector3.left*2,"timid",4);var enemy=Unit(1,Vector3.zero);
        Unit(0,Vector3.left*6);Set(u,"target",enemy);Set(enemy,"target",u);
        Vector3 before=Position(u);Step(u,25);
        Assert.That(Get(u,"target"),Is.SameAs(enemy));Assert.That(Position(u).magnitude,Is.GreaterThan(before.magnitude+.2f));
        Assert.That(Cue,Is.EqualTo("!"));
    }
    [Test] public void LastTimidAllyStaysInCombat()
    {
        var u=Unit(0,Vector3.left*2,"timid",4);var enemy=Unit(1,Vector3.zero);
        Set(u,"target",enemy);Set(enemy,"target",u);
        Assert.That(Call("ShouldRetreatForPersonality",u,enemy),Is.False);Assert.That(Cues.Count,Is.Zero);
    }
    [Test] public void DistantEnemyDoesNotMakeTimidRetreatBeforeEngagement()
    {
        var u=Unit(0,Vector3.zero,"timid");var enemy=Unit(1,Vector3.right*12);
        Unit(0,Vector3.left*4);Set(u,"target",enemy);Set(enemy,"target",u);
        Assert.That(Call("ShouldRetreatForPersonality",u,enemy),Is.False);Assert.That(Cues.Count,Is.Zero);
    }
    [TestCase("vengeful","ko")] [TestCase("vengeful","en")]
    [TestCase("fickle","ko")] [TestCase("fickle","en")]
    [TestCase("cooperative","ko")] [TestCase("cooperative","en")]
    [TestCase("protective","ko")] [TestCase("protective","en")]
    [TestCase("bold","ko")] [TestCase("bold","en")]
    [TestCase("timid","ko")] [TestCase("timid","en")]
    public void PersonalityCuesUseOnlyExclamationAndAreThrottled(string personality,string language)
    {
        GameLocalization.SetLanguage(language);
        var u=Unit(0,Vector3.zero,personality);Call("ShowPersonalityCue",u);Call("ShowPersonalityCue",u);
        Assert.That(Cues.Count,Is.EqualTo(1));Assert.That(Cue,Is.EqualTo("!"));
    }
}
