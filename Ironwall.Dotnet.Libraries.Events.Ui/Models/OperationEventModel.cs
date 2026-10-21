using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;

/// <summary>
/// 운영 이벤트 한 건(<c>GET /api/events/operations</c>) — 함체 · 통문 개폐와 함체 환경 임계 경보.
/// </summary>
/// <remarks>
/// <para><b>조회 전용</b>이다 — 서버가 매니저 보고로 스스로 적재한다(클라가 POST 하면 같은 사건이 2건이 된다).</para>
/// <para>사유 · 심각도는 서버 어휘가 늘어날 수 있어 문자열 그대로 둔다(모르는 값도 지우지 않는다).</para>
/// </remarks>
public class OperationEventModel : ExEventModel
{
    public OperationEventModel()
    {
        MessageType = EnumEventType.Operation;
    }

    /// <summary>발생 사유 원문(<c>ENCLOSURE_DOOR_OPEN</c> · <c>GATE_CLOSED</c> · <c>ENCLOSURE_TEMP_HIGH</c> …).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>심각도 원문(<c>INFO</c> · <c>WARNING</c> · <c>CRITICAL</c>). 없으면 null.</summary>
    public string? Severity { get; set; }

    /// <summary>
    /// 장비 카테고리 원문(<c>enclosure</c> · <c>camera</c> …, 서버 <c>device.category_device</c>). 사유 글자의 장비 종류를 가른다(G19).
    /// 6.3 처럼 없으면 null.
    /// </summary>
    public string? CategoryDevice { get; set; }

    /// <summary>상세(JSONB)를 한 줄로 편 것 — 사유마다 키가 달라 강타입으로 두지 않는다. 없으면 null.</summary>
    public string? DetailText { get; set; }
}
