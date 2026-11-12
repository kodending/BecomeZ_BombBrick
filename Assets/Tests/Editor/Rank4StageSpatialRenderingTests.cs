using NUnit.Framework;
using System.Linq;
using System.Reflection;
using BombBrick.Combat;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class Rank4StageSpatialRenderingTests
{
    private sealed class MissingArtworkStage:Rank4CinematicStage
    {
        public override string Family=>"missing-art-test";
        public override float Duration=>1f;
        public MissingArtworkStage():base(null,"Rank4 missing artwork cleanup",Color.black)
        {LoadAtlas("CombatCinematics/MissingArtworkForResourceCleanupTest",1);}
        protected override void Animate(float time,float dt){}
    }

    [Test]
    public void MissingArtworkDuringConstructionReleasesItsPrivateCameraAndRenderTexture()
    {
        Assert.Throws<System.InvalidOperationException>(()=>new MissingArtworkStage());
        Assert.That(System.Array.Exists(Resources.FindObjectsOfTypeAll<GameObject>(),g=>g.name=="Rank4 missing artwork cleanup"),Is.False);
        Assert.That(System.Array.Exists(Resources.FindObjectsOfTypeAll<RenderTexture>(),r=>r.name=="Rank4 missing artwork cleanup pixel stage"),Is.False);
    }

    private sealed class SpatialStage:Rank4CinematicStage
    {
        private readonly SpriteRenderer translucent;
        private readonly SpriteRenderer returned;
        public bool TestReturn;
        public SpriteRenderer Returned=>returned;
        public override string Family=>"spatial-test";
        public override float Duration=>2f;
        public Camera Camera=>View;
        public Transform SceneRoot=>Root;
        public SpatialStage():base(null,"Rank4 spatial rendering test",new Color(.04f,.06f,.1f,1f))
        {
            Geometry("Far blue plane",new[]{new Vector3(-2,-2,2),new Vector3(2,-2,2),new Vector3(2,2,2),new Vector3(-2,2,2)},
                new[]{0,1,2,0,2,3},new[]{Color.blue,Color.blue,Color.blue,Color.blue});
            Geometry("Near red plane",new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)},
                new[]{0,1,2,0,2,3},new[]{Color.red,Color.red,Color.red,Color.red});
            translucent=MakeSprite("Translucent foreground pixel");
            returned=MakeSprite("Lead battlefield return");
        }
        protected override void Animate(float time,float dt)
        {
            CameraPose(new Vector3(time*3f,time,-10),Vector3.zero,50f-time*6f);
            if(TestReturn){CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);PaintReturn(returned,40);return;}
            PaintWorld(translucent,Context.source.sprite,new Vector3(-3f,-1f,-1f),1.5f,
                new Color(0,1,0,.5f),20,CameraFacing);
            Opacity=Mathf.Clamp01(time+.1f);ActionTime=time*.4f;SoundBeat=time>=1f?1:0;
        }
    }

    [Test]
    public void PerspectiveCameraReallyMovesThroughDepthAndNearGeometryOccludesFarGeometry()
    {
        Check((stage,image)=>
        {
            stage.Tick(0f,0f);stage.Camera.Render();Read(stage,image);
            Color center=image.GetPixel(image.width/2,image.height/2);
            Assert.That(center.r,Is.GreaterThan(.9f));Assert.That(center.b,Is.LessThan(.1f));
            Quaternion before=stage.Camera.transform.localRotation;
            stage.Tick(1.5f,.016f);
            Assert.That(stage.Camera.orthographic,Is.False);
            Assert.That(stage.Camera.transform.localPosition.x,Is.EqualTo(4.5f));
            Assert.That(Quaternion.Angle(before,stage.Camera.transform.localRotation),Is.GreaterThan(15f));
            Assert.That(stage.Camera.fieldOfView,Is.EqualTo(41f));
            Assert.That(stage.SceneRoot.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(2));
        });
    }

    [Test]
    public void TranslucentArtworkCannotMakeTheStageLeakTheUnderlyingBattleThroughItsAlpha()
    {
        Check((stage,image)=>
        {
            stage.Tick(0f,0f);stage.Camera.Render();Read(stage,image);
            Assert.That(System.Array.TrueForAll(image.GetPixels32(),pixel=>pixel.a==255),Is.True);
            Assert.That(stage.Opacity,Is.EqualTo(.1f).Within(.0001f));
        });
    }

    [Test]
    public void EachStageOwnsItsTransitionAndCombatTimingInsteadOfUsingFourSharedShots()
    {
        Check((stage,image)=>
        {
            stage.Tick(.3f,0f);Assert.That(stage.ActionTime,Is.EqualTo(.12f).Within(.0001f));
            Assert.That(stage.Opacity,Is.EqualTo(.4f).Within(.0001f));Assert.That(stage.SoundBeat,Is.Zero);
            stage.Tick(1.2f,0f);Assert.That(stage.ActionTime,Is.EqualTo(.48f).Within(.0001f));
            Assert.That(stage.SoundBeat,Is.EqualTo(1));stage.Hide();Assert.That(stage.Active,Is.False);
        });
    }

    [Test]
    public void CinematicMoltShowsTheRealClosedShellBeforeTheBattleClockResumes()
    {
        var model=new GameObject("Cinematic cocoon return fixture");var body=model.AddComponent<SpriteRenderer>();
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.First(d=>d.monsterId=="mantius");
        body.sprite=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true).IdleFrames[0];
        var cocoon=PixelMantisCocoon.Play(body);var clock=new CombatSkillClock(model.transform);float previous=Time.timeScale;
        try
        {
            clock.Hold();Time.timeScale=0f;cocoon.Pose(.57f,0f);cocoon.RefreshPose();
            var shell=(SpriteRenderer)typeof(PixelMantisCocoon).GetField("shell",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cocoon);
            Assert.That(body.enabled,Is.False,"The adult must not flash behind the returning cocoon.");
            Assert.That(shell.enabled,Is.True);Assert.That(shell.transform.localScale.y,Is.GreaterThan(.1f));
        }
        finally{clock.End();Time.timeScale=previous;cocoon.Cancel();Object.DestroyImmediate(model);}
    }

    [Test]
    public void ReturnUsesTheCurrentFieldPoseAndDoesNotRedrawAHiddenOrDeadCaster()
    {
        Check((stage,image)=>
        {
            var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.First(d=>d.monsterId=="velos");
            var actual=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true).IdleFrames[0];
            stage.TestReturn=true;stage.CurrentContext.returnSprite=actual;stage.CurrentContext.returnHeight=1.3f;
            stage.Tick(1f,0f);Assert.That(stage.Returned.enabled,Is.True);Assert.That(stage.Returned.sprite,Is.SameAs(actual));
            stage.CurrentContext.returnBodyVisible=false;stage.Tick(1.1f,0f);Assert.That(stage.Returned.enabled,Is.False);
        });
    }

    [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)]
    public void FieldCocoonUsesTheSameSpeciesArtworkAsTheReviewedStage(int row)
    {
        var model=new GameObject("Species cocoon fixture");var body=model.AddComponent<SpriteRenderer>();
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters.First(d=>d.monsterId=="mantius");
        body.sprite=Resources.Load<GameObject>(definition.resourcePath).GetComponentInChildren<SpriteMonsterAnimationDriver>(true).IdleFrames[0];
        var cocoon=PixelMantisCocoon.Play(body,row);
        try
        {
            cocoon.Pose(.57f,0f);cocoon.RefreshPose();
            var shell=(SpriteRenderer)typeof(PixelMantisCocoon).GetField("shell",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cocoon);
            var atlas=Resources.Load<Texture2D>("CombatCinematics/Rank4V3/mantis/CocoonMotion");
            Assert.That(shell.sprite.texture,Is.SameAs(atlas));
            Assert.That(shell.sprite.rect.x,Is.EqualTo(3*(atlas.width/4)));
            Assert.That(shell.sprite.rect.y,Is.EqualTo((4-row)*(atlas.height/5)));
            var visible=typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic);
            var shellBounds=(Bounds)visible.Invoke(null,new object[]{shell.sprite});
            var bodyBounds=(Bounds)visible.Invoke(null,new object[]{body.sprite});
            Assert.That(shellBounds.size.y*shell.transform.localScale.y,Is.EqualTo(bodyBounds.size.y*.82f).Within(.025f));
        }
        finally{cocoon.Cancel();Object.DestroyImmediate(model);}
    }

    private static void Read(SpatialStage stage,Texture2D image)
    {RenderTexture.active=(RenderTexture)stage.Texture;image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();}

    private static void Check(System.Action<SpatialStage,Texture2D> assertion)
    {
        Assert.That(Shader.Find("Hidden/Rank4PixelGeometry"),Is.Not.Null);
        var texture=new Texture2D(8,8,TextureFormat.RGBA32,false);
        var colors=new Color[64];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;texture.SetPixels(colors);texture.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,8,8),Vector2.one*.5f,8);
        var stage=new SpatialStage();var image=new Texture2D(640,360,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        try
        {
            Assert.That(stage.Begin(new Rank4StageContext{source=new FrogCatchCinematicStage.Actor{sprite=sprite,color=Color.white,size=1f}}),Is.True);
            assertion(stage,image);
        }
        finally
        {
            RenderTexture.active=previous;stage.Dispose();Object.DestroyImmediate(image);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
        }
    }
}
