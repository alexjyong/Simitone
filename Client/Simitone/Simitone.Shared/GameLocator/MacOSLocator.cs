using System;
using System.IO;

namespace Simitone.Windows.GameLocator
{
    public class MacOSLocator : ILocator
    {
        public string FindTheSimsOnline()
        {
            return "";
        }

        public string FindTheSims1()
        {
            // check relative directory first (portable install)
            string localDir = @"../The Sims/";
            if (File.Exists(Path.Combine(localDir, "GameData", "Behavior.iff"))) return localDir;

            // check fallback directory
            if (File.Exists(Path.Combine("game1/", "GameData", "Behavior.iff"))) return "game1/";
            
            return null; // Not found
        }
    }
}
