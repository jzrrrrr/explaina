using System;
using System.IO;
using Microsoft.Win32;
using System.Text;
using System.Configuration; // 添加这个引用

namespace explaina.Services
{
    public class SettingsService
    {
        private const string API_KEY_SETTING = "ApiKey";
        private const string MODEL_NAME_SETTING = "ModelName";
        private const string PROMPT_SUFFIX_SETTING = "PromptSuffix";
        private const string AUTO_START_SETTING = "AutoStart";
        private const string DEFAULT_MODEL = "google/gemini-2.0-flash-exp:free";
        private const string DEFAULT_PROMPT_SUFFIX = "请用中文解释上面的概念。";

        // 替换所有的 Properties.Settings.Default 为自定义实现
        private static readonly string CONFIG_FILE = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Explaina",
            "settings.config");

        static SettingsService()
        {
            // 确保配置目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(CONFIG_FILE));
            
            // 如果配置文件不存在，创建默认配置
            if (!File.Exists(CONFIG_FILE))
            {
                SaveToFile(new Dictionary<string, object>
                {
                    { API_KEY_SETTING, "" },
                    { MODEL_NAME_SETTING, DEFAULT_MODEL },
                    { PROMPT_SUFFIX_SETTING, DEFAULT_PROMPT_SUFFIX },
                    { AUTO_START_SETTING, false }
                });
            }
        }

        private static Dictionary<string, object> LoadFromFile()
        {
            try
            {
                if (File.Exists(CONFIG_FILE))
                {
                    string json = File.ReadAllText(CONFIG_FILE);
                    return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
            return new Dictionary<string, object>();
        }

        private static void SaveToFile(Dictionary<string, object> settings)
        {
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(settings);
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private static object GetSetting(string key, object defaultValue = null)
        {
            var settings = LoadFromFile();
            if (settings.TryGetValue(key, out object value))
                return value;
            return defaultValue;
        }

        private static void SetSetting(string key, object value)
        {
            var settings = LoadFromFile();
            settings[key] = value;
            SaveToFile(settings);
        }

        public static string GetApiKey()
        {
            return GetSetting(API_KEY_SETTING)?.ToString() ?? "";
        }

        public static void SaveApiKey(string apiKey)
        {
            SetSetting(API_KEY_SETTING, apiKey);
        }

        public static string GetModelName()
        {
            return GetSetting(MODEL_NAME_SETTING)?.ToString() ?? DEFAULT_MODEL;
        }

        public static void SaveModelName(string modelName)
        {
            SetSetting(MODEL_NAME_SETTING, string.IsNullOrWhiteSpace(modelName) ? DEFAULT_MODEL : modelName);
        }

        public static string GetPromptSuffix()
        {
            return GetSetting(PROMPT_SUFFIX_SETTING)?.ToString() ?? DEFAULT_PROMPT_SUFFIX;
        }

        public static void SavePromptSuffix(string promptSuffix)
        {
            SetSetting(PROMPT_SUFFIX_SETTING, string.IsNullOrWhiteSpace(promptSuffix) ? DEFAULT_PROMPT_SUFFIX : promptSuffix);
        }

        public static bool GetAutoStart()
        {
            var value = GetSetting(AUTO_START_SETTING);
            if (value is bool boolValue)
                return boolValue;
            if (value is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.True)
                return true;
            return false;
        }

        public static void SaveAutoStart(bool autoStart)
        {
            SetSetting(AUTO_START_SETTING, autoStart);
            SetAutoStart(autoStart);
        }

        private static void SetAutoStart(bool enable)
        {
            string appName = "Explaina";
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            if (enable)
                key.SetValue(appName, appPath);
            else if (key.GetValue(appName) != null)
                key.DeleteValue(appName);

            key.Close();
        }
    }
}