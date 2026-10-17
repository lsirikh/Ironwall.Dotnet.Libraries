namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 번호 대역을 가르는 센서 갈래(fence-wiring-editor §1-A) — 현장 번호는 이 갈래별 대역으로 나뉜다.
/// </summary>
public enum FenceSensorCategory
{
    /// <summary>모르는 종류 — 대역이 없다(번호를 건드리지 않는다).</summary>
    Other = 0,
    /// <summary>스마트센서 · 스마트 복합센서(II 포함).</summary>
    Smart = 1,
    /// <summary>펜스센서(철망 가운데).</summary>
    Fence = 2,
    /// <summary>복합센서(기둥 위 볼 마운트).</summary>
    Multi = 3,
    /// <summary>지진동센서(땅속).</summary>
    Underground = 4,
}
