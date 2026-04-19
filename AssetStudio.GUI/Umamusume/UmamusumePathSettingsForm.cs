using System;
using System.IO;
using System.Windows.Forms;

namespace AssetStudio.GUI
{
    internal sealed class UmamusumePathSettingsForm : Form
    {
        private readonly TextBox installPathTextBox;
        private readonly TextBox cachePathTextBox;
        private readonly CheckBox fallbackCheckBox;
        private readonly RadioButton localPreferredRadio;
        private readonly RadioButton standaloneOnlyRadio;

        public UmamusumePathSettingsForm(UmamusumeIntegrationSettings current, string detectedPath)
        {
            Text = "Configure Umamusume Paths";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Width = 680;
            Height = 300;

            var installLabel = new Label
            {
                Text = "Local install path (root containing dat):",
                Left = 12,
                Top = 16,
                Width = 320
            };
            installPathTextBox = new TextBox
            {
                Left = 12,
                Top = 38,
                Width = 520,
                Text = current.InstallPath ?? string.Empty
            };
            var browseInstallButton = new Button
            {
                Text = "Browse...",
                Left = 540,
                Top = 36,
                Width = 110
            };
            browseInstallButton.Click += (_, _) => BrowseFolder(installPathTextBox);

            var detectedLabel = new Label
            {
                Text = string.IsNullOrWhiteSpace(detectedPath)
                    ? "Auto-detected path: (not found)"
                    : $"Auto-detected path: {detectedPath}",
                Left = 12,
                Top = 68,
                Width = 640
            };

            var useDetectedButton = new Button
            {
                Text = "Use detected",
                Left = 540,
                Top = 90,
                Width = 110,
                Enabled = !string.IsNullOrWhiteSpace(detectedPath)
            };
            useDetectedButton.Click += (_, _) =>
            {
                installPathTextBox.Text = detectedPath;
            };

            var cacheLabel = new Label
            {
                Text = "Standalone cache path:",
                Left = 12,
                Top = 125,
                Width = 240
            };
            cachePathTextBox = new TextBox
            {
                Left = 12,
                Top = 146,
                Width = 520,
                Text = string.IsNullOrWhiteSpace(current.StandaloneCachePath)
                    ? UmamusumeSettingsStore.GetDefaultCachePath()
                    : current.StandaloneCachePath
            };
            var browseCacheButton = new Button
            {
                Text = "Browse...",
                Left = 540,
                Top = 144,
                Width = 110
            };
            browseCacheButton.Click += (_, _) => BrowseFolder(cachePathTextBox);

            fallbackCheckBox = new CheckBox
            {
                Left = 12,
                Top = 176,
                Width = 460,
                Text = "Allow fallback to standalone cache when local file is missing",
                Checked = current.AllowFallbackToStandalone
            };

            localPreferredRadio = new RadioButton
            {
                Left = 12,
                Top = 200,
                Width = 180,
                Text = "Use Local Preferred",
                Checked = current.FileSourceMode == UmaFileSourceMode.LocalPreferred
            };
            standaloneOnlyRadio = new RadioButton
            {
                Left = 220,
                Top = 200,
                Width = 180,
                Text = "Use Standalone Only",
                Checked = current.FileSourceMode == UmaFileSourceMode.StandaloneOnly
            };

            var okButton = new Button
            {
                Text = "OK",
                Left = 460,
                Top = 228,
                Width = 90,
                DialogResult = DialogResult.None
            };
            okButton.Click += (_, _) =>
            {
                if (!ValidateInputs())
                {
                    return;
                }

                Settings = new UmamusumeIntegrationSettings
                {
                    InstallPath = installPathTextBox.Text.Trim(),
                    StandaloneCachePath = cachePathTextBox.Text.Trim(),
                    AllowFallbackToStandalone = fallbackCheckBox.Checked,
                    FileSourceMode = standaloneOnlyRadio.Checked ? UmaFileSourceMode.StandaloneOnly : UmaFileSourceMode.LocalPreferred
                };

                DialogResult = DialogResult.OK;
                Close();
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Left = 560,
                Top = 228,
                Width = 90,
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(installLabel);
            Controls.Add(installPathTextBox);
            Controls.Add(browseInstallButton);
            Controls.Add(detectedLabel);
            Controls.Add(useDetectedButton);
            Controls.Add(cacheLabel);
            Controls.Add(cachePathTextBox);
            Controls.Add(browseCacheButton);
            Controls.Add(fallbackCheckBox);
            Controls.Add(localPreferredRadio);
            Controls.Add(standaloneOnlyRadio);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Settings = current;
        }

        public UmamusumeIntegrationSettings Settings { get; private set; }

        private static void BrowseFolder(TextBox target)
        {
            var dialog = new OpenFolderDialog();
            dialog.InitialFolder = target.Text;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                target.Text = dialog.Folder;
            }
        }

        private bool ValidateInputs()
        {
            var cachePath = cachePathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(cachePath))
            {
                MessageBox.Show(this, "Standalone cache path is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            try
            {
                Directory.CreateDirectory(cachePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to create cache path: {ex.Message}", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            var installPath = installPathTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(installPath) && !UmamusumeInstallLocator.TryNormalizeInstallPath(installPath, out _))
            {
                var result = MessageBox.Show(this,
                    "The install path does not look like a valid Umamusume folder (missing dat). Save anyway?",
                    "Validation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
