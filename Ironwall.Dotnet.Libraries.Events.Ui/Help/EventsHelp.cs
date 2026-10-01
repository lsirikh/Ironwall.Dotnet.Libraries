using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Help;

/// <summary>
/// 이벤트 화면(설정 콘솔의 이벤트 절 포함)의 "?" 설명 목록(help-callout PRD FR-03) — 화면은 키만 걸고 문구는 여기 한 곳에 둔다.
/// </summary>
/// <remarks>
/// 등록은 <c>[ModuleInitializer]</c>(이 어셈블리의 형식이 처음 쓰일 때 한 번) — 이유는 <see cref="HelpCatalog"/> 참조.
/// 키를 거는 XAML 은 이 어셈블리 안에만 둔다.
/// </remarks>
public static class EventsHelp
{
    public static IReadOnlyList<HelpEntry> Entries { get; } = new[]
    {
        HelpEntry.Create("Settings.CameraPopup.DoubleClick", "더블클릭 팝업",
            "지도 위 카메라를 더블클릭해 띄운 팝업에 적용됩니다.",
            "**자동 닫기**를 켜면 정한 초가 지난 뒤 팝업이 저절로 닫힙니다.",
            "팝업을 옮긴 자리는 지도 좌표로 기억합니다."),
    };

#pragma warning disable CA2255 // 라이브러리의 모듈 초기화 — 의도적: 설명 목록은 이 어셈블리의 뷰보다 먼저 있어야 하고 DI 와 무관해야 한다.
    [ModuleInitializer]
    internal static void Register() => HelpCatalog.Register(Entries);
#pragma warning restore CA2255
}
