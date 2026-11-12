using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private object ConcertGhost(string species="holydeer")
    {
        var ghost=Unit(0,Vector3.zero);
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(ghost,"definition",Get(source,"definition"));Set(ghost,"spiritConcertOwner",source);
        Set(ghost,"spiritConcertScale",.5f);Set(ghost,"augmentShadowClone",true);
        Set(source,"maxHp",1000f);Set(ghost,"maxHp",1000f);Set(ghost,"damage",100f);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        return ghost;
    }

    [Test]
    public void SpiritDeerConcertDamageUsesHalfAttackHealthAndTrueDamageBeforeShields()
    {
        var ghost=ConcertGhost();Set(target,"itemShield",20f);
        Assert.That(SpiritReturnDamage(ghost,target,200f,skill:true,basic:false),Is.EqualTo(80f));
        Assert.That(Get(target,"itemShield"),Is.Zero);
        Assert.That(SpiritReturnDamage(ghost,target,160f,skill:true,basic:false),Is.EqualTo(80f));
        var method=typeof(MonsterAutoBattlePrototype).GetMethod("ApplyDamage",Private);
        var result=method.Invoke(battle,new object[]{ghost,target,0f,true,false,false,false,0f,120f,1f,false,false,false});
        Assert.That(result,Is.EqualTo(60f));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(220f));
        Assert.That(SpiritReturnDamage(source,target,200f,skill:true,basic:false),Is.EqualTo(200f));
    }

    [TestCase(CombatStatusKind.Root)][TestCase(CombatStatusKind.Knockdown)]
    [TestCase(CombatStatusKind.Freeze)][TestCase(CombatStatusKind.Airborne)]
    [TestCase(CombatStatusKind.AttackSpeedDown)][TestCase(CombatStatusKind.Silence)]
    public void SpiritDeerConcertControlIsHalfDurationWithoutShorteningExistingControl(CombatStatusKind kind)
    {
        var ghost=ConcertGhost();var states=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(states.ApplyTimed(kind,2f,ghost).remaining,Is.EqualTo(1f));
        states.ApplyTimed(kind,3f,source);states.ApplyTimed(kind,2f,ghost);
        Assert.That(states.Get(kind).remaining,Is.EqualTo(3f));
    }

    [Test]
    public void SpiritDeerConcertHolyBlessingHalvesHealingShieldAndImmunity()
    {
        var ghost=ConcertGhost();((IList)Get(ghost,"activeTraitIds")).Add(SpiritDeerCombatRules.HolydeerTraitId);
        Set(source,"hp",400f);
        MammothCall("ApplySpiritDeerSanctuaryBlessing",ghost,source,1f);
        Assert.That(Hp(source),Is.EqualTo(450f));
        var statuses=(CombatStatusState)Get(source,"combatStatuses");
        var shield=statuses.Get(CombatStatusKind.Shield);
        Assert.That(shield.magnitude,Is.EqualTo(40f));Assert.That(shield.remaining,Is.EqualTo(2f));
        statuses.Tick(1.01f);Assert.That(statuses.DebuffImmune,Is.False);
    }

    [Test]
    public void SpiritDeerConcertForestHasteKeepsStrengthHalvesDurationAndDoesNotStack()
    {
        var ghost=ConcertGhost("forestdeer");((IList)Get(ghost,"activeTraitIds")).Add(SpiritDeerCombatRules.ForestdeerTraitId);
        MammothCall("ApplySpiritDeerSanctuaryBlessing",ghost,source,1f);
        Assert.That(Get(source,"familyAttackSpeedTimer"),Is.EqualTo(2f));
        Assert.That(Get(source,"familyMoveSpeedTimer"),Is.EqualTo(2f));
        Assert.That(Get(source,"familyAttackSpeedBonus"),Is.EqualTo(.2f));
        MammothCall("ApplySpiritDeerSanctuaryBlessing",ghost,source,1f);
        Assert.That(Get(source,"familyAttackSpeedBonus"),Is.EqualTo(.2f));
    }

    [Test]
    public void SpiritDeerConcertGhostCannotBeTargetedDamagedOrOccupyOwnerTile()
    {
        var ghost=ConcertGhost();
        Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("IsCombatTargetable",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)
            .Invoke(null,new[]{ghost}),Is.False);
        Assert.That(SpiritReturnDamage(target,ghost,100f),Is.Zero);
        var occupied=(IDictionary)typeof(MonsterAutoBattlePrototype).GetField("occupancy",Private).GetValue(battle);
        occupied[Vector2Int.zero]=source;MammothCall("OccupyUnit",ghost,Vector2Int.zero);
        Assert.That(occupied[Vector2Int.zero],Is.SameAs(source));
    }

    [Test]
    public void SpiritDeerConcertCopiesOnlyIntrinsicsAndPreservesOriginalCastState()
    {
        var ghost=ConcertGhost();Set(ghost,"spiritConcertOwner",null);
        ((IList)Get(source,"activeTraitIds")).Add(SpiritDeerCombatRules.HolydeerTraitId);
        Trait(SpiritDeerRank3Rules.MyriadConcert);Trait("trait_skill_echo");
        Set(source,"skillTimer",7f);Set(source,"skillEchoId","original_pending_echo");
        MammothCall("ConfigureSpiritConcertGhost",ghost,source);
        Assert.That(((IList)Get(ghost,"activeTraitIds")).Cast<string>(),Is.EqualTo(new[]{SpiritDeerCombatRules.HolydeerTraitId}));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(7f));
        Assert.That(Get(source,"skillEchoId"),Is.EqualTo("original_pending_echo"));
        Assert.That(((GameObject)Get(ghost,"ring")).activeSelf,Is.False);
        Assert.That(Vector3.Distance(Model(ghost).transform.position,Model(source).transform.position),Is.GreaterThan(1.7f));
    }

    [Test]
    public void SpiritDeerConcertFieldDoesNotSlowEnemyCooldownOrRepeatBlessing()
    {
        Set(source,"skillId","");Set(source,"maxHp",1000f);Set(source,"hp",100f);
        Trait(SpiritDeerCombatRules.HolydeerTraitId);Set(target,"skillTimer",5f);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        cast=(IEnumerator)MammothCall("ResolveMyriadConcert",source,Vector3.zero,1f,0);
        cast.MoveNext();Assert.That(Hp(source),Is.EqualTo(200f));
        Model(source).transform.position=Vector3.right*20;
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,1f);cast.MoveNext();
        Model(source).transform.position=Vector3.zero;cast.MoveNext();
        Assert.That(Hp(source),Is.EqualTo(200f));Assert.That(Get(target,"skillTimer"),Is.EqualTo(5f));
        typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,5f);
        Assert.That(cast.MoveNext(),Is.False);
    }

    [Test]
    public void SpiritDeerConcertBeetleAirborneAnimationUsesHalfTimeAndStillPauses()
    {
        var go=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/061_jangpung"));
        try
        {
            var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
            float clock=0;int finished=0;
            driver.PlayBeetleFlip(()=>true,t=>clock=t,()=>finished++,false,
                1f/SpiritDeerRank3Rules.ConcertDurationScale);
            advance.Invoke(driver,new object[]{BeetleFlipRules.Landing*.5f});
            Assert.That(clock,Is.EqualTo(BeetleFlipRules.Landing).Within(.001f));
            driver.SetFrozen(true);advance.Invoke(driver,new object[]{1f});
            Assert.That(clock,Is.EqualTo(BeetleFlipRules.Landing).Within(.001f));
            driver.SetFrozen(false);advance.Invoke(driver,new object[]{(BeetleFlipRules.End-BeetleFlipRules.Landing)*.5f+.01f});
            Assert.That(finished,Is.EqualTo(1));
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }

    [Test]
    public void SpiritDeerConcertApproachesBeforeCastingInsteadOfLosingOutOfRangeEcho()
    {
        var ghost=ConcertGhost("geomchi");Set(ghost,"skillId",SabertoothCombatRules.SkillId);
        Set(ghost,"target",target);Set(ghost,"attackRange",1);
        Model(target).transform.position=Vector3.right*8f;Set(source,"skillTimer",7f);
        Assert.That(MammothCall("ApproachSpiritConcertTarget",ghost,0f),Is.False);
        for(int i=0;i<120;i++)
            if((bool)MammothCall("ApproachSpiritConcertTarget",ghost,1f/60f))break;
        Assert.That(MammothCall("ApproachSpiritConcertTarget",ghost,0f),Is.True);
        Assert.That(Vector3.Distance(Model(ghost).transform.position,Model(target).transform.position),Is.LessThan(3f));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(7f));
    }
}
