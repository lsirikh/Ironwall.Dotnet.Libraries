using Xunit;

namespace Ironwall.Dotnet.Libraries.Tests.LiveApi;

/// <summary>
/// 라이브 API <b>읽기</b> 시험 — 전용 계정 파일이 없거나 서버가 닿지 않으면 <b>건너뜀</b>(실패 아님).
/// <para>xunit 2 는 발견 단계에서 특성 인스턴스의 <see cref="FactAttribute.Skip"/> 을 읽는다 —
/// 생성자에서 환경을 보고 채우면 실행 때마다 판정이 달라지는 동적 건너뜀이 된다(PLAN.md 결정 D1).</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class LiveApiFactAttribute : FactAttribute
{
    public LiveApiFactAttribute()
    {
        var reason = LiveApiTarget.ReadSkipReason;
        if (reason is not null) Skip = reason;
    }
}

/// <summary>
/// 라이브 API <b>쓰기</b> 시험(생성 · 수정 · 삭제) — 읽기 조건에 더해 주소가 loopback · 사설망이 아니면
/// 네트워크에 닿기 <b>전에</b> 건너뛴다(원격 운영 서버 보호).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class LiveApiWriteFactAttribute : FactAttribute
{
    public LiveApiWriteFactAttribute()
    {
        var reason = LiveApiTarget.WriteSkipReason;
        if (reason is not null) Skip = reason;
    }
}
