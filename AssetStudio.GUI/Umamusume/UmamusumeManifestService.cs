using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#if NET8_0_OR_GREATER
using UmamusumeData;
#endif

namespace AssetStudio.GUI
{
#if NET8_0_OR_GREATER
    internal sealed class UmamusumeManifestService
    {
        private readonly object initLock = new();
        private bool initialized;
        private Task initializationTask;
        private List<ManifestEntry> entries = new();
        private Dictionary<string, ManifestEntry> entriesByName = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<ManifestEntry> Entries => entries;

        public void Reset()
        {
            lock (initLock)
            {
                initialized = false;
                initializationTask = null;
                entries = new List<ManifestEntry>();
                entriesByName = new Dictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public async Task EnsureInitializedAsync(UmamusumeIntegrationSettings settings, Action<string, int> progressUpdate = null)
        {
            Task pendingInitialization;
            lock (initLock)
            {
                if (initialized)
                {
                    return;
                }

                if (initializationTask == null)
                {
                    initializationTask = EnsureInitializedCoreAsync(settings, progressUpdate);
                }

                pendingInitialization = initializationTask;
            }

            try
            {
                await pendingInitialization;
            }
            catch
            {
                lock (initLock)
                {
                    if (ReferenceEquals(initializationTask, pendingInitialization))
                    {
                        initializationTask = null;
                    }
                }

                throw;
            }
        }

        private async Task EnsureInitializedCoreAsync(UmamusumeIntegrationSettings settings, Action<string, int> progressUpdate)
        {
            UmaDataHelper.CacheDirectory = settings.GetEffectiveCachePath();
            Directory.CreateDirectory(UmaDataHelper.CacheDirectory);
            UmaDataHelper.OnProgress = progressUpdate;

            await UmaDataHelper.InitializeAsync();
            var loadedEntries = UmaDataHelper.GetManifestEntries();

            var ordered = loadedEntries
                .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.HashName))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var byName = new Dictionary<string, ManifestEntry>(ordered.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in ordered)
            {
                if (!byName.ContainsKey(entry.Name))
                {
                    byName.Add(entry.Name, entry);
                }
            }

            UpdateUmaBundleKeyMap(ordered);

            lock (initLock)
            {
                entries = ordered;
                entriesByName = byName;
                initialized = true;
                initializationTask = null;
            }
        }

        public IEnumerable<ManifestEntry> Search(string keyword, int maxCount = 5000)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return entries.Take(maxCount);
            }

            return entries
                .Where(x => x.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Take(maxCount);
        }

        public List<ManifestEntry> ExpandDependencies(IEnumerable<ManifestEntry> roots)
        {
            var resolved = new List<ManifestEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<ManifestEntry>(roots.Where(x => x != null));

            while (stack.Count > 0)
            {
                var entry = stack.Pop();
                if (entry == null || string.IsNullOrWhiteSpace(entry.HashName))
                {
                    continue;
                }

                if (!seen.Add(entry.HashName))
                {
                    continue;
                }

                resolved.Add(entry);

                if (string.IsNullOrWhiteSpace(entry.Dependencies))
                {
                    continue;
                }

                foreach (var dep in entry.Dependencies.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(dep))
                    {
                        continue;
                    }

                    if (entriesByName.TryGetValue(dep, out var depEntry))
                    {
                        stack.Push(depEntry);
                    }
                }
            }

            return resolved;
        }

        public async Task<ResolveResult> ResolveEntriesAsync(
            IEnumerable<ManifestEntry> requestedEntries,
            UmamusumeIntegrationSettings settings,
            Action<int, int, string> progressUpdate,
            CancellationToken cancellationToken)
        {
            var unique = requestedEntries
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.HashName))
                .DistinctBy(x => x.HashName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var paths = new List<string>(unique.Count);
            var missing = new List<string>();
            int total = unique.Count;
            int done = 0;

            foreach (var entry in unique)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = string.Empty;

                if (settings.FileSourceMode == UmaFileSourceMode.LocalPreferred)
                {
                    path = UmamusumeInstallLocator.ResolveLocalAssetPath(settings.InstallPath, entry.HashName);
                }

                if (string.IsNullOrEmpty(path))
                {
                    if (settings.FileSourceMode == UmaFileSourceMode.StandaloneOnly || settings.AllowFallbackToStandalone)
                    {
                        path = await Task.Run(() => UmaDataHelper.GetPath(entry), cancellationToken);
                    }
                }

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    paths.Add(path);
                }
                else
                {
                    missing.Add(entry.Name);
                }

                done++;
                progressUpdate?.Invoke(done, total, entry.BaseName);
            }

            return new ResolveResult(paths, missing);
        }

        private static void UpdateUmaBundleKeyMap(IEnumerable<ManifestEntry> loadedEntries)
        {
            var keyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in loadedEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.HashName))
                {
                    continue;
                }

                string key = entry.EncryptionKey.ToString(CultureInfo.InvariantCulture);
                keyMap[entry.HashName] = key;

                if (!string.IsNullOrWhiteSpace(entry.Name))
                {
                    var normalized = entry.Name.Replace('\\', '/');
                    keyMap[normalized] = key;
                    var fileName = Path.GetFileName(normalized);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        keyMap[fileName] = key;
                    }
                }
            }

            if (keyMap.Count > 0)
            {
                AssetStudio.UmaJPManager.UpdateBundleKeys(keyMap);
            }
        }
    }

    internal sealed class ResolveResult
    {
        public ResolveResult(IReadOnlyList<string> paths, IReadOnlyList<string> missing)
        {
            Paths = paths;
            Missing = missing;
        }

        public IReadOnlyList<string> Paths { get; }

        public IReadOnlyList<string> Missing { get; }
    }
#else
    internal sealed class UmamusumeManifestService
    {
    }

    internal sealed class ResolveResult
    {
    }
#endif
}
