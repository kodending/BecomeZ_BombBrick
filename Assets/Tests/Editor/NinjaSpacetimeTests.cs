using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

internal sealed class NinjaSpacetimeFixture:IDisposable
{
    internal const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal readonly Type type=typeof(MonsterAutoBattlePrototype),unitType;
    internal readonly MonsterAutoBattlePrototype battle;
    internal readonly object source,enemy;
    private readonly HashSet<GameObject> existing;
    private readonly List<Object> assets=new List<Object>();
    private readonly float originalSpeed;
    internal NinjaSpacetimeFixture()
    {
        originalSpeed=Time.timeScale;existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Ninja spacetime contract");host.SetActive(false);battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var run=type.GetField("expeditionRun",Private);run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        Field("mlEpisodeActive",true);Field("combatBounds",new Bounds(Vector3.zero,Vector3.one*1000f));
        source=Unit(0,-3f,"chundongi");enemy=Unit(1,3f,"woongi");
        Set(source,"augmentTranscended",true);Set(source,"target",enemy);
    }
    internal object Unit(int team,float x,string species="woongi")
    {
        var unit=Activator.CreateInstance(unitType,true);var units=(IList)type.GetField("units",Private).GetValue(battle);
        Set(unit,"id",units.Count);Set(unit,"stableId","spacetime-"+units.Count);
        var model=new GameObject("Spacetime unit "+units.Count);model.transform.position=new Vector3(x,0f,units.Count%3*4f);
        Set(unit,"model",model);Set(unit,"ring",new GameObject("Spacetime ring"));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"skillId",species=="woongi"?BearCombatRules.SkillId:NinjaCatCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);Set(unit,"damage",100f);
        Set(unit,"attackInterval",1f);Set(unit,"collisionRadius",.3f);Set(unit,"facingDirection",Vector3.right);
        Set(unit,"skillCooldownDuration",8.5f);units.Add(unit);return unit;
    }
    internal object Get(object unit,string name)=>unitType.GetField(name).GetValue(unit);
    internal void Set(object unit,string name,object value)=>unitType.GetField(name).SetValue(unit,value);
    internal void Field(string name,object value)=>type.GetField(name,Private).SetValue(battle,value);
    internal object Call(string name,params object[] args)=>type.GetMethod(name,Private).Invoke(battle,args);
    internal int Int(object unit,string field)=>(int)Get(unit,field);
    internal float Float(object unit,string field)=>(float)Get(unit,field);
    internal GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    internal bool Active=>(bool)Call("NinjaSpacetimeActive",source);
    internal bool Begin()=>(bool)Call("BeginNinjaSpacetime",source,GameMasterData.Skill(NinjaCatCombatRules.SkillId));
    internal void Advance(float time)=>Call("AdvanceNinjaSpacetime",source,time);
    internal SpriteRenderer Renderer(object unit)
    {
        var sprite=Model(unit).GetComponent<SpriteRenderer>();if(sprite!=null)return sprite;
        var texture=new Texture2D(8,8){filterMode=FilterMode.Point};var pixels=new Color[64];for(int i=0;i<64;i++)pixels[i]=Color.white;
        texture.SetPixels(pixels);texture.Apply();assets.Add(texture);
        var image=Sprite.Create(texture,new Rect(0,0,8,8),new Vector2(.5f,.125f),8f);assets.Add(image);
        sprite=Model(unit).AddComponent<SpriteRenderer>();sprite.sprite=image;return sprite;
    }
    internal PixelNinjaSpacetime AttachEffect()
    {
        var effect=PixelNinjaSpacetime.Create(new Bounds(Vector3.zero,new Vector3(20f,0f,12f)),Renderer(source),new[]{Renderer(enemy).bounds},"chundongi");
        var casts=(IDictionary)type.GetField("ninjaSpacetimeCasts",Private).GetValue(battle);object cast=casts[source];
        cast.GetType().GetField("effect").SetValue(cast,effect);
        ((IList)type.GetField("ninjaSpacetimeEffects",Private).GetValue(battle)).Add(effect);return effect;
    }
    public void Dispose()
    {
        Call("ResetNinjaSpacetime");
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)Object.DestroyImmediate(go);
        foreach(var asset in assets)if(asset!=null)Object.DestroyImmediate(asset);
        Time.timeScale=originalSpeed;
    }
}

public sealed class NinjaSpacetimeTests
{
    private NinjaSpacetimeFixture f;
    [SetUp] public void SetUp()=>f=new NinjaSpacetimeFixture();
    [TearDown] public void TearDown()=>f.Dispose();

    [TestCase("chundongi",0)] [TestCase("seolmyo",0)] [TestCase("sundongi",0)] [TestCase("hongryeon",0)]
    [TestCase("chundongi",1)] [TestCase("seolmyo",1)] [TestCase("sundongi",1)] [TestCase("hongryeon",1)]
    public void EverySpeciesAndTeamHitsEightDistantOpponentsExactlyOnce(string species,int team)
    {
        f.Set(f.source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        var teamType=f.unitType.GetField("team").FieldType;
        f.Set(f.source,"team",Enum.ToObject(teamType,team));f.Set(f.enemy,"team",Enum.ToObject(teamType,1-team));
        var victims=new List<object>{f.enemy};for(int i=1;i<8;i++)victims.Add(f.Unit(1-team,25f+i*30f));
        var ally=f.Unit(team,-25f);
        Assert.That(f.Begin(),Is.True);f.Advance(NinjaSpacetimeRules.Windup-.001f);
        foreach(var victim in victims)Assert.That(f.Float(victim,"hp"),Is.EqualTo(5000f));
        f.Advance(NinjaSpacetimeRules.Windup);f.Advance(NinjaSpacetimeRules.Windup);f.Advance(.2f);f.Advance(1.2f);
        foreach(var victim in victims)Assert.That(f.Float(victim,"hp"),Is.EqualTo(4700f).Within(.01f));
        Assert.That(f.Float(ally,"hp"),Is.EqualTo(5000f));Assert.That(f.Int(f.source,"skillHits"),Is.EqualTo(8));
        Assert.That(f.Float(f.source,"skillDamage"),Is.EqualTo(2400f).Within(.01f));
        Assert.That(f.Int(f.source,"skillUses"),Is.EqualTo(1));Assert.That(f.Float(f.source,"skillTimer"),Is.EqualTo(8.5f));
    }

    [Test] public void OriginalTargetCanDieOrDisappearWhileNewTargetsAreSelectedAtImpact()
    {
        Assert.That(f.Begin(),Is.True);f.Set(f.enemy,"alive",false);f.Set(f.enemy,"hp",0f);f.Set(f.source,"target",null);
        var late=f.Unit(1,350f);f.Advance(NinjaSpacetimeRules.Windup);
        Assert.That(f.Float(late,"hp"),Is.EqualTo(4700f).Within(.01f));Assert.That(f.Int(f.source,"skillHits"),Is.EqualTo(1));
    }

    [Test] public void DamageUsesNormalDefenseWithoutCrowdControlOrKnockback()
    {
        f.Set(f.enemy,"defense",100f);Vector3 position=f.Model(f.enemy).transform.position;
        Assert.That(f.Begin(),Is.True);f.Advance(NinjaSpacetimeRules.Windup);
        Assert.That(f.Float(f.enemy,"hp"),Is.EqualTo(4850f).Within(.01f));
        Assert.That(((CombatStatusState)f.Get(f.enemy,"combatStatuses")).HarmfulCount,Is.Zero);
        Assert.That(f.Get(f.enemy,"displacing"),Is.False);Assert.That(f.Get(f.enemy,"inertiaVelocity"),Is.EqualTo(Vector3.zero));
        Assert.That(f.Model(f.enemy).transform.position,Is.EqualTo(position));
    }

    [Test] public void UnavailableAndAlliedUnitsAreNotHit()
    {
        var hidden=f.Unit(1,15f);f.Set(hidden,"ninjaCrossActive",true);
        var dead=f.Unit(1,25f);f.Set(dead,"alive",false);
        var stored=f.Unit(1,35f);f.Set(stored,"inStorage",true);
        Assert.That(f.Begin(),Is.True);f.Advance(NinjaSpacetimeRules.Windup);
        foreach(var untouched in new[]{hidden,dead,stored})Assert.That(f.Float(untouched,"hp"),Is.EqualTo(5000f));
        Assert.That(f.Int(f.source,"skillHits"),Is.EqualTo(1));
    }

    [TestCase("death")] [TestCase("generation")] [TestCase("battle_end")] [TestCase("reset")]
    public void CancellationBeforeImpactCannotApplyLateDamage(string reason)
    {
        Assert.That(f.Begin(),Is.True);f.Advance(.5f);
        if(reason=="death"){f.Set(f.source,"alive",false);f.Set(f.source,"hp",0f);}
        if(reason=="generation")f.Field("combatGeneration",1);
        if(reason=="battle_end")f.Field("battleEnding",true);
        if(reason=="reset")f.Call("ResetNinjaSpacetime");
        f.Advance(NinjaSpacetimeRules.Duration);
        Assert.That(f.Active,Is.False);Assert.That(f.Float(f.enemy,"hp"),Is.EqualTo(5000f));
        Assert.That(f.Int(f.source,"skillHits"),Is.Zero);
    }

    [Test] public void AbsoluteFilmClockAndWorldClockDoNotDoubleAdvanceQueuedCasts()
    {
        Assert.That(f.Begin(),Is.True);f.Field("battleStarted",true);
        Type requestType=f.type.GetNestedType("Rank4PresentationRequest",BindingFlags.NonPublic);
        object request=Activator.CreateInstance(requestType,true);
        requestType.GetField("source").SetValue(request,f.source);
        requestType.GetField("generation").SetValue(request,0);
        requestType.GetField("context").SetValue(request,new Rank4StageContext{family="ninja_cat"});
        var queue=(IList)f.type.GetField("rank4PresentationQueue",NinjaSpacetimeFixture.Private).GetValue(f.battle);queue.Add(request);
        f.Call("UpdateFamilyTranscendence",f.source,5f);Assert.That(f.Int(f.source,"skillHits"),Is.Zero);
        f.Advance(.55f);f.Advance(.55f);f.Call("UpdateFamilyTranscendence",f.source,5f);
        Assert.That(f.Int(f.source,"skillHits"),Is.Zero);
        queue.Clear();f.Call("UpdateFamilyTranscendence",f.source,.54f);Assert.That(f.Int(f.source,"skillHits"),Is.Zero);
        f.Call("UpdateFamilyTranscendence",f.source,.02f);Assert.That(f.Int(f.source,"skillHits"),Is.EqualTo(1));
    }

    [Test] public void DuplicateCastIsRejectedAndGenerationResetAllowsOneFreshCast()
    {
        Assert.That(f.Begin(),Is.True);Assert.That(f.Begin(),Is.False);Assert.That(f.Int(f.source,"skillUses"),Is.EqualTo(1));
        Assert.That(f.Call("TryUseSkill",f.source,true),Is.False);
        f.Field("combatGeneration",1);f.Advance(1.1f);Assert.That(f.Active,Is.False);
        Assert.That(f.Begin(),Is.True);f.Advance(NinjaSpacetimeRules.Duration);
        Assert.That(f.Float(f.enemy,"hp"),Is.EqualTo(4700f).Within(.01f));Assert.That(f.Int(f.source,"skillUses"),Is.EqualTo(2));
        Assert.That(f.Active,Is.False);Assert.That(f.Float(f.source,"attackMovementLockTimer"),Is.Zero);
    }

    [Test] public void CapturedSkillTraitMultiplierIsAppliedOnceAfterThePipelineResetsIt()
    {
        f.Set(f.source,"skillTraitDamageMultiplier",1.5f);Assert.That(f.Begin(),Is.True);
        f.Set(f.source,"skillTraitDamageMultiplier",1f);f.Advance(NinjaSpacetimeRules.Windup);
        Assert.That(f.Float(f.enemy,"hp"),Is.EqualTo(4550f).Within(.01f));
    }

    [Test] public void CommittedImpactFinishesHarmlesslyOnTheResultPlaybackClock()
    {
        Assert.That(f.Begin(),Is.True);var effect=f.AttachEffect();f.Advance(NinjaSpacetimeRules.Windup);
        CombatEffectEndPlayback.Begin(effect.gameObject,new HashSet<Type>{typeof(PixelNinjaSpacetime)});
        var playback=effect.GetComponent<CombatEffectEndPlayback>();Assert.That(playback,Is.Not.Null);
        Assert.That(CombatEffectEndPlayback.IsPreserving(effect),Is.True);
        f.Field("battleEnding",true);f.Call("CleanupRank3Unit",f.source);f.Call("ResetFamilyTranscendenceStates");
        Assert.That(f.Active,Is.False);Assert.That(effect==null,Is.False);
        Time.timeScale=0f;
        typeof(CombatEffectEndPlayback).GetMethod("Advance",NinjaSpacetimeFixture.Private).Invoke(playback,new object[]{.2f});
        Assert.That(effect.Age,Is.EqualTo(1.30f).Within(.001f));Assert.That(f.Float(f.enemy,"hp"),Is.EqualTo(4700f).Within(.01f));
        typeof(CombatEffectEndPlayback).GetMethod("Advance",NinjaSpacetimeFixture.Private).Invoke(playback,new object[]{.5f});
        Assert.That(effect==null,Is.True);
    }

    [Test] public void EndBeforeImpactDissipatesTheChargeWithoutInventingAnImpact()
    {
        Assert.That(f.Begin(),Is.True);var effect=f.AttachEffect();f.Advance(.5f);
        CombatEffectEndPlayback.Begin(effect.gameObject,new HashSet<Type>{typeof(PixelNinjaSpacetime)});
        var playback=effect.GetComponent<CombatEffectEndPlayback>();f.Call("CancelNinjaSpacetime",f.source);
        Assert.That(effect.RemainingEndTime,Is.EqualTo(.24f).Within(.001f));
        typeof(CombatEffectEndPlayback).GetMethod("Advance",NinjaSpacetimeFixture.Private).Invoke(playback,new object[]{.25f});
        Assert.That(effect==null,Is.True);Assert.That(f.Float(f.enemy,"hp"),Is.EqualTo(5000f));
    }

    [Test] public void NextWaveResetClearsReleasedResidualGeometry()
    {
        Assert.That(f.Begin(),Is.True);var effect=f.AttachEffect();f.Advance(NinjaSpacetimeRules.Duration);
        Assert.That(f.Active,Is.False);Assert.That(effect==null,Is.False);
        f.Call("ResetNinjaSpacetime");Assert.That(effect==null,Is.True);
    }

    [Test] public void FilmRecoveryKeepsAdvancingThroughItsLastQuarterSecond()
    {
        Assert.That(f.Begin(),Is.True);Sprite original=f.Renderer(f.source).sprite;var effect=f.AttachEffect();
        var stage=new NinjaSubstitutionRank4Stage(f.battle.transform);
        var clock=new CombatSkillClock(f.Model(f.source).transform);
        try
        {
            Assert.That(stage.Begin(new Rank4StageContext{family="ninja_cat",
                source=new FrogCatchCinematicStage.Actor{species="chundongi",sprite=original,color=Color.white,size=1f},
                returnBodyVisible=false}),Is.True);
            clock.Hold();Time.timeScale=0f;
            stage.Tick(NinjaSubstitutionRank4Stage.FilmImpactTime,0f);f.Advance(stage.ActionTime);
            Assert.That(f.Int(f.source,"skillHits"),Is.EqualTo(1));
            float previous=effect.Age;
            foreach(float filmTime in new[]{4.00f,4.10f,4.20f})
            {
                stage.Tick(filmTime,.10f);f.Advance(stage.ActionTime);
                Assert.That(f.Active,Is.True,"The field body must not be released while the film still holds its clock.");
                Assert.That(effect.Age,Is.GreaterThan(previous));previous=effect.Age;
                Assert.That(f.Renderer(f.source).sprite.texture,
                    Is.SameAs(Resources.Load<Texture2D>("CombatCinematics/NinjaSpacetimeV1/Motion")));
            }
            Assert.That(stage.Duration,Is.EqualTo(4.25f));stage.Tick(stage.Duration,.05f);f.Advance(stage.ActionTime);
            Assert.That(f.Active,Is.False);Assert.That(f.Renderer(f.source).sprite,Is.SameAs(original));
            Assert.That(effect.Age,Is.EqualTo(NinjaSpacetimeRules.Duration).Within(.001f));
            clock.End();Time.timeScale=1f;
            effect.AdvanceEnd(.10f);Assert.That(effect.Age,Is.GreaterThan(NinjaSpacetimeRules.Duration));
            Assert.That(f.Int(f.source,"skillHits"),Is.EqualTo(1));
        }
        finally{clock.End();stage.Dispose();}
    }

    [TestCase(false)] [TestCase(true)]
    public void ForegroundMirrorCopiesTheAuthoredPoseAfterTheOrdinaryDriver(bool flipped)
    {
        int Order(Type type)=>((DefaultExecutionOrder)Attribute.GetCustomAttribute(type,typeof(DefaultExecutionOrder))).order;
        Assert.That(Order(typeof(PixelNinjaSpacetime)),Is.GreaterThan(Order(typeof(SpriteMonsterAnimationDriver))));
        Assert.That(Order(typeof(PixelNinjaSpacetime)),Is.LessThan(Order(typeof(SkillFocusSpriteMirror))));
        foreach(string id in new[]{"037_chundongi","038_seolmyo","039_sundongi","040_hongryeon"})
        {
            var prefab=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/"+id));
            prefab.transform.SetParent(f.Model(f.source).transform,false);
            var driver=prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>();
            var source=driver.GetComponent<SpriteRenderer>();source.flipX=flipped;
            float time=0f;driver.PlaySkillAttackWithHold(0,NinjaSpacetimeRules.Windup,1f,null,()=>time);
            var effect=PixelNinjaSpacetime.Create(new Bounds(Vector3.zero,Vector3.one*20f),source,null,id.Substring(4));
            var mirrorHost=new GameObject("Ninja pose ordering mirror");
            var mirror=mirrorHost.AddComponent<SkillFocusSpriteMirror>();mirror.Follow(source,0f);
            var ordered=new MonoBehaviour[]{driver,effect,mirror};Array.Sort(ordered,(a,b)=>Order(a.GetType()).CompareTo(Order(b.GetType())));
            foreach(float poseTime in new[]{.4f,1.10f,1.35f})
            {
                time=poseTime;effect.Pose(time);
                foreach(var component in ordered)component.GetType().GetMethod("LateUpdate",NinjaSpacetimeFixture.Private).Invoke(component,null);
                var foreground=mirror.GetComponent<SpriteRenderer>();
                Assert.That(foreground.sprite,Is.SameAs(source.sprite));Assert.That(foreground.sprite.texture,
                    Is.SameAs(Resources.Load<Texture2D>("CombatCinematics/NinjaSpacetimeV1/Motion")));
                Assert.That(Vector3.Distance(foreground.transform.position,source.transform.position),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(foreground.transform.lossyScale,source.transform.lossyScale),Is.LessThan(.0001f));
                Assert.That(foreground.flipX,Is.EqualTo(flipped));
            }
            source.forceRenderingOff=true;
            foreach(var component in ordered)component.GetType().GetMethod("LateUpdate",NinjaSpacetimeFixture.Private).Invoke(component,null);
            Assert.That(source.forceRenderingOff,Is.True);Assert.That(mirror.GetComponent<SpriteRenderer>().enabled,Is.False);
            effect.Clear();Object.DestroyImmediate(mirrorHost);Object.DestroyImmediate(prefab);
        }
    }

    [TestCase("chundongi")] [TestCase("seolmyo")] [TestCase("sundongi")] [TestCase("hongryeon")]
    public void AuthoredFieldPoseKeepsOneScaleAndFeetThenRestoresTheActualBody(string species)
    {
        var renderer=f.Renderer(f.source);renderer.transform.localScale=new Vector3(1.7f,1.7f,1f);renderer.transform.localPosition=new Vector3(2f,.2f,-1f);
        Sprite original=renderer.sprite;Vector3 scale=renderer.transform.localScale,position=renderer.transform.localPosition;
        Vector3 feet=renderer.transform.TransformPoint(new Vector3(0f,-.125f,0f));
        var effect=PixelNinjaSpacetime.Create(new Bounds(Vector3.zero,Vector3.one*20f),renderer,new[]{f.Renderer(f.enemy).bounds},species);
        Vector3 commonScale=renderer.transform.localScale;
        foreach(float time in new[]{.15f,.4f,.7f,.9f,1.02f,1.10f,1.35f})
        {
            effect.Pose(time);Assert.That(renderer.sprite.texture,Is.SameAs(Resources.Load<Texture2D>("CombatCinematics/NinjaSpacetimeV1/Motion")));
            Assert.That(renderer.transform.position,Is.EqualTo(feet));Assert.That(renderer.transform.localScale,Is.EqualTo(commonScale));
        }
        effect.Impact(new[]{f.Renderer(f.enemy).bounds});effect.Release();
        Assert.That(renderer.sprite,Is.SameAs(original));Assert.That(renderer.transform.localScale,Is.EqualTo(scale));
        Assert.That(renderer.transform.localPosition,Is.EqualTo(position));Assert.That(renderer.forceRenderingOff,Is.False);
        Assert.That(effect.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));
        Assert.That(effect.GetComponentsInChildren<LineRenderer>(),Is.Empty);
        bool authoredRift=false;foreach(var part in effect.GetComponentsInChildren<SpriteRenderer>())
            if(part.sprite!=null&&part.sprite.texture==Resources.Load<Texture2D>("CombatCinematics/NinjaSpacetimeV1/Rift"))authoredRift=true;
        Assert.That(authoredRift,Is.True);effect.Clear();
    }
}
