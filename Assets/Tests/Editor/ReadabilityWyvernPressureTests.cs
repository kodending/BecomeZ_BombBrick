using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityWyvernPressureTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private bool active,enabled,reduced;
    private CombatReadabilityPreset preset;
    private HashSet<GameObject> existing;
    private CombatSkillClock stage;

    [SetUp] public void Setup()
    {
        active=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;preset=CombatReadabilityPreview.Preset;
        reduced=CombatFlashPresentation.Reduced;CombatFlashPresentation.Apply(false);
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
    }
    [TearDown] public void Cleanup()
    {
        stage?.End();
        foreach(var root in Resources.FindObjectsOfTypeAll<GameObject>())
            if(root!=null&&root.scene.IsValid()&&root.transform.parent==null&&!existing.Contains(root))Object.DestroyImmediate(root);
        CombatEffectTestConfiguration.Active=active;CombatReadabilityPreview.Enabled=enabled;CombatReadabilityPreview.Preset=preset;
        CombatFlashPresentation.Apply(reduced);
    }

    [TestCase(CombatReadabilityPreset.Current,true,true,false,false)]
    [TestCase(CombatReadabilityPreset.Compact,true,true,false,false)]
    [TestCase(CombatReadabilityPreset.Local,false,true,false,false)]
    [TestCase(CombatReadabilityPreset.Local,true,false,false,false)]
    [TestCase(CombatReadabilityPreset.Local,true,true,true,false)]
    [TestCase(CombatReadabilityPreset.Local,true,true,false,true)]
    public void OnlyOrdinaryLocalPressureClearsTheInteriorAndPreservesItsBrighterLeadingCrest(
        CombatReadabilityPreset mode,bool test,bool selected,bool cinematic,bool refined)
    {
        Mode(CombatReadabilityPreset.Current,true,true);var original=Create(2);
        if(cinematic){stage=new CombatSkillClock(null);stage.Hold();}
        Mode(mode,test,selected);var preview=Create(2);
        foreach(float time in new[]{.2f,.53f,.57f,.63f,.72f})
        {
            Mode(CombatReadabilityPreset.Current,true,true);original.Pose(time,Vector3.right);
            var body=Read(original,"body");var sparks=Read(original,"sparks");
            Mode(mode,test,selected);preview.Pose(time,Vector3.right);
            Same(body,Read(preview,"body"),refined&&time>=WyvernRank3Rules.PressureWindup);
            Same(sparks,Read(preview,"sparks"),false);
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void AllElementsKeepTheirGeometryAndReposingDoesNotCompoundAlpha(int element)
    {
        Mode(CombatReadabilityPreset.Current,true,true);var original=Create(element);
        original.Pose(.6f,Vector3.right);var before=Read(original,"body");var sparks=Read(original,"sparks");
        Mode(CombatReadabilityPreset.Local,true,true);var preview=Create(element);
        for(int i=0;i<4;i++)
        {
            preview.Pose(.6f,Vector3.right);
            Same(before,Read(preview,"body"),true);Same(sparks,Read(preview,"sparks"),false);
        }
        Assert.That(before.Length,Is.EqualTo(180));Assert.That(sparks.Length,Is.EqualTo(96));
    }

    [Test] public void SourceDeathKeepsTheSameResidualWaveAndOriginalExpiry()
    {
        var source=new GameObject("Readability pressure source").AddComponent<SpriteRenderer>();
        Mode(CombatReadabilityPreset.Current,true,true);var original=Create(2,source);
        Mode(CombatReadabilityPreset.Local,true,true);var preview=Create(2,source);
        source.gameObject.SetActive(false);
        float end=WyvernRank3Rules.PressureWindup+WyvernRank3Rules.PressureDuration+.24f;
        float at=end-.08f;
        Mode(CombatReadabilityPreset.Current,true,true);original.Pose(at,Vector3.right);var body=Read(original,"body");
        Mode(CombatReadabilityPreset.Local,true,true);preview.Pose(at,Vector3.right);Same(body,Read(preview,"body"),true);
        Assert.That(Read(preview,"body").Any(p=>p.startColor.a>0),Is.True);
        original.Pose(end+.01f,Vector3.right);preview.Pose(end+.01f,Vector3.right);
        Assert.That(original==null||!original.gameObject.activeSelf,Is.True);
        Assert.That(preview==null||!preview.gameObject.activeSelf,Is.True);
    }

    [Test] public void FireResidueKeepsItsExistingSparksAfterTheWave()
    {
        Mode(CombatReadabilityPreset.Current,true,true);var original=Create(1);original.PoseFire(.6f);var sparks=Read(original,"sparks");
        Mode(CombatReadabilityPreset.Local,true,true);var preview=Create(1);preview.PoseFire(.6f);
        Same(sparks,Read(preview,"sparks"),false);
        Assert.That(Read(preview,"body"),Is.Empty);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void PressureContactKeepsItsFullAuthoredBurst(int element)
    {
        Mode(CombatReadabilityPreset.Local,true,true);
        PixelWyvernPressure.Hit(Vector3.zero,null,element,Vector3.right);
        var hit=Object.FindObjectsOfType<ParticleSystem>().Single(p=>p.name=="Pressure Breath Contact"&&!existing.Contains(p.gameObject));
        var particles=Read(hit);Assert.That(particles.Length,Is.EqualTo(16));
        Assert.That(particles[0].startLifetime,Is.EqualTo(.045f).Within(.0001f));
        Assert.That(particles[0].startSize,Is.EqualTo(.32f*1.215f).Within(.0001f));
        Assert.That(particles.All(p=>p.startColor.a==255),Is.True);
    }

    private static PixelWyvernPressure Create(int element,SpriteRenderer source=null)=>
        PixelWyvernPressure.Create(source,Vector3.zero,Vector3.right,6.8f,element,()=>0f,()=>true);
    private static void Mode(CombatReadabilityPreset mode,bool test,bool selected)
    {CombatReadabilityPreview.Preset=mode;CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=selected;}
    private static ParticleSystem.Particle[] Read(PixelWyvernPressure effect,string field)=>
        Read((ParticleSystem)typeof(PixelWyvernPressure).GetField(field,Hidden).GetValue(effect));
    private static ParticleSystem.Particle[] Read(ParticleSystem ps)
    {
        var data=new ParticleSystem.Particle[ps.main.maxParticles];int count=ps.GetParticles(data);Array.Resize(ref data,count);return data;
    }
    private static void Same(ParticleSystem.Particle[] before,ParticleSystem.Particle[] after,bool refinedBody)
    {
        Assert.That(after.Length,Is.EqualTo(before.Length));Assert.That(before,Is.Not.Empty);
        for(int i=0;i<before.Length;i++)
        {
            Assert.That(after[i].position,Is.EqualTo(before[i].position));
            Assert.That(Vector3.Distance(after[i].startSize3D,before[i].startSize3D*CombatReadabilityPreview.EffectVisualScale),Is.LessThan(.0001f));
            Assert.That(after[i].rotation,Is.EqualTo(before[i].rotation));
            Assert.That(after[i].startLifetime,Is.EqualTo(before[i].startLifetime));
            var a=after[i].startColor;var b=before[i].startColor;
            Assert.That(a.r,Is.EqualTo(b.r));Assert.That(a.g,Is.EqualTo(b.g));Assert.That(a.b,Is.EqualTo(b.b));
            Assert.That((float)a.a,Is.EqualTo(b.a*(refinedBody?(i<60?.85f:.45f):1f)).Within(2f));
        }
    }
}
