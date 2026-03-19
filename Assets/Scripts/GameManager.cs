using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, Won }
    public GameState CurrentState { get; private set; } = GameState.Playing;

    [Header("References")]
    public DoorPairGenerator doorGenerator;
    public UIManager uiManager;

    [Header("Teleport")]
    [Tooltip("Delay in seconds before the player reappears at the destination door")]
    public float teleportDelay = 0.3f;

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
        CurrentState = GameState.Playing;
        elapsedTime = 0f;

        if (doorGenerator != null)
        {
            doorGenerator.Generate();
        }
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
                SceneManager.LoadScene("StartScreen");
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

        bool isHighScore = HighScoreManager.Instance != null
            && HighScoreManager.Instance.IsHighScore(elapsedTime);

        if (uiManager != null)
        {
            uiManager.ShowWinPanel(playerNumber, elapsedTime, isHighScore,
                winner != null ? winner.profilePhoto : null);
        }

        if (isHighScore && HighScoreManager.Instance != null)
        {
            Texture2D photo = winner != null ? winner.profilePhoto : null;
            HighScoreManager.Instance.AddScore($"Player {playerNumber}", elapsedTime, photo);
        }
    }

    void Restart()
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
    }

    public Vector3 GetSpawnPosition(int playerNumber)
    {
        GameObject f0 = GameObject.Find("Floor_0");
        float spawnY = f0 != null ? f0.transform.position.y + f0.transform.localScale.y * 0.5f + 0.35f : 1f;
        float x = playerNumber == 1 ? -3f : 3f;
        return new Vector3(x, spawnY, 0f);
    }

    public float GetElapsedTime() => elapsedTime;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
