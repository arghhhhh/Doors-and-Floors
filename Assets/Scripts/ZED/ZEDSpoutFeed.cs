using Klak.Spout;
using sl;
using UnityEngine;

/// <summary>
/// Shares the ZED left-eye camera image over Spout so OBS (Spout2 plugin) can record it
/// while Unity owns the camera. The raw ZED texture is BGRA and upside down, so it is
/// converted into a RenderTexture each frame and that is what gets sent.
/// Add this component to the ZED_Rig_Mono GameObject (it persists into GameScreen).
/// </summary>
[RequireComponent(typeof(ZEDManager))]
[RequireComponent(typeof(SpoutSender))]
public class ZEDSpoutFeed : MonoBehaviour
{
    [Tooltip("Name shown in OBS's Spout2 Capture source.")]
    public string senderName = "ZED Camera";
    [Tooltip("Flip the image vertically. Turn off if the feed shows upside down in OBS.")]
    public bool flipVertical = true;
    [Tooltip("Swap red and blue. Turn off if colors look wrong in OBS.")]
    public bool swapRedBlue = true;
    [Tooltip("Hidden/ZEDSpoutBlit. Assigned here so the shader is included in builds.")]
    public Shader blitShader;

    ZEDManager zedManager;
    SpoutSender sender;
    Texture2D source;
    RenderTexture output;
    Material material;

    void Awake()
    {
        zedManager = GetComponent<ZEDManager>();
        sender = GetComponent<SpoutSender>();
        sender.spoutName = senderName;
        sender.captureMethod = CaptureMethod.Texture;
    }

    void OnEnable()
    {
        zedManager.OnZEDReady += GrabTexture;
        if (zedManager.IsZEDReady) GrabTexture();
    }

    void OnDisable()
    {
        zedManager.OnZEDReady -= GrabTexture;
    }

    void GrabTexture()
    {
        // Returns the texture the ZED plugin already registered for the left view.
        source = zedManager.zedCamera?.CreateTextureImageType(sl.VIEW.LEFT);
        if (source == null) return;

        if (output == null)
        {
            output = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            output.name = "ZEDSpoutFeed";
        }
        sender.sourceTexture = output;
        Debug.Log($"[ZEDSpoutFeed] Sending ZED feed over Spout as \"{senderName}\" ({source.width}x{source.height}).");
    }

    void LateUpdate()
    {
        if (source == null || output == null) return;
        if (material == null)
        {
            if (blitShader == null) blitShader = Shader.Find("Hidden/ZEDSpoutBlit");
            material = new Material(blitShader) { hideFlags = HideFlags.DontSave };
        }
        material.SetFloat("_Flip", flipVertical ? 1f : 0f);
        material.SetFloat("_Swap", swapRedBlue ? 1f : 0f);
        Graphics.Blit(source, output, material);
    }

    void OnDestroy()
    {
        if (output != null) { output.Release(); Destroy(output); }
        if (material != null) Destroy(material);
    }
}
