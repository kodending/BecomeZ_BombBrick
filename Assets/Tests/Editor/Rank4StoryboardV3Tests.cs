using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class Rank4StoryboardV3Tests
{
    private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static string PlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/Rank4StoryboardsV3/20261023"));
    private static string OutputRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/Rank4StoryboardV3QA/20261023"));
    private static string SpiderPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/AraneaRank4/CeilingV01-20261023"));
    private static string NinjaPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/NinjaCatRank4/SpacetimeCollapse-20261023"));
    private static string TyrannoPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/TyrannoRank4/GodFootstepsV3-20261024"));
    private static string MonkeyPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/MonkeyRank4/HeavenlyThunder-20261024"));
    private static string MantisPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/MantisRank4/MoltPolish-20261024"));
    private static string MushroomPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/MushroomRank4/SmokeBloomRevision-20261024"));
    private static string MammothPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/MammothRank4/ChainCollision-20261024"));
    private static string SpiritDeerPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/SpiritDeerRank4/WaterMantle-20261028"));
    private static string WyvernPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/WyvernRank4/NativeLink-20261028"));
    private static string HaetaePlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/HaetaeRank4/ShadowAwakening-20261029"));
    private static string BeetlePlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/BeetleRank4/ImperialHorn-20261029"));
    private static string GriffinPlanRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Docs/Concepts/GriffinRank4/StormOrb-20261029"));
    private static string[] Families=>Rank4CinematicCatalog.Families;

    [TestCaseSource(nameof(Families))]
    public void FilmUsesItsReviewedBoardAndFreshArtwork(string family)
    {
        var plan=Plan(family);string directory=PlanDirectory(family);
        bool revised=family=="spider"||family=="ninja_cat"||family=="tyranno"||family=="monkey"||family=="mantis"||family=="mushroom"||family=="mammoth"||family=="spirit_deer"||family=="wyvern"||family=="haetae"||family=="beetle"||family=="griffin";
        var approved=JObject.Parse(File.ReadAllText(revised?Path.Combine(directory,"ApprovedImplementation.json"):Path.Combine(PlanRoot,"ApprovedBoards.json")));
        JToken locked=revised?approved:approved[family];
        Assert.That(locked,Is.Not.Null,"Review the visual board before authoring the film.");
        foreach(string file in family=="spider"?new[]{"ImplementationSpec.txt","BoardChecks.json","Storyboard.png"}:
            family=="tyranno"?new[]{"Spec.txt","BoardChecks.json","Storyboard-v03.png"}:
            family=="mushroom"?new[]{"Spec.txt","BoardChecks.json","SporeBloom-Identity-Native192.png"}:
            family=="mammoth"?new[]{"Spec.txt","BoardChecks.json","Storyboard-v01.png"}:
            family=="wyvern"?new[]{"Spec-v03.txt","BoardChecks.json","Storyboard-v03.png"}:
            family=="haetae"||family=="beetle"?new[]{"Spec-v01.txt","BoardChecks.json","Storyboard-v01.png"}:
            family=="spirit_deer"||family=="griffin"?new[]{"Spec-v02.txt","BoardChecks.json","Storyboard-v02.png"}:
            family=="monkey"||family=="mantis"?new[]{"Spec.txt","BoardChecks.json"}:new[]{"Spec.txt","BoardChecks.json","Storyboard.png"})
            Assert.That(Hash(Path.Combine(directory,file)),Is.EqualTo((string)locked[file]),"The reviewed board changed: "+family+"/"+file);
        Check(family,(stage,context)=>
        {
            var field=stage.GetType().GetField("BoardTimes",Hidden);Assert.That(field,Is.Not.Null);
            var times=(float[])field.GetValue(null);Assert.That(times,Has.Length.EqualTo(plan["panels"].Count()));
            Assert.That(stage.Duration,Is.EqualTo((float)plan["duration"]).Within(.001f));
            float previous=-1f;
            for(int i=0;i<times.Length;i++)
            {
                Assert.That(times[i],Is.GreaterThan(previous));Assert.That(times[i],Is.LessThan(stage.Duration));
                Assert.That(times[i],Is.EqualTo((float)plan["panels"][i]["time"]).Within(.001f));previous=times[i];
            }
            var frames=stage.ArtworkFrames.ToArray();Assert.That(frames.Length,Is.GreaterThan(0));
            string resourceRoot=family=="spider"?"CombatCinematics/AraneaCeilingV1/":family=="ninja_cat"?"CombatCinematics/NinjaSpacetimeV1/":
                family=="tyranno"?"CombatCinematics/TyrannoGodV2/":family=="monkey"?MonkeyCircusRank4Stage.SkyResourcePath:
                family=="mammoth"?"CombatCinematics/MammothChainV1/":family=="haetae"?"CombatCinematics/HaetaeShadowV1/":
                family=="beetle"?"CombatCinematics/BeetleImperialHornV1/":family=="griffin"?"CombatCinematics/GriffinStormOrbV1/":"CombatCinematics/Rank4V3/";
            Assert.That(frames.All(s=>s.name.StartsWith(resourceRoot,StringComparison.Ordinal)||
                family=="tyranno"&&s.name.StartsWith(PixelTyrannoReignStomp.ResourceRoot,StringComparison.Ordinal)||
                family=="monkey"&&s.name.StartsWith(PixelMonkeyStorm.CloudResourcePath,StringComparison.Ordinal)||
                family=="haetae"&&s.name.StartsWith("CombatEffects/Haetae/Devourer16",StringComparison.Ordinal)),Is.True,"A rejected atlas cannot be reused.");
        });
    }

    [TestCaseSource(nameof(Families))]
    public void RuntimeCompositionMatchesTheSixLockedStoryboardMoments(string family)
    {
        var plan=Plan(family);Check(family,(stage,context)=>
        {
            Camera camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
            Transform root=(Transform)typeof(Rank4CinematicStage).GetField("Root",Hidden).GetValue(stage);
            string directory=Path.Combine(OutputRoot,family);Directory.CreateDirectory(directory);
            int rows=(plan["panels"].Count()+1)/2;
            var measured=new JArray();var sheet=new Texture2D(1280,rows*360,TextureFormat.RGB24,false);
            var mismatches=new System.Collections.Generic.List<string>();
            var frame=new Texture2D(640,360,TextureFormat.RGBA32,false);RenderTexture previous=RenderTexture.active;
            try
            {
                int index=0;
                foreach(var cue in plan["panels"])
                {
                    float time=(float)cue["time"];stage.Tick(time,0f);camera.Render();
                    if(camera.orthographic!=((string)cue["projection"]=="orthographic"))mismatches.Add(cue["id"]+" projection");
                    ReadStageImage(stage,frame);
                    bool transparent=(bool?)cue["transparent"]??index==plan["panels"].Count()-1;
                    bool capturedField=family=="wyvern"&&(bool?)cue["fieldConnected"]==true;
                    if(capturedField)
                    {
                        var field=root.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Actual captured battlefield");
                        Assert.That(field.enabled&&field.sprite==context.wyvernBattlefieldSprite,Is.True,"A field-connected shot must reuse the provided actual battlefield capture.");
                        if(frame.GetPixels32().Any(p=>p.a<255))CompositeBattlefield(frame,stage.Opacity);
                    }
                    else if(!transparent)
                        Assert.That(Array.TrueForAll(frame.GetPixels32(),p=>p.a==255),Is.True,"Unintended alpha exposes the battlefield through opaque set pieces.");
                    else
                    {
                        File.WriteAllBytes(Path.Combine(directory,index==plan["panels"].Count()-1?"Return-Overlay.png":"Intro-"+(string)cue["id"]+"-Overlay.png"),frame.EncodeToPNG());
                        // A storm intentionally covers the upper corners; the combat area must stay clear.
                        if(family=="haetae")
                        {
                            Color32[] pixels=frame.GetPixels32();
                            Assert.That(pixels.Count(pixel=>pixel.a<64),Is.GreaterThan(frame.width*frame.height*.65f),
                                "The 4.53-second physical return must reveal the field through the final scenery alpha fade.");
                            Assert.That(pixels.Count(pixel=>pixel.a>2&&pixel.a<64),Is.GreaterThan(frame.width*frame.height*.50f),
                                "The scenery must fade continuously rather than disappearing through a hard mask.");
                        }
                        else Assert.That(frame.GetPixel(family=="monkey"?639:0,family=="monkey"?0:359).a,Is.LessThan(.01f),"The field-connected shot must reveal the real battlefield.");
                        CompositeBattlefield(frame,stage.Opacity);
                    }
                    File.WriteAllBytes(Path.Combine(directory,"Actual-"+(string)cue["id"]+".png"),frame.EncodeToPNG());
                    sheet.SetPixels(index%2*640,(rows-1-index/2)*360,640,360,frame.GetPixels());index++;
                    var sample=new JObject{{"id",(string)cue["id"]},{"time",time},{"shot",stage.Shot},{"opacity",stage.Opacity},{"actionTime",stage.ActionTime}};
                    if((bool)cue["leadVisible"])
                    {
                        string prefix=(string)cue["actorPrefix"]??"Lead";
                        var actors=root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.enabled&&r.sprite!=null&&r.name.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
                        if(actors.Length==0){mismatches.Add(cue["id"]+": storyboard lead missing");measured.Add(sample);continue;}
                        Rect visible=actors.Select(r=>ProjectedBody(camera,r)).OrderByDescending(r=>r.height).First();
                        if((bool?)cue["combineActors"]==true)
                        {
                            Rect[] bounds=actors.Select(r=>ProjectedBody(camera,r)).ToArray();
                            visible=Rect.MinMaxRect(bounds.Min(b=>b.xMin),bounds.Min(b=>b.yMin),bounds.Max(b=>b.xMax),bounds.Max(b=>b.yMax));
                        }
                        Vector2 center=new Vector2(visible.center.x,1f-visible.center.y);
                        var expected=new Vector2((float)cue["center"][0],(float)cue["center"][1]);
                        float expectedHeight=(float)cue["height"];
                        if((bool?)cue["allowCrop"]==true)
                        {
                            float top=Mathf.Max(0f,expected.y-expectedHeight*.5f),bottom=Mathf.Min(1f,expected.y+expectedHeight*.5f);
                            expected.y=(top+bottom)*.5f;expectedHeight=bottom-top;
                        }
                        float tolerance=(float)cue["positionTolerance"],heightTolerance=(float)cue["heightTolerance"];
                        sample["center"]=new JArray(center.x,center.y);sample["height"]=visible.height;
                        measured.Add(sample);File.WriteAllText(Path.Combine(directory,"Composition.actual.json"),measured.ToString());
                        if(Mathf.Abs(center.x-expected.x)>tolerance)mismatches.Add(cue["id"]+" horizontal: "+center.x+" expected "+expected.x);
                        if(Mathf.Abs(center.y-expected.y)>tolerance)mismatches.Add(cue["id"]+" vertical: "+center.y+" expected "+expected.y);
                        if(Mathf.Abs(visible.height-expectedHeight)>heightTolerance)mismatches.Add(cue["id"]+" height: "+visible.height+" expected visible "+expectedHeight);
                    }
                    else measured.Add(sample);
                }
                sheet.Apply();File.WriteAllBytes(Path.Combine(directory,family=="tyranno"?"Actual-EightMoments.png":"Actual-SixMoments.png"),sheet.EncodeToPNG());
                File.WriteAllText(Path.Combine(directory,"Composition.actual.json"),measured.ToString());
                SaveCutBoundaries(stage,camera,frame,directory);
                context.enemy=true;var enemy=context.source;enemy.flipX=!enemy.flipX;context.source=enemy;
                Assert.That(stage.Begin(context),Is.True);stage.Tick((float)plan["panels"][2]["time"],0f);camera.Render();
                ReadStageImage(stage,frame);
                File.WriteAllBytes(Path.Combine(directory,"Actual-Enemy-S3.png"),frame.EncodeToPNG());
                Assert.That(mismatches,Is.Empty,family+": the locked board must guide the runtime, "+string.Join("; ",mismatches.ToArray()));
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(frame);Object.DestroyImmediate(sheet);}
        });
    }

    [TestCaseSource(nameof(Families))]
    public void EveryRealSpeciesAndExistingShinyHasItsOwnReadableArtwork(string family)
    {
        var definitions=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters
            .Where(d=>d!=null&&Rank4CinematicCatalog.Canonical(d.familyId)==family).OrderBy(d=>d.serialNumber).ToArray();
        var actors=new System.Collections.Generic.List<FrogCatchCinematicStage.Actor>();
        foreach(var definition in definitions)
        {
            actors.Add(Actor(definition));
            if(Resources.Load<GameObject>("Enemies/Shiny/"+definition.monsterId)!=null)actors.Add(Actor(definition,true));
        }
        var plan=Plan(family);Check(family,(stage,context)=>
        {
            Camera camera=(Camera)typeof(Rank4CinematicStage).GetField("View",Hidden).GetValue(stage);
            string directory=Path.Combine(OutputRoot,family);Directory.CreateDirectory(directory);
            var frame=new Texture2D(640,360,TextureFormat.RGBA32,false);
            var sheet=new Texture2D(1280,((actors.Count+1)/2)*360,TextureFormat.RGB24,false);
            var signatures=new System.Collections.Generic.HashSet<string>();RenderTexture previous=RenderTexture.active;
            try
            {
                for(int index=0;index<actors.Count;index++)
                {
                    context.source=actors[index];context.returnSprite=actors[index].sprite;Assert.That(stage.Begin(context),Is.True);
                    int sample=family=="spider"?2:family=="ninja_cat"||family=="wyvern"||family=="griffin"?1:family=="monkey"?5:family=="tyranno"?3:0;
                    stage.Tick((float)plan["panels"][sample]["time"],0f);camera.Render();ReadStageImage(stage,frame);byte[] bytes=frame.EncodeToPNG();
                    string label=actors[index].species+(actors[index].shiny?"-shiny":"");
                    File.WriteAllBytes(Path.Combine(directory,"Variant-"+label+".png"),bytes);
                    using(var hash=SHA256.Create())Assert.That(signatures.Add(BitConverter.ToString(hash.ComputeHash(bytes))),Is.True,
                        label+": another species' drawing was reused without its actual appearance.");
                    sheet.SetPixels(index%2*640,((actors.Count+1)/2-1-index/2)*360,640,360,frame.GetPixels());
                }
                sheet.Apply();File.WriteAllBytes(Path.Combine(directory,"Actual-Variants.png"),sheet.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(frame);Object.DestroyImmediate(sheet);}
        });
    }

    [TestCaseSource(nameof(Families))]
    public void FilmOwnsItsClockWithoutChangingBattleTimeAndHidesAtTheEnd(string family)
    {
        Check(family,(stage,context)=>
        {
            float previousSpeed=Time.timeScale,previousAction=-1f;
            try
            {
                Time.timeScale=0f;
                for(float time=0;time<=stage.Duration;time+=1f/120f)
                {
                    stage.Tick(time,1f/120f);Assert.That(float.IsNaN(stage.ActionTime)||float.IsInfinity(stage.ActionTime),Is.False);
                    Assert.That(stage.ActionTime,Is.GreaterThanOrEqualTo(previousAction-.0001f),family+" must not replay an earlier contact.");
                    previousAction=stage.ActionTime;Assert.That(Time.timeScale,Is.Zero);
                }
                stage.Tick(stage.Duration,0f);Assert.That(stage.Opacity,Is.LessThan(.01f));stage.Hide();Assert.That(stage.Active,Is.False);
            }
            finally{Time.timeScale=previousSpeed;}
        });
    }

    private static Rect ProjectedBody(Camera camera,SpriteRenderer actor)
    {
        Bounds body=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Hidden).Invoke(null,new object[]{actor.sprite});
        float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            Vector3 corner=new Vector3(x==0?body.min.x:body.max.x,y==0?body.min.y:body.max.y,0f);if(actor.flipX)corner.x=-corner.x;
            Vector3 p=camera.WorldToViewportPoint(actor.transform.TransformPoint(corner));Assert.That(p.z,Is.GreaterThan(0f),"The principal actor is behind the camera.");
            minX=Mathf.Min(minX,p.x);minY=Mathf.Min(minY,p.y);maxX=Mathf.Max(maxX,p.x);maxY=Mathf.Max(maxY,p.y);
        }
        return Rect.MinMaxRect(Mathf.Max(0,minX),Mathf.Max(0,minY),Mathf.Min(1,maxX),Mathf.Min(1,maxY));
    }

    private static JObject Plan(string family)
    {
        var plan=JObject.Parse(File.ReadAllText(Path.Combine(PlanDirectory(family),"BoardChecks.json")));
        Assert.That((string)plan["family"],Is.EqualTo(family));Assert.That(plan["panels"].Count(),Is.EqualTo(family=="tyranno"?8:6));
        return plan;
    }
    private static string PlanDirectory(string family)=>family=="spider"?SpiderPlanRoot:family=="ninja_cat"?NinjaPlanRoot:
        family=="tyranno"?TyrannoPlanRoot:family=="monkey"?MonkeyPlanRoot:family=="mantis"?MantisPlanRoot:family=="mushroom"?MushroomPlanRoot:
        family=="mammoth"?MammothPlanRoot:family=="spirit_deer"?SpiritDeerPlanRoot:family=="wyvern"?WyvernPlanRoot:
        family=="haetae"?HaetaePlanRoot:family=="beetle"?BeetlePlanRoot:family=="griffin"?GriffinPlanRoot:Path.Combine(PlanRoot,family);
    private static string Hash(string path)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}

    private static void ReadStageImage(Rank4CinematicStage stage,Texture2D frame)
    {
        RenderTexture previous=RenderTexture.active;
        RenderTexture target=RenderTexture.GetTemporary(frame.width,frame.height,0,RenderTextureFormat.ARGB32);
        try
        {
            Graphics.Blit(stage.Texture,target);RenderTexture.active=target;
            frame.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);frame.Apply();
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
    }

    private static void SaveCutBoundaries(Rank4CinematicStage stage,Camera camera,Texture2D frame,string directory)
    {
        stage.Tick(0f,0f);int previous=stage.Shot;
        for(float time=1f/120f;time<stage.Duration;time+=1f/120f)
        {
            stage.Tick(time,0f);int shot=stage.Shot;if(shot==previous)continue;
            for(int side=0;side<2;side++)
            {
                stage.Tick(Mathf.Clamp(time+(side==0?-.025f:.025f),0f,stage.Duration),0f);camera.Render();
                ReadStageImage(stage,frame);
                if(camera.backgroundColor.a<.01f)CompositeBattlefield(frame,stage.Opacity);
                File.WriteAllBytes(Path.Combine(directory,"Cut-"+previous+"-"+shot+"-"+(side==0?"Before":"After")+".png"),frame.EncodeToPNG());
            }
            previous=shot;
        }
    }

    // This is the actual game's field plate under the transparent return overlay, not cinematic artwork.
    private static void CompositeBattlefield(Texture2D overlay,float opacity)
    {
        var plate=Resources.Load<Texture2D>("Environment/PrimordialMeadow/BattleBackground");Assert.That(plate,Is.Not.Null);
        RenderTexture previous=RenderTexture.active;var target=RenderTexture.GetTemporary(640,360,0);
        var field=new Texture2D(640,360,TextureFormat.RGB24,false);
        try
        {
            Graphics.Blit(plate,target);RenderTexture.active=target;field.ReadPixels(new Rect(0,0,640,360),0,0);field.Apply();
            var front=overlay.GetPixels();var back=field.GetPixels();
            for(int i=0;i<front.Length;i++){front[i]=Color.Lerp(back[i],front[i],front[i].a*opacity);front[i].a=1f;}
            overlay.SetPixels(front);overlay.Apply();
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(field);}
    }

    private static void Check(string family,Action<Rank4CinematicStage,Rank4StageContext> inspect)
    {
        var definitions=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters;
        var definition=definitions.Where(d=>d!=null&&Rank4CinematicCatalog.Canonical(d.familyId)==family).OrderBy(d=>d.serialNumber).First();
        var context=new Rank4StageContext{family=family,source=Actor(definition),returnPosition=new Vector2(-2,-2),returnHeight=1.3f};
        context.targets=new[]{"woongi","velos","geomchi","tino"}.Select(id=>Actor(definitions.First(d=>d.monsterId==id),false,false)).ToArray();
        if(family=="mushroom")
        {
            var fallen=context.source;
            fallen.frames=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true).DeathFrames;
            context.source=fallen;
        }
        for(int i=0;i<context.targets.Length;i++){context.targets[i].ground=new Vector2(.2f+i*.18f,-.5f+i*.3f);context.targets[i].flipX=true;}
        context.allies=new[]{"chundongi","velos","woongi"}.Select(id=>Actor(definitions.First(d=>d.monsterId==id),false,false)).ToArray();
        if(family=="beetle")
        {
            context.targets=new[]{"woongi","velos","geomchi","tino","chundongi","aranea","haeta","kogason"}
                .Select(id=>Actor(definitions.First(d=>d.monsterId==id),false,false)).ToArray();
            Vector2[] positions={new Vector2(.20f,.45f),new Vector2(.45f,.45f),new Vector2(.72f,.45f),
                new Vector2(.20f,0f),new Vector2(.48f,0f),new Vector2(.75f,0f),new Vector2(.35f,-.45f),new Vector2(.70f,-.45f)};
            for(int i=0;i<context.targets.Length;i++){context.targets[i].ground=positions[i];context.targets[i].flipX=true;}
            context.allies[0].ground=new Vector2(-.24f,-.20f);
            context.allies[1].ground=new Vector2(-.42f,.15f);
            context.allies[2].ground=new Vector2(-.18f,.48f);
        }
        Sprite wyvernField=null,haetaeGuardian=null;
        if(family=="haetae")
        {
            context.allies[0].ground=new Vector2(-.24f,-.20f);
            context.allies[1].ground=new Vector2(-.42f,.15f);
            context.allies[2].ground=new Vector2(-.18f,.48f);
            var atlas=Resources.Load<Texture2D>("CombatEffects/Haetae/Devourer16");Assert.That(atlas,Is.Not.Null);
            haetaeGuardian=Sprite.Create(atlas,new Rect(0,atlas.height*.75f,atlas.width*.25f,atlas.height*.25f),Vector2.one*.5f,64f);
            context.guardianReturnSprite=haetaeGuardian;context.guardianReturnPosition=new Vector2(6.08f,-.45f);
            context.guardianReturnHeight=2.52f;context.guardianReturnVisible=true;
        }
        if(family=="wyvern")
        {
            context.battlefieldBackdrop=Resources.Load<Texture2D>("Environment/PrimordialMeadow/BattleBackground");Assert.That(context.battlefieldBackdrop,Is.Not.Null);
            var plate=context.battlefieldBackdrop;
            wyvernField=Sprite.Create(plate,new Rect(0,0,plate.width,plate.height),Vector2.one*.5f,64f);
            context.wyvernBattlefieldSprite=wyvernField;
        }
        Rank4CinematicStage stage=Rank4CinematicCatalog.Create(family,null);Assert.That(stage,Is.Not.Null,family+" film is not connected.");
        try{Assert.That(stage.Begin(context),Is.True);inspect(stage,context);}finally{stage.Dispose();Object.DestroyImmediate(wyvernField);Object.DestroyImmediate(haetaeGuardian);}
    }
    private static FrogCatchCinematicStage.Actor Actor(MonsterDefinition definition,bool shiny=false,bool caster=true)
    {
        var prefab=Resources.Load<GameObject>(shiny?"Enemies/Shiny/"+definition.monsterId:definition.resourcePath);var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        return new FrogCatchCinematicStage.Actor{species=definition.monsterId,shiny=shiny,sprite=driver.IdleFrames[0],frames=caster?(Sprite[])typeof(SpriteMonsterAnimationDriver).GetField("skillAttackFrames",Hidden).GetValue(driver):driver.DragFrames??driver.IdleFrames,size=1f,color=Color.white};
    }
}
