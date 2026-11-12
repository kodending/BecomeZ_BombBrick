using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MushroomBloomArtworkTests
{
    [Test]
    public void DedicatedBlossomHasTwelveUniqueDrawingsWithoutReplacingTheRankThreeKing()
    {
        var bloom=PixelMycoking.DeathBloomFrames;var legacy=PixelMycoking.FlowerFrames;
        Assert.That(bloom,Has.Length.EqualTo(12));Assert.That(legacy,Has.Length.EqualTo(16));
        var signatures=new HashSet<uint>();
        foreach(Sprite frame in bloom)
        {
            Assert.That(frame.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Rect rect=frame.rect;var pixels=frame.texture.GetPixels((int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height);
            uint hash=2166136261;
            foreach(Color pixel in pixels)
            {Color32 color=pixel;unchecked{hash=(hash^color.r)*16777619;hash=(hash^color.g)*16777619;hash=(hash^color.b)*16777619;hash=(hash^color.a)*16777619;}}
            Assert.That(signatures.Add(hash),Is.True,"Every stage of the blossom must be an authored drawing.");
            Assert.That(frame.pivot.x/frame.rect.width,Is.EqualTo(.5f).Within(.001f));
            Assert.That(frame.pivot.y/frame.rect.height,Is.EqualTo(.0625f).Within(.001f));
        }
        Assert.That(bloom.All(frame=>frame.texture!=legacy[0].texture),Is.True);
        Assert.That(PixelMycoking.SporeBudSprite,Is.SameAs(legacy[0]));
    }

    [Test]
    public void OnlyTheDeathFlowerUsesNewGrowthBreathingAndWitheringFrames()
    {
        var legacy=PixelMycoking.Play(null,Vector3.zero,Color.magenta,()=>false);
        var flower=PixelMycoking.Play(null,Vector3.right*3f,Color.magenta,()=>false);
        try
        {
            flower.UseDeathBloomArtwork();
            float[] times={.02f,.15f,.27f,.38f,.44f,.95f,1.45f,1.95f};
            for(int i=0;i<times.Length;i++)
            {
                flower.Pose(times[i]);legacy.Pose(times[i]);
                Assert.That(flower.BodyRenderer.sprite,Is.SameAs(PixelMycoking.DeathBloomFrames[i]));
                Assert.That(legacy.BodyRenderer.sprite,Is.SameAs(PixelMycoking.FlowerFrames[PixelMycoking.FrameAt(times[i])]));
            }
            flower.Depart();
            var field=typeof(PixelMycoking).GetField("depart",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            float[] exits={.02f,.17f,.34f,.53f};
            for(int i=0;i<exits.Length;i++)
            {field.SetValue(flower,exits[i]);flower.Pose(2f);Assert.That(flower.BodyRenderer.sprite,Is.SameAs(PixelMycoking.DeathBloomFrames[8+i]));}
        }
        finally{flower.Cancel();legacy.Cancel();}
    }

    [Test]
    public void TheNewFlowerDoesNotEmitTheLegacyRadialMistOrOpeningDirt()
    {
        var flower=PixelMycoking.Play(null,Vector3.zero,Color.magenta,()=>false);
        try
        {
            flower.UseDeathBloomArtwork();
            for(float age=0f;age<8f;age+=.1f)
            {
                flower.Pose(age);
                Assert.That(flower.GetComponentsInChildren<ParticleSystem>().All(p=>p.particleCount==0),Is.True,
                    "The rolling smoke system owns the new plume; legacy radial particles must stay disabled.");
            }
            Assert.That(flower.Outline.TargetRenderers.Contains(flower.BodyRenderer),Is.True);
        }
        finally{flower.Cancel();}
    }
}
