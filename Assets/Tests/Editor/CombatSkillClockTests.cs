using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CombatSkillClockTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject ownerHost,otherHost,childHost,battleHost;
    private CombatSkillClock clock,otherClock;
    private float previousSpeed;

    [SetUp]
    public void SetUp()
    {
        previousSpeed=Time.timeScale;
        Assert.That(CombatSkillClock.Active,Is.False,"A previous presentation must release the exclusive clock.");
        ownerHost=new GameObject("Clock owner");otherHost=new GameObject("Other clock owner");
        childHost=new GameObject("Owner child");childHost.transform.SetParent(ownerHost.transform);
        clock=new CombatSkillClock(ownerHost.transform);otherClock=new CombatSkillClock(otherHost.transform);
        Time.timeScale=0f;
    }

    [TearDown]
    public void TearDown()
    {
        clock.End();otherClock.End();
        Object.DestroyImmediate(ownerHost);Object.DestroyImmediate(otherHost);Object.DestroyImmediate(battleHost);
        Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Capture(),Is.Null);
        Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void HoldFreezesEveryoneAndExecutionOnlyReleasesTheOwnersBodyAndChildren()
    {
        clock.Hold();
        Assert.That(CombatSkillClock.ActorFrozen(ownerHost.transform),Is.True);
        Assert.That(CombatSkillClock.ActorFrozen(childHost.transform),Is.True);
        Assert.That(CombatSkillClock.ActorFrozen(otherHost.transform),Is.True);
        BeginExecution(clock,.1f);
        Assert.That(CombatSkillClock.ActorFrozen(ownerHost.transform),Is.False);
        Assert.That(CombatSkillClock.ActorFrozen(childHost.transform),Is.False);
        Assert.That(CombatSkillClock.ActorFrozen(otherHost.transform),Is.True);
        Assert.That(CombatSkillClock.ActorDelta(ownerHost.transform),Is.EqualTo(.1f));
        Assert.That(otherClock.DeltaTime,Is.Zero);
        Assert.That(Time.timeScale,Is.Zero);
        clock.End();
        Assert.That(CombatSkillClock.ActorFrozen(otherHost.transform),Is.False);
        Assert.That(clock.Executing,Is.False);
    }

    [Test]
    public void NestedAndExceptionalCallbackScopesRestoreThePreviousOwner()
    {
        using(CombatSkillClock.Enter(clock))
        {
            Assert.That(CombatSkillClock.Capture(),Is.SameAs(clock));
            try
            {
                using(CombatSkillClock.Enter(otherClock))
                {Assert.That(CombatSkillClock.Capture(),Is.SameAs(otherClock));throw new InvalidOperationException();}
            }
            catch(InvalidOperationException){}
            Assert.That(CombatSkillClock.Capture(),Is.SameAs(clock));
        }
        Assert.That(CombatSkillClock.Capture(),Is.Null);
    }

    [Test]
    public void OldAndOtherOwnersRoutinesCannotEvenEnterMoveNextDuringTheSoloExecution()
    {
        int owned=0,old=0,other=0;
        var ownRun=CombatSkillClock.Run(Once(()=>owned++),clock);
        var oldRun=CombatSkillClock.Run(Once(()=>old++),null);
        var otherRun=CombatSkillClock.Run(Once(()=>other++),otherClock);
        clock.Hold();
        Assert.That(ownRun.MoveNext(),Is.True);Assert.That(owned,Is.Zero);
        BeginExecution(clock,.1f);
        Assert.That(ownRun.MoveNext(),Is.True);Assert.That(owned,Is.EqualTo(1));
        for(int i=0;i<3;i++){Assert.That(oldRun.MoveNext(),Is.True);Assert.That(otherRun.MoveNext(),Is.True);}
        Assert.That(old,Is.Zero);Assert.That(other,Is.Zero);
        (ownRun as IDisposable)?.Dispose();(oldRun as IDisposable)?.Dispose();(otherRun as IDisposable)?.Dispose();
    }

    [Test]
    public void NestedWaitForSecondsUsesTheOwnedClockAndCallbacksInheritItsScope()
    {
        var steps=new List<string>();var captures=new List<CombatSkillClock>();
        BeginExecution(clock,.1f);
        float origin=clock.Time;
        IEnumerator run=CombatSkillClock.Run(Outer(steps,captures),clock);
        Assert.That(run.MoveNext(),Is.True);
        Assert.That(steps,Is.EqualTo(new[]{"outer","child"}));
        Set(clock,"credit",clock.Time-origin+.2f);
        Assert.That(run.MoveNext(),Is.True);Assert.That(steps.Count,Is.EqualTo(2));
        Set(clock,"credit",.31f);
        Assert.That(run.MoveNext(),Is.True);Assert.That(steps[2],Is.EqualTo("after wait"));
        Assert.That(run.MoveNext(),Is.False);Assert.That(steps[3],Is.EqualTo("outer complete"));
        foreach(var captured in captures)Assert.That(captured,Is.SameAs(clock));
        Assert.That(CombatSkillClock.Capture(),Is.Null);
    }

    [Test]
    public void DisposingAStoppedNestedRoutineDisposesBothItsChildAndParent()
    {
        bool childDisposed=false,parentDisposed=false;
        BeginExecution(clock,.1f);
        IEnumerator run=CombatSkillClock.Run(WithFinally(WithFinally(Once(()=>{}),()=>childDisposed=true),()=>parentDisposed=true),clock);
        Assert.That(run.MoveNext(),Is.True);
        clock.Hold();Assert.That(run.MoveNext(),Is.True);
        (run as IDisposable).Dispose();
        Assert.That(childDisposed,Is.True);Assert.That(parentDisposed,Is.True);
        Assert.That(CombatSkillClock.Capture(),Is.Null);
    }

    [Test]
    public void AProjectileCapturesOnlyItsMatchingOwnerScopeEvenForTwoCastersOnTheSameTeam()
    {
        battleHost=new GameObject("Inactive flight clock test");battleHost.SetActive(false);
        var battle=battleHost.AddComponent<MonsterAutoBattlePrototype>();Set(battle,"battleElapsed",12f);
        foreach(var host in new[]{ownerHost,otherHost})
        {var source=host.AddComponent<CombatProjectileSource>();source.battle=battle;source.team=0;}
        BeginExecution(clock,.1f);
        CombatProjectileSource.Flight own,other;
        using(CombatSkillClock.Enter(clock))
        {own=CombatProjectileSource.Capture(ownerHost.transform);other=CombatProjectileSource.Capture(otherHost.transform);}
        Assert.That(own.skillClock,Is.SameAs(clock));Assert.That(other.skillClock,Is.Null);
        float start=own.clock();Set(clock,"credit",.6f);
        Assert.That(own.clock(),Is.EqualTo(clock.Time));Assert.That(own.clock(),Is.GreaterThan(start+.3f));
        Assert.That(other.clock(),Is.EqualTo(12f));
        clock.End();Assert.That(own.skillClock,Is.SameAs(clock),"A released projectile retains its cast clock after the solo ends.");
        own.finished();other.finished();
    }

    [Test]
    public void EndingOrReplacingTheOwnerRestoresTrackedParticlesAndReleasesTheFreeze()
    {
        var system=ownerHost.AddComponent<ParticleSystem>();var main=system.main;
        main.useUnscaledTime=false;main.simulationSpeed=2.5f;
        clock.BeginExecution(2f);
        using(CombatSkillClock.Enter(clock)){CombatSkillClock.TrackParticle(system);CombatSkillClock.TrackParticle(system);}
        Assert.That(main.useUnscaledTime,Is.True);Assert.That(main.simulationSpeed,Is.EqualTo(5f));
        otherClock.Hold();
        Assert.That(main.useUnscaledTime,Is.False);Assert.That(main.simulationSpeed,Is.EqualTo(2.5f));
        Assert.That(clock.Executing,Is.False);Assert.That(CombatSkillClock.Active,Is.True);
        otherClock.End();clock.End();Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void AnExecutingClockAccumulatesRealTimeOnlyOncePerFrame()
    {
        clock.BeginExecution(2f);Set(clock,"steppedFrame",-1);
        float expected=Time.unscaledDeltaTime*2f;
        clock.Advance();float after=clock.Time;
        Assert.That((float)typeof(CombatSkillClock).GetField("credit",Private).GetValue(clock),Is.EqualTo(expected).Within(.00001f));
        clock.Advance();Assert.That(clock.Time,Is.EqualTo(after));
        clock.End();Assert.That(clock.Time,Is.EqualTo(after));
    }

    private static IEnumerator Once(Action callback){callback();yield return null;}
    private static IEnumerator WithFinally(IEnumerator nested,Action disposed)
    {try{yield return nested;}finally{disposed();}}
    private static IEnumerator Outer(List<string> steps,List<CombatSkillClock> captures)
    {
        steps.Add("outer");captures.Add(CombatSkillClock.Capture());
        yield return Child(steps,captures);
        steps.Add("outer complete");captures.Add(CombatSkillClock.Capture());
    }
    private static IEnumerator Child(List<string> steps,List<CombatSkillClock> captures)
    {
        steps.Add("child");captures.Add(CombatSkillClock.Capture());yield return new WaitForSeconds(.3f);
        steps.Add("after wait");captures.Add(CombatSkillClock.Capture());yield return null;
    }
    private static void BeginExecution(CombatSkillClock value,float delta)
    {value.BeginExecution(1f);Set(value,"delta",delta);}
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Private).SetValue(value,data);
}
