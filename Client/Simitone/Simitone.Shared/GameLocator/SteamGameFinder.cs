using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Simitone.Windows.GameLocator
{
    public static partial class SteamGameLocator
    {
        private static readonly string SteamRegistryPath = Environment.Is64BitOperatingSystem
            ? @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam"
            : @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam";

        private static readonly Lazy<string> SteamInstallPath = new(() =>
        {
            string path = Registry.GetValue(SteamRegistryPath, "InstallPath", null)?.ToString();
            return Directory.Exists(path) ? path : null;
        }, isThreadSafe: true);

        /// <summary>
        /// Gets the install path of a specific Steam game by its Steam App ID.
        /// </summary>
        /// <param name="steamGameId">The Steam App ID of the game.</param>
        /// <exception cref="DirectoryNotFoundException">Thrown if Steam is not installed or the game is not found.</exception>
        /// <returns>The full path to the game's installation directory.</returns>
        public static string GetGamePath(int steamGameId)
        {
            string steamPath = SteamInstallPath.Value;
            if (steamPath is null)
                throw new DirectoryNotFoundException("Steam is not installed.");

            List<string> libraries = GetSteamLibraryPaths(steamPath);

            foreach (string library in libraries)
            {
                string manifestPath = Path.Combine(library, "steamapps", $"appmanifest_{steamGameId}.acf");
                if (!File.Exists(manifestPath)) continue;

                if (TryGetInstallDir(manifestPath, out var installDir))
                {
                    string gamePath = Path.Combine(library, "steamapps", "common", installDir);
                    if (Directory.Exists(gamePath))
                        return (gamePath + "\\").Replace('\\', '/');
                }
            }

            throw new DirectoryNotFoundException($"Steam game with ID {steamGameId} not found.");
        }

        private static List<string> GetSteamLibraryPaths(string steamPath)
        {
            var libraries = new List<string> { steamPath };
            string libraryVdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

            if (!File.Exists(libraryVdfPath))
                return libraries;

            try
            {
                foreach (string line in File.ReadLines(libraryVdfPath))
                {
                    var match = LibraryPathRegex().Match(line);
                    if (!match.Success)
                        continue;

                    string path = match.Groups["path"].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(path))
                        libraries.Add(path);
                }
            }
            catch (IOException) { } //ignore if file is locked or unreadable

            return libraries;
        }

        private static bool TryGetInstallDir(string manifestPath, out string installDir)
        {
            Regex regex = InstallDirRegex();

            try
            {
                foreach (string line in File.ReadLines(manifestPath))
                {
                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        installDir = match.Groups["dir"].Value;
                        return true;
                    }
                }
            }
            catch (IOException) { } //ignore

            installDir = string.Empty;
            return false;
        }

        [GeneratedRegex("\"path\"\\s+\"(?<path>[^\"]+)\"", RegexOptions.Compiled)]
        private static partial Regex LibraryPathRegex();

        [GeneratedRegex("\"installdir\"\\s+\"(?<dir>[^\"]+)\"", RegexOptions.Compiled)]
        private static partial Regex InstallDirRegex();
    }
}
