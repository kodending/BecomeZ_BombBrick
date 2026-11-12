using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class CombatReadabilityProductionTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly Dictionary<string,int> Styles=new Dictionary<string,int>
    {
        {"sabertooth",0},{"ninja_cat",0},{"mantis",0},{"griffin",0},
        {"bear",1},{"kingkong",1},{"wizard_mouse",2},{"spirit_deer",2},{"wyvern",2},
        {"raptor",3},{"wolf",3},{"tyranno",3},{"haetae",3},
        {"crocodile",4},{"mammoth",4},{"beetle",4},{"monkey",5},
        {"frog",6},{"arachne",6},{"mushroom",6}
    };
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object source,target;
    private SpriteMonsterAnimationDriver driver;
    private GameObject model;
    private Camera camera;
    private int users,seed;
    private bool test,enabled,basicOnly,contact,audio;
    private CombatReadabilityPreset preset;
    private SpriteRenderer recipient;
    private Vector3 incoming;
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()
    {
        users=CombatReadabilityPresentation.BattleUsers;seed=CombatReadabilityPreview.Seed;
        test=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;
        basicOnly=CombatReadabilityPreview.BasicOnly;contact=CombatReadabilityPreview.ContactFeedback;
        audio=CombatReadabilityPreview.SkillAudio;preset=CombatReadabilityPreview.Preset;
        recipient=CombatReadabilityPreview.ProjectileContactRenderer;incoming=CombatReadabilityPreview.ProjectileContactDirection;
        random=UnityEngine.Random.state;
        while(CombatReadabilityPresentation.BattleUsers>0)CombatReadabilityPresentation.UnregisterBattle();
        CombatEffectTestConfiguration.Active=false;CombatReadabilityPreview.Enabled=false;
        CombatReadabilityPreview.ProjectileContactRenderer=null;CombatReadabilityPreview.ProjectileContactDirection=Vector3.zero;
        fixture=new MonkeyArcTests();fixture.SetUp();battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        source=Get<object>(fixture,"source");target=Get<object>(fixture,"target");
        camera=new GameObject("Production presentation test camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(0f,8f,-12f);camera.transform.LookAt(Vector3.zero);Set(battle,"mainCamera",camera);
        CombatReadabilityPresentation.RegisterBattle();
    }

    [TearDown] public void Cleanup()
    {
        try{fixture?.TearDown();}
        finally
        {
            while(CombatReadabilityPresentation.BattleUsers>0)CombatReadabilityPresentation.UnregisterBattle();
            for(int i=0;i<users;i++)CombatReadabilityPresentation.RegisterBattle();
            CombatEffectTestConfiguration.Active=test;CombatReadabilityPreview.Enabled=enabled;
            CombatReadabilityPreview.BasicOnly=basicOnly;CombatReadabilityPreview.ContactFeedback=contact;
            CombatReadabilityPreview.SkillAudio=audio;CombatReadabilityPreview.Preset=preset;CombatReadabilityPreview.Seed=seed;
            CombatReadabilityPreview.ProjectileContactRenderer=recipient;CombatReadabilityPreview.ProjectileContactDirection=incoming;
            UnityEngine.Random.state=random;
        }
    }

    [Test] public void BalancedRuntimeRegistrationActivatesOnlyLiveBattlesAndNeverUnderflows()
    {
        Assert.That(CombatReadabilityPresentation.Active,Is.True);Assert.That(CombatReadabilityPresentation.Local,Is.True);
        CombatReadabilityPresentation.UnregisterBattle();Assert.That(CombatReadabilityPresentation.Active,Is.False);
        CombatReadabilityPresentation.UnregisterBattle();Assert.That(CombatReadabilityPresentation.BattleUsers,Is.Zero);
        CombatReadabilityPresentation.RegisterBattle();CombatReadabilityPresentation.RegisterBattle();
        CombatReadabilityPresentation.UnregisterBattle();Assert.That(CombatReadabilityPresentation.BattleUsers,Is.EqualTo(1));
        Assert.That(CombatReadabilityPresentation.Local,Is.True);
    }

    [TestCase(CombatReadabilityPreset.Current,true,false,false)]
    [TestCase(CombatReadabilityPreset.Compact,true,true,false)]
    [TestCase(CombatReadabilityPreset.Local,true,true,true)]
    [TestCase(CombatReadabilityPreset.Local,false,false,false)]
    public void OfflineRegressionFixturesCanStillExercisePreviousPresentationPaths(CombatReadabilityPreset mode,bool selected,bool adjusted,bool local)
    {
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Enabled=selected;CombatReadabilityPreview.Preset=mode;
        Assert.That(CombatReadabilityPresentation.Active,Is.EqualTo(selected));
        Assert.That(CombatReadabilityPresentation.Adjusted,Is.EqualTo(adjusted));
        Assert.That(CombatReadabilityPresentation.Local,Is.EqualTo(local));
        Assert.That(CombatReadabilityPresentation.BodyVisualScale,Is.EqualTo(local?.9f:1f));
    }

    [Test] public void ProductionUsesTheApprovedBodyEffectHudAndGrowthIndependentRingSizes()
    {
        Assert.That(CombatReadabilityPresentation.BodyVisualScale,Is.EqualTo(.9f));
        Assert.That(CombatReadabilityPresentation.EffectVisualScale,Is.EqualTo(1.215f).Within(.00001f));
        var body=new Rect(10f,20f,50f,60f);var pivot=new Vector3(body.center.x,body.yMin-6f,0f);
        Matrix4x4 hud=CombatReadabilityPresentation.WorldHudMatrix(Matrix4x4.identity,body);
        Assert.That(hud.MultiplyVector(Vector3.right).magnitude,Is.EqualTo(.675f).Within(.00001f));
        Assert.That(Vector3.Distance(hud.MultiplyPoint3x4(pivot),pivot),Is.LessThan(.00001f));
        var ring=Get<GameObject>(source,"ring").AddComponent<CombatTargetRing>();ring.Configure(.65f,Color.cyan,null);
        foreach(float growth in new[]{.5f,1f,1.7f})
        {
            ring.transform.localScale=Vector3.one*growth;ring.SetRadius(.65f);
            Assert.That(Get<Transform>(ring,"normalizer").TransformVector(Vector3.right).magnitude,
                Is.EqualTo(.65f*.72f).Within(.00001f));
            Assert.That(Get<float>(ring,"radius"),Is.EqualTo(.65f));
        }
        var clock=new CombatSkillClock(Get<GameObject>(source,"model").transform);
        try{clock.Hold();Assert.That(CombatReadabilityPresentation.Local,Is.True);
            Assert.That(CombatReadabilityPresentation.RefinedSkills,Is.False);
            Assert.That(CombatReadabilityPresentation.BodyVisualScale,Is.EqualTo(.9f));}
        finally{clock.End();}
    }

    [Test] public void ProductionIgnoresDebugAudioAndBasicOnlySwitchesWithoutChangingRunIdentityOrSeed()
    {
        CombatReadabilityPreview.Enabled=true;CombatReadabilityPreview.BasicOnly=true;
        CombatReadabilityPreview.ContactFeedback=CombatReadabilityPreview.SkillAudio=false;CombatReadabilityPreview.Seed=982711;
        var run=new ExpeditionRunSave{runId="production_run_untouched",activeBattleSeed=917};Set(battle,"expeditionRun",run);
        string before=JsonUtility.ToJson(UnityEngine.Random.state);Call(battle,"ConfigureReadabilityRun");
        Assert.That(run.runId,Is.EqualTo("production_run_untouched"));Assert.That(run.activeBattleSeed,Is.EqualTo(917));
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(before));
        Assert.That(CombatReadabilityPresentation.ContactFeedback,Is.True);Assert.That(CombatReadabilityPresentation.SkillAudio,Is.True);
        Assert.That(CombatReadabilityPreview.BasicOnly,Is.True);Assert.That(CombatReadabilityPreview.Seed,Is.EqualTo(982711));
    }

    private static IEnumerable<TestCaseData> Species()
    {
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        foreach(var definition in database.monsters.Where(x=>x!=null).OrderBy(x=>x.monsterId))
            yield return new TestCaseData(definition.monsterId).SetName("ProductionPresentation_"+definition.monsterId);
    }

    [Test] public void ProductionRosterCoversAllTwentyFamiliesAndTheirFourVariants()
    {
        var definitions=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").monsters;
        Assert.That(definitions.Count,Is.EqualTo(80));Assert.That(definitions.Select(x=>x.monsterId).Distinct().Count(),Is.EqualTo(80));
        CollectionAssert.AreEquivalent(Styles.Keys,definitions.Select(x=>x.familyId).Distinct());
        foreach(var family in definitions.GroupBy(x=>x.familyId))Assert.That(family.Count(),Is.EqualTo(4),family.Key);
    }

    [TestCaseSource(nameof(Species))]
    public void EverySpeciesKeepsLogicalCombatGeometryRandomAndItsOwnAttackBeat(string species)
    {
        CombatEffectTestConfiguration.Active=true;CombatReadabilityPreview.Enabled=true;CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get(species);Source(definition);
        Transform root=model.transform;Vector3 position=root.position,scale=root.localScale,authored=driver.transform.localScale;
        float reach=(float)Call(battle,"AttackDistance",source,target),radius=Get<float>(source,"collisionRadius");
        int range=Get<int>(source,"attackRange");float interval=Get<float>(source,"attackInterval"),spacing=Get<float>(source,"combatBodyScale");
        Call(battle,"CacheMeleeReadabilityBody",source);Rect footprint=Get<Rect>(source,"meleeBodyShape");
        string before=JsonUtility.ToJson(UnityEngine.Random.state);
        CombatEffectTestConfiguration.Active=false;Advance(0f);Call(battle,"CacheMeleeReadabilityBody",source);
        Assert.That(Vector3.Distance(driver.transform.localScale,authored*.9f),Is.LessThan(.00001f));
        Assert.That((int)Call(battle,"ReadabilityBasicAttackStyle",source),Is.EqualTo(Styles[definition.familyId]));
        int hits=0;driver.PlayBasicAttack(1f,()=>hits++);
        Call(battle,"TrackReadabilityMeleeAttack",source,target);Advance(driver.BasicAttackHitTime+.001f);
        Assert.That(hits,Is.EqualTo(1));Assert.That(Get<bool>(driver,"readabilityBasicPoseContactShown"),Is.True);
        Assert.That(root.position,Is.EqualTo(position));Assert.That(root.localScale,Is.EqualTo(scale));
        Assert.That(Call(battle,"AttackDistance",source,target),Is.EqualTo(reach));Assert.That(Get<float>(source,"collisionRadius"),Is.EqualTo(radius));
        Assert.That(Get<int>(source,"attackRange"),Is.EqualTo(range));Assert.That(Get<float>(source,"attackInterval"),Is.EqualTo(interval));
        Assert.That(Get<float>(source,"combatBodyScale"),Is.EqualTo(spacing));Assert.That(Get<Rect>(source,"meleeBodyShape"),Is.EqualTo(footprint));
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(before));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
    public void EveryAttackStyleContainsAtLeastFourDifferentPixelShapes(int style)
    {
        Source(Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos"));
        var targetModel=Get<GameObject>(target,"model");
        var body=new GameObject("Pixel frame test target");body.transform.SetParent(targetModel.transform,false);
        var renderer=body.AddComponent<SpriteRenderer>();
        renderer.sprite=driver.GetComponent<SpriteRenderer>().sprite;
        driver.PlayBasicAttack(1f,()=>{});
        var effect=ReadabilityAttackOriginEffect.TrackAttack(driver,renderer,style,()=>true);
        Assert.That(effect,Is.Not.Null);
        var frames=Get<Sprite[]>(effect,"frames");Assert.That(frames.Length,Is.EqualTo(6));
        var hash=typeof(ReadabilityAttackOriginTests).GetMethod("PixelHash",Static);
        Assert.That(frames.Select(frame=>(int)hash.Invoke(null,new object[]{frame.texture})).Distinct().Count(),
            Is.GreaterThanOrEqualTo(4),"Each style must animate its pixels rather than transform one picture.");
    }

    [TestCase(false)] [TestCase(true)]
    public void ActualProductionDirectAndGroundHitsKeepTheirDistinctFeedbackWithoutChangingDamageCameraOrTime(bool ground)
    {
        Source(Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos"));
        Set(target,"model",model);Set(target,"definition",Get<MonsterDefinition>(source,"definition"));Set(target,"spriteAnimation",driver);
        var reaction=model.GetComponent<VoxelEnemyReaction>()??model.AddComponent<VoxelEnemyReaction>();reaction.Initialize();Set(target,"reaction",reaction);
        Set(target,"hp",5000f);Set(target,"maxHp",5000f);Set(target,"alive",true);
        Vector3 position=model.transform.position,scale=model.transform.localScale,cameraPosition=camera.transform.position;
        Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView,time=Time.timeScale,spacing=Get<float>(target,"combatBodyScale");
        float hp=Get<float>(target,"hp");
        float applied=ground?(float)Call(battle,"ApplyGroundSkillDamage",source,target,100f,false):
            (float)typeof(MonsterAutoBattlePrototype).GetMethods(Fields).Single(x=>x.Name=="ApplySkillDamage"&&x.GetParameters().Length==4&&
                x.GetParameters()[2].ParameterType==typeof(float)).Invoke(battle,new object[]{source,target,100f,false});
        Assert.That(applied,Is.GreaterThan(0f));Assert.That(hp-Get<float>(target,"hp"),Is.EqualTo(applied).Within(.001f));
        Assert.That(Get<float>(reaction,"flashPeak"),Is.EqualTo(ground?.55f*.55f:.68f).Within(.00001f));
        Assert.That(Get<float>(driver,"impactHoldRemaining"),Is.EqualTo(ground?0f:.03f));
        Assert.That(Get<bool>(battle,"readabilityGroundImpact"),Is.False);
        Assert.That(model.transform.position,Is.EqualTo(position));Assert.That(model.transform.localScale,Is.EqualTo(scale));
        Assert.That(Get<float>(target,"combatBodyScale"),Is.EqualTo(spacing));Assert.That(camera.transform.position,Is.EqualTo(cameraPosition));
        Assert.That(camera.transform.rotation,Is.EqualTo(rotation));Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(Time.timeScale,Is.EqualTo(time));
    }

    [Test] public void ProductionDeathUsesVolumetricDustAndCompletesItsBurstWithoutMovingTheActor()
    {
        Source(Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos"));
        var type=typeof(VoxelEnemyReaction).GetNestedType("SpriteDeathFragments",BindingFlags.Public|BindingFlags.NonPublic);
        var previous=Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(x=>x!=null&&x.GetType()==type).ToArray();
        int bursts=0;Vector3 position=model.transform.position,scale=model.transform.localScale;
        Assert.That((bool)type.GetMethod("Create",Static).Invoke(null,new object[]{driver.GetComponent<SpriteRenderer>(),Vector3.right,(Action)(()=>bursts++)}),Is.True);
        var effect=Resources.FindObjectsOfTypeAll<MonoBehaviour>().Single(x=>x!=null&&x.GetType()==type&&!previous.Contains(x));
        Assert.That(Get<bool>(effect,"popProfile"),Is.True);var particles=Get<ParticleSystem>(effect,"pixelParticles");
        Assert.That(particles.GetComponent<ParticleSystemRenderer>().renderMode,Is.EqualTo(ParticleSystemRenderMode.Mesh));
        Assert.That(particles.main.startRotation3D,Is.True);Assert.That(particles.main.maxParticles,Is.LessThanOrEqualTo(1264));
        Call(effect,"Advance",.051f);Assert.That(bursts,Is.EqualTo(1));Call(effect,"Advance",1.6f);Assert.That(bursts,Is.EqualTo(1));
        Assert.That(effect==null||!effect.gameObject.activeSelf,Is.True);
        Assert.That(model.transform.position,Is.EqualTo(position));Assert.That(model.transform.localScale,Is.EqualTo(scale));
    }

    private void Source(MonsterDefinition definition)
    {
        model=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));model.transform.position=Vector3.zero;
        driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(source,"model",model);Set(source,"definition",definition);Set(source,"spriteAnimation",driver);Set(source,"skillId",definition.skillId);
        Set(source,"attackRange",definition.attackRange);Set(source,"attackInterval",definition.attackInterval);Set(source,"collisionRadius",definition.collisionRadius);
        Advance(0f);
    }
    private void Advance(float dt){Call(driver,"ClearAppliedRecoil");Call(driver,"Advance",dt);Call(driver,"LateUpdate");}
    private static T Get<T>(object value,string field)=>(T)value.GetType().GetField(field,Fields).GetValue(value);
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,Fields).SetValue(value,data);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Fields).Invoke(value,args);
}
