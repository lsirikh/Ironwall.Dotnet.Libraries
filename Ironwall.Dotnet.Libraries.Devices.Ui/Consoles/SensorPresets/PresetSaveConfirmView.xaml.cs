using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>프리셋 저장 확인(W5).</summary>
public partial class PresetSaveConfirmView : UserControl
{
    private Window? _window;

    public PresetSaveConfirmView()
    {
        InitializeComponent();
        PreviewKeyDown += OnEnter;
        // 포커스가 창 뿌리에 있으면 키 사건이 이 뷰를 지나지 않는다 — 창에서도 먼저 받는다(터널 맨 앞)
        Loaded += (_, _) =>
        {
            if (Window.GetWindow(this) is not { } window || ReferenceEquals(window, _window)) return;
            if (_window is not null) _window.PreviewKeyDown -= OnEnter;
            _window = window;
            window.PreviewKeyDown += OnEnter;
        };
    }

    private PresetSaveConfirmViewModel? ViewModel => DataContext as PresetSaveConfirmViewModel;

    /// <summary>
    /// Enter = 포커스 단추(처음 포커스 = [취소]) · 포커스 단추가 없으면 [취소] — W5 계약 "Enter 실수 = 취소"(FR-12).
    /// 틀의 규칙(Enter = 주 동작)으로는 [취소]에 포커스가 있어도 [저장]이 나갔다 · 실창에서는 창이 닫히지도 않았다(실창 4회차 MNT-R28-453).
    /// </summary>
    internal void OnEnter(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter) return;
        e.Handled = true;
        if (Keyboard.FocusedElement is ButtonBase { IsEnabled: true } button && IsAncestorOf(button)) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        else OnCancel(this, e);
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.SaveAsync(); } catch { /* 창을 닫는 것 말고 할 일이 없다 */ }
    }

    private async void OnSaveAsNew(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.SaveAsNewAsync(); } catch { /* 같다 */ }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        try { if (ViewModel is { } vm) await vm.CancelAsync(); } catch { /* 같다 */ }
    }
}
