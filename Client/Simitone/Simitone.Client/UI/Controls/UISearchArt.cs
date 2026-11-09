using FSO.Client;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using System;

namespace Simitone.Client.UI.Controls
{
    /// <summary>
    /// The catalog search box's art, drawn in code so it follows the UI style: a pill with a thin border (an accent
    /// border while typing), a magnifier, and a clear (x) mark. The icons are white, to be tinted when drawn.
    /// </summary>
    public static class UISearchArt
    {
        public const int Width = 230, Height = 32, IconSize = 16;
        private static UIStyle Built;
        public static Texture2D Box, BoxFocused, Magnifier, Clear;

        /// <summary>Makes the textures, again if the UI style has changed since.</summary>
        public static void Ensure()
        {
            var style = UIStyle.Current;
            if (Built == style && Box != null && !Box.IsDisposed) return;
            var gd = GameFacade.GraphicsDevice;
            Box?.Dispose(); BoxFocused?.Dispose(); Magnifier?.Dispose(); Clear?.Dispose();

            var fill = style.TitleBg;
            Box = Pill(gd, fill, Color.Lerp(fill, style.Text, 0.22f), 1);
            BoxFocused = Pill(gd, fill, style.ActiveSelection, 2);
            Magnifier = Icon(gd, (x, y) => Math.Min(
                Math.Abs(Distance(x, y, 6.5f, 6.5f) - 4.4f) - 0.95f, //the lens
                Capsule(x, y, 10.2f, 10.2f, 14f, 14f) - 1.15f));    //the handle
            Clear = Icon(gd, (x, y) => Math.Min(
                Capsule(x, y, 4f, 4f, 12f, 12f) - 0.95f,
                Capsule(x, y, 12f, 4f, 4f, 12f) - 0.95f));
            Built = style;
        }

        private static float Distance(float x, float y, float cx, float cy) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        /// <summary>Distance from a point to a line segment.</summary>
        private static float Capsule(float x, float y, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            var t = MathHelper.Clamp(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy), 0, 1);
            return Distance(x, y, ax + dx * t, ay + dy * t);
        }

        /// <summary>How much of a pixel a shape covers, from its signed distance (negative inside).</summary>
        private static float Coverage(float distance) => MathHelper.Clamp(0.5f - distance, 0, 1);

        private static Texture2D Icon(GraphicsDevice gd, Func<float, float, float> distance)
        {
            var data = new Color[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
                for (int x = 0; x < IconSize; x++)
                    data[y * IconSize + x] = Color.White * Coverage(distance(x + 0.5f, y + 0.5f)); //premultiplied
            var tex = new Texture2D(gd, IconSize, IconSize);
            tex.SetData(data);
            return tex;
        }

        private static Texture2D Pill(GraphicsDevice gd, Color fill, Color border, int borderWidth)
        {
            var data = new Color[Width * Height];
            float radius = Height / 2f, halfW = Width / 2f, halfH = Height / 2f;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float qx = Math.Abs(x + 0.5f - halfW) - (halfW - radius), qy = Math.Abs(y + 0.5f - halfH) - (halfH - radius);
                    var d = (float)Math.Sqrt(Math.Max(qx, 0) * Math.Max(qx, 0) + Math.Max(qy, 0) * Math.Max(qy, 0)) + Math.Min(Math.Max(qx, qy), 0) - radius;
                    var outside = Coverage(d);
                    var inside = Coverage(d + borderWidth);
                    var color = Color.Lerp(border, fill, inside);
                    data[y * Width + x] = color * outside; //premultiplied
                }
            }
            var tex = new Texture2D(gd, Width, Height);
            tex.SetData(data);
            return tex;
        }
    }
}
