using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 이벤트 맵핑 워크벤치 — 계약(종류 축 · 장비 공급 · 서버 왕복)
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>액션 보드의 세 축. 탭·레일·드롭존이 전부 이 값 하나를 본다.</summary>
public enum MappingActionKind
{
    /// <summary>카메라 배선 — 프리셋·지연.</summary>
    Camera,
    /// <summary>스피커 배선 — 음원그룹·반복.</summary>
    Speaker,
    /// <summary>경광등 배선 — 색·점등모드·부저.</summary>
    Lamp,
}

/// <summary>보드 행이 저장 때 무엇이 되는가. 고아 여부는 <b>직교</b>라 여기 섞지 않는다.</summary>
public enum MappingDraftState
{
    /// <summary>서버와 같다 — 보낼 것이 없다.</summary>
    Pristine,
    /// <summary>드롭으로 새로 들어왔다 — 벌크 등록 대상.</summary>
    Added,
    /// <summary>값 또는 순서가 바뀌었다 — PATCH 대상.</summary>
    Edited,
    /// <summary>해제 표시 — 벌크 해제 대상. <b>화면에서 사라지지 않고</b> 취소선으로 남는다.</summary>
    Removed,
}

/// <summary>
/// 한 줄에 쓸 장비 정보 — <b>팔레트와 이름 조인이 함께 쓰는 유일한 모양</b>.
/// </summary>
/// <remarks>
/// 서버 연동 응답은 장비를 <c>{id, category_device}</c> 2키로만 준다. 이름·그룹·운용여부는
/// <b>장비 캐시에서 조인</b>해야 나온다 — 그 캐시의 한 행이 이 타입이다.
/// </remarks>
/// <param name="Id">장비 id(전역 유일).</param>
/// <param name="Name">표시 이름. 캐시에 없으면 화면이 <c>#id</c> 로 적는다.</param>
/// <param name="TypeText">종류 표기(모델·타입축) — 팔레트 부제.</param>
/// <param name="GroupIds">소속 장비그룹 id 집합 — 매핑의 그룹과 대조해 "소속 불일치" 경고를 낸다.</param>
/// <param name="IsEnabled">장비 자체의 운용 여부 — <c>false</c> 면 행을 흐리게 적는다.</param>
public sealed record MappingDeviceInfo(
    int Id,
    string Name,
    string TypeText,
    IReadOnlyList<int> GroupIds,
    bool IsEnabled);

/// <summary>프리셋 후보 1건 — 카메라를 고른 뒤 <b>그 카메라 것만</b> 받아 온다.</summary>
/// <param name="Id">프리셋 PK.</param>
/// <param name="CameraId">소유 카메라 id — 오배정을 드롭 전에 막는 근거.</param>
/// <param name="Name">표시 이름(<c>preset_name</c>).</param>
/// <param name="IsRestrictedZone">감시금지구역이면 화면이 🔒 를 붙인다.</param>
public sealed record MappingPresetInfo(int Id, int CameraId, string Name, bool IsRestrictedZone);

/// <summary>음원그룹 후보 1건.</summary>
/// <param name="Id">음원그룹 PK.</param>
/// <param name="Name">표시 이름(<c>group_name</c>).</param>
public sealed record MappingFileGroupInfo(int Id, string Name);

/// <summary>장비그룹 후보 1건.</summary>
/// <param name="Id">그룹 PK.</param>
/// <param name="Name">표시 이름.</param>
public sealed record MappingGroupInfo(int Id, string Name);

/// <summary>
/// 팔레트·이름 조인이 쓰는 장비 공급원. 화면은 이 인터페이스만 알고 프로바이더를 모른다
/// (그래서 뷰모델이 헤드리스로 선다).
/// </summary>
public interface IMappingDeviceSource
{
    /// <summary>그 종류의 장비 전량. 팔레트가 어차피 전량을 쓰므로 캐시를 통째로 준다.</summary>
    IReadOnlyList<MappingDeviceInfo> Devices(MappingActionKind kind);

    /// <summary>장비그룹 후보(매핑 속성의 드롭다운).</summary>
    IReadOnlyList<MappingGroupInfo> Groups();

    /// <summary>한 장비 조회 — 캐시 미스면 <c>null</c>(화면은 <c>#id</c> 로 적는다).</summary>
    MappingDeviceInfo? Find(MappingActionKind kind, int deviceId);
}

/// <summary>
/// 워크벤치가 서버와 주고받는 전부. <b>기존 <c>IEventApiService</c> 를 넓히지 않는다</b> —
/// 그 인터페이스의 목·페이크가 레포에 여럿이라 멤버를 더하면 전부 깨진다.
/// </summary>
/// <remarks>
/// 읽기는 관용적으로, 쓰기는 엄격하게 다룬다. 응답 <c>data</c> 가 <c>[]</c> 인지 <c>{items,total}</c> 인지
/// 판본마다 갈려서(배포 v8 은 <c>{items,total}</c>, 문서 v4 는 <c>[]</c>) <b>양쪽을 모두 받는다</b>.
/// </remarks>
public interface IMappingWorkbenchGateway
{
    /// <summary>매핑 목록 — 전량(내부에서 <c>limit=100</c> 으로 페이지를 돈다).</summary>
    Task<MappingCallResult<IReadOnlyList<EventMappingReadDto>>> ListMappingsAsync(CancellationToken token = default);

    /// <summary>매핑 1건 재조회 — 저장 직전 대조(동시 편집 감지)에 쓴다.</summary>
    Task<MappingCallResult<EventMappingReadDto>> GetMappingAsync(int mappingId, CancellationToken token = default);

    /// <summary>카메라 배선 전량.</summary>
    Task<MappingCallResult<IReadOnlyList<MappingCameraReadDto>>> ListCamerasAsync(int mappingId, CancellationToken token = default);

    /// <summary>스피커 배선 전량.</summary>
    Task<MappingCallResult<IReadOnlyList<MappingSpeakerReadDto>>> ListSpeakersAsync(int mappingId, CancellationToken token = default);

    /// <summary>경광등 배선 전량.</summary>
    Task<MappingCallResult<IReadOnlyList<MappingLampReadDto>>> ListLampsAsync(int mappingId, CancellationToken token = default);

    /// <summary>매핑 생성.</summary>
    Task<MappingCallResult<EventMappingReadDto>> CreateMappingAsync(EventMappingCreateDto body, CancellationToken token = default);

    /// <summary>매핑 부분 수정(PATCH 전용 — PUT 은 쓰지 않는다).</summary>
    Task<MappingCallResult<EventMappingReadDto>> PatchMappingAsync(int mappingId, EventMappingUpdateDto body, CancellationToken token = default);

    /// <summary>배선 벌크 등록(1~100). 투입은 <b>반드시 이 경로로만</b> — 단건 POST 는 중복 시 409 다.</summary>
    Task<MappingCallResult<MappingBulkCreateResultDto>> BulkCreateAsync(int mappingId, MappingActionKind kind, IReadOnlyList<object> items, CancellationToken token = default);

    /// <summary>배선 벌크 해제(1~100).</summary>
    Task<MappingCallResult<MappingBulkUnassignResultDto>> BulkUnassignAsync(int mappingId, MappingActionKind kind, IReadOnlyList<int> configIds, CancellationToken token = default);

    /// <summary>배선 1행 부분 수정. 순서 저장도 이 경로다 — 서버에 재정렬 API 가 없다.</summary>
    Task<MappingCallResult<bool>> PatchConfigAsync(int mappingId, MappingActionKind kind, int configId, object body, CancellationToken token = default);

    /// <summary>그 카메라 소유 프리셋만. 전역 선적재는 하지 않는다(500대 × N 폭발).</summary>
    Task<MappingCallResult<IReadOnlyList<MappingPresetInfo>>> ListPresetsAsync(int cameraId, CancellationToken token = default);

    /// <summary>음원그룹 후보.</summary>
    Task<MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>> ListFileGroupsAsync(CancellationToken token = default);
}

/// <summary>
/// 한 번의 서버 왕복 결과. <b>실패 사유를 사용자 문구와 원문으로 갈라서</b> 들고 온다.
/// </summary>
/// <typeparam name="T">본문 타입.</typeparam>
/// <remarks>
/// 🔴 <see cref="RawError"/> 는 서버 원문(영문·스택)이라 <b>화면에 그대로 내지 않는다</b>.
/// 화면은 <see cref="Message"/>(한국어)만 쓰고 원문은 툴팁·로그로 내린다.
/// </remarks>
public sealed class MappingCallResult<T>
{
    private MappingCallResult(bool ok, T? value, string message, string? rawError, int statusCode)
    {
        IsSuccess = ok;
        Value = value;
        Message = message;
        RawError = rawError;
        StatusCode = statusCode;
    }

    /// <summary>성공 여부.</summary>
    public bool IsSuccess { get; }

    /// <summary>본문. 실패면 <c>default</c>.</summary>
    public T? Value { get; }

    /// <summary>사용자에게 보일 <b>한국어</b> 한 줄.</summary>
    public string Message { get; }

    /// <summary>서버·예외 원문 — 진단용. 화면 본문에 쓰지 않는다.</summary>
    public string? RawError { get; }

    /// <summary>HTTP 상태 코드(0 = 못 보냄).</summary>
    public int StatusCode { get; }

    /// <summary>권한 부족(403) 또는 인증 만료(401)인가 — Draft 를 <b>버리지 않고</b> 안내만 하는 분기.</summary>
    public bool IsAuthProblem => StatusCode is 401 or 403;

    /// <summary>성공 결과.</summary>
    public static MappingCallResult<T> Ok(T value, int statusCode = 200)
        => new(true, value, string.Empty, null, statusCode);

    /// <summary>실패 결과.</summary>
    public static MappingCallResult<T> Fail(string message, string? rawError = null, int statusCode = 0)
        => new(false, default, message, rawError, statusCode);
}

/// <summary>
/// 워크벤치를 여는 입구. 이벤트 콘솔은 이 인터페이스만 알고, 실패하면 <b>입구만 감춘다</b>.
/// </summary>
public interface IMappingWorkbenchLauncher
{
    /// <summary>이 서버 판본에서 워크벤치를 낼 수 있는가(연동 계약은 v7.0 이상에서만 이 모양이다).</summary>
    bool IsAvailable { get; }

    /// <summary>워크벤치 창을 연다.</summary>
    Task OpenAsync();
}

/// <summary>종류 축에 붙는 표기 — 화면 어디서나 같은 한국어를 쓴다.</summary>
public static class MappingKindText
{
    /// <summary>한국어 라벨.</summary>
    public static string Label(MappingActionKind kind) => kind switch
    {
        MappingActionKind.Camera => "카메라",
        MappingActionKind.Speaker => "스피커",
        MappingActionKind.Lamp => "경광등",
        _ => "알 수 없음",
    };

    /// <summary>서버 경로 조각(<c>cameras</c>·<c>speakers</c>·<c>lamps</c>).</summary>
    public static string Segment(MappingActionKind kind) => kind switch
    {
        MappingActionKind.Camera => "cameras",
        MappingActionKind.Speaker => "speakers",
        MappingActionKind.Lamp => "lamps",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>드롭존 키 — 보드 쪽.</summary>
    public static string BoardZone(MappingActionKind kind) => "mapping-board-" + Segment(kind);

    /// <summary>드롭존 키 — 팔레트(해제) 쪽.</summary>
    public const string PaletteZone = "mapping-palette";

    /// <summary>장비 카테고리 → 종류 축. 알 수 없으면 <c>null</c>.</summary>
    public static MappingActionKind? FromCategory(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Camera => MappingActionKind.Camera,
        EnumDeviceCategory.Speaker => MappingActionKind.Speaker,
        EnumDeviceCategory.Lamp => MappingActionKind.Lamp,
        _ => null,
    };
}
