using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SkillFocusPresentationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [TestCase(false)]
    [TestCase(true)]
    public void FocusEnlargesOnlyTheDrawnMonsterAndPreservesItsFacing(bool moving)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            instance.transform.position=new Vector3(3f,0f,-2f);
            instance.transform.localScale=new Vector3(1.2f,.8f,1.1f);
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            var renderer=driver.GetComponent<SpriteRenderer>();
            renderer.flipX=true;
            driver.SetMoving(moving);
            Advance(driver,0f);
            Vector3 bodyPosition=instance.transform.position;
            Vector3 bodyScale=instance.transform.localScale;
            Quaternion bodyRotation=instance.transform.rotation;
            Vector3 spritePosition=driver.transform.position;
            Quaternion spriteRotation=driver.transform.localRotation;
            Vector3 authoredScale=driver.transform.localScale;

            driver.SetSkillFocus(1f);
            Advance(driver,0f);

            AssertVector(driver.transform.localScale,authoredScale*1.5f);
            Assert.That(instance.transform.position,Is.EqualTo(bodyPosition));
            Assert.That(instance.transform.localScale,Is.EqualTo(bodyScale));
            Assert.That(instance.transform.rotation,Is.EqualTo(bodyRotation));
            Assert.That(driver.transform.position,Is.EqualTo(spritePosition));
            Assert.That(driver.transform.localRotation,Is.EqualTo(spriteRotation));
            Assert.That(renderer.flipX,Is.True);
            Assert.That(driver.CurrentMotionName,Is.EqualTo(moving?"Run":"Idle"));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(-1f,1f)]
    [TestCase(.5f,1.25f)]
    [TestCase(2f,1.5f)]
    public void FocusIsBoundedDoesNotAccumulateAndRestoresTheAuthoredSkillPose(float amount,float multiplier)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            driver.PlaySkillAttack(1f,null);
            driver.SetSkillPoseScale(new Vector3(.93f,1.06f,1f));
            Advance(driver,0f);
            Vector3 authoredScale=driver.transform.localScale;
            Sprite authoredFrame=driver.GetComponent<SpriteRenderer>().sprite;

            for(int i=0;i<8;i++)
            {
                driver.SetSkillFocus(amount);
                Advance(driver,0f);
                AssertVector(driver.transform.localScale,authoredScale*multiplier);
                Assert.That(driver.CurrentMotionName,Is.EqualTo("SkillAttack"));
                Assert.That(driver.GetComponent<SpriteRenderer>().sprite,Is.SameAs(authoredFrame));
            }

            driver.SetSkillFocus(0f);
            Advance(driver,0f);
            AssertVector(driver.transform.localScale,authoredScale);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FocusChangesNeverDispatchAnExtraAttackHitOrInterruptTheAction(bool skill)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            int hits=0;
            if(skill)driver.PlaySkillAttack(1f,()=>hits++);
            else driver.PlayBasicAttack(1f,()=>hits++);
            float hitTime=skill?driver.SkillAttackHitTime:driver.BasicAttackHitTime;
            Advance(driver,hitTime-.01f);
            driver.SetSkillFocus(1f);
            Advance(driver,0f);
            Assert.That(hits,Is.Zero);

            Advance(driver,.015f);
            Assert.That(hits,Is.EqualTo(1));
            Assert.That(driver.CurrentMotionName,Is.EqualTo(skill?"SkillAttack":"Attack"));
            for(int i=0;i<4;i++)
            {
                driver.SetSkillFocus(i%2);
                Advance(driver,.01f);
            }
            Assert.That(hits,Is.EqualTo(1));
            Assert.That(skill?driver.HasPendingSkillAttackHit:driver.HasPendingBasicAttackHit,Is.False);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EndingFocusOnAStatusOrTerminalFrozenFrameDoesNotLeaveAnEnlargedSprite(bool terminal)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            Advance(driver,0f);
            Vector3 authoredScale=driver.transform.localScale;
            Vector3 spritePosition=driver.transform.localPosition;
            driver.SetSkillFocus(1f);
            Advance(driver,0f);
            AssertVector(driver.transform.localScale,authoredScale*1.5f);

            if(terminal)driver.FreezeCurrentFrame();
            else driver.SetFrozen(true);
            Sprite frozenSprite=driver.GetComponent<SpriteRenderer>().sprite;
            driver.SetSkillFocus(0f);
            AssertVector(driver.transform.localScale,authoredScale);
            Advance(driver,1f);
            AssertVector(driver.transform.localScale,authoredScale);
            Assert.That(driver.transform.localPosition,Is.EqualTo(spritePosition));
            Assert.That(driver.GetComponent<SpriteRenderer>().sprite,Is.SameAs(frozenSprite));
            if(!terminal)
            {
                driver.SetFrozen(false);
                Advance(driver,0f);
                AssertVector(driver.transform.localScale,authoredScale);
            }
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReuseAndDisableRemoveFocusBeforeTheMonsterIsShownAgain(bool disable)
    {
        GameObject instance=SpawnRaptor();
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            Advance(driver,0f);
            Vector3 authoredScale=driver.transform.localScale;
            driver.SetSkillFocus(1f);
            Advance(driver,0f);
            AssertVector(driver.transform.localScale,authoredScale*1.5f);

            if(disable)typeof(SpriteMonsterAnimationDriver).GetMethod("OnDisable",Private).Invoke(driver,null);
            else driver.ResetForReuse();
            AssertVector(driver.transform.localScale,authoredScale);
            Advance(driver,0f);
            AssertVector(driver.transform.localScale,authoredScale);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    private static GameObject SpawnRaptor()=>PrefabUtility.InstantiatePrefab(
        AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;

    private static void Advance(SpriteMonsterAnimationDriver driver,float seconds)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{seconds});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }

    private static void AssertVector(Vector3 actual,Vector3 expected)=>
        Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.00001f));
}
