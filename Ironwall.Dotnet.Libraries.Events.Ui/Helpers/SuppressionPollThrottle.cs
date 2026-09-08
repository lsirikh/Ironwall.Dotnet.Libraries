using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : 억제 활성목록 즉시 폴링 요청의 스로틀 판정(순수 함수).
                  NATS SYNC_EVENT_SUPPRESSION 은 가속 신호일 뿐이고,
                  서버가 단발 창 경계에서 중복 발행하며 bulk-delete 는 N건 버스트다.
                  스로틀이 없으면 원격 REST GET(타임아웃 10s)이 배수로 터진다.
   Created By   : GHLee
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>즉시 폴링 요청에 대한 판정.</summary>
public enum PollDecision
{
    /// <summary>지금 바로 폴링한다.</summary>
    PollNow,
    /// <summary>쿨다운 중 — 마지막 1건을 지연 발화하도록 예약한다(<b>드롭 금지</b>).</summary>
    Defer,
    /// <summary>이미 예약돼 있다 — 아무것도 하지 않는다.</summary>
    AlreadyScheduled,
}

/// <summary>
/// 즉시 폴링 스로틀 — <b>리딩 엣지 + 트레일링 보장</b>.
/// <para>첫 요청은 즉시 통과시키고, 쿨다운 안의 후속 요청은 <b>버리지 않고</b> 1건으로 접어
/// 쿨다운이 끝나는 시점에 발화하도록 예약한다. 마지막 이벤트를 드롭하면 <b>영구 침묵</b>이 된다.</para>
/// <para>순수 함수라 <c>DispatcherTimer</c> 없이 헤드리스로 고정할 수 있다.</para>
/// </summary>
public static class SuppressionPollThrottle
{
    /// <summary>쿨다운(초) — 이 안에 들어온 요청은 트레일링 1건으로 접힌다.</summary>
    public const double CooldownSeconds = 5.0;

    /// <summary>
    /// 판정한다.
    /// </summary>
    /// <param name="now">현재 시각.</param>
    /// <param name="lastPollAt">마지막으로 실제 폴링한 시각(없으면 null).</param>
    /// <param name="hasPendingSchedule">이미 트레일링 발화가 예약돼 있는가.</param>
    /// <param name="cooldownSeconds">쿨다운(기본 <see cref="CooldownSeconds"/>).</param>
    public static PollDecision Decide(
        DateTime now,
        DateTime? lastPollAt,
        bool hasPendingSchedule,
        double cooldownSeconds = CooldownSeconds)
    {
        if (hasPendingSchedule) return PollDecision.AlreadyScheduled;
        if (lastPollAt is null) return PollDecision.PollNow;

        var elapsed = now - lastPollAt.Value;
        // 시계 역행(수동 조정·DST)에도 멈추지 않도록 음수는 즉시 통과로 본다.
        if (elapsed < TimeSpan.Zero) return PollDecision.PollNow;

        return elapsed.TotalSeconds >= cooldownSeconds
            ? PollDecision.PollNow
            : PollDecision.Defer;
    }

    /// <summary>트레일링 발화까지 남은 지연. <see cref="PollDecision.Defer"/> 일 때만 의미가 있다.</summary>
    public static TimeSpan RemainingCooldown(
        DateTime now, DateTime? lastPollAt, double cooldownSeconds = CooldownSeconds)
    {
        if (lastPollAt is null) return TimeSpan.Zero;
        var remain = TimeSpan.FromSeconds(cooldownSeconds) - (now - lastPollAt.Value);
        return remain > TimeSpan.Zero ? remain : TimeSpan.Zero;
    }

    /// <summary>
    /// 폴링 결과가 stale 인가 — 마지막 <b>성공</b> 이후 TTL(폴링주기 × 3)을 넘겼는가.
    /// <para>stale 이어도 목록을 버리지 않는다. 이 배너의 존재 이유가 <b>억제 은폐 방지</b>이므로
    /// 조용한 소멸은 목적을 배반하고, 조용한 유지는 거짓말이다 — <b>병기</b>가 정답이다.</para>
    /// </summary>
    public static bool IsStale(DateTime now, DateTime? lastSuccessAt, double ttlSeconds)
    {
        if (lastSuccessAt is null) return false;   // 아직 한 번도 성공 못 했으면 stale 판정 보류
        var elapsed = now - lastSuccessAt.Value;
        return elapsed > TimeSpan.FromSeconds(ttlSeconds);
    }

    /// <summary>stale 표기용 경과 문구(예: <c>"3분 전"</c>).</summary>
    public static string DescribeAge(DateTime now, DateTime? lastSuccessAt)
    {
        if (lastSuccessAt is null) return "확인 안 됨";
        var elapsed = now - lastSuccessAt.Value;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        if (elapsed.TotalSeconds < 60) return $"{(int)elapsed.TotalSeconds}초 전";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}분 전";
        return $"{(int)elapsed.TotalHours}시간 전";
    }
}
