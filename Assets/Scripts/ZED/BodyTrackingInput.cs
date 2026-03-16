using UnityEngine;
using UnityEngine.UI;
using sl;

/// <summary>
/// Per-player input adapter that translates ZED body tracking data into
/// PlayerController movement. Attached at runtime by GameScreenManager.
/// </summary>
public class BodyTrackingInput : MonoBehaviour
{
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

    [Header("Jump")]
    [Tooltip("Cooldown between jumps in seconds")]
    public float jumpCooldown = 0.5f;

    [Header("Tracking Loss")]
    [Tooltip("Grace period before showing lost indicator")]
    public float trackingLossGracePeriod = 1.0f;
    [Tooltip("Time after grace period before removing player")]
    public float trackingLossTimeout = 10f;

    [Header("Tracking Loss UI (auto-created if null)")]
    public GameObject trackingLostIndicator;
    public Text trackingLostTimerText;

    enum TrackingState { Tracking, Searching, Lost }
    TrackingState trackingState = TrackingState.Tracking;

    PlayerController playerController;
    ZEDTrackingProvider trackingProvider;

    // Jump state
    bool wasGestureActive;
    float jumpCooldownTimer;

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

        trackingState = TrackingState.Tracking;
        searchingTimer = 0f;
        lostTimer = 0f;
        wasGestureActive = false;
        jumpCooldownTimer = 0f;

        if (trackingLostIndicator != null)
            trackingLostIndicator.SetActive(false);
    }

    void OnDisable()
    {
        if (trackingProvider != null)
        {
            trackingProvider.OnPlayerBodyUpdated -= HandleBodyUpdated;
            trackingProvider.OnPlayerBodyLost -= HandleBodyLost;
        }
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
        if (playerController.IsFrozen) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        // Cooldown tick
        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        // Handle tracking loss states
        switch (trackingState)
        {
            case TrackingState.Searching:
                searchingTimer += Time.deltaTime;
                if (searchingTimer >= trackingLossGracePeriod)
                {
                    trackingState = TrackingState.Lost;
                    lostTimer = 0f;
                    if (trackingLostIndicator != null)
                        trackingLostIndicator.SetActive(true);
                }
                return; // Don't process input while searching

            case TrackingState.Lost:
                lostTimer += Time.deltaTime;
                if (trackingLostTimerText != null)
                    trackingLostTimerText.text = $"{Mathf.CeilToInt(trackingLossTimeout - lostTimer)}s";

                if (lostTimer >= trackingLossTimeout)
                {
                    // Timeout — remove player
                    GameScreenManager gsm = FindObjectOfType<GameScreenManager>();
                    if (gsm != null) gsm.RemovePlayer(playerNumber);
                }
                return; // Don't process input while lost
        }

        // --- Active tracking input ---
        if (trackingProvider == null) return;

        DetectedBody body = trackingProvider.GetBodyForPlayer(playerNumber);
        if (body == null) return;

        // X Movement — pelvis-based
        Vector3 pelvisWorld = trackingProvider.GetKeypointWorld(body, (int)BODY_38_PARTS.PELVIS);
        if (float.IsFinite(pelvisWorld.x))
        {
            float normalizedX = Mathf.InverseLerp(physicalXMin, physicalXMax, pelvisWorld.x);
            float targetX = Mathf.Lerp(gameXMin, gameXMax, normalizedX);

            float currentX = transform.position.x;
            float velocityX = (targetX - currentX) * xTrackingSpeed;
            playerController.rb.velocity = new Vector3(velocityX, playerController.rb.velocity.y, 0f);
        }

        // Jump — rising edge of field goal gesture
        Vector3[] keypoints = GetWorldKeypoints(body);
        float[] confidences = body.rawBodyData.keypointConfidence;
        bool gestureActive = GestureDetector.IsFieldGoalGesture(keypoints, confidences);

        if (gestureActive && !wasGestureActive && playerController.IsGrounded && jumpCooldownTimer <= 0f)
        {
            playerController.rb.AddForce(Vector3.up * playerController.jumpForce, ForceMode.Impulse);
            jumpCooldownTimer = jumpCooldown;
        }

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
