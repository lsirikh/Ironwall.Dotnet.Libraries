using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 목록 열 정본 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>서버 목록 열이 그려지는 방식.</summary>
public enum ServerColumnKind
{
    Text,
    /// <summary>고정폭 — 주소처럼 자릿수가 흔들리면 안 되는 값.</summary>
    Mono,
    /// <summary>상태 배지 — 색 말고 글리프(▲ · ● · ○)로도 구분한다.</summary>
    StatusPill,
}

/// <summary>서버 목록 열 하나의 선언.</summary>
/// <param name="Key">사용자 "열" 설정이 저장되는 안정 키 — 한 번 붙이면 바꾸지 않는다.</param>
/// <param name="Header">머리글.</param>
/// <param name="BindingPath"><see cref="ServerRowViewModel"/> 위의 속성 경로.</param>
/// <param name="Kind">렌더 방식.</param>
/// <param name="IsDefault">처음 열었을 때 보이는 기본 열인가.</param>
/// <param name="Width">DIU 고정폭. <c>0</c> 은 별(★) 열.</param>
/// <param name="UnitEraOnly">부대 편제(8.0 이상)에서만 뜻이 있는 열인가 — 그 밖에서는 <b>감춘다</b>(비활성이 아니라).</param>
public sealed record ServerColumnSpec(
    string Key, string Header, string BindingPath, ServerColumnKind Kind, bool IsDefault, double Width, bool UnitEraOnly = false);

/// <summary>
/// 목록 열 정본 — 스토리보드 L1344 의 "이름 · 유형 · 주소 · 상태 · <b>마지막 변화</b> · 부대" 를 그대로 옮겼다.
/// </summary>
/// <remarks>
/// "마지막 변화" 라는 머리글 자체가 계약이다 — "상태 시각" · "최종 확인" 으로 바꾸면 화면이
/// <b>REST 로 생존을 판정한다</b>는 거짓말을 시작한다(L1357).
/// </remarks>
public static class ServerColumnCatalog
{
    public const string LastChangeHeader = "마지막 변화";

    public static IReadOnlyList<ServerColumnSpec> For(bool isUnitEra)
        => All.Where(c => !c.UnitEraOnly || isUnitEra).ToArray();

    /// <summary>계약과 무관한 전체 선언(테스트가 이 순서를 잠근다).</summary>
    public static readonly IReadOnlyList<ServerColumnSpec> All = new[]
    {
        new ServerColumnSpec("name", "이름", nameof(ServerRowViewModel.Name), ServerColumnKind.Text, IsDefault: true, Width: 0),
        new ServerColumnSpec("type", "유형", nameof(ServerRowViewModel.TypeText), ServerColumnKind.Text, IsDefault: true, Width: 110),
        new ServerColumnSpec("address", "주소", nameof(ServerRowViewModel.AddressText), ServerColumnKind.Mono, IsDefault: true, Width: 150),
        new ServerColumnSpec("status", "상태", nameof(ServerRowViewModel.StatusText), ServerColumnKind.StatusPill, IsDefault: true, Width: 112),
        new ServerColumnSpec("last_change", LastChangeHeader, nameof(ServerRowViewModel.LastChangeText), ServerColumnKind.Text, IsDefault: true, Width: 120),
        new ServerColumnSpec("unit", "부대", nameof(ServerRowViewModel.UnitText), ServerColumnKind.Text, IsDefault: true, Width: 110, UnitEraOnly: true),
        // 선택 열 — 기본으로는 상세 칸이 맡는다.
        new ServerColumnSpec("hostname", "호스트명", nameof(ServerRowViewModel.HostnameText), ServerColumnKind.Text, IsDefault: false, Width: 140),
    };
}
