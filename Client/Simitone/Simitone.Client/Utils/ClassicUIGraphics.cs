using FSO.Client;
using FSO.Content;
using FSO.Files;
using FSO.Files.FAR1;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// The original Windows UI art, read from UIGraphics/UIGraphics.far in the player's TS1 install for the classic
    /// control panel (none of it is shipped with Simitone). Bitmaps use magenta as transparency, which the image
    /// loader removes; buttons are strips of 4 frames (normal, pressed, hover, disabled) like FreeSO's UIButton.
    /// </summary>
    public static class ClassicUIGraphics
    {
        private static bool Initialized;
        private static FAR1Archive Archive;
        private static Dictionary<string, FarEntry> Entries;
        private static Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// If the install has the classic UI art (UIGraphics.far).
        /// </summary>
        public static bool Available
        {
            get
            {
                Init();
                return Archive != null;
            }
        }

        private static void Init()
        {
            if (Initialized) return;
            Initialized = true;
            var path = FindFile(Content.Get().TS1BasePath, "UIGraphics", "UIGraphics.far");
            if (path == null) return;
            try
            {
                Archive = new FAR1Archive(path, false);
                //paths in the archive use backslashes and inconsistent case ("cpanel\Buttons\AMPM.BMP")
                Entries = Archive.GetAllFarEntries()
                    .GroupBy(x => x.Filename.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                Archive = null;
            }
        }

        /// <summary>
        /// Finds a file in the TS1 install, ignoring case, such as ("GameData", "Build.iff"). Null if it isn't there.
        /// </summary>
        public static string FindGameFile(params string[] parts) => FindFile(Content.Get().TS1BasePath, parts);

        /// <summary>
        /// Finds a file below the base folder, ignoring case (installs copied from Windows can differ on other platforms).
        /// </summary>
        private static string FindFile(string baseDir, params string[] parts)
        {
            var path = baseDir;
            foreach (var part in parts)
            {
                if (path == null || !Directory.Exists(path)) return null;
                path = Directory.EnumerateFileSystemEntries(path)
                    .FirstOrDefault(x => string.Equals(Path.GetFileName(x), part, StringComparison.OrdinalIgnoreCase));
            }
            return (path != null && File.Exists(path)) ? path : null;
        }

        private static Dictionary<string, IffFile> GameData = new Dictionary<string, IffFile>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// One of the game's data files from GameData, such as "Live.iff" (the live mode panels' text), or null.
        /// </summary>
        public static IffFile GetGameData(string name)
        {
            if (GameData.TryGetValue(name, out var iff)) return iff;
            var path = FindGameFile("GameData", name);
            try
            {
                if (path != null) iff = new IffFile(path);
            }
            catch (Exception) { }
            GameData[name] = iff;
            return iff;
        }

        /// <summary>
        /// A string from one of the game's data files (see GetGameData), or the fallback if it isn't there.
        /// </summary>
        public static string GameString(string file, ushort table, int index, string fallback = "")
        {
            return GetGameData(file)?.Get<STR>(table)?.GetString(index) ?? fallback;
        }

        /// <summary>
        /// A file's bytes from the archive, such as "Fonts/variablesans_10.ffn", or null if it isn't there.
        /// </summary>
        public static byte[] GetFile(string path)
        {
            Init();
            if (Archive == null || !Entries.TryGetValue(path, out var entry)) return null;
            lock (Archive) return Archive.GetEntry(entry);
        }

        /// <summary>
        /// A texture from the archive, such as "cpanel/Buttons/people.bmp", or null if it isn't there.
        /// </summary>
        public static Texture2D Get(string path)
        {
            Init();
            if (Archive == null) return null;
            if (Cache.TryGetValue(path, out var tex)) return tex;
            if (Entries.TryGetValue(path, out var entry))
            {
                lock (Archive)
                {
                    using (var stream = new MemoryStream(Archive.GetEntry(entry)))
                        tex = ImageLoader.FromStream(GameFacade.GraphicsDevice, stream);
                }
            }
            Cache[path] = tex;
            return tex;
        }
    }
}
