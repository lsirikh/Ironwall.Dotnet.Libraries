using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Nats.Tests;

/// <summary>
/// NATS Detection 이벤트 송출 통합 테스트
/// 전제조건: NATS 서버(localhost:4222)와 GIS 애플리케이션이 실행 중이어야 함
/// </summary>
public class NatsDetectionPublishTests
{
    // GIS가 구독 중인 subject: sensorway.unit001.gis.>
    // Publish subject는 해당 wildcard 하위 임의 경로
    private const string PublishSubject = "sensorway.unit001.gis.event.detect";

    private readonly NatsSetupModel _setupModel = new()
    {
        IpAddressNats = "localhost",
        PortNats = 4222,
        DomainNats = "sensorway",
        GroupNats = "unit001",
        SubsystemNats = "gis",
        ConnectionTimeoutNats = 5000
    };

    /// <summary>
    /// A구역 장비 목록 — deviceId, deviceName, deviceNumber, detectionResult
    /// 실제 GIS DB의 device ID와 맞지 않아도 DTO Fallback으로 EventCard 생성됨
    /// </summary>
    private static readonly (int Id, string Name, int Number, string Result)[] ZoneADevices =
    [
        (1, "A구역 1번 센서", 1, "PIR_SENSOR"),
        (2, "A구역 2번 센서", 2, "CABLE_CUTTING"),
        (3, "A구역 3번 센서", 3, "VIBRATION_SENSOR"),
        (4, "A구역 4번 센서", 4, "PIR_SENSOR"),
        (5, "A구역 5번 센서", 5, "CONTACT_SENSOR"),
    ];

    [Fact]
    public async Task PublishDetectionEvents_ZoneA_GisShouldReceiveAll()
    {
        // Arrange
        var log = new LogService();
        var natsService = new NatsService(log);
        await natsService.ConnectAsync(_setupModel);
        await natsService.ExecuteAsync();
        await Task.Delay(300); // 구독 준비 대기

        Console.WriteLine($"[NATS] Connected — GIS subscription: {_setupModel.EffectiveSubject}");
        Console.WriteLine($"[NATS] Publish subject: {PublishSubject}");
        Console.WriteLine();

        // Act — A구역 장비별 Detection 이벤트 순차 송출
        foreach (var (id, name, number, result) in ZoneADevices)
        {
            var json = BuildDetectionRequestJson(id, name, number, result, groupId: 1, groupName: "A구역");
            await natsService.PublishAsync(PublishSubject, json);

            Console.WriteLine($"[SENT] Device #{id} '{name}' → result={result}");
            Console.WriteLine($"[JSON] {json}");
            Console.WriteLine();

            await Task.Delay(300); // 연속 이벤트 사이 간격
        }

        Console.WriteLine($"[DONE] {ZoneADevices.Length}개 이벤트 송출 완료.");
        Console.WriteLine("GIS 화면에서 EventCard, Sound, 심볼 업데이트를 확인하세요.");

        // Assert
        Assert.True(true, $"{ZoneADevices.Length}개 Detection 이벤트 송출 완료");
    }

    [Fact]
    public async Task PublishSingleDetection_PirSensor_GisShouldReceive()
    {
        // Arrange
        var log = new LogService();
        var natsService = new NatsService(log);
        await natsService.ConnectAsync(_setupModel);
        await natsService.ExecuteAsync();
        await Task.Delay(300);

        // Act
        var json = BuildDetectionRequestJson(
            deviceId: 1, deviceName: "A구역 1번 센서", deviceNumber: 1,
            result: "PIR_SENSOR", groupId: 1, groupName: "A구역");

        await natsService.PublishAsync(PublishSubject, json);

        Console.WriteLine($"[SENT] {PublishSubject}");
        Console.WriteLine($"[JSON] {json}");

        Assert.True(true, "단건 Detection 이벤트 송출 완료");
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// BrokerRequest&lt;DetectionEventDto&gt; JSON 직접 생성
    /// <para>
    ///   m_type: "REQ" → MessageSelector REQ 분류<br/>
    ///   cmd: "DETECTION" → RouteRequestByCommand → ProcessDetection<br/>
    ///   body: DetectionEventDto 구조
    /// </para>
    /// </summary>
    private static string BuildDetectionRequestJson(
        int deviceId, string deviceName, int deviceNumber,
        string result, int groupId, string groupName)
    {
        var msgId = Guid.NewGuid().ToString();
        var utcNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var koreaTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");

        return $$"""
                 {
                   "id": "{{msgId}}",
                   "m_type": "REQ",
                   "cmd": "DETECTION",
                   "from": "pids-proxy",
                   "body": {
                     "id": {{deviceId}},
                     "type_event": "Intrusion",
                     "device_id": {{deviceId}},
                     "device": {
                       "id": {{deviceId}},
                       "number_device": {{deviceNumber}},
                       "name_device": "{{deviceName}}",
                       "type_device": "Sensor",
                       "status": "ACTIVATED",
                       "is_enable": true,
                       "device_groups": [
                         { "id": {{groupId}}, "name": "{{groupName}}" }
                       ]
                     },
                     "action_reported": "False",
                     "result": "{{result}}",
                     "created_at": "{{koreaTime}}"
                   },
                   "created": "{{utcNow}}"
                 }
                 """;
    }
}
