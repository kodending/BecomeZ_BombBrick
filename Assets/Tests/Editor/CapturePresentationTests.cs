using System.Collections;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    [Test]
    public void SuccessfulCaptureRevealsTheCommittedStatsOnceAndClearsOnlyItsPresentation()
    {
        var type=typeof(MonsterAutoBattlePrototype);
        var captured=ChestRewardPresentationTests.MonsterWithScore(95f);
        captured.instanceId="captured_reveal";captured.monsterId="velos";
        string before=JsonUtility.ToJson(captured);
        type.GetField("capturePresentationSucceeded",Private).SetValue(battle,true);
        type.GetField("capturePresentationMonster",Private).SetValue(battle,captured);
        cast=(IEnumerator)MammothCall("PlayCaptureResult",target);
        Assert.That(cast.MoveNext(),Is.True);
        Assert.That(cast.Current,Is.Null,"Successful acquisition uses the unscaled reveal clock, not a fixed game-time wait.");
        MammothCall("UpdateCaptureAcquisitionAudio",MonsterAcquisitionReveal.StatsAt+.001f);
        Assert.That(type.GetField("captureRevealSoundMask",Private).GetValue(battle),Is.EqualTo(1));
        MammothCall("UpdateCaptureAcquisitionAudio",MonsterAcquisitionReveal.StampAt);
        Assert.That(type.GetField("captureRevealSoundMask",Private).GetValue(battle),Is.EqualTo(127));
        MammothCall("UpdateCaptureAcquisitionAudio",MonsterAcquisitionReveal.StampAt);
        Assert.That(type.GetField("captureRevealSoundMask",Private).GetValue(battle),Is.EqualTo(127));
        Assert.That(JsonUtility.ToJson(captured),Is.EqualTo(before));
        MammothCall("ClearCapturePresentation");
        Assert.That(type.GetField("capturePresentationMonster",Private).GetValue(battle),Is.Null);
        Assert.That(type.GetField("captureRevealSoundMask",Private).GetValue(battle),Is.EqualTo(0));
        Assert.That(JsonUtility.ToJson(captured),Is.EqualTo(before));
    }

    [TestCase(false,"ko","포획 실패")] [TestCase(true,"ko","포획 성공")]
    [TestCase(false,"en","Capture Failed")] [TestCase(true,"en","Capture Success")]
    public void CaptureResultKeepsOpaqueShadeAndLabelForBothOutcomes(
        bool success,string language,string expected)
    {
        string previousLanguage=GameLocalization.CurrentLanguage;
        try
        {
            GameLocalization.SetLanguage(language);
            var type=typeof(MonsterAutoBattlePrototype);
            var shadeObject=new GameObject("Capture result shade");
            var shade=shadeObject.AddComponent<SpriteRenderer>();
            shade.color=Color.black;
            type.GetField("capturePresentationShade",Private).SetValue(battle,shadeObject);
            type.GetField("capturePresentationResolving",Private).SetValue(battle,true);
            type.GetField("capturePresentationSucceeded",Private).SetValue(battle,success);
            cast=(IEnumerator)MammothCall("PlayCaptureResult",target);

            Assert.That(cast.MoveNext(),Is.True);
            Assert.That(cast.Current,Is.TypeOf<WaitForSeconds>(),"Read the result before the shade fades.");
            float hold=(float)typeof(WaitForSeconds).GetField("m_Seconds",Private).GetValue(cast.Current);
            Assert.That(hold,Is.GreaterThanOrEqualTo(1.2f));
            Assert.That(shade.color.a,Is.EqualTo(1f));
            Assert.That(type.GetField("capturePresentationResolving",Private).GetValue(battle),Is.True);
            Assert.That((float)type.GetField("capturePresentationResultStartedAt",Private).GetValue(battle),
                Is.GreaterThanOrEqualTo(0f));
            string message=(string)type.GetField("captureUiMessage",Private).GetValue(battle);
            Assert.That(message,Is.EqualTo(expected));
            Object.DestroyImmediate(shadeObject);
            MammothCall("ClearCapturePresentation");
            Assert.That(type.GetField("capturePresentationResultStartedAt",Private).GetValue(battle),Is.EqualTo(-1f));
        }
        finally{GameLocalization.SetLanguage(previousLanguage);}
    }

    [Test] public void CaptureHeartbeatAcceleratesAndExpandsOnlyItsSilhouetteEchoes()
    {
        var type=typeof(MonsterAutoBattlePrototype);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        float[] beats=(float[])type.GetField("CaptureHeartbeatBeats",flags).GetValue(null);
        Assert.That(beats.Length,Is.GreaterThanOrEqualTo(3));
        Assert.That(beats[0],Is.GreaterThanOrEqualTo(0.5f));
        Assert.That(beats[beats.Length-1],Is.LessThanOrEqualTo(0.15f));
        for(int i=1;i<beats.Length;i++)Assert.That(beats[i],Is.LessThan(beats[i-1]));
        var renderer=Model(target).AddComponent<SpriteRenderer>();
        renderer.sprite=PixelStarProjectile.BeanSprite;
        renderer.sortingOrder=30004;renderer.flipX=true;renderer.flipY=true;
        var position=renderer.transform.position;
        var scale=renderer.transform.localScale;
        MammothCall("UpdateCaptureHeartbeat",renderer,0f,beats[0],1f);
        var echoes=(SpriteRenderer[])type.GetField("captureHeartbeatEchoes",Private).GetValue(battle);
        Assert.That(echoes.Length,Is.EqualTo(2));
        Assert.That(echoes[1].color.a,Is.Zero,"The second silhouette trails the first beat.");
        MammothCall("UpdateCaptureHeartbeat",renderer,.12f,beats[0],1f);
        foreach(var echo in echoes)
        {
            Assert.That(echo.sprite,Is.SameAs(renderer.sprite));
            Assert.That(echo.flipX&&echo.flipY,Is.True);
            Assert.That(echo.sortingOrder,Is.InRange(30001,30003));
            Assert.That(echo.transform.localScale.x,Is.GreaterThan(scale.x));
            Assert.That(Vector3.Distance(echo.bounds.center,renderer.bounds.center),Is.LessThan(.001f));
            Assert.That(echo.color.a,Is.InRange(.01f,.4f));
        }
        Assert.That(renderer.transform.position,Is.EqualTo(position));
        Assert.That(renderer.transform.localScale,Is.EqualTo(scale));
        MammothCall("UpdateCaptureHeartbeat",renderer,beats[0],beats[0],1f);
        foreach(var echo in echoes)Assert.That(echo.color.a,Is.Zero);
        var root=(GameObject)type.GetField("captureHeartbeatRoot",Private).GetValue(battle);
        MammothCall("ClearCapturePresentation");
        Assert.That(root==null,Is.True,"Cancelling capture must remove every echo.");
    }

    [Test] public void CaptureFocusHidesCombatAndSelectionOutlinesUntilItEnds()
    {
        var ally=Model(source).AddComponent<ScreenSpaceOutlineTarget>();
        var enemy=Model(target).AddComponent<ScreenSpaceOutlineTarget>();
        Set(source,"outline",ally);Set(target,"outline",enemy);
        var type=typeof(MonsterAutoBattlePrototype);
        type.GetField("battleStarted",Private).SetValue(battle,true);
        type.GetField("selectedUnit",Private).SetValue(battle,source);
        MammothCall("RefreshUnitOutlines");
        Assert.That(ally.IsOutlined||enemy.IsOutlined,Is.False,
            "Normal combat no longer applies team or selection outlines.");

        type.GetField("capturePresentationResolving",Private).SetValue(battle,true);
        MammothCall("RefreshUnitOutlines");
        Assert.That(ally.IsOutlined||enemy.IsOutlined,Is.False);
        type.GetField("battleStarted",Private).SetValue(battle,false);
        MammothCall("RefreshUnitOutlines");
        Assert.That(ally.IsOutlined||enemy.IsOutlined,Is.False);

        MammothCall("ClearCapturePresentation");
        MammothCall("RefreshUnitOutlines");
        Assert.That(ally.IsOutlined,Is.True,"Selection highlighting must return after the focus closes.");
        Assert.That(enemy.IsOutlined,Is.False);
    }

    [TestCase(16f/9f,1f)] [TestCase(16f/10f,2f)] [TestCase(4f/3f,2f)]
    [TestCase(21f/9f,1f)]
    public void CaptureWaitsAboveTheCombatFieldCenter(float aspect,float scale)
    {
        var bounds=new Bounds(new Vector3(7f,0f,-3f),new Vector3(18f,0f,10f));
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private)
            .SetValue(battle,bounds);
        var camera=new GameObject("Capture waiting camera").AddComponent<Camera>();
        camera.aspect=aspect;
        MonsterAutoBattlePrototype.FrameCameraToBattleArena(camera,bounds);
        typeof(MonsterAutoBattlePrototype).GetField("mainCamera",Private).SetValue(battle,camera);
        var renderer=Model(target).AddComponent<SpriteRenderer>();
        renderer.sprite=PixelStarProjectile.BeanSprite;
        renderer.transform.rotation=camera.transform.rotation;
        renderer.transform.localScale=Vector3.one*scale;
        // The waiting spot must still track the top when framing changes during battle.
        foreach(float fov in new[]{36f,32f})
        {
            camera.fieldOfView=fov;
            Model(target).transform.position=(Vector3)MammothCall("CaptureWaitingPoint",target);
            Bounds visual=renderer.bounds;
            Assert.That(camera.WorldToViewportPoint(visual.center).x,Is.EqualTo(.5f).Within(.001f));
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)
            {
                var corner=new Vector3(x==0?visual.min.x:visual.max.x,
                    y==0?visual.min.y:visual.max.y,z==0?visual.min.z:visual.max.z);
                float height=camera.WorldToViewportPoint(corner).y;
                Assert.That(height,Is.InRange(.87f,.995f),"Remain above the floor without clipping.");
            }
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void CaptureFocusPrecedesBothResultsAndShowsMonsterAboveShade(bool success)
    {
        var camera=new GameObject("Capture test camera").AddComponent<Camera>();
        camera.orthographic=true;
        var type=typeof(MonsterAutoBattlePrototype);
        type.GetField("mainCamera",Private).SetValue(battle,camera);
        type.GetField("capturePresentationTarget",Private).SetValue(battle,target);
        type.GetField("capturePresentationSucceeded",Private).SetValue(battle,success);
        SpriteRenderer renderer=Model(target).AddComponent<SpriteRenderer>();
        renderer.sortingOrder=73;
        cast=(IEnumerator)MammothCall("PlayCaptureOutcomePresentation");
        Assert.That(cast.MoveNext(),Is.True);
        var focus=cast.Current as IEnumerator;
        Assert.That(focus,Is.Not.Null,"Both results must yield the common focus presentation first.");
        Assert.That(focus.MoveNext(),Is.True);
        var shade=(GameObject)type.GetField("capturePresentationShade",Private).GetValue(battle);
        Assert.That(shade,Is.Not.Null);
        Assert.That(renderer.sortingOrder,Is.GreaterThan(shade.GetComponent<SpriteRenderer>().sortingOrder));
        Assert.That(type.GetField("capturePresentationVisual",Private).GetValue(battle),Is.Null,
            "No success parcel may reveal the outcome before the focus and shake finish.");
        Assert.That(Model(target).activeSelf,Is.True);
        Assert.That(type.GetField("capturePresentationTargetSortingOrder",Private).GetValue(battle),Is.EqualTo(73));
    }

    [Test] public void BattleEndCancelsManualChargeWithoutLandingDamage()
    {
        Model(target).transform.position=Vector3.right*2f;
        var charge=StartMammoth("kogason");
        MammothCall("AdvanceMammothCharge",source,charge,.4f);
        var position=Model(source).transform.position;
        typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).SetValue(battle,true);
        MammothCall("EndBattleUnitAction",source);
        Assert.That(Get(source,"mammothCharge"),Is.Null);
        Assert.That(Model(source).transform.position.x,Is.EqualTo(position.x).Within(.01f));
        Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(0f));
        Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [TestCase("hold")] [TestCase("impact")] [TestCase("frozen")] [TestCase("knockdown")]
    public void BattleResultImmediatelyRestoresIdleAndCancelsPendingHits(string phase)
    {
        var model=Model(source);
        model.AddComponent<SpriteRenderer>();
        var driver=model.AddComponent<SpriteMonsterAnimationDriver>();
        var frames=new Sprite[6];
        for(int i=0;i<frames.Length;i++)frames[i]=PixelStarProjectile.BeanSprite;
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        driver.ConfigureHitFrames(3,3);
        Set(source,"spriteAnimation",driver);
        Set(source,"attackMovementLockTimer",50f);
        Set(source,"displacing",true);
        Set(target,"alive",false);
        var type=typeof(MonsterAutoBattlePrototype);
        int hits=0;
        driver.PlaySkillAttackWithHold(2,10f,1f,()=>hits++,()=>0f);
        if(phase=="impact")typeof(SpriteMonsterAnimationDriver).GetField("skillImpactClipActive",Private).SetValue(driver,true);
        if(phase=="frozen")driver.SetFrozen(true);
        if(phase=="knockdown")driver.PlayKnockdown(5f);
        MammothCall("CheckBattleResult");
        Assert.That(type.GetField("battleEnding",Private).GetValue(battle),Is.True);
        Assert.That(type.GetField("waveOutcomeStage",Private).GetValue(battle).ToString(),Is.EqualTo("Message"));
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
        Assert.That(driver.IsActionLocked,Is.False);
        Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(0f));
        Assert.That(Get(source,"displacing"),Is.False);
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        advance.Invoke(driver,new object[]{20f});
        Assert.That(hits,Is.Zero,"Ending the action must never release its queued skill hit.");
        Assert.That(MammothCall("TryUseSkill",source,false),Is.False);
        MammothCall("Attack",source,target);
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0));
    }

    [Test] public void BattleEndPreservesCapturedDeadMonstersStrugglePose()
    {
        var model=Model(target);
        model.AddComponent<SpriteRenderer>();
        var driver=model.AddComponent<SpriteMonsterAnimationDriver>();
        var frames=new Sprite[6];
        for(int i=0;i<frames.Length;i++)frames[i]=PixelStarProjectile.BeanSprite;
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        Set(target,"spriteAnimation",driver);
        Set(target,"alive",false);
        driver.SetStruggling(true);
        MammothCall("EndBattleUnitAction",target);
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Struggle"));
    }
}
