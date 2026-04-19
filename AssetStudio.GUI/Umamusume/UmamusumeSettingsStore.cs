using System;
using System.IO;
using System.Text.Json;

namespace AssetStudio.GUI
{
    internal static class UmamusumeSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static UmamusumeIntegrationSettings Load()
        {
            var settings = new UmamusumeIntegrationSettings();
            var path = GetSettingsFilePath();
            if (!File.Exists(path))
            {
                settings.StandaloneCachePath = GetDefaultCachePath();
                return settings;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<UmamusumeIntegrationSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    settings = loaded;
                }
            }
            catch
            {
                // Fall back to default settings if the file is invalid.
            }

            if (string.IsNullOrWhiteSpace(settings.StandaloneCachePath))
            {
                settings.StandaloneCachePath = GetDefaultCachePath();
            }

            if (!Enum.IsDefined(typeof(UmaFileSourceMode), settings.FileSourceMode))
            {
                settings.FileSourceMode = UmaFileSourceMode.LocalPreferred;
            }

            return settings;
        }

        public static void Save(UmamusumeIntegrationSettings settings)
        {
            var path = GetSettingsFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (string.IsNullOrWhiteSpace(settings.StandaloneCachePath))
            {
                settings.StandaloneCachePath = GetDefaultCachePath();
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(path, json);
        }

        public static string GetSettingsFilePath()
        {
            return Path.Combine(GetConfigDirectory(), "umamusume.integration.settings.json");
        }

        public static string GetDefaultCachePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UmaStudio",
                "UmamusumeCache");
        }

        private static string GetConfigDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UmaStudio");
        }
    }
}
