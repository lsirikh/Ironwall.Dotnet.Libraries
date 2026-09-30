using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 조립 카드(L3, component-display-unify FR-05)의 수명 — 운영 모드에서 심볼을 클릭하면 열고, Esc · 빈 곳 클릭 · 다른 심볼 ·
/// 편집 모드 · 심볼 사라짐에 닫는다. <see cref="LabelAdornerService"/> 와 같은 급의 맵 부속 서비스다.
/// </summary>
/// <remarks>
/// <para><b>지도 팬을 뺏지 않는다</b>: 지도는 좌드래그가 팬이고, 심볼 클릭 신호(<c>OnMarkerClicked</c>)는 <b>누른 순간</b> 온다.
/// 그 자리에서 열면 "심볼 위에서 시작한 팬"도 카드를 연다. 그래서 누름 때는 후보만 적고, 뗄 때 그 사이 지도 드래그가 없었을 때만 연다.
/// 이 서비스는 마우스를 캡처하지 않고 입력을 소비하지 않는다(엿보기만 한다).</para>
/// <para><b>6.3 무회귀</b>: 부품 축이 아예 없는 장비(6.3 서버 · 미연결)는 카드를 열지 않는다. 축은 있는데 관측을 못 받았으면 카드는
/// 열되 "부품 정보 없음"만 적는다(FR-08).</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public sealed class ComponentCardService : IDisposable
{
    private readonly GMapCustomControl _map;
    private readonly ILogService? _log;
    private readonly Func<IDeviceConsoleNavigator?> _navigator;
    private ComponentCardAdorner? _adorner;
    private GMapPidsMarker? _pending;
    private bool _draggedSincePress;
    private Window? _window;
    private bool _disposed;

    public ComponentCardService(GMapCustomControl map, ILogService? log = null, Func<IDeviceConsoleNavigator?>? navigator = null)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _log = log;
        _navigator = navigator ?? ResolveNavigator;
        _map.OnMapDrag += OnMapDrag;
        _map.PreviewMouseLeftButtonUp += OnMapMouseUp;
    }

    /// <summary>지금 열린 카드의 대상. 없으면 null.</summary>
    public GMapPidsMarker? Current => _adorner?.Marker;

    /// <summary>지금 열린 카드(시험용).</summary>
    internal ComponentCardAdorner? Adorner => _adorner;

    /// <summary>카드를 열 수 있는 심볼인가 — 연결 장비에 부품 축이 있다(6.3 · 미연결은 열지 않는다).</summary>
    public static bool CanShow(IEditableMarker? marker)
        => marker is GMapPidsMarker { IsDisposed: false } pids && pids.ComponentSummary.HasAxes;

    /// <summary>심볼 클릭(누름) — 후보로만 적는다. 뗄 때 드래그가 없었으면 연다.</summary>
    public void OnMarkerPressed(IEditableMarker marker)
    {
        _pending = CanShow(marker) ? (GMapPidsMarker)marker : null;
        _draggedSincePress = false;
        if (_pending is null) Hide();   // 카드를 못 여는 심볼을 골랐다 — 옛 카드는 걷는다
    }

    /// <summary>카드를 연다(이미 다른 심볼에 열려 있으면 바꾼다).</summary>
    public void Show(GMapPidsMarker marker)
    {
        if (_disposed || !CanShow(marker)) return;
        if (ReferenceEquals(_adorner?.Marker, marker)) { _adorner!.ViewModel.Refresh(); return; }
        Hide();

        var layer = AdornerLayer.GetAdornerLayer(_map);
        if (layer is null) { _log?.Warning("[ComponentCard] AdornerLayer 없음 — 카드를 띄우지 않는다"); return; }

        var vm = new ComponentCardViewModel(marker, SafeNavigator(), DateTime.Today);
        vm.CloseRequested += Hide;
        _adorner = new ComponentCardAdorner(_map, vm);
        layer.Add(_adorner);
        marker.IsComponentCardOpen = true;
        ((INotifyPropertyChanged)marker).PropertyChanged += OnMarkerPropertyChanged;
        HookWindow();
    }

    /// <summary>카드를 닫는다. 열려 있지 않으면 아무것도 하지 않는다.</summary>
    public void Hide()
    {
        _pending = null;
        var adorner = _adorner;
        if (adorner is null) return;
        _adorner = null;

        adorner.ViewModel.CloseRequested -= Hide;
        ((INotifyPropertyChanged)adorner.Marker).PropertyChanged -= OnMarkerPropertyChanged;
        adorner.Marker.IsComponentCardOpen = false;
        try { AdornerLayer.GetAdornerLayer(_map)?.Remove(adorner); } catch (Exception) { /* 레이어 정리 중 */ }
        adorner.Dispose();
        UnhookWindow();
    }

    /// <summary>빈 곳 클릭 — 닫는다.</summary>
    public void OnMapClicked() => Hide();

    private void OnMapDrag() => _draggedSincePress = true;

    private void OnMapMouseUp(object sender, MouseButtonEventArgs e)
    {
        var pending = _pending;
        _pending = null;
        if (pending is null || _draggedSincePress) return;   // 팬이었다 — 열지 않는다
        Show(pending);
    }

    /// <summary>부품 요약이 바뀌면(SYNC_DEVICE) 표를 다시 만들고, 심볼이 사라지면 닫는다.</summary>
    private void OnMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_adorner is null || !ReferenceEquals(sender, _adorner.Marker)) return;
        if (_adorner.Marker.IsDisposed) { Hide(); return; }
        if (e.PropertyName is nameof(GMapPidsMarker.ComponentSummary) or nameof(GMapPidsMarker.Title))
        {
            if (!CanShow(_adorner.Marker)) { Hide(); return; }
            _adorner.ViewModel.Refresh();
            _adorner.InvalidateMeasure();
        }
        else if (e.PropertyName is nameof(IEditableMarker.Position) or nameof(IEditableMarker.Width))
            _adorner.InvalidateArrange();
    }

    // ── Esc: 지도가 아니라 창에서 받는다(포커스가 지도에 없어도 닫힌다). 소비하지 않는다 — 다른 Esc 동작(조준 취소 등)을 막지 않는다.
    private void HookWindow()
    {
        if (_window != null) return;
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
        if (e.Key == Key.Escape && _adorner != null) Hide();
    }

    private IDeviceConsoleNavigator? SafeNavigator()
    {
        try { return _navigator(); }
        catch (Exception) { return null; }
    }

    private static IDeviceConsoleNavigator? ResolveNavigator()
    {
        try { return Caliburn.Micro.IoC.GetAllInstances(typeof(IDeviceConsoleNavigator))?.OfType<IDeviceConsoleNavigator>().FirstOrDefault(); }
        catch (Exception) { return null; }   // 호스트가 등록하지 않았다 — 항법 단추를 숨긴다
    }

    public void Dispose()
    {
        if (_disposed) return;
        Hide();
        _disposed = true;
        _map.OnMapDrag -= OnMapDrag;
        _map.PreviewMouseLeftButtonUp -= OnMapMouseUp;
    }
}
