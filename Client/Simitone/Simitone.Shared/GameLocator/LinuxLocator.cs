using System;
using System.IO;

namespace Simitone.Windows.GameLocator
{
    public class LinuxLocator : ILocator
    {
        public string FindTheSimsOnline()
        {
            return "";
            //string localDir = @"../The Sims Online/TSOClient/";
            //if (File.Exists(Path.Combine(localDir, "tuning.dat"))) return localDir;

            //return "game/TSOClient/";
        }

        public string FindTheSims1()
        {

            string localDir = @"../The Sims/";
            if (File.Exists(Path.Combine(localDir, "GameData", "Behavior.iff"))) return localDir;

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            var steamPath = Path.Combine(homeDir, ".steam/steam/steamapps/common/The Sims/");
            if (File.Exists(Path.Combine(steamPath, "GameData", "Behavior.iff"))) return steamPath;
            
            var winePath = Path.Combine(homeDir, ".wine/drive_c/Program Files/Maxis/The Sims/");
            if (File.Exists(Path.Combine(winePath, "GameData", "Behavior.iff"))) return winePath;
            
            var winePath32 = Path.Combine(homeDir, ".wine/drive_c/Program Files (x86)/Maxis/The Sims/");
            if (File.Exists(Path.Combine(winePath32, "GameData", "Behavior.iff"))) return winePath32;

            if (File.Exists(Path.Combine("game1/", "GameData", "Behavior.iff"))) return "game1/";
            
            return null;
        }
    }
}
