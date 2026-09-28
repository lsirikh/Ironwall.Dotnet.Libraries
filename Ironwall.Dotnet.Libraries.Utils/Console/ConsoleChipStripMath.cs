namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/****************************************************************************
   Purpose      : 칩 줄(ConsoleChipStrip)이 접힌 한 줄에 칩을 몇 개 세우고 몇 개를 [더 보기]로 넘길지 — 순수 계산
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary><see cref="ConsoleChipStripMath.Fit"/> 의 결과.</summary>
/// <param name="Visible">접힌 줄에 세울 칩의 번호(그리는 순서). 고른 칩이 뒤에 있으면 [더 보기] 바로 앞 — 맨 끝 — 에 온다.</param>
/// <param name="HiddenCount">[더 보기] 뒤로 넘긴 칩 수.</param>
/// <param name="ShowsToggle">[더 보기]가 필요한가(칩이 한 줄에 다 들어가면 거짓).</param>
/// <param name="SelectedKept">고른 칩이 접힌 줄에 보이는가(고른 칩이 없으면 참).</param>
public readonly record struct ChipStripFit(IReadOnlyList<int> Visible, int HiddenCount, bool ShowsToggle, bool SelectedKept);

/// <summary>
/// 칩 줄의 접힌 한 줄 판정 — <b>이 한 곳에서만</b> 한다. 화면 없이 단위 시험할 수 있게 WPF 에 기대지 않는다.
/// </summary>
/// <remarks>
/// <para>규칙 ① 칩이 한 줄에 다 들어가면 [더 보기]를 두지 않는다. ② 넘치면 [더 보기] 자리를 먼저 떼어 두고 앞에서부터 들어가는
/// 만큼만 세운다. ③ <b>고른 칩은 접혀도 늘 보인다</b> — 뒤로 밀려날 차례라면 앞 칩을 덜 세우고 [더 보기] 바로 앞에 세운다
/// (운영자가 지금 무엇을 골랐는지 화면에서 잃지 않게). 자리가 칩 하나에도 모자라면 고른 칩 하나만 세운다(잘려 보일 수 있다).</para>
/// <para>폭은 모두 칩의 바깥 여백까지 더한 값(DesiredSize.Width)이다.</para>
/// </remarks>
public static class ConsoleChipStripMath
{
    /// <summary>폭 비교의 반올림 여유 — 측정 폭의 소수점 오차로 마지막 칩이 흔들리지 않게.</summary>
    public const double Epsilon = 0.5;

    /// <param name="available">줄에 쓸 수 있는 폭. 무한이면 늘 한 줄에 다 들어간다.</param>
    /// <param name="chipWidths">칩마다의 폭(순서대로).</param>
    /// <param name="toggleWidth">[더 보기] 칩의 폭(바깥 여백 포함).</param>
    /// <param name="selectedIndex">고른 칩의 번호. 없으면 음수.</param>
    public static ChipStripFit Fit(double available, IReadOnlyList<double> chipWidths, double toggleWidth, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(chipWidths);
        var count = chipWidths.Count;
        var selected = selectedIndex >= 0 && selectedIndex < count ? selectedIndex : -1;

        var total = 0.0;
        for (var i = 0; i < count; i++) total += Math.Max(0, chipWidths[i]);
        if (double.IsInfinity(available) || double.IsNaN(available) || total <= available + Epsilon)
            return new ChipStripFit(Enumerable.Range(0, count).ToArray(), 0, false, true);

        var budget = Math.Max(0, available - Math.Max(0, toggleWidth));

        // 앞에서부터 들어가는 만큼
        var prefix = 0;
        var used = 0.0;
        while (prefix < count && used + Math.Max(0, chipWidths[prefix]) <= budget + Epsilon)
        {
            used += Math.Max(0, chipWidths[prefix]);
            prefix++;
        }

        var visible = new List<int>(prefix + 1);
        if (selected < 0 || selected < prefix)
        {
            for (var i = 0; i < prefix; i++) visible.Add(i);
        }
        else
        {
            // 고른 칩이 밀려날 차례 — 고른 칩 폭만큼 앞 칩을 덜 세운다
            var selectedWidth = Math.Max(0, chipWidths[selected]);
            var room = budget - selectedWidth;
            var take = 0;
            var sum = 0.0;
            while (take < count && take != selected && sum + Math.Max(0, chipWidths[take]) <= room + Epsilon)
            {
                sum += Math.Max(0, chipWidths[take]);
                take++;
            }
            for (var i = 0; i < take; i++) visible.Add(i);
            visible.Add(selected);
        }

        return new ChipStripFit(visible, count - visible.Count, true, selected < 0 || visible.Contains(selected));
    }

    /// <summary>
    /// 펼친 줄 — 칩을 순서대로 흘려 넣어 줄 번호를 매긴다(줄 첫 칸은 폭이 모자라도 그 줄에 선다 — 빈 줄을 만들지 않는다).
    /// </summary>
    /// <returns>칸마다의 줄 번호(0부터).</returns>
    public static int[] Wrap(double available, IReadOnlyList<double> widths)
    {
        ArgumentNullException.ThrowIfNull(widths);
        var lines = new int[widths.Count];
        var line = 0;
        var x = 0.0;
        for (var i = 0; i < widths.Count; i++)
        {
            var w = Math.Max(0, widths[i]);
            if (x > 0 && x + w > available + Epsilon)
            {
                line++;
                x = 0;
            }
            lines[i] = line;
            x += w;
        }
        return lines;
    }
}
