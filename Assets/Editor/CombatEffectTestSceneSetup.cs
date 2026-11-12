using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombatEffectTestSceneSetup
{
    public const string ScenePath="Assets/Scenes/CombatEffectTest.unity";

    [MenuItem("Tools/Monster Production/Test Combat SFX")]
    public static void TestCombatSfx()
    {
        if(EditorApplication.isPlaying)
        {
            CombatEffectTestSceneController controls=
                Object.FindObjectOfType<CombatEffectTestSceneController>();
            if(controls!=null)controls.SelectSoundPreset(0);
            else Debug.LogWarning("현재 플레이를 종료한 뒤 SFX 테스트를 여세요.");
            return;
        }
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        // Session-only test selection. Never reset or write the player's profile.
        CombatEffectTestConfiguration.AllyMonsterIds.Clear();
        CombatEffectTestConfiguration.EnemyMonsterIds.Clear();
        CombatEffectTestConfiguration.AllyMonsterId="velos";
        CombatEffectTestConfiguration.EnemyMonsterId="ironkuma";
        CombatEffectTestConfiguration.AllyCount=1;
        CombatEffectTestConfiguration.EnemyCount=2;
        CombatEffectTestConfiguration.AllyRank=ExpeditionMonsterRank.Rank3;
        CombatEffectTestConfiguration.EnemyRank=ExpeditionMonsterRank.Rank2;
        CombatEffectTestConfiguration.AllyTranscended=false;
        CombatEffectTestConfiguration.EnemyTranscended=false;
        CombatEffectTestConfiguration.AllyRank2TraitId="";
        CombatEffectTestConfiguration.AllyRank3TraitId="";
        CombatEffectTestConfiguration.EnemyRank2TraitId="";
        CombatEffectTestConfiguration.EnemyRank3TraitId="";
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/Monster Production/Create Combat Effect Test Scene")]
    public static void CreateScene()
    {
        Scene scene=EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GameObject controls=new GameObject("Combat Effect Test Controls");
        controls.AddComponent<AudioListener>();
        controls.AddComponent<CombatEffectTestSceneController>();
        EditorSceneManager.SaveScene(scene,ScenePath);
        Selection.activeGameObject=controls;
        Debug.Log("[BombBrick] Created production-battle test bootstrap: "+ScenePath);
    }
}
