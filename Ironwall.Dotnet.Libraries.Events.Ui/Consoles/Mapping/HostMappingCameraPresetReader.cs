using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 프리셋 관리 창 — 카메라 ONVIF 프리셋 읽기(카메라 팝업 호스트 GetPresets)
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 카메라 팝업 호스트에 <see cref="CameraRequestKind.GetPresets"/> 를 보내 카메라의 프리셋을 읽는다.
/// </summary>
/// <remarks>
/// <para>제공자 정보는 <b>이벤트 창과 같은 빌더</b>(<see cref="EventWindowPlanning.BuildProvider"/>)로 만든다 —
/// 이벤트 때 GotoPreset 을 보내는 그 카메라 · 그 계정으로 읽어야 토큰이 맞는다.</para>
/// <para>절대 던지지 않는다. 호스트 · 카메라 · 장비 캐시 어디서 막혀도 사유 한 줄을 담은 실패 결과로 끝난다.</para>
/// </remarks>
public sealed class HostMappingCameraPresetReader : IMappingCameraPresetReader
{
    private readonly Func<ICameraPopupControl?> _control;
    private readonly IEventWindowDeviceDirectory _directory;
    private readonly Func<CameraPopupSettings> _settings;
    private readonly ILogService? _log;

    /// <summary>생성자.</summary>
    /// <param name="control">호스트 창구. <c>null</c> 을 돌려주면 "영상 기능을 쓸 수 없음" 으로 답한다.</param>
    /// <param name="directory">카메라 장비 캐시.</param>
    /// <param name="settings">카메라 팝업 설정(제공자 · 스트림 선택). 생략하면 기본값.</param>
    /// <param name="log">로그.</param>
    public HostMappingCameraPresetReader(
        Func<ICameraPopupControl?> control,
        IEventWindowDeviceDirectory directory,
        Func<CameraPopupSettings>? settings = null,
        ILogService? log = null)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _settings = settings ?? (() => new CameraPopupSettings().Normalize());
        _log = log;
    }

    /// <summary>DI 용 — 호스트 감시자가 있으면 그 위에 창구를 하나 세운다(처음 부를 때).</summary>
    public static HostMappingCameraPresetReader FromHost(
        ICameraPopupHost? host,
        IEventWindowDeviceDirectory directory,
        ICameraPopupSettingsSource? settings,
        ILogService? log)
    {
        ICameraPopupControl? control = null;
        var gate = new object();
        return new HostMappingCameraPresetReader(
            () =>
            {
                if (host is null) return null;
                lock (gate) return control ??= new CameraPopupControl(host, log);
            },
            directory,
            settings is null ? null : () => settings.Current,
            log);
    }

    /// <inheritdoc/>
    public async Task<MappingCallResult<IReadOnlyList<MappingCameraPreset>>> ReadCameraPresetsAsync(int cameraId, CancellationToken token = default)
    {
        try
        {
            var camera = _directory.FindCamera(cameraId);
            if (camera is null)
                return Fail("장비 목록에서 이 카메라를 찾지 못해 카메라 프리셋을 읽을 수 없습니다.");
            if (camera.Category != EnumCameraType.PTZ)
                return Fail("PTZ 카메라가 아니라 카메라 프리셋이 없습니다.");

            var control = _control();
            if (control is null || !control.IsAvailable)
                return Fail("영상 기능을 쓸 수 없어 카메라 프리셋을 읽지 못했습니다.");

            CameraPopupSettings settings;
            try { settings = _settings(); }
            catch (Exception) { settings = new CameraPopupSettings().Normalize(); }

            var response = await control.RequestAsync(new CameraRequest
            {
                Kind = CameraRequestKind.GetPresets,
                CameraId = camera.Id.ToString(CultureInfo.InvariantCulture),
                Provider = EventWindowPlanning.BuildProvider(camera, settings),
            }, token).ConfigureAwait(false);

            if (!response.Success)
            {
                _log?.Warning($"[MappingPresets] 카메라 프리셋 읽기 실패 cam={cameraId}: {response.ErrorCode} {response.Message}");
                return Fail(response.ErrorCode switch
                {
                    CameraErrorCodes.Timeout => "카메라가 응답하지 않아 카메라 프리셋을 읽지 못했습니다.",
                    CameraErrorCodes.NotSupported => "이 카메라는 ONVIF 프리셋을 지원하지 않습니다.",
                    CameraErrorCodes.HostUnavailable or CameraErrorCodes.HostRestarted => "영상 기능을 쓸 수 없어 카메라 프리셋을 읽지 못했습니다.",
                    _ => "카메라 프리셋을 읽지 못했습니다.",
                }, response.Message);
            }

            var presets = (response.Presets ?? new List<CameraPreset>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Token))
                .Select(p => new MappingCameraPreset(p.Token.Trim(), p.Name?.Trim() ?? string.Empty))
                .ToList();
            return MappingCallResult<IReadOnlyList<MappingCameraPreset>>.Ok(presets);
        }
        catch (Exception ex)
        {
            _log?.Error($"[MappingPresets] 카메라 프리셋 읽기 예외 cam={cameraId}: {ex.GetType().Name} {ex.Message}");
            return Fail("카메라 프리셋을 읽지 못했습니다.", ex.Message);
        }
    }

    private static MappingCallResult<IReadOnlyList<MappingCameraPreset>> Fail(string message, string? raw = null)
        => MappingCallResult<IReadOnlyList<MappingCameraPreset>>.Fail(message, raw);
}
