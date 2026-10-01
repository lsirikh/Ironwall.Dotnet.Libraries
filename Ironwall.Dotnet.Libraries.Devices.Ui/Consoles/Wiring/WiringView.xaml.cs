using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 장비 셋업 · 결선맵의 뷰. 뷰모델은 창도 클립보드도 모르고, 여기서는 <b>전달만</b> 한다.
/// </summary>
/// <remarks>
/// <para>키보드 폴백이 여기 있다 — 드래그 전용 UI 를 만들지 않는다(자동화가 좌표 클릭에 묶인다).
/// <c>Alt+←/→</c> 는 WPF 에서 <see cref="Key.System"/> 으로 도착하고 실제 키는 <see cref="KeyEventArgs.SystemKey"/> 에 있다 —
/// <c>e.Key == Key.Left</c> 로 짠 폴백은 영원히 뜨지 않는다.</para>
/// </remarks>
public partial class WiringView : UserControl
{
    public WiringView()
    {
        InitializeComponent();
    }

    private WiringViewModel? ViewModel => DataContext as WiringViewModel;

    #region - Head · steps -
    private void OnGoSensors(object sender, RoutedEventArgs e) => ViewModel?.GoSensors();
    private void OnGoWiring(object sender, RoutedEventArgs e) => ViewModel?.GoWiring();
    private void OnUndo(object sender, RoutedEventArgs e) => ViewModel?.Undo();
    private void OnAutoLayout(object sender, RoutedEventArgs e) => ViewModel?.AutoLayout();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.SaveAsync();
    }
    #endregion

    #region - Sensor table -
    private void OnAddRow(object sender, RoutedEventArgs e) => ViewModel?.AddOneRow();

    private async void OnMakeSensors(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.MakeSensorsAsync();
    }

    private async void OnPaste(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.PasteAsync();
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        ViewModel?.OnSelectionChanged(grid.SelectedItems.OfType<SensorRowViewModel>().ToList());
    }

    private void OnApplyEdit(object sender, RoutedEventArgs e) => ViewModel?.ApplyEdit();
    private void OnCancelEdit(object sender, RoutedEventArgs e) => ViewModel?.CancelEdit();
    private void OnUnifyFirst(object sender, RoutedEventArgs e) => ViewModel?.UnifyWithFirst();

    private void OnToggleGroup(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WiringGroupCheckViewModel group) ViewModel?.ToggleGroup(group);
    }

    private async void OnFillSequential(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.FillSequentialAsync();
    }

    private async void OnNameRule(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.ApplyNameRuleAsync();
    }
    #endregion

    #region - Wiring map -
    private void OnAcceptSuggestion(object sender, RoutedEventArgs e) => ViewModel?.AcceptSuggestion();
    private void OnUnplaceSelected(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (vm.IsFenceView) vm.UnplaceFenceSelected();      // 펜스 보기에서는 펜스에서 고른 센서
        else vm.UnplaceSelected();
    }

    #region - Fence view (wiring-fence-view F-3) -
    private void OnShowFence(object sender, RoutedEventArgs e) => ViewModel?.ShowFenceView();
    private void OnShowTable(object sender, RoutedEventArgs e) => ViewModel?.ShowTableView();
    private void OnFacingFront(object sender, RoutedEventArgs e) => ViewModel?.FenceSetFacingFront();
    private void OnFacingBack(object sender, RoutedEventArgs e) => ViewModel?.FenceSetFacingBack();
    private void OnFenceBack(object sender, RoutedEventArgs e) => ViewModel?.StepSelectedBack();
    private void OnFenceForward(object sender, RoutedEventArgs e) => ViewModel?.StepSelectedForward();
    private void OnFenceUnplace(object sender, RoutedEventArgs e) => ViewModel?.UnplaceFenceSelected();
    private void OnFenceAppend(object sender, RoutedEventArgs e) => ViewModel?.AppendFenceSelected();
    private void OnEnclosureBack(object sender, RoutedEventArgs e) => ViewModel?.MoveEnclosureBack();
    private void OnEnclosureForward(object sender, RoutedEventArgs e) => ViewModel?.MoveEnclosureForward();
    #endregion

    #region - Fence editor pane (fence-wiring-editor FR-03 · FR-07 · FR-10) -
    private static string? TagOf(object sender) => (sender as FrameworkElement)?.Tag as string;

    private void OnPanelStyle(object sender, RoutedEventArgs e)
    {
        if (Enum.TryParse<Ironwall.Dotnet.Libraries.Enums.EnumFenceStyle>(TagOf(sender), out var style)) ViewModel?.ChoosePanelStyle(style);
    }

    private void OnPanelColor(object sender, RoutedEventArgs e) => ViewModel?.ChoosePanelColor(string.IsNullOrEmpty(TagOf(sender)) ? null : TagOf(sender));

    private async void OnPanelColorCustom(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) await vm.AskPanelColorAsync();
    }

    private void OnPanelSpan(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(TagOf(sender), NumberStyles.Float, CultureInfo.InvariantCulture, out var metres)) ViewModel?.ChoosePanelSpan(metres);
    }

    private void OnApplyPanelEdit(object sender, RoutedEventArgs e) => ViewModel?.ApplyPanelEdit();

    private void OnMountSpot(object sender, RoutedEventArgs e)
    {
        if (Enum.TryParse<Ironwall.Dotnet.Monitoring.Models.Fences.FenceMountSpot>(TagOf(sender), out var spot)) ViewModel?.ChooseMountSpot(spot);
    }

    private void OnMountOffsetKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel is not { } vm) return;
        vm.ApplyMountOffset();
        e.Handled = true;
    }

    private void OnMountOffsetLostFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel?.ApplyMountOffset();

    private void OnMountPanelPrev(object sender, RoutedEventArgs e) => ViewModel?.FenceMoveSelectedToPreviousPanel();
    private void OnMountPanelNext(object sender, RoutedEventArgs e) => ViewModel?.FenceMoveSelectedToNextPanel();

    private void OnMountPanelKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel is not { } vm) return;
        vm.ApplyMountPanel();
        e.Handled = true;
    }

    private void OnMountPanelLostFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel?.ApplyMountPanel();

    private void OnChooseBands(object sender, RoutedEventArgs e) => ViewModel?.ShowBandPicker();

    private void OnBandPreset(object sender, RoutedEventArgs e)
    {
        if (TagOf(sender) is { } preset) ViewModel?.ChooseBandPreset(preset);
    }

    private void OnBandClear(object sender, RoutedEventArgs e) => ViewModel?.ClearBands();
    private void OnBandCustom(object sender, RoutedEventArgs e) => ViewModel?.ApplyCustomBands();
    #endregion

    private void OnRemoveSlot(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WiringSlotViewModel slot) ViewModel?.Unplace(slot);
    }

    /// <summary>고른 칸은 한 선에서만 — 키보드 명령이 어느 칸을 가리키는지 흔들리지 않게.</summary>
    private void OnSlotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list || ViewModel is not { } vm) return;

        foreach (var slot in e.RemovedItems.OfType<WiringSlotViewModel>()) slot.IsSelected = false;
        foreach (var slot in e.AddedItems.OfType<WiringSlotViewModel>()) slot.IsSelected = true;

        // 선택 하나로(fence-wiring-editor FR-13) — 표에서 고른 줄이 펜스 · 개념도의 선택이 된다.
        vm.SelectFromTable(list.SelectedItems.OfType<WiringSlotViewModel>().Where(s => s.Row is not null).Select(s => s.Row!.Key).ToList());

        if (e.AddedItems.Count == 0) return;

        var other = ReferenceEquals(list.ItemsSource, vm.Line1) ? vm.Line2 : vm.Line1;
        foreach (var slot in other) slot.IsSelected = false;
    }

    /// <summary>선 위의 키보드 폴백 — Alt+←/→ 는 <see cref="Key.System"/> 으로 온다.</summary>
    private void OnLinePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (HandleLineKey(vm, e.Key, e.SystemKey, Keyboard.Modifiers)) e.Handled = true;
    }

    /// <summary>
    /// 선 목록의 키 판정 — 처리했으면 <c>true</c>(시험이 이 길로 뷰의 판정을 부른다 · F-2b L4).
    /// 모든 제어기가 링(v0.4)이라 선이 하나 — Alt+↑/↓(옛 "다른 가지로")는 흘려보낸다.
    /// </summary>
    internal static bool HandleLineKey(WiringViewModel vm, Key key, Key systemKey, ModifierKeys modifiers = ModifierKeys.None)
    {
        if (key == Key.System && systemKey is Key.Left) { vm.MoveSelectedBack(); return true; }
        if (key == Key.System && systemKey is Key.Right) { vm.MoveSelectedForward(); return true; }

        // F = 보는 쪽 뒤집기(FR-20) — 펜스 보기와 같은 키. 수식 키가 없을 때만(Ctrl+F 등은 흘려보낸다).
        if (key == Key.F && modifiers == ModifierKeys.None) { vm.FlipSelectedSlotFacing(); return true; }

        if (key == Key.Delete) { vm.UnplaceSelected(); return true; }
        return false;
    }

    /// <summary>팔레트의 키보드 폴백 — Enter = 결선 끝에 붙이기.</summary>
    private void OnPalettePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel is not { } vm) return;
        if (sender is not ListBox list) return;

        vm.PlaceManyFromPalette(list.SelectedItems.OfType<SensorRowViewModel>().ToList());
        e.Handled = true;
    }
    #endregion
}

/// <summary>검증 한 줄의 심각도 → 색. 색 <b>하나로</b> 뜻을 전하지 않는다(글자 표지가 함께 간다).</summary>
public sealed class IssueLevelBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value as WiringIssueLevel? switch
        {
            WiringIssueLevel.Critical => "StatusCriticalBrush",
            WiringIssueLevel.Warning => "StatusWarningBrush",
            _ => "StatusInfoBrush",
        };

        // 매번 다시 찾는다 — 한 번 찾아 캐시하면 테마를 바꿔도 옛 색으로 굳는다.
        return Application.Current?.TryFindResource(key) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>붙여넣기 열의 뜻 → 사람 말(W7).</summary>
public sealed class PasteColumnTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value as PasteColumn? switch
        {
            PasteColumn.Number => "번호",
            PasteColumn.Name => "이름",
            PasteColumn.Type => "종류",
            PasteColumn.Zone => "구역",
            _ => "읽지 않음",
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>심각도 → 글자 표지(형태로 구분).</summary>
public sealed class IssueLevelGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value as WiringIssueLevel? switch
        {
            WiringIssueLevel.Critical => "✕",
            WiringIssueLevel.Warning => "⚠",
            _ => "ℹ",
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// 센서 종류 코드 → "한국어 (코드)" — 목록 렌더 전용(device-console enum-korean-consistency).
/// </summary>
/// <remarks>
/// <b>왜 <c>Text</c> 바인딩은 그대로 두는가</b> — 종류 콤보는 <c>IsEditable="True"</c> +
/// <c>Text="{Binding TypeText}"</c> 로 서버에 보낼 원문 코드를 직접 나른다(빈 값을 지어내지 않고
/// 카탈로그 밖 값도 그대로 보내 422 로 진단하는 설계, <c>WiringLauncher.SensorTypeCodes</c> remarks 참조).
/// 편집형 <see cref="ComboBox"/> 는 항목이 <see cref="string"/> 이면 선택 시 <c>Text</c> 에 그 문자열을
/// 그대로 채운다 — <c>ItemTemplate</c> 은 드롭다운의 <b>겉보기 렌더</b>만 바꿀 뿐 항목 자체(=Text 로 들어갈 값)는
/// 여전히 원문 코드이므로, 이 컨버터를 <c>ItemTemplate</c>·읽기전용 표시에만 물리면 wire value 는 바뀌지 않는다.
/// </remarks>
public sealed class SensorTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string code
            ? Ironwall.Dotnet.Libraries.Devices.Ui.Helpers.DeviceEnumDisplay.SensorTypeBilingual(code)
            : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("표시 전용 — Text 바인딩은 원문 코드를 그대로 쓴다.");
}

/// <summary>
/// 아이콘 이름 → <see cref="MaterialDesignThemes.Wpf.PackIconKind"/>. XAML 문자열-enum 은 판본에 없는 이름이면 조용히 깨지므로
/// 여기서 한 번 고르고 없으면 레포가 이미 쓰는 <c>Radar</c> 로 둔다(표 보기 종류 열).
/// </summary>
public sealed class PackIconKindNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Enum.TryParse<MaterialDesignThemes.Wpf.PackIconKind>(value as string, ignoreCase: false, out var kind)
            ? kind
            : MaterialDesignThemes.Wpf.PackIconKind.Radar;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
