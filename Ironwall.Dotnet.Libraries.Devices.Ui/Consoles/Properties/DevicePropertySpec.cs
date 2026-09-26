using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
/****************************************************************************
   Purpose      : 정규화된 장비 속성 명세 — 상세 폼이 이 계약에서 생성된다 (device-console-v8 FR-07~FR-09)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>상세 폼의 절(섹션) — 이 순서가 곧 화면 순서다(FR-08, 고정). 타입 특화 설정은 맨 밑.</summary>
public enum DevicePropertySection
{
    /// <summary>장비 그룹 폼 전용 절(이름 · 설명 · 장비 수) — 장비 폼에는 나오지 않는다.</summary>
    GroupInfo,
    Common,
    Connection,
    HardwareSpec,
    Components,
    DeviceStatus,
    Location,
    Groups,
    DeviceConfig,
    Extra,
}

/// <summary>속성 한 칸을 어떤 입력기로 그릴지 — 편집기 종류가 검증·직렬화 방식을 함께 정한다.</summary>
public enum DevicePropertyEditor
{
    Text,
    Integer,
    Number,
    Boolean,
    Choice,
    Password,
    ReadOnly,
}

/// <summary>
/// 쓰기 가능 등급. <see cref="No"/> 는 항상 <see cref="DevicePropertySpec.LockReason"/> 을 동반한다 —
/// "막혀 있다"만으로는 다음 세션의 내가 왜 막았는지 알 수 없다.
/// </summary>
public enum DevicePropertyWritable
{
    Yes,
    CreateOnly,
    No,
}

/// <summary>선택지(콤보·라디오)의 출처 — 코드에 목록을 박지 않고 "어디서 가져오는가"만 선언한다.</summary>
public enum DevicePropertyOptionSource
{
    None,
    /// <summary>카테고리별 종류축(<c>type_&lt;category&gt;</c>) — <c>ICatalogService.TypeAxis</c>.</summary>
    TypeAxis,
    /// <summary>스피커 <c>speaker_role</c> 같은 추가 축 — <c>ICatalogService.ExtraAxes</c>.</summary>
    ExtraAxis,
    /// <summary>이름표(<see cref="DevicePropertySpec.VocabularyName"/>)로 찾는 일반 카탈로그 어휘.</summary>
    CatalogVocabulary,
    /// <summary>클라 고정 enum(<see cref="DevicePropertySpec.EnumType"/>) — 서버 카탈로그가 필요 없는 값.</summary>
    ClrEnum,
    /// <summary>제어기 목록에서 고른다(센서의 소속 제어기).</summary>
    Controllers,
    /// <summary>서버 목록에서 고른다(스피커의 관리 서버).</summary>
    Servers,
    /// <summary>부대 목록에서 고른다(<c>unit_id</c>, 서버 8.0+) — 값은 부대 id, 표시는 부대 이름.</summary>
    Units,
    /// <summary>클라 고정 표시 사전(<see cref="DevicePropertySpec.FixedOptions"/>) — 저장 값은 코드, 화면은 한국어.</summary>
    Fixed,
}

/// <summary>축 값 부분 수정(<see cref="DevicePropertySpec.AxisWritePath"/>)으로 보낼 때의 JSON 값 형.</summary>
public enum DeviceAxisValueKind
{
    Text,
    Integer,
    Number,
    Boolean,
}

/// <summary>
/// 장비 속성 한 개의 정규화된 명세 — 키 · 라벨 · API 경로 · 절 · 편집기 · 쓰기 가능 · 적용 카테고리 ·
/// 값 출처(뷰모델 속성 또는 축 리더)를 한 줄에 담는다(FR-07).
/// </summary>
/// <remarks>
/// <para><b>왜 값 출처가 둘인가</b> — 오늘 편집 가능한 칸(장비번호·이름·IP…)은 행 뷰모델의 공개 속성이
/// 정본이라 <see cref="ViewModelPath"/> 로 읽고 쓴다. v7.0+ 축(<c>connection</c>·<c>hardware_spec</c>…)은
/// 아직 쓰기 경로가 없어 읽기 전용인데, 축은 뷰모델이 아니라 <c>IBaseDeviceModel.Axes</c> 에 있다 —
/// 그래서 <see cref="AxisReader"/> 로 모델에서 직접 표시 문자열을 뽑는다. 한 칸이 둘 다 갖는 일은 없다.</para>
/// <para><b>왜 잠긴 속성도 목록에 싣는가</b> — "이 속성이 왜 안 보이지?"를 막으려는 것이다. 못 쓰는 칸도
/// 화면에 나타나 <see cref="LockReason"/> 을 보이면, 사용자가 헤매지 않고 "왜 막혔는지"를 바로 안다.</para>
/// </remarks>
public sealed record DevicePropertySpec
{
    /// <summary>안정적인 식별자 — (카테고리, 계약) 조합 안에서 유일하다. 예: "name_device", "connection.ip_address".</summary>
    public required string Key { get; init; }

    /// <summary>화면에 뜨는 한글 라벨(스토리보드 원문).</summary>
    public required string Label { get; init; }

    /// <summary>서버 요청/응답에서의 경로 — 표시·진단용. 실제 전송은 기존 저장 경로(NFR-02)가 한다.</summary>
    public required string ApiPath { get; init; }

    public required DevicePropertySection Section { get; init; }

    public required DevicePropertyEditor Editor { get; init; }

    public DevicePropertyWritable Writable { get; init; } = DevicePropertyWritable.Yes;

    /// <summary><see cref="Writable"/> 가 <see cref="DevicePropertyWritable.Yes"/> 가 아니면 반드시 있어야 하는 한글 사유.</summary>
    public string? LockReason { get; init; }

    /// <summary>이 속성을 갖는 카테고리들. 같은 <see cref="Key"/> 라도 카테고리가 겹치지 않으면 별개 명세로 쪼갤 수 있다
    /// (예: 종류축은 생성 필수 여부가 카테고리마다 달라 두 명세로 나뉜다).</summary>
    public required IReadOnlyCollection<EnumDeviceCategory> Categories { get; init; }

    /// <summary>행 뷰모델의 공개 속성 이름(단일 레벨). <c>null</c> 이면 <see cref="AxisReader"/> 로만 읽는다.</summary>
    public string? ViewModelPath { get; init; }

    /// <summary><c>model.Axes</c> 에서 표시 문자열을 뽑는 함수. <see cref="ViewModelPath"/> 가 없을 때만 쓰인다.</summary>
    public Func<IBaseDeviceModel, string?>? AxisReader { get; init; }

    /// <summary>
    /// 이 값이 속한 축 절 이름 — "connection" · "hardware_spec" · "components" · "device_status" · "device_config".
    /// 폼이 <c>IDeviceAxesModel.IsSectionReceived</c> 로 "미수신"을 판단할 때 쓴다. 축과 무관한 속성은 <c>null</c>.
    /// </summary>
    public string? AxisSection { get; init; }

    public bool IsRequiredOnCreate { get; init; }

    /// <summary>여러 행을 한꺼번에 편집할 때 이 칸도 같이 바뀌어도 되는가. 식별 칸(장비번호)은 <c>false</c>.</summary>
    public bool AllowMultiEdit { get; init; } = true;

    public DevicePropertyOptionSource OptionSource { get; init; } = DevicePropertyOptionSource.None;

    /// <summary><see cref="DevicePropertyOptionSource.CatalogVocabulary"/> 또는 <see cref="DevicePropertyOptionSource.ExtraAxis"/> 일 때의 어휘 이름.</summary>
    public string? VocabularyName { get; init; }

    /// <summary><see cref="DevicePropertyOptionSource.ClrEnum"/> 일 때의 enum 타입.</summary>
    public Type? EnumType { get; init; }

    /// <summary>v7.0+ 축 계약에서만 보인다(6.3 에서는 감춘다).</summary>
    public bool AxisContractOnly { get; init; }

    /// <summary>6.3 레거시 계약에서만 보인다(v7.0+ 에서는 감춘다).</summary>
    public bool LegacyContractOnly { get; init; }

    public double? Min { get; init; }

    public double? Max { get; init; }

    public int? MaxLength { get; init; }

    /// <summary>추가 설명(선택) — 폼이 도움말로 보일 수 있다.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// 이 칸을 행 뷰모델이 아니라 <b>축 값 부분 수정</b>(<c>PATCH /devices/{종류}/{id}</c>)으로 보내는 경로 —
    /// 점으로 이은 본문 경로다(예: <c>connection.channel</c> · <c>hardware_spec.model</c> ·
    /// <c>device_config.thresholds.temperature.high</c> · <c>unit_id</c>). <c>null</c> 이면 기존 저장 경로(행 뷰모델)를 탄다.
    /// </summary>
    /// <remarks>
    /// 값은 <see cref="AxisReader"/> 로 읽는다 — 그래서 <see cref="AxisReader"/> 는 표시 문장이 아니라 <b>저장 값 그대로</b>
    /// (코드 · 숫자)를 돌려줘야 한다. 한국어 표시는 선택지(<see cref="FixedOptions"/> 등)가 맡는다.
    /// </remarks>
    public string? AxisWritePath { get; init; }

    /// <summary><see cref="AxisWritePath"/> 로 보낼 때의 JSON 값 형.</summary>
    public DeviceAxisValueKind AxisValueKind { get; init; } = DeviceAxisValueKind.Text;

    /// <summary>빈 칸을 "지운다"(JSON <c>null</c>)로 보내도 되는가. 서버가 NOT NULL 로 받는 칸(접속 방식 · 카메라 프로토콜 · 부대)은 <c>false</c>.</summary>
    public bool AxisAllowsClear { get; init; } = true;

    /// <summary>
    /// 잠긴 칸의 까닭(<see cref="LockReason"/>)을 화면에 띄울 것인가. 표시 전용 요약(장비 링크 · 부품별 설정)처럼
    /// 운영자에게 할 말이 없는 칸은 <c>false</c> — 까닭은 명세 문서로만 남는다.
    /// </summary>
    public bool ShowLockReason { get; init; } = true;

    /// <summary>값이 비었을 때 읽기 전용 표시로 보일 글(예: 소속 부대 "미배치"). <c>null</c> 이면 빈 칸.</summary>
    public string? EmptyDisplay { get; init; }

    /// <summary><see cref="DevicePropertyOptionSource.Fixed"/> 일 때의 선택지 — (저장 코드, 한국어 표시).</summary>
    public IReadOnlyList<(string Code, string Display)>? FixedOptions { get; init; }
}
