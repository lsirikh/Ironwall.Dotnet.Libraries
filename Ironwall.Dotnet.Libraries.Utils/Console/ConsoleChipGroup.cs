using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 칩 묶음(기간 · 상태 · 유형 칩) — 데이터 목록으로 칩을 찍어 내되, 보조 기술(UIA)에는 <b>칩 자신</b>을 곧바로 내놓는다.
/// </summary>
/// <remarks>
/// <para><b>왜 그냥 <see cref="ItemsControl"/> 이 아닌가</b> — WPF 의 <c>ItemsControlAutomationPeer</c> 는 칩마다 항목 peer
/// (DataItem)를 만들고, 칩(<c>RadioButton</c> · <c>Button</c>)은 그 항목 peer 의 자식으로 매단다. 항목 peer 는 <b>데이터 항목을 열쇠로
/// 재사용</b>되는데, 창을 닫았다 다시 열 때처럼 뷰가 트리에서 떨어졌다가 <b>같은 항목</b>으로 다시 붙으면 재사용된 항목 peer 가
/// 옛 칩(DataContext = <c>{DisconnectedItem}</c>, 화면에 없음)을 자식으로 쥔 채 갱신되지 않는다. 옛 칩은 보이지 않으니 기본 보기
/// (Control view)에서 빠지고, 결과는 "항목은 있는데 자식 0" — 자동화도 화면 읽기도 칩을 찾지 못한다
/// (2026-09-28 헤디드 SC-EVT-013 · SC-EVT-014 · SC-SUP-003. 분리 → 같은 항목 재부착으로 헤드리스 재현).</para>
/// <para>이 묶음의 peer 는 항목 peer 층을 두지 않고 <b>시각 트리의 칩 peer 를 그대로</b> 자식으로 낸다
/// (<see cref="FrameworkElementAutomationPeer"/>). 칩 peer 는 칩 요소에 붙어 있어 컨테이너가 바뀌면 새 칩의 peer 로 바뀐다 —
/// 툴바의 다른 단추가 늘 보이는 것과 같은 길이다. 칩의 토글 · 선택 패턴도 그대로 남는다.</para>
/// <para>AutomationId 는 묶음에 단 그대로(예: <c>Console.Events.Toolbar.Period</c>), 칩 id 도 그대로다. 종류는 목록이 아니라
/// <c>Group</c> 이다 — 칩 묶음은 고를 항목의 목록이 아니라 단추 모음이다.</para>
/// </remarks>
public class ConsoleChipGroup : ItemsControl
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ConsoleChipGroupAutomationPeer(this);

    private sealed class ConsoleChipGroupAutomationPeer : FrameworkElementAutomationPeer
    {
        public ConsoleChipGroupAutomationPeer(ConsoleChipGroup owner) : base(owner) { }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        // 파생(ConsoleChipStrip)은 제 이름으로 읽힌다
        protected override string GetClassNameCore() => Owner.GetType().Name;
    }
}
