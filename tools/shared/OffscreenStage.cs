using System.Windows;

namespace PreviewTools.Shared;

/// <summary>
/// 미리보기 창을 <b>보이지 않게</b> 띄운다 — 화면 밖(-20000, -20000)에 두고 활성화 · 작업 표시줄 표시를 끈다.
/// </summary>
/// <remarks>
/// <para>왜 Show() 는 그대로 하나: 창이 떠 있어야(HWND + 레이아웃 · 렌더 패스) 콘솔이 실제 폭으로 재고
/// 배치된다. 스냅숏은 RenderTargetBitmap 으로 시각 트리를 직접 그리므로 화면 위 픽셀이 필요 없다.</para>
/// <para>사용자가 이 PC 에서 일하는 중에 창이 튀어나오지 않게 한다(2026-09-26 요청). 팝업(드롭다운 등)은
/// 모니터 안쪽으로 밀려 나오므로, 팝업을 여는 스냅숏 모드는 이 보호 밖이다 — 그런 모드는 따로 적는다.</para>
/// </remarks>
public static class OffscreenStage
{
    public const double Offset = -20000;

    public static T Hide<T>(T window) where T : Window
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = Offset;
        window.Top = Offset;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        return window;
    }

    /// <summary>
    /// <c>--surface 1120x700</c> — 실제 앱의 표면(틀 안쪽) 크기 그대로 콘솔을 못 박는다. 창 크기와 상관없이
    /// 뷰가 그 크기로 재고 배치되므로(서랍/도킹 판정 · 열 폭 · 툴바 예산) 실창 캡처와 같은 조건이 된다.
    /// </summary>
    public static bool ApplySurface(string[] args, FrameworkElement view, Window window)
    {
        var at = System.Array.IndexOf(args, "--surface");
        if (at < 0 || at + 1 >= args.Length) return false;

        var parts = args[at + 1].Split('x', 'X');
        if (parts.Length != 2
            || !double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width)
            || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height))
            return false;

        view.Width = width;
        view.Height = height;
        view.HorizontalAlignment = HorizontalAlignment.Left;
        view.VerticalAlignment = VerticalAlignment.Top;
        window.Width = width + 80;
        window.Height = height + 100;
        return true;
    }
}
