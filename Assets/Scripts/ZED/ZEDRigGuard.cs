using UnityEngine;

/// <summary>
/// Keeps a single ZED rig alive across scene loads.
/// ZEDManager has no duplicate check: when StartScreen is reloaded, the scene's own
/// ZED_Rig_Mono would run ZEDManager.Awake (re-creating camera 0 and overwriting the
/// static instance slot), then its OnDestroy would close the camera the persistent rig
/// is still using. This runs before any other script and deactivates the duplicate so
/// none of its components ever Awake.
/// Add this component to the ZED_Rig_Mono root.
/// </summary>
[DefaultExecutionOrder(-32000)]
public class ZEDRigGuard : MonoBehaviour
{
    static ZEDRigGuard instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            Debug.Log("[ZEDRigGuard] Persistent ZED rig already exists — discarded scene copy.");
            return;
        }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
