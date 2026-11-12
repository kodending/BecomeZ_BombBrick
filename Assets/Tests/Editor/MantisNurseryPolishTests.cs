using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MantisNurseryPolishTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    private GameObject host;
    private MantisDeathMoltRank4Stage stage;
    private Rank4StageContext context;
    private Transform root;
    private Camera camera;
    private T Read<T>(string field)=>(T)typeof(MantisDeathMoltRank4Stage).GetField(field,Hidden).GetValue(stage);

    [SetUp]public void Setup()
    {
        host=new GameObject("Mantis fixed nursery test");
        context=new Rank4StageContext{family="mantis",source=Actor("mantius"),returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f};
        stage=new MantisDeathMoltRank4Stage(host.transform);Assert.That(stage.Begin(context),Is.True);
        root=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage);
        camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
    }
    [TearDown]public void Cleanup(){stage?.Dispose();if(host!=null)Object.DestroyImmediate(host);}

    private static FrogCatchCinematicStage.Actor Actor(string species,bool shiny=false)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        var prefab=Resources.Load<GameObject>(shiny?"Enemies/Shiny/"+species:definition.resourcePath);
        var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        return new FrogCatchCinematicStage.Actor{species=species,shiny=shiny,sprite=driver.IdleFrames[0],size=1f,color=Color.white};
    }
    private static Bounds Visible(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{sprite});
    private static uint Pixels(Sprite sprite)
    {
        var pixels=sprite.texture.GetPixels32();Rect rect=sprite.rect;uint hash=2166136261;
        for(int y=(int)rect.yMin;y<(int)rect.yMax;y++)for(int x=(int)rect.xMin;x<(int)rect.xMax;x++)
        {
            Color32 pixel=pixels[y*sprite.texture.width+x];
            unchecked{hash=(hash^pixel.r)*16777619;hash=(hash^pixel.g)*16777619;hash=(hash^pixel.b)*16777619;hash=(hash^pixel.a)*16777619;}
        }
        return hash;
    }

    [Test]public void OneTiledFloorAndPlantedCocoonRemainInTheSameWorldThroughTheDolly()
    {
        var floor=Read<MeshRenderer>("earth");var mesh=floor.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh.vertices.Min(v=>v.x),Is.EqualTo(-30f));Assert.That(mesh.vertices.Max(v=>v.x),Is.EqualTo(30f));
        Assert.That(mesh.uv.Max(uv=>uv.x),Is.EqualTo(5f));Assert.That(mesh.uv.Max(uv=>uv.y),Is.EqualTo(5f));
        Assert.That(floor.sharedMaterial.mainTexture.wrapMode,Is.EqualTo(TextureWrapMode.Repeat));
        Assert.That(floor.sharedMaterial.shader.name,Is.EqualTo("Hidden/Rank4V3MantisNurseryFloor"));
        Vector3 floorPosition=floor.transform.position;Sprite reference=Read<Sprite[]>("cocoons")[3];
        foreach(float time in new[]{.90f,1.48f,2.65f,3.12f})
        {
            stage.Tick(time,0f);var parent=Read<SpriteRenderer>("lead");Bounds closed=Visible(reference);
            Vector3 feet=root.InverseTransformPoint(parent.transform.TransformPoint(new Vector3(closed.center.x,closed.min.y,0f)));
            Assert.That(feet.magnitude,Is.LessThan(.001f),"A moving camera must not relocate the physical cocoon through FloorPoint.");
            Assert.That(floor.transform.position,Is.EqualTo(floorPosition));
            Assert.That(camera.transform.localPosition.y,Is.InRange(6.7f,7.4f));
            float down=Vector3.Angle(camera.transform.localRotation*Vector3.forward,Vector3.down);
            Assert.That(down+camera.fieldOfView*.5f,Is.LessThan(89f),"The top of the view must hit the ground rather than expose a hard floor edge.");
        }
        Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(sprite=>sprite.sprite!=null&&sprite.sprite.name.Contains("NurseryKit")),Is.False,
            "The former earth sticker and triangular shaft must not remain in the rebuilt set.");
    }

    [TestCase("mantius",false,0)][TestCase("flotis",false,1)][TestCase("santius",false,2)]
    [TestCase("nectis",false,3)][TestCase("flotis",true,4)]
    public void ParentFormsWithFourActualFramesWhileChildrenFollowTheRealNormalSpecies(string species,bool shiny,int row)
    {
        context.source=Actor(species,shiny);Assert.That(stage.Begin(context),Is.True);var seen=new System.Collections.Generic.HashSet<Sprite>();
        foreach(float time in new[]{.32f,.50f,.69f,.90f})
        {
            stage.Tick(time,0f);var parent=Read<SpriteRenderer>("lead");Assert.That(parent.enabled,Is.True);
            Assert.That(parent.sprite.name,Does.StartWith(MantisDeathMoltRank4Stage.ResourceRoot+"CocoonMotion "+row+":"));seen.Add(parent.sprite);
        }
        Assert.That(seen.Count,Is.EqualTo(4));Assert.That(seen.Select(Pixels).Distinct().Count(),Is.EqualTo(4),"The four formation cells must contain different pixels, not four copies of one still.");
        stage.Tick(2.65f,0f);
        var babies=Read<SpriteRenderer[]>("baby");int childRow=row==4?1:row;
        Assert.That(babies.All(child=>child.enabled),Is.True);
        foreach(var child in babies)Assert.That(child.sprite.name,Does.Contain("Motion "+childRow+":"));
        Assert.That(babies.Select(child=>child.sprite).Distinct().Count(),Is.EqualTo(3),"Staggered births must not share one global walking pose.");
    }

    [Test]public void EachChildGetsAtLeastFourDistinctPosesWithoutScalingIntoExistence()
    {
        var children=Read<SpriteRenderer[]>("baby");
        for(int child=0;child<3;child++)
        {
            float birth=2.12f+child*.24f;var frames=new System.Collections.Generic.HashSet<Sprite>();Vector3 scale=Vector3.zero;
            foreach(float age in new[]{.04f,.13f,.22f,.31f,.40f,.49f})
            {
                stage.Tick(birth+age,0f);Assert.That(children[child].enabled,Is.True);frames.Add(children[child].sprite);
                if(scale!=Vector3.zero)Assert.That(children[child].transform.localScale,Is.EqualTo(scale));
                scale=children[child].transform.localScale;
            }
            Assert.That(frames.Count,Is.GreaterThanOrEqualTo(4));
        }
    }

    [Test]public void OriginalActionBirthsAndNativeReturnRemainUnchangedWithoutALastFrameCameraSnap()
    {
        foreach(var cue in new[]{new Vector2(2.12f,.32f),new Vector2(2.36f,.44f),new Vector2(2.60f,.56f),new Vector2(3.62f,.90f)})
        {stage.Tick(cue.x,0f);Assert.That(stage.ActionTime,Is.EqualTo(cue.y).Within(.00001f));}
        Assert.That(stage.Duration,Is.EqualTo(4.4f));Assert.That(MantisDeathMoltRank4Stage.FieldReturnTime,Is.EqualTo(3.98f));
        stage.Tick(3.97f,0f);Vector3 position=camera.transform.localPosition;Quaternion rotation=camera.transform.localRotation;
        context.returnBodyVisible=false;stage.Tick(3.98f,0f);
        Assert.That(camera.transform.localPosition,Is.EqualTo(position));Assert.That(Quaternion.Angle(rotation,camera.transform.localRotation),Is.LessThan(.001f));
        Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(sprite=>sprite.enabled),Is.False);
        Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Any(mesh=>mesh.enabled),Is.False);
        Assert.That(camera.backgroundColor.a,Is.Zero);Assert.That(stage.Opacity,Is.Zero);
        stage.Tick(4.02f,0f);Assert.That(stage.ActionTime,Is.EqualTo(.90f).Within(.00001f));
        Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Any(sprite=>sprite.enabled),Is.False);
        Assert.That(stage.Begin(context),Is.True);stage.Tick(.28f,0f);Assert.That(camera.backgroundColor.a,Is.EqualTo(1f));
        Assert.That(camera.projectionMatrix.m02,Is.Zero.Within(.00001f));Assert.That(camera.projectionMatrix.m12,Is.Zero.Within(.00001f));
    }

    [Test]public void ClosedParentMatchesTheRealCocoonFootAndHeightBeforeItsFadeWithoutMovingInTheWorld()
    {
        stage.Tick(3.94f,0f);SpriteRenderer parent=Read<SpriteRenderer>("lead");
        Bounds closed=Visible(Read<Sprite[]>("cocoons")[3]);
        Vector3 foot=parent.transform.TransformPoint(new Vector3(closed.center.x,closed.min.y,0f));
        Vector3 top=parent.transform.TransformPoint(new Vector3(closed.center.x,closed.max.y,0f));
        Assert.That(root.InverseTransformPoint(foot).magnitude,Is.LessThan(.001f));
        Vector3 viewport=camera.WorldToViewportPoint(foot);
        Assert.That(viewport.x,Is.EqualTo(.5f+context.returnPosition.x/16f).Within(.002f));
        Assert.That(viewport.y,Is.EqualTo(.5f+context.returnPosition.y/9f).Within(.002f));
        Assert.That(camera.WorldToViewportPoint(top).y-viewport.y,Is.EqualTo(context.returnHeight*.82f/9f).Within(.002f));
        Assert.That(parent.color.a,Is.GreaterThan(.4f),"The matched cocoon remains visible while the native body is still hidden.");
    }
}
