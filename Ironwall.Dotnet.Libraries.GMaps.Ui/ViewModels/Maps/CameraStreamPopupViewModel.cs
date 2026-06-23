using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.ViewModel;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/// <summary>
/// 맵 위 카메라 RTSP 팝업 1개의 런타임 상태(비영속). Hub(공유 디코더) 경로로 영상 표시.
/// <para>
/// Hub 배선: <see cref="CameraRowViewModel"/>(ISharedCameraStreamHub) + <see cref="CameraViewModel"/>(=player DataContext).
/// 플레이어가 OnLoaded에서 vm.IsHubManaged를 보고 ConnectViaHubAsync→Lease→공유 BitmapSource 표시.
/// </para>
/// </summary>
public class CameraStreamPopupViewModel : PropertyChangedBase, IAsyncDisposable
{
    public const double DefaultWidth = 384;
    public const double DefaultHeight = 300;
    public const double LargeWidth = 640;
    public const double LargeHeight = 380;

    private readonly CameraRowViewModel _row;
    private double _canvasLeft;
    private double _canvasTop;
    private double _popupWidth = DefaultWidth;
    private double _popupHeight = DefaultHeight;
    private double _cameraScreenX;
    private double _cameraScreenY;
    private double _lineX1, _lineY1, _lineX2, _lineY2;
    private bool _isLarge;
    private ICommand? _closeCommand;
    private ICommand? _toggleSizeCommand;

    /// <summary>닫기 요청 — MapViewModel이 컬렉션에서 제거 + DisposeAsync.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>드래그 종료 — MapViewModel이 CanvasLeft/Top→AnchorGeo 재계산 + DB 저장.</summary>
    public event EventHandler? DragCompleted;

    /// <summary>컨트롤이 드래그 종료 시 호출 → DragCompleted 발화.</summary>
    internal void RaiseDragCompleted() => DragCompleted?.Invoke(this, EventArgs.Empty);

    public CameraStreamPopupViewModel(int cameraId, string? title, RtspConnectionInfo connInfo,
        PointLatLng anchorGeo, ISharedCameraStreamHub hub)
    {
        CameraId = cameraId;
        Title = string.IsNullOrWhiteSpace(title) ? $"카메라 {cameraId}" : title!;
        ConnectionInfo = connInfo;
        AnchorGeo = anchorGeo;

        _row = new CameraRowViewModel(cameraId.ToString(), Title, hub);
        var model = new CameraModel
        {
            Guid = cameraId.ToString(),
            Title = Title,
            ConnectionInfo = connInfo,
            StreamingOptions = StreamingOptions.CreateDefault(),
            AutoPlay = true,
            ShowControls = true,
        };
        // 플레이어 DataContext가 될 스트리밍 VM (OwnerRow를 통해 Hub Lease 획득)
        StreamVm = new CameraViewModel(model, _row.RowId, _row);
    }

    /// <summary>카메라 장비 Id(= PidsSymbol.LinkedDeviceId). 팝업 식별/위치 키.</summary>
    public int CameraId { get; }
    public string Title { get; }
    public RtspConnectionInfo ConnectionInfo { get; }

    /// <summary>팝업 좌상단 코너의 위경도 앵커(드래그 완료 시 갱신 → DB 저장).</summary>
    public PointLatLng AnchorGeo { get; set; }

    /// <summary>ImprovedRtspPlayer의 DataContext(Hub 경로 분기에 사용).</summary>
    public CameraViewModel StreamVm { get; }

    public double CanvasLeft { get => _canvasLeft; set { _canvasLeft = value; NotifyOfPropertyChange(nameof(CanvasLeft)); RecomputeLine(); } }
    public double CanvasTop { get => _canvasTop; set { _canvasTop = value; NotifyOfPropertyChange(nameof(CanvasTop)); RecomputeLine(); } }
    public double PopupWidth { get => _popupWidth; set { _popupWidth = value; NotifyOfPropertyChange(nameof(PopupWidth)); RecomputeLine(); } }
    public double PopupHeight { get => _popupHeight; set { _popupHeight = value; NotifyOfPropertyChange(nameof(PopupHeight)); RecomputeLine(); } }

    // ── 연결선(Leader Line): 카메라 심볼 중점 → 팝업 경계 (빨간 점선) ──────────
    /// <summary>카메라 심볼 중점의 위경도(팬/줌 시 화면점 재계산용). MapViewModel이 설정.</summary>
    public PointLatLng CameraGeo { get; set; }

    /// <summary>카메라 심볼 중점의 화면(Canvas) 좌표 = 연결선 끝점1. MapViewModel이 팬/줌 시 갱신.</summary>
    public double CameraScreenX { get => _cameraScreenX; set { _cameraScreenX = value; RecomputeLine(); } }
    public double CameraScreenY { get => _cameraScreenY; set { _cameraScreenY = value; RecomputeLine(); } }

    public double LineX1 { get => _lineX1; private set { _lineX1 = value; NotifyOfPropertyChange(nameof(LineX1)); } }
    public double LineY1 { get => _lineY1; private set { _lineY1 = value; NotifyOfPropertyChange(nameof(LineY1)); } }
    public double LineX2 { get => _lineX2; private set { _lineX2 = value; NotifyOfPropertyChange(nameof(LineX2)); } }
    public double LineY2 { get => _lineY2; private set { _lineY2 = value; NotifyOfPropertyChange(nameof(LineY2)); } }

    /// <summary>끝점1=카메라 중점, 끝점2=카메라→팝업중심 선분이 팝업 사각형 경계와 만나는 점(좌/우/상/하 자동).</summary>
    private void RecomputeLine()
    {
        var cx = _canvasLeft + _popupWidth / 2;
        var cy = _canvasTop + _popupHeight / 2;
        var dx = _cameraScreenX - cx;
        var dy = _cameraScreenY - cy;

        LineX1 = _cameraScreenX;
        LineY1 = _cameraScreenY;

        if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) { LineX2 = cx; LineY2 = cy; return; }

        var scaleX = Math.Abs(dx) > 1e-6 ? (_popupWidth / 2) / Math.Abs(dx) : double.PositiveInfinity;
        var scaleY = Math.Abs(dy) > 1e-6 ? (_popupHeight / 2) / Math.Abs(dy) : double.PositiveInfinity;
        var scale = Math.Min(Math.Min(scaleX, scaleY), 1.0);   // 팝업 경계까지(카메라가 안쪽이면 중심)

        LineX2 = cx + dx * scale;
        LineY2 = cy + dy * scale;
    }

    /// <summary>"크게보기" 토글 상태(런타임만, 영속 안 함 — Q4).</summary>
    public bool IsLarge { get => _isLarge; private set { _isLarge = value; NotifyOfPropertyChange(nameof(IsLarge)); } }

    public ICommand CloseCommand =>
        _closeCommand ??= new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

    public ICommand ToggleSizeCommand =>
        _toggleSizeCommand ??= new RelayCommand(ToggleSize);

    private void ToggleSize()
    {
        IsLarge = !IsLarge;
        PopupWidth = IsLarge ? LargeWidth : DefaultWidth;
        PopupHeight = IsLarge ? LargeHeight : DefaultHeight;
    }

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;   // 멱등(타이머 Tick + 수동 Close 동시 진입 방어)
        _disposed = true;

        // Hub Lease 해제(C-03: Row가 Stop→Dispose 순서 담당)
        try { await _row.DisposeAsync().ConfigureAwait(false); }
        catch { /* 종료 경로 — 무해 */ }
        CloseRequested = null;
    }
}
