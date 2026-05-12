#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StableEnum.Editor
{
    /// <summary>
    /// Persistently stores per-member int value history in ProjectSettings/StableEnumRegistry.json.
    /// Serves as the baseline for migration decisions when a [StableEnum] enum is added, removed, or reordered.
    /// </summary>
    internal static class StableEnumRegistry
    {
        private const string FilePath = "ProjectSettings/StableEnumRegistry.json";

        private static Dictionary<string, Dictionary<string, int>> _cache;
        
        /// <summary>Returns the stored snapshot. Null if the enum has not been registered yet.</summary>
        public static Dictionary<string, int> GetSnapshot(string enumFullName)
        {
            EnsureLoaded();
            return _cache.TryGetValue(enumFullName, out var snap) ? snap : null;
        }

        public static void SetSnapshot(string enumFullName, Dictionary<string, int> snapshot)
        {
            EnsureLoaded();

            // 동일한 스냅샷이면 디스크 쓰기 생략.
            // Watcher가 변경 0건일 때도 이 메서드를 호출하므로,
            // 가드가 없으면 도메인 리로드마다 JSON이 재작성되어
            // 사용자 git 설정(core.autocrlf 등)에 따라 가짜 modified로 표시됨.
            if (_cache.TryGetValue(enumFullName, out var existing)
                && SnapshotsEqual(existing, snapshot))
                return;

            _cache[enumFullName] = snapshot;
            Flush();
        }

        private static bool SnapshotsEqual(Dictionary<string, int> a, Dictionary<string, int> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Count != b.Count) return false;
            foreach (var kv in a)
                if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
            return true;
        }

        /// <summary>Invalidates the cache after domain reload (prevents stale data).</summary>
        public static void InvalidateCache() => _cache = null;

        /// <summary>List of all registered enum names (for EditorWindow).</summary>
        public static IEnumerable<string> GetAllEnumFullNames()
        {
            EnsureLoaded();
            return _cache.Keys;
        }

        private static void EnsureLoaded()
        {
            if (_cache != null) return;

            _cache = new Dictionary<string, Dictionary<string, int>>();

            if (!File.Exists(FilePath)) return;

            try
            {
                var raw  = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<RegistryData>(raw);
                if (data?.records == null) return;

                foreach (var rec in data.records)
                {
                    var dict = new Dictionary<string, int>(rec.memberNames.Count);
                    for (int i = 0; i < rec.memberNames.Count; i++)
                        dict[rec.memberNames[i]] = rec.memberValues[i];
                    _cache[rec.enumFullName] = dict;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[StableEnum] Failed to load registry: {e.Message}");
            }
        }

        private static void Flush()
        {
            var data = new RegistryData();

            foreach (var kv in _cache)
            {
                var rec = new EnumRecord { enumFullName = kv.Key };
                foreach (var m in kv.Value)
                {
                    rec.memberNames.Add(m.Key);
                    rec.memberValues.Add(m.Value);
                }
                data.records.Add(rec);
            }

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[StableEnum] Failed to save registry: {e.Message}");
            }
        }

        // ── Serializable Data Models ──────────────────────────────────────────

        [Serializable]
        private class RegistryData
        {
            public List<EnumRecord> records = new();
        }

        [Serializable]
        private class EnumRecord
        {
            public string       enumFullName;
            public List<string> memberNames  = new();
            public List<int>    memberValues = new();
        }
    }
}

#endif
