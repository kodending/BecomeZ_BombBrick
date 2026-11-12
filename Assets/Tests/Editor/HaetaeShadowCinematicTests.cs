using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class HaetaeShadowCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private const string ArtRoot="CombatCinematics/HaetaeShadowV1/",NativeResource="CombatEffects/Haetae/Devourer16";
    private GameObject parent;
    private HaetaeMandateRank4Stage stage;
    private Sprite borrowedGuardian;
    private Rank4StageContext context;

    [SetUp]public void Setup()
    {
        parent=new GameObject("Haetae shadow cinematic tests");
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.First(d=>d!=null&&d.monsterId=="haeta");
        var driver=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var atlas=Resources.Load<Texture2D>(NativeResource);Assert.That(atlas,Is.Not.Null);
        borrowedGuardian=Sprite.Create(atlas,new Rect(0,atlas.height*.75f,atlas.width*.25f,atlas.height*.25f),Vector2.one*.5f,atlas.width*.25f);
        var actor=new FrogCatchCinematicStage.Actor{species="haeta",sprite=driver.IdleFrames[0],frames=driver.IdleFrames,size=1f,color=Color.white};
        context=new Rank4StageContext{family="haetae",source=actor,returnSprite=actor.sprite,returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f,
            guardianReturnSprite=borrowedGuardian,guardianReturnPosition=new Vector2(6.08f,-.45f),guardianReturnHeight=2.52f,guardianReturnVisible=true};
        stage=new HaetaeMandateRank4Stage(parent.transform);Assert.That(stage.Begin(context),Is.True);
    }

    [TearDown]public void Cleanup()
    {stage?.Dispose();Object.DestroyImmediate(borrowedGuardian);Object.DestroyImmediate(parent);}

    private Camera View=>parent.GetComponentInChildren<Camera>(true);
    private SpriteRenderer Actor(string name)=>parent.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==name);

    [Test]public void ApprovedPhasesHaveAnIndependentClockAndNoCombatOrMealCue()
    {
        Assert.That(stage.Duration,Is.EqualTo(4.80f));
        Assert.That(HaetaeMandateRank4Stage.FieldReturnTime,Is.EqualTo(4.05f));
        Assert.That(HaetaeMandateRank4Stage.NativeRevealTime,Is.EqualTo(4.60f));
        Assert.That(HaetaeMandateRank4Stage.BoardTimes,Is.EqualTo(new[]{.38f,1.10f,1.90f,2.70f,3.65f,4.53f}));
        float speed=Time.timeScale;var random=UnityEngine.Random.state;
        try
        {
            Time.timeScale=0f;
            for(float time=0f;time<=stage.Duration;time+=1f/120f)
            {
                stage.Tick(time,1f/120f);Assert.That(stage.ActionTime,Is.EqualTo(-1f));
                Assert.That(stage.SoundBeat,Is.InRange(-1,0),"Only an optional paw preparation cue can belong to this film.");
                Assert.That(Time.timeScale,Is.Zero);Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
                AssertFinite(View.transform.localPosition);AssertFinite(View.transform.localRotation.eulerAngles);
                Assert.That(View.fieldOfView,Is.InRange(1f,179f));Assert.That(View.orthographicSize,Is.GreaterThan(0f));
            }
            stage.Tick(stage.Duration,0f);Assert.That(stage.Opacity,Is.LessThan(.01f));stage.Hide();Assert.That(stage.Active,Is.False);
        }
        finally{Time.timeScale=speed;UnityEngine.Random.state=random;}
    }

    [TestCase(.65f)][TestCase(1.50f)][TestCase(2.30f)][TestCase(3.10f)][TestCase(3.90f)][TestCase(4.05f)]
    public void AwakeningCameraDoesNotJumpAtTheAuthoredPhaseBoundaries(float boundary)
    {
        stage.Tick(boundary-.001f,0f);Vector3 position=View.transform.localPosition;Quaternion angle=View.transform.localRotation;
        float lens=View.fieldOfView;bool orthographic=View.orthographic;
        stage.Tick(boundary+.001f,0f);
        Assert.That(View.orthographic,Is.EqualTo(orthographic));
        Assert.That(Vector3.Distance(position,View.transform.localPosition),Is.LessThan(.25f));
        Assert.That(Quaternion.Angle(angle,View.transform.localRotation),Is.LessThan(3f));
        Assert.That(Mathf.Abs(lens-View.fieldOfView),Is.LessThan(3f));
    }

    [TestCase("haeta",false,0)][TestCase("yeomhae",false,1)][TestCase("geumgangsu",false,2)]
    [TestCase("mukryeong",false,3)][TestCase("haeta",true,4)]
    public void EachActualBodyRowHasFourDifferentAuthoredPawFrames(string species,bool shiny,int row)
    {
        var actor=context.source;actor.species=species;actor.shiny=shiny;context.source=actor;Assert.That(stage.Begin(context),Is.True);
        var atlas=Resources.Load<Texture2D>(ArtRoot+"Body20");Assert.That(atlas,Is.Not.Null);
        var poses=new HashSet<Rect>();
        foreach(float time in new[]{.13f,.28f,.43f,.60f})
        {
            stage.Tick(time,0f);var body=Actor("Lead Haetae paw");Assert.That(body.enabled,Is.True);
            Assert.That(body.sprite.texture,Is.SameAs(atlas));
            Assert.That(body.sprite.rect.y,Is.EqualTo((4-row)*atlas.height/5f).Within(.001f));poses.Add(body.sprite.rect);
        }
        Assert.That(poses.Count,Is.EqualTo(4),"Paw lift, extension, contact and weight settlement must use four drawings.");
        AssertDistinctCells(atlas,4,5,row);
    }

    [TestCase("Shadow4")][TestCase("Mane4")][TestCase("Closed4")][TestCase("Eye4")]
    public void EveryAwakeningPhaseHasFourAuthoredDrawingsInsteadOfAReusedTheatricalAtlas(string resource)
    {
        var atlas=Resources.Load<Texture2D>(ArtRoot+resource);Assert.That(atlas,Is.Not.Null);
        AssertDistinctCells(atlas,4,1,0);
        Assert.That(stage.ArtworkFrames.Any(s=>s.texture==atlas),Is.True,"The authored phase must be connected to the film.");
        Assert.That(stage.ArtworkFrames.Any(s=>s.name.StartsWith("CombatCinematics/Rank4V3/haetae",StringComparison.Ordinal)),Is.False);
        string name=resource=="Shadow4"?"Independent waking ground shadow":resource=="Mane4"?"Layered jade mane 0":
            resource=="Closed4"?"Closed-eye jade guardian":"Opening gold eye and inhale";
        float[] times=resource=="Shadow4"?new[]{.75f,1.00f,1.25f,1.50f}:resource=="Mane4"?new[]{1.53f,1.77f,1.97f,2.22f}:
            resource=="Closed4"?new[]{2.35f,2.58f,2.79f,3.00f}:new[]{3.12f,3.22f,3.32f,3.43f};
        var frames=new HashSet<int>();
        foreach(float time in times)
        {
            stage.Tick(time,0f);var renderer=Actor(name);Assert.That(renderer.enabled,Is.True);
            Assert.That(renderer.sprite.texture,Is.SameAs(atlas));frames.Add(Mathf.FloorToInt(renderer.sprite.rect.x/(atlas.width/4f)));
        }
        Assert.That(frames,Is.EquivalentTo(new[]{0,1,2,3}),"Every authored phase drawing must actually be played.");
    }

    [Test]public void ManeParticlesReuseTheFourAuthoredCurlsAtDifferentWorldDepths()
    {
        stage.Tick(1.90f,0f);
        var particles=parent.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="Layered four-drawing jade mane particles");
        var animation=particles.textureSheetAnimation;Assert.That(animation.enabled,Is.True);
        Assert.That(animation.mode,Is.EqualTo(ParticleSystemAnimationMode.Sprites));Assert.That(animation.spriteCount,Is.EqualTo(4));
        var atlas=Resources.Load<Texture2D>(ArtRoot+"Mane4");
        for(int index=0;index<4;index++)Assert.That(animation.GetSprite(index).texture,Is.SameAs(atlas));
        var motes=new ParticleSystem.Particle[64];int count=particles.GetParticles(motes);Assert.That(count,Is.GreaterThan(8));
        Assert.That(motes.Take(count).Max(p=>p.position.z)-motes.Take(count).Min(p=>p.position.z),Is.GreaterThan(1f));
    }

    [Test]public void TheSmallFourLeggedSourceRemainsSeparateFromTheSingleHeadAndNoBenefitsAreInvented()
    {
        foreach(float time in HaetaeMandateRank4Stage.BoardTimes.Take(5))
        {
            stage.Tick(time,0f);var sprites=parent.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(sprites.Any(r=>r.enabled&&(r.name.StartsWith("Benefit shape",StringComparison.Ordinal)||
                r.name.IndexOf("buff icon",StringComparison.OrdinalIgnoreCase)>=0)),Is.False);
            var heads=sprites.Where(r=>r.enabled&&(r.name=="Closed-eye jade guardian"||r.name=="Opening gold eye and inhale"||
                r.name=="Native jade guardian continuation")).ToArray();
            Assert.That(heads.Length,Is.LessThanOrEqualTo(1));
            if(time<1.5f)continue;
            var body=Actor("Lead Haetae paw");Assert.That(body.enabled,Is.True);
            Assert.That(VisibleRect(View,body).height,Is.LessThan(.20f),"A close view of the independent head must not enlarge the source into a giant body.");
        }
    }

    [TestCase(false)][TestCase(true)]
    public void ReturnBorrowsTheExactNativeFaceAndHandsItOverOnceWithoutDestroyingItsResources(bool enemy)
    {
        context.enemy=enemy;var source=context.source;source.flipX=enemy;context.source=source;
        context.guardianReturnPosition=new Vector2(enemy?-6.08f:6.08f,-.45f);context.guardianReturnColor=new Color(.9f,.95f,1f,.75f);
        Assert.That(stage.Begin(context),Is.True);stage.Tick(4.00f,0f);
        var head=Actor("Native jade guardian continuation");
        float physicalHeight=head.sprite.bounds.size.y*head.transform.lossyScale.y,lens=View.fieldOfView;
        Vector3 cameraPosition=View.transform.position;stage.Tick(4.53f,0f);Assert.That(head.enabled,Is.True);
        Assert.That(head.sprite,Is.SameAs(borrowedGuardian));Assert.That(head.color,Is.EqualTo(context.guardianReturnColor));
        Assert.That(head.flipX,Is.EqualTo(enemy));Assert.That(View.orthographic,Is.False);Assert.That(View.backgroundColor.a,Is.Zero);
        Assert.That(head.sprite.bounds.size.y*head.transform.lossyScale.y,Is.EqualTo(physicalHeight).Within(.001f),
            "The same physical head keeps its world size while the perspective camera pulls back.");
        Assert.That(Vector3.Distance(View.transform.position,cameraPosition),Is.GreaterThan(1f));Assert.That(View.fieldOfView,Is.GreaterThan(lens));
        Assert.That(Actor("Lead Haetae paw").enabled,Is.False,"The source has already returned to the actual battlefield.");
        Rect visible=VisibleRect(View,head);Assert.That(visible.center.x,Is.EqualTo(enemy?.12f:.88f).Within(.015f));
        Assert.That(visible.center.y,Is.EqualTo(.59f).Within(.015f));Assert.That(visible.height,Is.EqualTo(.28f).Within(.015f));
        stage.Tick(HaetaeMandateRank4Stage.NativeRevealTime,0f);Assert.That(head.enabled,Is.False,"The native renderer owns the only head from 4.60 seconds.");
        Texture texture=borrowedGuardian.texture;stage.Dispose();
        Assert.That(borrowedGuardian!=null&&texture!=null,Is.True,"A borrowed native face and its atlas outlive the private film.");
    }

    [Test]public void ExistingNativeSixteenFrameAtlasIsPreservedForTheMouthHandoff()
    {
        string path=Path.Combine(Application.dataPath,"Resources/CombatEffects/Haetae/Devourer16.png");
        using(var hash=SHA256.Create())
            Assert.That(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant(),
                Is.EqualTo("4a99a09ddfe650f6e0d59077e272bea727ebe44b1014dba7d1136ddb82c68a37"));
        var atlas=Resources.Load<Texture2D>(NativeResource);
        Assert.That(stage.ArtworkFrames.Count(s=>s.texture==atlas),Is.EqualTo(16));
        foreach(var sample in new[]{new Vector2(3.65f,4),new Vector2(3.99f,11),new Vector2(4.40f,0)})
        {
            stage.Tick(sample.x,0f);var sprite=Actor("Native jade guardian continuation").sprite;
            Assert.That(sprite.texture,Is.SameAs(atlas));int cell=NativeFrame(sprite);
            Assert.That(cell,Is.InRange((int)sample.y,(int)sample.y+(sample.y==11?4:3)));
        }
    }

    [TestCase("PrimordialMeadow")][TestCase("PoisonFogSwamp")][TestCase("CrimsonJungle")]
    [TestCase("FrozenHighlands")][TestCase("SkySanctuary")]
    public void TheCorrectRegionalFloorAndPanoramaStayInsideThePrivateThreeDimensionalSet(string region)
    {
        context.sceneryRegion=region;Assert.That(stage.Begin(context),Is.True);stage.Tick(1.90f,0f);
        var scenery=(FrogCatchCinematicEnvironment)typeof(HaetaeMandateRank4Stage).GetFields(Hidden)
            .Single(f=>f.FieldType==typeof(FrogCatchCinematicEnvironment)).GetValue(stage);
        Assert.That(scenery.Active,Is.True);Assert.That(scenery.Region,Is.EqualTo(region));
        Assert.That(scenery.Kit,Is.SameAs(Resources.Load<Texture2D>(FrogCatchCinematicEnvironment.FloorResourceRoot+region)));
        Assert.That(scenery.Panorama,Is.SameAs(Resources.Load<Texture2D>(FrogCatchCinematicEnvironment.PanoramaResourceRoot+region)));
        Assert.That(View.orthographic,Is.False);
        var meshes=parent.GetComponentsInChildren<MeshRenderer>(true);
        Assert.That(meshes.Any(r=>r.name=="Generated continuous battle ground"&&r.gameObject.activeInHierarchy),Is.True);
        Assert.That(meshes.Any(r=>r.name=="Generated curved horizon"&&r.gameObject.activeInHierarchy),Is.True);
        foreach(float time in new[]{HaetaeMandateRank4Stage.FieldReturnTime,4.35f})
        {
            stage.Tick(time,0f);Assert.That(scenery.Active,Is.True);Assert.That(View.orthographic,Is.False);
            foreach(var mesh in meshes)Assert.That(mesh.sharedMaterial.GetFloat("_SceneOpacity"),Is.EqualTo(1f));
        }
        stage.Tick(4.475f,0f);Assert.That(scenery.Active,Is.True);
        foreach(var mesh in meshes)Assert.That(mesh.sharedMaterial.GetFloat("_SceneOpacity"),Is.EqualTo(.5f).Within(.001f));
        stage.Tick(HaetaeMandateRank4Stage.NativeRevealTime,0f);Assert.That(scenery.Active,Is.False);
        var floor=scenery.Kit;var panorama=scenery.Panorama;stage.Dispose();
        Assert.That(scenery.Active,Is.False);Assert.That(floor!=null&&panorama!=null,Is.True,"Shared regional textures must not be destroyed with a film.");
    }

    [TestCase(true)][TestCase(false)]
    public void HiddenNativeClockPreservesRealMealsAndReleasesTheirFeedbackExactlyOnce(bool allied)
    {
        var reference=parent.AddComponent<SpriteRenderer>();reference.sprite=context.source.sprite;
        float clock=0f;int absorbed=0,swallowed=0;
        var fx=PixelHaetaeDevourer.Create(reference,new Bounds(Vector3.zero,new Vector3(20f,1f,12f)),allied,()=>clock,()=>true,
            point=>swallowed++,point=>absorbed++,true);Assert.That(fx,Is.Not.Null);
        fx.transform.SetParent(parent.transform,true);
        try
        {
            fx.Absorb(Vector3.zero,CombatBenefitKind.Healing);clock=5f;fx.Absorb(Vector3.right,CombatBenefitKind.Shield);
            clock=10f;Refresh(fx);Assert.That(fx.PresentationHidden,Is.True);
            Assert.That(fx.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),Is.True);
            Assert.That(absorbed,Is.Zero);Assert.That(swallowed,Is.Zero);
            Assert.That(((IList)typeof(PixelHaetaeDevourer).GetField("meals",Hidden).GetValue(fx)).Count,Is.EqualTo(2));
            fx.CompleteCinematicEntrance();fx.SetCinematicHidden(true,true);Refresh(fx);
            Assert.That(fx.BodyRenderer.forceRenderingOff,Is.False);Assert.That(fx.BodyRenderer.flipX,Is.EqualTo(!allied));
            Assert.That(fx.BodyRenderer.color.a,Is.EqualTo(.75f).Within(.001f));Assert.That(NativeFrame(fx.BodyRenderer.sprite),Is.InRange(0,3));
            Assert.That(fx.GetComponentsInChildren<ParticleSystemRenderer>(true).All(r=>r.forceRenderingOff),Is.True);
            Assert.That(absorbed+swallowed,Is.Zero,"Revealing the idle head cannot consume queued benefits or play meal feedback.");
            fx.SetCinematicHidden(false);clock=10.02f;Refresh(fx);Refresh(fx);
            Assert.That(absorbed,Is.EqualTo(2));Assert.That(swallowed,Is.Zero);
            clock=11f;Refresh(fx);Refresh(fx);Assert.That(swallowed,Is.EqualTo(1));Assert.That(absorbed,Is.EqualTo(2));
        }
        finally{if(fx!=null)fx.Cancel();}
    }

    [Test]public void InvalidHiddenNativeCancelsWithoutPlayingTheQueuedMeal()
    {
        var reference=parent.AddComponent<SpriteRenderer>();reference.sprite=context.source.sprite;
        float clock=0f;bool valid=true;int feedback=0;
        var fx=PixelHaetaeDevourer.Create(reference,new Bounds(Vector3.zero,new Vector3(20f,1f,12f)),true,()=>clock,()=>valid,
            point=>feedback++,point=>feedback++,true);
        fx.transform.SetParent(parent.transform,true);
        fx.Absorb(Vector3.zero,CombatBenefitKind.Enhancement);clock=10f;valid=false;Refresh(fx);
        clock=11f;Refresh(fx);Assert.That(fx==null,Is.True);Assert.That(feedback,Is.Zero);
    }

    private static void Refresh(PixelHaetaeDevourer fx)=>typeof(PixelHaetaeDevourer).GetMethod("LateUpdate",Hidden).Invoke(fx,null);
    private static int NativeFrame(Sprite sprite)=>(3-Mathf.RoundToInt(sprite.rect.y/sprite.rect.height))*4+Mathf.RoundToInt(sprite.rect.x/sprite.rect.width);
    private static void AssertFinite(Vector3 value)
    {foreach(float component in new[]{value.x,value.y,value.z})Assert.That(float.IsNaN(component)||float.IsInfinity(component),Is.False);}

    private static void AssertDistinctCells(Texture2D atlas,int columns,int rows,int row)
    {
        Assert.That(atlas.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(atlas.isReadable,Is.True);
        Assert.That(atlas.width%columns,Is.Zero);Assert.That(atlas.height%rows,Is.Zero);
        Color32[] pixels=atlas.GetPixels32();int width=atlas.width/columns,height=atlas.height/rows;
        var signatures=new HashSet<ulong>();
        for(int column=0;column<columns;column++)
        {
            ulong signature=14695981039346656037UL;int visible=0;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                Color32 p=pixels[((rows-1-row)*height+y)*atlas.width+column*width+x];if(p.a>0)visible++;
                unchecked{signature=(signature^p.r)*1099511628211UL;signature=(signature^p.g)*1099511628211UL;
                    signature=(signature^p.b)*1099511628211UL;signature=(signature^p.a)*1099511628211UL;}
            }
            Assert.That(visible,Is.GreaterThan(16),atlas.name+" has an empty authored cell.");
            Assert.That(signatures.Add(signature),Is.True,atlas.name+" repeats a drawing in row "+row);
        }
    }

    private static Rect VisibleRect(Camera camera,SpriteRenderer actor)
    {
        Bounds body=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{actor.sprite});
        float left=float.PositiveInfinity,bottom=float.PositiveInfinity,right=float.NegativeInfinity,top=float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            Vector3 point=new Vector3(x==0?body.min.x:body.max.x,y==0?body.min.y:body.max.y,0f);if(actor.flipX)point.x=-point.x;
            Vector3 projected=camera.WorldToViewportPoint(actor.transform.TransformPoint(point));Assert.That(projected.z,Is.GreaterThan(0f));
            left=Mathf.Min(left,projected.x);bottom=Mathf.Min(bottom,projected.y);right=Mathf.Max(right,projected.x);top=Mathf.Max(top,projected.y);
        }
        return Rect.MinMaxRect(Mathf.Max(0f,left),Mathf.Max(0f,bottom),Mathf.Min(1f,right),Mathf.Min(1f,top));
    }
}
