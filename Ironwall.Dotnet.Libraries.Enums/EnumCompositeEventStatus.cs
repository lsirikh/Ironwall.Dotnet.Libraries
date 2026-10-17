namespace Ironwall.Dotnet.Libraries.Enums;

public enum EnumCompositeEventStatus
{
    Normal = 0,
    Detecting = 1,
    Faulted = 2,
    FaultedDetecting = 3,
    Connection = 4,
    /// <summary>제어기 무통신(먹통) — 제어기 장애로 연결 센서가 모두 불능인 그룹.
    /// 검은색 표시. 우선순위 최상위(센서 Faulted/Detecting을 덮음). GMap_Controller_Blackout.</summary>
    Blackout = 5,
}
