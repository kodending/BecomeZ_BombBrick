using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PixelSpecialAnimationClipSetup
{
    public const string OutputRoot="Assets/Generated/PixelSpecialAnimationClips";
    private const float SkillFps=11f;
    private const float KnockdownFallSeconds=0.25f;

    [MenuItem("Tools/Monster Production/Build Special Combat Clips 001-080")]
    public static void BuildAll()
    {
        MonsterDatabase database=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(
            "Assets/Resources/MonsterData/MonsterDatabase.asset");
        if(database==null)throw new InvalidOperationException("Monster database is missing.");

        EnsureFolder(OutputRoot);
        int count=0;
        foreach(MonsterDefinition definition in database.monsters
                    .Where(value=>value!=null&&value.enabled)
                    .OrderBy(value=>value.serialNumber))
        {
            count+=ConfigurePrefab(definition,definition.resourcePath,false);
            if(!string.IsNullOrWhiteSpace(definition.shinyResourcePath))
                count+=ConfigurePrefab(definition,definition.shinyResourcePath,true);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[Pixel Special Clips] "+count+" prefabs configured.");
    }

    private static int ConfigurePrefab(
        MonsterDefinition definition,string resourcePath,bool shiny)
    {
        string prefabPath="Assets/Resources/"+resourcePath+".prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)==null)return 0;

        GameObject root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SpriteMonsterAnimationDriver driver=
                root.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            if(driver==null)return 0;
            Animation legacyAnimation=driver.GetComponent<Animation>();
            if(legacyAnimation!=null)
                UnityEngine.Object.DestroyImmediate(legacyAnimation,true);

            SerializedObject serializedDriver=new SerializedObject(driver);
            Sprite[] death=ReadFrames(serializedDriver,"deathFrames");
            Sprite[] skill=ReadFrames(serializedDriver,"skillAttackFrames");
            string suffix=shiny?"_Shiny":string.Empty;
            string folder=OutputRoot+"/"+definition.serialNumber;
            EnsureFolder(folder);

            AnimationClip knockdown=CreateClip(
                folder+"/"+definition.serialNumber+suffix+"_Knockdown.anim",
                definition.serialNumber+suffix+"_Knockdown",
                death,
                Enumerable.Range(0,death.Length)
                    .Select(index=>death.Length<=1
                        ?0f
                        :KnockdownFallSeconds*index/(death.Length-1))
                    .ToArray());

            AnimationClip windup=null;
            AnimationClip impact=null;
            if(string.Equals(definition.familyId,"tyranno",StringComparison.OrdinalIgnoreCase))
            {
                if(skill.Length<6)
                    throw new InvalidOperationException(
                        definition.serialNumber+" Tyranno SkillAttack needs six frames.");
                windup=CreateClip(
                    folder+"/"+definition.serialNumber+suffix+"_SkillWindup.anim",
                    definition.serialNumber+suffix+"_SkillWindup",
                    new[]{skill[0],skill[1],skill[2]},
                    new[]{0f,1f/SkillFps,2f/SkillFps});
                impact=CreateClip(
                    folder+"/"+definition.serialNumber+suffix+"_SkillImpact.anim",
                    definition.serialNumber+suffix+"_SkillImpact",
                    new[]{skill[3],skill[4],skill[5]},
                    new[]{0f,1f/SkillFps,2f/SkillFps});
            }

            AnimatorController controller=CreateController(
                folder+"/"+definition.serialNumber+suffix+"_Special.controller",
                windup,impact,knockdown);
            Animator specialAnimator=driver.GetComponent<Animator>();
            if(specialAnimator==null)
                specialAnimator=driver.gameObject.AddComponent<Animator>();
            specialAnimator.runtimeAnimatorController=controller;
            specialAnimator.applyRootMotion=false;
            specialAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            specialAnimator.enabled=false;
            driver.ConfigureSpecialClips(windup,impact,knockdown);
            driver.ConfigureSpecialAnimator(specialAnimator);
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            return 1;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static AnimationClip CreateClip(
        string path,string name,IReadOnlyList<Sprite> frames,IReadOnlyList<float> times)
    {
        if(frames.Count==0||frames.Count!=times.Count)
            throw new InvalidOperationException(name+" has invalid frames.");
        AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null)
        {
            clip=new AnimationClip();
            AssetDatabase.CreateAsset(clip,path);
        }
        clip.name=name;
        clip.legacy=false;
        clip.frameRate=SkillFps;
        clip.wrapMode=WrapMode.ClampForever;
        clip.ClearCurves();
        AnimationUtility.SetObjectReferenceCurve(
            clip,
            EditorCurveBinding.PPtrCurve(string.Empty,typeof(SpriteRenderer),"m_Sprite"),
            frames.Select((sprite,index)=>new ObjectReferenceKeyframe
            {
                time=times[index],value=sprite
            }).ToArray());
        AnimationClipSettings settings=AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime=false;
        settings.loopBlend=false;
        settings.stopTime=times[times.Count-1];
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateController(
        string path,AnimationClip windup,AnimationClip impact,AnimationClip knockdown)
    {
        // Rebuild instead of mutating an imported controller: Unity can retain an
        // empty runtime state table even when the YAML contains newly added states.
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(path)!=null)
            AssetDatabase.DeleteAsset(path);
        AnimatorController controller=
            AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine=controller.layers[0].stateMachine;
        AnimatorState first=null;
        first=AddState(machine,"Knockdown",knockdown,first);
        first=AddState(machine,"SkillWindup",windup,first);
        first=AddState(machine,"SkillImpact",impact,first);
        machine.defaultState=first;
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        // Prefab saving imports referenced controllers. Persist the state table
        // first or that import reloads this controller as an empty one.
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            path,
            ImportAssetOptions.ForceSynchronousImport|
            ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
    }

    private static AnimatorState AddState(
        AnimatorStateMachine machine,string name,AnimationClip clip,AnimatorState first)
    {
        if(clip==null)return first;
        AnimatorState state=machine.AddState(name);
        state.motion=clip;
        state.writeDefaultValues=false;
        EditorUtility.SetDirty(state);
        return first??state;
    }

    private static Sprite[] ReadFrames(SerializedObject owner,string propertyName)
    {
        SerializedProperty property=owner.FindProperty(propertyName);
        if(property==null)return Array.Empty<Sprite>();
        Sprite[] frames=new Sprite[property.arraySize];
        for(int index=0;index<frames.Length;index++)
            frames[index]=property.GetArrayElementAtIndex(index).objectReferenceValue as Sprite;
        return frames;
    }

    private static void EnsureFolder(string folder)
    {
        if(AssetDatabase.IsValidFolder(folder))return;
        string parent=Path.GetDirectoryName(folder).Replace('\\','/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
    }
}
