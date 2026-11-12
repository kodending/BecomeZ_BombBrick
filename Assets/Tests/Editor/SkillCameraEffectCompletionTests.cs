using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private static void CameraCompletion(object state,Action complete)=>
        state.GetType().GetField("cameraEffectCompleted").SetValue(state,complete);
    private static T CameraState<T>(object state,string name)=>(T)state.GetType().GetField(name).GetValue(state);
    private IList CameraEffects(string name)=>(IList)typeof(MonsterAutoBattlePrototype).GetField(name,Private).GetValue(battle);

    [TestCase("toss")]
    [TestCase("gravity")]
    [TestCase("heavenly")]
    [TestCase("wyvern")]
    public void EffectCreationAcquiresAndReleasesTheCurrentCameraCastsActualHold(string effect)
    {
        var type=typeof(MonsterAutoBattlePrototype);
        object presentation=Activator.CreateInstance(type.GetNestedType("LocalSkillPresentation",BindingFlags.NonPublic),true);
        presentation.GetType().GetField("unit").SetValue(presentation,source);
        presentation.GetType().GetField("active").SetValue(presentation,true);
        CameraEffects("localSkillPresentations").Add(presentation);
        object focus=Activator.CreateInstance(type.GetNestedType("SkillCameraPresentation",BindingFlags.NonPublic),true);
        focus.GetType().GetField("unit").SetValue(focus,source);
        focus.GetType().GetField("presentation").SetValue(focus,presentation);
        type.GetField("skillCameraPresentation",Private).SetValue(battle,focus);
        object state;
        if(effect=="toss")state=StartToss();
        else if(effect=="gravity"){StartBeetleGravity();state=CameraEffects("beetleGravityFields")[0];}
        else if(effect=="heavenly")
        {
            var skill=HaetaeSetup();Trait(HaetaeRank3Rules.HeavenlyJudgment);
            Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.True);state=Get(source,"haetaeRoar");
        }
        else{WyvernSetup();state=Get(source,"wyvernBreath");}
        Assert.That(CameraState<Action>(state,"cameraEffectCompleted"),Is.Not.Null);
        Assert.That(CameraState<int>(focus,"pendingEffects"),Is.EqualTo(1));
        if(effect=="toss")MammothCall("EndMammothToss",source,state);
        else if(effect=="gravity")MammothCall("EndBeetleGravity",state);
        else if(effect=="heavenly")GriffinCall("CancelHaetaeRoar",state);
        else GriffinCall("ClearWyvernBreaths");
        Assert.That(CameraState<int>(focus,"pendingEffects"),Is.Zero);
    }

    [Test]
    public void MountainTossCameraWaitsForExplosionDamageAfterTheBodyEndsButNotForDebris()
    {
        object toss=StartToss();Model(target).transform.position=Vector3.right*8.5f;
        int completed=0;float impactHp=0f;
        CameraCompletion(toss,()=>{completed++;impactHp=Hp(target);});
        MammothCall("AdvanceMammothToss",source,toss,.8f);
        MammothCall("EndMammothToss",source,toss);
        Assert.That(Get(source,"mammothToss"),Is.Null);Assert.That(completed,Is.Zero);
        MammothCall("AdvanceMammothBoulder",toss,.2f);Assert.That(completed,Is.Zero);
        MammothCall("AdvanceMammothBoulder",toss,.65f);
        Assert.That(completed,Is.EqualTo(1));Assert.That(impactHp,Is.EqualTo(Hp(target)).And.LessThan(5000f));
        Assert.That(CameraState<bool>(toss,"finished"),Is.False,"Debris may continue after the final damage.");
        MammothCall("AdvanceMammothBoulder",toss,1f);MammothCall("ClearMammothBoulders");
        Assert.That(completed,Is.EqualTo(1));
    }

    [TestCase("unreleased")]
    [TestCase("generation")]
    [TestCase("clear")]
    public void MountainTossCameraCompletionIsReleasedOnceOnEveryCancellation(string reason)
    {
        object toss=StartToss();int completed=0;CameraCompletion(toss,()=>completed++);
        if(reason=="unreleased")MammothCall("EndMammothToss",source,toss);
        else if(reason=="generation")
        {
            typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
            MammothCall("AdvanceMammothBoulder",toss,.2f);
        }
        else MammothCall("ClearMammothBoulders");
        Assert.That(completed,Is.EqualTo(1));MammothCall("ClearMammothBoulders");
        Assert.That(completed,Is.EqualTo(1));
    }

    [Test]
    public void GravityCameraCompletesAfterLandingDamageWhileItsVisualTailRemains()
    {
        StartBeetleGravity();object field=CameraEffects("beetleGravityFields")[0];
        int completed=0;float impactHp=0f;CameraCompletion(field,()=>{completed++;impactHp=Hp(target);});
        MammothCall("UpdateBeetleGravityFields",BeetleRank3Rules.GravityLanding-.01f);
        Assert.That(completed,Is.Zero);Assert.That(Hp(target),Is.EqualTo(5000f));
        MammothCall("UpdateBeetleGravityFields",.02f);
        Assert.That(completed,Is.EqualTo(1));Assert.That(impactHp,Is.EqualTo(Hp(target)).And.LessThan(5000f));
        Assert.That(CameraEffects("beetleGravityFields").Count,Is.EqualTo(1));
        MammothCall("EndBeetleGravity",field);MammothCall("EndBeetleGravity",field);
        Assert.That(completed,Is.EqualTo(1));
    }

    [Test]
    public void GravityCameraCancellationReleasesItsHoldAndTheSuspendedVictims()
    {
        StartBeetleGravity();object field=CameraEffects("beetleGravityFields")[0];
        int completed=0;CameraCompletion(field,()=>completed++);
        MammothCall("EndBeetleGravity",field);
        Assert.That(completed,Is.EqualTo(1));Assert.That(Get(target,"beetleGravityHeld"),Is.Null);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(CameraEffects("beetleGravityFields"),Is.Empty);
    }

    [Test]
    public void HeavenlyRoarCameraWaitsForTheLastScheduledStrikeDamageAndNotItsAfterglow()
    {
        var skill=HaetaeSetup();Trait(HaetaeRank3Rules.HeavenlyJudgment);
        Model(target).transform.position=Vector3.right*5.6f;Unit(1,Vector3.right);
        Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.True);
        object roar=Get(source,"haetaeRoar");int completed=0;float impactHp=0f;
        CameraCompletion(roar,()=>{completed++;impactHp=Hp(target);});
        RoarActionTick(HaetaeRank3Rules.HeavenlyWindup+HaetaeCombatRules.WaveDuration+.001f);
        Assert.That(Get(source,"haetaeRoar"),Is.Null,"The body's completed wave must remain independent of the camera hold.");
        Assert.That(((IList)roar.GetType().GetField("strikes").GetValue(roar)).Count,Is.EqualTo(2));
        Assert.That(completed,Is.Zero);RoarActionTick(.25f);Assert.That(completed,Is.Zero);
        RoarActionTick(.2f);
        Assert.That(completed,Is.EqualTo(1));Assert.That(impactHp,Is.EqualTo(Hp(target)).And.LessThan(5000f));
        Assert.That(CameraState<bool>(roar,"cancelled"),Is.False,"The thunder afterglow must not retain the camera.");
        GriffinCall("CancelHaetaeRoar",roar);GriffinCall("CancelHaetaeRoar",roar);
        Assert.That(completed,Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HeavenlyRoarCameraCancellationReleasesUnresolvedStrikesOnce(bool released)
    {
        var skill=HaetaeSetup();Trait(HaetaeRank3Rules.HeavenlyJudgment);
        Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,skill),Is.True);
        object roar=Get(source,"haetaeRoar");int completed=0;CameraCompletion(roar,()=>completed++);
        if(released)RoarActionTick(HaetaeRank3Rules.HeavenlyWindup+HaetaeCombatRules.WaveDuration+.001f);
        Assert.That(completed,Is.Zero);GriffinCall("CancelHaetaeRoar",roar);GriffinCall("CancelHaetaeRoar",roar);
        Assert.That(completed,Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FirstReaperMoltOwnsItsCameraCompletionUntilTheTransformationEndsOrIsCancelled(bool cancel)
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        typeof(MonsterAutoBattlePrototype).GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
        int completed=0;Vector3 scaleAtCompletion=Vector3.zero;
        GriffinCall("ActivateMantisReaperWithCameraCompletion",source,(Action)(()=>{completed++;scaleAtCompletion=Model(source).transform.localScale;}));
        Assert.That(CameraState<int>(Get(source,"mantisReaperAction"),"kind"),Is.Zero);
        GriffinCall("TickMantisReaper",source,CombatActionSeconds(MantisRank3Rules.ReaperMoltEnd-.01f));
        Assert.That(completed,Is.Zero);
        if(cancel)GriffinCall("EndMantisReaperAction",source,false);
        else GriffinCall("TickMantisReaper",source,CombatActionSeconds(.02f));
        Assert.That(completed,Is.EqualTo(1));Assert.That(Get(source,"mantisReaperAction"),Is.Null);
        Assert.That(scaleAtCompletion,Is.EqualTo(Vector3.one*MantisRank3Rules.ReaperScale));
        GriffinCall("EndMantisReaperAction",source,false);Assert.That(completed,Is.EqualTo(1));
    }

    [Test]
    public void InvalidFirstReaperMoltImmediatelyReleasesItsCameraCompletion()
    {
        Set(source,"alive",false);int completed=0;
        GriffinCall("ActivateMantisReaperWithCameraCompletion",source,(Action)(()=>completed++));
        Assert.That(completed,Is.EqualTo(1));Assert.That(Get(source,"mantisReaperAction"),Is.Null);
    }

    [TestCase(null)]
    [TestCase(WyvernRank3Rules.CataclysmBreath)]
    [TestCase(WyvernRank3Rules.WingSweep)]
    [TestCase(WyvernRank3Rules.DragonCore)]
    public void EveryWyvernBreathBranchCompletesTheCameraAtItsLastAttack(string trait)
    {
        if(trait!=null)Trait(trait);WyvernSetup();object breath=Get(source,"wyvernBreath");int completed=0;
        CameraCompletion(breath,()=>completed++);BreathActionTick(.1f);Assert.That(completed,Is.Zero);
        for(int frame=0;frame<600&&!CameraState<bool>(breath,"finished");frame++)BreathTick(1f/120f);
        Assert.That(CameraState<bool>(breath,"finished"),Is.True);Assert.That(completed,Is.EqualTo(1));
        GriffinCall("ClearWyvernBreaths");Assert.That(completed,Is.EqualTo(1));
    }

    [TestCase(WyvernRank3Rules.CataclysmBreath)]
    [TestCase(WyvernRank3Rules.WingSweep)]
    public void WyvernFireFieldContinuesAfterTheCameraHoldCompletes(string trait)
    {
        Trait(trait);WyvernSetup("igvire",WyvernCombatRules.IgvireTraitId);
        object breath=Get(source,"wyvernBreath");int completed=0;CameraCompletion(breath,()=>completed++);
        for(int frame=0;frame<600&&!CameraState<bool>(breath,"finished");frame++)BreathTick(1f/120f);
        Assert.That(completed,Is.EqualTo(1));Assert.That(CameraState<bool>(breath,"fireField"),Is.True);
        BreathTick(.2f);Assert.That(CameraState<bool>(breath,"fireField"),Is.True);
        Assert.That(completed,Is.EqualTo(1));GriffinCall("ClearWyvernBreaths");
    }

    [Test]
    public void InterruptedWyvernBreathReleasesTheCameraHoldOnce()
    {
        WyvernSetup();object breath=Get(source,"wyvernBreath");int completed=0;CameraCompletion(breath,()=>completed++);
        BreathActionTick(.1f);Set(source,"alive",false);BreathTick(.01f);
        Assert.That(completed,Is.EqualTo(1));GriffinCall("ClearWyvernBreaths");Assert.That(completed,Is.EqualTo(1));
    }

    [Test]
    public void NativeWyvernHeavenOwnsItsCameraCompletionUntilItsImpact()
    {
        Set(source,"augmentTranscended",true);WyvernSetup();object breath=CameraEffects("wyvernHeavenBreaths")[0];
        int completed=0;CameraCompletion(breath,()=>completed++);
        Assert.That(Get(source,"wyvernBreath"),Is.Null);
        Assert.That(completed,Is.Zero);Assert.That(CameraEffects("wyvernHeavenBreaths").Count,Is.EqualTo(1));
        HeavenTick(0f);HeavenTick(WyvernHeavenBreathRules.ImpactTime-.01f);Assert.That(completed,Is.Zero);
        float hp=Hp(target);HeavenTick(.02f);
        Assert.That(completed,Is.EqualTo(1));Assert.That(Hp(target),Is.LessThan(hp));
        Assert.That(CameraEffects("wyvernHeavenBreaths").Count,Is.EqualTo(1));
        GriffinCall("ClearWyvernBreaths");Assert.That(completed,Is.EqualTo(1));
    }

    [Test]
    public void ClearingTheNativeHeavenBreathReleasesItsCameraCompletionOnce()
    {
        Set(source,"augmentTranscended",true);WyvernSetup();object breath=CameraEffects("wyvernHeavenBreaths")[0];
        int completed=0;CameraCompletion(breath,()=>completed++);
        Assert.That(completed,Is.Zero);GriffinCall("ClearWyvernBreaths");GriffinCall("ClearWyvernBreaths");
        Assert.That(completed,Is.EqualTo(1));Assert.That(CameraEffects("wyvernHeavenBreaths"),Is.Empty);
    }
}
