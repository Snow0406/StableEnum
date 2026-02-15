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
    /// 컴파일 완료(도메인 리로드) 시 자동으로 [StableEnum] enum의 변경을 감지하고
    /// StableEnumMigrator 를 통해 프로젝트 에셋을 자동 마이그레이션한다.
    /// </summary>
    [InitializeOnLoad]
    internal static class StableEnumWatcher
    {
        public static readonly List<string> MigrationLog = new();

        static StableEnumWatcher()
        {
            StableEnumRegistry.InvalidateCache();

            // 에디터 시작 직후 즉시 실행하면 AssetDatabase 가 아직 준비 안 됐을 수 있음
            EditorApplication.delayCall += RunCheck;
        }

        /// <summary>수동 실행 (EditorWindow 의 "지금 체크" 버튼에서 호출).</summary>
        public static void RunCheck()
        {
            var stableEnums = CollectStableEnumTypes();

            foreach (var type in stableEnums)
                ProcessEnum(type);
        }

        private static void ProcessEnum(Type enumType)
        {
            var current = TakeSnapshot(enumType);
            var saved = StableEnumRegistry.GetSnapshot(enumType.FullName);

            if (saved == null)
            {
                // 최초 등록
                StableEnumRegistry.SetSnapshot(enumType.FullName, current);
                Log($"[StableEnum] 등록 완료: {enumType.Name}");
                return;
            }

            var remap = BuildRemapTable(saved, current);
            if (remap.Count == 0)
            {
                // 변경 없음 -> 레지스트리만 최신화 (새 멤버 추가 등 대비)
                StableEnumRegistry.SetSnapshot(enumType.FullName, current);
                return;
            }
            
            Log($"[StableEnum] '{enumType.Name}' 변경 감지 — {BuildChangeDescription(saved, current)}");

            var currentMemberNames = Enum.GetNames(enumType); // 선언 순서 유지
            var result = StableEnumMigrator.Migrate(enumType.Name, currentMemberNames, remap);

            Log($"[StableEnum] '{enumType.Name}' 마이그레이션 완료 — " +
                $"오브젝트 {result.ObjectsModified}개, 필드 {result.FieldsModified}개 수정");

            // 마이그레이션 후 레지스트리 갱신
            StableEnumRegistry.SetSnapshot(enumType.FullName, current);
        }
        

        private static List<Type> CollectStableEnumTypes()
        {
            return TypeCache.GetTypesWithAttribute<StableAttribute>().ToList();
        }

        /// <summary>현재 enum 상태를 딕셔너리로 변환.</summary>
        private static Dictionary<string, int> TakeSnapshot(Type enumType)
        {
            var snap = new Dictionary<string, int>();
            foreach (var name in Enum.GetNames(enumType))
                snap[name] = Convert.ToInt32(Enum.Parse(enumType, name));
            return snap;
        }

        /// <summary>
        /// 이전/현재 스냅샷 비교 -> 같은 이름인데 int값이 달라진 케이스를 remap 테이블로 반환.
        /// </summary>
        private static Dictionary<int, int> BuildRemapTable(
            Dictionary<string, int> saved,
            Dictionary<string, int> current)
        {
            var remap = new Dictionary<int, int>();

            foreach (var kv in current)
            {
                // 새로 추가된 멤버는 remap 불필요
                if (!saved.TryGetValue(kv.Key, out int oldVal)) continue;
                // 값이 그대로면 스킵
                if (oldVal == kv.Value) continue;
                // 이미 같은 구값에 대한 매핑이 있으면 덮어쓰지 않음 (충돌 방지)
                if (!remap.ContainsKey(oldVal))
                    remap[oldVal] = kv.Value;
            }

            return remap;
        }

        /// <summary>
        /// saved랑 current 비교해서 "추가: CC / 이동: BB(1→2)" 형태 문자열 반환.
        /// </summary>
        private static string BuildChangeDescription(
            Dictionary<string, int> saved,
            Dictionary<string, int> current)
        {
            var parts = new List<string>();

            var added = current.Keys.Except(saved.Keys).ToList();
            if (added.Count > 0)
                parts.Add($"추가: {string.Join(", ", added)}");

            var removed = saved.Keys.Except(current.Keys).ToList();
            if (removed.Count > 0)
                parts.Add($"제거: {string.Join(", ", removed)}");

            return parts.Count > 0 ? string.Join(" / ", parts) : "(변경 없음)";
        }

        private static void Log(string msg)
        {
            MigrationLog.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
        }
    }
}

#endif
