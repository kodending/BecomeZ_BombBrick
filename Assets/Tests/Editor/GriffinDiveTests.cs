using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private object GriffinCall(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype)
        .GetMethod(name,Private|BindingFlags.Static).Invoke(battle,args);

    private void GriffinSetup(string id="pone")
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id));
        Set(source,"skillId",GriffinCombatRules.SkillId);Set(source,"skillCooldownDuration",9f);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,Vector3.one*40f));
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Model(target).transform.position=Vector3.right*4.2f;
    }

    [Test]
    public void GriffinDiveLocksLandingAndHitsOnlyWhenFeetReachGround()
    {
        GriffinSetup();var skill=GameMasterData.Skill(GriffinCombatRules.SkillId);
        Assert.That(GriffinCall("BeginGriffinSkyfall",source,skill),Is.True);
        Vector3 landing=(Vector3)Get(source,"griffinSkyfallOrigin");
        Assert.That(landing.x,Is.InRange(3.5f,3.7f));
        object nearby=Unit(1,landing+Vector3.forward*2.9f),outside=Unit(1,landing+Vector3.forward*3.5f);
        for(int frame=0;frame<42;frame++)GriffinCall("UpdateGriffinSkyfall",source,1f/60f);
        Assert.That(Model(source).transform.position.x,Is.EqualTo(0f).Within(.001f));
        Assert.That(Model(source).transform.position.y,Is.GreaterThan(5f));
        Assert.That(Hp(target),Is.EqualTo(5000f));
        Model(target).transform.position=Vector3.left*10f;
        GriffinCall("UpdateGriffinSkyfall",source,1f/60f);
        Assert.That(Model(source).transform.position.x,Is.InRange(.5f,landing.x-.1f));
        Assert.That(Model(source).transform.position.y,Is.GreaterThan(1f));
        Assert.That(Hp(nearby),Is.EqualTo(5000f));
        for(int frame=0;frame<2;frame++)GriffinCall("UpdateGriffinSkyfall",source,1f/60f);
        Assert.That(Model(source).transform.position,Is.EqualTo(landing));
        Assert.That(Get(source,"griffinSkyfalling"),Is.False);
        Assert.That(Hp(nearby),Is.EqualTo(4830f).Within(.01f));
        Assert.That(Hp(outside),Is.EqualTo(5000f));Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(((CombatStatusState)Get(nearby,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.True);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
    }

    [TestCase("pone",170f)]
    [TestCase("griphonas",170f)]
    [TestCase("holophonas",170f)]
    [TestCase("entpone",510f)]
    public void GriffinDiveUsesEachSpeciesScaling(string id,float expected)
    {
        GriffinSetup(id);GriffinCall("BeginGriffinSkyfall",source,GameMasterData.Skill(GriffinCombatRules.SkillId));
        GriffinCall("UpdateGriffinSkyfall",source,1f);
        Assert.That(5000f-Hp(target),Is.EqualTo(expected).Within(.01f));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(9f));
    }

    [Test]
    public void GriffinDiveRangeMatchesOpportunityAndCancelledFlightReturnsToGround()
    {
        GriffinSetup();var skill=GameMasterData.Skill(GriffinCombatRules.SkillId);
        Model(target).transform.position=Vector3.right*4.3f;
        Assert.That(GriffinCall("GriffinDiveTarget",source,skill),Is.Null);
        Model(target).transform.position=Vector3.right*4.2f;
        Assert.That(GriffinCall("GriffinDiveTarget",source,skill),Is.SameAs(target));
        GriffinCall("BeginGriffinSkyfall",source,skill);GriffinCall("UpdateGriffinSkyfall",source,.725f);
        Vector3 before=Model(source).transform.position;
        GriffinCall("InterruptPositionalAction",source);
        Assert.That(Get(source,"griffinSkyfalling"),Is.False);
        Assert.That(Model(source).transform.position,Is.EqualTo(new Vector3(before.x,0f,before.z)));
        Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [Test]
    public void GriffinDiveRetainsPoneCentralHitAndEntponeAllyShield()
    {
        GriffinSetup();Trait(GriffinCombatRules.PoneTraitId);
        GriffinCall("BeginGriffinSkyfall",source,GameMasterData.Skill(GriffinCombatRules.SkillId));
        GriffinCall("UpdateGriffinSkyfall",source,1f);
        Assert.That(Hp(target),Is.EqualTo(4780f).Within(.01f));
        ((System.Collections.IList)Get(source,"activeTraitIds")).Clear();
        GriffinSetup("entpone");Trait(GriffinCombatRules.EntPoneTraitId);
        object ally=Unit(0,Model(source).transform.position+Vector3.forward);
        GriffinCall("BeginGriffinSkyfall",source,GameMasterData.Skill(GriffinCombatRules.SkillId));
        GriffinCall("UpdateGriffinSkyfall",source,1f);
        var shield=((CombatStatusState)Get(ally,"combatStatuses")).Get(CombatStatusKind.Shield);
        Assert.That(shield,Is.Not.Null);Assert.That(shield.magnitude,Is.EqualTo(400f).Within(.01f));
    }

    [TestCase("pone",280f)]
    [TestCase("griphonas",280f)]
    [TestCase("holophonas",280f)]
    [TestCase("entpone",840f)]
    public void GriffinTyphoonDealsOneHeavyAreaHitOnContactWithoutGathering(string id,float damage)
    {
        GriffinSetup(id);Trait(GriffinRank3Rules.TyphoonDive);
        GriffinCall("BeginGriffinSkyfall",source,GameMasterData.Skill(GriffinCombatRules.SkillId));
        Vector3 landing=(Vector3)Get(source,"griffinSkyfallOrigin"),preyPosition=Model(target).transform.position;
        Assert.That(landing.x,Is.GreaterThan(3f));
        object edge=Unit(1,landing+Vector3.forward*4.2f),outside=Unit(1,landing+Vector3.forward*4.4f);
        for(int frame=0;frame<44;frame++)GriffinCall("UpdateGriffinSkyfall",source,1f/60f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Hp(edge),Is.EqualTo(5000f));
        Assert.That(Model(target).transform.position,Is.EqualTo(preyPosition));
        Assert.That(Model(source).transform.position.y,Is.GreaterThan(1f));
        GriffinCall("UpdateGriffinSkyfall",source,1f/60f);
        Assert.That(Model(source).transform.position,Is.EqualTo(landing));
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
        Assert.That(5000f-Hp(edge),Is.EqualTo(damage).Within(.01f));
        Assert.That(Hp(outside),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
        Assert.That(((CombatStatusState)Get(edge,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void GriffinTyphoonLeavesTheCameraBeforeItsThreeFramePlunge(bool orthographic)
    {
        var camera=new GameObject("Typhoon Camera Test").AddComponent<Camera>();
        camera.orthographic=orthographic;camera.orthographicSize=9f;
        camera.transform.position=new Vector3(0f,16f,-18f);camera.transform.LookAt(Vector3.zero);
        Vector3 launch=new Vector3(0f,0f,-4f);
        float height=GriffinCombatRules.TyphoonLiftHeight(launch,camera);
        Assert.That(camera.WorldToViewportPoint(launch+Vector3.up*height).y,Is.GreaterThan(1.25f));
        Assert.That(GriffinCombatRules.TyphoonLift(.58f,.7f,height),Is.EqualTo(height).Within(.001f));
        Assert.That(GriffinCombatRules.TyphoonLift(.7f+2f/60f,.7f,height),Is.GreaterThan(0f));
        Assert.That(GriffinCombatRules.TyphoonLift(.75f,.7f,height),Is.EqualTo(0f).Within(.001f));
    }
}
