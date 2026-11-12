using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class SpiderCeilingCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private GameObject parent;
    private SpiderQueenRank4Stage stage;
    private Rank4StageContext context;
    private Camera view;

    [SetUp]public void Setup()
    {
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");Assert.That(database,Is.Not.Null);
        var definition=database.monsters.First(d=>d.monsterId=="aranea");
        var source=Actor(definition);parent=new GameObject("Spider ceiling contract fixture");
        stage=new SpiderQueenRank4Stage(parent.transform);
        context=new Rank4StageContext{family="spider",source=source,returnSprite=source.sprite,returnPosition=new Vector2(-2,-2),returnHeight=1.3f,
            targets=new[]{"woongi","velos","geomchi","tino"}.Select(id=>Actor(database.monsters.First(d=>d.monsterId==id))).ToArray()};
        for(int i=0;i<context.targets.Length;i++)context.targets[i].ground=new Vector2(.2f+i*.18f,-.5f+i*.3f);
        Assert.That(stage.Begin(context),Is.True);view=parent.GetComponentInChildren<Camera>(true);
    }
    [TearDown]public void Cleanup(){stage?.Dispose();Object.DestroyImmediate(parent);}

    [TestCase(2.20f)][TestCase(3.10f)][TestCase(3.70f)]
    public void CeilingCameraLooksExactlyDownOnTheGroundRatherThanTiltingASideSprite(float time)
    {
        stage.Tick(time,0f);
        Assert.That(view.orthographic,Is.False);
        Assert.That(view.fieldOfView,Is.EqualTo(28.0725f).Within(.01f));
        Assert.That(Vector3.Dot(view.transform.forward,Vector3.down),Is.GreaterThan(.99999f),"The user requested a true ceiling view, not an oblique field camera.");
        Assert.That(Vector3.Dot(view.transform.up,Vector3.forward),Is.GreaterThan(.99999f));
        var body=VisibleLead("Lead ceiling");
        Assert.That(body.sprite.name.StartsWith("CombatCinematics/AraneaCeilingV1/",StringComparison.Ordinal),Is.True);
        Assert.That(Vector3.Dot(body.transform.forward,view.transform.forward),Is.GreaterThan(.999f));
        float nearDepth=Vector3.Dot(body.bounds.center-view.transform.position,view.transform.forward);
        var distant=parent.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.name=="Ceiling spider distant floor shadow");
        float farDepth=Vector3.Dot(distant.bounds.center-view.transform.position,view.transform.forward);
        Assert.That(farDepth/nearDepth,Is.GreaterThan(2f),"The suspended caster must be physically closer to the lens than its ground shadow.");
        Assert.That(ProjectedBody(body).height,Is.GreaterThan(ProjectedBody(distant).height*2f));
        WithImage(image=>Assert.That(Array.TrueForAll(image.GetPixels32(),p=>p.a==255),Is.True,"The overhead ground must fully hide the original perspective map."));
    }

    [Test]public void UpwardWebPullTakesTheActualBodyOutsideTheTopBeforeTheCeilingCut()
    {
        stage.Tick(.20f,0f);var body=VisibleLead("Lead actual");
        Assert.That(body.sprite,Is.SameAs(context.source.sprite));
        float first=ProjectedBody(body).center.y;
        stage.Tick(1.12f,0f);body=VisibleLead("Lead actual");
        Assert.That(body.sprite.name,Does.StartWith(SpiderQueenRank4Stage.ResourceRoot+"Ascent "),"Climbing needs newly authored poses rather than moving a single combat frame.");
        Assert.That(ProjectedBody(body).center.y,Is.GreaterThan(first+.10f),"The actual caster must travel upward before changing the view.");
        stage.Tick(1.49f,0f);
        var remaining=parent.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled&&r.sprite!=null&&r.name.StartsWith("Lead actual",StringComparison.Ordinal)).ToArray();
        Assert.That(remaining.All(r=>ProjectedBody(r).yMin>1f),Is.True,"The body must climb out before the ceiling transition.");
        Assert.That(parent.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled&&r.name.StartsWith("Lead ceiling",StringComparison.Ordinal)),Is.False);
    }

    [TestCase(.20f)][TestCase(1.12f)][TestCase(5.75f)]
    public void FieldConnectedIntroductionAndReturnKeepTheUnderlyingMapVisible(float time)
    {
        stage.Tick(time,0f);Assert.That(view.backgroundColor.a,Is.Zero);
        WithImage(image=>Assert.That(image.GetPixel(0,359).a,Is.LessThan(.01f)));
    }

    [TestCase(false)][TestCase(true)]
    public void ReturnRestoresTheCurrentFieldFramePositionHeightAndFacing(bool enemy)
    {
        context.enemy=enemy;var source=context.source;source.flipX=enemy;context.source=source;
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.First(d=>d.monsterId=="aranea");
        var driver=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        context.returnSprite=driver.IdleFrames.Last();Assert.That(stage.Begin(context),Is.True);stage.Tick(5.75f,0f);
        var body=VisibleLead("Lead actual");Assert.That(body.sprite,Is.SameAs(context.returnSprite));Assert.That(body.flipX,Is.EqualTo(enemy));
        Bounds visible=VisibleBounds(body.sprite);
        Assert.That(visible.size.y*body.transform.localScale.y,Is.EqualTo(1.3f).Within(.0001f));
        Vector3 feet=body.transform.TransformPoint(new Vector3(enemy?-visible.center.x:visible.center.x,visible.min.y,0f));
        Vector3 local=parent.GetComponentInChildren<Camera>(true).transform.parent.InverseTransformPoint(feet);
        Assert.That(local.x,Is.EqualTo(-2f).Within(.001f));Assert.That(local.y,Is.EqualTo(-2f).Within(.001f));
        context.returnBodyVisible=false;stage.Tick(5.76f,0f);
        Assert.That(parent.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled&&r.name.StartsWith("Lead",StringComparison.Ordinal)),Is.False,"A dead or hidden body cannot be resurrected by the return overlay.");
    }

    [Test]public void OnlyNewCeilingArtworkContainsAllFourSpeciesActualShinyAndEightyEnemyCells()
    {
        var frames=stage.ArtworkFrames.ToArray();
        Assert.That(frames.All(s=>s.name.StartsWith("CombatCinematics/AraneaCeilingV1/",StringComparison.Ordinal)),Is.True);
        var motion=frames.Where(s=>s.name.StartsWith("CombatCinematics/AraneaCeilingV1/Motion ",StringComparison.Ordinal)).ToArray();
        var climb=frames.Where(s=>s.name.StartsWith("CombatCinematics/AraneaCeilingV1/Ascent ",StringComparison.Ordinal)).ToArray();
        var targets=frames.Where(s=>s.name.StartsWith("CombatCinematics/AraneaCeilingV1/EnemyTop ",StringComparison.Ordinal)).ToArray();
        var shinyTargets=frames.Where(s=>s.name.StartsWith("CombatCinematics/AraneaCeilingV1/EnemyTopShiny ",StringComparison.Ordinal)).ToArray();
        Assert.That(motion,Has.Length.EqualTo(40),"Four original rows and the actual ragon shiny need eight authored poses each.");
        Assert.That(climb,Has.Length.EqualTo(30),"All four species and actual shiny ragon need six dedicated climbing poses.");
        Assert.That(targets,Has.Length.EqualTo(80),"Every actual enemy species needs a true top view; sideways fallbacks change the perspective.");
        Assert.That(shinyTargets,Has.Length.EqualTo(80),"The actual shiny enemies also need the same dorsal projection.");
        var atlas=targets[0].texture;var target=RenderTexture.GetTemporary(atlas.width,atlas.height,0,RenderTextureFormat.ARGB32);
        var image=new Texture2D(atlas.width,atlas.height,TextureFormat.RGBA32,false);RenderTexture previous=RenderTexture.active;
        try
        {
            foreach(var group in motion.Concat(climb).Concat(targets).Concat(shinyTargets).GroupBy(s=>s.texture))
            {
                if(target.width!=group.Key.width||target.height!=group.Key.height)
                {
                    RenderTexture.active=previous;
                    RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);
                    target=RenderTexture.GetTemporary(group.Key.width,group.Key.height,0,RenderTextureFormat.ARGB32);
                    image=new Texture2D(group.Key.width,group.Key.height,TextureFormat.RGBA32,false);
                }
                Graphics.Blit(group.Key,target);RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                foreach(Sprite sprite in group)
                {
                    Rect r=sprite.rect;
                    Assert.That(image.GetPixels((int)r.x,(int)r.y,(int)r.width,(int)r.height).Count(p=>p.a>.125f),Is.GreaterThan(20),sprite.name+" is empty.");
                }
            }
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
    }

    [Test]public void ReadOnlyPresentationCannotAdvanceItsDomainOrOwnTheBattleClock()
    {
        float previous=Time.timeScale;
        try
        {
            Time.timeScale=0f;
            for(float time=0f;time<=stage.Duration;time+=1f/120f)
            {stage.Tick(time,1f/120f);Assert.That(stage.ActionTime,Is.EqualTo(-1f));Assert.That(Time.timeScale,Is.Zero);}
            stage.Tick(stage.Duration,0f);Assert.That(stage.Opacity,Is.LessThan(.01f));
        }
        finally{Time.timeScale=previous;}
    }

    [Test]public void EveryDatabaseEnemyHasItsOwnTopViewCellInsteadOfFallingBackToASidePose()
    {
        var definitions=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.Where(d=>d!=null).ToArray();
        Assert.That(definitions,Has.Length.EqualTo(80));
        var cells=definitions.Select(d=>SpiderQueenRank4Stage.TargetCell(d.monsterId)).ToArray();
        Assert.That(cells,Is.EquivalentTo(Enumerable.Range(0,80)),"Missing or shared cells would silently display a different species from above.");
        foreach(var definition in definitions)
        {
            int number;Assert.That(int.TryParse(definition.serialNumber,out number),Is.True);
            Assert.That(SpiderQueenRank4Stage.TargetCell(definition.monsterId),Is.EqualTo(number-1),definition.monsterId);
        }
        Assert.That(SpiderQueenRank4Stage.TargetCell("unknown-summon"),Is.EqualTo(-1),"A non-database summon must keep its own snapshot instead of being mislabeled as aranea.");
    }

    [Test]public void ActualShinyEnemiesUseTheirOwnTopViewRatherThanTheOriginalSideSnapshot()
    {
        var definitions=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters
            .Where(d=>d!=null&&Resources.Load<GameObject>("Enemies/Shiny/"+d.monsterId)!=null).ToArray();
        Assert.That(definitions.Length,Is.GreaterThan(0));
        var select=typeof(SpiderQueenRank4Stage).GetMethod("TargetSprite",Hidden);
        foreach(var definition in definitions)
        {
            var actor=Actor(definition);actor.shiny=true;
            var actual=Resources.Load<GameObject>("Enemies/Shiny/"+definition.monsterId).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            actor.sprite=actual.IdleFrames[0];actor.frames=actual.IdleFrames;
            var sprite=(Sprite)select.Invoke(stage,new object[]{actor});
            Assert.That(sprite.name,Does.StartWith("CombatCinematics/AraneaCeilingV1/EnemyTopShiny "),definition.monsterId);
            int index=SpiderQueenRank4Stage.TargetCell(definition.monsterId);
            Assert.That(sprite.rect.x,Is.EqualTo(index%8*(sprite.texture.width/8f)));
            Assert.That(sprite.rect.y,Is.EqualTo((9-index/8)*(sprite.texture.height/10f)));
        }
    }

    [Test]public void ReleasedWebStartsAtBothRealHandsAndReachesDistinctMapWideGroundContacts()
    {
        stage.Tick(3.70f,0f);
        var type=typeof(SpiderQueenRank4Stage);
        var left=(Vector3)type.GetField("releaseLeftHand",Hidden).GetValue(stage);
        var right=(Vector3)type.GetField("releaseRightHand",Hidden).GetValue(stage);
        var vertices=(Vector3[])type.GetField("threadVertices",Hidden).GetValue(stage);
        int count=(int)type.GetField("vertexCount",Hidden).GetValue(stage),fromLeft=0,fromRight=0;
        var root=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage);
        var ground=parent.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.name=="New vertical chamber floor").transform.parent;
        Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
        Assert.That(left.y,Is.GreaterThan(5f));Assert.That(right.y,Is.GreaterThan(5f));
        Assert.That(Vector3.Distance(left,right),Is.GreaterThan(.05f),"The fan must emerge from two visible hands, not one disconnected center point.");
        for(int i=0;i<count;i+=4)
        {
            Vector3 start=(vertices[i]+vertices[i+3])*.5f,end=(vertices[i+1]+vertices[i+2])*.5f;
            bool l=Vector3.Distance(start,left)<.005f,r=Vector3.Distance(start,right)<.005f;
            if(!l&&!r)continue;if(l)fromLeft++;if(r)fromRight++;
            Vector3 localEnd=ground.InverseTransformPoint(root.TransformPoint(end));
            Assert.That(localEnd.y,Is.EqualTo(.055f).Within(.015f),"Launch strands must land on the moving ground, not remain at the old floor height.");
            min=Vector3.Min(min,localEnd);max=Vector3.Max(max,localEnd);
        }
        Assert.That(fromLeft,Is.GreaterThanOrEqualTo(4));Assert.That(fromRight,Is.GreaterThanOrEqualTo(4));
        Assert.That(min.x,Is.LessThan(-6f));Assert.That(max.x,Is.GreaterThan(6f));
        Assert.That(min.z,Is.LessThan(-3f));Assert.That(max.z,Is.GreaterThan(3f));
    }

    [Test]public void RepeatedBeginReturnsToTheSameTransparentIntroductionWithoutOldOverheadGeometry()
    {
        for(int attempt=0;attempt<3;attempt++)
        {
            stage.Tick(3.2f,0f);Assert.That(view.backgroundColor.a,Is.EqualTo(1f));
            stage.Tick(5.75f,0f);
            Assert.That(parent.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled),Is.False);
            Assert.That(stage.Begin(context),Is.True);stage.Tick(.20f,0f);Assert.That(view.backgroundColor.a,Is.Zero);
            Assert.That(VisibleLead("Lead actual").sprite,Is.SameAs(context.source.sprite));
            Assert.That(parent.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled&&r.name.StartsWith("Lead ceiling",StringComparison.Ordinal)),Is.False);
        }
    }

    [Test]public void CeilingLandingUsesTheOriginalNeonDomainAndHidesItBeforeReturningToCombat()
    {
        stage.Tick(4.45f,0f);
        var effects=parent.GetComponentsInChildren<PixelSilkQueenDomain>(true);
        Assert.That(effects,Has.Length.EqualTo(1),"The ceiling should reuse the actual in-game effect, including its spider emblem.");
        var effect=effects[0];var renderer=effect.GetComponent<MeshRenderer>();
        Assert.That(effect.enabled,Is.False,"The read-only stage controls visual age without a second Update clock.");
        Assert.That(renderer.enabled,Is.True);
        Assert.That(effect.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.EqualTo(3120));
        Assert.That(renderer.sharedMaterial.color.a,Is.GreaterThan(.8f));
        Assert.That(stage.Begin(context),Is.True);stage.Tick(SpiderQueenRank4Stage.DomainRevealTime+.01f,0f);
        Assert.That(renderer.sharedMaterial.color.a,Is.LessThan(.1f),"Repeated casts must begin their visual reveal from zero.");
        stage.Tick(5.75f,0f);Assert.That(renderer.enabled,Is.False);
    }

    [Test]public void AscentKeepsNativePixelInformationAndUsesOneScaleForEveryClimbingPose()
    {
        Assert.That(stage.Texture.width,Is.EqualTo(1920));Assert.That(stage.Texture.height,Is.EqualTo(1080));
        context.returnHeight=.6f;Assert.That(stage.Begin(context),Is.True);stage.Tick(.20f,0f);
        var body=VisibleLead("Lead actual");
        float sourcePixels=VisibleBounds(body.sprite).size.y*body.sprite.pixelsPerUnit;
        Assert.That(ProjectedBody(body).height*stage.Texture.height,Is.GreaterThan(sourcePixels*.8f),
            "The old 86→24→72 pixel round trip discarded the legs and face before display.");
        Vector3 scale=Vector3.zero;var poses=new System.Collections.Generic.HashSet<Sprite>();
        foreach(float time in new[]{.40f,.70f,.95f,1.12f,1.35f})
        {
            stage.Tick(time,0f);body=VisibleLead("Lead actual");poses.Add(body.sprite);
            if(scale==Vector3.zero)scale=body.transform.localScale;
            Assert.That(body.transform.localScale,Is.EqualTo(scale),"Raised arms and folded legs must not resize the abdomen through per-frame bounding-box fitting.");
        }
        Assert.That(poses.Count,Is.GreaterThanOrEqualTo(4));
    }

    [Test]public void WholeArenaAndEnemiesPhysicallyRecedeWhileTheNearbyCasterKeepsItsSize()
    {
        var sprites=parent.GetComponentsInChildren<SpriteRenderer>(true);
        var floor=sprites.First(r=>r.name=="New vertical chamber floor");
        var target=sprites.First(r=>r.name=="Actual target dorsal body 0");
        stage.Tick(1.74f,0f);float firstFloor=ProjectedBody(floor).width,firstTarget=ProjectedBody(target).height;
        float leadHeight=ProjectedBody(VisibleLead("Lead ceiling")).height,firstY=floor.transform.position.y;
        stage.Tick(2.15f,0f);float middle=ProjectedBody(floor).width;
        stage.Tick(2.55f,0f);float last=ProjectedBody(floor).width;
        Assert.That(middle,Is.LessThan(firstFloor));Assert.That(last,Is.LessThan(middle));
        Assert.That(last/firstFloor,Is.LessThan(.6f),"A changing FOV must not cancel the ground's visible retreat.");
        Assert.That(last,Is.InRange(.46f,.50f));
        Assert.That(floor.transform.position.y,Is.LessThan(firstY-15f));
        Assert.That(ProjectedBody(target).height/firstTarget,Is.EqualTo(last/firstFloor).Within(.025f));
        Assert.That(ProjectedBody(VisibleLead("Lead ceiling")).height,Is.EqualTo(leadHeight).Within(.01f));
        Assert.That(target.transform.parent,Is.SameAs(floor.transform.parent));
        Assert.That(parent.GetComponentInChildren<PixelSilkQueenDomain>(true).transform.parent,Is.SameAs(floor.transform.parent));
    }

    private SpriteRenderer VisibleLead(string prefix)
    {
        var bodies=parent.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled&&r.sprite!=null&&r.name.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
        Assert.That(bodies,Has.Length.EqualTo(1),"Exactly one principal source body is required for "+prefix);return bodies[0];
    }
    private Rect ProjectedBody(SpriteRenderer actor)
    {
        Bounds body=VisibleBounds(actor.sprite);Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            Vector3 point=new Vector3(x==0?body.min.x:body.max.x,y==0?body.min.y:body.max.y,0f);if(actor.flipX)point.x=-point.x;
            Vector3 projected=view.WorldToViewportPoint(actor.transform.TransformPoint(point));Assert.That(projected.z,Is.GreaterThan(0f));
            min=Vector2.Min(min,projected);max=Vector2.Max(max,projected);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    private static Bounds VisibleBounds(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{sprite});
    private static FrogCatchCinematicStage.Actor Actor(MonsterDefinition definition)
    {
        var prefab=Resources.Load<GameObject>(definition.resourcePath);Assert.That(prefab,Is.Not.Null,definition.monsterId);
        var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        return new FrogCatchCinematicStage.Actor{species=definition.monsterId,sprite=driver.IdleFrames[0],frames=driver.IdleFrames,color=Color.white,size=1f};
    }
    private void WithImage(Action<Texture2D> inspect)
    {
        var image=new Texture2D(stage.Texture.width,stage.Texture.height,TextureFormat.RGBA32,false);RenderTexture previous=RenderTexture.active;
        try{view.Render();RenderTexture.active=(RenderTexture)stage.Texture;image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();inspect(image);}
        finally{RenderTexture.active=previous;Object.DestroyImmediate(image);}
    }
}
