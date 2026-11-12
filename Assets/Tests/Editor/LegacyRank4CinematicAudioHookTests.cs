using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class LegacyRank4CinematicAudioHookTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;

    [TestCase(typeof(SabertoothDevourCinematicTests),"sabertooth","sabertoothCinematic","UpdateSabertoothCinematic","CancelSabertoothCinematic")]
    [TestCase(typeof(FrogCatchCinematicTests),"frog","frogCatchCinematic","UpdateFrogCatchCinematic","CancelFrogCatchCinematic")]
    [TestCase(typeof(CrocodileAbyssDiveCinematicTests),"crocodile","crocodileAbyssCinematic","UpdateCrocodileAbyssDiveCinematic","CancelCrocodileAbyssDiveCinematic")]
    [TestCase(typeof(KingKongLeylineCinematicTests),"kingkong","kingKongCinematic","UpdateKingKongLeylineCinematic","CancelKingKongLeylineCinematic")]
    [TestCase(typeof(WizardCygnusCinematicTests),"wizard_mouse","wizardCygnusCinematic","UpdateWizardCygnusCinematic","CancelWizardCygnusCinematic")]
    [TestCase(typeof(BearAvatarCinematicTests),"bear","bearAvatarCinematic","UpdateBearAvatarCinematic","CancelBearAvatarCinematic")]
    [TestCase(typeof(WolfStampedeCinematicTests),"wolf","wolfStampedeCinematic","UpdateWolfStampedeCinematic","CancelWolfStampedeCinematic")]
    public void ActualCinematicOwnsItsTrackAdvancesWithItsClockAndStopsOnCancellation(
        Type fixtureType,string family,string stateField,string update,string cancel)
    {
        Assert.That(Rank4CinematicAudio.Supports(family),Is.True,"The production audio catalog must include "+family);
        object fixture=Activator.CreateInstance(fixtureType);
        Lifecycle<SetUpAttribute>(fixture);
        try
        {
            object battle=Field(fixture,"battle"),source=Field(fixture,"source");
            switch(family)
            {
                case "sabertooth":
                    Call(fixture,"QueueCast",source);Call(fixture,"UpdateCinematicAfterPrelude",update);break;
                case "frog":case "crocodile":
                    Call(fixture,"Begin");Call(fixture,"Advance",.5f);Call(fixture,"StartStage");break;
                case "wolf":
                    Call(fixture,"Begin",source);Call(fixture,"StartStage",source);break;
                default:
                    Call(fixture,"Trigger");Call(fixture,"StartStage");break;
            }
            object state=Field(battle,stateField),audio=Field(battle,"familyRank4CinematicAudio");
            Assert.That(state,Is.Not.Null);Assert.That(audio,Is.Not.Null);
            object bank=Field(audio,"bank");
            Assert.That(bank,Is.Not.Null);Assert.That(Field(bank,"family"),Is.EqualTo(family));
            AudioSource voice=(AudioSource)Field(audio,"source");
            Assert.That(Time.timeScale,Is.Zero);

            state.GetType().GetField("elapsed",Hidden).SetValue(state,1f);
            Assert.That(Call(battle,update),Is.True);
            Assert.That(Field(audio,"bank"),Is.SameAs(bank));
            Assert.That(Field(audio,"source"),Is.SameAs(voice),"One continuous track must keep the same voice through the film.");

            Call(battle,cancel);
            Assert.That(Field(battle,stateField),Is.Null);
            Assert.That(Field(audio,"bank"),Is.Null);
            Assert.That(Field(audio,"playAudio"),Is.False);
            Assert.That(Field(audio,"focusHeld"),Is.False);
            Assert.That(((AudioSource)Field(audio,"source")).isPlaying,Is.False);
            Assert.That(CombatSkillClock.Active,Is.False);
            Call(battle,cancel);
            Assert.That(Field(audio,"bank"),Is.Null,"Repeated cleanup must leave no pending track.");
        }
        finally{Lifecycle<TearDownAttribute>(fixture);}
    }

    private static void Lifecycle<T>(object fixture) where T:Attribute=>fixture.GetType().GetMethods(Hidden)
        .Single(m=>m.IsDefined(typeof(T),true)).Invoke(fixture,null);
    private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Hidden).Invoke(value,args);
    private static object Field(object value,string name)=>value.GetType().GetField(name,Hidden).GetValue(value);
}
