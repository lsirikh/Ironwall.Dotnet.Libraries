using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 함체 임계값 창 저장(GOP API 8.0.15 대조 G16 · G17) — 진짜 <see cref="DeviceApiService"/> 를 가짜 HTTP 처리기 위에 올려
/// <b>실제 직렬화된 본문</b>을 본다. 창 없음 · 원격 없음.
/// </summary>
[Collection("CaliburnIoC")]
public sealed class EnclosureThresholdClearTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private const string ReceivedThresholds =
        """{"temperature":{"high":45.0,"low":-10.0},"humidity":{"high":80.0},"ups_battery_level":{"low":20.0}}""";

    private sealed class Probe : IServerContractProbe
    {
        public Probe(EnumServerContract contract, string raw) { Contract = contract; RawVersion = raw; }
        public EnumServerContract Contract { get; }
        public string? RawVersion { get; }
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    private sealed class Handler : DelegatingHandler
    {
        private readonly string _reply;
        public ConcurrentQueue<(HttpMethod Method, string Body)> Writes { get; } = new();
        public Handler(string reply) => _reply = reply;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Get)
                Writes.Enqueue((request.Method, request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_reply, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }

    private static string Reply(string warnings = "[]") =>
        "{\"success\":true,\"message\":\"ok\",\"data\":{\"id\":5,\"name_device\":\"함체-5\"},\"warnings\":" + warnings + "}";

    private static (IDeviceApiService Api, Handler Http, DeviceQueryPolicy Policy) Rig(EnumServerContract contract, string raw, string reply)
    {
        var http = new Handler(reply);
        var setup = new ApiSetupModel { Url = "http://127.0.0.1:9/api" };
        var api = new ApiService(null, setup, http);
        api.Initialize();
        var probe = new Probe(contract, raw);
        return (new DeviceApiService(null, api, setup, probe), http, new DeviceQueryPolicy(probe));
    }

    private static EnclosureDeviceModel Enclosure()
    {
        var model = new EnclosureDeviceModel
        {
            Id = 5,
            DeviceName = "함체-5",
            ThresholdConfig = new EnclosureThresholdConfigModel { TempHigh = 45, TempLow = -10, HumidityHigh = 80 },
        };
        model.Axes = new DeviceAxesModel { DeviceConfig = new DeviceConfigModel { Thresholds = JObject.Parse(ReceivedThresholds) } };
        return model;
    }

    private static JObject SentThresholds(Handler http)
    {
        var (method, body) = Assert.Single(http.Writes);
        Assert.Equal(HttpMethod.Patch, method);
        return (JObject)JObject.Parse(body)["device_config"]!["thresholds"]!;
    }

    [Fact]
    public async Task should_send_null_for_cleared_bound_when_temp_high_is_erased_on_axis_server()
    {
        var (api, http, policy) = Rig(EnumServerContract.V8_0, "8.0.11", Reply());
        var dialog = new EnclosureThresholdDialogViewModel(Enclosure());
        dialog.Working.TempHigh = null;

        Assert.True(await dialog.SaveAsync(api, null, policy, null));

        var thresholds = SentThresholds(http);
        Assert.True(thresholds.SelectToken("temperature.high") is { Type: JTokenType.Null }, thresholds.ToString());   // 지운 경계 = 명시적 null
        Assert.Equal(-10.0, (double)thresholds["temperature"]!["low"]!);          // 같은 메트릭의 남은 값은 그대로
        Assert.Equal(80.0, (double)thresholds["humidity"]!["high"]!);
        Assert.Null(thresholds["ups_battery_level"]);                              // 창에 없는 경계는 건드리지 않는다
    }

    [Fact]
    public async Task should_send_null_for_whole_metric_when_every_bound_of_it_is_erased()
    {
        var (api, http, policy) = Rig(EnumServerContract.V8_0, "8.0.11", Reply());
        var dialog = new EnclosureThresholdDialogViewModel(Enclosure());
        dialog.Working.HumidityHigh = null;

        Assert.True(await dialog.SaveAsync(api, null, policy, null));

        var thresholds = SentThresholds(http);
        Assert.True(thresholds["humidity"] is { Type: JTokenType.Null }, thresholds.ToString());
        Assert.Equal(45.0, (double)thresholds["temperature"]!["high"]!);
    }

    [Fact]
    public async Task should_send_nothing_extra_when_no_bound_is_erased()
    {
        var (api, http, policy) = Rig(EnumServerContract.V8_0, "8.0.11", Reply());
        var dialog = new EnclosureThresholdDialogViewModel(Enclosure());
        dialog.Working.TempHigh = 50;

        Assert.True(await dialog.SaveAsync(api, null, policy, null));

        var thresholds = SentThresholds(http);
        Assert.Equal(50.0, (double)thresholds["temperature"]!["high"]!);
        Assert.DoesNotContain(thresholds.Descendants(), t => t.Type == JTokenType.Null);
    }

    [Fact]
    public async Task should_keep_flat_put_without_nulls_on_6_3_server()
    {
        var (api, http, policy) = Rig(EnumServerContract.V6_3, "6.3.2", Reply());
        var dialog = new EnclosureThresholdDialogViewModel(Enclosure());
        dialog.Working.TempHigh = null;

        Assert.True(await dialog.SaveAsync(api, null, policy, null));

        var (method, body) = Assert.Single(http.Writes);
        Assert.Equal(HttpMethod.Put, method);
        var json = JObject.Parse(body);
        Assert.Null(json["device_config"]);
        Assert.True(json["threshold_config"]!["temp_high"] is null or { Type: JTokenType.Null });   // 6.3 은 평면 PUT 그대로
        Assert.Equal(-10.0, (double)json["threshold_config"]!["temp_low"]!);
    }

    [Fact]
    public async Task should_announce_unmatched_threshold_warning_in_plain_words_after_save()
    {
        const string warnings = """[{"field":"thresholds.vibration","code":"UNMATCHED_THRESHOLD","message":"no producer"}]""";
        var (api, _, policy) = Rig(EnumServerContract.V8_0, "8.0.11", Reply(warnings));
        var aggregator = new EventAggregator();
        var sink = new Sink();
        aggregator.SubscribeOnPublishedThread(sink);
        var dialog = new EnclosureThresholdDialogViewModel(Enclosure());
        dialog.Working.VibrationHigh = 3;

        Assert.True(await dialog.SaveAsync(api, null, policy, aggregator));

        Assert.NotNull(dialog.LastWarningText);
        Assert.Contains("진동", dialog.LastWarningText);
        Assert.DoesNotContain("UNMATCHED", dialog.LastWarningText);
        var shown = Assert.Single(sink.Messages);
        Assert.Equal(dialog.LastWarningText, shown.Explain);
    }

    [Fact]
    public async Task should_not_announce_anything_when_server_returns_no_warnings()
    {
        var (api, _, policy) = Rig(EnumServerContract.V8_0, "8.0.11", Reply());
        var aggregator = new EventAggregator();
        var sink = new Sink();
        aggregator.SubscribeOnPublishedThread(sink);
        var dialog = new EnclosureThresholdDialogViewModel(Enclosure());

        Assert.True(await dialog.SaveAsync(api, null, policy, aggregator));

        Assert.Null(dialog.LastWarningText);
        Assert.Empty(sink.Messages);
    }

    [Theory]
    [InlineData("""{"temperature":{"high":45,"low":-10}}""", """{"temperature":{"low":-10}}""", """{"temperature":{"low":-10,"high":null}}""")]
    [InlineData("""{"temperature":{"high":45,"low":-10}}""", null, """{"temperature":null}""")]
    [InlineData("""{"voltage":{"low":11}}""", """{"current":{"high":5}}""", """{"voltage":null}""")]
    [InlineData("""{"ups_battery_level":{"low":20}}""", null, null)]
    [InlineData("""{"temperature":{"high":45}}""", """{"temperature":{"high":45}}""", null)]
    public void should_build_clear_piece_only_for_erased_editable_bounds(string received, string? next, string? expected)
    {
        var piece = EnclosureThresholdClears.Build(JObject.Parse(received), next == null ? null : JObject.Parse(next));

        if (expected == null) Assert.Null(piece);
        else Assert.True(JToken.DeepEquals(JObject.Parse(expected), piece), piece?.ToString());
    }

    private sealed class Sink : IHandle<OpenInfoPopupMessageModel>
    {
        public List<OpenInfoPopupMessageModel> Messages { get; } = new();
        public Task HandleAsync(OpenInfoPopupMessageModel message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
