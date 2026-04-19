using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetStudio.GUI
{
    internal static class UmamusumeInstallLocator
    {
        public static string DetectInstallPath()
        {
            var candidates = new List<string>();

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                candidates.Add(Path.Combine(userProfile, "AppData", "LocalLow", "Cygames", "umamusume"));
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                candidates.Add(Path.Combine(localAppData, "Cygames", "umamusume"));
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                candidates.Add(Path.Combine(programFiles, "DMM GAMES", "umamusume"));
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                candidates.Add(Path.Combine(programFilesX86, "DMM GAMES", "umamusume"));
            }

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (TryNormalizeInstallPath(candidate, out var normalized))
                {
                    return normalized;
                }
            }

            return string.Empty;
        }

        public static bool TryNormalizeInstallPath(string path, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!Directory.Exists(fullPath))
                {
                    return false;
                }

                if (IsDatDirectory(fullPath))
                {
                    var parent = Directory.GetParent(fullPath)?.FullName;
                    normalized = string.IsNullOrWhiteSpace(parent) ? fullPath : parent;
                    return true;
                }

                if (Directory.Exists(Path.Combine(fullPath, "dat")))
                {
                    normalized = fullPath;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static string ResolveLocalAssetPath(string installPath, string hashName)
        {
            if (string.IsNullOrWhiteSpace(hashName) || hashName.Length < 2)
            {
                return string.Empty;
            }

            if (!TryNormalizeInstallPath(installPath, out var normalizedRoot))
            {
                return string.Empty;
            }

            var candidates = new[]
            {
                Path.Combine(normalizedRoot, "dat", hashName[..2], hashName),
                Path.Combine(normalizedRoot, hashName[..2], hashName),
                Path.Combine(normalizedRoot, hashName)
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static bool IsDatDirectory(string fullPath)
        {
            var dirName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.Equals(dirName, "dat", StringComparison.OrdinalIgnoreCase);
        }
    }
}
