using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilitySkillFeedbackTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private ReadabilityBasicFeedbackTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object attacker,victim;
    private SpriteMonsterAnimationDriver driver;
    private VoxelEnemyReaction reaction;
    private GameObject root;
    private Camera camera;
    private Dictionary<PixelCombatEffect,bool> effects;
    private bool reduced;

    [SetUp] public void Setup()
    {
        effects=Resources.FindObjectsOfTypeAll<PixelCombatEffect>().ToDictionary(x=>x,x=>x.gameObject.activeSelf);
        reduced=CombatFlashPresentation.Reduced;CombatFlashPresentation.Apply(false);
        fixture=new ReadabilityBasicFeedbackTests();fixture.Setup();Link();
    }
    [TearDown] public void Cleanup()
    {
        try
        {
            foreach(var effect in OwnContacts())effect.ReleaseAfterBattleEndFade();
            fixture?.Cleanup();
        }
        finally{CombatFlashPresentation.Apply(reduced);}
    }

    [TestCase(false)] [TestCase(true)]
    public void ActualSkillContactUsesTheMiddleFeedbackTierAndKeepsDamageGeometryAndCameraUnchanged(bool strong)
    {
        var renderer=driver.GetComponent<SpriteRenderer>();
        var basic=PixelCombatEffect.PlayContactImpact(root.transform,renderer,Vector3.right,strong);
        Assert.That(basic,Is.Not.Null);float basicSize=basic.transform.localScale.x;basic.ReleaseAfterBattleEndFade();
        Vector3 position=root.transform.position,scale=root.transform.localScale,cameraPosition=camera.transform.position;
        Quaternion cameraRotation=camera.transform.rotation;float fov=camera.fieldOfView;
        float reach=(float)Call(battle,"AttackDistance",attacker,victim),radius=Get<float>(victim,"collisionRadius");
        float spacing=Get<float>(victim,"combatBodyScale"),interval=Get<float>(attacker,"attackInterval");
        Set(attacker,"lastSkillCritical",strong);float hp=Get<float>(victim,"hp"),applied=Damage(false,true,true);
        Assert.That(applied,Is.GreaterThan(0f));Assert.That(hp-Get<float>(victim,"hp"),Is.EqualTo(applied).Within(.001f));SkillProfile(strong);
        var effect=OwnContacts().Single();Assert.That(effect.transform.localScale.x,Is.EqualTo(basicSize*1.1f/.75f).Within(.0001f));
        Assert.That(effect.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(strong?.94f:.84f).Within(.0001f));
        Assert.That(Get<Sprite[]>(effect,"frames").Length,Is.EqualTo(4));Assert.That(Get<float>(effect,"framesPerSecond"),Is.EqualTo(40f));
        Assert.That(root.transform.position,Is.EqualTo(position));Assert.That(root.transform.localScale,Is.EqualTo(scale));
        Assert.That(Call(battle,"AttackDistance",attacker,victim),Is.EqualTo(reach));Assert.That(Get<float>(victim,"collisionRadius"),Is.EqualTo(radius));
        Assert.That(Get<float>(victim,"combatBodyScale"),Is.EqualTo(spacing));Assert.That(Get<float>(attacker,"attackInterval"),Is.EqualTo(interval));
        Assert.That(camera.transform.position,Is.EqualTo(cameraPosition));Assert.That(camera.transform.rotation,Is.EqualTo(cameraRotation));
        Assert.That(camera.fieldOfView,Is.EqualTo(fov));
    }

    [Test] public void SkillCanUpgradeABasicCooldownOnceAndFollowingBasicLegacyOrSkillHitsCannotEraseIt()
    {
        reaction.PlayBasicHitFeedback(Vector3.right,false);Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(.52f));
        reaction.PlaySkillHitFeedback(Vector3.left,false);SkillProfile(false);Vector3 held=Get<Vector3>(driver,"contactShakeOffset");
        Assert.That(Get<int>(reaction,"contactPriority"),Is.EqualTo(2));Assert.That(Get<int>(driver,"contactReactionPriority"),Is.EqualTo(2));
        int count=OwnContacts().Length;
        reaction.PlayBasicHitFeedback(Vector3.right,true);reaction.PlayHit(Vector3.right,true,true);reaction.PlaySkillHitFeedback(Vector3.up,true);
        driver.PlayBasicContactReaction(Vector3.right,true);driver.PlayContactReaction(Vector3.right,true);driver.PlaySkillContactReaction(Vector3.up,true);
        Assert.That(Get<Vector3>(driver,"contactShakeOffset"),Is.EqualTo(held));SkillProfile(false);Assert.That(OwnContacts().Length,Is.EqualTo(count));
        Set(reaction,"contactAccentCooldown",0f);Call(reaction,"UpdateFlash",.2f);Unpin();Advance(.13f);
        Assert.That(Get<int>(driver,"contactReactionPriority"),Is.Zero);
        reaction.PlaySkillHitFeedback(Vector3.left,true);SkillProfile(true);
    }

    [Test] public void PeriodicDamageAddsNoContactEffectOrBodyReactionAndCannotReplaceAnActiveSkillAccent()
    {
        int count=OwnContacts().Length;Assert.That(Damage(false,true,false),Is.GreaterThan(0f));
        Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(.2f*.55f).Within(.0001f));
        Assert.That(Get<float>(reaction,"activeFlashDuration"),Is.EqualTo(.05f));Assert.That(OwnContacts().Length,Is.EqualTo(count));
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);
        Call(reaction,"UpdateFlash",.2f);reaction.PlaySkillHitFeedback(Vector3.left,false);
        count=OwnContacts().Length;Vector3 held=Get<Vector3>(driver,"contactShakeOffset");
        Assert.That(Damage(false,true,false),Is.GreaterThan(0f));SkillProfile(false);
        Assert.That(OwnContacts().Length,Is.EqualTo(count));Assert.That(Get<Vector3>(driver,"contactShakeOffset"),Is.EqualTo(held));
    }

    [Test] public void ActualGroundDamageKeepsItsSmallerProfileAndRestoresContextBeforeTheNextDirectSkill()
    {
        Assert.That((float)Call(battle,"ApplyGroundSkillDamage",attacker,victim,100f,false),Is.GreaterThan(0f));
        Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(.55f*.55f).Within(.0001f));
        Assert.That(Get<float>(reaction,"activeFlashDuration"),Is.EqualTo(.09f));
        Assert.That(WorldOffset(),Is.EqualTo(.055f).Within(.0001f));Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);
        Assert.That(Get<bool>(battle,"readabilityGroundImpact"),Is.False);
        FreshTarget();
        var direct=battle.GetType().GetMethods(Fields).Single(method=>method.Name=="ApplySkillDamage"&&
            method.GetParameters().Length==4&&method.GetParameters()[2].ParameterType==typeof(float));
        Assert.That((float)direct.Invoke(battle,new object[]{attacker,victim,100f,false}),Is.GreaterThan(0f));
        SkillProfile(false);Assert.That(Get<bool>(battle,"readabilityGroundImpact"),Is.False);
    }

    [Test] public void CurrentCompactAndOptedOutScenesKeepTheOriginalSkillContactProfile()
    {
        var modes=new[]{CombatReadabilityPreset.Current,CombatReadabilityPreset.Compact,
            CombatReadabilityPreset.Local,CombatReadabilityPreset.Local};
        for(int i=0;i<modes.Length;i++)
        {
            CombatReadabilityPreview.Preset=modes[i];CombatReadabilityPreview.Enabled=i!=2;CombatEffectTestConfiguration.Active=i!=3;
            FreshTarget();Assert.That(Damage(false,true,true),Is.GreaterThan(0f));float intensity=i==1?.55f:1f;
            Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(.55f*intensity).Within(.0001f));
            Assert.That(Get<float>(reaction,"activeFlashDuration"),Is.EqualTo(.09f));
            Assert.That(WorldOffset(),Is.EqualTo(.1f*intensity).Within(.0001f));Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void SkillVisualHoldNeverDelaysTheVictimsPendingBasicOrSkillDamage(bool skill)
    {
        int hits=0;if(skill)driver.PlaySkillAttack(1f,()=>hits++);else driver.PlayBasicAttack(1f,()=>hits++);
        float time=skill?driver.SkillAttackHitTime:driver.BasicAttackHitTime;Advance(time-.01f);
        reaction.PlaySkillHitFeedback(Vector3.right,false);Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.EqualTo(.03f));
        Assert.That(hits,Is.Zero);Unpin();Advance(.02f);Assert.That(hits,Is.EqualTo(1));
        Assert.That(skill?driver.HasPendingSkillAttackHit:driver.HasPendingBasicAttackHit,Is.False);
        Advance(.2f);Assert.That(hits,Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void SkillPreparationKeepsTheFlashButLeavesNoQueuedContactRecoilOrVisualHold(bool hitBeforePreparation)
    {
        int hits=0;driver.PlaySkillAttack(1f,()=>hits++);
        if(hitBeforePreparation)reaction.PlaySkillHitFeedback(Vector3.right,false);
        driver.SetSkillAnticipating(true);if(!hitBeforePreparation)reaction.PlaySkillHitFeedback(Vector3.right,false);
        Assert.That(Get<float>(reaction,"flashTimer"),Is.GreaterThan(0f));Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);
        Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);Assert.That(Get<int>(driver,"contactReactionPriority"),Is.Zero);
        Assert.That(driver.HasPendingSkillAttackHit,Is.True);Advance(1f);Assert.That(hits,Is.Zero);
        driver.SetSkillAnticipating(false);Advance(driver.SkillAttackHitTime+.01f);Assert.That(hits,Is.EqualTo(1));
        Assert.That(Get<Vector3>(driver,"appliedRecoilOffset"),Is.EqualTo(Vector3.zero));
    }

    [Test] public void InterruptedActionsAndReuseDoNotLeaveTheVictimInAContactPose()
    {
        Vector3 original=driver.transform.localPosition;int hits=0;driver.PlayBasicAttack(1f,()=>hits++);
        reaction.PlaySkillHitFeedback(Vector3.right,true);Call(driver,"LateUpdate");driver.InterruptCurrentAction();
        Unpin();Advance(.2f);Call(reaction,"UpdateFlash",.2f);
        Assert.That(hits,Is.Zero);Assert.That(driver.HasPendingBasicAttackHit,Is.False);
        Assert.That(Vector3.Distance(driver.transform.localPosition,original),Is.LessThan(.0001f));
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);Assert.That(Get<int>(driver,"contactReactionPriority"),Is.Zero);
        Set(reaction,"contactAccentCooldown",0f);reaction.PlaySkillHitFeedback(Vector3.left,false);driver.ResetForReuse();Call(driver,"LateUpdate");
        Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);Assert.That(Get<int>(driver,"contactReactionPriority"),Is.Zero);
        Assert.That(Vector3.Distance(driver.transform.localPosition,original),Is.LessThan(.0001f));
    }

    [Test] public void FreezePreservesPendingDamageAndDoesNotReplayOldContactRecoilAfterThawing()
    {
        int hits=0;driver.PlayBasicAttack(1f,()=>hits++);float hit=driver.BasicAttackHitTime;Advance(hit-.02f);
        reaction.PlaySkillHitFeedback(Vector3.right,false);driver.SetFrozen(true);Vector3 position=driver.transform.localPosition;
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);
        Advance(.4f);Assert.That(hits,Is.Zero);Assert.That(driver.HasPendingBasicAttackHit,Is.True);
        Assert.That(driver.transform.localPosition,Is.EqualTo(position));driver.SetFrozen(false);Advance(.03f);Assert.That(hits,Is.EqualTo(1));
        Unpin();Advance(.2f);Assert.That(Get<Vector3>(driver,"appliedRecoilOffset"),Is.EqualTo(Vector3.zero));
        Assert.That(Get<int>(driver,"contactReactionPriority"),Is.Zero);
    }

    [Test] public void ContactToggleSuppressesSkillPresentationWhileTheActualDamageStillApplies()
    {
        CombatReadabilityPreview.ContactFeedback=false;int count=OwnContacts().Length;float hp=Get<float>(victim,"hp");
        Assert.That(Damage(false,true,true),Is.GreaterThan(0f));Assert.That(Get<float>(victim,"hp"),Is.LessThan(hp));
        Assert.That(Get<float>(reaction,"flashTimer"),Is.Zero);Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);
        Assert.That(OwnContacts().Length,Is.EqualTo(count));
    }

    [Test] public void ATranscendedMonstersOrdinaryAttackRemainsAtTheBasicTier()
    {
        Set(attacker,"augmentTranscended",true);Assert.That(Damage(true,false,false),Is.GreaterThan(0f));
        Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(.52f));Assert.That(Get<float>(reaction,"activeFlashDuration"),Is.EqualTo(.06f));
        Assert.That(WorldOffset(),Is.EqualTo(.13f).Within(.0001f));Assert.That(Get<int>(reaction,"contactPriority"),Is.EqualTo(1));
        Assert.That(Get<int>(driver,"contactReactionPriority"),Is.EqualTo(1));
    }

    [Test] public void TheNewSkillPresentationDoesNotAlterActualDamageOrTheGameplayRandomStream()
    {
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;FreshTarget();UnityEngine.Random.InitState(6248);
        float original=Damage(false,true,true);string state=JsonUtility.ToJson(UnityEngine.Random.state);
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;FreshTarget();UnityEngine.Random.InitState(6248);
        Assert.That(Damage(false,true,true),Is.EqualTo(original).Within(.001f));
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(state));
    }

    private void SkillProfile(bool strong)
    {
        Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(strong?.80f:.68f).Within(.0001f));
        Assert.That(Get<float>(reaction,"activeFlashDuration"),Is.EqualTo(strong?.10f:.08f));
        Assert.That(Get<float>(reaction,"contactAccentCooldown"),Is.EqualTo(.12f));
        Assert.That(Get<float>(driver,"impactReactionCooldown"),Is.EqualTo(.12f));
        Assert.That(WorldOffset(),Is.EqualTo(strong?.22f:.18f).Within(.0001f));
        Assert.That(Get<float>(driver,"contactCompressionAmount"),Is.EqualTo(strong?.075f:.055f));
        Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.EqualTo(strong?.035f:.03f));
    }
    private void Link()
    {
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");attacker=Get<object>(fixture,"attacker");victim=Get<object>(fixture,"victim");
        driver=Get<SpriteMonsterAnimationDriver>(fixture,"driver");reaction=Get<VoxelEnemyReaction>(fixture,"reaction");
        root=Get<GameObject>(fixture,"targetRoot");camera=Get<Camera>(fixture,"camera");
    }
    private void FreshTarget(){Call(fixture,"Target");Link();}
    private float Damage(bool basic,bool skill,bool contact)=>(float)Call(fixture,"Damage",basic,skill,contact);
    private float WorldOffset()
    {
        Vector3 local=Get<Vector3>(driver,"contactShakeOffset");return driver.transform.parent==null?local.magnitude:driver.transform.parent.TransformVector(local).magnitude;
    }
    private PixelCombatEffect[] OwnContacts()=>Object.FindObjectsOfType<PixelCombatEffect>()
        .Where(x=>x.name=="Contact impact"&&(!effects.TryGetValue(x,out bool wasActive)||!wasActive)).ToArray();
    private void Unpin()=>Set(driver,"impactReactionStartedFrame",Time.frameCount-1);
    private void Advance(float seconds)
    {Call(driver,"ClearAppliedRecoil");Call(driver,"Advance",seconds);Call(driver,"LateUpdate");}
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Fields).GetValue(value);
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Fields).SetValue(value,data);
    private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Fields).Invoke(value,args);
}
