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

    void Start()
    {
        anxietyPrePanel.SetActive(false);
        ShowCutscene();
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
        if (cutsceneSprites == null ||
            currentIndex >= cutsceneSprites.Length) return;

        cutsceneImage.sprite = cutsceneSprites[currentIndex];

        if (typewriter != null &&
            dialogueTexts != null &&
            currentIndex < dialogueTexts.Length)
            typewriter.ShowText(dialogueTexts[currentIndex]);
    }

    public void OnTapScreen()
    {
        if (typewriter != null && typewriter.IsTyping)
        {
            typewriter.SkipOrNext();
            return;
        }

        currentIndex++;
        if (currentIndex < cutsceneSprites.Length)
        {
            UpdateCutsceneImage();
        }
        else
        {
            cutscenePanel.SetActive(false);
            ShowPreAnxiety();
        }
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