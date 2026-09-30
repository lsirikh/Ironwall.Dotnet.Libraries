using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 이벤트 창 뷰(FR-09~16). 배치는 물리 픽셀(SetWindowPos — 호스트는 모니터별 DPI 인지 v2).
/// 타일 끌기는 레포 관용구인 캡처 드래그(drag-first-ux): 손잡이에서만 시작 · 8 DIU 데드존(미만 = 클릭) ·
/// Esc/캡처 잃음 = 취소 · 단일 <see cref="FinishDrag"/> · 삽입 위치는 세로 막대 · 키보드 폴백 Alt+←/→.
/// 닫기는 한 길로만: ✕ · Alt+F4 → VM.RequestClose → 창 세션이 <see cref="CloseBySession"/>.
/// </summary>
internal partial class EventWindowView : Window
{
    private static readonly TimeSpan PlacementSettle = TimeSpan.FromSeconds(2);

    private readonly EventWindowViewModel _vm;
    private readonly PixelRect _target;
    private readonly DateTime _createdUtc = DateTime.UtcNow;
    private IntPtr _hwnd;
    private bool _closingBySession;

    // ── 끌기 상태 ──
    private bool _pressed;
    private bool _dragging;
    private Point _pressPoint;
    private TileViewModel? _dragTile;
    private int _candidateInsert = -1;

    public EventWindowView(EventWindowViewModel vm)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        InitializeComponent();
        DataContext = vm;

        var msg = vm.Message;
        _target = ResolveTarget(msg);
        Topmost = msg.AlwaysOnTop;
        WindowStartupLocation = WindowStartupLocation.Manual;
        // 핸들이 생기기 전 힌트(DIU). 실제 위치 · 크기는 SourceInitialized 에서 물리 픽셀로 맞춘다.
        Left = WindowPlacement.ToDiu(_target.X, msg.DpiScale);
        Top = WindowPlacement.ToDiu(_target.Y, msg.DpiScale);
        Width = WindowPlacement.ToDiu(_target.Width, msg.DpiScale);
        Height = WindowPlacement.ToDiu(_target.Height, msg.DpiScale);

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            WindowNative.Place(_hwnd, _target);
        };
        DpiChanged += OnDpiChanged;
        Closing += OnClosing;

        PreviewMouseDown += (_, _) => _vm.NoteInteraction();
        PreviewMouseWheel += (_, _) => _vm.NoteInteraction();
        PreviewKeyDown += OnPreviewKeyDown;

        TilesItems.PreviewMouseDown += OnTilesPreviewMouseDown;
        TilesHost.MouseMove += OnTilesMouseMove;
        TilesHost.MouseLeftButtonUp += (_, _) => FinishDrag(commit: true);
        TilesHost.LostMouseCapture += (_, _) => FinishDrag(commit: false);
    }

    /// <summary>오프스크린 렌더 시험용 — 창을 띄우지 않고 내용만 그린다.</summary>
    internal FrameworkElement RenderRoot => Frame;

    /// <summary>화면에 맞춘 최종 사각형(물리 픽셀). 대상 모니터가 사라졌으면 주 모니터 작업영역 안으로(FR-12 안전망).</summary>
    private static PixelRect ResolveTarget(OpenEventWindow msg)
    {
        PixelRect workArea;
        try
        {
            if (!WindowNative.TryGetWorkArea(msg.Window, out workArea) && workArea.IsEmpty) workArea = msg.MonitorWorkArea;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            workArea = msg.MonitorWorkArea;
        }
        return WindowPlacement.ClampToWorkArea(msg.Window, workArea);
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        // 다른 배율 모니터로 옮겨 놓자마자 WPF 가 DIU 크기를 지키려 창을 키우거나 줄인다 — 열린 직후에는 물리 크기를 되돌린다.
        if (DateTime.UtcNow - _createdUtc < PlacementSettle && _hwnd != IntPtr.Zero)
            Dispatcher.BeginInvoke(() => WindowNative.Place(_hwnd, _target));
    }

    // ───────── 머리: 창 이동 · 닫기 · 앞으로 ─────────

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        var before = WindowNative.GetBounds(_hwnd);
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return; // 버튼이 이미 떼어짐
        }
        var after = WindowNative.GetBounds(_hwnd);
        if (after is not null && (before is null || before.X != after.X || before.Y != after.Y))
            _vm.NotifyUserMoved(after.X, after.Y);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => _vm.RequestClose(EventWindowCloseReason.User);

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closingBySession) return;
        // Alt+F4 등 — 한 길(VM → 세션)로 돌린다.
        e.Cancel = true;
        _vm.RequestClose(EventWindowCloseReason.User);
    }

    /// <summary>창 세션만 부른다.</summary>
    public void CloseBySession()
    {
        _closingBySession = true;
        FinishDrag(commit: false);
        Close();
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        if (!Topmost)
        {
            // 포그라운드 잠금 때문에 Activate 만으로는 안 올라올 수 있다 — Z 순서만 잠깐 끌어올린다.
            Topmost = true;
            Topmost = false;
        }
        Activate();
    }

    // ───────── 키보드 ─────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        _vm.NoteInteraction();
        if (_pressed && e.Key == Key.Escape)
        {
            // 끌기 중일 때만 소비(ESC 취소 — 원위치).
            FinishDrag(commit: false);
            e.Handled = true;
            return;
        }
        // Alt+←/→ 는 Key.System 으로 도착하고 실제 키는 SystemKey 에 있다(drag-first-ux 실측).
        if (e.Key == Key.System && e.SystemKey is Key.Left or Key.Right && _vm.SelectedTile is { } selected)
        {
            _vm.MoveTileBy(selected, e.SystemKey == Key.Left ? -1 : +1);
            e.Handled = true;
        }
    }

    // ───────── 타일 끌기(캡처 드래그) ─────────

    private void OnTilesPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _pressed) return;
        var source = e.OriginalSource as DependencyObject;
        var tileView = FindAncestor<TileView>(source);
        if (tileView?.Tile is not { IsCamera: true } tile || !tileView.IsOnDragHandle(source)) return;

        // 터널 단계에서 선점 — Thumb 자체 캡처가 돌지 않게 하고, 캡처는 격자 겹이 쥔다.
        e.Handled = true;
        _pressed = true;
        _dragging = false;
        _dragTile = tile;
        _candidateInsert = -1;
        _pressPoint = e.GetPosition(TilesHost);
        TilesHost.CaptureMouse();
    }

    private void OnTilesMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _dragTile is null) return;
        var p = e.GetPosition(TilesHost);
        if (!_dragging)
        {
            if (!TileReorder.IsDrag(p.X - _pressPoint.X, p.Y - _pressPoint.Y)) return;
            _dragging = true;
            GhostText.Text = _dragTile.Name;
            Ghost.Visibility = Visibility.Visible;
            TilesHost.Cursor = Cursors.SizeAll;
        }
        Canvas.SetLeft(Ghost, p.X + 12);
        Canvas.SetTop(Ghost, p.Y + 8);
        UpdateCandidate(p);
    }

    /// <summary>후보 삽입 위치 — 바뀔 때만 삽입 막대를 옮긴다. 인덱스는 HitTest → ContainerFromElement → IndexOf.</summary>
    private void UpdateCandidate(Point inHost)
    {
        var cams = _vm.CameraTiles.ToList();
        int from = _dragTile is null ? -1 : cams.IndexOf(_dragTile);
        int insert = -1;
        FrameworkElement? container = null;
        bool before = true;

        var inItems = TilesHost.TranslatePoint(inHost, TilesItems);
        var hit = VisualTreeHelper.HitTest(TilesItems, inItems)?.VisualHit;
        if (hit is not null && TilesItems.ContainerFromElement(hit) is FrameworkElement c
            && TilesItems.ItemContainerGenerator.ItemFromContainer(c) is TileViewModel item)
        {
            container = c;
            if (item.IsCamera)
            {
                before = TilesItems.TranslatePoint(inItems, c).X < c.ActualWidth / 2;
                insert = TileReorder.InsertIndex(cams.IndexOf(item), before, cams.Count);
            }
            else
            {
                insert = cams.Count; // 빈 칸 위 = 맨 뒤로
            }
        }

        if (!TileReorder.IsMove(from, insert, cams.Count)) insert = -1;
        if (insert == _candidateInsert) return;
        _candidateInsert = insert;

        if (insert < 0 || container is null)
        {
            InsertBar.Visibility = Visibility.Collapsed;
            return;
        }
        var topLeft = container.TranslatePoint(new Point(0, 0), TilesHost);
        double x = before ? topLeft.X - InsertBar.Width / 2 : topLeft.X + container.ActualWidth - InsertBar.Width / 2;
        Canvas.SetLeft(InsertBar, Math.Max(0, x));
        Canvas.SetTop(InsertBar, topLeft.Y);
        InsertBar.Height = Math.Max(0, container.ActualHeight);
        InsertBar.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 끌기 종료 단일 지점(뗌 · 캡처 잃음 · Esc · 창 닫힘). 순서: ①상태 지움 ②시각 복원 ③(구독 없음) ④캡처 해제 ⑤확정 통지.
    /// 데드존 미만이면 클릭으로 본다(그 타일 선택).
    /// </summary>
    private void FinishDrag(bool commit)
    {
        if (!_pressed) return;
        bool wasDragging = _dragging;
        var tile = _dragTile;
        int insert = _candidateInsert;

        _pressed = false;
        _dragging = false;
        _dragTile = null;
        _candidateInsert = -1;

        Ghost.Visibility = Visibility.Collapsed;
        InsertBar.Visibility = Visibility.Collapsed;
        TilesHost.ClearValue(CursorProperty);

        if (TilesHost.IsMouseCaptured) TilesHost.ReleaseMouseCapture();

        if (!commit || tile is null) return;
        if (!wasDragging) _vm.Select(tile);
        else if (insert >= 0) _vm.MoveTile(tile, insert);
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            if (d is T match) return match;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }
}
