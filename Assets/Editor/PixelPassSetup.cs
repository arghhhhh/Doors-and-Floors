using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Experimental.Rendering.Universal;

/// <summary>
/// One-shot editor script to configure the pixelation renderer feature on the
/// URP-Balanced-Renderer.  Run via menu: Tools > Setup Pixel Pass.
/// Auto-destroys itself after running so it doesn't stay in the project.
/// </summary>
public static class PixelPassSetup
{
    private const string RendererPath = "Assets/Settings/URP-Balanced-Renderer.asset";
    private const string MaterialPath = "Assets/PixelPass/BlitMaterial.mat";

    [MenuItem("Tools/Setup Pixel Pass")]
    public static void Run()
    {
        // --- Load renderer asset ---
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null)
        {
            Debug.LogError("[PixelPassSetup] Could not load renderer at " + RendererPath);
            return;
        }

        // --- Load material ---
        var blitMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (blitMat == null)
        {
            Debug.LogError("[PixelPassSetup] Could not load BlitMaterial at " + MaterialPath);
            return;
        }

        // ---------------------------------------------------------------
        // Step A: Remove any existing BasicFeature instances on this renderer
        // ---------------------------------------------------------------
        var features = renderer.rendererFeatures;
        var toRemove = new List<ScriptableRendererFeature>();
        foreach (var f in features)
        {
            if (f is BasicFeature)
                toRemove.Add(f);
        }
        foreach (var f in toRemove)
        {
            features.Remove(f);
            AssetDatabase.RemoveObjectFromAsset(f);
        }

        // ---------------------------------------------------------------
        // Step B: Create and configure the BasicFeature instance
        // ---------------------------------------------------------------
        var feature = ScriptableObject.CreateInstance<BasicFeature>();
        feature.name = "BasicFeature";

        // Layer 3 = "Pixel"
        int pixelLayerIndex = 3;
        int pixelLayerMask = 1 << pixelLayerIndex;

        feature.settings.layerMask = pixelLayerMask;
        feature.settings.Event = UnityEngine.Rendering.Universal.RenderPassEvent.BeforeRenderingTransparents;
        feature.settings.blitMat = blitMat;
        feature.settings.pixelDensity = 10f;
        blitMat.SetFloat("_PixelDensity", 10f);

        // Add the feature as a sub-asset of the renderer
        AssetDatabase.AddObjectToAsset(feature, renderer);
        features.Add(feature);

        // ---------------------------------------------------------------
        // Step C: Exclude "Pixel" layer (bit 3 = value 8) from both masks
        // 0xFFFFFFFF = 4294967295 (all layers). Clear bit 3 -> 4294967287.
        // ---------------------------------------------------------------
        int excludePixel = ~pixelLayerMask;  // all bits except bit 3

        // Use serialized object to write the layer mask fields
        var so = new SerializedObject(renderer);

        var opaqueMask = so.FindProperty("m_OpaqueLayerMask");
        if (opaqueMask != null)
        {
            var bits = opaqueMask.FindPropertyRelative("m_Bits");
            if (bits != null)
            {
                uint current = (uint)bits.longValue;
                bits.longValue = (long)(current & (uint)excludePixel);
                Debug.Log("[PixelPassSetup] Opaque mask bits: " + bits.longValue);
            }
            else Debug.LogWarning("[PixelPassSetup] Could not find m_Bits on m_OpaqueLayerMask");
        }
        else Debug.LogWarning("[PixelPassSetup] Could not find m_OpaqueLayerMask");

        var transparentMask = so.FindProperty("m_TransparentLayerMask");
        if (transparentMask != null)
        {
            var bits = transparentMask.FindPropertyRelative("m_Bits");
            if (bits != null)
            {
                uint current = (uint)bits.longValue;
                bits.longValue = (long)(current & (uint)excludePixel);
                Debug.Log("[PixelPassSetup] Transparent mask bits: " + bits.longValue);
            }
            else Debug.LogWarning("[PixelPassSetup] Could not find m_Bits on m_TransparentLayerMask");
        }
        else Debug.LogWarning("[PixelPassSetup] Could not find m_TransparentLayerMask");

        so.ApplyModifiedPropertiesWithoutUndo();

        // ---------------------------------------------------------------
        // Step D: Mark dirty and save
        // ---------------------------------------------------------------
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(blitMat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[PixelPassSetup] Done! BasicFeature added to URP-Balanced-Renderer. " +
                  "Layer 'Pixel' (3) excluded from opaque+transparent masks. " +
                  "PixelDensity set to 10.");
    }
}
