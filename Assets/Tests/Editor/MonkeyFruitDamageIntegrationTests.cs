using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    private void BeginPacedFruitRelease(string trait,float step)
    {
        Trait(trait);Set(source,"target",target);
        var prefab=Resources.Load<GameObject>("Enemies/PixelMonsters/045_chiwoo");
        var driver=Object.Instantiate(prefab).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        Set(source,"spriteAnimation",driver);
        driver.SetCombatPlaybackSpeed(BombBrick.Combat.CombatStatFormula.CombatAnimationRate);
        var skill=GameMasterData.Skill(MonkeyCombatRules.SkillId);
        Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("BeginAcrobaticBarrage",Private)
            .Invoke(battle,new[]{source,skill}),Is.EqualTo(true));
        int released=0;
        // Only the scheduler is driven manually: begin, animation contact and nested production resolvers stay real.
        driver.PlaySkillAttack(1f,()=>
        {
            released++;StartCast();Assert.That(cast.MoveNext(),Is.True);
            kingCast=(IEnumerator)cast.Current;Assert.That(kingCast.MoveNext(),Is.True);
        });
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        advance.Invoke(driver,new object[]{.1f});
        Assert.That(released,Is.Zero);Assert.That(Hp(target),Is.EqualTo(5000f));
        for(float elapsed=0;elapsed<2f&&released==0;elapsed+=step)advance.Invoke(driver,new object[]{step});
        Assert.That(released,Is.EqualTo(1));Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Hp(target),Is.EqualTo(5000f),"A release cannot damage before its visible projectile contact.");
    }

    [TestCase(MonkeyRank3Rules.MyriadReturn,.008333333f,350f)]
    [TestCase(MonkeyRank3Rules.MyriadReturn,.23f,350f)]
    [TestCase(MonkeyRank3Rules.FiveOrbJuggling,.008333333f,450f)]
    [TestCase(MonkeyRank3Rules.FiveOrbJuggling,.23f,450f)]
    public void RealBeginAndPacedReleaseReachAllStationaryFruitContacts(string trait,float step,float damage)
    {
        var random=UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(20261019);Model(target).transform.position=Vector3.right*.9f;
            BeginPacedFruitRelease(trait,step);
            if(trait==MonkeyRank3Rules.MyriadReturn)
            {
                var fx=Object.FindObjectOfType<PixelMonkeyReturn>();Assert.That(fx,Is.Not.Null);
                var advance=typeof(PixelMonkeyReturn).GetMethod("Advance",Private);
                for(float t=0;t<2f;t+=step)advance.Invoke(fx,new object[]{step});
                Assert.That(fx.Complete,Is.True);
            }
            else
            {
                var fx=Object.FindObjectOfType<PixelMonkeyMortar>();Assert.That(fx,Is.Not.Null);
                var advance=typeof(PixelMonkeyMortar).GetMethod("Advance",Private);
                advance.Invoke(fx,new object[]{1f});Assert.That(Hp(target),Is.EqualTo(5000f));
                for(float t=0;t<1f;t+=step)advance.Invoke(fx,new object[]{step});
                Assert.That(fx.Complete,Is.True);
            }
            Assert.That(kingCast.MoveNext(),Is.False);Assert.That(cast.MoveNext(),Is.False);
            Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
            Assert.That(Get(source,"skillHits"),Is.EqualTo(trait==MonkeyRank3Rules.MyriadReturn?10:5));
            Assert.That(Get(source,"skillDamage"),Is.EqualTo(damage).Within(.01f));
        }
        finally{UnityEngine.Random.state=random;}
    }

    [TestCase(.008333333f)] [TestCase(.23f)]
    public void ReleasedBananasHitAMovingCrossingBodyWithoutFollowingTheOriginalTarget(float step)
    {
        var random=UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(20261019);Model(target).transform.position=Vector3.right*.9f;
            BeginPacedFruitRelease(MonkeyRank3Rules.MyriadReturn,step);
            var fx=Object.FindObjectOfType<PixelMonkeyReturn>();
            var directions=(Vector3[])typeof(PixelMonkeyReturn).GetField("directions",Private).GetValue(fx);
            var original=directions.ToArray();
            Model(target).transform.position=Vector3.forward*20f;
            object crossing=Unit(1,new Vector3(1.2f,0f,-3f));Set(crossing,"collisionRadius",.8f);
            object ally=Unit(0,Vector3.right*1.2f);
            var advance=typeof(PixelMonkeyReturn).GetMethod("Advance",Private);
            for(float t=0;t<1.6f;t+=step)
            {
                Model(crossing).transform.position=new Vector3(1.2f,0f,Mathf.Lerp(-3f,3f,Mathf.Clamp01((t-.8f)/.4f)));
                advance.Invoke(fx,new object[]{step});
            }
            Assert.That(kingCast.MoveNext(),Is.False);Assert.That(cast.MoveNext(),Is.False);
            Assert.That(Hp(crossing),Is.LessThan(5000f));
            Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Hp(ally),Is.EqualTo(5000f));
            CollectionAssert.AreEqual(original,directions,"Released fruit keeps its launch direction; no homing was added.");
        }
        finally{UnityEngine.Random.state=random;}
    }
}
