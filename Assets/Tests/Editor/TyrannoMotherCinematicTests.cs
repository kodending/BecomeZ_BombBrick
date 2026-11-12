using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class TyrannoMotherCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    private GameObject host;
    private TyrannoReignRank4Stage stage;
    private Rank4StageContext context;
    private Camera camera;
    private Transform root;
    private T Read<T>(string name)=>(T)typeof(TyrannoReignRank4Stage).GetField(name,Hidden).GetValue(stage);

    [SetUp]public void Setup()
    {
        host=new GameObject("God tyrant eight-panel cinematic contract");
        context=new Rank4StageContext{family="tyranno",source=Actor("tino"),returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f,
            targets=new[]{Actor("woongi"),Actor("velos")}};
        stage=new TyrannoReignRank4Stage(host.transform);Assert.That(stage.Begin(context),Is.True);
        camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
        root=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage);
    }

    [TearDown]public void Cleanup(){stage?.Dispose();Object.DestroyImmediate(host);}

    private static FrogCatchCinematicStage.Actor Actor(string species,bool shiny=false)
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.First(d=>d!=null&&d.monsterId==species);
        var prefab=Resources.Load<GameObject>(shiny?"Enemies/Shiny/"+species:definition.resourcePath);
        var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        return new FrogCatchCinematicStage.Actor{species=species,shiny=shiny,sprite=driver.IdleFrames[0],size=1f,color=Color.white};
    }

    [Test]public void NativeContactOccursOnceAfterTheFieldReturnsAndNeverDuringTheGodShot()
    {
        float previous=-1f;int contacts=0;
        for(int frame=0;frame<=Mathf.CeilToInt(stage.Duration*120f);frame++)
        {
            float time=frame/120f,current=TyrannoReignRank4Stage.CastTimeAt(time);
            Assert.That(current,Is.GreaterThanOrEqualTo(previous-.00001f));
            if(time<TyrannoReignRank4Stage.FilmImpactTime)
                Assert.That(current,Is.LessThan(FamilyTranscendenceRules.TyrannoStompWarning));
            if(previous<FamilyTranscendenceRules.TyrannoStompWarning&&current>=FamilyTranscendenceRules.TyrannoStompWarning)contacts++;
            previous=current;
        }
        Assert.That(contacts,Is.EqualTo(1));
        Assert.That(stage.Duration,Is.EqualTo(5.80f).Within(.00001f));
        Assert.That(TyrannoReignRank4Stage.FieldReturnTime,Is.LessThan(TyrannoReignRank4Stage.FilmImpactTime));
        stage.Tick(.20f,0f);Assert.That(stage.ActionTime,Is.EqualTo(-1f));
        stage.Tick(TyrannoReignRank4Stage.FieldReturnTime,0f);
        Assert.That(stage.ActionTime,Is.InRange(.35f,.37f),"The real foot must already be falling when the battlefield returns.");
        stage.Tick(TyrannoReignRank4Stage.FilmImpactTime,0f);
        Assert.That(stage.ActionTime,Is.EqualTo(FamilyTranscendenceRules.TyrannoStompWarning).Within(.00001f));
        stage.Tick(stage.Duration,0f);
        Assert.That(stage.ActionTime,Is.EqualTo(FamilyTranscendenceRules.TyrannoStompEnd).Within(.00001f));
    }

    [Test]public void LensDescendsToTheEarthThenLooksUpAtAVerticalGodForEitherTeam()
    {
        var playerLens=new Dictionary<float,float>();
        foreach(bool enemy in new[]{false,true})
        {
            context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
            stage.Tick(TyrannoReignRank4Stage.FieldPreludeEndTime,0f);
            float previousY=camera.transform.localPosition.y;
            Assert.That(previousY,Is.GreaterThan(6f));
            Assert.That((camera.transform.localRotation*Vector3.forward).y,Is.LessThan(0f));
            foreach(float time in new[]{1.70f,1.80f,1.90f,1.98f})
            {
                stage.Tick(time,0f);
                Assert.That(camera.transform.localPosition.y,Is.LessThan(previousY));
                previousY=camera.transform.localPosition.y;
            }
            foreach(float time in new[]{2.25f,2.58f,3.94f,4.10f})
            {
                stage.Tick(time,0f);var mother=Read<SpriteRenderer>("lead");
                Assert.That(camera.orthographic,Is.False);
                Assert.That(mother.flipX,Is.EqualTo(enemy));
                if(!enemy)playerLens[time]=camera.transform.localPosition.x;
                else Assert.That(camera.transform.localPosition.x,Is.EqualTo(-playerLens[time]).Within(.04f));
                Assert.That(camera.transform.localPosition.y,Is.InRange(0f,.35f));
                Assert.That((camera.transform.localRotation*Vector3.forward).y,Is.GreaterThan(.15f));
                Assert.That(Vector3.Dot(mother.transform.localRotation*Vector3.up,Vector3.up),Is.GreaterThan(.9999f));
                Assert.That(Quaternion.Angle(mother.transform.localRotation,camera.transform.localRotation),Is.GreaterThan(10f),
                    "The giant is a vertical world plane, not an image that tilts with the camera.");
            }
        }
        var vertices=Read<MeshRenderer>("ground").GetComponent<MeshFilter>().sharedMesh.vertices;
        Assert.That(vertices.All(p=>Mathf.Abs(p.y)<.0001f),Is.True);
        Assert.That(vertices.Max(p=>p.z)-vertices.Min(p=>p.z),Is.GreaterThan(50f));
        var rocks=Read<SpriteRenderer[]>("rocks");
        var rockFeet=rocks.Select(VisibleFeet).ToArray();
        Assert.That(rockFeet.All(p=>Mathf.Abs(p.y-.015f)<.001f),Is.True);
        Assert.That(rockFeet.Select(p=>Mathf.RoundToInt(p.z*10f)).Distinct().Count(),Is.EqualTo(rocks.Length),
            "Scenery rocks must occupy varied depths rather than mirrored exhibition rows.");
        Assert.That(rockFeet.Any(p=>p.x<0f)&&rockFeet.Any(p=>p.x>0f),Is.True);
        var contacts=Read<SpriteRenderer[]>("rockShadows");
        for(int i=0;i<rocks.Length;i++)
        {
            Assert.That(contacts[i].enabled,Is.True);
            Assert.That(contacts[i].transform.localPosition.x,Is.EqualTo(rockFeet[i].x).Within(.001f));
            Assert.That(contacts[i].transform.localPosition.z,Is.EqualTo(rockFeet[i].z).Within(.001f));
            Assert.That(contacts[i].sprite,Is.Not.SameAs(Read<Sprite[]>("props")[3]),
                "A tiny actor shadow must not be a gold-ring crater badge.");
        }
    }

    [Test]public void AuthoredRevealRoarWeightLiftAndHoldKeepAStablePlantedLeftLeg()
    {
        var mother=Read<SpriteRenderer>("lead");var pictures=new HashSet<Sprite>();float scale=-1f;
        float[] moments={2.58f,3.05f,3.35f,3.64f,3.85f};
        for(int index=0;index<moments.Length;index++)
        {
            int pose=index+1;stage.Tick(moments[index],0f);
            Assert.That(TyrannoReignRank4Stage.MotherPoseAt(moments[index]),Is.EqualTo(pose));
            Assert.That(mother.sprite.name,Does.EndWith(" 0:"+pose));
            Assert.That(pictures.Add(mother.sprite),Is.True,"Each phase needs a newly drawn pose.");
            if(scale>0f)
            {
                Assert.That(mother.transform.localScale.x/scale,Is.InRange(.99f,1.01f));
                Assert.That(mother.transform.localScale.y/scale,Is.InRange(.99f,1.01f));
            }
            else scale=mother.transform.localScale.x;
            Vector3 support=new Vector3((100f/128f-.5f)*mother.sprite.rect.width/mother.sprite.pixelsPerUnit,
                (.5f-113f/128f)*mother.sprite.rect.height/mother.sprite.pixelsPerUnit,0f);
            Assert.That(root.InverseTransformPoint(mother.transform.TransformPoint(support)).y,Is.EqualTo(0f).Within(.001f),
                "Breathing and pose settling must pivot on the planted foot.");
        }
        stage.Tick(4.39f,0f);var sole=Read<SpriteRenderer>("sole");
        float raisedDepth=camera.WorldToViewportPoint(sole.transform.position).z;
        stage.Tick(4.66f,0f);
        Assert.That(camera.WorldToViewportPoint(sole.transform.position).z,Is.LessThan(raisedDepth-5f),
            "The same sole must accelerate into the lens before the native foot takes over.");
        stage.Tick(4.72f,0f);Assert.That(sole.enabled,Is.False);Assert.That(camera.orthographic,Is.True);
        Assert.That(root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled),Is.False,
            "The sole itself provides the cut; no full-screen earth mask may take over.");
        stage.Tick(4.80f,0f);Assert.That(sole.enabled,Is.False);
        stage.Tick(4.85f,0f);Assert.That(sole.enabled,Is.False);
    }

    [Test]public void OtherSpeciesRetainTheirLowThreeQuarterJawCloseups()
    {
        foreach(string species in new[]{"magno","cheolno","bonrex"})
        {
            context.source=Actor(species);Assert.That(stage.Begin(context),Is.True);
            stage.Tick(2.58f,0f);Vector3 reveal=camera.transform.localPosition;
            stage.Tick(3.05f,0f);var mother=Read<SpriteRenderer>("lead");
            Rect jaw=ProjectedArt(mother,new Rect(.46f,.49f,.39f,.48f));
            Assert.That(jaw.height,Is.InRange(.70f,.86f),species+" jaw/chest must dominate the frame, not keep the reveal's full-body composition.");
            Assert.That(jaw.center.x,Is.InRange(.38f,.62f));
            Assert.That(jaw.center.y,Is.InRange(.38f,.62f));
            Assert.That(camera.fieldOfView,Is.LessThan(55f));
            Assert.That(Vector3.Distance(camera.transform.localPosition,reveal),Is.GreaterThan(15f));
            Assert.That((camera.transform.rotation*Vector3.forward).y,Is.GreaterThan(.15f),"The close-up must still look upward under the jaw.");
            Vector3 focus=mother.transform.TransformPoint(new Vector3((.655f-.5f)*mother.sprite.rect.width/mother.sprite.pixelsPerUnit,
                (.73f-.5f)*mother.sprite.rect.height/mother.sprite.pixelsPerUnit,0f));
            Assert.That(Mathf.Abs(camera.transform.localPosition.x-root.InverseTransformPoint(focus).x),Is.GreaterThan(8f),
                "The roar needs a three-quarter angle, rather than the same frontal reveal with a larger sprite.");
        }
    }

    [Test]public void TinoUsesFourNewRoarPosesWithTheActualOpenJawDominatingTheFrame()
    {
        var frames=Read<Sprite[]>("roarCloseFrames");Assert.That(frames,Has.Length.EqualTo(4));
        Assert.That(frames.Select(BodyShape).Distinct().Count(),Is.EqualTo(4),"Four real mouth/anatomy poses are required, not one moving image.");
        foreach(bool enemy in new[]{false,true})
        {
            context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
            var close=Read<SpriteRenderer>("closeJaw");
            var pictures=new HashSet<Sprite>();float[] moments={2.83f,2.94f,3.05f,3.31f};
            for(int i=0;i<moments.Length;i++)
            {
                stage.Tick(moments[i],0f);
                Assert.That(close.enabled,Is.True);Assert.That(Read<SpriteRenderer>("lead").enabled,Is.False,"The old head must never show behind the dedicated closeup.");
                Assert.That(close.sprite,Is.SameAs(frames[i]));Assert.That(pictures.Add(close.sprite),Is.True);
                Assert.That(close.flipX,Is.EqualTo(enemy));
            }
            Rect image=ProjectedArt(close,new Rect(0f,0f,1f,1f));
            Assert.That(enemy?image.xMin<0f:image.xMax>1f,Is.True,
                "The settling pose's cropped snout edge must continue outside the screen, never end against the sky.");
            stage.Tick(3.05f,0f);
            Rect jaw=ProjectedArt(close,new Rect(.275390625f,.068359375f,.662109375f,.748046875f));
            Assert.That(jaw.height,Is.InRange(.80f,1.02f),"Measure the actual open jaw, not a large box that includes the belly.");
            Assert.That(jaw.width,Is.GreaterThan(.42f));
            Assert.That(jaw.center.y,Is.InRange(.38f,.62f));
            Rect eye=ProjectedArt(close,new Rect(.5078125f,.77734375f,.001f,.001f));
            Assert.That(eye.center.y,Is.InRange(.72f,.99f),"The eye must remain visible above the enormous jaw.");
            stage.Tick(3.40f,0f);Assert.That(close.enabled,Is.False);Assert.That(Read<SpriteRenderer>("lead").enabled,Is.True);
        }
    }

    [Test]public void TinoSoleAndAllMovingStageDustUseAtLeastFourDistinctRuntimeFrames()
    {
        var soles=Read<Sprite[]>("soleMotionTino");Assert.That(soles,Has.Length.EqualTo(4));
        Assert.That(soles.Select(BodyShape).Distinct().Count(),Is.EqualTo(4),"The charging and driving sole must change its drawn anatomy.");
        var pictures=new HashSet<Sprite>();float[] moments={3.76f,4.10f,4.45f,4.66f};
        for(int i=0;i<moments.Length;i++)
        {
            stage.Tick(moments[i],0f);var sole=Read<SpriteRenderer>("sole");
            Assert.That(sole.enabled,Is.True);Assert.That(sole.sprite,Is.SameAs(soles[i]));pictures.Add(sole.sprite);
        }
        Assert.That(pictures.Count,Is.EqualTo(4));
        var dustFrames=Read<Sprite[]>("dustMotion");Assert.That(dustFrames,Has.Length.EqualTo(8));
        Assert.That(dustFrames.Select(BodyShape).Distinct().Count(),Is.GreaterThanOrEqualTo(4));
        var movingDust=new HashSet<Sprite>();
        for(float t=1.62f;t<4.70f;t+=.025f)
        {
            stage.Tick(t,0f);var puff=Read<SpriteRenderer[]>("dust")[0];
            if(puff.enabled)movingDust.Add(puff.sprite);
        }
        Assert.That(movingDust.Count,Is.GreaterThanOrEqualTo(4),"A live dust plume must advance through real drawn frames.");
    }

    [Test]public void DedicatedSetIsOpaqueAndFieldConnectedShotsHaveTransparentCorners()
    {
        foreach(float time in new[]{1.94f,2.58f,3.05f})
        {
            stage.Tick(time,0f);Color32[] pixels=Pixels();
            Assert.That(pixels.All(p=>p.a==255),Is.True,"The old battlefield must not show through the mother set.");
        }
        foreach(float time in new[]{.18f,.98f,4.85f,4.91f,5.55f})
        {
            stage.Tick(time,0f);Color32[] pixels=Pixels();
            Assert.That(pixels[0].a,Is.Zero);Assert.That(pixels[pixels.Length-1].a,Is.Zero);
            Assert.That(camera.orthographic,Is.True);
        }
    }

    [Test]public void FourSpeciesAndExistingBoneRexShinyUseTheirOwnGodAndSoleArtwork()
    {
        var normal=Read<Sprite[]>("motion");var alternate=Read<Sprite[]>("shiny");
        var normalSoles=Read<Sprite[]>("soles");var alternateSoles=Read<Sprite[]>("shinySoles");
        Assert.That(normal.Length,Is.EqualTo(32));Assert.That(alternate.Length,Is.EqualTo(32));
        Assert.That(normalSoles.Length,Is.EqualTo(4));Assert.That(alternateSoles.Length,Is.EqualTo(4));
        Assert.That(normal[1].rect.height,Is.GreaterThanOrEqualTo(512f),"Full-screen god artwork must retain its source detail instead of using the 128-pixel combat-sprite reduction.");
        Assert.That(normalSoles[0].rect.height,Is.GreaterThanOrEqualTo(512f),"The lens-filling sole also needs the original detail.");
        string[] species={"tino","magno","cheolno","bonrex"};var rows=new HashSet<Sprite>();
        for(int row=0;row<species.Length;row++)
        {
            context.source=Actor(species[row]);Assert.That(stage.Begin(context),Is.True);stage.Tick(2.58f,0f);
            Sprite selected=Read<SpriteRenderer>("lead").sprite;
            Assert.That(selected,Is.SameAs(normal[row*8+1]));Assert.That(rows.Add(selected),Is.True);
            Bounds body=VisibleBody(selected);
            Vector3 head=camera.WorldToViewportPoint(Read<SpriteRenderer>("lead").transform.TransformPoint(new Vector3(body.center.x,body.max.y-body.size.y*.035f,0f)));
            Assert.That(head.y,Is.InRange(.65f,.98f),species[row]+" must reveal its face rather than crop it above the frame.");
            for(int pose=0;pose<8;pose++)
                Assert.That(VisibleBody(normal[row*8+pose]).size.y,Is.GreaterThan(1f),"No authored pose may be empty.");
            stage.Tick(4.66f,0f);
            Assert.That(Read<SpriteRenderer>("sole").sprite,Is.SameAs(row==0?Read<Sprite[]>("soleMotionTino")[3]:normalSoles[row]));
        }
        context.source=Actor("bonrex",true);Assert.That(stage.Begin(context),Is.True);stage.Tick(2.58f,0f);
        Assert.That(Read<SpriteRenderer>("lead").sprite,Is.SameAs(alternate[3*8+1]));
        Assert.That(alternate[25].texture,Is.Not.SameAs(normal[25].texture));
        Assert.That(VisibleAverage(alternate[25])-VisibleAverage(normal[25]),Is.Not.EqualTo(Vector3.zero),
            "The shiny's god cannot silently reuse the normal bone palette.");
        stage.Tick(4.66f,0f);Assert.That(Read<SpriteRenderer>("sole").sprite,Is.SameAs(alternateSoles[3]));
        Assert.That(VisibleAverage(alternateSoles[3])-VisibleAverage(normalSoles[3]),Is.Not.EqualTo(Vector3.zero));
    }

    [Test]public void EveryLowAngleCornerHitsTheSkyArtworkBeforeTheFarClip()
    {
        foreach(bool enemy in new[]{false,true})
        {
            context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
            foreach(float time in new[]{1.60f,1.80f,1.94f,2.58f,3.05f,3.94f,4.66f})
            {
                stage.Tick(time,0f);var sky=Read<SpriteRenderer>("sky");
                Assert.That(sky.enabled,Is.True);
                var plane=new Plane(sky.transform.forward,sky.transform.position);
                for(int x=0;x<2;x++)for(int y=0;y<2;y++)
                {
                    Ray ray=camera.ViewportPointToRay(new Vector3(x,y,0f));
                    Assert.That(plane.Raycast(ray,out float distance),Is.True,"The sky must face every corner ray.");
                    Assert.That(distance,Is.GreaterThan(0f));Vector3 hit=ray.GetPoint(distance);
                    float depth=camera.WorldToViewportPoint(hit).z;
                    Assert.That(depth,Is.GreaterThan(camera.nearClipPlane));Assert.That(depth,Is.LessThan(camera.farClipPlane));
                    Vector3 local=sky.transform.InverseTransformPoint(hit);Bounds body=sky.sprite.bounds;
                    Assert.That(local.x,Is.InRange(body.min.x,body.max.x),"The sky image must cover the full horizontal view.");
                    Assert.That(local.y,Is.InRange(body.min.y,body.max.y),"The sky image must cover the full vertical view.");
                }
            }
        }
    }

    [Test]public void CallingLeavesTheActualChildToTheHostAndOnlyReturnsItWhenRequested()
    {
        context.source=Actor("bonrex",true);context.source.flipX=true;
        context.returnSprite=context.source.sprite;
        foreach(bool enemy in new[]{false,true})
        {
            context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
            stage.Tick(.18f,0f);
            Assert.That(Read<SpriteRenderer>("field").enabled,Is.False,"The host already displays the actual child.");
            Assert.That(Read<SpriteRenderer>("lead").enabled,Is.False);
            Assert.That(Pixels().Count(p=>p.a>0),Is.GreaterThan(4),"The golden exclamation must appear over the actual child.");
            stage.Tick(.98f,0f);
            Assert.That(Read<SpriteRenderer>("field").enabled,Is.False);
            Assert.That(Read<SpriteRenderer>("lead").enabled,Is.False);
            Assert.That(Pixels().All(p=>p.a==0),Is.True,"The approaching giant must remain unseen during the footstep prelude.");
            foreach(float time in new[]{4.73f,4.91f})
            {
                stage.Tick(time,0f);var child=Read<SpriteRenderer>("field");
                Assert.That(child.enabled,Is.True);Assert.That(child.sprite,Is.SameAs(context.source.sprite));
                Assert.That(child.flipX,Is.True);Assert.That(Read<SpriteRenderer>("lead").enabled,Is.False);
            }
        }
        context.returnBodyVisible=false;Assert.That(stage.Begin(context),Is.True);
        stage.Tick(4.91f,0f);Assert.That(Read<SpriteRenderer>("field").enabled,Is.False);
    }

    [Test]public void FieldHandoffAndReuseClearAllGodSetGeometryAndDisposeOwnedMeshes()
    {
        stage.Tick(2.80f,0f);var mesh=Read<MeshRenderer>("ground").GetComponent<MeshFilter>().sharedMesh;
        Assert.That(Read<MeshRenderer>("ground").enabled,Is.True);
        stage.Tick(4.85f,0f);
        Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.enabled),Is.False);
        foreach(string name in new[]{"lead","sole","sky","shadow","closeJaw"})Assert.That(Read<SpriteRenderer>(name).enabled,Is.False);
        Assert.That(Read<SpriteRenderer[]>("rocks").Any(r=>r.enabled),Is.False);
        Assert.That(Read<SpriteRenderer[]>("dust").Any(r=>r.enabled),Is.False);
        Assert.That(Read<SpriteRenderer[]>("witnesses").Any(r=>r.enabled),Is.False);
        foreach(string name in new[]{"rockShadows","witnessShadows","rockDust","godContacts","godDust","ridge","entryDust"})
            Assert.That(Read<SpriteRenderer[]>(name).Any(r=>r.enabled),Is.False,name+" must clear at field handoff.");
        stage.Hide();Assert.That(stage.Active,Is.False);Assert.That(root.gameObject.activeInHierarchy,Is.False);
        Assert.That(stage.Begin(context),Is.True);stage.Tick(.20f,0f);
        Assert.That(Read<MeshRenderer>("ground").enabled,Is.False);Assert.That(Read<SpriteRenderer>("sky").enabled,Is.False);
        stage.Dispose();Assert.That(mesh==null,Is.True);Assert.That(root==null,Is.True);
    }

    [Test]public void TheRightSoleItselfCompletelyOccludesTheLensBeforeTheMatchCut()
    {
        foreach(var actor in new[]{Actor("tino"),Actor("magno"),Actor("cheolno"),Actor("bonrex"),Actor("bonrex",true)})
        {
            context.source=actor;Assert.That(stage.Begin(context),Is.True);stage.Tick(4.66f,0f);
            var sole=Read<SpriteRenderer>("sole");Assert.That(sole.enabled,Is.True);
            Assert.That(Read<SpriteRenderer>("lead").enabled,Is.False,"The god must not show through the authored sole's near-opaque pixels.");
            var renderers=root.GetComponentsInChildren<Renderer>(true);bool[] enabled=renderers.Select(r=>r.enabled).ToArray();
            Color clear=camera.backgroundColor;
            try
            {
                foreach(Renderer renderer in renderers)renderer.enabled=false;
                sole.enabled=true;camera.backgroundColor=Color.clear;
                Color32[] pixels=Pixels();
                float coverage=pixels.Count(p=>p.a>204)/(float)pixels.Length;
                Assert.That(coverage,Is.GreaterThanOrEqualTo(.995f),actor.species+" must fully cover the screen with the sole artwork before the hidden camera cut.");
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];
                camera.backgroundColor=clear;
            }
        }
    }

    [Test]public void ActualFormationDepthAndSharedGroundContactsReplaceTheExhibitionLine()
    {
        var child=context.source;child.ground=new Vector2(-.8f,-.4f);child.size=1.4f;context.source=child;
        var near=context.targets[0];near.ground=new Vector2(.35f,-.8f);near.size=1f;
        var far=context.targets[1];far.ground=new Vector2(.35f,.75f);far.size=1f;
        var ally=Actor("cheolno");ally.ground=new Vector2(-.35f,.25f);context.allies=new[]{ally};
        context.targets=new[]{near,far};context.battlefieldBounds=new Bounds(Vector3.zero,new Vector3(20f,1f,10f));
        Assert.That(stage.Begin(context),Is.True);stage.Tick(2.58f,0f);
        var actors=Read<SpriteRenderer[]>("witnesses");var shadows=Read<SpriteRenderer[]>("witnessShadows");
        var expected=new[]{context.source,near,far,ally};
        for(int i=0;i<expected.Length;i++)
        {
            var actor=expected[i];Vector3 feet=VisibleFeet(actors[i]);
            Assert.That(feet.x,Is.EqualTo(actor.ground.x*7.6f).Within(.001f));
            Assert.That(feet.z,Is.EqualTo(8.8f+actor.ground.y*3.8f).Within(.001f));
            Assert.That(feet.y,Is.EqualTo(.02f).Within(.001f));
            Assert.That(shadows[i].enabled,Is.True);
            Assert.That(shadows[i].transform.localPosition.x,Is.EqualTo(feet.x).Within(.001f));
            Assert.That(shadows[i].transform.localPosition.z,Is.EqualTo(feet.z).Within(.001f));
            Assert.That(actors[i].color.r,Is.GreaterThan(actors[i].color.b),"Actors and scenery share the warm ambient light.");
        }
        float nearHeight=ProjectedHeight(actors[1]),farHeight=ProjectedHeight(actors[2]);
        Assert.That(farHeight,Is.LessThan(nearHeight*.9f),"Equal-size distant creatures must read smaller in perspective.");
        Assert.That(actors[1].sortingOrder,Is.GreaterThan(actors[2].sortingOrder));
        var block=new MaterialPropertyBlock();actors[1].GetPropertyBlock(block);float nearAir=block.GetFloat("_HitAmount");
        Assert.That(block.GetTexture("_MainTex"),Is.SameAs(actors[1].sprite.texture),"Atmosphere must retain the actual sprite atlas instead of rendering a white quad.");
        actors[2].GetPropertyBlock(block);
        Assert.That(block.GetFloat("_HitAmount"),Is.GreaterThan(nearAir),"Air color must increase with actual formation depth.");
    }

    [Test]public void TheRaisedRightSoleContinuesTowardTheActualLandingWithoutADustWipeOrDuplicateFoot()
    {
        context.returnBodyVisible=false;
        context.hasTyrannoFootprint=true;context.tyrannoFootprintViewport=new Rect(.15f,.23f,.72f,.30f);
        Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(1),"Only horizontal ground geometry remains; a smoke curtain must not replace the same-foot match cut.");
        foreach(bool enemy in new[]{false,true})
        {
            context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
            stage.Tick(3.74f,0f);var sole=Read<SpriteRenderer>("sole");var mother=Read<SpriteRenderer>("lead");
            Rect original=ProjectedArt(mother,new Rect(.5547f,.4023f,.0001f,.0001f));
            Rect attached=ProjectedArt(sole,new Rect(.5625f,.0918f,.0001f,.0001f));
            Assert.That(Vector2.Distance(original.center,attached.center),Is.LessThan(.005f),
                "The new sole's actual ankle must begin on the raised body's ankle, not an arbitrary image center.");
            Assert.That(sole.flipX,Is.EqualTo(mother.flipX));
            stage.Tick(3.94f,0f);
            Assert.That(ProjectedVisible(sole).width,Is.InRange(.50f,.70f),"Panel six holds an immense foreground sole while the left leg remains visible.");
            Vector3 support=new Vector3((100f/128f-.5f)*mother.sprite.rect.width/mother.sprite.pixelsPerUnit,
                (.5f-113f/128f)*mother.sprite.rect.height/mother.sprite.pixelsPerUnit,0f);
            Assert.That(root.InverseTransformPoint(mother.transform.TransformPoint(support)).y,Is.EqualTo(0f).Within(.001f));
            foreach(float time in new[]{4.64f,4.66f,4.70f})
            {
                stage.Tick(time,0f);
                Assert.That(Vector2.Distance(ProjectedVisible(sole).center,context.tyrannoFootprintViewport.center),Is.LessThan(.005f),
                    "The same sole must descend toward the real native contact area for either team.");
                Color32[] pixels=PixelsWithOnly(new[]{sole});
                Assert.That(pixels.Count(p=>p.a>250)/(float)pixels.Length,Is.GreaterThanOrEqualTo(.995f),
                    time+" must be covered by the real sole itself, not generic smoke.");
            }
            foreach(float time in new[]{4.72f,4.78f,4.80f,4.91f})
            {
                stage.Tick(time,0f);
                Assert.That(sole.enabled,Is.False,"After the cut only the actual native foot may exist.");
                Assert.That(Pixels().All(p=>p.a==0),Is.True,"The native descent must be unobstructed by a second sole or a dust curtain.");
            }
        }
    }

    [Test]public void EightAnimatedEntryBillowsKeepTheirViewportPositionsAcrossTheCameraCut()
    {
        var clouds=Read<SpriteRenderer[]>("entryDust");Assert.That(clouds,Has.Length.EqualTo(8));
        stage.Tick(1.599f,0f);
        Rect[] before=clouds.Select(ProjectedVisible).ToArray();Sprite[] poses=clouds.Select(c=>c.sprite).ToArray();
        Assert.That(camera.orthographic,Is.True);
        Color32[] covered=PixelsWithOnly(clouds);
        Assert.That(covered.Count(p=>p.a>250)/(float)covered.Length,Is.GreaterThanOrEqualTo(.995f),
            "The third approaching footstep's real animated dust must conceal the incoming camera change.");
        stage.Tick(1.601f,0f);Assert.That(camera.orthographic,Is.False);
        for(int i=0;i<clouds.Length;i++)
        {
            Assert.That(clouds[i].sprite,Is.SameAs(poses[i]));
            Assert.That(Vector2.Distance(ProjectedVisible(clouds[i]).center,before[i].center),Is.LessThan(.005f),
                "Changing camera projection must not teleport the billow at the lens.");
        }
        var animation=new HashSet<Sprite>();
        for(float t=1.505f;t<1.82f;t+=.015f){stage.Tick(t,0f);if(clouds[0].enabled)animation.Add(clouds[0].sprite);}
        Assert.That(animation.Count,Is.GreaterThanOrEqualTo(4));
        stage.Tick(1.82f,0f);Assert.That(clouds.Any(c=>c.enabled),Is.False);
        stage.Tick(4.70f,0f);Assert.That(clouds.Any(c=>c.enabled),Is.False,"Entry dust may never replace the outgoing sole wipe.");
    }

    private Rect ProjectedArt(SpriteRenderer renderer,Rect area)
    {
        Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            float px=((x==0?area.xMin:area.xMax)-.5f)*renderer.sprite.rect.width/renderer.sprite.pixelsPerUnit;
            float py=((y==0?area.yMin:area.yMax)-.5f)*renderer.sprite.rect.height/renderer.sprite.pixelsPerUnit;
            Vector3 point=camera.WorldToViewportPoint(renderer.transform.TransformPoint(new Vector3(renderer.flipX?-px:px,py,0f)));
            min=Vector2.Min(min,point);max=Vector2.Max(max,point);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    private Rect ProjectedVisible(SpriteRenderer renderer)
    {
        Bounds body=VisibleBody(renderer.sprite);Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            var local=new Vector3(x==0?body.min.x:body.max.x,y==0?body.min.y:body.max.y,0f);
            if(renderer.flipX)local.x=-local.x;
            Vector3 point=camera.WorldToViewportPoint(renderer.transform.TransformPoint(local));
            min=Vector2.Min(min,point);max=Vector2.Max(max,point);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    private float ProjectedHeight(SpriteRenderer renderer)
    {
        Bounds body=VisibleBody(renderer.sprite);
        Vector3 low=camera.WorldToViewportPoint(renderer.transform.TransformPoint(new Vector3(body.center.x,body.min.y,0f)));
        Vector3 high=camera.WorldToViewportPoint(renderer.transform.TransformPoint(new Vector3(body.center.x,body.max.y,0f)));
        return high.y-low.y;
    }

    private Color32[] PixelsWithOnly(IEnumerable<Renderer> selected)
    {
        var allowed=new HashSet<Renderer>(selected);var renderers=root.GetComponentsInChildren<Renderer>(true);
        bool[] enabled=renderers.Select(r=>r.enabled).ToArray();Color clear=camera.backgroundColor;
        try
        {
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i]&&allowed.Contains(renderers[i]);
            camera.backgroundColor=Color.clear;return Pixels();
        }
        finally
        {
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];
            camera.backgroundColor=clear;
        }
    }

    private Color32[] Pixels()
    {
        camera.Render();RenderTexture previous=RenderTexture.active;
        var target=RenderTexture.GetTemporary(160,90,0,RenderTextureFormat.ARGB32);
        var image=new Texture2D(160,90,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(stage.Texture,target);RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,160,90),0,0);image.Apply();return image.GetPixels32();
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
    }

    private Vector3 VisibleFeet(SpriteRenderer renderer)
    {
        Bounds body=VisibleBody(renderer.sprite);
        return root.InverseTransformPoint(renderer.transform.TransformPoint(new Vector3(
            renderer.flipX?-body.center.x:body.center.x,body.min.y,0f)));
    }

    private static Bounds VisibleBody(Sprite image)=>(Bounds)typeof(PixelMantisReaper)
        .GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{image});

    private static ulong BodyShape(Sprite image)
    {
        Bounds bounds=VisibleBody(image);Color[] pixels=image.texture.GetPixels((int)image.rect.x,(int)image.rect.y,(int)image.rect.width,(int)image.rect.height);
        int width=(int)image.rect.width,height=(int)image.rect.height;ulong hash=14695981039346656037UL;
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            int px=Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(bounds.min.x,bounds.max.x,(x+.5f)/64f)*image.pixelsPerUnit+image.pivot.x),0,width-1);
            int py=Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(bounds.min.y,bounds.max.y,(y+.5f)/64f)*image.pixelsPerUnit+image.pivot.y),0,height-1);
            hash=unchecked((hash^(pixels[py*width+px].a>.5f?1UL:0UL))*1099511628211UL);
        }
        return hash;
    }

    private static Vector3 VisibleAverage(Sprite image)
    {
        Color[] pixels=image.texture.GetPixels((int)image.rect.x,(int)image.rect.y,(int)image.rect.width,(int)image.rect.height);
        Vector3 total=Vector3.zero;int count=0;
        foreach(Color p in pixels)if(p.a>.5f){total+=new Vector3(p.r,p.g,p.b);count++;}
        Assert.That(count,Is.GreaterThan(128));return total/count;
    }
}
