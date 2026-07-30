using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Nats.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 경광등 제어 NATS 발행 서비스 (GIS.md v1.5.2 §2.2~2.5)
                  LAMP_CLEAR / LAMP_OFF / LAMP_COLOR_SET / LAMP_BUZZER_SET 4종을
                  REQ 발행하고 RSP(success/req_id/message)를 확인한다(§6.4 통일 규칙).
                  Subject: "{DomainNats}.{GroupNats}.proxy.{action}" (수신=PidsProxy)
                  ※ 경광등 UI는 아직 없음 — 로직 계층 선행 구축(PRD FR-07). 발행/RSP는
                    BrokerRequestClient(공통 실행기, 기본 5s) 경유.
   Created By   : GHLee
   Created On   : 2026-07-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class LampControlService : ILampControlService
{
    #region - Ctors -
    public LampControlService(IBrokerRequestClient brokerClient, INatsSetupModel natsSetupModel, ILogService? log = null)
    {
        _brokerClient = brokerClient;
        _natsSetup = natsSetupModel;
        _log = log;
    }
    #endregion

    #region - Implementation of Interface -
    public async Task<BrokerRequestResult> ClearAsync(IList<int>? lampIds = null, CancellationToken ct = default)
    {
        // lamp_ids 선택 필드 — null이면 JSON 생략 = 전체 해제 (§2.2).
        // 빈 리스트는 스펙에 없는 제3의 와이어 상태("lamp_ids":[])가 되므로 차단 —
        // 전체 해제는 반드시 명시적 null로만 (빈 선택을 전체 해제로 승격시키는 사고 방지).
        if (lampIds is { Count: 0 })
            return InvalidIds(EnumGopCommand.LAMP_CLEAR);

        var body = new LampClearBodyDto { LampIds = lampIds?.ToList() };
        return await _brokerClient.RequestAsync(
            BuildSubject("lamp-clear"), EnumGopCommand.LAMP_CLEAR.ToString(), body, ct: ct).ConfigureAwait(false);
    }

    public async Task<BrokerRequestResult> OffAsync(IList<int> lampIds, CancellationToken ct = default)
    {
        if (lampIds is not { Count: > 0 })
            return InvalidIds(EnumGopCommand.LAMP_OFF);

        var body = new LampOffBodyDto { LampIds = lampIds.ToList() };
        return await _brokerClient.RequestAsync(
            BuildSubject("lamp-off"), EnumGopCommand.LAMP_OFF.ToString(), body, ct: ct).ConfigureAwait(false);
    }

    public async Task<BrokerRequestResult> SetColorAsync(IList<int> lampIds, EnumLampColor color, EnumLightMode mode, CancellationToken ct = default)
    {
        if (lampIds is not { Count: > 0 })
            return InvalidIds(EnumGopCommand.LAMP_COLOR_SET);

        var body = new LampColorSetBodyDto { LampIds = lampIds.ToList(), Color = color, Mode = mode };
        return await _brokerClient.RequestAsync(
            BuildSubject("lamp-color"), EnumGopCommand.LAMP_COLOR_SET.ToString(), body, ct: ct).ConfigureAwait(false);
    }

    public async Task<BrokerRequestResult> SetBuzzerAsync(IList<int> lampIds, EnumBuzzerSound buzzer, CancellationToken ct = default)
    {
        if (lampIds is not { Count: > 0 })
            return InvalidIds(EnumGopCommand.LAMP_BUZZER_SET);

        var body = new LampBuzzerSetBodyDto { LampIds = lampIds.ToList(), Buzzer = buzzer };
        return await _brokerClient.RequestAsync(
            BuildSubject("lamp-buzzer"), EnumGopCommand.LAMP_BUZZER_SET.ToString(), body, ct: ct).ConfigureAwait(false);
    }
    #endregion

    #region - Processes -
    /// <summary>NATS Subject 빌드: "{DomainNats}.{GroupNats}.proxy.{action}"</summary>
    public string BuildSubject(string action)
        => $"{_natsSetup.DomainNats}.{_natsSetup.GroupNats}.proxy.{action}";

    private BrokerRequestResult InvalidIds(EnumGopCommand cmd)
    {
        _log?.Warning($"[LampControl] {cmd} 발행 취소 — lamp_ids가 비어 있음");
        return BrokerRequestResult.Fail(EnumBrokerFailure.Invalid, "대상 경광등이 지정되지 않았습니다.");
    }
    #endregion

    #region - Attributes -
    private readonly IBrokerRequestClient _brokerClient;
    private readonly INatsSetupModel _natsSetup;
    private readonly ILogService? _log;
    #endregion
}
