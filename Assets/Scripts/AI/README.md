# Call Free AI Integration

Unity용 Gemini 연동 코드입니다. `Assets/GeminiTest`의 .NET 콘솔 하네스에서 필요한 부분만 옮기고, Unity 런타임에 맞게 `UnityWebRequest`와 `JsonUtility` 기반으로 다시 작성했습니다.

## Config

실제 API 키는 커밋하지 않습니다.

1. `Assets/StreamingAssets/config.example.json`을 참고합니다.
2. 로컬 테스트용 `config.local.json`을 만들고 `geminiApiKey`를 입력합니다.
3. `config.local.json`은 `.gitignore` 대상입니다.

`GeminiApiConfigLoader`는 다음 순서로 설정을 읽습니다.

1. `Application.persistentDataPath/config.local.json`
2. `Assets/StreamingAssets/config.local.json`
3. `GEMINI_API_KEY` 환경변수

## Connection Probe

빈 GameObject에 `GeminiConnectionProbe`를 붙인 뒤 Inspector 컨텍스트 메뉴에서 `Run Gemini Connection Probe`를 실행합니다.

- API key가 있으면 Gemini `generateContent` structured output 호출을 합니다.
- API key가 없고 `useMockWhenApiKeyMissing`가 켜져 있으면 로컬 mock 판정만 실행합니다.

## Kept From GeminiTest

- API config 모델
- key masking
- judge prompt builder
- judgement JSON schema
- judgement parser/normalizer
- mock judge client
- Live API setup message builder

`GeminiTest`의 `bin`, `obj`, `tools`, `.csproj`, 콘솔 하네스 파일은 Unity 런타임에 필요하지 않습니다.
