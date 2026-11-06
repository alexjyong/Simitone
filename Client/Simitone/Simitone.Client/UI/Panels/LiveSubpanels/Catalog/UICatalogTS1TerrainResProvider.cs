using FSO.Client;
using FSO.Client.UI.Controls.Catalog;
using FSO.Content;
using FSO.Files;
using FSO.Files.FAR1;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client.UI.Panels.LiveSubpanels.Catalog
{
    /// <summary>
    /// Catalog icons for the terrain tools from The Sims 1's own build mode art. The FreeSO provider asks for The Sims
    /// Online's UI images, which TS1 doesn't have, so the tools showed plain squares or nothing.
    /// </summary>
    public class UICatalogTS1TerrainResProvider : UICatalogTerrainResProvider
    {
        //in UIGraphics/UIGraphics.far, by ResID: raise/lower, flatten, grass
        private static readonly string[] Pictures =
        {
            @"cpanel\Build\PopupTerrainUp.BMP",
            @"cpanel\Build\PopupTerrainLevel.bmp",
            @"cpanel\HDBuild\popupGrass.bmp",
        };

        private static readonly Dictionary<ulong, Texture2D> Icons = new Dictionary<ulong, Texture2D>();

        public override Texture2D GetIcon(ulong id)
        {
            if (!Icons.TryGetValue(id, out var icon))
            {
                icon = (id < (ulong)Pictures.Length) ? Load(Pictures[id]) : null;
                Icons[id] = icon;
            }
            return icon ?? base.GetIcon(id);
        }

        /// <summary>A picture from TS1's UIGraphics.far as a catalog icon, or null if it can't be read.</summary>
        public static Texture2D Load(string name)
        {
            try
            {
                var path = Path.Combine(Content.Get().TS1BasePath, "UIGraphics/UIGraphics.far");
                var far = new FAR1Archive(path, false);
                try
                {
                    var entry = far.GetAllFarEntries().FirstOrDefault(x => string.Equals(x.Filename, name, StringComparison.OrdinalIgnoreCase));
                    if (entry == null) return null;
                    using (var str = new MemoryStream(far.GetEntry(entry)))
                    using (var bmp = ImageLoader.FromStream(GameFacade.GraphicsDevice, str))
                    {
                        return Cutout(bmp);
                    }
                }
                finally
                {
                    far.Close();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// TS1's build popups are a light shape on a dark blue box. Keep just the shape, in the dark blue, on a
        /// transparent square trimmed to it, so it reads on the catalog's light circle.
        /// </summary>
        public static Texture2D Cutout(Texture2D src)
        {
            var w = src.Width;
            var h = src.Height;
            var data = new Color[w * h];
            src.GetData(data);

            var bg = data[0].ToVector3();
            var fg = data.Select(x => x.ToVector3()).OrderByDescending(x => (x - bg).LengthSquared()).First();
            var span = (fg - bg).Length();
            var ink = bg; //the box's own dark blue

            var alpha = new float[w * h];
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    var a = (span <= 0) ? 0 : MathHelper.Clamp((data[i].ToVector3() - bg).Length() / span, 0, 1);
                    alpha[i] = a;
                    if (a > 0.1f)
                    {
                        minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    }
                }
            }
            if (maxX < 0) return null;

            //square, so the catalog draws it whole rather than as half of a two-frame picture
            var cw = maxX - minX + 1;
            var ch = maxY - minY + 1;
            var size = Math.Max(cw, ch) + 8;
            var ox = (size - cw) / 2 - minX;
            var oy = (size - ch) / 2 - minY;
            var result = new Color[size * size];
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var a = alpha[y * w + x];
                    result[(y + oy) * size + (x + ox)] = new Color(ink * a) { A = (byte)(a * 255) }; //premultiplied
                }
            }
            var tex = new Texture2D(GameFacade.GraphicsDevice, size, size);
            tex.SetData(result);
            return tex;
        }

        public override bool DoDispose()
        {
            return false;
        }
    }
}
