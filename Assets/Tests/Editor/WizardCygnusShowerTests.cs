using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class WizardCygnusShowerTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private Camera camera;
    private CombatCameraDirector director;
    private Type unitType;
    private object source,target,previousFrontend;
    private HashSet<GameObject> existing;
    private float previousSpeed;
    private bool previousTestMode;

    [SetUp] public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Hidden).GetValue(null);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,null);
        Assert.That(CombatSkillClock.Active,Is.False);
        Time.timeScale=1f;CombatEffectTestConfiguration.Active=true;
        var host=new GameObject("Inactive Cygnus gameplay tests");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",Hidden);
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",true);
        BattleSet("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        camera=new GameObject("Cygnus gameplay camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(0,8,-12),Quaternion.Euler(35,0,0));camera.fieldOfView=40f;camera.aspect=16f/9f;
        director=camera.gameObject.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        BattleSet("mainCamera",camera);BattleSet("combatCameraDirector",director);
        source=Unit("kongjwi",0,Vector3.zero,true);target=Unit("geomchi",1,Vector3.right);
        Set(source,"target",target);
    }

    [TearDown] public void Cleanup()
    {
        if(battle!=null){Call("ClearWizardCygnusShowers",true);Call("CancelWizardCygnusCinematic");Call("CancelCommonSkillFocus");Call("ResetFamilyTranscendenceStates");}
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)Object.DestroyImmediate(value);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"A completed or cancelled shower must not leave an actor clock active.");
    }

    [TestCase("kongjwi",0)][TestCase("kongjwi",1)]
    [TestCase("patjwi",0)][TestCase("patjwi",1)]
    [TestCase("seoljwi",0)][TestCase("seoljwi",1)]
    [TestCase("byeoljwi",0)][TestCase("byeoljwi",1)]
    public void AllFourSpeciesOnEitherTeamReplaceTheirFirstCastWithoutAccumulatingPriorCasts(string species,int team)
    {
        Units.Remove(source);Model(source).SetActive(false);source=Unit(species,team,Vector3.zero,true);
        Set(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));Set(source,"target",target);
        Begin();Assert.That(Shower(source),Is.Not.Null);Assert.That(Get(source,"wizardBarragePending"),Is.False);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8f));Assert.That(Hp(target),Is.EqualTo(5000f));
        Release();StepPastShower();Assert.That(Hp(target),Is.EqualTo(4550f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(5));Assert.That(Shower(source),Is.Null);Assert.That(Showers,Is.Empty);
    }

    [Test]
    public void NormalAndRankThreeWithoutTranscendenceDoNotUseTheNewAbility()
    {
        Set(source,"augmentTranscended",false);
        foreach(string trait in new[]{"",WizardMouseCombatRules.MilkyWayBarrageTraitId,WizardMouseCombatRules.PentagramAnnihilationTraitId,WizardMouseCombatRules.SupernovaDescentTraitId})
        {
            Traits(source).Clear();if(trait.Length>0)Traits(source).Add(trait);
            Assert.That(Call("UsesWizardCygnusShower",source),Is.False);
            Assert.That(Call("BeginWizardCygnusShower",source,Skill),Is.False);
        }
        Assert.That(Showers,Is.Empty);Assert.That(Get(source,"skillUses"),Is.Zero);
    }

    [TestCase(0)][TestCase(1)]
    public void EightOpponentsOnEitherTeamReceiveThirtySixBalancedNinetyPercentHits(int team)
    {
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
        var enemies=new List<object>{target};for(int i=1;i<8;i++)enemies.Add(Unit("geomchi",1-team,Vector3.right*(1f+i*.1f)));
        Begin();Release();StepPastShower(8);float total=0f,min=float.PositiveInfinity,max=0f;
        foreach(object enemy in enemies)
        {
            float damage=5000f-Hp(enemy);Assert.That(damage,Is.EqualTo(360f).Within(.01f).Or.EqualTo(450f).Within(.01f));
            total+=damage;min=Mathf.Min(min,damage);max=Mathf.Max(max,damage);
        }
        Assert.That(Get(source,"skillHits"),Is.EqualTo(36));Assert.That(total,Is.EqualTo(3240f).Within(.01f));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(total).Within(.01f));Assert.That(max-min,Is.LessThanOrEqualTo(90.01f));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8f));Assert.That(Showers,Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public void StormOrbAbsorbsEachEnemyCygnusMeteorOnceAndAllowsFriendlyMeteors(bool friendly)
    {
        object griffin=friendly?Unit("pone",0,Vector3.forward,true):target;
        Set(griffin,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_pone"));
        Set(griffin,"augmentTranscended",true);Call("BeginGriffinCrosswind",griffin);
        Begin();Release();StepPastShower();
        var winds=(IList)BattleGet("griffinCrosswinds");
        var stored=(IList)Field(winds[0],"shots");
        Assert.That(stored.Count,Is.EqualTo(friendly?3:8));
        Assert.That(Hp(target),Is.EqualTo(friendly?4550f:5000f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(friendly?5:0));
    }

    [Test]
    public void ThirtySixMeteorContactsKeepTheirDamageButSpaceTheirAudioAndUseOneDedicatedFinalCue()
    {
        for(int i=1;i<8;i++)Unit("geomchi",1,Vector3.right*(1f+i*.1f));
        Begin();Release();object state=Shower(source);
        var singleton=typeof(GameAudio).GetField("instance",Hidden);object previousAudio=singleton.GetValue(null);
        singleton.SetValue(null,null);BattleSet("mlEpisodeActive",false);
        try
        {
            int ordinaryAccents=0;float previousNext=0f;
            for(int step=0;step<140;step++)
            {
                Step(.01f);float next=(float)Field(state,"nextHitSoundAt");
                if(next<=previousNext)continue;
                if(ordinaryAccents>0)Assert.That(next-previousNext,Is.GreaterThanOrEqualTo(.07999f));
                ordinaryAccents++;previousNext=next;
            }
            Assert.That(Get(source,"skillHits"),Is.EqualTo(36));
            Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(3240f).Within(.01f));
            Assert.That(ordinaryAccents,Is.InRange(7,12),"Visual contacts remain dense while audible accents leave space.");
            Type cues=typeof(MonsterAutoBattlePrototype).GetNestedType("WizardSound",Hidden);
            var clips=(AudioClip[])BattleGet("wizardSkillSounds");
            Assert.That(clips[Convert.ToInt32(Enum.Parse(cues,"MeteorHit"))],Is.Not.Null);
            Assert.That(clips[Convert.ToInt32(Enum.Parse(cues,"MeteorFinish"))],Is.Not.Null,
                "The final contact bypasses the ordinary accent interval.");
            Assert.That(clips[Convert.ToInt32(Enum.Parse(cues,"Hit"))],Is.Null,
                "Meteor hits must not also request the original basic-star hit.");
            Assert.That(clips[Convert.ToInt32(Enum.Parse(cues,"Crash"))],Is.Null,
                "The final cue replaces, rather than layers with, the old crash call.");
        }
        finally{singleton.SetValue(null,previousAudio);BattleSet("mlEpisodeActive",true);}
    }

    [Test]
    public void AnAbsorbedCygnusMeteorKeepsItsFlightSpriteForStorageAndNeverDrawsItsOldTrailOrImpact()
    {
        var effect=PixelWizardCygnusMeteorShower.Create(Vector3.zero,new[]{Vector3.right},null,i=>Vector3.right);
        effect.Advance(.1f);SpriteRenderer star=effect.ProjectileRenderer(0);
        Assert.That(star.sprite,Is.Not.Null);Assert.That(star.enabled,Is.True);
        effect.AbsorbProjectile(0);effect.Advance(WizardCygnusShowerRules.ImpactTime(0,1)+.05f);
        Assert.That(star.enabled,Is.False);
        Assert.That(((bool[])Field(effect,"landed"))[0],Is.False);
        Assert.That(((int[])Field(effect,"trailCounts"))[0],Is.Zero);
        effect.Cancel();
    }

    [Test]
    public void FirstQueuedCinematicFreezesGameplayTimeAndItsPreparationDoesNotCauseAnEarlyHit()
    {
        BattleSet("mlEpisodeActive",false);Begin();object state=Shower(source);
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(Field(state,"released"),Is.False);
        Step(10f);Assert.That(Field(state,"time"),Is.EqualTo(0f));Assert.That(Hp(target),Is.EqualTo(5000f));
        Call("CancelWizardCygnusCinematicFor",source);Release();Assert.That(Field(state,"time"),Is.EqualTo(0f));
        Assert.That(Field(state,"released"),Is.True);Assert.That(Hp(target),Is.EqualTo(5000f));
        AdvanceTo(WizardCygnusShowerRules.ImpactTime(0,1)-.001f);Assert.That(Hp(target),Is.EqualTo(5000f));
        AdvanceTo(WizardCygnusShowerRules.ImpactTime(0,1)+.001f);Assert.That(Hp(target),Is.EqualTo(4910f).Within(.01f));
    }

    [Test]
    public void OrdinaryRecastPreparationDefersDamageAndPreservesOnlyTheElapsedRemainderOnRelease()
    {
        Begin();Step(WizardCygnusShowerRules.Preparation-.001f);
        Assert.That(Field(Shower(source),"released"),Is.False);Assert.That(Hp(target),Is.EqualTo(5000f));
        Step(.002f);Assert.That(Field(Shower(source),"released"),Is.True);
        Assert.That((float)Field(Shower(source),"time"),Is.EqualTo(.001f).Within(.0001f));Assert.That(Hp(target),Is.EqualTo(5000f));
        AdvanceTo(WizardCygnusShowerRules.ImpactTime(0,1)-.001f);Assert.That(Hp(target),Is.EqualTo(5000f));
        AdvanceTo(WizardCygnusShowerRules.ImpactTime(0,1)+.001f);Assert.That(Hp(target),Is.EqualTo(4910f).Within(.01f));
    }

    [Test]
    public void EveryMeteorDealsNinetyPercentExactlyOnceAtItsOwnImpactEvenWhenVictimsOverlap()
    {
        object second=Unit("tino",1,Model(target).transform.position);Begin();Release();
        Assert.That(Hp(target)+Hp(second),Is.EqualTo(10000f));
        int firstHits=0,secondHits=0;
        for(int index=0;index<WizardCygnusShowerRules.ShotCount(2);index++)
        {
            AdvanceTo(WizardCygnusShowerRules.ImpactTime(index,2)-.001f);
            Assert.That(Hp(target),Is.EqualTo(5000f-firstHits*90f).Within(.01f));
            Assert.That(Hp(second),Is.EqualTo(5000f-secondHits*90f).Within(.01f));
            AdvanceTo(WizardCygnusShowerRules.ImpactTime(index,2)+.001f);
            if(index%2==0)firstHits++;else secondHits++;
            Assert.That(Hp(target),Is.EqualTo(5000f-firstHits*90f).Within(.01f));
            Assert.That(Hp(second),Is.EqualTo(5000f-secondHits*90f).Within(.01f));
        }
        Step(.01f);Assert.That(Get(source,"skillHits"),Is.EqualTo(9));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(810f).Within(.01f));
        foreach(object enemy in new[]{target,second})Assert.That(Get(enemy,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
    }

    [TestCase(50f,300f)][TestCase(100f,225f)]
    public void TheBuffedFiveHitsStillUseOrdinaryDefenseWithoutAddingArmorPenetration(float defense,float expectedDamage)
    {
        Set(target,"defense",defense);Begin();Release();StepPastShower();
        Assert.That(Hp(target),Is.EqualTo(5000f-expectedDamage).Within(.01f));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(expectedDamage).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(5));
    }

    [Test]
    public void LargeSummonedGroupsResolveInterleavedWavesAndLongFramesWithoutSkippedOrRepeatedDamage()
    {
        var enemies=new List<object>{target};
        for(int i=1;i<32;i++)
        {
            object enemy=Unit("geomchi",1,Vector3.right);if(i>=8)Set(enemy,"rank3MycokingSummon",true);
            enemies.Add(enemy);
        }
        Begin();Release();Step(WizardCygnusShowerRules.ImpactTime(16,32)+.001f);
        Assert.That(Hp(enemies[0]),Is.EqualTo(4910f).Within(.01f));
        Assert.That(Hp(enemies[31]),Is.EqualTo(5000f));
        StepPastShower(enemies.Count);for(int i=0;i<enemies.Count;i++)Assert.That(Hp(enemies[i]),Is.EqualTo(i<16?4550f:4640f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(144));Assert.That(Showers,Is.Empty);
        StepPastShower(enemies.Count);for(int i=0;i<enemies.Count;i++)Assert.That(Hp(enemies[i]),Is.EqualTo(i<16?4550f:4640f).Within(.01f));
    }

    [Test]
    public void DamageAndTraitScaleAreSnapshottedAtCastAndAcceptedTargetsCanLeaveTheOriginalRange()
    {
        Set(source,"skillTraitDamageMultiplier",.5f);Begin();
        Model(target).transform.position=Vector3.right*50f;Set(source,"damage",1000f);Set(source,"skillTraitDamageMultiplier",4f);
        Release();StepPastShower();Assert.That(Hp(target),Is.EqualTo(4775f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(5));
    }

    [Test]
    public void PrimaryMustBeInReachAtCommitButTheAcceptedShowerIncludesAllCurrentTargetableOpponents()
    {
        Model(target).transform.position=Vector3.right*50f;
        Assert.That(Call("BeginMagicStarBarrage",source,Skill),Is.False);Assert.That(Get(source,"skillUses"),Is.Zero);
        Model(target).transform.position=Vector3.right;object distant=Unit("tino",1,Vector3.right*50f);
        object stored=Unit("geomchi",1,Vector3.right);Set(stored,"inStorage",true);
        Begin();Release();StepPastShower(2);
        Assert.That(Hp(target),Is.EqualTo(4550f));Assert.That(Hp(distant),Is.EqualTo(4640f));Assert.That(Hp(stored),Is.EqualTo(5000f));
    }

    [Test]
    public void DeadTargetsDoNotTransferTheirRemainingStarsAndNewEnemiesAreNeverEnrolled()
    {
        object second=Unit("tino",1,new Vector3(1,0,.1f));Begin();Release();Step(WizardCygnusShowerRules.ImpactTime(1,2)+.001f);
        Assert.That(Hp(target),Is.EqualTo(4910f));Assert.That(Hp(second),Is.EqualTo(4910f));
        Set(target,"alive",false);Set(target,"hp",0f);object entrant=Unit("geomchi",1,Vector3.right);
        StepPastShower(2);Assert.That(Hp(target),Is.Zero);Assert.That(Hp(second),Is.EqualTo(4640f));Assert.That(Hp(entrant),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(5));
    }

    [TestCase("death")][TestCase("freeze")][TestCase("knockdown")][TestCase("displacement")]
    public void ReleasedMeteorsFinishEvenAfterTheOwnerDiesOrItsActionIsInterrupted(string reason)
    {
        Begin();Release();Interrupt(reason);Call("CancelWizardCygnusShowerFor",source);
        Assert.That(Shower(source),Is.Null);Assert.That(Showers.Count,Is.EqualTo(1));
        StepPastShower();Assert.That(Hp(target),Is.EqualTo(4550f).Within(.01f));Assert.That(Showers,Is.Empty);
    }

    [TestCase("death")][TestCase("knockdown")][TestCase("displacement")]
    public void UnreleasedInvalidatedCastCancelsWithoutSpawningOrApplyingAnyStars(string reason)
    {
        Begin();Interrupt(reason);StepPastShower();
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Showers,Is.Empty);Assert.That(Shower(source),Is.Null);
        Assert.That(Queue,Is.Empty);Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(0f));
    }

    [Test]
    public void FrozenPreparationWaitsThenResumesItsOwnPreparationInsteadOfLosingOrReleasingTheCast()
    {
        Begin();Status(source).ApplyTimed(CombatStatusKind.Freeze,3f);Step(2f);
        Assert.That(Field(Shower(source),"time"),Is.EqualTo(0f));Assert.That(Hp(target),Is.EqualTo(5000f));
        Status(source).Remove(CombatStatusKind.Freeze);Step(WizardCygnusShowerRules.Preparation+.001f);StepPastShower();
        Assert.That(Hp(target),Is.EqualTo(4550f).Within(.01f));
    }

    [TestCase("generation")][TestCase("reset")][TestCase("disable")]
    public void WaveAndLifecycleCleanupCancelAllRemainingDamageAndReleaseTheCaster(string reason)
    {
        Begin();Release();Step(WizardCygnusShowerRules.ImpactTime(0,1)+.001f);float hp=Hp(target);
        if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else if(reason=="reset")Call("ResetFamilyTranscendenceStates");
        else Call("OnDisable");
        StepPastShower();Assert.That(Hp(target),Is.EqualTo(hp));Assert.That(Showers,Is.Empty);Assert.That(Shower(source),Is.Null);
        Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(0f));Assert.That(Queue,Is.Empty);
    }

    [Test]
    public void RankThreeAndIntrinsicWizardEffectsDoNotProcAgainFromTheTranscendentShower()
    {
        foreach(string id in new[]{WizardMouseCombatRules.KongjwiTraitId,WizardMouseCombatRules.PatjwiTraitId,
            WizardMouseCombatRules.SeoljwiTraitId,WizardMouseCombatRules.ByeoljwiTraitId,
            WizardMouseCombatRules.MilkyWayBarrageTraitId,WizardMouseCombatRules.PentagramAnnihilationTraitId,WizardMouseCombatRules.SupernovaDescentTraitId})Traits(source).Add(id);
        Set(source,"hp",4000f);object ally=Unit("kongjwi",0,Vector3.back);Set(ally,"hp",1000f);
        object second=Unit("tino",1,Vector3.right*1.1f);Begin();Release();StepPastShower(2);
        Assert.That(Hp(target),Is.EqualTo(4550f).Within(.01f));Assert.That(Hp(second),Is.EqualTo(4640f).Within(.01f));
        Assert.That(Hp(source),Is.EqualTo(4000f));Assert.That(Hp(ally),Is.EqualTo(1000f));
        Assert.That(Status(target).Has(CombatStatusKind.Freeze),Is.False);
        Assert.That(Status(target).Has(CombatStatusKind.ComboChilled),Is.False);
        Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));Assert.That(Object.FindObjectsOfType<PixelStarProjectile>(),Is.Empty);
        Assert.That(Object.FindObjectsOfType<PixelWizardSupernova>(),Is.Empty);
    }

    [Test]
    public void EchoUsesFiveScaledHitsOnItsOwnTargetWithoutAStageCooldownOrReplacingTheOriginalCast()
    {
        object second=Unit("tino",1,Vector3.right*1.1f);Begin();Release();object original=Shower(source);
        float cooldown=(float)Get(source,"skillTimer");Call("ResolveWizardCygnusShowerEcho",source,target,.5f);
        Assert.That(Shower(source),Is.SameAs(original));Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(cooldown));Assert.That(Queue,Is.Empty);
        StepPastShower(2);Assert.That(Hp(target),Is.EqualTo(4325f).Within(.01f));Assert.That(Hp(second),Is.EqualTo(4640f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(14));Assert.That(Showers,Is.Empty);
    }

    [TestCase(false)][TestCase(true)]
    public void CompletingAnOlderReleasedCastOrEchoDoesNotCancelANewerQueuedCastOnTheSameOwner(bool echo)
    {
        if(echo)Call("ResolveWizardCygnusShowerEcho",source,target,1f);
        else{Begin();Release();Call("CancelWizardCygnusShowerFor",source);}
        BattleSet("mlEpisodeActive",false);Begin();object newer=Shower(source);
        Assert.That(Queue.Count,Is.EqualTo(1));StepPastShower();
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(Shower(source),Is.SameAs(newer));
        Assert.That(Field(newer,"released"),Is.False);Assert.That(Field(newer,"time"),Is.EqualTo(0f));
        Assert.That(Hp(target),Is.EqualTo(4550f));
    }

    [TestCase(false)][TestCase(true)]
    public void ResultScreenPreservesReleasedVisualTailWithoutAnyFurtherGameplayDamage(bool alreadyPreserved)
    {
        BattleSet("mlEpisodeActive",false);Begin();Call("CancelWizardCygnusCinematicFor",source);Release();Step(WizardCygnusShowerRules.ImpactTime(0,1)+.001f);
        var effect=(PixelWizardCygnusMeteorShower)Field(Shower(source),"effect");Assert.That(effect,Is.Not.Null);
        if(alreadyPreserved)Call("PreserveBattleEffects");
        float hp=Hp(target),elapsed=effect.Elapsed;BattleSet("battleEnding",true);Call("ClearWizardCygnusShowers",false);
        Assert.That(effect!=null,Is.True);Assert.That(CombatEffectEndPlayback.IsPreserving(effect),Is.True);
        Assert.That(Field(effect,"liveTarget"),Is.Null);Assert.That(Showers,Is.Empty);Assert.That(Shower(source),Is.Null);
        StepPastShower();Assert.That(Hp(target),Is.EqualTo(hp));
        var playback=effect.GetComponent<CombatEffectEndPlayback>();
        typeof(CombatEffectEndPlayback).GetMethod("Advance",Hidden).Invoke(playback,new object[]{.1f});
        Assert.That(effect.Elapsed,Is.GreaterThan(elapsed));Assert.That(Hp(target),Is.EqualTo(hp));
    }

    private void Begin()=>Assert.That(Call("BeginMagicStarBarrage",source,Skill),Is.True);
    private void Release()=>Call("ReleaseWizardCygnusShower",source);
    private void Step(float dt)=>Call("UpdateWizardCygnusShowers",dt);
    private void StepPastShower(int targetCount=1)=>Step(WizardCygnusShowerRules.DurationForTargets(targetCount)+.001f);
    private void AdvanceTo(float time)=>Step(time-(float)Field(Shower(source),"time"));
    private void Interrupt(string reason)
    {
        if(reason=="death"){Set(source,"alive",false);Set(source,"hp",0f);}
        else if(reason=="displacement")Set(source,"positionRevision",(int)Get(source,"positionRevision")+1);
        else Status(source).ApplyTimed(reason=="freeze"?CombatStatusKind.Freeze:CombatStatusKind.Knockdown,3f);
    }
    private object Unit(string species,int team,Vector3 position,bool transcended=false)
    {
        object unit=Activator.CreateInstance(unitType,true);var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        Assert.That(definition,Is.Not.Null);var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null);
        var authored=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);Assert.That(authored,Is.Not.Null);
        var model=new GameObject("Cygnus gameplay actor "+species);model.transform.position=position;
        var visual=new GameObject("Authored sprite");visual.transform.SetParent(model.transform,false);
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        driver.Configure(Frames(authored,"idleFrames"),Frames(authored,"runFrames"),Frames(authored,"attackFrames"),
            Frames(authored,"skillAttackFrames"),Frames(authored,"deathFrames"),Frames(authored,"captureFrames"),Frames(authored,"struggleFrames"));
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        visual.transform.rotation=camera.transform.rotation;var ring=new GameObject("Cygnus shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",Units.Count);Set(unit,"model",model);Set(unit,"ring",ring);Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"definition",definition);Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",WizardMouseCombatRules.SkillId);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));Set(unit,"alive",true);Set(unit,"augmentTranscended",transcended);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",3);
        Set(unit,"collisionRadius",.3f);Set(unit,"skillCooldownDuration",8f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"lastGroundPosition",position);
        Units.Add(unit);return unit;
    }
    private SkillMasterRow Skill=>GameMasterData.Skill(WizardMouseCombatRules.SkillId);
    private IList Units=>(IList)BattleGet("units");
    private IList Showers=>(IList)BattleGet("wizardCygnusShowers");
    private IList Queue=>(IList)BattleGet("wizardCygnusCinematicQueue");
    private IList Traits(object unit)=>(IList)Get(unit,"activeTraitIds");
    private object Shower(object unit)=>Get(unit,"wizardCygnusShower");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private static Sprite[] Frames(SpriteMonsterAnimationDriver authored,string name)=>(Sprite[])Field(authored,name);
    private object Get(object unit,string name)=>unitType.GetField(name,Hidden).GetValue(unit);
    private void Set(object unit,string name,object value)=>unitType.GetField(name,Hidden).SetValue(unit,value);
    private object BattleGet(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).GetValue(battle);
    private void BattleSet(string name,object value)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).SetValue(battle,value);
    private static object Field(object value,string name)=>value.GetType().GetField(name,Hidden).GetValue(value);
    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Hidden).Invoke(battle,args);
}
