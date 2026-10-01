using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Help;

/// <summary>장비 콘솔 · 부대 콘솔 · 서버 콘솔.</summary>
public static partial class DevicesHelp
{
    private static IEnumerable<HelpEntry> ConsoleEntries() => new[]
    {
        // ── 장비 콘솔 ──
        HelpEntry.Create("Devices.Console.Toolbar", "장비 도구줄",
                "[셋업 · 결선] 은 제어기 목록에서 제어기 한 대를 골랐을 때 열립니다.",
                "[장비 배정] 은 그룹 레일에서 그룹 하나를 골랐을 때 열립니다.")
            .With("[조립 · 프리셋] 메뉴 — 부품 모델이 있는 서버에서만",
                "**조립기** — 부품을 조립해 장비를 만들거나 프리셋으로 저장합니다.",
                "**프리셋으로 등록** — 프리셋을 고르고 번호 · 이름 · 접속만 넣으면 부품까지 한 번에 등록합니다.",
                "**프리셋 관리** — 저장한 프리셋을 보고 이름을 바꾸거나 지웁니다."),

        HelpEntry.Create("Devices.Console.Groups", "그룹 칩",
                "목록에서 장비를 고른 뒤 칩을 누르면 그 그룹에 넣습니다.",
                "행 손잡이를 끌어 칩에 놓아도 됩니다. 넣은 직후 [되돌리기] 로 취소합니다.",
                "그룹이 없으면 왼쪽 '그룹'에서 먼저 만드세요."),

        // ── 부대 콘솔 ──
        HelpEntry.Create("Devices.Units", "부대 편제",
                "왼쪽 편제에서 부대를 고르면 오른쪽에 상세가 나옵니다. 새로 만들려면 [부대 등록]을 누르세요.",
                "[이동 되돌리기] — 마지막 이동 1회를 반대로 되돌립니다.",
                "다른 곳에서 편제가 바뀌면 아래 줄에 알림이 뜹니다 — [다시 읽기] 는 서버의 지금 편제로 트리 · 관계도를 새로 그리고, 상세 칸에 적용하지 않은 변경은 그대로 둡니다."),

        HelpEntry.Create("Devices.Units.Tree", "편제 트리",
                "같은 단계의 부대는 코드 순으로 표시됩니다.",
                "손잡이를 끌어 다른 부대 아래로 옮깁니다 — 끄는 중 {Esc} 는 취소입니다.",
                "맨 위 줄에 끌어 놓거나, 부대를 고른 뒤 그 줄을 누르면 최상위로 올립니다."),

        HelpEntry.Create("Devices.Units.Map", "부대 관계도",
                "**계층선** — 상위 · 하위를 잇는 선을 보이거나 감춥니다.",
                "**인접선** — 인접 부대를 잇는 선을 보이거나 감춥니다.",
                "**장비 배지** — 부대마다 소속 장비 수 · 장애 수 배지를 보이거나 감춥니다.",
                "세 가지는 내 화면에만 적용됩니다.",
                "**예하 포함** — [지도에서 보기]가 고른 부대의 예하 부대 장비까지 함께 보입니다."),

        HelpEntry.Create("Devices.Units.Detail.Basic", "기본",
                "**부대 코드**는 등록 후에는 바꿀 수 없습니다 — 신중히 입력하세요.",
                "**운용** — 퇴역은 삭제가 아니라 운용 중지입니다. 운용을 멈추면 이력과 소속은 그대로 남습니다."),

        HelpEntry.Create("Devices.Units.Detail.Parent", "상위 부대",
                "상위를 고르고 [옮기기] 를 누르면 그 부대 아래로 옮깁니다. [최상위로] 는 맨 위로 올립니다.",
                "툴바의 [이동 되돌리기]로 되돌릴 수 있습니다.",
                "트리에서 손잡이로 끌어 옮겨도 같습니다."),

        HelpEntry.Create("Devices.Units.Detail.Adjacent", "인접 부대",
                "인접은 양방향으로 연결됩니다 — 상대 부대의 상세에도 함께 표시됩니다.",
                "부대를 아래 칸에 끌어다 놓거나, 목록에서 고르고 [인접 추가] 를 누릅니다.",
                "칩의 ✕ 는 인접을 끊습니다 — 양쪽에서 함께 사라집니다."),

        // ── 서버 콘솔 ──
        HelpEntry.Create("Devices.Servers", "서버",
                "목록에서 서버를 고르면 접속 정보 · 설정 · 상태가 오른쪽에 나옵니다.",
                "새 서버는 [추가]를 누르세요."),

        HelpEntry.Create("Devices.Servers.Assign", "장비 배정",
                "장비 칩의 손잡이를 끌어 위의 서버 행에 놓거나, 서버 행을 고르고 [배정] 을 누르세요.",
                "배정은 대기 줄에 쌓였다가 [적용] 을 누르면 저장됩니다 — [버리기] 는 대기 줄을 비웁니다.",
                "현재 서버 판에 따라 배정할 수 있는 장비가 다릅니다(옛 판은 스피커만)."),

        HelpEntry.Create("Devices.Servers.Connection", "접속",
                "**호스트명 · 계정** — 비우면 서버에서 지워집니다. 옛 판 서버는 비울 수 없고 다른 값으로 바꾸기만 할 수 있습니다.",
                "**비밀번호** — 비워 두면 바꾸지 않습니다. 새 값을 넣으면 저장할 때 바뀝니다."),

        HelpEntry.Create("Devices.Servers.Status", "상태",
                ServerWriteGuard.STATUS_IS_OBSERVED_NOTE,
                "**마지막 변화** — 상태가 마지막으로 바뀐 시각입니다.",
                "**마지막 수정** — 이름이나 접속 정보를 마지막으로 고친 시각입니다."),
    };
}
