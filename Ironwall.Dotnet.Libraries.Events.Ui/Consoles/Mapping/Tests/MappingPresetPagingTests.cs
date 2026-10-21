using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.Tests;
/****************************************************************************
   Purpose      : G21 — 카메라 프리셋 목록을 끝까지 모은다(서버 기본 한 쪽 10건에 잘리지 않게)
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public class MappingPresetPagingTests
{
    private const int CAMERA = 379;

    /// <summary>
    /// 프리셋 <paramref name="count"/> 건을 가진 서버 흉내. 명세 §5.7 처럼 <c>limit</c> 기본 10 · 최대 100,
    /// <paramref name="pageSize"/> 를 주면 요청 limit 과 상관없이 그 크기로 자른다(서버가 한도를 낮춘 경우).
    /// </summary>
    private static MappingScriptedApi PresetServer(int count, int? pageSize = null, bool withPagination = true, bool withTotal = true)
        => new((endpoint, q) =>
        {
            var limit = pageSize ?? (q.TryGetValue("limit", out var l) ? System.Math.Min(int.Parse(l), 100) : 10);
            var page = q.TryGetValue("page", out var p) ? int.Parse(p) : 1;
            var items = new JArray(Enumerable.Range(1, count).Skip((page - 1) * limit).Take(limit)
                .Select(i => new JObject
                {
                    ["id"] = 5000 + i,
                    ["camera_id"] = CAMERA,
                    ["preset_index"] = i,
                    ["preset_name"] = $"프리셋 {i}",
                    ["touring_time"] = 10,
                    ["is_restricted_zone"] = false,
                }));
            var data = new JObject { ["items"] = items };
            if (withTotal) data["total"] = count;
            var envelope = new JObject { ["success"] = true, ["data"] = data };
            if (withPagination)
                envelope["pagination"] = new JObject
                {
                    ["page"] = page,
                    ["limit"] = limit,
                    ["total"] = count,
                    ["total_pages"] = (count + limit - 1) / limit,
                };
            return (HttpStatusCode.OK, envelope.ToString());
        });

    [Fact]
    public async Task 프리셋_후보는_한도_100으로_요청해_25건을_모두_받는다()
    {
        var api = PresetServer(25);
        var result = await api.Gateway().ListPresetsAsync(CAMERA);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(25, result.Value!.Count);
        Assert.Equal("100", api.Calls[0].Query.GetValueOrDefault("limit"));
    }

    [Fact]
    public async Task 서버가_10건씩_3쪽으로_주면_쪽을_올려_25건을_모은다()
    {
        var api = PresetServer(25, pageSize: 10);

        var options = await api.Gateway().ListPresetsAsync(CAMERA);
        Assert.Equal(25, options.Value!.Count);
        Assert.Equal(Enumerable.Range(5001, 25), options.Value.Select(p => p.Id));
        Assert.Equal(new[] { "1", "2", "3" }, api.Calls.Select(c => c.Query.GetValueOrDefault("page")));

        api.Calls.Clear();
        var details = await api.Gateway().ListPresetDetailsAsync(CAMERA);
        Assert.Equal(25, details.Value!.Count);
        Assert.Equal(Enumerable.Range(1, 25), details.Value.Select(p => p.PresetIndex));
        Assert.Equal(3, api.Calls.Count);
        Assert.All(api.Calls, c => Assert.Equal("100", c.Query.GetValueOrDefault("limit")));
    }

    [Fact]
    public async Task total_pages가_없으면_total로_끝을_안다()
    {
        var api = PresetServer(25, pageSize: 10, withPagination: false);
        var result = await api.Gateway().ListPresetDetailsAsync(CAMERA);

        Assert.Equal(25, result.Value!.Count);
        Assert.Equal(3, api.Calls.Count);
    }

    [Fact]
    public async Task 쪽수도_총수도_없는_옛_응답은_한_쪽만_읽는다()
    {
        // 6.3 류 배열 응답 — 끝을 알 수 없으면 받은 수가 요청 한도보다 적을 때 멈춘다.
        var api = new MappingScriptedApi((_, _) => (HttpStatusCode.OK,
            "{\"success\":true,\"data\":[{\"id\":1,\"camera_id\":379,\"preset_index\":1,\"preset_name\":\"a\"}]}"));
        var result = await api.Gateway().ListPresetsAsync(CAMERA);

        Assert.Single(result.Value!);
        Assert.Single(api.Calls);
    }

    [Fact]
    public async Task 둘째_쪽이_실패하면_목록_전체를_실패로_돌려준다()
    {
        var api = new MappingScriptedApi((_, q) => q.GetValueOrDefault("page") == "2"
            ? (HttpStatusCode.InternalServerError, "{}")
            : (HttpStatusCode.OK, "{\"success\":true,\"data\":{\"items\":[{\"id\":1,\"preset_index\":1}],\"total\":2},\"pagination\":{\"page\":1,\"limit\":1,\"total\":2,\"total_pages\":2}}"));
        var result = await api.Gateway().ListPresetsAsync(CAMERA);

        Assert.False(result.IsSuccess);
        Assert.Equal(500, result.StatusCode);
    }
}
