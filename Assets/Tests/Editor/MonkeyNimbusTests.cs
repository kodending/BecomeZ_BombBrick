using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void NimbusSwoopsQuicklyWithoutJumpingOrLeavingTheArena(bool nearEdge)
    {
        var bounds=new Bounds(Vector3.zero,new Vector3(24f,1f,18f));
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private).SetValue(battle,bounds);
        if(nearEdge)
        {
            Model(source).transform.position=new Vector3(9f,0f,6f);
            Model(target).transform.position=new Vector3(10f,0f,7f);
        }
        typeof(MonsterAutoBattlePrototype).GetMethod("BeginMonkeyNimbus",Private)
            .Invoke(battle,new[]{source,target,GameMasterData.Skill(MonkeyCombatRules.SkillId)});
        var flight=Get(source,"monkeyNimbus");
        flight.GetType().GetField("casts").SetValue(flight,3);
        var tick=typeof(MonsterAutoBattlePrototype).GetMethod("TickMonkeyNimbus",Private);
        Vector3 previous=Model(source).transform.position;
        float distance=0f,peakStep=0f;
        int framesBeforeLanding=Mathf.CeilToInt(CombatActionSeconds(5f)*60f)-1;
        for(int i=0;i<framesBeforeLanding;i++)
        {
            tick.Invoke(battle,new[]{source,(object)(1f/60f)});
            Vector3 position=Model(source).transform.position;position.y=0f;previous.y=0f;
            float step=Vector3.Distance(previous,position);distance+=step;peakStep=Mathf.Max(peakStep,step);
            Assert.That(bounds.Contains(position),Is.True,"Flight must remain inside the combat area.");
            previous=position;
        }
        Assert.That(distance,Is.GreaterThan(nearEdge?20f:35f));
        Assert.That(peakStep,Is.LessThan(.75f),"A fast swoop must still move continuously at 60 Hz.");
        Assert.That(Get(source,"monkeyNimbus"),Is.Not.Null);
        tick.Invoke(battle,new[]{source,(object).05f});
        Assert.That(Get(source,"monkeyNimbus"),Is.Null,"The paced path must land after five authored animation seconds.");
    }

    [Test]
    public void NimbusAfterimageStaysBehindWithRiderPoseAndIsRemovedOnCancellation()
    {
        var rider=Model(source).AddComponent<SpriteRenderer>();
        var sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f);
        try
        {
            rider.sprite=sprite;rider.flipX=true;rider.sortingOrder=20;
            rider.transform.rotation=Quaternion.Euler(25f,10f,18f);
            var cloud=PixelMonkeyNimbus.Play(rider);
            cloud.Pose(Vector3.zero,2f,Vector3.right*15f,.3f);
            typeof(PixelMonkeyNimbus).GetMethod("LateUpdate",Private).Invoke(cloud,null);
            var echo=GameObject.Find("Nimbus Rider Afterimage").GetComponent<SpriteRenderer>();
            Vector3 snapshot=echo.transform.position;
            rider.transform.position+=Vector3.right*2f;
            cloud.Pose(Vector3.right*2f,2f,Vector3.right*15f,.4f);
            Assert.That(echo.sprite,Is.SameAs(sprite));Assert.That(echo.flipX,Is.True);
            Assert.That(echo.transform.rotation,Is.EqualTo(rider.transform.rotation));
            Assert.That(echo.transform.position,Is.EqualTo(snapshot));
            Assert.That(echo.sortingOrder,Is.LessThan(rider.sortingOrder));
            cloud.Stop(false);
            Assert.That(echo==null,Is.True);
        }
        finally{UnityEngine.Object.DestroyImmediate(sprite);}
    }

    [TestCase(MonkeyCombatRules.ChiwooTraitId,555f)]
    [TestCase(MonkeyCombatRules.TotoTraitId,520f)]
    [TestCase(MonkeyCombatRules.MakaTraitId,540f)]
    public void NimbusHalfPowerMortarRetainsRank3AndScalesIntrinsicDamage(string intrinsic,float fullDamage)
    {
        Trait(intrinsic);Trait(MonkeyRank3Rules.FiveOrbJuggling);
        StartCast(.5f);Assert.That(cast.MoveNext(),Is.True);
        kingCast=(IEnumerator)cast.Current;Assert.That(kingCast.MoveNext(),Is.True);
        var fx=UnityEngine.Object.FindObjectOfType<PixelMonkeyMortar>();
        AdvanceMortar(fx,2f);kingCast.MoveNext();cast.MoveNext();
        Assert.That(5000f-Hp(target),Is.EqualTo(fullDamage*.5f).Within(.01f));
    }

    [Test]
    public void MovingCloudLaunchesEachBananaAtItsOwnOriginAndCatchesAtCurrentPosition()
    {
        Vector3 rider=new Vector3(0f,3f,0f);
        var fx=PixelMonkeyReturn.Play(rider,Vector3.right,null,Color.white,()=>true,()=>rider,
            null,null,()=>rider,.5f);
        AdvanceReturn(fx,.04f);rider+=Vector3.forward*2f;AdvanceReturn(fx,.1f);
        var origins=(Vector3[])typeof(PixelMonkeyReturn).GetField("origins",Private).GetValue(fx);
        Assert.That(origins[0],Is.EqualTo(new Vector3(0f,3f,0f)));
        Assert.That(origins[1],Is.EqualTo(rider));
        var point=typeof(PixelMonkeyReturn).GetMethod("Point",Private);
        var halfway=(Vector3)point.Invoke(fx,new object[]{0,MonkeyRank3Rules.BoomerangOutDuration,rider});
        Assert.That(halfway.y,Is.EqualTo(.5f).Within(.001f));
        rider=new Vector3(-2f,2f,-1f);
        var end=(Vector3)point.Invoke(fx,new object[]{0,MonkeyRank3Rules.BoomerangOutDuration+MonkeyRank3Rules.ReturnFlightDuration,rider});
        Assert.That(Vector3.Distance(end,rider),Is.LessThan(.001f));
    }

    [Test]
    public void MovingCloudMortarKeepsSeparateLaunchOriginsAndFixedImpactCenter()
    {
        Vector3 rider=Vector3.up*3f,center=Vector3.right*4f;
        var fx=PixelMonkeyMortar.Play(rider,center,null,Color.white,1f,2f,()=>true,null,null,()=>rider);
        AdvanceMortar(fx,.04f);rider+=Vector3.forward*2f;AdvanceMortar(fx,.1f);
        var origins=(Vector3[])typeof(PixelMonkeyMortar).GetField("origins",Private).GetValue(fx);
        Assert.That(origins[0],Is.EqualTo(Vector3.up*3f));Assert.That(origins[1],Is.EqualTo(rider));
        Assert.That(typeof(PixelMonkeyMortar).GetField("center",Private).GetValue(fx),Is.EqualTo(center));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NimbusMovesAboveGroundThenRestoresTargetabilityAndFullCooldown(bool cancel)
    {
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,new Bounds(Vector3.zero,new Vector3(24f,1f,18f)));
        Set(source,"skillCooldownDuration",8f);
        typeof(MonsterAutoBattlePrototype).GetMethod("BeginMonkeyNimbus",Private)
            .Invoke(battle,new[]{source,target,GameMasterData.Skill(MonkeyCombatRules.SkillId)});
        var flight=Get(source,"monkeyNimbus");
        flight.GetType().GetField("casts").SetValue(flight,3); // Exercise movement without starting runtime coroutines in EditMode.
        var targetable=typeof(MonsterAutoBattlePrototype).GetMethod("IsCombatTargetable",BindingFlags.Static|BindingFlags.NonPublic);
        Assert.That(targetable.Invoke(null,new[]{source}),Is.False);
        var tick=typeof(MonsterAutoBattlePrototype).GetMethod("TickMonkeyNimbus",Private);
        tick.Invoke(battle,new[]{source,(object)CombatActionSeconds(1f)});
        Assert.That(Model(source).transform.position.y,Is.GreaterThan(1f));
        Vector3 first=Model(source).transform.position;
        tick.Invoke(battle,new[]{source,(object)CombatActionSeconds(1f)});
        Assert.That(Vector3.Distance(first,Model(source).transform.position),Is.GreaterThan(.5f));
        if(cancel)typeof(MonsterAutoBattlePrototype).GetMethod("EndMonkeyNimbus",Private).Invoke(battle,new[]{source,(object)false});
        else tick.Invoke(battle,new[]{source,(object)CombatActionSeconds(3f)});
        Assert.That(Get(source,"monkeyNimbus"),Is.Null);
        Assert.That(Model(source).transform.position.y,Is.EqualTo((float)Get(source,"visualHeightOffset")).Within(.001f));
        Assert.That(targetable.Invoke(null,new[]{source}),Is.True);
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8f));
        Assert.That(UnityEngine.Object.FindObjectsOfType<PixelMonkeyNimbus>(),Is.Empty);
    }
}
