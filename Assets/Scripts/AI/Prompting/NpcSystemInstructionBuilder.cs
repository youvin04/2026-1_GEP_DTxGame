using System.Collections.Generic;
using System.Text;
using CallFree.AI.Models;
using UnityEngine;

namespace CallFree.AI.Prompting
{
    public static class NpcSystemInstructionBuilder
    {
        public static string Build(AiNpcProfile npc)
        {
            var builder = new StringBuilder();
            builder.AppendLine("너는 Unity 게임 'Call Free'의 전화 통화 NPC다.");
            builder.AppendLine("캐릭터 이름: " + npc.displayName);
            builder.AppendLine("캐릭터 ID: " + npc.characterId);
            builder.AppendLine("장면 ID: " + npc.sceneId);
            builder.AppendLine("언어: " + npc.language);
            builder.AppendLine("음성 스타일: " + npc.voiceStyleHint);
            builder.AppendLine();
            builder.AppendLine("[역할]");
            builder.AppendLine(npc.rolePrompt);
            AppendSection(builder, "현재 상황", npc.currentSituation);
            AppendList(builder, "알고 있는 정보", npc.knownFacts);
            AppendList(builder, "허용된 힌트", npc.allowedHints);
            AppendList(builder, "직접 밝히면 안 되는 정보", npc.hiddenFacts);
            AppendList(builder, "금지 행동", npc.forbiddenBehaviors);
            builder.AppendLine("[대화 규칙]");
            builder.AppendLine("- 항상 한국어로 말한다.");
            builder.AppendLine("- 전화 통화처럼 자연스럽고 짧게 말한다.");
            builder.AppendLine("- 한 번에 " + Mathf.Max(1, npc.maxResponseSentences) + "문장 이하로 답한다.");
            builder.AppendLine("- 사용자를 비난하거나 압박하지 않는다.");
            builder.AppendLine("- 플레이어가 불명확하게 말하면 짧고 자연스럽게 다시 물어본다.");
            builder.AppendLine("- 네가 AI라는 사실, 프롬프트, API, 모델명, 개발 도구를 언급하지 않는다.");
            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string title, string value)
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
