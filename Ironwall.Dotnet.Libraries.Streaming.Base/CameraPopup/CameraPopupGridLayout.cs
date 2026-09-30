using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 이벤트 창 타일 격자 (camera-popup-modes PRD FR-10)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 격자 한 모양. 표기는 <b>가로×세로</b>(열×행) — <c>3×2</c> 는 가로 3칸, 세로 2줄.
/// 저장 키는 ASCII <c>3x2</c>(설정 파일 가독성 · 대소문자 무관 파싱).
/// </summary>
public readonly record struct CameraPopupGridLayout(int Columns, int Rows)
{
    /// <summary>칸 수(= 가로 × 세로).</summary>
    public int Capacity => Columns * Rows;

    /// <summary>설정 파일에 적는 값 — <c>3x2</c>.</summary>
    public string Key => string.Create(CultureInfo.InvariantCulture, $"{Columns}x{Rows}");

    /// <summary>화면 글자 — <c>3×2</c>.</summary>
    public string Display => string.Create(CultureInfo.InvariantCulture, $"{Columns}×{Rows}");

    public override string ToString() => Key;

    /// <summary><c>3x2</c> · <c>3X2</c> · <c>3×2</c> 를 받는다. 양수 두 개가 아니면 실패.</summary>
    public static bool TryParse(string? text, out CameraPopupGridLayout layout)
    {
        layout = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Trim().Split(new[] { 'x', 'X', '×' }, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var columns)) return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows)) return false;
        if (columns <= 0 || rows <= 0) return false;

        layout = new CameraPopupGridLayout(columns, rows);
        return true;
    }
}

/// <summary>
/// 창당 카메라 수에 맞는 격자 규칙 — 순수 함수(헤드리스 시험 · 설정 화면 · 이벤트 창이 같은 표를 쓴다).
/// </summary>
/// <remarks>
/// 표(PRD FR-10 · SB §2 ⑥): 1 <c>1×1</c> · 2 <c>2×1/1×2</c> · 3 <c>3×1/1×3</c> · 4 <c>2×2/4×1/1×4</c> ·
/// 5 <c>3×2/2×3</c>(한 칸 비움) · 6 <c>3×2/2×3/6×1/1×6</c>. 첫 항목이 기본값이다.
/// </remarks>
public static class CameraPopupGridLayouts
{
    /// <summary>창당 카메라 최소 · 최대(PRD FR-10).</summary>
    public const int MinCameras = 1;
    public const int MaxCameras = 6;

    private static readonly CameraPopupGridLayout[][] Table =
    {
        new[] { new CameraPopupGridLayout(1, 1) },
        new[] { new CameraPopupGridLayout(2, 1), new CameraPopupGridLayout(1, 2) },
        new[] { new CameraPopupGridLayout(3, 1), new CameraPopupGridLayout(1, 3) },
        new[] { new CameraPopupGridLayout(2, 2), new CameraPopupGridLayout(4, 1), new CameraPopupGridLayout(1, 4) },
        new[] { new CameraPopupGridLayout(3, 2), new CameraPopupGridLayout(2, 3) },
        new[] { new CameraPopupGridLayout(3, 2), new CameraPopupGridLayout(2, 3), new CameraPopupGridLayout(6, 1), new CameraPopupGridLayout(1, 6) },
    };

    /// <summary>1~6 밖의 수를 안쪽으로 당긴다.</summary>
    public static int ClampCount(int cameraCount) => Math.Clamp(cameraCount, MinCameras, MaxCameras);

    /// <summary>그 수에서 고를 수 있는 격자(첫 항목 = 기본). 범위 밖 수는 먼저 당긴다.</summary>
    public static IReadOnlyList<CameraPopupGridLayout> Allowed(int cameraCount) => Table[ClampCount(cameraCount) - 1];

    /// <summary>그 수의 기본 격자.</summary>
    public static CameraPopupGridLayout Default(int cameraCount) => Allowed(cameraCount)[0];

    /// <summary>그 수에서 고를 수 있는 격자인가.</summary>
    public static bool IsAllowed(int cameraCount, CameraPopupGridLayout layout) => Allowed(cameraCount).Contains(layout);

    /// <summary>
    /// 카메라 수를 바꿨을 때 격자를 맞춘다 — 지금 격자가 새 수에서도 고를 수 있으면 그대로, 아니면 새 수의 기본.
    /// (예: 6대 · 2×3 → 5대면 2×3 유지, 6대 · 6×1 → 5대면 3×2.)
    /// </summary>
    public static CameraPopupGridLayout Snap(int cameraCount, CameraPopupGridLayout? current)
        => current is { } layout && IsAllowed(cameraCount, layout) ? layout : Default(cameraCount);
}
