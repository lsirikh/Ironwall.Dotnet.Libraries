namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Ptz;

/// <summary>
/// 영상 위 드래그 한 번의 결과 — 영상 상자 크기에 대한 비율(오른쪽 +, 아래 +)과 상자 가로세로비(가로/세로).
/// 비율이라 DPI · 상자 크기와 무관하다: 호스트는 "지금 화각의 몇 배"만 알면 된다.
/// </summary>
public readonly record struct PtzDragVector(double ViewX, double ViewY, double ViewAspect);

/// <summary>화살표 그림 한 장 — 몸통 끝(촉 안쪽) 좌표와 촉 방향(도, 화면 좌표: 0 = 오른쪽 · 90 = 아래).</summary>
public readonly record struct PtzArrow(double ShaftEndX, double ShaftEndY, double AngleDeg);

/// <summary>
/// 영상 위 PTZ 드래그 제스처(순수 · WPF 무의존) — 지도 오버레이(GIS)와 이벤트 창 타일(호스트)이 같은 판정을 쓴다.
/// 캡처 드래그 계약(drag-first-ux): 눌림(<see cref="IsPressed"/>)과 데드존 통과(<see cref="IsDragging"/>)를 분리하고,
/// 데드존(<see cref="DeadZone"/> = 8 DIU) 미만은 클릭이다. 끝내는 길은 <see cref="Finish"/> 하나 —
/// 뗌(commit) · 캡처 잃음 · Esc(취소)가 모두 이것을 부른다.
/// UI 스레드 전용(상태를 잠그지 않는다).
/// </summary>
public sealed class PtzDragGesture
{
    /// <summary>드래그 · 클릭을 가르는 데드존(DIU). 레포 공통 값 — 새 상수를 만들지 않는다.</summary>
    public const double DeadZone = 8.0;

    private double _startX, _startY, _currentX, _currentY, _viewWidth, _viewHeight;

    public bool IsPressed { get; private set; }

    /// <summary>데드존을 넘었다 — 이때부터 화살표를 그리고, 떼면 이동을 보낸다(누르는 동안은 보내지 않는다).</summary>
    public bool IsDragging { get; private set; }

    public double StartX => _startX;
    public double StartY => _startY;
    public double CurrentX => _currentX;
    public double CurrentY => _currentY;

    /// <summary>
    /// 누름. 영상 상자 밖이거나 상자 크기가 0 이면 시작하지 않는다(false). 이미 눌린 상태에서 또 오면 무시.
    /// </summary>
    public bool Press(double x, double y, double viewWidth, double viewHeight)
    {
        if (IsPressed) return false;
        if (!(viewWidth > 0) || !(viewHeight > 0)) return false;
        if (!double.IsFinite(x) || !double.IsFinite(y)) return false;
        if (x < 0 || y < 0 || x > viewWidth || y > viewHeight) return false;
        IsPressed = true;
        IsDragging = false;
        _startX = _currentX = x;
        _startY = _currentY = y;
        _viewWidth = viewWidth;
        _viewHeight = viewHeight;
        return true;
    }

    /// <summary>이동. 데드존을 넘은 뒤로는 계속 true(다시 안으로 들어와도 드래그는 유지 — 되돌리려면 Esc).</summary>
    public bool Move(double x, double y)
    {
        if (!IsPressed || !double.IsFinite(x) || !double.IsFinite(y)) return IsDragging;
        _currentX = x;
        _currentY = y;
        if (!IsDragging && IsBeyondDeadZone(x - _startX, y - _startY)) IsDragging = true;
        return IsDragging;
    }

    /// <summary>
    /// 끝(단일 지점). 상태를 먼저 지우고 결과를 돌려준다 — <paramref name="commit"/> 이고 데드존을 넘었을 때만 벡터,
    /// 그 밖(클릭 · 취소 · 눌린 적 없음)은 null.
    /// </summary>
    public PtzDragVector? Finish(bool commit)
    {
        if (!IsPressed) return null;
        bool dragged = IsDragging;
        double dx = _currentX - _startX, dy = _currentY - _startY, w = _viewWidth, h = _viewHeight;
        IsPressed = false;
        IsDragging = false;
        if (!commit || !dragged) return null;
        return new PtzDragVector(dx / w, dy / h, w / h);
    }

    /// <summary>제곱 비교(레포의 다른 캡처 드래그와 같은 식).</summary>
    public static bool IsBeyondDeadZone(double dx, double dy) => (dx * dx) + (dy * dy) > DeadZone * DeadZone;

    /// <summary>
    /// 화살표 길이 기준(사용자 결정 2026-10-16 — 10-15 의 "가로 1/2 = 한 폭"을 대체) — 화면 비율: 가로 성분 ÷ 영상 폭, 세로 성분 ÷ 영상 높이,
    /// 성분마다 ±1(화면 하나)에서 자른다. <b>화살표가 화면 끝까지(폭 전체) = 화면 한 폭(수평 화각 1개)만큼 돈다</b>, 높이 전체 = 수직 화각 1개.
    /// 돌려주는 값은 폭 단위(세로 = 높이 비율 ÷ 가로세로비) — 각도/픽셀이 가로와 같다.
    /// 입력은 영상 상자 비율(<see cref="PtzDragVector"/> 와 같은 꼴). 유한하지 않은 값은 0.
    /// </summary>
    public static (double X, double Y) WidthVector(double viewX, double viewY, double viewAspect)
    {
        double aspect = double.IsFinite(viewAspect) && viewAspect > 0.1 && viewAspect < 10 ? viewAspect : 16d / 9d;
        double x = double.IsFinite(viewX) ? Math.Clamp(viewX, -1d, 1d) : 0d;
        double y = double.IsFinite(viewY) ? Math.Clamp(viewY, -1d, 1d) / aspect : 0d;
        return (x, y);
    }

    /// <summary>화살표 크기 = 화면 비율 벡터(가로 ÷ 폭, 세로 ÷ 높이, 성분마다 ±1 에서 자름)의 길이. 1 = 화면 하나만큼. 화살표 옆 글자에 쓴다.</summary>
    public static double DragMagnitude(double viewWidth, double viewHeight, double dx, double dy)
    {
        if (!(viewWidth > 0) || !(viewHeight > 0)) return 0d;
        double x = Math.Clamp(dx / viewWidth, -1d, 1d), y = Math.Clamp(dy / viewHeight, -1d, 1d);
        return double.IsFinite(x) && double.IsFinite(y) ? Math.Sqrt((x * x) + (y * y)) : 0d;
    }

    /// <summary>화살표 크기 글자 — "0.6화면"(지금 보이는 화면의 0.6배만큼 돈다).</summary>
    public static string AmountText(double magnitude)
        => Math.Max(0, magnitude).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "화면";

    /// <summary>
    /// 화살표 그림(순수) — 시작점(누른 곳) → 끝점(지금 마우스). 몸통은 끝점에서 촉 길이(<paramref name="headLength"/>)만큼 물러난 자리에서 끝나
    /// 둥근 끝이 촉 끝을 뚫고 나오지 않는다(화살표가 촉보다 짧으면 몸통 없음 = 시작점). 각도 = 화면 좌표(아래 +)의 도, 0 = 오른쪽.
    /// </summary>
    public static PtzArrow Arrow(double startX, double startY, double endX, double endY, double headLength)
    {
        double dx = endX - startX, dy = endY - startY;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        double angle = length > 0 ? Math.Atan2(dy, dx) * 180d / Math.PI : 0d;
        if (!(length > headLength)) return new PtzArrow(startX, startY, angle);
        return new PtzArrow(endX - (dx / length * headLength), endY - (dy / length * headLength), angle);
    }

    /// <summary>
    /// 목표 자리(순수) — 떼면 <b>화면 중심으로 올 점</b> = 중심 + 화살표(<see cref="WidthVector"/> × 화면 가로).
    /// 화면 밖이면 중심에서 그 방향으로 가장자리에 붙인다(방향은 유지).
    /// </summary>
    public static (double X, double Y) TargetPoint(double viewWidth, double viewHeight, double dx, double dy)
    {
        double w = Math.Max(0, viewWidth), h = Math.Max(0, viewHeight);
        if (!(w > 0) || !(h > 0)) return (w / 2, h / 2);
        var (mx, my) = WidthVector(dx / w, dy / h, w / h);
        double tx = mx * w, ty = my * w;
        double scale = 1d;
        if (Math.Abs(tx) > w / 2) scale = Math.Min(scale, (w / 2) / Math.Abs(tx));
        if (Math.Abs(ty) > h / 2) scale = Math.Min(scale, (h / 2) / Math.Abs(ty));
        return ((w / 2) + (tx * scale), (h / 2) + (ty * scale));
    }
}
