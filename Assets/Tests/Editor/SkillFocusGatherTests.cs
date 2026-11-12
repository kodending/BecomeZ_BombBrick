using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is=NUnit.Framework.Is;
using Object=UnityEngine.Object;

public sealed class SkillFocusGatherTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private GameObject sourceHost,mirrorHost;
    private SpriteRenderer source;
    private SkillFocusSpriteMirror mirror;
    private Texture2D texture;
    private Sprite first,second;
    private float previousSpeed;
    private bool previousReduced;

    [SetUp]
    public void SetUp()
    {
        previousSpeed=Time.timeScale;previousReduced=CombatFlashPresentation.Reduced;
        CombatFlashPresentation.Apply(false);
        texture=new Texture2D(4,2,TextureFormat.RGBA32,false);
        var pixels=new Color[8];for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white;
        texture.SetPixels(pixels);texture.Apply();
        first=Sprite.Create(texture,new Rect(0f,0f,2f,2f),Vector2.one*.5f,2f);
        second=Sprite.Create(texture,new Rect(2f,0f,2f,2f),Vector2.one*.5f,2f);
        sourceHost=new GameObject("Gather source");sourceHost.layer=10;
        source=sourceHost.AddComponent<SpriteRenderer>();source.sprite=first;source.sortingOrder=10;
        mirrorHost=new GameObject("Gather foreground");mirror=mirrorHost.AddComponent<SkillFocusSpriteMirror>();
        mirror.Follow(source);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(mirrorHost);Object.DestroyImmediate(sourceHost);
        ForgetVisibleBody(first);ForgetVisibleBody(second);
        Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(texture);
        CombatFlashPresentation.Apply(previousReduced);Time.timeScale=previousSpeed;
    }

    [Test]
    public void ThreeStaggeredSilhouettesExpandSlowlyFromTheBodyAndFinishTogether()
    {
        Time.timeScale=0f;
        Vector3 position=source.transform.position,scale=source.transform.localScale;
        Sample(.025d);AssertVisible(1);
        float firstScale=Ghosts[0].transform.localScale.x;
        Sample(.06d);AssertVisible(2);
        Sample(.1d);AssertVisible(3);
        Sample(.23d);AssertVisible(3);
        Assert.That(Ghosts[0].transform.localScale.x,Is.GreaterThan(firstScale));
        for(int i=0;i<Ghosts.Length;i++)
        {
            Assert.That(Ghosts[i].transform.localScale.x,Is.InRange(1.03f,1.42f+i*.14f));
            Assert.That(Ghosts[i].transform.localPosition,Is.EqualTo(Vector3.zero));
            Assert.That(Ghosts[i].sortingOrder,Is.EqualTo(30001+i));
            Assert.That(Ghosts[i].gameObject.layer,Is.Zero);
            Assert.That(Ghosts[i].color.a,Is.GreaterThan(0f).And.LessThanOrEqualTo(.32f));
            Assert.That(Ghosts[i].color.r,Is.EqualTo(.95f).Within(.00001f));
            Assert.That(Ghosts[i].color.g,Is.EqualTo(.78f).Within(.00001f));
            Assert.That(Ghosts[i].color.b,Is.EqualTo(.35f).Within(.00001f));
        }
        Sample(.499d);AssertVisible(3);
        for(int i=0;i<Ghosts.Length;i++)
            Assert.That(Ghosts[i].transform.localScale.x,Is.EqualTo(1.42f+i*.14f).Within(.0001f));
        Sample(.5d);AssertVisible(0);
        Assert.That(mirrorHost.GetComponent<SpriteRenderer>().enabled,Is.True);
        Assert.That(mirrorHost.GetComponent<SpriteRenderer>().sortingOrder,Is.EqualTo(30004));
        Assert.That(source.sortingOrder,Is.EqualTo(10));
        Assert.That(source.transform.position,Is.EqualTo(position));
        Assert.That(source.transform.localScale,Is.EqualTo(scale));
        Assert.That(Time.timeScale,Is.Zero);
    }

    [Test]
    public void EveryGatherLayerTracksTheCurrentSpriteFacingMaterialPropertyBlockAndBodyPose()
    {
        source.sprite=second;source.flipX=true;source.flipY=true;
        source.color=new Color(.7f,.6f,.4f,.8f);
        source.transform.SetPositionAndRotation(new Vector3(2f,1f,-3f),Quaternion.Euler(45f,15f,8f));
        source.transform.localScale=new Vector3(1.3f,.9f,.8f);
        int effect=Shader.PropertyToID("_GatherTestWeight");
        var properties=new MaterialPropertyBlock();properties.SetFloat(effect,.67f);source.SetPropertyBlock(properties);
        Sample(.23d);AssertVisible(3);
        foreach(var ghost in Ghosts)
        {
            Assert.That(ghost.sprite,Is.SameAs(second));
            Assert.That(ghost.flipX,Is.True);Assert.That(ghost.flipY,Is.True);
            Assert.That(ghost.sharedMaterial,Is.SameAs(source.sharedMaterial));
            ghost.GetPropertyBlock(properties);Assert.That(properties.GetFloat(effect),Is.EqualTo(.67f));
            Assert.That(Quaternion.Angle(ghost.transform.rotation,source.transform.rotation),Is.LessThan(.001f));
            Vector3 origin=ghost.transform.position-ghost.transform.parent.TransformVector(ghost.transform.localPosition);
            Assert.That(Vector3.Distance(origin,source.transform.position),Is.LessThan(.00001f));
            Assert.That(ghost.transform.localScale.y,Is.EqualTo(ghost.transform.localScale.x));
        }
        Assert.That(mirrorHost.transform.position,Is.EqualTo(source.transform.position));
        Assert.That(Vector3.Distance(mirrorHost.transform.lossyScale,source.transform.lossyScale),Is.LessThan(.00001f));
        Assert.That(source.sortingOrder,Is.EqualTo(10));
        Assert.That(source.GetComponentsInChildren<SpriteRenderer>(true),Has.Length.EqualTo(1));
    }

    [Test]
    public void ReducedFlashesHalvesGatherOpacityWithoutChangingItsTravelOrDuration()
    {
        Sample(.23d);
        var alpha=new float[3];var scale=new Vector3[3];var offset=new Vector3[3];
        for(int i=0;i<3;i++){alpha[i]=Ghosts[i].color.a;scale[i]=Ghosts[i].transform.localScale;offset[i]=Ghosts[i].transform.localPosition;}
        CombatFlashPresentation.Apply(true);Sample(.23d);
        for(int i=0;i<3;i++)
        {
            Assert.That(Ghosts[i].color.a,Is.EqualTo(alpha[i]*.5f).Within(.002f));
            Assert.That(Vector3.Distance(Ghosts[i].transform.localScale,scale[i]),Is.LessThan(.005f));
            Assert.That(Vector3.Distance(Ghosts[i].transform.localPosition,offset[i]),Is.LessThan(.005f));
        }
        Sample(.501d);AssertVisible(0);
    }

    [Test]
    public void ChargeDurationScalesTheStaggerAndExpansionWithoutChangingTheTravel()
    {
        Sample(.23d);AssertVisible(3);
        var scale=new Vector3[3];var offset=new Vector3[3];
        for(int i=0;i<3;i++){scale[i]=Ghosts[i].transform.localScale;offset[i]=Ghosts[i].transform.localPosition;}

        mirror.Follow(source,1f);
        Sample(.05d);AssertVisible(1);
        Sample(.12d);AssertVisible(2);
        Sample(.2d);AssertVisible(3);
        Sample(.46d);AssertVisible(3);
        for(int i=0;i<3;i++)
        {
            Assert.That(Vector3.Distance(Ghosts[i].transform.localScale,scale[i]),Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(Ghosts[i].transform.localPosition,offset[i]),Is.LessThan(.00001f));
        }
        Sample(.999d);AssertVisible(3);
        Sample(1d);AssertVisible(0);
        Assert.That(mirrorHost.GetComponent<SpriteRenderer>().enabled,Is.True);
    }

    [Test]
    public void ZeroChargeHidesGatherLayersAndKeepsTheForegroundWithoutAllocatingNewRenderers()
    {
        Sample(.23d);AssertVisible(3);
        var retained=Ghosts;
        mirror.Follow(source,0f);
        AssertVisible(0);
        Sample(.23d);AssertVisible(0);
        Assert.That(mirrorHost.GetComponent<SpriteRenderer>().enabled,Is.True);
        Assert.That(mirrorHost.GetComponent<SpriteRenderer>().sortingOrder,Is.EqualTo(30004));

        mirror.Follow(source,.5f);
        Sample(.23d);AssertVisible(3);
        Assert.That(Ghosts,Is.SameAs(retained));
        Assert.That(mirrorHost.GetComponentsInChildren<SpriteRenderer>(true),Has.Length.EqualTo(4));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClearAndDisableHideEveryLayerAndTheNextFocusReusesTheSameRenderers(bool disable)
    {
        var retained=Ghosts;Sample(.23d);AssertVisible(3);
        if(disable)Call("OnDisable");else mirror.Clear();
        AssertVisible(0);Assert.That(mirrorHost.GetComponent<SpriteRenderer>().enabled,Is.False);
        mirror.Follow(source);
        Assert.That(Ghosts,Is.SameAs(retained));
        Assert.That(mirrorHost.GetComponentsInChildren<SpriteRenderer>(true),Has.Length.EqualTo(4));
        Sample(.025d);AssertVisible(1);
        Sample(.23d);AssertVisible(3);
        Assert.That(mirrorHost.GetComponent<SpriteRenderer>().enabled,Is.True);
    }

    [Test]
    public void UpdatingPreparedGatherLayersDoesNotAllocateManagedMemory()
    {
        Sample(.23d);
        var update=(Action)Delegate.CreateDelegate(typeof(Action),mirror,
            typeof(SkillFocusSpriteMirror).GetMethod("LateUpdate",Private));
        for(int i=0;i<8;i++)update();
        TestDelegate work=()=>{for(int i=0;i<16;i++)update();};
        Assert.That(work,UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        Assert.That(mirrorHost.GetComponentsInChildren<SpriteRenderer>(true),Has.Length.EqualTo(4));
    }

    [Test]
    public void DiffusionStaysVisibleThroughTheMiddleAndFadesWhileContinuingOutward()
    {
        Sample(.24d);AssertVisible(3);
        var peakAlpha=new float[3];var previousScale=new float[3];
        for(int i=0;i<3;i++)
        {
            peakAlpha[i]=Ghosts[i].color.a;previousScale[i]=Ghosts[i].transform.localScale.x;
            Assert.That(peakAlpha[i],Is.EqualTo(.32f-i*.07f).Within(.00001f));
        }
        Sample(.4d);AssertVisible(3);
        for(int i=0;i<3;i++)
        {
            Assert.That(Ghosts[i].color.a,Is.GreaterThan(0f).And.LessThan(peakAlpha[i]));
            Assert.That(Ghosts[i].transform.localScale.x,Is.GreaterThan(previousScale[i]));
            peakAlpha[i]=Ghosts[i].color.a;previousScale[i]=Ghosts[i].transform.localScale.x;
        }
        Sample(.49d);AssertVisible(3);
        for(int i=0;i<3;i++)
        {
            Assert.That(Ghosts[i].color.a,Is.GreaterThan(0f).And.LessThan(peakAlpha[i]));
            Assert.That(Ghosts[i].transform.localScale.x,Is.GreaterThan(previousScale[i]));
        }
        Sample(.5d);AssertVisible(0);
    }

    [TestCase(false,false)] [TestCase(true,false)] [TestCase(false,true)] [TestCase(true,true)]
    public void ExpansionKeepsTheVisibleBodyCenterFixedForOffCenterPivotsAndFlips(bool flipX,bool flipY)
    {
        var offCenter=Sprite.Create(texture,new Rect(0f,0f,2f,2f),Vector2.zero,2f);
        try
        {
            source.sprite=offCenter;source.flipX=flipX;source.flipY=flipY;
            source.transform.SetPositionAndRotation(new Vector3(2f,1f,-3f),Quaternion.Euler(45f,15f,8f));
            source.transform.localScale=new Vector3(1.3f,.9f,.8f);
            mirror.Follow(source);
            Vector3 center=offCenter.bounds.center;
            if(flipX)center.x=-center.x;if(flipY)center.y=-center.y;
            foreach(double elapsed in new[]{.12d,.3d,.49d})
            {
                Sample(elapsed);AssertVisible(3);
                foreach(var ghost in Ghosts)
                    Assert.That(Vector3.Distance(ghost.transform.TransformPoint(center),source.transform.TransformPoint(center)),
                        Is.LessThan(.00001f),"Outward growth must use the body center instead of dragging an off-center pivot.");
            }
            source.sprite=second;Sample(.3d);
            foreach(var ghost in Ghosts)
                Assert.That(Vector3.Distance(ghost.transform.localPosition,center*(1f-ghost.transform.localScale.x)),
                    Is.LessThan(.00001f),"Changing an attack frame must preserve the anchor captured at charge acceptance.");
        }
        finally{source.sprite=first;ForgetVisibleBody(offCenter);Object.DestroyImmediate(offCenter);}
    }

    private static void ForgetVisibleBody(Sprite sprite)
    {
        var cache=(IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        cache.Remove(sprite);
    }

    private SpriteRenderer[] Ghosts=>(SpriteRenderer[])typeof(SkillFocusSpriteMirror)
        .GetField("gatherSprites",Private).GetValue(mirror);
    private void Sample(double elapsed)
    {
        typeof(SkillFocusSpriteMirror).GetField("gatherStartedAt",Private)
            .SetValue(mirror,Time.unscaledTimeAsDouble-elapsed);
        Call("LateUpdate");
    }
    private void Call(string method)=>typeof(SkillFocusSpriteMirror).GetMethod(method,Private).Invoke(mirror,null);
    private void AssertVisible(int count)
    {
        int visible=0;foreach(var ghost in Ghosts)if(ghost.enabled)visible++;
        Assert.That(visible,Is.EqualTo(count));
    }
}
