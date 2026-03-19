using UnityEngine;
using UnityEngine.Video;
using UnityEditor;

public static class CreateVideoRenderTexture
{
    [MenuItem("ZedGames/Fix VideoQuad Color Banding")]
    public static void Fix()
    {
        const string rtPath = "Assets/Media/VideoQuad_RT.renderTexture";

        // --- 1. Create the RenderTexture asset ---
        RenderTexture rt = new RenderTexture(1080, 1080, 0, RenderTextureFormat.ARGBHalf);
        rt.name = "VideoQuad_RT";
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();

        AssetDatabase.CreateAsset(rt, rtPath);
        AssetDatabase.SaveAssets();

        // Reload from disk so we have a persistent reference
        rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if (rt == null)
        {
            Debug.LogError("[CreateVideoRenderTexture] Failed to load RenderTexture asset after creation.");
            return;
        }
        Debug.Log($"[CreateVideoRenderTexture] RenderTexture created at {rtPath}");

        // --- 2. Find VideoQuad in the scene ---
        GameObject videoQuad = GameObject.Find("VideoQuad");
        if (videoQuad == null)
        {
            Debug.LogError("[CreateVideoRenderTexture] Could not find 'VideoQuad' in the active scene.");
            return;
        }

        // --- 3. Configure VideoPlayer ---
        VideoPlayer vp = videoQuad.GetComponent<VideoPlayer>();
        if (vp == null)
        {
            Debug.LogError("[CreateVideoRenderTexture] VideoQuad has no VideoPlayer component.");
            return;
        }

        Undo.RecordObject(vp, "Fix VideoQuad Color Banding");
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        EditorUtility.SetDirty(vp);
        Debug.Log("[CreateVideoRenderTexture] VideoPlayer renderMode -> RenderTexture, targetTexture assigned.");

        // --- 4. Assign RenderTexture to the Quad material's _BaseMap ---
        Renderer rend = videoQuad.GetComponent<Renderer>();
        if (rend == null)
        {
            Debug.LogError("[CreateVideoRenderTexture] VideoQuad has no Renderer component.");
            return;
        }

        // Work on the actual material asset, not a temporary instance
        Material mat = rend.sharedMaterial;
        if (mat == null)
        {
            Debug.LogError("[CreateVideoRenderTexture] VideoQuad material is null.");
            return;
        }

        Undo.RecordObject(mat, "Fix VideoQuad Color Banding - Material");
        mat.SetTexture("_BaseMap", rt);
        EditorUtility.SetDirty(mat);
        Debug.Log($"[CreateVideoRenderTexture] Material '{mat.name}' _BaseMap assigned to RenderTexture.");

        // --- 5. Save the scene ---
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("[CreateVideoRenderTexture] Done. Scene saved.");
    }
}
 
