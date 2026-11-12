using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SanctuarySkyStoryboardV3Tests
{
    private GameObject parent;
    private Texture2D texture;
    private Sprite sprite;
    private Rank4CinematicStage stage;
    [SetUp]public void Setup()
    {
        parent=new GameObject("Sanctuary storyboard test");
        texture=new Texture2D(4,4);texture.SetPixels(Enumerable.Repeat(Color.white,16).ToArray());texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0,0,4,4),new Vector2(.5f,.5f),4);
    }
    [TearDown]public void Cleanup()
    {stage?.Dispose();Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);Object.DestroyImmediate(parent);}
    private Rank4CinematicStage Make(string family)
    {
        switch(family)
        {case "spirit_deer":return new SpiritGuardianRank4Stage(parent.transform);case "griffin":return new GriffinCrosswindRank4Stage(parent.transform);
         case "beetle":return new BeetleOverturnRank4Stage(parent.transform);
         case "wyvern":return new WyvernHeavenRank4Stage(parent.transform);default:return new HaetaeMandateRank4Stage(parent.transform);}
    }
    private Rank4StageContext Context(string family,string species,bool shiny=false)
    {
        var actor=new FrogCatchCinematicStage.Actor{species=species,sprite=sprite,frames=new[]{sprite},color=Color.white,size=1f,shiny=shiny};
        return new Rank4StageContext{family=family,source=actor,allies=new[]{actor},targets=new[]{actor},returnPosition=new Vector2(-2,-2),returnHeight=1.3f};
    }
    private SpriteRenderer Lead()=>parent.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.name.StartsWith("Lead "));
    [TestCase("spirit_deer","holydeer")][TestCase("griffin","pone")][TestCase("haetae","haeta")]
    public void PassiveFilmNeverAdvancesOrRepeatsCombatAction(string family,string species)
    {
        stage=Make(family);Assert.IsTrue(stage.Begin(Context(family,species)));
        for(float t=0;t<stage.Duration;t+=.035f){stage.Tick(t,.035f);Assert.AreEqual(-1f,stage.ActionTime);}
    }
    [Test]public void HeavenFilmNeverConsumesTheOriginalFieldShadowFallOrImpact()
    {
        stage=Make("wyvern");stage.Begin(Context("wyvern","vivre"));
        var heard=new bool[4];int previous=-1;
        for(float t=0;t<=stage.Duration;t+=.01f)
        {
            stage.Tick(t,.01f);Assert.AreEqual(-1f,stage.ActionTime);Assert.That(stage.SoundBeat,Is.InRange(0,3));
            Assert.GreaterOrEqual(stage.SoundBeat,previous);heard[stage.SoundBeat]=true;previous=stage.SoundBeat;
        }
        Assert.That(heard.All(beat=>beat),Is.True,"Launch wing, preparation, compression and pass wing must each occur in order.");
        int[] expected={0,1,1,2,3,3};
        for(int i=0;i<WyvernHeavenRank4Stage.BoardTimes.Length;i++)
        {stage.Tick(WyvernHeavenRank4Stage.BoardTimes[i],0);Assert.AreEqual(-1f,stage.ActionTime);Assert.AreEqual(expected[i],stage.SoundBeat);}
        stage.Tick(stage.Duration,0);Assert.AreEqual(-1f,stage.ActionTime);
    }
    [Test]public void HeavenFilmFinishesBeforeAnyFieldShadowFallOrContact()
    {
        stage=Make("wyvern");stage.Begin(Context("wyvern","vivre"));
        foreach(float time in WyvernHeavenRank4Stage.BoardTimes)
        {
            stage.Tick(time,0);
            Assert.That(stage.ActionTime,Is.EqualTo(-1f));
            Assert.That(parent.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled&&
                (r.name=="Vertical waterfall breath"||r.name=="Single arena contact"||r.name.StartsWith("Actual heaven target "))),Is.False);
        }
    }
    [TestCase("spirit_deer","holydeer",3.85f)][TestCase("griffin","pone",4.12f)]
    public void ReturnUsesTransparentOverlayAndActualCurrentPose(string family,string species,float time)
    {
        stage=Make(family);var context=Context(family,species);stage.Begin(context);stage.Tick(time,0);
        var camera=parent.GetComponentInChildren<Camera>(true);Assert.AreEqual(0f,camera.backgroundColor.a);
        Assert.AreSame(sprite,Lead().sprite);Assert.IsTrue(Lead().enabled);
        context.returnBodyVisible=false;stage.Tick(time,0);Assert.IsFalse(Lead().enabled);
    }
    [TestCase("spirit_deer","lunadeer",1)][TestCase("spirit_deer","forestdeer",2)][TestCase("spirit_deer","eclipsedeer",3)]
    [TestCase("griffin","griphonas",1)][TestCase("griffin","holophonas",2)][TestCase("griffin","entpone",3)]
    [TestCase("beetle","noepung",1)][TestCase("beetle","atlagon",2)][TestCase("beetle","solarbeetle",3)]
    [TestCase("haetae","yeomhae",1)][TestCase("haetae","geumgangsu",2)][TestCase("haetae","mukryeong",3)]
    public void ActualSpeciesIdSelectsItsOwnRedrawnBodyRow(string family,string species,int row)
    {
        stage=Make(family);stage.Begin(Context(family,species));stage.Tick(family=="spirit_deer"?1.60f:.24f,0);
        if(family=="beetle")
        {
            float cell=Lead().sprite.texture.height/10f;
            Assert.That(Lead().sprite.rect.y/cell,Is.InRange(8-row*2,9-row*2));
        }
        else if(family=="griffin")
        {
            stage.Tick(.75f,0);
            float cell=Lead().sprite.texture.height/15f;
            Assert.That(Lead().sprite.rect.y/cell,Is.InRange(12-row*3,14-row*3));
        }
        else Assert.AreEqual((Lead().sprite.texture.height/5f)*(4-row),Lead().sprite.rect.y,.001f);
    }
    [TestCase("spirit_deer","forestdeer")][TestCase("griffin","holophonas")][TestCase("haetae","haeta")][TestCase("beetle","jangpung")]
    public void ActualShinyUsesItsDedicatedFifthRow(string family,string species)
    {
        stage=Make(family);stage.Begin(Context(family,species,true));stage.Tick(family=="spirit_deer"?1.60f:.24f,0);
        if(family=="griffin")
        {
            stage.Tick(.75f,0);
            Assert.That(Lead().sprite.rect.y,Is.InRange(0f,Lead().sprite.texture.height/15f*2));
        }
        else if(family=="beetle")Assert.That(Lead().sprite.rect.y,Is.InRange(0f,Lead().sprite.texture.height/10f));
        else Assert.AreEqual(0f,Lead().sprite.rect.y,.001f);
    }
    [Test]public void WaterProtectionUsesActualAlliedSnapshot()
    {
        stage=Make("spirit_deer");var context=Context("spirit_deer","holydeer");stage.Begin(context);stage.Tick(2.50f,0);
        var ally=parent.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.name=="Protected ally 0");
        Assert.IsTrue(ally.enabled);Assert.AreSame(sprite,ally.sprite);
    }
    [Test]public void WaterSurfaceHasRealUnderwaterPerspectiveCamera()
    {
        stage=Make("spirit_deer");stage.Begin(Context("spirit_deer","holydeer"));stage.Tick(.8f,0);
        var camera=parent.GetComponentInChildren<Camera>(true);Assert.IsFalse(camera.orthographic);
        Assert.Greater(Vector3.Dot(camera.transform.forward,Vector3.up),.45f);
    }
    [Test]public void OverturnClockHasSingleLaunchLandingAndExactOriginalEnd()
    {
        stage=Make("beetle");stage.Begin(Context("beetle","jangpung"));float last=-1f;int launchCrossings=0,landingCrossings=0;
        for(float t=0;t<=stage.Duration;t+=.01f)
        {
            stage.Tick(t,.01f);Assert.GreaterOrEqual(stage.ActionTime,last);
            if(last<BeetleOverturnRules.Warning&&stage.ActionTime>=BeetleOverturnRules.Warning)launchCrossings++;
            if(last<BeetleOverturnRules.Landing&&stage.ActionTime>=BeetleOverturnRules.Landing)landingCrossings++;
            last=stage.ActionTime;Assert.AreEqual(-1,stage.SoundBeat);
        }
        Assert.AreEqual(1,launchCrossings);Assert.AreEqual(1,landingCrossings);
        stage.Tick(2.90f,0);Assert.AreEqual(BeetleOverturnRules.Landing,stage.ActionTime,.000001f);
        stage.Tick(3.42f,0);Assert.AreEqual(BeetleOverturnRules.End,stage.ActionTime,.000001f);
    }
    [Test]public void ImperialHornRaisesALocalGroundWaveAndReturnsToTheActualFieldPose()
    {
        stage=Make("beetle");stage.Begin(Context("beetle","jangpung"));
        var renderer=parent.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Flexible local battlefield wave");
        stage.Tick(.28f,0);Vector3[] before=renderer.GetComponent<MeshFilter>().sharedMesh.vertices;
        stage.Tick(1.62f,0);Vector3[] after=renderer.GetComponent<MeshFilter>().sharedMesh.vertices;
        Assert.That(before.Where((vertex,index)=>Vector3.Distance(vertex,after[index])>.5f).Any(),Is.True);
        stage.Tick(3.93f,0);var camera=parent.GetComponentInChildren<Camera>(true);
        Assert.IsFalse(renderer.enabled);Assert.IsTrue(camera.orthographic);Assert.AreEqual(0,camera.backgroundColor.a);
        Assert.AreSame(sprite,Lead().sprite);
    }
}
