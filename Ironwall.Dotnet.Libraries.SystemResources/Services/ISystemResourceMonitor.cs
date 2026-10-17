using System;
using Ironwall.Dotnet.Libraries.SystemResources.Models;

namespace Ironwall.Dotnet.Libraries.SystemResources.Services;

/****************************************************************************
   Purpose      : 시스템 리소스 모니터 (WPF 비의존 — 소비자가 틱 구동)
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// CPU/GPU/RAM 사용률 모니터. <b>WPF 비의존</b>(NFR-06): 타이머를 소유하지 않고 소비자가
/// 주기적으로 <see cref="Sample"/>을 호출한다(예: MapViewModel의 UI-스레드 DispatcherTimer).
/// 이 설계로 라이브러리는 프레임워크 독립·완전 테스트가능해지고, UI 스레드 단일 구동 시
/// 락/크로스스레드 마샬/재진입이 원천 제거된다(PRD FR-05/09 의도를 더 잘 충족).
/// <para><b>IService를 구현하지 않는다</b>(Bootstrapper 이중 생명주기 회피 — 오직 소비자가 생명주기 소유).</para>
/// </summary>
public interface ISystemResourceMonitor : IDisposable
{
    /// <summary>마지막 스냅샷(원자적 참조 읽기, lock 불요). 첫 <see cref="Sample"/> 전에는 <see cref="SystemResourceSnapshot.Empty"/>.</summary>
    SystemResourceSnapshot Current { get; }

    /// <summary>새 스냅샷 발행 시 발화. 핸들러는 <b>이벤트 인자</b> 스냅샷을 사용하고 <see cref="Current"/>를 재조회하지 않는다.</summary>
    event EventHandler<SystemResourceSnapshot>? SnapshotUpdated;

    /// <summary>PDH 쿼리 open + 프라이밍(워밍업 시작). 멱등. 소비자가 활성화 시 1회 호출(핸들은 <see cref="Dispose"/>까지 유지).</summary>
    void Start();

    /// <summary>PDH 쿼리 close(핸들 해제). 멱등. 소비자는 탭 전환 시 호출하지 않는다(타이머만 멈춤 → 워밍업 유지).</summary>
    void Stop();

    /// <summary>1회 폴링: 원시값 취득 → clamp/레벨 매핑 → <see cref="Current"/> 갱신 + 이벤트 발화 → 스냅샷 반환. 예외를 던지지 않고 실패 시 Empty 발행(fail-safe).</summary>
    SystemResourceSnapshot Sample();
}
