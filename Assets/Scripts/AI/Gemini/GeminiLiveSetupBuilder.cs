using System;
using System.Text;
using CallFree.AI.Models;
using CallFree.AI.Prompting;

namespace CallFree.AI.Gemini
{
    public static class GeminiLiveSetupBuilder
    {
        public static string BuildSetupMessage(ApiConfig config, AiNpcProfile npc)
        {
            string model = ToModelResourceName(string.IsNullOrWhiteSpace(config.liveModel)
                ? "gemini-2.5-flash-native-audio-preview-12-2025"
                : config.liveModel);
            string instruction = NpcSystemInstructionBuilder.Build(npc);

            return "{"
                + "\"setup\":{"
                + "\"model\":\"" + JsonEscape(model) + "\","
                + "\"generationConfig\":{"
                + "\"responseModalities\":[\"AUDIO\"],"
                + "\"speechConfig\":{"
                + "\"voiceConfig\":{"
                + "\"prebuiltVoiceConfig\":{"
                + "\"voiceName\":\"" + JsonEscape(GetVoiceName(config, npc)) + "\""
                + "}"
                + "}"
                + "}"
                + "},"
                + "\"systemInstruction\":{\"parts\":[{\"text\":\"" + JsonEscape(instruction) + "\"}]}," 
                + "\"inputAudioTranscription\":{},"
                + "\"outputAudioTranscription\":{}"
                + "}"
                + "}";
        }

        private static string GetVoiceName(ApiConfig config, AiNpcProfile npc)
        {
            if (npc != null && !string.IsNullOrWhiteSpace(npc.voiceName))
            {
                return npc.voiceName.Trim();
            }

            return config == null || string.IsNullOrWhiteSpace(config.liveVoiceName)
                ? "Charon"
                : config.liveVoiceName.Trim();
        }

        private static string ToModelResourceName(string model)
        {
            return model.StartsWith("models/", StringComparison.Ordinal) ? model : "models/" + model;
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
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
