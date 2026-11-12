using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class KingKongSlamAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [Test]
    public void LeylineStonesSpanTheZoneAndRenderOnEitherSideOfCharacters()
    {
        var owner=new GameObject("Leyline depth owner");
        var source=owner.AddComponent<SpriteRenderer>();source.sprite=PixelStarProjectile.BeanSprite;
        source.color=Color.green;source.sortingOrder=10;owner.layer=31;
        typeof(PixelCombatEffect).GetProperty("BearSpiritParticleMaterial",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        var cachedSprite=(Sprite)typeof(PixelCombatEffect).GetField("bearSpiritParticleSprite",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(cachedSprite!=null)Object.DestroyImmediate(cachedSprite);
        var aura=PixelCombatEffect.PlayKingKongLeylineAura(owner.transform,source);
        var cameraObject=new GameObject("Leyline depth camera");var camera=cameraObject.AddComponent<Camera>();
        var target=new RenderTexture(32,32,24);var pixels=new Texture2D(32,32,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;
        try
        {
            var origins=(Vector3[])typeof(PixelKingKongLeylineAura).GetField("stoneOrigins",Private).GetValue(aura);
            float radius=MonsterAutoBattlePrototype.TileSize*FamilyTranscendenceRules.KingKongZoneRadiusTiles;
            int quadrants=0;float furthest=0f;
            foreach(var position in origins)
            {
                furthest=Mathf.Max(furthest,position.magnitude);
                Assert.That(position.magnitude,Is.LessThan(radius));
                quadrants|=1<<((position.x>=0?1:0)+(position.z>=0?2:0));
            }
            Assert.That(furthest,Is.GreaterThan(radius*.9f));Assert.That(quadrants,Is.EqualTo(15));
            Assert.That(aura.GetComponentInChildren<ParticleSystemRenderer>().enabled,Is.False);
            var stones=(SpriteRenderer[])typeof(PixelKingKongLeylineAura).GetField("stoneSprites",Private).GetValue(aura);
            foreach(var renderer in aura.GetComponentsInChildren<SpriteRenderer>())renderer.enabled=false;
            foreach(var stone in stones)Assert.That(stone.sortingOrder,Is.EqualTo(source.sortingOrder));
            var sample=stones[0];sample.enabled=true;sample.gameObject.layer=31;
            sample.color=Color.red;sample.transform.rotation=Quaternion.identity;sample.transform.localScale=Vector3.one*2f;
            camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<31;
            camera.targetTexture=target;
            foreach(float depth in new[]{1f,-1f})
            {
                sample.transform.position=new Vector3(0,0,depth);
                camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                Color center=pixels.GetPixel(16,16);
                Assert.That(depth>0?center.g:center.r,Is.GreaterThan(.9f));
                Assert.That(depth>0?center.r:center.g,Is.LessThan(.1f));
            }
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;
            Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
            if(aura!=null)Object.DestroyImmediate(aura.gameObject);Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void LeylineKeepsTheSameStonesSuspendedAcrossRefreshes()
    {
        var owner=new GameObject("Leyline hover owner");
        var aura=PixelCombatEffect.PlayKingKongLeylineAura(owner.transform,null);
        try
        {
            var stones=aura.GetComponentInChildren<ParticleSystem>();
            var initial=new ParticleSystem.Particle[12];
            Assert.That(stones.GetParticles(initial),Is.EqualTo(12));
            Assert.That(stones.emission.rateOverTime.constant,Is.Zero);
            var type=typeof(PixelKingKongLeylineAura);
            var current=new ParticleSystem.Particle[12];
            foreach(float time in new[]{1f,3f,5.8f,9f})
            {
                type.GetField("stoneElapsed",Private).SetValue(aura,time);
                aura.Refresh();
                type.GetMethod("Sync",Private).Invoke(aura,null);
                Assert.That(stones.GetParticles(current),Is.EqualTo(initial.Length));
                for(int i=0;i<current.Length;i++)
                {
                    Assert.That(current[i].randomSeed,Is.EqualTo(initial[i].randomSeed));
                    Vector3 offset=current[i].position-initial[i].position;
                    Assert.That(offset.y,Is.InRange(.45f,1.15f));
                    Assert.That(Mathf.Abs(offset.x),Is.LessThanOrEqualTo(.026f));
                    Assert.That(Mathf.Abs(offset.z),Is.LessThanOrEqualTo(.026f));
                    Assert.That(current[i].velocity,Is.EqualTo(Vector3.zero));
                }
            }
            aura.Stop();
            float height=current[0].position.y;
            stones.Simulate(.2f,true,false);
            stones.GetParticles(current);
            Assert.That(current[0].position.y,Is.LessThan(height));
        }
        finally{if(aura!=null)Object.DestroyImmediate(aura.gameObject);Object.DestroyImmediate(owner);}
    }

    [TestCase("lilas")]
    [TestCase("magril")]
    [TestCase("bingareu")]
    [TestCase("moska")]
    public void LeylineFollowsItsOwnerWithoutChangingSpriteAndDropsStonesOnExpiry(string id)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);
        var root=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>(definition.resourcePath));
        PixelKingKongLeylineAura aura=null;
        try
        {
            var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            var renderer=driver.GetComponent<SpriteRenderer>();var color=renderer.color;
            var position=renderer.transform.localPosition;
            aura=PixelCombatEffect.PlayKingKongLeylineAura(root.transform,renderer);
            var type=typeof(PixelKingKongLeylineAura);
            type.GetField("elapsed",Private).SetValue(aura,.4f);
            type.GetMethod("Sync",Private).Invoke(aura,null);
            var glow=aura.transform.Find("Leyline Heart Glow").GetComponent<SpriteRenderer>();
            Vector3 before=glow.transform.position;
            root.transform.position+=Vector3.right*3f;
            type.GetMethod("Sync",Private).Invoke(aura,null);
            Assert.That(glow.transform.position-before,Is.EqualTo(Vector3.right*3f));
            Assert.That(renderer.color,Is.EqualTo(color));
            Assert.That(renderer.transform.localPosition,Is.EqualTo(position));
            Assert.That(glow.color.a,Is.GreaterThan(.3f));
            var particles=aura.GetComponentInChildren<ParticleSystem>();
            Assert.That(particles.main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.Local));
            aura.Stop();
            Assert.That(particles.isEmitting,Is.False);
            Assert.That(particles.main.gravityModifier.constant,Is.GreaterThan(1f));
            type.GetField("ending",Private).SetValue(aura,.5f);
            type.GetMethod("LateUpdate",Private).Invoke(aura,null);
            Assert.That(aura==null,Is.True);
        }
        finally{if(aura!=null)Object.DestroyImmediate(aura.gameObject);Object.DestroyImmediate(root);}
    }

    [TestCase(false,5.95f)]
    [TestCase(true,2.04f)]
    public void LeylineWavesShowTheirActualRadiusAboveTheFloor(bool basic,float radius)
    {
        var wave=PixelCombatEffect.PlayKingKongLeylineWave(new Vector3(2f,8f,3f),radius,null,basic);
        try
        {
            var type=typeof(PixelHaetaeRoarWave);
            type.GetField("elapsed",Private).SetValue(wave,(basic?.18f:.29f)-Time.deltaTime);
            type.GetMethod("Update",Private).Invoke(wave,null);
            var renderer=wave.GetComponent<SpriteRenderer>();
            Assert.That(renderer.bounds.extents.x,Is.EqualTo(radius).Within(.001f));
            Assert.That(renderer.bounds.extents.z,Is.EqualTo(radius).Within(.001f));
            Assert.That(wave.transform.position,Is.EqualTo(new Vector3(2f,.045f,3f)));
            Assert.That(renderer.sortingOrder,Is.GreaterThanOrEqualTo(1));
        }
        finally{if(wave!=null)Object.DestroyImmediate(wave.gameObject);}
    }

    [Test]
    public void LeylineRefreshReusesAuraAndDisabledOwnerRemovesIt()
    {
        var owner=new GameObject("Leyline owner");var renderer=owner.AddComponent<SpriteRenderer>();
        renderer.sprite=PixelStarProjectile.BeanSprite;
        var aura=PixelCombatEffect.PlayKingKongLeylineAura(owner.transform,renderer);
        try
        {
            var type=typeof(PixelKingKongLeylineAura);
            type.GetField("elapsed",Private).SetValue(aura,5.5f);
            aura.Refresh();
            Assert.That((float)type.GetField("elapsed",Private).GetValue(aura),Is.Zero);
            Assert.That(aura.GetComponentsInChildren<ParticleSystem>(),Has.Length.EqualTo(1));
            owner.SetActive(false);type.GetMethod("LateUpdate",Private).Invoke(aura,null);
            Assert.That(aura==null,Is.True);
        }
        finally{if(aura!=null)Object.DestroyImmediate(aura.gameObject);Object.DestroyImmediate(owner);}
    }

    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(180f)]
    [TestCase(270f)]
    [TestCase(225f)]
    [TestCase(315f)]
    public void FissureHitsOnlyTheNewlyReachedBandInEveryDirection(float angle)
    {
        Vector3 direction=Quaternion.Euler(0f,angle,0f)*Vector3.forward;
        Assert.That(KingKongCombatRules.InsideContinentalFissure(
            direction*2.5f+Vector3.up*5f,3f,2f),Is.True);
        Assert.That(KingKongCombatRules.InsideContinentalFissure(
            direction*1.999f,3f,2f),Is.False);
        Assert.That(KingKongCombatRules.InsideContinentalFissure(
            direction*3.001f,3f,2f),Is.False);
    }

    [Test]
    public void FissureLongFrameCoversTheDiscIncludingCenterAndBack()
    {
        Assert.That(KingKongCombatRules.InsideContinentalFissure(
            new Vector3(0f,0f,-4f),5f),Is.True);
        Assert.That(KingKongCombatRules.InsideContinentalFissure(
            Vector3.zero,5f),Is.True);
        Assert.That(KingKongCombatRules.InsideContinentalFissure(
            new Vector3(3.01f,0f,4f),5f),Is.False);
    }

    [Test]
    public void FissureCrestAndDebrisExpandAroundTheSameCenter()
    {
        var start=new Vector3(2f,0f,1f);
        var wave=PixelCombatEffect.PlayKingKongContinentalFissure(
            start,.35f,null);
        try
        {
            var renderer=wave.GetComponent<SpriteRenderer>();
            Assert.That(wave.transform.localScale,Is.EqualTo(Vector3.zero));
            wave.transform.localScale=Vector3.one*(17f/renderer.sprite.bounds.size.x);
            Assert.That(renderer.bounds.size.x,Is.EqualTo(17f).Within(.001f));
            Assert.That(renderer.bounds.size.z,Is.EqualTo(17f).Within(.001f));
            Assert.That(renderer.bounds.center.x,Is.EqualTo(start.x).Within(.001f));
            Assert.That(renderer.bounds.center.z,Is.EqualTo(start.z).Within(.001f));
            Assert.That(renderer.sortingOrder,Is.GreaterThanOrEqualTo(1));
            Assert.That(wave.GetComponentInChildren<ParticleSystem>().main.simulationSpace,
                Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(wave.GetComponentInChildren<ParticleSystem>().shape.shapeType,
                Is.EqualTo(ParticleSystemShapeType.Circle));
            Assert.That(wave.GetComponentInChildren<ParticleSystem>().main.scalingMode,
                Is.EqualTo(ParticleSystemScalingMode.Shape));
        }
        finally { Object.DestroyImmediate(wave); }
    }

    private static void Step(SpriteMonsterAnimationDriver driver,float dt)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }

    [TestCase("lilas",1f)]
    [TestCase("magril",1f)]
    [TestCase("bingareu",2f)]
    [TestCase("moska",0.5f)]
    public void WorldOverturnShakesOnlyTheSpriteThenHitsOnceAtContact(string id,float speed)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);
        var root=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>(definition.resourcePath));
        ParticleSystem charge=null;
        try
        {
            var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            var start=driver.transform.localPosition;var body=root.transform.position;
            charge=PixelCombatEffect.PlayKingKongWorldOverturnCharge(body,5.1f,driver.GetComponent<SpriteRenderer>());
            int hits=0,contact=-1,swings=0;
            driver.PlayWorldOverturnAttack(speed,()=>{hits++;contact=driver.CurrentFrameIndex;},charge,_=>swings++);
            Step(driver,.31f/speed);
            Assert.That(driver.transform.localPosition,Is.Not.EqualTo(start));
            var first=driver.transform.localPosition;
            Step(driver,.03f/speed);
            Assert.That(swings,Is.Zero);
            Assert.That(driver.transform.localPosition,Is.Not.EqualTo(first));
            Assert.That(root.transform.position,Is.EqualTo(body));
            Assert.That(driver.IsActionLocked,Is.True);
            charge.Simulate(.3f,true,true,true);
            var particles=new ParticleSystem.Particle[48];int count=charge.GetParticles(particles);
            Assert.That(count,Is.GreaterThan(0));
            bool lifted=false;for(int i=0;i<count;i++)if(particles[i].position.y>body.y+.1f)lifted=true;
            Assert.That(lifted,Is.True,"Ground pieces must visibly rise before contact.");
            Step(driver,.15f/speed);driver.NotifySkillAttackHitFrameFromAnimation();
            Assert.That(swings,Is.EqualTo(1));
            Assert.That(hits,Is.Zero,"An old hit event cannot bypass the charged windup.");
            Step(driver,1f/speed);
            Assert.That(hits,Is.EqualTo(1));Assert.That(contact,Is.EqualTo(3));
            Assert.That(charge==null,Is.True,"Floating pieces end on contact.");
            Assert.That(driver.transform.localPosition,Is.EqualTo(start));
            Step(driver,.08f/speed);
            Assert.That(((Vector3)typeof(SpriteMonsterAnimationDriver).GetField("skillPoseScale",Private).GetValue(driver)).y,Is.EqualTo(.64f).Within(.001f));
            Step(driver,2f/speed);
            Assert.That(swings,Is.EqualTo(1));
            Assert.That(hits,Is.EqualTo(1));Assert.That(driver.IsActionLocked,Is.False);
            Assert.That(typeof(SpriteMonsterAnimationDriver).GetField("skillPoseScale",Private).GetValue(driver),Is.EqualTo(Vector3.one));
        }
        finally { if(charge!=null)Object.DestroyImmediate(charge.gameObject);Object.DestroyImmediate(root); }
    }

    [TestCase("interrupt")]
    [TestCase("death")]
    [TestCase("disable")]
    public void WorldOverturnCancellationRemovesFloatingPiecesAndShake(string stop)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Enemies/PixelMonsters/017_lilas"));
        ParticleSystem charge=null;
        try
        {
            var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            var start=driver.transform.localPosition;int hits=0;
            charge=PixelCombatEffect.PlayKingKongWorldOverturnCharge(root.transform.position,5.1f,null);
            driver.PlayWorldOverturnAttack(1f,()=>hits++,charge);Step(driver,.3f);
            if(stop=="interrupt")driver.InterruptCurrentAction();
            else if(stop=="death")driver.PlayDeath();
            else
            {
                driver.enabled=false;
                // EditMode does not dispatch lifecycle events for this non-ExecuteAlways component.
                typeof(SpriteMonsterAnimationDriver).GetMethod("OnDisable",Private).Invoke(driver,null);
            }
            Assert.That(charge==null,Is.True);
            Assert.That(driver.transform.localPosition,Is.EqualTo(start));
            Assert.That(hits,Is.Zero);
            if(stop=="disable")
            {
                driver.enabled=true;
                driver.NotifySkillAttackHitFrameFromAnimation();
                Assert.That(hits,Is.Zero,"Re-enabling must not dispatch the cancelled slam.");
            }
            if(stop=="interrupt")
            {
                driver.PlayEarthPulseAttack(1f,()=>hits++);Step(driver,.28f);
                Assert.That(hits,Is.EqualTo(1),"The next basic slam must regain its normal timing.");
            }
        }
        finally { if(charge!=null)Object.DestroyImmediate(charge.gameObject);Object.DestroyImmediate(root); }
    }

    [TestCase("lilas")]
    [TestCase("magril")]
    [TestCase("bingareu")]
    [TestCase("moska")]
    public void RaisedArmsLeadToOneContactFrameAndRestorePose(string id)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);
        var root=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>(definition.resourcePath));
        try
        {
            var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            int hits=0,contactFrame=-1,swings=0;
            driver.PlayEarthPulseAttack(1f,()=>{hits++;contactFrame=driver.CurrentFrameIndex;},
                downstrokeCallback:_=>swings++);
            Step(driver,.19f);
            Assert.That(swings,Is.Zero);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(2));
            Assert.That(((Vector3)typeof(SpriteMonsterAnimationDriver).GetField("skillPoseScale",Private).GetValue(driver)).y,Is.GreaterThan(1f));
            driver.NotifySkillAttackHitFrameFromAnimation();
            Assert.That(hits,Is.Zero,"An old animation event cannot hit before the slam.");
            Step(driver,.5f);
            Assert.That(swings,Is.EqualTo(1));
            Assert.That(hits,Is.EqualTo(1));
            Assert.That(contactFrame,Is.EqualTo(3),"Even a long frame must show the planted fists.");
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
            Step(driver,.03f);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
            Step(driver,.3f);
            Assert.That(hits,Is.EqualTo(1));
            Assert.That(driver.IsActionLocked,Is.False);
            Assert.That(typeof(SpriteMonsterAnimationDriver).GetField("skillPoseScale",Private).GetValue(driver),Is.EqualTo(Vector3.one));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void InterruptedWindupCancelsDamageAndCanBeReplayed()
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Enemies/PixelMonsters/017_lilas"));
        try
        {
            var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            int hits=0;driver.PlayEarthPulseAttack(1f,()=>hits++);Step(driver,.19f);
            driver.InterruptCurrentAction();Step(driver,1f);
            Assert.That(hits,Is.Zero);
            Assert.That(typeof(SpriteMonsterAnimationDriver).GetField("skillPoseScale",Private).GetValue(driver),Is.EqualTo(Vector3.one));
            driver.PlayEarthPulseAttack(1f,()=>hits++);Step(driver,1f);
            Assert.That(hits,Is.EqualTo(1));
            Step(driver,1f);Assert.That(driver.IsActionLocked,Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void LiftPopsUpEarlyAndReturnsToTheSameGroundHeight()
    {
        Assert.That(KingKongCombatRules.PulseLiftHeight(0f),Is.Zero);
        Assert.That(KingKongCombatRules.PulseLiftHeight(.28f),Is.EqualTo(.75f).Within(.001f));
        Assert.That(KingKongCombatRules.PulseLiftHeight(.14f),Is.GreaterThan(.5f));
        Assert.That(KingKongCombatRules.PulseLiftHeight(1f),Is.Zero.Within(.001f));
        Assert.That(KingKongCombatRules.PulseLiftHeight(2f),Is.Zero.Within(.001f));
    }

    [TestCase("lilas",1f)]
    [TestCase("magril",1f)]
    [TestCase("bingareu",2f)]
    [TestCase("moska",0.5f)]
    public void TripleSlamKeepsTheActionLockedAndHitsAtThreePlantedFrames(string id,float speed)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);
        var root=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>(definition.resourcePath));
        try
        {
            var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            int hits=0,swings=0;float elapsed=0f,previousHit=0f;
            var pose=typeof(SpriteMonsterAnimationDriver).GetField("skillPoseScale",Private);
            System.Action<int> hit=strike=>
            {
                Assert.That(strike,Is.EqualTo(hits));hits++;
                Assert.That(swings,Is.EqualTo(hits),"Each impact follows exactly one downstroke.");
                Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
                Assert.That(((Vector3)pose.GetValue(driver)).y,Is.EqualTo(strike==2?.68f:.78f).Within(.001f));
                if(strike>0)Assert.That((elapsed-previousHit)*speed,
                    Is.EqualTo(strike==1?KingKongCombatRules.TriplePulseFollowupDelay:KingKongCombatRules.TriplePulseFinalDelay).Within(.03f));
                previousHit=elapsed;
            };
            driver.PlayEarthPulseAttack(speed,()=>hit(0),hit,strike=>
            {
                Assert.That(strike,Is.EqualTo(swings));swings++;
                float t=(float)typeof(SpriteMonsterAnimationDriver).GetField("earthPulseElapsed",Private).GetValue(driver);
                Assert.That(t,Is.EqualTo(strike==2?.30f:strike==1?.16f:.20f).Within(.005f));
            });
            int attemptedOtherHits=0;
            for(int i=0;i<2000&&driver.IsActionLocked;i++)
            {
                driver.PlayBasicAttack(1f,()=>attemptedOtherHits++);
                driver.NotifySkillAttackHitFrameFromAnimation();
                elapsed+=.002f;Step(driver,.002f);
            }
            Assert.That(hits,Is.EqualTo(3));
            Assert.That(attemptedOtherHits,Is.Zero);
            Assert.That(driver.IsActionLocked,Is.False);
            Assert.That(pose.GetValue(driver),Is.EqualTo(Vector3.one));
            Assert.That(elapsed*speed,Is.InRange(1.5f,1.57f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(1)]
    [TestCase(2)]
    public void InterruptedTripleCancelsRemainingHitsAndNextCastIsSingle(int interruptAfter)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Enemies/PixelMonsters/017_lilas"));
        try
        {
            var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            int hits=0,swings=0;driver.PlayEarthPulseAttack(1f,()=>hits++,_=>hits++,_=>swings++);
            for(int i=0;i<100&&hits<interruptAfter;i++)Step(driver,.02f);
            driver.InterruptCurrentAction();Step(driver,10f);
            Assert.That(hits,Is.EqualTo(interruptAfter));
            Assert.That(driver.IsActionLocked,Is.False);
            driver.PlayEarthPulseAttack(1f,()=>hits++);
            Step(driver,10f);Step(driver,10f);Step(driver,10f);
            Assert.That(hits,Is.EqualTo(interruptAfter+1));
            Assert.That(swings,Is.EqualTo(interruptAfter),"Cancelled callbacks cannot leak into the next cast.");
            Assert.That(driver.IsActionLocked,Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SlowFramesStillRenderEveryTripleContactOnce()
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Enemies/PixelMonsters/017_lilas"));
        try
        {
            var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            int hits=0,swings=0;
            System.Action hit=()=>{Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));hits++;};
            driver.PlayEarthPulseAttack(1f,hit,_=>hit(),_=>swings++);
            for(int i=0;i<3;i++)
            {
                Step(driver,10f);Assert.That(hits,Is.EqualTo(i+1));
                Assert.That(swings,Is.EqualTo(i+1));
                Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
                Step(driver,10f);
            }
            Assert.That(driver.IsActionLocked,Is.False);
            Assert.That(hits,Is.EqualTo(3));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
