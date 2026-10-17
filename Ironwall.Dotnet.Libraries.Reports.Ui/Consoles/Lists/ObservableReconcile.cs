using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Lists;

/// <summary>
/// 보이는 목록을 <b>제자리에서</b> 맞춘다 — <c>Clear()</c> 하고 다시 채우지 않는다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: <c>Clear()</c> 는 묶인 <c>DataGrid</c> 의 선택을 그 자리에서 <c>null</c> 로 만든다.
/// 검색 글자를 한 자 칠 때마다 선택이 풀리고 상세 칸(미리보기)이 비워졌다 — 고르고 검색하는 흔한 순서가
/// 곧바로 깨졌다. 지우고 · 끼우고 · 옮기면 남는 줄의 선택은 그대로다.</para>
/// <para>같음 판정은 <b>인스턴스</b>다(서버에서 새로 받은 줄은 다른 인스턴스이므로 실제로 갈린다 —
/// 그때의 선택 복원은 Id 로 다시 고르는 쪽이 맡는다).</para>
/// </remarks>
public static class ObservableReconcile
{
    public static void Apply<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
    {
        // ① 빠진 줄을 뒤에서부터 뺀다.
        for (var i = target.Count - 1; i >= 0; i--)
            if (!desired.Contains(target[i]))
                target.RemoveAt(i);

        // ② 자리를 맞춘다 — 없으면 끼우고, 어긋나면 옮긴다.
        for (var i = 0; i < desired.Count; i++)
        {
            var item = desired[i];
            var at = target.IndexOf(item);
            if (at < 0) target.Insert(i, item);
            else if (at != i) target.Move(at, i);
        }

        // ③ 꼬리에 남은 것이 있으면 자른다(있을 수 없지만 방어).
        while (target.Count > desired.Count) target.RemoveAt(target.Count - 1);
    }
}
