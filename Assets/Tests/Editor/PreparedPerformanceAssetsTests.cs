using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is=UnityEngine.TestTools.Constraints.Is;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PreparedPerformanceAssetsTests
{
    [UnityTest]
    public IEnumerator CombatBodyWarmupOnlyScansUncachedCombatFramesWithoutAdvancingAnimation()
    {
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var cache=(IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",flags).GetValue(null);
        var sprites=new Sprite[7];
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        var host=new GameObject("Combat body warmup test");host.SetActive(false);
        try
        {
            var pixels=new Color32[64*64];
            for(int y=12;y<48;y++)for(int x=8;x<24;x++)pixels[y*64+x]=new Color32(255,255,255,255);
            texture.SetPixels32(pixels);texture.Apply(false,true);
            for(int i=0;i<sprites.Length;i++)
                sprites[i]=Sprite.Create(texture,new Rect(0,0,64,64),Vector2.one*.5f,32f,0,SpriteMeshType.FullRect);
            var driver=host.AddComponent<SpriteMonsterAnimationDriver>();
            driver.Configure(new[]{sprites[0],null,sprites[0]},new[]{sprites[1]},new[]{sprites[2]},
                new[]{sprites[3]},new[]{sprites[4]},new[]{sprites[5]},new[]{sprites[6]});
            var renderer=host.GetComponent<SpriteRenderer>();renderer.sprite=sprites[0];
            var frames=((System.Collections.Generic.IEnumerable<Sprite>)typeof(SpriteMonsterAnimationDriver)
                .GetMethod("CombatFrames",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(driver,null)).ToArray();
            CollectionAssert.AreEqual(new[]{sprites[0],sprites[0],sprites[1],sprites[2],sprites[3],sprites[4],sprites[6]},frames);
            var random=UnityEngine.Random.state;
            var position=driver.transform.localPosition;var scale=driver.transform.localScale;
            string motion=driver.CurrentMotionName;int frameIndex=driver.CurrentFrameIndex;
            int effects=Resources.FindObjectsOfTypeAll<PixelMantisReaper>().Length;
            var expectedBounds=new Bounds(new Vector3(-.5f,-.0625f,0),new Vector3(.5f,1.125f,0));
            Assert.That(typeof(PixelMantisReaper).GetMethod("VisibleBody",flags).Invoke(null,new object[]{sprites[1]}),
                Is.EqualTo(expectedBounds));
            var cachedOnly=(IEnumerator)typeof(PixelMantisReaper).GetMethod("WarmupVisibleBodies",flags)
                .Invoke(null,new object[]{new Sprite[]{null,sprites[1],sprites[1]}});
            try{Assert.That(cachedOnly.MoveNext(),Is.False,"Null, duplicate and already-cached frames need no wait.");}
            finally{((IDisposable)cachedOnly).Dispose();}
            var warmup=(IEnumerator)typeof(PixelMantisReaper).GetMethod("WarmupVisibleBodies",flags)
                .Invoke(null,new object[]{frames.Concat(new Sprite[]{null})});
            try{while(warmup.MoveNext())yield return null;}
            finally{((IDisposable)warmup).Dispose();}
            Assert.That(frames.Distinct().Count(sprite=>cache.Contains(sprite)),Is.EqualTo(6),
                "Every unique combat sprite must be cached before the warmup completes.");
            for(int i=0;i<5;i++)
                Assert.That(cache[sprites[i]],Is.EqualTo(expectedBounds));
            Assert.That(cache.Contains(sprites[5]),Is.False,"Capture frames are outside active combat.");
            Assert.That(cache[sprites[6]],Is.EqualTo(expectedBounds),"Dragged and held monsters still display their Struggle frames in combat.");
            warmup=(IEnumerator)typeof(PixelMantisReaper).GetMethod("WarmupVisibleBodies",flags)
                .Invoke(null,new object[]{frames});
            Assert.That(warmup.MoveNext(),Is.False,"Cached sprites must not add another wait.");
            Assert.That(driver.transform.localPosition,Is.EqualTo(position));
            Assert.That(driver.transform.localScale,Is.EqualTo(scale));
            Assert.That(driver.CurrentMotionName,Is.EqualTo(motion));
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(frameIndex));
            Assert.That(renderer.sprite,Is.SameAs(sprites[0]));
            Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
            Assert.That(Resources.FindObjectsOfTypeAll<PixelMantisReaper>().Length,Is.EqualTo(effects));
        }
        finally
        {
            Object.DestroyImmediate(host);
            foreach(Sprite sprite in sprites)
            {
                if(sprite==null)continue;
                cache.Remove(sprite);Object.DestroyImmediate(sprite);
            }
            Object.DestroyImmediate(texture);
        }
    }

    [UnityTest]
    public IEnumerator CombatBodyWarmupTraversesAllUnitsAndSkipsUnitsWithoutSpriteAnimation()
    {
        var staticFlags=BindingFlags.Static|BindingFlags.NonPublic;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var cache=(IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",staticFlags).GetValue(null);
        var texture=new Texture2D(8,8,TextureFormat.RGBA32,false);
        texture.SetPixels32(Enumerable.Repeat(new Color32(255,255,255,255),64).ToArray());
        texture.Apply(false,true);
        var sprites=new[]{
            Sprite.Create(texture,new Rect(0,0,8,8),Vector2.one*.5f,8f,0,SpriteMeshType.FullRect),
            Sprite.Create(texture,new Rect(0,0,8,8),Vector2.one*.5f,16f,0,SpriteMeshType.FullRect)};
        var host=new GameObject("Combined combat body warmup");host.SetActive(false);
        var models=new GameObject[sprites.Length];
        IEnumerator warmup=null;
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();
        var unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        var units=(IList)typeof(MonsterAutoBattlePrototype).GetField("units",flags).GetValue(battle);
        try
        {
            units.Add(Activator.CreateInstance(unitType,true));
            for(int i=0;i<sprites.Length;i++)
            {
                models[i]=new GameObject("Combat body source "+i);models[i].SetActive(false);
                var driver=models[i].AddComponent<SpriteMonsterAnimationDriver>();
                var frames=new[]{sprites[i],null,sprites[i]};
                driver.Configure(frames,frames,frames,frames,frames,frames,frames);
                var unit=Activator.CreateInstance(unitType,true);
                unitType.GetField("spriteAnimation").SetValue(unit,driver);units.Add(unit);
            }
            warmup=(IEnumerator)typeof(MonsterAutoBattlePrototype).GetMethod("WarmupUnitBodyBounds",flags).Invoke(battle,null);
            yield return warmup;
            Assert.That(cache.Contains(sprites[0]),Is.True);
            Assert.That(cache.Contains(sprites[1]),Is.True,"Later units must not be lost when the work is flattened.");
            Assert.That(cache[sprites[0]],Is.EqualTo(new Bounds(Vector3.zero,new Vector3(1f,1f,0f))));
            Assert.That(cache[sprites[1]],Is.EqualTo(new Bounds(Vector3.zero,new Vector3(.5f,.5f,0f))));
            Assert.That(battle.IsSceneReady,Is.False,"Body preparation alone cannot publish scene readiness.");
        }
        finally
        {
            (warmup as IDisposable)?.Dispose();units.Clear();
            Object.DestroyImmediate(host);
            foreach(var model in models)if(model!=null)Object.DestroyImmediate(model);
            foreach(var sprite in sprites){cache.Remove(sprite);Object.DestroyImmediate(sprite);}
            Object.DestroyImmediate(texture);
        }
    }

    [UnityTest]
    public IEnumerator TranscendedBearPreparationWarmsEveryGroundedAvatarMotionWithoutChangingTheUnit()
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var staticFlags=BindingFlags.Static|BindingFlags.NonPublic;
        var cache=(IDictionary)typeof(PixelMantisReaper).GetField("visibleBounds",staticFlags).GetValue(null);
        var spiritFrames=typeof(PixelBearUpperBodyHologram).GetMethod("SpiritFrames",staticFlags);
        var motions=new[]{"Idle","Run","Attack","SkillAttack","Death","Struggle"};
        var frames=motions.Select(motion=>(Sprite[])spiritFrames.Invoke(null,new object[]{motion,true})).ToArray();
        for(int i=0;i<motions.Length;i++)Assert.That(frames[i],Is.Not.Empty,motions[i]);
        var avatarFrames=frames.SelectMany(values=>values).Distinct().ToArray();
        var originalBounds=new System.Collections.Generic.Dictionary<Sprite,Bounds>();
        foreach(var sprite in avatarFrames)
        {
            if(cache.Contains(sprite))originalBounds.Add(sprite,(Bounds)cache[sprite]);
            cache.Remove(sprite);
        }
        var host=new GameObject("Transcended bear body preparation");host.SetActive(false);
        var model=new GameObject("Unchanged bear presentation");model.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();
        var unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        var units=(IList)typeof(MonsterAutoBattlePrototype).GetField("units",flags).GetValue(battle);
        IEnumerator warmup=null;
        try
        {
            var driver=model.AddComponent<SpriteMonsterAnimationDriver>();
            var empty=Array.Empty<Sprite>();driver.Configure(empty,empty,empty,empty,empty,empty,empty);
            var unit=Activator.CreateInstance(unitType,true);
            var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_dami");
            Assert.That(definition.familyId,Is.EqualTo("bear"));
            unitType.GetField("definition").SetValue(unit,definition);
            unitType.GetField("model").SetValue(unit,model);
            unitType.GetField("spriteAnimation").SetValue(unit,driver);units.Add(unit);
            var prepare=typeof(MonsterAutoBattlePrototype).GetMethod("WarmupUnitBodyBounds",flags);
            warmup=(IEnumerator)prepare.Invoke(battle,null);
            yield return warmup;
            Assert.That(avatarFrames.Count(sprite=>cache.Contains(sprite)),Is.Zero,
                "Common cinematic preparation must not scan an ordinary bear's unused transcended avatar.");
            ((IDisposable)warmup).Dispose();

            unitType.GetField("augmentTranscended").SetValue(unit,true);
            Vector3 position=driver.transform.localPosition,scale=driver.transform.localScale;
            string motionName=driver.CurrentMotionName;int frameIndex=driver.CurrentFrameIndex;
            int effects=Resources.FindObjectsOfTypeAll<PixelBearUpperBodyHologram>().Length;
            warmup=(IEnumerator)prepare.Invoke(battle,null);
            yield return warmup;
            Assert.That(avatarFrames.Count(sprite=>cache.Contains(sprite)),Is.EqualTo(avatarFrames.Length),
                "The roster's future grounded avatar, including Struggle, must be ready before combat.");
            Assert.That(driver.BearAvatarActive,Is.False,"Preparation must not activate the actual transformation.");
            Assert.That(driver.CurrentMotionName,Is.EqualTo(motionName));
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(frameIndex));
            Assert.That(driver.transform.localPosition,Is.EqualTo(position));
            Assert.That(driver.transform.localScale,Is.EqualTo(scale));
            Assert.That(Resources.FindObjectsOfTypeAll<PixelBearUpperBodyHologram>().Length,Is.EqualTo(effects));
            Assert.That(battle.IsSceneReady,Is.False);
        }
        finally
        {
            (warmup as IDisposable)?.Dispose();units.Clear();
            Object.DestroyImmediate(host);Object.DestroyImmediate(model);
            foreach(var sprite in avatarFrames)cache.Remove(sprite);
            foreach(var pair in originalBounds)cache[pair.Key]=pair.Value;
        }
    }

    [UnityTest]
    public IEnumerator CombatAudioWarmupLoadsEveryClipAndPreservesLevelsWithStaleEffectMetadata()
    {
        var singleton=typeof(GameAudio).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        object previous=singleton.GetValue(null);
        var host=new GameObject("Combat audio data preparation test");host.SetActive(false);
        var audio=host.AddComponent<GameAudio>();
        var prepared=JsonUtility.FromJson<GameAudio.PreparedCombatSounds>(
            Resources.Load<TextAsset>(GameAudio.PreparedCombatSoundsPath).text);
        var effects=Resources.Load<PreparedEffectSoundLevels>("Audio/PreparedEffectSoundLevels");
        var original=effects.clips;
        var stale=(PreparedEffectSoundLevels.Entry[])original.Clone();
        stale[0].samples++;
        effects.clips=stale;
        singleton.SetValue(null,audio);
        try
        {
            yield return GameAudio.WarmupCombatSounds();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            Assert.That(typeof(GameAudio).GetField("combatAudioWarmupComplete",flags).GetValue(audio),Is.True);
            var levels=(IDictionary)typeof(GameAudio).GetField("combatLevels",flags).GetValue(audio);
            foreach(var entry in prepared.clips)
            {
                var clip=Resources.Load<AudioClip>(entry.path);
                Assert.That(clip,Is.Not.Null,entry.path);
                Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),entry.path);
                Assert.That(clip.loadInBackground,Is.True,entry.path);
                Assert.That(levels.Contains(clip),Is.True,entry.path);
                object level=levels[clip];
                Assert.That(level.GetType().GetField("peak").GetValue(level),Is.EqualTo(entry.peak));
                Assert.That(level.GetType().GetField("rms").GetValue(level),Is.EqualTo(entry.rms));
            }
            foreach(var entry in original)
            {
                Assert.That(entry.clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),entry.clip.name);
                Assert.That(entry.clip.loadInBackground,Is.True,entry.clip.name);
                Assert.That(levels.Contains(entry.clip),Is.True,"Even a stale metadata entry must retain its decoded clip.");
                object level=levels[entry.clip];
                Assert.That((float)level.GetType().GetField("peak").GetValue(level),Is.EqualTo(entry.peak).Within(.000001f));
                Assert.That((float)level.GetType().GetField("rms").GetValue(level),Is.EqualTo(entry.rms).Within(.000001f));
            }
            Assert.That(GameAudio.WarmupCombatSounds().MoveNext(),Is.False);
            Assert.That(host.GetComponents<AudioSource>().Any(source=>source.isPlaying),Is.False);
        }
        finally
        {
            effects.clips=original;
            singleton.SetValue(null,previous);
            Object.DestroyImmediate(host);
        }
    }

    [UnityTest]
    public IEnumerator ProceduralNinjaSpritePreservesTightGeometryPixelsAndUnreadableTexture()
    {
        const int width=32,height=40;
        const float ppu=28f;
        Func<int,int,Color> pixel=(x,y)=>
        {
            int distance=Mathf.Abs(x-15);
            return distance>3+y%11||y<2||y>37?Color.clear:
                new Color(x/31f,y/39f,.45f,distance==3+y%11?.5f:1f);
        };
        var referenceTexture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        Texture2D actualTexture=null,readable=null;
        Sprite reference=null,actual=null;
        RenderTexture copy=null,previous=RenderTexture.active;
        try
        {
            var colors=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)colors[y*width+x]=pixel(x,y);
            referenceTexture.SetPixels(colors);referenceTexture.Apply(false,true);
            reference=Sprite.Create(referenceTexture,new Rect(0,0,width,height),Vector2.one*.5f,ppu);
            actual=(Sprite)typeof(PixelNinjaSubstitution).GetMethod("MakeSprite",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{"Prepared ninja regression",width,height,ppu,pixel});
            actualTexture=actual.texture;
            CollectionAssert.AreEqual(reference.vertices,actual.vertices);
            CollectionAssert.AreEqual(reference.triangles,actual.triangles);
            CollectionAssert.AreEqual(reference.uv,actual.uv);
            Assert.That(actual.bounds,Is.EqualTo(reference.bounds));
            Assert.That(actual.rect,Is.EqualTo(reference.rect));
            Assert.That(actual.pivot,Is.EqualTo(reference.pivot));
            Assert.That(actual.pixelsPerUnit,Is.EqualTo(reference.pixelsPerUnit));
            Assert.That(actualTexture.isReadable,Is.False);
            Assert.That(actualTexture.filterMode,Is.EqualTo(referenceTexture.filterMode));
            Assert.That(actualTexture.wrapMode,Is.EqualTo(referenceTexture.wrapMode));
            Assert.That(actualTexture.hideFlags,Is.EqualTo(referenceTexture.hideFlags));
            Assert.That(actual.hideFlags,Is.EqualTo(HideFlags.HideAndDontSave));
            yield return null;

            copy=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            readable=new Texture2D(width,height,TextureFormat.RGBA32,false);
            Graphics.Blit(referenceTexture,copy);RenderTexture.active=copy;
            readable.ReadPixels(new Rect(0,0,width,height),0,0,false);
            Color32[] expected=readable.GetPixels32();
            Graphics.Blit(actualTexture,copy);
            readable.ReadPixels(new Rect(0,0,width,height),0,0,false);
            CollectionAssert.AreEqual(expected,readable.GetPixels32());
        }
        finally
        {
            RenderTexture.active=previous;
            if(copy!=null)RenderTexture.ReleaseTemporary(copy);
            Object.DestroyImmediate(readable);
            Object.DestroyImmediate(actual);Object.DestroyImmediate(actualTexture);
            Object.DestroyImmediate(reference);Object.DestroyImmediate(referenceTexture);
        }
    }

    [Test]
    public void UiButtonsReuseFrameTexturesAcrossFontSizesWithoutChangingTheirColors()
    {
        var method=typeof(BombBrickUiTheme).GetMethod("FramedButtonStyle",BindingFlags.NonPublic|BindingFlags.Static);
        var small=(GUIStyle)method.Invoke(null,new object[]{12,true,true,false});
        var large=(GUIStyle)method.Invoke(null,new object[]{24,true,true,false});
        var primary=(GUIStyle)method.Invoke(null,new object[]{24,true,true,true});
        Assert.That(small.normal.background,Is.SameAs(large.normal.background));
        Assert.That(small.hover.background,Is.SameAs(large.hover.background));
        Assert.That(large.normal.background,Is.Not.SameAs(primary.normal.background));
        Assert.That(large.normal.background.width,Is.EqualTo(12));
        Assert.That(large.normal.background.filterMode,Is.EqualTo(FilterMode.Point));
    }

    [Test]
    public void UnityAllocationConstraintDetectsKnownAllocationsAndQuietDelegates()
    {
        byte[] allocation=null;
        Assert.That(()=>{allocation=new byte[1048576];},Is.AllocatingGCMemory());
        Assert.That(allocation,Has.Length.EqualTo(1048576));
        Assert.That(()=>{GC.KeepAlive(allocation);},Is.Not.AllocatingGCMemory());
    }

    [Test]
    public void CombatLookupsDoNotAllocateOrHideLiveResearchChanges()
    {
        var profile=new PlayerProfileSave();
        profile.permanentResearchAllocations.Add(new PermanentResearchAllocationSave
            {researchId=PermanentResearchSystem.Recovery,rank=3});
        var row=PermanentResearchSystem.All.First(r=>r.id==PermanentResearchSystem.Recovery);
        float expected=1f+3f*row.valuePerRank;
        Assert.That(PermanentResearchSystem.RecoveryMultiplier(profile),Is.EqualTo(expected));
        for(int i=0;i<16;i++){PermanentResearchSystem.RecoveryMultiplier(profile);GameMasterData.Skill("raptor_hunting_leap");}
        TestDelegate work=()=>
        {
            for(int i=0;i<2048;i++){PermanentResearchSystem.RecoveryMultiplier(profile);GameMasterData.Skill("raptor_hunting_leap");}
        };
        work();Assert.That(work,Is.Not.AllocatingGCMemory(),"Frame queries must not rebuild/sort catalogs or allocate predicates.");
        profile.permanentResearchAllocations[0].rank=1;
        Assert.That(PermanentResearchSystem.RecoveryMultiplier(profile),Is.EqualTo(1f+row.valuePerRank));
        Assert.That(PermanentResearchSystem.Value(profile,"missing"),Is.Zero);
        Assert.That(GameMasterData.Skill("missing"),Is.Null);
    }

    [Test]
    public void TraitMembershipDoesNotAllocateForCombatListsOrArraysAndPreservesFallbackSemantics()
    {
        var traits=new System.Collections.Generic.List<string>{null,"Mixed","other"};
        var array=traits.ToArray();
        Assert.That(MonsterTraitRuntime.Has(null,"Mixed"),Is.False);
        Assert.That(MonsterTraitRuntime.Has(traits,null),Is.True);
        Assert.That(MonsterTraitRuntime.Has(traits,"Mixed"),Is.True);
        Assert.That(MonsterTraitRuntime.Has(traits,"mixed"),Is.False);
        var fallback=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase){"Mixed"};
        Assert.That(MonsterTraitRuntime.Has(fallback,"Mixed"),Is.True);
        Assert.That(MonsterTraitRuntime.Has(fallback,"mixed"),Is.False);
        for(int i=0;i<16;i++)
        {
            MonsterTraitRuntime.Has(traits,"Mixed");MonsterTraitRuntime.Has(traits,"missing");
            MonsterTraitRuntime.Has(array,"Mixed");MonsterTraitRuntime.Has(array,"missing");
        }
        bool matches=true;
        TestDelegate work=()=>
        {
            for(int i=0;i<2048;i++)
            {
                matches&=MonsterTraitRuntime.Has(traits,"Mixed")&&!MonsterTraitRuntime.Has(traits,"missing");
                matches&=MonsterTraitRuntime.Has(array,"Mixed")&&!MonsterTraitRuntime.Has(array,"missing");
            }
        };
        work();Assert.That(work,Is.Not.AllocatingGCMemory(),"Combat trait lookups must not box collection enumerators.");
        Assert.That(matches,Is.True);
    }

    [Test]
    public void ItemEffectLookupsPreserveFirstMatchAndNumericAggregation()
    {
        var type=typeof(MonsterAutoBattlePrototype);
        var unit=Activator.CreateInstance(type.GetNestedType("Unit",BindingFlags.NonPublic),true);
        var items=(CombatItemModifiers)unit.GetType().GetField("itemModifiers").GetValue(unit);
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        Func<ItemMasterRow,float> selector=row=>row.effectParam1;
        foreach(var values in new[]{Array.Empty<float>(),new[]{-5f,-2f},new[]{float.NaN,3f,-2f},new[]{float.NaN}})
        {
            items.specialEffects.Clear();
            items.specialEffects.Add(new ItemMasterRow{specialEffect="other",effectParam1=100f});
            foreach(float value in values)items.specialEffects.Add(new ItemMasterRow{specialEffect="selected",effectParam1=value});
            var args=new object[]{unit,"selected",selector};
            Assert.That(type.GetMethod("EffectSum",flags).Invoke(null,args),Is.EqualTo(values.Sum()));
            Assert.That(type.GetMethod("EffectMax",flags).Invoke(null,args),Is.EqualTo(values.Length==0?0f:values.Max()));
            Assert.That(type.GetMethod("HasEffect",flags).Invoke(null,new[]{unit,"selected"}),Is.EqualTo(values.Length>0));
        }
        var host=new GameObject("Corruption lookup test");host.SetActive(false);
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var first=new ItemTransformMasterRow{corruptionGreatEffectId="MiXeD"};
            items.transformedEffects.Add(null);items.transformedEffects.Add(first);
            items.transformedEffects.Add(new ItemTransformMasterRow{corruptionGreatEffectId="mixed"});
            Assert.That(type.GetMethod("CorruptionEffect",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(battle,new[]{unit,"mixed"}),Is.SameAs(first));
        }
        finally{Object.DestroyImmediate(host);}
    }

    [TestCase("prototype_region",1,4,"ager|chundongi|gaiason|velos")]
    [TestCase("poison_fog_swamp",6,6,"bonrex|doksirum|flotis|holophonas|raputo|sundongi")]
    [TestCase("crimson_jungle",8,6,"rokrok|patjwi|magril|griphonas|yeomhae|magno")]
    [TestCase("frozen_highlands",10,7,"wilo|seoljwi|seollang|cheolno|lunadeer|seolmyo|frovire")]
    [TestCase("sky_sanctuary",12,8,"holydeer|solarbeetle|geumgangsu|noepung|uno|dami|lumirum|santius")]
    public void DistributedWaveSearchKeepsTheSeededRosterAndGuardianRules(
        string region,int wave,int count,string expected)
    {
        var database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        var synchronous=HunterRegionCatalog.BuildExpeditionWaveMonsterIds(
            region,wave,count,"performance_regression",database);
        if(wave==8)
        {
            var final=HunterRegionCatalog.BuildExpeditionWaveMonsterIds(region,12,
                GameMasterData.WaveScaling(region,12).enemyCount,"performance_regression",database);
            Assert.That(synchronous,Is.EqualTo(new[]{final[0]}));
        }
        else if(wave==12)Assert.That(synchronous,Is.EquivalentTo(expected.Split('|')));
        else Assert.That(string.Join("|",synchronous),Is.EqualTo(expected));
        System.Collections.Generic.List<string> result=null;
        var search=HunterRegionCatalog.BuildExpeditionWaveMonsterIdsAcrossFrames(
            region,wave,count,"performance_regression",database,value=>result=value);
        int yields=0;
        while(search.MoveNext())
        {
            Assert.That(result,Is.Null,"Partial candidates must not become the selected wave.");
            yields++;
        }
        Assert.That(yields,Is.GreaterThan(1));
        Assert.That(result,Is.EqualTo(synchronous));
    }

    [Test]
    public void CancellingWaveSearchDoesNotPublishAPartialComposition()
    {
        bool completed=false;
        var search=HunterRegionCatalog.BuildExpeditionWaveMonsterIdsAcrossFrames(
            "sky_sanctuary",12,8,"performance_regression",
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase"),value=>completed=true);
        Assert.That(search.MoveNext(),Is.True);
        ((IDisposable)search).Dispose();
        Assert.That(completed,Is.False);
    }

    [UnityTest]
    public IEnumerator EffectWarmupRetainsSpriteFramesAndDoesNotSpawnLiveEffects()
    {
        var catalog = Resources.Load<TextAsset>("CombatEffects/PreparedFramePaths");
        Assert.That(catalog, Is.Not.Null);
        var paths = catalog.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(paths.Distinct().Count(), Is.EqualTo(paths.Length));
        Assert.That(paths.Length, Is.GreaterThan(20));
        int effects = Resources.FindObjectsOfTypeAll<PixelCombatEffect>().Length;
        yield return GameResourceCache.WarmupCombatEffects();
        foreach (var path in paths)
        {
            Assert.That(GameResourceCache.GetTexture(path), Is.Not.Null, path);
            Assert.That(PixelCombatEffect.LoadFrames(path).Select(sprite => sprite.name),
                Is.EqualTo(Resources.LoadAll<Sprite>(path).OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                    .Select(sprite => sprite.name)), path);
        }
        Assert.That(Resources.FindObjectsOfTypeAll<PixelCombatEffect>().Length, Is.EqualTo(effects));
    }

    [Test]
    public void RegionRosterParsingReusesItsResultAndInvalidatesWhenTheSourceChanges()
    {
        var region = new HunterRegionDefinition { monsterIds = "velos| ironkuma |velos||" };
        var ids = region.MonsterIds();
        Assert.That(ids, Is.EqualTo(new[] { "velos", "ironkuma" }));
        Assert.That(region.MonsterIds(), Is.SameAs(ids));
        region.monsterIds = "hongryeon";
        Assert.That(region.MonsterIds(), Is.EqualTo(new[] { "hongryeon" }));
        region.monsterIds = null;
        Assert.That(region.MonsterIds(), Is.Empty);
    }

    [Test]
    public void ScenePreparationHoldsInputAndDoesNotReportReadyBeforeItsWorkFinishes()
    {
        var host = new GameObject("Scene preparation test"); host.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var battle = host.AddComponent<MonsterAutoBattlePrototype>();
            var preparation = (IEnumerator)typeof(MonsterAutoBattlePrototype)
                .GetMethod("InitializePlayerScene", flags).Invoke(battle, null);
            Assert.That(preparation.MoveNext(), Is.True);
            Assert.That(battle.IsSceneReady, Is.False);
            Assert.That(battle.SaveBeforeLeaving(), Is.False);
            Assert.That(typeof(MonsterAutoBattlePrototype).GetField("wavePreparationAcrossFrames", flags)
                .GetValue(battle), Is.True);
            ((IDisposable)preparation).Dispose();
            Assert.That(battle.IsSceneReady, Is.False);
            Assert.That(typeof(MonsterAutoBattlePrototype).GetField("wavePreparationAcrossFrames", flags)
                .GetValue(battle), Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WavePreparationCallsBackOnlyAfterCompletionAndAlwaysReleasesItsGuard(bool cancel)
    {
        var host = new GameObject("Wave preparation test"); host.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(MonsterAutoBattlePrototype);
        try
        {
            var battle = host.AddComponent<MonsterAutoBattlePrototype>();
            var run = new ExpeditionRunSave { wave = 3, regionId = HunterRegionCatalog.FirstRegionId };
            type.GetField("expeditionRun", flags).SetValue(battle, run);
            bool completed = false;
            var preparation = (IEnumerator)type.GetMethod("PreparePlayerWaveRoutine", flags)
                .Invoke(battle, new object[] { false, (Action)(() => completed = true) });
            Assert.That(preparation.MoveNext(), Is.True);
            Assert.That(type.GetField("wavePreparationAcrossFrames", flags).GetValue(battle), Is.True);
            type.GetMethod("AdvanceToNextWave", flags).Invoke(battle, null);
            Assert.That(run.wave, Is.EqualTo(3));
            Assert.That(completed, Is.False);
            if (cancel) ((IDisposable)preparation).Dispose();
            else
            {
                var reset = (IEnumerator)preparation.Current;
                while (reset.MoveNext()) { }
                Assert.That(completed, Is.False);
                Assert.That(preparation.MoveNext(), Is.False);
            }
            Assert.That(completed, Is.EqualTo(!cancel));
            Assert.That(type.GetField("wavePreparationAcrossFrames", flags).GetValue(battle), Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void ScenePreparationSkipsInvalidSavedAugmentsBeforeWarmingValidSelection()
    {
        var host=new GameObject("Saved augment preparation test");host.SetActive(false);
        var type=typeof(MonsterAutoBattlePrototype);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();
        var run=new ExpeditionRunSave();
        run.augments.Add(null);
        run.augments.Add(new ExpeditionAugmentSelectionSave{augmentId=" "});
        run.augments.Add(new ExpeditionAugmentSelectionSave{augmentId="augment_auto_deployment"});
        type.GetField("expeditionRun",flags).SetValue(battle,run);
        var preparation=(IEnumerator)type.GetMethod("InitializePlayerScene",flags).Invoke(battle,null);
        try
        {
            IEnumerator spawn=null;
            // Added cinematic warmups may yield freely before the saved-run phase.
            const int maximumPreparationSteps=128;
            for(int step=0;step<maximumPreparationSteps&&spawn==null;step++)
            {
                Assert.That(preparation.MoveNext(),Is.True);
                var pending=preparation.Current as IEnumerator;
                if(pending!=null&&pending.GetType().Name.StartsWith("<SpawnTeamsRoutine>",StringComparison.Ordinal))
                    spawn=pending;
            }
            Assert.That(spawn,Is.Not.Null);
            Assert.That(type.GetField("periodicStatusTick",flags).GetValue(battle),Is.Not.Null);
            Assert.That(type.GetField("skillExecutor",flags).GetValue(battle),Is.Not.Null);
            // Complete spawning without loading a profile or creating live combat units.
            ((Action<bool>)spawn.GetType().GetField("spawned",flags).GetValue(spawn))(true);
            Assert.That(preparation.MoveNext(),Is.True);
            var warmup=(IEnumerator)preparation.Current;
            Assert.That(warmup.GetType().GetField("path",flags).GetValue(warmup),
                Is.EqualTo("UI/AugmentIcons/augment_auto_deployment"));
            Assert.That(battle.IsSceneReady,Is.False);
        }
        finally
        {
            ((IDisposable)preparation).Dispose();
            (type.GetField("raptorCinematicStage",flags).GetValue(battle) as IDisposable)?.Dispose();
            type.GetField("raptorCinematicStage",flags).SetValue(battle,null);
            var assets=new System.Collections.Generic.HashSet<Object>();
            foreach(string name in new[]{"tileMaterial","unitShadowMaterial","skillRadiusMarkerMaterial",
                "tileMesh","benchTileMesh","placementMarkerSprite","benchPlacementMarkerSprite"})
            {
                var field=type.GetField(name,flags);
                var asset=(Object)field.GetValue(battle);
                if(asset is Sprite sprite)assets.Add(sprite.texture);
                if(asset!=null)assets.Add(asset);
                field.SetValue(battle,null);
            }
            foreach(var asset in assets)if(asset!=null)Object.DestroyImmediate(asset);
            Object.DestroyImmediate(host);
        }
    }

    [TestCase("MainMenuSkyOccluder")]
    [TestCase("MainMenuTreeOccluder")]
    public void PreparedMenuMaskKeepsBackdropDimensionsAndPointSampling(string name)
    {
        var backdrop = Resources.Load<Texture2D>("Environment/MainMenu/MainMenuExpeditionBase");
        var mask = Resources.Load<Texture2D>("Environment/MainMenu/" + name);
        Assert.That(mask, Is.Not.Null);
        Assert.That(mask.width, Is.EqualTo(backdrop.width));
        Assert.That(mask.height, Is.EqualTo(backdrop.height));
        Assert.That(mask.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(mask.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        Assert.That(mask.mipmapCount, Is.EqualTo(1));
    }

    [Test]
    public void PreparedMenuAnimationRetainsItsLogicalPixelGrid()
    {
        var atlas = Resources.Load<Texture2D>("Environment/MainMenu/ChundongiPeekAtlas_Runtime");
        Assert.That(atlas, Is.Not.Null);
        Assert.That(atlas.width, Is.EqualTo(128)); Assert.That(atlas.height, Is.EqualTo(128));
        Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Point));
    }

    [TestCase("Audio/Combat/Wyvern/breath")]
    [TestCase("Audio/Combat/Tyranno/mawappear")]
    [TestCase("Audio/Combat/NinjaCat/blast")]
    [TestCase("Audio/Rewards/chest_open")]
    public void PreparedSoundLevelsMatchTheOriginalMixerAnalysis(string path)
    {
        var data = JsonUtility.FromJson<GameAudio.PreparedCombatSounds>(
            Resources.Load<TextAsset>(GameAudio.PreparedCombatSoundsPath).text);
        Assert.That(data.clips.Select(c => c.path).Distinct().Count(), Is.EqualTo(data.clips.Length));
        var expected = data.clips.Single(c => c.path == path);
        var clip = Resources.Load<AudioClip>(path);
        var host = new GameObject("Prepared sound test"); host.SetActive(false);
        try
        {
            var audio = host.AddComponent<GameAudio>();
            var actual = typeof(GameAudio).GetMethod("MeasureCombatClip", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(audio, new object[] { clip });
            Assert.That(expected.samples, Is.EqualTo(clip.samples));
            Assert.That(expected.channels, Is.EqualTo(clip.channels));
            Assert.That(expected.frequency, Is.EqualTo(clip.frequency));
            Assert.That(expected.peak, Is.EqualTo((float)actual.GetType().GetField("peak").GetValue(actual)).Within(.000001f));
            Assert.That(expected.rms, Is.EqualTo((float)actual.GetType().GetField("rms").GetValue(actual)).Within(.000001f));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void ExternalEffectSoundKeepsItsOriginalAudioReferenceAndMixerLevels()
    {
        var data=Resources.Load<PreparedEffectSoundLevels>("Audio/PreparedEffectSoundLevels");
        Assert.That(data,Is.Not.Null);
        var entry=data.clips.Single(e=>e.clip!=null&&e.clip.name=="SFX_Vefects_Zap_Big_02");
        var host=new GameObject("External sound test");host.SetActive(false);
        try
        {
            var audio=host.AddComponent<GameAudio>();
            var level=typeof(GameAudio).GetMethod("MeasureCombatClip",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(audio,new object[]{entry.clip});
            Assert.That(entry.samples,Is.EqualTo(entry.clip.samples));
            Assert.That(entry.peak,Is.EqualTo((float)level.GetType().GetField("peak").GetValue(level)).Within(.000001f));
            Assert.That(entry.rms,Is.EqualTo((float)level.GetType().GetField("rms").GetValue(level)).Within(.000001f));
        }
        finally{Object.DestroyImmediate(host);}
    }

    [Test]
    public void EnemyVisualReuseDetachesTheOldUnitAndKeepsResourcePoolsSeparate()
    {
        var host = new GameObject("Enemy pool test"); host.SetActive(false);
        var model = new GameObject("Enemy model"); var ring = new GameObject("Enemy ring");
        var type = typeof(MonsterAutoBattlePrototype);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var unitType = type.GetNestedType("Unit", BindingFlags.NonPublic);
        try
        {
            var battle = host.AddComponent<MonsterAutoBattlePrototype>();
            var unit = Activator.CreateInstance(unitType, true);
            unitType.GetField("model").SetValue(unit, model);
            unitType.GetField("ring").SetValue(unit, ring);
            unitType.GetField("stableId").SetValue(unit, "enemy_previous_wave");
            unitType.GetField("resourcePath").SetValue(unit, "normal_enemy");
            unitType.GetField("team").SetValue(unit, Enum.ToObject(unitType.GetField("team").FieldType, 1));
            type.GetField("recyclingEnemyVisuals", flags).SetValue(battle, true);
            Assert.That(type.GetMethod("ReturnEnemyVisual", flags).Invoke(battle, new[] { unit }), Is.True);
            Assert.That(unitType.GetField("model").GetValue(unit), Is.Null);
            Assert.That(model.activeSelf, Is.False); Assert.That(ring.activeSelf, Is.False);
            Assert.That(type.GetMethod("TakeEnemyVisual", flags).Invoke(battle, new object[] { "shiny_enemy" }), Is.Null);
            var reused = type.GetMethod("TakeEnemyVisual", flags).Invoke(battle, new object[] { "normal_enemy" });
            Assert.That(reused.GetType().GetField("model").GetValue(reused), Is.SameAs(model));
            Assert.That(reused.GetType().GetField("ring").GetValue(reused), Is.SameAs(ring));
            Assert.That(type.GetField("pooledEnemyVisualCount", flags).GetValue(battle), Is.Zero);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(model); Object.DestroyImmediate(ring); }
    }
}
