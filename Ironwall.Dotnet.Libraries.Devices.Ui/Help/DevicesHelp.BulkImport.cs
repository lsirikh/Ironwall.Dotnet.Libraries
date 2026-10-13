using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Help;

/// <summary>장비 대량 등록(올리기 미리보기 판).</summary>
public static partial class DevicesHelp
{
    private static IEnumerable<HelpEntry> BulkImportEntries() => new[]
    {
        HelpEntry.Create("Devices.BulkImport", "장비 한꺼번에 올리기",
                "파일을 읽기만 했습니다 — 아직 서버에는 아무것도 바뀌지 않았습니다. [등록]을 눌러야 보냅니다.",
                "**신규** 는 새로 만들고, **수정** 은 바뀐 칸(진한 테두리)만 고치고, **같음** 은 건너뜁니다.",
                "**오류** 줄은 붉은 칸에 마우스를 올리면 까닭이 보입니다. 파일을 고쳐 다시 올리거나 '오류 줄 빼고 등록' 을 고르세요.")
            .With("파일 쓰는 법",
                "[양식 받기]로 받은 파일에 한 줄에 한 대씩 적습니다. 첫 줄(칸 이름)과 숨은 둘째 줄은 지우지 마세요.",
                "ID 칸이 비어 있으면 같은 이름의 장비를 고치고, 없으면 새로 만듭니다. ID 를 적으면 그 장비를 고칩니다.",
                "빈 칸은 바꾸지 않습니다. 비밀번호는 내보낸 파일에 들어가지 않습니다 — 바꿀 때만 적으세요.",
                "고르는 칸(종류 · 운영 상태 등)은 상세 칸의 목록 글자나 코드 둘 다 됩니다. Excel 양식에는 고를 수 있는 값 목록이 붙어 있습니다.",
                "센서의 소속 제어기는 제어기 이름 · IP · #번호 중 하나로 적습니다. 같은 파일에 새 제어기를 함께 적으면 제어기를 먼저 만듭니다.",
                "한 번에 2,000줄까지 올릴 수 있습니다.")
            .With("보낸 뒤",
                "실패한 줄은 까닭(입력 거부 · 권한 없음 · 충돌 · 연결 오류)과 함께 남습니다. [실패 줄 다시]는 그 줄만 다시 보냅니다.",
                "[결과 받기]는 줄마다 결과를 CSV 로 저장합니다."),
    };
}
