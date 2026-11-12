using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class WolfStampedeCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private readonly Type type=typeof(MonsterAutoBattlePrototype);
    private MonsterAutoBattlePrototype battle;
    private Camera camera;
    private CombatCameraDirector director;
    private Type unitType;
    private object source,primary,secondary,previousFrontend;
    private HashSet<GameObject> existing;
    private float previousSpeed;
    private bool previousTestMode;

    [SetUp] public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Hidden).GetValue(null);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,null);
        Assert.That(CombatSkillClock.Active,Is.False);Time.timeScale=1f;CombatEffectTestConfiguration.Active=true;
        var host=new GameObject("Inactive stampede cinematic tests");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=type.GetNestedType("Unit",Hidden);
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",false);
        BattleSet("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        camera=new GameObject("Stampede integration battle camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-12f),Quaternion.Euler(35f,0f,0f));
        camera.fieldOfView=40f;camera.aspect=16f/9f;
        director=camera.gameObject.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        BattleSet("mainCamera",camera);BattleSet("combatCameraDirector",director);
        source=Unit("nukgu",0,Vector3.zero);Set(source,"augmentTranscended",true);
        primary=Unit("geomchi",1,Vector3.right);secondary=Unit("tino",1,new Vector3(1.2f,0f,.2f));Set(source,"target",primary);
    }

    [TearDown] public void Cleanup()
    {
        if(battle!=null){Call("CancelWolfStampedeCinematic");Call("ResetWolfStampedes",true);Call("CancelCommonSkillFocus");Call("ResetFamilyTranscendenceStates");}
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)
                Object.DestroyImmediate(value);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"A stampede cinematic must not leave an exclusive actor clock behind.");
    }

    [TestCase("nukgu",0)] [TestCase("nukgu",1)]
    [TestCase("seollang",0)] [TestCase("seollang",1)]
    [TestCase("honga",0)] [TestCase("honga",1)]
    [TestCase("wolyeong",0)] [TestCase("wolyeong",1)]
    public void EachSpeciesAndTeamShowsItsOwnFirstCinematicPerWaveOnly(string species,int team)
    {
        Units.Remove(source);Model(source).SetActive(false);source=Unit(species,team,Vector3.zero);
        Set(source,"augmentTranscended",true);Team(primary,1-team);Team(secondary,1-team);Set(source,"target",primary);
        Begin(source);Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(Field(Family(source),"wolfCinematicGeneration"),Is.Not.EqualTo(BattleGet("combatGeneration")));
        StartStage(source);var stage=(WolfStampedeCinematicStage)BattleGet("wolfStampedeCinematicStage");
        Assert.That(stage.Source.species,Is.EqualTo(species));Assert.That(stage.Active,Is.True);
        Assert.That(Field(Family(source),"wolfCinematicGeneration"),Is.EqualTo(BattleGet("combatGeneration")));
        Call("FinishWolfStampedeCinematic");Call("ResetWolfStampedes",true);
        Begin(source);Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);
        Assert.That(Field(Cast(source),"released"),Is.False,"A repeated cast still uses its ordinary preparation.");
        Call("ResetWolfStampedes",true);BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        Begin(source);Assert.That(Queue.Count,Is.EqualTo(1));StartStage(source);AssertNoDamage();
    }

    [Test]
    public void SimultaneousCastersKeepSeparateQueuedClocksAndNeverStartDamageUnderAnotherStage()
    {
        object secondWolf=Unit("seollang",0,Vector3.left*2f);Set(secondWolf,"augmentTranscended",true);
        Begin(source);Begin(secondWolf);object firstCast=Cast(source),secondCast=Cast(secondWolf);
        Call("QueueWolfStampedeCinematic",source,firstCast);Assert.That(Queue.Count,Is.EqualTo(2));
        var foreign=new CombatSkillClock(camera.transform);
        try
        {
            foreign.Hold();Assert.That(UpdateCinematicAfterPrelude("UpdateWolfStampedeCinematic"),Is.False);
            Call("UpdateWolfStampedes",5f);Assert.That(Queue.Count,Is.EqualTo(2));
            Assert.That(Cinematic,Is.Null);AssertWaiting(firstCast);AssertWaiting(secondCast);
            Assert.That(Field(Family(source),"wolfCinematicGeneration"),Is.Not.EqualTo(BattleGet("combatGeneration")));
        }
        finally { foreign.End(); }
        StartStage(source);Call("UpdateWolfStampedes",5f);
        AssertWaiting(firstCast);AssertWaiting(secondCast);Assert.That(Queue.Count,Is.EqualTo(1));
        foreach(object unit in new[]{source,secondWolf,primary})
            Assert.That(CombatSkillClock.ActorFrozen(Model(unit).transform),Is.True);
        Call("FinishWolfStampedeCinematic");Call("CancelWolfStampedeFor",source);Call("UpdateWolfStampedes",.01f);
        StartStage(secondWolf);Assert.That(Field(Family(secondWolf),"wolfCinematicGeneration"),Is.EqualTo(BattleGet("combatGeneration")));
        AssertWaiting(secondCast);AssertNoDamage();
    }

    [TestCase(false)] [TestCase(true)]
    public void CancellationRestoresTheExactCameraSpeedAndPriorSourceVisibilityWithoutCancellingTheCommittedCast(bool hidden)
    {
        Time.timeScale=2f;Renderer(source).forceRenderingOff=hidden;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        float fov=camera.fieldOfView;Vector2 lens=camera.lensShift;Matrix4x4 projection=camera.projectionMatrix;
        Begin(source);object cast=Cast(source);StartStage(source);Assert.That(Renderer(source).forceRenderingOff,Is.True);
        Call("CancelWolfStampedeCinematic");
        Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(director.enabled,Is.True);
        Assert.That(Renderer(source).forceRenderingOff,Is.EqualTo(hidden));Assert.That(Cast(source),Is.SameAs(cast));
        Assert.That(Field(cast,"cancelled"),Is.False);AssertWaiting(cast);
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.lensShift,Is.EqualTo(lens));
        for(int row=0;row<4;row++)for(int col=0;col<4;col++)
            Assert.That(camera.projectionMatrix[row,col],Is.EqualTo(projection[row,col]).Within(.00001f));
        Call("UpdateWolfStampedes",WolfStampedeRules.Preparation+WolfStampedeRules.Duration+.001f);
        Assert.That(Hp(primary),Is.EqualTo(4640f).Within(.01f));Assert.That(Hp(secondary),Is.EqualTo(4640f).Within(.01f));
    }

    [TestCase("pause")] [TestCase("freeze")]
    public void PausingOrFreezingTheUnreleasedCasterReturnsVisibilityAndReleasesTheWorldClock(string reason)
    {
        Begin(source);object cast=Cast(source);StartStage(source);
        if(reason=="pause")BattleSet("paused",true);else Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);
        Assert.That(UpdateCinematicAfterPrelude("UpdateWolfStampedeCinematic"),Is.False);
        Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(director.enabled,Is.True);
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(Field(cast,"released"),Is.False);AssertNoDamage();
    }

    [TestCase("ml")] [TestCase("no camera")]
    public void HeadlessOrCameraLessPlaySkipsTheStageAndKeepsTheSameAbility(string reason)
    {
        if(reason=="ml")BattleSet("mlEpisodeActive",true);else BattleSet("mainCamera",null);
        Begin(source);Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
        Call("UpdateWolfStampedes",WolfStampedeRules.Preparation+WolfStampedeRules.Duration+.001f);
        Assert.That(Hp(primary),Is.EqualTo(4640f).Within(.01f));Assert.That(Hp(secondary),Is.EqualTo(4640f).Within(.01f));
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(Get(source,"skillHits"),Is.EqualTo(6));
    }

    [Test]
    public void TheFinalTransitionReleasesOnePackAndItsClockContinuesAfterTheStage()
    {
        Vector3 waitingPosition=Model(source).transform.position;
        Begin(source);object cast=Cast(source);StartStage(source);
        Cinematic.GetType().GetField("elapsed",Hidden).SetValue(Cinematic,WolfStampedeCinematicStage.Duration-.25f);
        UpdateCinematicAfterPrelude("UpdateWolfStampedeCinematic");
        Assert.That(Field(cast,"released"),Is.True);float credited=(float)Field(cast,"time");
        Assert.That(credited,Is.GreaterThanOrEqualTo(0f));
        Assert.That(Model(source).transform.position,Is.EqualTo(waitingPosition),"The wipe must not hide the caster's body rush.");
        Assert.That(Field(cast,"bodyHuntStarted"),Is.False);
        Assert.That((float)Field(cast,"poseTime"),Is.EqualTo(WolfStampedeRules.Preparation).Within(.0001f));
        // Editor frames can cross the full return interval; only assert the release, not a partial-frame stage lifetime.
        Call("FinishWolfStampedeCinematic");Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Call("UpdateWolfStampedes",.1f);Assert.That((float)Field(cast,"time"),Is.GreaterThanOrEqualTo(credited));
        Assert.That(Field(cast,"bodyHuntStarted"),Is.True);
        Assert.That(Get(source,"wolfHuntPending"),Is.True,"The real hunt begins only after the stage returns.");
        Assert.That(Get(source,"wolfHuntTarget"),Is.SameAs(primary));
        Assert.That(Model(source).transform.position,Is.EqualTo(waitingPosition),"Its original animation callback releases the dash, not a second pose interpolation.");
        Assert.That((bool)Field(Get(source,"spriteAnimation"),"wolfHuntAttack"),Is.True);
        Call("UpdateWolfStampedes",WolfStampedeRules.Duration+.001f);
        Assert.That(Hp(primary),Is.EqualTo(4640f).Within(.01f));Assert.That(Hp(secondary),Is.EqualTo(4640f).Within(.01f));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillHits"),Is.EqualTo(6));
    }

    [Test]
    public void BattleResultStopsDamageWhileReleasedWolvesAndContactsFinishTheirVisualLifetime()
    {
        var effect=CreateReleasedEffect();var contacts=(Mesh)Field(effect,"contactMesh");
        var contactMaterial=(Material)Field(effect,"contactMaterial");float hp=Hp(primary),otherHp=Hp(secondary);
        BattleSet("battleEnding",true);Call("ResetWolfStampedes",false);
        Assert.That(effect!=null,Is.True);Assert.That(Cast(source),Is.Null);
        Call("PreserveBattleEffects");
        var playback=effect.GetComponent<CombatEffectEndPlayback>();
        Assert.That(playback,Is.Not.Null);Assert.That(CombatEffectEndPlayback.IsPreserving(effect),Is.True);
        Call("UpdateWolfStampedes",WolfStampedeRules.Duration+1f);
        typeof(CombatEffectEndPlayback).GetMethod("Advance",Hidden).Invoke(playback,new object[]{WolfStampedeRules.Duration+1f});
        Assert.That(effect==null,Is.True);Assert.That(contacts==null,Is.True);Assert.That(contactMaterial==null,Is.True);
        Assert.That(Hp(primary),Is.EqualTo(hp));Assert.That(Hp(secondary),Is.EqualTo(otherHp));
    }

    [Test]
    public void NextWaveCleanupImmediatelyRemovesDetachedWolvesAndTheirDrawnContacts()
    {
        var effect=CreateReleasedEffect();var contacts=(Mesh)Field(effect,"contactMesh");
        var contactMaterial=(Material)Field(effect,"contactMaterial");float hp=Hp(primary),otherHp=Hp(secondary);
        Call("ResetWolfStampedes",false);Assert.That(effect!=null,Is.True);
        Call("ClearBattleEffects");
        Assert.That(effect==null,Is.True);Assert.That(contacts==null,Is.True);Assert.That(contactMaterial==null,Is.True);
        Assert.That(Cast(source),Is.Null);Call("UpdateWolfStampedes",WolfStampedeRules.Duration+1f);
        Assert.That(Hp(primary),Is.EqualTo(hp));Assert.That(Hp(secondary),Is.EqualTo(otherHp));
    }

    private PixelWolfStampede CreateReleasedEffect()
    {
        Assert.That(Resources.Load<Texture2D>(PixelWolfStampede.AtlasResource),Is.Not.Null);
        Assert.That(Resources.Load<Texture2D>(PixelWolfStampede.ContactResource),Is.Not.Null);
        Begin(source);object cast=Cast(source);Call("CancelWolfStampedeCinematicFor",source);Call("ReleaseWolfStampede",source);
        var effect=(PixelWolfStampede)Field(cast,"effect");Assert.That(effect,Is.Not.Null);
        Assert.That(((Mesh)Field(effect,"contactMesh")).vertexCount,Is.GreaterThan(0));return effect;
    }

    private void Begin(object unit)=>Assert.That(Call("BeginWolfStampede",unit,GameMasterData.Skill(WolfCombatRules.SkillId)),Is.True);
    private void StartStage(object unit)
    {
        Assert.That(Queue.Count,Is.GreaterThan(0));object request=Queue[0];Queue.RemoveAt(0);
        Assert.That(Field(request,"unit"),Is.SameAs(unit));
        Assert.That(Call("BeginWolfStampedeCinematic",request),Is.True);Assert.That(Cinematic,Is.Not.Null);
        Assert.That(CombatSkillClock.Active,Is.True);Assert.That(Time.timeScale,Is.Zero);
    }
    private void AssertWaiting(object cast)
    {Assert.That(Field(cast,"released"),Is.False);Assert.That(Field(cast,"time"),Is.EqualTo(0f));AssertNoDamage();}
    private void AssertNoDamage()
    {Assert.That(Hp(primary),Is.EqualTo(5000f));Assert.That(Hp(secondary),Is.EqualTo(5000f));}
    private object Unit(string species,int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        Assert.That(definition,Is.Not.Null);var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null);
        var authored=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var model=new GameObject("Stampede cinematic actor "+species);model.transform.position=position;
        var visual=new GameObject("Authored sprite");visual.transform.SetParent(model.transform,false);
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        driver.Configure(Frames(authored,"idleFrames"),Frames(authored,"runFrames"),Frames(authored,"attackFrames"),
            Frames(authored,"skillAttackFrames"),Frames(authored,"deathFrames"),Frames(authored,"captureFrames"),Frames(authored,"struggleFrames"));
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        visual.transform.rotation=camera.transform.rotation;var ring=new GameObject("Stampede cinematic shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",Units.Count);Set(unit,"model",model);Set(unit,"ring",ring);Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"definition",definition);Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",definition.skillId);
        Team(unit,team);Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);
        Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);Set(unit,"collisionRadius",.3f);
        Set(unit,"skillCooldownDuration",5f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"lastGroundPosition",position);
        Status(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);Units.Add(unit);return unit;
    }
    private object Cinematic=>BattleGet("wolfStampedeCinematic");
    private IList Queue=>(IList)BattleGet("wolfStampedeCinematicQueue");
    private IList Units=>(IList)BattleGet("units");
    private object Family(object unit)=>Call("FamilyTranscendenceStateFor",unit,true);
    private object Cast(object unit)=>Get(unit,"wolfStampede");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private SpriteRenderer Renderer(object unit)=>((SpriteMonsterAnimationDriver)Get(unit,"spriteAnimation")).GetComponent<SpriteRenderer>();
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private static Sprite[] Frames(SpriteMonsterAnimationDriver authored,string name)=>(Sprite[])Field(authored,name);
    private void Team(object unit,int team)=>Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
    private object Get(object unit,string name)=>unitType.GetField(name,Hidden).GetValue(unit);
    private void Set(object unit,string name,object value)=>unitType.GetField(name,Hidden).SetValue(unit,value);
    private object BattleGet(string name)=>type.GetField(name,Hidden).GetValue(battle);
    private void BattleSet(string name,object value)=>type.GetField(name,Hidden).SetValue(battle,value);
    private static object Field(object value,string name)=>value.GetType().GetField(name,Hidden).GetValue(value);
    private object UpdateCinematicAfterPrelude(string method)
    {
        object result=Call(method);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        object prelude=typeof(MonsterAutoBattlePrototype).GetField("rank4SkillPrelude",flags).GetValue(battle);
        if(prelude==null)return result;
        prelude.GetType().GetField("elapsed",flags).SetValue(prelude,.451f);
        Call("UpdateRank4SkillPrelude");
        return Call(method);
    }

    private object Call(string method,params object[] args)=>type.GetMethod(method,Hidden).Invoke(battle,args);
}
