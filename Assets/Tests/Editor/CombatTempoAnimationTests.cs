using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatTempoAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject body;
    private SpriteMonsterAnimationDriver driver;
    private float previousSpeed;

    [SetUp]
    public void SetUp()
    {
        previousSpeed=Time.timeScale;
        body=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
        driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Advance(0f);
    }

    [TearDown]
    public void TearDown()
    {
        try { Assert.That(Time.timeScale,Is.EqualTo(previousSpeed),"Animation tempo must not change the user's world playback speed."); }
        finally { Object.DestroyImmediate(body);Time.timeScale=previousSpeed; }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IdleAndRunAdvanceAtEightyPercentOfTheNormalAnimationClock(bool moving)
    {
        Assert.That(driver.CombatPlaybackSpeed,Is.EqualTo(1f));
        driver.SetMoving(moving);Advance(.25f);
        float normal=Elapsed;
        Assert.That(normal,Is.EqualTo(.25f).Within(.00001f));

        driver.ResetForReuse();driver.SetCombatPlaybackSpeed(.8f);
        driver.SetMoving(moving);Advance(.25f);
        Assert.That(driver.CombatPlaybackSpeed,Is.EqualTo(.8f));
        Assert.That(Elapsed,Is.EqualTo(normal*.8f).Within(.00001f));
        Assert.That(driver.CurrentMotionName,Is.EqualTo(moving?"Run":"Idle"));
    }

    [Test]
    public void SkillDamageWaitsForTheSlowerDrawnWindupAndStillDispatchesExactlyOnce()
    {
        int hits=0;
        driver.SetCombatPlaybackSpeed(.8f);driver.PlaySkillAttack(1f,()=>hits++);
        float contact=driver.SkillAttackHitTime;
        Advance((contact-.01f)/.8f);
        Assert.That(Elapsed,Is.EqualTo(contact-.01f).Within(.00001f));
        Assert.That(hits,Is.Zero);Assert.That(driver.HasPendingSkillAttackHit,Is.True);

        Advance(.02f/.8f);
        Assert.That(hits,Is.EqualTo(1));Assert.That(driver.HasPendingSkillAttackHit,Is.False);
        Advance(.03f);
        Assert.That(hits,Is.EqualTo(1));
    }

    [Test]
    public void MammothChargeCallbackAndContactPoseShareTheSlowerClockAndCompleteOnce()
    {
        Object.DestroyImmediate(body);
        body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/057_kogason"));
        driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        driver.SetCombatPlaybackSpeed(.8f);
        float reported=-1f;int finished=0;
        driver.PlayMammothCharge(()=>true,time=>reported=time,()=>finished++);
        Advance(.4f);
        Assert.That(reported,Is.EqualTo(.32f).Within(.00001f));
        Assert.That(Field<float>("mammothChargeElapsed"),Is.EqualTo(reported));
        Assert.That(driver.CurrentFrameIndex,Is.LessThan(driver.SkillAttackHitFrameIndex));

        float contact=MammothCombatRules.ContactTime+.001f;
        Advance(contact/.8f-.4f);
        Assert.That(reported,Is.EqualTo(contact).Within(.00001f));
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(driver.SkillAttackHitFrameIndex));
        Assert.That(driver.GetComponent<SpriteRenderer>().sprite,
            Is.SameAs(driver.GetSkillAttackFrame(driver.SkillAttackHitFrameIndex)));
        Assert.That(finished,Is.Zero);

        Advance((MammothCombatRules.EndTime-contact+.02f)/.8f);
        Assert.That(finished,Is.EqualTo(1));
        Advance(.2f);Assert.That(finished,Is.EqualTo(1));
    }

    [Test]
    public void BasicAttackPlaybackAlreadyScaledByItsIntervalIsNotSlowedASecondTime()
    {
        int hits=0;
        driver.SetCombatPlaybackSpeed(.8f);driver.PlayBasicAttack(.8f,()=>hits++);
        float contact=driver.BasicAttackHitTime;
        Advance((contact-.01f)/.8f);
        Assert.That(Elapsed,Is.EqualTo(contact-.01f).Within(.00001f));
        Assert.That(hits,Is.Zero);
        Advance(.02f/.8f);
        Assert.That(hits,Is.EqualTo(1));Assert.That(driver.HasPendingBasicAttackHit,Is.False);
        Advance(.03f);Assert.That(hits,Is.EqualTo(1));
    }

    [Test]
    public void StrugglingUsesTheAlreadyPacedOwnerClockWithoutSlowingItASecondTime()
    {
        float ownerClock=0f;
        driver.SetCombatPlaybackSpeed(.8f);driver.SetGlobalSpeedMultiplier(1f);
        driver.SetStruggling(true,()=>ownerClock);
        ownerClock=.16f;Advance(1f);
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Struggle"));
        Assert.That(Elapsed,Is.EqualTo(.16f).Within(.00001f));
        Advance(1f);
        Assert.That(Elapsed,Is.EqualTo(.16f).Within(.00001f),"Only the owner's clock may advance this pose.");
        ownerClock=.32f;Advance(.001f);
        Assert.That(Elapsed,Is.EqualTo(.32f).Within(.00001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StatusSlowAndCombatTempoMultiplyOnceAndReuseRestoresBothDefaults(bool basic)
    {
        driver.SetCombatPlaybackSpeed(.8f);driver.SetGlobalSpeedMultiplier(.5f);
        if(basic)driver.PlayBasicAttack(.8f,null);
        else driver.PlaySkillAttack(1f,null);
        Advance(.2f);
        Assert.That(Elapsed,Is.EqualTo(.2f*.8f*.5f).Within(.00001f));
        Assert.That(driver.CombatPlaybackSpeed,Is.EqualTo(.8f));
        Assert.That(driver.GlobalSpeedMultiplier,Is.EqualTo(.5f));

        driver.ResetForReuse();
        Assert.That(driver.CombatPlaybackSpeed,Is.EqualTo(1f));
        Assert.That(driver.GlobalSpeedMultiplier,Is.EqualTo(1f));
        driver.SetMoving(true);Advance(.2f);
        Assert.That(Elapsed,Is.EqualTo(.2f).Within(.00001f));
    }

    private float Elapsed=>Field<float>("motionElapsed");
    private T Field<T>(string name)=>(T)typeof(SpriteMonsterAnimationDriver).GetField(name,Private).GetValue(driver);
    private void Advance(float seconds)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{seconds});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }
}
