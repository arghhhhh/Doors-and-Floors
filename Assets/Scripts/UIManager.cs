using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
using Unity.AppUI.UI;
using sl;

public class UIManager : MonoBehaviour
{
    [Header("High Score Entry Template")]
    [SerializeField] VisualTreeAsset highScoreEntryTemplate;

    [Header("Win Screen")]
    [Tooltip("Last-frame gesture image shown next to restart prompt")]
    public Texture2D gestureIcon;
    [Tooltip("Countdown duration before auto-return to menu")]
    public float winCountdownDuration = 10f;

    [Header("Tutorial")]
    public VideoClip runClip;
    public VideoClip jumpClip;
    public float tutorialDuration = 10f;

    // UI Toolkit elements
    Heading timerEl;
    VisualElement winPanelEl;
    Heading winnerTextEl;
    VisualElement winnerPhotoEl;
    Heading finalTimeEl;
    Unity.AppUI.UI.Text highScoreLabelEl;
    VisualElement highScoreListEl;
    Unity.AppUI.UI.Text instructionsEl;
    VisualElement gestureIconEl;
    Unity.AppUI.UI.Text countdownEl;

    // Blink state for high score label
    bool highScoreLabelVisible = true;

    // Win countdown state
    float winCountdown;
    bool winCountdownActive;
    float gestureHoldTime;
    const float gestureHoldRequired = 1f;

    // Tutorial state
    VisualElement tutorialPanelEl;
    VisualElement tutorialVideoRunEl;
    VisualElement tutorialVideoJumpEl;
    Unity.AppUI.UI.Text tutorialCountdownEl;
    float tutorialCountdown;
    bool tutorialActive;
    Action tutorialCompleteCallback;
    RenderTexture runRT;
    RenderTexture jumpRT;
    VideoPlayer runPlayer;
    VideoPlayer jumpPlayer;

    void Awake()
    {
        var uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) return;

        var root = uiDoc.rootVisualElement;
        timerEl = root.Q<Heading>("timer-text");
        winPanelEl = root.Q<VisualElement>("win-panel");
        winnerTextEl = root.Q<Heading>("winner-text");
        winnerPhotoEl = root.Q<VisualElement>("winner-photo");
        finalTimeEl = root.Q<Heading>("final-time-text");
        highScoreLabelEl = root.Q<Unity.AppUI.UI.Text>("high-score-label");
        highScoreListEl = root.Q<VisualElement>("high-score-list");
        instructionsEl = root.Q<Unity.AppUI.UI.Text>("instructions-text");
        gestureIconEl = root.Q<VisualElement>("gesture-icon");
        countdownEl = root.Q<Unity.AppUI.UI.Text>("countdown-text");
        tutorialPanelEl = root.Q<VisualElement>("tutorial-panel");
        tutorialVideoRunEl = root.Q<VisualElement>("tutorial-video-run");
        tutorialVideoJumpEl = root.Q<VisualElement>("tutorial-video-jump");
        tutorialCountdownEl = root.Q<Unity.AppUI.UI.Text>("tutorial-countdown");
    }

    void Update()
    {
        if (tutorialActive)
        {
            tutorialCountdown -= Time.deltaTime;
            if (tutorialCountdownEl != null)
                tutorialCountdownEl.text = $"Starting in {Mathf.CeilToInt(Mathf.Max(0f, tutorialCountdown))}s";

            if (tutorialCountdown <= 0f)
                HideTutorial();

            return;
        }

        if (!winCountdownActive) return;

        // Countdown to auto-return to menu
        winCountdown -= Time.deltaTime;
        if (countdownEl != null)
            countdownEl.text = $"Returning to menu in {Mathf.CeilToInt(Mathf.Max(0f, winCountdown))}s";

        if (winCountdown <= 0f)
        {
            winCountdownActive = false;
            GameManager.Instance?.ReturnToMenu();
            return;
        }

        // Check for hands-up gesture from any tracked body
        CheckGestureRestart();
    }

    void CheckGestureRestart()
    {
        ZEDTrackingProvider provider = ZEDTrackingProvider.Instance;
        if (provider == null || !provider.IsZEDReady) return;

        var bodies = provider.GetAllCurrentBodies();
        bool anyGesture = false;

        foreach (var kvp in bodies)
        {
            DetectedBody body = kvp.Value;
            int count = body.rawBodyData.keypoint.Length;
            Vector3[] keypoints = new Vector3[count];
            for (int i = 0; i < count; i++)
                keypoints[i] = provider.GetKeypointWorld(body, i);

            if (GestureDetector.IsFieldGoalGesture(keypoints, body.rawBodyData.keypointConfidence))
            {
                anyGesture = true;
                break;
            }
        }

        if (anyGesture)
        {
            gestureHoldTime += Time.deltaTime;
            if (gestureHoldTime >= gestureHoldRequired)
            {
                winCountdownActive = false;
                GameManager.Instance?.Restart();
            }
        }
        else
        {
            gestureHoldTime = 0f;
        }
    }

    void Start()
    {
        HideWinPanel();
    }

    public void HideWinPanel()
    {
        if (winPanelEl != null)
            winPanelEl.style.display = DisplayStyle.None;

        if (highScoreListEl != null)
            highScoreListEl.Clear();

        winCountdownActive = false;
    }

    public void ShowTutorial(Action onComplete)
    {
        tutorialCompleteCallback = onComplete;
        tutorialCountdown = tutorialDuration;
        tutorialActive = true;

        if (tutorialPanelEl != null)
            tutorialPanelEl.style.display = DisplayStyle.Flex;

        // Create RenderTextures
        runRT = new RenderTexture(512, 512, 0);
        jumpRT = new RenderTexture(512, 512, 0);

        // Create VideoPlayers as child GameObjects
        runPlayer = CreateTutorialPlayer("TutorialRunPlayer", runClip, runRT);
        jumpPlayer = CreateTutorialPlayer("TutorialJumpPlayer", jumpClip, jumpRT);

        // Assign RenderTextures to UI elements
        if (tutorialVideoRunEl != null)
            tutorialVideoRunEl.style.backgroundImage = Background.FromRenderTexture(runRT);
        if (tutorialVideoJumpEl != null)
            tutorialVideoJumpEl.style.backgroundImage = Background.FromRenderTexture(jumpRT);
    }

    VideoPlayer CreateTutorialPlayer(string name, VideoClip clip, RenderTexture rt)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);
        VideoPlayer vp = go.AddComponent<VideoPlayer>();
        vp.clip = clip;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        vp.isLooping = true;
        vp.playOnAwake = false;
        vp.audioOutputMode = VideoAudioOutputMode.None;
        vp.Play();
        return vp;
    }

    void HideTutorial()
    {
        tutorialActive = false;

        if (tutorialPanelEl != null)
            tutorialPanelEl.style.display = DisplayStyle.None;

        // Clean up VideoPlayers and RenderTextures
        if (runPlayer != null) { Destroy(runPlayer.gameObject); runPlayer = null; }
        if (jumpPlayer != null) { Destroy(jumpPlayer.gameObject); jumpPlayer = null; }
        if (runRT != null) { runRT.Release(); Destroy(runRT); runRT = null; }
        if (jumpRT != null) { jumpRT.Release(); Destroy(jumpRT); jumpRT = null; }

        tutorialCompleteCallback?.Invoke();
        tutorialCompleteCallback = null;
    }

    public void DebugToggleTutorial()
    {
        if (tutorialActive)
            HideTutorial();
        else
            ShowTutorial(null);
    }

    public void UpdateTimer(float time)
    {
        if (timerEl != null)
        {
            int minutes = (int)(time / 60f);
            int seconds = (int)(time % 60f);
            int millis = (int)((time * 100f) % 100f);
            timerEl.text = $"{minutes:00}:{seconds:00}:{millis:00}";
        }
    }

    public void ShowWinPanel(int playerNumber, float time, bool isHighScore, Texture2D profilePhoto = null)
    {
        if (winPanelEl == null) return;

        winPanelEl.style.display = DisplayStyle.Flex;

        if (winnerTextEl != null)
            winnerTextEl.text = $"Player {playerNumber} Wins!";

        if (winnerPhotoEl != null)
        {
            if (profilePhoto != null)
            {
                winnerPhotoEl.style.backgroundImage = new StyleBackground(profilePhoto);
                winnerPhotoEl.style.display = DisplayStyle.Flex;
            }
            else
            {
                winnerPhotoEl.style.display = DisplayStyle.None;
            }
        }

        if (finalTimeEl != null)
        {
            int minutes = (int)(time / 60f);
            int seconds = (int)(time % 60f);
            int millis = (int)((time * 100f) % 100f);
            finalTimeEl.text = $"Time: {minutes:00}:{seconds:00}:{millis:00}";
        }

        if (highScoreLabelEl != null)
        {
            highScoreLabelEl.text = isHighScore ? "NEW HIGH SCORE!" : "";

            // Blink the high score label
            if (isHighScore)
            {
                highScoreLabelVisible = true;
                highScoreLabelEl.schedule.Execute(() =>
                {
                    highScoreLabelVisible = !highScoreLabelVisible;
                    highScoreLabelEl.style.opacity = highScoreLabelVisible ? 1f : 0f;
                }).Every(500);
            }
        }

        BuildHighScoreList();

        if (instructionsEl != null)
        {
            instructionsEl.text = "Raise hands to play again";
            bool visible = true;
            instructionsEl.schedule.Execute(() =>
            {
                visible = !visible;
                instructionsEl.style.opacity = visible ? 1f : 0f;
            }).Every(750);
        }

        // Start countdown
        winCountdown = winCountdownDuration;
        winCountdownActive = true;
        gestureHoldTime = 0f;
    }

    void BuildHighScoreList()
    {
        if (highScoreListEl != null)
            highScoreListEl.Clear();

        if (HighScoreManager.Instance == null) return;
        if (highScoreListEl == null) return;

        List<HighScoreManager.ScoreEntry> scores = HighScoreManager.Instance.GetScores();

        for (int i = 0; i < scores.Count; i++)
        {
            VisualElement entry;

            entry = new VisualElement();
            entry.name = "score-entry";
            entry.AddToClassList("score-entry");

            var rank = new Label();
            rank.name = "score-rank";
            rank.AddToClassList("score-rank");
            entry.Add(rank);

            var photo = new VisualElement();
            photo.name = "score-photo";
            photo.AddToClassList("score-photo");
            entry.Add(photo);

            var time = new Label();
            time.name = "score-time";
            time.AddToClassList("score-time");
            entry.Add(time);

            // Set rank
            rank.text = $"#{i + 1}";

            // Set photo
            Texture2D photo2d = HighScoreManager.Instance.LoadProfilePhoto(scores[i]);
            if (photo != null && photo2d != null)
                photo.style.backgroundImage = new StyleBackground(photo2d);

            // Set time
            int m = (int)(scores[i].time / 60f);
            int s = (int)(scores[i].time % 60f);
            int ms = (int)((scores[i].time * 100f) % 100f);
            time.text = $"{m:00}:{s:00}:{ms:00}";

            highScoreListEl.Add(entry);
        }
    }
}
