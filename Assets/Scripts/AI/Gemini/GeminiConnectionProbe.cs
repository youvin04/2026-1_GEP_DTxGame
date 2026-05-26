using System;
using System.Threading;
using CallFree.AI.Models;
using UnityEngine;

namespace CallFree.AI.Gemini
{
    public sealed class GeminiConnectionProbe : MonoBehaviour
    {
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool useMockWhenApiKeyMissing = true;
        [SerializeField, TextArea(4, 12)] private string lastResult;

        public string LastResult
        {
            get { return lastResult; }
        }

        private void Start()
        {
            if (runOnStart)
            {
                RunProbe();
            }
        }

        [ContextMenu("Run Gemini Connection Probe")]
        public async void RunProbe()
        {
            try
            {
                ApiConfig config = GeminiApiConfigLoader.Load();
                IGeminiJudgeClient client;
                if (config.HasApiKey)
                {
                    client = new GeminiJudgeClient(config);
                }
                else if (useMockWhenApiKeyMissing)
                {
                    client = new MockGeminiJudgeClient();
                }
                else
                {
                    throw new InvalidOperationException("Gemini API key is missing.");
                }

                var question = new AiQuestionProfile
                {
                    questionId = "connection.probe",
                    questionText = "연결 테스트입니다. 사용자가 치킨 주문 대본을 말했나요?",
                    rubricText = "후라이드와 한 마리를 포함하면 통과입니다.",
                    requiredCriteria = new System.Collections.Generic.List<string> { "후라이드", "한 마리" },
                    partialCreditHint = "둘 중 하나만 있으면 partial.",
                    retryHint = "전사가 비어 있으면 retry."
                };

                AnswerJudgement judgement = await client.JudgeTranscriptAsync(
                    question,
                    "Gemini generateContent structured output 연결 확인입니다.",
                    "후라이드 한 마리 배달해 주세요.",
                    CancellationToken.None);

                lastResult = AnswerJudgementParser.ToJson(judgement);
                Debug.Log("[GeminiConnectionProbe] " + lastResult);
            }
            catch (Exception ex)
            {
                lastResult = ex.Message;
                Debug.LogError("[GeminiConnectionProbe] " + ex);
            }
        }
    }
}
