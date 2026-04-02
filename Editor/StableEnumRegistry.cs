#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StableEnum.Editor
{
    /// <summary>
    /// Library/StableEnumRegistry.json 에 enum 멤버별 int 값 이력을 영속 저장.
    /// Library/ 폴더는 .gitignore 대상이므로 소스 관리에 노출되지 않는다.
    /// [StableEnum] enum이 추가/삭제/재정렬될 때 마이그레이션 판단의 기준이 된다.
    /// </summary>
    internal static class StableEnumRegistry
    {
        private const string FilePath = "Library/StableEnumRegistry.json";

        private static Dictionary<string, Dictionary<string, int>> _cache;
        
        /// <summary>저장된 스냅샷 반환. 처음 등록되는 enum이면 null.</summary>
        public static Dictionary<string, int> GetSnapshot(string enumFullName)
        {
            EnsureLoaded();
            return _cache.TryGetValue(enumFullName, out var snap) ? snap : null;
        }

        public static void SetSnapshot(string enumFullName, Dictionary<string, int> snapshot)
        {
            EnsureLoaded();
            _cache[enumFullName] = snapshot;
            Flush();
        }

        /// <summary>도메인 리로드 후 캐시 무효화 (stale 데이터 방지).</summary>
        public static void InvalidateCache() => _cache = null;

        /// <summary>등록된 모든 enum 이름 목록 (EditorWindow 용).</summary>
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
                Debug.LogError($"[StableEnum] Registry 로드 실패: {e.Message}");
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
                Debug.LogError($"[StableEnum] Registry 저장 실패: {e.Message}");
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
