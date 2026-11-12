using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityDeathPopTests
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static Type FragmentType=>typeof(VoxelEnemyReaction).GetNestedType("SpriteDeathFragments",BindingFlags.Public|BindingFlags.NonPublic);
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object victim;
    private GameObject sourceRoot;
    private SpriteRenderer source;
    private SpriteMonsterAnimationDriver driver;
    private VoxelEnemyReaction reaction;
    private bool active,enabled;
    private CombatReadabilityPreset preset;
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()
    {
        active=CombatEffectTestConfiguration.Active;enabled=CombatReadabilityPreview.Enabled;
        preset=CombatReadabilityPreview.Preset;random=UnityEngine.Random.state;
        CombatEffectTestConfiguration.Active=CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        fixture=new MonkeyArcTests();fixture.SetUp();battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        victim=Get<object>(fixture,"target");Source("velos");
    }

    [TearDown] public void Cleanup()
    {
        try{fixture?.TearDown();}
        finally
        {
            CombatEffectTestConfiguration.Active=active;CombatReadabilityPreview.Enabled=enabled;
            CombatReadabilityPreview.Preset=preset;UnityEngine.Random.state=random;
        }
    }

    [TestCase("velos")] [TestCase("geomchi")]
    public void OrdinaryDeathShowsItsHeldWhiteAccentThenReleasesPixelsAtTheSameDeadline(string species)
    {
        Source(species);int bursts=0;Sprite held=source.sprite;
        reaction.PlayDeath(Vector3.right,()=>bursts++);var fx=Fragments().Single();
        Assert.That(Get<bool>(fx,"popProfile"),Is.True);Assert.That(source.enabled,Is.True);
        Assert.That(source.sprite,Is.SameAs(held));Assert.That(Read(fx).Length,Is.GreaterThan(16));
        Advance(fx,.025f);var white=Read(fx);
        float originalBrightness=Get<IList>(fx,"phasePixels").Cast<object>().Average(p=>
        {var c=Get<Color32>(p,"color");return (float)c.r+c.g+c.b;});
        Assert.That(white.Average(Brightness),Is.GreaterThan(originalBrightness+15f));
        Assert.That(source.enabled,Is.True);Assert.That(bursts,Is.Zero);
        Advance(fx,.024f);Assert.That(source.enabled,Is.True);Assert.That(bursts,Is.Zero);
        Advance(fx,.002f);Assert.That(source.enabled,Is.False);Assert.That(bursts,Is.EqualTo(1));
        Assert.That(Read(fx).Length,Is.GreaterThan(16));
    }

    [Test] public void LongerDustKeepsTheParticleBudgetRandomStateSourcePoseAndOriginalBurstDelay()
    {
        Vector3 position=sourceRoot.transform.position,scale=sourceRoot.transform.localScale;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;UnityEngine.Random.InitState(8246);
        var original=Create(()=>{});string state=JsonUtility.ToJson(UnityEngine.Random.state);
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;UnityEngine.Random.InitState(8246);
        var preview=Create(()=>{});Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(state));
        float span=Get<float>(preview,"visibleBodySpan"),width=Get<float>(preview,"visibleBodyWidth");
        Bounds visible=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",Static).Invoke(null,new object[]{source.sprite});
        float actualWidth=source.transform.TransformVector(Vector3.right*visible.size.x).magnitude;
        float actualHeight=source.transform.TransformVector(Vector3.up*visible.size.y).magnitude;
        Assert.That(width,Is.EqualTo(actualWidth).Within(.00001f));
        Assert.That(span,Is.EqualTo(Mathf.Max(actualWidth,actualHeight)).Within(.00001f));
        IList before=Get<IList>(original,"phasePixels"),after=Get<IList>(preview,"phasePixels");
        Assert.That(after.Count,Is.EqualTo(before.Count));Assert.That(after.Count,Is.InRange(1,1200));
        for(int i=0;i<before.Count;i++)
        {
            Assert.That(Get<float>(after[i],"duration"),Is.EqualTo(Get<float>(before[i],"duration")*10f).Within(.00001f));
            Assert.That(Get<Vector3>(after[i],"scatter").magnitude/span,Is.InRange(1.9999f,3.0001f));
            Assert.That(Get<Vector3>(after[i],"origin"),Is.EqualTo(Get<Vector3>(before[i],"origin")));
            Assert.That(Get<float>(after[i],"delay"),Is.EqualTo(.05f));
        }
        IList oldFlying=Get<IList>(original,"flyingPixels"),newFlying=Get<IList>(preview,"flyingPixels");
        Assert.That(newFlying.Count,Is.EqualTo(oldFlying.Count));Assert.That(newFlying.Count,Is.InRange(1,64));
        for(int i=0;i<oldFlying.Count;i++)
        {
            Assert.That(Get<float>(newFlying[i],"life"),Is.EqualTo(Get<float>(oldFlying[i],"life")*4f).Within(.00001f));
            Assert.That(Get<float>(newFlying[i],"maxLife"),Is.EqualTo(Get<float>(oldFlying[i],"maxLife")*4f).Within(.00001f));
            Assert.That(Get<float>(newFlying[i],"size"),Is.EqualTo(Get<float>(oldFlying[i],"size")*1.5f).Within(.00001f));
            Assert.That(Get<Vector3>(newFlying[i],"velocity").magnitude/width,Is.InRange(5.9999f,9.0001f));
        }
        Assert.That(sourceRoot.transform.position,Is.EqualTo(position));Assert.That(sourceRoot.transform.localScale,Is.EqualTo(scale));
    }

    [Test] public void BrighterLargerPixelsSpreadOutwardThenFadeWithoutLeavingASecondBurst()
    {
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;UnityEngine.Random.InitState(73);
        var original=Create(()=>{});CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;UnityEngine.Random.InitState(73);
        int bursts=0;var preview=Create(()=>bursts++);Advance(original,.055f);Advance(preview,.055f);
        var before=Read(original);var peak=Read(preview);int phaseCount=Get<IList>(preview,"phasePixels").Count;
        Assert.That(peak.Length,Is.EqualTo(before.Length));
        Assert.That(peak.Take(phaseCount).Average(p=>p.startSize),Is.GreaterThan(before.Take(phaseCount).Average(p=>p.startSize)*1.6f));
        Assert.That(peak.Take(phaseCount).Average(Brightness),Is.GreaterThan(before.Take(phaseCount).Average(Brightness)+20f));
        Vector3 center=Get<Vector3>(preview,"burstCenter");float near=peak.Take(phaseCount).Average(p=>Vector3.Distance(center,p.position));
        Advance(preview,.045f);var spread=Read(preview);
        Assert.That(spread.Take(phaseCount).Average(p=>Vector3.Distance(center,p.position)),Is.GreaterThan(near));
        Advance(preview,.8f);var faded=Read(preview);
        Assert.That(faded.Average(p=>(float)p.startColor.a),Is.LessThan(peak.Average(p=>(float)p.startColor.a)));
        Advance(preview,.65f);Assert.That(preview==null||!preview.gameObject.activeSelf,Is.True);Assert.That(bursts,Is.EqualTo(1));
    }

    [Test] public void DustKeepsTravelingFarInDepthWhileEachEqualTimeStepCoversLessDistance()
    {
        UnityEngine.Random.InitState(876);var fx=Create(()=>{});int count=Get<IList>(fx,"phasePixels").Count;
        float span=Get<float>(fx,"visibleBodySpan");Vector3 center=Get<Vector3>(fx,"burstCenter");
        var depth=Get<IList>(fx,"phasePixels").Cast<object>()
            .Select(p=>Vector3.Dot(Get<Vector3>(p,"scatter"),source.transform.forward)).ToArray();
        Assert.That(depth.Min(),Is.LessThan(-span*.1f));Assert.That(depth.Max(),Is.GreaterThan(span*.1f));
        Assert.That(depth.Max()-depth.Min(),Is.GreaterThan(span*.4f));
        Advance(fx,.05f);var previous=Read(fx).Take(count).ToArray();var increments=new float[3];
        float firstExtent=0f,lastExtent=0f;
        for(int sample=0;sample<increments.Length;sample++)
        {
            Advance(fx,.1f);var current=Read(fx).Take(count).ToArray();Assert.That(current.Length,Is.EqualTo(count));
            increments[sample]=current.Select((p,index)=>Vector3.Distance(p.position,previous[index].position)).Average();
            lastExtent=current.Average(p=>Vector3.Distance(center,p.position));
            if(sample==0)firstExtent=lastExtent;previous=current;
        }
        Assert.That(increments[0],Is.GreaterThan(increments[1]));Assert.That(increments[1],Is.GreaterThan(increments[2]));
        Assert.That(increments[2],Is.GreaterThan(.001f));Assert.That(lastExtent,Is.GreaterThan(firstExtent));
        Advance(fx,.45f);
        Assert.That(Read(fx).Take(count).Average(p=>Vector3.Distance(center,p.position)),Is.GreaterThan(span*1.8f));
    }

    [Test] public void DustRetainsReadableSizeAndOpacityAtHalfAndEightTenthsOfASecondThenExpires()
    {
        var fx=Create(()=>{});int count=Get<IList>(fx,"phasePixels").Count;
        float cell=Get<IList>(fx,"phasePixels").Cast<object>().Average(p=>Get<float>(p,"size"));
        Advance(fx,.5f);var half=Read(fx).Take(count).ToArray();Assert.That(half.Length,Is.EqualTo(count));
        Assert.That(half.Average(p=>p.startSize),Is.GreaterThan(cell*.85f));
        Assert.That(half.Average(p=>(float)p.startColor.a),Is.GreaterThan(120f));
        Advance(fx,.3f);var late=Read(fx).Take(count).ToArray();Assert.That(late.Length,Is.EqualTo(count));
        Assert.That(late.Average(p=>p.startSize),Is.GreaterThan(cell*.7f));
        Assert.That(late.Average(p=>(float)p.startColor.a),Is.GreaterThan(45f));
        Advance(fx,.7f);Assert.That(fx==null||!fx.gameObject.activeSelf,Is.True);
    }

    [Test] public void FlyingDustUsesTheSameTrajectoryForOneLargeStepAnd120SmallStepsIncludingExpiredPieces()
    {
        int oneBurst=0,manyBursts=0;UnityEngine.Random.InitState(19);var one=Create(()=>oneBurst++);
        UnityEngine.Random.InitState(19);var many=Create(()=>manyBursts++);
        IList large=Get<IList>(one,"flyingPixels"),small=Get<IList>(many,"flyingPixels");
        Assert.That(large.Count,Is.GreaterThan(0));Assert.That(large.Count,Is.EqualTo(small.Count));
        Advance(one,.95f);for(int step=0;step<120;step++)Advance(many,.95f/120f);
        Assert.That(large.Cast<object>().Any(p=>Get<float>(p,"life")<=0f),Is.True);
        for(int i=0;i<large.Count;i++)
        {
            Assert.That(Vector3.Distance(Get<Vector3>(large[i],"position"),Get<Vector3>(small[i],"position")),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(Get<Vector3>(large[i],"velocity"),Get<Vector3>(small[i],"velocity")),Is.LessThan(.001f));
        }
        Assert.That(oneBurst,Is.EqualTo(1));Assert.That(manyBursts,Is.EqualTo(1));
    }

    [Test] public void VolumeDustSharesACachedShadedCubeAndRotatesInThreeDimensions()
    {
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;var legacy=Create(()=>{});
        Assert.That(Get<ParticleSystem>(legacy,"pixelParticles").GetComponent<ParticleSystemRenderer>().renderMode,Is.EqualTo(ParticleSystemRenderMode.Billboard));
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;var first=Create(()=>{});var second=Create(()=>{});
        var particles=Get<ParticleSystem>(first,"pixelParticles");var renderer=particles.GetComponent<ParticleSystemRenderer>();
        Assert.That(renderer.renderMode,Is.EqualTo(ParticleSystemRenderMode.Mesh));Assert.That(renderer.mesh,Is.Not.Null);
        Assert.That(UnityEditor.ShaderUtil.IsPassCompiled(renderer.sharedMaterial,0),Is.True,
            "The first burst must not display Unity's temporary shader-compilation colour.");
        Assert.That(Get<ParticleSystem>(second,"pixelParticles").GetComponent<ParticleSystemRenderer>().mesh,Is.EqualTo(renderer.mesh));
        Assert.That(renderer.mesh.vertexCount,Is.EqualTo(24));Assert.That(renderer.mesh.triangles.Length,Is.EqualTo(36));
        Assert.That(renderer.mesh.colors32.Distinct().Count(),Is.GreaterThanOrEqualTo(3));
        Assert.That(renderer.mesh.bounds.size.x,Is.GreaterThan(0f));Assert.That(renderer.mesh.bounds.size.y,Is.GreaterThan(0f));
        Assert.That(renderer.mesh.bounds.size.z,Is.GreaterThan(0f));Assert.That(particles.main.startRotation3D,Is.True);
        Advance(first,.1f);Assert.That(Read(first).Any(p=>Mathf.Abs(p.rotation3D.x)>.01f&&Mathf.Abs(p.rotation3D.y)>.01f),Is.True);
    }

    [TestCase(.05f)] [TestCase(.15f)]
    public void CrossingTheOriginalBurstDeadlineInvokesItsCallbackExactlyOnceEvenWithALargeStep(float step)
    {
        int bursts=0;var fx=Create(()=>bursts++);Advance(fx,0f);Assert.That(bursts,Is.Zero);
        Advance(fx,step);Assert.That(bursts,Is.EqualTo(1));Advance(fx,.01f);Assert.That(bursts,Is.EqualTo(1));
        Assert.That(source.enabled,Is.False);
    }

    [Test] public void CurrentCompactAndOptedOutScenesKeepTheLegacyProfile()
    {
        var modes=new[]{CombatReadabilityPreset.Current,CombatReadabilityPreset.Compact,
            CombatReadabilityPreset.Local,CombatReadabilityPreset.Local};
        for(int i=0;i<modes.Length;i++)
        {
            CombatReadabilityPreview.Preset=modes[i];CombatReadabilityPreview.Enabled=i!=2;CombatEffectTestConfiguration.Active=i!=3;
            var fx=Create(()=>{});Assert.That(Get<bool>(fx,"popProfile"),Is.False);Assert.That(Read(fx),Is.Empty);
            foreach(object pixel in Get<IList>(fx,"phasePixels"))Assert.That(Get<float>(pixel,"duration"),Is.InRange(.1f,.14f));
        }
    }

    [Test] public void ReturningPixelsAndSuccessfulCaptureKeepTheirExistingPresentation()
    {
        var before=Fragments();Assert.That((bool)FragmentType.GetMethod("CreateReturn",Static).Invoke(null,new object[]{source,.6f}),Is.True);
        var returning=Fragments().Except(before).Single();Assert.That(Get<bool>(returning,"returning"),Is.True);
        Assert.That(Get<bool>(returning,"popProfile"),Is.False);Assert.That(Get<IList>(returning,"flyingPixels"),Is.Empty);
        Assert.That(Get<ParticleSystem>(returning,"pixelParticles").GetComponent<ParticleSystemRenderer>().renderMode,
            Is.EqualTo(ParticleSystemRenderMode.Billboard));
        foreach(object pixel in Get<IList>(returning,"phasePixels"))Assert.That(Get<float>(pixel,"duration"),Is.EqualTo(.6f));
        before=Fragments();reaction.PlayCapturedDeath(Vector3.right);
        CollectionAssert.AreEquivalent(before,Fragments());Assert.That(Get<bool>(reaction,"capturedDeath"),Is.True);
    }

    [Test] public void AReleasedPopRetainsItsCapturedProfileWhenThePreviewModeChanges()
    {
        int bursts=0;var fx=Create(()=>bursts++);
        CombatReadabilityPreview.Enabled=false;CombatEffectTestConfiguration.Active=false;CombatReadabilityPreview.Preset=CombatReadabilityPreset.Current;
        Advance(fx,.025f);Assert.That(Get<bool>(fx,"popProfile"),Is.True);Assert.That(Read(fx).Length,Is.GreaterThan(16));
        Advance(fx,.03f);Assert.That(bursts,Is.EqualTo(1));Assert.That(source.enabled,Is.False);
    }

    [Test] public void SpriteActorCleanupStillUsesItsOriginalDeadlineAndDoesNotOwnTheFragmentRoot()
    {
        reaction.PlayDeath(Vector3.right,()=>{});var fx=Fragments().Single();Assert.That(fx.transform.parent,Is.Null);
        Call(reaction,"UpdateDeath",.64f);Assert.That(sourceRoot.activeSelf,Is.True);Assert.That(reaction.IsDying,Is.True);
        Call(reaction,"UpdateDeath",.02f);Assert.That(sourceRoot.activeSelf,Is.False);Assert.That(reaction.IsDying,Is.False);
        Assert.That(fx!=null&&fx.gameObject.activeSelf,Is.True);
    }

    [Test] public void HeadlessBattleDoesNotWaitForDeathPixelsAndTheNextWaveClearsTheirRoots()
    {
        Set(victim,"model",sourceRoot);Set(victim,"spriteAnimation",driver);Set(victim,"reaction",reaction);
        Set(victim,"definition",Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos"));
        Set(victim,"alive",false);Set(victim,"hp",0f);Set(battle,"mlEpisodeActive",true);
        reaction.PlayDeath(Vector3.right,()=>{});var fx=Fragments().Single();
        Call(battle,"CheckBattleResult");Assert.That(Get<bool>(battle,"battleEnding"),Is.True);
        Assert.That(Get<string>(battle,"pendingResult"),Is.EqualTo("아군 승리"));
        Assert.That(fx!=null&&fx.gameObject.activeSelf,Is.True);Advance(fx,.08f);Assert.That(Read(fx).Length,Is.GreaterThan(16));
        Call(battle,"ClearBattleEffects");Assert.That(Fragments(),Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public void OutcomeMessageWaitsForTheLastDeathDustOnEitherTeam(bool playerDies)
    {
        Set(victim,"model",sourceRoot);Set(victim,"spriteAnimation",driver);Set(victim,"reaction",reaction);
        Set(victim,"alive",false);Set(victim,"hp",0f);Set(battle,"mlEpisodeActive",false);
        if(playerDies)
        {
            var attacker=Get<object>(fixture,"source");
            var team=Get<object>(victim,"team").GetType();
            Set(victim,"team",Enum.ToObject(team,0));Set(attacker,"team",Enum.ToObject(team,1));
        }
        reaction.PlayDeath(Vector3.right,()=>{});var fx=Fragments().Single();
        Call(battle,"CheckBattleResult");
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Death"));
        Assert.That(Get<string>(battle,"pendingResult"),Is.EqualTo(playerDies?"적군 승리":"아군 승리"));
        Call(battle,"UpdateWaveOutcomePresentation",3f);
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Death"));
        Advance(fx,.6f);Call(battle,"UpdateWaveOutcomePresentation",.6f);
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Death"));
        Advance(fx,2f);Call(battle,"UpdateWaveOutcomePresentation",2f);
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Message"));
        Assert.That(Get<float>(battle,"waveOutcomeTimer"),Is.EqualTo(2f));
        Assert.That(Get<string>(battle,"resultText"),Is.Empty);
    }

    [Test] public void ReturnReassemblyDoesNotDelayOutcomeAndClearingDeathVisualsCannotLeaveItStuck()
    {
        Set(victim,"alive",false);Set(battle,"mlEpisodeActive",false);
        FragmentType.GetMethod("CreateReturn",Static).Invoke(null,new object[]{source,.9f});
        Call(battle,"CheckBattleResult");
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Message"));
        Call(battle,"ClearBattleEffects");
        Set(battle,"battleEnding",false);Set(battle,"pendingResult","");
        reaction.PlayDeath(Vector3.right,()=>{});
        Call(battle,"CheckBattleResult");
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Death"));
        Call(battle,"ClearBattleEffects");Call(battle,"UpdateWaveOutcomePresentation",0f);
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Message"));
    }

    [Test] public void FrozenDeathParticlesFinishBeforeTheMessage()
    {
        Set(victim,"alive",false);Set(battle,"mlEpisodeActive",false);
        var particles=PixelCombatEffect.PlayFreezeDeathShatter(source.transform,source);
        Assert.That(particles.IsAlive(true),Is.True);
        Call(battle,"TrackWaveDeathParticles",particles);Call(battle,"CheckBattleResult");
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Death"));
        Call(battle,"UpdateWaveOutcomePresentation",3f);
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Death"));
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        Call(battle,"UpdateWaveOutcomePresentation",0f);
        Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Message"));
    }

    [Test] public void DeathDustInAnotherSceneCannotDelayThisBattle()
    {
        var other=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var copy=Object.Instantiate(sourceRoot);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy,other);
            var sprite=copy.GetComponentInChildren<SpriteRenderer>();
            Assert.That((bool)FragmentType.GetMethod("Create",Static).Invoke(null,
                new object[]{sprite,Vector3.right,null}),Is.True);
            var fx=Fragments().Single();Assert.That(fx.gameObject.scene,Is.EqualTo(other));
            Set(victim,"alive",false);Set(battle,"mlEpisodeActive",false);
            Call(battle,"CheckBattleResult");
            Assert.That(Get<object>(battle,"waveOutcomeStage").ToString(),Is.EqualTo("Message"));
        }
        finally {UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(other);}
    }

    [Test] public void AtlasCropAndTrimOffsetRestoreOnlyTheCorrectPixelsIntoTheLogicalCanvas()
    {
        var texture=new Texture2D(32,32,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        var pixels=Enumerable.Repeat(new Color32(255,0,255,255),32*32).ToArray();
        for(int y=8;y<16;y++)for(int x=16;x<24;x++)pixels[y*32+x]=new Color32(0,255,0,255);
        texture.SetPixels32(pixels);texture.Apply(false,true);
        try
        {
            var restored=(Color32[])FragmentType.GetMethod("ReadSpritePixels",Static).Invoke(null,
                new object[]{texture,new Rect(16,8,8,8),new Vector2(16,16),new Vector2(4,6)});
            Assert.That(restored.Length,Is.EqualTo(48*48));Assert.That(restored.Count(p=>p.a>0),Is.EqualTo(24*24));
            for(int y=0;y<48;y++)for(int x=0;x<48;x++)
            {
                Color32 pixel=restored[y*48+x];bool inside=x>=12&&x<36&&y>=18&&y<42;
                Assert.That(pixel.a,Is.EqualTo(inside?255:0));
                if(inside){Assert.That(pixel.r,Is.Zero);Assert.That(pixel.g,Is.EqualTo(255));Assert.That(pixel.b,Is.Zero);}
            }
        }
        finally{Object.DestroyImmediate(texture);}
    }

    private MonoBehaviour Create(Action burst)
    {
        var before=Fragments();Assert.That((bool)FragmentType.GetMethod("Create",Static).Invoke(null,new object[]{source,Vector3.right,burst}),Is.True);
        return Fragments().Except(before).Single();
    }
    private static MonoBehaviour[] Fragments()=>Resources.FindObjectsOfTypeAll<MonoBehaviour>()
        .Where(x=>x!=null&&x.GetType()==FragmentType&&x.gameObject.scene.IsValid()).ToArray();
    private static ParticleSystem.Particle[] Read(MonoBehaviour fx)
    {
        var particles=Get<ParticleSystem>(fx,"pixelParticles");var buffer=new ParticleSystem.Particle[particles.main.maxParticles];
        int count=particles.GetParticles(buffer);Array.Resize(ref buffer,count);return buffer;
    }
    private static float Brightness(ParticleSystem.Particle pixel)=>pixel.startColor.r+pixel.startColor.g+pixel.startColor.b;
    private void Source(string species)
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get(species);
        sourceRoot=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
        driver=sourceRoot.GetComponentInChildren<SpriteMonsterAnimationDriver>();Call(driver,"Advance",0f);Call(driver,"LateUpdate");
        source=driver.GetComponent<SpriteRenderer>();reaction=sourceRoot.GetComponent<VoxelEnemyReaction>()??sourceRoot.AddComponent<VoxelEnemyReaction>();
        reaction.Initialize();
    }
    private static void Advance(MonoBehaviour fx,float dt)=>Call(fx,"Advance",dt);
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Fields).GetValue(value);
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Fields).SetValue(value,data);
    private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Fields).Invoke(value,args);
}
