using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private IList Winds=>(IList)typeof(MonsterAutoBattlePrototype).GetField("griffinCrosswinds",Private).GetValue(battle);
    private void WindSetup()
    {
        GriffinSetup();Set(source,"augmentTranscended",true);
        GriffinCall("BindProjectileSource",source);GriffinCall("BindProjectileSource",target);
        GriffinCall("BeginGriffinCrosswind",source);
    }
    private void WindTime(float time)=>typeof(MonsterAutoBattlePrototype).GetField("battleElapsed",Private).SetValue(battle,time);
    private bool WindBlocks(Vector3 a,Vector3 b,object attacker=null)=>
        (bool)GriffinCall("DeflectGriffinProjectile",Get(attacker??target,"team"),a,b,null);
    private void WindTick(float time){WindTime(time);GriffinCall("UpdateGriffinCrosswinds");}
    private void WindWaveStart()
    {
        typeof(MonsterAutoBattlePrototype).GetField("battleStarted",Private).SetValue(battle,true);
        GriffinCall("BeginGriffinStormOrbs");
    }
    private object WindField(object wind,string name)=>wind.GetType().GetField(name).GetValue(wind);
    private IList StoredWindShots=>(IList)WindField(Winds[0],"shots");

    [TestCase(false)]
    [TestCase(true)]
    public void GriffinWindStaysHorizontalWhenTargetsChangeRowsOrDisappear(bool enemy)
    {
        GriffinSetup();Set(source,"augmentTranscended",true);
        if(enemy)Set(source,"team",Get(target,"team"));
        foreach(var position in new[]{new Vector3(4,0,6),new Vector3(-4,0,-6),Vector3.zero})
        {
            Model(target).transform.position=position;
            Set(source,"target",position==Vector3.zero?null:target);
            GriffinCall("BeginGriffinCrosswind",source);
            var direction=(Vector3)Winds[0].GetType().GetField("forward").GetValue(Winds[0]);
            Assert.That(direction,Is.EqualTo(enemy?Vector3.left:Vector3.right));
        }
    }

    [Test]
    public void GriffinWindCoversEveryArenaRowButNotOutsideTheBattlefield()
    {
        WindSetup();
        foreach(float x in new[]{-19f,0f,19f})foreach(float z in new[]{-19f,0f,19f})
            Assert.That(WindBlocks(new Vector3(x,1,z),new Vector3(x,1,z)),Is.True);
        Assert.That(WindBlocks(new Vector3(21,1,5),new Vector3(21,1,-5)),Is.False);
        Assert.That(WindBlocks(new Vector3(-19,1,21),new Vector3(19,1,21)),Is.False);
        Assert.That(WindBlocks(new Vector3(-19,1,-21),new Vector3(19,1,-21)),Is.False);
        Assert.That(WindBlocks(new Vector3(0,1,5),new Vector3(0,1,-5),source),Is.False);
        Model(source).transform.position=Vector3.forward*10;
        Assert.That(WindBlocks(new Vector3(0,1,5),new Vector3(0,1,-5)),Is.True);
        Assert.That(WindBlocks(new Vector3(0,1,9),new Vector3(0,1,11)),Is.True);
    }

    [Test]
    public void GriffinStormOrbAbsorbsForEightBattleSecondsAndNeverRefreshesBeforeWaveReset()
    {
        WindSetup();WindTime(7.999f);
        Assert.That(WindBlocks(Vector3.forward*10,Vector3.back*10),Is.True);
        WindTime(8f);Assert.That(WindBlocks(Vector3.forward*10,Vector3.back*10),Is.False);
        GriffinCall("BeginGriffinCrosswind",source);
        Assert.That(WindField(Winds[0],"started"),Is.EqualTo(0f));
        WindTick(GriffinStormOrbRules.End+.01f);Assert.That(Winds.Count,Is.Zero);
        GriffinCall("BeginGriffinCrosswind",source);Assert.That(Winds.Count,Is.Zero);
        GriffinCall("ResetFamilyTranscendenceStates");Assert.That(Winds.Count,Is.Zero);
        GriffinCall("BeginGriffinCrosswind",source);Assert.That(Winds.Count,Is.EqualTo(1));
    }

    [TestCase(GriffinRank3Rules.TyphoonDive)]
    [TestCase(GriffinRank3Rules.RaptorHunt)]
    [TestCase(GriffinRank3Rules.RoyalDescent)]
    public void GriffinStormOrbStartsAtWaveEntryWithoutConsumingCooldownAndRankThreeCastsStayIndependent(string trait)
    {
        GriffinSetup();Set(source,"augmentTranscended",true);Trait(trait);
        Set(source,"skillTimer",2f);
        Assert.That(Winds.Count,Is.Zero);WindWaveStart();
        Assert.That(Winds.Count,Is.EqualTo(1));Assert.That(Get(source,"griffinSkyfalling"),Is.False);
        Assert.That(Get(source,"griffinSkyfallPending"),Is.False);Assert.That(Get(source,"griffinHunt"),Is.Null);
        Assert.That(Get(source,"skillUses"),Is.Zero);Assert.That(Get(source,"skillTimer"),Is.EqualTo(2f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(0));
        Assert.That(GriffinCall("TryUseSkill",source,false),Is.False);
        WindTick(GriffinStormOrbRules.End+.01f);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));Assert.That(Hp(target),Is.EqualTo(4850f).Within(.01f));
        GriffinCall("BeginGriffinSkyfall",source,GameMasterData.Skill(GriffinCombatRules.SkillId));
        Assert.That(Winds.Count,Is.Zero);Assert.That(Get(source,"griffinSkyfalling"),Is.True);
        for(int i=0;i<140;i++)GriffinCall("UpdateGriffinSkyfall",source,1f/60f);
        Assert.That(Winds.Count,Is.Zero);Assert.That(Get(source,"skillHits"),Is.GreaterThan(3));
        int uses=(int)Get(source,"skillUses");
        GriffinCall("ResolveGriffinSkyfallImpact",source,GameMasterData.Skill(GriffinCombatRules.SkillId),
            Vector3.zero,.5f,true,false);
        Assert.That(Winds.Count,Is.Zero);Assert.That(Get(source,"skillUses"),Is.EqualTo(uses));
    }

    [TestCase(false,false)]
    [TestCase(true,true)]
    public void GriffinWindRejectsRankThreeAndShadowClones(bool rankFour,bool clone)
    {
        GriffinSetup();Set(source,"augmentTranscended",rankFour);Set(source,"augmentShadowClone",clone);
        GriffinCall("BeginGriffinCrosswind",source);Assert.That(Winds.Count,Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GriffinStormOrbReturnsThreeBaseShotsAndStoredShotsToTheOpposingTeamOnce(bool enemy)
    {
        GriffinSetup();Set(source,"augmentTranscended",true);
        if(enemy){Set(source,"team",Get(target,"team"));Set(target,"team",Enum.ToObject(Get(target,"team").GetType(),0));}
        GriffinCall("BeginGriffinCrosswind",source);
        WindBlocks(Vector3.zero,Vector3.right);WindBlocks(Vector3.forward,Vector3.back);
        Assert.That(StoredWindShots.Count,Is.EqualTo(5));
        WindTick(GriffinStormOrbRules.Burst-.01f);Assert.That(Hp(target),Is.EqualTo(5000f));
        WindTick(GriffinStormOrbRules.End+.01f);
        Assert.That(5000f-Hp(target),Is.EqualTo(220f).Within(.01f));Assert.That(Get(source,"skillHits"),Is.EqualTo(5));
        WindTick(GriffinStormOrbRules.End+5f);Assert.That(Get(source,"skillHits"),Is.EqualTo(5));
        Assert.That(Winds.Count,Is.Zero);
    }

    [Test]
    public void GriffinStormOrbCapturesPowerAndTraitsAtSummonAndAbsorbsOnlyDuringItsWindow()
    {
        GriffinSetup();Set(source,"augmentTranscended",true);Set(source,"skillTraitDamageMultiplier",2f);
        GriffinCall("BeginGriffinCrosswind",source);
        Set(source,"damage",900f);Set(source,"skillTraitDamageMultiplier",7f);
        WindTime(GriffinStormOrbRules.AbsorbDuration-.001f);
        Assert.That(WindBlocks(Vector3.zero,Vector3.right),Is.True);
        WindTime(GriffinStormOrbRules.AbsorbDuration);
        Assert.That(WindBlocks(Vector3.zero,Vector3.right),Is.False);
        WindTick(GriffinStormOrbRules.End+.01f);
        Assert.That(Hp(target),Is.EqualTo(4630f).Within(.01f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AReleasedGriffinStormOrbSurvivesItsCastersDeathOrFreeze(bool killed)
    {
        WindSetup();WindBlocks(Vector3.zero,Vector3.right);
        if(killed){Set(source,"alive",false);Set(source,"hp",0f);}
        else ((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(CombatStatusKind.Freeze,20f,source);
        WindTick(GriffinStormOrbRules.End+.01f);
        Assert.That(Hp(target),Is.EqualTo(4815f).Within(.01f));Assert.That(Winds.Count,Is.Zero);
    }

    [Test]
    public void GriffinStormOrbRetargetsDeadPreyAndIgnoresAnOpposingOrbOnItsReturn()
    {
        WindSetup();object second=Unit(1,Vector3.right*8);
        Set(second,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_pone"));
        Set(second,"augmentTranscended",true);GriffinCall("BeginGriffinCrosswind",second);
        WindTick(GriffinStormOrbRules.Burst+.1f);
        Set(target,"alive",false);Set(target,"hp",0f);
        WindTick(GriffinStormOrbRules.End+.01f);
        Assert.That(Hp(second),Is.EqualTo(4850f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(3));Assert.That(Get(second,"skillHits"),Is.EqualTo(3));
        Assert.That(Winds.Count,Is.Zero);
    }

    [TestCase("ending")]
    [TestCase("generation")]
    [TestCase("reset")]
    public void GriffinStormOrbCancelsOutstandingDamageAtBattleEndOrWaveReset(string reason)
    {
        WindSetup();WindBlocks(Vector3.zero,Vector3.right);
        WindTick(GriffinStormOrbRules.Burst+.2f);
        Assert.That(Hp(target),Is.EqualTo(5000f));
        if(reason=="ending")typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).SetValue(battle,true);
        else if(reason=="generation")typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else GriffinCall("ResetFamilyTranscendenceStates");
        WindTick(GriffinStormOrbRules.End+.01f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Winds.Count,Is.Zero);
        Assert.That(GriffinCall("HasPendingReleasedEffects",Get(source,"team")),Is.False);
    }

    [Test]
    public void GriffinStormOrbKeepsTheEightSecondWindowUntilItsCinematicEntranceIsRevealed()
    {
        WindSetup();object wind=Winds[0];wind.GetType().GetField("waiting").SetValue(wind,true);
        WindTick(20f);Assert.That(Winds.Count,Is.EqualTo(1));
        Assert.That(WindBlocks(Vector3.zero,Vector3.right),Is.False);
        GriffinCall("RevealGriffinStormOrb",source);Assert.That(WindField(wind,"started"),Is.EqualTo(20f));
        WindTime(27.999f);Assert.That(WindBlocks(Vector3.zero,Vector3.right),Is.True);
        WindTime(28f);Assert.That(WindBlocks(Vector3.zero,Vector3.right),Is.False);
    }

    [Test]
    public void GriffinStormOrbCuesItsBurstWithoutTheDeadCastersModelAndNeverStacksHitSliding()
    {
        WindSetup();object wind=Winds[0];
        for(int i=0;i<24;i++)WindBlocks(Vector3.zero,Vector3.right);
        var sourceModel=Model(source);Set(source,"alive",false);Set(source,"model",null);
        Assert.DoesNotThrow(()=>WindTick(GriffinStormOrbRules.Burst));
        Assert.That(WindField(wind,"burstSounded"),Is.True);
        Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.DoesNotThrow(()=>WindTick(GriffinStormOrbRules.Burst+.1f));
        Set(source,"model",sourceModel);
        WindTick(GriffinStormOrbRules.End+.01f);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(27));
        Assert.That(WindField(wind,"nextHitSound"),Is.EqualTo(GriffinStormOrbRules.End+.10f).Within(.001f));
        Assert.That(Get(target,"hitSlideVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(Get(target,"displacing"),Is.False);
    }

    [Test]
    public void GriffinStormOrbStoresOriginalSpriteColorScaleAndFlipsWithoutAnEnemyHitCallback()
    {
        WindSetup();var renderer=new GameObject("Stored enemy projectile").AddComponent<SpriteRenderer>();
        renderer.sprite=PixelStarProjectile.BeanSprite;renderer.color=new Color(.2f,.7f,.4f,.8f);
        renderer.transform.localScale=new Vector3(2f,3f,1f);renderer.flipX=renderer.flipY=true;
        Assert.That(GriffinCall("DeflectGriffinProjectile",Get(target,"team"),Vector3.zero,Vector3.right,renderer),Is.True);
        object shot=StoredWindShots[3];
        Assert.That(WindField(shot,"sprite"),Is.SameAs(renderer.sprite));
        Assert.That(WindField(shot,"color"),Is.EqualTo(renderer.color));
        Assert.That(WindField(shot,"scale"),Is.EqualTo(renderer.transform.lossyScale));
        Assert.That(WindField(shot,"flipX"),Is.True);Assert.That(WindField(shot,"flipY"),Is.True);
        foreach(var field in shot.GetType().GetFields())Assert.That(typeof(Delegate).IsAssignableFrom(field.FieldType),Is.False);
        UnityEngine.Object.DestroyImmediate(renderer.gameObject);
        WindTick(GriffinStormOrbRules.End+.01f);Assert.That(Hp(target),Is.EqualTo(4815f).Within(.01f));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void GriffinStormOrbReturnCurvesBeginAtTheirOrbitAndEndExactlyOnTheTarget(int variant)
    {
        Vector3 start=new Vector3(0f,6f,0f),end=new Vector3(8f,1f,4f);
        Vector3 At(float t)=>GriffinStormOrbRules.ReturnPosition(start,end,t,variant,Vector3.right,
            Vector3.up,Vector3.right,3f);
        Assert.That(At(0f),Is.EqualTo(start));Assert.That(At(1f),Is.EqualTo(end));
        Assert.That(Vector3.Distance(At(.95f),end),Is.GreaterThan(Vector3.Distance(At(.05f),start)));
        Assert.That(Vector3.Cross(At(.5f)-start,end-start).magnitude,Is.GreaterThan(.1f));
        Assert.That(GriffinStormOrbRules.FlightTime(variant),Is.InRange(.55f,.95f));
    }

    [TestCase("pone",150f)]
    [TestCase("griphonas",150f)]
    [TestCase("holophonas",150f)]
    [TestCase("entpone",450f)]
    public void GriffinStormOrbStartsWithoutAReachableDiveTargetButNormalSkillRangeNeverChanges(string species,float damage)
    {
        GriffinSetup(species);Set(source,"augmentTranscended",true);
        Model(target).transform.position=Vector3.right*18f;
        var skill=GameMasterData.Skill(GriffinCombatRules.SkillId);
        Assert.That(GriffinCall("GriffinDiveTarget",source,skill),Is.Null);
        float reach=(float)GriffinCall("SkillActivationDistance",source,target,skill);
        Assert.That(float.IsPositiveInfinity(reach),Is.False);Assert.That(reach,Is.GreaterThan(0f));
        WindWaveStart();
        Assert.That(Get(source,"griffinSkyfalling"),Is.False);
        WindTick(GriffinStormOrbRules.End+.01f);
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
        Assert.That(GriffinCall("GriffinDiveTarget",source,skill),Is.Null);
        Assert.That(GriffinCall("SkillActivationDistance",source,target,skill),Is.EqualTo(reach));
    }

    [Test]
    public void GriffinStormOrbLeavesTheFirstNormalSkillAndItsEchoUntouched()
    {
        GriffinSetup();Set(source,"augmentTranscended",true);Trait("trait_skill_dual_manifest");
        WindWaveStart();
        Assert.That(Get(source,"skillEchoTimer"),Is.EqualTo(0f));
        Assert.That(Get(source,"skillUses"),Is.Zero);
        Assert.That(GriffinCall("ExecuteReadySkill",source,false),Is.True);
        Assert.That(Get(source,"skillEchoTimer"),Is.GreaterThan(0f));
        Assert.That(Get(source,"griffinSkyfalling"),Is.True);
        Assert.That(Get(source,"skillUses"),Is.EqualTo(1));Assert.That(Winds.Count,Is.EqualTo(1));
    }

    [Test]
    public void GriffinStormOrbsAutomaticallyStartForEveryLivingDeployedRankFourOnBothTeamsOncePerWaveInSimulation()
    {
        GriffinSetup();Set(source,"augmentTranscended",true);Set(source,"skillTimer",6f);
        Set(target,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_entpone"));
        Set(target,"augmentTranscended",true);Set(target,"skillTimer",4f);
        foreach(string state in new[]{"rank3","clone","bench","dead"})
        {
            object other=Unit(0,Vector3.forward*3f);
            Set(other,"definition",Get(source,"definition"));Set(other,"augmentTranscended",state!="rank3");
            if(state=="clone")Set(other,"augmentShadowClone",true);
            if(state=="bench")Set(other,"inStorage",true);
            if(state=="dead")Set(other,"alive",false);
        }
        GriffinCall("BeginGriffinStormOrbs");Assert.That(Winds.Count,Is.Zero,"Preparation does not summon an orb.");
        WindWaveStart();Assert.That(Winds.Count,Is.EqualTo(2));
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(6f));Assert.That(Get(target,"skillTimer"),Is.EqualTo(4f));
        Assert.That(Get(source,"skillUses"),Is.Zero);Assert.That(Get(target,"skillUses"),Is.Zero);
        Assert.That(UnityEngine.Object.FindObjectsOfType<PixelGriffinStormOrb>(),Is.Empty);
        WindWaveStart();Assert.That(Winds.Count,Is.EqualTo(2));
        GriffinCall("ResetFamilyTranscendenceStates");WindWaveStart();Assert.That(Winds.Count,Is.EqualTo(2));
    }

    [Test]
    public void GriffinStormOrbAbsorbsTheSimulationWebWithoutCreatingAVisualOrGroundZone()
    {
        WindSetup();Set(target,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_aranea"));
        Set(target,"skillId",SpiderCombatRules.SkillId);Set(target,"spiderWebTarget",Vector3.zero);
        int generation=(int)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle);
        GriffinCall("LaunchSpiderCaptureWeb",target,GameMasterData.Skill(SpiderCombatRules.SkillId),1f,false,generation);
        Assert.That(StoredWindShots.Count,Is.EqualTo(4));
        Assert.That(((IList)typeof(MonsterAutoBattlePrototype).GetField("spiderWebZones",Private).GetValue(battle)).Count,Is.Zero);
        Assert.That(UnityEngine.Object.FindObjectsOfType<PixelGriffinStormOrb>(),Is.Empty);
        Assert.That(UnityEngine.Object.FindObjectsOfType<PixelSpiderCaptureNet>(),Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void GriffinWindCancelsGuidedArcingPiercingAndKingFruitBeforeTheirHitCallbacks(int kind)
    {
        WindSetup();Model(target).transform.position=Vector3.forward*7;
        var enemy=Model(target).AddComponent<SpriteRenderer>();enemy.sprite=PixelStarProjectile.BeanSprite;
        object victim=Unit(0,Vector3.back*7);var model=Model(victim);
        int hits=0,sweeps=0,deflected=0;Action hit=()=>hits++;
        PixelStarProjectile shot=kind==0?PixelStarProjectile.Play(enemy.transform,enemy,model.transform,null,Color.white,false,hit):
            kind==1?PixelStarProjectile.PlayStone(enemy.transform,enemy,model.transform,null,Color.white,hit):
            kind==2?PixelStarProjectile.PlayBean(enemy.transform,enemy,model.transform,null,Color.white,true,2,hit):
            kind==3?PixelStarProjectile.PlayScytheWind(enemy.transform,enemy,model.transform.position,Color.white,1,
                null,0,false,(a,b)=>{if(b>.7f)sweeps++;},()=>true):
                PixelStarProjectile.PlayKingFruit(enemy.transform.position,model.transform.position,enemy,Color.white,0,hit);
        shot.OnDeflected=()=>deflected++;
        typeof(PixelStarProjectile).GetMethod("Advance",Private).Invoke(shot,new object[]{2f});
        Assert.That(deflected,Is.EqualTo(1));Assert.That(hits,Is.Zero);Assert.That(sweeps,Is.Zero);
    }

    [Test]
    public void GriffinWindLeavesAlliedFlightsAndOutsideArenaEnemyFlightsIntact()
    {
        WindSetup();int hits=0;
        foreach(bool allied in new[]{true,false})
        {
            object owner=allied?source:target;
            float row=allied?5f:21f;
            Model(owner).transform.position=new Vector3(-7,0,row);
            object victim=Unit(allied?1:0,new Vector3(7,0,row));
            var shot=PixelStarProjectile.Play(Model(owner).transform,null,Model(victim).transform,null,Color.white,false,()=>hits++);
            typeof(PixelStarProjectile).GetMethod("Advance",Private).Invoke(shot,new object[]{1f});
        }
        Assert.That(hits,Is.EqualTo(2));
    }

    [Test]
    public void GriffinWindPreventsBoomerangReturnHitsAndMortarLandings()
    {
        WindSetup();Model(target).transform.position=Vector3.forward*.1f;
        var enemy=Model(target).AddComponent<SpriteRenderer>();enemy.sprite=PixelStarProjectile.BeanSprite;
        int hits=0;
        var banana=PixelMonkeyReturn.Play(enemy.transform.position,Vector3.back,enemy,Color.white,()=>true,
            ()=>enemy.transform.position,i=>{},(i,back,a,b)=>hits++);
        typeof(PixelMonkeyReturn).GetMethod("Advance",Private).Invoke(banana,new object[]{5f});
        Assert.That(hits,Is.Zero);Assert.That(banana.Complete,Is.True);
        var mortar=PixelMonkeyMortar.Play(Vector3.forward*6,Vector3.zero,enemy,Color.white,1,2,()=>true,i=>{},i=>hits++);
        typeof(PixelMonkeyMortar).GetMethod("Advance",Private).Invoke(mortar,new object[]{3f});
        Assert.That(hits,Is.Zero);Assert.That(mortar.Complete,Is.True);
    }

    [Test]
    public void GriffinWindDisarmsFlyingWebBeforeItCreatesAGroundNet()
    {
        WindSetup();Model(target).transform.position=Vector3.forward*25;
        var enemy=Model(target).AddComponent<SpriteRenderer>();enemy.sprite=PixelStarProjectile.BeanSprite;
        int hits=0;var net=PixelSpiderCaptureNet.Throw(enemy,enemy.transform.position,Vector3.back*5,2,Color.white,()=>true,()=>hits++);
        typeof(PixelSpiderCaptureNet).GetMethod("Animate",Private).Invoke(net,new object[]{1f});
        Assert.That(hits,Is.Zero);Assert.That(net==null,Is.True);
    }

    [Test]
    public void GriffinWindReleasesWizardBarrageWaitsWithoutResolvingAnyHit()
    {
        WindSetup();Set(target,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_seoljwi"));
        Set(target,"skillId",WizardMouseCombatRules.SkillId);Set(target,"target",source);
        int generation=(int)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle);
        var barrage=(IEnumerator)GriffinCall("ResolveMagicStarBarrage",target,
            GameMasterData.Skill(WizardMouseCombatRules.SkillId),1f,false,generation);
        int steps=0;
        try
        {
            while(barrage.MoveNext()&&steps++<20)
                foreach(var shot in UnityEngine.Object.FindObjectsOfType<PixelStarProjectile>())
                    typeof(PixelStarProjectile).GetMethod("Advance",Private).Invoke(shot,new object[]{1f});
            Assert.That(steps,Is.LessThan(20));Assert.That(Get(target,"skillHits"),Is.EqualTo(0));
        }
        finally{(barrage as IDisposable)?.Dispose();}
    }

    [Test]
    public void GriffinWindDisarmsConvergingKunaiAndTheirCombinedExplosion()
    {
        WindSetup();float hp=Hp(source);
        Set(target,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_seolmyo"));
        int generation=(int)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle);
        GriffinCall("ResolveNinjaEightGateImmediate",target,source,1f,generation);
        Assert.That(Hp(source),Is.EqualTo(hp));
        Assert.That(((CombatStatusState)Get(source,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.False);
    }

    [Test]
    public void GriffinWindCatchesDescendingMeteorBeforeImpactRegardlessOfUpdateOrder()
    {
        WindSetup();var enemy=Model(target).AddComponent<SpriteRenderer>();enemy.sprite=PixelStarProjectile.BeanSprite;
        var meteor=PixelWizardSupernova.Create(Vector3.zero,Vector3.right*7,2,enemy,Color.white,()=>true);
        meteor.BeginDescent();Assert.That(meteor.DeflectBeforeImpact(),Is.True);Assert.That(meteor==null,Is.True);
    }

    private object AddGriffinEnergyAbsorber(bool friendly)
    {
        object griffin=Unit(friendly?0:1,new Vector3(1,0,6));
        Set(griffin,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_pone"));
        Set(griffin,"augmentTranscended",true);GriffinCall("BeginGriffinCrosswind",griffin);
        return griffin;
    }

    [TestCase("basic",false,4)] [TestCase("basic",true,0)]
    [TestCase("core",false,6)] [TestCase("core",true,0)]
    [TestCase("pressure",false,1)] [TestCase("pressure",true,0)]
    [TestCase("heaven",false,1)] [TestCase("heaven",true,0)]
    public void GriffinStormOrbAbsorbsEnemyEnergyPulsesWithoutTheirDamageOrDebuffsAndAllowsFriendlyEnergy(string kind,bool friendly,int captures)
    {
        if(kind=="heaven")Set(source,"augmentTranscended",true);
        if(kind=="core")OverheatSetup("frovire",WyvernCombatRules.FrovireTraitId);
        else if(kind=="pressure")PressureSetup("frovire",WyvernCombatRules.FrovireTraitId);
        else WyvernSetup("frovire",WyvernCombatRules.FrovireTraitId);
        AddGriffinEnergyAbsorber(friendly);
        if(kind=="heaven")HeavenTick(WyvernHeavenBreathRules.EndTime+.01f);
        else BreathActionTick(3f);
        Assert.That(StoredWindShots.Count,Is.EqualTo(GriffinStormOrbRules.BasicShots+captures));
        if(friendly)Assert.That(Hp(target),Is.LessThan(5000f));
        else
        {
            Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.Zero);
            var statuses=(CombatStatusState)Get(target,"combatStatuses");
            Assert.That(statuses.Has(CombatStatusKind.ComboChilled),Is.False);
            Assert.That(statuses.Has(CombatStatusKind.Freeze),Is.False);
            Assert.That(WindField(StoredWindShots[3],"color"),Is.EqualTo(PixelWyvernBreath.Tint(2)));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void GriffinStormOrbPreventsANewAbsorbedFireFanButLeavesAnAlreadyLaidFireField(bool existingField)
    {
        PressureSetup("igvire",WyvernCombatRules.IgvireTraitId);
        if(existingField)BreathActionTick(.7f);
        AddGriffinEnergyAbsorber(false);
        if(!existingField)BreathActionTick(.7f);
        float before=Hp(target);BreathTick(4f);
        Assert.That(before-Hp(target),Is.EqualTo(existingField?80f:0f).Within(.01f));
        Assert.That(StoredWindShots.Count,Is.EqualTo(existingField?3:4));
    }

    [Test]
    public void AbsorbedPressureAndHeavenParticlesConvergeIntoTheOrbWithoutLandingDebris()
    {
        Vector3 anchor=new Vector3(0,8,0);
        var pressure=PixelWyvernPressure.Create(null,Vector3.zero,Vector3.right,6f,1,()=>0f,()=>true);
        pressure.Pose(.6f,Vector3.right);pressure.AbsorbTo(anchor);pressure.Pose(.72f,Vector3.right);
        var buffer=new ParticleSystem.Particle[288];var body=pressure.transform.Find("Rolling Breath").GetComponent<ParticleSystem>();
        int count=body.GetParticles(buffer);float far=0f;
        for(int i=0;i<count;i++)far+=Vector3.Distance(buffer[i].position,anchor);
        pressure.Pose(.83f,Vector3.right);count=body.GetParticles(buffer);float near=0f;
        for(int i=0;i<count;i++)near+=Vector3.Distance(buffer[i].position,anchor);
        Assert.That(near,Is.LessThan(far*.1f));pressure.Cancel();

        var heaven=PixelWyvernHeavenBreath.Create(new Bounds(Vector3.zero,new Vector3(20,5,16)),null,5,2);
        heaven.Advance(1.2f);heaven.AbsorbTo(anchor);heaven.Advance(1.49f);
        body=heaven.transform.Find("Heaven Breath Mass").GetComponent<ParticleSystem>();
        Assert.That(body.particleCount,Is.Zero);
        Assert.That(heaven.transform.Find("Heaven Breath Fragments").GetComponent<ParticleSystem>().particleCount,Is.Zero);
        heaven.Cancel();
    }
}
