namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// <c>%LocalAppData%</c> 경로 — 프로세스에서 한 번만 구한다.
/// </summary>
/// <remarks>
/// <para><see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> 는 셸(SHGetKnownFolderPath)을 부른다.
/// 시험 프로세스에서 여러 스레드가 이것을 동시에 처음 부르면 셸 안에서 교착해 멈춘다(2026-10-01 덤프 2회 실측 — 장비 시험 31분 정지).
/// 그래서 환경변수 <c>LOCALAPPDATA</c> 를 먼저 쓰고(셸 호출 없음), 없거나 이상할 때만 셸에 묻는다 — 그것도 <see cref="Lazy{T}"/> 로 한 스레드만.</para>
/// </remarks>
public static class LocalAppDataFolder
{
    private static readonly Lazy<string> _path = new(Resolve, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>현재 사용자의 로컬 앱 데이터 폴더.</summary>
    public static string Path => _path.Value;

    private static string Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && System.IO.Path.IsPathFullyQualified(fromEnvironment))
            return fromEnvironment;
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }
}
