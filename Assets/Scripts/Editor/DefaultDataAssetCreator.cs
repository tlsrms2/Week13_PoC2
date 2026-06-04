// ============================================================================
// 파일:    Editor/DefaultDataAssetCreator.cs
// 프로젝트: Project SEVERANCE
// 용도: 기본 데이터 에셋 자동 생성 유틸리티.
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Severance.Editor
{
    // [InitializeOnLoad]
    internal static class DefaultDataAssetCreator
    {
        private const string DataFolder = "Assets/Data";
        private const string StagePath = DataFolder + "/Stage01.asset";

        // static DefaultDataAssetCreator()
        // {
        //     EditorApplication.delayCall += EnsureDefaultAssets;
        // }

        [MenuItem("Severance/Data/기본 데이터 생성")]
        public static void EnsureDefaultAssets()
        {
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }

            bool created = false;
            created |= CreateAssetIfMissing<GameConfig>(DataFolder + "/DefaultGameConfig.asset");
            created |= CreateAssetIfMissing<StageConfig>(StagePath);

            StageConfig stage = AssetDatabase.LoadAssetAtPath<StageConfig>(StagePath);
            bool stageSynced = SyncStageDefaults(stage);
            bool linked = LinkGameConfigs(stage);

            if (created || stageSynced || linked)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[DefaultDataAssetCreator] Assets/Data 기본 데이터 에셋 생성 및 연결 완료.");
            }
        }

        private static bool CreateAssetIfMissing<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                return false;
            }

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return true;
        }

        private static bool SyncStageDefaults(StageConfig stage)
        {
            if (stage == null || MatchesDefaultStage(stage.ResourceNodes))
            {
                return false;
            }

            stage.ResetToDefault();
            EditorUtility.SetDirty(stage);
            return true;
        }

        private static bool MatchesDefaultStage(StageResourceNode[] nodes)
        {
            StageResourceNode[] defaults = StageConfig.CreateDefaultResourceNodes();
            if (nodes == null || nodes.Length != defaults.Length)
            {
                return false;
            }

            for (int i = 0; i < defaults.Length; i++)
            {
                if (nodes[i].Position != defaults[i].Position ||
                    nodes[i].ResourceType != defaults[i].ResourceType ||
                    nodes[i].Yield != defaults[i].Yield)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>GameConfig에 StageConfig를 자동 연결합니다.</summary>
        private static bool LinkGameConfigs(StageConfig stage)
        {
            if (stage == null)
            {
                return false;
            }

            bool changed = false;
            string[] guids = AssetDatabase.FindAssets("t:GameConfig", new[] { DataFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
                if (config == null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(config);
                SerializedProperty stageProp = serialized.FindProperty("stageConfig");

                if (stageProp != null && stageProp.objectReferenceValue == null)
                {
                    stageProp.objectReferenceValue = stage;
                    changed = true;
                }

                if (serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    EditorUtility.SetDirty(config);
                }
            }

            return changed;
        }
    }
}
