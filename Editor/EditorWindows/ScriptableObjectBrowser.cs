using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace UrbanFox.ScriptableObjectBrowser.Editor
{
    public class ScriptableObjectBrowser : EditorWindow
    {
        [Serializable]
        public struct EditorData
        {
            public string TypeSearch;
            public string AssetSearch;
            public bool HideUnityTypes;
            public bool DisplayTypeFullName;
            public string SelectedTypeFullName;
            public string SelectedTypeShortName;
            public UnityEngine.Object SelectedAsset;
        }

        private static GUIStyle m_buttonAlightLeftStyle;
        private static GUIStyle m_divider;

        [SerializeField]
        private EditorData m_editorData;

        private List<Type> m_allTypes;
        private List<UnityEngine.Object> m_candidateAssets;
        private Vector2 m_masterScroll;
        private Vector2 m_typeSelectorScroll;
        private Vector2 m_assetSelectorScroll;
        private Vector2 m_assetInspectorScroll;
        private int m_candidateTypeCount = 0;
        private int m_candidateAssetCount = 0;

        private static string EditorDataKey => $"{Application.companyName}/{Application.productName}/{typeof(ScriptableObjectBrowser).Name}/{nameof(m_editorData)}";

        private static GUIStyle ButtonAlignLeftStyle
        {
            get
            {
                if (m_buttonAlightLeftStyle == null)
                {
                    m_buttonAlightLeftStyle = new GUIStyle(GUI.skin.button)
                    {
                        alignment = TextAnchor.MiddleLeft
                    };
                }
                return m_buttonAlightLeftStyle;
            }
        }

        private static GUIStyle Divider
        {
            get
            {
                if (m_divider == null)
                {
                    var whiteTexture = new Texture2D(1, 1);
                    whiteTexture.SetPixel(0, 0, Color.white);
                    whiteTexture.Apply();
                    m_divider = new GUIStyle();
                    m_divider.normal.background = whiteTexture;
                    m_divider.margin = new RectOffset(2, 2, 2, 2);
                }
                return m_divider;
            }
        }

        private static void DrawHorizontalLine(float height, Color color)
        {
            Divider.fixedHeight = height;
            var cachedGUIColor = GUI.color;
            GUI.color = color;
            GUILayout.Box(GUIContent.none, Divider);
            GUI.color = cachedGUIColor;
        }

        private static string SearchBox(string label, string text, string clearText = "Clear", float clearButtonWidth = 60)
        {
            GUILayout.BeginHorizontal();
            text = EditorGUILayout.TextField(label, text);
            if (GUILayout.Button(clearText, GUILayout.Width(clearButtonWidth)))
            {
                text = string.Empty;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();
            return text;
        }

        private static bool ColoredButton(GUIContent content, Color color, GUIStyle style, params GUILayoutOption[] options)
        {
            var cachedBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            if (GUILayout.Button(content, style, options))
            {
                GUI.backgroundColor = cachedBackgroundColor;
                return true;
            }
            GUI.backgroundColor = cachedBackgroundColor;
            return false;
        }

        private static bool ColoredButton(GUIContent content, Color color, params GUILayoutOption[] options)
        {
            return ColoredButton(content, color, GUI.skin.button, options);
        }

        private static List<UnityEngine.Object> GetInstancesOfType(string typeName)
        {
            var results = new List<UnityEngine.Object>();
            if (typeName != null)
            {
                var guids = AssetDatabase.FindAssets($"t:{typeName}");
                if (guids != null && guids.Length > 0)
                {
                    for (int i = 0; i < guids.Length; i++)
                    {
                        results.Add(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guids[i])));
                    }
                }
            }
            return results;
        }

        private static string GetCurrentFolderInProjectPanel()
        {
            var path = "Assets/";
            foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                    break;
                }
            }
            return path;
        }

        [MenuItem("OwO/Window/ScriptableObject Browser")]
        private static void ShowWindow()
        {
            var window = GetWindow<ScriptableObjectBrowser>();
            window.titleContent = new GUIContent("Scriptable Browser");
            window.minSize = new Vector2(150, 250);
            window.Show();
        }

        [MenuItem("Window/OwO/ScriptableObject Browser")]
        private static void ShowWindowAlternate()
        {
            ShowWindow();
        }

        private void OnEnable()
        {
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            m_allTypes = new List<Type>();
            foreach (var assembly in allAssemblies)
            {
                var newTypes = assembly.GetTypes().Where(type => type.IsClass

                    // Include ScriptableObject classes
                    && type.IsSubclassOf(typeof(ScriptableObject))

                    // Exclude abstract and generic classes (they cannot be instantiated)
                    && !type.IsAbstract
                    && !type.IsGenericType

                    // Exclude special editor windows
                    && !type.IsSubclassOf(typeof(UnityEditor.Editor))
                    && !type.IsSubclassOf(typeof(EditorWindow)));

                m_allTypes.AddRange(newTypes);
            }
            m_editorData = EditorPrefs.HasKey(EditorDataKey) ? JsonUtility.FromJson<EditorData>(EditorPrefs.GetString(EditorDataKey)) : new EditorData()
            {
                TypeSearch = string.Empty,
                AssetSearch = string.Empty,
            };
            if (string.IsNullOrEmpty(m_editorData.SelectedTypeFullName))
            {
                m_editorData.SelectedAsset = null;
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(EditorDataKey, JsonUtility.ToJson(m_editorData));
        }

        private void OnGUI()
        {
            m_masterScroll = GUILayout.BeginScrollView(m_masterScroll);
            GUILayout.BeginHorizontal();
            DrawTypeSelector();
            EditorGUILayout.Space();
            DrawAssetSelector();
            EditorGUILayout.Space();
            DrawAssetInspector();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        private void DrawTypeSelector()
        {
            GUILayout.BeginVertical(GUILayout.MaxWidth(200));
            GUILayout.Label("Search for Types", EditorStyles.boldLabel);
            m_editorData.TypeSearch = SearchBox(string.Empty, m_editorData.TypeSearch);
            m_editorData.HideUnityTypes = EditorGUILayout.Toggle("Hide Unity Types", m_editorData.HideUnityTypes);
            m_editorData.DisplayTypeFullName = EditorGUILayout.Toggle("Display Type Full Name", m_editorData.DisplayTypeFullName);
            EditorGUILayout.Space();
            GUILayout.Label($"ScriptableObjects ({m_candidateTypeCount})", EditorStyles.boldLabel);
            m_candidateTypeCount = 0;
            DrawHorizontalLine(1, Color.gray);
            m_typeSelectorScroll = GUILayout.BeginScrollView(m_typeSelectorScroll);
            if (m_allTypes != null && m_allTypes.Count > 0)
            {
                var cachedTypeSearch = m_editorData.TypeSearch.ToLower();
                foreach (var type in m_allTypes)
                {
                    if (type.FullName.ToLower().Contains(cachedTypeSearch))
                    {
                        if (!type.FullName.Contains("Unity") || !m_editorData.HideUnityTypes)
                        {
                            m_candidateTypeCount++;
                            GUILayout.BeginHorizontal();
                            var buttonGUIContent = new GUIContent(m_editorData.DisplayTypeFullName ? type.FullName : type.Name, type.FullName);
                            if (ColoredButton(buttonGUIContent, m_editorData.SelectedTypeFullName == type.FullName ? Color.yellow : Color.white, ButtonAlignLeftStyle, GUILayout.Width(205)))
                            {
                                if (m_editorData.SelectedTypeFullName != type.FullName)
                                {
                                    m_editorData.SelectedAsset = null;
                                }
                                m_editorData.SelectedTypeFullName = type.FullName;
                                m_editorData.SelectedTypeShortName = type.Name;
                            }
                            if (ColoredButton(new GUIContent("New"), Color.green, GUILayout.Width(45)))
                            {
                                var newInstance = CreateInstance(type);
                                var instancePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(GetCurrentFolderInProjectPanel(), $"{type.Name}.asset"));
                                ProjectWindowUtil.CreateAsset(newInstance, instancePath);
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"No ScriptableObject types can be found.", MessageType.Info);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawAssetSelector()
        {
            GUILayout.BeginVertical(GUILayout.MaxWidth(200));
            GUILayout.Label("Search Assets from the Selected Type", EditorStyles.boldLabel);
            m_editorData.AssetSearch = SearchBox(string.Empty, m_editorData.AssetSearch);
            EditorGUILayout.Space();
            if (string.IsNullOrEmpty(m_editorData.SelectedTypeFullName))
            {
                EditorGUILayout.HelpBox($"Select a type on the left column first.", MessageType.Info);
            }
            else
            {
                // Potentially expensive function to be called every frame
                m_candidateAssets = GetInstancesOfType(m_editorData.SelectedTypeFullName);

                GUILayout.Label($"{m_editorData.SelectedTypeShortName} ({m_candidateAssetCount})", EditorStyles.boldLabel);
                m_candidateAssetCount = 0;
                DrawHorizontalLine(1, Color.gray);
                m_assetSelectorScroll = GUILayout.BeginScrollView(m_assetSelectorScroll);
                if (m_candidateAssets != null && m_candidateAssets.Count > 0)
                {
                    var cachedAssetSearch = m_editorData.AssetSearch.ToLower();
                    foreach (var asset in m_candidateAssets)
                    {
                        // An asset might be deleted when the window is opened - make sure to check for null reference here
                        if (asset != null)
                        {
                            if (asset.name.ToLower().Contains(cachedAssetSearch))
                            {
                                m_candidateAssetCount++;
                                GUILayout.BeginHorizontal();
                                if (ColoredButton(new GUIContent(asset.name, AssetDatabase.GetAssetPath(asset)), m_editorData.SelectedAsset == asset ? Color.yellow : Color.white, ButtonAlignLeftStyle, GUILayout.Width(205)))
                                {
                                    m_editorData.SelectedAsset = asset;
                                }
                                if (GUILayout.Button("Ping", GUILayout.Width(45)))
                                {
                                    EditorGUIUtility.PingObject(asset);
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"No asset instances of {m_editorData.SelectedTypeShortName} can be found.", MessageType.Info);
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private void DrawAssetInspector()
        {
            GUILayout.BeginVertical();
            if (m_editorData.SelectedAsset == null)
            {
                EditorGUILayout.HelpBox($"Select a type and an asset from the left columns first.", MessageType.Info);
            }
            else
            {
                GUILayout.Label($"{m_editorData.SelectedAsset.name}", EditorStyles.boldLabel);
                DrawHorizontalLine(1, Color.gray);
                m_assetInspectorScroll = GUILayout.BeginScrollView(m_assetInspectorScroll);
                var editor = UnityEditor.Editor.CreateEditor(m_editorData.SelectedAsset);
                if (editor != null)
                {
                    editor.OnInspectorGUI();
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }
    }
}
