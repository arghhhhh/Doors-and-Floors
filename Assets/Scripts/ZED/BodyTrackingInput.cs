using UnityEngine;
using UnityEngine.UI;
using sl;

/// <summary>
/// Per-player input adapter that translates ZED body tracking data into
/// PlayerController movement. Attached at runtime by GameScreenManager.
/// </summary>
public class BodyTrackingInput : MonoBehaviour
{
    public enum JumpMethod { PhysicalJump, FieldGoalGesture }

    [Header("Player")]
    public int playerNumber = 1;

    [Header("X Mapping")]
    [Tooltip("Physical space left bound in meters")]
    public float physicalXMin = -1.5f;
    [Tooltip("Physical space right bound in meters")]
    public float physicalXMax = 1.5f;
    [Tooltip("Game space left bound")]
    public float gameXMin = -6.5f;
    [Tooltip("Game space right bound")]
    public float gameXMax = 6.5f;
    [Tooltip("Speed multiplier for tracking-based movement")]
    public float xTrackingSpeed = 10f;
    [Tooltip("Mirror the X axis so left/right matches the player's perspective")]
    public bool mirrorX = true;

    [Header("Jump")]
    [Tooltip("How to detect a jump")]
    public JumpMethod jumpMethod = JumpMethod.PhysicalJump;
    [Tooltip("Cooldown between jumps in seconds")]
    public float jumpCooldown = 0.5f;
    [Tooltip("Height (meters) pelvis must rise above baseline to trigger a physical jump")]
    public float physicalJumpThreshold = 0.12f;

    [Header("Tracking Loss")]
    [Tooltip("Grace period before showing lost indicator")]
    public float trackingLossGracePeriod = 1.0f;
    [Tooltip("Time after grace period before removing player")]
    public float trackingLossTimeout = 10f;

    [Header("Tracking Loss UI (auto-created if null)")]
    public GameObject trackingLostIndicator;
    public Text trackingLostTimerText;

    [Header("Tracking Loss Overlay")]
    public TrackingLostOverlay trackingLostOverlay;

    enum TrackingState { Tracking, Searching, Lost }
    TrackingState trackingState = TrackingState.Tracking;

    PlayerController playerController;
    ZEDTrackingProvider trackingProvider;

    // Jump state
    bool wasGestureActive;
    float jumpCooldownTimer;

    // Physical jump detection state
    float baselinePelvisY = float.NegativeInfinity;
    bool wasPhysicalJumpDetected;
    const float baselineRiseSpeed = 0.3f; // m/s — how fast baseline follows pelvis upward

    // Tracking loss state
    float searchingTimer;
    float lostTimer;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    void OnEnable()
    {
        trackingProvider = ZEDTrackingProvider.Instance;
        if (trackingProvider != null)
        {
            trackingProvider.OnPlayerBodyUpdated += HandleBodyUpdated;
            trackingProvider.OnPlayerBodyLost += HandleBodyLost;
        }
        if (playerController != null)
            playerController.OnReset += ResetInputState;

        ResetInputState();
    }

    void OnDisable()
    {
        if (trackingProvider != null)
        {
            trackingProvider.OnPlayerBodyUpdated -= HandleBodyUpdated;
            trackingProvider.OnPlayerBodyLost -= HandleBodyLost;
        }
        if (playerController != null)
            playerController.OnReset -= ResetInputState;
    }

    /// <summary>
    /// Clears tracking-loss and jump state. Runs on enable and whenever the player is reset
    /// (restart, tutorial end). If the body is still missing, HandleBodyLost re-enters
    /// Searching on the next tracking frame.
    /// </summary>
    void ResetInputState()
    {
        trackingState = TrackingState.Tracking;
        searchingTimer = 0f;
        lostTimer = 0f;
        wasGestureActive = false;
        wasPhysicalJumpDetected = false;
        baselinePelvisY = float.NegativeInfinity;
        jumpCooldownTimer = 0f;

        if (trackingLostIndicator != null)
            trackingLostIndicator.SetActive(false);
        if (trackingLostOverlay != null)
            trackingLostOverlay.Hide();
    }

    void HandleBodyUpdated(int pNum, DetectedBody body)
    {
        if (pNum != playerNumber) return;

        // Recovered from searching/lost
        if (trackingState != TrackingState.Tracking)
        {
            trackingState = TrackingState.Tracking;
            searchingTimer = 0f;
            lostTimer = 0f;
            if (trackingLostIndicator != null)
                trackingLostIndicator.SetActive(false);
            if (trackingLostOverlay != null)
                trackingLostOverlay.Hide();
            if (playerController != null)
                playerController.UnfreezeFromTrackingLoss();
        }
    }

    void HandleBodyLost(int pNum)
    {
        if (pNum != playerNumber) return;

        if (trackingState == TrackingState.Tracking)
        {
            trackingState = TrackingState.Searching;
            searchingTimer = 0f;
        }
    }

    void Update()
    {
        if (playerController == null) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        // Handle tracking loss states (must run even when frozen, since we freeze the player on loss)
        switch (trackingState)
        {
            case TrackingState.Searching:
                searchingTimer += Time.deltaTime;
                if (searchingTimer >= trackingLossGracePeriod)
                {
                    trackingState = TrackingState.Lost;
                    lostTimer = 0f;
                    SFXManager.Instance?.Play(SFXManager.Instance?.voiceTrackingLost);
                    if (playerController != null)
                        playerController.FreezeForTrackingLoss();
                    if (trackingLostOverlay != null)
                        trackingLostOverlay.Show();
                    if (trackingLostIndicator != null)
                        trackingLostIndicator.SetActive(true);
                }
                return;

            case TrackingState.Lost:
                lostTimer += Time.deltaTime;
                if (trackingLostTimerText != null)
                    trackingLostTimerText.text = $"{Mathf.CeilToInt(trackingLossTimeout - lostTimer)}s";

                if (lostTimer >= trackingLossTimeout)
                {
                    GameScreenManager gsm = FindObjectOfType<GameScreenManager>();
                    if (gsm != null) gsm.RemovePlayer(playerNumber);
                }
                return;
        }

        // Don't process input while frozen for other reasons (portal, win)
        if (playerController.IsFrozen) return;

        // Cooldown tick
        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        // --- Active tracking input ---
        if (trackingProvider == null) return;

        DetectedBody body = trackingProvider.GetBodyForPlayer(playerNumber);
        if (body == null) return;

        // X Movement — pelvis-based, mirrored so player's left/right matches in-game
        Vector3 pelvisWorld = trackingProvider.GetKeypointWorld(body, (int)BODY_38_PARTS.PELVIS);
        if (float.IsFinite(pelvisWorld.x))
        {
            float normalizedX = Mathf.InverseLerp(physicalXMin, physicalXMax, pelvisWorld.x);
            if (mirrorX)
                normalizedX = 1f - normalizedX;

            // After teleportation, recalculate offset from actual tracked position
            // so the player stays exactly at the destination. This avoids
            // accumulation errors from frame-rate-dependent trigger timing.
            if (playerController.needsOffsetRecalculation)
            {
                float mappedX = Mathf.Lerp(gameXMin, gameXMax, normalizedX);
                playerController.trackingXOffset = transform.position.x - mappedX;
                playerController.needsOffsetRecalculation = false;
            }

            float targetX = Mathf.Lerp(gameXMin, gameXMax, normalizedX) + playerController.trackingXOffset;

            float currentX = transform.position.x;
            float velocityX = (targetX - currentX) * xTrackingSpeed;
            playerController.SetHorizontalVelocity(velocityX);
        }

        // Jump
        if (jumpMethod == JumpMethod.PhysicalJump)
        {
            ProcessPhysicalJump(pelvisWorld);
        }
        else
        {
            ProcessFieldGoalJump(body);
        }
    }

    void ProcessPhysicalJump(Vector3 pelvisWorld)
    {
        if (!float.IsFinite(pelvisWorld.y)) return;

        float pelvisY = pelvisWorld.y;

        // Initialize baseline on first valid reading
        if (!float.IsFinite(baselinePelvisY))
            baselinePelvisY = pelvisY;

        // Baseline follows pelvis downward immediately, upward slowly
        if (pelvisY < baselinePelvisY)
            baselinePelvisY = pelvisY;
        else
            baselinePelvisY = Mathf.MoveTowards(baselinePelvisY, pelvisY, baselineRiseSpeed * Time.deltaTime);

        float heightAboveBaseline = pelvisY - baselinePelvisY;
        bool isJumping = heightAboveBaseline > physicalJumpThreshold;

        // Rising edge — trigger jump
        if (isJumping && !wasPhysicalJumpDetected && jumpCooldownTimer <= 0f && playerController.TryJump())
            jumpCooldownTimer = jumpCooldown;

        wasPhysicalJumpDetected = isJumping;
    }

    void ProcessFieldGoalJump(DetectedBody body)
    {
        Vector3[] keypoints = GetWorldKeypoints(body);
        float[] confidences = body.rawBodyData.keypointConfidence;
        bool gestureActive = GestureDetector.IsFieldGoalGesture(keypoints, confidences);

        if (gestureActive && !wasGestureActive && jumpCooldownTimer <= 0f && playerController.TryJump())
            jumpCooldownTimer = jumpCooldown;

        wasGestureActive = gestureActive;
    }

    Vector3[] GetWorldKeypoints(DetectedBody body)
    {
        int count = body.rawBodyData.keypoint.Length;
        Vector3[] worldKeypoints = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            worldKeypoints[i] = trackingProvider.GetKeypointWorld(body, i);
        }
        return worldKeypoints;
    }
}
