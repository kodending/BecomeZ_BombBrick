using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityRaptorSkillDensityTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private RaptorRelentlessPresentationTests fixture;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private Dictionary<PixelCombatEffect,bool> existingEffects;
    private bool testMode,enabled;
    private CombatReadabilityPreset preset;

    [SetUp] public void Setup()
    {
        testMode=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;preset=CombatReadabilityPreview.Preset;
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        existingEffects=Resources.FindObjectsOfTypeAll<PixelCombatEffect>().ToDictionary(e=>e,e=>e.gameObject.activeSelf);
        fixture=new RaptorRelentlessPresentationTests();fixture.Setup();
        battle=(MonsterAutoBattlePrototype)typeof(RaptorRelentlessPresentationTests).GetField("battle",Hidden).GetValue(fixture);
        Mode(true,true,CombatReadabilityPreset.Local);
    }

    [TearDown] public void Cleanup()
    {
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&existingEffects.TryGetValue(effect,out bool active)&&!active&&effect.gameObject.activeSelf)
                effect.ReleaseAfterBattleEndFade();
        fixture?.Cleanup();
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&go.scene.IsValid()&&go.transform.parent==null&&!existing.Contains(go))Object.DestroyImmediate(go);
        CombatEffectTestConfiguration.Active=testMode;CombatReadabilityPreview.Enabled=enabled;CombatReadabilityPreview.Preset=preset;
    }

    [TestCase(false,true,CombatReadabilityPreset.Local,false,3,.3f)]
    [TestCase(true,false,CombatReadabilityPreset.Local,false,3,.3f)]
    [TestCase(true,true,CombatReadabilityPreset.Current,false,3,.3f)]
    [TestCase(true,true,CombatReadabilityPreset.Compact,false,3,.3f)]
    [TestCase(true,true,CombatReadabilityPreset.Local,true,3,.3f)]
    [TestCase(true,true,CombatReadabilityPreset.Local,false,1,.18f)]
    public void ActualLeapKeepsItsAuthoredTrailsExceptInTheOrdinaryLocalPreview(
        bool test,bool selected,CombatReadabilityPreset mode,bool transcended,int ghosts,float alpha)
    {
        Mode(test,selected,mode);Pair(out object source,out object target);
        Set(source,"augmentTranscended",transcended);Approach(source,target);
        Assert.That(Get(source,"leapAfterimages"),Is.EqualTo(ghosts));
        var afterimages=Object.FindObjectsOfType<SpriteRenderer>().Where(r=>r.name=="Raptor Leap Afterimage"&&!existing.Contains(r.gameObject)).ToArray();
        Assert.That(afterimages.Length,Is.EqualTo(ghosts));
        foreach(var ghost in afterimages)
        {
            Assert.That(ghost.color.a,Is.EqualTo(alpha));
            Assert.That(ghost.GetComponent<Rank3FxLifetime>().duration,Is.EqualTo(.09f));
        }
    }

    [TestCase(false,true,CombatReadabilityPreset.Local,false,3)]
    [TestCase(true,false,CombatReadabilityPreset.Local,false,3)]
    [TestCase(true,true,CombatReadabilityPreset.Current,false,3)]
    [TestCase(true,true,CombatReadabilityPreset.Compact,false,3)]
    [TestCase(true,true,CombatReadabilityPreset.Local,true,3)]
    [TestCase(true,true,CombatReadabilityPreset.Local,false,2)]
    public void PiercingKeepsBothGroundLayersAndItsSweepWithFewerLocalMarks(
        bool test,bool selected,CombatReadabilityPreset mode,bool transcended,int marks)
    {
        Mode(test,selected,mode);Pair(out object source,out object target);Set(source,"augmentTranscended",transcended);
        ((List<string>)Get(source,"activeTraitIds")).Add(RaptorCombatRules.PiercingHuntTraitId);
        var before=new HashSet<PixelCombatEffect>(Object.FindObjectsOfType<PixelCombatEffect>());
        Approach(source,target);
        Assert.That(Get(source,"leapGroundScuffs"),Is.EqualTo(marks));
        var ground=Object.FindObjectsOfType<PixelCombatEffect>().Where(e=>!before.Contains(e)&&
            (e.name=="Raptor Ground Gouge"||e.name=="Raptor Ground Scuff")).ToArray();
        Assert.That(ground.Length,Is.EqualTo(marks*2));
        foreach(var effect in ground)
        {
            Assert.That(effect.transform.position.y,Is.InRange(.03f,.05f));
            Assert.That(GetField<Sprite[]>(effect,"frames").Length,Is.EqualTo(6));
            Assert.That(GetField<float>(effect,"framesPerSecond"),Is.EqualTo(24f));
        }
    }

    [TestCase(-1,false,1f)] [TestCase(0,false,.85f)] [TestCase(1,false,.85f)] [TestCase(2,false,1f)]
    [TestCase(0,true,1f)] [TestCase(1,true,1f)]
    public void EarlyClawsUseLessSpaceWhileSingleHitsFinalHitsAndRankFourKeepTheirImpact(int strike,bool transcended,float ratio)
    {
        Pair(out object source,out object target);Set(source,"augmentTranscended",transcended);
        Mode(true,true,CombatReadabilityPreset.Current);
        RenderPoses(source,target);
        float hp=(float)Get(target,"hp");var original=Hit(source,target,strike);
        float damage=hp-(float)Get(target,"hp");Vector3 size=original.transform.localScale;
        Mode(true,true,CombatReadabilityPreset.Local);
        RenderPoses(source,target);
        hp=(float)Get(target,"hp");var refined=Hit(source,target,strike);
        Assert.That(Vector3.Distance(refined.transform.localScale,size*ratio*1.215f),Is.LessThan(.00001f));
        Assert.That(hp-(float)Get(target,"hp"),Is.EqualTo(damage).Within(.02f));
        Assert.That(GetField<Sprite[]>(refined,"frames").Length,Is.EqualTo(6));
        Assert.That(GetField<float>(refined,"framesPerSecond"),Is.EqualTo(60f));
    }

    [Test] public void LocalPreviewKeepsTheExistingThreeHitDamageSlideFacingAndReturnChecks()
    {
        fixture.ThreeActualSkillHitsKeepTheirDamageAndSlideTheVictimWithoutLosingTheComboOrReturn("velos",0);
    }

    [Test] public void LessTrailDoesNotChangeTheApproachTrajectory()
    {
        Pair(out object source,out object target);Vector3 start=Model(source).transform.position;
        Mode(true,true,CombatReadabilityPreset.Current);Call("StartAssassinLeap",source,target,1f);
        var trajectory=new List<Vector3>();
        for(int i=0;i<25;i++){Call("UpdateAssassinLeap",source,.005f);trajectory.Add(Model(source).transform.position);}
        Model(source).transform.position=start;
        Mode(true,true,CombatReadabilityPreset.Local);Call("StartAssassinLeap",source,target,1f);
        for(int i=0;i<trajectory.Count;i++)
        {
            Call("UpdateAssassinLeap",source,.005f);
            Assert.That(Vector3.Distance(Model(source).transform.position,trajectory[i]),Is.LessThan(.00001f));
        }
    }

    private void Pair(out object source,out object target)
    {
        var actor=typeof(RaptorRelentlessPresentationTests).GetMethod("Actor",Hidden);
        source=actor.Invoke(fixture,new object[]{"velos",0,Vector3.left*3f});
        target=actor.Invoke(fixture,new object[]{"velos",1,Vector3.zero});
        Set(source,"target",target);Set(target,"target",source);
    }
    private void Approach(object source,object target)
    {
        Assert.That(Call("StartAssassinLeap",source,target,1f),Is.True);
        int steps=0;
        while((float)Get(source,"leapProgress")<.85f&&steps++<100)Call("UpdateAssassinLeap",source,.005f);
        Assert.That(steps,Is.LessThan(100));
    }
    private PixelCombatEffect Hit(object source,object target,int strike)
    {
        var before=new HashSet<PixelCombatEffect>(Object.FindObjectsOfType<PixelCombatEffect>());
        Call("ApplyRaptorLeapHit",source,target,1f,strike);
        return Object.FindObjectsOfType<PixelCombatEffect>().Single(e=>e.name=="Raptor Contact Claws"&&!before.Contains(e));
    }
    private static void Mode(bool test,bool selected,CombatReadabilityPreset mode)
    {CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=selected;CombatReadabilityPreview.Preset=mode;}
    private static void RenderPoses(params object[] units)
    {
        foreach(object unit in units)
        {
            var driver=(SpriteMonsterAnimationDriver)Get(unit,"spriteAnimation");
            if(driver!=null)typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Hidden).Invoke(driver,null);
        }
    }
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Hidden).Invoke(battle,args);
    private static object Get(object unit,string name)=>unit.GetType().GetField(name).GetValue(unit);
    private static void Set(object unit,string name,object value)=>unit.GetType().GetField(name).SetValue(unit,value);
    private static GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private static T GetField<T>(object target,string name)=>(T)target.GetType().GetField(name,Hidden).GetValue(target);
}
