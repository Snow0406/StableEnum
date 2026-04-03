#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StableEnum.Editor
{
    internal class StableEnumWindow : EditorWindow
    {
        [MenuItem("Tools/StableEnum")]
        private static void Open()
        {
            var w = GetWindow<StableEnumWindow>("StableEnum");
            w.minSize = new Vector2(360, 300);
        }

        // ── State ──
        private int    _tab;
        private string _enumSearch = "";
        private string _logSearch  = "";

        private Vector2 _scrollEnum;
        private Vector2 _scrollLog;

        private readonly Dictionary<string, bool> _foldouts = new();

        private static readonly string[] TabLabels = { "Enums", "Log" };

        // ── Colors ──
        private static readonly Color DividerColor  = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color AccentBlue    = new(0.35f, 0.65f, 1f, 1f);
        private static readonly Color MutedText     = new(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color TagBg         = new(0.28f, 0.28f, 0.28f, 1f);
        private static readonly Color PillBlue      = new(0.35f, 0.65f, 1f,   0.22f);
        private static readonly Color PillBlueTxt   = new(0.55f, 0.80f, 1f,   1f);
        private static readonly Color PillOrange    = new(1f,    0.65f, 0.25f, 0.22f);
        private static readonly Color PillOrangeTxt = new(1f,    0.80f, 0.45f, 1f);
        private static readonly Color LogInfo       = new(0.6f,  0.75f, 0.6f,  1f);
        private static readonly Color LogWarn       = new(1f,    0.85f, 0.4f,  1f);
        private static readonly Color LogMigrate    = new(0.5f,  0.7f,  1f,    1f);

        // ── Styles (lazy) ──
        private GUIStyle _cardStyle;
        private GUIStyle _searchStyle;
        private GUIStyle _tagStyle;
        private GUIStyle _memberLabel;
        private GUIStyle _memberValue;
        private GUIStyle _logStyle;
        private GUIStyle _emptyLabel;
        private GUIStyle _foldoutBold;
        private GUIStyle _nsLabel;

        private void EnsureStyles()
        {
            if (_cardStyle != null) return;

            _cardStyle = new GUIStyle("box")
            {
                margin  = new RectOffset(6, 6, 2, 4),
                padding = new RectOffset(10, 10, 8, 8),
            };

            _searchStyle = new GUIStyle(EditorStyles.toolbarSearchField)
            {
                margin = new RectOffset(6, 6, 4, 4),
            };

            _tagStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                padding   = new RectOffset(6, 6, 2, 2),
                margin    = new RectOffset(4, 0, 0, 0),
                normal    = { textColor = MutedText },
            };

            _memberLabel = new GUIStyle(EditorStyles.label)
            {
                padding  = new RectOffset(20, 4, 1, 1),
                richText = true,
            };

            _memberValue = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight,
                padding   = new RectOffset(4, 12, 1, 1),
                normal    = { textColor = AccentBlue },
                fontStyle = FontStyle.Bold,
            };

            _logStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                fontSize = 11,
                padding  = new RectOffset(10, 10, 2, 2),
                wordWrap = true,
            };

            _emptyLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize  = 12,
                alignment = TextAnchor.MiddleCenter,
            };
            
            _foldoutBold = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
            };

            _nsLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                margin = new RectOffset(8, 0, 0, 0),
                normal = { textColor = MutedText },
            };
        }

        // ═══════════════════════════════════════════════════════════════
        //  OnGUI
        // ═══════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            EnsureStyles();

            DrawTabs();
            GUILayout.Space(2);
            DrawDivider();

            if (_tab == 0) DrawEnumPanel();
            else           DrawLogPanel();
        }

        // ── Tabs ──

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(6);

            for (int i = 0; i < TabLabels.Length; i++)
            {
                string label = TabLabels[i];
                if (i == 1)
                {
                    int count = StableEnumWatcher.MigrationLog.Count;
                    if (count > 0) label += $"  ({count})";
                }

                if (GUILayout.Toggle(_tab == i, label, EditorStyles.toolbarButton, GUILayout.Width(60)))
                    _tab = i;
            }

            GUILayout.Space(6);

            if (_tab == 0)
            {
                _enumSearch = EditorGUILayout.TextField(_enumSearch, _searchStyle);
            }
            else
            {
                _logSearch = EditorGUILayout.TextField(_logSearch, _searchStyle);
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    StableEnumWatcher.MigrationLog.Clear();
            }

            GUILayout.Space(4);
            GUI.backgroundColor = new Color(0.3f, 0.6f, 1f, 0.3f);
            if (GUILayout.Button("Run Check", EditorStyles.toolbarButton, GUILayout.Width(76)))
            {
                StableEnumWatcher.RunCheck();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Space(6);

            EditorGUILayout.EndHorizontal();
        }

        // ── Enum Panel ──

        private void DrawEnumPanel()
        {
            var allNames = StableEnumRegistry.GetAllEnumFullNames().ToList();

            if (!string.IsNullOrEmpty(_enumSearch))
            {
                allNames = allNames
                    .Where(n => n.IndexOf(_enumSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            if (allNames.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    string.IsNullOrEmpty(_enumSearch)
                        ? "No [StableEnum] enums registered yet."
                        : "No matching enums found.",
                    _emptyLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            _scrollEnum = EditorGUILayout.BeginScrollView(_scrollEnum);
            GUILayout.Space(2);

            foreach (var fullName in allNames)
                DrawEnumCard(fullName);

            GUILayout.Space(4);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEnumCard(string fullName)
        {
            _foldouts.TryAdd(fullName, false);

            var snap = StableEnumRegistry.GetSnapshot(fullName);
            int count = snap?.Count ?? 0;
            string shortName = SimpleName(fullName);

            EditorGUILayout.BeginVertical(_cardStyle);

            // Header row
            EditorGUILayout.BeginHorizontal();

            // Foldout: allocate only as much space as the text width requires
            float nameW = _foldoutBold.CalcSize(new GUIContent(shortName)).x;
            var foldRect = GUILayoutUtility.GetRect(nameW, EditorGUIUtility.singleLineHeight, GUILayout.Width(nameW));
            _foldouts[fullName] = EditorGUI.Foldout(foldRect, _foldouts[fullName], shortName, true, _foldoutBold);

            int dot = fullName.LastIndexOf('.');
            if (dot >= 0)
                GUILayout.Label($"({fullName[..dot]})", _nsLabel);

            GUILayout.FlexibleSpace();

            // Flags badge (placed before member count)
            var enumType = FindEnumType(fullName);
            if (enumType != null && enumType.IsDefined(typeof(FlagsAttribute), false))
            {
                DrawPill("Flags", PillOrange, PillOrangeTxt);
                GUILayout.Space(4);
            }

            // Member count pill
            DrawPill($"{count} members", PillBlue, PillBlueTxt, 90f);

            EditorGUILayout.EndHorizontal();

            // Member list
            if (_foldouts[fullName] && snap != null)
            {
                GUILayout.Space(4);
                DrawDivider();
                GUILayout.Space(4);

                foreach (var kv in snap.OrderBy(x => x.Value))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(kv.Key, _memberLabel);
                    GUILayout.Label(kv.Value.ToString(), _memberValue, GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        // ── Log Panel ──

        private void DrawLogPanel()
        {
            var log = StableEnumWatcher.MigrationLog;

            if (log.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No migration events yet.", _emptyLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            _scrollLog = EditorGUILayout.BeginScrollView(_scrollLog);
            GUILayout.Space(4);

            for (int i = log.Count - 1; i >= 0; i--)
            {
                string entry = log[i];

                if (!string.IsNullOrEmpty(_logSearch)
                    && entry.IndexOf(_logSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                DrawLogEntry(entry);
            }

            GUILayout.Space(4);
            EditorGUILayout.EndScrollView();
        }

        private void DrawLogEntry(string entry)
        {
            // Determine color by content
            Color c;
            string icon;

            if (entry.Contains("migration complete"))
            {
                c = LogMigrate;
                icon = "▸";
            }
            else if (entry.Contains("change detected"))
            {
                c = LogWarn;
                icon = "●";
            }
            else
            {
                c = LogInfo;
                icon = "○";
            }

            var prev = GUI.color;
            GUI.color = c;
            GUILayout.Label($"  {icon}  {entry}", _logStyle);
            GUI.color = prev;
        }

        // ── Helpers ──

        private void DrawDivider()
        {
            var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, DividerColor);
        }

        private void DrawTag(string text)
        {
            var content = new GUIContent(text);
            var size = _tagStyle.CalcSize(content);
            var rect = GUILayoutUtility.GetRect(size.x + 8, 18);
            EditorGUI.DrawRect(rect, TagBg);
            GUI.Label(rect, content, _tagStyle);
        }

        private void DrawPill(string text, Color bg, Color textColor, float fixedWidth = 0f)
        {
            var content = new GUIContent(text);
            float w = fixedWidth > 0f ? fixedWidth : _tagStyle.CalcSize(content).x + 12;
            var rect = GUILayoutUtility.GetRect(w, 18);

            // Background
            EditorGUI.DrawRect(rect, bg);

            // Text
            var prev = _tagStyle.normal.textColor;
            _tagStyle.normal.textColor = textColor;
            GUI.Label(rect, content, _tagStyle);
            _tagStyle.normal.textColor = prev;
        }

        private static string SimpleName(string fullName)
        {
            int dot = fullName.LastIndexOf('.');
            return dot >= 0 ? fullName[(dot + 1)..] : fullName;
        }

        private static Type FindEnumType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null && t.IsEnum) return t;
            }
            return null;
        }
    }
}

#endif
