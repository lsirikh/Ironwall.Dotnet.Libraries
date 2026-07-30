using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;

/// <summary>
/// GIS→매니저 제어 REQ 공통 실행기 (GIS.md v1.5.2 §6.4).
/// <para>봉투 생성(m_type=REQ, from=GIS) → NATS request/reply → RSP(success/req_id/message) 파싱까지 담당.</para>
/// <para>기본 타임아웃 5초. 실패는 예외가 아니라 BrokerRequestResult로 반환한다.</para>
/// </summary>
public interface IBrokerRequestClient
{
    /// <summary>
    /// body를 REQ 봉투에 실어 subject로 발행하고 RSP를 기다린다.
    /// </summary>
    /// <param name="subject">NATS subject (예: sensorway.unit001.proxy.lamp-off)</param>
    /// <param name="command">cmd 문자열 (EnumGopCommand.ToString() 권장)</param>
    /// <param name="body">body DTO</param>
    /// <param name="timeout">RSP 대기 시간 (기본 5초)</param>
    Task<BrokerRequestResult> RequestAsync<TBody>(
        string subject,
        string command,
        TBody body,
        TimeSpan? timeout = null,
        CancellationToken ct = default) where TBody : class;
}
