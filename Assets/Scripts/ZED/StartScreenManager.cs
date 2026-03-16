using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using sl;

/// <summary>
/// Lobby scene controller for player registration via ZED body tracking gestures.
/// State machine: WaitingForP1Detection → WaitingForP1Confirm → WaitingForP2 → WaitingForP2Confirm → Launching
/// </summary>
public class StartScreenManager : MonoBehaviour
{
    public enum LobbyState
    {
        WaitingForP1Detection,
        WaitingForP1Confirm,
        WaitingForP2,
        WaitingForP2Confirm,
        Launching
    }

    [Header("UI References")]
    public Text titleText;
    public Text p1StatusText;
    public Text p2StatusText;
    public Text countdownText;

    [Header("Timing")]
    [Tooltip("How long the field goal gesture must be held to confirm")]
    public float confirmHoldDuration = 1.0f;
    [Tooltip("Countdown duration for P2 after P1 confirms")]
    public float p2CountdownDuration = 10f;

    [Header("Camera Preview")]
    [Tooltip("Show ZED camera feed as a small overlay in the top-left corner")]
    public bool showCameraPreview = true;

    [Header("Game Scene")]
    public string gameSceneName = "GameScreen";

    public LobbyState CurrentState { get; private set; } = LobbyState.WaitingForP1Detection;

    ZEDTrackingProvider trackingProvider;
    Camera zedCamera;

    // P1 candidate tracking
    int p1CandidateBodyId = -1;
    float p1GestureHoldTime;

    // P2 tracking
    float p2Countdown;
    int p2CandidateBodyId = -1;
    float p2GestureHoldTime;
    int lastUnassignedBodyCount;

    void Start()
    {
        trackingProvider = ZEDTrackingProvider.Instance;
        if (trackingProvider != null)
            trackingProvider.ClearAllAssignments();

        // Auto-find UI references by name if not assigned in inspector
        if (titleText == null)
        {
            var go = GameObject.Find("TitleText");
            if (go != null) titleText = go.GetComponent<Text>();
        }
        if (p1StatusText == null)
        {
            var go = GameObject.Find("P1StatusText");
            if (go != null) p1StatusText = go.GetComponent<Text>();
        }
        if (p2StatusText == null)
        {
            var go = GameObject.Find("P2StatusText");
            if (go != null) p2StatusText = go.GetComponent<Text>();
        }
        if (countdownText == null)
        {
            var go = GameObject.Find("CountdownText");
            if (go != null) countdownText = go.GetComponent<Text>();
        }

        // Find and configure ZED camera preview
        if (trackingProvider != null && trackingProvider.zedManager != null)
        {
            Transform camLeft = trackingProvider.zedManager.transform.Find("Camera_Left");
            if (camLeft != null)
                zedCamera = camLeft.GetComponent<Camera>();
        }
        ApplyCameraPreview();

        UpdateUI();
    }

    void ApplyCameraPreview()
    {
        if (zedCamera == null) return;
        zedCamera.enabled = showCameraPreview;

        // Also toggle the Frame rendering plane
        Transform frame = zedCamera.transform.Find("Frame");
        if (frame != null)
            frame.gameObject.SetActive(showCameraPreview);
    }

    void Update()
    {
        // Keep camera preview in sync with toggle (supports runtime inspector changes)
        ApplyCameraPreview();

        if (trackingProvider == null)
        {
            trackingProvider = ZEDTrackingProvider.Instance;
            if (trackingProvider == null) return;
        }

        if (!trackingProvider.IsZEDReady) return;

        switch (CurrentState)
        {
            case LobbyState.WaitingForP1Detection:
                HandleWaitingForP1Detection();
                break;
            case LobbyState.WaitingForP1Confirm:
                HandleWaitingForP1Confirm();
                break;
            case LobbyState.WaitingForP2:
                HandleWaitingForP2();
                break;
            case LobbyState.WaitingForP2Confirm:
                HandleWaitingForP2Confirm();
                break;
            case LobbyState.Launching:
                break;
        }

        UpdateUI();
    }

    void HandleWaitingForP1Detection()
    {
        var bodies = trackingProvider.GetAllCurrentBodies();
        if (bodies.Count > 0)
        {
            // Take the first detected body as P1 candidate
            foreach (var kvp in bodies)
            {
                p1CandidateBodyId = kvp.Key;
                p1GestureHoldTime = 0f;
                CurrentState = LobbyState.WaitingForP1Confirm;
                break;
            }
        }
    }

    void HandleWaitingForP1Confirm()
    {
        var bodies = trackingProvider.GetAllCurrentBodies();

        // Check if candidate body still exists
        if (!bodies.ContainsKey(p1CandidateBodyId))
        {
            // Lost the candidate — go back to detection
            p1CandidateBodyId = -1;
            CurrentState = LobbyState.WaitingForP1Detection;
            return;
        }

        DetectedBody body = bodies[p1CandidateBodyId];
        Vector3[] keypoints = GetWorldKeypoints(body);
        float[] confidences = body.rawBodyData.keypointConfidence;

        if (GestureDetector.IsFieldGoalGesture(keypoints, confidences))
        {
            p1GestureHoldTime += Time.deltaTime;
            if (p1GestureHoldTime >= confirmHoldDuration)
            {
                // P1 confirmed
                trackingProvider.AssignBodyToPlayer(1, p1CandidateBodyId);
                p2Countdown = p2CountdownDuration;
                lastUnassignedBodyCount = 0;
                CurrentState = LobbyState.WaitingForP2;
            }
        }
        else
        {
            p1GestureHoldTime = 0f;
        }
    }

    void HandleWaitingForP2()
    {
        p2Countdown -= Time.deltaTime;

        var unassigned = trackingProvider.GetUnassignedBodies();
        int currentUnassigned = unassigned.Count;

        if (currentUnassigned > 0)
        {
            // New body detected — reset countdown and move to confirm
            if (currentUnassigned != lastUnassignedBodyCount && currentUnassigned > lastUnassignedBodyCount)
            {
                p2Countdown = p2CountdownDuration;
            }

            // Take first unassigned body as P2 candidate
            p2CandidateBodyId = unassigned[0].id;
            p2GestureHoldTime = 0f;
            CurrentState = LobbyState.WaitingForP2Confirm;
            lastUnassignedBodyCount = currentUnassigned;
            return;
        }

        lastUnassignedBodyCount = currentUnassigned;

        // Timeout — launch with P1 only
        if (p2Countdown <= 0f)
        {
            LaunchGame();
        }
    }

    void HandleWaitingForP2Confirm()
    {
        p2Countdown -= Time.deltaTime;

        var bodies = trackingProvider.GetAllCurrentBodies();

        // Check if P2 candidate still exists
        if (!bodies.ContainsKey(p2CandidateBodyId))
        {
            p2CandidateBodyId = -1;
            CurrentState = LobbyState.WaitingForP2;
            return;
        }

        DetectedBody body = bodies[p2CandidateBodyId];
        Vector3[] keypoints = GetWorldKeypoints(body);
        float[] confidences = body.rawBodyData.keypointConfidence;

        if (GestureDetector.IsFieldGoalGesture(keypoints, confidences))
        {
            p2GestureHoldTime += Time.deltaTime;
            if (p2GestureHoldTime >= confirmHoldDuration)
            {
                // P2 confirmed — assign and launch
                trackingProvider.AssignBodyToPlayer(2, p2CandidateBodyId);
                LaunchGame();
                return;
            }
        }
        else
        {
            p2GestureHoldTime = 0f;
        }

        // Timeout — launch with P1 only
        if (p2Countdown <= 0f)
        {
            LaunchGame();
        }
    }

    void LaunchGame()
    {
        CurrentState = LobbyState.Launching;
        Debug.Log($"[StartScreenManager] Launching with {trackingProvider.GetAssignedPlayerCount()} player(s)");
        SceneManager.LoadScene(gameSceneName);
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

    void UpdateUI()
    {
        if (titleText != null)
            titleText.text = "ZED GAMES";

        if (p1StatusText != null)
        {
            switch (CurrentState)
            {
                case LobbyState.WaitingForP1Detection:
                    p1StatusText.text = "P1: Step into view...";
                    break;
                case LobbyState.WaitingForP1Confirm:
                    float p1Progress = Mathf.Clamp01(p1GestureHoldTime / confirmHoldDuration);
                    p1StatusText.text = $"P1: Raise arms to confirm! [{p1Progress:P0}]";
                    break;
                default:
                    p1StatusText.text = "P1: Ready!";
                    break;
            }
        }

        if (p2StatusText != null)
        {
            switch (CurrentState)
            {
                case LobbyState.WaitingForP1Detection:
                case LobbyState.WaitingForP1Confirm:
                    p2StatusText.text = "";
                    break;
                case LobbyState.WaitingForP2:
                    p2StatusText.text = "P2: Step into view...";
                    break;
                case LobbyState.WaitingForP2Confirm:
                    float p2Progress = Mathf.Clamp01(p2GestureHoldTime / confirmHoldDuration);
                    p2StatusText.text = $"P2: Raise arms to confirm! [{p2Progress:P0}]";
                    break;
                default:
                    p2StatusText.text = trackingProvider != null && trackingProvider.IsPlayerAssigned(2)
                        ? "P2: Ready!"
                        : "P2: ---";
                    break;
            }
        }

        if (countdownText != null)
        {
            if (CurrentState == LobbyState.WaitingForP2 || CurrentState == LobbyState.WaitingForP2Confirm)
            {
                countdownText.text = $"Starting in {Mathf.CeilToInt(p2Countdown)}s";
                countdownText.gameObject.SetActive(true);
            }
            else
            {
                countdownText.gameObject.SetActive(false);
            }
        }
    }
}
