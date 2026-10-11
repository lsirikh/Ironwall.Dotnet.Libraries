using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 원격 6.3.x 대비 — 6.3 서버의 GET /api/servers/{id}/metrics/latest 는 threshold_exceeded 를
                  지표 이름 → {level, value, threshold} dict 로 보낸다(6.3.0 payload app/routers/server_metrics.py:51-100 _check_thresholds,
                  서버가 자기 임계로 판정한 값). 종전엔 배열(7.0+)만 읽어 6.3 에서 임계 초과 배지가 조용히 하나도 뜨지 않았다.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class OldServerMetricBadgeTests
{
    /// <summary>6.3.0 <c>latest_metrics</c> 한 줄(<c>_metrics_to_response</c> + <c>threshold_exceeded</c>).</summary>
    private const string Latest630 = """
    {"id":120,"server_id":3,"cpu_usage":95.2,"ram_usage":83.0,"ram_total_gb":32.0,"ram_used_gb":26.6,"disk_usage":55.0,
     "disk_total_gb":500.0,"disk_used_gb":275.0,"network_in_mbps":10.0,"network_out_mbps":5.0,"process_count":210,"detail":null,
     "collected_at":"2026-10-11T10:00:00+09:00","created_at":"2026-10-11T10:00:01+09:00",
     "threshold_exceeded":{"cpu":{"level":"critical","value":95.2,"threshold":90},"ram":{"level":"warning","value":83.0,"threshold":80}}}
    """;

    [Fact]
    [Trait("Scenario", "DEP-310")]
    public void should_read_the_6_3_threshold_dictionary_as_server_judged_items()
    {
        var dto = JsonConvert.DeserializeObject<ServerMetricDto>(Latest630)!;

        var items = dto.ThresholdExceededItems.OrderBy(i => i.Field).ToArray();

        Assert.Equal(new[] { "cpu", "ram" }, items.Select(i => i.Field).ToArray());
        Assert.Equal(("critical", 95.2, 90d, "HIGH"), (items[0].Severity, items[0].Value, items[0].Threshold, items[0].Direction));
        Assert.Equal(("warning", 83d, 80d), (items[1].Severity, items[1].Value, items[1].Threshold));
    }

    [Fact]
    [Trait("Scenario", "DEP-310")]
    public void should_draw_the_6_3_server_badges_on_the_metric_band()
    {
        var cells = ServerMetricBand.Band(JsonConvert.DeserializeObject<ServerMetricDto>(Latest630));

        var cpu = cells.Single(c => c.Key == "cpu");
        Assert.True(cpu.IsCritical);
        Assert.Equal("임계 초과 · 90 초과", cpu.BadgeText);
        var ram = cells.Single(c => c.Key == "ram");
        Assert.False(ram.IsCritical);
        Assert.Equal("임계 경고 · 80 초과", ram.BadgeText);
        Assert.Null(cells.Single(c => c.Key == "disk").BadgeText);       // 서버가 판정하지 않은 지표엔 배지가 없다
    }

    [Fact]
    [Trait("Scenario", "DEP-310")]
    public void should_still_read_the_7_0_threshold_array()
    {
        // 무회귀 — 7.0+ 배열 모양.
        var dto = new ServerMetricDto
        {
            ThresholdExceeded = JArray.Parse("""[{"field":"disk","value":97,"threshold":95,"direction":"HIGH","severity":"critical"}]"""),
        };

        var item = Assert.Single(dto.ThresholdExceededItems);
        Assert.Equal(("disk", "critical", 95d), (item.Field, item.Severity, item.Threshold));
    }

    [Fact]
    [Trait("Scenario", "DEP-310")]
    public void should_skip_dictionary_entries_that_are_not_judgements()
    {
        var dto = new ServerMetricDto { ThresholdExceeded = JObject.Parse("""{"cpu":true,"ram":{"level":"warning"}}""") };

        Assert.Empty(dto.ThresholdExceededItems);   // threshold 가 없는 항목은 판정이 아니다 — 지어내지 않는다
    }
}
