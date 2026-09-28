using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 콘솔 트리(ListBox) 선택 → 뷰모델 선택 다리
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 트리 목록의 <c>SelectionChanged</c> 를 <see cref="UnitConsoleViewModel.SelectRowAsync"/> 로 옮긴다.
/// 뷰(<see cref="UnitConsoleView"/>)와 시험이 같은 몸통을 쓰도록 뷰 밖으로 꺼냈다.
/// </summary>
/// <remarks>
/// <para><b>재조회의 행 갈아 끼우기는 사람의 선택 해제가 아니다.</b> 편제를 다시 읽으면 뷰모델이 행 인스턴스를 전부 새로 만들고
/// (고른 부대는 새 인스턴스로 조용히 옮겨 둔다), 옛 인스턴스가 목록에서 빠지는 순간 WPF <see cref="Selector"/> 가 선택을 비우며
/// <c>SelectionChanged(null)</c> 를 올린다. 이것을 그대로 옮기면 <b>아직 있는 부대</b>의 선택이 풀려
/// [옮기기] · [최상위로] 가 꺼지고, SYNC_UNIT 재조회 뒤에는 "고른 부대가 다른 곳에서 삭제되었습니다" 가 떴다
/// (헤디드 3회차 SC-UNT-022 · SC-UNT-008 — 우리 쓰기의 SYNC_UNIT 메아리가 재조회를 부른 뒤부터 매번).</para>
/// <para>목록 선택을 되돌릴 때는 <see cref="System.Windows.DependencyObject.SetCurrentValue"/> 를 쓴다 —
/// <c>SelectedItem = x</c> 로 로컬 값을 쓰면 <c>SelectedItem="{Binding SelectedRow, Mode=OneWay}"</c> 결선이 끊겨
/// 그 뒤 뷰모델의 선택(재조회 · 지도에서 보기)이 목록에 닿지 않는다.</para>
/// </remarks>
internal sealed class UnitTreeSelectionBridge
{
    private bool _syncing;

    public async Task OnSelectionChangedAsync(Selector list, SelectionChangedEventArgs e, UnitConsoleViewModel? vm)
    {
        if (_syncing || vm is null) return;

        var wanted = list.SelectedItem as UnitNodeRowViewModel;
        if (wanted is null && IsRowInstanceSwap(list, e, vm.SelectedRow))
        {
            // 뷰모델은 이미 새 인스턴스를 고르고 있다 — 넘기지 않고, 목록만 그 인스턴스로 되돌린다(재조회의 통지가 먼저 오면 할 일이 없다).
            var current = vm.SelectedRow;
            _ = list.Dispatcher.InvokeAsync(() =>
            {
                if (list.SelectedItem is not null || !ReferenceEquals(vm.SelectedRow, current) || !list.Items.Contains(current)) return;
                SetListSelection(list, current);
            }, DispatcherPriority.DataBind);
            return;
        }

        await vm.SelectRowAsync(wanted);

        // 관문이 거절했으면 목록 선택을 되돌린다 — 되돌리지 않으면 목록은 B 를 가리키는데 상세는 A 를 보이는 어긋남이 남는다.
        var actual = vm.SelectedRow;
        if (ReferenceEquals(actual, wanted)) return;
        SetListSelection(list, actual);
    }

    /// <summary>
    /// 선택이 비워진 까닭이 <b>고른 행이 목록에서 빠져서</b>이고, 뷰모델이 고른 행은 <b>여전히 목록에 있다</b>(새 인스턴스) — 재조회의 행 갈아 끼우기다.
    /// </summary>
    /// <remarks>
    /// 필터가 고른 행을 가린 경우(뷰모델의 행도 목록에 없다)와 고른 부대가 정말 사라진 경우(뷰모델 선택이 이미 비었다)는 아니다 — 종전대로 넘긴다.
    /// </remarks>
    internal static bool IsRowInstanceSwap(Selector list, SelectionChangedEventArgs e, UnitNodeRowViewModel? current)
        => current is not null
           && list.Items.Contains(current)
           && e.RemovedItems.Count > 0
           && e.RemovedItems.Cast<object>().All(removed => !list.Items.Contains(removed));

    private void SetListSelection(Selector list, object? item)
    {
        _syncing = true;
        try { list.SetCurrentValue(Selector.SelectedItemProperty, item); }
        finally { _syncing = false; }
    }
}
