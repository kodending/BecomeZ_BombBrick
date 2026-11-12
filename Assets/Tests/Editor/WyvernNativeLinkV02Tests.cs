using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class WyvernNativeLinkV03Tests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private const string ArtRoot="CombatCinematics/Rank4V3/wyvern/NativeLinkV03/";
    private static readonly float[] Boundaries={0f,.60f,1.45f,2.25f,3.10f,3.65f,4.40f};
    private static readonly string[] PhaseAtlases={"Takeoff40","Takeoff40","Hunt40","Hunt40","Pass40","Pass40"};
    private WyvernHeavenRank4Stage stage;
    private Rank4StageContext context;
    private Transform root;
    private Camera camera;
    private Texture2D sourceTexture,fieldTexture;
    private Sprite sourceSprite,fieldSprite;

    [SetUp] public void Setup()
    {
        sourceTexture=new Texture2D(4,4);sourceTexture.SetPixels(Enumerable.Repeat(Color.white,16).ToArray());sourceTexture.Apply();
        sourceSprite=Sprite.Create(sourceTexture,new Rect(0,0,4,4),Vector2.one*.5f,4);
        fieldTexture=new Texture2D(16,9);fieldTexture.SetPixels(Enumerable.Range(0,144).Select(i=>new Color(i%16/16f,i/16/9f,.25f,1f)).ToArray());fieldTexture.Apply();
        fieldSprite=Sprite.Create(fieldTexture,new Rect(0,0,16,9),Vector2.one*.5f,16);
        var actor=new FrogCatchCinematicStage.Actor{species="vivre",sprite=sourceSprite,frames=new[]{sourceSprite},color=Color.white,size=1f};
        context=new Rank4StageContext{family="wyvern",source=actor,allies=new[]{actor},targets=new[]{actor},
            battlefieldBackdrop=sourceTexture,wyvernBattlefieldSprite=fieldSprite,returnSprite=sourceSprite,returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f};
        stage=new WyvernHeavenRank4Stage(null);Assert.That(stage.Begin(context),Is.True);
        root=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage);
        camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
    }
    [TearDown] public void Cleanup()
    {stage?.Dispose();Object.DestroyImmediate(sourceSprite);Object.DestroyImmediate(sourceTexture);Object.DestroyImmediate(fieldSprite);Object.DestroyImmediate(fieldTexture);}

    [Test]
    public void CapturedFieldUsesACompiledApertureAndRestoresTheWholeViewportBeforeReturning()
    {
        context.wyvernBattlefieldViewportRect=new Rect(.1f,.2f,.7f,.45f);Assert.That(stage.Begin(context),Is.True);
        var field=root.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Actual captured battlefield");
        stage.Tick(1.88f,0f);var material=field.sharedMaterial;
        Assert.That(material.shader.name,Is.EqualTo("Hidden/WyvernFieldAperture"));
        Assert.That(material.shader.isSupported,Is.True,"An unsupported shader produces an invalid magenta battlefield.");
        Assert.That(material.GetVector("_ArenaRect"),Is.EqualTo(new Vector4(.1f,.2f,.7f,.45f)));
        Assert.That(material.GetFloat("_Aperture"),Is.EqualTo(1f));
        stage.Tick(WyvernHeavenRank4Stage.FieldRevealTime,0f);
        Assert.That(material.GetFloat("_Aperture"),Is.EqualTo(0f));
    }

    [TestCase("Takeoff40",8,5,true)] [TestCase("Hunt40",8,5,true)] [TestCase("Pass40",8,5,true)]
    [TestCase("Sky4",2,2,false)] [TestCase("Cloud4",2,2,true)]
    public void EveryPrimaryAnimationHasDistinctAuthoredFramesAndTheCorrectImportAndAlpha(string name,int columns,int rows,bool cutout)
    {
        var texture=Resources.Load<Texture2D>(ArtRoot+name);Assert.That(texture,Is.Not.Null);
        var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Resources/"+ArtRoot+name+".png");
        Assert.That(importer,Is.Not.Null);Assert.That(importer.textureCompression,Is.EqualTo(UnityEditor.TextureImporterCompression.Uncompressed));
        Assert.That(texture.isReadable,Is.True);Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(texture.mipmapCount,Is.EqualTo(1));
        Assert.That(texture.width%columns,Is.Zero);Assert.That(texture.height%rows,Is.Zero);Assert.That(texture.width/columns,Is.GreaterThan(0));
        if(rows==5)Assert.That(texture.width/columns,Is.EqualTo(texture.height/rows),"Authored poses share square logical cells.");
        var frames=stage.ArtworkFrames.Where(s=>s.name.StartsWith(ArtRoot+name+" ",StringComparison.Ordinal)).ToArray();
        Assert.That(frames,Has.Length.EqualTo(columns*rows));var hashes=new HashSet<uint>();
        foreach(Sprite frame in frames)
        {
            Rect rect=frame.rect;var pixels=texture.GetPixels((int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height);
            uint hash=2166136261;int clear=0,solid=0;
            foreach(Color pixel in pixels)
            {
                Color32 c=pixel;if(c.a<8)clear++;if(c.a>240)solid++;
                unchecked{hash=(hash^c.r)*16777619;hash=(hash^c.g)*16777619;hash=(hash^c.b)*16777619;hash=(hash^c.a)*16777619;}
            }
            Assert.That(hashes.Add(hash),Is.True,frame.name+": repeated drawings are not additional animation frames.");
            Assert.That(solid,Is.GreaterThan(pixels.Length*(cutout?.01f:.99f)),frame.name);
            if(cutout)Assert.That(clear,Is.GreaterThan(pixels.Length*.01f),frame.name+": creatures and clouds need real alpha.");
        }
    }

    [TestCase("vivre",false,0)] [TestCase("igvire",false,1)] [TestCase("frovire",false,2)]
    [TestCase("temvire",false,3)] [TestCase("vivre",true,4)]
    public void EverySpeciesAndExistingShinyUsesItsOwnRowInAllSixPhases(string species,bool shiny,int row)
    {
        var actor=context.source;actor.species=species;actor.shiny=shiny;context.source=actor;Assert.That(stage.Begin(context),Is.True);
        for(int phase=0;phase<PhaseAtlases.Length;phase++)
        {
            stage.Tick(WyvernHeavenRank4Stage.BoardTimes[phase],0f);string prefix=ArtRoot+PhaseAtlases[phase]+" ";
            var renderers=VisibleSprites().Where(r=>r.sprite.name.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            Assert.That(renderers,Is.Not.Empty,species+"/phase "+phase);
            foreach(var renderer in renderers)
            {
                Sprite sprite=renderer.sprite;float cell=sprite.texture.height/5f;
                Assert.That(sprite.rect.y,Is.EqualTo(cell*(4-row)).Within(.001f),species+"/phase "+phase);
                Assert.That(Column(sprite),Is.InRange(phase%2*4,phase%2*4+3),"Each phase uses its own authored half of the atlas.");
            }
        }
    }

    [Test]
    public void PlaybackUsesFourAuthoredPosesInEachOfTheSixPhasesAndAllEnvironmentFrames()
    {
        for(int phase=0;phase<PhaseAtlases.Length;phase++)
        {
            var seen=new HashSet<Sprite>();string prefix=ArtRoot+PhaseAtlases[phase]+" ";
            for(float time=Boundaries[phase]+.001f;time<Boundaries[phase+1];time+=.005f)
            {
                stage.Tick(time,0f);
                foreach(var renderer in VisibleSprites())
                    if(renderer.sprite.name.StartsWith(prefix,StringComparison.Ordinal)&&Column(renderer.sprite)/4==phase%2)seen.Add(renderer.sprite);
            }
            Assert.That(seen.Count,Is.GreaterThanOrEqualTo(4),"Phase "+phase+": all four genuine poses must appear during playback.");
        }
        foreach(string name in new[]{"Sky4","Cloud4"})
        {
            var seen=new HashSet<Sprite>();
            for(float time=0f;time<stage.Duration;time+=.02f)
            {stage.Tick(time,0f);foreach(var renderer in VisibleSprites())if(renderer.sprite.name.StartsWith(ArtRoot+name+" ",StringComparison.Ordinal))seen.Add(renderer.sprite);}
            Assert.That(seen.Count,Is.EqualTo(4),name+": all imported environment frames must appear.");
        }
    }

    [Test]
    public void TheSixReviewedMomentsFollowTheSeparateWarningAndEndBeforeTheNativeCast()
    {
        Assert.That(stage.Duration,Is.EqualTo(4.4f));
        Assert.That(WyvernHeavenRank4Stage.BoardTimes,Is.EqualTo(new[]{.38f,1.05f,1.88f,2.72f,3.40f,4.17f}));
        for(int i=0;i<WyvernHeavenRank4Stage.BoardTimes.Length;i++)
        {stage.Tick(WyvernHeavenRank4Stage.BoardTimes[i],0f);Assert.That(stage.Shot,Is.EqualTo(i));}
        foreach(float time in new[]{.61f,1.05f,1.88f,2.72f,3.40f,4.17f})
        {stage.Tick(time,0f);Assert.That(stage.Opacity,Is.EqualTo(1f));Assert.That(camera.backgroundColor.a,Is.EqualTo(1f));}
        stage.Tick(stage.Duration,0f);Assert.That(camera.backgroundColor.a,Is.Zero);Assert.That(stage.Opacity,Is.Zero);Assert.That(stage.ActionTime,Is.EqualTo(-1f));
    }

    [Test]
    public void TheFilmContainsNoSecondWarningWingWipeNativeShadowFallingBreathOrImpact()
    {
        string[] rejected={"Wing occlusion","Inside wing shade","Vertical waterfall breath","Single arena contact","Irregular cloud ground shadow",
            "Breath from the actual open mouth","White core from the actual open mouth"};
        for(float time=0f;time<=stage.Duration;time+=1f/60f)
        {
            stage.Tick(time,0f);Assert.That(stage.ActionTime,Is.EqualTo(-1f));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t=>rejected.Contains(t.name)||t.name.StartsWith("Actual heaven target ",StringComparison.Ordinal)||
                t.name.IndexOf("warning",StringComparison.OrdinalIgnoreCase)>=0),Is.False);
        }
        Assert.That(stage.ArtworkFrames.Any(s=>s.name.Contains("NativeLinkV02/")),Is.False,"The rejected flight/wing-wipe atlas cannot supply v03.");
    }

    [Test]
    public void TheOpeningPreservesTheActualSourceAndTheCapturedBattlefieldStaysBorrowed()
    {
        var actor=context.source;actor.flipX=true;actor.color=new Color(.65f,.81f,.92f,.87f);context.source=actor;
        Assert.That(stage.Begin(context),Is.True);stage.Tick(0f,0f);Assert.That(camera.backgroundColor.a,Is.Zero);Assert.That(stage.Opacity,Is.EqualTo(1f));
        var lead=Lead();Assert.That(lead.enabled,Is.True);Assert.That(lead.sprite,Is.SameAs(sourceSprite));
        Assert.That(lead.flipX,Is.True);Assert.That(lead.color,Is.EqualTo(actor.color));
        var field=root.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Actual captured battlefield");
        stage.Tick(.38f,0f);
        Assert.That(field.enabled,Is.True);Assert.That(field.sprite,Is.SameAs(fieldSprite),"Reuse the actual captured map and monsters.");
        context.returnBodyVisible=false;stage.Tick(0f,0f);Assert.That(lead.enabled,Is.False,"A hidden caster cannot be revived by the field snapshot.");
        stage.Tick(4.35f,0f);Assert.That(field.enabled,Is.True);Assert.That(field.sprite,Is.SameAs(fieldSprite));
        stage.Dispose();Assert.That(fieldSprite!=null&&fieldTexture!=null,Is.True,"Only presentation owns the captured battlefield.");
    }

    [TestCase(false)] [TestCase(true)]
    public void CameraAndLogicalActorPlacementAreContinuousAcrossEveryPhaseBoundary(bool enemy)
    {
        context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
        foreach(float boundary in Boundaries.Skip(1).Take(5))
        {
            stage.Tick(boundary-.001f,0f);var lead=Lead();Assert.That(lead.enabled,Is.True);
            Vector3 before=camera.transform.localPosition,actorBefore=camera.WorldToViewportPoint(lead.transform.position);
            Quaternion facing=camera.transform.localRotation;float lens=camera.fieldOfView;bool flip=lead.flipX;
            stage.Tick(boundary+.001f,0f);Assert.That(lead.enabled,Is.True);AssertFinite(camera.transform.localPosition);AssertFinite(camera.transform.eulerAngles);
            Assert.That(camera.orthographic,Is.False,"The moving camera cannot switch projection at a phase boundary.");
            Assert.That(Vector3.Distance(before,camera.transform.localPosition),Is.LessThan(.15f),"Camera jump at "+boundary);
            Assert.That(Quaternion.Angle(facing,camera.transform.localRotation),Is.LessThan(2f),"Camera snap at "+boundary);
            Assert.That(Mathf.Abs(lens-camera.fieldOfView),Is.LessThan(.5f),"Lens snap at "+boundary);
            Vector3 after=camera.WorldToViewportPoint(lead.transform.position);AssertFinite(after);
            Assert.That(Vector2.Distance(actorBefore,after),Is.LessThan(.06f),"Actor jump at "+boundary);
            if(boundary>=2.25f)Assert.That(lead.flipX,Is.EqualTo(flip),"Bank, charge and downstroke preserve the same facing.");
        }
    }

    [Test]
    public void AuthoredPoseChangesKeepAConstantLogicalCellScaleInsteadOfRefittingTheirVisibleBounds()
    {
        Sprite previous=null;Vector3 scale=Vector3.one;int changes=0;
        for(float time=.12f;time<4.20f;time+=.002f)
        {
            stage.Tick(time,0f);var lead=Lead();if(!lead.enabled)continue;
            if(previous!=null&&lead.sprite!=previous)
            {
                Assert.That(Vector3.Distance(scale,lead.transform.localScale)/Mathf.Max(.001f,scale.magnitude),Is.LessThan(.025f),"Silhouette changes cannot resize the logical actor cell at "+time);changes++;
            }
            previous=lead.sprite;scale=lead.transform.localScale;
        }
        Assert.That(changes,Is.GreaterThanOrEqualTo(18));
    }

    [Test]
    public void ReplayAndCleanupPreserveWorldTimeRandomActorSnapshotsAndSharedArt()
    {
        float speed=Time.timeScale;string random=JsonUtility.ToJson(UnityEngine.Random.state);
        string source=JsonUtility.ToJson(context.source),target=JsonUtility.ToJson(context.targets[0]);
        var artwork=stage.ArtworkFrames.ToArray();Texture texture=stage.Texture;
        foreach(float time in new[]{0f,.38f,1.05f,1.88f,2.72f,3.40f,4.17f,stage.Duration,float.NaN,float.PositiveInfinity})
        {
            stage.Tick(time,.016f);Assert.That(stage.ActionTime,Is.EqualTo(-1f));Assert.That(Time.timeScale,Is.EqualTo(speed));
            Assert.That(float.IsNaN(stage.Opacity)||float.IsInfinity(stage.Opacity),Is.False);Assert.That(stage.Opacity,Is.InRange(0f,1f));
            AssertFinite(camera.transform.localPosition);AssertFinite(camera.transform.eulerAngles);
        }
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));
        Assert.That(JsonUtility.ToJson(context.source),Is.EqualTo(source));Assert.That(JsonUtility.ToJson(context.targets[0]),Is.EqualTo(target));
        stage.Hide();Assert.That(stage.Active,Is.False);Assert.That(root.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.particleCount==0),Is.True);
        Assert.That(stage.Begin(context),Is.True);stage.Tick(1.05f,0f);Assert.That(stage.Opacity,Is.EqualTo(1f));
        stage.Dispose();stage.Dispose();Assert.That(stage.Active,Is.False);Assert.That(artwork.All(s=>s==null),Is.True);Assert.That(texture==null,Is.True);
        Assert.That(sourceSprite!=null&&sourceTexture!=null&&fieldSprite!=null&&fieldTexture!=null,Is.True,"Stage disposal cannot destroy shared source or captured battlefield resources.");
    }
    private SpriteRenderer Lead()=>root.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Lead wyvern");
    private IEnumerable<SpriteRenderer> VisibleSprites()=>root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled&&r.color.a>.01f&&r.sprite!=null);
    private static int Column(Sprite sprite)=>Mathf.RoundToInt(sprite.rect.x/sprite.rect.width);
    private static void AssertFinite(Vector3 value)
    {Assert.That(float.IsNaN(value.x)||float.IsInfinity(value.x)||float.IsNaN(value.y)||float.IsInfinity(value.y)||float.IsNaN(value.z)||float.IsInfinity(value.z),Is.False);}
}
