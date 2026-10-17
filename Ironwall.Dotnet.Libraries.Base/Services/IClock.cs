using System;

namespace Ironwall.Dotnet.Libraries.Base.Services;
/****************************************************************************
   Purpose      : 시간 추상화 (테스트 가능성 — 규칙 I-02)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 시계 추상화. 시간 의존 로직(추적 TTL 스윕·observed_at 역전 방지·재생 드리프트 보정)은
/// <c>DateTime.Now</c>/<c>UtcNow</c>를 직접 호출하지 않고 본 인터페이스를 주입받아 테스트 가능하게 한다.
/// DI: <c>builder.RegisterInstance(...).As&lt;IClock&gt;()</c> 또는 <c>AddSingleton&lt;IClock, SystemClock&gt;()</c>.
/// </summary>
public interface IClock
{
    /// <summary>로컬 현재 시각.</summary>
    DateTime Now { get; }

    /// <summary>UTC 현재 시각 (추적 observed_at 비교의 기준).</summary>
    DateTime UtcNow { get; }
}
