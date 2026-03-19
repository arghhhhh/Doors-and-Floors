using UnityEngine;

/// <summary>
/// Disables the ZED camera preview (Camera_Left + Frame) after the ZED initializes.
/// ZEDRenderingPlane re-enables these on OnZEDReady, so this script subscribes to
/// the same event and disables them again afterward.
/// Add this component to the ZED_Rig_Mono GameObject.
/// </summary>
[RequireComponent(typeof(ZEDManager))]
public class ZEDPreviewDisabler : MonoBehaviour
{
    ZEDManager zedManager;

    void Awake()
    {
        zedManager = GetComponent<ZEDManager>();
    }

    void OnEnable()
    {
        if (zedManager != null)
            zedManager.OnZEDReady += DisablePreview;
    }

    void OnDisable()
    {
        if (zedManager != null)
            zedManager.OnZEDReady -= DisablePreview;
    }

    void DisablePreview()
    {
        Transform camLeft = transform.Find("Camera_Left");
        if (camLeft == null) return;

        Camera cam = camLeft.GetComponent<Camera>();
        if (cam != null) cam.enabled = false;

        Transform frame = camLeft.Find("Frame");
        if (frame != null) frame.gameObject.SetActive(false);

        Debug.Log("[ZEDPreviewDisabler] Camera_Left and Frame disabled.");
    }
}
