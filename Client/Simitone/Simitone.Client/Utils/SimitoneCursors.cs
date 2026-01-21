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

            // Load the eyedropper cursor from the Content folder
            var cursorPath = Path.Combine(FSOEnvironment.ContentDir, "Cursors", "eyedropper.cur");
            
            // Debug: write to log file
            var logPath = Path.Combine(FSOEnvironment.ContentDir, "cursor_debug.log");
            try
            {
                File.AppendAllText(logPath, $"Looking for cursor at: {cursorPath}\n");
                File.AppendAllText(logPath, $"File exists: {File.Exists(cursorPath)}\n");
            }
            catch { }
            
            if (File.Exists(cursorPath))
            {
                try
                {
                    using (var stream = File.Open(cursorPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var cursorData = CurLoader.LoadMonoCursor(gd, stream);
                        EyedropperCursor = cursorData.MouseCursor;
                        try { File.AppendAllText(logPath, "Cursor loaded successfully!\n"); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    // Cursor file is invalid - will use fallback
                    try { File.AppendAllText(logPath, $"Failed to load cursor: {ex.Message}\n"); } catch { }
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
                // Fallback - set directly via Mouse to bypass CursorManager state
                var group = CursorManager.INSTANCE?.GetCurrentGroup();
                if (CursorManager.INSTANCE != null)
                {
                    // Force set SimsRotate as fallback (visually distinct)
                    Mouse.SetCursor(MouseCursor.Crosshair);
                }
            }
        }

        /// <summary>
        /// Returns true if the eyedropper cursor was loaded successfully.
        /// </summary>
        public static bool HasEyedropperCursor => EyedropperCursor != null;
    }
}
