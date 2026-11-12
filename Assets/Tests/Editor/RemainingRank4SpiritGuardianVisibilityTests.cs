using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class RemainingRank4CinematicIntegrationTests
{
    private static void RefreshNativeGuardian(PixelSpiritGuardian guardian)=>
        typeof(PixelSpiritGuardian).GetMethod("LateUpdate",Hidden).Invoke(guardian,null);

    private PixelSpiritGuardian AddNativeGuardian(bool allied,System.Action blessingStarted=null)
    {
        var guardian=PixelSpiritGuardian.Create(Renderer(source),(Bounds)BattleGet("combatBounds"),allied,
            ()=>(float)BattleGet("battleElapsed"),()=>true,allied?-1.8f:1.8f,blessingStarted);
        Assert.That(guardian,Is.Not.Null);
        ((IList)BattleGet("spiritGuardians")).Add(guardian);return guardian;
    }

    private Dictionary<Renderer,bool> SpiritBattleRendererFlags(params PixelSpiritGuardian[] guardians)
    {
        var original=new Dictionary<Renderer,bool>();
        foreach(object unit in Units)
        {
            var ring=(GameObject)Get(unit,"ring");
            if(ring.GetComponent<MeshRenderer>()==null)ring.AddComponent<MeshRenderer>();
            foreach(Renderer renderer in Model(unit).GetComponentsInChildren<Renderer>(true))
                original[renderer]=renderer.forceRenderingOff;
        }
        foreach(PixelSpiritGuardian guardian in guardians)
            foreach(Renderer renderer in guardian.GetComponentsInChildren<Renderer>(true))
                original[renderer]=renderer.forceRenderingOff;
        return original;
    }

    [TestCase(0)][TestCase(1)]
    public void WaterMantleCapturesTheNativeSettledGoddessAndHidesHerBodyAndLightUntilTheMatchingHandoff(int team)
    {
        ReplaceSource("holydeer",team);
        var own=AddNativeGuardian(team==0);var opposite=AddNativeGuardian(team!=0);
        Assert.That(QueueSource(),Is.True);BeginQueued(source);
        var context=Stage.CurrentContext;var snapshot=new Rank4StageContext();
        Call("CaptureRank4ReturnBody",snapshot,own.BodyRenderer);
        Assert.That(context.guardianReturnVisible,Is.True);
        Assert.That(context.guardianReturnSprite,Is.SameAs(own.BodyRenderer.sprite));
        Assert.That(context.guardianReturnColor,Is.EqualTo(own.BodyRenderer.color));
        Assert.That(context.guardianReturnRearEdge,Is.EqualTo(own.BodyRenderer.sharedMaterial.GetFloat("_RearEdge")));
        Assert.That(context.guardianReturnPosition,Is.EqualTo(snapshot.returnPosition));
        Assert.That(context.guardianReturnHeight,Is.EqualTo(snapshot.returnHeight).Within(.0001f));
        Assert.That(context.guardianReturnHeight,Is.GreaterThan(0f));
        Assert.That(own.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),Is.True);
        Assert.That(opposite.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),Is.True);
        Assert.That(Stage.ActionTime,Is.EqualTo(-1f));Assert.That(BattleGet("battleElapsed"),Is.EqualTo(0f));
        SetField(Presentation,"elapsed",SpiritGuardianRank4Stage.FieldRevealTime);
        Call("UpdateSpiritGuardianCinematicVisibility",Presentation);
        Assert.That(opposite.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff),Is.True);
        Assert.That(own.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),Is.True);
        SetField(Presentation,"elapsed",SpiritGuardianRank4Stage.FieldReturnTime);
        Call("UpdateSpiritGuardianCinematicVisibility",Presentation);
        Assert.That(own.GetComponentsInChildren<Renderer>(true).All(r=>!r.forceRenderingOff),Is.True);
        Vector3 settledPosition=own.BodyRenderer.transform.position;Sprite settledPose=own.BodyRenderer.sprite;
        Call("FinishRank4Cinematic");RefreshNativeGuardian(own);
        Assert.That(own.BodyRenderer.transform.position,Is.EqualTo(settledPosition));
        Assert.That(own.BodyRenderer.sprite,Is.SameAs(settledPose),"The native apparition must not restart its descent after the final shot.");
        Assert.That(BattleGet("battleElapsed"),Is.EqualTo(0f));AssertNoDamage();
    }

    [TestCase(0)][TestCase(1)]
    public void WaterMantleReplacesItsNativeEntranceCueWithoutSilencingTheOppositeGuardian(int team)
    {
        ReplaceSource("holydeer",team);int ownBlessings=0,oppositeBlessings=0;
        var own=AddNativeGuardian(team==0,()=>ownBlessings++);
        var opposite=AddNativeGuardian(team!=0,()=>oppositeBlessings++);
        Assert.That(QueueSource(),Is.True);BeginQueued(source);Call("FinishRank4Cinematic");
        RefreshNativeGuardian(own);Assert.That(ownBlessings,Is.Zero,"The film owns this entrance's Aegis cue.");
        BattleSet("battleElapsed",.8f);RefreshNativeGuardian(opposite);RefreshNativeGuardian(opposite);
        Assert.That(oppositeBlessings,Is.EqualTo(1),"An unpresented guardian retains its own one-shot entrance cue.");
    }

    [TestCase(0,false)][TestCase(1,true)]
    public void WaterMantleRestoresActorsAndIndependentRingsAtTheFieldRevealWithoutRevealingTheGoddessEarly(int team,bool hiddenEnemy)
    {
        ReplaceSource("lunadeer",team,true);Renderer(primary).forceRenderingOff=hiddenEnemy;
        var own=AddNativeGuardian(team==0);var opposite=AddNativeGuardian(team!=0);
        opposite.GetComponentInChildren<ParticleSystemRenderer>(true).forceRenderingOff=true;
        var original=SpiritBattleRendererFlags(own,opposite);
        Assert.That(QueueSource(),Is.True);BeginQueued(source);
        Assert.That(original.Keys.All(renderer=>renderer.forceRenderingOff),Is.True);
        SetField(Presentation,"elapsed",SpiritGuardianRank4Stage.FieldRevealTime-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.True);
        foreach(var pair in original)
        {
            if(pair.Key.transform.IsChildOf(own.transform))Assert.That(pair.Key.forceRenderingOff,Is.True);
            else Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
        }
        Assert.That(Stage.CurrentContext.returnBodyVisible,Is.False,"The native source returns once, without a second stage body on top.");
        SetField(Presentation,"elapsed",SpiritGuardianRank4Stage.FieldReturnTime);
        Call("UpdateSpiritGuardianCinematicVisibility",Presentation);
        foreach(var pair in original)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
        Call("FinishRank4Cinematic");
        foreach(var pair in original)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
    }

    [TestCase("FinishRank4Cinematic",0)][TestCase("FinishRank4Cinematic",1)]
    [TestCase("ResetRank4Cinematics",0)][TestCase("ResetRank4Cinematics",1)]
    [TestCase("OnDisable",0)][TestCase("OnDisable",1)]
    public void InterruptedWaterMantleRestoresBothGuardiansTheirParticlesAndAllBattleActors(string cleanup,int team)
    {
        ReplaceSource("forestdeer",team);Renderer(primary).forceRenderingOff=true;
        var own=AddNativeGuardian(team==0);var opposite=AddNativeGuardian(team!=0);
        own.GetComponentInChildren<ParticleSystemRenderer>(true).forceRenderingOff=true;
        opposite.BodyRenderer.forceRenderingOff=true;
        var original=SpiritBattleRendererFlags(own,opposite);
        Assert.That(QueueSource(),Is.True);BeginQueued(source);
        Assert.That(original.Keys.All(renderer=>renderer.forceRenderingOff),Is.True);
        Call(cleanup);
        foreach(var pair in original)
            if(pair.Key!=null)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));AssertNoDamage();
    }
}
