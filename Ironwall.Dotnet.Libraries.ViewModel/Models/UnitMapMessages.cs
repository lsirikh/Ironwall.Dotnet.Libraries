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
/// <param name="Missing">지도에 심볼이 없는 장비 수.</param>
public sealed record MapLocateResult(Guid RequestId, int Shown, int Missing);

/// <summary>지도 심볼 [관계도에서 보기] → 부대 콘솔 런처: 창을 열거나 활성화하고 그 부대를 고른다.</summary>
/// <param name="OpenMap">관계도 레일로 열지(<c>false</c> 면 트리 레일).</param>
public sealed record OpenUnitConsoleRequest(int UnitId, bool OpenMap);

/// <summary>
/// 공유 배치 문서가 바뀌었다 — 서버 <c>SYNC_UNIT_LAYOUT</c>(S-1 ⑥)을 호스트가 옮긴다. 가진 버전보다 클 때만 다시 읽는다(FR-53).
/// </summary>
public sealed record UnitLayoutChangedMessage(long Version);

/// <summary>
/// 이 클라이언트가 장비의 소속 부대를 바꿨다 — <c>DeviceProvider</c> 장비 모델의 <c>UnitId</c> 를 고친다(FR-49).
/// </summary>
public sealed record DeviceUnitChangedMessage(int DeviceId, int UnitId)
{
    /// <summary>둘 다 양수일 때만 만든다. 아니면 <c>null</c>.</summary>
    public static DeviceUnitChangedMessage? For(int deviceId, int unitId)
        => deviceId > 0 && unitId > 0 ? new DeviceUnitChangedMessage(deviceId, unitId) : null;
}
