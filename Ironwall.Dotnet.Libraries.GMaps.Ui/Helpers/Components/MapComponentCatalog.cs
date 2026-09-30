using Ironwall.Dotnet.Monitoring.Models.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 지도가 쓰는 부품 유형 한글 이름의 출처 — 서버 카탈로그(<see cref="IComponentTypeLabels"/>, 장비 콘솔 카탈로그 캐시)를
/// <see cref="Lazy{T}"/> 로 받는다(component-display-unify FR-07).
/// </summary>
/// <remarks>
/// <para><b>배선</b>: <c>MapViewModel</c> 이 DI 로 <c>Lazy&lt;IComponentTypeLabels&gt;</c> 를 받으면 <see cref="Use"/> 로 건다
/// (지도 심볼은 DI 로 만들어지지 않아 생성자 주입을 받을 수 없다). 걸린 것이 없으면 컨테이너에서 찾는다.</para>
/// <para><b>실패 · 미준비</b>: 출처가 예외를 던지거나 null 이면 <c>null</c> → 공용 사전의 내장 사전(서버 카탈로그와 같은 한글).
/// 컨테이너가 아직 준비되지 않아 null 이었던 것은 <b>굳히지 않는다</b> — 값을 얻을 때까지 다음에 다시 찾는다(찾기는 싸다).</para>
/// <para>시험은 <see cref="Reset"/> 으로 정적 상태를 되돌린다.</para>
/// </remarks>
public static class MapComponentCatalog
{
    private static readonly object Gate = new();
    private static Lazy<IComponentTypeLabels>? _injected;
    private static IComponentTypeLabels? _resolved;

    /// <summary>지금 쓸 카탈로그. 없으면 <c>null</c>(내장 사전). 예외를 던지지 않는다.</summary>
    public static IComponentTypeLabels? Labels
    {
        get
        {
            Lazy<IComponentTypeLabels>? injected;
            lock (Gate)
            {
                if (_resolved != null) return _resolved;
                injected = _injected;
            }

            IComponentTypeLabels? found;
            try { found = injected != null ? injected.Value : FromContainer(); }
            catch (Exception) { found = null; }   // 출처 실패 — 내장 사전(FR-07)

            if (found != null) lock (Gate) { if (ReferenceEquals(_injected, injected)) _resolved = found; }
            return found;
        }
    }

    /// <summary>출처를 건다(DI 의 암시적 <c>Lazy&lt;IComponentTypeLabels&gt;</c>). null 이면 컨테이너 찾기로 되돌린다.</summary>
    public static void Use(Lazy<IComponentTypeLabels>? source)
    {
        lock (Gate) { _injected = source; _resolved = null; }
    }

    /// <summary>시험용 — 걸린 출처와 찾은 값을 모두 비운다.</summary>
    internal static void Reset() => Use(null);

    private static IComponentTypeLabels? FromContainer()
    {
        try
        {
            return Caliburn.Micro.IoC.GetAllInstances(typeof(IComponentTypeLabels))?.OfType<IComponentTypeLabels>().FirstOrDefault();
        }
        catch (Exception)
        {
            return null;   // 컨테이너 미구성(시험 · 미리보기 · 부팅 이전) — 이번엔 내장 사전, 다음에 다시 찾는다
        }
    }
}
