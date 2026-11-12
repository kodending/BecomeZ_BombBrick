using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

public static class PixelRaptorFamilySetup
{
    private sealed class RaptorSpec
    {
        public string serial;
        public string displayName;
        public string monsterId;
        public string definitionPath;
        public string prefabPath;
    }

    private sealed class MotionSpec
    {
        public string state;
        public int frameCount;
        public float framesPerSecond;
        public bool loop;
        public float idleRelativeScale;
    }

    public const string AnimationSetId="raptor_pixel_sprite_v1";
    public const string SourceRoot=
        "Assets/Art/Sprites/Characters/Velociraptor";
    public const string GeneratedRoot=
        "Assets/Resources/Enemies/PixelRaptors";
    public const string SharedAnimationFolder=
        GeneratedRoot+"/SharedAnimation";
    public const string ControllerPath=
        SharedAnimationFolder+"/PixelRaptor.overrideController";
    public const string BaseControllerPath=
        "Assets/Resources/Enemies/SpriteTestVelociraptor000/"+
        "SpriteTestVelociraptor000.controller";
    public const int BasicAttackHitFrameIndex=3;
    public const int SkillAttackHitFrameIndex=3;

    private const int FrameWidth=256;
    private const int FrameHeight=256;
    private const float PixelsPerUnit=96f;

    private static readonly RaptorSpec[] Raptors=
    {
        new RaptorSpec
        {
            serial="001",
            displayName="벨로스",
            monsterId="velos",
            definitionPath=
                "Assets/Resources/MonsterData/RaptorReferencePaletteV2.asset",
            prefabPath=
                "Assets/Resources/Enemies/RaptorReferencePaletteV2Enemy.prefab"
        },
        new RaptorSpec
        {
            serial="002",
            displayName="벨록",
            monsterId="velok",
            definitionPath="Assets/Resources/MonsterData/Velok.asset",
            prefabPath="Assets/Resources/Enemies/VelokEnemy.prefab"
        },
        new RaptorSpec
        {
            serial="003",
            displayName="라푸토",
            monsterId="raputo",
            definitionPath="Assets/Resources/MonsterData/Raputo.asset",
            prefabPath="Assets/Resources/Enemies/RaputoEnemy.prefab"
        },
        new RaptorSpec
        {
            serial="004",
            displayName="윌로",
            monsterId="wilo",
            definitionPath=
                "Assets/Resources/MonsterData/Monster_wilo.asset",
            prefabPath="Assets/Resources/Enemies/WiloEnemy.prefab"
        }
    };

    private static readonly MotionSpec[] Motions=
    {
        Motion("Idle",4,7f,true,1f),
        Motion("Run",6,11f,true,1.14f),
        Motion("Attack",6,12f,false,1.15f),
        Motion("SkillAttack",6,11f,false,1.24f),
        Motion("Death",6,8f,false,1.08f),
        Motion("Capture",6,12f,false,1.04f),
        Motion("Struggle",8,12f,true,1.15f)
    };

    [MenuItem("Tools/Monster Production/Build Pixel Raptors 001-004")]
    public static void BuildAll()
    {
        PixelSpriteIdleScaleNormalizer.ClearCaches();
        ValidateSourceSheets();
        EnsureFolder(GeneratedRoot);
        EnsureFolder(SharedAnimationFolder);

        foreach(RaptorSpec raptor in Raptors)
        {
            EnsureFolder(GeneratedFolder(raptor));
            EnsureFolder(FrameFolder(raptor));
            foreach(MotionSpec motion in Motions)
                ConfigureSourceSheet(raptor,motion);
            GenerateIndependentFrames(raptor);
        }

        Dictionary<string,AnimationClip> clips=
            CreateOrUpdateSharedClips();
        RuntimeAnimatorController controller=
            CreateOrUpdateController(clips);

        foreach(RaptorSpec raptor in Raptors)
        {
            Dictionary<string,Sprite[]> frames=LoadAllFrames(raptor);
            CreateOrUpdatePrefab(raptor,frames,controller);
            UpdateDefinitionPresentation(raptor);
        }

        AssetDatabase.SaveAssets();
        foreach(MotionSpec motion in Motions)
            AssetDatabase.ImportAsset(
                ClipPath(motion),
                ImportAssetOptions.ForceSynchronousImport|
                ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(
            ControllerPath,
            ImportAssetOptions.ForceSynchronousImport|
            ImportAssetOptions.ForceUpdate);
        foreach(RaptorSpec raptor in Raptors)
            AssetDatabase.ImportAsset(
                raptor.prefabPath,
                ImportAssetOptions.ForceSynchronousImport|
                ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        GameMasterData.ClearCache();
        ValidateAll();
        PixelSpecialAnimationClipSetup.BuildAll();
        Debug.Log(
            "[Pixel Raptors] 001-004 build complete: "+
            "Idle / Run / Attack / SkillAttack / Death / Capture / Struggle");
    }

    [MenuItem("Tools/Monster Production/Validate Pixel Raptors 001-004")]
    public static void ValidateAll()
    {
        ValidateSourceSheets();
        AnimatorOverrideController controller=
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                ControllerPath);
        if(controller==null)
            throw new InvalidOperationException(
                "Pixel raptor shared Animator Controller is missing.");
        AnimatorController baseController=
            AssetDatabase.LoadAssetAtPath<AnimatorController>(
                BaseControllerPath);
        if(baseController==null||
           controller.runtimeAnimatorController!=baseController)
            throw new InvalidOperationException(
                "Pixel raptor base Animator Controller is invalid.");
        string[] requiredStates=
        {
            "Idle","Run","Attack","SkillAttack",
            "Death","Capture","Struggle"
        };
        ChildAnimatorState[] states=
            baseController.layers[0].stateMachine.states;
        if(states.Length!=requiredStates.Length||
           requiredStates.Any(required=>states.All(child=>
               child.state.name!=required||
               child.state.motion==null))||
           controller.animationClips.Distinct().Count()!=
               requiredStates.Length)
            throw new InvalidOperationException(
                "Pixel raptor shared Animator states are incomplete.");

        ValidateSharedClips();
        foreach(RaptorSpec raptor in Raptors)
            ValidateRaptor(raptor,controller);
    }

    private static MotionSpec Motion(
        string state,
        int frameCount,
        float framesPerSecond,
        bool loop,
        float idleRelativeScale)
    {
        return new MotionSpec
        {
            state=state,
            frameCount=frameCount,
            framesPerSecond=framesPerSecond,
            loop=loop,
            idleRelativeScale=idleRelativeScale
        };
    }

    private static void ValidateSourceSheets()
    {
        foreach(RaptorSpec raptor in Raptors)
        foreach(MotionSpec motion in Motions)
        {
            string path=SourceSheetPath(raptor,motion);
            if(!File.Exists(Path.GetFullPath(path)))
                throw new FileNotFoundException(
                    raptor.serial+" "+motion.state+" source sheet is missing.",
                    path);
            Texture2D texture=new Texture2D(
                2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!ImageConversion.LoadImage(
                       texture,
                       File.ReadAllBytes(Path.GetFullPath(path)),
                       false)||
                   texture.width!=motion.frameCount*FrameWidth||
                   texture.height!=FrameHeight)
                    throw new InvalidOperationException(
                        raptor.serial+" "+motion.state+
                        " sheet must be "+
                        (motion.frameCount*FrameWidth)+"x"+
                        FrameHeight+".");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    private static void ConfigureSourceSheet(
        RaptorSpec raptor,
        MotionSpec motion)
    {
        string path=SourceSheetPath(raptor,motion);
        float pixelsPerUnit=
            PixelSpriteIdleScaleNormalizer.CalculatePixelsPerUnit(
                SourceFolder(raptor),
                motion.state,
                PixelsPerUnit);
        PixelSpriteIdleScaleNormalizer.ConfigureSheetImporter(
            path,
            motion.state,
            pixelsPerUnit,
            "Raptor"+raptor.serial+"_"+motion.state);
    }

    private static void GenerateIndependentFrames(RaptorSpec raptor)
    {
        foreach(MotionSpec motion in Motions)
        {
            string sourcePath=SourceSheetPath(raptor,motion);
            Texture2D source=new Texture2D(
                2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!ImageConversion.LoadImage(
                       source,
                       File.ReadAllBytes(Path.GetFullPath(sourcePath)),
                       false))
                    throw new InvalidOperationException(
                        "Could not decode Sprite sheet: "+sourcePath);
                if(source.width!=motion.frameCount*FrameWidth||
                   source.height!=FrameHeight)
                    throw new InvalidOperationException(
                        raptor.serial+" "+motion.state+
                        " dimensions changed during extraction.");

                for(int index=0;index<motion.frameCount;index++)
                {
                    Texture2D frame=new Texture2D(
                        FrameWidth,
                        FrameHeight,
                        TextureFormat.RGBA32,
                        false);
                    try
                    {
                        frame.SetPixels(source.GetPixels(
                            index*FrameWidth,
                            0,
                            FrameWidth,
                            FrameHeight));
                        frame.Apply(false,false);
                        File.WriteAllBytes(
                            Path.GetFullPath(
                                IndependentFramePath(
                                    raptor,motion,index)),
                            frame.EncodeToPNG());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(frame);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(MotionSpec motion in Motions)
        {
            float pixelsPerUnit=
                PixelSpriteIdleScaleNormalizer.CalculatePixelsPerUnit(
                    SourceFolder(raptor),
                    motion.state,
                    PixelsPerUnit);
            for(int index=0;index<motion.frameCount;index++)
                ConfigureIndependentFrame(
                    IndependentFramePath(raptor,motion,index),
                    pixelsPerUnit);
        }
    }

    private static void ConfigureIndependentFrame(
        string assetPath,
        float pixelsPerUnit)
    {
        TextureImporter importer=
            AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if(importer==null)
            throw new InvalidOperationException(
                "Generated frame importer is missing: "+assetPath);

        TextureImporterSettings settings=new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType=TextureImporterType.Sprite;
        settings.spriteMode=(int)SpriteImportMode.Single;
        settings.spriteMeshType=SpriteMeshType.FullRect;
        settings.spriteAlignment=(int)SpriteAlignment.Custom;
        settings.spritePivot=new Vector2(
            0.5f,
            PixelSpriteIdleScaleNormalizer.GroundLineY/(float)FrameHeight);
        settings.spritePixelsPerUnit=pixelsPerUnit;
        settings.npotScale=TextureImporterNPOTScale.None;
        settings.mipmapEnabled=false;
        settings.alphaIsTransparency=true;
        settings.filterMode=FilterMode.Point;
        settings.wrapMode=TextureWrapMode.Clamp;
        settings.readable=false;
        importer.SetTextureSettings(settings);

        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=pixelsPerUnit;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=FrameWidth;
        importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;
        importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.isReadable=false;
        importer.SaveAndReimport();
    }

    private static Dictionary<string,Sprite[]> LoadAllFrames(
        RaptorSpec raptor)
    {
        Dictionary<string,Sprite[]> result=
            new Dictionary<string,Sprite[]>(StringComparer.Ordinal);
        foreach(MotionSpec motion in Motions)
        {
            Sprite[] frames=Enumerable.Range(0,motion.frameCount)
                .Select(index=>AssetDatabase.LoadAssetAtPath<Sprite>(
                    IndependentFramePath(raptor,motion,index)))
                .ToArray();
            if(frames.Any(frame=>frame==null))
                throw new InvalidOperationException(
                    raptor.serial+" "+motion.state+
                    " generated frame load failed.");
            result.Add(motion.state,frames);
        }
        return result;
    }

    private static Dictionary<string,AnimationClip>
        CreateOrUpdateSharedClips()
    {
        Dictionary<string,AnimationClip> clips=
            new Dictionary<string,AnimationClip>(StringComparer.Ordinal);
        foreach(MotionSpec motion in Motions)
        {
            string path=ClipPath(motion);
            AnimationClip clip=
                AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null)
            {
                clip=new AnimationClip
                {
                    name="PixelRaptor_"+motion.state
                };
                AssetDatabase.CreateAsset(clip,path);
            }

            clip.ClearCurves();
            clip.frameRate=motion.framesPerSecond;
            float duration=motion.frameCount/motion.framesPerSecond;
            SetConstantScaleCurve(
                clip,"m_LocalScale.x",
                motion.idleRelativeScale,duration);
            SetConstantScaleCurve(
                clip,"m_LocalScale.y",
                motion.idleRelativeScale,duration);
            SetConstantScaleCurve(
                clip,"m_LocalScale.z",
                motion.idleRelativeScale,duration);

            AnimationEvent[] events=Array.Empty<AnimationEvent>();
            if(motion.state=="Attack")
                events=new[]
                {
                    new AnimationEvent
                    {
                        functionName=
                            SpriteMonsterAnimationEventRelay
                                .BasicAttackHitEventName,
                        time=BasicAttackHitFrameIndex/
                             motion.framesPerSecond
                    }
                };
            else if(motion.state=="SkillAttack")
                events=new[]
                {
                    new AnimationEvent
                    {
                        functionName=
                            SpriteMonsterAnimationEventRelay
                                .SkillAttackHitEventName,
                        time=SkillAttackHitFrameIndex/
                             motion.framesPerSecond
                    }
                };
            AnimationUtility.SetAnimationEvents(clip,events);
            SetLoopTime(clip,motion.loop);
            EditorUtility.SetDirty(clip);
            clips.Add(motion.state,clip);
        }
        return clips;
    }

    private static RuntimeAnimatorController CreateOrUpdateController(
        IReadOnlyDictionary<string,AnimationClip> clips)
    {
        RuntimeAnimatorController baseController=
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                BaseControllerPath);
        if(baseController==null)
            throw new InvalidOperationException(
                "Sprite monster base Animator Controller is missing.");
        AnimatorOverrideController controller=
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                ControllerPath);
        if(controller==null)
        {
            controller=new AnimatorOverrideController(baseController)
            {
                name="PixelRaptor"
            };
            AssetDatabase.CreateAsset(controller,ControllerPath);
        }
        controller.runtimeAnimatorController=baseController;
        List<KeyValuePair<AnimationClip,AnimationClip>> overrides=
            new List<KeyValuePair<AnimationClip,AnimationClip>>();
        controller.GetOverrides(overrides);
        for(int index=0;index<overrides.Count;index++)
        {
            AnimationClip source=overrides[index].Key;
            if(source==null)continue;
            MotionSpec motion=Motions.FirstOrDefault(candidate=>
                source.name.EndsWith(
                    "_"+candidate.state,
                    StringComparison.OrdinalIgnoreCase));
            if(motion==null)
                throw new InvalidOperationException(
                    "Unknown Sprite Animator motion: "+source.name);
            overrides[index]=
                new KeyValuePair<AnimationClip,AnimationClip>(
                    source,
                    clips[motion.state]);
        }
        controller.ApplyOverrides(overrides);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void CreateOrUpdatePrefab(
        RaptorSpec raptor,
        IReadOnlyDictionary<string,Sprite[]> frames,
        RuntimeAnimatorController controller)
    {
        GameObject root=new GameObject(
            Path.GetFileNameWithoutExtension(raptor.prefabPath));
        try
        {
            root.transform.localScale=
                Vector3.one*PixelSpriteIdleScaleNormalizer.BaseMonsterScale;
            Animator animator=root.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller;
            animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode=AnimatorUpdateMode.Normal;

            GameObject visual=new GameObject("Visual");
            visual.transform.SetParent(root.transform,false);
            visual.transform.localPosition=Vector3.zero;

            SpriteRenderer renderer=visual.AddComponent<SpriteRenderer>();
            renderer.sprite=frames["Idle"][0];
            renderer.color=Color.white;
            renderer.sortingOrder=10;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            visual.AddComponent<SpriteMonsterBillboard>();
            SpriteMonsterAnimationDriver driver=
                visual.AddComponent<SpriteMonsterAnimationDriver>();
            driver.Configure(
                frames["Idle"],
                frames["Run"],
                frames["Attack"],
                frames["SkillAttack"],
                frames["Death"],
                frames["Capture"],
                frames["Struggle"]);
            driver.ConfigureHitFrames(
                BasicAttackHitFrameIndex,
                SkillAttackHitFrameIndex);

            SpriteMonsterAnimationEventRelay relay=
                root.AddComponent<SpriteMonsterAnimationEventRelay>();
            relay.Bind(driver);

            if(PrefabUtility.SaveAsPrefabAsset(root,raptor.prefabPath)==null)
                throw new InvalidOperationException(
                    "Could not save pixel raptor prefab: "+
                    raptor.prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void UpdateDefinitionPresentation(RaptorSpec raptor)
    {
        MonsterDefinition definition=
            AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                raptor.definitionPath);
        if(definition==null||
           definition.monsterId!=raptor.monsterId||
           definition.serialNumber!=raptor.serial||
           definition.displayName!=raptor.displayName||
           definition.familyId!="raptor"||
           definition.skillId!="raptor_hunting_leap")
            throw new InvalidOperationException(
                raptor.serial+" gameplay identity is invalid.");

        definition.resourcePath=
            "Enemies/"+
            Path.GetFileNameWithoutExtension(raptor.prefabPath);
        definition.animationSetId=AnimationSetId;
        definition.visualScale=1f;
        definition.modelHeight=1.38f;
        definition.groundSpanTileRatio=0.78f;
        definition.collisionRadius=0.56f;
        definition.dataVersion=Mathf.Max(definition.dataVersion,5);
        EditorUtility.SetDirty(definition);
    }

    private static void ValidateSharedClips()
    {
        foreach(MotionSpec motion in Motions)
        {
            AnimationClip clip=
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    ClipPath(motion));
            if(clip==null||clip.isLooping!=motion.loop)
                throw new InvalidOperationException(
                    motion.state+" shared clip is invalid.");
            ObjectReferenceKeyframe[] spriteKeys=
                AnimationUtility.GetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        "Visual",typeof(SpriteRenderer),"m_Sprite"));
            if(spriteKeys!=null&&spriteKeys.Length>0)
                throw new InvalidOperationException(
                    motion.state+
                    " clip must not animate the SpriteRenderer.");

            AnimationEvent[] events=
                AnimationUtility.GetAnimationEvents(clip);
            if(motion.state=="Attack")
                ValidateEvent(
                    motion,events,
                    SpriteMonsterAnimationEventRelay
                        .BasicAttackHitEventName,
                    BasicAttackHitFrameIndex);
            else if(motion.state=="SkillAttack")
                ValidateEvent(
                    motion,events,
                    SpriteMonsterAnimationEventRelay
                        .SkillAttackHitEventName,
                    SkillAttackHitFrameIndex);
            else if(events.Length!=0)
                throw new InvalidOperationException(
                    motion.state+" contains an unexpected Animation Event.");
        }
    }

    private static void ValidateEvent(
        MotionSpec motion,
        AnimationEvent[] events,
        string functionName,
        int frameIndex)
    {
        float expectedTime=frameIndex/motion.framesPerSecond;
        if(events.Length!=1||
           events[0].functionName!=functionName||
           Mathf.Abs(events[0].time-expectedTime)>0.001f)
            throw new InvalidOperationException(
                motion.state+" hit Animation Event is invalid.");
    }

    private static void ValidateRaptor(
        RaptorSpec raptor,
        RuntimeAnimatorController controller)
    {
        Dictionary<string,Sprite[]> frames=LoadAllFrames(raptor);
        foreach(MotionSpec motion in Motions)
        {
            Sprite[] stateFrames=frames[motion.state];
            float expectedPixelsPerUnit=
                PixelSpriteIdleScaleNormalizer.CalculatePixelsPerUnit(
                    SourceFolder(raptor),
                    motion.state,
                    PixelsPerUnit);
            if(stateFrames.Length!=motion.frameCount||
               stateFrames.Select(frame=>frame.texture)
                   .Distinct().Count()!=motion.frameCount)
                throw new InvalidOperationException(
                    raptor.serial+" "+motion.state+
                    " runtime frames are not independent.");
            foreach(Sprite frame in stateFrames)
            {
                if(frame.texture.width!=FrameWidth||
                   frame.texture.height!=FrameHeight||
                   frame.vertices.Length!=4||
                   !Mathf.Approximately(frame.textureRect.x,0f)||
                   !Mathf.Approximately(frame.textureRect.y,0f)||
                   Mathf.Abs(
                       frame.pivot.y/frame.rect.height-
                       PixelSpriteIdleScaleNormalizer.GroundLineY/
                       (float)FrameHeight)>0.001f||
                   Mathf.Abs(
                       frame.pixelsPerUnit-
                       expectedPixelsPerUnit)>0.01f)
                    throw new InvalidOperationException(
                        raptor.serial+" "+motion.state+
                        " contains an invalid runtime frame.");
            }
        }

        GameObject prefab=
            AssetDatabase.LoadAssetAtPath<GameObject>(raptor.prefabPath);
        if(prefab==null)
            throw new InvalidOperationException(
                raptor.serial+" pixel prefab is missing.");
        Transform visual=prefab.transform.Find("Visual");
        SpriteMonsterAnimationDriver driver=
            visual==null
                ?null
                :visual.GetComponent<SpriteMonsterAnimationDriver>();
        SpriteMonsterAnimationEventRelay relay=
            prefab.GetComponent<SpriteMonsterAnimationEventRelay>();
        Animator animator=prefab.GetComponent<Animator>();
        if(visual==null||
           Vector3.Distance(
               prefab.transform.localScale,
               Vector3.one*PixelSpriteIdleScaleNormalizer.BaseMonsterScale)>
               0.001f||
           visual.localPosition.sqrMagnitude>0.000001f||
           visual.GetComponent<SpriteRenderer>()==null||
           visual.GetComponent<SpriteMonsterBillboard>()==null||
           driver==null||
           relay==null||
           relay.Driver!=driver||
           animator==null||
           animator.runtimeAnimatorController!=controller||
           prefab.GetComponentsInChildren<MeshRenderer>(true).Length!=0||
           prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length!=0)
            throw new InvalidOperationException(
                raptor.serial+" pixel prefab components are invalid.");
        if(driver.IdleFrameCount!=4||
           driver.RunFrameCount!=6||
           driver.AttackFrameCount!=6||
           driver.SkillAttackFrameCount!=6||
           driver.DeathFrameCount!=6||
           driver.CaptureFrameCount!=6||
           driver.StruggleFrameCount!=8)
            throw new InvalidOperationException(
                raptor.serial+" pixel frame counts are invalid.");

        MonsterDefinition definition=
            AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                raptor.definitionPath);
        if(definition==null||
           definition.monsterId!=raptor.monsterId||
           definition.serialNumber!=raptor.serial||
           definition.displayName!=raptor.displayName||
           definition.familyId!="raptor"||
           definition.skillId!="raptor_hunting_leap"||
           definition.animationSetId!=AnimationSetId||
           definition.resourcePath!=
               "Enemies/"+
               Path.GetFileNameWithoutExtension(raptor.prefabPath)||
           Resources.Load<GameObject>(definition.resourcePath)==null)
            throw new InvalidOperationException(
                raptor.serial+" pixel presentation data is invalid.");
    }

    private static AnimatorState AddState(
        AnimatorStateMachine machine,
        string name,
        Motion motion,
        Vector3 position)
    {
        AnimatorState state=machine.AddState(name,position);
        state.motion=motion;
        state.speed=1f;
        return state;
    }

    private static void AddBoolTransition(
        AnimatorState source,
        AnimatorState destination,
        string parameter,
        bool value)
    {
        AnimatorStateTransition transition=source.AddTransition(destination);
        ConfigureImmediateTransition(transition);
        transition.AddCondition(
            value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,
            0f,parameter);
    }

    private static void AddStruggleExit(
        AnimatorState source,
        AnimatorState destination,
        bool moving)
    {
        AnimatorStateTransition transition=source.AddTransition(destination);
        ConfigureImmediateTransition(transition);
        transition.AddCondition(
            AnimatorConditionMode.IfNot,0f,
            SpriteMonsterAnimationDriver.StrugglingParameter);
        transition.AddCondition(
            moving?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,
            0f,
            SpriteMonsterAnimationDriver.MovingParameter);
    }

    private static void AddOneShotExits(
        AnimatorState source,
        AnimatorState idle,
        AnimatorState run)
    {
        AnimatorStateTransition toIdle=source.AddTransition(idle);
        ConfigureExitTransition(toIdle);
        toIdle.AddCondition(
            AnimatorConditionMode.IfNot,0f,
            SpriteMonsterAnimationDriver.MovingParameter);
        AnimatorStateTransition toRun=source.AddTransition(run);
        ConfigureExitTransition(toRun);
        toRun.AddCondition(
            AnimatorConditionMode.If,0f,
            SpriteMonsterAnimationDriver.MovingParameter);
    }

    private static void AddAnyTrigger(
        AnimatorStateMachine machine,
        AnimatorState destination,
        string parameter)
    {
        AnimatorStateTransition transition=
            machine.AddAnyStateTransition(destination);
        ConfigureImmediateTransition(transition);
        transition.canTransitionToSelf=false;
        transition.AddCondition(AnimatorConditionMode.If,0f,parameter);
    }

    private static void AddAnyBool(
        AnimatorStateMachine machine,
        AnimatorState destination,
        string parameter,
        bool value)
    {
        AnimatorStateTransition transition=
            machine.AddAnyStateTransition(destination);
        ConfigureImmediateTransition(transition);
        transition.canTransitionToSelf=false;
        transition.AddCondition(
            value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,
            0f,parameter);
    }

    private static void ConfigureImmediateTransition(
        AnimatorStateTransition transition)
    {
        transition.hasExitTime=false;
        transition.hasFixedDuration=true;
        transition.duration=0f;
        transition.offset=0f;
    }

    private static void ConfigureExitTransition(
        AnimatorStateTransition transition)
    {
        transition.hasExitTime=true;
        transition.exitTime=1f;
        transition.hasFixedDuration=true;
        transition.duration=0f;
        transition.offset=0f;
    }

    private static void SetConstantScaleCurve(
        AnimationClip clip,
        string property,
        float value,
        float duration)
    {
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(
                "Visual",typeof(Transform),property),
            AnimationCurve.Constant(0f,duration,value));
    }

    private static void SetLoopTime(AnimationClip clip,bool loop)
    {
        SerializedObject serializedClip=new SerializedObject(clip);
        SerializedProperty settings=
            serializedClip.FindProperty("m_AnimationClipSettings");
        if(settings==null)return;
        SerializedProperty loopTime=
            settings.FindPropertyRelative("m_LoopTime");
        if(loopTime!=null)loopTime.boolValue=loop;
        SerializedProperty loopBlend=
            settings.FindPropertyRelative("m_LoopBlend");
        if(loopBlend!=null)loopBlend.boolValue=false;
        serializedClip.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string SourceFolder(RaptorSpec raptor)
    {
        return SourceRoot+"/"+raptor.serial+". "+raptor.displayName;
    }

    private static string SourceSheetPath(
        RaptorSpec raptor,
        MotionSpec motion)
    {
        return SourceFolder(raptor)+"/"+
               raptor.serial+" "+raptor.displayName+
               "_"+motion.state+".png";
    }

    private static string GeneratedFolder(RaptorSpec raptor)
    {
        return GeneratedRoot+"/"+raptor.serial+"_"+raptor.monsterId;
    }

    private static string FrameFolder(RaptorSpec raptor)
    {
        return GeneratedFolder(raptor)+"/Frames";
    }

    private static string FrameName(
        RaptorSpec raptor,
        MotionSpec motion,
        int index)
    {
        return "Raptor"+raptor.serial+"_"+motion.state+
               "_"+index.ToString("00");
    }

    private static string IndependentFramePath(
        RaptorSpec raptor,
        MotionSpec motion,
        int index)
    {
        return FrameFolder(raptor)+"/"+
               FrameName(raptor,motion,index)+".png";
    }

    private static string ClipPath(MotionSpec motion)
    {
        return SharedAnimationFolder+
               "/PixelRaptor_"+motion.state+".anim";
    }

    private static void EnsureFolder(string path)
    {
        string current="Assets";
        foreach(string part in path.Split('/').Skip(1))
        {
            string next=current+"/"+part;
            if(!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current,part);
            current=next;
        }
    }
}
