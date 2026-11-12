using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MonsterAutoBattleSceneSetup
{
    private const string ScenePath="Assets/Scenes/MonsterAutoBattleTilePrototype.unity";

    [MenuItem("Tools/Brick Survivor/Create Monster Auto Battle Tile Prototype")]
    public static void CreateScene()
    {
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);

        GameObject cameraObject=new GameObject("Main Camera");
        Camera camera=cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        cameraObject.tag="MainCamera";
        camera.fieldOfView=34f;
        camera.nearClipPlane=0.1f;
        camera.farClipPlane=120f;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(0.055f,0.065f,0.085f);
        cameraObject.transform.position=new Vector3(17f,27f,-29.4f);
        cameraObject.transform.rotation=Quaternion.LookRotation(new Vector3(0f,0f,0f)-cameraObject.transform.position,Vector3.up);

        GameObject lightObject=new GameObject("Directional Light");
        Light light=lightObject.AddComponent<Light>();
        light.type=LightType.Directional;
        light.intensity=1.25f;
        light.color=new Color(0.92f,0.95f,1f);
        lightObject.transform.rotation=Quaternion.Euler(48f,-35f,0f);

        GameObject system=new GameObject("Monster Auto Battle Prototype");
        system.AddComponent<MonsterAutoBattlePrototype>();

        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettingsScene[] scenes=EditorBuildSettings.scenes;
        if(!scenes.Any(item=>item.path==ScenePath))
            EditorBuildSettings.scenes=scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        Selection.activeGameObject=system;
        Debug.Log("[Brick Survivor] Created Monster Auto Battle tile prototype scene: "+ScenePath);
    }
}
