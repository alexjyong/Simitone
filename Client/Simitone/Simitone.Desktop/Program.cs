using FSO.Client;
using FSO.Common;
using FSO.LotView;
using Simitone.Client;
using Simitone.Windows.GameLocator;
using Simitone.Windows.Utils;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Simitone.Windows
{
    /// <summary>
    /// Cross-platform entry point for Simitone (Linux/macOS/Windows with OpenGL)
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(baseDir);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            OperatingSystem os = Environment.OSVersion;
            PlatformID pid = os.Platform;

            ILocator gameLocator;
            bool linux = pid == PlatformID.MacOSX || pid == PlatformID.Unix;
            if (linux && Directory.Exists("/Users"))
                gameLocator = new MacOSLocator();
            else if (linux)
                gameLocator = new LinuxLocator();
            else
                gameLocator = new WindowsLocator();

            // Default to OpenGL on all platforms for the Desktop build
            var useDX = false;
            var path = gameLocator.FindTheSims1();

            FSOEnvironment.Enable3D = false;
            bool ide = false;
            bool aa = false;
            bool jit = false;
            
            #region User resolution parameters

            FSOEnvironment.Args = string.Join(" ", args);

            foreach (var arg in args)
            {
                if (arg.Length > 0 && arg[0] == '-')
                {
                    var cmd = arg.Substring(1);
                    if (cmd.StartsWith("lang"))
                    {
                        GlobalSettings.Default.LanguageCode = byte.Parse(cmd.Substring(4));
                    }
                    else if (cmd.StartsWith("hz")) GlobalSettings.Default.TargetRefreshRate = int.Parse(cmd.Substring(2));
                    else
                    {
                        //normal style param
                        switch (cmd)
                        {
                            case "ide":
                                ide = true;
                                break;
                            case "3d":
                                FSOEnvironment.Enable3D = true;
                                break;
                            case "aa":
                                aa = true;
                                break;
                            case "jit":
                                jit = true;
                                break;
                            case "dx":
                            case "dx11":
                                // Allow forcing DX on Windows even in Desktop build
                                if (!linux)
                                    useDX = true;
                                else
                                    Console.WriteLine("Warning: DirectX is not available on Linux/macOS. Using OpenGL.");
                                break;
                            case "gl":
                            case "ogl":
                                useDX = false;
                                break;
                            case "touch":
                                FSOEnvironment.SoftwareKeyboard = true;
                                break;
                            case "nosound":
                                FSOEnvironment.NoSound = true;
                                break;
                            case string s when s.StartsWith("path"): //The Sims path
                                path = s.Length > 4 ? s.Substring(4).Trim('"').Replace('\\', '/') + "/" : path;
                                break;
                        }
                    }
                }
            }
            #endregion
            
            useDX = MonogameLinker.Link(useDX);

            FSO.Files.ImageLoaderHelpers.BitmapFunction = BitmapReader;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            FSOEnvironment.SoftwareDepth = false;
            FSOEnvironment.UseMRT = true;

            if (path != null)
            {
                FSOEnvironment.ContentDir = "Content/";
                FSOEnvironment.GFXContentDir = "Content/" + (useDX ? "DX/" : "OGL/");
                
                // Get user directory - handle Linux where MyDocuments may be empty
                string userDir;
                var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(myDocs))
                {
                    userDir = Path.Combine(myDocs, "Simitone/");
                }
                else
                {
                    // Fallback for Linux: use ~/.local/share/Simitone or ~/Simitone
                    var localShare = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (!string.IsNullOrEmpty(localShare))
                    {
                        userDir = Path.Combine(localShare, "Simitone/");
                    }
                    else
                    {
                        // Ultimate fallback: ~/Simitone
                        userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Simitone/");
                    }
                }
                userDir = userDir.Replace('\\', '/');
                
                FSOEnvironment.UserDir = userDir;
                Directory.CreateDirectory(FSOEnvironment.UserDir);
                FSOEnvironment.Linux = linux;
                FSOEnvironment.DirectX = useDX;
                FSOEnvironment.GameThread = Thread.CurrentThread;
                if (GlobalSettings.Default.LanguageCode == 0) GlobalSettings.Default.LanguageCode = 1;
                FSO.Files.Formats.IFF.Chunks.STR.DefaultLangCode = (FSO.Files.Formats.IFF.Chunks.STRLangCode)GlobalSettings.Default.LanguageCode;

                GlobalSettings.Default.StartupPath = path;
                GlobalSettings.Default.TS1HybridEnable = true;
                GlobalSettings.Default.TS1HybridPath = path;
                GlobalSettings.Default.ClientVersion = "0";
                GlobalSettings.Default.LightingMode = 3;
                GlobalSettings.Default.AntiAlias = aa ? 1 : 0;
                GlobalSettings.Default.ComplexShaders = true;
                GlobalSettings.Default.EnableTransitions = true;

                // FSO.IDE (Volcanic) is not available on Linux/macOS - it requires Windows Forms
                if (ide)
                {
                    Console.WriteLine("Warning: Volcanic IDE is not available on this platform.");
                    Console.WriteLine("The IDE requires Windows. Continuing without IDE support.");
                }

                var assemblies = new FSO.SimAntics.JIT.Runtime.AssemblyStore();
                if (jit) assemblies.InitAOT();
                FSO.SimAntics.Engine.VMTranslator.INSTANCE = new FSO.SimAntics.JIT.Runtime.VMAOTTranslator(assemblies);

                var start = new GameStartProxy();
                start.Start(useDX);
            }
            else
            {
                Console.WriteLine("Error: Could not find The Sims 1 installation.");
                Console.WriteLine("Please use the -path argument to specify the location:");
                Console.WriteLine("  ./Simitone -path\"/path/to/The Sims/\"");
                Console.WriteLine();
                Console.WriteLine("Common locations:");
                Console.WriteLine("  Steam Play/Proton: ~/.steam/steam/steamapps/common/The Sims/");
                Console.WriteLine("  Wine: ~/.wine/drive_c/Program Files/Maxis/The Sims/");
                Environment.Exit(1);
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var name = args.Name;
                if (name.StartsWith("FSO.Scripts"))
                {
                    return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == name);
                }
                else
                {
                    var assemblyName = args.Name.Substring(0, name.IndexOf(',')) + ".dll";
                    // Use absolute path based on application base directory
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var assemblyPath = Path.Combine(baseDir, MonogameLinker.AssemblyDir, assemblyName);
                    
                    if (File.Exists(assemblyPath))
                    {
                        var assembly = Assembly.LoadFrom(assemblyPath);
                        return assembly;
                    }
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject;
            if (exception is OutOfMemoryException)
            {
                Console.Error.WriteLine("=== FATAL ERROR ===");
                Console.Error.WriteLine("Out of Memory! Simitone needs to close.");
                Console.Error.WriteLine(e.ExceptionObject.ToString());
            }
            else
            {
                Console.Error.WriteLine("=== FATAL ERROR ===");
                Console.Error.WriteLine("A fatal error occurred! Please report this issue on GitHub or Discord.");
                Console.Error.WriteLine(e.ExceptionObject.ToString());
            }
        }

        public static Tuple<byte[], int, int> BitmapReader(Stream str)
        {
            // Use ImageSharp for cross-platform image loading
            // System.Drawing.Common has issues with libgdiplus on Linux
            using (var image = Image.Load<Rgba32>(str))
            {
                var data = new byte[image.Width * image.Height * 4];
                image.CopyPixelDataTo(data);
                
                // Swap R and B channels (RGBA -> BGRA) to match Windows behavior
                // The Windows version uses an RGB->BGR color matrix transformation
                for (int i = 0; i < data.Length; i += 4)
                {
                    byte temp = data[i];       // R
                    data[i] = data[i + 2];     // R = B
                    data[i + 2] = temp;        // B = R
                }
                
                return new Tuple<byte[], int, int>(data, image.Width, image.Height);
            }
        }
    }
}
