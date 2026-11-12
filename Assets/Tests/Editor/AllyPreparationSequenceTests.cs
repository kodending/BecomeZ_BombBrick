using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class AllyPreparationSequenceTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private readonly List<GameObject> models=new List<GameObject>();
    private readonly Dictionary<object,int> commits=new Dictionary<object,int>();
    private readonly Dictionary<object,float> commitSpeeds=new Dictionary<object,float>();
    private readonly Dictionary<object,double> commitAges=new Dictionary<object,double>();
    private readonly Dictionary<object,bool> commitShades=new Dictionary<object,bool>();
    private readonly Dictionary<object,float> commitScales=new Dictionary<object,float>();
    private GameObject host,cameraHost;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private Texture2D texture;
    private Sprite sprite;
    private Type unitType;
    private object primary,secondary,enemy,previousCaster,previousFrontend;
    private float previousSpeed;

    [SetUp]
    public void SetUp()
    {
        models.Clear();commits.Clear();commitSpeeds.Clear();commitAges.Clear();commitShades.Clear();commitScales.Clear();
        previousSpeed=Time.timeScale;
        previousFrontend=typeof(GameFrontend).GetField("instance",Static).GetValue(null);
        typeof(GameFrontend).GetField("instance",Static).SetValue(null,null);
        host=new GameObject("Inactive allied preparation test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        Set(battle,"battleStarted",true);Set(battle,"expeditionRun",new ExpeditionRunSave());
        Set(battle,"combatBounds",new Bounds(Vector3.zero,Vector3.one*100f));
        cameraHost=new GameObject("Allied preparation camera");
        var camera=cameraHost.AddComponent<Camera>();camera.transform.position=Vector3.back*10f;
        director=cameraHost.AddComponent<CombatCameraDirector>();
        Set(director,"battleCamera",camera);Set(director,"homeFieldOfView",camera.fieldOfView);
        Set(battle,"mainCamera",camera);Set(battle,"combatCameraDirector",director);
        director.BeginBattle();Time.timeScale=1f;

        texture=new Texture2D(4,4,TextureFormat.RGBA32,false);
        var pixels=new Color[16];for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white;
        texture.SetPixels(pixels);texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0f,0f,4f,4f),new Vector2(.5f,0f),4f);
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        primary=Unit(0,Vector3.zero);secondary=Unit(0,Vector3.forward*.3f);
        enemy=Unit(1,Vector3.right*.8f);previousCaster=Unit(0,Vector3.back*.3f);
        SetUnit(primary,"target",enemy);SetUnit(secondary,"target",enemy);
        SetUnit(enemy,"target",primary);SetUnit(previousCaster,"target",enemy);
        RegisterSpy();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if(battle!=null)Call(battle,"CancelCommonSkillFocus");
            if(director!=null)director.EndBattle();
            Object.DestroyImmediate(host);
            foreach(GameObject model in models)Object.DestroyImmediate(model);
            Object.DestroyImmediate(cameraHost);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
        }
        finally
        {
            typeof(GameFrontend).GetField("instance",Static).SetValue(null,previousFrontend);
            Time.timeScale=previousSpeed;
        }
    }

    [TestCase(.5f)] [TestCase(2f)]
    public void AlliedPreparationSlowsTheSelectedSpeedAndPlacesItsCasterAboveTheHalfShade(float speed)
    {
        Time.timeScale=speed;Begin(primary);
        SpriteRenderer shade=Shade;
        Assert.That(Time.timeScale,Is.EqualTo(speed*.08f).Within(.000001f));
        Assert.That(Get<float>(battle,"allyPreparationSavedSpeed"),Is.EqualTo(speed));
        Assert.That(Get<bool>(battle,"allyPreparationSequenceActive"),Is.True);
        Assert.That(Get<double>(battle,"allyPreparationSequenceEndsAt")-Get<double>(Local(primary),"startedAt"),
            Is.EqualTo(1d).Within(.000001d));
        Assert.That(shade,Is.Not.Null);Assert.That(shade.gameObject.activeSelf,Is.True);
        Assert.That(shade.color,Is.EqualTo(new Color(0f,0f,0f,.5f)));
        Assert.That(Foreground(primary).sortingOrder,Is.GreaterThan(shade.sortingOrder));
        Assert.That(Driver(primary).IsSkillAnticipating,Is.True);
        Assert.That(FocusScale(primary),Is.EqualTo(1.5f));
        Assert.That(GetUnit<float>(primary,"collisionRadius"),Is.EqualTo(.3f));
        Assert.That(GetUnit<float>(primary,"combatBodyScale"),Is.EqualTo(1f));
        Assert.That(CombatSkillClock.Active,Is.False,"Preparation slows the world without acquiring the exclusive cinematic clock.");
    }

    [Test]
    public void AnEnemyPreparingAloneKeepsTheWorldSpeedAndDoesNotCreateTheShade()
    {
        Time.timeScale=2f;Begin(enemy);
        Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(Shade,Is.Null);
        Assert.That(Get<bool>(battle,"allyPreparationSequenceActive"),Is.False);
        Assert.That(Get<bool>(Local(enemy),"allyPreparationMember"),Is.False);
        Assert.That(Driver(enemy).IsSkillAnticipating,Is.True);
        Assert.That(FocusScale(enemy),Is.EqualTo(1f));
        Assert.That(Count(enemy),Is.Zero);
        Set(Local(enemy),"startedAt",Time.unscaledTimeAsDouble-.349d);Update();
        Assert.That(Count(enemy),Is.Zero);
        Mature(enemy);Update();
        Assert.That(Count(enemy),Is.EqualTo(1));
        Assert.That(commitAges[enemy],Is.GreaterThanOrEqualTo(.35d).And.LessThan(1d));
        Assert.That(commitSpeeds[enemy],Is.EqualTo(2f));
        Assert.That(commitScales[enemy],Is.EqualTo(1f));
    }

    [Test]
    public void ALaterAllyExtendsTheBatchAndBothSkillsReleaseOnceAtNormalSpeedAfterTheirOwnMinimum()
    {
        Time.timeScale=2f;Begin(primary);
        object first=Local(primary);
        double now=Time.unscaledTimeAsDouble;
        Set(first,"startedAt",now-.2d);Set(battle,"allyPreparationSequenceEndsAt",now+.8d);
        Begin(secondary);object second=Local(secondary);
        Assert.That(Get<double>(battle,"allyPreparationSequenceEndsAt")-Get<double>(second,"startedAt"),
            Is.EqualTo(1d).Within(.000001d));
        Assert.That(Get<bool>(first,"allyPreparationMember"),Is.True);
        Assert.That(Get<bool>(second,"allyPreparationMember"),Is.True);

        // The first ally has finished its own second, but the new ally still needs the rest of its full second.
        now=Time.unscaledTimeAsDouble;
        Set(first,"startedAt",now-1.3d);Set(second,"startedAt",now-.999d);
        Set(battle,"allyPreparationSequenceEndsAt",now+.001d);
        Update();
        Assert.That(Count(primary),Is.Zero);Assert.That(Count(secondary),Is.Zero);
        Assert.That(Time.timeScale,Is.EqualTo(.16f).Within(.000001f));
        Assert.That(Foreground(primary).sortingOrder,Is.GreaterThan(Shade.sortingOrder));
        Assert.That(Foreground(secondary).sortingOrder,Is.GreaterThan(Shade.sortingOrder));
        Assert.That(FocusScale(primary),Is.EqualTo(1.5f));Assert.That(FocusScale(secondary),Is.EqualTo(1.5f));

        now=Time.unscaledTimeAsDouble;
        Set(first,"startedAt",now-1.301d);Set(second,"startedAt",now-1.001d);
        Set(battle,"allyPreparationSequenceEndsAt",now-.001d);
        Update();Update();
        foreach(object caster in new[]{primary,secondary})
        {
            Assert.That(Count(caster),Is.EqualTo(1));Assert.That(commitAges[caster],Is.GreaterThanOrEqualTo(1d));
            Assert.That(commitSpeeds[caster],Is.EqualTo(2f));Assert.That(commitShades[caster],Is.False);
            Assert.That(commitScales[caster],Is.EqualTo(1.5f));Assert.That(FocusScale(caster),Is.EqualTo(1.5f));
            Assert.That(Driver(caster).IsSkillActionActive,Is.True);
            Assert.That(GetUnit<float>(caster,"collisionRadius"),Is.EqualTo(.3f));
            Assert.That(GetUnit<float>(caster,"combatBodyScale"),Is.EqualTo(1f));
            Assert.That(Get<object>(Local(caster),"pending"),Is.Null);
            Assert.That(GetUnit<int>(caster,"skillUses"),Is.EqualTo(1));
        }
        Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(Shade.gameObject.activeSelf,Is.False);
    }

    [Test]
    public void OnlyTheJoinedAlliesAreAboveTheShadeWhileEnemyAndAnAlreadyCastingAllyStayBelowIt()
    {
        Begin(previousCaster);Mature(previousCaster);
        Set(battle,"allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble-.001d);Update();
        Assert.That(Count(previousCaster),Is.EqualTo(1));Assert.That(FocusScale(previousCaster),Is.EqualTo(1.5f));
        Begin(enemy);Begin(primary);Begin(secondary);
        foreach(object caster in new[]{primary,secondary})
        {
            Assert.That(Foreground(caster).enabled,Is.True);
            Assert.That(Foreground(caster).sortingOrder,Is.GreaterThan(Shade.sortingOrder));
            Assert.That(GetUnit<ScreenSpaceOutlineTarget>(caster,"outline").IsOutlined,Is.True);
            Assert.That(FocusScale(caster),Is.EqualTo(1.5f));
        }
        foreach(object caster in new[]{enemy,previousCaster})
        {
            Assert.That(Foreground(caster).enabled,Is.True);
            Assert.That(Foreground(caster).sortingOrder,Is.LessThan(Shade.sortingOrder));
            Assert.That(GetUnit<ScreenSpaceOutlineTarget>(caster,"outline").IsOutlined,Is.False);
            Assert.That(FocusScale(caster),Is.EqualTo(caster==previousCaster?1.5f:1f));
        }
        Assert.That(Get<object>(Local(enemy),"pending"),Is.Not.Null);
        Assert.That(Driver(previousCaster).IsSkillActionActive,Is.True);
        Finish(previousCaster);
        Assert.That(FocusScale(previousCaster),Is.EqualTo(1f));
        Assert.That(Get<bool>(battle,"allyPreparationSequenceActive"),Is.True);
        Assert.That(Shade.gameObject.activeSelf,Is.True);
        Assert.That(FocusScale(primary),Is.EqualTo(1.5f));Assert.That(FocusScale(secondary),Is.EqualTo(1.5f));
    }

    [Test]
    public void FreezingTheLastMemberRestoresTheWorldButKeepsItsAcceptedSkillForRecovery()
    {
        Begin(primary);object presentation=Local(primary),request=Get<object>(presentation,"pending");
        var statuses=GetUnit<CombatStatusState>(primary,"combatStatuses");
        statuses.ApplyTimed(CombatStatusKind.Freeze,1f);Update();
        Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(Shade.gameObject.activeSelf,Is.False);
        Assert.That(Get<bool>(battle,"allyPreparationSequenceActive"),Is.False);
        Assert.That(Get<bool>(presentation,"allyPreparationMember"),Is.False);
        Assert.That(Get<object>(presentation,"pending"),Is.SameAs(request));
        Assert.That(Get<bool>(presentation,"active"),Is.True);Assert.That(Count(primary),Is.Zero);
        Assert.That(Driver(primary).IsSkillAnticipating,Is.False);
        Assert.That(FocusScale(primary),Is.EqualTo(1f));
        statuses.Remove(CombatStatusKind.Freeze);Mature(primary);Update();Update();
        Assert.That(Count(primary),Is.EqualTo(1));Assert.That(commitSpeeds[primary],Is.EqualTo(1f));
        Assert.That(commitScales[primary],Is.EqualTo(1.5f));
    }

    [TestCase(0f)] [TestCase(2f)]
    public void ExternalPauseOrSpeedSelectionIsNeverOverwrittenBySequenceCleanup(float externalSpeed)
    {
        Begin(primary);object request=Get<object>(Local(primary),"pending");
        Time.timeScale=externalSpeed;Update();
        Assert.That(Time.timeScale,Is.EqualTo(externalSpeed));
        Assert.That(Get<bool>(battle,"allyPreparationSequenceActive"),Is.False);
        Assert.That(Shade.gameObject.activeSelf,Is.False);
        Assert.That(FocusScale(primary),Is.EqualTo(1f));
        Assert.That(Get<object>(Local(primary),"pending"),Is.SameAs(request));Assert.That(Count(primary),Is.Zero);
        Call(battle,"CancelCommonSkillFocus");Assert.That(Time.timeScale,Is.EqualTo(externalSpeed));
    }

    [TestCase(false)] [TestCase(true)]
    public void PublicPauseAndSystemMenuHooksRestoreTheResumeSpeedWithoutDiscardingTheAcceptedRequest(bool systemMenu)
    {
        Time.timeScale=2f;Begin(primary);
        object presentation=Local(primary),request=Get<object>(presentation,"pending");
        if(systemMenu)battle.PrepareForSystemMenu();else CombatCameraDirector.PrepareForGameplayPause();
        Assert.That(Time.timeScale,Is.EqualTo(2f));Assert.That(Shade.gameObject.activeSelf,Is.False);
        Assert.That(Get<bool>(battle,"allyPreparationSequenceActive"),Is.False);
        Assert.That(FocusScale(primary),Is.EqualTo(1f));
        Assert.That(Get<object>(presentation,"pending"),Is.SameAs(request));
        Time.timeScale=0f;Update();Assert.That(Count(primary),Is.Zero);
        Assert.That(Get<object>(presentation,"pending"),Is.SameAs(request));
        Time.timeScale=2f;Mature(primary);Update();Update();
        Assert.That(Count(primary),Is.EqualTo(1));Assert.That(commitSpeeds[primary],Is.EqualTo(2f));
        Assert.That(commitScales[primary],Is.EqualTo(1.5f));
    }

    private SpriteRenderer Shade=>Get<SpriteRenderer>(battle,"allyPreparationShade");
    private SpriteMonsterAnimationDriver Driver(object caster)=>GetUnit<SpriteMonsterAnimationDriver>(caster,"spriteAnimation");
    private float FocusScale(object caster)=>Get<float>(Driver(caster),"skillFocusScale");
    private SpriteRenderer Foreground(object caster)=>Get<SkillFocusSpriteMirror>(Local(caster),"mirror").GetComponent<SpriteRenderer>();
    private int Count(object caster)=>commits.TryGetValue(caster,out int count)?count:0;
    private void Begin(object caster)=>Assert.That(Call(battle,"BeginCommonSkillFocus",caster,false),Is.True);
    private void Update()=>Call(battle,"UpdateCommonSkillFocus");
    private void Mature(object caster)
    {
        double duration=Convert.ToInt32(GetUnit<object>(caster,"team"))==0?1d:.35d;
        Set(Local(caster),"startedAt",Time.unscaledTimeAsDouble-duration-.001d);
    }
    private void Finish(object caster)
    {
        object state=Local(caster);SpriteMonsterAnimationDriver driver=Driver(caster);
        Call(driver,"Advance",driver.SkillAttackDuration+1f);Call(driver,"LateUpdate");
        Assert.That(driver.IsSkillActionActive,Is.False,"The native action must end before clearing its emphasis.");
        // EditMode manual steps leave Time.time at zero; a negative recoveryAt is the runtime's unset sentinel.
        Call(battle,"ClearLocalSkillPresentation",state);
    }
    private object Local(object caster)=>Call(battle,"SkillPresentationFor",caster);

    private bool Commit<TContext>(TContext context,SkillMasterRow skill)
    {
        object caster=context.GetType().GetField("source").GetValue(context);
        commits[caster]=Count(caster)+1;commitSpeeds[caster]=Time.timeScale;
        commitAges[caster]=Time.unscaledTimeAsDouble-Get<double>(Local(caster),"startedAt");
        commitShades[caster]=Shade!=null&&Shade.gameObject.activeSelf;
        commitScales[caster]=FocusScale(caster);
        SetUnit(caster,"skillUses",Count(caster));SetUnit(caster,"skillTimer",4f);
        Driver(caster).PlaySkillAttack(1f,null);
        return true;
    }

    private void RegisterSpy()
    {
        FieldInfo field=typeof(MonsterAutoBattlePrototype).GetField("skillExecutor",Private);
        object executor=Activator.CreateInstance(field.FieldType);field.SetValue(battle,executor);
        Type context=field.FieldType.GetGenericArguments()[0];
        Type handler=typeof(Func<,,>).MakeGenericType(context,typeof(SkillMasterRow),typeof(bool));
        Delegate spy=Delegate.CreateDelegate(handler,this,GetType().GetMethod("Commit",Private).MakeGenericMethod(context));
        field.FieldType.GetMethod("Register").Invoke(executor,new object[]{
            GameMasterData.Skill("guardian_taunt").behavior,MonsterSkillTrigger.Active,spy});
    }

    private object Unit(int team,Vector3 position)
    {
        var model=new GameObject("Synthetic preparation caster "+models.Count);models.Add(model);
        model.transform.position=position;
        var renderer=model.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
        var driver=model.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames={sprite,sprite,sprite,sprite};driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        object caster=Activator.CreateInstance(unitType,true);
        SetUnit(caster,"model",model);SetUnit(caster,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        SetUnit(caster,"definition",Resources.Load<MonsterDefinition>("MonsterData/Raputo"));
        SetUnit(caster,"id",models.Count);SetUnit(caster,"stableId",Guid.NewGuid().ToString());
        SetUnit(caster,"skillId","guardian_taunt");SetUnit(caster,"alive",true);
        SetUnit(caster,"hp",5000f);SetUnit(caster,"maxHp",5000f);SetUnit(caster,"damage",100f);
        SetUnit(caster,"attackInterval",1f);SetUnit(caster,"attackRange",1);SetUnit(caster,"collisionRadius",.3f);
        SetUnit(caster,"spriteAnimation",driver);SetUnit(caster,"outline",model.AddComponent<ScreenSpaceOutlineTarget>());
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle)).Add(caster);
        return caster;
    }

    private void SetUnit(object caster,string field,object value)=>unitType.GetField(field).SetValue(caster,value);
    private T GetUnit<T>(object caster,string field)=>(T)unitType.GetField(field).GetValue(caster);
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,Fields).SetValue(target,value);
    private static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Fields).GetValue(target);
    private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
}
