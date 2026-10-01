using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 칩을 <b>화면 크기</b>로 벌리기 — 순수 함수(헤디드 r22: 벽돌 한 칸 세 대가 낮은 배율에서 39px 칩이 27px 간격으로 겹쳤다).
/// 칩 그림(번호판)은 배율이 작아지면 세계 단위로 커지므로(글자 최소 크기) 세계 간격(<see cref="FenceWorld.ChipGap"/>)만으로는 모자란다 —
/// 지금 배율에서 칩 사각형의 폭으로 다시 벌린다.
/// </summary>
public static class FenceChipSpacing
{
    /// <summary>
    /// 한 줄의 칩(앵커 x · 사각형 왼쪽 끝 오프셋 · 폭 — 모두 세계 단위)을 왼쪽 끝 순서대로 받아, 사각형이 서로 겹치지 않는 앵커 x 를 돌려준다.
    /// 왼쪽 끝 사이를 "가장 넓은 칩 + <paramref name="gap"/>" 이상으로 — 겹친 무리만 움직이고 무리 가운데는 제자리 평균(<see cref="FenceWorld.Separate"/>).
    /// 이미 떨어진 칩은 그대로다.
    /// </summary>
    public static IReadOnlyList<double> Spread(IReadOnlyList<(double X, double Left, double Width)> chips, double gap)
    {
        var list = chips ?? Array.Empty<(double, double, double)>();
        if (list.Count == 0) return Array.Empty<double>();
        var widest = list.Max(c => Math.Max(0, c.Width));
        var lefts = list.Select(c => c.X + c.Left).ToList();
        var spread = FenceWorld.Separate(lefts, widest + Math.Max(0, gap));
        return list.Select((c, i) => spread[i] - c.Left).ToList();
    }
}
