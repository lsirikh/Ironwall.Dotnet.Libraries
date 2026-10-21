using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/// <summary>
/// 함체 임계값 창에서 <b>지운</b> 경계를 7.0+ 쓰기 본문에 명시적 <c>null</c> 로 싣는 조각을 만든다(GOP API 8.0.15 대조 G16).
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b> — 7.0+ 의 기존 장비 수정은 <c>PATCH</c>(RFC 7396)라 <b>보내지 않은 키는 그대로 남는다</b>.
/// 평면 모델에서 다시 만든 <c>thresholds</c> 는 지운 경계를 그냥 빠뜨리므로, 운영자가 칸을 비우고 저장해도 서버 값이 살아 있었다.
/// 지우려면 그 키를 <c>null</c> 로 보내야 한다(명세 §5.5.5 <c>{"thresholds": {"humidity": null}}</c> = 습도 임계 삭제).</para>
/// <para><b>이 창이 고치는 경계만 본다</b>(<see cref="Editable"/> 6개) — 창에 칸이 없는 경계(예 <c>ups_battery_level.low</c>)는
/// 평면 모델에 없어서 "사라진 것처럼" 보이지만 지운 것이 아니다. 그런 키는 건드리지 않는다(PATCH 라 서버 값이 남는다).</para>
/// <para>한 메트릭에서 일부 경계만 지웠으면 그 메트릭의 <b>남은 값과 함께</b> <c>{bound: null}</c> 을 싣는다 —
/// 조각은 메트릭 단위로 계산 본문을 덮어쓰므로(<c>DeviceAxisWrite.MergeJObjectOverride</c>) 남은 값을 빼면 함께 고친 값이 빠진다.
/// 메트릭의 받은 경계가 전부 지워졌으면 <c>{metric: null}</c> 이다.</para>
/// </remarks>
internal static class EnclosureThresholdClears
{
    /// <summary>임계값 창(<c>EnclosureThresholdConfigModel</c>)이 고치는 경계 — 평면 6키와 같은 표(<see cref="DeviceThresholdAxis"/>).</summary>
    internal static readonly (string Metric, string Bound)[] Editable =
    {
        (DeviceThresholdAxis.Temperature, "high"),
        (DeviceThresholdAxis.Temperature, "low"),
        (DeviceThresholdAxis.Humidity, "high"),
        (DeviceThresholdAxis.Current, "high"),
        (DeviceThresholdAxis.Voltage, "low"),
        (DeviceThresholdAxis.Vibration, "high"),
    };

    /// <summary>
    /// 받은 임계(<paramref name="received"/>)와 보낼 임계(<paramref name="next"/>)를 견줘, 지운 경계를 <c>null</c> 로 담은
    /// <c>thresholds</c> 조각을 돌려준다. 지운 것이 없으면 <c>null</c>.
    /// </summary>
    public static JObject? Build(JObject? received, JObject? next)
    {
        if (received == null) return null;

        var clears = new JObject();
        foreach (var group in Editable.GroupBy(e => e.Metric))
        {
            var metric = group.Key;
            if (received[metric] is not JObject had) continue;

            var nextMetric = next?[metric] as JObject;
            var removed = group
                .Select(e => e.Bound)
                .Where(bound => HasValue(had[bound]) && !HasValue(nextMetric?[bound]))
                .ToList();
            if (removed.Count == 0) continue;

            var nextHasAny = nextMetric != null && nextMetric.Properties().Any(p => HasValue(p.Value));
            var receivedKeepsOther = had.Properties().Any(p => HasValue(p.Value) && !removed.Contains(p.Name));
            if (!nextHasAny && !receivedKeepsOther)
            {
                clears[metric] = JValue.CreateNull();
                continue;
            }

            var piece = nextMetric != null ? (JObject)nextMetric.DeepClone() : new JObject();
            foreach (var bound in removed) piece[bound] = JValue.CreateNull();
            clears[metric] = piece;
        }
        return clears.Count == 0 ? null : clears;
    }

    private static bool HasValue(JToken? token) => token != null && token.Type != JTokenType.Null;
}
