using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;

/****************************************************************************
   Purpose      : 기존 장비의 부품 구성 적용 — 다시 받기 → 비교 → PATCH 1건 (FR-15)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 적용 결과. <see cref="IsConflict"/> 면 <b>아무것도 보내지 않았다</b> — 그 사이 다른 쪽이 부품을 바꿨다.
/// </summary>
public sealed record ComponentApplyResult(bool IsSuccess, bool IsConflict, string Message);

/// <summary>
/// 조립 보드의 결과를 <b>이미 있는 장비</b>에 적용한다(FR-15) — 다시 받아 비교하고, PATCH <b>한 건</b>을 보낸다.
/// </summary>
/// <remarks>
/// <para><b>왜 다시 받는가</b> — 서버는 <c>PATCH</c> 에서도 <c>components</c> <b>배열을 통째 교체</b>하고
/// 빠진 부품을 그 관측·설정과 함께 <b>경고 없이</b> 지운다(8.0.1 실측 2026-09-19). 우리가 보드를 연 뒤에
/// 다른 클라(또는 <c>SYNC_DEVICE</c>)가 부품을 더했다면, 우리 배열을 보내는 순간 그 부품이 사라진다.
/// 그래서 <b>보내기 직전</b>에 다시 받아 기준선과 비교하고, 달라졌으면 <b>멈춘다</b>.</para>
/// <para><b>본문을 좁게 유지한다</b> — <c>hardware_spec.components</c> 와
/// <c>device_config.component_overrides</c> 말고는 새로 싣지 않는다. 축은 <c>PATCH</c> 에서 <b>객체 병합</b>이라
/// 안 보낸 키가 보존되지만 <b>배열은 교체</b>다. 다만 축 모드에서 조건 없이 직렬화되는 키
/// (제어기·카메라·경광등의 <c>connection</c>, 함체·카메라의 <c>device_config</c>, 스피커의 <c>speaker_role</c>,
/// <b>스피커·경광등의 <c>description</c></b>)는 뺄 수단이 없어 <b>방금 받은 값으로 채워</b>
/// 되돌림이 아니라 제자리 유지가 되게 한다.</para>
/// <para><b>전수 감사(2026-09-19)</b> — 일곱 카테고리를 모두 훑어 "본문에 null 이 없고 실린 키가 받은 값과 같은가"를
/// 못 박았다(<c>PresetRegisterTests.should_never_null_or_reset_a_fetched_value_in_the_apply_body</c>).
/// 그때 다섯이 걸렸다 — 스피커·경광등 <c>description</c>(<b>null 전송 = 서버 삭제</b>) ·
/// 제어기·카메라 <c>connection</c>(빈 껍데기) · 함체 <c>device_config.thresholds</c> ·
/// 카메라 <c>device_config.modes</c>. 통문의 <c>connection</c> 은 null 이면 키째 빠져 병합에서는 무해했지만,
/// 같은 이유로 받은 값을 채워 통째 교체 해석에도 견디게 했다.</para>
/// </remarks>
public sealed class ComponentApplyService
{
    #region - Ctors -
    /// <param name="policy">
    /// 서버 계약 정책. <c>null</c> 이면 <see cref="DeviceQueryPolicy.Resolve()"/> — 패널이 쓰는 그 방식 그대로다
    /// (컨테이너가 없으면 6.3 폴백이라 <see cref="ApplyAsync"/> 가 보내지 않고 막힌다 — 안전한 방향).
    /// </param>
    public ComponentApplyService(
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
    /// ① 그 장비 한 건을 다시 받는다 ② 기준선과 다르면 멈춘다 ③ <b>원하는 배열 전체</b>를 PATCH ④ 재조회.
    /// </summary>
    /// <param name="device">대상 장비(콘솔 상세가 들고 있는 모델).</param>
    /// <param name="baseline">보드를 열 때 받았던 부품 배열 — 이것과 서버가 지금 달라졌으면 경합이다.</param>
    /// <param name="desired">보내려는 <b>전체</b> 배열. 일부만 보내면 나머지가 지워진다.</param>
    /// <param name="overridesToSend">뺀 key 마다 명시적 JSON <c>null</c> 이 이미 들어 있는 재정의 묶음(FR-07).</param>
    public async Task<ComponentApplyResult> ApplyAsync(
        IBaseDeviceModel device,
        IReadOnlyList<ComponentDefinitionModel> baseline,
        IReadOnlyList<ComponentDefinitionModel> desired,
        JObject? overridesToSend,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(desired);

        var category = DeviceAxesMapper.CategoryOf(device);
        var id = device.Id;

        try
        {
            token.ThrowIfCancellationRequested();

            // 축 계약이 아니면 한 줄도 보내지 않는다 — 다시 받기조차 하지 않는다(AssemblyWriteGuard).
            if (AssemblyWriteGuard.IsBlocked(_policy))
            {
                _log?.Warning($"[{nameof(ApplyAsync)}] 서버 계약 {_policy.Contract} — 부품 적용을 보내지 않았습니다.");
                return new ComponentApplyResult(false, false, AssemblyWriteGuard.LEGACY_CONTRACT_MESSAGE);
            }

            if (id <= 0)
                return new ComponentApplyResult(false, false, "아직 서버에 없는 장비입니다 — 먼저 등록해야 부품을 바꿀 수 있습니다.");

            var (fetched, fetchMessage) = await RefetchAsync(category, id, token).ConfigureAwait(false);
            if (fetched == null) return new ComponentApplyResult(false, false, fetchMessage);

            var server = SpecOf(fetched)?.Components;
            if (!SameComponents(baseline, server))
            {
                _log?.Warning($"[{nameof(ApplyAsync)}] Id={id} 부품 구성이 그 사이 바뀌어 적용을 멈췄습니다.");
                return new ComponentApplyResult(false, true,
                    "보드를 연 뒤에 이 장비의 부품 구성이 바뀌었습니다 — 아무것도 보내지 않았습니다. 다시 받아 확인한 뒤 적용하십시오.");
            }

            var (ok, message) = await PatchAsync(category, id, fetched, desired, overridesToSend, token).ConfigureAwait(false);
            if (!ok)
            {
                _log?.Warning($"[{nameof(ApplyAsync)}] Id={id} 부품 적용 실패: {message}");
                return new ComponentApplyResult(false, false, message);
            }

            await _providerService.FetchAllDevicesAsync(token).ConfigureAwait(false);
            return new ComponentApplyResult(true, false, $"부품 구성을 적용했습니다(부품 {desired.Count}개).");
        }
        catch (OperationCanceledException)
        {
            return new ComponentApplyResult(false, false, "적용이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ApplyAsync)}] {ex.Message}");
            return new ComponentApplyResult(false, false, ex.Message);
        }
    }
    #endregion

    #region - Refetch -
    /// <summary>
    /// 그 장비 한 건을 다시 받는다. 단건 조회는 서버 기본이 이미 <c>meta.view=full</c> 이라
    /// <c>?view=full</c> 을 붙이지 않는다(<see cref="DeviceQueryPolicy.View"/> remarks — 목록에만 붙인다).
    /// </summary>
    private async Task<(BaseDeviceDto? Dto, string Message)> RefetchAsync(
        EnumDeviceCategory category, int id, CancellationToken token)
    {
        switch (category)
        {
            case EnumDeviceCategory.Controller:
                return Read(await _api.GetControllerByIdAsync(id, false, token).ConfigureAwait(false));
            case EnumDeviceCategory.Sensor:
                return Read(await _api.GetSensorByIdAsync(id, false, token).ConfigureAwait(false));
            case EnumDeviceCategory.Camera:
                return Read(await _api.GetCameraByIdAsync(id, token).ConfigureAwait(false));
            case EnumDeviceCategory.Speaker:
                return Read(await _api.GetSpeakerByIdAsync(id, token).ConfigureAwait(false));
            case EnumDeviceCategory.Enclosure:
                return Read(await _api.GetEnclosureByIdAsync(id, token).ConfigureAwait(false));
            case EnumDeviceCategory.Lamp:
                return Read(await _api.GetLampByIdAsync(id, token).ConfigureAwait(false));
            case EnumDeviceCategory.Gate:
                return Read(await _api.GetGateByIdAsync(id, token).ConfigureAwait(false));
            default:
                throw new ArgumentException($"부품을 바꿀 수 없는 카테고리입니다: {category}", nameof(category));
        }

        static (BaseDeviceDto?, string) Read<T>(ApiResponse<T> response) where T : BaseDeviceDto
        {
            if (response.Success && response.Data != null) return (response.Data, response.Message);
            return (null, ApiErrorTextHelper.FromFieldErrorsMultiline(response.Error)
                          ?? ApiErrorTextHelper.Resolve(response.Error, response.Message, "장비를 다시 받지 못했습니다."));
        }
    }

    private static HardwareSpecDto? SpecOf(BaseDeviceDto dto) => dto switch
    {
        ControllerDeviceDto d => d.HardwareSpec,
        SensorDeviceDto d => d.HardwareSpec,
        CameraDeviceDto d => d.HardwareSpec,
        SpeakerDeviceDto d => d.HardwareSpec,
        EnclosureDeviceDto d => d.HardwareSpec,
        LampDeviceDto d => d.HardwareSpec,
        GateDeviceDto d => d.HardwareSpec,
        _ => null,
    };
    #endregion

    #region - Compare -
    /// <summary>
    /// 기준선과 서버의 현재 배열이 같은가 — <b>key 집합 + key 마다의 사실</b>로 본다. 순서는 보지 않는다
    /// (서버 <c>components[]</c> 에 순서 계약이 없다 — FR-05).
    /// </summary>
    internal static bool SameComponents(IReadOnlyList<ComponentDefinitionModel> baseline, List<ComponentDefinitionDto>? server)
    {
        var left = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in baseline)
        {
            if (c == null) continue;
            left[c.Key ?? string.Empty] = Fingerprint(PresetRequestBuilder.ToComponentDto(c));
        }

        var right = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in server ?? new List<ComponentDefinitionDto>())
        {
            if (c == null) continue;
            right[c.Key ?? string.Empty] = Fingerprint(c);
        }

        if (left.Count != right.Count) return false;
        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other) || !string.Equals(value, other, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 부품 한 개의 사실을 한 줄로 — DTO 직렬화를 그대로 쓴다(필드가 늘어도 비교가 같이 늘어난다).
    /// </summary>
    private static string Fingerprint(ComponentDefinitionDto dto) => JsonConvert.SerializeObject(dto);
    #endregion

    #region - Patch -
    /// <summary>
    /// 원하는 배열 <b>전체</b> + 재정의를 PATCH 한 건으로 보낸다.
    /// </summary>
    /// <remarks>
    /// <para>새 DTO 로 만든다 — 방금 받은 DTO 를 그대로 되보내면 부품과 무관한 축까지 전부 본문에 실린다.
    /// 대신 축 모드에서 <b>조건 없이 직렬화되는</b> 키만 받은 값으로 채운다.</para>
    /// <para><b>"안 채우면 생략된다"가 아니다</b>(2026-09-19 감사) — 조건 없이 나가는 키는 두 부류다.
    /// ① <c>speaker.description</c>·<c>lamp.description</c> 처럼 <c>NullValueHandling</c> 도
    /// <c>ShouldSerialize</c> 도 없는 것은 <b><c>"description": null</c> 로 나가고</b>,
    /// <c>PATCH</c> 는 RFC 7396 병합이라 <b>null 은 삭제</b>다 — 안 채우면 설명이 서버에서 지워진다.
    /// ② <c>connection</c>·<c>device_config</c> 처럼 계산으로 조립되는 축은 <b>빈 껍데기</b>로 나간다.
    /// 객체 병합이라 오늘은 살아남지만, 축 하나를 통째 교체로 읽는 경로(<c>PUT</c>)에서는 그대로 손실이다.
    /// 그래서 <b>받은 값으로 채워</b> 어느 해석에서도 제자리 유지가 되게 한다.</para>
    /// <para>남은 한계 — <c>connection</c> 은 평면 필드에서 <b>재조립</b>되므로 받은 원본 그대로는 실을 수 없다.
    /// <c>type</c> 이 <c>IP_DIRECT</c> 로 고정되고 <c>channel</c>·<c>parent_device_id</c> 는 재현되지 않는다
    /// (통문만 <c>link_info</c> 를 통해 살아난다). 고치려면 DTO 쪽에 "받은 축을 그대로 되싣는" 통로가 필요하다.</para>
    /// </remarks>
    private async Task<(bool Ok, string Message)> PatchAsync(
        EnumDeviceCategory category,
        int id,
        BaseDeviceDto source,
        IReadOnlyList<ComponentDefinitionModel> desired,
        JObject? overridesToSend,
        CancellationToken token)
    {
        var spec = new HardwareSpecDto
        {
            // 스칼라는 하나도 싣지 않는다(축 모드에서 빈 문자열은 전부 생략된다) — 배열만 교체한다.
            Components = ToDtoList(desired),
            UseAxisWrite = true,
            AllowComponentsWrite = true,
        };

        var carrier = overridesToSend is { Count: > 0 }
            ? new DeviceConfigAxisDto { ComponentOverrides = (JObject)overridesToSend.DeepClone() }
            : null;

        switch (category)
        {
            case EnumDeviceCategory.Controller:
            {
                var origin = (ControllerDeviceDto)source;
                var dto = new ControllerDeviceDto();
                CopyCommon(source, dto, carrier);
                // 기본 생성자가 넣은 "Controller" 를 지운다 — 안 지우면 IoController 의 종류가 덮인다.
                dto.TypeDevice = string.Empty;
                // connection 은 축 모드에서 무조건 나간다 — 평면 두 칸이 그 축의 유일한 재료다.
                dto.IpAddress = origin.IpAddress;
                dto.IpPort = origin.IpPort;
                dto.HardwareSpec = spec;
                return await Send(dto, _api.PatchControllerAsync).ConfigureAwait(false);
            }

            case EnumDeviceCategory.Sensor:
            {
                var dto = new SensorDeviceDto();
                CopyCommon(source, dto, carrier);
                dto.TypeDevice = string.Empty;   // type_sensor 는 값이 없으면 나가지 않는다
                dto.HardwareSpec = spec;
                return await Send(dto, _api.PatchSensorAsync).ConfigureAwait(false);
            }

            case EnumDeviceCategory.Camera:
            {
                var origin = (CameraDeviceDto)source;
                var dto = new CameraDeviceDto();
                CopyCommon(source, dto, carrier);
                dto.TypeDevice = string.Empty;
                // connection 은 축 모드에서 무조건 나가고 protocol 이 필수다 — 비우면 "NONE" 으로 덮인다.
                dto.Mode = origin.Mode;
                dto.IpAddress = origin.IpAddress;
                dto.IpPort = origin.IpPort;
                dto.UserName = origin.UserName;
                dto.UserPassword = origin.UserPassword;
                dto.Urls = origin.Urls;
                // device_config 는 카메라만 AllowDeviceConfigWrite 없이도 나간다 — 모드 묶음이 같은 축이라
                // 재정의만 담아 보내면 통째 교체 해석에서 day_night_mode 따위가 통째로 사라진다.
                dto.IsRecord = origin.IsRecord;
                dto.DeviceConfigModes = origin.DeviceConfigModes;
                dto.HardwareSpec = spec;
                return await Send(dto, _api.PatchCameraAsync).ConfigureAwait(false);
            }

            case EnumDeviceCategory.Speaker:
            {
                var origin = (SpeakerDeviceDto)source;
                var dto = new SpeakerDeviceDto();
                CopyCommon(source, dto, carrier);
                dto.TypeDevice = string.Empty;
                // speaker_role 은 무조건 나가고 기본값이 "NORMAL" 이다 — 비우면 ADMIN 스피커가 NORMAL 로 덮인다.
                dto.SpeakerType = origin.SpeakerType;
                // description 은 조건이 하나도 없어 null 이면 "description": null 로 나간다 = 서버에서 삭제.
                dto.Description = origin.Description;
                dto.HardwareSpec = spec;
                return await Send(dto, _api.PatchSpeakerAsync).ConfigureAwait(false);
            }

            case EnumDeviceCategory.Enclosure:
            {
                var origin = (EnclosureDeviceDto)source;
                var dto = new EnclosureDeviceDto();
                CopyCommon(source, dto, carrier);
                dto.TypeDevice = string.Empty;
                // device_config 는 축 모드에서 무조건 나간다 — 선언된 히터·팬의 의도를 받은 값 그대로 유지한다.
                dto.HeaterEnabled = origin.HeaterEnabled;
                dto.FanEnabled = origin.FanEnabled;
                // 임계치도 같은 축이다 — 빼고 보내면 통째 교체 해석에서 온도·습도 임계치가 전부 사라진다.
                dto.ThresholdConfig = origin.ThresholdConfig;
                dto.HardwareSpec = spec;
                return await Send(dto, _api.PatchEnclosureAsync).ConfigureAwait(false);
            }

            case EnumDeviceCategory.Lamp:
            {
                var origin = (LampDeviceDto)source;
                var dto = new LampDeviceDto();
                CopyCommon(source, dto, carrier);
                dto.TypeDevice = string.Empty;
                // 스피커와 같은 이유 — 조건 없는 description 은 안 채우면 null 로 나가 서버에서 지워진다.
                dto.Description = origin.Description;
                dto.IpAddress = origin.IpAddress;
                dto.IpPort = origin.IpPort;
                dto.UserName = origin.UserName;
                dto.UserPassword = origin.UserPassword;
                dto.HardwareSpec = spec;
                return await Send(dto, _api.PatchLampAsync).ConfigureAwait(false);
            }

            case EnumDeviceCategory.Gate:
            {
                var origin = (GateDeviceDto)source;
                var dto = new GateDeviceDto();
                CopyCommon(source, dto, carrier);
                dto.TypeDevice = string.Empty;
                // 통문의 connection 은 link_info·urls 에서 조립된다 — 둘 다 없으면 축이 통째로 빠진다.
                dto.Urls = origin.Urls;
                dto.LinkInfo = origin.LinkInfo;
                dto.HardwareSpec = spec;
                return await Send(dto, _api.PatchGateAsync).ConfigureAwait(false);
            }

            default:
                throw new ArgumentException($"부품을 바꿀 수 없는 카테고리입니다: {category}", nameof(category));
        }

        // 보내기 직전 단 한 자리 — 등록(PresetRegistrar)과 같은 관문을 지나야 8.0 에서 부대가 바뀌지 않는다.
        // unit_id 를 생략하면 서버가 장비를 기본 부대로 재귀속시키고 응답에는 아무 신호도 남기지 않는다.
        async Task<(bool Ok, string Message)> Send<T>(
            T dto, Func<int, T, CancellationToken, Task<ApiResponse<T>>> patch) where T : BaseDeviceDto
        {
            await UnitScopeGate.StampAsync(dto, nameof(ApplyAsync), _log, token).ConfigureAwait(false);
            return Read(await patch(id, dto, token).ConfigureAwait(false));
        }

        static (bool, string) Read<T>(ApiResponse<T> response) where T : BaseDeviceDto
        {
            if (response.Success) return (true, response.Message);
            return (false, ApiErrorTextHelper.FromFieldErrorsMultiline(response.Error)
                           ?? ApiErrorTextHelper.Resolve(response.Error, response.Message, "부품 구성 적용에 실패했습니다."));
        }
    }

    /// <summary>
    /// PATCH 가 빈 값으로 덮지 않도록, <b>축 모드에서 무조건 나가는</b> 공통 키만 받은 값으로 채운다.
    /// <c>id</c>·<c>geolocation</c>·<c>group_ids</c> 는 건드리지 않는다(부품과 무관한 축).
    /// </summary>
    private static void CopyCommon(BaseDeviceDto source, BaseDeviceDto target, DeviceConfigAxisDto? carrier)
    {
        target.NumberDevice = source.NumberDevice;
        target.NameDevice = source.NameDevice;
        target.Status = source.Status;
        target.IsEnable = source.IsEnable;

        target.UseAxisWrite = true;
        if (carrier != null)
        {
            target.AllowDeviceConfigWrite = true;
            target.DeviceConfigWrite = carrier;
        }
    }

    private static List<ComponentDefinitionDto> ToDtoList(IReadOnlyList<ComponentDefinitionModel> components)
    {
        var list = new List<ComponentDefinitionDto>(components.Count);
        foreach (var c in components)
        {
            if (c == null) continue;
            list.Add(PresetRequestBuilder.ToComponentDto(c));
        }
        return list;
    }
    #endregion

    #region - Attributes -
    private readonly IDeviceApiService _api;
    private readonly IDeviceProviderService _providerService;
    private readonly ILogService? _log;
    private readonly DeviceQueryPolicy _policy;
    #endregion
}
