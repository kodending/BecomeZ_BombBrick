using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is=NUnit.Framework.Is;
using Object=UnityEngine.Object;

public sealed class PixelSabertoothDevourTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const string HeadResource="CombatEffects/SabertoothDevour/DevourHeadParts";
    private const string NeonResource="CombatEffects/SabertoothDevour/DevourNeon";
    private readonly List<PixelSabertoothDevour> effects=new List<PixelSabertoothDevour>();
    private readonly ParticleSystem.Particle[] before=new ParticleSystem.Particle[512],after=new ParticleSystem.Particle[512];
    private GameObject host,cameraHost,victimHost;
    private SpriteRenderer source;
    private Texture2D texture;
    private Sprite sprite;
    private Vector3 mouth,center;
    private List<Transform> victims;

    [SetUp] public void Setup()
    {
        host=new GameObject("Devour VFX source");source=host.AddComponent<SpriteRenderer>();
        texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,2f);source.sprite=sprite;
        source.sortingOrder=10;host.transform.position=Vector3.left*2f+Vector3.up*.5f;
        cameraHost=new GameObject("Devour unchanged camera");var camera=cameraHost.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-5f),Quaternion.Euler(58f,0f,0f));
        source.transform.rotation=camera.transform.rotation;
        victimHost=new GameObject("Devour unchanged victim");victimHost.transform.position=new Vector3(1f,0f,.3f);
        victims=new List<Transform>{victimHost.transform};mouth=new Vector3(-1.5f,.6f,0f);center=Vector3.right;
    }

    [TearDown] public void Cleanup()
    {
        foreach(var effect in effects)if(effect!=null)effect.Cancel(true);effects.Clear();
        Object.DestroyImmediate(host);Object.DestroyImmediate(cameraHost);Object.DestroyImmediate(victimHost);
        Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
    }

    [TestCase(false)] [TestCase(true)]
    public void TheAuthoredSabertoothHeadOpensAtItsHingesAndSnapsClosedOnce(bool left)
    {
        var effect=Create(left);
        var atlas=Resources.Load<Texture2D>(HeadResource);Assert.That(atlas,Is.Not.Null);
        Assert.That(effect.UpperJaw.sprite.texture,Is.SameAs(atlas));
        Assert.That(effect.LowerJaw.sprite.texture,Is.SameAs(atlas));
        Assert.That(effect.LowerJaw.sprite,Is.Not.SameAs(effect.UpperJaw.sprite),
            "The drawn upper head and lower jaw must use their own authored atlas regions.");
        Assert.That(effect.UpperJaw.sprite.rect.Overlaps(effect.LowerJaw.sprite.rect),Is.False);
        Assert.That(atlas.filterMode,Is.EqualTo(FilterMode.Point));
        var neon=Resources.Load<Shader>(NeonResource);Assert.That(neon,Is.Not.Null);
        Assert.That(neon.isSupported,Is.True,"The Resources neon shader must compile for the active renderer.");
        Assert.That(neon.name,Does.Not.StartWith("Hidden/"));
        Material headMaterial=effect.UpperJaw.sharedMaterial;
        Material particleMaterial=effect.Particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        Assert.That(headMaterial.shader,Is.SameAs(neon));
        Assert.That(effect.LowerJaw.sharedMaterial,Is.SameAs(headMaterial));
        Assert.That(headMaterial.mainTexture,Is.SameAs(atlas));
        Assert.That(headMaterial.HasProperty("_Flash"),Is.True);
        Assert.That(headMaterial.GetFloat("_ParticleMode"),Is.EqualTo(0f));
        Assert.That(particleMaterial.shader,Is.SameAs(neon));
        Assert.That(particleMaterial.GetFloat("_ParticleMode"),Is.EqualTo(1f));
        Assert.That(particleMaterial.GetFloat("_DstBlend"),
            Is.EqualTo((float)UnityEngine.Rendering.BlendMode.One));
        effect.Advance(.2f,mouth);
        Assert.That(effect.UpperJaw.enabled,Is.True,"The actual sabertooth head must appear during the inhale.");
        effect.Advance(SabertoothDevourRules.PullEnd+.2f,mouth);
        Assert.That(effect.UpperJaw.enabled,Is.True);Assert.That(effect.LowerJaw.enabled,Is.True);
        Assert.That(effect.LowerJaw.flipY,Is.False,"A separate lower-jaw drawing must not be a flipped copy of the head.");
        Assert.That(Mathf.Sign(effect.UpperJaw.transform.localScale.x),Is.EqualTo(left?-1f:1f));
        Assert.That(effect.LowerJaw.transform.localScale,Is.EqualTo(effect.UpperJaw.transform.localScale),
            "Both atlas pieces must keep their authored size ratio while rotating around their hinges.");
        Assert.That(effect.LowerJaw.transform.position,Is.EqualTo(effect.UpperJaw.transform.position));
        float height=effect.UpperJaw.transform.localScale.y;
        float openAngle=Quaternion.Angle(effect.UpperJaw.transform.rotation,effect.LowerJaw.transform.rotation);
        Assert.That(openAngle,Is.GreaterThan(20f));
        effect.Advance(SabertoothDevourRules.SnapStart-.01f,mouth);
        float beforeFlash=headMaterial.GetFloat("_Flash");
        effect.Advance(SabertoothDevourRules.HitTime,mouth);
        Assert.That(Quaternion.Angle(effect.UpperJaw.transform.rotation,effect.LowerJaw.transform.rotation),
            Is.LessThan(1f),"Both authored parts must meet at the single hit, rather than squash their fangs flat.");
        Assert.That(effect.UpperJaw.transform.localScale.y,Is.GreaterThanOrEqualTo(height*.9f));
        float hitFlash=headMaterial.GetFloat("_Flash");
        Assert.That(hitFlash,Is.GreaterThan(beforeFlash+.5f));
        Assert.That(hitFlash,Is.GreaterThanOrEqualTo(.95f),
            "The shader must turn the neon head gold at the actual bite, independently of the sprite tint.");
        int count=effect.Particles.GetParticles(before);Assert.That(count,Is.GreaterThan(0));
        effect.Advance(SabertoothDevourRules.HitTime,mouth);
        Assert.That(effect.Particles.GetParticles(after),Is.EqualTo(count));
        for(int i=0;i<count;i++)Assert.That(after[i].position,Is.EqualTo(before[i].position));
        effect.Advance(SabertoothDevourRules.HitTime+.16f,mouth);
        Assert.That(headMaterial.GetFloat("_Flash"),Is.LessThan(hitFlash*.1f),
            "The gold flash must clear while the head and recovery are still visible.");
    }

    [Test]
    public void ExternalClockMovesCurvedMintStreamsWithoutMovingVictimsOrChangingTheCameraOrSource()
    {
        var effect=Create(false);Vector3 victimPosition=victimHost.transform.position;
        Vector3 cameraPosition=cameraHost.transform.position;Quaternion cameraRotation=cameraHost.transform.rotation;
        float fov=cameraHost.GetComponent<Camera>().fieldOfView;
        victims.Clear();effect.Advance(.3f,mouth);
        int count=effect.Particles.GetParticles(before);Assert.That(count,Is.GreaterThan(20));
        effect.Advance(.35f,mouth);
        count=Mathf.Min(count,effect.Particles.GetParticles(after));
        int moved=0,mint=0,visible=0,inward=0,aligned=0,curved=0,streaks=0;
        Quaternion view=Camera.main==null?source.transform.rotation:Camera.main.transform.rotation;
        Vector3 right=view*Vector3.right,up=view*Vector3.up;
        for(int i=0;i<count;i++)
        {
            Vector3 movement=after[i].position-before[i].position;
            if(movement.magnitude>.01f)moved++;
            if(after[i].startColor.g>after[i].startColor.r)mint++;
            if(after[i].startColor.a>0)visible++;
            if(Vector3.Dot(movement,mouth-before[i].position)>0f)inward++;
            float angle=before[i].rotation*Mathf.Deg2Rad;
            Vector3 streak=right*Mathf.Sin(angle)+up*Mathf.Cos(angle);
            if(Vector3.Dot(movement.normalized,streak)>.9f)aligned++;
            if(Mathf.Abs(Mathf.DeltaAngle(before[i].rotation,after[i].rotation))>.1f)curved++;
            if(after[i].startSize3D.y>after[i].startSize3D.x*2f)streaks++;
        }
        Assert.That(moved,Is.GreaterThan(count/2));Assert.That(mint,Is.GreaterThan(count/2));
        Assert.That(visible,Is.GreaterThan(count/2));Assert.That(inward,Is.GreaterThan(count/2));
        Assert.That(aligned,Is.GreaterThan(count/2),"The visible long axis must follow the stream's movement.");
        Assert.That(curved,Is.GreaterThan(count/4),"Wind streaks must turn along the curves instead of flying as fixed rays.");
        Assert.That(streaks,Is.GreaterThan(count/2));
        effect.Advance(.3f,mouth);
        Assert.That(effect.Particles.GetParticles(after),Is.EqualTo(count));
        for(int i=0;i<count;i++)Assert.That(after[i].position,Is.EqualTo(before[i].position),
            "Rewinding the external clock must redraw the same curved stream instead of accumulating particles.");
        effect.Advance(.35f,mouth);
        Assert.That(effect.Particles.isPaused,Is.True,"Advance alone owns the visual timeline.");
        Assert.That(effect.Age,Is.EqualTo(.35f));Assert.That(effect.Mouth,Is.EqualTo(mouth));
        Assert.That(victimHost.transform.position,Is.EqualTo(victimPosition));
        Assert.That(cameraHost.transform.position,Is.EqualTo(cameraPosition));
        Assert.That(cameraHost.transform.rotation,Is.EqualTo(cameraRotation));
        Assert.That(cameraHost.GetComponent<Camera>().fieldOfView,Is.EqualTo(fov));
        Assert.That(source.enabled,Is.True);Assert.That(source.color,Is.EqualTo(Color.white));
        Assert.That(source.sortingOrder,Is.EqualTo(10));Assert.That(source.sprite,Is.SameAs(sprite));
    }

    [Test]
    public void TheSingleBiteProducesOneExpandingGoldBurstAtTheActualHitTime()
    {
        var effect=Create(false);effect.Advance(SabertoothDevourRules.HitTime-.01f,mouth);
        int count=effect.Particles.GetParticles(before),beforeGold=GoldPixels(before,count);
        effect.Advance(SabertoothDevourRules.HitTime,mouth);
        count=effect.Particles.GetParticles(before);
        Assert.That(count,Is.LessThanOrEqualTo(320));
        Assert.That(GoldPixels(before,count),Is.GreaterThanOrEqualTo(220),
            "The heavy bite needs a dense gold eruption, not just the small charging swirl.");
        Assert.That(GoldPixels(before,count),Is.GreaterThan(beforeGold+20),
            "The bite's gold pressure and shards must erupt on contact, not during the preparation.");
        float impactSpread=Spread(before,count,center);
        effect.Advance(SabertoothDevourRules.HitTime+.08f,mouth);
        int later=effect.Particles.GetParticles(after);
        Assert.That(Spread(after,later,center),Is.GreaterThan(impactSpread+.05f));
        effect.Advance(SabertoothDevourRules.HitTime+.01f,mouth);effect.Particles.GetParticles(before);
        Vector3 earlyStart=before[0].position;
        effect.Advance(SabertoothDevourRules.HitTime+.03f,mouth);effect.Particles.GetParticles(before);
        float earlyTravel=Vector3.Distance(earlyStart,before[0].position);
        effect.Advance(SabertoothDevourRules.HitTime+.13f,mouth);effect.Particles.GetParticles(before);
        Vector3 lateStart=before[0].position;
        effect.Advance(SabertoothDevourRules.HitTime+.15f,mouth);effect.Particles.GetParticles(before);
        Assert.That(Vector3.Distance(lateStart,before[0].position),Is.LessThan(earlyTravel*.6f),
            "Shards must launch hard and visibly brake instead of drifting at a constant speed.");
        effect.Advance(SabertoothDevourRules.Duration,mouth);
        Assert.That(effect==null,Is.True,"The burst and head must finish without starting a second bite.");
    }

    [TestCase(false)] [TestCase(true)]
    public void TheFastBiteClampsShutThenSettlesWithoutReopeningOrShakingItsJaw(bool left)
    {
        var effect=Create(left);float timeScale=Time.timeScale;
        Assert.That(SabertoothDevourRules.HitTime-SabertoothDevourRules.SnapStart,Is.EqualTo(.055f).Within(.00001f));
        Assert.That(SabertoothDevourRules.Duration,Is.EqualTo(2.66f).Within(.00001f));
        effect.Advance(SabertoothDevourRules.SnapStart,mouth);
        float openAngle=Quaternion.Angle(effect.UpperJaw.transform.rotation,effect.LowerJaw.transform.rotation);
        effect.Advance(SabertoothDevourRules.SnapStart+.025f,mouth);
        Assert.That(Quaternion.Angle(effect.UpperJaw.transform.rotation,effect.LowerJaw.transform.rotation),
            Is.LessThan(openAngle*.25f),"The first half of the snap should close with a decisive fast motion.");
        effect.Advance(SabertoothDevourRules.HitTime,mouth);
        Quaternion closed=effect.UpperJaw.transform.rotation;
        Vector3 contact=effect.UpperJaw.transform.position,scale=effect.UpperJaw.transform.localScale;
        Vector3 forward=closed*Vector3.right*(left?-1f:1f);
        for(int step=0;step<=22;step++)
        {
            effect.Advance(SabertoothDevourRules.HitTime+step*.005f,mouth);
            Assert.That(Quaternion.Angle(effect.UpperJaw.transform.rotation,closed),Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(effect.LowerJaw.transform.rotation,closed),Is.LessThan(.01f),
                "Both jaws must remain clamped during the heavy hold, with no sinusoidal rebound.");
            Assert.That(effect.LowerJaw.transform.position,Is.EqualTo(effect.UpperJaw.transform.position));
            Assert.That(effect.UpperJaw.transform.localScale,Is.EqualTo(scale));
            Assert.That(Vector3.Dot(effect.UpperJaw.transform.position-contact,forward),Is.InRange(-.0001f,.1501f));
        }
        Assert.That(Vector3.Dot(effect.UpperJaw.transform.position-contact,forward),Is.GreaterThan(.1f));
        effect.Advance(SabertoothDevourRules.HitTime+.035f,mouth);
        Assert.That(effect.UpperJaw.sharedMaterial.GetFloat("_Flash"),Is.EqualTo(1f).Within(.0001f));
        effect.Advance(SabertoothDevourRules.HitTime+SabertoothDevourRules.BiteClampHold+.18f,mouth);
        Assert.That(Vector3.Distance(effect.UpperJaw.transform.position,contact),Is.LessThan(.0001f));
        Assert.That(Quaternion.Angle(effect.LowerJaw.transform.rotation,closed),Is.LessThan(.01f));
        Assert.That(Time.timeScale,Is.EqualTo(timeScale),"Only the presentation should compress; combat time must keep running.");
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void BitePoseAndBurstAtTheSameClockTimeDoNotDependOnIntermediateFrameSteps(int fps)
    {
        var stepped=Create(false);var direct=Create(false);
        float end=SabertoothDevourRules.HitTime+.08f;
        for(int frame=0;frame/(float)fps<end;frame++)stepped.Advance(frame/(float)fps,mouth);
        stepped.Advance(end,mouth);direct.Advance(end,mouth);
        Assert.That(stepped.UpperJaw.transform.position,Is.EqualTo(direct.UpperJaw.transform.position));
        Assert.That(stepped.UpperJaw.transform.rotation,Is.EqualTo(direct.UpperJaw.transform.rotation));
        Assert.That(stepped.LowerJaw.transform.rotation,Is.EqualTo(direct.LowerJaw.transform.rotation));
        Assert.That(stepped.UpperJaw.sharedMaterial.GetFloat("_Flash"),
            Is.EqualTo(direct.UpperJaw.sharedMaterial.GetFloat("_Flash")));
        int count=stepped.Particles.GetParticles(before);
        Assert.That(direct.Particles.GetParticles(after),Is.EqualTo(count));
        for(int i=0;i<count;i++)
        {
            Assert.That(before[i].position,Is.EqualTo(after[i].position));
            Assert.That(before[i].startSize3D,Is.EqualTo(after[i].startSize3D));
            Assert.That(before[i].startColor,Is.EqualTo(after[i].startColor));
        }
    }

    [Test]
    public void OrdinaryCancellationFadesTheCurrentJawInsteadOfSnappingOrRunningAnotherHit()
    {
        var effect=Create(false);effect.Advance(SabertoothDevourRules.SnapStart-.1f,mouth);
        float alpha=effect.UpperJaw.color.a,scale=effect.UpperJaw.transform.localScale.y,age=effect.Age;
        effect.Cancel();
        typeof(PixelSabertoothDevour).GetMethod("AdvanceFade",Private).Invoke(effect,new object[]{PixelSabertoothDevour.FadeDuration*.5f});
        Assert.That(effect.UpperJaw.color.a,Is.GreaterThan(0f).And.LessThan(alpha));
        Assert.That(effect.UpperJaw.transform.localScale.y,Is.EqualTo(scale));Assert.That(effect.Age,Is.EqualTo(age));
        typeof(PixelSabertoothDevour).GetMethod("AdvanceFade",Private).Invoke(effect,new object[]{PixelSabertoothDevour.FadeDuration});
        Assert.That(effect==null,Is.True);
    }

    [Test]
    public void ResetAndNaturalCompletionReleaseOnlyOwnedGraphicsResources()
    {
        var first=Create(false);var second=Create(true);
        Sprite upperSprite=first.UpperJaw.sprite,lowerSprite=first.LowerJaw.sprite;
        Texture atlas=upperSprite.texture;
        Material headMaterial=first.UpperJaw.sharedMaterial;
        Material ownedMaterial=first.Particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        Texture ownedPixels=ownedMaterial.mainTexture;
        Assert.That(atlas,Is.SameAs(Resources.Load<Texture2D>(HeadResource)));
        Assert.That(second.UpperJaw.sprite.texture,Is.SameAs(atlas));
        Assert.That(second.UpperJaw.sprite,Is.Not.SameAs(upperSprite));
        first.Cancel(true);
        Assert.That(first==null,Is.True);Assert.That(upperSprite==null,Is.True);Assert.That(lowerSprite==null,Is.True);
        Assert.That(headMaterial==null,Is.True);
        Assert.That(ownedMaterial==null,Is.True);Assert.That(ownedPixels==null,Is.True);
        Assert.That(atlas!=null,Is.True,"The shared Resources atlas must outlive an individual cast.");
        Assert.That(sprite!=null,Is.True);Assert.That(texture!=null,Is.True);
        second.Advance(SabertoothDevourRules.HitTime,mouth);Assert.That(second.UpperJaw.enabled,Is.True);
        second.Advance(SabertoothDevourRules.Duration,mouth);Assert.That(second==null,Is.True);
    }

    [Test]
    public void WarmedVisualUpdatesDoNotAllocateManagedMemory()
    {
        var effect=Create(false);effect.Advance(.8f,mouth);effect.Advance(SabertoothDevourRules.HitTime,mouth);
        TestDelegate work=()=>
        {
            effect.Advance(.8f,mouth);effect.Advance(SabertoothDevourRules.PullEnd+.2f,mouth);
            effect.Advance(SabertoothDevourRules.HitTime,mouth);
        };
        work();Assert.That(work,UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
    }

    [TestCase(false)] [TestCase(true)]
    public void TheLiveJawIsMoreThanTwiceAsLargeWithoutMovingTheCasterOrVictims(bool left)
    {
        Vector3 position=host.transform.position,victimPosition=victimHost.transform.position;
        var enlarged=Create(left);
        var prior=PixelSabertoothDevour.Create(source,left?Vector3.left:Vector3.right,center,3f,1.8f,victims,1f);
        effects.Add(prior);
        foreach(float time in new[]{.3f,SabertoothDevourRules.PullEnd,SabertoothDevourRules.HitTime})
        {
            enlarged.Advance(time,mouth);prior.Advance(time,mouth);
            Assert.That(enlarged.UpperJaw.transform.localScale.y/prior.UpperJaw.transform.localScale.y,
                Is.EqualTo(2.2f).Within(.001f));
            Assert.That(enlarged.LowerJaw.transform.localScale,Is.EqualTo(enlarged.UpperJaw.transform.localScale));
            Assert.That(enlarged.Age,Is.EqualTo(prior.Age));
        }
        Assert.That(host.transform.position,Is.EqualTo(position));
        Assert.That(victimHost.transform.position,Is.EqualTo(victimPosition));
    }

    [TestCase(false)] [TestCase(true)]
    public void TheReturnedJawStartsAtTheStageWidthAndHingeThenGrowsIntoTheLiveSkill(bool left)
    {
        var effect=Create(left);var native=Create(left);
        effect.BeginCinematicHandoff(5f);
        float start=SabertoothDevourRules.PullStart;
        effect.Advance(start,mouth);
        float width=effect.UpperJaw.sprite.bounds.size.x*Mathf.Abs(effect.UpperJaw.transform.localScale.x);
        Assert.That(width,Is.EqualTo(5f).Within(.001f));
        Assert.That(effect.UpperJaw.transform.position,Is.EqualTo(mouth));
        Assert.That(Quaternion.Angle(effect.UpperJaw.transform.rotation,effect.LowerJaw.transform.rotation),
            Is.GreaterThan(20f));
        Assert.That(effect.UpperJaw.color.a,Is.EqualTo(.32f).Within(.001f));
        effect.Advance(start+.25f,mouth);
        float halfway=effect.UpperJaw.transform.localScale.y;
        effect.Advance(start+.5f,mouth);native.Advance(start+.5f,mouth);
        Assert.That(effect.UpperJaw.transform.localScale,Is.EqualTo(native.UpperJaw.transform.localScale));
        Assert.That(effect.UpperJaw.transform.position,Is.EqualTo(native.UpperJaw.transform.position));
        Assert.That(effect.UpperJaw.transform.localScale.y,Is.GreaterThan(halfway));
        Assert.That(effect.Age,Is.EqualTo(start+.5f));
        effect.Advance(SabertoothDevourRules.HitTime,mouth);
        Assert.That(Quaternion.Angle(effect.UpperJaw.transform.rotation,effect.LowerJaw.transform.rotation),
            Is.LessThan(1f));
    }

    [Test]
    public void TheHandoffCanMoveWhileItsOpenPoseIsHeldWithoutAdvancingTheSkillEffectAge()
    {
        var effect=Create(false);effect.BeginCinematicHandoff(5f);
        effect.Advance(SabertoothDevourRules.PullStart,mouth,0f);
        float initial=effect.UpperJaw.transform.localScale.y;
        effect.Advance(SabertoothDevourRules.PullStart,mouth,.25f);
        Assert.That(effect.UpperJaw.transform.localScale.y,Is.GreaterThan(initial));
        Assert.That(effect.Age,Is.EqualTo(SabertoothDevourRules.PullStart));
        Assert.That(effect.UpperJaw.sharedMaterial.GetFloat("_Flash"),Is.Zero);
        Assert.That(Quaternion.Angle(effect.UpperJaw.transform.rotation,effect.LowerJaw.transform.rotation),
            Is.GreaterThan(20f));
        float progressing=effect.UpperJaw.transform.localScale.y;
        effect.Advance(SabertoothDevourRules.PullStart+.01f,mouth);
        Assert.That(effect.UpperJaw.transform.localScale.y,Is.GreaterThanOrEqualTo(progressing),
            "Result-window playback must continue the handoff instead of restarting its width blend.");
    }

    private PixelSabertoothDevour Create(bool left)
    {
        var effect=PixelSabertoothDevour.Create(source,left?Vector3.left:Vector3.right,center,3f,1.8f,victims);
        Assert.That(effect,Is.Not.Null);effects.Add(effect);return effect;
    }

    private static int GoldPixels(ParticleSystem.Particle[] particles,int count)
    {
        int gold=0;
        for(int i=0;i<count;i++)
            if(particles[i].startColor.a>0&&particles[i].startColor.r>particles[i].startColor.b+30)gold++;
        return gold;
    }

    private static float Spread(ParticleSystem.Particle[] particles,int count,Vector3 origin)
    {
        float radius=0f;
        for(int i=0;i<count;i++)
            if(particles[i].startColor.a>0)radius=Mathf.Max(radius,Vector3.Distance(origin,particles[i].position));
        return radius;
    }
}
