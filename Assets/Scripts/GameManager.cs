using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Tutorial, Playing, Won }
    public GameState CurrentState { get; private set; } = GameState.Tutorial;

    static bool showedTutorial;

    [Header("References")]
    public DoorPairGenerator doorGenerator;
    public UIManager uiManager;

    [Header("Teleport")]
    [Tooltip("Delay in seconds before the player reappears at the destination door")]
    public float teleportDelay = 0.3f;

    [Header("Time Limit")]
    [Tooltip("Maximum game duration in seconds before auto-returning to menu (0 = no limit)")]
    public float maxGameTime = 300f;

    float elapsedTime;
    int winnerPlayerNumber;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-find references if not assigned in inspector
        if (doorGenerator == null)
            doorGenerator = GetComponent<DoorPairGenerator>();
        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();
    }

    void Start()
    {
        elapsedTime = 0f;

        if (doorGenerator != null)
            doorGenerator.Generate();

        if (!showedTutorial)
        {
            CurrentState = GameState.Tutorial;
            FreezeAllPlayers();
            if (uiManager != null)
                uiManager.ShowTutorial(OnTutorialComplete);
        }
        else
        {
            CurrentState = GameState.Playing;
            SFXManager.Instance?.Play(SFXManager.Instance?.voiceGoGoGo);
        }
    }

    void FreezeAllPlayers()
    {
        foreach (var p in FindObjectsOfType<PlayerController>())
        {
            p.FreezeForWin();
            p.SetVisible(false);
        }
    }

    void UnfreezeAllPlayers()
    {
        foreach (var p in FindObjectsOfType<PlayerController>())
            p.ResetPlayer(GetSpawnPosition(p.playerNumber));
    }

    void OnTutorialComplete()
    {
        showedTutorial = true;
        UnfreezeAllPlayers();
        CurrentState = GameState.Playing;
        SFXManager.Instance?.Play(SFXManager.Instance?.voiceGoGoGo);
    }

    void Update()
    {
        if (CurrentState == GameState.Playing)
        {
            elapsedTime += Time.deltaTime;
            if (uiManager != null)
            {
                uiManager.UpdateTimer(elapsedTime);
            }

            // Time limit — return to menu if exceeded
            if (maxGameTime > 0f && elapsedTime >= maxGameTime)
            {
                ReturnToMenu();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Debug: Q key instantly wins as Player 1
            if (Input.GetKeyDown(KeyCode.Q))
            {
                DebugWinPlayer1();
            }

            // Debug: T key toggles tutorial modal
            if (Input.GetKeyDown(KeyCode.T))
            {
                if (uiManager != null)
                    uiManager.DebugToggleTutorial();
            }
#endif
        }

        // Restart
        if (CurrentState == GameState.Won)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                Restart();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnToMenu();
            }
        }
    }

    public void PlayerWon(int playerNumber)
    {
        if (CurrentState == GameState.Won) return;

        CurrentState = GameState.Won;
        winnerPlayerNumber = playerNumber;

        // Freeze all players immediately — stop all physics and coroutines
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        PlayerController winner = null;
        foreach (var p in players)
        {
            p.StopAllCoroutines();
            p.FreezeForWin();
            if (p.playerNumber == playerNumber) winner = p;
        }

        bool cpuWon = winner != null && winner.isCpu;

        // CPU times never go on the leaderboard
        bool isHighScore = !cpuWon
            && HighScoreManager.Instance != null
            && HighScoreManager.Instance.IsHighScore(elapsedTime);

        // Play win sound based on player count
        if (SFXManager.Instance != null)
        {
            if (cpuWon)
                SFXManager.Instance.Play(SFXManager.Instance.voiceCpuWins != null
                    ? SFXManager.Instance.voiceCpuWins
                    : SFXManager.Instance.voiceP2Wins);
            else if (players.Length <= 1)
                SFXManager.Instance.Play(SFXManager.Instance.voiceWinner);
            else if (playerNumber == 1)
                SFXManager.Instance.Play(SFXManager.Instance.voiceP1Wins);
            else
                SFXManager.Instance.Play(SFXManager.Instance.voiceP2Wins);
        }

        if (uiManager != null)
        {
            string winnerLabel = cpuWon ? "CPU Wins!" : $"Player {playerNumber} Wins!";
            uiManager.ShowWinPanel(winnerLabel, elapsedTime, isHighScore,
                winner != null ? winner.profilePhoto : null);
        }

        if (isHighScore && HighScoreManager.Instance != null)
        {
            SFXManager.Instance?.Play(SFXManager.Instance?.voiceNewHighscore);
            Texture2D photo = winner != null ? winner.profilePhoto : null;
            HighScoreManager.Instance.AddScore($"Player {playerNumber}", elapsedTime, photo);
        }
    }

    public void ReturnToMenu()
    {
        showedTutorial = false;
        SFXManager.Instance?.Play(SFXManager.Instance?.sfxReturnHome);

        if (ZEDTrackingProvider.Instance != null)
            ZEDTrackingProvider.Instance.ClearAllAssignments();

        Instance = null;
        SceneManager.LoadScene("StartScreen");
    }

    public void Restart()
    {
        // Reset players — kills coroutines and invalidates pending teleports
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.ResetPlayer(GetSpawnPosition(p.playerNumber));
        }

        // Regenerate doors
        if (doorGenerator != null)
            doorGenerator.Generate();

        // Reset finish line
        FinishLine finishLine = FindObjectOfType<FinishLine>();
        if (finishLine != null)
            finishLine.ResetFinishLine();

        // Hide win panel
        if (uiManager != null)
            uiManager.HideWinPanel();

        elapsedTime = 0f;

        // Delay setting Playing state by one frame so triggers clear
        StartCoroutine(EnablePlayingNextFrame());
    }

    IEnumerator EnablePlayingNextFrame()
    {
        yield return new WaitForFixedUpdate();

        // Ensure players are on Floor_0 before starting
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            p.ResetPlayer(GetSpawnPosition(p.playerNumber));
        }

        CurrentState = GameState.Playing;
        SFXManager.Instance?.Play(SFXManager.Instance?.voiceGoGoGo);
    }

    public Vector3 GetSpawnPosition(int playerNumber)
    {
        GameObject f0 = GameObject.Find("Floor_0");
        float spawnY = f0 != null ? f0.transform.position.y + f0.transform.localScale.y * 0.5f + 0.35f : 1f;
        float x = playerNumber == 1 ? -3f : 3f;
        return new Vector3(x, spawnY, 0f);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void DebugWinPlayer1()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p.playerNumber == 1)
            {
                // Place Player 1 directly in the WinZone (Y=12.5) grounded
                p.transform.position = new Vector3(0f, 12.5f, -0.3f);
                Rigidbody rb = p.GetComponent<Rigidbody>();
                if (rb != null) rb.velocity = Vector3.zero;
                return;
            }
        }
    }
#endif

    public float GetElapsedTime() => elapsedTime;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
