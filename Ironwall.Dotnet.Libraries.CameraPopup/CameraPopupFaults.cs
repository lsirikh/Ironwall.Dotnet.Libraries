namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>
/// FR-27 보조 — GIS 전역 처리기(DispatcherUnhandledException · UnobservedTaskException · AppDomain)가
/// "팝업 출처" 예외를 알아보고 처리됨으로 표시할 수 있게 하는 순수 판정. 배선은 호스트 앱(GIS 부트스트래퍼).
/// <para>기본 출처는 이 라이브러리 이름공간이다. 팝업 화면을 가진 다른 어셈블리(예: GMaps.Ui 의 지도 오버레이 VM · 컨트롤)는
/// <see cref="AddOrigin"/> 로 타입 · 이름공간 접두사를 더한다(T-02).</para>
/// </summary>
public static class CameraPopupFaults
{
    private const string PopupNamespace = "Ironwall.Dotnet.Libraries.CameraPopup";
    private static readonly object Gate = new();
    private static string[] _origins = { PopupNamespace };

    /// <summary>팝업 출처로 볼 타입 전체 이름 · 이름공간 접두사를 더한다(멱등, 스레드 안전).</summary>
    public static void AddOrigin(string typeOrNamespacePrefix)
    {
        if (string.IsNullOrWhiteSpace(typeOrNamespacePrefix)) return;
        lock (Gate)
        {
            if (Array.IndexOf(_origins, typeOrNamespacePrefix) >= 0) return;
            _origins = _origins.Append(typeOrNamespacePrefix).ToArray();
        }
    }

    /// <summary>예외(내부 예외 포함)가 팝업 코드에서 던져졌는지.</summary>
    public static bool IsPopupOrigin(Exception? exception)
    {
        var origins = Volatile.Read(ref _origins);
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            var declaring = ex.TargetSite?.DeclaringType?.FullName;
            var stack = ex.StackTrace;
            foreach (var origin in origins)
            {
                if (declaring?.StartsWith(origin, StringComparison.Ordinal) == true) return true;
                if (stack?.Contains(origin, StringComparison.Ordinal) == true) return true;
            }
            if (ex is AggregateException agg && agg.InnerExceptions.Any(IsPopupOrigin)) return true;
        }
        return false;
    }
}
