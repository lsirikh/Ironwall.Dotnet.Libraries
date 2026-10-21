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

/// <summary>
/// 스피커 연동 칸의 음원그룹 후보 공급원(<c>GET /api/file-groups</c>, GOP API 8.0.15 대조 G25).
/// </summary>
/// <remarks>
/// 권한(<c>files:view</c>)이 없거나(403) 경로가 없는 서버(404)면 실패로 돌려준다 — 화면은 "목록을 볼 수 없음" 을 알리고 번호 입력으로 받는다.
/// </remarks>
public interface IMappingFileGroupSource
{
    /// <summary>음원그룹 전량(내부에서 쪽을 넘겨 끝까지 모은다).</summary>
    Task<MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>> ListFileGroupsAsync(CancellationToken token = default);
}

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

}

/// <summary>
/// 카메라 배선에 넣을 프리셋 후보를 받아 온다 — <b><see cref="IMappingWorkbenchGateway"/> 와 따로 둔다</b>.
/// </summary>
/// <remarks>
/// 게이트웨이 인터페이스에 멤버를 더하면 레포의 목·페이크가 전부 깨진다(<c>CountingGateway</c> 등).
/// 실제 구현(<see cref="MappingWorkbenchGateway"/>)이 이것도 함께 구현하고, 뷰모델은 없으면 편집 칸만 "불러올 수 없음" 으로 둔다.
/// </remarks>
public interface IMappingPresetSource
{
    /// <summary>그 카메라의 프리셋 전량(<c>GET /devices/cameras/{id}/presets</c>).</summary>
    Task<MappingCallResult<IReadOnlyList<MappingPresetInfo>>> ListPresetsAsync(int cameraId, CancellationToken token = default);
}

/// <summary>프리셋 관리 창의 한 줄 — 서버 프리셋 레코드 그대로.</summary>
/// <param name="Id">프리셋 PK.</param>
/// <param name="CameraId">소유 카메라 id.</param>
/// <param name="PresetIndex">카메라의 프리셋 번호(<c>preset_index</c>).</param>
/// <param name="Name">이름(<c>preset_name</c>).</param>
/// <param name="TouringTime">투어링 시간(초, <c>touring_time</c>).</param>
/// <param name="IsRestrictedZone">감시금지구역.</param>
public sealed record MappingPresetDetail(int Id, int CameraId, int PresetIndex, string Name, int TouringTime, bool IsRestrictedZone)
{
    /// <summary>목록에 그리는 글자.</summary>
    public string Display => IsRestrictedZone ? $"{PresetIndex}. 🔒 {Name}" : $"{PresetIndex}. {Name}";

    /// <summary>목록 부제.</summary>
    public string Subtitle => $"투어링 {TouringTime}초";
}

/// <summary>프리셋 생성 · 수정에 보낼 값.</summary>
public sealed record MappingPresetDraft(int PresetIndex, string Name, int TouringTime, bool IsRestrictedZone);

/// <summary>
/// 카메라 프리셋 관리(<c>/devices/cameras/{id}/presets</c> CRUD) — <b>다른 인터페이스를 넓히지 않고</b> 따로 둔다.
/// </summary>
/// <remarks>실제 구현은 <see cref="MappingWorkbenchGateway"/> 가 함께 진다.</remarks>
public interface IMappingPresetEditor
{
    /// <summary>그 카메라의 프리셋 전량(번호 순).</summary>
    Task<MappingCallResult<IReadOnlyList<MappingPresetDetail>>> ListPresetDetailsAsync(int cameraId, CancellationToken token = default);

    /// <summary>프리셋 생성.</summary>
    Task<MappingCallResult<MappingPresetDetail>> CreatePresetAsync(int cameraId, MappingPresetDraft draft, CancellationToken token = default);

    /// <summary>프리셋 부분 수정(PATCH).</summary>
    Task<MappingCallResult<MappingPresetDetail>> PatchPresetAsync(int cameraId, int presetId, MappingPresetDraft draft, CancellationToken token = default);

    /// <summary>프리셋 삭제.</summary>
    Task<MappingCallResult<bool>> DeletePresetAsync(int cameraId, int presetId, CancellationToken token = default);
}

/// <summary>ROI 꼭짓점 1개 — 화면은 언제나 <b>0~1 정규화</b> 좌표로 다룬다(왼쪽 위가 0,0).</summary>
/// <param name="X">가로(0~1).</param>
/// <param name="Y">세로(0~1).</param>
public readonly record struct MappingRoiPoint(double X, double Y);

/// <summary>프리셋 ROI 설정 창의 한 줄 — 서버 ROI 레코드(점 포함).</summary>
/// <param name="Id">ROI PK.</param>
/// <param name="PresetId">소유 프리셋 id.</param>
/// <param name="Name">이름.</param>
/// <param name="ResolutionWidth">기준 해상도 가로.</param>
/// <param name="ResolutionHeight">기준 해상도 세로.</param>
/// <param name="IsEnable">사용 여부.</param>
/// <param name="IsExclusion">제외구역 여부 — 서버가 이 키를 주지 않았으면 <c>null</c>(구판). 저장 때 값이 있을 때만 되돌려 싣는다.</param>
/// <param name="Points">꼭짓점(정규화, 그리는 순서).</param>
/// <param name="IsPixelUnit">서버 원본이 픽셀 좌표였나 — 저장 때 같은 단위로 되돌린다.</param>
public sealed record MappingRoiDetail(
    int Id,
    int PresetId,
    string Name,
    double ResolutionWidth,
    double ResolutionHeight,
    bool IsEnable,
    bool? IsExclusion,
    IReadOnlyList<MappingRoiPoint> Points,
    bool IsPixelUnit = false)
{
    /// <summary>목록에 그리는 글자.</summary>
    public string Display => IsEnable ? Name : $"{Name} (꺼짐)";

    /// <summary>목록 부제.</summary>
    public string Subtitle => $"점 {Points.Count}개 · {ResolutionWidth:0}×{ResolutionHeight:0}";
}

/// <summary>ROI 생성 · 교체(PUT)에 보낼 값. <see cref="Points"/> 는 정규화 좌표.</summary>
public sealed record MappingRoiDraft(
    string Name,
    double ResolutionWidth,
    double ResolutionHeight,
    bool IsEnable,
    bool? IsExclusion,
    IReadOnlyList<MappingRoiPoint> Points,
    bool IsPixelUnit = false);

/// <summary>
/// 프리셋 ROI 관리(<c>/presets/{id}/rois</c>) — <b>다른 인터페이스를 넓히지 않고</b> 따로 둔다.
/// </summary>
/// <remarks>실제 구현은 <see cref="MappingWorkbenchGateway"/> 가 함께 진다. 점은 ROI 본문에 함께 실려 한 번에 바뀐다(PUT 전체 교체).</remarks>
public interface IMappingRoiEditor
{
    /// <summary>그 프리셋의 ROI 전량(점 포함).</summary>
    Task<MappingCallResult<IReadOnlyList<MappingRoiDetail>>> ListRoisAsync(int presetId, CancellationToken token = default);

    /// <summary>ROI 생성(점 3개 이상).</summary>
    Task<MappingCallResult<MappingRoiDetail>> CreateRoiAsync(int presetId, MappingRoiDraft draft, CancellationToken token = default);

    /// <summary>ROI 전체 교체(PUT — 점까지).</summary>
    Task<MappingCallResult<MappingRoiDetail>> ReplaceRoiAsync(int presetId, int roiId, MappingRoiDraft draft, CancellationToken token = default);

    /// <summary>ROI 삭제.</summary>
    Task<MappingCallResult<bool>> DeleteRoiAsync(int presetId, int roiId, CancellationToken token = default);
}

/// <summary>ROI 다각형 판정 — 화면 · 시험이 함께 쓴다.</summary>
public static class MappingRoiGeometry
{
    /// <summary>다각형 최소 꼭짓점 수(서버 <c>min_length=3</c>).</summary>
    public const int MIN_POINTS = 3;

    /// <summary>한 ROI 의 최대 꼭짓점 수(화면 한도).</summary>
    public const int MAX_POINTS = 64;

    /// <summary>0~1 로 자른다.</summary>
    public static MappingRoiPoint Clamp(MappingRoiPoint p) => new(Clamp01(p.X), Clamp01(p.Y));

    private static double Clamp01(double v) => double.IsNaN(v) ? 0 : v < 0 ? 0 : v > 1 ? 1 : v;

    /// <summary>
    /// 닫힌 다각형의 변끼리 서로 가로지르는가(이웃 변의 공유 꼭짓점은 빼고 본다).
    /// </summary>
    public static bool IsSelfIntersecting(IReadOnlyList<MappingRoiPoint> points)
    {
        int n = points.Count;
        if (n < 4) return false;
        for (int i = 0; i < n; i++)
        {
            var a1 = points[i];
            var a2 = points[(i + 1) % n];
            for (int j = i + 1; j < n; j++)
            {
                // 이웃 변(꼭짓점 공유)은 건너뛴다 — 처음 변과 마지막 변도 이웃이다.
                if (j == i || (j + 1) % n == i || (i + 1) % n == j) continue;
                if (SegmentsIntersect(a1, a2, points[j], points[(j + 1) % n])) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 열린 꺾은선에 <paramref name="next"/> 를 이으면 기존 변을 가로지르는가 — 그리는 중 점 추가 판정.
    /// </summary>
    public static bool WouldCross(IReadOnlyList<MappingRoiPoint> points, MappingRoiPoint next)
    {
        int n = points.Count;
        if (n < 3) return false;
        var last = points[n - 1];
        for (int i = 0; i < n - 2; i++)
            if (SegmentsIntersect(points[i], points[i + 1], last, next)) return true;
        return false;
    }

    /// <summary>다각형 넓이(정규화 단위 — 0 이면 한 줄로 늘어선 것).</summary>
    public static double Area(IReadOnlyList<MappingRoiPoint> points)
    {
        double sum = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(sum) / 2;
    }

    private static bool SegmentsIntersect(MappingRoiPoint p1, MappingRoiPoint p2, MappingRoiPoint q1, MappingRoiPoint q2)
    {
        double d1 = Cross(q1, q2, p1), d2 = Cross(q1, q2, p2), d3 = Cross(p1, p2, q1), d4 = Cross(p1, p2, q2);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true;
        return (d1 == 0 && OnSegment(q1, q2, p1)) || (d2 == 0 && OnSegment(q1, q2, p2))
            || (d3 == 0 && OnSegment(p1, p2, q1)) || (d4 == 0 && OnSegment(p1, p2, q2));
    }

    private static double Cross(MappingRoiPoint a, MappingRoiPoint b, MappingRoiPoint c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool OnSegment(MappingRoiPoint a, MappingRoiPoint b, MappingRoiPoint p)
        => Math.Min(a.X, b.X) <= p.X && p.X <= Math.Max(a.X, b.X) && Math.Min(a.Y, b.Y) <= p.Y && p.Y <= Math.Max(a.Y, b.Y);
}

/// <summary>카메라 자신이 기억하는 프리셋 1건(ONVIF <c>GetPresets</c>).</summary>
/// <param name="Token">ONVIF 프리셋 토큰 — 이벤트 때 GotoPreset 에 그대로 간다.</param>
/// <param name="Name">카메라에 적힌 이름. 없으면 빈 문자열.</param>
public sealed record MappingCameraPreset(string Token, string Name)
{
    /// <summary>
    /// 토큰을 서버 <c>preset_index</c> 로 쓸 수 있으면 그 번호, 아니면 <c>null</c>.
    /// 이벤트 경로는 <c>preset_index</c> 를 문자열로 바꿔 토큰으로 보내므로 <b>토큰이 1 이상의 정수일 때만</b> 서버에 담을 수 있다.
    /// </summary>
    public int? Index
        => int.TryParse(Token.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 1
           && string.Equals(n.ToString(System.Globalization.CultureInfo.InvariantCulture), Token.Trim(), StringComparison.Ordinal)
            ? n : null;

    /// <summary>목록에 그리는 글자.</summary>
    public string Display => string.IsNullOrWhiteSpace(Name) ? $"토큰 {Token}" : $"{Token}. {Name}";
}

/// <summary>프리셋 관리 창의 "카메라 프리셋" 한 줄 — 서버에 이미 담겼는지를 함께 들고 있다.</summary>
/// <param name="Preset">카메라 프리셋.</param>
/// <param name="IsRegistered">같은 번호의 서버 프리셋이 있나.</param>
public sealed record MappingCameraPresetRow(MappingCameraPreset Preset, bool IsRegistered)
{
    /// <summary>목록에 그리는 글자.</summary>
    public string Display => Preset.Display;

    /// <summary>목록 부제.</summary>
    public string Subtitle => Preset.Index is null ? "번호가 아닌 토큰 — 등록 불가" : IsRegistered ? "서버에 등록됨" : "미등록";
}

/// <summary>
/// 카메라에서 ONVIF 프리셋을 읽는 창구 — 프리셋 관리 창이 서버 프리셋을 <b>카메라 것과 맞춰</b> 만들 때 쓴다.
/// </summary>
/// <remarks>GIS 는 ONVIF 를 직접 부르지 않는다 — 실제 구현은 카메라 팝업 호스트에 <c>GetPresets</c> 를 보낸다.</remarks>
public interface IMappingCameraPresetReader
{
    /// <summary>그 카메라가 기억하는 프리셋 전량. 실패면 사유(한국어)를 담은 실패 결과.</summary>
    Task<MappingCallResult<IReadOnlyList<MappingCameraPreset>>> ReadCameraPresetsAsync(int cameraId, CancellationToken token = default);
}

/// <summary>영상 창구가 보는 카메라 팝업 호스트 상태.</summary>
public enum MappingVideoHostState
{
    /// <summary>명령을 받는다.</summary>
    Running,
    /// <summary>시작 · 재시작 중 — 곧 이어진다.</summary>
    Starting,
    /// <summary>없음 · 일시 중지 · 시작 전 — 영상 기능을 쓸 수 없다(GIS 는 정상).</summary>
    Unavailable,
}

/// <summary>
/// 프리셋 관리 · ROI 설정 창의 카메라 실시간 영상 창구.
/// </summary>
/// <remarks>
/// GIS 는 영상을 직접 재생하지 않는다 — 실제 구현은 카메라 팝업 호스트(별도 프로세스)에 오버레이 스트림을 열고,
/// 호스트가 공유 메모리에 쓴 프레임(<see cref="Ironwall.Dotnet.Libraries.CameraPopup.Frames.IFrameSource"/>)을 돌려준다.
/// 모든 멤버는 던지지 않는다.
/// </remarks>
public interface IMappingCameraVideo
{
    /// <summary>호스트 상태.</summary>
    MappingVideoHostState HostState { get; }

    /// <summary>호스트 상태가 바뀌었다(배경 스레드에서).</summary>
    event EventHandler? HostStateChanged;

    /// <summary>
    /// 영상을 (다시) 연다. 같은 <paramref name="streamId"/> 로 다시 열면 이전 것을 교체한다.
    /// 돌려받은 프레임 원천을 <c>Dispose</c> 하면 스트림이 닫힌다.
    /// </summary>
    MappingCallResult<Ironwall.Dotnet.Libraries.CameraPopup.Frames.IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height);

    /// <summary>
    /// <see cref="OpenVideo(int, string, int, int)"/> 에 상자 꽉 채우기를 더한 것 — <paramref name="fillBox"/> 가 참이면 영상을
    /// 비율과 관계없이 상자(기준 해상도 틀)에 꽉 채워 그린다(ROI 설정 창). 기본 구현은 꽉 채우기를 무시하고 그냥 연다.
    /// </summary>
    MappingCallResult<Ironwall.Dotnet.Libraries.CameraPopup.Frames.IFrameSource> OpenVideo(int cameraId, string streamId, int width, int height, bool fillBox)
        => OpenVideo(cameraId, streamId, width, height);

    /// <summary>카메라를 프리셋 자리로 움직일 수 있는 카메라인가(PTZ).</summary>
    bool CanGotoPreset(int cameraId);

    /// <summary>카메라를 그 번호의 프리셋 자리로 움직인다(ONVIF GotoPreset — 이벤트 경로와 같은 토큰).</summary>
    Task<MappingCallResult<bool>> GotoPresetAsync(int cameraId, int presetIndex, CancellationToken token = default);

    /// <summary>
    /// 카메라 메인 스트림 해상도(ONVIF 프로파일 중 최대) — 새 ROI 의 기준 해상도를 채운다.
    /// 못 읽으면 실패 결과(호출 쪽은 기본 해상도로 폴백). 기본 구현은 "읽을 수 없음".
    /// </summary>
    Task<MappingCallResult<MappingVideoResolution>> ReadVideoResolutionAsync(int cameraId, CancellationToken token = default)
        => Task.FromResult(MappingCallResult<MappingVideoResolution>.Fail("카메라 해상도를 읽을 수 없습니다."));
}

/// <summary>카메라 영상 해상도(px).</summary>
/// <param name="Width">가로.</param>
/// <param name="Height">세로.</param>
public sealed record MappingVideoResolution(int Width, int Height);

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
