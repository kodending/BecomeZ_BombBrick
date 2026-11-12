using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityBasicFeedbackTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Fields=Hidden|BindingFlags.Public;
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object attacker,victim;
    private SpriteMonsterAnimationDriver driver;
    private VoxelEnemyReaction reaction;
    private GameObject targetRoot;
    private Camera camera;
    private bool enabled,active,contact;
    private CombatReadabilityPreset preset;
    private SpriteRenderer previousRecipient;
    private Vector3 previousDirection;
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()
    {
        enabled=CombatReadabilityPreview.Enabled;active=CombatEffectTestConfiguration.Active;
        contact=CombatReadabilityPreview.ContactFeedback;preset=CombatReadabilityPreview.Preset;
        previousRecipient=CombatReadabilityPreview.ProjectileContactRenderer;
        previousDirection=CombatReadabilityPreview.ProjectileContactDirection;random=UnityEngine.Random.state;
        CombatReadabilityPreview.Enabled=CombatEffectTestConfiguration.Active=CombatReadabilityPreview.ContactFeedback=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        CombatReadabilityPreview.ProjectileContactRenderer=null;CombatReadabilityPreview.ProjectileContactDirection=Vector3.zero;
        fixture=new MonkeyArcTests();fixture.SetUp();battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        attacker=Get<object>(fixture,"source");victim=Get<object>(fixture,"target");Target();
        camera=new GameObject("Basic feedback camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(0f,8f,-12f);camera.transform.LookAt(Vector3.zero);
        Set(battle,"mainCamera",camera);
    }

    [TearDown] public void Cleanup()
    {
        try{fixture?.TearDown();}
        finally
        {
            CombatReadabilityPreview.Enabled=enabled;CombatEffectTestConfiguration.Active=active;
            CombatReadabilityPreview.ContactFeedback=contact;CombatReadabilityPreview.Preset=preset;
            CombatReadabilityPreview.ProjectileContactRenderer=previousRecipient;
            CombatReadabilityPreview.ProjectileContactDirection=previousDirection;UnityEngine.Random.state=random;
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void ActualBasicDamageUsesTheSharperFlashAndBodyResponseWithoutAddingAnotherHit(bool critical)
    {
        Set(attacker,"lastBasicAttackCritical",critical);float before=Get<float>(victim,"hp");
        float damage=Damage(true,false);
        Assert.That(damage,Is.GreaterThan(0f));Assert.That(before-Get<float>(victim,"hp"),Is.EqualTo(damage).Within(.001f));
        Profile(critical);
        Assert.That(Get<float>(driver,"contactCompressionAmount"),Is.EqualTo(critical?.065f:.045f));
        Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.EqualTo(critical?.035f:.025f));
    }

    [Test] public void ActualRangedBasicArrivalStartsTheNewFeedbackOnlyWhenTheProjectileHits()
    {
        Actor("kongjwi");float hp=Get<float>(victim,"hp");
        Call(battle,"BeginBasicAttackImpact",attacker,victim,100f,false,1f);
        var shots=Object.FindObjectsOfType<PixelStarProjectile>();Assert.That(shots.Length,Is.EqualTo(1));
        Assert.That(Get<float>(victim,"hp"),Is.EqualTo(hp));Assert.That(Get<float>(reaction,"flashTimer"),Is.Zero);
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);
        Call(shots[0],"Advance",1f);
        Assert.That(Get<float>(victim,"hp"),Is.LessThan(hp));Profile(false);
    }

    [Test] public void CurrentCompactAndOptedOutScenesRetainTheirOriginalBasicFeedback()
    {
        var modes=new[]{CombatReadabilityPreset.Current,CombatReadabilityPreset.Compact,
            CombatReadabilityPreset.Local,CombatReadabilityPreset.Local};
        for(int i=0;i<modes.Length;i++)
        {
            CombatReadabilityPreview.Preset=modes[i];CombatReadabilityPreview.Enabled=i!=2;
            CombatEffectTestConfiguration.Active=i!=3;Target();
            Assert.That(Damage(true,false),Is.GreaterThan(0f));float strength=i==1?.55f:1f;
            Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(.55f*strength).Within(.0001f));
            Assert.That(Get<float>(reaction,"activeFlashDuration"),Is.EqualTo(.09f));
            Assert.That(Get<float>(reaction,"contactAccentCooldown"),Is.EqualTo(.12f));
            Assert.That(WorldOffset(),Is.EqualTo(.1f*strength).Within(.0001f));
            Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void DirectSkillsStandAboveBasicsWhilePeriodicDamageKeepsItsQuietProfile(bool directSkill)
    {
        Assert.That(Damage(false,true,directSkill),Is.GreaterThan(0f));
        Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(directSkill?.68f:.2f*.55f).Within(.0001f));
        Assert.That(Get<float>(reaction,"activeFlashDuration"),Is.EqualTo(directSkill?.08f:.05f));
        Assert.That(WorldOffset(),Is.EqualTo(directSkill?.18f:0f).Within(.0001f));
        Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.EqualTo(directSkill?.03f:0f));
    }

    [TestCase(false)] [TestCase(true)]
    public void HeldVisualFrameDoesNotDelayPendingBasicOrSkillDamage(bool skill)
    {
        int hits=0;
        if(skill)driver.PlaySkillAttack(1f,()=>hits++);else driver.PlayBasicAttack(1f,()=>hits++);
        float hit=skill?driver.SkillAttackHitTime:driver.BasicAttackHitTime;
        Advance(hit-.01f);reaction.PlayBasicHitFeedback(Vector3.right,false);
        Assert.That(hits,Is.Zero);Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.EqualTo(.025f));
        Set(driver,"impactReactionStartedFrame",Time.frameCount-1);Advance(.02f);
        Assert.That(hits,Is.EqualTo(1));
        Assert.That(skill?driver.HasPendingSkillAttackHit:driver.HasPendingBasicAttackHit,Is.False);
        Advance(.02f);Assert.That(hits,Is.EqualTo(1));
    }

    [Test] public void BasicRecoilAndFlashClearWithoutMovingBodiesChangingReachSpacingOrCamera()
    {
        Vector3 position=targetRoot.transform.position,scale=targetRoot.transform.localScale;
        Vector3 cameraPosition=camera.transform.position;Quaternion cameraRotation=camera.transform.rotation;
        float fov=camera.fieldOfView,reach=(float)Call(battle,"AttackDistance",attacker,victim);
        float radius=Get<float>(victim,"collisionRadius"),bodyScale=Get<float>(victim,"combatBodyScale");
        int range=Get<int>(victim,"attackRange");Vector3 idle=driver.transform.localScale;
        reaction.PlayBasicHitFeedback(Vector3.right,false);Call(driver,"LateUpdate");
        Assert.That(driver.transform.parent.TransformVector(Get<Vector3>(driver,"appliedRecoilOffset")).magnitude,Is.GreaterThan(.1f));
        Assert.That(driver.transform.localScale.y,Is.LessThan(idle.y));
        Set(driver,"impactReactionStartedFrame",Time.frameCount-1);Advance(.2f);Call(reaction,"UpdateFlash",.2f);
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);
        Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);
        Assert.That(Get<Vector3>(driver,"appliedRecoilOffset"),Is.EqualTo(Vector3.zero));
        Assert.That(Vector3.Distance(driver.transform.localScale,idle),Is.LessThan(.0001f));
        Assert.That(Get<float>(reaction,"flashTimer"),Is.Zero);
        Assert.That(targetRoot.transform.position,Is.EqualTo(position));Assert.That(targetRoot.transform.localScale,Is.EqualTo(scale));
        Assert.That(Call(battle,"AttackDistance",attacker,victim),Is.EqualTo(reach));
        Assert.That(Get<float>(victim,"collisionRadius"),Is.EqualTo(radius));Assert.That(Get<float>(victim,"combatBodyScale"),Is.EqualTo(bodyScale));
        Assert.That(Get<int>(victim,"attackRange"),Is.EqualTo(range));Assert.That(camera.transform.position,Is.EqualTo(cameraPosition));
        Assert.That(camera.transform.rotation,Is.EqualTo(cameraRotation));Assert.That(camera.fieldOfView,Is.EqualTo(fov));
    }

    [Test] public void ContactToggleSuppressesTheNewVisualsWhileActualDamageStillApplies()
    {
        CombatReadabilityPreview.ContactFeedback=false;float hp=Get<float>(victim,"hp");
        Assert.That(Damage(true,false),Is.GreaterThan(0f));Assert.That(Get<float>(victim,"hp"),Is.LessThan(hp));
        Assert.That(Get<float>(reaction,"flashTimer"),Is.Zero);
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);
    }

    [Test] public void BasicDamageDuringPreparationFlashesImmediatelyWithoutQueuingBodyRecoil()
    {
        driver.SetSkillAnticipating(true);Assert.That(Damage(true,false),Is.GreaterThan(0f));
        Assert.That(Get<float>(reaction,"flashTimer"),Is.GreaterThan(0f));
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);
        Assert.That(Get<float>(driver,"impactReactionCooldown"),Is.Zero);Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);
        driver.SetSkillAnticipating(false);Advance(.01f);
        Assert.That(Get<Vector3>(driver,"appliedRecoilOffset"),Is.EqualTo(Vector3.zero));
    }

    [Test] public void EnteringPreparationDiscardsOnlyOldBasicFeedbackAndPreservesPendingHitsAndSkillRecoil()
    {
        int hits=0;driver.PlayBasicAttack(1f,()=>hits++);driver.PlayImpactRecoil(Vector3.right,.2f);
        float skillRecoil=Get<float>(driver,"impactRecoilRemaining");reaction.PlayBasicHitFeedback(Vector3.right,false);
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.GreaterThan(0f));driver.SetSkillAnticipating(true);
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.Zero);
        Assert.That(Get<float>(driver,"impactReactionCooldown"),Is.Zero);
        Assert.That(Get<float>(driver,"impactRecoilRemaining"),Is.EqualTo(skillRecoil));
        Assert.That(driver.HasPendingBasicAttackHit,Is.True);Advance(1f);Assert.That(hits,Is.Zero);
        driver.SetSkillAnticipating(false);Advance(driver.BasicAttackHitTime+.02f);Assert.That(hits,Is.EqualTo(1));
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);

        Target();driver.PlayContactReaction(Vector3.right,true);float original=Get<float>(driver,"contactShakeRemaining");
        driver.SetSkillAnticipating(true);Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.EqualTo(original));
        Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.EqualTo(.04f));
    }

    [Test] public void FullyAbsorbedBasicDamageDoesNotProduceTheNewBodyHitFeedback()
    {
        Set(victim,"itemShield",10000f);float hp=Get<float>(victim,"hp");Assert.That(Damage(true,false),Is.Zero);
        Assert.That(Get<float>(victim,"hp"),Is.EqualTo(hp));Assert.That(Get<float>(victim,"preventedDamage"),Is.GreaterThan(0f));
        Assert.That(Get<float>(reaction,"contactAccentCooldown"),Is.Zero);Assert.That(Get<float>(reaction,"flashTimer"),Is.Zero);
        Assert.That(Get<float>(driver,"contactShakeRemaining"),Is.Zero);
    }

    private void Profile(bool strong)
    {
        Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(strong?.68f:.52f).Within(.0001f));
        Assert.That(Get<float>(reaction,"activeFlashDuration"),Is.EqualTo(strong?.075f:.06f));
        Assert.That(Get<float>(reaction,"contactAccentCooldown"),Is.EqualTo(.085f));
        Assert.That(Get<float>(driver,"impactReactionCooldown"),Is.EqualTo(.085f));
        Assert.That(WorldOffset(),Is.EqualTo(strong?.18f:.13f).Within(.0001f));
    }
    private float Damage(bool basic,bool skill,bool skillContact=false)
    {
        return (float)Call(battle,"ResolveDamage",attacker,victim,100f,skill,basic,false,false,
            0f,0f,1f,false,false,false,0f,false,skillContact,false);
    }
    private float WorldOffset()
    {
        Vector3 local=Get<Vector3>(driver,"contactShakeOffset");
        return driver.transform.parent==null?local.magnitude:driver.transform.parent.TransformVector(local).magnitude;
    }
    private void Target()
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos");
        targetRoot=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));targetRoot.transform.position=Vector3.right*4f;
        driver=targetRoot.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        reaction=targetRoot.GetComponent<VoxelEnemyReaction>()??targetRoot.AddComponent<VoxelEnemyReaction>();reaction.Initialize();
        Set(victim,"model",targetRoot);Set(victim,"definition",definition);Set(victim,"spriteAnimation",driver);Set(victim,"reaction",reaction);
        Set(victim,"hp",5000f);Set(victim,"maxHp",5000f);Set(victim,"alive",true);Set(victim,"itemShield",0f);
        Advance(0f);
    }
    private void Actor(string species)
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get(species);
        var root=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
        Set(attacker,"model",root);Set(attacker,"definition",definition);Set(attacker,"spriteAnimation",root.GetComponentInChildren<SpriteMonsterAnimationDriver>());
        Set(attacker,"skillId",definition.skillId);Set(attacker,"attackRange",definition.attackRange);
    }
    private void Advance(float seconds){Call(driver,"Advance",seconds);Call(driver,"LateUpdate");}
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Fields).GetValue(value);
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Fields).SetValue(value,data);
    private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Hidden).Invoke(value,args);
}
