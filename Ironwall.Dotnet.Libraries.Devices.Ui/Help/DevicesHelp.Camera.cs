using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Help;

/// <summary>카메라 장비 패널.</summary>
public static partial class DevicesHelp
{
    private static IEnumerable<HelpEntry> CameraEntries() => new[]
    {
        HelpEntry.Create("Devices.Camera.OnvifDiscovery", "ONVIF 검색",
                "같은 망에서 ONVIF 검색에 답하는 카메라를 찾습니다.",
                "카메라 계정을 넣고 [조회] 하면 제조사 · 모델 · 영상 주소 · PTZ 여부를 읽습니다.",
                "[추가] 는 카메라 목록에 미저장 행으로 넣습니다. [저장] 해야 서버에 등록됩니다.")
            .With("알아 두기",
                "이미 같은 주소 · 포트로 있는 카메라는 '이미 등록됨' 으로 표시되고 추가되지 않습니다.",
                "검색에 안 나오는 카메라는 [추가] 로 주소를 직접 넣습니다(망이 멀티캐스트를 막으면 검색이 닿지 않습니다).",
                "계정은 조회할 때의 값이 그 행에 저장됩니다."),
    };
}
