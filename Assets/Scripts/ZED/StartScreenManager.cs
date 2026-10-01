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
/// If no P2 confirms before the countdown ends, P1 plays against the CPU (GameSession.VsCpu).
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
    public float idleBrightness = 0.4f;
    [Tooltip("HDR brightness multiplier for confirmed state (>1 = overbright)")]
    public float confirmedBrightness = 1.4f;
    [Tooltip("Scale punch amount added on confirm")]
    public float scalePulseAmount = 0.8f;
    [Tooltip("How fast the scale pulse eases back")]
    public float scalePulseSpeed = 4f;
    [Tooltip("How fast the sweep moves across the quad")]
    public float sweepSpeed = 3f;

    [Header("Detection")]
    [Tooltip("Use nose confidence for facing-camera check (requires good lighting). When off, uses shoulder depth instead.")]
    public bool useNoseFacing = false;
    [Tooltip("Minimum nose confidence when useNoseFacing is enabled")]
    public float noseConfMin = 40f;

    #if UNITY_EDITOR
    [Header("Debug (editor only)")]
    [Tooltip("Shows face crop preview and tuning sliders (C key to recapture)")]
    public bool debugFaceCrop = false;
    [Tooltip("Shows per-body gesture recognition, confidence values, and candidate scoring")]
    public bool debugGestureTracking = false;
    #endif

    [Header("Shaders")]
    [Tooltip("SweepUnlit shader — must be assigned so it's included in builds")]
    public Shader sweepShader;

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

    // Face crop padding (tunable via debug sliders in editor)
    float debugPadSides = -0.03f;
    float debugPadTop = 0.37f;
    float debugPadBottom = -0.24f;

    #if UNITY_EDITOR
    // Debug face preview textures (editor only)
    Texture2D p1FacePreview;
    Texture2D p2FacePreview;
    #endif

    // Video player references (cached from quads)
    VideoPlayer p1Video;
    VideoPlayer p2Video;
    Renderer p1Renderer;
    Renderer p2Renderer;

    // Face preview quads (shown above video quads after player confirmation)
    GameObject p1FaceQuad;
    GameObject p2FaceQuad;

    // Scale pulse state
    float p1ScalePulse;
    float p2ScalePulse;
    Vector3 p1BaseScale;
    Vector3 p2BaseScale;

    // Sweep animation state (-0.5 = off, animates to 1.5)
    float p1SweepProgress = -0.5f;
    float p2SweepProgress = -0.5f;
    bool p1Sweeping;
    bool p2Sweeping;

    static readonly int SweepProgressID = Shader.PropertyToID("_SweepProgress");

    // P1 candidate tracking
    int p1CandidateBodyId = -1;
    float p1GestureHoldTime;

    // P2 tracking
    float p2Countdown;
    int p2CandidateBodyId = -1;
    float p2GestureHoldTime;

    // Body evaluation — filter passersby, prefer facing + still + gesturing
    Dictionary<int, Vector3> prevPelvis = new Dictionary<int, Vector3>();
    Dictionary<int, float> stillTime = new Dictionary<int, float>();
    const float stillSpeedMax = 0.2f;       // m/s — below this is "standing still"
    const float shoulderZMaxDiff = 0.25f;   // max Z-depth difference between shoulders to count as "facing camera"
    const float shoulderConfMin = 30f;      // minimum shoulder confidence for facing check

    void Start()
    {
        GameSession.VsCpu = false;

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
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Debug: launch straight into keyboard mode without the ZED (K = 2 players, V = vs CPU)
        if (CurrentState != LobbyState.Launching)
        {
            if (Input.GetKeyDown(KeyCode.K)) { LaunchKeyboardDebug(false); return; }
            if (Input.GetKeyDown(KeyCode.V)) { LaunchKeyboardDebug(true); return; }
        }
        #endif

        if (trackingProvider == null)
        {
            trackingProvider = ZEDTrackingProvider.Instance;
            if (trackingProvider == null) return;
        }

        if (!trackingProvider.IsZEDReady) return;

        UpdateBodyStillness();

        #if UNITY_EDITOR
        // Debug: press C to recapture face from any tracked body
        if (debugFaceCrop && Input.GetKeyDown(KeyCode.C))
        {
            var bodies = trackingProvider.GetAllCurrentBodies();
            foreach (var kvp in bodies)
            {
                CapturePlayerFace(1, kvp.Value);
                break;
            }
        }
        #endif

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
                p2Countdown -= Time.deltaTime;
                if (p2Countdown <= 0f)
                    LaunchGame();
                break;
        }

        // Animate scale pulses
        AnimateScalePulse(p1VideoQuad, ref p1ScalePulse, p1BaseScale);
        AnimateScalePulse(p2VideoQuad, ref p2ScalePulse, p2BaseScale);

        // Animate sweeps
        AnimateSweep(p1Renderer, ref p1SweepProgress, ref p1Sweeping);
        AnimateSweep(p2Renderer, ref p2SweepProgress, ref p2Sweeping);

        UpdateUI();
    }

    void AnimateScalePulse(GameObject quad, ref float pulse, Vector3 baseScale)
    {
        if (quad == null || pulse <= 0f) return;
        pulse = Mathf.MoveTowards(pulse, 0f, scalePulseSpeed * Time.deltaTime);
        float s = 1f + pulse;
        quad.transform.localScale = baseScale * s;
    }

    void AnimateSweep(Renderer rend, ref float progress, ref bool sweeping)
    {
        if (!sweeping || rend == null) return;
        progress += sweepSpeed * Time.deltaTime;
        if (progress >= 1.5f)
        {
            progress = -0.5f;
            sweeping = false;
        }
        rend.material.SetFloat(SweepProgressID, progress);
    }

    void SetupVideoQuads()
    {
        if (p1VideoQuad != null)
        {
            p1Video = p1VideoQuad.GetComponent<VideoPlayer>();
            p1Renderer = p1VideoQuad.GetComponent<Renderer>();
            if (sweepShader != null)
                ApplySweepShader(p1Renderer, sweepShader, 0f);
        }
        if (p2VideoQuad != null)
        {
            p2Video = p2VideoQuad.GetComponent<VideoPlayer>();
            p2Renderer = p2VideoQuad.GetComponent<Renderer>();
            if (sweepShader != null)
                ApplySweepShader(p2Renderer, sweepShader, p2HueShift);
        }

        // Cache base scales
        if (p1VideoQuad != null) p1BaseScale = p1VideoQuad.transform.localScale;
        if (p2VideoQuad != null) p2BaseScale = p2VideoQuad.transform.localScale;

        // Create face preview quads above each video quad
        p1FaceQuad = CreateFacePreviewQuad(p1VideoQuad);
        p2FaceQuad = CreateFacePreviewQuad(p2VideoQuad);

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
            // vp.isLooping = true;
            vp.Play();
        }
        SetQuadTint(rend, 1f);
    }

    void SetQuadConfirmed(VideoPlayer vp, Renderer rend, int playerNumber)
    {
        if (rend == null) return;
        if (vp != null) { vp.Stop(); vp.enabled = false; }
        SetQuadTexture(rend, frameLast);
        SetQuadTint(rend, confirmedBrightness);

        // Kick off scale pulse + sweep
        if (playerNumber == 1)
        {
            p1ScalePulse = scalePulseAmount;
            p1SweepProgress = -0.5f;
            p1Sweeping = true;
        }
        else
        {
            p2ScalePulse = scalePulseAmount;
            p2SweepProgress = -0.5f;
            p2Sweeping = true;
        }
    }

    void ApplySweepShader(Renderer rend, Shader shader, float hueShift)
    {
        Material[] mats = rend.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            Material clone = new Material(mats[i]);
            clone.shader = shader;
            clone.SetFloat("_HueShift", hueShift);
            clone.SetFloat(SweepProgressID, -0.5f);
            mats[i] = clone;
        }
        rend.materials = mats;
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

    // ── Face preview quads ──────────────────────────────────────────

    GameObject CreateFacePreviewQuad(GameObject videoQuad)
    {
        if (videoQuad == null) return null;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "FacePreview";
        quad.transform.SetParent(videoQuad.transform, false);

        Destroy(quad.GetComponent<Collider>());

        // NoPixel layer so it renders with the correct camera
        quad.layer = LayerMask.NameToLayer("NoPixel");

        quad.transform.localPosition = new Vector3(0f, 0.4f, 0.2f);
        quad.transform.localScale = new Vector3(0.15f, 0.15f, 1f);
        quad.transform.localRotation = Quaternion.identity;

        // Border quad behind the face (slightly larger, gold)
        GameObject border = GameObject.CreatePrimitive(PrimitiveType.Quad);
        border.name = "FaceBorder";
        border.layer = LayerMask.NameToLayer("NoPixel");
        border.transform.SetParent(quad.transform, false);
        Destroy(border.GetComponent<Collider>());
        float borderScale = 1.12f;
        border.transform.localScale = new Vector3(borderScale, borderScale, 1f);
        border.transform.localPosition = new Vector3(0f, 0f, 0.01f);

        Renderer borderRend = border.GetComponent<Renderer>();
        if (sweepShader != null)
        {
            Material borderMat = new Material(sweepShader);
            borderMat.SetColor("_BaseColor", new Color(0.85f, 0.65f, 0f)); // gold
            borderMat.SetFloat("_HueShift", 0f);
            borderMat.SetFloat(SweepProgressID, -0.5f);
            borderRend.material = borderMat;
        }

        // Face material — use sweep shader with no sweep, white tint
        Renderer faceRend = quad.GetComponent<Renderer>();
        if (sweepShader != null)
        {
            Material faceMat = new Material(sweepShader);
            faceMat.SetColor("_BaseColor", Color.white);
            faceMat.SetFloat("_HueShift", 0f);
            faceMat.SetFloat(SweepProgressID, -0.5f);
            faceRend.material = faceMat;
        }

        quad.SetActive(false);
        return quad;
    }

    void ShowFacePreview(int playerNumber, Texture2D face)
    {
        GameObject faceQuad = playerNumber == 1 ? p1FaceQuad : p2FaceQuad;
        if (faceQuad == null || face == null) return;

        Renderer rend = faceQuad.GetComponent<Renderer>();
        if (rend != null)
            rend.material.SetTexture("_BaseMap", face);
        faceQuad.SetActive(true);
    }

    void HideFacePreview(int playerNumber)
    {
        GameObject faceQuad = playerNumber == 1 ? p1FaceQuad : p2FaceQuad;
        if (faceQuad != null)
            faceQuad.SetActive(false);
    }

    // ── Body evaluation helpers ──────────────────────────────────────

    void UpdateBodyStillness()
    {
        var bodies = trackingProvider.GetAllCurrentBodies();

        // Prune bodies that disappeared
        var stale = new List<int>(prevPelvis.Keys);
        foreach (int id in stale)
            if (!bodies.ContainsKey(id))
            {
                prevPelvis.Remove(id);
                stillTime.Remove(id);
            }

        foreach (var kvp in bodies)
        {
            Vector3 pelvis = trackingProvider.GetKeypointWorld(kvp.Value, (int)BODY_38_PARTS.PELVIS);
            if (!float.IsFinite(pelvis.x)) continue;

            if (prevPelvis.TryGetValue(kvp.Key, out Vector3 prev))
            {
                float speed = (pelvis - prev).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
                float current;
                stillTime.TryGetValue(kvp.Key, out current);
                stillTime[kvp.Key] = speed < stillSpeedMax ? current + Time.deltaTime : 0f;
            }
            else
            {
                stillTime[kvp.Key] = 0f;
            }

            prevPelvis[kvp.Key] = pelvis;
        }
    }

    bool IsFacingCamera(DetectedBody body)
    {
        if (useNoseFacing)
            return body.rawBodyData.keypointConfidence[(int)BODY_38_PARTS.NOSE] >= noseConfMin;

        float[] conf = body.rawBodyData.keypointConfidence;
        int lsIdx = (int)BODY_38_PARTS.LEFT_SHOULDER;
        int rsIdx = (int)BODY_38_PARTS.RIGHT_SHOULDER;

        // Need both shoulders with decent confidence
        if (conf[lsIdx] < shoulderConfMin || conf[rsIdx] < shoulderConfMin)
            return false;

        Vector3 ls = trackingProvider.GetKeypointWorld(body, lsIdx);
        Vector3 rs = trackingProvider.GetKeypointWorld(body, rsIdx);
        if (!float.IsFinite(ls.z) || !float.IsFinite(rs.z))
            return false;

        // Facing camera = both shoulders at similar depth
        // Walking past sideways = large Z gap between shoulders
        return Mathf.Abs(ls.z - rs.z) < shoulderZMaxDiff;
    }

    /// <summary>
    /// Scores a body for candidate selection. Returns -1 if disqualified.
    /// Gesture bodies score >= 1000, making them always win.
    /// </summary>
    float ScoreBody(DetectedBody body)
    {
        if (!IsFacingCamera(body)) return -1f;

        float score = 0f;

        // Gesture = overwhelming priority
        Vector3[] kp = GetWorldKeypoints(body);
        if (GestureDetector.IsFieldGoalGesture(kp, body.rawBodyData.keypointConfidence))
            score += 1000f;

        // Standing still (> 0.3 s)
        float st;
        stillTime.TryGetValue(body.id, out st);
        if (st > 0.3f)
            score += 10f + Mathf.Min(st, 3f);

        // Nose confidence as tiebreaker
        score += body.rawBodyData.keypointConfidence[(int)BODY_38_PARTS.NOSE] * 0.01f;

        return score;
    }

    /// <summary>
    /// Finds the best candidate body from a set. Returns bodyId or -1.
    /// </summary>
    int FindBestBody(IEnumerable<KeyValuePair<int, DetectedBody>> bodies, out float bestScore)
    {
        int bestId = -1;
        bestScore = -1f;
        foreach (var kvp in bodies)
        {
            float s = ScoreBody(kvp.Value);
            if (s > bestScore) { bestScore = s; bestId = kvp.Key; }
        }
        return bestId;
    }

    int FindBestUnassigned(out float bestScore)
    {
        var unassigned = trackingProvider.GetUnassignedBodies();
        int bestId = -1;
        bestScore = -1f;
        foreach (var body in unassigned)
        {
            float s = ScoreBody(body);
            if (s > bestScore) { bestScore = s; bestId = body.id; }
        }
        return bestId;
    }

    // ── State handlers ────────────────────────────────────────────────

    void HandleWaitingForP1Detection()
    {
        var bodies = trackingProvider.GetAllCurrentBodies();
        float bestScore;
        int bestId = FindBestBody(bodies, out bestScore);

        if (bestId >= 0)
        {
            p1CandidateBodyId = bestId;
            p1GestureHoldTime = 0f;
            SetQuadPlaying(p1Video, p1Renderer);
            SFXManager.Instance?.Play(SFXManager.Instance?.sfxPlayerTracked);
            CurrentState = LobbyState.WaitingForP1Confirm;
        }
    }

    void HandleWaitingForP1Confirm()
    {
        var bodies = trackingProvider.GetAllCurrentBodies();

        // Check if a better body appeared (e.g. someone gesturing)
        float bestScore;
        int bestId = FindBestBody(bodies, out bestScore);

        if (!bodies.ContainsKey(p1CandidateBodyId))
        {
            // Current candidate lost — switch to best alternative or go back
            if (bestId >= 0)
            {
                p1CandidateBodyId = bestId;
                p1GestureHoldTime = 0f;
            }
            else
            {
                p1CandidateBodyId = -1;
                SetQuadIdle(p1Video, p1Renderer);
                HideFacePreview(1);
                SFXManager.Instance?.Play(SFXManager.Instance?.sfxPlayerLost);
                CurrentState = LobbyState.WaitingForP1Detection;
                return;
            }
        }
        else if (bestId != p1CandidateBodyId && bestScore >= 1000f)
        {
            // A different body is gesturing — switch to them
            p1CandidateBodyId = bestId;
            p1GestureHoldTime = 0f;
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
                SetQuadConfirmed(p1Video, p1Renderer, 1);
                SFXManager.Instance?.Play(SFXManager.Instance?.voiceP1Activated);
                trackingProvider.AssignBodyToPlayer(1, p1CandidateBodyId);
                p2Countdown = p2CountdownDuration;
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

        float bestScore;
        int bestId = FindBestUnassigned(out bestScore);

        if (bestId >= 0)
        {
            if (bestId != p2CandidateBodyId)
                p2Countdown = p2CountdownDuration; // reset countdown for new candidate

            p2CandidateBodyId = bestId;
            p2GestureHoldTime = 0f;
            SetQuadPlaying(p2Video, p2Renderer);
            CurrentState = LobbyState.WaitingForP2Confirm;
            return;
        }

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

        // Check for better unassigned body (e.g. someone gesturing)
        float bestScore;
        int bestId = FindBestUnassigned(out bestScore);

        if (!bodies.ContainsKey(p2CandidateBodyId))
        {
            if (bestId >= 0)
            {
                p2CandidateBodyId = bestId;
                p2GestureHoldTime = 0f;
            }
            else
            {
                p2CandidateBodyId = -1;
                SetQuadIdle(p2Video, p2Renderer);
                HideFacePreview(2);
                SFXManager.Instance?.Play(SFXManager.Instance?.sfxPlayerLost);
                CurrentState = LobbyState.WaitingForP2;
                return;
            }
        }
        else if (bestId != p2CandidateBodyId && bestScore >= 1000f)
        {
            // A different body is gesturing — switch to them
            p2CandidateBodyId = bestId;
            p2GestureHoldTime = 0f;
        }

        DetectedBody candidate = bodies[p2CandidateBodyId];
        Vector3[] keypoints = GetWorldKeypoints(candidate);
        float[] confidences = candidate.rawBodyData.keypointConfidence;

        if (GestureDetector.IsFieldGoalGesture(keypoints, confidences))
        {
            p2GestureHoldTime += Time.deltaTime;
            if (p2GestureHoldTime >= confirmHoldDuration)
            {
                // P2 confirmed — capture face, freeze video, assign, and start countdown
                CapturePlayerFace(2, candidate);
                SetQuadConfirmed(p2Video, p2Renderer, 2);
                SFXManager.Instance?.Play(SFXManager.Instance?.voiceP2Activated);
                trackingProvider.AssignBodyToPlayer(2, p2CandidateBodyId);
                p2Countdown = 6f;
                CurrentState = LobbyState.Launching;
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
            ShowFacePreview(playerNumber, face);
            #if UNITY_EDITOR
            if (debugFaceCrop)
            {
                if (playerNumber == 1) p1FacePreview = face;
                else if (playerNumber == 2) p2FacePreview = face;
            }
            #endif
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
        GameSession.VsCpu = trackingProvider != null
            && trackingProvider.IsPlayerAssigned(1)
            && !trackingProvider.IsPlayerAssigned(2);
        SFXManager.Instance?.Play(SFXManager.Instance?.sfxGameStarting);
        int count = trackingProvider != null ? trackingProvider.GetAssignedPlayerCount() : 0;
        Debug.Log($"[StartScreenManager] Launching with {count} player(s){(GameSession.VsCpu ? " vs CPU" : "")}");
        SceneManager.LoadScene(gameSceneName);
    }

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    void LaunchKeyboardDebug(bool vsCpu)
    {
        // No assignments → GameScreenManager falls back to keyboard control
        if (trackingProvider != null)
            trackingProvider.ClearAllAssignments();
        CurrentState = LobbyState.Launching;
        GameSession.VsCpu = vsCpu;
        Debug.Log($"[StartScreenManager] Debug keyboard launch{(vsCpu ? " vs CPU" : " (2 players)")}");
        SceneManager.LoadScene(gameSceneName);
    }
    #endif

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

    #if UNITY_EDITOR
    void OnGUI()
    {
        if (!debugFaceCrop && !debugGestureTracking) return;

        float padding = 10f;

        // ── Face Crop debug ──────────────────────────────────────────
        if (debugFaceCrop)
        {
            float previewSize = 128f;
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

        // ── Gesture Tracking debug ───────────────────────────────────
        if (debugGestureTracking && trackingProvider != null && trackingProvider.IsZEDReady)
        {
            var bodies = trackingProvider.GetAllCurrentBodies();
            float diagY = padding;
            float diagW = Screen.width - 20f;
            GUI.Box(new UnityEngine.Rect(6, diagY, diagW, 20 + bodies.Count * 18), "Gesture Tracking");
            diagY += 20;
            foreach (var kvp in bodies)
            {
                Vector3[] kp = GetWorldKeypoints(kvp.Value);
                float[] conf = kvp.Value.rawBodyData.keypointConfidence;
                string diag = $"Body {kvp.Key}: {GestureDetector.GetDiagnostics(kp, conf)}";

                float st;
                stillTime.TryGetValue(kvp.Key, out st);
                bool facing = IsFacingCamera(kvp.Value);
                diag += $" | facing={facing} still={st:F1}s score={ScoreBody(kvp.Value):F0}";

                GUI.Label(new UnityEngine.Rect(10, diagY, diagW - 8, 18), diag);
                diagY += 18;
            }
        }
    }
    #endif

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
                    p1StatusEl.text = "P1: Raise arms to confirm!";
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
                case LobbyState.WaitingForP2:
                    p2StatusEl.text = "P2: Step into view...";
                    break;
                case LobbyState.WaitingForP2Confirm:
                    p2StatusEl.text = "P2: Raise arms to confirm!";
                    break;
                default:
                    p2StatusEl.text = trackingProvider != null && trackingProvider.IsPlayerAssigned(2)
                        ? "P2: Ready!"
                        : "P2: CPU";
                    break;
            }
        }

        if (countdownEl != null)
        {
            if (CurrentState == LobbyState.WaitingForP2 || CurrentState == LobbyState.WaitingForP2Confirm
                || CurrentState == LobbyState.Launching)
            {
                bool p2Joined = trackingProvider != null && trackingProvider.IsPlayerAssigned(2);
                string mode = p2Joined ? "multiplayer" : "vs CPU";
                countdownEl.text = $"Starting {mode} in {Mathf.CeilToInt(Mathf.Max(0f, p2Countdown))}s";
                countdownEl.style.display = DisplayStyle.Flex;
            }
            else
            {
                countdownEl.style.display = DisplayStyle.None;
            }
        }
    }
}
