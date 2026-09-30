using System.Globalization;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// 호스트 실행 인자(순수 함수 — 만들기 · 해석). 감시자와 호스트가 같은 코드로 오간다.
/// <code>--parent-pid 1234 --pipe ironwall-camhost-1234-… --token … --memory-limit-mb 1536 [--headless] [--debug-commands] [--log-dir …]</code>
/// </summary>
public sealed class HostLaunchArguments
{
    public const int DefaultMemoryLimitMb = 1536;

    public int ParentProcessId { get; init; }
    public string PipeName { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public int MemoryLimitMb { get; init; } = DefaultMemoryLimitMb;

    /// <summary>창을 실제로 띄우지 않는다(헤드리스 시험). 이벤트 창 상태만 추적한다.</summary>
    public bool Headless { get; init; }

    /// <summary>디버그 명령(충돌 · 멈춤 주입)을 받는다. 운영에서는 끈다.</summary>
    public bool DebugCommands { get; init; }

    public string? LogDirectory { get; init; }

    /// <summary><see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/> 에 넣을 인자 목록.</summary>
    public IReadOnlyList<string> ToArgumentList()
    {
        var list = new List<string>
        {
            "--parent-pid", ParentProcessId.ToString(CultureInfo.InvariantCulture),
            "--pipe", PipeName,
            "--token", Token,
            "--memory-limit-mb", MemoryLimitMb.ToString(CultureInfo.InvariantCulture),
        };
        if (Headless) list.Add("--headless");
        if (DebugCommands) list.Add("--debug-commands");
        if (!string.IsNullOrWhiteSpace(LogDirectory))
        {
            list.Add("--log-dir");
            list.Add(LogDirectory!);
        }
        return list;
    }

    /// <summary>인자 해석 + 검증. 실패하면 false 와 사유.</summary>
    public static bool TryParse(IReadOnlyList<string> args, out HostLaunchArguments? result, out string? error)
    {
        result = null;
        error = null;
        int parentPid = 0, memoryMb = DefaultMemoryLimitMb;
        string? pipe = null, token = null, logDir = null;
        bool headless = false, debug = false;

        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            string? Next() => i + 1 < args.Count ? args[++i] : null;
            switch (a)
            {
                case "--parent-pid":
                    if (!int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parentPid)) { error = "--parent-pid 값 오류"; return false; }
                    break;
                case "--pipe": pipe = Next(); break;
                case "--token": token = Next(); break;
                case "--memory-limit-mb":
                    if (!int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out memoryMb)) { error = "--memory-limit-mb 값 오류"; return false; }
                    break;
                case "--headless": headless = true; break;
                case "--debug-commands": debug = true; break;
                case "--log-dir": logDir = Next(); break;
                default: error = $"알 수 없는 인자: {a}"; return false;
            }
        }

        if (parentPid <= 0) { error = "--parent-pid 필수"; return false; }
        if (!PipeNaming.IsValidToken(token)) { error = "--token 형식 오류"; return false; }
        if (!PipeNaming.Matches(pipe, parentPid, token)) { error = "--pipe 가 pid · 토큰 규칙과 다름"; return false; }
        if (memoryMb < 1) { error = "--memory-limit-mb 는 1 이상"; return false; }

        result = new HostLaunchArguments
        {
            ParentProcessId = parentPid,
            PipeName = pipe!,
            Token = token!,
            MemoryLimitMb = memoryMb,
            Headless = headless,
            DebugCommands = debug,
            LogDirectory = logDir,
        };
        return true;
    }
}
