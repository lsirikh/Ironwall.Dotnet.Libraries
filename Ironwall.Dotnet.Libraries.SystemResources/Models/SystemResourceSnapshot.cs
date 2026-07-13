using System;

namespace Ironwall.Dotnet.Libraries.SystemResources.Models;

/****************************************************************************
   Purpose      : 시스템 리소스 사용률 스냅샷 (불변)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 한 시점의 CPU/GPU/RAM 사용률 스냅샷. <b>불변 record class</b>이므로 참조 스왑이 원자적이라
/// lock 없이 안전하게 읽을 수 있다(구조체가 아님 — torn read 방지). 소비자는 이벤트 인자로 받은
/// 스냅샷을 사용하고 <c>Current</c>를 재조회하지 않는다(틱 간 순서 일관성).
/// </summary>
public sealed record SystemResourceSnapshot
{
    /// <summary>CPU 사용률 % (0..100, clamp됨).</summary>
    public double CpuPercent { get; init; }

    /// <summary>RAM 사용률 % (0..100).</summary>
    public double RamPercent { get; init; }

    /// <summary>GPU 사용률 % (0..100, busiest 엔진).</summary>
    public double GpuPercent { get; init; }

    /// <summary>CPU 카운터 사용 가능 + 이번 샘플 유효 여부.</summary>
    public bool CpuAvailable { get; init; }

    /// <summary>RAM 조회 가능 여부(사실상 항상 true — 모니터 전체 가용성의 canary).</summary>
    public bool RamAvailable { get; init; }

    /// <summary>GPU 카운터 사용 가능 여부(없으면 GPU 칩 숨김).</summary>
    public bool GpuAvailable { get; init; }

    /// <summary>CPU 심각도 레벨(히스테리시스).</summary>
    public ResourceLevel CpuLevel { get; init; }

    /// <summary>RAM 심각도 레벨.</summary>
    public ResourceLevel RamLevel { get; init; }

    /// <summary>GPU 심각도 레벨.</summary>
    public ResourceLevel GpuLevel { get; init; }

    /// <summary>물리 메모리 총량(바이트) — 툴팁 절대값용.</summary>
    public ulong RamTotalBytes { get; init; }

    /// <summary>사용 중 물리 메모리(바이트) = Total − Avail.</summary>
    public ulong RamUsedBytes { get; init; }

    /// <summary>스냅샷 생성 UTC 시각(IClock).</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>
    /// 초기/장애 스냅샷 — 모든 지표 미가용(RamAvailable=false = 모니터 전체 불가 → UI "N/A").
    /// 워밍업 전 또는 PDH 전체 실패 시 사용.
    /// </summary>
    public static SystemResourceSnapshot Empty { get; } = new();
}
