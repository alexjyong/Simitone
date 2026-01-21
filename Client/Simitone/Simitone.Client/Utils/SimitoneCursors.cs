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
        private static bool EyedropperActive;

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
                try
                {
                    using (var stream = File.Open(cursorPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var cursorData = CurLoader.LoadMonoCursor(gd, stream);
                        EyedropperCursor = cursorData.MouseCursor;
                    }
                }
                catch (Exception)
                {
                    // Cursor file is invalid - will use fallback
                    EyedropperCursor = null;
                }
            }

            Initialized = true;
        }

        /// <summary>
        /// Sets the eyedropper cursor if available, otherwise uses a fallback.
        /// Uses high priority to prevent CursorManager from overriding it.
        /// </summary>
        public static void SetEyedropperCursor()
        {
            // Use high priority to prevent other cursor changes from overriding
            CursorManager.INSTANCE?.SetCursorPriority(10);
            EyedropperActive = true;

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
        /// Clears the eyedropper cursor priority, allowing normal cursor handling.
        /// Call this when eyedropper mode is disabled.
        /// </summary>
        public static void ClearEyedropperCursor()
        {
            if (EyedropperActive)
            {
                CursorManager.INSTANCE?.SetCursorPriority(0);
                EyedropperActive = false;
            }
        }

        /// <summary>
        /// Returns true if the eyedropper cursor was loaded successfully.
        /// </summary>
        public static bool HasEyedropperCursor => EyedropperCursor != null;
    }
}
