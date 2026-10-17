namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 이벤트 창 배치 — 모니터 찾기 · 첫 위치 · 계단식 · 화면 밖 당기기 (PRD FR-12)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>대상 모니터를 어떻게 찾았는가.</summary>
public enum MonitorMatch
{
    /// <summary>장치 이름 + 해상도가 그대로 맞다.</summary>
    Exact,

    /// <summary>대상을 정하지 않았다(빈 값) — 주 모니터.</summary>
    DefaultPrimary,

    /// <summary>같은 장치인데 해상도가 바뀌었다 — 그 모니터를 쓰되 위치는 다시 당긴다(안내).</summary>
    ResolutionChanged,

    /// <summary>그 모니터가 없다 — 주 모니터 안쪽으로 옮긴다(안내).</summary>
    FallbackPrimary,

    /// <summary>모니터를 하나도 못 읽었다.</summary>
    NoMonitors,
}

/// <summary>모니터 찾기 결과.</summary>
public readonly record struct MonitorResolution(DisplayMonitorInfo? Monitor, MonitorMatch Match)
{
    /// <summary>사람에게 알려야 하는가(PRD FR-12 "옮기고 안내").</summary>
    public bool NeedsNotice => Match is MonitorMatch.ResolutionChanged or MonitorMatch.FallbackPrimary or MonitorMatch.NoMonitors;
}

/// <summary>
/// 이벤트 창 배치의 순수 함수 — 설정 화면 미리보기 · 창 관리자(T-04)가 같은 계산을 쓴다.
/// </summary>
/// <remarks>
/// <para>계단식(PRD FR-12): 첫 창은 고른 모니터 작업 영역 안 (x, y). 다음 창은 <b>오른쪽 아래로 간격씩</b>.
/// 창이 작업 영역 오른쪽 또는 아래 끝을 넘게 되면 <b>첫 위치로 돌아와</b> 다시 쌓는다.</para>
/// <para>화면 밖 방어: 창 크기가 작업 영역보다 크면 작업 영역 크기로 줄이고, 위치는 창이 통째로 들어오게 당긴다.</para>
/// </remarks>
public static class CameraPopupPlacement
{
    /// <summary>
    /// 저장된 대상 모니터를 지금 목록에서 찾는다 — 이름 + 해상도 → 이름만(해상도 바뀜) → 주 모니터.
    /// </summary>
    public static MonitorResolution ResolveMonitor(IReadOnlyList<DisplayMonitorInfo>? monitors, string? targetId)
    {
        if (monitors is null || monitors.Count == 0) return new MonitorResolution(null, MonitorMatch.NoMonitors);

        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        if (!CameraPopupMonitorId.TryParse(targetId, out var device, out var width, out var height))
            return new MonitorResolution(primary, MonitorMatch.DefaultPrimary);

        var sameDevice = monitors.Where(m => string.Equals(m.DeviceName, device, StringComparison.OrdinalIgnoreCase)).ToList();
        var exact = sameDevice.FirstOrDefault(m => m.Bounds.Width == width && m.Bounds.Height == height);
        if (exact is not null) return new MonitorResolution(exact, MonitorMatch.Exact);
        if (sameDevice.Count > 0) return new MonitorResolution(sameDevice[0], MonitorMatch.ResolutionChanged);

        return new MonitorResolution(primary, MonitorMatch.FallbackPrimary);
    }

    /// <summary>
    /// 창 크기를 작업 영역에 맞게 줄이고(넘칠 때만), 작업 영역 기준 상대 위치를 창이 통째로 들어오게 당긴다.
    /// </summary>
    /// <returns>당긴 상대 위치 · 크기, 그리고 당겼는지(안내용).</returns>
    public static (int X, int Y, int Width, int Height, bool Clamped) ClampRelative(PixelRect workArea, int x, int y, int width, int height)
    {
        var w = Math.Max(1, Math.Min(width, Math.Max(1, workArea.Width)));
        var h = Math.Max(1, Math.Min(height, Math.Max(1, workArea.Height)));
        var cx = Math.Clamp(x, 0, Math.Max(0, workArea.Width - w));
        var cy = Math.Clamp(y, 0, Math.Max(0, workArea.Height - h));
        var clamped = cx != x || cy != y || w != width || h != height;
        return (cx, cy, w, h, clamped);
    }

    /// <summary>첫 창의 절대 자리(당긴 뒤).</summary>
    public static PixelRect FirstWindow(PixelRect workArea, int x, int y, int width, int height)
    {
        var c = ClampRelative(workArea, x, y, width, height);
        return new PixelRect(workArea.X + c.X, workArea.Y + c.Y, c.Width, c.Height);
    }

    /// <summary>
    /// 계단 한 바퀴의 창 수 — 첫 위치에서 간격씩 밀어 작업 영역을 넘기 직전까지(최소 1).
    /// 간격이 0 이면 1(전부 같은 자리).
    /// </summary>
    public static int CascadePeriod(PixelRect workArea, PixelRect first, int step)
    {
        if (step <= 0) return 1;
        var roomX = workArea.Right - first.Right;
        var roomY = workArea.Bottom - first.Bottom;
        if (roomX < 0 || roomY < 0) return 1;
        return Math.Min(roomX / step, roomY / step) + 1;
    }

    /// <summary><paramref name="index"/> 번째(0부터) 창의 절대 자리.</summary>
    public static PixelRect CascadeAt(PixelRect workArea, int x, int y, int width, int height, int step, int index)
    {
        var first = FirstWindow(workArea, x, y, width, height);
        var period = CascadePeriod(workArea, first, step);
        var k = period <= 1 ? 0 : ((index % period) + period) % period;
        return first.Offset(k * Math.Max(0, step), k * Math.Max(0, step));
    }

    /// <summary>처음 <paramref name="count"/> 개 창의 자리.</summary>
    public static IReadOnlyList<PixelRect> Cascade(PixelRect workArea, int x, int y, int width, int height, int step, int count)
    {
        var list = new List<PixelRect>(Math.Max(0, count));
        for (var i = 0; i < count; i++) list.Add(CascadeAt(workArea, x, y, width, height, step, i));
        return list;
    }

    /// <summary>설정 한 벌로 계단 자리.</summary>
    public static IReadOnlyList<PixelRect> Cascade(PixelRect workArea, CameraPopupSettings settings, int count)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        return Cascade(workArea, settings.FirstWindowX, settings.FirstWindowY,
                       settings.WindowWidth, settings.WindowHeight, settings.CascadeStepPx, count);
    }
}
