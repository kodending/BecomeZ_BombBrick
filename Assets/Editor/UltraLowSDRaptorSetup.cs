using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class UltraLowSDRaptorSetup
{
    public const string AnimationSetId="raptor_ultralowsd";
    public const string VelosMonsterId="raptor_reference_palette_v2";
    public const string VelosDefinitionPath=
        "Assets/Resources/MonsterData/RaptorReferencePaletteV2.asset";
    public const string ModelDirectory="Assets/Art/Models/RaptorPrototypes/UltraLowSD";
    public const string SharedAnimationDirectory=
        "Assets/Resources/Enemies/UltraLowSDRaptorAnimation";
    public const string IdleClipPath=SharedAnimationDirectory+"/UltraLowSD_Raptor_Idle.anim";
    public const string MoveClipPath=SharedAnimationDirectory+"/UltraLowSD_Raptor_Move.anim";
    public const string ControllerPath=SharedAnimationDirectory+"/UltraLowSD_Raptor.controller";

    public static readonly string[] ModelPaths=
    {
        ModelDirectory+"/Raptor_UltraLowSD_V1_Rig_Rebuilt_Eyebrows.fbx",
        ModelDirectory+"/DemonRaptor_UltraLowSD_V2_Overlord_Rigged.fbx",
        ModelDirectory+"/DesertRaptor_UltraLowSD_V1_SunScorpion_Rigged.fbx",
        ModelDirectory+"/IceRaptor_UltraLowSD_V4_CrackedShell_Rigged.fbx"
    };

    public static readonly string[] PrefabPaths=
    {
        "Assets/Resources/Enemies/RaptorReferencePaletteV2Enemy.prefab",
        "Assets/Resources/Enemies/VelokEnemy.prefab",
        "Assets/Resources/Enemies/RaputoEnemy.prefab",
        "Assets/Resources/Enemies/WiloEnemy.prefab"
    };

    public static readonly string[] DefinitionPaths=
    {
        "Assets/Resources/MonsterData/RaptorReferencePaletteV2.asset",
        "Assets/Resources/MonsterData/Velok.asset",
        "Assets/Resources/MonsterData/Raputo.asset",
        "Assets/Resources/MonsterData/Monster_wilo.asset"
    };

    public static readonly string[] MonsterIds=
    {
        "raptor_reference_palette_v2","velok","raputo","wilo"
    };

    [MenuItem("Tools/Monster Production/Build Raptors 001-004")]
    public static void BuildAll()
    {
        PixelRaptorFamilySetup.BuildAll();
    }

    [MenuItem("Tools/Monster Production/Restore Raptors 001-004 UltraLowSD")]
    public static void RestoreAllRaptors()
    {
        ValidateSourceModels();
        for(int i=0;i<ModelPaths.Length;i++)
            ConfigureImporter(ModelPaths[i]);
        EnsureFolder(SharedAnimationDirectory);
        GameObject canonical=AssetDatabase.LoadAssetAtPath<GameObject>(
            ModelPaths[0]);
        AnimationClip idle=CreateOrUpdateIdle(canonical);
        AnimationClip move=CreateOrUpdateMove(canonical);
        RuntimeAnimatorController controller=
            CreateOrUpdateController(idle,move);

        for(int i=0;i<ModelPaths.Length;i++)
        {
            BuildCombatPrefab(ModelPaths[i],PrefabPaths[i],controller);
            RestoreDefinition(i);
        }

        AssetDatabase.SaveAssets();
        for(int i=0;i<PrefabPaths.Length;i++)
            AssetDatabase.ImportAsset(
                PrefabPaths[i],
                ImportAssetOptions.ForceSynchronousImport|
                ImportAssetOptions.ForceUpdate);
        ValidateAllRaptors();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            "[Raptors] 001-004 restored to UltraLowSD models.");
    }

    [MenuItem("Tools/Monster Production/Restore 001 Velos UltraLowSD")]
    public static void RestoreVelos()
    {
        ConfigureImporter(ModelPaths[0]);
        EnsureFolder(SharedAnimationDirectory);
        GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(
            ModelPaths[0]);
        if(model==null)
            throw new InvalidOperationException(
                "001 Velos UltraLowSD source FBX is missing.");
        AnimationClip idle=CreateOrUpdateIdle(model);
        AnimationClip move=CreateOrUpdateMove(model);
        RuntimeAnimatorController controller=
            CreateOrUpdateController(idle,move);
        BuildCombatPrefab(ModelPaths[0],PrefabPaths[0],controller);

        RestoreDefinition(0);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            PrefabPaths[0],
            ImportAssetOptions.ForceSynchronousImport|
            ImportAssetOptions.ForceUpdate);
        ValidateVelos();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            "[Raptors] 001 Velos restored to UltraLowSD model.");
    }

    public static void ValidateVelos()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            PrefabPaths[0]);
        MonsterDefinition definition=
            AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                VelosDefinitionPath);
        RuntimeAnimatorController controller=
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                ControllerPath);
        if(prefab==null||
           prefab.transform.Find("Visual/Root/Body/Neck/Head")==null)
            throw new InvalidOperationException(
                "001 Velos UltraLowSD combat rig is invalid.");
        Animator animator=prefab.GetComponentInChildren<Animator>(true);
        if(animator==null||
           animator.runtimeAnimatorController!=controller||
           animator.cullingMode!=AnimatorCullingMode.AlwaysAnimate)
            throw new InvalidOperationException(
                "001 Velos UltraLowSD Animator is invalid.");
        RaptorTurnAnticipation anticipation=
            prefab.GetComponent<RaptorTurnAnticipation>();
        if(anticipation==null)
            throw new InvalidOperationException(
                "001 Velos turn anticipation is missing.");
        anticipation.Initialize();
        if(!anticipation.HasTurnChain||
           anticipation.RigProfile!="UltraLowSD")
            throw new InvalidOperationException(
                "001 Velos UltraLowSD turn chain is invalid.");
        if(definition==null||
           definition.animationSetId!=AnimationSetId||
           !Mathf.Approximately(definition.visualScale,1f)||
           !Mathf.Approximately(definition.modelHeight,1.5f)||
           !Mathf.Approximately(
               definition.groundSpanTileRatio,1f))
            throw new InvalidOperationException(
                "001 Velos presentation data is invalid.");
    }

    public static void ValidateAllRaptors()
    {
        RuntimeAnimatorController controller=
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                ControllerPath);
        for(int i=0;i<PrefabPaths.Length;i++)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPaths[i]);
            MonsterDefinition definition=
                AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                    DefinitionPaths[i]);
            string serial=(i+1).ToString("000");
            if(prefab==null||
               prefab.transform.Find("Visual/Root/Body/Neck/Head")==null)
                throw new InvalidOperationException(
                    serial+" UltraLowSD combat rig is invalid.");
            Animator animator=prefab.GetComponentInChildren<Animator>(true);
            if(animator==null||
               animator.runtimeAnimatorController!=controller||
               animator.cullingMode!=AnimatorCullingMode.AlwaysAnimate)
                throw new InvalidOperationException(
                    serial+" UltraLowSD Animator is invalid.");
            RaptorTurnAnticipation anticipation=
                prefab.GetComponent<RaptorTurnAnticipation>();
            if(anticipation==null)
                throw new InvalidOperationException(
                    serial+" turn anticipation is missing.");
            anticipation.Initialize();
            if(!anticipation.HasTurnChain||
               anticipation.RigProfile!="UltraLowSD")
                throw new InvalidOperationException(
                    serial+" UltraLowSD turn chain is invalid.");
            if(definition==null||
               definition.monsterId!=MonsterIds[i]||
               definition.serialNumber!=serial||
               definition.familyId!="raptor"||
               definition.resourcePath!=
                   "Enemies/"+Path.GetFileNameWithoutExtension(
                       PrefabPaths[i])||
               definition.animationSetId!=AnimationSetId||
               !Mathf.Approximately(definition.visualScale,1f)||
               !Mathf.Approximately(definition.modelHeight,1.5f)||
               !Mathf.Approximately(
                   definition.groundSpanTileRatio,1f))
                throw new InvalidOperationException(
                    serial+" presentation data is invalid.");
        }
    }

    private static void RestoreDefinition(int index)
    {
        MonsterDefinition definition=
            AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                DefinitionPaths[index]);
        string serial=(index+1).ToString("000");
        if(definition==null||
           definition.monsterId!=MonsterIds[index]||
           definition.serialNumber!=serial||
           definition.familyId!="raptor"||
           definition.skillId!="raptor_hunting_leap")
            throw new InvalidOperationException(
                serial+" gameplay identity is invalid.");
        definition.resourcePath=
            "Enemies/"+Path.GetFileNameWithoutExtension(
                PrefabPaths[index]);
        definition.animationSetId=AnimationSetId;
        definition.visualScale=1f;
        definition.modelHeight=1.5f;
        definition.groundSpanTileRatio=1f;
        EditorUtility.SetDirty(definition);
    }

    private static void ValidateSourceModels()
    {
        for(int i=0;i<ModelPaths.Length;i++)
            if(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPaths[i])==null)
                throw new InvalidOperationException("UltraLowSD source FBX is missing: "+ModelPaths[i]);

        Dictionary<string,Transform> expected=RigMap(
            AssetDatabase.LoadAssetAtPath<GameObject>(ModelPaths[0]).transform);
        for(int i=1;i<ModelPaths.Length;i++)
        {
            Dictionary<string,Transform> actual=RigMap(
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPaths[i]).transform);
            if(!expected.Keys.SequenceEqual(actual.Keys))
                throw new InvalidOperationException(
                    "UltraLowSD rig hierarchy mismatch: "+ModelPaths[i]);
            foreach(string path in expected.Keys)
            {
                Transform left=expected[path];
                Transform right=actual[path];
                if(Vector3.Distance(left.localPosition,right.localPosition)>0.0001f||
                   Quaternion.Angle(left.localRotation,right.localRotation)>0.001f||
                   Vector3.Distance(left.localScale,right.localScale)>0.0001f)
                    throw new InvalidOperationException(
                        "UltraLowSD rig rest pose mismatch: "+ModelPaths[i]+" / "+path);
            }
        }
    }

    private static Dictionary<string,Transform> RigMap(Transform modelRoot)
    {
        Transform rigRoot=modelRoot.Find("Root");
        if(rigRoot==null)throw new InvalidOperationException(
            "UltraLowSD model must contain Root: "+modelRoot.name);
        HashSet<Transform> rendered=new HashSet<Transform>(
            modelRoot.GetComponentsInChildren<Renderer>(true).Select(renderer=>renderer.transform));
        return rigRoot.GetComponentsInChildren<Transform>(true)
            .Where(item=>!rendered.Contains(item))
            .ToDictionary(item=>RelativePath(modelRoot,item),item=>item,StringComparer.Ordinal);
    }

    private static void ConfigureImporter(string path)
    {
        ModelImporter importer=AssetImporter.GetAtPath(path) as ModelImporter;
        if(importer==null)throw new InvalidOperationException("ModelImporter missing: "+path);
        bool changed=importer.animationType!=ModelImporterAnimationType.Generic||
                     importer.avatarSetup!=ModelImporterAvatarSetup.NoAvatar||
                     importer.importAnimation||importer.optimizeGameObjects||
                     importer.importCameras||importer.importLights;
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
        importer.importAnimation=false;
        importer.optimizeGameObjects=false;
        importer.importCameras=false;
        importer.importLights=false;
        if(changed)importer.SaveAndReimport();
    }

    private static AnimationClip CreateOrUpdateIdle(GameObject canonical)
    {
        AnimationClip clip=LoadOrCreateClip(IdleClipPath,"UltraLowSD Raptor Idle",2.4f);
        Transform body=Require(canonical.transform,"Root/Body");
        SetPositionY(clip,"Root/Body",body.localPosition.y,2.4f,
            new[]{0f,0.6f,1.2f,1.8f,2.4f},new[]{0f,0.055f,0f,-0.035f,0f});
        SetRotation(clip,canonical,"Root/Body/Tail.01",2.4f,
            new[]{0f,0.6f,1.2f,1.8f,2.4f},
            new[]{Vector3.zero,new Vector3(0f,0f,3.5f),Vector3.zero,
                new Vector3(0f,0f,-3.5f),Vector3.zero});
        SetRotation(clip,canonical,"Root/Body/Tail.01/Tail.02",2.4f,
            new[]{0f,0.6f,1.2f,1.8f,2.4f},
            new[]{Vector3.zero,new Vector3(0f,0f,5f),Vector3.zero,
                new Vector3(0f,0f,-5f),Vector3.zero});
        SetRotation(clip,canonical,"Root/Body/Tail.01/Tail.02/Tail.03",2.4f,
            new[]{0f,0.6f,1.2f,1.8f,2.4f},
            new[]{Vector3.zero,new Vector3(0f,0f,7f),Vector3.zero,
                new Vector3(0f,0f,-7f),Vector3.zero});
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip CreateOrUpdateMove(GameObject canonical)
    {
        const float length=0.8f;
        float[] times={0f,0.2f,0.4f,0.6f,0.8f};
        AnimationClip clip=LoadOrCreateClip(MoveClipPath,"UltraLowSD Raptor Move",length);
        Transform body=Require(canonical.transform,"Root/Body");
        SetPositionY(clip,"Root/Body",body.localPosition.y,length,times,
            new[]{0f,0.12f,0f,0.12f,0f});
        SetAlternatingLimb(clip,canonical,"Root/Body/Thigh.L",length,times,24f);
        SetAlternatingLimb(clip,canonical,"Root/Body/Thigh.R",length,times,-24f);
        SetAlternatingLimb(clip,canonical,"Root/Body/Thigh.L/Shin.L",length,times,-18f);
        SetAlternatingLimb(clip,canonical,"Root/Body/Thigh.R/Shin.R",length,times,18f);
        SetAlternatingLimb(clip,canonical,"Root/Body/Thigh.L/Shin.L/Foot.L",length,times,11f);
        SetAlternatingLimb(clip,canonical,"Root/Body/Thigh.R/Shin.R/Foot.R",length,times,-11f);
        SetAlternatingLimb(clip,canonical,"Root/Body/Arm.L",length,times,-12f);
        SetAlternatingLimb(clip,canonical,"Root/Body/Arm.R",length,times,12f);
        SetRotation(clip,canonical,"Root/Body/Tail.01",length,times,
            new[]{new Vector3(0f,0f,-5f),Vector3.zero,new Vector3(0f,0f,5f),
                Vector3.zero,new Vector3(0f,0f,-5f)});
        SetRotation(clip,canonical,"Root/Body/Tail.01/Tail.02",length,times,
            new[]{new Vector3(0f,0f,-8f),Vector3.zero,new Vector3(0f,0f,8f),
                Vector3.zero,new Vector3(0f,0f,-8f)});
        SetRotation(clip,canonical,"Root/Body/Tail.01/Tail.02/Tail.03",length,times,
            new[]{new Vector3(0f,0f,-11f),Vector3.zero,new Vector3(0f,0f,11f),
                Vector3.zero,new Vector3(0f,0f,-11f)});
        FinishClip(clip);
        return clip;
    }

    private static void SetAlternatingLimb(
        AnimationClip clip,GameObject canonical,string path,float length,float[] times,float angle)
    {
        SetRotation(clip,canonical,path,length,times,
            new[]{new Vector3(angle,0f,0f),Vector3.zero,new Vector3(-angle,0f,0f),
                Vector3.zero,new Vector3(angle,0f,0f)});
    }

    private static AnimationClip LoadOrCreateClip(string path,string name,float length)
    {
        AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null)
        {
            clip=new AnimationClip();
            AssetDatabase.CreateAsset(clip,path);
        }
        clip.name=name;
        clip.frameRate=30f;
        clip.ClearCurves();
        AnimationClipSettings settings=AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime=true;
        settings.loopBlend=true;
        settings.stopTime=length;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        return clip;
    }

    private static void SetPositionY(
        AnimationClip clip,string path,float rest,float length,float[] times,float[] offsets)
    {
        float[] values=offsets.Select(value=>rest+value).ToArray();
        SetFloatCurve(clip,path,"m_LocalPosition.y",times,values,length);
    }

    private static void SetRotation(
        AnimationClip clip,GameObject canonical,string path,float length,
        float[] times,Vector3[] eulerOffsets)
    {
        Transform target=Require(canonical.transform,path);
        Quaternion[] values=eulerOffsets.Select(offset=>
            target.localRotation*Quaternion.Euler(offset)).ToArray();
        SetFloatCurve(clip,path,"m_LocalRotation.x",times,values.Select(q=>q.x).ToArray(),length);
        SetFloatCurve(clip,path,"m_LocalRotation.y",times,values.Select(q=>q.y).ToArray(),length);
        SetFloatCurve(clip,path,"m_LocalRotation.z",times,values.Select(q=>q.z).ToArray(),length);
        SetFloatCurve(clip,path,"m_LocalRotation.w",times,values.Select(q=>q.w).ToArray(),length);
    }

    private static void SetFloatCurve(
        AnimationClip clip,string path,string property,float[] times,float[] values,float length)
    {
        if(times.Length!=values.Length)throw new ArgumentException("Curve key count mismatch.");
        AnimationCurve curve=new AnimationCurve();
        for(int i=0;i<times.Length;i++)curve.AddKey(new Keyframe(times[i],values[i]));
        curve.preWrapMode=WrapMode.Loop;
        curve.postWrapMode=WrapMode.Loop;
        AnimationUtility.SetEditorCurve(
            clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
    }

    private static void FinishClip(AnimationClip clip)
    {
        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
    }

    private static AnimatorController CreateOrUpdateController(
        AnimationClip idleClip,AnimationClip moveClip)
    {
        AnimatorController controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if(controller==null)
            controller=AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        bool valid=controller.parameters.Any(parameter=>
                       parameter.name=="Moving"&&
                       parameter.type==AnimatorControllerParameterType.Bool)&&
                   controller.layers.Length==1&&
                   controller.layers[0].stateMachine!=null&&
                   controller.layers[0].stateMachine.states.Length==2&&
                   controller.animationClips.Contains(idleClip)&&
                   controller.animationClips.Contains(moveClip);
        if(valid)return controller;

        if(controller!=null&&!AssetDatabase.DeleteAsset(ControllerPath))
            throw new InvalidOperationException("Could not rebuild Animator Controller: "+ControllerPath);
        controller=AnimatorController.CreateAnimatorControllerAtPathWithClip(
            ControllerPath,idleClip);
        controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine=controller.layers[0].stateMachine;
        AnimatorState idle=machine.defaultState;
        idle.name="Idle";
        machine.defaultState=idle;
        AnimatorState move=machine.AddState("Move");
        move.motion=moveClip;
        AnimatorStateTransition toMove=idle.AddTransition(move);
        toMove.hasExitTime=false;
        toMove.duration=0.1f;
        toMove.AddCondition(AnimatorConditionMode.If,0f,"Moving");
        AnimatorStateTransition toIdle=move.AddTransition(idle);
        toIdle.hasExitTime=false;
        toIdle.duration=0.1f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot,0f,"Moving");
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            ControllerPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if(controller==null||controller.parameters.Length!=1||
           controller.layers.Length!=1||
           controller.layers[0].stateMachine.states.Length!=2||
           controller.animationClips.Distinct().Count()!=2)
            throw new InvalidOperationException(
                "Animator Controller did not survive a synchronous reload: "+ControllerPath);
        return controller;
    }

    private static void BuildCombatPrefab(
        string modelPath,string prefabPath,RuntimeAnimatorController controller)
    {
        GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        GameObject wrapper=new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
        GameObject visual=PrefabUtility.InstantiatePrefab(model) as GameObject;
        if(visual==null)
        {
            UnityEngine.Object.DestroyImmediate(wrapper);
            throw new InvalidOperationException("Could not instantiate: "+modelPath);
        }
        try
        {
            visual.name="Visual";
            visual.transform.SetParent(wrapper.transform,false);
            visual.transform.localPosition=Vector3.zero;
            Animator[] animators=visual.GetComponentsInChildren<Animator>(true);
            Animator animator=animators.Length>0?animators[0]:visual.AddComponent<Animator>();
            for(int i=1;i<animators.Length;i++)UnityEngine.Object.DestroyImmediate(animators[i]);
            animator.runtimeAnimatorController=controller;
            animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode=AnimatorUpdateMode.Normal;
            RaptorTurnAnticipation anticipation=wrapper.AddComponent<RaptorTurnAnticipation>();
            anticipation.Initialize();
            PrefabUtility.SaveAsPrefabAsset(wrapper,prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(wrapper);
        }
    }

    private static Transform Require(Transform root,string path)
    {
        Transform result=root.Find(path);
        if(result==null)throw new InvalidOperationException("Rig path missing: "+path);
        return result;
    }

    private static string RelativePath(Transform root,Transform item)
    {
        List<string> names=new List<string>();
        while(item!=null&&item!=root)
        {
            names.Add(item.name);
            item=item.parent;
        }
        names.Reverse();
        return string.Join("/",names.ToArray());
    }

    private static void EnsureFolder(string path)
    {
        string current="Assets";
        foreach(string part in path.Split('/').Skip(1))
        {
            string next=current+"/"+part;
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,part);
            current=next;
        }
    }
}
