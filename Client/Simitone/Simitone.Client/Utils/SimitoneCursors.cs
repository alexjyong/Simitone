using FSO.Common;
using FSO.Common.Rendering.Framework;
using FSO.Common.Utils;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Manages Simitone-specific custom cursors that aren't part of FreeSO.
    /// </summary>
    public static class SimitoneCursors
    {
        private static MouseCursor EyedropperCursor;
        private static bool Initialized;

        /// <summary>
        /// Initialize Simitone-specific cursors.
        /// </summary>
        public static void Init(GraphicsDevice gd)
        {
            if (Initialized) return;

            // Try loading from PNG first (more reliable), then fall back to .cur
            var pngPath = Path.Combine(FSOEnvironment.ContentDir, "Cursors", "eyedropper.png");
            var curPath = Path.Combine(FSOEnvironment.ContentDir, "Cursors", "eyedropper.cur");
            
            // Try PNG first
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
            
            // Fall back to .cur if PNG didn't work
            if (EyedropperCursor == null && File.Exists(curPath))
            {
                try
                {
                    using (var stream = File.Open(curPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var cursorData = CurLoader.LoadMonoCursor(gd, stream);
                        EyedropperCursor = cursorData.MouseCursor;
                    }
                }
                catch (Exception)
                {
                    EyedropperCursor = null;
                }
            }

            Initialized = true;
        }

        /// <summary>
        /// Sets the eyedropper cursor if available, otherwise uses a fallback.
        /// </summary>
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

        /// <summary>
        /// Returns true if the eyedropper cursor was loaded successfully.
        /// </summary>
        public static bool HasEyedropperCursor => EyedropperCursor != null;
    }
}
