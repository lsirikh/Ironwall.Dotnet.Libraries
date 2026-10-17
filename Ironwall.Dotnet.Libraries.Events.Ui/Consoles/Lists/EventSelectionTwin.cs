using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;

/// <summary>
/// 두 행 뷰모델이 <b>같은 이벤트</b>인가 — 목록 다시 읽기가 같은 id 의 행을 새 인스턴스로 갈아 끼울 때 고른 행의 짝을 찾는다.
/// </summary>
/// <remarks>
/// 탐지 · 장애 · 연결 · 조치는 id 공간이 겹친다(탐지 5 와 장애 5 는 다른 이벤트) — 그래서 행 형식까지 같아야 짝이다.
/// id 가 없는(0 · 저장 전) 행은 짝이 없다. 순수 함수(헤드리스 시험 대상).
/// </remarks>
public static class EventSelectionTwin
{
    public static bool IsSameEvent(object? candidate, object? original)
    {
        if (candidate is null || original is null) return false;
        if (ReferenceEquals(candidate, original)) return true;
        if (candidate.GetType() != original.GetType()) return false;
        var id = IdOf(candidate);
        return id is > 0 && id == IdOf(original);
    }

    /// <summary>행 뷰모델이 쥔 모델의 서버 id — 모델이 없으면 null.</summary>
    public static int? IdOf(object row)
        => (row.GetType().GetProperty("Model")?.GetValue(row) as IBaseModel)?.Id;
}
