using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

/// <summary>
/// One-shot editor utility to set up App UI (PanelSettings + UIDocument) for
/// StartScreen and GameScreen scenes.
/// Run via: Tools > App UI > Setup All Scenes
/// </summary>
public static class AppUISetup
{
    private const string PanelSettingsPath = "Assets/UI/Settings/GamePanelSettings.asset";
    private const string AppUIThemePath   = "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI.tss";
    private const string StartScreenPath  = "Assets/Scenes/StartScreen.unity";
    private const string GameScreenPath   = "Assets/Scenes/GameScreen.unity";
    private const string StartScreenUxml  = "Assets/UI/UXML/StartScreen.uxml";
    private const string GameScreenUxml   = "Assets/UI/UXML/GameScreen.uxml";
    private const string HighScoreUxml    = "Assets/UI/UXML/HighScoreEntry.uxml";

    [MenuItem("Tools/App UI/Setup All Scenes")]
    public static void SetupAllScenes()
    {
        // ----------------------------------------------------------------
        // Step 1 – create PanelSettings asset
        // ----------------------------------------------------------------
        PanelSettings panelSettings = CreateOrLoadPanelSettings();
        if (panelSettings == null)
        {
            Debug.LogError("[AppUISetup] Failed to create/load PanelSettings – aborting.");
            return;
        }

        // ----------------------------------------------------------------
        // Step 2 – StartScreen
        // ----------------------------------------------------------------
        SetupStartScreen(panelSettings);

        // ----------------------------------------------------------------
        // Step 3 – GameScreen
        // ----------------------------------------------------------------
        SetupGameScreen(panelSettings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AppUISetup] Done. Both scenes have been set up.");
    }

    // --------------------------------------------------------------------
    // PanelSettings
    // --------------------------------------------------------------------
    private static PanelSettings CreateOrLoadPanelSettings()
    {
        // Return existing asset if it's already there
        PanelSettings existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (existing != null)
        {
            Debug.Log("[AppUISetup] PanelSettings already exists – reusing.");
            ConfigurePanelSettings(existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // Create directory if needed
        string dir = Path.GetDirectoryName(PanelSettingsPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            // Create each path segment
            string[] parts = dir.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        PanelSettings ps = ScriptableObject.CreateInstance<PanelSettings>();
        ConfigurePanelSettings(ps);
        AssetDatabase.CreateAsset(ps, PanelSettingsPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AppUISetup] Created PanelSettings at {PanelSettingsPath}");
        return ps;
    }

    private static void ConfigurePanelSettings(PanelSettings ps)
    {
        ps.scaleMode             = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution   = new Vector2Int(1920, 1080);
        ps.match                 = 0.5f;

        // Assign the App UI dark theme TSS
        ThemeStyleSheet tss = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AppUIThemePath);
        if (tss != null)
        {
            ps.themeStyleSheet = tss;
            Debug.Log("[AppUISetup] App UI theme TSS assigned.");
        }
        else
        {
            Debug.LogWarning($"[AppUISetup] Could not find App UI theme TSS at: {AppUIThemePath}. " +
                             "The package may not be resolved yet. Assign it manually after package import.");
        }
    }

    // --------------------------------------------------------------------
    // StartScreen scene
    // --------------------------------------------------------------------
    private static void SetupStartScreen(PanelSettings panelSettings)
    {
        var scene = EditorSceneManager.OpenScene(StartScreenPath, OpenSceneMode.Single);

        // Remove Canvas (and its children) if present
        DeleteGameObjectByName("Canvas");
        DeleteGameObjectByName("EventSystem");

        // Find the StartScreenManager GameObject
        GameObject smGO = FindGameObjectWithComponent("StartScreenManager");
        if (smGO == null)
        {
            Debug.LogError("[AppUISetup] Could not find a GameObject with StartScreenManager in StartScreen.");
        }
        else
        {
            AddOrConfigureUIDocument(smGO, panelSettings, StartScreenUxml);
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AppUISetup] StartScreen scene saved.");
    }

    // --------------------------------------------------------------------
    // GameScreen scene
    // --------------------------------------------------------------------
    private static void SetupGameScreen(PanelSettings panelSettings)
    {
        var scene = EditorSceneManager.OpenScene(GameScreenPath, OpenSceneMode.Single);

        // Remove Canvas (and its children) if present
        DeleteGameObjectByName("Canvas");
        DeleteGameObjectByName("EventSystem");

        // Find a GameObject with UIManager, or find GameScreenManager and add UIManager to it,
        // or find GameManager as a fallback host.
        GameObject uiManagerGO = FindGameObjectWithComponent("UIManager");
        if (uiManagerGO == null)
        {
            // UIManager doesn't exist yet — add it to GameScreenManager (the UI-facing orchestrator)
            uiManagerGO = GameObject.Find("GameScreenManager");
            if (uiManagerGO == null)
                uiManagerGO = GameObject.Find("GameManager");

            if (uiManagerGO == null)
            {
                // Last resort: create a dedicated GameObject
                uiManagerGO = new GameObject("UIManager");
                Debug.Log("[AppUISetup] Created new UIManager GameObject.");
            }
            else
            {
                Debug.Log($"[AppUISetup] Adding UIManager component to '{uiManagerGO.name}'.");
            }

            // Add UIManager component
            uiManagerGO.AddComponent<UIManager>();
        }

        AddOrConfigureUIDocument(uiManagerGO, panelSettings, GameScreenUxml);

        // Wire highScoreEntryTemplate on UIManager
        VisualTreeAsset highScoreVta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HighScoreUxml);
        if (highScoreVta != null)
        {
            var uiManager = uiManagerGO.GetComponent<UIManager>();
            if (uiManager != null)
            {
                // highScoreEntryTemplate is [SerializeField] private — use SerializedObject
                SerializedObject so = new SerializedObject(uiManager);
                SerializedProperty prop = so.FindProperty("highScoreEntryTemplate");
                if (prop != null)
                {
                    prop.objectReferenceValue = highScoreVta;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[AppUISetup] UIManager.highScoreEntryTemplate set.");
                }
                else
                {
                    Debug.LogWarning("[AppUISetup] Could not find serialized property 'highScoreEntryTemplate' on UIManager.");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[AppUISetup] HighScoreEntry.uxml not found at {HighScoreUxml}");
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AppUISetup] GameScreen scene saved.");
    }

    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------
    private static void AddOrConfigureUIDocument(GameObject go, PanelSettings panelSettings, string uxmlPath)
    {
        UIDocument doc = go.GetComponent<UIDocument>();
        if (doc == null)
        {
            doc = go.AddComponent<UIDocument>();
            Debug.Log($"[AppUISetup] Added UIDocument to {go.name}.");
        }

        doc.panelSettings = panelSettings;

        VisualTreeAsset vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        if (vta != null)
        {
            doc.visualTreeAsset = vta;
            Debug.Log($"[AppUISetup] UIDocument on {go.name}: sourceAsset set to {uxmlPath}");
        }
        else
        {
            Debug.LogWarning($"[AppUISetup] UXML not found at {uxmlPath} – assign manually.");
        }

        EditorUtility.SetDirty(go);
    }

    private static void DeleteGameObjectByName(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            Object.DestroyImmediate(go);
            Debug.Log($"[AppUISetup] Deleted GameObject '{name}'.");
        }
    }

    private static GameObject FindGameObjectWithComponent(string componentTypeName)
    {
        // We find by component type name using FindObjectsOfType on Object
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == componentTypeName)
                    return go;
            }
        }
        return null;
    }
}
