using System.Runtime.CompilerServices;

namespace DummyCameras.Infra;

/// <summary>
/// Where the tool lives on disk. The executable is usually built to a scratch OutDir, so the source tree
/// (tools/dummy-cameras) is located from the compile-time path of this file, overridable by DUMMYCAM_ROOT.
/// </summary>
public static class ToolPaths
{
    public static string ToolRoot { get; } = ResolveRoot();

    /// <summary>Gitignored folder for downloaded binaries (tools/dummy-cameras/bin).</summary>
    public static string BinDir => Path.Combine(ToolRoot, "bin");

    private static string ResolveRoot([CallerFilePath] string thisFile = "")
    {
        var env = Environment.GetEnvironmentVariable("DUMMYCAM_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env)) return Path.GetFullPath(env);
        // src/Infra/ToolPaths.cs → tools/dummy-cameras
        var dir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)));
        if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "DummyCameras.csproj"))) return dir;
        return AppContext.BaseDirectory;
    }

    /// <summary>First executable with this name on PATH (Windows adds .exe).</summary>
    public static string? FindOnPath(string exe)
    {
        var names = OperatingSystem.IsWindows() && !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? new[] { exe + ".exe", exe }
            : new[] { exe };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            foreach (var n in names)
            {
                try
                {
                    var p = Path.Combine(dir.Trim('"'), n);
                    if (File.Exists(p)) return p;
                }
                catch (ArgumentException) { /* malformed PATH entry */ }
            }
        }
        return null;
    }
}
