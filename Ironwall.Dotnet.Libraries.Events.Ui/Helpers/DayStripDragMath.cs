using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : 요일 스트립 드래그 범위 페인팅의 순수 판정.
                  UI 에서 분리해 헤드리스로 고정한다(드래그 제스처 자체는
                  .NET 8 WPF 에 UIA 드래그 패턴이 없어 자동화로 단언할 수 없다).
   Created By   : GHLee
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>요일 스트립 드래그 판정(부작용 없음 — 단위 테스트 대상).</summary>
public static class DayStripDragMath
{
    /// <summary>
    /// 데드존(DIU). 레포 표준 8.0 — <c>CameraPopupHubMath.IsDrag</c> 와 같은 값·같은 수식이다.
    /// <para>⚠ 그 정본은 <c>GMaps.Ui</c> 에 있고 <c>Events.Ui</c> 가 참조할 수 없다
    /// (공용 <c>Utils</c> 이관은 <c>GMaps.Ui.Tests</c> 가 net8.0 → net8.0-windows 참조 불가(NU1201)라 별건).
    /// 새 상수를 만든 것이 아니라 <b>같은 값을 복제</b>했음을 명시한다.</para>
    /// </summary>
    public const double DragThresholdDiu = 8.0;

    /// <summary>
    /// 드래그로 볼 것인가. <b>경계 배타</b> — 이동량이 정확히 8.0이면 <b>드래그가 아니다</b>
    /// (정본 <c>CameraPopupHubMathTests</c> 가 이 경계를 고정하고 있다).
    /// </summary>
    public static bool IsDrag(double dx, double dy, double threshold = DragThresholdDiu)
        => (dx * dx + dy * dy) > (threshold * threshold);

    /// <summary>
    /// 스트립 안의 x 좌표 → 셀 인덱스(0=월 … 6=일).
    /// <para>가상화가 없고 7칸이 균등하므로 좌표 산술이 안전하다.
    /// 범위를 벗어나면 <b>양 끝으로 clamp</b> 한다 — 스트립 밖으로 끌어도 선택이 끊기지 않아야 한다.</para>
    /// </summary>
    /// <param name="x">스트립 내부 기준 x(패딩 제외한 콘텐츠 좌표).</param>
    /// <param name="contentWidth">셀들이 차지하는 전체 폭.</param>
    /// <param name="cellCount">셀 개수(기본 7).</param>
    public static int IndexFromX(double x, double contentWidth, int cellCount = 7)
    {
        if (cellCount <= 0) return 0;
        if (contentWidth <= 0) return 0;
        var slot = contentWidth / cellCount;
        var i = (int)Math.Floor(x / slot);
        return i < 0 ? 0 : i >= cellCount ? cellCount - 1 : i;
    }

    /// <summary>
    /// 시작~현재 구간에 의도를 적용한 새 마스크.
    /// <para><paramref name="paint"/> 가 true 면 칠하기(비트 set), false 면 지우기(비트 clear).
    /// 구간은 방향 무관(뒤로 끌어도 동일).</para>
    /// </summary>
    public static int ApplyRange(int baseMask, int fromIndex, int toIndex, bool paint)
    {
        var lo = Math.Min(fromIndex, toIndex);
        var hi = Math.Max(fromIndex, toIndex);
        if (lo < 0) lo = 0;
        if (hi > 6) hi = 6;

        var mask = baseMask;
        for (var i = lo; i <= hi; i++)
        {
            var bit = 1 << i;
            mask = paint ? (mask | bit) : (mask & ~bit);
        }
        return mask;
    }

    /// <summary>
    /// 제스처 의도 결정 — 시작 셀이 꺼져 있으면 '칠하기', 켜져 있으면 '지우기'.
    /// <para>이렇게 해야 한 번의 드래그가 한 방향으로만 작동해 예측 가능하다.</para>
    /// </summary>
    public static bool DecidePaintIntent(int mask, int startIndex)
        => (mask & (1 << startIndex)) == 0;

    /// <summary>
    /// 이 셀이 지금 제스처 범위 안인가(시각 표시용).
    /// <para>드래그 중이 아니면 항상 false.</para>
    /// </summary>
    public static bool IsInRange(bool dragging, int fromIndex, int toIndex, int cellIndex)
        => dragging
        && cellIndex >= Math.Min(fromIndex, toIndex)
        && cellIndex <= Math.Max(fromIndex, toIndex);
}
