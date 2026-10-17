using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 원격 6.3.x 대비(F2) — 6.3.0 서버의 매핑 카메라 응답(camera:{id…} 중첩)에서 탐지 창 카메라가 조용히 0개가 되지 않는가
                  서버 응답 본문은 6.3.0 소스(api-test-server offline-installer payload app/routers/event_mapping_cameras.py
                  _build_response · _build_preset_nested) 모양 그대로다. 호스트 시험 OldServerCompatScenarioTests.MappingServer 와 같은 본문.
                  진짜 ApiService · EventApiService · EventMappingCameraCache 를 가짜 HTTP 처리기 위에 올린다 — 창 없음 · 원격 없음.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class OldServerMappingCameraCompatTests
{
    private const string MappingList630 =
        "{\"success\":true,\"message\":\"ok\",\"data\":[{\"id\":7,\"name_event\":\"침입\",\"device_group_id\":3,\"status\":true," +
        "\"category_event_mapping\":\"FENCE_SENSOR_ONLY\",\"description\":null,\"created_at\":\"2026-08-01T09:00:00+09:00\",\"updated_at\":\"2026-08-01T09:00:00+09:00\"}]," +
        "\"pagination\":{\"page\":1,\"limit\":100,\"total\":1,\"total_pages\":1}}";

    /// <summary>6.3.0 <c>GET …/event-mappings/7/cameras</c> — 최상위 <c>camera_id</c> 없음, <c>camera</c> · <c>target_preset</c> · <c>home_preset</c> 중첩.</summary>
    private const string Cameras630 =
        "{\"success\":true,\"message\":\"Event mapping cameras retrieved successfully\",\"data\":{\"items\":[" +
        "{\"id\":31,\"event_mapping_id\":7," +
        "\"camera\":{\"id\":5,\"number_device\":5,\"group_device\":1,\"name_device\":\"CAM-5\",\"type_device\":\"Camera\",\"status\":\"Active\"," +
        "\"is_enable\":true,\"ip_address\":\"10.0.0.5\",\"ip_port\":80,\"device_groups\":[]}," +
        "\"target_preset\":{\"id\":61,\"camera_id\":5,\"camera_name\":\"CAM-5\",\"preset_index\":3,\"preset_name\":\"정문\",\"touring_time\":10}," +
        "\"home_preset\":{\"id\":60,\"camera_id\":5,\"camera_name\":\"CAM-5\",\"preset_index\":1,\"preset_name\":\"홈\",\"touring_time\":10}," +
        "\"delay_time\":2,\"is_enable\":true,\"priority\":1,\"created_at\":\"2026-08-01T09:00:00+09:00\",\"updated_at\":\"2026-08-01T09:00:00+09:00\"}," +
        "{\"id\":32,\"event_mapping_id\":7," +
        "\"camera\":{\"id\":6,\"number_device\":6,\"group_device\":1,\"name_device\":\"CAM-6\",\"type_device\":\"Camera\",\"status\":\"Active\"," +
        "\"is_enable\":true,\"ip_address\":\"10.0.0.6\",\"ip_port\":80,\"device_groups\":[]},\"target_preset\":null,\"home_preset\":null," +
        "\"delay_time\":0,\"is_enable\":true,\"priority\":2,\"created_at\":\"2026-08-01T09:00:00+09:00\",\"updated_at\":\"2026-08-01T09:00:00+09:00\"}" +
        "],\"total\":2}}";

    private sealed class RouteHandler : DelegatingHandler
    {
        private readonly Func<string, string?> _route;
        public ConcurrentQueue<string> Paths { get; } = new();
        public RouteHandler(Func<string, string?> route) => _route = route;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Enqueue(path);
            var body = _route(path);
            return Task.FromResult(new HttpResponseMessage(body is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(body ?? "{\"detail\":\"Not Found\"}", System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
        }
    }

    private sealed class RecordingLog : ILogService
    {
        public ConcurrentQueue<string> Infos { get; } = new();
        public ConcurrentQueue<string> Warnings { get; } = new();
        public ConcurrentQueue<string> Errors { get; } = new();
        public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Errors.Enqueue(msg);
        public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Infos.Enqueue(msg);
        public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Warnings.Enqueue(msg);
#pragma warning disable CS0067
        public event EventHandler<LogEventArgs>? LogEvent;
#pragma warning restore CS0067
    }

    private static (EventMappingCameraCache Cache, RouteHandler Http, RecordingLog Log) Rig(string camerasBody)
    {
        var http = new RouteHandler(path => path switch
        {
            "/api/integrations/event-mappings" => MappingList630,
            "/api/integrations/event-mappings/7/cameras" => camerasBody,
            _ => null,
        });
        var log = new RecordingLog();
        var setup = new ApiSetupModel { Url = "http://127.0.0.1:9/api" };
        var api = new ApiService(log, setup, http);
        api.Initialize();
        // 게이트웨이 없음 = 서버 계약 7.0 미만(6.3) 경로 — 옛 IEventApiService 로 읽는다.
        return (new EventMappingCameraCache(gateway: null, new EventApiService(log, api, setup), log), http, log);
    }

    [Fact]
    [Trait("Scenario", "DEP-301")]
    public async Task should_return_the_mapped_cameras_from_a_6_3_mapping_response_with_nested_camera()
    {
        var (cache, http, _) = Rig(Cameras630);

        var cameras = await cache.GetCamerasForGroupsAsync(new[] { 3 });

        Assert.Equal(new[] { "/api/integrations/event-mappings", "/api/integrations/event-mappings/7/cameras" }, http.Paths.ToArray());
        Assert.Equal(new[] { 5, 6 }, cameras.Select(c => c.CameraId).ToArray());
        Assert.Equal(new int?[] { 1, 2 }, cameras.Select(c => c.Priority).ToArray());
        Assert.Equal(2, cameras[0].DelaySeconds);
    }

    [Fact]
    [Trait("Scenario", "DEP-302")]
    public async Task should_carry_the_target_and_home_preset_numbers_from_a_6_3_mapping_response()
    {
        // 6.3.0 응답에도 target_preset.preset_index 가 있다(_build_preset_nested) — 버리면 서버에 설정한 프리셋 이동이 조용히 사라진다.
        var (cache, _, _) = Rig(Cameras630);

        var cameras = await cache.GetCamerasForGroupsAsync(new[] { 3 });

        var withPreset = cameras.Single(c => c.CameraId == 5);
        Assert.Equal((3, "정문", 1), (withPreset.TargetPresetIndex, withPreset.TargetPresetName, withPreset.HomePresetIndex));
        Assert.False(withPreset.TargetPresetRestricted);   // 6.3.0 프리셋엔 감시금지구역 표시가 없다
        var noPreset = cameras.Single(c => c.CameraId == 6);
        Assert.Null(noPreset.TargetPresetIndex);
        Assert.Null(noPreset.HomePresetIndex);
    }

    [Fact]
    [Trait("Scenario", "DEP-303")]
    public async Task should_warn_instead_of_silently_dropping_a_mapping_camera_row_without_any_camera_id()
    {
        // 카메라가 지워져 camera:null 로 온 행(6.3.0 _build_response: emc.camera 없으면 None) — 걸러내되 말없이 버리지 않는다.
        const string body =
            "{\"success\":true,\"message\":\"ok\",\"data\":{\"items\":[" +
            "{\"id\":31,\"event_mapping_id\":7,\"camera\":{\"id\":5,\"name_device\":\"CAM-5\"},\"target_preset\":null,\"home_preset\":null,\"delay_time\":0,\"is_enable\":true,\"priority\":1}," +
            "{\"id\":33,\"event_mapping_id\":7,\"camera\":null,\"target_preset\":null,\"home_preset\":null,\"delay_time\":0,\"is_enable\":true,\"priority\":2}" +
            "],\"total\":2}}";
        var (cache, _, log) = Rig(body);

        var cameras = await cache.GetCamerasForGroupsAsync(new[] { 3 });

        Assert.Equal(new[] { 5 }, cameras.Select(c => c.CameraId).ToArray());
        var warning = Assert.Single(log.Warnings, w => w.Contains("카메라 id"));
        Assert.Contains("매핑 7", warning);
        Assert.Contains("1건", warning);
    }

    [Fact]
    [Trait("Scenario", "DEP-303")]
    public async Task should_not_warn_when_every_mapping_camera_row_has_a_camera_id()
    {
        var (cache, _, log) = Rig(Cameras630);

        await cache.GetCamerasForGroupsAsync(new[] { 3 });

        Assert.DoesNotContain(log.Warnings, w => w.Contains("카메라 id"));
    }
}
