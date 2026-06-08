using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CallFree.AI.Gemini;
using CallFree.AI.Models;
using CallFree.AI.Prompting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class CallManager : MonoBehaviour
{
    [Header("패널")]
    public GameObject incomingPanel;
    public GameObject callActivePanel;

    [Header("수신 화면 UI")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI callerNameText;
    public TextMeshProUGUI descText;

    [Header("통화 중 UI")]
    public TextMeshProUGUI callerNameActiveText;
    public TextMeshProUGUI hintText;
    public GameObject waveformImage;

    [Header("버튼")]
    public Button endCallButton;
    public Button laterButton;

    [Header("오디오")]
    public AudioSource npcAudioSource;
    public AudioClip ringSFX;
    public AudioClip connectSFX;
    public AudioClip endSFX;

    // 내부 상태
    private ApiConfig _config;
    private AiNpcProfile _npcProfile;
    private ClientWebSocket _socket;
    private CancellationTokenSource _cts;
    private bool _callActive = false;
    private bool _isNpcSpeaking = false;
    private Queue<float[]> _audioQueue = new Queue<float[]>();
    private StringBuilder _fullTranscript = new StringBuilder();

    // 마이크
    private AudioClip _micClip;
    private int _lastMicPos = 0;
    private bool _isMicStreaming = false;
    private const int MIC_SAMPLE_RATE = 16000;

    void Start()
    {
        incomingPanel.SetActive(true);
        callActivePanel.SetActive(false);

        // LevelData 불러오기
        var data = GameManager.Instance?.CurrentLevelData;
        if (data != null)
        {
            if (callerNameText) callerNameText.text = data.npcName;
            if (callerNameActiveText) callerNameActiveText.text = data.npcName;
            if (descText) descText.text = data.callDescription;
            if (levelText) levelText.text = data.levelTitle;

            // NPC 프로필 생성
            _npcProfile = new AiNpcProfile
            {
                characterId = $"npc.level{data.levelIndex}",
                displayName = data.npcName,
                sceneId = $"callfree.level{data.levelIndex}",
                voiceStyleHint = "natural, warm, korean phone call",
                rolePrompt = data.npcSystemPrompt,
                currentSituation = data.callDescription,
                forbiddenBehaviors = new List<string>
                {
                    "API key나 내부 설정을 말하지 않는다.",
                    "네가 AI라고 말하지 않는다.",
                    "영어로 대답하지 않는다."
                },
                maxResponseSentences = 3
            };
        }

        // API 설정 로드
        _config = GeminiApiConfigLoader.Load();
                

        // 아래 코드 추가
        if (_config == null)
        {
            Debug.LogWarning("ApiConfig 없음 - 기본값 사용");
            _config = new ApiConfig();
        }

        // 버튼 연결
        endCallButton.onClick.AddListener(OnEndCall);

        // laterButton null 체크 추가
        if (laterButton != null)
            laterButton.onClick.AddListener(OnLaterButton);

        // 벨소리
        if (npcAudioSource && ringSFX)
        {
            npcAudioSource.clip = ringSFX;
            npcAudioSource.loop = true;
            npcAudioSource.Play();
        }
    }

    // ── 전화 받기 ─────────────────────────

    public void OnCallAccepted()
    {
        incomingPanel.SetActive(false);
        callActivePanel.SetActive(true);
        _callActive = true;

        if (npcAudioSource)
        {
            npcAudioSource.Stop();
            npcAudioSource.loop = false;
            if (connectSFX) npcAudioSource.PlayOneShot(connectSFX);
        }

        if (hintText) hintText.text = "연결 중...";

        // Live WebSocket 연결 시작
        _ = ConnectLiveAsync();
    }

    // ── Live WebSocket 연결 ───────────────

    async Task ConnectLiveAsync()
        {
            if (string.IsNullOrEmpty(_config?.geminiApiKey) || 
            !_config.HasApiKey)
        {
            Debug.LogWarning("API 키 없음 - 통화 건너뜀");
            if (hintText) hintText.text = "🎤 (테스트 모드)";
            return;
        }
        try
        {
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            _socket.Options.SetRequestHeader(
                "x-goog-api-key", _config.geminiApiKey);

            await _socket.ConnectAsync(
                new Uri(_config.liveWebSocketUrl), _cts.Token);

            // Setup 메시지 전송
            string setupJson = GeminiLiveSetupBuilder
                .BuildSetupMessage(_config, _npcProfile);
            await SendTextAsync(setupJson);

            // 수신 루프 시작
            _ = ReceiveLoopAsync();

            // 마이크 스트리밍 시작
            StartMicStreaming();

            if (hintText) hintText.text = "🎤 말해보세요";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CallManager] WebSocket 연결 실패: {ex.Message}");
            if (hintText) hintText.text = "연결 실패. 다시 시도해주세요.";
        }
    }

    // ── 메시지 수신 루프 ──────────────────

    async Task ReceiveLoopAsync()
    {
        byte[] buffer = new byte[64 * 1024];

        while (_socket?.State == WebSocketState.Open 
               && !_cts.IsCancellationRequested)
        {
            try
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult result;

                do
                {
                    result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        return;

                    sb.Append(Encoding.UTF8.GetString(
                        buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                HandleServerMessage(sb.ToString());
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Debug.LogError($"[CallManager] 수신 오류: {ex.Message}");
                break;
            }
        }
    }

    void HandleServerMessage(string json)
    {
        // NPC 음성 데이터 (base64 PCM)
        if (json.Contains("\"inlineData\""))
        {
            // base64 오디오 추출 후 재생 큐에 추가
            MainThread(() => EnqueueAudioFromJson(json));
            return;
        }

        // 텍스트 응답 (transcript)
        if (json.Contains("\"text\""))
        {
            string text = ExtractTextFromJson(json);
            if (!string.IsNullOrEmpty(text))
            {
                _fullTranscript.AppendLine(text);
                MainThread(() => {
                    if (hintText) hintText.text = text;
                });
            }
        }
    }

    // ── 마이크 스트리밍 ───────────────────

    void StartMicStreaming()
    {
        _micClip = Microphone.Start(null, true, 10, MIC_SAMPLE_RATE);
        _lastMicPos = 0;
        _isMicStreaming = true;
        StartCoroutine(MicStreamCoroutine());
    }

    IEnumerator MicStreamCoroutine()
    {
        // 마이크 시작 대기
        yield return new WaitUntil(() => 
            Microphone.GetPosition(null) > 0);

        while (_isMicStreaming && _callActive)
        {
            int currentPos = Microphone.GetPosition(null);
            if (currentPos == _lastMicPos)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            // 새로 들어온 샘플 추출
            int sampleCount = currentPos > _lastMicPos
                ? currentPos - _lastMicPos
                : _micClip.samples - _lastMicPos + currentPos;

            float[] samples = new float[sampleCount];
            _micClip.GetData(samples, _lastMicPos);
            _lastMicPos = currentPos;

            // PCM16으로 변환 후 전송
            byte[] pcm16 = FloatToPcm16(samples);
            string base64Audio = Convert.ToBase64String(pcm16);

            string audioMessage = 
                $"{{\"realtimeInput\":{{\"mediaChunks\":[" +
                $"{{\"mimeType\":\"audio/pcm;rate=16000\"," +
                $"\"data\":\"{base64Audio}\"}}]}}}}";

            _ = SendTextAsync(audioMessage);

            yield return new WaitForSeconds(0.1f);
        }
    }

    byte[] FloatToPcm16(float[] samples)
    {
        byte[] pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short val = (short)(Mathf.Clamp(samples[i], -1f, 1f) 
                * short.MaxValue);
            pcm[i * 2] = (byte)(val & 0xFF);
            pcm[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }
        return pcm;
    }

    // ── NPC 오디오 재생 큐 ─────────────────

    void EnqueueAudioFromJson(string json)
    {
        try
        {
            // "data":"BASE64..." 추출
            int dataStart = json.IndexOf("\"data\":\"") + 8;
            int dataEnd = json.IndexOf("\"", dataStart);
            if (dataStart < 8 || dataEnd < 0) return;

            string base64 = json.Substring(dataStart, dataEnd - dataStart);
            byte[] pcmBytes = Convert.FromBase64String(base64);

            // PCM16 → float[]
            float[] floatSamples = new float[pcmBytes.Length / 2];
            for (int i = 0; i < floatSamples.Length; i++)
            {
                short s = BitConverter.ToInt16(pcmBytes, i * 2);
                floatSamples[i] = s / (float)short.MaxValue;
            }

            _audioQueue.Enqueue(floatSamples);

            if (!_isNpcSpeaking)
                StartCoroutine(PlayAudioQueue());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CallManager] 오디오 파싱 오류: {ex}");
        }
    }

    IEnumerator PlayAudioQueue()
    {
        _isNpcSpeaking = true;
        if (waveformImage) waveformImage.SetActive(true);

        while (_audioQueue.Count > 0)
        {
            float[] samples = _audioQueue.Dequeue();
            AudioClip clip = AudioClip.Create(
                "npc_audio", samples.Length, 1, 24000, false);
            clip.SetData(samples, 0);

            npcAudioSource.clip = clip;
            npcAudioSource.Play();
            yield return new WaitForSeconds(clip.length);
        }

        _isNpcSpeaking = false;
        if (waveformImage) waveformImage.SetActive(false);
    }

    // ── WebSocket 메시지 전송 ─────────────

    async Task SendTextAsync(string message)
    {
        if (_socket?.State != WebSocketState.Open) return;
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text, true, _cts.Token);
    }

    // ── 통화 종료 ─────────────────────────

    public void OnEndCall()
    {
        Debug.Log("OnEndCall 호출됨!");
        _callActive = false;
        _isMicStreaming = false;

        if (Microphone.IsRecording(null))
            Microphone.End(null);

        _cts?.Cancel();
        StopAllCoroutines();

        if (npcAudioSource && endSFX)
            npcAudioSource.PlayOneShot(endSFX);

        // 채점 후 Scenario 씬으로
        StartCoroutine(EndCallSequence());
    }

    IEnumerator EndCallSequence()
    {
        yield return new WaitForSeconds(0.8f);

        // transcript를 GameManager에 저장
        if (GameManager.Instance != null)
            GameManager.Instance.LastTranscript = 
                _fullTranscript.ToString();

        SceneManager.LoadScene("Result");
    }

    void OnLaterButton()
    {
        _callActive = false;
        _isMicStreaming = false;
        if (Microphone.IsRecording(null)) Microphone.End(null);
        _cts?.Cancel();
        SceneManager.LoadScene("StartScene");
    }

    // ── 유틸 ──────────────────────────────

    string ExtractTextFromJson(string json)
    {
        try
        {
            int start = json.IndexOf("\"text\":\"") + 8;
            int end = json.IndexOf("\"", start);
            if (start < 8 || end < 0) return "";
            return json.Substring(start, end - start);
        }
        catch { return ""; }
    }

    void MainThread(Action action)
    {
        // Unity 메인 스레드에서 실행
        UnityMainThreadDispatcher.Instance().Enqueue(action);
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _socket?.Dispose();
        if (Microphone.IsRecording(null)) Microphone.End(null);
    }
}