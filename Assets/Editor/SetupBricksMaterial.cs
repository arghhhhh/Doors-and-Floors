// SetupBricksMaterial.cs — run once via Tools > ZedGames > Setup Bricks Material
// Creates the Bricks material with world-space UV shader and applies it to all
// Floor_* and Wall_* objects in the active scene.
using UnityEditor;
using UnityEngine;

public static class SetupBricksMaterial
{
    private const string ShaderPath   = "Assets/Shaders/WorldSpaceUnlit.shader";
    private const string TexturePath  = "Assets/Media/bricks.jpg";
    private const string MaterialPath = "Assets/Materials/Bricks.mat";

    // Arena is ~14 units wide, ~15 units tall.
    // Tiling of (2, 2) gives bricks ~0.5 world-unit tall — adjust as desired.
    private static readonly Vector2 Tiling = new Vector2(2f, 2f);

    private static readonly string[] TargetNames =
    {
        "Floor_0", "Floor_1", "Floor_2", "Floor_3",
        "Floor_4", "Floor_5", "Floor_6",
        "Wall_Left", "Wall_Right"
    };

    [MenuItem("Tools/ZedGames/Setup Bricks Material")]
    public static void Run()
    {
        // --- 1. Load shader ---
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogError($"[SetupBricksMaterial] Shader not found at {ShaderPath}");
            return;
        }

        // --- 2. Load texture ---
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogError($"[SetupBricksMaterial] Texture not found at {TexturePath}");
            return;
        }

        // --- 3. Create or load material ---
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            Debug.Log($"[SetupBricksMaterial] Created material at {MaterialPath}");
        }
        else
        {
            mat.shader = shader;
            Debug.Log($"[SetupBricksMaterial] Updated existing material at {MaterialPath}");
        }

        // --- 4. Configure material properties ---
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetVector("_Tiling", new Vector4(Tiling.x, Tiling.y, 0f, 0f));
        mat.SetVector("_Offset", Vector4.zero);

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // --- 5. Apply to scene objects ---
        int applied = 0;
        foreach (string goName in TargetNames)
        {
            GameObject go = GameObject.Find(goName);
            if (go == null)
            {
                Debug.LogWarning($"[SetupBricksMaterial] GameObject '{goName}' not found in scene.");
                continue;
            }

            Renderer rend = go.GetComponent<Renderer>();
            if (rend == null)
            {
                Debug.LogWarning($"[SetupBricksMaterial] No Renderer on '{goName}'.");
                continue;
            }

            Undo.RecordObject(rend, "Apply Bricks Material");
            rend.sharedMaterial = mat;
            EditorUtility.SetDirty(go);
            applied++;
            Debug.Log($"[SetupBricksMaterial] Applied Bricks material to '{goName}'.");
        }

        // --- 6. Save scene ---
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[SetupBricksMaterial] Done. Material applied to {applied}/{TargetNames.Length} objects.");
    }
}
