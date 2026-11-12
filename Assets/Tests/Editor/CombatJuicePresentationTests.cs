using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatJuicePresentationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [TestCase(false)] [TestCase(true)]
    public void EachRelentlessStrikeThrustsTowardTheVictimAndRecoilsOnAHeldPose(bool reverse)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            var renderer=driver.GetComponent<SpriteRenderer>();renderer.flipX=reverse;
            driver.PlaySkillAttack(1f,null);driver.HoldSkillAttackHitFrame(3f);Advance(driver,0f);
            Sprite held=renderer.sprite;Vector3 body=instance.transform.position,scale=driver.transform.localScale;
            Quaternion rotation=driver.transform.rotation;
            Vector3 direction=(driver.transform.right*(reverse?-1f:1f)+driver.transform.up*.3f).normalized;
            float firstPeak=0f;
            for(int strike=0;strike<3;strike++)
            {
                driver.PlayRaptorRelentlessStrike(direction,strike);
                Advance(driver,0f,sameFrame:true);
                float first=Vector3.Dot(WorldRecoil(driver),direction);
                Assert.That(first,Is.EqualTo((strike==2?.3f:.18f)*.45f).Within(.0001f));
                Advance(driver,.025f);
                float peak=Vector3.Dot(WorldRecoil(driver),direction);
                Assert.That(peak,Is.GreaterThan(first));
                if(strike==0)firstPeak=peak;
                if(strike==2)Assert.That(peak,Is.GreaterThan(firstPeak*1.3f));
                Advance(driver,.08f);
                Assert.That(Vector3.Dot(WorldRecoil(driver),direction),Is.LessThan(0f));
                Assert.That(renderer.sprite,Is.SameAs(held));
                Assert.That(driver.CurrentMotionName,Is.EqualTo("SkillAttack"));
                Assert.That(driver.transform.localScale,Is.EqualTo(scale));
                Assert.That(driver.transform.rotation,Is.EqualTo(rotation));
                Assert.That(instance.transform.position,Is.EqualTo(body));
                Advance(driver,.2f);
                Assert.That(WorldRecoil(driver),Is.EqualTo(Vector3.zero));
            }
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void ANewRelentlessStrikeRestartsImmediatelyWithoutAdvancingOrRepeatingSkillDamage()
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            int hits=0;driver.PlaySkillAttack(1f,()=>hits++);
            Advance(driver,driver.SkillAttackHitTime-.01f);
            Vector3 body=instance.transform.position,right=driver.transform.right;
            float before=Field<float>(driver,"motionElapsed");
            driver.PlayRaptorRelentlessStrike(right,0);
            driver.PlayRaptorRelentlessStrike(-right,1);
            Assert.That(Field<float>(driver,"motionElapsed"),Is.EqualTo(before));
            Assert.That(hits,Is.Zero);
            Advance(driver,0f,sameFrame:true);
            Assert.That(Vector3.Dot(WorldRecoil(driver),right),Is.LessThan(-.07f));
            Advance(driver,.015f);
            Assert.That(hits,Is.EqualTo(1));
            driver.PlayRaptorRelentlessStrike(right,2);Advance(driver,0f,sameFrame:true);
            Assert.That(Vector3.Dot(WorldRecoil(driver),right),Is.GreaterThan(.13f));
            Advance(driver,1f);
            Assert.That(hits,Is.EqualTo(1));
            Assert.That(instance.transform.position,Is.EqualTo(body));
            Assert.That(Field<float>(driver,"raptorStrikeRemaining"),Is.Zero);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase("interrupt")] [TestCase("freeze")] [TestCase("death")]
    [TestCase("disable")] [TestCase("reuse")]
    public void RelentlessVisualThrustCannotLeakAcrossActionCancellationOrReuse(string transition)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            driver.PlaySkillAttack(1f,null);driver.HoldSkillAttackHitFrame(3f);Advance(driver,0f);
            Vector3 body=instance.transform.position;
            driver.PlayRaptorRelentlessStrike(driver.transform.right,2);Advance(driver,0f,sameFrame:true);
            Assert.That(WorldRecoil(driver).magnitude,Is.GreaterThan(.1f));
            if(transition=="interrupt")driver.InterruptCurrentAction(true);
            else if(transition=="freeze")driver.SetFrozen(true);
            else if(transition=="death")driver.PlayDeath();
            else if(transition=="disable")
                typeof(SpriteMonsterAnimationDriver).GetMethod("OnDisable",Private).Invoke(driver,null);
            else driver.ResetForReuse();
            Assert.That(Field<float>(driver,"raptorStrikeRemaining"),Is.Zero);
            Assert.That(WorldRecoil(driver),Is.EqualTo(Vector3.zero));
            Assert.That(instance.transform.position,Is.EqualTo(body));
            if(transition=="freeze")
            {
                Vector3 frozen=driver.transform.position;Advance(driver,1f);
                Assert.That(driver.transform.position,Is.EqualTo(frozen));
                driver.SetFrozen(false);Advance(driver,.2f);
                Assert.That(WorldRecoil(driver),Is.EqualTo(Vector3.zero));
            }
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ContactShakeStartsImmediatelyReboundsAndSettlesWithoutMovingTheBody(bool strong)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            Advance(driver,0f);
            Vector3 bodyPosition=instance.transform.position;
            Vector3 spritePosition=driver.transform.position;
            Vector3 spriteScale=driver.transform.localScale;
            Quaternion spriteRotation=driver.transform.localRotation;
            Vector3 right=driver.transform.right;

            driver.PlayContactReaction(right,strong);
            Advance(driver,.08f,sameFrame:true);
            float initial=Vector3.Dot(driver.transform.position-spritePosition,right);
            Assert.That(initial,Is.GreaterThan(.05f),"The first drawn impact frame must already react.");

            Advance(driver,.04f);
            float rebound=Vector3.Dot(driver.transform.position-spritePosition,right);
            Assert.That(rebound,Is.LessThan(0f));
            Assert.That(Mathf.Abs(rebound),Is.LessThan(initial));
            Assert.That(instance.transform.position,Is.EqualTo(bodyPosition));
            Assert.That(driver.transform.localRotation,Is.EqualTo(spriteRotation));
            Assert.That(driver.transform.localScale.x,Is.GreaterThan(spriteScale.x));
            Assert.That(driver.transform.localScale.y,Is.LessThan(spriteScale.y));

            float remaining=Field<float>(driver,"contactShakeRemaining");
            driver.PlayBasicAttack(1f,null);
            Assert.That(Field<float>(driver,"contactShakeRemaining"),Is.EqualTo(remaining),
                "Starting the victim's own attack must not cancel its hit presentation.");

            Advance(driver,.13f);
            Assert.That(Vector3.Distance(driver.transform.position,spritePosition),Is.LessThan(.00001f));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(false)] [TestCase(true)]
    public void ContactVibrationAlternatesQuicklyAndFullySettlesWithoutAccumulating(bool strong)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            Advance(driver,0f);
            Vector3 body=instance.transform.position,origin=driver.transform.position;
            Vector3 right=driver.transform.right;
            driver.PlayContactReaction(right,strong);
            Advance(driver,0f,sameFrame:true);
            Assert.That(Vector3.Dot(driver.transform.position-origin,right),
                Is.EqualTo(strong?.14f:.1f).Within(.0001f));
            Advance(driver,.02f);
            Assert.That(Vector3.Dot(driver.transform.position-origin,right),Is.LessThan(-.015f));
            Advance(driver,.04f);
            Assert.That(Vector3.Dot(driver.transform.position-origin,right),Is.GreaterThan(.015f));
            Advance(driver,.02f);
            Assert.That(Vector3.Dot(driver.transform.position-origin,right),Is.LessThan(0f));
            Advance(driver,.05f);
            Assert.That(Vector3.Distance(driver.transform.position,origin),Is.LessThan(.00001f));
            Assert.That(instance.transform.position,Is.EqualTo(body));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void ContactShakePreservesALargerSkillRecoil()
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            Advance(driver,0f);
            Vector3 spritePosition=driver.transform.position;
            Vector3 spriteScale=driver.transform.localScale;
            Vector3 right=driver.transform.right;
            driver.PlayImpactRecoil(-right,.15f);
            typeof(SpriteMonsterAnimationDriver).GetField("impactRecoilRemaining",Private)
                .SetValue(driver,.112f);

            driver.PlayContactReaction(right,true);
            Advance(driver,0f,sameFrame:true);

            Assert.That(Vector3.Dot(driver.transform.position-spritePosition,right),
                Is.EqualTo(-.15f).Within(.0001f));
            Assert.That(driver.transform.localScale,Is.EqualTo(spriteScale));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void ContactRecoilFollowsTheImpactInTheSpritePlaneWithoutTurningOrMovingTheBody()
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            var renderer=driver.GetComponent<SpriteRenderer>();
            Advance(driver,0f);
            renderer.flipX=true;
            Vector3 bodyPosition=instance.transform.position;
            Quaternion bodyRotation=instance.transform.rotation;
            Quaternion spriteRotation=driver.transform.rotation;
            Vector3 spritePosition=driver.transform.position;
            Vector3 direction=(driver.transform.right+driver.transform.up*.6f+driver.transform.forward*.3f).normalized;
            Vector3 projected=Vector3.ProjectOnPlane(direction,driver.transform.forward).normalized;

            driver.PlayContactReaction(direction,true);
            Advance(driver,0f,sameFrame:true);

            Vector3 recoil=driver.transform.position-spritePosition;
            Assert.That(Vector3.Dot(recoil,projected),Is.EqualTo(.14f).Within(.0001f));
            Assert.That(Vector3.Cross(recoil,projected).magnitude,Is.LessThan(.0001f));
            Assert.That(instance.transform.position,Is.EqualTo(bodyPosition));
            Assert.That(instance.transform.rotation,Is.EqualTo(bodyRotation));
            Assert.That(driver.transform.rotation,Is.EqualTo(spriteRotation));
            Assert.That(renderer.flipX,Is.True);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(false,false)]
    [TestCase(false,true)]
    [TestCase(true,false)]
    [TestCase(true,true)]
    public void IdleAndRunCompressionIsSmallDoesNotAccumulateAndFullyRestores(bool moving,bool strong)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            driver.SetMoving(moving);
            Advance(driver,0f);
            Vector3 baseScale=driver.transform.localScale;
            Vector3 bodyPosition=instance.transform.position;
            Vector3 pivotPosition=driver.transform.position;
            driver.PlayContactReaction(driver.transform.right,strong);
            Advance(driver,0f,sameFrame:true);
            Vector3 firstScale=driver.transform.localScale;
            float compression=strong?.04f:.025f;
            Assert.That(firstScale.x/baseScale.x,Is.EqualTo(1f+compression).Within(.0001f));
            Assert.That(firstScale.y/baseScale.y,Is.EqualTo(1f-compression).Within(.0001f));
            for(int i=0;i<4;i++)Advance(driver,0f,sameFrame:true);
            Assert.That(driver.transform.localScale,Is.EqualTo(firstScale));
            Assert.That(instance.transform.position,Is.EqualTo(bodyPosition));
            Assert.That(driver.CurrentMotionName,Is.EqualTo(moving?"Run":"Idle"));

            Advance(driver,.13f);
            Assert.That(driver.transform.localScale,Is.EqualTo(baseScale));
            Assert.That(Vector3.Distance(driver.transform.position,pivotPosition),Is.LessThan(.00001f));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ContactReactionPreservesAuthoredAttackScaleAndNormalHitTiming(bool skill)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            int hits=0;
            if(skill)driver.PlaySkillAttack(1f,()=>hits++);
            else driver.PlayBasicAttack(1f,()=>hits++);
            driver.SetSkillPoseScale(new Vector3(.93f,1.06f,1f));
            Advance(driver,(skill?driver.SkillAttackHitTime:driver.BasicAttackHitTime)-.01f);
            Vector3 authoredScale=driver.transform.localScale;
            Vector3 bodyPosition=instance.transform.position;
            float timeScale=Time.timeScale;

            driver.PlayContactReaction(driver.transform.right,true);
            Advance(driver,.015f);

            Assert.That(hits,Is.EqualTo(1));
            Assert.That(driver.transform.localScale,Is.EqualTo(authoredScale));
            Assert.That(driver.CurrentMotionName,Is.EqualTo(skill?"SkillAttack":"Attack"));
            Assert.That(instance.transform.position,Is.EqualTo(bodyPosition));
            Assert.That(Time.timeScale,Is.EqualTo(timeScale));
            Advance(driver,.06f);
            Assert.That(hits,Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void ConcentratedHitsStayBoundedAndSettleAfterTheLastContact()
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            Advance(driver,0f);
            Vector3 bodyPosition=instance.transform.position;
            Vector3 spritePosition=driver.transform.position;
            Vector3 baseScale=driver.transform.localScale;
            for(int i=0;i<60;i++)
            {
                Advance(driver,.01f);
                driver.PlayContactReaction(driver.transform.right*(i%2==0?1f:-1f),true);
                Advance(driver,0f,sameFrame:true);
                Assert.That(Vector3.Distance(driver.transform.position,spritePosition),Is.LessThanOrEqualTo(.14001f));
                Assert.That(driver.transform.localScale.x/baseScale.x,Is.InRange(1f,1.04001f));
                Assert.That(driver.transform.localScale.y/baseScale.y,Is.InRange(.95999f,1f));
                Assert.That(instance.transform.position,Is.EqualTo(bodyPosition));
            }
            Advance(driver,.13f);
            Assert.That(driver.transform.localScale,Is.EqualTo(baseScale));
            Assert.That(Vector3.Distance(driver.transform.position,spritePosition),Is.LessThan(.00001f));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase("freeze")]
    [TestCase("terminal")]
    [TestCase("disable")]
    [TestCase("reuse")]
    public void ContactCompressionCannotRemainOnFrozenTerminalOrReusedSprites(string transition)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            Advance(driver,0f);
            Vector3 baseScale=driver.transform.localScale;
            driver.PlayContactReaction(driver.transform.right,true);
            Advance(driver,0f,sameFrame:true);
            Assert.That(driver.transform.localScale,Is.Not.EqualTo(baseScale));

            if(transition=="freeze")driver.SetFrozen(true);
            else if(transition=="terminal")driver.FreezeCurrentFrame();
            else if(transition=="disable")
                typeof(SpriteMonsterAnimationDriver).GetMethod("OnDisable",Private).Invoke(driver,null);
            else driver.ResetForReuse();

            Assert.That(driver.transform.localScale,Is.EqualTo(baseScale));
            Assert.That(Field<float>(driver,"contactShakeRemaining"),Is.Zero);
            Assert.That(Field<bool>(driver,"contactScaleApplied"),Is.False);
            if(transition=="freeze")
            {
                Vector3 frozenPosition=driver.transform.position;
                Advance(driver,1f);
                Assert.That(driver.transform.localScale,Is.EqualTo(baseScale));
                Assert.That(driver.transform.position,Is.EqualTo(frozenPosition));
            }
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void ImpactFrameHoldDoesNotDelayAttackCallbackOrMoveTheCombatBody()
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            var renderer=driver.GetComponent<SpriteRenderer>();
            int hits=0;
            driver.PlayBasicAttack(1f,()=>hits++);
            Advance(driver,driver.BasicAttackHitTime-.01f);
            Sprite held=renderer.sprite;
            Vector3 bodyPosition=instance.transform.position;
            float timeScale=Time.timeScale;

            driver.PlayContactReaction(Vector3.right,true);
            Advance(driver,.015f);

            Assert.That(hits,Is.EqualTo(1),"The damage callback must still occur on its normal combat frame.");
            Assert.That(renderer.sprite,Is.SameAs(held),"Only the drawn frame is held.");
            Assert.That(driver.HasPendingBasicAttackHit,Is.False);
            Assert.That(instance.transform.position,Is.EqualTo(bodyPosition));
            Assert.That(Time.timeScale,Is.EqualTo(timeScale));

            Advance(driver,.04f);
            Assert.That(renderer.sprite,Is.Not.SameAs(held));
            Assert.That(hits,Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void SlowCreationFrameStillDrawsTheHeldSpriteWithoutDelayingDamage()
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            var renderer=driver.GetComponent<SpriteRenderer>();
            int hits=0;
            driver.PlayBasicAttack(1f,()=>hits++);
            Advance(driver,driver.BasicAttackHitTime-.01f);
            Sprite held=renderer.sprite;

            driver.PlayContactReaction(Vector3.right,true);
            Advance(driver,.08f,sameFrame:true);

            Assert.That(hits,Is.EqualTo(1));
            Assert.That(renderer.sprite,Is.SameAs(held));
            Assert.That(Field<float>(driver,"impactHoldRemaining"),Is.EqualTo(.04f));

            Advance(driver,.08f);
            Assert.That(Field<float>(driver,"impactHoldRemaining"),Is.Zero);
            Assert.That(renderer.sprite,Is.Not.SameAs(held));
            Assert.That(hits,Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void RapidHitsDoNotExtendTheVisualHoldAndReuseClearsIt()
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            driver.PlayContactReaction(Vector3.right,true);
            Advance(driver,.03f);
            float remaining=Field<float>(driver,"contactShakeRemaining");
            driver.PlayContactReaction(Vector3.left,true);
            Assert.That(Field<float>(driver,"contactShakeRemaining"),Is.EqualTo(remaining));
            Advance(driver,.02f);
            Assert.That(Field<float>(driver,"impactHoldRemaining"),Is.Zero);

            Advance(driver,.1f);
            driver.PlayContactReaction(Vector3.right,true);
            Assert.That(Field<float>(driver,"impactHoldRemaining"),Is.GreaterThan(0f));
            driver.ResetForReuse();
            Assert.That(Field<float>(driver,"impactHoldRemaining"),Is.Zero);
            Assert.That(Field<float>(driver,"impactReactionCooldown"),Is.Zero);
            Assert.That(Field<float>(driver,"contactShakeRemaining"),Is.Zero);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void FrozenAndTerminalSpritesIgnoreContactRecoil()
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            driver.SetFrozen(true);
            driver.PlayContactReaction(Vector3.right,true);
            Assert.That(Field<float>(driver,"impactHoldRemaining"),Is.Zero);
            Assert.That(Field<float>(driver,"impactRecoilRemaining"),Is.Zero);
            Assert.That(Field<float>(driver,"contactShakeRemaining"),Is.Zero);
            driver.SetFrozen(false);
            driver.FreezeCurrentFrame();
            driver.PlayContactReaction(Vector3.right,true);
            Assert.That(Field<float>(driver,"impactHoldRemaining"),Is.Zero);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void DamageTrailHoldsBrieflyThenDrainsAndNeverHidesHealing()
    {
        Assert.That(MonsterAutoBattlePrototype.DamageTrailRatio(.5f,1f,.1f),Is.EqualTo(1f));
        Assert.That(MonsterAutoBattlePrototype.DamageTrailRatio(.5f,1f,.22f),Is.EqualTo(.75f).Within(.001f));
        Assert.That(MonsterAutoBattlePrototype.DamageTrailRatio(.5f,1f,.5f),Is.EqualTo(.5f));
        Assert.That(MonsterAutoBattlePrototype.DamageTrailRatio(.9f,.5f,.05f),Is.EqualTo(.9f));
        Assert.That(MonsterAutoBattlePrototype.DamageTrailRatio(1.4f,2f,0f),Is.EqualTo(1f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DamageNumbersPopEvenOnASlowFrameThenRemainReadableBeforeFading(bool critical)
    {
        var first=SampleText(0f,critical);
        var slowFrame=SampleText(.067f,critical);
        var apex=SampleText(.23f,critical);
        var fading=SampleText(.5f,critical);
        var end=SampleText(.72f,critical);
        Assert.That(first.size,Is.GreaterThan(1f));
        Assert.That(slowFrame.size,Is.GreaterThan(first.size));
        Assert.That(slowFrame.rise,Is.GreaterThan(15f));
        Assert.That(apex.size,Is.EqualTo(1f).Within(.001f));
        Assert.That(apex.alpha,Is.EqualTo(1f));
        Assert.That(fading.rise,Is.LessThan(apex.rise));
        Assert.That(fading.rise,Is.GreaterThan(15f));
        Assert.That(fading.alpha,Is.InRange(.1f,.9f));
        Assert.That(end.alpha,Is.EqualTo(0f).Within(.001f));
        if(critical)
            Assert.That(slowFrame.size,Is.GreaterThan(SampleText(.067f,false).size));
    }

    private static (float size,float rise,float alpha) SampleText(float elapsed,bool critical)
    {
        object[] args={elapsed,critical,0f,0f,0f};
        typeof(MonsterAutoBattlePrototype).GetMethod("SampleDamageText",
            BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
        return ((float)args[2],(float)args[3],(float)args[4]);
    }

    private static GameObject SpawnRaptor()=>PrefabUtility.InstantiatePrefab(
        AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;

    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);

    private static Vector3 WorldRecoil(SpriteMonsterAnimationDriver driver)
    {
        Vector3 offset=Field<Vector3>(driver,"appliedRecoilOffset");
        return driver.transform.parent==null?offset:driver.transform.parent.TransformVector(offset);
    }

    private static void Advance(SpriteMonsterAnimationDriver driver,float seconds,bool sameFrame=false)
    {
        if(!sameFrame)
        {
            typeof(SpriteMonsterAnimationDriver).GetField("impactReactionStartedFrame",Private)
                .SetValue(driver,Time.frameCount-1);
            typeof(SpriteMonsterAnimationDriver).GetField("raptorStrikeStartedFrame",Private)
                .SetValue(driver,Time.frameCount-1);
        }
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{seconds});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }
}
