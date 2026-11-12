using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    [TestCase(10)]
    [TestCase(30)]
    [TestCase(60)]
    public void MantisAnimationDispatchesOnceOnTheFirstContactAndRestoresPose(int fps)
    {
        var driver=Model(source).AddComponent<SpriteMonsterAnimationDriver>();
        var frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        typeof(SpriteMonsterAnimationDriver).GetField("skillAttackFrames",Private).SetValue(driver,frames);
        typeof(SpriteMonsterAnimationDriver).GetField("idleFrames",Private).SetValue(driver,new[]{frames[0]});
        int calls=0;float contact=-1f,time=0f;
        driver.PlayMantisCrossAttack(()=>{calls++;contact=time;});
        for(int i=0;i<fps;i++)
        {
            time+=1f/fps;
            typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{1f/fps});
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
        }
        Assert.That(calls,Is.EqualTo(1));
        Assert.That(contact,Is.InRange(MantisCombatRules.WindupDuration,MantisCombatRules.WindupDuration+1f/fps+.001f));
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
        Assert.That(typeof(SpriteMonsterAnimationDriver).GetField("skillPoseScale",Private).GetValue(driver),Is.EqualTo(Vector3.one));
        driver.PlayMantisCrossAttack(()=>calls++);driver.InterruptCurrentAction();
        Assert.That(typeof(SpriteMonsterAnimationDriver).GetField("mantisCrossAttack",Private).GetValue(driver),Is.False);
        Assert.That(calls,Is.EqualTo(1));
    }

    private void StartMantisCast(string intrinsic=null)
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_mantius"));
        Set(source,"skillId",MantisCombatRules.SkillId);
        if(intrinsic!=null)Trait(intrinsic);
        cast=(IEnumerator)typeof(MonsterAutoBattlePrototype).GetMethod("ResolveCrossScytheWind",Private)
            .Invoke(battle,new[]{source,target,GameMasterData.Skill(MantisCombatRules.SkillId),1f,false,
                typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle),false});
        Assert.That(cast.MoveNext(),Is.True);
    }

    private PixelStarProjectile MantisWind()=>UnityEngine.Object.FindObjectsOfType<PixelStarProjectile>()
        .Single(p=>p.name=="Pixel Mantis Scythe Wind");
    private void AdvanceMantis(PixelStarProjectile wind,float dt)=>typeof(PixelStarProjectile)
        .GetMethod("Advance",Private).Invoke(wind,new object[]{dt});

    [Test]
    public void MantisWindsHitNearThenFarAtContactOnlyOncePerBlade()
    {
        object near=Unit(1,Vector3.right*1.5f),far=Unit(1,Vector3.right*5.5f);
        object outside=Unit(1,new Vector3(4f,0f,5f)),ally=Unit(0,Vector3.right*2f);
        StartMantisCast();var first=MantisWind();
        Assert.That(Hp(near),Is.EqualTo(5000f));Assert.That(Hp(target),Is.EqualTo(5000f));
        AdvanceMantis(first,.02f);Assert.That(Hp(near),Is.EqualTo(5000f));
        AdvanceMantis(first,.04f);Assert.That(Hp(near),Is.EqualTo(4920f));
        Assert.That(Hp(target),Is.EqualTo(5000f));
        AdvanceMantis(first,.1f);Assert.That(Hp(target),Is.EqualTo(4920f));
        Assert.That(Hp(far),Is.EqualTo(5000f));
        AdvanceMantis(first,.2f);Assert.That(Hp(far),Is.EqualTo(4920f));
        Assert.That(Hp(near),Is.EqualTo(4920f));
        Assert.That(cast.MoveNext(),Is.True);var second=MantisWind();
        AdvanceMantis(second,1f); // A long frame must sweep the whole remaining path.
        Assert.That(cast.MoveNext(),Is.False);
        foreach(var victim in new[]{near,target,far})
        {
            Assert.That(Hp(victim),Is.EqualTo(4840f));
            Assert.That(((CombatStatusState)Get(victim,"combatStatuses")).Has(CombatStatusKind.Bleed),Is.True);
        }
        Assert.That(Hp(outside),Is.EqualTo(5000f));Assert.That(Hp(ally),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(6));
    }

    [Test]
    public void MantisWindChecksPositionsAtContactAndDoesNotHitBehindTheBlade()
    {
        var entering=Unit(1,new Vector3(3f,0f,5f));
        StartMantisCast();var wind=MantisWind();AdvanceMantis(wind,.03f);
        Model(target).transform.position=new Vector3(4f,0f,6f);
        Model(entering).transform.position=Vector3.right*3f;
        AdvanceMantis(wind,.09f);
        Assert.That(Hp(entering),Is.EqualTo(4920f));Assert.That(Hp(target),Is.EqualTo(5000f));
        Model(target).transform.position=Vector3.right;
        AdvanceMantis(wind,1f);Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MantisReleasedWindsSurviveDeathAndCancelOnBattleReset(bool resetBattle)
    {
        StartMantisCast();var wind=MantisWind();
        if(resetBattle)
        {
            var generation=typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private);
            generation.SetValue(battle,(int)generation.GetValue(battle)+1);
        }
        else Set(source,"alive",false);
        AdvanceMantis(wind,1f);
        Assert.That(wind==null,Is.True);
        if(!resetBattle){Assert.That(cast.MoveNext(),Is.True);AdvanceMantis(MantisWind(),1f);}
        Assert.That(Hp(target),Is.EqualTo(resetBattle?5000f:4840f));
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(UnityEngine.Object.FindObjectsOfType<PixelStarProjectile>(),Is.Empty);
    }

    [TestCase(MantisCombatRules.MantiusTraitId,192f,false)]
    [TestCase(MantisCombatRules.FlotisTraitId,160f,true)]
    [TestCase(MantisCombatRules.SantiusTraitId,160f,false)]
    public void MantisContactRetainsIntrinsicDamageAndPollen(string intrinsic,float damage,bool pollen)
    {
        StartMantisCast(intrinsic);
        AdvanceMantis(MantisWind(),1f);Assert.That(cast.MoveNext(),Is.True);
        AdvanceMantis(MantisWind(),1f);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.HallucinatoryPollen),Is.EqualTo(pollen));
    }
}
