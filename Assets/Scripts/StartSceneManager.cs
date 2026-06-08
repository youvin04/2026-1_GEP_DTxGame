using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class StartSceneManager : MonoBehaviour
{
    [Header("화면 연결")]
    public GameObject titleScreen;
    public GameObject scenarioSelectScreen;

    [Header("레벨 버튼들 (Inspector에서 연결)")]
    public Button[] levelButtons;      // 5개
    public TextMeshProUGUI[] levelLabels;

    void Start()
    {
        ShowTitle();
        RefreshLevelButtons();
    }

    // 타이틀 화면 표시
    public void ShowTitle()
    {
        titleScreen.SetActive(true);
        scenarioSelectScreen.SetActive(false);
    }

    // 레벨 선택 화면 표시
    public void ShowScenarioSelect()
    {
        titleScreen.SetActive(false);
        scenarioSelectScreen.SetActive(true);
        RefreshLevelButtons();
    }

    // 레벨 버튼 잠금/해제 상태 갱신
    void RefreshLevelButtons()
    {
        if (levelButtons == null) return;

        int maxUnlocked = 0;
        if (GameManager.Instance != null)
            maxUnlocked = GameManager.Instance.MaxUnlockedLevel;

        for (int i = 0; i < levelButtons.Length; i++)
        {
            bool unlocked = i <= maxUnlocked;
            levelButtons[i].interactable = unlocked;

            if (levelLabels != null && i < levelLabels.Length)
                levelLabels[i].color = unlocked
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.3f);
        }
    }

    // 레벨 선택 시 호출
    public void OnLevelSelected(int levelIndex)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.CurrentLevel = levelIndex;

        SceneManager.LoadScene("Scenario");
    }

    // 시작하기 버튼 → 레벨 선택 화면으로
    public void OnStartButton()
    {
        ShowScenarioSelect();
    }
}