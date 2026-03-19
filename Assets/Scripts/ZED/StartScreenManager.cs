using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Video;
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

    [Header("Video Quads")]
    [Tooltip("P1 video quad (left side)")]
    public GameObject p1VideoQuad;
    [Tooltip("P2 video quad (right side)")]
    public GameObject p2VideoQuad;
    [Tooltip("Static image for idle state (arms down)")]
    public Texture2D frameFirst;
    [Tooltip("Static image for confirmed state (arms up)")]
    public Texture2D frameLast;
    [Tooltip("Hue shift for P2 quad (0.4 = green-ish)")]
    public float p2HueShift = 0.4f;
    [Tooltip("Tint brightness when no player is detected (0=black, 1=full)")]
    [Range(0f, 1f)]
    public float idleBrightness = 0.7f;

    [Header("Debug")]
    [Tooltip("Shows face capture debug UI: preview, sliders, and C-key recapture")]
    public bool showCameraPreview = false;

    [Header("Game Scene")]
    public string gameSceneName = "GameScreen";

    public LobbyState CurrentState { get; private set; } = LobbyState.WaitingForP1Detection;

    ZEDTrackingProvider trackingProvider;

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

    // Debug tuning sliders for face crop (only used when showCameraPreview is true)
    float debugPadSides = -0.03f;
    float debugPadTop = 0.37f;
    float debugPadBottom = -0.24f;

    // Video player references (cached from quads)
    VideoPlayer p1Video;
    VideoPlayer p2Video;
    Renderer p1Renderer;
    Renderer p2Renderer;

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

        SetupVideoQuads();

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

        UpdateUI();
    }

    void Update()
    {
        if (trackingProvider == null)
        {
            trackingProvider = ZEDTrackingProvider.Instance;
            if (trackingProvider == null) return;
        }

        if (!trackingProvider.IsZEDReady) return;

        // Debug: press C to recapture face from any tracked body
        if (showCameraPreview && Input.GetKeyDown(KeyCode.C))
        {
            var bodies = trackingProvider.GetAllCurrentBodies();
            foreach (var kvp in bodies)
            {
                CapturePlayerFace(1, kvp.Value);
                break;
            }
        }

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

    void SetupVideoQuads()
    {
        if (p1VideoQuad != null)
        {
            p1Video = p1VideoQuad.GetComponent<VideoPlayer>();
            p1Renderer = p1VideoQuad.GetComponent<Renderer>();
        }
        if (p2VideoQuad != null)
        {
            p2Video = p2VideoQuad.GetComponent<VideoPlayer>();
            p2Renderer = p2VideoQuad.GetComponent<Renderer>();

            // Apply hue shift to P2's material instance
            if (p2HueShift > 0f)
            {
                Shader hueShader = Shader.Find("ZedGames/HueShiftLit");
                if (hueShader != null)
                {
                    Material[] mats = p2Renderer.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material clone = new Material(mats[i]);
                        clone.shader = hueShader;
                        clone.SetFloat("_HueShift", p2HueShift);
                        mats[i] = clone;
                    }
                    p2Renderer.materials = mats;
                }
            }
        }

        // Both start showing first-frame image, dimmed
        SetQuadIdle(p1Video, p1Renderer);
        SetQuadIdle(p2Video, p2Renderer);
    }

    void SetQuadIdle(VideoPlayer vp, Renderer rend)
    {
        if (rend == null) return;
        if (vp != null) { vp.Stop(); vp.enabled = false; }
        SetQuadTexture(rend, frameFirst);
        SetQuadTint(rend, idleBrightness);
    }

    void SetQuadPlaying(VideoPlayer vp, Renderer rend)
    {
        if (rend == null) return;
        if (vp != null)
        {
            vp.enabled = true;
            vp.isLooping = true;
            vp.Play();
        }
        SetQuadTint(rend, 1f);
    }

    void SetQuadConfirmed(VideoPlayer vp, Renderer rend)
    {
        if (rend == null) return;
        if (vp != null) { vp.Stop(); vp.enabled = false; }
        SetQuadTexture(rend, frameLast);
        SetQuadTint(rend, 1f);
    }

    void SetQuadTexture(Renderer rend, Texture2D tex)
    {
        if (rend == null || tex == null) return;
        rend.material.SetTexture("_BaseMap", tex);
    }

    void SetQuadTint(Renderer rend, float brightness)
    {
        if (rend == null) return;
        Color c = new Color(brightness, brightness, brightness, 1f);
        rend.material.SetColor("_BaseColor", c);
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
                SetQuadPlaying(p1Video, p1Renderer);
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
            SetQuadIdle(p1Video, p1Renderer);
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
                // P1 confirmed — capture face, freeze video, and assign
                CapturePlayerFace(1, body);
                SetQuadConfirmed(p1Video, p1Renderer);
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
            SetQuadPlaying(p2Video, p2Renderer);
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
            SetQuadIdle(p2Video, p2Renderer);
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
                // P2 confirmed — capture face, freeze video, assign, and launch
                CapturePlayerFace(2, body);
                SetQuadConfirmed(p2Video, p2Renderer);
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

        Texture2D face = FaceCaptureHelper.CaptureFace(trackingProvider.zedManager, body, 128,
            debugPadSides, debugPadTop, debugPadBottom);
        if (face != null)
        {
            trackingProvider.SetPlayerProfilePhoto(playerNumber, face);
            if (showCameraPreview)
            {
                if (playerNumber == 1) p1FacePreview = face;
                else if (playerNumber == 2) p2FacePreview = face;
            }
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
        if (!showCameraPreview) return;

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

        // Debug sliders for face crop tuning
        float sliderW = 200f;
        float sliderX = Screen.width - sliderW - padding;
        float sliderY = padding + previewSize + 40f;
        if (p2FacePreview != null) sliderY += previewSize + 40f;

        GUI.Box(new UnityEngine.Rect(sliderX - 4, sliderY, sliderW + 8, 110), "Face Crop [C=capture]");
        sliderY += 20;

        GUI.Label(new UnityEngine.Rect(sliderX, sliderY, sliderW, 20), $"Sides: {debugPadSides:F2}");
        debugPadSides = GUI.HorizontalSlider(new UnityEngine.Rect(sliderX, sliderY + 16, sliderW, 20), debugPadSides, -0.5f, 1f);
        sliderY += 30;

        GUI.Label(new UnityEngine.Rect(sliderX, sliderY, sliderW, 20), $"Top: {debugPadTop:F2}");
        debugPadTop = GUI.HorizontalSlider(new UnityEngine.Rect(sliderX, sliderY + 16, sliderW, 20), debugPadTop, -0.5f, 1.5f);
        sliderY += 30;

        GUI.Label(new UnityEngine.Rect(sliderX, sliderY, sliderW, 20), $"Bottom: {debugPadBottom:F2}");
        debugPadBottom = GUI.HorizontalSlider(new UnityEngine.Rect(sliderX, sliderY + 16, sliderW, 20), debugPadBottom, -0.5f, 1f);
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
