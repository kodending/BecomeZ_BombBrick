using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class RemainingRank4CinematicIntegrationTests
{
    [TestCase(0,false)][TestCase(0,true)][TestCase(1,false)][TestCase(1,true)]
    public void MantisFilmHidesTheIndependentCocoonAndNewbornRenderersUntilOneNativeOnlyReturn(int team,bool originallyHidden)
    {
        ReplaceSource("mantius",team);PrepareMantisReaperVisibility();Set(source,"hp",0f);
        Assert.That(Call("TryBeginMantisDeathMolt",source,primary),Is.True);
        object cast=Get(source,"mantisMolt");var cocoon=(PixelMantisCocoon)Field(cast,"cocoon");
        Assert.That(cocoon,Is.Not.Null);Assert.That(cocoon.transform.IsChildOf(Model(source).transform),Is.False);
        Renderer(source).forceRenderingOff=originallyHidden;
        cocoon.GetComponentsInChildren<Renderer>(true)[0].forceRenderingOff=originallyHidden;
        MantisReaperVisualRoot(source).GetComponentsInChildren<Renderer>(true)[0].forceRenderingOff=originallyHidden;
        GameObject sourceRing=IndependentMantisVisibilityRing(source,originallyHidden);
        var original=MantisVisibilitySnapshot(cocoon.gameObject,sourceRing,MantisReaperVisualRoot(source));
        original.Add(Renderer(source),originallyHidden);
        BeginQueued(source);
        Assert.That(original.Keys.All(renderer=>renderer.forceRenderingOff),Is.True);
        Assert.That(Stage.CurrentContext.returnBodyVisible,Is.False);

        // Newborn models are created by the handoff after its initial renderer snapshot.
        object[] children=AddMantisVisibilityBrood(cast,originallyHidden);
        foreach(object child in children)
            foreach(var pair in MantisVisibilitySnapshot(Model(child),(GameObject)Get(child,"ring"),MantisReaperVisualRoot(child)))original.Add(pair.Key,pair.Value);
        HandoffAt(.57f);HandoffAt(.9f);
        float hp=(float)Get(source,"hp");
        foreach(object child in children)
        {
            Assert.That(Model(child).activeInHierarchy,Is.True);
            Assert.That(Call("IsCombatActive",child),Is.True);
            Assert.That(Model(child).GetComponentsInChildren<Renderer>(true).All(renderer=>renderer.forceRenderingOff),Is.True);
            Assert.That(((GameObject)Get(child,"ring")).GetComponentsInChildren<Renderer>(true).All(renderer=>renderer.forceRenderingOff),Is.True);
            Assert.That(MantisReaperVisualRoot(child),Is.Not.Null);
            Assert.That(MantisReaperVisualRoot(child).transform.IsChildOf(Model(child).transform),Is.False);
            Assert.That(MantisReaperVisualRoot(child).GetComponentsInChildren<Renderer>(true).All(renderer=>renderer.forceRenderingOff),Is.True);
        }
        Renderer(children[0]).forceRenderingOff=false;HandoffAt(.9f);
        Assert.That(Renderer(children[0]).forceRenderingOff,Is.True,"An existing animation callback cannot expose a second on-field cast.");
        Assert.That((float)Get(source,"hp"),Is.EqualTo(hp));Assert.That(((IList)Field(cast,"children")).Count,Is.EqualTo(3));
        Assert.That((float)Field(cast,"age"),Is.EqualTo(.57f).Within(.0001f));

        Assert.That(MantisVisibilityFilmAt(MantisDeathMoltRank4Stage.FieldReturnTime-.001f),Is.True);
        Assert.That(original.Keys.All(renderer=>renderer.forceRenderingOff),Is.True);
        Assert.That(MantisVisibilityFilmAt(MantisDeathMoltRank4Stage.FieldReturnTime),Is.True);
        foreach(var pair in original)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
        Assert.That(Stage.CurrentContext.returnBodyVisible,Is.False,"The real cocoon replaces the staged one; do not paint a duplicate parent.");
        var stageRoot=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(Stage);
        Assert.That(stageRoot.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer=>renderer.enabled),Is.False);
        Assert.That(Renderer(source).enabled,Is.False,"Hiding a film must not force the adult out of its still-living cocoon.");
        Assert.That(Get(source,"mantisMolt"),Is.SameAs(cast));Assert.That(Field(cast,"hatchAge"),Is.EqualTo(-1f));
        Assert.That((float)Get(source,"hp"),Is.EqualTo(hp));
        Call("FinishRank4Cinematic");
        foreach(var pair in original)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
        Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
    }

    [TestCase("finish")][TestCase("reset")][TestCase("disable")]
    [TestCase("pause")][TestCase("generation")][TestCase("death")]
    public void InterruptedMantisFilmRestoresNativeFlagsEvenWhenANewbornRendererWasDestroyed(string interruption)
    {
        ReplaceSource("flotis",0,true);PrepareMantisReaperVisibility();Set(source,"hp",0f);
        Assert.That(Call("TryBeginMantisDeathMolt",source,primary),Is.True);
        object cast=Get(source,"mantisMolt");var cocoon=(PixelMantisCocoon)Field(cast,"cocoon");
        var native=cocoon.GetComponentsInChildren<Renderer>(true);native[0].forceRenderingOff=true;
        var original=MantisVisibilitySnapshot(cocoon.gameObject,IndependentMantisVisibilityRing(source,true),MantisReaperVisualRoot(source));
        BeginQueued(source);object[] children=AddMantisVisibilityBrood(cast,true);
        foreach(object child in children)
            foreach(var pair in MantisVisibilitySnapshot(Model(child),(GameObject)Get(child,"ring"),MantisReaperVisualRoot(child)))original.Add(pair.Key,pair.Value);
        HandoffAt(.57f);
        Assert.That(original.Keys.All(renderer=>renderer.forceRenderingOff),Is.True);
        Object.DestroyImmediate(((GameObject)Get(children[0],"ring")).transform.GetChild(0).GetComponent<MeshRenderer>());
        if(interruption=="finish")Call("FinishRank4Cinematic");
        else if(interruption=="reset")Call("ResetRank4Cinematics");
        else if(interruption=="disable")Call("OnDisable");
        else
        {
            if(interruption=="pause")BattleSet("paused",true);
            else if(interruption=="generation")BattleSet("combatGeneration",(int)BattleGet("combatGeneration")+1);
            else Set(source,"hp",0f);
            Assert.That(UpdateRank4AndPrelude(),Is.False);
        }
        Assert.That(Presentation,Is.Null);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        foreach(var pair in original)
            if(pair.Key!=null)Assert.That(pair.Key.forceRenderingOff,Is.EqualTo(pair.Value),pair.Key.name);
        Assert.That(Renderer(source).forceRenderingOff,Is.False);
    }

    private object[] AddMantisVisibilityBrood(object cast,bool originallyHidden)
    {
        var result=new object[3];var children=(IList)Field(cast,"children");
        for(int i=0;i<result.Length;i++)
        {
            Vector3 destination=new Vector3(i-1,0f,1.5f);object child=Unit(Definition(source).monsterId,System.Convert.ToInt32(Get(source,"team")),destination);
            GameObject ring=IndependentMantisVisibilityRing(child,i==2&&originallyHidden);
            var detail=new GameObject("Native target ring edge");detail.transform.SetParent(ring.transform,false);detail.AddComponent<MeshRenderer>();
            Call("ConfigureMantisBroodling",source,child,Get(source,"maxHp"));
            Set(child,"mantisBroodEnd",destination);Set(child,"mantisBroodRise",.32f);Model(child).transform.position=Model(source).transform.position;
            Renderer(child).forceRenderingOff=i==1&&originallyHidden;
            children.Add(child);result[i]=child;
        }
        SetField(cast,"spawned",3);return result;
    }

    private void PrepareMantisReaperVisibility()
    {
        IndependentMantisVisibilityRing(source,false);
        ((IList)Get(source,"activeTraitIds")).Add(MantisRank3Rules.ReaperEmergence);
        Call("ActivateMantisReaper",source);
        Assert.That(MantisReaperVisualRoot(source),Is.Not.Null);
        Assert.That(MantisReaperVisualRoot(source).transform.IsChildOf(Model(source).transform),Is.False);
    }

    private GameObject MantisReaperVisualRoot(object unit)
    {
        var effect=(PixelMantisReaper)Get(unit,"mantisReaperFx");
        return effect==null?null:effect.gameObject;
    }

    private GameObject IndependentMantisVisibilityRing(object unit,bool hidden)
    {
        var ring=(GameObject)Get(unit,"ring");ring.transform.SetParent(battle.transform,false);
        var renderer=ring.GetComponent<MeshRenderer>();
        if(renderer==null)renderer=ring.AddComponent<MeshRenderer>();
        renderer.forceRenderingOff=hidden;
        Assert.That(ring.transform.IsChildOf(Model(unit).transform),Is.False);
        return ring;
    }

    private static Dictionary<Renderer,bool> MantisVisibilitySnapshot(params GameObject[] models)
    {
        var result=new Dictionary<Renderer,bool>();
        foreach(GameObject model in models)
            if(model!=null)foreach(Renderer renderer in model.GetComponentsInChildren<Renderer>(true))result.Add(renderer,renderer.forceRenderingOff);
        return result;
    }

    private bool MantisVisibilityFilmAt(float time)
    {
        SetField(Presentation,"elapsed",time-Time.unscaledDeltaTime);
        return (bool)UpdateRank4AndPrelude();
    }
}
