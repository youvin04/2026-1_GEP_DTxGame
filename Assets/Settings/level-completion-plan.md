# Call Free Level Completion Plan

## Goal

Lv.5 중심으로 동작하던 현재 구현을 Lv.1~Lv.5 전체가 같은 데이터 흐름으로 플레이되도록 확장한다. Gemini Live native audio는 모든 레벨의 실시간 통화 NPC에 공통으로 사용하고, 통화 성공 여부는 통화 종료 후 별도 Gemini structured output 판정으로 처리한다.

## Source Materials

- Scenario source: `Assets/Settings/시리어스게임시나리오.md`
- Existing data template: `Assets/Settings/Level_05.asset`
- Runtime data class: `Assets/Scripts/LevelData.cs`
- Live call flow: `Assets/Scripts/CallManager.cs`
- Existing JSON judge client: `Assets/Scripts/AI/Gemini/GeminiJudgeClient.cs`
- Existing judge schema: `Assets/Scripts/AI/Prompting/AnswerJudgementSchema.cs`

## Architecture Decision

Use two separate Gemini responsibilities.

1. Realtime call NPC
   - Use Gemini Live native audio for Lv.1~Lv.5.
   - Each level supplies different NPC identity, situation, known facts, hidden facts, hints, and initial turn prompt.
   - The Live model should focus on natural phone conversation, not final grading.

2. Completion judgement
   - After the call ends, send transcript plus level rubric to `GeminiJudgeClient`.
   - Use `generateContent` with `responseMimeType: application/json` and `responseJsonSchema`.
   - Parse into `AnswerJudgement`.
   - This is more reliable than asking the realtime audio model to speak or emit JSON during the call.

## Implementation Order

### 1. Expand LevelData

Add fields needed for all levels.

- `trainingType`
- `missionDescription`
- `missionCardText`
- `initialNpcPrompt`
- `knownFacts`
- `hiddenFacts`
- `allowedHints`
- `judgeQuestionText`
- `judgeRubricText`
- `judgeRequiredCriteria`
- `judgePartialCreditHint`
- `judgeRetryHint`

Keep existing fields for compatibility.

- `levelIndex`
- `levelTitle`
- `npcName`
- `npcSprite`
- `cutsceneDialogues`
- `npcSystemPrompt`
- `callDescription`
- `endingDialogues`

### 2. Create Level Assets

Create these files in `Assets/Settings`.

- `Level_01.asset`
- `Level_02.asset`
- `Level_03.asset`
- `Level_04.asset`
- keep and update `Level_05.asset`

Use `시리어스게임시나리오.md` as the canonical story source.

Level mapping:

- Lv.1: 택배 기사님, 짧은 긍정 발화, 포획 틀 단서
- Lv.2: 치킨집 아저씨, 대본 기반 주문, 거절 대응
- Lv.3: 쌀집 아저씨/이장님, 빠른 압박 질문 대응
- Lv.4: 짜장면집 아저씨, 주소/메뉴/수량 빈칸 정보 전달
- Lv.5: 풍년떡집 사장님, 꿀떡 주문 조건 자유 대화

### 3. Fix Level Selection

Current issue:

- `LevelSelectManager` stores `levels[0]` regardless of selected index.
- `StartSceneManager` stores only `CurrentLevel`, not `CurrentLevelData`.

Target behavior:

- Selecting Lv.N sets both:
  - `GameManager.Instance.CurrentLevel = levelIndex`
  - `GameManager.Instance.CurrentLevelData = levels[levelIndex]`
- Prefer one owner for level selection logic to avoid duplicate scene responsibilities.

### 4. Make Scenario Use LevelData

Current issue:

- `ScenarioManager` uses Inspector `dialogueTexts`, not `CurrentLevelData.cutsceneDialogues`.

Target behavior:

- On start, if `GameManager.Instance.CurrentLevelData` exists, use `cutsceneDialogues` from the selected level.
- Keep Inspector arrays only as fallback.

### 5. Make Call Use Level-Specific NPC Setup

Current issue:

- `CallManager.SendInitialNpcTurnAsync()` always asks an order-confirmation style question.

Target behavior:

- Use `LevelData.initialNpcPrompt` when present.
- Populate `AiNpcProfile.knownFacts`, `hiddenFacts`, and `allowedHints` from `LevelData`.
- Keep Live API `responseModalities: ["AUDIO"]`.
- Keep `inputAudioTranscription` and `outputAudioTranscription` enabled.

Important:

- Live native audio is appropriate for all levels.
- Do not rely on the Live audio model itself for final JSON grading.

### 6. Improve Transcript Capture

Current issue:

- `CallManager` extracts any `"text"` field with regex and appends it to one transcript buffer.

Target behavior:

- Store enough transcript text for judgement.
- Ideally distinguish player transcript and NPC transcript if the Live response structure allows it.
- Minimum viable version can judge the full mixed transcript, but the prompt should say it may include both player and NPC lines.

### 7. Add Post-Call Judge Step

After `OnEndCall`, before or during Result flow:

1. Build `AiQuestionProfile` from selected `LevelData`.
2. Call `GeminiJudgeClient.JudgeTranscriptAsync(...)`.
3. Store `AnswerJudgement` in `GameManager`.
4. Result screen uses judgement for score/feedback/debug display.

Suggested `GameManager` additions:

- `public AnswerJudgement LastJudgement { get; set; }`
- optional `public bool LastJudgementAvailable { get; set; }`

Failure behavior:

- If judge call fails, still let the player proceed to Result.
- Show fallback feedback and log the exception.

### 8. Level Rubrics

Lv.1 required criteria:

- Player gives a short positive confirmation such as "네, 맞아요" or "제가 받았어요".
- Player asks what the delivered object is, or otherwise completes the delivery confirmation context.

Lv.2 required criteria:

- Player states grandmother house/address context.
- Player orders fried chicken, one chicken, or equivalent.
- Player accepts refusal and ends politely.

Lv.3 required criteria:

- Player responds as if grandmother is away.
- Player confirms sack/rope or preparation without breaking the scene.
- Player handles pressure without derailing the call.

Lv.4 required criteria:

- Player orders jjajangmyeon.
- Player provides address `풍수길 12번지 할머니 댁`.
- Player provides quantity `보통 두 개` or equivalent.

Lv.5 required criteria:

- Player confirms `꿀떡 2되`.
- Player confirms `토요일 오후 2시`.
- Player confirms grandmother name should be written.
- Player completes the call politely.

### 9. Result Flow

Current issue:

- `ResultManager` mostly uses anxiety delta and static ending data.

Target behavior:

- Use selected level title from `CurrentLevelData`.
- Use selected level ending from `CurrentLevelData.endingDialogues`.
- Use `LastJudgement` to decide completion or feedback when available.
- Keep anxiety score as a separate therapy metric.

### 10. Verification Checklist

- Lv.1 button selects `Level_01.asset`.
- Lv.2 button selects `Level_02.asset`.
- Lv.3 button selects `Level_03.asset`.
- Lv.4 button selects `Level_04.asset`.
- Lv.5 button selects `Level_05.asset`.
- Scenario text changes per selected level.
- Incoming call name and description change per selected level.
- First NPC line fits selected level.
- Live audio still connects.
- Transcript is saved after ending a call.
- Judge JSON parses into `AnswerJudgement`.
- Result screen does not crash if judge fails.
- Existing Lv.5 behavior remains playable.

## Recommended Work Phases

### Phase A: Minimum Whole-Game Completion

- [x] Expand `LevelData`.
- [x] Create Lv.1~Lv.4 assets.
- [x] Update Lv.5 asset to the expanded structure.
- [x] Fix level selection.
- [x] Make Scenario/Call/Result read selected `LevelData`.
- [x] Keep current UI shape.

### Phase B: Reliable Judgement

- [x] Add level judge rubric fields.
- [x] Wire `GeminiJudgeClient` during Result flow after call end.
- [x] Store judgement result in `GameManager`.
- [x] Add fallback path for missing API key or judge errors.

### Phase C: Training-Mode Polish

- [ ] Add mission card display.
- [ ] Add Lv.1 fixed utterance hint.
- [ ] Add Lv.2 script UI.
- [ ] Add Lv.3 response card UI.
- [ ] Add Lv.4 blank-fill mission UI.
- [x] Keep Lv.5 as free conversation.
- [ ] Add NPC-driven auto hangup after required mission content is complete.

## Current Validation Status

- [x] `Assets/Settings/Level_01.asset` through `Level_05.asset` exist.
- [x] `StartScene.unity` references Lv.1~Lv.5 assets in order.
- [x] Each level asset includes Live NPC setup fields and judge rubric fields.
- [x] `ScenarioManager`, `CallManager`, and `ResultManager` prefer selected `LevelData`.
- [x] `ResultManager` stores `AnswerJudgement` in `GameManager`.
- [x] `ResultManager` calls `GameManager.CompleteLevel` after post-call anxiety confirmation.
- [x] Lv.1 prompt hides cat/rescue details and only reveals vague capture-equipment information.
- [x] Live voice selection supports a default voice and per-level override; Lv.1 uses `Charon`.
- [x] NPC audio playback buffers and combines short chunks before playback to reduce stutter.
- [x] Server-side Live WebSocket close now auto-ends the call and moves to Result after buffered audio finishes.
- [x] Remote judge failure falls back to local judgement instead of leaving judgement unavailable.
- [x] Remote Live WebSocket close is not treated as success by itself.
- [x] Level completion/unlock only runs when judgement returns `pass` with `isCorrect`.
- [x] Player transcript is stored separately from NPC transcript for judgement.
- [x] Post-call confirmation waits for judgement to finish before completion can be saved.
- [x] Lv.1 result dialogue now shows clue-only text instead of Lv.5-style story resolution.
- [x] Lv.1~Lv.4 run in-call completion judgement from player transcript.
- [x] Lv.1~Lv.4 auto-send a short NPC closing line and end the call after `pass`.
- [x] Manual end-call button remains available for early hang-up.
- [ ] Lv.5 remains excluded from semantic auto hangup because it is the free-conversation final level.
- [ ] Unity Editor compile check. Not run in this environment because `dotnet`, `csc`, `msbuild`, and Unity CLI are not available on PATH.
