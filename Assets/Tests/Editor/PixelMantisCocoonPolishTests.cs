using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class PixelMantisCocoonPolishTests
{
    private GameObject model;
    private Texture2D texture;
    private Sprite sprite;
    private SpriteRenderer body;
    private PixelMantisCocoon cocoon;
    private readonly Vector3 originalScale=new Vector3(1.25f,1.4f,1f);

    [SetUp]public void Setup()
    {
        model=new GameObject("Native animated cocoon fixture");body=model.AddComponent<SpriteRenderer>();
        texture=new Texture2D(8,16);texture.SetPixels(Enumerable.Repeat(Color.white,128).ToArray());texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0f,0f,8f,16f),Vector2.one*.5f,16f);body.sprite=sprite;
        body.transform.localScale=originalScale;body.color=new Color(.8f,.9f,.7f,.6f);
    }

    [TearDown]public void Cleanup()
    {
        if(cocoon!=null)cocoon.Cancel();
        Object.DestroyImmediate(model);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
    }

    [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)]
    public void EachSpeciesWrapsThroughFourDrawnFramesThenStaysClosedAndCancelRestoresTheBody(int row)
    {
        bool visible=row%2==0;body.enabled=visible;cocoon=PixelMantisCocoon.Play(body,row);
        var shell=Shell();Assert.That(shell.enabled,Is.True);AssertFrame(shell.sprite,row,0);
        float[] times={0f,.1f,.2f,.3f,.57f,8f};
        for(int i=0;i<times.Length;i++)
        {
            cocoon.Pose(times[i],0f);cocoon.RefreshPose();AssertFrame(shell.sprite,row,Mathf.Min(i,3));
            Assert.That(body.enabled,Is.EqualTo(times[i]<.16f&&visible));
        }
        Assert.That((float)Field("height"),Is.EqualTo(originalScale.y*.82f).Within(.0001f));
        cocoon.Cancel();Assert.That(cocoon==null,Is.True);
        Assert.That(body.enabled,Is.EqualTo(visible));Assert.That(body.transform.localScale,Is.EqualTo(originalScale));
        Assert.That(body.color,Is.EqualTo(new Color(.8f,.9f,.7f,.6f)));
    }

    [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)]
    public void EachSpeciesSplitsTheClosedShellEvenDuringFormationAndFinishRestoresTheBody(int row)
    {
        bool visible=row%2==0;body.enabled=visible;cocoon=PixelMantisCocoon.Play(body,row);
        cocoon.Pose(.1f,0f);cocoon.RefreshPose();cocoon.Finish(row%2==0);
        Assert.That(body.enabled,Is.EqualTo(visible));Assert.That(body.transform.localScale,Is.EqualTo(originalScale));
        cocoon.RefreshPose();Assert.That(Shell().enabled,Is.False);AssertFrame(Shell().sprite,row,3);
        var halves=cocoon.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Split Cocoon Half").ToArray();
        Assert.That(halves,Has.Length.EqualTo(2));
        var atlas=Resources.Load<Texture2D>("CombatCinematics/Rank4V3/mantis/CocoonMotion");
        for(int i=0;i<halves.Length;i++)
        {
            Assert.That(halves[i].enabled,Is.True);Assert.That(halves[i].sprite.texture,Is.SameAs(atlas));
            Assert.That(halves[i].sprite.rect,Is.EqualTo(new Rect(3f*96f+i*48f,(4-row)*96f,48f,96f)));
        }
    }

    private object Field(string name)=>typeof(PixelMantisCocoon).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cocoon);
    private SpriteRenderer Shell()=>(SpriteRenderer)Field("shell");
    private static void AssertFrame(Sprite frame,int row,int column)
    {
        Assert.That(frame.texture,Is.SameAs(Resources.Load<Texture2D>("CombatCinematics/Rank4V3/mantis/CocoonMotion")));
        Assert.That(frame.rect,Is.EqualTo(new Rect(column*96f,(4-row)*96f,96f,96f)));
    }
}
