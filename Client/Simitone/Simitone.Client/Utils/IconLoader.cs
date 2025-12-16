using System;
using System.IO;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Provides cross-platform application icon loading for window decoration.
    /// On Windows, icons are embedded in the executable via .ico files.
    /// On Linux/macOS, icons must be set programmatically using SDL2.
    /// </summary>
    public static class IconLoader
    {
        // Determine SDL2 library name based on platform
        private static readonly string SDL2_LIB = GetSDL2LibraryName();

        private static string GetSDL2LibraryName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "libSDL2-2.0.so.0";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "libSDL2.dylib";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "SDL2.dll";
            else
                return "SDL2"; // Fallback
        }

        [DllImport("libSDL2-2.0.so.0", EntryPoint = "SDL_CreateRGBSurfaceFrom", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_CreateRGBSurfaceFrom_Linux(
            IntPtr pixels,
            int width,
            int height,
            int depth,
            int pitch,
            uint Rmask,
            uint Gmask,
            uint Bmask,
            uint Amask);

        [DllImport("libSDL2.dylib", EntryPoint = "SDL_CreateRGBSurfaceFrom", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_CreateRGBSurfaceFrom_macOS(
            IntPtr pixels,
            int width,
            int height,
            int depth,
            int pitch,
            uint Rmask,
            uint Gmask,
            uint Bmask,
            uint Amask);

        [DllImport("SDL2.dll", EntryPoint = "SDL_CreateRGBSurfaceFrom", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_CreateRGBSurfaceFrom_Windows(
            IntPtr pixels,
            int width,
            int height,
            int depth,
            int pitch,
            uint Rmask,
            uint Gmask,
            uint Bmask,
            uint Amask);

        private static IntPtr SDL_CreateRGBSurfaceFrom(
            IntPtr pixels,
            int width,
            int height,
            int depth,
            int pitch,
            uint Rmask,
            uint Gmask,
            uint Bmask,
            uint Amask)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return SDL_CreateRGBSurfaceFrom_Linux(pixels, width, height, depth, pitch, Rmask, Gmask, Bmask, Amask);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return SDL_CreateRGBSurfaceFrom_macOS(pixels, width, height, depth, pitch, Rmask, Gmask, Bmask, Amask);
            else
                return SDL_CreateRGBSurfaceFrom_Windows(pixels, width, height, depth, pitch, Rmask, Gmask, Bmask, Amask);
        }

        [DllImport("libSDL2-2.0.so.0", EntryPoint = "SDL_FreeSurface", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_FreeSurface_Linux(IntPtr surface);

        [DllImport("libSDL2.dylib", EntryPoint = "SDL_FreeSurface", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_FreeSurface_macOS(IntPtr surface);

        [DllImport("SDL2.dll", EntryPoint = "SDL_FreeSurface", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_FreeSurface_Windows(IntPtr surface);

        private static void SDL_FreeSurface(IntPtr surface)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                SDL_FreeSurface_Linux(surface);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                SDL_FreeSurface_macOS(surface);
            else
                SDL_FreeSurface_Windows(surface);
        }

        [DllImport("libSDL2-2.0.so.0", EntryPoint = "SDL_SetWindowIcon", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_SetWindowIcon_Linux(IntPtr window, IntPtr icon);

        [DllImport("libSDL2.dylib", EntryPoint = "SDL_SetWindowIcon", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_SetWindowIcon_macOS(IntPtr window, IntPtr icon);

        [DllImport("SDL2.dll", EntryPoint = "SDL_SetWindowIcon", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_SetWindowIcon_Windows(IntPtr window, IntPtr icon);

        private static void SDL_SetWindowIcon(IntPtr window, IntPtr icon)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                SDL_SetWindowIcon_Linux(window, icon);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                SDL_SetWindowIcon_macOS(window, icon);
            else
                SDL_SetWindowIcon_Windows(window, icon);
        }

        /// <summary>
        /// Sets the window icon for Linux/macOS using SDL2.
        /// On Windows, this is handled by the .ico file in the project settings.
        /// </summary>
        /// <param name="window">The SDL window handle (from MonoGame's Window.Handle)</param>
        /// <param name="iconPath">Path to the icon file (BMP, PNG, etc.)</param>
        public static void SetWindowIcon(IntPtr window, string iconPath)
        {
            if (window == IntPtr.Zero)
            {
                Console.WriteLine("Warning: Cannot set window icon - invalid window handle");
                return;
            }

            if (!File.Exists(iconPath))
            {
                Console.WriteLine($"Warning: Icon file not found: {iconPath}");
                return;
            }

            try
            {
                // Load image using ImageSharp
                using (var image = Image.Load<Rgba32>(iconPath))
                {
                    // Extract pixel data
                    var pixelData = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(pixelData);

                    // Pin the pixel data in memory
                    var handle = GCHandle.Alloc(pixelData, GCHandleType.Pinned);
                    try
                    {
                        // Create SDL surface from pixel data
                        // RGBA format: R=0xFF000000, G=0x00FF0000, B=0x0000FF00, A=0x000000FF
                        var surface = SDL_CreateRGBSurfaceFrom(
                            handle.AddrOfPinnedObject(),
                            image.Width,
                            image.Height,
                            32,
                            image.Width * 4,
                            0x000000FF,
                            0x0000FF00,
                            0x00FF0000,
                            0xFF000000
                        );

                        if (surface != IntPtr.Zero)
                        {
                            // Set the window icon
                            SDL_SetWindowIcon(window, surface);
                            SDL_FreeSurface(surface);
                            Console.WriteLine($"Window icon set successfully from: {iconPath}");
                        }
                        else
                        {
                            Console.WriteLine("Warning: Failed to create SDL surface for icon");
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to set window icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the window icon using an embedded resource.
        /// </summary>
        /// <param name="window">The SDL window handle</param>
        /// <param name="resourceStream">Stream to the icon resource</param>
        public static void SetWindowIconFromStream(IntPtr window, Stream resourceStream)
        {
            if (window == IntPtr.Zero || resourceStream == null)
            {
                Console.WriteLine("Warning: Cannot set window icon - invalid parameters");
                return;
            }

            try
            {
                using (var image = Image.Load<Rgba32>(resourceStream))
                {
                    var pixelData = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(pixelData);

                    var handle = GCHandle.Alloc(pixelData, GCHandleType.Pinned);
                    try
                    {
                        var surface = SDL_CreateRGBSurfaceFrom(
                            handle.AddrOfPinnedObject(),
                            image.Width,
                            image.Height,
                            32,
                            image.Width * 4,
                            0x000000FF,
                            0x0000FF00,
                            0x00FF0000,
                            0xFF000000
                        );

                        if (surface != IntPtr.Zero)
                        {
                            SDL_SetWindowIcon(window, surface);
                            SDL_FreeSurface(surface);
                            Console.WriteLine("Window icon set successfully from embedded resource");
                        }
                        else
                        {
                            Console.WriteLine("Warning: Failed to create SDL surface for icon");
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to set window icon from stream: {ex.Message}");
            }
        }
    }
}
