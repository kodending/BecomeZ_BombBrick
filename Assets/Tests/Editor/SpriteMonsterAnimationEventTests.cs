using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PixelRaptorAnimationEventTests
{
    [TestCase("Run")]
    [TestCase("Attack")]
    [TestCase("SkillAttack")]
    [TestCase("SkillHold")]
    public void StatusFreezePreservesDisplayedPoseAndResumesPendingHit(string motion)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0]);
        var instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            var renderer=driver.GetComponent<SpriteRenderer>();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var advance=typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",flags);
            var late=typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",flags);
            var elapsed=typeof(SpriteMonsterAnimationDriver).GetField("motionElapsed",flags);
            var hold=typeof(SpriteMonsterAnimationDriver).GetField("skillAttackHoldRemaining",flags);
            int hits=0;
            if(motion=="Run")driver.SetMoving(true);
            else if(motion=="Attack")driver.PlayBasicAttack(1f,()=>hits++);
            else if(motion=="SkillHold")driver.PlaySkillAttackWithHold(2,1f,1f,()=>hits++);
            else driver.PlaySkillAttack(1f,()=>hits++);
            advance.Invoke(driver,new object[]{motion=="SkillHold"?0.5f:0.12f});
            late.Invoke(driver,null);
            driver.transform.localPosition+=new Vector3(0.03f,0.02f,0f);
            driver.transform.localScale*=1.02f;
            Sprite frozenSprite=renderer.sprite;
            int frozenIndex=driver.CurrentFrameIndex;
            string frozenMotion=driver.CurrentMotionName;
            float frozenTime=(float)elapsed.GetValue(driver);
            float frozenHold=(float)hold.GetValue(driver);
            Vector3 frozenPosition=driver.transform.localPosition,frozenScale=driver.transform.localScale;
            Assert.That(frozenIndex,Is.GreaterThan(0));
            driver.SetFrozen(true);

            driver.SetMoving(false);
            driver.SetStruggling(true);
            driver.InterruptCurrentAction();
            driver.SetGlobalSpeedMultiplier(1.5f);
            driver.PlayKnockdown(1f);
            driver.PlayBasicAttack(1f,()=>hits+=10);
            driver.NotifyBasicAttackHitFrameFromAnimation();
            driver.NotifySkillAttackHitFrameFromAnimation();
            for(int i=0;i<5;i++)
            {
                advance.Invoke(driver,new object[]{1f});
                late.Invoke(driver,null);
                driver.SetFrozen(true);
            }
            Assert.That(renderer.sprite,Is.SameAs(frozenSprite));
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(frozenIndex));
            Assert.That(driver.CurrentMotionName,Is.EqualTo(frozenMotion));
            Assert.That(elapsed.GetValue(driver),Is.EqualTo(frozenTime));
            Assert.That(hold.GetValue(driver),Is.EqualTo(frozenHold));
            Assert.That(driver.transform.localPosition,Is.EqualTo(frozenPosition));
            Assert.That(driver.transform.localScale,Is.EqualTo(frozenScale));
            Assert.That(hits,Is.Zero);
            Assert.That(instance.GetComponentInChildren<Animator>(true).speed,Is.Zero);

            driver.SetFrozen(false);
            advance.Invoke(driver,new object[]{0.1f});
            if(motion=="SkillHold")Assert.That((float)hold.GetValue(driver),Is.LessThan(frozenHold));
            else Assert.That((float)elapsed.GetValue(driver),Is.GreaterThan(frozenTime));
            for(int i=0;i<25;i++){advance.Invoke(driver,new object[]{0.1f});late.Invoke(driver,null);}
            Assert.That(hits,Is.EqualTo(motion=="Run"?0:1));
        }
        finally{Object.DestroyImmediate(instance);}
    }

    [Test]
    public void StatusFreezeAllowsDeathAndIsClearedOnReuse()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(UltraLowSDRaptorSetup.PrefabPaths[0]);
        var instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            driver.PlayBasicAttack(1f,()=>Assert.Fail("A dead unit must not hit."));
            driver.SetFrozen(true);
            driver.PlayDeath();
            Assert.That(driver.CurrentMotionName,Is.EqualTo("Death"));
            Assert.That(driver.HasPendingBasicAttackHit,Is.False);
            driver.ResetForReuse();
            driver.SetFrozen(true);
            driver.ResetForReuse();
            driver.SetMoving(true);
            Assert.That(driver.CurrentMotionName,Is.EqualTo("Run"));
            Assert.That(driver.IsActionLocked,Is.False);
        }
        finally{Object.DestroyImmediate(instance);}
    }

    [Test]
    public void ConsumedSkillAttackCanBeInterruptedAndReplayed()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Assert.IsNotNull(instance);
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            driver.PlaySkillAttack(1f,null);
            driver.NotifySkillAttackHitFrameFromAnimation();
            Assert.That(driver.IsActionLocked,Is.True);

            driver.InterruptCurrentAction();
            driver.PlaySkillAttack(1f,()=>{});

            Assert.That(driver.CurrentMotionName,Is.EqualTo("SkillAttack"));
            Assert.That(driver.CurrentFrameIndex,Is.Zero);
            Assert.That(driver.HasPendingSkillAttackHit,Is.True);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void AttackClipUsesFrameThreeHitAnimationEvent()
    {
        AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(
            PixelRaptorFamilySetup.SharedAnimationFolder+
            "/PixelRaptor_Attack.anim");

        Assert.IsNotNull(clip);
        AnimationEvent[] events=AnimationUtility.GetAnimationEvents(clip);
        Assert.AreEqual(1,events.Length);
        Assert.AreEqual(
            SpriteMonsterAnimationEventRelay.BasicAttackHitEventName,
            events[0].functionName);
        Assert.That(
            events[0].time,
            Is.EqualTo(
                PixelRaptorFamilySetup.BasicAttackHitFrameIndex/12f)
                .Within(0.001f));

        ObjectReferenceKeyframe[] spriteKeys=
            AnimationUtility.GetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    "Visual",typeof(SpriteRenderer),"m_Sprite"));
        Assert.That(spriteKeys==null||spriteKeys.Length==0,Is.True);
    }

    [Test]
    public void SkillAttackClipUsesFrameThreeHitAnimationEvent()
    {
        AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(
            PixelRaptorFamilySetup.SharedAnimationFolder+
            "/PixelRaptor_SkillAttack.anim");

        Assert.IsNotNull(clip);
        AnimationEvent[] events=AnimationUtility.GetAnimationEvents(clip);
        Assert.AreEqual(1,events.Length);
        Assert.AreEqual(
            SpriteMonsterAnimationEventRelay.SkillAttackHitEventName,
            events[0].functionName);
        Assert.That(
            events[0].time,
            Is.EqualTo(
                PixelRaptorFamilySetup.SkillAttackHitFrameIndex/11f)
                .Within(0.001f));
    }

    [Test]
    public void SpritePrefabUsesRelayAndIndependentAttackTextures()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);

        Assert.IsNotNull(prefab);
        SpriteMonsterAnimationDriver driver=
            prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        SpriteMonsterAnimationEventRelay relay=
            prefab.GetComponent<SpriteMonsterAnimationEventRelay>();
        Assert.IsNotNull(driver);
        Assert.IsNotNull(relay);
        Assert.AreSame(driver,relay.Driver);

        SerializedProperty frames=
            new SerializedObject(driver).FindProperty("attackFrames");
        Assert.IsNotNull(frames);
        Assert.AreEqual(6,frames.arraySize);
        List<Texture2D> textures=new List<Texture2D>();
        for(int index=0;index<frames.arraySize;index++)
        {
            Sprite sprite=frames.GetArrayElementAtIndex(index)
                .objectReferenceValue as Sprite;
            Assert.IsNotNull(sprite);
            Assert.AreEqual(
                PixelSpriteIdleScaleNormalizer.FrameWidth,
                sprite.texture.width);
            Assert.AreEqual(
                PixelSpriteIdleScaleNormalizer.FrameHeight,
                sprite.texture.height);
            Assert.That(sprite.textureRect.x,Is.EqualTo(0f).Within(0.001f));
            Assert.That(sprite.textureRect.y,Is.EqualTo(0f).Within(0.001f));
            textures.Add(sprite.texture);
        }
        Assert.AreEqual(6,textures.Distinct().Count());
    }

    [Test]
    public void RelayDispatchesPendingHitOnceAndRestoresPlaybackSpeed()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=
            PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Assert.IsNotNull(instance);
        try
        {
            Animator animator=instance.GetComponent<Animator>();
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<
                    SpriteMonsterAnimationDriver>(true);
            SpriteMonsterAnimationEventRelay relay=
                instance.GetComponent<SpriteMonsterAnimationEventRelay>();
            int hitCount=0;

            driver.PlayBasicAttack(2f,()=>hitCount++);

            Assert.AreEqual(2f,driver.CurrentPlaybackSpeed,0.001f);
            Assert.AreEqual(2f,animator.speed,0.001f);
            Assert.IsTrue(driver.HasPendingBasicAttackHit);

            relay.OnBasicAttackHitFrame();
            relay.OnBasicAttackHitFrame();

            Assert.AreEqual(1,hitCount);
            Assert.IsFalse(driver.HasPendingBasicAttackHit);

            driver.InterruptCurrentAction();
            driver.PlaySkillAttack(1.5f,()=>hitCount+=10);
            Assert.AreEqual(1.5f,driver.CurrentPlaybackSpeed,0.001f);
            Assert.IsTrue(driver.HasPendingSkillAttackHit);
            Assert.That(
                driver.transform.localScale.x,
                Is.EqualTo(1.24f).Within(0.001f));

            relay.OnSkillAttackHitFrame();
            relay.OnSkillAttackHitFrame();

            Assert.AreEqual(11,hitCount);
            Assert.IsFalse(driver.HasPendingSkillAttackHit);

            driver.ResetForReuse();
            Assert.AreEqual(1f,driver.CurrentPlaybackSpeed,0.001f);
            Assert.AreEqual(1f,animator.speed,0.001f);
            Assert.That(
                driver.transform.localScale,
                Is.EqualTo(Vector3.one));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void SkillAttack_CanUseSlowWindupAndNormalRecovery()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            int hits=0;

            driver.PlaySkillAttack(0.2f,1f,()=>hits++);
            Assert.That(driver.CurrentPlaybackSpeed,Is.EqualTo(0.2f).Within(0.001f));

            driver.NotifySkillAttackHitFrameFromAnimation();

            Assert.That(hits,Is.EqualTo(1));
            Assert.That(driver.CurrentPlaybackSpeed,Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void SkillAttack_CanHoldThirdFrameUntilImpact()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/PixelMonsters/041_tino.prefab");
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            Assert.IsNotNull(driver.SkillWindupClip);
            Assert.IsNotNull(driver.SkillImpactClip);
            ObjectReferenceKeyframe[] windupKeys=AnimationUtility.GetObjectReferenceCurve(
                driver.SkillWindupClip,
                EditorCurveBinding.PPtrCurve(
                    string.Empty,typeof(SpriteRenderer),"m_Sprite"));
            Assert.That(windupKeys.Length,Is.EqualTo(3));
            Assert.That(windupKeys[2].value.name,Does.Contain("SkillAttack_02"));
            MethodInfo advance=typeof(SpriteMonsterAnimationDriver).GetMethod(
                "Advance",BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo lateUpdate=typeof(SpriteMonsterAnimationDriver).GetMethod(
                "LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            FieldInfo holdRemaining=typeof(SpriteMonsterAnimationDriver).GetField(
                "skillAttackHoldRemaining",
                BindingFlags.Instance|BindingFlags.NonPublic);
            int hits=0;
            driver.PlaySkillAttackWithHold(
                TyrannoCombatRules.TremorWindupFrames-1,
                TyrannoCombatRules.TremorCastSeconds,1f,()=>hits++);
            advance.Invoke(driver,new object[]{driver.SkillWindupClip.length});
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(2));
            Assert.That(driver.CurrentFrameName,Does.Contain("SkillAttack_02"));
            SpriteRenderer baseRenderer=driver.GetComponent<SpriteRenderer>();
            Assert.That(baseRenderer.enabled,Is.True);
            Assert.That(baseRenderer.sprite.name,Does.Contain("SkillAttack_02"));
            Assert.That(instance.GetComponent<Animator>().enabled,Is.False);
            Assert.That((float)holdRemaining.GetValue(driver),
                Is.EqualTo(TyrannoCombatRules.TremorCastSeconds-
                           driver.SkillWindupClip.length).Within(0.001f));
            Assert.That(driver.HasPendingSkillAttackHit,Is.True);
            Assert.That(hits,Is.Zero);

            advance.Invoke(driver,new object[]{
                TyrannoCombatRules.TremorCastSeconds-
                driver.SkillWindupClip.length-0.01f});
            driver.SetMoving(false);
            driver.InterruptCurrentAction();
            driver.PlayBasicAttack();
            driver.PlaySkillAttack();
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentMotionName,Is.EqualTo("SkillAttack"));
            Assert.That(driver.CurrentFrameName,Does.Contain("SkillAttack_02"));
            Assert.That(driver.HasPendingSkillAttackHit,Is.True);
            Assert.That(hits,Is.Zero);

            advance.Invoke(driver,new object[]{0.02f});
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentMotionName,Is.EqualTo("SkillAttack"));
            Assert.That(driver.CurrentFrameName,Does.Contain("SkillAttack_03"));
            Assert.That(driver.HasPendingSkillAttackHit,Is.False);
            Assert.That(hits,Is.EqualTo(1));
            Assert.That(baseRenderer.enabled,Is.True);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void EveryTyrannoSkillAttackEntryPointUsesThirdFrameHold()
    {
        string[] paths=
        {
            "Assets/Resources/Enemies/PixelMonsters/041_tino.prefab",
            "Assets/Resources/Enemies/PixelMonsters/042_magno.prefab",
            "Assets/Resources/Enemies/PixelMonsters/043_cheolno.prefab",
            "Assets/Resources/Enemies/PixelMonsters/044_bonrex.prefab"
        };
        MethodInfo advance=typeof(SpriteMonsterAnimationDriver).GetMethod(
            "Advance",BindingFlags.Instance|BindingFlags.NonPublic);
        MethodInfo lateUpdate=typeof(SpriteMonsterAnimationDriver).GetMethod(
            "LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(string path in paths)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                SpriteMonsterAnimationDriver driver=
                    instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
                int hits=0;
                driver.PlaySkillAttack(1f,()=>hits++);
                advance.Invoke(driver,new object[]{0.5f});
                lateUpdate.Invoke(driver,null);

                Assert.That(driver.CurrentFrameIndex,Is.EqualTo(2),path);
                Assert.That(driver.CurrentFrameName,Does.Contain("SkillAttack_02"),path);
                Assert.That(driver.IsActionLocked,Is.True,path);
                Assert.That(hits,Is.Zero,path);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void SpriteDeath_FreezesCurrentPoseAndLaunchesFragments(bool captureFailure)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/PixelMonsters/041_tino.prefab");
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        GameObject fragments=null;
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            SpriteRenderer renderer=driver.GetComponent<SpriteRenderer>();
            VoxelEnemyReaction reaction=instance.AddComponent<VoxelEnemyReaction>();
            reaction.Initialize();
            Sprite frozen=renderer.sprite;

            int bursts=0;
            if(captureFailure)
            {
                reaction.PlayCaptureHold();
                renderer.sortingOrder=30004;
                reaction.ResolveCaptureFailure(Vector3.right,()=>bursts++);
            }
            else reaction.PlayDeath(Vector3.right,()=>bursts++);
            fragments=GameObject.Find("Sprite Death Fragments");

            Assert.That(renderer.sprite,Is.SameAs(frozen));
            Assert.That(renderer.enabled,Is.True,"Keep the original pose crisp during the short hold.");
            Assert.That(fragments,Is.Not.Null);
            ParticleSystem pixels=fragments.GetComponent<ParticleSystem>();
            Assert.That(pixels,Is.Not.Null);
            Assert.That(pixels.main.maxParticles,Is.EqualTo(1264));
            var pixelRenderer=pixels.GetComponent<ParticleSystemRenderer>();
            Assert.That(pixelRenderer.sortingLayerID,Is.EqualTo(renderer.sortingLayerID));
            Assert.That(pixelRenderer.sortingOrder,Is.EqualTo(renderer.sortingOrder));
            Assert.That(pixelRenderer.sharedMaterial.renderQueue,
                Is.EqualTo(renderer.sharedMaterial.renderQueue),
                "Death pixels must share the sprite's transparent pass to remain above the capture shade.");
            Assert.That(pixels.particleCount,Is.Zero);
            int heldPixels=pixels.particleCount;
            Component effect=fragments.GetComponents<MonoBehaviour>()
                .Single(component=>component.GetType().Name=="SpriteDeathFragments");
            MethodInfo step=effect.GetType().GetMethod("Advance",
                BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(bursts,Is.Zero,"Sound must wait for the visible burst.");
            step.Invoke(effect,new object[]{0f});
            step.Invoke(effect,new object[]{.03f});
            Assert.That(bursts,Is.Zero);
            Assert.That(pixels.particleCount,Is.EqualTo(heldPixels),"No flying debris during the hold.");
            step.Invoke(effect,new object[]{.025f});
            Assert.That(bursts,Is.EqualTo(1));
            Assert.That(renderer.enabled,Is.False);
            Assert.That(pixels.particleCount,Is.GreaterThan(16));
            reaction.PlayDeath(Vector3.right,()=>bursts++);
            step.Invoke(effect,new object[]{.15f});
            Assert.That(bursts,Is.EqualTo(1),"No repeated burst or audio.");
            Assert.That(pixels.particleCount,Is.LessThanOrEqualTo(64),"Only brief debris remains.");
        }
        finally
        {
            if(fragments!=null)Object.DestroyImmediate(fragments);
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void Knockdown_PlaysDeathMotionWithoutBecomingTerminal()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            Assert.IsNotNull(driver.KnockdownClip);
            MethodInfo advance=typeof(SpriteMonsterAnimationDriver).GetMethod(
                "Advance",BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo lateUpdate=typeof(SpriteMonsterAnimationDriver).GetMethod(
                "LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            driver.PlayKnockdown(
                GameMasterData.Skill(TyrannoCombatRules.SkillId).statusDurationSeconds);
            FieldInfo elapsed=typeof(SpriteMonsterAnimationDriver).GetField(
                "motionElapsed",BindingFlags.Instance|BindingFlags.NonPublic);
            FieldInfo remaining=typeof(SpriteMonsterAnimationDriver).GetField(
                "knockdownRemaining",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int frame=0;frame<30;frame++)
                advance.Invoke(driver,new object[]{1f/120f});
            lateUpdate.Invoke(driver,null);

            Assert.That(driver.CurrentMotionName,Is.EqualTo("Death"));
            Assert.That(driver.CurrentFrameName,Does.Contain("Death_05"));
            SpriteRenderer baseRenderer=driver.GetComponent<SpriteRenderer>();
            Assert.That(baseRenderer.enabled,Is.True);
            Assert.That(baseRenderer.sprite.name,Does.Contain("Death_05"));
            Assert.That(instance.GetComponent<Animator>().enabled,Is.False);
            Assert.That((bool)typeof(SpriteMonsterAnimationDriver).GetField(
                "terminal",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(driver),
                Is.False);

            float timeLeft=(float)remaining.GetValue(driver);
            driver.SetKnockdownActive(timeLeft);
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentFrameName,Does.Contain("Death_05"));
            advance.Invoke(driver,new object[]{timeLeft-0.01f});
            lateUpdate.Invoke(driver,null);
            driver.SetMoving(false);
            driver.InterruptCurrentAction();
            driver.PlayBasicAttack();
            driver.PlaySkillAttack();
            Assert.That(driver.CurrentMotionName,Is.EqualTo("Death"));
            Assert.That(driver.CurrentFrameName,Does.Contain("Death_05"));
            Assert.That(instance.GetComponent<Animator>().enabled,Is.False);

            driver.SetKnockdownActive(0f);
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
            Assert.That(baseRenderer.enabled,Is.True);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void LockedFrames_AreIdenticalAcrossMultipleSpawnedInstances()
    {
        const string tinoPath=
            "Assets/Resources/Enemies/PixelMonsters/041_tino.prefab";
        const string velosPath=
            "Assets/Resources/Enemies/RaptorReferencePaletteV2Enemy.prefab";
        GameObject tinoPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(tinoPath);
        GameObject velosPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(velosPath);
        var instances=new System.Collections.Generic.List<GameObject>();
        MethodInfo advance=typeof(SpriteMonsterAnimationDriver).GetMethod(
            "Advance",BindingFlags.Instance|BindingFlags.NonPublic);
        MethodInfo lateUpdate=typeof(SpriteMonsterAnimationDriver).GetMethod(
            "LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        try
        {
            for(int i=0;i<4;i++)
            {
                GameObject instance=PrefabUtility.InstantiatePrefab(tinoPrefab) as GameObject;
                instances.Add(instance);
                SpriteMonsterAnimationDriver driver=
                    instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
                driver.PlaySkillAttackWithHold(2,1.5f,1f,null);
                advance.Invoke(driver,new object[]{driver.SkillWindupClip.length+1f});
                driver.PlayBasicAttack();
                driver.PlaySkillAttack();
                foreach(Animator animator in instance.GetComponentsInChildren<Animator>(true))
                    animator.enabled=true;
                lateUpdate.Invoke(driver,null);
                Assert.That(driver.CurrentMotionName,Is.EqualTo("SkillAttack"));
                Assert.That(driver.CurrentFrameName,Does.Contain("SkillAttack_02"));
                Assert.That(instance.GetComponentsInChildren<Animator>(true)
                    .All(animator=>!animator.enabled),Is.True);
            }
            for(int i=0;i<5;i++)
            {
                GameObject instance=PrefabUtility.InstantiatePrefab(velosPrefab) as GameObject;
                instances.Add(instance);
                SpriteMonsterAnimationDriver driver=
                    instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
                driver.PlayKnockdown(3f);
                advance.Invoke(driver,new object[]{driver.KnockdownClip.length+1f});
                driver.PlayBasicAttack();
                driver.PlaySkillAttack();
                foreach(Animator animator in instance.GetComponentsInChildren<Animator>(true))
                    animator.enabled=true;
                lateUpdate.Invoke(driver,null);
                Assert.That(driver.CurrentMotionName,Is.EqualTo("Death"));
                Assert.That(driver.CurrentFrameName,Does.Contain("Death_05"));
                Assert.That(instance.GetComponentsInChildren<Animator>(true)
                    .All(animator=>!animator.enabled),Is.True);
            }
        }
        finally
        {
            foreach(GameObject instance in instances)
                Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void EveryPixelMonsterKeepsKnockdownUnderCompetingActions()
    {
        MonsterDatabase database=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(
            "Assets/Resources/MonsterData/MonsterDatabase.asset");
        MethodInfo advance=typeof(SpriteMonsterAnimationDriver).GetMethod(
            "Advance",BindingFlags.Instance|BindingFlags.NonPublic);
        MethodInfo lateUpdate=typeof(SpriteMonsterAnimationDriver).GetMethod(
            "LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(MonsterDefinition definition in database.monsters)
        {
            int serial;
            if(definition==null||!int.TryParse(definition.serialNumber,out serial)||
               serial<1||serial>80||string.IsNullOrEmpty(definition.resourcePath))
                continue;
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/"+definition.resourcePath+".prefab");
            for(int copy=0;copy<3;copy++)
            {
                GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                try
                {
                    SpriteMonsterAnimationDriver driver=
                        instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
                    string label=definition.serialNumber+" copy "+copy;
                    Assert.IsNotNull(driver,label);
                    Assert.IsNotNull(driver.KnockdownClip,label);
                    if(copy==0)driver.PlayBasicAttack();
                    else if(copy==1)driver.PlaySkillAttack();
                    else driver.SetMoving(true);
                    driver.PlayKnockdown(1f);
                    advance.Invoke(driver,new object[]{driver.KnockdownClip.length});
                    driver.SetMoving(true);
                    driver.PlayBasicAttack();
                    driver.PlaySkillAttack();
                    driver.InterruptCurrentAction();
                    lateUpdate.Invoke(driver,null);
                    Assert.That(driver.CurrentMotionName,Is.EqualTo("Death"),label);
                    Assert.That(driver.CurrentFrameIndex,
                        Is.EqualTo(driver.DeathFrameCount-1),label);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }
    }

    [Test]
    public void PixelRaptorBillboardSwitchesFacingImmediatelyAndLocksDuringAttack()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=
            PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        GameObject cameraObject=new GameObject(
            "PixelRaptorFacingTestCamera",typeof(Camera));
        Assert.IsNotNull(instance);
        try
        {
            Camera camera=cameraObject.GetComponent<Camera>();
            SpriteMonsterBillboard billboard=
                instance.GetComponentInChildren<SpriteMonsterBillboard>(true);
            SpriteRenderer renderer=
                billboard.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(billboard);
            Assert.IsNotNull(renderer);

            typeof(SpriteMonsterBillboard).GetMethod(
                "Awake",BindingFlags.Instance|BindingFlags.NonPublic)
                ?.Invoke(billboard,null);
            typeof(SpriteMonsterBillboard).GetField(
                "cachedCamera",BindingFlags.Instance|BindingFlags.NonPublic)
                ?.SetValue(billboard,camera);

            billboard.SetFacingDirection(camera.transform.right);
            Assert.IsFalse(renderer.flipX);
            billboard.SetFacingDirection(-camera.transform.right);
            Assert.IsTrue(renderer.flipX);
            SpriteMonsterAnimationDriver driver=
                billboard.GetComponent<SpriteMonsterAnimationDriver>();
            driver.PlaySkillAttack();
            billboard.SetFacingDirection(camera.transform.right);
            Assert.IsTrue(renderer.flipX);
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void ResetForReuseAcceptsInactiveMonster()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=
            PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Assert.IsNotNull(instance);
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<
                    SpriteMonsterAnimationDriver>(true);
            instance.SetActive(false);

            driver.ResetForReuse();

            Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
            Assert.That(driver.CurrentPlaybackSpeed,Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void ExplicitHitFrameDispatchesPendingSkillHit()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Assert.IsNotNull(instance);
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            int hitCount=0;
            driver.ConfigureHitFrames(999,999);
            driver.PlaySkillAttack(1f,()=>hitCount++);
            typeof(SpriteMonsterAnimationDriver).GetField(
                    "motionElapsed",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .SetValue(driver,driver.SkillAttackHitTime);
            typeof(SpriteMonsterAnimationDriver).GetMethod(
                    "ApplyExplicitFrame",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(driver,null);
            Assert.AreEqual(1,hitCount);
            Assert.IsFalse(driver.HasPendingSkillAttackHit);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void ReverseSkillAttackPlaysFramesFromLastToFirst()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Assert.IsNotNull(instance);
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            FieldInfo elapsed=typeof(SpriteMonsterAnimationDriver).GetField(
                "motionElapsed",BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo apply=typeof(SpriteMonsterAnimationDriver).GetMethod(
                "ApplyExplicitFrame",BindingFlags.Instance|BindingFlags.NonPublic);

            driver.PlaySkillAttackReverse(1.7f);
            Assert.That(driver.CurrentFrameIndex,
                Is.EqualTo(driver.SkillAttackFrameCount-1));
            Assert.That(driver.CurrentPlaybackSpeed,Is.EqualTo(1.7f));

            elapsed.SetValue(driver,driver.SkillAttackDuration-0.0001f);
            apply.Invoke(driver,null);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void EndOfMotionDispatchesPendingHitBeforeFallback()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            int hits=0;
            driver.PlayBasicAttack(1f,()=>hits++);
            typeof(SpriteMonsterAnimationDriver).GetField(
                    "motionElapsed",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .SetValue(driver,driver.BasicAttackDuration);
            typeof(SpriteMonsterAnimationDriver).GetMethod(
                    "Update",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(driver,null);
            Assert.AreEqual(1,hits);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void InterruptedSkillAttackCannotApplyItsStoredHitLater()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            int hits=0;
            driver.PlaySkillAttack(1f,()=>hits++);
            driver.InterruptCurrentAction();
            driver.NotifySkillAttackHitFrameFromAnimation();

            Assert.AreEqual(0,hits);
            Assert.IsFalse(driver.HasPendingSkillAttackHit);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void FrozenDeathFrameStopsAnimationAndPendingHit()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            SpriteRenderer renderer=driver.GetComponent<SpriteRenderer>();
            int hits=0;
            driver.PlayBasicAttack(1f,()=>hits++);
            Sprite frozen=renderer.sprite;

            driver.FreezeCurrentFrame();
            driver.NotifyBasicAttackHitFrameFromAnimation();

            Assert.AreSame(frozen,renderer.sprite);
            Assert.AreEqual(0,hits);
            Assert.IsFalse(driver.HasPendingBasicAttackHit);
            Assert.AreEqual(0f,driver.GetComponentInParent<Animator>().speed);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void PixelHitReactionDoesNotMoveSpriteBackToItsLocalOrigin()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            UltraLowSDRaptorSetup.PrefabPaths[0]);
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            VoxelEnemyReaction reaction=instance.GetComponent<VoxelEnemyReaction>();
            if(reaction==null)reaction=instance.AddComponent<VoxelEnemyReaction>();
            reaction.PlayHit(Vector3.right);
            bool hitting=(bool)typeof(VoxelEnemyReaction).GetField(
                    "hitting",BindingFlags.Instance|BindingFlags.NonPublic)
                .GetValue(reaction);
            bool captured=(bool)typeof(VoxelEnemyReaction).GetField(
                    "hitPoseCaptured",BindingFlags.Instance|BindingFlags.NonPublic)
                .GetValue(reaction);
            Assert.That(hitting,Is.False);
            Assert.That(captured,Is.False);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void SpriteHitFlashDoesNotRestoreIdleTextureOverLockedPose()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Enemies/PixelMonsters/041_tino.prefab");
        GameObject instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        try
        {
            SpriteMonsterAnimationDriver driver=
                instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            VoxelEnemyReaction reaction=instance.AddComponent<VoxelEnemyReaction>();
            SpriteRenderer renderer=driver.GetComponent<SpriteRenderer>();
            MethodInfo advance=typeof(SpriteMonsterAnimationDriver).GetMethod(
                "Advance",BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo lateUpdate=typeof(SpriteMonsterAnimationDriver).GetMethod(
                "LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo updateFlash=typeof(VoxelEnemyReaction).GetMethod(
                "UpdateFlash",BindingFlags.Instance|BindingFlags.NonPublic);

            reaction.Initialize();
            driver.PlaySkillAttackWithHold(2,1.5f,1f,null);
            advance.Invoke(driver,new object[]{driver.SkillWindupClip.length+0.5f});
            lateUpdate.Invoke(driver,null);
            reaction.PlayHit(Vector3.right);
            updateFlash.Invoke(reaction,new object[]{1f});

            MaterialPropertyBlock block=new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(renderer.sprite.name,Does.Contain("SkillAttack_02"));
            Assert.That(block.GetTexture(Shader.PropertyToID("_MainTex")),
                Is.EqualTo(renderer.sprite.texture));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void SpriteOutlineUsesAlphaMaskAndTwoPixelWidth()
    {
        Shader mask=Shader.Find("Hidden/BombBrick/Boss Part Outline Mask");
        Shader composite=Shader.Find("Hidden/BombBrick/Boss Part Outline Composite");
        Assert.That(mask,Is.Not.Null);
        Assert.That(composite,Is.Not.Null);

        Material maskMaterial=new Material(mask);
        Material material=new Material(composite);
        try
        {
            Assert.That(maskMaterial.HasProperty("_MainTex"),Is.True);
            Assert.That(material.GetFloat("_OutlinePixels"),Is.EqualTo(2f));
        }
        finally
        {
            Object.DestroyImmediate(maskMaterial);
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void ItemTargetHoverUsesEmphasizedOutlineMask()
    {
        GameObject instance=new GameObject("Item Target Outline");
        SpriteRenderer renderer=instance.AddComponent<SpriteRenderer>();
        ScreenSpaceOutlineTarget target=instance.AddComponent<ScreenSpaceOutlineTarget>();
        MaterialPropertyBlock properties=new MaterialPropertyBlock();
        try
        {
            target.SetOutlined(true,false);
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_OutlineStrength"),Is.EqualTo(0.5f));
            int outlineLayer=LayerMask.NameToLayer("ScreenSpaceOutline");
            Assert.That(renderer.gameObject.layer,Is.EqualTo(outlineLayer));
            instance.SetActive(false);
            instance.SetActive(true);
            Assert.That(renderer.gameObject.layer,Is.EqualTo(outlineLayer));
            renderer.SetPropertyBlock(new MaterialPropertyBlock());
            renderer.gameObject.layer=0;
            typeof(ScreenSpaceOutlineTarget).GetMethod("LateUpdate",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_OutlineStrength"),Is.EqualTo(0.5f));
            Assert.That(renderer.gameObject.layer,Is.EqualTo(outlineLayer));
            target.SetOutlined(true,true);
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_OutlineStrength"),Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void OutlineEligibilityExcludesNormalCombatButKeepsItemDragAndDeploymentSelection()
    {
        Assert.That(MonsterAutoBattlePrototype.ShouldShowOutline(
            true,false,true,true,false,false,false),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldShowOutline(
            true,false,true,true,false,false,true),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldShowOutline(
            true,false,true,true,true,true,true),Is.True);
        Assert.That(MonsterAutoBattlePrototype.ShouldShowOutline(
            true,true,true,true,false,false,false),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldShowOutline(
            false,true,true,true,true,true,false),Is.True);
        Assert.That(MonsterAutoBattlePrototype.ShouldShowOutline(
            false,true,true,true,false,true,true),Is.False);
        Assert.That(MonsterAutoBattlePrototype.ShouldShowOutline(
            false,false,true,true,false,false,true),Is.True);
        Assert.That(MonsterAutoBattlePrototype.ShouldShowOutline(
            false,false,false,true,true,true,false),Is.False);
    }

    [Test]
    public void CrocodileAttackPivotsKeepFeetOnIdleGroundLine()
    {
        const string skillPath=
            "Assets/Art/Sprites/Characters/Velociraptor/013. 아거/013 아거_SkillAttack.png";
        Vector2 attackPivot=PixelSpriteIdleScaleNormalizer.CalculateFramePivot(
            "Assets/Art/Sprites/Characters/Velociraptor/013. 아거/013 아거_Attack.png",
            "Attack");
        Vector2 skillPivot=PixelSpriteIdleScaleNormalizer.CalculateFramePivot(
            skillPath,"SkillAttack");
        Sprite[] skillFrames=AssetDatabase.LoadAllAssetsAtPath(skillPath)
            .OfType<Sprite>().ToArray();

        Assert.That(attackPivot.y,Is.EqualTo(83f/256f).Within(0.0001f));
        Assert.That(skillPivot.y,Is.EqualTo(83f/256f).Within(0.0001f));
        Assert.That(skillFrames,Is.Not.Empty);
        Assert.That(skillFrames.All(frame=>
            Mathf.Abs(frame.pivot.y/frame.rect.height-skillPivot.y)<0.0001f),Is.True);
    }

}
