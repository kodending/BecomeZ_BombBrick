using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class NinjaSpacetimeCinematicTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    private GameObject host;
    private Texture2D texture,other;
    private Sprite sprite;
    private NinjaSubstitutionRank4Stage stage;
    private Rank4StageContext context;
    [SetUp]public void Setup()
    {
        host=new GameObject("Ninja spatial rendering fixture");texture=Plate(Color.green);other=Plate(Color.cyan);
        sprite=Sprite.Create(texture,new Rect(0,0,4,4),Vector2.one*.5f,4f);
        var source=new FrogCatchCinematicStage.Actor{species="chundongi",sprite=sprite,color=Color.white,size=1f};
        context=new Rank4StageContext{family="ninja_cat",source=source,battlefieldBackdrop=texture,
            returnPosition=new Vector2(-2,-2),returnHeight=1.3f};
        context.targets=Enumerable.Range(0,8).Select(i=>new FrogCatchCinematicStage.Actor{species="velos",sprite=sprite,
            ground=new Vector2((i%4)*.30f,(i/4)*.6f),size=.8f,color=Color.white,flipX=true}).ToArray();
        stage=new NinjaSubstitutionRank4Stage(host.transform);Assert.That(stage.Begin(context),Is.True);
    }
    [TearDown]public void Cleanup()
    {stage?.Dispose();Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);Object.DestroyImmediate(other);Object.DestroyImmediate(host);}
    private static Texture2D Plate(Color color)
    {var image=new Texture2D(4,4);image.SetPixels(Enumerable.Repeat(color,16).ToArray());image.Apply();return image;}
    private T Read<T>(string field)=>(T)stage.GetType().GetField(field,Hidden).GetValue(stage);

    private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    private static float SignedArea(IList<Vector2> polygon)
    {
        float twice=0f;for(int i=0;i<polygon.Count;i++)twice+=Cross(polygon[i],polygon[(i+1)%polygon.Count]);
        return twice*.5f;
    }
    private static float IntersectionArea(Vector2[] subject,Vector2[] clip)
    {
        var result=new List<Vector2>(subject);float winding=Mathf.Sign(SignedArea(clip));
        for(int edge=0;edge<clip.Length&&result.Count>0;edge++)
        {
            var input=result;result=new List<Vector2>();Vector2 a=clip[edge],direction=clip[(edge+1)%clip.Length]-a;
            Vector2 previous=input[input.Count-1];float before=Cross(direction,previous-a)*winding;
            foreach(Vector2 point in input)
            {
                float after=Cross(direction,point-a)*winding;
                if((after>=0f)!=(before>=0f))result.Add(Vector2.LerpUnclamped(previous,point,before/(before-after)));
                if(after>=0f)result.Add(point);previous=point;before=after;
            }
        }
        return result.Count<3?0f:Mathf.Abs(SignedArea(result));
    }
    private static Vector2[] RemoveLinearDrift(IList<Vector2> samples)
    {
        Vector2 mean=Vector2.zero;foreach(Vector2 point in samples)mean+=point;mean/=samples.Count;
        float middle=(samples.Count-1)*.5f,denominator=0f;Vector2 slope=Vector2.zero;
        for(int i=0;i<samples.Count;i++){float t=i-middle;denominator+=t*t;slope+=(samples[i]-mean)*t;}
        slope/=denominator;var residual=new Vector2[samples.Count];
        for(int i=0;i<samples.Count;i++)residual[i]=samples[i]-mean-slope*(i-middle);
        return residual;
    }
    private static float Correlation(Vector2[] samples,int lag,bool y)
    {
        int count=samples.Length-lag;float a=0f,b=0f;
        for(int i=0;i<count;i++){a+=y?samples[i].y:samples[i].x;b+=y?samples[i+lag].y:samples[i+lag].x;}
        a/=count;b/=count;float numerator=0f,first=0f,second=0f;
        for(int i=0;i<count;i++)
        {
            float x=(y?samples[i].y:samples[i].x)-a,z=(y?samples[i+lag].y:samples[i+lag].x)-b;
            numerator+=x*z;first+=x*x;second+=z*z;
        }
        return first*second<=.000001f?0f:numerator/Mathf.Sqrt(first*second);
    }
    private static bool MaterialDepthState(Material material,string property,string command,string value,int numeric)
    {
        if(material.HasProperty(property))return Mathf.RoundToInt(material.GetFloat(property))==numeric;
        string path=AssetDatabase.GetAssetPath(material.shader);
        if(!string.IsNullOrEmpty(path))
        {
            if(!Path.IsPathRooted(path))path=Path.Combine(Path.GetDirectoryName(Application.dataPath),path);
            if(File.Exists(path))return Regex.IsMatch(File.ReadAllText(path),@"\b"+command+@"\s+"+value+@"\b",RegexOptions.IgnoreCase);
        }
        // Unity's built-in sprite pass uses ZWrite Off and the default LEqual test.
        return material.shader.name=="Sprites/Default"&&command=="ZWrite"&&value=="Off";
    }

    [Test]public void IrregularConvexCellsPartitionTheWholeMapWithoutOverlap()
    {
        var cells=Read<Vector2[][]>("Faces");Assert.That(cells.Length,Is.EqualTo(9));
        var areas=new List<float>();var vertexCounts=new HashSet<int>();
        foreach(Vector2[] cell in cells)
        {
            Assert.That(cell.Length,Is.InRange(3,10));vertexCounts.Add(cell.Length);
            float orientation=Mathf.Sign(SignedArea(cell));float area=Mathf.Abs(SignedArea(cell));
            Assert.That(area,Is.GreaterThan(.001f));areas.Add(area);
            for(int i=0;i<cell.Length;i++)
            {
                Assert.That(cell[i].x,Is.InRange(-.00001f,1.00001f));Assert.That(cell[i].y,Is.InRange(-.00001f,1.00001f));
                Vector2 edge=cell[(i+1)%cell.Length]-cell[i],next=cell[(i+2)%cell.Length]-cell[(i+1)%cell.Length];
                Assert.That(Cross(edge,next)*orientation,Is.GreaterThanOrEqualTo(-.00001f),"Every cell must remain convex.");
            }
        }
        Assert.That(vertexCounts.Count,Is.GreaterThanOrEqualTo(2));
        Assert.That(areas.Max()/areas.Min(),Is.GreaterThan(1.20f));
        Assert.That(areas.Sum(),Is.EqualTo(1f).Within(.0001f),"Map UVs must cover the whole source picture exactly once.");
        for(int i=0;i<cells.Length;i++)for(int j=i+1;j<cells.Length;j++)
            Assert.That(IntersectionArea(cells[i],cells[j]),Is.LessThan(.00001f),"Cells "+i+" and "+j+" overlap.");
    }

    [Test]public void NativeRenderAndSpeciesEyesUseTheNewOriginalArt()
    {
        Assert.That(stage.Texture.width,Is.EqualTo(1920));Assert.That(stage.Texture.height,Is.EqualTo(1080));
        string[] names={"chundongi","seolmyo","sundongi","hongryeon"};
        for(int i=0;i<names.Length;i++)
        {
            var actor=context.source;actor.species=names[i];context.source=actor;
            Assert.That(stage.Begin(context),Is.True);stage.Tick(.85f,0f);
            var eye=Read<SpriteRenderer>("leadEye");
            Assert.That(eye.enabled,Is.True);Assert.That(eye.sprite.name,Does.StartWith(NinjaSubstitutionRank4Stage.ResourceRoot+"Eyes "+i+":"));
        }
        Assert.That(stage.ArtworkFrames.All(s=>s.name.StartsWith(NinjaSubstitutionRank4Stage.ResourceRoot,StringComparison.Ordinal)),Is.True);
    }

    [TestCase(false)][TestCase(true)]
    public void AReusedStageUsesTheCurrentMapAndMovesItsTexturedSurfacesInDepth(bool enemy)
    {
        context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
        stage.Tick(2.02f,0f);var progress=Read<float[]>("layerProgress");
        Assert.That(progress.Max()-progress.Min(),Is.GreaterThan(.05f),"Refraction must start at different times for different pieces.");
        stage.Tick(2.45f,0f);var compression=Read<float[]>("compressionProgress");
        Assert.That(compression.Max()-compression.Min(),Is.GreaterThan(.01f),"Compression cannot run every piece in lockstep.");
        stage.Tick(1.65f,0f);var space=Read<MeshRenderer>("space");
        var block=new MaterialPropertyBlock();space.GetPropertyBlock(block);
        Assert.That(block.GetTexture("_MainTex"),Is.SameAs(texture));
        Vector3[] before=Read<Vector3[]>("spaceVertices").ToArray();
        stage.Tick(2.80f,0f);Vector3[] after=Read<Vector3[]>("spaceVertices");
        Assert.That(before.Where((v,i)=>Vector3.Distance(v,after[i])>.15f).Count(),Is.GreaterThan(after.Length/2));
        Assert.That(after.Max(v=>v.z)-after.Min(v=>v.z),Is.GreaterThan(1f));
        Assert.That(after.Max(v=>v.y)-after.Min(v=>v.y),Is.GreaterThan(.25f),"Floor fragments need real vertical depth, not only an XZ footprint.");
        var cells=Read<Vector2[][]>("Faces");var rotations=Read<Quaternion[]>("rotations");var scales=Read<Vector3[]>("scales");
        float angularSpread=0f;
        for(int i=0;i<rotations.Length;i++)for(int j=i+1;j<rotations.Length;j++)
            angularSpread=Mathf.Max(angularSpread,Quaternion.Angle(rotations[i],rotations[j]));
        Assert.That(angularSpread,Is.GreaterThan(1f));
        Assert.That(scales.Max(v=>v.x)-scales.Min(v=>v.x),Is.GreaterThan(.02f),"Pieces must compress by different amounts.");

        Mesh mesh=space.GetComponent<MeshFilter>().sharedMesh;Vector2[] mapUV=mesh.uv;
        Rect floor=Read<Rect>("FloorUV");
        Assert.That(floor.xMin,Is.GreaterThanOrEqualTo(.15f));Assert.That(floor.xMax,Is.LessThanOrEqualTo(.85f));
        Assert.That(floor.yMin,Is.GreaterThanOrEqualTo(.25f));Assert.That(floor.yMax,Is.LessThanOrEqualTo(.80f));
        Assert.That(floor.width,Is.InRange(.35f,.89f));Assert.That(floor.height,Is.InRange(.20f,.59f));
        Vector3 origin=(Vector3)stage.GetType().GetMethod("MapPoint",Hidden).Invoke(stage,new object[]{Vector2.zero});
        Vector3 across=(Vector3)stage.GetType().GetMethod("MapPoint",Hidden).Invoke(stage,new object[]{Vector2.right})-origin;
        Vector3 away=(Vector3)stage.GetType().GetMethod("MapPoint",Hidden).Invoke(stage,new object[]{Vector2.up})-origin;
        Assert.That(Mathf.Abs(Vector3.Dot(Vector3.Cross(across,away).normalized,Vector3.up)),Is.GreaterThan(.999f),
            "The broken arena must be an XZ floor, not an upright photograph.");
        var thickness=Read<float[]>("thickness");var sides=Read<MeshRenderer>("sides");var sideVertices=Read<Vector3[]>("sideVertices");
        Assert.That(sides.enabled,Is.True);Assert.That(thickness.Length,Is.EqualTo(cells.Length));
        Assert.That(mesh.vertexCount,Is.EqualTo(cells.Sum(c=>c.Length)));
        Assert.That(mesh.triangles.Length,Is.EqualTo(cells.Sum(c=>(c.Length-2)*3)));
        Assert.That(sideVertices.Length,Is.EqualTo(mesh.vertexCount*4));
        int cursor=0;
        for(int face=0;face<cells.Length;face++)
        {
            Assert.That(thickness[face],Is.GreaterThan(.015f));
            for(int edge=0;edge<cells[face].Length;edge++)
            {
                int vertex=cursor+edge,next=cursor+(edge+1)%cells[face].Length,side=vertex*4;
                Vector2 expectedUV=new Vector2(Mathf.Lerp(floor.xMin,floor.xMax,enemy?1f-cells[face][edge].x:cells[face][edge].x),
                    Mathf.Lerp(floor.yMin,floor.yMax,cells[face][edge].y));
                Assert.That(Vector2.Distance(mapUV[vertex],expectedUV),Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(sideVertices[side],after[vertex]),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(sideVertices[side+1],after[next]),Is.LessThan(.001f));
                Vector3 back=-(rotations[face]*Vector3.up)*thickness[face];
                Assert.That(Vector3.Distance(sideVertices[side+2]-sideVertices[side+1],back),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(sideVertices[side+3]-sideVertices[side],back),Is.LessThan(.001f));
            }
            cursor+=cells[face].Length;
        }
        context.battlefieldBackdrop=other;Assert.That(stage.Begin(context),Is.True);stage.Tick(1.65f,0f);
        space.GetPropertyBlock(block);Assert.That(block.GetTexture("_MainTex"),Is.SameAs(other));
    }

    [Test]public void StageFragmentsAppearWithDelaysAndTravelThroughDepthBeforeTheImpact()
    {
        stage.Tick(NinjaSubstitutionRank4Stage.TerrainSnapTime-.001f,0f);var fragments=Read<SpriteRenderer[]>("fragments");
        Assert.That(fragments.Any(f=>f.enabled),Is.False,"Preparation must finish before terrain fragments appear.");
        stage.Tick(2.00f,0f);int early=fragments.Count(f=>f.enabled);
        stage.Tick(2.40f,0f);int later=fragments.Count(f=>f.enabled);
        Assert.That(later,Is.GreaterThan(early));Assert.That(later,Is.GreaterThanOrEqualTo(8));
        var active=fragments.Where(f=>f.enabled).ToArray();Vector3[] positions=active.Select(f=>f.transform.localPosition).ToArray();
        Quaternion[] rotations=active.Select(f=>f.transform.localRotation).ToArray();
        Assert.That(positions.Max(v=>v.z)-positions.Min(v=>v.z),Is.GreaterThan(.10f));
        stage.Tick(2.70f,0f);int moved=0;float turned=0f;
        for(int i=0;i<active.Length;i++)if(active[i].enabled)
        {
            if(Vector3.Distance(positions[i],active[i].transform.localPosition)>.02f)moved++;
            turned=Mathf.Max(turned,Quaternion.Angle(rotations[i],active[i].transform.localRotation));
        }
        Assert.That(moved,Is.GreaterThanOrEqualTo(4));Assert.That(turned,Is.GreaterThan(.5f));
    }

    [TestCase(false)][TestCase(true)]
    public void FastHandSealsFinishBeforeTheTerrainStartsMoving(bool enemy)
    {
        context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
        Assert.That(NinjaSubstitutionRank4Stage.SealFrameRate,Is.EqualTo(22f));
        stage.Tick(NinjaSubstitutionRank4Stage.SealStartTime,0f);Vector3[] settled=Read<Vector3[]>("spaceVertices").ToArray();
        var seen=new HashSet<int>();int lastPose=-1,changes=0;
        int beats=Mathf.FloorToInt((NinjaSubstitutionRank4Stage.SealEndTime-NinjaSubstitutionRank4Stage.SealStartTime)*22f);
        Assert.That(beats,Is.GreaterThanOrEqualTo(15));
        for(int beat=0;beat<beats;beat++)
        {
            float time=NinjaSubstitutionRank4Stage.SealStartTime+(beat+.5f)/22f;stage.Tick(time,1f/22f);
            var lead=Read<SpriteRenderer>("lead");Assert.That(lead.enabled,Is.True);
            int pose=Mathf.RoundToInt(lead.sprite.rect.x/lead.sprite.rect.width);seen.Add(pose);
            Assert.That(pose,Is.InRange(2,3),"Fast seals must use the stable arm poses, reserving the crouch for final compression.");
            Assert.That(pose,Is.EqualTo(NinjaSubstitutionRank4Stage.PreparationPose(time)));
            if(lastPose>=0&&pose!=lastPose)changes++;lastPose=pose;
            Assert.That(Read<float[]>("layerProgress"),Is.All.EqualTo(0f));
            Assert.That(Read<float[]>("compressionProgress"),Is.All.EqualTo(0f));
            Assert.That(Read<Vector3[]>("layerTremor"),Is.All.EqualTo(Vector3.zero));
            CollectionAssert.AreEqual(settled,Read<Vector3[]>("spaceVertices"));
            Assert.That(Read<SpriteRenderer>("centralRift").enabled,Is.False);
            Assert.That(Read<SpriteRenderer[]>("fragments").Any(f=>f.enabled),Is.False);
            Assert.That(stage.SoundBeat,Is.EqualTo(0));
        }
        Assert.That(seen.Count,Is.GreaterThanOrEqualTo(2));
        Assert.That(changes,Is.GreaterThanOrEqualTo(10),"Preparation must visibly repeat fast hand seals, not hold a single pose.");
        stage.Tick(NinjaSubstitutionRank4Stage.SealEndTime,0f);
        Assert.That(Mathf.RoundToInt(Read<SpriteRenderer>("lead").sprite.rect.x/128f),Is.EqualTo(3));
        Assert.That(Read<float[]>("layerProgress"),Is.All.EqualTo(0f));
    }

    [Test]public void TerrainSnapsThenBuildsIrregularTensionBeforeTheCondensedHold()
    {
        stage.Tick(NinjaSubstitutionRank4Stage.TerrainSnapTime-.001f,0f);
        Assert.That(Read<float[]>("layerProgress"),Is.All.EqualTo(0f));Assert.That(stage.SoundBeat,Is.EqualTo(0));
        stage.Tick(2.02f,0f);var snap=Read<float[]>("layerProgress");
        Assert.That(snap.Max()-snap.Min(),Is.GreaterThan(.05f));Assert.That(stage.SoundBeat,Is.EqualTo(1));
        stage.Tick(NinjaSubstitutionRank4Stage.TerrainSnapEndTime,0f);
        Assert.That(Read<float[]>("layerProgress"),Is.All.EqualTo(1f));
        Assert.That(Read<Vector3[]>("layerTremor"),Is.All.EqualTo(Vector3.zero));
        Quaternion[] snapped=Read<Quaternion[]>("rotations").ToArray();

        var samples=new List<Vector2>[9];for(int i=0;i<samples.Length;i++)samples[i]=new List<Vector2>();
        for(int frame=0;frame<=96;frame++)
        {
            stage.Tick(2.20f+frame/120f,1f/120f);Vector3[] tremor=Read<Vector3[]>("layerTremor");
            Assert.That(tremor.Max(v=>v.magnitude),Is.LessThan(.20f));
            for(int face=0;face<samples.Length;face++)samples[face].Add(new Vector2(tremor[face].x,tremor[face].z));
        }
        for(int face=0;face<samples.Length;face++)
        {
            Vector2[] residual=RemoveLinearDrift(samples[face]);float energy=residual.Sum(v=>v.sqrMagnitude);
            Assert.That(energy/residual.Length,Is.GreaterThan(.000001f),"A terrain piece has no tension movement.");
            float repeat=0f;for(int lag=8;lag<=24;lag++)repeat=Mathf.Max(repeat,
                Mathf.Abs(Correlation(residual,lag,false)),Mathf.Abs(Correlation(residual,lag,true)));
            Assert.That(repeat,Is.LessThan(.96f),"Terrain piece "+face+" repeats a short vibration loop.");
        }
        stage.Tick(NinjaSubstitutionRank4Stage.CondenseTime,0f);
        foreach(float value in Read<float[]>("compressionProgress"))Assert.That(value,Is.EqualTo(.82f).Within(.0001f));
        Assert.That(Read<Vector3[]>("layerTremor"),Is.All.EqualTo(Vector3.zero));
        stage.Tick(NinjaSubstitutionRank4Stage.CondenseEndTime,0f);
        Assert.That(Read<float[]>("compressionProgress"),Is.All.EqualTo(1f));
        var finalRotations=Read<Quaternion[]>("rotations");
        for(int face=0;face<snapped.Length;face++)
        {
            float snapAngle=Quaternion.Angle(Quaternion.identity,snapped[face]),finalAngle=Quaternion.Angle(Quaternion.identity,finalRotations[face]);
            Assert.That(snapAngle+.5f,Is.GreaterThanOrEqualTo(finalAngle*.70f),
                "Terrain piece "+face+" postpones its large angle change until the slow compression.");
        }
        Vector3[] compressed=Read<Vector3[]>("spaceVertices").ToArray();
        foreach(float time in new[]{3.25f,3.29f})
        {
            stage.Tick(time,0f);CollectionAssert.AreEqual(compressed,Read<Vector3[]>("spaceVertices"));
            Assert.That(Read<Vector3[]>("layerTremor"),Is.All.EqualTo(Vector3.zero));
            Assert.That(Read<SpriteRenderer>("impactRift").enabled,Is.False);Assert.That(stage.SoundBeat,Is.EqualTo(1));
            Assert.That(stage.ActionTime,Is.LessThan(NinjaSpacetimeRules.Windup));
        }
    }

    [Test]public void CondensedHoldReleasesOneExplosionAndOneDamagePulseAt330()
    {
        using(var combat=new NinjaSpacetimeFixture())
        {
            Assert.That(combat.Begin(),Is.True);
            foreach(float time in new[]{3.15f,3.20f,3.25f,3.299f})
            {
                stage.Tick(time,0f);combat.Advance(stage.ActionTime);
                Assert.That(combat.Int(combat.source,"skillHits"),Is.Zero);Assert.That(Read<SpriteRenderer>("impactRift").enabled,Is.False);
            }
            stage.Tick(NinjaSubstitutionRank4Stage.FilmImpactTime,0f);combat.Advance(stage.ActionTime);
            Assert.That(stage.ActionTime,Is.EqualTo(NinjaSpacetimeRules.Windup).Within(.00001f));
            Assert.That(Read<SpriteRenderer>("impactRift").enabled,Is.True);Assert.That(stage.SoundBeat,Is.EqualTo(2));
            for(int frame=0;frame<=114;frame++)
            {
                float time=NinjaSubstitutionRank4Stage.FilmImpactTime+frame/120f;stage.Tick(time,1f/120f);combat.Advance(stage.ActionTime);
                Assert.That(combat.Int(combat.source,"skillHits"),Is.EqualTo(1));
                if(time>=3.50f)Assert.That(Read<SpriteRenderer>("impactRift").enabled,Is.False,"The explosion must not restart after its short impact.");
            }
            Assert.That(combat.Float(combat.enemy,"hp"),Is.EqualTo(4700f).Within(.01f));Assert.That(combat.Active,Is.False);
        }
    }

    [Test]public void FixedSceneryAndRecessedGroundStaySeparateFromTheFracturedFloor()
    {
        stage.Tick(1.65f,0f);var scenery=Read<MeshRenderer>("scenery");var underlay=Read<MeshRenderer>("underlay");
        Assert.That(scenery.enabled&&underlay.enabled,Is.True);Assert.That(scenery,Is.Not.SameAs(Read<MeshRenderer>("space")));
        Mesh back=scenery.GetComponent<MeshFilter>().sharedMesh,ground=underlay.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] background=back.vertices,recessed=ground.vertices;
        Assert.That(back.uv.Min(v=>v.y),Is.GreaterThanOrEqualTo(.70f-.00001f));
        Assert.That(back.uv.Max(v=>v.y),Is.LessThanOrEqualTo(1f));
        Assert.That(Read<Rect>("FloorUV").yMax,Is.LessThanOrEqualTo(back.uv.Min(v=>v.y)+.00001f));
        Assert.That(recessed.Max(v=>v.y),Is.LessThan(-.10f));
        Assert.That(Mathf.Abs(Vector3.Dot(Vector3.Cross(recessed[1]-recessed[0],recessed[3]-recessed[0]).normalized,Vector3.up)),
            Is.GreaterThan(.999f));
        stage.Tick(2.80f,0f);CollectionAssert.AreEqual(background,back.vertices);CollectionAssert.AreEqual(recessed,ground.vertices);
        context.battlefieldBackdrop=other;Assert.That(stage.Begin(context),Is.True);stage.Tick(1.65f,0f);
        var block=new MaterialPropertyBlock();scenery.GetPropertyBlock(block);
        Assert.That(block.GetTexture("_MainTex"),Is.SameAs(other));
        var voidProperties=new MaterialPropertyBlock();underlay.GetPropertyBlock(voidProperties);
        Texture voidTexture=voidProperties.GetTexture("_MainTex")??underlay.sharedMaterial.mainTexture;
        Assert.That(voidTexture,Is.SameAs(Texture2D.whiteTexture),"The recessed void must not show a second grass photograph.");
        Assert.That(ground.colors.Length,Is.EqualTo(ground.vertexCount));
        foreach(Color color in ground.colors)
        {
            Assert.That(Mathf.Max(color.r,color.g,color.b),Is.LessThanOrEqualTo(.20f));
            Assert.That(color.b,Is.GreaterThan(color.r));Assert.That(color.r,Is.GreaterThan(color.g));
            Assert.That(color.a,Is.GreaterThanOrEqualTo(.99f));
        }
        stage.Tick(3.35f,0f);Assert.That(scenery.enabled||underlay.enabled,Is.False,"Stage scenery must not leak into the returned battle.");
    }

    [TestCase(false)][TestCase(true)]
    public void EightBillboardTargetsStandAboveTheirFloorPiecesWithoutDepthClipping(bool enemy)
    {
        context.enemy=enemy;Assert.That(stage.Begin(context),Is.True);
        foreach(float time in new[]{1.45f,1.65f,2.30f,2.80f,3.18f})
        {
            stage.Tick(time,0f);var root=host.transform.GetChild(0);
            var camera=host.GetComponentInChildren<Camera>(true);
            var actors=Read<SpriteRenderer[]>("victims");var uv=Read<Vector2[]>("targetUV");var faces=Read<int[]>("targetFace");
            var rotations=Read<Quaternion[]>("rotations");var heights=Read<float[]>("targetHeights");
            var terrain=new[]{Read<MeshRenderer>("space"),Read<MeshRenderer>("sides")};
            Assert.That(Vector3.Dot(camera.transform.forward,Vector3.down),Is.GreaterThan(.35f),"The camera must look down onto the floor.");
            for(int i=0;i<8;i++)
            {
                Assert.That(actors[i].enabled,Is.True);Assert.That(actors[i].sprite,Is.SameAs(context.targets[i].sprite));
                Vector3 expected=(Vector3)stage.GetType().GetMethod("WarpPoint",Hidden).Invoke(stage,new object[]{uv[i],faces[i]});
                expected+=rotations[faces[i]]*Vector3.up*.065f;
                Bounds body=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{actors[i].sprite});
                Vector3 anchor=new Vector3(actors[i].flipX?-body.center.x:body.center.x,body.min.y,0f);
                Vector3 worldFoot=actors[i].transform.TransformPoint(anchor),actual=root.InverseTransformPoint(worldFoot);
                Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.02f),"A target detached from its refracted section.");
                Assert.That(Quaternion.Angle(actors[i].transform.rotation,camera.transform.rotation),Is.LessThanOrEqualTo(5.01f),
                    "Targets must stand as readable billboards instead of lying inside the floor.");
                Vector3 faceScale=Read<Vector3[]>("scales")[faces[i]],actorScale=actors[i].transform.localScale;
                Assert.That(actorScale.x/actorScale.y,Is.EqualTo(faceScale.x).Within(.001f));
                Vector3 top=actors[i].transform.TransformPoint(new Vector3(anchor.x,body.max.y,0f));
                Assert.That(Vector3.Distance(top,worldFoot),Is.EqualTo(heights[i]).Within(.005f));
                Assert.That(Vector3.Dot((top-worldFoot).normalized,Vector3.up),Is.GreaterThan(.20f));
                Assert.That(camera.WorldToViewportPoint(top).y-camera.WorldToViewportPoint(worldFoot).y,Is.GreaterThan(.01f));
                Material bodyMaterial=actors[i].sharedMaterial;Assert.That(bodyMaterial,Is.Not.Null);
                bool always=MaterialDepthState(bodyMaterial,"_ZTest","ZTest","Always",8);
                Assert.That(MaterialDepthState(bodyMaterial,"_ZWrite","ZWrite","Off",0),Is.True,"The actor overlay must not write depth.");
                Assert.That(bodyMaterial.renderQueue,Is.GreaterThanOrEqualTo(3000));
                foreach(var floor in terrain)
                {
                    bool noDepth=MaterialDepthState(floor.sharedMaterial,"_ZWrite","ZWrite","Off",0);
                    Assert.That(always||noDepth,Is.True,"Depth-writing floor fragments can slice a default LEqual sprite body.");
                    Assert.That(bodyMaterial.renderQueue,Is.GreaterThanOrEqualTo(floor.sharedMaterial.renderQueue));
                    if(bodyMaterial.renderQueue==floor.sharedMaterial.renderQueue)
                        Assert.That(actors[i].sortingOrder,Is.GreaterThan(floor.sortingOrder));
                }
            }
        }
    }

    [TestCase(false)][TestCase(true)]
    public void CasterFaceStaysAlignedAcrossPosesWithBoundedNonrepeatingTremor(bool enemy)
    {
        context.enemy=enemy;var camera=host.GetComponentInChildren<Camera>(true);
        var faces=Read<Vector2[][]>("Face");
        string[] species={"chundongi","seolmyo","sundongi","hongryeon"};
        for(int row=0;row<species.Length;row++)
        {
            var actor=context.source;actor.species=species[row];context.source=actor;
            Assert.That(stage.Begin(context),Is.True);var lead=Read<SpriteRenderer>("lead");
            var window=new List<Vector2>();var repeatWindow=new List<Vector2>();Vector2 previous=default(Vector2);
            float maxStep=0f,peakTime=0f,previousFoot=0f,maxFootStep=0f;bool sampledFoot=false;
            for(int frame=0;frame<=251;frame++)
            {
                float time=Mathf.Min(3.29f,1.20f+frame/120f);stage.Tick(time,1f/120f);
                Assert.That(lead.enabled,Is.True);
                int pose=Mathf.RoundToInt(lead.sprite.rect.x/lead.sprite.rect.width);
                Assert.That(pose,Is.InRange(1,5));Vector2 marker=faces[row][pose-1];
                Vector3 point=new Vector3((marker.x-lead.sprite.pivot.x)/lead.sprite.pixelsPerUnit,
                    (lead.sprite.rect.height-marker.y-lead.sprite.pivot.y)/lead.sprite.pixelsPerUnit,0f);
                if(lead.flipX)point.x=-point.x;if(lead.flipY)point.y=-point.y;
                Vector3 viewport=camera.WorldToViewportPoint(lead.transform.TransformPoint(point));
                Assert.That(viewport.z,Is.GreaterThan(0f));
                Vector2 pixel=new Vector2(viewport.x*1920f,(1f-viewport.y)*1080f);
                if(frame>0)
                {
                    float step=Vector2.Distance(previous,pixel);
                    if(step>maxStep){maxStep=step;peakTime=time;}
                }
                previous=pixel;
                if(time<NinjaSubstitutionRank4Stage.SealEndTime)
                {
                    Bounds body=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{lead.sprite});
                    Vector3 foot=new Vector3(body.center.x,body.min.y,0f);
                    if(lead.flipX)foot.x=-foot.x;if(lead.flipY)foot.y=-foot.y;
                    float footY=(1f-camera.WorldToViewportPoint(lead.transform.TransformPoint(foot)).y)*1080f;
                    Assert.That(footY,Is.InRange(0f,1080f),species[row]+" hand-seal feet are clipped outside the frame.");
                    if(sampledFoot)maxFootStep=Mathf.Max(maxFootStep,Mathf.Abs(footY-previousFoot));
                    previousFoot=footY;sampledFoot=true;
                }
                if(time>=2.30f-.0001f&&time<=2.55f+.0001f)window.Add(pixel);
                if(time>=1.95f-.0001f&&time<=2.65f+.0001f)repeatWindow.Add(pixel);
            }
            string label=species[row]+(enemy?" enemy":" ally");
            Assert.That(maxStep,Is.LessThanOrEqualTo(12f),label+" face jumped at "+peakTime.ToString("F4")+" seconds.");
            Assert.That(maxFootStep,Is.LessThanOrEqualTo(40f),label+" hand-seal feet jump instead of staying planted.");

            // The authored screen drift is slow; random tension must stay small
            // after that drift is removed, and cannot repeat a short sine cycle.
            Vector2[] residual=RemoveLinearDrift(window);Vector2 min=Vector2.positiveInfinity,max=Vector2.negativeInfinity;float energy=0f;
            foreach(Vector2 point in residual){min=Vector2.Min(min,point);max=Vector2.Max(max,point);energy+=point.sqrMagnitude;}
            Assert.That(max.x-min.x,Is.LessThanOrEqualTo(12f),label+" has broad horizontal shaking.");
            Assert.That(max.y-min.y,Is.LessThanOrEqualTo(8f),label+" has broad vertical shaking.");
            Assert.That(Mathf.Sqrt(energy/window.Count),Is.GreaterThan(.20f),label+" has no visible preparation tremor.");
            Vector2[] repeating=RemoveLinearDrift(repeatWindow);float closestRepeat=0f;
            for(int lag=8;lag<=24;lag++)
                closestRepeat=Mathf.Max(closestRepeat,Mathf.Abs(Correlation(repeating,lag,false)),Mathf.Abs(Correlation(repeating,lag,true)));
            Assert.That(closestRepeat,Is.LessThan(.92f),label+" repeats a short periodic shake instead of irregular tension.");
        }
    }
}
