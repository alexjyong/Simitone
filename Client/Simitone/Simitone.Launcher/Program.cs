using System.Diagnostics;
using System.Runtime.InteropServices;

// Get the directory where this launcher executable is located
string? exePath = Environment.ProcessPath;
if (string.IsNullOrEmpty(exePath))
{
    Console.Error.WriteLine("Error: Could not determine launcher location.");
    Environment.Exit(1);
}

string baseDir = Path.GetDirectoryName(exePath) ?? ".";
string libDir = Path.Combine(baseDir, "lib");

// Determine the target executable name based on platform
string targetExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Simitone.exe" : "Simitone";
string targetExePath = Path.Combine(libDir, targetExeName);

// Verify the target executable exists
if (!File.Exists(targetExePath))
{
    string message = $"Error: Cannot find Simitone executable.\nExpected location: {targetExePath}\n\nPlease ensure the 'lib' folder exists and contains the game files.";
    
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        // On Windows, show a message box since we're a WinExe (no console)
        ShowWindowsMessageBox(message, "Simitone Launcher Error");
    }
    else
    {
        Console.Error.WriteLine(message);
    }
    Environment.Exit(1);
}

// Set up the process to launch
var startInfo = new ProcessStartInfo(targetExePath)
{
    WorkingDirectory = libDir,
    UseShellExecute = false
};

// Forward all command-line arguments
foreach (var arg in args)
{
    startInfo.ArgumentList.Add(arg);
}

try
{
    using var process = Process.Start(startInfo);
    // Exit immediately - don't wait for the game to close
}
catch (Exception ex)
{
    string message = $"Error: Failed to launch Simitone.\n{ex.Message}";
    
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        ShowWindowsMessageBox(message, "Simitone Launcher Error");
    }
    else
    {
        Console.Error.WriteLine(message);
    }
    Environment.Exit(1);
}

// Windows MessageBox helper using P/Invoke
static void ShowWindowsMessageBox(string message, string title)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        MessageBox(IntPtr.Zero, message, title, 0x10); // MB_ICONERROR
    }
}

// P/Invoke declaration for Windows MessageBox
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
