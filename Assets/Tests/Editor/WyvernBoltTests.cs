using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void BoltSetup()
    {
        GriffinSetup("vivre");Set(source,"skillId",WyvernCombatRules.SkillId);
        Set(source,"skillCooldownDuration",8.5f);Set(source,"skillTimer",5f);
    }
    private void BoltFire(float damage=100f,bool critical=false)=>
        GriffinCall("BeginBasicAttackImpact",source,target,damage,critical,1f);

    [Test]
    public void WyvernBoltPiercesInContactOrderOnceAndKeepsSecondaryDamageAtSixtyPercent()
    {
        BoltSetup();object first=Unit(1,Vector3.right*2f),third=Unit(1,Vector3.right*7f);
        object ally=Unit(0,Vector3.right),behind=Unit(1,Vector3.left),side=Unit(1,new Vector3(3,0,1));
        Assert.That(Hp(first),Is.EqualTo(5000));BoltFire();float cooldown=(float)Get(source,"skillTimer");
        Assert.That(Hp(first),Is.EqualTo(4900));Assert.That(Hp(target),Is.EqualTo(4940));
        Assert.That(Hp(third),Is.EqualTo(4940));
        foreach(var untouched in new[]{ally,behind,side})Assert.That(Hp(untouched),Is.EqualTo(5000));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(cooldown));
        Assert.That(Get(source,"basicAttackCount"),Is.EqualTo(0)); // Impact does not count extra attacks.
    }

    [Test]
    public void WyvernBoltHitsTheWholeActualAttackReachOnTheSameFrame()
    {
        BoltSetup();float reach=MonsterAutoBattlePrototype.AttackDistanceForRange(3,.3f,.3f);
        Model(target).transform.position=Vector3.right*(reach-.001f);
        object outside=Unit(1,Vector3.right*(reach+1f));BoltFire();
        Assert.That(Hp(target),Is.EqualTo(4900));Assert.That(Hp(outside),Is.EqualTo(5000));
    }

    [Test]
    public void WyvernBoltAnimationHitsAllContactsExactlyAtItsHitFrame()
    {
        BoltSetup();object far=Unit(1,Vector3.right*8f);
        var driver=Model(source).AddComponent<SpriteMonsterAnimationDriver>();
        var frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        driver.ConfigureHitFrames(3,3);Set(source,"spriteAnimation",driver);
        driver.PlayBasicAttack(1f,()=>BoltFire());
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        var late=typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private);
        advance.Invoke(driver,new object[]{driver.BasicAttackHitTime-.005f});late.Invoke(driver,null);
        Assert.That(Hp(target),Is.EqualTo(5000));Assert.That(Hp(far),Is.EqualTo(5000));
        advance.Invoke(driver,new object[]{.006f});late.Invoke(driver,null);
        Assert.That(Hp(target),Is.EqualTo(4900));Assert.That(Hp(far),Is.EqualTo(4940));
        advance.Invoke(driver,new object[]{.1f});late.Invoke(driver,null);
        Assert.That(Hp(target),Is.EqualTo(4900));Assert.That(Hp(far),Is.EqualTo(4940));
    }

    [Test]
    public void WyvernBoltHasNoDelayedDamageAfterTheAttackEvent()
    {
        BoltSetup();object later=Unit(1,new Vector3(2,0,4));BoltFire();
        Assert.That(Hp(target),Is.EqualTo(4900));Model(later).transform.position=Vector3.right*2f;
        Set(source,"alive",false);BoltFire();Assert.That(Hp(later),Is.EqualTo(5000));
        Assert.That(Hp(target),Is.EqualTo(4900));
    }

    [Test]
    public void WyvernBoltCriticalDamageAndBasicDefenseApplyToEveryContactWithoutBreathStatuses()
    {
        BoltSetup();Trait(WyvernCombatRules.VivreTraitId);object second=Unit(1,Vector3.right*7f);
        Set(source,"lastBasicAttackCritical",false);BoltFire(150f,true);
        Assert.That(Hp(target),Is.EqualTo(4850));Assert.That(Hp(second),Is.EqualTo(4910));
        foreach(var enemy in new[]{target,second})Assert.That(((CombatStatusState)Get(enemy,"combatStatuses")).Has(CombatStatusKind.Poison),Is.False);
        Assert.That(Get(source,"lastBasicAttackCritical"),Is.EqualTo(false));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WyvernBoltEnemyWindStopsAllDamageAndFriendlyWindAllowsPiercing(bool friendly)
    {
        BoltSetup();object griffin=Unit(friendly?0:1,new Vector3(0,0,5));
        Set(griffin,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_pone"));
        Set(griffin,"augmentTranscended",true);GriffinCall("BeginGriffinCrosswind",griffin);
        BoltFire();Assert.That(Hp(target),Is.EqualTo(friendly?4900:5000));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WyvernBoltHairlineSpansTheRangeThenExpandsEverywhereOnTheFireFrame(bool diagonal)
    {
        var renderer=Model(source).AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
        Vector3 direction=diagonal?new Vector3(1,0,1).normalized:Vector3.right;
        var line=PixelWyvernBolt.Charge(renderer,1,()=>Vector3.zero,()=>direction,9f,()=>.8f,()=>true);
        typeof(PixelWyvernBolt).GetMethod("PoseCharge",Private).Invoke(line,null);
        var ps=line.GetComponentInChildren<ParticleSystem>();
        var particles=new ParticleSystem.Particle[192];int count=ps.GetParticles(particles);
        float thinWidth=particles[0].startSize3D.y;
        Vector3 lineEnd=particles[79].position;
        Assert.That(particles[79].startColor.a,Is.GreaterThan(0));
        Assert.That(Vector3.Dot(lineEnd,direction),Is.GreaterThan(8.8f));
        line.Cancel();
        var fx=PixelWyvernBolt.Fire(renderer,Vector3.zero,direction,9f,1,()=>0f,()=>true);
        ps=fx.GetComponentInChildren<ParticleSystem>();count=ps.GetParticles(particles);
        Assert.That(count,Is.EqualTo(192));
        Assert.That(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported,Is.True);
        Assert.That(particles[159].position,Is.EqualTo(lineEnd));
        for(int i=80;i<160;i++)
        {
            Assert.That(particles[i].startColor.a,Is.GreaterThan(0));
            Assert.That(particles[i].startSize3D.y,Is.GreaterThan(thinWidth*5f));
        }
        float width=particles[80].startSize3D.y;fx.Pose(.15f);ps.GetParticles(particles);
        Assert.That(particles[80].startSize3D.y,Is.LessThan(width*.4f));
        fx.Pose(.19f);Assert.That(fx==null,Is.True);
    }
}
