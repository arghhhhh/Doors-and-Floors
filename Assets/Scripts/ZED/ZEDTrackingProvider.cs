using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using sl;

/// <summary>
/// Persistent singleton (DontDestroyOnLoad) that manages ZED body tracking data
/// and player-to-body assignments across scene transitions.
/// Lives on same root as ZED_Rig_Mono.
/// </summary>
public class ZEDTrackingProvider : MonoBehaviour
{
    public static ZEDTrackingProvider Instance { get; private set; }

    [Header("ZED Reference")]
    public ZEDManager zedManager;

    // Player number → body ID
    Dictionary<int, int> playerBodyAssignments = new Dictionary<int, int>();

    // Body ID → DetectedBody (updated each frame)
    Dictionary<int, DetectedBody> currentBodies = new Dictionary<int, DetectedBody>();

    // Last known world X per player (for re-identification)
    Dictionary<int, float> lastKnownPlayerX = new Dictionary<int, float>();

    [Header("Re-identification")]
    [Tooltip("Max distance in meters to auto-reassign a lost body")]
    public float reidentificationRadius = 1.0f;

    // Events
    public event Action<int, DetectedBody> OnPlayerBodyUpdated;  // (playerNumber, body)
    public event Action<int> OnPlayerBodyLost;                    // (playerNumber)

    [HideInInspector] public bool IsZEDReady;

    void Awake()
    {
        IsZEDReady = false;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (zedManager == null)
            zedManager = GetComponent<ZEDManager>();
        if (zedManager == null)
            zedManager = FindObjectOfType<ZEDManager>();
    }

    void OnEnable()
    {
        if (zedManager != null)
            zedManager.OnBodyTracking += OnBodyTrackingFrame;
    }

    void OnDisable()
    {
        if (zedManager != null)
            zedManager.OnBodyTracking -= OnBodyTrackingFrame;
    }

    void Start()
    {
        StartCoroutine(WaitForZEDReady());
    }

    IEnumerator WaitForZEDReady()
    {
        Debug.LogWarning("[ZEDTrackingProvider] WaitForZEDReady() started.");

        // Poll until ANY ZEDManager in the scene becomes ready.
        // Re-acquire reference each iteration to handle DontDestroyOnLoad duplication
        // where our initial reference may point to a stale/destroyed instance.
        int attempts = 0;
        while (true)
        {
            // Re-find the ZEDManager each poll — handles instance changes from DontDestroyOnLoad
            ZEDManager candidate = GetComponent<ZEDManager>();
            if (candidate == null)
                candidate = FindObjectOfType<ZEDManager>();

            // Also scan all instances in case FindObjectOfType returns the wrong one
            if (candidate != null && !candidate.IsZEDReady)
            {
                foreach (var mgr in FindObjectsOfType<ZEDManager>())
                {
                    if (mgr.IsZEDReady)
                    {
                        candidate = mgr;
                        break;
                    }
                }
            }

            if (candidate != null && candidate.IsZEDReady)
            {
                zedManager = candidate;
                Debug.LogWarning($"[ZEDTrackingProvider] ZEDManager ready (instanceId={zedManager.GetInstanceID()}) after {attempts} polls.");
                break;
            }

            attempts++;
            if (attempts % 50 == 0)
                Debug.LogWarning($"[ZEDTrackingProvider] Still waiting (attempt {attempts})...");

            yield return new WaitForSecondsRealtime(0.1f);
        }

        zedManager.OnBodyTracking -= OnBodyTrackingFrame;
        zedManager.OnBodyTracking += OnBodyTrackingFrame;

        OnZEDReady();
    }

    void Update()
    {
        // Lightweight fallback: if the coroutine found a zedManager but died before OnZEDReady
        if (!IsZEDReady && zedManager != null && zedManager.IsZEDReady)
        {
            Debug.LogWarning("[ZEDTrackingProvider] Update() fallback: calling OnZEDReady().");
            OnZEDReady();
        }
    }

    void OnZEDReady()
    {
        if (IsZEDReady) return; // Prevent double-init
        IsZEDReady = true;

        // Re-subscribe to body tracking (in case OnDisable/OnEnable cycled while we were waiting)
        zedManager.OnBodyTracking -= OnBodyTrackingFrame;
        zedManager.OnBodyTracking += OnBodyTrackingFrame;

        if (!zedManager.IsBodyTrackingRunning)
            zedManager.StartBodyTracking();

        Debug.LogWarning("[ZEDTrackingProvider] ZED ready — StartBodyTracking() called.");
    }

    void OnBodyTrackingFrame(BodyTrackingFrame bodyFrame)
    {
        // Update current bodies dictionary
        currentBodies.Clear();

        var validBodies = bodyFrame.GetFilteredObjectList(
            tracking_ok: true,
            tracking_searching: true,
            tracking_off: false,
            confidencemin: 0);

        foreach (var body in validBodies)
        {
            currentBodies[body.id] = body;
        }

        // Check each assigned player
        var assignedPlayers = new List<int>(playerBodyAssignments.Keys);
        foreach (int playerNumber in assignedPlayers)
        {
            int assignedBodyId = playerBodyAssignments[playerNumber];

            if (currentBodies.TryGetValue(assignedBodyId, out DetectedBody body))
            {
                // Body still tracked — update last known position and fire event
                Vector3 pelvisWorld = GetKeypointWorld(body, (int)BODY_38_PARTS.PELVIS);
                if (float.IsFinite(pelvisWorld.x))
                    lastKnownPlayerX[playerNumber] = pelvisWorld.x;

                OnPlayerBodyUpdated?.Invoke(playerNumber, body);
            }
            else
            {
                // Body lost — attempt re-identification
                bool reidentified = TryReidentify(playerNumber);
                if (!reidentified)
                {
                    OnPlayerBodyLost?.Invoke(playerNumber);
                }
            }
        }
    }

    bool TryReidentify(int playerNumber)
    {
        if (!lastKnownPlayerX.ContainsKey(playerNumber)) return false;

        float lastX = lastKnownPlayerX[playerNumber];
        var assignedBodyIds = new HashSet<int>(playerBodyAssignments.Values);

        DetectedBody bestMatch = null;
        float bestDist = reidentificationRadius;

        foreach (var kvp in currentBodies)
        {
            if (assignedBodyIds.Contains(kvp.Key)) continue; // already assigned to another player

            Vector3 pelvis = GetKeypointWorld(kvp.Value, (int)BODY_38_PARTS.PELVIS);
            if (!float.IsFinite(pelvis.x)) continue;

            float dist = Mathf.Abs(pelvis.x - lastX);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestMatch = kvp.Value;
            }
        }

        if (bestMatch != null)
        {
            playerBodyAssignments[playerNumber] = bestMatch.id;
            Debug.Log($"[ZEDTrackingProvider] Re-identified P{playerNumber} as body {bestMatch.id}");
            OnPlayerBodyUpdated?.Invoke(playerNumber, bestMatch);
            return true;
        }

        return false;
    }

    // --- Public API ---

    public void AssignBodyToPlayer(int playerNumber, int bodyId)
    {
        playerBodyAssignments[playerNumber] = bodyId;
        Debug.Log($"[ZEDTrackingProvider] Assigned body {bodyId} to P{playerNumber}");
    }

    public void UnassignPlayer(int playerNumber)
    {
        playerBodyAssignments.Remove(playerNumber);
        lastKnownPlayerX.Remove(playerNumber);
        Debug.Log($"[ZEDTrackingProvider] Unassigned P{playerNumber}");
    }

    public void ClearAllAssignments()
    {
        playerBodyAssignments.Clear();
        lastKnownPlayerX.Clear();
    }

    public DetectedBody GetBodyForPlayer(int playerNumber)
    {
        if (playerBodyAssignments.TryGetValue(playerNumber, out int bodyId))
        {
            if (currentBodies.TryGetValue(bodyId, out DetectedBody body))
                return body;
        }
        return null;
    }

    public bool IsPlayerAssigned(int playerNumber)
    {
        return playerBodyAssignments.ContainsKey(playerNumber);
    }

    public int GetAssignedPlayerCount()
    {
        return playerBodyAssignments.Count;
    }

    public List<DetectedBody> GetUnassignedBodies()
    {
        var assignedIds = new HashSet<int>(playerBodyAssignments.Values);
        return currentBodies.Values
            .Where(b => !assignedIds.Contains(b.id))
            .ToList();
    }

    public Dictionary<int, DetectedBody> GetAllCurrentBodies()
    {
        return currentBodies;
    }

    /// <summary>
    /// Gets a keypoint's world position from a DetectedBody.
    /// Transforms from camera space to world space.
    /// </summary>
    public Vector3 GetKeypointWorld(DetectedBody body, int keypointIndex)
    {
        if (body == null || body.rawBodyData.keypoint == null)
            return Vector3.negativeInfinity;

        if (keypointIndex < 0 || keypointIndex >= body.rawBodyData.keypoint.Length)
            return Vector3.negativeInfinity;

        Vector3 camSpacePos = body.rawBodyData.keypoint[keypointIndex];

        if (!float.IsFinite(camSpacePos.x))
            return Vector3.negativeInfinity;

        if (zedManager != null)
            return zedManager.GetZedRootTransform().TransformPoint(camSpacePos);

        return camSpacePos;
    }

    public float GetKeypointConfidence(DetectedBody body, int keypointIndex)
    {
        if (body == null || body.rawBodyData.keypointConfidence == null)
            return 0f;

        if (keypointIndex < 0 || keypointIndex >= body.rawBodyData.keypointConfidence.Length)
            return 0f;

        return body.rawBodyData.keypointConfidence[keypointIndex];
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
 
 
 
 
 
 
 
