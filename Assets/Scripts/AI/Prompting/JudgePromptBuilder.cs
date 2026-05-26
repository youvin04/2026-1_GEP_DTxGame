using System.Collections.Generic;
using System.Text;
using CallFree.AI.Models;

namespace CallFree.AI.Prompting
{
    public static class JudgePromptBuilder
    {
        public static string Build(AiQuestionProfile question, string sceneContext, string transcript)
        {
            var builder = new StringBuilder();
            builder.AppendLine("You evaluate a player's spoken answer in the Korean phone training game 'Call Free'.");
            builder.AppendLine("Return only JSON that matches the provided schema.");
            builder.AppendLine();
            builder.AppendLine("[Question]");
            builder.AppendLine(question.questionText);
            builder.AppendLine();
            builder.AppendLine("[Player transcript]");
            builder.AppendLine(string.IsNullOrWhiteSpace(transcript) ? "(empty)" : transcript.Trim());
            builder.AppendLine();
            builder.AppendLine("[Rubric]");
            builder.AppendLine(question.rubricText);
            AppendList(builder, "Required criteria", question.requiredCriteria);
            AppendOptional(builder, "Partial credit hint", question.partialCreditHint);
            AppendOptional(builder, "Retry hint", question.retryHint);
            AppendOptional(builder, "Scene context", sceneContext);
            builder.AppendLine("[Judging rules]");
            builder.AppendLine("- First transcribe or normalize the player's meaning in Korean if needed.");
            builder.AppendLine("- isAppropriate means the answer is relevant to the question and situation.");
            builder.AppendLine("- isCorrect means the core rubric is satisfied.");
            builder.AppendLine("- Use nextState pass, partial, fail, or retry.");
            builder.AppendLine("- Use retry when the transcript is empty, too ambiguous, or probably unusable.");
            builder.AppendLine("- Do not infer facts that are not supported by the transcript.");
            builder.AppendLine("- Do not punish the player; feedback should support retry when needed.");
            return builder.ToString();
        }

        private static void AppendOptional(StringBuilder builder, string title, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            builder.AppendLine("[" + title + "]");
            builder.AppendLine(value.Trim());
            builder.AppendLine();
        }

        private static void AppendList(StringBuilder builder, string title, IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            builder.AppendLine("[" + title + "]");
            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    builder.AppendLine("- " + value.Trim());
                }
            }

            builder.AppendLine();
        }
    }
}
