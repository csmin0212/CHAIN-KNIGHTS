using System.Linq;
using ChainKnights.Client.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainKnights.Editor
{
    /// <summary>
    /// 전투 씬을 코드로 만든다. 화면은 BattleScreen이 실행할 때 그리니 씬에는 카메라와 BattleScreen만 둔다.
    /// 명령줄: Unity -batchmode -projectPath . -executeMethod ChainKnights.Editor.SceneBuilder.CreateBattleSceneAndExit
    /// </summary>
    public static class SceneBuilder
    {
        private const string BattleScenePath = "Assets/Scenes/Battle.unity";

        [MenuItem("Chain Knights/전투 씬 다시 만들기")]
        public static void CreateBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x17, 0x18, 0x1F, 0xFF);
            cameraObject.transform.position = new Vector3(0, 0, -10);

            new GameObject("BattleScreen", typeof(BattleScreen));

            EditorSceneManager.SaveScene(scene, BattleScenePath);

            // 빌드 설정에서 전투 씬을 맨 앞에 둔다.
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != BattleScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(BattleScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"전투 씬을 만들었습니다: {BattleScenePath}");
        }

        public static void CreateBattleSceneAndExit()
        {
            CreateBattleScene();
            EditorApplication.Exit(0);
        }
    }
}
