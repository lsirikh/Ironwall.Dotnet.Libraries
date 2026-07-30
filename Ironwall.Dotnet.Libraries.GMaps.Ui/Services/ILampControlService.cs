using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 경광등 제어 NATS 발행 서비스 인터페이스 (GIS.md v1.5.2 §2.2~2.5 — 4종 전부 REQ+RSP 확인).
/// <para>Subject: "{DomainNats}.{GroupNats}.proxy.lamp-clear|lamp-off|lamp-color|lamp-buzzer" (수신=PidsProxy)</para>
/// <para>현재 경광등 제어 UI(심볼 메뉴)는 없다 — UI가 생기면 이 인터페이스에 배선한다(PRD FR-07, UI는 Out of Scope).</para>
/// </summary>
public interface ILampControlService
{
    /// <summary>LAMP_CLEAR — 이벤트 연동 동작 해제. lampIds가 null이면 body에서 lamp_ids가 생략되어 전체 해제.</summary>
    Task<BrokerRequestResult> ClearAsync(IList<int>? lampIds = null, CancellationToken ct = default);

    /// <summary>LAMP_OFF — 장비 비활성화(DB status=DEACTIVATED). lampIds 필수.</summary>
    Task<BrokerRequestResult> OffAsync(IList<int> lampIds, CancellationToken ct = default);

    /// <summary>LAMP_COLOR_SET — 색상 직접 설정(LAMP_CLEAR 전까지 유지).</summary>
    Task<BrokerRequestResult> SetColorAsync(IList<int> lampIds, EnumLampColor color, EnumLightMode mode, CancellationToken ct = default);

    /// <summary>LAMP_BUZZER_SET — 부저 직접 설정(LAMP_CLEAR 전까지 유지).</summary>
    Task<BrokerRequestResult> SetBuzzerAsync(IList<int> lampIds, EnumBuzzerSound buzzer, CancellationToken ct = default);
}
