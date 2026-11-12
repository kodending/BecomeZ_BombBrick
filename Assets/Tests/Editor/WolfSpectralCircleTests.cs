using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class WolfSpectralCircleTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private readonly Type type=typeof(MonsterAutoBattlePrototype);
    private Type unitType;
    private MonsterAutoBattlePrototype battle;
    private HashSet<GameObject> existing;
    private object source,target;
    private IEnumerator cast;
    private readonly Vector3 landing=Vector3.right*4.2f;

    [SetUp] public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Pack rush test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(new Vector3(6,0,0),new Vector3(20,2,16)));
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        source=Unit(0,Vector3.zero);target=Unit(1,Vector3.right*3f);
        Set(source,"wolfHuntDirection",Vector3.right);
        ((IList)Get(source,"activeTraitIds")).Add(WolfCombatRules.NukguTraitId);
    }

    [TearDown] public void TearDown()
    {
        (cast as IDisposable)?.Dispose();
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)
                Object.DestroyImmediate(go);
    }

    private object Unit(int team,Vector3 position)
    {
        object u=Activator.CreateInstance(unitType,true);
        var units=(IList)type.GetField("units",Private).GetValue(battle);
        var model=new GameObject("Rush unit");model.transform.position=position;
        Set(u,"id",units.Count);Set(u,"stableId","rush-"+units.Count);
        Set(u,"model",model);Set(u,"ring",new GameObject("Rush ring"));
        Set(u,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(u,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_nukgu"));
        Set(u,"skillId",WolfCombatRules.SkillId);Set(u,"alive",true);
        Set(u,"hp",5000f);Set(u,"maxHp",5000f);Set(u,"damage",100f);
        Set(u,"attackInterval",1f);Set(u,"collisionRadius",.3f);
        units.Add(u);return u;
    }
    private void Set(object u,string field,object value)=>unitType.GetField(field).SetValue(u,value);
    private object Get(object u,string field)=>unitType.GetField(field).GetValue(u);
    private float Hp(object u)=>(float)Get(u,"hp");
    private CombatStatusState Status(object u)=>(CombatStatusState)Get(u,"combatStatuses");

    [Test]
    public void EclipseFollowupHitsTwiceWithoutSplashAndAddsFrostOnce()
    {
        var nearby=Unit(1,new Vector3(3,0,.2f));
        ((IList)Get(source,"activeTraitIds")).Add(WolfCombatRules.SeollangTraitId);
        type.GetMethod("StartWolfEclipsePack",Private).Invoke(battle,new[]{source});
        Assert.That(Hp(target),Is.EqualTo(5000f));
        cast=(IEnumerator)type.GetMethod("ResolveWolfEclipseFollowup",Private).Invoke(battle,
            new[]{source,target,type.GetField("combatGeneration",Private).GetValue(battle)});
        Assert.That(cast.MoveNext(),Is.True);Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(cast.MoveNext(),Is.True);Assert.That(Hp(target),Is.EqualTo(4945f));
        Assert.That(cast.MoveNext(),Is.False);Assert.That(Hp(target),Is.EqualTo(4890f));
        Assert.That(Hp(nearby),Is.EqualTo(5000f));
        Assert.That(Status(target).Get(CombatStatusKind.ComboChilled).stacks,Is.EqualTo(1));
        typeof(MonsterAutoBattlePrototype).GetMethod("FinishWolfEclipsePack",
            BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{source});
        Assert.That(Hp(target),Is.EqualTo(4890f));Assert.That(Hp(nearby),Is.EqualTo(5000f));
    }

    [TestCase("expired")]
    [TestCase("owner_dead")]
    [TestCase("target_dead")]
    [TestCase("recast")]
    public void EclipseDelayedAttackCancelsWhenItsCastOrVictimIsNoLongerValid(string reason)
    {
        var start=type.GetMethod("StartWolfEclipsePack",Private);
        start.Invoke(battle,new[]{source});
        cast=(IEnumerator)type.GetMethod("ResolveWolfEclipseFollowup",Private).Invoke(battle,
            new[]{source,target,type.GetField("combatGeneration",Private).GetValue(battle)});
        Assert.That(cast.MoveNext(),Is.True);
        if(reason=="expired")Set(source,"wolfEclipseTimer",0f);
        if(reason=="owner_dead")Set(source,"alive",false);
        if(reason=="target_dead")Set(target,"alive",false);
        if(reason=="recast")start.Invoke(battle,new[]{source});
        Assert.That(cast.MoveNext(),Is.False);Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EclipseRecastLetsThePreviousPairFinishWhileTheNewPairStarts(bool alreadyFinishing)
    {
        type.GetField("mlEpisodeActive",Private).SetValue(battle,false);
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/029_nukgu"));
        Set(source,"spriteAnimation",body.GetComponentInChildren<SpriteMonsterAnimationDriver>());
        Set(source,"target",target);
        var start=type.GetMethod("StartWolfEclipsePack",Private);
        var finish=type.GetMethod("FinishWolfEclipsePack",BindingFlags.Static|BindingFlags.NonPublic);
        var animate=typeof(PixelWolfHuntAfterimage).GetMethod("AnimateCompanion",Private);
        var endAge=typeof(PixelWolfHuntAfterimage).GetField("companionEndAge",Private);
        start.Invoke(battle,new[]{source});
        var left=(PixelWolfHuntAfterimage)Get(source,"wolfEclipseLeft");
        var right=(PixelWolfHuntAfterimage)Get(source,"wolfEclipseRight");
        // The battle host stays inactive in this fixture; step its visuals explicitly.
        foreach(var ghost in new[]{left,right})
            typeof(PixelWolfHuntAfterimage).GetField("huntValid",Private)
                .SetValue(ghost,(Func<bool>)(()=>true));
        Set(source,"wolfEclipseTimer",2f);
        if(alreadyFinishing)
        {
            finish.Invoke(null,new[]{source});
            foreach(var ghost in new[]{left,right})animate.Invoke(ghost,new object[]{.35f});
        }
        int revision=(int)Get(source,"wolfEclipseRevision");
        start.Invoke(battle,new[]{source});
        Assert.That(left!=null&&right!=null,Is.True);
        Assert.That(Get(source,"wolfEclipseLeft"),Is.Not.SameAs(left));
        Assert.That(Get(source,"wolfEclipseRight"),Is.Not.SameAs(right));
        Assert.That(Get(source,"wolfEclipseTimer"),Is.EqualTo(WolfCombatRules.EclipseDuration));
        Assert.That(Get(source,"wolfEclipseRevision"),Is.EqualTo(revision+1));
        foreach(var ghost in new[]{left,right})
        {
            Assert.That((float)endAge.GetValue(ghost),Is.EqualTo(alreadyFinishing?.35f:0f));
            animate.Invoke(ghost,new object[]{1.11f});
            Assert.That(ghost==null,Is.True);
        }
        int bursts=0;
        foreach(var ps in Object.FindObjectsOfType<ParticleSystem>())
            if(ps.name=="Wolf Eclipse Dissolve Burst")bursts++;
        Assert.That(bursts,Is.EqualTo(2));
        Assert.That((PixelWolfHuntAfterimage)Get(source,"wolfEclipseLeft")!=null,Is.True);
        Assert.That((PixelWolfHuntAfterimage)Get(source,"wolfEclipseRight")!=null,Is.True);
        Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    private void StartCast(bool frost=false,bool crimson=false,bool moon=false)
    {
        cast=(IEnumerator)type.GetMethod("ResolveWolfCelestialSpectralPack",Private).Invoke(battle,new[]{
            source,target,GameMasterData.Skill(WolfCombatRules.SkillId),1f,frost,crimson,moon,0,
            type.GetField("combatGeneration",Private).GetValue(battle)});
    }
    private IEnumerator Finish()
    {
        int frames=0;
        while(cast.MoveNext()){Assert.That(++frames,Is.LessThan(300));yield return null;}
    }

    [UnityTest] public IEnumerator HongaSpiritKillRefundsHalfTheRemainingCooldownOnce()
    {
        Set(target,"hp",20f);Set(source,"skillTimer",8f);StartCast(false,true);
        yield return Finish();
        Assert.That(Hp(target),Is.LessThanOrEqualTo(0f));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(4f));
    }

    [UnityTest] public IEnumerator HongaBodyFinisherKillRefundsHalfTheRemainingCooldownOnce()
    {
        Set(target,"hp",300f);Set(target,"maxHp",300f);Set(source,"skillTimer",8f);StartCast(false,true);
        yield return Finish();
        Assert.That(Get(source,"skillHits"),Is.EqualTo(9));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(4f));
    }

    [UnityTest] public IEnumerator HongaSurvivingTargetDoesNotRefundCooldown()
    {
        Set(source,"skillTimer",8f);StartCast(false,true);yield return Finish();
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8f));
    }

    [UnityTest] public IEnumerator DamageArrivesTogetherAtContactWithoutCollateral()
    {
        var nearby=Unit(1,new Vector3(3,0,1));StartCast();
        int frames=0,damageFrames=0;float clock=0f,previous=Hp(target);
        while(cast.MoveNext())
        {
            Assert.That(++frames,Is.LessThan(300));
            if(Hp(target)!=previous)
            {
                damageFrames++;Assert.That(clock+Time.deltaTime,Is.GreaterThanOrEqualTo(.444f));
                Assert.That(Hp(target),Is.EqualTo(4580f).Within(.01f));previous=Hp(target);
            }
            clock+=Time.deltaTime;yield return null;
        }
        Assert.That(damageFrames,Is.EqualTo(1));
        Assert.That(Hp(nearby),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(9));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(420f).Within(.01f));
        Assert.That(Status(source).ControlImmune,Is.False);
        Assert.That(Get(source,"wolfChaseEnhanced"),Is.True);
        Assert.That(Get(source,"wolfChaseTarget"),Is.SameAs(target));
    }

    [UnityTest] public IEnumerator IntrinsicsApplyOnlyTheirApprovedAmounts()
    {
        StartCast(frost:true,crimson:true,moon:true);yield return Finish();
        Assert.That(Hp(target),Is.EqualTo(4560.8f).Within(.02f));
        Assert.That(Status(target).Get(CombatStatusKind.ComboChilled).stacks,Is.EqualTo(2));
        Assert.That(Status(target).Has(CombatStatusKind.Freeze),Is.False);
        Assert.That(Status(source).Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(400f).Within(.01f));
    }

    [UnityTest] public IEnumerator DeadTargetCancelsWithoutDeathLocationSplash()
    {
        var nearby=Unit(1,new Vector3(3,0,1));StartCast();Assert.That(cast.MoveNext(),Is.True);
        Set(target,"alive",false);Set(target,"hp",0f);yield return Finish();
        Assert.That(Hp(nearby),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.EqualTo(0));
        Assert.That(Status(source).ControlImmune,Is.False);
        Assert.That(Get(source,"wolfHuntAfterimages"),Is.Null);
    }

    [UnityTest] public IEnumerator PositionInterruptCancelsAndRestoresPreviousImmunity()
    {
        Status(source).ControlImmune=true;StartCast();Assert.That(cast.MoveNext(),Is.True);
        Set(source,"positionRevision",1);yield return Finish();
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Status(source).ControlImmune,Is.True);
        Assert.That(Get(source,"wolfHuntAfterimages"),Is.Null);
    }

    [UnityTest] public IEnumerator BattleResetCancelsBeforeDamage()
    {
        StartCast();Assert.That(cast.MoveNext(),Is.True);
        type.GetField("combatGeneration",Private).SetValue(battle,1);yield return Finish();
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Status(source).ControlImmune,Is.False);
    }

    [UnityTest] public IEnumerator OnlyTheBodyHitIgnoresThirtyPercentDefense()
    {
        Set(target,"defense",100f);StartCast();yield return Finish();
        Assert.That(Hp(target),Is.EqualTo(5000f-240f*.5f-180f/1.7f).Within(.02f));
    }

    [UnityTest] public IEnumerator LethalContactFinishesWithoutCollateral()
    {
        var nearby=Unit(1,new Vector3(3,0,1));Set(target,"hp",15f);
        StartCast();yield return Finish();
        Assert.That((bool)Get(target,"alive"),Is.False);
        Assert.That(Hp(nearby),Is.EqualTo(5000f));
        Assert.That(Status(source).ControlImmune,Is.False);
        Assert.That(Get(source,"wolfHuntAfterimages"),Is.Null);
    }

    [TestCase("029_nukgu")] [TestCase("030_seollang")]
    [TestCase("031_honga")] [TestCase("032_wolyeong")]
    public void BodyWaitsCrouchedThenJumpsAndRestoresScale(string prefab)
    {
        var model=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+prefab));
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Action<float> step=dt=>
        {
            typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
        };
        step(0f);Vector3 scale=driver.transform.localScale;
        driver.PlayWolfHuntAttack(1f,null,true);step(.42f);
        Assert.That(driver.GetComponent<SpriteRenderer>().sprite,Is.SameAs(driver.GetSkillAttackFrame(0)));
        driver.HoldSkillAttackHitFrame(.38f);step(.1f);
        int impact=driver.SkillAttackHitFrameIndex;
        Assert.That(driver.GetComponent<SpriteRenderer>().sprite,Is.SameAs(driver.GetSkillAttackFrame(impact)));
        float skillScale=(float)typeof(SpriteMonsterAnimationDriver).GetMethod("ScaleFor",Private)
            .Invoke(driver,new object[]{"SkillAttack"});
        Assert.That(driver.transform.localScale.y,Is.EqualTo(skillScale).Within(.001f));
        driver.InterruptCurrentAction(true);step(.1f);
        Assert.That(driver.transform.localScale,Is.EqualTo(scale));
    }
}
