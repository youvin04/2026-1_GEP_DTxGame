using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", 
                 menuName = "CallDetective/LevelData")]
public class LevelData : ScriptableObject
{
    [Header("기본 정보")]
    public int levelIndex;
    public string levelTitle;        // "LEVEL.5"
    public string npcName;           // "풍년떡집 사장님"
    public Sprite npcSprite;         // NPC 이미지

    [Header("컷씬 대사 목록")]
    [TextArea(3, 6)]
    public string[] cutsceneDialogues; // 시나리오 대사들

    [Header("통화 설정")]
    [TextArea(3, 6)]
    public string npcSystemPrompt;   // Gemini에게 줄 NPC 역할 프롬프트
    public string callDescription;   // 수신 화면 상황 설명

    [Header("결과")]
    [TextArea(3, 6)]
    public string[] endingDialogues; // 결과 후 엔딩 대사들
}