using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// PIDS 그룹(펜스 라인) 3D 철망의 간격 의미(사용자 지적 2026-09-07: "기둥 간격일 수도, 펜스 센서 장착 간격일 수도").
    /// DB <c>PidsGroupSymbols.FenceMode TINYINT</c> 에 정수로 저장된다.
    /// </summary>
    public enum EnumFenceMode
    {
        /// <summary>구조 기둥 간격 — 기둥 n=max(1,round(L/s)) 균등 분배, 꺾임점 코너 기둥</summary>
        [Description("기둥 간격")]
        Posts = 0,
        /// <summary>펜스 센서 장착 간격 — 철망 위 감지 노드 floor(L/s) 정확 간격, 노드 k ↔ 그룹 센서 순번 k</summary>
        [Description("센서 장착")]
        SensorMount = 1,
    }
}
