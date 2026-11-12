using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SpriteVisualOffsetTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Vector3 Anchor=new Vector3(.17f,.11f,-.23f);
    private static readonly Vector3 RollOffset=new Vector3(.6f,.1f,.2f);
    private readonly List<GameObject> owned=new List<GameObject>();
    private GameObject body;
    private SpriteMonsterAnimationDriver driver;
    private SpriteMonsterBillboard billboard;
    private Vector3 bodyPosition;
    private Quaternion bodyRotation;
    private float clock;

    [SetUp] public void Setup()
    {
        var camera=Go("Visual offset camera").AddComponent<Camera>();camera.cullingMask=0;
        camera.transform.rotation=Quaternion.Euler(58f,0f,0f);
        camera.transform.position=camera.transform.rotation*(Vector3.back*25f);
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        Assert.That(database,Is.Not.Null);
        var definition=database.Get("tino");Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null);
        body=Object.Instantiate(prefab);owned.Add(body);
        body.transform.position=new Vector3(2f,0f,-1f);
        body.transform.rotation=Quaternion.Euler(0f,23f,0f);
        body.transform.localScale=new Vector3(1.5f,1.3f,1.7f);
        bodyPosition=body.transform.position;bodyRotation=body.transform.rotation;
        driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        billboard=body.GetComponentInChildren<SpriteMonsterBillboard>(true);
        Assert.That(driver,Is.Not.Null);Assert.That(billboard,Is.Not.Null);
        Call(driver,"Awake");driver.ResetForReuse();
        Call(billboard,"Awake");Field(billboard,"cachedCamera").SetValue(billboard,camera);
        driver.transform.localPosition=Anchor;
        billboard.SetRollPose(0f,Vector3.zero,Vector3.zero);
    }

    [TearDown] public void Cleanup()
    {
        if(driver!=null)Call(driver,"OnDisable");
        if(billboard!=null)Call(billboard,"OnDisable");
        for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [Test]
    public void HeldSkillCapturesTheAuthoredAnchorWhileThePreviousDeathRollIsStillDrawn()
    {
        Roll(147f);Call(billboard,"LateUpdate");
        Assert.That(Vector3.Distance(driver.transform.localPosition,Anchor),Is.GreaterThan(.2f));
        driver.PlaySkillAttackWithHold(1,.8f,1f,null,()=>clock);
        Assert.That(Vector3.Distance((Vector3)Field(driver,"skillAttackHoldPosition").GetValue(driver),Anchor),
            Is.LessThan(.00001f),"A displayed death-roll offset must never become the next skill's stored anchor.");
        Frame(.02f);
        AssertUnposedNearAnchor(.04f);
        driver.InterruptCurrentAction(true);EndRoll();Settle();AssertRestored();
    }

    [Test]
    public void ACompleteHeldSkillAndChangingFullRollsCannotAccumulatePositionOrRepeatItsHit()
    {
        int hits=0;
        driver.PlaySkillAttackWithHold(1,.65f,1f,()=>hits++,()=>clock);
        for(int frame=0;frame<120;frame++)
        {
            Roll(frame*12f);Frame(.02f);
            AssertUnposedNearAnchor(.04f);
        }
        Assert.That(hits,Is.EqualTo(1));
        EndRoll();Settle();AssertRestored();
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
        Frame(.5f);Assert.That(hits,Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void FreezingWithBothContactRecoilAndRollDoesNotBakeEitherOffsetIntoTheFrozenPose(bool holding)
    {
        int hits=0;
        if(holding)driver.PlaySkillAttackWithHold(1,.65f,1f,()=>hits++,()=>clock);
        Roll(239f);Frame(.02f);
        driver.PlayContactReaction(driver.transform.right+driver.transform.up*.4f,true);
        driver.PlayImpactRecoil(driver.transform.right,.16f);
        Field(driver,"impactRecoilRemaining").SetValue(driver,.112f);
        Call(driver,"LateUpdate");Call(billboard,"LateUpdate");
        Assert.That(Vector3.Distance(driver.transform.localPosition,Anchor),Is.GreaterThan(.15f));
        driver.SetFrozen(true);
        Assert.That(Vector3.Distance((Vector3)Field(driver,"statusFrozenPosition").GetValue(driver),Anchor),
            Is.LessThan(.025f),"Freeze may retain the held-frame quiver, never roll or contact displacement.");
        for(int frame=0;frame<30;frame++)
        {
            Roll(720f*frame/29f);Frame(.02f);
            // Multiple draws in one frame must not subtract yesterday's roll after an absolute pose write.
            Call(driver,"LateUpdate");Call(billboard,"LateUpdate");
            Render();
            AssertUnposedNearAnchor(.045f);
        }
        Assert.That(hits,Is.Zero);
        driver.SetFrozen(false);EndRoll();Settle();AssertRestored(holding?.025f:.00001f);
        Assert.That(hits,Is.EqualTo(holding?1:0));
    }

    [TestCase(false)] [TestCase(true)]
    public void RenderingAFrozenSpriteRestoresItsFrameAndScaleWithoutRemovingTheComposedRoll(bool holding)
    {
        if(holding)driver.PlaySkillAttackWithHold(1,.8f,1f,null,()=>clock);
        Roll(132f);Frame(.02f);driver.SetFrozen(true);Frame(.02f);
        Vector3 composed=driver.transform.position;
        Assert.That(Vector3.Distance(composed,billboard.UnposedPoint(Vector3.zero)),Is.GreaterThan(.2f));
        var renderer=driver.GetComponent<SpriteRenderer>();
        Sprite frozen=renderer.sprite;Vector3 scale=driver.transform.localScale;
        renderer.sprite=null;driver.transform.localScale=scale*1.1f;
        Render();Render();
        Assert.That(renderer.sprite,Is.SameAs(frozen));
        Assert.That(driver.transform.localScale,Is.EqualTo(scale));
        Assert.That(Vector3.Distance(driver.transform.position,composed),Is.LessThan(.000001f));
        Assert.That(((Vector3)Field(billboard,"appliedRollOffset").GetValue(billboard)).sqrMagnitude,
            Is.GreaterThan(.01f),"The render callback must retain the billboard's already-composed translation.");
        AssertUnposedNearAnchor(.045f);
        driver.SetFrozen(false);driver.InterruptCurrentAction(true);EndRoll();Settle();AssertRestored();
    }

    [TestCase("interrupt")] [TestCase("reuse")]
    [TestCase("driver_first_disable")] [TestCase("billboard_first_disable")]
    public void CancelReuseAndEitherDisableOrderRestoreTheNonzeroAnchorWithoutReleasingTheQueuedHit(string transition)
    {
        int hits=0;
        Roll(317f);Call(billboard,"LateUpdate");
        driver.PlaySkillAttackWithHold(1,5f,1f,()=>hits++,()=>clock);
        for(int frame=0;frame<12;frame++){Roll(frame*60f);Frame(.02f);}
        driver.PlayContactReaction(driver.transform.right,true);
        Call(driver,"LateUpdate");Call(billboard,"LateUpdate");
        if(transition=="interrupt")driver.InterruptCurrentAction(true);
        else if(transition=="reuse")driver.ResetForReuse();
        else if(transition=="driver_first_disable")
        {Call(driver,"OnDisable");Call(billboard,"OnDisable");}
        else {Call(billboard,"OnDisable");Call(driver,"OnDisable");}
        EndRoll();
        if(transition.Contains("disable"))
        {
            AssertRestored(.025f);
            driver.ResetForReuse();
        }
        Settle();AssertRestored();
        Assert.That(hits,Is.Zero,"Clearing a visual pose must not dispatch the abandoned skill hit.");
    }

    private void Roll(float angle)=>billboard.SetRollPose(angle,new Vector3(.07f,.24f,0f),RollOffset);
    private void EndRoll()=>billboard.SetRollPose(0f,Vector3.zero,Vector3.zero);
    private void Frame(float dt)
    {
        if(!(bool)Field(driver,"statusFrozen").GetValue(driver))
        {
            clock+=dt;
            // The deterministic equivalent of Driver.Update precedes Billboard.Update in play.
            Call(driver,"ClearAppliedRecoil");
            Field(driver,"impactRecoilRemaining").SetValue(driver,
                Mathf.Max(0f,(float)Field(driver,"impactRecoilRemaining").GetValue(driver)-dt));
            Field(driver,"impactReactionStartedFrame").SetValue(driver,-1);
            Call(driver,"Advance",dt);
        }
        Call(billboard,"Update");Call(driver,"LateUpdate");Call(billboard,"LateUpdate");
        Render();
    }
    private void Render()
    {
        Vector3 composed=driver.transform.position;
        Call(driver,"OnWillRenderObject");
        Assert.That(Vector3.Distance(driver.transform.position,composed),Is.LessThan(.000001f),
            "Rendering must preserve the translation already composed by Driver.LateUpdate and Billboard.LateUpdate.");
    }
    private void Settle(){for(int frame=0;frame<100;frame++)Frame(.02f);}
    private void AssertUnposedNearAnchor(float tolerance)
    {
        Vector3 expected=driver.transform.parent.TransformPoint(Anchor);
        Assert.That(Vector3.Distance(billboard.UnposedPoint(Vector3.zero),expected),Is.LessThan(tolerance));
        Assert.That(body.transform.position,Is.EqualTo(bodyPosition));
        Assert.That(Quaternion.Angle(body.transform.rotation,bodyRotation),Is.LessThan(.0001f));
    }
    private void AssertRestored(float tolerance=.00001f)
    {
        Assert.That(Vector3.Distance(driver.transform.localPosition,Anchor),Is.LessThan(tolerance));
        Assert.That(Field(driver,"appliedRecoilOffset").GetValue(driver),Is.EqualTo(Vector3.zero));
        Assert.That(Field(billboard,"appliedRollOffset").GetValue(billboard),Is.EqualTo(Vector3.zero));
        Assert.That(body.transform.position,Is.EqualTo(bodyPosition));
    }
    private GameObject Go(string name){var value=new GameObject(name);owned.Add(value);return value;}
    private static FieldInfo Field(object value,string name)=>value.GetType().GetField(name,Private);
    private static void Call(object value,string name,params object[] args)=>
        value.GetType().GetMethod(name,Private).Invoke(value,args);
}
