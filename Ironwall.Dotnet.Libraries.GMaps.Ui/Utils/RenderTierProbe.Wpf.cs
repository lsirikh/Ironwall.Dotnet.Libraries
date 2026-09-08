using System;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/// <summary>
/// <see cref="RenderTierProbe"/> 의 WPF 어댑터(FR-13) — <see cref="RenderCapability.Tier"/> 를 최초 접근 시 1회 읽어 캐시하고
/// <see cref="RenderCapability.TierChanged"/> 를 구독해 <see cref="TierChanged"/> 로 재발행한다(RDP 접속/해제·GPU 드라이버 변경 시 발화).
/// 순수 판정(<c>RenderTierProbe.cs</c>)은 tests/GMaps.Ui.Tests 에 소스링크되므로 WPF 참조는 이 파일에만 둔다.
/// <para>
/// ★ UI 스레드 전제: <see cref="RenderCapability.Tier"/> 는 "현재 스레드"의 렌더 tier 이고 <see cref="RenderCapability.TierChanged"/> 도
/// 그 스레드의 Dispatcher 에서 발화한다. 최초 접근(<see cref="Current"/>)과 <see cref="TierChanged"/> 구독은 반드시 맵 컨트롤을 소유한
/// UI 스레드에서 한다 — 백그라운드 스레드에서 먼저 읽으면 그 스레드의 tier(보통 0)가 캐시되어 영구 강등된다.
/// 정적 수명이므로 앱 수명 구독자(GMapCustomControl.ReevaluateTilt 배선, IMPL-F2)만 구독한다(RotationFeature.EnabledChanged 와 동형).
/// </para>
/// <para>
/// ★ PRD V-04(RDP 세션 Tier 값·프레임 비용)는 실기 항목 — RDP 실측 전까지 틸트 기능은 기본 OFF.
/// 이 프로브는 판정과 로그(<see cref="CurrentLog"/>)만 제공하고 정책(강등 후 툴팁·복귀)은 TiltMath.Decide 가 결정한다.
/// </para>
/// </summary>
public static partial class RenderTierProbe
{
    private static int _cachedTier;
    private static bool _isInitialized;

    /// <summary>tier 변경 통지(인자 = 새 <c>RenderCapability.Tier</c> 원시값). 같은 값이면 발화하지 않는다.
    /// 구독 자체가 초기화를 유발하지 않으므로 UI 스레드에서 <see cref="Current"/> 를 먼저 한 번 읽어 두어야 재발행이 시작된다.</summary>
    public static event Action<int>? TierChanged;

    /// <summary>현재 <c>RenderCapability.Tier</c> 원시값(최초 접근 시 1회 읽기, 이후 <c>TierChanged</c> 로만 갱신). UI 스레드에서 호출.</summary>
    public static int Current
    {
        get
        {
            EnsureInitialized();
            return _cachedTier;
        }
    }

    /// <summary>현재 tier 가 소프트웨어 렌더링(Tier 0)인지 — <c>TiltMath.Decide</c> 의 <c>tier0</c> 입력.</summary>
    public static bool IsSoftware => IsSoftwareTier(Current);

    /// <summary>현재 tier 의 로그 문자열 <c>[Tilt] tier=0x{tier:X} software={bool}</c> — 부팅 1회·변경 시마다 호출부가 기록(VER-04 실기 grep).</summary>
    public static string CurrentLog => FormatLog(Current);

    private static void EnsureInitialized()
    {
        if (_isInitialized) return;
        _cachedTier = RenderCapability.Tier;         // 1회 읽기(현재 스레드 = UI 스레드 전제) — 예외 시 플래그를 세우지 않아 다음 접근이 재시도한다
        RenderCapability.TierChanged += OnRenderCapabilityTierChanged;
        _isInitialized = true;                       // 읽기·구독이 모두 성공한 뒤에만 완료 표시(실패 시 영구 0 캐시·구독 누락 방지)
    }

    private static void OnRenderCapabilityTierChanged(object? sender, EventArgs e)
    {
        int tier = RenderCapability.Tier;
        if (tier == _cachedTier) return;
        _cachedTier = tier;
        TierChanged?.Invoke(tier);                   // 구독자(ReevaluateTilt)가 FormatLog 로 사유 로그 후 φ 재판정
    }
}
