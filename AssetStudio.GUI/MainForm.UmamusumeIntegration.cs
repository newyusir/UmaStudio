using System;
using System.IO;
using System.Windows.Forms;

namespace AssetStudio.GUI
{
    partial class MainForm
    {
#if NET8_0_OR_GREATER
        private ToolStripSeparator umaFileManagerSeparator;
        private ToolStripMenuItem useUmaLocalPreferredToolStripMenuItem;
        private ToolStripMenuItem useUmaStandaloneOnlyToolStripMenuItem;
        private ToolStripMenuItem configureUmaPathsToolStripMenuItem;
    private TabPage umaFileManagerTabPage;
    private UmamusumeFileManagerForm embeddedUmaFileManagerForm;

        private UmamusumeIntegrationSettings umaIntegrationSettings;
        private readonly UmamusumeManifestService umaManifestService = new();
#endif

        private void InitializeUmaFileManagerIntegration()
        {
#if NET8_0_OR_GREATER
            umaIntegrationSettings = UmamusumeSettingsStore.Load();
            if (string.IsNullOrWhiteSpace(umaIntegrationSettings.StandaloneCachePath))
            {
                umaIntegrationSettings.StandaloneCachePath = UmamusumeSettingsStore.GetDefaultCachePath();
            }

            if (string.IsNullOrWhiteSpace(umaIntegrationSettings.InstallPath))
            {
                umaIntegrationSettings.InstallPath = UmamusumeInstallLocator.DetectInstallPath();
            }

            PersistUmaIntegrationSettings();
            InjectUmaFileManagerMenu();
            UpdateUmaFileManagerMenuState();
                EnsureUmaFileManagerTab();
                RefreshUmaFileManagerAvailability();
                tabControl1.SelectedTab = umaFileManagerTabPage;
#endif
        }

        private void OnGameChangedUmaFileManagerIntegration()
        {
#if NET8_0_OR_GREATER
            UpdateUmaFileManagerMenuState();
        RefreshUmaFileManagerAvailability();
#endif
        }

#if NET8_0_OR_GREATER
        private void InjectUmaFileManagerMenu()
        {
            if (useUmaLocalPreferredToolStripMenuItem != null)
            {
                return;
            }

            useUmaLocalPreferredToolStripMenuItem = new ToolStripMenuItem("Use Local Preferred")
            {
                Name = "useUmaLocalPreferredToolStripMenuItem",
                CheckOnClick = true
            };
            useUmaLocalPreferredToolStripMenuItem.Click += UseUmaLocalPreferredToolStripMenuItem_Click;

            useUmaStandaloneOnlyToolStripMenuItem = new ToolStripMenuItem("Use Standalone Only")
            {
                Name = "useUmaStandaloneOnlyToolStripMenuItem",
                CheckOnClick = true
            };
            useUmaStandaloneOnlyToolStripMenuItem.Click += UseUmaStandaloneOnlyToolStripMenuItem_Click;

            configureUmaPathsToolStripMenuItem = new ToolStripMenuItem("Configure Umamusume Paths...")
            {
                Name = "configureUmaPathsToolStripMenuItem"
            };
            configureUmaPathsToolStripMenuItem.Click += ConfigureUmaPathsToolStripMenuItem_Click;

            umaFileManagerSeparator = new ToolStripSeparator
            {
                Name = "umaFileManagerSeparator"
            };

            var resetIndex = fileToolStripMenuItem.DropDownItems.IndexOf(resetToolStripMenuItem);
            if (resetIndex < 0)
            {
                resetIndex = fileToolStripMenuItem.DropDownItems.Count;
            }

            fileToolStripMenuItem.DropDownItems.Insert(resetIndex++, umaFileManagerSeparator);
            fileToolStripMenuItem.DropDownItems.Insert(resetIndex++, useUmaLocalPreferredToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Insert(resetIndex++, useUmaStandaloneOnlyToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Insert(resetIndex, configureUmaPathsToolStripMenuItem);
        }

        private void UpdateUmaFileManagerMenuState()
        {
            if (useUmaLocalPreferredToolStripMenuItem == null || useUmaStandaloneOnlyToolStripMenuItem == null)
            {
                return;
            }

            bool isUma = Studio.Game.Type == AssetStudio.GameType.UmamusumeJP;
            useUmaLocalPreferredToolStripMenuItem.Enabled = isUma;
            useUmaStandaloneOnlyToolStripMenuItem.Enabled = isUma;

            useUmaLocalPreferredToolStripMenuItem.Checked = umaIntegrationSettings.FileSourceMode == UmaFileSourceMode.LocalPreferred;
            useUmaStandaloneOnlyToolStripMenuItem.Checked = umaIntegrationSettings.FileSourceMode == UmaFileSourceMode.StandaloneOnly;
        }

        private void PersistUmaIntegrationSettings()
        {
            UmamusumeSettingsStore.Save(umaIntegrationSettings);
        }

        private void UseUmaLocalPreferredToolStripMenuItem_Click(object sender, EventArgs e)
        {
            umaIntegrationSettings.FileSourceMode = UmaFileSourceMode.LocalPreferred;
            PersistUmaIntegrationSettings();
            UpdateUmaFileManagerMenuState();
            RefreshUmaFileManagerAvailability();
        }

        private void UseUmaStandaloneOnlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            umaIntegrationSettings.FileSourceMode = UmaFileSourceMode.StandaloneOnly;
            PersistUmaIntegrationSettings();
            UpdateUmaFileManagerMenuState();
            RefreshUmaFileManagerAvailability();
        }

        private void ConfigureUmaPathsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowUmaPathSettingsDialog();
        }

        private bool ShowUmaPathSettingsDialog()
        {
            var detectedPath = UmamusumeInstallLocator.DetectInstallPath();
            using var dialog = new UmamusumePathSettingsForm(umaIntegrationSettings, detectedPath);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }

            umaIntegrationSettings = dialog.Settings;
            if (string.IsNullOrWhiteSpace(umaIntegrationSettings.StandaloneCachePath))
            {
                umaIntegrationSettings.StandaloneCachePath = UmamusumeSettingsStore.GetDefaultCachePath();
            }

            PersistUmaIntegrationSettings();
            UpdateUmaFileManagerMenuState();
            RefreshUmaFileManagerAvailability();
            return true;
        }

        private void EnsureUmaFileManagerTab()
        {
            if (umaFileManagerTabPage == null || umaFileManagerTabPage.IsDisposed)
            {
                umaFileManagerTabPage = new TabPage("File Manager")
                {
                    Name = "tabPageUmaFileManager",
                    UseVisualStyleBackColor = true
                };
                tabControl1.TabPages.Insert(0, umaFileManagerTabPage);
            }

            if (embeddedUmaFileManagerForm == null || embeddedUmaFileManagerForm.IsDisposed)
            {
                embeddedUmaFileManagerForm = new UmamusumeFileManagerForm(this, umaManifestService, () => umaIntegrationSettings)
                {
                    TopLevel = false,
                    FormBorderStyle = FormBorderStyle.None,
                    Dock = DockStyle.Fill
                };

                umaFileManagerTabPage.Controls.Clear();
                umaFileManagerTabPage.Controls.Add(embeddedUmaFileManagerForm);
                embeddedUmaFileManagerForm.Show();
            }
            else if (!ReferenceEquals(embeddedUmaFileManagerForm.Parent, umaFileManagerTabPage))
            {
                embeddedUmaFileManagerForm.Parent = umaFileManagerTabPage;
                embeddedUmaFileManagerForm.Dock = DockStyle.Fill;
                embeddedUmaFileManagerForm.Show();
            }
        }

        private void RefreshUmaFileManagerAvailability()
        {
            if (embeddedUmaFileManagerForm == null || embeddedUmaFileManagerForm.IsDisposed)
            {
                return;
            }

            var isAvailable = EvaluateUmaFileManagerAvailability(out var reason);
            embeddedUmaFileManagerForm.SetAvailability(isAvailable, reason);
        }

        private bool EvaluateUmaFileManagerAvailability(out string reason)
        {
            if (Studio.Game.Type != AssetStudio.GameType.UmamusumeJP)
            {
                reason = "Set Options > Specify Game to UmamusumeJP to use File Manager.";
                return false;
            }

            if (umaIntegrationSettings.FileSourceMode == UmaFileSourceMode.LocalPreferred)
            {
                if (!UmamusumeInstallLocator.TryNormalizeInstallPath(umaIntegrationSettings.InstallPath, out var normalized))
                {
                    normalized = UmamusumeInstallLocator.DetectInstallPath();
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        umaIntegrationSettings.InstallPath = normalized;
                        PersistUmaIntegrationSettings();
                    }
                }

                if (!UmamusumeInstallLocator.TryNormalizeInstallPath(umaIntegrationSettings.InstallPath, out _)
                    && !umaIntegrationSettings.AllowFallbackToStandalone)
                {
                    reason = "Local install path is not configured/detected and standalone fallback is disabled. Use File > Configure Umamusume Paths...";
                    return false;
                }
            }

            var cachePath = umaIntegrationSettings.GetEffectiveCachePath();
            try
            {
                Directory.CreateDirectory(cachePath);
            }
            catch (Exception ex)
            {
                reason = $"Standalone cache path is not writable: {ex.Message}";
                return false;
            }

            reason = "Ready";
            return true;
        }
#endif
    }
}
