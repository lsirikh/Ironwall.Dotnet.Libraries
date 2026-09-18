using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 통문·함체 문 개폐 명령 NATS 발행 서비스.
                  브로커 연동설계 v1.6 §7 "제어 명령 — 함체·통문 문 개폐(all.*, v6.3.16)":
                  서버 REST POST …/{id}/control 제거 · gop_command 채널 폐지 →
                  클라(Central/GIS) → 구동 담당 매니저 NATS 직행.
                  Subject: "{DomainNats}.{GroupNats}.all.gate-door"
                           "{DomainNats}.{GroupNats}.all.enclosure-door"
                  m_type = PUB(명세 예시 그대로 — RSP 규격 없음), from = "GIS".
   Created By   : GHLee
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DoorControlService : IDoorControlService
{
    #region - Ctors -
    public DoorControlService(INatsService natsService, INatsSetupModel natsSetupModel, ILogService? log = null)
    {
        _natsService = natsService ?? throw new ArgumentNullException(nameof(natsService));
        _natsSetup = natsSetupModel ?? throw new ArgumentNullException(nameof(natsSetupModel));
        _log = log;
    }
    #endregion

    #region - Implementation of Interface -
    public async Task<bool> PublishDoorCommandAsync(
        int deviceId,
        EnumDeviceType deviceType,
        string command,
        string? deviceDescription = null,
        string? requestedBy = null,
        CancellationToken token = default)
    {
        // 경계 검증 — 모르는 subject·어휘로 발행하면 매니저가 조용히 버리고 아무 일도 일어나지 않는다.
        if (deviceId <= 0)
        {
            _log?.Warning($"[개폐] 발행 취소 — 잘못된 device_id({deviceId})");
            return false;
        }

        var normalized = NormalizeCommand(command);
        if (normalized is null)
        {
            _log?.Warning($"[개폐] 발행 취소 — 지원하지 않는 명령('{command}'). OPEN/CLOSE 만 허용.");
            return false;
        }

        var topic = ResolveTopic(deviceType);
        if (topic is null)
        {
            _log?.Warning($"[개폐] 발행 취소 — 문이 없는 장비 타입({deviceType})");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_natsSetup.DomainNats) || string.IsNullOrWhiteSpace(_natsSetup.GroupNats))
        {
            _log?.Warning("[개폐] 발행 취소 — NATS Domain/Group(부대ID) 미설정. subject 를 만들 수 없다.");
            return false;
        }

        var body = new DoorSetBodyDto
        {
            DeviceId = deviceId,
            Command = normalized,
            DeviceDescription = string.IsNullOrWhiteSpace(deviceDescription)
                                ? BuildFallbackDescription(deviceId, deviceType)
                                : deviceDescription!,
            RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? null : requestedBy,
            // 서버 규약: ISO 8601 + 오프셋 필수(naive 금지)
            RequestedAt = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
        };

        var subject = BuildSubject(topic);
        var cmd = ResolveCommandName(deviceType);

        try
        {
            token.ThrowIfCancellationRequested();
            var envelope = body.ToBrokerPublish(cmd, FromSystem);
            await _natsService.PublishAsync(subject, JsonConvert.SerializeObject(envelope)).ConfigureAwait(false);
            _log?.Info($"[개폐] {cmd} 발행 — subject={subject} device_id={deviceId} command={normalized}");
            return true;
        }
        catch (OperationCanceledException)
        {
            _log?.Warning($"[개폐] {cmd} 발행 취소(cancel) — device_id={deviceId}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"[개폐] {cmd} 발행 실패 — subject={subject} device_id={deviceId}: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// Subject 빌드 — "{DomainNats}.{GroupNats}.all.{topic}".
    /// <para>⚠ 두 번째 토큰은 <b>부대 코드</b>(예: unit001)다. <c>global</c> 은 편제·카탈로그 등
    /// 전역 자원 SYNC 전용이고(서버 GLOBAL_CMDS), 문 명령은 부대 자원이다(명세 §7 표 · 전송경로 도식
    /// <c>sensorway.{부대ID}.all.gate-door</c>).</para>
    /// </summary>
    public string BuildSubject(string topic)
        => $"{_natsSetup.DomainNats}.{_natsSetup.GroupNats}.all.{topic}";

    /// <summary>장비 타입 → subject 마지막 토큰. 문이 없는 타입이면 null.</summary>
    internal static string? ResolveTopic(EnumDeviceType deviceType) => deviceType switch
    {
        EnumDeviceType.Gate => GateTopic,
        EnumDeviceType.Enclosure => EnclosureTopic,
        _ => null,
    };

    /// <summary>장비 타입 → cmd 이름.</summary>
    internal static string ResolveCommandName(EnumDeviceType deviceType) => deviceType switch
    {
        EnumDeviceType.Gate => GateDoorSetCommand,
        _ => EnclosureDoorSetCommand,
    };

    /// <summary>명령 어휘 정규화 — OPEN/CLOSE 만 허용(대소문자·공백 관용). 그 밖은 null.</summary>
    internal static string? NormalizeCommand(string? command)
    {
        var value = command?.Trim().ToUpperInvariant();
        return value switch
        {
            DoorSetBodyDto.Open => DoorSetBodyDto.Open,
            DoorSetBodyDto.Close => DoorSetBodyDto.Close,
            _ => null,
        };
    }

    /// <summary>
    /// 장비 이름을 모를 때의 최소 식별 흔적 — 명세가 허용하는 <b>카테고리만</b> 형식.
    /// (종류값을 추측해 넣지 않는다. 수신측은 이 문자열을 파싱하지 않는다.)
    /// </summary>
    internal static string BuildFallbackDescription(int deviceId, EnumDeviceType deviceType)
        => $"[{ResolveCategory(deviceType)}] (id: {deviceId})";

    /// <summary>category_device — 서버는 <b>소문자</b>를 쓴다.</summary>
    internal static string ResolveCategory(EnumDeviceType deviceType) => deviceType switch
    {
        EnumDeviceType.Gate => "gate",
        EnumDeviceType.Enclosure => "enclosure",
        _ => "device",
    };

    /// <summary>
    /// 명세 형식의 device_description 을 만든다 —
    /// <c>[&lt;category&gt;] &lt;name&gt; (number: N, id: ID)</c>.
    /// 종류값은 클라가 보유하지 않으므로 카테고리만 넣는다(명세 허용).
    /// </summary>
    public static string BuildDescription(int deviceId, EnumDeviceType deviceType, string? deviceName, int deviceNumber)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return BuildFallbackDescription(deviceId, deviceType);
        return $"[{ResolveCategory(deviceType)}] {deviceName!.Trim()} (number: {deviceNumber}, id: {deviceId})";
    }
    #endregion

    #region - Attributes -
    /// <summary>명세 §7 cmd 이름 — 브로커 연동설계 v1.6.</summary>
    internal const string GateDoorSetCommand = "GATE_DOOR_SET";
    internal const string EnclosureDoorSetCommand = "ENCLOSURE_DOOR_SET";

    /// <summary>subject 마지막 토큰.</summary>
    internal const string GateTopic = "gate-door";
    internal const string EnclosureTopic = "enclosure-door";

    /// <summary>발신 클라 식별 — 명세 §7 "from = Central | GIS".</summary>
    internal const string FromSystem = "GIS";

    private readonly INatsService _natsService;
    private readonly INatsSetupModel _natsSetup;
    private readonly ILogService? _log;
    #endregion
}
