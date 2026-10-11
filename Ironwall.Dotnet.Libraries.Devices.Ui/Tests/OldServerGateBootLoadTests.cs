using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 원격 6.3.x 대비(F1) — 통문 자원(GET /api/devices/gates)이 없는 6.3.0~6.3.2 서버에서
                  부팅 적재가 통문 404/405 를 '이 판본 미지원' 정보 1줄로 넘기고 경광등 · 그룹 적재를 계속하는가
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class OldServerGateBootLoadTests
{
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

    private sealed class RecordingAggregator : IEventAggregator
    {
        public ConcurrentQueue<object> Published { get; } = new();
        public bool HandlerExistsFor(Type messageType) => false;
        public void Subscribe(object subscriber) { }
        public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal) { }
        public void Unsubscribe(object subscriber) { }
        public Task PublishAsync(object message, CancellationToken cancellationToken = default) { Published.Enqueue(message); return Task.CompletedTask; }
        public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = default) { Published.Enqueue(message); return Task.CompletedTask; }
    }

    private sealed class FixedProbe : IServerContractProbe
    {
        public EnumServerContract Contract => EnumServerContract.V6_3;
        public string? RawVersion => "6.3.0";
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    private static (DeviceProviderService Service, MockDeviceApiService Api, RecordingLog Log, RecordingAggregator Ea) Rig(int gateStatus, string gateCode)
    {
        var api = new MockDeviceApiService();
        var failure = ApiListResponse<GateDeviceDto>.CreateError(gateCode, $"HTTP {gateStatus}: 6.3.0 에 없는 경로", "{\"detail\":\"Not Found\"}");
        failure.StatusCode = gateStatus;
        api.GateResponses.Add(failure);
        var log = new RecordingLog();
        var ea = new RecordingAggregator();
        var provider = new DeviceProvider();
        var service = new DeviceProviderService(
            logService: log,
            eventAggregator: ea,
            apiService: api,
            deviceProvider: provider,
            controllerProvider: new ControllerDeviceProvider(log, provider),
            sensorProvider: new SensorDeviceProvider(log, provider),
            cameraProvider: new CameraDeviceProvider(log, provider),
            deviceGroupProvider: new DeviceGroupProvider(log),
            serverApiService: new MockServerApiService(),
            serverProvider: new ServerProvider(log),
            queryPolicy: new DeviceQueryPolicy(new FixedProbe()));
        return (service, api, log, ea);
    }

    [Theory]
    [Trait("Scenario", "DEP-306")]
    [InlineData(404, "NOT_FOUND")]
    [InlineData(405, "METHOD_NOT_ALLOWED")]
    public async Task should_skip_gates_with_one_info_line_and_keep_loading_when_the_6_3_server_has_no_gate_resource(int status, string code)
    {
        var (service, api, log, ea) = Rig(status, code);

        await service.FetchAllDevicesAsync();

        Assert.DoesNotContain(log.Errors, e => e.Contains("gate", StringComparison.OrdinalIgnoreCase));
        var notice = Assert.Single(log.Infos, i => i.Contains("이 서버 판본"));
        Assert.Contains("통문", notice);
        Assert.Contains("6.3.0", notice);
        Assert.Contains(status.ToString(), notice);
        Assert.Single(api.ListRequests, r => r.StartsWith("gates "));       // 한 번만 묻고 다음 쪽을 넘기지 않는다
        Assert.Contains(api.ListRequests, r => r.StartsWith("lamps "));     // 통문 뒤 적재가 이어진다
        Assert.Contains(ea.Published, m => m is AllDevicesLoadedMessage);   // 로그인 커버가 내려간다
    }

    [Fact]
    [Trait("Scenario", "DEP-306")]
    public async Task should_still_log_an_error_when_the_gate_list_fails_for_another_reason()
    {
        // 대조군 — 서버 오류(500)는 '미지원'이 아니다. 종전처럼 오류 로그로 남긴다.
        var (service, _, log, ea) = Rig(500, "INTERNAL_ERROR");

        await service.FetchAllDevicesAsync();

        Assert.Contains(log.Errors, e => e.Contains("gates", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(log.Infos, i => i.Contains("이 서버 판본"));
        Assert.Contains(ea.Published, m => m is AllDevicesLoadedMessage);
    }
}
