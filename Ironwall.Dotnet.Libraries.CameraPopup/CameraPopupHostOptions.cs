using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>팝업 호스트 감시 설정. 기본값이 PRD FR-25 값이다.</summary>
public sealed class CameraPopupHostOptions
{
    public const string DefaultExecutableName = "Ironwall.CameraPopupHost.exe";

    /// <summary>
    /// 설치 · 출력 폴더 안 호스트 자리(T-02) — 호스트는 자기 의존성 · libvlc 폴더와 함께 하위 폴더에 산다
    /// (GIS 폴더의 같은 이름 DLL 과 판이 섞이지 않게).
    /// </summary>
    public const string DefaultRelativePath = @"CameraPopupHost\" + DefaultExecutableName;

    /// <summary>호스트 exe 경로. 상대 경로면 GIS 실행 폴더(<see cref="AppContext.BaseDirectory"/>) 기준.</summary>
    public string HostExecutablePath { get; set; } = DefaultRelativePath;

    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>이 시간 동안 호스트에게서 아무것도 못 받으면 멈춤으로 보고 강제 종료 · 재시작.</summary>
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>호스트 시작 후 파이프 연결 · 핸드셰이크 제한 시간.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>파이프 한 번 쓰기 제한 시간 — 넘으면 호스트가 막힌 것으로 본다.</summary>
    public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary><see cref="CrashWindow"/> 안에서 허용하는 재시작 수. 넘으면 Suspended.</summary>
    public int MaxRestartsInWindow { get; set; } = 3;

    public TimeSpan CrashWindow { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>메모리 한도 초과로 인한 계획된 재시작 허용 수(같은 창 안). 폭주 방지용 별도 예산.</summary>
    public int MaxPlannedRestartsInWindow { get; set; } = 10;

    /// <summary>호스트 자기 메모리 한도(MB).</summary>
    public int HostMemoryLimitMb { get; set; } = HostLaunchArguments.DefaultMemoryLimitMb;

    /// <summary>명령 대기열 상한(FR-28). 넘으면 오래된 것부터 버리고 로그.</summary>
    public int CommandQueueCapacity { get; set; } = 256;

    /// <summary>호스트 로그 폴더. 비우면 %LOCALAPPDATA%\Ironwall\CameraPopupHost\logs.</summary>
    public string? HostLogDirectory { get; set; }

    /// <summary>창을 띄우지 않는 헤드리스 호스트(시험용).</summary>
    public bool Headless { get; set; }

    /// <summary>디버그 고장 주입 명령 허용(시험용). 운영에서는 false.</summary>
    public bool EnableDebugCommands { get; set; }

    /// <summary>설정 경로를 절대 경로로(순수).</summary>
    /// <remarks>상대 경로가 없으면 실행 폴더 바로 아래 <see cref="DefaultExecutableName"/> 로 폴백한다
    /// (하나의 출력 폴더로 모아 빌드한 개발 · 시험 배치).</remarks>
    public string ResolveExecutablePath()
    {
        var path = string.IsNullOrWhiteSpace(HostExecutablePath) ? DefaultRelativePath : HostExecutablePath;
        if (Path.IsPathRooted(path)) return path;
        var full = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        if (File.Exists(full)) return full;
        var flat = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, DefaultExecutableName));
        return File.Exists(flat) ? flat : full;
    }
}
