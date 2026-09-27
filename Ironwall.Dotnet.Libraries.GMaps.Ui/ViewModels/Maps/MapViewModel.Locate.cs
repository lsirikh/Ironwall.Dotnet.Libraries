using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Devices.Units;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : [지도에서 보기] 처리기 — MapLocateRequest → 강조 고리 · 맞춤 · 회신 (unit-relationship-map FR-45)
                  MapViewModel partial 분리 — 본체(MapViewModel.cs)는 IHandle 목록 한 줄만 고친다.
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public partial class MapViewModel
{
    // 판정은 순수(MapLocateResolver), 여기는 지도에 옮기기만 한다.
    private readonly MapLocateHighlight _locateHighlight = new();
    private readonly List<LocateRingMarker> _locateRings = new();
    private Window? _locateEscWindow;
    private bool _locateMouseHooked;
    private Point? _locatePressPoint;

    /// <summary>
    /// 콘솔 [지도에서 보기] · 이벤트 상세가 보낸 요청 — 그 장비들의 심볼에 고리를 그리고, 그 범위로 맞추고, 회신한다.
    /// </summary>
    /// <remarks>
    /// <see cref="HandleAsync(DeviceGroupMembershipChangedMessage, CancellationToken)"/> 와 같은 모양이다 — 구독은 발행 스레드라
    /// (<c>SubscribeOnPublishedThread</c>) 어느 스레드에서도 올 수 있으므로, 지도 마커를 읽고 쓰는 일은 UI 스레드로 넘긴다.
    /// </remarks>
    public Task HandleAsync(MapLocateRequest message, CancellationToken cancellationToken)
    {
        if (message is null) return Task.CompletedTask;
        return OnUiAsync(() => LocateDevices(message));
    }

    /// <summary>강조 · 맞춤 · 회신. 호출 스레드: UI.</summary>
    private void LocateDevices(MapLocateRequest request)
    {
        MapLocatePlan? plan = null;
        try
        {
            var symbols = MainMap?.Markers.OfType<IPidsEditableMarker>()
                                  .Select(m => new MapLocateSymbol(
                                      MapLocateResolver.DeviceKeyOf(m.LinkedDevice?.Id, m.LinkedDeviceId),
                                      m.Position.Lat, m.Position.Lng,
                                      IsLayerVisible: m.IsLayerEnabled && m.Visible))
                                  .ToList()
                          ?? new List<MapLocateSymbol>();

            plan = MapLocateResolver.Plan(request, symbols, CurrentAnchorSite());

            RemoveLocateRings(_locateHighlight.Begin(plan));
            if (MainMap != null)
            {
                foreach (var ring in plan.Rings)
                {
                    var marker = new LocateRingMarker(ring.DeviceId, new PointLatLng(ring.Lat, ring.Lng));
                    _locateRings.Add(marker);
                    MainMap.Markers.Add(marker);
                }

                // 맞춤 — 사이트 고정 중이면 판정이 이미 원본 구역 안으로 잘랐다. 컨트롤은 중심을 라이브 inset(BoundsOfMap) 안으로
                //   되돌리고(ClampCenterToBounds) 줌이 바뀌면 inset 을 다시 계산하므로, 구역 안의 경계만 주면 가두기와 싸우지 않는다.
                //   최소 줌 하한(앵커 MinZoomFloor)은 컨트롤 MinZoom 이 지킨다.
                if (plan.Fit is { } fit)
                    MainMap.SetZoomToFitRect(RectLatLng.FromLTRB(fit.West, fit.North, fit.East, fit.South));
            }

            if (plan.Rings.Count > 0) HookLocateClearTriggers();
            _log?.Info($"[지도에서 보기] '{request.Title}' 요청 {request.DeviceIds.Count} · 강조 {plan.Shown.Count}"
                     + $" · 지도에 없음 {plan.NotOnMap.Count} · 숨김 {plan.Hidden.Count} · 구역 밖 {plan.OutsideAnchor.Count}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[지도에서 보기] 처리 실패: {ex.Message}");
        }
        finally
        {
            // 실패해도 회신은 보낸다 — 요청자가 기다리다 멈추지 않게(보이지 않은 장비는 전부 Missing).
            var result = plan?.ToResult() ?? new MapLocateResult(request.RequestId, 0, request.DeviceIds.Count);
            _ = PublishLocateResultAsync(result);
        }
    }

    private async Task PublishLocateResultAsync(MapLocateResult result)
    {
        try { await _eventAggregator!.PublishOnCurrentThreadAsync(result); }
        catch (Exception ex) { _log?.Warning($"[지도에서 보기] 회신 발행 실패: {ex.Message}"); }
    }

    /// <summary>사이트 고정(맵 앵커)이 켜져 있으면 그 원본 구역, 아니면 <c>null</c>.</summary>
    private MapLocateBounds? CurrentAnchorSite()
    {
        if (MainMap?.IsAnchorActive != true) return null;
        var anchor = _setupModel?.MapAnchor;
        if (anchor == null || !anchor.IsAvailable) return null;
        if (BuildAnchorRect(anchor) is not { } r) return null;
        return new MapLocateBounds(North: r.Lat, South: r.Lat - r.HeightLat, East: r.Lng + r.WidthLng, West: r.Lng);
    }

    /// <summary>강조를 내린다 — 지도 빈 곳 클릭 · <c>Esc</c>. 다음 요청은 <see cref="LocateDevices"/> 가 스스로 교체한다.</summary>
    private void ClearLocateHighlight()
    {
        RemoveLocateRings(_locateHighlight.Clear());
        UnhookLocateClearTriggers();
    }

    private void RemoveLocateRings(IReadOnlyList<MapLocateRing> _)
    {
        // 판정 상태의 고리와 지도의 고리 마커는 1:1 로 함께 올리고 내린다 — 지도 쪽은 우리가 만든 인스턴스만 뺀다.
        if (MainMap != null)
            foreach (var marker in _locateRings)
                MainMap.Markers.Remove(marker);
        _locateRings.Clear();
    }

    #region - 소속 부대 (FR-46) -
    private IUnitDirectory? _unitDirectory;
    private bool _unitDirectoryResolved;

    /// <summary>
    /// 부대 이름 · 경로 사전 — <b>선택</b>이다(컨테이너에 없으면 null → 소속 부대 줄 · 메뉴 항목을 숨긴다).
    /// 해석에 성공했을 때만 캐시한다(실패하면 다음에 다시 — 부팅 순서로 늦게 등록될 수 있다).
    /// </summary>
    private IUnitDirectory? ResolveUnitDirectory()
    {
        if (_unitDirectoryResolved) return _unitDirectory;
        try
        {
            _unitDirectory = IoC.GetAll<IUnitDirectory>().FirstOrDefault();
            _unitDirectoryResolved = _unitDirectory != null;
        }
        catch (Exception ex)
        {
            _log?.Warning($"[소속 부대] 부대 사전 미해석(줄 숨김): {ex.Message}");
            _unitDirectory = null;
        }
        return _unitDirectory;
    }

    /// <summary>
    /// 마커 우클릭 메뉴에 "소속 부대 …" 한 항목을 더한다 — 누르면 부대 콘솔을 관계도 레일로 연다(<see cref="OpenUnitConsoleRequest"/>).
    /// 운영 모드의 PIDS 심볼 · 사전이 있을 때만. 문구 판정은 상세 창과 같은 <see cref="SymbolDetailUnitLine"/>.
    /// </summary>
    private void AddUnitLineMenuItem(ContextMenu menu, IEditableMarker marker)
    {
        try
        {
            if (IsEditModeEnabled || marker is not IPidsEditableMarker pids) return;
            var directory = ResolveUnitDirectory();
            var unitId = pids.LinkedDevice?.UnitId;
            var line = SymbolDetailUnitLine.Evaluate(unitId, directory, loadAttempted: true);
            if (!line.IsVisible) return;

            // 아직 못 읽었으면 다음 번을 위해 한 번 읽게 한다(메뉴는 지금 문구로 뜬다).
            if (directory != null && unitId is > 0 && !line.CanOpen)
                _ = directory.EnsureLoadedAsync();

            var item = new MenuItem
            {
                Header = line.CanOpen ? $"{line.Text}  ›  관계도에서 보기" : line.Text,
                IsEnabled = line.CanOpen,
                Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.FileTree, Width = 16, Height = 16 },
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(item, "GMaps.MarkerMenu.OpenUnitMap");
            if (line.UnitId is int id)
                item.Click += (_, _) => _ = PublishOpenUnitConsoleAsync(new OpenUnitConsoleRequest(id, OpenMap: true));
            menu.Items.Add(item);
            menu.Items.Add(new Separator());
        }
        catch (Exception ex)
        {
            _log?.Warning($"[소속 부대] 메뉴 항목 구성 실패: {ex.Message}");
        }
    }

    private async Task PublishOpenUnitConsoleAsync(OpenUnitConsoleRequest request)
    {
        try { await _eventAggregator!.PublishOnCurrentThreadAsync(request); }
        catch (Exception ex) { _log?.Warning($"[소속 부대] 관계도 열기 요청 발행 실패: {ex.Message}"); }
    }
    #endregion

    #region - 해제 트리거(빈 곳 클릭 · Esc) -
    private void HookLocateClearTriggers()
    {
        if (MainMap == null) return;
        if (!_locateMouseHooked)
        {
            MainMap.PreviewMouseLeftButtonDown += OnLocatePreviewMouseDown;
            MainMap.PreviewMouseLeftButtonUp += OnLocatePreviewMouseUp;
            _locateMouseHooked = true;
        }
        if (_locateEscWindow == null)
        {
            _locateEscWindow = Window.GetWindow(MainMap);
            if (_locateEscWindow != null) _locateEscWindow.PreviewKeyDown += OnLocatePreviewKeyDown;
        }
    }

    private void UnhookLocateClearTriggers()
    {
        if (MainMap != null && _locateMouseHooked)
        {
            MainMap.PreviewMouseLeftButtonDown -= OnLocatePreviewMouseDown;
            MainMap.PreviewMouseLeftButtonUp -= OnLocatePreviewMouseUp;
        }
        _locateMouseHooked = false;
        _locatePressPoint = null;
        if (_locateEscWindow != null) _locateEscWindow.PreviewKeyDown -= OnLocatePreviewKeyDown;
        _locateEscWindow = null;
    }

    /// <summary>Esc 는 소비하지 않는다 — 다른 기능(배치 모드 취소 등)이 같은 키를 쓴다. 강조만 내린다.</summary>
    private void OnLocatePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _locateHighlight.ActiveRequestId != null) ClearLocateHighlight();
    }

    /// <summary>누른 자리를 적는다 — 심볼 위에서 누르면 '빈 곳 클릭' 이 아니다(심볼 클릭 · 우클릭 메뉴를 방해하지 않는다).</summary>
    private void OnLocatePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _locatePressPoint = IsOverSymbol(e.OriginalSource as DependencyObject) || MainMap == null ? null : e.GetPosition(MainMap);
    }

    /// <summary>빈 곳에서 데드존(8 DIU) 미만으로 뗐으면 클릭 — 강조를 내린다. 넘었으면 팬이므로 그대로 둔다. 이벤트를 소비하지 않는다.</summary>
    private void OnLocatePreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_locatePressPoint is not { } pressed || MainMap == null) return;
        _locatePressPoint = null;
        var now = e.GetPosition(MainMap);
        if (!DragMath.IsDrag(now.X - pressed.X, now.Y - pressed.Y)) ClearLocateHighlight();
    }

    /// <summary>누른 요소가 편집 마커(심볼 · 이미지)의 일부인가 — 판정 결정이 아니라 해제 조건 거르기라 부모 사슬만 본다.</summary>
    private static bool IsOverSymbol(DependencyObject? source)
    {
        for (var node = source; node != null; node = node is Visual || node is System.Windows.Media.Media3D.Visual3D
                                                  ? VisualTreeHelper.GetParent(node)
                                                  : LogicalTreeHelper.GetParent(node))
        {
            if (node is IMarkerControl) return true;
        }
        return false;
    }
    #endregion
}
