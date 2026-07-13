using System;

namespace Ironwall.Dotnet.Libraries.SystemResources.Services;

/****************************************************************************
   Purpose      : 원시 리소스 취득 seam (PDH/kernel32 은닉 — 테스트 목킹 지점)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// OS로부터 원시 CPU/GPU/RAM 값을 취득하는 seam. 실제 구현(<see cref="PdhResourceProbe"/>)은
/// PDH/kernel32 네이티브를 은닉하고, 테스트는 Fake로 대체하여 <see cref="SystemResourceMonitor"/>의
/// 순수 로직(clamp·히스테리시스·워밍업·fail-safe)을 검증한다.
/// </summary>
public interface IResourceProbe : IDisposable
{
    /// <summary>PDH 쿼리 open + 카운터 추가 + 프라이밍 collect(rate 카운터 2-collect 워밍업). 멱등.</summary>
    void Open();

    /// <summary>PDH 쿼리 close(핸들 해제). 멱등.</summary>
    void Close();

    /// <summary>1회 샘플: collect 후 CPU/GPU/RAM 원시값을 읽어 반환. 재앙적 실패 시 예외.</summary>
    ProbeReading Sample();
}

/// <summary>
/// 프로브 1회 읽기 결과. 각 <c>*Ok</c>는 "카운터 가용 + 이번 샘플 값 유효"(rate 워밍업/무효 데이터 구분)를 의미.
/// </summary>
/// <param name="Cpu">CPU 사용률 %(미clamp).</param>
/// <param name="CpuOk">CPU 값 유효 여부.</param>
/// <param name="Gpu">GPU 사용률 %(busiest 엔진).</param>
/// <param name="GpuOk">GPU 값 유효 여부.</param>
/// <param name="GpuAdapterCount">감지된 GPU 어댑터 수(진단).</param>
/// <param name="Ram">RAM 사용률 %(dwMemoryLoad).</param>
/// <param name="RamOk">RAM 조회 성공 여부.</param>
/// <param name="RamTotalBytes">물리 메모리 총량.</param>
/// <param name="RamUsedBytes">사용 중 물리 메모리.</param>
public readonly record struct ProbeReading(
    double Cpu, bool CpuOk,
    double Gpu, bool GpuOk, int GpuAdapterCount,
    double Ram, bool RamOk, ulong RamTotalBytes, ulong RamUsedBytes);
