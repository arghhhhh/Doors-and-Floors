using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using sl;

/// <summary>
/// Game scene orchestrator that reads player assignments from ZEDTrackingProvider,
/// activates player GameObjects, and attaches BodyTrackingInput components.
/// In vs-CPU mode (GameSession.VsCpu) Player2 is driven by CpuPlayerInput.
/// Handles player removal on tracking loss timeout.
/// </summary>
public class GameScreenManager : MonoBehaviour
{
    [Header("Player References")]
    [Tooltip("Player1 GameObject in scene")]
    public GameObject player1Object;
    [Tooltip("Player2 GameObject in scene")]
    public GameObject player2Object;

    [Header("Tracking Loss UI Prefab")]
    [Tooltip("Optional prefab for tracking lost indicator. If null, a simple one is created.")]
    public GameObject trackingLostPrefab;

    [Header("Shaders (must be assigned so they're included in builds)")]
    public Shader trackingLostShader;

    [Header("CPU Opponent")]
    [Tooltip("Keyboard fallback (no ZED players): Player2 is the CPU instead of arrow keys")]
    public bool keyboardVsCpu = false;
    [Tooltip("Optional portrait shown on the win screen when the CPU wins")]
    public Texture2D cpuProfilePhoto;

    [Header("Scene Navigation")]
    public string startScreenSceneName = "StartScreen";
    public float returnToStartDelay = 2f;

    // Humans only — the CPU never keeps a game alive by itself
    int humanPlayerCount;
    bool returningToStart;

    void Start()
    {
        // Auto-find player references by name if not assigned in inspector
        if (player1Object == null) player1Object = GameObject.Find("Player1");
        if (player2Object == null) player2Object = GameObject.Find("Player2");

        ZEDTrackingProvider provider = ZEDTrackingProvider.Instance;

        if (provider == null || provider.GetAssignedPlayerCount() == 0)
        {
            // No ZED tracking — fall back to keyboard mode
            bool vsCpu = keyboardVsCpu || GameSession.VsCpu;
            Debug.Log($"[GameScreenManager] No body tracking assignments found. Keyboard mode{(vsCpu ? " vs CPU" : "")}.");
            SetupKeyboardMode(vsCpu);
            return;
        }

        // Deactivate both players initially
        if (player1Object != null) player1Object.SetActive(false);
        if (player2Object != null) player2Object.SetActive(false);

        humanPlayerCount = 0;

        // Activate and configure assigned players
        if (provider.IsPlayerAssigned(1))
            SetupTrackedPlayer(1, player1Object, provider);

        if (provider.IsPlayerAssigned(2))
            SetupTrackedPlayer(2, player2Object, provider);
        else if (GameSession.VsCpu)
            SetupCpuPlayer(player2Object);

        // If tutorial hasn't been shown yet, GameManager will freeze/hide players.
        // Re-apply freeze here since SetupTrackedPlayer calls ResetPlayer which makes them visible.
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Tutorial)
        {
            foreach (var pc in FindObjectsOfType<PlayerController>())
            {
                pc.FreezeForWin();
                pc.SetVisible(false);
            }
        }

        Debug.Log($"[GameScreenManager] Started with {humanPlayerCount} tracked player(s){(GameSession.VsCpu ? " vs CPU" : "")}.");
    }

    void SetupKeyboardMode(bool vsCpu)
    {
        // Player1 always on keyboard; Player2 on keyboard or CPU
        if (player1Object != null)
        {
            player1Object.SetActive(true);
            var pc = player1Object.GetComponent<PlayerController>();
            if (pc != null) pc.useExternalInput = false;
        }
        humanPlayerCount = 1;

        if (vsCpu)
        {
            SetupCpuPlayer(player2Object);
        }
        else if (player2Object != null)
        {
            player2Object.SetActive(true);
            var pc = player2Object.GetComponent<PlayerController>();
            if (pc != null) pc.useExternalInput = false;
            humanPlayerCount = 2;
        }
    }

    void SetupCpuPlayer(GameObject playerObj)
    {
        if (playerObj == null) return;

        playerObj.SetActive(true);

        PlayerController pc = playerObj.GetComponent<PlayerController>();
        if (pc == null) return;

        pc.useExternalInput = true;
        pc.isCpu = true;
        // Replace the inspector demo photo; null hides the photo on the win screen
        pc.SetProfilePhoto(cpuProfilePhoto);

        // Normally pre-added (disabled) on Player2 so difficulty is tuned in the scene
        CpuPlayerInput cpu = playerObj.GetComponent<CpuPlayerInput>();
        if (cpu == null)
            cpu = playerObj.AddComponent<CpuPlayerInput>();
        cpu.enabled = true;
    }

    void SetupTrackedPlayer(int playerNumber, GameObject playerObj, ZEDTrackingProvider provider)
    {
        if (playerObj == null) return;

        playerObj.SetActive(true);
        humanPlayerCount++;

        PlayerController pc = playerObj.GetComponent<PlayerController>();
        if (pc == null) return;

        pc.useExternalInput = true;

        // Apply captured profile photo from lobby (overrides inspector-assigned demo photo)
        Texture2D capturedPhoto = provider.GetPlayerProfilePhoto(playerNumber);
        if (capturedPhoto != null)
            pc.SetProfilePhoto(capturedPhoto);

        // Attach BodyTrackingInput
        BodyTrackingInput input = playerObj.GetComponent<BodyTrackingInput>();
        if (input == null)
            input = playerObj.AddComponent<BodyTrackingInput>();
        input.playerNumber = playerNumber;

        // Create tracking lost overlay (scan lines)
        TrackingLostOverlay overlay = playerObj.GetComponent<TrackingLostOverlay>();
        if (overlay == null)
            overlay = playerObj.AddComponent<TrackingLostOverlay>();
        overlay.scanLinesShader = trackingLostShader;
        input.trackingLostOverlay = overlay;

        // Create tracking lost indicator text
        CreateTrackingLostUI(playerObj, input);

        // Set initial spawn X from current physical position
        DetectedBody body = provider.GetBodyForPlayer(playerNumber);
        if (body != null)
        {
            Vector3 pelvis = provider.GetKeypointWorld(body, (int)BODY_38_PARTS.PELVIS);
            if (float.IsFinite(pelvis.x))
            {
                float normalizedX = Mathf.InverseLerp(input.physicalXMin, input.physicalXMax, pelvis.x);
                float mappedX = Mathf.Lerp(input.gameXMin, input.gameXMax, normalizedX);

                Vector3 spawnPos = GameManager.Instance != null
                    ? GameManager.Instance.GetSpawnPosition(playerNumber)
                    : pc.transform.position;
                spawnPos.x = mappedX;
                pc.ResetPlayer(spawnPos);
            }
        }
    }

    void CreateTrackingLostUI(GameObject playerObj, BodyTrackingInput input)
    {
        // Create a simple Canvas child for the tracking lost indicator
        GameObject indicator = new GameObject("TrackingLostIndicator");
        indicator.transform.SetParent(playerObj.transform);
        indicator.transform.localPosition = new Vector3(0f, 2f, 0f);

        Canvas canvas = indicator.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1.5f, 0.5f);
        indicator.transform.localScale = Vector3.one * 0.01f;

        // Red X text
        GameObject xTextObj = new GameObject("LostText");
        xTextObj.transform.SetParent(indicator.transform);
        UnityEngine.UI.Text xText = xTextObj.AddComponent<UnityEngine.UI.Text>();
        xText.text = "X TRACKING LOST";
        xText.color = Color.red;
        xText.fontSize = 24;
        xText.alignment = TextAnchor.MiddleCenter;
        xText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform xRect = xTextObj.GetComponent<RectTransform>();
        xRect.anchoredPosition = Vector2.zero;
        xRect.sizeDelta = new Vector2(200f, 30f);

        // Timer text
        GameObject timerObj = new GameObject("TimerText");
        timerObj.transform.SetParent(indicator.transform);
        UnityEngine.UI.Text timerText = timerObj.AddComponent<UnityEngine.UI.Text>();
        timerText.text = "";
        timerText.color = Color.red;
        timerText.fontSize = 20;
        timerText.alignment = TextAnchor.MiddleCenter;
        timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform timerRect = timerObj.GetComponent<RectTransform>();
        timerRect.anchoredPosition = new Vector2(0, -25f);
        timerRect.sizeDelta = new Vector2(200f, 30f);

        indicator.SetActive(false);

        input.trackingLostIndicator = indicator;
        input.trackingLostTimerText = timerText;
    }

    public void RemovePlayer(int playerNumber)
    {
        Debug.Log($"[GameScreenManager] Removing P{playerNumber} due to tracking loss timeout.");

        GameObject playerObj = playerNumber == 1 ? player1Object : player2Object;
        if (playerObj != null)
            playerObj.SetActive(false);

        if (ZEDTrackingProvider.Instance != null)
            ZEDTrackingProvider.Instance.UnassignPlayer(playerNumber);

        humanPlayerCount--;

        if (humanPlayerCount <= 0 && !returningToStart)
        {
            returningToStart = true;
            StartCoroutine(ReturnToStartScreen());
        }
    }

    IEnumerator ReturnToStartScreen()
    {
        Debug.Log("[GameScreenManager] All players lost. Returning to StartScreen...");
        SFXManager.Instance?.Play(SFXManager.Instance?.sfxReturnHome);
        yield return new WaitForSeconds(returnToStartDelay);

        if (ZEDTrackingProvider.Instance != null)
            ZEDTrackingProvider.Instance.ClearAllAssignments();

        SceneManager.LoadScene(startScreenSceneName);
    }
}
