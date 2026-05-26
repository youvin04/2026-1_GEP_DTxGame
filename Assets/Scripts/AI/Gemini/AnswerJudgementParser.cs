using System;
using CallFree.AI.Models;
using UnityEngine;

namespace CallFree.AI.Gemini
{
    public static class AnswerJudgementParser
    {
        public static AnswerJudgement ParseAndNormalize(string json)
        {
            string objectJson = ExtractJsonObject(json);
            AnswerJudgement judgement = JsonUtility.FromJson<AnswerJudgement>(objectJson);
            if (judgement == null)
            {
                throw new InvalidOperationException("Judge response was empty JSON.");
            }

            Normalize(judgement);
            return judgement;
        }

        public static string ToJson(AnswerJudgement judgement)
        {
            Normalize(judgement);
            return JsonUtility.ToJson(judgement, true);
        }

        public static void Normalize(AnswerJudgement judgement)
        {
            judgement.transcript = (judgement.transcript ?? string.Empty).Trim();
            judgement.reason = Limit((judgement.reason ?? string.Empty).Trim(), 300);
            judgement.score = Mathf.Clamp01(judgement.score);
            judgement.confidence = Mathf.Clamp01(judgement.confidence);

            if (judgement.matchedCriteria == null)
            {
                judgement.matchedCriteria = new System.Collections.Generic.List<string>();
            }

            if (judgement.missingCriteria == null)
            {
                judgement.missingCriteria = new System.Collections.Generic.List<string>();
            }

            if (!JudgementStates.IsValid(judgement.nextState))
            {
                judgement.nextState = JudgementStates.Retry;
            }

            if (string.IsNullOrWhiteSpace(judgement.transcript))
            {
                judgement.nextState = JudgementStates.Retry;
                judgement.isCorrect = false;
                judgement.score = Mathf.Min(judgement.score, 0.25f);
            }
        }

        private static string ExtractJsonObject(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new InvalidOperationException("Judge response was blank.");
            }

            int start = input.IndexOf('{');
            int end = input.LastIndexOf('}');
            if (start < 0 || end < start)
            {
                throw new InvalidOperationException("Judge response did not contain a JSON object.");
            }

            return input.Substring(start, end - start + 1);
        }

        private static string Limit(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
