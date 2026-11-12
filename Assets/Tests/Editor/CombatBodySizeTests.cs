using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CombatBodySizeTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> objects=new List<GameObject>();
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private IList units;
    private MonsterDatabase database;
    private ExpeditionRunSave run;
    private UnityEngine.Random.State randomState;
    private bool previousEffectTest;

    [SetUp] public void Setup()
    {
        randomState=UnityEngine.Random.state;
        previousEffectTest=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=false;
        var host=Go("Combat body size test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        units=(IList)Field("units").GetValue(battle);
        database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        run=new ExpeditionRunSave{runId="combat_body_size",regionId=HunterRegionCatalog.FirstRegionId,
            difficulty=ExpeditionDifficulty.Beginner};
        Field("expeditionRun").SetValue(battle,run);
        Field("monsterDatabase").SetValue(battle,database);
        Field("playerProfile").SetValue(battle,new PlayerProfileSave());
        Field("combatBounds").SetValue(battle,new Bounds(Vector3.zero,new Vector3(28f,10f,20f)));
        Field("mlEpisodeActive").SetValue(battle,true);
    }

    [TearDown] public void Cleanup()
    {
        for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)Object.DestroyImmediate(objects[i]);
        objects.Clear();UnityEngine.Random.state=randomState;
        CombatEffectTestConfiguration.Active=previousEffectTest;
    }

    [TestCase(0,1,1f)] [TestCase(0,2,1.15f)] [TestCase(0,3,1.3f)]
    [TestCase(1,1,1f)] [TestCase(1,2,1.15f)] [TestCase(1,3,1.3f)]
    public void BothTeamsRankScalingChangesBodyOccupancyWithoutChangingTheAuthoredHitRadius(int team,int rank,float expected)
    {
        var unit=Unit(team,Vector3.zero,.5f);
        ApplyScale(unit,(ExpeditionMonsterRank)rank,1f);
        Assert.That(Number(unit,"combatBodyScale"),Is.EqualTo(expected).Within(.0001f));
        Assert.That(Radius(unit),Is.EqualTo(.5f*expected).Within(.0001f));
        Assert.That(Number(unit,"collisionRadius"),Is.EqualTo(.5f));
        ApplyScale(unit,ExpeditionMonsterRank.Rank1,1f);
        Assert.That(Number(unit,"combatBodyScale"),Is.EqualTo(1f));
        Assert.That(Radius(unit),Is.EqualTo(.5f));
    }

    [TestCase(0)] [TestCase(1)]
    public void AugmentSizeIsStableAcrossRepeatedRefreshAndTemporaryPresentationScale(int team)
    {
        var unit=Unit(team,Vector3.zero,.4f);
        for(int pass=0;pass<3;pass++)
        {
            ApplyScale(unit,ExpeditionMonsterRank.Rank3,2f);
            Assert.That(Number(unit,"combatBodyScale"),Is.EqualTo(2.6f).Within(.0001f));
            Assert.That(Radius(unit),Is.EqualTo(1.04f).Within(.0001f));
        }
        Model(unit).transform.localScale*=3f;
        Assert.That(Radius(unit),Is.EqualTo(1.04f).Within(.0001f),
            "A cinematic or action pose must not become the movement footprint.");
        ApplyScale(unit,ExpeditionMonsterRank.Rank1,1f);
        Assert.That(Radius(unit),Is.EqualTo(.4f).Within(.0001f));
        Assert.That(Number(unit,"collisionRadius"),Is.EqualTo(.4f));
    }

    [Test]
    public void RealGuardianPreviewProgressionAddsItsSizeOnceAndRestoresTheFinalGuardianSize()
    {
        run.wave=8;
        var previewRoster=Roster(8);var definition=previewRoster[0];
        var owned=PlayerProfileStore.CreateMonster(definition,"body_guardian",false);
        var preview=Unit(1,Vector3.left*4f);Set(preview,"definition",definition);Set(preview,"ownedInstance",owned);
        Call("ConfigureEnemyProgression",preview,0,previewRoster);
        run.wave=12;
        var final=Unit(1,Vector3.right*4f);Set(final,"definition",definition);Set(final,"ownedInstance",owned);
        Call("ConfigureEnemyProgression",final,0,Roster(12));
        Assert.That(Get(preview,"guardianPreview"),Is.True);Assert.That(Get(final,"guardianPreview"),Is.False);
        float expected=Radius(final)*1.6f;
        Assert.That(Radius(preview),Is.EqualTo(expected).Within(.0001f));
        Assert.That(Number(preview,"collisionRadius"),Is.EqualTo(Number(final,"collisionRadius")));
        run.wave=8;
        Call("RefreshUnitFromOwnedData",preview,definition,owned);
        Call("RefreshUnitFromOwnedData",preview,definition,owned);
        Assert.That(Radius(preview),Is.EqualTo(expected).Within(.0001f));
    }

    [TestCase(false)] [TestCase(true)]
    public void ClearanceIncludesBothScaledBodiesAndTheExistingTeamPadding(bool sameTeam)
    {
        var source=Unit(0,Vector3.left*4f,.4f);var other=Unit(sameTeam?0:1,Vector3.zero,.3f);
        ApplyScale(source,ExpeditionMonsterRank.Rank1,2f);
        ApplyScale(other,ExpeditionMonsterRank.Rank1,1f);
        float expected=.4f*2f+.3f+.24f*1.5f;
        Assert.That((float)Call("BodyClearance",source,other,.24f),Is.EqualTo(expected).Within(.0001f));
        Assert.That(Number(source,"collisionRadius"),Is.EqualTo(.4f));
        Assert.That(Number(other,"collisionRadius"),Is.EqualTo(.3f));
    }

    [TestCase(0,1f)] [TestCase(1,1f)] [TestCase(0,1.3f)] [TestCase(1,1.3f)]
    public void AlliesCanPassBetweenFixedBodiesWithoutChangingMeleeContactOrDamageReach(int team,float size)
    {
        var source=Unit(team,Vector3.left*(4f*size));
        var target=Unit(1-team,Vector3.right*(4f*size));
        var upper=Unit(team,Vector3.forward*(.95f*size));
        var lower=Unit(team,Vector3.back*(.95f*size));
        foreach(object actor in new[]{source,target,upper,lower})ApplyScale(actor,ExpeditionMonsterRank.Rank1,size);
        Set(source,"target",target);Field("battleStarted").SetValue(battle,true);
        Vector3 upperPosition=Position(upper),lowerPosition=Position(lower),targetPosition=Position(target);
        float contact=(.6f+.24f+MonsterAutoBattlePrototype.TileSize*.22f)*size;
        float arrival=contact+.6f*size;
        float reach=MonsterAutoBattlePrototype.AttackDistanceForRange(1,.3f*size,.3f*size);
        Assert.That((float)Call("MeleeContactDistance",source,target),Is.EqualTo(contact).Within(.0001f));
        Assert.That((float)Call("MeleeArrivalDistance",source,target),Is.EqualTo(arrival).Within(.0001f));
        Assert.That((float)Call("AttackDistance",source,target),Is.EqualTo(reach).Within(.0001f));
        Assert.That(.95f*size,Is.LessThan((.6f+.24f+MonsterAutoBattlePrototype.TileSize*.22f)*size),
            "The former allied vacancy margin blocked this physically open passage.");
        Vector3 destination=(Vector3)Call("ClosestMeleeAttackPosition",source,target);
        Assert.That(Call("MeleeSlotSegmentClear",source,target,Position(source),destination),Is.True,
            "The reduced allied movement padding must open the direct passage.");
        bool crossed=false;float maximumLateral=0f;
        for(int frame=0;frame<300;frame++)
        {
            Vector3 previous=Position(source);int attacks=(int)Get(source,"basicAttackCount");
            Call("UpdateUnit",source,1f/60f);
            Vector3 position=Position(source);maximumLateral=Mathf.Max(maximumLateral,Mathf.Abs(position.z));
            crossed|=previous.x<0f&&position.x>=0f;
            foreach(object other in new[]{target,upper,lower})
                Assert.That(Vector3.Distance(position,Position(other)),
                    Is.GreaterThanOrEqualTo(Radius(source)+Radius(other)-.001f));
            if((int)Get(source,"basicAttackCount")>attacks)
                Assert.That(Vector3.Distance(position,Position(target)),Is.LessThanOrEqualTo(arrival+.026f));
        }
        Assert.That(crossed,Is.True,"The actor must cross between the fixed allies before reaching the target.");
        Assert.That(maximumLateral,Is.LessThanOrEqualTo(.05f*size),"An arena detour must not substitute for the open passage.");
        Assert.That((int)Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Number(target,"hp"),Is.LessThan(5000f));
        Assert.That(Position(upper),Is.EqualTo(upperPosition));Assert.That(Position(lower),Is.EqualTo(lowerPosition));
        Assert.That(Position(target),Is.EqualTo(targetPosition));
        foreach(object actor in new[]{source,target,upper,lower})
        {
            Assert.That(Number(actor,"collisionRadius"),Is.EqualTo(.3f));
            Assert.That(Radius(actor),Is.EqualTo(.3f*size).Within(.0001f));
        }
        Assert.That((float)Call("MeleeContactDistance",source,target),Is.EqualTo(contact).Within(.0001f));
        Assert.That((float)Call("MeleeArrivalDistance",source,target),Is.EqualTo(arrival).Within(.0001f));
        Assert.That((float)Call("AttackDistance",source,target),Is.EqualTo(reach).Within(.0001f));
    }

    [TestCase(0,1.3f)] [TestCase(1,1.3f)] [TestCase(0,2f)] [TestCase(1,2f)]
    public void ScaledBodiesUseTheirActualApproachDistanceAndDealBasicDamage(int team,float size)
    {
        var target=Unit(1-team,Vector3.zero,.45f);var source=Unit(team,Vector3.left*8f,.35f);
        Set(source,"target",target);ApplyScale(source,ExpeditionMonsterRank.Rank1,size);
        ApplyScale(target,ExpeditionMonsterRank.Rank1,1.6f);
        float reach=(float)Call("AttackDistance",source,target);
        Assert.That(reach,Is.EqualTo(MonsterAutoBattlePrototype.AttackDistanceForRange(1,Radius(source),Radius(target))).Within(.0001f));
        float contact=(float)Call("MeleeContactDistance",source,target);
        float arrival=(float)Call("MeleeArrivalDistance",source,target);
        Assert.That(arrival,Is.GreaterThanOrEqualTo(contact).And.LessThanOrEqualTo(reach));
        Field("battleStarted").SetValue(battle,true);
        for(int frame=0;frame<300;frame++)
        {
            int attacks=(int)Get(source,"basicAttackCount");
            Call("UpdateUnit",source,1f/60f);
            float distance=Vector3.Distance(Position(source),Position(target));
            Assert.That(distance,Is.GreaterThanOrEqualTo(Radius(source)+Radius(target)-.001f));
            if((int)Get(source,"basicAttackCount")>attacks)
                Assert.That(distance,Is.LessThanOrEqualTo(arrival+.026f));
        }
        Assert.That(Call("IsImmediatelyAttackable",source,target),Is.True);
        Assert.That((int)Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Number(target,"hp"),Is.LessThan(5000f));
        Assert.That(Vector3.Distance(Position(source),Position(target)),Is.InRange(contact-.002f,arrival+.026f));
    }

    [Test]
    public void MountainGrowthAndItsEndRestoreTheStableRankAndAugmentSizeWithoutCompounding()
    {
        var unit=Unit(0,Vector3.zero,.5f);
        Set(unit,"definition",database.monsters.First(definition=>definition!=null&&definition.familyId=="bear"));
        ApplyScale(unit,ExpeditionMonsterRank.Rank3,2f);float original=Radius(unit);
        Set(unit,"bearResolveTimer",10f);
        for(int pass=0;pass<2;pass++)
        {
            Call("ActivateBearMountainColossus",unit,5f);
            Call("UpdateBearMountainScale",unit,BearCombatRules.MountainScaleTransitionDuration);
            Assert.That(Radius(unit),Is.EqualTo(original*BearCombatRules.MountainVisualScale).Within(.0001f));
            Call("EndBearMountainColossus",unit,true);
            Assert.That(Radius(unit),Is.EqualTo(original).Within(.0001f));
        }
        Assert.That(Number(unit,"collisionRadius"),Is.EqualTo(.5f));
    }

    [TestCase(false)] [TestCase(true)]
    public void GrowingEitherBodyDuringApproachUsesTheLargerContactDistanceWithoutOverlap(bool growTarget)
    {
        var target=Unit(1,Vector3.zero);
        var source=Unit(0,Vector3.left*8f);Set(source,"target",target);
        Field("battleStarted").SetValue(battle,true);
        for(int frame=0;frame<30;frame++)Call("UpdateUnit",source,1f/60f);
        float previous=(float)Call("MeleeContactDistance",source,target);
        Vector3 before=Position(source),targetBefore=Position(target);
        if(growTarget)ApplyScale(target,ExpeditionMonsterRank.Rank1,4f);
        else ApplyScale(source,ExpeditionMonsterRank.Rank1,2f);
        float contact=(float)Call("MeleeContactDistance",source,target);
        Assert.That(contact,Is.GreaterThan(previous));
        Assert.That(Position(source),Is.EqualTo(before));Assert.That(Position(target),Is.EqualTo(targetBefore));
        for(int frame=0;frame<300;frame++)
        {
            Call("UpdateUnit",source,1f/60f);
            Assert.That(Vector3.Distance(Position(source),Position(target)),Is.GreaterThanOrEqualTo(Radius(source)+Radius(target)-.001f));
        }
        Assert.That(Vector3.Distance(Position(source),Position(target)),Is.InRange(contact-.002f,
            (float)Call("MeleeArrivalDistance",source,target)+.026f));
        Assert.That(Get(source,"basicAttackCount"),Is.GreaterThan(0));
        Assert.That(Number(target,"hp"),Is.LessThan(5000f));
    }

    [Test]
    public void ASizeChangeUpdatesApproachSpacingWithoutMovingTheActorOrChangingBasicDamage()
    {
        var target=Unit(1,Vector3.zero);var source=Unit(0,Vector3.left*8f);Set(source,"target",target);
        ApplyScale(source,ExpeditionMonsterRank.Rank1,1.9f);
        float contact=(float)Call("MeleeContactDistance",source,target),damage=Number(source,"damage");
        Vector3 original=Position(source);var beforeRandom=UnityEngine.Random.state;
        float before=BasicDamageFromContact(source,target);
        Model(source).transform.position=original+Vector3.up*Number(source,"visualHeightOffset");
        ApplyScale(source,ExpeditionMonsterRank.Rank1,2f);
        Assert.That(Position(source),Is.EqualTo(original));
        Assert.That((float)Call("MeleeContactDistance",source,target),Is.GreaterThan(contact));
        Assert.That(Number(source,"damage"),Is.EqualTo(damage));
        UnityEngine.Random.state=beforeRandom;
        Assert.That(BasicDamageFromContact(source,target),Is.EqualTo(before).Within(.0001f));
    }

    [Test]
    public void SizeGrowthDoesNotExpandAuthoredAreaHitsOrSkillPower()
    {
        var source=Unit(0,Vector3.zero);var target=Unit(1,Vector3.right*1.4f);
        float power=(float)Call("SkillPower",source);float radius=Number(target,"collisionRadius");
        Assert.That((IList)Call("Rank3Enemies",source,Vector3.zero,1f),Is.Empty);
        ApplyScale(source,ExpeditionMonsterRank.Rank3,2f);
        ApplyScale(target,ExpeditionMonsterRank.Rank3,2f);
        Assert.That((IList)Call("Rank3Enemies",source,Vector3.zero,1f),Is.Empty);
        Model(target).transform.position=Vector3.right*1.25f;
        Assert.That((IList)Call("Rank3Enemies",source,Vector3.zero,1f),Has.Count.EqualTo(1));
        Assert.That((float)Call("SkillPower",source),Is.EqualTo(power).Within(.0001f));
        Assert.That(Number(target,"collisionRadius"),Is.EqualTo(radius));
    }

    [Test]
    public void MixedSizedAttackersKeepTheirSpaceAndAllJoinTheFight()
    {
        var tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)Field("tiles").GetValue(battle);
        for(int x=-12;x<=12;x++)for(int z=-12;z<=12;z++)
        {
            var cell=new Vector2Int(x,z);var tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,cell);
            tileType.GetField("world").SetValue(tile,Call("GridWorld",cell,false));
            tileType.GetField("walkable").SetValue(tile,true);tiles.Add(cell,tile);
        }
        var target=Unit(1,Vector3.zero,.5f);ApplyScale(target,ExpeditionMonsterRank.Rank1,2f);
        Set(target,"meleeSlotReady",false);
        float[] scales={1.15f,1.3f,1.6f,2f};var attackers=new List<object>();
        for(int i=0;i<scales.Length;i++)
        {
            var source=Unit(0,new Vector3(-8f,0f,(i-1.5f)*3f));Set(source,"target",target);
            ApplyScale(source,ExpeditionMonsterRank.Rank1,scales[i]);attackers.Add(source);
        }
        Field("battleStarted").SetValue(battle,true);
        for(int frame=0;frame<420;frame++)
        {
            foreach(object source in attackers)
            {
                int attacks=(int)Get(source,"basicAttackCount");
                Call("UpdateUnit",source,1f/60f);
                if((int)Get(source,"basicAttackCount")>attacks)
                    Assert.That(Vector3.Distance(Position(source),Position(target)),
                        Is.LessThanOrEqualTo((float)Call("MeleeArrivalDistance",source,target)+.026f));
                Assert.That(Vector3.Distance(Position(source),Position(target)),Is.GreaterThanOrEqualTo(Radius(source)+Radius(target)-.001f));
            }
            for(int a=0;a<attackers.Count;a++)for(int b=a+1;b<attackers.Count;b++)
            {
                float minimum=(float)Call("BodyClearance",attackers[a],attackers[b],.025f);
                Assert.That(Vector3.Distance(Position(attackers[a]),Position(attackers[b])),Is.GreaterThanOrEqualTo(minimum-.002f));
            }
        }
        foreach(object source in attackers)
        {
            Assert.That((int)Get(source,"basicAttackCount"),Is.GreaterThan(0));
            Assert.That(Call("IsImmediatelyAttackable",source,target),Is.True);
            Assert.That(Number(source,"damage"),Is.EqualTo(20f));
        }
    }

    private List<MonsterDefinition> Roster(int wave)=>HunterRegionCatalog.BuildExpeditionWaveMonsterIds(
        run.regionId,wave,GameMasterData.WaveScaling(run.regionId,wave).enemyCount,run.runId,database).Select(database.Get).ToList();
    private void ApplyScale(object unit,ExpeditionMonsterRank rank,float augment)
    {
        Set(unit,"expeditionRank",rank);Call("ApplyRankVisualScale",unit,null,rank,augment);
    }
    private object Unit(int team,Vector3 position,float radius=.3f)
    {
        object unit=Activator.CreateInstance(unitType,true);var model=Go("Body size actor");
        model.transform.position=position+Vector3.up*MonsterAutoBattlePrototype.PixelMonsterGroundOffset;
        Set(unit,"model",model);Set(unit,"ring",Go("Body size shadow"));Set(unit,"id",units.Count);
        Set(unit,"stableId","body_size_"+units.Count);Set(unit,"definition",database.Get("velos"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",20f);
        Set(unit,"attackRange",1);Set(unit,"attackInterval",.8f);Set(unit,"cooldown",0f);
        Set(unit,"collisionRadius",radius);Set(unit,"speedTilesPerSecond",2f);Set(unit,"skillTimer",100f);
        Set(unit,"skillId","focus_chain");Set(unit,"meleeSlotReady",true);Set(unit,"targetLockTimer",2.4f);
        Set(unit,"baseModelScale",Vector3.one);Set(unit,"lastGroundPosition",position);
        Set(unit,"baseVisualHeightOffset",MonsterAutoBattlePrototype.PixelMonsterGroundOffset);
        Set(unit,"visualHeightOffset",MonsterAutoBattlePrototype.PixelMonsterGroundOffset);
        units.Add(unit);return unit;
    }
    private GameObject Go(string name){var value=new GameObject(name);objects.Add(value);return value;}
    private FieldInfo Field(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Private);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Private).Invoke(battle,args);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private float Number(object unit,string field)=>(float)Get(unit,field);
    private float Radius(object unit)=>(float)Call("CombatBodyRadius",unit);
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private Vector3 Position(object unit){Vector3 value=Model(unit).transform.position;value.y=0f;return value;}
    private float BasicDamageFromContact(object source,object target)
    {
        float contact=(float)Call("MeleeContactDistance",source,target);
        Model(source).transform.position=Position(target)+Vector3.left*contact+Vector3.up*Number(source,"visualHeightOffset");
        Set(target,"hp",5000f);Set(source,"cooldown",0f);Set(source,"attackMovementLockTimer",0f);
        Set(source,"basicAttackCount",0);Set(source,"velocity",Vector3.zero);Call("ClearUnitInertia",source);
        Field("battleStarted").SetValue(battle,true);Call("UpdateUnit",source,1f/60f);
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(1));
        return 5000f-Number(target,"hp");
    }
}
