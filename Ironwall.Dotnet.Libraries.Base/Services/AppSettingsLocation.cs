namespace Ironwall.Dotnet.Libraries.Base.Services;

/// <summary>
/// 런타임에 쓰는 설정 파일(appsettings.json · login-prefs.json)의 위치를 한곳에서 정한다.
/// <para>정본은 실행 폴더(<see cref="AppContext.BaseDirectory"/>) 아래 <c>Config\</c> 다 — 설치 폴더 루트는 일반 사용자가 쓸 수 없게
/// 걷어 내므로(SSO 회신 2026-10-14 Q-3: 승인된 exe 옆 dll 바꿔치기 방지), 쓰는 파일은 <c>Config\</c> 에만 둔다.</para>
/// <para>이관: <c>Config\</c> 에 없고 루트에 옛 파일이 있으면 읽기는 옛 파일을 쓰고, 처음 쓸 때 <c>Config\</c> 로 복사한 뒤 거기에 쓴다.
/// 옛 파일은 지우지 않는다 — 새 설치기에서 루트는 쓰기 불가라 지울 수 없고, 아직 루트를 읽는 쪽이 있을 수 있다.</para>
/// </summary>
public static class AppSettingsLocation
{
    public const string ConfigFolderName = "Config";
    public const string AppSettingsFileName = "appsettings.json";
    public const string LoginPrefsFileName = "login-prefs.json";

    /// <summary>정본 경로 <c>{실행 폴더}\Config\appsettings.json</c>(있든 없든).</summary>
    public static string AppSettingsPath => GetConfigPath(AppContext.BaseDirectory, AppSettingsFileName);

    /// <summary>appsettings.json 을 읽을 경로(옛 루트 파일 폴백 포함).</summary>
    public static string AppSettingsReadPath => ResolveReadPath(AppContext.BaseDirectory, AppSettingsFileName);

    /// <summary>login-prefs.json 을 읽을 경로(옛 루트 파일 폴백 포함).</summary>
    public static string LoginPrefsReadPath => ResolveReadPath(AppContext.BaseDirectory, LoginPrefsFileName);

    /// <summary>appsettings.json 을 쓰기 전에 부른다 — 필요하면 이관하고 쓸 경로(<c>Config\</c>)를 돌려준다.</summary>
    public static string PrepareAppSettingsWrite() => PrepareWrite(AppContext.BaseDirectory, AppSettingsFileName);

    /// <summary>login-prefs.json 을 쓰기 전에 부른다 — 필요하면 이관하고 쓸 경로(<c>Config\</c>)를 돌려준다.</summary>
    public static string PrepareLoginPrefsWrite() => PrepareWrite(AppContext.BaseDirectory, LoginPrefsFileName);

    /// <summary><c>{baseDirectory}\Config\{fileName}</c>.</summary>
    public static string GetConfigPath(string baseDirectory, string fileName)
        => Path.Combine(baseDirectory, ConfigFolderName, fileName);

    /// <summary>
    /// 읽을 경로를 고른다 — <c>Config\</c> 에 있으면 그것, 없고 루트에 옛 파일이 있으면 옛 파일, 둘 다 없으면 <c>Config\</c> 경로.
    /// </summary>
    /// <param name="fileExists">파일 존재 판정(시험용 주입, 기본 <see cref="File.Exists"/>).</param>
    public static string ResolveReadPath(string baseDirectory, string fileName, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        var config = GetConfigPath(baseDirectory, fileName);
        if (fileExists(config)) return config;
        var legacy = Path.Combine(baseDirectory, fileName);
        return fileExists(legacy) ? legacy : config;
    }

    /// <summary>
    /// 쓰기 준비 — <c>Config\</c> 폴더를 만들고, <c>Config\</c> 에 파일이 없고 루트에 옛 파일이 있으면 복사(이관)한다. 쓸 경로를 돌려준다.
    /// </summary>
    public static string PrepareWrite(string baseDirectory, string fileName)
    {
        var config = GetConfigPath(baseDirectory, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        var legacy = Path.Combine(baseDirectory, fileName);
        if (!File.Exists(config) && File.Exists(legacy))
        {
            try { File.Copy(legacy, config, overwrite: false); }
            catch (IOException) when (File.Exists(config)) { }   // 동시에 다른 쪽이 먼저 이관했다
        }
        return config;
    }
}
