using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using CallFree.AI.Gemini;
using CallFree.AI.Models;

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
    private bool _judgementFinished = false;
    private int postAnxiety = -1;
    private string[] activeEndingDialogues;

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
        LoadLevelData();

        // EndingPanel 탭 → 다음 장면으로
        var endingBtn = endingPanel.GetComponent<Button>();
        if (endingBtn == null)
            endingBtn = endingPanel.AddComponent<Button>();
        endingBtn.onClick.AddListener(OnEndingTap);
        endingBtn.transition = Selectable.Transition.None;

        ShowResult();
        _ = RunPostCallJudgementAsync();
    }

    void LoadLevelData()
    {
        var gm = GameManager.Instance;
        var data = gm != null ? gm.CurrentLevelData : null;
        activeEndingDialogues = data != null &&
            data.endingDialogues != null &&
            data.endingDialogues.Length > 0
                ? data.endingDialogues
                : endingDialogues;
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
        postConfirmButton.interactable = _judgementFinished;

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
        if (!_judgementFinished) return;

        if (GameManager.Instance != null)
        {
            int level = GameManager.Instance.CurrentLevel;
            float pre  = GameManager.Instance.PreAnxiety[level];
            if (IsMissionPassed(GameManager.Instance))
            {
                GameManager.Instance.CompleteLevel(level, pre, postAnxiety);
            }
            else
            {
                GameManager.Instance.PostAnxiety[level] = postAnxiety;
            }

            float diff = pre - postAnxiety;
            int starCount = CalculateStars(diff);
            ShowStars(starCount);
            if (feedbackText)
                feedbackText.text = BuildFeedbackText(diff, starCount, GameManager.Instance);
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

    string BuildFeedbackText(float diff, int count, GameManager gm)
    {
        string text = IsMissionPassed(gm)
            ? GetFeedbackText(diff, count)
            : "통화는 종료됐지만 미션 완료 조건은 아직 확인되지 않았어요.";

        if (gm != null && gm.LastJudgementAvailable && gm.LastJudgement != null &&
            !string.IsNullOrWhiteSpace(gm.LastJudgement.reason))
        {
            text += "\n" + gm.LastJudgement.reason;
        }

        return text;
    }

    async System.Threading.Tasks.Task RunPostCallJudgementAsync()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            return;
        }

        if (IsMissionPassed(gm))
        {
            FinishJudgement();
            return;
        }

        gm.LastJudgement = null;
        gm.LastJudgementAvailable = false;
        _judgementFinished = false;

        LevelData data = gm.CurrentLevelData;
        if (data == null)
        {
            Debug.LogWarning("[ResultManager] Cannot judge call because CurrentLevelData is missing.");
            FinishJudgement();
            return;
        }

        string transcript = !string.IsNullOrWhiteSpace(gm.LastPlayerTranscript)
            ? gm.LastPlayerTranscript
            : string.Empty;
        if (string.IsNullOrWhiteSpace(transcript))
        {
            Debug.LogWarning("[ResultManager] Cannot judge call because player transcript is empty.");
            SetConservativeRetryJudgement(gm, "플레이어 발화 전사가 없어 미션 완료로 처리하지 않았습니다.");
            FinishJudgement();
            return;
        }

        try
        {
            ApiConfig config = GeminiApiConfigLoader.Load() ?? new ApiConfig();
            IGeminiJudgeClient judgeClient = config.HasApiKey
                ? new GeminiJudgeClient(config)
                : new MockGeminiJudgeClient();

            AiQuestionProfile question = BuildJudgeQuestion(data);
            string sceneContext = BuildSceneContext(data);
            AnswerJudgement judgement = await judgeClient.JudgeTranscriptAsync(
                question,
                sceneContext,
                transcript);

            gm.LastJudgement = judgement;
            gm.LastJudgementAvailable = true;
            Debug.Log("[ResultManager] Call judgement: " + AnswerJudgementParser.ToJson(judgement));
            FinishJudgement();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ResultManager] Remote call judgement failed, falling back to local judgement: " + ex.Message);
            await RunFallbackJudgementAsync(gm, data, transcript);
            FinishJudgement();
        }
    }

    async System.Threading.Tasks.Task RunFallbackJudgementAsync(GameManager gm, LevelData data, string transcript)
    {
        try
        {
            await System.Threading.Tasks.Task.Yield();
            SetConservativeRetryJudgement(gm, "원격 판정 실패로 미션 완료를 보류했습니다. 발화 내용은 저장됐습니다.");
        }
        catch (Exception fallbackEx)
        {
            Debug.LogWarning("[ResultManager] Fallback call judgement failed: " + fallbackEx.Message);
        }
    }

    void SetConservativeRetryJudgement(GameManager gm, string reason)
    {
        var judgement = new AnswerJudgement
        {
            transcript = gm != null ? (gm.LastPlayerTranscript ?? string.Empty) : string.Empty,
            isAppropriate = false,
            isCorrect = false,
            score = 0f,
            reason = reason,
            nextState = JudgementStates.Retry,
            confidence = 1f
        };

        AnswerJudgementParser.Normalize(judgement);
        if (gm != null)
        {
            gm.LastJudgement = judgement;
            gm.LastJudgementAvailable = true;
        }

        Debug.Log("[ResultManager] Conservative call judgement: " + AnswerJudgementParser.ToJson(judgement));
    }

    bool IsMissionPassed(GameManager gm)
    {
        return gm != null &&
            gm.LastJudgementAvailable &&
            gm.LastJudgement != null &&
            gm.LastJudgement.nextState == JudgementStates.Pass &&
            gm.LastJudgement.isCorrect;
    }

    void FinishJudgement()
    {
        _judgementFinished = true;
        if (postConfirmButton != null && postAnxiety >= 0)
        {
            postConfirmButton.interactable = true;
        }
    }

    AiQuestionProfile BuildJudgeQuestion(LevelData data)
    {
        return new AiQuestionProfile
        {
            questionId = "callfree.level" + data.levelIndex,
            questionText = string.IsNullOrWhiteSpace(data.judgeQuestionText)
                ? "이 레벨의 통화 미션을 완료했는가?"
                : data.judgeQuestionText,
            rubricText = string.IsNullOrWhiteSpace(data.judgeRubricText)
                ? "레벨 미션 조건을 충족했으면 pass."
                : data.judgeRubricText,
            requiredCriteria = ToList(data.judgeRequiredCriteria),
            partialCreditHint = data.judgePartialCreditHint,
            retryHint = data.judgeRetryHint
        };
    }

    string BuildSceneContext(LevelData data)
    {
        return "Level: " + data.levelTitle + "\n"
            + "Training: " + data.trainingType + "\n"
            + "Mission: " + data.missionDescription + "\n"
            + "Call situation: " + data.callDescription + "\n"
            + "Transcript contains only the player's transcribed speech when available.";
    }

    List<string> ToList(string[] values)
    {
        var result = new List<string>();
        if (values == null)
        {
            return result;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                result.Add(values[i]);
            }
        }

        return result;
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
            activeEndingDialogues != null &&
            currentEndingIndex < activeEndingDialogues.Length)
            typewriter.ShowText(activeEndingDialogues[currentEndingIndex]);
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

        if (currentEndingIndex < GetEndingFrameCount())
            ShowEndingFrame();
        else
            SceneManager.LoadScene("StartScene");
    }

    int GetEndingFrameCount()
    {
        int dialogueCount = activeEndingDialogues == null ? 0 : activeEndingDialogues.Length;
        int spriteCount = endingSprites == null ? 0 : endingSprites.Length;
        return Mathf.Max(1, dialogueCount, spriteCount);
    }
}
