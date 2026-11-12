using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SkillAnticipationPoseTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject body,otherBody;
    private SpriteMonsterAnimationDriver driver;
    private SpriteRenderer renderer;

    [SetUp]
    public void SetUp()
    {
        body=SpawnRaptor();
        driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        renderer=driver.GetComponent<SpriteRenderer>();
        Advance(driver,0f);
    }

    [TearDown]
    public void TearDown()
    {
        if(otherBody!=null)Object.DestroyImmediate(otherBody);
        Object.DestroyImmediate(body);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PreparingCasterHoldsItsEnlargedPoseWhileAnotherMonsterKeepsAnimating(bool moving)
    {
        driver.SetMoving(moving);Advance(driver,.12f);
        Vector3 authoredScale=driver.transform.localScale;
        Vector3 bodyPosition=body.transform.position;
        driver.SetSkillFocus(1f);driver.SetSkillAnticipating(true);
        Sprite heldSprite=renderer.sprite;
        Vector3 heldPosition=driver.transform.localPosition;
        Vector3 heldScale=driver.transform.localScale;
        float heldElapsed=Field<float>(driver,"motionElapsed");
        int heldFrame=driver.CurrentFrameIndex;
        float speed=Time.timeScale;
        AssertVector(heldScale,authoredScale*1.5f);

        otherBody=SpawnRaptor();
        var other=otherBody.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        other.SetMoving(true);Advance(other,0f);
        float otherElapsed=Field<float>(other,"motionElapsed");
        int otherFrame=other.CurrentFrameIndex;
        for(int i=0;i<3;i++)
        {
            driver.SetMoving(!moving);
            Call(driver,"Update");Advance(driver,.2f);Call(driver,"OnWillRenderObject");
            Assert.That(driver.IsSkillAnticipating,Is.True);
            Assert.That(driver.CurrentMotionName,Is.EqualTo(moving?"Run":"Idle"));
            Assert.That(Field<float>(driver,"motionElapsed"),Is.EqualTo(heldElapsed));
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(heldFrame));
            Assert.That(renderer.sprite,Is.SameAs(heldSprite));
            AssertVector(driver.transform.localPosition,heldPosition);
            AssertVector(driver.transform.localScale,heldScale);
        }
        Advance(other,.2f);
        Assert.That(Field<float>(other,"motionElapsed"),Is.GreaterThan(otherElapsed));
        Assert.That(other.CurrentFrameIndex,Is.Not.EqualTo(otherFrame));
        Assert.That(other.CurrentMotionName,Is.EqualTo("Run"));
        Assert.That(other.IsSkillAnticipating,Is.False);
        Assert.That(body.transform.position,Is.EqualTo(bodyPosition));
        Assert.That(Time.timeScale,Is.EqualTo(speed));

        driver.SetSkillAnticipating(false);driver.SetMoving(!moving);Advance(driver,.2f);
        Assert.That(driver.CurrentMotionName,Is.EqualTo(moving?"Idle":"Run"));
        Assert.That(Field<float>(driver,"motionElapsed"),Is.GreaterThan(0f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReleasingAnticipationResumesThePendingHitExactlyOnceWithoutLeakingAnimationEvents(bool skill)
    {
        int hits=0;
        if(skill)driver.PlaySkillAttack(1f,()=>hits++);
        else driver.PlayBasicAttack(1f,()=>hits++);
        Advance(driver,(skill?driver.SkillAttackHitTime:driver.BasicAttackHitTime)-.01f);
        driver.SetSkillFocus(1f);driver.SetSkillAnticipating(true);
        float elapsed=Field<float>(driver,"motionElapsed");
        Sprite held=renderer.sprite;
        driver.NotifyBasicAttackHitFrameFromAnimation();
        driver.NotifySkillAttackHitFrameFromAnimation();
        Advance(driver,1f);
        Assert.That(hits,Is.Zero);
        Assert.That(skill?driver.HasPendingSkillAttackHit:driver.HasPendingBasicAttackHit,Is.True);
        Assert.That(Field<float>(driver,"motionElapsed"),Is.EqualTo(elapsed));
        Assert.That(renderer.sprite,Is.SameAs(held));

        driver.SetSkillAnticipating(false);Advance(driver,.015f);
        Assert.That(hits,Is.EqualTo(1));
        Assert.That(skill?driver.HasPendingSkillAttackHit:driver.HasPendingBasicAttackHit,Is.False);
        Assert.That(driver.CurrentMotionName,Is.EqualTo(skill?"SkillAttack":"Attack"));
        Advance(driver,.03f);
        Assert.That(hits,Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReleasingAnticipationPreservesAnOverlappingStatusFreeze(bool freezeFirst)
    {
        int hits=0;
        driver.PlaySkillAttack(1f,()=>hits++);
        Advance(driver,driver.SkillAttackHitTime-.01f);
        driver.SetSkillFocus(1f);
        if(freezeFirst)driver.SetFrozen(true);
        driver.SetSkillAnticipating(true);
        if(!freezeFirst)driver.SetFrozen(true);
        Sprite frozen=renderer.sprite;
        float elapsed=Field<float>(driver,"motionElapsed");

        driver.SetSkillAnticipating(false);driver.SetSkillFocus(0f);
        Vector3 restoredScale=driver.transform.localScale;
        Advance(driver,.5f);Call(driver,"Update");Call(driver,"OnWillRenderObject");
        Assert.That(driver.IsSkillAnticipating,Is.False);
        Assert.That(Field<bool>(driver,"statusFrozen"),Is.True);
        Assert.That(Field<float>(driver,"motionElapsed"),Is.EqualTo(elapsed));
        Assert.That(renderer.sprite,Is.SameAs(frozen));
        AssertVector(driver.transform.localScale,restoredScale);
        Assert.That(hits,Is.Zero);

        driver.SetFrozen(false);Advance(driver,.015f);
        Assert.That(hits,Is.EqualTo(1));
        Assert.That(Field<bool>(driver,"statusFrozen"),Is.False);
    }

    [TestCase("reuse")]
    [TestCase("disable")]
    [TestCase("death")]
    public void ReuseDisableAndDeathDoNotLeaveTheAnticipationHoldBehind(string transition)
    {
        Vector3 authoredScale=driver.transform.localScale;
        driver.SetSkillFocus(1f);driver.SetSkillAnticipating(true);
        Assert.That(driver.IsSkillAnticipating,Is.True);
        if(transition=="reuse")driver.ResetForReuse();
        else if(transition=="disable")Call(driver,"OnDisable");
        else driver.PlayDeath();

        Assert.That(driver.IsSkillAnticipating,Is.False);
        if(transition=="death")
        {
            Assert.That(driver.CurrentMotionName,Is.EqualTo("Death"));
            Advance(driver,.1f);
            Assert.That(Field<float>(driver,"motionElapsed"),Is.GreaterThan(0f));
        }
        else
        {
            AssertVector(driver.transform.localScale,authoredScale);
            driver.SetMoving(true);Advance(driver,.1f);
            Assert.That(driver.CurrentMotionName,Is.EqualTo("Run"));
            Assert.That(Field<float>(driver,"motionElapsed"),Is.GreaterThan(0f));
        }
    }

    private static GameObject SpawnRaptor()=>PrefabUtility.InstantiatePrefab(
        AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;

    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
    private static void Call(SpriteMonsterAnimationDriver value,string method)=>
        typeof(SpriteMonsterAnimationDriver).GetMethod(method,Private).Invoke(value,null);
    private static void Advance(SpriteMonsterAnimationDriver value,float seconds)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(value,new object[]{seconds});
        Call(value,"LateUpdate");
    }
    private static void AssertVector(Vector3 actual,Vector3 expected)=>
        Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.00001f));
}
