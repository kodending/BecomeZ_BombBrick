using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityStraightPunchTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object actor,victim;
    private SpriteMonsterAnimationDriver driver;
    private SpriteRenderer source,target;
    private Camera camera;
    private readonly List<Camera> mutedCameras=new List<Camera>();
    private int users;
    private bool test,preview;
    private CombatReadabilityPreset preset;
    private UnityEngine.Random.State random;

    [SetUp]public void Setup()
    {
        users=CombatReadabilityPresentation.BattleUsers;test=CombatEffectTestConfiguration.Active;
        preview=CombatReadabilityPreview.Enabled;preset=CombatReadabilityPreview.Preset;random=UnityEngine.Random.state;
        while(CombatReadabilityPresentation.BattleUsers>0)CombatReadabilityPresentation.UnregisterBattle();
        fixture=new MonkeyArcTests();fixture.SetUp();battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        actor=Get<object>(fixture,"source");victim=Get<object>(fixture,"target");
        CombatEffectTestConfiguration.Active=false;CombatReadabilityPreview.Enabled=false;CombatReadabilityPresentation.RegisterBattle();
        foreach(Camera existing in Resources.FindObjectsOfTypeAll<Camera>())
            if(existing.gameObject.scene.IsValid()&&existing.enabled&&existing.CompareTag("MainCamera"))
            {mutedCameras.Add(existing);existing.enabled=false;}
        camera=new GameObject("Straight punch production camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(0f,7f,-10f);camera.transform.LookAt(Vector3.zero);Set(battle,"mainCamera",camera);
        Source("lilas");target=Model(victim,"velos",Vector3.right*4f).GetComponent<SpriteRenderer>();
    }

    [TearDown]public void Cleanup()
    {
        try{fixture?.TearDown();}
        finally
        {
            foreach(Camera previous in mutedCameras)if(previous!=null)previous.enabled=true;mutedCameras.Clear();
            while(CombatReadabilityPresentation.BattleUsers>0)CombatReadabilityPresentation.UnregisterBattle();
            for(int i=0;i<users;i++)CombatReadabilityPresentation.RegisterBattle();
            CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=preview;
            CombatReadabilityPreview.Preset=preset;UnityEngine.Random.state=random;
        }
    }

    [TestCase("lilas",7,"Readability Straight Punch")][TestCase("woongi",1,"Readability Attack Sweep")]
    public void ProductionSeparatesTheStraightPunchVisualWhilePreservingTheSharedBodyPose(string species,int effectStyle,string name)
    {
        Source(species);Assert.That(Call(battle,"ReadabilityBasicAttackStyle",actor),Is.EqualTo(1));
        int hits=0;driver.PlayBasicAttack(1f,()=>hits++);Call(battle,"TrackReadabilityMeleeAttack",actor,victim);
        var effect=Object.FindObjectsOfType<ReadabilityAttackOriginEffect>().Single(fx=>Get<SpriteRenderer>(fx,"source")==source);
        Assert.That(effect.name,Is.EqualTo(name));Assert.That(Get<int>(effect,"style"),Is.EqualTo(effectStyle));
        Assert.That(Get<int>(driver,"readabilityBasicPoseStyle"),Is.EqualTo(1));
        Step(driver.BasicAttackHitTime+.001f);Pose(effect);Assert.That(hits,Is.EqualTo(1));
        Assert.That(Output(effect).enabled,Is.True);Assert.That(source.sprite,Is.SameAs(Get<Sprite[]>(driver,"attackFrames")[3]));
    }

    [Test]
    public void StraightPunchReusesAllSixExistingPressureDrawingsRatherThanCreatingOrTransformingOnePicture()
    {
        driver.PlayBasicAttack(1f,()=>{});
        var punch=ReadabilityAttackOriginEffect.TrackAttack(driver,target,ReadabilityAttackOriginEffect.StraightPunchStyle,()=>true);
        var existing=ReadabilityAttackOriginEffect.TrackAttack(driver,target,4,()=>true);
        Sprite[] frames=Get<Sprite[]>(punch,"frames");Assert.That(frames,Is.SameAs(Get<Sprite[]>(existing,"frames")));
        Assert.That(frames.Length,Is.EqualTo(6));
        var hash=typeof(ReadabilityAttackOriginTests).GetMethod("PixelHash",Hidden);
        Assert.That(frames.Select(frame=>(int)hash.Invoke(null,new object[]{frame.texture})).Distinct().Count(),Is.GreaterThanOrEqualTo(4));
        Step(driver.BasicAttackHitTime-.02f);Pose(punch);
        Assert.That(punch.transform.localScale.x,Is.GreaterThan(punch.transform.localScale.y*2f));
    }

    [TestCase(0,1f,0f)][TestCase(1,-1f,0f)][TestCase(0,1f,1f)][TestCase(1,-1f,-1f)]
    public void EitherTeamsHorizontalAndDiagonalPunchesFollowTheProjectedTargetWithoutDoubleFlipping(int team,float x,float z)
    {
        Set(actor,"team",Enum.ToObject(Get<object>(actor,"team").GetType(),team));source.flipX=team==1;
        target.transform.root.position=new Vector3(x*4f,0f,z*3f);driver.PlayBasicAttack(1f,()=>{});
        Bounds original=Body(source.sprite);
        var effect=ReadabilityAttackOriginEffect.TrackAttack(driver,target,ReadabilityAttackOriginEffect.StraightPunchStyle,()=>true);
        Step(driver.BasicAttackHitTime-.02f);Pose(effect);AssertAxis(effect,original);
        source.flipX=!source.flipX;Pose(effect);AssertAxis(effect,original);
        Assert.That(Output(effect).flipX||Output(effect).flipY,Is.False);
    }

    [Test]
    public void ChangingAllActualAttackFramesKeepsTheLocalAnchorStableAndDoesNotAlterTheHitOrDamage()
    {
        int hits=0;float hp=Get<float>(victim,"hp");float attack=Get<float>(actor,"damage");
        Sprite[] actualFrames=Get<Sprite[]>(driver,"attackFrames");var originals=(Sprite[])actualFrames.Clone();
        Vector3 root=Get<GameObject>(actor,"model").transform.position;float hit=driver.BasicAttackHitTime;
        driver.PlayBasicAttack(1f,()=>{hits++;Set(victim,"hp",Get<float>(victim,"hp")-attack);});
        var effect=ReadabilityAttackOriginEffect.TrackAttack(driver,target,ReadabilityAttackOriginEffect.StraightPunchStyle,()=>true);
        Step(hit-.02f);Pose(effect);Vector3 anchor=effect.transform.position,scale=effect.transform.localScale;Sprite held=source.sprite;
        foreach(Sprite frame in actualFrames)
        {
            source.sprite=frame;Pose(effect);Assert.That(Vector3.Distance(effect.transform.position,anchor),Is.LessThan(.00001f));
            Assert.That(effect.transform.localScale,Is.EqualTo(scale));Assert.That(hits,Is.Zero);Assert.That(Get<float>(victim,"hp"),Is.EqualTo(hp));
        }
        source.sprite=held;Step(.025f);Pose(effect);
        Assert.That(hits,Is.EqualTo(1));Assert.That(Get<float>(victim,"hp"),Is.EqualTo(hp-attack));
        Assert.That(driver.BasicAttackHitTime,Is.EqualTo(hit));Assert.That(actualFrames,Is.EqualTo(originals));
        Assert.That(Get<GameObject>(actor,"model").transform.position,Is.EqualTo(root));
        effect.PrepareEndPlayback();effect.AdvanceEnd(.4f);Assert.That(Live(effect),Is.False);Assert.That(hits,Is.EqualTo(1));
    }

    [TestCase("freeze")][TestCase("cancel")][TestCase("superseded")][TestCase("result")]
    public void FrozenWindupAndCancelledOrFinishedVisualsNeverCreateAnExtraPunch(string reason)
    {
        bool valid=true;int hits=0,swings=0;driver.PlayBasicAttack(1f,()=>hits++);
        var effect=ReadabilityAttackOriginEffect.TrackAttack(driver,target,ReadabilityAttackOriginEffect.StraightPunchStyle,()=>valid,()=>swings++);
        Step(driver.BasicAttackHitTime-.02f);Pose(effect);Assert.That(swings,Is.EqualTo(1));Sprite held=Output(effect).sprite;
        if(reason=="freeze")
        {
            driver.SetFrozen(true);float clock=driver.BasicAttackAnimationTime;
            for(int i=0;i<3;i++){Step(.5f);Pose(effect);Assert.That(driver.BasicAttackAnimationTime,Is.EqualTo(clock));Assert.That(Output(effect).sprite,Is.SameAs(held));}
            Assert.That(hits,Is.Zero);driver.SetFrozen(false);Step(.025f);Pose(effect);Assert.That(hits,Is.EqualTo(1));
            effect.PrepareEndPlayback();effect.AdvanceEnd(.4f);
        }
        else if(reason=="result")
        {effect.PrepareEndPlayback();Call(driver,"CancelPendingBasicAttack");effect.AdvanceEnd(.4f);Assert.That(hits,Is.Zero);}
        else
        {if(reason=="cancel")Call(driver,"CancelPendingBasicAttack");else valid=false;Pose(effect);Assert.That(hits,Is.Zero);}
        Assert.That(Live(effect),Is.False);Assert.That(swings,Is.EqualTo(1));
    }

    private void AssertAxis(ReadabilityAttackOriginEffect effect,Bounds original)
    {
        Vector3 point=original.center;if(source.flipX)point.x=-point.x;if(source.flipY)point.y=-point.y;
        Vector3 center=source.transform.TransformPoint(point);
        Vector3 screen=camera.WorldToScreenPoint(CombatReadabilityPresentation.VisibleBodyCenter(target))-camera.WorldToScreenPoint(center);
        Vector3 direction=(camera.transform.right*screen.x+camera.transform.up*screen.y).normalized;
        Assert.That(Vector3.Dot(effect.transform.right,direction),Is.GreaterThan(.999f));
        Assert.That(Vector3.Dot(effect.transform.position-center,direction),Is.GreaterThan(0f));
    }
    private void Source(string species){driver=Model(actor,species,Vector3.zero);source=driver.GetComponent<SpriteRenderer>();}
    private SpriteMonsterAnimationDriver Model(object unit,string species,Vector3 position)
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get(species);
        var model=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));model.transform.position=position;
        var animation=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(unit,"model",model);Set(unit,"definition",definition);Set(unit,"spriteAnimation",animation);Set(unit,"skillId",definition.skillId);
        Call(animation,"Advance",0f);Call(animation,"LateUpdate");return animation;
    }
    private void Step(float seconds){Call(driver,"Advance",seconds);Call(driver,"LateUpdate");}
    private static void Pose(ReadabilityAttackOriginEffect effect)=>Call(effect,"LateUpdate");
    private static SpriteRenderer Output(ReadabilityAttackOriginEffect effect)=>Get<SpriteRenderer>(effect,"output");
    private static bool Live(ReadabilityAttackOriginEffect effect)=>effect!=null&&effect.gameObject.activeSelf;
    private static Bounds Body(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{sprite});
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Hidden).GetValue(value);
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Hidden).SetValue(value,data);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Hidden).Invoke(value,args);
}
