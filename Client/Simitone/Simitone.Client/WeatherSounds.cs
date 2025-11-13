using Microsoft.Xna.Framework.Audio;
using System;
using System.IO;

namespace Simitone.Client
{
    /// <summary>
    /// Manages weather sound effects (rain and thunder)
    /// </summary>
    public static class WeatherSounds
    {
        private static SoundEffect RainLoopSound;
        private static SoundEffect ThunderSound;
        private static SoundEffectInstance RainLoopInstance;

        private static bool IsLoaded = false;
        private static bool IsRainPlaying = false;

        /// <summary>
        /// Load weather sound effects from disk
        /// </summary>
        public static void Load(string contentPath)
        {
            try
            {
                var rainPath = Path.Combine(contentPath, "Sounds", "rain_loop.ogg");
                var thunderPath = Path.Combine(contentPath, "Sounds", "thunder.ogg");

                if (File.Exists(rainPath))
                {
                    using (var stream = File.OpenRead(rainPath))
                    {
                        RainLoopSound = SoundEffect.FromStream(stream);
                    }
                }

                if (File.Exists(thunderPath))
                {
                    using (var stream = File.OpenRead(thunderPath))
                    {
                        ThunderSound = SoundEffect.FromStream(stream);
                    }
                }

                IsLoaded = RainLoopSound != null;
            }
            catch (Exception ex)
            {
                // Silently fail if sounds can't be loaded
                Console.WriteLine($"Failed to load weather sounds: {ex.Message}");
                IsLoaded = false;
            }
        }

        /// <summary>
        /// Play rain loop at specified intensity (0.0 to 1.0)
        /// </summary>
        public static void PlayRain(float intensity)
        {
            if (!IsLoaded || RainLoopSound == null) return;

            if (!IsRainPlaying)
            {
                RainLoopInstance = RainLoopSound.CreateInstance();
                RainLoopInstance.IsLooped = true;
                RainLoopInstance.Volume = Math.Clamp(intensity, 0f, 1f) * 0.5f; // Max 50% volume
                RainLoopInstance.Play();
                IsRainPlaying = true;
            }
            else
            {
                // Update volume based on intensity
                if (RainLoopInstance != null)
                {
                    RainLoopInstance.Volume = Math.Clamp(intensity, 0f, 1f) * 0.5f;
                }
            }
        }

        /// <summary>
        /// Stop rain loop
        /// </summary>
        public static void StopRain()
        {
            if (RainLoopInstance != null && IsRainPlaying)
            {
                RainLoopInstance.Stop();
                RainLoopInstance.Dispose();
                RainLoopInstance = null;
                IsRainPlaying = false;
            }
        }

        /// <summary>
        /// Play thunder sound effect
        /// </summary>
        public static void PlayThunder(float volume = 0.7f)
        {
            if (!IsLoaded || ThunderSound == null) return;

            // Play thunder as one-shot (not looped)
            ThunderSound.Play(Math.Clamp(volume, 0f, 1f), 0f, 0f);
        }

        /// <summary>
        /// Cleanup sound resources
        /// </summary>
        public static void Unload()
        {
            StopRain();
            RainLoopSound?.Dispose();
            ThunderSound?.Dispose();
            RainLoopSound = null;
            ThunderSound = null;
            IsLoaded = false;
        }
    }
}
