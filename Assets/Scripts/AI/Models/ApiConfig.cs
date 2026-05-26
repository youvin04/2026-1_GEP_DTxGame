using System;

namespace CallFree.AI.Models
{
    [Serializable]
    public sealed class ApiConfig
    {
        public string geminiApiKey = string.Empty;
        public string liveModel = "gemini-2.5-flash-native-audio-preview-12-2025";
        public string judgeModel = "gemini-2.5-flash";
        public string language = "ko-KR";
        public bool saveApiKeyLocally = true;
        public string generateContentBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
        public string liveWebSocketUrl = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";

        public bool HasApiKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(geminiApiKey))
                {
                    return false;
                }

                string trimmed = geminiApiKey.Trim();
                return trimmed != "PUT_YOUR_GEMINI_API_KEY_HERE";
            }
        }
    }
}
