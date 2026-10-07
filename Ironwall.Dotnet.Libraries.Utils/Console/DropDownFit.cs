using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 콤보 드롭다운을 창(작업 영역) 안에 — <c>c:DropDownFit.IsEnabled="True"</c>. 창 맨 아래 콤보의 목록이 위로 펼쳐지고,
/// 위 · 아래 넓은 쪽에 들어가는 높이로 잘린다(항목이 늘면 스크롤). 판정은 <see cref="DropDownPlacement"/>(순수 함수).
/// </summary>
/// <remarks>
/// <para>템플릿의 <c>PART_Popup</c> 배치 콜백을 <b>감싼다</b> — 원래 콜백(MaterialDesign 의 ComboBoxPopup 은 여기서 위 · 아래 모양 템플릿을 고른다)을
/// 먼저 부르고, 그 자리가 경계를 넘을 때만 옮긴다. 원래 배치가 사용자 배치가 아니면 대상 바로 아래를 제안으로 쓴다.</para>
/// <para>높이는 열릴 때(<see cref="ComboBox.DropDownOpened"/>) 정한다 — 처음 값(<see cref="ComboBox.MaxDropDownHeight"/>)을 기억해 그보다 늘리지 않는다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public static class DropDownFit
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(DropDownFit), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value)
        => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(IsEnabledProperty, value);

    // 처음 MaxDropDownHeight — 자른 값이 다음 열림의 상한이 되지 않게.
    private static readonly DependencyProperty RequestedMaxProperty = DependencyProperty.RegisterAttached(
        "RequestedMax", typeof(double), typeof(DropDownFit), new PropertyMetadata(double.NaN));

    // 팝업에 건 우리 콜백 — 템플릿이 다시 붙거나 원래 콜백이 바뀌었는지 알아본다.
    private static readonly DependencyProperty WrapperProperty = DependencyProperty.RegisterAttached(
        "Wrapper", typeof(CustomPopupPlacementCallback), typeof(DropDownFit), new PropertyMetadata(null));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox combo) return;
        combo.Loaded -= OnLoaded;
        combo.DropDownOpened -= OnDropDownOpened;
        if (e.NewValue is not true) return;
        combo.Loaded += OnLoaded;
        combo.DropDownOpened += OnDropDownOpened;
        if (combo.IsLoaded) Wrap(combo);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Wrap((ComboBox)sender);

    private static void OnDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not ComboBox combo) return;
        Wrap(combo);

        var requested = (double)combo.GetValue(RequestedMaxProperty);
        if (double.IsNaN(requested))
        {
            requested = combo.MaxDropDownHeight;
            combo.SetValue(RequestedMaxProperty, requested);
        }
        var size = new Size(combo.ActualWidth, combo.ActualHeight);
        var (window, work) = PopupScreenBounds.For(combo, size);   // 대상 크기를 DIU 로 주면 경계도 DIU
        var max = DropDownPlacement.MaxHeight(new Rect(size), DropDownPlacement.Bounds(window, work), requested);
        combo.SetCurrentValue(ComboBox.MaxDropDownHeightProperty, max);
    }

    private static void Wrap(ComboBox combo)
    {
        combo.ApplyTemplate();
        if (combo.Template?.FindName("PART_Popup", combo) is not Popup popup) return;
        if (popup.GetValue(WrapperProperty) is CustomPopupPlacementCallback mine
            && popup.Placement == PlacementMode.Custom && ReferenceEquals(popup.CustomPopupPlacementCallback, mine)) return;

        var inner = popup.Placement == PlacementMode.Custom ? popup.CustomPopupPlacementCallback : null;
        CustomPopupPlacementCallback wrapper = (popupSize, targetSize, offset) => Place(combo, popup, inner, popupSize, targetSize, offset);
        popup.SetValue(WrapperProperty, wrapper);
        popup.CustomPopupPlacementCallback = wrapper;
        popup.Placement = PlacementMode.Custom;
    }

    private static CustomPopupPlacement[] Place(ComboBox combo, Popup popup, CustomPopupPlacementCallback? inner,
                                                Size popupSize, Size targetSize, Point offset)
    {
        var proposed = inner?.Invoke(popupSize, targetSize, offset) is { Length: > 0 } placements
            ? placements[0].Point
            : new Point(0, targetSize.Height);
        var target = popup.PlacementTarget as FrameworkElement ?? combo;
        var (window, work) = PopupScreenBounds.For(target, targetSize);
        var point = DropDownPlacement.Resolve(new Rect(targetSize), popupSize, proposed, DropDownPlacement.Bounds(window, work));
        return new[] { new CustomPopupPlacement(point, PopupPrimaryAxis.None) };
    }
}
