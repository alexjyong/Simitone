using FSO.Common;
using FSO.Common.Rendering.Framework;
using FSO.Common.Utils;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

            // Load the eyedropper cursor from the Content folder
            var cursorPath = Path.Combine(FSOEnvironment.ContentDir, "Cursors", "eyedropper.cur");
            if (File.Exists(cursorPath))
            {
                using (var stream = File.Open(cursorPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var cursorData = CurLoader.LoadMonoCursor(gd, stream);
                    EyedropperCursor = cursorData.MouseCursor;
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
                // Fallback to LiveObjectAvail if custom cursor not available
                CursorManager.INSTANCE?.SetCursor(CursorType.LiveObjectAvail);
            }
        }

        /// <summary>
        /// Returns true if the eyedropper cursor was loaded successfully.
        /// </summary>
        public static bool HasEyedropperCursor => EyedropperCursor != null;
    }
}
