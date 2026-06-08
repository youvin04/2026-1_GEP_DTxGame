using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ResultManager : MonoBehaviour
{
    [Header("결과 패널")]
    public GameObject resultPanel;
    public TextMeshProUGUI levelText;
    public Image[] stars;
    public Sprite starOn;
    public Sprite starOff;
    public TextMeshProUGUI feedbackText;

    [Header("불안도 체크 패널 (통화 후)")]
    public GameObject anxietyPostPanel;
    public GameObject[] postCheckImages;
    public TextMeshProUGUI scoreText;
    public Button postConfirmButton;

    [Header("엔딩 패널")]
    public GameObject endingPanel;
    public Image endingImage;
    public TextMeshProUGUI nameText;
    public Sprite[] endingSprites;

    [Header("타이핑 효과")]
    public TypewriterEffect typewriter;

    [Header("엔딩 대사")]
    [TextArea(2, 4)]
    public string[] endingDialogues;

    [Header("엔딩 화자 이름")]
    public string[] endingNames;

    private int currentEndingIndex = 0;
    private bool _endingTapEnabled = false;
    private int postAnxiety = -1;

    void Start()
    {
        if (resultPanel == null)
        {
            Debug.LogError("ResultPanel 연결 안됨!");
            return;
        }

        resultPanel.SetActive(true);
        anxietyPostPanel.SetActive(false);
        endingPanel.SetActive(false);

        // EndingPanel 탭 → 다음 장면으로
        var endingBtn = endingPanel.GetComponent<Button>();
        if (endingBtn == null)
            endingBtn = endingPanel.AddComponent<Button>();
        endingBtn.onClick.AddListener(OnEndingTap);
        endingBtn.transition = Selectable.Transition.None;

        ShowResult();
    }

    // ── 결과 화면 ─────────────────────

    void ShowResult()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int level = gm.CurrentLevel;
        var data = gm.CurrentLevelData;

        if (levelText && data != null)
            levelText.text = data.levelTitle;

        ShowStars(0);
    }

    // ── ResultPanel 탭 → 불안도 체크 ──

    public void OnResultTap()
    {
        Debug.Log("OnResultTap 호출됨!");
        resultPanel.SetActive(false);
        anxietyPostPanel.SetActive(true);
        postAnxiety = -1;
        postConfirmButton.interactable = false;
        ResetCheckImages();
    }

    // ── 불안도 체크 ───────────────────

    void ResetCheckImages()
    {
        foreach (var c in postCheckImages)
            c.SetActive(false);
    }

    public void OnPostAnxietyClicked(int index)
    {
        postAnxiety = index + 1;
        ResetCheckImages();
        postCheckImages[index].SetActive(true);
        postConfirmButton.interactable = true;

        if (GameManager.Instance != null)
        {
            int pre = (int)GameManager.Instance.PreAnxiety[
                GameManager.Instance.CurrentLevel];
            int diff = pre - postAnxiety;
            if (scoreText != null)
                scoreText.text = Mathf.Max(0, diff).ToString();
        }
    }

    public void OnPostConfirm()
    {
        if (postAnxiety < 0) return;

        if (GameManager.Instance != null)
        {
            int level = GameManager.Instance.CurrentLevel;
            GameManager.Instance.PostAnxiety[level] = postAnxiety;

            float pre  = GameManager.Instance.PreAnxiety[level];
            float diff = pre - postAnxiety;
            int starCount = CalculateStars(diff);
            ShowStars(starCount);
            if (feedbackText)
                feedbackText.text = GetFeedbackText(diff, starCount);
        }

        anxietyPostPanel.SetActive(false);
        ShowEnding();
    }

    // ── 별점 ──────────────────────────

    int CalculateStars(float diff)
    {
        if (diff >= 3) return 5;
        if (diff >= 2) return 4;
        if (diff >= 1) return 3;
        if (diff >= 0) return 2;
        return 1;
    }

    void ShowStars(int count)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null) continue;
            stars[i].sprite = (i < count) ? starOn : starOff;
        }
    }

    string GetFeedbackText(float diff, int count)
    {
        if (count == 5) return "완벽해요! 통화가 전혀 무섭지 않죠? 🎉";
        if (count == 4) return "정말 잘 했어요! 많이 나아졌어요 👍";
        if (count == 3) return "해냈어요! 통화를 완주한 것 자체가 대단해요 😊";
        if (count == 2) return "첫 걸음을 내딛었어요. 반복하면 더 나아질 거예요 💪";
        return "도전한 것 자체가 용기 있는 행동이에요 🌱";
    }

    // ── 엔딩 ──────────────────────────

    void ShowEnding()
    {
        endingPanel.SetActive(true);
        currentEndingIndex = 0;
        ShowEndingFrame();
        StartCoroutine(EnableEndingTapNextFrame());
    }

    void ShowEndingFrame()
    {
        if (endingSprites != null &&
            currentEndingIndex < endingSprites.Length)
            endingImage.sprite = endingSprites[currentEndingIndex];

        if (nameText != null &&
            endingNames != null &&
            currentEndingIndex < endingNames.Length)
            nameText.text = endingNames[currentEndingIndex];

        if (typewriter != null &&
            endingDialogues != null &&
            currentEndingIndex < endingDialogues.Length)
            typewriter.ShowText(endingDialogues[currentEndingIndex]);
    }

    IEnumerator EnableEndingTapNextFrame()
    {
        _endingTapEnabled = false;
        yield return null;
        _endingTapEnabled = true;
    }

    public void OnEndingTap()
    {
        if (!_endingTapEnabled) return;

        if (typewriter != null && typewriter.IsTyping)
        {
            typewriter.SkipOrNext();
            return;
        }

        currentEndingIndex++;

        if (currentEndingIndex < endingSprites.Length)
            ShowEndingFrame();
        else
            SceneManager.LoadScene("StartScene");
    }
}