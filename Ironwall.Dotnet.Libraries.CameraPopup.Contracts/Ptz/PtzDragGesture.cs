namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Ptz;

/// <summary>
/// 영상 위 드래그 한 번의 결과 — 영상 상자 크기에 대한 비율(오른쪽 +, 아래 +)과 상자 가로세로비(가로/세로).
/// 비율이라 DPI · 상자 크기와 무관하다: 호스트는 "지금 화각의 몇 배"만 알면 된다.
/// </summary>
public readonly record struct PtzDragVector(double ViewX, double ViewY, double ViewAspect);

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

    /// <summary>데드존을 넘었다 — 이때부터 목표 표시를 그리고, 떼면 이동을 보낸다.</summary>
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
    /// 목표 표시 자리(순수) — 드래그 벡터만큼 화면 중심에서 떨어진 점. 떼면 <b>이 점이 화면 중심으로 온다</b>.
    /// 상자 밖으로 나가면 가장자리에 붙인다(카메라도 그만큼만 간다 — 한 번에 화면 반 너비까지).
    /// </summary>
    public static (double X, double Y) TargetPoint(double viewWidth, double viewHeight, double dx, double dy)
    {
        double x = Math.Clamp((viewWidth / 2) + dx, 0, Math.Max(0, viewWidth));
        double y = Math.Clamp((viewHeight / 2) + dy, 0, Math.Max(0, viewHeight));
        return (x, y);
    }
}
