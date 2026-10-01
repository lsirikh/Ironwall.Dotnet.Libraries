using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Help;

/// <summary>결선 창 · 펜스 보기 · 개념도 · 결선 대화창.</summary>
public static partial class DevicesHelp
{
    private static IEnumerable<HelpEntry> WiringEntries() => new[]
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
}
