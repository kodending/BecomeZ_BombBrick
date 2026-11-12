using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ReadabilityRaptorBiteTimingTests
{
    private bool previousTestMode,previousEnabled;
    private CombatReadabilityPreset previousPreset;

    [SetUp]
    public void SetUp()
    {
        previousTestMode=CombatEffectTestConfiguration.Active;
        previousEnabled=CombatReadabilityPreview.Enabled;
        previousPreset=CombatReadabilityPreview.Preset;
        CombatEffectTestConfiguration.Active=true;
        CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
    }

    [TearDown]
    public void TearDown()
    {
        CombatEffectTestConfiguration.Active=previousTestMode;
        CombatReadabilityPreview.Enabled=previousEnabled;
        CombatReadabilityPreview.Preset=previousPreset;
    }

    [TestCase(CombatReadabilityPreset.Compact,.5f)]
    [TestCase(CombatReadabilityPreset.Compact,1f)]
    [TestCase(CombatReadabilityPreset.Compact,2f)]
    [TestCase(CombatReadabilityPreset.Local,.5f)]
    [TestCase(CombatReadabilityPreset.Local,1f)]
    [TestCase(CombatReadabilityPreset.Local,2f)]
    public void AdjustedBiteReachesItsClosedSpriteOnTheDamageBeat(CombatReadabilityPreset preset,float speed)
    {
        CombatReadabilityPreview.Preset=preset;
        float hitTime=.25f/speed;
        float lead=PixelCombatEffect.RaptorBiteLeadTime(hitTime,speed);
        Assert.That(lead+2f/(PixelCombatEffect.RaptorBiteFramesPerSecond*speed),
            Is.EqualTo(hitTime).Within(.00001f));
        var anchor=new GameObject("Readability bite timing target");
        PixelCombatEffect effect=null;
        try
        {
            effect=PixelCombatEffect.PlayRaptorBite(anchor.transform,null,speed);
            Assert.That(effect,Is.Not.Null);
            var frames=(Sprite[])typeof(PixelCombatEffect).GetField("frames",
                BindingFlags.Instance|BindingFlags.NonPublic).GetValue(effect);
            Sprite[] source=PixelCombatEffect.LoadFrames(PixelCombatEffect.RaptorBiteResourcePath);
            CollectionAssert.AreEqual(new[]{source[0],source[1],source[2],source[1],source[0]},frames);
            typeof(PixelCombatEffect).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(effect,new object[]{hitTime-lead+.00001f});
            Assert.That(effect.GetComponent<SpriteRenderer>().sprite,Is.SameAs(source[2]),
                "Damage must coincide with the peak closure, before the jaw starts reopening.");
        }
        finally
        {
            if(effect!=null)Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [TestCase(false,false)]
    [TestCase(false,true)]
    [TestCase(true,false)]
    public void DisabledAndProductionScenesKeepTheOriginalContactTime(bool testMode,bool enabled)
    {
        CombatEffectTestConfiguration.Active=testMode;
        CombatReadabilityPreview.Enabled=enabled;
        AssertLegacyContact();
    }

    [Test]
    public void CurrentComparisonKeepsItsOriginalTiming()
    {
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
        AssertLegacyContact();
    }

    [TestCase(.5f)] [TestCase(1f)] [TestCase(2f)]
    public void ApexBiteKeepsItsAlreadyAlignedContactTime(float speed)
    {
        float hitTime=.25f/speed;
        Assert.That(PixelCombatEffect.RaptorBiteLeadTime(hitTime,speed,true)+
            PixelCombatEffect.RaptorApexBiteImpactFrame/(PixelCombatEffect.RaptorApexBiteFramesPerSecond*speed),
            Is.EqualTo(hitTime).Within(.00001f));
    }

    [TestCase(CombatReadabilityPreset.Compact)]
    [TestCase(CombatReadabilityPreset.Local)]
    public void ActualContactPinsTheClosedSpriteForItsFirstRenderedFrameThenContinues(CombatReadabilityPreset mode)
    {
        CombatReadabilityPreview.Preset=mode;
        PixelCombatEffect effect=PixelCombatEffect.PlayRaptorBite(null,null,1f);
        try
        {
            Call(effect,"Advance",.02f);
            effect.AlignReadabilityBiteContact(effect.ReadabilityBiteVersion);
            var frames=Get<Sprite[]>(effect,"frames");
            float contactTime=2f/PixelCombatEffect.RaptorBiteFramesPerSecond;
            Assert.That(Get<float>(effect,"elapsed"),Is.EqualTo(contactTime));
            Assert.That(effect.GetComponent<SpriteRenderer>().sprite,Is.SameAs(frames[2]));
            Call(effect,"Update");
            Assert.That(Get<float>(effect,"elapsed"),Is.EqualTo(contactTime));
            Assert.That(effect.GetComponent<SpriteRenderer>().sprite,Is.SameAs(frames[2]));

            Call(effect,"Advance",1f/PixelCombatEffect.RaptorBiteFramesPerSecond+.00001f);
            Assert.That(effect.GetComponent<SpriteRenderer>().sprite,Is.SameAs(frames[3]),
                "The contact-frame hold must not stop the remaining reopening animation.");
            effect.AlignReadabilityBiteContact(effect.ReadabilityBiteVersion);
            Set(effect,"readabilityBiteHitFrame",Time.frameCount-1);
            Call(effect,"Update");
            Assert.That(Get<float>(effect,"elapsed"),Is.EqualTo(contactTime+Time.deltaTime).Within(.00001f));
        }
        finally{Object.DestroyImmediate(effect.gameObject);}
    }

    [TestCase(false,true,CombatReadabilityPreset.Local)]
    [TestCase(true,false,CombatReadabilityPreset.Local)]
    [TestCase(true,true,CombatReadabilityPreset.Current)]
    public void ActualContactCannotPinOutsideAnAdjustedComparison(bool testMode,bool enabled,CombatReadabilityPreset mode)
    {
        CombatEffectTestConfiguration.Active=testMode;
        CombatReadabilityPreview.Enabled=enabled;CombatReadabilityPreview.Preset=mode;
        PixelCombatEffect effect=PixelCombatEffect.PlayRaptorBite(null,null,1f);
        try
        {
            Call(effect,"Advance",.02f);
            Sprite before=effect.GetComponent<SpriteRenderer>().sprite;
            effect.AlignReadabilityBiteContact(effect.ReadabilityBiteVersion);
            Assert.That(Get<float>(effect,"elapsed"),Is.EqualTo(.02f));
            Assert.That(effect.GetComponent<SpriteRenderer>().sprite,Is.SameAs(before));
            Assert.That(Get<int>(effect,"readabilityBiteHitFrame"),Is.EqualTo(-1));
        }
        finally{Object.DestroyImmediate(effect.gameObject);}
    }

    [TestCase(false)] [TestCase(true)]
    public void StaleContactCannotPinAPooledEffectOrItsNextUse(bool anotherBite)
    {
        PixelCombatEffect effect=PixelCombatEffect.PlayRaptorBite(null,null,1f);
        PixelCombatEffect reused=null;
        try
        {
            int previousVersion=effect.ReadabilityBiteVersion;
            effect.AlignReadabilityBiteContact(previousVersion);
            effect.ReleaseAfterBattleEndFade();
            effect.AlignReadabilityBiteContact(previousVersion);
            Assert.That(effect.GetComponent<SpriteRenderer>().sprite,Is.Null);
            reused=anotherBite?PixelCombatEffect.PlayRaptorBite(null,null,1f):PixelCombatEffect.PlayCritical(null,null);
            Assert.That(reused,Is.SameAs(effect));
            Assert.That(reused.ReadabilityBiteVersion,Is.Not.EqualTo(previousVersion));
            Assert.That(Get<int>(reused,"readabilityBiteHitFrame"),Is.EqualTo(-1));
            Sprite first=reused.GetComponent<SpriteRenderer>().sprite;
            reused.AlignReadabilityBiteContact(previousVersion);
            Assert.That(Get<float>(reused,"elapsed"),Is.Zero);
            Assert.That(reused.GetComponent<SpriteRenderer>().sprite,Is.SameAs(first));
            if(anotherBite)
            {
                reused.AlignReadabilityBiteContact(reused.ReadabilityBiteVersion);
                Assert.That(reused.GetComponent<SpriteRenderer>().sprite,Is.SameAs(Get<Sprite[]>(reused,"frames")[2]));
            }
            else
            {
                reused.AlignReadabilityBiteContact(reused.ReadabilityBiteVersion);
                Assert.That(reused.GetComponent<SpriteRenderer>().sprite,Is.SameAs(first));
                Call(reused,"Update");
                Assert.That(Get<float>(reused,"elapsed"),Is.EqualTo(Time.deltaTime).Within(.00001f));
            }
        }
        finally
        {
            if(reused!=null)Object.DestroyImmediate(reused.gameObject);
            else if(effect!=null)Object.DestroyImmediate(effect.gameObject);
        }
    }

    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,
        BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,
        BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    private static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,
        BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);

    private static void AssertLegacyContact()
    {
        Assert.That(PixelCombatEffect.RaptorBiteImpactFrame,Is.EqualTo(3));
        Assert.That(PixelCombatEffect.RaptorBiteLeadTime(.25f,1f)+
            PixelCombatEffect.RaptorBiteImpactFrame/PixelCombatEffect.RaptorBiteFramesPerSecond,
            Is.EqualTo(.25f).Within(.00001f));
    }
}
