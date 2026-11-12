using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityAttackOriginTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Fields=Hidden|BindingFlags.Public;
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object actor,victim;
    private SpriteMonsterAnimationDriver driver;
    private SpriteRenderer source,target;
    private bool enabled,active;
    private CombatReadabilityPreset preset;
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()
    {
        enabled=CombatReadabilityPreview.Enabled;active=CombatEffectTestConfiguration.Active;
        preset=CombatReadabilityPreview.Preset;random=UnityEngine.Random.state;
        CombatReadabilityPreview.Enabled=CombatEffectTestConfiguration.Active=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        fixture=new MonkeyArcTests();fixture.SetUp();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        actor=Get<object>(fixture,"source");victim=Get<object>(fixture,"target");
        Source("geomchi");target=Model(victim,"velos",Vector3.right*4f).GetComponent<SpriteRenderer>();
        var camera=new GameObject("Attacker origin camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(0f,7f,-10f);camera.transform.LookAt(Vector3.zero);
    }

    [TearDown] public void Cleanup()
    {
        try {fixture?.TearDown();}
        finally
        {
            CombatReadabilityPreview.Enabled=enabled;CombatEffectTestConfiguration.Active=active;
            CombatReadabilityPreview.Preset=preset;UnityEngine.Random.state=random;
        }
    }

    [TestCase(CombatReadabilityPreset.Current,true,true)]
    [TestCase(CombatReadabilityPreset.Compact,true,true)]
    [TestCase(CombatReadabilityPreset.Local,false,true)]
    [TestCase(CombatReadabilityPreset.Local,true,false)]
    public void FactoriesLeaveExistingModesAndNonTestCombatUntouched(CombatReadabilityPreset mode,bool selected,bool test)
    {
        CombatReadabilityPreview.Preset=mode;CombatReadabilityPreview.Enabled=selected;
        CombatEffectTestConfiguration.Active=test;driver.PlayBasicAttack(1f,()=>{});
        Assert.That(ReadabilityAttackOriginEffect.TrackMelee(driver,target,false,()=>true),Is.Null);
        Assert.That(ReadabilityAttackOriginEffect.PlayLaunch(source,target),Is.Null);
    }

    [Test] public void ClawPawAndEmissionUseAtLeastFourDifferentPixelFramesAndReuseTheirCachedArt()
    {
        foreach(bool bear in new[]{false,true})
        {
            Source(bear?"woongi":"geomchi");driver.PlayBasicAttack(1f,()=>{});
            var first=ReadabilityAttackOriginEffect.TrackMelee(driver,target,bear,()=>true);
            var second=ReadabilityAttackOriginEffect.TrackMelee(driver,target,bear,()=>true);
            AssertFrames(first);Assert.That(Get<Sprite[]>(second,"frames"),Is.SameAs(Get<Sprite[]>(first,"frames")));
        }
        var launch=ReadabilityAttackOriginEffect.PlayLaunch(source,target);AssertFrames(launch);
    }

    [Test] public void LargerOriginEffectsKeepTheirTargetAxisAndOnlyClawsReverseTheSweepSide()
    {
        var sizes=new[]{.72f*1.5f,.85f*1.5f,.5f*1.5f};
        for(int kind=0;kind<3;kind++)
        {
            Source(kind==0?"geomchi":kind==1?"woongi":"kongjwi");
            driver.PlayBasicAttack(1f,()=>{});
            var fx=kind==2?ReadabilityAttackOriginEffect.PlayLaunch(source,target):
                ReadabilityAttackOriginEffect.TrackMelee(driver,target,kind==1,()=>true);
            if(kind<2){Step(driver.BasicAttackHitTime-.02f);Pose(fx);}
            var body=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",
                BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{source.sprite});
            float width=source.transform.TransformVector(Vector3.right*body.size.x).magnitude;
            float height=source.transform.TransformVector(Vector3.up*body.size.y).magnitude;
            Assert.That(fx.transform.localScale.x,Is.EqualTo(Mathf.Max(width,height*.6f)*sizes[kind]).Within(.00001f));
            Assert.That(Output(fx).flipY,Is.EqualTo(kind==0));Facing(fx);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void MeleeWindupFollowsTheAttackClockAndFreezeNeverAdvancesItsFrameOrDamage(bool bear)
    {
        Source(bear?"woongi":"geomchi");int hits=0;driver.PlayBasicAttack(1f,()=>hits++);
        var fx=ReadabilityAttackOriginEffect.TrackMelee(driver,target,bear,()=>true);
        Assert.That(Output(fx).enabled,Is.False);
        Step(driver.BasicAttackHitTime-.02f);Pose(fx);
        Assert.That(Output(fx).enabled,Is.True);Assert.That(hits,Is.Zero);
        Sprite held=Output(fx).sprite;Vector3 root=Get<GameObject>(actor,"model").transform.position;
        driver.SetFrozen(true);
        for(int i=0;i<3;i++)
        {
            Step(.5f);Pose(fx);Assert.That(Output(fx).sprite,Is.SameAs(held));Assert.That(hits,Is.Zero);
        }
        Assert.That(Get<GameObject>(actor,"model").transform.position,Is.EqualTo(root));
        driver.SetFrozen(false);Step(.025f);Pose(fx);
        Assert.That(hits,Is.EqualTo(1));Assert.That(Output(fx).enabled,Is.True);
        Assert.That(Output(fx).sprite,Is.SameAs(Get<Sprite[]>(fx,"frames")[2]),"The actual contact frame must be visible even after a large step.");
    }

    [TestCase(false)] [TestCase(true)]
    public void CanceledOrSupersededWindupRemovesItsOriginEffectWithoutInvokingTheHit(bool cancel)
    {
        int hits=0;bool valid=true;driver.PlayBasicAttack(1f,()=>hits++);
        var fx=ReadabilityAttackOriginEffect.TrackMelee(driver,target,false,()=>valid);
        Step(driver.BasicAttackHitTime-.02f);Pose(fx);Assert.That(Output(fx).enabled,Is.True);
        if(cancel)Call(driver,"CancelPendingBasicAttack");else valid=false;
        Pose(fx);Assert.That(Live(fx),Is.False);Assert.That(hits,Is.Zero);
    }

    [Test] public void MeleeOriginFollowsTheAttackerAndPointsAtTheActualTargetInScreenSpace()
    {
        driver.PlayBasicAttack(1f,()=>{});
        var fx=ReadabilityAttackOriginEffect.TrackMelee(driver,target,false,()=>true);
        Step(driver.BasicAttackHitTime-.02f);Pose(fx);
        Facing(fx);Vector3 before=fx.transform.position;
        Get<GameObject>(actor,"model").transform.position+=Vector3.right*1.5f;Pose(fx);
        Assert.That(Vector3.Distance(fx.transform.position-before,Vector3.right*1.5f),Is.LessThan(.15f));
        target.transform.parent.position=new Vector3(-4f,0f,3f);Pose(fx);Facing(fx);
        source.flipX=!source.flipX;Pose(fx);Facing(fx);
    }

    [Test] public void SwingSoundStartsWithTheFinalWindupOnceAndFreezeDoesNotReplayIt()
    {
        int swings=0,hits=0;driver.PlayBasicAttack(1f,()=>hits++);
        var fx=ReadabilityAttackOriginEffect.TrackMelee(driver,target,false,()=>true,()=>swings++);
        Assert.That(swings,Is.Zero);
        Step(driver.BasicAttackHitTime-.11f);Pose(fx);Assert.That(swings,Is.Zero);
        Step(.03f);Pose(fx);Assert.That(swings,Is.EqualTo(1));Assert.That(hits,Is.Zero);
        driver.SetFrozen(true);Step(.5f);Pose(fx);Assert.That(swings,Is.EqualTo(1));Assert.That(hits,Is.Zero);
        driver.SetFrozen(false);Step(.09f);Pose(fx);Pose(fx);
        Assert.That(swings,Is.EqualTo(1));Assert.That(hits,Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void CanceledWindupAndResultBeforeTheSwingNeverPlayADelayedSound(bool result)
    {
        int swings=0;driver.PlayBasicAttack(1f,()=>{});
        var fx=ReadabilityAttackOriginEffect.TrackMelee(driver,target,false,()=>true,()=>swings++);
        if(result)fx.PrepareEndPlayback();else {Call(driver,"CancelPendingBasicAttack");Pose(fx);}
        Assert.That(swings,Is.Zero);Assert.That(Live(fx),Is.False);
    }

    [Test] public void LaunchFinishesAfterSourceDeathWithoutChangingTheExistingProjectileOrItsCallback()
    {
        Source("kongjwi");int arrivals=0;
        var shot=PixelStarProjectile.Play(source.transform,source,target.transform,target,Color.magenta,false,()=>arrivals++);
        float duration=shot.FlightDuration;Vector3 start=Get<Vector3>(shot,"start"),end=Get<Vector3>(shot,"lastTarget");
        var state=JsonUtility.ToJson(UnityEngine.Random.state);
        var fx=ReadabilityAttackOriginEffect.PlayLaunch(source,target);
        Assert.That(Output(fx).enabled,Is.True);Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(state));
        Object.DestroyImmediate(Get<GameObject>(actor,"model"));Call(fx,"Advance",.02f);
        Assert.That(Live(fx),Is.True);Assert.That(Output(fx).enabled,Is.True);Assert.That(arrivals,Is.Zero);
        Assert.That(shot.FlightDuration,Is.EqualTo(duration));Assert.That(Get<Vector3>(shot,"start"),Is.EqualTo(start));
        Assert.That(Get<Vector3>(shot,"lastTarget"),Is.EqualTo(end));
        Call(fx,"Advance",.3f);Assert.That(Live(fx),Is.False);Assert.That(arrivals,Is.Zero);
        Call(shot,"Advance",1f);Assert.That(arrivals,Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void BattleEndKeepsOnlyAnAlreadyVisibleTailAndNeverCreatesAPendingSwing(bool visible)
    {
        driver.PlayBasicAttack(1f,()=>{});
        var fx=ReadabilityAttackOriginEffect.TrackMelee(driver,target,false,()=>true);
        if(visible){Step(driver.BasicAttackHitTime-.02f);Pose(fx);}
        fx.PrepareEndPlayback();
        if(!visible){Assert.That(Live(fx),Is.False);return;}
        driver.ResetForReuse();Object.DestroyImmediate(Get<GameObject>(actor,"model"));
        fx.AdvanceEnd(.02f);Assert.That(Live(fx),Is.True);Assert.That(Output(fx).enabled,Is.True);
        fx.AdvanceEnd(.4f);Assert.That(Live(fx),Is.False);
    }

    [Test] public void ActualBattleHookIncludesTheNewFamiliesAndWaveClearRemovesTheirMeleeEffects()
    {
        foreach(string species in new[]{"geomchi","woongi","kongjwi","velos"})
        {
            Source(species);driver.PlayBasicAttack(1f,()=>{});
            Call(battle,"TrackReadabilityMeleeAttack",actor,victim);
            Assert.That(Get<bool>(driver,"readabilityBasicPoseActive"),Is.True);
            Assert.That(Object.FindObjectsOfType<ReadabilityAttackOriginEffect>().Length,Is.EqualTo(species=="kongjwi"?0:1));
            Call(battle,"ClearBattleEffects");Assert.That(Object.FindObjectsOfType<ReadabilityAttackOriginEffect>().Length,Is.Zero);
            Step(.01f);Call(battle,"TrackReadabilityMeleeAttack",actor,victim);
            Assert.That(Object.FindObjectsOfType<ReadabilityAttackOriginEffect>().Length,Is.Zero,"A delayed or rejected attack must not attach another swing.");
        }
    }

    private void Source(string species)
    {driver=Model(actor,species,Vector3.zero);source=driver.GetComponent<SpriteRenderer>();}
    private SpriteMonsterAnimationDriver Model(object unit,string species,Vector3 position)
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get(species);
        var model=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));model.transform.position=position;
        var animation=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(unit,"model",model);Set(unit,"definition",definition);Set(unit,"spriteAnimation",animation);Set(unit,"skillId",definition.skillId);
        Call(animation,"Advance",0f);Call(animation,"LateUpdate");return animation;
    }
    private void Step(float seconds){Call(driver,"Advance",seconds);Call(driver,"LateUpdate");}
    private static void Pose(ReadabilityAttackOriginEffect fx)=>Call(fx,"LateUpdate");
    private static SpriteRenderer Output(ReadabilityAttackOriginEffect fx)=>Get<SpriteRenderer>(fx,"output");
    private static bool Live(ReadabilityAttackOriginEffect fx)=>fx!=null&&fx.gameObject.activeSelf;
    private void Facing(ReadabilityAttackOriginEffect fx)
    {
        Camera camera=Camera.main;
        Vector3 projected=camera.WorldToScreenPoint(CombatReadabilityPreview.VisibleBodyCenter(target))-
            camera.WorldToScreenPoint(CombatReadabilityPreview.VisibleBodyCenter(source));
        Vector3 direction=(camera.transform.right*projected.x+camera.transform.up*projected.y).normalized;
        Assert.That(Vector3.Dot(Output(fx).transform.right,direction),Is.GreaterThan(.98f));
        Assert.That(Vector3.Dot(fx.transform.position-CombatReadabilityPreview.VisibleBodyCenter(source),direction),Is.GreaterThan(0f));
    }
    private static void AssertFrames(ReadabilityAttackOriginEffect fx)
    {
        Assert.That(fx,Is.Not.Null);var frames=Get<Sprite[]>(fx,"frames");Assert.That(frames.Length,Is.GreaterThanOrEqualTo(4));
        var hashes=new HashSet<int>();foreach(var frame in frames)hashes.Add(PixelHash(frame.texture));
        Assert.That(hashes.Count,Is.GreaterThanOrEqualTo(4),"Frames must contain different pixel shapes, not repeat one transformed picture.");
    }
    private static int PixelHash(Texture texture)
    {
        var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);
        var copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(texture,target);RenderTexture.active=target;copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();
            int hash=17;unchecked{foreach(Color32 pixel in copy.GetPixels32())hash=hash*31+pixel.r+pixel.g*257+pixel.b*65537+pixel.a*16777619;}return hash;
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(copy);}
    }
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Fields).GetValue(value);
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Fields).SetValue(value,data);
    private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Hidden).Invoke(value,args);
}
