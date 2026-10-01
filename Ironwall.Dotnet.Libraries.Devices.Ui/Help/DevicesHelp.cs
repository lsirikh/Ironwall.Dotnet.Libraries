using System.Collections.Generic;
using System.Linq;
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
/// <para>창마다 부분 파일로 나눈다(결선 · 조립기 · 장비/부품/부대/서버 콘솔). 부분 파일의 목록은 메서드로 돌려준다 —
/// 부분 파일 사이의 정적 필드 초기화 순서는 정해져 있지 않다.</para>
/// <para>키 형식: <c>Devices.{창}.{섹션}</c>. 키를 지우거나 바꾸면 화면의 <c>HelpKey</c> 도 같이 — 계약 시험이 고아 · 빠진 키를 잡는다.</para>
/// </remarks>
public static partial class DevicesHelp
{
    public static IReadOnlyList<HelpEntry> Entries { get; } =
        WiringEntries().Concat(AssemblyEntries()).Concat(ConsoleEntries()).ToArray();

#pragma warning disable CA2255 // 라이브러리의 모듈 초기화 — 의도적: 설명 목록은 이 어셈블리의 뷰보다 먼저 있어야 하고 DI 와 무관해야 한다.
    [ModuleInitializer]
    internal static void Register() => HelpCatalog.Register(Entries);
#pragma warning restore CA2255
}
