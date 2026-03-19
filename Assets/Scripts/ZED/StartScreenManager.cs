using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
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

    // UI Toolkit elements
    Heading titleEl;
    Unity.AppUI.UI.Text promptEl;
    Unity.AppUI.UI.Text countdownEl;
    Unity.AppUI.UI.Text p1StatusEl;
    Unity.AppUI.UI.Text p2StatusEl;

    // Blink state
    bool promptVisible = true;

    // Debug face preview textures
    Texture2D p1FacePreview;
    Texture2D p2FacePreview;

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

        // Query UI Toolkit elements from UIDocument
        var uiDoc = GetComponent<UIDocument>();
        if (uiDoc != null)
        {
            var root = uiDoc.rootVisualElement;
            titleEl = root.Q<Heading>("title-text");
            promptEl = root.Q<Unity.AppUI.UI.Text>("prompt-text");
            countdownEl = root.Q<Unity.AppUI.UI.Text>("countdown-text");
            p1StatusEl = root.Q<Unity.AppUI.UI.Text>("p1-status");
            p2StatusEl = root.Q<Unity.AppUI.UI.Text>("p2-status");

            // Blink the prompt text
            if (promptEl != null)
            {
                promptEl.schedule.Execute(() =>
                {
                    promptVisible = !promptVisible;
                    promptEl.style.opacity = promptVisible ? 1f : 0f;
                }).Every(500);
            }
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
                // P1 confirmed — capture face and assign
                CapturePlayerFace(1, body);
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
                // P2 confirmed — capture face, assign, and launch
                CapturePlayerFace(2, body);
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

    void CapturePlayerFace(int playerNumber, DetectedBody body)
    {
        if (trackingProvider == null || trackingProvider.zedManager == null) return;

        Texture2D face = FaceCaptureHelper.CaptureFace(trackingProvider.zedManager, body);
        if (face != null)
        {
            trackingProvider.SetPlayerProfilePhoto(playerNumber, face);
            if (playerNumber == 1) p1FacePreview = face;
            else if (playerNumber == 2) p2FacePreview = face;
            Debug.Log($"[StartScreenManager] Captured face for P{playerNumber}");
        }
        else
        {
            Debug.LogWarning($"[StartScreenManager] Failed to capture face for P{playerNumber}");
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

    void OnGUI()
    {
        // Debug preview: show captured face photos in the top-right corner
        float previewSize = 128f;
        float padding = 10f;
        float x = Screen.width - previewSize - padding;

        if (p1FacePreview != null)
        {
            float y = padding;
            GUI.Box(new UnityEngine.Rect(x - 4, y - 20, previewSize + 8, previewSize + 28), "P1 Face");
            GUI.DrawTexture(new UnityEngine.Rect(x, y, previewSize, previewSize), p1FacePreview);
        }

        if (p2FacePreview != null)
        {
            float y = padding + previewSize + 40f;
            GUI.Box(new UnityEngine.Rect(x - 4, y - 20, previewSize + 8, previewSize + 28), "P2 Face");
            GUI.DrawTexture(new UnityEngine.Rect(x, y, previewSize, previewSize), p2FacePreview);
        }
    }

    void UpdateUI()
    {
        if (titleEl != null)
            titleEl.text = "DOORS & FLOORS";

        if (p1StatusEl != null)
        {
            switch (CurrentState)
            {
                case LobbyState.WaitingForP1Detection:
                    p1StatusEl.text = "P1: Step into view...";
                    break;
                case LobbyState.WaitingForP1Confirm:
                    float p1Progress = Mathf.Clamp01(p1GestureHoldTime / confirmHoldDuration);
                    p1StatusEl.text = $"P1: Raise arms to confirm! [{p1Progress:P0}]";
                    break;
                default:
                    p1StatusEl.text = "P1: Ready!";
                    break;
            }
        }

        if (p2StatusEl != null)
        {
            switch (CurrentState)
            {
                case LobbyState.WaitingForP1Detection:
                case LobbyState.WaitingForP1Confirm:
                    p2StatusEl.text = "";
                    break;
                case LobbyState.WaitingForP2:
                    p2StatusEl.text = "P2: Step into view...";
                    break;
                case LobbyState.WaitingForP2Confirm:
                    float p2Progress = Mathf.Clamp01(p2GestureHoldTime / confirmHoldDuration);
                    p2StatusEl.text = $"P2: Raise arms to confirm! [{p2Progress:P0}]";
                    break;
                default:
                    p2StatusEl.text = trackingProvider != null && trackingProvider.IsPlayerAssigned(2)
                        ? "P2: Ready!"
                        : "P2: ---";
                    break;
            }
        }

        if (countdownEl != null)
        {
            if (CurrentState == LobbyState.WaitingForP2 || CurrentState == LobbyState.WaitingForP2Confirm)
            {
                countdownEl.text = $"Starting in {Mathf.CeilToInt(p2Countdown)}s";
                countdownEl.style.display = DisplayStyle.Flex;
            }
            else
            {
                countdownEl.style.display = DisplayStyle.None;
            }
        }
    }
}
