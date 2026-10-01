using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 펜스 형상 뷰 — 도구줄(평면/입체 · 탐지 범위 · 전체 보기 · 줌) + 캔버스 + 종류별 수. 뷰모델은 결선 창의 것(<see cref="WiringViewModel"/>)을 그대로 쓴다.
/// </summary>
/// <remarks>
/// <b>입체가 기본 — 원격 데스크톱(Tier 0)에서도</b>(FR-10 개정 · 2026-09-30). 렌더 tier 는 로그로만 남긴다 —
/// <c>RenderCapability.Tier</c> 의 상위 워드가 0 이면 소프트웨어 렌더링(<c>GMaps.Ui/Utils/RenderTierProbe</c> 와 같은 <c>&gt;&gt; 16</c> 규칙).
/// 옛 "Tier 0 이면 자동 평면" 은 Viewport3D 비용을 피하려던 것인데 입체는 2D 비스듬 투영이라 비용이 같다.
/// </remarks>
public partial class FenceView : UserControl
{
    public FenceView()
    {
        InitializeComponent();
        // 한글을 띄어쓰기에서만 끊는다(커널 KoreanWordWrap — 결합자 U+2060). 전역 Install 은 앱이 한다 — 여기서는 이 글 하나만.
        foreach (var hint in LogicalDescendants<TextBlock>(this).Where(t => AutomationProperties.GetAutomationId(t) is "Devices.Wiring.Fence.Hint" or "Devices.Wiring.Fence.HelpText"))
            hint.Text = Ironwall.Dotnet.Libraries.Utils.Consoles.KoreanWordWrap.Join(hint.Text);
        Loaded += OnLoaded;
        Unloaded += (_, _) => RenderCapability.TierChanged -= OnTierChanged;
    }

    /// <summary>소프트웨어 렌더링(Tier 0)인가 — 순수 판정.</summary>
    public static bool IsSoftwareTier(int renderCapabilityTier) => renderCapabilityTier >> 16 == 0;

    private WiringViewModel? ViewModel => DataContext as WiringViewModel;

    internal FenceCanvas? Canvas => Descendants<FenceCanvas>(this).FirstOrDefault();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderCapability.TierChanged -= OnTierChanged;
        RenderCapability.TierChanged += OnTierChanged;
        OnTierChanged(this, EventArgs.Empty);
        if (Canvas is { } canvas)
        {
            canvas.ViewChanged -= OnViewChanged;
            canvas.ViewChanged += OnViewChanged;
            OnViewChanged(canvas, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 렌더 tier 는 <b>기록만</b> 한다 — 원격 데스크톱(Tier 0)에서도 입체가 기본이다(2026-09-30 결정 · FR-10 개정).
    /// 입체는 2D 비스듬 투영이라 Tier 0 에서도 비용이 같다.
    /// </summary>
    private void OnTierChanged(object? sender, EventArgs e)
    {
        var software = IsSoftwareTier(RenderCapability.Tier);
        System.Diagnostics.Trace.WriteLine($"[FenceView] render tier {RenderCapability.Tier >> 16} · software={software} — 입체 기본 유지(평면은 사람이 고를 때만)");
        if (ViewModel is { } vm) vm.IsSoftwareRendering = software;
    }

    private void OnViewChanged(object? sender, EventArgs e)
    {
        if (sender is not FenceCanvas canvas) return;
        var zoom = Descendants<TextBlock>(this).FirstOrDefault(t => AutomationProperties.GetAutomationId(t) == "Devices.Wiring.Fence.ZoomText");
        if (zoom is not null) zoom.Text = (canvas.Scale * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    private void OnFlat(object sender, RoutedEventArgs e) => ViewModel?.ChooseFlat();
    private void OnTilt(object sender, RoutedEventArgs e) => ViewModel?.ChooseTilt();
    private void OnRange(object sender, RoutedEventArgs e) => ViewModel?.ToggleRange();
    private void OnCables(object sender, RoutedEventArgs e) => ViewModel?.ToggleCables();
    private void OnFit(object sender, RoutedEventArgs e) => Canvas?.Fit();
    private void OnZoomIn(object sender, RoutedEventArgs e) => Canvas?.ZoomIn();
    private void OnZoomOut(object sender, RoutedEventArgs e) => Canvas?.ZoomOut();

    private static System.Collections.Generic.IEnumerable<T> LogicalDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T hit) yield return hit;
            foreach (var deeper in LogicalDescendants<T>(child)) yield return deeper;
        }
    }

    private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }
}
