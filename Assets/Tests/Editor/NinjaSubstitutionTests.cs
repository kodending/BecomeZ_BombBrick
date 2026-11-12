using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class NinjaSubstitutionTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private HashSet<GameObject> existing;
    [SetUp] public void SetUp()=>existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
    [TearDown] public void TearDown()
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
    }
    private static SpriteMonsterAnimationDriver Driver(string id="037_chundongi")=>
        Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+id))
            .GetComponentInChildren<SpriteMonsterAnimationDriver>();
    private static void Step(SpriteMonsterAnimationDriver driver,float dt)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }

    [TestCase("037_chundongi")]
    [TestCase("038_seolmyo")]
    [TestCase("039_sundongi")]
    [TestCase("040_hongryeon")]
    public void VanishThenLogThenArrivalThenOneHitUsesAllSixFramesAndConfiguredSize(string id)
    {
        var driver=Driver(id);var renderer=driver.GetComponent<SpriteRenderer>();
        var events=new List<string>();var frames=new HashSet<int>();
        float scale=(float)typeof(SpriteMonsterAnimationDriver).GetMethod("ScaleFor",Private)
            .Invoke(driver,new object[]{"SkillAttack"});
        driver.PlayNinjaShadowAttack(()=>true,()=>events.Add("vanish"),()=>events.Add("log"),()=>events.Add("arrive"),()=>events.Add("hit"));
        for(int i=0;i<75;i++)
        {
            float t=(i+1)/120f;Step(driver,1f/120f);
            if(driver.CurrentMotionName=="SkillAttack")
            {
                frames.Add(driver.CurrentFrameIndex);
                Assert.That(driver.transform.localScale,Is.EqualTo(Vector3.one*scale));
            }
            if(t>.11f&&t<.31f)Assert.That(renderer.forceRenderingOff,Is.True);
            if(t>.11f&&t<.175f)CollectionAssert.AreEqual(new[]{"vanish"},events);
            if(t>.19f&&t<.31f)CollectionAssert.AreEqual(new[]{"vanish","log"},events);
            if(t>.33f)Assert.That(renderer.forceRenderingOff,Is.False);
            if(t>.33f&&t<.395f)CollectionAssert.AreEqual(new[]{"vanish","log","arrive"},events);
            if(t<.395f)Assert.That(events,Does.Not.Contain("hit"));
            if(t>.41f&&t<.425f)Assert.That(driver.CurrentFrameIndex,Is.EqualTo(driver.SkillAttackHitFrameIndex));
        }
        CollectionAssert.AreEqual(new[]{"vanish","log","arrive","hit"},events);
        Assert.That(frames.Count,Is.EqualTo(6));Assert.That(driver.IsActionLocked,Is.False);
    }

    [Test] public void SlowFramesStillResolveSubstitutionArrivalAndContactInOrderOnce()
    {
        var d=Driver();var events=new List<int>();
        d.PlayNinjaShadowAttack(()=>true,()=>events.Add(1),()=>events.Add(2),()=>events.Add(3),()=>events.Add(4));
        Step(d,.7f);Step(d,.7f);CollectionAssert.AreEqual(new[]{1,2,3,4},events);
        Assert.That(d.GetComponent<SpriteRenderer>().forceRenderingOff,Is.False);
    }

    [TestCase("interrupt")]
    [TestCase("reset")]
    [TestCase("death")]
    [TestCase("disable")]
    [TestCase("target_lost")]
    public void CancellationDuringSubstitutionRestoresVisibilityWithoutDamage(string reason)
    {
        var d=Driver();bool valid=true;int hit=0,arrival=0;
        d.PlayNinjaShadowAttack(()=>valid,()=>{},()=>{},()=>arrival++,()=>hit++);Step(d,.12f);
        Assert.That(d.GetComponent<SpriteRenderer>().forceRenderingOff,Is.True);
        if(reason=="interrupt")d.InterruptCurrentAction();
        if(reason=="reset")d.ResetForReuse();
        if(reason=="death")d.PlayDeath();
        if(reason=="disable")
        {
            d.enabled=false;
            // EditMode does not dispatch MonoBehaviour lifecycle events on these prefabs.
            typeof(SpriteMonsterAnimationDriver).GetMethod("OnDisable",Private).Invoke(d,null);
        }
        if(reason=="target_lost")valid=false;
        Step(d,.5f);Assert.That(hit,Is.Zero);Assert.That(arrival,Is.Zero);
        Assert.That(d.GetComponent<SpriteRenderer>().forceRenderingOff,Is.False);
    }

    [Test] public void FreezeKeepsTheExactFrameAndPausesBothMovementAndDamage()
    {
        var d=Driver();int arrival=0,hit=0;
        d.PlayNinjaShadowAttack(()=>true,()=>{},()=>{},()=>arrival++,()=>hit++);Step(d,.12f);
        d.SetFrozen(true);var frame=d.GetComponent<SpriteRenderer>().sprite;
        Step(d,2f);Assert.That(arrival,Is.Zero);Assert.That(hit,Is.Zero);
        Assert.That(d.GetComponent<SpriteRenderer>().sprite,Is.SameAs(frame));
        Assert.That(d.GetComponent<SpriteRenderer>().forceRenderingOff,Is.False);
        d.SetFrozen(false);Step(d,.30f);Assert.That(arrival,Is.EqualTo(1));Assert.That(hit,Is.EqualTo(1));
    }

    [Test] public void LogStaysAtDepartureHasNoColliderAndExpires()
    {
        var source=Driver().GetComponent<SpriteRenderer>();Step(source.GetComponent<SpriteMonsterAnimationDriver>(),0f);
        var log=PixelNinjaSubstitution.PlayLog(source,()=>true);Vector3 origin=log.transform.position;
        source.transform.position+=Vector3.right*5f;
        var advance=typeof(PixelNinjaSubstitution).GetMethod("Animate",Private);
        advance.Invoke(log,new object[]{.3f});Assert.That(log.transform.position,Is.EqualTo(origin));
        Assert.That(log.GetComponentInChildren<Collider>(),Is.Null);
        var sprite=log.transform.Find("Rope-bound Log").GetComponent<SpriteRenderer>().sprite;
        Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(48,48)));
        advance.Invoke(log,new object[]{.8f});Assert.That(log==null,Is.True);
    }

    [TestCase("037_chundongi")]
    [TestCase("038_seolmyo")]
    [TestCase("039_sundongi")]
    [TestCase("040_hongryeon")]
    public void CrossMovesBeforeEachOfFiveRealHitFramesAndReleasesTheAction(string id)
    {
        var d=Driver(id);var events=new List<string>();int finished=0;
        d.PlayNinjaCrossAttack(()=>true,()=>events.Add("vanish"),()=>events.Add("log"),
            i=>events.Add("move"+i),i=>
            {
                Assert.That(d.CurrentFrameIndex,Is.EqualTo(d.SkillAttackHitFrameIndex));
                Assert.That(d.GetComponent<SpriteRenderer>().forceRenderingOff,Is.False);
                events.Add("hit"+i);
            },()=>finished++);
        for(int i=0;i<120;i++)Step(d,1f/120f);
        CollectionAssert.AreEqual(new[]{"vanish","log","move0","hit0","move1","hit1",
            "move2","hit2","move3","hit3","move4","hit4"},events);
        Assert.That(finished,Is.EqualTo(1));Assert.That(d.IsActionLocked,Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CrossSlowFrameStillStopsImmediatelyWhenTheTargetDies(bool killAfterFirstHit)
    {
        var d=Driver();int moves=0,hits=0,finished=0;bool valid=true,protectedNow=false;
        d.PlayNinjaCrossAttack(()=>valid,()=>protectedNow=true,()=>{},i=>moves++,i=>
            {hits++;if(killAfterFirstHit)valid=false;},()=>{finished++;protectedNow=false;});
        Step(d,1f);Step(d,1f);
        Assert.That(hits,Is.EqualTo(killAfterFirstHit?1:5));Assert.That(moves,Is.EqualTo(hits));
        Assert.That(finished,Is.EqualTo(1));Assert.That(protectedNow,Is.False);
        Assert.That(d.GetComponent<SpriteRenderer>().forceRenderingOff,Is.False);
        Assert.That(d.IsActionLocked,Is.False);
    }

    [TestCase("death")]
    [TestCase("interrupt")]
    [TestCase("reset")]
    [TestCase("disable")]
    [TestCase("frozen_target_lost")]
    public void CrossCancellationAlwaysReleasesProtectionAndPendingHits(string reason)
    {
        var d=Driver();bool valid=true,protectedNow=false;int hits=0,finished=0;
        d.PlayNinjaCrossAttack(()=>valid,()=>protectedNow=true,()=>{},i=>{},i=>hits++,
            ()=>{protectedNow=false;finished++;});Step(d,.12f);
        Assert.That(protectedNow,Is.True);
        if(reason=="death")d.PlayDeath();
        if(reason=="interrupt")d.InterruptCurrentAction();
        if(reason=="reset")d.ResetForReuse();
        if(reason=="disable")typeof(SpriteMonsterAnimationDriver).GetMethod("OnDisable",Private).Invoke(d,null);
        if(reason=="frozen_target_lost"){d.SetFrozen(true);valid=false;}
        Step(d,2f);Assert.That(hits,Is.Zero);Assert.That(finished,Is.EqualTo(1));
        Assert.That(protectedNow,Is.False);Assert.That(d.GetComponent<SpriteRenderer>().forceRenderingOff,Is.False);
    }

    [Test] public void CrossKeepsItsDamageAndOnlyHitsEnemiesInsideEachCutPath()
    {
        var host=new GameObject("Cross damage test");host.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();var type=typeof(MonsterAutoBattlePrototype);
        Type ut=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var units=(System.Collections.IList)type.GetField("units",Private).GetValue(battle);
        Func<int,Vector3,object> make=(team,position)=>
        {
            object u=Activator.CreateInstance(ut,true);var go=new GameObject("Cross actor");go.transform.position=position;
            ut.GetField("model").SetValue(u,go);ut.GetField("alive").SetValue(u,true);
            ut.GetField("hp").SetValue(u,10000f);ut.GetField("maxHp").SetValue(u,10000f);
            ut.GetField("damage").SetValue(u,100f);
            var field=ut.GetField("team");field.SetValue(u,Enum.ToObject(field.FieldType,team));units.Add(u);return u;
        };
        float tile=MonsterAutoBattlePrototype.TileSize;
        object source=make(0,Vector3.forward*tile*2f),target=make(1,Vector3.zero),
            side=make(1,Vector3.forward*tile*1.8f),ally=make(0,Vector3.forward*tile*1.8f),
            outside=make(1,Vector3.right*tile*4f);
        ut.GetField("ninjaCrossActive").SetValue(source,true);
        Assert.That(type.GetMethod("IsCombatTargetable",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new[]{source}),Is.EqualTo(false));
        var hit=type.GetMethod("HitNinjaCross",Private);
        for(int i=0;i<5;i++)
        {
            ((GameObject)ut.GetField("model").GetValue(source)).transform.position=
                Quaternion.AngleAxis(i*90f,Vector3.up)*Vector3.forward*tile*2f;
            hit.Invoke(battle,new[]{source,target,(object)i,1f});
        }
        Assert.That((float)ut.GetField("hp").GetValue(target),Is.EqualTo(9466.4f).Within(.01f));
        Assert.That((float)ut.GetField("hp").GetValue(side),Is.EqualTo(9950f).Within(.01f));
        foreach(var u in new[]{ally,outside})Assert.That(ut.GetField("hp").GetValue(u),Is.EqualTo(10000f));
        Assert.That(ut.GetField("ninjaCrossActive").GetValue(source),Is.EqualTo(false));
        Assert.That(ut.GetField("ninjaEmpoweredBasicAttacks").GetValue(source),Is.EqualTo(2));
    }

    [TestCase(false)] [TestCase(true)]
    public void OpeningSecondHitCancelsOnFreezeOrReplacementCast(bool recast)
    {
        var host=new GameObject("Ninja follow-up test");host.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();var type=typeof(MonsterAutoBattlePrototype);
        Type unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        object source=Activator.CreateInstance(unitType,true),target=Activator.CreateInstance(unitType,true);
        foreach(object unit in new[]{source,target})
        {
            unitType.GetField("alive").SetValue(unit,true);
            unitType.GetField("hp").SetValue(unit,100f);
            unitType.GetField("model").SetValue(unit,new GameObject("Ninja follow-up actor"));
        }
        var statuses=(CombatStatusState)unitType.GetField("combatStatuses").GetValue(source);
        var sequence=(System.Collections.IEnumerator)type.GetMethod("ResolveNinjaOpeningSecondHit",Private)
            .Invoke(battle,new object[]{source,target,100f,false,1f,0,0,0});
        Assert.That(sequence.MoveNext(),Is.True);
        if(recast)unitType.GetField("skillUses").SetValue(source,1);
        else statuses.ApplyTimed(CombatStatusKind.Freeze,2f);
        Assert.That(sequence.MoveNext(),Is.False);
        Assert.That(unitType.GetField("hp").GetValue(target),Is.EqualTo(100f));
    }
}
