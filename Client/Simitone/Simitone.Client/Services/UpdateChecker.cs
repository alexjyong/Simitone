/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using FSO.Common.Utils;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Model;
using FSO.HIT;
using Simitone.Client.UI.Panels;

namespace Simitone.Client.Services
{
    /// <summary>
    /// Represents a GitHub release from the API response.
    /// </summary>
    public class GitHubRelease
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }
    }

    /// <summary>
    /// Checks for updates from GitHub releases on startup.
    /// </summary>
    public static class UpdateChecker
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static bool _hasChecked = false;

        static UpdateChecker()
        {
            // GitHub API requires a User-Agent header
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Simitone-UpdateChecker");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Checks for updates asynchronously. Safe to call multiple times - only runs once.
        /// </summary>
        public static void CheckForUpdatesAsync()
        {
            if (_hasChecked) return;
            _hasChecked = true;

            Task.Run(async () =>
            {
                try
                {
                    var response = await _httpClient.GetStringAsync(SimitoneVersion.GitHubReleasesApiUrl);
                    var release = JsonConvert.DeserializeObject<GitHubRelease>(response);

                    if (release != null && IsNewerVersion(release.TagName))
                    {
                        // Marshal back to game thread to show dialog
                        GameThread.NextUpdate(_ => ShowUpdateDialog(release));
                    }
                }
                catch (Exception)
                {
                    // Silently fail - don't interrupt game if update check fails
                    // This handles: no internet, API errors, JSON parse errors, etc.
                }
            });
        }

        /// <summary>
        /// Compares version strings to determine if the remote version is newer.
        /// Handles version formats like "v0.8.19", "0.8.19-forked", etc.
        /// </summary>
        private static bool IsNewerVersion(string tagName)
        {
            // Normalize version strings: strip 'v' prefix and any suffix after hyphen for comparison
            var latestVersion = NormalizeVersion(tagName);
            var currentVersion = NormalizeVersion(SimitoneVersion.Current);

            if (Version.TryParse(latestVersion, out var latest) &&
                Version.TryParse(currentVersion, out var current))
            {
                return latest > current;
            }

            // Fallback: string comparison if parsing fails
            return string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }

        /// <summary>
        /// Normalizes a version string by removing 'v' prefix and extracting the numeric part.
        /// "v0.8.19-forked" -> "0.8.19"
        /// </summary>
        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return "0.0.0";

            // Remove 'v' or 'V' prefix
            version = version.TrimStart('v', 'V');

            // Remove suffix after hyphen (e.g., "-forked", "-beta")
            var hyphenIndex = version.IndexOf('-');
            if (hyphenIndex > 0)
            {
                version = version.Substring(0, hyphenIndex);
            }

            return version;
        }

        /// <summary>
        /// Shows the update available dialog to the user.
        /// </summary>
        private static void ShowUpdateDialog(GitHubRelease release)
        {
            var latestVersion = release.TagName.TrimStart('v', 'V');

            UIMobileAlert alert = null;
            alert = new UIMobileAlert(new UIAlertOptions
            {
                Title = "Update Available",
                Message = $"A new version of Simitone is available!\n\n" +
                          $"Current version: {SimitoneVersion.Current}\n" +
                          $"Latest version: {latestVersion}\n\n" +
                          $"Would you like to download the update?",
                Buttons = new UIAlertButton[]
                {
                    new UIAlertButton(UIAlertButtonType.Yes, (btn) =>
                    {
                        OpenDownloadPage(release.HtmlUrl);
                        alert.Close();
                    }, "Download"),
                    new UIAlertButton(UIAlertButtonType.No, (btn) =>
                    {
                        alert.Close();
                    }, "Skip")
                }
            });

            HITVM.Get().PlaySoundEvent(UISounds.CallSend);
            UIScreen.GlobalShowDialog(alert, true);
        }

        /// <summary>
        /// Opens the download page in the default browser.
        /// </summary>
        private static void OpenDownloadPage(string url)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception)
            {
                // Failed to open browser - not critical, just ignore
            }
        }
    }
}
