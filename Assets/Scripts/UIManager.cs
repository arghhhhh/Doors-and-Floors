using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;

public class UIManager : MonoBehaviour
{
    [Header("High Score Entry Template")]
    [SerializeField] VisualTreeAsset highScoreEntryTemplate;

    // UI Toolkit elements
    Heading timerEl;
    VisualElement winPanelEl;
    Heading winnerTextEl;
    VisualElement winnerPhotoEl;
    Heading finalTimeEl;
    Unity.AppUI.UI.Text highScoreLabelEl;
    VisualElement highScoreListEl;
    Unity.AppUI.UI.Text instructionsEl;

    // Blink state for high score label
    bool highScoreLabelVisible = true;

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
            instructionsEl.text = "Press ENTER to play again\nPress ESC for main menu";
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

            if (highScoreEntryTemplate != null)
            {
                entry = highScoreEntryTemplate.Instantiate();
            }
            else
            {
                // Fallback: create elements manually
                entry = new VisualElement();
                entry.name = "score-entry";
                entry.AddToClassList("score-entry");

                var photo = new VisualElement();
                photo.name = "score-photo";
                photo.AddToClassList("score-photo");
                entry.Add(photo);

                var text = new Label();
                text.name = "score-text";
                text.AddToClassList("score-text");
                entry.Add(text);
            }

            // Set photo
            Texture2D photo2d = HighScoreManager.Instance.LoadProfilePhoto(scores[i]);
            var photoEl = entry.Q<VisualElement>("score-photo");
            if (photoEl != null && photo2d != null)
            {
                photoEl.style.backgroundImage = new StyleBackground(photo2d);
            }

            // Set text (App UI Text inherits LocalizedTextElement, not TextElement)
            int m = (int)(scores[i].time / 60f);
            int s = (int)(scores[i].time % 60f);
            int ms = (int)((scores[i].time * 100f) % 100f);
            string timeStr = $"{i + 1}. {m:00}:{s:00}:{ms:00}";

            var appuiTextEl = entry.Q<Unity.AppUI.UI.Text>("score-text");
            if (appuiTextEl != null)
            {
                appuiTextEl.text = timeStr;
            }
            else
            {
                // Fallback for manually created Label
                var labelEl = entry.Q<Label>("score-text");
                if (labelEl != null)
                    labelEl.text = timeStr;
            }

            highScoreListEl.Add(entry);
        }
    }
}
