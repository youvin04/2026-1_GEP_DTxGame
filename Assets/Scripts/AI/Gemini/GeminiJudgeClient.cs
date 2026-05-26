using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CallFree.AI.Models;
using CallFree.AI.Prompting;
using CallFree.AI.Security;
using UnityEngine;
using UnityEngine.Networking;

namespace CallFree.AI.Gemini
{
    public sealed class GeminiJudgeClient : IGeminiJudgeClient
    {
        private readonly ApiConfig config;

        public GeminiJudgeClient(ApiConfig config)
        {
            this.config = config ?? new ApiConfig();
        }

        public async Task<AnswerJudgement> JudgeTranscriptAsync(
            AiQuestionProfile question,
            string sceneContext,
            string transcript,
            CancellationToken cancellationToken = default)
        {
            if (!config.HasApiKey)
            {
                throw new InvalidOperationException("Gemini API key is required. Set config.local.json or GEMINI_API_KEY.");
            }

            string prompt = JudgePromptBuilder.Build(question, sceneContext, transcript);
            string requestJson = BuildRequestJson(prompt);
            string responseJson = await PostJsonAsync(BuildGenerateContentUri(), requestJson, cancellationToken);
            string judgementJson = ExtractText(responseJson);
            return AnswerJudgementParser.ParseAndNormalize(judgementJson);
        }

        public async Task<string> GenerateConnectionProbeAsync(CancellationToken cancellationToken = default)
        {
            var question = new AiQuestionProfile
            {
                questionId = "connection.probe",
                questionText = "연결 테스트입니다. 사용자가 '후라이드 한 마리 배달해 주세요'라고 말했나요?",
                rubricText = "치킨 메뉴와 한 마리 주문 의도가 있으면 pass.",
                requiredCriteria = new System.Collections.Generic.List<string> { "후라이드", "한 마리" },
                partialCreditHint = "둘 중 하나만 있으면 partial.",
                retryHint = "비어 있으면 retry."
            };

            AnswerJudgement judgement = await JudgeTranscriptAsync(
                question,
                "Gemini API 연결 확인용 테스트입니다.",
                "후라이드 한 마리 배달해 주세요.",
                cancellationToken);

            return AnswerJudgementParser.ToJson(judgement);
        }

        private string BuildGenerateContentUri()
        {
            string baseUrl = (config.generateContentBaseUrl ?? string.Empty).TrimEnd('/');
            string modelName = string.IsNullOrWhiteSpace(config.judgeModel) ? "gemini-2.5-flash" : config.judgeModel.Trim();
            if (modelName.StartsWith("models/", StringComparison.Ordinal))
            {
                modelName = modelName.Substring("models/".Length);
            }

            return baseUrl + "/" + Uri.EscapeDataString(modelName) + ":generateContent";
        }

        private async Task<string> PostJsonAsync(string uri, string requestJson, CancellationToken cancellationToken)
        {
            byte[] body = Encoding.UTF8.GetBytes(requestJson);
            using (var request = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-goog-api-key", config.geminiApiKey);

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        request.Abort();
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    await Task.Yield();
                }

                string responseText = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException(
                        "Gemini judge request failed: " + request.responseCode + " " + request.error +
                        ". API key " + SecretMasker.Mask(config.geminiApiKey) +
                        ". Body: " + responseText);
                }

                return responseText;
            }
        }

        private static string BuildRequestJson(string prompt)
        {
            return "{"
                + "\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + JsonEscape(prompt) + "\"}]}],"
                + "\"generationConfig\":{"
                + "\"responseMimeType\":\"application/json\","
                + "\"responseJsonSchema\":" + AnswerJudgementSchema.Json
                + "}"
                + "}";
        }

        private static string ExtractText(string responseJson)
        {
            GenerateContentResponse response = JsonUtility.FromJson<GenerateContentResponse>(responseJson);
            if (response == null || response.candidates == null || response.candidates.Count == 0)
            {
                throw new InvalidOperationException("Gemini response did not include candidates.");
            }

            var builder = new StringBuilder();
            for (int i = 0; i < response.candidates.Count; i++)
            {
                Candidate candidate = response.candidates[i];
                if (candidate == null || candidate.content == null || candidate.content.parts == null)
                {
                    continue;
                }

                for (int j = 0; j < candidate.content.parts.Count; j++)
                {
                    Part part = candidate.content.parts[j];
                    if (part != null && !string.IsNullOrEmpty(part.text))
                    {
                        builder.Append(part.text);
                    }
                }
            }

            string text = builder.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Gemini response did not contain text JSON.");
            }

            return text;
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

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
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        [Serializable]
        private sealed class GenerateContentResponse
        {
            public System.Collections.Generic.List<Candidate> candidates;
        }

        [Serializable]
        private sealed class Candidate
        {
            public Content content;
        }

        [Serializable]
        private sealed class Content
        {
            public System.Collections.Generic.List<Part> parts;
        }

        [Serializable]
        private sealed class Part
        {
            public string text;
        }
    }
}
