using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class HighScoreManager : MonoBehaviour
{
    public static HighScoreManager Instance { get; private set; }

    const int MaxScores = 10;
    const string FileName = "highscores.json";

    [Serializable]
    public class ScoreEntry
    {
        public string playerName;
        public float time;
        public string profilePhotoBase64; // PNG encoded as base64
    }

    [Serializable]
    class ScoreData
    {
        public List<ScoreEntry> scores = new List<ScoreEntry>();
    }

    ScoreData data;
    string filePath;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        filePath = Path.Combine(Application.persistentDataPath, FileName);
        Load();
    }

    public bool IsHighScore(float time)
    {
        if (data.scores.Count < MaxScores) return true;
        return time < data.scores[data.scores.Count - 1].time;
    }

    public void AddScore(string playerName, float time, Texture2D profilePhoto = null)
    {
        string photoBase64 = "";
        if (profilePhoto != null)
        {
            // Create a readable copy to encode
            RenderTexture rt = RenderTexture.GetTemporary(profilePhoto.width, profilePhoto.height);
            Graphics.Blit(profilePhoto, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D readable = new Texture2D(profilePhoto.width, profilePhoto.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, profilePhoto.width, profilePhoto.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            byte[] png = readable.EncodeToPNG();
            photoBase64 = Convert.ToBase64String(png);
            UnityEngine.Object.Destroy(readable);
        }

        data.scores.Add(new ScoreEntry
        {
            playerName = playerName,
            time = time,
            profilePhotoBase64 = photoBase64
        });
        data.scores.Sort((a, b) => a.time.CompareTo(b.time));

        if (data.scores.Count > MaxScores)
            data.scores.RemoveRange(MaxScores, data.scores.Count - MaxScores);

        Save();
    }

    public Texture2D LoadProfilePhoto(ScoreEntry entry)
    {
        if (string.IsNullOrEmpty(entry.profilePhotoBase64)) return null;

        try
        {
            byte[] png = Convert.FromBase64String(entry.profilePhotoBase64);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(png);
            return tex;
        }
        catch
        {
            return null;
        }
    }

    public List<ScoreEntry> GetScores() => data.scores;

    void Load()
    {
        data = new ScoreData();
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                data = JsonUtility.FromJson<ScoreData>(json) ?? new ScoreData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load high scores: {e.Message}");
                data = new ScoreData();
            }
        }
    }

    void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to save high scores: {e.Message}");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
