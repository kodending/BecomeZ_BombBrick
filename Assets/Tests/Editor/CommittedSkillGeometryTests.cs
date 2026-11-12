using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void GeometryWorld()
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
        Set(source,"skillCooldownDuration",10f);
        Model(source).AddComponent<SpriteRenderer>();
        Set(source,"spriteAnimation",Model(source).AddComponent<SpriteMonsterAnimationDriver>());
    }

    private void WithGeometryCommit(Action action)
    {
        var type=typeof(MonsterAutoBattlePrototype);
        var requestType=type.GetNestedType("PendingSkillFocus",BindingFlags.NonPublic);
        object request=Activator.CreateInstance(requestType,true);
        requestType.GetField("unit").SetValue(request,source);
        requestType.GetField("castTarget").SetValue(request,target);
        var field=type.GetField("committingSkill",Private);object previous=field.GetValue(battle);
        field.SetValue(battle,request);
        try{action();}finally{field.SetValue(battle,previous);}
    }

    private void CrowdGeometryTarget()
    {
        for(int i=0;i<8;i++)Unit(0,Model(target).transform.position+
            Quaternion.AngleAxis(i*45f,Vector3.up)*Vector3.forward*1.1f);
    }

    private void GeometryObstacle(Vector3 center)
    {
        var type=typeof(MonsterAutoBattlePrototype);
        var tileType=type.GetNestedType("Tile",BindingFlags.NonPublic);
        object tile=Activator.CreateInstance(tileType,true);
        tileType.GetField("world").SetValue(tile,center);tileType.GetField("walkable").SetValue(tile,false);
        ((IDictionary)type.GetField("tiles",Private).GetValue(battle)).Add(new Vector2Int(99,99),tile);
    }

    [TestCase(false)] [TestCase(true)]
    public void CommittedShadowOrWolfStartKeepsSafePositionWhenEveryLandingIsCrowded(bool wolf)
    {
        GeometryWorld();Set(source,"skillId",wolf?WolfCombatRules.SkillId:NinjaCatCombatRules.SkillId);
        Model(target).transform.position=Vector3.right*24f;CrowdGeometryTarget();
        object[] landing={source,target,Vector3.zero,Vector3.right,Vector3.zero};
        Assert.That(MammothCall("TryResolveShadowSlashLanding",landing),Is.False,
            "An uncommitted relocation must still reject a fully occupied destination.");
        WithGeometryCommit(()=>Assert.That(MammothCall(wolf?"BeginThreeSidedHunt":"BeginShadowHunt",
            source,GameMasterData.Skill((string)Get(source,"skillId"))),Is.True));
        Assert.That(Get(source,wolf?"wolfHuntLanding":"ninjaHuntLanding"),Is.EqualTo(Vector3.zero));
        Assert.That(Model(source).transform.position,Is.EqualTo(Vector3.zero));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [TestCase(false)] [TestCase(true)]
    public void DelayedShadowArrivalKeepsSafeLandingAndResolvesOnlyItsIntendedStrike(bool occupyPrevious)
    {
        GeometryWorld();Set(source,"skillId",NinjaCatCombatRules.SkillId);
        var skill=GameMasterData.Skill(NinjaCatCombatRules.SkillId);
        Assert.That(MammothCall("BeginShadowHunt",source,skill),Is.True);
        Vector3 previous=(Vector3)Get(source,"ninjaHuntLanding");
        Model(target).transform.position=Vector3.right*24f;CrowdGeometryTarget();
        if(occupyPrevious)Unit(0,previous);
        object outsider=Unit(1,Vector3.forward*12f);
        MammothCall("ArriveShadowHunt",source);
        Assert.That(Model(source).transform.position,Is.EqualTo(occupyPrevious?Vector3.zero:previous));
        Assert.That(Get(source,"ninjaHuntPending"),Is.True);
        MammothCall("ResolveShadowHunt",source,skill,1f,false,0);
        float hp=Hp(target);Assert.That(hp,Is.LessThan(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));Assert.That(Hp(outsider),Is.EqualTo(5000f));
        MammothCall("ResolveShadowHunt",source,skill,1f,false,0);
        Assert.That(Hp(target),Is.EqualTo(hp));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void CommittedMammothShortensBlockedChargeAndStillCompletesOneImpact(bool blockedAtStart)
    {
        GeometryWorld();Set(source,"skillId",MammothCombatRules.SkillId);
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_kogason"));
        var skill=GameMasterData.Skill(MammothCombatRules.SkillId);
        if(blockedAtStart)
            GeometryObstacle(Vector3.right*(MonsterAutoBattlePrototype.TileSize*.5f*.82f+.3f+.08f+.00001f));
        else typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,new Vector3(6f,1f,30f)));
        Assert.That(MammothCall("BeginEarthCharge",source,skill),Is.False);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(0));
        WithGeometryCommit(()=>
        {
            Model(target).transform.position=Vector3.right*20f;
            Assert.That(MammothCall("BeginEarthCharge",source,skill),Is.True);
        });
        Vector3 end=(Vector3)Get(source,"mammothChargeEnd");
        Assert.That(end.x,Is.InRange(0f,2.701f));
        if(blockedAtStart)Assert.That(end,Is.EqualTo(Vector3.zero));
        else
        {
            Assert.That(end.x,Is.GreaterThan(2.69f));
            Assert.That(MammothCall("HasClearPathSegment",Vector3.zero,end,.3f),Is.True);
        }
        object near=Unit(1,end+Vector3.forward*.8f);
        MammothCall("StartMammothCharge",source,0);object charge=Get(source,"mammothCharge");
        Assert.That(charge,Is.Not.Null);
        MammothCall("AdvanceMammothCharge",source,charge,.95f);
        float hp=Hp(near);Assert.That(hp,Is.LessThan(5000f));Assert.That(Hp(target),Is.EqualTo(5000f));
        int hits=(int)Get(source,"skillHits");
        MammothCall("AdvanceMammothCharge",source,charge,1f);
        Assert.That(Hp(near),Is.EqualTo(hp));Assert.That(Get(source,"skillHits"),Is.EqualTo(hits));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Model(source).transform.position.x,Is.EqualTo(end.x).Within(.001f));
    }

    [Test]
    public void CommittedBellowKeepsDistantAcceptedDirectionWithoutExpandingItsCone()
    {
        var skill=BellowSetup();object ahead=Unit(1,Vector3.forward*2f),side=Unit(1,Vector3.right*2f);
        WithGeometryCommit(()=>
        {
            Model(target).transform.position=Vector3.forward*20f;
            Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.True);
        });
        object roar=Get(source,"haetaeRoar");
        Assert.That(roar.GetType().GetField("forward").GetValue(roar),Is.EqualTo(Vector3.forward));
        Assert.That((float)roar.GetType().GetField("radius").GetValue(roar),
            Is.EqualTo(MonsterAutoBattlePrototype.TileSize*HaetaeRank3Rules.BellowRangeTiles));
        RoarActionTick(1f);float hp=Hp(ahead);Assert.That(hp,Is.LessThan(5000f));
        Assert.That(Hp(side),Is.EqualTo(5000f));Assert.That(Hp(target),Is.EqualTo(5000f));
        int hits=(int)Get(source,"skillHits");RoarActionTick(.5f);
        Assert.That(Hp(ahead),Is.EqualTo(hp));Assert.That(Get(source,"skillHits"),Is.EqualTo(hits));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
    }
}
