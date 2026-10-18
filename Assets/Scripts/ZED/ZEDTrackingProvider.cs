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

    // Last known world pelvis position per player (for re-identification)
    Dictionary<int, Vector3> lastKnownPlayerPos = new Dictionary<int, Vector3>();

    // Players whose body is currently missing (used for one-shot logging and gesture reclaim)
    HashSet<int> lostPlayers = new HashSet<int>();

    // Unassigned body ID → time it started holding the field goal gesture
    Dictionary<int, float> reclaimGestureStart = new Dictionary<int, float>();

    // Player number → captured face photo (persists across scenes via DontDestroyOnLoad)
    Dictionary<int, Texture2D> playerProfilePhotos = new Dictionary<int, Texture2D>();

    [Header("Re-identification")]
    [Tooltip("Max floor distance (X/Z, meters) to auto-reassign a lost player to a new body")]
    public float reidentificationRadius = 1.0f;
    [Tooltip("In GameScreen, a lost player can reclaim their slot by holding both arms up for this long (seconds)")]
    public float reclaimGestureHold = 0.5f;

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
                    lastKnownPlayerPos[playerNumber] = pelvisWorld;

                MarkFound(playerNumber);
                OnPlayerBodyUpdated?.Invoke(playerNumber, body);
            }
            else
            {
                // Body lost — attempt re-identification
                bool reidentified = TryReidentify(playerNumber);
                if (!reidentified)
                {
                    if (lostPlayers.Add(playerNumber))
                        LogLoss(playerNumber, assignedBodyId);
                    OnPlayerBodyLost?.Invoke(playerNumber);
                }
            }
        }

        TryGestureReclaim();
    }

    bool TryReidentify(int playerNumber)
    {
        if (!lastKnownPlayerPos.TryGetValue(playerNumber, out Vector3 lastPos)) return false;

        var assignedBodyIds = new HashSet<int>(playerBodyAssignments.Values);

        DetectedBody bestMatch = null;
        float bestDist = reidentificationRadius;

        foreach (var kvp in currentBodies)
        {
            if (assignedBodyIds.Contains(kvp.Key)) continue; // already assigned to another player

            Vector3 pelvis = GetKeypointWorld(kvp.Value, (int)BODY_38_PARTS.PELVIS);
            if (!float.IsFinite(pelvis.x)) continue;

            // Floor distance (X and depth), so a spectator standing behind the player doesn't match
            float dist = FloorDistance(pelvis, lastPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestMatch = kvp.Value;
            }
        }

        if (bestMatch != null)
        {
            playerBodyAssignments[playerNumber] = bestMatch.id;
            Debug.Log($"[ZEDTrackingProvider] Re-identified P{playerNumber} as body {bestMatch.id} ({bestDist:F2}m from last position)");
            MarkFound(playerNumber);
            OnPlayerBodyUpdated?.Invoke(playerNumber, bestMatch);
            return true;
        }

        return false;
    }

    /// <summary>
    /// GameScreen only: a lost player who walked too far for re-identification (or whose
    /// slot was taken by the wrong body) gets it back by holding the field goal gesture.
    /// Disabled in the lobby, where that same gesture registers new players.
    /// </summary>
    void TryGestureReclaim()
    {
        if (lostPlayers.Count == 0 || GameManager.Instance == null)
        {
            reclaimGestureStart.Clear();
            return;
        }

        var assignedBodyIds = new HashSet<int>(playerBodyAssignments.Values);
        var seen = new HashSet<int>();

        foreach (var kvp in currentBodies)
        {
            int bodyId = kvp.Key;
            if (assignedBodyIds.Contains(bodyId)) continue;

            DetectedBody body = kvp.Value;
            int count = body.rawBodyData.keypoint?.Length ?? 0;
            var keypoints = new Vector3[count];
            for (int i = 0; i < count; i++)
                keypoints[i] = GetKeypointWorld(body, i);

            if (!GestureDetector.IsFieldGoalGesture(keypoints, body.rawBodyData.keypointConfidence))
                continue;

            seen.Add(bodyId);
            if (!reclaimGestureStart.TryGetValue(bodyId, out float start))
            {
                reclaimGestureStart[bodyId] = Time.unscaledTime;
                continue;
            }
            if (Time.unscaledTime - start < reclaimGestureHold) continue;

            // Give the body to the lost player whose last position is closest
            Vector3 pelvis = GetKeypointWorld(body, (int)BODY_38_PARTS.PELVIS);
            int bestPlayer = -1;
            float bestDist = float.PositiveInfinity;
            foreach (int p in lostPlayers)
            {
                float d = lastKnownPlayerPos.TryGetValue(p, out Vector3 last) && float.IsFinite(pelvis.x)
                    ? FloorDistance(pelvis, last)
                    : float.MaxValue;
                if (bestPlayer < 0 || d < bestDist) { bestDist = d; bestPlayer = p; }
            }

            playerBodyAssignments[bestPlayer] = bodyId;
            if (float.IsFinite(pelvis.x))
                lastKnownPlayerPos[bestPlayer] = pelvis;
            Debug.Log($"[ZEDTrackingProvider] P{bestPlayer} reclaimed with gesture as body {bodyId}");
            reclaimGestureStart.Remove(bodyId);
            MarkFound(bestPlayer);
            OnPlayerBodyUpdated?.Invoke(bestPlayer, body);
            return; // one reclaim per frame; assignments changed
        }

        // Forget bodies that dropped the gesture or disappeared
        var stale = new List<int>();
        foreach (int id in reclaimGestureStart.Keys)
            if (!seen.Contains(id)) stale.Add(id);
        foreach (int id in stale)
            reclaimGestureStart.Remove(id);
    }

    void MarkFound(int playerNumber)
    {
        if (lostPlayers.Remove(playerNumber))
            Debug.Log($"[ZEDTrackingProvider] P{playerNumber} tracking recovered");
    }

    void LogLoss(int playerNumber, int lostBodyId)
    {
        string last = lastKnownPlayerPos.TryGetValue(playerNumber, out Vector3 p)
            ? $"({p.x:F2}, {p.z:F2})" : "unknown";
        var others = new System.Text.StringBuilder();
        foreach (var kvp in currentBodies)
        {
            Vector3 pelvis = GetKeypointWorld(kvp.Value, (int)BODY_38_PARTS.PELVIS);
            others.Append($" {kvp.Key}@({pelvis.x:F2}, {pelvis.z:F2})");
        }
        Debug.Log($"[ZEDTrackingProvider] P{playerNumber} lost body {lostBodyId}, last X/Z {last}; bodies in view:{(others.Length > 0 ? others.ToString() : " none")}");
    }

    static float FloorDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
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
        lastKnownPlayerPos.Remove(playerNumber);
        lostPlayers.Remove(playerNumber);
        Debug.Log($"[ZEDTrackingProvider] Unassigned P{playerNumber}");
    }

    public void ClearAllAssignments()
    {
        playerBodyAssignments.Clear();
        lastKnownPlayerPos.Clear();
        lostPlayers.Clear();
        reclaimGestureStart.Clear();
        foreach (var tex in playerProfilePhotos.Values)
        {
            if (tex != null) Destroy(tex);
        }
        playerProfilePhotos.Clear();
    }

    /// <summary>
    /// Stores a captured profile photo for a player. Persists across scene loads.
    /// </summary>
    public void SetPlayerProfilePhoto(int playerNumber, Texture2D photo)
    {
        if (playerProfilePhotos.TryGetValue(playerNumber, out Texture2D old) && old != null)
            Destroy(old);
        playerProfilePhotos[playerNumber] = photo;
        Debug.Log($"[ZEDTrackingProvider] Stored profile photo for P{playerNumber} ({photo.width}x{photo.height})");
    }

    /// <summary>
    /// Returns the captured profile photo for a player, or null if none captured.
    /// </summary>
    public Texture2D GetPlayerProfilePhoto(int playerNumber)
    {
        playerProfilePhotos.TryGetValue(playerNumber, out Texture2D photo);
        return photo;
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
 
 
 
 
 
 
 
