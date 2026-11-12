using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ReadabilityPreparationPreviewTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Fields=Private|BindingFlags.Public;
    private AllyPreparationSequenceTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object primary,secondary;
    private bool previousEnabled,previousTestMode,previousReduced;
    private CombatReadabilityPreset previousPreset;

    [SetUp]
    public void SetUp()
    {
        previousEnabled=CombatReadabilityPreview.Enabled;
        previousPreset=CombatReadabilityPreview.Preset;
        previousTestMode=CombatEffectTestConfiguration.Active;
        previousReduced=CombatFlashPresentation.Reduced;
        CombatReadabilityPreview.Enabled=true;
        CombatEffectTestConfiguration.Active=true;
        CombatFlashPresentation.Apply(false);
        fixture=new AllyPreparationSequenceTests();fixture.SetUp();
        battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        primary=Get<object>(fixture,"primary");secondary=Get<object>(fixture,"secondary");
    }

    [TearDown]
    public void TearDown()
    {
        try {fixture?.TearDown();}
        finally
        {
            CombatReadabilityPreview.Enabled=previousEnabled;
            CombatReadabilityPreview.Preset=previousPreset;
            CombatEffectTestConfiguration.Active=previousTestMode;
            CombatFlashPresentation.Apply(previousReduced);
        }
    }

    [TestCase(CombatReadabilityPreset.Current,1f,.08f,.5f)]
    [TestCase(CombatReadabilityPreset.Compact,.3f,.45f,.15f)]
    [TestCase(CombatReadabilityPreset.Local,.35f,1f,0f)]
    public void PreparedAlliesUseOnlyTheSelectedTestPolicyAndRestoreTheChosenSpeed(
        CombatReadabilityPreset preset,float duration,float ratio,float alpha)
    {
        CombatReadabilityPreview.Preset=preset;
        Time.timeScale=2f;
        Vector3 position=Model(primary).transform.position,scale=Model(primary).transform.localScale;
        Begin(primary);
        object local=Call(battle,"SkillPresentationFor",primary);
        Assert.That(Get<float>(Mirror(primary),"gatherDuration"),Is.EqualTo(duration));
        Assert.That(Get<double>(battle,"allyPreparationSequenceEndsAt")-Get<double>(local,"startedAt"),
            Is.EqualTo(duration).Within(.000001d));
        Assert.That(Time.timeScale,Is.EqualTo(2f*ratio).Within(.000001f));
        SpriteRenderer shade=Get<SpriteRenderer>(battle,"allyPreparationShade");
        if(alpha==0f)Assert.That(shade==null||!shade.gameObject.activeSelf,Is.True,
            "Local emphasis must not allocate or show a full-screen shade.");
        else
        {
            Assert.That(shade,Is.Not.Null);Assert.That(shade.gameObject.activeSelf,Is.True);
            Assert.That(shade.color.a,Is.EqualTo(alpha));
        }
        Assert.That(Model(primary).transform.position,Is.EqualTo(position));
        Assert.That(Get<float>(Get<SpriteMonsterAnimationDriver>(primary,"spriteAnimation"),"skillFocusScale"),
            Is.EqualTo(CombatReadabilityPreview.SkillBodyScale));
        Assert.That((float)primary.GetType().GetField("collisionRadius").GetValue(primary),Is.EqualTo(.3f));
        Assert.That(Get<float>(primary,"combatBodyScale"),Is.EqualTo(1f));
        Call(battle,"CancelCommonSkillFocus");
        Assert.That(Time.timeScale,Is.EqualTo(2f));
        Assert.That(Vector3.Distance(Model(primary).transform.localScale,scale),Is.LessThan(.00001f));
        Assert.That(Get<bool>(battle,"allyPreparationSequenceActive"),Is.False);
        if(shade!=null)Assert.That(shade.gameObject.activeSelf,Is.False);
    }

    [TestCase(false,true)]
    [TestCase(true,false)]
    public void PreviewControlsCannotChangePreparationOutsideTheOptedInTest(bool enabled,bool testMode)
    {
        CombatReadabilityPreview.Enabled=enabled;CombatEffectTestConfiguration.Active=testMode;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        Time.timeScale=1f;Begin(primary);
        Assert.That(Get<float>(Mirror(primary),"gatherDuration"),Is.EqualTo(1f));
        Assert.That(Time.timeScale,Is.EqualTo(.08f));
        Assert.That(Get<SpriteRenderer>(battle,"allyPreparationShade").color.a,Is.EqualTo(.5f));
    }

    [TestCase(CombatReadabilityPreset.Compact)]
    [TestCase(CombatReadabilityPreset.Local)]
    public void AJoiningAllyKeepsItsOwnCompletePreparationAndUserPauseStaysPaused(CombatReadabilityPreset preset)
    {
        CombatReadabilityPreview.Preset=preset;Begin(primary);
        object first=Call(battle,"SkillPresentationFor",primary);
        Set(first,"startedAt",Time.unscaledTimeAsDouble-.1d);
        Set(battle,"allyPreparationSequenceEndsAt",Time.unscaledTimeAsDouble+.05d);
        Begin(secondary);
        object second=Call(battle,"SkillPresentationFor",secondary);
        Assert.That(Get<double>(battle,"allyPreparationSequenceEndsAt")-Get<double>(second,"startedAt"),
            Is.EqualTo(CombatReadabilityPreview.PreparationDuration).Within(.000001d));
        Time.timeScale=0f;Set(battle,"paused",true);
        Call(battle,"UpdateAllyPreparationSequence",Time.unscaledTimeAsDouble);
        Assert.That(Time.timeScale,Is.Zero);
        Assert.That(Get<bool>(battle,"allyPreparationSequenceActive"),Is.False);
        Assert.That(Get<object>(first,"pending"),Is.Not.Null,"Pausing emphasis must not commit or discard a queued skill.");
        Assert.That(Get<object>(second,"pending"),Is.Not.Null);
        SpriteRenderer shade=Get<SpriteRenderer>(battle,"allyPreparationShade");
        if(shade!=null)Assert.That(shade.gameObject.activeSelf,Is.False);
    }

    [TestCase(CombatReadabilityPreset.Current,3,false)]
    [TestCase(CombatReadabilityPreset.Compact,1,false)]
    [TestCase(CombatReadabilityPreset.Local,1,false)]
    [TestCase(CombatReadabilityPreset.Local,1,true)]
    public void HologramDensityAndSizeChangeWithoutMovingTheCasterOrItsForeground(
        CombatReadabilityPreset preset,int expected,bool reduced)
    {
        CombatReadabilityPreview.Preset=preset;CombatFlashPresentation.Apply(reduced);
        Begin(primary);
        var mirror=Mirror(primary);
        var source=primary.GetType().GetField("spriteAnimation").GetValue(primary) as SpriteMonsterAnimationDriver;
        Vector3 position=source.transform.position,scale=source.transform.localScale;
        double duration=Get<float>(mirror,"gatherDuration");
        Set(mirror,"gatherStartedAt",Time.unscaledTimeAsDouble-duration*.5d);
        Call(mirror,"LateUpdate");
        SpriteRenderer[] ghosts=Get<SpriteRenderer[]>(mirror,"gatherSprites");
        int visible=0;
        foreach(SpriteRenderer ghost in ghosts)
        {
            if(!ghost.enabled)continue;
            visible++;
            Assert.That(ghost.transform.localScale.x,Is.InRange(1.03f,preset==CombatReadabilityPreset.Current?1.7f:1.14f));
            float maximum=.32f*(preset==CombatReadabilityPreset.Current?1f:.45f)*(reduced?.5f:1f);
            Assert.That(ghost.color.a,Is.GreaterThan(0f).And.LessThanOrEqualTo(maximum));
        }
        Assert.That(visible,Is.EqualTo(expected));
        Assert.That(source.transform.position,Is.EqualTo(position));Assert.That(source.transform.localScale,Is.EqualTo(scale));
        Assert.That(mirror.GetComponent<SpriteRenderer>().sortingOrder,Is.EqualTo(30004));
        mirror.Clear();
        foreach(SpriteRenderer ghost in ghosts)Assert.That(ghost.enabled,Is.False);
    }

    [TestCase(CombatReadabilityPreset.Local,true,false,true)]
    [TestCase(CombatReadabilityPreset.Local,true,true,true)]
    [TestCase(CombatReadabilityPreset.Current,true,false,false)]
    [TestCase(CombatReadabilityPreset.Compact,true,false,false)]
    [TestCase(CombatReadabilityPreset.Local,false,false,false)]
    public void OnlyLocalPreparationKeepsItsExistingPeakUntilJustBeforeRelease(
        CombatReadabilityPreset preset,bool enabled,bool reduced,bool delayedTail)
    {
        CombatReadabilityPreview.Preset=preset;CombatReadabilityPreview.Enabled=enabled;
        CombatFlashPresentation.Apply(reduced);Begin(primary);
        var mirror=Mirror(primary);
        float duration=Get<float>(mirror,"gatherDuration");
        var ghost=Get<SpriteRenderer[]>(mirror,"gatherSprites")[0];
        float peak=Get<SpriteMonsterAnimationDriver>(primary,"spriteAnimation")
            .GetComponent<SpriteRenderer>().color.a*.32f*(enabled&&preset!=CombatReadabilityPreset.Current?.45f:1f)*(reduced?.5f:1f);
        float[] progress={.09f,.5f,.85f,.95f,1.01f};
        foreach(float t in progress)
        {
            Set(mirror,"gatherStartedAt",Time.unscaledTimeAsDouble-duration*t);
            Call(mirror,"LateUpdate");
            if(t>1f){Assert.That(ghost.enabled,Is.False);continue;}
            Assert.That(ghost.enabled,Is.True);
            Assert.That(ghost.color.a,Is.LessThanOrEqualTo(peak+.001f));
            if(t==.09f)Assert.That(ghost.color.a,Is.EqualTo(peak*.5f).Within(.004f));
            else if(t==.5f||t==.85f&&delayedTail)
                Assert.That(ghost.color.a,Is.EqualTo(peak).Within(.001f));
            else if(t==.85f)Assert.That(ghost.color.a,Is.LessThan(peak*.6f));
            else if(delayedTail)Assert.That(ghost.color.a,Is.EqualTo(peak*.5f).Within(.004f));
            else Assert.That(ghost.color.a,Is.LessThan(peak*.15f));
        }
        Assert.That(Get<float>(mirror,"gatherDuration"),Is.EqualTo(duration),"The release time stays unchanged.");
    }

    private void Begin(object caster)=>Call(fixture,"Begin",caster);
    private SkillFocusSpriteMirror Mirror(object caster)=>Get<SkillFocusSpriteMirror>(Call(battle,"SkillPresentationFor",caster),"mirror");
    private static GameObject Model(object caster)=>(GameObject)caster.GetType().GetField("model").GetValue(caster);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Fields).GetValue(target);
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Fields).SetValue(target,value);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
}
