using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Timer")]
    public Text timerText;

    [Header("Win Panel")]
    public GameObject winPanel;
    public Text winnerText;
    public RawImage winnerPhoto;
    public Text finalTimeText;
    public Text highScoreLabel;
    public Transform highScoreListContainer;
    public Text instructionsText;

    [Header("High Score Entry Prefab")]
    public float scoreEntryHeight = 40f;
    public float scorePhotoSize = 36f;

    List<GameObject> spawnedScoreEntries = new List<GameObject>();

    void Awake()
    {
        // Auto-find references by name if not assigned
        if (timerText == null)
        {
            var go = transform.Find("TimerText");
            if (go != null) timerText = go.GetComponent<Text>();
        }

        if (winPanel == null)
        {
            var go = transform.Find("WinPanel");
            if (go != null)
            {
                winPanel = go.gameObject;

                if (winnerText == null)
                {
                    var t = go.Find("WinnerText");
                    if (t != null) winnerText = t.GetComponent<Text>();
                }
                if (winnerPhoto == null)
                {
                    var t = go.Find("WinnerPhoto");
                    if (t != null)
                    {
                        winnerPhoto = t.GetComponent<RawImage>();
                    }
                    else
                    {
                        GameObject photoObj = new GameObject("WinnerPhoto");
                        photoObj.transform.SetParent(go, false);
                        winnerPhoto = photoObj.AddComponent<RawImage>();
                        RectTransform rt = photoObj.GetComponent<RectTransform>();
                        rt.anchorMin = new Vector2(0.35f, 0.55f);
                        rt.anchorMax = new Vector2(0.65f, 0.75f);
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                }
                if (finalTimeText == null)
                {
                    var t = go.Find("FinalTimeText");
                    if (t != null) finalTimeText = t.GetComponent<Text>();
                }
                if (highScoreLabel == null)
                {
                    var t = go.Find("HighScoreLabel");
                    if (t != null) highScoreLabel = t.GetComponent<Text>();
                }
                if (highScoreListContainer == null)
                {
                    var t = go.Find("HighScoreList");
                    if (t != null)
                    {
                        highScoreListContainer = t;
                    }
                    else
                    {
                        GameObject listObj = new GameObject("HighScoreList");
                        listObj.transform.SetParent(go, false);
                        highScoreListContainer = listObj.transform;
                        RectTransform rt = listObj.AddComponent<RectTransform>();
                        rt.anchorMin = new Vector2(0.1f, 0.15f);
                        rt.anchorMax = new Vector2(0.9f, 0.55f);
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                }
                if (instructionsText == null)
                {
                    var t = go.Find("InstructionsText");
                    if (t != null) instructionsText = t.GetComponent<Text>();
                }
            }
        }
    }

    void Start()
    {
        if (winPanel != null)
            winPanel.SetActive(false);
    }

    public void HideWinPanel()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        ClearScoreEntries();
    }

    public void UpdateTimer(float time)
    {
        if (timerText != null)
        {
            int minutes = (int)(time / 60f);
            int seconds = (int)(time % 60f);
            int millis = (int)((time * 100f) % 100f);
            timerText.text = $"{minutes:00}:{seconds:00}:{millis:00}";
        }
    }

    public void ShowWinPanel(int playerNumber, float time, bool isHighScore, Texture2D profilePhoto = null)
    {
        if (winPanel == null) return;

        winPanel.SetActive(true);

        if (winnerText != null)
            winnerText.text = $"Player {playerNumber} Wins!";

        if (winnerPhoto != null)
        {
            if (profilePhoto != null)
            {
                winnerPhoto.texture = profilePhoto;
                winnerPhoto.gameObject.SetActive(true);
            }
            else
            {
                winnerPhoto.gameObject.SetActive(false);
            }
        }

        if (finalTimeText != null)
        {
            int minutes = (int)(time / 60f);
            int seconds = (int)(time % 60f);
            int millis = (int)((time * 100f) % 100f);
            finalTimeText.text = $"Time: {minutes:00}:{seconds:00}:{millis:00}";
        }

        if (highScoreLabel != null)
            highScoreLabel.text = isHighScore ? "NEW HIGH SCORE!" : "";

        BuildHighScoreList();

        if (instructionsText != null)
            instructionsText.text = "Press ENTER to play again\nPress ESC for main menu";
    }

    void BuildHighScoreList()
    {
        ClearScoreEntries();

        if (HighScoreManager.Instance == null) return;
        if (highScoreListContainer == null) return;

        List<HighScoreManager.ScoreEntry> scores = HighScoreManager.Instance.GetScores();

        for (int i = 0; i < scores.Count; i++)
        {
            GameObject entry = new GameObject($"ScoreEntry_{i}");
            entry.transform.SetParent(highScoreListContainer, false);

            RectTransform entryRect = entry.AddComponent<RectTransform>();
            entryRect.anchorMin = new Vector2(0f, 1f);
            entryRect.anchorMax = new Vector2(1f, 1f);
            entryRect.pivot = new Vector2(0.5f, 1f);
            entryRect.anchoredPosition = new Vector2(0f, -i * scoreEntryHeight);
            entryRect.sizeDelta = new Vector2(0f, scoreEntryHeight);

            // Profile photo
            Texture2D photo = HighScoreManager.Instance.LoadProfilePhoto(scores[i]);
            if (photo != null)
            {
                GameObject photoObj = new GameObject("Photo");
                photoObj.transform.SetParent(entry.transform, false);
                RawImage img = photoObj.AddComponent<RawImage>();
                img.texture = photo;
                RectTransform photoRect = photoObj.GetComponent<RectTransform>();
                photoRect.anchorMin = new Vector2(0f, 0.5f);
                photoRect.anchorMax = new Vector2(0f, 0.5f);
                photoRect.pivot = new Vector2(0f, 0.5f);
                photoRect.anchoredPosition = new Vector2(5f, 0f);
                photoRect.sizeDelta = new Vector2(scorePhotoSize, scorePhotoSize);
            }

            // Score text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(entry.transform, false);
            Text text = textObj.AddComponent<Text>();
            int m = (int)(scores[i].time / 60f);
            int s = (int)(scores[i].time % 60f);
            int ms = (int)((scores[i].time * 100f) % 100f);
            text.text = $"{i + 1}. {scores[i].playerName} - {m:00}:{s:00}:{ms:00}";
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            float textOffset = photo != null ? scorePhotoSize + 10f : 5f;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(textOffset, 0f);
            textRect.offsetMax = Vector2.zero;

            spawnedScoreEntries.Add(entry);
        }
    }

    void ClearScoreEntries()
    {
        foreach (var entry in spawnedScoreEntries)
        {
            if (entry != null) Destroy(entry);
        }
        spawnedScoreEntries.Clear();
    }
}
