using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : φ_layout 정착기 — map-tilt-25d PRD G8/FR-04 커밋 규칙의 순수 상태기계.
                  각도 조작은 첫 변경에서 φ_layout=φmax 로 선확장(오버스캔 캐스케이드 1회),
                  마지막 변경 후 FenceDefaults.SliderCommitDelayMs(150 ms) 뒤 실제 φ 로 정착(커밋 1회).
                  게이트 ON/OFF·플래그·앵커·Tier 전이는 즉시 정착. 시계는 nowMs 인자로 주입(타이머 무의존).
   Note         : WPF 무의존(tests/GMaps.Ui.Tests 소스 링크). DispatcherTimer 배선은 호출자(GMapCustomControl)가
                  Tick(nowMs) 를 주기 호출하거나 마지막 변경 +150 ms 에 1회 호출한다.
                  CommitCount/LayoutChangeCount 는 NFR-01(조작당 Height 커밋 1회) 계측용이다.
   Created By   : Claude
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 상태: Idle(φ_layout==φ) ↔ Pending(φ_layout==φmax, 정착 대기).
/// <list type="bullet">
/// <item><see cref="Begin"/>: 조작 시작 — φ_layout=φmax 선확장, Pending.</item>
/// <item><see cref="Change"/>: 조작 중 — 목표 φ 갱신 + 디바운스 재시작(Pending 아니면 Begin 과 동일).</item>
/// <item><see cref="Tick"/>: 마지막 변경 후 <see cref="CommitDelayMs"/> 경과 시 φ_layout=φ 정착(커밋 1회).</item>
/// <item><see cref="GateTransition"/>: 즉시 정착(대기 취소).</item>
/// </list>
/// </summary>
public sealed class TiltLayoutSettler
{
    private long _lastChangeMs;

    /// <param name="maxAngleDeg">선확장 각(φmax, PRD G1 — MapTiltModel.MaxAngleDeg).</param>
    /// <param name="commitDelayMs">디바운스(ms). 기본 <see cref="FenceDefaults.SliderCommitDelayMs"/>(150).</param>
    public TiltLayoutSettler(double maxAngleDeg, int commitDelayMs = FenceDefaults.SliderCommitDelayMs)
    {
        if (double.IsNaN(maxAngleDeg) || double.IsInfinity(maxAngleDeg) || maxAngleDeg < 0)
            throw new ArgumentOutOfRangeException(nameof(maxAngleDeg), maxAngleDeg, "maxAngleDeg must be finite and >= 0");
        if (commitDelayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(commitDelayMs), commitDelayMs, "commitDelayMs must be >= 0");
        MaxAngleDeg = maxAngleDeg;
        CommitDelayMs = commitDelayMs;
    }

    public double MaxAngleDeg { get; }
    public int CommitDelayMs { get; }

    /// <summary>실제(렌더) φ — 마지막 목표값(0~MaxAngleDeg 클램프).</summary>
    public double PhiDeg { get; private set; }

    /// <summary>레이아웃 φ — 오버스캔 Height/Margin 산출에 쓰는 값. Pending 동안 φmax.</summary>
    public double PhiLayoutDeg { get; private set; }

    /// <summary>정착 대기 중(선확장 상태).</summary>
    public bool IsPending { get; private set; }

    /// <summary>커밋(정착·게이트 전이) 횟수 — 조작당 1이어야 한다(NFR-01).</summary>
    public int CommitCount { get; private set; }

    /// <summary>φ_layout 실제 변경 횟수(= Height 재레이아웃 횟수, 선확장 포함) — 조작당 ≤2.</summary>
    public int LayoutChangeCount { get; private set; }

    /// <summary>커밋 통지(정착된 φ_layout). Pending 중 Change 로는 발화하지 않는다.</summary>
    public event Action<double>? Committed;

    /// <summary>조작 시작 — φ 목표 기록 + φ_layout=φmax 선확장. 반환: φ_layout 이 바뀌었는가(Height 재계산 필요).</summary>
    public bool Begin(double userAngleDeg, long nowMs)
    {
        PhiDeg = Clamp(userAngleDeg);
        IsPending = true;
        _lastChangeMs = nowMs;
        return SetLayout(MaxAngleDeg);
    }

    /// <summary>조작 중 변경 — 목표 φ 갱신, 디바운스 재시작. Pending 이 아니면 <see cref="Begin"/> 과 동일. 반환: φ_layout 변경 여부.</summary>
    public bool Change(double angleDeg, long nowMs)
    {
        if (!IsPending) return Begin(angleDeg, nowMs);
        PhiDeg = Clamp(angleDeg);
        _lastChangeMs = nowMs;
        return false;
    }

    /// <summary>시계 틱 — 마지막 변경 후 <see cref="CommitDelayMs"/> 이상 경과했으면 정착. 반환: 이번 틱에 커밋했는가.</summary>
    public bool Tick(long nowMs)
    {
        if (!IsPending) return false;
        if (nowMs - _lastChangeMs < CommitDelayMs) return false;
        Commit();
        return true;
    }

    /// <summary>게이트/플래그/앵커/Tier 전이 — 대기 취소 후 즉시 φ_layout=φ. 반환: φ_layout 변경 여부.</summary>
    public bool GateTransition(double phiDeg)
    {
        PhiDeg = Clamp(phiDeg);
        return Commit();
    }

    /// <summary>대기 중이면 지금 정착(언로드·F11 등). 반환: 커밋했는가.</summary>
    public bool Flush()
    {
        if (!IsPending) return false;
        Commit();
        return true;
    }

    private bool Commit()
    {
        IsPending = false;
        bool changed = SetLayout(PhiDeg);
        CommitCount++;
        Committed?.Invoke(PhiLayoutDeg);
        return changed;
    }

    private bool SetLayout(double value)
    {
        if (Math.Abs(PhiLayoutDeg - value) < TiltOverscanMath.AngleEpsilon) return false;
        PhiLayoutDeg = value;
        LayoutChangeCount++;
        return true;
    }

    private double Clamp(double angleDeg)
    {
        if (double.IsNaN(angleDeg) || double.IsInfinity(angleDeg)) return 0.0;
        return Math.Clamp(angleDeg, 0.0, MaxAngleDeg);
    }
}
