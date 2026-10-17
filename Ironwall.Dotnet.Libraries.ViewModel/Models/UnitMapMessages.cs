using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.ViewModel.Models;
/****************************************************************************
   Purpose      : 부대 관계도 ↔ GIS 지도 ↔ 부대 콘솔 공용 메시지 (unit-relationship-map §3.5 · FR-44 · FR-46 · FR-49 · FR-53)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

// ⚠ UnitTopologyChangedMessage 는 여기 두지 않는다 — CommonMessages.cs 에 병렬 작업(SYNC_UNIT 라우팅)이 이미 정의했다.
//   이 파일을 따로 둔 까닭: 그 작업이 CommonMessages.cs 를 동시에 고치고 있어 같은 파일을 만지면 충돌한다.

/// <summary>
/// "이 장비들을 지도에서 보여 달라" — 부대 콘솔 [지도에서 보기] · 이벤트 상세가 보내고 지도(<c>MapViewModel</c>)가 받는다.
/// </summary>
/// <param name="RequestId">회신(<see cref="MapLocateResult"/>)을 맞출 표지.</param>
/// <param name="DeviceIds">장비 <b>서버 id</b>(양수 · 겹침 없음 · 오름차순). 지도는 심볼의 <c>LinkedDevice.Id</c> 와 대조한다(probe log V-08).</param>
/// <param name="Title">회신 문구에 쓰는 이름(예: "7중대").</param>
/// <remarks>장비 id 기반 일반형이다(D-7) — 부대 → 장비 풀이는 장비 목록을 가진 쪽이 한다. 수신 스레드는 정하지 않는다.</remarks>
public sealed record MapLocateRequest(Guid RequestId, IReadOnlyList<int> DeviceIds, string Title)
{
    /// <summary>유효한(양수) 장비만 겹침 없이 오름차순으로 담는다. 남는 것이 없으면 <c>null</c> — 보낼 것이 없다.</summary>
    public static MapLocateRequest? For(string title, IEnumerable<int>? deviceIds)
    {
        if (deviceIds is null) return null;
        var ids = deviceIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
        return ids.Count == 0 ? null : new MapLocateRequest(Guid.NewGuid(), ids, title ?? string.Empty);
    }
}

/// <summary>지도의 회신 — 요청 장비 중 지도에 보인 수와 없는 수.</summary>
/// <param name="Shown">강조한 장비 수(장비 기준 — 한 장비에 심볼이 둘이어도 1).</param>
/// <param name="Missing">지도에서 보이지 않는 장비 수 — 심볼이 없거나(지도에 없음) 레이어에서 숨김(<paramref name="Hidden"/> 포함).</param>
/// <param name="Hidden"><paramref name="Missing"/> 중 심볼은 있으나 레이어에서 숨긴 수(문구 "숨김 N" 용, 선택).</param>
/// <param name="OutsideAnchor"><paramref name="Shown"/> 중 사이트 고정 구역 밖이라 맞춤에서 빠진 수(선택).</param>
/// <remarks>
/// <para>뒤의 둘은 기본값 0 의 위치 매개변수다 — 세 인자로 만드는 쪽은 그대로 컴파일된다(레인 A 소비처가 이 모양으로 이미 쓴다).</para>
/// <para><b>v1.3 회신 4수</b>(FR-45 · §3.5 · ISSUE-42)는 서로 겹치지 않는 네 묶음이다 — <see cref="ShownInView"/> · <see cref="OutOfAnchor"/> ·
/// <see cref="Hidden"/> · <see cref="NotOnMap"/>. 합 = <see cref="Requested"/>(요청 장비 수). 위치 매개변수(<c>Shown</c> 은 구역 밖 포함 ·
/// <c>Missing</c> 은 숨김 포함)는 그대로 두고 네 묶음을 계산 속성으로 낸다.</para>
/// </remarks>
public sealed record MapLocateResult(Guid RequestId, int Shown, int Missing, int Hidden = 0, int OutsideAnchor = 0)
{
    /// <summary>강조했지만 사이트 고정 구역 밖이라 맞춤에서 빠진 장비 수.</summary>
    public int OutOfAnchor => OutsideAnchor;

    /// <summary>강조했고 맞춘 화면(구역 안)에 보이는 장비 수.</summary>
    public int ShownInView => Math.Max(0, Shown - OutsideAnchor);

    /// <summary>지도에 심볼이 아예 없는 장비 수(숨김 제외).</summary>
    public int NotOnMap => Math.Max(0, Missing - Hidden);

    /// <summary>요청 장비 수 — 네 묶음의 합.</summary>
    public int Requested => Shown + Missing;
}

/// <summary>지도 심볼 [관계도에서 보기] → 부대 콘솔 런처: 창을 열거나 활성화하고 그 부대를 고른다.</summary>
/// <param name="OpenMap">관계도 레일로 열지(<c>false</c> 면 트리 레일).</param>
public sealed record OpenUnitConsoleRequest(int UnitId, bool OpenMap);

/// <summary>
/// 공유 배치 문서가 바뀌었다 — 서버 <c>SYNC_UNIT_LAYOUT</c>(v8.0.4 · 브로커 §9.18, <c>sensorway.global.all.sync.unit-layout</c>)을
/// 호스트가 옮긴다. <see cref="Version"/> = 새 <b>문서 판</b>. 가진 판보다 클 때만 다시 읽는다(FR-53 — 자기 저장 메아리는 건너뛴다).
/// </summary>
public sealed record UnitLayoutChangedMessage(long Version)
{
    /// <summary>
    /// 알림 본문의 판 — <c>resource_id</c>(서버 v8.0.4 확정 키 — 부대 id 가 아니라 <b>새 문서 판</b>)를 먼저, 없으면 <c>version</c>(옛 요청서 키, 과도기 폴백).
    /// 둘 다 없거나 0 이하면 <c>null</c>(보낼 것이 없다).
    /// </summary>
    /// <remarks>분석 ISSUE-2 는 회신 2026-09-29 로 닫혔다 — 본문은 <c>{action:"UPDATED", resource_id:&lt;새 문서 판&gt;}</c>.
    /// <c>version</c> 폴백은 해가 없어 남긴다(SIM-N073 · N074).</remarks>
    public static UnitLayoutChangedMessage? FromBody(long? version, long? resourceId)
        => resourceId is > 0 ? new UnitLayoutChangedMessage(resourceId.Value)
         : version is > 0 ? new UnitLayoutChangedMessage(version.Value)
         : null;
}

/// <summary>배치 알림(<c>SYNC_UNIT_LAYOUT</c>) 본문 읽기(ISSUE-2 — v8.0.4 확정). 호스트(EXT-03)가 case 한 줄로 부른다.</summary>
public static class UnitLayoutNotice
{
    /// <summary>
    /// 본문의 <c>resource_id</c>(서버 v8.0.4 정본 — 새 문서 판)를 먼저, 숫자가 아니거나 0 이하면 <c>version</c>(옛 요청서 키)을 읽는다.
    /// 둘 다 못 쓰면 <c>null</c> + 사유.
    /// </summary>
    /// <param name="versionRaw">본문 <c>version</c> 의 원문(없으면 <c>null</c>).</param>
    /// <param name="resourceIdRaw">본문 <c>resource_id</c> 의 원문.</param>
    /// <param name="rejectReason">버린 사유 — 부르는 쪽(호스트)이 경고 로그 1회로 남긴다. 읽었으면 <c>null</c>.</param>
    /// <remarks>ViewModel 어셈블리는 JSON 라이브러리를 모른다 — 호스트가 <c>item["body"]?["version"]?.ToString()</c> 을 넘긴다.</remarks>
    public static long? TryReadVersion(string? versionRaw, string? resourceIdRaw, out string? rejectReason)
    {
        if (Parse(resourceIdRaw) is long resourceId) { rejectReason = null; return resourceId; }
        if (Parse(versionRaw) is long fallback) { rejectReason = null; return fallback; }

        rejectReason = $"배치 알림 본문에 쓸 수 있는 버전이 없습니다(version='{versionRaw ?? "null"}', resource_id='{resourceIdRaw ?? "null"}') — 버립니다.";
        return null;

        static long? Parse(string? raw)
            => long.TryParse(raw?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
    }
}

/// <summary>[관계도에서 보기] 요청의 결과(ISSUE-43) — 런처가 보내고 요청자(지도 · 심볼 상세)가 받는다.</summary>
public enum OpenUnitConsoleOutcome
{
    /// <summary>창을 열거나 활성화하고 그 부대를 골랐다.</summary>
    Shown,

    /// <summary>창은 앞으로 왔지만 상세에 적용하지 않은 변경이 있어 선택을 옮기지 않았다.</summary>
    BlockedByUnsavedEdit,

    /// <summary>서버가 부대 편제를 갖지 않거나(8.0 미만) 그 부대가 편제에 없다.</summary>
    Unavailable,
}

/// <summary><see cref="OpenUnitConsoleRequest"/> 의 회신 — 말없는 실패를 없앤다(ISSUE-43).</summary>
public sealed record OpenUnitConsoleResult(int UnitId, OpenUnitConsoleOutcome Outcome);

/// <summary>
/// 이 클라이언트가 장비의 소속 부대를 바꿨다 — <c>DeviceProvider</c> 장비 모델의 <c>UnitId</c> 를 고친다(FR-49).
/// </summary>
public sealed record DeviceUnitChangedMessage(int DeviceId, int UnitId)
{
    /// <summary>둘 다 양수일 때만 만든다. 아니면 <c>null</c>.</summary>
    public static DeviceUnitChangedMessage? For(int deviceId, int unitId)
        => deviceId > 0 && unitId > 0 ? new DeviceUnitChangedMessage(deviceId, unitId) : null;
}
