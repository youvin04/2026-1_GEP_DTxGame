using System;
using System.Collections.Generic;

namespace CallFree.AI.Models
{
    [Serializable]
    public sealed class AnswerJudgement
    {
        public string transcript = string.Empty;
        public bool isAppropriate;
        public bool isCorrect;
        public float score;
        public string reason = string.Empty;
        public List<string> matchedCriteria = new List<string>();
        public List<string> missingCriteria = new List<string>();
        public string nextState = JudgementStates.Retry;
        public float confidence;
    }

    public static class JudgementStates
    {
        public const string Pass = "pass";
        public const string Partial = "partial";
        public const string Fail = "fail";
        public const string Retry = "retry";

        public static bool IsValid(string value)
        {
            return value == Pass || value == Partial || value == Fail || value == Retry;
        }
    }
}
