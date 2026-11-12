using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class MushroomDeathBloomEndingTests
{
    [Test]
    public void ExistingSmokeFinishesAfterDamageStopsWithoutWaitingForTheBattle()
    {
        var effect=PixelMushroomDeathBloom.Play(Vector3.zero,4f,8f,null,Color.green);
        try
        {
            effect.Advance(2f);effect.StopEmission();
            CombatEffectEndPlayback.Begin(effect.gameObject,new HashSet<Type>{typeof(PixelMushroomDeathBloom)});
            Assert.That(CombatEffectEndPlayback.IsPreserving(effect),Is.True);
            var playback=effect.GetComponent<CombatEffectEndPlayback>();
            var advance=typeof(CombatEffectEndPlayback).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic);
            advance.Invoke(playback,new object[]{.2f});
            Assert.That(effect!=null&&effect.gameObject.activeInHierarchy,Is.True,"Already emitted smoke needs a visible tail.");
            for(float elapsed=.2f;elapsed<PixelMushroomDeathBloom.TailDuration+.3f&&playback!=null;elapsed+=.1f)
                advance.Invoke(playback,new object[]{.1f});
            Assert.That(effect==null||!effect.gameObject.activeInHierarchy,Is.True,"The decorative tail must finish without another gameplay tick.");
        }
        finally{if(effect!=null)Object.DestroyImmediate(effect.gameObject);}
    }
}
