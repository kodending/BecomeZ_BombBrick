using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SabertoothPredationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Static;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private readonly List<object> enemies=new List<object>();
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object hunter,prey,ally;
    private float previousSpeed;
    private bool previousEffectTest;
    private int resumedCommits;
    private UnityEngine.Random.State random;

    [SetUp] public void SetUp()
    {
        previousSpeed=Time.timeScale;Time.timeScale=1f;
        previousEffectTest=CombatEffectTestConfiguration.Active;CombatEffectTestConfiguration.Active=false;
        random=UnityEngine.Random.state;UnityEngine.Random.InitState(7341);
        resumedCommits=0;
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Sabertooth devour test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        BattleSet("expeditionRun",new ExpeditionRunSave());BattleSet("playerProfile",new PlayerProfileSave());
        BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(24f,10f,20f)));
        BattleSet("mlEpisodeActive",true);BattleSet("battleStarted",true);
        hunter=CreateUnit(0,Vector3.zero);prey=CreateUnit(1,new Vector3(2f,0f,0f));
        enemies.Add(prey);enemies.Add(CreateUnit(1,new Vector3(6f,0f,-3f)));
        enemies.Add(CreateUnit(1,new Vector3(7f,0f,3f)));
        ally=CreateUnit(0,new Vector3(-3f,0f,3f));
        Set(hunter,"augmentTranscended",true);Set(hunter,"target",prey);Set(hunter,"skillTimer",0f);
        foreach(object enemy in enemies)Set(enemy,"target",hunter);
    }

    [TearDown] public void TearDown()
    {
        try
        {
            if(battle!=null)Call("EndSabertoothDevour",hunter,true);
            foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
                if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&created.transform.parent==null)
                    Object.DestroyImmediate(created);
        }
        finally
        {
            enemies.Clear();Time.timeScale=previousSpeed;
            CombatEffectTestConfiguration.Active=previousEffectTest;UnityEngine.Random.state=random;
        }
    }

    [TestCase("geomchi",0)] [TestCase("geomchi",1)]
    [TestCase("seola",0)] [TestCase("seola",1)]
    [TestCase("nox",0)] [TestCase("nox",1)]
    [TestCase("terakan",0)] [TestCase("terakan",1)]
    public void EveryTranscendedSabertoothUsesOneDevourAgainstAllEnemiesInsteadOfThreeFangLockHits(string species,int team)
    {
        SetTeam(team);Set(hunter,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(hunter,"defense",100f);
        object dead=CreateUnit(1-team,new Vector3(4f,0f,4f));Set(dead,"alive",false);
        object hidden=CreateUnit(1-team,new Vector3(4f,0f,-4f));Set(hidden,"ninjaUntargetableTimer",5f);
        Vector3 allyPosition=Position(ally);
        Assert.That(Call("BeginFangLock",hunter,Skill),Is.True);
        Assert.That(Get(hunter,"fangLockTarget"),Is.Null);
        Assert.That(Get(hunter,"skillUses"),Is.EqualTo(1));
        Assert.That(Get(hunter,"skillTimer"),Is.EqualTo(7.5f));
        Assert.That((IList)StateField("targets"),Is.EquivalentTo(enemies));
        float damage=(float)Call("SkillPower",hunter)*1.8f;
        AdvanceTo(SabertoothDevourRules.HitTime-.001f);
        foreach(object enemy in enemies)Assert.That(Hp(enemy),Is.EqualTo(5000f));
        Advance(.002f);
        foreach(object enemy in enemies)Assert.That(Hp(enemy),Is.EqualTo(5000f-damage).Within(.02f));
        Assert.That(Get(hunter,"skillHits"),Is.EqualTo(enemies.Count));
        Advance(SabertoothDevourRules.Duration+1f);
        AssertReleased();
        foreach(object enemy in enemies)Assert.That(Hp(enemy),Is.EqualTo(5000f-damage).Within(.02f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));Assert.That(Position(ally),Is.EqualTo(allyPosition));
        Assert.That(Hp(hidden),Is.EqualTo(5000f));
    }

    [TestCase(SabertoothCombatRules.ThroatLockTraitId)]
    [TestCase(SabertoothCombatRules.PreyDriveTraitId)]
    [TestCase(SabertoothCombatRules.BloodRefluxTraitId)]
    public void RankThreeSelectionsDoNotAddSilenceAssistanceOrRefluxToTheIndependentDevour(string trait)
    {
        Traits(hunter).Add(trait);Begin();AdvanceTo(SabertoothDevourRules.PullEnd+.1f);
        Assert.That(Get(hunter,"fangLockTarget"),Is.Null);
        Assert.That(Get(hunter,"fangLockBloodReflux"),Is.False);
        Assert.That(Get(hunter,"preyDragTarget"),Is.Null);Assert.That(Get(ally,"preyFocusTarget"),Is.Null);
        foreach(object enemy in enemies)Assert.That(Statuses(enemy).Has(CombatStatusKind.Silence),Is.False);
        Call("ResolveBasicAttackImpact",prey,hunter,100f);
        float hunterHp=Hp(hunter);
        Assert.That(hunterHp,Is.LessThan(5000f));
        foreach(object enemy in enemies)Assert.That(Hp(enemy),Is.EqualTo(5000f));
        AdvanceTo(SabertoothDevourRules.Duration+.1f);
        foreach(object enemy in enemies)Assert.That(Hp(enemy),Is.EqualTo(4820f).Within(.02f));
        Assert.That(Hp(hunter),Is.EqualTo(hunterHp));
    }

    [TestCase("geomchi",SabertoothCombatRules.GeomchiTraitId)]
    [TestCase("seola",SabertoothCombatRules.SeolaTraitId)]
    [TestCase("nox",SabertoothCombatRules.NoxTraitId)]
    [TestCase("terakan",SabertoothCombatRules.TerakanTraitId)]
    public void SpeciesIntrinsicsStillApplyToTheIndependentSingleBite(string species,string intrinsic)
    {
        Set(hunter,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(hunter,"hp",4000f);Set(hunter,"defense",100f);Traits(hunter).Add(intrinsic);
        if(species=="nox")
        {
            Set(prey,"hp",1000f);Set(prey,"defense",100f);Set(enemies[1],"defense",100f);
        }
        Begin();Call("ResolveBasicAttackImpact",prey,hunter,100f);float damagedHp=Hp(hunter);
        AdvanceTo(SabertoothDevourRules.Duration+.1f);AssertReleased();
        if(species=="geomchi")Assert.That(Hp(hunter),Is.GreaterThan(damagedHp).And.LessThanOrEqualTo(4000f));
        else if(species=="seola")
            foreach(object enemy in enemies)Assert.That(Statuses(enemy).Has(CombatStatusKind.ComboChilled),Is.True);
        else if(species=="nox")Assert.That(1000f-Hp(prey),Is.GreaterThan(5000f-Hp(enemies[1])),
            "The low-health victim must retain Nox's final-bite armor penetration.");
        else Assert.That(Statuses(hunter).Get(CombatStatusKind.Shield).magnitude,Is.GreaterThan(0f));
    }

    [Test]
    public void TheSingleFinalHitIsPhysicalAndIndependentOfTheVictimsMaximumOrMissingHealth()
    {
        Set(enemies[1],"maxHp",50000f);Set(enemies[1],"hp",3000f);
        Set(enemies[2],"defense",100f);
        Statuses(prey).ApplyShield(6f,50f,prey);
        Begin();Advance(SabertoothDevourRules.Duration+1f);
        Assert.That(Hp(prey),Is.EqualTo(4870f).Within(.02f));
        Assert.That(Hp(enemies[1]),Is.EqualTo(2820f).Within(.02f));
        Assert.That(5000f-Hp(enemies[2]),Is.GreaterThan(0f).And.LessThan(180f));
        float total=(float)Get(hunter,"skillDamage");
        Advance(10f);
        Assert.That(Get(hunter,"skillDamage"),Is.EqualTo(total));Assert.That(Get(hunter,"skillHits"),Is.EqualTo(3));
    }

    [Test]
    public void OnlyTheFinalBiteRequestsTheDedicatedCameraImpact()
    {
        var cameraHost=new GameObject("Devour combat impact camera");var camera=cameraHost.AddComponent<Camera>();
        var director=cameraHost.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);
        director.SetShakeStrength(1f);director.BeginBattle();
        BattleSet("mlEpisodeActive",false);BattleSet("combatCameraDirector",director);
        var until=typeof(CombatCameraDirector).GetField("shakeUntil",Private);
        Begin();AdvanceTo(SabertoothDevourRules.HitTime-.001f);
        Assert.That(until.GetValue(director),Is.EqualTo(0f));
        Advance(.002f);
        Assert.That(typeof(CombatCameraDirector).GetField("shakeAmplitudePixels",Private).GetValue(director),Is.EqualTo(18f));
        Assert.That((float)until.GetValue(director),Is.GreaterThan(Time.unscaledTime));
        director.BeginBattle();Advance(.1f);
        Assert.That(until.GetValue(director),Is.EqualTo(0f),"Recovery must not replay the final bite impact.");
        director.EndBattle();
    }

    [Test]
    public void KillingAnEnemyDoesNotRestoreTheRemovedMarkHealingOrCooldownReset()
    {
        Set(prey,"hp",100f);Set(hunter,"hp",3000f);Begin();
        AdvanceTo(SabertoothDevourRules.HitTime+.01f);
        Assert.That(Get(prey,"alive"),Is.False);Assert.That(Hp(hunter),Is.EqualTo(3000f));
        Assert.That(Get(hunter,"skillTimer"),Is.EqualTo(7.5f));
        Assert.That(Call("FamilyTranscendenceStateFor",hunter,false),Is.Null);
    }

    [TestCase("control")] [TestCase("debuff")] [TestCase("force")]
    public void ImmuneEnemiesKeepTheirPositionAndActionsButStillTakeTheFinalHit(string immunity)
    {
        if(immunity=="control")Statuses(prey).ControlImmune=true;
        else if(immunity=="debuff")Statuses(prey).DebuffImmune=true;
        else Set(prey,"rank3ForceImmuneTimer",10f);
        Vector3 original=Position(prey);Begin();AdvanceTo(SabertoothDevourRules.PullEnd+.2f);
        Assert.That(Position(prey),Is.EqualTo(original));Assert.That(Get(prey,"sabertoothDevouredBy"),Is.Null);
        Assert.That(Statuses(prey).Has(CombatStatusKind.Root),Is.False);
        AdvanceTo(SabertoothDevourRules.HitTime+.01f);
        Assert.That(Position(prey),Is.EqualTo(original));Assert.That(Hp(prey),Is.EqualTo(4820f).Within(.02f));
    }

    [TestCase("pause")] [TestCase("freeze")]
    public void PausingOrFreezingTheCasterPausesTheSameCastWithoutDamagingOrRepositioningItsPrey(string cause)
    {
        Begin();AdvanceTo(SabertoothDevourRules.PullStart+.1f);
        object state=State;float age=(float)StateField("time");Vector3 position=Position(prey);
        if(cause=="pause")BattleSet("paused",true);else Statuses(hunter).ApplyTimed(CombatStatusKind.Freeze,2f);
        Advance(1f);
        Assert.That(State,Is.SameAs(state));Assert.That(StateField("time"),Is.EqualTo(age));
        Assert.That(Position(prey),Is.EqualTo(position));Assert.That(Hp(prey),Is.EqualTo(5000f));
        if(cause=="pause")BattleSet("paused",false);else Statuses(hunter).Remove(CombatStatusKind.Freeze);
        AdvanceTo(SabertoothDevourRules.Duration+.1f);
        Assert.That(Hp(prey),Is.EqualTo(4820f).Within(.02f));Assert.That(Get(hunter,"skillHits"),Is.EqualTo(3));
        AssertReleased();
    }

    [Test]
    public void CapturedEnemiesCannotWalkOrStartBasicAttacksDuringThePullAndHold()
    {
        Begin();AdvanceTo(SabertoothDevourRules.PullEnd+.1f);
        foreach(object enemy in enemies)
        {
            Assert.That(Get(enemy,"sabertoothDevouredBy"),Is.SameAs(hunter));
            Vector3 position=Position(enemy);Call("UpdateUnit",enemy,.1f);
            Assert.That(Position(enemy),Is.EqualTo(position));
            Assert.That(Get(enemy,"basicAttackCount"),Is.EqualTo(0));
        }
        Call("EndSabertoothDevour",hunter,true);AssertReleased();
    }

    [TestCase(false)] [TestCase(true)]
    public void APreviousRootKeepsItsSourceAndRemainingLifetimeAfterCaptureEnds(bool naturalExpiry)
    {
        var previous=Statuses(prey).ApplyTimed(CombatStatusKind.Root,8f,ally);
        Begin();Advance(SabertoothDevourRules.PullStart);
        var capture=Statuses(prey).Get(CombatStatusKind.Root);
        Assert.That(capture,Is.Not.SameAs(previous));Assert.That(capture.source,Is.SameAs(hunter));
        float elapsed=naturalExpiry?capture.remaining+.01f:.5f;
        if(naturalExpiry)
        {
            Statuses(prey).Tick(elapsed);
            Assert.That(capture.remaining,Is.Zero);Assert.That(Statuses(prey).Get(CombatStatusKind.Root),Is.Null);
        }
        BattleSet("battleElapsed",elapsed);Advance(elapsed);Call("EndSabertoothDevour",hunter,true);
        var restored=Statuses(prey).Get(CombatStatusKind.Root);
        Assert.That(restored,Is.Not.Null);Assert.That(restored.source,Is.SameAs(ally));
        Assert.That(restored.remaining,Is.EqualTo(8f-elapsed).Within(.001f));AssertReleased();
    }

    [TestCase(false)] [TestCase(true)]
    public void CleansingOrReplacingTheCaptureDoesNotResurrectItsSuppressedPreviousRoot(bool replace)
    {
        Statuses(prey).ApplyTimed(CombatStatusKind.Root,8f,ally);
        Begin();AdvanceTo(SabertoothDevourRules.PullStart+.1f);
        Statuses(prey).RemoveAllHarmful();
        var replacement=replace?Statuses(prey).ApplyTimed(CombatStatusKind.Root,2f,ally):null;
        Advance(.01f);Assert.That(Get(prey,"sabertoothDevouredBy"),Is.Null);
        Call("EndSabertoothDevour",hunter,true);
        Assert.That(Statuses(prey).Get(CombatStatusKind.Root),Is.SameAs(replacement));
        if(replace)Assert.That(replacement.remaining,Is.EqualTo(2f));
        AssertReleased();
    }

    [TestCase(0)] [TestCase(1)]
    public void ALaterAlliedDevourCannotStealOrReleaseTheFirstCastersCapturedEnemies(int team)
    {
        SetTeam(team);object second=CreateUnit(team,new Vector3(-4f,0f,-3f));
        Set(second,"augmentTranscended",true);Set(second,"target",prey);Set(second,"skillTimer",0f);
        Begin();AdvanceTo(SabertoothDevourRules.PullStart+.1f);
        var root=Statuses(prey).Get(CombatStatusKind.Root);Vector3 position=Position(prey);
        Assert.That(Call("BeginSabertoothDevour",second,Skill),Is.True);
        Call("AdvanceSabertoothDevour",second,SabertoothDevourRules.PullStart+.1f);
        Assert.That(Get(prey,"sabertoothDevouredBy"),Is.SameAs(hunter));
        Assert.That(Statuses(prey).Get(CombatStatusKind.Root),Is.SameAs(root));
        Assert.That(Position(prey),Is.EqualTo(position));
        Call("AdvanceSabertoothDevour",second,SabertoothDevourRules.HitTime);
        Call("EndSabertoothDevour",second,true);
        foreach(object enemy in enemies)
        {
            Assert.That(Get(enemy,"sabertoothDevouredBy"),Is.SameAs(hunter));
            Assert.That(Statuses(enemy).Get(CombatStatusKind.Root).source,Is.SameAs(hunter));
            Assert.That(Hp(enemy),Is.EqualTo(4820f).Within(.02f));
        }
        AdvanceTo(SabertoothDevourRules.Duration+.1f);AssertReleased();
        foreach(object enemy in enemies)Assert.That(Hp(enemy),Is.EqualTo(4640f).Within(.02f));
    }

    [Test]
    public void CapturingAPreparingEnemyPreservesItsAcceptedRequestAndReleasesItOnceAfterRecovery()
    {
        var frontend=typeof(GameFrontend).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        object previousFrontend=frontend.GetValue(null);
        var sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0f,0f,1f,1f),Vector2.one*.5f,1f);
        var renderer=Model(prey).AddComponent<SpriteRenderer>();renderer.sprite=sprite;
        var driver=Model(prey).AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames={sprite,sprite,sprite,sprite};driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        Set(prey,"spriteAnimation",driver);Set(prey,"skillId","guardian_taunt");Set(prey,"skillTimer",0f);
        Model(prey).transform.position=Vector3.right*.8f;Set(prey,"lastGroundPosition",Position(prey));
        var cameraHost=new GameObject("Captured preparation camera");var camera=cameraHost.AddComponent<Camera>();
        var director=cameraHost.AddComponent<CombatCameraDirector>();
        typeof(CombatCameraDirector).GetField("battleCamera",Private).SetValue(director,camera);
        BattleSet("mlEpisodeActive",false);BattleSet("mainCamera",camera);BattleSet("combatCameraDirector",director);
        director.BeginBattle();
        var field=battleType.GetField("skillExecutor",Private);object executor=Activator.CreateInstance(field.FieldType);
        field.SetValue(battle,executor);Type context=field.FieldType.GetGenericArguments()[0];
        Type handler=typeof(Func<,,>).MakeGenericType(context,typeof(SkillMasterRow),typeof(bool));
        Delegate spy=Delegate.CreateDelegate(handler,this,GetType().GetMethod("CommitPreparedVictim",Private).MakeGenericMethod(context));
        field.FieldType.GetMethod("Register").Invoke(executor,new object[]{
            GameMasterData.Skill("guardian_taunt").behavior,MonsterSkillTrigger.Active,spy});
        try
        {
            frontend.SetValue(null,null);
            Assert.That(Call("BeginCommonSkillFocus",prey,false),Is.True);
            object presentation=Call("SkillPresentationFor",prey);
            var pendingField=presentation.GetType().GetField("pending");object request=pendingField.GetValue(presentation);
            var startedField=presentation.GetType().GetField("startedAt");object started=startedField.GetValue(presentation);
            Begin();AdvanceTo(SabertoothDevourRules.PullStart+.1f);Call("UpdateCommonSkillFocus");
            Assert.That(Get(prey,"sabertoothDevouredBy"),Is.SameAs(hunter));
            Assert.That(Call("SkillPresentationFor",prey),Is.SameAs(presentation));
            Assert.That(pendingField.GetValue(presentation),Is.SameAs(request));Assert.That(startedField.GetValue(presentation),Is.EqualTo(started));
            double matured=Time.unscaledTimeAsDouble-.351d;startedField.SetValue(presentation,matured);
            Call("UpdateCommonSkillFocus");Assert.That(resumedCommits,Is.Zero);
            Call("EndSabertoothDevour",hunter,true);Call("UpdateCommonSkillFocus");Call("UpdateCommonSkillFocus");
            Assert.That(resumedCommits,Is.EqualTo(1));Assert.That(Get(prey,"skillUses"),Is.EqualTo(1));
            Assert.That(pendingField.GetValue(presentation),Is.Null);Assert.That(startedField.GetValue(presentation),Is.EqualTo(matured));
        }
        finally
        {
            Call("CancelCommonSkillFocus");director.EndBattle();
            frontend.SetValue(null,previousFrontend);Object.DestroyImmediate(sprite);
        }
    }

    [TestCase("cancel")] [TestCase("death")] [TestCase("fear")] [TestCase("knockdown")]
    [TestCase("revision")] [TestCase("generation")] [TestCase("disabled")]
    public void InterruptedOrResetCastsReleaseEveryEnemyAndCannotHitLater(string cause)
    {
        Begin();AdvanceTo(SabertoothDevourRules.PullEnd+.1f);
        Assert.That(Get(prey,"sabertoothDevouredBy"),Is.SameAs(hunter));
        if(cause=="cancel")Call("EndSabertoothDevour",hunter,true);
        else if(cause=="death")Set(hunter,"alive",false);
        else if(cause=="fear")Statuses(hunter).ApplyTimed(CombatStatusKind.Fear,1f);
        else if(cause=="knockdown")Statuses(hunter).ApplyTimed(CombatStatusKind.Knockdown,1f);
        else if(cause=="revision")Set(hunter,"positionRevision",(int)Get(hunter,"positionRevision")+1);
        else if(cause=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else Call("OnDisable");
        Advance(10f);AssertReleased();
        foreach(object enemy in enemies)Assert.That(Hp(enemy),Is.EqualTo(5000f));
    }

    [Test]
    public void APreyDeathDuringThePullDoesNotLeaveItsOwnerStuckOrPreventTheRemainingHits()
    {
        Begin();AdvanceTo(SabertoothDevourRules.PullStart+.1f);Set(prey,"alive",false);Set(prey,"hp",0f);
        AdvanceTo(SabertoothDevourRules.Duration+.1f);AssertReleased();
        Assert.That(Hp(enemies[1]),Is.EqualTo(4820f).Within(.02f));
        Assert.That(Hp(enemies[2]),Is.EqualTo(4820f).Within(.02f));
        Assert.That(Get(hunter,"skillHits"),Is.EqualTo(2));
    }

    [Test]
    public void EightEnemiesGatherWithoutOverlappingBodiesOrLeavingTheArena()
    {
        foreach(object enemy in enemies)Set(enemy,"alive",false);enemies.Clear();
        for(int column=0;column<2;column++)for(int row=0;row<4;row++)
            enemies.Add(CreateUnit(1,new Vector3(5f+column*2f,0f,(row-1.5f)*2f)));
        Set(hunter,"target",enemies[0]);Begin();
        for(int frame=0;frame<100;frame++)
        {
            Advance(.02f);
            for(int i=0;i<enemies.Count;i++)
            {
                Vector3 point=Position(enemies[i]);
                Assert.That(point.x,Is.InRange(-11.701f,11.701f));Assert.That(point.z,Is.InRange(-9.701f,9.701f));
                Assert.That(Vector3.Distance(point,Position(hunter)),Is.GreaterThanOrEqualTo(.599f));
                for(int j=i+1;j<enemies.Count;j++)
                    Assert.That(Vector3.Distance(point,Position(enemies[j])),Is.GreaterThanOrEqualTo(.599f));
            }
        }
        Assert.That(enemies.Count,Is.EqualTo(8));
        Assert.That(enemies.All(enemy=>Get(enemy,"sabertoothDevouredBy")==hunter),Is.True);
        var plans=(IDictionary)StateField("plans");
        foreach(object enemy in enemies)
            Assert.That(Vector3.Distance(Position(enemy),(Vector3)plans[enemy]),Is.LessThan(.2f),
                "An unobstructed eight-enemy pull must reach its planned gathering positions.");
    }

    [Test]
    public void AnObstacleCanStopThePullWithoutBeingCrossedOrCancellingTheDistantBite()
    {
        var tileType=battleType.GetNestedType("Tile",BindingFlags.NonPublic);
        object wall=Activator.CreateInstance(tileType,true);var cell=new Vector2Int(1,0);
        tileType.GetField("coordinate").SetValue(wall,cell);
        tileType.GetField("world").SetValue(wall,Vector3.right*4f);tileType.GetField("walkable").SetValue(wall,false);
        ((IDictionary)battleType.GetField("tiles",Private).GetValue(battle)).Add(cell,wall);
        Model(prey).transform.position=Vector3.right*7f;Begin();
        for(int frame=0;frame<100;frame++)
        {
            Vector3 previous=Position(prey);Advance(.02f);
            Assert.That(Call("HasClearPathSegment",previous,Position(prey),.3f),Is.True);
        }
        AdvanceTo(SabertoothDevourRules.Duration+.1f);
        Assert.That(Hp(prey),Is.EqualTo(4820f).Within(.02f));AssertReleased();
    }

    [Test]
    public void BattleEndReleasesThePreyAndTheNextGenerationCannotReceiveAnOldHit()
    {
        Begin();AdvanceTo(SabertoothDevourRules.PullEnd+.1f);BattleSet("battleEnding",true);
        Advance(SabertoothDevourRules.Duration+.1f);
        BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);Advance(1f);
        AssertReleased();foreach(object enemy in enemies)Assert.That(Hp(enemy),Is.EqualTo(5000f));
    }

    [Test]
    public void TheMasterAndLocalizationDescribeDevourWithoutTheRemovedPredationRewards()
    {
        const string id="trait_transcend_sabertooth_absolute_predation";
        var row=GameMasterData.Database.traits.Single(trait=>trait.id==id);
        Assert.That(row.displayName,Is.EqualTo("대포식"));
        Assert.That(row.effectSummary,Does.Contain("180%"));
        Assert.That(row.effectSummary,Does.Not.Contain("25%").And.Not.Contain("쿨타임 초기화"));
        Assert.That(Skill.baseCooldown,Is.EqualTo(7.5f));
        var localization=JsonUtility.FromJson<LocalizationDatabaseFile>(Resources.Load<TextAsset>(GameLocalization.ResourcePath).text);
        Assert.That(localization.entries.Single(entry=>entry.key==id+".effect").ko,Is.EqualTo(row.effectSummary));
    }

    private bool CommitPreparedVictim<TContext>(TContext context,SkillMasterRow skill)
    {
        object caster=context.GetType().GetField("source").GetValue(context);
        Assert.That(caster,Is.SameAs(prey));resumedCommits++;
        Set(prey,"skillUses",resumedCommits);Set(prey,"skillTimer",7.5f);return true;
    }
    private void Begin()=>Assert.That(Call("BeginSabertoothDevour",hunter,Skill),Is.True);
    private void Advance(float dt)=>Call("AdvanceSabertoothDevour",hunter,dt);
    private void AdvanceTo(float time)
    {
        float remaining=time-(State==null?0f:(float)StateField("time"));
        while(remaining>.000001f&&State!=null){float dt=Mathf.Min(.025f,remaining);Advance(dt);remaining-=dt;}
    }
    private void AssertReleased()
    {
        Assert.That(State,Is.Null);
        foreach(object enemy in enemies)Assert.That(Get(enemy,"sabertoothDevouredBy"),Is.Null);
    }
    private object CreateUnit(int side,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var units=(IList)battleType.GetField("units",Private).GetValue(battle);
        var model=new GameObject("Devour unit");model.transform.position=position;
        Set(unit,"id",units.Count);Set(unit,"model",model);Set(unit,"ring",new GameObject("Devour shadow"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,side));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_geomchi"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",SabertoothCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);
        Set(unit,"attackInterval",1f);Set(unit,"attackRange",1);Set(unit,"collisionRadius",.3f);
        Set(unit,"skillCooldownDuration",7.5f);Set(unit,"skillTimer",100f);Set(unit,"lastGroundPosition",position);
        Statuses(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);
        units.Add(unit);return unit;
    }
    private void SetTeam(int team)
    {
        Set(hunter,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(ally,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        foreach(object enemy in enemies)Set(enemy,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
    }
    private SkillMasterRow Skill=>GameMasterData.Skill(SabertoothCombatRules.SkillId);
    private object State=>Get(hunter,"sabertoothDevour");
    private object StateField(string field)=>State.GetType().GetField(field,Private|BindingFlags.Public).GetValue(State);
    private List<string> Traits(object unit)=>(List<string>)Get(unit,"activeTraitIds");
    private CombatStatusState Statuses(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private Vector3 Position(object unit)=>Model(unit).transform.position;
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    private object Call(string name,params object[] args)=>battleType.GetMethod(name,Private).Invoke(battle,args);
    private void BattleSet(string name,object value)=>battleType.GetField(name,Private).SetValue(battle,value);
    private object BattleGet(string name)=>battleType.GetField(name,Private).GetValue(battle);
}
