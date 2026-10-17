using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

/****************************************************************************
   Purpose      : 모니터 식별자 = 장치 이름 + 해상도 (camera-popup-modes PRD §3 모니터)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>\\.\DISPLAY2@2560x1440</c>. 순번(재부팅 · 도킹에서 바뀐다)이 아니라 장치 이름 + 해상도로 찾는다.
/// </summary>
public static class CameraPopupMonitorId
{
    public static string Format(string deviceName, int width, int height)
        => string.Create(CultureInfo.InvariantCulture, $"{deviceName}@{width}x{height}");

    /// <summary>읽기. 해상도 부분이 없거나 깨졌으면 장치 이름만 돌려주고 해상도는 0.</summary>
    public static bool TryParse(string? id, out string deviceName, out int width, out int height)
    {
        deviceName = string.Empty;
        width = height = 0;
        if (string.IsNullOrWhiteSpace(id)) return false;

        var at = id.LastIndexOf('@');
        if (at < 0)
        {
            deviceName = id.Trim();
            return deviceName.Length > 0;
        }

        deviceName = id[..at].Trim();
        if (CameraPopupGridLayout.TryParse(id[(at + 1)..], out var size))
        {
            width = size.Columns;
            height = size.Rows;
        }
        return deviceName.Length > 0;
    }

    /// <summary>장치 이름 끝 숫자(<c>\\.\DISPLAY2</c> → 2). 없으면 <c>null</c>.</summary>
    public static int? DisplayNumber(string? deviceName)
    {
        if (string.IsNullOrEmpty(deviceName)) return null;
        var end = deviceName.Length;
        var start = end;
        while (start > 0 && char.IsDigit(deviceName[start - 1])) start--;
        return start < end && int.TryParse(deviceName.AsSpan(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
    }
}
