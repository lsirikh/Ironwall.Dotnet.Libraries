namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 개폐 심볼(통문 Gate · 함체 Enclosure)의 <b>형태 축</b> 상태 — 이벤트 색 축(CompositeStatus)과 독립이며 비영속이다.
    /// 기존 <see cref="EnumDoorStatus"/>(CLOSED/OPEN)는 서버 함체 계약(door_status)이라 건드리지 않고,
    /// 기동 직후 "아직 모름"을 표현하기 위해 Unknown 을 가진 별도 enum 을 둔다.
    /// (PRD pidsgroup-3d-fence-gate FR-09/FR-12)
    /// </summary>
    public enum EnumDoorState
    {
        /// <summary>서버 상태 조회 전/미제공 — 표시는 닫힘 형태 + 라벨 '?'</summary>
        Unknown = 0,
        Closed = 1,
        Open = 2,
    }
}
