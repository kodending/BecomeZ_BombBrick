using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class PixelWizardCygnusMeteorShowerTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    private readonly List<PixelWizardCygnusMeteorShower> effects=new List<PixelWizardCygnusMeteorShower>();
    private readonly List<Camera> mutedCameras=new List<Camera>();
    private readonly ParticleSystem.Particle[] pixels=new ParticleSystem.Particle[6144],comparison=new ParticleSystem.Particle[6144];
    private Camera camera;
    private SpriteRenderer source;
    private Sprite sprite;
    private Texture2D texture;
    private static Func<Vector3,Vector3,float,int,Vector3> guidance;

    [SetUp] public void Setup()
    {
        foreach(Camera existing in Resources.FindObjectsOfTypeAll<Camera>())
            if(existing.gameObject.scene.IsValid()&&existing.enabled&&existing.CompareTag("MainCamera"))
            {mutedCameras.Add(existing);existing.enabled=false;}
        camera=new GameObject("Cygnus rain test camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-12f),Quaternion.Euler(35f,0f,0f));
        camera.fieldOfView=40f;camera.aspect=16f/9f;
        texture=new Texture2D(2,2);texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,2f);
        source=new GameObject("Cygnus rain actual source").AddComponent<SpriteRenderer>();source.sprite=sprite;
    }
    [TearDown] public void Cleanup()
    {
        foreach(var effect in effects)if(effect!=null)effect.Cancel();effects.Clear();
        if(source!=null)Object.DestroyImmediate(source.gameObject);
        if(camera!=null)Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
        foreach(Camera previous in mutedCameras)if(previous!=null)previous.enabled=true;mutedCameras.Clear();
    }

    [Test]
    public void ProductionMeteorAtlasAnimatesBothRowsAndReusesItsSpritesAcrossCasts()
    {
        Texture2D atlas=Resources.Load<Texture2D>(PixelWizardCygnusMeteorShower.MeteorResource);
        Assert.That(atlas,Is.Not.Null,"The released skill must load its authored production meteor atlas.");
        var first=Create(Targets(1));var cached=new Sprite[8];
        float contact=WizardCygnusShowerRules.ImpactTime(0,1);
        float[] probes={.01f,.06f,.11f,.16f,contact+.01f,contact+.065f,contact+.16f,contact+.28f};
        for(int i=0;i<probes.Length;i++)
        {
            first.Advance(probes[i]);SpriteRenderer meteor=Stars(first)[0];
            Assert.That(meteor.enabled,Is.True);cached[i]=meteor.sprite;
            Assert.That(cached[i].texture,Is.SameAs(atlas),"A spell meteor must use the drawn atlas rather than the generic white star.");
            Assert.That(cached[i].rect,Is.EqualTo(AtlasCell(atlas,i)),"Flight uses each complete upper-row drawing; impact uses each complete lower-row drawing.");
            if(i==3){first.Advance(.21f);Assert.That(Stars(first)[0].sprite,Is.SameAs(cached[0]),"Flight must loop through its four authored drawings.");}
        }
        CollectionAssert.AllItemsAreUnique(cached);
        first.Cancel();
        var second=Create(Targets(1));
        for(int i=0;i<probes.Length;i++)
        {
            second.Advance(probes[i]);
            Assert.That(Stars(second)[0].sprite,Is.SameAs(cached[i]),"A new cast must reuse cached Sprite objects, including after the previous cast is destroyed.");
            Assert.That(Stars(second)[0].sprite.texture,Is.SameAs(atlas),"A cast must not clone its atlas texture.");
        }
    }

    [Test]
    public void TheDownwardDrawnMeteorNoseFollowsTheProjectedCurveTangent()
    {
        var targets=Targets(1);var effect=Create(targets);Vector3 end=targets[0]+Vector3.up*.035f;
        for(int index=0;index<WizardCygnusShowerRules.ShotCount(1);index++)
        foreach(float progress in new[]{.18f,.5f,.86f})
        {
            float contact=WizardCygnusShowerRules.ImpactTime(index,1);
            effect.Advance(contact-WizardCygnusShowerRules.FlightSeconds*(1f-progress));
            SpriteRenderer meteor=Stars(effect)[index];Vector3 start=Launches(effect)[index];
            Vector3 tangent=camera.transform.InverseTransformDirection(Curve(start,end,progress+.001f,index)-Curve(start,end,progress-.001f,index));
            tangent.z=0f;tangent=camera.transform.TransformDirection(tangent.normalized);
            Assert.That(meteor.enabled,Is.True);
            Assert.That(Vector3.Dot(meteor.transform.TransformDirection(Vector3.down),tangent),Is.GreaterThan(.99f),
                "The downward-facing atlas nose must lead along the arc, with the flame tail behind it.");
        }
    }

    [Test]
    public void EveryMeteorLaunchesFromTheVisibleCygnusInsteadOfAnUnrelatedOffscreenPoint()
    {
        var targets=Targets(8);var effect=Create(targets);
        Assert.That(Stars(effect).Length,Is.EqualTo(36));
        for(int index=0;index<WizardCygnusShowerRules.ShotCount(targets.Length);index++)
        {
            int target=index%targets.Length,wave=index/targets.Length;
            float launch=WizardCygnusShowerRules.ImpactTime(index,targets.Length)-WizardCygnusShowerRules.FlightSeconds;
            effect.Advance(launch+.00001f);SpriteRenderer meteor=Stars(effect)[index];Vector3 anchor=Launches(effect)[index];
            Assert.That(meteor.enabled,Is.True);Assert.That(Launched(effect)[index],Is.True);
            Assert.That(Vector3.Distance(meteor.transform.position,anchor),Is.LessThan(.002f));
            object sky=typeof(PixelWizardCygnusMeteorShower).GetField("sky",Hidden).GetValue(effect);
            var nodes=(Vector2[])typeof(PixelWizardCygnusMeteorShower).GetField("LaunchNodes",Hidden).GetValue(null);
            Vector3 point=(Vector3)sky.GetType().GetMethod("Point",Hidden).Invoke(sky,new object[]{nodes[(target+wave*3)%nodes.Length]});
            Assert.That(Vector3.Distance(anchor,point),Is.LessThan(.002f),"The recorded launch must match an actual visible Cygnus anchor.");
            Bounds wings=VisibleSkyBounds(effect);wings.Expand(.2f);
            Assert.That(wings.Contains(anchor),Is.True,"The launch anchor must belong to the authored live constellation.");
            Vector3 viewport=camera.WorldToViewportPoint(anchor);
            Assert.That(viewport.z,Is.GreaterThan(camera.nearClipPlane));
            Assert.That(viewport.x,Is.InRange(0f,1f));Assert.That(viewport.y,Is.InRange(0f,1f));
        }
    }

    [Test]
    public void EachMeteorKeepsItsOwnLaunchAnchorWhileTheReducedHeadFollowsItsCurve()
    {
        var targets=Targets(1);var effect=Create(targets);Vector3 end=targets[0]+Vector3.up*.035f;
        for(int index=0;index<WizardCygnusShowerRules.ShotCount(1);index++)
        {
            float contact=WizardCygnusShowerRules.ImpactTime(index,1);SpriteRenderer star=Stars(effect)[index];
            float launch=contact-WizardCygnusShowerRules.FlightSeconds;
            if(launch>0f){effect.Advance(launch-.00001f);Assert.That(star.enabled,Is.False);}
            effect.Advance(launch+.00001f);Assert.That(star.enabled,Is.True);
            Vector3 start=Launches(effect)[index];
            Assert.That(Vector3.Distance(star.transform.position,start),Is.LessThan(.001f));
            effect.Advance(contact-WizardCygnusShowerRules.FlightSeconds*.5f);
            Assert.That(Launches(effect)[index],Is.EqualTo(start),"The constellation's breathing must not drag an already launched meteor's origin.");
            Assert.That(Vector3.Distance(star.transform.position,Curve(start,end,.5f,index)),Is.LessThan(.002f));
            float coreWidth=(float)typeof(PixelWizardCygnusMeteorShower).GetField("flightBodyWidth",Hidden).GetValue(null);
            Assert.That(coreWidth*Mathf.Abs(star.transform.lossyScale.x),Is.InRange(.4199f,.4801f),
                "At unit source scale, the core width must be 37.5 percent of its original 1.12–1.28 size.");
        }
    }

    [Test]
    public void TheLiveCygnusStaysVisibleUntilTheLastTargetHasBeenHit()
    {
        var effect=Create(Targets(8));float last=WizardCygnusShowerRules.ImpactTime(WizardCygnusShowerRules.ShotCount(8)-1,8);
        foreach(float time in new[]{0f,last*.25f,last*.5f,last*.75f,last})
        {
            effect.Advance(time);Assert.That(VisibleSkyBounds(effect).size.sqrMagnitude,Is.GreaterThan(0f));
        }
    }

    [Test]
    public void SeededSingleArcsAlwaysAdvanceBendToOneSideOnlyOnceAndKeepExactDeterministicEndpoints()
    {
        Vector3 start=new Vector3(-2f,7f,-3f),end=new Vector3(3f,.035f,2f),line=end-start;
        var random=UnityEngine.Random.state;
        try
        {
            float expectedRandom=UnityEngine.Random.value;UnityEngine.Random.state=random;
            for(int seed=0;seed<8;seed++)
            {
                Assert.That(Curve(start,end,0f,seed),Is.EqualTo(start));Assert.That(Curve(start,end,1f,seed),Is.EqualTo(end));
                float largestBend=0f,previousForward=0f,previousBend=0f;bool passedApex=false;Vector3 previous=start;
                Vector3 midpoint=Curve(start,end,.5f,seed)-start;
                Vector3 arcSide=(midpoint-line.normalized*Vector3.Dot(midpoint,line.normalized)).normalized;
                for(int step=1;step<=200;step++)
                {
                    float time=step/200f;Vector3 point=Curve(start,end,time,seed);
                    Assert.That(point,Is.EqualTo(Curve(start,end,time,seed)),"Identical clocks and seeds must produce identical guidance.");
                    Assert.That(Vector3.Distance(point,previous),Is.LessThan(line.magnitude*.08f),"Small clock steps must not teleport the head.");
                    float forward=Vector3.Dot(point-start,line.normalized);
                    Assert.That(forward,Is.GreaterThanOrEqualTo(previousForward-.0001f),"The meteor must not move backward on its way to the target.");
                    Assert.That(forward,Is.InRange(-.0001f,line.magnitude+.0001f));
                    Vector3 lateral=point-start-line.normalized*forward;float offset=Vector3.Dot(lateral,arcSide);
                    Assert.That(offset,Is.GreaterThanOrEqualTo(-.0001f),"The arc must not swing across to the other side.");
                    Assert.That(Vector3.Cross(lateral,arcSide).magnitude,Is.LessThan(.001f),"One arc must keep its bending direction instead of wriggling through different planes.");
                    if(offset<previousBend-.0001f)passedApex=true;
                    if(passedApex)Assert.That(offset,Is.LessThanOrEqualTo(previousBend+.0001f),"The meteor must bend once and then close in without another oscillation.");
                    largestBend=Mathf.Max(largestBend,lateral.magnitude);previous=point;previousForward=forward;previousBend=offset;
                }
                Assert.That(largestBend,Is.GreaterThan(.1f),"Each meteor needs one readable arc before impact.");
                if(seed>0)Assert.That(Vector3.Distance(Curve(start,end,.43f,seed),Curve(start,end,.43f,seed-1)),Is.GreaterThan(.02f));
            }
            Assert.That(UnityEngine.Random.value,Is.EqualTo(expectedRandom),"Guidance must not consume the global per-frame random stream.");
            var allocationMethod=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);
            if(allocationMethod!=null)
            {
                var allocated=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),allocationMethod);
                Curve(start,end,.5f,0);long before=allocated();
                for(int i=0;i<1000;i++)Curve(start,end,(i%100)/100f,i%8);
                long bytes=allocated()-before;Assert.That(bytes,Is.Zero,"Seeded guidance must not allocate while evaluating frame positions.");
            }
        }
        finally{UnityEngine.Random.state=random;}
    }

    [Test]
    public void EachSeededMeteorGainsWorldSpeedThroughoutItsDescent()
    {
        Vector3 start=new Vector3(-2f,7f,-3f),end=new Vector3(3f,.035f,2f);
        const float halfWindow=.005f;
        float intervalSeconds=2f*halfWindow*WizardCygnusShowerRules.FlightSeconds;
        for(int seed=0;seed<8;seed++)
        {
            float firstSpeed=0f,previousSpeed=0f;
            for(int sample=1;sample<=9;sample++)
            {
                float progress=sample*.1f;
                float speed=Vector3.Distance(Curve(start,end,progress-halfWindow,seed),
                    Curve(start,end,progress+halfWindow,seed))/intervalSeconds;
                Assert.That(speed,Is.GreaterThan(previousSpeed),
                    "Equal-duration world-space samples must keep accelerating, including around the arc's apex.");
                if(sample==1)firstSpeed=speed;previousSpeed=speed;
            }
            Assert.That(previousSpeed,Is.GreaterThan(firstSpeed*3f),
                "The final descent must be visibly faster than launch, rather than only slightly speeding up.");
        }
    }

    [Test]
    public void FiveMeteorsReachTheirVictimOnlyAtTheirSharedContactTimesWithinTheOriginalWindow()
    {
        Assert.That(WizardCygnusShowerRules.FlightSeconds,Is.EqualTo(.38f));
        Assert.That(WizardCygnusShowerRules.FirstImpact,Is.EqualTo(WizardCygnusShowerRules.FlightSeconds));
        Assert.That(WizardCygnusShowerRules.WaveInterval,Is.EqualTo(.34f));Assert.That(WizardCygnusShowerRules.Duration,Is.EqualTo(1.6f));
        var effect=Create(Targets(1));var landed=(bool[])typeof(PixelWizardCygnusMeteorShower).GetField("landed",Hidden).GetValue(effect);
        Assert.That(landed.Length,Is.EqualTo(5));
        Assert.That(WizardCygnusShowerRules.ImpactTime(0,1),Is.EqualTo(WizardCygnusShowerRules.FirstImpact));
        Assert.That(WizardCygnusShowerRules.ImpactTime(4,1),Is.EqualTo(WizardCygnusShowerRules.FirstImpact+2f*WizardCygnusShowerRules.WaveInterval));
        for(int index=0;index<WizardCygnusShowerRules.ShotCount(1);index++)
        {
            float contact=WizardCygnusShowerRules.ImpactTime(index,1);
            effect.Advance(contact-.00001f);Assert.That(landed[index],Is.False);
            effect.Advance(contact);Assert.That(landed[index],Is.True);
            if(index+1<landed.Length)Assert.That(landed[index+1],Is.False);
        }
    }

    [Test]
    public void EveryDamageStarReachesItsTrackedVictimExactlyAtTheSharedGameplayContact()
    {
        var targets=Targets(8);Vector3 offset=Vector3.zero;
        var effect=Create(targets,i=>targets[i]+offset);
        for(int index=0;index<WizardCygnusShowerRules.ShotCount(targets.Length);index++)
        {
            int target=index%targets.Length,wave=index/targets.Length;
            offset=new Vector3(wave*.13f,0f,target*.015f);
            float contact=WizardCygnusShowerRules.ImpactTime(index,targets.Length);
            Vector3 expected=targets[target]+offset+Vector3.up*.035f;
            effect.Advance(contact-.00001f);SpriteRenderer star=Stars(effect)[index];
            Assert.That(star.enabled,Is.True);
            Assert.That(Vector3.Distance(star.transform.position,expected),Is.LessThan(.002f));
            effect.Advance(contact);
            Assert.That(Landings(effect)[index],Is.EqualTo(targets[target]+offset));
        }
    }

    [Test]
    public void ATargetReversalCannotMoveTheAlreadyRecordedWorldSpaceTrail()
    {
        Vector3 offset=Vector3.zero;var targets=Targets(1);
        var effect=Create(targets,i=>targets[i]+offset);effect.Advance(.1f);
        Vector3[] history=TrailPoints(effect);int savedCount=TrailCounts(effect)[0];
        Assert.That(savedCount,Is.GreaterThan(1));var saved=new Vector3[savedCount];Array.Copy(history,saved,savedCount);
        offset=Vector3.right*12f;effect.Advance(.14f);
        offset=Vector3.left*12f;effect.Advance(.2f);
        Assert.That(TrailCounts(effect)[0],Is.GreaterThan(savedCount),"New flight positions must append after the existing history.");
        for(int i=0;i<savedCount;i++)Assert.That(TrailPoints(effect)[i],Is.EqualTo(saved[i]),
            "A target turn must not recalculate positions the meteor has already passed.");
        ParticleSystem.Particle anchor=TrailPixelAt(effect,saved[savedCount-1],.2f);
        Assert.That(anchor.position,Is.EqualTo(saved[savedCount-1]),"The rendered tail must stay on the recorded path rather than the new target curve.");
    }

    [Test]
    public void RecordedTailPixelsShrinkFadeAndExpireSoonAfterTheMeteorLands()
    {
        var effect=Create(Targets(1));effect.Advance(.1f);
        int savedCount=TrailCounts(effect)[0];Assert.That(savedCount,Is.GreaterThan(1));
        Vector3 point=TrailPoints(effect)[savedCount-1];
        ParticleSystem.Particle fresh=TrailPixelAt(effect,point,.11f),older=TrailPixelAt(effect,point,.18f);
        Assert.That(older.position,Is.EqualTo(fresh.position));
        Assert.That(older.startSize3D.sqrMagnitude,Is.LessThan(fresh.startSize3D.sqrMagnitude),"A recorded tail pixel must shrink as it ages.");
        Assert.That(older.startColor.a,Is.LessThan(fresh.startColor.a),"A recorded tail pixel must fade as it ages.");
        float contact=WizardCygnusShowerRules.ImpactTime(0,1);effect.Advance(contact);
        Assert.That(RenderOnlyTrail(effect,contact+.05f),Is.GreaterThan(0),"The final flight samples must finish naturally after impact.");
        Assert.That(RenderOnlyTrail(effect,contact+.1401f),Is.Zero,"The flight tail must be gone within .14 seconds of contact, while the separate explosion can continue.");
    }

    [Test]
    public void AuthoredImpactDrawingsPlayAtTheirContactPointWithoutMagnifyingTheFlightStar()
    {
        var targets=Targets(1);var effect=Create(targets);float contact=WizardCygnusShowerRules.ImpactTime(0,1);
        effect.Advance(contact+.01f);SpriteRenderer impact=Stars(effect)[0];
        Vector3 position=impact.transform.position,scale=impact.transform.localScale;
        float authoredWidth=(float)typeof(PixelWizardCygnusMeteorShower).GetField("impactVisibleWidth",Hidden).GetValue(null);
        Assert.That(authoredWidth*Mathf.Abs(scale.x),Is.EqualTo(2.7f).Within(.0001f),"The explosion width must also shrink by 25 percent.");
        Sprite previous=null;
        foreach(float age in new[]{.01f,.065f,.16f,.28f})
        {
            effect.Advance(contact+age);
            Assert.That(impact.enabled,Is.True,"The whole authored impact sequence must remain visible.");
            Assert.That(impact.transform.position,Is.EqualTo(position),"The blast must stay at its frozen contact point.");
            Assert.That(impact.transform.localScale,Is.EqualTo(scale),"Impact growth must come from the drawn sequence, not enlarging one star.");
            if(previous!=null)Assert.That(impact.sprite,Is.Not.SameAs(previous));previous=impact.sprite;
        }
        Vector3 viewport=camera.WorldToViewportPoint(position);
        Assert.That(viewport.z,Is.GreaterThan(0f));Assert.That(viewport.x,Is.InRange(0f,1f));Assert.That(viewport.y,Is.InRange(0f,1f));
        effect.Advance(contact+.16f);int count=Particles(effect).GetParticles(pixels);bool expandedRing=false;
        for(int i=0;i<count;i++)
        {
            Vector3 point=effect.transform.TransformPoint(pixels[i].position),delta=point-targets[0];
            if(Mathf.Abs(delta.y)<.15f&&new Vector2(delta.x,delta.z).magnitude>.75f&&pixels[i].startColor.a>32)expandedRing=true;
        }
        Assert.That(expandedRing,Is.True,"The ground shockwave must spread visibly beyond the former small impact ring.");
        effect.Advance(contact+.35001f);Assert.That(impact.enabled,Is.False,"The finished blast must not leave an enlarged star behind.");
    }

    [Test]
    public void ThirtyTwoVictimsStayWithinThePixelBudgetWithoutAddingPersistentBackgroundRain()
    {
        var effect=Create(Targets(32));int peak=0;
        int frames=Mathf.CeilToInt(WizardCygnusShowerRules.DurationForTargets(32)/.025f);
        for(int frame=0;frame<=frames;frame++)
        {
            effect.Advance(frame*.025f);int count=Particles(effect).GetParticles(pixels);
            Assert.That(count,Is.InRange(0,6144));peak=Mathf.Max(peak,count);
        }
        Assert.That(peak,Is.GreaterThan(0));
        Assert.That(Particles(effect).particleCount,Is.Zero);
    }

    [Test]
    public void AReleasedShowerFreezesItsTargetsAndFinishesAfterItsCasterIsDestroyed()
    {
        var targets=Targets(8);Vector3 offset=Vector3.right;
        bool frozen=false;var effect=Create(targets,i=>{if(frozen)throw new InvalidOperationException("Target callback survived FreezeTargets");return targets[i]+offset;});
        effect.Advance(.1f);effect.FreezeTargets();frozen=true;offset=Vector3.one*50f;
        Object.DestroyImmediate(source.gameObject);
        effect.Advance(WizardCygnusShowerRules.ImpactTime(8,8));
        Assert.That(Landings(effect)[8],Is.EqualTo(targets[0]+Vector3.right));
        Assert.That(Particles(effect).particleCount,Is.GreaterThan(0));
        Assert.That(typeof(PixelWizardCygnusMeteorShower).GetMethod("Update",Hidden),Is.Null);
        Assert.That(typeof(PixelWizardCygnusMeteorShower).GetMethod("LateUpdate",Hidden),Is.Null);
    }

    [Test]
    public void TheFinalImpactRetainsItsOriginalPointThreeFiveSecondTail()
    {
        var effect=Create(Targets(1));float last=WizardCygnusShowerRules.ImpactTime(WizardCygnusShowerRules.ShotCount(1)-1,1);
        effect.Advance(last+.34f);Assert.That(Particles(effect).particleCount,Is.GreaterThan(0));
        effect.Advance(last+.35001f);Assert.That(Particles(effect).particleCount,Is.Zero);
        foreach(var star in Stars(effect))Assert.That(star.enabled,Is.False);
    }

    [TestCase(8)][TestCase(32)]
    public void TheExactLifetimeClearsAllStarsAndImpactPixelsBeforeWaveCleanup(int targetCount)
    {
        var effect=Create(Targets(targetCount));float lifetime=WizardCygnusShowerRules.DurationForTargets(targetCount);
        effect.Advance(WizardCygnusShowerRules.ImpactTime(WizardCygnusShowerRules.ShotCount(targetCount)-1,targetCount)+.1f);Assert.That(Particles(effect).particleCount,Is.GreaterThan(0));
        effect.Advance(lifetime);
        Assert.That(effect.RemainingLifetime,Is.Zero);Assert.That(Particles(effect).particleCount,Is.Zero);
        foreach(var star in Stars(effect))Assert.That(star.enabled,Is.False);
        foreach(var wing in effect.GetComponentsInChildren<MeshRenderer>())Assert.That(wing.enabled,Is.False);
        effect.Cancel();Assert.That(effect==null,Is.True);
    }

    [TestCase(30)][TestCase(120)]
    public void TheExplicitClockProducesTheSamePixelsAtDifferentFrameRates(int fps)
    {
        var stepped=Create(Targets(8));var direct=Create(Targets(8));float finish=WizardCygnusShowerRules.ImpactTime(8,8)+.1f;
        for(int frame=0;frame/(float)fps<finish;frame++)stepped.Advance(frame/(float)fps);
        stepped.Advance(finish);direct.Advance(finish);
        AssertSameFrame(stepped,direct);
    }

    [Test]
    public void RewindingTheClockResetsLaunchAndImpactSnapshotsAndReplayingProducesTheSameFrame()
    {
        var replay=Create(Targets(8));var direct=Create(Targets(8));float finish=WizardCygnusShowerRules.ImpactTime(8,8)+.1f;
        replay.Advance(finish);replay.Advance(.01f);
        for(int i=1;i<Launched(replay).Length;i++)
            if(WizardCygnusShowerRules.ImpactTime(i,8)-WizardCygnusShowerRules.FlightSeconds>.01f)
                Assert.That(Launched(replay)[i],Is.False,"A rewind must not retain future launch snapshots.");
        for(int frame=1;frame/120f<finish;frame++)replay.Advance(frame/120f);
        replay.Advance(finish);direct.Advance(finish);AssertSameFrame(replay,direct);
    }

    private void AssertSameFrame(PixelWizardCygnusMeteorShower actual,PixelWizardCygnusMeteorShower expected)
    {
        int count=Particles(actual).GetParticles(pixels);Assert.That(Particles(expected).GetParticles(comparison),Is.EqualTo(count));
        for(int i=0;i<count;i++)
        {
            Assert.That(comparison[i].position,Is.EqualTo(pixels[i].position));Assert.That(comparison[i].startColor,Is.EqualTo(pixels[i].startColor));
            Assert.That(comparison[i].startSize3D,Is.EqualTo(pixels[i].startSize3D));Assert.That(comparison[i].rotation,Is.EqualTo(pixels[i].rotation));
        }
        var first=Stars(actual);var second=Stars(expected);
        for(int i=0;i<first.Length;i++)
        {
            Assert.That(first[i].enabled,Is.EqualTo(second[i].enabled));
            if(first[i].enabled)
            {
                Assert.That(first[i].transform.position,Is.EqualTo(second[i].transform.position));
                Assert.That(first[i].transform.rotation,Is.EqualTo(second[i].transform.rotation));
                Assert.That(first[i].transform.localScale,Is.EqualTo(second[i].transform.localScale));
                Assert.That(first[i].color,Is.EqualTo(second[i].color));
                Assert.That(first[i].sprite,Is.SameAs(second[i].sprite),"The explicit clock must also select the same cached animation drawing.");
            }
            Assert.That(Launched(actual)[i],Is.EqualTo(Launched(expected)[i]));
            if(Launched(actual)[i])Assert.That(Launches(actual)[i],Is.EqualTo(Launches(expected)[i]));
        }
    }

    private PixelWizardCygnusMeteorShower Create(Vector3[] targets,Func<int,Vector3> live=null)
    {var effect=PixelWizardCygnusMeteorShower.Create(Vector3.zero,targets,source,live);effects.Add(effect);return effect;}
    private static Vector3[] Targets(int count)
    {
        var targets=new Vector3[count];
        for(int i=0;i<count;i++)targets[i]=new Vector3((i%8-3.5f)*.9f,0f,(i/8-1.5f)*.85f);
        return targets;
    }
    private static ParticleSystem Particles(PixelWizardCygnusMeteorShower effect)
    {return (ParticleSystem)typeof(PixelWizardCygnusMeteorShower).GetField("particles",Hidden).GetValue(effect);}
    private static SpriteRenderer[] Stars(PixelWizardCygnusMeteorShower effect)
    {return (SpriteRenderer[])typeof(PixelWizardCygnusMeteorShower).GetField("stars",Hidden).GetValue(effect);}
    private static Vector3[] Landings(PixelWizardCygnusMeteorShower effect)
    {return (Vector3[])typeof(PixelWizardCygnusMeteorShower).GetField("landings",Hidden).GetValue(effect);}
    private static Vector3[] Launches(PixelWizardCygnusMeteorShower effect)
    {return (Vector3[])typeof(PixelWizardCygnusMeteorShower).GetField("launches",Hidden).GetValue(effect);}
    private static bool[] Launched(PixelWizardCygnusMeteorShower effect)
    {return (bool[])typeof(PixelWizardCygnusMeteorShower).GetField("launched",Hidden).GetValue(effect);}
    private static Vector3[] TrailPoints(PixelWizardCygnusMeteorShower effect)
    {return (Vector3[])typeof(PixelWizardCygnusMeteorShower).GetField("trailPoints",Hidden).GetValue(effect);}
    private static int[] TrailCounts(PixelWizardCygnusMeteorShower effect)
    {return (int[])typeof(PixelWizardCygnusMeteorShower).GetField("trailCounts",Hidden).GetValue(effect);}
    private static int RenderOnlyTrail(PixelWizardCygnusMeteorShower effect,float clock)
    {
        typeof(PixelWizardCygnusMeteorShower).GetField("count",Hidden).SetValue(effect,0);
        typeof(PixelWizardCygnusMeteorShower).GetMethod("RenderTrail",Hidden).Invoke(effect,new object[]{0,clock});
        return (int)typeof(PixelWizardCygnusMeteorShower).GetField("count",Hidden).GetValue(effect);
    }
    private static ParticleSystem.Particle TrailPixelAt(PixelWizardCygnusMeteorShower effect,Vector3 point,float clock)
    {
        int count=RenderOnlyTrail(effect,clock);
        var buffer=(ParticleSystem.Particle[])typeof(PixelWizardCygnusMeteorShower).GetField("buffer",Hidden).GetValue(effect);
        for(int i=0;i<count;i++)if(buffer[i].position==point)return buffer[i];
        Assert.Fail("The unexpired recorded world-space trail sample must still be rendered at its original anchor.");
        return default(ParticleSystem.Particle);
    }
    private static Rect AtlasCell(Texture2D atlas,int frame)
    {
        int column=frame%4,row=frame/4;
        int left=column*atlas.width/4,right=(column+1)*atlas.width/4;
        int bottom=(1-row)*atlas.height/2,top=(2-row)*atlas.height/2;
        return new Rect(left,bottom,right-left,top-bottom);
    }
    private static Vector3 Curve(Vector3 start,Vector3 end,float time,int seed)
    {
        if(guidance==null)guidance=(Func<Vector3,Vector3,float,int,Vector3>)Delegate.CreateDelegate(
            typeof(Func<Vector3,Vector3,float,int,Vector3>),typeof(PixelWizardCygnusMeteorShower).GetMethod("CurvePoint",Hidden));
        return guidance(start,end,time,seed);
    }
    private static Bounds VisibleSkyBounds(PixelWizardCygnusMeteorShower effect)
    {
        object sky=typeof(PixelWizardCygnusMeteorShower).GetField("sky",Hidden).GetValue(effect);
        var renderers=(MeshRenderer[])sky.GetType().GetField("renderers",Hidden).GetValue(sky);
        var property=new MaterialPropertyBlock();Bounds result=new Bounds();bool found=false;
        foreach(var renderer in renderers)
        {
            renderer.GetPropertyBlock(property);if(!renderer.enabled||property.GetFloat("_Opacity")<=.001f)continue;
            if(found)result.Encapsulate(renderer.bounds);else{result=renderer.bounds;found=true;}
        }
        Assert.That(found,Is.True,"The live authored Cygnus must remain enabled with nonzero opacity while it launches meteors.");
        return result;
    }
}
