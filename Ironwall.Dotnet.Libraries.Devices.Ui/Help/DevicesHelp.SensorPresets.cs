using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Help;

/// <summary>센서 모델 창 · 위치 프리셋 편집기 · 속성 프리셋 · 결선맵 설치 위치(sensor-mount-preset NFR-02 — 설명은 화면이 아니라 "?" 에).</summary>
public static partial class DevicesHelp
{
    private static IEnumerable<HelpEntry> SensorPresetEntries() => new[]
    {
        HelpEntry.Create("Devices.SensorPresets.Models", "센서 모델",
                "센서 한 대는 **모델 + 위치 프리셋 + 따로 고친 값**으로 섭니다.",
                "회사 기본 7종은 지울 수 없고 숨기기만 됩니다. 추가한 모델은 쓰는 센서가 없을 때만 지울 수 있습니다.",
                "★기본 프리셋은 모델마다 하나 — 새로 놓는 센서가 이 자리로 섭니다.",
                "[내보내기] · [가져오기] 로 다른 PC(현장)와 맞춥니다. 같은 이름의 프리셋은 바뀌고 새 이름은 더해집니다."),

        HelpEntry.Create("Devices.SensorPresets.MountEditor", "위치 프리셋 편집",
                "아이콘을 끌면 가장 가까운 **붙는 점**(회색 점)에 붙습니다. 붙을 점은 **빨간 점 + 고리**로 바뀌고 위에 \"… 에 붙음\" 이 뜹니다.",
                "{Alt} 를 누른 채 끌면 붙지 않고 1 cm 단위로 자유롭게 놓입니다. {Esc} 는 끄는 중이면 원래 자리로 돌립니다.",
                "키보드: {Tab} 다음 붙는 점 · {Shift+Tab} 이전 · 화살표 10 cm · {Shift}+화살표 1 cm · 오른쪽 수치 칸에 바로 적기.",
                "위에서 본 모습의 파란 손잡이를 돌려 **바라보는 방향**을 정합니다 — 15° 단위({Alt} = 1°).",
                "철조망이 있는 칸이면 철조망 위 · 가운데 · 기둥 쪽 끝 고리에도 붙을 수 있습니다.",
                "이미 쓰는 센서가 있으면 저장 전에 몇 대가 옮겨지는지 묻습니다 — 원치 않으면 [새 프리셋으로 저장]."),

        HelpEntry.Create("Devices.SensorPresets.Attributes", "속성 프리셋",
                "모델 값(탐지 거리 · 탐지 각 · 권장 설치 간격 …)을 키-값으로 둡니다.",
                "쓰임이 **그림 · 알림**인 값은 결선맵이 읽습니다(탐지 범위 · 권장 간격 넘는 곳). **참고**는 표시만 합니다.",
                "장비 자체 설정(서버)에는 보내지 않습니다."),

        HelpEntry.Create("Devices.Wiring.MountPreset", "설치 위치",
                "센서는 위치 프리셋 자리로 서고, 여기서 고친 값은 **따로 고친 값**(주황)으로 남습니다.",
                "[프리셋으로] — 따로 고친 값을 지웁니다. [이 값으로 새 프리셋] — 지금 자리를 새 프리셋으로 저장합니다.",
                "[위치 프리셋 적용…] — 고른 센서 · 같은 모델 전체 · 이 제어기 전체에 한 번에 적용합니다.",
                "저장은 결선맵 [저장하기] 하나로 — 되돌리기에 들어갑니다."),
    };
}
