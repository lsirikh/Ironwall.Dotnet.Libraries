using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 타일 한 칸 뷰. PTZ 패드는 누르는 동안 연속 이동 · 떼거나 캡처를 잃으면 정지(FR-14, NFR-01).
/// 패드가 열린 타일에 포커스가 있으면 방향키(누르는 동안) · +/−(줌) · Esc(패드 닫기)도 된다.
/// 우클릭 메뉴는 열 때마다 새로 만든다 — 권한 · 제공자 · 크게 보기 상태를 그때그때 반영.
/// </summary>
internal partial class TileView : UserControl
{
    private TileViewModel? _subscribed;
    private bool _padMoving;
    private Key _padKey = Key.None;

    public TileView()
    {
        InitializeComponent();
        foreach (var button in new[] { PadUp, PadDown, PadLeft, PadRight, PadZoomIn, PadZoomOut })
        {
            button.PreviewMouseLeftButtonDown += OnPadButtonDown;
            button.PreviewMouseLeftButtonUp += OnPadButtonUp;
            button.LostMouseCapture += OnPadButtonLostCapture;
        }
        ContextMenu = new ContextMenu();
        ContextMenuOpening += OnContextMenuOpening;
        MouseLeftButtonDown += OnTileMouseDown;
        PreviewKeyDown += OnTileKeyDown;
        PreviewKeyUp += OnTileKeyUp;
        DataContextChanged += OnDataContextChanged;
    }

    internal TileViewModel? Tile => DataContext as TileViewModel;

    /// <summary>창 뷰가 끌기 시작을 판정할 때 쓴다(손잡이에서만 끌기 — handle-only).</summary>
    internal bool IsOnDragHandle(DependencyObject? source)
    {
        for (var d = source; d is not null && !ReferenceEquals(d, this); d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
        {
            if (ReferenceEquals(d, DragHandle)) return true;
        }
        return false;
    }

    private EventWindowViewModel? Owner
    {
        get
        {
            for (DependencyObject? d = this; d is not null; d = VisualTreeHelper.GetParent(d))
            {
                if (d is ItemsControl items && items.DataContext is EventWindowViewModel vm) return vm;
            }
            return null;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribed is not null) _subscribed.PropertyChanged -= OnTilePropertyChanged;
        _subscribed = Tile;
        if (_subscribed is not null) _subscribed.PropertyChanged += OnTilePropertyChanged;
        UpdateStateGlyph();
        if (_subscribed is not null)
        {
            string id = _subscribed.AutomationId;
            AutomationProperties.SetAutomationId(DragHandle, id + ".DragHandle");
        }
    }

    private void OnTilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TileViewModel.StreamState)) UpdateStateGlyph();
        if (e.PropertyName == nameof(TileViewModel.IsPadVisible) && Tile?.IsPadVisible == false) StopPadMove();
    }

    private void UpdateStateGlyph()
    {
        // 상태는 글자 + 모양(● 재생 · ○ 연결 중 · ‖ 멈춤 · ✕ 연결 안 됨) — 색만으로 구분하지 않는다.
        StateGlyph.Text = Tile?.StreamState switch
        {
            StreamState.Playing => "●",
            StreamState.Stalled => "‖",
            StreamState.Failed => "✕",
            _ => "○",
        };
    }

    private void OnTileMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Tile is not { IsCamera: true } tile) return;
        Owner?.Select(tile);
        Focus();
    }

    private void OnRetryClick(object sender, RoutedEventArgs e) => Tile?.RequestRetry();

    // ───────── PTZ 패드 ─────────

    private static (double Pan, double Tilt, double Zoom) Vector(string? tag) => tag switch
    {
        "up" => (0, 0.5, 0),
        "down" => (0, -0.5, 0),
        "left" => (-0.5, 0, 0),
        "right" => (0.5, 0, 0),
        "zoom-in" => (0, 0, 0.5),
        "zoom-out" => (0, 0, -0.5),
        _ => (0, 0, 0),
    };

    private void OnPadButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Tile is not { IsPtzEnabled: true } tile || sender is not FrameworkElement button) return;
        var (pan, tilt, zoom) = Vector(button.Tag as string);
        _padMoving = true;
        _ = tile.PtzMoveAsync(pan, tilt, zoom);
    }

    private void OnPadButtonUp(object sender, MouseButtonEventArgs e) => StopPadMove();

    private void OnPadButtonLostCapture(object sender, MouseEventArgs e) => StopPadMove();

    private void StopPadMove()
    {
        if (!_padMoving) return;
        _padMoving = false;
        _padKey = Key.None;
        if (Tile is { } tile) _ = tile.PtzStopAsync();
    }

    private void OnPadStopClick(object sender, RoutedEventArgs e)
    {
        _padMoving = false;
        if (Tile is { } tile) _ = tile.PtzStopAsync();
    }

    private void OnPadCloseClick(object sender, RoutedEventArgs e)
    {
        StopPadMove();
        Tile?.HidePad();
        Focus();
    }

    private static string? KeyTag(Key key) => key switch
    {
        Key.Up => "up",
        Key.Down => "down",
        Key.Left => "left",
        Key.Right => "right",
        Key.OemPlus or Key.Add => "zoom-in",
        Key.OemMinus or Key.Subtract => "zoom-out",
        _ => null,
    };

    private void OnTileKeyDown(object sender, KeyEventArgs e)
    {
        if (Tile is not { IsPadVisible: true } tile) return;
        if (e.Key == Key.Escape)
        {
            StopPadMove();
            tile.HidePad();
            e.Handled = true;
            return;
        }
        if (KeyTag(e.Key) is not { } tag) return;
        e.Handled = true;
        if (e.IsRepeat || _padKey == e.Key) return;
        _padKey = e.Key;
        _padMoving = true;
        var (pan, tilt, zoom) = Vector(tag);
        _ = tile.PtzMoveAsync(pan, tilt, zoom);
    }

    private void OnTileKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != _padKey) return;
        e.Handled = true;
        StopPadMove();
    }

    // ───────── 우클릭 메뉴(FR-14) ─────────

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var owner = Owner;
        if (Tile is not { IsCamera: true } tile || owner is null || ContextMenu is null)
        {
            e.Handled = true;
            return;
        }
        owner.Select(tile);
        BuildMenu(ContextMenu, tile, owner);
    }

    internal static void BuildMenu(ContextMenu menu, TileViewModel tile, EventWindowViewModel owner)
    {
        string id = tile.AutomationId + ".Menu";
        AutomationProperties.SetAutomationId(menu, id);
        menu.Items.Clear();
        string? reason = tile.PtzDisabledReason;

        var ptz = Item("PTZ 제어 (타일 위 패드)", id + ".Ptz", tile.IsPtzEnabled, reason, () => tile.TogglePad());
        ptz.IsChecked = tile.IsPadVisible;
        menu.Items.Add(ptz);

        var presets = Item("프리셋 이동", id + ".Presets", tile.IsPtzEnabled, reason, null);
        presets.Items.Add(new MenuItem { Header = "불러오는 중…", IsEnabled = false });
        presets.SubmenuOpened += async (_, args) =>
        {
            if (!ReferenceEquals(args.OriginalSource, presets)) return;
            try
            {
                await tile.LoadPresetsAsync();
                FillPresets(presets, tile, id);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                presets.Items.Clear();
                presets.Items.Add(new MenuItem { Header = "불러오기 실패", IsEnabled = false });
            }
        };
        menu.Items.Add(presets);

        menu.Items.Add(Item("복귀 프리셋", id + ".Home", tile.IsPtzEnabled, reason, () => _ = tile.GotoHomeAsync()));
        menu.Items.Add(new Separator());

        var enlarge = Item("이 카메라만 크게", id + ".Enlarge", true, null, () => owner.ToggleEnlarge(tile));
        enlarge.IsChecked = tile.IsEnlarged;
        menu.Items.Add(enlarge);
        menu.Items.Add(Item("이 타일 닫기", id + ".CloseTile", true, null, () => owner.CloseTile(tile)));

        if (reason is not null)
        {
            menu.Items.Add(new Separator());
            var info = new MenuItem { Header = $"{reason} → 위 3개 비활성", IsEnabled = false };
            AutomationProperties.SetAutomationId(info, id + ".PtzReason");
            menu.Items.Add(info);
        }
    }

    private static void FillPresets(MenuItem presets, TileViewModel tile, string id)
    {
        presets.Items.Clear();
        if (tile.PresetsStatus is { } status)
        {
            presets.Items.Add(new MenuItem { Header = status, IsEnabled = false });
            return;
        }
        foreach (var preset in tile.Presets)
        {
            var p = preset;
            presets.Items.Add(Item(p.Display, $"{id}.Preset.{p.Token}", true, null, () => _ = tile.GotoPresetAsync(p.Token)));
        }
    }

    private static MenuItem Item(string header, string automationId, bool enabled, string? disabledReason, Action? onClick)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        AutomationProperties.SetAutomationId(item, automationId);
        if (!enabled && disabledReason is not null) item.ToolTip = disabledReason;
        if (onClick is not null) item.Click += (_, _) => onClick();
        return item;
    }
}
