using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net.Http;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : 부대 관계도 공유 배치 API 구현 — If-Match · 412 · 404/422 → 미지원 해석
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary><see cref="IUnitLayoutApiService"/> 구현.</summary>
/// <remarks>
/// <para><b>지원 판정</b>(FR-50 v1.2, <b>프로브 GET 한정</b>) — 200 = 지원 · 404 / 405 / 410 / <c>ENDPOINT_REMOVED</c> /
/// <b>422 + <c>path.unit_id</c></b> = 미지원 · 그 밖 = 읽기 실패. 쓰기의 422 는 언제나 검증 실패(<see cref="UnitLayoutWriteResult.Rejected"/>). 422 갈래는 V-06 실측(8.0.3 이 <c>/units/layout</c> 을 <c>/{unit_id}</c> 로 읽는다)에서 왔다.
/// 판본 번호를 상수로 비교하지 않는다 — 경로의 실존으로 판정한다(메모리 <c>api_spec_ahead_of_deployment</c>).</para>
/// <para><b>조건부 쓰기</b> — 모든 PATCH 는 <c>If-Match: "&lt;version&gt;"</c> 를 싣는다. 전송 계층이 요청 헤더를 실을 수 없으면
/// (<see cref="IApiHeaderRequestService"/> 없음) <b>보내지 않는다</b> — 조건 없는 쓰기는 말없는 덮어쓰기다(NFR-15).</para>
/// <para>8.0 미만 계약이면 네트워크에 나가지 않는다 — 운영 6.3.2 에 404 를 퍼붓지 않는다(<see cref="UnitApiService"/> 와 같은 게이트).</para>
/// </remarks>
public sealed class UnitLayoutApiService : IUnitLayoutApiService
{
    #region - Ctors -
    public UnitLayoutApiService(ILogService? log, IApiService apiService, ApiSetupModel setupModel, IServerContractProbe? contractProbe = null)
    {
        _log = log;
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _setupModel = setupModel ?? throw new ArgumentNullException(nameof(setupModel));
        _contractProbe = contractProbe;
    }
    #endregion

    #region - Implementation of IUnitLayoutApiService -
    public async Task<UnitLayoutReadResult> GetAsync(CancellationToken token = default)
    {
        if (!IsUnitEra)
            return new UnitLayoutReadResult.Unsupported(0, ContractTooOld);

        try
        {
            using var response = await _apiService.GetRequestAsync(LayoutUrl).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var envelope = await response.ToApiResponseAsync<UnitLayoutDocumentDto>().ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                if (!envelope.Success || envelope.Data is null)
                {
                    _log?.Warning($"[{nameof(UnitLayoutApiService)}] 배치 문서를 읽지 못했습니다(HTTP {status}): {envelope.Error?.Message}");
                    return new UnitLayoutReadResult.ReadFailed(UnitLayoutFailureKind.Parse, status, "배치 문서를 읽지 못했습니다.");
                }
                return new UnitLayoutReadResult.Supported(envelope.Data, ParseETag(response));
            }

            if (IsRouteAbsent(status, envelope.Error))
            {
                _log?.Info($"[{nameof(UnitLayoutApiService)}] 이 서버는 공유 배치를 지원하지 않습니다(HTTP {status}, {envelope.Error?.Code}) — 세션 전용.");
                return new UnitLayoutReadResult.Unsupported(status, "이 서버는 배치 저장을 지원하지 않습니다.");
            }

            var kind = KindOf(status);
            _log?.Warning($"[{nameof(UnitLayoutApiService)}] 배치 읽기 실패(HTTP {status}, {envelope.Error?.Code}): {envelope.Error?.Message}");
            return new UnitLayoutReadResult.ReadFailed(kind, status, envelope.Error?.Message ?? $"HTTP {status}");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UnitLayoutApiService)}] 배치 읽기 예외: {ex.Message}");
            return new UnitLayoutReadResult.ReadFailed(UnitLayoutFailureKind.Unreachable, 0, ex.Message);
        }
    }

    public async Task<UnitLayoutWriteResult> PatchAsync(long ifMatchVersion, UnitLayoutPatchDto patch, CancellationToken token = default)
    {
        if (!IsUnitEra)
            return new UnitLayoutWriteResult.Unsupported(0, ContractTooOld);

        if (patch is null)
            return new UnitLayoutWriteResult.Rejected("요청 본문이 없습니다.");

        if (_apiService is not IApiHeaderRequestService conditional)
        {
            _log?.Error($"[{nameof(UnitLayoutApiService)}] 전송 계층이 If-Match 를 실을 수 없어 배치 쓰기를 보내지 않았습니다 — 조건 없는 쓰기는 다른 운영자의 배치를 덮는다.");
            return new UnitLayoutWriteResult.Failed(UnitLayoutFailureKind.NoConditionalTransport, 0, "배치를 저장할 수 없습니다(전송 계층).");
        }

        // 서버 검증이 목록을 기대한다 — null 을 빈 목록으로(생략 · null 금지).
        patch.Set ??= new List<UnitLayoutItemDto>();
        patch.Clear ??= new List<int>();

        try
        {
            var headers = new Dictionary<string, string>
            {
                ["If-Match"] = "\"" + ifMatchVersion.ToString(CultureInfo.InvariantCulture) + "\"",
            };
            using var response = await conditional.SendJsonAsync(HttpMethod.Patch, LayoutUrl, patch, headers, token).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var envelope = await response.ToApiResponseAsync<UnitLayoutDocumentDto>().ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                if (!envelope.Success || envelope.Data is null)
                {
                    // 쓰기는 반영됐을 수 있다 — 부르는 쪽이 다시 읽어 확인한다.
                    return new UnitLayoutWriteResult.Failed(UnitLayoutFailureKind.Parse, status, "저장 결과를 읽지 못했습니다.");
                }
                return new UnitLayoutWriteResult.Ok(envelope.Data, ParseETag(response));
            }

            if (status == 412)
                return new UnitLayoutWriteResult.Conflict(ReadCurrentVersion(envelope.Error));

            // 422 는 먼저 본다 — 쓰기의 422 는 언제나 검증 실패다(FR-50 v1.2: 422 → 미지원 해석은 프로브 GET 한정).
            //   path.unit_id 를 짚는 422 라도 미지원으로 읽으면 세션 전용으로 조용히 넘어가 서버 거절이 숨는다.
            if (status == 422)
            {
                _log?.Warning($"[{nameof(UnitLayoutApiService)}] 배치 쓰기 거절(422): {envelope.Error?.Message} {envelope.Error?.Details}");
                return new UnitLayoutWriteResult.Rejected(envelope.Error?.Message ?? "서버 규칙에 맞지 않아 거절됐습니다.");
            }

            // 지원 중이던 경로가 사라졌다(404 · 405 · 410 · ENDPOINT_REMOVED) → 부르는 쪽이 세션 전용으로 넘긴다(SIM-F059 · 분석 ISSUE-6).
            if (IsRouteAbsent(status, envelope.Error))
                return new UnitLayoutWriteResult.EndpointGone(status, "배치 저장 경로가 사라졌습니다 — 이 창에서는 세션 전용으로 동작합니다.");

            if (status == 428)
            {
                _log?.Error($"[{nameof(UnitLayoutApiService)}] 서버가 If-Match 누락(428)으로 거절했습니다 — 전송 계층을 확인하십시오.");
                return new UnitLayoutWriteResult.PreconditionRequired(status, envelope.Error?.Message ?? "HTTP 428");
            }

            _log?.Warning($"[{nameof(UnitLayoutApiService)}] 배치 쓰기 실패(HTTP {status}, {envelope.Error?.Code}): {envelope.Error?.Message}");
            if (status == 504)   // 시간 초과 — 서버에는 반영됐을 수 있다(부르는 쪽이 다시 읽어 확정한다)
                return new UnitLayoutWriteResult.Unknown(status, envelope.Error?.Message ?? "결과를 확인하지 못했습니다.");
            return new UnitLayoutWriteResult.Failed(KindOf(status), status, envelope.Error?.Message ?? $"HTTP {status}");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UnitLayoutApiService)}] 배치 쓰기 예외: {ex.Message}");
            return new UnitLayoutWriteResult.Failed(UnitLayoutFailureKind.Unreachable, 0, ex.Message);
        }
    }
    #endregion

    #region - 판정 (순수) -
    /// <summary>
    /// 배치 경로가 이 서버에 없는가 — 404 · 405 · 410 · <c>ENDPOINT_REMOVED</c> · <b>422 + <c>path.unit_id</c></b>(라우트 가림).
    /// </summary>
    /// <remarks>
    /// 422 는 <b><c>path.unit_id</c> 를 짚을 때만</b> 미지원이다 — 그 밖의 422 는 본문 검증 실패이므로 미지원으로 오판하면
    /// 결함이 "지원 안 함" 문구 뒤로 숨는다(SIM-P015).
    /// </remarks>
    internal static bool IsRouteAbsent(int status, ApiError? error)
    {
        if (string.Equals(error?.Code, ApiErrorCodes.EndpointRemoved, StringComparison.Ordinal)) return true;
        if (status is 404 or 405 or 410) return true;
        if (status == 422 && error is not null)
            return error.FieldErrors.Any(f => string.Equals(f.Field, PATH_UNIT_ID_FIELD, StringComparison.Ordinal));
        return false;
    }

    /// <summary>상태 코드 → 실패 종류.</summary>
    internal static UnitLayoutFailureKind KindOf(int status) => status switch
    {
        401 => UnitLayoutFailureKind.Unauthorized,
        403 => UnitLayoutFailureKind.Forbidden,
        428 => UnitLayoutFailureKind.PreconditionRequired,
        503 => UnitLayoutFailureKind.Unreachable,
        504 => UnitLayoutFailureKind.Timeout,
        >= 500 and < 600 => UnitLayoutFailureKind.Server,
        _ => UnitLayoutFailureKind.Other,
    };

    /// <summary><c>ETag: "13"</c> · <c>W/"13"</c> → 13. 숫자가 아니면 <c>null</c>(본문 <c>version</c> 이 정본).</summary>
    private static long? ParseETag(HttpResponseMessage response)
    {
        var tag = response.Headers.ETag?.Tag;
        if (tag is null && response.Headers.TryGetValues("ETag", out var raw)) tag = raw.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var trimmed = tag.Trim();
        if (trimmed.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[2..];
        trimmed = trimmed.Trim('"');
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) ? version : null;
    }

    /// <summary>412 본문 <c>error.details.current_version</c>. 없거나 모양이 다르면 <c>null</c>.</summary>
    private static long? ReadCurrentVersion(ApiError? error)
    {
        if (error?.DetailsToken is JObject details
            && details.TryGetValue("current_version", StringComparison.Ordinal, out var value)
            && value.Type is JTokenType.Integer or JTokenType.String
            && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
        {
            return version;
        }
        return null;
    }
    #endregion

    #region - Contract Gate -
    private EnumServerContract Contract => _contractProbe?.Contract ?? EnumServerContract.V6_3;

    /// <summary>부대 표면은 8.0 에서 생겼다 — <c>&gt;=</c> 비교만 쓴다.</summary>
    private bool IsUnitEra => Contract >= EnumServerContract.V8_0;

    private string ContractTooOld => $"부대 편제는 현재 서버 계약({Contract})에 없습니다 — 배치 저장도 없습니다.";

    private string LayoutUrl => $"{_setupModel.Url}/units/layout";
    #endregion

    #region - Attributes -
    /// <summary>라우트 가림 422 의 표지 — 8.0.3 실측(<c>error.details[].field</c>).</summary>
    internal const string PATH_UNIT_ID_FIELD = "path.unit_id";

    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    private readonly IServerContractProbe? _contractProbe;
    #endregion
}
