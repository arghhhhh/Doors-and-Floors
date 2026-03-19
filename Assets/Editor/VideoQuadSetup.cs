using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Editor utility: creates a looping VideoPlayer Quad in the StartScreen scene
/// facing the Main Camera. Run via Tools > Setup Video Quad.
/// </summary>
public static class VideoQuadSetup
{
    private const string VideoClipPath = "Assets/Media/hands_up_gesture.mp4";
    private const string QuadName = "VideoQuad";

    [MenuItem("Tools/Setup Video Quad")]
    public static void Run()
    {
        // --- 1. Load the video clip ---
        var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoClipPath);
        if (clip == null)
        {
            Debug.LogError($"[VideoQuadSetup] Could not find VideoClip at '{VideoClipPath}'. " +
                           "Make sure the file has been imported.");
            return;
        }

        // --- 2. Remove any pre-existing quad ---
        var existing = GameObject.Find(QuadName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
            Debug.Log("[VideoQuadSetup] Removed existing VideoQuad.");
        }

        // --- 3. Create the Quad ---
        // Camera: position (0,1,-10), forward +Z, FOV 60, aspect ~1.6
        // We place the quad 8 units in front of the camera: z = -10 + 8 = -2
        // At distance 8, visible height = 2 * 8 * tan(30°) ≈ 9.24 units.
        // 16:9 at ~50% of screen height: height=4.5, width=8.0 → scale (8, 4.5, 1)
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = QuadName;
        Undo.RegisterCreatedObjectUndo(quad, "Create VideoQuad");

        // Position: same Y as camera, 8 units in front (camera looks +Z)
        quad.transform.position = new Vector3(0f, 1f, -2f);

        // Rotate 180° on Y so the quad's front face (+Z) points back toward the camera
        quad.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // Scale to 16:9 — fills roughly half the screen height comfortably
        quad.transform.localScale = new Vector3(8f, 4.5f, 1f);

        // --- 4. Create a new unlit material for the video output ---
        // Use URP/Unlit so there's no lighting baked onto the video surface
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.name = "VideoQuadMat";
        AssetDatabase.CreateAsset(mat, "Assets/Materials/VideoQuadMat.mat");

        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;

        // --- 5. Add and configure the VideoPlayer ---
        var vp = quad.AddComponent<VideoPlayer>();

        vp.source           = VideoSource.VideoClip;
        vp.clip             = clip;
        vp.isLooping        = true;
        vp.playOnAwake      = true;
        vp.waitForFirstFrame = true;

        // Render to the Quad's material main texture
        vp.renderMode       = VideoRenderMode.MaterialOverride;
        vp.targetMaterialRenderer = renderer;
        vp.targetMaterialProperty  = "_BaseMap"; // URP Unlit main texture property

        // Prevent audio from playing (video is a gesture, no audio needed — toggle if desired)
        vp.audioOutputMode  = VideoAudioOutputMode.None;

        // --- 6. Save the scene ---
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[VideoQuadSetup] Done! Created '{QuadName}' at (0,1,-2) with VideoPlayer " +
                  $"targeting '{VideoClipPath}'. Save the scene to persist.");
    }
}
 
