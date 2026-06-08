using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int MaxUnlockedLevel { get; private set; } = 0;
    public int CurrentLevel { get; set; } = 0;

    public float[] PreAnxiety  = new float[5];
    public float[] PostAnxiety = new float[5];

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProgress();
    }

    public void CompleteLevel(int levelIndex, float pre, float post)
    {
        PreAnxiety[levelIndex]  = pre;
        PostAnxiety[levelIndex] = post;
        if (levelIndex + 1 > MaxUnlockedLevel)
            MaxUnlockedLevel = levelIndex + 1;
        SaveProgress();
    }

    void SaveProgress()
    {
        PlayerPrefs.SetInt("MaxUnlocked", MaxUnlockedLevel);
        for (int i = 0; i < 5; i++)
        {
            PlayerPrefs.SetFloat($"Pre_{i}",  PreAnxiety[i]);
            PlayerPrefs.SetFloat($"Post_{i}", PostAnxiety[i]);
        }
        PlayerPrefs.Save();
    }

    void LoadProgress()
    {
        MaxUnlockedLevel = PlayerPrefs.GetInt("MaxUnlocked", 0);
        for (int i = 0; i < 5; i++)
        {
            PreAnxiety[i]  = PlayerPrefs.GetFloat($"Pre_{i}", 0f);
            PostAnxiety[i] = PlayerPrefs.GetFloat($"Post_{i}", 0f);
        }
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
        MaxUnlockedLevel = 0;
        PreAnxiety  = new float[5];
        PostAnxiety = new float[5];
    }
    public LevelData CurrentLevelData { get; set; }
    // 기존 코드에 한 줄 추가
    public string LastTranscript { get; set; } = "";
}