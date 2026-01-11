using System;
using System.IO;
using System.Collections.Generic;

namespace Simitone.Windows.GameLocator
{
    public class MacOSLocator : ILocator
    {
        public string FindTheSimsOnline()
        {
            return "";
            //string localDir = @"../The Sims Online/TSOClient/";
            //if (File.Exists(Path.Combine(localDir, "tuning.dat"))) return localDir;

            //return string.Format("{0}/Documents/The Sims Online/TSOClient/", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        }

        public string FindTheSims1()
        {
            // Return first found installation
            var installations = GetAllTheSims1Installations();
            return installations.Count > 0 ? installations[0].path : null;
        }

        /// <summary>
        /// Gets all detected The Sims 1 installations on macOS
        /// </summary>
        public List<(string description, string path, TS1InstallationType type)> GetAllTheSims1Installations()
        {
            var installations = new List<(string, string, TS1InstallationType)>();

            // Check relative path (portable install)
            string localDir = @"../The Sims/";
            if (File.Exists(Path.Combine(localDir, "GameData", "Behavior.iff")))
            {
                installations.Add(("Portable Install (Relative Directory)", localDir, TS1InstallationType.Portable));
            }

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            // Check Steam for macOS
            var steamPath = Path.Combine(homeDir, "Library/Application Support/Steam/steamapps/common/The Sims/");
            if (File.Exists(Path.Combine(steamPath, "GameData", "Behavior.iff")))
            {
                installations.Add(("Steam - The Sims: Legacy Collection", steamPath, TS1InstallationType.Steam));
            }
            
            // Check Wine for macOS (if installed)
            var winePath = Path.Combine(homeDir, ".wine/drive_c/Program Files/Maxis/The Sims/");
            if (File.Exists(Path.Combine(winePath, "GameData", "Behavior.iff")))
            {
                installations.Add(("Wine - Default Prefix", winePath, TS1InstallationType.Wine));
            }
            
            // Check Wine (x86 prefix)
            var winePath32 = Path.Combine(homeDir, ".wine/drive_c/Program Files (x86)/Maxis/The Sims/");
            if (File.Exists(Path.Combine(winePath32, "GameData", "Behavior.iff")))
            {
                installations.Add(("Wine - Default Prefix (x86)", winePath32, TS1InstallationType.Wine));
            }

            // Check fallback location
            if (File.Exists(Path.Combine("game1/", "GameData", "Behavior.iff")))
            {
                installations.Add(("Fallback Location", "game1/", TS1InstallationType.Unknown));
            }
            
            return installations;
        }
    }
}
