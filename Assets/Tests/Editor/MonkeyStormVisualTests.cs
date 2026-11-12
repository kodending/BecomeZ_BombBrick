using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MonkeyStormVisualTests
{
    private static readonly Bounds Arena=new Bounds(Vector3.zero,new Vector3(27f,1f,13.5f));
    private static float[] StrikeTimes()=>MonkeyStormRules.CreateImpactTimes(new System.Random(1234));

    [Test]
    public void ReusedCloudHasFourDistinctAuthoredFrames()
    {
        var frames=PixelMonkeyStorm.CloudFrames();Assert.That(frames,Has.Length.EqualTo(4));
        var fingerprints=new HashSet<int>();
        foreach(var frame in frames)
        {
            Assert.That(frame.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Color[] pixels=frame.texture.GetPixels((int)frame.rect.x,(int)frame.rect.y,(int)frame.rect.width,(int)frame.rect.height);
            int hash=17;unchecked{foreach(var pixel in pixels)hash=hash*31+((Color32)pixel).GetHashCode();}
            fingerprints.Add(hash);
        }
        Assert.That(fingerprints.Count,Is.EqualTo(4));
    }

    [Test]
    public void ExistingZapKeepsItsAuthoredCoreFlareAndHaloWhilePlayingItsFourTiles()
    {
        var bolt=PixelMonkeyStorm.CreateAnimatedBolt(Vector3.zero,null,30);
        try
        {
            Assert.That(bolt.enabled,Is.False,"The parent storm or stage owns the one playback clock.");
            var original=Resources.Load<GameObject>(PixelMonkeyStorm.ZapResourcePath);
            Assert.That(original,Is.Not.Null,"The existing blue Zap prefab needs a build-safe Resources variant.");
            Assert.That(bolt.variants[0],Is.SameAs(original));
            int animated=0;
            foreach(var particles in bolt.GetComponentsInChildren<ParticleSystem>(true))
            {
                Assert.That(particles.gameObject.layer,Is.EqualTo(30));
                var renderer=particles.GetComponent<ParticleSystemRenderer>();
                if(!renderer.enabled||!particles.gameObject.activeInHierarchy)continue;
                var sheet=particles.textureSheetAnimation;
                int tiles=sheet.numTilesX*sheet.numTilesY;
                if(!sheet.enabled||tiles<4)continue;
                Assert.That(sheet.startFrame.constant,Is.Zero);
                var shown=new HashSet<int>();
                foreach(float time in new[]{.08f,.35f,.62f,.9f})
                    shown.Add(Mathf.FloorToInt(sheet.frameOverTime.Evaluate(time)*tiles));
                Assert.That(shown.Count,Is.EqualTo(4),particles.name+" must animate, rather than randomly holding one tile.");
                Assert.That(renderer.sharedMaterial.shader.name,Does.Contain("Vefects"));animated++;
            }
            int authoredAnimated=original.GetComponentsInChildren<ParticleSystem>(true).Count(particles=>
                particles.gameObject.activeSelf&&particles.GetComponent<ParticleSystemRenderer>().enabled&&
                particles.textureSheetAnimation.enabled&&particles.textureSheetAnimation.numTilesX*particles.textureSheetAnimation.numTilesY>=4);
            Assert.That(animated,Is.EqualTo(authoredAnimated));Assert.That(animated,Is.GreaterThanOrEqualTo(2));
            foreach(string name in new[]{"Zap","Zap LUT","Zap Add","Zap BG","Zap Add Floor","Flare"})
            {
                var copy=bolt.GetComponentsInChildren<ParticleSystem>(true).Single(item=>item.name==name);
                var authored=original.GetComponentsInChildren<ParticleSystem>(true).Single(item=>item.name==name);
                Assert.That(copy.gameObject.activeSelf,Is.EqualTo(authored.gameObject.activeSelf),name);
                var renderer=copy.GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer.enabled,Is.EqualTo(authored.GetComponent<ParticleSystemRenderer>().enabled),name);
                Assert.That(renderer.sharedMaterial,Is.SameAs(authored.GetComponent<ParticleSystemRenderer>().sharedMaterial));
                if(name=="Zap Add Floor")
                {
                    Assert.That(renderer.renderMode,Is.EqualTo(ParticleSystemRenderMode.HorizontalBillboard));
                    Assert.That(copy.main.startSize.constant*bolt.visualScale.x,Is.InRange(3.4f,4.1f));
                    Assert.That(copy.main.startLifetime.constant,Is.EqualTo(.24f));
                }
                else
                {
                    Assert.That(renderer.renderMode,Is.EqualTo(authored.GetComponent<ParticleSystemRenderer>().renderMode));
                    Assert.That(copy.main.startSize.Evaluate(.5f),Is.EqualTo(authored.main.startSize.Evaluate(.5f)));
                    Assert.That(copy.main.startLifetime.Evaluate(.5f),Is.EqualTo(authored.main.startLifetime.Evaluate(.5f)));
                }
                Assert.That(copy.main.startColor.Evaluate(.5f),Is.EqualTo(authored.main.startColor.Evaluate(.5f)));
                Assert.That(copy.colorOverLifetime.color.Evaluate(.5f),Is.EqualTo(authored.colorOverLifetime.color.Evaluate(.5f)),
                    "Keep the source asset's colors rather than replacing its gradient.");
            }
            Assert.That(bolt.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty,
                "Use the existing Zap asset without overlaying a generated lightning sprite.");
            Assert.That(bolt.GetComponentsInChildren<Light>(true).All(light=>!light.enabled),Is.True);
            Assert.That(bolt.visualScale.x,Is.GreaterThan(1f),"The existing blue bolt must read as a heavy strike.");
        }
        finally{bolt.Cancel();}
    }

    [Test]
    public void FourAbsoluteStrikeTimesProduceFourShortFlashesWithoutRepeatedAdvanceOrReducedFlashLeaks()
    {
        bool reduced=CombatFlashPresentation.Reduced;CombatFlashPresentation.Apply(false);
        var storm=PixelMonkeyStorm.Play(Arena,null,null);
        try
        {
            Assert.That(MonkeyStormRules.StrikeCount,Is.EqualTo(4));
            float[] times=StrikeTimes();
            for(int i=0;i<4;i++)storm.QueueStrike(i,Vector3.right*i,times[i]);
            int pulses=0;bool active=false;
            for(int frame=0;frame<=Mathf.CeilToInt((MonkeyStormRules.Duration+.3f)*120f);frame++)
            {
                float time=frame/120f;storm.Advance(time);Color color=storm.ScreenFlashColor;
                if(!active&&color.a>0f)pulses++;active=color.a>0f;
                storm.Advance(time);Assert.That(storm.ScreenFlashColor,Is.EqualTo(color),"Repainting or handing the same clock back cannot retrigger a strike.");
            }
            Assert.That(pulses,Is.EqualTo(4));Assert.That(storm.ScreenFlashColor.a,Is.Zero);
        }
        finally{storm.Cancel();CombatFlashPresentation.Apply(reduced);}

        storm=PixelMonkeyStorm.Play(Arena,null,null);
        try
        {
            float first=StrikeTimes()[0];storm.QueueStrike(0,Vector3.zero,first);storm.Advance(first);
            CombatFlashPresentation.Apply(false);Color normal=storm.ScreenFlashColor;
            CombatFlashPresentation.Apply(true);Color dimmed=storm.ScreenFlashColor;
            Assert.That(normal.a,Is.GreaterThan(.5f));Assert.That(dimmed.a,Is.EqualTo(normal.a*.25f).Within(.0001f));
            Assert.That(Mathf.Max(dimmed.r,Mathf.Max(dimmed.g,dimmed.b)),Is.LessThanOrEqualTo(.68001f));
        }
        finally{storm.Cancel();CombatFlashPresentation.Apply(reduced);}
    }

    [Test]
    public void AllQueuedStrikesUseOneBoundedPoolAcrossALargeTimeStep()
    {
        var storm=PixelMonkeyStorm.Play(Arena,null,null);
        try
        {
            var original=storm.GetComponentsInChildren<PixelHaetaeThunder>(true);
            Assert.That(PixelMonkeyStorm.ConcurrentStrikeLimit,Is.EqualTo(2));
            Assert.That(original.Length,Is.EqualTo(2));
            float[] times=StrikeTimes();
            for(int i=0;i<MonkeyStormRules.StrikeCount;i++)
            {
                storm.QueueStrike(i,new Vector3(i%8-4,0f,i%3-1),times[i]);
                if(i>0)Assert.That(times[i]-times[i-1],Is.GreaterThanOrEqualTo(MonkeyStormRules.MinimumInterval-.00001f));
            }
            storm.QueueStrike(MonkeyStormRules.StrikeCount-1,Vector3.one*1000f,times[times.Length-1]);
            float afterLastImpact=times[times.Length-1]+.12f;
            storm.Advance(afterLastImpact);
            var active=original.Where(bolt=>bolt.gameObject.activeSelf).ToArray();
            int remainingTails=times.Count(time=>afterLastImpact>=time&&afterLastImpact<time+MonkeyStormRules.Tail);
            Assert.That(remainingTails,Is.InRange(1,2));
            Assert.That(active.Length,Is.EqualTo(remainingTails),"Every still-visible tail needs its own bounded slot after the schedule is compressed.");
            Assert.That(active.All(bolt=>Mathf.Abs(bolt.transform.position.x)<5f),Is.True,"A duplicate index cannot move a locked strike.");
            CollectionAssert.AreEqual(original,storm.GetComponentsInChildren<PixelHaetaeThunder>(true));
            Vector3[] positions=active.Select(bolt=>bolt.transform.position).ToArray();
            storm.Advance(afterLastImpact);storm.Advance(1f);
            Assert.That(storm.Elapsed,Is.EqualTo(afterLastImpact));
            CollectionAssert.AreEqual(positions,active.Select(bolt=>bolt.transform.position).ToArray());
            storm.Advance(MonkeyStormRules.Duration+MonkeyStormRules.Tail+.1f);
            Assert.That(original.All(bolt=>!bolt.gameObject.activeSelf),Is.True);
            Assert.That(storm.RemainingLifetime,Is.Zero);
        }
        finally{storm.Cancel();}
    }

    [Test]
    public void FourShortGapStrikesAppearAtTheirOwnContactPointsWithoutWaitingForThePreviousTail()
    {
        var storm=PixelMonkeyStorm.Play(Arena,null,null);
        try
        {
            var pool=storm.GetComponentsInChildren<PixelHaetaeThunder>(true);
            Assert.That(pool,Has.Length.EqualTo(2));
            var times=new[]{.8f,1.15f,1.5f,1.85f};
            var points=new[]{new Vector3(-3f,0f,-1f),new Vector3(-1f,0f,-.5f),
                new Vector3(1f,0f,.5f),new Vector3(3f,0f,1f)};
            for(int i=0;i<times.Length;i++)storm.QueueStrike(i,points[i],times[i]);
            var shown=new List<Vector3>();
            for(int i=0;i<times.Length;i++)
            {
                Vector3 expected=points[i];expected.y=Arena.center.y+.045f;
                storm.Advance(times[i]-.001f);
                Assert.That(pool.Any(bolt=>bolt.gameObject.activeSelf&&
                    Vector3.Distance(bolt.transform.position,expected)<.0001f),Is.False,
                    "An upcoming bolt cannot show before its own contact.");
                storm.Advance(times[i]);
                var current=pool.SingleOrDefault(bolt=>bolt.gameObject.activeSelf&&
                    Vector3.Distance(bolt.transform.position,expected)<.0001f);
                Assert.That(current,Is.Not.Null,"Strike "+i+" must show at contact even while the previous tail is still alive.");
                Assert.That(storm.ScreenFlashColor.a,Is.GreaterThan(0f));
                shown.Add(current.transform.position);
                int remainingTails=times.Count(time=>times[i]>=time&&times[i]<time+MonkeyStormRules.Tail);
                Assert.That(pool.Count(bolt=>bolt.gameObject.activeSelf),Is.EqualTo(remainingTails));
                storm.Advance(times[i]);
                Assert.That(current.transform.position,Is.EqualTo(expected));
                CollectionAssert.AreEqual(pool,storm.GetComponentsInChildren<PixelHaetaeThunder>(true));
            }
            Assert.That(shown.Distinct().Count(),Is.EqualTo(4));
            storm.Advance(times[3]+MonkeyStormRules.Tail-.001f);
            Assert.That(pool.Count(bolt=>bolt.gameObject.activeSelf),Is.EqualTo(1),"The final tail keeps its original duration.");
            storm.Advance(times[3]+MonkeyStormRules.Tail+.001f);
            Assert.That(pool.All(bolt=>!bolt.gameObject.activeSelf),Is.True);
        }
        finally{storm.Cancel();}
    }

    [Test]
    public void CanopyStaysAboveCombatAndLockedBoltsSurviveLosingTheirCaster()
    {
        var owner=new GameObject("Storm caster");var sprite=owner.AddComponent<SpriteRenderer>();
        var camera=new GameObject("Storm view").AddComponent<Camera>();camera.aspect=16f/9f;camera.fieldOfView=50f;
        camera.transform.position=new Vector3(0f,22f,-14f);camera.transform.rotation=Quaternion.Euler(58f,0f,0f);
        var storm=PixelMonkeyStorm.Play(Arena,sprite,camera);
        try
        {
            storm.QueueStrike(0,new Vector3(3f,0f,2f),.3f);storm.Advance(.2f);
            var canopy=storm.GetComponentsInChildren<SpriteRenderer>(true).Where(item=>item.name.StartsWith("Animated storm canopy")).ToArray();
            Assert.That(canopy.Length,Is.EqualTo(14));
            float lowest=1f;
            foreach(var cloud in canopy)
            {
                Bounds visible=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{cloud.sprite});
                for(int corner=0;corner<4;corner++)
                {
                    Vector3 local=new Vector3((corner%2==0?visible.min.x:visible.max.x)*(cloud.flipX?-1f:1f),
                        corner<2?visible.min.y:visible.max.y,0f);
                    float y=camera.WorldToViewportPoint(cloud.transform.TransformPoint(local)).y;lowest=Mathf.Min(lowest,y);
                    Assert.That(y,Is.GreaterThan(.77f),"The storm ceiling must leave the combatants visible.");
                }
            }
            Assert.That(lowest,Is.LessThan(.83f),"The sky must fill with two cloud rows rather than a few clumps at the top edge.");
            Object.DestroyImmediate(owner);storm.Advance(.31f);
            var bolt=storm.GetComponentsInChildren<PixelHaetaeThunder>(true).Single(item=>item.gameObject.activeSelf);
            Assert.That(bolt.transform.position.x,Is.EqualTo(3f));Assert.That(bolt.transform.position.z,Is.EqualTo(2f));
        }
        finally{storm.Cancel();if(owner!=null)Object.DestroyImmediate(owner);Object.DestroyImmediate(camera.gameObject);}
    }

    [Test]
    public void ResultStopsFutureStrikesThenFinishesExistingTailAndCancelRemovesEverything()
    {
        var storm=PixelMonkeyStorm.Play(Arena,null,null);
        try
        {
            float[] times=StrikeTimes();
            for(int i=0;i<MonkeyStormRules.StrikeCount;i++)storm.QueueStrike(i,Vector3.right*i,times[i]);
            float stopAt=times[0]+.08f;storm.Advance(stopAt);storm.StopEmission();float remaining=storm.RemainingLifetime;
            Assert.That(remaining,Is.InRange(.45f,.65f));
            storm.QueueStrike(MonkeyStormRules.StrikeCount-1,Vector3.one*1000f,stopAt+.1f);storm.Advance(stopAt+.4f);
            foreach(var bolt in storm.GetComponentsInChildren<PixelHaetaeThunder>(true))
                if(bolt.gameObject.activeSelf)Assert.That(bolt.transform.position.x,Is.Zero);
            storm.Advance(stopAt+.8f);
            Assert.That(storm.GetComponentsInChildren<PixelHaetaeThunder>(true).All(bolt=>!bolt.gameObject.activeSelf),Is.True);
            Assert.That(storm.GetComponentsInChildren<SpriteRenderer>().All(cloud=>!cloud.enabled),Is.True);
            Assert.That(storm.RemainingLifetime,Is.Zero);
            storm.Cancel();Assert.That(storm==null,Is.True);
        }
        finally{if(storm!=null)storm.Cancel();}
    }
}
