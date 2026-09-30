namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>
/// FR-27 보조 — GIS 전역 처리기(DispatcherUnhandledException · UnobservedTaskException)가
/// "팝업 출처" 예외를 알아보고 처리됨으로 표시할 수 있게 하는 순수 판정. 배선은 호스트 앱 태스크에서.
/// </summary>
public static class CameraPopupFaults
{
    private const string PopupNamespace = "Ironwall.Dotnet.Libraries.CameraPopup";

    /// <summary>예외(내부 예외 포함)가 팝업 코드에서 던져졌는지.</summary>
    public static bool IsPopupOrigin(Exception? exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex.TargetSite?.DeclaringType?.FullName?.StartsWith(PopupNamespace, StringComparison.Ordinal) == true) return true;
            if (ex.StackTrace?.Contains(PopupNamespace, StringComparison.Ordinal) == true) return true;
            if (ex is AggregateException agg && agg.InnerExceptions.Any(IsPopupOrigin)) return true;
        }
        return false;
    }
}
