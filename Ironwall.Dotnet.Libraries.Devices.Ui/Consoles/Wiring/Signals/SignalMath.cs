using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;

/// <summary>통신 신호등 상태(fence-wiring-editor FR-14) — 켜진 <b>자리</b>로도 읽힌다(왼 = 응답 없음 · 가운데 = 느림 · 오른 = 정상 · 모두 꺼짐 = 모름).</summary>
public enum SignalLevel
{
    /// <summary>모름 — 표본 · 보고가 없다(신호등 모두 꺼짐).</summary>
    Unknown = 0,
    /// <summary>응답 없음(왼쪽 등).</summary>
    Down = 1,
    /// <summary>느림 · 일부 손실(가운데 등).</summary>
    Slow = 2,
    /// <summary>정상(오른쪽 등).</summary>
    Ok = 3,
}

/// <summary>ping 한 번의 결과.</summary>
/// <param name="Success">응답이 왔는가.</param>
/// <param name="RoundTripMs">왕복 시간(ms) — 실패면 무시.</param>
public readonly record struct PingSample(bool Success, double RoundTripMs);

/// <summary>
/// 신호등 판정 — <b>순수 함수</b>(fence-wiring-editor FR-14 · NFR-01).
/// </summary>
/// <remarks>
/// <para><b>제어기</b>(GIS 가 5초마다 ICMP ping) — 최근 <see cref="WINDOW"/> 회: 연속 <see cref="DOWN_STREAK"/> 회 실패 = 응답 없음 /
/// 손실 0 · 평균 &lt; <see cref="SLOW_MS"/> ms = 정상 / 그 밖(손실 1~2 · 평균 ≥ 200ms · 연속이 아닌 손실 3회 이상) = 느림. 표본이 없으면 모름.</para>
/// <para><b>센서</b> — 스마트복합센서2 의 IP 는 제어기 뒤 내부망이라 ping 하지 않는다. 매니저가 보고한 <c>NETWORK_INTERFACE</c> 부품
/// health 로: OK = 정상 · DEGRADED = 느림 · FAULT = 응답 없음 · 그 밖 · 없음 = 모름.</para>
/// </remarks>
public static class SignalMath
{
    public const int WINDOW = 5;
    public const double SLOW_MS = 200;
    public const int DOWN_STREAK = 3;

    /// <summary>센서 링크 상태를 싣는 부품 종류(서버 요청 Q-4).</summary>
    public const string NETWORK_COMPONENT = "NETWORK_INTERFACE";

    /// <summary>ping 표본(오래된 것 → 최근) → 상태.</summary>
    public static SignalLevel Classify(IReadOnlyList<PingSample>? samples)
    {
        if (samples is null || samples.Count == 0) return SignalLevel.Unknown;
        if (samples.Count >= DOWN_STREAK && samples.Skip(samples.Count - DOWN_STREAK).All(s => !s.Success)) return SignalLevel.Down;

        var window = samples.Skip(Math.Max(0, samples.Count - WINDOW)).ToList();
        var ok = window.Where(s => s.Success).ToList();
        var loss = window.Count - ok.Count;
        if (ok.Count == 0) return SignalLevel.Slow;                   // 실패뿐이지만 아직 연속 3회가 아니다
        var average = ok.Average(s => Math.Max(0, s.RoundTripMs));
        return loss == 0 && average < SLOW_MS ? SignalLevel.Ok : SignalLevel.Slow;
    }

    /// <summary>부품 health 원값 → 상태(대소문자 무시).</summary>
    public static SignalLevel FromHealth(string? health) => health?.Trim().ToUpperInvariant() switch
    {
        "OK" => SignalLevel.Ok,
        "DEGRADED" => SignalLevel.Slow,
        "FAULT" => SignalLevel.Down,
        _ => SignalLevel.Unknown,
    };

    /// <summary>켜지는 등의 자리 — 0 = 왼(응답 없음) · 1 = 가운데(느림) · 2 = 오른(정상) · −1 = 모두 꺼짐(모름).</summary>
    public static int LitIndex(SignalLevel level) => level switch
    {
        SignalLevel.Down => 0,
        SignalLevel.Slow => 1,
        SignalLevel.Ok => 2,
        _ => -1,
    };

    /// <summary>사람이 읽는 말.</summary>
    public static string Text(SignalLevel level) => level switch
    {
        SignalLevel.Down => "응답 없음",
        SignalLevel.Slow => "느림 · 일부 손실",
        SignalLevel.Ok => "정상",
        _ => "모름",
    };

    /// <summary>최근 표본 요약 — "평균 12ms · 손실 0/5". 표본이 없으면 빈 글자.</summary>
    public static string Summary(IReadOnlyList<PingSample>? samples)
    {
        if (samples is null || samples.Count == 0) return string.Empty;
        var window = samples.Skip(Math.Max(0, samples.Count - WINDOW)).ToList();
        var ok = window.Where(s => s.Success).ToList();
        var average = ok.Count == 0 ? (double?)null : ok.Average(s => s.RoundTripMs);
        return (average is { } a ? $"평균 {a:0}ms" : "응답 없음") + $" · 손실 {window.Count - ok.Count}/{window.Count}";
    }
}
