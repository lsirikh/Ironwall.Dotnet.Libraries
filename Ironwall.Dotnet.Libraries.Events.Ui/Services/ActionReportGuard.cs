using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : IActionReportGuard 구현 — in-flight 집합.
                  싱글톤으로 등록되어 자동/자동복구/배치/수동 조치보고 경로가 공유한다.
                  (N-07 R1) 타입까지 보는 IKeyedActionReportGuard 를 함께 구현한다 —
                  탐지 3번과 장애 3번이 한 자물쇠를 나눠 쓰던 것을 가른다.
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class ActionReportGuard : IActionReportGuard, IKeyedActionReportGuard
{
    /// <summary>타입 없는 옛 호출이 잠그는 자리 — 그 Id 의 <b>모든</b> 종류를 막는다.</summary>
    private const string AnyKind = "*";

    private readonly object _gate = new();
    private readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);

    #region - IActionReportGuard (기존 계약 — 넓히지 않는다) -
    /// <summary>Id 만으로 점유한다. 그 Id 의 <b>모든 종류</b>를 막는다(자동 · 배치 경로의 지금 동작 보존).</summary>
    public bool TryEnter(int eventId)
    {
        if (eventId <= 0) return true;              // 가드 대상 아님(잘못된 Id) — 진행 허용

        lock (_gate)
        {
            // 누군가(타입 있든 없든) 이 Id 를 쥐고 있으면 들어가지 못한다.
            if (AnyHeld(eventId)) return false;
            return _inFlight.Add(Key(AnyKind, eventId));
        }
    }

    public void Exit(int eventId)
    {
        if (eventId <= 0) return;
        lock (_gate) _inFlight.Remove(Key(AnyKind, eventId));
    }
    #endregion

    #region - IKeyedActionReportGuard (N-07 R1) -
    /// <summary>종류 + Id 로 점유한다. 같은 Id 라도 종류가 다르면 서로를 막지 않는다.</summary>
    public bool TryEnter(string kind, int eventId)
    {
        if (eventId <= 0) return true;
        if (string.IsNullOrEmpty(kind) || kind == AnyKind) return TryEnter(eventId);

        lock (_gate)
        {
            // 타입 없는 옛 경로(자동 · 자동복구 · 배치)가 이 Id 를 쥐고 있으면 종류와 무관하게 막힌다.
            if (_inFlight.Contains(Key(AnyKind, eventId))) return false;
            return _inFlight.Add(Key(kind, eventId));
        }
    }

    public void Exit(string kind, int eventId)
    {
        if (eventId <= 0) return;
        if (string.IsNullOrEmpty(kind) || kind == AnyKind) { Exit(eventId); return; }
        lock (_gate) _inFlight.Remove(Key(kind, eventId));
    }
    #endregion

    /// <summary>이 Id 를 어떤 종류로든 누가 쥐고 있는가 — 타입 없는 점유가 전부를 막기 위해 본다.</summary>
    private bool AnyHeld(int eventId)
    {
        var suffix = ":" + eventId.ToString();
        foreach (var key in _inFlight)
            if (key.EndsWith(suffix, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string Key(string kind, int eventId) => kind + ":" + eventId;
}
