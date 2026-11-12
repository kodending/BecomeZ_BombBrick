using System;
using NUnit.Framework;
using UnityEngine;

public sealed partial class RemainingRank4CinematicIntegrationTests
{
    [TestCase(0)] [TestCase(1)]
    public void WyvernStoryKeepsTheNativeClockAtZeroAndReturnsTheRealCasterOnlyAtTheFieldFade(int team)
    {
        ReplaceSource("vivre",team);object cast=CreateRealHeavenBreath(source);
        Call("UpdateWyvernHeavenBreaths",.1f);
        Assert.That(Field(cast,"time"),Is.EqualTo(0f));AssertNoDamage();
        BeginQueued(source);var state=Presentation;
        SetField(state,"elapsed",4.10f-Time.unscaledDeltaTime);Call("UpdateRank4Cinematic");
        Assert.That(Renderer(source).forceRenderingOff,Is.True);
        Assert.That(Field(cast,"time"),Is.EqualTo(0f));AssertNoDamage();
        SetField(state,"elapsed",4.25f-Time.unscaledDeltaTime);Call("UpdateRank4Cinematic");
        Assert.That(Renderer(source).forceRenderingOff,Is.False);Assert.That(CombatSkillClock.Active,Is.True);
        Assert.That(Field(cast,"time"),Is.EqualTo(0f));AssertNoDamage();
        Call("FinishRank4Cinematic");Call("UpdateWyvernHeavenBreaths",.1f);
        Assert.That((float)Field(cast,"time"),Is.GreaterThan(0f));AssertNoDamage();
    }

    [TestCase(0)] [TestCase(1)]
    public void WyvernStoryCaptureBorrowsTheCurrentCameraWithoutChangingItsTargetsOrRendererFlags(int team)
    {
        ReplaceSource("vivre",team);object cast=CreateRealHeavenBreath(source);
        Call("BeginLocalSkillPresentation",source);
        var presentation=Call("SkillPresentationFor",source);Assert.That(presentation,Is.Not.Null);
        var mirror=(SkillFocusSpriteMirror)Field(presentation,"mirror");
        mirror.transform.SetParent(null,true);mirror.gameObject.SetActive(true);mirror.Follow(Renderer(source),0f);
        Renderer(source).gameObject.layer=31;mirror.gameObject.layer=31;
        camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var state=Activator.CreateInstance(type.GetNestedType("Rank4Presentation",Hidden),true);
        SetField(state,"request",Queue[0]);
        var target=new RenderTexture(32,32,16);target.Create();
        var active=new RenderTexture(16,16,16);active.Create();
        RenderTexture previousTarget=camera.targetTexture,previousActive=RenderTexture.active;
        var renderer=Renderer(source);bool original=renderer.forceRenderingOff;
        var effect=(PixelWyvernHeavenBreath)Field(cast,"effect");
        var native=effect.GetComponentsInChildren<Renderer>(true);var visibility=new bool[native.Length];
        for(int i=0;i<native.Length;i++)visibility[i]=native[i].forceRenderingOff;
        try
        {
            camera.targetTexture=target;RenderTexture.active=active;
            Call("CaptureWyvernCinematicBattlefield",state);
            Assert.That(camera.targetTexture,Is.SameAs(target));Assert.That(RenderTexture.active,Is.SameAs(active));
            Assert.That(renderer.forceRenderingOff,Is.EqualTo(original));
            for(int i=0;i<native.Length;i++)Assert.That(native[i].forceRenderingOff,Is.EqualTo(visibility[i]));
            var frame=(Texture2D)Field(state,"wyvernBattlefieldFrame");
            var sprite=(Sprite)Field(state,"wyvernBattlefieldSprite");
            var context=(Rank4StageContext)Field(Queue[0],"context");
            Assert.That(frame.width,Is.EqualTo(960));Assert.That(frame.height,Is.EqualTo(540));
            Assert.That(sprite.texture,Is.SameAs(frame));Assert.That(context.wyvernBattlefieldSprite,Is.SameAs(sprite));
            Assert.That(Array.TrueForAll(frame.GetPixels32(),pixel=>pixel.r<=2&&pixel.g<=2&&pixel.b<=2),Is.True,
                "Neither the real source nor its emphasis mirror belongs in the captured ground.");
            Call("ReleaseWyvernCinematicBattlefield",state);Call("ReleaseWyvernCinematicBattlefield",state);
            Assert.That(sprite==null&&frame==null,Is.True);Assert.That(context.wyvernBattlefieldSprite,Is.Null);
            AssertNoDamage();
        }
        finally
        {
            Call("ReleaseWyvernCinematicBattlefield",state);
            camera.targetTexture=previousTarget;RenderTexture.active=previousActive;
            target.Release();active.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(active);
        }
    }

    [Test]
    public void FinishingOrCancellingWyvernStoryDisposesOnlyItsOwnedBattlefieldCapture()
    {
        ReplaceSource("vivre",0);CreateRealHeavenBreath(source);BeginQueued(source);
        var state=Presentation;var context=Stage.CurrentContext;
        var frame=(Texture2D)Field(state,"wyvernBattlefieldFrame");
        var sprite=(Sprite)Field(state,"wyvernBattlefieldSprite");var shared=Renderer(source).sprite.texture;
        Assert.That(frame,Is.Not.Null);Assert.That(context.wyvernBattlefieldSprite,Is.SameAs(sprite));
        Call("FinishRank4Cinematic");Call("FinishRank4Cinematic");
        Assert.That(frame==null&&sprite==null,Is.True);Assert.That(context.wyvernBattlefieldSprite,Is.Null);
        Assert.That(shared!=null,Is.True);Assert.That(CombatSkillClock.Active,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1f));
        AssertNoDamage();
    }
}
