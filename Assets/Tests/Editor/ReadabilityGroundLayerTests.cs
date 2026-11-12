using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityGroundLayerTests
{
    private bool testMode,enabled;
    private CombatReadabilityPreset preset;
    private GameObject host;
    private PixelMushroomGas gas;
    private PixelSpiritSanctuaryEffect decal;
    private CombatSkillClock stage;
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;

    [SetUp] public void Setup()
    {
        testMode=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;preset=CombatReadabilityPreview.Preset;
    }

    [TearDown] public void Cleanup()
    {
        stage?.End();
        if(gas!=null)Object.DestroyImmediate(gas.gameObject);
        if(decal!=null)Object.DestroyImmediate(decal.gameObject);
        if(host!=null)Object.DestroyImmediate(host);
        CombatEffectTestConfiguration.Active=testMode;CombatReadabilityPreview.Enabled=enabled;CombatReadabilityPreview.Preset=preset;
    }

    [TestCase(CombatReadabilityPreset.Current,true,true,false)]
    [TestCase(CombatReadabilityPreset.Compact,true,true,false)]
    [TestCase(CombatReadabilityPreset.Local,true,true,false)]
    [TestCase(CombatReadabilityPreset.Local,false,true,false)]
    [TestCase(CombatReadabilityPreset.Local,true,false,false)]
    [TestCase(CombatReadabilityPreset.Local,true,true,true)]
    public void AreaCloudKeepsItsOriginalOrderAboveBodiesAndTheFloorDecal(
        CombatReadabilityPreset mode,bool test,bool selected,bool cinematic)
    {
        CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=selected;CombatReadabilityPreview.Preset=mode;
        if(cinematic){stage=new CombatSkillClock(null);stage.Hold();}
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("kingmore");
        var prefab=Resources.Load<GameObject>(definition.resourcePath);
        var authoredBody=prefab.GetComponentInChildren<SpriteRenderer>(true);
        var authoredAnimation=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var frames=(Sprite[])typeof(SpriteMonsterAnimationDriver).GetField("idleFrames",Hidden).GetValue(authoredAnimation);
        Assert.That(frames.Length,Is.GreaterThanOrEqualTo(4));
        host=new GameObject("Floor gas layer body");
        var body=host.AddComponent<SpriteRenderer>();
        body.sharedMaterial=authoredBody.sharedMaterial;body.sortingLayerID=authoredBody.sortingLayerID;body.sortingOrder=10;
        gas=PixelMushroomGas.Play(Vector3.zero,2f,4f,Color.green,body);
        decal=PixelCombatEffect.PlayMushroomPoisonZone(Vector3.zero,2f,4f,body);
        var cloud=gas.transform.Find("Drifting Gas").GetComponent<ParticleSystemRenderer>();
        var motes=gas.transform.Find("Drifting Spores").GetComponent<ParticleSystemRenderer>();
        gas.Resonate(Vector3.zero,.5f,1f);
        var resonance=gas.transform.Find("Resonant Spore Burst").GetComponent<ParticleSystemRenderer>();
        Assert.That(cloud.sharedMaterial.renderQueue,Is.EqualTo(body.sharedMaterial.renderQueue));
        Assert.That(cloud.sortingLayerID,Is.EqualTo(body.sortingLayerID));
        Assert.That(motes.sortingOrder,Is.EqualTo(12));
        Assert.That(resonance.sortingOrder,Is.EqualTo(13));
        Assert.That(cloud.sortingOrder,Is.EqualTo(11));
        Assert.That(cloud.sortingOrder,Is.GreaterThan(decal.GetComponent<SpriteRenderer>().sortingOrder));
        for(int i=0;i<4;i++)
        {
            body.sprite=frames[i];gas.Pose(.7f);
            Assert.That(body.sprite,Is.SameAs(frames[i]));
            Assert.That(cloud.sortingOrder,Is.GreaterThan(body.sortingOrder));
        }
    }
}
