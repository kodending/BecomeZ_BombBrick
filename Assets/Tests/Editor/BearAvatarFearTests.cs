using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class BearAvatarFearTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Static;
    private readonly Type type=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object bear,near,far,ally;
    private UnityEngine.Random.State randomState;

    [SetUp]
    public void SetUp()
    {
        randomState=UnityEngine.Random.state;UnityEngine.Random.InitState(5614);
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Avatar fear test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        bear=Unit(0,0);near=Unit(1,3);far=Unit(1,30);ally=Unit(0,-3);
        Set(bear,"augmentTranscended",true);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Random.state=randomState;
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)
                Object.DestroyImmediate(go);
    }

    private object Unit(int team,float x)
    {
        object u=Activator.CreateInstance(unitType,true);
        var units=(IList)type.GetField("units",Private).GetValue(battle);
        var model=new GameObject("Fear unit");model.transform.position=Vector3.right*x;
        Set(u,"id",units.Count);Set(u,"stableId","fear-test-"+units.Count);
        Set(u,"model",model);Set(u,"ring",new GameObject("Fear ring"));
        Set(u,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(u,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_woongi"));
        Set(u,"skillId",BearCombatRules.SkillId);Set(u,"alive",true);
        Set(u,"hp",5000f);Set(u,"maxHp",5000f);Set(u,"damage",100f);
        Set(u,"attackInterval",1f);Set(u,"collisionRadius",.4f);
        Set(u,"speedTilesPerSecond",1f);Set(u,"skillCooldownDuration",5f);Set(u,"skillTimer",3f);
        Status(u).Applied+=effect=>Call("HandleCombatStatusApplied",u,effect);
        units.Add(u);return u;
    }
    private void Set(object u,string field,object value)=>unitType.GetField(field).SetValue(u,value);
    private object Get(object u,string field)=>unitType.GetField(field).GetValue(u);
    private object Call(string method,params object[] args)=>type.GetMethod(method,Private).Invoke(battle,args);
    private CombatStatusState Status(object u)=>(CombatStatusState)Get(u,"combatStatuses");
    private Vector3 Position(object u)=>((GameObject)Get(u,"model")).transform.position;
    private void Roar()=>Call("ResolveBearAvatarRoar",bear);

    [Test]
    public void RoarFearsAllEnemiesWithoutAnyDamageOrFriendlyControl()
    {
        Roar();
        foreach(var enemy in new[]{near,far})
        {
            Assert.That(Status(enemy).Get(CombatStatusKind.Fear).remaining,Is.EqualTo(1f));
            Assert.That(Get(enemy,"hp"),Is.EqualTo(5000f));
            Assert.That(Get(enemy,"displacing"),Is.False);
        }
        Assert.That(Status(ally).HarmfulCount,Is.Zero);
        Assert.That(Status(bear).HarmfulCount,Is.Zero);
        Assert.That(Get(bear,"skillHits"),Is.EqualTo(0));
        Assert.That(Get(bear,"skillDamage"),Is.EqualTo(0f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ImmunityRejectsFearWithoutInterruptingTheTarget(bool control)
    {
        if(control)Status(near).ControlImmune=true;else Status(near).DebuffImmune=true;
        Set(near,"target",bear);Set(near,"bearSpiritPending",true);
        Roar();
        Assert.That(Status(near).Has(CombatStatusKind.Fear),Is.False);
        Assert.That(Get(near,"target"),Is.SameAs(bear));
        Assert.That(Get(near,"bearSpiritPending"),Is.True);
    }

    [Test]
    public void ResistanceAndHarmfulDurationMultiplierShortenFear()
    {
        Set(near,"statusResistance",100f);Status(near).harmfulDurationMultiplier=.5f;
        float expected=(float)Call("HostileStatusDuration",near,1f)*.5f;
        Roar();Assert.That(Status(near).Get(CombatStatusKind.Fear).remaining,Is.EqualTo(expected).Within(.001f));
        Assert.That(expected,Is.LessThan(1f));
    }

    [Test]
    public void RandomDirectionsDifferPerEnemyAndRemainStableWhileWalking()
    {
        Roar();Vector3 a=(Vector3)Get(near,"fearDirection"),b=(Vector3)Get(far,"fearDirection");
        Assert.That(a.magnitude,Is.EqualTo(1f).Within(.001f));
        Assert.That(b.magnitude,Is.EqualTo(1f).Within(.001f));
        Assert.That(Vector3.Distance(a,b),Is.GreaterThan(.1f));
        Vector3 previous=Position(near);Call("UpdateFearMovement",near,.1f);
        Assert.That((Position(near)-previous).magnitude,Is.GreaterThan(0f));
        Assert.That((Position(near)-previous).magnitude,Is.LessThanOrEqualTo(.171f));
        Assert.That(Get(near,"fearDirection"),Is.EqualTo(a));
    }

    [Test]
    public void RootedFearDoesNotMoveAndCleanseImmediatelyPermitsSkillsAgain()
    {
        Roar();Status(near).ApplyTimed(CombatStatusKind.Root,2f);
        Vector3 previous=Position(near);Call("UpdateFearMovement",near,.2f);
        Assert.That(Position(near),Is.EqualTo(previous));
        Assert.That(Call("TryUseSkill",near,true),Is.False);
        Call("CleanseAllHarmfulEffects",near);
        Assert.That(Status(near).Has(CombatStatusKind.Fear),Is.False);
        Assert.That(Call("SelectTarget",near,false,null),Is.Not.Null);
    }

    [Test]
    public void FearCancelsPreparationAndQueuedBasicAttacks()
    {
        Set(near,"target",bear);Set(near,"bearSpiritPending",true);
        Set(near,"wizardBarragePending",true);Set(near,"giraffeSlamming",true);
        Set(near,"fangLockTarget",bear);Set(near,"preyDragTarget",bear);Set(bear,"preyDraggedBy",near);
        Roar();
        Assert.That(Get(near,"bearSpiritPending"),Is.False);
        Assert.That(Get(near,"wizardBarragePending"),Is.False);
        Assert.That(Get(near,"giraffeSlamming"),Is.False);
        Assert.That(Get(near,"fangLockTarget"),Is.Null);
        Assert.That(Get(bear,"preyDraggedBy"),Is.Null);
        Assert.That(Get(near,"target"),Is.Null);
        Call("Attack",near,bear);Call("ResolveBasicAttackImpact",near,bear,100f);
        Assert.That(Get(near,"basicAttackCount"),Is.EqualTo(0));
        Assert.That(Get(bear,"hp"),Is.EqualTo(5000f));
    }

    [Test]
    public void OldRankFourHealingAndLethalSaveAreGone()
    {
        var state=Call("FamilyTranscendenceStateFor",bear,true);
        state.GetType().GetField("timer").SetValue(state,6f);
        Set(bear,"hp",100f);Call("HandleFamilyTranscendenceBasicAttack",bear,near,100f);
        Assert.That(Get(bear,"hp"),Is.EqualTo(100f));
        Assert.That(Call("ApplyDamage",near,bear,150f,false,false,false,false,0f,0f,1f,false,false,false),Is.EqualTo(100f));
        Assert.That(Get(bear,"alive"),Is.False);
        Assert.That(Get(bear,"familyDamageReductionRatio"),Is.EqualTo(0f));
    }

    [Test]
    public void ExpiryAndResetBothRevertTheAvatar()
    {
        var state=Call("FamilyTranscendenceStateFor",bear,true);
        state.GetType().GetField("timer").SetValue(state,6f);
        Set(bear,"bearAvatarRoarPending",true);
        Call("UpdateFamilyTranscendence",bear,6f);
        Assert.That(Call("BearAvatarActive",bear),Is.False);
        Assert.That(Get(bear,"bearAvatarRoarPending"),Is.False);
        state.GetType().GetField("timer").SetValue(state,6f);
        Call("ResetFamilyTranscendenceStates");
        Assert.That(Call("BearAvatarActive",bear),Is.False);
    }

    [TestCase("Enemies/PixelMonsters/026_dami")]
    [TestCase("Enemies/Shiny/dami")]
    public void FullSpiritKeepsAnimationHitTimingAndRestoresBodyScale(string path)
    {
        var body=Object.Instantiate(Resources.Load<GameObject>(path));
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var renderer=driver.GetComponent<SpriteRenderer>();
        var driverType=typeof(SpriteMonsterAnimationDriver);
        Action<float> step=dt=>
        {
            driverType.GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
            driverType.GetMethod("LateUpdate",Private).Invoke(driver,null);
        };
        driver.SetMoving(false);step(0f);
        string original=renderer.sprite.name;Vector3 scale=driver.transform.localScale;
        driver.SetBearAvatar(true);step(0f);
        Assert.That(renderer.sprite.name,Does.StartWith("OriginalBear_Idle"));
        Assert.That(renderer.enabled,Is.True);
        Assert.That(renderer.color.a,Is.EqualTo(1f));
        int hits=0;driver.PlayBearFightingSpirit(1f,()=>hits++);
        for(int i=0;i<60;i++)step(1f/60f);
        Assert.That(hits,Is.EqualTo(1));
        driver.InterruptCurrentAction();driver.SetMoving(true);step(.1f);
        Assert.That(renderer.sprite.name,Does.StartWith("OriginalBear_Run"));
        driver.SetFrozen(true);driver.SetBearAvatar(false);step(0f);
        Assert.That(renderer.sprite.name,Does.Not.StartWith("OriginalBear"));
        driver.SetFrozen(false);driver.SetMoving(false);step(0f);
        Assert.That(renderer.sprite.name,Is.EqualTo(original));
        Assert.That(driver.transform.localScale,Is.EqualTo(scale));
    }

    [Test]
    public void CancelledSecondRoarCannotFireAfterPreparation()
    {
        var state=Call("FamilyTranscendenceStateFor",bear,true);
        state.GetType().GetField("timer").SetValue(state,6f);
        int generation=(int)type.GetField("combatGeneration",Private).GetValue(battle);
        var prepare=(IEnumerator)Call("PrepareBearAvatarRoar",bear,generation,0);
        Set(bear,"bearAvatarRoarPending",false);
        Assert.That(prepare.MoveNext(),Is.False);
        Assert.That(Status(near).Has(CombatStatusKind.Fear),Is.False);
    }

    [TestCase(0)] [TestCase(1)]
    public void MountainExpiresAtThreeSecondsWithoutMultiplyingOrShorteningTheSixSecondAvatar(int team)
    {
        var body=(GameObject)Get(bear,"model");body.transform.localScale=Vector3.one*2f;
        Set(bear,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(bear,"speedTilesPerSecond",0f);Set(bear,"cooldown",100f);Set(bear,"skillTimer",100f);
        ((IList)Get(bear,"activeTraitIds")).Add(BearCombatRules.MountainColossusTraitId);
        type.GetField("playerProfile",Private).SetValue(battle,new PlayerProfileSave());
        int generation=(int)type.GetField("combatGeneration",Private).GetValue(battle);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
            "Coroutine couldn't be started because the the game object 'Avatar fear test' is inactive!");
        Assert.That(Call("ApplyBearFightingSpirit",bear,GameMasterData.Skill(BearCombatRules.SkillId),1f,false,generation),Is.True);
        var state=Call("FamilyTranscendenceStateFor",bear,false);
        Assert.That(state.GetType().GetField("timer").GetValue(state),Is.EqualTo(6f));
        Assert.That(Get(bear,"bearResolveTimer"),Is.EqualTo(6f));
        Assert.That(Get(bear,"bearMountainShieldTimer"),Is.EqualTo(3f));
        Call("UpdateBearMountainScale",bear,.2f);
        Assert.That(body.transform.localScale,Is.EqualTo(Vector3.one*2f));
        Assert.That(Get(bear,"bearMountainShield"),Is.EqualTo(750f));
        Call("UpdateUnit",bear,2.99f);
        Assert.That(Get(bear,"bearMountainColossusActive"),Is.True);
        Assert.That(Get(bear,"bearMountainShield"),Is.EqualTo(750f));
        Call("UpdateUnit",bear,.02f);
        Assert.That(Get(bear,"bearMountainColossusActive"),Is.False);
        Assert.That(Get(bear,"bearMountainShield"),Is.EqualTo(0f));
        Assert.That(Call("BearAvatarActive",bear),Is.True);
        Assert.That((float)Get(bear,"bearResolveTimer"),Is.EqualTo(2.99f).Within(.001f));
        Assert.That(body.transform.localScale,Is.EqualTo(Vector3.one*2f));
        Call("UpdateUnit",bear,2.98f);Assert.That(Call("BearAvatarActive",bear),Is.True);
        Call("UpdateUnit",bear,.02f);
        Assert.That(Call("BearAvatarActive",bear),Is.False);
    }

    [TestCase("025_woongi")]
    [TestCase("026_dami")]
    [TestCase("027_ironkuma")]
    [TestCase("028_berber")]
    public void AvatarSolesStayOnTheOriginalGroundThroughoutHeavyRoar(string prefab)
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+prefab));
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var renderer=driver.GetComponent<SpriteRenderer>();
        var driverType=typeof(SpriteMonsterAnimationDriver);
        Action<float> step=dt=>
        {
            driverType.GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
            driverType.GetMethod("LateUpdate",Private).Invoke(driver,null);
        };
        driver.SetMoving(false);step(0f);
        Vector3 originalFoot=driver.transform.position;
        driver.SetBearAvatar(true);
        int hits=0;float hitTime=-1f,time=0f;
        driver.PlayBearAvatarRoar(()=>
        {
            hits++;hitTime=time;
            driver.HoldSkillAttackHitFrame(BearCombatRules.AvatarRoarImpactHoldDuration);
        });
        for(int i=0;i<120;i++)
        {
            time=(i+1)/60f;step(1f/60f);
            Sprite sprite=renderer.sprite;
            Assert.That(sprite.pivot.y,Is.EqualTo(82f).Within(.001f));
            Vector3 sole=driver.transform.TransformPoint(new Vector3(0f,(82f-sprite.pivot.y)/sprite.pixelsPerUnit,0f));
            Assert.That(sole.y,Is.EqualTo(originalFoot.y).Within(.002f));
        }
        Assert.That(hits,Is.EqualTo(1));
        Assert.That(hitTime,Is.InRange(.8f,1f),"Fear release needs a visibly longer breath than the .25s transformation.");
        Assert.That(driver.IsActionLocked,Is.False);
        driver.SetBearAvatar(false);step(0f);
        Assert.That(driver.transform.position.y,Is.EqualTo(originalFoot.y).Within(.002f));
        Assert.That(driver.BearAvatarRoarCharging,Is.False);
        Assert.That(driver.BearAvatarRoarImpact,Is.False);
    }

    [Test]
    public void GlobalRoarHasDistinctFramesAndReleasesBothPressureLayersToPool()
    {
        var reference=new GameObject("Pressure reference").AddComponent<SpriteRenderer>();
        var opening=PixelCombatEffect.PlayBearKingRoarBurst(Vector3.zero,5f,reference,false);
        var heavy=PixelCombatEffect.PlayBearAvatarRoar(Vector3.zero,8f,reference);
        Assert.That(opening.GetComponent<SpriteRenderer>().sprite.name,Does.StartWith("BearKingOpening"));
        Assert.That(heavy.GetComponent<SpriteRenderer>().sprite.name,Does.StartWith("BearAvatarPressure"));
        var pressure=heavy.GetComponent<PixelBearRoarPressure>();
        typeof(PixelBearRoarPressure).GetField("elapsed",Private).SetValue(pressure,.2f);
        typeof(PixelBearRoarPressure).GetMethod("Sync",Private).Invoke(pressure,null);
        var clouds=(SpriteRenderer[])typeof(PixelBearRoarPressure).GetField("clouds",Private).GetValue(pressure);
        Assert.That(clouds.Length,Is.EqualTo(32));
        foreach(var cloud in clouds)Assert.That(cloud.enabled,Is.True);
        heavy.CancelBearRoar();
        foreach(var cloud in clouds)Assert.That(cloud.enabled,Is.False);
        opening.CancelBearRoar();
    }

    [Test]
    public void DepartingSoulKeepsTheLastPoseAndRisesAsPixelsAfterBodyReturns()
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/026_dami"));
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        driver.SetBearAvatar(true);
        var source=driver.GetComponent<SpriteRenderer>();
        var texture=source.sprite.texture;
        var effect=PixelBearAvatarDeparture.Create(source,()=>true);
        var mesh=effect.GetComponent<MeshFilter>().sharedMesh;
        var material=effect.GetComponent<MeshRenderer>().sharedMaterial;
        var before=mesh.vertices;
        Vector3 departure=effect.transform.position,foot=driver.transform.position;
        driver.SetBearAvatar(false);
        Assert.That(driver.transform.position,Is.EqualTo(foot));
        body.transform.position+=Vector3.right*3f;
        Assert.That(effect.transform.position,Is.EqualTo(departure));
        Assert.That(material.GetTexture("_MainTex"),Is.SameAs(texture));
        Assert.That(source.sprite.texture,Is.Not.SameAs(texture));
        typeof(PixelBearAvatarDeparture).GetMethod("Animate",Private).Invoke(effect,new object[]{.65f});
        var after=mesh.vertices;
        Assert.That(after.Length,Is.EqualTo(before.Length));
        Assert.That(after[0].y,Is.GreaterThan(before[0].y));
        Assert.That(Mathf.Abs(after[1].x-after[0].x),Is.LessThan(Mathf.Abs(before[1].x-before[0].x)));
        foreach(Color color in mesh.colors)Assert.That(color.a,Is.LessThan(.72f));
        effect.Cancel();effect.Cancel();
        Assert.That(mesh==null,Is.True);Assert.That(material==null,Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NaturalExpiryReleasesOneSoulAndResetOrDeathCancelsIt(bool death)
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/026_dami"));
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(bear,"spriteAnimation",driver);driver.SetBearAvatar(true);
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        var state=Call("FamilyTranscendenceStateFor",bear,true);
        state.GetType().GetField("timer").SetValue(state,.1f);
        Call("UpdateFamilyTranscendence",bear,.1f);
        var soul=(PixelBearAvatarDeparture)state.GetType().GetField("bearDeparture").GetValue(state);
        Assert.That(soul!=null,Is.True);Assert.That(driver.BearAvatarActive,Is.False);
        Call("UpdateFamilyTranscendence",bear,.1f);
        Assert.That(state.GetType().GetField("bearDeparture").GetValue(state),Is.SameAs(soul));
        if(death)Call("HandleFamilyTranscendenceDeath",near,bear);
        else Call("ResetFamilyTranscendenceStates");
        Assert.That(soul==null,Is.True);
        Assert.That(state.GetType().GetField("bearDeparture").GetValue(state),Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DepartingSoulReleasesItsMeshOnExpiryOrInvalidation(bool expired)
    {
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/026_dami"));
        var driver=body.GetComponentInChildren<SpriteMonsterAnimationDriver>();driver.SetBearAvatar(true);
        var effect=PixelBearAvatarDeparture.Create(driver.GetComponent<SpriteRenderer>(),()=>expired);
        var mesh=effect.GetComponent<MeshFilter>().sharedMesh;
        var material=effect.GetComponent<MeshRenderer>().sharedMaterial;
        if(expired)typeof(PixelBearAvatarDeparture).GetField("elapsed",Private).SetValue(effect,PixelBearAvatarDeparture.Duration);
        typeof(PixelBearAvatarDeparture).GetMethod("Update",Private).Invoke(effect,null);
        Assert.That(effect==null,Is.True);
        Assert.That(mesh==null,Is.True);Assert.That(material==null,Is.True);
    }
}
