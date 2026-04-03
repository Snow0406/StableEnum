#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using StableEnum;

namespace StableEnum.Editor
{
    /// <summary>
    /// Automatically detects [StableEnum] enum changes on compilation (domain reload)
    /// and auto-migrates project assets via StableEnumMigrator.
    /// </summary>
    [InitializeOnLoad]
    internal static class StableEnumWatcher
    {
        public static readonly List<string> MigrationLog = new();

        static StableEnumWatcher()
        {
            StableEnumRegistry.InvalidateCache();

            // Running immediately after editor startup may find AssetDatabase not ready yet
            EditorApplication.delayCall += RunCheck;
        }

        /// <summary>Manual invocation (called from the EditorWindow "Run Check" button).</summary>
        public static void RunCheck()
        {
            var stableEnums = CollectStableEnumTypes();

            foreach (var type in stableEnums)
                ProcessEnum(type);
            
            Log($"[StableEnum] Check complete ({MigrationLog.Count} migrations)");
        }

        private static void ProcessEnum(Type enumType)
        {
            var current = TakeSnapshot(enumType);
            var saved = StableEnumRegistry.GetSnapshot(enumType.FullName);

            if (saved == null)
            {
                // First registration
                StableEnumRegistry.SetSnapshot(enumType.FullName, current);
                Log($"[StableEnum] Registered: {enumType.Name}");
                return;
            }

            var remap = BuildRemapTable(saved, current);
            if (remap.Count == 0)
            {
                // No changes -> just update registry (in case of new members added)
                StableEnumRegistry.SetSnapshot(enumType.FullName, current);
                return;
            }
            
            Log($"[StableEnum] '{enumType.Name}' change detected — {BuildChangeDescription(saved, current)}");

            var currentMemberNames = Enum.GetNames(enumType); // Preserve declaration order
            bool isFlags = enumType.IsDefined(typeof(System.FlagsAttribute), false);
            var result = StableEnumMigrator.Migrate(enumType, currentMemberNames, remap, isFlags);

            Log($"[StableEnum] '{enumType.Name}' migration complete — " +
                $"{result.ObjectsModified} objects, {result.FieldsModified} fields modified");

            // Update registry after migration
            StableEnumRegistry.SetSnapshot(enumType.FullName, current);
        }
        

        private static List<Type> CollectStableEnumTypes()
        {
            return TypeCache.GetTypesWithAttribute<StableAttribute>().ToList();
        }

        /// <summary>Converts current enum state to a dictionary.</summary>
        private static Dictionary<string, int> TakeSnapshot(Type enumType)
        {
            var snap = new Dictionary<string, int>();
            foreach (var name in Enum.GetNames(enumType))
                snap[name] = Convert.ToInt32(Enum.Parse(enumType, name));
            return snap;
        }

        /// <summary>
        /// Compares saved/current snapshots -> returns remap table for entries where the same name has a changed int value.
        /// </summary>
        private static Dictionary<int, int> BuildRemapTable(
            Dictionary<string, int> saved,
            Dictionary<string, int> current)
        {
            var remap = new Dictionary<int, int>();

            foreach (var kv in current)
            {
                // Newly added members don't need remapping
                if (!saved.TryGetValue(kv.Key, out int oldVal)) continue;
                // Skip if value unchanged
                if (oldVal == kv.Value) continue;
                // Don't overwrite existing mapping for the same old value (prevent collision)
                if (!remap.ContainsKey(oldVal))
                    remap[oldVal] = kv.Value;
            }

            return remap;
        }

        /// <summary>
        /// Compares saved and current to return a string like "added: CC / removed: BB".
        /// </summary>
        private static string BuildChangeDescription(
            Dictionary<string, int> saved,
            Dictionary<string, int> current)
        {
            var parts = new List<string>();

            var added = current.Keys.Except(saved.Keys).ToList();
            if (added.Count > 0)
                parts.Add($"added: {string.Join(", ", added)}");

            var removed = saved.Keys.Except(current.Keys).ToList();
            if (removed.Count > 0)
                parts.Add($"removed: {string.Join(", ", removed)}");

            return parts.Count > 0 ? string.Join(" / ", parts) : "(no changes)";
        }

        private static void Log(string msg)
        {
            MigrationLog.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }
    }
}

#endif
