using UnityEditor;
using UnityEngine;

/// <summary>
/// Patches ModelImporter clip settings on the four hazmat animation FBXes so that
/// all root bone position (XZ and Y) is baked into the skeletal pose instead of
/// being applied to the character's Transform. This makes the Rigidbody the sole
/// authority on character position and eliminates the visual "skip" when
/// transitioning between JumpUp and JumpDown.
///
/// Run via: Tools > Fix Animation Root Motion
/// </summary>
public static class FixAnimationRootMotion
{
    const string MODEL_BASE = "Assets/Models/Meshy_AI_hazmat_man_1_biped_separate/Meshy_AI_hazmat_man_1_biped/";

    // The four FBXes to patch. The Regular Jump FBX contains two clips (JumpUp + JumpDown).
    static readonly string[] TargetFbxPaths = new[]
    {
        MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Idle_02_withSkin.fbx",
        MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Walking_withSkin.fbx",
        MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Running_withSkin.fbx",
        MODEL_BASE + "Meshy_AI_hazmat_man_1_biped_Animation_Regular_Jump_withSkin.fbx",
    };

    [MenuItem("Tools/Fix Animation Root Motion")]
    public static void Run()
    {
        int fbxPatched = 0;
        int clipsPatched = 0;

        foreach (string fbxPath in TargetFbxPaths)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[FixAnimationRootMotion] Could not get ModelImporter for: {fbxPath}");
                continue;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;

            // If clipAnimations is empty Unity hasn't had custom clips authored yet,
            // fall back to the default clips so we can still patch them.
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[FixAnimationRootMotion] No clips found in: {fbxPath}");
                continue;
            }

            bool dirty = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool changed = false;

                // Bake XZ root position into pose (Rigidbody handles horizontal movement)
                if (!clip.lockRootPositionXZ)   { clip.lockRootPositionXZ   = true;  changed = true; }
                // Bake Y root position into pose (Rigidbody handles vertical movement / jump arc)
                if (!clip.lockRootHeightY)       { clip.lockRootHeightY      = true;  changed = true; }
                // Do not keep the original root XZ offset from the clip
                if (clip.keepOriginalPositionXZ) { clip.keepOriginalPositionXZ = false; changed = true; }
                // Do not keep the original root Y offset from the clip
                if (clip.keepOriginalPositionY)  { clip.keepOriginalPositionY  = false; changed = true; }

                if (changed)
                {
                    Debug.Log($"[FixAnimationRootMotion] Patched clip '{clip.name}' in {System.IO.Path.GetFileName(fbxPath)}");
                    clipsPatched++;
                    dirty = true;
                }
                else
                {
                    Debug.Log($"[FixAnimationRootMotion] Clip '{clip.name}' in {System.IO.Path.GetFileName(fbxPath)} already correct, skipping.");
                }
            }

            if (dirty)
            {
                // Write the modified clips back — required before SaveAndReimport
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                fbxPatched++;
                Debug.Log($"[FixAnimationRootMotion] Reimported: {fbxPath}");
            }
        }

        AssetDatabase.SaveAssets();

        if (fbxPatched > 0)
            Debug.Log($"[FixAnimationRootMotion] Done. Patched {clipsPatched} clip(s) across {fbxPatched} FBX file(s). Root motion is now fully baked into pose — Rigidbody is the sole position authority.");
        else
            Debug.Log("[FixAnimationRootMotion] Done. All clips were already correctly configured.");
    }
}
