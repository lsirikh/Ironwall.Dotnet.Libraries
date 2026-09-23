using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 이벤트 맵핑 워크벤치 서버 왕복 — 읽기는 관대하게, 쓰기는 엄격하게
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="IMappingWorkbenchGateway"/> 의 실제 구현 — <c>/api/integrations/*</c> 왕복.
/// </summary>
/// <remarks>
/// <para><b>기존 <c>IEventApiService</c> 를 넓히지 않았다.</b> 그 인터페이스에는 벌크 6종이 없고,
/// 멤버를 더하면 레포의 목·페이크가 전부 깨진다. 새 인터페이스를 따로 두는 편이 싸다.</para>
/// <para>읽기 경로는 <c>data</c> 가 배열이든 <c>{items,total}</c> 이든 <b>둘 다 받는다</b>.
/// 배포 v8 서버는 하위 목록을 <c>{items,total}</c> 로 주지만 문서 v4 판은 경광등을 <c>[]</c> 로 적고 있고,
/// 운영 6.3.2 는 아직 확인되지 않았다 — 어느 쪽이 오든 화면이 침묵하지 않게 한다.</para>
/// </remarks>
public sealed class MappingWorkbenchGateway : IMappingWorkbenchGateway
{
    private const int PAGE_LIMIT = 100;
    private const int MAX_PAGES = 200;      // 2만 건. 이보다 많으면 경고하고 멈춘다(무한 루프 금지)

    /// <summary>이 화면이 읽고 쓰는 연동 계약이 없는 판본에서 쓰는 거절 문구.</summary>
    public const string ContractRefusal = "이 서버 판본에는 이벤트 맵핑 연동 계약이 없습니다.";

    private readonly IApiService _api;
    private readonly ApiSetupModel _setup;
    private readonly IServerContractProbe? _probe;
    private readonly ILogService? _log;

    /// <summary>생성자.</summary>
    /// <param name="api">HTTP 클라이언트.</param>
    /// <param name="setup">서버 주소 설정.</param>
    /// <param name="probe">서버 계약 세대. <c>null</c>(미등록)이면 <see cref="EnumServerContract.V6_3"/> 로 본다.</param>
    /// <param name="log">로그(없어도 동작한다).</param>
    public MappingWorkbenchGateway(IApiService api, ApiSetupModel setup, IServerContractProbe? probe = null, ILogService? log = null)
    {
        _api = api;
        _setup = setup;
        _probe = probe;
        _log = log;
    }

    /// <summary>이 판본에서 연동 경로를 써도 되는가 — <b>7.0 이상</b>.</summary>
    /// <remarks>
    /// 🔴 입구(<see cref="MappingWorkbenchLauncher"/>)만 막으면 부족하다. 뷰모델을 직접 만든 코드는
    /// 그 관문을 지나지 않는다. <b>서비스 쪽에서도 거절</b>한다 — 선례는 <c>AssemblyWriteGuard</c>.
    /// <c>==</c> 가 아니라 <c>&gt;=</c> 로 본다(판본은 앞으로도 올라간다).
    /// </remarks>
    public bool IsSupported => (_probe?.Contract ?? EnumServerContract.V6_3) >= EnumServerContract.V7_0;

    private MappingCallResult<T> Refuse<T>()
    {
        _log?.Warning("[MappingWorkbench] " + ContractRefusal);
        return MappingCallResult<T>.Fail(ContractRefusal, "server contract < 7.0", 0);
    }

    private string Root => $"{_setup.Url}/integrations/event-mappings";

    #region - 매핑 본체 -
    /// <inheritdoc/>
    public async Task<MappingCallResult<IReadOnlyList<EventMappingReadDto>>> ListMappingsAsync(CancellationToken token = default)
    {
        if (!IsSupported) return Refuse<IReadOnlyList<EventMappingReadDto>>();
        var all = new List<EventMappingReadDto>();
        for (var page = 1; page <= MAX_PAGES; page++)
        {
            var parameters = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["limit"] = PAGE_LIMIT.ToString(),
            };
            var result = await SendAsync(() => _api.GetRequestAsync(Root, parameters), "맵핑 목록을 불러오지 못했습니다.").ConfigureAwait(false);
            if (!result.IsSuccess) return MappingCallResult<IReadOnlyList<EventMappingReadDto>>.Fail(result.Message, result.RawError, result.StatusCode);

            var rows = ReadList<EventMappingReadDto>(result.Value);
            all.AddRange(rows);

            // 종료 조건은 "받은 개수 < 요청 한도" 하나로 통일한다 — total 이 없는 판본에서도 성립한다.
            if (rows.Count < PAGE_LIMIT) break;
            if (page == MAX_PAGES) _log?.Warning($"[MappingWorkbench] 맵핑이 {MAX_PAGES * PAGE_LIMIT}건을 넘어 읽기를 멈췄다.");
        }
        return MappingCallResult<IReadOnlyList<EventMappingReadDto>>.Ok(all);
    }

    /// <inheritdoc/>
    public async Task<MappingCallResult<EventMappingReadDto>> GetMappingAsync(int mappingId, CancellationToken token = default)
    {
        if (!IsSupported) return Refuse<EventMappingReadDto>();
        var result = await SendAsync(() => _api.GetRequestAsync($"{Root}/{mappingId}"), "맵핑을 다시 읽지 못했습니다.").ConfigureAwait(false);
        if (!result.IsSuccess) return MappingCallResult<EventMappingReadDto>.Fail(result.Message, result.RawError, result.StatusCode);

        var dto = ReadOne<EventMappingReadDto>(result.Value);
        return dto is null
            ? MappingCallResult<EventMappingReadDto>.Fail("맵핑을 다시 읽지 못했습니다.", "data was empty", result.StatusCode)
            : MappingCallResult<EventMappingReadDto>.Ok(dto, result.StatusCode);
    }

    /// <inheritdoc/>
    public async Task<MappingCallResult<EventMappingReadDto>> CreateMappingAsync(EventMappingCreateDto body, CancellationToken token = default)
    {
        if (!IsSupported) return Refuse<EventMappingReadDto>();
        var result = await SendAsync(() => _api.PostRequestAsync(Root, body), "맵핑을 만들지 못했습니다.").ConfigureAwait(false);
        if (!result.IsSuccess) return MappingCallResult<EventMappingReadDto>.Fail(result.Message, result.RawError, result.StatusCode);

        var dto = ReadOne<EventMappingReadDto>(result.Value);
        return dto is null
            ? MappingCallResult<EventMappingReadDto>.Fail("맵핑을 만들었지만 응답을 읽지 못했습니다. 목록을 새로 고치십시오.", "data was empty", result.StatusCode)
            : MappingCallResult<EventMappingReadDto>.Ok(dto, result.StatusCode);
    }

    /// <inheritdoc/>
    public async Task<MappingCallResult<EventMappingReadDto>> PatchMappingAsync(int mappingId, EventMappingUpdateDto body, CancellationToken token = default)
    {
        if (!IsSupported) return Refuse<EventMappingReadDto>();
        if (body.IsEmpty) return MappingCallResult<EventMappingReadDto>.Fail("바뀐 값이 없습니다.", null, 0);

        var result = await SendAsync(() => _api.PatchRequestAsync($"{Root}/{mappingId}", body), "맵핑을 저장하지 못했습니다.").ConfigureAwait(false);
        if (!result.IsSuccess) return MappingCallResult<EventMappingReadDto>.Fail(result.Message, result.RawError, result.StatusCode);

        var dto = ReadOne<EventMappingReadDto>(result.Value);
        return dto is null
            ? MappingCallResult<EventMappingReadDto>.Fail("저장했지만 응답을 읽지 못했습니다. 목록을 새로 고치십시오.", "data was empty", result.StatusCode)
            : MappingCallResult<EventMappingReadDto>.Ok(dto, result.StatusCode);
    }
    #endregion

    #region - 하위 배선 -
    /// <inheritdoc/>
    public Task<MappingCallResult<IReadOnlyList<MappingCameraReadDto>>> ListCamerasAsync(int mappingId, CancellationToken token = default)
        => ListConfigsAsync<MappingCameraReadDto>(mappingId, MappingActionKind.Camera, token);

    /// <inheritdoc/>
    public Task<MappingCallResult<IReadOnlyList<MappingSpeakerReadDto>>> ListSpeakersAsync(int mappingId, CancellationToken token = default)
        => ListConfigsAsync<MappingSpeakerReadDto>(mappingId, MappingActionKind.Speaker, token);

    /// <inheritdoc/>
    public Task<MappingCallResult<IReadOnlyList<MappingLampReadDto>>> ListLampsAsync(int mappingId, CancellationToken token = default)
        => ListConfigsAsync<MappingLampReadDto>(mappingId, MappingActionKind.Lamp, token);

    private async Task<MappingCallResult<IReadOnlyList<T>>> ListConfigsAsync<T>(int mappingId, MappingActionKind kind, CancellationToken token)
    {
        if (!IsSupported) return Refuse<IReadOnlyList<T>>();
        var label = MappingKindText.Label(kind);
        var url = $"{Root}/{mappingId}/{MappingKindText.Segment(kind)}";
        var result = await SendAsync(() => _api.GetRequestAsync(url), $"{label} 배선을 불러오지 못했습니다.").ConfigureAwait(false);
        if (!result.IsSuccess) return MappingCallResult<IReadOnlyList<T>>.Fail(result.Message, result.RawError, result.StatusCode);
        // 주의: IApiService 에 토큰 오버로드가 없어 요청 자체는 취소되지 않는다 — 늦게 온 결과를 버릴 뿐이다.
        if (token.IsCancellationRequested) return MappingCallResult<IReadOnlyList<T>>.Fail("취소되었습니다.", "cancelled", 0);

        return MappingCallResult<IReadOnlyList<T>>.Ok(ReadList<T>(result.Value), result.StatusCode);
    }

    /// <inheritdoc/>
    public async Task<MappingCallResult<MappingBulkCreateResultDto>> BulkCreateAsync(
        int mappingId, MappingActionKind kind, IReadOnlyList<object> items, CancellationToken token = default)
    {
        if (!IsSupported) return Refuse<MappingBulkCreateResultDto>();
        if (items.Count == 0) return MappingCallResult<MappingBulkCreateResultDto>.Fail("보낼 항목이 없습니다.");
        if (items.Count > EventMappingRules.CHUNK_SIZE)
            return MappingCallResult<MappingBulkCreateResultDto>.Fail($"한 번에 {EventMappingRules.CHUNK_SIZE}건까지만 보낼 수 있습니다.");

        var label = MappingKindText.Label(kind);
        var url = $"{Root}/{mappingId}/{MappingKindText.Segment(kind)}/bulk";
        var body = new MappingBulkCreateRequestDto<object> { Items = items.ToList() };

        var result = await SendAsync(() => _api.PostRequestAsync(url, body), $"{label} 등록에 실패했습니다.").ConfigureAwait(false);
        if (!result.IsSuccess) return MappingCallResult<MappingBulkCreateResultDto>.Fail(result.Message, result.RawError, result.StatusCode);

        var data = ReadOne<MappingBulkCreateResultDto>(result.Value);
        return data is null
            ? MappingCallResult<MappingBulkCreateResultDto>.Fail($"{label} 등록 결과를 읽지 못했습니다.", "data was empty", result.StatusCode)
            : MappingCallResult<MappingBulkCreateResultDto>.Ok(data, result.StatusCode);
    }

    /// <inheritdoc/>
    public async Task<MappingCallResult<MappingBulkUnassignResultDto>> BulkUnassignAsync(
        int mappingId, MappingActionKind kind, IReadOnlyList<int> configIds, CancellationToken token = default)
    {
        if (!IsSupported) return Refuse<MappingBulkUnassignResultDto>();
        if (configIds.Count == 0) return MappingCallResult<MappingBulkUnassignResultDto>.Fail("해제할 배선이 없습니다.");
        if (configIds.Count > EventMappingRules.CHUNK_SIZE)
            return MappingCallResult<MappingBulkUnassignResultDto>.Fail($"한 번에 {EventMappingRules.CHUNK_SIZE}건까지만 해제할 수 있습니다.");

        var label = MappingKindText.Label(kind);
        var url = $"{Root}/{mappingId}/{MappingKindText.Segment(kind)}";
        var body = new MappingBulkUnassignRequestDto { ConfigIds = configIds.ToList() };

        var result = await SendAsync(() => _api.DeleteRequestAsync(url, body), $"{label} 해제에 실패했습니다.").ConfigureAwait(false);
        if (!result.IsSuccess) return MappingCallResult<MappingBulkUnassignResultDto>.Fail(result.Message, result.RawError, result.StatusCode);

        var data = ReadOne<MappingBulkUnassignResultDto>(result.Value);
        return data is null
            ? MappingCallResult<MappingBulkUnassignResultDto>.Fail($"{label} 해제 결과를 읽지 못했습니다.", "data was empty", result.StatusCode)
            : MappingCallResult<MappingBulkUnassignResultDto>.Ok(data, result.StatusCode);
    }

    /// <inheritdoc/>
    public async Task<MappingCallResult<bool>> PatchConfigAsync(
        int mappingId, MappingActionKind kind, int configId, object body, CancellationToken token = default)
    {
        if (!IsSupported) return Refuse<bool>();
        var label = MappingKindText.Label(kind);
        var url = $"{Root}/{mappingId}/{MappingKindText.Segment(kind)}/{configId}";
        var result = await SendAsync(() => _api.PatchRequestAsync(url, body), $"{label} 배선을 저장하지 못했습니다.").ConfigureAwait(false);
        return result.IsSuccess
            ? MappingCallResult<bool>.Ok(true, result.StatusCode)
            : MappingCallResult<bool>.Fail(result.Message, result.RawError, result.StatusCode);
    }
    #endregion

    #region - 봉투 다루기 -
    /// <summary>
    /// 한 번 보내고 봉투를 <see cref="JObject"/> 로 돌려준다. 실패 사유는 <b>한국어 한 줄 + 원문</b>으로 나뉜다.
    /// </summary>
    private async Task<MappingCallResult<JObject>> SendAsync(Func<Task<HttpResponseMessage>> send, string failureText)
    {
        try
        {
            using var response = await send().ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _log?.Warning($"[MappingWorkbench] {status} — {Trim(content)}");
                return MappingCallResult<JObject>.Fail(Explain(status, failureText), Trim(content), status);
            }

            var envelope = Parse(content);
            if (envelope is null)
                return MappingCallResult<JObject>.Fail(failureText, "envelope was not an object", status);

            // 서버는 200 안에 success:false 를 실을 수 있다.
            if (envelope.Value<bool?>("success") == false)
            {
                var message = envelope.Value<string>("message");
                return MappingCallResult<JObject>.Fail(failureText, Trim(message ?? content), status);
            }
            return MappingCallResult<JObject>.Ok(envelope, status);
        }
        catch (Exception ex)
        {
            // 예외 원문은 사용자에게 내지 않는다 — 사유는 한 줄로, 원문은 진단 칸으로.
            _log?.Error($"[MappingWorkbench] {failureText} — {ex.Message}");
            return MappingCallResult<JObject>.Fail(failureText, ex.Message, 0);
        }
    }

    private static string Explain(int status, string fallback) => status switch
    {
        401 => "로그인이 만료되었습니다. 변경한 내용은 그대로 있습니다 — 다시 로그인한 뒤 [적용]하십시오.",
        403 => "이 작업을 할 권한이 없습니다.",
        404 => "서버에서 찾을 수 없습니다. 목록을 새로 고치십시오.",
        409 => "이미 등록되어 있습니다.",
        422 => fallback + " 입력값을 확인하십시오.",
        >= 500 => "서버가 응답하지 못했습니다. 잠시 뒤 다시 시도하십시오.",
        _ => fallback,
    };

    /// <summary>
    /// 봉투를 읽는다 — <b>날짜를 날짜로 바꾸지 않는다</b>(<see cref="DateParseHandling.None"/>).
    /// </summary>
    /// <remarks>
    /// 기본 설정은 <c>"2026-09-24T01:55:48.053887+09:00"</c> 을 <see cref="DateTime"/> 으로 바꾸고, 문자열 필드
    /// (<c>UpdatedAt</c>)로 옮길 때 <c>"09/24/2026 01:55:48"</c> 로 다시 적는다 — 마이크로초와 offset 이 사라져
    /// [적용] 전 동시 편집 대조가 <b>같은 초 안의 변경</b>을 못 봤다(실서버 왕복 E4b · E4c).
    /// 대조는 서버 원문끼리 한다.
    /// </remarks>
    private static readonly JsonSerializerSettings EnvelopeSettings = new() { DateParseHandling = DateParseHandling.None };

    internal static JObject? Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        try { return JsonConvert.DeserializeObject<JObject>(content, EnvelopeSettings); }
        catch { return null; }
    }

    /// <summary>
    /// <c>data</c> 에서 목록을 꺼낸다 — <c>[]</c> 와 <c>{items:[…]}</c> 를 <b>둘 다</b> 받는다.
    /// </summary>
    internal static IReadOnlyList<T> ReadList<T>(JObject? envelope)
    {
        var data = envelope?["data"];
        var array = data as JArray ?? data?["items"] as JArray;
        if (array is null) return Array.Empty<T>();

        var rows = new List<T>(array.Count);
        foreach (var token in array)
        {
            var row = token.ToObject<T>();
            if (row is not null) rows.Add(row);
        }
        return rows;
    }

    /// <summary><c>data</c> 를 단건으로 읽는다.</summary>
    internal static T? ReadOne<T>(JObject? envelope) where T : class
        => envelope?["data"] as JObject is { } obj ? obj.ToObject<T>() : null;

    private static string Trim(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var text = raw.Trim();
        return text.Length <= 400 ? text : text[..400] + "…";
    }
    #endregion
}
