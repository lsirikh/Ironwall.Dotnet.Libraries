using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>
/// FR-27 보조 — GIS 전역 처리기(DispatcherUnhandledException · UnobservedTaskException · AppDomain)가
/// "팝업 출처" 예외를 알아보고 처리됨으로 표시할 수 있게 하는 순수 판정. 배선은 호스트 앱(GIS 부트스트래퍼).
/// <para>기본 출처는 이 라이브러리 이름공간이다. 팝업 화면을 가진 다른 어셈블리(예: GMaps.Ui 의 지도 오버레이 VM · 컨트롤)는
/// <see cref="AddOrigin"/> 로 <b>팝업 전용</b> 타입 · 이름공간 접두사를 더한다(T-02).</para>
/// <para><b>판정은 던진 곳 기준이다(M3)</b> — 예외가 던져진 가장 안쪽 사용자 코드 프레임(프레임워크 프레임은 건너뜀)이
/// 팝업 출처일 때만 참. 스택 어딘가에 팝업 타입이 끼어 있기만 한 경우(팝업 VM 이 동기로 부른 MapViewModel 이 던짐)는
/// GIS 버그이므로 거짓 — 전역 처리기가 그것을 가리면 안 된다.</para>
/// </summary>
public static class CameraPopupFaults
{
    private const string PopupNamespace = "Ironwall.Dotnet.Libraries.CameraPopup";
    private static readonly object Gate = new();
    private static string[] _origins = { PopupNamespace };

    /// <summary>던진 곳 판정에서 건너뛰는 프레임워크 이름공간(사용자 코드가 아님).</summary>
    private static readonly string[] FrameworkPrefixes =
    {
        "System.", "Microsoft.", "MS.", "Windows.", "Internal.", "Caliburn.", "Autofac.", "Newtonsoft.", "Interop",
    };

    /// <summary>팝업 출처로 볼 팝업 전용 타입 전체 이름 · 이름공간 접두사를 더한다(멱등, 스레드 안전).</summary>
    public static void AddOrigin(string typeOrNamespacePrefix)
    {
        if (string.IsNullOrWhiteSpace(typeOrNamespacePrefix)) return;
        lock (Gate)
        {
            if (Array.IndexOf(_origins, typeOrNamespacePrefix) >= 0) return;
            _origins = _origins.Append(typeOrNamespacePrefix).ToArray();
        }
    }

    /// <summary>
    /// 예외가 팝업 코드에서 던져졌는지. 던지지 않은(스택 없는) 감싸기 예외는 안쪽 예외로 판정하고,
    /// <see cref="AggregateException"/> 은 안쪽이 <b>모두</b> 팝업 출처일 때만 참(GIS 예외가 하나라도 섞이면 가리지 않는다).
    /// </summary>
    public static bool IsPopupOrigin(Exception? exception)
    {
        try
        {
            return Judge(exception, depth: 0) == true;
        }
        catch
        {
            return false;   // 판정 실패는 "팝업 아님" — GIS 의 원래 처리로 간다
        }
    }

    private static bool? Judge(Exception? exception, int depth)
    {
        if (exception is null || depth > 16) return null;
        if (exception is AggregateException agg)
        {
            var inner = agg.InnerExceptions;
            if (inner.Count == 0) return null;
            foreach (var ex in inner)
                if (Judge(ex, depth + 1) != true) return false;
            return true;
        }

        var thrownIn = ThrowSiteType(exception);
        if (thrownIn is not null) return MatchesOrigin(thrownIn);
        return Judge(exception.InnerException, depth + 1);   // 던진 적 없는 감싸기 → 안쪽으로
    }

    /// <summary>예외가 던져진 가장 안쪽 사용자 코드 프레임의 타입 전체 이름. 스택이 없거나 전부 프레임워크면 null.</summary>
    private static string? ThrowSiteType(Exception exception)
    {
        var frames = new StackTrace(exception, fNeedFileInfo: false).GetFrames();
        foreach (var frame in frames)
        {
            var type = frame.GetMethod()?.DeclaringType;
            var name = type?.FullName;
            if (string.IsNullOrEmpty(name) || IsFramework(name)) continue;
            return name;
        }
        return null;
    }

    private static bool IsFramework(string typeName)
    {
        foreach (var prefix in FrameworkPrefixes)
            if (typeName.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool MatchesOrigin(string typeName)
    {
        foreach (var origin in Volatile.Read(ref _origins))
        {
            if (!typeName.StartsWith(origin, StringComparison.Ordinal)) continue;
            if (typeName.Length == origin.Length) return true;
            char next = typeName[origin.Length];
            if (next is '.' or '+' or '`') return true;   // 이름 경계(비슷한 이름의 다른 타입은 제외)
        }
        return false;
    }
}
