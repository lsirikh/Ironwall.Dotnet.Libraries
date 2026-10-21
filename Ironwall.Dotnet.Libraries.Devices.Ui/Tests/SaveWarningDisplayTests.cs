using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 저장 성공 응답의 경고 표시(GOP API 8.0.15 대조 G17) — 상세 폼 축 값 저장(<see cref="DeviceAxisWriter"/>)과
/// 등록 경로 공용 입구(<see cref="SaveWarningNotice"/>). 영어 코드가 화면 글에 새지 않는지까지 본다.
/// </summary>
[Collection("CaliburnIoC")]
public sealed class SaveWarningDisplayTests : System.IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private sealed class Probe : IServerContractProbe
    {
        public Probe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => "8.0.11";
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    /// <summary>축 값 PATCH 를 성공시키며 정해 준 경고를 봉투에 싣는다.</summary>
    private sealed class WarningApi : MockDeviceApiService, IDeviceApiService
    {
        private readonly List<ResponseWarningDto> _warnings;
        public WarningApi(params ResponseWarningDto[] warnings) => _warnings = warnings.ToList();
        public int Calls { get; private set; }

        Task<ApiResponse<object>> IDeviceApiService.PatchDeviceAxesAsync(string deviceTypePath, int deviceId, JObject body, CancellationToken token)
        {
            Calls++;
            var response = ApiResponse<object>.CreateSuccess(new object());
            response.Warnings = _warnings.Count == 0 ? null : _warnings.ToList();
            return Task.FromResult(response);
        }
    }

    private sealed class Sink : IHandle<OpenInfoPopupMessageModel>
    {
        public List<OpenInfoPopupMessageModel> Messages { get; } = new();
        public Task HandleAsync(OpenInfoPopupMessageModel message, CancellationToken cancellationToken) { Messages.Add(message); return Task.CompletedTask; }
    }

    private static EnclosureDeviceModel Enclosure(int id) => new() { Id = id, DeviceNumber = id, DeviceName = $"함체 {id}", UnitId = 3 };

    private static DevicePropertySpec ThresholdSpec()
        => DevicePropertyCatalog.For(EnumDeviceCategory.Enclosure, isAxisContract: true, isUnitEra: true)
                                .First(s => s.Key == "device_config.thresholds.vibration.high");

    private static ResponseWarningDto Unmatched(string metric)
        => new() { Code = "UNMATCHED_THRESHOLD", Field = $"thresholds.{metric}", Message = "no producer" };

    [Fact]
    public async Task should_add_plain_warning_to_status_line_when_axis_save_returns_unmatched_threshold()
    {
        var api = new WarningApi(Unmatched("vibration"));
        var writer = new DeviceAxisWriter(api, new DeviceQueryPolicy(new Probe(EnumServerContract.V8_0)));

        var result = await writer.ApplyAsync(new IBaseDeviceModel[] { Enclosure(5) }, new[] { (ThresholdSpec(), "3") });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.WarningText);
        Assert.Contains("진동", result.WarningText);
        Assert.Contains("경보가 동작하지 않습니다", result.Message);
        Assert.DoesNotContain("UNMATCHED", result.Message);
        Assert.DoesNotContain("vibration", result.Message);
    }

    [Fact]
    public async Task should_say_the_warning_once_when_several_devices_return_the_same_warning()
    {
        var api = new WarningApi(Unmatched("vibration"));
        var writer = new DeviceAxisWriter(api, new DeviceQueryPolicy(new Probe(EnumServerContract.V8_0)));

        var result = await writer.ApplyAsync(new IBaseDeviceModel[] { Enclosure(5), Enclosure(6) }, new[] { (ThresholdSpec(), "3") });

        Assert.Equal(2, api.Calls);
        Assert.Single(result.WarningText!.Split('\n'));
        Assert.Equal(1, result.WarningText!.Split("진동").Length - 1);
    }

    [Fact]
    public async Task should_keep_status_line_unchanged_when_axis_save_has_no_warnings()
    {
        var api = new WarningApi();
        var writer = new DeviceAxisWriter(api, new DeviceQueryPolicy(new Probe(EnumServerContract.V8_0)));

        var result = await writer.ApplyAsync(new IBaseDeviceModel[] { Enclosure(5) }, new[] { (ThresholdSpec(), "3") });

        Assert.Null(result.WarningText);
        Assert.EndsWith("값을 저장했습니다.", result.Message);
    }

    [Fact]
    public async Task should_open_info_popup_in_plain_words_when_registration_returns_server_id_missing()
    {
        var aggregator = new EventAggregator();
        var sink = new Sink();
        aggregator.SubscribeOnPublishedThread(sink);
        var warnings = new[] { new ResponseWarningDto { Code = "SERVER_ID_MISSING", Field = "server_id", Message = "speaker has no server" } };

        var text = await SaveWarningNotice.AnnounceAsync(aggregator, warnings);

        var shown = Assert.Single(sink.Messages);
        Assert.Equal(SaveWarningNotice.Title, shown.Title);
        Assert.Equal(text, shown.Explain);
        Assert.Contains("방송 서버", shown.Explain);
        Assert.DoesNotContain("SERVER_ID", shown.Explain);
    }

    [Fact]
    public async Task should_not_open_anything_when_registration_warnings_are_only_ignorable_codes()
    {
        var aggregator = new EventAggregator();
        var sink = new Sink();
        aggregator.SubscribeOnPublishedThread(sink);
        var warnings = new[] { new ResponseWarningDto { Code = "READ_ONLY_IGNORED", Field = "status", Message = "ignored" } };

        var text = await SaveWarningNotice.AnnounceAsync(aggregator, warnings);

        Assert.Null(text);
        Assert.Empty(sink.Messages);
    }
}
