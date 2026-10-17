using System.Windows;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;

/// <summary>
/// 셸 안 카드(<see cref="ConsoleDialogFrame"/> — 창의 뿌리가 아닌 것)의 자리 — <b>가운데에서 얼마나 비켜 있는가</b>(<see cref="Vector"/>)로만 다룬다.
/// </summary>
/// <remarks>
/// <para>카드는 틀(= 호스트 층 전체) 한가운데에 놓이고(템플릿의 <c>HorizontalAlignment=Center</c>), 사람이 머리를 끌면 그만큼 비킨다.
/// 비킴은 늘 <b>카드 전체가 틀 안에 남도록</b> 잘린다 — 버튼 줄 · ✕ 가 셸 밖으로 나가 닫을 길이 사라지지 않게.</para>
/// <para>좌표는 틀의 DIU 다. 셸 안에서 잰 값이라 모니터 · DPI 가 달라도 같은 식이다(호스트 층이 그 셸의 크기를 이미 따른다).
/// OS 창으로 뜨는 다이얼로그(<see cref="ConsoleDialogFrame.IsWindowRoot"/>)는 런처가 <c>CenterOwner</c> 로 띄우고 OS 제목 줄로 옮긴다 — 여기 오지 않는다.</para>
/// <para><b>순수 함수</b>라 창 없이 시험한다(drag-first-ux "판정 로직은 UI 에서 분리한 순수 함수").</para>
/// </remarks>
public static class DialogPlacementRules
{
    /// <summary>화살표 한 번(DIU) — 셸 표면(<see cref="SurfaceMath.KeyboardStep"/>)과 같은 걸음.</summary>
    public const double KeyboardStep = SurfaceMath.KeyboardStep;

    /// <summary>Ctrl+화살표 한 번(DIU).</summary>
    public const double KeyboardCoarseStep = SurfaceMath.KeyboardCoarseStep;

    /// <summary>가운데에서 비킬 수 있는 최대치(축마다 양쪽 같다). 카드가 틀보다 크거나 치수를 모르면 0 — 가운데에 둔다.</summary>
    public static Vector MaxOffset(Size card, Size host)
        => new(Half(host.Width - card.Width), Half(host.Height - card.Height));

    /// <summary>비킴을 틀 안으로 자른다. 쓸 수 없는 값(NaN · 무한)은 0(가운데)으로 본다.</summary>
    public static Vector Clamp(Vector offset, Size card, Size host)
    {
        var max = MaxOffset(card, host);
        return new Vector(ClampAxis(offset.X, max.X), ClampAxis(offset.Y, max.Y));
    }

    /// <summary>끌기 — 누른 순간의 비킴 + 포인터가 움직인 만큼, 틀 안으로 잘라서.</summary>
    public static Vector Drag(Vector offsetAtPress, double dx, double dy, Size card, Size host)
        => Clamp(new Vector(offsetAtPress.X + Finite(dx), offsetAtPress.Y + Finite(dy)), card, host);

    /// <summary>손잡이에 초점이 있을 때 화살표 — 한 걸음(<paramref name="coarse"/> 면 큰 걸음), 틀 안으로 잘라서.</summary>
    public static Vector KeyboardMove(Vector offset, int dirX, int dirY, bool coarse, Size card, Size host)
    {
        var step = coarse ? KeyboardCoarseStep : KeyboardStep;
        return Clamp(new Vector(offset.X + Math.Sign(dirX) * step, offset.Y + Math.Sign(dirY) * step), card, host);
    }

    /// <summary>카드 왼쪽 위 모서리(틀 좌표) — 가운데 자리 + 비킴. 시험 · 진단용.</summary>
    public static Point Origin(Vector offset, Size card, Size host)
        => new(((host.Width - card.Width) / 2) + offset.X, ((host.Height - card.Height) / 2) + offset.Y);

    private static double Half(double room) => double.IsFinite(room) && room > 0 ? room / 2 : 0;

    private static double ClampAxis(double value, double max)
        => double.IsFinite(value) ? Math.Clamp(value, -max, max) : 0;

    private static double Finite(double value) => double.IsFinite(value) ? value : 0;
}
