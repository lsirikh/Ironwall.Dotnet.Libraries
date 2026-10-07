using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.SharedMemory;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 프리셋 · ROI 창 — 카메라 실시간 영상(카메라 팝업 호스트 오버레이) · 프리셋 자리로 이동
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 카메라 팝업 호스트로 영상을 여는 <see cref="IMappingCameraVideo"/> — 지도 카메라 팝업과 <b>같은 오버레이 경로</b>다.
/// </summary>
/// <remarks>
/// <para>GIS 는 LibVLC 를 부르지 않는다 — 호스트 프로세스가 RTSP · ONVIF 를 디코딩해 GIS 가 만든 공유 메모리에 프레임을 쓰고,
/// 화면은 그것을 복사해 그리기만 한다. 호스트가 죽으면 감시자가 다시 띄우고 같은 스트림을 복원한다.</para>
/// <para>제공자 정보는 이벤트 창과 같은 빌더(<see cref="EventWindowPlanning.BuildProvider"/>)로 만든다.</para>
/// <para>절대 던지지 않는다. 막히면 사유 한 줄을 담은 실패 결과로 끝난다.</para>
/// </remarks>
public sealed class HostMappingCameraVideo : IMappingCameraVideo
{
    private readonly ICameraPopupHost? _host;
    private readonly IEventWindowDeviceDirectory _directory;
    private readonly Func<CameraPopupSettings> _settings;
    private readonly Func<ICameraPopupControl?> _control;
    private readonly ILogService? _log;

    /// <summary>생성자.</summary>
    /// <param name="host">팝업 호스트 감시자. 없으면 "영상 기능을 쓸 수 없음".</param>
    /// <param name="directory">카메라 장비 캐시.</param>
    /// <param name="settings">카메라 팝업 설정(제공자 · 스트림 선택). 생략하면 기본값.</param>
    /// <param name="log">로그.</param>
    /// <param name="control">호스트 요청 창구(프리셋 이동). 생략하면 <paramref name="host"/> 위에 처음 부를 때 하나 세운다.</param>
    public HostMappingCameraVideo(
        ICameraPopupHost? host,
        IEventWindowDeviceDirectory directory,
        Func<CameraPopupSettings>? settings = null,
        ILogService? log = null,
        Func<ICameraPopupControl?>? control = null)
    {
        _host = host;
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _settings = settings ?? (() => new CameraPopupSettings().Normalize());
        _log = log;
        if (control is null)
        {
            ICameraPopupControl? lazy = null;
            var gate = new object();
            control = () =>
            {
                if (host is null) return null;
                lock (gate) return lazy ??= new CameraPopupControl(host, log);
            };
        }
        _control = control;
        if (_host is not null) _host.StateChanged += OnHostStateChanged;
    }

    /// <summary>DI 용.</summary>
    public static HostMappingCameraVideo FromHost(
        ICameraPopupHost? host,
        IEventWindowDeviceDirectory directory,
        ICameraPopupSettingsSource? settings,
        ILogService? log)
        => new(host, directory, settings is null ? null : () => settings.Current, log);

    /// <inheritdoc/>
    public event EventHandler? HostStateChanged;

    /// <inheritdoc/>
    public MappingVideoHostState HostState
    {
        get
        {
            try { return Map(_host?.State); }
            catch (Exception) { return MappingVideoHostState.Unavailable; }
        }
    }

    /// <summary>호스트 상태 → 화면 상태(순수). 시작 전 · 일시 중지 · 없음은 "쓸 수 없음" (지도 팝업과 같다).</summary>
    public static MappingVideoHostState Map(CameraPopupHostState? state) => state switch
    {
        CameraPopupHostState.Running => MappingVideoHostState.Running,
        CameraPopupHostState.Starting or CameraPopupHostState.Restarting => MappingVideoHostState.Starting,
        _ => MappingVideoHostState.Unavailable,
    };

    /// <inheritdoc/>
    public MappingCallResult<IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height)
        => OpenVideo(cameraId, streamId, width, height, fillBox: false);

    /// <inheritdoc/>
    /// <remarks>꽉 채우기는 제공자 정보(<see cref="VideoProviderInfo.FillBox"/>)에 실어 호스트로 보낸다 — 호스트가 상자 크기로 디코딩한다.</remarks>
    public MappingCallResult<IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height, bool fillBox)
    {
        try
        {
            if (_host is null) return Fail("영상 기능을 쓸 수 없습니다.");
            var camera = _directory.FindCamera(cameraId);
            if (camera is null) return Fail("장비 목록에서 이 카메라를 찾지 못해 영상을 열 수 없습니다.");

            var w = Math.Clamp(width, 16, SharedFrameLayout.MaxWidth) & ~1;
            var h = Math.Clamp(height, 16, SharedFrameLayout.MaxHeight) & ~1;
            var source = _host.OpenOverlay(new OverlayStreamRequest
            {
                StreamId = streamId,
                Camera = new CameraRef
                {
                    CameraId = camera.Id.ToString(CultureInfo.InvariantCulture),
                    Name = string.IsNullOrWhiteSpace(camera.DeviceName) ? $"카메라 {camera.Id}" : camera.DeviceName,
                },
                Provider = EventWindowPlanning.BuildProvider(camera, ReadSettings()).WithFillBox(fillBox),
                Width = w,
                Height = h,
            });
            return source is null ? Fail("영상을 열지 못했습니다.") : MappingCallResult<IFrameSource>.Ok(source);
        }
        catch (Exception ex)
        {
            _log?.Error($"[MappingVideo] 영상 열기 예외 cam={cameraId}: {ex.GetType().Name} {ex.Message}");
            return Fail("영상을 열지 못했습니다.", ex.Message);
        }
    }

    /// <inheritdoc/>
    public bool CanGotoPreset(int cameraId)
    {
        try { return _host is not null && _directory.FindCamera(cameraId)?.Category == EnumCameraType.PTZ; }
        catch (Exception) { return false; }
    }

    /// <inheritdoc/>
    public async Task<MappingCallResult<bool>> GotoPresetAsync(int cameraId, int presetIndex, CancellationToken token = default)
    {
        try
        {
            var camera = _directory.FindCamera(cameraId);
            if (camera is null)
                return MappingCallResult<bool>.Fail("장비 목록에서 이 카메라를 찾지 못해 이동할 수 없습니다.");
            if (camera.Category != EnumCameraType.PTZ)
                return MappingCallResult<bool>.Fail("PTZ 카메라가 아니라 프리셋 자리로 이동할 수 없습니다.");

            var control = _control();
            if (control is null || !control.IsAvailable)
                return MappingCallResult<bool>.Fail("영상 기능을 쓸 수 없어 카메라를 움직이지 못했습니다.");

            // 이벤트 경로와 같다 — 서버 preset_index 를 문자열로 바꿔 ONVIF 토큰으로 보낸다.
            var response = await control.RequestAsync(new CameraRequest
            {
                Kind = CameraRequestKind.GotoPreset,
                CameraId = camera.Id.ToString(CultureInfo.InvariantCulture),
                Provider = EventWindowPlanning.BuildProvider(camera, ReadSettings()),
                PresetToken = presetIndex.ToString(CultureInfo.InvariantCulture),
            }, token).ConfigureAwait(false);

            if (response.Success) return MappingCallResult<bool>.Ok(true);
            _log?.Warning($"[MappingVideo] 프리셋 이동 실패 cam={cameraId} preset={presetIndex}: {response.ErrorCode} {response.Message}");
            return MappingCallResult<bool>.Fail(response.ErrorCode switch
            {
                CameraErrorCodes.Timeout => "카메라가 응답하지 않아 프리셋 자리로 이동하지 못했습니다.",
                CameraErrorCodes.NotSupported => "이 카메라는 ONVIF 프리셋 이동을 지원하지 않습니다.",
                CameraErrorCodes.HostUnavailable or CameraErrorCodes.HostRestarted => "영상 기능을 쓸 수 없어 카메라를 움직이지 못했습니다.",
                _ => "프리셋 자리로 이동하지 못했습니다. 카메라에 그 번호의 프리셋이 있는지 확인하세요.",
            }, response.Message);
        }
        catch (Exception ex)
        {
            _log?.Error($"[MappingVideo] 프리셋 이동 예외 cam={cameraId}: {ex.GetType().Name} {ex.Message}");
            return MappingCallResult<bool>.Fail("프리셋 자리로 이동하지 못했습니다.", ex.Message);
        }
    }

    /// <inheritdoc/>
    /// <remarks>호스트에 <see cref="CameraRequestKind.GetVideoResolution"/> 를 보낸다 — 호스트가 ONVIF 프로파일(준비 때 받은 캐시)에서 답한다.</remarks>
    public async Task<MappingCallResult<MappingVideoResolution>> ReadVideoResolutionAsync(int cameraId, CancellationToken token = default)
    {
        try
        {
            var camera = _directory.FindCamera(cameraId);
            if (camera is null)
                return MappingCallResult<MappingVideoResolution>.Fail("장비 목록에서 이 카메라를 찾지 못해 해상도를 읽을 수 없습니다.");

            var control = _control();
            if (control is null || !control.IsAvailable)
                return MappingCallResult<MappingVideoResolution>.Fail("영상 기능을 쓸 수 없어 카메라 해상도를 읽지 못했습니다.");

            var response = await control.RequestAsync(new CameraRequest
            {
                Kind = CameraRequestKind.GetVideoResolution,
                CameraId = camera.Id.ToString(CultureInfo.InvariantCulture),
                Provider = EventWindowPlanning.BuildProvider(camera, ReadSettings()),
            }, token).ConfigureAwait(false);

            if (response.Success && response.VideoWidth > 0 && response.VideoHeight > 0)
                return MappingCallResult<MappingVideoResolution>.Ok(new MappingVideoResolution(response.VideoWidth, response.VideoHeight));

            _log?.Info($"[MappingVideo] 카메라 해상도 읽기 실패 cam={cameraId}: {response.ErrorCode} {response.Message}");
            return MappingCallResult<MappingVideoResolution>.Fail("카메라 해상도를 읽지 못했습니다.", response.Message);
        }
        catch (Exception ex)
        {
            _log?.Error($"[MappingVideo] 카메라 해상도 읽기 예외 cam={cameraId}: {ex.GetType().Name} {ex.Message}");
            return MappingCallResult<MappingVideoResolution>.Fail("카메라 해상도를 읽지 못했습니다.", ex.Message);
        }
    }

    private CameraPopupSettings ReadSettings()
    {
        try { return _settings(); }
        catch (Exception) { return new CameraPopupSettings().Normalize(); }
    }

    private void OnHostStateChanged(object? sender, CameraPopupHostStateChangedEventArgs e)
    {
        var handler = HostStateChanged;
        if (handler is null) return;
        foreach (EventHandler h in handler.GetInvocationList())
        {
            try { h(this, EventArgs.Empty); }
            catch (Exception ex) { _log?.Error($"[MappingVideo] 호스트 상태 알림 처리 실패: {ex.GetType().Name} {ex.Message}"); }
        }
    }

    private static MappingCallResult<IFrameSource> Fail(string message, string? raw = null)
        => MappingCallResult<IFrameSource>.Fail(message, raw);
}
