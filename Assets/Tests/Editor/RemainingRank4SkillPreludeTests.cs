using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed partial class RemainingRank4CinematicIntegrationTests
{
    private static IEnumerable<TestCaseData> Rank4PreludeActors()
    {
        foreach(string species in new[]{"velos","geomchi","golgaebi","ager","lilas","kongjwi","woongi","nukgu",
            "aranea","chundongi","tino","chiwoo","mantius","versus","kogason","jangpung","holydeer","pone","vivre","haeta"})
            for(int team=0;team<2;team++)yield return new TestCaseData(species,team);
    }

    private object Prelude=>BattleGet("rank4SkillPrelude");
    private HashSet<object> CompletedPreludes=>(HashSet<object>)BattleGet("rank4SkillPreludeCompleted");
    private bool TryPrelude(object unit,object key)=>(bool)Call("TryBeginRank4SkillPrelude",unit,key);
    private float PreludeDuration=>(float)type.GetField("Rank4SkillPreludeDuration",Hidden).GetValue(null);

    private object UpdateRank4AndPrelude()
    {
        object result=Call("UpdateRank4Cinematic");
        if(Prelude==null)return result;
        CompletePrelude();
        return Call("UpdateRank4Cinematic");
    }

    private void CompletePrelude()
    {
        Assert.That(Prelude,Is.Not.Null);
        SetField(Prelude,"elapsed",PreludeDuration+.001f);
        Call("UpdateRank4SkillPrelude");
        Assert.That(Prelude,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
    }

    [TestCaseSource(nameof(Rank4PreludeActors))]
    public void EveryRank4FamilyOnEitherTeamShowsAReadOnlyHitStopBeforeItsStage(string species,int team)
    {
        ReplaceSource(species,team);
        if(Definition(source).familyId=="mushroom")ArmReleasedFollowup(source);
        object key=new object();Vector3 caster=Model(source).transform.position,opponent=Model(primary).transform.position;
        Vector3 rootScale=Model(source).transform.localScale;bool flip=Renderer(source).flipX;
        float timer=(float)Get(source,"skillTimer"),cooldown=(float)Get(source,"cooldown");
        int uses=(int)Get(source,"skillUses");BattleSet("battleElapsed",3.25f);Time.timeScale=1.5f;

        Assert.That(TryPrelude(source,key),Is.True);
        Assert.That(Prelude,Is.Not.Null);Assert.That(Field(Prelude,"unit"),Is.SameAs(source));
        Assert.That(Field(Prelude,"key"),Is.SameAs(key));
        Assert.That(Time.timeScale,Is.Zero);Assert.That(CombatSkillClock.Active,Is.True);
        Assert.That(CombatSkillClock.ActorFrozen(Model(source).transform),Is.True);
        Assert.That(CombatSkillClock.ActorFrozen(Model(primary).transform),Is.True);
        Assert.That(Presentation,Is.Null,"The cutscene cannot cover the attention cue before it has been shown.");
        Assert.That(Model(source).transform.position,Is.EqualTo(caster));
        Assert.That(Model(source).transform.localScale,Is.EqualTo(rootScale),"Only the drawn body grows, never attack reach or collision.");
        Assert.That(Renderer(source).flipX,Is.EqualTo(flip));

        float presentationStep=Mathf.Clamp(Time.unscaledDeltaTime,0f,.05f);
        SetField(Prelude,"elapsed",PreludeDuration*.5f-presentationStep);Call("UpdateRank4SkillPrelude");
        Assert.That(Prelude,Is.Not.Null);Assert.That(BattleGet("battleElapsed"),Is.EqualTo(3.25f));
        Assert.That(Field(Prelude,"elapsed"),Is.EqualTo(PreludeDuration*.5f).Within(.0001f));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(uses));Assert.That(Get(source,"skillTimer"),Is.EqualTo(timer));
        Assert.That(Get(source,"cooldown"),Is.EqualTo(cooldown));AssertNoDamage();

        CompletePrelude();
        Assert.That(Time.timeScale,Is.EqualTo(1.5f));Assert.That(CompletedPreludes.Contains(key),Is.True);
        Assert.That(TryPrelude(source,key),Is.False,"The same committed cast must not show the intro twice.");
        Assert.That(Prelude,Is.Null);Assert.That(Model(primary).transform.position,Is.EqualTo(opponent));
    }

    [TestCase(.5f)][TestCase(1f)][TestCase(2f)]
    public void RepeatedRequestsHoldOnePreludeAndAnotherCastGetsItsOwnFullDuration(float speed)
    {
        ReplaceSource("holydeer",0);object first=new object(),second=new object();Time.timeScale=speed;
        Assert.That(TryPrelude(source,first),Is.True);object state=Prelude;
        SetField(state,"elapsed",.17f);
        Assert.That(TryPrelude(source,first),Is.True);Assert.That(Prelude,Is.SameAs(state));
        Assert.That(Field(state,"elapsed"),Is.EqualTo(.17f),"Retries cannot restart or shorten the visible cue.");
        CompletePrelude();Assert.That(Time.timeScale,Is.EqualTo(speed));
        Assert.That(TryPrelude(source,first),Is.False);
        Assert.That(TryPrelude(source,second),Is.True);Assert.That(Prelude,Is.Not.SameAs(state));
        Assert.That(Field(Prelude,"elapsed"),Is.EqualTo(0f).Within(.001f));
        Assert.That(Field(Prelude,"key"),Is.SameAs(second));CompletePrelude();
        Assert.That(Time.timeScale,Is.EqualTo(speed));
    }

    [Test]
    public void DifferentReadyCastersCannotStealTheCurrentHitStopAndEachGetsItsOwnCue()
    {
        ReplaceSource("holydeer",0);object other=Unit("velos",1,Vector3.right*3f);Set(other,"augmentTranscended",true);
        object first=new object(),second=new object();
        Assert.That(TryPrelude(source,first),Is.True);object state=Prelude;SetField(state,"elapsed",.2f);
        Assert.That(TryPrelude(other,second),Is.True,"Another ready request must wait instead of bypassing the current cue.");
        Assert.That(Prelude,Is.SameAs(state));Assert.That(Field(state,"unit"),Is.SameAs(source));
        Assert.That(Field(state,"elapsed"),Is.EqualTo(.2f));CompletePrelude();
        Assert.That(TryPrelude(other,second),Is.True);Assert.That(Field(Prelude,"unit"),Is.SameAs(other));
        Assert.That(Field(Prelude,"elapsed"),Is.EqualTo(0f).Within(.001f));CompletePrelude();
        Assert.That(CompletedPreludes.SetEquals(new[]{first,second}),Is.True);AssertNoDamage();
    }

    [TestCase("lower rank")][TestCase("ML episode")][TestCase("shadow clone")][TestCase("battle not started")]
    public void OrdinarySkillsSimulationClonesAndPreparationDoNotAcquireTheRank4Clock(string reason)
    {
        ReplaceSource("holydeer",0);
        if(reason=="lower rank")Set(source,"augmentTranscended",false);
        else if(reason=="ML episode")BattleSet("mlEpisodeActive",true);
        else if(reason=="shadow clone")Set(source,"augmentShadowClone",true);
        else BattleSet("battleStarted",false);
        Assert.That(TryPrelude(source,new object()),Is.False);Assert.That(Prelude,Is.Null);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(CompletedPreludes,Is.Empty);AssertNoDamage();
    }

    [TestCase("CancelRank4SkillPrelude")][TestCase("ResetRank4Cinematics")][TestCase("OnDisable")]
    public void CancellationAndWaveOrSceneCleanupRestoreTheBodyAndSelectedSpeed(string cleanup)
    {
        ReplaceSource("holydeer",0);var renderer=Renderer(source);
        Vector3 scale=renderer.transform.localScale,position=renderer.transform.localPosition;
        bool hidden=renderer.forceRenderingOff;Color tint=renderer.color;Time.timeScale=1.4f;
        Assert.That(TryPrelude(source,new object()),Is.True);
        SetField(Prelude,"elapsed",.2f);Call("UpdateRank4SkillPrelude");Call(cleanup);
        Assert.That(Prelude,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1.4f));
        Assert.That(renderer.forceRenderingOff,Is.EqualTo(hidden));Assert.That(renderer.color,Is.EqualTo(tint));
        Assert.That(Vector3.Distance(renderer.transform.localScale,scale),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(renderer.transform.localPosition,position),Is.LessThan(.0001f));AssertNoDamage();
    }

    [TestCase("generation")][TestCase("ending")][TestCase("caster death")][TestCase("external speed")]
    public void InvalidPreludeOwnersCannotLeaveTheBattleFrozenOrOverwriteAnExternalSpeed(string reason)
    {
        ReplaceSource("holydeer",0);Time.timeScale=1.6f;Assert.That(TryPrelude(source,new object()),Is.True);
        if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else if(reason=="ending")BattleSet("battleEnding",true);
        else if(reason=="caster death"){Set(source,"alive",false);Set(source,"hp",0f);}
        else Time.timeScale=.25f;
        Call("UpdateRank4SkillPrelude");
        Assert.That(Prelude,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(reason=="external speed"?.25f:1.6f));AssertNoDamage();
    }

    [Test]
    public void TheLastDeadMushroomMayPresentWhileItsRealDeathFlowerIsStillActive()
    {
        ReplaceSource("versus",0);ArmReleasedFollowup(source);
        Assert.That(Get(source,"alive"),Is.False);Assert.That(Get(source,"hp"),Is.EqualTo(0f));
        Assert.That(Call("IsMushroomDeathBloomActive",source),Is.True);
        Assert.That(TryPrelude(source,new object()),Is.True);
        CompletePrelude();Assert.That(Call("IsMushroomDeathBloomActive",source),Is.True);AssertNoDamage();
    }

    [TestCase(0,false)][TestCase(1,false)][TestCase(0,true)][TestCase(1,true)]
    public void FirstAndRepeatedRank4FieldCastsUseOrdinaryLocalPreparationUntilAnActualStageIsDequeued(int team,bool repeated)
    {
        bool enabled=CombatReadabilityPreview.Enabled;var preset=CombatReadabilityPreview.Preset;
        CombatReadabilityPreview.Enabled=true;CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        try
        {
            ReplaceSource("nukgu",team);Set(source,"skillTimer",0f);
            if(repeated)SetField(Call("FamilyTranscendenceStateFor",source,true),"wolfCinematicGeneration",BattleGet("combatGeneration"));
            var renderer=Renderer(source);Vector3 scale=renderer.transform.localScale;
            float opponentCooldown=(float)Get(primary,"cooldown");
            Assert.That(Call("BeginCommonSkillFocus",source,false),Is.True);
            object presentation=Call("SkillPresentationFor",source);
            Assert.That(Field(presentation,"pending"),Is.Not.Null);
            Assert.That(Prelude,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
            Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(renderer.transform.localScale,Is.EqualTo(scale));

            SetField(presentation,"startedAt",Time.unscaledTimeAsDouble-.10d);
            Call("UpdateCommonSkillFocus");
            Assert.That(Field(presentation,"pending"),Is.Not.Null,"The normal preparation delay must remain visible.");
            Assert.That(Prelude,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
            Assert.That(CompletedPreludes,Is.Empty);Assert.That(Get(source,"skillUses"),Is.EqualTo(0));
            Assert.That(Get(primary,"cooldown"),Is.EqualTo(opponentCooldown));

            SetField(presentation,"startedAt",Time.unscaledTimeAsDouble-1d);
            BattleSet("allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble-1d);
            Call("UpdateCommonSkillFocus");
            Assert.That(Field(presentation,"pending"),Is.Null);Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
            Assert.That(Get(source,"wolfStampede"),Is.Not.Null);
            Assert.That(Prelude,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
            var stageQueue=(IList)BattleGet("wolfStampedeCinematicQueue");
            Assert.That(stageQueue.Count,Is.EqualTo(repeated?0:1));
            Assert.That(Call("UpdateWolfStampedeCinematic"),Is.EqualTo(!repeated));
            Assert.That(Prelude==null,Is.EqualTo(repeated),"Only a real pending stage may acquire the global prelude.");
        }
        finally
        {
            Call("CancelCommonSkillFocus");
            CombatReadabilityPreview.Enabled=enabled;CombatReadabilityPreview.Preset=preset;
        }
    }

    [TestCase(0)][TestCase(1)]
    public void ARealOpeningStageWaitsForItsOwnCueBeforeTheCutsceneCanHideTheBattlefield(int team)
    {
        ReplaceSource("holydeer",team);Assert.That(QueueSource(),Is.True);
        object request=Queue[0];
        Assert.That(Call("UpdateRank4Cinematic"),Is.True);
        Assert.That(Prelude,Is.Not.Null);Assert.That(Field(Prelude,"key"),Is.SameAs(request));
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(Queue[0],Is.SameAs(request));
        Assert.That(Presentation,Is.Null);AssertNoDamage();
        CompletePrelude();Assert.That(Call("UpdateRank4Cinematic"),Is.True);
        Assert.That(Presentation,Is.Not.Null);Assert.That(Queue,Is.Empty);Assert.That(Prelude,Is.Null);
    }

    [Test]
    public void ResettingTheWaveClearsCompletedKeysSoTheNextWaveGetsItsAttentionCue()
    {
        ReplaceSource("holydeer",0);object key=new object();Assert.That(TryPrelude(source,key),Is.True);CompletePrelude();
        Assert.That(CompletedPreludes.Contains(key),Is.True);Call("ResetRank4Cinematics");
        BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        Assert.That(CompletedPreludes,Is.Empty);
        Assert.That(TryPrelude(source,key),Is.True);CompletePrelude();AssertNoDamage();
    }
}
