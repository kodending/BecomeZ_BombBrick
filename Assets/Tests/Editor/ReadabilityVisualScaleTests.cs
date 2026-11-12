using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityVisualScaleTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Fields=Hidden|BindingFlags.Public;
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object source,target;
    private SpriteMonsterAnimationDriver driver;
    private bool previousEnabled,previousActive;
    private CombatReadabilityPreset previousPreset;

    [SetUp] public void Setup()
    {
        previousEnabled=CombatReadabilityPreview.Enabled;previousActive=CombatEffectTestConfiguration.Active;
        previousPreset=CombatReadabilityPreview.Preset;
        CombatReadabilityPreview.Enabled=true;CombatEffectTestConfiguration.Active=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
        fixture=new MonkeyArcTests();fixture.SetUp();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        source=Get<object>(fixture,"source");target=Get<object>(fixture,"target");
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos");
        var model=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
        model.transform.position=Get<GameObject>(source,"model").transform.position;
        driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(source,"model",model);Set(source,"definition",definition);Set(source,"spriteAnimation",driver);
        Set(source,"attackRange",definition.attackRange);
        Advance(0f);
    }

    [TearDown] public void Cleanup()
    {
        try {fixture?.TearDown();}
        finally
        {
            CombatReadabilityPreview.Enabled=previousEnabled;CombatEffectTestConfiguration.Active=previousActive;
            CombatReadabilityPreview.Preset=previousPreset;
        }
    }

    [Test] public void OnlyOptedInLocalPreviewShrinksArtAndItsCinematicClockDoesNotChangeTheFactor()
    {
        foreach(CombatReadabilityPreset preset in new[]{CombatReadabilityPreset.Current,
            CombatReadabilityPreset.Compact,CombatReadabilityPreset.Local})
        {
            CombatReadabilityPreview.Preset=preset;
            Assert.That(CombatReadabilityPreview.BodyVisualScale,
                Is.EqualTo(preset==CombatReadabilityPreset.Local?.9f:1f));
        }
        CombatReadabilityPreview.Enabled=false;
        Assert.That(CombatReadabilityPreview.BodyVisualScale,Is.EqualTo(1f));
        CombatReadabilityPreview.Enabled=true;CombatEffectTestConfiguration.Active=false;
        Assert.That(CombatReadabilityPreview.BodyVisualScale,Is.EqualTo(1f));
        CombatEffectTestConfiguration.Active=true;
        var clock=new CombatSkillClock(Get<GameObject>(source,"model").transform);
        try {clock.Hold();Assert.That(CombatReadabilityPreview.BodyVisualScale,Is.EqualTo(.9f));}
        finally {clock.End();}
    }

    [Test] public void IdleRunBasicAndSkillPosesShrinkOnceWithoutCompoundingAcrossRenderPasses()
    {
        for(int motion=0;motion<4;motion++)
        {
            CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
            driver.ResetForReuse();driver.SetMoving(motion==1);
            if(motion==2)driver.PlayBasicAttack(1f,()=>{});
            if(motion==3)driver.PlaySkillAttack(1f,()=>{});
            Advance(0f);Vector3 authored=driver.transform.localScale;
            CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
            for(int pass=0;pass<8;pass++)
            {
                Advance(0f);Call(driver,"OnWillRenderObject");
                AssertVector(driver.transform.localScale,authored*.9f);
            }
        }
    }

    [Test] public void SmallerArtworkKeepsRootAttackReachCollisionAndCachedMeleeFootprintUnchanged()
    {
        Transform root=Get<GameObject>(source,"model").transform;
        Vector3 position=root.position,scale=root.localScale;
        int range=Get<int>(source,"attackRange");float radius=Get<float>(source,"collisionRadius");
        float bodyScale=Get<float>(source,"combatBodyScale"),idleScale=driver.IdleScale;
        float reach=(float)Call(battle,"AttackDistance",source,target);
        Call(battle,"CacheMeleeReadabilityBody",source);Rect footprint=Get<Rect>(source,"meleeBodyShape");
        float visibleWidth=driver.GetComponent<SpriteRenderer>().bounds.size.x;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;Advance(0f);
        Call(battle,"CacheMeleeReadabilityBody",source);
        Assert.That(driver.GetComponent<SpriteRenderer>().bounds.size.x,Is.EqualTo(visibleWidth*.9f).Within(.00001f));
        Assert.That(root.position,Is.EqualTo(position));Assert.That(root.localScale,Is.EqualTo(scale));
        Assert.That(Get<int>(source,"attackRange"),Is.EqualTo(range));
        Assert.That(Get<float>(source,"collisionRadius"),Is.EqualTo(radius));
        Assert.That(Get<float>(source,"combatBodyScale"),Is.EqualTo(bodyScale));
        Assert.That(driver.IdleScale,Is.EqualTo(idleScale));
        Assert.That(Call(battle,"AttackDistance",source,target),Is.EqualTo(reach));
        Assert.That(Get<Rect>(source,"meleeBodyShape"),Is.EqualTo(footprint));
    }

    [Test] public void GroundRingShrinksOnlyItsVisualRadiusAcrossRankAndRepeatedRadiusChanges()
    {
        var host=Get<GameObject>(source,"ring");
        var ring=host.AddComponent<CombatTargetRing>();ring.Configure(.65f,Color.cyan,null);
        var normalizer=Get<Transform>(ring,"normalizer");
        foreach(float rootScale in new[]{.5f,1f,1.7f})
        foreach(float radius in new[]{.65f,1.3f,.65f})
        {
            host.transform.localScale=new Vector3(rootScale,.012f,rootScale*.68f);
            foreach(CombatReadabilityPreset preset in new[]{CombatReadabilityPreset.Current,
                CombatReadabilityPreset.Compact,CombatReadabilityPreset.Local})
            {
                CombatReadabilityPreview.Preset=preset;
                for(int pass=0;pass<3;pass++)
                {
                    ring.SetRadius(radius);ring.UpdateDirection(Vector3.right,false);
                    Assert.That(Get<float>(ring,"radius"),Is.EqualTo(radius));
                    Assert.That(normalizer.TransformVector(Vector3.right).magnitude,
                        Is.EqualTo(radius*(preset==CombatReadabilityPreset.Local?.9f*.8f:1f)).Within(.00001f));
                }
            }
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void FrozenSmallerAttackKeepsItsPoseAndPendingHitThenDispatchesExactlyOnce(bool skill)
    {
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;Advance(0f);
        int hits=0;
        if(skill)driver.PlaySkillAttack(1f,()=>hits++);else driver.PlayBasicAttack(1f,()=>hits++);
        Advance((skill?driver.SkillAttackHitTime:driver.BasicAttackHitTime)-.01f);
        driver.SetFrozen(true);Vector3 held=driver.transform.localScale;
        for(int i=0;i<3;i++)
        {
            Advance(.5f);Call(driver,"OnWillRenderObject");
            driver.NotifyBasicAttackHitFrameFromAnimation();driver.NotifySkillAttackHitFrameFromAnimation();
            AssertVector(driver.transform.localScale,held);Assert.That(hits,Is.Zero);
        }
        Assert.That(skill?driver.HasPendingSkillAttackHit:driver.HasPendingBasicAttackHit,Is.True);
        driver.SetFrozen(false);Advance(.015f);Advance(.03f);
        Assert.That(hits,Is.EqualTo(1));
        Assert.That(skill?driver.HasPendingSkillAttackHit:driver.HasPendingBasicAttackHit,Is.False);
    }

    [TestCase(false)] [TestCase(true)]
    public void MantisFanStillHitsTheSameEdgeOfItsDamageCorridor(bool smaller)
    {
        var renderer=driver.GetComponent<SpriteRenderer>();
        Transform root=Get<GameObject>(source,"model").transform;
        // Keep the authored fan height between its clamps so a rendering-scale leak is observable.
        root.localScale*=.9f/(renderer.sprite.bounds.size.y*Mathf.Abs(renderer.transform.lossyScale.y)*.52f);
        CombatReadabilityPreview.Preset=smaller?CombatReadabilityPreset.Local:CombatReadabilityPreset.Current;
        Advance(0f);
        object edge=Call(fixture,"Unit",1,new Vector3(4f,0f,.3f+.9f*.35f-.01f));
        Call(fixture,"StartFan",(object)null);
        PixelStarProjectile center=null;
        foreach(var shot in Object.FindObjectsOfType<PixelStarProjectile>())
            if(shot.name=="Pixel Mantis Myriad Scythe"&&
                Mathf.Abs(Get<Func<float,Vector3>>(shot,"scythePath")(1f).z)<.001f)
                center=shot;
        Assert.That(center,Is.Not.Null);
        Call(center,"Advance",1f);
        Assert.That(Get<float>(edge,"hp"),Is.EqualTo(4900f),
            "The ten-percent art reduction must not narrow the real skill hit corridor.");
    }

    private void Advance(float seconds)
    {Call(driver,"Advance",seconds);Call(driver,"LateUpdate");}
    private static void AssertVector(Vector3 actual,Vector3 expected)=>
        Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.00001f));
    private static T Get<T>(object instance,string name)=>(T)instance.GetType().GetField(name,Fields).GetValue(instance);
    private static void Set(object instance,string name,object value)=>instance.GetType().GetField(name,Fields).SetValue(instance,value);
    private static object Call(object instance,string name,params object[] args)=>
        instance.GetType().GetMethod(name,Hidden).Invoke(instance,args);
}
