#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StableEnum.Editor
{
    /// <summary>
    /// Receives a remap table and scans all serialized objects in the project,
    /// applying bulk updates to enum-typed field values.
    /// </summary>
    internal static class StableEnumMigrator
    {
        public struct Result
        {
            public int ObjectsModified;
            public int FieldsModified;
        }

        /// <param name="enumType">System.Type of the target enum</param>
        /// <param name="currentMemberNames">Current enum member name array (declaration order preserved)</param>
        /// <param name="remap">{ old int value → new int value } table</param>
        public static Result Migrate(System.Type enumType, string[] currentMemberNames, Dictionary<int, int> remap, bool isFlags)
        {
            var result = new Result();
            if (remap.Count == 0) return result;

            // Collect types that have this enum as a serialized field via reflection
            var ownerTypes = CollectOwnerTypes(enumType);
            if (ownerTypes.Count == 0) return result;

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
                    if (asset == null || !ownerTypes.Contains(asset.GetType())) continue;
                    var so = new SerializedObject(asset);
                    int fields = MigrateObject(so, enumType, currentMemberNames, remap, isFlags);
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

            // Open scenes (handled outside AssetDatabase batch)
            bool anySceneDirty = false;

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;

                bool sceneDirty = false;

                foreach (var root in scene.GetRootGameObjects())
                foreach (var comp in root.GetComponentsInChildren<Component>(includeInactive: true))
                {
                    if (comp == null || !ownerTypes.Contains(comp.GetType())) continue;
                    var so = new SerializedObject(comp);
                    int fields = MigrateObject(so, enumType, currentMemberNames, remap, isFlags);
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
            Dictionary<int, int> remap,
            bool isFlags)
        {
            int count = 0;
            var prop = so.GetIterator();
            bool hasNext = prop.Next(true);

            while (hasNext)
            {
                if (prop.propertyType == SerializedPropertyType.Enum
                    && IsTargetEnumType(prop, enumType, currentMemberNames))
                {
                    if (isFlags)
                    {
                        int oldVal = prop.intValue;
                        int remaining = oldVal;
                        int newVal = 0;
                        bool changed = false;

                        foreach (var kv in remap)
                        {
                            if ((remaining & kv.Key) == kv.Key && kv.Key != 0)
                            {
                                newVal |= kv.Value;
                                remaining &= ~kv.Key;
                                changed = true;
                            }
                        }
                        newVal |= remaining;

                        if (changed)
                        {
                            prop.intValue = newVal;
                            count++;
                        }
                    }
                    else if (remap.TryGetValue(prop.intValue, out int newVal))
                    {
                        prop.intValue = newVal;
                        count++;
                    }
                }
                hasNext = prop.Next(true);
            }

            return count;
        }

        /// <summary>
        /// Collects MonoBehaviour/ScriptableObject types that have the given enum type as a serialized field via reflection.
        /// Also recursively searches nested [Serializable] structs.
        /// </summary>
        private static HashSet<System.Type> CollectOwnerTypes(System.Type enumType)
        {
            var owners = new HashSet<System.Type>();
            const BindingFlags kFlags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (var type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                         .Concat(TypeCache.GetTypesDerivedFrom<ScriptableObject>()))
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition) continue;

                var visited = new HashSet<System.Type>();
                bool found = false;

                for (var t = type;
                     t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject);
                     t = t.BaseType)
                {
                    foreach (var field in t.GetFields(kFlags))
                    {
                        if (FieldContainsType(field.FieldType, enumType, visited))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                if (found) owners.Add(type);
            }

            return owners;
        }

        private static bool FieldContainsType(System.Type fieldType, System.Type target, HashSet<System.Type> visited)
        {
            if (fieldType == target) return true;

            // Array: T[]
            if (fieldType.IsArray)
            {
                var elem = fieldType.GetElementType();
                if (elem == target) return true;
                if (elem != null && IsUserSerializable(elem))
                    return FieldContainsType(elem, target, visited);
                return false;
            }

            // Generic: List<T>, etc.
            if (fieldType.IsGenericType)
            {
                foreach (var arg in fieldType.GetGenericArguments())
                {
                    if (arg == target) return true;
                    if (IsUserSerializable(arg) && FieldContainsType(arg, target, visited))
                        return true;
                }
                return false;
            }

            // Nested [Serializable] struct/class
            if (IsUserSerializable(fieldType) && visited.Add(fieldType))
            {
                foreach (var f in fieldType.GetFields(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (FieldContainsType(f.FieldType, target, visited))
                        return true;
                }
            }

            return false;
        }

        private static bool IsUserSerializable(System.Type t)
        {
            return !t.IsPrimitive && !t.IsEnum
                && t.IsDefined(typeof(System.SerializableAttribute), false);
        }

        /// <summary>
        /// Verifies the exact field type via reflection.
        /// Final fallback: compare enumNames (raw names).
        /// </summary>
        private static bool IsTargetEnumType(SerializedProperty prop, System.Type enumType, string[] memberNames)
        {
            var fieldType = GetFieldType(prop);
            if (fieldType != null)
                return fieldType == enumType;

            // fallback
            var enumNames = prop.enumNames;
            if (enumNames == null || enumNames.Length != memberNames.Length) return false;

            for (int i = 0; i < memberNames.Length; i++)
                if (enumNames[i] != memberNames[i]) return false;

            Debug.LogWarning($"[StableEnum] Falling back to member-name comparison for '{enumType.Name}'. " +
                $"If another enum has the same member layout, a mismatch may occur. (property: {prop.propertyPath})");
            return true;
        }

        /// <summary>
        /// Calls Unity's internal ScriptAttributeUtility.GetFieldInfoAndStaticTypeFromProperty via reflection
        /// and returns the actual C# field type of the SerializedProperty.
        /// </summary>
        private static System.Type GetFieldType(SerializedProperty prop)
        {
            try
            {
                var utilType = typeof(UnityEditor.Editor).Assembly
                    .GetType("UnityEditor.ScriptAttributeUtility");
                if (utilType == null) return null;

                var method = utilType.GetMethod(
                    "GetFieldInfoAndStaticTypeFromProperty",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (method == null) return null;

                var args = new object[] { prop, null };
                method.Invoke(null, args);
                return args[1] as System.Type;
            }
            catch
            {
                return null;
            }
        }
    }
}

#endif
