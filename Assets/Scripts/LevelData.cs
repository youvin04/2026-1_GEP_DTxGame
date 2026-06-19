using UnityEngine;

[CreateAssetMenu(fileName = "LevelData",
                 menuName = "CallDetective/LevelData")]
public class LevelData : ScriptableObject
{
    [Header("Basic Info")]
    public int levelIndex;
    public string levelTitle;
    public string npcName;
    public Sprite npcSprite;

    [Header("Training")]
    [TextArea(2, 4)]
    public string trainingType;
    [TextArea(3, 6)]
    public string missionDescription;
    [TextArea(3, 6)]
    public string missionCardText;

    [Header("Cutscene")]
    [TextArea(3, 6)]
    public string[] cutsceneDialogues;

    [Header("Call")]
    [TextArea(3, 6)]
    public string npcSystemPrompt;
    [TextArea(3, 6)]
    public string callDescription;
    [TextArea(3, 6)]
    public string initialNpcPrompt;
    public string liveVoiceName;

    [Header("NPC Knowledge")]
    [TextArea(2, 4)]
    public string[] knownFacts;
    [TextArea(2, 4)]
    public string[] hiddenFacts;
    [TextArea(2, 4)]
    public string[] allowedHints;

    [Header("Judge")]
    [TextArea(2, 4)]
    public string judgeQuestionText;
    [TextArea(3, 6)]
    public string judgeRubricText;
    [TextArea(2, 4)]
    public string[] judgeRequiredCriteria;
    [TextArea(2, 4)]
    public string judgePartialCreditHint;
    [TextArea(2, 4)]
    public string judgeRetryHint;

    [Header("Result")]
    [TextArea(3, 6)]
    public string[] endingDialogues;
}
