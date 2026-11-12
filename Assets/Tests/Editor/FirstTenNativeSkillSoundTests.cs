using System;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class FirstTenNativeSkillSoundTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;

    [TestCase("geomchi","Sabertooth","sabertooth","DevourInhale","devour_inhale","drag")]
    [TestCase("geomchi","Sabertooth","sabertooth","DevourSnap","devour_snap","finish")]
    [TestCase("chundongi","Ninja","ninja_cat","SpacetimeCharge","spacetime_charge","scrolls")]
    [TestCase("chundongi","Ninja","ninja_cat","SpacetimeCollapse","spacetime_collapse","moonburst")]
    [TestCase("kongjwi","Wizard","wizard_mouse","MeteorRelease","meteor_release","descent")]
    [TestCase("kongjwi","Wizard","wizard_mouse","MeteorHit","meteor_hit","crash")]
    [TestCase("kongjwi","Wizard","wizard_mouse","MeteorFinish","meteor_finish","crash")]
    [TestCase("nukgu","Wolf","wolf","PackPass","pack_pass","rush")]
    [TestCase("nukgu","Wolf","wolf","PackHit","pack_hit","spectralhit")]
    public void NewBattlefieldActionsUseTheirOwnPreparedClipWithoutOverwritingTheOriginalSkillBank(
        string species,string bank,string family,string cue,string key,string fallback)
    {
        var singleton=typeof(GameAudio).GetField("instance",Hidden);object previous=singleton.GetValue(null);
        using(var f=new NinjaSpacetimeFixture())
        {
            singleton.SetValue(null,null);
            try
            {
                f.Field("mlEpisodeActive",false);object actor=f.Unit(0,0f,species);
                Type sounds=typeof(MonsterAutoBattlePrototype).GetNestedType(bank+"Sound",Hidden);
                object sound=Enum.Parse(sounds,cue);f.Call("Play"+bank+"Sound",actor,sound,Vector3.zero,1f);
                string field=char.ToLowerInvariant(bank[0])+bank.Substring(1)+"SkillSounds";
                var clips=(AudioClip[])typeof(MonsterAutoBattlePrototype).GetField(field,Hidden).GetValue(f.battle);
                string originalFolder=bank=="Ninja"?"NinjaCat":bank=="Wizard"?"WizardMouse":bank;
                AudioClip expected=Resources.Load<AudioClip>("Audio/Combat/Native/"+family+"/"+key)??
                    Resources.Load<AudioClip>("Audio/Combat/"+originalFolder+"/"+fallback);
                Assert.That(expected,Is.Not.Null);Assert.That(clips[Convert.ToInt32(sound)],Is.SameAs(expected));
                Assert.That(clips[0],Is.Null,"The original first cue remains separate and untouched.");
            }
            finally{singleton.SetValue(null,previous);}
        }
    }
}
