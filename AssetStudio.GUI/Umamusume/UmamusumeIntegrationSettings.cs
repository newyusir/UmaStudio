using System;

namespace AssetStudio.GUI
{
    internal enum UmaFileSourceMode
    {
        LocalPreferred = 0,
        StandaloneOnly = 1
    }

    internal sealed class UmamusumeIntegrationSettings
    {
        public UmaFileSourceMode FileSourceMode { get; set; } = UmaFileSourceMode.LocalPreferred;

        public string InstallPath { get; set; } = string.Empty;

        public string StandaloneCachePath { get; set; } = string.Empty;

        public bool AllowFallbackToStandalone { get; set; } = true;

        public string GetEffectiveCachePath()
        {
            if (!string.IsNullOrWhiteSpace(StandaloneCachePath))
            {
                return StandaloneCachePath;
            }

            return UmamusumeSettingsStore.GetDefaultCachePath();
        }
    }
}
