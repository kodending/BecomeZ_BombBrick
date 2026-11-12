using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    [TestCase("gas",typeof(PixelMushroomGas))]
    [TestCase("crown",typeof(PixelParasiticCrown))]
    [TestCase("breath",typeof(PixelWyvernBreath))]
    [TestCase("overheat",typeof(PixelWyvernBreath))]
    [TestCase("pressure",typeof(PixelWyvernPressure))]
    [TestCase("rain",typeof(PixelWyvernRain))]
    [TestCase("roar",typeof(PixelHaetaeJusticeRoar))]
    [TestCase("oppression",typeof(PixelHaetaeOppression))]
    [TestCase("bellow",typeof(PixelHaetaeBellow))]
    [TestCase("thunder",typeof(PixelHaetaeThunder))]
    [TestCase("gravity",typeof(PixelBeetleGravity))]
    [TestCase("royal",typeof(PixelGriffinRoyal))]
    [TestCase("dive",typeof(PixelGriffinDive))]
    [TestCase("guardian",typeof(PixelSpiritGuardian))]
    [TestCase("supernova",typeof(PixelWizardSupernova))]
    [TestCase("lava",typeof(PixelTyrannoLavaFieldEffect))]
    [TestCase("sanctuary",typeof(PixelSpiritSanctuaryEffect))]
    public void AuthoredSkillVisualsFinishAfterTheResultWithoutContinuingCombat(string skill,Type effectType)
    {
        var previous=new HashSet<CombatEffectEndPlayback>(Object.FindObjectsOfType<CombatEffectEndPlayback>());
        var definition=(MonsterDefinition)Get(source,"definition");
        var prefab=Resources.Load<GameObject>(definition.resourcePath);
        var sprite=prefab.GetComponentInChildren<SpriteRenderer>(true).sprite;
        Assert.That(sprite,Is.Not.Null,"Use a real monster body for bounds and pixel geometry.");
        Model(source).AddComponent<SpriteRenderer>().sprite=sprite;
        Model(target).AddComponent<SpriteRenderer>().sprite=sprite;
        SpawnBattleEndVisual(skill);
        // This fixture keeps the battle disabled so its full scene initialization cannot run.
        foreach(var child in battle.transform.Cast<Transform>().ToArray())child.SetParent(null,true);
        var effects=Object.FindObjectsOfType(effectType).Cast<MonoBehaviour>().ToArray();
        Assert.That(effects.Length,Is.GreaterThan(0),"The real skill must create its authored visual.");
        Set(target,"alive",false);
        float hp=Hp(target),sourceHp=Hp(source);
        int hits=(int)Get(source,"skillHits");

        Assert.DoesNotThrow(()=>ReaperCall("CheckBattleResult"),skill+" capture adapter");
        foreach(var effect in effects)
        {
            Assert.That(CombatEffectEndPlayback.IsPreserving(effect),Is.True,skill+" must survive battle cleanup.");
            if(effect is PixelTyrannoLavaFieldEffect lava)
            {
                lava.Stop();
                Assert.That(typeof(PixelTyrannoLavaFieldEffect).GetField("elapsed",Private).GetValue(lava),
                    Is.LessThan(lava.Duration),"Battle cleanup cannot skip the remaining lava animation.");
            }
            if(effect is PixelSpiritSanctuaryEffect sanctuary)
            {
                sanctuary.Stop();
                Assert.That(typeof(PixelSpiritSanctuaryEffect).GetField("elapsed",Private).GetValue(sanctuary),
                    Is.LessThan(sanctuary.Duration),"Battle cleanup cannot skip the remaining field animation.");
            }
        }
        for(int frame=0;frame<200;frame++)
        foreach(var playback in Object.FindObjectsOfType<CombatEffectEndPlayback>().Where(p=>!previous.Contains(p)).ToArray())
            Assert.DoesNotThrow(()=>typeof(CombatEffectEndPlayback).GetMethod("Advance",Private)
                .Invoke(playback,new object[]{.1f}),skill+" visual step");

        foreach(var effect in effects)
            Assert.That(effect==null||!effect.gameObject.activeInHierarchy,Is.True,skill+" must reach natural completion.");
        Assert.That(Hp(target),Is.EqualTo(hp),"Result-time visuals cannot deal further damage.");
        Assert.That(Hp(source),Is.EqualTo(sourceHp),"Result-time visuals cannot apply healing.");
        Assert.That(Get(source,"skillHits"),Is.EqualTo(hits));
        CombatEffectEndPlayback.Clear(battle.gameObject.scene);
        ReaperCall("ClearBattleEffects");
        Assert.That(Object.FindObjectsOfType<CombatEffectEndPlayback>().Where(p=>!previous.Contains(p)),Is.Empty);
    }

    private void SpawnBattleEndVisual(string skill)
    {
        if(skill=="gas"){GasReady();GasCreate();return;}
        if(skill=="crown")
        {
            CrownReady();typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,false);
            CrownCreate();return;
        }
        if(skill=="breath"||skill=="overheat"||skill=="pressure"||skill=="rain")
        {
            GriffinSetup("vivre");Set(source,"skillId",WyvernCombatRules.SkillId);
            Set(source,"skillCooldownDuration",8.5f);
            typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,false);
            if(skill=="overheat")Trait(WyvernRank3Rules.DragonCore);
            if(skill=="pressure")Trait(WyvernRank3Rules.CataclysmBreath);
            if(skill=="rain")Trait(WyvernRank3Rules.WingSweep);
            Assert.That(GriffinCall("BeginWyvernBreath",source,GameMasterData.Skill(WyvernCombatRules.SkillId)),Is.True);
            return;
        }
        if(skill=="roar"||skill=="oppression"||skill=="bellow"||skill=="thunder")
        {
            var row=HaetaeSetup("haeta",null);
            typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,false);
            if(skill=="oppression")Trait(HaetaeRank3Rules.DivineOppression);
            if(skill=="bellow")Trait(HaetaeRank3Rules.JudgmentBellow);
            if(skill=="thunder")Trait(HaetaeRank3Rules.HeavenlyJudgment);
            Assert.That(GriffinCall("BeginHaetaeJusticeRoar",source,row),Is.True);
            if(skill=="oppression"||skill=="thunder")GriffinCall("UpdateHaetaeRoars",.7f);
            return;
        }
        if(skill=="gravity")
        {
            Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_jangpung"));
            Set(source,"skillId",BeetleCombatRules.SkillId);Trait(BeetleRank3Rules.ZeroGravity);
            Assert.That(MammothCall("TryResolveBeetleRank3",source,target,
                GameMasterData.Skill(BeetleCombatRules.SkillId),1f,1f,0),Is.True);return;
        }
        if(skill=="royal"||skill=="dive")
        {
            GriffinSetup();typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,false);
            if(skill=="dive")Assert.That(GriffinCall("BeginGriffinSkyfall",source,GameMasterData.Skill(GriffinCombatRules.SkillId)),Is.True);
            else
            {
                Trait(GriffinRank3Rules.RoyalDescent);GriffinCall("GrantGriffinRoyal",source);
                GriffinCall("FireGriffinRoyalWind",source,target);
            }
            return;
        }
        if(skill=="guardian")
        {
            GriffinSetup("holydeer");Set(source,"augmentTranscended",true);
            typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,false);
            typeof(MonsterAutoBattlePrototype).GetField("battleStarted",Private).SetValue(battle,true);
            MammothCall("BeginSpiritGuardians");return;
        }
        if(skill=="supernova")
        {
            var meteor=PixelWizardSupernova.Create(Model(target).transform.position,Model(source).transform.position,
                2f,Model(source).GetComponent<SpriteRenderer>(),Color.white,()=>true);
            for(int i=0;i<5;i++)meteor.Launch(Model(source).transform.position,i);
            return;
        }
        if(skill=="lava")
        {
            PixelCombatEffect.PlayTyrannoLavaField(Model(target).transform.position,2f,3f,
                Model(source).GetComponent<SpriteRenderer>());return;
        }
        if(skill=="sanctuary")
        {
            PixelCombatEffect.PlaySpiritSanctuary(Model(target).transform.position,2f,3f,Color.cyan,
                Model(source).GetComponent<SpriteRenderer>());return;
        }
        Assert.Fail("Unknown authored skill fixture: "+skill);
    }
}
