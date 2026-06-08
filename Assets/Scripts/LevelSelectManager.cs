using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LevelSelectManager : MonoBehaviour
{
    [Header("레벨 데이터 연결")]
    public LevelData[] levels;  // Level_05 드래그

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
            // levels 배열에서 안전하게 접근
            if (levels != null && levels.Length > 0)
                GameManager.Instance.CurrentLevelData = levels[0];
        }
        SceneManager.LoadScene("Scenario");
    }
}