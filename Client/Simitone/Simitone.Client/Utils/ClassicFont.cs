using FSO.Client;
using FSO.Client.UI.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// The game's own font (variablesans, Fonts/*.ffn in UIGraphics.far), for the classic UI. Each size is a bitmap
    /// font, read into a SpriteFont that FreeSO's text styles can draw with.
    ///
    /// The .ffn layout, as far as the classic UI needs it (all little endian):
    ///   "FNTFPF\0\0", u16 ?, u16 glyph count, ... , u32 bitmap offset at 0x1C
    ///   from 0x20, 11 bytes a glyph: u16 character (Windows-1252), u8 width, u8 height, u16 x, u16 y in the bitmap,
    ///     s8 left bearing, s8 right bearing, s8 top (from the top of the line). The space's bearings aren't a width.
    ///   at the bitmap offset: 16 byte header with u16 width and height at 4, then 4 bits a pixel (high nibble first),
    ///     which is the glyph's coverage.
    /// </summary>
    public static class ClassicFont
    {
        private static readonly int[] Sizes = { 7, 8, 9, 10, 11, 12, 14, 16, 18, 20 };
        //Windows-1252's characters from 0x80 to 0x9F (0 where it has none)
        private static readonly char[] Cp1252 =
        {
            '€', '\0', '‚', 'ƒ', '„', '…', '†', '‡', 'ˆ', '‰', 'Š', '‹', 'Œ', '\0', 'Ž', '\0',
            '\0', '‘', '’', '“', '”', '•', '–', '—', '˜', '™', 'š', '›', 'œ', '\0', 'ž', 'Ÿ'
        };

        private static bool Loaded;
        private static Font _Font;

        /// <summary>
        /// Every size of the game's font, or null if the install doesn't have them.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (!Loaded)
                {
                    Loaded = true;
                    var fonts = new Dictionary<int, SpriteFont>();
                    foreach (var size in Sizes)
                    {
                        var data = ClassicUIGraphics.GetFile("Fonts/variablesans_" + size.ToString("00") + ".ffn");
                        if (data == null) continue;
                        try
                        {
                            fonts[size] = Read(data, size);
                        }
                        catch (Exception) { }
                    }
                    if (fonts.Count > 0)
                    {
                        //every pixel size draws the nearest size the game has, unscaled (a scaled bitmap font blurs)
                        _Font = new Font();
                        for (int px = 4; px <= 48; px++) _Font.AddSize(px, fonts[fonts.Keys.OrderBy(x => Math.Abs(x - px)).First()]);
                    }
                }
                return _Font;
            }
        }

        /// <summary>
        /// A label style in the game's font, or Simitone's own if the game's isn't there.
        /// </summary>
        public static TextStyle Style(int size)
        {
            var style = TextStyle.DefaultLabel.Clone();
            if (Font != null)
            {
                style.VFont = null;
                style.Font = Font;
            }
            style.Size = size;
            return style;
        }

        private static SpriteFont Read(byte[] data, int size)
        {
            using (var io = new BinaryReader(new MemoryStream(data)))
            {
                if (new string(io.ReadChars(6)) != "FNTFPF") throw new InvalidDataException("not a font");
                io.BaseStream.Position = 10;
                var count = io.ReadUInt16();
                io.BaseStream.Position = 0x1C;
                var bitmapOffset = io.ReadInt32();

                var glyphs = new List<(char Char, Rectangle Bounds, Rectangle Cropping, Vector3 Kerning)>();
                var lineSpacing = 0;
                for (int i = 0; i < count; i++)
                {
                    io.BaseStream.Position = 0x20 + i * 11;
                    var code = io.ReadUInt16();
                    int w = io.ReadByte(), h = io.ReadByte(), x = io.ReadUInt16(), y = io.ReadUInt16();
                    int left = io.ReadSByte(), right = io.ReadSByte(), top = io.ReadSByte();
                    var c = (code >= 0x80 && code < 0xA0) ? Cp1252[code - 0x80] : (char)code;
                    if (c == '\0') continue;
                    if (c == ' ')
                    {
                        //the space has no real metrics; a third of the size and a bit is close to the game's word spacing
                        glyphs.Add((c, new Rectangle(x, y, 0, 0), new Rectangle(0, 0, 0, 0), new Vector3(0, 0, (size + 4) / 3)));
                        continue;
                    }
                    //the bearings are each a pixel wider than the game draws them, as the glyphs are padded for smoothing
                    glyphs.Add((c, new Rectangle(x, y, w, h), new Rectangle(0, top, w, h), new Vector3(left, w, right - 1)));
                    if (c > ' ' && c < 0x7F) lineSpacing = Math.Max(lineSpacing, top + h);
                }

                io.BaseStream.Position = bitmapOffset + 4;
                int width = io.ReadUInt16(), height = io.ReadUInt16();
                io.BaseStream.Position = bitmapOffset + 16;
                var bits = io.ReadBytes(width * height / 2);
                var pixels = new Color[width * height];
                for (int i = 0; i < bits.Length; i++)
                {
                    //coverage is the alpha; premultiplied white so the text style's colour tints it
                    var hi = (byte)((bits[i] >> 4) * 17);
                    var lo = (byte)((bits[i] & 15) * 17);
                    pixels[i * 2] = new Color(hi, hi, hi, hi);
                    pixels[i * 2 + 1] = new Color(lo, lo, lo, lo);
                }
                var texture = new Texture2D(GameFacade.GraphicsDevice, width, height);
                texture.SetData(pixels);

                glyphs.Sort((a, b) => a.Char.CompareTo(b.Char));
                return new SpriteFont(texture, glyphs.Select(g => g.Bounds).ToList(), glyphs.Select(g => g.Cropping).ToList(),
                    glyphs.Select(g => g.Char).ToList(), lineSpacing, 0, glyphs.Select(g => g.Kerning).ToList(), '*');
            }
        }
    }
}
