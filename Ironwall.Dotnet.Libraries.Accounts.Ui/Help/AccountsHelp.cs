using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Help;

/// <summary>
/// 계정 · 권한 화면의 "?" 설명 목록(help-callout PRD FR-03 · H-3) — 화면은 키만 걸고 문구는 여기 한 곳에 둔다.
/// </summary>
/// <remarks>
/// <para>등록은 <c>[ModuleInitializer]</c>(이 어셈블리의 형식이 처음 쓰일 때 한 번) — 이유는 <see cref="HelpCatalog"/> 참조.
/// 키를 거는 XAML 은 이 어셈블리 안에만 둔다.</para>
/// <para>잠금 사유 · 입력 범위(예: "0 = 잠그지 않음 · 3~20회") · 오류 · 건수는 화면에 남는다(PRD §2). 끌기 면의 키보드 대체 경로는 "키보드로" 묶음에 저절로 붙는다.</para>
/// </remarks>
public static class AccountsHelp
{
    public static IReadOnlyList<HelpEntry> Entries { get; } = new[]
    {
        // ── 콘솔 전체 — 창 머리 "?" ──
        HelpEntry.Create("Accounts.Console", "계정 · 권한",
                "왼쪽 레일에서 사용자 · 권한 설정 · 세션 관리 · 권한 부여 · 감사 로그 · 세션 설정을 고릅니다.")
            .With("사용자 — 권한 그룹 바꾸기",
                "목록에서 계정을 고른 뒤 아래 '권한 그룹' 칩을 누르면 그 그룹에 넣습니다. 목록의 손잡이를 칩에 끌어 놓아도 됩니다.",
                "그룹 배정은 **[적용]**을 눌러야 저장됩니다.",
                "**[비밀번호 · 사진 변경]**은 비밀번호 초기화와 사진 변경 창을 엽니다. 손댄 칸이 있으면 꺼집니다 — 먼저 적용하거나 되돌리세요.")
            .With("세션 · 감사 로그",
                "세션 관리에서 세션을 고르면 단말 · 만료 시각과 종료 단추가 오른쪽에 나옵니다.",
                "감사 로그에서 기록을 고르면 무엇이 어떻게 바뀌었는지 오른쪽에 나옵니다."),

        // ── 권한 설정 — 그룹 '요약' 절 "?" ──
        HelpEntry.Create("Accounts.Permissions", "권한 그룹",
                "가운데에서 권한 그룹을 고르면 모듈별 권한과 요약이 나옵니다.",
                "열 머리 단추를 누르면 그 열 전체를 켜거나 끕니다. 모듈 줄의 단추는 그 모듈의 권한을 한 번에 켜거나 끕니다.",
                "**▨ 이 모듈에 없는 동작** — 빗금 칸은 그 모듈에 없는 동작입니다.")
            .With("서버 적용(방패)",
                "방패 표시는 서버가 그 제어 권한을 직접 검사한다는 뜻입니다.",
                "방패(서버 적용) 표시가 없는 모듈의 제어 권한은 화면의 단추만 막습니다.")
            .With("구성원",
                "위 '구성원 추가'에서 계정을 고른 뒤 **[추가]**를 누르세요."),

        // ── 권한 부여 — '새 부여' 절 "?" ──
        HelpEntry.Create("Accounts.Grants", "권한 부여",
                "계정과 그룹을 고르고 시작 · 종료 시각을 정한 뒤 **[부여]**를 누릅니다.",
                "종료를 비우면 기한 없이 부여합니다.",
                "목록에서 부여를 고르면 회수할 수 있습니다."),

        // ── 세션 설정 ──
        HelpEntry.Create("Accounts.SessionSetup.Policy", "세션 정책",
                "바꾼 값은 다음 로그인부터 적용됩니다.",
                "칸에서 값을 바꾼 뒤 아래 **[저장]**을 누르세요. 잘못 바꿨으면 **[되돌리기]**를 누르세요."),
        HelpEntry.Create("Accounts.SessionSetup.Concurrency", "동시 로그인",
                "**여러 곳 동시 로그인 허용** — 같은 계정이 여러 곳에서 로그인해 있어도 서로 끊기지 않습니다.",
                "**최대 동시 로그인 수** — 여러 곳 동시 로그인을 허용할 때만 적용됩니다. 이 수를 넘으면 가장 오래된 로그인부터 끊습니다.",
                "**이전 로그인 교체** — 같은 프로그램에서 다시 로그인하면 그 프로그램의 이전 로그인만 끊습니다. 여러 곳 동시 로그인을 허용할 때만 적용됩니다.",
                "**로그인 기록 보존** — 0이면 지우지 않습니다. 그보다 크면 로그아웃 · 만료된 오래된 기록을 정해진 날수가 지난 뒤 지웁니다.",
                "바꾼 값은 다음 로그인부터 적용됩니다."),
    };

#pragma warning disable CA2255 // 라이브러리의 모듈 초기화 — 의도적: 설명 목록은 이 어셈블리의 뷰보다 먼저 있어야 하고 DI 와 무관해야 한다.
    [ModuleInitializer]
    internal static void Register() => HelpCatalog.Register(Entries);
#pragma warning restore CA2255
}
