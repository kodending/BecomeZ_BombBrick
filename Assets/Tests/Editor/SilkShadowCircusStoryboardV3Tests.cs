using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SilkShadowCircusStoryboardV3Tests
{
    private GameObject parent;
    private Texture2D texture;
    private Sprite source;
    private Rank4CinematicStage stage;

    [SetUp]public void Setup()
    {
        parent=new GameObject("Silk shadow circus V3 invariant fixture");
        texture=new Texture2D(4,4);texture.SetPixels(Enumerable.Repeat(Color.white,16).ToArray());texture.Apply();
        source=Sprite.Create(texture,new Rect(0,0,4,4),Vector2.one*.5f,4f);
    }
    [TearDown]public void Cleanup()
    {stage?.Dispose();Object.DestroyImmediate(source);Object.DestroyImmediate(texture);Object.DestroyImmediate(parent);}

    private void Begin(string family)
    {
        switch(family)
        {
            case "spider":stage=new SpiderQueenRank4Stage(parent.transform);break;
            case "ninja_cat":stage=new NinjaSubstitutionRank4Stage(parent.transform);break;
            case "tyranno":stage=new TyrannoReignRank4Stage(parent.transform);break;
            default:stage=new MonkeyCircusRank4Stage(parent.transform);break;
        }
        var actor=new FrogCatchCinematicStage.Actor{species=family=="spider"?"aranea":family=="ninja_cat"?"chundongi":family=="tyranno"?"tino":"chiwoo",sprite=source,frames=new[]{source},color=Color.white,size=1f};
        Assert.IsTrue(stage.Begin(new Rank4StageContext{family=family,source=actor,targets=new[]{actor},returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f}));
    }
    [Test]public void NinjaSpacetimeContactCrossesOnceAndMatchesTheFieldReturn()
    {
        Begin("ninja_cat");float previous=-1f;int crossings=0;
        for(float time=0;time<=stage.Duration;time+=1f/120f)
        {
            stage.Tick(time,1f/120f);
            Assert.That(stage.ActionTime,Is.GreaterThanOrEqualTo(previous));
            if(previous<NinjaSpacetimeRules.Windup&&stage.ActionTime>=NinjaSpacetimeRules.Windup)crossings++;
            previous=stage.ActionTime;
        }
        Assert.That(crossings,Is.EqualTo(1));
        stage.Tick(NinjaSubstitutionRank4Stage.FilmImpactTime-.001f,0f);
        Assert.That(stage.ActionTime,Is.LessThan(NinjaSpacetimeRules.Windup));
        stage.Tick(NinjaSubstitutionRank4Stage.FieldReturnTime,0f);
        Assert.That(stage.ActionTime,Is.EqualTo(NinjaSpacetimeRules.Windup).Within(.0001f));
        Assert.That(parent.GetComponentInChildren<Camera>(true).backgroundColor.a,Is.Zero);
        stage.Tick(stage.Duration,0f);Assert.That(stage.ActionTime,Is.EqualTo(NinjaSpacetimeRules.Duration).Within(.0001f));
    }

    [TestCase("spider",5.75f)][TestCase("ninja_cat",3.95f)]
    [TestCase("tyranno",5.55f)][TestCase("monkey",4.03f)]
    public void TransparentReturnDisablesEverySetMeshAndNextBeginRestoresIt(string family,float returnTime)
    {
        Begin(family);stage.Tick(returnTime,0f);
        Camera camera=parent.GetComponentInChildren<Camera>(true);
        Assert.That(camera.backgroundColor.a,Is.Zero);
        Assert.That(parent.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled),Is.False,"Stage geometry must never remain over the actual field.");
        var context=stage.CurrentContext;Assert.IsTrue(stage.Begin(context));
        stage.Tick(family=="spider"?1.85f:family=="ninja_cat"?1.65f:family=="tyranno"?2.58f:.92f,0f);
        Assert.That(camera.backgroundColor.a,Is.EqualTo(1f));
        Assert.That(parent.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled&&r.name.StartsWith("Lead ",StringComparison.Ordinal)),Is.True);
    }

    [TestCase("spider")]
    public void PreparingPersistentEffectsCannotSpendChargesOrAdvanceTheirCombatClock(string family)
    {
        Begin(family);
        for(float time=0f;time<=stage.Duration;time+=.037f){stage.Tick(time,0f);Assert.That(stage.ActionTime,Is.EqualTo(-1f));}
    }

    [Test]public void TyrantContactCrossesTheRealWarningExactlyOnceAtTheBoardImpact()
    {
        Assert.That(TyrannoReignRank4Stage.CastTimeAt(.349f),Is.EqualTo(-1f));
        Assert.That(TyrannoReignRank4Stage.CastTimeAt(TyrannoReignRank4Stage.ImpactTime-.001f),Is.LessThan(FamilyTranscendenceRules.TyrannoStompWarning));
        Assert.That(TyrannoReignRank4Stage.CastTimeAt(TyrannoReignRank4Stage.ImpactTime),Is.EqualTo(FamilyTranscendenceRules.TyrannoStompWarning).Within(.00001f));
        Assert.That(TyrannoReignRank4Stage.CastTimeAt(20f),Is.EqualTo(FamilyTranscendenceRules.TyrannoStompEnd));
    }

    [TestCase("spider",1.85f)]
    public void EnemyFacingNeverDisconnectsTheNewActorFromItsUnflippedProps(string family,float time)
    {
        Begin(family);var context=stage.CurrentContext;context.enemy=true;
        var actor=context.source;actor.flipX=true;context.source=actor;Assert.IsTrue(stage.Begin(context));stage.Tick(time,0f);
        foreach(var body in parent.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled&&r.name.StartsWith("Lead ",StringComparison.Ordinal)))
            Assert.IsFalse(body.flipX,"The whole staged action follows the locked axis; the current field pose preserves facing on return.");
    }
}
