using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Broadcast;

/****************************************************************************
   Purpose      : BROADCAST_STATUS 수신 서비스 인터페이스 — PRD symbol-detail-and-door-control FR-24
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// BroadcastingManager 가 발행하는 <c>BROADCAST_STATUS</c> 를 구독해 "이 스피커가 지금 송출 중인지"를 알려준다.
/// <para>음원·TTS·마이크가 <b>같은 메시지</b>를 쓴다 — 마이크 때문에 새 상태 메시지를 만들지 않는다(FR-24).</para>
/// </summary>
public interface IBroadcastStatusNatsSyncService
{
    /// <summary>(스피커 장비 Id, 송출 중 여부). NATS 스레드에서 발화하므로 구독자가 UI 마샬을 책임진다.</summary>
    event System.Action<int, bool>? BroadcastStateChanged;

    Task StartService(CancellationToken token = default);
    Task StopAsync(CancellationToken token = default);
}
