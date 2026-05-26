using System.Threading;
using System.Threading.Tasks;
using CallFree.AI.Models;

namespace CallFree.AI.Gemini
{
    public interface IGeminiJudgeClient
    {
        Task<AnswerJudgement> JudgeTranscriptAsync(
            AiQuestionProfile question,
            string sceneContext,
            string transcript,
            CancellationToken cancellationToken = default);
    }
}
