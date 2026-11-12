using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CrocodileAbyssDiveCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private MonsterAutoBattlePrototype battle;
    private Camera camera;
    private CombatCameraDirector director;
    private Type unitType;
    private object source,primary,previousFrontend;
    private readonly List<object> enemies=new List<object>();
    private readonly List<Vector3> startingPositions=new List<Vector3>();
    private HashSet<GameObject> existing;
    private float previousSpeed;
    private bool previousTestMode;

    [SetUp] public void Setup()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousSpeed=Time.timeScale;previousTestMode=CombatEffectTestConfiguration.Active;
        previousFrontend=typeof(GameFrontend).GetField("instance",Hidden).GetValue(null);
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,null);
        Assert.That(CombatSkillClock.Active,Is.False,"A previous stage cannot retain the exclusive actor clock.");
        Time.timeScale=1f;CombatEffectTestConfiguration.Active=true;
        var host=new GameObject("Inactive crocodile cinematic integration tests");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",Hidden);
        BattleSet("battleStarted",true);BattleSet("mlEpisodeActive",false);
        BattleSet("expeditionRun",new ExpeditionRunSave{regionId=HunterRegionCatalog.FirstRegionId,wave=1});
        BattleSet("playerProfile",new PlayerProfileSave());BattleSet("combatBounds",new Bounds(Vector3.zero,new Vector3(30f,10f,24f)));
        camera=new GameObject("Crocodile integration battle camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0f,8f,-12f),Quaternion.Euler(35f,0f,0f));camera.fieldOfView=40f;camera.aspect=16f/9f;
        director=camera.gameObject.AddComponent<CombatCameraDirector>();director.Initialize(camera,false);director.BeginBattle();
        BattleSet("mainCamera",camera);BattleSet("combatCameraDirector",director);
        source=Unit(0,Vector3.zero,"ager");Set(source,"augmentTranscended",true);
        for(int i=0;i<8;i++)
        {
            Vector3 position=i==0?Vector3.right:new Vector3(3f+i%3*2f,0f,(i/3-1)*2f);
            enemies.Add(Unit(1,position,i%2==0?"geomchi":"tino"));startingPositions.Add(position);
        }
        primary=enemies[0];Set(primary,"defense",999f);Set(source,"target",primary);
    }
    [TearDown] public void Cleanup()
    {
        if(battle!=null)
        {Call("CancelCrocodileAbyssDiveCinematic");Call("CancelCommonSkillFocus");Call("ResetFamilyTranscendenceStates");}
        if(director!=null)director.EndBattle();
        foreach(var value in Resources.FindObjectsOfTypeAll<GameObject>())
            if(value!=null&&!existing.Contains(value)&&value.scene.IsValid()&&value.transform.parent==null)Object.DestroyImmediate(value);
        enemies.Clear();startingPositions.Clear();
        typeof(GameFrontend).GetField("instance",Hidden).SetValue(null,previousFrontend);
        CombatEffectTestConfiguration.Active=previousTestMode;Time.timeScale=previousSpeed;
        Assert.That(CombatSkillClock.Active,Is.False,"Every exit must release the global actor clock.");
    }

    [TestCase(0)][TestCase(1)]
    public void ARepeatCastFullySubmergesBothBodiesThenBreachesBeforeItsSingleStrike(int team)
    {
        SetTeam(team);Set(source,"crocodileAbyssCinematicGeneration",BattleGet("combatGeneration"));
        Begin();AdvanceTo(.55f);ApplyBillboards();
        Assert.That(Queue,Is.Empty);
        foreach(object actor in new[]{source,primary})
        {
            AssertGroundClip(actor,true);
            Assert.That(VisibleTop(actor),Is.LessThan(.025f),"During the submerged hold every painted pixel must be below the surface.");
        }
        AssertNoDamage();AdvanceTo(.86f);ApplyBillboards();
        Assert.That(VisibleTop(source),Is.GreaterThan(.025f));Assert.That(Hp(primary),Is.EqualTo(5000f));
        AdvanceTo(.96f);AssertGroundClip(source,false);AssertGroundClip(primary,false);
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        AdvanceTo(1.21f);AssertGroundClip(source,false);AssertGroundClip(primary,false);
    }

    [TestCase("ager")] [TestCase("kimbul")] [TestCase("krogal")] [TestCase("tarvil")]
    public void RealGrownPrefabsAndTheLocalHighlightMirrorDrawZeroPixelsDuringTheEntireSubmergedHold(string species)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Submerge verification requires GPU pixel readback.");
        ReplaceWithRealPrefab(source,species);ReplaceWithRealPrefab(primary,"geomchi");
        Set(source,"crocodileAbyssCinematicGeneration",BattleGet("combatGeneration"));
        camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        camera.allowHDR=false;camera.allowMSAA=false;
        var output=new RenderTexture(192,108,16,RenderTextureFormat.ARGB32);output.Create();
        var pixels=new Texture2D(192,108,TextureFormat.RGBA32,false);
        RenderTexture previous=RenderTexture.active;
        try
        {
            camera.targetTexture=output;
            Begin();((PixelCrocodileAbyssDive)Field(Cast,"effect")).gameObject.SetActive(false);
            Call("BeginLocalSkillPresentation",source);
            Driver(source).SetSkillFocus(1f);Model(primary).transform.localScale*=2.3f;
            var presentation=Call("SkillPresentationFor",source);
            var mirror=(SkillFocusSpriteMirror)Field(presentation,"mirror");mirror.gameObject.layer=31;
            Assert.That(RenderLiveBodies(output,pixels,mirror),Is.GreaterThan(0),"The baseline must contain actual prefab pixels.");
            foreach(float time in new[]{.4f,.55f,.72f})
            {
                AdvanceTo(time);
                Assert.That(RenderLiveBodies(output,pixels,mirror),Is.Zero,"A grown prefab or its foreground copy remains visible at "+time);
                foreach(object actor in new[]{source,primary})
                {
                    var billboard=(SpriteMonsterBillboard)Get(actor,"spriteBillboard");Assert.That(billboard.IsFullySubmerged,Is.True);
                    Assert.That(Model(actor).activeInHierarchy,Is.True,"Only rendering is submerged; actor logic stays active.");
                    foreach(Renderer floor in ((GameObject)Get(actor,"ring")).GetComponentsInChildren<Renderer>(true))
                        Assert.That(floor.forceRenderingOff,Is.True);
                    object[] hud={actor,new Rect()};Assert.That(Call("TryGetUnitWorldHudRect",hud),Is.False);
                }
            }
            AdvanceTo(.86f);Assert.That(RenderLiveBodies(output,pixels,mirror),Is.GreaterThan(0));
            foreach(object actor in new[]{source,primary})
            {
                Assert.That(((SpriteMonsterBillboard)Get(actor,"spriteBillboard")).IsFullySubmerged,Is.False);
                foreach(Renderer floor in ((GameObject)Get(actor,"ring")).GetComponentsInChildren<Renderer>(true))
                    Assert.That(floor.forceRenderingOff,Is.False);
            }
            Call("EndCrocodileAbyssDive",source,true);
            Assert.That(RenderLiveBodies(output,pixels,mirror),Is.GreaterThan(0));
        }
        finally
        {camera.targetTexture=null;RenderTexture.active=previous;output.Release();Object.DestroyImmediate(output);Object.DestroyImmediate(pixels);}
    }

    private void ReplaceWithRealPrefab(object actor,string species)
    {
        Model(actor).SetActive(false);
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);var model=Object.Instantiate(prefab);
        model.transform.position=actor==source?Vector3.zero:Vector3.right;
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);var renderer=driver.GetComponent<SpriteRenderer>();
        Bounds painted=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{renderer.sprite});
        float height=painted.size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        model.transform.localScale/=Mathf.Max(.01f,height);
        foreach(Renderer other in model.GetComponentsInChildren<Renderer>(true)){other.gameObject.layer=31;if(other!=renderer)other.enabled=false;}
        var billboard=renderer.GetComponent<SpriteMonsterBillboard>()??renderer.gameObject.AddComponent<SpriteMonsterBillboard>();
        typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        renderer.transform.rotation=camera.transform.rotation;
        var floor=new GameObject("Real prefab floor marker"){layer=31};floor.transform.SetParent(model.transform,false);
        floor.AddComponent<SpriteRenderer>().sprite=PixelStarProjectile.BeanSprite;
        Set(actor,"definition",definition);Set(actor,"model",model);Set(actor,"ring",floor);
        Set(actor,"spriteAnimation",driver);Set(actor,"spriteBillboard",billboard);
    }

    private int RenderLiveBodies(RenderTexture output,Texture2D pixels,SkillFocusSpriteMirror mirror)
    {
        foreach(object actor in new[]{source,primary})
        {
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Hidden).Invoke(Driver(actor),null);
            typeof(SpriteMonsterBillboard).GetMethod("LateUpdate",Hidden).Invoke(Get(actor,"spriteBillboard"),null);
        }
        typeof(SkillFocusSpriteMirror).GetMethod("LateUpdate",Hidden).Invoke(mirror,null);
        camera.Render();RenderTexture.active=output;pixels.ReadPixels(new Rect(0,0,output.width,output.height),0,0);pixels.Apply();
        int visible=0;foreach(Color32 pixel in pixels.GetPixels32())if(pixel.a>12)visible++;
        return visible;
    }

    [TestCase("cancel")][TestCase("target moved")][TestCase("freeze")]
    public void SubmergedVisualsAreRestoredOnInterruptionAndPreservedWhileFrozen(string exit)
    {
        Set(source,"crocodileAbyssCinematicGeneration",BattleGet("combatGeneration"));Begin();AdvanceTo(.55f);
        AssertGroundClip(source,true);AssertGroundClip(primary,true);
        if(exit=="target moved")
        {
            Set(primary,"positionRevision",(int)Get(primary,"positionRevision")+1);Advance(.01f);
            AssertGroundClip(primary,false);AssertGroundClip(source,true);
        }
        else if(exit=="freeze")
        {
            Status(source).ApplyTimed(CombatStatusKind.Freeze,2f,primary);Advance(.8f);
            Assert.That(CastTime,Is.EqualTo(.55f).Within(.001f));AssertGroundClip(source,true);AssertGroundClip(primary,true);
        }
        Call("EndCrocodileAbyssDive",source,true);
        AssertGroundClip(source,false);AssertGroundClip(primary,false);AssertNoDamage();
    }

    [TestCase("cleanse")] [TestCase("expiry")] [TestCase("target moved")] [TestCase("immunity")]
    public void AReleasedOrImmuneVictimImmediatelyRegainsItsBodyAndFloorWithoutCancellingTheCommittedCrocodile(string reason)
    {
        Set(source,"crocodileAbyssCinematicGeneration",BattleGet("combatGeneration"));Begin();AdvanceTo(.55f);
        Assert.That(((SpriteMonsterBillboard)Get(primary,"spriteBillboard")).IsFullySubmerged,Is.True);
        if(reason=="cleanse")Status(primary).RemoveAllHarmful();
        else if(reason=="expiry")Status(primary).Tick(3f);
        else if(reason=="target moved")Set(primary,"positionRevision",(int)Get(primary,"positionRevision")+1);
        else Status(primary).ControlImmune=true;
        Advance(.01f);
        Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.Null);
        Assert.That(((SpriteMonsterBillboard)Get(primary,"spriteBillboard")).IsFullySubmerged,Is.False);
        AssertGroundClip(primary,false);
        Assert.That(((SpriteMonsterBillboard)Get(source,"spriteBillboard")).IsFullySubmerged,Is.True);
        Assert.That(Cast,Is.Not.Null);AssertNoDamage();
    }

    [TestCase("cancel")] [TestCase("death")] [TestCase("target death")] [TestCase("CC")]
    [TestCase("reset")] [TestCase("source moved")] [TestCase("generation")]
    public void EverySubmergedExitRestoresRenderStateWithoutDeactivatingEitherActorOrDealingItsUnseenStrike(string reason)
    {
        Set(source,"crocodileAbyssCinematicGeneration",BattleGet("combatGeneration"));Begin();AdvanceTo(.55f);
        if(reason=="cancel")Call("EndCrocodileAbyssDive",source,true);
        else if(reason=="death")Set(source,"alive",false);
        else if(reason=="target death")Set(primary,"alive",false);
        else if(reason=="CC")Status(source).ApplyTimed(CombatStatusKind.Knockdown,2f,primary);
        else if(reason=="reset")Call("ResetFamilyTranscendenceStates");
        else if(reason=="source moved")Set(source,"positionRevision",(int)Get(source,"positionRevision")+1);
        else BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        Advance(.01f);
        foreach(object actor in new[]{source,primary})
        {
            Assert.That(((SpriteMonsterBillboard)Get(actor,"spriteBillboard")).IsFullySubmerged,Is.False);
            Assert.That(Model(actor).activeSelf,Is.True);AssertGroundClip(actor,false);
        }
        AssertNoDamage();
    }

    [Test]
    public void ASecondCrocodileThatNeverOwnedTheCaptureCannotRevealTheFirstCastersSubmergedVictim()
    {
        Set(source,"crocodileAbyssCinematicGeneration",BattleGet("combatGeneration"));Begin();
        object second=Unit(0,new Vector3(0,0,-.2f),"ager");
        Set(second,"augmentTranscended",true);Set(second,"target",primary);
        Set(second,"crocodileAbyssCinematicGeneration",BattleGet("combatGeneration"));
        Assert.That(Call("BeginDeathRoll",second,GameMasterData.Skill(CrocodileCombatRules.SkillId)),Is.True);
        AdvanceTo(.55f);Call("AdvanceCrocodileAbyssDive",second,.55f);
        Call("EndCrocodileAbyssDive",second,true);
        Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.SameAs(source));
        Assert.That(((SpriteMonsterBillboard)Get(primary,"spriteBillboard")).IsFullySubmerged,Is.True);
        Assert.That(Cast,Is.Not.Null);AssertNoDamage();
    }

    private void AssertGroundClip(object unit,bool active)
    {
        var block=new MaterialPropertyBlock();Renderer(unit).GetPropertyBlock(block);
        Assert.That(block.GetVector("_GroundClip").y,Is.EqualTo(active?1f:0f));
    }
    private void ApplyBillboards()
    {
        foreach(object unit in new[]{source,primary})
            typeof(SpriteMonsterBillboard).GetMethod("LateUpdate",Hidden).Invoke(Get(unit,"spriteBillboard"),null);
    }
    private float VisibleTop(object unit)
    {
        var renderer=Renderer(unit);
        Bounds bounds=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{renderer.sprite});
        float top=float.NegativeInfinity;
        for(int i=0;i<4;i++)
        {
            Vector3 point=new Vector3((i&1)==0?bounds.min.x:bounds.max.x,(i&2)==0?bounds.min.y:bounds.max.y,0f);
            if(renderer.flipX)point.x=-point.x;
            top=Mathf.Max(top,renderer.transform.TransformPoint(point).y);
        }
        return top;
    }

    [TestCase(.5f)][TestCase(5f)]
    public void ALongContactFrameQueuesTheSameCommittedCastWithoutDamageOrSpendingTwice(float dt)
    {
        Begin();object cast=Cast;Advance(dt);AssertContactOnly(cast);
        Assert.That(Queue.Count,Is.EqualTo(1));Assert.That(Field(Queue[0],"unit"),Is.SameAs(source));Assert.That(Field(Queue[0],"cast"),Is.SameAs(cast));
        Assert.That(Field(Queue[0],"generation"),Is.EqualTo(BattleGet("combatGeneration")));
        Assert.That(Call("IsCrocodileAbyssDiveCinematicPending",source),Is.True);
        Advance(5f);AssertContactOnly(cast);Assert.That(Queue.Count,Is.EqualTo(1));
        Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void FourSecondsOfPresentationFreezeAllActorsThenHandOffOneArmorIgnoringStrikeOnTheSameCast()
    {
        Begin();object cast=Cast;Advance(.5f);StartStage();
        Assert.That(Field(Cinematic,"cast"),Is.SameAs(cast));Assert.That(Field(Cinematic,"target"),Is.SameAs(primary));
        Assert.That(Renderer(source).forceRenderingOff,Is.True);Assert.That(Renderer(primary).forceRenderingOff,Is.True);
        Assert.That(CombatSkillClock.ActorFrozen(Model(source).transform),Is.True);
        foreach(object enemy in enemies)Assert.That(CombatSkillClock.ActorFrozen(Model(enemy).transform),Is.True);
        var stage=(CrocodileAbyssDiveCinematicStage)BattleGet("crocodileAbyssCinematicStage");
        foreach(float time in new[]{0f,1f,2f,3.84f,3.99f})
        {
            stage.Tick(time,.5f);Advance(5f);AssertContactOnly(cast);
            Assert.That(Time.timeScale,Is.Zero);Assert.That(CombatSkillClock.Active,Is.True);
        }
        Assert.That(stage.Target.species,Is.EqualTo(((MonsterDefinition)Get(primary,"definition")).monsterId));
        Assert.That(stage.Bystanders.Count,Is.EqualTo(7));
        Call("FinishCrocodileAbyssDiveCinematic",true);
        Assert.That(Cast,Is.SameAs(cast));Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(CastTime,Is.EqualTo(CrocodileAbyssDiveRules.HitTime-.001f).Within(.00002f));
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Get(source,"skillHits"),Is.Zero);AssertNoDamage();
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(Renderer(primary).forceRenderingOff,Is.False);
        Advance(.002f);
        Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(600f).Within(.01f));Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        foreach(object enemy in enemies.Skip(1))Assert.That(Hp(enemy),Is.EqualTo(5000f));
        Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.Null);
        Advance(5f);Assert.That(Cast,Is.Null);Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
    }

    [Test]
    public void AKillingCrushShowsItsUprightClosedJawInTheDamageFrameWithoutWaitingForAnotherDriverUpdate()
    {
        Set(primary,"hp",250f);Begin();Advance(.5f);StartStage();Call("FinishCrocodileAbyssDiveCinematic",true);
        Advance(.002f);
        Assert.That(Get(primary,"alive"),Is.False);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        Assert.That(Renderer(source).sprite,Is.SameAs(Driver(source).GetSkillAttackFrame(1)));
    }

    [TestCase("ager",0)][TestCase("ager",1)]
    [TestCase("kimbul",0)][TestCase("kimbul",1)]
    [TestCase("krogal",0)][TestCase("krogal",1)]
    [TestCase("tarvil",0)][TestCase("tarvil",1)]
    public void EveryCrocodileOnEitherTeamGetsOnlyItsFirstAcceptedStagePerWave(string species,int team)
    {
        if(species!="ager")
        {
            ((IList)BattleGet("units")).Remove(source);Model(source).SetActive(false);source=Unit(0,Vector3.zero,species);
            Set(source,"augmentTranscended",true);Set(source,"target",primary);
        }
        SetTeam(team);Begin();Advance(.5f);StartStage();Call("FinishCrocodileAbyssDiveCinematic",true);
        Assert.That(Get(source,"crocodileAbyssCinematicGeneration"),Is.EqualTo(BattleGet("combatGeneration")));
        Call("EndCrocodileAbyssDive",source,true);ResetForNextCast();
        Begin();Advance(.5f);Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);
        Assert.That(Call("IsCrocodileAbyssDiveCinematicPending",source),Is.False);Assert.That(Get(source,"skillUses"),Is.EqualTo(2));
        Call("EndCrocodileAbyssDive",source,true);ResetForNextCast();BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        Begin();Advance(.5f);Assert.That(Queue.Count,Is.EqualTo(1));StartStage();Assert.That(Get(source,"skillUses"),Is.EqualTo(3));
    }

    [TestCase(false)][TestCase(true)]
    public void CancellationRestoresExactOriginalVisibilityCameraAndSpeedButKeepsTheOneCommittedCast(bool wasHidden)
    {
        Time.timeScale=2f;Renderer(source).forceRenderingOff=wasHidden;Renderer(primary).forceRenderingOff=wasHidden;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        float fov=camera.fieldOfView;Vector2 lens=camera.lensShift;Matrix4x4 projection=camera.projectionMatrix;
        Begin();object cast=Cast;Advance(.5f);StartStage();Call("CancelCrocodileAbyssDiveCinematic");
        Assert.That(Cast,Is.SameAs(cast));AssertContactOnly(cast);Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(director.enabled,Is.True);
        Assert.That(Renderer(source).forceRenderingOff,Is.EqualTo(wasHidden));Assert.That(Renderer(primary).forceRenderingOff,Is.EqualTo(wasHidden));
        Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(camera.lensShift,Is.EqualTo(lens));
        for(int row=0;row<4;row++)for(int column=0;column<4;column++)Assert.That(camera.projectionMatrix[row,column],Is.EqualTo(projection[row,column]).Within(.00001f));
        Advance(.1f);Assert.That(CastTime,Is.GreaterThan(CrocodileAbyssDiveRules.ContactTime));AssertNoDamage();
    }

    [TestCase("interrupt")][TestCase("death")][TestCase("target_death")][TestCase("generation")][TestCase("disable")]
    public void InvalidatedCastsReleaseBothBodiesAndTheExclusiveClockWithoutADeferredHit(string reason)
    {
        Time.timeScale=2f;Begin();Advance(.5f);StartStage();
        if(reason=="interrupt")Call("InterruptPositionalAction",source);
        else if(reason=="death")Set(source,"alive",false);
        else if(reason=="target_death")Set(primary,"alive",false);
        else if(reason=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
        else Call("OnDisable");
        UpdateCinematicAfterPrelude("UpdateCrocodileAbyssDiveCinematic");Advance(0f);
        Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);Assert.That(Cast,Is.Null);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(director.enabled,Is.True);
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(Renderer(primary).forceRenderingOff,Is.False);
        Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.Null);Assert.That(Status(primary).Has(CombatStatusKind.Root),Is.False);
        AssertNoDamage();Assert.That(Get(source,"skillHits"),Is.Zero);
    }

    [TestCase(false)][TestCase(true)]
    public void PausingOrFreezingTheCasterCancelsOnlyPresentationAndKeepsTheCastAtContactUntilResumed(bool freeze)
    {
        Begin();object cast=Cast;Advance(.5f);StartStage();
        if(freeze)Status(source).ApplyTimed(CombatStatusKind.Freeze,2f);else BattleSet("paused",true);
        UpdateCinematicAfterPrelude("UpdateCrocodileAbyssDiveCinematic");
        Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        AssertContactOnly(cast);Advance(.5f);AssertContactOnly(cast);
        if(freeze)Status(source).Remove(CombatStatusKind.Freeze);else BattleSet("paused",false);
        Advance(.1f);Assert.That(CastTime,Is.GreaterThan(CrocodileAbyssDiveRules.ContactTime));AssertNoDamage();
    }

    [Test]
    public void CleansingThePrimaryDuringPresentationReleasesTheVisualHoldButTheCommittedSingleStrikeStillCompletes()
    {
        Begin();object cast=Cast;Advance(.5f);StartStage();Status(primary).Remove(CombatStatusKind.Root);
        UpdateCinematicAfterPrelude("UpdateCrocodileAbyssDiveCinematic");Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Cast,Is.SameAs(cast));Advance(.01f);Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.Null);
        AdvanceTo(CrocodileAbyssDiveRules.HitTime+.001f);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));
        foreach(object enemy in enemies.Skip(1))Assert.That(Hp(enemy),Is.EqualTo(5000f));
    }

    [Test]
    public void AControlImmuneTargetDoesNotCreateAnEmptyStageOrLoseItsSingleCommittedDamage()
    {
        Status(primary).ControlImmune=true;Begin();Advance(.5f);
        Assert.That(Queue,Is.Empty);Assert.That(Cinematic,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);
        AdvanceTo(CrocodileAbyssDiveRules.HitTime+.001f);Assert.That(Hp(primary),Is.EqualTo(4400f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.Null);
    }

    [Test]
    public void AnExistingCinematicClockCannotBeStolenByTheQueuedDive()
    {
        Begin();Advance(.5f);object cast=Cast;var foreign=new CombatSkillClock(camera.transform);
        try
        {
            foreign.Hold();Assert.That(UpdateCinematicAfterPrelude("UpdateCrocodileAbyssDiveCinematic"),Is.False);
            Assert.That(Cinematic,Is.Null);Assert.That(Queue.Count,Is.EqualTo(1));AssertContactOnly(cast);
        }
        finally{foreign.End();}
        StartStage();Assert.That(Field(Cinematic,"cast"),Is.SameAs(cast));
    }

    [TestCase(false)][TestCase(true)]
    public void SceneDestroyedEffectChildrenDoNotPreventTheBattleFromRestoringItsClockAndControls(bool stageActive)
    {
        Begin();Advance(.5f);if(stageActive)StartStage();
        var effect=(PixelCrocodileAbyssDive)Field(Cast,"effect");Object.DestroyImmediate(effect.gameObject);
        Assert.DoesNotThrow(()=>Call("OnDisable"));Assert.That(Cast,Is.Null);Assert.That(Cinematic,Is.Null);Assert.That(Queue,Is.Empty);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Get(primary,"crocodileAbyssHeldBy"),Is.Null);
    }

    private void AssertContactOnly(object cast)
    {
        Assert.That(Cast,Is.SameAs(cast));Assert.That(CastTime,Is.EqualTo(CrocodileAbyssDiveRules.ContactTime).Within(.00002f));
        Assert.That(Field(cast,"contacted"),Is.True);Assert.That(Field(cast,"hit"),Is.False);Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Get(source,"skillHits"),Is.Zero);AssertNoDamage();
    }
    private void AssertNoDamage(){foreach(object enemy in enemies)Assert.That(Hp(enemy),Is.EqualTo(5000f));}
    private void StartStage()
    {
        Assert.That(UpdateCinematicAfterPrelude("UpdateCrocodileAbyssDiveCinematic"),Is.True);Assert.That(Cinematic,Is.Not.Null);
        Assert.That(Time.timeScale,Is.Zero);Assert.That(CombatSkillClock.Active,Is.True);
    }
    private void ResetForNextCast()
    {
        Call("CancelCrocodileAbyssDiveCinematic");Set(source,"skillTimer",0f);Driver(source).InterruptCurrentAction(true);
        for(int i=0;i<enemies.Count;i++)
        {Status(enemies[i]).RemoveAllHarmful();Model(enemies[i]).transform.position=startingPositions[i];Driver(enemies[i]).InterruptCurrentAction(true);}
    }
    private void SetTeam(int team)
    {
        Set(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        foreach(object enemy in enemies)Set(enemy,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
    }
    private object Unit(int team,Vector3 position,string species)
    {
        object unit=Activator.CreateInstance(unitType,true);var list=(IList)BattleGet("units");
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);Assert.That(definition,Is.Not.Null);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null);
        var authored=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);Assert.That(authored,Is.Not.Null);
        var model=new GameObject("Abyss cinematic actor "+species);model.transform.position=position;
        var visual=new GameObject("Authored sprite");visual.transform.SetParent(model.transform,false);
        var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=authored.IdleFrames[0];
        var driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        driver.Configure(Frames(authored,"idleFrames"),Frames(authored,"runFrames"),Frames(authored,"attackFrames"),
            Frames(authored,"skillAttackFrames"),Frames(authored,"deathFrames"),Frames(authored,"captureFrames"),Frames(authored,"struggleFrames"));
        var billboard=visual.AddComponent<SpriteMonsterBillboard>();typeof(SpriteMonsterBillboard).GetField("cachedCamera",Hidden).SetValue(billboard,camera);
        visual.transform.rotation=camera.transform.rotation;var ring=new GameObject("Abyss shadow");ring.transform.SetParent(model.transform,false);
        Set(unit,"id",list.Count);Set(unit,"model",model);Set(unit,"ring",ring);Set(unit,"spriteAnimation",driver);Set(unit,"spriteBillboard",billboard);
        Set(unit,"definition",definition);Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",CrocodileCombatRules.SkillId);
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));Set(unit,"alive",true);
        Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",2);
        Set(unit,"collisionRadius",.3f);Set(unit,"skillCooldownDuration",8f);Set(unit,"skillTimer",100f);Set(unit,"cooldown",100f);Set(unit,"lastGroundPosition",position);
        Status(unit).Applied+=effect=>Call("HandleCombatStatusApplied",unit,effect);list.Add(unit);return unit;
    }
    private static Sprite[] Frames(SpriteMonsterAnimationDriver authored,string field)=>(Sprite[])Field(authored,field);
    private void Begin()=>Assert.That(Call("BeginDeathRoll",source,GameMasterData.Skill(CrocodileCombatRules.SkillId)),Is.True);
    private void Advance(float dt)=>Call("AdvanceCrocodileAbyssDive",source,dt);
    private void AdvanceTo(float time)
    {
        for(int i=0;Cast!=null&&CastTime<time-.000001f;i++)
        {Assert.That(i,Is.LessThan(200));Advance(Mathf.Min(.025f,time-CastTime));}
    }
    private object Cast=>Get(source,"crocodileAbyssDive");
    private float CastTime=>(float)Field(Cast,"time");
    private object Cinematic=>BattleGet("crocodileAbyssCinematic");
    private IList Queue=>(IList)BattleGet("crocodileAbyssCinematicQueue");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private SpriteMonsterAnimationDriver Driver(object unit)=>(SpriteMonsterAnimationDriver)Get(unit,"spriteAnimation");
    private SpriteRenderer Renderer(object unit)=>Driver(unit).GetComponent<SpriteRenderer>();
    private CombatStatusState Status(object unit)=>(CombatStatusState)Get(unit,"combatStatuses");
    private object Get(object unit,string name)=>unitType.GetField(name,Hidden).GetValue(unit);
    private void Set(object unit,string name,object value)=>unitType.GetField(name,Hidden).SetValue(unit,value);
    private object BattleGet(string name)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).GetValue(battle);
    private void BattleSet(string name,object value)=>typeof(MonsterAutoBattlePrototype).GetField(name,Hidden).SetValue(battle,value);
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

    private object Call(string method,params object[] args)=>typeof(MonsterAutoBattlePrototype).GetMethod(method,Hidden).Invoke(battle,args);
}

