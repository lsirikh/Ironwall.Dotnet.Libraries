using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Help;

/// <summary>
/// 보고서 · 조치보고 문구 화면의 "?" 설명 목록(help-callout PRD FR-03 · H-3) — 화면은 키만 걸고 문구는 여기 한 곳에 둔다.
/// </summary>
/// <remarks>
/// <para>등록은 <c>[ModuleInitializer]</c>(이 어셈블리의 형식이 처음 쓰일 때 한 번) — 이유는 <see cref="HelpCatalog"/> 참조.
/// 키를 거는 XAML 은 이 어셈블리 안에만 둔다.</para>
/// <para>잠금 사유 · 실패 · 검색 결과 없음 · 진행 상태는 화면에 남는다(PRD §2). 끌기 면의 키보드 대체 경로는 "키보드로" 묶음에 저절로 붙는다.</para>
/// </remarks>
public static class ReportsHelp
{
    public static IReadOnlyList<HelpEntry> Entries { get; } = new[]
    {
        // ── 보고서 콘솔 — 창 머리 "?" ──
        HelpEntry.Create("Reports.Console", "보고서",
                "왼쪽 레일에서 생성 이력 · 새 보고서 · 템플릿을 고릅니다.",
                "생성 이력에서 보고서를 고르면 미리보기가 열립니다.")
            .With("새 보고서",
                "제목과 기간을 정하고 **[생성]**을 누르세요.",
                "요청한 보고서는 '최근 생성 이력' 목록에서 진행됩니다. 진행 상황도 그 목록에 나옵니다.")
            .With("템플릿",
                "템플릿을 고르면 이름 · 기간 · 구성 요소를 고치는 칸이 열립니다.",
                "새 템플릿은 이름을 적고 구성 요소를 고른 뒤 **[등록]**을 누르세요."),

        // ── 새 보고서 · 심각도 ──
        HelpEntry.Create("Reports.Create.Severity", "심각도",
                "아무것도 고르지 않으면 전 심각도입니다.",
                "심각도는 시스템 이벤트 계열에만 적용됩니다."),

        // ── 템플릿 · 구성 요소 ──
        HelpEntry.Create("Reports.Template.Components", "구성 요소",
                "체크한 구성 요소가 보고서에 들어갑니다.",
                "손잡이를 끌거나, 고른 뒤 {Alt}+{↑} / {Alt}+{↓} 또는 ▲▼ 로 순서를 바꿉니다."),

        // ── 생성 이력 · 되돌릴 수 없는 동작 ──
        HelpEntry.Create("Reports.Generation.Actions", "생성 취소 · 삭제",
                "생성 취소 · 삭제는 확인을 받은 뒤 실행합니다.",
                "**[생성 취소]**는 대기 · 생성 중인 보고서만 됩니다.",
                "**[삭제]**는 보고서 기록과 PDF 파일을 지웁니다. 되돌릴 수 없습니다."),

        // ── 조치보고 문구 — 창 머리 "?" ──
        HelpEntry.Create("Reports.Phrase", "조치보고 문구",
                "조치보고 창과 조치 트레이에서 고르는 문구 목록입니다. 목록 순서대로 보입니다.",
                "새 문구는 **[새 문구]**를 누르고 문구를 입력한 뒤 **[등록]**을 누르세요.")
            .With("순서 바꾸기",
                "문구를 끌거나, 고른 뒤 {Alt}+{↑} / {Alt}+{↓} 또는 오른쪽 ▲▼ 로 순서를 바꿉니다.",
                "**[↶ 순서 되돌리기]**는 마지막 순서 바꾸기 한 번을 되돌립니다.")
            .With("다른 곳에서 바뀌었을 때",
                "다른 곳에서 문구 목록이 바뀌면 바닥 줄에 알림과 **[다시 읽기]**가 뜹니다.",
                "**[다시 읽기]**는 서버의 지금 문구 목록을 다시 읽습니다. 적용하지 않은 문구가 있으면 먼저 적용하거나 되돌려야 합니다."),
    };

#pragma warning disable CA2255 // 라이브러리의 모듈 초기화 — 의도적: 설명 목록은 이 어셈블리의 뷰보다 먼저 있어야 하고 DI 와 무관해야 한다.
    [ModuleInitializer]
    internal static void Register() => HelpCatalog.Register(Entries);
#pragma warning restore CA2255
}
