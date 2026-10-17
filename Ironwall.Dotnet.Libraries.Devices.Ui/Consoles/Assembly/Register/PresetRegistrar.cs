using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;

/****************************************************************************
   Purpose      : 프리셋으로 장비 등록 — POST 1건 → 재조회 (device-assembly-preset FR-12·FR-14)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>등록 결과. 실패면 <see cref="Message"/> 가 <b>서버가 준 문장 그대로</b>다(스택 트레이스 금지).</summary>
public sealed record PresetRegisterResult(bool IsSuccess, int? NewDeviceId, string Message);

/// <summary>
/// <see cref="PresetRequest"/> 를 <b>POST 한 건</b>으로 보낸다(FR-12). 성공하면 프로바이더를 다시 읽는다(FR-14).
/// </summary>
/// <remarks>
/// <para><b>왕복 1회가 안전장치다</b> — 부품마다 PATCH 를 날리는 구조라면 중간에 끊겼을 때
/// <b>반쯤 조립된 장비</b>가 서버에 남는다. POST 한 건이면 성공 아니면 아무것도 없음이다(AS L351).</para>
/// <para>실패하면 <b>재조회도 하지 않는다</b> — 창은 그대로 두고 입력을 보존한다(FR-14).</para>
/// </remarks>
public sealed class PresetRegistrar
{
    #region - Ctors -
    /// <param name="policy">
    /// 서버 계약 정책. <c>null</c> 이면 <see cref="DeviceQueryPolicy.Resolve()"/> — 패널이 쓰는 그 방식 그대로다
    /// (컨테이너가 없으면 6.3 폴백이라 <see cref="RegisterAsync"/> 가 보내지 않고 막힌다 — 안전한 방향).
    /// </param>
    public PresetRegistrar(
        IDeviceApiService api,
        IDeviceProviderService providerService,
        ILogService? log = null,
        DeviceQueryPolicy? policy = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        _log = log;
        _policy = policy ?? DeviceQueryPolicy.Resolve();
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// 요청의 카테고리에 맞는 생성 호출 <b>한 번</b>. 성공 → 재조회 후 새 Id, 실패 → 서버 메시지 그대로.
    /// </summary>
    /// <remarks><see cref="ArgumentNullException"/> 말고는 던지지 않는다 — 창이 죽으면 입력이 사라진다.</remarks>
    public async Task<PresetRegisterResult> RegisterAsync(PresetRequest request, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            token.ThrowIfCancellationRequested();

            // 축 계약이 아니면 POST 조차 하지 않는다 — 6.3 으로 나가면 ShapeWrite 가 본문을 평면으로
            // 되돌려 부품은 사라지고 채우지 않은 평면 칸만 기본값으로 실린다(AssemblyWriteGuard).
            if (AssemblyWriteGuard.IsBlocked(_policy))
            {
                _log?.Warning($"[{nameof(RegisterAsync)}] 서버 계약 {_policy.Contract} — 프리셋 등록을 보내지 않았습니다.");
                return new PresetRegisterResult(false, null, AssemblyWriteGuard.LEGACY_CONTRACT_MESSAGE);
            }

            // (8.0 unit_id) 패널이 쓰는 그 관문 그대로 — 8.0 미만이면 관문이 키를 지운다(6.3/7.0 은 422).
            await UnitScopeGate.StampAsync(request.Dto, nameof(RegisterAsync), _log, token).ConfigureAwait(false);

            var (success, id, statusCode, message) = await SendAsync(request, token).ConfigureAwait(false);
            if (!success)
            {
                _log?.Warning($"[{nameof(RegisterAsync)}] {request.Category} 등록 실패({statusCode}): {message}");
                return new PresetRegisterResult(false, null, message);
            }

            // (FR-14) 성공 → 프로바이더 재조회. 콘솔이 새 장비를 고를 수 있어야 한다.
            await _providerService.FetchAllDevicesAsync(token).ConfigureAwait(false);
            return new PresetRegisterResult(true, id, "장비를 등록했습니다.");
        }
        catch (OperationCanceledException)
        {
            return new PresetRegisterResult(false, null, "등록이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            // 예외 메시지는 남기되 창에는 문장만 — 스택 트레이스를 사용자에게 보이지 않는다.
            _log?.Error($"[{nameof(RegisterAsync)}] {ex.Message}");
            return new PresetRegisterResult(false, null, "장비를 등록하지 못했습니다. 서버 연결을 확인하고 다시 시도하세요.");
        }
    }

    /// <summary>카테고리 → 생성 엔드포인트. 여기 말고 다른 곳에서 장비를 만들지 않는다.</summary>
    private async Task<(bool Success, int? Id, int StatusCode, string Message)> SendAsync(
        PresetRequest request, CancellationToken token)
    {
        switch (request.Category)
        {
            case EnumDeviceCategory.Controller:
                return Read(await _api.CreateControllerAsync(Cast<ControllerDeviceDto>(request), token).ConfigureAwait(false), "제어기");
            case EnumDeviceCategory.Sensor:
                return Read(await _api.CreateSensorAsync(Cast<SensorDeviceDto>(request), token).ConfigureAwait(false), "센서");
            case EnumDeviceCategory.Camera:
                return Read(await _api.CreateCameraAsync(Cast<CameraDeviceDto>(request), token).ConfigureAwait(false), "카메라");
            case EnumDeviceCategory.Speaker:
                return Read(await _api.CreateSpeakerAsync(Cast<SpeakerDeviceDto>(request), token).ConfigureAwait(false), "스피커");
            case EnumDeviceCategory.Enclosure:
                return Read(await _api.CreateEnclosureAsync(Cast<EnclosureDeviceDto>(request), token).ConfigureAwait(false), "함체");
            case EnumDeviceCategory.Lamp:
                return Read(await _api.CreateLampAsync(Cast<LampDeviceDto>(request), token).ConfigureAwait(false), "경광등");
            case EnumDeviceCategory.Gate:
                return Read(await _api.CreateGateAsync(Cast<GateDeviceDto>(request), token).ConfigureAwait(false), "통문");
            default:
                throw new ArgumentException($"프리셋으로 등록할 수 없는 카테고리입니다: {request.Category}", nameof(request));
        }
    }

    private static T Cast<T>(PresetRequest request) where T : BaseDeviceDto
        => request.Dto as T
           ?? throw new ArgumentException(
               $"요청의 카테고리({request.Category})와 DTO 형식({request.Dto.GetType().Name})이 맞지 않습니다.", nameof(request));

    /// <summary>응답 한 건 → (성공 여부 · 새 Id · 사람이 읽는 문장). 422 다필드는 줄바꿈으로 전건 표기(패널과 같은 규칙).</summary>
    private static (bool Success, int? Id, int StatusCode, string Message) Read<T>(ApiResponse<T> response, string what)
        where T : BaseDeviceDto
    {
        var ok = response.Success && response.Data != null;
        if (ok) return (true, response.Data!.Id, response.StatusCode, response.Message);

        var message = ApiErrorTextHelper.FromFieldErrorsMultiline(response.Error)
                      ?? ApiErrorTextHelper.Resolve(response.Error, response.Message, $"{what} 등록에 실패했습니다.");
        return (false, null, response.StatusCode, message);
    }
    #endregion

    #region - Attributes -
    private readonly IDeviceApiService _api;
    private readonly IDeviceProviderService _providerService;
    private readonly ILogService? _log;
    private readonly DeviceQueryPolicy _policy;
    #endregion
}
