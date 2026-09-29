namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;

/// <summary>
/// 다이얼로그 폭 — <b>세 가지뿐</b>이다(스토리보드 #h-dlg L1425-1428: 실측 400 · 450 · 460 · 500 · 550 · 560 · 600 → S 400 · M 560 · L 720).
/// </summary>
public enum DialogSize
{
    /// <summary>400 — 확인 · 안내 · 한 줄 묻기 · 비밀번호 재설정 · 부대 소속 픽커.</summary>
    Small,
    /// <summary>560 — 폼 하나. 계정 등록 · 내 정보 · 함체 임계값 · 조치보고 · 변경 미리보기.</summary>
    Medium,
    /// <summary>720 — 두 칸이 나란히 서는 창. 장비 배정(좌 ↔ 우) · 프리셋 관리.</summary>
    Large,
}

/// <summary>한 다이얼로그가 실제로 차지하는 치수. 폭은 규격이 정하고 높이는 내용이 정하되 <b>최대치만</b> 정한다(L1425).</summary>
/// <param name="Width">카드 폭(DIU). 규격 폭이되 창보다 넓어지지 않는다.</param>
/// <param name="MaxHeight">카드가 넘을 수 없는 높이. 넘치는 몸통은 스크롤한다.</param>
public readonly record struct DialogMetrics(double Width, double MaxHeight);

/// <summary>
/// 규격 → 실제 치수. <b>순수 함수</b>라 창 없이 시험한다(규칙 drag-first-ux "판정 로직은 UI 에서 분리한 순수 함수").
/// </summary>
/// <remarks>
/// 세 가지를 지킨다: ① 규격 폭을 넘지 않는다 ② <b>창보다 넓지 않다</b> ③ 최소 폭 아래로 내려가 글자가 겹치지 않는다.
/// 창 치수를 모르면(0 · 음수 · NaN · 무한) 규격 폭을 그대로 쓴다 — 아직 배치되지 않은 첫 측정이 그렇다.
/// </remarks>
public static class DialogSizeRules
{
    /// <summary>카드와 창 사이 여백(한쪽). 모달 스크림 위에서 카드가 창에 닿지 않게 한다.</summary>
    public const double Gutter = 24d;

    /// <summary>이보다 좁아지면 버튼 줄이 겹친다 — 창이 더 좁으면 창 폭을 쓴다.</summary>
    public const double MinWidth = 320d;

    /// <summary>머리 + 버튼 줄만 남아도 이만큼은 있어야 한다.</summary>
    public const double MinHeight = 160d;

    /// <summary>규격 폭(DIU) — 스토리보드 L1425 · T4 정의 L1502.</summary>
    public static double NominalWidth(DialogSize size) => size switch
    {
        DialogSize.Small => 400d,
        DialogSize.Medium => 560d,
        DialogSize.Large => 720d,
        _ => 560d,
    };

    /// <summary>규격별 높이 상한 — 내용이 길어도 여기서 멈추고 몸통이 스크롤한다.</summary>
    public static double NominalMaxHeight(DialogSize size) => size switch
    {
        DialogSize.Small => 560d,
        DialogSize.Medium => 720d,
        DialogSize.Large => 780d,
        _ => 720d,
    };

    /// <summary>규격과 창 치수로 카드 치수를 정한다. <paramref name="ownerWidth"/>/<paramref name="ownerHeight"/> 가 쓸 수 없는 값이면 규격 값 그대로.</summary>
    public static DialogMetrics Resolve(DialogSize size, double ownerWidth, double ownerHeight)
        => new(ResolveWidth(size, ownerWidth), ResolveMaxHeight(size, ownerHeight));

    public static double ResolveWidth(DialogSize size, double ownerWidth)
    {
        var nominal = NominalWidth(size);
        if (!IsUsable(ownerWidth)) return nominal;

        var available = ownerWidth - (2 * Gutter);
        var width = Math.Min(nominal, Math.Max(MinWidth, available));
        // 창이 최소 폭보다도 좁다 — 그래도 창 밖으로 나가지는 않는다.
        return Math.Min(width, ownerWidth);
    }

    public static double ResolveMaxHeight(DialogSize size, double ownerHeight)
    {
        var nominal = NominalMaxHeight(size);
        if (!IsUsable(ownerHeight)) return nominal;

        var available = ownerHeight - (2 * Gutter);
        var height = Math.Min(nominal, Math.Max(MinHeight, available));
        return Math.Min(height, ownerHeight);
    }

    /// <summary>창을 이 규격의 다이얼로그에 맞춰 띄울 때의 바깥 치수 — 카드 폭 + 양쪽 여백.</summary>
    public static double WindowWidth(DialogSize size) => NominalWidth(size) + (2 * Gutter);

    /// <summary>
    /// 틀이 제 OS 창의 뿌리일 때 창 높이 — 창 겉(제목 줄 · 테두리) + 틀이 원하는 높이. 틀 높이는 규격 상한을, 창은 작업 영역을 넘지 않는다.
    /// 쓸 수 없는 값(0 · 음수 · NaN · 무한)이 하나라도 있으면 <c>null</c> — 창을 건드리지 않는다.
    /// </summary>
    /// <param name="frameDesiredHeight">틀을 창 폭 · 무한 높이로 잰 높이(몸통이 다 보이는 높이).</param>
    /// <param name="chromeHeight">창 바깥 높이 − 틀 높이(제목 줄 · 테두리 · 뷰의 여백).</param>
    /// <param name="workAreaHeight">작업 영역 높이.</param>
    public static double? FitWindowHeight(DialogSize size, double frameDesiredHeight, double chromeHeight, double workAreaHeight)
    {
        if (!IsUsable(frameDesiredHeight) || double.IsNaN(chromeHeight) || double.IsInfinity(chromeHeight) || chromeHeight < 0 || !IsUsable(workAreaHeight))
            return null;
        var frame = Math.Min(frameDesiredHeight, NominalMaxHeight(size));
        return Math.Min(Math.Ceiling(chromeHeight + frame), workAreaHeight);
    }

    private static bool IsUsable(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
}
