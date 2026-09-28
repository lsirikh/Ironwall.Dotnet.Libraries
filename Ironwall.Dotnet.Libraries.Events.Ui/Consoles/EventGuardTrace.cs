using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles;

/// <summary>
/// 이벤트 콘솔 미적용 관문 진단 기록 — 환경변수 <c>IRONWALL_EVENT_GUARD_TRACE</c> 에 파일 경로가 있을 때만 켜진다(평소에는 아무것도 안 한다).
/// </summary>
/// <remarks>
/// 실창에서만 나는 '고친 칸 유실'(헤디드 SC-EVT-028)을 증거로 좇기 위한 것이다 — 관문 판정 · 그리드 선택 변경 · 되돌림 ·
/// 다시 읽기 짝 찾기 · 바쁨 끝(어느 목록) · 레일 전환 · 손댄 칸 수 변화(0 이 되는 순간의 호출 사슬)를 한 줄씩 남긴다.
/// <c>Utils.Behaviors.Drag.DragTrace</c> 와 같은 모양. 호출 스레드: UI.
/// </remarks>
public static class EventGuardTrace
{
    private static readonly string? s_path = ReadPath();

    public static bool IsOn => s_path != null;

    private static string? ReadPath()
    {
        var value = Environment.GetEnvironmentVariable("IRONWALL_EVENT_GUARD_TRACE");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static void Write(string message)
    {
        if (s_path == null) return;
        try
        {
            File.AppendAllText(s_path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>행 목록 한 줄 — "형식#id" 들.</summary>
    public static string Rows(IEnumerable? rows)
    {
        if (rows == null) return "[]";
        return "[" + string.Join(",", rows.Cast<object?>().Select(r => r == null ? "null"
            : $"{r.GetType().Name.Replace("EventViewModel", string.Empty)}#{EventSelectionTwin.IdOf(r)?.ToString() ?? "?"}@{r.GetHashCode():x}")) + "]";
    }

    /// <summary>지금 호출 사슬 가운데 우리 코드만(최대 <paramref name="depth"/>개) — 누가 손댄 칸을 비웠는지 가린다.</summary>
    public static string Caller(int depth = 8)
    {
        var frames = new StackTrace(2, false).GetFrames() ?? Array.Empty<StackFrame>();
        return string.Join(" < ", frames
            .Select(f => f.GetMethod())
            .Where(m => m?.DeclaringType?.Namespace?.StartsWith("Ironwall", StringComparison.Ordinal) == true)
            .Take(depth)
            .Select(m => $"{m!.DeclaringType!.Name}.{m.Name}"));
    }
}
