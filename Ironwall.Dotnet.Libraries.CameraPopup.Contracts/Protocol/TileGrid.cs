using System.Globalization;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// 이벤트 창 격자(순수). "열x행" 문자열을 해석하고, 비었거나 카메라 수를 못 담으면 자동 격자로 대체한다.
/// 선택지 규칙(FR-10: 1~6대별 허용 격자)은 설정 화면(T-03/T-05)에서 정교화 — 여기서는 안전한 해석만.
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
}
