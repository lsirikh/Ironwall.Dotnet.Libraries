using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Ptz;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 타일 한 칸 뷰. PTZ 패드는 <b>누르는 동안만</b> 연속 이동 — 뗌 · 캡처 잃음 · 포커스 이탈 · 창 비활성 · 패드 닫힘 · 타일 닫힘이
/// 모두 단일 <see cref="FinishPress"/> 로 모여 정지를 한 번 보낸다(FR-14, NFR-01).
/// 패드가 열린 타일에 포커스가 있으면 방향키(누르는 동안) · +/−(줌) · Esc(패드 닫기)도 되고, 패드 단추에 포커스가 있으면
/// Space/Enter 도 누르는 동안 이동이다(자동 반복은 무시).
/// 영상 위 좌드래그 = 드래그 길이만큼 PTZ 상대 이동(떼는 순간 한 번) — 8 DIU 미만은 클릭(선택), Esc · 캡처 잃음은 취소.
/// 타일 순서 바꾸기는 손잡이에서만 시작하므로(창 뷰) 서로 부딪치지 않는다.
/// 우클릭 메뉴는 열 때마다 새로 만든다 — 권한 · 제공자 · 크게 보기 상태를 그때그때 반영.
/// </summary>
internal partial class TileView : UserControl
{
    private const double DragTargetSize = 28;

    private TileViewModel? _subscribed;
    private readonly PtzHoldPress _hold = new();
    private readonly PtzDragGesture _drag = new();
    private Rect _dragView;          // 눌렀을 때의 영상 사각형(타일 좌표)
    private Key _padKey = Key.None;
    private Window? _window;

    /// <summary>마우스 위치 읽기(기본 = 이벤트의 위치). 헤드리스 시험이 좌표를 넣을 수 있게 분리했다 — 화면에 없는 요소는 위치를 못 읽는다.</summary>
    internal Func<IInputElement, MouseEventArgs, Point> PointerPosition { get; set; } = (element, e) => e.GetPosition(element);

    public TileView()
    {
        InitializeComponent();
        foreach (var button in new[] { PadUp, PadDown, PadLeft, PadRight, PadZoomIn, PadZoomOut })
        {
            button.PreviewMouseLeftButtonDown += OnPadButtonDown;
            button.PreviewMouseLeftButtonUp += (_, _) => FinishPress();
            button.LostMouseCapture += (_, _) => FinishPress();
        }
        MouseMove += OnTileMouseMove;
        MouseLeftButtonUp += (_, _) => FinishDrag(commit: true);
        LostMouseCapture += (_, _) => FinishDrag(commit: false);
        IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (e.NewValue is false) FinishPress();   // 포커스가 타일을 떠나면 키 뗌을 못 받는다
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(window, _window)) return;
        if (_window is not null) _window.Deactivated -= OnWindowDeactivated;
        _window = window;
        if (_window is not null) _window.Deactivated += OnWindowDeactivated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 타일 · 창이 닫혔다 — 누르고 있던 이동을 멈추고 드래그는 버린다.
        FinishPress();
        FinishDrag(commit: false);
        if (_window is not null) _window.Deactivated -= OnWindowDeactivated;
        _window = null;
    }

    internal void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // 다른 창으로 넘어가면 뗌(마우스 · 키)이 이 창에 오지 않을 수 있다.
        FinishPress();
        FinishDrag(commit: false);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 다른 타일로 바뀌기 전에 — 이전 타일에 걸린 누름 · 드래그를 이전 타일 기준으로 끝낸다.
        FinishPress();
        FinishDrag(commit: false);
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
        if (e.PropertyName == nameof(TileViewModel.IsPadVisible) && Tile?.IsPadVisible == false) FinishPress();
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

        // 영상 위에서만 PTZ 드래그를 시작한다(패드 · 손잡이 · 단추 · 검은 여백 제외). 8 DIU 미만은 위 선택만 남는다.
        if (!ReferenceEquals(e.OriginalSource, VideoImage) || VideoImage.ActualWidth <= 0 || VideoImage.ActualHeight <= 0) return;
        var origin = VideoImage.TranslatePoint(new Point(0, 0), this);
        var view = new Rect(origin.X, origin.Y, VideoImage.ActualWidth, VideoImage.ActualHeight);
        var p = PointerPosition(this, e);
        if (!_drag.Press(p.X - view.X, p.Y - view.Y, view.Width, view.Height)) return;
        _dragView = view;
        CaptureMouse();   // 타일(UserControl)이 쥔다 — 단추 계열이 아니라 캡처를 빼앗기지 않는다
        e.Handled = true;
    }

    // ───────── 영상 위 드래그 PTZ ─────────

    private void OnTileMouseMove(object sender, MouseEventArgs e)
    {
        if (!_drag.IsPressed) return;
        var p = PointerPosition(this, e);
        if (!_drag.Move(p.X - _dragView.X, p.Y - _dragView.Y)) return;   // 데드존 안 — 아직 클릭
        if (Tile is not { IsPtzEnabled: true }) return;                   // 못 하는 타일은 표시 없이, 떼면 이유를 알린다
        DrawDragTarget();
    }

    /// <summary>끈 길이(선)와 떼면 화면 중심으로 올 점(과녁)을 그린다.</summary>
    private void DrawDragTarget()
    {
        double sx = _dragView.X + _drag.StartX, sy = _dragView.Y + _drag.StartY;
        double cx = _dragView.X + _drag.CurrentX, cy = _dragView.Y + _drag.CurrentY;
        foreach (var line in new[] { DragShaftHalo, DragShaft })
        {
            line.X1 = sx;
            line.Y1 = sy;
            line.X2 = cx;
            line.Y2 = cy;
        }
        var (tx, ty) = PtzDragGesture.TargetPoint(_dragView.Width, _dragView.Height, _drag.CurrentX - _drag.StartX, _drag.CurrentY - _drag.StartY);
        Canvas.SetLeft(DragTarget, _dragView.X + tx - (DragTargetSize / 2));
        Canvas.SetTop(DragTarget, _dragView.Y + ty - (DragTargetSize / 2));
        if (PtzDragOverlay.Visibility != Visibility.Visible)
        {
            PtzDragOverlay.Visibility = Visibility.Visible;
            Cursor = Cursors.Cross;   // 요소 로컬 커서(전역 OverrideCursor 금지) — FinishDrag 가 ClearValue 로 되돌린다
        }
    }

    /// <summary>
    /// 드래그 종료 단일 지점(뗌 · 캡처 잃음 · Esc · 창 비활성 · 타일 닫힘). 순서: ①상태 지움 ②시각 복원 ③(구독 없음) ④캡처 해제 ⑤확정 통지.
    /// 데드존 미만 · 취소면 아무것도 보내지 않는다.
    /// </summary>
    private void FinishDrag(bool commit)
    {
        if (!_drag.IsPressed) return;
        var vector = _drag.Finish(commit);

        PtzDragOverlay.Visibility = Visibility.Collapsed;
        ClearValue(CursorProperty);

        if (IsMouseCaptured) ReleaseMouseCapture();

        if (vector is not { } v || (_subscribed ?? Tile) is not { } tile) return;
        if (tile.IsPtzEnabled) _ = tile.PtzDragAsync(v.ViewX, v.ViewY, v.ViewAspect);
        else tile.NotePtzUnavailable();
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
        if (sender is FrameworkElement { Tag: string tag }) BeginPress(tag);
    }

    /// <summary>누름 시작 — 이동을 한 번 보낸다(키 자동 반복 · 같은 단추 재누름은 무시).</summary>
    private void BeginPress(string tag, bool isRepeat = false)
    {
        if (Tile is not { IsPtzEnabled: true } tile) return;
        var (pan, tilt, zoom) = Vector(tag);
        if (pan == 0 && tilt == 0 && zoom == 0) return;
        if (!_hold.Press(tag, isRepeat)) return;
        _ = tile.PtzMoveAsync(pan, tilt, zoom);
    }

    /// <summary>
    /// 누름 종료 단일 지점 — 뗌(마우스 · 키) · 캡처 잃음 · 포커스 이탈 · 창 비활성 · 패드 닫힘 · 타일/창 닫힘이 모두 여기로 온다.
    /// 눌려 있던 게 있을 때만 정지를 한 번 보낸다.
    /// </summary>
    private void FinishPress()
    {
        _padKey = Key.None;
        if (_hold.Finish() is null) return;
        if ((_subscribed ?? Tile) is { } tile) _ = tile.StopIfMovingAsync();
    }

    private void OnPadStopClick(object sender, RoutedEventArgs e)
    {
        _hold.Finish();
        _padKey = Key.None;
        if (Tile is { } tile) _ = tile.PtzStopAsync();   // ■ 는 눌린 게 없어도 항상 정지를 보낸다
    }

    private void OnPadCloseClick(object sender, RoutedEventArgs e)
    {
        FinishPress();
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

    /// <summary>포커스가 있는 방향 · 줌 단추의 표식(Space/Enter 누름 이동용). 정지 · 닫기 단추는 null(보통 클릭).</summary>
    private static string? FocusedPadTag(object? source)
        => source is Button { Tag: string tag } && tag is not ("stop" or "close") ? tag : null;

    private void OnTileKeyDown(object sender, KeyEventArgs e)
    {
        if (_drag.IsPressed && e.Key == Key.Escape)
        {
            // 드래그 중일 때만 소비 — 취소(이동을 보내지 않는다).
            FinishDrag(commit: false);
            e.Handled = true;
            return;
        }
        if (Tile is not { IsPadVisible: true } tile) return;
        if (e.Key == Key.Escape)
        {
            FinishPress();
            tile.HidePad();
            e.Handled = true;
            return;
        }
        string? tag = KeyTag(e.Key);
        if (tag is null && e.Key is Key.Space or Key.Enter) tag = FocusedPadTag(e.OriginalSource);
        if (tag is null) return;
        e.Handled = true;   // 단추의 기본 클릭(Space 누름 → 캡처)으로 넘기지 않는다
        if (e.IsRepeat || _padKey == e.Key) return;   // 자동 반복은 이동을 다시 보내지 않는다
        _padKey = e.Key;
        BeginPress(tag);
    }

    private void OnTileKeyUp(object sender, KeyEventArgs e)
    {
        if (_padKey == Key.None || e.Key != _padKey) return;
        e.Handled = true;
        FinishPress();
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
