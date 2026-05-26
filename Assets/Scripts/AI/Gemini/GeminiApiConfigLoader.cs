using System;
using System.IO;
using CallFree.AI.Models;
using UnityEngine;

namespace CallFree.AI.Gemini
{
    public static class GeminiApiConfigLoader
    {
        private const string LocalConfigFileName = "config.local.json";

        public static string PersistentConfigPath
        {
            get { return Path.Combine(Application.persistentDataPath, LocalConfigFileName); }
        }

        public static string StreamingAssetsConfigPath
        {
            get { return Path.Combine(Application.streamingAssetsPath, LocalConfigFileName); }
        }

        public static ApiConfig Load()
        {
            ApiConfig config = LoadFromFirstExistingPath(PersistentConfigPath, StreamingAssetsConfigPath);
            string envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(config.geminiApiKey) && !string.IsNullOrWhiteSpace(envKey))
            {
                config.geminiApiKey = envKey;
            }

            return config;
        }

        public static void SaveToPersistentDataPath(ApiConfig config)
        {
            string directory = Path.GetDirectoryName(PersistentConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(PersistentConfigPath, JsonUtility.ToJson(config, true));
        }

        private static ApiConfig LoadFromFirstExistingPath(params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    ApiConfig config = JsonUtility.FromJson<ApiConfig>(json);
                    return config ?? new ApiConfig();
                }
            }

            return new ApiConfig();
        }
    }
}
