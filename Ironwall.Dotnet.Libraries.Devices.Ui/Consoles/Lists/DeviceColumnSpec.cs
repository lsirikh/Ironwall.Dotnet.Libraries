namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Lists;
/****************************************************************************
   Purpose      :
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 콘솔 목록 DataGrid 열이 화면에서 어떻게 그려지는가 — 렌더러가 이 값으로 <c>DataTemplate</c> 을 고른다.
/// </summary>
/// <remarks>
/// 목업(<c>window-layout-system-storyboard.html</c> <c>cell()</c>)의 렌더 분기를 4갈래로 요약한 것이다 —
/// 상태 배지(<c>pill</c>) · 고정폭 숫자/식별자(<c>mono</c>) · 체크(<c>chk</c>) · 나머지 일반 텍스트.
/// </remarks>
public enum DeviceColumnKind
{
    /// <summary>일반 텍스트 — 좌측 정렬, 가변폭.</summary>
    Text,
    /// <summary>고정폭 폰트 — IP:포트·장비번호처럼 자릿수가 흔들리면 안 되는 값(목업 <c>class="m"</c>).</summary>
    Mono,
    /// <summary>상태 배지 — 색+모양으로 상태를 드러낸다(목업 <c>pill()</c>/<c>compPill()</c>).</summary>
    StatusPill,
    /// <summary>체크 표시 — 활성화 여부처럼 이진값(목업 <c>class="chk"</c>).</summary>
    Check,
}

/// <summary>
/// 콘솔 목록 DataGrid 열 하나의 <b>선언</b> — 실제 <c>DataGridColumn</c> 은 이 값을 읽어 조립한다(N03 몫).
/// </summary>
/// <remarks>
/// <para><b>계약은 고정이다.</b> 이 파일을 만든 세션(N02-B) 밖에서 이 레코드의 멤버를 늘리거나 바꾸면
/// 동시에 N02-A(레일 카운터 소비)·N03(그리드 조립)가 깨진다. 새 축이 필요하면 이 기록을 새로 정의하지 말고
/// PRD 를 먼저 갱신한다.</para>
/// <para><see cref="AxisContractOnly"/>·<see cref="LegacyContractOnly"/> 는 <b>동시에 참일 수 없다</b>
/// (둘 다 거짓이면 계약 무관 — 두 계약 모두에 나온다). <see cref="DeviceColumnCatalog.For"/> 가 호출 시점의
/// <c>isAxisContract</c> 값으로 맞지 않는 쪽을 걸러 반환하므로, 이 두 플래그가 동시에 켜진 채 밖으로
/// 나가는 일은 없다 — 즉 호출자는 "계약 유출"을 걱정하지 않고 반환값을 그대로 그린다.</para>
/// </remarks>
/// <param name="Key">
/// 안정 식별자 — 사용자별 "열" 설정(보이기/숨기기·순서 기억)이 이 값으로 저장된다.
/// 리네임하면 저장된 사용자 설정이 끊어지므로 한 번 붙이면 바꾸지 않는다.
/// </param>
/// <param name="Header">그리드 헤더에 그대로 찍히는 한글 라벨(목업 표기를 따른다).</param>
/// <param name="BindingPath">행 뷰모델(카테고리별 <c>*DeviceViewModel</c> 또는 <see cref="Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.DeviceGroupViewModel"/>) 위의 속성 경로.</param>
/// <param name="Kind">렌더 방식 — <see cref="DeviceColumnKind"/>.</param>
/// <param name="IsDefault">창을 처음 열었을 때 보이는 기본 6열(그룹은 3열) 중 하나인가.</param>
/// <param name="Width">DIU 고정폭. <c>0</c> 은 "별(★) 열" 표식 — 카테고리마다 정확히 하나(<c>name</c>)만 갖는다.</param>
/// <param name="AxisContractOnly">v7.0+ 축 계약(<c>type_&lt;category&gt;</c> 등)에서만 유효 — 6.3 계약에서는 걸러진다.</param>
/// <param name="LegacyContractOnly">6.3 계약에서만 유효 — v7.0+ 에서 제거된 필드(카메라 <c>mode</c>·<c>category</c>·<c>is_record</c>, <c>version</c>)를 옛 화면에만 남긴다.</param>
public sealed record DeviceColumnSpec(
    string Key,
    string Header,
    string BindingPath,
    DeviceColumnKind Kind,
    bool IsDefault,
    double Width,
    bool AxisContractOnly = false,
    bool LegacyContractOnly = false);
