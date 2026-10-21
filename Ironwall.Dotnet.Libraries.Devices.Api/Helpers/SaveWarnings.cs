using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Helpers;

/// <summary>
/// 저장 성공 응답의 <c>warnings[]</c>(명세 §3.2 · §12.1.2)를 <b>운영자에게 알릴 말</b>로 바꾸는 공용 해석기.
/// </summary>
/// <remarks>
/// <para>명세의 심각도 권고대로 <c>UNMATCHED_THRESHOLD</c> · <c>SERVER_ID_MISSING</c> 만 알린다.
/// <c>READ_ONLY_IGNORED</c> · <c>OBSERVED_AT_DEFAULTED</c> 와 <b>모르는 코드</b>는 무시한다(경고는 거부가 아니다).</para>
/// <para>항목 모양은 <c>{field, code, message}</c>. 임계치 경고의 <c>field</c> 는 입구에 따라
/// <c>device_config.thresholds.&lt;메트릭&gt;</c>(<c>/config</c>) 또는 <c>thresholds.&lt;메트릭&gt;</c>(장비 본체 쓰기)다 —
/// 둘 다 마지막 점 뒤를 메트릭 이름으로 읽는다.</para>
/// <para>보이는 말에는 영어 코드 · 내부 키를 넣지 않는다 — 메트릭은 한글 이름, 모르면 "측정 항목".</para>
/// </remarks>
public static class SaveWarnings
{
    /// <summary>알릴 경고 한 건 — <see cref="Code"/> 는 분기용(화면에 쓰지 않는다), <see cref="Text"/> 가 보일 말이다.</summary>
    public sealed record Notice(string Code, string? Field, string? Metric, string Text);

    /// <summary>메트릭 키 → 한글 이름(카탈로그 <c>metric_key</c> 중 함체 계측에 쓰이는 것).</summary>
    public static readonly IReadOnlyDictionary<string, string> MetricLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["temperature"] = "온도",
        ["humidity"] = "습도",
        ["voltage"] = "전압",
        ["current"] = "전류",
        ["vibration"] = "진동",
        ["ups_battery_level"] = "UPS 배터리 잔량",
        ["signal"] = "신호 크기",
        ["dust"] = "먼지",
        ["network"] = "네트워크",
        ["power"] = "전력",
        ["pressure"] = "압력",
        ["water_level"] = "수위",
        ["smoke"] = "연기",
    };

    /// <summary>메트릭 키의 한글 이름. <paramref name="extraLabels"/>(예: 카탈로그 label)가 먼저 이긴다. 모르면 "측정 항목".</summary>
    public static string MetricLabel(string? metric, IReadOnlyDictionary<string, string>? extraLabels = null)
    {
        if (string.IsNullOrWhiteSpace(metric)) return "측정 항목";
        if (extraLabels != null && extraLabels.TryGetValue(metric!, out var extra) && !string.IsNullOrWhiteSpace(extra)) return extra;
        return MetricLabels.TryGetValue(metric!, out var label) ? label : "측정 항목";
    }

    /// <summary>경고 목록에서 알릴 것만 골라 쉬운 말로 만든다. 없으면 빈 목록.</summary>
    public static IReadOnlyList<Notice> Collect(IEnumerable<ResponseWarningDto>? warnings, IReadOnlyDictionary<string, string>? extraLabels = null)
    {
        var notices = new List<Notice>();
        if (warnings == null) return notices;

        var unmatched = new List<(string? Field, string? Metric)>();
        foreach (var warning in warnings)
        {
            if (warning == null) continue;
            var code = warning.Code?.Trim();
            if (string.Equals(code, ApiWarningCodes.UnmatchedThreshold, StringComparison.OrdinalIgnoreCase))
                unmatched.Add((warning.Field, MetricOf(warning.Field)));
            else if (string.Equals(code, ApiWarningCodes.ServerIdMissing, StringComparison.OrdinalIgnoreCase))
                notices.Add(new Notice(ApiWarningCodes.ServerIdMissing, warning.Field, null,
                    "저장했지만 방송 서버가 정해지지 않았습니다. 방송 서버를 등록한 뒤 이 스피커에 지정해 주십시오."));
            // READ_ONLY_IGNORED · OBSERVED_AT_DEFAULTED · 모르는 코드 — 알리지 않는다.
        }

        if (unmatched.Count > 0)
        {
            var names = unmatched
                .Select(u => MetricLabel(u.Metric, extraLabels))
                .Distinct()
                .ToList();
            var text = $"저장했지만 이 장비에 없는 측정 항목이라 경보가 동작하지 않습니다({string.Join(", ", names)})";
            // 한 건으로 묶는다 — 메트릭마다 따로 띄우면 같은 말이 여러 번 뜬다.
            notices.Insert(0, new Notice(ApiWarningCodes.UnmatchedThreshold,
                string.Join(",", unmatched.Select(u => u.Field)), string.Join(",", unmatched.Select(u => u.Metric)), text));
        }
        return notices;
    }

    /// <summary>응답 봉투(단건)의 경고에서 알릴 것을 모은다.</summary>
    public static IReadOnlyList<Notice> Collect<T>(ApiResponse<T>? response, IReadOnlyDictionary<string, string>? extraLabels = null)
        => Collect(response?.Warnings, extraLabels);

    /// <summary>응답 봉투(목록)의 경고에서 알릴 것을 모은다.</summary>
    public static IReadOnlyList<Notice> Collect<T>(ApiListResponse<T>? response, IReadOnlyDictionary<string, string>? extraLabels = null)
        => Collect(response?.Warnings, extraLabels);

    /// <summary>원문 JSON(봉투 전체)의 최상위 <c>warnings</c> 에서 알릴 것을 모은다 — 봉투 DTO 가 경고 자리를 갖지 않는 경로용.</summary>
    public static IReadOnlyList<Notice> CollectFromJson(string? envelopeJson, IReadOnlyDictionary<string, string>? extraLabels = null)
    {
        if (string.IsNullOrWhiteSpace(envelopeJson)) return Array.Empty<Notice>();
        try
        {
            if (JToken.Parse(envelopeJson!) is not JObject root || root["warnings"] is not JArray items) return Array.Empty<Notice>();
            var warnings = items.OfType<JObject>()
                .Select(o => new ResponseWarningDto
                {
                    Code = o["code"]?.Type == JTokenType.String ? (string?)o["code"] : null,
                    Field = o["field"]?.Type == JTokenType.String ? (string?)o["field"] : null,
                    Message = o["message"]?.Type == JTokenType.String ? (string?)o["message"] : null,
                });
            return Collect(warnings, extraLabels);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return Array.Empty<Notice>();
        }
    }

    /// <summary>알릴 말을 한 문장 묶음으로 — 없으면 <c>null</c>(호출부는 알림을 띄우지 않는다).</summary>
    public static string? ToText(IReadOnlyList<Notice> notices)
        => notices.Count == 0 ? null : string.Join(Environment.NewLine, notices.Select(n => n.Text));

    /// <summary>
    /// <c>field</c> 경로에서 메트릭 키를 꺼낸다 — <c>device_config.thresholds.humidity</c> · <c>thresholds.humidity.high</c> → <c>humidity</c>.
    /// </summary>
    internal static string? MetricOf(string? field)
    {
        if (string.IsNullOrWhiteSpace(field)) return null;
        var parts = field!.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var at = Array.FindIndex(parts, p => string.Equals(p, "thresholds", StringComparison.OrdinalIgnoreCase));
        if (at >= 0 && at + 1 < parts.Length) return parts[at + 1];
        return parts.Length > 0 ? parts[^1] : null;
    }
}
