using FSO.Client;
using FSO.Common;
using FSO.Common.Rendering.Framework;
using FSO.Common.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;

namespace Simitone.Client.Utils
{
    // Manages Simitone-specific custom cursors that aren't part of FreeSO.
    public static class SimitoneCursors
    {
        private static MouseCursor EyedropperCursor;
        private static bool Initialized;

        // Initialize Simitone-specific cursors.
        public static void Init(GraphicsDevice gd)
        {
            if (Initialized) return;

            var pngPath = Path.Combine(FSOEnvironment.ContentDir, "Cursors", "eyedropper.png");

            if (File.Exists(pngPath))
            {
                try
                {
                    using (var stream = File.Open(pngPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var texture = Texture2D.FromStream(gd, stream);
                        // Hotspot at tip of eyedropper (adjust X,Y as needed)
                        EyedropperCursor = MouseCursor.FromTexture2D(texture, 1, 1);
                    }
                }
                catch (Exception)
                {
                    EyedropperCursor = null;
                }
            }

            Initialized = true;
        }

        // Sets the eyedropper cursor if available, otherwise uses a fallback.
        public static void SetEyedropperCursor()
        {
            if (EyedropperCursor != null)
            {
                Mouse.SetCursor(EyedropperCursor);
            }
            else
            {
                // Fallback to crosshair if custom cursor not available
                Mouse.SetCursor(MouseCursor.Crosshair);
            }
        }

        // Returns true if the eyedropper cursor was loaded successfully.
        public static bool HasEyedropperCursor => EyedropperCursor != null;

        private static Dictionary<CursorType, CursorGroup> ClassicCursors;

        // With the classic UI, the game's own cursors from the TS1 install: its arrow in place of the system's, and its
        // neighbourhood cursors (moving in, evicting, and nowhere to do either). On Windows the others already come from
        // the install; macOS and Linux otherwise keep the system arrow everywhere, so there classic mode loads them all,
        // and turning it off puts the system arrow back.
        public static void SetClassic(bool classic)
        {
            var cursors = GameFacade.Cursor;
            if (cursors == null || (!classic && ClassicCursors == null)) return; //nothing to put back
            if (ClassicCursors == null)
            {
                ClassicCursors = new Dictionary<CursorType, CursorGroup>();
                //Windows sets this at startup; elsewhere the image loader can't read a cursor's 1-bit bitmaps
                if (CurLoader.BmpLoaderFunc == null) CurLoader.BmpLoaderFunc = LoadBitmap;
                void load(CursorType type, string name)
                {
                    var group = Load(name);
                    if (group != null) ClassicCursors[type] = group.Value;
                }
                load(CursorType.Normal, "Arrow.cur");
                load(CursorType.MoveIn, "MOVEIN.cur");
                load(CursorType.Bulldoze, "BULLDOZE.cur");
                load(CursorType.NoDrop, "NODROP.CUR");
                load(CursorType.Rezone, "Rezone2.cur"); //the sign and shovel of the Rezone button (Rezone.cur is an older bulldozer)
                if (GameFacade.Linux)
                {
                    foreach (var item in cursors.GenMap()) load(item.Key, item.Value);
                    var stars = ClassicUIGraphics.FindGameFile("UIGraphics", "Shared", "cursors", "liveobjectavail.cur");
                    try
                    {
                        if (stars != null)
                        {
                            using (var stream = File.OpenRead(stars))
                            {
                                var upgrades = CurLoader.LoadUpgradeCursors(GameFacade.GraphicsDevice, stream, 5);
                                for (int i = 0; i < upgrades.Length; i++) ClassicCursors[CursorType.LiveObject1Star + i] = upgrades[i];
                            }
                        }
                    }
                    catch (Exception) { }
                }
            }
            var system = new CursorGroup(MouseCursor.Arrow);
            foreach (var item in ClassicCursors)
            {
                //on Windows only the arrow had a system cursor before; the others stay loaded
                if (!classic && !GameFacade.Linux && item.Key != CursorType.Normal) continue;
                cursors.SetCursorGroup(item.Key, classic ? item.Value : system);
            }
        }

        /// <summary>
        /// An uncompressed bitmap of up to 32 bits per pixel, as a cursor holds (its image and mask, one above the other).
        /// </summary>
        private static Texture2D LoadBitmap(GraphicsDevice gd, Stream stream)
        {
            var io = new BinaryReader(stream);
            io.ReadBytes(14); //the file header's data offset isn't set by the cursor loader; the data follows the palette
            var headerSize = io.ReadInt32();
            var width = io.ReadInt32();
            var height = io.ReadInt32();
            io.ReadInt16();
            var bpp = io.ReadInt16();
            io.ReadBytes(16);
            var colorsUsed = io.ReadInt32();
            stream.Seek(14 + headerSize, SeekOrigin.Begin);
            var palette = new Color[(bpp <= 8) ? ((colorsUsed == 0) ? (1 << bpp) : colorsUsed) : 0];
            for (int i = 0; i < palette.Length; i++)
            {
                var bgra = io.ReadBytes(4);
                palette[i] = new Color(bgra[2], bgra[1], bgra[0]);
            }

            var rows = Math.Abs(height);
            var stride = ((width * bpp + 31) / 32) * 4;
            var data = new Color[width * rows];
            for (int r = 0; r < rows; r++)
            {
                var row = io.ReadBytes(stride);
                var y = (height > 0) ? rows - 1 - r : r; //bottom up, unless the height is negative
                for (int x = 0; x < width; x++)
                {
                    Color c;
                    switch (bpp)
                    {
                        case 32: c = new Color(row[x * 4 + 2], row[x * 4 + 1], row[x * 4]); break;
                        case 24: c = new Color(row[x * 3 + 2], row[x * 3 + 1], row[x * 3]); break;
                        case 8: c = palette[row[x]]; break;
                        case 4: c = palette[(row[x / 2] >> ((x % 2 == 0) ? 4 : 0)) & 15]; break;
                        default: c = palette[(row[x / 8] >> (7 - x % 8)) & 1]; break;
                    }
                    data[y * width + x] = c;
                }
            }
            var tex = new Texture2D(gd, width, rows);
            tex.SetData(data);
            return tex;
        }

        private static CursorGroup? Load(string name)
        {
            var path = ClassicUIGraphics.FindGameFile("UIGraphics", "Shared", "cursors", name);
            if (path == null) return null;
            try
            {
                var data = File.ReadAllBytes(path);
                if (data.Length > 14 && data[2] == 1)
                {
                    //MOVEIN.cur is stored as an icon, which has no hotspot: make it a cursor pointing at its middle
                    data[2] = 2;
                    data[10] = (byte)(data[6] / 2);
                    data[11] = 0;
                    data[12] = (byte)(data[7] / 2);
                    data[13] = 0;
                }
                using (var stream = new MemoryStream(data))
                    return CurLoader.LoadMonoCursor(GameFacade.GraphicsDevice, stream);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
