using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("StartScreen")]
    public AudioClip sfxGameStarting;
    public AudioClip sfxPlayerTracked;
    public AudioClip voiceP1Activated;
    public AudioClip voiceP2Activated;
    public AudioClip sfxPlayerLost;

    [Header("GameScreen")]
    public AudioClip voiceGoGoGo;
    public AudioClip voiceWinner;
    public AudioClip voiceP1Wins;
    public AudioClip voiceP2Wins;
    [Tooltip("Played when the CPU wins; falls back to voiceP2Wins if unassigned")]
    public AudioClip voiceCpuWins;
    public AudioClip voiceNewHighscore;
    public AudioClip voiceTrackingLost;
    public AudioClip sfxReturnHome;
    public AudioClip sfxTeleport;

    AudioSource audioSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
 
