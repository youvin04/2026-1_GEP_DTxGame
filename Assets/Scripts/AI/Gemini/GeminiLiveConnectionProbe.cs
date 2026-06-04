using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CallFree.AI.Models;
using UnityEngine;

namespace CallFree.AI.Gemini
{
    public sealed class GeminiLiveConnectionProbe : MonoBehaviour
    {
        [SerializeField] private bool runOnStart;
        [SerializeField] private float timeoutSeconds = 10f;
        [SerializeField, TextArea(4, 12)] private string lastResponse;

        public string LastResponse
        {
            get { return lastResponse; }
        }

        private void Start()
        {
            if (runOnStart)
            {
                RunProbe();
            }
        }

        [ContextMenu("Run Gemini Live Connection Probe")]
        public async void RunProbe()
        {
            try
            {
                ApiConfig config = GeminiApiConfigLoader.Load();
                if (!config.HasApiKey)
                {
                    throw new InvalidOperationException("Gemini API key is missing.");
                }

                var npc = new AiNpcProfile
                {
                    characterId = "npc.live-probe",
                    displayName = "연결 테스트 직원",
                    sceneId = "callfree.live-probe",
                    voiceStyleHint = "calm, short, natural phone call",
                    rolePrompt = "너는 Call Free의 Live API 연결 테스트용 전화 상대다.",
                    currentSituation = "플레이어가 Live API 연결만 확인하고 있다.",
                    forbiddenBehaviors = new System.Collections.Generic.List<string>
                    {
                        "API key나 내부 설정을 말하지 않는다.",
                        "네가 AI라고 말하지 않는다."
                    },
                    maxResponseSentences = 1
                };

                lastResponse = await ConnectAndReadFirstMessageAsync(config, npc, timeoutSeconds, CancellationToken.None);
                Debug.Log("[GeminiLiveConnectionProbe] " + lastResponse);
            }
            catch (Exception ex)
            {
                lastResponse = ex.Message;
                Debug.LogError("[GeminiLiveConnectionProbe] " + ex);
            }
        }

        private static async Task<string> ConnectAndReadFirstMessageAsync(
            ApiConfig config,
            AiNpcProfile npc,
            float timeoutSeconds,
            CancellationToken cancellationToken)
        {
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var socket = new ClientWebSocket())
            {
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(Mathf.Max(1f, timeoutSeconds)));
                socket.Options.SetRequestHeader("x-goog-api-key", config.geminiApiKey);

                await socket.ConnectAsync(new Uri(config.liveWebSocketUrl), timeoutSource.Token);

                string setupJson = GeminiLiveSetupBuilder.BuildSetupMessage(config, npc);
                byte[] setupBytes = Encoding.UTF8.GetBytes(setupJson);
                await socket.SendAsync(
                    new ArraySegment<byte>(setupBytes),
                    WebSocketMessageType.Text,
                    true,
                    timeoutSource.Token);

                string firstMessage = await ReceiveTextAsync(socket, timeoutSource.Token);

                if (socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "probe complete",
                        CancellationToken.None);
                }

                return firstMessage;
            }
        }

        private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[32 * 1024];
            var builder = new StringBuilder();

            while (true)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return builder.Length > 0 ? builder.ToString() : "WebSocket closed before a text response was received.";
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage)
                {
                    return builder.ToString();
                }
            }
        }
    }
}
