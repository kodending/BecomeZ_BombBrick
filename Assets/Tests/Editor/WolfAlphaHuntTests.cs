using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class WolfStampedeTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private readonly Type type=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object wolf,ally,prey,other,previousFrontend;
    private bool previousTestMode;

    [SetUp] public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousTestMode=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=true;
        previousFrontend=typeof(GameFrontend).GetField("instance",Hidden).GetValue(null);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,null);
        var host=new GameObject("Inactive stampede gameplay tests");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=type.GetNestedType("Unit",Hidden);
        BattleSet("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());BattleSet("mlEpisodeActive",true);BattleSet("battleStarted",true);
        BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        wolf=Unit("nukgu",0,Vector3.zero);ally=Unit("woongi",0,Vector3.left*3f);
        prey=Unit("geomchi",1,Vector3.right*3f);other=Unit("tino",1,Vector3.right*8f);
        Set(wolf,"augmentTranscended",true);Set(wolf,"target",prey);
    }

    [TearDown] public void TearDown()
    {
        if(battle!=null){Call("ResetWolfStampedes",true);Call("CancelCommonSkillFocus");Call("ResetFamilyTranscendenceStates");}
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;
    }

    [TestCase("nukgu",0)] [TestCase("nukgu",1)]
    [TestCase("seollang",0)] [TestCase("seollang",1)]
    [TestCase("honga",0)] [TestCase("honga",1)]
    [TestCase("wolyeong",0)] [TestCase("wolyeong",1)]
    public void EveryWolfOnEitherTeamUsesOneIndependentThreeWaveAbility(string species,int team)
    {
        Units.Remove(wolf);Model(wolf).SetActive(false);wolf=Unit(species,team,Vector3.zero);
        Set(wolf,"augmentTranscended",true);Set(wolf,"target",prey);Team(ally,team);Team(prey,1-team);Team(other,1-team);
        Vector3 start=Model(wolf).transform.position;
        Begin();Assert.That(State,Is.Not.Null);Assert.That(Get(wolf,"skillUses"),Is.EqualTo(1));
        Assert.That(Get(wolf,"skillTimer"),Is.EqualTo(5f));Assert.That(Hp(prey),Is.EqualTo(5000f));
        Assert.That(Call("BeginWolfStampede",wolf,Skill),Is.False,"A committed cast must not register twice.");
        Step(.1f);Assert.That(Model(wolf).transform.position,Is.EqualTo(start),"The body must wait for the actual release.");
        Release();Step(.08f);
        Assert.That(Field(State,"bodyHuntStarted"),Is.True);
        Assert.That(Get(wolf,"wolfHuntPending"),Is.True,"The real hunt must wait for its original animation contact callback.");
        Assert.That(Get(wolf,"wolfHuntTarget"),Is.SameAs(prey));
        Assert.That(Hp(prey),Is.EqualTo(5000f),"Preparing the original hunt does not apply contact damage early.");
        FinishAll();
        Assert.That(Hp(prey),Is.EqualTo(4640f).Within(.01f));Assert.That(Hp(other),Is.EqualTo(4640f).Within(.01f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));Assert.That(Get(wolf,"skillHits"),Is.EqualTo(6));
        Assert.That((float)Get(wolf,"skillDamage"),Is.EqualTo(720f).Within(.01f));
        Assert.That(State,Is.Null);Assert.That(Casts,Is.Empty);
    }

    [Test]
    public void OrdinaryAndRankThreeWolvesKeepTheirExistingAbilities()
    {
        Set(wolf,"augmentTranscended",false);
        var traits=(IList)Get(wolf,"activeTraitIds");
        foreach(string trait in new[]{"",WolfCombatRules.PackRushTraitId,WolfCombatRules.CelestialSpectralPackTraitId,WolfCombatRules.EclipsePackTraitId})
        {
            traits.Clear();if(trait.Length>0)traits.Add(trait);
            Assert.That(Call("UsesWolfStampede",wolf),Is.False);
            Assert.That(Call("BeginWolfStampede",wolf,Skill),Is.False);
        }
        Assert.That(Casts,Is.Empty);Assert.That(Get(wolf,"skillUses"),Is.Zero);
    }

    [UnityTest]
    public IEnumerator TheOriginalHuntCrossesThePrimaryTargetAndDealsItsOwnThreeHitsAlongsideThePack()
    {
        Model(ally).transform.position=Vector3.right*1.5f;Set(ally,"combatBodyScale",2f);
        Begin();Release();Step(.001f);Vector3 landing=(Vector3)Get(wolf,"wolfHuntLanding");
        Assert.That(landing.x,Is.GreaterThan(Model(prey).transform.position.x),"The original attack crosses the target instead of stopping on the approach side.");
        Assert.That(DriverFlag("wolfHuntAttack"),Is.True);Assert.That(Driver(wolf).HasPendingSkillAttackHit,Is.True);
        yield return FinishOriginalHunt();FinishAll();
        Assert.That(Model(wolf).transform.position,Is.EqualTo(landing));
        Assert.That(Get(wolf,"target"),Is.SameAs(prey));Assert.That(Get(wolf,"skillUses"),Is.EqualTo(1));
        Assert.That(Hp(prey),Is.EqualTo(4640f-100f*Skill.damageMultiplier*WolfCombatRules.HitCount).Within(.01f));
        Assert.That(Hp(other),Is.EqualTo(4640f).Within(.01f));Assert.That(Get(wolf,"skillHits"),Is.EqualTo(9));
        Assert.That(Hp(ally),Is.EqualTo(5000f));
    }

    [TestCase("death")] [TestCase("freeze")] [TestCase("displacement")]
    public void InterruptedOriginalHuntCannotReleaseAStaleAttackWhileTheReleasedPackContinues(string reason)
    {
        Begin();Release();Step(.04f);Assert.That(Driver(wolf).HasPendingSkillAttackHit,Is.True);
        Vector3 stopped=Model(wolf).transform.position;Interrupt(reason);Call("CancelWolfStampedeFor",wolf);
        Assert.That(Get(wolf,"wolfHuntPending"),Is.False);
        if(reason=="freeze")
        {
            Assert.That(Driver(wolf).HasPendingSkillAttackHit,Is.True,"The existing status freeze preserves its paused animation callback.");
            Status(wolf).Remove(CombatStatusKind.Freeze);Driver(wolf).SetFrozen(false);
            Driver(wolf).NotifySkillAttackHitFrameFromAnimation();
        }
        Assert.That(Driver(wolf).HasPendingSkillAttackHit,Is.False,"A thawed callback sees the cancelled hunt token and cannot release a stale attack.");
        FinishAll();Assert.That(Model(wolf).transform.position,Is.EqualTo(stopped));
        Assert.That(Hp(prey),Is.EqualTo(4640f).Within(.01f));Assert.That(Hp(other),Is.EqualTo(4640f).Within(.01f));
        Assert.That(Get(wolf,"skillHits"),Is.EqualTo(6));Assert.That(State,Is.Null);
    }

    [Test]
    public void TheOriginalHuntKeepsItsCommittedPrimaryEvenIfTheAiTargetChangesAndThePrimaryMovesOutOfRange()
    {
        Begin();Set(wolf,"target",other);Model(prey).transform.position=Vector3.right*10f;Release();Step(.001f);
        Assert.That(Get(wolf,"wolfHuntTarget"),Is.SameAs(prey));Assert.That(Get(wolf,"target"),Is.SameAs(prey));
        Assert.That(((Vector3)Get(wolf,"wolfHuntLanding")).x,Is.GreaterThan(10f));
        Assert.That(Get(wolf,"skillUses"),Is.EqualTo(1));
    }

    [Test]
    public void NoOpenFarSideLandingKeepsTheAcceptedOriginalHuntWithoutLeavingTheArena()
    {
        Model(wolf).transform.position=Vector3.right*14f;Model(prey).transform.position=Vector3.right*100f;
        Begin();Release();Step(.001f);
        Assert.That(Get(wolf,"wolfHuntPending"),Is.True);Assert.That(Get(wolf,"wolfHuntTarget"),Is.SameAs(prey));
        Assert.That(Get(wolf,"wolfHuntLanding"),Is.EqualTo(Vector3.right*14f));
        FinishAll();Assert.That(Model(wolf).transform.position,Is.EqualTo(Vector3.right*14f));
    }

    [TestCase("clone")] [TestCase("summon")]
    public void CopiesCannotRecursivelyStartTheirOwnTranscendentStampede(string kind)
    {
        Set(wolf,kind=="clone"?"augmentShadowClone":"rank3MycokingSummon",true);
        Assert.That(Call("UsesWolfStampede",wolf),Is.False);
        Assert.That(Call("BeginWolfStampede",wolf,Skill),Is.False);Assert.That(Casts,Is.Empty);
    }

    [Test]
    public void PreparationWaitsAndLargeStepsKeepOnlyTheRemainderAfterRelease()
    {
        Begin();Step(WolfStampedeRules.Preparation-.001f);
        Assert.That(Field(State,"released"),Is.False);Assert.That(Hp(prey),Is.EqualTo(5000f));
        Step(.002f);Assert.That(Field(State,"released"),Is.True);
        Assert.That((float)Field(State,"time"),Is.EqualTo(.001f).Within(.0001f));Assert.That(Hp(prey),Is.EqualTo(5000f));
        FinishAll();Assert.That(Hp(prey),Is.EqualTo(4640f).Within(.01f));
    }

    [Test]
    public void ReleaseSnapshotsEveryLivingEnemyIncludingArrivalsDuringPreparationButNoLaterEntrants()
    {
        Begin();object arrival=Unit("tino",1,Vector3.right*10f);
        object stored=Unit("geomchi",1,Vector3.right);Set(stored,"inStorage",true);
        object dead=Unit("tino",1,Vector3.right);Set(dead,"alive",false);Set(dead,"hp",0f);
        Release();object late=Unit("geomchi",1,Vector3.right);FinishAll();
        foreach(object enemy in new[]{prey,other,arrival})Assert.That(Hp(enemy),Is.EqualTo(4640f).Within(.01f));
        Assert.That(Hp(late),Is.EqualTo(5000f));Assert.That(Hp(stored),Is.EqualTo(5000f));Assert.That(Hp(dead),Is.Zero);
        Assert.That(Hp(ally),Is.EqualTo(5000f));Assert.That(Get(wolf,"skillHits"),Is.EqualTo(9));
    }

    [TestCase(50f,240f)] [TestCase(100f,180f)]
    public void AllThreeHundredSixtyPercentUseOrdinaryDefenseWithoutArmorPenetration(float defense,float expected)
    {
        Set(prey,"defense",defense);Begin();Release();FinishAll();
        Assert.That(Hp(prey),Is.EqualTo(5000f-expected).Within(.01f));
        Assert.That(Hp(other),Is.EqualTo(4640f).Within(.01f));Assert.That(Get(wolf,"skillHits"),Is.EqualTo(6));
    }

    [TestCase(8)] [TestCase(32)]
    public void OverlappingTargetsAndLargeFramesReceiveExactlyThreeHitsWithoutAddedControl(int count)
    {
        Model(other).transform.position=Model(prey).transform.position;
        var victims=new List<object>{prey,other};
        for(int i=2;i<count;i++)victims.Add(Unit("geomchi",1,Model(prey).transform.position));
        Begin();object cast=State;Release();FinishAll();
        Assert.That(Get(wolf,"skillHits"),Is.EqualTo(count*3));Assert.That(Casts,Is.Empty);
        foreach(object enemy in victims)
        {
            Assert.That(Hp(enemy),Is.EqualTo(4640f).Within(.01f));
            Assert.That(Status(enemy).HarmfulCount,Is.Zero);
            Assert.That(Get(enemy,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
            Assert.That(Get(enemy,"displacing"),Is.False);
        }
        foreach(bool hit in (bool[])Field(cast,"impacts"))Assert.That(hit,Is.True);
        FinishAll();foreach(object enemy in victims)Assert.That(Hp(enemy),Is.EqualTo(4640f).Within(.01f));
    }

    [Test]
    public void SmallAndSingleLargeClockStepsProduceIdenticalDamageAndHitCounts()
    {
        Begin();Release();for(int frame=0;frame<150;frame++)Step(1f/60f);
        float first=Hp(prey),second=Hp(other);int hits=(int)Get(wolf,"skillHits");
        Assert.That(State,Is.Null);Set(prey,"hp",5000f);Set(other,"hp",5000f);
        Begin();Step(WolfStampedeRules.Preparation+WolfStampedeRules.Duration+.001f);
        Assert.That(Hp(prey),Is.EqualTo(first).Within(.01f));Assert.That(Hp(other),Is.EqualTo(second).Within(.01f));
        Assert.That((int)Get(wolf,"skillHits")-hits,Is.EqualTo(hits));Assert.That(Casts,Is.Empty);
    }

    [Test]
    public void AttackAndTraitMultiplierAreCapturedAtCommitAndVictimsMayMoveBeyondTheArena()
    {
        Set(wolf,"skillTraitDamageMultiplier",.5f);Begin();
        Set(wolf,"damage",1000f);Set(wolf,"skillTraitDamageMultiplier",4f);
        Release();Model(prey).transform.position=Vector3.right*100f;FinishAll();
        Assert.That(Hp(prey),Is.EqualTo(4820f).Within(.01f));Assert.That(Hp(other),Is.EqualTo(4820f).Within(.01f));
    }

    [Test]
    public void RepeatedReleaseCallsDoNotDuplicateDamageOrConsumeAnotherSkillUse()
    {
        Begin();object cast=State;Release();Release();Assert.That(State,Is.SameAs(cast));
        FinishAll();Assert.That(Get(wolf,"skillUses"),Is.EqualTo(1));Assert.That(Get(wolf,"skillHits"),Is.EqualTo(6));
        Assert.That(Hp(prey),Is.EqualTo(4640f).Within(.01f));
    }

    [TestCase("death")] [TestCase("freeze")] [TestCase("fear")] [TestCase("displacement")]
    public void ReleasedPackCompletesAllWavesAfterItsCasterDiesOrIsInterrupted(string reason)
    {
        Begin();Release();Interrupt(reason);Call("CancelWolfStampedeFor",wolf);FinishAll();
        Assert.That(Hp(prey),Is.EqualTo(4640f).Within(.01f));Assert.That(Hp(other),Is.EqualTo(4640f).Within(.01f));
        Assert.That(Casts,Is.Empty);Assert.That(State,Is.Null);
    }

    [TestCase("death")] [TestCase("fear")] [TestCase("displacement")] [TestCase("cancel")]
    public void UnreleasedInvalidatedCastDoesNotDamageAnyone(string reason)
    {
        Begin();object cast=State;
        if(reason=="cancel")Call("CancelWolfStampedeFor",wolf);else Interrupt(reason);
        Step(WolfStampedeRules.Preparation+WolfStampedeRules.Duration+.001f);
        Assert.That(Hp(prey),Is.EqualTo(5000f));Assert.That(Hp(other),Is.EqualTo(5000f));
        Assert.That(Field(cast,"released"),Is.False);Assert.That(Field(cast,"cancelled"),Is.True);
        Assert.That(Casts,Is.Empty);Assert.That(State,Is.Null);Assert.That(Get(wolf,"attackMovementLockTimer"),Is.EqualTo(0f));
    }

    [Test]
    public void FrozenPreparationResumesAfterThawWithoutReleasingEarly()
    {
        Begin();Status(wolf).ApplyTimed(CombatStatusKind.Freeze,3f);Step(2f);
        Assert.That(Field(State,"released"),Is.False);Assert.That(Field(State,"time"),Is.EqualTo(0f));
        Assert.That(Hp(prey),Is.EqualTo(5000f));Status(wolf).Remove(CombatStatusKind.Freeze);
        Step(WolfStampedeRules.Preparation+WolfStampedeRules.Duration+.001f);
        Assert.That(Hp(prey),Is.EqualTo(4640f).Within(.01f));
    }

    [TestCase("generation")] [TestCase("wave reset")] [TestCase("battle result")]
    public void WaveOrResultCleanupPreventsAnyRemainingGameplayDamage(string reason)
    {
        Begin();Release();Step(.05f);float hp=Hp(prey),otherHp=Hp(other);
        if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else
        {
            if(reason=="battle result")BattleSet("battleEnding",true);
            Call("ResetWolfStampedes",reason=="wave reset");
        }
        FinishAll();Assert.That(Hp(prey),Is.EqualTo(hp));Assert.That(Hp(other),Is.EqualTo(otherHp));
        Assert.That(Casts,Is.Empty);Assert.That(State,Is.Null);Assert.That(Get(wolf,"attackMovementLockTimer"),Is.EqualTo(0f));
    }

    [Test]
    public void UnrelatedBasicAndSkillDamageCannotSummonOldMarkedTargetHuntersOrAmplifyDamage()
    {
        Begin();Release();FinishAll();
        Assert.That(Damage(wolf,prey,100f,true),Is.EqualTo(100f));
        Assert.That(Damage(ally,other,100f,true),Is.EqualTo(100f));
        Assert.That(Damage(ally,prey,100f,false),Is.EqualTo(100f));Step(1f);
        Assert.That(Hp(prey),Is.EqualTo(4440f).Within(.01f));Assert.That(Hp(other),Is.EqualTo(4540f).Within(.01f));
        Assert.That(Casts,Is.Empty);Assert.That(Get(wolf,"skillHits"),Is.EqualTo(6));
    }

    [Test]
    public void EachImpactRechecksLivingAndTargetableStateWithoutTransferringOrAddingTargets()
    {
        Begin();Release();Set(prey,"alive",false);Set(prey,"hp",0f);
        Set(other,"ninjaUntargetableTimer",1f);object entrant=Unit("tino",1,Vector3.right);
        FinishAll();Assert.That(Hp(prey),Is.Zero);Assert.That(Hp(other),Is.EqualTo(5000f));
        Assert.That(Hp(entrant),Is.EqualTo(5000f));Assert.That(Get(wolf,"skillHits"),Is.EqualTo(0));
    }

    private void Begin()=>Assert.That(Call("BeginWolfStampede",wolf,Skill),Is.True);
    private void Release()=>Call("ReleaseWolfStampede",wolf);
    private void Step(float dt)=>Call("UpdateWolfStampedes",dt);
    private void FinishAll()=>Step(WolfStampedeRules.Duration+.001f);
    private IEnumerator FinishOriginalHunt()
    {
        var routine=(IEnumerator)Call("ResolveThreeSidedHunt",wolf,Get(wolf,"wolfHuntTarget"),
            Get(wolf,"wolfHuntLanding"),Skill,1f,false,(int)BattleGet("combatGeneration"));
        var run=CombatSkillClock.Run(routine,null);int frames=0;
        try
        {
            while(run.MoveNext())
            {
                Assert.That(++frames,Is.LessThan(300));
                float dt=Mathf.Max(.001f,Time.deltaTime);
                BattleSet("battleElapsed",(float)BattleGet("battleElapsed")+dt);Step(dt);
                yield return null;
            }
        }
        finally{(run as IDisposable)?.Dispose();}
    }
    private void Interrupt(string reason)
    {
        if(reason=="death"){Set(wolf,"alive",false);Set(wolf,"hp",0f);}
        else if(reason=="displacement")Set(wolf,"positionRevision",(int)Get(wolf,"positionRevision")+1);
        else Status(wolf).ApplyTimed(reason=="freeze"?CombatStatusKind.Freeze:CombatStatusKind.Fear,3f);
    }
    private object Unit(string species,int team,Vector3 position)
    {
        var unit=Activator.CreateInstance(unitType,true);var model=new GameObject("Stampede unit "+species);
        model.transform.position=position;var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        Assert.That(definition,Is.Not.Null);Set(unit,"id",Units.Count);Set(unit,"stableId","stampede-"+Units.Count);
        Set(unit,"model",model);Set(unit,"ring",new GameObject("Stampede ring"));Set(unit,"definition",definition);
        var authored=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var visual=new GameObject("Stampede authored sprite");visual.transform.SetParent(model.transform,false);
        visual.AddComponent<SpriteRenderer>().sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] Frames(string name)=>(Sprite[])Field(authored,name);
        driver.Configure(Frames("idleFrames"),Frames("runFrames"),Frames("attackFrames"),Frames("skillAttackFrames"),
            Frames("deathFrames"),Frames("captureFrames"),Frames("struggleFrames"));
        Set(unit,"spriteAnimation",driver);
        Set(unit,"skillId",definition.skillId);Team(unit,team);Set(unit,"alive",true);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);Set(unit,"attackInterval",1f);
        Set(unit,"collisionRadius",.3f);Set(unit,"attackRange",2);Set(unit,"skillCooldownDuration",5f);
        Status(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);Units.Add(unit);return unit;
    }
    private SkillMasterRow Skill=>GameMasterData.Skill(WolfCombatRules.SkillId);
    private IList Units=>(IList)BattleGet("units");
    private IList Casts=>(IList)BattleGet("wolfStampedes");
    private object State=>Get(wolf,"wolfStampede");
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private SpriteMonsterAnimationDriver Driver(object unit)=>(SpriteMonsterAnimationDriver)Get(unit,"spriteAnimation");
    private bool DriverFlag(string name)=>(bool)Field(Driver(wolf),name);
    private void Team(object unit,int team)=>Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
    private object Get(object unit,string field)=>unitType.GetField(field,Hidden).GetValue(unit);
    private void Set(object unit,string field,object value)=>unitType.GetField(field,Hidden).SetValue(unit,value);
    private object Call(string method,params object[] args)=>type.GetMethod(method,Hidden).Invoke(battle,args);
    private object BattleGet(string name)=>type.GetField(name,Hidden).GetValue(battle);
    private void BattleSet(string name,object value)=>type.GetField(name,Hidden).SetValue(battle,value);
    private static object Field(object instance,string field)=>instance.GetType().GetField(field,Hidden).GetValue(instance);
    private float Damage(object attacker,object victim,float damage,bool basic)=>
        (float)Call("ApplyDamage",attacker,victim,damage,false,basic,true,true,0f,0f,1f,false,false,false);
}
