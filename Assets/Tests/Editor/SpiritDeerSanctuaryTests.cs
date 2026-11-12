using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void SpiritGuardSetup()
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Set(source,"maxHp",1000f);Set(source,"hp",1000f);
        cast=(IEnumerator)MammothCall("ResolveInviolableSanctuary",source,Vector3.zero,1f,0);
        Assert.That(cast.MoveNext(),Is.True);
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,.4f);
        cast.MoveNext();
    }

    [Test]
    public void SpiritDeerInviolableHudFollowsLiveMembershipAndReset()
    {
        SpiritGuardSetup();
        var icons=(IList)typeof(MonsterAutoBattlePrototype).GetField("combatHudIcons",Private).GetValue(battle);
        System.Func<bool> hasGuard=()=>icons.Cast<object>().Any(entry=>
            entry.GetType().GetField("effect").GetValue(entry)?.ToString()=="DamageReduction");
        MammothCall("CollectCombatHudIcons",source);Assert.That(hasGuard(),Is.True);
        Model(source).transform.position=Vector3.right*20;
        MammothCall("CollectCombatHudIcons",source);Assert.That(hasGuard(),Is.False);
        Model(source).transform.position=Vector3.zero;
        MammothCall("ClearSpiritGuardSanctuaries");
        MammothCall("CollectCombatHudIcons",source);Assert.That(hasGuard(),Is.False);
        Assert.That(cast.MoveNext(),Is.False);
    }

    [TestCase(false)][TestCase(true)]
    public void SpiritDeerInviolableCapsHealthAfterDefenseAndShields(bool skill)
    {
        SpiritGuardSetup();Set(source,"defense",50f);Set(source,"itemShield",60f);
        Assert.That(SpiritReturnDamage(target,source,1000f,basic:!skill,skill:skill),Is.EqualTo(150f));
        Assert.That(Get(source,"itemShield"),Is.Zero);Assert.That(Hp(source),Is.EqualTo(850f));
        Set(source,"defense",0f);Set(source,"itemShield",300f);
        Assert.That(SpiritReturnDamage(target,source,400f),Is.EqualTo(100f));
        Set(source,"itemShield",500f);
        Assert.That(SpiritReturnDamage(target,source,400f),Is.Zero);
        Assert.That(Get(source,"itemShield"),Is.EqualTo(100f));
    }

    [Test]
    public void SpiritDeerInviolableAllowsSmallRepeatedHitsAndDoesNotPreventDeath()
    {
        SpiritGuardSetup();
        for(int i=0;i<3;i++)Assert.That(SpiritReturnDamage(target,source,80f),Is.EqualTo(80f));
        Assert.That(Hp(source),Is.EqualTo(760f));
        Set(source,"hp",120f);Assert.That(SpiritReturnDamage(target,source,400f),Is.EqualTo(120f));
        Assert.That(Get(source,"alive"),Is.False);
    }

    [Test]
    public void SpiritDeerInviolableChecksLiveBoundaryBloomExpiryAndTeam()
    {
        SpiritGuardSetup();
        Assert.That(MammothCall("IsSpiritSanctuaryProtected",target),Is.False);
        var timer=typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private);
        float edge=MonsterAutoBattlePrototype.TileSize*2.5f+.3f;
        Model(source).transform.position=Vector3.right*(edge-.01f);
        Assert.That(MammothCall("IsSpiritSanctuaryProtected",source),Is.True);
        Model(source).transform.position=Vector3.right*(edge+.01f);
        Assert.That(SpiritReturnDamage(target,source,400f),Is.EqualTo(400f));
        Model(source).transform.position=Vector3.right*3;
        Assert.That(SpiritReturnDamage(target,source,400f),Is.EqualTo(150f));
        timer.SetValue(battle,0f);Assert.That(MammothCall("IsSpiritSanctuaryProtected",source),Is.False);
        timer.SetValue(battle,.4f);Set(source,"augmentShadowClone",true);
        Assert.That(MammothCall("IsSpiritSanctuaryProtected",source),Is.False);Set(source,"augmentShadowClone",false);
        timer.SetValue(battle,5f);Assert.That(MammothCall("IsSpiritSanctuaryProtected",source),Is.False);
    }

    [Test]
    public void SpiritDeerInviolableOverlappingFieldsDoNotStackAndSurviveOtherCasterDeath()
    {
        SpiritGuardSetup();var other=Unit(0,Vector3.zero);
        kingCast=(IEnumerator)MammothCall("ResolveInviolableSanctuary",other,Vector3.zero,1f,0);
        kingCast.MoveNext();Assert.That(SpiritReturnDamage(target,source,400f),Is.EqualTo(150f));
        Set(other,"alive",false);Assert.That(SpiritReturnDamage(target,source,400f),Is.EqualTo(150f));
        ((System.IDisposable)cast).Dispose();Assert.That(MammothCall("IsSpiritSanctuaryProtected",source),Is.True);
        Set(other,"alive",true);Assert.That(MammothCall("IsSpiritSanctuaryProtected",source),Is.True);
        typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        Assert.That(MammothCall("IsSpiritSanctuaryProtected",source),Is.False);
    }

    [Test]
    public void SpiritDeerInviolableKeepsSpeciesBlessingOncePerFieldAndLateEntry()
    {
        SpiritDeerSetup("forestdeer",SpiritDeerCombatRules.ForestdeerTraitId);
        Set(target,"team",Get(source,"team"));Set(target,"hp",1000f);Model(target).transform.position=Vector3.right*9;
        cast=(IEnumerator)MammothCall("ResolveInviolableSanctuary",source,Vector3.zero,1f,0);cast.MoveNext();
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,1f);
        Model(target).transform.position=Vector3.right*2;cast.MoveNext();Assert.That(Hp(target),Is.EqualTo(1060f));
        Assert.That(Get(target,"familyAttackSpeedBonus"),Is.EqualTo(.2f));
        Model(target).transform.position=Vector3.right*9;cast.MoveNext();Model(target).transform.position=Vector3.right*2;cast.MoveNext();
        Assert.That(Hp(target),Is.EqualTo(1060f));Assert.That(MammothCall("IsSpiritSanctuaryProtected",target),Is.True);
    }

    [Test]
    public void SpiritDeerAntlerBarrierTracksImpactSidePausesAndRefreshesOneEffect()
    {
        var renderer=Model(source).AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
        float clock=0;bool alive=true;
        var fx=PixelSpiritBlessing.GuardHit(renderer,Vector3.right,.25f,()=>clock,()=>alive);
        var tick=typeof(PixelSpiritBlessing).GetMethod("LateUpdate",Private);clock=.08f;tick.Invoke(fx,null);
        var ps=fx.GetComponentInChildren<ParticleSystem>();var a=new ParticleSystem.Particle[512];var b=new ParticleSystem.Particle[512];
        int count=ps.GetParticles(a);Assert.That(count,Is.GreaterThan(80).And.LessThan(512));
        tick.Invoke(fx,null);ps.GetParticles(b);Assert.That(b[0].position,Is.EqualTo(a[0].position));
        renderer.transform.position+=Vector3.right;tick.Invoke(fx,null);ps.GetParticles(b);
        Assert.That(b[0].position.x-a[0].position.x,Is.EqualTo(1f).Within(.001f));
        Assert.That(PixelSpiritBlessing.GuardHit(renderer,Vector3.left,.6f,()=>clock,()=>alive),Is.SameAs(fx));
        Assert.That(typeof(PixelSpiritBlessing).GetField("guardDirection",Private).GetValue(fx),Is.EqualTo(Vector3.left));
        clock+=.08f;tick.Invoke(fx,null);Assert.That(renderer.GetComponentsInChildren<PixelSpiritBlessing>().Length,Is.EqualTo(1));
        alive=false;tick.Invoke(fx,null);Assert.That(fx==null,Is.True);
    }

    private float SpiritReturnDamage(object attacker,object victim,float damage,bool basic=true,bool skill=false,bool bypass=false)=>
        (float)MammothCall("ApplyDamage",attacker,victim,damage,skill,basic,false,false,0f,0f,1f,bypass,false,false);

    [TestCase(false,true)][TestCase(false,false)][TestCase(true,true)][TestCase(true,false)]
    public void SpiritDeerLifeReturnUsesOnlyBasicActualHealthDamage(bool basic,bool skill)
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Set(source,"hp",1000f);Set(target,"defense",50f);Set(target,"itemShield",25f);
        MammothCall("GrantSpiritLifeReturn",source,5f,source);
        float applied=SpiritReturnDamage(source,target,100f,basic,skill);
        Assert.That(applied,Is.GreaterThan(0f).And.LessThan(75f));
        Assert.That(Hp(source),Is.EqualTo(1000f+(basic&&!skill?applied*.35f:0f)).Within(.001f));
        Set(target,"itemShield",1000f);float before=Hp(source);
        Assert.That(SpiritReturnDamage(source,target,100f,basic,skill),Is.Zero);
        Assert.That(Hp(source),Is.EqualTo(before));
    }

    [Test]
    public void SpiritDeerLifeReturnCannotHealOverkillAndStopsAfterBuffExpires()
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Set(source,"hp",1000f);Set(target,"hp",10f);MammothCall("GrantSpiritLifeReturn",source,5f,source);
        Assert.That(SpiritReturnDamage(source,target,100f),Is.EqualTo(10f));
        Assert.That(Hp(source),Is.EqualTo(1003.5f));
        Set(source,"spiritLifeReturnTimer",0f);var another=Unit(1,Vector3.right);
        SpiritReturnDamage(source,another,100f);Assert.That(Hp(source),Is.EqualTo(1003.5f));
    }

    [Test]
    public void SpiritDeerLifeReturnOverhealRespectsReductionAndKeepsIndependentCappedShield()
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        MammothCall("GrantSpiritLifeReturn",source,5f,source);Set(source,"hp",4990f);
        Set(source,"familyHealingReductionTimer",5f);Set(source,"familyHealingReductionRatio",.5f);
        Set(source,"itemShield",50f);
        SpiritReturnDamage(source,target,100f);
        Assert.That(Hp(source),Is.EqualTo(5000f));Assert.That(Get(source,"spiritLifeShield"),Is.EqualTo(7.5f));
        Assert.That(Get(source,"itemShield"),Is.EqualTo(50f));
        SpiritReturnDamage(source,target,3000f);
        Assert.That(Get(source,"spiritLifeShield"),Is.EqualTo(400f));
        Assert.That(Get(source,"spiritLifeShieldTimer"),Is.EqualTo(3f));
        Assert.That(SpiritReturnDamage(target,source,100f),Is.Zero);
        Assert.That(Get(source,"spiritLifeShield"),Is.EqualTo(300f));
        Assert.That(Get(source,"itemShield"),Is.EqualTo(50f));
        Assert.That(SpiritReturnDamage(target,source,100f,bypass:true),Is.EqualTo(100f));
        Assert.That(Get(source,"spiritLifeShield"),Is.EqualTo(300f));
    }

    [Test]
    public void SpiritDeerLifeReturnFirstContactReentryAndMultipleFieldsDoNotStack()
    {
        SpiritDeerSetup("forestdeer",SpiritDeerCombatRules.ForestdeerTraitId);
        Set(target,"team",Get(source,"team"));var late=Unit(0,Vector3.right*20);
        var timer=typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private);
        cast=(IEnumerator)MammothCall("ResolveSpiritLifeReturn",source,Vector3.right*4,1f,0);
        cast.MoveNext();Assert.That(Get(target,"spiritLifeReturnTimer"),Is.EqualTo(5f));
        Assert.That(Get(late,"spiritLifeReturnTimer"),Is.Zero);
        Set(target,"spiritLifeReturnTimer",2f);Model(target).transform.position=Vector3.right*20;
        timer.SetValue(battle,3f);cast.MoveNext();Model(target).transform.position=Vector3.right*4;
        Model(late).transform.position=Vector3.right*4;cast.MoveNext();
        Assert.That(Get(target,"spiritLifeReturnTimer"),Is.EqualTo(2f));
        Assert.That(Get(late,"spiritLifeReturnTimer"),Is.EqualTo(5f));
        var second=(IEnumerator)MammothCall("ResolveSpiritLifeReturn",source,Vector3.right*4,1f,0);
        second.MoveNext();Assert.That(Get(target,"spiritLifeReturnTimer"),Is.EqualTo(5f));
        Set(source,"alive",false);Assert.That(cast.MoveNext(),Is.True);Assert.That(second.MoveNext(),Is.True);
        timer.SetValue(battle,10f);Assert.That(cast.MoveNext(),Is.False);Assert.That(second.MoveNext(),Is.False);
        Assert.That(Get(target,"spiritLifeReturnTimer"),Is.EqualTo(5f));
        Set(target,"hp",1000f);var enemy=Unit(1,Vector3.right*6);
        Assert.That(SpiritReturnDamage(target,enemy,100f),Is.EqualTo(100f));
        Assert.That(Hp(target),Is.EqualTo(1035f));
    }

    [Test]
    public void SpiritDeerLifeReturnTimersExpireIndependently()
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        MammothCall("GrantSpiritLifeReturn",source,5f,source);Set(source,"spiritLifeShield",100f);Set(source,"spiritLifeShieldTimer",3f);
        Set(source,"cooldown",100f);Set(source,"skillTimer",100f);
        MammothCall("UpdateUnit",source,3.01f);
        Assert.That(Get(source,"spiritLifeShield"),Is.Zero);
        Assert.That(Get(source,"spiritLifeReturnTimer"),Is.EqualTo(1.99f).Within(.001f));
        MammothCall("UpdateUnit",source,2f);Assert.That(Get(source,"spiritLifeReturnTimer"),Is.Zero);
    }

    [Test]
    public void SpiritDeerReturningLightPausesTracksRecipientAndArrivesOnce()
    {
        var renderer=Model(target).AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
        float time=0;int arrivals=0;
        var fx=PixelSpiritBlessing.ReturnLife(Vector3.zero,renderer,()=>time,()=>renderer!=null,()=>arrivals++);
        var tick=typeof(PixelSpiritBlessing).GetMethod("LateUpdate",Private);time=.15f;tick.Invoke(fx,null);
        var ps=fx.GetComponentInChildren<ParticleSystem>();var a=new ParticleSystem.Particle[96];var b=new ParticleSystem.Particle[96];
        int count=ps.GetParticles(a);tick.Invoke(fx,null);Assert.That(ps.GetParticles(b),Is.EqualTo(count));
        Assert.That(b[0].position,Is.EqualTo(a[0].position));Assert.That(arrivals,Is.Zero);
        renderer.transform.position+=Vector3.right;tick.Invoke(fx,null);ps.GetParticles(b);
        Assert.That(b[0].position.x,Is.GreaterThan(a[0].position.x));
        time=.31f;tick.Invoke(fx,null);tick.Invoke(fx,null);Assert.That(arrivals,Is.EqualTo(1));
        Object.DestroyImmediate(renderer);tick.Invoke(fx,null);Assert.That(fx==null,Is.True);
    }

    [TestCase("persistent")][TestCase("calm")][TestCase("challenger")][TestCase("vengeful")]
    [TestCase("fickle")][TestCase("cooperative")][TestCase("independent")][TestCase("protective")]
    [TestCase("opportunist")][TestCase("impulsive")][TestCase("cautious")][TestCase("bold")]
    [TestCase("timid")][TestCase("eccentric")]
    public void SpiritDeerAlwaysTargetsAnotherInRangeAllyBeforeSelf(string personality)
    {
        Set(source,"personalityPolicy",PersonalityCombatPolicy.FromId(personality));
        Set(source,"hp",1f);Set(source,"damageTaken",9999f);Set(source,"spiritSanctuaryLastAnchor",source);
        var ally=Unit(0,Vector3.right*4.5f);Set(ally,"id",1);
        var clone=Unit(0,Vector3.right);Set(clone,"augmentShadowClone",true);
        var dead=Unit(0,Vector3.right);Set(dead,"alive",false);
        var far=Unit(0,Vector3.right*8);Set(far,"hp",1f);
        for(int castIndex=0;castIndex<12;castIndex++)
        {
            Set(source,"skillUses",castIndex);
            Assert.That(MammothCall("SelectSpiritSanctuaryAnchor",source,4.5f,2f),Is.SameAs(ally));
        }
        Model(ally).transform.position=Vector3.right*4.81f;
        Assert.That(MammothCall("SelectSpiritSanctuaryAnchor",source,4.5f,2f),Is.SameAs(source));
        Assert.That(Get(source,"spiritSanctuaryLastAnchor"),Is.SameAs(source));
        Model(ally).transform.position=Vector3.right*4.5f;Set(ally,"hp",1000f);
        Assert.That(MammothCall("SelectSpiritSanctuaryAnchor",source,4.5f,2f),Is.SameAs(ally));
    }

    [TestCase("calm",false)][TestCase("protective",true)]
    public void SpiritDeerPersonalityChoosesPreferredAllyAboutEightyPercentOfCasts(string personality,bool threatened)
    {
        Set(source,"personalityPolicy",PersonalityCombatPolicy.FromId(personality));
        var preferred=Unit(0,Vector3.right*2);Set(preferred,"id",1);Set(preferred,"hp",threatened?5000f:1000f);
        var other=Unit(0,Vector3.right*4);Set(other,"id",2);Set(other,"hp",threatened?1000f:5000f);
        Set(target,"target",preferred);int chosen=0;
        for(int castIndex=1;castIndex<=1000;castIndex++)
        {
            Set(source,"skillUses",castIndex);
            object result=MammothCall("SelectSpiritSanctuaryAnchor",source,4.5f,2f);
            Assert.That(result==preferred||result==other,Is.True);
            if(result==preferred)chosen++;
        }
        Assert.That(chosen,Is.InRange(750,850));
    }

    private void SpiritDeerSetup(string species,string trait)
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(source,"skillId",SpiritDeerCombatRules.SkillId);Set(source,"maxHp",1000f);Set(source,"hp",1000f);
        Set(target,"maxHp",10000f);Set(target,"hp",6000f);Trait(trait);
    }

    [TestCase("holydeer",SpiritDeerCombatRules.HolydeerTraitId,100f)]
    [TestCase("lunadeer",SpiritDeerCombatRules.LunadeerTraitId,580f)]
    [TestCase("forestdeer",SpiritDeerCombatRules.ForestdeerTraitId,60f)]
    [TestCase("eclipsedeer",SpiritDeerCombatRules.EclipsedeerTraitId,60f)]
    public void SpiritDeerBlessingsUseCasterStatsAndCleanseOnlyWhenApplied(string species,string trait,float healed)
    {
        SpiritDeerSetup(species,trait);var status=(CombatStatusState)Get(target,"combatStatuses");
        status.ApplyTimed(CombatStatusKind.Root,3f,source);
        MammothCall("ApplySpiritDeerSanctuaryBlessing",source,target,1f);
        Assert.That(Hp(target),Is.EqualTo(6000+healed).Within(.01f));Assert.That(status.Has(CombatStatusKind.Root),Is.False);
        if(species=="holydeer")
        {Assert.That(status.Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(80f));Assert.That(status.DebuffImmune,Is.True);}
        if(species=="forestdeer")
        {Assert.That(Get(target,"familyAttackSpeedBonus"),Is.EqualTo(.2f));Assert.That(Get(target,"familyMoveSpeedBonus"),Is.EqualTo(.2f));Assert.That(Get(target,"familyAttackSpeedTimer"),Is.EqualTo(4f));}
    }

    [Test]
    public void SpiritDeerEclipseRespectsImmunityAndCleanseRemovesBothDebuffs()
    {
        SpiritDeerSetup("eclipsedeer",SpiritDeerCombatRules.EclipsedeerTraitId);
        var status=(CombatStatusState)Get(target,"combatStatuses");status.ApplyDebuffImmunity(2f);
        MammothCall("ApplySpiritDeerEclipseCurse",source,target);Assert.That(Get(target,"familyHealingReductionTimer"),Is.EqualTo(0f));
        status.Clear();MammothCall("ApplySpiritDeerEclipseCurse",source,target);
        Assert.That(Get(target,"familyHealingReductionRatio"),Is.EqualTo(.25f));Assert.That(Get(target,"familyResistanceShredRatio"),Is.EqualTo(.15f));
        MammothCall("CleanseAllHarmfulEffects",target);
        Assert.That(Get(target,"familyHealingReductionTimer"),Is.EqualTo(0f));Assert.That(Get(target,"familyResistanceShredTimer"),Is.EqualTo(0f));
    }

    [Test]
    public void SpiritDeerBloomAppliesAtWaveArrivalAllowsLateEntryAndCannotRepeatOnReentry()
    {
        SpiritDeerSetup("forestdeer",SpiritDeerCombatRules.ForestdeerTraitId);
        Set(target,"team",Get(source,"team"));Set(target,"hp",1000f);
        var edge=Unit(0,Vector3.right*7.2f);Set(edge,"hp",4000f);
        var outside=Unit(0,Vector3.right*9);Set(outside,"hp",4000f);
        cast=(IEnumerator)MammothCall("ResolveSpiritSanctuary",source,GameMasterData.Skill(SpiritDeerCombatRules.SkillId),1f,false,0);
        Assert.That(cast.MoveNext(),Is.True);Assert.That(Hp(target),Is.EqualTo(1060f));Assert.That(Hp(edge),Is.EqualTo(4000f));
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,.4f);cast.MoveNext();
        Assert.That(Hp(edge),Is.EqualTo(4060f));Assert.That(Hp(outside),Is.EqualTo(4000f));
        Model(edge).transform.position=Vector3.right*20;cast.MoveNext();Model(edge).transform.position=Vector3.right*7;
        Model(outside).transform.position=Vector3.right*4;
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,1f);cast.MoveNext();
        Assert.That(Hp(edge),Is.EqualTo(4060f));Assert.That(Hp(outside),Is.EqualTo(4060f));
        Set(source,"alive",false);Assert.That(cast.MoveNext(),Is.True);
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,10f);
        Assert.That(cast.MoveNext(),Is.False);
    }

    [Test]
    public void SpiritDeerBuffVisualUsesBattleClockAndDisappearsWhenEffectIsRemoved()
    {
        var renderer=Model(target).AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
        float clock=0;bool active=true;
        var fx=PixelSpiritBlessing.Bless(renderer,2,Color.green,4f,()=>clock,()=>active);
        var tick=typeof(PixelSpiritBlessing).GetMethod("LateUpdate",Private);clock=.8f;tick.Invoke(fx,null);
        var ps=fx.GetComponentInChildren<ParticleSystem>();var a=new ParticleSystem.Particle[96];var b=new ParticleSystem.Particle[96];
        int count=ps.GetParticles(a);tick.Invoke(fx,null);Assert.That(ps.GetParticles(b),Is.EqualTo(count));
        for(int i=0;i<count;i++)Assert.That(b[i].position,Is.EqualTo(a[i].position));
        active=false;tick.Invoke(fx,null);Assert.That(fx==null,Is.True);
    }
}
