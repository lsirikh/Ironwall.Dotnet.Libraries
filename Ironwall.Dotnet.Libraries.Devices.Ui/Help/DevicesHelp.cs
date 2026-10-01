using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Help;

/// <summary>
/// 장비 화면의 "?" 설명 목록(help-callout PRD FR-03) — 화면은 키만 걸고 문구는 여기 한 곳에 둔다.
/// </summary>
/// <remarks>
/// <para>등록은 <c>[ModuleInitializer]</c> — 이 어셈블리의 형식이 처음 쓰일 때(= 장비 뷰가 처음 뜰 때) 한 번 돈다.
/// Autofac 모듈 · 부트스트래퍼 순서와 무관하고, 컨테이너 없이 뜨는 시험 창 · 미리보기 도구에서도 같다.</para>
/// <para>표기: <c>{Ctrl}</c> = 단축키 칩, <c>**굵게**</c>. 키를 거는 XAML 은 이 어셈블리 안에만 둔다(뷰가 뜨기 전에 등록이 끝나 있게).</para>
/// </remarks>
public static class DevicesHelp
{
    public static IReadOnlyList<HelpEntry> Entries { get; } = new[]
    {
        HelpEntry.Create("Devices.Wiring.Fence", "펜스 보기 조작",
                "**왼쪽 끌기** = 센서 선택 · {Shift}+끌기 = 망 선택 · {Ctrl} 클릭 = 더함/뺌",
                "센서를 잡고 끌면 빨강 점(망마다 9점)에 맞춰 옮깁니다.",
                "오른쪽 끌기 · 휠 누르고 끌기 = 화면 이동 · 휠 = 확대 · 오른쪽 클릭 = 메뉴")
            .With("키보드로",
                "{Ctrl}+{←}/{→} = 다음 격자 점",
                "{Alt}+{Shift}+{←}/{→} = 다른 망으로",
                "{Alt}+{↑}/{↓} = 한 줄 위 · 아래(하단 · 중단 · 상단 · 윤형 망 펜스센서는 코일)",
                "{Shift}+{Alt}+{↑}/{↓} = 미세 높이 0.1m",
                "{Esc} = 취소"),
    };

#pragma warning disable CA2255 // 라이브러리의 모듈 초기화 — 의도적: 설명 목록은 이 어셈블리의 뷰보다 먼저 있어야 하고 DI 와 무관해야 한다.
    [ModuleInitializer]
    internal static void Register() => HelpCatalog.Register(Entries);
#pragma warning restore CA2255
}
