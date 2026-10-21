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

    /// <summary>함체 환경 · 부품 판정 경고가 걸리거나 풀렸다(함체 id). NATS 스레드에서 발화한다(이슈 #7 Q-4a).</summary>
    event System.Action<int>? AlertsChanged;

    /// <summary>함체에 걸린 경고 요약 — 없으면 null.</summary>
    EnclosureAlertSummary? GetAlert(int enclosureId);

    /// <summary>
    /// <c>GET /api/enclosure-metrics</c> 1회로 함체별 최신 계측을 다시 채운다(8.0.15 G24) — 기동 때 자동으로 한 번 부르고,
    /// NATS 재연결 뒤처럼 빈칸이 생겼을 때 다시 부른다. 404 · 실패는 조용히 0. 반환=새로 채운 함체 수.
    /// </summary>
    Task<int> RefreshFromServerAsync(CancellationToken token = default);

    Task StartService(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
