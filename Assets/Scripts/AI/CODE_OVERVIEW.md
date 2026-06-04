# Call Free AI Code Overview

이 문서는 `Assets/Scripts/AI` 아래 Gemini 연동 코드의 역할을 정리한 메모입니다.

## 전체 흐름

현재 구현된 AI 흐름은 두 갈래입니다.

1. `generateContent` structured output 채점
   - transcript 텍스트를 Gemini에 보냅니다.
   - Gemini가 정해진 JSON schema에 맞춰 채점 결과를 돌려줍니다.
   - 현재 실제 연결 성공이 확인된 부분입니다.

2. Live API 전화 통화 준비
   - NPC system instruction과 Live API setup JSON을 만들 수 있습니다.
   - 실제 마이크 입력, 음성 chunk 전송, 오디오 재생 큐는 아직 게임에 붙이지 않았습니다.

## 설정/보안

### `.gitignore`

`config.local.json`, `config.local.json.meta`, `*.secret.json`, `ApiConfig.json`이 Git에 올라가지 않게 막습니다.

### `Assets/StreamingAssets/config.example.json`

설정 파일 예시입니다. 실제 키는 여기에 넣지 않고, 같은 형식의 `config.local.json`에 넣습니다.

주요 필드:

- `geminiApiKey`: Google AI Studio에서 발급받은 API key
- `judgeModel`: structured output 채점용 모델
- `liveModel`: Live API 전화 통화용 모델
- `generateContentBaseUrl`: 일반 Gemini REST API endpoint
- `liveWebSocketUrl`: Live API WebSocket endpoint

## Models

### `Models/ApiConfig.cs`

Gemini 설정값을 담습니다.

- API key
- judge 모델명
- Live 모델명
- API endpoint

`PUT_YOUR_GEMINI_API_KEY_HERE` placeholder는 실제 키가 아니므로 `HasApiKey == false`로 처리합니다.

### `Models/AnswerJudgement.cs`

Gemini 채점 결과 구조입니다.

주요 필드:

- `transcript`: 인식/정리된 사용자 답변
- `isAppropriate`: 질문 맥락에 맞는지
- `isCorrect`: 통과로 볼 수 있는지
- `score`: 0~1 점수
- `matchedCriteria`: 만족한 조건
- `missingCriteria`: 빠진 조건
- `nextState`: `pass`, `partial`, `fail`, `retry`

### `Models/AiQuestionProfile.cs`

채점할 질문과 기준을 담습니다.

예:

- 질문: 치킨 주문 대본을 말했나요?
- 기준: `후라이드`, `한 마리`가 포함되어야 함

### `Models/AiNpcProfile.cs`

Live API에서 전화 NPC를 만들 때 쓰는 프로필입니다.

예:

- NPC 이름
- 현재 상황
- 역할 프롬프트
- 알고 있는 정보
- 금지 행동
- 응답 문장 수 제한

## Prompting

### `Prompting/JudgePromptBuilder.cs`

질문, transcript, 채점 기준을 합쳐 Gemini에게 보낼 프롬프트를 만듭니다.

### `Prompting/AnswerJudgementSchema.cs`

Gemini structured output용 JSON schema입니다.

`GeminiJudgeClient`가 이 schema를 `responseJsonSchema`로 보내서 응답 형태를 안정화합니다.

### `Prompting/NpcSystemInstructionBuilder.cs`

`AiNpcProfile`을 Live API system instruction 문자열로 바꿉니다.

포함되는 규칙:

- 항상 한국어로 말하기
- 전화처럼 짧게 답하기
- 사용자를 비난하지 않기
- AI, API, 프롬프트, 모델명 언급 금지

## Gemini

### `Gemini/GeminiApiConfigLoader.cs`

설정을 읽습니다.

읽는 순서:

1. `Application.persistentDataPath/config.local.json`
2. `Assets/StreamingAssets/config.local.json`
3. `GEMINI_API_KEY` 환경변수

### `Gemini/IGeminiJudgeClient.cs`

실제 Gemini 채점 클라이언트와 mock 클라이언트를 같은 방식으로 부르기 위한 인터페이스입니다.

### `Gemini/GeminiJudgeClient.cs`

실제 Gemini `generateContent` API를 호출합니다.

역할:

- `JudgePromptBuilder`로 프롬프트 생성
- `AnswerJudgementSchema`를 포함한 request JSON 생성
- `UnityWebRequest`로 Gemini 호출
- 응답 JSON에서 text 추출
- `AnswerJudgementParser`로 파싱

### `Gemini/MockGeminiJudgeClient.cs`

API key 없이 로컬에서 흐름을 확인하는 mock 채점기입니다.

단순히 transcript에 required criteria가 포함되어 있는지 검사합니다.

### `Gemini/AnswerJudgementParser.cs`

Gemini 응답을 `AnswerJudgement`로 변환하고 보정합니다.

보정 내용:

- score/confidence를 0~1로 제한
- `nextState`가 이상하면 `retry`로 변경
- transcript가 비어 있으면 `retry`로 변경

### `Gemini/GeminiConnectionProbe.cs`

Unity에서 `generateContent` 채점 연결을 테스트하는 컴포넌트입니다.

현재 성공 확인된 테스트:

- transcript: `후라이드 한 마리 배달해 주세요.`
- criteria: `후라이드`, `한 마리`
- 기대 결과: `nextState: pass`

### `Gemini/GeminiLiveSetupBuilder.cs`

Live API WebSocket에 처음 보낼 setup JSON을 만듭니다.

아직 마이크/오디오 통화 전체 구현은 아니고, Live 세션 시작 메시지를 만드는 역할입니다.

### `Gemini/GeminiLiveConnectionProbe.cs`

Live API WebSocket에 연결하고 setup 메시지를 보내 첫 서버 응답을 받는 테스트 컴포넌트입니다.

이 컴포넌트는 “Live API endpoint와 모델 설정이 맞는지”만 확인합니다. 실제 음성 통화는 별도 구현이 필요합니다.

## Live API 테스트 방법

공식 문서 기준 Live API는 WebSocket 기반 stateful API이고, 첫 메시지로 session setup을 보냅니다. endpoint는 `config.local.json`의 `liveWebSocketUrl`을 사용합니다.

테스트 순서:

1. `config.local.json`에 실제 `geminiApiKey`가 들어있는지 확인합니다.
2. `liveModel`은 우선 `gemini-2.5-flash-native-audio-preview-12-2025`를 사용합니다.
3. 빈 GameObject에 `GeminiLiveConnectionProbe`를 붙입니다.
4. Inspector에서 `runOnStart`를 켜거나 컴포넌트 컨텍스트 메뉴 `Run Gemini Live Connection Probe`를 실행합니다.
5. Console에 `setupComplete`류 응답이 오면 Live WebSocket 연결과 setup은 성공입니다.

주의:

- 이 테스트는 마이크 오디오를 보내지 않습니다.
- Live API는 preview라 모델명/제한이 바뀔 수 있습니다.
- 게임 클라이언트에 API key를 직접 넣는 방식은 평가/로컬 테스트용입니다. 실제 배포에서는 ephemeral token 또는 서버 중계 구조가 더 안전합니다.

## 실제 게임에 연결하는 순서

권장 순서:

1. API key 설정 화면 만들기
   - `GeminiApiConfigLoader.Load()`로 현재 설정 확인
   - 키가 없으면 Settings UI에서 입력받기
   - 저장 시 `GeminiApiConfigLoader.SaveToPersistentDataPath(config)` 사용

2. Lv2/Lv4/Lv5 채점에 `GeminiJudgeClient` 붙이기
   - 사용자의 transcript를 만든 뒤 `JudgeTranscriptAsync()` 호출
   - `nextState == pass`면 완료 처리
   - `partial`, `retry`, `fail`은 패널티 없이 재시도 안내

3. Lv1~Lv3 scripted flow 먼저 완성
   - API가 없어도 게임이 돌아가야 합니다.

4. Live API 전화 통화 붙이기
   - WebSocket 연결
   - setup 메시지 전송
   - 마이크 PCM16 16kHz chunk 전송
   - Gemini output PCM16 24kHz chunk 수신
   - AudioSource 재생 큐 구현
   - input/output transcript 저장

5. 통화 종료 후 judge 호출
   - Live transcript 전체를 `GeminiJudgeClient`에 전달
   - JSON 판정으로 미션 성공 여부 결정

## 현재 검증 상태

검증됨:

- API key 로드
- `generateContent` 호출
- structured output JSON 응답
- Unity JSON 파싱
- mock fallback

아직 미구현/미검증:

- Live API 실제 WebSocket 연결
- 마이크 입력 streaming
- Gemini 음성 output 재생
- transcript를 게임 레벨 결과에 연결
- Settings UI
