using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MonkeyStormTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private object source,target,previousFrontend;
    private Camera camera;
    private HashSet<GameObject> existing;
    private float previousSpeed;
    private bool previousTestMode;

    [SetUp] public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Hidden).GetValue(null);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,null);
        Time.timeScale=1f;CombatEffectTestConfiguration.Active=true;
        var host=new GameObject("Inactive thunderstorm gameplay test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",Hidden);
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",true);
        BattleSet("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());
        BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,1f,24f)));
        camera=new GameObject("Thunderstorm battle camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(0,8,-12),Quaternion.Euler(35,0,0));camera.fieldOfView=40f;camera.aspect=16f/9f;
        BattleSet("mainCamera",camera);
        source=Unit("chiwoo",0,Vector3.zero,true);target=Unit("geomchi",1,Vector3.right);
        Set(source,"target",target);
    }

    [TearDown] public void Cleanup()
    {
        if(battle!=null){Call("ClearMonkeyStorms",false);Call("ResetRank4Cinematics");Call("CancelCommonSkillFocus");}
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False);
    }

    [TestCase("chiwoo",0)][TestCase("chiwoo",1)]
    [TestCase("toto",0)][TestCase("toto",1)]
    [TestCase("uno",0)][TestCase("uno",1)]
    [TestCase("maka",0)][TestCase("maka",1)]
    public void AllSpeciesReplaceNimbusOnBothTeamsWithOneOrdinaryCooldownAndNoInvulnerability(string species,int team)
    {
        Units.Remove(source);Model(source).SetActive(false);source=Unit(species,team,Vector3.zero,true);
        Set(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));Set(source,"target",target);
        Begin();
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"traitActivations"),Is.EqualTo(1));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8f));Assert.That(Get(source,"monkeyNimbus"),Is.Null);
        Assert.That(Get(source,"monkeyBarragePending"),Is.False);Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(0f));
        Assert.That(Call("IsCombatTargetable",source),Is.True);Assert.That(Model(source).transform.position,Is.EqualTo(Vector3.zero));
        Assert.That(Call("IsMonkeyStormActive",source),Is.True);Assert.That(Call("IsMonkeyStormPending",source),Is.False);
    }

    [Test]
    public void EachThunderShakesOnceIncludingTheWinningHitAndRespectsShakeOff()
    {
        ConcentrateArena();Set(target,"hp",2000f);Begin();BattleSet("mlEpisodeActive",false);
        var director=camera.gameObject.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);
        director.SetShakeStrength(1f);BattleSet("combatCameraDirector",director);
        float[] times=Times;Step(times[0]-.001f);Assert.That(Field(director,"shakeUntil"),Is.EqualTo(0f));
        for(int i=0;i<4;i++)
        {
            director.BeginBattle();Step(i==0?.001f:times[i]-times[i-1]);
            Assert.That(Field(director,"shakeAmplitudePixels"),Is.EqualTo(14f));
            Assert.That((float)Field(director,"shakeUntil"),Is.GreaterThan(Time.unscaledTime));
        }
        Assert.That(Hp(target),Is.Zero);Assert.That(Get(source,"skillHits"),Is.EqualTo(4));
        director.EndBattle();Assert.That((float)Field(director,"shakeUntil"),Is.GreaterThan(Time.unscaledTime));
        director.SetShakeStrength(0f);director.ReportMonkeyStormThunder();
        Assert.That(Field(director,"shakeUntil"),Is.EqualTo(0f));
    }

    [Test]
    public void NormalAndRankThreeCastsRetainTheirOriginalFruitSkills()
    {
        Set(source,"augmentTranscended",false);
        Assert.That(Call("BeginAcrobaticBarrage",source,Skill),Is.True);
        Assert.That(Storms,Is.Empty);Assert.That(Get(source,"monkeyBarragePending"),Is.True);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
    }

    [TestCase(0f,2000f)][TestCase(50f,1333.3333f)][TestCase(100f,1000f)]
    public void FourHeavyStrikesDealFiveHundredPercentEachWithOrdinaryDefenseEvenAcrossOneLongFrame(float defense,float damage)
    {
        ConcentrateArena();Set(target,"defense",defense);Begin();Step(MonkeyStormRules.Duration+MonkeyStormRules.Tail+.001f);
        Assert.That(Hp(target),Is.EqualTo(5000f-damage).Within(.02f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(4));Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(damage).Within(.02f));
        Assert.That(Storms,Is.Empty);Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Step(5f);Assert.That(Hp(target),Is.EqualTo(5000f-damage).Within(.02f));
    }

    [Test]
    public void FourStrikesFollowOneIrregularScheduleFixedAtCastTime()
    {
        ConcentrateArena();Begin();float[] times=(float[])Times.Clone();
        Step(times[0]-.001f);Assert.That(Get(source,"skillHits"),Is.Zero);
        for(int i=0;i<4;i++)
        {
            Step(i==0?.001f:times[i]-times[i-1]);
            CollectionAssert.AreEqual(times,Times,"Advancing or handing off the storm cannot reroll its schedule.");
            Assert.That(Get(source,"skillHits"),Is.EqualTo(i+1));
            Assert.That(Hp(target),Is.EqualTo(5000f-(i+1)*500f).Within(.02f));
        }
        Step(MonkeyStormRules.Duration+MonkeyStormRules.Tail-times[3]+.001f);Assert.That(Storms,Is.Empty);
    }

    [Test]
    public void FourRandomStrikesStayInsideTwoSecondsWithReadableGapsAndVaryBetweenCasts()
    {
        Assert.That(MonkeyStormRules.Duration,Is.EqualTo(2f));
        var schedules=new HashSet<string>();bool irregular=false;
        for(int seed=0;seed<512;seed++)
        {
            float[] times=MonkeyStormRules.CreateImpactTimes(new System.Random(seed));
            Assert.That(times,Has.Length.EqualTo(4));Assert.That(times[0],Is.GreaterThanOrEqualTo(.8f));
            Assert.That(times[3],Is.LessThanOrEqualTo(2f));
            for(int i=1;i<4;i++)Assert.That(times[i]-times[i-1],Is.GreaterThanOrEqualTo(.35f-.00001f));
            irregular|=Mathf.Abs((times[1]-times[0])-(times[3]-times[2]))>.1f;
            schedules.Add(string.Join(",",Array.ConvertAll(times,t=>t.ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
        }
        Assert.That(schedules.Count,Is.EqualTo(512));Assert.That(irregular,Is.True);
        Begin();float[] first=(float[])Times.Clone();Call("ClearMonkeyStorms",false);Set(source,"skillTimer",0f);Begin();
        CollectionAssert.AreNotEqual(first,Times,"A later cast by the same monster must receive a fresh schedule.");
    }

    [Test]
    public void AttackAndTraitPowerAreCapturedOnceAndTheCasterReturnsToOrdinaryGroundCombat()
    {
        ConcentrateArena();Set(source,"skillTraitDamageMultiplier",.5f);Begin();
        Set(source,"damage",1000f);Set(source,"skillTraitDamageMultiplier",4f);Step(5f);
        Assert.That(Hp(target),Is.EqualTo(4000f).Within(.02f));
        Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(0f));Assert.That(Get(source,"skillTimer"),Is.EqualTo(8f));
    }

    [Test]
    public void WarningLocksTheGroundCoordinateSoMovingOutBeforeContactAvoidsTheStrike()
    {
        Begin();Step(Times[0]-MonkeyStormRules.Warning+.001f);object cast=Storms[0];
        var points=(Vector3[])Field(cast,"points");Vector3 warned=points[0];
        Model(target).transform.position+=Vector3.right*10f;Step(.25f);
        Assert.That(points[0],Is.EqualTo(warned));Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.Zero);
    }

    [Test]
    public void AllFourWarningsPrioritizeDistinctLivingEnemiesBeforeRepeatingTargets()
    {
        var enemies=new List<object>{target};
        for(int i=1;i<8;i++)enemies.Add(Unit("geomchi",1,new Vector3(2f+i*2f,0,3f)));
        Begin();object cast=Storms[0];Step(4f);
        var aimed=(IDictionary)Field(cast,"aimed");int low=int.MaxValue,high=0,total=0;
        foreach(object enemy in enemies)
        {
            int count=aimed.Contains(enemy)?(int)aimed[enemy]:0;
            low=Mathf.Min(low,count);high=Mathf.Max(high,count);total+=count;
        }
        Assert.That(aimed.Count,Is.EqualTo(4));Assert.That(total,Is.EqualTo(4));Assert.That(high-low,Is.LessThanOrEqualTo(1));
        Assert.That((int)Field(cast,"nextImpact"),Is.EqualTo(4));
    }

    [TestCase("death")][TestCase("freeze")][TestCase("stun")]
    public void AlreadyCommittedWeatherContinuesAfterCasterDeathFreezeOrStun(string interruption)
    {
        ConcentrateArena();Begin();
        if(interruption=="death"){Set(source,"alive",false);Set(source,"hp",0f);}
        else if(interruption=="freeze")Status(source).ApplyTimed(CombatStatusKind.Freeze,4f);
        else Set(source,"giraffeStunTimer",4f);
        Step(MonkeyStormRules.Duration+MonkeyStormRules.Tail+.001f);Assert.That(Hp(target),Is.EqualTo(3000f).Within(.02f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(4));Assert.That(Storms,Is.Empty);
    }

    [Test]
    public void MultipleCastersDealIndependentDamageButShareTheVictimsStunRecoveryWindow()
    {
        ConcentrateArena();var casters=new List<object>{source};
        for(int i=1;i<4;i++){object ally=Unit("chiwoo",0,Vector3.left*i,true);Set(ally,"target",target);casters.Add(ally);}
        foreach(object caster in casters)Assert.That(Call("BeginAcrobaticBarrage",caster,Skill),Is.True);
        Set(target,"hp",50000f);Set(target,"maxHp",50000f);
        int previousRevision=0;float lastStun=-10f;
        for(int frame=1;frame<=466;frame++)
        {
            Step(.01f);int revision=(int)Get(target,"positionRevision");
            if(revision==previousRevision)continue;
            Assert.That(revision,Is.EqualTo(previousRevision+1));
            Assert.That(frame*.01f-lastStun,Is.GreaterThanOrEqualTo(1.19f));
            previousRevision=revision;lastStun=frame*.01f;
        }
        Assert.That(Hp(target),Is.EqualTo(50000f-4*2000f).Within(.05f));
        foreach(object caster in casters)
        {Assert.That(Get(caster,"skillHits"),Is.EqualTo(4));Assert.That(Hp(caster),Is.EqualTo(5000f));}
        Assert.That(previousRevision,Is.InRange(1,2),"Overlapping irregular storms still share the 1.2-second stun recovery window.");
    }

    [TestCase(false)][TestCase(true)]
    public void ControlImmunityRejectsStunWithoutRejectingLightningDamage(bool immune)
    {
        ConcentrateArena();Status(target).ControlImmune=immune;Begin();Step(Times[0]+.001f);
        Assert.That(Hp(target),Is.EqualTo(4500f).Within(.01f));
        if(immune)Assert.That((float)Get(target,"giraffeStunTimer"),Is.Zero);
        else Assert.That((float)Get(target,"giraffeStunTimer"),Is.EqualTo(1f));
    }

    [TestCase("generation")][TestCase("reset")][TestCase("ending")]
    public void NewWaveResetAndResultCancelEveryRemainingGameplayHit(string reason)
    {
        ConcentrateArena();Begin();Step(Times[0]+.001f);float hp=Hp(target);
        if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else if(reason=="ending")BattleSet("battleEnding",true);
        else Call("ClearMonkeyStorms",false);
        Step(5f);Assert.That(Hp(target),Is.EqualTo(hp));Assert.That(Storms,Is.Empty);
    }

    [Test]
    public void PauseAndAbsoluteCinematicHandoffDoNotAdvanceOrDoubleCountTheSameStrike()
    {
        ConcentrateArena();Begin();object cast=Storms[0];BattleSet("paused",true);Step(1f);
        Assert.That((float)Field(cast,"time"),Is.Zero);Assert.That(Hp(target),Is.EqualTo(5000f));
        float[] times=(float[])Times.Clone();BattleSet("paused",false);Call("AdvanceMonkeyStormCinematic",source,times[0]);
        Assert.That(Hp(target),Is.EqualTo(4500f));Call("AdvanceMonkeyStormCinematic",source,times[0]);
        Assert.That(Hp(target),Is.EqualTo(4500f));Step(times[1]-times[0]);Assert.That(Hp(target),Is.EqualTo(4000f));
    }

    [TestCase(false)][TestCase(true)]
    public void ARemovedCinematicQueueOrDeadCasterReleasesTheCommittedStormInsteadOfLosingTheCast(bool dead)
    {
        BattleSet("mlEpisodeActive",false);Begin();Assert.That(Call("IsMonkeyStormPending",source),Is.True);
        if(dead){Set(source,"alive",false);Set(source,"hp",0f);}
        else ((IList)BattleGet("rank4PresentationQueue")).Clear();
        Step(.1f);
        Assert.That(Call("IsMonkeyStormPending",source),Is.False);Assert.That(Field(Storms[0],"released"),Is.True);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
    }

    [Test]
    public void ResultScreenPreservesExistingLightningTailsWithoutAnyFurtherDamage()
    {
        ConcentrateArena();BattleSet("mlEpisodeActive",false);Begin();
        ((IList)BattleGet("rank4PresentationQueue")).Clear();Call("ReleaseMonkeyStorm",source);Step(Times[0]+.001f);
        var effect=(PixelMonkeyStorm)Field(Storms[0],"effect");Assert.That(effect,Is.Not.Null);
        float hp=Hp(target),age=effect.Elapsed;BattleSet("battleEnding",true);Call("ClearMonkeyStorms",true);
        Assert.That(Storms,Is.Empty);Assert.That(effect!=null,Is.True);Assert.That(CombatEffectEndPlayback.IsPreserving(effect),Is.True);
        Assert.That(effect.enabled,Is.True,"The winning hit must keep its OnGUI flash during harmless tail playback.");
        Assert.That(effect.ScreenFlashColor.a,Is.GreaterThan(0f));
        var playback=effect.GetComponent<CombatEffectEndPlayback>();
        typeof(CombatEffectEndPlayback).GetMethod("Advance",Hidden).Invoke(playback,new object[]{.1f});
        Assert.That(effect.Elapsed,Is.GreaterThan(age));Assert.That(Hp(target),Is.EqualTo(hp));
        Step(5f);Assert.That(Hp(target),Is.EqualTo(hp));
    }

    [TestCase(0)][TestCase(1)]
    public void RealHostHandsOneStormToTheFieldThenSkipsItsFilmUntilTheNextWave(int team)
    {
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
        ConcentrateArena();BattleSet("mlEpisodeActive",false);Begin();
        var queue=(IList)BattleGet("rank4PresentationQueue");Assert.That(queue.Count,Is.EqualTo(1));
        Assert.That(Call("UpdateRank4Cinematic"),Is.True);Assert.That(BattleGet("rank4Presentation"),Is.Not.Null);
        Assert.That(Time.timeScale,Is.Zero);Assert.That(CombatSkillClock.Active,Is.True);
        object cast=Storms[0];
        Assert.That(AtFilm(MonkeyCircusRank4Stage.FieldReturnTime-.01f),Is.True);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Field(cast,"released"),Is.False);
        Assert.That(AtFilm(4.2f),Is.False);
        Assert.That(BattleGet("rank4Presentation"),Is.Null);Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That((float)Field(cast,"time"),Is.EqualTo(.7f).Within(.0001f));
        var native=(PixelMonkeyStorm)Field(cast,"effect");Assert.That(native,Is.Not.Null);Assert.That(native.Elapsed,Is.EqualTo(.7f).Within(.0001f));
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.Zero);
        float first=Times[0];Step(first-.7f);Assert.That(Hp(target),Is.EqualTo(4500f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Step(MonkeyStormRules.Duration+MonkeyStormRules.Tail-first+.001f);
        Assert.That(Hp(target),Is.EqualTo(3000f).Within(.02f));Assert.That(Get(source,"skillHits"),Is.EqualTo(4));Assert.That(Storms,Is.Empty);

        Set(source,"skillTimer",0f);Begin();Assert.That(queue,Is.Empty);
        Assert.That(Call("IsMonkeyStormPending",source),Is.False);Assert.That(Get(source,"skillUses"),Is.EqualTo(2));
        Call("ClearMonkeyStorms",false);Call("ResetRank4Cinematics");
        BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);Set(source,"skillTimer",0f);Begin();
        Assert.That(queue.Count,Is.EqualTo(1));Assert.That(Call("IsMonkeyStormPending",source),Is.True);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(3));Assert.That(Get(source,"skillHits"),Is.EqualTo(4));
    }

    [Test]
    public void ATranscendedSkillEchoCannotReintroduceFruitOrAnExtraStorm()
    {
        Begin();int casts=Storms.Count;
        foreach(string trait in new[]{MonkeyRank3Rules.FiveOrbJuggling,MonkeyRank3Rules.MyriadReturn,MonkeyRank3Rules.MountainCollapse})
            ((IList)Get(source,"activeTraitIds")).Add(trait);
        Set(source,"skillEchoId",MonkeyCombatRules.SkillId);Set(source,"skillEchoTarget",target);Set(source,"rank3EchoScale",.65f);
        Call("ExecuteSkillEcho",source);
        Assert.That(Storms.Count,Is.EqualTo(casts));Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillHits"),Is.Zero);
        Assert.That(Get(source,"skillEchoId"),Is.EqualTo(""));Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(Object.FindObjectsOfType<PixelMonkeyMortar>(),Is.Empty);Assert.That(Object.FindObjectsOfType<PixelMonkeyReturn>(),Is.Empty);
        Assert.That(Object.FindObjectsOfType<PixelStarProjectile>(),Is.Empty);
    }

    private bool AtFilm(float time)
    {
        object presentation=BattleGet("rank4Presentation");Assert.That(presentation,Is.Not.Null);
        presentation.GetType().GetField("elapsed",Hidden).SetValue(presentation,time-Time.unscaledDeltaTime);
        return (bool)Call("UpdateRank4Cinematic");
    }

    private void Begin()=>Assert.That(Call("BeginAcrobaticBarrage",source,Skill),Is.True);
    private void Step(float dt)=>Call("UpdateMonkeyStorms",dt);
    private void ConcentrateArena()=>BattleSet("combatBounds",new Bounds(Model(target).transform.position,Vector3.one*.1f));
    private SkillMasterRow Skill=>GameMasterData.Skill(MonkeyCombatRules.SkillId);
    private IList Units=>(IList)BattleGet("units");
    private IList Storms=>(IList)BattleGet("monkeyStorms");
    private float[] Times=>(float[])Field(Storms[0],"impactTimes");
    private object Unit(string species,int team,Vector3 position,bool transcended=false)
    {
        object unit=Activator.CreateInstance(unitType,true);var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        Assert.That(definition,Is.Not.Null);var prefab=Resources.Load<GameObject>(definition.resourcePath);
        var authored=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var model=new GameObject("Thunderstorm gameplay actor "+species);model.transform.position=position;
        var visual=new GameObject("Authored sprite");visual.transform.SetParent(model.transform,false);
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        driver.Configure(Frames(authored,"idleFrames"),Frames(authored,"runFrames"),Frames(authored,"attackFrames"),
            Frames(authored,"skillAttackFrames"),Frames(authored,"deathFrames"),Frames(authored,"captureFrames"),Frames(authored,"struggleFrames"));
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        visual.transform.rotation=camera.transform.rotation;var ring=new GameObject("Thunderstorm shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",Units.Count);Set(unit,"model",model);Set(unit,"ring",ring);Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"definition",definition);Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",MonkeyCombatRules.SkillId);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));Set(unit,"alive",true);Set(unit,"augmentTranscended",transcended);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",3);
        Set(unit,"collisionRadius",.3f);Set(unit,"skillCooldownDuration",8f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"lastGroundPosition",position);
        Units.Add(unit);return unit;
    }
    private static Sprite[] Frames(SpriteMonsterAnimationDriver driver,string name)=>(Sprite[])Field(driver,name);
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private object Get(object unit,string name)=>unitType.GetField(name,Hidden).GetValue(unit);
    private void Set(object unit,string name,object value)=>unitType.GetField(name,Hidden).SetValue(unit,value);
    private object BattleGet(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).GetValue(battle);
    private void BattleSet(string name,object value)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).SetValue(battle,value);
    private static object Field(object value,string name)=>value.GetType().GetField(name,Hidden).GetValue(value);
    private object Call(string name,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(name,Hidden).Invoke(battle,args);
}
