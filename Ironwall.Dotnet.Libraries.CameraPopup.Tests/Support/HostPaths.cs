namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;

/// <summary>호스트 exe 찾기: 환경 변수 → 시험 폴더 → 저장소의 Host bin(같은 구성).</summary>
internal static class HostPaths
{
    public const string ExeName = "Ironwall.CameraPopupHost.exe";

    public static string? FindHostExe()
    {
        var env = Environment.GetEnvironmentVariable("IRONWALL_CAMHOST_EXE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

        var local = Path.Combine(AppContext.BaseDirectory, ExeName);
        if (File.Exists(local)) return local;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.CameraPopup.Host")))
            dir = dir.Parent;
        if (dir is null) return null;
        var bin = Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.CameraPopup.Host", "bin");
        if (!Directory.Exists(bin)) return null;
        return Directory.EnumerateFiles(bin, ExeName, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static string RequireHostExe()
        => FindHostExe() ?? throw new InvalidOperationException($"{ExeName} 를 찾지 못했다 — Host 프로젝트를 먼저 빌드하거나 IRONWALL_CAMHOST_EXE 를 지정");

    public static string NewLogDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ironwall-camhost-tests", DateTime.Now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
