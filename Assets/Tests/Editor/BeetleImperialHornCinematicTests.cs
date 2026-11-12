using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class BeetleImperialHornCinematicTests
{
    private const string ArtRoot="CombatCinematics/BeetleImperialHornV1/";
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private GameObject host;
    private BeetleOverturnRank4Stage stage;
    private Rank4StageContext context;
    private Camera View=>host.GetComponentInChildren<Camera>(true);

    [SetUp]public void Setup()
    {
        host=new GameObject("Imperial horn storyboard tests");
        var definitions=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters;
        Func<string,FrogCatchCinematicStage.Actor> actor=id=>
        {
            var definition=definitions.First(d=>d!=null&&d.monsterId==id);
            var driver=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            return new FrogCatchCinematicStage.Actor{species=id,sprite=driver.IdleFrames[0],frames=driver.IdleFrames,color=Color.white,size=1f};
        };
        var source=actor("jangpung");
        var targets=new[]{"woongi","velos","geomchi","tino","chundongi","aranea","haeta","kogason"}.Select(actor).ToArray();
        Vector2[] positions={new Vector2(.20f,.45f),new Vector2(.45f,.45f),new Vector2(.72f,.45f),
            new Vector2(.20f,0f),new Vector2(.48f,0f),new Vector2(.75f,0f),new Vector2(.35f,-.45f),new Vector2(.70f,-.45f)};
        for(int i=0;i<targets.Length;i++){targets[i].ground=positions[i];targets[i].flipX=true;}
        context=new Rank4StageContext{family="beetle",source=source,targets=targets,returnSprite=source.sprite,
            returnPosition=new Vector2(-2f,-2f),returnHeight=1.3f};
        stage=new BeetleOverturnRank4Stage(host.transform);Assert.That(stage.Begin(context),Is.True);
    }

    [TearDown]public void Cleanup(){stage?.Dispose();Object.DestroyImmediate(host);}
    private SpriteRenderer Lead=>host.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Lead imperial horn beetle");

    [TestCase("jangpung",false,0)][TestCase("noepung",false,1)][TestCase("atlagon",false,2)]
    [TestCase("solarbeetle",false,3)][TestCase("jangpung",true,4)]
    public void AllActualSpeciesUseEightIndependentDrawnPosesAtACommonBaseScaleWithTheSharedChargeImpulse(string species,bool shiny,int speciesIndex)
    {
        var actor=context.source;actor.species=species;actor.shiny=shiny;context.source=actor;Assert.That(stage.Begin(context),Is.True);
        var atlas=Resources.Load<Texture2D>(ArtRoot+"Body40");Assert.That(atlas,Is.Not.Null);
        var frames=new HashSet<int>();Vector3? scale=null;
        for(float time=.12f;time<3.40f;time+=.01f)
        {
            stage.Tick(time,0f);Assert.That(Lead.enabled,Is.True);Assert.That(Lead.sprite.texture,Is.SameAs(atlas));
            int index=CellIndex(Lead.sprite,4,10);Assert.That(index,Is.InRange(speciesIndex*8,speciesIndex*8+7));frames.Add(index);
            Vector3 impulse=BeetleOverturnRules.SourceScale(stage.ActionTime);
            Assert.That(Mathf.Min(impulse.x,Mathf.Min(impulse.y,impulse.z)),Is.GreaterThan(0f));
            Vector3 baseline=Vector3.Scale(Lead.transform.localScale,new Vector3(1f/impulse.x,1f/impulse.y,1f/impulse.z));
            if(!scale.HasValue)scale=baseline;
            Assert.That(Vector3.Distance(baseline,scale.Value),Is.LessThan(.001f),"Only the shared charge/release impulse may change the authored common body scale.");
        }
        Assert.That(frames.Count,Is.EqualTo(8),"Crouch, brace, lift, fling and recovery need actual different drawings.");
        AssertDistinctCells(atlas,4,10,speciesIndex*8,8);
    }

    [TestCase("Horn8",4,2,8)][TestCase("Earth4",4,1,4)][TestCase("Impact8",4,2,8)]
    public void AllAuthoredEffectFramesAreDistinctAndActuallyPlayed(string name,int columns,int rows,int count)
    {
        var atlas=Resources.Load<Texture2D>(ArtRoot+name);Assert.That(atlas,Is.Not.Null);
        Assert.That(atlas.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(atlas.mipmapCount,Is.EqualTo(1));
        AssertDistinctCells(atlas,columns,rows,0,count);
        var frames=new HashSet<int>();
        for(float time=.12f;time<3.70f;time+=.01f)
        {
            stage.Tick(time,0f);
            foreach(var renderer in host.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled&&r.sprite!=null&&r.sprite.texture==atlas))
                frames.Add(CellIndex(renderer.sprite,columns,rows));
        }
        Assert.That(frames.Count,Is.EqualTo(count),"The actual effect must play the authored frames rather than moving one still image.");
    }

    [TestCase(false)][TestCase(true)]
    public void AirborneEnemiesReallyCompleteSomersaultsWithoutChangingTheCapturedGroundFormation(bool enemy)
    {
        context.enemy=enemy;var source=context.source;source.flipX=enemy;context.source=source;Assert.That(stage.Begin(context),Is.True);
        Vector2[] ground=context.targets.Select(actor=>actor.ground).ToArray();
        var previous=new Dictionary<string,float>();var turns=new Dictionary<string,float>();
        for(float time=1.30f;time<2.90f;time+=.008f)
        {
            stage.Tick(time,0f);Assert.That(context.targets.Select(actor=>actor.ground),Is.EqualTo(ground));
            foreach(var renderer in host.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled&&r.name.StartsWith("Spinning imperial horn victim ",StringComparison.Ordinal)))
            {
                float angle=(Quaternion.Inverse(View.transform.localRotation)*renderer.transform.localRotation).eulerAngles.z;
                float last;if(previous.TryGetValue(renderer.name,out last))turns[renderer.name]+=Mathf.DeltaAngle(last,angle);
                else turns[renderer.name]=0f;
                previous[renderer.name]=angle;
            }
        }
        Assert.That(turns.Count,Is.EqualTo(8));
        foreach(var turn in turns)Assert.That(Mathf.Abs(turn.Value),Is.GreaterThan(1000f),turn.Key+" must complete at least three continuous somersaults.");
    }

    [TestCase("PrimordialMeadow")][TestCase("PoisonFogSwamp")][TestCase("CrimsonJungle")]
    [TestCase("FrozenHighlands")][TestCase("SkySanctuary")]
    public void EveryMapUsesItsOwnContinuousThreeDimensionalFloorAndCurvedPanorama(string region)
    {
        context.sceneryRegion=region;Assert.That(stage.Begin(context),Is.True);stage.Tick(1.62f,0f);
        var scenery=(FrogCatchCinematicEnvironment)typeof(BeetleOverturnRank4Stage).GetFields(Hidden)
            .Single(field=>field.FieldType==typeof(FrogCatchCinematicEnvironment)).GetValue(stage);
        Assert.That(scenery.Active,Is.True);Assert.That(scenery.Region,Is.EqualTo(region));
        Assert.That(scenery.Kit,Is.SameAs(Resources.Load<Texture2D>(FrogCatchCinematicEnvironment.FloorResourceRoot+region)));
        Assert.That(scenery.Panorama,Is.SameAs(Resources.Load<Texture2D>(FrogCatchCinematicEnvironment.PanoramaResourceRoot+region)));
        Assert.That(View.orthographic,Is.False);
        Assert.That(host.GetComponentsInChildren<MeshRenderer>(true).Any(renderer=>renderer.name=="Flexible local battlefield wave"&&renderer.enabled),Is.True);
    }

    [Test]public void AnImmuneVictimStaysOnTheGroundDuringTheSameCinematicThatThrowsOtherEnemies()
    {
        context.beetleLiftAllowed=new[]{false,true,true,true,true,true,true,true};
        Assert.That(stage.Begin(context),Is.True);stage.Tick(2.18f,0f);
        var renderers=host.GetComponentsInChildren<SpriteRenderer>(true);
        var immune=renderers.Single(renderer=>renderer.name=="Spinning imperial horn victim 0");
        var lifted=renderers.Single(renderer=>renderer.name=="Spinning imperial horn victim 1");
        float ground=View.transform.parent.position.y;
        Assert.That(immune.bounds.center.y-ground,Is.LessThan(2f));
        Assert.That(lifted.bounds.center.y-ground,Is.GreaterThan(3f));
        float immuneRoll=(Quaternion.Inverse(View.transform.localRotation)*immune.transform.localRotation).eulerAngles.z;
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f,immuneRoll)),Is.LessThan(.01f));
    }

    [Test]public void ReturnUsesOnlyTheActualCurrentSourcePoseAndEndsWithNoPrivateSceneryOrVictimClones()
    {
        stage.Tick(3.93f,0f);Assert.That(View.backgroundColor.a,Is.Zero);Assert.That(View.orthographic,Is.True);
        Assert.That(Lead.sprite,Is.SameAs(context.returnSprite));Assert.That(Lead.enabled,Is.True);
        Assert.That(host.GetComponentsInChildren<SpriteRenderer>(true).Any(r=>r.enabled&&r.name.StartsWith("Spinning imperial horn victim ",StringComparison.Ordinal)),Is.False);
        context.returnBodyVisible=false;stage.Tick(3.93f,0f);Assert.That(Lead.enabled,Is.False);
        stage.Tick(stage.Duration,0f);Assert.That(stage.Opacity,Is.LessThan(.01f));
        stage.Hide();Assert.That(stage.Active,Is.False);
    }

    private void UseRealCloseMeleeCluster(bool enemy=false)
    {
        float side=enemy?-1f:1f;
        var source=context.source;source.ground=new Vector2(-.11f*side,.05f);source.flipX=enemy;context.source=source;context.enemy=enemy;
        Vector2[] positions={new Vector2(-.29f,-.18f),new Vector2(-.27f,.18f),new Vector2(-.15f,.43f),
            new Vector2(.04f,.32f),new Vector2(.05f,.02f),new Vector2(.02f,-.23f),new Vector2(-.11f,-.43f),new Vector2(-.25f,-.38f)};
        for(int i=0;i<context.targets.Length;i++)
        {var target=context.targets[i];target.ground=new Vector2(positions[i].x*side,positions[i].y);target.flipX=!enemy;context.targets[i]=target;}
        Assert.That(stage.Begin(context),Is.True);
    }

    [TestCase(false)][TestCase(true)]
    public void RealSurroundingMeleeGroupsKeepEveryOpaqueSpinningBodyInsideTheSkyFrame(bool enemy)
    {
        UseRealCloseMeleeCluster(enemy);Vector2[] positions=context.targets.Select(actor=>actor.ground).ToArray();stage.Tick(2.18f,0f);
        var actors=host.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled&&r.name.StartsWith("Spinning imperial horn victim ",StringComparison.Ordinal)).ToArray();
        Assert.That(actors.Length,Is.EqualTo(8));
        foreach(var actor in actors)
        {
            Bounds body=VisibleBody(actor.sprite);
            for(int corner=0;corner<4;corner++)
            {
                Vector3 local=new Vector3(corner%2==0?body.min.x:body.max.x,corner<2?body.min.y:body.max.y,0f);
                if(actor.flipX)local.x=-local.x;
                Vector3 point=View.WorldToViewportPoint(actor.transform.TransformPoint(local));
                Assert.That(point.z,Is.GreaterThan(.1f));
                Assert.That(point.x,Is.InRange(.015f,.985f),actor.name+" horizontal crop");
                Assert.That(point.y,Is.InRange(.015f,.985f),actor.name+" vertical crop");
            }
        }
        Assert.That(context.targets.Select(actor=>actor.ground),Is.EqualTo(positions),"Camera adaptation cannot invent separated ground positions.");
    }

    [Test]public void RealCloseMeleeUpheavalAndTurfStayUnderTheActualVictimsInsteadOfAnEmptyDistantLane()
    {
        UseRealCloseMeleeCluster();stage.Tick(1.62f,0f);
        var wave=host.GetComponentsInChildren<MeshFilter>(true).Single(filter=>filter.name=="Flexible local battlefield wave");
        Bounds area=wave.sharedMesh.bounds;
        Assert.That(area.max.x,Is.LessThan(2.4f));Assert.That(area.min.x,Is.LessThan(-1.4f));
        foreach(var actor in context.targets)
        {
            Vector2 relative=actor.ground-context.source.ground;
            Assert.That(relative.x*9f,Is.InRange(area.min.x,area.max.x));
            Assert.That(relative.y*4f,Is.InRange(area.min.z,area.max.z));
        }
        foreach(var turf in host.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled&&r.name.StartsWith("Layered rising turf ",StringComparison.Ordinal)))
        {
            Vector3 point=View.transform.parent.InverseTransformPoint(turf.bounds.center);
            float distance=context.targets.Min(actor=>Vector2.Distance(new Vector2(point.x,point.z),
                new Vector2((actor.ground.x-context.source.ground.x)*9f,(actor.ground.y-context.source.ground.y)*4f)));
            Assert.That(distance,Is.LessThan(1.1f),"Flying turf must originate near an actual victim's ground footprint.");
        }
    }

    [Test]public void SomersaultsRotateAroundTheOpaqueBodyCenterInsteadOfTransparentSpritePadding()
    {
        UseRealCloseMeleeCluster();stage.Tick(2.18f,0f);
        var victim=host.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Spinning imperial horn victim 0");
        Vector3 local=VisibleBody(victim.sprite).center;if(victim.flipX)local.x=-local.x;
        Vector3 before=victim.transform.TransformPoint(local);
        typeof(BeetleOverturnRank4Stage).GetMethod("RotateAtCenter",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{victim,137f});
        Assert.That(Vector3.Distance(before,victim.transform.TransformPoint(local)),Is.LessThan(.001f));
    }

    private SpriteRenderer[] Victims()=>host.GetComponentsInChildren<SpriteRenderer>(true)
        .Where(renderer=>renderer.name.StartsWith("Spinning imperial horn victim ",StringComparison.Ordinal))
        .OrderBy(renderer=>int.Parse(renderer.name.Substring("Spinning imperial horn victim ".Length))).ToArray();

    private Vector3 VisibleCenter(SpriteRenderer renderer)
    {
        Vector3 center=VisibleBody(renderer.sprite).center;if(renderer.flipX)center.x=-center.x;
        return View.transform.parent.InverseTransformPoint(renderer.transform.TransformPoint(center));
    }

    [TestCase(false)][TestCase(true)]
    public void HigherSeededThrowsHaveDifferentPeaksAndMeetTheirOwnGroundContinuously(bool enemy)
    {
        UseRealCloseMeleeCluster(enemy);Vector2[] ground=context.targets.Select(actor=>actor.ground).ToArray();
        var peaks=new float[context.targets.Length];
        for(float time=1.32f;time<2.90f;time+=.01f)
        {
            stage.Tick(time,0f);var victims=Victims();Assert.That(victims.Length,Is.EqualTo(8));
            for(int i=0;i<victims.Length;i++)
            {
                Vector3 center=VisibleCenter(victims[i])-View.transform.localRotation*Vector3.up*(.7f*context.targets[i].size);
                peaks[i]=Mathf.Max(peaks[i],center.y);
            }
        }
        Assert.That(peaks.Min(),Is.GreaterThan(6f),"Every thrown body must visibly rise much higher than the former roughly3.5world lift.");
        Assert.That(peaks.Max()-peaks.Min(),Is.GreaterThan(.25f),"Individuals need small deterministic differences in peak height.");
        stage.Tick(2.899f,0f);var before=Victims().Select(VisibleCenter).ToArray();
        var lastAngles=Victims().Select(renderer=>(Quaternion.Inverse(View.transform.localRotation)*renderer.transform.localRotation).eulerAngles.z).ToArray();
        stage.Tick(2.90f,0f);var landed=Victims();
        for(int i=0;i<landed.Length;i++)
        {
            Assert.That(Vector3.Distance(before[i],VisibleCenter(landed[i])),Is.LessThan(.12f),"No final-frame positional snap at landing.");
            float angle=(Quaternion.Inverse(View.transform.localRotation)*landed[i].transform.localRotation).eulerAngles.z;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(lastAngles[i],angle)),Is.LessThan(10f),"Whole rotations must reach the landing orientation continuously.");
            Vector2 relative=context.targets[i].ground-context.source.ground;
            Vector3 feet=VisibleCenter(landed[i])-View.transform.localRotation*Vector3.up*(.7f*context.targets[i].size);
            Assert.That(Vector3.Distance(feet,new Vector3(relative.x*9f,0f,relative.y*4f)),Is.LessThan(.003f));
        }
        Assert.That(context.targets.Select(actor=>actor.ground),Is.EqualTo(ground));
    }

    [TestCase(false)][TestCase(true)]
    public void ChargeSquashAndFastReleaseMoveTheBeetleAndItsHornAroundTheSameFootAnchor(bool enemy)
    {
        UseRealCloseMeleeCluster(enemy);var atlas=Resources.Load<Texture2D>(ArtRoot+"Body40");
        Sprite anchor=stage.ArtworkFrames.Where(sprite=>sprite.texture==atlas).ElementAt(7);
        Bounds body=VisibleBody(anchor);Vector3 foot=new Vector3(enemy?-body.center.x:body.center.x,body.min.y,0f);
        foreach(float time in new[]{.95f,1.45f})
        {
            stage.Tick(time,0f);Vector3 pose=BeetleOverturnRules.SourcePose(stage.ActionTime)*1.55f;pose.x*=enemy?-1f:1f;
            Vector3 expected=View.transform.localRotation*pose;
            Vector3 actual=View.transform.parent.InverseTransformPoint(Lead.transform.TransformPoint(foot));
            Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.003f),"The pose impulse must use the planted feet, not the sprite cell center.");
            var horn=host.GetComponentsInChildren<SpriteRenderer>(true).Single(renderer=>renderer.name=="Continuous forked imperial horn");
            float angle=(Quaternion.Inverse(View.transform.localRotation)*horn.transform.localRotation).eulerAngles.z;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(pose.y/1.55f*180f*(enemy?-1f:1f),angle)),Is.LessThan(.01f));
        }
        stage.Tick(.95f,0f);Assert.That(BeetleOverturnRules.SourceScale(stage.ActionTime).y,Is.LessThan(.9f));
        stage.Tick(1.45f,0f);Assert.That(BeetleOverturnRules.SourceScale(stage.ActionTime).y,Is.GreaterThan(1.1f));
    }

    private static Bounds VisibleBody(Sprite sprite)=>(Bounds)typeof(PixelMantisReaper)
        .GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{sprite});

    private static int CellIndex(Sprite sprite,int columns,int rows)=>
        (rows-1-Mathf.RoundToInt(sprite.rect.y/(sprite.texture.height/(float)rows)))*columns+
        Mathf.RoundToInt(sprite.rect.x/(sprite.texture.width/(float)columns));

    private static void AssertDistinctCells(Texture2D atlas,int columns,int rows,int first,int count)
    {
        int width=atlas.width/columns,height=atlas.height/rows;var pixels=atlas.GetPixels32();var signatures=new HashSet<string>();
        using(var sha=SHA256.Create())for(int frame=first;frame<first+count;frame++)
        {
            var rgba=new byte[width*height*4];int x0=frame%columns*width,y0=(rows-1-frame/columns)*height,index=0,opaque=0;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {Color32 color=pixels[(y0+y)*atlas.width+x0+x];rgba[index++]=color.r;rgba[index++]=color.g;rgba[index++]=color.b;rgba[index++]=color.a;if(color.a>64)opaque++;}
            Assert.That(opaque,Is.GreaterThan(500));Assert.That(signatures.Add(BitConverter.ToString(sha.ComputeHash(rgba))),Is.True,"Duplicate authored frame "+frame);
        }
    }
}
