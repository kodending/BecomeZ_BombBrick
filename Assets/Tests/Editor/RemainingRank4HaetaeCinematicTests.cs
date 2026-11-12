using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class RemainingRank4CinematicIntegrationTests
{
    private PixelHaetaeDevourer BeginShadowHaetae(int team)
    {
        ReplaceSource("haeta",team);BattleSet("battleElapsed",3.25f);Call("BeginHaetaeDevourers");
        Assert.That(Queue.Count,Is.EqualTo(1));
        var effects=(IDictionary)BattleGet("haetaeDevourEffects");
        var effect=(PixelHaetaeDevourer)effects[Get(source,"team")];Assert.That(effect,Is.Not.Null);
        Assert.That(effect.PresentationHidden,Is.True);return effect;
    }

    private void AdvanceShadowHaetae(float time)
    {
        SetField(Presentation,"elapsed",time-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.True);
    }

    private static void RefreshShadowHaetae(PixelHaetaeDevourer effect)=>
        typeof(PixelHaetaeDevourer).GetMethod("LateUpdate",Hidden).Invoke(effect,null);

    private AudioClip HaetaeClip(string cue)
    {
        Type sound=type.GetNestedType("HaetaeSound",Hidden);
        return ((AudioClip[])BattleGet("haetaeSkillSounds"))[Convert.ToInt32(Enum.Parse(sound,cue))];
    }

    [TestCase(0)][TestCase(1)]
    public void ShadowHaetaeKeepsTheOpeningBenefitGateImmediateDuringPreludeAndAllSixReadOnlyShots(int team)
    {
        ReplaceSource("haeta",team);Set(primary,"itemShield",150f);Status(primary).ApplyShield(4f,100f);
        Status(primary).ApplyTimed(CombatStatusKind.ComboRally,5f,null,.5f);Set(primary,"rank3AttackBonusTimer",5f);
        BattleSet("battleElapsed",3.25f);Call("BeginHaetaeDevourers");
        Assert.That(Call("TotalVisibleShield",primary),Is.EqualTo(0f));Assert.That(Status(primary).Has(CombatStatusKind.ComboRally),Is.False);
        Assert.That(Get(primary,"rank3AttackBonusTimer"),Is.EqualTo(0f));Assert.That(Status(primary).BenefitsBlocked,Is.True);
        Set(primary,"hp",4000f);Assert.That(Call("Heal",primary,100f),Is.EqualTo(0f));
        Assert.That(Status(primary).ApplyShield(4f,150f).magnitude,Is.Zero);
        Assert.That(Call("UpdateRank4Cinematic"),Is.True);Assert.That(Prelude,Is.Not.Null);
        Assert.That(PreludeDuration,Is.EqualTo(.45f));Assert.That(Presentation,Is.Null);CompletePrelude();
        Assert.That(Call("UpdateRank4Cinematic"),Is.True);Assert.That(Stage.Duration,Is.EqualTo(4.80f));
        int uses=(int)Get(source,"skillUses"),hits=(int)Get(source,"skillHits");float damage=(float)Get(source,"skillDamage");
        float timer=(float)Get(source,"skillTimer"),cooldown=(float)Get(source,"cooldown");
        Vector3 caster=Model(source).transform.position,opponent=Model(primary).transform.position;var random=UnityEngine.Random.state;
        foreach(float time in HaetaeMandateRank4Stage.BoardTimes)
        {
            AdvanceShadowHaetae(time);Assert.That(Stage.ActionTime,Is.EqualTo(-1f));Assert.That(Status(primary).BenefitsBlocked,Is.True);
            Assert.That(BattleGet("battleElapsed"),Is.EqualTo(3.25f));Assert.That(Get(source,"skillTimer"),Is.EqualTo(timer));
            Assert.That(Get(source,"cooldown"),Is.EqualTo(cooldown));Assert.That(Get(source,"skillUses"),Is.EqualTo(uses));
            Assert.That(Get(source,"skillHits"),Is.EqualTo(hits));Assert.That(Get(source,"skillDamage"),Is.EqualTo(damage));
            Assert.That(Model(source).transform.position,Is.EqualTo(caster));Assert.That(Model(primary).transform.position,Is.EqualTo(opponent));
            Assert.That(Get(primary,"hp"),Is.EqualTo(4000f));Assert.That(Get(secondary,"hp"),Is.EqualTo(5000f));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));Assert.That(HaetaeClip("Absorb"),Is.Null);Assert.That(HaetaeClip("Swallow"),Is.Null);
        }
        Call("FinishRank4Cinematic");Assert.That(Status(primary).BenefitsBlocked,Is.True);
        Assert.That(Get(primary,"hp"),Is.EqualTo(4000f));Set(source,"hp",0f);Assert.That(Status(primary).BenefitsBlocked,Is.False);
        Assert.That(Call("TotalVisibleShield",primary),Is.EqualTo(0f),"Consumed opening benefits cannot return when the caster dies.");
    }

    [TestCase(0)][TestCase(1)]
    public void ShadowHaetaeHandsTheMatchingHeadToNativeAt460ButDefersRealMealParticlesAndSoundsUntil480(int team)
    {
        var fx=BeginShadowHaetae(team);BeginQueued(source);Rank4CinematicStage film=Stage;
        Assert.That(film.CurrentContext.guardianReturnVisible,Is.True);
        Assert.That(film.CurrentContext.guardianReturnSprite,Is.SameAs(fx.BodyRenderer.sprite));
        var snapshot=new Rank4StageContext();Call("CaptureRank4ReturnBody",snapshot,fx.BodyRenderer);
        Assert.That(film.CurrentContext.guardianReturnPosition,Is.EqualTo(snapshot.returnPosition));
        Assert.That(film.CurrentContext.guardianReturnHeight,Is.EqualTo(snapshot.returnHeight));
        Assert.That(fx.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),Is.True);
        Assert.That(Status(primary).ApplyShield(4f,100f).magnitude,Is.Zero);Assert.That(HaetaeClip("Absorb"),Is.Null);
        AdvanceShadowHaetae(4.53f);
        var rendered=film.CurrentContext.guardianReturnSprite;Assert.That(rendered,Is.Not.Null);
        Assert.That(fx.BodyRenderer.forceRenderingOff,Is.True);
        AdvanceShadowHaetae(4.60f);Assert.That(fx.PresentationHidden,Is.True);Assert.That(fx.BodyRenderer.forceRenderingOff,Is.False);
        Assert.That(fx.BodyRenderer.flipX,Is.EqualTo(team==1));Assert.That(fx.BodyRenderer.color.a,Is.EqualTo(.75f).Within(.001f));
        Assert.That(fx.GetComponentsInChildren<ParticleSystemRenderer>(true).All(r=>r.forceRenderingOff),Is.True);
        Assert.That(HaetaeClip("Absorb"),Is.Null);Assert.That(HaetaeClip("Swallow"),Is.Null);
        Assert.That(HaetaeClip("Appear"),Is.Not.Null);Assert.That(Time.timeScale,Is.Zero);
        Assert.That(Get(source,"skillHits"),Is.Zero);AssertNoDamage();
        SetField(Presentation,"elapsed",4.80f-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.False);Assert.That(Presentation,Is.Null);Assert.That(film.Active,Is.False);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(fx.PresentationHidden,Is.False);
        Assert.That(fx.GetComponentsInChildren<ParticleSystemRenderer>(true).All(r=>!r.forceRenderingOff),Is.True);
        BattleSet("battleElapsed",3.30f);RefreshShadowHaetae(fx);Assert.That(HaetaeClip("Absorb"),Is.Not.Null);
        Assert.That(HaetaeClip("Swallow"),Is.Null);BattleSet("battleElapsed",4.30f);RefreshShadowHaetae(fx);
        Assert.That(HaetaeClip("Swallow"),Is.Not.Null);Assert.That(Status(primary).BenefitsBlocked,Is.True);AssertNoDamage();
    }

    [TestCase("FinishRank4Cinematic",0)][TestCase("FinishRank4Cinematic",1)]
    [TestCase("ResetRank4Cinematics",0)][TestCase("ResetRank4Cinematics",1)]
    [TestCase("OnDisable",0)][TestCase("OnDisable",1)]
    [TestCase("caster death",0)][TestCase("caster death",1)]
    [TestCase("restart",0)][TestCase("restart",1)]
    public void InterruptedShadowHaetaeReleasesItsHiddenPresentationAndNeverCreatesDamageOrRestoresAnOldBenefit(string cleanup,int team)
    {
        var fx=BeginShadowHaetae(team);BeginQueued(source);Assert.That(CombatSkillClock.Active,Is.True);
        Assert.That(Status(primary).ApplyShield(5f,100f).magnitude,Is.Zero);AdvanceShadowHaetae(1.90f);
        if(cleanup=="caster death")
        {Set(source,"hp",0f);Assert.That(UpdateRank4AndPrelude(),Is.False);}
        else if(cleanup=="restart")
        {BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);Call("ResetRank4Cinematics");Call("ClearHaetaeDevourers");}
        else Call(cleanup);
        Assert.That(Presentation,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        Assert.That(fx==null||!fx.PresentationHidden,Is.True);Assert.That(Renderer(source).forceRenderingOff,Is.False);
        Assert.That(Call("TotalVisibleShield",primary),Is.EqualTo(0f));Assert.That(HaetaeClip("Absorb"),Is.Null);Assert.That(HaetaeClip("Swallow"),Is.Null);
        AssertNoDamage();
    }
}
