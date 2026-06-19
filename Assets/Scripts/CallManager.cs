using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CallFree.AI.Gemini;
using CallFree.AI.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class CallManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject incomingPanel;
    public GameObject callActivePanel;

    [Header("Incoming UI")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI callerNameText;
    public TextMeshProUGUI descText;

    [Header("Active Call UI")]
    public TextMeshProUGUI callerNameActiveText;
    public TextMeshProUGUI hintText;
    public GameObject waveformImage;

    [Header("Buttons")]
    public Button endCallButton;
    public Button laterButton;

    [Header("Audio")]
    public AudioSource npcAudioSource;
    public AudioClip ringSFX;
    public AudioClip connectSFX;
    public AudioClip endSFX;

    private const int MicSampleRate = 16000;
    private const int GeminiOutputSampleRate = 24000;
    private const float MicSendIntervalSeconds = 0.1f;
    private const float InitialAudioBufferSeconds = 0.18f;
    private const float AudioQueuePollSeconds = 0.02f;
    private const float AutoEndAfterPassSeconds = 2.4f;

    private static readonly Regex DataRegex = new Regex(
        "\"data\"\\s*:\\s*\"(?<value>[^\"]+)\"",
        RegexOptions.Compiled);

    private static readonly Regex TextRegex = new Regex(
        "\"text\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"",
        RegexOptions.Compiled);

    private static readonly Regex InputTranscriptionRegex = new Regex(
        "\"(?:input(?:Audio)?Transcription|input(?:_audio)?_transcription)\"\\s*:\\s*\\{[^{}]*\"text\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"",
        RegexOptions.Compiled);

    private static readonly Regex OutputTranscriptionRegex = new Regex(
        "\"(?:output(?:Audio)?Transcription|output(?:_audio)?_transcription)\"\\s*:\\s*\\{[^{}]*\"text\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"",
        RegexOptions.Compiled);

    private ApiConfig _config;
    private AiNpcProfile _npcProfile;
    private ClientWebSocket _socket;
    private CancellationTokenSource _cts;
    private AudioClip _micClip;
    private int _lastMicPos;
    private bool _callActive;
    private bool _isMicStreaming;
    private bool _isNpcSpeaking;
    private bool _isEndingCall;
    private bool _completionJudgeRunning;
    private bool _completionJudgePending;
    private bool _completionPassed;
    private string _lastJudgedPlayerTranscript = string.Empty;
    private LevelData _levelData;

    private readonly Queue<float[]> _audioQueue = new Queue<float[]>();
    private readonly StringBuilder _fullTranscript = new StringBuilder();
    private readonly StringBuilder _playerTranscript = new StringBuilder();
    private Coroutine _audioPlaybackCoroutine;

    private void Start()
    {
        if (incomingPanel != null) incomingPanel.SetActive(true);
        if (callActivePanel != null) callActivePanel.SetActive(false);
        if (waveformImage != null) waveformImage.SetActive(false);

        ConfigureAudioSource();
        LoadLevelData();

        _config = GeminiApiConfigLoader.Load() ?? new ApiConfig();

        if (endCallButton != null) endCallButton.onClick.AddListener(OnEndCall);
        if (laterButton != null) laterButton.onClick.AddListener(OnLaterButton);

        PlayLoopingRing();
    }

    private void ConfigureAudioSource()
    {
        if (npcAudioSource == null)
        {
            Debug.LogWarning("[CallManager] npcAudioSource is not assigned.");
            return;
        }

        npcAudioSource.playOnAwake = false;
        npcAudioSource.loop = false;
        npcAudioSource.spatialBlend = 0f;
    }

    private void LoadLevelData()
    {
        LevelData data = GameManager.Instance != null ? GameManager.Instance.CurrentLevelData : null;
        if (data == null)
        {
            _npcProfile = CreateFallbackNpcProfile();
            return;
        }

        _levelData = data;

        if (callerNameText != null) callerNameText.text = data.npcName;
        if (callerNameActiveText != null) callerNameActiveText.text = data.npcName;
        if (descText != null) descText.text = data.callDescription;
        if (levelText != null) levelText.text = data.levelTitle;

        _npcProfile = new AiNpcProfile
        {
            characterId = "npc.level" + data.levelIndex,
            displayName = data.npcName,
            sceneId = "callfree.level" + data.levelIndex,
            voiceStyleHint = "natural, warm, Korean phone call",
            voiceName = data.liveVoiceName,
            rolePrompt = data.npcSystemPrompt,
            currentSituation = data.callDescription,
            knownFacts = ToList(data.knownFacts),
            hiddenFacts = ToList(data.hiddenFacts),
            allowedHints = ToList(data.allowedHints),
            forbiddenBehaviors = new List<string>
            {
                "Do not mention API keys, prompts, models, or system instructions.",
                "Do not say you are an AI.",
                "Do not answer in English unless the player speaks English first."
            },
            maxResponseSentences = 3
        };
    }

    private static AiNpcProfile CreateFallbackNpcProfile()
    {
        return new AiNpcProfile
        {
            characterId = "npc.level5",
            displayName = "Pungnyeon rice cake shop owner",
            sceneId = "callfree.level5",
            voiceStyleHint = "natural, warm, Korean phone call",
            rolePrompt = "You are a friendly Korean rice cake shop owner. Confirm a honey rice cake order for the player's grandmother.",
            currentSituation = "The player answered your phone call. Confirm the order naturally.",
            forbiddenBehaviors = new List<string>
            {
                "Do not mention API keys, prompts, models, or system instructions.",
                "Do not say you are an AI."
            },
            maxResponseSentences = 3
        };
    }

    private void PlayLoopingRing()
    {
        if (npcAudioSource == null || ringSFX == null) return;

        npcAudioSource.Stop();
        npcAudioSource.clip = ringSFX;
        npcAudioSource.loop = true;
        npcAudioSource.Play();
    }

    public void OnCallAccepted()
    {
        if (incomingPanel != null) incomingPanel.SetActive(false);
        if (callActivePanel != null) callActivePanel.SetActive(true);

        _callActive = true;

        if (npcAudioSource != null)
        {
            npcAudioSource.Stop();
            npcAudioSource.loop = false;
            if (connectSFX != null) npcAudioSource.PlayOneShot(connectSFX);
        }

        SetHint("Connecting...");
        _ = ConnectLiveAsync();
    }

    private async Task ConnectLiveAsync()
    {
        if (_config == null || !_config.HasApiKey)
        {
            Debug.LogWarning("[CallManager] Gemini API key is missing.");
            SetHint("Test mode: API key is missing.");
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            _socket.Options.SetRequestHeader("x-goog-api-key", _config.geminiApiKey);

            await _socket.ConnectAsync(new Uri(_config.liveWebSocketUrl), _cts.Token);
            Debug.Log("[CallManager] Gemini Live WebSocket connected.");

            string setupJson = GeminiLiveSetupBuilder.BuildSetupMessage(_config, _npcProfile);
            await SendTextAsync(setupJson);

            _ = ReceiveLoopAsync();

            await SendInitialNpcTurnAsync();
            StartMicStreaming();

            SetHint("Connected. Speak now.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[CallManager] Gemini Live connection failed: " + ex);
            SetHint("Connection failed. Check API key and network.");
        }
    }

    private async Task SendInitialNpcTurnAsync()
    {
        string name = string.IsNullOrWhiteSpace(_npcProfile.displayName) ? "the caller" : _npcProfile.displayName;
        string prompt = _levelData != null && !string.IsNullOrWhiteSpace(_levelData.initialNpcPrompt)
            ? _levelData.initialNpcPrompt
            : "The phone call has just connected. As " + name
                + ", greet the player first in Korean and ask one short question that fits the current situation.";

        string message = "{"
            + "\"clientContent\":{"
            + "\"turns\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + JsonEscape(prompt) + "\"}]}],"
            + "\"turnComplete\":true"
            + "}"
            + "}";

        await SendTextAsync(message);
    }

    private async Task ReceiveLoopAsync()
    {
        byte[] buffer = new byte[64 * 1024];

        while (_socket != null && _socket.State == WebSocketState.Open && _cts != null && !_cts.IsCancellationRequested)
        {
            try
            {
                var builder = new StringBuilder();
                WebSocketReceiveResult result;

                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[CallManager] Gemini Live WebSocket closed by server.");
                        MainThread(BeginRemoteEndCall);
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                HandleServerMessage(builder.ToString());
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError("[CallManager] Receive loop error: " + ex);
                SetHint("Receive error.");
                MainThread(BeginRemoteEndCall);
                return;
            }
        }
    }

    private void HandleServerMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        if (json.Contains("\"setupComplete\""))
        {
            Debug.Log("[CallManager] Gemini Live setup complete.");
        }

        if (json.Contains("\"inlineData\"") || json.Contains("\"data\""))
        {
            foreach (Match match in DataRegex.Matches(json))
            {
                string base64 = match.Groups["value"].Value;
                if (string.IsNullOrWhiteSpace(base64)) continue;
                MainThread(() => EnqueueAudio(base64));
            }
        }

        bool capturedSpecificTranscript = false;
        foreach (Match match in InputTranscriptionRegex.Matches(json))
        {
            string text = Regex.Unescape(match.Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(text)) continue;

            capturedSpecificTranscript = true;
            _playerTranscript.AppendLine(text);
            _fullTranscript.AppendLine("[PLAYER] " + text);
            SetHint(text);
            ScheduleCompletionJudge();
        }

        foreach (Match match in OutputTranscriptionRegex.Matches(json))
        {
            string text = Regex.Unescape(match.Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(text)) continue;

            capturedSpecificTranscript = true;
            _fullTranscript.AppendLine("[NPC] " + text);
            SetHint(text);
        }

        if (capturedSpecificTranscript)
        {
            return;
        }

        foreach (Match match in TextRegex.Matches(json))
        {
            string text = Regex.Unescape(match.Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(text)) continue;

            _fullTranscript.AppendLine("[TEXT] " + text);
            SetHint(text);
        }
    }

    private void StartMicStreaming()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[CallManager] No microphone device found.");
            SetHint("No microphone found.");
            return;
        }

        _micClip = Microphone.Start(null, true, 10, MicSampleRate);
        _lastMicPos = 0;
        _isMicStreaming = true;
        StartCoroutine(MicStreamCoroutine());
    }

    private IEnumerator MicStreamCoroutine()
    {
        yield return new WaitUntil(() => Microphone.GetPosition(null) > 0 || !_isMicStreaming);

        while (_isMicStreaming && _callActive)
        {
            int currentPos = Microphone.GetPosition(null);
            if (_micClip == null || currentPos == _lastMicPos)
            {
                yield return new WaitForSeconds(MicSendIntervalSeconds);
                continue;
            }

            int sampleCount = currentPos > _lastMicPos
                ? currentPos - _lastMicPos
                : _micClip.samples - _lastMicPos + currentPos;

            if (sampleCount > 0)
            {
                float[] samples = new float[sampleCount];
                _micClip.GetData(samples, _lastMicPos);
                _lastMicPos = currentPos;

                string base64Audio = Convert.ToBase64String(FloatToPcm16(samples));
                string audioMessage = "{"
                    + "\"realtimeInput\":{"
                    + "\"audio\":{"
                    + "\"mimeType\":\"audio/pcm;rate=16000\","
                    + "\"data\":\"" + base64Audio + "\""
                    + "}"
                    + "}"
                    + "}";

                _ = SendTextAsync(audioMessage);
            }

            yield return new WaitForSeconds(MicSendIntervalSeconds);
        }
    }

    private static byte[] FloatToPcm16(float[] samples)
    {
        byte[] pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return pcm;
    }

    private void EnqueueAudio(string base64)
    {
        try
        {
            byte[] pcmBytes = Convert.FromBase64String(base64);
            if (pcmBytes.Length < 2) return;

            float[] samples = new float[pcmBytes.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = BitConverter.ToInt16(pcmBytes, i * 2);
                samples[i] = sample / (float)short.MaxValue;
            }

            _audioQueue.Enqueue(samples);
            if (_audioPlaybackCoroutine == null)
            {
                _audioPlaybackCoroutine = StartCoroutine(PlayAudioQueue());
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[CallManager] Failed to parse Gemini audio: " + ex);
        }
    }

    private IEnumerator PlayAudioQueue()
    {
        _isNpcSpeaking = true;
        if (waveformImage != null) waveformImage.SetActive(true);

        yield return new WaitForSeconds(InitialAudioBufferSeconds);

        while (_audioQueue.Count > 0 || _callActive)
        {
            if (_audioQueue.Count == 0)
            {
                yield return new WaitForSeconds(AudioQueuePollSeconds);
                if (_audioQueue.Count == 0)
                {
                    break;
                }
            }

            float[] samples = DrainAudioQueue();
            if (samples.Length == 0)
            {
                continue;
            }

            AudioClip clip = AudioClip.Create("gemini_live_audio", samples.Length, 1, GeminiOutputSampleRate, false);
            clip.SetData(samples, 0);

            if (npcAudioSource != null)
            {
                npcAudioSource.clip = clip;
                npcAudioSource.loop = false;
                npcAudioSource.Play();
                yield return new WaitForSeconds(clip.length);
            }
            else
            {
                yield return null;
            }
        }

        if (waveformImage != null) waveformImage.SetActive(false);
        _isNpcSpeaking = false;
        _audioPlaybackCoroutine = null;
    }

    private float[] DrainAudioQueue()
    {
        int totalSamples = 0;
        foreach (float[] chunk in _audioQueue)
        {
            if (chunk != null)
            {
                totalSamples += chunk.Length;
            }
        }

        if (totalSamples == 0)
        {
            _audioQueue.Clear();
            return new float[0];
        }

        float[] combined = new float[totalSamples];
        int offset = 0;
        while (_audioQueue.Count > 0)
        {
            float[] chunk = _audioQueue.Dequeue();
            if (chunk == null || chunk.Length == 0)
            {
                continue;
            }

            Array.Copy(chunk, 0, combined, offset, chunk.Length);
            offset += chunk.Length;
        }

        return combined;
    }

    private async Task SendTextAsync(string message)
    {
        if (_socket == null || _socket.State != WebSocketState.Open || _cts == null || _cts.IsCancellationRequested)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
    }

    private void ScheduleCompletionJudge()
    {
        if (!ShouldAutoCompleteCurrentLevel() || _completionPassed || _isEndingCall)
        {
            return;
        }

        if (_completionJudgeRunning)
        {
            _completionJudgePending = true;
            return;
        }

        _ = JudgeCompletionAsync();
    }

    private bool ShouldAutoCompleteCurrentLevel()
    {
        return _levelData != null && _levelData.levelIndex >= 0 && _levelData.levelIndex < 4;
    }

    private async Task JudgeCompletionAsync()
    {
        _completionJudgeRunning = true;

        try
        {
            do
            {
                _completionJudgePending = false;

                string transcript = _playerTranscript.ToString();
                if (string.IsNullOrWhiteSpace(transcript) ||
                    transcript == _lastJudgedPlayerTranscript)
                {
                    continue;
                }

                _lastJudgedPlayerTranscript = transcript;

                ApiConfig config = _config ?? GeminiApiConfigLoader.Load() ?? new ApiConfig();
                if (!config.HasApiKey)
                {
                    Debug.Log("[CallManager] Skipping in-call completion judgement because API key is missing.");
                    continue;
                }

                var judgeClient = new GeminiJudgeClient(config);
                AnswerJudgement judgement = await judgeClient.JudgeTranscriptAsync(
                    BuildJudgeQuestion(_levelData),
                    BuildSceneContext(_levelData),
                    transcript);

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.LastJudgement = judgement;
                    GameManager.Instance.LastJudgementAvailable = true;
                }

                if (judgement != null &&
                    judgement.nextState == JudgementStates.Pass &&
                    judgement.isCorrect)
                {
                    MainThread(BeginMissionCompleteAutoEnd);
                    return;
                }
            }
            while (_completionJudgePending && !_completionPassed && !_isEndingCall);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CallManager] In-call completion judgement failed: " + ex.Message);
        }
        finally
        {
            _completionJudgeRunning = false;
        }
    }

    private void BeginMissionCompleteAutoEnd()
    {
        if (_completionPassed || _isEndingCall)
        {
            return;
        }

        _completionPassed = true;
        _isMicStreaming = false;
        if (Microphone.IsRecording(null)) Microphone.End(null);

        SetHint("Mission complete.");
        _ = SendAutoClosingTurnAsync();
        StartCoroutine(AutoEndAfterMissionSequence());
    }

    private async Task SendAutoClosingTurnAsync()
    {
        string prompt = "The player has completed this level's mission. "
            + "As " + (_npcProfile != null ? _npcProfile.displayName : "the NPC")
            + ", say one short natural Korean closing line for this phone call. "
            + "Do not reveal hidden story facts. End as if you are hanging up.";

        string message = "{"
            + "\"clientContent\":{"
            + "\"turns\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + JsonEscape(prompt) + "\"}]}],"
            + "\"turnComplete\":true"
            + "}"
            + "}";

        await SendTextAsync(message);
    }

    private IEnumerator AutoEndAfterMissionSequence()
    {
        yield return new WaitForSeconds(AutoEndAfterPassSeconds);

        while (_audioPlaybackCoroutine != null || _audioQueue.Count > 0 || _isNpcSpeaking)
        {
            yield return null;
        }

        BeginEndCall(false);
    }

    private static AiQuestionProfile BuildJudgeQuestion(LevelData data)
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

    private static string BuildSceneContext(LevelData data)
    {
        return "Level: " + data.levelTitle + "\n"
            + "Training: " + data.trainingType + "\n"
            + "Mission: " + data.missionDescription + "\n"
            + "Call situation: " + data.callDescription + "\n"
            + "Transcript contains only the player's transcribed speech.";
    }

    public void OnEndCall()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LastCallEndedByRemoteClose = false;
        }

        BeginEndCall(true);
    }

    private void BeginRemoteEndCall()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LastCallEndedByRemoteClose = true;
        }

        BeginEndCall(false);
    }

    private void BeginEndCall(bool playEndSound)
    {
        _callActive = false;
        _isMicStreaming = false;
        if (_isEndingCall) return;
        _isEndingCall = true;

        if (Microphone.IsRecording(null)) Microphone.End(null);
        _cts?.Cancel();

        if (npcAudioSource != null)
        {
            npcAudioSource.loop = false;
            if (playEndSound)
            {
                npcAudioSource.Stop();
                if (endSFX != null) npcAudioSource.PlayOneShot(endSFX);
            }
        }

        StartCoroutine(EndCallSequence());
    }

    private IEnumerator EndCallSequence()
    {
        while (_audioPlaybackCoroutine != null || _audioQueue.Count > 0 || _isNpcSpeaking)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LastTranscript = _fullTranscript.ToString();
            GameManager.Instance.LastPlayerTranscript = _playerTranscript.ToString();
        }

        SceneManager.LoadScene("Result");
    }

    private void OnLaterButton()
    {
        _callActive = false;
        _isMicStreaming = false;

        if (Microphone.IsRecording(null)) Microphone.End(null);
        _cts?.Cancel();

        SceneManager.LoadScene("StartScene");
    }

    private void SetHint(string message)
    {
        MainThread(() =>
        {
            if (hintText != null) hintText.text = message;
        });
    }

    private static string JsonEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var builder = new StringBuilder(value.Length + 16);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private static List<string> ToList(string[] values)
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

    private static void MainThread(Action action)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(action);
    }

    private void OnDestroy()
    {
        _callActive = false;
        _isMicStreaming = false;
        _cts?.Cancel();
        _socket?.Dispose();

        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
        }
    }
}
