using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

/// <summary>
/// 드래그 진단 기록 — 환경변수 <c>IRONWALL_DRAG_TRACE</c> 에 파일 경로가 있을 때만 켜진다(평소에는 아무것도 하지 않는다).
/// </summary>
/// <remarks>
/// 실창에서만 나는 드롭 실패(헤드리스 시험은 통과)를 증거로 좇기 위한 것이다 — 누른 요소 · 커서 아래 요소 · 드롭존 · 취소 여부 ·
/// 캡처 주인을 한 줄씩 남긴다. 호출 스레드: UI.
/// </remarks>
public static class DragTrace
{
    private static readonly string? s_path = ReadPath();

    public static bool IsOn => s_path != null;

    private static string? ReadPath()
    {
        var value = Environment.GetEnvironmentVariable("IRONWALL_DRAG_TRACE");
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

    /// <summary>요소에서 위로 몇 단계의 타입 · 이름 사슬.</summary>
    public static string Chain(DependencyObject? start, int depth = 6)
    {
        if (start == null) return "(null)";
        var sb = new StringBuilder();
        var d = start;
        for (var i = 0; i < depth && d != null; i++)
        {
            if (i > 0) sb.Append(" < ");
            sb.Append(d.GetType().Name);
            if (d is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name)) sb.Append('#').Append(fe.Name);
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return sb.ToString();
    }

    public static string Captured() => Chain(Mouse.Captured as DependencyObject, 3);
}
