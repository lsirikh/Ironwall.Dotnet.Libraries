using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Enclosure;

/****************************************************************************
   Purpose      : ENCLOSURE_METRICS 수신 서비스 인터페이스 — 이슈 #7-20
   Created By   : Claude Code
   Created On   : 2026-10-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 통제DB 가 주기(기본 10초)로 보내는 <c>ENCLOSURE_METRICS</c> 를 구독해 함체별 최신 계측을 들고 있는다.
/// </summary>
public interface IEnclosureMetricsNatsSyncService
{
    /// <summary>계측 수신. NATS 스레드에서 발화하므로 구독자가 UI 마샬을 책임진다.</summary>
    event System.Action<EnclosureMetricsReading>? MetricsReceived;

    /// <summary>함체의 최신 계측 — 받은 적이 없으면 false.</summary>
    bool TryGetLatest(int enclosureId, out EnclosureMetricsReading reading);

    Task StartService(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
