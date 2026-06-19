using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ScenarioManager : MonoBehaviour
{
    [Header("컷씬 패널")]
    public GameObject cutscenePanel;
    public Image cutsceneImage;
    public Sprite[] cutsceneSprites;

    [Header("컷씬 대사")]
    [TextArea(2, 4)]
    public string[] dialogueTexts;

    [Header("타이핑 효과")]
    public TypewriterEffect typewriter;

    [Header("자가진단 - 통화 전")]
    public GameObject anxietyPrePanel;
    public GameObject[] preCheckImages;
    public Button preConfirmButton;

    private int currentIndex = 0;
    private int preAnxiety = -1;
    private string[] activeDialogues;

    void Start()
    {
        anxietyPrePanel.SetActive(false);
        LoadLevelData();
        ShowCutscene();
    }

    void LoadLevelData()
    {
        var gm = GameManager.Instance;
        var data = gm != null ? gm.CurrentLevelData : null;
        activeDialogues = data != null &&
            data.cutsceneDialogues != null &&
            data.cutsceneDialogues.Length > 0
                ? data.cutsceneDialogues
                : dialogueTexts;
    }

    // ── 컷씬 ──────────────────────────────

    void ShowCutscene()
    {
        cutscenePanel.SetActive(true);
        currentIndex = 0;
        UpdateCutsceneImage();
    }

    void UpdateCutsceneImage()
    {
        if (cutsceneImage != null &&
            cutsceneSprites != null &&
            cutsceneSprites.Length > 0)
        {
            int spriteIndex = Mathf.Min(currentIndex, cutsceneSprites.Length - 1);
            cutsceneImage.sprite = cutsceneSprites[spriteIndex];
        }

        if (typewriter != null &&
            activeDialogues != null &&
            currentIndex < activeDialogues.Length)
            typewriter.ShowText(activeDialogues[currentIndex]);
    }

    public void OnTapScreen()
    {
        if (typewriter != null && typewriter.IsTyping)
        {
            typewriter.SkipOrNext();
            return;
        }

        currentIndex++;
        if (currentIndex < GetCutsceneFrameCount())
        {
            UpdateCutsceneImage();
        }
        else
        {
            cutscenePanel.SetActive(false);
            ShowPreAnxiety();
        }
    }

    int GetCutsceneFrameCount()
    {
        int dialogueCount = activeDialogues == null ? 0 : activeDialogues.Length;
        int spriteCount = cutsceneSprites == null ? 0 : cutsceneSprites.Length;
        return Mathf.Max(1, dialogueCount, spriteCount);
    }

    // ── 자가진단 공통 ──────────────────────

    void ResetCheckImages(GameObject[] checks)
    {
        foreach (var c in checks)
            c.SetActive(false);
    }

    // ── 통화 전 자가진단 ───────────────────

    void ShowPreAnxiety()
    {
        anxietyPrePanel.SetActive(true);
        preAnxiety = -1;
        preConfirmButton.interactable = false;
        ResetCheckImages(preCheckImages);
    }

    public void OnPreAnxietyClicked(int index)
    {
        preAnxiety = index + 1;
        ResetCheckImages(preCheckImages);
        preCheckImages[index].SetActive(true);
        preConfirmButton.interactable = true;
    }

    public void OnPreConfirm()
    {
        if (preAnxiety < 0) return;
        if (GameManager.Instance != null)
            GameManager.Instance.PreAnxiety[
                GameManager.Instance.CurrentLevel] = preAnxiety;

        anxietyPrePanel.SetActive(false);
        SceneManager.LoadScene("Call");
    }
}
