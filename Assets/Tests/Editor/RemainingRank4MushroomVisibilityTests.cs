using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed partial class RemainingRank4CinematicIntegrationTests
{
    private void CreateDeathBloomPresentation(object unit)
    {
        Call("TryBeginMushroomDeathBloom",unit);
        // The integration host is deliberately inactive; only its actual combat actors are active.
        foreach(GameObject visual in (IEnumerable<GameObject>)Call("MushroomDeathBloomVisualRoots",unit))
            if(visual!=null)visual.transform.SetParent(null,true);
        Call("FlushMushroomDeathBlooms");
        if(!Queue.Cast<object>().Any(request=>Field(request,"source")==unit))
            Assert.That(Call("QueueRank4Cinematic",unit,null),Is.True);
        SetField(Call("MushroomDeathBloomFor",unit),"awaitingPresentation",true);
    }
    [TestCase("versus",false,0)] [TestCase("versus",false,1)]
    [TestCase("doksirum",false,0)] [TestCase("doksirum",false,1)]
    [TestCase("lumirum",false,0)] [TestCase("lumirum",false,1)]
    [TestCase("kingmore",false,0)] [TestCase("kingmore",false,1)]
    [TestCase("doksirum",true,0)] [TestCase("doksirum",true,1)]
    public void DeathBloomKeepsItsNativeFlowerHiddenUntilTheMatchingFieldReturn(string species,bool shiny,int team)
    {
        ReplaceSource(species,team,shiny);Set(source,"hp",0f);Set(source,"alive",false);
        var entry=new Rank4StageContext();Call("CaptureRank4ReturnBody",entry,Renderer(source));
        CreateDeathBloomPresentation(source);BeginQueued(source);
        var body=(SpriteRenderer)Call("MushroomDeathBloomBodyRenderer",source);
        Assert.That(body,Is.Not.Null);Assert.That(body.forceRenderingOff,Is.True);
        Assert.That(Stage.CurrentContext.source.species,Is.EqualTo(species));
        Assert.That(Stage.CurrentContext.source.shiny,Is.EqualTo(shiny));
        Assert.That(Stage.CurrentContext.source.frames,Is.EqualTo(((SpriteMonsterAnimationDriver)Get(source,"spriteAnimation")).DeathFrames));
        Assert.That(Stage.CurrentContext.returnHeight,Is.GreaterThan(0f));
        Assert.That(Stage.CurrentContext.entryPosition,Is.EqualTo(entry.returnPosition));
        Assert.That(Stage.CurrentContext.entryHeight,Is.EqualTo(entry.returnHeight).Within(.0001f),
            "The original dying sprite must not inherit the later flower's body dimensions.");
        Assert.That(Stage.CurrentContext.returnBodyVisible,Is.False);
        SetField(Presentation,"elapsed",MushroomColonyRank4Stage.FieldReturnTime-.001f);
        Call("UpdateMushroomCinematicVisibility",Presentation);Assert.That(body.forceRenderingOff,Is.True);
        SetField(Presentation,"elapsed",MushroomColonyRank4Stage.FieldReturnTime);
        Call("UpdateMushroomCinematicVisibility",Presentation);Assert.That(body.forceRenderingOff,Is.False);
        Stage.Tick(MushroomColonyRank4Stage.FieldReturnTime,0f);
        Assert.That(Stage.CurrentContext.returnBodyVisible,Is.False,"The native flower must not have a second stage body drawn on top.");
        HandoffAt(0f);Assert.That(Call("IsMushroomDeathBloomPending",source),Is.False);
        Assert.That(Get(Call("MushroomDeathBloomUnit",source),"hp"),Is.EqualTo(Get(source,"maxHp")));
        AssertNoDamage();Call("FinishRank4Cinematic");
        Assert.That(body.forceRenderingOff,Is.False);Assert.That(CombatSkillClock.Active,Is.False);
    }

    [Test]
    public void TheOriginalCorpseDoesNotReappearOnTopOfTheFlowerWhenTheCurtainClears()
    {
        ReplaceSource("versus",0);Set(source,"hp",0f);Set(source,"alive",false);
        CreateDeathBloomPresentation(source);BeginQueued(source);
        SetField(Presentation,"elapsed",MushroomColonyRank4Stage.FieldReturnTime+.01f-Time.unscaledDeltaTime);
        Assert.That(UpdateRank4AndPrelude(),Is.True);
        Assert.That(Model(source).activeSelf,Is.False,"The corpse has already become the flower.");
        Assert.That(Model(Call("MushroomDeathBloomUnit",source)).activeInHierarchy,Is.True);
        Call("FinishRank4Cinematic");Assert.That(Model(source).activeSelf,Is.False);
    }

    [Test]
    public void SkippingDeathBloomPresentationRestoresTheFlowerAndReleasesItsClock()
    {
        ReplaceSource("versus",0);Set(source,"hp",0f);Set(source,"alive",false);
        CreateDeathBloomPresentation(source);BeginQueued(source);
        var body=(SpriteRenderer)Call("MushroomDeathBloomBodyRenderer",source);
        Assert.That(body.forceRenderingOff,Is.True);Call("FinishRank4Cinematic");
        Assert.That(body.forceRenderingOff,Is.False);
        Assert.That(Call("IsMushroomDeathBloomPending",source),Is.False);
        Call("UpdateMushroomDeathBlooms",.5f);float hp=(float)Get(primary,"hp");
        Assert.That(hp,Is.LessThan(5000f));Call("FinishRank4Cinematic");
        Assert.That(Get(primary,"hp"),Is.EqualTo(hp));
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase(false)] [TestCase(true)]
    public void DeathBloomKeepsOtherActorsOutOfTheOpaqueStageAndRestoresTheirExactFlagsAtTheCurtain(bool hiddenEnemy)
    {
        ReplaceSource("versus",0);Set(source,"hp",0f);Set(source,"alive",false);
        object friend=Unit("woongi",0,Vector3.left*2f);
        Renderer(primary).forceRenderingOff=hiddenEnemy;
        var original=new Dictionary<Renderer,bool>();
        foreach(object unit in new[]{primary,secondary,friend})
        {
            var ring=(GameObject)Get(unit,"ring");
            var ringRenderer=ring.GetComponent<MeshRenderer>();
            if(ringRenderer==null)ringRenderer=ring.AddComponent<MeshRenderer>();
            ringRenderer.forceRenderingOff=unit==primary&&hiddenEnemy;
            foreach(var renderer in Model(unit).GetComponentsInChildren<Renderer>(true)
                .Concat(ring.GetComponentsInChildren<Renderer>(true)))original[renderer]=renderer.forceRenderingOff;
        }
        CreateDeathBloomPresentation(source);BeginQueued(source);
        var flower=(SpriteRenderer)Call("MushroomDeathBloomBodyRenderer",source);
        foreach(var pair in original)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),"The battlefield prelude must stay intact.");
        SetField(Presentation,"elapsed",MushroomColonyRank4Stage.OpaqueStageTime);
        Call("UpdateMushroomCinematicVisibility",Presentation);
        Assert.That(original.Keys.All(renderer=>renderer.forceRenderingOff),Is.True);
        Assert.That(flower.forceRenderingOff,Is.True);
        SetField(Presentation,"elapsed",MushroomColonyRank4Stage.FieldRevealTime);
        Call("UpdateMushroomCinematicVisibility",Presentation);
        foreach(var pair in original)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
        Assert.That(flower.forceRenderingOff,Is.True,"Only the staged flower bridges the covered return until the handoff.");
        SetField(Presentation,"elapsed",MushroomColonyRank4Stage.FieldReturnTime);
        Call("UpdateMushroomCinematicVisibility",Presentation);
        Assert.That(flower.forceRenderingOff,Is.False);
        Call("FinishRank4Cinematic");
        foreach(var pair in original)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
    }

    [TestCase("FinishRank4Cinematic")] [TestCase("ResetRank4Cinematics")] [TestCase("OnDisable")]
    public void InterruptedDeathBloomRestoresOtherActorsAndIndependentRings(string cleanup)
    {
        ReplaceSource("lumirum",1,true);Set(source,"hp",0f);Set(source,"alive",false);
        Renderer(primary).forceRenderingOff=true;
        var original=new Dictionary<Renderer,bool>();
        foreach(object unit in new[]{primary,secondary})
        {
            var ring=(GameObject)Get(unit,"ring");
            if(ring.GetComponent<MeshRenderer>()==null)ring.AddComponent<MeshRenderer>();
            foreach(var renderer in Model(unit).GetComponentsInChildren<Renderer>(true)
                .Concat(ring.GetComponentsInChildren<Renderer>(true)))original[renderer]=renderer.forceRenderingOff;
        }
        CreateDeathBloomPresentation(source);BeginQueued(source);
        SetField(Presentation,"elapsed",MushroomColonyRank4Stage.OpaqueStageTime+.1f);
        Call("UpdateMushroomCinematicVisibility",Presentation);
        Assert.That(original.Keys.All(renderer=>renderer.forceRenderingOff),Is.True);
        Call(cleanup);
        foreach(var pair in original)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
        Assert.That(CombatSkillClock.Active,Is.False);
        Assert.That(Time.timeScale,Is.EqualTo(1f));
    }
}
