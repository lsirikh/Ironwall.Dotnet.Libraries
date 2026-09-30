namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// 이벤트 창 키(순수) = <c>{종류}-{이벤트 id}</c>, 예 <c>detection-1234</c> · <c>malfunction-77</c>.
/// 탐지와 장애의 id 공간이 겹쳐도 창이 섞이지 않게 종류를 붙인다. AutomationId 조각으로도 쓰인다.
/// </summary>
public static class EventKeys
{
    public const string DetectionPrefix = "detection";
    public const string MalfunctionPrefix = "malfunction";

    public static string Build(EventWindowKind kind, string? eventId)
        => $"{(kind == EventWindowKind.Malfunction ? MalfunctionPrefix : DetectionPrefix)}-{eventId ?? string.Empty}";

    public static bool TryParse(string? key, out EventWindowKind kind, out string eventId)
    {
        kind = EventWindowKind.Detection;
        eventId = string.Empty;
        if (string.IsNullOrEmpty(key)) return false;
        int dash = key.IndexOf('-');
        if (dash <= 0 || dash == key.Length - 1) return false;
        var prefix = key.Substring(0, dash);
        if (prefix == DetectionPrefix) kind = EventWindowKind.Detection;
        else if (prefix == MalfunctionPrefix) kind = EventWindowKind.Malfunction;
        else return false;
        eventId = key.Substring(dash + 1);
        return true;
    }
}
