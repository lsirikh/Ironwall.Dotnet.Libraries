using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
/****************************************************************************
   Purpose      : 함체 임계치 6.3 평면 ↔ 7.0 device_config.thresholds 양방향 변환
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 함체 임계치의 두 판본을 잇는다 — 6.3 평면 <c>threshold_config{temp_high,…}</c> ↔
/// 7.0 <c>device_config.thresholds{metric:{high,low}}</c>.
/// </summary>
/// <remarks>
/// <para><b>어휘 근거(추측 아님)</b>. 세 곳이 같은 표를 말한다:</para>
/// <list type="number">
/// <item>배포 Swagger 8.0.1 <c>DeviceConfigAxis.thresholds</c> 설명 —
///   "키는 카탈로그 <c>metric_key</c>(<c>temperature</c>·<c>humidity</c>·<c>current</c>·<c>voltage</c>·
///   <c>vibration</c>·<c>ups_battery_level</c>)".</item>
/// <item>명세 §5 머리 이관표 —
///   <c>threshold_config</c>·<c>thresholds_v2</c> → <c>device_config.thresholds</c>
///   (예 <c>{"temperature": {"high": 45}}</c>), 그리고 §5.5.3 "<c>thresholds</c> <b>7경계</b>".</item>
/// <item><b>서버 자신의 백필 SQL</b> <c>app/migrations/v98_device_axes_backfill.sql:46-51</c> —
///   레거시 평면 키를 metric 별 <c>{high,low}</c> 로 옮긴 그 매핑. 이 클래스의 표는
///   그 SQL 을 1:1 로 옮긴 것이다(그래서 서버가 이미 옮겨 놓은 값과 우리가 보내는 값이 갈리지 않는다).</item>
/// </list>
/// <para><b>7경계뿐이다</b> — <c>app/services/thresholds.py</c> <c>JUDGED</c>/<c>ALLOWED_BOUNDS</c> 가
/// 판정하는 경계만 허용하고 그 밖(예 <c>humidity.low</c>)이나 모르는 메트릭은 <b>422</b> 다.
/// 예전엔 조용히 저장되고 영원히 판정되지 않았기 때문에 서버가 스키마로 막았다.
/// 그러므로 이 변환기는 <b>표에 있는 7개만</b> 만들고 나머지는 버린다 —
/// 임의 경계를 만들어 보내면 요청 전체가 422 로 죽는다.</para>
/// <para><b>순서 제약은 검사하지 않는다</b> — 한 메트릭에 <c>high</c>·<c>low</c> 가 둘 다 있으면
/// <c>low &lt; high</c> 여야 하고 같아도 422 다. 여기서 임의 보정하면 운용자가 넣은 값이
/// 조용히 달라지므로, 서버가 <c>order_violations</c> 로 거부하게 둔다.</para>
/// </remarks>
public static class DeviceThresholdAxis
{
    /// <summary>7.0 메트릭 키 — 카탈로그 <c>metric_key</c>.</summary>
    public const string Temperature = "temperature";
    public const string Humidity = "humidity";
    public const string Current = "current";
    public const string Voltage = "voltage";
    public const string Vibration = "vibration";
    public const string UpsBatteryLevel = "ups_battery_level";

    private const string High = "high";
    private const string Low = "low";

    /// <summary>
    /// 6.3 평면 키 → (7.0 메트릭, 경계). <b>서버 <c>v98_device_axes_backfill.sql:46-51</c> 과 동일</b>.
    /// </summary>
    private static readonly (string Legacy, string Metric, string Bound)[] MAP =
    {
        ("temp_high",       Temperature,     High),
        ("temp_low",        Temperature,     Low),
        ("humidity_high",   Humidity,        High),
        ("current_high",    Current,         High),
        ("voltage_low",     Voltage,         Low),
        ("vibration_high",  Vibration,       High),
        // 운영 6.3.2 `EnclosureThresholdConfig` 에는 없는 키지만 v98 이 변환 대상으로 둔다 —
        // 손으로 JSONB 에 넣은 행이 있어서다. 있으면 옮기고 없으면 그냥 빠진다.
        ("ups_battery_low", UpsBatteryLevel, Low),
    };

    /// <summary>
    /// 시드형 <c>{metric:{min,max}}</c> 를 받아 줄 때의 경계 배정 —
    /// <c>v98…sql:62-63</c>: <c>max</c>→<c>high</c>(temperature·humidity·current·vibration),
    /// <c>min</c>→<c>low</c>(temperature·voltage·ups_battery_level).
    /// </summary>
    private static readonly HashSet<string> SEED_MAX_METRICS =
        new(StringComparer.Ordinal) { Temperature, Humidity, Current, Vibration };

    private static readonly HashSet<string> SEED_MIN_METRICS =
        new(StringComparer.Ordinal) { Temperature, Voltage, UpsBatteryLevel };

    /// <summary>
    /// 6.3 평면 <c>threshold_config</c> → 7.0 <c>device_config.thresholds</c>.
    /// 옮길 값이 하나도 없으면 <c>null</c>(빈 객체를 보내 저장값을 지우지 않는다).
    /// </summary>
    public static JObject? ToAxis(JObject? legacy)
    {
        if (legacy == null || legacy.Count == 0) return null;

        var axis = new JObject();

        foreach (var (legacyKey, metric, bound) in MAP)
        {
            var number = AsNumber(legacy[legacyKey]);
            if (number == null) continue;
            Put(axis, metric, bound, number.Value);
        }

        // 시드형 {metric:{min,max}} — 평면 키가 없을 때만 본다(평면 키가 정본).
        foreach (var metric in new[] { Temperature, Humidity, Current, Voltage, Vibration, UpsBatteryLevel })
        {
            if (legacy[metric] is not JObject pair) continue;

            if (SEED_MAX_METRICS.Contains(metric) && axis[metric]?[High] == null)
            {
                var max = AsNumber(pair["max"]) ?? AsNumber(pair[High]);
                if (max != null) Put(axis, metric, High, max.Value);
            }
            if (SEED_MIN_METRICS.Contains(metric) && axis[metric]?[Low] == null)
            {
                var min = AsNumber(pair["min"]) ?? AsNumber(pair[Low]);
                if (min != null) Put(axis, metric, Low, min.Value);
            }
        }

        return axis.Count == 0 ? null : axis;
    }

    /// <summary>
    /// 7.0 <c>device_config.thresholds</c> → 6.3 평면 <c>threshold_config</c>(응답 역투영).
    /// </summary>
    /// <remarks>
    /// 7.0+ 응답에는 평면 <c>threshold_config</c> 가 <b>없다</b>(A-devices D-24). 이 역투영이 없으면
    /// 함체 임계치 화면이 영원히 비어 보인다 — 422 도 안 나고 값만 사라진다.
    /// </remarks>
    public static JObject? FromAxis(JObject? axis)
    {
        if (axis == null || axis.Count == 0) return null;

        var legacy = new JObject();
        foreach (var (legacyKey, metric, bound) in MAP)
        {
            var number = AsNumber(axis[metric]?[bound]);
            if (number == null) continue;
            legacy[legacyKey] = number.Value;
        }
        return legacy.Count == 0 ? null : legacy;
    }

    private static void Put(JObject axis, string metric, string bound, double value)
    {
        if (axis[metric] is not JObject pair)
        {
            pair = new JObject();
            axis[metric] = pair;
        }
        pair[bound] = value;
    }

    /// <summary>
    /// 숫자만 임계치로 쓴다(<c>0</c> 도 유효). <c>bool</c> 은 제외하고, 문자열은 <b>Invariant</b> 로만 파싱한다.
    /// </summary>
    /// <remarks>
    /// 서버 <c>thresholds._number</c> 는 문자열을 아예 무시하지만, 우리 쪽 6.3 JSONB 에는 손으로 넣은
    /// 문자열 값이 섞여 있을 수 있어 <b>보내기 직전에 수치로 정규화</b>한다(로케일 의존 파싱 금지 —
    /// ko-KR 에서 소수점 해석이 갈리면 임계치가 조용히 바뀐다).
    /// </remarks>
    private static double? AsNumber(JToken? token)
    {
        if (token == null) return null;
        switch (token.Type)
        {
            case JTokenType.Integer:
            case JTokenType.Float:
                return token.Value<double>();
            case JTokenType.String:
                var text = token.Value<string>();
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null;
            default:
                return null;
        }
    }
}
