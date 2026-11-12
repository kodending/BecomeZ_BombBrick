using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class WolfPackRushTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly Type type=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object source,target;
    private IEnumerator cast;
    private readonly Vector3 landing=Vector3.right*4.2f;

    [SetUp] public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Pack rush test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(new Vector3(6,0,0),new Vector3(20,2,16)));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        source=Unit(0,Vector3.zero);target=Unit(1,Vector3.right*3f);
        Set(source,"wolfHuntDirection",Vector3.right);
        ((IList)Get(source,"activeTraitIds")).Add(WolfCombatRules.NukguTraitId);
    }

    [TearDown] public void TearDown()
    {
        (cast as IDisposable)?.Dispose();
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)
                Object.DestroyImmediate(go);
    }

    private object Unit(int team,Vector3 position)
    {
        object u=Activator.CreateInstance(unitType,true);
        var units=(IList)type.GetField("units",Private).GetValue(battle);
        var model=new GameObject("Rush unit");model.transform.position=position;
        Set(u,"id",units.Count);Set(u,"stableId","rush-"+units.Count);
        Set(u,"model",model);Set(u,"ring",new GameObject("Rush ring"));
        Set(u,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(u,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_nukgu"));
        Set(u,"skillId",WolfCombatRules.SkillId);Set(u,"alive",true);
        Set(u,"hp",5000f);Set(u,"maxHp",5000f);Set(u,"damage",100f);
        Set(u,"attackInterval",1f);Set(u,"collisionRadius",.3f);
        units.Add(u);return u;
    }
    private void Set(object u,string field,object value)=>unitType.GetField(field).SetValue(u,value);
    private object Get(object u,string field)=>unitType.GetField(field).GetValue(u);
    private float Hp(object u)=>(float)Get(u,"hp");
    private CombatStatusState Status(object u)=>(CombatStatusState)Get(u,"combatStatuses");
    private void StartCast(bool frost=false,bool crimson=false,bool moon=false)
    {
        cast=(IEnumerator)type.GetMethod("ResolveWolfPackRush",Private).Invoke(battle,new[]{
            source,target,landing,GameMasterData.Skill(WolfCombatRules.SkillId),1f,frost,crimson,moon,0,
            type.GetField("combatGeneration",Private).GetValue(battle)});
    }
    private IEnumerator Finish()
    {
        int frames=0;
        while(cast.MoveNext()){Assert.That(++frames,Is.LessThan(300));yield return null;}
    }

    [UnityTest] public IEnumerator WaveHitsThreeLanesInOrderOnceAndBodyStopsNearTarget()
    {
        var upper=Unit(1,new Vector3(6,0,1.7f));var lower=Unit(1,new Vector3(9,0,-1.7f));
        var large=Unit(1,new Vector3(12,0,0));Set(large,"collisionRadius",1.2f);
        var ally=Unit(0,new Vector3(5,0,0));var outside=Unit(1,new Vector3(6,0,4.5f));
        var behind=Unit(1,new Vector3(-2,0,0));StartCast();bool staggered=false;
        int frames=0;
        while(cast.MoveNext())
        {
            Assert.That(++frames,Is.LessThan(300));
            if(Hp(target)<5000&&Hp(large)==5000)staggered=true;
            yield return null;
        }
        Assert.That(staggered,Is.True);
        foreach(var enemy in new[]{target,upper,lower,large})
        {
            Assert.That(Hp(enemy),Is.EqualTo(4760f).Within(.01f));
            Assert.That(Status(enemy).Has(CombatStatusKind.Knockdown),Is.False);
        }
        foreach(var safe in new[]{source,ally,outside,behind})Assert.That(Hp(safe),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(4));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(960f).Within(.01f));
        Assert.That(((GameObject)Get(source,"model")).transform.position,Is.EqualTo(landing));
        Assert.That(Get(source,"wolfChaseTarget"),Is.SameAs(target));
    }

    [UnityTest] public IEnumerator ReleasedRushContinuesAfterPrimaryTargetDies()
    {
        var far=Unit(1,new Vector3(12,0,1.7f));Set(target,"alive",false);Set(target,"hp",0f);
        StartCast();yield return Finish();Assert.That(Hp(far),Is.EqualTo(4760f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator InterruptStopsPendingHitsAndRecordsOnlyExistingDamage()
    {
        var far=Unit(1,new Vector3(14,0,0));StartCast();int frames=0;
        while(Hp(target)==5000f)
        {
            Assert.That(++frames,Is.LessThan(300));Assert.That(cast.MoveNext(),Is.True);yield return null;
        }
        Set(source,"positionRevision",1);yield return Finish();
        Assert.That(Hp(far),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(240f).Within(.01f));
    }

    [UnityTest] public IEnumerator FrostAddsOneStackPerEnemyWithoutInstantFreeze()
    {
        var far=Unit(1,new Vector3(12,0,0));StartCast(frost:true);yield return Finish();
        foreach(var enemy in new[]{target,far})
        {
            Assert.That(Status(enemy).Get(CombatStatusKind.ComboChilled).stacks,Is.EqualTo(1));
            Assert.That(Status(enemy).Has(CombatStatusKind.Freeze),Is.False);
        }
    }

    [UnityTest] public IEnumerator MoonShieldDoesNotMultiplyWithVictimCount()
    {
        Unit(1,new Vector3(8,0,1.7f));Unit(1,new Vector3(12,0,-1.7f));
        StartCast(moon:true);yield return Finish();
        Assert.That(Status(source).Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(400f).Within(.01f));
    }

    [UnityTest] public IEnumerator CrimsonAddsMissingHealthDamageOnce()
    {
        Set(target,"hp",4500f);StartCast(crimson:true);yield return Finish();
        Assert.That(Hp(target),Is.EqualTo(4220f).Within(.01f));
    }

    [TestCase(1,0)] [TestCase(-1,0)] [TestCase(0,1)] [TestCase(0,-1)] [TestCase(1,1)]
    public void SweepEndsAtTheArenaBoundaryAndRejectsPassedOrRearPositions(float x,float z)
    {
        Vector3 forward=new Vector3(x,0,z).normalized,side=Vector3.Cross(Vector3.up,forward);
        var bounds=new Bounds(Vector3.zero,new Vector3(20,2,20));
        float distance=WolfCombatRules.RushDistanceToBoundary(Vector3.zero,forward,bounds);
        Vector3 end=forward*distance;
        Assert.That(Mathf.Max(Mathf.Abs(end.x),Mathf.Abs(end.z)),Is.EqualTo(10f).Within(.001f));
        Assert.That(WolfCombatRules.IsInsideRushSweep(forward*5+side*2.5f,Vector3.zero,forward,4,6,1.7f,0),Is.True);
        Assert.That(WolfCombatRules.IsInsideRushSweep(forward*5+side*2.6f,Vector3.zero,forward,4,6,1.7f,0),Is.False);
        Assert.That(WolfCombatRules.IsInsideRushSweep(-forward,Vector3.zero,forward,0,6,1.7f,0),Is.False);
        Assert.That(WolfCombatRules.IsInsideRushSweep(forward,Vector3.zero,forward,4,6,1.7f,0),Is.False);
    }

    [TestCase(1f)] [TestCase(-1f)]
    public void RushIdleKeepsHeadingThenScatters(float direction)
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/029_nukgu"));
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var renderer=driver.GetComponent<SpriteRenderer>();Vector3 heading=Vector3.right*direction;
        var ghost=PixelCombatEffect.PlayWolfHuntAfterimage(renderer,Vector3.zero,heading,Color.cyan,.18f);
        var effect=ghost.GetComponent<PixelWolfHuntAfterimage>();
        effect.PrepareAmbush(renderer,()=>true,driver.GetSkillAttackFrame(2),driver);
        effect.LaunchRush(heading);bool facing=ghost.flipX;
        ghost.transform.position=heading*10f;effect.FinishRush();
        var animate=typeof(PixelWolfHuntAfterimage).GetMethod("AnimateAmbush",Private);
        animate.Invoke(effect,new object[]{.1f});
        Assert.That(ghost.sprite,Is.SameAs(driver.GetIdleFrame(.1f)));
        Assert.That(ghost.flipX,Is.EqualTo(facing));
        Assert.That(ghost.GetComponentInChildren<ParticleSystem>(),Is.Null);
        animate.Invoke(effect,new object[]{.2f});
        Assert.That(ghost.GetComponentInChildren<ParticleSystem>().particleCount,Is.EqualTo(24));
        Assert.That(ghost.transform.position,Is.EqualTo(heading*10f));
        animate.Invoke(effect,new object[]{.6f});Assert.That(effect==null,Is.True);
    }
}
