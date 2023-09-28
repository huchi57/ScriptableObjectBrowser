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
        private static GUIStyle m_buttonAlightLeftStyle;
        private static GUIStyle m_divider;

        [SerializeField] private string m_typeSearch = string.Empty;
        [SerializeField] private string m_assetSearch = string.Empty;
        [SerializeField] private bool m_hideUnityTypes;
        [SerializeField] private Type m_selectedType;
        [SerializeField] private UnityEngine.Object m_selectedAsset;

        private List<Type> m_allTypes;
        private List<UnityEngine.Object> m_candidateAssets;
        private Vector2 m_masterScroll;
        private Vector2 m_typeSelectorScroll;
        private Vector2 m_assetSelectorScroll;
        private Vector2 m_assetInspectorScroll;
        private int m_candidateTypeCount = 0;
        private int m_candidateAssetCount = 0;

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

        private static List<UnityEngine.Object> GetInstancesOfType(Type type)
        {
            var results = new List<UnityEngine.Object>();
            if (type != null)
            {
                var guids = AssetDatabase.FindAssets($"t:{type.Name}");
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
            if (m_selectedType == null)
            {
                m_selectedAsset = null;
            }
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
            m_typeSearch = SearchBox(string.Empty, m_typeSearch);
            m_hideUnityTypes = EditorGUILayout.Toggle("Hide Unity Types", m_hideUnityTypes);
            EditorGUILayout.Space();
            GUILayout.Label($"ScriptableObjects ({m_candidateTypeCount})", EditorStyles.boldLabel);
            m_candidateTypeCount = 0;
            DrawHorizontalLine(1, Color.gray);
            m_typeSelectorScroll = GUILayout.BeginScrollView(m_typeSelectorScroll);
            if (m_allTypes != null && m_allTypes.Count > 0)
            {
                var cachedTypeSearch = m_typeSearch.ToLower();
                foreach (var type in m_allTypes)
                {
                    if (type.FullName.ToLower().Contains(cachedTypeSearch))
                    {
                        if (!type.FullName.Contains("Unity") || !m_hideUnityTypes)
                        {
                            m_candidateTypeCount++;
                            GUILayout.BeginHorizontal();
                            if (ColoredButton(new GUIContent(type.Name, type.FullName), m_selectedType == type ? Color.yellow : Color.white, ButtonAlignLeftStyle, GUILayout.Width(205)))
                            {
                                if (m_selectedType != type)
                                {
                                    m_selectedAsset = null;
                                }
                                m_selectedType = type;
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
            m_assetSearch = SearchBox(string.Empty, m_assetSearch);
            EditorGUILayout.Space();
            if (m_selectedType == null)
            {
                EditorGUILayout.HelpBox($"Select a type on the left column first.", MessageType.Info);
            }
            else
            {
                // Potentially expensive function to be called every frame
                m_candidateAssets = GetInstancesOfType(m_selectedType);

                GUILayout.Label($"{m_selectedType.Name} ({m_candidateAssetCount})", EditorStyles.boldLabel);
                m_candidateAssetCount = 0;
                DrawHorizontalLine(1, Color.gray);
                m_assetSelectorScroll = GUILayout.BeginScrollView(m_assetSelectorScroll);
                if (m_candidateAssets != null && m_candidateAssets.Count > 0)
                {
                    var cachedAssetSearch = m_assetSearch.ToLower();
                    foreach (var asset in m_candidateAssets)
                    {
                        // An asset might be deleted when the window is opened - make sure to check for null reference here
                        if (asset != null)
                        {
                            if (asset.name.ToLower().Contains(cachedAssetSearch))
                            {
                                m_candidateAssetCount++;
                                GUILayout.BeginHorizontal();
                                if (ColoredButton(new GUIContent(asset.name), m_selectedAsset == asset ? Color.yellow : Color.white, ButtonAlignLeftStyle, GUILayout.Width(205)))
                                {
                                    m_selectedAsset = asset;
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
                    EditorGUILayout.HelpBox($"No asset instances of {m_selectedType.Name} can be found.", MessageType.Info);
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private void DrawAssetInspector()
        {
            GUILayout.BeginVertical();
            if (m_selectedAsset == null)
            {
                EditorGUILayout.HelpBox($"Select a type and an asset from the left columns first.", MessageType.Info);
            }
            else
            {
                GUILayout.Label($"{m_selectedAsset.name}", EditorStyles.boldLabel);
                DrawHorizontalLine(1, Color.gray);
                m_assetInspectorScroll = GUILayout.BeginScrollView(m_assetInspectorScroll);
                var editor = UnityEditor.Editor.CreateEditor(m_selectedAsset);
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
