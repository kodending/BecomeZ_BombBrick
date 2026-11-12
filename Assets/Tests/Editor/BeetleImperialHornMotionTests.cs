using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BeetleImperialHornMotionTests
{
    [Test]public void IndividualFlightsRiseHigherWithSmallUnequalPeaksAndAQuickFinalDrop()
    {
        float minimum=float.PositiveInfinity,maximum=0f;var peaks=new HashSet<int>();
        var random=Random.state;float speed=Time.timeScale;
        for(int seed=0;seed<16;seed++)
        {
            float peak=0f,peakTime=0f;
            for(float time=BeetleOverturnRules.Warning;time<BeetleOverturnRules.Landing;time+=.002f)
            {
                Vector3 pose=BeetleOverturnRules.AirOffset(time,seed);
                Assert.That(pose.y,Is.GreaterThanOrEqualTo(0f));
                Assert.That(Mathf.Abs(pose.x),Is.LessThan(.24f));Assert.That(Mathf.Abs(pose.z),Is.LessThan(.19f));
                if(pose.y>peak){peak=pose.y;peakTime=time;}
                Assert.That(Vector3.Distance(pose,BeetleOverturnRules.AirOffset(time+.0001f,seed)),Is.LessThan(.02f));
            }
            minimum=Mathf.Min(minimum,peak);maximum=Mathf.Max(maximum,peak);peaks.Add(Mathf.RoundToInt(peakTime*100f));
            float gentle=BeetleOverturnRules.Height(BeetleOverturnRules.ApexEnd-.10f,seed)-BeetleOverturnRules.Height(BeetleOverturnRules.ApexEnd,seed);
            float fall=BeetleOverturnRules.Height(BeetleOverturnRules.Landing-.08f,seed)-BeetleOverturnRules.Height(BeetleOverturnRules.Landing-.001f,seed);
            Assert.That(fall,Is.GreaterThan(Mathf.Abs(gentle)*5f+.20f));
            Assert.That(BeetleOverturnRules.AirOffset(BeetleOverturnRules.Warning,seed).sqrMagnitude,Is.LessThan(.000001f));
            Assert.That(BeetleOverturnRules.AirOffset(BeetleOverturnRules.Landing,seed).sqrMagnitude,Is.LessThan(.000001f));
        }
        Assert.That(minimum,Is.GreaterThan(4.60f));Assert.That(maximum-minimum,Is.InRange(.30f,.90f));
        Assert.That(peaks.Count,Is.GreaterThan(3));Assert.That(Random.state,Is.EqualTo(random));Assert.That(Time.timeScale,Is.EqualTo(speed));
    }

    [Test]public void RotationContinuesThroughTheFallAndMeetsTheUprightLandingWithoutAHalfTurnSnap()
    {
        for(int seed=0;seed<16;seed++)
        {
            float before=BeetleOverturnRules.Roll(BeetleOverturnRules.Landing-.0001f,seed);
            Assert.That(before/360f,Is.InRange(2.99f,5.01f));
            Assert.That(BeetleOverturnRules.Roll(BeetleOverturnRules.ApexEnd+.04f,seed),
                Is.GreaterThan(BeetleOverturnRules.Roll(BeetleOverturnRules.ApexEnd,seed)));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before,BeetleOverturnRules.Roll(BeetleOverturnRules.Landing,seed))),Is.LessThan(.02f));
        }
    }
}

public sealed partial class MonkeyArcTests
{
    [Test]public void ImperialHornUsesVisibleBodyPivotsAndDifferentHighFlightOffsetsWithoutChangingGroundOccupancy()
    {
        BeetleOverturnBoard();object other=BeetleOverturnEnemy(6,2);
        var billboards=new List<SpriteMonsterBillboard>();var positions=new List<Vector3>();
        Sprite sprite=PixelBeetleOverturn.SourceFrames("jangpung",false)[7];
        Bounds body=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[]{sprite});
        foreach(object unit in new[]{target,other})
        {
            var billboard=Model(unit).AddComponent<SpriteMonsterBillboard>();
            var renderer=billboard.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.flipX=true;
            Set(unit,"spriteBillboard",billboard);billboards.Add(billboard);positions.Add(Model(unit).transform.position);
        }
        BeginImperialHorn();float time=BeetleOverturnRules.RiseEnd;AdvanceImperialHorn(time);
        for(int index=0;index<billboards.Count;index++)
        {
            var offset=(Vector3)typeof(SpriteMonsterBillboard).GetField("rollOffset",Private).GetValue(billboards[index]);
            var pivot=(Vector3)typeof(SpriteMonsterBillboard).GetField("rollPivot",Private).GetValue(billboards[index]);
            Assert.That(Vector3.Distance(offset,MonsterAutoBattlePrototype.TileSize*BeetleOverturnRules.AirOffset(time,index)),Is.LessThan(.0001f));
            Assert.That(pivot,Is.EqualTo(new Vector3(-body.center.x,body.center.y,body.center.z)));
            Assert.That(offset.y,Is.GreaterThan(MonsterAutoBattlePrototype.TileSize*4.5f));
        }
        Assert.That(Model(target).transform.position,Is.EqualTo(positions[0]));Assert.That(Model(other).transform.position,Is.EqualTo(positions[1]));
        AdvanceImperialHorn(2f);Assert.That(Get(target,"beetleOverturnedBy"),Is.Null);Assert.That(Get(other,"beetleOverturnedBy"),Is.Null);
    }
}
