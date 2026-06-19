using System;
using System.Collections.Generic;

namespace CallFree.AI.Models
{
    [Serializable]
    public sealed class AiNpcProfile
    {
        public string characterId = "npc.default";
        public string displayName = "전화 상대";
        public string sceneId = "callfree.default";
        public string voiceStyleHint = "calm, natural phone call";
        public string voiceName = string.Empty;
        public string language = "ko-KR";
        public string rolePrompt = "너는 Unity 게임 'Call Free'의 전화 통화 NPC다.";
        public string currentSituation = string.Empty;
        public List<string> knownFacts = new List<string>();
        public List<string> hiddenFacts = new List<string>();
        public List<string> allowedHints = new List<string>();
        public List<string> forbiddenBehaviors = new List<string>();
        public int maxResponseSentences = 2;
    }
}
