using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    private void DisableReleasedCaster(string state)
    {
        if(state=="death")MoltDamage(target,source,99999f);
        else if(state=="stun")
        {
            Set(source,"giraffeStunTimer",5f);
            typeof(MonsterAutoBattlePrototype).GetMethod("InterruptPositionalAction",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new[]{source});
        }
        else
        {
            var statuses=(CombatStatusState)Get(source,"combatStatuses");
            var effect=statuses.ApplyTimed(state=="freeze"?CombatStatusKind.Freeze:CombatStatusKind.Knockdown,5f,target);
            ReaperCall("HandleCombatStatusApplied",source,effect);
        }
    }

    [TestCase("death")] [TestCase("stun")] [TestCase("freeze")] [TestCase("knockdown")]
    public void ReleasedBeansCompleteTheirVolleyAfterCasterIsDisabled(string state)
    {
        StartCast();Assert.That(cast.MoveNext(),Is.True);DisableReleasedCaster(state);
        for(int i=0;i<4;i++)Assert.That(cast.MoveNext(),Is.True);
        foreach(var shot in Shots())Arrive(shot);
        Assert.That(cast.MoveNext(),Is.False);Assert.That(Hp(target),Is.LessThan(4800f));
    }

    [TestCase("death")] [TestCase("stun")] [TestCase("freeze")]
    public void ReleasedMountainCollapseStillLaunchesItsFinalFruitAfterCasterIsDisabled(string state)
    {
        Trait(MonkeyRank3Rules.MountainCollapse);StartCast();
        for(int i=0;i<4;i++)Assert.That(cast.MoveNext(),Is.True);
        DisableReleasedCaster(state);
        Assert.That(cast.MoveNext(),Is.True);kingCast=(IEnumerator)cast.Current;
        Assert.That(kingCast.MoveNext(),Is.True);
        foreach(var shot in Shots())Arrive(shot);
        for(int stage=0;stage<3;stage++){Arrive(KingFruit());kingCast.MoveNext();}
        while(cast.MoveNext()){}
        Assert.That(Get(source,"skillHits"),Is.EqualTo(7));Assert.That(Hp(target),Is.LessThan(4800f));
    }

    [TestCase("death")] [TestCase("stun")] [TestCase("freeze")] [TestCase("knockdown")]
    public void ReleasedSporeLandsAndItsGasRunsToExpiryAfterCasterIsDisabled(string state)
    {
        GasReady();cast=(IEnumerator)ReaperCall("ResolveSporeColony",source,target,
            GameMasterData.Skill(MushroomCombatRules.SkillId),1f,false,0);
        Assert.That(cast.MoveNext(),Is.True);DisableReleasedCaster(state);
        Arrive(Object.FindObjectOfType<PixelStarProjectile>());Assert.That(cast.MoveNext(),Is.False);
        GasTick(4f);Assert.That(Hp(target),Is.EqualTo(4840f));Assert.That(GasZones.Count,Is.Zero);
    }

    [TestCase("stun")] [TestCase("freeze")] [TestCase("knockdown")]
    public void ReleasedWyvernBreathRunsAllHitsAfterLivingCasterIsDisabled(string state)
    {
        WyvernSetup();BreathTick(.52f);DisableReleasedCaster(state);BreathTick(1f);
        Assert.That(Hp(target),Is.EqualTo(4800f));Assert.That(Get(source,"wyvernBreath"),Is.Null);
    }

    [TestCase("death")] [TestCase("stun")] [TestCase("freeze")] [TestCase("knockdown")]
    public void ReleasedRoyalWindAdvancesAfterCasterIsDisabled(string state)
    {
        RoyalSetup();GriffinCall("FireGriffinRoyalWind",source,target);DisableReleasedCaster(state);
        GriffinCall("TickGriffinRoyal",source,1f);Assert.That(Hp(target),Is.EqualTo(4920f));
    }

    [TestCase("chiwoo")] [TestCase("seoljwi")] [TestCase("versus")] [TestCase("aranea")] [TestCase("holydeer")]
    public void ReleasedBasicProjectileHitsAfterCasterDeathAndFear(string monster)
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+monster));
        ReaperCall("BindProjectileSource",source);
        if(monster=="aranea")
        {
            cast=(IEnumerator)ReaperCall("PlaySpiderSilkBasicAttack",source,target,100f,0);
            Assert.That(cast.MoveNext(),Is.False);
        }
        else ReaperCall("BeginBasicAttackImpact",source,target,100f,false,1f);
        var shot=Object.FindObjectsOfType<PixelStarProjectile>().Single();
        ((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(CombatStatusKind.Fear,5f,target);
        DisableReleasedCaster("death");Arrive(shot);
        Assert.That(Hp(target),Is.EqualTo(4900f));
    }

    [TestCase(0)] [TestCase(1)]
    public void BattleResultDoesNotWaitForReleasedEffectsAfterEitherTeamWipe(int casterTeam)
    {
        var teamType=unitType.GetField("team").FieldType;
        Set(source,"team",Enum.ToObject(teamType,casterTeam));
        Set(target,"team",Enum.ToObject(teamType,1-casterTeam));
        ReaperCall("BindProjectileSource",source);LaunchAll();DisableReleasedCaster("death");
        var shots=Shots();
        Assert.That(ReaperCall("HasPendingReleasedEffects",Get(source,"team")),Is.True);
        float hp=Hp(target);
        var type=typeof(MonsterAutoBattlePrototype);
        int generation=(int)type.GetField("combatGeneration",Private).GetValue(battle);
        ReaperCall("CheckBattleResult");
        Assert.That(type.GetField("battleEnding",Private).GetValue(battle),Is.True);
        Assert.That(type.GetField("pendingResult",Private).GetValue(battle),
            Is.EqualTo(casterTeam==0?"적군 승리":"아군 승리"));
        Assert.That(type.GetField("waveOutcomeStage",Private).GetValue(battle).ToString(),
            Is.EqualTo("Message"),"Show the outcome immediately while released visuals keep playing.");
        Assert.That(type.GetField("waveOutcomeTimer",Private).GetValue(battle),Is.EqualTo(2f));
        Assert.That(type.GetField("combatGeneration",Private).GetValue(battle),Is.EqualTo(generation+1));
        ReaperCall("UpdateBattleEnding",.25f);
        ReaperCall("CheckBattleResult");
        Assert.That(type.GetField("waveOutcomeTimer",Private).GetValue(battle),Is.EqualTo(1.75f),
            "Repeated result checks cannot restart the message or its ending cue.");
        Assert.That(type.GetField("combatGeneration",Private).GetValue(battle),Is.EqualTo(generation+1));
        foreach(var shot in shots)
        {
            Assert.That(shot!=null,Is.True);
            Assert.That(shot.enabled,Is.False,"Only isolated playback may advance the old visual.");
            Assert.That(CombatEffectEndPlayback.IsPreserving(shot),Is.True);
            Assert.That(shot.GetComponent<CombatEffectEndFade>(),Is.Null,"Do not shorten the skill to a fade.");
            var playback=shot.GetComponent<CombatEffectEndPlayback>();
            Vector3 before=shot.transform.GetChild(0).position;
            typeof(CombatEffectEndPlayback).GetMethod("Advance",Private).Invoke(playback,new object[]{.1f});
            Assert.That(shot!=null,Is.True);
            Assert.That(shot.transform.GetChild(0).position,Is.Not.EqualTo(before));
            typeof(CombatEffectEndPlayback).GetMethod("Advance",Private).Invoke(playback,new object[]{1f});
            Assert.That(shot==null||!shot.gameObject.activeSelf,Is.True);
        }
        Assert.That(Hp(target),Is.EqualTo(hp),"Visual playback must not deal damage after the result.");
        Assert.That(cast.MoveNext(),Is.False,"Old combat routines cannot dispatch further shots.");
        Assert.That(Shots(),Is.Empty);
    }

    [Test]
    public void ReleasedEffectCounterOldCallbacksCannotConsumeNewBattleEffects()
    {
        ReaperCall("BindProjectileSource",source);
        var old=CombatProjectileSource.Capture(Model(source).transform);
        typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        var current=CombatProjectileSource.Capture(Model(source).transform);
        Assert.That(old.valid(),Is.False);old.finished();old.finished();
        Assert.That(ReaperCall("HasPendingReleasedEffects",Get(source,"team")),Is.True);
        current.finished();Assert.That(ReaperCall("HasPendingReleasedEffects",Get(source,"team")),Is.False);
    }

    [Test]
    public void ReleasedEffectClockPausesGloballyAndIgnoresCasterFreeze()
    {
        ReaperCall("BindProjectileSource",source);LaunchAll();var shot=Shots()[0];
        var start=shot.transform.GetChild(0).position;DisableReleasedCaster("freeze");
        var late=typeof(PixelStarProjectile).GetMethod("Update",Private);
        late.Invoke(shot,null);Assert.That(shot.transform.GetChild(0).position,Is.EqualTo(start));
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,.1f);
        late.Invoke(shot,null);Assert.That(shot.transform.GetChild(0).position,Is.Not.EqualTo(start));
    }

    [Test]
    public void ReleasedGasStillRespondsToAlliedResonanceAfterCasterDeath()
    {
        ResonanceReady();GasCreate();DisableReleasedCaster("death");
        var ally=Unit(0,Vector3.zero);ResonanceHit(ally,target);GasTick(.08f);
        Assert.That(Hp(target),Is.EqualTo(4959f));Assert.That(Resonances(),Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void ReleasedStormItemFieldSurvivesDeathAndResetCancelsPendingTicks(bool reset)
    {
        cast=(IEnumerator)ReaperCall("CorruptStormZone",source,target,new ItemTransformMasterRow());
        Assert.That(cast.MoveNext(),Is.True);
        if(reset)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else DisableReleasedCaster("death");
        while(cast.MoveNext()){}
        Assert.That(Hp(target),Is.EqualTo(reset?5000f:4900f));
    }

    [Test]
    public void ReleasedSpiritGhostWaitPreservesItsDelayAfterOwnerAndGhostDeath()
    {
        var ghost=ConcertGhost("chiwoo");
        var wait=(WaitUntil)ReaperCall("SkillWait",ghost,.2f);
        Set(source,"alive",false);Set(ghost,"alive",false);
        Assert.That(wait.keepWaiting,Is.True);
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,.2f);
        Assert.That(wait.keepWaiting,Is.False);
    }

    [TestCase(false)] [TestCase(true)]
    public void ReleasedReaperEchoSurvivesDeathAndResetCancelsBothShots(bool reset)
    {
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_mantius"));
        Set(source,"skillId",MantisCombatRules.SkillId);
        var actionType=typeof(MonsterAutoBattlePrototype).GetNestedType("MantisReaperAction",System.Reflection.BindingFlags.NonPublic);
        var action=Activator.CreateInstance(actionType,true);
        actionType.GetField("scale").SetValue(action,1f);
        actionType.GetField("killPoint").SetValue(action,Model(target).transform.position);
        cast=(IEnumerator)ReaperCall("MantisReaperEcho",source,action);
        Assert.That(cast.MoveNext(),Is.True);
        if(reset)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else DisableReleasedCaster("death");
        if(!reset)
        {
            Assert.That(cast.MoveNext(),Is.True);
            foreach(var shot in Object.FindObjectsOfType<PixelStarProjectile>())Arrive(shot);
        }
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(reset?5000f:4856f));
        Assert.That(Object.FindObjectsOfType<PixelStarProjectile>(),Is.Empty);
    }
}
