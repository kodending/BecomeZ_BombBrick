using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BombBrick.Combat;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class MammothSkillLifecycleTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly Type battleType=typeof(MonsterAutoBattlePrototype);
    private HashSet<GameObject> existing;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private object source,target,previousFrontend;
    private SpriteMonsterAnimationDriver driver;
    private float previousTimeScale;

    [SetUp] public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        previousTimeScale=Time.timeScale;Time.timeScale=1f;
        var frontend=typeof(GameFrontend).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        previousFrontend=frontend.GetValue(null);frontend.SetValue(null,null);
        var host=new GameObject("Inactive mammoth lifecycle test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        SetBattle("battleStarted",true);SetBattle("combatBounds",new Bounds(Vector3.zero,Vector3.one*100f));
        var run=battleType.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        var cameraHost=new GameObject("Mammoth lifecycle camera");var camera=cameraHost.AddComponent<Camera>();
        var director=cameraHost.AddComponent<CombatCameraDirector>();
        typeof(CombatCameraDirector).GetField("battleCamera",Private).SetValue(director,camera);
        SetBattle("mainCamera",camera);SetBattle("combatCameraDirector",director);
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/057_kogason"));
        model.transform.position=Vector3.zero;
        driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        driver.SetCombatPlaybackSpeed(CombatStatFormula.CombatAnimationRate);
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{0f});
        unitType=battleType.GetNestedType("Unit",BindingFlags.NonPublic);
        source=Unit(model,0);target=Unit(new GameObject("Mammoth lifecycle target"),1);
        Model(target).transform.position=Vector3.right*.8f;
        SetUnit(source,"spriteAnimation",driver);SetUnit(source,"target",target);
    }

    [TearDown] public void TearDown()
    {
        try
        {
            if(battle!=null)Call("CancelCommonSkillFocus");
            foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
        }
        finally
        {
            typeof(GameFrontend).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,previousFrontend);
            Time.timeScale=previousTimeScale;
        }
    }

    [TestCase("charge")] [TestCase("wall")] [TestCase("toss")] [TestCase("brace")]
    public void RealPreparedMammothCastFinishesAtPacedSpeedAndReturnsToNormalAttacks(string variant)
    {
        ConfigureVariant(variant);
        Assert.That(Call("BeginCommonSkillFocus",source,false),Is.True);
        Assert.That(Call("IsPreparingSkill",source),Is.True);Assert.That(driver.IsSkillAnticipating,Is.True);
        Assert.That(GetUnit<int>(source,"skillUses"),Is.Zero);Assert.That(MammothState(variant),Is.Null);
        var presentation=Presentation();
        float preparation=(float)battleType.GetMethod("SkillPreparationDuration",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{source});
        presentation.GetType().GetField("startedAt").SetValue(presentation,Time.unscaledTimeAsDouble-preparation+.001d);
        SetBattle("allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble+.001d);
        Call("UpdateCommonSkillFocus");
        Assert.That(MammothState(variant),Is.Null,"Preparation must not create a body action early.");
        presentation.GetType().GetField("startedAt").SetValue(presentation,Time.unscaledTimeAsDouble-preparation-.001d);
        SetBattle("allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble-.001d);
        Call("UpdateCommonSkillFocus");
        Assert.That(Call("IsPreparingSkill",source),Is.False);Assert.That(driver.IsSkillAnticipating,Is.False);
        Assert.That(MammothState(variant),Is.Not.Null);Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));
        Assert.That(DriverField<bool>("mammothChargeAttack"),Is.True);

        float elapsed=0f;
        if(variant=="brace")
        {
            Vector3 position=Model(source).transform.position;
            for(int i=0;i<120;i++){Step(1f/60f);elapsed+=1f/60f;}
            object brace=MammothState(variant);
            Assert.That(brace,Is.Not.Null);
            Assert.That((float)brace.GetType().GetField("time").GetValue(brace),Is.EqualTo(1.6f).Within(.0001f),
                "The planted guard must keep progressing even while the body remains stationary.");
            Assert.That(Model(source).transform.position,Is.EqualTo(position));
        }
        int frames=0;
        while(MammothState(variant)!=null&&frames++<360){Step(1f/60f);elapsed+=1f/60f;}
        Assert.That(MammothState(variant),Is.Null,"A completed real driver must release the unit's action state.");
        Assert.That(elapsed,Is.EqualTo(ActionEnd(variant)/CombatStatFormula.CombatAnimationRate).Within(2f/60f));
        Assert.That(DriverField<bool>("mammothChargeAttack"),Is.False);Assert.That(driver.IsActionLocked,Is.False);
        Assert.That(GetUnit<bool>(source,"mammothChargePending"),Is.False);
        Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));
        AssertNormalAttackResumes();
    }

    [Test]
    public void CooldownChainCollisionFinishesTheChargeWhileItsSurvivingVictimSlidesAndThenResumes()
    {
        SetUnit(source,"augmentTranscended",true);SetUnit(target,"hp",999f);
        Vector3 start=Model(source).transform.position;
        Assert.That(Call("BeginEarthCharge",source,GameMasterData.Skill(MammothCombatRules.SkillId)),Is.True);
        Assert.That(MammothState("expulsion"),Is.Not.Null);
        int frames=0;
        while(MammothState("expulsion")!=null&&frames++<180)Step(1f/60f);
        float expected=MammothExpulsionRules.Contact/CombatStatFormula.CombatAnimationRate+MammothExpulsionRules.ChargeDuration;
        Assert.That(frames/60f,Is.EqualTo(expected).Within(2f/60f));
        Assert.That(MammothState("expulsion"),Is.Null);Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));
        Assert.That(Vector3.Distance(start,Model(source).transform.position),Is.GreaterThan(.5f));
        Assert.That(GetUnit<bool>(target,"alive"),Is.True);Assert.That(GetUnit<float>(target,"hp"),Is.InRange(1f,998f));
        Assert.That(GetUnit<bool>(target,"mammothRicochetActive"),Is.True);Assert.That(driver.IsActionLocked,Is.False);
        for(int i=0;i<240&&GetUnit<bool>(target,"mammothRicochetActive");i++)Call("UpdateMammothExpulsionFlights",1f/60f);
        Assert.That(GetUnit<bool>(target,"mammothRicochetActive"),Is.False,"The 3.2-second chain cannot leave the victim's movement permanently owned.");
        AssertNormalAttackResumes();
    }

    [TestCase(0)] [TestCase(1)]
    public void TranscendedMammothCannotAutoCastBeforeItsOrdinarySkillCooldownAndCanBasicAttack(int team)
    {
        SetTeam(team);SetUnit(source,"augmentTranscended",true);
        SetUnit(source,"skillTimer",10f);SetUnit(source,"cooldown",10f);
        Call("UpdateUnitCore",source,.1f);
        Assert.That(MammothState("expulsion"),Is.Null);Assert.That(Call("IsPreparingSkill",source),Is.False);
        Assert.That(GetUnit<int>(source,"skillUses"),Is.Zero);
        Assert.That(GetUnit<float>(source,"skillTimer"),Is.InRange(9f,10f));
        AssertNormalAttackResumes(true);
        Assert.That(GetUnit<bool>(source,"augmentTranscended"),Is.True);
        Assert.That(MammothState("expulsion"),Is.Null);Assert.That(GetUnit<int>(source,"skillUses"),Is.Zero);
    }

    [TestCase("charge",0)] [TestCase("wall",0)] [TestCase("toss",0)] [TestCase("brace",0)]
    [TestCase("charge",1)] [TestCase("wall",1)] [TestCase("toss",1)] [TestCase("brace",1)]
    public void TranscendedMammothReplacesEveryRank3VariantAndRepeatsThroughTheNormalCooldown(string variant,int team)
    {
        ConfigureVariant(variant);SetTeam(team);SetUnit(source,"augmentTranscended",true);
        SetUnit(source,"skillTimer",0f);SetUnit(source,"cooldown",10f);
        Model(target).transform.position=Vector3.right*25f;
        Assert.That(Call("TryUseSkill",source,false),Is.True);
        Assert.That(Call("IsPreparingSkill",source),Is.True);Assert.That(GetUnit<int>(source,"skillUses"),Is.Zero);
        ReleasePreparation();AssertChainReplacedRank3(1);
        Assert.That(GetUnit<float>(source,"skillTimer"),Is.EqualTo(GetUnit<float>(source,"skillCooldownDuration")));
        int frames=0;
        while(MammothState("expulsion")!=null&&frames++<180)Step(1f/60f);
        Assert.That(MammothState("expulsion"),Is.Null);Assert.That(driver.IsActionLocked,Is.False);
        Call("UpdateMammothExpulsionFlights",MammothExpulsionRules.Duration);
        ExpirePresentation();
        Assert.That(Call("TryUseSkill",source,false),Is.False,"A completed rank-4 cast still has its ordinary cooldown.");
        Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(1));
        SetUnit(source,"skillTimer",.01f);Call("UpdateUnitCore",source,.02f);
        Assert.That(Call("IsPreparingSkill",source),Is.True,"The ordinary countdown must trigger the second cast when it expires.");
        ReleasePreparation();AssertChainReplacedRank3(2);
        Assert.That(GetUnit<float>(source,"skillTimer"),Is.EqualTo(GetUnit<float>(source,"skillCooldownDuration")));
    }

    [TestCase("wall")] [TestCase("toss")] [TestCase("brace")]
    public void TranscendedMammothEchoCannotReintroduceTheReplacedRank3Skill(string variant)
    {
        ConfigureVariant(variant);SetUnit(source,"augmentTranscended",true);
        SetUnit(source,"skillEchoId",MammothCombatRules.SkillId);SetUnit(source,"skillEchoTarget",target);
        SetUnit(source,"rank3EchoScale",.65f);SetUnit(source,"skillTimer",30f);
        Call("ExecuteSkillEcho",source);
        Assert.That(GetUnit<string>(source,"skillEchoId"),Is.Empty);Assert.That(GetUnit<object>(source,"skillEchoTarget"),Is.Null);
        Assert.That(MammothState("charge"),Is.Null);Assert.That(MammothState("toss"),Is.Null);
        Assert.That(MammothState("brace"),Is.Null);Assert.That(MammothState("expulsion"),Is.Null);
        Assert.That(GetUnit<bool>(source,"mammothChargePending"),Is.False);
        Assert.That(GetUnit<int>(source,"skillUses"),Is.Zero);Assert.That(GetUnit<float>(source,"skillTimer"),Is.EqualTo(30f));
        Assert.That(GetUnit<float>(target,"hp"),Is.EqualTo(5000f));Assert.That(driver.IsActionLocked,Is.False);
    }

    [TestCase("charge")] [TestCase("wall")] [TestCase("toss")] [TestCase("brace")]
    public void RejectedDriverStartDoesNotLeaveAMammothStateThatStopsNormalAiForever(string variant)
    {
        ConfigureVariant(variant);
        SetUnit(source,"mammothChargeStart",Vector3.zero);SetUnit(source,"mammothChargeEnd",Vector3.right*5.95f);
        SetUnit(source,"cooldown",10f);SetUnit(source,"skillTimer",30f);
        driver.PlayBasicAttack(1f,null);Assert.That(driver.IsActionLocked,Is.True);
        string start=variant=="toss"?"StartMammothToss":variant=="brace"?"StartMammothBrace":"StartMammothCharge";
        Call(start,source,0);
        for(int i=0;i<180;i++)Step(1f/60f);
        Assert.That(MammothState(variant),Is.Null,
            "If the real driver refuses a locked start, no orphan action may keep UpdateUnitCore returning forever.");
        Assert.That(DriverField<bool>("mammothChargeAttack"),Is.False);
        AssertNormalAttackResumes();
    }

    private void ConfigureVariant(string variant)
    {
        string trait=variant=="wall"?MammothRank3Rules.WallRush:variant=="toss"?MammothRank3Rules.MountainToss:
            variant=="brace"?MammothRank3Rules.MountainBrace:null;
        if(trait!=null)((IList)unitType.GetField("activeTraitIds").GetValue(source)).Add(trait);
    }
    private void SetTeam(int team)
    {
        SetUnit(source,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        SetUnit(target,"team",Enum.ToObject(unitType.GetField("team").FieldType,1-team));
    }
    private void ReleasePreparation()
    {
        object presentation=Presentation();Assert.That(presentation,Is.Not.Null);
        float duration=(float)battleType.GetMethod("SkillPreparationDuration",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{source});
        presentation.GetType().GetField("startedAt").SetValue(presentation,Time.unscaledTimeAsDouble-duration-.01d);
        SetBattle("allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble-.01d);Call("UpdateCommonSkillFocus");
        Assert.That(Call("IsPreparingSkill",source),Is.False);
    }
    private void ExpirePresentation()
    {
        object presentation=Presentation();if(presentation==null)return;
        Assert.That((bool)battleType.GetMethod("CommonSkillActionActive",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new[]{source}),Is.False,"The native action must finish before its visual fade is cleared.");
        // EditMode manual driver steps leave Time.time at zero, so a negative recoveryAt is the runtime's unset sentinel.
        Call("ClearLocalSkillPresentation",presentation);Assert.That(Presentation(),Is.Null);
    }
    private void AssertChainReplacedRank3(int uses)
    {
        Assert.That(MammothState("expulsion"),Is.Not.Null);Assert.That(GetUnit<int>(source,"skillUses"),Is.EqualTo(uses));
        Assert.That(MammothState("charge"),Is.Null);Assert.That(MammothState("toss"),Is.Null);Assert.That(MammothState("brace"),Is.Null);
        Assert.That(GetUnit<bool>(source,"mammothChargePending"),Is.False);
        Assert.That(DriverField<bool>("mammothChargeAttack"),Is.True);
        Assert.That(DriverField<bool>("mammothExpulsionAttack"),Is.True);
        Assert.That(driver.IsActionLocked,Is.True,"Generic skill animation must not cancel the rank-4 driver.");
    }
    private object MammothState(string variant)=>unitType.GetField(variant=="toss"?"mammothToss":
        variant=="brace"?"mammothBrace":variant=="expulsion"?"mammothExpulsion":"mammothCharge").GetValue(source);
    private static float ActionEnd(string variant)=>variant=="wall"?MammothRank3Rules.WallRushEnd:
        variant=="toss"?MammothRank3Rules.TossCastEnd:variant=="brace"?MammothRank3Rules.BraceEnd:MammothCombatRules.EndTime;
    private object Presentation()=>Call("SkillPresentationFor",source);
    private void Step(float dt)
    {
        Call("UpdateUnitCore",source,dt);
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
        Call("UpdateMammothExpulsionFlights",dt);
    }
    private void AssertNormalAttackResumes(bool preserveRank4=false)
    {
        object presentation=Presentation();
        if(presentation!=null)
        {
            Assert.That(presentation.GetType().GetField("pending").GetValue(presentation),Is.Null);
            // Manual driver steps do not advance Unity's scaled clock. A visual fade may still be present,
            // but it must not retain the preparation hold or prevent the ordinary AI from attacking.
        }
        Assert.That(driver.IsSkillAnticipating,Is.False);
        if(!GetUnit<bool>(target,"alive"))
        {
            target=Unit(new GameObject("Mammoth replacement target"),1);
            SetUnit(source,"target",target);
        }
        Model(target).transform.position=Model(source).transform.position+Vector3.right*1.2f;
        SetUnit(target,"displacing",false);SetUnit(source,"skillTimer",30f);SetUnit(source,"cooldown",0f);
        SetUnit(source,"attackMovementLockTimer",0f);SetUnit(source,"inertiaVelocity",Vector3.zero);
        if(!preserveRank4)SetUnit(source,"augmentTranscended",false);
        int attacks=GetUnit<int>(source,"basicAttackCount");
        LogAssert.Expect(LogType.Error,"Coroutine couldn't be started because the the game object 'Inactive mammoth lifecycle test' is inactive!");
        Call("UpdateUnitCore",source,1f/60f);
        Assert.That(GetUnit<int>(source,"basicAttackCount"),Is.EqualTo(attacks+1),
            "The ordinary AI must issue its next attack, not remain in the completed mammoth state.");
    }
    private object Unit(GameObject model,int team)
    {
        object unit=Activator.CreateInstance(unitType,true);
        SetUnit(unit,"model",model);SetUnit(unit,"ring",new GameObject("Mammoth lifecycle ring"));
        SetUnit(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        SetUnit(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_kogason"));
        SetUnit(unit,"stableId",Guid.NewGuid().ToString());SetUnit(unit,"skillId",MammothCombatRules.SkillId);
        SetUnit(unit,"alive",true);SetUnit(unit,"hp",5000f);SetUnit(unit,"maxHp",5000f);
        SetUnit(unit,"damage",100f);SetUnit(unit,"attackInterval",1f);SetUnit(unit,"attackRange",1);
        SetUnit(unit,"collisionRadius",.3f);SetUnit(unit,"speedTilesPerSecond",2f);SetUnit(unit,"skillCooldownDuration",30f);
        ((IList)battleType.GetField("units",Private).GetValue(battle)).Add(unit);return unit;
    }
    private GameObject Model(object unit)=>GetUnit<GameObject>(unit,"model");
    private T GetUnit<T>(object unit,string name)=>(T)unitType.GetField(name).GetValue(unit);
    private void SetUnit(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    private void SetBattle(string name,object value)=>battleType.GetField(name,Private).SetValue(battle,value);
    private T DriverField<T>(string name)=>(T)typeof(SpriteMonsterAnimationDriver).GetField(name,Private).GetValue(driver);
    private object Call(string name,params object[] args)=>battleType.GetMethod(name,Private).Invoke(battle,args);
}
