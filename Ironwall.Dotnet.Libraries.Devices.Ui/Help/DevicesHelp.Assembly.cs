using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Help;

/// <summary>부품 조립기 · 반복 펼치기 · 프리셋 관리 · 프리셋으로 장비 만들기.</summary>
public static partial class DevicesHelp
{
    private static IEnumerable<HelpEntry> AssemblyEntries() => new[]
    {
        HelpEntry.Create("Devices.Assembly", "부품 조립기",
                "**저장하기 전에는 아무것도 바뀌지 않습니다.** 바닥 줄에 부품 수와 미저장 변경 수가 보입니다.",
                "[등록…] — 번호 · 이름만 입력하면 한 번에 등록됩니다.",
                "[적용] — 저장 전에 최신 상태와 비교합니다.",
                "[프리셋 저장] — 이 PC의 프리셋에 저장합니다."),

        HelpEntry.Create("Devices.Assembly.Palette", "부품 팔레트",
                "이 카테고리에 달 수 있는 부품만 보입니다.",
                "손잡이 ⣿ 를 끌어 보드에 놓거나, 고른 뒤 {Enter} 로 보드 끝에 다세요.",
                "검색 칸은 유형 · 이름으로 찾습니다."),

        HelpEntry.Create("Devices.Assembly.Board", "조립 보드",
                "손잡이 ⣿ 를 끌어 순서를 바꿉니다.",
                "팔레트 블록을 끌어 놓으면 그 자리에 들어갑니다(팔레트에서 {Enter} 를 누르면 끝에 답니다).",
                "**빼는 곳**에 블록을 끌어 놓으면 빠집니다 — [되돌리기]로 다시 넣을 수 있습니다.",
                "끄는 중 {Esc} 를 누르면 제자리로 돌아갑니다."),

        HelpEntry.Create("Devices.Assembly.Inspector", "속성",
                "보드에서 블록 하나를 고르면 그 부품 정보를 고칠 수 있습니다.",
                "아래 묶음(이 부품 정보 · 이 부품 설정 · 부품 유형 정보)마다 \"?\" 에 그 묶음의 설명이 있습니다."),

        HelpEntry.Create("Devices.Assembly.Inspector.Slot", "이 부품 정보",
                "이 장비에 단 부품 하나의 사실입니다.",
                "유형은 바꿀 수 없습니다 — 바꾸려면 빼고 다시 다세요.",
                "**일련번호**는 프리셋에 담기지 않습니다 — 그 장비만의 값입니다."),

        HelpEntry.Create("Devices.Assembly.Inspector.Overrides", "이 부품 설정",
                "비워 두면 유형 기본값을 씁니다.",
                "부품을 빼면 이 설정도 함께 지워집니다."),

        HelpEntry.Create("Devices.Assembly.Inspector.Catalog", "부품 유형 정보",
                "같은 유형의 부품이 모두 함께 쓰는 값입니다(읽기 전용).",
                "**보고** — 동작 상태를 보고하는 유형인지, 정상 · 고장 여부만 보고하는 유형인지 보여 줍니다."),

        HelpEntry.Create("Devices.Assembly.CategoryPresets", "이 카테고리의 프리셋",
                "프리셋을 누르면 보드에 올립니다 — 지금 보드를 바꿉니다.",
                "[되돌리기] 한 번으로 돌아올 수 있습니다.",
                "프리셋을 만들려면 보드를 조립한 뒤 [프리셋으로 저장]을 누르세요."),

        HelpEntry.Create("Devices.Assembly.Repeat", "반복 펼치기",
                "규칙적인 부품을 한 번에 만듭니다 — 유형 · 개수 · 시작 채널을 정하세요.",
                "식별 이름 규칙: {02d} = 두 자리 번호 · {d} = 그대로.",
                "아래 미리보기에서 빨간 줄은 겹치거나 잘못된 줄입니다."),

        HelpEntry.Create("Devices.Assembly.PresetManager", "프리셋 관리",
                "프리셋은 구조(부품 · 설정)만 담습니다 — 번호 · 이름 · 접속 · 일련번호는 담지 않습니다.",
                "프리셋은 이 PC에만 저장됩니다 — 다른 PC로 옮기려면 [내보내기]를 쓰세요."),

        HelpEntry.Create("Devices.Assembly.Register", "프리셋으로 장비 만들기",
                "프리셋이 부품 구성을 채웁니다 — 여기서는 그 장비만의 값(번호 · 이름 · 접속)을 입력하세요.",
                "[등록]을 누르면 오른쪽 \"등록할 내용\"으로 장비를 만듭니다.",
                "요청 본문(JSON)은 [요청 본문 보기]를 펼치면 보입니다."),
    };
}
