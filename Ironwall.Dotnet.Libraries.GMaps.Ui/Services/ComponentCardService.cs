using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>조립 카드를 실제로 띄우고 걷는 쪽 — 지도에서는 어도너 층, 시험에서는 가짜.</summary>
public interface IComponentCardHost
{
    /// <summary>지금 카드가 열린 심볼. 없으면 null.</summary>
    GMapPidsMarker? Current { get; }

    /// <summary>카드를 연다(다른 심볼에 열려 있으면 호출 전에 <see cref="Close"/> 가 불린다).</summary>
    void Open(GMapPidsMarker marker, Action onCloseRequested);

    /// <summary>카드를 걷는다.</summary>
    void Close();

    /// <summary>같은 심볼의 표를 다시 만든다(부품 요약 · 제목이 바뀌었다).</summary>
    void Refresh();

    /// <summary>심볼 위치 · 크기가 바뀌어 자리만 다시 잡는다.</summary>
    void Relayout();
}

/// <summary>
/// 조립 카드(L3, component-display-unify FR-05)의 수명 — 운영 모드에서 심볼을 <b>클릭</b>하면 열고, Esc · 빈 곳 <b>클릭</b> · 다른 심볼 ·
/// 편집 모드 · 심볼 사라짐에 닫는다. <see cref="LabelAdornerService"/> 와 같은 급의 맵 부속 서비스다.
/// </summary>
/// <remarks>
/// <para><b>지도 팬을 뺏지 않는다 — 누름이 아니라 뗌에 결정한다</b>: 지도는 좌드래그가 팬이고, 심볼 클릭 · 빈 곳 클릭 신호는 <b>누른 순간</b> 온다.
/// 그 자리에서 열거나 닫으면 "심볼 위에서 시작한 팬"이 카드를 열고 "빈 곳에서 시작한 팬"이 카드를 닫는다. 그래서 누름에는 후보만 적고,
/// 뗄 때 누른 자리에서 8 DIU(<see cref="DragMath.DeadZone"/>) 안이고 그 사이 지도 드래그가 없었을 때만 연다 · 닫는다.
/// 팬 중에는 카드가 그대로 있고 지도를 따라간다. 휠 줌도 카드를 닫지 않는다.</para>
/// <para>이 서비스는 마우스를 캡처하지 않고 입력을 소비하지 않는다(엿보기만 한다). 후보는 지도 밖으로 나가거나 · 캡처를 잃거나 ·
/// 오버레이 이미지를 누르면 버린다(나중에 엉뚱한 뗌에 열리지 않게).</para>
/// <para><b>6.3 무회귀</b>: 부품 축이 아예 없는 장비(6.3 서버 · 미연결)는 카드를 열지 않는다. 축은 있는데 관측을 못 받았으면 카드는
/// 열되 "부품 정보 없음"만 적는다(FR-08).</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public sealed class ComponentCardService : IDisposable
{
    private enum PressKind { None, Marker, Empty }

    private readonly IComponentCardHost _host;
    private readonly GMapCustomControl? _map;
    private PressKind _press;
    private GMapPidsMarker? _pressedMarker;
    private Point _pressAt;
    private bool _draggedSincePress;
    private Window? _window;
    private bool _disposed;

    /// <summary>지도에 붙인다 — 어도너 층에 카드를 띄우고 지도 입력을 엿본다.</summary>
    public ComponentCardService(GMapCustomControl map, ILogService? log = null, Func<IDeviceConsoleNavigator?>? navigator = null)
        : this(new AdornerCardHost(map, log, navigator ?? ResolveNavigator))
    {
        _map = map;
        map.OnMapDrag += OnMapDragged;
        map.PreviewMouseLeftButtonUp += OnMapMouseUp;
        map.LostMouseCapture += OnMapLostCapture;
        map.MouseLeave += OnMapMouseLeave;
        map.OnImageClicked += OnMapImageClicked;
    }

    /// <summary>시험 · 다른 호스트용.</summary>
    internal ComponentCardService(IComponentCardHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>지금 열린 카드의 대상. 없으면 null.</summary>
    public GMapPidsMarker? Current => _host.Current;

    /// <summary>카드를 열 수 있는 심볼인가 — 연결 장비에 부품 축이 있다(6.3 · 미연결은 열지 않는다).</summary>
    public static bool CanShow(IEditableMarker? marker)
        => marker is GMapPidsMarker { IsDisposed: false } pids && pids.ComponentSummary.HasAxes;

    #region - 입력(누름 · 뗌) -
    /// <summary>심볼 누름(지도의 <c>OnMarkerClicked</c>) — 후보로만 적는다. 위치는 지도의 지금 마우스 위치.</summary>
    public void OnMarkerPressed(IEditableMarker marker)
        => OnMarkerPressed(marker, _map != null ? Mouse.GetPosition(_map) : default);

    /// <summary>심볼 누름 — 후보로만 적는다. 뗄 때 데드존 안이면 연다.</summary>
    public void OnMarkerPressed(IEditableMarker marker, Point at)
    {
        _press = PressKind.Marker;
        _pressedMarker = marker as GMapPidsMarker;
        _pressAt = at;
        _draggedSincePress = false;
    }

    /// <summary>빈 곳 누름(지도의 <c>OnMapClicked</c>, 좌표는 지도 요소 좌표) — 후보로만 적는다. 뗄 때 데드존 안이면 닫는다.</summary>
    public void OnEmptyPressed(Point at)
    {
        _press = PressKind.Empty;
        _pressedMarker = null;
        _pressAt = at;
        _draggedSincePress = false;
    }

    /// <summary>그 밖의 누름(오버레이 이미지 등) — 후보를 버린다. 카드는 그대로.</summary>
    public void OnOtherPressed() => ClearPress();

    /// <summary>지도 드래그(팬)가 일어났다 — 이번 누름은 클릭이 아니다.</summary>
    public void OnMapDragged() => _draggedSincePress = true;

    /// <summary>
    /// 뗌 — 누른 자리에서 <see cref="DragMath.DeadZone"/> 안이고 드래그가 없었을 때만 클릭으로 본다:
    /// 심볼이면 연다(못 여는 심볼이면 닫는다), 빈 곳이면 닫는다.
    /// </summary>
    public void OnReleased(Point at)
    {
        var kind = _press;
        var marker = _pressedMarker;
        var dragged = _draggedSincePress || DragMath.IsDrag(at.X - _pressAt.X, at.Y - _pressAt.Y);
        ClearPress();
        if (kind == PressKind.None || dragged) return;   // 팬이었다 — 카드는 그대로(지도를 따라간다)

        if (kind == PressKind.Empty) { Hide(); return; }
        if (marker != null && CanShow(marker)) Show(marker);
        else Hide();   // 카드를 못 여는 심볼을 클릭했다 — 옛 카드는 걷는다
    }

    /// <summary>후보를 버린다(캡처 상실 · 지도 밖 · 뗌을 받지 못함).</summary>
    public void OnPressCancelled() => ClearPress();

    /// <summary>Esc — 열려 있으면 닫는다.</summary>
    public void OnEscape()
    {
        if (_host.Current != null) Hide();
    }
    #endregion

    /// <summary>카드를 연다(이미 다른 심볼에 열려 있으면 바꾼다).</summary>
    public void Show(GMapPidsMarker marker)
    {
        if (_disposed || !CanShow(marker)) return;
        if (ReferenceEquals(_host.Current, marker)) { _host.Refresh(); return; }
        Hide();
        _host.Open(marker, Hide);
        if (_host.Current is null) return;
        marker.IsComponentCardOpen = true;
        ((INotifyPropertyChanged)marker).PropertyChanged += OnMarkerPropertyChanged;
        HookWindow();
    }

    /// <summary>카드를 닫는다. 열려 있지 않으면 아무것도 하지 않는다.</summary>
    public void Hide()
    {
        var marker = _host.Current;
        if (marker is null) return;
        ((INotifyPropertyChanged)marker).PropertyChanged -= OnMarkerPropertyChanged;
        marker.IsComponentCardOpen = false;
        _host.Close();
        UnhookWindow();
    }

    /// <summary>편집 모드 · 맵 전환 등 — 후보와 카드를 모두 버린다.</summary>
    public void Reset()
    {
        ClearPress();
        Hide();
    }

    private void ClearPress()
    {
        _press = PressKind.None;
        _pressedMarker = null;
        _draggedSincePress = false;
    }

    // ── 지도 배선(지도에 붙였을 때만) ──
    private void OnMapMouseUp(object sender, MouseButtonEventArgs e) => OnReleased(e.GetPosition((IInputElement)sender));
    private void OnMapLostCapture(object sender, MouseEventArgs e)
    {
        // 팬이 끝나며 캡처를 놓는 것은 뗌 뒤라 후보가 이미 비었다. 단추가 눌린 채 캡처를 잃었다면 이 누름은 뗌을 못 받는다.
        if (e.LeftButton == MouseButtonState.Pressed) ClearPress();
    }
    private void OnMapMouseLeave(object sender, MouseEventArgs e)
    {
        if (_map?.IsMouseCaptured != true) ClearPress();   // 캡처 없이 지도 밖으로 — 뗌을 못 받는다(카드 위로 옮겨 간 경우 포함)
    }
    private void OnMapImageClicked(GMapImages.GMapCustomImage _) => ClearPress();

    /// <summary>부품 요약이 바뀌면(SYNC_DEVICE) 표를 다시 만들고, 심볼이 사라지면 닫는다.</summary>
    private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var current = _host.Current;
        if (current is null || !ReferenceEquals(sender, current)) return;
        if (current.IsDisposed) { Hide(); return; }
        if (e.PropertyName is nameof(GMapPidsMarker.ComponentSummary) or nameof(GMapPidsMarker.Title))
        {
            if (!CanShow(current)) { Hide(); return; }
            _host.Refresh();
        }
        else if (e.PropertyName is nameof(IEditableMarker.Position) or nameof(IEditableMarker.Width))
            _host.Relayout();
    }

    // ── Esc: 지도가 아니라 창에서 받는다(포커스가 지도에 없어도 닫힌다). 소비하지 않는다 — 다른 Esc 동작(조준 취소 등)을 막지 않는다.
    private void HookWindow()
    {
        if (_window != null || _map == null) return;
        _window = Window.GetWindow(_map);
        if (_window != null) _window.PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private void UnhookWindow()
    {
        if (_window == null) return;
        _window.PreviewKeyDown -= OnWindowPreviewKeyDown;
        _window = null;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) OnEscape();
    }

    private static IDeviceConsoleNavigator? ResolveNavigator()
    {
        try { return Caliburn.Micro.IoC.GetAllInstances(typeof(IDeviceConsoleNavigator))?.OfType<IDeviceConsoleNavigator>().FirstOrDefault(); }
        catch (Exception) { return null; }   // 호스트가 등록하지 않았다 — 항법 단추를 숨긴다
    }

    public void Dispose()
    {
        if (_disposed) return;
        Reset();
        _disposed = true;
        if (_map != null)
        {
            _map.OnMapDrag -= OnMapDragged;
            _map.PreviewMouseLeftButtonUp -= OnMapMouseUp;
            _map.LostMouseCapture -= OnMapLostCapture;
            _map.MouseLeave -= OnMapMouseLeave;
            _map.OnImageClicked -= OnMapImageClicked;
        }
    }

    /// <summary>지도 어도너 층에 카드를 띄우는 호스트.</summary>
    private sealed class AdornerCardHost : IComponentCardHost
    {
        private readonly GMapCustomControl _map;
        private readonly ILogService? _log;
        private readonly Func<IDeviceConsoleNavigator?> _navigator;
        private ComponentCardAdorner? _adorner;
        private Action? _onClose;

        public AdornerCardHost(GMapCustomControl map, ILogService? log, Func<IDeviceConsoleNavigator?> navigator)
        {
            _map = map; _log = log; _navigator = navigator;
        }

        public GMapPidsMarker? Current => _adorner?.Marker;

        public void Open(GMapPidsMarker marker, Action onCloseRequested)
        {
            var layer = AdornerLayer.GetAdornerLayer(_map);
            if (layer is null) { _log?.Warning("[ComponentCard] AdornerLayer 없음 — 카드를 띄우지 않는다"); return; }
            IDeviceConsoleNavigator? navigator;
            try { navigator = _navigator(); } catch (Exception) { navigator = null; }
            var vm = new ComponentCardViewModel(marker, navigator, DateTime.Today);
            _onClose = onCloseRequested;
            vm.CloseRequested += onCloseRequested;
            _adorner = new ComponentCardAdorner(_map, vm);
            layer.Add(_adorner);
        }

        public void Close()
        {
            var adorner = _adorner;
            if (adorner is null) return;
            _adorner = null;
            if (_onClose != null) adorner.ViewModel.CloseRequested -= _onClose;
            _onClose = null;
            try { AdornerLayer.GetAdornerLayer(_map)?.Remove(adorner); } catch (Exception) { /* 레이어 정리 중 */ }
            adorner.Dispose();
        }

        public void Refresh()
        {
            if (_adorner is null) return;
            _adorner.ViewModel.Refresh();
            _adorner.InvalidateMeasure();
        }

        public void Relayout() => _adorner?.InvalidateArrange();
    }
}
