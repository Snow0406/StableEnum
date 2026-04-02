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

        /// <param name="enumType">대상 enum의 System.Type</param>
        /// <param name="currentMemberNames">현재 enum 멤버명 배열 (선언 순서 유지)</param>
        /// <param name="remap">{ 이전 int값 → 새 int값 } 테이블</param>
        public static Result Migrate(System.Type enumType, string[] currentMemberNames, Dictionary<int, int> remap)
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
                    int fields = MigrateObject(so, enumType, currentMemberNames, remap);
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
                    int fields = MigrateObject(so, enumType, currentMemberNames, remap);
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
            System.Type enumType,
            string[] currentMemberNames,
            Dictionary<int, int> remap)
        {
            int count = 0;
            var prop = so.GetIterator();
            bool hasNext = prop.Next(true);

            while (hasNext)
            {
                if (prop.propertyType == SerializedPropertyType.Enum
                    && IsTargetEnumType(prop, enumType, currentMemberNames)
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
        /// Unity 2022: prop.type 으로 판별. Unity 6+: boxedValue 타입 비교.
        /// 공통 fallback: enumNames(raw 이름) 비교.
        /// </summary>
        private static bool IsTargetEnumType(SerializedProperty prop, System.Type enumType, string[] memberNames)
        {
#if UNITY_6000_0_OR_NEWER
            // Unity 6+: prop.type이 더 이상 enum 타입명을 반환하지 않으므로 boxedValue로 정확한 타입 비교
            try
            {
                var boxed = prop.boxedValue;
                if (boxed != null && boxed.GetType() == enumType)
                    return true;
            }
            catch
            {
                // boxedValue 접근 실패 시 fallback으로 진행
            }
#else
            // Unity 2022: prop.type이 enum 타입명을 반환
            if (prop.type == enumType.Name) return true;
#endif

            // fallback: enumNames(raw 이름)로 멤버 비교 — enumDisplayNames는 NicifyVariableName 적용되어 불일치 가능
            var enumNames = prop.enumNames;
            if (enumNames == null || enumNames.Length != memberNames.Length) return false;

            for (int i = 0; i < memberNames.Length; i++)
                if (enumNames[i] != memberNames[i]) return false;

            return true;
        }
    }
}

#endif
