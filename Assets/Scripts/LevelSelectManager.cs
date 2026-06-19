using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LevelSelectManager : MonoBehaviour
{
    [Header("레벨 데이터 연결")]
    public LevelData[] levels;

    void Start()
    {
        // 프로토타입: Lv.5만 활성화
        // 나중에 GameManager.MaxUnlockedLevel로 잠금 해제 가능
    }

    public void OnLevelButtonClicked(int levelIndex)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentLevel = levelIndex;
            if (levels != null && levelIndex >= 0 && levelIndex < levels.Length)
            {
                GameManager.Instance.CurrentLevelData = levels[levelIndex];
            }
            else
            {
                Debug.LogWarning("[LevelSelectManager] LevelData is missing for index " + levelIndex);
                GameManager.Instance.CurrentLevelData = null;
            }
        }
        SceneManager.LoadScene("Scenario");
    }
}
