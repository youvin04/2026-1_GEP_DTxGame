using System;
using System.Threading;
using System.Threading.Tasks;
using CallFree.AI.Models;

namespace CallFree.AI.Gemini
{
    public sealed class MockGeminiJudgeClient : IGeminiJudgeClient
    {
        public Task<AnswerJudgement> JudgeTranscriptAsync(
            AiQuestionProfile question,
            string sceneContext,
            string transcript,
            CancellationToken cancellationToken = default)
        {
            var judgement = new AnswerJudgement
            {
                transcript = (transcript ?? string.Empty).Trim(),
                isAppropriate = !string.IsNullOrWhiteSpace(transcript),
                reason = "로컬 mock 판정입니다.",
                confidence = 0.75f
            };

            if (question.requiredCriteria != null)
            {
                for (int i = 0; i < question.requiredCriteria.Count; i++)
                {
                    string criterion = question.requiredCriteria[i];
                    if (ContainsLoose(transcript, criterion))
                    {
                        judgement.matchedCriteria.Add(criterion);
                    }
                    else
                    {
                        judgement.missingCriteria.Add(criterion);
                    }
                }
            }

            int requiredCount = question.requiredCriteria == null ? 0 : question.requiredCriteria.Count;
            if (string.IsNullOrWhiteSpace(transcript))
            {
                judgement.nextState = JudgementStates.Retry;
            }
            else if (requiredCount == 0)
            {
                judgement.nextState = JudgementStates.Partial;
                judgement.score = 0.5f;
            }
            else if (judgement.missingCriteria.Count == 0)
            {
                judgement.nextState = JudgementStates.Pass;
                judgement.isCorrect = true;
                judgement.score = 1f;
            }
            else if (judgement.matchedCriteria.Count > 0)
            {
                judgement.nextState = JudgementStates.Partial;
                judgement.score = judgement.matchedCriteria.Count / (float)requiredCount;
            }
            else
            {
                judgement.nextState = JudgementStates.Fail;
                judgement.score = 0f;
            }

            AnswerJudgementParser.Normalize(judgement);
            return Task.FromResult(judgement);
        }

        private static bool ContainsLoose(string text, string criterion)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(criterion))
            {
                return false;
            }

            return text.IndexOf(criterion, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
