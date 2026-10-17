using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 컨트롤 템플릿 안에서 쓰는 글 — 자동화(UIA) 기본 보기에 나온다.
/// </summary>
/// <remarks>
/// WPF 의 <see cref="TextBlockAutomationPeer"/> 는 <c>TemplatedParent</c> 가 있는 TextBlock 을 기본 보기(Control view)에서 뺀다 —
/// 커널 템플릿의 상세 제목 · 바닥 안내 문구에 AutomationId 를 달아도 트리에 나오지 않았다(2026-09-27 헤디드 시험:
/// "보고서를 만들었습니다" · "설정을 저장했습니다" 등 바닥 줄 문구를 자동화 · 화면 읽기 프로그램이 못 읽음).
/// 운영자에게 보이는 결과 문장은 보조 기술에도 보여야 하므로 템플릿 글은 이것을 쓴다.
/// </remarks>
public class ConsoleText : TextBlock
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ConsoleTextAutomationPeer(this);

    private sealed class ConsoleTextAutomationPeer : TextBlockAutomationPeer
    {
        public ConsoleTextAutomationPeer(TextBlock owner) : base(owner) { }

        // 클래스 이름은 TextBlock 그대로 둔다 — 글을 ClassName 으로 찾는 기존 자동화가 그대로 읽는다.
        protected override bool IsControlElementCore() => true;
    }
}
