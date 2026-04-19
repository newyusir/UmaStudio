using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

#if NET8_0_OR_GREATER
using UmamusumeData;
#endif

namespace AssetStudio.GUI
{
#if NET8_0_OR_GREATER
    internal sealed class UmamusumeFileManagerForm : Form
    {
        private readonly MainForm parent;
        private readonly UmamusumeManifestService manifestService;
        private readonly Func<UmamusumeIntegrationSettings> settingsProvider;

        private readonly SortedDictionary<string, ManifestEntry> entries = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, ManifestEntry> searchedEntries;
        private IDictionary<string, ManifestEntry> targetEntries;
        private bool searched;
        private readonly TreeView fileTreeView;
        private readonly TextBox searchTextBox;
        private readonly Button searchButton;
        private readonly Button openButton;
        private readonly Button refreshButton;
        private readonly Label statusLabel;
        private readonly Label unavailableLabel;
        private CancellationTokenSource cts;
        private bool isAvailable;
        private bool isInitializing;
        private bool hasLoaded;
        private string unavailableReason = string.Empty;

        public UmamusumeFileManagerForm(MainForm parent, UmamusumeManifestService manifestService, Func<UmamusumeIntegrationSettings> settingsProvider)
        {
            this.parent = parent;
            this.manifestService = manifestService;
            this.settingsProvider = settingsProvider;

            Text = "Umamusume File Manager";
            StartPosition = FormStartPosition.CenterParent;
            Width = 900;
            Height = 680;

            searchTextBox = new TextBox
            {
                Left = 12,
                Top = 12,
                Width = 680,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            searchButton = new Button
            {
                Left = 702,
                Top = 10,
                Width = 84,
                Text = "Search",
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            searchButton.Click += SearchButton_Click;

            refreshButton = new Button
            {
                Left = 792,
                Top = 10,
                Width = 84,
                Text = "Reload",
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            refreshButton.Click += RefreshButton_Click;

            fileTreeView = new TreeView
            {
                Left = 12,
                Top = 42,
                Width = 864,
                Height = 560,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                PathSeparator = "/"
            };
            fileTreeView.BeforeExpand += FileTreeView_BeforeExpand;
            fileTreeView.NodeMouseDoubleClick += FileTreeView_NodeMouseDoubleClick;

            openButton = new Button
            {
                Left = 12,
                Top = 612,
                Width = 160,
                Height = 28,
                Text = "Open Selected",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            openButton.Click += OpenButton_Click;

            statusLabel = new Label
            {
                Left = 184,
                Top = 617,
                Width = 692,
                Height = 20,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = "Ready"
            };

            unavailableLabel = new Label
            {
                Left = 12,
                Top = 42,
                Width = 864,
                Height = 560,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Visible = false
            };

            Controls.Add(searchTextBox);
            Controls.Add(searchButton);
            Controls.Add(refreshButton);
            Controls.Add(fileTreeView);
            Controls.Add(unavailableLabel);
            Controls.Add(openButton);
            Controls.Add(statusLabel);

            FormClosing += UmamusumeFileManagerForm_FormClosing;
        }

        public void SetAvailability(bool available, string reason)
        {
            isAvailable = available;
            unavailableReason = reason ?? string.Empty;

            if (!available)
            {
                cts?.Cancel();
                unavailableLabel.Text = unavailableReason;
                unavailableLabel.Visible = true;
                fileTreeView.Nodes.Clear();
                statusLabel.Text = unavailableReason;
                SetUiEnabled(false);
                return;
            }

            unavailableLabel.Visible = false;
            if (hasLoaded)
            {
                SetUiEnabled(true);
                statusLabel.Text = $"Loaded {entries.Count} entries.";
                return;
            }

            _ = InitializeEntriesAsync();
        }

        private void UmamusumeFileManagerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        private async void RefreshButton_Click(object sender, EventArgs e)
        {
            if (!isAvailable)
            {
                statusLabel.Text = unavailableReason;
                return;
            }

            manifestService.Reset();
            hasLoaded = false;
            await InitializeEntriesAsync();
        }

        private async Task InitializeEntriesAsync()
        {
            if (!isAvailable || isInitializing)
            {
                return;
            }

            isInitializing = true;
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            SetUiEnabled(false);
            statusLabel.Text = "Initializing manifest...";

            try
            {
                await manifestService.EnsureInitializedAsync(settingsProvider(), (msg, pct) =>
                {
                    if (!IsHandleCreated || IsDisposed)
                    {
                        return;
                    }

                    BeginInvoke(new Action(() =>
                    {
                        statusLabel.Text = $"{msg} ({pct}%)";
                    }));
                });

                entries.Clear();
                foreach (var entry in manifestService.Entries)
                {
                    if (!entries.ContainsKey(entry.Name))
                    {
                        entries.Add(entry.Name, entry);
                    }
                }

                targetEntries = entries;
                searchedEntries = null;
                searched = false;
                searchButton.Text = "Search";
                BuildRootNode();
                hasLoaded = true;
                statusLabel.Text = $"Loaded {entries.Count} entries.";
            }
            catch (Exception ex)
            {
                hasLoaded = false;
                statusLabel.Text = "Failed to initialize.";
                MessageBox.Show(this, $"Failed to initialize Umamusume manifest: {ex.Message}", "Umamusume", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isInitializing = false;
                SetUiEnabled(isAvailable);
            }
        }

        private void BuildRootNode()
        {
            fileTreeView.BeginUpdate();
            fileTreeView.Nodes.Clear();
            var root = new TreeNode("Root");
            root.Nodes.Add(string.Empty);
            fileTreeView.Nodes.Add(root);
            fileTreeView.EndUpdate();
        }

        private void FileTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (targetEntries == null)
            {
                return;
            }

            var expandingNode = e.Node;
            expandingNode.Nodes.Clear();

            IEnumerable<KeyValuePair<string, ManifestEntry>> entryList;
            var convertedNodePath = string.Empty;
            if (expandingNode.Text == "Root")
            {
                entryList = targetEntries;
            }
            else
            {
                convertedNodePath = expandingNode.FullPath["Root/".Length..];
                entryList = targetEntries.Where(ga => ga.Key.StartsWith(convertedNodePath + "/", StringComparison.OrdinalIgnoreCase));
            }

            var files = new SortedDictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entryValue in entryList)
            {
                var entry = entryValue.Value;
                var entryName = expandingNode.Text == "Root" ? entry.Name : entry.Name[(convertedNodePath.Length + 1)..];

                var firstSlashIndex = entryName.IndexOf('/');
                if (firstSlashIndex < 1)
                {
                    files[entry.Name] = entry;
                }
                else
                {
                    var nodeName = entryName[..firstSlashIndex];
                    var nodeKey = string.IsNullOrEmpty(convertedNodePath) ? nodeName : convertedNodePath + '/' + nodeName;
                    if (!expandingNode.Nodes.ContainsKey(nodeKey))
                    {
                        expandingNode.Nodes.Add(nodeKey, nodeName);
                        expandingNode.Nodes[nodeKey]?.Nodes.Add(string.Empty);
                    }
                }
            }

            foreach (var file in files.Values)
            {
                var fileName = file.BaseName;
                if (file.Manifest.StartsWith("manifest", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".manifest";
                }

                if (!expandingNode.Nodes.ContainsKey(file.Name))
                {
                    var node = expandingNode.Nodes.Add(file.Name, fileName);
                    node.Tag = file;
                }
            }
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            if (!searched)
            {
                var keyword = searchTextBox.Text;
                searchedEntries = manifestService
                    .Search(keyword)
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
                targetEntries = searchedEntries;
                searched = true;
                searchButton.Text = "Reset";
            }
            else
            {
                searchedEntries?.Clear();
                searchedEntries = null;
                targetEntries = entries;
                searched = false;
                searchButton.Text = "Search";
            }

            BuildRootNode();
            if (fileTreeView.Nodes.Count > 0)
            {
                fileTreeView.Nodes[0].Expand();
            }
        }

        private async void OpenButton_Click(object sender, EventArgs e)
        {
            await OpenSelectionAsync();
        }

        private async void FileTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            fileTreeView.SelectedNode = e.Node;
            if (e.Node.Tag is ManifestEntry)
            {
                await OpenSelectionAsync();
            }
        }

        private async Task OpenSelectionAsync()
        {
            if (!isAvailable)
            {
                statusLabel.Text = unavailableReason;
                return;
            }

            var selectedNode = fileTreeView.SelectedNode;
            if (selectedNode == null)
            {
                return;
            }

            var selectedEntries = CollectEntriesFromNode(selectedNode).ToList();
            if (selectedEntries.Count == 0)
            {
                MessageBox.Show(this, "No file entries found for the selected node.", "Umamusume", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (selectedEntries.Count > 400)
            {
                var answer = MessageBox.Show(this,
                    $"You are about to resolve {selectedEntries.Count} entries from the selected node. Continue?",
                    "Umamusume",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    return;
                }
            }

            var resolvedEntries = manifestService.ExpandDependencies(selectedEntries);

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            SetUiEnabled(false);
            statusLabel.Text = $"Resolving {resolvedEntries.Count} entries (dependencies included)...";

            try
            {
                var result = await manifestService.ResolveEntriesAsync(
                    resolvedEntries,
                    settingsProvider(),
                    (done, total, name) =>
                    {
                        if (!IsHandleCreated || IsDisposed)
                        {
                            return;
                        }

                        BeginInvoke(new Action(() =>
                        {
                            statusLabel.Text = $"Resolving {done}/{total}: {name}";
                        }));
                    },
                    cts.Token);

                if (result.Paths.Count == 0)
                {
                    MessageBox.Show(this,
                        "No files could be resolved. Check your local path settings or enable standalone fallback.",
                        "Umamusume",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    statusLabel.Text = "No files resolved.";
                    return;
                }

                parent.LoadPaths(result.Paths.ToArray());

                if (result.Missing.Count > 0)
                {
                    var missingPreview = string.Join(Environment.NewLine, result.Missing.Take(10));
                    MessageBox.Show(this,
                        $"Loaded {result.Paths.Count} files, but {result.Missing.Count} entries could not be resolved.\n\n{missingPreview}",
                        "Umamusume",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                statusLabel.Text = $"Loaded {result.Paths.Count} files.";
            }
            catch (OperationCanceledException)
            {
                statusLabel.Text = "Operation canceled.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Resolve failed.";
                MessageBox.Show(this, $"Failed to resolve files: {ex.Message}", "Umamusume", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetUiEnabled(isAvailable);
            }
        }

        private IEnumerable<ManifestEntry> CollectEntriesFromNode(TreeNode node)
        {
            if (targetEntries == null)
            {
                return Enumerable.Empty<ManifestEntry>();
            }

            if (node.Tag is ManifestEntry entry)
            {
                return new[] { entry };
            }

            if (node.FullPath == "Root")
            {
                return targetEntries.Values;
            }

            var convertedNodePath = node.FullPath["Root/".Length..];
            return targetEntries
                .Where(ga => ga.Key.StartsWith(convertedNodePath + '/', StringComparison.OrdinalIgnoreCase))
                .Select(ga => ga.Value);
        }

        private void SetUiEnabled(bool enabled)
        {
            searchTextBox.Enabled = enabled;
            searchButton.Enabled = enabled;
            refreshButton.Enabled = enabled;
            fileTreeView.Enabled = enabled;
            openButton.Enabled = enabled;
            UseWaitCursor = !enabled;
        }
    }
#else
    internal sealed class UmamusumeFileManagerForm : Form
    {
        public UmamusumeFileManagerForm(MainForm parent, UmamusumeManifestService manifestService, Func<UmamusumeIntegrationSettings> settingsProvider)
        {
            Text = "Umamusume File Manager";
            var label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Text = "Umamusume file manager is available only on net8 builds."
            };
            Controls.Add(label);
        }
    }
#endif
}
