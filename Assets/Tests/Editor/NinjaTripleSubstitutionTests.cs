using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

// Rank 4 replaces the old reactive triple-substitution window. Base/rank-3
// substitution animation coverage remains in NinjaSubstitutionTests.
public sealed class NinjaTripleSubstitutionTests
{
    private NinjaSpacetimeFixture f;
    [SetUp] public void SetUp()=>f=new NinjaSpacetimeFixture();
    [TearDown] public void TearDown()=>f.Dispose();

    [Test] public void RankFourNoLongerArmsThreePassiveEvades()
    {
        f.Call("TriggerFamilyTranscendenceSkill",f.source);
        Assert.That(f.Call("FamilyTranscendenceStateFor",f.source,false),Is.Null);
        float dealt=(float)f.Call("ApplyDamage",f.enemy,f.source,100f,false,true,true,true,0f,0f,1f,false,false,false);
        Assert.That(dealt,Is.EqualTo(100f).Within(.01f));
        Assert.That(f.Float(f.source,"hp"),Is.EqualTo(4900f).Within(.01f));
        Assert.That(f.Int(f.source,"skillUses"),Is.Zero);
    }

    [TestCase("")]
    [TestCase(NinjaCatRank3Rules.ShadowlessCross)]
    [TestCase(NinjaCatRank3Rules.BlackMoonHunt)]
    [TestCase(NinjaCatRank3Rules.EightGateFormation)]
    public void RankFourReplacesTheSelectedHuntInsteadOfAddingItAfterward(string trait)
    {
        // Use real rank-3 IDs when supplied; identity does not affect rank-4 dispatch.
        if(!string.IsNullOrEmpty(trait))((IList)f.Get(f.source,"activeTraitIds")).Add(trait);
        var before=f.Model(f.source).transform.position;
        Assert.That(f.Call("BeginShadowHunt",f.source,GameMasterData.Skill(NinjaCatCombatRules.SkillId)),Is.True);
        Assert.That(f.Active,Is.True);Assert.That(f.Get(f.source,"ninjaHuntPending"),Is.False);
        Assert.That(f.Get(f.source,"ninjaCrossActive"),Is.False);
        Assert.That(f.Float(f.source,"ninjaUntargetableTimer"),Is.Zero);
        Assert.That(f.Int(f.source,"skillUses"),Is.EqualTo(1));
        Assert.That(f.Model(f.source).transform.position,Is.EqualTo(before));
        f.Advance(NinjaSpacetimeRules.Windup);
        Assert.That(f.Int(f.source,"skillHits"),Is.EqualTo(1));
        Assert.That(f.Float(f.enemy,"hp"),Is.EqualTo(4700f).Within(.01f));
        Assert.That(f.Get(f.source,"currentAction"),Is.EqualTo("ninja_spacetime_collapse"));
    }

    [Test] public void RankFourEchoCannotResurrectTheOldRankThreeHunt()
    {
        f.Set(f.source,"skillEchoId",NinjaCatCombatRules.SkillId);f.Set(f.source,"skillEchoTarget",f.enemy);
        f.Call("ExecuteSkillEcho",f.source);
        Assert.That(f.Float(f.enemy,"hp"),Is.EqualTo(5000f));
        Assert.That(f.Int(f.source,"skillHits"),Is.Zero);
        Assert.That(f.Get(f.source,"ninjaHuntPending"),Is.False);
    }

    [Test] public void PreparationDoesNotInterceptIncomingBasicAttacks()
    {
        Assert.That(f.Begin(),Is.True);
        Assert.That((float)f.Call("ApplyDamage",f.enemy,f.source,100f,false,true,true,true,0f,0f,1f,false,false,false),
            Is.EqualTo(100f).Within(.01f));
        Assert.That(f.Active,Is.True);Assert.That(f.Int(f.source,"skillHits"),Is.Zero);
    }
}
