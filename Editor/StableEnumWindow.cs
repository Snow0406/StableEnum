#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StableEnum.Editor
{
    /// <summary>
    /// Tools > StableEnum
    /// 등록된 enum 현황 확인 + 수동 마이그레이션 실행.
    /// </summary>
    internal class StableEnumWindow : EditorWindow
    {
        [MenuItem("Tools/StableEnum")]
        private static void Open() => GetWindow<StableEnumWindow>("StableEnum Manager");

        private Vector2 _scrollEnum;
        private Vector2 _scrollLog;
        private readonly Dictionary<string, bool> _foldouts = new();

        private void OnGUI()
        {
            DrawHeader();
            GUILayout.Space(4);

            float totalH = position.height;
            float enumH = totalH * 0.55f;
            float logH = totalH * 0.35f;

            DrawEnumList(enumH);
            GUILayout.Space(4);
            DrawLog(logH);
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("StableEnum Manager", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("지금 체크", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                StableEnumWatcher.RunCheck();
                Repaint();
            }
            if (GUILayout.Button("로그 지우기", EditorStyles.toolbarButton, GUILayout.Width(70)))
                StableEnumWatcher.MigrationLog.Clear();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEnumList(float height)
        {
            GUILayout.Label("등록된 Enum", EditorStyles.boldLabel);

            var allNames = StableEnumRegistry.GetAllEnumFullNames().ToList();
            if (allNames.Count == 0)
            {
                EditorGUILayout.HelpBox("[StableEnum] 어트리뷰트가 붙은 enum이 없습니다.", MessageType.Info);
                return;
            }

            _scrollEnum = EditorGUILayout.BeginScrollView(_scrollEnum,
                GUILayout.Height(height));

            foreach (var fullName in allNames)
            {
                if (!_foldouts.ContainsKey(fullName)) _foldouts[fullName] = false;

                var snap = StableEnumRegistry.GetSnapshot(fullName);
                string label = $"{SimpleName(fullName)}  ({snap?.Count ?? 0}개 멤버)";

                _foldouts[fullName] = EditorGUILayout.Foldout(
                    _foldouts[fullName], label, toggleOnLabelClick: true);

                if (_foldouts[fullName] && snap != null)
                {
                    EditorGUI.indentLevel++;
                    foreach (var kv in snap)
                        EditorGUILayout.LabelField(kv.Key, kv.Value.ToString());
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLog(float height)
        {
            GUILayout.Label("마이그레이션 로그", EditorStyles.boldLabel);

            _scrollLog = EditorGUILayout.BeginScrollView(_scrollLog, GUILayout.Height(height));

            var log = StableEnumWatcher.MigrationLog;
            if (log.Count == 0)
            {
                GUILayout.Label("(로그 없음)", EditorStyles.miniLabel);
            }
            else
            {
                // 최신 순으로 출력
                for (int i = log.Count - 1; i >= 0; i--)
                    GUILayout.Label(log[i], EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private static string SimpleName(string fullName)
        {
            int dot = fullName.LastIndexOf('.');
            return dot >= 0 ? fullName[(dot + 1)..] : fullName;
        }
    }
}

#endif
