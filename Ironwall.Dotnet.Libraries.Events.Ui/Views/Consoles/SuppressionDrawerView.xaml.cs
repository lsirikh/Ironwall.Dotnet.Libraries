using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Consoles;

/// <summary>
/// 억제 편집 서랍 — 같은 창 안 780 오버레이(정본 SB L738-742 · L1028 · L1122 E-D7).
/// </summary>
/// <remarks>
/// 뷰는 <b>배선만</b> 한다. 닫아도 되는지 · 담아도 되는지 · 보내도 되는지는 전부 뷰모델과 순수 함수가 정한다.
/// </remarks>
public partial class SuppressionDrawerView : UserControl
{
    /// <summary>바닥 막대가 한 번 흔들리는 시간(ms) — 커널 상세 칸과 같은 어휘.</summary>
    private const int ShakeMilliseconds = 220;

    private SuppressionDrawerViewModel? _bound;
    private FrameworkElement? _footer;
    private int _lastShakeToken;

    public SuppressionDrawerView()
    {
        InitializeComponent();
        // 배선 탐색은 Loaded 에서 — OnAttached · 생성자 시점에는 템플릿이 아직 없다.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private SuppressionDrawerViewModel? Model => DataContext as SuppressionDrawerViewModel;

    #region - Wiring -

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _footer = FindByAutomationId(this, "Events.SuppressionSchedule.DrawerFooter");
        Hook(Model);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Hook(null);

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => Hook(Model);

    /// <summary>구독은 짝으로만 건다 — 서랍은 콘솔과 함께 오래 산다.</summary>
    private void Hook(SuppressionDrawerViewModel? next)
    {
        if (ReferenceEquals(_bound, next)) return;
        if (_bound is not null) _bound.PropertyChanged -= OnModelPropertyChanged;
        _bound = next;
        if (_bound is not null)
        {
            _bound.PropertyChanged += OnModelPropertyChanged;
            _lastShakeToken = _bound.ShakeToken;
        }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_bound is null) return;
        // 빈 이름은 "전부 바뀌었다" 는 뜻이라 여기도 지나간다.
        if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(SuppressionDrawerViewModel.ShakeToken)) return;
        if (_bound.ShakeToken == _lastShakeToken) return;

        _lastShakeToken = _bound.ShakeToken;
        Shake();
    }

    private void Shake()
    {
        if (_footer?.RenderTransform is not TranslateTransform transform) return;

        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(ShakeMilliseconds) };
        foreach (var (at, to) in new[] { (0.0, 0.0), (0.2, -6.0), (0.4, 6.0), (0.6, -4.0), (0.8, 4.0), (1.0, 0.0) })
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(
                to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ShakeMilliseconds * at))));

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    #endregion

    #region - Input -

    /// <summary>
    /// ESC — 터널에서, <b>서랍이 열려 있을 때만</b> 소비한다.
    /// 무조건 소비하면 목록 쪽의 Esc 동작(선택 해제 등)이 조용히 죽는다.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Model is not { IsOpen: true } model) return;
        if (e.Key != Key.Escape) return;

        e.Handled = true;
        model.TryClose();
    }

    /// <summary>스크림 클릭 — 닫기를 '시도' 한다(미적용 변경이 있으면 막힌다).</summary>
    private void OnScrimClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Model?.TryClose();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Model?.TryClose();

    private void OnRevert(object sender, RoutedEventArgs e) => Model?.Revert();

    private void OnSave(object sender, RoutedEventArgs e) => _ = Model?.SaveAsync();

    /// <summary>[추가 ▶] — 드롭과 <b>같은 함수</b>를 부른다.</summary>
    private void OnAddSelected(object sender, RoutedEventArgs e) => AddPickerSelection();

    /// <summary>픽커에서 Enter — 드래그의 키보드 폴백.</summary>
    private void OnPickerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddPickerSelection();
    }

    private void AddPickerSelection()
    {
        if (Model is null) return;
        var picker = FindByAutomationId(this, "Events.SuppressionSchedule.GroupPickerComboBox") as ListBox;
        if (picker is null) return;

        var rows = picker.SelectedItems.Cast<object>().ToList();
        if (rows.Count == 0)
        {
            Model.StatusLine = "담을 대상을 먼저 고르세요.";
            return;
        }
        Model.AddSelected(rows);
    }

    /// <summary>칩 ✕.</summary>
    private void OnRemoveChip(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SuppressionTargetChip chip }) Model?.RemoveChip(chip);
    }

    /// <summary>트레이에서 Delete — 고른 칩을 뺀다.</summary>
    private void OnTrayKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        if (sender is not ListBox { SelectedItem: SuppressionTargetChip chip }) return;
        e.Handled = true;
        Model?.RemoveChip(chip);
    }

    private void OnClearChips(object sender, RoutedEventArgs e) => Model?.ClearChips();

    #endregion

    #region - Helpers -

    /// <summary>
    /// 자동화 식별자로 요소를 찾는다 — <c>x:Name</c> 은 Caliburn 바인딩 지시자라 쓰지 않는다.
    /// </summary>
    internal static FrameworkElement? FindByAutomationId(DependencyObject root, string automationId)
    {
        if (root is FrameworkElement self && (string?)self.GetValue(AutomationProperties.AutomationIdProperty) == automationId)
            return self;

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
            if (FindByAutomationId(VisualTreeHelper.GetChild(root, i), automationId) is { } found) return found;

        return null;
    }

    #endregion
}

/// <summary>
/// 서랍 폭 — 정본 CSS <c>width:min(780px,94%)</c>(SB L739)를 WPF 로 옮긴 것.
/// 창이 좁으면 양 옆 여백만 남기고 줄어든다.
/// </summary>
public sealed class DrawerWidthConverter : IValueConverter
{
    /// <summary>확정 폭.</summary>
    public const double Preferred = 780;

    /// <summary>좁을 때 남기는 왼쪽 여백 — 뒤의 목록이 '있다'는 것이 보여야 한다.</summary>
    public const double MinimumGutter = 24;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var available = value is double d && d > 0 ? d : Preferred + MinimumGutter;
        return Math.Max(280, Math.Min(Preferred, available - MinimumGutter));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("표시 전용입니다.");
}
