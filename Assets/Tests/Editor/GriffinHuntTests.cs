using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    [Test]
    public void GriffinHuntAnimationPinsCarryPoseAndPausesVictimDragWithCombatClock()
    {
        var model=new GameObject("Griffin Hunt Pose Test");model.AddComponent<SpriteRenderer>();
        var driver=model.AddComponent<SpriteMonsterAnimationDriver>();
        var frames=new Sprite[6];for(int i=0;i<frames.Length;i++)frames[i]=PixelStarProjectile.BeanSprite;
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);driver.ConfigureHitFrames(3,3);
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        var late=typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private);
        float clock=0f;
        driver.PlaySkillAttackWithHold(2,.34f,1f,null,()=>clock);
        clock=.34f;advance.Invoke(driver,new object[]{.34f});
        driver.HoldSkillAttackFrame(2,.4f);late.Invoke(driver,null);
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(2));
        driver.HoldSkillAttackHitFrame(.1f);late.Invoke(driver,null);
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
        driver.InterruptCurrentAction(true);driver.SetStruggling(true,()=>clock);
        clock+=.12f;advance.Invoke(driver,new object[]{.12f});late.Invoke(driver,null);
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Struggle"));int frame=driver.CurrentFrameIndex;
        advance.Invoke(driver,new object[]{5f});late.Invoke(driver,null);
        Assert.That(driver.CurrentFrameIndex,Is.EqualTo(frame));
        driver.SetStruggling(false);Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
    }

    private void StartHunt(string species="pone")
    {
        GriffinSetup(species);Trait(GriffinRank3Rules.RaptorHunt);
        Assert.That(GriffinCall("BeginGriffinSkyfall",source,GameMasterData.Skill(GriffinCombatRules.SkillId)),Is.True);
    }

    [Test]
    public void GriffinHuntCasterPoseUsesItsAlreadyPacedActionClockExactlyOnce()
    {
        GriffinSetup();Trait(GriffinRank3Rules.RaptorHunt);
        Model(source).AddComponent<SpriteRenderer>();
        var driver=Model(source).AddComponent<SpriteMonsterAnimationDriver>();
        var frames=new Sprite[6];for(int i=0;i<frames.Length;i++)frames[i]=PixelStarProjectile.BeanSprite;
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);driver.ConfigureHitFrames(3,3);
        driver.SetCombatPlaybackSpeed(BombBrick.Combat.CombatStatFormula.CombatAnimationRate);
        Set(source,"spriteAnimation",driver);
        int generation=(int)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle);
        GriffinCall("StartGriffinHunt",source,target,generation);
        var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private);
        var elapsed=typeof(SpriteMonsterAnimationDriver).GetField("motionElapsed",Private);

        GriffinCall("UpdateGriffinHunt",source,.08f);
        advance.Invoke(driver,new object[]{1f});
        Assert.That((float)elapsed.GetValue(driver),Is.EqualTo(.08f).Within(.00001f));
        advance.Invoke(driver,new object[]{1f});
        Assert.That((float)elapsed.GetValue(driver),Is.EqualTo(.08f).Within(.00001f),
            "A stopped action clock must hold the caster pose even when frame time advances.");
        GriffinCall("UpdateGriffinHunt",source,.06f);
        advance.Invoke(driver,new object[]{.001f});
        Assert.That((float)elapsed.GetValue(driver),Is.EqualTo(.14f).Within(.00001f),
            "The caster pose must follow hunt.time, rather than multiplying its .8 tempo a second time.");
        Assert.That(Hp(target),Is.EqualTo(5000f),"The grab contact has not been reached.");
    }

    [TestCase("pone",120f,360f)]
    [TestCase("griphonas",120f,360f)]
    [TestCase("holophonas",120f,360f)]
    [TestCase("entpone",360f,1080f)]
    public void GriffinHuntDealsTwoSingleTargetHitsAtGrabAndGroundContact(string species,float grab,float total)
    {
        StartHunt(species);object nearby=Unit(1,Model(target).transform.position+Vector3.forward*.6f);
        GriffinCall("UpdateGriffinSkyfall",source,GriffinHuntRules.Grab-.01f);
        Assert.That(Hp(target),Is.EqualTo(5000f));
        GriffinCall("UpdateGriffinSkyfall",source,.0101f);
        Assert.That(5000f-Hp(target),Is.EqualTo(grab).Within(.01f));
        Assert.That(Get(target,"griffinHeldBy"),Is.SameAs(source));
        var status=(CombatStatusState)Get(target,"combatStatuses");
        Assert.That(status.Has(CombatStatusKind.Airborne),Is.True);
        status.Tick(5f);Assert.That(status.Has(CombatStatusKind.Airborne),Is.True,"Carry owns the hold until contact, including paused casts.");
        GriffinCall("UpdateGriffinSkyfall",source,GriffinHuntRules.Fall-GriffinHuntRules.Grab-.0001f);
        float apex=Model(source).transform.position.y;
        Assert.That(apex,Is.GreaterThan(5f));
        GriffinCall("UpdateGriffinSkyfall",source,2f/60f);
        Assert.That(Model(source).transform.position.y,Is.GreaterThan(.1f));
        Assert.That(5000f-Hp(target),Is.EqualTo(grab).Within(.01f));
        GriffinCall("UpdateGriffinSkyfall",source,1f/60f);
        Assert.That(Model(source).transform.position.y,Is.EqualTo(0f).Within(.001f));
        Assert.That(5000f-Hp(target),Is.EqualTo(total).Within(.01f));
        Assert.That(Hp(nearby),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
        Assert.That(Get(target,"griffinHeldBy"),Is.Null);Assert.That(status.Has(CombatStatusKind.Airborne),Is.False);
        Assert.That(status.Get(CombatStatusKind.Knockdown).remaining,Is.EqualTo(.6f).Within(.001f));
        Assert.That(Get(target,"itemDefenseShredRatio"),Is.EqualTo(0f));
        Assert.That(Get(target,"familyResistanceShredRatio"),Is.EqualTo(0f));
        GriffinCall("UpdateGriffinSkyfall",source,1f);
        Assert.That(Get(source,"griffinHunt"),Is.Null);Assert.That(Get(source,"griffinSkyfalling"),Is.False);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
    }

    [Test]
    public void GriffinHuntTargetsHighestAttackAndFollowsUntilGrab()
    {
        GriffinSetup();Trait(GriffinRank3Rules.RaptorHunt);
        object strong=Unit(1,Vector3.right*10f),outside=Unit(1,Vector3.right*12f);
        Set(strong,"damage",200f);Set(outside,"damage",300f);
        GriffinCall("BeginGriffinSkyfall",source,GameMasterData.Skill(GriffinCombatRules.SkillId));
        Assert.That(Get(source,"target"),Is.SameAs(strong));
        Model(strong).transform.position+=Vector3.forward;
        GriffinCall("UpdateGriffinSkyfall",source,GriffinHuntRules.Grab);
        Assert.That(Get(source,"griffinSkyfallOrigin"),Is.EqualTo(Model(strong).transform.position));
        Assert.That(Hp(strong),Is.EqualTo(4880f).Within(.01f));Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [Test]
    public void GriffinCommittedHuntKeepsFollowingAnEscapingTargetWithoutChangingItsVictimOrDamageArea()
    {
        StartHunt();object alternate=Unit(1,Vector3.right*2f);Set(alternate,"damage",500f);
        Model(target).transform.position=new Vector3(14f,0f,1f);
        object nearby=Unit(1,Model(target).transform.position+Vector3.forward*.6f);
        GriffinCall("UpdateGriffinSkyfall",source,GriffinHuntRules.Grab);
        Assert.That(Get(source,"griffinHunt"),Is.Not.Null);
        Assert.That(Get(source,"target"),Is.SameAs(target));
        Assert.That(Get(source,"griffinSkyfallOrigin"),Is.EqualTo(Model(target).transform.position));
        Assert.That(Get(target,"griffinHeldBy"),Is.SameAs(source));
        Assert.That(Hp(target),Is.EqualTo(4880f).Within(.01f));
        GriffinCall("UpdateGriffinSkyfall",source,GriffinHuntRules.Landing-GriffinHuntRules.Grab);
        Assert.That(Hp(target),Is.EqualTo(4640f).Within(.01f));
        Assert.That(Hp(alternate),Is.EqualTo(5000f));Assert.That(Hp(nearby),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));Assert.That(Get(target,"griffinHeldBy"),Is.Null);
        GriffinCall("UpdateGriffinSkyfall",source,GriffinHuntRules.End-GriffinHuntRules.Landing+.01f);
        Assert.That(Get(source,"griffinHunt"),Is.Null);
    }

    [TestCase(false)] [TestCase(true)]
    public void GriffinCommitPlanKeepsTheOriginallySelectedTargetAfterItLeavesRange(bool hunting)
    {
        GriffinSetup();if(hunting)Trait(GriffinRank3Rules.RaptorHunt);
        var skill=GameMasterData.Skill(GriffinCombatRules.SkillId);
        Model(target).transform.position=Vector3.right*20f;
        object nearby=Unit(1,Vector3.right);Set(nearby,"damage",500f);
        Assert.That(GriffinCall("GriffinDiveTarget",source,skill),Is.SameAs(nearby),
            "An ordinary selection must still respect the authored range.");
        var requestType=typeof(MonsterAutoBattlePrototype).GetNestedType("PendingSkillFocus",System.Reflection.BindingFlags.NonPublic);
        object request=System.Activator.CreateInstance(requestType,true);
        requestType.GetField("unit").SetValue(request,source);requestType.GetField("castTarget").SetValue(request,target);
        var context=typeof(MonsterAutoBattlePrototype).GetField("committingSkill",Private);
        object previous=context.GetValue(battle);
        try
        {
            context.SetValue(battle,request);
            Assert.That(GriffinCall("GriffinDiveTarget",source,skill),Is.SameAs(target));
            requestType.GetField("unit").SetValue(request,nearby);
            Assert.That(GriffinCall("GriffinDiveTarget",source,skill),Is.SameAs(nearby),
                "A different caster's commitment cannot bypass this caster's range.");
        }
        finally{context.SetValue(battle,previous);}
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GriffinHuntReleasesOnCasterInterruptionOrTargetDeath(bool targetDies)
    {
        StartHunt();GriffinCall("UpdateGriffinSkyfall",source,GriffinHuntRules.Apex);
        if(targetDies){Set(target,"alive",false);GriffinCall("CleanupRank3Unit",target);GriffinCall("UpdateGriffinSkyfall",source,.01f);}
        else GriffinCall("InterruptPositionalAction",source);
        Assert.That(Get(target,"griffinHeldBy"),Is.Null);
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Airborne),Is.False);
        Assert.That(Get(source,"griffinHunt"),Is.Null);
        Assert.That(Model(source).transform.position.y,Is.EqualTo(0f).Within(.001f));
        Assert.That(Hp(target),Is.EqualTo(4880f).Within(.01f));
    }

    [Test]
    public void GriffinHuntRespectsControlImmunityAndRetainsCasterHealthShield()
    {
        StartHunt("entpone");Trait(GriffinCombatRules.EntPoneTraitId);
        object ally=Unit(0,Model(target).transform.position+Vector3.forward);
        Set(ally,"maxHp",1000f);
        var status=(CombatStatusState)Get(target,"combatStatuses");status.ControlImmune=true;
        GriffinCall("UpdateGriffinSkyfall",source,GriffinHuntRules.Grab);
        Assert.That(Get(target,"griffinHeldBy"),Is.Null);Assert.That(status.Has(CombatStatusKind.Airborne),Is.False);
        GriffinCall("UpdateGriffinSkyfall",source,GriffinHuntRules.Landing-GriffinHuntRules.Grab);
        Assert.That(Hp(target),Is.EqualTo(3920f).Within(.01f));Assert.That(status.Has(CombatStatusKind.Knockdown),Is.False);
        var shield=((CombatStatusState)Get(ally,"combatStatuses")).Get(CombatStatusKind.Shield);
        Assert.That(shield.magnitude,Is.EqualTo(400f).Within(.01f));
    }
}
