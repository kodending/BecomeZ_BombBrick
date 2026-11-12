using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class GriffinStormOrbVisualTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private readonly List<PixelGriffinStormOrb> effects=new List<PixelGriffinStormOrb>();
    private GameObject host;
    private GameObject[] previousMainCameras;
    private Camera camera;
    private SpriteRenderer original;
    private GriffinCrosswindRank4Stage stage;
    private readonly Bounds arena=new Bounds(Vector3.zero,new Vector3(32f,10f,24f));

    [SetUp]public void Setup()
    {
        previousMainCameras=Resources.FindObjectsOfTypeAll<Camera>().Where(c=>c.gameObject.scene.IsValid()&&c.CompareTag("MainCamera"))
            .Select(c=>c.gameObject).ToArray();
        foreach(var existing in previousMainCameras)existing.tag="Untagged";
        host=new GameObject("Native storm orb visual tests");
        camera=new GameObject("Storm orb actual main camera").AddComponent<Camera>();camera.transform.SetParent(host.transform);
        camera.tag="MainCamera";camera.aspect=16f/9f;camera.fieldOfView=43f;camera.nearClipPlane=.1f;
        camera.transform.SetPositionAndRotation(new Vector3(2f,8f,-14f),Quaternion.Euler(28f,-8f,0f));
        var unrelated=new GameObject("Unrelated preview camera").AddComponent<Camera>();unrelated.transform.SetParent(host.transform);
        unrelated.transform.SetPositionAndRotation(new Vector3(45f,27f,-70f),Quaternion.Euler(5f,25f,0f));
        unrelated.orthographic=true;unrelated.orthographicSize=20f;
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.First(d=>d!=null&&d.monsterId=="pone");
        var authored=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        original=new GameObject("Original colored projectile").AddComponent<SpriteRenderer>();original.transform.SetParent(host.transform);
        original.sprite=authored.IdleFrames[0];original.color=new Color(.37f,.61f,.85f,.78f);
        original.flipX=true;original.flipY=true;original.transform.localScale=new Vector3(.17f,.23f,1f);
        Assert.That(Camera.main,Is.SameAs(camera));
    }

    [TearDown]public void Cleanup()
    {
        stage?.Dispose();
        foreach(var effect in effects)if(effect!=null)effect.Cancel();effects.Clear();
        if(host!=null)Object.DestroyImmediate(host);
        foreach(var existing in previousMainCameras)if(existing!=null)existing.tag="MainCamera";
    }

    private PixelGriffinStormOrb Create(bool allied=true)
    {var effect=PixelGriffinStormOrb.Create(original,arena,allied);effects.Add(effect);return effect;}
    private static SpriteRenderer Core(PixelGriffinStormOrb effect)=>Named(effect,"Eight drawing spherical wind");
    private static SpriteRenderer Burst(PixelGriffinStormOrb effect)=>Named(effect,"Eight drawing wind release");
    private static SpriteRenderer Shot(PixelGriffinStormOrb effect,int id)=>Named(effect,"Captured projectile "+id);
    private static SpriteRenderer Named(PixelGriffinStormOrb effect,string name)=>effect.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==name);

    [TestCase(true,false)][TestCase(false,false)][TestCase(true,true)][TestCase(false,true)]
    public void OrbStaysInItsTeamMenuGapAtElevenPercentHeightUsingTheActualMainCamera(bool allied,bool orthographic)
    {
        camera.orthographic=orthographic;camera.orthographicSize=6.5f;
        var effect=Create(allied);effect.CompleteCinematicEntrance();effect.Pose(0f);
        AssertCoreAnchor(effect,allied);
        camera.fieldOfView=58f;camera.orthographicSize=8f;camera.transform.position+=new Vector3(-2f,1f,3f);
        effect.Pose(0f);AssertCoreAnchor(effect,allied);
    }

    [TestCase(true)][TestCase(false)]
    public void CinematicAndNativeCoreMeetAtTheSameVisibleCenterSizeDrawingAndPhase(bool allied)
    {
        var effect=Create(allied);effect.Pose(.29f);effect.CompleteCinematicEntrance();
        var source=new FrogCatchCinematicStage.Actor{species="pone",sprite=original.sprite,color=Color.white,size=1f};
        var context=new Rank4StageContext{family="griffin",source=source,enemy=!allied,returnSprite=source.sprite,
            returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f,
            griffinOrbViewport=PixelGriffinStormOrb.ViewportAnchor(allied),griffinOrbHeight=PixelGriffinStormOrb.ViewportHeight};
        stage=new GriffinCrosswindRank4Stage(host.transform);Assert.That(stage.Begin(context),Is.True);
        stage.Tick(GriffinCrosswindRank4Stage.FieldReturnTime,0f);
        var filmCore=host.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Sovereign storm orb core");
        var filmCamera=host.GetComponentsInChildren<Camera>(true).Single(c=>c.targetTexture==stage.Texture);
        Assert.That(Core(effect).sprite.texture,Is.SameAs(filmCore.sprite.texture));Assert.That(Core(effect).sprite.rect,Is.EqualTo(filmCore.sprite.rect));
        Rect native=Projected(camera,Core(effect)),film=Projected(filmCamera,filmCore);
        Assert.That(Vector2.Distance(native.center,film.center),Is.LessThan(.0015f));Assert.That(Mathf.Abs(native.height-film.height),Is.LessThan(.0015f));
        Vector3 scale=Core(effect).transform.localScale;
        for(int i=0;i<8;i++)
        {effect.Pose((i+.1f)*.6f/8f);Assert.That(Vector3.Distance(Core(effect).transform.localScale,scale),Is.LessThan(.00001f));}
        effect.CompleteCinematicEntrance();Assert.That(Core(effect).sprite.rect,Is.EqualTo(filmCore.sprite.rect));
    }

    [Test]
    public void NativeOrbAndCounterattackPlayAllEightDrawingsAtTheirOwnTimes()
    {
        var effect=Create();effect.CompleteCinematicEntrance();
        var coreFrames=new HashSet<Rect>();var burstFrames=new HashSet<Rect>();
        for(int i=0;i<8;i++)
        {
            effect.Pose((i+.1f)*.6f/8f);Assert.That(Core(effect).enabled,Is.True);Assert.That(Burst(effect).enabled,Is.False);
            Assert.That(Core(effect).sprite.texture,Is.SameAs(Resources.Load<Texture2D>(PixelGriffinStormOrb.ResourceRoot+"Orb8")));
            coreFrames.Add(Core(effect).sprite.rect);
            effect.Pose(GriffinStormOrbRules.Burst+(i+.1f)*.48f/8f);
            Assert.That(Core(effect).enabled,Is.False);Assert.That(Burst(effect).enabled,Is.True);
            Assert.That(Burst(effect).sprite.texture,Is.SameAs(Resources.Load<Texture2D>(PixelGriffinStormOrb.ResourceRoot+"Burst8")));
            burstFrames.Add(Burst(effect).sprite.rect);
        }
        Assert.That(coreFrames.Count,Is.EqualTo(8));Assert.That(burstFrames.Count,Is.EqualTo(8));
        effect.Pose(GriffinStormOrbRules.End);Assert.That(Core(effect).enabled||Burst(effect).enabled,Is.False);
    }

    [Test]
    public void WindWrapsThroughRealSphereDepthAndOnlyTheOwnedPoseTimeAdvancesItsShape()
    {
        var effect=Create();effect.CompleteCinematicEntrance();effect.Pose(1.1f);
        var meshes=effect.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.GetComponent<MeshRenderer>()!=null).ToArray();
        Assert.That(meshes.Length,Is.GreaterThanOrEqualTo(2),"The wind needs visible portions before and behind the sphere.");
        var depths=new List<float>();bool front=false,behind=false;
        foreach(var mesh in meshes)
        {
            var renderer=mesh.GetComponent<MeshRenderer>();Assert.That(renderer.enabled,Is.True);
            Vector3[] vertices=mesh.sharedMesh.vertices;Color[] colors=mesh.sharedMesh.colors;var visibleDepths=new List<float>();
            for(int i=0;i<vertices.Length;i++)if(colors[i].a>.02f)
                visibleDepths.Add(Vector3.Dot(mesh.transform.TransformPoint(vertices[i])-effect.AnchorPosition,camera.transform.forward));
            Assert.That(visibleDepths,Is.Not.Empty);depths.AddRange(visibleDepths);
            if(renderer.sortingOrder>Core(effect).sortingOrder)
            {front=true;Assert.That(visibleDepths.Average(),Is.LessThan(0f),"Near-side wind must render in front of the core.");}
            else
            {behind=true;Assert.That(renderer.sortingOrder,Is.LessThan(Core(effect).sortingOrder));Assert.That(visibleDepths.Average(),Is.GreaterThan(0f));}
        }
        Assert.That(front&&behind,Is.True);Assert.That(depths.Max()-depths.Min(),Is.GreaterThan(effect.WorldHeight*.6f));
        Vector3[] first=WindPoints(meshes);effect.Pose(1.1f);Vector3[] same=WindPoints(meshes);
        Assert.That(first.Zip(same,(a,b)=>Vector3.Distance(a,b)).Max(),Is.LessThan(.000001f),"Rendering twice at the same battle time must not advance the wind.");
        effect.Pose(1.7f);Vector3[] later=WindPoints(meshes);
        Assert.That(first.Zip(later,(a,b)=>Vector3.Distance(a,b)).Max(),Is.GreaterThan(effect.WorldHeight*.1f),"A later battle pose must visibly turn the three-dimensional wind.");
    }

    private static Vector3[] WindPoints(IEnumerable<MeshFilter> meshes)=>meshes.SelectMany(mesh=>
        mesh.sharedMesh.vertices.Select(vertex=>mesh.transform.TransformPoint(vertex))).ToArray();

    [Test]
    public void CapturedProjectilesKeepTheirOriginalAppearanceThroughCurvedIntakeTiltedOrbitsAndRelease()
    {
        var effect=Create();effect.CompleteCinematicEntrance();
        Sprite sprite=original.sprite;Color color=original.color;Vector3 scale=original.transform.localScale;
        for(int i=0;i<3;i++)
        {
            Vector3 contact=effect.AnchorPosition+camera.transform.right*(7f+i)+camera.transform.up*(-5f+i);
            effect.Store(i,original,contact,0f);Assert.That(Vector3.Distance(effect.ShotPosition(i),contact),Is.LessThan(.0001f));
        }
        var depths=new List<float>();var frontSizes=new List<float>();var backSizes=new List<float>();
        var frontBrightness=new List<float>();var backBrightness=new List<float>();bool sawFront=false,sawBehind=false;
        for(float time=.4f;time<2.5f;time+=.17f)
        {
            effect.Pose(time);
            for(int i=0;i<3;i++)
            {
                SpriteRenderer stored=Shot(effect,i);AssertOrbitAppearance(stored,sprite,color);
                Assert.That(Vector3.Distance(stored.transform.position,effect.ShotPosition(i)),Is.LessThan(.0001f));
                depths.Add(Vector3.Dot(stored.transform.position-effect.AnchorPosition,camera.transform.forward));
                sawFront|=stored.sortingOrder>Core(effect).sortingOrder;sawBehind|=stored.sortingOrder<Core(effect).sortingOrder;
                float brightness=stored.color.r/color.r;
                if(stored.sortingOrder>Core(effect).sortingOrder)
                {frontSizes.Add(stored.transform.localScale.magnitude);frontBrightness.Add(brightness);}
                else
                {backSizes.Add(stored.transform.localScale.magnitude);backBrightness.Add(brightness);}
            }
        }
        Assert.That(depths.Max()-depths.Min(),Is.GreaterThan(effect.WorldHeight*.25f),"Stored rounds need depth, not one flat circular belt.");
        Assert.That(sawFront&&sawBehind,Is.True,"Orbiting rounds must pass both before and behind the sphere.");
        Assert.That(frontSizes.Average(),Is.GreaterThan(backSizes.Average()),"Near rounds must read larger than the far rounds.");
        Assert.That(frontBrightness.Average(),Is.GreaterThan(backBrightness.Average()),"Depth shading must brighten the near side without changing its original hue.");
        Vector3 start=effect.ShotPosition(0),target=start+camera.transform.right*5f-camera.transform.up*4f;
        for(int i=0;i<=5;i++)
        {
            float progress=i/5f;Vector3 position=GriffinStormOrbRules.ReturnPosition(start,target,progress,0,
                effect.ShotTangent(0),camera.transform.up,camera.transform.right,effect.WorldHeight*3f);
            effect.PoseReleased(0,position,target-position,progress);
            Assert.That(Vector3.Distance(effect.ShotPosition(0),position),Is.LessThan(.0001f));AssertOriginalAppearance(Shot(effect,0),sprite,color);
        }
        effect.Pose(3f);Assert.That(Vector3.Distance(effect.ShotPosition(0),target),Is.LessThan(.0001f),"A released round may not snap back onto its orbit.");
        Assert.That(original.sprite,Is.SameAs(sprite));Assert.That(original.color,Is.EqualTo(color));
        Assert.That(original.flipX&&original.flipY,Is.True);Assert.That(original.transform.localScale,Is.EqualTo(scale));
    }

    [Test]
    public void HiddenEntranceAlsoHidesNewlyCapturedRoundsAndCancellationRemovesEveryVisual()
    {
        var effect=Create();effect.SetCinematicHidden(true);effect.Store(7,original,effect.AnchorPosition+Vector3.right*3f,0f);
        effect.Pose(.5f);effect.CompleteCinematicEntrance();
        Assert.That(effect.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),Is.True);
        effect.SetCinematicHidden(false);effect.Pose(.5f);
        Assert.That(Core(effect).forceRenderingOff||Shot(effect,7).forceRenderingOff,Is.False);
        var rendered=effect.GetComponentsInChildren<Renderer>(true);var root=effect.gameObject;
        effect.Cancel();Assert.That(root==null,Is.True);Assert.That(rendered.All(r=>r==null),Is.True);
        var next=Create();next.CompleteCinematicEntrance();Assert.That(Core(next).enabled,Is.True);Assert.That(Core(next).sprite,Is.Not.Null);
    }

    private void AssertCoreAnchor(PixelGriffinStormOrb effect,bool allied)
    {
        Vector2 expected=new Vector2(allied?.55f:.76f,.85f);
        Vector3 anchor=camera.WorldToViewportPoint(effect.AnchorPosition);
        Assert.That(Vector2.Distance(new Vector2(anchor.x,anchor.y),expected),Is.LessThan(.0001f));
        Rect visible=Projected(camera,Core(effect));Assert.That(Vector2.Distance(visible.center,expected),Is.LessThan(.0015f));
        Assert.That(visible.height,Is.EqualTo(.11f).Within(.0015f));
    }
    private static void AssertOriginalAppearance(SpriteRenderer renderer,Sprite sprite,Color color)
    {Assert.That(renderer.sprite,Is.SameAs(sprite));Assert.That(renderer.color,Is.EqualTo(color));Assert.That(renderer.flipX&&renderer.flipY,Is.True);}
    private static void AssertOrbitAppearance(SpriteRenderer renderer,Sprite sprite,Color color)
    {
        Assert.That(renderer.sprite,Is.SameAs(sprite));Assert.That(renderer.flipX&&renderer.flipY,Is.True);
        float red=renderer.color.r/color.r,green=renderer.color.g/color.g,blue=renderer.color.b/color.b;
        Assert.That(red,Is.InRange(.57f,1.001f));Assert.That(green,Is.EqualTo(red).Within(.001f));Assert.That(blue,Is.EqualTo(red).Within(.001f));
        Assert.That(renderer.color.a/color.a,Is.InRange(.71f,1.001f));
    }
    private static Rect Projected(Camera camera,SpriteRenderer renderer)
    {
        Bounds body=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{renderer.sprite});
        Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            Vector3 point=new Vector3(x==0?body.min.x:body.max.x,y==0?body.min.y:body.max.y,0f);
            if(renderer.flipX)point.x=-point.x;if(renderer.flipY)point.y=-point.y;
            Vector3 view=camera.WorldToViewportPoint(renderer.transform.TransformPoint(point));
            Assert.That(view.z,Is.GreaterThan(0f));min=Vector2.Min(min,view);max=Vector2.Max(max,view);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
}
