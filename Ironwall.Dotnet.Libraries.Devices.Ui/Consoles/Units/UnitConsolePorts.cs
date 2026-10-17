using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Devices;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 콘솔이 서버를 보는 좁은 창구 (N-11)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부대 콘솔이 쓰는 <b>부대 쪽 창구</b>. <see cref="IUnitApiService"/> 를 그대로 쓰지 않고 한 겹 두는 이유는
/// 두 가지다 — ① 기존 인터페이스를 넓히지 않고(수십 개의 가짜 구현이 깨진다) ② 테스트가
/// <see cref="IService"/> 수명주기까지 흉내 내지 않아도 되게.
/// </summary>
public interface IUnitGraphApi
{
    /// <summary>이 서버 판본에서 부대 편제 자체가 존재하는가(8.0+). 아니면 콘솔 입구를 내지 않는다.</summary>
    bool IsAvailable { get; }

    Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default);
    Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default);
    Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default);
    Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default);
    Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default);
}

/// <summary>부대 콘솔이 쓰는 <b>장비 쪽 창구</b> — 목록 한 번, 소속 바꾸기 한 대씩.</summary>
public interface IUnitDeviceApi
{
    bool IsAvailable { get; }

    /// <summary>일곱 카테고리를 훑어 장비를 전부 읽는다(부대 필터 없이 한 번 — 트리 장비 수 · 관계도 배지 · [지도에서 보기]의 원천).</summary>
    Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default);

    /// <summary>장비 한 대의 소속 부대를 바꾼다 — 다시 받기 1회 + PATCH 1회. 본문은 늘 <b>1 이상의 부대 id</b> 다(명시적 <c>null</c> 은 422).</summary>
    /// <remarks>
    /// 「미배치 장비」 칸(Q-1 ⓐ 폐지)이 걷힌 뒤 콘솔 화면에는 부르는 곳이 없다. 창구는 남긴다 — 장비 한 대의 소속을 바꾸는
    /// <b>좁은 본문</b>(<see cref="UnitAssignRequestBuilder"/>)의 유일한 경로이고, 라이브 하네스(dl.3d)가 저장된
    /// <c>connection.type</c> 보존을 이 경로로 지킨다.
    /// </remarks>
    Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default);
}

/// <summary>장비 목록의 <c>status</c> 를 부대 콘솔이 쓰는 세 갈래로(FR-03 — 관계도 L2 "▲오류 N" 의 원천, 읽은 시점 스냅샷).</summary>
public enum UnitDeviceStatus
{
    /// <summary><c>ACTIVATED</c>.</summary>
    Normal = 0,

    /// <summary><c>ERROR</c>.</summary>
    Error = 1,

    /// <summary><c>DEACTIVATED</c>.</summary>
    Deactivated = 2,

    /// <summary>없거나 모르는 값 — v1.3 FR-03 "알 수 없음". 배지 수(오류 N)에 넣지 않는다(ISSUE-48).</summary>
    Unknown = 3,
}

/// <summary>장비 한 대 — 부대 콘솔이 아는 만큼만.</summary>
/// <param name="Status">목록의 <c>status</c>(<see cref="UnitDeviceText.StatusOf"/>). 위치 매개변수 <b>기본값</b>이라 기존 생성 지점은 그대로 컴파일된다(NFR-10).</param>
public sealed record UnitDeviceItem(int Id, int NumberDevice, string Name, EnumDeviceCategory Category, int? UnitId,
                                    UnitDeviceStatus Status = UnitDeviceStatus.Unknown)
{
    public string CategoryText => UnitDeviceText.CategoryText(Category);
    public override string ToString() => $"{Name} (#{NumberDevice})";
}

/// <summary>장비 전량 읽기 결과. 일부 카테고리가 실패해도 나머지는 살린다.</summary>
public sealed record UnitDeviceLoadResult(IReadOnlyList<UnitDeviceItem> Items, IReadOnlyList<string> Failures)
{
    public bool HasFailure => Failures.Count > 0;
    public static UnitDeviceLoadResult Empty { get; } = new(Array.Empty<UnitDeviceItem>(), Array.Empty<string>());
}

/// <summary>소속 바꾸기 한 대의 결과.</summary>
public sealed record UnitDeviceAssignResult(bool IsSuccess, string Message);

public static class UnitDeviceText
{
    public static string CategoryText(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Controller => "제어기",
        EnumDeviceCategory.Sensor => "센서",
        EnumDeviceCategory.Camera => "카메라",
        EnumDeviceCategory.Speaker => "스피커",
        EnumDeviceCategory.Enclosure => "함체",
        EnumDeviceCategory.Lamp => "경광등",
        EnumDeviceCategory.Gate => "통문",
        _ => category.ToString(),
    };

    /// <summary>목록 <c>status</c> 문자열 → <see cref="UnitDeviceStatus"/>. 대소문자 · 앞뒤 공백 무시, 없거나 모르면 <b>알 수 없음</b>(v1.3 FR-03).</summary>
    public static UnitDeviceStatus StatusOf(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        "ACTIVATED" => UnitDeviceStatus.Normal,
        "ERROR" => UnitDeviceStatus.Error,
        "DEACTIVATED" => UnitDeviceStatus.Deactivated,
        _ => UnitDeviceStatus.Unknown,
    };
}

/// <summary><see cref="IUnitApiService"/> 위의 얇은 어댑터. 계약 판정은 서비스가 이미 한 번 더 한다(이중 방어).</summary>
public sealed class UnitGraphApiAdapter : IUnitGraphApi
{
    private readonly IUnitApiService? _api;
    private readonly IServerContractProbe? _probe;

    public UnitGraphApiAdapter(IUnitApiService? api, IServerContractProbe? probe)
    {
        _api = api;
        _probe = probe;
    }

    /// <summary>프로브가 없으면 6.3 으로 본다 — 부대 표면이 없는 쪽이 안전하다. 비교는 <c>==</c> 가 아니라 <c>&gt;=</c>.</summary>
    public bool IsAvailable => _api != null && (_probe?.Contract ?? EnumServerContract.V6_3) >= EnumServerContract.V8_0;

    public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
        => _api is null ? Task.FromResult(Refused<UnitGraphDto>()) : _api.GetUnitGraphAsync(null, null, token);

    public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
        => _api is null ? Task.FromResult(Refused<UnitDetailDto>()) : _api.GetUnitAsync(unitId, "parent,children,adjacent", token);

    public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
        => _api is null ? Task.FromResult(Refused<UnitDto>()) : _api.CreateUnitAsync(dto, token);

    public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
        => _api is null ? Task.FromResult(Refused<UnitDto>()) : _api.PatchUnitAsync(unitId, dto, token);

    public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
        => _api is null ? Task.FromResult(Refused<UnitDeleteResultDto>()) : _api.DeleteUnitAsync(unitId, token);

    private static ApiResponse<T> Refused<T>()
        => ApiResponse<T>.CreateError(ApiErrorCodes.EndpointRemoved, "현재 서버는 부대 편제를 지원하지 않습니다.");
}

/// <summary>
/// 장비 창구 — 일곱 카테고리 목록을 병렬로 읽고, 소속 바꾸기는 <b>다시 받아 좁은 본문</b>으로 보낸다.
/// </summary>
/// <remarks>
/// <para>부대마다 따로 묻지 않고 전량을 한 번 읽어 클라가 부대별로 센다(필터축은 <c>unit_id</c>·<c>include_descendants</c> 뿐).
/// 목록은 <b>한 장</b>이 아니라 큰 한도로 한 번에 받는다(limit 100 이 서버 상한).</para>
/// </remarks>
public sealed class UnitDeviceApiAdapter : IUnitDeviceApi
{
    private const int PAGE_LIMIT = 100;
    private const int MAX_PAGES = 40;           // 4,000대 — 그 이상은 이 화면이 다룰 규모가 아니다

    private readonly IDeviceApiService? _api;
    private readonly IServerContractProbe? _probe;
    private readonly ILogService? _log;

    public UnitDeviceApiAdapter(IDeviceApiService? api, IServerContractProbe? probe, ILogService? log = null)
    {
        _api = api;
        _probe = probe;
        _log = log;
    }

    public bool IsAvailable => _api != null && (_probe?.Contract ?? EnumServerContract.V6_3) >= EnumServerContract.V8_0;

    public async Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
    {
        if (_api is null || !IsAvailable) return UnitDeviceLoadResult.Empty;

        var items = new List<UnitDeviceItem>();
        var failures = new List<string>();

        await ReadAsync(EnumDeviceCategory.Controller,
            (page, ct) => Wrap(_api.GetControllersAsync(null, false, page, PAGE_LIMIT, ct)), items, failures, token).ConfigureAwait(false);
        await ReadAsync(EnumDeviceCategory.Sensor,
            (page, ct) => Wrap(_api.GetSensorsAsync(null, null, null, false, page, PAGE_LIMIT, ct)), items, failures, token).ConfigureAwait(false);
        await ReadAsync(EnumDeviceCategory.Camera,
            (page, ct) => Wrap(_api.GetCamerasAsync(null, null, null, page, PAGE_LIMIT, ct)), items, failures, token).ConfigureAwait(false);
        await ReadAsync(EnumDeviceCategory.Speaker,
            (page, ct) => Wrap(_api.GetSpeakersAsync(null, null, page, PAGE_LIMIT, ct)), items, failures, token).ConfigureAwait(false);
        await ReadAsync(EnumDeviceCategory.Enclosure,
            (page, ct) => Wrap(_api.GetEnclosuresAsync(null, null, page, PAGE_LIMIT, ct)), items, failures, token).ConfigureAwait(false);
        await ReadAsync(EnumDeviceCategory.Lamp,
            (page, ct) => Wrap(_api.GetLampsAsync(null, page, PAGE_LIMIT, ct)), items, failures, token).ConfigureAwait(false);
        await ReadAsync(EnumDeviceCategory.Gate,
            (page, ct) => Wrap(_api.GetGatesAsync(null, null, page, PAGE_LIMIT, ct)), items, failures, token).ConfigureAwait(false);

        return new UnitDeviceLoadResult(items, failures);
    }

    public async Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_api is null || !IsAvailable)
            return new UnitDeviceAssignResult(false, "현재 서버는 부대 편제를 지원하지 않습니다.");
        if (device.Id <= 0)
            return new UnitDeviceAssignResult(false, $"'{device.Name}'은(는) 아직 등록되지 않은 장비입니다.");

        try
        {
            var fetched = await RefetchAsync(device, token).ConfigureAwait(false);
            if (fetched == null) return new UnitDeviceAssignResult(false, $"'{device.Name}' 정보를 다시 불러오지 못했습니다.");

            var body = UnitAssignRequestBuilder.Build(fetched, unitId);
            var response = await PatchAsync(device.Category, device.Id, body, token).ConfigureAwait(false);

            return response.Success
                 ? new UnitDeviceAssignResult(true, $"'{device.Name}'의 소속을 바꿨습니다.")
                 : new UnitDeviceAssignResult(false, $"'{device.Name}'의 소속을 바꾸지 못했습니다. {Reason(response)}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[UnitDeviceApi] assign {device.Id} → unit {unitId}: {ex.Message}");
            return new UnitDeviceAssignResult(false, $"'{device.Name}'의 소속을 바꾸지 못했습니다. 서버 연결을 확인하세요.");
        }
    }

    #region - Helpers -
    private async Task<BaseDeviceDto?> RefetchAsync(UnitDeviceItem device, CancellationToken token) => device.Category switch
    {
        EnumDeviceCategory.Controller => Read(await _api!.GetControllerByIdAsync(device.Id, false, token).ConfigureAwait(false)),
        EnumDeviceCategory.Sensor => Read(await _api!.GetSensorByIdAsync(device.Id, false, token).ConfigureAwait(false)),
        EnumDeviceCategory.Camera => Read(await _api!.GetCameraByIdAsync(device.Id, token).ConfigureAwait(false)),
        EnumDeviceCategory.Speaker => Read(await _api!.GetSpeakerByIdAsync(device.Id, token).ConfigureAwait(false)),
        EnumDeviceCategory.Enclosure => Read(await _api!.GetEnclosureByIdAsync(device.Id, token).ConfigureAwait(false)),
        EnumDeviceCategory.Lamp => Read(await _api!.GetLampByIdAsync(device.Id, token).ConfigureAwait(false)),
        EnumDeviceCategory.Gate => Read(await _api!.GetGateByIdAsync(device.Id, token).ConfigureAwait(false)),
        _ => null,
    };

    private async Task<ApiResponse<BaseDeviceDto>> PatchAsync(EnumDeviceCategory category, int id, BaseDeviceDto body, CancellationToken token)
    {
        return category switch
        {
            EnumDeviceCategory.Controller => Up(await _api!.PatchControllerAsync(id, (ControllerDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Sensor => Up(await _api!.PatchSensorAsync(id, (SensorDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Camera => Up(await _api!.PatchCameraAsync(id, (CameraDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Speaker => Up(await _api!.PatchSpeakerAsync(id, (SpeakerDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Enclosure => Up(await _api!.PatchEnclosureAsync(id, (EnclosureDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Lamp => Up(await _api!.PatchLampAsync(id, (LampDeviceDto)body, token).ConfigureAwait(false)),
            EnumDeviceCategory.Gate => Up(await _api!.PatchGateAsync(id, (GateDeviceDto)body, token).ConfigureAwait(false)),
            _ => ApiResponse<BaseDeviceDto>.CreateError(ApiErrorCodes.BadRequest, "소속 부대를 바꿀 수 없는 장비입니다."),
        };

        static ApiResponse<BaseDeviceDto> Up<T>(ApiResponse<T> response) where T : BaseDeviceDto => new()
        {
            Success = response.Success,
            Message = response.Message,
            Data = response.Data,
            Error = response.Error,
            StatusCode = response.StatusCode,
        };
    }

    private static BaseDeviceDto? Read<T>(ApiResponse<T> response) where T : BaseDeviceDto
        => response.Success ? response.Data : null;

    /// <summary>서버 거절 원문은 로그로 보내고 화면에는 고정 문장을 준다(U-18 공통 규칙).</summary>
    private string Reason<T>(ApiResponse<T> response)
    {
        var raw = string.IsNullOrWhiteSpace(response.Error?.Message) ? response.Message : response.Error!.Message;
        if (!string.IsNullOrWhiteSpace(raw)) _log?.Warning($"[UnitDeviceApi] 서버 거절 원문: {raw}");
        return "잠시 후 다시 시도하세요.";
    }

    private static async Task<(bool Ok, string Message, List<BaseDeviceDto> Data)> Wrap<T>(Task<ApiListResponse<T>> call) where T : BaseDeviceDto
    {
        var response = await call.ConfigureAwait(false);
        return (response.Success,
                string.IsNullOrWhiteSpace(response.Error?.Message) ? response.Message : response.Error!.Message,
                response.Data?.Cast<BaseDeviceDto>().ToList() ?? new List<BaseDeviceDto>());
    }

    private async Task ReadAsync(
        EnumDeviceCategory category,
        Func<int, CancellationToken, Task<(bool Ok, string Message, List<BaseDeviceDto> Data)>> call,
        List<UnitDeviceItem> sink,
        List<string> failures,
        CancellationToken token)
    {
        try
        {
            for (var page = 1; page <= MAX_PAGES; page++)
            {
                token.ThrowIfCancellationRequested();
                var (ok, message, data) = await call(page, token).ConfigureAwait(false);
                if (!ok)
                {
                    failures.Add($"{UnitDeviceText.CategoryText(category)} — {message}");
                    return;
                }

                foreach (var dto in data)
                    if (dto != null && dto.Id > 0)
                        sink.Add(new UnitDeviceItem(dto.Id, dto.NumberDevice, Label(dto), category, dto.UnitId,
                                                    UnitDeviceText.StatusOf(dto.Status)));   // FR-03 — 읽은 시점 상태(관계도 L2 ▲오류)

                if (data.Count < PAGE_LIMIT) return;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[UnitDeviceApi] {category} 목록: {ex.Message}");
            failures.Add($"{UnitDeviceText.CategoryText(category)} — 서버에 닿지 못했습니다.");
        }
    }

    private static string Label(BaseDeviceDto dto)
        => string.IsNullOrWhiteSpace(dto.NameDevice) ? $"#{dto.Id}" : dto.NameDevice;
    #endregion
}
