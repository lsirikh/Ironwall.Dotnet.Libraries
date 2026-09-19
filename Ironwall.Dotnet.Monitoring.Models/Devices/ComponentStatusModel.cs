namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 부품 한 개의 관측 상태(<c>device_status.components.&lt;key&gt;</c>).
/// </summary>
public class ComponentStatusModel
{
    /// <summary>
    /// 관측 시각 — 서버가 준 aware ISO-8601 <b>문자열 그대로</b>(마이크로초 6자리).
    /// <c>DateTime</c> 으로 바꾸면 100ns 틱 반올림·표시 포맷에서 자릿수가 달라진다 — 표시단이 필요할 때 해석한다.
    /// </summary>
    public string? ObservedAt { get; set; }

    /// <summary>동작 상태 — 부품 유형별 어휘(<c>OPEN · CLOSED · RUNNING · ON · OFF …</c>, 카탈로그 <c>component_state</c>).</summary>
    public string? State { get; set; }

    /// <summary>건강 — <c>OK · DEGRADED · FAULT · UNKNOWN</c>(서버 엄격 어휘). 동작 상태와 다른 축이다.</summary>
    public string? Health { get; set; }

    /// <summary>고장 사유 — 카탈로그 <c>component_fault</c> 어휘. 건강이 OK 면 비어 있다.</summary>
    public string? FaultReason { get; set; }
}
