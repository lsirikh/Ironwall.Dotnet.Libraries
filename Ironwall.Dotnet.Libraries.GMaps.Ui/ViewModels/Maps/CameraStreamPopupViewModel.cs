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

    public double CanvasLeft { get => _canvasLeft; set { _canvasLeft = value; NotifyOfPropertyChange(nameof(CanvasLeft)); } }
    public double CanvasTop { get => _canvasTop; set { _canvasTop = value; NotifyOfPropertyChange(nameof(CanvasTop)); } }
    public double PopupWidth { get => _popupWidth; set { _popupWidth = value; NotifyOfPropertyChange(nameof(PopupWidth)); } }
    public double PopupHeight { get => _popupHeight; set { _popupHeight = value; NotifyOfPropertyChange(nameof(PopupHeight)); } }

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

    public async ValueTask DisposeAsync()
    {
        // Hub Lease 해제(C-03: Row가 Stop→Dispose 순서 담당)
        try { await _row.DisposeAsync().ConfigureAwait(false); }
        catch { /* 종료 경로 — 무해 */ }
        CloseRequested = null;
    }
}
