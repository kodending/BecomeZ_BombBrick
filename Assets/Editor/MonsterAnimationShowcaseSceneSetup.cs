using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MonsterAnimationShowcaseSceneSetup
{
    public const string ScenePath =
        "Assets/Scenes/MonsterAnimationShowcase.unity";

    [MenuItem("Tools/Monster Production/Create Animation Showcase Scene")]
    public static void CreateScene()
    {
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.orthographic = false;
        camera.fieldOfView =
            MonsterAutoBattlePrototype.CameraFieldOfView;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 120f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.color = new Color(0.92f, 0.95f, 1f);
        lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        GameObject system = new GameObject("Monster Animation Showcase");
        system.AddComponent<MonsterAnimationShowcase>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = system;
        Debug.Log("[Monster Showcase] Created " + ScenePath);
    }
}
