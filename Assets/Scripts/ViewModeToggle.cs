using UnityEngine;

[ExecuteInEditMode]
public class ViewModeToggle : MonoBehaviour
{
    public bool showOpenPose = false;

    private bool _cachedShowOpenPose;

    private const int OpenPoseLayer = 3;
    private const int OpenPoseMask = 1 << OpenPoseLayer;

    private void OnValidate()
    {
        ApplyCullingMasks();
    }

    private void Start()
    {
        _cachedShowOpenPose = showOpenPose;
        ApplyCullingMasks();
    }

    private void Update()
    {
        if (showOpenPose != _cachedShowOpenPose)
        {
            _cachedShowOpenPose = showOpenPose;
            ApplyCullingMasks();
        }
    }

    private void ApplyCullingMasks()
    {
        Camera[] cameras = FindObjectsOfType<Camera>();
        int mask = showOpenPose ? OpenPoseMask : ~OpenPoseMask;
        foreach (Camera cam in cameras)
        {
            cam.cullingMask = mask;
        }
    }
}
