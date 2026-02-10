#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StableEnum.Editor
{
    /// <summary>
    /// remap 테이블을 받아 프로젝트의 모든 직렬화 오브젝트를 스캔,
    /// 해당 enum 타입의 필드 값을 일괄 수정한다.
    /// </summary>
    internal static class StableEnumMigrator
    {
        public struct Result
        {
            public int ObjectsModified;
            public int FieldsModified;
        }

        /// <param name="typeName">enum의 단순 클래스명 (e.g. "WeaponType")</param>
        /// <param name="currentMemberNames">현재 enum 멤버명 배열 (선언 순서 유지)</param>
        /// <param name="remap">{ 이전 int값 → 새 int값 } 테이블</param>
        public static Result Migrate(string typeName, string[] currentMemberNames, Dictionary<int, int> remap)
        {
            var result = new Result();
            if (remap.Count == 0) return result;

            // .prefab / .asset
            var guids = AssetDatabase.FindAssets("t:Prefab")
                .Concat(AssetDatabase.FindAssets("t:ScriptableObject"))
                .Distinct();

            var assetPaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".prefab") || p.EndsWith(".asset"))
                .Distinct()
                .ToList();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in assetPaths)
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset == null) continue;
                    var so = new SerializedObject(asset);
                    int fields = MigrateObject(so, typeName, currentMemberNames, remap);
                    if (fields > 0)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        result.ObjectsModified++;
                        result.FieldsModified += fields;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            // 열린 씬 (AssetDatabase 배치 밖에서 처리)
            bool anySceneDirty = false;

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                bool sceneDirty = false;

                foreach (var root in scene.GetRootGameObjects())
                foreach (var comp in root.GetComponentsInChildren<Component>(includeInactive: true))
                {
                    if (comp == null) continue;
                    var so = new SerializedObject(comp);
                    int fields = MigrateObject(so, typeName, currentMemberNames, remap);
                    if (fields > 0)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        result.ObjectsModified++;
                        result.FieldsModified += fields;
                        sceneDirty = true;
                    }
                }

                if (sceneDirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    anySceneDirty = true;
                }
            }

            if (anySceneDirty)
                EditorSceneManager.SaveOpenScenes();

            return result;
        }

        private static int MigrateObject(
            SerializedObject so,
            string typeName,
            string[] currentMemberNames,
            Dictionary<int, int> remap)
        {
            int count = 0;
            var prop= so.GetIterator();
            bool hasNext = prop.Next(true);

            while (hasNext)
            {
                if (prop.propertyType == SerializedPropertyType.Enum
                    && IsTargetEnumType(prop, typeName, currentMemberNames)
                    && remap.TryGetValue(prop.intValue, out int newVal))
                {
                    prop.intValue = newVal;
                    count++;
                }
                hasNext = prop.Next(true);
            }

            return count;
        }

        /// <summary>
        /// prop.type 으로 1차 판별, 실패 시 enumDisplayNames 로 fallback.
        /// Unity 버전에 따라 prop.type 이 "int" 를 반환하는 경우 대비.
        /// </summary>
        private static bool IsTargetEnumType(SerializedProperty prop, string typeName, string[] memberNames)
        {
            // 1차: prop.type 직접 비교 (Unity 2022 기본)
            if (prop.type == typeName) return true;

            // 2차 fallback: enumDisplayNames 로 현재 멤버명과 비교
            var displayNames = prop.enumDisplayNames;
            if (displayNames == null || displayNames.Length != memberNames.Length) return false;

            for (int i = 0; i < memberNames.Length; i++)
                if (displayNames[i] != memberNames[i]) return false;

            return true;
        }
    }
}

#endif
