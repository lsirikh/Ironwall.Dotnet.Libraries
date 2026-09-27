using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;

/****************************************************************************
   Purpose      : 부대 관계도 의미 줌 단계 — 3단계 + 히스테리시스 (FR-19)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 배율 → 의미 줌 단계. 들어가는 값과 나오는 값을 달리 둬서 경계에서 휠을 한 칸씩 오가도 그림이 깜빡이지 않게 한다(SB S3).
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><term>L0 → L1</term><description>배율 ≥ <see cref="EnterL1"/>(0.40)</description></item>
/// <item><term>L1 → L0</term><description>배율 &lt; <see cref="ExitL1"/>(0.36)</description></item>
/// <item><term>L1 → L2</term><description>배율 ≥ <see cref="EnterL2"/>(0.80)</description></item>
/// <item><term>L2 → L1</term><description>배율 &lt; <see cref="ExitL2"/>(0.72)</description></item>
/// </list>
/// <para>한 번에 두 단계를 건널 수 있다(전체 보기 · 키 <c>0</c> · 개인 뷰 복원).</para>
/// <para><b>부동소수 경계</b>: 휠 곱(×1.2 · ÷1.2)이 쌓이면 0.40 이 0.39999999999999997 이 된다 — HUD 는 40% 인데 L0 에 머무는 결함
/// (시나리오 ISSUE-38). 경계에서 <see cref="Tolerance"/> 안쪽은 경계값으로 본다.</para>
/// <para>단계가 바뀔 때만 노드 모양을 갈아 끼운다 — 호출부는 돌려받은 값이 지금 단계와 다를 때만 템플릿을 바꾼다(NFR-03).</para>
/// </remarks>
public static class UnitMapLod
{
    /// <summary>L1 에 들어가는 배율.</summary>
    public const double EnterL1 = 0.40;

    /// <summary>L1 에서 L0 로 나가는 배율(이 값 <b>미만</b>).</summary>
    public const double ExitL1 = 0.36;

    /// <summary>L2 에 들어가는 배율.</summary>
    public const double EnterL2 = 0.80;

    /// <summary>L2 에서 L1 로 나가는 배율(이 값 <b>미만</b>).</summary>
    public const double ExitL2 = 0.72;

    /// <summary>경계 비교의 허용 오차 — 배율 곱의 누적 오차(ulp 수준)를 삼킨다. 사람이 고르는 배율 간격(≥ 0.0001)보다 훨씬 작다.</summary>
    public const double Tolerance = 1e-9;

    /// <summary>지금 단계 <paramref name="current"/> 에서 배율이 <paramref name="scale"/> 이 되면 어느 단계인가.</summary>
    public static UnitMapLevel Resolve(double scale, UnitMapLevel current) => current switch
    {
        UnitMapLevel.L2 => Below(scale, ExitL1) ? UnitMapLevel.L0
                         : Below(scale, ExitL2) ? UnitMapLevel.L1
                         : UnitMapLevel.L2,
        UnitMapLevel.L1 => AtLeast(scale, EnterL2) ? UnitMapLevel.L2
                         : Below(scale, ExitL1) ? UnitMapLevel.L0
                         : UnitMapLevel.L1,
        _ => AtLeast(scale, EnterL2) ? UnitMapLevel.L2
           : AtLeast(scale, EnterL1) ? UnitMapLevel.L1
           : UnitMapLevel.L0,
    };

    /// <summary>지금 단계가 없을 때(첫 그림) — 들어가는 값만으로 판정한다.</summary>
    public static UnitMapLevel ForScale(double scale) => Resolve(scale, UnitMapLevel.L0);

    /// <summary>
    /// 그 단계의 <b>들어가는</b> 배율 — 빈 곳 더블클릭(FR-14) · 검색 이동이 L0 일 때(FR-16) 가는 배율. L0 는 최소 배율.
    /// </summary>
    public static double EntryScale(UnitMapLevel level) => level switch
    {
        UnitMapLevel.L1 => EnterL1,
        UnitMapLevel.L2 => EnterL2,
        _ => GraphViewport.MinScale,
    };

    private static bool AtLeast(double scale, double threshold) => scale >= threshold - Tolerance;

    private static bool Below(double scale, double threshold) => scale < threshold - Tolerance;
}
