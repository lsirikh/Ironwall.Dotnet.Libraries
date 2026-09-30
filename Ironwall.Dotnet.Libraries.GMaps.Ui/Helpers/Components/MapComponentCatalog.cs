using Ironwall.Dotnet.Monitoring.Models.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 지도가 쓰는 부품 유형 한글 이름의 출처 — 서버 카탈로그(<see cref="IComponentTypeLabels"/>, 장비 콘솔 카탈로그 캐시)를
/// <see cref="Lazy{T}"/> 로 받는다(component-display-unify FR-07).
/// </summary>
/// <remarks>
/// <para>지도 심볼은 DI 로 만들어지지 않는다(마커 수백 개를 뷰모델이 직접 만든다) — 그래서 생성자 주입 대신 처음 쓸 때 한 번
/// 컨테이너에서 찾는다. 등록이 없거나(장비 콘솔 모듈 미구성 · 시험) 찾다가 실패하면 <c>null</c> → 공용 사전의 내장 사전으로
/// 떨어진다(<see cref="ComponentDisplay.TypeName"/>). 카탈로그가 아직 안 읽혔을 때도 같은 폴백이다(내장 사전 = 서버 카탈로그와 같은 한글).</para>
/// <para>시험 · 호스트는 <see cref="Use"/> 로 출처를 바꿀 수 있다.</para>
/// </remarks>
public static class MapComponentCatalog
{
    private static Lazy<IComponentTypeLabels?> _source = CreateDefault();

    /// <summary>지금 쓸 카탈로그. 없으면 <c>null</c>(내장 사전). 예외를 던지지 않는다.</summary>
    public static IComponentTypeLabels? Labels
    {
        get
        {
            try { return _source.Value; }
            catch (Exception) { return null; }
        }
    }

    /// <summary>출처를 바꾼다(시험 · 호스트 배선). null 이면 기본(컨테이너에서 찾기)으로 되돌린다.</summary>
    public static void Use(Lazy<IComponentTypeLabels?>? source) => _source = source ?? CreateDefault();

    private static Lazy<IComponentTypeLabels?> CreateDefault() => new(() =>
    {
        try
        {
            return Caliburn.Micro.IoC.GetAllInstances(typeof(IComponentTypeLabels))?.OfType<IComponentTypeLabels>().FirstOrDefault();
        }
        catch (Exception)
        {
            return null;   // 컨테이너 미구성(시험 · 미리보기) — 내장 사전
        }
    });
}
