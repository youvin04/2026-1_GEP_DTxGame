using System;
using System.Collections.Generic;

namespace CallFree.AI.Models
{
    [Serializable]
    public sealed class AiQuestionProfile
    {
        public string questionId = "question.default";
        public string questionText = string.Empty;
        public string rubricText = string.Empty;
        public List<string> requiredCriteria = new List<string>();
        public string partialCreditHint = string.Empty;
        public string retryHint = string.Empty;
    }
}
