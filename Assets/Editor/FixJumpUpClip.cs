using UnityEditor;
using UnityEngine;

/// <summary>
/// Two targeted fixes for the Hazmat Man Regular_Jump FBX:
///
///   Fix 1 — JumpUp start frame: changes firstFrame from 0 to 5 to skip the
///            idle-like pose at the start of the clip.
///
///   Fix 2 — Root Y baking: ensures lockRootHeightY=true and keepOriginalPositionY=false
///            on BOTH JumpUp and JumpDown so vertical offset is fully baked into the
///            skeletal pose and the Rigidbody remains the sole Y authority.
///
/// Run via: Tools / Fix JumpUp Clip (Frame + Root Y)
/// </summary>
public static class FixJumpUpClip
{
    const string FBX_PATH =
        "Assets/Models/Meshy_AI_hazmat_man_1_biped_separate/Meshy_AI_hazmat_man_1_biped/" +
        "Meshy_AI_hazmat_man_1_biped_Animation_Regular_Jump_withSkin.fbx";

    [MenuItem("Tools/Fix JumpUp Clip (Frame + Root Y)")]
    public static void Run()
    {
        var importer = AssetImporter.GetAtPath(FBX_PATH) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[FixJumpUpClip] Could not load ModelImporter for:\n  {FBX_PATH}");
            return;
        }

        // Prefer authored clips; fall back to defaultClipAnimations if none are defined.
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        if (clips == null || clips.Length == 0)
        {
            Debug.LogError("[FixJumpUpClip] No clip animations found in the FBX.");
            return;
        }

        bool dirty = false;

        foreach (var clip in clips)
        {
            // ---------- Fix 1: JumpUp start frame ----------
            if (clip.name == "JumpUp")
            {
                if (clip.firstFrame != 5f)
                {
                    Debug.Log($"[FixJumpUpClip] JumpUp firstFrame: {clip.firstFrame} → 5");
                    clip.firstFrame = 5f;
                    dirty = true;
                }
                else
                {
                    Debug.Log("[FixJumpUpClip] JumpUp firstFrame is already 5 — no change needed.");
                }
            }

            // ---------- Fix 2: Root Y baking (applies to both JumpUp and JumpDown) ----------
            string label = clip.name;

            if (!clip.lockRootHeightY)
            {
                Debug.Log($"[FixJumpUpClip] {label}: lockRootHeightY false → true");
                clip.lockRootHeightY = true;
                dirty = true;
            }
            else
            {
                Debug.Log($"[FixJumpUpClip] {label}: lockRootHeightY already true — OK");
            }

            if (clip.keepOriginalPositionY)
            {
                Debug.Log($"[FixJumpUpClip] {label}: keepOriginalPositionY true → false");
                clip.keepOriginalPositionY = false;
                dirty = true;
            }
            else
            {
                Debug.Log($"[FixJumpUpClip] {label}: keepOriginalPositionY already false — OK");
            }

            if (clip.heightFromFeet)
            {
                Debug.Log($"[FixJumpUpClip] {label}: heightFromFeet true → false");
                clip.heightFromFeet = false;
                dirty = true;
            }
            else
            {
                Debug.Log($"[FixJumpUpClip] {label}: heightFromFeet already false — OK");
            }
        }

        if (dirty)
        {
            // Write modified clips back before SaveAndReimport — required by Unity API.
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            Debug.Log("[FixJumpUpClip] Done. Changes applied and FBX reimported.");
        }
        else
        {
            Debug.Log("[FixJumpUpClip] Done. All settings were already correct — no reimport needed.");
        }
    }
}
