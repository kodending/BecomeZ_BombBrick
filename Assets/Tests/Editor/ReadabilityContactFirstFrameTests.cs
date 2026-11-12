using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityContactFirstFrameTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private bool testMode,enabled,contact,reduced;
    private CombatReadabilityPreset preset;
    private float speed;
    private GameObject model;

    [SetUp] public void SetUp()
    {
        testMode=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;
        contact=CombatReadabilityPreview.ContactFeedback;preset=CombatReadabilityPreview.Preset;
        speed=Time.timeScale;reduced=CombatFlashPresentation.Reduced;
        CombatEffectTestConfiguration.Active=CombatReadabilityPreview.Enabled=CombatReadabilityPreview.ContactFeedback=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        Time.timeScale=1f;CombatFlashPresentation.Apply(false);
        ClearEffects();
        model=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0])) as GameObject;
    }

    [TearDown] public void TearDown()
    {
        Object.DestroyImmediate(model);ClearEffects();
        CombatEffectTestConfiguration.Active=testMode;CombatReadabilityPreview.Enabled=enabled;
        CombatReadabilityPreview.ContactFeedback=contact;CombatReadabilityPreview.Preset=preset;
        Time.timeScale=speed;CombatFlashPresentation.Apply(reduced);
    }

    [TestCase(false)] [TestCase(true)]
    public void NewContactKeepsItsFirstBeatWhileItsAttackCallbackRemainsLive(bool strong)
    {
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var reaction=model.GetComponent<VoxelEnemyReaction>()??model.AddComponent<VoxelEnemyReaction>();
        int hits=0;Action callback=()=>hits++;
        driver.PlayBasicAttack(1f,callback);
        Vector3 root=model.transform.position;
        reaction.PlayHit(Vector3.right,strong);
        var effect=FindImpact();Sprite first=effect.GetComponent<SpriteRenderer>().sprite;
        float flash=Get<float>(reaction,"flashTimer"),cooldown=Get<float>(reaction,"contactAccentCooldown");
        Call(reaction,"Update");Call(effect,"Update");
        Assert.That(Get<float>(reaction,"flashTimer"),Is.EqualTo(flash));
        Assert.That(Get<float>(reaction,"contactAccentCooldown"),Is.EqualTo(cooldown));
        Assert.That(Get<float>(effect,"elapsed"),Is.Zero);
        Assert.That(effect.GetComponent<SpriteRenderer>().sprite,Is.SameAs(first));
        Assert.That(Get<Action>(driver,"pendingBasicAttackHit"),Is.SameAs(callback));
        Assert.That(hits,Is.Zero);Assert.That(model.transform.position,Is.EqualTo(root));

        Set(reaction,"contactStartedFrame",Time.frameCount-1);Set(effect,"contactStartedFrame",Time.frameCount-1);
        Call(reaction,"Update");Call(effect,"Update");
        Assert.That(Get<float>(reaction,"flashTimer"),Is.EqualTo(Mathf.Max(0f,flash-Time.deltaTime)).Within(.00001f));
        Assert.That(Get<float>(effect,"elapsed"),Is.EqualTo(Time.deltaTime).Within(.00001f));
        driver.NotifyBasicAttackHitFrameFromAnimation();driver.NotifyBasicAttackHitFrameFromAnimation();
        Assert.That(hits,Is.EqualTo(1));
        Call(effect,"Advance",.11f);Call(reaction,"UpdateFlash",.12f);
        Assert.That(PixelCombatEffect.ActiveContactImpacts,Is.Zero);
        Assert.That(Get<float>(reaction,"flashTimer"),Is.Zero);
    }

    [TestCase(false,true,CombatReadabilityPreset.Local)]
    [TestCase(true,false,CombatReadabilityPreset.Local)]
    [TestCase(true,true,CombatReadabilityPreset.Current)]
    public void ProductionOffAndCurrentKeepTheirOriginalElapsedTime(bool test,bool selected,CombatReadabilityPreset mode)
    {
        CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=selected;CombatReadabilityPreview.Preset=mode;
        var reaction=model.GetComponent<VoxelEnemyReaction>()??model.AddComponent<VoxelEnemyReaction>();
        reaction.PlayHit(Vector3.right);var effect=FindImpact();
        float flash=Get<float>(reaction,"flashTimer");
        Call(reaction,"Update");Call(effect,"Update");
        Assert.That(Get<float>(reaction,"flashTimer"),Is.EqualTo(Mathf.Max(0f,flash-Time.deltaTime)).Within(.00001f));
        Assert.That(Get<float>(effect,"elapsed"),Is.EqualTo(Time.deltaTime).Within(.00001f));
    }

    [Test]
    public void ContactProtectionDoesNotDelayDeathOrSpreadToAPooledGenericEffect()
    {
        var reaction=model.GetComponent<VoxelEnemyReaction>()??model.AddComponent<VoxelEnemyReaction>();
        reaction.PlayHit(Vector3.right);var effect=FindImpact();
        Set(reaction,"dying",true);Set(reaction,"capturedDeath",true);
        float death=Get<float>(reaction,"deathTime");Call(reaction,"Update");
        Assert.That(Get<float>(reaction,"deathTime"),Is.EqualTo(death+Time.deltaTime).Within(.00001f));
        Assert.That(Get<float>(reaction,"flashTimer"),Is.EqualTo(.09f));
        effect.ReleaseAfterBattleEndFade();
        var generic=PixelCombatEffect.PlayCritical(null,null);
        try
        {
            Assert.That(generic,Is.SameAs(effect));Call(generic,"Update");
            Assert.That(Get<float>(generic,"elapsed"),Is.EqualTo(Time.deltaTime).Within(.00001f));
        }
        finally{Object.DestroyImmediate(generic.gameObject);}
    }

    private static PixelCombatEffect FindImpact()
    {
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&effect.gameObject.scene.IsValid()&&effect.gameObject.activeInHierarchy&&effect.name=="Contact impact")return effect;
        Assert.Fail("Expected one contact impact");return null;
    }
    private static void ClearEffects()
    {
        foreach(var effect in Resources.FindObjectsOfTypeAll<PixelCombatEffect>())
            if(effect!=null&&effect.gameObject.scene.IsValid()&&(effect.name=="Contact impact"||effect.name=="Contact impact pool"))Object.DestroyImmediate(effect.gameObject);
    }
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    private static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
}
