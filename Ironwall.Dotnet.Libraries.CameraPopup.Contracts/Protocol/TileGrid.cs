using System.Globalization;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// 이벤트 창 격자(순수). "열x행" 문자열을 해석하고, 비었거나 카메라 수를 못 담으면 자동 격자로 대체한다.
/// 선택지 규칙(FR-10): 1 <c>1×1</c> · 2 <c>2×1/1×2</c> · 3 <c>3×1/1×3</c> · 4 <c>2×2/4×1/1×4</c> ·
/// 5 <c>3×2/2×3</c> · 6 <c>3×2/2×3/6×1/1×6</c>. 표기는 "열×행".
/// </summary>
public static class TileGrid
{
    public const int MaxCameras = 6;

    public static bool TryParse(string? layout, out int columns, out int rows)
    {
        columns = rows = 0;
        if (string.IsNullOrWhiteSpace(layout)) return false;
        var parts = layout.Trim().ToLowerInvariant().Split('x');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out columns)) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out rows)) return false;
        return columns is >= 1 and <= MaxCameras && rows is >= 1 and <= MaxCameras && columns * rows <= MaxCameras;
    }

    /// <summary>표시할 격자. 카메라 수는 1..6 으로 자른다.</summary>
    public static (int Columns, int Rows) Resolve(string? layout, int cameraCount)
    {
        int count = Math.Clamp(cameraCount, 1, MaxCameras);
        if (TryParse(layout, out var c, out var r) && c * r >= count) return (c, r);
        int columns = (int)Math.Ceiling(Math.Sqrt(count));
        int rowsAuto = (int)Math.Ceiling(count / (double)columns);
        return (columns, rowsAuto);
    }

    private static readonly (int Columns, int Rows)[][] Allowed =
    {
        new[] { (1, 1) },
        new[] { (2, 1), (1, 2) },
        new[] { (3, 1), (1, 3) },
        new[] { (2, 2), (4, 1), (1, 4) },
        new[] { (3, 2), (2, 3) },
        new[] { (3, 2), (2, 3), (6, 1), (1, 6) },
    };

    /// <summary>창당 카메라 수(1..6)에 허용된 격자(첫 항목이 기본). 범위 밖은 잘라서 본다.</summary>
    public static IReadOnlyList<(int Columns, int Rows)> AllowedFor(int cameraCount)
        => Allowed[Math.Clamp(cameraCount, 1, MaxCameras) - 1];

    /// <summary>
    /// 열 · 행으로 표시할 격자. 카메라를 다 담고 6칸 이하이면 그대로(빈 칸 허용 — 5대 3×2),
    /// 아니면 허용 목록의 기본값.
    /// </summary>
    public static (int Columns, int Rows) Resolve(int columns, int rows, int cameraCount)
    {
        int count = Math.Clamp(cameraCount, 1, MaxCameras);
        if (columns >= 1 && rows >= 1 && columns * rows <= MaxCameras && columns * rows >= count) return (columns, rows);
        return AllowedFor(count)[0];
    }
}
