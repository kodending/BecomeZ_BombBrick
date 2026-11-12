using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is=UnityEngine.TestTools.Constraints.Is;

public sealed class FamilyTranscendenceRulesTests
{
    private static readonly string[] Families=
    {
        "sabertooth","frog","crocodile","kingkong","wizard_mouse",
        "bear","wolf","arachne","ninja_cat","tyranno","monkey",
        "mantis","mushroom","mammoth","beetle","spirit_deer",
        "griffin","wyvern","haetae"
    };

    private static readonly string[] TraitIds=
    {
        "trait_transcend_raptor_apex_predator",
        "trait_transcend_sabertooth_absolute_predation",
        "trait_transcend_frog_all_fishing",
        "trait_transcend_crocodile_abyss_death_roll",
        "trait_transcend_kingkong_heart_of_leyline",
        "trait_transcend_wizard_mouse_infinite_constellation",
        "trait_transcend_bear_immortal_bear_soul",
        "trait_transcend_wolf_full_moon_alpha",
        "trait_transcend_arachne_spider_queen_domain",
        "trait_transcend_ninja_cat_infinite_assassination",
        "trait_transcend_tyranno_tyrants_reign",
        "trait_transcend_monkey_celestial_acrobatic_troupe",
        "trait_transcend_mantis_dimensional_severing",
        "trait_transcend_mushroom_endless_proliferation",
        "trait_transcend_mammoth_ark_of_earth",
        "trait_transcend_beetle_horn_of_gravity",
        "trait_transcend_spirit_deer_eternal_sanctuary",
        "trait_transcend_griffin_divine_bird_combo",
        "trait_transcend_wyvern_dragon_blood_awakening",
        "trait_transcend_haetae_executor_of_heavens_will"
    };

    [Test]
    public void EveryRemainingFamilyHasOneDistinctTranscendence()
    {
        List<string> names=Families
            .Select(FamilyTranscendenceRules.DisplayName).ToList();
        Assert.That(names,Has.None.Empty);
        Assert.That(names.Distinct().Count(),Is.EqualTo(Families.Length));
        Assert.That(Families.All(FamilyTranscendenceRules.IsSupportedFamily),
            Is.True);
    }

    [Test]
    public void FamilyActivationMembershipDoesNotAllocateAndKeepsCaseAndAliasSupport()
    {
        var families=Families.Concat(new[]{"spider"}).ToArray();
        var upper=families.Select(value=>value.ToUpperInvariant()).ToArray();
        bool matches=true;
        TestDelegate work=()=>
        {
            for(int repeat=0;repeat<128;repeat++)
            {
                for(int i=0;i<families.Length;i++)
                    matches&=FamilyTranscendenceRules.CanActivate(families[i],true)&&
                        FamilyTranscendenceRules.IsSupportedFamily(upper[i]);
                matches&=!FamilyTranscendenceRules.IsSupportedFamily(null)&&
                    !FamilyTranscendenceRules.IsSupportedFamily("")&&
                    !FamilyTranscendenceRules.IsSupportedFamily("not_a_family");
            }
        };
        work();Assert.That(work,Is.Not.AllocatingGCMemory());Assert.That(matches,Is.True);
        Assert.That(FamilyTranscendenceRules.DisplayName("SpIdEr"),Is.EqualTo("거미 여왕의 영역"));
    }

    [Test]
    public void RaptorStaysOnItsDedicatedApexPredatorPath()
    {
        Assert.That(FamilyTranscendenceRules.IsSupportedFamily("raptor"),Is.False);
        Assert.That(FamilyTranscendenceRules.DisplayName("raptor"),Is.Empty);
    }

    [Test]
    public void EveryFamilyTranscendenceIsRegisteredInMasterData()
    {
        Assert.That(GameMasterData.Database,Is.Not.Null);
        foreach(string traitId in TraitIds)
        {
            TraitMasterRow row=GameMasterData.Database.traits
                .FirstOrDefault(trait=>trait!=null&&trait.id==traitId);
            Assert.That(row,Is.Not.Null,traitId);
            Assert.That(row.enabled,Is.True,traitId);
            Assert.That(row.category,Is.EqualTo("Rank4Transcendence"),traitId);
            Assert.That(row.effectSummary,Is.Not.Empty,traitId);
        }
    }

    [Test]
    public void ClonesAndSummonsCannotRecursivelyActivateTranscendence()
    {
        Assert.That(FamilyTranscendenceRules.CanActivate("wolf",true),Is.True);
        Assert.That(FamilyTranscendenceRules.CanActivate(
            "wolf",true,shadowClone:true),Is.False);
        Assert.That(FamilyTranscendenceRules.CanActivate(
            "wolf",true,eclipseClone:true),Is.False);
        Assert.That(FamilyTranscendenceRules.CanActivate(
            "mushroom",true,summon:true),Is.False);
        Assert.That(FamilyTranscendenceRules.CanActivate("wolf",false),Is.False);
    }

    [Test]
    public void CoreBalanceValuesMatchApprovedDesign()
    {
        Assert.That(FamilyTranscendenceRules.DisplayName("sabertooth"),Is.EqualTo("대포식"));
        Assert.That(SabertoothDevourRules.InhaleDuration,Is.EqualTo(.45f));
        Assert.That(SabertoothDevourRules.PullDuration,Is.EqualTo(1.1f));
        Assert.That(SabertoothDevourRules.HoldDuration,Is.EqualTo(.65f));
        Assert.That(SabertoothDevourRules.HitTime,Is.EqualTo(2.255f).Within(.0001f));
        Assert.That(SabertoothDevourRules.Duration,Is.EqualTo(2.66f).Within(.0001f));
        Assert.That(SabertoothDevourRules.DamageRatio,Is.EqualTo(1.8f));
        Assert.That(WizardCygnusShowerRules.WaveCount,
            Is.EqualTo(3));
        Assert.That(FamilyTranscendenceRules.ArachneZoneDuration,
            Is.EqualTo(8f));
        Assert.That(FamilyTranscendenceRules.DisplayName("mammoth"),Is.EqualTo("거인의 연쇄 충돌"));
        Assert.That(MammothExpulsionRules.InitialDamageRatio,Is.EqualTo(1f));
        Assert.That(MammothExpulsionRules.CollisionDamageRatio,Is.EqualTo(.6f));
        Assert.That(MammothExpulsionRules.MaximumCollisionHits,Is.EqualTo(6));
        Assert.That(MammothExpulsionRules.Duration,Is.EqualTo(3.2f));
        Assert.That(FamilyTranscendenceRules.DisplayName("spirit_deer"),Is.EqualTo("수호정령 강림"));
        Assert.That(FamilyTranscendenceRules.DisplayName("haetae"),Is.EqualTo("천명 집행자"));
    }
}
